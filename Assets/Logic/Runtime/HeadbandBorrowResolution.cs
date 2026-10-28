using System;
using System.Linq;
using System.Collections.Generic;
using AxialCoord = MK.Logic.Runtime.Map.AxialCoord;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.CardEffects;

namespace MK.Logic.Runtime
{
    /// <summary>A physical headband authorizes independent uses of one actual public skill, without acquiring it.</summary>
    public sealed class HeadbandBorrowResolution
    {
        private readonly PlayerState _player;
        private readonly ActionContext _context;
        public DeedCard PhysicalCard { get; }
        public SkillCard Skill { get; }
        public int Remaining { get; private set; }
        public int Completed { get; private set; }
        internal bool IsExecuting { get; private set; }
        public static bool Handles(SkillCard skill) => skill?.SourceId == "hero_skill_pool_004:04" || IsSideways(skill) || IsWound(skill) || IsUnit(skill) || IsPain(skill) || IsLeadership(skill) || IsShapeshift(skill) || IsForkedLightning(skill) || IsFlight(skill) || IsSocial(skill) || IsDuel(skill) || IsFlip(skill) || IsPuppet(skill);
        public static bool IsFlip(SkillCard skill) => skill?.SourceId == "hero_skill_pool_003:02"
            || skill?.SourceId == "hero_skill_pool_003:03" || skill?.SourceId == HeroSocialSkillRules.Disguise;

        // A flip is a single borrowed effect, not acquisition of a personal face-down marker.
        // Resolve the complete printed headband use atomically; never mix a prior normal use with a flip.
        public static void UseFlipSkill(PlayerState player, ActionContext context, DeedCard card, SkillCard skill, int option)
        {
            if (!IsFlip(skill) || context.PendingHeadbandBorrow != null || option == 100)
                throw new InvalidOperationException("请选择新的头环翻面技能借用；恢复个人标记不属于借用");
            Use(player, context, card, skill, pending =>
            {
                pending.RequireAuthorization(player, context);
                if (skill.SourceId == "hero_skill_pool_003:02")
                {
                    BattleFrenzyEffect.Borrow(player, context, option);
                    if (option == 0) BattleFrenzyEffect.Borrow(player, context, option);
                }
                else if (skill.SourceId == "hero_skill_pool_003:03")
                    ShamanRitualEffect.Borrow(player, context, option);
                else
                {
                    HeroSocialSkillRules.BorrowFlip(player, context, pending, option);
                    if (option == 0) HeroSocialSkillRules.BorrowFlip(player, context, pending, option);
                }
            }, skill.SourceId == "hero_skill_pool_003:03" ? 1 : 2);
        }

        public static bool IsPuppet(SkillCard skill) => skill?.SourceId == PuppetMasterActions.SkillId;
        public static bool IsDuel(SkillCard skill) => skill?.SourceId == DuelActions.SourceId;
        public static bool IsPayment(SkillCard skill) => skill?.SourceId == "hero_skill_pool_000:06" || skill?.SourceId == "hero_skill_pool_000:07";
        public static bool IsSocial(SkillCard skill) => HeroSocialSkillRules.Handles(skill);
        public static bool IsShapeshift(SkillCard skill) => skill?.SourceId == "hero_skill_pool_001:06";
        public static bool IsForkedLightning(SkillCard skill) => skill?.SourceId == ForkedLightningActions.SourceId;
        public static bool IsFlight(SkillCard skill) => skill?.SourceId == "hero_skill_pool_005:06";
        public static bool IsLeadership(SkillCard skill) => skill?.SourceId == "hero_skill_pool_002:06";
        public static bool IsSideways(SkillCard skill) => skill?.SourceId == "hero_skill_pool_007:06"
            || skill?.SourceId == "hero_skill_pool_005:07" || skill?.SourceId == "hero_skill_pool_007:07";
        public static bool IsWound(SkillCard skill) => skill?.SourceId == "hero_skill_pool_001:08"
            || skill?.SourceId == "hero_skill_pool_003:04" || skill?.SourceId == "hero_skill_pool_007:05";
        public static bool IsPain(SkillCard skill) => skill?.SourceId == "hero_skill_pool_000:05";
        public static bool IsUnit(SkillCard skill) => skill?.SourceId == "hero_skill_pool_002:02";
        private HeadbandBorrowResolution(PlayerState player, ActionContext context, DeedCard card, SkillCard skill)
        { _player = player; _context = context; PhysicalCard = card; Skill = skill; Remaining = skill.Frequency == SkillFrequency.OncePerTurn ? 2 : 1; }

