using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    /// <summary>One layer of actual original-effect selection; the formal effect owns all results.</summary>
    public static class PveCopyInputs
    {
        public static BattleCardChoice[] Choices(ActionContext ctx,string id,bool enhanced)
            => BattleCardChoice.For(id).Where(c=>c.Enhanced==enhanced &&
                (c.Allows(ctx.InBattle?ctx.CombatPhase:Phase.End) || ctx.InBattle&&ctx.AllAttacksSiege&&ctx.CombatPhase==Phase.Ranged&&c.Allows(Phase.Melee))).ToArray();

        private static void WithChoice(PlayerState player,ActionContext ctx,DeedCard donor,bool enhanced,string choiceId,PveDeedSelection inputs,Action<int> action)
        {
            if(donor.Id is "advanced_card_024" or "advanced_card_030")throw new InvalidOperationException("多层复制尚未接入，请选择另一张原行动");
            var choice=Choices(ctx,donor.Id,enhanced).SingleOrDefault(c=>c.Id==choiceId)
                ??throw new InvalidOperationException("请选择供体在当前阶段合法的原效果");
            var selection=inputs??new PveDeedSelection();int target=selection.TargetIndex;
            if(choice.Target!=BattleCardTarget.None && (ctx.Enemies==null||target<0||target>=ctx.Enemies.Count||ctx.DefeatedEnemyIndices.Contains(target)))
                throw new InvalidOperationException("请选择本场实际存活敌人");
            var oldEnemy=ctx.SelectedEnemy;int oldAttack=ctx.SelectedEnemyAttackIndex;var oldMana=ctx.SelectedActionManaColor;
            // Copy mode comes from the host; a pending next-card enhancement must not change the donor mode.
            bool oldEnhanced=ctx.NextCardEnhanced;
            bool submitted=false;
            try
            {
                ctx.NextCardEnhanced=false;ctx.SelectedActionManaColor=choice.ActionMana;
                if(choice.Target==BattleCardTarget.EnemyAttack)
                {
                    if(selection.AttackIndex<0||selection.AttackIndex>=ctx.Enemies[target].AttackCount)
                        throw new InvalidOperationException("请选择实际敌人攻击槽");
                    ctx.SelectedEnemy=ctx.Enemies[target];ctx.SelectedEnemyAttackIndex=selection.AttackIndex;
                }
                PveDeedInputs.Execute(player,ctx,donor,choice,selection,option=>
                {
                    // Restore the host's real enhancement for its own formal payment/play command.
                    ctx.NextCardEnhanced=oldEnhanced;
                    submitted=true;
                    action(choice.Target==BattleCardTarget.EnemyIndex?target:option);
                });
            }
            finally {if(!submitted)ctx.NextCardEnhanced=oldEnhanced;ctx.SelectedEnemy=oldEnemy;ctx.SelectedEnemyAttackIndex=oldAttack;ctx.SelectedActionManaColor=oldMana;}
        }

        public static void Execute(PlayerState player,ActionContext ctx,DeedCard host,bool enhanced,PveDeedSelection input,Action<int> play)
        {
            bool overload=host.Id=="advanced_card_024";DeedCard donor;int index=-1;
            if(overload)
            {
                var cards=input.Cards??Array.Empty<DeedCard>();
                if(cards.Length!=1||cards[0].Type!=CardType.Action||ReferenceEquals(cards[0],host)||!player.Deck.Hand.Any(c=>ReferenceEquals(c,cards[0])))
                    throw new InvalidOperationException("请选择另一张实际原行动作为永久去除供体");
                donor=cards[0];
            }
            else
            {
                index=ctx.AdvActionSupply?.Offer.FindIndex(c=>ReferenceEquals(c,input.Advanced))??-1;
                if(index<0)throw new InvalidOperationException("请选择实际高级行动供应");
                donor=new DeedCard(input.Advanced.Id,CardType.Action); // Describes an effect, never enters any player zone.
            }
            WithChoice(player,ctx,donor,enhanced,input.BorrowedChoice,input.BorrowedInputs,option=>
            {
                var oldRemove=ctx.CardToRemove;var oldSelections=ctx.OverloadSelections;bool oldStepwise=ctx.OverloadStepwise;
                int oldOption=ctx.BloodlineEffectOption;bool oldWound=ctx.BloodlineWoundToDiscard;
                try
                {
                    if(overload)
                    {
                        ctx.OverloadSelections=new List<RepeatedCardSelection>{new(){Option=option,CardToRemove=ctx.CardToRemove,CardToDiscard=ctx.CardToDiscard}};
                        ctx.CardToRemove=donor;ctx.OverloadStepwise=true;
                    }
                    else {ctx.BloodlineEffectOption=option;ctx.BloodlineWoundToDiscard=input.WoundToDiscard;}
                    play(overload?option:index);
                }
                finally {ctx.CardToRemove=oldRemove;ctx.OverloadSelections=oldSelections;ctx.OverloadStepwise=oldStepwise;ctx.BloodlineEffectOption=oldOption;ctx.BloodlineWoundToDiscard=oldWound;}
            });
        }

        public static void Continue(PlayerState player,ActionContext ctx,string choiceId,PveDeedSelection inputs=null)
        {
            var pending=ctx.PendingRepeatedEffect??throw new InvalidOperationException("没有待继续的原效果");
            WithChoice(player,ctx,new DeedCard(pending.OriginalCardId,CardType.Action),pending.Enhanced,choiceId,inputs,option=>
                CardPlayActions.ContinueRepeatedEffect(player,ctx,new RepeatedCardSelection{Option=option,CardToRemove=ctx.CardToRemove,CardToDiscard=ctx.CardToDiscard}));
        }
    }
}
