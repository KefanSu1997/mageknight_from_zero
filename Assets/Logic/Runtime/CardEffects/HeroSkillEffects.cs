using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 黑暗道路：依照日夜獲得不同移動力。
    /// </summary>
    public sealed class DarkPathEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 0);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.MovementPool += ctx.DayPart == DayPart.Day ? 1 : 2;
        }
    }

    /// <summary>
    /// 燃燒之力：提供1點攻城攻擊（火焰或物理）。
    /// </summary>
    public sealed class BurningPowerEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 1);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.CombatPower.AddAttack(1, option == 0 ? Element.Physical : Element.Fire, AttackType.Siege);
        }
    }

    /// <summary>
    /// 炙熱劍術：攻擊2點。
    /// </summary>
    public sealed class HotSwordEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 1);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.CombatPower.AddAttack(2, option == 0 ? Element.Physical : Element.Fire);
        }
    }

    /// <summary>
    /// 秘密談判：依日夜獲得影響力。
    /// </summary>
    public sealed class SecretNegotiationEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 0);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.InfluencePool += ctx.DayPart == DayPart.Day ? 2 : 3;
        }
    }

    /// <summary>
    /// 暗火魔法：翻面獲得紅色晶體並取得紅或黑色法力。
    /// option 0 紅色，1 黑色。
    /// </summary>
    public sealed class DarkFireMagicEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 1);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            player.Mana.AddCrystal(ManaColor.Red, 1);
            ManaColor color = option == 1 ? ManaColor.Black : ManaColor.Red;
            player.Mana.AddToken(color, 1);
        }
    }

    /// <summary>
    /// 痛苦之力：允許本回合以值2橫置打出創傷牌。
    /// </summary>
    public sealed class PainPowerEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.SidewaysWoundValue = 2;
        }
    }

    /// <summary>
    /// 魔咒：棄牌獲得對應顏色法力標記。
    /// option 0/1 對應紅/黑或白/綠。
    /// 需於 ctx.CardToDiscard 指定要棄掉的牌。
    /// </summary>
    public sealed class CurseMagicEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.CardToDiscard == null)
                throw new System.InvalidOperationException("未指定要棄掉的卡牌");
            var card = ctx.CardToDiscard;
            player.Deck.Discard(card);
            ctx.CardToDiscard = null;

            bool wound = card.Type == CardType.Wound;
            ManaColor color = wound
                ? (option == 1 ? ManaColor.Black : ManaColor.Red)
                : (option == 1 ? ManaColor.Green : ManaColor.White);
            player.Mana.AddToken(color, 1);
        }
    }

    /// <summary>
    /// 兩極分化：白晝黑色任意色，黑夜金色任意色(近似處理)。
    /// </summary>
    public sealed class PolarizationEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.DayPart == DayPart.Day)
                ctx.BlackManaWild = true;
            else
                ctx.GoldManaWild = true;
        }
    }

    /// <summary>
    /// 激勵：抽二牌並在最低名望時獲得紅色法力。
    /// </summary>
    public sealed class MotivationEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Deck.DrawExact(2);
            bool lowest = true;
            foreach (var p in ctx.OtherPlayers)
                if (p.Reputation <= player.Reputation)
                    lowest &= p.Reputation > player.Reputation;
            if (lowest)
                player.Mana.AddToken(ManaColor.Red, 1);
        }
    }

    /// <summary>
    /// 痛苦儀式：移除最多兩張創傷牌。
    /// </summary>
    public sealed class RitualPainEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int removed = 0;
            var hand = player.Deck.Hand.ToArray();
            foreach (var c in hand)
            {
                if (c.Type == CardType.Wound)
                {
                    player.Deck.RemoveFromHand(c);
                    removed++;
                    if (removed == 2) break;
                }
            }
        }
    }

    /// <summary>
    /// 治療儀式：移除最多兩張創傷，其中一張轉移給其他玩家。
    /// </summary>
    public sealed class RitualHealEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            PlayerState? target = ctx.OtherPlayers.Count > 0 ? ctx.OtherPlayers[0] : null;
            int removed = 0;
            foreach (var c in player.Deck.Hand.ToArray())
            {
                if (c.Type == CardType.Wound)
                {
                    player.Deck.RemoveFromHand(c);
                    if (removed == 0 && target != null)
                        target.Deck.Hand.Add(c);
                    removed++;
                    if (removed == 2) break;
                }
            }
        }
    }

    /// <summary>
    /// 元素抗性：忽略下一次指定傷害。
    /// option 0 表火焰或寒冰 2 點；1 表其他 1 點。
    /// </summary>
    public sealed class ElementalResistEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 0)
                ctx.IgnoreFireIceDamage = 2;
            else
                ctx.IgnoreOtherDamage = 1;
        }
    }

    /// <summary>
    /// 野性盟友：探索減費並提供攻擊或減傷。
    /// option 0 攻擊1；1 降低1點敵人攻擊。
    /// </summary>
    public sealed class WildAllyEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.ExploreMoveDiscount = 1;
            if (option == 0)
                ctx.MeleePool += 1;
            else
            {
                ctx.AttackReduction += 1;
                ctx.AttackReductionTimes = 1;
            }
        }
    }

    /// <summary>
    /// 雷雲風暴：獲得兩顆指定顏色法力標記。
    /// option 低8位第一顆 0綠1藍；次8位第二顆 0綠1白。
    /// </summary>
    public sealed class ThunderstormEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int c1 = option & 0xFF;
            int c2 = (option >> 8) & 0xFF;
            ManaColor t1 = c1 == 1 ? ManaColor.Blue : ManaColor.Green;
            ManaColor t2 = c2 == 1 ? ManaColor.White : ManaColor.Green;
            player.Mana.AddToken(t1, 1);
            player.Mana.AddToken(t2, 1);
        }
    }

    /// <summary>
    /// 閃電風暴：與雷雲風暴類似的配色組合。
    /// option 低8位 0藍1綠；次8位 0藍1紅。
    /// </summary>
    public sealed class LightningStormEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int c1 = option & 0xFF;
            int c2 = (option >> 8) & 0xFF;
            ManaColor t1 = c1 == 1 ? ManaColor.Green : ManaColor.Blue;
            ManaColor t2 = c2 == 1 ? ManaColor.Red : ManaColor.Blue;
            player.Mana.AddToken(t1, 1);
            player.Mana.AddToken(t2, 1);
        }
    }

    /// <summary>
    /// 迷醉：固定提供影響力3，地形判定尚未實作。
    /// </summary>
    public sealed class TranceEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.InfluencePool += 3;
        }
    }

    /// <summary>
    /// 叉形閃電：遠程冰火攻擊1×3，簡化為攻擊力3處理。
    /// </summary>
    public sealed class ForkedLightningEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.RangedPool += 3;
        }
    }

    /// <summary>
    /// 變換身形：下張移動牌改為攻擊或格擋。
    /// option 0 攻擊，1 格擋。
    /// </summary>
    public sealed class ShapeshiftEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.TransformMoveType = option == 1 ? 2 : 1;
        }
    }

    /// <summary>
    /// 隱秘之路：提供移動力並修改山脈與湖泊花費。
    /// option 0 不付藍色；1 付藍色並可進湖泊。
    /// </summary>
    public sealed class HiddenPathEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += 1;
            ctx.TerrainCostOverride[TerrainType.Mountain] = 5;
            ctx.MountainSafe = true;
            if (option == 1)
            {
                ctx.TerrainCostOverride[TerrainType.Lake] = 2;
                ctx.LakeSafe = true;
            }
        }
    }

    /// <summary>
    /// 再生：支付任意法力移除創傷，若綠色或最低名望則抽牌。
    /// option 為支付的顏色索引。
    /// </summary>
    public sealed class RegenerationSkillEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ManaColor color = (ManaColor)option;
            var cost = new ManaCost(new Dictionary<Element,int>{{color.ToElement(),1}});
            if (!player.Mana.Pay(cost, player)) return;

            foreach (var c in player.Deck.Hand.ToArray())
            {
                if (c.Type == CardType.Wound)
                {
                    player.Deck.RemoveFromHand(c);
                    if (color == ManaColor.Green || IsLowest(player, ctx))
                        player.Deck.DrawExact(1);
                    break;
                }
            }
        }

        private static bool IsLowest(PlayerState p, ActionContext ctx)
        {
            bool lowest = true;
            foreach (var o in ctx.OtherPlayers)
                if (o.Reputation <= p.Reputation)
                    lowest &= o.Reputation > p.Reputation;
            return lowest;
        }
    }

    /// <summary>
    /// 自然復仇：降低一次敵人攻擊並使其笨重，進階效果略過實作其他加成。
    /// </summary>