        internal void RequireAuthorization(PlayerState player, ActionContext context)
        {
            if (!IsExecuting || !ReferenceEquals(player, _player) || !ReferenceEquals(context, _context)
                || !ReferenceEquals(context.PendingHeadbandBorrow, this) || Remaining <= 0)
                throw new InvalidOperationException("缺少当前物理头环的借用授权");
        }

        private static HeadbandBorrowResolution Begin(PlayerState player, ActionContext context, DeedCard card, SkillCard skill)
        {
            var pending = context.PendingHeadbandBorrow;
            if (pending != null)
            {
                if (!ReferenceEquals(player, pending._player) || !ReferenceEquals(context, pending._context)
                    || !ReferenceEquals(card, pending.PhysicalCard) || !ReferenceEquals(skill, pending.Skill)
                    || !player.Deck.PlayedCards.Any(c => ReferenceEquals(c, card)))
                    throw new InvalidOperationException("请继续当前头环选定的公共技能");
                return pending;
            }
            RepeatedEffectResolution.RequireCompleted(context);
            if (card?.Id != "items_022" || card.Type != CardType.Artifact
                || !player.Deck.Hand.Any(c => ReferenceEquals(c, card)))
                throw new InvalidOperationException("请选择手中的真实启迪头环");
            if (skill == null || context.SkillSupply?.Offer.Any(s => ReferenceEquals(s, skill)) != true
                || skill.IsInteractive != false || skill.Frequency != (skill.SourceId == "hero_skill_pool_003:03" ? SkillFrequency.ManualFlip : IsUnit(skill) || IsFlight(skill) ? SkillFrequency.OncePerRound : SkillFrequency.OncePerTurn) || (!Handles(skill) && !IsPayment(skill)))
                throw new InvalidOperationException("请选择公共供应中已接入逐次借用的原非互动技能");
            if (context.ManaSkillPayment != null)
                throw new InvalidOperationException("头环内部付款尚不支持付款技能叠加");
            pending = new HeadbandBorrowResolution(player, context, card, skill);
            context.PendingHeadbandBorrow = pending;
            return pending;
        }

        private static void Use(PlayerState player, ActionContext context, DeedCard card, SkillCard skill, Action<HeadbandBorrowResolution> execute, int uses = 1)
        {
            var pending = Begin(player, context, card, skill);
            pending.IsExecuting = true;
            try
            {
                execute(pending);
                if (pending.Completed == 0 && !player.Deck.TryPutInPlay(card))
                    throw new InvalidOperationException("头环执行后丢失原手牌实例");
                pending.Completed += uses;
                pending.Remaining -= uses;
                if (pending.Remaining == 0) context.PendingHeadbandBorrow = null;
            }
            catch
            {
                if (pending.Completed == 0) context.PendingHeadbandBorrow = null;
                throw;
            }
            finally { pending.IsExecuting = false; }
        }

        // Payment uses commit at the real fee boundary, before the paid effect executes.
        // This internal callback is constructed only by typed original-card/unit adapters.
        internal static void ExecutePayment(PlayerState player, ActionContext context, DeedCard card,
            SkillCard skill, Action<HeadbandBorrowResolution> command)
        {
            if (!IsPayment(skill)) throw new InvalidOperationException("请选择原魔咒或两极分化");
            var pending = Begin(player, context, card, skill);
            pending.IsExecuting = true;
            try { command(pending); }
            finally
            {
                pending.IsExecuting = false;
                if (pending.Completed == 0) context.PendingHeadbandBorrow = null;
            }
        }
        internal void CommitPayment(PlayerState player, ActionContext context)
        {
            RequireAuthorization(player, context);
            if (!IsPayment(Skill)) throw new InvalidOperationException("当前借用不是付款技能");
            if (Completed == 0 && !player.Deck.TryPutInPlay(PhysicalCard))
                throw new InvalidOperationException("付款时头环已离开实际手牌");
            Completed++;
            Remaining--;
            if (Remaining == 0) context.PendingHeadbandBorrow = null;
        }

