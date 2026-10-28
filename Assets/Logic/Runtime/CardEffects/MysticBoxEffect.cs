using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>原宝盒的揭示、可选借用与回合末归还；物理卡始终是宝盒本身。</summary>
    public sealed class MysticBoxEffect : ICardEffect
    {
        // 保留旧工厂构造兼容，但旧模块路径不能代替真实神器实例流程。
        public MysticBoxEffect(bool once) { }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
            => throw new InvalidOperationException("神秘宝盒必须由实际手牌揭示，再选择借用神器");

        public sealed class PendingUse
        {
            public PlayerState Owner { get; }
            public DeedCard Box { get; }
            public ArtifactSupply Supply { get; }
            public ArtifactCard RevealedArtifact { get; }
            public bool Used { get; internal set; }

            internal PendingUse(PlayerState owner, DeedCard box, ArtifactSupply supply, ArtifactCard revealed)
            { Owner = owner; Box = box; Supply = supply; RevealedArtifact = revealed; }
        }

        internal static void Reveal(PlayerState player, ActionContext ctx, DeedCard box, bool burn)
        {
            if (burn) throw new InvalidOperationException("请先揭示神秘宝盒，再选择所借神器的普通或焚毁效果");
            if (box == null || box.Id != "items_024" || box.Type != CardType.Artifact
                || !player.Deck.Hand.Any(c => ReferenceEquals(c, box)))
                throw new InvalidOperationException("神秘宝盒必须来自实际手牌");
            if (ctx.MysticBox != null) throw new InvalidOperationException("本回合已经揭示神秘宝盒");
            var supply = ctx.ArtifactSupply;
            if (supply == null || supply.Count == 0) throw new InvalidOperationException("神器供应中没有可揭示的卡牌");
            // 校验完毕后，供应顶对象暂存在回合状态中；不是立即放回或获得到玩家牌库。
            var revealed = supply.Draw();
            if (!player.Deck.TryPutInPlay(box)) throw new InvalidOperationException("揭示时丢失实际宝盒手牌");
            ctx.MysticBox = new PendingUse(player, box, supply, revealed);
        }

        internal static void Use(PlayerState player, ActionContext ctx, DeedCard box, bool burn, int option)
        {
            RepeatedEffectResolution.RequireCompleted(ctx);
            var pending = ctx.MysticBox;
            if (pending == null || !ReferenceEquals(pending.Owner, player) || !ReferenceEquals(pending.Box, box)
                || pending.Used || !player.Deck.PlayedCards.Any(c => ReferenceEquals(c, box)))
                throw new InvalidOperationException("请选择本回合已揭示且尚未使用的实际神秘宝盒");
            if (ReferenceEquals(ctx.CardToDiscard, box) || ReferenceEquals(ctx.CardToRemove, box)
                || ctx.CardsToDiscard.Any(c => ReferenceEquals(c, box)))
                throw new InvalidOperationException("使用中的宝盒不能作为自己的材料");
            var original = CardJsonLoader.LoadItems().SingleOrDefault(c => c.Id == pending.RevealedArtifact.Id)
                ?? throw new InvalidOperationException("被揭示的神器没有原定义");
            if (original.Id == "items_024") throw new InvalidOperationException("嵌套神秘宝盒尚未接入");

            if (!burn && original.Id is "items_000" or "items_001" or "items_002" or "items_003" or "items_018")
            {
                BannerActions.AssignBorrowedBox(player, ctx, ctx.TargetUnit!, box, original.Id);
            }
            else
            {
                // 和普通神器使用同一个正式效果映射，不能按枚举偏移推导或创造临时原卡。
                var effect = CardPlayActions.ArtifactEffect(original.Id, burn);
                if (effect is ICardEffectValidator validator) validator.Validate(player, ctx, option);
                ctx.EnsureBattleWounds(player);
                using var source = CardContributionSource.Scope(ctx, box);
                effect.Execute(player, ctx, option);
                if (burn && !player.Deck.TryRemovePlayedPermanentlyExact(box))
                    throw new InvalidOperationException("借用焚毁时丢失实际宝盒");
                if (!burn && original.Id == "items_017") ctx.CommandBannersPlayedThisTurn.Add(box);
            }
            pending.Used = true;
            player.Fame++;
            ctx.ResolveBattleWounds(player);
        }

        internal static string EffectivePlayedId(ActionContext ctx, DeedCard card)
        {
            var pending = ctx.MysticBox;
            return pending != null && pending.Used && ReferenceEquals(pending.Box, card)
                ? pending.RevealedArtifact.Id : card.Id;
        }

        public static void ResolveEndTurn(PlayerState player, ActionContext? ctx)
        {
            var pending = ctx?.MysticBox;
            if (pending == null) return;
            if (!ReferenceEquals(pending.Owner, player))
                throw new InvalidOperationException("不能结算其他玩家的神秘宝盒");
            if (!pending.Used)
            {
                if (!player.Deck.TryTakePlayedExact(pending.Box))
                    throw new InvalidOperationException("未使用的神秘宝盒不在本回合已打出区");
                player.Deck.Hand.Add(pending.Box);
            }
            else
            {
                // 部队先前已毁灭或换旗时，Release已弃盒；此处不能重复弃掉同一实例。
                foreach (var unit in player.Units.Where(u => ReferenceEquals(u.Banner, pending.Box)).ToArray())
                    BannerActions.Release(unit);
            }
            pending.Supply.PutUnder(pending.RevealedArtifact);
            ctx!.MysticBox = null;
        }
    }
}
