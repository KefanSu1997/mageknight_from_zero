using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using TMPro;
using UnityEngine;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        public bool optimizedInteractionUi = false;
        private bool _fluentSourceOpen, _fluentDiscardMode, _fluentExtraSource;
        private int _fluentSourcePage;
        public void SetUiDesignPreview(bool optimized)
        {
            optimizedInteractionUi=optimized;
            _fluentDiscardMode=false;
            _fluentSourceOpen=optimized && _journey!=null;
            _fluentExtraSource=false;
            _journeyTools=_journeyHand=_battleMenuOpen=false;
            Draw();
        }
        private void OpenFluentManaTools()
        {
            _battleMenuOpen=false;
            if(!optimizedInteractionUi){_journeyTools=true;return;}
            _fluentSourceOpen=!_fluentSourceOpen;
            _fluentSourcePage=0;
        }
        private void CloseFluentManaTools(){_journeyTools=false;_quickSourceReturn=false;_battleSourceOpen=false;_fluentSourceOpen=optimizedInteractionUi;_tacticDice.Clear();}
        private void OpenFluentSupport()
        {
            OpenJourneyHand();
            _journeySupport=true;_supportPage=0;_restOrdinary=null;_restWounds.Clear();_outsideSkill=null;
        }
        private void OpenFluentDiscardPlan()
        {
            _fluentDiscardMode=!_fluentDiscardMode;
            _showUnits=_showSkills=false;
            _selected=null;_selectedUnit=null;_selectedSkill=null;_choice=null;
            _selectMaterials=false;_recovery=_bowPanel=false;
        }
        private void SelectAllFluentDiscards()
        {
            _endTurnDiscards.Clear();
            _endTurnDiscards.AddRange(_journey.Player.Deck.Hand.Where(c=>c.Type!=CardType.Wound));
        }
        private bool CanFluentEndTurn => _journey!=null && !_journey.Context.InBattle
            && !_journey.Context.TunnelAssaultActive
            && !(_journey.Context.Dungeon?.Reward==DungeonRewardKind.Artifact && _journey.Context.Dungeon.SelectedArtifact==null)
            && !(_journey.Context.PendingSpellReward && _journey.Context.SelectedSpellReward==null);
        private void EndJourneyTurnDiscardingOrdinary()
        {
            if(!CanFluentEndTurn)throw new InvalidOperationException("请先完成战斗或地点奖励。");
            SelectAllFluentDiscards();
            EndJourneyTurn();
        }
        private void ToggleDamageUnitPlan(UnitState unit)
        {
            if(_battle.Phase!=Phase.AssignDamage || _targets.Count!=1)
                throw new InvalidOperationException("请先选择一个敌人的攻击。");
            if(!UnitParticipation.CanUse(_battle.Player,_battle.Context,unit))
                throw new InvalidOperationException("此地点不允许这支部队参与。");
            int target=_targets.Single();
            var units=_battle.DamagePlan.FirstOrDefault(a=>a.TargetIndex==target && a.AttackIndex==_attackSlot)?.Units.ToList() ?? new System.Collections.Generic.List<UnitState>();
            if(!units.Remove(unit))units.Add(unit);
            _battle.PlanDamage(target,_attackSlot,units.ToArray());
            _selectedUnit=unit;_selected=null;_choice=null;_recovery=false;_bannerMode=false;_unitUtilityPanel=false;
        }
        private static string ManaShort(ManaColor color)=>color switch
        {ManaColor.Red=>"红",ManaColor.Blue=>"蓝",ManaColor.Green=>"绿",ManaColor.White=>"白",ManaColor.Gold=>"金",_=>"黑"};
        private void DrawFluentSource(bool battleShell)
        {
            if(battleShell && embeddedBattleUi && _battleSourceOpen && _journey!=null)
            {DrawEmbeddedSource();return;}
            if(!optimizedInteractionUi || !_fluentSourceOpen || _journey==null)return;
            float x=battleShell?38:995,y=battleShell?206:108,w=battleShell?405:885,h=battleShell?439:240;
            var p=InformationSurface(_root,"InlineSource",x,y,w,h,new Color32(40,59,52,234),true);
            Button(p,"Btn_SourceTacticDetails","源骰 / 战术详情",16,10,battleShell?220:210,43,
                ()=>{_fluentSourceOpen=false;_journeyTools=true;});
            var dice=_journey.Game.Globals.Mana.Dice;
            int perPage=6;
            _fluentSourcePage=Math.Min(_fluentSourcePage,Math.Max(0,(dice.Count-1)/perPage));
            int pages=Math.Max(1,(dice.Count+perPage-1)/perPage);
            if(pages>1)Button(p,"Btn_SourcePage","翻页",battleShell?248:690,12,82,43,()=>_fluentSourcePage=(_fluentSourcePage+1)%pages);
            Button(p,"Btn_CloseInlineSource","×",w-59,12,43,43,()=>{_fluentSourceOpen=false;_tacticDice.Clear();});
            string selection=_tacticDice.Count==1?ManaShort(_tacticDice[0].Face):"未选";
            string quota="普通："+(_journey.Player.HeldManaDie==null?"未用":"已用")+" · 额外 "+_journey.Context.ExtraManaDice;
            string tactic=_journey.Player.Tactic==null?"无战术":OriginalTactics.Get(_journey.Player.Tactic.Card.Id).Name;
            W.Text(p,"Quota",quota+"\n选："+selection+" · "+tactic+" · "+(_journey.Game.Clock.DayPart==DayPart.Day?"昼":"夜")+_journey.Game.Clock.RoundIndex,
                battleShell?16:230,battleShell?56:10,battleShell?373:440,56,20,W.Cream);
            int columns=battleShell?3:6;
            bool extraMode=_fluentExtraSource || _journey.Player.HeldManaDie!=null;
            for(int slot=0;slot<perPage && _fluentSourcePage*perPage+slot<dice.Count;slot++)
            {
                int index=_fluentSourcePage*perPage+slot;var die=dice[index];
                bool chooseBlack=die.Face==ManaColor.Black && (_journey.Context.BlackManaWild || extraMode&&_journey.Context.ExtraBlackManaDice>0);
                Button(p,"Btn_TakeSource_"+index,(index+1)+(chooseBlack?"选黑":extraMode?"额外":"取")+(chooseBlack?"":ManaShort(die.Face)),16+slot%columns*128,
                    (battleShell?118:66)+slot/columns*58,116,52,()=>
                    {_tacticDice.Clear();_tacticDice.Add(die);if(!chooseBlack)UseJourneySource(extraMode);},_tacticDice.Contains(die))
                    .interactable=extraMode?_journey.Context.ExtraManaDice>0:_journey.Player.HeldManaDie==null;
            }
            bool wild=_tacticDice.Count==1 && _tacticDice[0].Face==ManaColor.Black && (_journey.Context.BlackManaWild || _journey.Context.ExtraBlackManaDice>0);
            if(wild)
            {
                var colors=new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black};
                for(int i=0;i<colors.Length;i++)
                {
                    var color=colors[i];
                    Button(p,"Btn_InlineSourceColor_"+color,ManaShort(color),16+i*60,battleShell?238:184,56,47,()=>_sourceColor=color,_sourceColor==color);
                }
            }
            float actionY=battleShell?292:184,actionX=battleShell?16:400;
            Button(p,"Btn_InlineUseSource",_tacticDice.Count==1?"确认普通取骰":"普通取骰模式",actionX,actionY,174,43,
                ()=>{if(_tacticDice.Count==1)UseJourneySource(false);else _fluentExtraSource=false;},!extraMode)
                .interactable=_journey.Player.HeldManaDie==null;
            Button(p,"Btn_InlineUseExtraSource",_tacticDice.Count==1?"确认额外取骰":"额外取骰模式",actionX+191,actionY,174,43,
                ()=>{if(_tacticDice.Count==1)UseJourneySource(true);else _fluentExtraSource=true;},extraMode)
                .interactable=_journey.Context.ExtraManaDice>0;
            var colorsForSummary=new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black};
            W.Text(p,"SourceResources",string.Join("  ",colorsForSummary.Select(c=>ManaShort(c)+" "+(_journey.Context.InfiniteMana.Contains(c)?"∞":_journey.Player.Mana.Tokens.GetValueOrDefault(c).ToString())))+
                "\n金骰保留金色，付款时选择基本色。",16,battleShell?340:124,battleShell?373:853,58,20,W.Cream);
            if(battleShell)W.Text(p,"SourceHero",$"{_battle.Player.Name} · 等级{_battle.Player.Level} · 护甲{_battle.Player.Armor}",16,399,373,38,22,W.Cream);
        }
        private bool DrawFluentDiscardInspector(RectTransform p)
        {
            if(!optimizedInteractionUi || !_fluentDiscardMode || _journey==null || _battle.Phase!=Phase.End)return false;
            W.Text(p,"Heading","回合末 · 保留与弃牌",25,21,442,49,31,W.Gold);
            W.Text(p,"Details","怪物、战果、资源与原手牌保留在战场。\n点击原手牌切换；金色高亮=待弃。\n创伤不能这样弃掉。\n选择不会结束回合，最后仍需确认。\n影响力 "+_journey.Context.InfluencePool+" · 治疗点 "+_journey.Context.Healing.Total,25,82,441,188,22,W.Cream);
            var pageDiscards=_journey.Player.Deck.Hand.Skip(_handPage*HandPageSize).Take(HandPageSize).Where(c=>_endTurnDiscards.Any(d=>ReferenceEquals(c,d))).ToArray();
            W.Text(p,"DiscardSummary","本页待弃 "+pageDiscards.Length+" / 总 "+_endTurnDiscards.Count+" 张\n"+string.Join("、",pageDiscards.Select(CardName)),25,281,442,130,22,W.Gold);
            Button(p,"Btn_SelectAllTurnDiscards","选择全部非创伤牌",25,447,442,52,SelectAllFluentDiscards);
            Button(p,"Btn_ClearTurnDiscards","全部保留 · 清除选择",25,510,442,52,()=>_endTurnDiscards.Clear());
            Button(p,"Btn_CancelTurnDiscards","返回原行动面板",25,644,442,42,()=>_fluentDiscardMode=false);
            W.Text(p,"Feedback",LastRule,25,575,442,60,22,W.Cream);
            W.Text(p,"Rules","批量选择覆盖所有手牌页；翻页逐张检查仍可用。",25,693,442,74,17,W.Muted);
            Button(p,"Btn_EndTurn","确认当前弃牌选择 · 结束回合",25,770,442,47,EndJourneyTurn,true);
            return true;
        }
    }
}
