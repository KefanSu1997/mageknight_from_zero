# 冥想／神游：弃牌实例、底牌顺序、一次补牌

- 原卡为`Assets/GameData/cards/magic_en_002.png`。基础随机选两张，强化自行选两张，不足时选全部；可放顶或底，下一次补牌上限+2。
- DeedCard是record，值相等不代表同一张实体牌。强化应付费前核验来源、数量与重复，不能Take(2)静默忽略多选，也不能接受外来同ID对象。移除必须用ReferenceEquals查实际弃牌，通过`TryRemoveFromDiscardExact`完成。
- `Stack.ToList()`已有顶到底顺序。放底时从该列表追加，再Reverse入栈即可；先读Reverse再追加会翻转原有牌序。共享`PlayerDeck.PutUnder`修复必须覆盖实际调用者与不同ID的牌序，不能全用同一种牌遮蔽问题。
- 选择顺序就是落入牌库的顶到底顺序；放顶时需要逆序Push。基础随机用已有EffectRandom注入，未注入仍采用真实Random；测试不是另写回收规则。
- 一次+2必须通过真实EndTurn验证：第一次补到7，弃掉可弃非伤牌后第二次只补到正常5；跳过补牌保留加成，牌库不足不重新洗入弃牌。还要检查法术手牌→已打出→弃牌，以及伤牌回收后的实例/总数守恒。
- 后台26新例与2相关回归通过后，场景27例763效果检查、106UI步骤通过；证据`AutomationOutputs/AllOriginalCards/20260917_045804_3eee7249/magic`。前后Console均双零。工作记录`vibe_coding/codex/worklog_2026-09-17_meditation_discard_order.md`。
- 只将本次范围从日常队列移除，partial仍保留，不把当前卡组操作链通过等同其他组合或完整游戏通过。
