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
 entry={'version': None, 'date': None, 'title': None, 'changes': ['해골 소환 해제 시 다른 구역에 저장된 내 소환 해골까지 월드에서 제거합니다.', '진행 중인 소환도 함께 취소합니다. 다른 구역의 해골을 정리할 수 있도록 인벤토리 버튼을 계속 표시합니다.'], 'notes': ['GhostR2에서 업데이트를 마친 뒤 게임을 다시 실행하세요.', '발헤임 1.0.21 게임 DLL 대상 빌드와 자동 검증을 완료했습니다. 실제 플레이 화면 확인은 필요합니다.']}
 entry.update(version=release['version'],date=release['date'],title=release['notes'])
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>',f'''<time data-release-date datetime="{release['date']}">{release['date'].replace('-', '.')}</time>''',index);assert n==1
 index=index.replace('modules.js?v=1.5.27','modules.js?v=1.5.28').replace('웹사이트 v1.5.27','웹사이트 v1.5.28')
 modules=(docs/'modules.js').read_text()
 module={'id': 'GhostSummonTravel', 'name': 'Ghost 해골 동행·방어막 표시', 'author': 'Ghost', 'category': 'world', 'icon': '◈', 'version': '1.0.16', 'description': '소환 해제 후 해골 개체가 다시 나타나는 현상 수정', 'points': ['해골 소환 해제 시 다른 구역에 저장된 내 소환 해골까지 월드에서 제거합니다.', '진행 중인 소환도 함께 취소합니다. 다른 구역의 해골을 정리할 수 있도록 인벤토리 버튼을 계속 표시합니다.'], 'note': 'GhostR2 업데이트 후 게임을 다시 실행하세요.'}
 i=modules.index('{"id":"GhostSummonTravel"');prior,length=json.JSONDecoder().raw_decode(modules[i:]);modules=modules[:i]+json.dumps(module,ensure_ascii=False,separators=(',',':'))+modules[i+length:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
