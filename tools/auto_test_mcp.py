import wexpect
import sys
import time


def run_mcp_test():
    print("🚀 [1/3] 启动 Codex，开始硬核监控模式...")

    # 指令
    instruction = "刷新一次，读取console log，再刷新一次，再检查编译状态"
    command = f'cmd /c codex "{instruction}"'

    try:
        # 启动进程
        child = wexpect.spawn(command)

        print(f"⏳ [2/3] 正在执行任务，脚本将强制等待 100 秒...")
        print("      (请耐心等待，不要关闭窗口，Unity 正在后台刷新...)")

        # 【关键修改】
        # 我们不再匹配 prompt，而是尝试匹配结果里的关键句子 "Worked for" (代表任务完成)
        # 如果 100秒内没等到这个词，就会触发 TIMEOUT，我们依然在 TIMEOUT 里提取日志
        # 这样既能提前结束，又能防止早退
        try:
            child.expect(['Worked for'], timeout=100)
            print("\n✨ 检测到任务完成标记！")
        except wexpect.TIMEOUT:
            print("\n⏰ 监控时间结束 (这很正常)，正在捕获当前缓冲区内容...")

        # 无论是因为匹配到了 'Worked for' 还是超时了，内容都在这里
        # 拼接 .before 和 .after 确保不漏掉最后一句
        raw_output = child.before + (child.after if isinstance(child.after, str) else "")

        print("\n📄 [3/3] === 完整执行日志 ===")
        print("=" * 50)
        # 这里直接打印，让你看到所有细节
        print(raw_output)
        print("=" * 50)

        # --- 智能分析结果 ---
        print("\n🔍 === 结果自动分析 ===")

        # 1. 检测 API 调用
        if "execute_menu_item" in raw_output:
            print("✅ 动作确认: 成功触发 Unity 菜单 (Refresh)")
        else:
            print("❌ 动作确认: 未检测到 Unity 菜单调用")

        # 2. 检测读取日志
        if "read_console" in raw_output:
            print("✅ 动作确认: 成功读取 Console 日志")

        # 3. 这里的关键逻辑：看是否有 Error 类型的日志被 fetch 到
        # 注意：日志里有 "types":["error"] 是发出的命令，不是结果。
        # 结果通常在 "data": [{"type": "Error" ...
        if '"type": "Error"' in raw_output or '"type":"Error"' in raw_output:
            print("⚠️ 发现潜在报错 (请检查上方日志详情)")
        else:
            print("🎉 看起来一切正常 (在获取的日志中未发现显式 Error 条目)")

    except Exception as e:
        print(f"❌ 脚本发生巨大异常: {e}")


if __name__ == "__main__":
    run_mcp_test()
