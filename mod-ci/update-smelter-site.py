import hashlib,json,re
from pathlib import Path
root=Path(__file__).resolve().parent.parent
docs=root/'docs'
data=json.loads((root/'mod-ci/smelter-1.0.3/release-input.json').read_text(encoding='utf-8'))
release=data['release']
protected=['release.json','releases.json','client-release.json','setup-release.json']
before={n:hashlib.sha256((docs/n).read_bytes()).hexdigest() for n in protected}
prior=json.loads((docs/'smelter-release.json').read_text(encoding='utf-8'))
version=lambda s:tuple(map(int,s.split('.')))
if version(prior['version'])>version(release['version']):raise ValueError('A newer module is already published')
if prior['version']==release['version'] and (prior['sha256']!=release['sha256'] or prior['bytes']!=release['bytes']):raise ValueError('Same version has different bytes')
catalog=json.loads((docs/'smelter-releases.json').read_text(encoding='utf-8'))
for old in catalog:
 if old['version']==release['version'] and old['sha256']!=release['sha256']:raise ValueError('Catalog conflict')
catalog=[release]+sorted([r for r in catalog if r['version']!=release['version']],key=lambda r:version(r['version']),reverse=True)
index=(docs/'index.html').read_text(encoding='utf-8')
start='<!-- GHOST-SMELTER-BEGIN -->';end='<!-- GHOST-SMELTER-END -->'
assert index.count(start)==index.count(end)==1
section=index[index.index(start):index.index(end)+len(end)]
section=re.sub(r'<p>.*?</p>','<p>제련소·용광로·숯가마·풍차를 한글 F1 메뉴에서.<br>원래 속도·2배·5배·10배와 최대 보관량을 쉽게 선택하세요.</p>',section)
section=re.sub(r'Ghost 제련 제어 v\d+\.\d+\.\d+','Ghost 제련 제어 v'+release['version'],section)
index=index[:index.index(start)]+section+index[index.index(end)+len(end):]
for old,new in {
 'F1 → Ghost Wardrobe':'F1 → Ghost 외형 꾸미기',
 'BodyShape의 Valheim은 발헤임 체형, Original은 원본 모델의 신체 비율을 사용합니다.':'의상에 맞출 체형에서 발헤임 체형 또는 원작 체형을 선택합니다. 둠 경갑은 원작 비율을 유지합니다.',
 'AnimateCapes로 광전사·해골기사의 망토 물리를 켜고 끕니다. Original은 원작 비율을 유지하고 전체 키를 게임 크기에 맞춥니다.':'광전사·해골기사 망토 움직임에서 망토가 움직일지 선택합니다. 원작 체형은 원작 비율을 유지하고 전체 키를 게임 크기에 맞춥니다.',
 '→ Tyrael Energy':'→ 티리엘 옷의 빛',
 'Tyrael Wings에서 움직임·크기·농도를 조절합니다.':'티리엘 날개에서 움직임·크기·빛 농도를 조절합니다. 크기는 배수, 빛 농도는 퍼센트로 표시합니다.'
}.items():index=index.replace(old,new)
modules=(docs/'modules.js').read_text(encoding='utf-8')
changes={
 'GhostSmelterControl':dict(name='Ghost 제련·제분 설정',version=release['version'],description='원래 속도·2배·5배·10배 선택 · 원료 최대 보관량 100개',points=['F1 → Ghost 제련·제분 설정에서 일반 제련소·용광로·숯가마·풍차를 선택합니다.','생산 속도는 원래 속도·2배·5배·10배 버튼이나 원하는 배수로 조절합니다. 현재 배수는 게임에서 읽은 기준으로 표시합니다.','원료·석탄 최대 보관량은 안에 넣어 둘 수 있는 개수입니다. 기본 100개이며 생산량이나 클릭 한 번의 투입량이 아닙니다.','풍차는 바람과 주변 가림의 영향을 계속 받습니다. 처리 주기와 묶음 배출 때문에 매초 한 개씩 나온다는 뜻은 아닙니다.','Ghost 외형·생활·성장과 지도 순간이동의 F1 설정도 한글 이름·설명·단위로 표시합니다.']),
 'GhostDoomLight':dict(name='Ghost 외형 꾸미기',points=['Tab → 외형에서 머리·상의·하의·망토를 부위별로 선택합니다.','둠·광전사·해골기사·군주 탐식자·티리엘 의상과 티리엘 빛의 날개를 제공합니다.','F1 → Ghost 외형 꾸미기에서 발헤임 체형·원작 체형, 망토·날개 움직임을 한글로 선택합니다.','크기·밝기·빛 흐름은 배수, 날개 빛 농도는 퍼센트입니다. 각 항목 아래에 숫자의 뜻을 설명합니다.','외형을 바꿔도 실제 장비 능력치는 유지됩니다.']),
 'GhostValheimTweaks':dict(name='Ghost 생활·성장 설정',description='자동 줍기 항상 사용 · 숙련도 경험치 · 발효 · 벨트 무게',points=['자동 줍기는 항상 사용합니다. 줍기 거리·인벤토리 여유 등 기본 게임의 줍기 조건은 유지합니다.','F1 → Ghost 생활·성장 설정에서 발효 완료 시간을 분으로 입력합니다. 5는 5분입니다.','경험치는 1이면 원래 경험치, 2면 경험치 2배입니다. 현재 숙련도 레벨과 사망 손실은 유지합니다.','벨트 무게는 추가 무게가 아니라 착용 후 기준 총 한도입니다. 450이면 기본 중량 300 기준으로 450까지 듭니다.']),
 'GhostBowDraw':dict(name='Ghost 활 당김 속도',points=['일반 활을 완전히 당기는 시간을 원래의 절반으로 줄입니다. 숙련도 효과도 유지합니다.','활 당김 속도 2배로 고정되어 F1에서 바꾸는 항목은 없습니다. 발사 후 동작까지 포함한 전체 연사 속도 2배를 뜻하지 않습니다.','석궁과 NPC는 제외합니다.']),
 'GhostBowSkill':dict(name='Ghost 활 9발 강공격',description='마우스 휠 강공격 · 화살 9개 소모 · 고정 설정',points=['마우스 휠 강공격으로 가로 5발·위 2발·아래 2발, 총 9발을 발사하고 화살 9개를 사용합니다.','각 화살은 일반 완전 당김 화살의 1.5배 피해입니다. 9발 모두 맞을 때 계산상 합계는 13.5배이며 저항·중첩 등 게임 규칙이 적용됩니다.','강공격 준비 시간은 일반 활 당김의 2.5배입니다. 좌우 최대 3.6도·상하 최대 1.8도로 퍼집니다.','강공격에만 더 큰 시각 효과와 속성 색 궤적을 표시합니다.','수치는 고정되어 F1에서 바꾸는 항목은 없습니다.']),
 'MapTeleport':dict(name='지도 순간이동 (Ghost 수정)',points=['큰 지도에서 F1로 선택한 키를 누른 채 마우스 왼쪽 버튼으로 탐험한 목적지를 누릅니다.','F1 → 지도 순간이동 (Ghost 수정)에서 왼쪽 Alt·Ctrl·Shift 키 중 하나를 선택합니다.','미탐험 지역에는 이동할 수 없습니다. 현재 위치에서 수평거리 100m 이상일 때만 사용할 수 있습니다.','포탈 금지 광물 소지와 적에게 감지된 상태의 사용 제한은 이 수정본에서 해제되어 있습니다.'])
}
decoder=json.JSONDecoder()
for module_id,values in changes.items():
 pos=modules.index('{"id":"'+module_id+'"');item,length=decoder.raw_decode(modules[pos:]);item.update(values)
 modules=modules[:pos]+json.dumps(item,ensure_ascii=False,separators=(',',':'))+modules[pos+length:]
