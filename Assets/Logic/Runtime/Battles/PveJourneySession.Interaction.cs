using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public bool AtVillage => Game.Map.Placed.ContainsKey(Player.Position)
            && Game.Map.Places.TryGetValue(Player.Position,out var site) && site.Type==PlaceType.Village;
        public bool AtMagicalGlade => Game.Map.Placed.ContainsKey(Player.Position)
            && Game.Map.Places.TryGetValue(Player.Position,out var site) && site.Type==PlaceType.MagicalGlade;

        public void RecruitGladeUnit(UnitCard card, ManaColor color)
        {
            RequireOutsideBattle();
            RepeatedEffectResolution.RequireCompleted(Context);
            if (!AtMagicalGlade) throw new InvalidOperationException("请先实际移动到魔法林地");
            var offer = Game.Globals.Units.Offer;
            int index = offer.FindIndex(candidate => ReferenceEquals(candidate, card));
            if (index < 0 || !CardUnitActions.TryRecruit(Player,Context,offer,index,RecruitLocation.MagicalGlade,familiarMana:color))
                throw new InvalidOperationException("林地招募失败：请核对实际魔宠供应、基本色魔力、影响力、指挥槽及本回合行动");
        }

        public void BeginVillageInteraction()
        {
            RequireOutsideBattle();
            if(!AtVillage)throw new InvalidOperationException("请先实际移动到已揭示村庄");
            BeginSiteInteraction();
        }
        private void RequireVillageInteraction()
        {
            RequireSiteInteraction();
            if(!AtVillage || !Context.InNegotiation || Context.InteractionClosed || Context.InteractionLocation!=RecruitLocation.Village)
                throw new InvalidOperationException("请先在当前村庄开始本回合交涉");
        }
        public void RecruitVillageUnit(UnitCard card)
        {
            RequireVillageInteraction();
            RecruitSiteUnit(card);
        }
        public void BuyVillageHealing(int points=1)
        {
            RequireVillageInteraction();
            if(points<=0 || points>Context.InfluencePool/3)
                throw new InvalidOperationException("村庄每点治疗需要支付3影响力");
            Context.InfluencePool-=points*3;Context.Healing.Add(points);
        }
        public void EndVillageInteraction()
        {
            RequireVillageInteraction();EndSiteInteraction();
        }
        public void ReadyWithInfluence(UnitState unit, bool peacefulMoment = false)
        {
            RequireOutsideBattle();
            if(!CardUnitActions.TryReady(Player,Context,unit,peacefulMoment))
                throw new InvalidOperationException("需要原卡重整许可、足够影响力，以及符合等级的自有耗竭部队");
        }
        public void ConvertPeacefulHealing(int points = 1)
        {
            RequireOutsideBattle();
            PeacefulMomentActions.ConvertHealing(Player,Context,points);
        }
        public void SidewaysInfluence(DeedCard card)
        {
            RequireOutsideBattle();
            if(!_actions.PlaySideways(card,Player,Context,SidewaysType.Influence))
                throw new InvalidOperationException("只能横置实际非创伤手牌获得影响力");
        }
        public void DismissUnit(UnitState unit)
        {
            RequireVillageInteraction();
            if(unit==null || unit.IsDestroyed || !Player.Units.Any(u=>ReferenceEquals(u,unit)))
                throw new InvalidOperationException("只能解散当前自有的实际部队");
            Player.Disband(unit);
        }
    }
}
