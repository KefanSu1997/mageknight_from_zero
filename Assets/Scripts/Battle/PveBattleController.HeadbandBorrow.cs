using System;
using System.Linq;
using System.Collections.Generic;
using MK.Logic.Data;
using MK.Logic.Core;
using MK.Logic.Runtime;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private DeedCard _headbandPhysical;
        private SkillCard _headbandSkill;
        private bool _headbandTargetInput, _headbandSocialInput;
        private ManaColor _headbandColor;
        private bool _headbandHeal;
        private static bool HasDynamicHeadbandInput(SkillCard skill) => HeadbandBorrowResolution.Handles(skill) || HeadbandBorrowResolution.IsPayment(skill);
        private bool IsHeadbandInput(SkillCard skill) => _headbandPhysical!=null && ReferenceEquals(skill,_headbandSkill);
        private void OpenHeadbandInput(DeedCard card,SkillCard skill)
        {
            if((HeadbandBorrowResolution.IsLeadership(skill) || HeadbandBorrowResolution.IsForkedLightning(skill)) && _journey?.Context.InBattle!=true)
                throw new InvalidOperationException("领导须在战斗中选择部队原攻击或格挡能力");
            _headbandPhysical=card;_headbandSkill=skill;
            if(HeadbandBorrowResolution.IsPayment(skill))
            {
                _specialCard=null;_selected=null;_journeyCard=null;
                ClearManaPayment();
                if(_journey.Context.InBattle){_showUnits=false;_showSkills=false;}
                else OpenJourneyHand();
            }
            else if(HeadbandBorrowResolution.IsFlip(skill) && _journey.Context.PendingHeadbandBorrow==null)
            {_headbandFlipInput=true;_publicSkillOption=0;}
            else if(skill.SourceId==DuelActions.SourceId)
            {
                if(_battle==null || _battle.Phase!=Phase.Block)throw new InvalidOperationException("头环决斗须在格挡阶段选择实际攻击");
                _headbandDuelInput=true;_headbandDuelEnemy=-1;_headbandDuelAttack=0;_headbandDuelPage=0;
            }
            else if(HeadbandBorrowResolution.IsSocial(skill)) { _headbandSocialInput=true; _publicSkillOption=0; }
            else if(HeadbandBorrowResolution.IsShapeshift(skill))OpenShapeshift(skill);
            else if(HeadbandBorrowResolution.IsForkedLightning(skill) || HeadbandBorrowResolution.IsPuppet(skill))OpenEnemyTokenSkill(skill);
            else if(HeadbandBorrowResolution.IsFlight(skill))
            {
                _flightSkill=skill;_specialCard=null;_journeyCard=null;_selected=null;_journeyHand=false;
                OpenJourneyMap();
            }
            else if(HeadbandBorrowResolution.IsLeadership(skill))
            {
                _leadershipSkill=skill;_showUnits=true;_showSkills=false;_selectedSkill=null;
                _specialCard=null;_journeyCard=null;_selected=null;_journeyHand=false;_selectedUnit=null;
            }
            else if(HeadbandBorrowResolution.IsWound(skill) || HeadbandBorrowResolution.IsUnit(skill))
            {
                _headbandTargetInput=true;_skillWound=null;_skillWoundPage=0;
                _outsideSkillTarget=null;_skillTargetPage=0;_headbandColor=ManaColor.Red;_headbandHeal=false;
            }
            else if(skill.SourceId=="hero_skill_pool_004:04")OpenContributionSkill(skill);
            else OpenSidewaysSkill(skill);
        }
        private void PrepareHeadbandInput()
        {
            var pending=_journey?.Context?.PendingHeadbandBorrow;
            if(pending==null || HeadbandPaymentSelected)return;
            if(!_headbandFlipInput && !_headbandDuelInput && !_headbandSocialInput && !_headbandTargetInput && _sidewaysSkill==null && _contributionSkill==null && _leadershipSkill==null && _shapeSkill==null && _enemyTokenSkill==null && _flightSkill==null)OpenHeadbandInput(pending.PhysicalCard,pending.Skill);
        }
        private void ClearHeadbandInput()
        {
            if(IsHeadbandInput(_leadershipSkill))_leadershipSkill=null;
            if(IsHeadbandInput(_shapeSkill))_shapeSkill=null;
            if(IsHeadbandInput(_enemyTokenSkill))_enemyTokenSkill=null;
            if(IsHeadbandInput(_flightSkill))_flightSkill=null;
            _headbandPhysical=null;_headbandSkill=null;_sidewaysSkill=null;_contributionSkill=null;
            _headbandFlipInput=false;_headbandDuelInput=false;_headbandSocialInput=false;_headbandTargetInput=false;_skillWound=null;_outsideSkillTarget=null;
        }
        private void AfterHeadbandInput()
        {
            _specialCard=null;_journeyCard=null;_selected=null;
            _sidewaysSkill=null;_sidewaysSkillCard=null;_contributionSkill=null;_skillAttack=null;
            _leadershipSkill=null;
            _shapeSkill=null;_shapeCard=null;_shapeMaterial=null;_enemyTokenSkill=null;_skillEnemies.Clear();_flightSkill=null;
            _headbandFlipInput=false;_headbandDuelInput=false;_headbandSocialInput=false;_headbandTargetInput=false;_skillWound=null;_outsideSkillTarget=null;
            if(_journey.Context.PendingHeadbandBorrow==null)ClearHeadbandInput();
        }
        private string HeadbandInputInstruction()
        {
            var pending=_journey.Context.PendingHeadbandBorrow;
            return "启迪头环 · 第"+((pending?.Completed??0)+1)+"次借用；选择本次实际输入。"+
                (pending==null?"首次成功后头环进入已打出区。":"已完成的效果保留；个人技能次数不变。");
        }
        private void DrawHeadbandFinish(Transform panel)
        {
            if(_journey.Context.PendingHeadbandBorrow==null)return;
            Button(panel,"Btn_FinishUnavailable","没有合法输入时结束剩余借用",1000,823,840,47,()=>
            {
                _journey.FinishHeadbandBorrowIfImpossible();AfterHeadbandInput();
            });
        }
        private void DrawHeadbandPaymentStatus()
        {
            if(!HeadbandPaymentSelected || _manaPaymentOpen)return;
            var pending=_journey.Context.PendingHeadbandBorrow;
            Button(_root,"Btn_HeadbandPaymentStatus","头环 · "+_headbandSkill.Id+" · 第"+((pending?.Completed??0)+1)+"次 · 选牌与付款"+(pending==null?" · 取消":" · 检查余次"),40,(_journeyHand||_ownedUnitsOpen)?25:192,(_journeyHand||_ownedUnitsOpen)?650:1310,55,()=>
            {
                if(pending==null){ClearManaPayment();ClearHeadbandInput();}
                else {_journey.FinishHeadbandBorrowIfImpossible();ClearManaPayment();AfterHeadbandInput();}
            });
        }
        private void DrawHeadbandSocialInput()
        {
            if (!_headbandSocialInput || _headbandSkill == null) return;
            var panel=W.Panel(_root,"HeadbandSocial",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","启迪头环 · "+_headbandSkill.Id,40,25,1800,65,36,W.Gold);
            W.Text(panel,"Instruction",HeadbandInputInstruction(),40,110,1800,65,24,W.Cream);
            Texture(panel,"OriginalSkillReference",SkillReference(_headbandSkill),40,210,300,420);
            W.Text(panel,"OriginalRule",_headbandSkill.PrintedRule,400,210,1430,250,26,W.Cream);
            Button(panel,"Btn_Option_0",_headbandSkill.SourceId==HeroSocialSkillRules.LoneWolf?"影响力 1":"影响力 2",400,510,690,65,()=>_publicSkillOption=0,_publicSkillOption==0);
            if (_headbandSkill.SourceId==HeroSocialSkillRules.LoneWolf)
                Button(panel,"Btn_Option_1","影响力 3 · 本回合不以影响力招募",1120,510,710,65,()=>_publicSkillOption=1,_publicSkillOption==1);
            else W.Text(panel,"Scope","翻面借用尚未接入；当前可借用原影响力效果。",400,600,1430,65,24,W.Muted);
            W.Text(panel,"ActualInfluence","实际影响力："+_journey.Context.InfluencePool,400,700,1430,65,28,W.Gold);
            DrawHeadbandFinish(panel);
            W.Text(panel,"Feedback",_feedback??"两次各自选择；完成后按实际交涉费用消费。",40,889,1800,70,23,W.Cream);
            Button(panel,"Btn_Back","返回原头环",40,990,880,60,ClearHeadbandInput).interactable=_journey.Context.PendingHeadbandBorrow==null;
            Button(panel,"Btn_Confirm","确认本次原效果",960,990,880,60,()=>{_journey.UseHeadbandSocial(_headbandPhysical,_headbandSkill,_publicSkillOption);AfterHeadbandInput();},true);
        }
        private void DrawHeadbandTargetInput()
        {
            if(!_headbandTargetInput || _headbandSkill==null)return;
            var player=_journey.Player;
            bool unit=HeadbandBorrowResolution.IsUnit(_headbandSkill);
            bool regeneration=NeedsSkillWound(_headbandSkill);
            var panel=W.Panel(_root,"HeadbandTarget",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","启迪头环 · "+_headbandSkill.Id,40,25,1800,65,36,W.Gold);
            W.Text(panel,"Progress",HeadbandInputInstruction(),40,100,1800,50,24,W.Cream);
            if(unit)DrawOutsideSkillUnitTargets(panel,"选择实际自有部队：重整恢复就绪，治疗移除一张创伤。\n每轮技能通过头环仅借用一次；不改变个人技能次数。");
            else DrawSkillWounds(panel,player,regeneration?null:"选择一张实际手牌创伤：弃到本人弃牌堆，再从实际牌库抽一张。\n无惧痛苦只能在战斗外使用，不需要魔力。");
            Texture(panel,"OriginalSkillReference",SkillReference(_headbandSkill),1000,190,240,336);
            W.Text(panel,"OriginalRule",_headbandSkill.PrintedRule,1270,190,580,336,24,W.Cream);
            W.Text(panel,"Resources",$"手牌创伤 {player.Wounds} · 牌库 {player.Deck.DrawPileCount}\n"+
                string.Join("  ",new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}.Select(c=>ColorName(c)+" 晶"+player.Mana.Crystals.GetValueOrDefault(c)+"/标"+player.Mana.Tokens.GetValueOrDefault(c))),1000,550,840,100,22,W.Cream);
            if(regeneration)
                for(int i=0;i<6;i++)
                {
                    var color=(ManaColor)i;
                    Button(panel,"Btn_Color_"+i,ColorName(color),1000+i%3*280,670+i/3*65,264,57,()=>_headbandColor=color,_headbandColor==color);
                }
            if(unit)
            {
                Button(panel,"Btn_Ready","重整 · 保留创伤",1000,700,408,60,()=>_headbandHeal=false,!_headbandHeal);
                Button(panel,"Btn_Heal","治疗 · 不自动重整",1430,700,408,60,()=>_headbandHeal=true,_headbandHeal);
            }
            DrawHeadbandFinish(panel);
            W.Text(panel,"Feedback",_feedback??"请选择本次实际目标，再确认原效果。",40,890,1800,70,24,W.Cream);
            Button(panel,"Btn_Back","返回原头环",40,990,880,60,ClearHeadbandInput)
                .interactable=_journey.Context.PendingHeadbandBorrow==null;
            Button(panel,"Btn_Confirm","确认本次借用",960,990,880,60,()=>
            {
                if(unit)_journey.UseHeadbandUnit(_headbandPhysical,_headbandSkill,_outsideSkillTarget,_headbandHeal);
                else _journey.UseHeadbandWound(_headbandPhysical,_headbandSkill,_skillWound,regeneration?(ManaColor?)_headbandColor:null);
                AfterHeadbandInput();
            },true).interactable=unit?_outsideSkillTarget!=null:_skillWound!=null;
        }
    }
}
