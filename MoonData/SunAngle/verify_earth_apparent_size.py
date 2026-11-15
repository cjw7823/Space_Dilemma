"""Independent Earth angular diameter and pinhole screen-size check; optional fresh Horizons query."""
import argparse
from bisect import bisect_left
import csv
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import urllib.parse
import urllib.request

root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(); parser.add_argument('--horizons',action='store_true'); args=parser.parse_args()
data=json.loads((root/'Assets/MoonEnvironment/Data/Site6Celestial.json').read_text())
start=datetime.fromisoformat(data['startUtc'])
out=root/'QA/Celestial'/start.date().isoformat()
if args.horizons:
    query=dict(urllib.parse.parse_qsl(urllib.parse.urlsplit((out/'horizons-query.txt').read_text()).query))
    query['QUANTITIES']="'13,20'"
    url='https://ssd.jpl.nasa.gov/api/horizons.api?'+urllib.parse.urlencode(query)
    response=json.loads(urllib.request.urlopen(url,timeout=60).read())
    (out/'earth-size-horizons.json').write_text(json.dumps(response,indent=2),encoding='utf8')
    (out/'earth-size-query.txt').write_text(url,encoding='utf8')
else:
    response=json.loads((out/'horizons-response.json').read_text(encoding='utf8'))
if 'error' in response: raise RuntimeError(response['error'])
header, body=response['result'].split('$$SOE',1)
header_row=next(line for line in header.splitlines() if line.lstrip().startswith('Date__(UT)'))
columns=next(csv.reader([header_row]))
diameter_column=[name.strip() for name in columns].index('Ang-diam')
rows=list(csv.reader(body.split('$$EOE')[0].strip().splitlines()))
if len(rows)!=3:
    raise ValueError('Expected three Horizons records for this ephemeris.')
samples=data['samples']
sample_seconds=[sample['seconds'] for sample in samples]
report=[]
duration=data['durationSeconds']
for row,expected in zip(rows,[0,duration/2,duration]):
    utc=datetime.strptime(row[0].strip(),'%Y-%b-%d %H:%M:%S.%f').replace(tzinfo=timezone.utc)
    if abs((utc-start).total_seconds()-expected)>0.001:
        raise ValueError('Horizons response uses a different period. Regenerate it for the active ephemeris.')
    index=bisect_left(sample_seconds,expected)
    if sample_seconds[index]==expected:
        distance=samples[index]['distanceKm']
    else:
        a,b=samples[index-1],samples[index]
        t=(expected-a['seconds'])/(b['seconds']-a['seconds'])
        distance=a['distanceKm']+(b['distanceKm']-a['distanceKm'])*t
    # JPL physical-parameters Earth equatorial radius; independent of the bake's 6378.137 constant.
    angular=math.degrees(2*math.asin(6378.1366/distance))
    horizons=float(row[diameter_column])/3600
    pixels=lambda h:h*math.tan(math.radians(angular/2))/math.tan(math.radians(60/2))
    assert abs(angular-horizons)<1e-6
    report.append({'utc':row[0].strip(),'distanceKm':distance,
        'formulaDiameterDegrees':angular,'horizonsDiameterDegrees':horizons,
        'differenceArcseconds':(angular-horizons)*3600,
        'pixelsAt720pVerticalFov60':pixels(720),'pixelsAt1080pVerticalFov60':pixels(1080),
        'diameterRelativeToSun053':angular/0.53})
(out/'earth-size-independent.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(report,indent=2))
