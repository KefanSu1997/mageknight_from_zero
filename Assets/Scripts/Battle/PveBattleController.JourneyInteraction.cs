using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _journeyInteraction;
        private bool _siteSpellShop;
        private SpellCard _shopSpell;
        private UnitCard _offerUnit;
        private UnitState _dismissUnit;
        private int _unitOfferPage;
        private ManaColor _recruitMana=ManaColor.Red;
        private UnitCard[] CreateJourneyUnitDeck(PlayerState player)
        {
            // The custom demo uses the authored catalog order; standard hero starts shuffle.
            // Each binding is one physical copy in this scenario's supply.
            var available=unitCatalog.Select(u=>u.Snapshot()).ToList();
            foreach(var unit in player.Units)
            {
                int index=available.FindIndex(c=>c.Id==unit.Card.Id);
                if(index>=0)available.RemoveAt(index);
            }
            var deck=available.ToArray();
            if(_originalStartHero!=null)
            {
                var random=new System.Random();
                for(int i=deck.Length-1;i>0;i--){int j=random.Next(i+1);(deck[i],deck[j])=(deck[j],deck[i]);}
            }
            return deck;
        }
        private static string RecruitmentSiteName(RecruitLocation location) => location switch
        {
            RecruitLocation.Village => "村庄", RecruitLocation.Keep => "自有要塞",
            RecruitLocation.MageTower => "法师塔", RecruitLocation.City => "城市",
            RecruitLocation.MagicalGlade => "魔法林地", _ => "当前无可交涉地点"
        };
        private void DrawJourneyInteraction()
        {
            if(_siteSpellShop){DrawSiteSpellShop();return;}
            var p=_journey.Player;var ctx=_journey.Context;var offer=_journey.Game.Globals.Units.Offer;
            bool glade=_journey.AtMagicalGlade;var location=glade?RecruitLocation.MagicalGlade:_journey.CurrentSiteRecruitLocation;
            string siteName=RecruitmentSiteName(location);
            bool attunement=glade || _offerUnit?.RequiresAttunement==true && _journey.CanRecruitSiteIcon(_offerUnit);
            bool whiteCity=location==RecruitLocation.City && _journey.Game.Map.Places.TryGetValue(p.Position,out var city) && city.CityColor==CityColor.White;
            string siteRule=location==RecruitLocation.Village?"村庄每点治疗需3影响力；部队治疗按等级消费。":location==RecruitLocation.City?"本人每枚城市盾徽+1影响；本次交涉只计一次。":location==RecruitLocation.Keep?"只可在本人已征服的要塞交涉。":location==RecruitLocation.MageTower?"已征服法师塔允许所有玩家交涉。":"请先移动到已揭示且允许交涉的实际地点。";
            var panel=W.Panel(_root,"JourneyInteraction",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title",glade?"魔法林地 · 原魔宠招募":"部队供应与"+siteName+"交涉",40,25,1820,60,36,W.Gold);
            W.Text(panel,"Status",$"当前位置：{siteName} · 影响力 {ctx.InfluencePool} · 声誉 {p.Reputation}\n部队 {p.Units.Count} / 指挥槽 {p.CommandSlots} · 治疗点 {ctx.Healing.Total}",40,105,1810,70,25,W.Cream);
            if(_journey.CanBuySpellAtCurrentSite)Button(panel,"Btn_SiteSpellShop","购买原法术 · 7影响力及同色魔力",1130,177,735,39,()=>{_siteSpellShop=true;_shopSpell=null;});
            int offerPages=Math.Max(1,(offer.Count+2)/3);_unitOfferPage%=offerPages;
            if(offerPages>1)Button(panel,"Btn_NextOffer","原部队供应 "+(_unitOfferPage+1)+"/"+offerPages+" · 下一页",40,177,1025,39,()=>_unitOfferPage=(_unitOfferPage+1)%offerPages);
            for(int i=0;i<Math.Min(3,offer.Count-_unitOfferPage*3);i++)
            {
                var card=offer[_unitOfferPage*3+i];var binding=unitCatalog.Single(u=>u.originalId==card.Id);
                var b=Button(panel,"Btn_Offer_"+card.Id,"",40+i*350,220,325,425,()=>_offerUnit=card,ReferenceEquals(card,_offerUnit));
                W.Sprite(b.transform,"OriginalOfferArt",binding.artwork,44,12,235,282);
                W.Text(b.transform,"UnitName",card.NameCn,12,306,300,38,26,W.Gold,TextAlignmentOptions.Center);
                W.Text(b.transform,"UnitCost",$"影响力 {card.InfluenceCost} · 等级 {card.Level}"+((glade?card.RecruitLocationMask.HasFlag(RecruitLocation.MagicalGlade):_journey.CanRecruitSiteIcon(card))?"\n当前地点可招募"+(card.RequiresAttunement?" · 魔力1":""):"\n无当前地点图标"),12,347,300,68,18,W.Cream,TextAlignmentOptions.Center);
            }
            W.Text(panel,"Rule",glade?"林地招募占本回合行动，不属于交涉。\n支付印刷影响力及一种基本色魔力，声誉不影响费用。\n所选颜色留在魔宠上，决定原能力数值。\n新轮战术前，以库存魔晶维持或移除。":"只可招募当前供应中的原部队。\n招募后本轮不补位，下轮重新翻供应。\n"+siteRule+"\n"+(whiteCity?"白城允许任意原招募图标；声誉独立计算。":"声誉在交涉开始时计一次，负七禁止交涉。"),1130,220,735,118,20,W.Cream);
            if(p.LoyaltySkill!=null)Button(panel,"Btn_LoyaltySlot",LoyaltyActions.SlotAvailable(p)?ctx.UseLoyaltySlot?"忠诚专属槽：本次招募减费5，不可解散":"普通槽 · 点击改用忠诚专属槽":"忠诚专属槽已由 "+p.LoyaltyUnit.Card.NameCn+" 使用",1130,347,735,39,()=>ctx.UseLoyaltySlot=!ctx.UseLoyaltySlot).interactable=LoyaltyActions.SlotAvailable(p);
            if(attunement)
                for(int i=0;i<4;i++){var color=(ManaColor)i;Button(panel,"Btn_RecruitMana_"+color,(_recruitMana==color?"● ":"")+ColorName(color),1130+i*185,394,177,57,()=>_recruitMana=color);}
            if(!glade) Button(panel,"Btn_BeginInteraction","开始当前"+siteName+"交涉",1130,attunement?547:394,735,57,()=>_journey.BeginSiteInteraction()).interactable=!ctx.InNegotiation && _journey.CanInteractAtCurrentSite;
            int cost=_offerUnit==null?0:RecruitmentService.GetCost(p,_offerUnit,location,ctx);
            Button(panel,"Btn_Recruit",_offerUnit==null?"先选择供应中的原部队":$"招募 {_offerUnit.NameCn} · 影响力 {cost}"+(attunement?" + "+ColorName(_recruitMana)+"1":""),1130,470,735,57,()=>
            {if(glade)_journey.RecruitGladeUnit(_offerUnit,_recruitMana);else _journey.RecruitSiteUnit(_offerUnit,attunement?_recruitMana:(ManaColor?)null);_offerUnit=null;},true).interactable=_offerUnit!=null;
            if(location==RecruitLocation.Village)Button(panel,"Btn_BuyHealing","支付3影响力，购买1治疗点",1130,547,735,57,()=>_journey.BuyVillageHealing());
            if(ctx.IntimidateUsed)
                Button(panel,"Btn_RecruitDiscount",ctx.UseIntimidateRecruitDiscount?"已选：无情威压减费2，成功后声誉−1":"不使用无情威压折扣",1130,614,735,42,()=>ctx.UseIntimidateRecruitDiscount=!ctx.UseIntimidateRecruitDiscount);
            var flags=_journey.AvailableCommandBanners();
            for(int i=0;i<flags.Length;i++)
            {
                var flag=flags[i];Button(panel,"Btn_CommandRecruit_"+i,(ReferenceEquals(ctx.CommandBannerForRecruit,flag)?"● ":"")+"用本回合指挥旌旗代替新部队指挥标记",1130,662+i*45,735,41,
                    ()=>_journey.SelectCommandBanner(ReferenceEquals(ctx.CommandBannerForRecruit,flag)?null:flag));
            }
            if(flags.Length>0)Button(panel,"Btn_CommandAssign","附给所选本回合新招部队",1130,715,735,47,()=>_journey.AssignCommandBanner(flags[0],_dismissUnit)).interactable=_dismissUnit!=null;
            W.Text(panel,"Owned","已有部队 · 仅满编换兵时需要选择并解散",40,660,1780,43,24,W.Gold);
            for(int i=0;i<p.Units.Count;i++)
            {
                var unit=p.Units[i];
                Button(panel,"Btn_Owned_"+i,(ReferenceEquals(unit,_dismissUnit)?"● ":"")+unit.Card.NameCn,40+i*355,720,334,65,()=>_dismissUnit=unit);
            }
            Button(panel,"Btn_Dismiss",_dismissUnit==null?"先选择待解散的部队":"解散 "+_dismissUnit.Card.NameCn,40,814,650,57,()=>
            {_journey.DismissSiteUnit(_dismissUnit);_dismissUnit=null;}).interactable=_dismissUnit!=null;
            Button(panel,"Btn_InteractionHand","出牌 / 横置影响力 / 使用治疗点",719,814,1146,57,()=>{_journeyInteraction=false;OpenJourneyHand();},true);
            string contract=ctx.ForbidInfluenceRecruitment?"特立独行承诺生效：本回合不能以影响力招募；仍可支付影响力购买治疗。\n":"";
            W.Text(panel,"Feedback",contract+(_feedback??(glade?"只招募当前真实供应中的原魔宠。成功后本回合不能再移动、战斗或休息。":"在当前合法地点交涉；本回合不能再移动、战斗或休息。")),40,905,1810,65,22,W.Cream);
            Button(panel,"Btn_CloseInteraction",glade?"返回当前回合":"返回当前回合（保留交涉）",40,1000,1050,57,()=>_journeyInteraction=false);
            if(!glade)Button(panel,"Btn_EndInteraction","结束交涉，剩余影响力清零",1130,1000,735,57,()=>_journey.EndSiteInteraction()).interactable=ctx.InNegotiation;
        }
        private void DrawSiteSpellShop()
        {
            var ctx=_journey.Context;
            var offer=_journey.Game.Globals.Spells.Offer;
            if(_shopSpell!=null&&!offer.Any(s=>ReferenceEquals(s,_shopSpell)))_shopSpell=null;
            var panel=W.Panel(_root,"SiteSpellShop",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","法师塔 / 蓝城 · 购买原法术",40,25,1820,60,34,W.Gold);
            W.Text(panel,"Rule","在当前地点交涉，支付7影响力及一枚法术同色基本魔力。\n所选原牌置于本人牌库顶，供应立即补位；取得后仍需正常抽牌与施法。",40,110,1820,95,25,W.Cream);
            W.Text(panel,"Status",$"实际影响力 {ctx.InfluencePool} · "+(ctx.InNegotiation?"交涉进行中":"请先开始地点交涉"),40,211,1820,45,25,W.Gold);
            for(int i=0;i<offer.Count;i++)
            {
                var spell=offer[i];var binding=Binding(new DeedCard(spell.Id,CardType.Spell));
                var b=Button(panel,"Btn_Spell_"+spell.Id,"",50+i*615,275,580,500,()=>_shopSpell=spell,ReferenceEquals(_shopSpell,spell));
                W.Sprite(b.transform,"OriginalSpellArt",binding.artwork,159,12,262,378);
                W.Text(b.transform,"Name",binding.source.NameCn+" · "+ColorName(((SpellCardSO)binding.source).ManaColor)+"1",22,410,536,62,26,W.Cream,TextAlignmentOptions.Center);
            }
            Button(panel,"Btn_BuySpell",_shopSpell==null?"先选择一张实际供应法术":"支付7影响力与同色魔力，购入牌库顶",40,802,1825,65,()=>
            {
                var name=CardName(new DeedCard(_shopSpell.Id,CardType.Spell));
                _journey.BuySiteSpell(_shopSpell);_shopSpell=null;
                _feedback="已购入 "+name+" 到实际牌库顶，原供应已补位。";
            },true).interactable=_shopSpell!=null&&ctx.InNegotiation&&!ctx.InteractionClosed;
            W.Text(panel,"Feedback",_feedback??"选择卡牌后确认购买；付款失败保留卡牌与资源。",40,889,1820,75,24,W.Cream);
            Button(panel,"Btn_Close","返回地点交涉",40,1000,1825,57,()=>_siteSpellShop=false);
        }
    }
}


