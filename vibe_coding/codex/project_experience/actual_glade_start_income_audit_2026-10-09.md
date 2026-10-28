# 真实rollout魔法林地回合收入漏验

## 失败与原依据
PPO episode1208 seed2134274446/task9dab80f3…在step68正式nextTurn报“real turn boundary clears old mana then issues exact persistent income”，79真实步完整失败保留。原place_006.png下半已视觉读取：回合开始在魔法林地，白昼金1/黑夜黑1；进入林地不直接取得。正式SiteTurnRules.BeginTurn在清理旧token/发TokensPerTurn后按真实地点发该收益。

## 最小修复
只修trajectory_audit.py的nextTurn收入oracle：真实turn增加、世界enabled、按当前q/r恰好一个公开cell、地点MagicalGlade时增加相应色1；仍逐色精确核对并拒绝缺/重复地图格。未改规则/DLL/费用/mask，普通非林地原清空预期保持。

## 实测证据
AutomationOutputs/BattleRl/20261009_glade_turn_income_audit_failure保留old audit/original_failure及verification.json。完整79before/action/合法/after/raw正式重放相同，1820有限检查0errors；另在step53实际飞到无地点森林0,-3，真实69步对照Black1→0。缺收入/双收入/错色/错位置/disabled/重复cell六负例均拒绝。白昼原文支持条件，但没有把合成白昼或这条黑夜轨迹冒充白昼实际Unity验收。

## 保存与继续
96计划先完成真实40局/10整任务至1208/68993/Adam381，失败1208未更新。只能从这个真实保存点续剩56，不能猜失败中间RNG权重。Python编译通过；Unity编辑器关闭且工具发现无Unity/Console API，Console两次零错与真实场景门禁仍pending，后台通过不替代。完整原目标不变。
