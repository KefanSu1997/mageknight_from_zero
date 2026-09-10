using System;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Adventure
{
    /// <summary>
    /// 把本场景的阶段/目标转换成既有 ActionSystem 的选择参数。
    /// 这里只适配已接通的资源池型基础牌，绝不为其他卡牌猜测效果。
    /// </summary>
    public static class OfficialActionAdapter
    {
        private static readonly ActionSystem Effects = new(loadSpellMetadata: false);

        public static bool Supports(CardSpec card) => card.Id is
            "basic_card_000" or "basic_card_014" or "basic_card_008" or "basic_card_016" or "basic_card_021";

        public static CardAction Action(AdventurePhase phase) => phase switch
        {
            AdventurePhase.Travel => CardAction.Movement,
            AdventurePhase.Interaction => CardAction.Influence,
            AdventurePhase.Block => CardAction.Block,
            _ => CardAction.Attack
        };

        public static int Preview(CardSpec card, AdventurePhase phase, bool enhanced, bool sideways = false)
        {
            if (card.Source == null || phase == AdventurePhase.Result) return 0;
            var player = new PlayerState();
            foreach (var color in card.Colors) player.Mana.AddCrystal(color, 1);
            return Apply(card, player, phase, enhanced, sideways);
        }

        public static int Apply(CardSpec card, PlayerState player, AdventurePhase phase, bool enhanced, bool sideways)
        {
            if (!Supports(card)) throw new InvalidOperationException("该原卡尚未接入本场景的目标流程：" + card.Id);
            var context = new ActionContext { InBattle = phase is AdventurePhase.Block or AdventurePhase.Attack,
                InNegotiation = phase == AdventurePhase.Interaction };
            if (sideways)
            {
                // 冒险持有实例区域；临时牌区仅供原横置规则计算，真实实例由 Session 移入已打出区。
                var temporary = new PlayerState();
                var deed = new DeedCard(card.Id, CardType.Action);
                temporary.Deck.Hand.Add(deed);
                var type = phase switch
                {
                    AdventurePhase.Travel => SidewaysType.Move,
                    AdventurePhase.Interaction => SidewaysType.Influence,
                    AdventurePhase.Block => SidewaysType.Block,
                    _ => SidewaysType.Attack
                };
                Effects.PlaySideways(deed, temporary, context, type);
            }
            else
            {
                // 决心/狂怒基础有二选一：格挡阶段选择格挡，攻击阶段选择攻击。
                Effects.Play(card.Source, player, context, enhanced, phase == AdventurePhase.Block ? 1 : 0);
            }
            return phase switch
            {
                AdventurePhase.Travel => context.MovementPool,
                AdventurePhase.Interaction => context.InfluencePool,
                AdventurePhase.Block => context.BlockPool,
                _ => context.MeleePool
            };
        }
    }
}
