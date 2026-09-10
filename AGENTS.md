# Repository Guidelines

## 注意事项：

1. 思考可以用英文，回复对话必须使用中文。

2. 代码是给人看的，它只是碰巧可以运行。每次进行代码修改后，详细地汇报你修改的思路和具体修改的功能。

3. 确认是否存在编译错误必须按照“Unity 编译错误查看”这一部分的标准流程进行，获取unity console中的编译错误信息。

4. 在完成代码修改后，必须确认是否存在编译错误，如果存在，继续修改直到无编译错误。

5. 在修复bug过程中，当一次代码修改后导致编译bug大量增加时，应该首先进行回退，撤销修改，然后重新思考解决方案。

6. 涉及测试场景的修改时，修改后必须运行自动化测试，观察对应的截图是否达到预期效果，如果存在问题必须继续修改。同样也必须确认是否存在编译错误，如有错误必须继续修改直到无编译错误。

7. vibe_coding/codex 是属于你的工作记录文件夹，每次进行代码修改后，都要在该文件夹中记录工作中的计划、思路、完成进度等等，方便之后我进行查阅，同时你也可以从中查找有用的信息。

8. vibe_coding/codex/project_experience 中记录了过往解决问题的经验和教训，你遇到问题时可以查阅。当你解决了新的问题时，必须把经验和教训记录到该文件夹中，以供之后查阅。记录经验教训后，也必须在本文件中更新经验索引：一句话总结作为索引，加上全部经验对应文件路径。

9. 当前已迁移到 Unity 6000.6.0f1，编辑器路径为'D:\Unity\Editor\6000.6.0f1\Editor\Unity.exe'；通过 Unity Hub 打开，不直接从命令行调用编辑器。迁移记录见 `vibe_coding/codex/worklog_2026-09-09_unity6_migration.md`。

10. 禁止安装或者卸载unity的packages

11. 禁止使用batchmode或者用命令行直接调用unity.exe，这会导致命令挂起卡住。

12. MCP retry rule: on `hint=retry` or `ping not answered`, do not rapid-retry. Run `refresh_unity(wait_for_ready=true)` before each retry, use exponential backoff (2s -> 4s -> 8s -> 16s, cap 30s) via active polling, and restart MCP session after repeated consecutive failures.

13. 除非用户明确要求设计新卡，行动卡必须复用 `Assets/GameData/CardsAssets` 的既有定义、`Assets/GameData/cards` 的原卡面和 `ActionSystem/CardEffects` 的效果实现；禁止用自制训练卡或猜测数值替代《魔法骑士》原卡。验收必须同时检查来源、牌面文字和实际效果。

## 经验索引

- 元素护盾成功触发、目标索引与魔力箭矢整笔额外费用；内存耗尽时减少Unity空闲导入worker —— `vibe_coding/codex/project_experience/card_elemental_triggers_and_atomic_extra_mana_2026-09-10.md`

- 原卡移动和重整：指定格/地形减费、真实成本消费、伤牌与就绪独立、逐步后续操作验收 —— `vibe_coding/codex/project_experience/card_movement_scope_and_unit_readiness_2026-09-10.md`

- 原版基础卡：本能数据恢复、互斥选项支付前验证、实际目标冰格挡及魔晶溢出 —— `vibe_coding/codex/project_experience/original_basic_card_choices_and_target_block_2026-09-10.md`

- 原版卡战斗资源：元素/攻城贡献、合计取整、城防阶段、实际击杀与两步场景验收 —— `vibe_coding/codex/project_experience/typed_card_combat_and_rounding_2026-09-10.md`

- 原法术费用与用例版本：ManaColor整笔支付、费用不足明确拒绝、TextAsset显式导入/SHA256、EditMode XML与摘要一致性 —— `vibe_coding/codex/project_experience/card_mana_and_suite_identity_2026-09-10.md`

- 真实伤牌守恒与报告IO1224：按实际牌区计数、治疗按实际移除数触发、独立运行目录和不可变检查点 —— `vibe_coding/codex/project_experience/card_wounds_and_immutable_reports_2026-09-10.md`

- 原卡场景审计：121张有效牌面、合法夹具、独立预期、模块与整卡结论分开、报告时效和编译等待 —— `vibe_coding/codex/project_experience/original_card_scene_audit_2026-09-10.md`

- Git 存档：隐藏未跟踪文件、未完成合并备份、SSH 443 和 LFS/远端提交核对 —— `vibe_coding/codex/project_experience/git_archive_pending_merge_2026-09-10.md`

- 原卡复用纠偏：原SO/原卡面/原效果三者同源、基础二选一与强化耗色、禁止用自制训练卡冒充正式内容 —— `vibe_coding/codex/project_experience/official_card_reuse_and_acceptance_2026-09-10.md`
- 可复用冒险：角色/地点独立资产、卡牌实例与目标命令、伤牌同步、地图边界及规则滚动验收 —— `vibe_coding/codex/project_experience/reusable_adventure_content_and_input_2026-09-10.md`
- 四个规则场景数值验收：独立预期、按钮命中、真实UI文本、失败资源守恒、协程超时和Console双扫描 —— `vibe_coding/codex/project_experience/rules_scenarios_numeric_acceptance_2026-09-09.md`
- Codex 手机远程连接：系统代理未覆盖 WebSocket，用户开启 FlClash TUN 后恢复 Connected；区分网络通道与手机配对 —— `vibe_coding/codex/project_experience/codex_remote_tun_connection_2026-09-09.md`
- DeckMana 桌面美化：统一材质与层级、保留按钮路径与隐藏绑定、空槽不计入手牌、截图及真实鼠标验收 —— `vibe_coding/codex/project_experience/deckmana_table_visual_polish_2026-09-09.md`

