# SceneAutomation 按钮路径漂移排查记录

## 背景
- Part1 场景的 UI 架构从 `Part1TestLayout/ControlColumn` 迁移到 `Part1TableRoot/ActionColumnShell/ActionColumnRoot`。
- `SceneAutomationQuickMenus` 和协作者文档仍引用旧路径，导致调试菜单无法定位按钮，被误判为“测试内容缺失”。

## 处理过程
1. 复核 `SceneAutomation` 运行时日志，确认自动化在新路径下仍能找到按钮，问题仅出现在快捷菜单与文档。
2. 更新 `SceneAutomationQuickMenus` 常量为新层级 (`Canvas/Part1TableRoot/ActionColumnShell/ActionColumnRoot/...`)。
3. 同步 `AGENTS.md`、`CLAUDE.md` 以及 combat 场景经验笔记中的路径描述，避免后续误导。
4. 重新执行 DeckMana 自动化，验证按钮定位与截图流程完整，Unity Console 无新增错误。

## 经验
- 当 UI 层级调整时，除自动化配置 JSON 外，还要同步检查调试脚本与文档是否引用旧路径。
- 优先通过 `Tools/Scene Automation/Debug/List Runtime Buttons` 输出当前实际路径，再更新常量与说明。
- 保持路径命名规范（`ActionColumnRoot/Buttons/...`）有助于跨场景共用同一配置与脚本。

