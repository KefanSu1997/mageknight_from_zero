import socket
import os
import re

# === 配置 ===
TARGET_PORT = 18090
TIMEOUT = 3  # 秒

def get_host_ip():
    """尝试从 /etc/resolv.conf 获取宿主机 IP"""
    try:
        with open('/etc/resolv.conf', 'r') as f:
            for line in f:
                if 'nameserver' in line:
                    return line.split()[1]
    except:
        pass
    return None

def check_connection(ip, port, label):
    print(f"[-] 测试 {label} ({ip}:{port})... ", end="", flush=True)
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(TIMEOUT)
    try:
        result = s.connect_ex((ip, port))
        if result == 0:
            print("✅ 成功连通!")
            s.close()
            return True
        else:
            print(f"❌ 失败 (错误码: {result})")
            s.close()
            return False
    except Exception as e:
        print(f"❌ 异常: {e}")
        return False

print(f"=== Unity MCP 端口连通性检查 (Port: {TARGET_PORT}) ===\n")

# 1. 测试 Localhost (依赖 WSL 的自动转发)
success_localhost = check_connection('127.0.0.1', TARGET_PORT, "Localhost")

# 2. 测试宿主机 IP
host_ip = get_host_ip()
success_host = False
if host_ip:
    success_host = check_connection(host_ip, TARGET_PORT, "WSL Host IP")
else:
    print("⚠️ 无法自动获取 Host IP，跳过测试 2")

print("\n=== 诊断结果 ===")
if success_localhost or success_host:
    print("🎉 通路正常！Unity 服务可达。")
    if not success_localhost and success_host:
        print("提示: localhost 转发似乎失效，请确保 Agent 配置使用 Host IP 连接。")
else:
    print("⛔ 彻底阻断。WSL 无法访问 Windows 上的 Unity。")
    print("可能原因：")
    print("1. Windows 防火墙拦截了 18090 端口 (请在防火墙入站规则中放行 Unity)。")
    print("2. Unity 刚刚重启，服务还没完全 Ready。")
