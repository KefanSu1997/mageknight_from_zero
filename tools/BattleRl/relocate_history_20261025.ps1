$ErrorActionPreference = 'Stop'
$taskWorkspace = 'D:\study_and_work\unity-MK-test\MageKnight_from_zero'
$taskArchive = 'H:\MageKnightArchives\20261025_history_additional'
if (Test-Path -LiteralPath $taskArchive) { throw 'Archive exists: inspect before resuming' }
New-Item -ItemType Directory -Path $taskArchive | Out-Null
$taskSummary = @()
foreach ($taskName in @('StructuredUiPpo', 'UiFluency')) {
    $taskSource = [IO.Path]::GetFullPath((Join-Path $taskWorkspace "AutomationOutputs\$taskName"))
    $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskArchive $taskName))
    $taskBackup = "$taskSource.relocation-verified-original-20261025"
    if ($taskSource -notin @("$taskWorkspace\AutomationOutputs\StructuredUiPpo", "$taskWorkspace\AutomationOutputs\UiFluency")) { throw 'Unexpected source' }
    if (!(Test-Path -LiteralPath $taskSource) -or (Test-Path -LiteralPath $taskBackup)) { throw 'Unexpected source state' }
    if ((Get-Item -LiteralPath $taskSource).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Source is a link' }
    $taskNodes = @(Get-ChildItem -LiteralPath $taskSource -Recurse -Force)
    if (@($taskNodes | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Nested links require review' }
    $taskFiles = @($taskNodes | Where-Object { !$_.PSIsContainer })
    $taskBytes = ($taskFiles | Measure-Object Length -Sum).Sum
    if ((Get-PSDrive H).Free -lt ($taskBytes + 2GB)) { throw 'Insufficient destination space' }
    $taskEntries = [Collections.Generic.List[object]]::new()
    Write-Output "$taskName : hashing $($taskFiles.Count) files"
    foreach ($taskFile in $taskFiles) {
        $taskEntries.Add([pscustomobject]@{relative=$taskFile.FullName.Substring($taskSource.Length+1); bytes=$taskFile.Length; sha256=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash})
    }
    $taskRecord = [ordered]@{source=$taskSource; destination=$taskTarget; files=$taskFiles.Count; bytes=$taskBytes; status='hashed'; entries=$taskEntries}
    $taskManifest = Join-Path $taskArchive "$taskName.manifest.json"
    $taskRecord | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskManifest -Encoding utf8
    Write-Output "$taskName : copying $([math]::Round($taskBytes/1GB,2)) GiB"
    & robocopy $taskSource $taskTarget /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:8 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copy failed: $LASTEXITCODE" }
    if (@(Get-ChildItem -LiteralPath $taskSource -File -Recurse -Force).Count -ne $taskFiles.Count -or @(Get-ChildItem -LiteralPath $taskTarget -File -Recurse -Force).Count -ne $taskFiles.Count) { throw 'File count changed' }
    Write-Output "$taskName : verifying every source and destination SHA256"
    foreach ($taskEntry in $taskEntries) {
        foreach ($taskBase in @($taskSource,$taskTarget)) {
            $taskFilePath = Join-Path $taskBase $taskEntry.relative
            if ((Get-Item -LiteralPath $taskFilePath).Length -ne $taskEntry.bytes -or (Get-FileHash -LiteralPath $taskFilePath -Algorithm SHA256).Hash -ne $taskEntry.sha256) { throw "File differs: $taskFilePath" }
        }
    }
    $taskRecord.status='verified_before_switch'
    $taskRecord | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskManifest -Encoding utf8
    Move-Item -LiteralPath $taskSource -Destination $taskBackup
    try { New-Item -ItemType Junction -Path $taskSource -Target $taskTarget | Out-Null }
    catch { Move-Item -LiteralPath $taskBackup -Destination $taskSource; throw }
    $taskLink = Get-Item -LiteralPath $taskSource
    if ($taskLink.LinkType -ne 'Junction' -or $taskLink.Target -ne $taskTarget) { throw 'Junction mismatch; original retained' }
    foreach ($taskEntry in $taskEntries) { if (!(Test-Path -LiteralPath (Join-Path $taskSource $taskEntry.relative) -PathType Leaf)) { throw 'Original path inaccessible; original retained' } }
    # Exact absolute cleanup target is verified and is inside the intended workspace.
    if ([IO.Path]::GetFullPath($taskBackup) -ne "$taskWorkspace\AutomationOutputs\$taskName.relocation-verified-original-20261025" -or !(Test-Path -LiteralPath $taskManifest)) { throw 'Unsafe cleanup path' }
    Remove-Item -LiteralPath $taskBackup -Recurse -Force
    $taskRecord.status='complete_verified_junction'
    $taskRecord.completedUtc=[DateTime]::UtcNow.ToString('o')
    $taskRecord | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskManifest -Encoding utf8
    $taskSummary += [pscustomobject]@{source=$taskSource; destination=$taskTarget; files=$taskFiles.Count; bytes=$taskBytes; status=$taskRecord.status; manifest=$taskManifest}
    $taskSummary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskWorkspace 'AutomationOutputs\disk_relocation_20261025_additional.json') -Encoding utf8
    Write-Output "$taskName : complete; D free $([math]::Round((Get-PSDrive D).Free/1GB,2)) GiB"
}

