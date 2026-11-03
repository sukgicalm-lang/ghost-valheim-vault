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
 entry={'version':release['version'],'date':release['date'],'title':release['notes'],'changes':['나를 따라오는 소환 해골이 지도 순간이동과 포탈 이동에 함께 따라옵니다.','기존 해골의 체력·장비·등급과 방어막 남은 시간·흡수량을 유지합니다. 로딩 중에도 시간은 흐릅니다.','바위·단차에 막혀 약 4초 동안 접근하지 못한 해골은 바닥과 충돌을 확인한 가까운 위치로 합류합니다. 플레이어가 공중에 있으면 기다립니다.','평소에는 걸어서 따라오며, 대기 명령의 해골과 다른 동물은 자동 이동시키지 않습니다.'],'notes':['게임과 GhostR2를 완전히 종료한 뒤 다시 실행하면 통합팩 1.0.41을 자동 준비합니다. 친구·전용 서버도 같은 버전으로 업데이트하세요.','자동 검사 65개 통과. 실제 게임 월드·친구 PC에서의 실행 확인은 별도로 필요합니다.']}
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>','<time data-release-date datetime="2026-11-03">2026.11.03</time>',index);assert n==1
 index=index.replace('modules.js?v=1.5.0','modules.js?v=1.5.12').replace('웹사이트 v1.5.11','웹사이트 v1.5.12')
 modules=(docs/'modules.js').read_text()
 module={'id':'GhostSummonTravel','name':'Ghost 해골 동행·길막힘 복귀','author':'Ghost','category':'world','icon':'◈','version':'1.0.0','description':'순간이동·방어막 유지 · 점프 지형에서 길을 잃으면 합류','points':['나를 따라오는 소환 해골이 지도 순간이동과 포탈을 함께 이용합니다.','체력·장비·등급과 방어막의 남은 시간·흡수량을 유지합니다.','바위·단차에 막혀 약 4초 동안 접근하지 못하면 안전한 가까운 위치로 합류합니다. 평소에는 걸어서 따라옵니다.','플레이어가 땅에 서 있고 해골이 설 바닥·공간이 있을 때만 복귀합니다.','대기 명령의 해골과 다른 동물은 대상이 아닙니다. 별도 설정 없이 적용됩니다.'],'note':'친구·전용 서버도 같은 통합팩으로 업데이트하세요. 실제 게임에서 동작 확인이 필요합니다.'}
 if '"id":"GhostSummonTravel"' not in modules:
  i=modules.rfind('];');assert i>=0;modules=modules[:i]+',\n '+json.dumps(module,ensure_ascii=False,separators=(',',':'))+'\n'+modules[i:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
