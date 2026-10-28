using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public void ConvertGold(ManaColor color)
        {
            if(!Game.HasActiveTurn || Game.IsEndingTurn || Context==null)
                throw new InvalidOperationException("请在自己的实际回合选择金色魔力的用途");
            ManaTokenActions.ConvertGold(Player,Context,color);
        }
        public bool CanPlayOutsideArtifact(DeedCard card, bool burn) => card?.Type==CardType.Artifact
            && _artifacts.Any(a=>a.Id==card.Id) && PveArtifactInputs.OutsideAllowed(card.Id,burn);

        public void PlayOutsideArtifact(DeedCard card, bool burn, UnitState unit=null, params ManaColor[] colors)
        {
            RequireOutsideBattle();
            if(!CanPlayOutsideArtifact(card,burn)) throw new InvalidOperationException("此神器选择需要在对应战斗阶段使用");
            bool banner=PveArtifactInputs.IsAttachedBanner(card.Id) && !burn;
            if(banner && (unit==null || !Player.Units.Contains(unit) || unit.IsDestroyed))
                throw new InvalidOperationException("请选择实际自有部队附着旌旗");
            if(!banner && unit!=null) throw new InvalidOperationException("此效果不选择附着部队");
            int option=PveArtifactInputs.IsResource(card.Id) ? PveArtifactInputs.ColorOption(card.Id,burn,colors)
                : colors!=null && colors.Length==0 ? 0 : throw new InvalidOperationException("此效果不选择魔力颜色");
            var previous=Context.TargetUnit;
            try
            {
                Context.TargetUnit=unit;
                CardPlayActions.PlayArtifact(Player,Context,card,_artifacts.Single(a=>a.Id==card.Id),burn,option);
            }
            finally { Context.TargetUnit=previous; }
        }
    }
}
