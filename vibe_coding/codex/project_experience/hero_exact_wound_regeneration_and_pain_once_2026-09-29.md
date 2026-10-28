# 实际伤牌选择、精确魔力再生与痛苦单次横置

- 再生的“去除创伤”不是普通治疗。使用真实SelectedSkillWound引用，允许战斗中发动；不增加HealedHandWoundsThisTurn，不触发治疗抽牌。去除回公共无限伤牌供应，不丢入永久移除区。
- DeedCard同ID可能值相等。不得使用Hand.Remove移除目标，要ReferenceEquals查索引后RemoveAt；后台选择第二张同ID伤、保留第一张证实此点。
- 任意颜色支付不能通过ToElement：绿色/白色转元素会丢失原颜色。沿正式TryPreviewExactColors/TryPayExactColors原子费用，保留已合法取得金/黑魔力的精确消费；获取时的昼夜限制仍由原ManaSource/护符入口负责。
- 痛苦之力是一张伤牌的单次许可，普通牌横置不能清除它，失败也不能清；成功伤牌进入正式已打出区后才清，回合末自然进入弃牌。不得为“去掉手牌伤”而误写永久移除或治疗。
- 六色按钮需三列两行，为选择伤牌及确认按钮留出空间；战外/战斗共用实际引用输入，finally恢复Context。横置反馈按本次实际池差显示2，不沿用旧固定+1文字。
- 新partial UI文件使用PlayerState需要MK.Logic.Runtime；CLI不编译表现层，必须读取实际Unity Console发现和修正，不能用CLI绿色替代Unity编译。
- 未使用的单次横置许可也必须由正式EndTurn清零，不能仅依赖连续旅程换Context。新增定向后台证明旧Context不再允许横置伤牌；已在场景消费完的三条路线不受这项到期修正影响，避免重复整局。

后台/场景证据见 `../worklog_2026-09-29_wound_skills.md`。复杂复制及多人比较不在本批场景覆盖，分别记录。
