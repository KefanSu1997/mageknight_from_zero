# 2026-09-29 法师塔/蓝城原法术购买入口

本批在实际地点交涉上接购买法术，不复用旧Interactions中的假单位/简化地点实现。当前生产修改：JourneyInteraction独立购牌面板，展示实际三张供应原卡图、同色费用；选择后确认，失败显示正式后端拒绝；返回保留同次交涉；ClearJourneyChoices清空临时选择。

规则与后台由PveSiteSpellPurchaseTests核对：征服地点/同一位置及引用、7影响+同色实际魔力、原供给实体、牌库顶和即时补位。UI约定CanBuySpellAtCurrentSite与BuySiteSpell。后台先通过，再实际Unity编译Console双0，最后连续场景。

场景计划复用172048塔征服前60操作，不重复计断言/截图。下一回合金币包焚毁产生8实际影响，买原magic015付7与蓝晶，正式回合抽到手，再取真实蓝骰施法寒冰远程5杀032。没有fixture修改、远端推送或全库测试。当前尚待后台及场景，不能记通过。

## 后台和购牌场景结果

173632_0f5e5129首次13/15，两个实际新牌消费者的测试夹具漏载法术ActionCardData；仅修原定义包装，173912_98d37a53两项补过，合15个有效通过。Unity编译Console双0见tmp/pve_site_spell_purchase_compile.json及display_compile.json。

实际174109_5b424c0c成功87总操作（前60只重放），新增27操作73断言；同玩家购入magic015付7影响/1蓝晶，供应001,015,000即时变001,000,022，入牌库顶后正式抽回、真实蓝源施法寒冰远程5杀032。三图已目视。费用标签重复“魔力”已修，仅174440_358a8e43补62一图一断言，目视正常，未重验已过规则。

要塞场景现独立运行，最终Console待其结束保存；蓝城本次只后台，不称其场景通过。当前共用API名为CanBuySpellAtCurrentSite（前文短名已更正）。


最终Console：tmp/pve_site_spell_purchase_keep_final_console.json，两次error列表均空。后续本人要塞93总/67新增170断言场景174623通过，未再修改生产代码；全部八张关键图（塔3+显示补1+要塞4）已目视正常。无后台/Unity测试运行中。蓝城场景与其它世界缺口继续单列。
