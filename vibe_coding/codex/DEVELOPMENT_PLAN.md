### **魔法骑士(Mage Knight)项目开发计划**

#### **第一部分：已完成功能模块 (Codebase Review)**

根据对 `Assets/Logic` 和 `Assets/Tests` 目录的分析，以下核心系统已基本完成，并拥有单元测试覆盖，后续工作主要是将它们与UI和完整的游戏流程集成：

1. **核心游戏循环 (Core Game Loop):**
   
   * **已实现:** `RoundClock` (昼夜)、`TurnEngine` (回合)、`ScenarioController` (剧本)。核心的时间和回合管理结构已经搭建。

2. **卡牌与效果系统 (Card & Effect System):**
   
   * **已实现:** `CardSO` (卡牌数据结构), `PlayerDeck` (玩家牌库), `CardEffectFactory` 以及位于 `Logic/Runtime/CardEffects/` 下的 **100+** 个卡牌效果。卡牌的定义、管理和效果执行机制非常完善。

3. **地图与探索 (Map & Exploration):**
   
   * **已实现:** `MapState` (地图状态), `MovementService` (移动), `ExplorationService` (探索), `TerrainCost` (地形). 地图逻辑、移动力计算和探索新地块的后端逻辑已经完成。

4. **公共资源与市场 (Public Resources & Market):**
   
   * **已实现:** `ManaPool` (魔力池), `AdvActionSupply` (高级行动), `SpellSupply` (法术), `ArtifactSupply` (神器)。公共资源和市场牌库的管理机制已就绪。

5. **战斗系统 (Combat System):**
   
   * **已实现:** `BattleResolver` (战斗结算), `DamagePacket` (伤害计算), `RangePhaseResolver` (远程战斗)。核心战斗逻辑和流程已经完成。

6. **招募系统 (Recruitment System):**
   
   * **已实现:** `RecruitmentService` (招募逻辑), `UnitState` (单位状态)。招募单位的核心规则已经编码。

---

#### **第二部分：待开发任务拆解 (Detailed Development Plan)**

以下是将计划中尚未完全实现的功能拆解成的具体编程任务，并为每个主要功能设计了测试场景。

##### **任务模块一：玩家交互与地点功能 (Location Interactions)**

* **目标:** 实现玩家与地图上各种地点的交互逻辑。
* **编程任务:**
  1. **创建交互接口:** 定义 `IInteractable` 接口，包含 `Interact(Player player)` 方法，由所有地点实现。
  2. **村庄 (Village):**
     * 实现治疗功能：花费影响力，恢复单位伤害。
     * 实现招募功能：刷新并提供可招募单位。
     * 触发战斗：如果声望过低，与守卫发生战斗。
  3. **修道院 (Monastery):**
     * 实现深度治疗：花费影响力，治疗重伤单位。
     * 实现技能获取：提供技能牌供玩家学习。
  4. **法师塔 (Mage Tower):**
     * 实现法术获取：提供法术牌市场。
     * 实现魔晶购买：允许玩家购买魔晶。
  5. **要塞 (Keep):**
     * 与领主交互：提升声望、招募精英单位。
     * 触发攻城战：玩家可以选择攻击要塞。
  6. **城市 (City):**
     * 实现所有交互：作为村庄、法师塔、要塞的集合体。
     * 实现攻城战：攻击城市以获得大量声望和战利品。
* **测试场景 (`Scene_LocationTests`):**
  * **场景内容:** 创建一个包含所有类型地点的测试地图。UI面板上有按钮模拟玩家的 `Interact` 动作。
  * **测试目的:** 点击按钮后，在日志中验证声望、影响力、手牌、单位状态的变化是否正确，确保每个地点的交互逻辑符合规则。

##### **任务模块二：UI与视觉表现 (UI & Visuals)**

