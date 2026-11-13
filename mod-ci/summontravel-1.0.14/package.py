import base64,hashlib,json,sys,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];INPUT=Path(__file__).resolve().parent
NEW='1.0.55';NOTES='인벤토리에 해골 소환 해제 버튼 추가'
DLL='BepInEx/plugins/Ghost-GhostSummonTravel/GhostSummonTravel.dll'
def sha(data):return hashlib.sha256(data).hexdigest()
def build(base,output):
 spec=json.loads((INPUT/'input.json').read_text());raw=Path(base).read_bytes()
 assert len(raw)==398949309 and sha(raw)==spec['base_sha256'],'Wrong base suite'
 plugin=base64.b64decode((INPUT/'module.dll.b64').read_text(),validate=True);assert sha(plugin)==spec['dll_sha256']
 for name,h in spec['source_hashes'].items():assert sha((ROOT/name).read_bytes())==h,'Source/binary provenance mismatch: '+name
 with zipfile.ZipFile(base) as z:
  assert len(z.namelist())==len(set(z.namelist()))
  files={n:z.read(n) for n in z.namelist() if not n.endswith('/')}
 old={n:sha(v) for n,v in files.items() if n.endswith('.dll')};assert len(old)==15 and DLL in files; old.pop(DLL)
 manifest=json.loads(files['manifest.json']);manifest.update(version_number=NEW,description=NOTES)
 files['manifest.json']=json.dumps(manifest,ensure_ascii=False,indent=2).encode()
 files['README.md']+=('\n\n## 1.0.55\n\n'+(ROOT/'mod-src/SummonTravel/README.md').read_text()).encode()
 files['CHANGELOG.md']=('# 1.0.55\n\n'+NOTES+'\n\n').encode()+files['CHANGELOG.md'];files[DLL]=plugin
 for p in sorted((ROOT/'mod-src/SummonTravel').iterdir()):
  if p.is_file() and p.name in {Path(name).name for name in spec["source_hashes"]}:files['Source/SummonTravel/'+p.name]=p.read_bytes()
 for name in ['behavior-results.txt','runtime-results.txt']:files['Verification/SummonTravel-1.0.14/'+name]=(INPUT/name).read_bytes()
 files['Verification/1.0.55-Preserved-DLLs.json']=json.dumps(old,indent=2).encode()
 files['SHA256SUMS.json']=json.dumps({n:sha(v) for n,v in sorted(files.items()) if n!='SHA256SUMS.json'},indent=2).encode()
 Path(output).parent.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(output,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
  for n,v in sorted(files.items()):
   assert not n.endswith('.cfg') and '..' not in Path(n).parts
   info=zipfile.ZipInfo(n,(2026,11,14,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,v)
 with zipfile.ZipFile(output) as z:
  assert z.testzip() is None
  assert len([n for n in z.namelist() if n.endswith('.dll')])==15
  assert all(sha(z.read(n))==h for n,h in old.items()),'Old DLL changed'
  assert sha(z.read(DLL))==spec['dll_sha256']
  assert not any(n.endswith('Doubles.cs') or n.endswith('assembly_valheim.dll') or n.endswith('.cfg') for n in z.namelist())
 payload=Path(output).read_bytes()
 release={'version':NEW,'date':'2026-11-14','file':f'downloads/Ghost-GhostValheimSuite-{NEW}.zip','bytes':len(payload),'sha256':sha(payload),'appIntegration':True,'dependencies':manifest['dependencies']+['Ghost-GhostSmelterControl-1.0.3'],'notes':NOTES,'downloadUrl':f'https://github.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-{NEW}/Ghost-GhostValheimSuite-{NEW}.zip'}
 Path(str(output)+'.release.json').write_text(json.dumps(release,ensure_ascii=False,indent=2));print(json.dumps(release,ensure_ascii=False))
if __name__=='__main__':build(sys.argv[1],sys.argv[2])