- Unity 6.6 迁移：包编译清单恢复、禁止导入回调内重导入、停用旧自动启动、Game View 黑图误验收与布局复测 —— `vibe_coding/codex/project_experience/unity6_migration_capture_and_legacy_startup_2026-09-09.md`

- DeckViewer 浮窗调试：Canvas 排序、RectTransform sizeDelta 与烟雾测试要点 —— `vibe_coding/codex/project_experience/deck_discard_viewer_notes.md`
- DeckMana 布局：ScrollRect 高度计算与自动化截图校准 —— `vibe_coding/codex/project_experience/deckmana_layout_scrollable_log.md`
- 字体修复：TMP 缺字回退与资源整理流程 —— `vibe_coding/codex/project_experience/font_fix_summary.md`
- 自动化等待：场景执行步进与实时等待的调优 —— `vibe_coding/codex/project_experience/scene_automation_waits.md`
- 理想卡组截图调优：立绘高光透明度、魔法阵/星云叠层与牌堆位置透明度指南 —— `vibe_coding/codex/project_experience/deck_ideal_layout_notes.md`
- Imdream 下载偶发 EOF：imdream_query.ps1 失败重试与下载参数要点 —— `vibe_coding/codex/project_experience/imdream_query_download_retry_2026-01-05.md`
- DeckIdeal 素材替换审计记录：输出路径/替换路径/导入要点 —— `vibe_coding/codex/project_experience/deck_ideal_asset_trace_log_2026-01-06.md`
- RR1/RR3 QA recovery: targeted rollback for `McpHttpBridgeMenu.cs` and compile_status root-field contract sync —— `vibe_coding/codex/project_experience/qa_rr1_rr3_mcp_bridge_rollback_and_compile_status_root_fields_2026-03-04.md`

---

# 自动化测试工具

通用步骤概括

1. 写配置：指定场景、截图/报告目录、按钮路径和等待时间。
2. 触发执行：可在 Unity 菜单、或命令行 -executeMethod 下运行。
3. 查看日志与报告：确认 Console 输出、report.json 状态与截图。
4. 复盘异常（如有）：利用 Debug 菜单检查 Pending Request、按钮层级或缺失按钮警告，再调整配置重试。

这套范例可直接复制，用于后续场景的自动化测试：只需仿照 JSON 更换 scenePath 与按钮路径，并重复上述流程即可。

具体示例：DeckManaTest 自动化范例

- 准备配置：在工程根目录创建 AutomationConfigs/part1_deckmana.json，约定 scenePath 指向 Assets/Scenes/Part1/
  Part1_DeckManaTest.unity，按钮路径为 Canvas/Part1TableRoot/ActionColumnShell/ActionColumnRoot/Buttons/测试牌库系统Button、
  …/测试魔力池Button，截图与报告输出到 AutomationOutputs/DeckManaTest/。

- Unity 内运行
  
  - 菜单：Tools/Scene Automation/Run DeckMana Automation
  
  - 调试：如需核对当前请求或按钮路径，可使用 Tools/Scene Automation/Debug/Log Pending Request、…/Test Deck Button
    Path、…/List Runtime Buttons。
  
  - 运行完成后 Unity Console 应输出：
    
    [SceneAutomation] 场景：Assets/Scenes/Part1/Part1_DeckManaTest.unity 状态：success 步骤数：3
      [OK] Scene Start -> .../AutomationOutputs/DeckManaTest/captures/000_Scene Start.png
      [OK] 测试牌库系统 -> .../AutomationOutputs/DeckManaTest/captures/001_测试牌库系统.png
      [OK] 测试魔力池 -> .../AutomationOutputs/DeckManaTest/captures/002_测试魔力池.png

- 命令行批处理（可选）
  
  "<Unity 编辑器路径>" -batchmode \
    -projectPath "<工程根目录>" \
    -executeMethod MageKnight.SceneAutomation.Editor.SceneAutomationCommand.RunFromCommandLine \
    --scene-automation-config "<工程根目录>/AutomationConfigs/part1_deckmana.json" \
    -quit

- 验证结果
  
  - 报告：AutomationOutputs/DeckManaTest/report.json，status= success 表示流程通过；若失败，steps[*].message 将注明
    原因（如按钮缺失），并保留失败截图。
  - 截图：…/captures/000_Scene Start.png（起始视图）、001_测试牌库系统.png、002_测试魔力池.png，用于比对 UI 变化。
  - 常见告警：运行中可能出现 msyh SDF 字体缺字警告，不影响逻辑；如需消除，可补充 TMP 字体或配置 fallback。

# Unity UI 认知包

## 1. UI 词汇与架构

### UI Toolkit

- **核心文件**
  
  - **UXML**：定义结构，类似 HTML。
  
  - **USS**：定义样式，类似 CSS。
  
  - **PanelSettings**：控制渲染、分辨率缩放等。

