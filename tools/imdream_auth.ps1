Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Import-ImdreamEnv([string]$Path) {
  if (-not (Test-Path $Path)) { return }
  Get-Content $Path | ForEach-Object {
    $line = $_.Trim()
    if (-not $line -or $line.StartsWith("#")) { return }
    $pair = $line -split "=", 2
    if ($pair.Count -ne 2) { return }
    $key = $pair[0].Trim()
    $value = $pair[1].Trim()
    $existing = Get-Item -Path "env:$key" -ErrorAction SilentlyContinue
    if (-not $existing -or [string]::IsNullOrWhiteSpace($existing.Value)) {
      Set-Item -Path "env:$key" -Value $value
    }
  }
}

function To-HexLower([byte[]]$bytes) {
  ($bytes | ForEach-Object { $_.ToString("x2") }) -join ""
}

function Sha256Hex([string]$value) {
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
    return To-HexLower $sha.ComputeHash($bytes)
  } finally {
    $sha.Dispose()
  }
}

function HmacSha256Bytes([byte[]]$keyBytes, [string]$content) {
  $hmac = [System.Security.Cryptography.HMACSHA256]::new($keyBytes)
  try {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($content)
    return $hmac.ComputeHash($bytes)
  } finally {
    $hmac.Dispose()
  }
}

function Normalize-Query([hashtable]$query) {
  $keys = $query.Keys | Sort-Object
  $pairs = New-Object System.Collections.Generic.List[string]
  foreach ($key in $keys) {
    $value = [string]$query[$key]
    $pairs.Add("$([System.Uri]::EscapeDataString($key))=$([System.Uri]::EscapeDataString($value))")
  }
  return ($pairs -join "&")
}

function Build-SignedHeaders(
  [string]$HostName,
  [string]$ContentType,
  [string]$XContentSha256,
  [string]$XDate,
  [string]$XSecurityToken
) {
  $headers = New-Object System.Collections.Generic.List[string]
  $signed = New-Object System.Collections.Generic.List[string]

  $headers.Add("content-type:$ContentType"); $signed.Add("content-type")
  $headers.Add("host:$HostName"); $signed.Add("host")
  $headers.Add("x-content-sha256:$XContentSha256"); $signed.Add("x-content-sha256")
  $headers.Add("x-date:$XDate"); $signed.Add("x-date")

  if ($XSecurityToken -and $XSecurityToken.Trim().Length -gt 0) {
    $headers.Add("x-security-token:$XSecurityToken"); $signed.Add("x-security-token")
  }

  return @{
    CanonicalHeaders = ($headers -join "`n")
    SignedHeaders = ($signed -join ";")
  }
}

function Maybe-Decode-Base64([string]$value) {
  if (-not $value) { return $value }
  $candidate = $value.Trim()
  for ($i = 0; $i -lt 5; $i++) {
    $padded = $candidate + ("=" * ((4 - ($candidate.Length % 4)) % 4))
    try {
      $decodedBytes = [System.Convert]::FromBase64String($padded)
    } catch {
      return $candidate
    }

    $decoded = [System.Text.Encoding]::UTF8.GetString($decodedBytes).Trim()
    if (-not $decoded) { return $candidate }
    if ($decoded -match '^[0-9a-fA-F]+$') { return $decoded }
    $candidate = $decoded
  }
  return $candidate
}

function Get-ImdreamSecretKey([string]$rawSecret) {
  $raw = $rawSecret
  if (-not $raw) { return $raw }
  $decodeFlag = [Environment]::GetEnvironmentVariable("IMDREAM_SECRET_KEY_DECODE_BASE64", "Process")
  if ($decodeFlag -and $decodeFlag.Trim().ToLowerInvariant() -in @("0","false","no","off")) {
    return $raw.Trim()
  }
  return (Maybe-Decode-Base64 $raw)
}

function Invoke-ImdreamPost([string]$requestUrl, [hashtable]$headers, [byte[]]$bodyBytes, [string]$contentType) {
  Add-Type -AssemblyName System.Net.Http
  $client = [System.Net.Http.HttpClient]::new()
  $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, $requestUrl)
  $content = [System.Net.Http.ByteArrayContent]::new($bodyBytes)
  $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse($contentType)
  $request.Content = $content

  if ($headers.ContainsKey("Host")) {
    $request.Headers.Host = [string]$headers["Host"]
  }
  foreach ($key in $headers.Keys) {
    if ($key -eq "Host" -or $key -eq "Content-Type") { continue }
    [void]$request.Headers.TryAddWithoutValidation($key, [string]$headers[$key])
  }

  $response = $null
  $payload = $null
  try {
    $response = $client.SendAsync($request).Result
    $payload = $response.Content.ReadAsStringAsync().Result
  } finally {
    $content.Dispose()
    $request.Dispose()
    $client.Dispose()
  }

  if (-not $response) {
    throw "Request failed: no response"
  }
  if (-not $response.IsSuccessStatusCode) {
    throw $payload
  }

  return $payload
}

