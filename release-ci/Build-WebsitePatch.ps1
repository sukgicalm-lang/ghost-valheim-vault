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
    $html = [regex]::Replace($html, 'https://github\.com/sukgicalm-lang/ghost-valheim-vault/releases/download/smelter-\d+\.\d+\.\d+/Ghost-GhostSmelterControl-\d+\.\d+\.\d+\.zip', [Text.RegularExpressions.MatchEvaluator]{ param($m) $release.downloadUrl })
    $section = @"
<!-- GHOST-SMELTER-BEGIN -->
<details id="ghost-smelter" class="setup-option">
<summary><span class="setup-badge">기본 포함</span><strong>제련 편의 모드</strong><span class="setup-option-hint">기능 · 설치 안내</span></summary>
<div class="setup-option-body">
<h3>Ghost 제련 제어 <small>v$($release.version)</small></h3>
<p>일반 제련소·용광로·숯가마의 기본 생산 시간을 1초, 원료 한도를 100개로 설정합니다. 제련소·용광로의 석탄 한도도 100개이며 F1에서 조절할 수 있습니다.</p>
<p>통합팩과 함께 사용하는 필수 모드이며, 위의 전체 모드 설치에 포함됩니다. 설치 전 기존 FrizzQOL Smelter Control을 제거하세요.</p>

<p class="setup-caption">런처가 새 버전을 자동으로 확인합니다. 업데이트는 Update All에서 적용하세요. 원료·연료의 소모 규칙과 생산물의 묶음 배출 방식은 유지합니다.</p>
</div>
</details>
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
