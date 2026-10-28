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
        private bool _manaPaymentOpen,_paymentExtra;
        private object _paymentTarget,_paymentBound;
        private SkillCard _paymentSkill;
        private DeedCard _paymentMaterial;
        private ManaDie _paymentDie;
        private ManaColor _paymentRequired=ManaColor.Red,_paymentSource=ManaColor.Red;
        private ManaSkillPaymentSelection _paymentSelection;
        private int _paymentPage;
        private PlayerState PaymentPlayer=>_journey?.Player??_battle.Player;
        private ActionContext PaymentContext=>_journey?.Context??_battle.Context;
        private static bool IsPaymentSkill(SkillCard skill)=>skill.SourceId=="hero_skill_pool_000:06" || skill.SourceId=="hero_skill_pool_000:07";
        private bool HeadbandPaymentSelected => _headbandPhysical!=null && HeadbandBorrowResolution.IsPayment(_headbandSkill);
        private bool DrawPaymentButton(Transform panel,object target,float x,float y,float width,float height)
        {
            if(target==null || target is DeedCard {Type:CardType.Artifact} || !HeadbandPaymentSelected && !PaymentPlayer.Skills.Any(IsPaymentSkill))return false;
            string label=ReferenceEquals(target,_paymentBound)&&_paymentSelection!=null?"已选付款技能 · 点击修改":"本次费用 · 选择付款技能";
            var b=Button(panel,"Btn_ManaPayment",label,x,y,width,height,()=>
            {
                _paymentTarget=target;_paymentSkill=HeadbandPaymentSelected?_headbandSkill:PaymentPlayer.Skills.First(IsPaymentSkill);
                _paymentMaterial=null;_paymentDie=null;_paymentExtra=false;_paymentPage=0;_manaPaymentOpen=true;
            });
            var t=b.GetComponentInChildren<TextMeshProUGUI>();t.enableAutoSizing=true;t.fontSizeMin=15;t.fontSizeMax=22;
            return true;
        }
        private void WithManaPayment(object target,Action command,Action<DeedCard,ManaSkillPaymentSelection> borrowedCommand=null)
        {
            if(HeadbandPaymentSelected)
            {
                if(borrowedCommand==null)throw new InvalidOperationException("此行动尚未接入头环付款，请选择原手牌的实际费用");
                if(!ReferenceEquals(target,_paymentBound)||_paymentSelection==null)
                    throw new InvalidOperationException("请先选择本次付款技能的实际材料或源色");
                var before=PaymentContext.PendingHeadbandBorrow?.Completed??0;
                try { borrowedCommand(_headbandPhysical,_paymentSelection); }
                finally
                {
                    var pending=PaymentContext.PendingHeadbandBorrow;
                    bool committed=pending?.Completed>before || PaymentPlayer.Deck.PlayedCards.Any(c=>ReferenceEquals(c,_headbandPhysical)) && pending==null;
                    if(committed)
                    {
                        ClearManaPayment();
                        if(pending==null)ClearHeadbandInput();
                    }
                }
            }
            else if(ReferenceEquals(target,_paymentBound)&&_paymentSelection!=null)
            {
                ManaSkillPaymentActions.Execute(PaymentPlayer,PaymentContext,_paymentSelection,command);
                _paymentSelection=null;_paymentBound=null;
            }
            else command();
        }
        private void ClearManaPayment()
        {_manaPaymentOpen=false;_paymentSelection=null;_paymentTarget=null;_paymentBound=null;_paymentSkill=null;_paymentMaterial=null;_paymentDie=null;}
        private void DrawManaPayment()
        {
            var p=PaymentPlayer;var ctx=PaymentContext;
            var panel=W.Panel(_root,"ManaPayment",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","付款技能 · "+(_paymentTarget is DeedCard c?CardName(c):(_paymentTarget as UnitState)?.Card.NameCn),40,25,1810,60,34,W.Gold);
            W.Text(panel,"Instruction","选择本次原费用中的一点；返回后确认出牌或发动能力时统一支付。此处不产生魔力、不消耗次数。",40,100,1810,64,23,W.Cream);
            var skills=HeadbandPaymentSelected?new[]{_headbandSkill}:p.Skills.Where(IsPaymentSkill).ToArray();
            for(int i=0;i<skills.Length;i++)
            {
                var skill=skills[i];Button(panel,"Btn_Skill_"+i,(ReferenceEquals(skill,_paymentSkill)?"● ":"")+skill.Id+(HeadbandPaymentSelected?" · 头环借用":SkillActions.IsUsed(p,skill)?" · 已使用":""),40+i*450,190,430,55,()=>{_paymentSkill=skill;_paymentMaterial=null;_paymentDie=null;_paymentPage=0;});
            }
            Texture(panel,"OriginalPaymentSkill",SkillReference(_paymentSkill),40,280,230,320);
            var rule=W.Text(panel,"OriginalRule",_paymentSkill.PrintedRule,300,280,575,230,24,W.Cream);rule.enableAutoSizing=true;rule.fontSizeMin=19;rule.fontSizeMax=24;
            W.Text(panel,"Inventory",string.Join("\n",Enum.GetValues(typeof(ManaColor)).Cast<ManaColor>().Select(color=>ColorName(color)+$"：魔力 {p.Mana.Tokens.GetValueOrDefault(color)} / 晶体 {p.Mana.Crystals.GetValueOrDefault(color)}")),300,525,560,220,23,W.Cream);
            W.Text(panel,"RequiredLabel","支付原费用中的颜色",940,190,920,50,27,W.Gold);
            for(int i=0;i<6;i++)
            {
                var color=(ManaColor)i;Button(panel,"Btn_Required_"+color,(_paymentRequired==color?"● ":"")+ColorName(color),940+i%3*310,255+i/3*62,295,54,()=>_paymentRequired=color);
            }
            bool invocation=_paymentSkill.SourceId=="hero_skill_pool_000:06";
            if(invocation)
            {
                W.Text(panel,"MaterialLabel","选择实际弃牌材料（不含正在使用的牌）",940,398,920,48,26,W.Gold);
                var cards=p.Deck.Hand.Where(card=>!ReferenceEquals(card,_paymentTarget) && !ReferenceEquals(card,_headbandPhysical)).ToArray();
                _paymentPage=Math.Min(_paymentPage,Math.Max(0,(cards.Length-1)/6));
                for(int slot=0;slot<6 && _paymentPage*6+slot<cards.Length;slot++)
                {
                    int index=_paymentPage*6+slot;var card=cards[index];
                    var b=Button(panel,"Btn_Material_"+index,"",940+slot%3*310,465+slot/3*176,295,165,()=>_paymentMaterial=card,ReferenceEquals(card,_paymentMaterial));
                    var art=CardArtwork(card);if(art!=null)W.Sprite(b.transform,"ActualMaterial",art,12,9,88,125);
                    W.Text(b.transform,"Name",(ReferenceEquals(card,_paymentMaterial)?"● ":"")+CardName(card),art==null?20:112,30,art==null?255:167,107,22,W.Cream);
                }
                Button(panel,"Btn_Previous","上一页",940,830,440,48,()=>_paymentPage--).interactable=_paymentPage>0;
                Button(panel,"Btn_Next","下一页",1400,830,455,48,()=>_paymentPage++).interactable=(_paymentPage+1)*6<cards.Length;
            }
            else
            {
                W.Text(panel,"SourceLabel","消耗的实际原色库存，或一枚公共源骰",940,398,920,48,26,W.Gold);
                for(int i=0;i<6;i++)
                {
                    var color=(ManaColor)i;Button(panel,"Btn_Source_"+color,(_paymentDie==null&&_paymentSource==color?"● ":"")+ColorName(color)+"库存",940+i%3*310,465+i/3*62,295,54,()=>{_paymentSource=color;_paymentDie=null;});
                }
                var dice=ctx.ManaSource?.Dice.ToArray()??Array.Empty<ManaDie>();
                for(int i=0;i<dice.Length;i++)
                {
                    var die=dice[i];Button(panel,"Btn_Die_"+i,(ReferenceEquals(_paymentDie,die)?"● ":"")+"源骰 "+(i+1)+" · "+ColorName(die.Face),940+i%3*310,625+i/3*62,295,54,()=>{_paymentDie=die;_paymentSource=die.Face;});
                }
                if(_paymentDie!=null)Button(panel,"Btn_ExtraDie",_paymentExtra?"使用原额外取骰额度":"使用原普通取骰额度",940,830,915,48,()=>_paymentExtra=!_paymentExtra);
            }
            W.Text(panel,"Feedback",_feedback??"魔咒的材料与其余原费用必须全部合法；两极分化只替换一点，其他费用仍须支付。",40,915,1810,62,23,W.Gold);
            Button(panel,"Btn_Cancel","取消本次付款技能",40,1000,890,55,ClearManaPayment);
            Button(panel,"Btn_Select","选好输入，返回原行动确认",970,1000,900,55,()=>
            {
                _paymentSelection=new ManaSkillPaymentSelection(_paymentSkill,_paymentRequired,invocation?_paymentMaterial:null,invocation?(ManaColor?)null:_paymentSource,invocation?null:_paymentDie,_paymentExtra);
                _paymentBound=_paymentTarget;_manaPaymentOpen=false;
            },true).interactable=(HeadbandPaymentSelected || !SkillActions.IsUsed(p,_paymentSkill)) && (!invocation || _paymentMaterial!=null);
        }
    }
}
