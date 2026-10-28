$ErrorActionPreference='Stop'
$taskWorkspace='D:\study_and_work\unity-MK-test\MageKnight_from_zero'
$taskSource="$taskWorkspace\AutomationOutputs\UiFluency"
$taskBackup="$taskWorkspace\AutomationOutputs\UiFluency.relocation-verified-original-20261025"
$taskTarget='H:\MageKnightArchives\20261025_history_additional\UiFluency'
$taskManifest='H:\MageKnightArchives\20261025_history_additional\UiFluency.manifest.json'
$taskRecord=Get-Content -LiteralPath $taskManifest -Raw | ConvertFrom-Json
if ($taskRecord.status -ne 'all_copied_bytes_and_destination_sha256_verified' -or $taskRecord.source -ne $taskSource -or $taskRecord.destination -ne $taskTarget) {throw 'Unexpected verification record'}
if ([IO.Path]::GetFullPath($taskSource) -ne "$taskWorkspace\AutomationOutputs\UiFluency" -or [IO.Path]::GetFullPath($taskBackup) -ne "$taskWorkspace\AutomationOutputs\UiFluency.relocation-verified-original-20261025") {throw 'Unexpected absolute paths'}
if ((Get-Item -LiteralPath $taskSource).Attributes -band [IO.FileAttributes]::ReparsePoint) {throw 'Source already linked'}
if (Test-Path -LiteralPath $taskBackup) {throw 'Backup exists'}
if (@(Get-ChildItem -LiteralPath $taskSource -Recurse -File -Force).Count -ne $taskRecord.files -or @(Get-ChildItem -LiteralPath $taskTarget -Recurse -File -Force).Count -ne $taskRecord.files) {throw 'File count changed'}
Move-Item -LiteralPath $taskSource -Destination $taskBackup
try {New-Item -ItemType Junction -Path $taskSource -Target $taskTarget | Out-Null}
catch {Move-Item -LiteralPath $taskBackup -Destination $taskSource; throw}
$taskLink=Get-Item -LiteralPath $taskSource
if ($taskLink.LinkType -ne 'Junction' -or $taskLink.Target -ne $taskTarget) {throw 'Link mismatch; backup retained'}
foreach ($taskEntry in $taskRecord.entries) {
    $taskAccessPath=Join-Path $taskSource $taskEntry.relative
    if (!(Test-Path -LiteralPath $taskAccessPath -PathType Leaf)) {throw 'Original path unavailable; backup retained'}
}
if (!(Test-Path -LiteralPath "$taskSource\full_cost_progress\report.html")) {throw 'Report unavailable; backup retained'}
# Both absolute targets are fixed above; verified bytes remain on H and through original D paths.
Remove-Item -LiteralPath $taskBackup -Recurse -Force
$taskRecord.status='complete_verified_junction'
$taskRecord | Add-Member -NotePropertyName completedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o'))
$taskRecord | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskManifest -Encoding utf8
$taskSummaryPath="$taskWorkspace\AutomationOutputs\disk_relocation_20261025_additional.json"
$taskSummary=@(Get-Content -LiteralPath $taskSummaryPath -Raw | ConvertFrom-Json)
$taskSummary += [pscustomobject]@{source=$taskSource;destination=$taskTarget;files=$taskRecord.files;bytes=$taskRecord.bytes;status=$taskRecord.status;manifest=$taskManifest}
$taskSummary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $taskSummaryPath -Encoding utf8
Write-Output "Migration completed; D free $([math]::Round((Get-PSDrive D).Free/1GB,2)) GiB"
