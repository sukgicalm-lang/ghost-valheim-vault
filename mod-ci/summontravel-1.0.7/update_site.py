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
 entry={'version': None, 'date': None, 'title': None, 'changes': ['내 방어막 막대를 HP 막대의 화면 오른쪽에 같은 높이로 배치했습니다. 세로 HP 모양·크기·UI 배율을 따르며 막대에는 남은 수치를 간결하게 표시합니다.', '본인 머리 위에도 방어막 잔량 / 최대치가 보입니다. 본인과 해골의 머리 위 표시는 위에 빨간 HP 막대·현재 / 최대 HP, 아래에 파란 방어막 막대·잔량 / 최대치를 표시합니다.', '아군 해골의 체력 표시가 마우스를 떼면 숨겨지던 조건을 보완하고, 30m 안의 살아 있는 해골은 HP·방어막을 계속 확인할 수 있게 했습니다.', '해골 HP 회복은 캐릭터 음식·회복 버프의 초당 회복량에 맞춰 개선했습니다. 설치된 Regeneration 모드의 회복량·주기 배율도 반영하며, 기존 회복이 더 빠르면 유지합니다.', '방어막 재시전 충전·20m 적용 범위·순간이동 시 잔량 보존·100m 또는 10초 걸림 복귀 조건을 유지합니다.'], 'notes': ['게임과 GhostR2를 완전히 종료한 뒤 다시 실행하면 통합팩 1.0.48을 자동 준비합니다. 친구·전용 서버도 같은 버전으로 업데이트하세요.', '동작 검사 181개·게임 DLL API 검사 65개·실제 Harmony 실행 검사 20개를 배포 전에 확인합니다. 실제 Valheim 월드 실행 확인은 별도로 필요합니다.']}
 entry.update(version=release['version'],date=release['date'],title=release['notes'])
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>','<time data-release-date datetime="2026-11-04">2026.11.04</time>',index);assert n==1
 index=index.replace('modules.js?v=1.5.18','modules.js?v=1.5.19').replace('웹사이트 v1.5.18','웹사이트 v1.5.19')
 modules=(docs/'modules.js').read_text()
 module={'id': 'GhostSummonTravel', 'name': 'Ghost 해골 동행·방어막 표시', 'author': 'Ghost', 'category': 'world', 'icon': '◈', 'version': '1.0.7', 'description': 'HP 오른쪽 방어막 · 본인·해골 머리 위 2줄 체력 표시 · 해골 회복 개선', 'points': ['내 방어막 막대를 HP 막대의 화면 오른쪽에 같은 높이로 배치했습니다. 세로 HP 모양·크기·UI 배율을 따르며 막대에는 남은 수치를 간결하게 표시합니다.', '본인 머리 위에도 방어막 잔량 / 최대치가 보입니다. 본인과 해골의 머리 위 표시는 위에 빨간 HP 막대·현재 / 최대 HP, 아래에 파란 방어막 막대·잔량 / 최대치를 표시합니다.', '아군 해골의 체력 표시가 마우스를 떼면 숨겨지던 조건을 보완하고, 30m 안의 살아 있는 해골은 HP·방어막을 계속 확인할 수 있게 했습니다.', '해골 HP 회복은 캐릭터 음식·회복 버프의 초당 회복량에 맞춰 개선했습니다. 설치된 Regeneration 모드의 회복량·주기 배율도 반영하며, 기존 회복이 더 빠르면 유지합니다.', '방어막 재시전 충전·20m 적용 범위·순간이동 시 잔량 보존·100m 또는 10초 걸림 복귀 조건을 유지합니다.'], 'note': '친구·전용 서버도 같은 통합팩으로 업데이트하세요. 실제 게임에서 화면·동작 확인이 필요합니다.'}
 i=modules.index('{"id":"GhostSummonTravel"');prior,length=json.JSONDecoder().raw_decode(modules[i:]);modules=modules[:i]+json.dumps(module,ensure_ascii=False,separators=(',',':'))+modules[i+length:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
