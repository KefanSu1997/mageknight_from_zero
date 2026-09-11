using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 獻祭 / 犧牲：轉化手牌為魔晶，或以魔晶成對換取攻擊。
    /// 强化option低/高8位分别为绿或白、红或蓝；基础每两位按弃牌顺序指定一件神器的魔晶颜色。
    /// </summary>
    public sealed class SacrificeEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public SacrificeEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (_enh)
            {
                int first = option & 255, second = (option >> 8) & 255;
                if (option < 0 || option > 65535
                    || (first != (int)ManaColor.Green && first != (int)ManaColor.White)
                    || (second != (int)ManaColor.Red && second != (int)ManaColor.Blue))
                    throw new System.InvalidOperationException("牺牲必须选择绿或白，再选择红或蓝");
                return;
            }
            if (ctx.CardsToDiscard.Count > 3)
                throw new System.InvalidOperationException("献祭最多弃掉三张非创伤手牌");
            for (int i = 0; i < ctx.CardsToDiscard.Count; i++)
            {
                var card = ctx.CardsToDiscard[i];
                if (card == null || !player.Deck.Hand.Any(c => ReferenceEquals(c, card)))
                    throw new System.InvalidOperationException("献祭必须选择实际持有的手牌实例");
                if (ctx.CardsToDiscard.Take(i).Any(c => ReferenceEquals(c, card)))
                    throw new System.InvalidOperationException("献祭不能重复选择同一张手牌");
                if (card.Type != CardType.Action && card.Type != CardType.Spell && card.Type != CardType.Artifact)
                    throw new System.InvalidOperationException("献祭不能弃掉创伤或非契约卡牌");
                if (card.Type != CardType.Artifact && !CardColorHelper.Get(card.Id).HasValue)
                    throw new System.InvalidOperationException("献祭所选原卡缺少颜色定义");
            }
            int artifactCount = ctx.CardsToDiscard.Count(c => c.Type == CardType.Artifact);
            if (option < 0 || option >= (1 << (2 * artifactCount)))
                throw new System.InvalidOperationException("献祭神器魔晶颜色选择无效");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                player.Mana.AddCrystal(ManaColor.Red, 1);
                int artifactIndex = 0;
                foreach (var card in ctx.CardsToDiscard)
                {
                    var color = card.Type == CardType.Artifact
                        ? (ManaColor)((option >> (2 * artifactIndex++)) & 3)
                        : CardColorHelper.Get(card.Id).Value;
                    player.Deck.TryDiscardExact(card);
                    player.Mana.AddCrystal(color);
                }
                ctx.CardsToDiscard.Clear();
            }
            else
            {
                ManaColor c1 = (ManaColor)(option & 0xFF);
                ManaColor c2 = (ManaColor)((option >> 8) & 0xFF);
                player.Mana.Crystals.TryGetValue(c1, out int n1);
                player.Mana.Crystals.TryGetValue(c2, out int n2);
                int pair = System.Math.Min(n1, n2);
                if (pair == 0) return;
                ctx.CombatPower.AddAttack((c1 == ManaColor.Green ? 4 : 6) * pair,
                    c2 == ManaColor.Red ? Element.Fire : Element.Ice,
                    c1 == ManaColor.Green ? AttackType.Siege : AttackType.Ranged);
                player.Mana.Crystals[c1] = n1 - pair;
                if (player.Mana.Crystals[c1] == 0) player.Mana.Crystals.Remove(c1);
                player.Mana.Crystals[c2] = n2 - pair;
                if (player.Mana.Crystals[c2] == 0) player.Mana.Crystals.Remove(c2);
                player.Mana.AddToken(c1, pair);
                player.Mana.AddToken(c2, pair);
            }
        }
    }
}
