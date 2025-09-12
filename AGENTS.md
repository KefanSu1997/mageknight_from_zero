# Repository Guidelines

## 注意事项：

1. 思考可以用英文，但是对话交流时用中文。

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
