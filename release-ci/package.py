import hashlib
import json
import os
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parent
WORK = ROOT.parent / '_smelter_build'
OUT = WORK / 'assets'
LAUNCHER = WORK / 'launcher'
BASE = 'https://github.com/sukgicalm-lang/ghost-valheim-vault/releases/download/'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def pack(path, entries):
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for file, name in sorted(entries, key=lambda x: x[1]):
            info = zipfile.ZipInfo(name, (2026, 10, 30, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            z.writestr(info, file.read_bytes())
    with zipfile.ZipFile(path) as z:
        if z.testzip() is not None:
            raise ValueError('Corrupt release ZIP')

def main():
    app = LAUNCHER / 'dist/electron/Packaged/win-unpacked'
    if not (app / 'GhostR2.exe').is_file() or not (app / 'resources/app.asar').is_file():
        raise ValueError('Missing built Windows runtime')
    pack(OUT / 'GhostR2-Windows-1.0.11.zip', [(p, p.relative_to(app).as_posix())
        for p in app.rglob('*') if p.is_file() and not any(x.startswith('.') for x in p.relative_to(app).parts)])
    entries = []
    for directory, folders, files in os.walk(LAUNCHER):
        folders[:] = [d for d in folders if d not in ('node_modules', 'dist', '.quasar', '.git')]
        for name in files:
            p = Path(directory) / name
            entries.append((p, p.relative_to(LAUNCHER).as_posix()))
    pack(OUT / 'GhostR2-Source-1.0.11.zip', entries)
    runtime = OUT / 'GhostR2-Windows-1.0.11.zip'
    installer = (ROOT / 'Install-GhostR2.template.ps1').read_text(encoding='utf-8-sig')
    if installer.count('__RUNTIME_SHA256__') != 1:
        raise ValueError('Bad installer template')
    (OUT / 'Install-GhostR2.ps1').write_text(installer.replace('__RUNTIME_SHA256__', sha(runtime)), encoding='utf-8-sig', newline='\r\n')
    (OUT / 'INSTALL_GHOST_R2.cmd').write_bytes(b'@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-GhostR2.ps1"\r\n')
    pack(OUT / 'GhostR2-Install-1.0.11.zip', [(OUT / n, n) for n in ('Install-GhostR2.ps1', 'INSTALL_GHOST_R2.cmd')])
    names = [
        ('Ghost-GhostSmelterControl-1.0.0.zip', 'smelter-1.0.0'),
        ('Ghost-SmelterControl-1.0.0-Source.zip', 'smelter-1.0.0'),
        ('GhostR2-Windows-1.0.11.zip', 'client-1.0.11'),
        ('GhostR2-Source-1.0.11.zip', 'client-1.0.11'),
        ('GhostR2-Install-1.0.11.zip', 'client-1.0.11'),
        ('Install-GhostR2.ps1', 'client-1.0.11'),
        ('INSTALL_GHOST_R2.cmd', 'client-1.0.11'),
        ('GhostR2-Setup-1.0.11.exe', 'client-1.0.11'),
    ]
    assets = [{'name': n, 'tag': tag, 'bytes': (OUT / n).stat().st_size, 'sha256': sha(OUT / n)} for n, tag in names]
    plugin = assets[0]
    app_asset = assets[2]
    smelter = {'version': '1.0.0', 'date': '2026-10-30', 'file': 'downloads/' + plugin['name'],
        'bytes': plugin['bytes'], 'sha256': plugin['sha256'], 'downloadUrl': BASE + plugin['tag'] + '/' + plugin['name'],
        'dependencies': ['denikson-BepInExPack_Valheim-5.4.2333'],
        'notes': 'Ghost 두 번째 독립 모드 · 제련소/용광로/숯가마 1초 · 원료/연료 100개 · F1 설정'}
    client = {'version': '1.0.11', 'date': '2026-10-31', 'file': 'installers/' + app_asset['name'],
        'bytes': app_asset['bytes'], 'sha256': app_asset['sha256'], 'downloadUrl': BASE + app_asset['tag'] + '/' + app_asset['name']}
    client.update({'installerUrl': BASE + 'client-1.0.11/GhostR2-Setup-1.0.11.exe', 'installerBytes': assets[-1]['bytes'], 'installerSha256': assets[-1]['sha256']})
    (OUT / 'Release-Bundle.json').write_text(json.dumps({'schema': 1, 'assets': assets, 'smelter': smelter, 'client': client}, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Packaged eight immutable assets; metadata uses the actual CI-built Windows bytes.')

if __name__ == '__main__':
    main()

