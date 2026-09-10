# Unity 6.6 迁移记录（2026-09-09）

## 用户目标
- 用户明确要求解决旧版无法打开的问题，尽量切到已安装的 Unity 6000.6.0f1。
- 本轮继续在已有 feat/resilient-element-scene 分支上工作，保全尚未提交的历史合并和未跟踪文件。
- 不通过命令行调用 Unity.exe，不使用 batchmode；通过 Unity Hub 打开。

## 迁移前备份
- 完整复制 Assets、Packages、ProjectSettings、UserSettings、AutomationConfigs。
- 备份目录：D:/study_and_work/multi_agent_system/recovery_backups/mageknight_unity6_20260909_000029。
- 同时保存 AGENTS.md、Git index/MERGE_HEAD/MERGE_MSG/ORIG_HEAD、未暂存 binary diff 与包配置哈希。
- 现有 Seedream 和场景流程开发尚未验证，不能作为本轮已完成成果。

## 启动问题与处理
- Hub 已完成 Editor、WebGL Support 和 Documentation 安装。
- 用户先前尝试打开后，Logs/Editor.log 报告 Package Manager 的 projectVersion 参数无效：2023.2.20f1c1。
- 将 ProjectVersion.txt 的中国版 c1 后缀规范化为 2023.2.20f1，保留原始 revision，以便新版识别升级源版本并执行正常升级。
- 此临时修正不代表项目已升级成功；最终版本由成功打开的 Editor 写入。

## 验证状态
- 待首次导入、编译兼容性检查、Unity Console 双次 error 扫描和场景运行截图。

## 首次重建恢复
- 版本标记修正后已进入 Project Upgrade Required，确认继续后 Editor 在程序集载入阶段退出，Hub 返回 4294967295，未产生可用编译诊断。
- 确认无 Unity Editor 进程后，将 Library 与 Temp 整体移动至上述备份目录的 Library-before-unity6、Temp-before-unity6；路径边界已核验，未删除缓存数据。
- 重开项目让 Unity 6 重新导入，排除旧程序集与导入数据库影响；不能据此断言缓存就是根因。
- 已确认官方 CLI 1.0.0-beta.6 随 Hub 安装可用；项目尚无 com.unity.pipeline，本轮尚未新增该包。

## 编译兼容修复
- 首次编译有 14 条不同的 CS0619：项目 TMP 换行属性 11 处、旧 MCP 运行时对象 ID API 3 处。仅是日志中的初步诊断，最终仍须读 Console。
- Part1SceneHarness 的 9 处与 Part1TestManager 的 2 处 enableWordWrapping 迁移为 textWrappingMode：true 对应 Normal，false 对应 NoWrap，保持原布局语义。
- Unity API Updater 自动将 RollingBallController 的 Rigidbody.velocity 迁移为 linearVelocity。
- 官方 MCP v10.1.2 源码包含 Unity 6.5+ EntityId 兼容层，故将项目已有依赖从 v8.2.1 更新并固定到 v10.1.2；没有手改 Library 中的包源码。
- 升级器自动迁移 URP、uGUI、Input System 等内置依赖；完整变更会在最终 diff 中审查。

## 首次导入与进一步检查
- 00:35 左右完成首次完整资源导入，退出 Safe Mode。随后出现 URP Material upgrade 对话框，已确认官方材质升级；该流程再次导入了三份微软雅黑字体，每份约 190–200 秒。
- 对比升级前工作区备份，目前发现 522 个变更文件，资源 GUID 无变化。检查结果保存于 AutomationOutputs/Unity6Migration/20260909/serialization_audit.json；这不替代运行时引用和画面验证。
- 退出 Safe Mode 的编译清单仍只列出 MCP Runtime 的旧 Serialization 文件，未列出新包 Runtime/Helpers 下的辅助文件，出现 CS0234。磁盘上的辅助文件确实存在，需等导入完成后刷新并复查；尚不能判为源码缺陷或编译通过。
- 新验收配置和输出独立保存在 AutomationOutputs/Unity6Migration/20260909，避免把旧报告当作本次结果。
- 00:48 通过 Hub 重开后，MCP Runtime 编译清单完整包含 6 个 Helpers 文件，构建成功，证明之前的 CS0234 来自过期的编译输入清单。没有修改包缓存源码。
- 官方 API Updater 继续迁移 RollingBallSceneBuilder 的 velocity/drag/angularDrag 为 linearVelocity/linearDamping/angularDamping，保持原数值。
- 编辑器脚本恢复后，纹理导入器暴露运行时异常：OnPreprocessTexture 中调用 SaveAndReimport，Unity 6 拒绝在导入期间再次 ImportAsset。删除该递归调用，保留 Sprite/Single 设置，由正在进行的导入正常应用。

## 停用旧循环自动启动
- 旧 DeckIdealAutoMenu 会在每次编辑器启动时自动切换场景并写历史验收目录。本次恢复编译后确实发生了一次自动运行，不能用该报告充当独立验收。
- 默认关闭 LegacyDeckIdealAutoRun、LegacyMcpHttpAutoStart、LegacyCcrBridge 三组 EditorPrefs 开关；旧功能需显式设为 true 才自动启动，手动 Scene Automation 和 MCP 菜单保留。
- MCP 窗口改为 Stdio 并启动本地端口 6400；不依赖旧 HTTP 服务器或外部多智能体循环。首次 Console 双次检查均为 0 条 error，原始结果已存档。

