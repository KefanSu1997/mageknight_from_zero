$ErrorActionPreference = 'Stop'
$taskWorkspace = 'D:\study_and_work\unity-MK-test\MageKnight_from_zero'
$taskSource = [IO.Path]::GetFullPath((Join-Path $taskWorkspace 'AutomationOutputs\UiFluency'))
$taskTarget = [IO.Path]::GetFullPath('H:\MageKnight_RL_Backups\UiFluency_live_20261013')
$taskOriginal = [IO.Path]::GetFullPath((Join-Path $taskWorkspace 'AutomationOutputs\UiFluency.relocation-original-20261013'))
$taskManifest = 'H:\MageKnight_RL_Backups\UiFluency_live_20261013.migration.json'
if (!$taskSource.StartsWith($taskWorkspace+'\',[StringComparison]::OrdinalIgnoreCase) -or
    !$taskOriginal.StartsWith($taskWorkspace+'\',[StringComparison]::OrdinalIgnoreCase) -or
    $taskTarget -ne 'H:\MageKnight_RL_Backups\UiFluency_live_20261013') { throw 'Unexpected migration paths' }
if ((Test-Path -LiteralPath $taskTarget) -or (Test-Path -LiteralPath $taskOriginal)) { throw 'Migration destination already exists; inspect before recovery' }
if ((Get-Item -LiteralPath $taskSource).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Source is already a link' }
$taskNodes = @(Get-ChildItem -LiteralPath $taskSource -Recurse -Force)
if (@($taskNodes | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Nested links require separate review' }
$taskWorkers = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'python' -and $_.CommandLine -match 'measure_sampled_ui_pairs|verify_ui_cost_incompatibilities|ingest_ui_cost_results|write_ui_cost_progress' })
if ($taskWorkers.Count) { throw 'UI cost worker still active' }
$taskFiles = @($taskNodes | Where-Object { !$_.PSIsContainer })
$taskBytes = ($taskFiles | Measure-Object -Property Length -Sum).Sum
if ((Get-PSDrive H).Free -lt ($taskBytes + 2GB)) { throw 'Destination lacks reserve space' }
$taskRecords = [Collections.Generic.List[object]]::new()
function Get-TaskSha256([string]$taskPath) {
    $taskStream=[IO.File]::OpenRead($taskPath)
    try { return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskStream)) }
    finally { $taskStream.Dispose() }
}
$taskIndex = 0
foreach ($taskFile in $taskFiles) {
    $taskRecords.Add([pscustomobject]@{ relative=$taskFile.FullName.Substring($taskSource.Length+1); bytes=$taskFile.Length; sha256=(Get-TaskSha256 $taskFile.FullName) })
    $taskIndex++
    if ($taskIndex % 1000 -eq 0) { Write-Output "Source hash $taskIndex / $($taskFiles.Count)" }
}
$taskRecord = [ordered]@{ source=$taskSource; target=$taskTarget; original=$taskOriginal; files=$taskFiles.Count; bytes=$taskBytes; status='source_hashed'; entries=$taskRecords }
$taskRecord | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskManifest -Encoding utf8
Write-Output "Copying $($taskFiles.Count) files, $([math]::Round($taskBytes/1GB,2)) GiB"
Copy-Item -LiteralPath $taskSource -Destination $taskTarget -Recurse -Force
if (@(Get-ChildItem -LiteralPath $taskTarget -File -Recurse -Force).Count -ne $taskFiles.Count) { throw 'Copied file count differs' }
if (@(Get-ChildItem -LiteralPath $taskSource -File -Recurse -Force).Count -ne $taskFiles.Count) { throw 'Source changed during copy' }
$taskIndex = 0
foreach ($taskEntry in $taskRecords) {
    $taskSourceFile = Join-Path $taskSource $taskEntry.relative
    $taskTargetFile = Join-Path $taskTarget $taskEntry.relative
    if ((Get-Item -LiteralPath $taskTargetFile).Length -ne $taskEntry.bytes -or
        (Get-TaskSha256 $taskTargetFile) -ne $taskEntry.sha256 -or
        (Get-TaskSha256 $taskSourceFile) -ne $taskEntry.sha256) { throw "Hash mismatch: $($taskEntry.relative)" }
    $taskIndex++
    if ($taskIndex % 1000 -eq 0) { Write-Output "Verified $taskIndex / $($taskFiles.Count)" }
}
$taskRecord.status='all_files_verified'
$taskRecord | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskManifest -Encoding utf8
Move-Item -LiteralPath $taskSource -Destination $taskOriginal
try { New-Item -ItemType Junction -Path $taskSource -Target $taskTarget | Out-Null }
catch { Move-Item -LiteralPath $taskOriginal -Destination $taskSource; throw }
$taskLink = Get-Item -LiteralPath $taskSource
if ($taskLink.LinkType -ne 'Junction' -or !(Test-Path -LiteralPath (Join-Path $taskSource 'full_cost_progress\report.html'))) { throw 'Link validation failed; original copy retained' }
# Exact original-copy path was checked within the workspace above. All file
# bytes remain in the independently verified destination, reachable at old paths.
if ([IO.Path]::GetFullPath($taskOriginal) -ne (Join-Path $taskWorkspace 'AutomationOutputs\UiFluency.relocation-original-20261013')) { throw 'Original cleanup path changed' }
Remove-Item -LiteralPath $taskOriginal -Recurse -Force
$taskRecord.status='complete_verified_junction'
$taskRecord.completedUtc=[DateTime]::UtcNow.ToString('o')
$taskRecord | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskManifest -Encoding utf8
Write-Output "Verified migration complete; D free $([math]::Round((Get-PSDrive D).Free/1GB,2)) GiB"
