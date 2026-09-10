# Codex 远程连接与系统代理（2026-09-09）

## 现象与证据
- 电脑端“允许你的设备控制这台电脑”提示无法启用远程控制，尚未完成手机配对。
- 后台 remote_control::websocket 日志反复出现 Windows 网络错误 10060（连接超时）与 10054（连接被重置）；设备注册信息已经存在，不能仅凭界面通用错误推断账号无权限。
- FlClash 系统代理已启用（本机端口 7890），虚拟网卡 TUN 关闭。
- 对相同远程服务地址做无凭证网络诊断：显式直连在 TLS 阶段被重置；显式经本机代理则完成 TLS 并收到 HTTP 426。426 只证明到达要求 WebSocket 升级的服务，不代表完成授权或配对。

## 处理与结果
- 用户手动打开 FlClash 虚拟网卡。
- 2026-09-09 21:22:56（Asia/Shanghai），后台日志明确记录 previous_status=Errored next_status=Connected，随后记录 connected to app-server remote control websocket。
- 开启 TUN 后，不指定显式代理的诊断也可完成 TLS。此证据支持此前远程通道没有被系统代理覆盖的判断。
- 已确认电脑到远程服务的通道恢复；手机扫码、账号验证和配对是否完成尚未由用户确认。
- 没有自动点击远程授权弹窗，没有修改安全设置或代理配置，没有重启桌面应用，没有修改 Unity 代码或 Packages。

## 后续排查
先检查后台真实连接状态，再区分设备注册、WebSocket 网络和手机配对三层。不能用普通网页能打开来推定后台 WebSocket 正常。日志只提取相关错误和状态，避免复制身份凭证与整段原始日志。

官方说明：https://learn.chatgpt.com/docs/remote-connections