        public static void UseSocial(PlayerState player, ActionContext context, DeedCard card, SkillCard skill, int option)
        {
            if (!IsSocial(skill)) throw new InvalidOperationException("请选择原交涉技能");
            Use(player, context, card, skill, pending => HeroSocialSkillRules.Borrow(player, context, pending, option));
        }

        public static void UseSideways(PlayerState player, ActionContext context, DeedCard card, SkillCard skill,
            DeedCard material, SidewaysType type, ManaColor? color = null, bool forgoSourceDice = false)
        {
            if (!IsSideways(skill) || ReferenceEquals(card, material))
                throw new InvalidOperationException("请选择横置技能及头环以外的实际手牌");
            ValidateSidewaysType(context, type);
            Use(player, context, card, skill, pending => SkillActions.BorrowSideways(player, context, pending, material, type, color, forgoSourceDice, true));
        }

        private static void ValidateSidewaysType(ActionContext context, SidewaysType type)
        {
            if (context.InBattle)
            {
                if (context.CombatPhase != Phase.Block && context.CombatPhase != Phase.Melee
                    || type != (context.CombatPhase == Phase.Block ? SidewaysType.Block : SidewaysType.Attack))
                    throw new InvalidOperationException("头环横置须选择当前格挡或近战阶段的对应用途");
            }
            else if ((type != SidewaysType.Move && type != SidewaysType.Influence)
                || type == SidewaysType.Move && (context.TurnActionCommitted || context.RestedThisTurn))
                throw new InvalidOperationException("战斗外请选择合法移动或影响力，行动或休息后不能再产生移动");
        }

        public static void UseBullseye(PlayerState player, ActionContext context, DeedCard card, SkillCard skill, AttackProfile attack)
        {
            if (skill?.SourceId != "hero_skill_pool_004:04") throw new InvalidOperationException("请选择实际百步穿杨");
            Use(player, context, card, skill, pending => ContributionSkillActions.BorrowBullseye(player, context, pending, attack));
        }

        public static void UseLeadership(PlayerState player, ActionContext context, DeedCard card, SkillCard skill,
            UnitState unit, int abilityIndex, int option = 0, int target = -1, ManaColor[] manaColors = null,
            int boost = 0, int attackIndex = 0)
        {
            if (!IsLeadership(skill)) throw new InvalidOperationException("请选择实际公共领导技能");
            Use(player, context, card, skill, pending => LeadershipActions.Borrow(player, context, pending,
                unit, abilityIndex, option, target, manaColors, boost, attackIndex));
        }

        public static void UseShapeshift(PlayerState player, ActionContext context, DeedCard card, SkillCard skill,
            DeedCard input, bool enhanced, int option, FixedOutputKind target, DeedCard material = null, AxialCoord? hex = null, TerrainType? terrain = null)
        {
            if (!IsShapeshift(skill) || ReferenceEquals(card, input) || ReferenceEquals(card, material))
                throw new InvalidOperationException("请选择原变换身形及头环以外的实际手牌材料");
            Use(player, context, card, skill, pending => ShapeshiftActions.Borrow(player, context, pending,
                input, enhanced, option, target, material, hex, terrain));
        }

        public static void UseForkedLightning(PlayerState player, ActionContext context, DeedCard card, SkillCard skill,
            IReadOnlyList<Monster> targets)
        {
            if (!IsForkedLightning(skill)) throw new InvalidOperationException("请选择原叉形闪电及实际敌人");
            Use(player, context, card, skill, pending => ForkedLightningActions.Borrow(player, context, pending, targets));
        }

        public static void UsePuppet(PlayerState player, ActionContext context, DeedCard card, SkillCard skill,
            Monster enemy, int mode)
        {
            if (!IsPuppet(skill)) throw new InvalidOperationException("请选择原傀儡大师公共技能");
            Use(player, context, card, skill, pending => PuppetMasterActions.Borrow(player, context, pending, enemy, mode));
        }

