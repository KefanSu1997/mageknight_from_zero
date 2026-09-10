Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "imdream_auth.ps1")

function Show-Usage {
@"
Usage:
  generate_imdream_image.ps1 "<prompt>" [legacy_count] [options]

Options:
  --ref <url>            Add a reference image URL (repeatable, max 10)
  --width <int>          Output width (must pair with --height)
  --height <int>         Output height (must pair with --width)
  --size <int>           Output area (used when width/height not provided)
  --scale <float>        Prompt strength in [0,1]
  --force-single         Force single image result
  --min-ratio <float>    Min aspect ratio (width/height)
  --max-ratio <float>    Max aspect ratio (width/height)
  -h, --help             Show help

Environment variables:
  IMDREAM_ACCESS_KEY     Required
  IMDREAM_SECRET_KEY     Required
  IMDREAM_MODEL          Optional, default jimeng_t2i_v40
  IMDREAM_IMAGE_REFS     Optional, comma-separated reference URLs
"@ | Write-Host
}

function Assert-Number([string]$label, [string]$value, [string]$pattern) {
  if (-not ($value -match $pattern)) {
    throw "$label is invalid: $value"
  }
}

if ($args.Count -lt 1) {
  Show-Usage
  exit 1
}

$prompt = $null
$legacyCount = $null
$model = if ($env:IMDREAM_MODEL) { $env:IMDREAM_MODEL } else { "jimeng_t2i_v40" }
$size = $null
$width = $null
$height = $null
$scale = $null
$forceSingle = $false
$minRatio = $null
$maxRatio = $null
$refImages = New-Object System.Collections.Generic.List[string]

for ($i = 0; $i -lt $args.Count; $i++) {
  $token = $args[$i]
  switch ($token) {
    "--ref" {
      if ($i + 1 -ge $args.Count) { throw "--ref requires a URL" }
      $refImages.Add($args[$i + 1])
      $i++
      continue
    }
    "--width" {
      if ($i + 1 -ge $args.Count) { throw "--width requires an integer" }
      Assert-Number "--width" $args[$i + 1] '^[0-9]+$'
      $width = $args[$i + 1]
      $i++
      continue
    }
    "--height" {
      if ($i + 1 -ge $args.Count) { throw "--height requires an integer" }
      Assert-Number "--height" $args[$i + 1] '^[0-9]+$'
      $height = $args[$i + 1]
      $i++
      continue
    }
    "--size" {
      if ($i + 1 -ge $args.Count) { throw "--size requires an integer" }
      Assert-Number "--size" $args[$i + 1] '^[0-9]+$'
      $size = $args[$i + 1]
      $i++
      continue
    }
    "--scale" {
      if ($i + 1 -ge $args.Count) { throw "--scale requires a float" }
      Assert-Number "--scale" $args[$i + 1] '^[0-9]+(\.[0-9]+)?$'
      $scale = $args[$i + 1]
      $i++
      continue
    }
    "--force-single" {
      $forceSingle = $true
      continue
    }
    "--min-ratio" {
      if ($i + 1 -ge $args.Count) { throw "--min-ratio requires a float" }
      Assert-Number "--min-ratio" $args[$i + 1] '^[0-9]+(\.[0-9]+)?$'
      $minRatio = $args[$i + 1]
      $i++
      continue
    }
    "--max-ratio" {
      if ($i + 1 -ge $args.Count) { throw "--max-ratio requires a float" }
      Assert-Number "--max-ratio" $args[$i + 1] '^[0-9]+(\.[0-9]+)?$'
      $maxRatio = $args[$i + 1]
      $i++
      continue
    }
    "-h" { Show-Usage; exit 0 }
    "--help" { Show-Usage; exit 0 }
    default {
      if (-not $prompt) {
        $prompt = $token
      } elseif (-not $legacyCount) {
        $legacyCount = $token
      } else {
        throw "Unexpected argument: $token"
      }
    }
  }
}

if (-not $prompt) {
  Show-Usage
  exit 1
}

if (($width -and -not $height) -or ($height -and -not $width)) {
  throw "--width and --height must be provided together"
}

if ($legacyCount -and ($legacyCount -match '^[0-9]+$')) {
  if ([int]$legacyCount -gt 1) {
    Write-Warning "legacy_count is ignored by the v4 API"
  }
}

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Import-ImdreamEnv (Join-Path $projectRoot ".env")

$accessKey = [Environment]::GetEnvironmentVariable("IMDREAM_ACCESS_KEY", "Process")
$rawSecretKey = [Environment]::GetEnvironmentVariable("IMDREAM_SECRET_KEY", "Process")
$sessionToken = [Environment]::GetEnvironmentVariable("IMDREAM_SESSION_TOKEN", "Process")

if (-not $accessKey -or -not $rawSecretKey) {
  throw "IMDREAM_ACCESS_KEY and IMDREAM_SECRET_KEY must be set"
}

if ($env:IMDREAM_IMAGE_REFS) {
  $env:IMDREAM_IMAGE_REFS.Split(",") | ForEach-Object {
    $item = $_.Trim()
    if ($item) { $refImages.Add($item) }
  }
}

if ($refImages.Count -gt 10) {
  throw "Too many reference images (max 10)"
}

$bodyMap = @{
  req_key = $model
  prompt  = $prompt
}

if ($refImages.Count -gt 0) { $bodyMap.image_urls = $refImages.ToArray() }
if ($size) { $bodyMap.size = [int]$size }
if ($width) { $bodyMap.width = [int]$width }
if ($height) { $bodyMap.height = [int]$height }
if ($scale) { $bodyMap.scale = [double]$scale }
if ($forceSingle) { $bodyMap.force_single = $true }
if ($minRatio) { $bodyMap.min_ratio = [double]$minRatio }
if ($maxRatio) { $bodyMap.max_ratio = [double]$maxRatio }

$body = $bodyMap | ConvertTo-Json -Depth 6 -Compress

$action = "CVSync2AsyncSubmitTask"
$version = "2022-08-31"
$region = "cn-north-1"
$service = "cv"
$hostName = "visual.volcengineapi.com"
$endpoint = "/"
$contentType = "application/json"
$requestUrl = "https://${hostName}/?Action=$action&Version=$version"

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

function Invoke-ImdreamSignedRequest([byte[]]$seedKeyBytes) {
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

$keyCandidates = Get-ImdreamSeedKeyCandidates $rawSecretKey

$response = $null
$lastError = $null
foreach ($candidate in $keyCandidates) {
  try {
    $response = Invoke-ImdreamSignedRequest -seedKeyBytes $candidate.SeedKeyBytes
    $lastError = $null
    break
  } catch {
    $payload = $_.Exception.Message
    if (Test-SignatureMismatch $payload) {
      $lastError = $payload
      continue
    }
    throw
  }
}

if (-not $response) {
  throw $lastError
}

if ($response.code -ne 10000) {
  $err = $response.message
  if (-not $err -and $response.error -and $response.error.message) { $err = $response.error.message }
  throw "API error: $err"
}

Write-Output $response.data.task_id
