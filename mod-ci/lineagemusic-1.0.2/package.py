import base64
import hashlib
import json
import shutil
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
INPUT = Path(__file__).resolve().parent
NEW = "1.0.58"
BASE = "1.0.57"
DATE = "2026-11-14"
NOTES = "리니지 II 배경음악 모드 자동 설치"
REPO = "sukgicalm-lang/ghost-valheim-vault"
PLUGIN = "BepInEx/plugins/Ghost-GhostLineageMusic/"
DLLS = ("GhostLineageMusic.dll", "NVorbis.dll")
FILES = {"manifest.json", "README.md", "CHANGELOG.md", "SHA256SUMS.json"}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def build(base, output):
    spec = json.loads((INPUT / "input.json").read_text())
    assert Path(base).stat().st_size == spec["base_bytes"] and digest(base) == spec["base_sha256"]
    for name, expected in spec["source_hashes"].items():
        assert digest(ROOT / name) == expected, f"Source hash mismatch: {name}"
    output = Path(output)
    with zipfile.ZipFile(base) as prior:
        originals = [entry for entry in prior.infolist() if not entry.is_dir()]
        names = {entry.filename for entry in originals}
        assert len(originals) == len(names)
        manifest = json.loads(prior.read("manifest.json"))
        assert manifest["version_number"] == BASE
        manifest.update(version_number=NEW, description=NOTES)
        readme = (ROOT / "mod-src/LineageMusic/README.md").read_bytes()
        contents = {
            "manifest.json": json.dumps(manifest, ensure_ascii=False, indent=2).encode(),
            "README.md": prior.read("README.md") + f"\n\n## {NEW}\n\n".encode() + readme,
            "CHANGELOG.md": f"# {NEW}\n\n{NOTES}\n\n".encode() + prior.read("CHANGELOG.md"),
            PLUGIN + "NVorbis-LICENSE.txt": (INPUT / "NVorbis-LICENSE.txt").read_bytes(),
        }
        for name in DLLS:
            binary = base64.b64decode((INPUT / (name + ".b64")).read_bytes(), validate=True)
            assert sha(binary) == spec["binary_hashes"][name]
            contents[PLUGIN + name] = binary
        assert set(contents).intersection(names) == FILES - {"SHA256SUMS.json"}
        hashes = json.loads(prior.read("SHA256SUMS.json"))
        assert set(hashes) == names - {"SHA256SUMS.json"}
        hashes.update({name: sha(data) for name, data in contents.items()})
        contents["SHA256SUMS.json"] = json.dumps(hashes, indent=2, sort_keys=True).encode()
        timestamp = (2026, 11, 14, 0, 0, 0)
        with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as current:
            for entry in sorted(originals, key=lambda item: item.filename):
                if entry.filename in FILES:
                    continue
                info = zipfile.ZipInfo(entry.filename, timestamp)
                info.compress_type = zipfile.ZIP_DEFLATED
                with prior.open(entry) as src, current.open(info, "w", force_zip64=True) as dest:
                    shutil.copyfileobj(src, dest)
            for name, data in sorted(contents.items()):
                info = zipfile.ZipInfo(name, timestamp)
                info.compress_type = zipfile.ZIP_DEFLATED
                current.writestr(info, data)
        with zipfile.ZipFile(output) as current:
            assert set(current.namelist()) == names | set(contents)
            assert len(current.namelist()) == len(set(current.namelist()))
            assert all(not n.lower().endswith((".ogg", ".mp3", ".wav", ".flac", ".cfg")) for n in current.namelist())
            for name, expected in hashes.items():
                with current.open(name) as stream:
                    assert hashlib.file_digest(stream, "sha256").hexdigest() == expected, name
    release = {
        "version": NEW, "date": DATE, "file": f"downloads/{output.name}",
        "bytes": output.stat().st_size, "sha256": digest(output), "appIntegration": True,
        "dependencies": spec["dependencies"], "notes": NOTES,
        "downloadUrl": f"https://github.com/{REPO}/releases/download/suite-{NEW}/{output.name}",
    }
    Path(str(output) + ".release.json").write_text(json.dumps(release, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(release, ensure_ascii=False))
    return release


if __name__ == "__main__":
    build(sys.argv[1], sys.argv[2])
