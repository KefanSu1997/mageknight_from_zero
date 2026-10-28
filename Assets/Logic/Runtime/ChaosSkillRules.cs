using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    public static class ChaosSkillRules
    {
        public const string SourceId = "hero_skill_pool_003:07";
        private static readonly ManaColor[] Cycle = { ManaColor.Blue, ManaColor.Green, ManaColor.Black, ManaColor.White, ManaColor.Red, ManaColor.Gold };

        public static ManaColor NextColor(PlayerState player)
        {
            if (player.ChaosColor == null || !player.Skills.Any(s => s.SourceId == SourceId))
                throw new InvalidOperationException("混乱大师须先经过实际技能取得和初始掷骰");
            int index = Array.IndexOf(Cycle, player.ChaosColor.Value);
            if (index < 0) throw new InvalidOperationException("混乱大师当前颜色无效");
            return Cycle[(index + 1) % Cycle.Length];
        }

        public static void Validate(PlayerState player, ActionContext context, int option = -1)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (player.ChaosUnusedRotationPending) throw new InvalidOperationException("请先决定上一回合未用的空转");
            var next = NextColor(player);
            if (next == ManaColor.Gold ? option < 0 || option > 4 : option != -1 && option != 0)
                throw new InvalidOperationException("金色须明确选择蓝/绿/黑/白/红奖励，其他颜色没有分支");
            var reward = next == ManaColor.Gold ? Cycle[option] : next;
            bool valid = reward switch
            {
                ManaColor.Blue => context.InBattle && context.CombatPhase == Phase.Block,
                ManaColor.Green => !context.InBattle && !context.TurnActionCommitted && !context.RestedThisTurn,
                ManaColor.Black => context.InBattle && (context.CombatPhase == Phase.Ranged || context.CombatPhase == Phase.Melee),
                ManaColor.White => !context.InBattle && !context.RestedThisTurn,
                ManaColor.Red => context.InBattle && (context.CombatPhase == Phase.Melee || context.CombatPhase == Phase.Ranged && context.AllAttacksSiege),
                _ => false
            };
            if (!valid) throw new InvalidOperationException("混乱大师下个颜色的奖励不能在当前阶段使用");
        }

        public static (int Option, string Label)[] Options(PlayerState player, ActionContext context)
        {
            var next = NextColor(player);
            string[] labels = { "格挡3", "移动1", "冰火远程1", "影响2", "攻击2" };
            var result = new System.Collections.Generic.List<(int Option, string Label)>();
            int count = next == ManaColor.Gold ? 5 : 1;
            for (int option = 0; option < count; option++)
            {
                try { Validate(player, context, option); }
                catch (InvalidOperationException) { continue; }
                int rewardIndex = next == ManaColor.Gold ? option : Array.IndexOf(Cycle, next);
                result.Add((option, "顺时针至" + next + " · " + labels[rewardIndex]));
            }
            return result.ToArray();
        }

        public static void Apply(PlayerState player, ActionContext context, int option = -1)
        {
            Validate(player, context, option);
            var next = NextColor(player);
            player.ChaosColor = next;
            var reward = next == ManaColor.Gold ? Cycle[option] : next;
            switch (reward)
            {
                case ManaColor.Blue: context.BlockPool += 3; break;
                case ManaColor.Green: context.MovementPool += 1; break;
                case ManaColor.Black: context.CombatPower.AddAttack(1, Element.ColdFire, AttackType.Ranged); break;
                case ManaColor.White: context.InfluencePool += 2; break;
                case ManaColor.Red: context.CombatPower.AddAttack(2); break;
            }
        }

        /// <summary>Call before the formal end-turn hook clears this turn's skill-use markers.</summary>
        public static void RecordEndTurn(PlayerState player)
        {
            if (player.IsPersonalTurnActive && player.ChaosColor.HasValue && player.Skills.Any(s => s.SourceId == SourceId)
                && !player.SkillsUsedThisTurn.Contains(SourceId)) player.ChaosUnusedRotationPending = true;
        }

        public static void ResolveUnusedRotation(PlayerState player, bool rotate)
        {
            if (!player.ChaosUnusedRotationPending || player.IsPersonalTurnActive)
                throw new InvalidOperationException("混乱大师空转须在未用的本人回合结束后、下回合开始前决定");
            var next = NextColor(player);
            if (rotate) player.ChaosColor = next;
            player.ChaosUnusedRotationPending = false;
        }

        public static void RequireResolvedBeforeStartTurn(PlayerState player)
        {
            if (player.ChaosUnusedRotationPending)
                throw new InvalidOperationException("开始下回合前请选择混乱大师空转或保持原色");
        }
    }
}
