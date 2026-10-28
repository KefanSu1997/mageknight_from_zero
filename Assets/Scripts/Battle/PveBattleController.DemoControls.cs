using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Battles;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private readonly Dictionary<Phase, DemoStateCheckpoint> _phaseStarts = new();
        private readonly Dictionary<Phase, (MK.Logic.Data.AttackProfile Part, string Label)[]> _phaseSourceLabels = new();
        private PveBattleSession _checkpointBattle;
        private bool _battleMenuOpen;
        private bool _quickSourceReturn;
        private string _checkpointError;
        private bool CanResetBattle => _battle != null && (_journey == null || _journey.Game.HasActiveTurn);

        private void RememberDemoPhase()
        {
            if (!CanResetBattle) { _phaseStarts.Clear(); _phaseSourceLabels.Clear(); _checkpointBattle=null; return; }
            if (!ReferenceEquals(_checkpointBattle, _battle))
            {
                _phaseStarts.Clear(); _phaseSourceLabels.Clear(); _checkpointBattle=_battle; _checkpointError=null;
            }
            if (_battle.Phase == Phase.None || _battle.Phase == Phase.End || _phaseStarts.ContainsKey(_battle.Phase)) return;
            try
            {
                _phaseStarts.Add(_battle.Phase, DemoStateCheckpoint.Capture(_journey, _battle));
                _phaseSourceLabels[_battle.Phase]=_skillAttackLabels.ToArray();
            }
            catch (NotSupportedException e) { _checkpointError=e.Message; }
        }

        private void ResetDemoPhase(Phase phase)
        {
            if (!CanResetBattle || !_phaseStarts.TryGetValue(phase, out var start))
                throw new InvalidOperationException(_checkpointError ?? "本阶段没有可恢复的检查点。");
            start.Restore();
            foreach (var later in _phaseStarts.Keys.Where(p => p > phase).ToArray()) _phaseStarts.Remove(later);
            ClearJourneyChoices(); ResetArtifactInputs(); ClearManaPayment();
            _skillAttackLabels.Clear(); _skillAttackLabels.AddRange(_phaseSourceLabels[phase]); _skillWound=null; _skillWoundPicker=false;
            _attackSlot=0; _skillPage=0; _deedChoicePage=0; _rules=false; _battleMenuOpen=false;
            _journeyTools=false; _camp=false; _unitUtilityPanel=false; _bannerMode=false;
            _siteRewardOpen=false; _hornBonus=0; _shieldSplit=false;
            _feedback="已恢复"+DemoPhaseName(phase)+"阶段开始时的实际状态。";
        }

        private static string DemoPhaseName(Phase phase) => phase switch
        {
            Phase.Ranged => "远程 / 攻城", Phase.Block => "格挡",
            Phase.AssignDamage => "承伤分配", Phase.Melee => "近战", _ => "战果"
        };

        private void DrawDemoControls()
        {
            var phase=_battle.Phase==Phase.End?Phase.Melee:_battle.Phase;
            if(optimizedInteractionUi && _journey!=null)
                Button(_root,"Btn_QuickMana","公共魔力 / 战术",1430,48,215,52,OpenFluentManaTools);
            else
                Button(_root,"Btn_ResetPhase",_battle.Phase==Phase.End?"重打近战":"重置本阶段",1430,48,215,52,
                    ()=>ResetDemoPhase(phase)).interactable=CanResetBattle && _phaseStarts.ContainsKey(phase);
            Button(_root,"Btn_Menu","菜单",1670,48,210,52,()=>_battleMenuOpen=!_battleMenuOpen);
            // Direct access reuses the existing source screen. Keep reset,
            // menu, stage strip and all information at their original sizes.
            if(!optimizedInteractionUi && _journey!=null && _battle.Phase!=Phase.End)
                Button(_root,"Btn_QuickSource","公共魔力",1430,130,215,52,
                    ()=>{if(embeddedBattleUi)OpenEmbeddedSource();else {_battleMenuOpen=false;_quickSourceReturn=true;_journeyTools=true;_fluentSourceOpen=false;}});
            if(optimizedInteractionUi && _journey!=null && _battle.Phase==Phase.End)
            {
                if(CanFluentEndTurn)
                {
                    Button(_root,"Btn_EndTurnDiscardOrdinary","弃非创伤并结束",1430,139,215,43,EndJourneyTurnDiscardingOrdinary);
                    Button(_root,"Btn_PlanEndTurnDiscards","逐张保留 / 弃牌",1670,139,210,43,OpenFluentDiscardPlan);
                }
                else Button(_root,"Btn_PlanEndTurnDiscards","回合末 · 保留 / 弃牌",1430,139,450,43,OpenFluentDiscardPlan);
            }
        }

        private void DrawBattleMenu()
        {
            var shield=W.Panel(_root,"BattleMenuShield",0,0,1920,1080,new Color(0,0,0,.8f));
            shield.GetComponent<Image>().raycastTarget=true;
            W.Panel(_root,"BattleMenuSurface",1170,170,710,845,new Color(.025f,.065f,.06f,1));
            W.Text(_root,"BattleMenuTitle","战斗菜单 · 演练悔棋",1200,195,650,55,30,W.Gold);
            Button(_root,"Btn_Camp","重新配置出战",1200,270,650,55,()=>{_battleMenuOpen=false;OpenCamp();});
            if(_journey!=null) Button(_root,"Btn_JourneyTools","魔力源 / 战术",1200,340,650,55,
                ()=>{_battleMenuOpen=false;_quickSourceReturn=false;_journeyTools=true;_fluentSourceOpen=false;});
            Button(_root,"Btn_PhaseRules","查看本阶段规则",1200,410,650,55,()=>{_battleMenuOpen=false;_rules=true;});
            W.Text(_root,"DemoResetHelp","回到本场战斗已到过的阶段起点：\n恢复原牌、资源、敌人、部队和使用次数。",1200,485,650,85,22,W.Cream);
            if(_checkpointError!=null) W.Text(_root,"DemoResetError",_checkpointError,1200,830,650,60,17,W.Cream);
            foreach(var p in new[]{Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee})
            {
                var target=p;
                Button(_root,"Btn_Reset_"+p,"重置 · "+DemoPhaseName(p),1200,590+((int)p-1)*65,650,52,
                    ()=>ResetDemoPhase(target)).interactable=CanResetBattle && _phaseStarts.ContainsKey(p);
            }
            Button(_root,"Btn_CloseBattleMenu","返回战斗",1200,900,650,60,()=>_battleMenuOpen=false,true);
            if(_checkpointError==null)
                Button(_root,"Btn_InteractionLayout",optimizedInteractionUi?"交互布局：优化版 · 点击切换基线":"交互布局：基线 · 点击切换优化版",1200,845,650,47,
                    ()=>SetUiDesignPreview(!optimizedInteractionUi));
        }
    }
}

