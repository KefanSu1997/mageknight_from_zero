using MK.Logic.Runtime;
using UnityEngine;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private static string ChaosStateText(PlayerState p)=>p.ChaosColor.HasValue?
            "轮盘当前："+ColorName(p.ChaosColor.Value)+" · 下次："+ColorName(ChaosSkillRules.NextColor(p)):"";
        private void DrawChaosUnusedRotation()
        {
            var p=_journey.Player;
            W.Text(_root,"ChaosPending","混乱大师 · 上回合未使用\n"+ChaosStateText(p)+"\n可顺时针空转一次，不获得资源，也不消耗下一回合次数。",600,340,1200,154,27,W.Cream);
            Button(_root,"Btn_ChaosRotate","顺时针空转一次",600,550,575,65,()=>ChaosSkillRules.ResolveUnusedRotation(p,true),true);
            Button(_root,"Btn_ChaosKeep","保持当前颜色",1200,550,600,65,()=>ChaosSkillRules.ResolveUnusedRotation(p,false));
        }
    }
}
