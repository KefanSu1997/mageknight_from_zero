#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os, json, subprocess, sys, glob, time, re, threading
from pathlib import Path
from queue import Queue, Empty
from abc import ABC, abstractmethod

# ---------- Environment Config ----------
# 强制转换为 Windows 路径对象
REPO_DIR = Path(os.getenv("REPO_DIR", ".")).resolve()
if os.getenv("WORKSPACE_DIR"):
    WORKSPACE = Path(os.getenv("WORKSPACE_DIR")).resolve()
else:
    WORKSPACE = REPO_DIR / "multi-agent-workspace"

TASK_ID = os.getenv("TASK_ID", "T-000")
BRANCH = os.getenv("BRANCH", "")
BASE_ENV = os.getenv("BASE_COMMIT", "").strip()
USE_MERGE_BASE = os.getenv("USE_MERGE_BASE", "0") == "1"

MAX_ROUNDS = int(os.getenv("MAX_ROUNDS", "20"))
SP_CODEX_PATH = Path(os.getenv("SP_CODEX_PATH", str(WORKSPACE / "prompts" / "sp_codex.md")))

# Windows Binary Config
CODEX_BIN = os.getenv("CODEX_BIN", "codex")  # 假设 codex 在 PATH 中
CLAUDE_BIN = os.getenv("CLAUDE_BIN", "claude.cmd")  # Windows npm install 产生的通常是 .cmd

# Resume Configuration
RESUME_ROUND = int(os.getenv("RESUME_ROUND", "1"))
RESUME_STAGE = os.getenv("RESUME_STAGE", "code").lower()  # 'code' or 'review'

# Reviewer Config
REVIEWER_IMPL = os.getenv("REVIEWER_IMPL", "claude")  # 'claude', 'codex', etc.

# ---------- Directory Conventions ----------
RUNS_DIR = WORKSPACE / "runs" / TASK_ID
BUNDLE_DIR = RUNS_DIR / "review_bundle"
SHOT_DIR = BUNDLE_DIR / "artifacts" / "screenshots"
# [Ref Images Logic]
REF_DIR = RUNS_DIR / "references"

COMPILE_DIR = WORKSPACE / "compile"
BASE_DIR = WORKSPACE / "baselines"

TASK_SPEC = RUNS_DIR / "task_spec.json"
COMPILE_STATUS = COMPILE_DIR / "compile_status.json"
STATE_FILE = RUNS_DIR / "run_state.json"


# ---------- Utilities ----------

# [Windows Fix] 异步流读取器
def enqueue_output(out, queue):
    try:
        for line in iter(out.readline, ''):
            queue.put(line)
    except ValueError:
        pass  # Handle closed file
    out.close()


def sh(cmd, cwd=None, check=True, cap=True):
    cmd_str = " ".join(cmd) if isinstance(cmd, list) else cmd
    print(f"+ {cmd_str}")
    try:
        # Windows 上 shell=True 有助于解析某些指令，但在 subprocess 中直接调用 exe 不需要
        use_shell = isinstance(cmd, str)
        return subprocess.run(cmd, cwd=cwd, check=check, text=True, capture_output=cap, shell=use_shell,
                              encoding='utf-8')
    except subprocess.CalledProcessError as e:
        print(f"Error executing command: {e}")
        if cap: print(f"Stdout: {e.stdout}\nStderr: {e.stderr}")
        raise e


def ensure_dirs():
    (BUNDLE_DIR / "diffs").mkdir(parents=True, exist_ok=True)
    (SHOT_DIR).mkdir(parents=True, exist_ok=True)
    (REF_DIR).mkdir(parents=True, exist_ok=True)
    (COMPILE_DIR).mkdir(parents=True, exist_ok=True)
    (BASE_DIR).mkdir(parents=True, exist_ok=True)
    RUNS_DIR.mkdir(parents=True, exist_ok=True)


def git_head():
    return sh(["git", "rev-parse", "HEAD"], cwd=REPO_DIR).stdout.strip()


