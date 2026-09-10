# 未完成合并的存档与 GitHub 连接

- 本项目隐藏未跟踪文件（`status.showUntrackedFiles=no`），必须显式使用 `git ls-files --others --exclude-standard`。仅看普通 status 会漏掉新场景、代码和美术。
- 合并前检查 MERGE_HEAD、冲突与 index tree；保存 index 和两份 diff。先保留原有历史合并，再按明确清单存档近期成果，不能 reset/clean 或盲目 add -A。
- GitHub HTTPS 公共 fetch 成功不表示有写权限；API 凭据可能已过期。现有 SSH 密钥可通过 `ssh.github.com:443` 连接，无需修改全局代理或覆盖凭据。
- Git LFS 必须同步美术与验收截图，远端分支验证同时检查实际提交哈希。PR 未创建/未合并必须如实记录，不能用本地合并冒充远端 main 完成。
