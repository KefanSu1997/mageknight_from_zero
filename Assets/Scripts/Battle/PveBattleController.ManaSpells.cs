using System.Linq;
using MK.Logic.Core;
using UnityEngine;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private void DrawManaSpellInputs(Transform panel,string id)
        {
            var p=_journey?.Player??_battle.Player;var ctx=_journey?.Context??_battle.Context;
            if(id=="magic_012")
            {
                W.Text(panel,"ClaimSource","当前公共源可选的基本色骰",1140,416,730,48,23,W.Gold);
                for(int i=0;i<4;i++)
                {
                    var color=(ManaColor)i;int available=ctx.ManaSource?.GetAvailableCount(color)??0;
                    Button(panel,"Btn_ClaimColor_"+color,(_inputColors[0]==color?"● ":"")+ColorName(color)+" · 可用 "+available,
                        1140+i%2*370,480+i/2*68,350,57,()=>_inputColors[0]=color).interactable=available>0;
                }
                W.Text(panel,"ClaimRule","按所选颜色保留一颗实际源骰至轮末。\n不占普通取骰额度；延期收益从下一回合开始。",40,620,1040,132,24,W.Cream);
                W.Text(panel,"ClaimHeld","本人保留至轮末："+string.Join("、",ctx.ManaSource?.RoundClaims.Where(x=>ReferenceEquals(x.Value,p)).Select(x=>ColorName(x.Key.Face))??Enumerable.Empty<string>()),1140,642,728,110,23,W.Cream);
            }
            string note=ctx.OtherPlayers.Count==0?"当前为单人旅程：没有其他玩家目标。":"多人效果使用当前实际玩家状态。";
            W.Text(panel,"OtherPlayerScope",note,40,788,1820,48,22,W.Gold);
        }
    }
}
