# 原卡主要模式有限差集（2026-09-29，15时后只读复核）

本次仅核对台账、当前按钮与已明确的后续索引。没有改生产、运行后台/Unity、生成验收配置或新增通过数。按 `basic_card_acceptance_index.json` 的最终121/121为历史基线，不能沿较早 actions 审计中的 partial/needs_basic_verification 抹去后续补证。

## 结论与优先级

下一组建议合并 **学习强化026、修行基础027、法术熔炉强化033、学习实际取得的魔力风暴基础022**，四个原主要模式同一真实玩家串联。它们的正式后台均已有通过证据；真实缺口是9/23后当前PVE选择面板及连续取得/抽牌/消费路径尚未取得这些模式的场景证据，**不是四张原卡规则尚未通过**。无需先重跑全部旧后台；先为实际合并路线补一个真实状态预演，只检查该路线新的依赖与消费。

鲜血仪式强化012留下一相容资源路线，不为了凑覆盖强塞到本次供应路线。神游指定两张的旧remaining不再直接作为排队理由：9/29宝典材料路线已有真实指定弃牌、抽回和移动消费者，直接物理法术的费用继续复用历史；如需补直接入口，先明确相对这两套证据到底新增了哪项，而非重测同一回收逻辑。

## 原卡有限证据与当前具体缺口

| 原模式 | 可复用原场景证据 | 当前PVE仍需的操作证据 |
|---|---|---|
| 学习026强化 | actions审计选例 `advanced_card_026_07`，`AllOriginalCards/20260917_130956_640c8866/advanced_card`；真实支付9/取得/次数和回合清理 | 白费强化影响4，再用真实影响来源攒到至少9；实际供应选择→入手→本回合直接使用取得牌。基本效果不能重新标未过 |
| 修行027基础 | `_02/_04/_06/_08`，`AllOriginalCards/20260917_122612_525de5fe/advanced_card`，同色真实材料永久去除/取得入弃；最终121索引gaps为空 | 当前材料/供应面板选择，不扣强化绿费；实际取得进入弃牌，正式换轮抽回消费，移除材料不回来 |
| 熔炉033强化 | 最终121索引 `_02/_03`，`20260917_224720_2aed42b5/advanced_card`及`20260917_225701_9b61de63/advanced_card`，真实供给选色与所得晶后续付款 | 当前面板同时选两张不同实际法术，支付蓝费，供给不移走，所得晶支付本路线的原卡/实际移动或攻防 |
| 风暴022基础 | 最终121补证 `_02/_03/_04`，`20260918_025819_49226360/advanced_card`，原源骰得晶/六面重掷及消费者 | 当前面板选择实际基本色源骰；取晶后同骰回源。优先作为学习当回合取得的原卡消费，随后消耗所得晶；不是重做所有骰面 |
| 鲜血仪式012强化（后续） | 最终121补证 `_02`至`_05`，`20260918_021452_3cac5217/advanced_card`，三色、放弃/新产/旧色转晶及真实伤牌消费者 | 当前PVE三行独立选色、可选转晶输入、红印刷费、实际手伤与后续使用；9/23 PVE仅基础路径，不可把后台True误称此新UI场景通过 |

上述 AllOriginalCards 路径均位于 `AutomationOutputs/`。后续通过覆盖旧失败范围，不改写旧报告。

## 下一组四卡连续路线（设计，尚未执行）

1. 合法单英雄构筑，复用真实高级供应与法术供应。可从已有 `build_pve_supply_deeds_acceptance.py` 种子0路线继承供应事实：高级 `005,022,003`；法术 `004,023,000`。先核选定loadout当前手牌/资源和正式等级，不沿用旧生成器默认资源为正式标准开局。
2. 熔炉强化从实际法术供给选两个不同实体，支付蓝色印刷费，记录两个颜色晶体实际增加、供应顺序/引用不变。选色是否足以支付后续白色费用应由预演核定；不通过设置资源伪造。
3. 学习强化4影响＋承诺强效4＋威吓基础2可得到10，实际支付9剩1；两笔白色印刷费用分别支付。选择当时仍在供应的风暴022加入手牌，立即从该真实手牌打开基础选骰面板，得晶重掷；再用所得晶强化原行进/耐力/狂怒之一，实际移动或完成原敌攻防。辅助原牌008/022是基本卡ID，不与高级022混淆。
4. 修行基础选实际同色普通行动材料，永久移除，实际同色高级供应进入弃牌。与学习先后引发的供应补位严格按当前offer选择，不硬编码已不存在的旧位置。
5. 正式结束回合/弃牌、轮末宣告、换轮洗牌/补牌，保留同一玩家与所有实体。取得牌若未在首5张出现，通过正常后续回合补牌取得；不可将其直接塞回手牌。使用取得牌完成第二次实际消费或战斗，核永久移除材料仍不在任何可抽区。

为了可靠执行，下一步先以真实状态预演确定整个构筑大小、初始手牌窗口、供应补位及哪枚晶体用于哪个消费者。上面不是已通过脚本，不能直接计入场景覆盖。

## 后台直接可复用索引

- `pve_supply_deeds_acceptance_index.json`：`AutomationOutputs/RulesState/20260923_075415_8d6b81d4/results.xml`。
  - `PveSupplyDeedEntryTests.LearningOptionalPurchasePaysOriginalInfluenceAndPreservesSupply(True)`。
  - `PracticeOriginalSameColorMaterialAndSupplyGoToRealZones(False)`。
  - `SpellForgeUsesActualDifferentSpellsWithoutTakingThem(True)`。
  - 同类已有拒绝守恒，不重复扩展颜色组合。
