# 地道的实际路径、地点城防及战后返回

原卡`Assets/GameData/cards/magic_en_004.png`明确最多三格已揭示路径，不能到达或经过沼泽/湖泊。不能仅用TeleportRange表示，也不能只核验终点距离；必须逐格邻接、揭示与地形验证。基础须安全落点，可跨中间山脉/肆虐，不消耗移动点，之后步行按正常地形/昼夜费用。

强化是一次实际突袭：保存起点、结束移动、绑定地点守军或其他玩家目标；**只**忽略地点城防，不能设置IgnoreFortified抹去怪物自身能力。法师塔必须属于城防地点，新增PlaceType项只追加以保留序号。底层正式TurnMachine继续执行远程、承伤、战斗结束。

战前不得提前调用FinishTunnelAssault；战后仍有守军不能留位，撤退返回保存起点。恢复进入时原有地点豁免，不能误清先前拆毁的效果。回合结束前必须结束这次突袭；战后本回合仍不能继续移动。

后台31新例与4直接相关回归通过。一次失败来自夹具误用白昼森林3而实际设置Night，应先核对昼夜和原规则；夜间森林费用5，不改生产地形费用迁就测试。

场景`AutomationOutputs/AllOriginalCards/20260917_051355_b3a2a80f/magic`31例860效果检查、141UI步骤成功，前后Console双零。守军自身城防时火球被拒，实际承受2伤并退回原点，不能只断言IgnoreSiteFortifications这个布尔值。

本批未接通世界地图征服/守军清除及奖励，也未验收完整玩家战斗，仅验证PVP目标路由；AdventureSession整合仍待后续。必须保留partial，不能将本次战斗调用链通过冒充整张卡在完整游戏通过。见`vibe_coding/codex/worklog_2026-09-17_underground_travel_assault.md`。
