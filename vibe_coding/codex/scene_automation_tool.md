# Scene Automation Tool 工作记录

## 2025-09-19 设计与实现

- **目标**：实现可由命令行触发的 Unity 场景自动化测试工具，按步骤点击按钮并输出截图/报告。
- **实现概述**：
  - 新增 `MageKnight.SceneAutomation` 运行时代码，`SceneAutomationOrchestrator` 在播放模式中读取配置，依序点击按钮、等待、截图并构建 JSON 报告。
  - 通过 `SceneAutomationRuntimeState` 在编辑器与运行时共享请求与完成事件，`SceneAutomationBootstrap` 在场景加载后自动挂载执行器。
  - Editor 端提供 `SceneAutomationCommand.RunFromCommandLine` 静态入口，解析 `--scene-automation-config` 配置文件参数，打开场景并自动进入播放模式，等待执行完毕后退出。
- **当前状态**：编辑器入口、运行时驱动、截图与报告逻辑全部实现，错误分支会在报告中标记 `failed` 并保留失败截图。

### 配置文件格式示例

```json
{
  "scenePath": "Assets/Scenes/Part1/Part1_FullFlowTest.unity",
  "screenshotsDirectory": "AutomationOutputs/captures",
  "reportPath": "AutomationOutputs/report.json",
  "captureInitialView": true,
  "initialDelaySeconds": 2,
  "defaultWaitAfterSeconds": 1,
  "steps": [
    {
      "buttonPath": "Canvas/Controls/StartTurnButton",
      "label": "Start Turn",
      "waitAfterSeconds": 1.5
    },
    {
      "buttonPath": "Canvas/Controls/ResolveCombatButton",
      "label": "Resolve Combat"
    }
  ]
}
```

- `scenePath`：Unity 项目内的相对路径，默认值可用命令行 `--scene` 覆盖。
- `buttonPath`：传给 `GameObject.Find` 的完整路径或名称，需确保对象激活并挂有 `Button`。
- `waitAfterSeconds`：单步点击后额外等待时间；未填写时使用 `defaultWaitAfterSeconds`。

### 使用命令行运行

```bash
"<Unity_Editor_Path>" -batchmode -projectPath "<项目根目录>" \
  -executeMethod MageKnight.SceneAutomation.Editor.SceneAutomationCommand.RunFromCommandLine \
  --scene-automation-config "<配置文件绝对路径>" -quit
```

- 可选 `--scene <Assets/...>.unity` 覆盖配置中的场景路径。
- 工具运行结束后将在 `reportPath` 指定位置写入报告，同时在控制台输出每个步骤的截图路径。

### 后续工作

- [ ] 与 UI 构建流程配合，编写针对各 Part1 场景的示例配置。
- [ ] 结合自动构建脚本，把报告复制到统一的测试归档目录。
- [ ] 通过 Unity CLI 实测，验证在 batchmode 下截图分辨率与 UI 渲染是否满足要求。

## 2025-09-22 自动化运行验证

- 生成 `AutomationConfigs/part1_deckmana.json`，配置 Part1 Deck+Mana 测试场景按钮顺序。
- 新增编辑器菜单快捷项，可直接从 Unity 内部加载项目相对路径配置。
- 运行 `Tools/Scene Automation/Run DeckMana Automation`，产出报告 `AutomationOutputs/DeckManaTest/report.json` 与截图 `AutomationOutputs/DeckManaTest/captures/*.png`，步骤全部成功完成。
- Unity Console 捕获到 `SceneAutomation` 日志，确认按钮查找、点击与截图流程按序执行。

## 2025-09-22 字体修复与复测

- 使用 `Tools/Fonts/Generate Dynamic MSYH Font` 菜单生成动态 TMP 字体（基于 `msyh.ttc`），自动写入 `Resources/Fonts/msyh TMP Dynamic.asset`，供运行时加载。
- `Part1SceneHarness` 运行时优先加载该动态字体，确保日志面板、按钮等中文文本不再出现缺字警告。
- 重新执行 `Tools/Scene Automation/Run DeckMana Automation`：
  - 报告：`AutomationOutputs/DeckManaTest/report.json`，状态 `success`，步骤包含初始截图及两次按钮点击。
  - 截图：`AutomationOutputs/DeckManaTest/captures/000~002`，用于验证 UI 变化。
  - Unity Console 未再出现 TMP 缺字警告，仅首轮载入时提示一次 Importer 警告（动态字体 atlas 运行期写入），后续运行无阻。

## 2025-09-27 等待逻辑调整

- DeckMana 自动化在播放模式下卡在初始延迟，定位为编辑器开启 Pause on Play 导致进入播放立刻暂停；在入口禁用 Pause on Play 并将配置等待时间压缩，使用逐帧轮询后流程可持续执行并生成截图/报告。
