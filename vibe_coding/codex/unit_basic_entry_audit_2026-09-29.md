# 30种原部队基本入口与既有场景证据审计

2026-09-29。只读有限审计，未运行测试、Unity、网络，未重新验图或重算旧报告哈希。来源是实际原JSON、当前正式入口及下列已保存验收索引/工作记录；“有入口”“有某分支场景证据”“整张基本验收通过”严格分开。

## 总结

2026-09-29 17:00更新：逐ID表已合入当天大师、四勇者、巫师、幻术师、护林人、魔宠和奇袭连续消费者。以下旧基线统计是历史记录，不是当前缺口；最终完整世界招募和探索仍独立未完成。

真实范围为 `resources/text_json/army.json` 的 army_000—014 与 `army_2.json` 的 army_2_000—014，共30种；015空槽不计。`UnitJsonLoader.SupportedIds`、场景unitCatalog和 `Assets/GameData/BattleDemo/Units` 已有这30种原绑定，原图来源为 `Assets/GameData/cards/<id>.png`。目录存在不等于全部能力通过。

既有19/30“有限基本战斗场景”基线有明确索引，应该复用。其余11种中，斥候私看、奇袭第三能力、魔宠真实林地招募/移动/跨轮维持已取得后续局部场景证据；**不能仍说这些入口完全没有，也不能据此改为30/30**。此处是当日早期基线：上述单位的主要后续消费者现已补入下表与 unit_primary_evidence_ledger_2026-09-29.json；只有账本明确列出的未验/待映射模式仍保留。不得由早期19/30推断当前尚缺11种。

## 当前共用入口

- 自有战斗：`PveBattleController.cs` 的 Units/Inspector → `PveBattleSession.UseUnit` → `CardUnitActions.Use`；包含实际费用、元素、目标、耗竭。大师有专属多能力续用规则；奇袭自有路径有实际攻击槽。
- 自有战外：`JourneyHand/Btn_OwnedUnits` → `PveBattleController.OwnedUnits.cs` → `PveJourneySession.Units.cs/UseOutsideUnitCore`。已列 Movement、Influence、MovementOrInfluence、产魔/晶体/选色、治疗、重整、全抗性、私看；真实阶段与材料仍由正式实现校验。旧20260923“完全无战外入口”结论已过时。
- 自然招募：`JourneyInteraction` 已有村庄、原林地魔宠、自有已征服要塞及已征服魔法塔/城市正式入口。村庄、林地、魔法塔和要塞均已有真实场景消费者；后两者分别见 `pve_natural_site_recruitment_acceptance_index.json` 与 `pve_keep_recruitment_acceptance_index.json`，包含同守护者魔像招募后攻击/跨轮蓝格挡。城市真实招募场景仍未定位。修道院原地图与服务入口仍缺，见专属修道院审计。合法HeroBuild能力验收仍与自然招募分开。
- 供应借能力：`OfferedUnits` → `PveJourneySession.OfferedUnits.cs/UseOfferedUnit` → `CardUnitActions.UseFromOffer`。这是独立路径，不能拿自有部队场景替代。

## 逐ID核对

下表“旧基本证据”只表示所引索引明确声明的有限范围；共用自然招募/世界地点未逐行重复列为失败。

