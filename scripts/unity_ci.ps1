# scripts/unity_ci.ps1
param(
  [Parameter(Mandatory=$true)][string]$UnityExe,
  [Parameter(Mandatory=$true)][string]$ProjectPath,
  [ValidateSet("Compile","EditMode","PlayMode")][string]$Mode = "Compile",
  [string]$LogFile = "AutomationLogs/ci.log",
  [string]$Results = "AutomationOutputs/results.xml"
)

if (-not (Test-Path $UnityExe)) {
  Write-Error "Unity not found at: $UnityExe"
  exit 2
}

# 确保输出目录存在
$logDir = Split-Path $LogFile -Parent
$resDir = Split-Path $Results -Parent
New-Item -Force -ItemType Directory $logDir  | Out-Null
New-Item -Force -ItemType Directory $resDir  | Out-Null

$common = @(
  "-batchmode","-nographics","-quit",
  "-projectPath", $ProjectPath,
  "-logFile", $LogFile
)

if ($Mode -eq "Compile") {
  & $UnityExe $common "-executeMethod" "CIHooks.CompileAndQuit"
  exit $LASTEXITCODE
}

if ($Mode -eq "EditMode" -or $Mode -eq "PlayMode") {
  $plat = if ($Mode -eq "EditMode") {"EditMode"} else {"PlayMode"}
  & $UnityExe $common "-runTests" "-testPlatform" $plat "-testResults" $Results
  exit $LASTEXITCODE
}
