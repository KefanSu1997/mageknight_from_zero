using MK.Logic.Runtime.Map;
using MK.Logic.Runtime;
using MK.Logic.Core;
using System.Linq;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 負責處理單位玩家回合開始與結束時的共通行為。
    /// </summary>
    public sealed class PlayerTurnEngine
    {
        private readonly IGameLogger? _logger;

        public PlayerTurnEngine(IGameLogger? logger = null)
        {
            _logger = logger;
        }
        /// <summary>
        /// 回合開始不重整部隊；部隊只在新一輪或卡牌明確允許時重整。
        /// </summary>
        public void StartTurn(
            PlayerState p,
            DayPart part,
            GlobalResources? globals = null,
            Map.MapState? map = null)
        {
            _logger?.Log($"StartTurn P{p.Id}");
            SkillActions.StartTurn(p);
            if (p.Tactic != null) p.Tactic.ManaSearchUsedThisTurn = false;
            globals?.Mana.BeginPlayerTurn(p);
            p.Deck.BeginTurnWounds();
            p.Deck.BeginTurnCards();
            // 開啟回合時重置持有骰，防止上一回合遺留
            p.HeldManaDie = null;

            // 發放因法術獲得的每回合法力標記
            foreach (var pair in p.TokensPerTurn)
                p.Mana.AddToken(pair.Key, pair.Value);
            SiteTurnRules.BeginTurn(p, part, map);
            p.ManaCurseTriggered = false;
            if (globals != null && map != null)
                _logger?.LogFullState(p, globals, map);
        }

        /// <summary>
        /// 與舊版相容的簡化入口。
        /// </summary>
        public void StartTurn(PlayerState p, DayPart part) =>
            StartTurn(p, part, null, null);

        /// <summary>
        /// 回合結束：清空臨時法力並依照上限摸牌。
        /// </summary>
        public void EndTurn(PlayerState p, DayPart part, ManaSource source, ActionContext? ctx = null, bool deferDraw = false, SiteTurnSelection siteSelection = null, bool deferRewards = false)
        {
            SiteTurnRules.ValidateEndTurn(p, ctx?.MovementMap, siteSelection);
            source?.RequireSourceOpeningReturnChoices(p);
            RepeatedEffectResolution.RequireCompleted(ctx);
            if (ctx?.RampagingBattle != null && ctx.InBattle)
                throw new System.InvalidOperationException("必须先完成被激怒敌人的战斗");
            if (ctx?.TimeBendingCard != null && !p.Deck.PlayedCards.Any(c => ReferenceEquals(c, ctx.TimeBendingCard)))
                throw new System.InvalidOperationException("时间扭曲来源不在本人的已打出区");
            if (ctx?.ManaMeltdownOwner != null)
                throw new System.InvalidOperationException("请先选择或放弃魔力熔毁的魔晶");
            _logger?.Log($"EndTurn P{p.Id}");
            if (ctx?.TunnelAssaultActive == true)
                throw new System.InvalidOperationException("地道突袭必须先完成战斗与撤退结算");
            if(!deferRewards)ValidateEndRewards(ctx);
            CardEffects.MysticBoxEffect.ResolveEndTurn(p, ctx);
            CardEffects.MountainLoreEffect.ResolveEndTurn(p, ctx);
            CardEffects.JoyfulManaEffect.ResolveReturn(p, ctx);
            CardEffects.SteadyTempoEffect.ResolveReturn(p, ctx);
            BannerActions.ClearProtectionChoices(p);
            p.Deck.EndTurnWounds(ctx?.RemoveNewWoundsAtTurnEnd == true);
            if (ctx != null)
            {
                ctx.RemoveNewWoundsAtTurnEnd = false;
                ctx.ProtectionBannerBurned = false;
            }
            (source ?? ctx?.ManaSource)?.ReturnPlayerDice(p);
            if (ctx != null)
            {
                ctx.ExtraManaDice = ctx.ExtraBlackManaDice = 0;
                ctx.BlackManaWild = ctx.GoldManaWild = false;
                ctx.SidewaysWoundValue = 0;
                ctx.SoulGuideBlockPending = false;
                ctx.AmbushAttackBonus = ctx.AmbushBlockBonus = 0;
                ctx.AmbushTriggered = false;
                ctx.ManaDiceToTransform.Clear();
                ctx.SelectedActionManaColor = null;
                ctx.ManaPowerBonusChoices.Clear();
                ctx.ClearBloodSurge();
                ctx.ClearColdStare();
                ctx.ClearMindRead();
                if (ctx.ReturnSpentCrystals)
                {
                    foreach (var pair in ctx.SpentCrystals)
                        p.Mana.AddCrystal(pair.Key, pair.Value);
                    ctx.ReturnSpentCrystals = false;
                }
                if (ctx.CardToRecycle != null)
                {
                    p.Deck.RemoveFromHand(ctx.CardToRecycle);
                    if (ctx.RecycleToTop)
                        p.Deck.PutOnTop(ctx.CardToRecycle);
                    else if (ctx.RecycleToBottom && p.Deck.DrawPileCount > 0)
                        p.Deck.PutUnder(ctx.CardToRecycle);
                    ctx.CardToRecycle = null;
                }
                ctx.SpentCrystals.Clear();
                ctx.RecycleToTop = ctx.RecycleToBottom = false;
                ctx.ClearMovementAndReadyEffects();
                // 未使用的攻击和格挡不能存到下一回合，跳过补牌也须清理。
                ctx.CombatPower.ConsumeAttacks(Phase.Melee);
                ctx.AllAttacksColdFire = false;
                ctx.AllAttacksSiege = false;
                ctx.CombatPower.ConsumeBlocks();
                ctx.AllowGoldAtNight = ctx.AllowBlackAtDay = ctx.RevealSitesAsDay = false;
                ctx.ClearInteraction();
                HeroSocialSkillRules.ClearTurn(ctx);
                ctx.MastersUsedAbilities.Clear();
                ctx.MastersActiveThisBattle.Clear();
                ctx.MastersDestroyWhenBlocked.Clear();
                ctx.OtherUnitAttackBonuses.Clear();
                ctx.ForcedDamageUnits.Clear();
                ctx.UnitToDisband = null;
                ctx.OfferedUnitUses = 0;
                ctx.ChargeUnitBonus = 0;
                ctx.HonorBannersThisTurn.Clear();
                ctx.CommandBannersPlayedThisTurn.Clear();
                ctx.RecruitedUnitsThisTurn.Clear();
                ctx.CommandBannerForRecruit = null;
                ctx.NoUnitDamage = false;
                ctx.CombatActionStarted = false;
                ctx.CharmRecruitDiscount = 0;
                ctx.SpellColors.Clear();
                ctx.LearnUsed = false;
                ctx.LearningModes = 0;
                CardTravelActions.EndTemporalPortal(ctx);
                ctx.SpaceBendingActive = ctx.IgnoreRampaging = false;
                ctx.ScoutedEnemies.Clear();
                ctx.LastScoutObservation = null;
                ctx.ScoutExplorationRange = 1;
                ctx.TemporalPortalTurn = ctx.TurnActionCommitted = false;
                ctx.MovedThisTurn = ctx.RestedThisTurn = false;
                ctx.SwordFamePerKill = ctx.BowFamePerKill = 0;
                ctx.CrystalsPerKill = 0;
                ctx.ClearKillCrystalChoices();
                ctx.DoublePhysicalAttack = ctx.IgnorePhysicalResist = ctx.StarBowActive = false;
                // 戒指焚毁的无尽魔力与施法奖励仅持续本回合，跳过补牌也必须失效。
                ctx.InfiniteMana.Clear();
                ctx.FamePerSpell.Clear();
                ctx.Healing.Clear();
                ctx.ClearDiseaseBattleState();
                ctx.ClearExplosiveBoltArmor();
                ctx.ClearCounterStrikeTurnState();
                ctx.ClearBattleRage();
                ctx.ClearAttackReductions();
                // Chill's resistance/armor changes last until turn end, not just combat end.
                // Clear before the SkipDraw early return as both end-turn paths expire them.
                ctx.ArmorReduction.Clear();
                ctx.IgnoreSiteFortifications = false;
                ctx.FireResistRemoved.Clear();
                ctx.SkipAttackIndices.Clear();
                ctx.CancelledAttacks.Clear();
                ctx.SuccessfullyBlockedAttacks.Clear();
                ctx.NightWingsSkippedIndices.Clear();
                ctx.PossessedEnemyIndices.Clear();
                ctx.HealedHandWoundsThisTurn = 0;
                ctx.HealedUnitsThisTurn.Clear();
                ctx.DrawPerHeal = 0;
                ctx.ReadyUnitOnHeal = false;
                // 迷雾帷幕持续本回合；完全由部队吸收的伤害不会消费英雄免伤。
                ctx.UnitsResistAll = false;
                ctx.IgnoreNextWound = false;
            }
            if (ctx?.TimeBendingCard != null)
            {
                p.Deck.ResolveTimeBending(ctx.TimeBendingCard);
                ctx.TimeBendingCard = null;
                ctx.SkipDraw = true;
                ctx.ExtraTurn = true;
            }
            else if (ctx?.TimeBendingReturnsPlayed == true)
            {
                // A borrowed spell has no owned spell instance to set aside.
                // Its discarded material and burned artifact keep their zones.
                p.Deck.ReturnTimeBendingPlayed();
            }
            if (ctx != null) ctx.TimeBendingReturnsPlayed = false;
            p.Deck.DiscardPlayedCards();
            p.Mana.ResetTokens();
            SiteTurnRules.EndTurn(p, ctx?.MovementMap, siteSelection);
            if(!deferRewards)GrantEndRewards(p,ctx);
            if (!deferDraw) DrawAfterEndTurn(p, ctx);
        }
        private static void ValidateEndRewards(ActionContext ctx)
        {
            FortifiedSiteActions.ValidateSpellReward(ctx);
            ctx?.Dungeon?.ValidateReward();ctx?.Lair?.ValidateReward();ctx?.Ruin?.ValidateReward();
        }
        internal void GrantEndRewards(PlayerState p,ActionContext ctx)
        {
            ValidateEndRewards(ctx);
            FortifiedSiteActions.GrantSpellReward(p,ctx);
            ctx?.Dungeon?.GrantReward(p);ctx?.Lair?.GrantReward(p);ctx?.Ruin?.GrantReward(p);
        }

        /// <summary>After rewards/level-up choices; callers must not repeat turn cleanup.</summary>
        public void DrawAfterEndTurn(PlayerState p, ActionContext? ctx)
        {
            if (ctx != null && ctx.SkipDraw)
            {
                ctx.SkipDraw = false;
                SkillActions.EndTurn(p);
                return;
            }
            int limit = p.HandLimit() + FortifiedSiteActions.HandBonus(p, ctx?.MovementMap) + TacticActions.PlanningDrawBonus(p);
            if (ctx != null && ctx.NextDrawBonus > 0)
            {
                limit += ctx.NextDrawBonus;
                ctx.NextDrawBonus = 0;
            }
            p.Deck.DrawToLimit(limit);
            SkillActions.EndTurn(p);
            _logger?.Log($"EndTurnComplete P{p.Id}");
        }
    }
}
