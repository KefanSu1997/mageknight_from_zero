using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _journeyHand;
        private int _journeyHandPage;
        private DeedCard _journeyCard;
        private string _journeyChoice;
        private readonly List<UnitState> _journeyUnitTargets = new();
        private readonly List<DeedCard> _endTurnDiscards = new();

        private void OpenJourneyHand()
        {
            _journeyHand=true;_journeyTools=false;_journeyCard=null;_journeyChoice=null;
            _journeyHandPage=0;_journeyUnitTargets.Clear();
        }
        private void DrawJourneyHand()
        {
            var panel=W.Panel(_root,"JourneyHand",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            var player=_journey.Player;var ctx=_journey.Context;
            _endTurnDiscards.RemoveAll(c=>!player.Deck.Hand.Any(h=>ReferenceEquals(h,c)));
            W.Text(panel,"Title",HeadbandPaymentSelected?"":"手牌与恢复 · 战斗外",40,25,1810,55,36,W.Gold);
            if(ctx.MysticBox is {Used:false} pendingBox)Button(panel,"Btn_PendingBox","继续处理宝盒",410,25,300,55,()=>OpenSpecialArtifact(pendingBox.Box));
            Button(panel,"Btn_Support","休息 / 技能 / 卡牌",740,25,330,55,()=>
            {_journeySupport=true;_supportPage=0;_restOrdinary=null;_restWounds.Clear();_outsideSkill=null;});
            Button(panel,"Btn_OwnedUnits","部队能力",1090,25,165,55,OpenOwnedUnits);
            Button(panel,"Btn_DiscardAtEnd",$"回合末弃掉所选牌 · 已选{_endTurnDiscards.Count}",1280,25,590,55,()=>
            {
                int index=_endTurnDiscards.FindIndex(c=>ReferenceEquals(c,_journeyCard));
                if(index>=0)_endTurnDiscards.RemoveAt(index);else _endTurnDiscards.Add(_journeyCard);
            }).interactable=_journeyCard!=null && _journeyCard.Type!=CardType.Wound;
            W.Text(panel,"Resources",$"手牌 {player.Deck.Hand.Count} · 牌库 {player.Deck.DrawPileCount} · 影响力 {ctx.InfluencePool} · 治疗点 {ctx.Healing.Total} · 手牌创伤 {player.Wounds}\n"+
                string.Join("   ",new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}.Select(c=>ColorName(c)+" 晶体"+player.Mana.Crystals.GetValueOrDefault(c)+" / 魔力"+(ctx.InfiniteMana.Contains(c)?"∞":player.Mana.Tokens.GetValueOrDefault(c).ToString())))+
                $"\n金色魔力 {player.Mana.Tokens.GetValueOrDefault(ManaColor.Gold)} · 黑色魔力 "+(ctx.InfiniteMana.Contains(ManaColor.Black)?"∞":player.Mana.Tokens.GetValueOrDefault(ManaColor.Black).ToString())+
                (ctx.AllowBlackAtDay?" · 本回合允许白昼使用黑色":"")+(ctx.AllowGoldAtNight?" · 本回合允许黑夜使用金色":"")+
                (ctx.LastManaRolls.Count>0?" · 实际骰面："+string.Join("、",ctx.LastManaRolls.Select(ColorName)):""),40,89,1810,95,22,W.Cream);
            var cards=player.Deck.Hand.ToArray();
            _journeyHandPage=Math.Min(_journeyHandPage,Math.Max(0,(cards.Length-1)/6));
            for(int slot=0;slot<6 && _journeyHandPage*6+slot<cards.Length;slot++)
            {
                int index=_journeyHandPage*6+slot;var card=cards[index];
                var b=Button(panel,"Btn_Hand_"+index,"",40+slot%3*309,190+slot/3*300,288,284,()=>
                {
                    _journeyCard=card;_journeyChoice=_journey.OutsideChoices(card).FirstOrDefault(c=>!ctx.NextCardEnhanced || card.Type==CardType.Spell || c.Enhanced)?.Id ?? _journey.OutsideChoices(card).FirstOrDefault()?.Id;
                    _journeyUnitTargets.Clear();ResetArtifactInputs();
                });
                var art=CardArtwork(card);
                if(art!=null)W.Sprite(b.transform,"OriginalCard_"+card.Id,art,66,8,156,225);
                else W.Text(b.transform,"Wound","创伤\n\n"+(ctx.SidewaysWoundValue>0?"可横置为"+ctx.SidewaysWoundValue:"不能正常打出"),24,61,240,145,24,new Color32(225,140,140,255),TextAlignmentOptions.Center);
                W.Text(b.transform,"CardName",(_endTurnDiscards.Any(c=>ReferenceEquals(c,card))?"待弃 · ":ReferenceEquals(card,_journeyCard)?"● ":"")+CardName(card),9,239,270,39,23,W.Cream,TextAlignmentOptions.Center);
            }
            Button(panel,"Btn_PreviousHand","上一页",40,801,280,49,()=>_journeyHandPage--).interactable=_journeyHandPage>0;
            Button(panel,"Btn_NextHand","下一页",658,801,288,49,()=>_journeyHandPage++).interactable=(_journeyHandPage+1)*6<cards.Length;
            W.Text(panel,"HandPage",(_journeyHandPage+1)+" / "+Math.Max(1,(cards.Length+5)/6),347,853,270,20,14,W.Cream,TextAlignmentOptions.Center);
            string rule="选择一张原手牌，再选原效果。治疗消费与部队重整分别操作。";
            if(_journeyCard!=null && Binding(_journeyCard)?.source!=null)
            {
                var selected=_journey.OutsideChoices(_journeyCard).FirstOrDefault(c=>c.Id==_journeyChoice);
                var binding=Binding(_journeyCard);
                if(binding.source is SpellCardSO spell)
                {
                    var half=selected?.Enhanced==true?spell.Bottom:spell.Top;
                    rule=half.Name+"："+half.Effect+"\n印刷费用："+string.Join("、",half.ManaCost.Select(ColorName));
                }
                else if(binding.source is ActionCardSO action)
                    rule=action.NameCn+"："+(selected?.Enhanced==true?action.EnhancedEffect:action.BaseEffect);
                else if(binding.source is ItemCardSO artifact)
                    rule=artifact.NameCn+"："+(_artifactBurn?artifact.OncePerRound:artifact.AssignEffect)+"\n"+(_artifactBurn?"焚毁后永久移除。":"普通效果保留实际神器实体。");
            }
            var details=W.Text(panel,"OriginalRule",rule,1010,190,860,164,23,W.Cream);
            details.enableAutoSizing=true;details.fontSizeMin=18;details.fontSizeMax=23;
            var choices=_journeyCard==null?Array.Empty<MK.Logic.Runtime.Battles.BattleCardChoice>():_journey.OutsideChoices(_journeyCard);
            bool artifactSelected=_journeyCard?.Type==CardType.Artifact;
            if(travelRoute!=null)
            {
                var target=Button(panel,"Btn_SelectTravelTarget","地图目标："+TravelTargetLabel(),333,801,309,49,OpenJourneyMap);
                var label=target.GetComponentInChildren<TextMeshProUGUI>();label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=21;
            }
            if(!artifactSelected) choices=DrawCardMode(panel,choices,_journeyChoice,id=>_journeyChoice=id,1010,366,860,"Btn_OutsideChoice_");
            for(int i=0;i<choices.Length;i++)
            {
                var choice=choices[i];if(choice.Id=="base" || choice.Id=="strong")continue;var b=Button(panel,"Btn_OutsideChoice_"+choice.Id,(_journeyChoice==choice.Id?"● ":"")+choice.Label,
                    1010+i%2*438,419+i/2*47,422,43,()=>_journeyChoice=choice.Id);
                var label=b.GetComponentInChildren<TextMeshProUGUI>();label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=21;
            }
            if(artifactSelected)
            {
                Button(panel,"Btn_ArtifactNormal",(!_artifactBurn?"● ":"")+"普通效果",1010,366,422,43,()=>_artifactBurn=false);
                Button(panel,"Btn_ArtifactBurn",(_artifactBurn?"● ":"")+"焚毁效果",1448,366,422,43,()=>_artifactBurn=true);
                DrawArtifactColors(panel,_journeyCard,_artifactBurn,1010,420,860,45);
            }
            if(ctx.ProtectionBannerBurned)
                Button(panel,"Btn_ProtectionCleanup",(ctx.RemoveNewWoundsAtTurnEnd?"● ":"")+"回合末去除本回合新伤 · 点击保留/去除",1010,565,860,48,
                    ()=>BannerActions.ChooseProtectionCleanup(ctx,!ctx.RemoveNewWoundsAtTurnEnd));
            else W.Text(panel,"TargetRule",artifactSelected?"旌旗普通效果：点击自有部队作为附着目标。其他效果不选部队。":"点击自有部队作为原效果目标；重生可多选。治疗部队按等级付点。",1010,565,860,54,20,W.Muted);
            if(ctx.LearningModes!=0 && !ctx.LearnUsed)
                Button(panel,"Btn_LearningSupply","学习 · 选择真实供应购牌",1010,510,860,44,OpenLearningSupply);
            for(int i=0;i<player.Units.Count;i++)
            {
                var unit=player.Units[i];var b=Button(panel,"Btn_RecoveryUnit_"+i,(_journeyUnitTargets.Contains(unit)?"● ":"")+unit.Card.NameCn+
                    $" · 等级{unit.Card.Level} · 创伤{unit.Wounds} · "+(unit.IsReady?"就绪":"耗竭"),1010,624+i*49,860,44,()=>
                {
                    if(_journeyUnitTargets.Contains(unit))_journeyUnitTargets.Remove(unit);
                    else {if(_journeyCard?.Id!="magic_005")_journeyUnitTargets.Clear();_journeyUnitTargets.Add(unit);}
                });
                b.GetComponentInChildren<TextMeshProUGUI>().fontSize=20;
            }
            Button(panel,"Btn_PlayOutside",artifactSelected?(_artifactBurn?"确认焚毁":"确认普通效果"):CardModeConfirm(choices,_journeyChoice),40,876,205,57,()=>
            {
                if(PveDeedInputs.Supports(_journeyCard.Id)&&_journeyCard.Id!="magic_005"){OpenDeedInputs(_journeyCard,_journeyChoice,false);return;}
                if(artifactSelected)
                {
                    if(PveArtifactInputs.IsSpecial(_journeyCard.Id)){OpenSpecialArtifact(_journeyCard);return;}
                    if(PveArtifactInputs.IsMaterial(_journeyCard.Id)){OpenMaterialArtifact(_journeyCard);return;}
                    var target=PveArtifactInputs.IsAttachedBanner(_journeyCard.Id) && !_artifactBurn ? _journeyUnitTargets.SingleOrDefault() : null;
                    _journey.PlayOutsideArtifact(_journeyCard,_artifactBurn,target,ArtifactColors(_journeyCard,_artifactBurn));
                }
                else if(_journeyCard.Id=="basic_card_018")
                    WithManaPayment(_journeyCard,()=>_journey.PlayTravelCard(_journeyCard,_journeyChoice,_journeyMapTarget,
                        _journeyMapTarget.HasValue && _journey.Game.Map.Placed.TryGetValue(_journeyMapTarget.Value,out var cell)?cell.Edges[0]:(TerrainType?)null),
                        (physical,payment)=>_journey.PlayTravelWithBorrowedPayment(physical,payment,_journeyCard,_journeyChoice,_journeyMapTarget,
                            _journeyMapTarget.HasValue && _journey.Game.Map.Placed.TryGetValue(_journeyMapTarget.Value,out var cell)?cell.Edges[0]:(TerrainType?)null));
                else WithManaPayment(_journeyCard,()=>_journey.PlayOutsideBattle(_journeyCard,_journeyChoice,_journeyUnitTargets.ToArray()),
                    (physical,payment)=>_journey.PlayOutsideWithBorrowedPayment(physical,payment,_journeyCard,_journeyChoice,null,_journeyUnitTargets.ToArray()));
                _journeyCard=null;_journeyChoice=null;
                if(ctx.TemporalPortalPending)OpenJourneyMap();
            },true).interactable=artifactSelected?(PveArtifactInputs.IsSpecial(_journeyCard.Id)||PveArtifactInputs.IsMaterial(_journeyCard.Id)||_journey.CanPlayOutsideArtifact(_journeyCard,_artifactBurn)):choices.Length>0;
            Button(panel,"Btn_SidewaysMove","横置移动",265,876,205,57,()=>
            {
                _journey.SidewaysMovement(_journeyCard);_journeyCard=null;_journeyChoice=null;
            }).interactable=_journeyCard!=null && (_journeyCard.Type!=CardType.Wound || ctx.SidewaysWoundValue>0) && !ctx.TurnActionCommitted;
            bool paymentButton=!artifactSelected && DrawPaymentButton(panel,_journeyCard,1200,944,670,45);
            Button(panel,"Btn_SidewaysInfluence","横置影响力",1010,800,400,49,()=>
            {_journey.SidewaysInfluence(_journeyCard);_journeyCard=null;_journeyChoice=null;}).interactable=_journeyCard!=null && (_journeyCard.Type!=CardType.Wound || ctx.SidewaysWoundValue>0);
            Button(panel,"Btn_ReadyWithInfluence","按原许可支付影响力重整",1430,800,440,49,()=>
            {
                if(_journeyUnitTargets.Count!=1)throw new InvalidOperationException("请选择一支实际部队");
                _journey.ReadyWithInfluence(_journeyUnitTargets[0]);
            }).interactable=ctx.ReadyInfluencePerLevel>0;
            Button(panel,"Btn_HealHero","治疗英雄一张手牌创伤",490,876,455,57,()=>_journey.HealOutsideBattle()).interactable=ctx.Healing.Total>0;
            Button(panel,"Btn_HealUnit","治疗所选部队一张创伤",1010,876,860,57,()=>
            {
                if(_journeyUnitTargets.Count!=1)throw new InvalidOperationException("请选择一支实际部队作为治疗目标");
                _journey.HealOutsideBattle(_journeyUnitTargets[0]);
            }).interactable=ctx.Healing.Total>0;
            W.Text(panel,"Feedback",_feedback??"治疗不会自动重整；重整不会自动移除创伤。",40,943,paymentButton?1130:1810,44,21,_feedback==null?W.Muted:W.Gold);
            if(optimizedInteractionUi)
            {
                bool allSelected=_endTurnDiscards.Count==cards.Count(c=>c.Type!=CardType.Wound)
                    && cards.Where(c=>c.Type!=CardType.Wound).All(c=>_endTurnDiscards.Any(d=>ReferenceEquals(c,d)));
                Button(panel,"Btn_SelectAllTurnDiscards",allSelected?"全部保留 · 清除选择":"全选非创伤牌 · 跨所有页",40,1001,435,56,
                    ()=>{if(allSelected)_endTurnDiscards.Clear();else SelectAllFluentDiscards();});
                Button(panel,"Btn_EndTurnFromHand",$"按选择结束 · 弃 {_endTurnDiscards.Count} 张",505,1001,435,56,EndJourneyTurn).interactable=CanFluentEndTurn;
                Button(panel,"Btn_DiscardOrdinaryEndFromHand","弃非创伤牌并结束",970,1001,435,56,EndJourneyTurnDiscardingOrdinary,true).interactable=CanFluentEndTurn;
                Button(panel,"Btn_CloseHand","返回当前回合",1435,1001,435,56,()=>{_journeyHand=false;_journeyCard=null;_journeyUnitTargets.Clear();});
            }
            else Button(panel,"Btn_CloseHand","返回当前回合",40,1001,1830,56,()=>{_journeyHand=false;_journeyCard=null;_journeyUnitTargets.Clear();},true);
            if(_journeySupport)DrawJourneySupport();
        }
    }
}
