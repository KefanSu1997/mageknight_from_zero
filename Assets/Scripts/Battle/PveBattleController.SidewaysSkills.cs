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
        private SkillCard _sidewaysSkill;
        private DeedCard _sidewaysSkillCard;
        private int _sidewaysSkillPage;
        private SidewaysType _sidewaysSkillType;
        private ManaColor _sidewaysSkillColor;
        private bool _sidewaysForgoSource;
        private static bool NeedsSidewaysSkill(SkillCard skill) => skill?.SourceId is "hero_skill_pool_005:07" or "hero_skill_pool_007:06" or "hero_skill_pool_007:07";
        private void OpenSidewaysSkill(SkillCard skill)
        {
            _sidewaysSkill=skill;_sidewaysSkillCard=null;_sidewaysSkillPage=0;
            _sidewaysSkillColor=ManaColor.Red;
            _sidewaysForgoSource=false;
            _sidewaysSkillType=_battle?.Context.InBattle==true
                ? (_battle.Phase==Phase.Block?SidewaysType.Block:SidewaysType.Attack) : SidewaysType.Move;
        }
        private void DrawSidewaysSkill()
        {
            var player=_journey?.Player??_battle.Player;
            bool inBattle=(_journey?.Context??_battle.Context).InBattle;
            bool universal=_sidewaysSkill.SourceId=="hero_skill_pool_005:07";
            bool repel=_sidewaysSkill.SourceId=="hero_skill_pool_007:07";
            bool borrowed=IsHeadbandInput(_sidewaysSkill);
            bool pain=borrowed && HeadbandBorrowResolution.IsPain(_sidewaysSkill);
            var cards=player.Deck.Hand.Where(c=>(pain?c.Type==CardType.Wound:c.Type!=CardType.Wound) && (!borrowed || !ReferenceEquals(c,_headbandPhysical))).ToArray();
            if(!cards.Any(c=>ReferenceEquals(c,_sidewaysSkillCard)))_sidewaysSkillCard=null;
            _sidewaysSkillPage=Math.Min(_sidewaysSkillPage,Math.Max(0,(cards.Length-1)/6));
            var panel=W.Panel(_root,"SidewaysSkill",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title",_sidewaysSkill.Id+" · 选择一张实际手牌",40,25,1800,70,36,W.Gold);
            W.Text(panel,"Instruction",borrowed?HeadbandInputInstruction():"只对所选原牌生效；确认后该牌进入已打出区，并记录本回合技能次数。",40,105,1820,65,24,W.Cream);
            for(int slot=0;slot<6 && _sidewaysSkillPage*6+slot<cards.Length;slot++)
            {
                int index=_sidewaysSkillPage*6+slot;var card=cards[index];
                var b=Button(panel,"Btn_Card_"+index,"",40+slot%3*306,190+slot/3*280,285,263,
                    ()=>_sidewaysSkillCard=card,ReferenceEquals(card,_sidewaysSkillCard));
                var art=CardArtwork(card);
                if(art!=null)W.Sprite(b.transform,"OriginalArt",art,70,8,145,210);
                W.Text(b.transform,"Name",CardName(card),8,226,270,32,21,W.Cream,TextAlignmentOptions.Center);
            }
            Button(panel,"Btn_Previous","上一页手牌",40,768,435,55,()=>_sidewaysSkillPage--).interactable=_sidewaysSkillPage>0;
            Button(panel,"Btn_Next","下一页手牌",496,768,435,55,()=>_sidewaysSkillPage++).interactable=(_sidewaysSkillPage+1)*6<cards.Length;
            Texture(panel,"OriginalSkillReference",SkillReference(_sidewaysSkill),1000,190,230,322);
            W.Text(panel,"OriginalRule",_sidewaysSkill.PrintedRule,1260,190,580,322,24,W.Cream);
            if(!inBattle)
            {
                Button(panel,"Btn_Move","横置为移动",1000,547,408,57,()=>_sidewaysSkillType=SidewaysType.Move,_sidewaysSkillType==SidewaysType.Move);
                Button(panel,"Btn_Influence","横置为影响力",1430,547,408,57,()=>_sidewaysSkillType=SidewaysType.Influence,_sidewaysSkillType==SidewaysType.Influence);
            }
            else W.Text(panel,"Direction",_sidewaysSkillType==SidewaysType.Block?"本阶段：横置为普通格挡":"本阶段：横置为普通攻击",1000,547,840,57,26,W.Gold);
            if(universal)
            {
                W.Text(panel,"Cost","支付一点所选颜色的实际魔力",1000,627,840,44,23,W.Cream);
                var names=new[]{"红","蓝","绿","白","金","黑"};
                for(int i=0;i<6;i++)
                {
                    var color=(ManaColor)i;
                    Button(panel,"Btn_Color_"+i,names[i],1000+i%3*280,686+i/3*65,264,57,()=>_sidewaysSkillColor=color,_sidewaysSkillColor==color);
                }
            }
            if(repel)
            {
                bool used=(_journey?.Context??_battle.Context).ManaSource?.WasUsedThisTurn(player)==true;
                W.Text(panel,"SourceHistory",used?"本回合已取用魔力源骰子，只能选择效果2。":"效果3要求整个回合不取用魔力源骰子。",1000,627,840,60,23,W.Cream);
                Button(panel,"Btn_Value2","效果2 · 保留取骰选择",1000,706,408,70,()=>_sidewaysForgoSource=false,!_sidewaysForgoSource);
                Button(panel,"Btn_Value3","效果3 · 本回合不取源骰",1430,706,408,70,()=>_sidewaysForgoSource=true,_sidewaysForgoSource).interactable=!used;
            }
            W.Text(panel,"Feedback",_feedback??(_sidewaysSkillCard==null?(pain?"请选择实际伤牌，本次横置为2；成功后该伤牌进入已打出区。":"请选择实际非创伤手牌。"):"已选："+CardName(_sidewaysSkillCard)),40,borrowed?882:855,1800,65,24,W.Cream);
            if(borrowed)DrawHeadbandFinish(panel);
            Button(panel,"Btn_Back","返回技能",40,990,880,60,()=>{_sidewaysSkill=null;if(borrowed)ClearHeadbandInput();})
                .interactable=!borrowed || _journey.Context.PendingHeadbandBorrow==null;
            Button(panel,"Btn_Confirm","确认横置所选原牌",960,990,880,60,()=>
            {
                ManaColor? color=universal?_sidewaysSkillColor:null;
                if(borrowed)
                {
                    if(pain)_journey.UseHeadbandPain(_headbandPhysical,_headbandSkill,_sidewaysSkillCard,_sidewaysSkillType);
                    else _journey.UseHeadbandSideways(_headbandPhysical,_headbandSkill,_sidewaysSkillCard,_sidewaysSkillType,color,_sidewaysForgoSource);
                    AfterHeadbandInput();return;
                }
                if(inBattle)_battle.PlaySidewaysWithSkill(_sidewaysSkill,_sidewaysSkillCard,color,_sidewaysForgoSource);
                else _journey.PlaySidewaysWithSkill(_sidewaysSkill,_sidewaysSkillCard,_sidewaysSkillType,color,_sidewaysForgoSource);
                _sidewaysSkill=null;_sidewaysSkillCard=null;
            },true).interactable=_sidewaysSkillCard!=null && (borrowed || !SkillActions.IsUsed(player,_sidewaysSkill));
        }
    }
}
