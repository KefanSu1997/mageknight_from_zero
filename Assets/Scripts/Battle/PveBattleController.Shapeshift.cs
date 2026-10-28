using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private SkillCard _shapeSkill;
        private DeedCard _shapeCard, _shapeMaterial;
        private bool _shapeEnhanced, _shapePickingMaterial;
        private int _shapePage, _shapeOption;
        private int _shapeInputPage;
        private AxialCoord? _shapeHex;
        private TerrainType? _shapeTerrain;
        private FixedCardOutput[] _shapeOutputs=Array.Empty<FixedCardOutput>();
        private void OpenShapeshift(SkillCard skill)
        {
            _shapeSkill=skill;_shapeCard=null;_shapeMaterial=null;_shapePage=0;
            _shapeEnhanced=false;_shapePickingMaterial=false;_shapeOutputs=Array.Empty<FixedCardOutput>();
            _shapeHex=null;_shapeTerrain=null;_shapeInputPage=0;
        }
        private void RefreshShapeOutputs()
        {
            _shapeOutputs=_shapeCard==null?Array.Empty<FixedCardOutput>():ShapeshiftActions.Choices(_shapeCard,_shapeEnhanced).ToArray();
            _shapeOption=_shapeOutputs.FirstOrDefault()?.Option??0;
            _shapeHex=null;_shapeTerrain=null;_shapeInputPage=0;
        }
        private static string ShapeKind(FixedOutputKind kind)=>kind==FixedOutputKind.Movement?"移动":kind==FixedOutputKind.Attack?"攻击":"格挡";
        private void DrawShapeshift()
        {
            var player=_journey?.Player??_battle.Player;
            var context=_journey?.Context??_battle.Context;
            bool borrowed=IsHeadbandInput(_shapeSkill);
            var target=!context.InBattle?FixedOutputKind.Movement:context.CombatPhase==Phase.Block?FixedOutputKind.Block:FixedOutputKind.Attack;
            var cards=player.Deck.Hand.Where(c=>(!borrowed || !ReferenceEquals(c,_headbandPhysical)) && (_shapePickingMaterial?c.Type!=CardType.Wound&&!ReferenceEquals(c,_shapeCard):c.Type==CardType.Action&&c.Id.StartsWith("basic_card_"))).ToArray();
            _shapePage=Math.Min(_shapePage,Math.Max(0,(cards.Length-1)/6));
            var panel=W.Panel(_root,"Shapeshift",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","变换身形 · "+(_shapePickingMaterial?"选择实际弃牌材料":"选择实际基本行动卡"),40,25,1800,65,36,W.Gold);
            W.Text(panel,"Instruction",borrowed?HeadbandInputInstruction():"选择原固定项，按原牌付款并转换；动态奖励仍按原规则结算。",40,105,1800,65,24,W.Cream);
            for(int slot=0;slot<6&&_shapePage*6+slot<cards.Length;slot++)
            {
                int index=_shapePage*6+slot;var card=cards[index];
                var b=Button(panel,"Btn_Card_"+index,"",40+slot%3*306,190+slot/3*280,285,263,()=>
                {
                    if(_shapePickingMaterial){_shapeMaterial=card;_shapePickingMaterial=false;_shapePage=0;}
                    else{_shapeCard=card;_shapeMaterial=null;RefreshShapeOutputs();}
                },ReferenceEquals(card,_shapePickingMaterial?_shapeMaterial:_shapeCard));
                var art=Binding(card)?.artwork;
                if(art!=null)W.Sprite(b.transform,"OriginalCard_"+card.Id,art,70,8,145,210);
                W.Text(b.transform,"Name",CardName(card),8,226,270,32,21,W.Cream,TextAlignmentOptions.Center);
            }
            Button(panel,"Btn_Previous","上一页",40,768,435,55,()=>_shapePage--).interactable=_shapePage>0;
            Button(panel,"Btn_Next","下一页",496,768,435,55,()=>_shapePage++).interactable=(_shapePage+1)*6<cards.Length;
            Texture(panel,"OriginalSkillReference",SkillReference(_shapeSkill),1000,190,215,301);
            W.Text(panel,"OriginalRule",_shapeSkill.PrintedRule,1240,190,600,301,24,W.Cream);
            Button(panel,"Btn_Basic","原基础效果",1000,510,408,55,()=>{_shapeEnhanced=false;RefreshShapeOutputs();},!_shapeEnhanced);
            Button(panel,"Btn_Strong","原强化效果 · 支付原费用",1430,510,408,55,()=>{_shapeEnhanced=true;RefreshShapeOutputs();},_shapeEnhanced);
            for(int i=0;i<_shapeOutputs.Length;i++)
            {
                var output=_shapeOutputs[i];
                Button(panel,"Btn_Output_"+output.Option,ShapeKind(output.Kind)+" "+ContributionElement(output.Element)+" "+output.Value,1000+i%2*430,584+i/2*65,408,57,()=>_shapeOption=output.Option,_shapeOption==output.Option);
            }
            if(_shapeCard?.Id=="basic_card_023")
                Button(panel,"Btn_Material",_shapeMaterial==null?"随机应变：选择实际弃牌":"弃牌："+CardName(_shapeMaterial),1000,725,838,58,()=>{_shapePickingMaterial=!_shapePickingMaterial;_shapePage=0;});
            bool druid=_shapeCard?.Id=="basic_card_018";
            bool effectiveStrong=_shapeEnhanced||context.NextCardEnhanced;
            if(druid)
            {
                if(effectiveStrong)
                {
                    var terrains=new[]{TerrainType.Plains,TerrainType.Hills,TerrainType.Forest,TerrainType.Desert,TerrainType.Swamp,TerrainType.Wasteland,TerrainType.Lake,TerrainType.Mountain};
                    for(int i=0;i<terrains.Length;i++)
                    {var terrain=terrains[i];Button(panel,"Btn_Terrain_"+terrain,TerrainName(terrain)+"减费1",1000+i%4*211,655+i/4*55,201,48,()=>_shapeTerrain=terrain,_shapeTerrain==terrain);}
                }
                else
                {
                    var cells=context.MovementMap?.Placed.OrderBy(x=>x.Key.Q).ThenBy(x=>x.Key.R).ToArray();
                    int count=cells?.Length??0;
                    _shapeInputPage=Math.Min(_shapeInputPage,Math.Max(0,(count-1)/6));
                    for(int i=0;i<6&&_shapeInputPage*6+i<count;i++)
                    {var cell=cells[_shapeInputPage*6+i];Button(panel,"Btn_Hex_"+cell.Key.Q+"_"+cell.Key.R,TerrainName(cell.Value.Edges[0])+" ("+cell.Key.Q+","+cell.Key.R+")",1000+i%3*282,650+i/3*48,272,43,()=>_shapeHex=cell.Key,_shapeHex.HasValue&&_shapeHex.Value.Equals(cell.Key));}
                    Button(panel,"Btn_InputPrevious","上一页地图格",1000,743,408,38,()=>_shapeInputPage--).interactable=_shapeInputPage>0;
                    Button(panel,"Btn_InputNext","下一页地图格",1430,743,408,38,()=>_shapeInputPage++).interactable=(_shapeInputPage+1)*6<count;
                    foreach(var name in new[]{"Btn_InputPrevious","Btn_InputNext"}) panel.Find(name).GetComponentInChildren<TextMeshProUGUI>().fontSize=18;
                }
            }
            var selected=_shapeOutputs.FirstOrDefault(o=>o.Option==_shapeOption);
            W.Text(panel,"Result",selected==null?"这项原效果没有可转换的固定数值。":"本阶段转换为："+ShapeKind(target)+" "+selected.Value+"；保留原元素。",1000,borrowed?785:803,838,borrowed?38:75,25,W.Gold);
            if(borrowed)DrawHeadbandFinish(panel);
            W.Text(panel,"Feedback",_feedback??"确认后沿用正式原牌费用、材料与牌区，成功才消耗技能次数。",40,889,1800,70,23,W.Cream);
            Button(panel,"Btn_Back","返回技能",40,990,880,60,()=>{_shapeSkill=null;if(borrowed)ClearHeadbandInput();}).interactable=!borrowed || _journey.Context.PendingHeadbandBorrow==null;
            Button(panel,"Btn_Confirm","付款并转换所选原牌",960,990,880,60,()=>
            {
                if(borrowed){_journey.UseHeadbandShapeshift(_headbandPhysical,_headbandSkill,_shapeCard,_shapeEnhanced,_shapeOption,target,_shapeMaterial,_shapeHex,_shapeTerrain);AfterHeadbandInput();return;}
                if(context.InBattle)_battle.PlayShapeshift(_shapeSkill,_shapeCard,_shapeEnhanced,_shapeOption,target,_shapeMaterial,_shapeHex,_shapeTerrain);
                else _journey.PlayShapeshift(_shapeSkill,_shapeCard,_shapeEnhanced,_shapeOption,target,_shapeMaterial,_shapeHex,_shapeTerrain);
                _shapeSkill=null;_shapeCard=null;_shapeMaterial=null;
            },true).interactable=selected!=null&&selected.Kind!=target&&!_shapePickingMaterial&&(borrowed||!SkillActions.IsUsed(player,_shapeSkill))&&(_shapeCard.Id!="basic_card_023"||_shapeMaterial!=null)&&(!druid||(effectiveStrong?_shapeTerrain.HasValue:_shapeHex.HasValue));
        }
    }
}
