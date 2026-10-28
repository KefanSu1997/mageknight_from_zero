# 阿瑞希亚三思后行：修复遗漏的战斗试玩入口

## 原因与修改
用户截图手牌3、牌库2、初始红蓝晶各1，符合旧的五张构筑入口。此前默认英雄页改动只覆盖标准地图开局，英雄页中间的“以此英雄开始已选构筑”仍调用 CreateConfiguredPlayer，将五张配置牌当作全部牌库。前一次验收没有覆盖用户实际选择的入口。

英雄页的主要“完整16张牌 · 战斗试玩”现在和标准地图开局共用原英雄 CreatePlayer 初始化，复用原16牌、原图与正式洗牌/抽牌；保留自定地图选敌战斗。正式三思后行的先弃牌、从原牌库抽牌、再洗回规则不修改。独立小牌库入口保留给既有重放，明确标为“高级演练 · 已选小牌库”，按钮ID不变，避免破坏已通过演练。页面说明同步去除“原卡与魔力取自演练配置”的错误泛化。

## 定向验证
- CLI：OriginalHeroJourneyRethinkReplacesTwoCardsBeforeReturningThemToDeck("Arythea") 1/1通过，报告 AutomationOutputs/RulesState/20260930_003852_7e0e06e9。核对实际换入引用、换出引用不在手、16张守恒，以及真实下一回合仍5张。
- 场景：AutomationOutputs/PveBattle/20260930_004113_53e4a394/report.json，10操作25断言全部通过，已查看换牌完成及兽人战团截图，均显示手牌5、牌库11。新增 tools/build_pve_manual_play_acceptance.py hero_demo_redraw，10操作25断言：原阿瑞希亚→完整16牌战斗试玩→三思后行换2→实际回合→兽人战团。
- Console：tmp/pve_full_deck_entry_compile.json 编译后两次0错误；tmp/pve_full_deck_entry_final_console.json 场景验收后两次0错误。
- 最小必要：不重跑攻击分配或全卡库，不推送远端，不上传截图。
