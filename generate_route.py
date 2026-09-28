import urllib.request
import json

start = '-8.6111,41.1475'  # Aliados
end = '-8.6210,41.2277'    # Maia
url = f'http://router.project-osrm.org/route/v1/driving/{start};{end}?geometries=geojson&overview=full'

req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
try:
    with urllib.request.urlopen(req) as response:
        data = json.loads(response.read().decode())
        coords = data['routes'][0]['geometry']['coordinates']
        # downsample if too large, take every 5th point
        sampled = coords[::5]
        # ensure last point is included
        if coords[-1] not in sampled:
            sampled.append(coords[-1])
        
        csharp_array = 'return new List<double[]> { \n'
        for c in sampled:
            # osrm is lon, lat -> we need lat, lon
            csharp_array += f'    new[] {{ {c[1]:.5f}, {c[0]:.5f} }},\n'
        csharp_array = csharp_array.rstrip(',\n') + '\n};'
        print(csharp_array)
except Exception as e:
    print(e)
