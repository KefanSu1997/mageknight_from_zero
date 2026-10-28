using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public sealed partial class SharedSkillArea
    {
        public const string HowlId = "hero_skill_pool_004:09";
        private sealed class HowlChoices
        {
            internal PlayerState Player;
            internal readonly Queue<SharedSkillToken> Tokens = new();
        }
        private readonly Dictionary<ActionContext, HowlChoices> _howlChoices = new();

        public IReadOnlyList<SharedSkillToken> PendingHowls(ActionContext context)
            => context != null && _howlChoices.TryGetValue(context, out var pending)
                ? pending.Tokens.ToArray() : Array.Empty<SharedSkillToken>();

        public void RequireHowlResolved(ActionContext context)
        {
            if (context != null && _howlChoices.TryGetValue(context, out var pending) && pending.Tokens.Count > 0)
                throw new InvalidOperationException("请先为群狼嚎叫选择减甲敌人与减攻目标");
        }

        public SharedSkillToken UseHowl(PlayerState player, ActionContext context, SkillCard skill, DeedCard card, SidewaysType type)
        {
            RequireUse(player, context, skill, HowlId);
            RequireHowlResolved(context);
            if (context.InNegotiation || context.RestedThisTurn || context.TemporalPortalTurn || context.PeacefulMomentAction
                || card == null || !player.Deck.Hand.Any(c => ReferenceEquals(c,card)) || !Enum.IsDefined(typeof(SidewaysType),type))
                throw new InvalidOperationException("群狼嚎叫须在交涉外选择实际可横置的原手牌");
            bool original = card.Type switch
            {
                CardType.Action => CardJsonLoader.LoadBasicActions().Any(c=>c.Id==card.Id) || CardJsonLoader.LoadAdvancedActions().Any(c=>c.Id==card.Id),
                CardType.Spell => CardJsonLoader.LoadSpells().Any(c=>c.Id==card.Id),
                CardType.Artifact => CardJsonLoader.LoadItems().Any(c=>c.Id==card.Id),
                _ => false
            };
            if (!original || player.FreeSlots < 0)
                throw new InvalidOperationException("必须使用类型匹配的原牌，且指挥标记分配合法");
            bool activeCombat = context.InBattle && context.CombatPhase != Phase.None && context.CombatPhase != Phase.End;
            if (context.InBattle && !activeCombat
                || type == SidewaysType.Attack && (!activeCombat || context.CombatPhase != Phase.Melee && !(context.CombatPhase == Phase.Ranged && context.AllAttacksSiege))
                || type == SidewaysType.Block && (!activeCombat || context.CombatPhase != Phase.Block)
                || !context.InBattle && context.TurnActionCommitted)
                throw new InvalidOperationException("当前阶段不能采用此原牌横置用途");
            int value = 4 + player.FreeSlots;
            if (!new ActionSystem(false).PlaySideways(card,player,context,type,value))
                throw new InvalidOperationException("实际手牌横置失败");
            if (context.InBattle) context.CombatActionStarted = context.TurnActionCommitted = true;
            return Publish(player,skill);
        }

        /// <summary>Invoke once at the actual start of enemy combat, after binding its original enemy list.</summary>
        public void OnBattleStarted(PlayerState player, ActionContext context)
        {
            if (player == null || context == null || !_isActualTurn(player,context))
                throw new InvalidOperationException("敌战开始必须属于实际玩家当前回合");
            RequireHowlResolved(context);
            if (!context.InBattle || context.CombatPhase != Phase.Ranged || context.Enemies == null || context.Enemies.Count == 0)
                throw new InvalidOperationException("必须先建立正式敌战和实际敌人");
            var tokens = _tokens.Where(t=>t.Skill.SourceId==HowlId).ToArray();
            if (tokens.Length == 0) return;
            var pending = new HowlChoices { Player=player };
            foreach (var token in tokens) { Return(token); pending.Tokens.Enqueue(token); }
            _howlChoices.Add(context,pending);
        }

        public void ResolveHowl(PlayerState player, ActionContext context, Monster armorEnemy, Monster attackEnemy, int attackIndex = 0)
        {
            // Deliberately bypass RequireActual's pending-choice guard: this is the resolving transaction.
            if (player == null || context == null || !_isActualTurn(player,context)
                || !_howlChoices.TryGetValue(context,out var pending) || !ReferenceEquals(pending.Player,player)
                || pending.Tokens.Count == 0 || !context.InBattle || context.CombatPhase != Phase.Ranged
                || context.PendingRepeatedEffect != null)
                throw new InvalidOperationException("当前实际敌战没有可结算的群狼嚎叫");
            int armor = ActualHowlEnemy(context,armorEnemy);
            int attack = ActualHowlEnemy(context,attackEnemy);
            if (armor < 0 || attack < 0 || attackIndex < 0 || attackIndex >= attackEnemy.AttackCount
                || context.SkipAttackIndices.Contains(attack) || context.IsAttackCancelled(attack,attackIndex)
                || context.ColdStareSkips(attackEnemy) || context.SuccessfullyBlockedAttacks.Contains((attack,attackIndex)))
                throw new InvalidOperationException("须选择本战存活的实际减甲敌人与一次有效攻击");
            context.AddSkillArmorReduction(armorEnemy,1);
            context.AddSkillAttackAdjustment(attackEnemy,attackIndex,-1);
            pending.Tokens.Dequeue();
            if (pending.Tokens.Count == 0) _howlChoices.Remove(context);
        }

        private static int ActualHowlEnemy(ActionContext context, Monster enemy)
            => context.Enemies == null ? -1 : Enumerable.Range(0,context.Enemies.Count)
                .Where(i=>ReferenceEquals(context.Enemies[i],enemy) && !context.DefeatedEnemyIndices.Contains(i)).DefaultIfEmpty(-1).First();
    }
}
