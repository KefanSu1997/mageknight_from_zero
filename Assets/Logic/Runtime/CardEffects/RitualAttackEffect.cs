using MK.Logic.Core;
using MK.Logic.Data;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 儀式攻擊：去除一張牌依顏色獲得不同攻擊。
    /// 顏色取自實際被去除的原行動卡，不能由 option 偽造。
    /// </summary>
    public sealed class RitualAttackEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public RitualAttackEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            var card = ctx.CardToRemove;
            if (card == null || !player.Deck.Hand.Exists(c => ReferenceEquals(c, card)))
                throw new System.InvalidOperationException("仪式攻击必须选择实际持有的行动卡实例");
            if (card.Type != CardType.Action || !CardColorHelper.Get(card.Id).HasValue)
                throw new System.InvalidOperationException("仪式攻击只能去除有颜色定义的行动卡");
            if (!CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Any(c => c.Id == card.Id))
                throw new System.InvalidOperationException("仪式攻击材料必须是原版行动牌");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var card = ctx.CardToRemove;
            var color = CardColorHelper.Get(card.Id).Value;
            // 按引用进入永久移除区，不能在删手牌时丢失实体或去掉同ID的另一张牌。
            if (!player.Deck.TryRemovePermanentlyExact(card))
                throw new System.InvalidOperationException("仪式攻击去除材料时丢失实际手牌实例");
            ctx.CardToRemove = null;
            switch (color)
            {
                case ManaColor.Red:
                    ctx.CombatPower.AddAttack(_enh ? 6 : 5, _enh ? Element.Fire : Element.Physical);
                    break;
                case ManaColor.Blue:
                    ctx.CombatPower.AddAttack(_enh ? 4 : 3, _enh ? Element.ColdFire : Element.Ice);
                    break;
                case ManaColor.White:
                    ctx.CombatPower.AddAttack(_enh ? 4 : 3, _enh ? Element.Fire : Element.Physical, AttackType.Ranged);
                    break;
                case ManaColor.Green:
                    ctx.CombatPower.AddAttack(_enh ? 3 : 2, _enh ? Element.Fire : Element.Physical, AttackType.Siege);
                    break;
            }
        }
    }
}
