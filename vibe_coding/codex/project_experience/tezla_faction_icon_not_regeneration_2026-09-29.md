# 原敌人阵营图案不能识别为战斗能力

官方入口 https://wizkids.com/mage-knight-shades-of-tezla/ 的 Download Rulebook 指向 https://eadn-wc03-13179282.nxedge.io/rules/MK3_Rulebook%20Final%20Resized%204.21.pdf 。本次先HEAD核体积，下载一次2,774,401字节，本地 `tmp/pdfs/tezla_official_rules.pdf`，SHA256 `b03d48c1cff13b27fee06d8ca2cfc713f130ec8a72c6b5dbbcc4a01aaf5d9f2e`；未安装依赖，使用既有Poppler只渲染第2、3页。

第2页明确名望右侧符号表示阵营；第3页原元素派系图与本地monster015/054/057右下绿色叶图一致。因此不是再生，不能由洞悉等忽略攻击/防御能力的效果移除。仅靠图形位置猜测不够，须与原文及图例一同核对。两份正式JSON删除这三处误识别regenerate，并保留独立faction元数据；未改印刷数值，尚未将阵营转为运行时奖励。

阵营击杀有独立奖励：正式规则可选奖励标记，或采用官方Just Fame变体额外1名望。已向用户询问偏好，未把删错误能力当成奖励完整实现。后续应独立表示阵营，统一覆盖远程、近战、直接消灭奖励与UI；不能简单把印刷Fame改大，也不能只改单一结算入口。

另发现057原图双护甲1/2及盾1，当前旧JSON/Monster模型缺相应闪避和Defend规则。删除regenerate只修误识别，不证明057全规则正确。现有SuccessfullyBlockedAttacks可复用，但必须检查该敌全部攻击槽，不能仅用已格挡过任意一槽的标志。后续定向接入，不扩为全部敌人无差别重测。

本批后台201953_129251ff 1/1、37断言检查双副本、三原ID的误识别删除和旧数值保持；场景202312_1e4dc463仅22总操作含21重放、6断言/1图，主代理确认原015火冰双攻击/抗性仍在、无再生显示。编译和最终Console各两次零错误，见 `pve_elementalist_label_acceptance_index.json`。不把标签检查说成击杀奖励或057全规则验收。
