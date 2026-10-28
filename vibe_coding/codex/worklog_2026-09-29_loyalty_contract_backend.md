# 忠诚契约：专属槽、真实供应与招募后台（2026-09-29）

## 实现边界与接口

`HeroSkillAcquisition.Validate(player, skill, UnitSupply units=null)` / `Acquire(player, skill, Random random=null, UnitSupply units=null)`：忠诚首次取得必须传真实已准备供应；其它技能兼容旧调用。同实际技能再次Acquire幂等，不能重复追加或增加槽。`UnitSupply.AppendRegularToOffer(maximum=2)`只出队现有普通单位卡加入现有Offer，不清原供应、不制造卡、不取精英；不足只加实际可用张数。

PlayerState持久保存LoyaltySkill原技能实体与LoyaltyUnit实际单位。CommandSlots含专属槽，OrdinaryFreeSlots剔除该槽与真实绑定单位，FreeSlots再计未用专属槽；因此默认普通招募在普通槽满时拒绝，不自动偷占空专属槽。原ExtraCommandSlots不被忠诚增加。

显式ActionContext.UseLoyaltySlot选择招募方式。GetCost只对有效空忠诚槽的当前交易减5并最低0；不写全局RecruitDiscount。费用前验证真实空槽/指挥旌旗互斥/不得同时解散，成功后绑定刚录入RecruitedUnitsThisTurn的新UnitState并清本次选择。免费取得允许显式选择空忠诚槽，不自动绑定，仍与旌旗互斥。

LoyaltyActions.ValidateDismiss被PlayerState.Disband和免费招募替换共用，禁止主动解散绑定单位。真正战斗销毁/移除仍允许；IsBound同时查引用、存活与仍在Units，SlotAvailable自然释放，后续招募覆盖旧引用。旧CityInteraction攻城失败的单位损失由Disband改成明确Destroy+Remove，以免误当主动解散。

主代理负责所有取得调用者、PVE直接Dismiss、指挥旗后来附着互斥、真实禁单位地点、界面、Console与场景。该后台不是地下城或完整忠诚场景完成证明。

## 验证计划

新增PveLoyaltyContractTests 13项：真实普通供给追加2/原精英未动/重复取得和下一轮不再加、缺供应拒绝、不足0或1、专属0费及真实贵部队减5付费、普通槽不自动占专属、免费显式选择与解散保护、真正损失后重新招募、旌旗互斥。暂未跑CLI，等待主代理安排串行窗口。

追加三条取得回归：正式HeroProgression公共忠诚实际实体与高级奖励/真实单位供应，头环实际焚毁取得和永久移除实体，缺供应时头环/公共技能/槽全部不变。孤立BattleLoadout没有真实部队供应，因此明确拒绝忠诚是保留限制；合法BeginHeroBuild在供应准备好后取得，不用pending字段假完成。

## 已有后台证据

`AutomationOutputs/RulesState/20260929_060639_659c113c/results.xml`：57/57通过，新增忠诚13，FreeRecruitStateTests/CommandBannerStateTests/HeroProgressionTests直接影响回归44。代码冻结；尚不以这些后台用例替代地点/场景验收。

Unity编译指出测试不得设置CommandBannerForRecruit内部setter（CLI与生产同程序集掩盖）。仅修互斥fixture：实际手牌原指挥旗普通出牌获得影响4，再经公开BannerActions.SelectCommandForRecruit选择，不放宽字段可见性。只补该例：`AutomationOutputs/RulesState/20260929_061050_fd4fd4d5/results.xml` 1/1通过。原初始影响从9调整为5以加入真实旗4，最终失败守恒仍严格9。其余历史通过复用，Unity由主代理重检。
