# PPO 原附旗真实消费者

上轮为progress：实际v17终止保存模型，v18烟测接入原宝典并持续训练；本轮核实v18进程202648实际运行后正常exit0。v18烟测1024步、6episode、205.27秒、4.99步/秒，最终重载七英雄5/7至少赢一整组、8完整胜场，平均204步。仅小样本，仍有拖延；未称通用模型完成。

## 修改及依据

`tools/BattleRl/JourneyTraining.cs` v19追加已附旌旗操作，复用正式 B.UseFearBanner、B.SelectFortitude、BannerActions.UseCourage/ChooseProtectionCleanup：实际自有部队、真实攻击槽、次数与伤牌不改。原源码 `BannerActions.cs`、`PveBattleSession.cs`、`PlayerTurnEngine.cs`。原牌附着后仍为同一物理卡，不重新从手牌使用。

Preview提取为同一无副作用许可检查，战斗End保留合法勇气重整与防护清理选择，再允许正式nextTurn；不开放其他旧战斗消费。追加RemoveNewWounds观察，不把用户不可见牌序放进actor。原动作Key及观察前缀保留，PPO热启动不恢复旧优化器。

`trajectory_audit.py`检查旗实体守恒、恐惧取消不算格挡/耗竭/+1名望、勇气每轮一次重整不治疗、强韧只预选保护、防护必须已实际焚毁。正式Preview仍拒绝错阶段/敌人/部队和二次消费。

## 验证

编译冻结 `tmp/battle_rl_v19_active_banners/BattleRl.dll` 零警告零错误；Console两次零错误 `tmp/rl_active_banners_final_console_20261003.json`。

`verify_active_banners.py` 12定向检查/456转换断言：seed57恐惧实际取消攻击与耗竭/名望；seed91强韧承伤消费；seed19勇气重整army_2_004后同部队再次发动；seed5防护焚毁后分别保留/移除当回合实际新伤，正式回合后旗状态清空。证据 `20261003_active_banners.json`。

初选seed30的火焰魔像物抗先吸收全部伤害，没有触发强韧；测试预期错误，改选真实无抗性单位seed91，没有放宽断言或改原规则。随后防护检查误把removed历史区算作可用牌库：实际新伤从可用区移至removed，Player.Wounds为零。保留失败 `20261003_active_banners_failure.json`，改正区定义并同时断言原伤实体在removed。后续失败使用唯一文件名防止覆写。

共享许可变化定向回归宝典17/165通过，`20261003_spell_tome_inputs.json`现在对应v19；此前v18输出和工作日志保留，当前证据不可冒充旧冻结版本。

## 训练与缺口

只读检查v18保留6轨迹893操作：3完整胜场/16启动战斗，2超时，最长连续计划239。保留子集未实际使用宝典，不能把17项教师定向检查算作PPO实际训练覆盖。后续采样需优先尚未实际使用的原要素；不强迫模型动作，不因缺口删除原目标。v19进程209568于20:42:26启动，manifest的实际scope与冻结DLL已核对；当前尚无首个训练检查点，不能称新增旗消费者在训练中已出现。

核实无旧训练器后，启动v19 16384步、seed3368，从v18最终policy_ppo热启动。输出 `AutomationOutputs/BattleRl/20261003_active_banners_ppo` 及同名stdout；每60秒保存新的实际轨迹审计。源与DLL哈希由run_manifest记录，不并行第二训练器。

轮末旗取回窗口仍未暴露为策略动作；头环/宝盒、复杂技能与地图消费者仍有缺口。最终Unity策略回放及留出构筑评估未完成，目标active。规则断言通过不能等同通用策略可用。
