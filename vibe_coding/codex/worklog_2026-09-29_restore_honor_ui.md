# 重生多部队与荣誉供应共用输入

## 实现思路
原RestoreEffect已有平原总等级3/森林5、真实所有权/重复校验与治疗/重整分离；本批只补PveDeedSelection.Units和统一输入入口，不复制原效果。null维持旧API传入targets，显式空数组表示不选。宝典/供应法术自动复用同一作用域。

UI DeedInputs增加原自有部队多选与等级合计，四部队一页；显示就绪、耗竭与创伤，原图来自单位目录。确认统一交回原服务，失败保持选择且不先付款。直接原牌保留手牌面板已有的多选和直接结算，不增加一个重复确认面板；新面板供宝典/供应等统一借用入口使用。基础复原只说明治疗点，不显示重整选择。

荣誉号召实际供应每页三张，按钮索引仍指实际完整供应，跨页保留选中引用，不改变供给或槽位规则。少于四张不显示翻页按钮。

## 验证进度
后台新增6及原直接多目标回归1有效通过，详见worklog_2026-09-29_rebirth_multi_input_backend.md。Unity实际Console编译检查两次0（tmp/pve_restore_inputs_compile.json）。场景待主代理串行执行，当前不标记场景通过。

多人读取/窃取只读审计tmp/multiplayer_mind_inputs_audit_20260929.md已完成，原响应须由其他玩家自己选牌，不切换正式活动玩家；本批未接入该项，不与重生多目标混淆。

## 场景最终证据
生成器 `tools/build_pve_restore_honor_inputs_acceptance.py` 两个variant均完成，seed27实际供应020/005/006。荣誉：合法Norowas Lv4+自身忠诚取得时实际五张供应，第二页选第四张赤袍僧侣，来回翻页实际引用保留；宝典弃白承诺并焚毁，原供应只移走所选单位，原法术保留；新单位攻击3实际杀护甲3哨卫。140728_252dfbdc一次51操作80断言，3图已看。

重生：Commoners原名望8配置允许先选两部队（移除原暴徒加北地僧侣），正式HeroBuild采用合法Tovak Lv4，五原牌。农民格挡2+决心2完整挡4，僧侣攻击3杀甲3；同回合战后宝典弃绿行进、焚毁借重生，多选实际耗竭农民Lv1+僧侣Lv2，平原总等级3恢复两单位。正式下一回合原农民移动2支付进村庄费用；另一未击败狼骑真实攻击3/英雄甲3造成一伤，原狂怒2+同僧侣3杀甲4，名望20。五次unit实例标识完全相同，耗竭False,False→重整True,True→农民再用False,True→僧侣再用False,False，部队创伤始终0、英雄真实伤牌1保留。

有效83操作120断言：140850_1f427f42前56步76检查通过，随后脚本错误点击根Btn_EndTurn；实际战后入口在Inspector，失败保留。仅修脚本从56补，141043_b82b79f6后27步44检查通过；成功前缀不重复计数。四图已目视，无布局截断。最终合计134操作200有效断言，索引 `pve_restore_honor_inputs_acceptance_index.json`。两fixture已恢复原字节；最终Console双0：`tmp/pve_restore_honor_console_final.json`。

初次刷新后的Console读取曾遇本地socket domain-reload拒绝连接；等待4秒、refresh_unity(wait_for_ready=true)后正常双0，未重开Unity或重复请求编译。无安装、VPN、重启、push或截图上传。

## 仍待
森林总5及非法目标本批后台通过而非新场景通过；普通直接原卡旧多选路径保留，不宣称所有借用组合通过。多人读取/窃取必要响应UI未实现；已审计并持久化 `multiplayer_mind_inputs_audit_2026-09-29.md`，下一批按原响应归属接入。整体目标仍active。
