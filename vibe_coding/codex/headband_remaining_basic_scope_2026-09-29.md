# 头环剩余基本范围：原文边界与静态审计

2026-09-29。只读源码/规则核查；未修改生产代码、未运行 CLI 或 Unity。本文不是场景通过证据。

## 结论

当前不能把“决斗／傀儡／翻面技能全部禁止头环借用”当作正式规则；也不能把 Frequency=OncePerTurn 一律执行两遍当作正确实现。优先保留已明确的普通入口，特殊借用须按来源分别落实。自有技能的主要消费者场景可独立继续，不必等待这些组合。

## 来源与可信度

1. 仓库原牌转录 `resources/text_json/items.json` items_022：普通选择公共供应任意非互动技能；只有每回合可用且不翻面的技能双用。焚毁永久取得与普通借用不同。`resources/text_json/skill.json` 有完整狂乱、仪式、伪装、傀儡、混乱与决斗描述；其通用 `flip:false` 不能覆盖正文中的翻面分支。
2. [WizKids 终极版官方页面](https://wizkids.com/mage-knight-ultimate-edition/) 的 Expansion Rules 链接为 [MKUE expansion PDF](https://eadn-wc03-13179282.nxedge.io/posters/repository/wizkids/MKUE_Expansions_Rulebook_BOOKLET-Online2.pdf)。web 服务因文件 13,661,329 bytes 拒绝解析；本轮没有下载此大文件。
3. 发行规则的数据库镜像 [Krang 2013 两页规则](https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Expansion-Krang.pdf)：索引读到了第 2 页 FAQ（傀儡原数值、召唤、多攻击及休息不能恢复仪式）。未成功读到可证实“头环特殊禁止”的条款。open 返回空文本/超时；一次小下载返回403后停止。不能宣称官方禁止，也不能拿未看见的第1页作证。
4. [Lost Legion 发行规则镜像](https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Expansion-The-Lost-Legion-Rules-2013.pdf)，印刷第9页 Detailed Game Concepts 已读到：头环属于复合效果，必须遵守原效果限制；即时攻防只对应当前单次攻击敌群/单次格挡，余量不转移。这是同攻击/阶段限制的一手发行规则依据，但不是所有 Krang 特例的答案。
5. [2024-04-15 社区 Q&A 汇编](https://www.scribd.com/document/733951911/Mage-Knight-Compendium-of-Q-A-15-April-2024)，Artifacts 85–87 页，Circlet S2/S9/S20/Q12：狂乱2+2同敌群；决斗同敌同攻击格挡2，后续攻击2，合条件名望2；傀儡本战已击败敌可再消费打另一组；混乱临时掷骰后依序两格。这里只是解释线索，不能称为官方 FAQ。S9 脚注743无规则页码，仅网页中不可解析的 Link；S20未见引用。O4忠诚禁止也是作者建议而非官方禁令。

## 必须基本缺口与最小处理方向

| 范围 | 当前源码事实 | 必要边界 |
|---|---|---|
| 战斗狂乱强化借用 | HeadbandEffect 按频率循环；强化首次获得4并写个人 FlippedSkills，第二次拒绝，可能留下部分奖励和未出牌头环 | 必须先消除部分提交风险；不能直接改8或臆造公共技能翻面。普通2+2与强化分支分别记状态；未核强化细则不标通过。 |
| 萨满仪式 | ManualFlip 被 HeadbandEffect 拒绝 | 原卡写非互动可使用，不能因枚举不支持就宣布原规则禁止；需确认一次效果与借用翻面身份后接入。 |
| 奥术伪装 | HeroSocialSkillRules.Borrow 只准普通影响，翻面/恢复明确未接 | 普通双影响保留既有证据；忽略声誉轨分支缺口独立列出，不改为永久取得。 |
| 决斗 | DuelActions.Validate 必须自有，DuelBattleState 仅一真实 Enemy，AddBlock/EnterPhase/TakeDefeatBonus 均固定1 | 同敌同攻击双用较符合复合规则，无需扩展多目标矩阵。须独立借用授权、格挡/近战/名望按同一次真实状态累计；本人次数及所有权不变。 |
| 傀儡 | RecordDefeat 只为持有者记录；Keep/Consume 只准自有；Consume 还拒绝本回合刚保留标记 | 不能临时把技能塞进自有列表。需正式战果捕获、物理标记唯一保留/释放与借用即时消费；“以前保留”不可武断收紧成“以前回合”，本地原文未写后者。社区S20表明这项限制值得定向核实。 |
| 混乱 | HeadbandEffect 明确以未接独立盾标拒绝普通借用 | 这是实现缺口，不是已证实原规则禁用。独立临时轮盘候选解释需一手确认，不能误触永久取得的个人盾标。 |

## 不应扩大的范围

- 暂不做宝盒再头环、多层复制、七十技能与全部目标排列。
- 不把决斗发展为两名敌人的多套跨阶段状态，除非找到相反权威依据。
- 不把普通借用转成永久持有技能，不污染自有次数/翻面/库存。
- 不用重新全卡测试寻找这些已定位缺口；实现后仅新增与实际受影响回归，再在合法连续路线补基本消费者。
- 本轮未取得“Krang一概禁止借用”的官方原文。上述待核不能被编码成无依据禁令，亦不能记为已验收完成。


15:00增量：头环普通决斗已接实际公共技能与同敌同攻击原子双用，格挡2→同敌近战2→合条件名望2；自有保持1，实际单位介入仍取消奖励，预定格挡攻击拒绝。后台20有效；Unity测试internal读取3错已改public查询，受影响10补过，最终Console双0。五原牌合法Wolfhawk真实升级公共决斗连续场景145739_e062eacb一次59步108断言、6图已看，旧伤2/同玩家/技能归属保留。见worklog_2026-09-29_headband_duel_ui.md与pve_headband_duel_acceptance_index.json。145303因延迟导入编译错在步骤前中止，保留未计通过。HornGold严格恢复seed0，无运行测试/代理，无push，goal active。狂乱强化部分结算风险、仪式/伪装翻面、傀儡/混乱借用仍明确未解决；不重复已通过决斗及原卡。


15:24最终：三条路线176操作297断言全部通过：Frenzy 151243_e931c862 51/86，Ritual 151549_1afc5c04 55/94，Disguise 152125_5a7bacdd 70/117。8张主线关键图已目视；Frenzy旧文案“两份攻击”改为“攻击在本次近战中使用”，仅152244_33709e1a重放前44步并补第44步一断言一图，未重复消费者。负声誉-1保留、影响5不扣减→支付3治疗首战伤2至1、正式回合末许可清除均通过。最终Console两次0错误 tmp/pve_headband_flip_final_console.json。HornGold三次精确恢复原字节seed0；无运行测试，无push。见pve_headband_flip_acceptance_index.json。

此批解决狂乱部分提交风险和仪式/伪装翻面入口；普通狂乱option0本批仅后台，普通伪装沿用旧场景。原牌组合解释与正式专项FAQ区分，详见规则决策文档。傀儡/混乱借用仍待；下一优先是remaining_primary_modes_2026-09-29.md所列学习强化/修行基础/熔炉强化/实际取得风暴消费者连续路线，不重跑全卡与无关复制组合。整体目标未完成。
