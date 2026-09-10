# Unity 6.6 迁移与验收经验（2026-09-09）

## 恢复顺序

1. 从受限的 2023.2.20f1c1 迁移到已安装的 6000.6.0f1。先备份 Assets、Packages、ProjectSettings、未提交 diff 和 Git 合并状态，再通过 Hub 处理升级对话框。
2. Package Manager 拒绝旧版本字符串的 c1 后缀。临时规范化为 2023.2.20f1 后交由官方升级器写入最终版本，不能只改版本文件就宣布升级成功。
3. 首次程序集加载退出后，确认编辑器已结束，将旧 Library/Temp 移入备份再重新导入。字体首次导入耗时约 10 分钟，URP 官方升级可能再触发一次；根据实际进程和导入进度判断，不盲目杀进程。
4. 既有 MCP 从 v8.2.1 更新到 v10.1.2 以适配新版对象 ID API。退出 Safe Mode 后若 Runtime/Helpers 文件存在但不在 csc 编译清单中，正常关闭并通过 Hub 重开可恢复；不要改 Library 包缓存源码来掩盖过期清单。
5. OnPreprocessTexture 内不要 SaveAndReimport；正在进行的导入会应用 importer 设置，回调里重复导入会被 Unity 6 拒绝。

## 移除无人值守启动干扰

以下旧功能的自动启动默认关闭；手动菜单保留。需要历史流程时显式设置对应 EditorPrefs 为 true：

- MageKnight.LegacyDeckIdealAutoRun.Enabled
- MageKnight.LegacyMcpHttpAutoStart.Enabled
- MageKnight.LegacyCcrBridge.Enabled

当前验收通过 MCP for Unity 的本地 Stdio 通道（端口 6400）调用编辑器内工具，不启动旧 CCR/外部多智能体循环。工具返回 compile_requested 只表示已请求编译，必须确认真实编译结束；Part1SceneHarness 属于 MageKnight.Scripts.dll，不能只看 Assembly-CSharp.dll 的时间。

打开文件选择框的 execute_menu_item 调用可能因模态窗口阻塞而超时，但菜单已经执行。应操作该窗口并检查新 report 的时间与截图，不要重复提交菜单导致多个任务。

## 黑图不能算验收成功

旧 Camera.Render 离屏截图在新版 URP + ScreenSpaceOverlay 下产生全黑 PNG，旧报告仍为 success。

- Canvas.ForceUpdateCanvases 后等待 WaitForEndOfFrame，使用 ScreenCapture.CaptureScreenshotAsTexture 捕获最终 Game View。
- 编码或写文件失败、无效纹理、纯色空白画面均使步骤及报告失败；保留空白图片供排查，并释放临时纹理。
- 纯色检测只是最低检查。仍需打开每类关键状态截图，确认素材、文字、层级和交互变化；非空图片不等于美术质量达标。
- DeckIdeal 会把 Game View 改成 1378×1204；之后测试 DeckMana 要先选回 Full HD 1920×1080。

## 布局与字体

DeckMana 原有最小高度总和超过容器，手牌区可用 164px 却放入 320px 高的卡牌。针对该测试组合把日志放到左侧、隐藏未启用按钮与重复内嵌预览、为手牌预留 416px，并给魔力条声明最小高度。详情仍由卡牌浮窗显示。

隐藏预览对象时保留字体引用：Part1TestManager 的弃牌空状态借用该预览标题的字体。直接删掉绑定会出现中文方框，必须检查空状态而不只看有卡牌的画面。

## 本次验证证据与边界

- 输出目录：AutomationOutputs/Unity6Migration/20260909。
- Console 按 types=["error"]、count=1000、includeStacktrace=true 连续读取两次；各轮结果原样留档。
- DeckMana 抽牌/魔力/两种浮窗及关闭，共 7 个状态；DeckIdeal 初始画面与三个高亮，共 4 个状态。
- 规则测试 51 项中列出 8 项失败。2026-01-15 记录也出现过 8 项遗留失败，但未找到完整逐项旧结果，不能认定八项完全相同。新版启动成功、场景冒烟通过、规则测试通过、美术质量通过是四个不同结论。
- 官方独立 Unity CLI 已安装，但本项目没有 com.unity.pipeline，尚未建立 CLI 控制现有编辑器的通道。不要误用 CLI 启动另一个 batchmode 编辑器。
