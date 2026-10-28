# 本人已征服塔实际购法术与原卡后续消费者

## 状态

本路线后台、实际场景、费用文字补图与最终Console均已通过；下文保留原执行计划及逐步进度。新 `tools/build_pve_tower_spell_purchase_acceptance.py` 默认plan；`--write` 默认从60产生新断言，`--from N`仅允许N>=60补后续。总87实际操作，其中0..59重放已通过同一路线而不重新断言/截图；新增27步73断言3关键图。未改生产、fixture或配置；未操作Unity/CLI。

## 确认的UI契约

主代理明确：`JourneyInteraction/Btn_SiteSpellShop` → `SiteSpellShop/Btn_Spell_magic_015` → `SiteSpellShop/Btn_BuySpell` → `SiteSpellShop/Btn_Close` → `JourneyInteraction/Btn_CloseInteraction`。后台API为CanBuySpellAtCurrentSite/BuySiteSpell(SpellCard)，待主代理接入并验证，未沿用旧MageTowerInteraction默认第一张/3魔力的旧不完整实现。

## 前缀证据与实际资源

实际读取 `AutomationOutputs/PveBattle/20260929_172048_1890cc47/report.json`（success），已逐项比较0..59共60个实际buttonPath全部与新路线前缀相同且success；仍实际重放合法状态，不能跳过真实征服而直接置旗。

- 首回合绿戒/黑护符/地道/原火球，真实002守军击杀、塔-1:-1征服、正式回合末奖励magic_011进手，名望40。
- 报告50：手牌items_010,magic_011；原法术供应magic_001,magic_015,magic_000；蓝1绿1红0；真实源White,Blue,White。
- 第二回合真实塔交涉-1声誉修正，原金币包焚毁+9影响/+3名望；步骤58影响8名望43、金币包永久移除。前缀59回旅程。
- 前缀报告SHA-256：a351e362dff0fffe22b3a77a1556f1d242bf906ba1ce2d8cfa252367597d0f6d

## 新增操作和消费者

1. 回同次本人塔交涉，查看实际三张原法术。明确选择中间原蓝法术magic_015风雪交加，不接受默认首张替代。
2. 支付7影响与1蓝晶：8→1、蓝1→0；新牌入真实牌库顶，手牌仍只有旧奖励magic_011，deck.count=1。场景自动化没有设置牌库/资源。
3. 正式EndTurn清影响并实际抽到magic_015：手牌magic_011,magic_015、deck0，完成第二回合。
4. 第三回合标记旧奖励法术末弃，从仍在真实源的蓝骰index1取得本回合正常蓝token；蓝库存仍0。
5. 实际新购magic_015基础支付蓝token，原寒冰远程5（power.attacks=Ranged:Ice:5）击杀原032甲3，名望43→45。
6. 正式完成其余阶段/EndTurn：手牌0、弃牌6（原五牌去焚毁金币包剩4，加征服奖励与购入法术各1）、同玩家/塔归属、红0蓝0绿1、无伤。

已实际查看 `Assets/GameData/cards/magic_015.png` 原图：上半蓝费远程寒冰攻击5；只覆盖该购入牌主要基础消费者，不顺带重跑下半自伤攻城8。

三关键图：选择原供应卡及费用；实际支付并置牌库顶；新购原牌印刷蓝费和寒冰远程消费者。报告同时记录真实供应补位，后续验收应核对未选原牌保留/所选消失；不通过模拟供给推断实际结果。

## 门禁与范围

此前塔征服/奖励已过证据复用；新“7影响+同色魔力”、失败原子性与实际供给补位由主代理本批RulesCli共用后台先验。后台尚未提供通过报告时不跑新场景。没有把已完成招魔像证据泛化成买法术证据，没有伪造原核心板块或后续地图来源。


## 17:40 后台与编译门禁更新（场景结果待回报）

主代理已报告：`AutomationOutputs/RulesState/20260929_173632_0f5e5129`初跑13/15，两消费者测试夹具漏给真实原法术ActionData；仅修测试包装后 `20260929_173912_98d37a53` 两失败2/2，合计15个有效后台案例。保留原失败，不把重复计数作17例。`tmp/pve_site_spell_purchase_compile.json` 实际Console双0。17:40重连后已正式dispatch新场景，当前不提前标记通过；等待实际report后核对供应补位、同玩家及消费，再生成index。


## 17:41 实际规则场景通过，费用文字补图待验

实际读取 `AutomationOutputs/PveBattle/20260929_174109_5b424c0c/report.json`：success，87操作，其中新范围60..86共27步73断言全部passed，前60操作无重复断言。

实际供应核查：步骤62为magic_001,magic_015,magic_000；63付款后为magic_001,magic_000,magic_022。未选两张保留，所选015从供应去除，新022立即补位。63蓝晶1→0、影响8→1、deck1且hand仍011；66正式EndTurn手牌011,015/deck0；79蓝token付款后贡献Ranged:Ice:5；81真实击杀后贡献清空；86手牌0弃6。60..86全部27个快照samePlayer=True。

已实际解析两后台XML并按完整案例名合并：原13Passed+2Failed，失败两项后缀2Passed，最终15个唯一案例均Passed；旧失败报告保留。实际编译Console文件两次data=[]。初版索引写入报告/XML哈希及核查细节。

主代理已看三图，发现纯文字“蓝色魔力魔力1”重复并修正，拟仅补62。索引当前明确visual_caption_recheck_pending，最终Console仍pending；待主代理给补图/最终Console后才完成本批记录。不重跑购牌及击杀已通过消费者。


## 17:44 费用文字定向补验完成

`AutomationOutputs/PveBattle/20260929_174440_358a8e43/report.json`实际读取success，63操作（0..61仅重放）、第62步1断言1图。主代理已看62图，“蓝色魔力1”正常。已将补图路径/报告哈希写索引；没有再次执行购买或击杀。规则新范围仍是27步73断言，另列显示补验1步1断言，不把重放计为新场景覆盖。

当前规则和视觉均通过；主代理正在串行要塞场景，待其结束统一检查最终Console，预期记录路径 `tmp/pve_site_spell_purchase_keep_final_console.json`。当前没有把未取得的最终Console写为完成。


## 最终门禁完成

实际读取 `tmp/pve_site_spell_purchase_keep_final_console.json`：两次结果status=success/result.success=True/data=[]，实际Console双0；路径及SHA-256已写 `pve_tower_spell_purchase_acceptance_index.json`，最终状态passed。

本批明确口径：15个唯一后台案例；新增场景27步73断言，另费用文字定向1步1断言；原3图加修正1图均由主代理查看。前60征服/资源操作复用旧证据并实际重放，不重复计数。实际原供应补位、所购法术正式入顶/抽回/费用及寒冰攻击消费与同玩家均已核验。只证明本人法师塔购牌，不扩大为蓝色城市购牌场景或整个地图/项目完成。
