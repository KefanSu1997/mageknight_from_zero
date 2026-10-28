using System;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 活力煥發：四擇一效果。
    /// </summary>
    public sealed class RefreshingEffect : ICardEffect, ICardEffectValidator
    {
        private readonly int _heal;
        private readonly int _draw;
        private readonly bool _crystal;
        private readonly int _maxLevel;
        public RefreshingEffect(int heal, int draw, bool crystal, int maxLevel)
        {
            _heal = heal;
            _draw = draw;
            _crystal = crystal;
            _maxLevel = maxLevel;
        }

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || option > 3)
                throw new InvalidOperationException("活力焕发必须选择治疗、抽牌、绿色资源或重整");
            if (ctx.InBattle)
                throw new InvalidOperationException("战斗中不能使用活力焕发的任何效果");
            if (option == 3 && (ctx.TargetUnit == null || !player.Units.Contains(ctx.TargetUnit)
                || ctx.TargetUnit.IsDestroyed || ctx.TargetUnit.Card.Level < 1 || ctx.TargetUnit.Card.Level > _maxLevel))
                throw new InvalidOperationException("活力焕发必须选择等级范围内未销毁的自有部队");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            switch (option)
            {
                case 0:
                    ctx.Healing.Add(_heal);
                    break;
                case 1:
                    player.Deck.DrawExact(_draw);
                    break;
                case 2:
                    if (_crystal)
                        player.Mana.AddCrystal(ManaColor.Green, 1);
                    else
                        player.Mana.AddToken(ManaColor.Green, 1);
                    break;
                case 3:
                    ctx.TargetUnit!.Ready();
                    break;
            }
        }
    }
}