public sealed class NatureRevengeEffect : ICardEffect
{
    public void Execute(PlayerState player, ActionContext ctx, int option = 0)
    {
        ctx.AttackReduction += 1;
        ctx.AttackReductionTimes = 1;
        // 笨重效果尚未實作，僅留空位
    }
}

    /// <summary>
    /// 全軍行進：依據已就緒且未受傷的部隊數量獲得最多3點移動力。
    /// </summary>
    public sealed class FullMarchEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int ready = 0;
            foreach (var u in player.Units)
                if (u.IsReady && u.Wounds == 0)
                    ready++;
            ctx.MovementPool += System.Math.Min(3, ready);
        }
    }

    /// <summary>
    /// 白晝狙擊：白晝遠程攻擊2，黑夜遠程攻擊1。
    /// </summary>
    public sealed class DaySniperEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.RangedPool += ctx.DayPart == DayPart.Day ? 2 : 1;
        }
    }

    /// <summary>
    /// 重整旗鼓：翻面以重整或治療一支部隊，需在 ctx.TargetUnit 指定。
    /// option 0 重整，1 治療1點。
    /// </summary>
    public sealed class RallyEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var unit = ctx.TargetUnit;
            if (unit == null || ctx.InBattle) return;
            if (option == 0)
                unit.Ready();
            else
                unit.Heal(1);
        }
    }

    /// <summary>
    /// 公開談判：白晝影響力3，黑夜影響力2。
    /// </summary>
    public sealed class PublicNegotiationEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.InfluencePool += ctx.DayPart == DayPart.Day ? 3 : 2;
        }
    }

    /// <summary>
    /// 風中殘葉：獲得綠色晶體並取得白色法力。
    /// </summary>
    public sealed class LeavesInWindEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Mana.AddCrystal(ManaColor.Green, 1);
            player.Mana.AddToken(ManaColor.White, 1);
        }
    }

    /// <summary>
    /// 樹梢風語：獲得白色晶體並取得綠色法力。
    /// </summary>
    public sealed class TreetopWhisperEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Mana.AddCrystal(ManaColor.White, 1);
            player.Mana.AddToken(ManaColor.Green, 1);
        }
    }

    /// <summary>
    /// 領導：激活部隊時依選項獲得格擋+3、攻擊+2 或遠程攻擊+1。
    /// option 0 格擋、1 攻擊、2 遠程。
    /// </summary>
    public sealed class LeadershipSkillEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            switch (option)
            {
                case 0:
                    ctx.UnitBlockBonus += 3;
                    break;
                case 1:
                    ctx.UnitAttackBonus += 2;
                    break;
                default:
                    ctx.RangedPool += 1;
                    break;
            }
        }
    }

    /// <summary>
    /// 忠誠契約：獲得永久指揮槽並招募折扣5點。
    /// </summary>
    public sealed class LoyalContractEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.ExtraCommandSlots += 1;
            ctx.RecruitDiscount += 5;
        }
    }

    /// <summary>
    /// 激勵（白色）：抽二牌，名望最低時得白色法力。
    /// </summary>
    public sealed class MotivationWhiteEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Deck.DrawExact(2);
            bool lowest = true;
            foreach (var p in ctx.OtherPlayers)
                if (p.Reputation <= player.Reputation)
                    lowest &= p.Reputation > player.Reputation;
            if (lowest)
                player.Mana.AddToken(ManaColor.White, 1);
        }
    }

    /// <summary>
    /// 風雨平復：本回合所有地形移動費用-2（最低1）。
    /// </summary>
    public sealed class CalmWindEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            foreach (TerrainType t in System.Enum.GetValues(typeof(TerrainType)))
            {
                int baseCost = Map.TerrainCost.GetCost(t, ctx.DayPart);
                ctx.TerrainCostOverride[t] = System.Math.Max(1, baseCost - 2);
            }
        }
    }

    /// <summary>
    /// 風雨祈禱：直到下回合開始前移動費用-2（簡化為立即效果），其他玩家+1 未實作。
    /// </summary>
