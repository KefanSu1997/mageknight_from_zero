# WSL 调用 Windows Unity 的路径与 DeckIdeal 截图流水线

- 使用 WSL 直接调用 Windows 版 Unity.exe 时，`-projectPath` 必须提供 Windows 风格路径（例如 `D:\study and work\code\unity-MK-test\MageKnight_from_zero`），避免自动拼接导致 `D:/...//mnt/d/...` 失效。
- `DeckIdealSceneBuilder.BuildAndCaptureMenu` 可在 batchmode 下重建 `Part1_DeckIdeal.unity` 并输出 `multi-agent-workspace/review_bundle/artifacts/screenshots/part1_deck_ideal_001.png`，无需 SceneAutomation 步骤即可满足理想卡组截图验收。
- 运行后检查 `multi-agent-workspace/review_bundle/deck_ideal_build.log`，只要无 `error CS`/`Exception` 便可将 `multi-agent-workspace/compile/compile_status.json` 标记为 `ok=true` 并指向该日志。
