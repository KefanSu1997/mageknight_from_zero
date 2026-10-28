# 多人思维读取/窃取正式输入后台

依据多人输入审计和原MindReadEffect实现，不另造效果、不替其他玩家自动选择。

- PveDeedInputs新增公开record PveMindReadResponse(PlayerState Player,DeedCard Card)，PveDeedSelection新增MindResponses数组与MindStealTarget。
- MindResponses默认null沿用原Context输入兼容；非null数组是本次完整明确响应，null Card是显式展示，空数组是零响应而非自动展示。
- 仅magic018安装响应scope；先拒绝null条目、重复或非实际OtherPlayers引用；原validator继续核遗漏、同色真实手牌、无色展示资格与原行动偷取资格。无需切换Game.CurrentPlayer或启动其他回合。
- finally还原原MindReadChoices字典内容及MindStealTarget；成功的MindReadRevealedHands快照保留至正式回合结束。不回滚效果、不提前弃牌。

新增PveMultiplayerMindInputTests 11例：真实双玩家普通响应与晶体支付行进、正式轮转；强化偷同一原行进实例并实际支付强化移动/原主失去归属；明确不偷；无同色展示及末回合清理；缺响应、重复、同ID假玩家、同ID假牌、错色、虚假展示、缺原白费均在付款/牌区前拒绝并恢复输入。另原StrongMayDeclineStealingAndBothActionAndSpellStayWithOriginalOwners一项兼容回归。

CLI `20260929_141702_c857f3ba` **12/12通过（新增11+回归1）**。未更改UI、效果、配置；未操作Unity。后台通过不代表新的共享设备选择UI/连续场景已通过。
