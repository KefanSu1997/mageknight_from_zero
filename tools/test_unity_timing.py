
# !/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
import subprocess
import select
import time
import shutil

# ================= 配置区 =================
CODEX_BIN = "codex"  # 确保此命令在 PATH 中

# 实验指令：加入 C# 脚本修改以触发 Domain Reload
EXPERIMENT_PROMPT = """
Task: Verify Unity MCP Stability AFTER C# Script Compilation (Domain Reload).

Please execute the following steps strictly in order:

1. **TRIGGER COMPILATION**: Create a new C# script at "Assets/TempDomainReloadTest.cs" with the following content:
   ```csharp
   using UnityEngine;
   public class TempDomainReloadTest : MonoBehaviour {
       // This file exists solely to force a recompile and domain reload.
       public void Test() { Debug.Log("Domain Reload Check"); }
   }
   ```

2. **FORCE UPDATE**: Call tool "UnityMCP.execute_menu_item" with arguments {"menu_path": "Assets/Refresh"}.


3. **VERIFY CONNECTION LOOP**: 
   Try to call "UnityMCP.read_console". 
   - If it returns data (even empty list), SUCCESS.
   - If it fails (timeout/busy), wait another 5 seconds and retry once.

Goal: Check if the connection survives the C# domain reload.
"""

LOG_FILE = "experiment_std_log.txt"


# ===========================================

def clean_env_for_child():
    """
    完全复刻 orchestra 中的环境变量清理逻辑。
    TERM=dumb 和 CI=1 是为了禁止 codex 尝试读取光标位置（解决 cursor error）。
    """
    env = os.environ.copy()
    # 移除代理
    for k in [k for k in env if "PROXY" in k.upper()]:
        env.pop(k, None)

    # 关键设置：告诉 Codex 这是非交互式环境
    env.update({
        "TERM": "dumb",  # 哑终端，禁用任何 UI 控制字符
        "CI": "1",  # 持续集成模式
        "NO_COLOR": "1",  # 禁用颜色
        "CODEX_NO_INDEX": "1"  # 禁用索引
    })
    return env


def cleanup_temp_file():
    """清理测试生成的垃圾文件，保持项目整洁"""
    file_path = os.path.join(os.getcwd(), "Assets", "TempDomainReloadTest.cs")
    file_meta_path = os.path.join(os.getcwd(), "Assets", "TempDomainReloadTest.cs.meta")
    try:
        if os.path.exists(file_path):
            os.remove(file_path)
            print(f"🧹 已清理临时文件: {file_path}")
        if os.path.exists(file_meta_path):
            os.remove(file_meta_path)
    except Exception as e:
        print(f"⚠️ 清理文件失败 (不影响测试结果): {e}")


def run_experiment_pipe():
    print(f"🚀 [Domain Reload 测试模式] 正在启动 {CODEX_BIN} ...")
    print(f"📄 目标：验证 '脚本修改 + Refresh' 是否会导致 MCP 连接断开")

    # 确保没有残留文件
    cleanup_temp_file()

    # 构建命令
    cmd = [
        CODEX_BIN,
        "exec",
        "--dangerously-bypass-approvals-and-sandbox",
        "--cd", ".",
        "-"
    ]

    try:
        p = subprocess.Popen(
            cmd,
            cwd=os.getcwd(),
            text=True,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            env=clean_env_for_child(),
            bufsize=1
        )

        print("📋 发送 Prompt 到 stdin...")
        if p.stdin:
            p.stdin.write(EXPERIMENT_PROMPT + "\n")
            p.stdin.close()

        print("⏳ 等待执行结果 (预计 30秒+)...")
        print("-" * 50)

        # 读取输出
        with open(LOG_FILE, "w", encoding="utf-8") as f_log:
            while p.poll() is None:
                r, _, _ = select.select([p.stdout, p.stderr], [], [], 0.1)
                for fh in r:
                    line = fh.readline()
                    if line:
                        print(line, end="")
                        f_log.write(line)

            # 读取剩余
            for line in p.stdout:
                print(line, end="")
                f_log.write(line)
            for line in p.stderr:
                print(line, end="")
                f_log.write(line)

        print("-" * 50)
        print(f"\n✅ 脚本执行结束。退出码: {p.returncode}")

    except FileNotFoundError:
        print(f"❌ 错误: 找不到命令 '{CODEX_BIN}'")
    except Exception as e:
        print(f"❌ 发生异常: {e}")
    finally:
        # 实验结束后尝试再次清理（可能需要 Unity 检测到删除后再刷新一次，这里手动删一下物理文件）
        cleanup_temp_file()


if __name__ == "__main__":
    run_experiment_pipe()
