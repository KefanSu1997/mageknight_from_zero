using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime;
using UnityEngine;
using TMPro;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;
namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private void DrawSacrificeInputs(Transform panel,bool strong)
        {
            var artifacts=_inputMaterials.Where(c=>c.Type==CardType.Artifact).ToArray();int count=strong?2:artifacts.Length;
            while(_inputBonuses.Count>count)_inputBonuses.RemoveAt(_inputBonuses.Count-1);
            while(_inputBonuses.Count<count)_inputBonuses.Add(-1);
            for(int row=0;row<count;row++)
            {
                int n=row;var colors=strong?(row==0?new[]{ManaColor.Green,ManaColor.White}:new[]{ManaColor.Red,ManaColor.Blue}):new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White};
                string label=strong?(row==0?"绿：攻城4 / 白：远程6，每对":"红：火焰 / 蓝：寒冰"):"神器 "+(row+1)+" · "+CardName(artifacts[row]);
                W.Text(panel,"SacrificeLabel_"+row,label,40,460+row*111,1050,42,23,W.Gold);
                for(int i=0;i<colors.Length;i++)
                {var c=colors[i];Button(panel,"Btn_SacrificeColor_"+row+"_"+c,(_inputBonuses[row]==(int)c?"● ":"")+ColorName(c),40+i*264,509+row*111,250,51,()=>_inputBonuses[n]=(int)c);}
            }
        }
        private void DrawRestoreInputs(Transform panel,bool strong)
        {
            var ctx=_journey?.Context??_battle.Context;var player=_journey?.Player??_battle.Player;
            int limit=ctx.CurrentTerrain==TerrainType.Forest?5:3;
            W.Text(panel,"RestoreRule","当前地形治疗 "+limit+"；治疗点另选伤牌或部队消费。\n"+(strong?"可重整自有部队，总等级不超过 "+limit+"；重整不会治疗创伤。":"基础效果不重整部队。"),40,440,1050,112,25,W.Gold);
            if(!strong)return;
            var units=player.Units;_restorePage=Math.Min(_restorePage,Math.Max(0,(units.Count-1)/4));
            W.Text(panel,"RestoreSelected","已选 "+_inputUnits.Count+" 支 · 总等级 "+_inputUnits.Sum(u=>u.Card.Level)+" / "+limit,1140,440,726,48,24,W.Gold);
            for(int slot=0;slot<4&&_restorePage*4+slot<units.Count;slot++)
            {
                int i=_restorePage*4+slot;var unit=units[i];bool selected=_inputUnits.Any(u=>ReferenceEquals(u,unit));
                var b=Button(panel,"Btn_RestoreUnit_"+i,"",1140+slot%2*370,500+slot/2*151,351,142,()=>
                {int index=_inputUnits.FindIndex(u=>ReferenceEquals(u,unit));if(index>=0)_inputUnits.RemoveAt(index);else _inputUnits.Add(unit);});
                b.interactable=!unit.IsDestroyed;
                var art=unitCatalog.Single(u=>u.originalId==unit.Card.Id).artwork;
                W.Sprite(b.transform,"OriginalRestoreUnit",art,8,6,100,127);
                var label=W.Text(b.transform,"RestoreUnitName",(selected?"● ":"")+unit.Card.NameCn+"\n等级 "+unit.Card.Level+" · "+(unit.IsReady?"就绪":"耗竭")+"\n创伤 "+unit.Wounds,119,12,221,119,23,W.Cream);
                label.enableAutoSizing=true;label.fontSizeMin=17;label.fontSizeMax=23;
            }
            if(units.Count>4)
            {
                Button(panel,"Btn_RestorePrevious","上一页部队",1140,815,351,43,()=>_restorePage--).interactable=_restorePage>0;
                Button(panel,"Btn_RestoreNext","下一页部队",1510,815,351,43,()=>_restorePage++).interactable=(_restorePage+1)*4<units.Count;
            }
        }
        private void DrawHonorSummonsInputs(Transform panel)
        {
            var ctx=_journey?.Context??_battle.Context;var player=_journey?.Player??_battle.Player;var offer=ctx.UnitOffer;
            _honorPage=Math.Min(_honorPage,Math.Max(0,((offer?.Count??0)-1)/3));
            for(int slot=0;offer!=null&&slot<3&&_honorPage*3+slot<offer.Count;slot++)
            {
                int i=_honorPage*3+slot;var unit=offer[i];var art=unitCatalog.Single(u=>u.originalId==unit.Id).artwork;
                var b=Button(panel,"Btn_HonorOffer_"+i,"",40+slot*350,463,335,342,()=>_inputOfferedUnit=unit);
                W.Sprite(b.transform,"OriginalHonorUnit",art,61,8,212,267);
                W.Text(b.transform,"HonorName",(ReferenceEquals(unit,_inputOfferedUnit)?"● ":"")+unit.NameCn,8,286,319,49,23,W.Cream,TextAlignmentOptions.Center);
            }
            if(offer!=null&&offer.Count>3)
            {
                Button(panel,"Btn_HonorPrevious","上一页供应",40,815,335,43,()=>_honorPage--).interactable=_honorPage>0;
                W.Text(panel,"HonorPage",(_honorPage+1)+" / "+Math.Max(1,(offer.Count+2)/3),390,815,335,43,23,W.Cream,TextAlignmentOptions.Center);
                Button(panel,"Btn_HonorNext","下一页供应",740,815,335,43,()=>_honorPage++).interactable=(_honorPage+1)*3<offer.Count;
            }
            W.Text(panel,"DisbandRule","指挥槽已满时，选择一支自有部队解散。",1140,460,726,60,23,W.Gold);
            for(int i=0;i<player.Units.Count;i++)
            {var u=player.Units[i];Button(panel,"Btn_HonorDisband_"+i,(ReferenceEquals(u,_inputDisband)?"● ":"")+u.Card.NameCn,1140,539+i*56,726,50,()=>_inputDisband=ReferenceEquals(u,_inputDisband)?null:u);}
        }
    }
}