* **目标:** 将后端的游戏数据和逻辑可视化，提供完整的用户操作界面。
* **编程任务:**
  1. **玩家状态面板 (Player HUD):**
     * 创建 `PlayerHUD.prefab`，包含显示声望、荣誉、护甲、手牌上限、指挥上限的UI元素。
     * 编写 `PlayerHUDController.cs` 脚本，监听玩家数据的变化并更新UI。
  2. **地图渲染与交互 (Map View):**
     * 编写 `MapView.cs`，根据 `MapState` 数据动态生成和摆放六边形地块 `Tile.prefab`。
     * 在地块上正确显示地形、敌人标记和地点建筑。
     * 实现玩家棋子的拖拽移动和路径预览功能。
  3. **手牌与出牌区 (Hand & Play Area):**
     * 利用已有的 `HandManager.cs` 和 `CardView.prefab`，实现从牌库抽卡到手牌的动画。
     * 实现卡牌的拖拽出牌、横置（激活效果）的视觉表现。
  4. **战斗界面 (Combat UI):**
     * 创建 `CombatUI.prefab`，清晰地展示攻击方和防御方单位。
     * 分阶段（远程、格挡、近战）显示战斗流程，并实时更新单位的伤害和状态。
  5. **市场面板 (Market UI):**
     * 为高级行动、法术和神器市场创建独立的UI面板，显示可购买的卡牌。
* **测试场景 (`Scene_UITests`):**
  * **场景内容:** 包含所有核心UI Prefab (`PlayerHUD`, `MapView`, `CombatUI` 等)。
  * **测试目的:** 在 `Start()` 函数中用模拟数据填充UI，检查所有UI元素是否能正确显示数据。测试UI的按钮、拖拽等交互是否能正常触发事件。

##### **任务模块三：游戏流程整合 (Game Flow Integration)**

* **目标:** 将所有独立的系统模块串联起来，形成完整的游戏体验。
* **编程任务:**
  1. **剧本控制器 (`ScenarioController`):**
     * 完善 `StartGame()` 流程：初始化玩家数据、构建初始牌库、设置起始地图板块。
     * 实现游戏结束条件判断：当剧本目标达成或回合数耗尽时，触发游戏结束流程。
  2. **回合引擎 (`TurnEngine`):**
     * 在回合开始阶段，调用 `PlayerDeck.DrawCard()` 补满手牌。
     * 在回合结束阶段，执行弃牌、重置魔力池、单位状态重置等逻辑。
  3. **输入管理器 (`InputManager`):**
     * 监听UI事件（如点击“结束回合”按钮），并调用 `TurnEngine` 的相应方法。
     * 处理地图上的点击事件，触发移动或地点交互流程。
* **测试场景 (`Scene_FullGame_FirstRound`):**
  * **场景内容:** 一个可以完整运行的最小化游戏场景。
  * **测试目的:** 从点击“开始游戏”到完成第一个完整的回合，确保所有流程（抽卡、移动、交互、结束回合）都能无缝衔接，没有逻辑中断或错误。

##### **任务模块四：存档与读取 (Save & Load)**

* **目标:** 实现游戏进度的保存和加载功能。
* **编程任务:**
  1. **数据容器 (`SaveData.cs`):**
     * 创建一个可序列化的类，用于存储所有需要持久化的游戏状态（如 `GameState`, `MapState`, `PlayerState` 等）。
  2. **存档管理器 (`SaveLoadManager.cs`):**
     * 实现 `SaveGame()` 方法：收集当前所有游戏状态，填充 `SaveData` 对象，并将其序列化为JSON文件。
     * 实现 `LoadGame()` 方法：读取JSON文件，反序列化为 `SaveData` 对象，并将数据恢复到游戏内的各个系统中。
* **测试场景 (`Scene_SaveLoadTest`):**
  * **场景内容:** 一个简单的游戏场景，UI上有“移动”、“存档”、“读取”三个按钮。
  * **测试目的:** 玩家先进行一些操作（如移动棋子），然后点击“存档”。关闭并重新打开场景，点击“读取”，验证玩家位置等游戏状态是否被正确恢复。