- **常见元素**
  
  - `VisualElement`（通用容器）
  
  - `Label`（文本）
  
  - `Button`（按钮，含 `text` 属性）
  
  - `Image`（图像，需在运行时绑定 Texture）

- **布局**
  
  - Flexbox 模型：`flex-direction`, `justify-content`, `align-items`。
  
  - 常见单位：px → 无单位数字，百分比可用。

- **运行方式**
  
  - 在场景中挂载 `UIDocument`，指向 UXML 与 PanelSettings。
  
  - UXML 内部元素可通过 `rootVisualElement.Q<T>("name")` 查询。

### uGUI (旧系统)

- **核心组件**
  
  - `Canvas`（必备根，决定渲染模式与分辨率）
  
  - `CanvasScaler`（缩放适配）
  
  - `GraphicRaycaster`（事件）
  
  - `RectTransform`（替代 Transform，支持锚点与对齐）

- **常见控件**
  
  - `Image`（图像，挂 Sprite）
  
  - `TextMeshProUGUI`（推荐文本控件）
  
  - `Button`（组合 Image + Button 脚本 + 子 Text）

- **特点**
  
  - 手工管理 RectTransform 的锚点/对齐。
  
  - 分层：Sorting Layer / Order in Layer。

### 两者并存注意

- UI Toolkit 用 PanelSettings 控制渲染顺序；uGUI 用 Canvas 的 Sorting Layer。

- 如果同时存在，要确保 PanelSettings 的排序不被 Canvas 遮挡。

- 事件系统（EventSystem、Input System）要确认兼容。

---

## 2. 项目约定

为了减少 agent 的歧义，请遵守以下规范：

- **命名规则**
  
  - 页面容器：`Page_XXX`
  
  - 通用容器：`VE_XXX`
  
  - 按钮：`Btn_XXX`
  
  - 图片：`Img_XXX`
  
  - 文本：`Txt_XXX`

- **目录结构**
  
  - 图片：`Assets/UI/Images`
  
  - UXML/USS：`Assets/UI/Views`
  
  - UI 脚本：`Assets/Scripts/UI`
  
  - Resources 路径：`Resources/UI/Images/…`（运行时加载用）

- **场景锚点**
  
  - 始终存在 `UIRoot` 节点。
  
  - UI Toolkit：`UIRoot` 上挂 `UIDocument`（指向 `.uxml` + `PanelSettings`）。
  
  - uGUI：`UIRoot` 下有 `Canvas`（含 CanvasScaler + GraphicRaycaster）。

- **MCP 工具调用（unity-mcp）**
  
  - `manage_asset`：写入/更新 UXML、USS、Sprites 等。
  
  - `manage_gameobject`：创建 GameObject，添加组件（如 UIDocument、Canvas）。
  
  - `manage_scene`：加载/保存测试场景。

---

## 3. UI 可见性检查清单

在调试 UI 渲染问题时，逐项检查：

### UI Toolkit

- `UIDocument` 是否启用？

- `UIDocument.visualTreeAsset` 是否正确引用？

- `PanelSettings` 是否绑定，渲染层级是否正确？

- `rootVisualElement` 下元素是否被 `display: none` 或 `visibility: hidden`？

- 图片是否在运行时正确绑定？（异步 Addressables 需要在主线程赋值）

### uGUI

- `Canvas` 是否启用，`Canvas.enabled == true`？

- `CanvasGroup.alpha` 是否为 1，`interactable` 与 `blocksRaycasts` 是否启用？

- `Image.color.a` 是否透明？

- 是否被 `Mask` / `RectMask2D` 裁切？

- 相机渲染层级与 Canvas Sorting Order 是否正确？

### 异步加载注意

- Addressables/Resources 回调要在主线程执行 UI 赋值。

- 赋值后最好调用 `LayoutRebuilder.ForceRebuildLayoutImmediate` 以刷新布局。

# Git & PR Workflow Rules

1. Never commit to main directly. Always create a new branch from origin/main:
   - Branch name: feat/ or fix/
2. Before coding:
   - Run: git fetch origin && git checkout -b origin/main
3. After changes:
   - Format & lint (use repo scripts): `make fmt && make lint` (或 npm/pnpm / python 脚本)
   - Run tests: `make test`（或自定义 test 命令）
4. Create atomic commits using the repository's commit template. Keep diffs small and focused.
5. Push and open a Pull Request:
   - Title uses Conventional Commit style.
   - PR description must include: Why / What / How to test / Risks / Related issues.
6. Never force-push unless explicitly instructed.
7. If conflicts occur:
   - Rebase onto origin/main: `git fetch origin && git rebase origin/main`
   - Resolve conflicts and run tests again.
8. For any automated file generation (codegen, assets lists), include reproducible commands in PR body.

You are operating in a Git repo. Follow the repo's Git & PR Workflow Rules strictly:

- Create a new branch from origin/main: feat/
- Make minimal, reviewable commits using the commit template.
- Run: format, lint, tests before committing.
- Show me `git diff` before staging.
- Push branch and open a PR with a detailed description (Why/What/How to test/Risks).
- Never push to main; never force-push unless I say so.

# Git 命令使用规范：

