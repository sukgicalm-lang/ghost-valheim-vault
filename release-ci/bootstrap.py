import base64
import hashlib
import json
import pathlib
import urllib.request
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent
WORK = ROOT.parent / '_smelter_build'
WORK.mkdir(exist_ok=True)
SOURCE_URL = 'https://github.com/sukgicalm-lang/ghost-valheim-vault/releases/download/client-1.0.5/GhostR2-Source-1.0.5.zip'
SOURCE_SHA = 'f09cbf26ace512b85e9acd5a64e89c1e42b6269e87b4d08c5fcc81e3653b1958'

def digest(data):
    return hashlib.sha256(data).hexdigest()

def safe_extract(archive, destination):
    with zipfile.ZipFile(archive) as z:
        for name in z.namelist():
            p = pathlib.PurePosixPath(name.replace('\\', '/'))
            if p.is_absolute() or '..' in p.parts or ':' in str(p):
                raise ValueError('Unsafe ZIP path')
        z.extractall(destination)

def main():
    archive = WORK / 'source-1.0.5.zip'
    with urllib.request.urlopen(SOURCE_URL, timeout=120) as response:
        data = response.read()
    if digest(data) != SOURCE_SHA:
        raise ValueError('Pinned launcher source hash mismatch')
    archive.write_bytes(data)
    launcher = WORK / 'launcher'
    safe_extract(archive, launcher)
    patch = json.loads((ROOT / 'launcher-patch.json').read_text(encoding='utf-8'))
    seen = set()
    for row in patch:
        path = pathlib.PurePosixPath(row['path'])
        if str(path) in seen or path.is_absolute() or '..' in path.parts or ':' in str(path):
            raise ValueError('Unsafe/duplicate patch path')
        if path.parts[0] not in ('src', 'src-electron', 'test') and str(path) not in ('package.json', 'BUILD_GHOST_1.0.6.md', 'BUILD_GHOST_1.0.7.md', 'BUILD_GHOST_1.0.8.md', 'BUILD_GHOST_1.0.9.md', 'BUILD_GHOST_1.0.10.md', 'BUILD_GHOST_1.0.11.md', 'scripts/check-ghost-release.cjs'):
            raise ValueError('Unexpected launcher patch scope')
        seen.add(str(path))
        target = launcher / str(path)
        if row['old_sha256']:
            if not target.is_file() or digest(target.read_bytes()) != row['old_sha256']:
                raise ValueError('Launcher patch baseline mismatch: ' + str(path))
        elif target.exists():
            raise ValueError('New launcher path already exists')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(row['content'], encoding='utf-8', newline='')
    if json.loads((launcher / 'package.json').read_text())['version'] != '1.0.11':
        raise ValueError('Wrong launcher version')
    inputs = json.loads((ROOT / 'input-manifest.json').read_text())
    output = WORK / 'assets'
    output.mkdir(exist_ok=True)
    for item in inputs['payloads']:
        data = base64.b64decode((ROOT / item['base64_file']).read_text(), validate=True)
        if digest(data) != item['sha256'] or len(data) != item['bytes']:
            raise ValueError('Release input hash mismatch')
        (output / item['name']).write_bytes(data)
    safe_extract(output / 'Ghost-GhostSmelterControl-1.0.0.zip', WORK / 'smelter-fixture')
    with zipfile.ZipFile(output / 'Ghost-GhostSmelterControl-1.0.0.zip') as z:
        manifest = json.loads(z.read('manifest.json'))
        dlls = [name for name in z.namelist() if name.endswith('.dll')]
        if manifest['name'] != 'GhostSmelterControl' or manifest['version_number'] != '1.0.0' or dlls != ['plugins/GhostSmelterControl/GhostSmelterControl.dll']:
            raise ValueError('Unexpected smelter package structure')
    print(f'Verified original launcher source, applied {len(patch)} scoped changes, and verified exact smelter release inputs.')

if __name__ == '__main__':
    main()

