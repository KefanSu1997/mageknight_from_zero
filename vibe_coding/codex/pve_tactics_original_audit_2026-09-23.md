# 12张原战术图：连续轮次接入依据

更新：03:19正式后台已接并完成45项本批/直接依赖验证，Console双0；详情见 `worklog_2026-09-23_original_tactics.md`。Unity原图/选择UI及场景仍待。以下“尚未实现”为接入前审计记录。

2026-09-23已逐张直接查看本地 `resources/images/strategy/` 内12张JPG，没有下载/生成新图。现有 `TacticCard` 只有Id/昼夜/先手编号/HandBonus，`RoundOrderService`仅按编号排序，**不能把这一空壳算完整原战术实现**。下面是原图信息摘要及精确文件映射，用于正式接入，当前没有为此新增“通过”。

|标识|原中文名|原效果要点|文件名（目录相同）|
|---|---|---|---|
|day_1|捷足先登|编号1，无额外效果|httpssteamusercontentaakamaihdnetugc10194487435939025545D6126220952C290F208347BC4074739124A5068.jpg|
|day_2|三思后行|取得时弃至多3张手牌（可伤牌），先抽同数量，再将弃牌堆洗回牌库|httpssteamusercontentaakamaihdnetugc1019448743593903875CA71F4BCED53F056CEA437F310E6B404D1A59C81.jpg|
|day_3|魔力窃取|取得时取一颗基本色公共骰留在卡上；本昼任一自己回合可用该魔力，使用后该回合末重掷归公共源|httpssteamusercontentaakamaihdnetugc1019448743593904313C14CF9D1E30455A88F4F3413ECC14880933330D1.jpg|
|day_4|周密计划|每回合末抽牌前若仍有至少2张手牌，本次补牌上限+1；不是全轮无条件手牌上限+1|httpssteamusercontentaakamaihdnetugc1019448743593904675239F8043D7A72F182B0363B9C72491755B892660.jpg|
|day_5|完美开端|取得时立即抽2张实际牌库牌|httpssteamusercontentaakamaihdnetugc1019448743593905007601606FC4B1CB6C89B9FAC60D3859FF0C1B23E94.jpg|
|day_6|最佳时机|本昼一次，自己回合中声明：该回合结束后立即再进行一个回合，并翻面|httpssteamusercontentaakamaihdnetugc1019448743593905317A7A4EE2DA08FCBD2C9F52C87133B6D26EC82F899.jpg|
|night_1|华灯初上|编号1，无额外效果|httpssteamusercontentaakamaihdnetugc101944874359390568492549C4931B7636EF89A34E853EA4877C61C9DA4.jpg|
|night_2|漫漫长夜|本夜一次，牌库空时将弃牌堆洗混，随机3张放回牌库，然后翻面|httpssteamusercontentaakamaihdnetugc10194487435939060598497C0B7B93F8D5CE68484E6BAF90DAF4E347239.jpg|
|night_3|魔力搜寻|每回合一次，取用源魔力之前重掷至多2颗源骰；有金色枯竭骰时必须优先选择|httpssteamusercontentaakamaihdnetugc1019448743593906500D10C9DFDAE02ADFB1E0110F9825A97AE2FB09651.jpg|
|night_4|午夜冥想|本夜一次，自己回合开始前将至多5张手牌（可伤牌）洗回牌库，再抽相同数量，然后翻面|httpssteamusercontentaakamaihdnetugc101944874359390691121CE2FFE12018311B226592B07E55E6C48680059.jpg|
|night_5|万全准备|取得时从实际牌库选任意一张入手，然后洗剩余牌库|httpssteamusercontentaakamaihdnetugc1019448743593907299F29A68A86926B9E8F544FF8FBD142908E8AA76AE.jpg|
|night_6|积蓄力量|每个自己回合开始前二选一：实际牌库顶牌盖放卡下，或翻面并取回卡下全部牌|httpssteamusercontentaakamaihdnetugc101944874359390768218F0892AFCAA11157E5143CD267240FDEE1F8EB4.jpg|

## 实现必须保留的顺序/实体

- 用上批 `GameEngine.PrepareNewRound`：完成轮次补牌、魔宠维持后选择战术；取得时效果与真实牌库/公共骰绑定。不能用`HandBonus`常量近似第4/5号效果。
- 三思后行与午夜冥想的洗牌/抽牌顺序相反，不能共用一个先洗后抽的近似方法；选择按牌实体，伤牌允许，但不是从已打出/弃牌任意挑回。
- 最佳时机复用正式额外回合调度。轮末倒数的限制须对照原规则落实，不能给别的玩家抢走最后回合。
- 原魔力窃取与搜寻必须保留公共骰实例、占用/归还/重掷时点及一次次数，不凭空添加晶体。原图明确窃取只能先选择基本色。
- 积蓄力量必须有独立卡下区，与弃牌/移除/其他持续卡区区分；每个真实回合开始前只能选一次，收回后本轮不再蓄牌，轮末归还来源需依据原规则。
- 尚未实现这些原战术效果、Unity绑定或选择UI。此表为源码接入依据，不作为场景或后台通过证据。
