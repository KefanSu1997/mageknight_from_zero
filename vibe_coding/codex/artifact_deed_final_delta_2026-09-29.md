# 神器 / 行动法术最终有限差集审计（2026-09-29）

只读主账本、小型后续 index 及其三组关联场景报告。本次没有运行后台/场景、联网、改共享账本或生产源码。本文仅给映射建议，不能作为新测试通过数。

## 结论：真正执行差集仍只有两处

1. **items_022.normal：公共混乱大师普通借用。** 现账本已正确保存 `finite_primary_evidence_retained_with_known_demo_gap`。初始化原依据仍未取得；本人混乱、焚毁取得、金色选择已验不能代替。继续保留 `summary.knownCurrentDemoGap` 和该面的 `unverified`，可附本次 `headband_chaos_bounded_source_audit_2026-09-29.md` 作为来源未决记录，不得升级为场景通过或规则禁用。
2. **magic_016.base：可核验原后续七格板块的实际两格探索。** 账本旧供给板块77用例支持旧局部探索逻辑，不能代替当前原板块供应/接图。保持 `remaining`、`needs_scope_or_dependency_review` 与 `completion=false`。增强时光弯曲、基础两格移动/肆虐豁免已有明确历史证据，不重新排队。

除此之外，本次没有发现由已读证据支持的新增“必须重做”主模式。不要把地图 Core 对精英招募的依赖再塞给25神器或全部96张行动/法术；它属于与探索共享的世界/部队依赖。

## 账本当前事实与保留字段

- 神器25行、50面都有保留主要场景；除022普通面的上述独立输入缺口外，各面 `unverified=[]`。50是保留证据行数，不等于当前所有动态输入完成。
- 行动/高级行动/法术96行均有来源和base/enhanced案例；95行 `verified_selected_effect_and_ui_records`，只有016是scope/dependency review。`unresolvedSelectedLinks` 全空。
- 旧expected漂移仅 `advanced_card_010_01` 与 `advanced_card_035_00/01`。010已有后续02/03，035已有02–05和失败批次已审计前缀。保留 `historicalExpectedDrift` 和旧案例值，不清空历史、不因此重测。
- deed 的 `actualConsumerEvidence` 目前只有000填入旧Exploration报告；其他行大量已经由 `cases`、`subsequentEvidence`、`finiteBasicEvidence` 证明局部真实消费者。该数组空本身不是失败。当前需要补后续索引映射，不能先把95行判未验。

## 已有新证据、逐ID建议更新（不用重新运行）

下列 index 晚于两账本，且三组实际报告路径/哈希已读核对。只追加到对应ID/模式，不把同一整批全部操作算给每张卡。

| ID / 模式 | 建议字段更新 | 已有确切消费者 |
|---|---|---|
| items_015.normal | `modes.normal.currentPveIndexes` 追加 `pve_polarization_special_primary_acceptance_index.json`；registry写该index及当前哈希；保持原status | 同原护符普通白昼生黑、正式跨夜轮后生金；两极支付真正白费/黑费。89总操作142断言的报告190632，046/048敏捷付款和击杀，081/083火焰攻城付款/伤牌/击杀。原护符次数与个人两极次数分开。 |
| items_015.burn | `modes.burn.currentPveIndexes` 追加 `pve_heroes_assault_fee_acceptance_index.json`；保持原status | 184358第039焚毁得到黑1白2和昼黑许可，045实际绿黑地道，051勇者实际白1与影响2费用。不是只看token增加。 |
| items_022.normal | 仅 `unverified` / `mappingNote` 引用本次有界来源审计；status和knownGap不变 | 没有新增借混乱通过证据。已有傀儡/翻面/决斗等索引继续复用。 |
| basic_card_007.enhanced | `actualConsumerEvidence` 或单独明确PVE overlay追加 polarization index 046/048 | 真实原敏捷从手牌进已打出，白费用两极消费护符黑token，远程0→3→0，名望15→17。不能把这里当作没有两极时也能随意黑付白。 |
| magic_009.enhanced | 同上，polarization index 081/083 | 夜间原红晶1和两极金token1真实消费，攻城8实际击杀、真伤1保留；牌区中真实magic_009。 |
| basic_card_008.base | 追加 heroes_assault_fee index 的承诺→051消费片段 | 承诺基础影响2由真实手牌得到，原白勇者启动时2→0；同次白token1消费，随后击杀实际塔守军。 |
| magic_004.enhanced | 追加 heroes_assault_fee index 040–061有限片段 | 045绿黑实际支付、同塔守军进入突袭；地道城防豁免不免单位突袭费；058征服，061正式次回合仍同玩家/单位/位置。 |
| basic_card_010.base | 追加 elusive_defend index 027、038 | 正式原卡远程1纳入三笔实际7，按Defend后的两真实目标护甲4+3消费。 |
| basic_card_011.enhanced | 追加 elusive_defend index 030、038；保留已有来源分类，不重新宣称其是七英雄标准开局牌 | 实际利斧远程3贡献，击杀两敌所得名望包含该卡独立奖励；旧预期35→40→41修正有保留。 |
| advanced_card_000.enhanced | 追加 elusive_defend index 033、038 | 实际原红费火焰远程3加入同次7远程并真实击杀，跨轮红晶保持0。 |
| basic_card_016.base、basic_card_021.base | 各自 `actualConsumerEvidence` 追加 elusive_defend index（016取042/044，021取047/050） | 分别真实物理格挡2折半挡一个冰攻1；052正式防御后两槽才记成功，解除闪避高甲。不得引用旧失败44为通过。 |
| advanced_card_034.base | 追加 elusive_defend index 055/057 | 原攻击3真实击杀已双槽完全格挡的低甲1蝠；实际手牌、已打出、回合弃牌和跨夜轮保留。 |
| advanced_card_010、advanced_card_035 | 只补 `mappingNote` 指向已有后继有效案例，保留 `historicalExpectedDrift` | 010_02/03与035_02–05已在当前账本内，并非当前新增待测。 |
| 其余神器/行动/法术ID | 当前主要模式状态不变；后续同类消费者可按需加索引，无需造新的完整验证批次 | 不从历史partial或“此晚间场景没用到”反推出缺口。 |