public sealed class WindPrayerEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            foreach (TerrainType t in System.Enum.GetValues(typeof(TerrainType)))
            {
                int baseCost = Map.TerrainCost.GetCost(t, ctx.DayPart);
                ctx.TerrainCostOverride[t] = System.Math.Max(1, baseCost - 2);
            }
            // 其他玩家移動費用增加的效果省略
        }
    }

    // 以下為技能池003 ──────────────────────────

    /// <summary>
    /// 靈魂向導：移動力1並增加1點格擋值。
    /// </summary>
    public sealed class SoulGuideEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += 1;
            ctx.BlockPool += 1;
        }
    }

    /// <summary>
    /// 身經百戰：忽略物理2或非物理1點傷害。
    /// option 0 物理、1 非物理。
    /// </summary>
    public sealed class BattleHardenedEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 0)
                ctx.IgnorePhysicalDamage += 2;
            else
                ctx.IgnoreNonPhysicalDamage += 1;
        }
    }

    /// <summary>
    /// 戰鬥狂亂：攻擊2或4點。
    /// option 0 普通，1 翻面強化。
    /// </summary>
    public sealed class BattleFrenzyEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MeleePool += option == 1 ? 4 : 2;
        }
    }

    /// <summary>
    /// 薩滿儀式：取得任意顏色法力標記。
    /// option 為顏色索引。
    /// </summary>
    public sealed class ShamanRitualEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ManaColor color = (ManaColor)option;
            player.Mana.AddToken(color, 1);
        }
    }

    /// <summary>
    /// 再生（紅）：支付法力移除創傷，使用紅色或最低名望時抽牌。
    /// </summary>
    public sealed class RegenerationRedEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ManaColor color = (ManaColor)option;
            var cost = new ManaCost(new System.Collections.Generic.Dictionary<Element, int>{{color.ToElement(),1}});
            if (!player.Mana.Pay(cost, player)) return;

            foreach (var c in player.Deck.Hand.ToArray())
            {
                if (c.Type == CardType.Wound)
                {
                    player.Deck.RemoveFromHand(c);
                    if (color == ManaColor.Red || IsLowest(player, ctx))
                        player.Deck.DrawExact(1);
                    break;
                }
            }
        }

        private static bool IsLowest(PlayerState p, ActionContext ctx)
        {
            bool lowest = true;
            foreach (var o in ctx.OtherPlayers)
                if (o.Reputation <= p.Reputation)
                    lowest &= o.Reputation > p.Reputation;
            return lowest;
        }
    }

    /// <summary>
    /// 奧術偽裝：影響力2或本回合忽略聲望變化。
    /// option 0 影響力、1 忽略聲望。
    /// </summary>
    public sealed class ArcaneDisguiseEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 0)
                ctx.InfluencePool += 2;
            else
                ctx.IgnoreReputationChange = true;
        }
    }

    /// <summary>
    /// 傀儡大師：保存或棄置敵人標記獲得攻擊/格擋。
    /// option 0 保存 ctx.KillEnemyIndex 指定的敵人；1 消耗獲得攻擊；2 消耗獲得格擋。
    /// </summary>
    public sealed class PuppetMasterEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 0)
            {
                if (ctx.Enemies != null && ctx.KillEnemyIndex.HasValue)
                {
                    int idx = ctx.KillEnemyIndex.Value;
                    if (idx >= 0 && idx < ctx.Enemies.Count)
                        player.StoredEnemy = ctx.Enemies[idx];
                }
            }
            else if (player.StoredEnemy != null)
            {
                var m = player.StoredEnemy;
                if (option == 1)
                    ctx.MeleePool += (m.Attack + 1) / 2;
                else
                    ctx.BlockPool += (m.Armor + 1) / 2;
                player.StoredEnemy = null;
            }
        }
    }

    /// <summary>
    /// 混亂大師：每次旋轉顏色獲得對應效果，金色由 option 決定。
    /// </summary>
    public sealed class ChaosMasterEffect : ICardEffect
    {
        private static readonly ManaColor[] Cycle = { ManaColor.Blue, ManaColor.Green, ManaColor.Black, ManaColor.White, ManaColor.Red, ManaColor.Gold };
        private static readonly System.Random Rng = new();

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (player.ChaosColor == null)
            {
                player.ChaosColor = Cycle[Rng.Next(Cycle.Length)];
            }
            else
            {
                int idx = System.Array.IndexOf(Cycle, player.ChaosColor.Value);
                player.ChaosColor = Cycle[(idx + 1) % Cycle.Length];
            }

            var color = player.ChaosColor.Value;
            switch (color)
            {
                case ManaColor.Blue:
                    ctx.BlockPool += 3;
                    break;
                case ManaColor.Green:
                    ctx.MovementPool += 1;
                    break;
                case ManaColor.Black:
                    ctx.RangedPool += 1;
                    break;
                case ManaColor.White:
                    ctx.InfluencePool += 2;
                    break;
                case ManaColor.Red:
                    ctx.MeleePool += 2;
                    break;
                case ManaColor.Gold:
                    switch (option)
                    {
                        case 0: ctx.BlockPool += 3; break;
                        case 1: ctx.MovementPool += 1; break;
                        case 2: ctx.RangedPool += 1; break;
                        case 3: ctx.InfluencePool += 2; break;
                        default: ctx.MeleePool += 2; break;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// 詛咒：降低敵人攻擊或護甲。
    /// option 低8位目標索引，次位1表示護甲。
    /// </summary>
    public sealed class HexCurseEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int idx = option & 0xFF;
            bool armor = (option >> 8) == 1;
            if (armor)
            {
                int cur = ctx.ArmorReduction.GetValueOrDefault(idx);
                ctx.ArmorReduction[idx] = cur + 1;
            }
            else
            {
                ctx.AttackReduction += 1;
                ctx.AttackReductionTimes = 1;
            }
        }
    }

    /// <summary>
    /// 魔力強化：支付法力時獲得同色晶體與法力標記。
    /// option 為顏色索引。
    /// </summary>
    public sealed class ManaBoostEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ManaColor color = (ManaColor)option;
            player.Mana.AddToken(color, 1);
            player.Mana.AddCrystal(color, 1);
        }
    }

    /// <summary>
    /// 魔力抑制：簡化為下回合獲得一顆指定晶體。
    /// option 為顏色索引。
    /// </summary>
