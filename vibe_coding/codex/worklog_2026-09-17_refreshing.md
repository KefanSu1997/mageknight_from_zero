# 活力焕发：原四选一、治疗目标和重整验证

原卡 basic_card_005.png 已查看。基础治疗1/抽1/绿魔力1/重整等级1至2；强化治疗2/抽2/绿魔晶1/重整等级1至3。只有手形治疗图标，四分支均禁止战斗中使用。规则依据：官方 Lost Legion 效果类型说明 https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Expansion-The-Lost-Legion-Rules-2013.pdf ，延续上一批宁静的时机规则。

## 修改与后台

RefreshingEffect 复用 HealingPowerPool，治疗不再自动去除英雄伤牌。增加 ICardEffectValidator，支付前检查四选一范围、战斗时机、自有且未销毁的目标部队及印刷等级范围；直接 Execute 同样检查。合法重整继续只 Ready，不治疗。抽牌与绿色魔力/魔晶继续复用原实现。

新增同源 RefreshingStateTests：实际出牌/牌库、原一级农民和二级魔像治疗、二级魔像与三级烈焰枪手重整伤牌保持、缺费用四分支、战斗禁用四分支、非法选择、目标缺失/外来/销毁/超等级、魔晶溢出、实际卡重复使用和回合弃牌。测试初次编译 PutUnder 参数误用数组，修正为既有单卡入口。

基线 AutomationOutputs/RulesState/20260916_235944_f02dcbb9：23例中12失败，复现治疗直接执行和缺少验证；修复后 20260917_000029_399ce4db：23/23通过，0.381秒。生产影响局限本卡，无其他效果复用 RefreshingEffect。

## 场景

新增23例加2个受影响旧治疗例。旧例伤牌预期不变，加入 HealHand 消费。新增 omitUnitTarget/foreignUnitTarget 夹具字段，使用真实原部队创建独立外来实例，不修改实际所有权判断。所选重整目标显示原部队图、等级、创伤、可发动状态；复用治疗结算 UI 显示四分支结果。

AutomationOutputs/AllOriginalCards/20260917_000412_a5e0ee84/basic_card：25例601/601断言，79次按钮操作及初始画面成功。已检查三级受伤部队重整后仍不可发动和外来目标拒绝画面。后者底部重复拒绝原因与卡图重叠，随后缩短本卡底部提示，完整原因仍保留结算区；仅补验 basic_card_005_28，结果追加在下方。

场景前 Logic 00:03:22、Scripts 00:03:25 DLL晚于源码，Console两次0 error；首次场景后同样两次0 error。通过项移出日常队列，索引878：294 passed / 576 partial / 8原定义无效案例失败。此统计不是整卡或全游戏通过；本批真实卡重打拒绝仍只有后台新增证据，原强化抽两张充足牌库等已有模块历史不自动升级为真实手牌整卡验证。

不推送远端、不传截图，不改变VPN/系统或Unity packages。后续继续补尚未迁移的治疗卡及卡区/资源后续结算。

## 界面补验收尾

20260917_000731_23e9c223/basic_card：仅 basic_card_005_28，31/31规则断言、2次按钮操作加初始画面成功。已查看002截图，底部提示不再覆盖部队卡，完整拒绝原因仍在结算区。Scripts DLL 00:06:32，代码更新后与最终场景后Console均两次0 error。通过例再次退出日常队列；定向 git diff --check 通过，仅既有换行提示。全卡目标继续 active。
