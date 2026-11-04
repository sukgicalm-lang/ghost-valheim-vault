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
 entry={'version': None, 'date': None, 'title': None, 'changes': ['해골 소환을 한 번 시전하면 현재 지팡이·숙련도 설정이 허용하는 최대 수까지 부족한 해골을 채웁니다. 이미 있는 살아 있는 해골은 체력·장비·보호막을 유지하며 최대 수에서는 새로 덮어 소환하지 않습니다.', '기존 해골의 무기까지 고려해 적게 있는 활·창·검 계열을 우선 배정하고, 같은 계열 안에서는 방패 유무도 분산합니다. 원래 해골이 가질 수 있는 장비 안에서 조절하며 선택값을 저장해 친구 화면·재접속 후에도 같은 장비를 유지합니다.', '동시에 시전해도 진행 중인 소환 자리를 함께 계산해 최대 수를 넘지 않게 하고, 실제 생성·능력치·명령·시전 자원 처리는 게임의 기존 소환 과정을 사용합니다.', '내 캐릭터 머리 위 HP·방어막 수치·이름·막대를 없앴습니다. 방어막이 있을 때만 원래 이름 크기의 작은 남은 시간(예: 1:48)을 표시합니다.', '해골은 이름 아래 HP 현재 / 최대치, 다음 줄에 방어막 잔량 / 최대치만 표시합니다. HP·방어막이라는 앞글자와 막대·배경 패널을 제거하고 숫자를 이름보다 약간 작게 표시합니다.', '본인과 해골의 보호막 남은 시간은 이름보다 한 줄 위에 이름과 동일한 글자 크기로 표시하며, 보호막 소진·만료 시 즉시 숨깁니다. 숫자와 시간은 실제 효과 값에서 읽고 원격 해골은 동기화된 기록의 경과 시간을 반영합니다.', '내 HP 오른쪽 파란 보호막은 유지합니다. 여왕 등 보스 버프의 아이콘·이름·준비/재사용 대기시간 전체가 겹치지 않도록, 실제 UI 크기와 배율을 확인해 보스 버프 묶음을 오른쪽으로 이동합니다. 보호막이 사라지면 원래 배치를 복원합니다.', '보호막 재시전 충전·20m 적용 반경·순간이동 잔량과 시간 보존·100m 또는 10초 걸림 복귀·캐릭터 기준 해골 회복을 유지합니다.'], 'notes': ['GhostR2는 켜둬도 업데이트를 자동 확인합니다. 업데이트 완료 후 게임만 다시 실행하세요. 친구·전용 서버도 같은 통합팩으로 업데이트하세요.', '동작 검사 227개·게임 DLL API 검사 97개·실제 Harmony 실행 검사 33개를 배포 전에 확인합니다. 실제 Valheim 월드 화면 확인은 별도로 필요합니다.']}
 entry.update(version=release['version'],date=release['date'],title=release['notes'])
 history['currentVersion']=release['version'];history['entries']=[entry]+[e for e in history['entries'] if e['version']!=release['version']]
 index=(docs/'index.html').read_text()
 for element,data in [('ghost-release-default',release),('ghost-update-history-data',history)]:
  index,n=re.subn(r'(?s)(<script id="'+element+r'" type="application/json">).*?(</script>)',lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[2],index);assert n==1
 for pattern,value in [(r'(<strong data-release-version>).*?(</strong>)','v'+release['version']),(r'(<h3 data-release-title>).*?(</h3>)',html.escape(release['notes'])),(r'(<p id="release-notes">).*?(</p>)',html.escape(release['notes'])),(r'(<code id="release-hash">).*?(</code>)',release['sha256'][:8]+'…'+release['sha256'][-8:]),(r'(<span data-release-size>).*?(</span>)',f"{release['bytes']/1048576:.2f} MiB · ZIP")]:
  index,n=re.subn(pattern,lambda m:m[1]+value+m[2],index);assert n==1
 index,n=re.subn(r'<time data-release-date datetime="[^"]+">.*?</time>','<time data-release-date datetime="2026-11-04">2026.11.04</time>',index);assert n==1
 index=index.replace('modules.js?v=1.5.20','modules.js?v=1.5.21').replace('웹사이트 v1.5.20','웹사이트 v1.5.21')
 modules=(docs/'modules.js').read_text()
 module={'id': 'GhostSummonTravel', 'name': 'Ghost 해골 동행·방어막 표시', 'author': 'Ghost', 'category': 'world', 'icon': '◈', 'version': '1.0.9', 'description': '한 번에 최대 수 소환 · 다양한 해골 장비 · 숫자와 남은 시간만 표시', 'points': ['해골 소환을 한 번 시전하면 현재 지팡이·숙련도 설정이 허용하는 최대 수까지 부족한 해골을 채웁니다. 이미 있는 살아 있는 해골은 체력·장비·보호막을 유지하며 최대 수에서는 새로 덮어 소환하지 않습니다.', '기존 해골의 무기까지 고려해 적게 있는 활·창·검 계열을 우선 배정하고, 같은 계열 안에서는 방패 유무도 분산합니다. 원래 해골이 가질 수 있는 장비 안에서 조절하며 선택값을 저장해 친구 화면·재접속 후에도 같은 장비를 유지합니다.', '동시에 시전해도 진행 중인 소환 자리를 함께 계산해 최대 수를 넘지 않게 하고, 실제 생성·능력치·명령·시전 자원 처리는 게임의 기존 소환 과정을 사용합니다.', '내 캐릭터 머리 위 HP·방어막 수치·이름·막대를 없앴습니다. 방어막이 있을 때만 원래 이름 크기의 작은 남은 시간(예: 1:48)을 표시합니다.', '해골은 이름 아래 HP 현재 / 최대치, 다음 줄에 방어막 잔량 / 최대치만 표시합니다. HP·방어막이라는 앞글자와 막대·배경 패널을 제거하고 숫자를 이름보다 약간 작게 표시합니다.', '본인과 해골의 보호막 남은 시간은 이름보다 한 줄 위에 이름과 동일한 글자 크기로 표시하며, 보호막 소진·만료 시 즉시 숨깁니다. 숫자와 시간은 실제 효과 값에서 읽고 원격 해골은 동기화된 기록의 경과 시간을 반영합니다.', '내 HP 오른쪽 파란 보호막은 유지합니다. 여왕 등 보스 버프의 아이콘·이름·준비/재사용 대기시간 전체가 겹치지 않도록, 실제 UI 크기와 배율을 확인해 보스 버프 묶음을 오른쪽으로 이동합니다. 보호막이 사라지면 원래 배치를 복원합니다.', '보호막 재시전 충전·20m 적용 반경·순간이동 잔량과 시간 보존·100m 또는 10초 걸림 복귀·캐릭터 기준 해골 회복을 유지합니다.'], 'note': 'GhostR2는 켜둔 채로 자동 갱신됩니다. 모드 업데이트 완료 후 게임만 다시 실행하세요. 실제 게임 화면·동작 확인이 필요합니다.'}
 i=modules.index('{"id":"GhostSummonTravel"');prior,length=json.JSONDecoder().raw_decode(modules[i:]);modules=modules[:i]+json.dumps(module,ensure_ascii=False,separators=(',',':'))+modules[i+length:]
 guide=(docs/'install.html').read_text();guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/suite-\d+\.\d+\.\d+/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
 for name,value in [('release.json',release),('releases.json',catalog),('update-history.json',history)]:(docs/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
 for name,value in [('index.html',index),('modules.js',modules),('install.html',guide)]:(docs/name).write_text(value)
 assert all((docs/n).read_bytes()==v for n,v in before.items());assert len(json.loads((docs/'setup-release.json').read_text())['packages'])==28
 print('Updated suite, homepage history, Korean feature details and manual link. Protected launcher/smelter/setup feeds unchanged.')
if __name__=='__main__':update(json.loads(Path(sys.argv[1]).read_text()))