public sealed class ManaSuppressEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ManaColor color = (ManaColor)option;
            player.Mana.AddCrystal(color, 1);
        }
    }

    // 以下為技能池004 ──────────────────────────

    /// <summary>
    /// 提神沐浴：治療1並獲得藍色晶體。
    /// </summary>
    public sealed class RefreshBathEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            CardHealing.Heal(player, ctx, 1);
            player.Mana.AddCrystal(ManaColor.Blue, 1);
        }
    }

    /// <summary>
    /// 提神微風：治療1並獲得白色晶體。
    /// </summary>
    public sealed class RefreshBreezeEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            CardHealing.Heal(player, ctx, 1);
            player.Mana.AddCrystal(ManaColor.White, 1);
        }
    }

    /// <summary>
    /// 神鷹之眼：移動力1，夜晚探索減費1，白晝可偵查城防。
    /// </summary>
    public sealed class EagleEyeEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += 1;
            if (ctx.DayPart == DayPart.Night)
                ctx.ExploreMoveDiscount = System.Math.Max(ctx.ExploreMoveDiscount, 1);
            else
                ctx.CastleRevealRange = 2;
        }
    }

    /// <summary>
    /// 特立獨行：若本回合未招募則影響力3，否則1。
    /// </summary>
    public sealed class LoneWolfEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.InfluencePool += ctx.HasRecruited ? 1 : 3;
        }
    }

    /// <summary>
    /// 百步穿楊：遠程或攻城+1，或近戰+2。
    /// option 0 遠程，1 近戰。
    /// </summary>
    public sealed class BullseyeEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 0)
                ctx.RangedPool += 1;
            else
                ctx.MeleePool += 2;
        }
    }

    /// <summary>
    /// 洞悉獵物：忽略敵人能力或改變攻擊元素。
    /// option 高位0x1忽略能力，0x2改變元素；低8位敵人索引，其餘為值。
    /// </summary>
    public sealed class InsightPreyEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int mode = (option >> 16) & 0xFF;
            int idx = option & 0xFF;
            int val = (option >> 8) & 0xFF;
            if (mode == 1)
            {
                ctx.IgnoredAbilities[idx] = (Ability)val;
            }
            else if (mode == 2)
            {
                ctx.AttackElementChange[idx] = (Element)val;
            }
        }
    }

    /// <summary>
    /// 嘲諷：降低敵人攻擊或提高攻擊並削弱護甲。
    /// option 0x1 減少，0x2 增加；低8位敵人索引。
    /// </summary>
    public sealed class TauntEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int mode = (option >> 8) & 0xFF;
            int idx = option & 0xFF;
            if (mode == 1)
            {
                ctx.EnemyAttackAdjust[idx] = ctx.EnemyAttackAdjust.GetValueOrDefault(idx) - 1;
            }
            else if (mode == 2)
            {
                ctx.EnemyAttackAdjust[idx] = ctx.EnemyAttackAdjust.GetValueOrDefault(idx) + 2;
                ctx.ArmorReduction[idx] = ctx.ArmorReduction.GetValueOrDefault(idx) + 2;
            }
        }
    }

    /// <summary>
    /// 決鬥：格擋1並對同一敵人攻擊1，擊殺額外得名望1。
    /// </summary>
    public sealed class DuelEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.BlockPool += 1;
            ctx.MeleePool += 1;
            ctx.PendingFameOnKill += 1;
        }
    }

    /// <summary>
    /// 激勵（名望）：抽二牌，名望最低時獲得1點名望。
    /// </summary>
    public sealed class MotivationFameEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Deck.DrawExact(2);
            bool lowest = true;
            foreach (var p in ctx.OtherPlayers)
                if (p.Reputation <= player.Reputation)
                    lowest &= p.Reputation > player.Reputation;
            if (lowest)
                player.Fame += 1;
        }
    }

    /// <summary>
    /// 群狼嚎叫：橫置牌+4並依空餘指揮加成，降低敵人攻擊與護甲。
    /// option 低8位護甲目標，次8位攻擊目標。
    /// </summary>
    public sealed class WolfPackHowlEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            int idxArmor = option & 0xFF;
            int idxAttack = (option >> 8) & 0xFF;
            ctx.SidewaysCardValue = 4 + player.FreeSlots;
            ctx.ArmorReduction[idxArmor] = ctx.ArmorReduction.GetValueOrDefault(idxArmor) + 1;
            ctx.EnemyAttackAdjust[idxAttack] = ctx.EnemyAttackAdjust.GetValueOrDefault(idxAttack) - 1;
        }
    }

    /// <summary>
    /// 狼嚎：橫置牌+4並依空餘指揮加成，其他玩家部隊攻擊格擋-1。
    /// </summary>
    public sealed class WolfHowlEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.SidewaysCardValue = 4 + player.FreeSlots;
            ctx.OtherUnitPenalty = -1;
        }
    }

    // 以下為技能池005 ──────────────────────────

    /// <summary>
    /// 冰封之力：提供1點攻城攻擊，可視為寒冰。
    /// </summary>
    public sealed class FrozenPowerEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.RangedPool += 1;
        }
    }

    /// <summary>
    /// 藥劑製作：治療2點英雄傷口。
    /// </summary>
    public sealed class PotionCraftEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            CardHealing.Heal(player, ctx, 2);
        }
    }

    /// <summary>
    /// 白晶鍛造：獲得藍色晶體並取得白色法力標記。
    /// </summary>
    public sealed class WhiteForgeEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Mana.AddCrystal(ManaColor.Blue, 1);
            player.Mana.AddToken(ManaColor.White, 1);
        }
    }

    /// <summary>
    /// 綠晶鍛造：獲得藍色晶體並取得綠色法力標記。
    /// </summary>
    public sealed class GreenForgeEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Mana.AddCrystal(ManaColor.Blue, 1);
            player.Mana.AddToken(ManaColor.Green, 1);
        }
    }

    /// <summary>
    /// 紅晶鍛造：獲得藍色晶體並取得紅色法力標記。
    /// </summary>
    public sealed class RedForgeEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Mana.AddCrystal(ManaColor.Blue, 1);
            player.Mana.AddToken(ManaColor.Red, 1);
        }
    }

    /// <summary>
    /// 熒光靈運：交涉時依持有晶體種類數獲得影響力。
    /// </summary>
    public sealed class CrystalLuckEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (!ctx.InNegotiation) return;
            int kinds = 0;
            foreach (var kv in player.Mana.Crystals)
                if (kv.Value > 0) kinds++;
            ctx.InfluencePool += kinds;
        }
    }

    /// <summary>
    /// 飛行：傳送1或2格，必須停在安全格且不激怒肆虐敵人。
    /// option 0 相鄰格，1 兩格。
    /// </summary>
    public sealed class FlightEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.TeleportRange = option == 1 ? 2 : 1;
            ctx.TeleportMustEndSafe = true;
            ctx.IgnoreRampaging = true;
        }
    }

    /// <summary>
    /// 通用力量：橫置牌支付魔力後值提升為3或4。
    /// option 0 不同色，1 同色。
    /// </summary>
    public sealed class UniversalPowerEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.SidewaysCardValue = option == 1 ? 4 : 3;
        }
    }

    /// <summary>
    /// 激勵（綠色）：抽兩牌，若名望最低則得綠色法力。
    /// </summary>
    public sealed class MotivationGreenEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Deck.DrawExact(2);
            bool lowest = true;
            foreach (var p in ctx.OtherPlayers)
                if (p.Reputation <= player.Reputation)
                    lowest &= p.Reputation > player.Reputation;
            if (lowest)
                player.Mana.AddToken(ManaColor.Green, 1);
        }
    }

    /// <summary>
    /// 魔力開源：重新擲指定魔力骰並額外允許使用一顆骰子。
    /// option 為要重擲的骰子索引。
    /// </summary>
    public sealed class ManaOpenEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var source = ctx.ManaSource;
            if (source == null || source.Dice.Count == 0) return;
            int idx = System.Math.Clamp(option, 0, source.Dice.Count - 1);
            source.Dice[idx].Roll(ctx.DayPart);
            ctx.ExtraManaDice += 1;
        }
    }

    /// <summary>
    /// 魔源冰封：本回合封鎖公共魔力源，僅記錄狀態，完整效果尚未實作。
    /// </summary>
    public sealed class ManaFreezeEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.ManaSourceFrozen = true;
        }
    }

    // ─── 技能池006 ──────────────────────────

    /// <summary>
    /// 猛毒藥瓶：一次攻擊提升，依選項轉換為遠程或攻城。
    /// option 0 近戰+3，1 遠程+2，2 攻城+1。
    /// </summary>
    public sealed class PoisonVialEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            switch (option)
            {
                case 1:
                    ctx.RangedPool += 2;
                    break;
                case 2:
                    ctx.RangedPool += 1;
                    ctx.RangedAttackAsSiege = true;
                    break;
                default:
                    ctx.MeleePool += 3;
                    break;
            }
        }
    }

    /// <summary>
    /// 聖族之杯：戰鬥結束後每擊殺一名敵人可棄掉傷牌，最多四張。
    /// </summary>
    public sealed class HolyChaliceEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.DiscardWoundsAfterBattle = 4;
        }
    }

    /// <summary>
    /// 鬼魂靈藥：下一次近戰攻擊視為遠程攻擊。
    /// </summary>
    public sealed class GhostElixirEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.NextMeleeAsRanged = true;
        }
    }

    /// <summary>
    /// 隱匿法杖：提供移動力並避免激怒敵人，追擊敵人本回合不行動。
    /// </summary>
    public sealed class HiddenStaffEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += 2;
            ctx.IgnoreRampaging = true;
            ctx.EnemySkipAttack = true;
        }
    }

    /// <summary>
    /// 復甦項鍊：隨機將一張棄牌放回牌庫底，下次抽牌上限+1。
    /// </summary>
    public sealed class RevivalNecklaceEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var discard = player.Deck.DiscardPile.Where(c => true).ToList();
            if (discard.Count > 0)
            {
#if NETSTANDARD2_1
                var idx = new System.Random().Next(discard.Count);
#else
                var idx = System.Random.Shared.Next(discard.Count);
#endif
                var card = discard[idx];
                player.Deck.RemoveFromDiscard(card);
                player.Deck.PutUnder(card);
            }
            ctx.NextDrawBonus += 1;
        }
    }

    /// <summary>
    /// 晨暮寶珠：將公共魔力源的黑色骰改為金色或反之。
    /// option 0 黑轉金，1 金轉黑。
    /// </summary>
    public sealed class DawnDuskOrbEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var src = ctx.ManaSource;
            if (src == null) return;
            ManaColor target = option == 0 ? ManaColor.Gold : ManaColor.Black;
            ManaColor from   = option == 0 ? ManaColor.Black : ManaColor.Gold;
            foreach (var d in src.Dice)
            {
                if (d.Face == from)
                {
                    var prop = typeof(ManaDie).GetProperty("Face");
                    prop!.SetValue(d, target);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 療傷草藥：治療英雄或移除棄牌堆中的創傷。
    /// option 0 治療1；1 移除棄牌堆創傷。
    /// </summary>
    public sealed class HealingHerbEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 1)
            {
                var w = player.Deck.DiscardPile.FirstOrDefault(c => c.Type == CardType.Wound);
                if (w != null)
                    player.Deck.RemoveFromDiscard(w);
            }
            else
            {
                CardHealing.Heal(player, ctx, 1);
            }
        }
    }

    /// <summary>
    /// 寒冰碎片：下一次攻擊或格擋視為寒冰元素，依選項決定目標。
    /// option 0 攻擊，1 格擋。
    /// </summary>
    public sealed class IceShardEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 1)
                ctx.InfluenceBlockElement = Element.Ice;
            else
                ctx.AttackElementChange[-1] = Element.Ice; // -1 表示玩家攻擊
        }
    }

    /// <summary>
    /// 火焰寶石：下一次攻擊或格擋視為火焰元素，依選項決定目標。
    /// option 0 攻擊，1 格擋。
    /// </summary>
    public sealed class FireGemEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 1)
                ctx.InfluenceBlockElement = Element.Fire;
            else
                ctx.AttackElementChange[-1] = Element.Fire;
        }
    }

    /// <summary>
    /// 秘法地圖：遠距離探索新地形，視為偵查城堡功能。
    /// </summary>
    public sealed class ArcaneMapEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.CastleRevealRange = System.Math.Max(ctx.CastleRevealRange, 3);
        }
    }

    /// <summary>
    /// 護御披風：忽略下一張進入手牌的傷牌。
    /// </summary>
    public sealed class GuardCloakEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.IgnoreNextWound = true;
        }
    }

    /// <summary>
    /// 重訓典籍：與供應區技能交換。
    /// option 低8位為自己技能索引，次8位為供應索引。
    /// </summary>
    public sealed class RetrainTomeEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var supply = ctx.SkillSupply ?? throw new System.InvalidOperationException("缺少技能供應");
            int own = option & 0xFF;
            int sup = (option >> 8) & 0xFF;
            if (own < 0 || own >= player.Skills.Count) return;
            if (sup < 0 || sup >= supply.Offer.Count) return;
            var tmp = player.Skills[own];
            player.Skills[own] = supply.Offer[sup];
            supply.Offer[sup] = tmp;
        }
    }

    // ─── 技能池007 ──────────────────────────

    /// <summary>
    /// 快步前行：依日夜提供不同移動力。
    /// </summary>
    public sealed class QuickStrideEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MovementPool += ctx.DayPart == DayPart.Day ? 2 : 1;
        }
    }

    /// <summary>
    /// 黑夜狙擊：依日夜提供遠程攻擊。
    /// </summary>
    public sealed class NightSniperEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.RangedPool += ctx.DayPart == DayPart.Day ? 1 : 2;
        }
    }

    /// <summary>
    /// 寒冰劍術：攻擊2或寒冰攻擊2。
    /// option 0 物理，1 寒冰。
    /// </summary>
    public sealed class IceSwordEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.MeleePool += 2;
            if (option == 1)
                ctx.AttackElementChange[-1] = Element.Ice;
        }
    }

    /// <summary>
    /// 盾牌精通：格擋3，寒冰格擋2或火焰格擋2。
    /// option 0 物理，1 冰，2 火。
    /// </summary>
    public sealed class ShieldMasteryEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option == 1)
            {
                ctx.BlockPool += 2;
                ctx.InfluenceBlockElement = Element.Ice;
            }
            else if (option == 2)
            {
                ctx.BlockPool += 2;
                ctx.InfluenceBlockElement = Element.Fire;
            }
            else
            {
                ctx.BlockPool += 3;
            }
        }
    }

    /// <summary>
    /// 抗性弱化：按抗性種類降低敵人護甲。
    /// option 目標索引。
    /// </summary>
    public sealed class ResistWeakeningEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.Enemies == null || option < 0 || option >= ctx.Enemies.Count) return;
            var m = ctx.Enemies[option];
            int count = 0;
            foreach (var a in m.Abilities)
            {
                if (a == Ability.FireResist || a == Ability.IceResist || a == Ability.MagicResist)
                    count += 1;
                else if (a == Ability.ColdFireResist)
                    count += 2;
            }
            int reduce = System.Math.Max(0, count);
            int cur = ctx.ArmorReduction.GetValueOrDefault(option);
            ctx.ArmorReduction[option] = cur + reduce;
        }
    }

    /// <summary>
    /// 無懼痛苦：戰鬥外棄傷牌並抽牌。
    /// </summary>
    public sealed class FearlessPainEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.InBattle) return;
            var w = player.Deck.Hand.FirstOrDefault(c => c.Type == CardType.Wound);
            if (w != null)
            {
                player.Deck.RemoveFromHand(w);
                player.Deck.GainToDiscard(w);
                player.Deck.DrawExact(1);
            }
        }
    }

    /// <summary>
    /// 漫不經心：橫置牌+2或+3。
    /// option 0 普通牌，1 高級/法術/神器。
    /// </summary>
    public sealed class CarelessEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.SidewaysCardValue = option == 1 ? 3 : 2;
        }
    }

    /// <summary>
    /// 魔法抗拒：若未使用公共骰子橫置牌+3，否則+2。
    /// </summary>
    public sealed class MagicRepelEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ctx.SidewaysCardValue = player.HeldManaDie == null ? 3 : 2;
        }
    }

    /// <summary>
    /// 激勵（藍色）：名望最低時獲得藍色法力。
    /// </summary>
    public sealed class MotivationBlueEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            player.Deck.DrawExact(2);
            bool lowest = true;
            foreach (var p in ctx.OtherPlayers)
                if (p.Reputation <= player.Reputation)
                    lowest &= p.Reputation > player.Reputation;
            if (lowest)
                player.Mana.AddToken(ManaColor.Blue, 1);
        }
    }

    /// <summary>
    /// 魔力霸主：選色獲標記，下張以該色強效的牌+4。
    /// option 為顏色索引。
    /// </summary>
    public sealed class ManaOverlordEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var color = (ManaColor)option;
            if (color == ManaColor.Gold) return;
            player.Mana.AddToken(color, 1);
            ctx.OverlordColor = color;
            ctx.OverlordPending = true;
        }
    }
}

