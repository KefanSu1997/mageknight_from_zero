# Repository Guidelines

## 注意事项：

1. 思考可以用英文，回复对话必须使用中文。

2. 代码是给人看的，它只是碰巧可以运行。每次进行代码修改后，详细地汇报你修改的思路和具体修改的功能。

3. 确认是否存在编译错误必须按照“Unity 编译错误查看”这一部分的标准流程进行，获取unity console中的编译错误信息。

4. 在完成代码修改后，必须确认是否存在编译错误，如果存在，继续修改直到无编译错误。

5. 在修复bug过程中，当一次代码修改后导致编译bug大量增加时，应该首先进行回退，撤销修改，然后重新思考解决方案。

6. 涉及测试场景的修改时，修改后必须运行自动化测试，检查运行效果。同样也必须确认是否存在编译错误，如有错误必须继续修改直到无编译错误。

7. vibe_coding/codex 是属于你的工作记录文件夹，每次进行代码修改后，都要在该文件夹中记录工作中的计划、思路、完成进度等等，方便之后我进行查阅，同时你也可以从中查找有用的信息。

8. vibe_coding/codex/project_experience 中记录了过往解决问题的经验和教训，你遇到问题时可以查阅。当你解决了新的问题时，必须把经验和教训记录到该文件夹中，以供之后查阅。

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
  Part1_DeckManaTest.unity，按钮路径为 Canvas/Part1TestLayout/ControlColumn/Buttons/测试牌库系统Button、…/测试魔力池
  Button，截图与报告输出到 AutomationOutputs/DeckManaTest/。

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

# Unity 编译错误查看

当你需要查看 Unity 项目的编译报错时，请调用 `unity-mcp` 提供的工具。

- 工具前缀：`@mcp-unity`
- 可用命令：
  - `@mcp-unity.get_compile_errors()`  
    获取当前 Unity 编辑器里的编译错误信息，并以列表形式返回。
  - `@mcp-unity.get_console_logs(level="error")`  
    获取 Unity Console 中的错误日志。
  - `@mcp-unity.execute_menu_item(path="...")`  
    在 Unity 中执行菜单命令（比如 `Assets/Reimport All`）。

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
