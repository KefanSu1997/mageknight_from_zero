using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        // Geographical availability, not a promise that fees/slots or this turn's action are available.
        private PlaceState CurrentRecruitSite => Player != null && Game.Map.Placed.ContainsKey(Player.Position)
            && Game.Map.Places.TryGetValue(Player.Position, out var site) ? site : null;
        private RecruitLocation SiteLocation(PlaceState site) => site?.Type switch
        {
            PlaceType.Village => RecruitLocation.Village,
            PlaceType.Keep when site.Conquered && site.OwnerId == Player.Id => RecruitLocation.Keep,
            PlaceType.MageTower when site.Conquered => RecruitLocation.MageTower,
            PlaceType.City when site.Conquered => RecruitLocation.City,
            PlaceType.Monastery when !site.MonasteryBurned => RecruitLocation.Monastery,
            _ => RecruitLocation.None
        };
        public RecruitLocation CurrentSiteRecruitLocation => SiteLocation(CurrentRecruitSite);
        public bool CanInteractAtCurrentSite => CurrentSiteRecruitLocation != RecruitLocation.None;
        public PlaceState CurrentInteractionSite => CurrentRecruitSite;
        public bool CanRecruitSiteIcon(UnitCard card)
        {
            var site = CurrentRecruitSite;
            var location = SiteLocation(site);
            return card != null && location != RecruitLocation.None
                && (site.Type == PlaceType.City && site.CityColor == CityColor.White
                    || (card.RecruitLocationMask & location) != 0);
        }
        public void BeginSiteInteraction()
        {
            RequireOutsideBattle();
            RepeatedEffectResolution.RequireCompleted(Context);
            var site = CurrentRecruitSite;
            var location = SiteLocation(site);
            if (location == RecruitLocation.None) throw new InvalidOperationException("当前位置没有已接入的合法交涉地点");
            RecruitmentService.BeginInteraction(Player, Context, location);
            Context.InteractionSite = site;
            Context.InteractionPosition = Player.Position;
            // Shields are not a reputation-track modifier: Heroes/Thugs must not multiply/reverse them.
            Context.InteractionCityInfluence = site.Type == PlaceType.City ? site.CityShields.Count(id => id == Player.Id) : 0;
            Context.InfluencePool += Context.InteractionCityInfluence;
        }
        private void RequireSiteInteraction()
        {
            RequireOutsideBattle();
            RepeatedEffectResolution.RequireCompleted(Context);
            if (!Context.InNegotiation || Context.InteractionClosed || !RecruitmentService.BoundSiteIsCurrent(Player, Context)
                || SiteLocation(CurrentRecruitSite) != Context.InteractionLocation || Context.InteractionLocation == RecruitLocation.None)
                throw new InvalidOperationException("请在开始本次交涉的同一实际地点完成操作");
        }
        public void RecruitSiteUnit(UnitCard card, ManaColor? familiarMana = null)
        {
            RequireSiteInteraction();
            var offer = Game.Globals.Units.Offer;
            int index = offer.FindIndex(candidate => ReferenceEquals(candidate, card));
            if (index < 0 || !CardUnitActions.TryRecruit(Player, Context, offer, index, Context.InteractionLocation, familiarMana))
                throw new InvalidOperationException("招募失败：请核对真实供应、地点图标、印刷费用、额外魔力与合法指挥槽");
        }
        public void SelectLoyaltyRecruit(bool selected)
        {
            RequireSiteInteraction();
            LoyaltyActions.SelectRecruit(Player,Context,selected);
        }
        public bool CanBuySpellAtCurrentSite
        {
            get
            {
                var site = CurrentRecruitSite;
                return site != null && site.Conquered && (site.Type == PlaceType.MageTower
                    || site.Type == PlaceType.City && site.CityColor == CityColor.Blue);
            }
        }
        public DeedCard BuySiteSpell(SpellCard offered)
        {
            RequireSiteInteraction();
            if (!CanBuySpellAtCurrentSite) throw new InvalidOperationException("只有已征服法师塔或蓝色城市可购买法术");
            var supply = Game.Globals.Spells;
            int index = supply.Offer.FindIndex(card => ReferenceEquals(card, offered));
            if (index < 0 || !ReferenceEquals(Context.SpellSupply, supply))
                throw new InvalidOperationException("请选择当前旅程实际法术供应中的原牌");
            var original = (_spells ?? CardJsonLoader.LoadSpells().ToArray()).SingleOrDefault(card => card.Id == offered.Id);
            if (original == null || original.ManaColor < ManaColor.Red || original.ManaColor > ManaColor.White)
                throw new InvalidOperationException("法术缺少原定义或合法的印刷基本颜色");
            if (Context.InfluencePool < 7) throw new InvalidOperationException("购买法术需要7点影响力和1枚同色魔力");
            if (!Player.Mana.TryPayExactColors(new[] { original.ManaColor }, Context, Player))
                throw new InvalidOperationException("购买法术缺少1枚与原法术同色的魔力");
            Context.InfluencePool -= 7;
            var claimed = supply.Claim(index); // The same supply replenishes immediately, preserving unchosen cards.
            var acquired = new DeedCard(claimed.Id, CardType.Spell);
            Player.Deck.PutOnTop(acquired);
            return acquired;
        }
        public void EndSiteInteraction()
        {
            RequireSiteInteraction();
            RecruitmentService.EndInteraction(Context);
        }
        /// <summary>MKUE rulebook, interacting with locals: green city, six influence.
        /// Blind draw never exposes the hidden card as an input choice.</summary>
        public DeedCard BuyGreenCityAdvanced(AdvActionCard offered=null,bool blind=false)
        {
            RequireSiteInteraction();
            var site=CurrentRecruitSite;
            if(site==null||!site.Conquered||site.Type!=PlaceType.City||site.CityColor!=CityColor.Green)
                throw new InvalidOperationException("Advanced purchase requires a conquered green city.");
            var supply=Game.Globals.AdvActions;
            if(!ReferenceEquals(Context.AdvActionSupply,supply)||blind&&offered!=null)
                throw new InvalidOperationException("Advanced purchase must use the actual bound supply.");
            int index=blind?-1:supply.Offer.FindIndex(c=>ReferenceEquals(c,offered));
            if(!blind&&index<0)throw new InvalidOperationException("Select an actual advanced action offer.");
            if(Context.InfluencePool<6)throw new InvalidOperationException("Advanced purchase requires six influence.");
            AdvActionCard claimed;
            if(blind)
            {
                if(!supply.TryDraw(out claimed))throw new InvalidOperationException("Advanced deck is empty.");
            }
            else claimed=supply.Take(index);
            Context.InfluencePool-=6;
            var acquired=new DeedCard(claimed.Id,CardType.Action);
            Player.Deck.PutOnTop(acquired);
            return acquired;
        }
        public void DismissSiteUnit(UnitState unit)
        {
            RequireSiteInteraction();
            if (unit == null || unit.IsDestroyed || !Player.Units.Any(u => ReferenceEquals(u, unit)))
                throw new InvalidOperationException("只能解散当前自有的实际部队");
            Player.Disband(unit);
        }
        public void BuyMonasteryHealing()
        {
            RequireSiteInteraction();
            if(CurrentRecruitSite.Type!=PlaceType.Monastery||CurrentRecruitSite.MonasteryBurned||Context.InfluencePool<2)
                throw new InvalidOperationException("An intact monastery sells one healing point for two influence.");
            Context.InfluencePool-=2;Context.Healing.Add(1);
        }
        public void LearnMonasteryAdvanced(AdvActionCard card)
        {
            RequireSiteInteraction();
            var offer=Game.Globals.MonasteryOffer;
            int slot=offer.FindIndex(c=>ReferenceEquals(c,card));
            if(CurrentRecruitSite.Type!=PlaceType.Monastery||CurrentRecruitSite.MonasteryBurned||slot<0||Context.InfluencePool<6)
                throw new InvalidOperationException("Choose an actual monastery offer card and pay six influence at an intact monastery.");
            Context.InfluencePool-=6;offer.RemoveAt(slot);
            Player.Deck.PutOnTop(new DeedCard(card.Id,CardType.Action));
        }
        public void ExpandWhiteCityUnitOffer()
        {
            RequireSiteInteraction();
            var site=CurrentRecruitSite;
            if(site==null||!site.Conquered||site.Type!=PlaceType.City||site.CityColor!=CityColor.White)
                throw new InvalidOperationException("Elite offer expansion requires a conquered white city.");
            if(Context.InfluencePool<2)throw new InvalidOperationException("Elite offer expansion requires two influence.");
            if(!Game.Globals.Units.TryAppendEliteToOffer())
                throw new InvalidOperationException("Elite unit deck is empty.");
            Context.InfluencePool-=2;
        }
    }
}
