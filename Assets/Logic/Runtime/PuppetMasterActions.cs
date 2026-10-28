using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Physical enemy tokens, not monster templates or a replaceable last-enemy slot.</summary>
    public sealed class PuppetTokenState
    {
        internal sealed record Entry(Monster Token, EnemyTokenSupply Supply, int DefeatedTurn);
        internal readonly List<Entry> Candidates = new();
        internal readonly List<Entry> Stored = new();
        public IReadOnlyList<Monster> DefeatedThisTurn => Candidates.Select(e => e.Token).ToArray();
        public IReadOnlyList<Monster> KeptTokens => Stored.Select(e => e.Token).ToArray();
    }

    public static class PuppetMasterActions
    {
        public const string SkillId = "hero_skill_pool_003:06";

        /// <summary>Called only after the formal result records this exact defeated enemy.</summary>
        public static void RecordDefeat(PlayerState player, ActionContext context, Monster enemy)
        {
            // Reserve only when an original owned or public skill makes borrowing possible.
            // The headband may be drawn later this turn; hand presence must not lose a real kill.
            if (!player.Skills.Any(s => s.SourceId == SkillId)
                && context?.SkillSupply?.Offer.Any(s => s.SourceId == SkillId) != true) return;
            if (enemy == null || context?.Enemies == null) throw new InvalidOperationException("缺少实际击败敌人");
            int index = Enumerable.Range(0, context.Enemies.Count)
                .Where(i => ReferenceEquals(context.Enemies[i], enemy)).DefaultIfEmpty(-1).First();
            if (index < 0 || !context.DefeatedEnemyIndices.Contains(index))
                throw new InvalidOperationException("只能登记该玩家实际战果中的已击败敌人");
            var state = player.PuppetTokens;
            if (state.Candidates.Concat(state.Stored).Any(e => ReferenceEquals(e.Token, enemy))) return;
            var supply = context.EnemySupply;
            if (supply != null && supply.IsInUse(enemy)) supply.ReservePuppet(enemy, player);
            else if (supply != null && supply.ContainsPhysicalToken(enemy))
                throw new InvalidOperationException("击败标记已在供给其它区域，不能再次保存");
            else supply = null; // Pre-existing encounters may own their tokens independently of the shared supply.
            state.Candidates.Add(new PuppetTokenState.Entry(enemy, supply, player.CompletedPersonalTurns));
        }

        private static void RequireSkill(PlayerState player, ActionContext context, SkillCard skill)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (skill == null || skill.SourceId != SkillId || !player.Skills.Any(s => ReferenceEquals(s, skill))
                || SkillActions.IsUsed(player, skill))
                throw new InvalidOperationException("必须选择本回合尚未使用的自有傀儡大师");
        }

        public static void Keep(PlayerState player, ActionContext context, SkillCard skill, Monster enemy)
        {
            RequireSkill(player, context, skill);
            KeepCore(player, enemy);
            player.SkillsUsedThisTurn.Add(SkillId);
        }

        private static void KeepCore(PlayerState player, Monster enemy)
        {
            var entry = player.PuppetTokens.Candidates.FirstOrDefault(e => ReferenceEquals(e.Token, enemy)
                && e.DefeatedTurn == player.CompletedPersonalTurns);
            if (entry == null) throw new InvalidOperationException("请选择本回合实际击败且尚未保存的敌人标记");
            player.PuppetTokens.Candidates.Remove(entry);
            player.PuppetTokens.Stored.Add(entry);
        }

        /// <summary>Discard one previously kept physical token to produce its original typed power.</summary>
        public static void Consume(PlayerState player, ActionContext context, SkillCard skill, Monster enemy, bool block)
        {
            RequireSkill(player, context, skill);
            ConsumeCore(player, context, skill, enemy, block);
            player.SkillsUsedThisTurn.Add(SkillId);
        }

        private static void ConsumeCore(PlayerState player, ActionContext context, SkillCard skill, Monster enemy, bool block)
        {
            if (!context.InBattle || context.CombatPhase != (block ? Phase.Block : Phase.Melee))
                throw new InvalidOperationException("傀儡攻击只能在攻击阶段使用，格挡只能在格挡阶段使用");
            var entry = player.PuppetTokens.Stored.FirstOrDefault(e => ReferenceEquals(e.Token, enemy));
            if (entry == null)
                throw new InvalidOperationException("请选择之前保存的真实敌人标记");
            if (enemy.Armor < 0 || Enumerable.Range(0, enemy.AttackCount).Any(i => enemy.GetAttack(i).Value < 0))
                throw new InvalidOperationException("原敌人标记数值无效");
            // Source supply release precedes mutation of owned storage. It verifies the same owner.
            entry.Supply?.ReleasePuppet(enemy, player);
            player.PuppetTokens.Stored.Remove(entry);
            if (block)
            {
                bool fire = enemy.Abilities.Contains(Ability.FireResist) || enemy.Abilities.Contains(Ability.ColdFireResist);
                bool ice = enemy.Abilities.Contains(Ability.IceResist) || enemy.Abilities.Contains(Ability.ColdFireResist);
                var element = fire && ice ? Element.ColdFire : fire ? Element.Ice : ice ? Element.Fire : Element.Physical;
                context.CombatPower.AddBlock((enemy.Armor + 1) / 2, element, sourceSkill: skill);
            }
            else
            {
                for (int i = 0; i < enemy.AttackCount; i++)
                {
                    var attack = enemy.GetAttack(i);
                    context.CombatPower.AddAttack((attack.Value + 1) / 2, attack.Element, AttackType.Melee, sourceSkill: skill);
                }
            }
        }

        internal static void Borrow(PlayerState player, ActionContext context, HeadbandBorrowResolution pending,
            Monster enemy, int mode)
        {
            pending.RequireAuthorization(player, context);
            if (pending.Skill.SourceId != SkillId || mode < 0 || mode > 2)
                throw new InvalidOperationException("请选择傀儡的保留、攻击或格挡");
            if (mode == 0) KeepCore(player, enemy);
            else ConsumeCore(player, context, pending.Skill, enemy, mode == 2);
        }

        public static bool HasBorrowInput(PlayerState player, ActionContext context)
            => player.PuppetTokens.Candidates.Any(e => e.DefeatedTurn == player.CompletedPersonalTurns)
                || context.InBattle && (context.CombatPhase == Phase.Block || context.CombatPhase == Phase.Melee)
                    && player.PuppetTokens.Stored.Any();

        /// <summary>Formal personal-turn cleanup; kept tokens survive both turn and round changes.</summary>
        public static void ReleaseUnkept(PlayerState player)
        {
            foreach (var entry in player.PuppetTokens.Candidates.ToArray())
            {
                entry.Supply?.ReleasePuppet(entry.Token, player);
                player.PuppetTokens.Candidates.Remove(entry);
            }
        }
    }
}


