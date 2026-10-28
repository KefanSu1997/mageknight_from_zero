using MK.Logic.Core;
using System;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 无尽宝石袋：掷骰获取魔晶或名望，强效提供多色法力。
    /// option 低8位及次8位分別指定第一次與第二次擲到金色時選擇的顏色索引。
    /// </summary>
    public sealed class GemBagEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _once;
        private readonly Random _rnd;
        public GemBagEffect(bool once, Random? rnd = null)
        {
            _once = once;
#if NETSTANDARD2_1
            _rnd = rnd ?? new Random();
#else
            _rnd = rnd ?? Random.Shared;
#endif
        }

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || (option >> 16) != 0 || (option & 0xFF) > 3 || ((option >> 8) & 0xFF) > 3
                || (_once && option != 0))
                throw new InvalidOperationException("无尽宝石袋必须为每次金色骰选择一种基本色");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.LastManaRolls.Clear();
            if (!_once)
            {
                for (int i = 0; i < 2; i++)
                {
                    var die = new ManaDie(ctx.EffectRandom ?? _rnd);
                    die.RollUnrestricted();
                    var color = die.Face;
                    ctx.LastManaRolls.Add(color);
                    if (color == ManaColor.Black)
                    {
                        player.Fame += 1;
                    }
                    else
                    {
                        if (color == ManaColor.Gold)
                        {
                            int idx = (option >> (8 * i)) & 0xFF;
                            color = (ManaColor)idx;
                        }
                        player.Mana.AddCrystal(color, 1);
                    }
                }
            }
            else
            {
                foreach (var c in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White })
                    player.Mana.AddToken(c, 1);
                if (ctx.DayPart == DayPart.Day)
                    player.Mana.AddToken(ManaColor.Gold, 1);
                else
                    player.Mana.AddToken(ManaColor.Black, 1);
            }
        }
    }
}
