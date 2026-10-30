$ErrorActionPreference='Stop'
function Write-FastJson([string]$Path,$Value){
 [IO.Directory]::CreateDirectory((Split-Path $Path -Parent))|Out-Null
 $tmp=$Path+'.pending-'+[Guid]::NewGuid().ToString('N')
 [IO.File]::WriteAllText($tmp,(ConvertTo-Json -InputObject $Value -Depth 30),[Text.UTF8Encoding]::new($false))
 if([IO.File]::Exists($Path)){[IO.File]::Replace($tmp,$Path,$Path+'.previous-'+[Guid]::NewGuid().ToString('N'))}else{[IO.File]::Move($tmp,$Path)}
}
function Get-FastHash([string]$Path){return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Get-FastRoot([string]$Requested){
 $candidates=@($Requested,(Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),'C:\GhostVault-SelfHost-Windows-1.0.0\GhostVault')
 if(-not $Requested){
  $ts=Join-Path $env:ProgramFiles 'Tailscale\tailscale.exe'
  if(Test-Path -LiteralPath $ts){try{$status=(& $ts serve status --json 2>$null)-join "`n";$config=$status|ConvertFrom-Json;foreach($web in $config.Web.PSObject.Properties){if($web.Name -eq 'user.tail054318.ts.net:443'){foreach($handler in $web.Value.Handlers.PSObject.Properties){if($handler.Name -eq '/' -and $handler.Value.Path){$candidates+=([string]$handler.Value.Path)}}}}}catch{}}
 }
 foreach($candidate in $candidates){if(-not $candidate){continue};if((Split-Path $candidate -Leaf) -eq 'website'){$candidate=Split-Path $candidate -Parent};if((Test-Path -LiteralPath (Join-Path $candidate 'website/index.html')) -and (Test-Path -LiteralPath (Join-Path $candidate 'Publish-Pack.ps1'))){return (Resolve-Path -LiteralPath $candidate).Path}}
 if($Requested){throw '기존 GhostVault 운영 폴더가 아닙니다.'}
 Add-Type -AssemblyName System.Windows.Forms
 $picker=New-Object Windows.Forms.FolderBrowserDialog;$picker.Description='현재 운영 중인 GhostVault 폴더를 선택하세요.';$picker.ShowNewFolderButton=$false
 if($picker.ShowDialog() -ne 'OK'){throw '폴더 선택을 취소했습니다.'}
 return Get-FastRoot $picker.SelectedPath
}
function Get-FastGh([string]$Tools){
 $existing=Get-Command gh.exe -ErrorAction SilentlyContinue;if($existing){return $existing.Source}
 $cache=Join-Path $Tools 'gh';if(Test-Path -LiteralPath $cache){$found=@(Get-ChildItem -LiteralPath $cache -Filter gh.exe -Recurse -File);if($found.Count -eq 1){return $found[0].FullName}}
 Write-Host '운영자용 GitHub 게시 도구를 준비합니다. 방문자에게는 설치가 필요 없습니다.' -ForegroundColor Cyan
 [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
 $latest=Invoke-RestMethod -Uri 'https://api.github.com/repos/cli/cli/releases/latest' -Headers @{'User-Agent'='GhostVault-Publisher'}
 $asset=@($latest.assets|Where-Object {$_.name -match '^gh_[0-9.]+_windows_amd64\.zip$'})
 $checks=@($latest.assets|Where-Object {$_.name -match '^gh_[0-9.]+_checksums\.txt$'})
 if($asset.Count -ne 1 -or $checks.Count -ne 1){throw 'GitHub 공식 게시 도구 파일을 확인하지 못했습니다.'}
 foreach($a in @($asset[0],$checks[0])){if(([Uri]$a.browser_download_url).Host -ne 'github.com'){throw '게시 도구 출처가 올바르지 않습니다.'}}
 [IO.Directory]::CreateDirectory($Tools)|Out-Null;$zip=Join-Path $Tools 'gh-download.zip'
 Invoke-WebRequest -UseBasicParsing -Uri $asset[0].browser_download_url -OutFile $zip -TimeoutSec 900
 $checkFile=Join-Path $Tools 'gh-checksums.txt'
 Invoke-WebRequest -UseBasicParsing -Uri $checks[0].browser_download_url -OutFile $checkFile -TimeoutSec 90
 $list=[IO.File]::ReadAllText($checkFile,[Text.Encoding]::ASCII)
 $expected=$null;foreach($line in ($list -split "`n")){if($line -match '^([a-fA-F0-9]{64})\s+\*?(.+?)\s*$' -and $Matches[2] -eq $asset[0].name){$expected=$Matches[1].ToLowerInvariant()}}
 if(-not $expected -or (Get-FastHash $zip) -ne $expected){throw 'GitHub 게시 도구 SHA-256 검증에 실패했습니다.'}
 $staging=Join-Path $Tools ('gh-staging-'+[Guid]::NewGuid().ToString('N'))
 try{
  [IO.Compression.ZipFile]::ExtractToDirectory($zip,$staging)
  $found=@(Get-ChildItem -LiteralPath $staging -Filter gh.exe -Recurse -File)
  if($found.Count -ne 1){throw 'GitHub 게시 도구 실행 파일을 확인하지 못했습니다.'}
  if(Test-Path -LiteralPath $cache){[IO.Directory]::Move($cache,($cache+'.previous-'+[Guid]::NewGuid().ToString('N')))}
  [IO.Directory]::Move($staging,$cache);[IO.File]::Delete($zip)
 }finally{if(Test-Path -LiteralPath $staging){Remove-Item -LiteralPath $staging -Recurse -Force}}
 $found=@(Get-ChildItem -LiteralPath $cache -Filter gh.exe -Recurse -File);return $found[0].FullName
}
function Invoke-FastGh([string[]]$Arguments,[switch]$AllowFailure){
 $ErrorActionPreference='Continue';$lines=& $script:GhostGh @Arguments 2>&1;$code=$LASTEXITCODE
 if($code -ne 0){if($AllowFailure){return $null};throw ('GitHub 작업이 중단됐습니다: '+($lines -join "`n"))}
 return ($lines -join "`n")
}
function Invoke-FastApi([string]$Endpoint,[string]$Method='GET',$Body=$null,[switch]$AllowFailure){
 $ghArgs=@('api','--hostname','github.com','--method',$Method,$Endpoint,'-H','Accept: application/vnd.github+json')
 $temp=$null
 try{
  if($null -ne $Body){$temp=Join-Path $script:FastTemp ([Guid]::NewGuid().ToString('N')+'.json');[IO.File]::WriteAllText($temp,(ConvertTo-Json -InputObject $Body -Depth 40),[Text.UTF8Encoding]::new($false));$ghArgs+=@('--input',$temp)}
  $raw=Invoke-FastGh $ghArgs -AllowFailure:$AllowFailure
  if($raw){return (ConvertFrom-Json -InputObject $raw)};return $null
 }finally{if($temp -and [IO.File]::Exists($temp)){[IO.File]::Delete($temp)}}
}
function New-FastZip([string]$Directory,[string]$Output){
 $stream=[IO.File]::Create($Output);$zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
 try{foreach($file in (Get-ChildItem -LiteralPath $Directory -File -Recurse|Sort-Object FullName)){
  $name=$file.FullName.Substring($Directory.TrimEnd('\','/').Length+1).Replace('\','/')
  $entry=$zip.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal);$entry.LastWriteTime=[DateTimeOffset]::new(2026,10,23,0,0,0,[TimeSpan]::Zero)
  $src=[IO.File]::OpenRead($file.FullName);$dst=$entry.Open();try{$src.CopyTo($dst)}finally{$src.Dispose();$dst.Dispose()}
 }}finally{$zip.Dispose();$stream.Dispose()}
}
function Assert-FastSuite([string]$Path,$Release){
 if(-not(Test-Path -LiteralPath $Path -PathType Leaf) -or (Get-Item -LiteralPath $Path).Length -ne [long]$Release.bytes -or (Get-FastHash $Path) -ne $Release.sha256){throw ('통합팩 크기/SHA-256 불일치: '+$Path)}
 $zip=[IO.Compression.ZipFile]::OpenRead($Path)
 try{
  foreach($entry in $zip.Entries){if($entry.FullName -match '(?i)(^/|^[a-z]:|(^|[/\\])\.\.([/\\]|$)|\.cfg$)'){throw '배포 ZIP의 경로나 설정 파일을 확인하세요.'}}
  $entry=$zip.GetEntry('manifest.json');if(-not $entry){throw '통합팩 manifest.json이 없습니다.'}
  $reader=New-Object IO.StreamReader($entry.Open());try{$manifest=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
  if($manifest.version_number -ne $Release.version -or $manifest.name -ne 'GhostValheimSuite'){throw '통합팩 이름/버전이 배포 기록과 다릅니다.'}
 }finally{$zip.Dispose()}
}
function New-FastAsset([string]$Repository,[string]$Tag,[string]$Path,[string]$LegacyPath){
 $item=Get-Item -LiteralPath $Path;if($item.Length -ge 2147483648){throw '단일 배포 파일은 2GiB 미만이어야 합니다.'}
 return [pscustomobject]@{path=$item.FullName;name=$item.Name;tag=$Tag;legacy=$LegacyPath;bytes=$item.Length;sha256=(Get-FastHash $item.FullName);url=('https://github.com/'+$Repository+'/releases/download/'+$Tag+'/'+$item.Name)}
}
function Publish-FastAsset([string]$Repository,$Asset){
 $release=Invoke-FastApi ('repos/'+$Repository+'/releases/tags/'+$Asset.tag) -AllowFailure
 if(-not $release){$null=Invoke-FastGh @('release','create',$Asset.tag,'--repo',$Repository,'--title',$Asset.tag,'--notes','Ghost Vault public distribution.');$release=Invoke-FastApi ('repos/'+$Repository+'/releases/tags/'+$Asset.tag)}
 $found=@($release.assets|Where-Object {$_.name -ceq $Asset.name})
 if($found.Count -gt 1){throw '중복된 GitHub 배포 파일이 있습니다.'}
 if($found.Count -eq 0){
  Write-Host ('업로드: '+$Asset.name+' ('+([double]$Asset.bytes/1048576).ToString('F1')+' MiB)') -ForegroundColor Cyan
  $null=Invoke-FastGh @('release','upload',$Asset.tag,$Asset.path,'--repo',$Repository)
  $release=Invoke-FastApi ('repos/'+$Repository+'/releases/tags/'+$Asset.tag);$found=@($release.assets|Where-Object {$_.name -ceq $Asset.name})
 }
 if($found.Count -ne 1 -or [long]$found[0].size -ne [long]$Asset.bytes -or $found[0].state -ne 'uploaded'){throw ('업로드한 파일 크기를 확인하지 못했습니다: '+$Asset.name)}
 $digest=[string]$found[0].digest
 if($digest){if($digest -cne ('sha256:'+$Asset.sha256)){throw ('같은 버전의 다른 파일이 이미 있습니다. 덮어쓰지 않았습니다: '+$Asset.name)}}
 else{
  $check=Join-Path $script:FastTemp ('remote-'+[Guid]::NewGuid().ToString('N')+'.zip')
  try{Invoke-WebRequest -UseBasicParsing -Uri $Asset.url -OutFile $check -TimeoutSec 900;if((Get-FastHash $check) -ne $Asset.sha256){throw ('배포 파일 검증 실패: '+$Asset.name)}}finally{if(Test-Path -LiteralPath $check){[IO.File]::Delete($check)}}
 }
}
function New-FastWebsite([string]$Source,[string]$Destination,[string]$Repository,[string]$SiteUrl,$Releases,$Assets,$Client){
 [IO.Directory]::CreateDirectory($Destination)|Out-Null
 $required=@('index.html','app.js','modules.js','style.css','motion.css','motion.js','doom.css','onboarding-v1.css')
 foreach($name in $required){if(-not(Test-Path -LiteralPath (Join-Path $Source $name))){throw ('홈페이지 원본 파일이 없습니다: '+$name)};[IO.File]::Copy((Join-Path $Source $name),(Join-Path $Destination $name))}
 $sourceAssets=Join-Path $Source 'assets'
 foreach($p in (Get-ChildItem -LiteralPath $sourceAssets -File -Recurse)){
  $relative=$p.FullName.Substring($sourceAssets.Length+1).Replace('\','/')
  if($relative -notmatch '^[A-Za-z0-9_./-]+\.(png|webp|jpe?g|svg|ico|woff2?)$' -or $relative -match '(^|/)\.\.') {continue}
  $target=Join-Path (Join-Path $Destination 'assets') $relative;[IO.Directory]::CreateDirectory((Split-Path $target -Parent))|Out-Null;[IO.File]::Copy($p.FullName,$target)
 }
 $routes=[ordered]@{};foreach($asset in $Assets){$routes[$asset.legacy]=$asset.url}
 $catalog=@();foreach($r in $Releases){$catalog+=[pscustomobject][ordered]@{version=$r.version;date=$r.date;file=$r.file;bytes=[long]$r.bytes;sha256=$r.sha256;appIntegration=$true;dependencies=[string[]]$r.dependencies;notes=$r.notes;downloadUrl=$routes['/'+$r.file]}}
 Write-FastJson (Join-Path $Destination 'releases.json') $catalog;Write-FastJson (Join-Path $Destination 'release.json') $catalog[0]
 $runtime=@($Assets|Where-Object {$_.legacy -eq ('/'+$Client.file)})[0]
 Write-FastJson (Join-Path $Destination 'client-release.json') ([ordered]@{version=$Client.version;date=$Client.date;file=$Client.file;bytes=$runtime.bytes;sha256=$runtime.sha256;downloadUrl=$runtime.url})
 Write-FastJson (Join-Path $Destination 'hosting.json') ([ordered]@{schema=1;repository=$Repository;siteUrl=$SiteUrl;visitorLoginRequired=$false})
 $html=[IO.File]::ReadAllText((Join-Path $Destination 'index.html'),[Text.Encoding]::UTF8)
 foreach($asset in $Assets){$html=$html.Replace($asset.legacy.TrimStart('/'),$asset.url)}
 $html=[regex]::Replace($html,'installers/GhostR2-Install-\d+\.\d+\.\d+\.zip',([string]$routes['/installers/GhostR2-Install-'+$Client.version+'.zip']))
 $html=[regex]::Replace($html,'downloads/Ghost-GhostValheimSuite-\d+\.\d+\.\d+\.zip',[string]$catalog[0].downloadUrl)
 $html=$html.Replace('https://user.tail054318.ts.net/',$SiteUrl).Replace('https://user.tail054318.ts.net',$SiteUrl.TrimEnd('/'))
 $html=[regex]::Replace($html,'(?<=data-release-version>)[^<]+',('v'+$catalog[0].version))
 $html=[regex]::Replace($html,'(?<=class="doom-version">)[^<]+',('v'+$catalog[0].version))
 $html=[regex]::Replace($html,'(?<=class="doom-ready">)[^<]+','게임 내 외형 적용 확인')
 $html=$html.Replace('테스트 배포로, 실제 착용 핏과 게임 내 동작은 확인 중입니다.','둠 경갑 외형의 게임 내 표시를 확인했습니다.')
 $html=[regex]::Replace($html,'<time data-release-date datetime="[^"]*">[^<]*</time>',('<time data-release-date datetime="'+$catalog[0].date+'">'+$catalog[0].date.Replace('-','.')+'</time>'))
 $size=([double]$catalog[0].bytes/1048576).ToString('F2',[Globalization.CultureInfo]::InvariantCulture)+' MiB · ZIP'
 $html=[regex]::Replace($html,'(?<=data-release-size>)[^<]+',$size)
 $short=$catalog[0].sha256.Substring(0,8)+'…'+$catalog[0].sha256.Substring(57)
 $html=[regex]::Replace($html,'(?<=<code id="release-hash">)[^<]*',$short)
 $notes=[Net.WebUtility]::HtmlEncode([string]$catalog[0].notes)
 $html=[regex]::Replace($html,'<p id="release-notes">[^<]*</p>',('<p id="release-notes">'+$notes+'</p>'))
 $html=$html.Replace('</head>',('<link rel="canonical" href="'+$SiteUrl+'"></head>'))
 [IO.File]::WriteAllText((Join-Path $Destination 'index.html'),$html,[Text.UTF8Encoding]::new($false))
 $app=[IO.File]::ReadAllText((Join-Path $Destination 'app.js'),[Text.Encoding]::UTF8)
 $old="document.querySelectorAll('.manual-download').forEach(x=>x.href=r.file);"
 if(-not $app.Contains($old)){throw '홈페이지 다운로드 코드가 예상과 다릅니다. 원본을 변경하지 않았습니다.'}
 $new="document.querySelectorAll('.manual-download').forEach(x=>x.href='https://github.com/"+$Repository+"/releases/download/suite-'+r.version+'/'+r.file.slice('downloads/'.length));"
 $app=$app.Replace($old,$new)
 [IO.File]::WriteAllText((Join-Path $Destination 'app.js'),$app,[Text.UTF8Encoding]::new($false))
 [IO.File]::WriteAllText((Join-Path $Destination '.nojekyll'),'')
 return [pscustomobject]$routes
}
function Publish-FastWebsite([string]$Repository,[string]$Directory){
 $repo=Invoke-FastApi ('repos/'+$Repository);$branch=[string]$repo.default_branch
 $ref=Invoke-FastApi ('repos/'+$Repository+'/git/ref/heads/'+$branch);$commit=Invoke-FastApi ('repos/'+$Repository+'/git/commits/'+$ref.object.sha)
 $tree=@()
 foreach($file in (Get-ChildItem -LiteralPath $Directory -File -Recurse -Force|Sort-Object FullName)){
  $relative=$file.FullName.Substring($Directory.TrimEnd('\','/').Length+1).Replace('\','/')
  $blob=Invoke-FastApi ('repos/'+$Repository+'/git/blobs') 'POST' @{encoding='base64';content=[Convert]::ToBase64String([IO.File]::ReadAllBytes($file.FullName))}
  $tree+=@{path=('docs/'+$relative);mode='100644';type='blob';sha=$blob.sha}
 }
 $marker=[Text.Encoding]::UTF8.GetBytes('{"schema":1,"kind":"ghost-vault-public-v1","legacyOrigin":"https://user.tail054318.ts.net"}')
 $blob=Invoke-FastApi ('repos/'+$Repository+'/git/blobs') 'POST' @{encoding='base64';content=[Convert]::ToBase64String($marker)};$tree+=@{path='.ghost-vault.json';mode='100644';type='blob';sha=$blob.sha}
 $newTree=Invoke-FastApi ('repos/'+$Repository+'/git/trees') 'POST' @{base_tree=$commit.tree.sha;tree=$tree}
 if($newTree.sha -eq $commit.tree.sha){Write-Host '홈페이지 파일은 이미 최신입니다.';return}
 $newCommit=Invoke-FastApi ('repos/'+$Repository+'/git/commits') 'POST' @{message='Update Ghost Vault public distribution';tree=$newTree.sha;parents=@($ref.object.sha)}
 $null=Invoke-FastApi ('repos/'+$Repository+'/git/refs/heads/'+$branch) 'PATCH' @{sha=$newCommit.sha;force=$false}
}
