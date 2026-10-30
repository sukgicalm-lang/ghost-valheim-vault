$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = 'sukgicalm-lang/ghost-valheim-vault'
if ($env:GITHUB_REPOSITORY -cne $repo) { throw 'Unexpected repository' }
$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root '_smelter_build/assets'
$bundle = Get-Content (Join-Path $assets 'Release-Bundle.json') -Raw -Encoding utf8 | ConvertFrom-Json
$script:FastTemp = Join-Path $env:RUNNER_TEMP ('ghost-release-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($script:FastTemp) | Out-Null
$script:GhostGh = (Get-Command gh).Source
. (Join-Path $PSScriptRoot 'GhostFastCommon.ps1')
. (Join-Path $PSScriptRoot 'Build-WebsitePatch.ps1')
foreach ($asset in $bundle.assets) {
    $path = Join-Path $assets $asset.name
    if ((Get-Item $path).Length -ne $asset.bytes -or (Get-FastHash $path) -ne $asset.sha256) { throw 'Asset hash mismatch' }
    Publish-FastAsset $repo (New-FastAsset $repo $asset.tag $path ('/' + $asset.name))
}

# Refresh the public website baseline immediately before changing exactly four files.
git -C $root pull --ff-only origin main
if ($LASTEXITCODE) { throw 'Could not refresh website baseline' }
$docs = Join-Path $root 'docs'
$index = [IO.File]::ReadAllText((Join-Path $docs 'index.html'))
$client = Get-Content (Join-Path $docs 'client-release.json') -Raw -Encoding utf8 | ConvertFrom-Json
$previous = $null; $catalog = @()
if (Test-Path (Join-Path $docs 'smelter-release.json')) { $previous = Get-Content (Join-Path $docs 'smelter-release.json') -Raw | ConvertFrom-Json }
if (Test-Path (Join-Path $docs 'smelter-releases.json')) { $catalog = Get-Content (Join-Path $docs 'smelter-releases.json') -Raw | ConvertFrom-Json }
$patch = Build-GhostSmelterPatch $index $client $previous $catalog $bundle
$protected = @('release.json','releases.json','modules.js','update-history.json')
$hashes = @{}
foreach ($name in $protected) { $hashes[$name] = Get-FastHash (Join-Path $docs $name) }
foreach ($name in $patch.Keys) { [IO.File]::WriteAllText((Join-Path $docs $name), $patch[$name], [Text.UTF8Encoding]::new($false)) }
foreach ($name in $protected) { if ((Get-FastHash (Join-Path $docs $name)) -ne $hashes[$name]) { throw 'Protected main-suite metadata changed' } }
git -C $root config user.name 'github-actions[bot]'
git -C $root config user.email '41898282+github-actions[bot]@users.noreply.github.com'
git -C $root add -- docs/index.html docs/client-release.json docs/smelter-release.json docs/smelter-releases.json
git -C $root diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    git -C $root commit -m 'Publish standalone Ghost smelter and launcher 1.0.7'
    if ($LASTEXITCODE) { throw 'Could not commit website release metadata' }
    git -C $root push origin HEAD:main
    if ($LASTEXITCODE) { throw 'Concurrent website update; stopped without force-pushing' }
}
$null = Invoke-FastGh @('api', '--method', 'POST', ('repos/' + $repo + '/pages/builds'))
$verified = $false
for ($i = 0; $i -lt 24; $i++) {
    try {
        $feed = Invoke-RestMethod ('https://sukgicalm-lang.github.io/ghost-valheim-vault/smelter-release.json?verify=' + [guid]::NewGuid().ToString('N')) -TimeoutSec 20
        $liveClient = Invoke-RestMethod ('https://sukgicalm-lang.github.io/ghost-valheim-vault/client-release.json?verify=' + [guid]::NewGuid().ToString('N')) -TimeoutSec 20
        if ($feed.sha256 -eq $bundle.smelter.sha256 -and $feed.version -eq '1.0.0' -and $liveClient.sha256 -eq $bundle.client.sha256 -and $liveClient.version -eq '1.0.7') { $verified = $true; break }
    } catch { }
    Start-Sleep -Seconds 10
}
if (-not $verified) { throw 'Release assets are uploaded, but public Pages metadata is not yet verified' }
Write-Host 'Public Ghost smelter 1.0.0 / launcher 1.0.7 release verified.'

