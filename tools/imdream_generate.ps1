param(
  [Parameter(Mandatory = $true)][string]$Prompt,
  [int]$Count = 1,
  [int]$PollSeconds = 2,
  [int]$TimeoutSeconds = 180,
  [string[]]$ImageUrls = @(),
  [double]$Scale = 0.5,
  [switch]$ForceSingle,
  [string]$OutputName = "",
  [string]$OutputDir = "AutomationOutputs/Imdream"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$submitScript = Join-Path $PSScriptRoot "imdream_submit.ps1"
$queryScript = Join-Path $PSScriptRoot "imdream_query.ps1"

if (-not (Test-Path $submitScript)) {
  throw "imdream_submit.ps1 not found at $submitScript"
}
if (-not (Test-Path $queryScript)) {
  throw "imdream_query.ps1 not found at $queryScript"
}

$normalizedOutputName = $OutputName.Trim()
if ($normalizedOutputName) {
  New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

$taskId = & $submitScript -Prompt $Prompt -Count $Count -ImageUrls $ImageUrls -Scale $Scale -ForceSingle:$ForceSingle
if (-not $taskId) {
  throw "No task_id returned from submit."
}

$jsonText = & $queryScript $taskId --poll --interval $PollSeconds --timeout $TimeoutSeconds
if (-not $jsonText) {
  throw "No response returned from query."
}

$response = $jsonText | ConvertFrom-Json
if ($response.data -and $response.data.image_urls) {
  if ($normalizedOutputName) {
    $urls = $response.data.image_urls
    if ($urls -isnot [System.Array]) { $urls = @($urls) }
    for ($i = 0; $i -lt $urls.Count; $i++) {
      $filePath = Join-Path $OutputDir ("{0}_{1}.png" -f $normalizedOutputName, $i)
      Invoke-WebRequest -Uri $urls[$i] -OutFile $filePath
      Write-Output $filePath
    }
    return
  }
  $response.data.image_urls | Write-Output
  return
}

if ($response.data -and $response.data.binary_data_base64) {
  if ($normalizedOutputName) {
    $images = $response.data.binary_data_base64
    if ($images -isnot [System.Array]) { $images = @($images) }
    for ($i = 0; $i -lt $images.Count; $i++) {
      $b64 = $images[$i]
      if (-not $b64) { continue }
      $ext = "png"
      if ($b64.StartsWith("/9j/")) { $ext = "jpg" }
      elseif ($b64.StartsWith("iVBORw0KGgo")) { $ext = "png" }
      $pad = (4 - ($b64.Length % 4)) % 4
      if ($pad -ne 0) { $b64 = $b64 + ("=" * $pad) }
      $bytes = [System.Convert]::FromBase64String($b64)
      $filePath = Join-Path $OutputDir ("{0}_{1}.{2}" -f $normalizedOutputName, $i, $ext)
      [System.IO.File]::WriteAllBytes($filePath, $bytes)
      Write-Output $filePath
    }
    return
  }
  $response.data.binary_data_base64 | Write-Output
  return
}

$jsonText | Write-Output
