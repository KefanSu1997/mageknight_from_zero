using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _conversionsOpen;
        private UnitState _peacefulUnit;
        private bool HasCombatConversions => _battle!=null && (_battle.Context.MoveCostAttack>0 || _battle.Context.InfluenceAsBlock);

        private void DrawConversions()
        {
            var ctx=_journey?.Context??_battle.Context;var player=_journey?.Player??_battle.Player;
            var panel=W.Panel(_root,"Conversions",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","原卡能力 · 支付实际资源",40,25,1800,55,36,W.Gold);
            W.Text(panel,"Resources",$"移动力 {ctx.MovementPool} · 影响力 {ctx.InfluencePool} · 治疗点 {ctx.Healing.Total} · 手牌创伤 {player.Wounds}",40,93,1800,50,28,W.Cream);
            string[] ids={"advanced_card_006","advanced_card_018","advanced_card_035"};
            for(int i=0;i<ids.Length;i++)
            {
                int x=40+i*620;var binding=deedCatalog.Single(d=>d.id==ids[i]);var source=(ActionCardSO)binding.source;
                bool available=i==0?ctx.MoveCostAttack>0:i==1?ctx.InfluenceAsBlock:ctx.PeacefulMomentAvailable;
                bool strong=i==0?ctx.MoveCostRanged>0:i==1?ctx.InfluenceBlockElement!=Element.Physical:ctx.PeacefulMomentReady;
                W.Sprite(panel,"OriginalConversion_"+i,binding.artwork,x,176,146,211);
                W.Text(panel,"Rule_"+i,source.NameCn+"\n"+(available?(strong?source.EnhancedEffect:source.BaseEffect):"本回合尚无此牌能力。"),x+166,174,425,219,23,W.Cream);
                if(i==0)
                {
                    int melee=ctx.MoveCostAttack>0?ctx.MovementPool/ctx.MoveCostAttack:0;
                    int ranged=ctx.MoveCostRanged>0?ctx.MovementPool/ctx.MoveCostRanged:0;
                    bool attackPhase=ctx.InBattle && _battle.Phase==Phase.Melee;
                    bool rangedPhase=ctx.InBattle && (_battle.Phase==Phase.Ranged || attackPhase);
                    Button(panel,"Btn_MoveAttackOne","支付1移动 → 攻击1",x,426,588,56,()=>_battle.ConvertMovement(false)).interactable=attackPhase&&melee>0;
                    Button(panel,"Btn_MoveAttackAll",$"支付{melee}移动 → 攻击{melee}",x,498,588,56,()=>_battle.ConvertMovement(false,melee)).interactable=attackPhase&&melee>0;
                    Button(panel,"Btn_MoveRangedOne","支付2移动 → 远程攻击1",x,570,588,56,()=>_battle.ConvertMovement(true)).interactable=rangedPhase&&ranged>0;
                    Button(panel,"Btn_MoveRangedAll",$"支付{ranged*2}移动 → 远程攻击{ranged}",x,642,588,56,()=>_battle.ConvertMovement(true,ranged)).interactable=rangedPhase&&ranged>0;
                }
                else if(i==1)
                {
                    string element=ctx.InfluenceBlockElement==Element.Fire?"火焰":ctx.InfluenceBlockElement==Element.Ice?"寒冰":"普通";
                    bool block=ctx.InBattle && _battle.Phase==Phase.Block && ctx.InfluenceAsBlock && ctx.InfluencePool>0;
                    Button(panel,"Btn_InfluenceBlockOne",$"支付1影响力 → {element}格挡1",x,426,588,56,()=>_battle.ConvertInfluence()).interactable=block;
                    Button(panel,"Btn_InfluenceBlockAll",$"支付{ctx.InfluencePool}影响力 → {element}格挡{ctx.InfluencePool}",x,498,588,56,()=>_battle.ConvertInfluence(ctx.InfluencePool)).interactable=block;
                }
                else
                {
                    bool peace=_journey!=null && !ctx.InBattle && ctx.PeacefulMomentAvailable;
                    Button(panel,"Btn_PeaceHeal","支付2影响力 → 治疗1",x,426,588,56,()=>_journey.ConvertPeacefulHealing()).interactable=peace&&ctx.InfluencePool>=2;
                    Button(panel,"Btn_PeaceReady","按所选部队等级×2支付并重整",x,498,588,56,()=>_journey.ReadyWithInfluence(_peacefulUnit,true)).interactable=peace&&ctx.PeacefulMomentReady&&_peacefulUnit!=null;
                    for(int u=0;u<player.Units.Count;u++)
                    {
                        var unit=player.Units[u];
                        Button(panel,"Btn_PeaceUnit_"+u,(ReferenceEquals(unit,_peacefulUnit)?"● ":"")+unit.Card.NameCn+$" · 等级{unit.Card.Level} · 创伤{unit.Wounds} · "+(unit.IsReady?"就绪":"耗竭"),x,576+u*57,588,49,()=>_peacefulUnit=unit);
                    }
                    W.Text(panel,"PeaceRule","实际转换或重整会占用本回合行动。\n治疗点仍需回到手牌界面消费；重整不治疗。",x,820,588,75,22,W.Muted);
                }
            }
            W.Text(panel,"Feedback",_feedback??"选择实际支付量，再回到战场选择目标结算。",40,919,1830,65,23,W.Gold);
            Button(panel,"Btn_CloseConversions","返回当前行动",40,1001,1830,56,()=>_conversionsOpen=false,true);
        }
    }
}
