$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = 'sukgicalm-lang/ghost-valheim-vault'
if ($env:GITHUB_REPOSITORY -cne $repo) { throw 'Unexpected repository' }
$root = Split-Path $PSScriptRoot -Parent
$inputRoot = Join-Path $PSScriptRoot 'smelter-1.0.1'
$manifest = Get-Content (Join-Path $inputRoot 'release-input.json') -Raw -Encoding utf8 | ConvertFrom-Json
$assets = Join-Path $env:RUNNER_TEMP 'ghost-smelter-assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
$script:FastTemp = Join-Path $env:RUNNER_TEMP ('ghost-smelter-publish-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $script:FastTemp -Force | Out-Null
$script:GhostGh = (Get-Command gh).Source
. (Join-Path $root 'release-ci/GhostFastCommon.ps1')
. (Join-Path $root 'release-ci/Build-WebsitePatch.ps1')
foreach ($asset in $manifest.assets) {
    if ($asset.name -notmatch '^Ghost-(GhostSmelterControl|SmelterControl)-1\.0\.1(-Source)?\.zip$') { throw 'Unexpected module asset name' }
    $bytes = [Convert]::FromBase64String([IO.File]::ReadAllText((Join-Path $inputRoot $asset.base64_file)))
    $path = Join-Path $assets $asset.name
    [IO.File]::WriteAllBytes($path,$bytes)
    if ($bytes.Length -ne $asset.bytes -or (Get-FastHash $path) -cne $asset.sha256) { throw 'Module input hash mismatch' }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $assets $manifest.assets[0].name))
try {
    $dlls = @($zip.Entries | Where-Object { $_.FullName.EndsWith('.dll') })
    if ($dlls.Count -ne 1 -or $dlls[0].FullName -cne 'plugins/GhostSmelterControl/GhostSmelterControl.dll') { throw 'Unexpected DLL scope' }
    if (@($zip.Entries | Where-Object { $_.FullName.EndsWith('.cfg') }).Count) { throw 'Package must preserve user configs' }
    foreach ($entry in $zip.Entries) { if ($entry.FullName -match '(^/|:|(^|/)\.\.(/|$))') { throw 'Unsafe module ZIP path' } }
    $reader = [IO.StreamReader]::new($zip.GetEntry('manifest.json').Open())
    try { $package = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($package.name -cne 'GhostSmelterControl' -or $package.version_number -cne '1.0.1') { throw 'Wrong module identity' }
    $dll = Join-Path $script:FastTemp 'GhostSmelterControl.dll'
    [IO.Compression.ZipFileExtensions]::ExtractToFile($dlls[0],$dll,$true)
    if ((Get-FastHash $dll) -cne $manifest.dll_sha256) { throw 'DLL hash mismatch' }
    if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString() -cne '1.0.1.0') { throw 'Wrong compiled assembly version' }
} finally { $zip.Dispose() }

# A launcher-only publication must keep this independent module, not restore 1.0.0.
$index = [IO.File]::ReadAllText((Join-Path $root 'docs/index.html'))
$client = Get-Content (Join-Path $root 'docs/client-release.json') -Raw | ConvertFrom-Json
$old = Get-Content (Join-Path $root 'docs/smelter-release.json') -Raw | ConvertFrom-Json
$new = $manifest.release
$fixture = [pscustomobject]@{client=$client;smelter=$old}
$probe = Build-GhostSmelterPatch $index $client $new @($new,$old) $fixture
$kept = ConvertFrom-Json -InputObject $probe['smelter-release.json']
if ($kept.version -cne '1.0.1' -or $kept.sha256 -cne $new.sha256) { throw 'Launcher publisher would roll back the independent module' }
foreach ($asset in $manifest.assets) {
    Publish-FastAsset $repo (New-FastAsset $repo 'smelter-1.0.1' (Join-Path $assets $asset.name) ('/' + $asset.name))
}

git -C $root pull --ff-only origin main
if ($LASTEXITCODE) { throw 'Could not refresh website baseline' }
python (Join-Path $PSScriptRoot 'update-smelter-site.py')
if ($LASTEXITCODE) { throw 'Website patch validation failed' }
git -C $root config user.name 'github-actions[bot]'
git -C $root config user.email '41898282+github-actions[bot]@users.noreply.github.com'
git -C $root add -- docs/smelter-release.json docs/smelter-releases.json docs/index.html docs/install.html docs/modules.js docs/update-history.json
git -C $root diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    git -C $root commit -m 'Publish Ghost Smelter Control 1.0.1 with windmill F1 time and capacity settings'
    if ($LASTEXITCODE) { throw 'Could not commit module metadata' }
    git -C $root push origin HEAD:main
    if ($LASTEXITCODE) { throw 'Concurrent update: stopped without force-pushing' }
}
$null = Invoke-FastGh @('api','--method','POST',('repos/' + $repo + '/pages/builds'))
for ($i=0;$i -lt 24;$i++) {
    try {
        $live = Invoke-RestMethod ('https://sukgicalm-lang.github.io/ghost-valheim-vault/smelter-release.json?verify=' + [guid]::NewGuid().ToString('N')) -TimeoutSec 20
        if ($live.version -ceq '1.0.1' -and $live.sha256 -ceq $new.sha256 -and $live.bytes -eq $new.bytes) { Write-Host 'Verified live Ghost Smelter Control 1.0.1; existing launcher unchanged.'; exit 0 }
    } catch { }
    Start-Sleep -Seconds 10
}
throw 'Module files were published but public metadata is not verified yet'
