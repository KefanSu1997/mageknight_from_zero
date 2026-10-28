using System.Linq;
using MK.Logic.Runtime;
using TMPro;
using UnityEngine;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private int _bannerRoundPage;

        private void DrawBannerRoundChoices()
        {
            var entries=_journey.Players.SelectMany((owner,playerIndex)=>_journey.RoundBannerUnits(owner)
                .Select(unit=>new {owner,playerIndex,unit,unitIndex=owner.Units.IndexOf(unit)})).ToArray();
            if(entries.Length==0)return;
            _bannerRoundPage=Mathf.Clamp(_bannerRoundPage,0,(entries.Length-1)/4);
            W.Text(_root,"BannerRoundRule","轮末旌旗：默认保留。点选取回后，在结束本轮时回到牌库并洗混。\n指挥旌旗取回会同时移除所附部队；其他旗不移除部队。",740,300,1130,89,23,W.Cream);
            for(int slot=0;slot<4&&_bannerRoundPage*4+slot<entries.Length;slot++)
            {
                var entry=entries[_bannerRoundPage*4+slot];
                bool returning=_journey.IsBannerReturning(entry.owner,entry.unit);
                float y=410+slot*128;
                var art=Binding(entry.unit.Banner)?.artwork;
                if(art!=null)W.Sprite(_root,"RoundBannerArt_"+slot,art,42,y,78,113);
                W.Text(_root,"RoundBannerOwner_"+slot,entry.owner.Name+" · "+entry.unit.Card.NameCn+"\n"+CardName(entry.unit.Banner)+(entry.unit.BannerEffectId=="items_017"?" · 取回将移除部队":""),140,y+8,930,93,24,W.Cream);
                Button(_root,"Btn_BannerReturn_"+entry.playerIndex+"_"+entry.unitIndex,
                    returning?"已选取回 · 点击改为保留":"保留附旗 · 点击改为取回",1110,y+25,760,63,
                    ()=>_journey.ChooseBannerRoundReturn(entry.owner,entry.unit,!returning),returning);
            }
            if(entries.Length>4)
            {
                Button(_root,"Btn_BannerPrevious","上一页",42,938,400,43,()=>_bannerRoundPage--).interactable=_bannerRoundPage>0;
                W.Text(_root,"BannerPage",(_bannerRoundPage+1)+" / "+((entries.Length+3)/4),480,938,180,43,23,W.Cream);
                Button(_root,"Btn_BannerNext","下一页",700,938,400,43,()=>_bannerRoundPage++).interactable=(_bannerRoundPage+1)*4<entries.Length;
            }
        }
    }
}
