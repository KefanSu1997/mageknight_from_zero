# 原冰蝠Elusive/Defend正常成功攻击路线计划（2026-09-29）

本次独立生成器 `tools/build_pve_elusive_defend_acceptance.py` 默认仅plan，支持 `--write`、`--from N`、`--output`。当前65操作138断言4截图。没有写共享config、没有运行RulesCli/Unity，没有修改生产/场景。遭遇和后台/UI由主代理及另一代理负责，本计划不是通过证据。

## 原规则与路线选择

已读本地官方tezla p2图 `tmp/pdfs/tezla_rules-02.png`：单来源Defend只给首次被攻击的一名敌人加甲，持续本战，不能同一战同时当作保护自身与另一敌。采用正常成功组攻，不用明知不足攻击消耗防护，也不重新生成死敌当连续战斗。自护分支由独立后台覆盖。

- 既有HornGold红蓝库存各1、无单位，Wolfhawk等级6名望35/手限6，不选技能。明确六牌自定构筑，非标准16牌起手。
- 构筑基本010迅捷反应、011利斧投掷第一页，016决心/021狂怒第二页；高级000火焰箭矢第一页、034骑士精神第三页。每页12与当前DeedPicker一致。
- 新可复用遭遇index13，原057、两个不同引用032。初始甲2,3,3。原010 `ranged`免费远程1，原011 `strong`支付蓝1远程3，原advanced000 `strong`支付红1火远程3，合计7；原032无抗性。
- 选Enemy_1、Enemy_2实际组攻后Btn_Resolve打开EnemyDefend。唯一source057自动选；点击Btn_Target_1，观察selected=0>1:1但实际甲仍2,3,3、贡献7未消耗。Btn_Confirm才正式应用为2,4,3，成功远程7击杀两032，名望35→40（包含原利斧投掷击杀额外1），红蓝均0。
- 原016/021各选 `block`物理格挡2，对蝠冰1有效各1。第二次重新选蝠会将攻击索引重置0，必须再点Inspector/Btn_AttackSlot一次选择1。两次成功记录0:0,0:1。
- 正式防御结算后无伤，蝠低甲1，受护已死032仍记录甲4（不是把Defend迁移蝠自身）。原advanced034 `base`物理攻击3实际击杀蝠，名望40→41，无升级奖励。
- 六牌已全部打出，正式EndTurn后手0/弃6/库0；不能直接BeginTurn。执行Announce→EndRound→PrepareRound→night1→BeginTurn，正式原六牌回收、同玩家下轮、两晶仍0。只开始新回合，不伪造第二场战斗或复活已死敌。

## 观察与待验证

原能力槽选择及两目标选择UI现成可用。新Defend路径按主代理源码核对；截图保留初始原敌双甲、实际Defend分配页、全挡后低甲、正式下轮资源。输入路径和费用已经定向核查；后台和场景尚未执行，等主代理门禁通过后串行运行。伤牌/部队本批未使用，不以本批补其他能力。

## 20:56实际完成与证据

后台11个唯一用例有效通过，初10/11后仅补失败1及Unity访问修复受影响1；准确方法/断言见pve_elusive_defend_acceptance_index.json。最初名望漏算原利斧投掷强化击杀+1，已按原卡纠正为35→40→41，未修改正式卡效果。

主代理新增复用原素材的IceBatsSentries遭遇（057和两个实际不同引用032），追加为连续演练第14组。生产共用EnemyDefenseState按引用保持分配，Monster原双甲/防护字段与原JSON同步；UI明确选择同名敌编号，预选不消费、确认实际攻击才应用。共享EffectiveArmor保持显示/结算同源，正式全攻击槽格挡后才启用低甲。

Unity实际编译初报新测试访问internal helper，已改正式英雄获得/使用技能路径，每分支独立本人且每轮次数合法；不放宽生产访问权限。场景外部修改Reload弹窗由原生UI确认后恢复，不重启编辑器。编译修后tmp/pve_elusive_defend_compile_fixed_console.json及最终tmp/pve_elusive_defend_final_console.json均两次零错误，旧失败文件保留。

场景205123_3c5c5e6c前44步有效，第44即时读取SuccessfullyBlockedAttacks失败：该正式记录在Defense写入，Block操作自身已成立并消费贡献。仅修脚本观察时机，用即时block0与正式Defense全槽记录交叉检查；205418_bc96af68前44纯重放，后21通过。合65有效操作，断言数见索引。5张图已由主代理目视，原图/数值/低甲/同人跨夜轮均符合。

本批不等于全敌规则或阵营奖励通过；不重新计已通过原卡，不全库、不网络传输、不push。规则、后台、实际场景三者证据对应索引。所有运行已结束。
