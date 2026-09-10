# 四元素场景与可靠制作流程，2026-09-08

## 目标与边界
- 沿用四元素卡牌展示任务，新增独立场景与完整画面验收，不覆盖旧场景。
- 不安装/卸载 Unity packages，不通过命令行启动 Unity，不使用 batchmode。
- 本地文件任务是主要执行入口，Unity MCP 的 Console handler 通过本地适配器调用；窗口操作用于恢复和独立人工式验证。

## 基线保全
- 原分支 feat/part1-ideal-deck-ui，原 HEAD dcbecfe44fd26fe4c7880880b54bda34b7c5d8ae。
- 原 3 份已修改文件与 330 份未跟踪项目文件备份到 D:/study_and_work/multi_agent_system/recovery_backups/mageknight_20260908。
- tracked baseline 另留 git stash；应用时保留 stash。
- git fetch origin 后从 origin/main 创建 feat/resilient-element-scene，合入旧功能分支以保留历史开发成果；原有修改恢复到工作区。main 长期未同步，PR 须明确区分历史基线与本轮修改。

## 实施计划
1. Seedream 单任务状态机：先持久化再提交；超时不自动重提；查询与下载独立重试；保存真实图片尺寸、SHA256 和提示词。
2. Unity 本地文件任务：状态/心跳、请求 ID、过期与超时、域重载恢复、Console 双扫描。
3. 独立四元素场景：统一背景、层级清楚的卡牌展示、可点击翻面与批量切换。
4. 全画面自动化：验证点击射线与状态变化，保存未裁切截图和可审查报告；视觉检查之后单独记录结论。

## 当前进展
- Unity Hub 已打开，目前显示登录页，已请用户完成身份验证并打开项目；其间继续独立工具工作。
- Seedream 凭据存在，仅检查 presence，未输出凭据。
- 开始实现可恢复生图驱动和独立场景背景规格。

## 验证状态
待实际 Unity Console 与场景运行结果；不可将静态检查写为编译通过。

## 安装等待与空间清理更新
- 用户已登录 Hub，正在安装 Unity 6.6 (6000.6.0f1)；界面最后观察 Editor application 下载 29%。
- 原指定版本 D:/Unity/Editor/2023.2.20f1c1/Editor/Unity.exe 已确认存在。后续用原版本打开工程，避免隐式版本迁移。
- 用户明确授权清理文明 VI zip 与 5 份旧日志；已核对6个完整路径均为普通文件，合计逻辑大小约39.38 GiB。
- 两次删除命令在执行前被自动审批/执行策略拒绝：blocked by policy。没有删除任何文件，不通过其他工具绕过限制。
- 文件显示日志具有 NTFS 压缩属性，因此逻辑大小不等于可释放的实际磁盘空间。
- 当前工作区仍在 merge 中，新增 Seedream 工具与背景规格尚未运行验证；Unity尚未打开，不可称编译通过。
- 用户说明不再依赖旧的外部 multi-agent system，后续由当前任务直接协调工具执行。
