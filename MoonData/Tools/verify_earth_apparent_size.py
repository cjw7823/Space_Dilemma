"""Independent Earth angular diameter and pinhole screen-size check; optional fresh Horizons query."""
import argparse
import csv
import json
import math
from pathlib import Path
import urllib.parse
import urllib.request

root=Path(__file__).resolve().parents[2]
out=root/'QA/StarDesign'; out.mkdir(parents=True,exist_ok=True)
parser=argparse.ArgumentParser(); parser.add_argument('--horizons',action='store_true'); args=parser.parse_args()
data=json.loads((root/'Assets/MoonEnvironment/Data/Site6Celestial.json').read_text())
if args.horizons:
    query=dict(urllib.parse.parse_qsl(urllib.parse.urlsplit((root/'QA/Celestial/horizons-query.txt').read_text()).query))
    query['QUANTITIES']="'13,20'"
    url='https://ssd.jpl.nasa.gov/api/horizons.api?'+urllib.parse.urlencode(query)
    response=json.loads(urllib.request.urlopen(url,timeout=60).read())
    (out/'earth-size-horizons.json').write_text(json.dumps(response,indent=2),encoding='utf8')
    (out/'earth-size-query.txt').write_text(url,encoding='utf8')
response=json.loads((out/'earth-size-horizons.json').read_text(encoding='utf8'))
if 'error' in response: raise RuntimeError(response['error'])
rows=list(csv.reader(response['result'].split('$$SOE')[1].split('$$EOE')[0].strip().splitlines()))
report=[]
for sample,row in zip([data['samples'][0],data['samples'][288],data['samples'][-1]],rows):
    # JPL physical-parameters Earth equatorial radius; independent of the bake's 6378.137 constant.
    angular=math.degrees(2*math.asin(6378.1366/sample['distanceKm']))
    horizons=float(row[3])/3600
    pixels=lambda h:h*math.tan(math.radians(angular/2))/math.tan(math.radians(60/2))
    assert abs(angular-horizons)<1e-6
    report.append({'utc':row[0].strip(),'distanceKm':sample['distanceKm'],
        'formulaDiameterDegrees':angular,'horizonsDiameterDegrees':horizons,
        'differenceArcseconds':(angular-horizons)*3600,
        'pixelsAt720pVerticalFov60':pixels(720),'pixelsAt1080pVerticalFov60':pixels(1080),
        'diameterRelativeToSun053':angular/0.53})
(out/'earth-size-independent.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(report,indent=2))