def checkout_branch(br):
    if not br: return
    out = sh(["git", "branch", "--list", br], cwd=REPO_DIR).stdout
    if br not in out:
        sh(["git", "checkout", "-b", br], cwd=REPO_DIR, cap=False)
    else:
        subprocess.run(["git", "checkout", br], cwd=REPO_DIR, capture_output=True, text=True)


def detect_base_commit(initial_head):
    if BASE_ENV: return BASE_ENV, "env:BASE_COMMIT"
    base_file = BASE_DIR / f"{BRANCH or 'feat-auto'}.txt"
    if base_file.exists(): return base_file.read_text().strip(), f"file: {base_file}"
    if USE_MERGE_BASE:
        try:
            mb = sh(["git", "merge-base", "origin/main", "HEAD"], cwd=REPO_DIR).stdout.strip()
            if mb: return mb, "merge-base"
        except:
            pass
    return initial_head, "initial HEAD"


def persist_baseline(head):
    if not BRANCH: return
    (BASE_DIR / f"{BRANCH}.txt").write_text(head, encoding="utf-8")


def make_patch(base, head):
    patch_rel = "diffs/0001-changes.patch"
    patch_path = BUNDLE_DIR / patch_rel
    print(f"+ [Stream] git format-patch {base}..{head} > {patch_rel}")
    with open(patch_path, "wb") as f:
        try:
            cmd = ["git", "format-patch", "--stdout", "--binary", f"{base}..{head}"]
            subprocess.run(cmd, cwd=REPO_DIR, stdout=f, stderr=subprocess.PIPE, check=True)
        except subprocess.CalledProcessError:
            print("   [Warn] format-patch failed, trying git diff...")
            f.seek(0);
            f.truncate()
            cmd = ["git", "diff", "--binary", f"{base}..{head}"]
            subprocess.run(cmd, cwd=REPO_DIR, stdout=f, stderr=subprocess.PIPE, check=False)
    return patch_rel


def extract_json_block(text):
    match = re.search(r'```json\s*(\{.*?\})\s*```', text, re.DOTALL)
    if match: return match.group(1)
    match = re.search(r'\{.*\}', text, re.DOTALL)
    if match:
        try:
            candidate = match.group(0);
            json.loads(candidate);
            return candidate
        except:
            pass
    return None


def load_state():
    if STATE_FILE.exists():
        try:
            return json.loads(STATE_FILE.read_text("utf-8"))
        except:
            pass
    return {"current_round": 0, "prev_feedback": "", "last_agent_summary": ""}


def save_state(state):
    STATE_FILE.write_text(json.dumps(state, indent=2), encoding="utf-8")


# ---------- Modular Reviewer System ----------

class BaseReviewer(ABC):
    def __init__(self):
        self.spec_data = {}
        if TASK_SPEC.exists():
            try:
                self.spec_data = json.loads(TASK_SPEC.read_text("utf-8"))
            except:
                pass

    @abstractmethod
    def review(self, round_idx, patch_rel_path, screenshots, references) -> (str, str):
        """Returns (status, feedback)"""
        pass

    def _build_strict_prompt(self, screenshot_names, reference_names, context_text=""):
        title = self.spec_data.get("title", "Task")
        description = self.spec_data.get("description", "")
        rubrics = self.spec_data.get("acceptance", {}).get("rubrics_checklist", [])

        rubrics_text = "\n[CRITICAL REQUIREMENTS - STRICT CHECK]\n"
        for r in rubrics: rubrics_text += f"- {r.get('text')}\n"

        visual_instruction = ""
        if reference_names:
            visual_instruction = f"""
[VISUAL COMPARISON REQUIRED]
You have been provided with REFERENCE IMAGES (Target Look) and IMPLEMENTATION SCREENSHOTS (Current State).
Reference Images: {', '.join(reference_names)}
Implementation Screenshots: {', '.join(screenshot_names)}

COMPARE THEM PIXEL-PERFECTLY:
1. Layout: Are elements positioned exactly as in the Reference?
2. Styling: Fonts, colors, borders, shadows match the Reference?
3. Content: Is any dummy text used where specific text was in the Reference?
"""
        else:
            visual_instruction = "[NOTE] No reference images provided. Judge based on UI/UX best practices and Rubrics."

        return f"""
[ROLE]
You are a Staff Engineer and Lead Art Director.
You are conducting a FINAL QA. You are EXTREMELY STRICT.
Your goal is to catch regressions and visual mismatches.

[CONTEXT]
Task: {title}
{description}
{rubrics_text}

{visual_instruction}

[INSTRUCTIONS]
Review the provided patch (diff) and images.
If ANY requirement is slightly off, or if the implementation looks different from the reference, REJECT it.
Do not say "Looks good" if it is barely acceptable. It must be PERFECT.

[ADDITIONAL CONTEXT]
{context_text}

[OUTPUT FORMAT]
JSON only:
{{
  "status": "approved" | "rejected",
  "feedback": "Specific, actionable points. List exactly what differs from the reference or requirements."
}}
"""


