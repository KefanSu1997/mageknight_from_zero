using System;
using System.Linq;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Original banner identity and assigned-card zone, shared by unit removal and round return.</summary>
    public static class BannerActions
    {
        public static void AssignFear(PlayerState player, UnitState unit, DeedCard banner)
            => AssignFromHand(player, unit, banner, "items_001", "必须选择原恐惧旌旗实例");

        /// <summary>Spend the assigned unit to cancel its selected enemy attack; this is not a successful Block.</summary>
        public static void UseFear(PlayerState player, ActionContext context, UnitState unit, int enemyIndex, int attackIndex = 0)
        {
            UnitParticipation.Require(player, context, unit);
            if (!context.InBattle || context.CombatPhase != MK.Logic.Core.Phase.Block)
                throw new InvalidOperationException("恐惧旌旗只能在格挡阶段发动");
            if (unit == null || !player.Units.Contains(unit) || unit.BannerOwner != player
                || unit.BannerEffectId != "items_001" || !unit.CanActivate)
                throw new InvalidOperationException("必须选择附着恐惧旌旗且就绪、未受伤的自有部队");
            if (context.Enemies == null || enemyIndex < 0 || enemyIndex >= context.Enemies.Count
                || attackIndex < 0 || attackIndex >= context.Enemies[enemyIndex].AttackCount
                || context.DefeatedEnemyIndices.Contains(enemyIndex) || context.IsAttackCancelled(enemyIndex, attackIndex)
                || context.ColdStareSkips(context.Enemies[enemyIndex])
                || context.SuccessfullyBlockedAttacks.Contains((enemyIndex, attackIndex)))
                throw new InvalidOperationException("必须选择尚未取消、格挡或击败的敌人攻击");
            // Arcane Immunity protects an enemy, not its attack (Shades of Tezla rules, p4).
            context.CancelEnemyAttack(enemyIndex, attackIndex);
            DuelActions.RecordUnitEffect(context, context.Enemies[enemyIndex]);
            unit.Exhaust();
            player.Fame++;
            context.CombatActionStarted = context.TurnActionCommitted = true;
        }

        public static void AssignHonor(PlayerState player, UnitState unit, DeedCard banner)
            => AssignFromHand(player,unit,banner,"items_000","必须选择原荣誉旌旗实例");

        public static void AssignFortitude(PlayerState player, UnitState unit, DeedCard banner)
            => AssignFromHand(player,unit,banner,"items_018","必须选择原强韧旌旗实例");

        public static void AssignCourage(PlayerState player, UnitState unit, DeedCard banner)
            => AssignFromHand(player,unit,banner,"items_003","必须选择原勇气旌旗实例");

        public static void AssignProtection(PlayerState player, UnitState unit, DeedCard banner)
            => AssignFromHand(player,unit,banner,"items_002","必须选择原防护旌旗实例");

        public static int ProtectionArmor(UnitState unit) => unit.BannerEffectId == "items_002" ? 1 : 0;
        public static bool ProtectionResists(UnitState unit, MK.Logic.Core.Element element)
            => unit.BannerEffectId == "items_002" && (element == MK.Logic.Core.Element.Fire
                || element == MK.Logic.Core.Element.Ice || element == MK.Logic.Core.Element.ColdFire);

        public static void ChooseProtectionCleanup(ActionContext context, bool remove)
        {
            if (!context.ProtectionBannerBurned) throw new InvalidOperationException("本回合尚未实际焚毁防护旌旗");
            context.RemoveNewWoundsAtTurnEnd = remove;
        }

        public static bool CourageSpent(UnitState unit)
            => unit.BannerEffectId == "items_003" && unit.BannerOwner != null
                && unit.BannerOwner.BannersUsedThisRound.Any(c => ReferenceEquals(c,unit.Banner));

        public static void UseCourage(PlayerState player, ActionContext context, UnitState unit)
        {
            UnitParticipation.Require(player, context, unit);
            if (context.InBattle) throw new InvalidOperationException("战斗中不能使用勇气旌旗重整部队");
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed || unit.BannerOwner != player
                || unit.BannerEffectId != "items_003" || CourageSpent(unit))
                throw new InvalidOperationException("必须选择自有部队上本轮尚未使用的勇气旌旗");
            player.BannersUsedThisRound.Add(unit.Banner);
            unit.Ready();
        }

        /// <summary>已揭示的实际宝盒从已打出区附旗，保留物理身份并复用原旗的正式能力。</summary>
        internal static void AssignBorrowedBox(PlayerState player, ActionContext context, UnitState unit, DeedCard box, string id)
        {
            var pending = context.MysticBox;
            if (pending == null || pending.Used || !ReferenceEquals(pending.Owner, player)
                || !ReferenceEquals(pending.Box, box) || pending.RevealedArtifact.Id != id
                || !player.Deck.PlayedCards.Any(c => ReferenceEquals(c, box)))
                throw new InvalidOperationException("只能附着本回合已揭示且尚未使用的实际宝盒");
            if (id is not ("items_000" or "items_001" or "items_002" or "items_003" or "items_018"))
                throw new InvalidOperationException("该原神器不能直接分配为旌旗");
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed)
                throw new InvalidOperationException("旗帜必须分配给自有且未销毁的部队");
            if (unit.BannerEffectId == "items_017")
                throw new InvalidOperationException("失去指挥旌旗会移除此部队，不能把宝盒附着给它");
            Release(unit);
            if (!player.Deck.TryTakePlayedExact(box)) throw new InvalidOperationException("附旗时丢失实际宝盒");
            unit.Banner = box;
            unit.BannerOwner = player;
            unit.BorrowedBannerId = id;
        }

        private static void AssignFromHand(PlayerState player, UnitState unit, DeedCard banner, string id, string error)
        {
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed)
                throw new InvalidOperationException("旗帜必须分配给自有且未销毁的部队");
            if (banner == null || banner.Id != id || banner.Type != MK.Logic.Core.CardType.Artifact)
                throw new InvalidOperationException(error);
            int index = player.Deck.Hand.FindIndex(c => ReferenceEquals(c, banner));
            if (index < 0) throw new InvalidOperationException("旗帜必须来自实际手牌，附着期间不能重新打出");
            Release(unit);
            if (unit.IsDestroyed) return; // Losing a command banner removes the unit before a replacement can attach.
            player.Deck.Hand.RemoveAt(index);
            player.Deck.RecordCardPlayed();
            unit.Banner = banner;
            unit.BannerOwner = player;
            unit.BorrowedBannerId = null;
        }

        public static bool CommandAvailable(PlayerState player, ActionContext context, DeedCard? banner)
            => banner != null && CardEffects.MysticBoxEffect.EffectivePlayedId(context, banner) == "items_017"
                && banner.Type == MK.Logic.Core.CardType.Artifact
                && context.CommandBannersPlayedThisTurn.Any(c => ReferenceEquals(c,banner))
                && player.Deck.PlayedCards.Any(c => ReferenceEquals(c,banner));

        public static void SelectCommandForRecruit(PlayerState player, ActionContext context, DeedCard? banner)
        {
            if (banner != null && !CommandAvailable(player,context,banner))
                throw new InvalidOperationException("必须选择本回合实际打出且未附着的指挥旌旗");
            context.CommandBannerForRecruit = banner;
        }

        public static void AssignCommand(PlayerState player, ActionContext context, UnitState unit, DeedCard banner)
        {
            if(LoyaltyActions.IsBound(player,unit))throw new InvalidOperationException("忠诚契约部队不能再使用指挥旌旗代替指挥标记");
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed || !context.RecruitedUnitsThisTurn.Contains(unit))
                throw new InvalidOperationException("指挥旌旗只能分配给本回合实际招募的自有部队");
            if (!CommandAvailable(player,context,banner))
                throw new InvalidOperationException("必须选择本回合实际打出且未附着的指挥旌旗");
            string effectId = CardEffects.MysticBoxEffect.EffectivePlayedId(context, banner);
            Release(unit);
            if (unit.IsDestroyed) return;
            if (!player.Deck.TryTakePlayedExact(banner)) throw new InvalidOperationException("指挥旌旗不在已打出区");
            unit.Banner = banner;
            unit.BannerOwner = player;
            unit.BorrowedBannerId = banner.Id == effectId ? null : effectId;
            context.CommandBannersPlayedThisTurn.RemoveAll(c => ReferenceEquals(c,banner));
            if (ReferenceEquals(context.CommandBannerForRecruit,banner)) context.CommandBannerForRecruit = null;
        }

        internal static void RecordRecruit(PlayerState player, ActionContext context, UnitState unit)
        {
            context.RecruitedUnitsThisTurn.Add(unit);
            if (context.CommandBannerForRecruit != null) AssignCommand(player,context,unit,context.CommandBannerForRecruit);
        }

        internal static void Release(UnitState unit)
        {
            var owner = unit.BannerOwner;
            var card = unit.Banner;
            string? effectId = unit.BannerEffectId;
            unit.BorrowedBannerId = null;
            unit.Banner = null;
            unit.BannerOwner = null;
            unit.FortitudeProtectionSelected = false;
            if (owner != null)
            {
                owner.BannersReturningAtRoundEnd.Remove(unit);
                if (card != null) owner.Deck.GainToDiscard(card);
                if (effectId == "items_017")
                {
                    owner.Units.Remove(unit);
                    if (!unit.IsDestroyed) unit.Destroy();
                }
            }
        }

        public static void ChooseRoundReturn(PlayerState player, UnitState unit, bool returnBanner)
        {
            if (unit == null || !player.Units.Contains(unit) || unit.Banner == null || unit.BannerOwner != player)
                throw new InvalidOperationException("只能选择自有部队上实际附着的旗帜");
            if (returnBanner) player.BannersReturningAtRoundEnd.Add(unit);
            else player.BannersReturningAtRoundEnd.Remove(unit);
        }

        internal static void EndRound(PlayerState player)
        {
            foreach (var unit in player.BannersReturningAtRoundEnd.ToArray()) Release(unit);
            player.BannersReturningAtRoundEnd.Clear();
        }

        public static bool FortitudeSpent(UnitState unit)
            => unit.BannerEffectId == "items_018" && unit.BannerOwner != null
                && unit.BannerOwner.BannersUsedThisRound.Any(c => ReferenceEquals(c,unit.Banner));

        public static void SelectFortitudeProtection(PlayerState player, UnitState unit, bool use)
        {
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed || unit.BannerEffectId != "items_018"
                || unit.BannerOwner != player || (use && FortitudeSpent(unit)))
                throw new InvalidOperationException("必须选择自有部队上本轮尚未使用的强韧旌旗");
            unit.FortitudeProtectionSelected = use;
        }

        internal static bool TryIgnoreWound(UnitState unit)
        {
            if (!unit.FortitudeProtectionSelected || unit.IsDestroyed || unit.BannerEffectId != "items_018"
                || unit.BannerOwner == null || FortitudeSpent(unit)) return false;
            unit.BannerOwner.BannersUsedThisRound.Add(unit.Banner);
            unit.FortitudeProtectionSelected = false;
            return true;
        }

        internal static void ClearProtectionChoices(PlayerState player)
        {
            foreach (var unit in player.Units) unit.FortitudeProtectionSelected = false;
        }

        internal static void StartRound(PlayerState player)
        {
            player.BannersUsedThisRound.Clear();
            ClearProtectionChoices(player);
        }

        public static int HonorBonus(ActionContext context, UnitState unit)
            => (unit.BannerEffectId == "items_000" ? 1 : 0) + context.HonorBannersThisTurn.Count;

        internal static void RewardHonorAction(PlayerState player, ActionContext context, UnitState unit, bool fromOffer)
        {
            if (unit.BannerEffectId == "items_000") player.Fame++;
            object identity = fromOffer ? (object)unit.Card : unit;
            foreach (var rewarded in context.HonorBannersThisTurn)
                if (!rewarded.Any(previous => ReferenceEquals(previous, identity)))
                {
                    rewarded.Add(identity);
                    player.Fame++;
                }
        }
    }
}
