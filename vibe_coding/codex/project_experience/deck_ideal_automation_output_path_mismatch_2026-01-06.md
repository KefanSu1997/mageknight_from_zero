# Deck Ideal 自动化输出路径差异排查（2026-01-06）

## 现象
- 通过菜单 `Tools/Scene Automation/Run DeckIdeal Automation` 运行后，Editor.log 显示自动化成功，但输出落盘在旧 run 目录（例如 T-20251028-009），而当前任务需要 T-20251028-012。
- Unity Console 未显示 SceneAutomation 相关日志，需要从 Editor.log 查证运行过程与步骤数。

## 处理方式
- 先读取 Editor.log，确认自动化流程与步骤执行成功。
- 检查生成的 `deck_ideal_report.json` 与 captures 截图所在目录。
- 将报告与截图复制到当前 run 的输出目录，并修正 report 内 `screenshotPath` 指向当前目录。

## 建议
- 运行前确认 `scene_automation_deckideal.json` 中 `reportPath` / `screenshotsDirectory` 指向当前 run。
- 运行后核对目标目录时间戳，避免误用历史 run 的输出。
- 若发现路径不一致，可使用脚本替换 report 内路径，保证证据链一致。