function Get-ImdreamAuthHeadersWithKeyBytes(
  [string]$Method,
  [string]$Path,
  [hashtable]$Query,
  [string]$HostName,
  [string]$ContentType,
  [string]$Body,
  [string]$AccessKey,
  [byte[]]$SeedKeyBytes,
  [string]$Region,
  [string]$Service,
  [string]$XSecurityToken
) {
  $xDate = (Get-Date).ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'")
  $shortDate = $xDate.Substring(0, 8)
  $bodyHash = Sha256Hex $Body
  $canonicalQuery = Normalize-Query $Query
  $headerInfo = Build-SignedHeaders $HostName $ContentType $bodyHash $xDate $XSecurityToken

  $canonicalRequest = @(
    $Method.ToUpper(),
    $Path,
    $canonicalQuery,
    $headerInfo.CanonicalHeaders,
    "",
    $headerInfo.SignedHeaders,
    $bodyHash
  ) -join "`n"

  $hashedCanonicalRequest = Sha256Hex $canonicalRequest
  $credentialScope = "$shortDate/$Region/$Service/request"
  $stringToSign = @("HMAC-SHA256", $xDate, $credentialScope, $hashedCanonicalRequest) -join "`n"

  $kDate = HmacSha256Bytes $SeedKeyBytes $shortDate
  $kRegion = HmacSha256Bytes $kDate $Region
  $kService = HmacSha256Bytes $kRegion $Service
  $kSigning = HmacSha256Bytes $kService "request"
  $signature = To-HexLower (HmacSha256Bytes $kSigning $stringToSign)

  return @{
    XDate = $xDate
    XContentSha256 = $bodyHash
    Authorization = "HMAC-SHA256 Credential=$AccessKey/$credentialScope, SignedHeaders=$($headerInfo.SignedHeaders), Signature=$signature"
  }
}

function Get-ImdreamSignPrefix {
  $prefix = [Environment]::GetEnvironmentVariable("IMDREAM_SIGN_PREFIX", "Process")
  if ([string]::IsNullOrWhiteSpace($prefix)) { return "VC3" }
  $normalized = $prefix.Trim().ToLowerInvariant()
  if ($normalized -in @("none","off","0","false")) { return "" }
  return $prefix
}

