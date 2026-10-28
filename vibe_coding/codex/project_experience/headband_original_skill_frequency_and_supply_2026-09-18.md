# 启迪头环：公共原技能、频率与取得

- 复用原技能 SourceId、Effect 和独立技能选项；神器 option 只表示实际供给下标，不压入第二个语义，也不 Clamp 非法选择。
- 普通借用按原 Frequency 判断：每回合技能使用两次，每轮技能使用一次。Reusable 在现有原加载器中均为 true，不能用于判断次数。
- 互动属性依据原资料独立分类，不能用 flip/Frequency/名称猜测。当前核定7个完整来源为互动；未知来源保持 nullable 未核定。参考 tmp/headband_interactive_skill_source_audit.md。
- 普通借用前拒绝互动和非法原技能选项；不把公共技能加入个人技能来绕过所有权验证。焚毁则真实从供给移同一引用进个人技能，不在取得时自动发动，不套用普通的非互动限制。
- 既有 SkillActions.Use 继续负责取得后的实际发动及使用标记。场景只注入实际供给和选项、记录来源和引用守恒，并显示原技能表的选定区域；不生成新技能素材或另写效果。

后台首次测试编译遇 Monster 同名冲突，使用 MK.Logic.Data.Monster 别名修复。相关旧测试尚未纳入 RulesCli，显式加入同源 HeroSkillActivationTests 后仅选两条。

051833_99faf6c9：4/5通过；失败来自原032护甲预期误写4，原数据实为攻击4/护甲3/名望2。修正独立预期，051914_e1fc530b仅重验失败项1/1通过。共3主例+2直接相关回归通过，未重跑其它原技能。场景待验，尚未计整卡通过。

最新接续（2026-09-18 05:25）：基本场景119/121，剩2：magic_016空间扭曲、items_024神秘宝盒。本轮018九后台/3场289检查21步骤，022三主+2回归/3场184检查19步骤完成；图片核对、最终Console两次0。022后台原032护甲预期误写4已按原数据3修正，仅失败项补过，失败记录保留。022场景20260918_052233_eb415683/items。focused清空后继续016：tmp/space_bending_basic_plan.md、tmp/SpaceBendingBasicStateTests.cs三主草稿待集成，肆虐消费者仍需root落实。024代理正在准备tmp/mystic_box_basic_plan.md和tmp/MysticBoxBasicStateTests.cs，无正式改动。目标active，未推送。
