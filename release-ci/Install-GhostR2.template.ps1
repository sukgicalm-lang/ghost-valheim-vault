[CmdletBinding()]
param([switch]$NoLaunch, [switch]$NoPause)
$ErrorActionPreference = 'Stop'
$resultCode = 0
$sourceUrl = 'https://github.com/sukgicalm-lang/ghost-valheim-vault/releases/download/client-1.0.10/GhostR2-Windows-1.0.10.zip'
$expectedHash = '__RUNTIME_SHA256__'
$payloadName = 'GhostR2-Windows-1.0.10.zip'
$temporary = $null
try {
    if ($env:OS -ne 'Windows_NT') { throw '이 설치 도구는 Windows 10/11용입니다.' }
    if (Get-Process -Name 'r2modman','r2modmanPlus','GhostR2' -ErrorAction SilentlyContinue) { throw 'r2modman과 Ghost r2modman을 모두 종료한 뒤 설치하세요.' }
    $installBase = Join-Path $env:LOCALAPPDATA 'GhostR2'
    $installPath = Join-Path $installBase '1.0.10'
    $temporary = Join-Path ([IO.Path]::GetTempPath()) ('GhostR2-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temporary -Force | Out-Null
    $localZip = Join-Path $PSScriptRoot $payloadName
    $zipPath = $localZip
    Write-Host "`nGhost r2modman 1.0.10 설치`n" -ForegroundColor Green
    if (-not (Test-Path -LiteralPath $localZip -PathType Leaf)) {
        $zipPath = Join-Path $temporary $payloadName
        Write-Host 'Ghost 사이트에서 전용 앱을 다운로드합니다. 공개 배포 서버에서 내려받습니다.'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri $sourceUrl -OutFile $zipPath -TimeoutSec 900
    }
    if ((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) { throw '앱 파일의 SHA-256이 일치하지 않습니다. 설치하지 않았습니다.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\','/')
            if ($name -match '(^/|^[A-Za-z]:|(^|/)\.\.(/|$))') { throw '앱 ZIP에 잘못된 경로가 있습니다.' }
        }
    } finally { $archive.Dispose() }
    $staging = Join-Path $temporary 'application'
    [IO.Compression.ZipFile]::ExtractToDirectory($zipPath,$staging)
    if (-not (Test-Path -LiteralPath (Join-Path $staging 'GhostR2.exe'))) { throw '앱 실행 파일이 없습니다.' }
    if (-not (Test-Path -LiteralPath (Join-Path $staging 'resources\app.asar'))) { throw '앱 리소스 파일이 없습니다.' }
    New-Item -ItemType Directory -Path $installBase -Force | Out-Null
    $priorPath = $null
    if (Test-Path -LiteralPath $installPath) {
        $priorPath = $installPath + '.previous-' + (Get-Date).ToString('yyyyMMddHHmmssfff')
        Move-Item -LiteralPath $installPath -Destination $priorPath
    }
    try { Move-Item -LiteralPath $staging -Destination $installPath }
    catch {
        if ($priorPath -and -not (Test-Path -LiteralPath $installPath)) { Move-Item -LiteralPath $priorPath -Destination $installPath }
        throw
    }
    $exe = Join-Path $installPath 'GhostR2.exe'
    $protocol = 'HKCU:\Software\Classes\ghostvault'
    New-Item -Path ($protocol + '\shell\open\command') -Force | Out-Null
    Set-Item -Path $protocol -Value 'URL:Ghost Vault install link'
    New-ItemProperty -Path $protocol -Name 'URL Protocol' -PropertyType String -Value '' -Force | Out-Null
    Set-Item -Path ($protocol + '\shell\open\command') -Value ('"' + $exe + '" "%1"')
    $shell = New-Object -ComObject WScript.Shell
    $desktop = [Environment]::GetFolderPath('Desktop')
    $shortcut = $shell.CreateShortcut((Join-Path $desktop 'Ghost r2modman.lnk'))
    $shortcut.TargetPath = $exe; $shortcut.WorkingDirectory = $installPath; $shortcut.Save()
    Write-Host '설치 완료. 바탕화면에 Ghost r2modman 바로가기를 만들었습니다.' -ForegroundColor Green
    Write-Host 'Valheim과 사용할 프로필을 선택한 뒤 홈페이지에서 전체 모드를 설치하세요. 이후 런처 새 버전은 자동으로 적용됩니다.'
    Write-Host '일반 r2modman의 설치와 ror2mm 연결은 변경하지 않았습니다.'
    if (-not $NoLaunch) { Start-Process -FilePath $exe -WorkingDirectory $installPath }
}
catch { Write-Host "`n[설치 중단] $($_.Exception.Message)" -ForegroundColor Yellow; $resultCode = 1 }
finally {
    if ($temporary -and (Test-Path -LiteralPath $temporary)) { Remove-Item -LiteralPath $temporary -Recurse -Force -ErrorAction SilentlyContinue }
}
if (-not $NoPause) { $null = Read-Host '엔터를 누르면 닫습니다' }
exit $resultCode

