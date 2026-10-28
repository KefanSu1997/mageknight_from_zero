using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 清風之翼 / 黑夜之翼：傳送或使多名敵人不攻擊。
    /// option 基礎為移動力數值，強效為目標敵人數量(包含首名)。
    /// </summary>
    public sealed class WindWingsEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public WindWingsEffect(bool enhanced) => _enh = enhanced;

        private static int[] Targets(ActionContext ctx, int option) => ctx.SpellTargetIndices.Count > 0
            ? ctx.SpellTargetIndices.ToArray() : Enumerable.Range(0, option).ToArray();

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!_enh)
            {
                if (option < 1 || option > 5 || ctx.TravelPath.Count != option)
                    throw new InvalidOperationException("清风之翼必须选择一至五格实际路径");
                if (ctx.InBattle || ctx.InNegotiation || ctx.TurnActionCommitted || ctx.MovementPool < option)
                    throw new InvalidOperationException("清风之翼需要移动阶段及足够移动力");
                var from = player.Position;
                foreach (var to in ctx.TravelPath)
                {
                    int dq = to.Q - from.Q, dr = to.R - from.R;
                    if ((Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2 != 1
                        || ctx.MovementMap == null || !ctx.MovementMap.Placed.ContainsKey(to))
                        throw new InvalidOperationException("清风之翼路径必须逐格相邻且已揭示");
                    from = to;
                }
                if (!CardTravelActions.IsSafeDestination(player, ctx, from))
                    throw new InvalidOperationException("清风之翼必须落在安全格");
            }
            else
            {
                if (!ctx.InBattle || option < 1 || ctx.Enemies == null || option > ctx.Enemies.Count)
                    throw new InvalidOperationException("黑夜之翼必须选择本场战斗中的敌人");
                var targets = Targets(ctx, option);
                if (targets.Length != option || targets.Distinct().Count() != option || targets.Any(i => i < 0 || i >= ctx.Enemies.Count))
                    throw new InvalidOperationException("黑夜之翼目标必须有效且不能重复");
                if (targets.Any(i => ctx.Enemies[i].Abilities.Contains(Ability.MagicResist)))
                    throw new InvalidOperationException("奥术免疫敌人不能成为黑夜之翼目标");
                if (ctx.MovementPool < option * (option - 1) / 2)
                    throw new InvalidOperationException("黑夜之翼额外目标需要递增移动力费用");
            }
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                ctx.MovementPool -= option;
                player.Position = ctx.TravelPath.Last();
                ctx.CurrentTerrain = ctx.MovementMap.Placed[player.Position].Edges[0];
                ctx.TravelPath.Clear();
            }
            else
            {
                ctx.MovementPool -= option * (option - 1) / 2;
                foreach (int i in Targets(ctx, option))
                    if (ctx.SkipAttackIndices.Add(i)) ctx.NightWingsSkippedIndices.Add(i);
            }
        }
    }
}