## 自动化验收发现
- EditMode 任务 cbe984963be74d319e50021bb1cbc23c 完成 51 项，列出 8 项失败。2026-01-15 工作记录也记载过 8 项遗留失败，但仍需按失败名称核对；尚不能宣称全部测试通过。
- DeckMana 第一轮报告 status=success、7 步均成功，但实际查看截图全部为黑图。保留 report_before_capture_fix.json 和对应截图作为失败证据。
- SceneAutomationOrchestrator 改为 WaitForEndOfFrame 后调用 ScreenCapture.CaptureScreenshotAsTexture，捕获最终 Game View；不再使用 Camera.Render 的离屏路径，避免漏掉 URP 和 Overlay UI。
- 捕获/编码/写文件异常及纯色空白截图使步骤和整份报告失败；空白截图仍保存供排查。无论结果如何都释放临时 Texture2D。截图非空仅是最低检查，最终仍须人工式视觉核验。
- Full HD 复测仍发现 DeckMana 的纵向最小高度总和超过容器；手牌 320px 高而可用区域只有 164px。仅针对牌库+魔力测试布局，将日志移到左侧按钮下、隐藏未启用的测试按钮，卡牌详情继续使用原有浮窗，去掉重复内嵌预览；手牌区域增至 416px，魔力条声明最小高度。其余测试组合保留原布局。
- 紧凑布局第一次复测发现弃牌空状态文字依赖隐藏预览的字体引用；因此保留该预览对象与绑定，仅设为 inactive，使中文字体继续传递给浮窗。

## 最终结果（2026-09-09 01:31，Asia/Shanghai）
- 已通过 Hub 正常打开 Unity 6000.6.0f1（f7f8ed4d1e24）；ProjectVersion.txt 由编辑器写入新版，退出 Safe Mode，编辑器当前可交互。
- 最后一次代码修改仅修正缩进；MageKnight.Scripts.dll 于 01:29:15 重新编译。其后 Console 两次读取 types=["error"]、count=1000、includeStacktrace=true，均 0 条 error：console_final_first.json、console_final_second.json。
- DeckMana 最终运行 Part1_DeckManaTest_20260909_012731，7 步 success，1920×1080。已查看手牌/魔力终态及弃牌空状态，中文正常、无面板重叠；此前完整轮次也查看了牌组浮窗和关闭返回。最终报告：AutomationOutputs/Unity6Migration/20260909/deckmana/report.json。
- DeckIdeal 运行 Part1_DeckIdeal_20260909_012418，4 步 success，1378×1204。已逐张查看初始、牌组、弃牌、手牌高亮；现有背景、卡背和高亮能显示，但整体偏淡和大片半透明框仍属于待改善的美术质量问题。报告：AutomationOutputs/Unity6Migration/20260909/deckideal/report.json。
- 迁移备份比对：536 个已跟踪文件不同，主要是 Unity 资源和项目设置升级；变更 .meta 的 GUID 无变化。没有据此宣称所有未测试场景均兼容。
- 已检查本轮相关 git diff；git diff --check 覆盖 Assets/Editor、Assets/Scripts、Packages、ProjectSettings、AGENTS.md 通过。清理了升级器写入的 3 行末尾空格；仓库 package.json 没有有效 format/lint/test 命令，未将占位脚本当作验证。
- 安装监控已暂停；没有启动旧外部 multi-agent loop。Seedream 素材生成与新美术场景尚未在本轮完成。

## Git 交付状态
- 当前分支 feat/resilient-element-scene，历史合并尚未提交；暂存区已有 4097 个文件的历史内容，不能混称为此次迁移的小提交。
- 本轮未新增暂存、未提交、未推送、未创建 PR。原因是现有合并需单独整理，而且 8 项规则测试仍失败；保留完整工作区及迁移前备份，未重置、未覆盖旧用户改动。
- 三个原本未跟踪的旧启动脚本也包含本轮 gate 修改：Assets/Editor/McpHttpBridgeAutoRecover.cs、Assets/Editor/McpForceStartMenu.cs、Assets/Editor/Temp/DeckIdealAutoMenu.cs，交付时不可遗漏。

## 后续边界
- 升级与场景运行恢复已完成；完整玩法回归、美术质量改进、Seedream 任务恢复机制验证、官方 CLI pipeline 接入仍是后续工作。
- 不因历史日志也出现过 8 项失败就认定是同一组；下表记录本次实际失败，原始结果见 AutomationOutputs/Unity6Migration/20260909/test_results.json。

| 失败测试 | 实际结果 |
| --- | --- |
| MageKnight.Tests.Part1.Part1IntegrationTests.CardSystem_EffectLoading | Effect should implement ICardEffect   Expected: assignable from <MK.Logic.Runtime.CardEffects.ICardEffect>   But was:  <MK.Logic.Runtime.CardEffects.FireballEffect> |
| MK.Tests.game.ManaPoolTests.TestReturnDice | Expected: null   But was:  <MK.Logic.Runtime.ManaDie> |
| MK.Tests.game.MarketTests.TestAdvActionSupplyInitialRefill | Expected: 5   But was:  4 |
| MK.Tests.game.MarketTests.TestSpellSupplyRefill | Expected: 5   But was:  3 |
| MK.Tests.game.PlayerStateTests.TestReputationInfluenceSystem | Expected: 0   But was:  5 |
| MK.Tests.map.ExplorationTests.TestExploreSuccessWithSufficientMovement | Expected: True   But was:  False |
| MK.Tests.map.ExplorationTests.TestExploreWithRotation | Expected: 180   But was:  0 |
| MK.Tests.map.ExplorationTests.TestTileDrawingOrder | Expected: <MK.Logic.Runtime.Map.MapTile>   But was:  null |
