"""
Moovit Real-Time Proxy Service for ParagensV2
Maintains an authenticated headless Playwright session to bypass AWS WAF,
and exposes local REST endpoints to retrieve live GPS arrivals for UNIR stops.
"""

import os
import sys
import json
import time
import base64
import queue
import threading

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except:
        pass
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs
from playwright.sync_api import sync_playwright

HOST = "0.0.0.0"
PORT = 5000

# Cache of stop mappings: stop_code -> list of {line: "6305", lineId: 3757089, stopId: 36782842}
STOP_CACHE = {
    # Pre-seeded common stops for quick instant responses
    "mai:621": [{"line": "6305", "lineId": 3757089, "stopId": 36782842, "direction": 0}],
    "mai:401": [{"line": "6305", "lineId": 3786201, "stopId": 315760910, "direction": 1}],
}

LINE_URLS = {
    "6305": [
        "https://moovitapp.com/index/pt/transportes_p%C3%BAblicos-line-6305-Porto-1904-3757089-181502237-0",
        "https://moovitapp.com/index/pt/transportes_p%C3%BAblicos-line-6305-Porto-1904-3786201-315760910-1"
    ]
}

# Request queue for Playwright thread
req_queue = queue.Queue()
is_ready = False

def playwright_worker():
    global is_ready
    print("[Proxy] Starting Playwright headless browser...")
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(
            user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36"
        )
        page = context.new_page()
        
        print("[Proxy] Navigating to Moovit Porto to acquire AWS WAF token...")
        try:
            page.goto("https://moovitapp.com/index/pt/transportes_p%C3%BAblicos-line-6305-Porto-1904-3757089-181502237-0", wait_until="networkidle", timeout=60000)
            # Dismiss cookie banner
            try:
                accept_btn = page.locator("#onetrust-accept-btn-handler")
                if accept_btn.is_visible(timeout=3000):
                    accept_btn.click()
            except:
                pass
            print("[Proxy] WAF token acquired! Ready for requests.")
            is_ready = True
        except Exception as e:
            print(f"[Proxy] Initial navigation warning: {e}")
            is_ready = True

        while True:
            cmd, args, resp_q = req_queue.get()
            if cmd == "stop":
                break
            elif cmd == "get_arrivals":
                stop_code = args.get("stop", "").lower()
                results = []
                
                # Check mapping for this stop
                mappings = STOP_CACHE.get(stop_code, [])
                
                # If not in cache, discover dynamically if line is provided
                if not mappings and "line" in args:
                    req_line = args["line"]
                    try:
                        line_url = f"https://moovitapp.com/index/pt/transportes_p%C3%BAblicos-line-{req_line}-Porto-1904"
                        page.goto(line_url, wait_until="networkidle", timeout=30000)
                        raw_b64 = page.evaluate("() => window.__HydratedState__")
                        if raw_b64:
                            model_data = json.loads(base64.b64decode(raw_b64).decode('utf-8')).get("model", {})
                            cur_line_id = model_data.get("agencyId") or model_data.get("pageId")
                            for s in model_data.get("stops", []):
                                sc = s.get("stopCode", "").lower()
                                sid = s.get("id")
                                for code_part in sc.split("|"):
                                    c_clean = code_part.strip().lower()
                                    if c_clean:
                                        if c_clean not in STOP_CACHE:
                                            STOP_CACHE[c_clean] = []
                                        STOP_CACHE[c_clean].append({"line": req_line, "lineId": cur_line_id, "stopId": sid})
                            mappings = STOP_CACHE.get(stop_code, [])
                    except Exception as ex:
                        print(f"[Proxy] Discovery error for line {req_line}: {ex}")

                for m in mappings:
                    line_id = m.get("lineId")
                    stop_id = m.get("stopId")
                    line_num = m.get("line")
                    if not line_id or not stop_id:
                        continue
                    
                    try:
                        js_query = f"""
                        async () => {{
                            try {{
                                const res = await fetch('https://moovitapp.com/api/lines/linearrival', {{
                                    method: 'POST',
                                    headers: {{
                                        'moovit_app_type': 'WEB_TRIP_PLANNER',
                                        'moovit_client_version': '5.151.2/V567',
                                        'moovit_customer_id': '4908',
                                        'moovit_metro_id': '1904',
                                        'moovit_phone_type': '2',
                                        'moovit_user_key': 'F36627',
                                        'moovit_gtfs_language': 'PT',
                                        'content-type': 'application/json'
                                    }},
                                    body: JSON.stringify({{
                                        stopId: {stop_id},
                                        lineIds: JSON.stringify({{ ids: [{line_id}] }})
                                    }}),
                                    credentials: 'include'
                                }});
                                if (!res.ok) return null;
                                return await res.json();
                            }} catch (e) {{
                                return null;
                            }}
                        }}
                        """
                        api_res = page.evaluate(js_query)
                        if api_res and isinstance(api_res, list) and len(api_res) > 0:
                            item = api_res[0]
                            line_arr = item.get("lineArrivals", {})
                            arr_list = line_arr.get("arrivals", [])
                            now_ms = int(time.time() * 1000)
                            for a in arr_list:
                                static_ms = a.get("staticEtdUTC", 0)
                                rt_ms = a.get("rtEtdUTC", static_ms)
                                duration_sec = a.get("durationInSeconds", 0)
                                
                                # calculate real-time minutes
                                rt_mins = int(duration_sec // 60) if duration_sec > 0 else int(max(0, (rt_ms - now_ms) // 60000))
                                delay_mins = int(round((rt_ms - static_ms) / 60000)) if (rt_ms and static_ms) else 0
                                
                                static_time_str = time.strftime('%H:%M', time.localtime(static_ms / 1000)) if static_ms else ""
                                
                                results.append({
                                    "line": line_num,
                                    "stopId": stop_id,
                                    "realtimeMinutes": rt_mins,
                                    "delayMinutes": delay_mins,
                                    "scheduledTime": static_time_str,
                                    "isRealtime": True
                                })
                    except Exception as ex:
                        print(f"[Proxy] Fetch error for line {line_num}: {ex}")

                resp_q.put({"stop": stop_code, "arrivals": results})
                req_queue.task_done()

        browser.close()

class ProxyHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        # Concise logging
        sys.stdout.write(f"[{time.strftime('%H:%M:%S')}] {self.command} {self.path}\n")

    def do_GET(self):
        parsed = urlparse(self.path)
        
        # Enable CORS for local app
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "*")
        
        if parsed.path == "/health":
            self.end_headers()
            resp = json.dumps({"status": "ok", "ready": is_ready, "time": time.time()})
            self.wfile.write(resp.encode("utf-8"))
            return
            
        elif parsed.path == "/arrivals":
            params = parse_qs(parsed.query)
            stop = params.get("stop", [""])[0].strip()
            line = params.get("line", [""])[0].strip()
            
            if not stop:
                self.end_headers()
                self.wfile.write(b'{"arrivals": []}')
                return
                
            resp_q = queue.Queue()
            req_queue.put(("get_arrivals", {"stop": stop, "line": line}, resp_q))
            try:
                data = resp_q.get(timeout=10)
                self.end_headers()
                self.wfile.write(json.dumps(data).encode("utf-8"))
            except queue.Empty:
                self.end_headers()
                self.wfile.write(b'{"arrivals": [], "error": "timeout"}')
            return
            
        else:
            self.end_headers()
            self.wfile.write(b'{"error": "not found"}')

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "*")
        self.end_headers()

def run_server():
    t = threading.Thread(target=playwright_worker, daemon=True)
    t.start()
    
    server = HTTPServer((HOST, PORT), ProxyHandler)
    print(f"\n=======================================================")
    print(f"[Proxy] Moovit Live Proxy ativo em http://localhost:{PORT}")
    print(f"   Endpoint de teste: http://localhost:{PORT}/health")
    print(f"   Chegadas de autocarro: http://localhost:{PORT}/arrivals?stop=mai:621")
    print(f"=======================================================\n")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n[Proxy] Encerrando servidor...")
        req_queue.put(("stop", {}, None))

if __name__ == "__main__":
    run_server()