| 原ID / 原名称 | 当前正式入口 | 已定位场景证据 | 明确剩余基本范围 |
|---|---|---|---|
| army_000 阿尔蒂姆守护者 | 战内攻5/迅捷挡8、绿费全抗；战外全抗通道 | altem_guard | 战前全抗的独立连续消费者尚未在本审计定位；旧战内范围复用 |
| army_001 阿尔蒂姆法师 | 选两魔力、双色攻挡、黑费两转换；战外选色 | altem_mages | 战前选色跨战斗证据未定位；旧三类原能力范围复用 |
| army_002 冰霜剑士 | 原攻挡、蓝费真实目标停攻减甲 | ice_swords | 旧基本范围已有；不扩大到全部免疫组合 |
| army_003 烈焰枪手 | 原攻挡与红费火远程 | unit + remaining_units | 旧基本范围已有 |
| army_004 投石车 | 三种原攻城费用/元素 | elite_golem | 旧基本范围已有 |
| army_005 黛尔法纳大师 | 四原能力及逐能力续用 | pve_masters_blue_hero_acceptance_index.json，107/176 | 白去城防/绿减甲、蓝停攻、红实际完整格挡击杀及跨轮同实体已验；城市自然招募另列世界缺口 |
| army_006 火焰魔像 | 普通攻挡、红费火远程 | elite_golem | 旧基本范围已有 |
| army_007 火焰法师 | 火远程、红攻挡、红token+晶体；战外产魔 | mage；outside_units产魔路线 | 已补战前真实产魔消费，旧战斗范围复用 |
| army_008 勇者（蓝） | 普通攻挡、蓝冰火挡、影响5 | pve_masters_blue_hero_acceptance_index.json | 蓝原费冰火格挡8实际消费已过；共同影响5/声誉+1及村庄消费者见heroes_village；不要求四颜色重复 |
| army_009 勇者（红） | 普通攻挡、红冰火攻、影响5 | pve_red_green_heroes_acceptance_index.json，105/168 | 红原费冰火6及正式跨轮免费攻5已过；共同影响5消费者复用heroes_village；精英招募翻倍仍待真实核心供应 |
| army_010 勇者（绿） | 原绿费治疗4、战外交涉 | pve_red_green_heroes_acceptance_index.json | 治疗实际Lv2部队及旧伤、耗竭与创伤独立、跨轮格挡5已过；共同影响5消费者复用heroes_village；精英招募翻倍仍待真实核心供应 |
| army_011 勇者（白） | 白费远程7、战外交涉 | pve_sorcerer_white_hero_acceptance_index.json，115/173 | 白真实费用及远程击杀已过；共同影响5消费者复用heroes_village；精英招募翻倍仍待真实核心供应 |
| army_012 寒冰魔像 | 普通攻挡、蓝费冰攻 | elite_golem | 旧基本范围已有 |
| army_013 寒冰法师 | 冰攻挡、蓝攻城、蓝token+晶体；战外产魔 | mage | 旧基本已含产魔与原卡消费者；未把全部跨回合时机当已测 |
| army_014 巫师 | 远程3、白去城防/绿去抗性 | pve_sorcerer_white_hero_acceptance_index.json | 两实际费用、目标状态和后续远程击杀已过，移除状态显示两处增量补验已过 |
| army_2_000 护林人 | 移动减费及格挡3 | pve_forester_illusionist_acceptance_index.json；pve_shock_forester_acceptance_index.json | 森林原费用与实际格挡3已过；不要求全地形排列重跑 |
| army_2_001 守护者魔像 | 普通攻挡、红火挡/蓝冰挡及原抗性 | unit + remaining_units | 旧基本范围已有 |
| army_2_002 药草师 | 原绿费治疗、免费重整等级I/II、绿token；战外均可 | herbalist；outside_units；headband_outside_unit_payment | 治疗/重整区分及实际消费已有后续证据，复用 |
| army_2_003 幻术师 | 白费停攻、战外白晶/影响4 | pve_forester_illusionist_acceptance_index.json；pve_blood_illusion_stop_acceptance_index.json | 白晶后续付款、同单位重整与影响治疗、白费实际停攻均过 |
| army_2_004 魔宠 | 林地招募、四色能力、库存维持 | glade_recruitment；pve_familiar_consumers_acceptance_index.json | 新161有效操作313断言：红攻5、蓝挡7、白移动/影响5、绿治疗3实际消费及跨轮同实例；放弃维持移除仅旧后台，不扩写本批场景 |
| army_2_005 北地僧侣 | 普通与蓝费冰攻挡 | monks | 旧基本范围已有；头环领导等消费者不需重复 |
| army_2_006 农民 | 攻挡2、影响2、移动2；战内外均可 | commoner；outside_units；journey_interaction | 实际移动/交涉与原承伤付款已有补证 |
| army_2_007 赤袍僧侣 | 普通与红费火攻挡 | monks | 旧基本范围已有 |
| army_2_008 凶蛮僧侣 | 普通攻挡、绿费攻城4 | ranged_units | 旧基本范围已有 |
| army_2_009 斥候 | 攻城1；战外移动2/探索距离2；私看真实地点或供应 | outside_units斥候线 | 私看+同敌击杀奖励已补；原七格两格探索消费者仍缺，不能用固定单格揭示替代 |
| army_2_010 奇袭队 | 远程加值、减攻减甲、强制承伤 | shocktroops_and_roster；pve_shock_forester_acceptance_index.json；offered_unit_selection | 三原能力主要消费者与供应非0攻击槽已有证据；供应强制承伤仍按正式身份拒绝 |
| army_2_011 暴徒 | 格挡3、攻击3/影响4扣声誉、影响费承伤；战外影响 | commoner | 旧OriginalUnits/074532步骤24–27已有整次声誉反转和影响消费；新heroes_village补当前战外影响4及实际购买治疗 |
| army_2_012 乌顿弩手 | 普通攻挡、远程2 | ranged_units | 旧基本范围已有 |
| army_2_013 乌顿守卫 | 攻2、迅捷双倍格挡4 | utem | 旧基本范围已有 |
| army_2_014 乌顿剑士 | 普通攻挡3、自伤攻挡6 | utem | 旧基本范围已有 |

