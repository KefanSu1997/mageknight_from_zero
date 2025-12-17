# SCRIPT MUST BE SAVED AS UTF-8 WITH BOM
# 纯 Windows 原生版本 - 修复 WinError 193 (强制使用 .cmd 或 .exe)
param(
  [string]$RepoPath     = "D:\study_and_work\unity-MK-test\MageKnight_from_zero",
  [string]$TaskId       = "T-20251028-005",
  # 指向你的 Python 解释器
  [string]$PythonExe    = "C:\Users\sukefan\anaconda3\envs\draw_plot\python.exe", 
  
  [ValidateSet("claude","codex")] [string]$Reviewer = "claude",
  [ValidateSet("codex","ccr")] [string]$Planner = "codex",
  [string]$CodexProfile = "",
  
  [int]$ResumeRound = 1,
  [ValidateSet("code","review")] [string]$ResumeStage = "code"
)

$ErrorActionPreference = "Stop"

# ---------- 0. 环境检查 (修复 WinError 193) ----------
Write-Host "[INFO] Detecting Environment..." -ForegroundColor Cyan

$CodexPath = $null

# 1. 优先寻找 npm 安装的 .cmd (能被 python 直接执行)
$CmdVersion = Get-Command "codex.cmd" -ErrorAction SilentlyContinue
if ($CmdVersion) {
    $CodexPath = $CmdVersion.Source
} else {
    # 2. 其次寻找 pip 安装的 .exe
    $ExeVersion = Get-Command "codex.exe" -ErrorAction SilentlyContinue
    if ($ExeVersion) {
        $CodexPath = $ExeVersion.Source
    }
}

# 3. 如果没找到，尝试在 Python Scripts 目录暴力查找
if (-not $CodexPath) {
    $PyDir = [System.IO.Path]::GetDirectoryName($PythonExe)
    $TryCmd = Join-Path $PyDir "Scripts\codex.cmd" # pip 在 Windows 上有时也生成 .cmd
    $TryExe = Join-Path $PyDir "Scripts\codex.exe" 
    
    if (Test-Path $TryCmd) { $CodexPath = $TryCmd }
    elseif (Test-Path $TryExe) { $CodexPath = $TryExe }
}

if ($CodexPath) {
    Write-Host "   ✔ Found Codex Binary at: $CodexPath" -ForegroundColor Green
    $env:CODEX_BIN = $CodexPath
} else {
    # 如果实在找不到，只能试图用 'codex' 让系统 PATH 决定，但这可能又回到 ps1
    # 我们这里做一个特殊的 fallback，直接指定 shell=True 可能会在 python 里处理
    Write-Warning "   ⚠ Cannot find explicit .cmd or .exe for codex. Using 'codex' (Might fail with WinError 193)."
    $env:CODEX_BIN = "codex.cmd" # 强行加上 .cmd 后缀试试
}

# 传递 Reviewer 设置
$env:REVIEWER_IMPL = $Reviewer
Write-Host "   ✔ Reviewer Strategy: $Reviewer" -ForegroundColor Green

# ---------- 1. Path Setup ----------
$RepoPath = $RepoPath.TrimEnd("\")
$Workspace = Join-Path $RepoPath "multi-agent-workspace"
$RunDir = Join-Path $Workspace ("runs\" + $TaskId)
$DraftDir = Join-Path $RunDir "_draft"
$sysPromptPath = Join-Path $Workspace "prompts\sp_codex.md"
$ProposalPath = Join-Path $DraftDir "proposal.json"
$TaskSpecPath = Join-Path $RunDir "task_spec.json"
$ApprovedPath = Join-Path $DraftDir "APPROVED.task_spec.json"
$SchemaPath = Join-Path $RepoPath "schemas\task_spec.schema.json"

# ---------- 2. Init Workspace & Logging ----------
Write-Host "[INFO] Checking workspace directories..." 
$dirs = @(
    $RunDir, 
    $DraftDir, 
    (Join-Path $RunDir "review_bundle\artifacts\screenshots"), 
    (Join-Path $Workspace "compile")
)
foreach ($d in $dirs) { 
    if (-not(Test-Path $d)) { 
        New-Item -ItemType Directory -Force -Path $d | Out-Null
    } 
}

# Start Logging
$LogPath = Join-Path $RunDir "session_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
try { Stop-Transcript | Out-Null } catch {}
Start-Transcript -Path $LogPath -Append
Write-Host ">>> Logging started: $LogPath"

if ($ResumeRound -gt 1 -or $ResumeStage -ne "code") {
    Write-Host "[RESUME MODE] Starting at Round $ResumeRound - Stage: $ResumeStage" -ForegroundColor Yellow
}

# Create Default Proposal if missing
if (!(Test-Path -LiteralPath $ProposalPath)) {
  Write-Host "[INFO] Creating default proposal.json..."
  $def = [ordered]@{ 
      title = "New Task"; 
      work_branch = "feat/$TaskId"; 
      description = "Describe task here"; 
      acceptance = [ordered]@{ 
          compile_ok_path = "multi-agent-workspace/runs/$TaskId/compile_status.json";
          screenshots_dir = "multi-agent-workspace/runs/$TaskId/review_bundle/artifacts/screenshots"; 
          required_screenshots = @([ordered]@{ name="layout"; pattern="*.png"; min_count=1 }) 
      } 
  }
  $def | ConvertTo-Json -Depth 8 | Out-File $ProposalPath -Encoding utf8
}

# ---------- 3. Launch Planner (Only if not resuming) ----------
if ($ResumeRound -eq 1 -and $ResumeStage -eq "code") {
    Write-Host "[INFO] Launching Codex for Planning..." -ForegroundColor Cyan
    
    if (Test-Path $sysPromptPath) {
        Write-Host "   Copying System Prompt to clipboard..."
        Get-Content -Raw $sysPromptPath | Set-Clipboard
    } else {
        Write-Warning "   System Prompt not found at: $sysPromptPath"
    }

    # 启动 cmd 窗口
    $planCmd = "echo '>>> SP is in clipboard.' && codex --sandbox read-only $CodexProfile"
    Start-Process wt.exe -ArgumentList "new-tab", "--title", "Planning", "--", "cmd", "/k", $planCmd

    Write-Host "👉 Please review proposal: $ProposalPath"
    Write-Host "   Waiting for approval file: $ApprovedPath ..."

    while (-not (Test-Path $ApprovedPath)) {
        Start-Sleep -Seconds 2
    }
    Write-Host "[SUCCESS] Approved found." -ForegroundColor Green

    Write-Host "[INFO] Validating task spec..."
    & $PythonExe "tools\validate_json.py" --input "$ApprovedPath" --fallback "$ProposalPath" --schema "$SchemaPath" --output "$TaskSpecPath"
    if ($LASTEXITCODE -ne 0) { Write-Error "Validation failed."; exit 2 }
} else {
    Write-Host "[INFO] Skipping Planning (Resume Mode)"
}

# ---------- 4. Execute Orchestrator ----------
Write-Host "[INFO] Starting Orchestrator..." -ForegroundColor Cyan

$env:REPO_DIR = $RepoPath
$env:TASK_ID = $TaskId
$env:CODEX_PROFILE = $CodexProfile
$env:RESUME_ROUND = $ResumeRound
$env:RESUME_STAGE = $ResumeStage
# $env:CODEX_BIN 已在顶部设置
$env:CLAUDE_BIN = "claude.cmd"

# 直接调用 Python
& $PythonExe "tools\orchestrator.py"

Stop-Transcript
