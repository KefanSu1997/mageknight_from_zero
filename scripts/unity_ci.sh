#!/usr/bin/env bash
set -euo pipefail

UNITY_WIN_DEFAULT="D:\Unity\Editor\2023.2.20f1c1\Editor\Unity.exe"

usage() {
  echo "用法: $0 -p <project_path_wsl> -m <Compile|EditMode|PlayMode> [-u <UnityExeWinPath>] [-l <logFile>] [-r <results>]"
}

UNITY_WIN="$UNITY_WIN_DEFAULT"
PROJ_WSL=""
MODE=""
LOG_FILE=""
RESULTS=""

while getopts ":p:m:u:l:r:" opt; do
  case $opt in
    p) PROJ_WSL="$OPTARG" ;;
    m) MODE="$OPTARG" ;;
    u) UNITY_WIN="$OPTARG" ;;
    l) LOG_FILE="$OPTARG" ;;
    r) RESULTS="$OPTARG" ;;
    *) usage; exit 1 ;;
  esac
done

[[ -z "$PROJ_WSL" || -z "$MODE" ]] && { usage; exit 1; }

PROJ_WIN="$(wslpath -w "$PROJ_WSL")"
LOG_FILE="${LOG_FILE:-AutomationLogs/ci.log}"
RESULTS="${RESULTS:-AutomationOutputs/results.xml}"

# 统一创建输出目录
mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$RESULTS")"

# 调用已有的 PowerShell 脚本
powershell.exe -ExecutionPolicy Bypass -File scripts/unity_ci.ps1 \
  -UnityExe "$UNITY_WIN" -ProjectPath "$PROJ_WIN" \
  -Mode "$MODE" -LogFile "$LOG_FILE" -Results "$RESULTS"