class ClaudeReviewer(BaseReviewer):
    def review(self, round_idx, patch_rel_path, screenshots, references):
        print(f"[Reviewer::Claude] processing {len(screenshots)} shots + {len(references)} refs...")
        patch_full = (BUNDLE_DIR / patch_rel_path).resolve()

        # Windows: Files MUST be absolute native paths
        win_patch = str(patch_full)
        win_shots = [str(Path(s).resolve()) for s in screenshots]
        win_refs = [str(Path(r).resolve()) for r in references]

        files = [win_patch] + win_refs + win_shots
        shot_names = [Path(s).name for s in screenshots]
        ref_names = [Path(r).name for r in references]

        prompt = self._build_strict_prompt(shot_names, ref_names)

        # Windows: Claude usually invoked via claude.cmd or shell=True
        cmd = [CLAUDE_BIN] + files

        env = os.environ.copy()
        env["NO_COLOR"] = "1"
        env["CI"] = "true"

        try:
            # shell=True required for .cmd execution if strict path not used
            res = subprocess.run(cmd, input=prompt, cwd=str(REPO_DIR), capture_output=True, text=True, timeout=1800,
                                 env=env, shell=True, encoding='utf-8')
        except Exception as e:
            return "error", f"Exec failed: {e}"

        return self._parse_output(res.stdout, round_idx)

    def _parse_output(self, stdout, round_idx):
        (RUNS_DIR / f"reviewer_round-{round_idx}.log").write_text(stdout, encoding="utf-8")
        clean = stdout.strip()
        json_str = extract_json_block(clean)

        if not json_str:
            if "approved" in clean.lower() and "rejected" not in clean.lower():
                return "approved", "Implicit Approval (Strict Mode Warn: JSON missing)"
            return "error", "JSON missing from Reviewer output"

        try:
            d = json.loads(json_str)
            return d.get("status", "error").lower(), d.get("feedback", "")
        except:
            return "error", "Invalid JSON"


