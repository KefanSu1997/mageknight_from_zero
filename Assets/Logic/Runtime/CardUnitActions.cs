using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Spend card-granted influence on an owned unit, independently of its wound state.</summary>
    public static class CardUnitActions
    {
        /// <summary>原勇者的激活费用取决于实际突袭/进攻玩家身份，不取决于可被法术移除的城防。</summary>
        public static int ActivationInfluenceCost(ActionContext context, UnitState unit)
        {
            if (!context.InBattle || unit.Card.AssaultActivationInfluenceCost == 0) return 0;
            var place = context.CombatSite?.Type;
            bool assault = place == PlaceType.Keep || place == PlaceType.MageTower || place == PlaceType.City;
            return assault || context.TunnelTargetPlayer != null ? unit.Card.AssaultActivationInfluenceCost : 0;
        }

        public static void ValidateFreeRecruit(PlayerState player, ActionContext context, int index)
        {
            if (context.UnitOffer == null || index < 0 || index >= context.UnitOffer.Count)
                throw new InvalidOperationException("必须选择真实部队供应中的部队");
            if (context.UnitOffer[index].InteractionRecruitOnly)
                throw new InvalidOperationException("此部队只能通过交涉招募，不能免费取得");
            LoyaltyActions.ValidateRecruit(player,context);
            if (context.UseLoyaltySlot)
            {
                if (player.OrdinaryFreeSlots < 0) throw new InvalidOperationException("普通部队超过指挥槽，不能借专属槽掩盖超额");
                return;
            }
            if (context.CommandBannerForRecruit != null)
            {
                if (!BannerActions.CommandAvailable(player,context,context.CommandBannerForRecruit))
                    throw new InvalidOperationException("必须选择本回合实际打出且未附着的指挥旌旗");
                if (player.OrdinaryFreeSlots < 0 || context.UnitToDisband != null)
                    throw new InvalidOperationException("使用指挥旌旗招募时不能同时解散部队或保留超额普通部队");
                return;
            }
            if (player.OrdinaryFreeSlots <= 0)
            {
                if (context.UnitToDisband == null || !player.Units.Contains(context.UnitToDisband) || context.UnitToDisband.IsDestroyed)
                    throw new InvalidOperationException("指挥槽已满，必须选择解散一支自有部队");
                if (player.OrdinaryFreeSlots < 0) throw new InvalidOperationException("解散一支部队后仍无指挥槽");
                LoyaltyActions.ValidateDismiss(player,context.UnitToDisband);
                if (context.UnitToDisband.BannerEffectId == "items_017")
                    throw new InvalidOperationException("解散旗帜指挥的部队不能释放普通指挥槽");
            }
            else if (context.UnitToDisband != null)
                throw new InvalidOperationException("有空闲指挥槽时不能借招募解散部队");
        }

        /// <summary>原法术/神器立即取得所选供应卡。非交涉，不计声誉、地点或招募折扣。</summary>
        public static UnitState RecruitFree(PlayerState player, ActionContext context, int index)
        {
            ValidateFreeRecruit(player, context, index);
            var gained = new UnitState(context.UnitOffer![index]);
            if (context.UnitToDisband != null)
            {
                player.Disband(context.UnitToDisband);
            }
            context.UnitOffer.RemoveAt(index);
            player.Units.Add(gained);
            BannerActions.RecordRecruit(player,context,gained);
            LoyaltyActions.BindRecruit(player,context,gained);
            context.UnitToDisband = null;
            context.HasRecruited = true;
            player.Fame += context.FamePerRecruit;
            player.Reputation = Math.Clamp(player.Reputation + context.ReputationPerRecruit, -7, 7);
            return gained;
        }

        public static void Use(PlayerState player, ActionContext context, UnitState unit, int abilityIndex, int option = 0, int target = -1, UnitState? unitTarget = null, ManaColor[] manaColors = null, int boost = 0, int attackIndex = 0, ScoutTarget scoutTarget = null, SkillCard leadership = null, HeadbandBorrowResolution leadershipBorrow = null)
        {
            if (unit == null || !player.Units.Contains(unit)) throw new InvalidOperationException("只能发动自有部队");
            if (leadershipBorrow != null && leadership == null) throw new InvalidOperationException("头环领导授权必须指定实际公共领导技能");
            if (leadership != null) LeadershipActions.Validate(player, context, leadership, unit, abilityIndex, option, leadershipBorrow);
            if (DelphanaMastersActions.IsMasters(unit))
            {
                if (option != 0 || unitTarget != null || boost != 0 || manaColors != null && manaColors.Length != 0)
                    throw new InvalidOperationException("大师能力只需选择原能力和敌人目标");
                DelphanaMastersActions.Use(player, context, unit, abilityIndex, target);
                return;
            }
            ApplyAbility(player, context, unit, abilityIndex, option, target: target, unitTarget: unitTarget, manaColors: manaColors, boost: boost, attackIndex: attackIndex, scoutTarget: scoutTarget, leadershipBonus: leadership == null ? 0 : LeadershipActions.Bonus(unit.Card.Actions[abilityIndex], option));
            if (leadership != null && leadershipBorrow == null) player.SkillsUsedThisTurn.Add(leadership.SourceId);
        }

        /// <summary>战斗号召：供应原卡只提供一个能力，不转入队伍、耗竭供应或成为承伤目标。</summary>
        public static void UseFromOffer(PlayerState player, ActionContext context, int offerIndex, int abilityIndex, int option = 0, int target = -1, ManaColor[] manaColors = null, int boost = 0, ScoutTarget scoutTarget = null,UnitState unitTarget=null,int attackIndex=0)
        {
            if (context.OfferedUnitUses <= 0) throw new InvalidOperationException("本回合没有可用的战斗号召许可");
            if (context.UnitOffer == null || offerIndex < 0 || offerIndex >= context.UnitOffer.Count)
                throw new InvalidOperationException("必须选择真实部队供应中的部队");
            if (context.UnitOffer[offerIndex].InteractionRecruitOnly)
                throw new InvalidOperationException("此部队不能成为战斗号召的目标");
            var temporary = new UnitState(context.UnitOffer[offerIndex]);
            ApplyAbility(player, context, temporary, abilityIndex, option, true, target, unitTarget:unitTarget, manaColors: manaColors, boost: boost, scoutTarget: scoutTarget,attackIndex:attackIndex);
            context.OfferedUnitUses -= 1;
        }

        private static void ApplyAbility(PlayerState player, ActionContext context, UnitState unit, int abilityIndex, int option, bool fromOffer = false, int target = -1, UnitState? unitTarget = null, ManaColor[] manaColors = null, int boost = 0, int attackIndex = 0, ScoutTarget scoutTarget = null, int leadershipBonus = 0)
        {
            using var contributionSource = CardContributionSource.Scope(context, null);
            UnitParticipation.Require(player, context, unit);
            if (!unit.CanActivate) throw new InvalidOperationException("部队必须就绪、未受伤且未销毁");
            var abilities = unit.Card.Actions;
            if (abilities == null || abilityIndex < 0 || abilityIndex >= abilities.Length)
                throw new InvalidOperationException("部队能力尚未接入或选择无效");
            var ability = abilities[abilityIndex];
            if (ShockTroopsActions.IsReaction(ability))
                ShockTroopsActions.Validate(player, context, unit, ability, target, attackIndex);
            if (option < 0 || option > (ability.Kind == UnitAbilityKind.AttackOrBlock || ability.Kind == UnitAbilityKind.TransformAttacks || ability.Kind == UnitAbilityKind.MovementOrInfluence ? 1 : 0))
                throw new InvalidOperationException("部队能力选项无效");
            if (boost < 0 || boost > 3 || !ability.RedBlueBoost && boost != 0)
                throw new InvalidOperationException("额外魔力只能选择不支付、红、蓝或红蓝各一");
            if (ability.MovementPhaseOnly && (context.InBattle || context.TurnActionCommitted || context.PendingRepeatedEffect != null))
                throw new InvalidOperationException("此斥候能力只能在移动阶段、开始行动前使用");
            if (scoutTarget != null && ability.Kind != UnitAbilityKind.ScoutToken)
                throw new InvalidOperationException("只有斥候的查看能力需要标记目标");
            if (ability.Kind == UnitAbilityKind.GainChosenTokens)
            {
                if (manaColors == null || manaColors.Length != ability.Value || Array.Exists(manaColors, c => c < ManaColor.Red || c > ManaColor.Black))
                    throw new InvalidOperationException("必须为每枚魔力标记选择一种实际颜色");
            }
            else if (manaColors != null && manaColors.Length != 0)
                throw new InvalidOperationException("此部队能力不产生自选魔力");
            if (ability.Kind == UnitAbilityKind.TransformAttacks && !context.InBattle)
                throw new InvalidOperationException("攻击转换只能在本次战斗中发动");
            if (!context.InBattle && (ability.Kind == UnitAbilityKind.Attack || ability.Kind == UnitAbilityKind.Block || ability.Kind == UnitAbilityKind.AttackOrBlock))
                throw new InvalidOperationException("攻击和格挡能力必须在战斗中发动");
            if (ability.Kind == UnitAbilityKind.FreezeArmor)
            {
                if (context.CombatPhase != Phase.Ranged && context.CombatPhase != Phase.Block && context.CombatPhase != Phase.Melee)
                    throw new InvalidOperationException("请在远程、格挡或近战阶段发动冰霜剑士能力");
                // Same original ice-effect target immunity and live-enemy rules as Freeze.
                new CardEffects.FreezeEffect(true).Validate(player, context, target);
            }
            if (ability.Kind == UnitAbilityKind.StopUnfortifiedEnemy || ability.RemoveTargetFortifications || ability.RemoveTargetResistances)
            {
                // Use the existing live-target check; white/green effects do not inherit ice immunity.
                new CardEffects.ExposureEffect(false).Validate(player, context, target);
                if (ability.Kind == UnitAbilityKind.StopUnfortifiedEnemy)
                {
                    if (context.CombatPhase != Phase.Block)
                        throw new InvalidOperationException("幻术师停攻能力只能在格挡阶段使用");
                    if (CombatMath.IsFortified(context.Enemies[target], target, context))
                        throw new InvalidOperationException("幻术师必须选择没有城防的敌人");
                    if (context.Enemies[target].Abilities.Contains(Ability.MagicResist))
                        throw new InvalidOperationException("奥术免疫敌人不受幻术师停攻影响");
                }
                else if (context.CombatPhase != Phase.Ranged && context.CombatPhase != Phase.Melee)
                    throw new InvalidOperationException("巫师远程能力只能在远程或近战阶段使用");
            }
            if ((ability.Kind == UnitAbilityKind.Healing || ability.Kind == UnitAbilityKind.ReadyUnit) && context.InBattle)
                throw new InvalidOperationException("治疗与重整部队能力只能在战斗外使用");
            if (context.InBattle && ability.Kind == UnitAbilityKind.MovementOrInfluence && option == 0)
                throw new InvalidOperationException("战斗中不能选择魔宠的移动能力");
            if (ability.Kind == UnitAbilityKind.ReadyUnit &&
                (unitTarget == null || !player.Units.Contains(unitTarget) || unitTarget.IsDestroyed || unitTarget.IsReady ||
                 unitTarget.Card.Level < 1 || unitTarget.Card.Level > ability.Value))
                throw new InvalidOperationException("请选择一支耗竭且未销毁的自有1或2级部队");
            if ((ability.Kind == UnitAbilityKind.GainMana || ability.Kind == UnitAbilityKind.GainToken || ability.Kind == UnitAbilityKind.GainCrystal) &&
                (!ability.GrantedMana.HasValue || ability.GrantedMana.Value < ManaColor.Red || ability.GrantedMana.Value > ManaColor.White || ability.Value < 1))
                throw new InvalidOperationException("部队魔力能力缺少合法颜色或数量");
            var costs = new List<ManaColor>(ability.Cost);
            if ((boost & 1) != 0) costs.Add(ManaColor.Red);
            if ((boost & 2) != 0) costs.Add(ManaColor.Blue);
            int influenceCost = ActivationInfluenceCost(context, unit);
            if (influenceCost > 0 && context.InfluencePool < influenceCost)
                throw new InvalidOperationException("突袭或进攻其他玩家时，激活勇者须另付2点影响力");
            var observation = ability.Kind == UnitAbilityKind.ScoutToken ? ScoutsActions.Preview(player, context, scoutTarget) : null;
            if (!player.Mana.TryPayExactColors(costs.ToArray(), context, player))
                throw new InvalidOperationException("部队能力所需魔力不足");
            context.InfluencePool -= influenceCost;
            // Immunity cancels the enemy modifier, while the independent printed ranged attack remains.
            if ((ability.RemoveTargetFortifications || ability.RemoveTargetResistances)
                && !context.Enemies[target].Abilities.Contains(Ability.MagicResist))
            {
                bool affected = ability.RemoveTargetFortifications && CombatMath.IsFortified(context.Enemies[target], target, context)
                    || ability.RemoveTargetResistances && new[] { Element.Physical, Element.Fire, Element.Ice, Element.ColdFire }
                        .Any(e => CombatMath.HasResistance(context.Enemies[target], target, e, context));
                if (ability.RemoveTargetFortifications) context.FortificationsRemoved.Add(target);
                if (ability.RemoveTargetResistances) context.ResistancesRemoved.Add(target);
                if (affected) DuelActions.RecordUnitEffect(context, context.Enemies[target]);
            }
            int unitBonus = context.ChargeUnitBonus + BannerActions.HonorBonus(context, unit) + leadershipBonus;
            int value = FamiliarActions.AbilityValue(unit, ability);
            int power = value + unitBonus + 2 * (costs.Count - ability.Cost.Length);
            switch (ability.Kind)
            {
                case UnitAbilityKind.Attack: context.CombatPower.AddAttack(value + unitBonus, ability.Element, ability.AttackType, sourceUnit: fromOffer ? null : unit, fromUnitAbility: true); break;
                case UnitAbilityKind.Block: context.CombatPower.AddBlock(value + unitBonus, ability.Element, doubleAgainstSwift: ability.DoubleAgainstSwift, sourceUnit: fromOffer ? null : unit, fromUnitAbility: true); break;
                case UnitAbilityKind.AttackOrBlock:
                    if (option == 0) context.CombatPower.AddAttack(power, ability.Element, ability.AttackType, sourceUnit: fromOffer ? null : unit, fromUnitAbility: true);
                    else context.CombatPower.AddBlock(power, ability.Element, sourceUnit: fromOffer ? null : unit, fromUnitAbility: true);
                    break;
                case UnitAbilityKind.GainChosenTokens:
                    foreach (var color in manaColors) player.Mana.AddToken(color, 1);
                    break;
                case UnitAbilityKind.TransformAttacks:
                    if (option == 0) { context.AllAttacksColdFire = true; context.Duel.UnitColdFire = true; }
                    else { context.AllAttacksSiege = true; context.Duel.UnitSiege = true; }
                    break;
                case UnitAbilityKind.FreezeArmor:
                    if (!context.SkipAttackIndices.Contains(target) || CombatMath.EffectiveArmor(context.Enemies[target], target, context) > 1)
                        DuelActions.RecordUnitEffect(context, context.Enemies[target]);
                    context.SkipAttackIndices.Add(target);
                    context.AddUnitArmorReduction(context.Enemies[target], ability.Value);
                    break;
                case UnitAbilityKind.Movement:
                    context.MovementPool += ability.Value;
                    context.ScoutExplorationRange = Math.Max(context.ScoutExplorationRange, ability.ExplorationRange);
                    if (ability.MovementReductionTerrains != null)
                        foreach (var terrain in ability.MovementReductionTerrains)
                            context.TerrainMoveReductionToZero[terrain] =
                                context.TerrainMoveReductionToZero.GetValueOrDefault(terrain) + ability.MovementReduction;
                    break;
                case UnitAbilityKind.Influence: context.InfluencePool += ability.Value; break;
                case UnitAbilityKind.Healing: context.Healing.Add(value); break;
                case UnitAbilityKind.MovementOrInfluence:
                    if (option == 0) context.MovementPool += value;
                    else context.InfluencePool += value;
                    break;
                case UnitAbilityKind.ScoutToken: ScoutsActions.Observe(context, observation); break;
                case UnitAbilityKind.ReadyUnit: unitTarget!.Ready(); break;
                case UnitAbilityKind.AllUnitResistance: context.UnitsResistAll = true; break;
                case UnitAbilityKind.GainToken:
                    player.Mana.AddToken(ability.GrantedMana.Value, ability.Value);
                    break;
                case UnitAbilityKind.GainCrystal:
                    player.Mana.AddCrystal(ability.GrantedMana.Value, ability.Value);
                    break;
                case UnitAbilityKind.StopUnfortifiedEnemy:
                    if (context.SkipAttackIndices.Add(target)) DuelActions.RecordUnitEffect(context, context.Enemies[target]);
                    break;
                case UnitAbilityKind.GainMana:
                    player.Mana.AddToken(ability.GrantedMana.Value, ability.Value);
                    player.Mana.AddCrystal(ability.GrantedMana.Value, ability.Value);
                    break;
                case UnitAbilityKind.WeakenEnemyAttack:
                case UnitAbilityKind.InterceptEnemyAttack:
                    ShockTroopsActions.Apply(context, unit, ability, target, attackIndex);
                    DuelActions.RecordUnitEffect(context, context.Enemies[target]);
                    break;
            }
            if (ability.OtherUnitAttackBonus > 0)
                context.OtherUnitAttackBonuses.Add((unit, ability.OtherUnitAttackBonus));
            unit.Exhaust();
            if (ability.Kind == UnitAbilityKind.Attack || ability.Kind == UnitAbilityKind.Block || ability.Kind == UnitAbilityKind.AttackOrBlock)
                BannerActions.RewardHonorAction(player, context, unit, fromOffer);
            if (context.InBattle) context.CombatActionStarted = true;
            if (ability.ReputationChange != 0) player.Reputation = Math.Clamp(player.Reputation + ability.ReputationChange,-7,7);
            if (ability.InteractionReputationChange != 0 && context.InNegotiation)
                player.Reputation = Math.Clamp(player.Reputation + ability.InteractionReputationChange, -7, 7);
            if (ability.SelfWounds > 0) unit.AddWounds(ability.SelfWounds);
        }

        public static bool TryRecruit(PlayerState player, ActionContext context, IList<UnitCard> offer, int index, RecruitLocation location, ManaColor? familiarMana = null)
        {
            if (context.TemporalPortalTurn) return false;
            if (index < 0 || index >= offer.Count) return false;
            var card = offer[index];
            if (location != RecruitLocation.MagicalGlade && (context.InBattle || context.InteractionClosed
                || (!context.InteractionReputationModifier.HasValue && (player.Reputation <= -7 && !HeroSocialSkillRules.ReputationEffectsIgnored(context) || context.TurnActionCommitted)))) return false;
            int cost = RecruitmentService.GetCost(player, card, location, context);
            if (!new RecruitmentService().TryRecruit(player, card, location, context.InfluencePool, context, familiarMana: familiarMana)) return false;
            context.InfluencePool -= cost;
            offer.RemoveAt(index);
            context.TurnActionCommitted = true;
            return true;
        }

        public static bool TryReady(PlayerState player, ActionContext context, UnitState unit, bool peacefulMoment = false)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (peacefulMoment && !PeacefulMomentActions.CanAct(context, ready: true)) return false;
            int rate = peacefulMoment ? 2 : context.ReadyInfluencePerLevel;
            int maxLevel = peacefulMoment ? int.MaxValue : context.ReadyUnitMaxLevel;
            if (unit == null || !player.Units.Contains(unit) || unit.IsDestroyed || unit.IsReady
                || rate <= 0 || unit.Card.Level < 1
                || unit.Card.Level > maxLevel) return false;
            int cost = unit.Card.Level * rate;
            if (context.InfluencePool < cost) return false;
            context.InfluencePool -= cost;
            unit.Ready();
            if (peacefulMoment) PeacefulMomentActions.Commit(context);
            return true;
        }
    }
}
