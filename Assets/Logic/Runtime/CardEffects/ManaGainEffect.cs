using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 取得指定顏色法力標記或晶體的效果。
    /// </summary>
    public sealed class ManaGainEffect : ICardEffect
    {
        private readonly ManaColor[] _colors;
        private readonly bool _crystal;
        public ManaGainEffect(bool crystal, params ManaColor[] colors)
        {
            _colors = colors;
            _crystal = crystal;
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int idx = option;
            if (idx < 0 || idx >= _colors.Length) idx = 0;
            var color = _colors[idx];
            if (_crystal)
                player.Mana.AddCrystal(color, 1);
            else
                player.Mana.AddToken(color, 1);
        }
    }
}