class CodexReviewer(BaseReviewer):
    def review(self, round_idx, patch_rel_path, screenshots, references):
        print(f"[Reviewer::Codex] processing patch + {len(references)} refs + {len(screenshots)} shots...")

        # 1. Read Patch
        patch_full = (BUNDLE_DIR / patch_rel_path).resolve()
        try:
            patch_content = patch_full.read_text("utf-8")
        except:
            patch_content = "[Binary or Large Diff]"

        # 2. Build Prompt
        shot_names = [Path(s).name for s in screenshots]
        ref_names = [Path(r).name for r in references]

        # [Visual Context Injection]
        visual_context = "\n[IMAGE FILES TO ANALYZE]\n"
        if references:
            visual_context += "--- Reference Images (Target Goal) ---\n"
            for ref in references:
                visual_context += f"{ref}\n"

        if screenshots:
            visual_context += "\n--- Implementation Screenshots (Current State) ---\n"
            for shot in screenshots:
                visual_context += f"{shot}\n"

        prompt = self._build_strict_prompt(
            shot_names,
            ref_names,
            context_text=f"\n[GIT DIFF]\n```diff\n{patch_content[:50000]}\n```" + visual_context
        )

        # 3. Call Codex
        cmd = [
            CODEX_BIN, "exec",
            "--dangerously-bypass-approvals-and-sandbox",
            "--model", "gpt-5.1-codex-max",
            "--json",
            "-"
        ]

        try:
            # No shell=True needed for direct exe call generally
            res = subprocess.run(cmd, input=prompt, cwd=str(REPO_DIR), capture_output=True, text=True, timeout=1800,
                                 encoding='utf-8')
        except Exception as e:
            return "error", f"Codex Exec failed: {e}"

        return self._parse_output(res.stdout, res.stderr, round_idx)

    def _parse_output(self, stdout, stderr, round_idx):
        log_file = RUNS_DIR / f"reviewer_round-{round_idx}.log"
        debug_content = f"=== STDOUT ===\n{stdout}\n\n=== STDERR ===\n{stderr}\n"
        log_file.write_text(debug_content, encoding="utf-8")

        target_text = ""
        lines = stdout.strip().splitlines()
        found_message = False

        for line in lines:
            try:
                event = json.loads(line)
                if event.get("type") == "item.completed":
                    item = event.get("item", {})
                    if item.get("type") == "agent_message" and "text" in item:
                        target_text = item["text"]
                        found_message = True
            except json.JSONDecodeError:
                continue

        if not found_message:
            target_text = stdout

        clean_text = target_text.strip()
        json_str = extract_json_block(clean_text)

        if not json_str:
            print(f"   [Debug] Codex Output (Extracted): {clean_text[:200]}...")
            return "error", f"JSON missing in response. Check log: {log_file.name}."

        try:
            d = json.loads(json_str)
            return d.get("status", "error").lower(), d.get("feedback", "")
        except:
            return "error", "Invalid JSON content inside message"


def get_reviewer(impl_name):
    if impl_name == "codex":
        return CodexReviewer()
    return ClaudeReviewer()


# ---------- Code Generation Logic ----------

def _clean_env_for_child(parent_env: dict) -> dict:
    env = dict(parent_env)
    # Remove proxy settings that might interfere
    for k in [k for k in env if "PROXY" in k.upper()]: env.pop(k, None)
    # Force UTF-8 encoding for python subprocesses
    env["PYTHONIOENCODING"] = "utf-8"
    env.update({"TERM": "dumb", "CI": "1", "NO_COLOR": "1", "CODEX_NO_INDEX": "1"})
    return env


def _render_codex_sp(sp_path, task_spec_path, branch, compile_status_path, shots_dir, feedback="", agent_summary="",
                     references=None):
    text = Path(sp_path).read_text(encoding="utf-8")

    # [FIX] Placeholder Naming Matching
    feedback_block = f"\n\n[PREVIOUS FEEDBACK]\n{feedback}\n" if feedback else ""
    summary_block = f"\n\n[PREVIOUS WORK SUMMARY]\n{agent_summary}\n" if agent_summary else ""

    ref_block = ""
    if references:
        ref_block += "\n\n[REFERENCE IMAGES PROVIDED]\n"
        ref_block += "The user has provided reference images (Target Design). Please look at these files:\n"
        ref_block += "\n".join([str(r) for r in references])
        ref_block += "\n"

    # 执行替换
    rendered = (text
                .replace("{TASK_SPEC_PATH}", str(task_spec_path))
                .replace("{BRANCH}", branch)
                .replace("{COMPILE_STATUS_PATH}", str(compile_status_path))
                .replace("{SCREENSHOTS_DIR}", str(shots_dir))
                + summary_block
                + feedback_block
                + ref_block
                )
    return rendered