guide=(docs/'install.html').read_text(encoding='utf-8')
guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/smelter-\d+\.\d+\.\d+/Ghost-GhostSmelterControl-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
guide,count=re.subn(r'(<summary>제련 속도와 투입량은 어디에서 바꾸나요\?</summary>)<p>.*?</p>',r'\1<p>월드에 들어간 뒤 F1 → Ghost 제련·제분 설정에서 시설을 선택하세요. 원래 속도·2배·5배·10배 버튼이나 직접 입력한 배수로 생산 속도를 정합니다. 최대 보관량은 안에 넣어 둘 수 있는 개수이며 기본 100개입니다. 풍차는 바람과 가림의 영향을 계속 받습니다. Ghost 외형·생활·성장 설정도 한글 설명과 단위로 표시합니다.</p>',guide);assert count==1
history=json.loads((docs/'update-history.json').read_text(encoding='utf-8'))
entry={'id':'module:GhostSmelterControl:1.0.3','kind':'module','component':'GhostSmelterControl','version':'1.0.3','date':'2026-10-31','title':'Ghost F1 설정을 한글 이름·버튼·설명으로 정리','changes':['한글 설정의 선택 버튼과 직접 입력 기능이 실제 F1 구조에서도 연결되도록 보완했습니다.','생산 속도를 원래 속도·2배·5배·10배로 선택하고 현재 배수를 표시합니다. 최대 보관량은 개수로 설명합니다.','Ghost 외형·생활·성장과 지도 순간이동의 설정 40개를 한글로 정리했습니다. 발효는 분, 경험치·외형 크기는 배수, 날개 빛 농도는 퍼센트입니다.','설명을 각 항목 아래에도 표시합니다. 현재 미사용인 이전 설정과 조절 항목 없는 고정 활 기능도 명확히 안내합니다.','기존 개인 설정과 게임 동작을 유지합니다. 풍차의 바람 영향도 그대로입니다.'],'notes':['게임을 종료하고 Ghost 런처를 다시 열면 자동으로 준비합니다. 런처 재설치나 통합팩 재다운로드는 필요하지 않습니다.']}
history['siteUpdates']=[entry]+[e for e in history['siteUpdates'] if e.get('id')!=entry['id']]
encoded=json.dumps(history,ensure_ascii=False,separators=(',',':'))
index,count=re.subn(r'(?s)(<script id="ghost-update-history-data" type="application/json">).*?(</script>)',lambda m:m[1]+encoded+m[2],index);assert count==1
updates={'smelter-release.json':json.dumps(release,ensure_ascii=False,indent=2),'smelter-releases.json':json.dumps(catalog,ensure_ascii=False,indent=2),'index.html':index,'install.html':guide,'modules.js':modules,'update-history.json':encoded}
for n,s in updates.items():(docs/n).write_text(s,encoding='utf-8')
for n,digest in before.items():assert hashlib.sha256((docs/n).read_bytes()).hexdigest()==digest
print('Updated Korean F1 descriptions and module metadata. Suite, launcher and required package baseline preserved.')
