# 可复用冒险：内容配置与操作说明

四个原场景 `Assets/Scenes/Part1/Part1_*Rules.unity` 仍可直接打开。运行后右上角“选择冒险”列出五份配置：原四个流程，加上林间灰狼遭遇。抽牌场景未改动。

## 为什么现在可以复用

角色、怪物、部队各自一张透明立绘；场景只放空背景，角色通过 `ActorView.prefab` 叠加，并以归一化落脚点对齐。同一英雄用于三个遭遇，同一灰狼用于森林和村庄。美术文件里不包含按钮或规则数字。

| 层 | 位置 | 职责 |
| --- | --- | --- |
| 原始美术 | Assets/Resources/Adventure/Art | 6份独立角色/怪物PNG，森林/村庄/山谷背景 |
| 角色与怪物 | Assets/Resources/Adventure/Actors | 名称、立绘、护甲、攻击、格挡、名望、能力、招募费用/地点 |
| 地点 | Assets/Resources/Adventure/Locations | 背景和英雄/敌人/招募伙伴落脚点 |
| 卡牌绑定 | Assets/Resources/Adventure/Cards | 只引用既有 ActionCardSO 和原卡面，不定义新数值 |
| 原卡数据与图 | Assets/GameData/CardsAssets、Assets/GameData/cards | 原有名称、ID、基础/强化文字、耗色、完整卡面 |
| 原卡效果 | Assets/Logic/Runtime/ActionSystem.cs、CardEffects | 执行既有牌效；OfficialActionAdapter 只转换当前阶段选项与资源池 |
| 遭遇 | Assets/Resources/Adventure/Encounters | 地点与敌人引用；英雄由冒险配置统一指定 |
| 冒险 | Assets/Resources/Adventure/Scenarios | 英雄、牌序、地图、初始资源、招募列与遭遇 |
| 可复用视图 | Assets/Resources/Adventure/Prefabs/ActorView.prefab | 角色图片、选中状态、姓名与数值 |
| 规则模型 | Assets/Logic/Runtime/Adventure | 卡牌实例、目标校验、资源支付、阶段推进、战斗累计与结算 |
| 共享界面 | Assets/Scripts/Adventure/Presentation | 地点组合、手牌、目标检查器、统一确认按钮 |

## 新增怪物或替换场景，不改UI代码

1. 将新的透明立绘放入 Art；Unity 导入为 Sprite (2D and UI)，开启 Alpha Is Transparency。背景使用独立、无角色图片。
2. Project 右键 `Create > Mage Knight > 角色或怪物`，填唯一 id、显示名、artwork、护甲/攻击/名望/能力。也可复制已有 Actor 资产再改，勿复用相同 id 表示不同角色。
3. 创建或复制“地点背景”，选择背景图，设置落脚点。`visualScale` 在角色资产中调整体量；角色纵横比保持不变，图片底部对齐，横向狼图不会悬浮在人物腰部。
4. 创建“遭遇组合”，指定地点和敌人数组。敌人个数不要超过地点 `enemyAnchors` 数量；当前示例布局预留2个敌人和3个招募位。
5. 复制现有“冒险配置”，改唯一 id、displayName、hero、encounter 和 deck。将文件放入 `Resources/Adventure/Scenarios`；运行时菜单会自动读入。新英雄的图像与护甲均来自同一份冒险 hero 引用。
6. 在新会话开始时，资源定义转换为规则快照；运行时扣牌、伤口和名望不回写共享 ScriptableObject。因此重开或切换冒险不会污染其他场景。

例如复制 `forest_wolf`，只更换 location 为 village，即可得到现有 `village_wolf` 的组合。修改 Actors/wolf 的 armor 会同时影响引用它的新开遭遇，无须改画面或按钮逻辑。

`Tools > Mage Knight > Adventure > Build Reusable Content` 补缺失角色、地点和Prefab，并重建本轮的原卡教学配置；Art 下 PNG 会重新应用 Sprite 导入设置。

`Use Existing Mage Knight Cards` 只重建五份教学冒险的原卡绑定、牌序、初始晶体和目标说明，会覆盖这些教学字段；保留角色和地点设计。不要对已有自定义冒险盲目运行迁移菜单。

## 操作原则

- 选择卡牌或部队不扣费。点选敌人、地点或招募伙伴后，再用右侧主要按钮确认。
- 移动/影响牌先产生资源，随后确认进入地点或招募；目标动作与出牌费用分开，失败不会重复扣牌。
- 卡牌按独立序号消耗。同名卡的两份实例能分别使用，同一实例不能重复使用。
- 决心、狂怒的基础效果都可选择攻击或格挡2；由战斗阶段决定所选分支。决心的强化只提供格挡5，狂怒的强化只提供攻击4。
- 非伤牌可明确横置，提供当前阶段的移动、影响、普通格挡或普通攻击1。原卡效果不适用当前阶段时，选中牌会明确预览“横置1”，确认前可以取消。伤牌不可打出或横置。
- 无选中卡牌时，主要按钮自动变为结束格挡、结算攻击、招募或结束回合；无需额外一排调试按钮。
- 强化确认时按原卡的 RequiredCrystals 支付魔力，先用临时标记，再用晶体。行进耗绿、耐力/决心耗蓝、承诺耗白、狂怒耗红。缺魔力时拒绝，不降级执行基础效果。
- 规则说明和最近行动记录默认收起，展开后可以滚动。

## 规则与美术如何对应验收

`AutomationConfigs/adventure_*.json` 保存独立固定预期；运行器通过射线命中真实按钮，记录卡面 Sprite、实际 TMP 效果文字与读数、点击前后状态和 PNG。例如决心基础格挡2不足以挡住攻击4，英雄护甲2，获得2伤；决心强化格挡5加横置1达到迅捷需求6；狂怒强化4加基础2达到攻击6。

`OfficialAdventureCardsTests` 检查每张卡的绑定确实是原 ActionCardSO 和原图，并独立核对五种原卡名称、ID、耗色与基础/强化数值，验证分支限制、横置和支付顺序。

全流程由 `tools/build_adventure_acceptance.py` 和 `tools/summarize_adventure_acceptance.py` 重现。报告移到 `AutomationOutputs/OfficialCards/20260910`；旧目录保留历史，不作为本轮正式卡牌接入证据。

## 后续扩展边界

当前为二维桌面内容组合器与规则操作框架，没有3D骨骼动画和正式存档。地图是固定、确定性的单格地点配置，当前视图适合示例坐标范围；不是完整七格地块拼版或自由大地图。战斗实现格挡后近战，未将远程、攻城、全部技能/卡牌效果接入此界面。招募单位可定义数值，但当前界面只接入部队格挡能力，游侠攻击、学徒治疗需要后续行动扩展。

本轮十张教学牌组是行进、耐力、承诺、决心、狂怒各两张，固定排序用于验收，不是某个英雄的16张初始牌库。七种自制行动卡已移出 Assets，归档于本轮输出。原卡库的其他卡、技能和图像保持原样；复杂抽牌/治疗/弃牌/魔力源及高级牌、法术尚未接入此场景交互，不能仅填写一个数值冒充原效果。

新增卡牌必须引用原 SO、原图及原 ActionSystem 效果；给 OfficialActionAdapter 增加与目标/阶段/牌区相匹配的适配，并新增独立的牌面—效果—UI验收。当前守卫、游侠、学徒和灰狼仍是上一轮的示例角色配置，本轮只恢复行动卡，不宣称所有敌人与部队已完成正式内容迁移。
