function Build-GhostSmelterPatch([string]$IndexHtml, $PriorClient, $PriorRelease, $PriorCatalog, $Bundle) {
    $release = $Bundle.smelter
    $client = $Bundle.client
    if ($PriorClient -and [version]$PriorClient.version -gt [version]$client.version) {
        throw '더 높은 런처가 이미 게시돼 있습니다. 이 파일로 내리지 않았습니다.'
    }
    if ($PriorClient -and $PriorClient.version -eq $client.version -and $PriorClient.sha256 -ne $client.sha256) {
        throw '같은 런처 버전의 다른 바이트가 이미 있습니다.'
    }
    $history = @($PriorCatalog | Where-Object { $null -ne $_ })
    if ($PriorRelease) { $history += $PriorRelease }
    foreach ($old in $history) {
        if ([version]$old.version -gt [version]$release.version) { throw '더 높은 제련 모드가 이미 게시돼 있습니다.' }
        if ($old.version -eq $release.version -and ($old.sha256 -ne $release.sha256 -or [long]$old.bytes -ne [long]$release.bytes)) {
            throw '같은 제련 모드 버전의 다른 파일이 이미 있습니다.'
        }
    }
    $catalog = @($release) + @($history | Where-Object { $_.version -ne $release.version } | Sort-Object { [version]$_.version } -Descending)
    $seen = @{}; $catalog = @($catalog | Where-Object { if ($seen.ContainsKey($_.version)) { $false } else { $seen[$_.version] = $true; $true } })
    $install = $client.installerUrl
    if ($install -cne ('https://github.com/sukgicalm-lang/ghost-valheim-vault/releases/download/client-' + $client.version + '/GhostR2-Setup-' + $client.version + '.exe')) { throw 'Invalid native installer URL' }
    $html = $IndexHtml
    # Change only launcher download links and the explicitly delimited addon section.
    $html = [regex]::Replace($html, 'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/client-\d+\.\d+\.\d+/(?:GhostR2-Install-\d+\.\d+\.\d+\.zip|GhostR2-Setup-\d+\.\d+\.\d+\.exe)', $install)
    $source = 'https://github.com/sukgicalm-lang/ghost-valheim-vault/releases/download/client-' + $client.version + '/GhostR2-Source-' + $client.version + '.zip'
    $html = [regex]::Replace($html, 'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/client-\d+\.\d+\.\d+/GhostR2-Source-\d+\.\d+\.\d+\.zip', $source)
    $html = [regex]::Replace($html, 'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/smelter-\d+\.\d+\.\d+/Ghost-GhostSmelterControl-\d+\.\d+\.\d+\.zip', [Text.RegularExpressions.MatchEvaluator]{ param($m) $release.downloadUrl })
    $section = @"
<!-- GHOST-SMELTER-BEGIN -->
<article id="ghost-smelter" class="experience-card">
<span class="experience-label">02 / 제련 · 생산</span><svg class="experience-icon" aria-hidden="true"><use href="#growth"/></svg>
<h3>기다림은 짧게.<br>모험은 더 길게.</h3>
<p>제련소·용광로·숯가마의 기본 생산 시간 1초.<br>원료·연료 한도는 100개, 세부 값은 F1에서.</p>
<button class="text-link" type="button" data-module="GhostSmelterControl">제련 기능 자세히 <span aria-hidden="true">↗</span></button>
<span class="experience-meta">기본 구성 포함 · Ghost 제련 제어 v$($release.version)</span>
</article>
<!-- GHOST-SMELTER-END -->
"@
    if ($html.Contains('<!-- GHOST-SMELTER-BEGIN -->')) {
        if ([regex]::Matches($html, '<!-- GHOST-SMELTER-BEGIN -->').Count -ne 1 -or [regex]::Matches($html, '<!-- GHOST-SMELTER-END -->').Count -ne 1) { throw '기존 제련 안내 영역을 안전하게 찾지 못했습니다.' }
        $html = [regex]::Replace($html, '(?s)<!-- GHOST-SMELTER-BEGIN -->.*?<!-- GHOST-SMELTER-END -->', [Text.RegularExpressions.MatchEvaluator]{ param($m) $section })
    } else {
        throw '제련 기능 카드의 표시 영역이 없습니다. 홈페이지 구성을 확인하세요.'
    }
    return [ordered]@{
        'index.html' = $html
        'client-release.json' = (ConvertTo-Json -InputObject $client -Depth 20)
        'smelter-release.json' = (ConvertTo-Json -InputObject $release -Depth 20)
        'smelter-releases.json' = (ConvertTo-Json -InputObject @($catalog) -Depth 20)
    }
}

