$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root '_smelter_build/launcher/src-electron/GhostSelfUpdateHelper.ts'
$raw = [IO.File]::ReadAllText($source)
$match = [regex]::Match($raw, '(?s)^export const GHOST_SELF_UPDATE_HELPER = String\.raw`(.*)`;\s*$')
if (-not $match.Success) { throw 'Could not locate the exact shipped helper' }
$temporary = Join-Path $env:RUNNER_TEMP ('ghost-helper-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
$helper = Join-Path $temporary 'helper.ps1'
[IO.File]::WriteAllText($helper, $match.Groups[1].Value, [Text.UTF8Encoding]::new($true))
. $helper -FunctionsOnly
function Assert-Ghost([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
function Expect-GhostFailure([scriptblock]$Run,[string]$Message) { $failed=$false;try { & $Run | Out-Null } catch {$failed=$true};Assert-Ghost $failed $Message }
try {
    $payload = Join-Path $temporary 'payload'
    $base = Join-Path $temporary 'apps'
    New-Item -ItemType Directory -Path (Join-Path $payload 'resources') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $payload 'GhostR2.exe'),'runtime fixture')
    [IO.File]::WriteAllText((Join-Path $payload 'resources/app.asar'),'asar fixture')
    $zip = Join-Path $temporary 'runtime.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($payload,$zip)
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    $profile = Join-Path $temporary 'r2modmanPlus-local/profiles/Default/BepInEx/config'
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $profile 'personal.cfg'),'unchanged settings')
    $exe = Install-GhostRuntime $zip $hash '1.0.8' $base
    Assert-Ghost ([IO.File]::ReadAllText($exe) -eq 'runtime fixture') 'Runtime was not installed'
    Assert-Ghost ([IO.File]::ReadAllText((Join-Path $profile 'personal.cfg')) -eq 'unchanged settings') 'Profile data changed'
    Expect-GhostFailure { Install-GhostRuntime $zip ('0'*64) '1.0.8' $base } 'Wrong hash was accepted'
    Expect-GhostFailure { Install-GhostRuntime $zip $hash '../1.0.8' $base } 'Unsafe version was accepted'
    $badPayload = Join-Path $temporary 'incomplete'
    New-Item -ItemType Directory -Path $badPayload | Out-Null
    [IO.File]::WriteAllText((Join-Path $badPayload 'GhostR2.exe'),'incomplete')
    $badZip=Join-Path $temporary 'incomplete.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($badPayload,$badZip)
    $badHash=(Get-FileHash $badZip -Algorithm SHA256).Hash.ToLowerInvariant()
    Expect-GhostFailure { Install-GhostRuntime $badZip $badHash '1.0.8' $base } 'Incomplete runtime was accepted'
    Assert-Ghost ([IO.File]::ReadAllText($exe) -eq 'runtime fixture') 'Failed update damaged existing runtime'
    $unsafeZip=Join-Path $temporary 'unsafe.zip'
    $archive=[IO.Compression.ZipFile]::Open($unsafeZip,[IO.Compression.ZipArchiveMode]::Create)
    $entry=$archive.CreateEntry('../outside.txt');$writer=[IO.StreamWriter]::new($entry.Open());$writer.Write('unsafe');$writer.Dispose();$archive.Dispose()
    $unsafeHash=(Get-FileHash $unsafeZip -Algorithm SHA256).Hash.ToLowerInvariant()
    Expect-GhostFailure { Install-GhostRuntime $unsafeZip $unsafeHash '1.0.9' $base } 'Unsafe ZIP entry was accepted'
    Assert-Ghost (-not (Test-Path (Join-Path $temporary 'outside.txt'))) 'ZIP escaped its destination'
    $again = Install-GhostRuntime $zip $hash '1.0.8' $base
    Assert-Ghost ([IO.File]::ReadAllText($again) -eq 'runtime fixture') 'Atomic runtime replacement failed'
    Assert-Ghost (@(Get-ChildItem $base -Directory -Filter '1.0.8.previous-*').Count -eq 1) 'Rollback copy was not retained'
    Write-Host 'PASS: shipped Windows updater helper, exact payload, existing settings, bad SHA, unsafe version, incomplete payload, traversal rejection and rollback copy.'
} finally {Remove-Item -LiteralPath $temporary -Recurse -Force -ErrorAction SilentlyContinue}
