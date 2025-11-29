# Codex Work Log

## 2025-11-25 – RollingBall 场景验收补齐
- 复核 RollingBallDemo：确认斜坡、方块、球体与跟随相机仍在场景内，并保留 WASD 操作提示文本。
- 检查 Editor.log 中最近一次 `Build + Capture Rolling Ball` 日志，确认截图目录含 rolling_slope_001.png / rolling_block_001.png（时间戳 2025-11-25 00:30）。
- 读取 Editor.log 搜索 `error CS` 计数为 0，将摘要写入 `multi-agent-workspace/compile/compile.log`，生成 `compile_status.json` 标记 ok=true。

## 2025-10-19 – DeckMana 快捷菜单路径同步
- 任务：用户反映 Part1_DeckManaTest 仅剩 UI 外观。排查后发现 SceneAutomation 调试菜单仍指向旧的 `Part1TestLayout/ControlColumn` 路径，导致按钮定位失败并误判测试缺失。
- 操作：将 `SceneAutomationQuickMenus` 常量更新为 `Canvas/Part1TableRoot/ActionColumnShell/ActionColumnRoot/...`，并同步 `AGENTS.md`、`CLAUDE.md` 与 combat 经验文档中的按钮路径说明。
- 校验：通过 Unity 菜单重跑 DeckMana 自动化，Console 输出 0 条编译错误且步骤成功；`Test Deck Button Path` 现可正确找到按钮。
- 后续：提醒团队在调整 UI 层级时同步更新文档与调试脚本，避免再次出现路径漂移。

## 2025-10-19 – Part1 table perspective refactor
- Goal: refresh Part1 deck UI table with themed shell, fix button clipping, switch camera to perspective as per latest checklist.
- Actions:
  - Rebuilt table scaffolding in `DeckUiTableBuilder` with shells for deck/discard pedestals, info dock, hand rail, and decorative card stacks.
  - Updated `Part1SceneHarness` to theme new anchors, auto-size test buttons, enhance hand slot glow, and drive stack visuals via theme cache.
  - Introduced dedicated Deck UI camera (screen-space camera mode) for angled presentation.
- Status: implementation complete; pending validation via Unity compile check and documentation updates.
- Next: keep an eye on automation configs once new layout stabilises; consider prefab extraction later.

## 2025-10-19 – Automation 路径修复 & CLI 校验
- 调整 `AutomationConfigs/*` 的按钮路径，适配新增的 `ActionColumnShell` 层级，恢复 DeckMana/Combat/Exploration/HandDeck 自动化能力。
- 试图通过 CLI 执行 `SceneAutomationCommand` 跑通 `part1_deckmana.json`，但当前项目已被其它 Unity 进程占用，命令退出并提示无法同时开启多个实例。
- 待办：空闲时再跑一次批处理截图，确认新版透视布局对自动化流程无回归风险。

## 2025-10-20 – DeckMana 交互反馈与手牌可视化
- 新增 `DeckUiClickFlash` 组件并挂到牌组/弃牌按钮，点击时提供瞬时闪光提示，同时在副标题中加入“点击查看”提醒。
- 调整测试按钮 UI：为底板添加阴影和描边，并让图标背景使用 `DeckUiGlowPulse`，避免“测试魔力池”按钮与背景融为一体。
- `CardRuntime` 增加卡背兜底逻辑，若加载卡图失败则默认显示主题卡背，保证抽牌后能在手牌区看到实体卡片。
- 运行 `Tools/Scene Automation/Run DeckMana Automation` 验证流程成功，Unity Console 未出现新的编译错误。

## 2025-10-20 – 自动化截图与主摄像机同步
- 修改 `Part1SceneHarness.CreateCanvas`：创建 `DeckUICamera` 时沿用场景主摄像机的 post-processing、MSAA、HDR 等设置，并在需要时接管 `MainCamera` 标签。
- `SceneAutomationOrchestrator` 优先选择名称包含 `DeckUI` 的摄像机截图，确保自动化输出与场景实际视觉一致。
- 最新一次自动化输出 `AutomationOutputs/DeckManaTest/captures/Part1_DeckManaTest_20251020_010718` 涵盖 7 张截图，与手动运行时的牌桌画面一致。

## 2025-10-20 – URP 可选化编译修复
- 任务：用户请求修复缺失 URP 包导致的编译异常。
- 操作：移除 `UnityEngine.Rendering.Universal` 编译期引用，改用反射探测并复制 UniversalAdditionalCameraData 设置；新增缓存函数避免重复查找。
- 校验：执行 `Assets/Refresh` 触发重新编译，Unity Console 返回 0 条错误。
- 后续：若后续接入 URP，可直接装包并触发相同逻辑，无需恢复硬编码引用。

