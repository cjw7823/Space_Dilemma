"""Build offline, five-minute lunar-site ephemerides using NASA/JPL SPICE.

Development dependency: spiceypy==8.0.2, numpy. Kernels stay in ignored Temp,
only the sampled JSON and provenance are imported by Unity. No runtime network.
"""
from __future__ import annotations
import argparse
from concurrent.futures import ThreadPoolExecutor
import csv
from datetime import datetime, timezone, timedelta
import hashlib
import json
import math
from pathlib import Path
import sys
import urllib.parse
import urllib.request
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Temp/CelestialDeps'))
import spiceypy as spice

BASE = 'https://naif.jpl.nasa.gov/pub/naif/generic_kernels/'
KERNELS = [
    'lsk/naif0012.tls', 'pck/pck00011.tpc', 'spk/planets/de440s.bsp',
    'pck/moon_pa_de440_200625.bpc', 'fk/satellites/moon_de440_250416.tf',
    'pck/earth_000101_270208_261113.bpc',
]
OUT = ROOT / 'Assets/MoonEnvironment/Data/Site6Celestial.json'
QA = ROOT / 'QA/Celestial'
P = np.array([[1.,0,0],[0,0,1],[0,1,0]])  # RH SPICE xyz -> Unity-friendly xzy

def unit(v):
    return np.asarray(v, dtype=float) / np.linalg.norm(v)

def vec(v):
    return dict(zip(('x','y','z'), map(float,v)))

def quat(matrix, previous=None):
    if abs(np.linalg.det(matrix)-1) > 1e-9:
        raise ValueError('Quaternion input must be a proper rotation, not a reflection')
    w,x,y,z = spice.m2q(np.ascontiguousarray(matrix))
    q = np.array([x,y,z,w])
    if previous is not None and np.dot(previous,q)<0: q=-q
    return dict(zip(('x','y','z','w'), map(float,q))), q