```
# 新建功能分支
git fetch origin
git checkout -b feat/xxx origin/main

# 查看改动 / 暂存 / 提交
git status
git add -A
git commit -m "feat(core): add inference monitor with CLI entries

Why:
- need to track GPU usage under vLLM

What:
- add monitor.py and CLI flags
- integrate with existing runner

How to test:
- python monitor.py --dry-run
"

# 同步远端
git push -u origin feat/xxx

# 基于 main 更新分支（保持线性历史）
git fetch origin
git rebase origin/main

# 打 PR（GitHub CLI，CCR 也能用）
gh pr create --fill --base main --head feat/xxx
gh pr view --web

# 代码审查后合并（保护分支+CI 通过）
gh pr merge --squash
```

# 自动化截图流程（Scene Automation）

本节总结“自动化截图流程”从配置到触发再到输出的完整步骤，默认 **不使用 batchmode**。

## 1) 配置

DeckIdeal 的快速入口会自动生成配置文件：

- 配置路径：`multi-agent-workspace/runs/T-20251028-012/scene_automation_deckideal.json`

配置核心字段说明：

- `scenePath`：目标场景，例如 `Assets/Scenes/Part1/Part1_DeckIdeal.unity`
- `screenshotsDirectory`：截图输出目录
- `reportPath`：报告输出路径（JSON）
- `captureInitialView`：是否捕获初始视图（会生成 000_ 开头的截图）
- `initialDelaySeconds`：场景加载后首次截图前的等待
- `defaultWaitAfterSeconds`：每个步骤点击后的默认等待
- `steps`：按钮点击步骤列表（label + buttonPath + waitAfterSeconds）
- `useRunSubfolder`：是否在截图目录下再创建一次性子目录

## 2) 触发（禁止 batchmode）

在 **Unity 编辑器** 中执行：

- 菜单：`Tools/Scene Automation/Run DeckIdeal Automation`

执行流程：

1. 编辑器自动写入配置文件（路径见上）。
2. 进入 Play Mode。
3. SceneAutomation 在运行时依次点击按钮、截图并写报告。
4. 流程结束后自动退出 Play Mode。

## 3) 输出

成功后应看到：

- 报告：`multi-agent-workspace/runs/T-20251028-012/review_bundle/artifacts/deck_ideal_report.json`
  - `status=success` 表示流程通过。
  - `steps[*].screenshotPath` 指向具体截图。
- 截图目录：`multi-agent-workspace/runs/T-20251028-012/review_bundle/artifacts/screenshots`
  - `000_Scene Start.png`（初始视图）
  - `001_...` / `002_...` / `003_...`（每个步骤的截图）
  - DeckIdeal 额外别名：`deck_ideal_overview.png`、`deck_ideal_automation_000.png`

## 4) 验证要点

- Unity Console 应输出：
  - `[SceneAutomation] 场景：... 状态：success 步骤数：N`
  - 每个步骤的 `[OK] label -> screenshotPath`
- 若无输出或没有新文件，请检查：
  - 当前是否处于 Play Mode
  - `scenePath` 是否正确
  - `screenshotsDirectory` / `reportPath` 是否可写

# Unity 编译错误查看

- Unity 编译错误检查（标准流程，按顺序执行）：
  
  1. 先清空/隔离旧日志（可选但推荐）
  - 调用 Unity Console：只拉取最近一段（例如 count=200），确认当前会话干净；或先执行清理（如果你们有清理菜单/工具的
    话）。
  2. 获取编译错误（必须）
  - 用 unity-mcp 从 Console 拉取 types=["error"]（建议 count=1000，并开启 include_stacktrace=true）。
  
  - 判定标准：返回 0 条 error 才算“无编译错误”。
  3. 如 error 为 0，再检查异常类报错（必须）
  - 再拉取一次 types=["error"]（同上）确保没有遗漏；必要时把 count 加大。
  
  - 注意：本项目工具的 types 只接受 error|warning|log|all，不要传 Exception/Assert 之类值。
  4. 如存在 error，按错误清单逐条处理（必须）
  - 记录每条 error 的 message/file/line/stackTrace。
  
  - 修复后重复步骤 2，直到 error 为 0。
  5. 最终留档（按你们仓库要求）
  - 在 vibe_coding/codex 记录：检查时间、错误条数、关键错误信息（若有）和修复结论。

## Project Structure & Module Organization

