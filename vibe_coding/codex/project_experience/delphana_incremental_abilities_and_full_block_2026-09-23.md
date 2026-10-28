# 大师：逐次能力与完整格挡

大师首次能力耗竭后仍可逐次付费使用不同能力，未受伤是继续条件。不得复用普通部队入口前临时Ready，或强制在首次激活时选定全部后续能力。能力图标应核教学规则第9页，扩展被动与招募限制不能只读卡面能力文字。

红色的条件绑定原敌人，判断全部攻击真实成功格挡；停攻/取消不算。现有`WasBlockedThisBattle`可能只记录一项，不能不加核实直接作为完整格挡证据。正式结算器已有奖励和原实例去向，条件消灭应加入它的实际击杀列表，避免另发名望或丢失世界战场身份。

02:26补录：13个后台案例已通过，Unity编译双0；尚未完成大师合法构筑与场景验收。增量证据见`vibe_coding/codex/worklog_2026-09-23_unity_resume.md`，源码范围见`vibe_coding/codex/worklog_2026-09-23_delphana_masters.md`。

CLI共编译测试和Logic可能掩盖Unity程序集访问限制。测试不能直接赋值internal setter；本次`OfferedUnitUses`改用正式`BattleSummonsEffect`建立许可。不要为了测试放宽生产属性权限，UnityConsole必须另验。