## 证据索引（均为现有证据，不是本轮新通过）

以下短名对应 `vibe_coding/codex/pve_<短名>_acceptance_index.json`。已读scope及报告指针，不把单一文字命中当全部通过。

| 短名 | 实际场景报告定位 |
|---|---|
| unit | AutomationOutputs/PveBattle/20260918_155302_7f8eb312/report.json |
| remaining_units | AutomationOutputs/PveBattle/20260918_180816_cda81c32/report.json |
| monks | AutomationOutputs/PveBattle/20260918_172149_14585266/report.json |
| ranged_units | AutomationOutputs/PveBattle/20260918_175803_f99e17ac/report.json |
| mage | AutomationOutputs/PveBattle/20260918_202203_24f618b6/report.json |
| ice_swords | AutomationOutputs/PveBattle/20260918_192940_537a7154/report.json |
| utem | AutomationOutputs/PveBattle/20260918_164602_f66ae9e4/report.json |
| elite_golem | AutomationOutputs/PveBattle/20260918_185453_ebb04442/report.json |
| altem_guard | AutomationOutputs/PveBattle/20260918_214841_1e1458fd/report.json |
| altem_mages | AutomationOutputs/PveBattle/20260918_221422_c30b95fd/report.json |
| herbalist | AutomationOutputs/PveBattle/20260918_205616_e65217a6/report.json |
| commoner | 20260922_115231_36e7bfbd/report.json的0—155有效前缀 + 20260922_115426_03cb450e/report.json补第六场；两者均在AutomationOutputs/PveBattle |
| outside_units | 同根20260929_015129_c40c7493前缀 + 015408_8c473e87斥候补验；015637_7e6e225b农民/药草师；015916_b4e6ac07产魔/治疗；分段范围见索引 |
| journey_interaction | 同根20260923_055818_52ce31a4前缀0—24 + 060015_becfdc12后缀25—103 |

其他具体补证：

- `worklog_2026-09-29_glade_recruitment.md`：`AutomationOutputs/PveBattle/20260929_122149_cee7cf11/report.json`，122操作/216断言；显示补验 `20260929_122510_b80360d5` 仅3检查。红→待维持→蓝状态与同一实体 `army_2_004:1131902944` 有明确报告步号。修正旧outside_units.not_completed中的“familiar actual recruitment”，不重测已过路线。
- `worklog_2026-09-23_shocktroops_and_roster.md`：`20260923_024535_dcde4ea8/report.json`13步35断言补验第三能力；前批有效目录/取消检查按原记录复用。记录明确不覆盖奇袭另两能力。
- `worklog_2026-09-23_delphana_masters.md` 本身早于后续CLI；`worklog_2026-09-23_unity_resume.md` 143例先142通过、补1通过。不能沿早期“全部待执行”反推当前后台失败，也不能由143后台称大师场景已过。
- `pve_headband_outside_unit_payment_acceptance_index.json` / 对应worklog记录药草师真实原绿費消费者，属于补证，不增加药草师种类计数。

