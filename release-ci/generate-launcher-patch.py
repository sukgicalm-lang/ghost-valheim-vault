"""Build the source patch from all changed managed files, never a hand-maintained path list."""
import hashlib,json,pathlib,zipfile
ROOT=pathlib.Path(__file__).resolve().parent
SOURCE=ROOT.parent/'_smelter_build/launcher'
with zipfile.ZipFile(ROOT.parent/'_smelter_build/source-1.0.5.zip') as z:
    original={n:z.read(n) for n in z.namelist() if not n.endswith('/')}
    def managed(p):
        return not any(part.startswith('.') for part in pathlib.PurePosixPath(p).parts) and (p.split('/')[0] in ('src','src-electron','test') or p=='package.json' or p=='scripts/check-ghost-release.cjs' or (p.startswith('BUILD_GHOST_') and p.endswith('.md')))
    current={p.relative_to(SOURCE).as_posix():p for folder in ('src','src-electron','test') for p in (SOURCE/folder).rglob('*') if p.is_file() and managed(p.relative_to(SOURCE).as_posix())}
    current.update({p.relative_to(SOURCE).as_posix():p for p in SOURCE.glob('BUILD_GHOST_*.md')})
    for p in ('package.json','scripts/check-ghost-release.cjs'):current[p]=SOURCE/p
    missing=[p for p in original if managed(p) and p not in current]
    if missing:raise ValueError('Managed deletions need explicit handling: '+str(missing))
    rows=[]
    for name,p in sorted(current.items()):
        data=p.read_bytes();prior=original.get(name)
        if prior==data:continue
        rows.append({'path':name,'old_sha256':hashlib.sha256(prior).hexdigest() if prior is not None else None,'content':data.decode('utf-8')})
    required={'src/pages/Manager.vue','src/store/modules/DownloadModule.ts','src/ghost/GhostRequiredSetup.ts','src/components/GhostRequiredSetupNotice.vue'}
    if not required.issubset({r['path'] for r in rows}):raise ValueError('Automatic setup integration files missing from source patch')
    (ROOT/'launcher-patch.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Included all',len(rows),'changed managed source files, including automatic manager and download wiring.')
