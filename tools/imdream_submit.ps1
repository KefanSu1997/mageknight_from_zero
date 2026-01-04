param(
  [Parameter(Mandatory = $true)][string]$Prompt,
  [int]$Count = 1,
  [string[]]$ImageUrls = @(),
  [double]$Scale = 0.5,
  [switch]$ForceSingle
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$submitScript = Join-Path $PSScriptRoot "generate_imdream_image.ps1"
if (-not (Test-Path $submitScript)) {
  throw "generate_imdream_image.ps1 not found at $submitScript"
}

if ($Count -gt 1) {
  Write-Warning "Count is ignored by the v4 API; submitting a single task."
}

$argsList = New-Object System.Collections.Generic.List[string]
$argsList.Add($Prompt)

if ($Scale -ge 0) {
  $argsList.Add("--scale")
  $argsList.Add($Scale.ToString("0.##"))
}

if ($ForceSingle.IsPresent -or $Count -le 1) {
  $argsList.Add("--force-single")
}

foreach ($url in $ImageUrls) {
  if ([string]::IsNullOrWhiteSpace($url)) { continue }
  $argsList.Add("--ref")
  $argsList.Add($url)
}

& $submitScript @argsList
