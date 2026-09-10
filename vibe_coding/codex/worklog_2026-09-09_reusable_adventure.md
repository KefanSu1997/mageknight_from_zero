# 可复用冒险场景与卡牌操作重构

## 用户确认
采用卡牌/部队→目标→确认的桌游式交互；规则按需展开。背景、英雄、怪物、部队各自具备素材，遭遇通过数据组合，不再把剑士VS兽人画进同一张图。

## 计划
1. 定义可独立编辑的角色、地点、卡牌和遭遇资源；纯逻辑层接收定义快照，运行状态与素材分离。
2. 提供复用的角色展示组件及场景组合器；至少两种敌人、多个背景组合证明可替换性。
3. 四场景共用手牌、目标检查器和单一主要行动；取消加减数值和大排案例按钮。战斗按格挡/攻击阶段推进，探索和招募根据目标自动选择行动。
4. 保留并迁移数值验收，验证扣牌、魔力、目标、阶段、失败资源守恒及实际UI读数；另测同素材不同遭遇和数据改动影响。
5. Unity Console双扫描、全量EditMode、自动化截图与原生鼠标验收，记录可扩展方法和范围限制。

## 基线
保留当前未完成历史合并与全部既有内容。本轮前备份：D:/study_and_work/multi_agent_system/recovery_backups/reusable_scenarios_20260909/rules_baseline.zip。

## 进度
功能与两轮自动化验收已完成，工作跨至2026-09-10凌晨；输出目录保留本轮开始日期20260909，避免拆散证据。

## 具体实现

新增Assets/Logic/Runtime/Adventure纯逻辑层（内容快照、卡牌实例与命令会话、分目标/元素的战斗累计）。复用原有BattleResolver、MovementService、RecruitmentService和PlayerState；手牌不再以演示数字代替实际卡牌集合。

新增五种ScriptableObject内容定义和ActorView Prefab：6份独立角色美术、3个地点背景、7种训练卡、3份遭遇、5份冒险。模板生成器只补缺失定义，避免覆盖设计修改。已有四个Rules场景接入新共享界面，通过运行时菜单选择配置；抽牌场景保持原样。

手牌/部队→目标→确认；卡牌消耗、强化魔力支付、伤牌入手、招募条件、回合补牌和单位耗竭统一进入逻辑命令。移除玩家界面的加减数值/案例按钮；规则默认折叠并可滚动。

## 编译与修复过程

- 初始出现一次Monster名称歧义，改为MK.Logic.Data.Monster后Console归零；没有大量新增编译错误。
- 首次原生窗口检查发现卡牌值文本区过低，扩大高度后截图恢复显示。
- 补齐伤牌实例同步，覆盖部分格挡完整受伤、毒素、麻痹、跨回合补牌和行动卡守恒。
- 第一轮95次操作全通过，但逐图检查发现地图上排侵入标题、选牌后迅捷说明截断；修正位置与文本布局，加入规则滚动，再跑全套。
- 第二轮全量EditMode82通过/0失败/0跳过，95次按钮操作全部通过，99张正式PNG。
- 原生鼠标另测选坚守→兽人→确认，观察手牌5→4、格挡5/4；展开规则、菜单切换森林灰狼均正常，截图放在manual/。
- 最终Console双扫描的时间和0条error结果见console_final_1.json、console_final_2.json及验收总览。

## 文件与复现

- 说明：`vibe_coding/codex/adventure_reuse_guide.md`
- 素材记录：`vibe_coding/codex/adventure_art_prompts_2026-09-09.md`
- 配置生成：`python tools/build_adventure_acceptance.py`
- Unity菜单：`Tools > Mage Knight > Adventure > Run All EditMode Tests`、`Run All Four`
- 故障注入：`Tools > Mage Knight > Rules > Verify Wrong Expectation Fails`
- 汇总：`python tools/summarize_adventure_acceptance.py`
- 输出：`AutomationOutputs/ReusableAdventure/20260909/`

## Git与检查范围

工作区起始就有未完成历史合并：HEAD/origin/main=7e137095，MERGE_HEAD=dcbecfe4，索引树670cd777（4097个历史变更）。此前已通过HTTPS地址改写执行fetch，远端main未变化。为保留旧工作没有执行checkout/reset/merge-abort/add-A，也没有把旧合并提交成此次功能PR；本轮按开始时备份单独生成补丁与文件包供审阅。

仓库无Makefile/专用format、lint脚本，package.json的test为占位失败命令。使用改动文件空白检查、Python语法编译、Unity实际编译、全量EditMode和场景自动化作为相应检查，不把npm占位命令声称为测试。

当前完成的是可扩展二维桌面Demo；正式卡牌全部效果、全部单位行动、完整地图拼版、远程/攻城、存档和Player/手机构建未在本轮完成，具体边界见复用说明。
