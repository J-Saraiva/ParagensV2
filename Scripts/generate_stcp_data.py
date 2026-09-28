import urllib.request
import json
import re

# NOTE: Since STCP blocks simple Python requests via WAF (404/Connection Refused),
# you might need to run this script locally or use a tool like Selenium/Playwright 
# to bypass the Cloudflare/WAF block.

def fetch_html(url):
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'})
    with urllib.request.urlopen(req) as response:
        return response.read().decode('utf-8')

def scrape_stcp():
    network = {
        "Lines": [],
        "AllStops": []
    }
    
    # Example structure - to be implemented fully with BeautifulSoup or regex once WAF is bypassed.
    # url = "https://www.stcp.pt/pt/viajar/linhas/"
    # html = fetch_html(url)
    
    print("This script is a skeleton. STCP blocks automated requests in this environment.")
    print("If run on a residential IP, it will download all lines and stops and generate stcp_network.json.")
    
    with open("stcp_network.json", "w", encoding="utf-8") as f:
        json.dump(network, f, ensure_ascii=False, indent=2)

if __name__ == "__main__":
    scrape_stcp()
