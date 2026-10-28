using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;
namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private DeedCard _copyHost,_copyDonor;
        private AdvActionCard _copyOffer;
        private string _copyChoice;
        private bool _copyBattle,_copyEnhanced,_copyWoundDiscard;
        private int _copyPage;
        private void OpenCopyPicker(DeedCard host,string choice,bool battle)
        {
            _inputCard=null;_copyHost=host;_copyChoice=choice;_copyBattle=battle;_copyDonor=null;_copyOffer=null;_copyPage=0;
            _copyEnhanced=choice=="strong"||(_journey?.Context??_battle.Context).NextCardEnhanced;_copyWoundDiscard=false;
        }
        private void DrawCopyPicker()
        {
            var ctx=_journey?.Context??_battle.Context;var player=_journey?.Player??_battle.Player;bool overload=_copyHost.Id=="advanced_card_024";
            var panel=W.Panel(_root,"CopyPicker",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            var binding=Binding(_copyHost);var source=(ActionCardSO)binding.source;
            W.Text(panel,"Title",source.NameCn+" · 选择实际原行动",40,25,1820,60,34,W.Gold);
            W.Sprite(panel,"OriginalCopyHost",binding.artwork,40,115,240,348);
            W.Text(panel,"CopyRule",_copyEnhanced?source.EnhancedEffect:source.BaseEffect,40,492,510,196,23,W.Cream);
            string[] ids=overload?player.Deck.Hand.Where(c=>c.Type==CardType.Action&&!ReferenceEquals(c,_copyHost)).Select(c=>c.Id).ToArray():ctx.AdvActionSupply.Offer.Select(c=>c.Id).ToArray();
            var materials=player.Deck.Hand.Where(c=>c.Type==CardType.Action&&!ReferenceEquals(c,_copyHost)).ToArray();
            for(int i=0;i<6&&_copyPage*6+i<ids.Length;i++)
            {
                int n=_copyPage*6+i;var d=deedCatalog.Single(c=>c.id==ids[n]);bool selected=overload?ReferenceEquals(_copyDonor,materials[n]):ReferenceEquals(_copyOffer,ctx.AdvActionSupply.Offer[n]);
                var b=Button(panel,"Btn_CopyDonor_"+n,"",600+i%3*424,160+i/3*319,402,301,()=>{if(overload)_copyDonor=materials[n];else _copyOffer=ctx.AdvActionSupply.Offer[n];});
                W.Sprite(b.transform,"OriginalCopyDonor",d.artwork,123,7,156,225);
                W.Text(b.transform,"CopyDonorName",(selected?"● ":"")+d.source.NameCn,9,243,383,47,23,W.Cream,TextAlignmentOptions.Center);
            }
            if(!overload)Button(panel,"Btn_CopyWound",_copyWoundDiscard?"创伤：加入弃牌":"创伤：加入手牌",40,721,510,62,()=>_copyWoundDiscard=!_copyWoundDiscard);
            Button(panel,"Btn_CopyPrevious","上一页",600,821,402,48,()=>_copyPage--).interactable=_copyPage>0;
            Button(panel,"Btn_CopyNext","下一页",1448,821,402,48,()=>_copyPage++).interactable=(_copyPage+1)*6<ids.Length;
            string id=overload?_copyDonor?.Id:_copyOffer?.Id;
            bool available=id!=null&&id!="advanced_card_024"&&id!="advanced_card_030"&&PveCopyInputs.Choices(ctx,id,_copyEnhanced).Length>0;
            W.Text(panel,"Feedback",_feedback??(id!=null&&!available?"此供体当前没有合法操作入口；多层复制仍待扩展。":"下一步按所选原牌逐次选择；供应变化与新抽手牌立即生效。"),40,900,1810,77,23,W.Gold);
            Button(panel,"Btn_CloseCopy","返回当前行动",40,1000,890,55,()=>_copyHost=null);
            Button(panel,"Btn_CopyEffect","选择供体原效果",970,1000,900,55,()=>OpenCopyEffect(id,false),true).interactable=available;
        }
        private void OpenCopyEffect(string id,bool pending)
        {
            var ctx=_journey?.Context??_battle.Context;bool enhanced=pending?ctx.PendingRepeatedEffect.Enhanced:_copyEnhanced;
            bool battle=pending?ctx.InBattle:_copyBattle;
            var donor=!pending&&_copyDonor!=null?_copyDonor:new DeedCard(id,CardType.Action);
            var choices=PveCopyInputs.Choices(ctx,id,enhanced);
            if(choices.Length==0)throw new InvalidOperationException("供体当前没有合法选择");
            OpenDeedInputs(donor,choices[0].Id,battle,true);_inputForcedEnhanced=enhanced;
            _inputSubmit=(choice,input)=>
            {
                if(pending){if(battle)_battle.ContinueCopiedEffect(choice,input);else _journey.ContinueCopiedEffect(choice,input);}
                else
                {
                    var selected=new PveDeedSelection{Cards=_copyDonor==null?Array.Empty<DeedCard>():new[]{_copyDonor},Advanced=_copyOffer,BorrowedChoice=choice,BorrowedInputs=input,WoundToDiscard=_copyWoundDiscard};
                    if(battle)_battle.Play(_copyHost,_copyChoice,inputs:selected);else _journey.PlayOutsideWithInputs(_copyHost,_copyChoice,selected);
                    _copyHost=null;
                }
            };
        }
        private void DrawPendingCopy()
        {
            var pending=(_journey?.Context??_battle.Context).PendingRepeatedEffect;
            if(pending==null||_inputCard!=null)return;
            var panel=W.Panel(_root,"PendingCopy",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            var d=deedCatalog.Single(c=>c.id==pending.OriginalCardId);
            W.Text(panel,"Title","效能过载 · 继续原效果",40,25,1820,66,36,W.Gold);W.Sprite(panel,"OriginalRepeatedCard",d.artwork,60,150,320,464);
            W.Text(panel,"Progress",d.source.NameCn+" · 已完成 "+pending.Completed+" 次 / 剩余 "+pending.Remaining+" 次\n根据当前手牌和供应重新选择。已完成的结果保留；结束前不能推进阶段或回合。",440,160,1360,210,30,W.Cream);
            W.Text(panel,"Feedback",_feedback??"",440,440,1360,145,23,W.Gold);
            Button(panel,"Btn_ContinueOriginal","选择下一次原效果",440,660,1360,70,()=>OpenCopyEffect(pending.OriginalCardId,true),true);
        }
        private void DrawBorrowedEnemyInputs(Transform panel)
        {
            var ctx=_journey?.Context??_battle.Context;
            for(int i=0;ctx.Enemies!=null&&i<ctx.Enemies.Count;i++)
            {int n=i;Button(panel,"Btn_InputEnemy_"+i,(_inputEnemy==i?"● ":"")+"实际敌人 "+(i+1),40+i%3*350,489+i/3*66,334,54,()=>{_inputEnemy=n;_inputAttack=0;}).interactable=!ctx.DefeatedEnemyIndices.Contains(i);}
            if(_inputEnemy>=0&&ctx.Enemies[_inputEnemy].AttackCount>1)
                for(int i=0;i<ctx.Enemies[_inputEnemy].AttackCount;i++){int n=i;Button(panel,"Btn_InputAttack_"+i,(_inputAttack==i?"● ":"")+"攻击 "+(i+1),40+i*350,715,334,54,()=>_inputAttack=n);}
        }
    }
}
