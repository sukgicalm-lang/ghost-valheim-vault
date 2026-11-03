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
 entry={'version': None, 'date': None, 'title': None, 'changes': ['자동 복귀는 100m 이상 떨어지거나, 실제로 이동해야 할 해골이 10초 이상 움직이지 못한 경우에만 시도합니다. 정상 대기·전투 시간은 걸림에서 제외합니다.', '20m 이상 뒤처지면 이동속도를 최대 6배로 높여 달려 따라오고 8m 이내에서 원래 속도로 돌아옵니다. 가까이 붙으면 걸어서 물러납니다.', '순간이동 로딩 중 공유 지역 목록을 건드리던 경로를 분리하고, 합류·충돌 탐색·이펙트 복구를 프레임별로 분산했습니다. 같은 도착 좌표의 반복 이동·강제 전송을 제거했습니다.', '내 HP 옆에 같은 모양의 파란 방어막 막대와 남은 흡수량 / 최대 흡수량을 실시간 표시합니다. 해골 방어막 표시·상태 유지도 보존합니다.'], 'notes': ['게임과 GhostR2를 완전히 종료한 뒤 다시 실행하면 통합팩 1.0.44를 자동 준비합니다. 친구·전용 서버도 같은 버전으로 업데이트하세요.', '자동 검사 170개 통과. 실제 Valheim 월드에서 화면·프레임 시간·서버 실행 확인은 별도로 필요합니다.']}
 entry.update(version=release['version'],date=release['date'],title=release['notes'])
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>','<time data-release-date datetime="2026-11-03">2026.11.03</time>',index);assert n==1
 index=index.replace('modules.js?v=1.5.14','modules.js?v=1.5.15').replace('웹사이트 v1.5.14','웹사이트 v1.5.15')
 modules=(docs/'modules.js').read_text()
 module={'id': 'GhostSummonTravel', 'name': 'Ghost 해골 동행·방어막 표시', 'author': 'Ghost', 'category': 'world', 'icon': '◈', 'version': '1.0.3', 'description': '100m·10초 자동 복귀 · 고속 추적 · 로딩 충돌 수정 · 내 방어막 막대', 'points': ['자동 복귀는 100m 이상 떨어지거나, 실제로 이동해야 할 해골이 10초 이상 움직이지 못한 경우에만 시도합니다. 정상 대기·전투 시간은 걸림에서 제외합니다.', '20m 이상 뒤처지면 이동속도를 최대 6배로 높여 달려 따라오고 8m 이내에서 원래 속도로 돌아옵니다. 가까이 붙으면 걸어서 물러납니다.', '순간이동 로딩 중 공유 지역 목록을 건드리던 경로를 분리하고, 합류·충돌 탐색·이펙트 복구를 프레임별로 분산했습니다. 같은 도착 좌표의 반복 이동·강제 전송을 제거했습니다.', '내 HP 옆에 같은 모양의 파란 방어막 막대와 남은 흡수량 / 최대 흡수량을 실시간 표시합니다. 해골 방어막 표시·상태 유지도 보존합니다.', '플레이어의 지도 순간이동·포탈에 기존 해골이 함께 이동하며 체력·방어막 잔량·남은 시간을 유지합니다. 도착 시 몸 사이 최소 1m 여유를 확인합니다.'], 'note': '친구·전용 서버도 같은 통합팩으로 업데이트하세요. 실제 게임 화면에서 동작 확인이 필요합니다.'}
 i=modules.index('{"id":"GhostSummonTravel"');prior,length=json.JSONDecoder().raw_decode(modules[i:]);modules=modules[:i]+json.dumps(module,ensure_ascii=False,separators=(',',':'))+modules[i+length:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
