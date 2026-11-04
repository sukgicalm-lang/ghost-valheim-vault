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
 entry={'version': None, 'date': None, 'title': None, 'changes': ['해골 소환의 개체별 생성 전·생성 사이 대기 시간을 제거했습니다. 원래 공통 시전 동작 이후 최대 수가 한꺼번에 생성되며 기존 해골 교체와 원래 소환 한도를 유지합니다.', 'NPC 무기가 같은 숙련도 항목으로 묶여 있어도 실제 무기 프리팹과 종류로 구분해 배정합니다. 원래 사용 가능한 무기 종류가 3개이고 3마리를 소환하면 서로 다른 3종을 배정합니다.', '게임이 실제 장비를 지급하는 GiveDefaultItems 직전에 해당 개체의 장비 목록에 배정 무기를 적용합니다. 코루틴 종료 후 다음 프레임에 장비가 생성되거나 아이템 초기화가 난수를 소비해도 무기 다양화가 유지됩니다.', '배정한 원래 무기·방패를 저장해 친구 화면과 재접속에도 유지합니다. 공유 원본 프리팹·외부 장비·기존 UI 배치·동행과 보호막 기능은 변경하지 않습니다.'], 'notes': ['GhostR2에서 업데이트 완료 후 게임을 다시 실행하세요. 이미 소환된 해골의 장비는 새 소환 때 교체됩니다. 친구·서버도 같은 버전으로 업데이트하세요.', '동작 검사 244개·게임 DLL API 검사 105개·실제 Harmony 실행 검사 36개를 확인합니다. 실제 게임 화면 확인은 별도입니다.']}
 entry.update(version=release['version'],date=release['date'],title=release['notes'])
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>','<time data-release-date datetime="2026-11-04">2026.11.04</time>',index);assert n==1
 index=index.replace('modules.js?v=1.5.22','modules.js?v=1.5.23').replace('웹사이트 v1.5.22','웹사이트 v1.5.23')
 modules=(docs/'modules.js').read_text()
 module={'id': 'GhostSummonTravel', 'name': 'Ghost 해골 동행·방어막 표시', 'author': 'Ghost', 'category': 'world', 'icon': '◈', 'version': '1.0.11', 'description': '한 번에 최대 수 동시 소환 · 실제 무기 종류 다양화 · 기존 해골 교체', 'points': ['해골 소환의 개체별 생성 전·생성 사이 대기 시간을 제거했습니다. 원래 공통 시전 동작 이후 최대 수가 한꺼번에 생성되며 기존 해골 교체와 원래 소환 한도를 유지합니다.', 'NPC 무기가 같은 숙련도 항목으로 묶여 있어도 실제 무기 프리팹과 종류로 구분해 배정합니다. 원래 사용 가능한 무기 종류가 3개이고 3마리를 소환하면 서로 다른 3종을 배정합니다.', '게임이 실제 장비를 지급하는 GiveDefaultItems 직전에 해당 개체의 장비 목록에 배정 무기를 적용합니다. 코루틴 종료 후 다음 프레임에 장비가 생성되거나 아이템 초기화가 난수를 소비해도 무기 다양화가 유지됩니다.', '배정한 원래 무기·방패를 저장해 친구 화면과 재접속에도 유지합니다. 공유 원본 프리팹·외부 장비·기존 UI 배치·동행과 보호막 기능은 변경하지 않습니다.'], 'note': 'GhostR2 업데이트 후 게임 재실행 → 다시 소환하면 적용됩니다.'}
 i=modules.index('{"id":"GhostSummonTravel"');prior,length=json.JSONDecoder().raw_decode(modules[i:]);modules=modules[:i]+json.dumps(module,ensure_ascii=False,separators=(',',':'))+modules[i+length:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