        // A composite Duel resolves both uses against the same attack, before either can be consumed.
        public static void UseDuel(PlayerState player, ActionContext context, DeedCard card, SkillCard skill,
            Monster enemy, int attackIndex = 0)
        {
            if (!IsDuel(skill)) throw new InvalidOperationException("请选择原决斗公共技能");
            Use(player, context, card, skill, pending => DuelActions.Borrow(player, context, pending, enemy, attackIndex), 2);
        }

        public static void UseFlight(PlayerState player, ActionContext context, DeedCard card, SkillCard skill,
            AxialCoord target, IEnumerable<PlayerState> otherPlayers = null)
        {
            if (!IsFlight(skill)) throw new InvalidOperationException("请选择原飞行及实际落点");
            Use(player, context, card, skill, pending => FlightSkillActions.Borrow(player, context, pending, target, otherPlayers));
        }

        public static void UsePain(PlayerState player, ActionContext context, DeedCard card, SkillCard skill,
            DeedCard wound, SidewaysType type)
        {
            if (!IsPain(skill) || wound?.Type != CardType.Wound
                || !player.Deck.Hand.Any(c => ReferenceEquals(c, wound)))
                throw new InvalidOperationException("痛苦之力借用须选择实际手牌中的一张创伤");
            ValidateSidewaysType(context, type);
            Use(player, context, card, skill, pending =>
            {
                int ownPermission = context.SidewaysWoundValue;
                try
                {
                    skill.Effect.Execute(player, context, 0);
                    if (!new ActionSystem(false).PlaySideways(wound, player, context, type))
                        throw new InvalidOperationException("所选创伤在横置前已不在手牌");
                }
                finally { context.SidewaysWoundValue = ownPermission; }
            });
        }

        public static void UseWound(PlayerState player, ActionContext context, DeedCard card, SkillCard skill,
            DeedCard wound, ManaColor? color = null)
        {
            if (!IsWound(skill) || wound == null)
                throw new InvalidOperationException("请选择已接入的原伤牌技能及实际伤牌");
            bool fearless = skill.SourceId == "hero_skill_pool_007:05";
            if (fearless ? color.HasValue : !color.HasValue || !Enum.IsDefined(typeof(ManaColor), color.Value))
                throw new InvalidOperationException("再生须选择实际付款颜色，无惧痛苦不支付魔力");
            int option = fearless ? 0 : (int)color.Value;
            Use(player, context, card, skill, pending => OriginalWithInputs(player, context, skill, option, wound, null, true));
        }

        public static void UseUnit(PlayerState player, ActionContext context, DeedCard card, SkillCard skill,
            UnitState unit, bool heal)
        {
            if (!IsUnit(skill)) throw new InvalidOperationException("请选择实际公共重整旗鼓");
            Use(player, context, card, skill, pending => OriginalWithInputs(player, context, skill, heal ? 1 : 0, null, unit, true));
        }

        // The same original validator/effect serves owned skills and authorized borrowing. Input fields are never retained.
        private static void OriginalWithInputs(PlayerState player, ActionContext context, SkillCard skill,
            int option, DeedCard wound, UnitState unit, bool execute)
        {
            var oldWound = context.SelectedSkillWound;
            var oldUnit = context.TargetUnit;
            try
            {
                context.SelectedSkillWound = wound;
                context.TargetUnit = unit;
                if (!(skill.Effect is ICardEffectValidator validator))
                    throw new InvalidOperationException("该原技能没有可用的真实输入校验");
                validator.Validate(player, context, option);
                if (execute) skill.Effect.Execute(player, context, option);
            }
            finally { context.SelectedSkillWound = oldWound; context.TargetUnit = oldUnit; }
        }

        private bool OriginalInputAvailable(int option, DeedCard wound, UnitState unit)
        {
            try { OriginalWithInputs(_player, _context, Skill, option, wound, unit, false); return true; }
            catch (InvalidOperationException) { return false; }
        }