### 证据定位 / 哈希

- `pve_polarization_special_primary_acceptance_index.json` SHA256 `8b61573b3d72d887d7e4ae1250152d2daf99c4c9fe7be3544b6523897058bb1e`。
  报告 `AutomationOutputs/PveBattle/20260929_190632_8d1aeca1/report.json`，success，SHA256 `ed914bd45b6f5e323c561cb7425b9d9e8a277befe6fb7fbfcbf7ec7b0d3f7306`。
- `pve_heroes_assault_fee_acceptance_index.json` SHA256 `38828a4c9b323723a3b67737f201e1fdd2349dbe948b6b1c7590478ffb26d33b`。
  报告 `AutomationOutputs/PveBattle/20260929_184358_8671e144/report.json`，success，SHA256 `7b9fd79fd0577a9bdee23fa6dfb1a216f2e19516a129c73d342402495f1e43bf`。62总操作中20为无断言设置、42为断言操作，共95断言。
- `pve_elusive_defend_acceptance_index.json` SHA256 `3f52d85a6f6527de7571a0e79b8e73c46752976a9a797b389b112b7501354d9d`。
  仅组合 `20260929_205123_3c5c5e6c/report.json` 的[0,44)与 `20260929_205418_bc96af68/report.json` 的[44,65)。前者整体failed保持，SHA256 `29c2cd83464598a0923961b437b7ebf936bea13a855ebf37cb29125fc9d41b69`；后者success，SHA256 `06769d1092c2039e68ea6db5bc06a5d260397885f7b209b710ff305b52288463`。合65操作139断言。图审和Console均引用index内主代理已记录结果，本次没有重复看图或重读Console。

阵营Just Fame后续由 `pve_faction_fame_acceptance_index.json` 独立记录，不能篡改上述旧印刷名望报告数值。该世界奖励更新不产生一套全卡重测义务；如追加账本说明，标明历史运行时点及变体状态即可。

## 审计输入指纹

- artifact ledger SHA256 `4d73f3c61ebaccbbf9d13e36d10a66259b9693fddcd8ec246ffd6c2b105d4814`。
- deed ledger SHA256 `4e454ca4832737e067f91460c69adc6e8bb180ebf90f589defc716a6e533ba2a`。

主代理最终更新应保持 implemented/backend/scene 和确切未决字段分开；本次审计没有把以上两处未决改成完成，也没有请求扩大复制、配色、地图完整世界范围。

冻结补注：`summary.currentEntryImplemented=25` 与各行 `entryImplemented=true` 只证明25种神器有当前入口，不证明所有动态借用都已实现。建议仅在summary说明或policy增加 `items_022.normal 的公共混乱例外见 knownCurrentDemoGap`，不改25这个已定义计数。`deed.completion=false`、`rowsWithReview=1` 与 `magic_016.remaining` 当前互相一致，未发现需要把review数清零或增加的依据。到此冻结，不再追扫旧报告。