- 同索引 `20260923_075716_3f14f033` 有旧连续供应路线；保留但它使用的是026基础/027强化/033基础，不能当本路线通过。
- `pve_deed_inputs_acceptance_index.json`：`20260923_072400_47a3fe82` 的 `PveDeedInputTests.StormUsesActualSourceAndExtraClaims(False)`、`BloodRitualRealWoundAndIndependentColorsPayMovement(True)` 均已有Passed。
- `pve_tome_materials_acceptance_index.json`：85步/151断言是9/29既有材料神游连续证据，非本次新通过。

## 已核实的实际按钮

以 `Canvas/BattleTable/` 为根；手牌按实际可见ID/引用选择，避免复用易变的旧序号。

- 战外：`Btn_JourneyHand` → `JourneyHand/Btn_Hand_<index>`；`JourneyHand/Btn_OutsideChoice_strong`/`base` → `Btn_PlayOutside`。
- 共用输入：`DeedInputs/Btn_InputChoice_strong`/`base`，`Btn_InputOffer_<actual offer index>`，`Btn_InputMaterial_<actual filtered index>`，`Btn_InputDie_<actual source index>`，`Btn_ConfirmInputs`。
- 学习：`JourneyHand/Btn_LearningSupply` → `LearningSupply/Btn_LearnStrong` → `Btn_InputOffer_<index>` → `Btn_BuyLearning`。购牌前可用 `Btn_CloseLearning` 返回攒影响。
- 鲜血：`DeedInputs/Btn_InputColor_0_<Color>`、`_1_`、`_2_`，`Btn_InputCrystal_None`或`Btn_InputCrystal_<basic color>`；不是单色循环器。
- 正式生命周期：战果内 `Inspector/Btn_EndTurn`；战外根 `Btn_BeginTurn`、`Btn_Announce`、`Btn_EndRound`、`Btn_PrepareRound`。必须依实际阶段选按钮。

源码：`PveBattleController.DeedSupply.cs`、`DeedInputs.cs`、`JourneyHand.cs`；后台 `Assets/Tests/RulesState/PveSupplyDeedEntryTests.cs`、`PveDeedInputTests.cs`。

## 部队与神器差集约束

- `unit_basic_entry_audit_2026-09-29.md` 后续13:02已关闭供应分页/非首攻击槽缺口；勿重复。尚未定位完整场景的主要模式集中在大师四能力、四勇者各色能力、巫师去城防/去抗性、幻术师停攻/影响/白晶、护林人真实减费和魔宠各色能力。它们应另按合法指挥槽/费用相容构筑合并；“有限索引未定位”不等于已证实规则失败。
- `artifact_basic_entry_audit_2026-09-29.md` 已由12:50至14:31增量关闭六旗取回、宝盒武器/材料/恐惧、宝典材料/世界/重生多单位/荣誉分页/多人018共用输入。头环决斗另由最新 `headband_duel` 证据覆盖；其余翻面/傀儡/混乱由主代理专项处理。
- 普通资源/材料神器另一面的“需映射历史”不是必须重跑清单。25神器已在121历史有限验收中，若无新输入或生命周期变化且历史有实际消费者，保留通过；只有明确差异才加入下一组。
- 原后续地图供应/探索缺真实来源，与这组当前可执行四卡无关，不阻止先补已能操作的路径，也不伪称完整地图完成。


15:47最终：供应两路线138有效操作227断言，六图已看。learning前25步旧证据+补25..71；practice前65步旧证据+154515_7c5dff05末65八断言一图。原材料永久去除、原盾正式跨轮抽回、实际格挡减甲后击杀、手牌0弃牌5与末选0全部通过；初次失败均保留。最终Console双0 tmp/pve_supply_primary_final_console.json，配置恢复原字节，无fixture修改。加护林人/幻术师95/139，本轮共233有效操作366断言、10关键图。见两份acceptance_index；新后台2/2，复用四原卡和两类部队原后台，无整库回归/无push。整体目标仍active。

下一组有限队列：鲜血仪式强化实际独立三色/可选转晶与消费者；部队幻术师停攻/巫师去城防与抗性、四勇者原能力和大师四能力。护林人森林减费及幻术师白晶/影响已由本批连续线补过。头环傀儡/混乱及后续原地图仍保留，勿将本批完成泛化。


16:07增量：鲜血仪式强化与幻术师停攻80/133、同大师四能力及蓝勇者冰火格挡107/176场景通过，七图/Console双0，同单位正式生命周期已核对；详见两acceptance_index。本次已通过范围退出执行队列。巫师去城防/抗性、其余颜色勇者、头环傀儡/混乱及原后续地图仍未完成；不把巨鹰去城防写成抗性场景。


16:23增量：巫师两种去除能力实际远程消费者、红/绿/白勇者专有能力已通过本轮两路线220/341，证据pve_sorcerer_white_hero_acceptance_index.json与pve_red_green_heroes_acceptance_index.json。和既有蓝勇者证据合并，四颜色专有能力退出待测；共用自然交涉若尚无历史消费者仍需核对，不泛化。敌人已移除显示定向补验两处，最终Console双0。