function Get-ImdreamAuthHeaders(
  [string]$Method,
  [string]$Path,
  [hashtable]$Query,
  [string]$HostName,
  [string]$ContentType,
  [string]$Body,
  [string]$AccessKey,
  [string]$SecretKey,
  [string]$Region,
  [string]$Service,
  [string]$XSecurityToken
) {
  $prefix = Get-ImdreamSignPrefix
  $seedKeyBytes = [System.Text.Encoding]::UTF8.GetBytes("$prefix$SecretKey")
  return Get-ImdreamAuthHeadersWithKeyBytes `
    -Method $Method `
    -Path $Path `
    -Query $Query `
    -HostName $HostName `
    -ContentType $ContentType `
    -Body $Body `
    -AccessKey $AccessKey `
    -SeedKeyBytes $seedKeyBytes `
    -Region $Region `
    -Service $Service `
    -XSecurityToken $XSecurityToken
}

function Get-ImdreamSeedKeyCandidates([string]$rawSecret) {
  $candidates = New-Object System.Collections.Generic.List[object]
  if (-not $rawSecret) { return $candidates }

  $prefixSetting = [Environment]::GetEnvironmentVariable("IMDREAM_SIGN_PREFIX", "Process")
  if ([string]::IsNullOrWhiteSpace($prefixSetting)) {
    $prefixes = @("", "VC3")
  } else {
    $normalized = $prefixSetting.Trim().ToLowerInvariant()
    if ($normalized -in @("none","off","0","false")) {
      $prefixes = @("")
    } else {
      $prefixes = @($prefixSetting)
    }
  }

  $raw = $rawSecret.Trim()
  $padded = $raw + ("=" * ((4 - ($raw.Length % 4)) % 4))
  $decodedBytes = $null
  try {
    $decodedBytes = [System.Convert]::FromBase64String($padded)
  } catch {
    $decodedBytes = $null
  }

  foreach ($prefix in $prefixes) {
    if ($raw) {
      $candidates.Add(@{
        Label = "$prefix:raw-string"
        SeedKeyBytes = [System.Text.Encoding]::UTF8.GetBytes("$prefix$raw")
      })
    }

    if ($decodedBytes) {
      $prefixBytes = [System.Text.Encoding]::UTF8.GetBytes($prefix)
      $seedBytes = New-Object byte[] ($prefixBytes.Length + $decodedBytes.Length)
      [System.Array]::Copy($prefixBytes, 0, $seedBytes, 0, $prefixBytes.Length)
      [System.Array]::Copy($decodedBytes, 0, $seedBytes, $prefixBytes.Length, $decodedBytes.Length)
      $candidates.Add(@{
        Label = "$prefix:base64-bytes"
        SeedKeyBytes = $seedBytes
      })

      try {
        $decodedString = [System.Text.Encoding]::UTF8.GetString($decodedBytes).Trim()
        if ($decodedString) {
          $candidates.Add(@{
            Label = "$prefix:base64-text"
            SeedKeyBytes = [System.Text.Encoding]::UTF8.GetBytes("$prefix$decodedString")
          })
        }
      } catch {
        # ignore non-text
      }
    }
  }

  return $candidates
}

function Resolve-PythonExecutable {
  $candidates = @("python", "py")
  foreach ($name in $candidates) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
  }
  throw "Python not found. Install Python or ensure python/py is on PATH."
}

function Get-ImdreamAuthHeadersFromPython(
  [string]$Method,
  [string]$Path,
  [hashtable]$Query,
  [string]$HostName,
  [string]$ContentType,
  [string]$Body,
  [string]$AccessKey,
  [string]$SecretKey,
  [string]$Region,
  [string]$Service,
  [string]$XSecurityToken
) {
  $pythonExe = Resolve-PythonExecutable
  $signScript = Join-Path $PSScriptRoot "imdream_sign_helper.py"
  $tempPath = [System.IO.Path]::GetTempFileName()
  $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($Body)
  [System.IO.File]::WriteAllBytes($tempPath, $bodyBytes)

  $prevAk = $env:IMDREAM_ACCESS_KEY
  $prevSk = $env:IMDREAM_SECRET_KEY
  $prevToken = $env:IMDREAM_SESSION_TOKEN
  $prevDecode = $env:IMDREAM_SECRET_KEY_DECODE_BASE64

  $env:IMDREAM_ACCESS_KEY = $AccessKey
  $env:IMDREAM_SECRET_KEY = $SecretKey
  $env:IMDREAM_SECRET_KEY_DECODE_BASE64 = "0"
  if ($XSecurityToken) {
    $env:IMDREAM_SESSION_TOKEN = $XSecurityToken
  } else {
    Remove-Item env:IMDREAM_SESSION_TOKEN -ErrorAction SilentlyContinue
  }

  $cmdArgs = @(
    "`"$pythonExe`"",
    "`"$signScript`"",
    "--action", $Query.Action,
    "--version", $Query.Version,
    "--region", $Region,
    "--service", $Service,
    "--host", $HostName,
    "--method", $Method,
    "--path", $Path,
    "--content-type", $ContentType,
    "<", "`"$tempPath`""
  )
  $cmd = ($cmdArgs | ForEach-Object { $_ }) -join " "

  try {
    $output = cmd /c $cmd 2>&1
    $exitCode = $LASTEXITCODE
  } finally {
    if ($null -ne $prevAk) { $env:IMDREAM_ACCESS_KEY = $prevAk } else { Remove-Item env:IMDREAM_ACCESS_KEY -ErrorAction SilentlyContinue }
    if ($null -ne $prevSk) { $env:IMDREAM_SECRET_KEY = $prevSk } else { Remove-Item env:IMDREAM_SECRET_KEY -ErrorAction SilentlyContinue }
    if ($null -ne $prevToken) { $env:IMDREAM_SESSION_TOKEN = $prevToken } else { Remove-Item env:IMDREAM_SESSION_TOKEN -ErrorAction SilentlyContinue }
    if ($null -ne $prevDecode) { $env:IMDREAM_SECRET_KEY_DECODE_BASE64 = $prevDecode } else { Remove-Item env:IMDREAM_SECRET_KEY_DECODE_BASE64 -ErrorAction SilentlyContinue }
    Remove-Item $tempPath -ErrorAction SilentlyContinue
  }

  if ($exitCode -ne 0) {
    throw "Python signer failed: $output"
  }

  $sign = $output | ConvertFrom-Json
  if ($sign.error) { throw "Python signer error: $($sign.error)" }

  return @{
    XDate = $sign.x_date
    XContentSha256 = $sign.x_content_sha256
    Authorization = $sign.authorization
  }
}
