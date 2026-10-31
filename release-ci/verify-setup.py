"""Verify the official, unmodified third-party ZIPs used by the required setup."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import sys
import urllib.request
import zipfile
from bootstrap import safe_extract
ROOT=Path(__file__).resolve().parent.parent
WORK=ROOT/'_smelter_build'
setup=json.loads((ROOT/'docs/setup-release.json').read_text(encoding='utf-8'))
bundle=json.loads((WORK/'launcher/src/ghost/GhostSetupBundle.json').read_text(encoding='utf-8'))
baseline=bundle['setup']
if setup['schema']!=1 or not 2<=len(setup['packages'])<=100 or setup['revision']<baseline['revision']:
    raise ValueError('Invalid or rolled-back required setup')
if setup['revision']==baseline['revision'] and setup!=baseline:
    raise ValueError('Same setup revision has different contents')
# The public feed may advance independently of an immutable launcher binary.
# Verify current payloads and the bundled offline fallback without republishing it.
payloads={}
for p in baseline['packages']+setup['packages']:
    key=(p['name'],p['version'])
    if key in payloads and payloads[key]!=p:
        raise ValueError('Same package version has conflicting verification metadata')
    payloads[key]=p
cache=Path(sys.argv[1]) if len(sys.argv)>1 else WORK/'setup-zips'
cache.mkdir(exist_ok=True,parents=True)
def verify(p):
    if p['name'].startswith('Ghost-'):return
    name=p['name']+'-'+p['version']
    target=cache/(name+'.zip')
    if not target.exists():
        with urllib.request.urlopen('https://gcdn.thunderstore.io/live/repository/packages/'+name+'.zip',timeout=90) as response:
            data=response.read()
        target.write_bytes(data)
    data=target.read_bytes()
    if len(data)!=p['bytes'] or hashlib.sha256(data).hexdigest()!=p['sha256']:
        raise ValueError('Official setup ZIP hash/size mismatch: '+name)
    with zipfile.ZipFile(target) as z:
        if z.testzip():raise ValueError('Corrupt ZIP: '+name)
        manifests=sorted((n for n in z.namelist() if n.lower().endswith('manifest.json') and n.count('/')<=1),key=len)
        actual=json.loads(z.read(manifests[0]))
        if actual['version_number']!=p['version'] or actual.get('dependencies',[])!=p['dependencies']:
            raise ValueError('Official manifest differs from pinned setup: '+name)
    safe_extract(target,WORK/'setup-fixtures'/name)
with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
    list(pool.map(verify,payloads.values()))
count=sum(not p['name'].startswith('Ghost-') for p in payloads.values())
print(f'Verified {count} unique official payloads across current and bundled setups; {len(setup["packages"])} current required packages.')
