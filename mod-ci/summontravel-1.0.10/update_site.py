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
 entry={'version': None, 'date': None, 'title': None, 'changes': ['해골이 이미 있어도 매번 현재 최대 수의 새 해골을 소환합니다. 한도가 4라면 기존 4마리 또는 2마리에서 시전해도 새 4마리로 교체됩니다. 새 소환 성공에 따라 게임의 원래 네트워크 해제 처리로 오래된 해골이 교체됩니다.', '새로 소환되는 해골끼리 활·창·검 계열과 방패 유무를 고르게 배정합니다. 기존 해골의 무기 구성 때문에 새 배치가 한 종류로 몰리지 않습니다.', '여왕 등 보스 버프의 빈 컨테이너·텍스트 상자 대신 실제 아이콘과 그려진 글자만 측정합니다. 원래 HP와 버프 사이 간격을 보호막 오른쪽에도 같은 비율로 유지하며 불필요한 추가 여백을 없앴습니다.', 'UI 배율·음식에 따른 HP 크기 변경·세로 HP 배치에도 간격을 맞추고, 보호막 소진 시 버프를 기본 위치로 복원합니다. 기존 머리 위 숫자·남은 시간, 보호막 재충전, 동행·순간이동·회복 기능을 유지합니다.'], 'notes': ['GhostR2에서 업데이트 완료 후 게임을 다시 실행하세요. 친구·전용 서버도 같은 통합팩으로 업데이트하세요.', '동작 검사 240개·게임 DLL API 검사 99개·실제 Harmony 실행 검사 33개를 확인했습니다. 실제 Valheim 화면 확인은 별도로 필요합니다.']}
 entry.update(version=release['version'],date=release['date'],title=release['notes'])
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>','<time data-release-date datetime="2026-11-04">2026.11.04</time>',index);assert n==1
 index=index.replace('modules.js?v=1.5.21','modules.js?v=1.5.22').replace('웹사이트 v1.5.21','웹사이트 v1.5.22')
 modules=(docs/'modules.js').read_text()
 module={'id': 'GhostSummonTravel', 'name': 'Ghost 해골 동행·방어막 표시', 'author': 'Ghost', 'category': 'world', 'icon': '◈', 'version': '1.0.10', 'description': '매번 새 해골로 최대 수 교체 · 다양한 장비 · 기본 간격의 버프 UI', 'points': ['해골이 이미 있어도 매번 현재 최대 수의 새 해골을 소환합니다. 한도가 4라면 기존 4마리 또는 2마리에서 시전해도 새 4마리로 교체됩니다. 새 소환 성공에 따라 게임의 원래 네트워크 해제 처리로 오래된 해골이 교체됩니다.', '새로 소환되는 해골끼리 활·창·검 계열과 방패 유무를 고르게 배정합니다. 기존 해골의 무기 구성 때문에 새 배치가 한 종류로 몰리지 않습니다.', '여왕 등 보스 버프의 빈 컨테이너·텍스트 상자 대신 실제 아이콘과 그려진 글자만 측정합니다. 원래 HP와 버프 사이 간격을 보호막 오른쪽에도 같은 비율로 유지하며 불필요한 추가 여백을 없앴습니다.', 'UI 배율·음식에 따른 HP 크기 변경·세로 HP 배치에도 간격을 맞추고, 보호막 소진 시 버프를 기본 위치로 복원합니다. 기존 머리 위 숫자·남은 시간, 보호막 재충전, 동행·순간이동·회복 기능을 유지합니다.'], 'note': 'GhostR2에서 업데이트 완료 후 게임을 다시 실행하세요.'}
 i=modules.index('{"id":"GhostSummonTravel"');prior,length=json.JSONDecoder().raw_decode(modules[i:]);modules=modules[:i]+json.dumps(module,ensure_ascii=False,separators=(',',':'))+modules[i+length:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
