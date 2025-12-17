import wexpect
import re
import sys


class UnityAgent:
    def __init__(self, timeout=120):
        self.timeout = timeout

    def run(self, instruction):
        """
        向 Unity 发送自然语言指令并等待结果
        """
        print(f"\n🤖 [Agent] 收到指令: {instruction}")
        command = f'cmd /c codex "{instruction}"'

        try:
            # 启动并等待
            child = wexpect.spawn(command)
            # 匹配 "Worked for" 代表任务完成，或者超时
            child.expect(['Worked for', wexpect.TIMEOUT], timeout=self.timeout)

            # 获取全部日志
            full_log = child.before + (child.after if isinstance(child.after, str) else "")

            # 简单解析结果
            return self._parse_log(full_log)

        except Exception as e:
            return {
                "success": False,
                "summary": f"执行期间发生严重错误: {e}",
                "raw_log": str(e)
            }

    def _parse_log(self, log):
        """
        简单清洗日志，提取有用的总结信息
        """
        summary = "未找到总结"

        # 尝试提取最后一段 AI 的总结 (通常在 'Worked for' 之前的一段话)
        # 这里做一个简单的截取逻辑：取最后 500 个字符进行展示
        clean_log = re.sub(r'\x1b\[[0-9;]*m', '', log)  # 去除颜色代码

        # 简单的成功判断逻辑
        success = True
        if "Error" in clean_log and '"type": "Error"' in clean_log:
            # 只有当 API 返回明确的 Error 类型数据时才算失败
            # 忽略 MCP 自身连接的 WebSocket 报错 (那是插件的问题)
            if "MCP-FOR-UNITY" not in clean_log:
                success = False

        return {
            "success": success,
            "summary": clean_log[-600:].strip(),  # 只取最后一部分给人看
            "raw_log": log
        }


# --- 测试代码 ---
if __name__ == "__main__":
    agent = UnityAgent()

    # 测试 1: 还是之前的测试
    print(">>> 测试 1: 检查编译状态")
    result = agent.run("刷新 Unity，检查是否有编译错误")
    print(f"结果: {'✅ 成功' if result['success'] else '❌ 失败'}")
    print("AI 回复摘要:", result['summary'])

    print("-" * 50)

    # 测试 2: 尝试让它修改场景 (高光时刻！)
    # 注意：这需要你的 Unity 打开了一个 Scene
    print(">>> 测试 2: 尝试创建物体")
    result = agent.run("在场景原点创建一个名为 'TestCube' 的 Cube")
    print(f"结果: {'✅ 成功' if result['success'] else '❌ 失败'}")
    print("AI 回复摘要:", result['summary'])
