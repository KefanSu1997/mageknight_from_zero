# PPO原鲜血仪式缺入口修复

## 真实缺口

原ID差集发现advanced_card_012没有实际发动。核对原卡图、resources/text_json/advanced_card.json、PveDeedInputs.ColorCount和BloodRitualEffect：基础要求一种选色，强化要求三种独立选色及可选基本色转晶。JourneyTraining.InputProfiles此前只有空输入，InputMatchesChoice拒绝全部模式，因此不是单纯PPO未选择，而是适配入口缺失。

## 最小修改

只改tools/BattleRl/JourneyTraining.cs：鲜血仪式生成六种基础选色、56种三枚魔力颜色多重集（含重复、金黑），每种强化多重集支持不转晶或红蓝绿白转晶；同一多重集排列对原效果无意义，避免216个排列冗余。ResolveInput将crystal字段绑定正式PveDeedSelection.Crystal；基础模式不得带强化转晶输入。复用原正式费用、实际伤牌和BloodRitualEffect，不直接改资源/动作结果。

featureSchema与5591观察位置不变，动作目录新增输入，实际Key排序下标可能变化；未来恢复模型必须按完整Key重映射原动作，不能沿用旧动作下标或把新tuple强行记为已训练。当前v46 PID237080继续冻结v46 DLL，未更换或重启。

## 后台验证与编译

dotnet build --no-restore输出tmp/battle_rl_v47_blood_ritual_inputs，0警告0错误。真实初始化seed80035手牌已有原鲜血仪式，未注入卡或资源。基础选择Black：实际创伤+1、实体原牌进已打出、红晶按上限+1、Black标记+1。强化Red/Blue/Gold：红晶真实印刷费-1、三枚标记、实际伤牌+1；另一独立同seed强化并选Blue转晶，新Blue真实消费、蓝晶+1、Red/Gold保留。漏颜色与强化单色动作不在合法掩码中。25断言通过，原物理牌区审计无错误，预检操作不计PPO训练覆盖。证据AutomationOutputs/BattleRl/20261004_v47_blood_ritual_actual_inputs_check.json，含原动作/前后状态与DLL SHA。

Unity refresh idle，Console count1000/stacktrue两次0错误，tmp/rl_blood_ritual_inputs_console_20261004.json。没有Unity场景修改，未重跑旧整库或截图。

## 限度与后续

生成魔力的后续战斗消费、保存模型实际Unity回放尚未证明，不计整卡验收完成。新RL入口未进入活v46训练，待其实际终止及最终结果核实后才可使用v47。当前已3072/8192步44局，新批轨迹审计11438检查0错误3缺口。

其他差集保持：advanced024复制材料及动态多次输入仍需适配核实；magic004和magic023基础依赖实际地图；magic000需真实已耗竭部队及可支付费用条件，不能仅根据缺计数断定无入口。正式地图场地、共享技能、未见构筑与可用策略仍未完成。
