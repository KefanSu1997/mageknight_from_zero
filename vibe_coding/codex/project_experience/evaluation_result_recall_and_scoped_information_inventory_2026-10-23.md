# 完整结算回查与有限信息分母

## 场景与原因

cycle01实际独立评审抓到凝结消费后完整条件在顶部和右侧同时截断。丢失的牌已离手，不能靠“以前选牌时看过”满足当前必要信息保留。既有画布下完整结果改放原Inspector，保留同战斗历史和22pt滚动阅读，行动选择可返回。

## 实际踩坑

- W.Button内部标签高度会比按钮少10px，33px按钮里的21pt中文完全没有生成；可点击不是可读证明。实际原图+TMP firstOverflowIndex=0识别，必须提升可用高度而非缩字。
- 回查按钮挤占Feedback区域，会让原三行反馈在22pt溢出。保留完整最近操作说明供回查，常驻确认缩为两行/一行；当前卡、目标、模式和支付区域仍展示，拒绝原因保留。
- 只在Act/BeginBattle清理history不够，原生fixture或journey会直接重绑BattleSession。Act及Draw入口的session引用守卫同时覆盖；同一session阶段切换不清空，真正新session才清空。
- 原文生成完整不证明滚动clip可见。必须保存顶部/底部实际截图；本次Windows真实wheel两次使正文从y295到256.49且bottom675，正式状态不变。

## 度量边界

必要信息须先声明有限角色及情境分母。新v2为17情境114角色，明确结构/内容/生成检查114/114，不冒称全游戏信息或无遮挡像素覆盖。用UTF-16 sourceIndex逐字匹配生成glyph可准确检出省略号和空按钮；不能替代原图审查。

固定截图9问的独立模型读取为9/9，仅该模型该题集；与真人搜索时间/误操作率分开。客观原始测量无1—5阈值时保留null，并解释缺锚点而非缺功能。

## 历史恢复

当前版本补图不能冒充旧轮源码截图。原30轮round14缺图门禁真实存在，其CAS与当前有12个Assets差异，含7个Logic文件。此次用managed worktree恢复4,450归档Unity文件，导入后再SHA验证，实际编译/10图/正常开局补证；标注事后重建和新MVID，不填假历史时间。当前主工程保持原字节，回来再实际验证MVID和正常开局。不要靠放宽门禁或覆盖当前规则解决历史证据缺口。

路径：`AutomationOutputs/RsiGame/evaluation_iterations/20261023_cycle02/README.md`、`tools/RsiGame/verify_resolution_recall.py`、`measure_required_ui_information.py`、`audit_ui_iteration.py`。
