# UI成本：回合/轮次批次完成，剩55

当前固定分母1,009组/4,083样本不变；3,833实测、194 UI不兼容、1正式规则拒绝，总4028解析（98.7%），55未解析。857组全实测，976组全解析。总体基线和优化结论尚未完成，Goal active。

## 本次实现与验证

- CostFixture新增实际已完成回合/轮次/Context引用变化的审计观察。仅跟随真实UI调用更新协议Turns/Rounds/TaskCursor/PendingEnemies，声明EffectSeed初始化真实新回合Context；不安装预期后状态。独立prefix期间禁用此观察，完成后重置观察游标。
- Progression实际ChooseLevelReward成功后通过真实新生非伤卡登记Acquired。初版AdvActionCard不能转换DeedCard的CS1503已改成实际owned-card差集；新域重载后Console双零。
- Python nextTurn通过实际手牌逐张选择回合末弃牌、EndTurn、实际升级、ChaosKeep、Announce/EndRound、魔宠维持、night_1战术和BeginTurn串接原宏动作。
- 第一次roundFinish测试点击EndRound后脚本误再次找EndRound。已纠正：仅按钮实际可用才进入EndRound分支。没有据此录入不兼容。用户已明确告知之前入口判断错误。
- 免费强化把原base参数映射到明确同Target/Option的对应strong UI分支；所有原动作参数仍保存，完整游戏状态验证不变。宝典magic_020 strong的offeredUnit/disband按实际HonorOffer/Disband控件映射。
- clearDefend在真实已清空的状态且公开enemies.defendSelected空、无打开的防护面板时可零输入完成；独立verifier增加相应严格无效果检查。不放松A必须有实际鼠标端点的既有门禁。

## 已完成并入库的不可变批次

H:/MageKnight_RL_Backups/UiFluency_future/ 下：

1. 20261013_turn_gate1：4成功/8快照，SHA07767977dd81a19c6a91142490ba2a43f2e910d6dc891088be408ae990d751cf。
2. 20261014_round_gate3：4成功/8快照，SHA3b03433a5e1e0f7eff45bda4ae5c5b175ab2bdd59f3c5104beea7d8f61c95e7d。
3. 20261014_payment_last2：2成功/4快照，SHA094c3761bbabcd60fb1984eaf7e36489df5206c77161b0ce14114dee9156b35b。episode978/26和5138/21通过，之前付款参数缺口已补。
4. 20261014_turn_round_remaining：289尝试/277成功，554完整快照独立passed，SHA7572e8dbfc2a53fcd58508462c761a3f047b99bafef8db8393f5c7cd14c355c0。
5. 20261014_turn_round_incompat12：12独立结果（11旌旗UI缺入口+1原轨迹EndTurn被规则拒绝），SHAc611752c6a8b598eb1790a9df7a3710bfc4900d9b1c39dc4904060fd46a194a1。
6. 20261014_residual_all189：189尝试/116成功，232完整快照独立passed，SHA9940c016588bee1fdeb957684e060d2cc7e393a902378529e5fc88cf049a500c。
7. 20261014_residual_incompat73：18独立结果，SHA24aba491c11054bcf62a507e76d103ccfc3bc67c50246846af88bb5062412234。涵盖原子转换数额无控件、嚎叫模态阻挡单位顺序、神器横置缺入口、1单位移动/影响阶段范围不一致。

所有以上exec会话均已terminal并入库。当前没有测量/核验worker；最后proof keep-live留下owned pilot：`H:/MageKnight_RL_Backups/UiFluency_future/20261014_round_gate_incompat4_recovered/live`。下次先fresh observe attach；C#编辑前正常停pilot。

报告新增verified_native_refused_samples和verified_ui_incompatible_samples；原verified_incompatible_samples为两类合计保兼容。规则拒绝明确不当UI缺功能证据。原轨迹episode2443/step9在未决定魔力开源源骰归还/重掷时被正式EndTurn拒绝；实际UI显示待选择对话框，完成成本null而非0。

## 剩55的实际分类

