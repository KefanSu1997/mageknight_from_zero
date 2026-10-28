# 提神漫步：移动治疗独立消费与战斗地图边界

原卡 advanced_card_007.png 已直接查看：基础移动2与治疗1，强化移动4与治疗2；牌面明确战斗中只有移动点。复用正式 ActionSystem/CardPlayActions 和原素材。

## 修复

RefreshingWalkEffect 的两处立即治疗改为 HealingPowerPool.Add。移动数值与战斗中不产生治疗的原分支保持。MovementService.TryMove 增加 InBattle 拒绝条件，与既有 TurnActionCommitted 限制并列，防止战斗产生的移动点用于跨地图移动；失败保持坐标和资源。场景复用治疗面板，补提神漫步的移动余量/坐标展示及下一格实际费用说明。

## 两阶段证据

- 新增同源 RefreshingWalkStateTests：12例。基线 AutomationOutputs/RulesState/20260917_001127_2ded84ef 中11失败，复现立即治疗与战斗中可跨地图移动。
- 修复后 20260917_001222_1df132a4：12个新例 + EarthStrengthStateTests.MovementChoiceMovesActualHexWithoutGrantingHealing 两个正常地图回归，全14/14通过，0.365秒。覆盖移动和伤牌独立消费、原一级/二级部队治疗、战斗只得移动点但不能地图移动/治疗、治疗点入战失效、行动提交后治疗仍可用但不能恢复移动、缺绿费用守恒、实际卡弃牌与两个池回合清空。
- 检查全库当前夹具，无既有 InBattle=True 且包含 Move 操作的案例；不以过去没有失败作为已覆盖该边界。新增本卡战斗拒绝场景补齐。
- Unity 场景 AutomationOutputs/AllOriginalCards/20260917_001536_1a42b737/advanced_card：新增12例加受影响旧主要效果2例，共14例233/233断言，63次按钮操作与初始画面成功。旧最终伤牌预期不变，增加明确 HealHand 消费。
- 已查看036强化移动后治疗、045战斗禁止地图移动和治疗截图：原卡文字、实际坐标Q1/0、移动余量2/4、伤牌1/3、绿色费用、操作True/False分别符合独立预期，无截断。
- Logic DLL 00:14:00，Scripts DLL 00:14:01，晚于源码；场景前后Console各两次0 error。14例已退出日常队列，历史保留。全局890例：306 passed、576 partial、8无有效原定义失败。

## 保留缺口

本批证明地图/治疗消费者，未证明战斗移动点的所有用途。自然复仇的“笨重”在 HeroSkillEffects 中仍有未实现注释，不能宣称提神漫步与笨重减攻联动已经完成；入战前移动点与战斗资源的生命周期隔离也须后续战斗集成审计。既有 partial 仍为 partial，不把本批用例数等同于整卡完成数。

没有重新执行无关整库、上传截图、推送远端或改动系统网络。全卡目标继续 active。
