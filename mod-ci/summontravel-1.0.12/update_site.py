import html,json,re,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def update(release):
 docs=ROOT/'docs';protected=['client-release.json','setup-release.json','smelter-release.json','smelter-releases.json'];before={n:(docs/n).read_bytes() for n in protected}
 current=json.loads((docs/'release.json').read_text());ver=lambda s:tuple(map(int,s.split('.')))
 assert ver(current['version'])<=ver(release['version']),'Newer suite already shipped'
 if current['version']==release['version']:assert current['sha256']==release['sha256'] and current['bytes']==release['bytes'],'Immutable release conflict'
 catalog=json.loads((docs/'releases.json').read_text())
 for r in catalog:
  if r['version']==release['version']:assert r['sha256']==release['sha256'] and r['bytes']==release['bytes']
 catalog=[release]+[r for r in catalog if r['version']!=release['version']]
 history=json.loads((docs/'update-history.json').read_text())
 entry={'version': None, 'date': None, 'title': None, 'changes': ['재소환으로 해제되는 옛 해골은 즉시 이름표·순간이동 보류·추적 보정·보호막 이펙트 복구 대상에서 제외합니다. 게임의 원래 본체 해제 RPC는 그대로 실행합니다.', '게임의 현재 개체 목록과 네트워크 유효성·생존·해제 상태를 확인해 본체 없는 이름·낮은 HP·보호막 0 잔상과 화면 캐시를 한 번에 정리합니다. 순간이동으로 화면이 초기화되기를 기다리지 않습니다.', '옛 해골에서 새 해골로 HUD 연결이 바뀌는 경우 표시를 새 개체 기준으로 다시 구성합니다. 현재 살아 있는 해골의 실제 체력·보호막과 원래 시전 적용은 유지합니다.', '최대 수 동시 소환·기존 해골 교체·무기 다양화·기본 비율의 여왕 버프 간격·정상 순간이동과 회복을 유지합니다.'], 'notes': ['GhostR2에서 업데이트 완료 후 게임을 다시 실행하고 새로 소환하세요. 친구·전용 서버도 같은 버전으로 업데이트하세요.', '동작 검사 253개·게임 DLL API 검사 107개·실제 Harmony 실행 검사 39개로 해제 지연·HUD 잔상·보호막 유지 경로를 확인합니다. 실제 게임 화면 검증은 별도입니다.']}
 entry.update(version=release['version'],date=release['date'],title=release['notes'])
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>','<time data-release-date datetime="2026-11-04">2026.11.04</time>',index);assert n==1
 index=index.replace('modules.js?v=1.5.23','modules.js?v=1.5.24').replace('웹사이트 v1.5.23','웹사이트 v1.5.24')
 modules=(docs/'modules.js').read_text()
 module={'id': 'GhostSummonTravel', 'name': 'Ghost 해골 동행·방어막 표시', 'author': 'Ghost', 'category': 'world', 'icon': '◈', 'version': '1.0.12', 'description': '동시 교체 소환 · 무기 다양화 · 본체 없는 이름표와 보호막 잔상 정리', 'points': ['재소환으로 해제되는 옛 해골은 즉시 이름표·순간이동 보류·추적 보정·보호막 이펙트 복구 대상에서 제외합니다. 게임의 원래 본체 해제 RPC는 그대로 실행합니다.', '게임의 현재 개체 목록과 네트워크 유효성·생존·해제 상태를 확인해 본체 없는 이름·낮은 HP·보호막 0 잔상과 화면 캐시를 한 번에 정리합니다. 순간이동으로 화면이 초기화되기를 기다리지 않습니다.', '옛 해골에서 새 해골로 HUD 연결이 바뀌는 경우 표시를 새 개체 기준으로 다시 구성합니다. 현재 살아 있는 해골의 실제 체력·보호막과 원래 시전 적용은 유지합니다.', '최대 수 동시 소환·기존 해골 교체·무기 다양화·기본 비율의 여왕 버프 간격·정상 순간이동과 회복을 유지합니다.'], 'note': 'GhostR2 업데이트 후 게임 재실행 → 새로 소환하세요.'}
 i=modules.index('{"id":"GhostSummonTravel"');prior,length=json.JSONDecoder().raw_decode(modules[i:]);modules=modules[:i]+json.dumps(module,ensure_ascii=False,separators=(',',':'))+modules[i+length:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
