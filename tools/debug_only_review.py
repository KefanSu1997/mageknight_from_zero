#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os, json, subprocess, sys, glob, re
from pathlib import Path

# ---------- 调试配置 ----------
os.environ["TASK_ID"] = "T-Debug-001"
IS_WSL = "WSL_DISTRO_NAME" in os.environ

# ---------- 路径设定 ----------
REPO_DIR = Path(os.getcwd()).resolve()
WORKSPACE = REPO_DIR / "multi-agent-workspace"
TASK_ID = os.getenv("TASK_ID")
RUNS_DIR = WORKSPACE / "runs" / TASK_ID
BUNDLE_DIR = RUNS_DIR / "review_bundle"
SHOT_DIR = BUNDLE_DIR / "artifacts" / "screenshots"
SPEC_PATH = RUNS_DIR / "task_spec.json"

# [必须] 你的绝对路径
CLAUDE_BIN_FULL = r"C:\Users\sukefan\AppData\Roaming\npm\claude.cmd"


def wsl_path_to_windows(linux_path):
    if not IS_WSL: return str(linux_path)
    try:
        return subprocess.run(["wslpath", "-w", str(linux_path)], capture_output=True, text=True).stdout.strip()
    except:
        return str(linux_path)


def load_task_spec():
    """读取任务说明书"""
    if not SPEC_PATH.exists():
        print(f"❌ Critical: Task Spec not found at {SPEC_PATH}")
        # 返回一个伪造的，防止脚本崩溃，但在真实场景应报错
        return {
            "title": "Debug Task",
            "description": "No spec found.",
            "acceptance": {"rubrics_checklist": []}
        }
    try:
        with open(SPEC_PATH, 'r', encoding='utf-8') as f:
            return json.load(f)
    except Exception as e:
        print(f"❌ Error reading JSON: {e}")
        return {}


def drive_reviewer_claude():
    print(f"\n[INFO] 🛠️ DEBUG MODE: Context-Aware Reviewer")

    # 1. 读取 Task Spec
    spec = load_task_spec()
    rubrics = spec.get("acceptance", {}).get("rubrics_checklist", [])
    description = spec.get("description", "No description provided.")

    # 格式化验收标准文本
    rubrics_text = ""
    for r in rubrics:
        rubrics_text += f"- [{r.get('id')}] {r.get('text')} (Required: {r.get('required')})\n"

    print(f"[Info] Loaded Spec: {spec.get('title')}")
    print(f"[Info] Found {len(rubrics)} rubric items.")

    # 2. 准备文件
    patch_files = glob.glob(str(BUNDLE_DIR / "diffs" / "*.patch"))
    patch_path = Path(patch_files[0]) if patch_files else BUNDLE_DIR / "diffs" / "debug.patch"
    if not patch_path.exists(): patch_path.write_text("No changes.", encoding="utf-8")

    screenshots = glob.glob(str(SHOT_DIR / "*.png")) + glob.glob(str(SHOT_DIR / "*.jpg"))
    print(f"[Info] Found {len(screenshots)} screenshots.")

    # 3. 构造 智能 Prompt (Context-Aware)
    prompt_text = f"""
[ROLE]
You are the Art Director & QA Lead for a Unity game project.

[CONTEXT - TASK SPECIFICATION]
The developer was tasked to implement the following feature:
Title: {spec.get('title')}
Description: {description}

[ACCEPTANCE CRITERIA / RUBRICS]
You must strictly verify the attached screenshots against these rules:
{rubrics_text}

[YOUR JOB]
1. LOOK at the attached images.
2. COMPARE them against the Description and Rubrics above.
3. BE CRITICAL.
   - If the spec asks for "Purple Magic Theme" and you see a "Gray Box", FAIL IT.
   - If the spec asks for "Silver Frame" and you see "Nothing", FAIL IT.
   - If the image looks like a placeholder, FAIL IT.

[OUTPUT FORMAT]
Reply with a SINGLE JSON block:
{{
  "status": "approved" OR "rejected",
  "feedback": "Step-by-step analysis against rubric: ..."
}}
"""

    # 4. 转换路径 & 执行
    win_patch = wsl_path_to_windows(patch_path)
    win_shots = [wsl_path_to_windows(s) for s in screenshots]
    files_to_attach = [win_patch] + win_shots  # Manifest 暂时不重要，重要的是图和Patch

    if IS_WSL:
        cmd = ["cmd.exe", "/c", CLAUDE_BIN_FULL] + files_to_attach
    else:
        cmd = [CLAUDE_BIN_FULL] + files_to_attach

    env = os.environ.copy()
    env["NO_COLOR"] = "1"

    print("\n>>> Asking Claude to review against SPEC... <<<")

    try:
        p = subprocess.Popen(
            cmd,
            stdin=subprocess.PIPE,
            stdout=sys.stdout,  # 直接显示结果
            stderr=sys.stderr,
            text=True, encoding='utf-8', errors='replace', env=env
        )

        # 写入 Prompt
        if p.stdin:
            p.stdin.write(prompt_text)
            p.stdin.close()

        p.wait()

    except Exception as e:
        print(f"❌ Execution Failed: {e}")


if __name__ == "__main__":
    drive_reviewer_claude()