        /// <summary>Only impossible remaining uses may be skipped; a playable material or contribution prevents abandonment.</summary>
        public static void FinishIfImpossible(PlayerState player, ActionContext context)
        {
            var pending = context.PendingHeadbandBorrow ?? throw new InvalidOperationException("没有待完成的头环借用");
            if (!ReferenceEquals(pending._player, player) || !ReferenceEquals(pending._context, context))
                throw new InvalidOperationException("头环借用不属于当前玩家");
            if (pending.HasLegalInput()) throw new InvalidOperationException("仍有合法技能输入，请完成本次借用");
            context.PendingHeadbandBorrow = null;
        }

        public bool HasLegalInput()
        {
            if (!ReferenceEquals(_context.PendingHeadbandBorrow, this) || Remaining <= 0) return false;
            if (IsPayment(Skill))
            {
                // Absence proves no typed card/unit command can pay; presence does not prove
                // a legal fee, target or material. Include even unsupported ready units.
                bool cardConsumer = _player.Deck.Hand.Any(c => c.Type == CardType.Action || c.Type == CardType.Spell);
                bool unitConsumer = _player.Units.Any(u => !u.IsDestroyed && u.IsReady);
                if (!cardConsumer && !unitConsumer) return false;
                throw new InvalidOperationException("仍有行动/法术或就绪部队，尚不能自动确认其费用、目标和材料是否可用；请继续选择实际原费用");
            }
            if (IsPuppet(Skill)) return PuppetMasterActions.HasBorrowInput(_player, _context);
            if (IsSocial(Skill)) return HeroSocialSkillRules.HasBorrowInput(_context, Skill);
            if (IsForkedLightning(Skill)) return ForkedLightningActions.HasBorrowInput(_context);
            if (IsShapeshift(Skill))
            {
                IsExecuting = true;
                try { return ShapeshiftActions.HasBorrowInput(_player, _context, this); }
                finally { IsExecuting = false; }
            }
            if (IsLeadership(Skill))
            {
                IsExecuting = true;
                try { return LeadershipActions.HasBorrowInput(_player, _context, this); }
                finally { IsExecuting = false; }
            }
            if (Skill.SourceId == "hero_skill_pool_004:04")
                return _context.InBattle && (_context.CombatPhase == Phase.Ranged || _context.CombatPhase == Phase.Melee)
                    && _context.CombatPower.AvailableAttacks(_context.CombatPhase, _context.AllAttacksSiege)
                        .Any(a => a.SourceCard != null && a.SourceUnit == null);
            if (IsWound(Skill))
            {
                var colors = Enum.GetValues(typeof(ManaColor)).Cast<ManaColor>();
                foreach (var wound in _player.Deck.Hand.Where(c => c.Type == CardType.Wound))
                {
                    if (Skill.SourceId == "hero_skill_pool_007:05")
                    { if (OriginalInputAvailable(0, wound, null)) return true; }
                    else foreach (var color in colors)
                        if (OriginalInputAvailable((int)color, wound, null)) return true;
                }
                return false;
            }
            if (IsUnit(Skill))
                return _player.Units.Any(unit => OriginalInputAvailable(0, null, unit) || OriginalInputAvailable(1, null, unit));
            var type = !_context.InBattle ? SidewaysType.Influence
                : _context.CombatPhase == Phase.Block ? SidewaysType.Block : SidewaysType.Attack;
            try { ValidateSidewaysType(_context, type); }
            catch (InvalidOperationException) { return false; }
            if (IsPain(Skill))
                return _player.Deck.Hand.Any(c => c.Type == CardType.Wound);
            IsExecuting = true;
            try
            {
                foreach (var material in _player.Deck.Hand.Where(c => !ReferenceEquals(c, PhysicalCard)))
                {
                    ManaColor?[] colors = Skill.SourceId == "hero_skill_pool_005:07"
                        ? Enum.GetValues(typeof(ManaColor)).Cast<ManaColor>().Select(c => (ManaColor?)c).ToArray() : new ManaColor?[] { null };
                    foreach (var color in colors)
                    {
                        try
                        {
                            SkillActions.BorrowSideways(_player, _context, this, material, type, color, false, false);
                            return true;
                        }
                        catch (InvalidOperationException) { }
                    }
                }
                return false;
            }
            finally { IsExecuting = false; }
        }
    }
}
