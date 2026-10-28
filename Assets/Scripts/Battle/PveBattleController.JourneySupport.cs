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
        private bool _journeySupport;
        private int _supportPage;
        private DeedCard _restOrdinary;
        private readonly List<DeedCard> _restWounds=new();
        private SkillCard _outsideSkill;
        private int _outsideOption;
        private UnitState _outsideSkillTarget;
        private int _skillTargetPage;
        private void DrawJourneySupport()
        {
            var player=_journey.Player;var context=_journey.Context;
            _restWounds.RemoveAll(c=>!player.Deck.Hand.Any(h=>ReferenceEquals(h,c)));
            if(!player.Deck.Hand.Any(c=>ReferenceEquals(c,_restOrdinary)))_restOrdinary=null;
            var panel=W.Panel(_root,"JourneySupport",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","休息与已学技能",40,25,1820,55,36,W.Gold);
            if(context.PeacefulMomentAvailable)Button(panel,"Btn_Conversions","宁静时光 · 治疗与重整",1000,25,860,55,()=>_conversionsOpen=true);
            W.Text(panel,"Resources",$"手牌 {player.Deck.Hand.Count} · 手牌创伤 {player.Wounds} · 移动力 {context.MovementPool} · 治疗点 {context.Healing.Total}",40,92,1800,44,24,W.Cream);
            bool unitSkill=_outsideSkill?.SourceId=="hero_skill_pool_002:02";
            bool woundSkill=NeedsSkillWound(_outsideSkill);
            if(woundSkill)DrawSkillWounds(panel,player);
            else if(unitSkill)DrawOutsideSkillUnitTargets(panel);
            else
            {
            var cards=player.Deck.Hand.ToArray();_supportPage=Math.Min(_supportPage,Math.Max(0,(cards.Length-1)/6));
            W.Text(panel,"RestRule","标准休息：选一张非创伤牌，再选任意伤牌。\n只有伤牌：仅选一张伤牌缓慢恢复。伤牌进入弃牌堆。",40,157,900,100,23,W.Cream);
            for(int slot=0;slot<6 && _supportPage*6+slot<cards.Length;slot++)
            {
                int index=_supportPage*6+slot;var card=cards[index];
                bool selected=ReferenceEquals(_restOrdinary,card)||_restWounds.Any(w=>ReferenceEquals(w,card));
                var b=Button(panel,"Btn_RestCard_"+index,"",40+slot%3*306,285+slot/3*244,285,229,()=>
                {
                    if(card.Type!=CardType.Wound)_restOrdinary=ReferenceEquals(_restOrdinary,card)?null:card;
                    else {int i=_restWounds.FindIndex(w=>ReferenceEquals(w,card));if(i<0)_restWounds.Add(card);else _restWounds.RemoveAt(i);}
                },selected);
                var art=CardArtwork(card);
                if(art!=null)W.Sprite(b.transform,"OriginalArt",art,82,8,121,175);
                else W.Text(b.transform,"Wound","创伤",35,68,210,65,34,new Color32(225,140,140,255),TextAlignmentOptions.Center);
                W.Text(b.transform,"Name",(selected?"● ":"")+CardName(card),8,190,270,32,21,W.Cream,TextAlignmentOptions.Center);
            }
            Button(panel,"Btn_RestPrevious","上一页",40,781,285,47,()=>_supportPage--).interactable=_supportPage>0;
            Button(panel,"Btn_RestNext","下一页",652,781,285,47,()=>_supportPage++).interactable=(_supportPage+1)*6<cards.Length;
            Button(panel,"Btn_Rest",context.RestedThisTurn?"本回合已休息":"确认休息",40,850,897,58,()=>
            {
                _journey.Rest(_restOrdinary,_restWounds.ToArray());_restOrdinary=null;_restWounds.Clear();
                _feedback="休息完成：所选牌已进入弃牌堆。仍可使用特殊或治疗效果，结束回合后补牌。";
            },true).interactable=!context.RestedThisTurn && !context.MovedThisTurn && !context.TurnActionCommitted;
            }
            W.Text(panel,"SkillsTitle",$"{player.Name} · 已学技能 {player.Skills.Count}",1000,157,850,45,28,W.Gold);
            for(int i=0;i<player.Skills.Count;i++)
            {
                var skill=player.Skills[i];
                Button(panel,"Btn_OutsideSkill_"+i,(ReferenceEquals(skill,_outsideSkill)?"● ":"")+skill.Id+(SkillActions.IsUsed(player,skill)?" · 已使用":""),1000,215+i*54,860,48,()=>{if(skill.IsInteractive==true){OpenSharedSkills(skill);return;}_outsideSkill=skill;_outsideOption=0;_outsideSkillTarget=null;_skillTargetPage=0;});
            }
            if(_outsideSkill!=null)
            {
                Texture(panel,"OriginalSkillReference",SkillReference(_outsideSkill),1000,506,208,290);
                var text=W.Text(panel,"OriginalRule",_outsideSkill.PrintedRule+(_outsideSkill.SourceId==ChaosSkillRules.SourceId?"\n"+ChaosStateText(player):""),1230,506,630,190,23,W.Cream);
                text.enableAutoSizing=true;text.fontSizeMin=14;text.fontSizeMax=23;
                var choices=_journey.OutsideSkillChoices(_outsideSkill);
                for(int i=0;i<choices.Length;i++)
                {
                    var choice=choices[i];int columns=choices.Length>4?3:choices.Length>2?2:1;
                    var b=Button(panel,"Btn_OutsideOption_"+choice.Option,(_outsideOption==choice.Option?"● ":"")+choice.Label,1230+i%columns*(640/columns),706+i/columns*59,630/columns,54,()=>_outsideOption=choice.Option);
                    var label=b.GetComponentInChildren<TextMeshProUGUI>();label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=22;
                }
                if(choices.Length==0)W.Text(panel,"Unavailable",IsPaymentSkill(_outsideSkill)?"先选择实际原牌或部队，再从本次费用入口选择此技能。":"此技能当前没有可选的战斗外效果。",1230,712,630,72,22,W.Muted);
                Button(panel,"Btn_UseOutsideSkill","确认使用所选技能",1000,850,860,58,()=>
                {
                    if(_outsideSkill.SourceId=="hero_skill_pool_005:06"){_flightSkill=_outsideSkill;_journeySupport=false;OpenJourneyMap();return;}
                    if(_outsideSkill.SourceId=="hero_skill_pool_001:06"){OpenShapeshift(_outsideSkill);return;}
                    if(NeedsSidewaysSkill(_outsideSkill)){OpenSidewaysSkill(_outsideSkill);return;}
                    _journey.UseOutsideSkill(_outsideSkill,_outsideOption,unitSkill?_outsideSkillTarget:null,woundSkill?_skillWound:null);_feedback=_outsideSkill.Id+"：效果已生效，使用次数已记录。";
                },true).interactable=choices.Length>0 && (!SkillActions.IsUsed(player,_outsideSkill) || (_outsideSkill.SourceId=="hero_skill_pool_003:03" || _outsideSkill.SourceId==HeroSocialSkillRules.Disguise) && _outsideOption==100) && (!unitSkill || _outsideSkillTarget!=null) && (!woundSkill || _skillWound!=null);
            }
            W.Text(panel,"Feedback",_feedback??"休息回合不能移动、战斗或交涉。技能使用时机和次数以印刷文字为准。",40,925,1820,66,22,W.Muted);
            Button(panel,"Btn_BackSupport","返回手牌与恢复",40,1001,900,56,()=>_journeySupport=false);
            Button(panel,"Btn_EndSupportTurn","结束回合 / 补牌",970,1001,900,56,EndJourneyTurn,true);
        }

        private void DrawOutsideSkillUnitTargets(Transform panel,string instruction=null)
        {
            var units=_journey.Player.Units.Where(u=>!u.IsDestroyed).ToArray();
            if(!units.Contains(_outsideSkillTarget))_outsideSkillTarget=null;
            _skillTargetPage=Math.Min(_skillTargetPage,Math.Max(0,(units.Length-1)/4));
            W.Text(panel,"UnitTargetRule",instruction??"选择实际自有部队：重整恢复就绪，治疗移除创伤。\n两种效果独立；使用后仍保留同一支部队及本轮技能次数。",40,157,900,100,23,W.Cream);
            for(int slot=0;slot<4 && _skillTargetPage*4+slot<units.Length;slot++)
            {
                int index=_skillTargetPage*4+slot;var unit=units[index];
                var button=Button(panel,"Btn_SkillUnit_"+index,"",40+slot%2*456,285+slot/2*244,435,229,()=>_outsideSkillTarget=unit,ReferenceEquals(unit,_outsideSkillTarget));
                var art=unitCatalog.Single(v=>v.originalId==unit.Card.Id).artwork;
                if(art!=null)W.Sprite(button.transform,"OriginalUnitArt",art,8,8,150,210);
                var label=W.Text(button.transform,"UnitState",unit.Card.NameCn+"\n等级 "+unit.Card.Level+"\n"+(unit.IsReady?"就绪":"耗竭")+" · 创伤 "+unit.Wounds,173,15,248,195,23,W.Cream);
                label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=23;
            }
            Button(panel,"Btn_SkillUnitsPrevious","上一页部队",40,810,435,55,()=>_skillTargetPage--).interactable=_skillTargetPage>0;
            Button(panel,"Btn_SkillUnitsNext","下一页部队",496,810,435,55,()=>_skillTargetPage++).interactable=(_skillTargetPage+1)*4<units.Length;
        }
    }
}
