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
 entry={'version':release['version'],'date':release['date'],'title':release['notes'],'changes':['순간이동 후 해골의 방어막 수치가 남아 있는데 이펙트만 사라지는 경로를 수정했습니다. 버프 재시전·소유권 전환·이펙트 유실 시 원래 방어막 이펙트를 다시 생성합니다.','해골 체력바 오른쪽에 방어막 413 / 450처럼 남은 흡수량과 전체 흡수량을 표시합니다. 피해·만료·재시전에 따라 갱신합니다.','순간이동과 자동 합류 때 캐릭터 뒤쪽·양옆을 우선하며 몸 사이에 최소 1m의 여유를 둡니다.','안전한 자리가 없으면 캐릭터와 겹치게 놓지 않고 위치 탐색을 재시도합니다. 기존 체력·방어막 잔량·남은 시간은 유지합니다.'],'notes':['게임과 GhostR2를 완전히 종료한 뒤 다시 실행하면 통합팩 1.0.42를 자동 준비합니다. 친구·전용 서버도 같은 버전으로 업데이트하세요.','자동 검사 96개 통과. 실제 게임 화면·친구 PC에서의 실행 확인은 별도로 필요합니다.']}
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>','<time data-release-date datetime="2026-11-03">2026.11.03</time>',index);assert n==1
 index=index.replace('modules.js?v=1.5.12','modules.js?v=1.5.13').replace('웹사이트 v1.5.12','웹사이트 v1.5.13')
 modules=(docs/'modules.js').read_text()
 module={'id':'GhostSummonTravel','name':'Ghost 해골 동행·방어막 표시','author':'Ghost','category':'world','icon':'◈','version':'1.0.1','description':'방어막 이펙트·실시간 잔량 표시 · 도착 시 몸 사이 1m 여유','points':['나를 따라오는 소환 해골이 순간이동과 포탈을 함께 이용하고, 지형에 막히면 자동 합류합니다.','방어막 이펙트가 사라졌을 때와 버프 재시전 때 원래 이펙트를 다시 생성합니다.','체력바 오른쪽에 방어막 413 / 450처럼 남은 흡수량과 전체 흡수량을 실시간 표시합니다.','캐릭터 뒤쪽·양옆에 몸 사이 최소 1m 여유를 두고 도착합니다. 안전한 자리가 없으면 다시 탐색합니다.','기존 체력·방어막 잔량·남은 시간을 유지합니다. 별도 설정 없이 적용됩니다.'],'note':'친구·전용 서버도 같은 통합팩으로 업데이트하세요. 실제 게임 화면에서 동작 확인이 필요합니다.'}
 i=modules.index('{"id":"GhostSummonTravel"');prior,length=json.JSONDecoder().raw_decode(modules[i:]);modules=modules[:i]+json.dumps(module,ensure_ascii=False,separators=(',',':'))+modules[i+length:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
