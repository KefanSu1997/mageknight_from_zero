using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;

namespace MK.Logic.Runtime
{
    public sealed record ManaSkillPaymentSelection(SkillCard Skill, ManaColor RequiredColor,
        DeedCard Material = null, ManaColor? SourceColor = null, ManaDie SourceDie = null, bool ExtraDie = false);

    /// <summary>A skill replaces one item of this command's actual cost; it never creates stored mana.</summary>
    public static class ManaSkillPaymentActions
    {
        internal static IDisposable StrongSpellScope(ActionContext context, bool strong)
            => new PurposeScope(context, strong);

        private sealed class PurposeScope : IDisposable
        {
            private readonly ActionContext context;
            private readonly bool previous;
            internal PurposeScope(ActionContext context, bool strong)
            { this.context = context; previous = context.PayingStrongSpell; context.PayingStrongSpell = strong; }
            public void Dispose() => context.PayingStrongSpell = previous;
        }
        public static void Execute(PlayerState player, ActionContext context, ManaSkillPaymentSelection selection,
            Action formalPaidCommand)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (context.ManaSkillPayment != null) throw new InvalidOperationException("不能嵌套魔力技能付款");
            var payment = new ManaSkillPayment(player, selection);
            payment.Validate(context);
            context.ManaSkillPayment = payment;
            try { formalPaidCommand(); }
            finally { context.ManaSkillPayment = null; }
        }

        /// <summary>Borrow a payment skill only around this actual original card command, never an arbitrary callback.</summary>
        public static void ExecuteBorrowedCard(PlayerState player, ActionContext context, DeedCard physical,
            ManaSkillPaymentSelection selection, DeedCard played, ActionCardData original, bool enhanced = false, int option = 0, ActionSystem actionSystem = null)
        {
            if (played == null || played.Type != CardType.Action && played.Type != CardType.Spell || original == null)
                throw new InvalidOperationException("请选择实际原行动或法术；神器内部付款尚未接入头环");
            if (ReferenceEquals(physical, played)) throw new InvalidOperationException("头环不能同时充当付款消费者");
            ExecuteBorrowed(player, context, physical, selection,
                () => CardPlayActions.Play(player, context, played, original, enhanced, option, actionSystem));
        }

        /// <summary>Regular original unit abilities share a mandatory payment gate, including zero-cost rejection.</summary>
        public static void ExecuteBorrowedUnit(PlayerState player, ActionContext context, DeedCard physical,
            ManaSkillPaymentSelection selection, UnitState unit, int abilityIndex, int option = 0, int target = -1,
            UnitState unitTarget = null, ManaColor[] manaColors = null, int boost = 0, int attackIndex = 0, ScoutTarget scoutTarget = null)
        {
            if (unit == null || DelphanaMastersActions.IsMasters(unit))
                throw new InvalidOperationException("请选择使用统一费用入口的实际部队能力；大师的独立能力付款尚未接入头环");
            ExecuteBorrowed(player, context, physical, selection,
                () => CardUnitActions.Use(player, context, unit, abilityIndex, option, target, unitTarget, manaColors, boost, attackIndex, scoutTarget));
        }

