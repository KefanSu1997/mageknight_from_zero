# 魅惑定向场景续验与本地 MCP 恢复

## 范围与修改

延续既有 magic_021（魅惑／附身）修改，不重跑全卡库。复用原 SO、原卡面、CharmEffect、RecruitmentService 与原版农民/僧侣供给。

既有实现：基础奖励只接受红蓝绿白魔晶或一次招募减三，非法选择在付费前拒绝；非交涉仅加影响力4。CharmRecruitDiscount 与通用折扣分离，招募成功才消费，地点/费用/指挥槽失败时保持供给与资源；回合结束失效。验收控制器展示真实供给卡面和队伍，逐次点击招募并比较费用、影响力、队伍与供给。18项定向 EditMode 已在 20260911_222437_8cdde8d7 通过，相关游戏代码本次未再修改，不重复执行。

本次仅修正一个错误用例预期：magic_021_52 原先要求结束回合后 InfluencePool=4，这与未使用回合资源失效规则冲突。改为结束后0，同时增加 played:InfluencePool=4，分别检查出牌确实获得4以及结束回合清零。不是用实际值回填并放宽标准；保留原失败报告，折扣、魔力、魔晶、队伍、供给断言均未删除。

MCP 原菜单直接写 EditorPrefs，但插件 BridgeControlService 从 EditorConfigurationCache 选通道，造成同会话缓存不一致。Assets/Editor/McpHttpBridgeMenu.cs 现在配置后显式 Refresh 缓存，保留 HTTP 入口并增加手动 Start Local Stdio Bridge，使用现有插件的互斥通道切换。没有自动启动旧 loop、安装插件、修改 PackageCache 或改动 VPN。只使用本机127.0.0.1连接，读取 Console 和调用菜单。

## 真实验证

- 第一批：AutomationOutputs/AllOriginalCards/20260915_011309_22987088/magic。33例、114次按钮操作（report含初始视图共115步）、387条UI断言；621条效果断言，最初620符合/1不符。结果7 passed、25 partial、1 failed。截图50张留本地，检查了原卡原部队、第二次招募资源不足被拒绝和回合结束失败图。
- 仅重测失败：AutomationOutputs/AllOriginalCards/20260915_012016_42584824/magic。仅 magic_021_52，3次操作，23/23效果断言，report success，case partial。检查最终截图 003_magic_021_52_execute.png，文字完整且影响力0、折扣0、23/23一致。
- 合并本批最新结果：33例、622/622效果断言符合，7 passed、26 partial、0 failed。partial的完整卡区、其他组合和回合行动限制仍须补充；不代表魅惑整卡或全卡库完成。
- 保留657条回归库。先用 --failed-from 首批报告选出1例，修正后用 --cases magic_021_52；补测结束再次 --failed-from 清空日常队列，不删除历史失败或partial未覆盖范围。
- Unity MCP read_console：修改前01:12连续两次0 error；桥接脚本编译后01:20:13连续两次0 error；补测退出Play Mode后再次连续两次0 error。本地通道在此次域重载/场景退出后仍能读取Console，修复经过实际使用验证。未新增无关测试。
- git diff --check 对本次桥接脚本和用例生成器无空白错误。没有提交、推送或上传截图。

## 下一批

本批局部断言通过不等于全目标完成。历史仍有7个有效卡面的失败（魔法天赋、仪式攻击、时光传送、清风之翼、公正之剑、星辰之弓、法术宝典），以及4个无有效牌面的来源问题；大量partial需要补齐实际后续行为。后续按失败和未覆盖规则挑选用例，禁止重新跑整库。