def drive_codex_exec(round_idx, feedback="", agent_summary="", references=None):
    if not TASK_SPEC.exists(): return 2
    spec = json.loads(TASK_SPEC.read_text("utf-8"))
    br = spec.get("work_branch", BRANCH or "feat/auto")
    out_txt = RUNS_DIR / f"codex_{round_idx}.txt"

    # 1. 准备参数
    # 在 Windows 上，即使是 .cmd 文件，显式调用 cmd /c 往往最稳健
    if os.name == 'nt' and "cmd" in CODEX_BIN.lower():
        args = ["cmd", "/c", CODEX_BIN]
    else:
        args = [CODEX_BIN]

    args += ["exec", "--cd", str(REPO_DIR)]
    if os.getenv("CODEX_PROFILE"): args += ["--profile", os.getenv("CODEX_PROFILE")]
    # 使用 full-auto 或 yolo 模式
    args += ["--dangerously-bypass-approvals-and-sandbox" if os.getenv("CODEX_YOLO") == "1" else "--full-auto"]
    # 关键：告诉 Codex 从 stdin (-) 读取指令
    args += ["--json", "--output-last-message", str(out_txt), "-"]

    # 2. 准备 Prompt 内容
    input_prompt = _render_codex_sp(SP_CODEX_PATH, TASK_SPEC, br, COMPILE_STATUS, SHOT_DIR, feedback, agent_summary,
                                    references)
    print(f"[INFO] Spawning Codex (Coder)...")

    # 3. [关键修复] 将 Prompt 写入临时文件，而不是通过 pipe.write
    # 这避免了 Windows Broken Pipe 问题，也绕过了命令行长度限制
    prompt_file = RUNS_DIR / f"codex_prompt_{round_idx}.temp.md"
    prompt_file.write_text(input_prompt, encoding="utf-8")

    # 用于捕获输出的文件
    out_jsonl = RUNS_DIR / f"codex_{round_idx}.jsonl"
    err_log = RUNS_DIR / f"codex_{round_idx}.stderr.log"

    print(f"   [Debug] Input file: {prompt_file.name}")
    print(f"   [Debug] Command: {' '.join(args)}")

    return_code = 1

    try:
        # 打开文件句柄
        with open(prompt_file, "r", encoding="utf-8") as f_in, \
                open(out_jsonl, "w", encoding="utf-8") as f_out, \
                open(err_log, "w", encoding="utf-8") as f_err:

            # Popen 调用：
            # stdin=f_in : 操作系统直接把文件内容喂给 Codex，极其稳定
            # stdout=f_out : 直接流式写入文件，不经过 Python 变量
            p = subprocess.Popen(
                args,
                cwd=str(REPO_DIR),
                stdin=f_in,  # <--- 修复核心：文件重定向
                stdout=f_out,
                stderr=f_err,
                env=_clean_env_for_child(os.environ),
                text=True,  # 文本模式
                encoding='utf-8'  # 强制 utf-8
            )

            # 等待结束，设置超时
            timeout = int(os.getenv("CODEX_TIMEOUT_SEC", "18000"))
            try:
                p.wait(timeout=timeout)
                return_code = p.returncode
            except subprocess.TimeoutExpired:
                print("   [Warn] Codex Timeout! Killing process...")
                p.kill()
                f_err.write("\n\n[SYSTEM] TIMEOUT KILLED\n")

    except Exception as e:
        print(f"   [Error] Subprocess execution failed: {e}")
        return 1, ""

    # 读取生成的 Summary
    summary = out_txt.read_text("utf-8").strip() if out_txt.exists() else ""

    if return_code != 0:
        print(f"   [Error] Codex exited with code {return_code}. See {err_log.name}")

    return return_code, summary


