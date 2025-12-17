## 现象

- 执行 `git diff` / `git add` 等需要读写 `.git/index` 或处理 LFS clean filter 的命令时失败：
  - `fatal: Unable to create .../.git/index.lock: Permission denied`
  - `cannot fork to run subprocess 'git-lfs filter-process'` / `clean filter 'lfs' failed`

## 影响

- 无法提供 reviewer 要求的 `git diff`、无法按规范做 commit / push / PR。
- 容易被误判为“未按要求修改/无可核验交付物”。

## 绕行策略（本仓库 agent 运行环境）

- 对任务交付的“可回归验证”优先使用 Unity 侧产物与 MCP 流程：
  - `Tools/Scene Builders/Build + Capture ...` 生成截图
  - `Tools/Scene Automation/...` 生成 report + captures
  - `read_console types=["error"]` 确认编译错误为 0
- 在 LFS/权限未修复前，避免新增/修改 `.png/.jpg` 之类受 LFS 管控的文件；只改 C# / JSON / md。

## 根因排查建议（给人类）

- 检查是否有其它进程占用/锁定 `.git/index`（IDE、杀毒、同步盘等）。
- 确认 `git-lfs` 可执行与 filter-process 可运行：
  - `git lfs version`
  - `git lfs env`
- 若仓库位于受控目录（如 OneDrive / 企业加密盘），建议迁移到普通磁盘路径或为目录加白名单。

