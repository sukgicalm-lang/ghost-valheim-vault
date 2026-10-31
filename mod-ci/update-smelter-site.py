import hashlib,json,re
from pathlib import Path
root=Path(__file__).resolve().parent.parent
docs=root/'docs'
data=json.loads((root/'mod-ci/smelter-1.0.1/release-input.json').read_text(encoding='utf-8'))
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
section=section.replace('02 / 제련 · 생산','02 / 제련 · 제분').replace('제련소·용광로·숯가마의 기본 생산 시간 1초.<br>원료·연료 한도는 100개, 세부 값은 F1에서.','제련소·용광로·숯가마·풍차의 시간과 투입 한도를 F1에서.<br>기본 1초·100개이며 풍차의 바람 영향은 유지합니다.')
section=re.sub(r'Ghost 제련 제어 v\d+\.\d+\.\d+','Ghost 제련 제어 v1.0.1',section)
assert '풍차' in section
index=index[:index.index(start)]+section+index[index.index(end)+len(end):]
modules=(docs/'modules.js').read_text(encoding='utf-8')
pattern=r'\{"id":"GhostSmelterControl".*?"config":"BepInEx/config/ghost\.valheim\.smeltercontrol\.cfg"\}'
match=re.search(pattern,modules);assert match
old=json.loads(match.group(0));new=dict(old)
new.update(version='1.0.1',description='제련소·용광로·숯가마·풍차 · 기준 1초 · 투입 한도 100개',points=['일반 제련소·용광로·숯가마와 풍차의 처리 시간과 투입 한도를 조절합니다.','기본 시간은 1초, 원료 한도는 100개입니다. 풍차는 기존 바람 세기·가림의 영향을 받습니다.','F1 → Ghost Smelter Control → Windmill에서 Seconds(기준 처리 시간)와 Barley(보리 한도)를 조절합니다.','기존 제련소·용광로·숯가마 설정과 원료 소모·묶음 배출 방식은 유지합니다.'])
modules=modules[:match.start()]+json.dumps(new,ensure_ascii=False,separators=(',',':'))+modules[match.end():]
assert modules[:match.start()]==(docs/'modules.js').read_text(encoding='utf-8')[:match.start()]
guide=(docs/'install.html').read_text(encoding='utf-8')
guide=re.sub(r'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/smelter-\d+\.\d+\.\d+/Ghost-GhostSmelterControl-\d+\.\d+\.\d+\.zip',release['downloadUrl'],guide)
needle='CharcoalKiln은 숯가마입니다.'
if needle in guide:guide=guide.replace(needle,'CharcoalKiln은 숯가마, Windmill은 풍차입니다. Windmill의 Seconds는 기준 처리 시간, Barley는 보리 한도이며 바람의 영향은 유지됩니다.')
assert 'Windmill' in guide
history=json.loads((docs/'update-history.json').read_text(encoding='utf-8'))
entry={'id':'module:GhostSmelterControl:1.0.1','kind':'module','component':'GhostSmelterControl','version':'1.0.1','date':'2026-10-31','title':'풍차 처리 시간·보리 투입량 F1 설정 추가','changes':['Ghost 제련 제어에 풍차를 추가했습니다. Windmill의 Seconds와 Barley 설정으로 기준 처리 시간과 보리 한도를 조절합니다.','새 기본값은 기준 1초·보리 100개입니다. 바람 세기·가림과 생산물 묶음 배출 방식은 유지합니다.','기존 제련소·용광로·숯가마의 F1 설정과 다른 Ghost 게임 DLL은 유지합니다.'],'notes':['게임을 종료하고 Ghost 런처를 열면 독립 모드 1.0.1을 자동으로 준비합니다. 런처 재설치는 필요하지 않습니다.']}
history['siteUpdates']=[entry]+[e for e in history['siteUpdates'] if e.get('id')!=entry['id']]
encoded=json.dumps(history,ensure_ascii=False,separators=(',',':'))
index,count=re.subn(r'(?s)(<script id="ghost-update-history-data" type="application/json">).*?(</script>)',lambda m:m[1]+encoded+m[2],index);assert count==1
updates={'smelter-release.json':json.dumps(release,ensure_ascii=False,indent=2),'smelter-releases.json':json.dumps(catalog,ensure_ascii=False,indent=2),'index.html':index,'install.html':guide,'modules.js':modules,'update-history.json':encoded}
for n,s in updates.items():(docs/n).write_text(s,encoding='utf8')
for n,digest in before.items():assert hashlib.sha256((docs/n).read_bytes()).hexdigest()==digest
print('Updated only independent smelter metadata and windmill descriptions; launcher, suite and 29-package baseline unchanged.')
