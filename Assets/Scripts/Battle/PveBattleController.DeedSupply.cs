using System;
using System.Linq;
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
        private bool _learningOpen,_learningEnhanced;
        private void OpenLearningSupply()
        {
            _learningOpen=true;_inputAdvanced=null;_inputSpells.Clear();
            _learningEnhanced=(_journey.Context.LearningModes&1)==0;
        }
        private void DrawInputSupply(Transform panel,bool spells,int y)
        {
            var ctx=_journey?.Context??_battle.Context;
            string[] ids=spells?ctx.SpellSupply?.Offer.Select(c=>c.Id).ToArray():ctx.AdvActionSupply?.Offer.Select(c=>c.Id).ToArray();
            W.Text(panel,"SupplyTitle",spells?"选择实际法术供应 · 牌留原处":"选择实际高级行动供应 · 取得后补位",1140,y,726,58,22,W.Gold);
            for(int i=0;ids!=null&&i<ids.Length;i++)
            {
                int n=i;var d=deedCatalog.Single(c=>c.id==ids[n]);
                int order=spells?_inputSpells.IndexOf(ctx.SpellSupply.Offer[n]):-1;
                bool selected=spells?order>=0:ReferenceEquals(_inputAdvanced,ctx.AdvActionSupply.Offer[n]);
                var b=Button(panel,"Btn_InputOffer_"+i,"",1140+i*247,y+75,232,286,()=>
                {
                    if(spells){var offered=ctx.SpellSupply.Offer[n];if(!_inputSpells.Remove(offered))_inputSpells.Add(offered);}
                    else _inputAdvanced=ctx.AdvActionSupply.Offer[n];
                });
                W.Sprite(b.transform,"OriginalSupply",d.artwork,38,7,156,225);
                var name=W.Text(b.transform,"SupplyName",(selected?(spells?"选"+(order+1)+" · ":"● "):"")+d.source.NameCn,7,241,218,40,21,W.Cream,TextAlignmentOptions.Center);
                name.enableAutoSizing=true;name.fontSizeMin=16;name.fontSizeMax=21;
            }
        }
        private void DrawLearningSupply()
        {
            var ctx=_journey.Context;var panel=W.Panel(_root,"LearningSupply",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            var original=deedCatalog.Single(c=>c.id=="advanced_card_026");var action=(ActionCardSO)original.source;
            W.Text(panel,"Title","学习 · 使用本回合购牌能力",40,25,1820,60,34,W.Gold);
            W.Sprite(panel,"OriginalLearning",original.artwork,40,110,220,319);
            W.Text(panel,"OriginalRule",_learningEnhanced?action.EnhancedEffect:action.BaseEffect,295,110,795,269,23,W.Cream);
            W.Text(panel,"AvailableInfluence","实际影响力："+ctx.InfluencePool+"\n"+(_learningEnhanced?"支付9影响力，所选牌加入手牌。":"支付6影响力，所选牌进入弃牌。")+"\n一次实际购牌后，本回合能力用尽。",40,490,1040,188,30,W.Cream);
            Button(panel,"Btn_LearnBase",(!_learningEnhanced?"● ":"")+"基础购牌 · 支付6",40,715,495,65,()=>_learningEnhanced=false).interactable=(ctx.LearningModes&1)!=0;
            Button(panel,"Btn_LearnStrong",(_learningEnhanced?"● ":"")+"强化购牌 · 支付9",565,715,495,65,()=>_learningEnhanced=true).interactable=(ctx.LearningModes&2)!=0;
            DrawInputSupply(panel,false,170);
            W.Text(panel,"Feedback",_feedback??"可以先返回积累影响力；确认前不扣费、不取得牌。",40,890,1830,75,23,W.Gold);
            Button(panel,"Btn_CloseLearning","返回当前行动",40,999,890,55,()=>_learningOpen=false);
            Button(panel,"Btn_BuyLearning","支付影响力并取得原牌",970,999,900,55,()=>
            {
                var card=_journey.PurchaseLearnedCard(_inputAdvanced,_learningEnhanced);_learningOpen=false;_inputAdvanced=null;
                RecordCostUiAcquisition(card);
                _feedback="已取得原牌："+CardName(card)+(_learningEnhanced?"，加入实际手牌。":"，放入实际弃牌堆。");
            },true).interactable=!ctx.LearnUsed;
        }
    }
}
