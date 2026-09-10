#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os, json, subprocess, re, sys
from pathlib import Path

# ================= 配置区域 =================
TASK_ID = "T-20251028-002"
REPO_DIR = Path(os.getcwd()).resolve()
WORKSPACE = REPO_DIR / "multi-agent-workspace"
PATCH_REL_PATH = "diffs/0001-changes.patch"
# ===========================================

BUNDLE_DIR = WORKSPACE / "review_bundle"


def wsl_path_to_windows(linux_path):
    try:
        result = subprocess.run(["wslpath", "-w", str(linux_path)], capture_output=True, text=True)
        return result.stdout.strip()
    except:
        return str(linux_path)


def extract_json_block(text):
    if not text: return None
    match = re.search(r'```json\s*(\{.*?\})\s*```', text, re.DOTALL)
    if match: return match.group(1)
    match = re.search(r'\{.*\}', text, re.DOTALL)
    if match: return match.group(0)
    return None


def test_reviewer():
    print(f"=== Debugging Reviewer for {TASK_ID} ===")

    patch_full_wsl = (BUNDLE_DIR / PATCH_REL_PATH).resolve()
    manifest_full_wsl = (BUNDLE_DIR / "manifest.json").resolve()

    if not patch_full_wsl.exists():
        print(f"[Error] Patch file missing.")
        return

    patch_win_path = wsl_path_to_windows(patch_full_wsl)
    manifest_win_path = wsl_path_to_windows(manifest_full_wsl)

    # 构造 Prompt
    prompt = f"""
You are the Lead Code Reviewer.
Step 1: Read the artifact manifest at: {manifest_win_path}
Step 2: Read the git diff/patch at: {patch_win_path}
Step 3: Analyze the changes.
Step 4: OUTPUT DECISION as a JSON block:
{{ "status": "approved" | "rejected", "feedback": "..." }}
"""

    # 【关键修改】仅仅调用 claude，不带 -p 参数
    # claude 检测到 stdin 有数据时，会自动将其作为 prompt
    cmd = ["cmd.exe", "/c", "claude"]

    env = os.environ.copy()
    env["NO_COLOR"] = "1"
    env.pop("TERM", None)

    print("\n[Action] Invoking Claude via STDIN Pipe...")

    try:
        # 【关键修改】使用 input=prompt 通过管道传送数据
        result = subprocess.run(
            cmd,
            input=prompt,  # 数据从这里进去，安全避开 cmd.exe 参数限制
            cwd=str(REPO_DIR),
            capture_output=True,  # 再次启用捕获，因为我们不需要交互了
            text=True,
            encoding='utf-8',
            errors='replace',
            env=env,
            timeout=120
        )
    except Exception as e:
        print(f"[Fatal Error] {e}")
        return

    print(f"[Finished] Return Code: {result.returncode}")

    # 安全的打印输出
    stdout_str = result.stdout if result.stdout else ""
    stderr_str = result.stderr if result.stderr else ""

    if result.returncode != 0:
        print("--- STDERR ---")
        print(stderr_str)
        print("--- STDOUT (Partial) ---")
        print(stdout_str[:500])
    else:
        print("\n[Success] Raw Output snippet:")
        print(stdout_str[:200] + "...")

        print("\n[Parsing JSON...]")
        json_str = extract_json_block(stdout_str)
        if json_str:
            try:
                print(json.dumps(json.loads(json_str), indent=2, ensure_ascii=False))
            except:
                print("[Error] Invalid JSON content.")
        else:
            print("[Error] No JSON block found in Claude's response.")
            print("Full response:\n", stdout_str)


if __name__ == "__main__":
    test_reviewer()
