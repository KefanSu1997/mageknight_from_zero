# 英雄技能构筑共性牌库问题

用户第二次指出换英雄仍手牌3/牌库2；截图托瓦克名望15护甲3、红蓝晶各1、英雄技能入口，对应 CompleteHeroBuild 而非此前 BeginBattleHero。此前只测试另一条正确路径，不应据此宣称整个问题解决。

## 共性原因
_buildOriginalDeck 默认false，CreateConfiguredPlayer 的配置手牌被当作完整牌库。技能构筑入口没有设置模式，返场/换英雄会沿用演练模式。原牌后台 Rethink 先抽后洗符合原文，缺陷在开局。

## 修改
- 正常 Btn_HeroBuild 通过 OpenHeroBuild(true) 显式选完整16牌，重置旧合作草稿和技能选择。每次正常入口都重设牌库模式。
- 原16牌按钮改为幂等选择，重复点击不能切回小牌库。
- 标准原地图、战斗试玩、技能构筑统一复用 CreateOriginalHeroPlayer 的原卡定义/原图校验与原英雄初始化。
- 完整英雄牌库忽略 _draftDeeds 的小牌库草稿（包括空草稿），保留构筑的合法等级、资源、部队和技能；不向游戏进行中的玩家凭空补牌。
- 小牌库＋技能为显式独立演练入口；原有50个生成器只迁移到该入口，步数/断言/构筑不改变。原有选择完整牌库的脚本仍选择原16牌，未重跑无关卡库。迁移索引 tmp/hero_build_fixture_entry_migration.json，全部Python语法检查通过。

## 开局路径核对
| 路径 | 牌库语义 |
| --- | --- |
| 英雄标准地图 / 战斗试玩 | 本英雄完整16原牌 |
| 原16牌技能构筑（单人/合作） | 本英雄完整16原牌；合法技能/等级自定 |
| 高级演练·已选小牌库 / 小牌库连续演练 / 小牌库＋技能 | 显式原卡小牌库，保持真实有限库存 |
| 单场演练 Begin | 固定配置，跳过选战术，不冒充正常开局 |

## 验证
- 七英雄后台换2张：7/7通过，AutomationOutputs/RulesState/20260930_005515_67ff96cf。真实换入/换出引用与牌区守恒。
- 场景通过：AutomationOutputs/PveBattle/20260930_005932_7bf3ab9a/report.json，87操作248断言全通过，三张关键截图已目检。tools/build_pve_hero_build_deck_regression.py，先遗留空手牌草稿和小牌库模式再返回正常技能构筑；七英雄逐次从前一场出战配置重新开局，名望15/护甲3，托瓦克含合法原技能；每位换2张并进入兽人战团，87操作248断言。只在托瓦克换牌/战斗与最后英雄留3张图。
- Console：tmp/pve_hero_build_shared_compile.json 编译后两次0；tmp/pve_hero_build_shared_final_console.json 场景后两次0。
- 汇总：pve_hero_build_shared_deck_acceptance_index.json，七英雄逐个记录实际战斗手牌5/牌库11。
