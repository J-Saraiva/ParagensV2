import re
import json

with open(r"C:\Users\João Saraiva\.gemini\antigravity-ide\brain\5b7740eb-2152-4c80-8dcc-5687d22d838e\.system_generated\steps\403\content.md", encoding="utf-8") as f:
    html = f.read()

# Pattern for line blocks:
# <a href="/pt/linhas/200">
#     <span class="line-number csp-inline-4" >200</span>
#     <div class="line-name">BOLHÃO - CAST.QUEIJO</div>
# </a>

# Also extract line color styles from CSS:
# .csp-inline-4 { --linebgcolor: #187EC2 !important; --textcolor: #FFFFFF !important }
color_map = {}
css_matches = re.findall(r'\.(csp-inline-\d+)\s*\{\s*--linebgcolor:\s*([^!]+)!important', html)
for cls, col in css_matches:
    color_map[cls.strip()] = col.strip()

matches = re.findall(r'<a href="/pt/linhas/([^"]+)">\s*<span class="line-number\s+([^"]+)"\s*>([^<]+)</span>\s*<div class="line-name">([^<]+)</div>', html)

lines = []
for href_num, cls, span_num, name in matches:
    cls_clean = cls.strip()
    color = color_map.get(cls_clean, "#2196F3")
    lines.append({
        "LineNumber": span_num.strip(),
        "Name": name.strip(),
        "Color": color,
        "RouteShape": [],
        "Stops": []
    })

print(f"Extracted {len(lines)} lines from STCP website!")
for l in lines[:10]:
    print(l)

with open("extracted_lines.json", "w", encoding="utf-8") as f:
    json.dump(lines, f, ensure_ascii=False, indent=2)