def download(relative):
    path=ROOT/'Temp/CelestialKernels'/Path(relative).name
    path.parent.mkdir(parents=True,exist_ok=True)
    if not path.exists():
        temp=path.with_suffix(path.suffix+'.partial')
        with urllib.request.urlopen(BASE+relative,timeout=120) as source, temp.open('wb') as dest:
            while chunk:=source.read(1024*1024): dest.write(chunk)
        temp.replace(path)
    return {'file':path.name,'url':BASE+relative,'bytes':path.stat().st_size,
            'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}

def observing_site():
    manifest=json.loads((ROOT/'Assets/MoonTerrain/InputData/manifest.json').read_text())
    nw=np.array(manifest['sourceNorthwestPixelCentreXY'])
    se=np.array(manifest['sourceSoutheastPixelCentreXY'])
    projected=(nw+se)/2
    radius=1737.4
    lon=math.atan2(projected[0],projected[1])
    lat=-math.pi/2+2*math.atan(np.linalg.norm(projected)/(2*radius*1000))
    # At the four center tiles' shared corner. Uses the same preserved source RAW as Unity.
    n=manifest['tileResolution']
    heights=np.fromfile(ROOT/'Assets/MoonTerrain/InputData/moon_r01_c01.raw',dtype='<u2').reshape(n,n)
    elevation_m=float(heights[0,-1])/65535*manifest['heightRangeMeters']+manifest['sourceMinHeightMeters']
    obs=spice.latrec(radius+elevation_m/1000,lon,lat)
    east=np.array([-math.sin(lon),math.cos(lon),0])
    north=np.array([-math.sin(lat)*math.cos(lon),-math.sin(lat)*math.sin(lon),math.cos(lat)])
    up=unit(obs)
    c,s=math.cos(lon),math.sin(lon)
    yaw=np.array([[c,0,s],[0,1,0],[-s,0,c]])
    world_from_moon=yaw@np.stack([east,up,north])
    site={'projectedXMeters':float(projected[0]),'projectedYMeters':float(projected[1]),
          'longitudeEastDegrees':math.degrees(lon),'latitudeDegrees':math.degrees(lat),
          'elevationMeters':elevation_m,'referenceRadiusKm':radius,'northYawDegrees':math.degrees(lon),
          'worldXMeters':manifest['totalWidthMeters']/2,'worldZMeters':manifest['totalLengthMeters']/2,
          'moonFrame':'MOON_ME (MOON_ME_DE440_ME421)', 'earthFrame':'ITRF93',
          'note':'DEM center adopted as observer. Original CSV extraction coordinates are unknown. DEM frame alignment to MOON_ME is an assumption.'}
    return site,obs,world_from_moon

def evaluate(et,obs,a,previous_earth=None,previous_stars=None):
    state,lt=spice.spkcpo('EARTH',et,'MOON_ME','OBSERVER','LT+S',obs,'MOON','MOON_ME')
    earth_vec=np.asarray(state[:3]); distance=np.linalg.norm(earth_vec)
    direction=unit(a@earth_vec)
    earth_matrix=a@spice.pxfrm2('ITRF93','MOON_ME',et-lt,et)@P
    stars_matrix=a@spice.pxform('J2000','MOON_ME',et)@P
    eq,eqn=quat(earth_matrix,previous_earth); sq,sqn=quat(stars_matrix,previous_stars)
    earth_sun,_=spice.spkpos('SUN',et-lt,'J2000','LT+S','EARTH')
    sun_world=unit(a@spice.pxform('J2000','MOON_ME',et)@earth_sun)
    sun_state,_=spice.spkcpo('SUN',et,'MOON_ME','OBSERVER','LT+S',obs,'MOON','MOON_ME')
    sun_direction=unit(a@sun_state[:3])
    # Geographic angles before projected-grid yaw, for comparison to Horizons.
    def azel(world):
        c,s=math.cos(math.radians(site_global['northYawDegrees'])),math.sin(math.radians(site_global['northYawDegrees']))
        local=np.array([[c,0,-s],[0,1,0],[s,0,c]])@world
        return math.degrees(math.atan2(local[0],local[2]))%360,math.degrees(math.asin(local[1]))
    az,el=azel(direction); saz,sel=azel(sun_direction)
    diameter=math.degrees(2*math.asin(6378.137/distance))
    # Sub-observer location in Earth-fixed coordinates, geodetic for Horizons comparison.
    body=P@earth_matrix.T@(-direction)
    radius_at_ray=1/math.sqrt((body[0]**2+body[1]**2)/6378.137**2+body[2]**2/6356.752314245**2)
    sublon,sublat,_=spice.recgeo(body*radius_at_ray,6378.137,(6378.137-6356.752314245)/6378.137)
    sample={'earthDirection':vec(direction),'earthOrientation':eq,'starsOrientation':sq,
            'earthSunDirection':vec(sun_world),'earthDiameter':diameter,
            'earthAzimuth':az,'earthElevation':el,
            'illuminatedFraction':(1+float(np.dot(sun_world,-direction)))/2,
            'subObserverLongitudeEast':math.degrees(sublon),'subObserverLatitude':math.degrees(sublat),
            'spiceSunAzimuth':saz,'spiceSunElevation':sel,'distanceKm':float(distance),'lightTimeSeconds':float(lt)}
    return sample,eqn,sqn

site_global={}

def horizons(site,start,stop):
    params={'format':'json','COMMAND':"'399'",'MAKE_EPHEM':"'YES'",'EPHEM_TYPE':"'OBSERVER'",
            'CENTER':"'coord@301'",'COORD_TYPE':"'GEODETIC'",
            'SITE_COORD':f"'{site['longitudeEastDegrees']:.12f},{site['latitudeDegrees']:.12f},{site['elevationMeters']/1000:.9f}'",
            'START_TIME':f"'{start:%Y-%m-%d %H:%M:%S}'",'STOP_TIME':f"'{stop:%Y-%m-%d %H:%M:%S}'",
            'STEP_SIZE':"'24 h'",'QUANTITIES':"'4,13,14,10'",'CSV_FORMAT':"'YES'",'ANG_FORMAT':"'DEG'",'EXTRA_PREC':"'YES'"}
    url='https://ssd.jpl.nasa.gov/api/horizons.api?'+urllib.parse.urlencode(params)
    response=json.loads(urllib.request.urlopen(url,timeout=120).read())
    (QA/'horizons-response.json').write_text(json.dumps(response,indent=2),encoding='utf8')
    (QA/'horizons-query.txt').write_text(url,encoding='utf8')
    if 'error' in response: raise RuntimeError(response['error'])
    return response['result']

def main():
    global site_global
    parser=argparse.ArgumentParser(); parser.add_argument('--download',action='store_true'); parser.add_argument('--horizons',action='store_true'); args=parser.parse_args()
    QA.mkdir(parents=True,exist_ok=True)
    if args.download:
        with ThreadPoolExecutor(max_workers=3) as pool: provenance=list(pool.map(download,KERNELS))
    else:
        provenance=[]
        for relative in KERNELS:
            path=ROOT/'Temp/CelestialKernels'/Path(relative).name
            if not path.exists(): raise FileNotFoundError(path)
            provenance.append({'file':path.name,'url':BASE+relative,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    for entry in provenance: spice.furnsh(str(ROOT/'Temp/CelestialKernels'/entry['file']))
    site,obs,a=observing_site(); site_global=site
    solar=list(csv.DictReader((ROOT/'Assets/MoonEnvironment/Data/SunAngle.csv').open()))
    parse_time=lambda text:datetime.strptime(text,'%Y-%jT%H:%M:%S.%f').replace(tzinfo=timezone.utc)
    start,stop=parse_time(solar[0]['Time']),parse_time(solar[-1]['Time'])
    samples=[]; eq=sq=None
    duration=(stop-start).total_seconds()
    for seconds in range(0,int(duration)+1,300):
        utc=start+timedelta(seconds=seconds); et=spice.str2et(utc.strftime('%Y-%m-%d %H:%M:%S UTC'))
        frame,eq,sq=evaluate(et,obs,a,eq,sq); frame['seconds']=seconds; samples.append(frame)
    data={'version':1,'startUtc':start.isoformat().replace('+00:00','Z'),'durationSeconds':duration,
          'stepSeconds':300,'site':site,'samples':samples}
    OUT.write_text(json.dumps(data,separators=(',',':')),encoding='utf8')
    (QA/'kernel-provenance.json').write_text(json.dumps(provenance,indent=2),encoding='utf8')
    (QA/'site.json').write_text(json.dumps(site,indent=2),encoding='utf8')
    residuals=[]
    for row in solar:
        et=spice.str2et(parse_time(row['Time']).strftime('%Y-%m-%d %H:%M:%S UTC'))
        frame,_,_=evaluate(et,obs,a)
        residuals.append({'time':row['Time'],'azimuthDifferenceDegrees':(frame['spiceSunAzimuth']-float(row['Azimuth'])+180)%360-180,
                          'elevationDifferenceDegrees':frame['spiceSunElevation']-float(row['Elevation'])})
    (QA/'sun-csv-residuals.json').write_text(json.dumps(residuals,indent=2),encoding='utf8')
    if args.horizons:
        result=horizons(site,start,stop)
        print(result[result.index('Date__(UT)'):result.index('$$EOE')+5])
    else:
        result=json.loads((QA/'horizons-response.json').read_text(encoding='utf8'))['result'] if (QA/'horizons-response.json').exists() else None
    if result:
        comparison=[]
        rows=list(csv.reader(result.split('$$SOE')[1].split('$$EOE')[0].strip().splitlines()))
        for row,sample in zip(rows,[samples[0],samples[len(samples)//2],samples[-1]]):
            delta=lambda a,b:(a-b+180)%360-180
            comparison.append({'utc':row[0].strip(),'azimuthErrorDegrees':delta(sample['earthAzimuth'],float(row[3])),
                'elevationErrorDegrees':sample['earthElevation']-float(row[4]),
                'diameterErrorArcseconds':sample['earthDiameter']*3600-float(row[5]),
                'subLongitudeErrorDegrees':delta(sample['subObserverLongitudeEast'],float(row[6])),
                'subLatitudeErrorDegrees':sample['subObserverLatitude']-float(row[7]),
                'illuminatedFractionError':sample['illuminatedFraction']-float(row[8])/100})
        (QA/'horizons-comparison.json').write_text(json.dumps(comparison,indent=2),encoding='utf8')
        assert all(abs(r['azimuthErrorDegrees'])<0.001 and abs(r['elevationErrorDegrees'])<0.001 and
                   abs(r['diameterErrorArcseconds'])<0.1 and abs(r['subLongitudeErrorDegrees'])<0.01 and
                   abs(r['subLatitudeErrorDegrees'])<0.01 and abs(r['illuminatedFractionError'])<0.001 for r in comparison)
    # Validate serialized handedness/longitude conversion, not just the quaternion's norm.
    checks=[]
    for sample in samples:
        q=sample['earthOrientation']; m=spice.q2m([q['w'],q['x'],q['y'],q['z']])
        for lon_deg,lat_deg in [(0,0),(90,0),(-90,0),(0,90),(0,-90)]:
            b=spice.latrec(1,math.radians(lon_deg),math.radians(lat_deg)); world=m@P@b
            texture_body=P@m.T@world
            assert np.max(np.abs(texture_body-b))<1e-12
    checks.append('577 orientations: Greenwich/E90/W90/North/South axes round-trip without handedness loss')
    checks.append('All proper rotation matrices validated before conversion; quaternion sign continuity retained')
    maxima={'earthPoseDegrees':0.,'starsPoseDegrees':0.,'earthDirectionDegrees':0.,'earthDiameterArcseconds':0.}
    for i in range(len(samples)-1):
        a0,b0=samples[i],samples[i+1]
        seconds=a0['seconds']+150
        utc=start+timedelta(seconds=seconds)
        exact,_,_=evaluate(spice.str2et(utc.strftime('%Y-%m-%d %H:%M:%S UTC')),obs,a)
        for field,key in [('earthOrientation','earthPoseDegrees'),('starsOrientation','starsPoseDegrees')]:
            qa=np.array(list(a0[field].values())); qb=np.array(list(b0[field].values()))
            if np.dot(qa,qb)<0: qb=-qb
            interpolated=unit(qa+qb); qe=np.array(list(exact[field].values()))
            error=math.degrees(2*math.acos(np.clip(abs(np.dot(interpolated,qe)),-1,1)))
            maxima[key]=max(maxima[key],error)
        da=np.array(list(a0['earthDirection'].values())); db=np.array(list(b0['earthDirection'].values()))
        de=np.array(list(exact['earthDirection'].values()))
        maxima['earthDirectionDegrees']=max(maxima['earthDirectionDegrees'],math.degrees(math.acos(np.clip(np.dot(unit(da+db),de),-1,1))))
        maxima['earthDiameterArcseconds']=max(maxima['earthDiameterArcseconds'],abs((a0['earthDiameter']+b0['earthDiameter'])/2-exact['earthDiameter'])*3600)
    assert maxima['earthPoseDegrees']<0.005 and maxima['starsPoseDegrees']<0.005 and maxima['earthDirectionDegrees']<0.001
    (QA/'interpolation-errors.json').write_text(json.dumps(maxima,indent=2),encoding='utf8')
    (QA/'offline-checks.json').write_text(json.dumps(checks,indent=2),encoding='utf8')
    print(json.dumps({'samples':len(samples),'site':site,'first':samples[0],'middle':samples[len(samples)//2],'last':samples[-1]},indent=2))

if __name__=='__main__': main()