def autogate(task_spec):
    # 1. Check Compile Status File
    raw = task_spec["acceptance"].get("compile_ok_path", str(COMPILE_STATUS))
    target = Path(raw) if Path(raw).is_absolute() else REPO_DIR / raw

    print(f"   [AutoGate] Checking Compile Status at: {target.name}")
    if not target.exists():
        print(f"   [AutoGate] FAILED: Status file not found.")
        return False

    try:
        data = json.loads(target.read_text("utf-8"))
        if not data.get("ok", False):
            print(f"   [AutoGate] FAILED: Status file says 'ok': false.")
            return False
    except Exception as e:
        print(f"   [AutoGate] FAILED: Invalid JSON or read error ({e}).")
        return False
    # 2. Check Screenshots
    req_shots = task_spec["acceptance"].get("required_screenshots", [])
    if req_shots:
        print(f"   [AutoGate] Checking {len(req_shots)} required screenshot patterns...")
        for r in req_shots:
            pattern = r["pattern"]
            matches = glob.glob(str(SHOT_DIR / pattern))
            if not matches:
                print(f"   [AutoGate] FAILED: Missing screenshot matching: {pattern}")
                return False

    print("   [AutoGate] PASSED.")
    return True


# ---------- Main Loop ----------
def main():
    ensure_dirs()
    if not TASK_SPEC.exists():
        draft = RUNS_DIR / "_draft" / "APPROVED.task_spec.json"
        if draft.exists():
            import shutil;
            shutil.copy(draft, TASK_SPEC)
        else:
            print("❌ No approved task spec found (and no draft to fallback).")
            return 2

    state = load_state()
    spec = json.loads(TASK_SPEC.read_text("utf-8"))

    checkout_branch(spec.get("work_branch", BRANCH or "feat/auto"))
    base, _ = detect_base_commit(git_head())

    # Instantiate Reviewer
    reviewer = get_reviewer(REVIEWER_IMPL)
    print(f"=== Pipeline Start: {TASK_ID} ===")
    print(f"    Reviewer Strategy: {REVIEWER_IMPL.upper()}")

    prev_feedback = state.get("prev_feedback", "")
    agent_summary = state.get("last_agent_summary", "")

    def get_references():
        ref_patterns = ["*.png", "*.PNG", "*.jpg", "*.JPG", "*.jpeg", "*.JPEG"]
        r_files = []
        for pat in ref_patterns:
            r_files.extend(glob.glob(str(REF_DIR / pat)))
        return r_files

    refs = get_references()
    if refs:
        print(f"   [Context] Found {len(refs)} reference images.")
    else:
        print(f"   [Context] Warning: No reference images found in {REF_DIR.name}")

    for i in range(RESUME_ROUND, MAX_ROUNDS + 1):
        state["current_round"] = i
        save_state(state)
        print(f"\n>>> Round {i}/{MAX_ROUNDS}")

        # Refresh refs in case user added them mid-run
        refs = get_references()
        if COMPILE_STATUS.exists():
            try:
                os.remove(COMPILE_STATUS)
                print("   [Setup] Cleared stale compile_status.json.")
            except:
                pass

        # 1. Code
        if not (i == RESUME_ROUND and RESUME_STAGE == "review"):
            # [FIX] Passing refs to Coder
            ret, agent_summary = drive_codex_exec(i, prev_feedback, agent_summary, refs)
            if ret != 0:
                print("❌ Codex failed")
                return ret
            state["last_agent_summary"] = agent_summary
            save_state(state)

        # 2. Artifacts
        head = git_head()
        patch_rel = make_patch(base, head)
        shots = glob.glob(str(SHOT_DIR / "*.*"))

        # 3. AutoGate
        if not autogate(spec):
            prev_feedback = "System: Compile failed or missing screenshots."
            state["prev_feedback"] = prev_feedback
            save_state(state)
            continue

        # 4. Review
        print(f"   [Reviewer] Using {type(reviewer).__name__}...")
        decision, feedback = reviewer.review(i, patch_rel, shots, refs)
        print(f"   [Reviewer] {decision.upper()}: {feedback}")

        if decision == "approved":
            persist_baseline(head)
            state["status"] = "success"
            save_state(state)
            print("✅ SUCCEEDED")
            return 0

        prev_feedback = f"Reviewer ({REVIEWER_IMPL}) Rejected: {feedback}"
        state["prev_feedback"] = prev_feedback
        save_state(state)

    print("❌ FAILURE: Max rounds reached.")
    return 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("\n[Stop] Interrupted by user.")
        sys.exit(130)
