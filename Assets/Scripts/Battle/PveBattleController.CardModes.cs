using System;
using System.Linq;
using MK.Logic.Runtime.Battles;
using UnityEngine;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        // Use the original choice's Enhanced flag, never infer a mode from its translated label.
        private BattleCardChoice[] DrawCardMode(Transform panel, BattleCardChoice[] all, string selected,
            Action<string> choose, float x, float y, float width, string prefix="Btn_Choice_")
        {
            bool freeStrong=PaymentContext.NextCardEnhanced && (_inputCard??_journeyCard??_selected)?.Type==MK.Logic.Core.CardType.Action;
            var current=all.FirstOrDefault(c=>c.Id==selected)??all.FirstOrDefault();
            foreach(var enhanced in new[]{false,true})
            {
                var first=all.FirstOrDefault(c=>c.Enhanced==enhanced);
                var mode=enhanced?"强化效果":"基础效果";
                Button(panel,prefix+(enhanced?"strong":"base"),
                    (current?.Enhanced==enhanced?"● ":"")+mode,x+(enhanced?(width+12)/2:0),y,(width-12)/2,43,
                    ()=>choose(first.Id),current?.Enhanced==enhanced).interactable=first!=null && (!freeStrong || enhanced);
            }
            var filtered=all.Where(c=>c.Enhanced==(current?.Enhanced??false)).ToArray();
            return filtered;
        }

        private string CurrentCardMode()
        {
            var card=_inputCard??_journeyCard??_selected;
            if(card==null)return "";
            var id=_inputCard!=null?_inputChoice:_journeyCard!=null?_journeyChoice:_choice;
            var choices=_inputCard!=null?(_inputInBattle?_battle.Choices(card):_journey.OutsideChoices(card)):
                _journeyCard!=null?_journey.OutsideChoices(card):_battle.Choices(card);
            var choice=choices.FirstOrDefault(c=>c.Id==id);
            return choice==null?"":choice.Enhanced?"strong":"base";
        }

        private string CardModeConfirm(BattleCardChoice[] all,string selected)
        {
            var choice=all.FirstOrDefault(c=>c.Id==selected);
            return choice==null?"请选择效果":"确认 · "+(PaymentContext.NextCardEnhanced && (_inputCard??_journeyCard??_selected)?.Type==MK.Logic.Core.CardType.Action?"免费强化":choice.Enhanced?"强化效果":"基础效果");
        }
    }
}
