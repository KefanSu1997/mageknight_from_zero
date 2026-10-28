using System;
using System.Collections.Generic;
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
        private bool _ownedUnitsOpen;
        private UnitState _outsideUnit,_outsideUnitTarget;
        private int _outsideAbility=-1,_outsideUnitPage,_outsideUnitOption;
        private ScoutTarget _outsideScout;
        private readonly ManaColor[] _outsideUnitColors={ManaColor.Red,ManaColor.Blue};
        private void OpenOwnedUnits()
        {
            _ownedUnitsOpen=true;_outsideUnit=null;_outsideUnitTarget=null;
            _outsideAbility=-1;_outsideUnitPage=0;_outsideUnitOption=0;_outsideScout=null;
        }
        private void DrawOwnedUnits()
        {
            var player=_journey.Player;var ctx=_journey.Context;
            var panel=W.Panel(_root,"OwnedUnits",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title",HeadbandPaymentSelected?"":"自有部队 · 战斗外能力",40,25,1800,58,35,W.Gold);
            W.Text(panel,"Resources","移动 "+ctx.MovementPool+" · 影响力 "+ctx.InfluencePool+" · 治疗 "+ctx.Healing.Total+" · 声誉 "+player.Reputation+" · 手牌创伤 "+player.Wounds+
                "\n"+string.Join("   ",new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}.Select(c=>ColorName(c)+" 晶"+player.Mana.Crystals.GetValueOrDefault(c)+"/标"+player.Mana.Tokens.GetValueOrDefault(c))),40,100,1810,72,24,W.Cream);
            for(int slot=0;slot<4 && _outsideUnitPage*4+slot<player.Units.Count;slot++)
            {
                int n=_outsideUnitPage*4+slot;var unit=player.Units[n];
                var b=Button(panel,"Btn_Unit_"+n,"",40+slot%2*292,195+slot/2*315,278,300,()=>
                {_outsideUnit=unit;_outsideAbility=_journey.OutsideUnitAbilities(unit).DefaultIfEmpty(-1).First();_outsideUnitTarget=null;_outsideUnitOption=0;_outsideScout=null;});
                W.Sprite(b.transform,"OriginalOutsideUnit",unitCatalog.Single(v=>v.originalId==unit.Card.Id).artwork,44,8,190,220);
                var stateText=W.Text(b.transform,"UnitState",(ReferenceEquals(unit,_outsideUnit)?"● ":"")+unit.Card.NameCn+"\n"+(unit.IsReady?"就绪":"耗竭")+" · 创伤 "+unit.Wounds,8,230,262,64,19,W.Cream,TextAlignmentOptions.Center);
                stateText.fontSizeMin=16;stateText.fontSizeMax=19;
            }
            Button(panel,"Btn_Previous","上一页",40,839,278,48,()=>_outsideUnitPage--).interactable=_outsideUnitPage>0;
            Button(panel,"Btn_Next","下一页",332,839,278,48,()=>_outsideUnitPage++).interactable=(_outsideUnitPage+1)*4<player.Units.Count;
            if(_outsideUnit!=null)
            {
                var choices=_journey.OutsideUnitAbilities(_outsideUnit);
                W.Text(panel,"OriginalUnitName",_outsideUnit.Card.NameCn+" · 原能力"+(_outsideUnit.AttunementColor.HasValue?" · 调谐："+ColorName(_outsideUnit.AttunementColor.Value):""),665,190,1190,53,30,W.Gold);
                for(int row=0;row<choices.Length;row++)
                {
                    int index=choices[row];var a=_outsideUnit.Card.Actions[index];
                    var b=Button(panel,"Btn_Ability_"+index,(_outsideAbility==index?"● ":"")+a.PrintedRule+" ["+(a.Cost.Length==0?"无魔力费":string.Join("、",a.Cost.Select(ColorName)))+"]",665,260+row*68,1205,60,()=>{_outsideAbility=index;_outsideUnitTarget=null;_outsideUnitOption=0;_outsideScout=null;});
                    var t=b.GetComponentInChildren<TextMeshProUGUI>();t.enableAutoSizing=true;t.fontSizeMin=17;t.fontSizeMax=24;
                }
                if(choices.Length==0)W.Text(panel,"NoOutsideAbility","这支部队的能力需要战斗阶段。",665,260,1190,62,24,W.Muted);
                if(_outsideAbility>=0 && choices.Contains(_outsideAbility))
                {
                    var ability=_outsideUnit.Card.Actions[_outsideAbility];
                    if(ability.Kind==UnitAbilityKind.MovementOrInfluence)
                        for(int i=0;i<2;i++){int n=i;Button(panel,"Btn_Option_"+i,(_outsideUnitOption==i?"● ":"")+(i==0?"移动":"影响力"),665+i*610,540,590,55,()=>_outsideUnitOption=n);}
                    if(ability.Kind==UnitAbilityKind.GainChosenTokens)
                        for(int row=0;row<ability.Value;row++)for(int c=0;c<6;c++)
                        {int n=row;var color=(ManaColor)c;Button(panel,"Btn_Color_"+row+"_"+color,(_outsideUnitColors[n]==color?"● ":"")+ColorName(color),665+c*202,540+row*65,193,55,()=>_outsideUnitColors[n]=color);}
                    if(ability.Kind==UnitAbilityKind.ReadyUnit)
                        for(int i=0;i<player.Units.Count;i++)
                        {var u=player.Units[i];Button(panel,"Btn_Target_"+i,(ReferenceEquals(u,_outsideUnitTarget)?"● ":"")+u.Card.NameCn+" · 等级 "+u.Card.Level+" · 创伤 "+u.Wounds,665+i%2*610,540+i/2*58,590,51,()=>_outsideUnitTarget=u);}
                    if(ability.Kind==UnitAbilityKind.ScoutToken)DrawScoutTargets(panel);
                    Button(panel,"Btn_Use","支付原费用并发动",970,1000,900,55,()=>
                    {
                        WithManaPayment(_outsideUnit,()=>_journey.UseOutsideUnit(_outsideUnit,_outsideAbility,_outsideUnitOption,_outsideUnitTarget,ability.Kind==UnitAbilityKind.GainChosenTokens?_outsideUnitColors.Take(ability.Value).ToArray():null,_outsideScout),
                            (physical,payment)=>_journey.UseOutsideUnitWithBorrowedPayment(physical,payment,_outsideUnit,_outsideAbility,_outsideUnitOption,_outsideUnitTarget,ability.Kind==UnitAbilityKind.GainChosenTokens?_outsideUnitColors.Take(ability.Value).ToArray():null,_outsideScout));
                        _outsideUnitTarget=null;
                    },true).interactable=_outsideUnit.CanActivate;
                }
            }
            W.Text(panel,"Feedback",_feedback??"发动后按原规则耗竭；重整与治疗分别结算。移动与侦察须在开始行动前使用。",40,915,1810,65,23,W.Gold);
            DrawPaymentButton(panel,_outsideUnit,665,852,1205,48);
            Button(panel,"Btn_Close","返回当前行动",40,1000,890,55,()=>_ownedUnitsOpen=false);
        }
        private void DrawScoutTargets(Transform panel)
        {
            int row=0;
            foreach(var cell in _journey.Game.Map.Places.Where(x=>x.Value.Defenders.Count>0 && !_journey.Game.Map.RevealedSites.Contains(x.Value)))
            {
                var at=cell.Key;int dq=at.Q-_journey.Player.Position.Q,dr=at.R-_journey.Player.Position.R;
                if((Math.Abs(dq)+Math.Abs(dr)+Math.Abs(dq+dr))/2>3)continue;
                for(int i=0;i<cell.Value.Defenders.Count;i++)
                {
                    var target=new ScoutTarget(Position:at,TokenIndex:i);
                    Button(panel,"Btn_ScoutSite_"+at.Q+"_"+at.R+"_"+i,(_outsideScout==target?"● ":"")+"地点 ("+at.Q+","+at.R+") 标记 "+(i+1),665+row%2*345,540+row/2*57,330,50,()=>_outsideScout=target);row++;
                }
            }
            foreach(EnemyTokenColor color in Enum.GetValues(typeof(EnemyTokenColor)))
            {
                if(!_journey.EnemySupply.CanDraw(color))continue;
                var target=new ScoutTarget(SupplyColor:color);
                Button(panel,"Btn_ScoutSupply_"+color,(_outsideScout==target?"● ":"")+"敌人堆顶 · "+color,665+row%2*345,540+row/2*57,330,50,()=>_outsideScout=target);row++;
            }
            var seen=_journey.Context.LastScoutObservation;
            if(seen?.Enemy!=null)
            {
                var art=worldEnemyCatalog.Single(v=>v.originalId==seen.Enemy.Id);
                Texture(panel,"OriginalPrivateToken",art.originalToken,1460,540,210,210);
                W.Text(panel,"PrivateObservation","私看："+art.displayName+"\n同一标记本回合击败：名望+1\n未公开揭示，未从供给抽走。",1390,770,480,103,23,W.Cream);
            }
        }
    }
}