- `Assets/`: game code and content.
  - `Assets/Scripts/`: gameplay/runtime code (C#).
  - `Assets/Scenes/`: Unity scenes.
  - `Assets/Tests/`: Unity Test Framework tests (`*Tests.cs`, assembly via `MageKnightTests.asmdef`).
- `Packages/manifest.json`: package versions; manage via Unity Package Manager.
- `ProjectSettings/`: Unity project settings.
- `MageKnight_from_zero.sln` and `*.csproj`: generated build files for IDEs.

## Build, Test, and Development Commands

- Open in Unity: use Unity Hub and target the project root.
- Run tests (Unity CLI, EditMode):
  `"<Unity_Editor>\\Unity.exe" -batchmode -projectPath "." -runTests -testPlatform EditMode -testResults ".\\TestResults.xml" -quit`
- Optional .NET tests (if configured): `dotnet test MageKnightTests.csproj`
- Create a player build (Editor): File → Build Settings → Build.
- Create a player build (CLI example, Windows):
  `"<Unity_Editor>\\Unity.exe" -batchmode -projectPath "." -buildWindows64Player ".\\Build\\MageKnight.exe" -quit`

## Coding Style & Naming Conventions

- C# with 4‑space indentation; UTF‑8 files with Unix or Windows line endings accepted.
- Types/methods: PascalCase; local vars/params: camelCase; private fields: `_camelCase`.
- One class per file; filename matches class (e.g., `ManaPool.cs`).
- Keep MonoBehaviours lean; prefer plain C# classes for domain logic.

## Testing Guidelines

- Place tests under `Assets/Tests/` and suffix files with `Tests` (e.g., `ManaPoolTests.cs`).
- Prefer EditMode tests for logic in `Assets/Scripts/`.
- Run via Unity Test Runner (Window → Test Runner) or CLI above.
- Aim to cover rules/edge cases for core systems (combat, exploration, mana, market).

## Commit & Pull Request Guidelines

- Commits: imperative mood, concise subject, useful body when needed.
- Conventional Commits encouraged (e.g., `feat: add terrain cost rules`).
- PRs: include summary, linked issues, steps to test, and relevant screenshots/logs.
- Keep PRs scoped; update or add tests for changed logic.

## Security & Configuration Tips

- Do not commit `Library/`, `Temp/`, `Logs/`, `Build/`, or IDE caches; use `.gitignore`.
- Packages are pinned via `Packages/manifest.json`; avoid manual edits to `Packages/packages-lock.json`.

## Agent‑Specific Notes

- When modifying files, prefer `Assets/Scripts/` for runtime logic and avoid touching `ProjectSettings/` unless required.
- Do not edit generated `Library/` artifacts. Follow this document’s scope rules when proposing changes.

# 附录：HTML/CSS → UXML/USS 映射表

## 1. HTML 标签 → UXML 元素

| HTML 标签               | 对应 UXML 元素                     | 说明                                  |
| --------------------- | ------------------------------ | ----------------------------------- |
| `<div>`               | `<VisualElement>`              | 通用容器，默认无样式。                         |
| `<span>`              | `<Label>`                      | 内联文本（无换行）。                          |
| `<p>`                 | `<Label>`                      | 段落文本。                               |
| `<h1>` `<h2>` `<h3>`  | `<Label>`                      | 大标题/中标题/小标题，需通过 USS 设置 `font-size`。 |
| `<ul>` `<ol>`         | `<VisualElement>`              | 列表容器，使用 `flex-direction: column;`。  |
| `<li>`                | `<Label>`                      | 列表项文本，前缀符号需手动加。                     |
| `<img>`               | `<Image>`                      | 仅定义占位，`image` 属性需运行时绑定。             |
| `<button>`            | `<Button>`                     | 默认含 `Label` 子元素。                    |
| `<input type="text">` | `<TextField>`                  | 单行输入框。                              |
| `<textarea>`          | `<TextField multiline="true">` | 多行输入框。                              |
| `<a>`                 | `<Label>` + 点击事件               | 超链接需手动添加事件。                         |

---

## 2. CSS 属性 → USS 样式

| CSS 属性                       | USS 属性                    | 说明                                                                      |
| ---------------------------- | ------------------------- | ----------------------------------------------------------------------- |
| `display: flex`              | `display: flex`           | 支持，默认就是 flex。                                                           |
| `flex-direction: row/column` | 同名                        | 横向/纵向布局。                                                                |
| `justify-content`            | 同名                        | 支持 `flex-start`, `center`, `flex-end`, `space-between`, `space-around`。 |
| `align-items`                | 同名                        | 支持 `flex-start`, `center`, `flex-end`, `stretch`。                       |
| `width`, `height`            | `width`, `height`         | 单位去掉 `px`，如 `100px` → `100`。                                            |
| `margin`, `padding`          | `margin`, `padding`       | 同 CSS 简写。                                                               |
| `background-color`           | `background-color`        | 十六进制或 `rgb()`。                                                          |
| `color`                      | `color`                   | 文本颜色。                                                                   |
| `font-size`                  | `font-size`               | 文本大小。                                                                   |
| `font-weight: bold`          | `-unity-font-style: bold` | 特殊写法。                                                                   |
| `text-align`                 | `-unity-text-align`       | 取值：`upper-left`, `middle-center` 等。                                     |
| `border`                     | 无原生支持                     | 需用 `border-color` + `border-width`。                                     |
| `overflow: hidden/scroll`    | `overflow`                | Unity 2022+ 支持。                                                         |

---

## 3. 属性/事件映射

| HTML 属性                     | UXML/USS 对应                          | 说明                          |
| --------------------------- | ------------------------------------ | --------------------------- |
| `id="foo"`                  | `name="foo"`                         | UXML 用 `name` 替代 HTML 的 id。 |
| `class="bar"`               | `class="bar"`                        | 同 HTML，USS 用 `.bar` 选择器。    |
| `src="img.png"` (img)       | 无                                    | Unity 不能直接写资源路径，需运行时绑定。     |
| `onclick="..."`             | C# `button.clicked += () => { ... }` | 事件逻辑要写在脚本里。                 |
| `placeholder="..."` (input) | `label="..."` (TextField)            | 占位符文本。                      |

---

## 4. 示例对照

### HTML

`<div class="container">  <h1>Hello World</h1>  <p class="desc">Welcome to UI Toolkit</p>  <button id="Btn_Start">Start</button> </div>`

### 生成的 UXML

`<UXML xmlns="UnityEngine.UIElements">  <VisualElement class="container">    <Label text="Hello World" class="h1"/>    <Label text="Welcome to UI Toolkit" class="desc"/>    <Button name="Btn_Start" text="Start"/>  </VisualElement> </UXML>`

### USS

`.container {  flex-direction: column;  align-items: center;  justify-content: center; }  .h1 {  font-size: 24;   -unity-font-style: bold; }  .desc {  font-size: 14;  color: #888888; }`

好的 👍
下面是一份完整的、**可直接放入你项目根目录的 `AGENT.md` 文件（中文版）**。
它是专门给 Codex / Claude Router / MCP-Unity 等代码代理使用的协作说明文档，
用于指导两个或更多 agent 在同一台电脑、同一 Unity 项目下安全并行开发。

---

## 📄 `AGENT.md` （中文版本）

```markdown
# 🧠 多智能体（Multi-Agent）并行开发指南  
## —— MageKnight_from_zero 项目（Unity + Git Worktree）

本仓库支持多个代码代理（如 Codex CLI、Claude Router、MCP-Unity）  
在 **同一台电脑上并行开发**，通过 **Git Worktree** 创建独立工作区，  
实现互不干扰的 Unity 编译、测试与合并流程。

---

## 一、环境初始化步骤（仅需执行一次）

在主仓库根目录执行以下命令：

```bash
git fetch origin
git worktree add ../wk-agentA -b feat/scene-test-A origin/main
git worktree add ../wk-agentB -b feat/scene-test-B origin/main
```

| Agent | 分支名称                | 工作目录           | Unity 工程路径         |
| ----- | ------------------- | -------------- | ------------------ |
| A     | `feat/scene-test-A` | `../wk-agentA` | `D:\...\wk-agentA` |
| B     | `feat/scene-test-B` | `../wk-agentB` | `D:\...\wk-agentB` |

> 每个工作目录都是完整的 Unity 工程，拥有独立的 `Library/`、`Temp/`、`Logs/`。
> 禁止两个 agent 在同一个目录下操作！

---

## 二、agent 工作流程（标准化指令）

### Step 1. 进入自己的工作区

```bash
cd ../wk-agentA        # 或 ../wk-agentB
git pull
```

### Step 2. 开发或创建场景

例如：

```
Assets/Scenes/AgentA_TestScene.unity
Assets/Scripts/Demo/RotateCube.cs
```

### Step 3. 运行编译检测（无界面模式）

用于快速验证脚本是否能正常编译：

```powershell
"D:\Unity\Editor\2023.2.20f1c1\Editor\Unity.exe" `
  -batchmode -nographics -quit `
  -projectPath "D:\...\wk-agentA" `
  -executeMethod CIHooks.CompileAndQuit `
  -logFile "AutomationLogs/compile_agentA.log"
```

在wsl环境下运行unity编译：

./scripts/unity_ci.sh -p <wk路径> -m <Compile|EditMode|PlayMode> -l <log> -r <xml>

### Step 4. 运行自动化测试

执行 Unity 自带的测试运行器（编辑器模式）：

```powershell
"C:\Program Files\Unity\Hub\Editor\<版本号>\Editor\Unity.exe" `
  -batchmode -nographics -quit `
  -projectPath "D:\...\wk-agentA" `
  -runTests -testPlatform EditMode `
  -testResults "AutomationOutputs/results_agentA.xml" `
  -logFile "AutomationOutputs/test_agentA.log"
```

> 每个 agent 必须使用自己独立的日志与测试输出路径（logFile 与 testResults）。

### Step 5. 提交与推送

```bash
git add .
git commit -m "feat(agentA): 新增基础测试场景"
git push -u origin feat/scene-test-A
```

---

## 三、Unity 批处理编译脚本（必备）

在项目中创建文件：
**`Assets/Editor/CIHooks.cs`**

内容如下：

```csharp
using UnityEditor;

public static class CIHooks {
    public static void CompileAndQuit() {
        AssetDatabase.Refresh();
        EditorApplication.Exit(0);
    }
}
```

此脚本用于支持命令行调用 Unity 进行无界面编译检测。

---

## 四、分支合并流程

当所有 agent 的测试都通过后，按以下顺序合并：

```bash
git checkout main
git pull
git merge --no-ff feat/scene-test-A
git merge --no-ff feat/scene-test-B
git push origin main
```

随后再执行一次完整测试，确认主分支编译和运行均正常。

---

## 五、协作规则（必须遵守）

* ❌ 不要在同一文件夹中同时运行两个 agent。

* ✅ 每个 agent 使用独立的 `worktree` 目录。

* 🪶 每个 agent 的 `-logFile`、`-testResults` 路径必须不同。

* 🔒 对 `.unity`、`.prefab` 等共享文件，使用 Git LFS 锁定防止冲突：
  
  ```bash
  git lfs lock Assets/Scenes/SharedScene.unity
  git lfs unlock Assets/Scenes/SharedScene.unity
  ```

* 💾 Unity 的序列化模式需设为 **Force Text**（文本格式），减少合并冲突。

* ✅ 所有提交必须是“可编译、可运行、测试通过”的版本。

---

## 六、常用命令速查表

| 命令                                                | 功能说明           |
| ------------------------------------------------- | -------------- |
| `git worktree list`                               | 显示当前存在的工作区     |
| `git worktree remove ../wk-agentA`                | 删除 agentA 的工作区 |
| `Unity.exe -executeMethod CIHooks.CompileAndQuit` | 执行无界面编译检测      |
| `Unity.exe -runTests …`                           | 执行自动化测试        |
| `git lfs lock/unlock <文件>`                        | 锁定或解锁共享场景文件    |

## 使用即梦 (Imdream) API 进行文生图，可用于美术素材生成

  你可以通过本地封装脚本调用即梦（Imdream）生成或编辑图像。整个流程是异步的：先提交生成任务并获得任务 ID，再用该 ID 轮询
  查询，直到任务完成并获取最终图像。

### 核心工作流程

1. 生成图像（提交任务）
   执行 `tools/imdream_submit.ps1`，提供核心提示词 (Prompt) 及可选参数控制输出。脚本成功后会返回唯一的任务 ID
   (Task ID)。
   
   - 注意：看到任务 ID（如 1234567890）表示任务已提交，图像尚未生成。
   - 若需要 `width/height/size/min_ratio` 等高级参数，请改用 `tools/generate_imdream_image.ps1`（同样返回任务 ID）。

2. 查询结果（获取图像）
   拿到任务 ID 后，执行 `tools/imdream_query.ps1 <task_id> [output_dir] --poll --interval <sec> --timeout <sec> --download-name <name>`
   轮询任务状态并下载结果。
   
   - 传入 `--download-name` 会自动下载 `image_urls` 并保存到 AutomationOutputs/Imdream/ 下。
   - 文件命名格式：<name>_<索引>.png，例如 t2i_cat_0.png。
   - 不传 `--download-name` 时只输出 JSON（仅当返回 base64 数据时才会自动落盘）。
   
   ———
   
   ### 具体指令与参数
   
   #### 1. 文生图 (Text-to-Image)
   
   .\tools\imdream_submit.ps1 -Prompt "<提示词>" [可选参数]
   
   示例：
   
   .\tools\imdream_submit.ps1 -Prompt "一只穿着宇航服的猫漂浮在星云中，赛博朋克风格，电影级光效" -Scale 0.6 -ForceSingle
   
   若需指定尺寸等高级参数：
   
   .\tools\generate_imdream_image.ps1 "一只穿着宇航服的猫漂浮在星云中，赛博朋克风格，电影级光效" --size 1048576 --scale 0.6
   
   #### 2. 图生图 / 图编辑 (Image-to-Image / Editing)
   
   需要提供参考图的公网 URL。
   
   - 若参考图在本地（如 my_cat.png），先上传到可公网访问的服务器（推荐 tmpfiles.org）：
     
     $IMAGE_URL = .\tools\imdream_upload_ref.ps1 "C:\path\to\my_cat.png"
     
     IMAGE_URL 的最终形式应为 https://tmpfiles.org/dl/<id>/my_cat.png，这是直接可下载链接。
   
   - 调用生成脚本时传入该 URL：
     
     .\tools\imdream_submit.ps1 -Prompt "把这只猫的背景换成月球表面" -ImageUrls $IMAGE_URL -Scale 0.6 -ForceSingle
     
     也可以多次添加参考图（最多 10 张），或用环境变量：
     
     $env:IMDREAM_IMAGE_REFS = "url1,url2"
     .\tools\generate_imdream_image.ps1 "<提示词>" [可选参数]
   
   #### 3. 常用可选参数
   
   下面参数适用于 `tools/generate_imdream_image.ps1`（`imdream_submit.ps1` 仅封装常用提交参数）。
   
   | 参数                  | 描述
   | 示例                               |
   
   | ---------------------                      | ----------------------------------------------------------------------------------------------- |
   | ------------------------------------------ | ----------------------------------------------------------------------------------------------- |
   | --width <W> &<br>--height <H>              | 成对使用，指定生成图像的精确宽度和高度。必须同时提供两个参数。                                                                 |
   | --width 2048 --height 2048                 |                                                                                                 |
   | --size <S>                                 | 指定输出图像的大致面积（像素总数），让模型自动选择宽高比（不同时提供 --width/--height 时生                                          |
   | 效）。                                        | --size 4194304（约 2048×2048）                                                                     |
   | --scale <F>                                | 控制提示词对画面的影响力，范围 [0,1]，默认 0.5。值越大越贴合提示词，越小越贴合参考图。                                                |
   | --scale 0.7                                |                                                                                                 |
   | --ref <URL>                                | 指定参考图 URL，可重复最多 10 次；同时传主持有图生图或风格参考。<br>（也可用                                                    |
   | IMDREAM_IMAGE_REFS="url1,url2" 环境变量一次性传入。） | --ref "https://tmpfiles.org/dl/...png"                                                          |
   | --force-single                             | 强制只生成一张图像。                                                                                      |
   | --force-single                             |                                                                                                 |
   | --min-ratio <R>                            | 限制生成图像的最小宽高比（宽/高）。                                                                              |
   | --min-ratio 1.5                            |                                                                                                 |
   | --max-ratio <R>                            | 限制生成图像的最大宽高比（宽/高）。                                                                              |
   | --max-ratio 2.0                            |                                                                                                 |
   
   #### 4. 查询与保存
   
   .\tools\imdream_query.ps1 <task_id> [可选输出目录] --download-name <name> --poll --interval 5 --timeout 300
- <task_id>：提交时返回的任务 ID。

- [可选输出目录]：缺省则保存到 AutomationOutputs/Imdream/。
  
  示例：
  
  .\tools\imdream_query.ps1 1234567890 AutomationOutputs/Imdream --download-name sample --poll --interval 5 --timeout 300
  
  运行完成后，即可在 AutomationOutputs/Imdream/sample_0.png 等文件中查看生成结果。

### 提示词工程 (Prompt Engineering) 指南

为了生成高质量、符合预期的图像，请遵循以下提示词构建策略。

#### A. 核心原则：内容与美学分离

将你的提示词分为两个部分，逻辑上更清晰，效果更好。

1. **内容描述 (用自然语言长句)**: 清晰、连贯地描述画面的核心元素：主体是谁/是什么、在做什么、环境是怎样的。
   
   > **公式**: `主体 + 行为/姿态 + 环境/背景`
   > 
   > **示例**: `一个穿着银色铠甲的骑士，骑着一匹白色的战马，驰骋在日落时分的广阔草原上`

2. **美学描述 (用逗号分隔的短词/词组)**: 描述画面的风格、艺术媒介、光影、色彩、构图等艺术性元素。
   
   > **公式**: `艺术风格, 色彩基调, 光照效果, 构图方式, 图像质量`
   > 
   > **示例**: `数字绘画, 电影感, 温暖的色调, 黄金时刻光影, 动态模糊, 广角镜头, 杰作, 8K, 超高细节`

**组合示例:**

> `一个穿着银色铠甲的骑士，骑着一匹白色的战馬，驰骋在日落时分的广阔草原上, 数字绘画, 电影感, 温暖的色调, 黄金时刻光影, 动态模糊, 广角镜头, 杰作, 8K, 超高细节`

#### B. 特定任务技巧

| 任务目标        | 核心技巧                                                | 示例                                                          |
|:----------- |:--------------------------------------------------- |:----------------------------------------------------------- |
| **生成多张图**   | 在提示词中加入意图词。                                         | `"帮我设计一系列不同风格的logo"`                                        |
| **指定宽高比**   | 直接在提示词中说明，但**优先使用 `--width`/`--height` 参数**以获得精确控制。 | `"一张16:9的电影海报"`                                             |
| **提升文字准确率** | 将需要生成的文字内容用**英文双引号** `""` 包裹。                       | `"一张咖啡店海报，上面写着 "Morning Brew""`                             |
| **提升场景适配度** | 明确指出图像的用途和类型。                                       | `"一张用于PPT封面的背景图，内容是抽象的蓝色科技线条"`                              |
| **图像编辑**    | 使用 `变化动作 + 变化对象 + 变化特征` 的清晰指令。                      | `使用参考图，"将图中男士的T恤衫变为红色"`                                     |
| **多图融合/编辑** | 明确指出每张参考图的作用。                                       | `上传图1（人物）和图2（背景），"将图1中的角色放入图2的背景中，并采用图3（风格参考图）的梵高油画风格进行生成"` |

#### C. 思考链与执行示例

**用户请求**: "帮我画一只可爱的卡通小猫，做成微信头像。方形的。"

**你的思考与执行流程:**

1. **解析需求**:
   
   * 主体: 可爱的卡通小猫。
   * 用途: 微信头像。
   * 尺寸: 方形。

2. **构建提示词**:
   
   * 内容描述: `一只非常可爱的卡通小猫，毛茸茸的，大眼睛`
   * 美学描述: `皮克斯风格, 3D渲染, 明亮的色彩, 柔和的光线, 肖像特写`
   * 组合: `一只非常可爱的卡通小猫，毛茸茸的大眼睛，皮克斯风格, 3D渲染, 明亮的色彩, 柔和的光线, 肖像特写, 微信头像`

3. **确定参数**:
   
   * "方形" -> 使用 `--width 1024 --height 1024` 或 `--size 1024`。精确尺寸更佳。

4. **执行命令**:
   
   * **步骤1 (生成)**:
     
     ```bash
     .\tools\generate_imdream_image.ps1 "一只非常可爱的卡通小猫，毛茸茸的大眼睛，皮克斯风格, 3D渲染, 明亮的色彩, 柔和的光线, 肖像特写, 微信头像" --width 1024 --height 1024
     ```
   
   * **假设返回**: `Task ID: 9876543210`
   
   * **步骤2 (查询)**:
     
     ```bash
     .\tools\imdream_query.ps1 9876543210 AutomationOutputs/Imdream --download-name avatar_cat --poll --interval 5 --timeout 300
     ```

5. **报告结果**:
   
   * "图像已生成！您可以在 `AutomationOutputs/Imdream/9876543210_0.png` 查看。"
