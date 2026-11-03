import json,os,subprocess,urllib.request
from pathlib import Path
from package import build,ROOT,NEW,NOTES
from update_site import update
REPO='sukgicalm-lang/ghost-valheim-vault';TAG='suite-'+NEW
def gh(*args):return subprocess.check_output(['gh',*args],text=True).strip()
def run(*args):subprocess.run(args,check=True,cwd=ROOT)
assert os.environ.get('GITHUB_REPOSITORY')==REPO
work=Path(os.environ['RUNNER_TEMP'])/'ghost-summontravel-1.0.3';work.mkdir(exist_ok=True);base=work/'base.zip'
if not base.exists():urllib.request.urlretrieve('https://github.com/'+REPO+'/releases/download/suite-1.0.43/Ghost-GhostValheimSuite-1.0.43.zip',base)
asset=work/f'Ghost-GhostValheimSuite-{NEW}.zip';build(base,asset);release=json.loads(Path(str(asset)+'.release.json').read_text())
try:meta=json.loads(gh('api',f'repos/{REPO}/releases/tags/{TAG}'))
except subprocess.CalledProcessError:
 gh('release','create',TAG,'--repo',REPO,'--target',os.environ['GITHUB_SHA'],'--title','Ghost 통합팩 '+NEW+' — 해골 100m·10초 복귀와 로딩·방어막 표시 수정','--notes',NOTES);meta=json.loads(gh('api',f'repos/{REPO}/releases/tags/{TAG}'))
existing=[a for a in meta['assets'] if a['name']==asset.name]
if existing:assert existing[0]['size']==release['bytes'] and existing[0].get('digest')=='sha256:'+release['sha256'],'Immutable release conflict'
else:gh('release','upload',TAG,str(asset),'--repo',REPO)
meta=json.loads(gh('api',f'repos/{REPO}/releases/tags/{TAG}'));remote=next(a for a in meta['assets'] if a['name']==asset.name)
assert remote['size']==release['bytes'] and remote['digest']=='sha256:'+release['sha256'] and remote['state']=='uploaded' and remote['browser_download_url']==release['downloadUrl']
run('git','pull','--ff-only','origin','main');update(release);run('git','config','user.name','github-actions[bot]');run('git','config','user.email','41898282+github-actions[bot]@users.noreply.github.com')
run('git','add','--','docs/release.json','docs/releases.json','docs/update-history.json','docs/index.html','docs/modules.js','docs/install.html')
if subprocess.run(['git','diff','--cached','--quiet'],cwd=ROOT).returncode:
 run('git','commit','-m','Publish Ghost suite 1.0.44 bounded recovery, loading fix and player shield bar');run('git','push','origin','HEAD:main')
gh('api','--method','POST',f'repos/{REPO}/pages/builds');print('Uploaded verified immutable suite and exposed automatic update:',release['sha256'])
