# 重生原多部队输入后台

原规则来源：resources/text_json/magic.json magic005，重生治疗3/森林5，同时重整一支或多支总等级3/森林5部队；复用原RestoreEffect.Validate与Execute，不改数量、费用或规则。

仅修改PveDeedInputs：
- PveDeedSelection新增UnitState[] Units，默认null保留旧外层targets或单unit；显式空数组为本次不选部队。
- Supports含magic005；该ID用专门作用域临时赋ReadyUnitTargets/TargetUnit，finally恢复。没有Units时保留原PlayOutsideBattle(...units)的真实目标，不以默认空selection覆盖；Unit单选仍可兼容。
- MaterialArtifacts和OfferedSpells原有共用Execute桥自动获得多选，无需额外生产修改。UI主代理处理。

新增PveRebirthMultiInputTests六例：平原两部队总3、森林三部队总5实际重整后再次发动并正式格挡；重复/外来实体/超额总等级费用牌区守恒；普通治疗消费手牌伤、显式空目标不重整。所有法术供应保留实际引用、宝典焚毁/绿色支付真实去向、临时输入复原。旧EnergyFlowAndMultiTargetRebirthKeepUnitIdentitiesAndWoundsSeparate证明直接多部队入口兼容。

CLI首轮20260929_140115_ed65579c共7项6通过1失败，森林例尾部误期望普通格挡找零2；原正式格挡消费整笔贡献为0。仅修测试该断言，补单个失败参数20260929_140158_961e4bf2 1/1。有效新增6/6+旧直接回归1/1，保留首次失败报告；生产效果未放宽。未执行Unity/配置/远端传输，场景与Console由主代理后续验收。
