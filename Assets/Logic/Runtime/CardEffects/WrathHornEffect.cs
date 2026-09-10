using MK.Logic.Core;
using System;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 愤怒号角：攻城攻击并可能因骰色受创。
    /// option 表示強效額外攻擊力，0~5。
    /// </summary>
    public sealed class WrathHornEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _once;
        private readonly Random _rnd;
        public WrathHornEffect(bool once, Random? rnd = null)
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
            if (option < 0 || option > (_once ? 5 : 0)) throw new InvalidOperationException("愤怒号角额外攻击必须在允许范围内");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.LastManaRolls.Clear();
            ctx.CombatPower.AddAttack(5 + (_once ? option : 0), Element.Physical, AttackType.Siege);
            int rolls = _once ? option : 1;
            for (int i = 0; i < rolls; i++)
            {
                var die = new ManaDie(ctx.EffectRandom ?? _rnd);
                die.RollUnrestricted();
                ctx.LastManaRolls.Add(die.Face);
                if (die.Face is ManaColor.Red or ManaColor.Black) player.Wounds += 1;
            }
        }
    }
}
