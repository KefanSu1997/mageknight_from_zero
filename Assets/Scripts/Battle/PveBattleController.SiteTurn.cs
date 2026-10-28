using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using UnityEngine;
using TMPro;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _siteEndOpen;
        private DeedCard _siteEndWound;
        private ManaColor? _siteEndMine;
        private int _siteEndPage;
        private DeedCard[] SiteEndWounds() => _journey.Player.Deck.Hand
            .Concat(_journey.Player.Deck.DiscardPile).Where(c=>c.Type==CardType.Wound).ToArray();

        private void DrawSiteEndChoice()
        {
            var p=_journey.Player;var place=SiteTurnRules.At(_journey.Game.Map,p);
            bool glade=place?.Type==PlaceType.MagicalGlade;
            var panel=W.Panel(_root,"SiteEnd",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            W.Text(panel,"Title",glade?"魔法林地 · 回合末移除创伤":"矿坑 · 回合末取得魔晶",40,30,1830,65,36,W.Gold);
            W.Text(panel,"Rule",glade?"可以将一张手牌或弃牌中的创伤归还创伤牌库。此效果不治疗部队，也不消费治疗点。":"选择此矿坑提供的一种颜色，取得一枚库存魔晶；同色已有三枚时不再取得。",40,118,1820,90,25,W.Cream);
            if(glade)
            {
                var wounds=SiteEndWounds();_siteEndPage=Math.Min(_siteEndPage,Math.Max(0,(wounds.Length-1)/6));
                for(int slot=0;slot<6 && _siteEndPage*6+slot<wounds.Length;slot++)
                {
                    int index=_siteEndPage*6+slot;var wound=wounds[index];
                    var button=Button(panel,"Btn_Wound_"+index,"",40+slot*306,240,285,390,()=>_siteEndWound=wound);
                    var art=CardArtwork(wound);if(art!=null)W.Sprite(button.transform,"OriginalWound",art,43,15,199,282);
                    string zone=p.Deck.Hand.Any(c=>ReferenceEquals(c,wound))?"手牌":"弃牌";
                    W.Text(button.transform,"Zone",(ReferenceEquals(_siteEndWound,wound)?"● ":"")+zone+" · 创伤",10,315,265,50,25,W.Cream,TextAlignmentOptions.Center);
                }
                Button(panel,"Btn_Previous","上一页",40,669,400,55,()=>_siteEndPage--).interactable=_siteEndPage>0;
                Button(panel,"Btn_Next","下一页",1470,669,400,55,()=>_siteEndPage++).interactable=(_siteEndPage+1)*6<wounds.Length;
                Button(panel,"Btn_Skip","不移除创伤并结束回合",980,889,890,65,()=>CompleteJourneySiteTurn(null));
            }
            else
            {
                var colors=place?.MineColors??Array.Empty<ManaColor>();
                for(int i=0;i<colors.Length;i++)
                {
                    var color=colors[i];Button(panel,"Btn_Color_"+color,(color==_siteEndMine?"● ":"")+ColorName(color)+"魔晶",40+i*450,260,425,80,()=>_siteEndMine=color);
                }
            }
            W.Text(panel,"Feedback",_feedback??"确认后结算本回合地点收益，再完成奖励和补牌。",40,765,1830,85,25,W.Cream);
            Button(panel,"Btn_Confirm","确认选择并结束回合",40,889,890,65,()=>CompleteJourneySiteTurn(new SiteTurnSelection{GladeWound=_siteEndWound,MineColor=_siteEndMine}),true).interactable=glade?_siteEndWound!=null:_siteEndMine.HasValue;
            Button(panel,"Btn_Cancel","返回本回合",40,985,1830,60,()=>_siteEndOpen=false);
        }
    }
}
