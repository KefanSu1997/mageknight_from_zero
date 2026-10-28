# 头环社会技能后台接入（2026-09-29）

本次仅编辑 `HeroSocialSkillRules.cs` 与新增 `HeadbandSocialBorrowTests.cs`。生产代码将社会技能真实时机/影响力/招募承诺拆为同一执行核心：自有入口仍验证拥有与个人次数；头环入口使用真实 `HeadbandBorrowResolution` 授权，不写个人次数。

- 特立独行允许每次独立选择影响1或影响3；影响3保留本回合不可用影响力招募的约束，实际已发生招募时拒绝。
- 奥术伪装本批只接普通影响2。翻面忽略声誉和付款翻回明确拒绝借用，不能猜测头环翻面规则或修改个人翻面标记。
- `HasBorrowInput(context, skill)` 仅验证普通选项与原时机，不付款、不发放影响、不改变标记。
- 新测试11项覆盖独立选项、村庄真实治疗/招募、已有自有次数不变、失败保留首次收益、回合清理、翻面缺口明确拒绝。

当前仅代码ready，未运行CLI与Unity。测试由主代理协调串行执行：`MK.Tests.Cards.HeadbandSocialBorrowTests`，直接回归 `MK.Tests.Battles.PveHeroSocialContractTests`。最终场景与两次Console零错误仍待主代理完成，不能据此记录场景通过。