## 2025-10-20 – DeckMana 场景视图统一
- 用户反馈 Play 模式下仍显示旧版 DeckUiBootstrapper 产出的界面，与自动化截图不一致。
- 新增 `DisableLegacyDeckUiCanvases`，在 `Part1SceneHarness.CreateCanvas` 前禁用所有旧 Canvas，并在 `OnDestroy` 时恢复，确保运行时始终显示新版桌面布局。
- 执行 `Assets/Refresh` 复编译后 Unity Console 0 报错，后续手动运行应与自动化截图一致。

## 2025-10-20 – DeckMana 桌面可视化调优
- 调整 `DeckUiTableBuilder`：降低桌面/手牌倾角，抬高牌组与弃牌区域并用主题背景贴图填充桌面，避免被手牌遮挡并让背景贴合新透视。
- `DeckUiHandPresenter` 新增 `ForceSnapLayout`，并在 `Part1SceneHarness.BindHandRoot` 中调用，重新绑定旧手牌时立即贴合 Slot 布局，解决手牌散乱问题。
- Unity Console 重新 `Assets/Refresh` 后无编译错误；自动化菜单因编辑器处于播放态提示不可执行，稍后需在非 Play 状态下补跑截图。

## 2025-10-20 – DeckMana 顶视布局完善
- 调整 `DeckUiTableBuilder`：牌桌倾角降至 18°、手牌托盘移至屏幕底部并取消 Z 倾斜，同时关闭背景 `raycastTarget`，恢复牌组按钮的点击与闪光反馈。
- `DeckUiHandPresenter` 改为零角度扇形排布，新增 `ForceSnapLayout()` 供 `Part1SceneHarness` 在重绑 HandRoot 时即时贴槽位，保持顶部相机下的完整展示。
- 测试按钮列新增折叠开关（`Btn_ToggleActionPanel`），收起时隐藏整组测试按钮，展开时保持原有顺序；信息面板开关随折叠面板一起移动。
- 运行 `Assets/Refresh` 验证无编译错误，自动化需在非 Play 状态下执行以更新截图。

## 2025-10-30 – start_task_conversation.ps1 CCR 启动修复
- 调整 Section 4，改用 Windows Terminal wt.exe new-tab + -Command 直接执行 ccr.cmd，确保继承 TTY，避免 Ink Raw mode 报错。
- 重构工作区目录创建逻辑，改为 Windows 端逐个 New-Item，解决包含空格路径时 wsl mkdir 失效问题。
- 优化 FileSystemWatcher 注册/释放流程，使用订阅列表循环注销并显式 Dispose，防止句柄泄露。

- 兼容 Windows PowerShell 5.1，移除 `?.Source` 空条件运算符，改为显式 `Get-Command` 判空。
- 调整 wt.exe 参数加入 --，防止分号被终端解析为命令分隔符引发 0x80070002。

## 2025-11-27 – 理想卡组展示与自动截图
- 编写 `DeckIdealSceneBuilder`（Tools/Scene Builders/Build + Capture Deck Ideal），复用 DeckUiBootstrapper 紫色主题并补齐顶部立绘、底部 5 张角色卡、漂浮牌堆与高光叠层，一键生成演示场景与截图。
- 通过 batchmode 执行 BuildAndCaptureMenu，产出场景 `Assets/Scenes/Part1/Part1_DeckIdeal.unity` 与截图 `multi-agent-workspace/review_bundle/artifacts/screenshots/part1_deck_ideal_001.png`。
- 更新 review_bundle manifest 与 `compile/compile_status.json`，记录 compile.log（无编译错误），便于验收脚本读取。

## 2025-11-29 – 理想卡组截图修复与编译标记
- `DeckIdealSceneBuilder.CreateCanvas` 额外强制 `RectTransform.localScale = Vector3.one`，防止异常缩放导致截图变灰。
- 重新 batchmode 执行 BuildAndCaptureMenu，截图已写入 `multi-agent-workspace/review_bundle/artifacts/screenshots/part1_deck_ideal_001.png`，画面恢复紫色主题与牌面。
- 写入 `multi-agent-workspace/compile/compile_status.json` 指向最新 `deck_ideal_build.log`，compile.log 更新记录本次无编译错误（仅授权/字体清理提示）。
