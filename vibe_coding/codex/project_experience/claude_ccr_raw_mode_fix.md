# Claude CCR Raw Mode 报错处理记录

## 背景
- 在 Windows 上通过 `start_task_conversation.ps1` 自动拉起 CCR（Claude Code Router）时，Ink UI 反复抛出 `Raw mode is not supported on the current process.stdin`。
- 触发点：脚本使用 `wt.exe new-tab` + `pwsh -File` 启动 `_ccr_launcher.ps1`，导致子进程继承到的 stdin 不是 TTY。

## 根因分析
- npm 安装的 CCR 同时提供 `ccr.cmd` 与 `ccr.ps1`，但通过 `-File` 执行 `.ps1` 会让 PowerShell 把 stdin 绑定到文件流，Ink 检测 `process.stdin.isTTY == false` 时立刻报错。
- 另外，`wsl mkdir` 在包含空格的 Windows 路径上会被拆参，后续 `proposal.json` 无法写入，导致工作区初始化不完整。

## 解决方案
1. **改用 `-Command` 保持 TTY**
   - `wt.exe new-tab -d <Repo> powershell.exe -NoExit -Command "Set-Location ...; & 'ccr.cmd' code"`。
   - 优先调用 `ccr.cmd`，确保走 npm shim，保留交互控制台。
2. **目录初始化切到 Windows 侧**
   - 使用 `New-Item -ItemType Directory -Force` 连续创建带空格的路径，避免 `wsl mkdir` 拆参。
3. **清理 FileSystemWatcher**
   - 缓存订阅句柄并逐个 `Unregister-Event`，最后显式 `Dispose`，免得 CCR 终止后残留监听占用。

## 验证步骤
1. 在普通 PowerShell 中执行 `ccr code`，确认环境自带没有问题。
2. 运行更新后的 `start_task_conversation.ps1`，观察新开的 Windows Terminal 标签页：
   - CCR UI 正常渲染；
   - 不再出现 Raw mode 报错。
3. 检查 `_draft` 目录与 proposal 文件已正确创建。

## 后续建议
- 如需在 WSL 中访问 Windows 代理，可在 `.wslconfig` 启用 `networkingMode=mirrored` + `autoProxy=true`，消除 “未镜像 localhost 代理” 警告。
- 若未来改用其他终端（如 ConEmu），需重新确认其 `-Command` 是否同样保持 TTY。
- wt.exe 参数需在子命令前加 -- 做分隔，避免 ';' 被解析为终端命令导致 0x80070002。
