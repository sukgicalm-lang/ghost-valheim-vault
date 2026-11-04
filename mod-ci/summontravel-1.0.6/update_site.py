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
 entry={'version': None, 'date': None, 'title': None, 'changes': ['이미 방어막이 있을 때 게임이 기존 적용 함수를 거치지 않고 직접 갱신하는 경로를 처리해, 본인과 해골 모두 재시전 충전이 적용되도록 수정했습니다.', '예: 최대 300·잔량 55 → 재시전 후 300 / 300. 숙련도 등으로 새 최대치가 350이면 350 / 350으로 갱신합니다.', '공통 시간 갱신·최대치 계산 후 실제 소모량을 초기화합니다. 순간이동 복원·HUD 읽기·레벨 계산만으로 방어막을 채우지 않습니다.', '반경 20m, 내 HP 옆 막대·해골 방어막 숫자, 100m·10초 자동 복귀와 고속 추적을 유지합니다.'], 'notes': ['게임과 GhostR2를 완전히 종료한 뒤 다시 실행하면 통합팩 1.0.47을 자동 준비합니다. 친구·전용 서버도 같은 버전으로 업데이트하세요.', '동작 검사 153개·게임 DLL API 검사 57개·실제 Harmony 실행 검사 16개를 배포 전에 확인합니다. 실제 Valheim 월드 실행 확인은 별도로 필요합니다.']}
 entry.update(version=release['version'],date=release['date'],title=release['notes'])
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>','<time data-release-date datetime="2026-11-04">2026.11.04</time>',index);assert n==1
 index=index.replace('modules.js?v=1.5.17','modules.js?v=1.5.18').replace('웹사이트 v1.5.17','웹사이트 v1.5.18')
 modules=(docs/'modules.js').read_text()
 module={'id': 'GhostSummonTravel', 'name': 'Ghost 해골 동행·방어막 표시', 'author': 'Ghost', 'category': 'world', 'icon': '◈', 'version': '1.0.6', 'description': '본인·해골 재시전 충전 경로 수정 · 방어막 반경 20m', 'points': ['이미 방어막이 있을 때 게임이 기존 적용 함수를 거치지 않고 직접 갱신하는 경로를 처리해, 본인과 해골 모두 재시전 충전이 적용되도록 수정했습니다.', '예: 최대 300·잔량 55 → 재시전 후 300 / 300. 숙련도 등으로 새 최대치가 350이면 350 / 350으로 갱신합니다.', '공통 시간 갱신·최대치 계산 후 실제 소모량을 초기화합니다. 순간이동 복원·HUD 읽기·레벨 계산만으로 방어막을 채우지 않습니다.', '반경 20m, 내 HP 옆 막대·해골 방어막 숫자, 100m·10초 자동 복귀와 고속 추적을 유지합니다.', '지도 순간이동·포탈에 기존 해골이 함께 이동하며 체력·방어막 잔량·남은 시간을 유지합니다. 안전한 도착 위치에 몸 사이 최소 1m 여유를 확인합니다.'], 'note': '친구·전용 서버도 같은 통합팩으로 업데이트하세요. 실제 게임에서 동작 확인이 필요합니다.'}
 i=modules.index('{"id":"GhostSummonTravel"');prior,length=json.JSONDecoder().raw_decode(modules[i:]);modules=modules[:i]+json.dumps(module,ensure_ascii=False,separators=(',',':'))+modules[i+length:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
