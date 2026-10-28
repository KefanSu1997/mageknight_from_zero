# 冲锋陷阵：真实部队加成与不同期限

上一轮完成战斗号召供应能力，属于进展。本轮针对未消费的部队加成推进，没有启动全库回归。

## 依据与修改

已观察原 `Assets/GameData/cards/advanced_card_016.png`，并核对原 advanced_card.json：必须在战斗开始时打出；本场所有部队攻击/格挡+2，红魔力强化+3；本回合不能向部队分配伤害。

- `ChargeEffect` 使用独立 `ChargeUnitBonus`，避免把旧旗帜/领导技能的未正确实现作用域混成全队加成。正式 `CardUnitActions.ApplyAbility` 在实际发动攻击/格挡时加值，保留元素、远程/攻城类型、原能力费用与自伤/声誉副作用；移动、影响力和英雄出牌不加。
- 战斗号召供应能力复用上述入口，仍不取得供应或耗竭自有部队。
- 新 `CombatActionStarted` 记录真实能力/攻击/格挡/伤害结算已进行；与阶段、已有攻击格挡贡献、击杀和承伤状态共同校验战斗开始时机。Charge验证发生在费用之前，模块直接Execute也校验；不因攻击已用光而重新开放窗口。
- `EndBattle` 清除本场ChargeUnitBonus，但不清NoUnitDamage；`EndTurn` 同时清除两者（包括跳过补牌）。正常新战斗重置活动标记，不解除整回合禁伤。
- 场景UnitUse传递真实二选一分支。冲锋独立界面展示原部队卡面、费用、攻击/格挡、实际所需强度/击杀、逐步期限与卡区。

## 后台

`AutomationOutputs/RulesState/20260917_082824_1d55a9e1`：27/27。其中21新增ChargeUnitStateTests，6项受影响战斗号召/暴徒承伤回归。只检查本次共享入口与期限影响范围。

首次编译发现新测试两个ActionSystem.Play调用遗漏enhanced参数，修为false后完成27项；没有进入场景查后台错误。

覆盖基本/强化×自有/供应×寒冰攻防选项、原火焰远程与攻城击杀原monster_027、非攻防/英雄隔离、战斗外/近战阶段/已有攻击/已发动/已结算后拒绝及资源守恒、跨战斗禁伤到回合末清除、自伤不等于分配伤害、强化缺红费用。

## 场景进展与失败保留

DLL08:32:41/43/44已更新，Console两次0后启动16例（原2例改为真实操作+新增14例），整个历史库1279例。

- `AllOriginalCards/20260917_083321_cf169721/advanced_card`：16例，13列明断言通过；06/07/15失败。真实夹具initialHandCount在追加extraHandCards后裁切，三个组合用例的额外牌被删除。
- 将默认手牌数量限制移到显式追加原卡之前。检查整库，只有06/07/15同时用了initialHandCount和extraHandCards，故只重测这三例。脚本DLL08:35:33后Console双零。
- `AllOriginalCards/20260917_083609_cc574934/advanced_card`：3例仍失败，额外牌这次存在，但旧默认CardToDiscard、CardToRemove和CardsToDiscard将这些牌选作材料，正式CardPlayActions正确拒绝。保留该报告。
- 三例显式配置discardIndex=-1、removeIndex=-1、discardIndices=[]；保留原预期，且15额外检查必须因“战斗开始时机”拒绝，而不是因丢牌或材料误选拒绝。仅再次补测这三例。

## 场景最终结果

- `AllOriginalCards/20260917_083829_61d5e594/advanced_card`：3/3列明断言通过，15次UI操作、70断言、4截图。汇总器核对当前套件hash、请求覆盖、实际点击命中、报告配对、独立预期与截图。
- 首轮16例共79次UI操作、350断言、17截图；其中13未变例使用本地incremental_audit.json核对当前逐例预期与历史报告/图片/点击完整性。三例失败分别由后续成功记录覆盖，不把旧全套件hash当作当前全量认证。
- 人工观察首轮029（凶蛮僧侣攻城4+2=6，原怪物护甲5，击杀1/名望4）、062（加成战斗末0，禁伤到回合末清除，前后承伤0/1），最终005（冲锋+战斗号召寒冰4+2=6，击杀并保留供应/实际两张已打出牌）。截图文本可读，无截断。
- 最后Unity Console连续两次0。16个已通过列明用例退出日常队列；focused为空。历史索引1279例：500 passed、771 partial、8缺少有效原定义的旧记录，missing=0、有效卡记录失败=0。
- **目标保持active，整卡保持partial**：这些是已列明分支证据，不是1279张卡或全卡目标完成。

## 未完成

HonorBannerEffect和LeadershipSkillEffect仍往旧UnitAttackBonus/UnitBlockBonus写值，旧旗帜还未按真实附着部队建模。未把这些错误状态直接加给所有部队；应继续核对原旗帜/技能的目标、次数、费用与生命周期。其他尚未接入部队及特殊地点、完整游戏组合仍待联验。

无远端推送、截图/轨迹上传、安装包变更、编辑器/系统重启或网络设置操作。
