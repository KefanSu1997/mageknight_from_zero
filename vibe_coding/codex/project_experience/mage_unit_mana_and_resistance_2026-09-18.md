# 火冰法师原定义、资源能力与抗性承伤

中英army_007/013核对：两者III级、护甲4、影响9、魔法塔/要塞招募，分别火/冰抗。原007 OCR错误IV已改；原强化占位改火攻挡6红费、冰攻城4蓝费。

UnitAbility增加独立GainMana和GrantedMana颜色，UnitJsonLoader只接受已审计原文；CardUnitActions先验证颜色/数量和就绪/伤牌/选项，再使用ManaPool生成同色token+crystal并实际耗竭。不会获得攻击/格挡额外加成或荣誉行动名望。原ManaPool负责3晶上限溢出与token优先支付；UseFromOffer沿用真实临时部队与许可消费，不招募/耗竭供应原卡。

PVE开放资源能力，在承伤界面仍允许法师生成魔力后分配自己承伤；耗竭与承伤资格分开，火法火抗吸收火3、冰法冰抗吸收冰2均无伤，然后真正出牌击杀。新增四快照复用原图与原JSON。资源按钮用ColorName已含“魔力”的返回值，不能再拼重复词。

后台17独立通过（201740_f5dfa883，后仅202102_e053e66a修补供应许可1例）。Unity程序集不能写internal状态，必须通过正式BattleSummonsEffect取得许可，不因CLI同程序集可用而跳过Unity编译。十场246点击697检查success；纯按钮文案修复仅补48点击105检查、两处显示，无全量回归。14原验收图+2新文案图，最终Console双0。运行时代码属于MageKnight.Scripts.dll，核对该程序集时间；Assembly-CSharp.dll不能代表控制器已更新。

缺费、受伤/非法选项、魔晶上限溢出和供应许可仅后台；完整招募、轮间流程、战后首次生成及全部组合未包含当前场景结论。证据pve_mage_acceptance_index.json，重放tools/build_pve_mage_acceptance.py（--labels-only只核对文案）。
