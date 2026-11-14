import json
import os
import subprocess
import urllib.request
from pathlib import Path

from package import BASE, NEW, NOTES, REPO, ROOT, build
from update_site import update


def gh(*args):
    return subprocess.check_output(["gh", *args], text=True)


def git(*args):
    subprocess.run(["git", *args], check=True, cwd=ROOT)


def version(value):
    return tuple(map(int, value.split(".")))


assert os.environ["GITHUB_REPOSITORY"] == REPO
git("pull", "--ff-only", "origin", "main")
current = json.loads((ROOT / "docs/release.json").read_text())
if version(current["version"]) >= version(NEW):
    print("Suite feed already at", current["version"])
    raise SystemExit(0)
assert current["version"] == BASE
work = Path(os.environ["RUNNER_TEMP"]) / "ghost-lineagemusic"
work.mkdir(exist_ok=True)
base = work / "base.zip"
urllib.request.urlretrieve(current["downloadUrl"], base)
asset = work / f"Ghost-GhostValheimSuite-{NEW}.zip"
release = build(base, asset)
tag = f"suite-{NEW}"

try:
    metadata = json.loads(gh("api", f"repos/{REPO}/releases/tags/{tag}"))
except subprocess.CalledProcessError:
    gh("release", "create", tag, "--repo", REPO, "--target", os.environ["GITHUB_SHA"],
       "--title", f"Ghost 통합팩 {NEW} — {NOTES}", "--notes", NOTES)
    metadata = json.loads(gh("api", f"repos/{REPO}/releases/tags/{tag}"))
if not any(item["name"] == asset.name for item in metadata["assets"]):
    gh("release", "upload", tag, str(asset), "--repo", REPO)
metadata = json.loads(gh("api", f"repos/{REPO}/releases/tags/{tag}"))
remote = next(item for item in metadata["assets"] if item["name"] == asset.name)
assert remote["size"] == release["bytes"] and remote["digest"] == "sha256:" + release["sha256"]
assert remote["state"] == "uploaded" and remote["browser_download_url"] == release["downloadUrl"]

git("pull", "--ff-only", "origin", "main")
update(release)
git("config", "user.name", "github-actions[bot]")
git("config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com")
git("add", "--", "docs/release.json", "docs/releases.json", "docs/update-history.json",
    "docs/index.html", "docs/modules.js", "docs/install.html")
git("commit", "-m", f"Publish Ghost suite {NEW} with Lineage II music auto-discovery")
git("push", "origin", "HEAD:main")
gh("api", "--method", "POST", f"repos/{REPO}/pages/builds")
