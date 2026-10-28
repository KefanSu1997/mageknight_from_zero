using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Linq;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 德魯伊法杖：棄牌選擇不同效果，強效可執行兩次且免棄牌。
    /// option 低8位為第1項目，次8位為其參數；高位相同結構表示第2項目。
    /// 0=白色傳送、1=藍色得晶體、2=紅色重整部隊、3=綠色治療。
    /// </summary>
    public sealed class DruidStaffEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _once;
        public DruidStaffEffect(bool once) => _once = once;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option < 0 || !_once && (option >> 16) != 0)
                throw new InvalidOperationException("德鲁伊法杖选项编码无效");
            int first = option & 0xFF;
            ValidateChoice(first, (option >> 8) & 0xFF, player, ctx);
            if (!_once)
            {
                var material = ctx.CardToDiscard;
                if (material == null || !player.Deck.Hand.Any(c => ReferenceEquals(c, material))
                    || material.Type != CardType.Action && material.Type != CardType.Spell)
                    throw new InvalidOperationException("德鲁伊法杖必须弃掉实际持有的有色原卡");
                var required = first switch
                {
                    0 => ManaColor.White, 1 => ManaColor.Blue, 2 => ManaColor.Red, _ => ManaColor.Green
                };
                if (CardColorHelper.Get(material.Id) != required)
                    throw new InvalidOperationException("德鲁伊法杖选项必须对应实际弃牌的原颜色");
            }
            else
            {
                int second = (option >> 16) & 0xFF;
                if (first == second) throw new InvalidOperationException("焚毁德鲁伊法杖必须选择两项不同效果");
                ValidateChoice(second, (option >> 24) & 0xFF, player, ctx);
            }
        }

        private static void ValidateChoice(int choice, int parameter, PlayerState player, ActionContext ctx)
        {
            if (choice < 0 || choice > 3 || (choice == 1 ? parameter > 3 : parameter != 0))
                throw new InvalidOperationException("法杖只支持四种印刷效果；魔晶必须明确选择基本色");
            if (choice == 0)
            {
                if (ctx.InBattle || ctx.InNegotiation || ctx.TurnActionCommitted || ctx.TravelPath.Count > 2)
                    throw new InvalidOperationException("法杖白色移动必须在移动阶段，最多两格");
                var from = player.Position;
                foreach (var to in ctx.TravelPath)
                {
                    int dq = to.Q - from.Q, dr = to.R - from.R;
                    if ((Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2 != 1
                        || ctx.MovementMap == null || !ctx.MovementMap.Placed.TryGetValue(to, out var tile)
                        || tile.Edges.Length == 0)
                        throw new InvalidOperationException("法杖路径必须逐格相邻且已经揭示");
                    from = to;
                }
                if (ctx.TravelPath.Count > 0 && !CardTravelActions.IsSafeDestination(player, ctx, from, ctx.OtherPlayers))
                    throw new InvalidOperationException("法杖白色移动必须停在安全格");
            }
            if (choice == 2 && (ctx.TargetUnit == null || !player.Units.Contains(ctx.TargetUnit)
                || ctx.TargetUnit.IsDestroyed || ctx.TargetUnit.Card.Level < 1 || ctx.TargetUnit.Card.Level > 3))
                throw new InvalidOperationException("法杖红色效果必须选择最高三级的自有部队");
            if (choice == 3 && ctx.InBattle)
                throw new InvalidOperationException("战斗中不能使用法杖治疗");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_once)
            {
                if (!player.Deck.TryDiscardExact(ctx.CardToDiscard))
                    throw new InvalidOperationException("法杖弃牌实例已不在手牌");
                ctx.CardToDiscard = null;
            }
            Apply(option & 0xFF, (option >> 8) & 0xFF, player, ctx);
            if (_once) Apply((option >> 16) & 0xFF, (option >> 24) & 0xFF, player, ctx);
        }

        private static void Apply(int opt, int param, PlayerState p, ActionContext ctx)
        {
            switch (opt)
            {
                case 0: // 白色：实际沿已验证路径移动，不借用时光传送的独占回合状态
                    if (ctx.TravelPath.Count > 0)
                    {
                        p.Position = ctx.TravelPath.Last();
                        ctx.CurrentTerrain = ctx.MovementMap.Placed[p.Position].Edges[0];
                        SiteVisibility.RevealVisible(p, ctx.MovementMap, ctx.DayPart, ctx);
                        ctx.TravelPath.Clear();
                    }
                    break;
                case 1: // 藍色：任意色晶體兩顆
                    var col = (ManaColor)param;
                    p.Mana.AddCrystal(col, 2);
                    break;
                case 2: // 紅色：重整3級以下部隊
                    ctx.TargetUnit.Ready();
                    break;
                case 3: // 綠色：治療3
                    ctx.Healing.Add(3);
                    break;
            }
        }
    }
}
