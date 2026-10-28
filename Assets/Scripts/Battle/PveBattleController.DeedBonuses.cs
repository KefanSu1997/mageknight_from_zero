using System;
using System.Linq;
using MK.Logic.Runtime.Battles;
using TMPro;
using UnityEngine;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private void DrawNatureUnitInputs(Transform panel)
        {
            var player=_journey?.Player??_battle.Player;
            W.Text(panel,"NatureTarget","选择实际自有部队 · 本场物理抗性",1140,426,726,50,23,W.Gold);
            for(int i=0;i<player.Units.Count;i++)
            {
                var unit=player.Units[i];var binding=unitCatalog.Concat(_activeUnits).First(u=>u.originalId==unit.Card.Id);
                var b=Button(panel,"Btn_InputUnit_"+i,"",1140+i%3*247,494+i/3*177,232,166,()=>_inputUnit=unit);
                W.Sprite(b.transform,"OriginalUnitArt",binding.artwork,8,8,98,148);
                W.Text(b.transform,"UnitName",(ReferenceEquals(_inputUnit,unit)?"● ":"")+unit.Card.NameCn+$"\n护甲{unit.Card.Armor}\n创伤{unit.Wounds}",116,13,109,144,20,W.Cream,TextAlignmentOptions.Center);
            }
        }
        private void DrawManaPowerBonuses(Transform panel,BattleCardChoice choice)
        {
            var player=_journey?.Player??_battle.Player;var ctx=_journey?.Context??_battle.Context;
            int sets;
            try{sets=PveDeedInputs.ManaPowerSetsAfterPayment(player,ctx,choice);}
            catch(InvalidOperationException ex){_inputBonuses.Clear();W.Text(panel,"SetPreview",ex.Message,40,464,1040,60,23,W.Gold);return;}
            while(_inputBonuses.Count<sets)_inputBonuses.Add(-1);
            if(_inputBonuses.Count>sets)_inputBonuses.RemoveRange(sets,_inputBonuses.Count-sets);
            W.Text(panel,"SetPreview",$"按实际标记优先付款后：{sets}套四色魔晶。\n每套分别选择奖励，与主效果独立。",40,454,1040,82,24,W.Gold);
            for(int row=0;row<sets;row++)
            {
                int n=row;W.Text(panel,"BonusLabel_"+n,"第"+(row+1)+"套",40,557+row*92,174,58,24,W.Cream);
                string[] labels={"移动2","治疗1","抽牌1"};
                for(int option=0;option<labels.Length;option++)
                {
                    int selected=option;
                    Button(panel,"Btn_InputBonus_"+n+"_"+option,(_inputBonuses[n]==option?"● ":"")+labels[option],234+option*279,552+row*92,260,60,()=>_inputBonuses[n]=selected).interactable=!ctx.InBattle||option!=1;
                }
            }
        }
    }
}
