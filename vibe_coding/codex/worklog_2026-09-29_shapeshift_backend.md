# 变换身形：原基本卡固定项后台接入（2026-09-29）

## 正式生产路径

原技能 `hero_skill_pool_001:06` 不再通过旧技能入口授予下张移动卡无限来源许可。新增 `ShapeshiftActions.Choices(DeedCard, enhanced)` 返回真实效果类参数元数据 `FixedCardOutput(Option, Kind, Value, Element, AttackType)`；`Play(player, context, skill, card, enhanced, option, target, material)` 校验实际持有原基本手牌与未用技能，按输出项验证当前阶段，然后走原 `CardPlayActions.Play` 付款、必要材料、卡区和后处理。转换输入仅在此次调用作用域内存在，成功后记技能次数，finally 恢复输入。

`IFixedCardOutput` 元数据与正式执行共用 `FixedCardOutputs.Add`，没有按卡ID重新猜数值的映射。接入有固定项的16种原基本卡：000/003/004/007/010/011/014/016/017/018/020/021/023/024/025/027，基础与强化按真实选项返回。非固定的影响/治疗等选项不列出；大地之子强化地形格挡不是固定值，明确不列入转换。

保留原攻击/格挡元素；转换为攻击产生同元素普通攻击，移动无元素消费者。原强化费用、随机应变真实材料、野蛮收获/德鲁伊/永不止步的独立许可保留。寒冰护体强化固定冰格挡5转换，符号动态奖励保留为零基础的原冰格挡贡献；CombatPowerPool 仅为带 CountEnemySymbols 的零基础格挡保留记录。战斗之怒固定2/4转换，本场真实伤牌加值仍属攻击；利斧强化转换成非攻击后没有本次攻击击杀，因此不发其条件名望。

UI 使用方需要缓存 Choices，避免每帧读取原定义；Play 已复用一次读出的 Original。当前场景控制器启动时明确设置原 JSON 路径为 Application.dataPath/../resources/text_json。

## 已验证 / 未验证

- `AutomationOutputs/RulesState/20260929_042611_94aec17f/results.xml`：本 fixture 11/11 通过（由另一代理在串行67项合批运行，不能把67全记为本技能）。覆盖强化行进真实付费格挡、狂怒真实地图移动、寒冰基础冰攻击实际抗性、寒冰强化固定/动态分离、随机材料基础/强化、非法阶段/费用/材料/非基本拒绝及16种元数据。
- `AutomationOutputs/RulesState/20260929_042732_8761c929/results.xml`：2/2 新增正式 session 入口通过。真实新伤→战斗之怒固定2转格挡且伤牌奖励仍攻击1；连续旅程拒绝战斗输出后，合法转移动并实际付地图费用。
- 后台共13个新增用例；各卡所有选项并未逐一消费者穷举。元数据存在不等于每卡已经场景验收。
- 德鲁伊之道还必须传实际 TargetHex/TargetTerrain，API保留原验证；主UI若没有提供此输入则仍是操作入口缺口，不能因目录存在记完成。
- 本代理未操作Unity。场景和Console由主代理执行，最终以主代理场景证据为准。

## 共享消费者定向回归

`AutomationOutputs/RulesState/20260929_042919_a8ec8e0a/results.xml`：KeepMovingBasicStateTests、BattleRageBasicStateTests、EarthStrengthStateTests 合计25/25通过。覆盖本次改变固定资源生产入口实际影响的后处理和动态地形/伤牌消费者；未跑全库。首次命令误写命名空间 RulesState，选择器在执行任何测试前拒绝；改为真实 MK.Tests.Cards 后通过，不计首次为测试失败或通过。