        private static void ExecuteBorrowed(PlayerState player, ActionContext context, DeedCard physical,
            ManaSkillPaymentSelection selection, Action command)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (context.ManaSkillPayment != null) throw new InvalidOperationException("不能嵌套魔力技能付款");
            if (ReferenceEquals(selection.Material, physical) || ReferenceEquals(context.CardToDiscard, physical)
                || ReferenceEquals(context.CardToRemove, physical) || context.CardsToDiscard.Any(c => ReferenceEquals(c, physical))
                || context.OverloadSelections?.Any(s => s?.Uses(physical) == true) == true)
                throw new InvalidOperationException("正在借用的物理头环不能同时作为付款或效果材料");
            HeadbandBorrowResolution.ExecutePayment(player, context, physical, selection.Skill, borrow =>
            {
                var payment = new ManaSkillPayment(player, selection, borrow);
                payment.Validate(context);
                var previous = context.ManaSkillPayment;
                context.ManaSkillPayment = payment;
                try
                {
                    RepeatedEffectResolution.RequireCompleted(context);
                    command();
                    if (!payment.Consumed) throw new InvalidOperationException("此正式命令没有提交对应魔力费用");
                }
                finally { context.ManaSkillPayment = previous; }
            });
        }

        internal static void ValidateCardMaterial(ActionContext context, DeedCard played)
        {
            var material = context.ManaSkillPayment?.Selection.Material;
            if (material != null && (ReferenceEquals(material, played)
                || ReferenceEquals(material, context.CardToDiscard) || ReferenceEquals(material, context.CardToRemove)
                || context.CardsToDiscard.Any(c => ReferenceEquals(c, material))
                || context.OverloadSelections?.Any(s => s?.Uses(material) == true) == true))
                throw new InvalidOperationException("魔咒材料不能同时作为本体或另一项材料");
        }
    }

    internal sealed class ManaSkillPayment
    {
        internal readonly PlayerState Owner;
        internal readonly ManaSkillPaymentSelection Selection;
        internal bool Consumed;
        private readonly HeadbandBorrowResolution _borrow;
        internal ManaSkillPayment(PlayerState owner, ManaSkillPaymentSelection selection, HeadbandBorrowResolution borrow = null)
        { Owner = owner; Selection = selection ?? throw new ArgumentNullException(nameof(selection)); _borrow = borrow; }

        internal void Validate(ActionContext context)
        {
            var s = Selection;
            if (_borrow != null) _borrow.RequireAuthorization(Owner, context);
            bool authorized = _borrow != null ? ReferenceEquals(s.Skill, _borrow.Skill)
                : Owner.Skills.Any(k => ReferenceEquals(k, s.Skill)) && !SkillActions.IsUsed(Owner, s.Skill);
            if (s.Skill == null || !authorized
                || s.Skill.Frequency != SkillFrequency.OncePerTurn
                || (s.Skill.SourceId != "hero_skill_pool_000:06" && s.Skill.SourceId != "hero_skill_pool_000:07")
                || !Enum.IsDefined(typeof(ManaColor), s.RequiredColor))
                throw new InvalidOperationException("必须选择本回合未使用的自有魔咒或两极分化");
            if (s.Skill.SourceId == "hero_skill_pool_000:06")
            {
                if (s.Material == null || !Owner.Deck.Hand.Any(c => ReferenceEquals(c, s.Material))
                    || s.SourceColor != null || s.SourceDie != null || s.ExtraDie
                    || (s.Material.Type == CardType.Wound
                        ? s.RequiredColor != ManaColor.Red && s.RequiredColor != ManaColor.Black
                        : s.RequiredColor != ManaColor.White && s.RequiredColor != ManaColor.Green))
                    throw new InvalidOperationException("魔咒须用实际伤牌支付红/黑，或非伤牌支付白/绿");
                ManaSkillPaymentActions.ValidateCardMaterial(context, context.CurrentSourceCard as DeedCard);
                return;
            }
            if (s.Material != null || s.SourceColor == null || !Enum.IsDefined(typeof(ManaColor), s.SourceColor.Value))
                throw new InvalidOperationException("两极分化须指定实际源色");
            if (s.SourceDie != null)
            {
                ManaSource.ValidateUse(Owner);
                if (context.ManaSource == null || !context.ManaSource.Dice.Contains(s.SourceDie)
                    || s.SourceDie.Face != s.SourceColor || (s.ExtraDie ? context.ExtraManaDice <= 0 : Owner.HeldManaDie != null))
                    throw new InvalidOperationException("两极分化须选择有取用额度的实际源骰");
            }
            else if (s.ExtraDie) throw new InvalidOperationException("额外取骰须指定骰子");
        }

        internal bool ReplaceCost(List<ManaColor> costs, ActionContext context)
        {
            Validate(context);
            int index = costs.IndexOf(Selection.RequiredColor);
            if (index < 0 || context.InfiniteMana.Contains(Selection.RequiredColor)) return false;
            if (Selection.Skill.SourceId == "hero_skill_pool_000:06") { costs.RemoveAt(index); return true; }
            var from = Selection.SourceColor.Value;
            var to = Selection.RequiredColor;
            bool basicPair = from == ManaColor.Red && to == ManaColor.Green || from == ManaColor.Green && to == ManaColor.Red
                || from == ManaColor.Blue && to == ManaColor.White || from == ManaColor.White && to == ManaColor.Blue;
            bool dayBlack = context.DayPart == DayPart.Day && from == ManaColor.Black && to >= ManaColor.Red && to <= ManaColor.White;
            bool nightGold = context.DayPart == DayPart.Night && from == ManaColor.Gold && to == ManaColor.Black && context.PayingStrongSpell;
            if (!basicPair && !dayBlack && !nightGold) return false;
            if (Selection.SourceDie != null) costs.RemoveAt(index); else costs[index] = from;
            return true;
        }

        internal void Commit(ActionContext context)
        {
            if (Consumed) throw new InvalidOperationException("同一笔技能付款不能重复提交");
            var s = Selection;
            if (s.Material != null && !Owner.Deck.TryDiscardExact(s.Material))
                throw new InvalidOperationException("魔咒材料在付款前离开手牌");
            if (s.SourceDie != null)
            {
                if (s.ExtraDie)
                {
                    context.ManaSource.TakeExtra(Owner, s.SourceDie);
                    context.ExtraManaDice--;
                    if (context.ExtraBlackManaDice > 0) context.ExtraBlackManaDice--;
                }
                else context.ManaSource.TakeOrdinary(Owner, s.SourceDie);
            }
            Consumed = true;
            if (_borrow == null) Owner.SkillsUsedThisTurn.Add(s.Skill.SourceId);
            else _borrow.CommitPayment(Owner, context);
        }
    }
}
