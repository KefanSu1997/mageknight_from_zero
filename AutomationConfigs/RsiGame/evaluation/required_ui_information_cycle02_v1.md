# cycle02：必要信息测量的预声明范围与缺证原因

本文件与 `required_ui_information_cycle02_v1.json` 在 cycle02 新截图和独立评分前创建。参考 cycle01 已有17个情境来定义有限信息清单；对旧截图运行只用于诊断工具，不补写旧评分或评分锚点。

## 已可计算的范围

`tools/RsiGame/measure_required_ui_information.py` 给17个情境声明112个必要**文本角色**。角色包含阶段、当前步骤或所选行动完整说明、操作反馈、资源、显示敌人的名称和数值、手牌计数；公共魔力、结局、消费结果各有情境专用项。重复敌人面板逐个输出，三敌及双敌情境还要求最低组件数。这里不是“游戏全部必要信息”的分母，也不是112个语义事实均已正确的证明。

每条保留完整文本及SHA、TMP overflow、UTF-16源字符索引与实际生成quad的字符匹配、缺失/替代字符、屏幕外quad、字号和alpha。图片SHA及尺寸与几何尺寸交叉检查。省略号不能用fullText字段存在来掩盖，字符替代也不能当作原字已经生成。脚本拒绝覆盖已有结果，且不产生1—5分。

cycle01 after_v2诊断：17张图片尺寸全部对齐；112角色的组件数及有限关键词检查满足。`source_consumed` 的 Feedback 98个非空白预期字符仅84个匹配生成，Result 88个仅46个匹配生成，两者均有TMP overflow。其余角色“生成文本完整”不等于无遮挡或可读性验收。诊断输出在 `AutomationOutputs/RsiGame/evaluation_iterations/20261023_cycle02/measurement_preflight/cycle01_required_info_diagnostic_v2.json`。

## 仍不能声称补齐1.1/1.2的原因

- 缺动态/语义证据：原卡悬停是图像，不在TMP文本角色分母里；卡面完整费用、效果、图像遮挡须独立读原图并对照原卡。敌人组件最低数也不是原生敌人状态完整性断言。
- 缺可见字形证据：现有仪表导出生成quad，没有每字像素mask、裁剪链和绘制遮挡。脚本的`actual_visible_glyph_coverage`固定为null，必须由独立原图逐角色核验，或增加像素mask/裁剪/遮挡证据后按预声明算法计算。屏幕内quad和alpha大于0都不证明文字未被覆盖。
- 缺评分锚点：这些是事实清单与几何检查，未定义阅读字号、对比度、检索正确率到1—5的映射。不得为凑数在看结果后自创阈值。真人搜索时长、误操作率不能由模型读图替代。
- 后续如果新增“结果回看”按钮/面板，应在截图和评分前升级spec，声明入口、摘要、展开详情和关闭后的返回情境；v1不暗中把新组件算为旧角色通过。

## 1.4与反馈的可行后续测量（提案，未当作已验收）

现有截图能显示相同卡名，但同名实体不等于同一物理实体。可将一次操作链的原生实体ID、handOrder物理槽、played/discard位置与UI按钮绑定逐步归档，再以实际消费前后两端对应关系统计。仅用`Btn_Card_<card id>_<index>`路径或同名卡片截图不能证明物理连续性；布局重建也可能复用索引。该指标需要额外的原生实体绑定证据，不能从现有静态截图补造。

现有pointer_events/interaction_checks可证明实际派发点击、选择保留、拒绝与原生前后状态。可对已归档的确定操作链预声明“点击后反馈文本包含操作号/动作摘要、拒绝原因、选择是否保留”并测覆盖，分母必须是这些明确事件，不外推所有操作。当前捕获的Editor响应时间与首完成帧不等于首有效视觉反馈帧；60个帧间隔不等于整个游戏长期性能。需要带帧序和事件时间的连续图像再定位首个有效反馈变化，人的感知延迟仍需人测。

## 新一轮使用

```powershell
python -X utf8 tools/RsiGame/measure_required_ui_information.py --captures <cycle02新采集目录>/key_phases --spec AutomationConfigs/RsiGame/evaluation/required_ui_information_cycle02_v1.json --out <全新版本目录>/required_ui_information.json
```

独立评审应收到spec、输出JSON和同SHA原图，逐项区分生成几何事实、独立可见性判断、语义正确性和评分锚点。脚本本身不取代独立评审。
