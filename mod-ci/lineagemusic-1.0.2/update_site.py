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
 entry={'version': None, 'date': None, 'title': None, 'changes': ['GhostR2 업데이트로 리니지 II 상황별 배경음악 모드를 설치합니다.', '설치된 리니지 II 클라이언트의 음원을 자동으로 찾습니다. 침대 반경 100m 마을곡, 필드곡, 전투곡을 상황에 맞춰 섞어 재생합니다.'], 'notes': ['리니지 II 클라이언트나 지정 음원이 없는 PC에서는 발헤임 기본 음악이 재생됩니다. 별도 설치 경로는 F1의 LineageFolder에서 지정할 수 있습니다.']}
 entry.update(version=release['version'],date=release['date'],title=release['notes'])
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>',f'''<time data-release-date datetime="{release['date']}">{release['date'].replace('-', '.')}</time>''',index);assert n==1
 index=index.replace('modules.js?v=1.5.28','modules.js?v=1.5.29').replace('웹사이트 v1.5.28','웹사이트 v1.5.29')
 modules=(docs/'modules.js').read_text()
 module={'id': 'GhostLineageMusic', 'name': 'Ghost 리니지 II 배경음악', 'author': 'Ghost', 'category': 'world', 'icon': '♪', 'version': '1.0.2', 'description': '침대 반경 100m · 필드 · 전투에 따라 자동 재생', 'points': ['GhostR2 통합팩 업데이트에 포함됩니다.', '이 PC에 설치된 리니지 II 클라이언트의 music 폴더를 자동으로 찾습니다.'], 'note': '리니지 II 클라이언트가 설치돼 있어야 해당 음원을 재생합니다.'}
 assert '{"id":"GhostLineageMusic"' not in modules
 modules=modules.replace('const modules = [','const modules = [\n '+json.dumps(module,ensure_ascii=False,separators=(',',':'))+',',1)
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
