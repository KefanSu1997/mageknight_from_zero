# 原地图和真实肆虐战斗的接入

1. 原七格板块先核原图、固定朝向和轴向坐标，再投影至正式 MapState。海洋保持来源记录但不进入可移动格集合；不能用湖泊替代。原起始已揭示板块不能当付费探索供应。
2. 地图敌人使用带 TokenColor 的正式 MonsterJsonLoader，并从真实 EnemyTokenSupply 取实际实例。另一 CardJsonLoader 路径缺颜色时绿色供应为空，不能造标记绕过。
3. 移动引战已建立 RampagingEncounter，旅程战斗接管同一 GameState/TurnMachine，不能重复 BeginBattle。主动挑战同一入口、付款前校验相邻及供应身份，结束沿用原回收/声誉。
4. CLI 引用成功不代表 Unity 测试程序集可见。overrideReferences=true 的 asmdef 使用已有预编译 DLL 须写 precompiledReferences: Newtonsoft.Json.dll，不是 references 的程序集名称；不需要安装包。
5. 场景失败先看原卡真实选项。宁静选择 heal 而非通用 base；修脚本后从失败63起补13断言，前63步93断言保留复用，没有修改规则放宽预期。

证据：worklog_2026-09-29_original_map_runtime.md；13有效后台、68操作106合并断言、关键截图和实际Console双0。地图美术仍为规则图示初版、后续探索未接，不称完整原地图或整局完成。
