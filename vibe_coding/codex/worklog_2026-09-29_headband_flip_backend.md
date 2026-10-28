# 头环三翻面技能借用后台（2026-09-29）

## 实现与范围
- `HeadbandBorrowResolution.IsFlip/UseFlipSkill` 与 session `UseHeadbandFlip`：一次完整原子借用。狂乱普通两次各2同次攻击，翻面4一次；仪式一种实际颜色token一次；伪装普通两次各2影响，或忽略声誉轨一次。
- `BattleFrenzyEffect`、`ShamanRitualEffect` 复用原ValidateCore/ExecuteCore，borrow只取消个人翻面状态读写；不临时取得技能，不修改公共供应和本人次数。`HeroSocialSkillRules` 同样共用原实际声誉轨修正，borrow不写个人翻面。
- 原仪式频率为ManualFlip，头环typed入口对此实际技能允许一次，不篡改原定义。选项100恢复个人标记不属于借用。
- 旧UseSocial伪装普通分次入口保持兼容；已有pending拒绝进入新原子入口，防止先影响2再切翻面。旧generic HeadbandEffect经Handles预先拒绝全部三技能，消除第一次加攻击4并翻面、第二次异常的部分提交。
- UI及Unity由主代理独占，本后台工作不操作Unity。

## 有效后台证据（46项，新增20 + 精准回归26）
- `AutomationOutputs/RulesState/20260929_150736_c308fbb6/results.xml`：41/44。失败三项保留：仪式两项暴露ManualFlip未被Begin接受；伪装普通招募例的负声誉导致实际费用不足。
- 修正ManualFlip授权和单次额度；普通招募夹具使用中性声誉，翻面例仍负声誉验证真实修正。不改变原费用。
- `AutomationOutputs/RulesState/20260929_150838_a7efb703/results.xml`：仅补3/3失败。
- `AutomationOutputs/RulesState/20260929_150910_e0922ff2/results.xml`：补2/2必要拒绝/守恒断言，既有个人伪装翻面保留与错误近战阶段拒绝。
- 复用首次通过41项，合计46个有效用例，不重跑已通过范围。回归含旧头环社会入口、自有狂乱/仪式持久翻面和伪装实际交涉轨修正。
- 消费者含实际原敌击杀名望、仪式红token支付原盛怒强化、影响力招募真实部队、负声誉修正及正式回合末清理。

## 剩余
需主代理完成实际Unity编译双零错误及本批连续场景验证。后台通过不记为场景通过。
