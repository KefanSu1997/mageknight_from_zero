using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public sealed partial class SharedSkillArea
    {
        public const string SourceOpeningId="hero_skill_pool_005:09";
        private static ManaSource RequireSource(ActionContext context)
            => context.ManaSource ?? throw new InvalidOperationException("需要本游戏实际魔力源");
        public SharedSkillToken UseSourceOpening(PlayerState player,ActionContext context,SkillCard skill,ManaDie optionalReroll=null)
        {
            RequireUse(player,context,skill,SourceOpeningId);
            var source=RequireSource(context);
            if(optionalReroll!=null && !source.Dice.Any(d=>ReferenceEquals(d,optionalReroll)))
                throw new InvalidOperationException("只能重掷源中实际可用骰");
            if(optionalReroll!=null)optionalReroll.Roll(context.DayPart);
            return Publish(player,skill);
        }
        public void ClaimSourceOpening(PlayerState player,ActionContext context,SharedSkillToken token,ManaDie die)
        {
            RequireClaim(player,context,token,SourceOpeningId);
            var source=RequireSource(context);
            ManaSource.ValidateUse(player);
            if(die==null || !source.Dice.Any(d=>ReferenceEquals(d,die)) || !FamiliarActions.IsBasicColor(die.Face))
                throw new InvalidOperationException("必须选择源中实际可用的基本色骰");
            var color=die.Face;
            source.TakeSourceOpening(player,die);
            player.Mana.AddToken(color);
            token.Owner.Mana.AddCrystal(color);
            Return(token);
        }
        public IReadOnlyList<ManaDie> PendingSourceOpeningReturns(PlayerState player,ActionContext context)
        {
            RequireActual(player,context);return RequireSource(context).PendingSourceOpeningReturns(player);
        }
        public void ChooseSourceOpeningReturn(PlayerState player,ActionContext context,ManaDie die,bool reroll)
        {
            RequireActual(player,context);RequireSource(context).ChooseSourceOpeningReturn(player,die,reroll);
        }
    }
}
