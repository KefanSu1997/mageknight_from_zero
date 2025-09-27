# CLAUDE.md

# Important Note：

1. 回复使用中文

2. 代码是给人看的，它只是碰巧可以运行。每次进行代码修改后，详细地汇报你修改的思路和具体修改的功能。

3. 在修复bug过程中，当一次代码修改后导致编译bug大量增加时，应该首先进行回退，撤销修改，然后重新思考解决方案

4. vibe_coding/ccr 是属于你的工作记录文件夹，你可以在该文件夹中记录工作中的计划、思路、完成进度等等，方便之后查阅。

5. 当出现API调用失败时，表明上下文到达上限，主动使用/compact之后继续之前的工作 
   
   

---

# Git & PR Workflow Rules

1. Never commit to main directly. Always create a new branch from origin/main:
   - Branch name: feat/<short-slug> or fix/<short-slug>
2. Before coding:
   - Run: git fetch origin && git checkout -b <branch> origin/main
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



- Create a new branch from origin/main: feat/<slug>
- Make minimal, reviewable commits using the commit template.
- Run: format, lint, tests before committing.
- Show me `git diff` before staging.
- Push branch and open a PR with a detailed description (Why/What/How to test/Risks).
- Never push to main; never force-push unless I say so.

# Git 命令使用规范：

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

## Current Develop plan：see in DEVELOPMENT_PLAN.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is a **Unity 2023.x** project implementing the **Mage Knight** (魔法骑士) board game system. The project is structured as a digital card game with separate layers for Unity-specific code (MonoBehaviours) and pure game logic assemblies.

## Architecture Overview

### Project Structure

```
Assets/
├── Scripts/              # Unity MonoBehaviour components
├── Logic/                # Pure C# game logic (separate assembly)
│   ├── Core/            # Constants, enums, utility classes
│   ├── Data/            # Data models and JSON loading
│   ├── Runtime/         # Game systems, state management
│   └── CardEffects/     # 100+ card effect implementations
├── Tests/               # NUnit tests for game logic
├── GameData/           # Card assets and ScriptableObjects
├── Prefabs/            # Unity prefabs (CardView.prefab)
├── TextMesh Pro/       # UI and fonts
└── AddressableAssetsData/
```

### Assembly Structure

- **MageKnight.Scripts**: Unity components (Monobehaviour)
- **MageKnight.Logic**: Pure C# game engine (referenced by Scripts)
- **MageKnightTests**: NUnit tests (references Logic only)

## Key Systems

### Card System

- **CardSO**: ScriptableObject base for all cards (Action, Spell, Item)
- **DeckRuntime**: Unity component for managing draw/discard piles
- **HandManager**: Unity component for hand visualization
- **CardRuntime**: Runtime card instance for UI display

### Addressables

- All card art loaded via Addressables (`ImagePath` in CardSO)
- Cards are assigned unique IDs and organized by `CardSet`

### Game Flow

- **TurnEngine**: Manages player turns
- **RoundClock**: Tracks day/night cycles
- **ScenarioController**: Manages overall game state

## Development Commands

### Unity Build & Test

```bash
# Build for Windows
Unity -quit -batchmode -executeMethod UnityEditor.BuildPipeline.BuildPlayer -projectPath . -buildTarget Win64 -buildPath ./Build/MageKnight.exe

# Run Unity tests
Unity -quit -batchmode -runTests -projectPath . -testResults ./Tests/results.xml -testPlatform editmode
```

### NUnit Tests

Tests use Unity Test Framework 1.3.9:

- **Test location**: `Assets/Tests/`
- **Test Type**: Edit mode tests only
- **Categories**: Combat, Exploration, Mana, Market, Terrain, PlayerState
- **Run tests**: Use Unity Test Runner or use `Unity -batchmode -runTests`  

### JSON Data Loading

- Card definitions in `resources/text_json/*.json`
- Monster definitions in `resources/text_json/monster.json`
- Place definitions in `resources/text_json/place.json`
- Load via `CardJsonLoader` and `MonsterJsonLoader`

### Unity Packages

- **Addressables**: 1.21.21 (for dynamic asset loading)
- **Input System**: 1.7.0 (new input system)
- **Test Framework**: 1.3.9 (Unity NUnit integration)
- **Universal Render Pipeline**: 16.0.6 (graphics)
- **Newtonsoft.Json**: 3.2.1 (via NuGet package)

## Key Classes

### Core Components

- `CardSO`: Base card definition (ScriptableObject)
- `DeckRuntime`: Unity-side deck management
- `HandManager`: Unity-side hand/card display
- `GameEngine`: Core game engine (Logic assembly)
- `ManaPool`: Universal mana system
- `BattleResolver`: Combat system

### Data Models

- `DamagePacket`: Combat damage/mana calculations
- `UnitCard`: Unit entities
- `SpellCard`: Spell definitions
- `ActionCard`: Player actions
- `PlaceData`: Map locations

### Game Systems (Runtime)

- `MapState`: Hex map management
- `MonsterSpawningService`: AI entity spawning
- `RecruitmentService`: Unit recruitment system
- `TurnMachine`: State machine for turns/phases

## Card Effects System

The project includes 100+ card effect implementations:

- Located in `Logic/Runtime/CardEffects/`
- All implement `ICardEffect` interface
- Effects are loaded via factory pattern (`CardEffectFactory`)
- Examples: `FireballEffect`, `HealEffect`, `ManaDrawEffect`

## Game State Flow

Based on gameProcedure.md documentation:

1. **Scenario**: Master game controller
2. **Day/Night**: Affects terrain costs, mana availability
3. **Round**: Phase management with tactical card selection
4. **Turn**: Individual player actions and combat
5. **Combat**: 5-phase combat system (ranged → block → assign → melee → end)

## Testing Strategy

- **Unit Tests**: Pure C# game logic (Logic assembly)
- **Integration**: Unity components combined with Logic
- **Test Data**: JSON-based test fixtures in test files
- **Categories**: Use `[Category("combat")]` attributes for selective testing

## Development Notes

- **Language**: Mixed Chinese/English (Card names in Chinese, code logic in English)
- **Card Sets**: Union/Archmage/Thracian expansion cards supported
- **Art Assets**: PNG files organized by card type in `GameData/cards/`
- **Editor Tools**: Custom importers in `Editor/` folder for card data

## Project Setup

1. Open in Unity 2023.x
2. Import Addressables package
3. Build Addressable assets: `Window → Asset Management → Addressables Groups`
4. Generate deck starter cards: Use CardImporter tool
5. All JSON data is automatically imported on startup