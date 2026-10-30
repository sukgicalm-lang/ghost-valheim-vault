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
    $install = 'https://github.com/sukgicalm-lang/ghost-valheim-vault/releases/download/client-' + $client.version + '/GhostR2-Install-' + $client.version + '.zip'
    $html = $IndexHtml
    # Change only launcher download links and the explicitly delimited addon section.
    $html = [regex]::Replace($html, 'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/client-\d+\.\d+\.\d+/GhostR2-Install-\d+\.\d+\.\d+\.zip', $install)
    $source = 'https://github.com/sukgicalm-lang/ghost-valheim-vault/releases/download/client-' + $client.version + '/GhostR2-Source-' + $client.version + '.zip'
    $html = [regex]::Replace($html, 'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/client-\d+\.\d+\.\d+/GhostR2-Source-\d+\.\d+\.\d+\.zip', $source)
    $section = @"
<!-- GHOST-SMELTER-BEGIN -->
<section id="ghost-smelter" class="wrap section-space" aria-labelledby="ghost-smelter-title">
<div class="section-heading"><div><div class="eyebrow">SECOND GHOST MOD</div><h2 id="ghost-smelter-title">Ghost 제련 제어</h2></div><p>기존 통합팩과 별도로 설치하는 작은 모드 · v$($release.version)</p></div>
<p>일반 제련소·용광로·숯가마의 기본 생산 시간 1초, 원료 한도 100개. 제련소·용광로의 석탄 한도도 100개이며 F1에서 조절할 수 있습니다.</p>
<p>기존 FrizzQOL Smelter Control을 제거하고, 본인과 친구 모두 Ghost r2 $($client.version) 이상으로 한 번 교체하세요. 이후 런처가 새 제련 버전을 자동 조회하고 Update All에서 적용할 수 있습니다. 기존 고스트 통합팩은 다시 받지 않습니다.</p>
<div class="guide-actions"><a class="button gold" href="ghostvault://v1/install/Ghost-GhostSmelterControl/latest">제련 모드 설치 ↗</a><a class="text-link" href="$install">Ghost r2 $($client.version) 받기 ↗</a><a class="text-link" href="$($release.downloadUrl)">제련 ZIP 직접 받기 ↗</a></div>
<p>실제 게임 실행 결과는 적용 후 확인이 필요합니다. 원료·연료 조건과 게임의 묶음 배출 동작은 유지합니다.</p>
</section>
<!-- GHOST-SMELTER-END -->
"@
    if ($html.Contains('<!-- GHOST-SMELTER-BEGIN -->')) {
        if ([regex]::Matches($html, '<!-- GHOST-SMELTER-BEGIN -->').Count -ne 1 -or [regex]::Matches($html, '<!-- GHOST-SMELTER-END -->').Count -ne 1) { throw '기존 제련 안내 영역을 안전하게 찾지 못했습니다.' }
        $html = [regex]::Replace($html, '(?s)<!-- GHOST-SMELTER-BEGIN -->.*?<!-- GHOST-SMELTER-END -->', [Text.RegularExpressions.MatchEvaluator]{ param($m) $section })
    } else {
        $anchor = '<section id="collection"'
        if ([regex]::Matches($html, [regex]::Escape($anchor)).Count -ne 1) { throw '기존 모드 목록의 삽입 지점을 찾지 못했습니다.' }
        $html = $html.Replace($anchor, $section + "`n" + $anchor)
    }
    return [ordered]@{
        'index.html' = $html
        'client-release.json' = (ConvertTo-Json -InputObject $client -Depth 20)
        'smelter-release.json' = (ConvertTo-Json -InputObject $release -Depth 20)
        'smelter-releases.json' = (ConvertTo-Json -InputObject @($catalog) -Depth 20)
    }
}
