using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 活力煥發：四擇一效果。
    /// </summary>
    public sealed class RefreshingEffect : ICardEffect
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

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            switch (option)
            {
                case 0:
                    player.Wounds = System.Math.Max(0, player.Wounds - _heal);
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
                    if (ctx.TargetUnit != null && ctx.TargetUnit.Card.Level <= _maxLevel)
                        ctx.TargetUnit.Ready();
                    break;
            }
        }
    }
}
