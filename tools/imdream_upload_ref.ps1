Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Show-Usage {
@"
Usage:
  imdream_upload_ref.ps1 <local_path>

Uploads a local image to tmpfiles.org and returns a public download URL.
"@ | Write-Host
}

if ($args.Count -lt 1) {
  Show-Usage
  exit 1
}

$path = $args[0]
if (-not (Test-Path $path)) {
  throw "File not found: $path"
}

$uri = "https://tmpfiles.org/api/v1/upload"
$fileItem = Get-Item $path

Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$form = New-Object System.Net.Http.MultipartFormDataContent
$fileStream = [System.IO.File]::OpenRead($fileItem.FullName)

try {
  $fileContent = New-Object System.Net.Http.StreamContent($fileStream)
  $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/octet-stream")
  $form.Add($fileContent, "file", $fileItem.Name) | Out-Null

  $response = $client.PostAsync($uri, $form).Result
  if (-not $response.IsSuccessStatusCode) {
    throw "Upload failed: HTTP $($response.StatusCode)"
  }

  $payload = $response.Content.ReadAsStringAsync().Result | ConvertFrom-Json
} finally {
  $fileStream.Dispose()
  $form.Dispose()
  $client.Dispose()
}

if (-not $payload -or -not $payload.data -or -not $payload.data.url) {
  throw "Upload failed: unexpected response"
}

$url = $payload.data.url
if ($url -match "^https?://tmpfiles.org/") {
  $url = $url -replace "^https?://tmpfiles.org/", "https://tmpfiles.org/dl/"
}

Write-Output $url
