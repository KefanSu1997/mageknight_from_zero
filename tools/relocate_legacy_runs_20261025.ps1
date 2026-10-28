$ErrorActionPreference = 'Stop'
$taskRoot = 'D:\study_and_work\unity-MK-test\MageKnight_from_zero\multi-agent-workspace\runs'
$taskArchive = 'H:\MageKnightArchives\20261025_legacy_agent_runs'
$taskNames = @('T-20251028-006','T-20251028-009','T-20251028-012')
New-Item -ItemType Directory -Path $taskArchive -Force | Out-Null
$taskResults = @()
foreach ($taskName in $taskNames) {
    $taskSource = [IO.Path]::GetFullPath((Join-Path $taskRoot $taskName))
    $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskArchive $taskName))
    $taskBackup = [IO.Path]::GetFullPath($taskSource + '.relocation-verified-original')
    if ($taskSource -ne ($taskRoot+'\'+$taskName) -or $taskTarget -ne ($taskArchive+'\'+$taskName) -or $taskBackup -ne ($taskRoot+'\'+$taskName+'.relocation-verified-original')) { throw 'Path outside fixed migration scope' }
    if ((Get-Item -LiteralPath $taskSource).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Already linked' }
    if ((Test-Path -LiteralPath $taskTarget) -or (Test-Path -LiteralPath $taskBackup)) { throw 'Destination exists; inspect before resuming' }
    $taskNodes = @(Get-ChildItem -LiteralPath $taskSource -Recurse -Force)
    if (@($taskNodes | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Nested link' }
    $taskFiles = @($taskNodes | Where-Object { !$_.PSIsContainer })
    $taskEntries = @($taskFiles | ForEach-Object { [pscustomobject]@{ relative=$_.FullName.Substring($taskSource.Length+1); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
    $taskBytes = ($taskFiles | Measure-Object Length -Sum).Sum
    if ((Get-PSDrive H).Free -lt ($taskBytes + 5GB)) { throw 'Insufficient H reserve' }
    Write-Output "Copying $taskName ($([math]::Round($taskBytes/1GB,2)) GiB)"
    Copy-Item -LiteralPath $taskSource -Destination $taskTarget -Recurse
    if (@(Get-ChildItem -LiteralPath $taskTarget -Recurse -File -Force).Count -ne $taskEntries.Count -or @(Get-ChildItem -LiteralPath $taskSource -Recurse -File -Force).Count -ne $taskEntries.Count) { throw 'File count changed' }
    foreach ($taskEntry in $taskEntries) {
        foreach ($taskFilePath in @((Join-Path $taskSource $taskEntry.relative),(Join-Path $taskTarget $taskEntry.relative))) {
            if ((Get-Item -LiteralPath $taskFilePath).Length -ne $taskEntry.bytes -or (Get-FileHash -LiteralPath $taskFilePath -Algorithm SHA256).Hash -ne $taskEntry.sha256) { throw "Hash mismatch $taskFilePath" }
        }
    }
    $taskRecord = [ordered]@{ source=$taskSource; destination=$taskTarget; bytes=$taskBytes; files=$taskEntries.Count; entries=$taskEntries; status='verified_both_copies' }
    $taskManifest = Join-Path $taskArchive ($taskName+'.migration.json')
    $taskRecord | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskManifest -Encoding utf8
    Move-Item -LiteralPath $taskSource -Destination $taskBackup
    try { New-Item -ItemType Junction -Path $taskSource -Target $taskTarget | Out-Null }
    catch { Move-Item -LiteralPath $taskBackup -Destination $taskSource; throw }
    if ((Get-Item -LiteralPath $taskSource).LinkType -ne 'Junction') { throw 'Junction failed; original retained' }
    foreach ($taskEntry in $taskEntries) {
        if (!(Test-Path -LiteralPath (Join-Path $taskSource $taskEntry.relative))) { throw 'Original path verification failed' }
    }
    # Exact absolute backup is inside the fixed history directory; verified H copy remains.
    if ([IO.Path]::GetFullPath($taskBackup) -ne ($taskRoot+'\'+$taskName+'.relocation-verified-original')) { throw 'Cleanup target mismatch' }
    Remove-Item -LiteralPath $taskBackup -Recurse -Force
    $taskRecord.status='complete_sha256_verified_junction'
    $taskRecord.completedUtc=[DateTime]::UtcNow.ToString('o')
    $taskRecord | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskManifest -Encoding utf8
    $taskResults += [pscustomobject]@{ name=$taskName; bytes=$taskBytes; files=$taskEntries.Count }
    Write-Output "Completed $taskName; D free $([math]::Round((Get-PSDrive D).Free/1GB,2)) GiB"
}
$taskResults | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskArchive 'summary.json') -Encoding utf8

