using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data.Cards;
using MK.Logic.Data;
using MK.Logic.Runtime.CardEffects;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 簡化的行動牌執行系統，僅示範讀取效果並套用。
    /// </summary>
    public sealed class ActionSystem
    {
        private readonly Dictionary<string, ActionEffectId> _baseMap = new();
        private readonly Dictionary<string, ActionEffectId> _enhMap = new();
        private readonly Dictionary<string, ManaColor> _spellColor = new();

        /// <summary>
        /// 嘗試以玩家持有的法力標記或晶體支付指定顏色需求。
        /// </summary>
        private static bool PayColors(PlayerState player, ActionContext ctx, ManaColor[] colors)
        {
            foreach (var c in colors)
            {
                if (ctx.InfiniteMana.Contains(c))
                    continue;
                if (player.Mana.Tokens.TryGetValue(c, out int t) && t > 0)
                {
                    if (--t == 0) player.Mana.Tokens.Remove(c); else player.Mana.Tokens[c] = t;
                    continue;
                }

                if (player.Mana.Crystals.TryGetValue(c, out int cr) && cr > 0)
                {
                    if (--cr == 0) player.Mana.Crystals.Remove(c); else player.Mana.Crystals[c] = cr;
                    ctx.SpentCrystals[c] = ctx.SpentCrystals.GetValueOrDefault(c) + 1;
                    continue;
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// 初始化時注入各牌 ID 與對應效果。
        /// </summary>
        public ActionSystem()
        {
            foreach (var s in CardJsonLoader.LoadSpells())
                _spellColor[s.Id] = s.ManaColor;
            // 映射編號 000-009 的基礎行動牌
            _baseMap["basic_card_000"] = ActionEffectId.Move2;                // 行進
            _enhMap["basic_card_000"]  = ActionEffectId.Move4;

            _baseMap["basic_card_001"] = ActionEffectId.HealOrDraw1;          // 寧靜
            _enhMap["basic_card_001"]  = ActionEffectId.HealOrDraw2;

            _baseMap["basic_card_002"] = ActionEffectId.GainManaBWR;          // 凝結
            _enhMap["basic_card_002"]  = ActionEffectId.ConcentrationBoost;

            _baseMap["basic_card_003"] = ActionEffectId.WildHarvestBase;      // 野蠻收穫
            _enhMap["basic_card_003"]  = ActionEffectId.WildHarvestEnhanced;

            _baseMap["basic_card_004"] = ActionEffectId.EarthStrengthBase;    // 大地之子
            _enhMap["basic_card_004"]  = ActionEffectId.EarthStrengthEnhanced;

            _baseMap["basic_card_005"] = ActionEffectId.RefreshingBase;       // 活力煥發
            _enhMap["basic_card_005"]  = ActionEffectId.RefreshingEnhanced;

            _baseMap["basic_card_006"] = ActionEffectId.WillFocusBase;        // 全神貫注
            _enhMap["basic_card_006"]  = ActionEffectId.WillFocusBoost;

            _baseMap["basic_card_007"] = ActionEffectId.Move2;                // 敏捷
            _enhMap["basic_card_007"]  = ActionEffectId.RangedAttack3;

            _baseMap["basic_card_008"] = ActionEffectId.Influence2;           // 承諾
            _enhMap["basic_card_008"]  = ActionEffectId.Influence4;

            _baseMap["basic_card_009"] = ActionEffectId.ManaDrawBase;         // 魔力汲取
            _enhMap["basic_card_009"]  = ActionEffectId.ManaDrawEnhanced;

            // 編號 010-019
            _baseMap["basic_card_010"] = ActionEffectId.QuickReflexesBase;   // 迅捷反應
            _enhMap["basic_card_010"]  = ActionEffectId.QuickReflexesEnhanced;

            _baseMap["basic_card_011"] = ActionEffectId.ThrowingAxeBase;    // 利斧投擲
            _enhMap["basic_card_011"]  = ActionEffectId.ThrowingAxeEnhanced;

            _baseMap["basic_card_012"] = ActionEffectId.NobleMannersBase;   // 高貴氣質
            _enhMap["basic_card_012"]  = ActionEffectId.NobleMannersEnhanced;

            _baseMap["basic_card_013"] = ActionEffectId.ManaPullBase;       // 魔力征引
            _enhMap["basic_card_013"]  = ActionEffectId.ManaPullEnhanced;

            _baseMap["basic_card_014"] = ActionEffectId.Move2;              // 耐力
            _enhMap["basic_card_014"]  = ActionEffectId.Move4;

            _baseMap["basic_card_015"] = ActionEffectId.CrystallizeBase;    // 晶化
            _enhMap["basic_card_015"]  = ActionEffectId.CrystallizeEnhanced;

            _baseMap["basic_card_016"] = ActionEffectId.DeterminationBase;  // 決心
            _enhMap["basic_card_016"]  = ActionEffectId.DeterminationEnhanced;

            _baseMap["basic_card_017"] = ActionEffectId.MoveBoostBase;      // 永不止步
            _enhMap["basic_card_017"]  = ActionEffectId.MoveBoostEnhanced;

            _baseMap["basic_card_018"] = ActionEffectId.DruidicPathsBase;   // 德魯伊之道
            _enhMap["basic_card_018"]  = ActionEffectId.DruidicPathsEnhanced;

            _baseMap["basic_card_019"] = ActionEffectId.JoyfulManaBase;     // 歡悅魔晶
            _enhMap["basic_card_019"]  = ActionEffectId.JoyfulManaEnhanced;

            _baseMap["basic_card_020"] = ActionEffectId.IcyShellBase;       // 寒冰護體
            _enhMap["basic_card_020"]  = ActionEffectId.IcyShellEnhanced;

            _baseMap["basic_card_021"] = ActionEffectId.FuryBase;          // 狂怒
            _enhMap["basic_card_021"]  = ActionEffectId.FuryEnhanced;

            _baseMap["basic_card_022"] = ActionEffectId.ThreatenBase;      // 威脅
            _enhMap["basic_card_022"]  = ActionEffectId.ThreatenEnhanced;

            _baseMap["basic_card_023"] = ActionEffectId.ImprovisationBase; // 隨機應變
            _enhMap["basic_card_023"]  = ActionEffectId.ImprovisationEnhanced;

            _baseMap["basic_card_024"] = ActionEffectId.VersatileBattleBase; // 靈活戰鬥
            _enhMap["basic_card_024"]  = ActionEffectId.VersatileBattleEnhanced;

            _baseMap["basic_card_025"] = ActionEffectId.BattleRageBase;    // 戰鬥之怒
            _enhMap["basic_card_025"]  = ActionEffectId.BattleRageEnhanced;

            _baseMap["basic_card_026"] = ActionEffectId.IntimidateBase;    // 無情威壓
            _enhMap["basic_card_026"]  = ActionEffectId.IntimidateEnhanced;

            _baseMap["basic_card_027"] = ActionEffectId.UnknownBase;
            _enhMap["basic_card_027"]  = ActionEffectId.UnknownEnhanced;

            // ─── 高級行動牌 000-008 ─────────────────────────────
            _baseMap["advanced_card_000"] = ActionEffectId.FireBoltBase;
            _enhMap["advanced_card_000"]  = ActionEffectId.FireBoltEnhanced;

            _baseMap["advanced_card_001"] = ActionEffectId.IceBoltBase;
            _enhMap["advanced_card_001"]  = ActionEffectId.IceBoltEnhanced;

            _baseMap["advanced_card_002"] = ActionEffectId.SwiftBoltBase;
            _enhMap["advanced_card_002"]  = ActionEffectId.SwiftBoltEnhanced;

            _baseMap["advanced_card_003"] = ActionEffectId.ShatterBoltBase;
            _enhMap["advanced_card_003"]  = ActionEffectId.ShatterBoltEnhanced;

            _baseMap["advanced_card_004"] = ActionEffectId.BloodRageBase;
            _enhMap["advanced_card_004"]  = ActionEffectId.BloodRageEnhanced;

            _baseMap["advanced_card_005"] = ActionEffectId.IceShieldBase;
            _enhMap["advanced_card_005"]  = ActionEffectId.IceShieldEnhanced;

            _baseMap["advanced_card_006"] = ActionEffectId.AgilityBase;
            _enhMap["advanced_card_006"]  = ActionEffectId.AgilityEnhanced;

            _baseMap["advanced_card_007"] = ActionEffectId.RefreshingWalkBase;
            _enhMap["advanced_card_007"]  = ActionEffectId.RefreshingWalkEnhanced;

            _baseMap["advanced_card_008"] = ActionEffectId.CoercionBase;
            _enhMap["advanced_card_008"]  = ActionEffectId.CoercionEnhanced;

            _baseMap["advanced_card_009"] = ActionEffectId.FrostBridgeBase;
            _enhMap["advanced_card_009"]  = ActionEffectId.FrostBridgeEnhanced;

            _baseMap["advanced_card_010"] = ActionEffectId.WindSongBase;
            _enhMap["advanced_card_010"]  = ActionEffectId.WindSongEnhanced;

            _baseMap["advanced_card_011"] = ActionEffectId.PathFindingBase;
            _enhMap["advanced_card_011"]  = ActionEffectId.PathFindingEnhanced;

            _baseMap["advanced_card_012"] = ActionEffectId.BloodRitualBase;
            _enhMap["advanced_card_012"]  = ActionEffectId.BloodRitualEnhanced;

            _baseMap["advanced_card_013"] = ActionEffectId.PureMagicBase;
            _enhMap["advanced_card_013"]  = ActionEffectId.PureMagicEnhanced;

            _baseMap["advanced_card_014"] = ActionEffectId.DeedsOfGloryBase;
            _enhMap["advanced_card_014"]  = ActionEffectId.DeedsOfGloryEnhanced;

            _baseMap["advanced_card_015"] = ActionEffectId.RegenerationBase;
            _enhMap["advanced_card_015"]  = ActionEffectId.RegenerationEnhanced;

            _baseMap["advanced_card_016"] = ActionEffectId.ChargeBase;
            _enhMap["advanced_card_016"]  = ActionEffectId.ChargeEnhanced;

            _baseMap["advanced_card_017"] = ActionEffectId.SteadyTempoBase;
            _enhMap["advanced_card_017"]  = ActionEffectId.SteadyTempoEnhanced;

            _baseMap["advanced_card_018"] = ActionEffectId.DiplomacyBase;
            _enhMap["advanced_card_018"]  = ActionEffectId.DiplomacyEnhanced;

            _baseMap["advanced_card_019"] = ActionEffectId.CallForHelpBase;
            _enhMap["advanced_card_019"]  = ActionEffectId.CallForHelpEnhanced;

            _baseMap["advanced_card_020"] = ActionEffectId.DecomposeBase;
            _enhMap["advanced_card_020"]  = ActionEffectId.DecomposeEnhanced;

            _baseMap["advanced_card_021"] = ActionEffectId.CrystalMasteryBase;
            _enhMap["advanced_card_021"]  = ActionEffectId.CrystalMasteryEnhanced;

            _baseMap["advanced_card_022"] = ActionEffectId.ManaStormBase;
            _enhMap["advanced_card_022"]  = ActionEffectId.ManaStormEnhanced;

            _baseMap["advanced_card_023"] = ActionEffectId.AmbushBase;
            _enhMap["advanced_card_023"]  = ActionEffectId.AmbushEnhanced;

            _baseMap["advanced_card_024"] = ActionEffectId.OverloadBase;
            _enhMap["advanced_card_024"]  = ActionEffectId.OverloadEnhanced;

            _baseMap["advanced_card_025"] = ActionEffectId.MagicTalentBase;
            _enhMap["advanced_card_025"]  = ActionEffectId.MagicTalentEnhanced;
            _baseMap["advanced_card_026"] = ActionEffectId.LearningBase;
            _enhMap["advanced_card_026"]  = ActionEffectId.LearningEnhanced;
            _baseMap["advanced_card_027"] = ActionEffectId.PracticeBase;
            _enhMap["advanced_card_027"]  = ActionEffectId.PracticeEnhanced;
            _baseMap["advanced_card_028"] = ActionEffectId.CounterStrikeBase;
            _enhMap["advanced_card_028"]  = ActionEffectId.CounterStrikeEnhanced;
            _baseMap["advanced_card_029"] = ActionEffectId.RitualAttackBase;
            _enhMap["advanced_card_029"]  = ActionEffectId.RitualAttackEnhanced;
            _baseMap["advanced_card_030"] = ActionEffectId.AncientBloodlineBase;
            _enhMap["advanced_card_030"]  = ActionEffectId.AncientBloodlineEnhanced;
            _baseMap["advanced_card_031"] = ActionEffectId.ShieldBashBase;
            _enhMap["advanced_card_031"]  = ActionEffectId.ShieldBashEnhanced;
            _baseMap["advanced_card_032"] = ActionEffectId.TimeBendingBase;
            _enhMap["advanced_card_032"]  = ActionEffectId.TimeBendingEnhanced;
            _baseMap["advanced_card_033"] = ActionEffectId.SpellForgeBase;
            _enhMap["advanced_card_033"]  = ActionEffectId.SpellForgeEnhanced;
            _baseMap["advanced_card_034"] = ActionEffectId.KnightlySpiritBase;
            _enhMap["advanced_card_034"]  = ActionEffectId.KnightlySpiritEnhanced;

            _baseMap["advanced_card_035"] = ActionEffectId.TranquilTimeBase;
            _enhMap["advanced_card_035"]  = ActionEffectId.TranquilTimeEnhanced;
            _baseMap["advanced_card_036"] = ActionEffectId.OutmaneuverBase;
            _enhMap["advanced_card_036"]  = ActionEffectId.OutmaneuverEnhanced;
            _baseMap["advanced_card_037"] = ActionEffectId.PerseveranceBase;
            _enhMap["advanced_card_037"]  = ActionEffectId.PerseveranceEnhanced;
            _baseMap["advanced_card_038"] = ActionEffectId.NatureForceBase;
            _enhMap["advanced_card_038"]  = ActionEffectId.NatureForceEnhanced;
            _baseMap["advanced_card_039"] = ActionEffectId.MountainLoreBase;
            _enhMap["advanced_card_039"]  = ActionEffectId.MountainLoreEnhanced;
            _baseMap["advanced_card_040"] = ActionEffectId.ManaPowerBase;
            _enhMap["advanced_card_040"]  = ActionEffectId.ManaPowerEnhanced;
            _baseMap["advanced_card_041"] = ActionEffectId.ExplosiveBoltsBase;
            _enhMap["advanced_card_041"]  = ActionEffectId.ExplosiveBoltsEnhanced;
            _baseMap["advanced_card_042"] = ActionEffectId.BloodSurgeBase;
            _enhMap["advanced_card_042"]  = ActionEffectId.BloodSurgeEnhanced;
            _baseMap["advanced_card_043"] = ActionEffectId.ColdStareBase;
            _enhMap["advanced_card_043"]  = ActionEffectId.ColdStareEnhanced;

            // ─── 法術牌 000～011 ────────────────────────────
            _baseMap["magic_000"] = ActionEffectId.EnergyFlowBase;
            _enhMap["magic_000"]  = ActionEffectId.EnergyStealEnhanced;
            _baseMap["magic_001"] = ActionEffectId.CureBase;
            _enhMap["magic_001"]  = ActionEffectId.InfectEnhanced;
            _baseMap["magic_002"] = ActionEffectId.MeditationBase;
            _enhMap["magic_002"]  = ActionEffectId.TranceEnhanced;
            _baseMap["magic_003"] = ActionEffectId.EarthTremorBase;
            _enhMap["magic_003"]  = ActionEffectId.EarthQuakeEnhanced;
            _baseMap["magic_004"] = ActionEffectId.TunnelTravelBase;
            _enhMap["magic_004"]  = ActionEffectId.TunnelAttackEnhanced;
            _baseMap["magic_005"] = ActionEffectId.RestoreBase;
            _enhMap["magic_005"]  = ActionEffectId.RebirthEnhanced;
            _baseMap["magic_006"] = ActionEffectId.ManaMeltdownBase;
            _enhMap["magic_006"]  = ActionEffectId.ManaRadiationEnhanced;
            _baseMap["magic_007"] = ActionEffectId.DemolishBase;
            _enhMap["magic_007"]  = ActionEffectId.DisintegrateEnhanced;
            _baseMap["magic_008"] = ActionEffectId.BurningShieldBase;
            _enhMap["magic_008"]  = ActionEffectId.BlastShieldEnhanced;
            _baseMap["magic_009"] = ActionEffectId.FireballBase;
            _enhMap["magic_009"]  = ActionEffectId.FirestormEnhanced;
            _baseMap["magic_010"] = ActionEffectId.FlameWallBase;
            _enhMap["magic_010"]  = ActionEffectId.FlameWaveEnhanced;
            _baseMap["magic_011"] = ActionEffectId.SacrificeBase;
            _enhMap["magic_011"]  = ActionEffectId.SacrificeEnhanced;

            _baseMap["magic_012"] = ActionEffectId.ManaClaimBase;
            _enhMap["magic_012"]  = ActionEffectId.ManaCurseEnhanced;
            _baseMap["magic_013"] = ActionEffectId.FreezeBase;
            _enhMap["magic_013"]  = ActionEffectId.DeadlyFreezeEnhanced;
            _baseMap["magic_014"] = ActionEffectId.MistFormBase;
            _enhMap["magic_014"]  = ActionEffectId.MistShroudEnhanced;
            _baseMap["magic_015"] = ActionEffectId.BlizzardBase;
            _enhMap["magic_015"]  = ActionEffectId.BlizzardStormEnhanced;
            _baseMap["magic_016"] = ActionEffectId.WarpSpaceBase;
            _enhMap["magic_016"]  = ActionEffectId.WarpTimeEnhanced;
            _baseMap["magic_017"] = ActionEffectId.ManaBoltBase;
            _enhMap["magic_017"]  = ActionEffectId.ManaThunderEnhanced;
            _baseMap["magic_018"] = ActionEffectId.MindReadBase;
            _enhMap["magic_018"]  = ActionEffectId.MindStealEnhanced;
            _baseMap["magic_019"] = ActionEffectId.ExposureBase;
            _enhMap["magic_019"]  = ActionEffectId.MassExposureEnhanced;
            _baseMap["magic_020"] = ActionEffectId.BattleSummonsBase;
            _enhMap["magic_020"]  = ActionEffectId.HonorSummonsEnhanced;
            _baseMap["magic_021"] = ActionEffectId.CharmBase;
            _enhMap["magic_021"]  = ActionEffectId.PossessEnhanced;
            _baseMap["magic_022"] = ActionEffectId.WhirlwindBase;
            _enhMap["magic_022"]  = ActionEffectId.HurricaneEnhanced;
            _baseMap["magic_023"] = ActionEffectId.WindWingsBase;
            _enhMap["magic_023"]  = ActionEffectId.NightWingsEnhanced;
        }

        /// <summary>
        /// 執行指定行動牌的效果。
        /// </summary>
        public void Play(ActionCardData card, PlayerState player, ActionContext ctx, bool enhanced, int option = 0)
        {
            bool useEnh = enhanced || ctx.NextCardEnhanced;
            bool overlord = false;
            if (useEnh && ctx.OverlordPending && ctx.OverlordColor.HasValue)
            {
                foreach (var c in card.RequiredCrystals)
                    if (c == ctx.OverlordColor.Value)
                        overlord = true;
            }

            // 當玩家主動宣告強效時需要支付指定顏色的法力。
            // 若支付失敗則退回執行基礎效果。
            if (enhanced && card.RequiredCrystals.Length > 0 && !ctx.NextCardEnhanced)
            {
                if (!PayColors(player, ctx, card.RequiredCrystals))
                    useEnh = false;
            }
            if (!useEnh)
                overlord = false;

            var map = useEnh ? _enhMap : _baseMap;
            if (!map.TryGetValue(card.Id, out var id))
                throw new System.InvalidOperationException($"未定義卡牌效果: {card.Id}");

            var effect = CardEffectFactory.Get(id);
            int preMove = ctx.MovementPool;
            int preInf  = ctx.InfluencePool;
            int preMelee = ctx.MeleePool;
            int preRanged = ctx.RangedPool;
            int preBlock = ctx.BlockPool;
            int preUnitAtk = ctx.UnitAttackBonus;
            int preUnitBlk = ctx.UnitBlockBonus;
            effect.Execute(player, ctx, option);
            if (ctx.NextMeleeAsRanged && ctx.MeleePool > preMelee)
            {
                int diff = ctx.MeleePool - preMelee;
                ctx.MeleePool = preMelee;
                ctx.RangedPool += diff;
                ctx.NextMeleeAsRanged = false;
            }
            if (!ctx.AmbushTriggered)
            {
                if ((ctx.MeleePool > preMelee) || (ctx.RangedPool > preRanged) || (ctx.UnitAttackBonus > preUnitAtk))
                {
                    if (ctx.AmbushAttackBonus != 0)
                    {
                        if (ctx.MeleePool > preMelee)
                            ctx.MeleePool += ctx.AmbushAttackBonus;
                        else if (ctx.RangedPool > preRanged)
                            ctx.RangedPool += ctx.AmbushAttackBonus;
                        else
                            ctx.UnitAttackBonus += ctx.AmbushAttackBonus;
                        ctx.AmbushTriggered = true;
                    }
                }
                if (!ctx.AmbushTriggered && (ctx.BlockPool > preBlock || ctx.UnitBlockBonus > preUnitBlk))
                {
                    if (ctx.AmbushBlockBonus != 0)
                    {
                        if (ctx.BlockPool > preBlock)
                            ctx.BlockPool += ctx.AmbushBlockBonus;
                        else
                            ctx.UnitBlockBonus += ctx.AmbushBlockBonus;
                        ctx.AmbushTriggered = true;
                    }
                }
            }
            if (overlord && ctx.OverlordPending)
            {
                if (ctx.MovementPool > preMove)
                    ctx.MovementPool += 4;
                else if (ctx.InfluencePool > preInf)
                    ctx.InfluencePool += 4;
                else if (ctx.RangedPool > preRanged)
                    ctx.RangedPool += 4;
                else if (ctx.MeleePool > preMelee)
                    ctx.MeleePool += 4;
                else if (ctx.BlockPool > preBlock)
                    ctx.BlockPool += 4;
                else if (ctx.UnitAttackBonus > preUnitAtk)
                    ctx.UnitAttackBonus += 4;
                else if (ctx.UnitBlockBonus > preUnitBlk)
                    ctx.UnitBlockBonus += 4;
                ctx.OverlordPending = false;
            }

            if (ctx.NextCardEnhanced && !enhanced)
                ctx.NextCardEnhanced = false;

            // 若設置了加值，則根據效果類型增加數值
            if (ctx.BoostValue != 0 && effect is not BoostNextCardEffect)
            {
                if (effect is MoveEffect)
                    ctx.MovementPool += ctx.BoostValue;
                else if (effect is InfluenceEffect)
                    ctx.InfluencePool += ctx.BoostValue;
                else if (effect is RangedAttackEffect)
                    ctx.RangedPool += ctx.BoostValue;
                ctx.BoostValue = 0;
            }

            if (card.Set == CardSet.Spell && _spellColor.TryGetValue(card.Id, out var sc) &&
                ctx.FamePerSpell.TryGetValue(sc, out int fameAdd) && fameAdd > 0)
            {
                player.Fame += fameAdd;
            }
        }

        /// <summary>以移動力換取指定次數的近戰攻擊。</summary>
        public bool ConvertMoveToAttack(ActionContext ctx, int times = 1)
        {
            if (ctx.MoveCostAttack <= 0) return false;
            int cost = ctx.MoveCostAttack * times;
            if (ctx.MovementPool < cost) return false;
            ctx.MovementPool -= cost;
            ctx.MeleePool += times;
            return true;
        }

        /// <summary>以移動力換取指定次數的遠程攻擊。</summary>
        public bool ConvertMoveToRanged(ActionContext ctx, int times = 1)
        {
            if (ctx.MoveCostRanged <= 0) return false;
            int cost = ctx.MoveCostRanged * times;
            if (ctx.MovementPool < cost) return false;
            ctx.MovementPool -= cost;
            ctx.RangedPool += times;
            return true;
        }

        /// <summary>
        /// 將任意手牌橫置打出並獲得基礎數值；創傷卡需允許方可使用。
        /// </summary>
        public bool PlaySideways(DeedCard card, PlayerState player, ActionContext ctx, SidewaysType type)
        {
            if (!player.Deck.RemoveFromHand(card))
                return false;

            int val = 1;
            if (card.Type == CardType.Wound)
            {
                if (ctx.SidewaysWoundValue <= 0)
                    return false;
                val = ctx.SidewaysWoundValue;
            }
            else if (ctx.SidewaysCardValue > 0)
            {
                val = ctx.SidewaysCardValue;
                ctx.SidewaysCardValue = 0;
            }

            switch (type)
            {
                case SidewaysType.Move:
                    ctx.MovementPool += val;
                    break;
                case SidewaysType.Influence:
                    ctx.InfluencePool += val;
                    break;
                case SidewaysType.Block:
                    ctx.BlockPool += val;
                    break;
                case SidewaysType.Attack:
                default:
                    ctx.MeleePool += val;
                    break;
            }

            player.Deck.Discard(card);
            return true;
        }
    }
}
