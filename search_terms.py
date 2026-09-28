with open("get_ws.py") as f:
    pass

import urllib.request, re
url = "https://comboios.ruicosta.pt/assets/index-cjj4V17Y.js"
req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
with urllib.request.urlopen(req) as resp:
    js = resp.read().decode("utf-8")

for term in ["infraestruturas", "api.cp.pt", "cp.pt", "train", "comboio", "delay", "stations", "departures", "horarios"]:
    matches = re.findall(rf'[^"\'`]{{0,30}}{term}[^"\'`]{{0,50}}', js, re.IGNORECASE)
    print(f"Term '{term}': {len(matches)} matches")
    for m in matches[:5]:
        print("   ", m.strip())
