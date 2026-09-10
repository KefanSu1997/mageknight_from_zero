# =================================================================================
# FINAL SCRIPT - robust: write+chmod+exec temp bash inside WSL (atomic)
# =================================================================================

param(
  [string]$RepoWSLPath = "/mnt/d/study_and_work/unity-MK-test/MageKnight_from_zero",
  [string]$TaskId = "T-Debug-001",
  [string]$Branch = "",
  [string]$BaseCommit = "",
  [int]$MaxRounds = 5,
  [int]$WaitReviewTimeout = 0,
  [int]$UseMergeBase = 1,
  
  [string]$CodexProfile = "", # Added Missing Parameter passing
  [string]$CodexPromptRegex = "> $",

  # [NEW] Reviewer Strategy
  [ValidateSet("claude","codex")] [string]$ReviewerImpl = "claude",

  # [NEW] Resume params
  [int]$ResumeRound = 1,
  [string]$ResumeStage = "code" 
)

# --- 1. PREPARE PATHS ---
$WorkspaceWSLPath = "$RepoWSLPath/multi-agent-workspace"
$RunWSLPath = "$WorkspaceWSLPath/runs/$TaskId"
$tmpScriptWSLPath = "$RunWSLPath/_tmp_runner.sh"

# --- 2. DEFINE BASH SCRIPT CONTENT (escape $ and $(...) for PowerShell) ---
$bashScriptContent = @"
#!/bin/bash
set -euo pipefail

echo "[BASH] Starting execution of temporary script."
cd '$RepoWSLPath'

# Make sure user env (nvm/pipx/npm PATH etc.) is loaded
if [ -f "`$HOME/.bashrc" ]; then
  # echo "[BASH] Sourcing ~/.bashrc ..."
  source "`$HOME/.bashrc"
fi

# Quiet NAT proxy warnings (optional)
unset HTTP_PROXY HTTPS_PROXY || true

# Common CLI paths
export PATH="`$HOME/.local/bin:`$HOME/.npm-global/bin:`$PATH"

echo "[BASH] Sourcing virtual environment..."
source "`$HOME/.venvs/orchestrator/bin/activate" || true

# Detect Codex CLI absolute path (leave empty if not found)
export CODEX_BIN="`$(command -v codex || true)"
echo "[BASH] Detected CODEX_BIN=`$CODEX_BIN"

echo "[BASH] Exporting environment variables..."
export CODEX_PROFILE="$CodexProfile"
export CODEX_YOLO=1            # Use --dangerously-bypass-approvals-and-sandbox
export CODEX_TIMEOUT_SEC=18000   # 5 hours global timeout if needed, mostly managed by python
export CODEX_MODEL="gpt-5.1-codex-max"  # Optional override

export REPO_DIR='$RepoWSLPath'
export WORKSPACE_DIR='$WorkspaceWSLPath'
export TASK_ID='$TaskId'
export BRANCH='$Branch'
export BASE_COMMIT='$BaseCommit'
export MAX_ROUNDS='$MaxRounds'
export WAIT_REVIEW_TIMEOUT='$WaitReviewTimeout'
export USE_MERGE_BASE='$UseMergeBase'
export CODEX_PROMPT_REGEX='$CodexPromptRegex'

# [NEW] Reviewer & Resume Config
export REVIEWER_IMPL='$ReviewerImpl'
export RESUME_ROUND='$ResumeRound'
export RESUME_STAGE='$ResumeStage'

export REVIEWER_MAX_TURNS="3"
export SP_CODEX_PATH='$WorkspaceWSLPath/prompts/sp_codex.md'
export SP_REVIEWER_PATH='$WorkspaceWSLPath/prompts/sp_reviewer.md'

echo "[BASH] All variables set. Running orchestrator ($ReviewerImpl)..."
"`$HOME/.venvs/orchestrator/bin/python" tools/orchestrator.py
echo "[BASH] Orchestrator finished."
"@

# --- 3. CREATE, EXECUTE, AND CLEAN UP THE SCRIPT (atomic) ---
try {
    # ensure dir exists
    wsl.exe -- mkdir -p "'$RunWSLPath'"

    $commandToCreateAndExecute = "cat > '$tmpScriptWSLPath' && chmod +x '$tmpScriptWSLPath' && '$tmpScriptWSLPath'"
    Write-Host "[INFO] Atomically creating, setting permissions, and executing temporary script inside WSL..."

    # pipe the bash content and run
    $bashScriptContent | wsl.exe -- bash -c $commandToCreateAndExecute

    Write-Host "[INFO] Temporary script execution finished."
}
finally {
    Write-Host "[INFO] Cleaning up temporary script..."
    wsl.exe -- rm -f "'$tmpScriptWSLPath'"
}
