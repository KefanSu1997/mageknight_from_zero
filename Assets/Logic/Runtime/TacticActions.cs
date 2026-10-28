using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Original tactics operate on the same deck, player and source as normal turns.</summary>
    public static class TacticActions
    {
        internal static void Select(PlayerState player, TacticCard card)
        {
            player.Tactic = OriginalTactics.IsOriginal(card) ? new TacticState(card) : null;
            if (player.Tactic == null) return; // Existing initiative-only fixtures have no invented effects.
            if (card.Id == "day_5") player.Deck.DrawExact(2);
            if (card.Id == "night_5" && player.Deck.DrawPileCount == 0) player.Tactic.SelectionResolved = true;
        }
        private static TacticState Require(PlayerState player, string id, bool allowSpent = false)
        {
            var state = player.Tactic;
            if (state == null || state.Card.Id != id || state.Spent && !allowSpent)
                throw new InvalidOperationException("未选择此原战术，或本轮次数已用尽");
            return state;
        }
        private static void ValidateHand(PlayerState player, IReadOnlyList<DeedCard> cards, int maximum)
        {
            if (cards == null || cards.Count > maximum || cards.Select((c, i) =>
                c == null || !player.Deck.Hand.Any(h => ReferenceEquals(h,c))
                    || cards.Take(i).Any(other => ReferenceEquals(c,other))).Any(x => x))
                throw new InvalidOperationException("必须选择数量合法且互不重复的实际手牌（允许创伤）");
        }
        internal static void ResolveSelection(PlayerState player, ManaSource source, IReadOnlyList<DeedCard> cards, ManaDie die, Random random)
        {
            var state = player.Tactic;
            if (state == null || state.SelectionResolved) throw new InvalidOperationException("没有待处理的取得战术效果");
            cards ??= Array.Empty<DeedCard>();
            switch (state.Card.Id)
            {
                case "day_2":
                    if (die != null) throw new InvalidOperationException("三思后行不选择魔力骰");
                    ValidateHand(player,cards,3); player.Deck.Rethink(cards,random); break;
                case "day_3":
                    if (cards.Count != 0) throw new InvalidOperationException("魔力窃取不选择手牌");
                    source.ReserveForTactic(player,die); state.ReservedDie = die; break;
                case "night_5":
                    if (die != null || cards.Count != 1) throw new InvalidOperationException("万全准备必须选择一张实际牌库牌");
                    player.Deck.SearchDrawToHand(cards[0],random); break;
                default: throw new InvalidOperationException("此战术没有待选择的取得效果");
            }
            state.SelectionResolved = true;
        }
        internal static void MidnightMeditation(PlayerState player, IReadOnlyList<DeedCard> cards, Random random)
        {
            var state = Require(player,"night_4"); ValidateHand(player,cards,5);
            player.Deck.MidnightMeditation(cards,random); state.Spent = true;
        }
        internal static void SparingPower(PlayerState player, bool collect)
        {
            var state = Require(player,"night_6");
            if (collect) { player.Deck.CollectTacticAside(); state.Spent = true; }
            else player.Deck.StoreTopUnderTactic();
        }
        internal static void LongNight(PlayerState player, Random random)
        {
            var state = Require(player,"night_2");
            player.Deck.RefillLongNight(random); state.Spent = true;
        }
        internal static void RightMoment(PlayerState player, ActionContext context)
        {
            var state = Require(player,"day_6"); state.Spent = true; context.ExtraTurn = true;
        }
        internal static void UseReservedMana(PlayerState player, ManaSource source)
        {
            var state = Require(player,"day_3");
            if (!state.SelectionResolved) throw new InvalidOperationException("必须先选择保留的魔力骰");
            source.UseTacticReservation(player,state.ReservedDie);
            player.Mana.AddToken(state.ReservedDie.Face); state.Spent = true; state.ReservedDie = null;
        }
        internal static void ManaSearch(PlayerState player, ManaSource source, IReadOnlyList<ManaDie> dice)
        {
            var state = Require(player,"night_3");
            if (state.ManaSearchUsedThisTurn || source.WasUsedThisTurn(player))
                throw new InvalidOperationException("魔力搜寻每回合一次，且必须在取用源魔力之前");
            if (dice == null || dice.Count > 2 || dice.Distinct().Count() != dice.Count || dice.Any(d => d == null || !source.Dice.Contains(d)))
                throw new InvalidOperationException("请选择至多两颗互不重复的实际可用源骰");
            int gold = source.Dice.Count(d => d.Face == ManaColor.Gold);
            if (dice.Count(d => d.Face == ManaColor.Gold) < Math.Min(gold,dice.Count))
                throw new InvalidOperationException("魔力搜寻必须优先选择金色枯竭骰");
            foreach (var die in dice) die.RollUnrestricted();
            state.ManaSearchUsedThisTurn = true;
        }
        internal static int PlanningDrawBonus(PlayerState player) => player.Tactic?.Card.Id == "day_4" && player.Deck.Hand.Count >= 2 ? 1 : 0;
    }
}
