$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot '../_smelter_build/assets/GhostR2-Setup-1.0.11.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:Microsoft.CSharp.dll "/out:$output" (Join-Path $PSScriptRoot 'NativeGhostSetup.cs')
if ($LASTEXITCODE) { throw 'Could not build native installer' }
$test = Start-Process -FilePath $output -ArgumentList '--self-test' -Wait -PassThru
if ($test.ExitCode -ne 0) {
    $log = Join-Path ([IO.Path]::GetTempPath()) 'GhostSetup-self-test-error.txt'
    if (Test-Path $log) { Get-Content $log }
    throw 'Native installer validation failed'
}
Write-Host 'Native installer: install, replacement, config retention, SHA and unsafe archive rejection passed.'
