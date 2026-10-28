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
        private bool _offeredUnitsOpen;
        private UnitCard _borrowedUnit;
        private int _borrowedAbility,_borrowedOption,_borrowedBoost,_borrowedTarget=-1;
        private int _borrowedPage,_borrowedAttackSlot;
        private UnitState _borrowedUnitTarget;
        private readonly ManaColor[] _borrowedColors={ManaColor.Red,ManaColor.Blue};
        private void OpenOfferedUnits(){_offeredUnitsOpen=true;_borrowedUnit=null;_borrowedAbility=0;_borrowedTarget=-1;_borrowedUnitTarget=null;_borrowedPage=_borrowedAttackSlot=0;}
        private void DrawOfferedUnits()
        {
            var ctx=_journey.Context;var player=_journey.Player;
            var panel=W.Panel(_root,"OfferedUnits",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","战斗号召 · 发动供应原能力",40,25,1810,61,35,W.Gold);
            W.Text(panel,"Rule","剩余许可 "+ctx.OfferedUnitUses+" · 支付部队原能力费用。供应卡不入队、不耗竭，不能替英雄承伤。",40,105,1810,72,24,W.Cream);
            _borrowedPage=Math.Min(_borrowedPage,Math.Max(0,(ctx.UnitOffer.Count-1)/3));
            for(int slot=0;slot<3&&_borrowedPage*3+slot<ctx.UnitOffer.Count;slot++)
            {
                int i=_borrowedPage*3+slot;
                var unit=ctx.UnitOffer[i];var art=unitCatalog.Single(u=>u.originalId==unit.Id).artwork;
                var b=Button(panel,"Btn_Offer_"+i,"",40+slot*350,195,335,337,()=>{_borrowedUnit=unit;_borrowedAbility=0;_borrowedOption=_borrowedBoost=_borrowedAttackSlot=0;_borrowedTarget=-1;_borrowedUnitTarget=null;});
                W.Sprite(b.transform,"OriginalOfferedUnit",art,62,8,210,263);W.Text(b.transform,"UnitName",(ReferenceEquals(unit,_borrowedUnit)?"● ":"")+unit.NameCn,7,285,320,47,23,W.Cream,TextAlignmentOptions.Center);
            }
            if(ctx.UnitOffer.Count>3)
            {
                Button(panel,"Btn_OfferPrevious","上一页供应",40,540,335,43,()=>_borrowedPage--).interactable=_borrowedPage>0;
                W.Text(panel,"OfferPage",(_borrowedPage+1)+" / "+Math.Max(1,(ctx.UnitOffer.Count+2)/3),390,540,335,43,23,W.Cream,TextAlignmentOptions.Center);
                Button(panel,"Btn_OfferNext","下一页供应",740,540,335,43,()=>_borrowedPage++).interactable=(_borrowedPage+1)*3<ctx.UnitOffer.Count;
            }
            if(_borrowedUnit!=null)
            {
                for(int i=0;i<_borrowedUnit.Actions.Length;i++)
                {
                    int n=i;var a=_borrowedUnit.Actions[i];string fee=a.Cost.Length==0?"无魔力费":string.Join("、",a.Cost.Select(ColorName));
                    var b=Button(panel,"Btn_Ability_"+i,(_borrowedAbility==i?"● ":"")+a.PrintedRule+" ["+fee+"]",1140,195+i*64,730,58,()=>{_borrowedAbility=n;_borrowedOption=_borrowedBoost=0;});
                    var t=b.GetComponentInChildren<TextMeshProUGUI>();t.enableAutoSizing=true;t.fontSizeMin=16;t.fontSizeMax=23;
                }
                var ability=_borrowedUnit.Actions[_borrowedAbility];
                if(ability.Kind==UnitAbilityKind.AttackOrBlock||ability.Kind==UnitAbilityKind.TransformAttacks||ability.Kind==UnitAbilityKind.MovementOrInfluence)
                {
                    var labels=ability.Kind==UnitAbilityKind.AttackOrBlock?new[]{"攻击","格挡"}:ability.Kind==UnitAbilityKind.TransformAttacks?new[]{"冰火转换","攻城转换"}:new[]{"移动","影响力"};
                    for(int i=0;i<2;i++){int n=i;Button(panel,"Btn_Option_"+i,(_borrowedOption==i?"● ":"")+labels[i],40+i*350,605,335,52,()=>_borrowedOption=n);}
                }
                if(ability.RedBlueBoost)
                    for(int i=0;i<4;i++){int n=i;Button(panel,"Btn_Boost_"+i,(_borrowedBoost==i?"● ":"")+new[]{"不额外支付","额外红","额外蓝","额外红蓝"}[i],40+i*263,674,250,52,()=>_borrowedBoost=n);}
                if(ability.Kind==UnitAbilityKind.GainChosenTokens)
                    for(int row=0;row<ability.Value;row++)for(int c=0;c<6;c++)
                    {int n=row;var color=(ManaColor)c;Button(panel,"Btn_Color_"+row+"_"+color,(_borrowedColors[row]==color?"● ":"")+ColorName(color),40+c*177,605+row*61,165,52,()=>_borrowedColors[n]=color);}
                if(ability.Kind==UnitAbilityKind.ReadyUnit)
                    for(int i=0;i<player.Units.Count;i++){var u=player.Units[i];Button(panel,"Btn_UnitTarget_"+i,(ReferenceEquals(u,_borrowedUnitTarget)?"● ":"")+u.Card.NameCn,1140,630+i*51,730,45,()=>_borrowedUnitTarget=u);}
                else if(ctx.InBattle)
                    for(int i=0;i<_battle.Enemies.Count;i++){int n=i;Button(panel,"Btn_Target_"+i,(_borrowedTarget==i?"● ":"")+"敌人 "+(i+1),1140+i%2*370,630+i/2*54,355,48,()=>{_borrowedTarget=n;_borrowedAttackSlot=0;}).interactable=!ctx.DefeatedEnemyIndices.Contains(i);}
                if(ctx.InBattle&&ShockTroopsActions.IsReaction(ability)&&_borrowedTarget>=0&&_borrowedTarget<_battle.Enemies.Count)
                {
                    var enemy=_battle.Enemies[_borrowedTarget];
                    for(int i=0;i<enemy.AttackCount;i++)
                    {int attack=i;Button(panel,"Btn_AttackSlot_"+i,(_borrowedAttackSlot==i?"● ":"")+"攻击 #"+(i+1),1140+i%3*245,755+i/3*54,235,48,()=>_borrowedAttackSlot=attack).interactable=!ctx.IsAttackCancelled(_borrowedTarget,i)&&!ctx.SuccessfullyBlockedAttacks.Contains((_borrowedTarget,i));}
                }
                Button(panel,"Btn_Use","支付原费用并发动一次能力",970,1000,900,55,()=>
                {
                    _journey.UseOfferedUnit(_borrowedUnit,_borrowedAbility,_borrowedOption,_borrowedTarget,ability.Kind==UnitAbilityKind.GainChosenTokens?_borrowedColors.Take(ability.Value).ToArray():null,_borrowedBoost,_borrowedUnitTarget,attackIndex:_borrowedAttackSlot);
                    _offeredUnitsOpen=false;_feedback="供应原能力已结算；卡牌仍在供应。";
                },true);
            }
            W.Text(panel,"Feedback",_feedback??"选择实际部队，再选择原能力与必要目标。",40,900,1820,79,23,W.Gold);
            Button(panel,"Btn_Close","返回当前行动",40,1000,890,55,()=>_offeredUnitsOpen=false);
        }
    }
}