- 25原地图fight路由：当前mapper只找Btn_JourneyEncounter_i，OriginalWorld实际overview有不同地图/地点入口。必须检查真实地图路线，不能凭按钮名字不存在就判不兼容，也不能增设测试快捷按钮伪造成本。
- 11防护面板/阶段衔接。
- 5神器在overlay后选牌。
- 5单位能力（1移动/影响已独立验证并入库，其余仍未知）。
- 3多目标Block预览：TargetPreview在Block调用_targets.Single；部分native prefix恢复多目标防护面板触发这个异常。需区分fixture pose是否设置过度与实际UI问题。当前未修改此函数，未据此判不兼容。
- 2 legacy初始化（episode233/51、233/54）。
- 免费强化等效分支1、已使用格挡来源1、killCrystal控件1、BtnSkills缺失1。

不要继续推演无证据的zero-A前驱：本次实际查到clearDefend A大多有native预选，已有多个真实Cancel点击成功。以实际失败快照定位。

## 旧任务登记新证据：重要

旧helper recover_cost_task_definitions.py只读D盘pool JSON和已恢复episode任务，未搜索H盘备份。只读扫描40 ZIP发现143 pool JSON；所有5个未解task_id都在part_016.zip的旧pool里。但它们明确是legacy_seed注册记录，不含现代TaskDefinition，不能误称找到了声明任务。

- `AutomationOutputs/BattleRl/20261007_recurrent_task_gate/seven_hero_solvable_pool.json` SHA11699c8bd2bf621dc06fb6f046ff1a5c2f024bf36fc79a4c202d9136f95e5d8a，包含f942...、c619...、b7e9...、da5b...。
- 同目录`solvable_pool.json` SHAa28372faa176f15d3bddda4fd3048bfdbd668a19f7f235dc11ec28d1372e71c6，包含1056...。

episode233登记task_id b7e9ace58babe75816580327b0925528009a7bc5e986392b8461e1d938d063df：generator legacy_seed，seed601425，rules SHAe099b3c9df5b9b849cd677dd3115e398d36a9414472fc93ac83e7d857465bd22，checker SHAc6ac454c099ff94d2cbb0ab4cbd4655d79bd3aa7f988203d434b3fefd0850616，catalog SHA10f8adee934a22e5ea6bd245b025ff1908280130d58dbbd6d0b7dde2888dccec，initial state SHAae3b29ea0c5d9ce06dbb3963c287462cd91e0eba27a761c1af0e07c260faff7e。

其第一真实before：Braevalar，Outside，Level1/Fame0，单位army_003，自有额外卡items_020/items_023，artifactSupply23，unitOffer army_2_001/005/006，87字段，无taskProgress。不是现代独立任务省略header。应追溯登记evidence/checker配置和对应原Env(seed)构造/旗标，绑定原SHA，根和实际prefix逐步检查；不得猜现代多流种子或安装预期状态。

JourneyTraining.cs Env(int seed,TaskDefinition task)保留旧无task构造：hero=OriginalHeroes.All[seed%7]，单位Random(seed^0x1357ab)，varied Random(seed^0x4d4b120)，artifact Random(seed^0x2ab417)，六相关独立流默认为seed，EffectSeed=seed^0x192abc；Encounter(offset)按原Enemies数组/seed公式生成。旗标和原catalog顺序须从登记/绑定源码核定，不凭当前源码或before库存猜。当前CostFixture仅支持声明任务，所以先前错误称generator unclassified仍保留，未完成legacy恢复。

## 编辑器恢复

上一Editor实际退出，Windows提示Scene Backup Detected。已用新版本Windows Computer Use skill `.../computer-use/26.1007.71326/skills/computer-use/SKILL.md`选择Keep Backups，未删除备份；项目通过Hub打开，当前Editor PID7756创建2026-10-14 14:47:04。MCP server已后台恢复PID37392创建14:47:29，H future `20261014_mcp_restore.*.log`。通过Tools/MCP/Start HTTP Bridge恢复，实际Console双零。

UnityMCP工具namespace仍不暴露，使用local_unity_mcp.py端口60022读resources/read mcpforunity://editor/state和调用工具。禁止因为一次timeout重启；先核对同一进程/资源/窗口。早前round_gate1在域重载后失去运行时对象，observe/reset都NRE；正常stop虽返回旧NRE，但已单独确认退出Play后才重新启动。所有失败证据未删除。

Goal继续active；不能以98.7%或仅支持子集宣称完整基线完成。离线atlas仍是3302快照版；新增成本未反向伪补到那个旧图谱。
