# 燃烧护盾：格挡后选择目标使用原火焰攻击

## 本批最终证据

`AutomationOutputs/AllOriginalCards/20260917_022001_38ac2eab/magic`：8个新增场景、201项效果断言全部符合，完整UI report.status=success，ui:textFits无失败。已查看007_magic_008_08（物抗目标火焰有效4、击杀1、重复攻击拒绝）、015_magic_008_09（火抗有效2、不击杀、资源已消费）、030_magic_008_11（失败格挡完整承伤3、无攻击奖励）、045_magic_008_13（爆破消灭1、实际狂怒2、已消灭目标拒绝保留2、换目标击杀1及最终名望6）截图，原卡文字、实际数值、真实卡区均可见且无截断。

新增日常队列已清空，旧通过范围未重跑。最终索引1000案例，398 passed / 594 partial / 8 failed，8例仍为advanced_card_044..047缺有效原定义，无有效卡失败或缺失。保留本批整卡其他范围partial声明，不把本次8条流程断言符合冒充整个游戏通过。目标active。

Unity Logic/Scripts/Tests DLL分别02:19:01/02/03；代码修改后和场景后Console两次0 error。所改文件git diff --check通过（仅LF换行提示）。本批无下载/上传、系统重启、VPN或包修改。下一批继续原卡分阶段交互中尚未核实的奖励期限或目标身份问题。

上一轮有实际修复和场景证据，属于progress。本批目视核对 `Assets/GameData/cards/magic_en_008.png`：基础火焰格挡4，成功格挡后可在攻击阶段用作火焰攻击4；强化成功时消灭被格挡敌人。保留红色/黑色费用、日夜规则和原ActionSystem映射。

原TurnMachine只有一次同时传入blocks及预先生成attacks的结算方法，格挡奖励产生后调用者没机会重新选择目标，随战斗清理被丢弃。本次增补正式分阶段API：ResolveDefense调用既有BattleResolver生成奖励并消费格挡，阶段停在Melee；Attack调用CardCombatActions使用原贡献，避免重复承伤；FinishBattle在玩家确认后清理未用奖励。分阶段记录已击败索引，爆破消灭过的敌人不能再次领取奖励；拒绝换用其他ActionContext。既有一次结算API保持，用于此前已验范围。

新增 `BurningShieldPhaseStateTests`，8项后台 `AutomationOutputs/RulesState/20260917_021542_84d3239f` 全符合：基础成功后攻击另一物抗/火抗/奥术免疫敌人，原火焰属性及折算；被格挡者奥术免疫不取消玩家攻击奖励；失败格挡伤害全额且无奖励；强化物抗可消灭、火抗/奥术免疫不消灭；重复目标、重复防御、结束后重复操作、不同context均拒绝；放弃奖励在结束时清理。强化后续攻击从直接加资源改成实际原狂怒出牌，并只复验该方法3参数：`20260917_021758_aa22c786` 3/3符合。

场景新增TurnDefense、TurnAttack、TurnFinish，调用同一正式分阶段入口；呈现原卡、目标、实际攻击4/2、剩余攻击、击杀、伤牌、魔力和回合末真实弃牌。只选新增magic_008_08..15八例，后台已过后再进入场景。场景验收待完成。

明确边界：这不是所有战斗卡或整个冒险已通过。BattleResolver的部分Pending击杀奖励在一次Resolve调用后会清理，分阶段对这些其他卡的期限仍需审计；新接口复用现有行为，本批未证明它们。远程移除索引、Adventure临时context和独立卡区仍在未完成列表。没有后台失败时启动场景，未全量重跑卡库，远端继续暂停。
