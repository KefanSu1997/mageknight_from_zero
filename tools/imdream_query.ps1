Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "imdream_auth.ps1")

function Show-Usage {
@"
Usage:
  imdream_query.ps1 <task_id> [output_dir] [--poll --interval <sec> --timeout <sec>] [--download-name <name>]

Environment variables:
  IMDREAM_ACCESS_KEY     Required
  IMDREAM_SECRET_KEY     Required
  IMDREAM_MODEL          Optional, default jimeng_t2i_v40
"@ | Write-Host
}

function Normalize-Base64([string]$value) {
  if (-not $value) { return $value }
  $pad = (4 - ($value.Length % 4)) % 4
  if ($pad -eq 0) { return $value }
  return $value + ("=" * $pad)
}

function Invoke-ImdreamQuery([string]$taskId, [string]$model, [string]$accessKey, [byte[]]$seedKeyBytes, [string]$sessionToken) {
  $action = "CVSync2AsyncGetResult"
  $version = "2022-08-31"
  $region = "cn-north-1"
  $service = "cv"
  $hostName = "visual.volcengineapi.com"
  $endpoint = "/"
  $contentType = "application/json"
  $requestUrl = "https://${hostName}/?Action=$action&Version=$version"

  $reqJson = @{ return_url = $true } | ConvertTo-Json -Compress
  $bodyMap = @{
    req_key = $model
    task_id = $taskId
    req_json = $reqJson
  }
  $body = $bodyMap | ConvertTo-Json -Depth 6 -Compress

  $sign = Get-ImdreamAuthHeadersWithKeyBytes `
    -Method "POST" `
    -Path $endpoint `
    -Query @{ Action = $action; Version = $version } `
    -HostName $hostName `
    -ContentType $contentType `
    -Body $body `
    -AccessKey $accessKey `
    -SeedKeyBytes $seedKeyBytes `
    -Region $region `
    -Service $service `
    -XSecurityToken $sessionToken

  $headers = @{
    "Content-Type"     = $contentType
    "Host"             = $hostName
    "X-Date"           = $sign.XDate
    "X-Content-Sha256" = $sign.XContentSha256
    "Authorization"    = $sign.Authorization
  }
  if ($sessionToken) {
    $headers["X-Security-Token"] = $sessionToken
  }

  $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($body)
  $responseText = Invoke-ImdreamPost -requestUrl $requestUrl -headers $headers -bodyBytes $bodyBytes -contentType $contentType
  return $responseText | ConvertFrom-Json
}

function Save-Images($response, [string]$outputDir, [string]$taskId) {
  if (-not $response.data -or -not $response.data.binary_data_base64) { return }
  $images = $response.data.binary_data_base64
  if ($images -isnot [System.Array]) { $images = @($images) }
  if ($images.Count -eq 0) { return }

  New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

  for ($i = 0; $i -lt $images.Count; $i++) {
    $b64 = $images[$i]
    if (-not $b64) { continue }
    $ext = "png"
    if ($b64.StartsWith("/9j/")) { $ext = "jpg" }
    elseif ($b64.StartsWith("iVBORw0KGgo")) { $ext = "png" }

    $normalized = Normalize-Base64 $b64
    $bytes = [System.Convert]::FromBase64String($normalized)
    $filePath = Join-Path $outputDir ("{0}_{1}.{2}" -f $taskId, $i, $ext)
    [System.IO.File]::WriteAllBytes($filePath, $bytes)
    Write-Host "imdream_query: saved -> $filePath"
  }
}

function Save-ImageUrls($response, [string]$outputDir, [string]$taskId, [string]$downloadName) {
  if (-not $downloadName) { return }
  if (-not $response.data -or -not $response.data.image_urls) { return }
  $urls = $response.data.image_urls
  if ($urls -isnot [System.Array]) { $urls = @($urls) }
  if ($urls.Count -eq 0) { return }

  New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
  $name = if ($downloadName) { $downloadName } else { $taskId }

  for ($i = 0; $i -lt $urls.Count; $i++) {
    $filePath = Join-Path $outputDir ("{0}_{1}.png" -f $name, $i)
    Invoke-WebRequest -Uri $urls[$i] -OutFile $filePath
    Write-Host "imdream_query: saved -> $filePath"
  }
}

if ($args.Count -lt 1) {
  Show-Usage
  exit 1
}

$taskId = $null
$outputDir = "AutomationOutputs/Imdream"
$outputDirSpecified = $false
$downloadName = $null
$poll = $false
$interval = 5
$timeout = 300

for ($i = 0; $i -lt $args.Count; $i++) {
  $token = $args[$i]
  switch ($token) {
    "--download-name" {
      if ($i + 1 -ge $args.Count) { throw "--download-name requires a name" }
      $downloadName = $args[$i + 1]
      $i++
      continue
    }
    "--poll" {
      $poll = $true
      continue
    }
    "--interval" {
      if ($i + 1 -ge $args.Count) { throw "--interval requires seconds" }
      $interval = [int]$args[$i + 1]
      $i++
      continue
    }
    "--timeout" {
      if ($i + 1 -ge $args.Count) { throw "--timeout requires seconds" }
      $timeout = [int]$args[$i + 1]
      $i++
      continue
    }
    "-h" { Show-Usage; exit 0 }
    "--help" { Show-Usage; exit 0 }
    default {
      if (-not $taskId) {
        $taskId = $token
      } elseif (-not $outputDirSpecified) {
        $outputDir = $token
        $outputDirSpecified = $true
      } else {
        throw "Unexpected argument: $token"
      }
    }
  }
}

if (-not $taskId) {
  Show-Usage
  exit 1
}

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Import-ImdreamEnv (Join-Path $projectRoot ".env")

$accessKey = [Environment]::GetEnvironmentVariable("IMDREAM_ACCESS_KEY", "Process")
$rawSecretKey = [Environment]::GetEnvironmentVariable("IMDREAM_SECRET_KEY", "Process")
$sessionToken = [Environment]::GetEnvironmentVariable("IMDREAM_SESSION_TOKEN", "Process")

if (-not $accessKey -or -not $rawSecretKey) {
  throw "IMDREAM_ACCESS_KEY and IMDREAM_SECRET_KEY must be set"
}

$model = if ($env:IMDREAM_MODEL) { $env:IMDREAM_MODEL } else { "jimeng_t2i_v40" }

$start = Get-Date
function Test-SignatureMismatch([string]$payload) {
  if (-not $payload) { return $false }
  if ($payload -match "SignatureDoesNotMatch") { return $true }
  try {
    $obj = $payload | ConvertFrom-Json
    if ($obj.ResponseMetadata -and $obj.ResponseMetadata.Error -and $obj.ResponseMetadata.Error.Code -eq "SignatureDoesNotMatch") {
      return $true
    }
  } catch {
    return $false
  }
  return $false
}

function Invoke-ImdreamQueryWithFallback([string]$taskIdValue) {
  $keyCandidates = Get-ImdreamSeedKeyCandidates $rawSecretKey
  $lastError = $null
  foreach ($candidate in $keyCandidates) {
    try {
      return Invoke-ImdreamQuery -taskId $taskIdValue -model $model -accessKey $accessKey -seedKeyBytes $candidate.SeedKeyBytes -sessionToken $sessionToken
    } catch {
      $payload = $_.Exception.Message
      if (Test-SignatureMismatch $payload) {
        $lastError = $payload
        continue
      }
      throw
    }
  }
  throw $lastError
}

do {
  $response = Invoke-ImdreamQueryWithFallback -taskIdValue $taskId
  if ($response.code -ne 10000) {
    $err = $response.message
    if (-not $err -and $response.error -and $response.error.message) { $err = $response.error.message }
    throw "API error: $err"
  }

  $hasImages = $false
  if ($response.data -and $response.data.binary_data_base64) { $hasImages = $true }
  if (-not $hasImages -and $response.data -and $response.data.image_urls) { $hasImages = $true }

  if ($hasImages -or -not $poll) { break }

  $elapsed = (Get-Date) - $start
  if ($elapsed.TotalSeconds -ge $timeout) {
    throw "Polling timeout after $timeout seconds"
  }
  Start-Sleep -Seconds $interval
} while ($true)

$response | ConvertTo-Json -Depth 10 | Write-Output
Save-Images -response $response -outputDir $outputDir -taskId $taskId
Save-ImageUrls -response $response -outputDir $outputDir -taskId $taskId -downloadName $downloadName
