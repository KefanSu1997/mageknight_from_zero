# 供应部队分页与真实攻击槽

## 修改

供应部队界面原本将全部卡牌横排，第4张与能力区域重叠；虽然正式后台接受 attackIndex，界面始终使用第0次攻击。现在每页显示3张真实供应卡，翻页保留原实体选择；奇袭反应能力选择实际敌人及其攻击槽，原服务消费该槽。增加 offer.selected/page/attackSlot 观测用于操作断言。

后台复用已有 UseOfferedUnit → UseFromOffer，不新增模拟规则。补 RepeatedEffectResolution.RequireCompleted，防止未完成头环重复效果时插入供应能力。供应单位不能替自有单位承伤，继续保留原所有权拒绝。

## 后台证据

- `AutomationOutputs/RulesState/20260929_125416_e8d7d96e`：6/6（3新增、3直接回归）。
- `AutomationOutputs/RulesState/20260929_125453_be17f403`：1/1 待决效果拒绝守恒。
- 共7有效通过，详见 `worklog_2026-09-29_offered_shock_backend.md`。

## 场景证据

生成器 `tools/build_pve_offered_unit_selection_acceptance.py`；报告 `AutomationOutputs/PveBattle/20260929_125901_bbe2cbd8/report.json`，95操作169断言一次通过。

合法等级4诺若威斯、原忠诚取得时追加实际供应，五原牌保留到正式后续轮次。首战白戒生成白标记与库存晶体，白标记支付战斗号召；决心强化实际挡4，分页选择第4张赤袍僧侣，翻页来回保留选择，原攻击3击杀护甲3。正式供给轮换后用保留白晶支付同一号召，实际奇袭队对原女巫第2次攻击减1：火3不变、冰3→2，护甲4→3。正式承伤产生2伤牌，决心与狂怒攻击4实际击杀。两场结束名望21，部队数始终0，供应未移除。

关键截图46/53/82/94已目视：原图、分页选中、数值变化和终局状态正常，无文本截断。`tmp/pve_offered_console_final.json` 记录实际Unity Console两次零错误。

本批是供应分页及非首攻击槽增量验收，不声称所有部队或所有复制组合完成。未修改VPN、未推送、未上传截图或日志。
