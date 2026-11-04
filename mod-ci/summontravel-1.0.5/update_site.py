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
 entry={'version': None, 'date': None, 'title': None, 'changes': ['보호의 스태프가 주변 아군에게 방어막을 주는 적용 반경을 20m(지름 40m)로 넓혔습니다.', '플레이어·소환 해골 등 원래 시전이 허용하던 대상에게 적용합니다. 다른 마법과 적의 범위 공격은 변경하지 않습니다.', '실제 적용 범위와 구체 충돌 반경을 함께 맞추며, 시각 효과 자체를 크게 늘리지 않습니다.', '재시전 시 이번 시전의 최대 방어막으로 충전하고 내 HP 옆 막대·해골 방어막 수치에 실시간 반영합니다. 100m·10초 자동 복귀와 고속 추적도 유지합니다.'], 'notes': ['게임과 GhostR2를 완전히 종료한 뒤 다시 실행하면 통합팩 1.0.46을 자동 준비합니다. 친구·전용 서버도 같은 버전으로 업데이트하세요.', '동작 검사 150개와 실제 제공 게임 DLL의 API 검사 54개 통과. 실제 Valheim 월드 실행 확인은 별도로 필요합니다.']}
 entry.update(version=release['version'],date=release['date'],title=release['notes'])
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>','<time data-release-date datetime="2026-11-04">2026.11.04</time>',index);assert n==1
 index=index.replace('modules.js?v=1.5.16','modules.js?v=1.5.17').replace('웹사이트 v1.5.16','웹사이트 v1.5.17')
 modules=(docs/'modules.js').read_text()
 module={'id': 'GhostSummonTravel', 'name': 'Ghost 해골 동행·방어막 표시', 'author': 'Ghost', 'category': 'world', 'icon': '◈', 'version': '1.0.5', 'description': '방어막 적용 반경 20m · 재시전 충전 · 해골 자동 동행', 'points': ['보호의 스태프가 주변 아군에게 방어막을 주는 적용 반경을 20m(지름 40m)로 넓혔습니다.', '플레이어·소환 해골 등 원래 시전이 허용하던 대상에게 적용합니다. 다른 마법과 적의 범위 공격은 변경하지 않습니다.', '실제 적용 범위와 구체 충돌 반경을 함께 맞추며, 시각 효과 자체를 크게 늘리지 않습니다.', '재시전 시 이번 시전의 최대 방어막으로 충전하고 내 HP 옆 막대·해골 방어막 수치에 실시간 반영합니다. 100m·10초 자동 복귀와 고속 추적도 유지합니다.', '지도 순간이동·포탈에 기존 해골이 함께 이동하며 체력·방어막 잔량·남은 시간을 유지합니다. 안전한 도착 위치에 몸 사이 최소 1m 여유를 확인합니다.'], 'note': '친구·전용 서버도 같은 통합팩으로 업데이트하세요. 실제 게임에서 동작 확인이 필요합니다.'}
 i=modules.index('{"id":"GhostSummonTravel"');prior,length=json.JSONDecoder().raw_decode(modules[i:]);modules=modules[:i]+json.dumps(module,ensure_ascii=False,separators=(',',':'))+modules[i+length:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