## 历史供应入口缺口（13:02已修复并验收，保留发现记录）

1. **当时非0攻击槽没有UI输入**。修复前 `PveBattleController.OfferedUnits.cs` 仅有 `Btn_Target_i` 敌人选择；调用 `UseOfferedUnit`没有传attackIndex。`PveJourneySession.OfferedUnits.cs:9`尾参数默认0并向正式实现透传。自有奇袭槽选择不覆盖此处。针对允许从供应发动的减攻/减甲能力，应选择实际未解决攻击槽；这不是“缺场景证据”而是确切输入缺失。
2. **当时真实供应超过3张没有分页**。修复前OfferedUnits遍历整列，用 `x=40+i*350`，第4张起与右侧能力区冲突，后续可越界；没有Offer页状态。当前 `JourneyInteraction` 和 `SpecialArtifacts` 的分页已存在，`pve_command_offer_pages_acceptance_index.json`只验证指挥旗第四张取得，不覆盖本面板。不能拿该历史通过关闭战斗号召供应分页缺口。

额外原规则边界：`ShockTroopsActions.Validate` 的Intercept需要 `UnitDamageResolver.Validate` 真实自有承伤单位；供应临时单位不属于玩家。应保留明确拒绝，不为“测试通过”开放虚假强制承伤。大师/魔宠禁止号召也属于原限制，不计为需放宽的入口。

## 当前最小下一组（以硬差集为准）

供应分页/非首攻击槽以及后续主要消费者已完成的范围退出队列，复用所有下列有效历史索引。当前逐模式账本为 `unit_primary_evidence_ledger_2026-09-29.json`：30 UnitID、98条模式记录，其中84条有限场景证据、7条需补场景、5条原明确未场景验证、1条部分场景、1条仅后台；共享勇者规则按4个ID重复列示，不是98个独立测试。明确未验为斥候原两格探索及4色勇者共用的精英招募声誉翻倍。待映射先核旧证据，不自动重测；不因缺新PVE截图否定有效旧场景。


13:02增量：供应分页及真实非首攻击槽已接；合法诺若威斯追加供应第4张赤袍攻击、奇袭第2冰攻击减攻与实际双战消费者场景95步169断言通过，后台7有效。见 `worklog_2026-09-29_offered_unit_selection.md`。其余部队覆盖结论不变。


15:37场景已执行：20260929_153451_bcd0d5d3一次95步139断言通过，四图已逐张检查；7处units.instances完全一致（护林人-1457006900、幻术师-1004329182），真实白晶跨回合付款、同单位重整后影响力消费、旧伤2移除、回合末治疗清零均有逐操作证据。索引pve_forester_illusionist_acceptance_index.json。无生产或fixture修改。原停攻、格挡等未覆盖分支仍留缺口。


16:23增量：巫师两种去除能力实际远程消费者、红/绿/白勇者专有能力已通过本轮两路线220/341，证据pve_sorcerer_white_hero_acceptance_index.json与pve_red_green_heroes_acceptance_index.json。和既有蓝勇者证据合并，四颜色专有能力退出待测；共用自然交涉若尚无历史消费者仍需核对，不泛化。敌人已移除显示定向补验两处，最终Console双0。


## 主要模式证据账本收敛

只读原JSON与既有索引，新增 `unit_primary_evidence_ledger_2026-09-29.json`。每个UnitID保留原定义、原美术、印刷能力、正式入口、后台引用、场景消费者及未验模式。索引内历史 limitations 保持原样，由账本新证据明确补齐；失败报告未改写。未运行后台/Unity、未生成截图、未网络传输。当前仍不是30种全部规则/边界通过。

定向映射续审：原8条待映射已查明，魔宠红/蓝调谐基础移动3已有旧林地真实消费者；勇者突袭附费为4ID共用一项需求，不要求四色重复。巫师免费远程3、斥候攻城1/移动2及魔宠其他基础分支未找到确切场景，详见 `worklog_2026-09-29_unit_evidence_mapping.md`，对应后台确切Passed方法已进入账本。
