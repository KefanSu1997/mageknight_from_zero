using System;
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
    public sealed class PainPowerEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 0);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
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
            => throw new System.InvalidOperationException("魔咒必须随实际费用选择材料并立即支付");
    }

    /// <summary>两极分化只替换一次真实费用，不设置全回合通用许可。</summary>
    public sealed class PolarizationEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
            => throw new System.InvalidOperationException("两极分化必须随实际费用选择源色并立即支付");
    }

    /// <summary>
    /// 激勵：抽二牌並在最低名望時獲得紅色法力。
    /// </summary>
    public sealed class MotivationEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => InspirationSkillRules.Validate(player, option);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            bool lowest = InspirationSkillRules.IsStrictlyLowest(player, ctx);
            InspirationSkillRules.DrawAndLock(player);
            if (lowest) player.Mana.AddToken(ManaColor.Red, 1);
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
    public sealed class ElementalResistEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            HeroAttackSkillRules.Mode(option);
            if (!ctx.InBattle || ctx.CombatPhase != Phase.AssignDamage)
                throw new InvalidOperationException("元素抗性须在分配伤害阶段发动");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (option == 0) ctx.IgnoreFireIceDamage = 2;
            else ctx.IgnoreOtherDamage = 1;
        }
    }

    /// <summary>
    /// 野性盟友：探索減費並提供攻擊或減傷。
    /// option 0 攻擊1；1 降低1點敵人攻擊。
    /// </summary>
    public sealed class WildAllyEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            HeroAttackSkillRules.Mode(option);
            if (option == 1) HeroAttackSkillRules.Target(ctx, true);
            else if (!ctx.InBattle || ctx.CombatPhase != Phase.Melee && !(ctx.CombatPhase == Phase.Ranged && ctx.AllAttacksSiege))
                throw new InvalidOperationException("野性盟友攻击须在合法攻击阶段使用");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (option == 0) ctx.CombatPower.AddAttack(1);
            else ctx.AddSkillAttackAdjustment(ctx.SelectedEnemy, ctx.SelectedEnemyAttackIndex, -1);
        }
    }

    /// <summary>
    /// 雷雲風暴：獲得兩顆指定顏色法力標記。
    /// option 低8位第一顆 0綠1藍；次8位第二顆 0綠1白。
    /// </summary>
    public sealed class ThunderstormEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option != 0 && option != 1 && option != 256 && option != 257)
                throw new System.InvalidOperationException("请选择两枚原技能许可颜色的魔力标记");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
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
    public sealed class LightningStormEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option != 0 && option != 1 && option != 256 && option != 257)
                throw new System.InvalidOperationException("请选择两枚原技能许可颜色的魔力标记");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            int c1 = option & 0xFF;
            int c2 = (option >> 8) & 0xFF;
            ManaColor t1 = c1 == 1 ? ManaColor.Green : ManaColor.Blue;
            ManaColor t2 = c2 == 1 ? ManaColor.Red : ManaColor.Blue;
            player.Mana.AddToken(t1, 1);
            player.Mana.AddToken(t2, 1);
        }
    }

    /// <summary>迷醉：按玩家实际所在地点产生影响力，城防2、魔法林地4、其他3。</summary>
    public sealed class TranceEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 0);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            int influence = 3;
            if (ctx.MovementMap != null && ctx.MovementMap.Places.TryGetValue(player.Position, out var site))
            {
                if (site.Type == PlaceType.Keep || site.Type == PlaceType.MageTower || site.Type == PlaceType.City)
                    influence = 2;
                else if (site.Type == PlaceType.MagicalGlade) influence = 4;
            }
            ctx.InfluencePool += influence;
        }
    }

    /// <summary>
    /// 叉形闪电：由真实多目标入口分别建立冰火远程贡献。
    /// </summary>
    public sealed class ForkedLightningEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player,ActionContext ctx,int option)
            => throw new InvalidOperationException("叉形闪电需要选择至多三名实际敌人");
        public void Execute(PlayerState player,ActionContext ctx,int option=0)=>Validate(player,ctx,option);
    }

    /// <summary>
    /// 變換身形：下張移動牌改為攻擊或格擋。
    /// option 0 攻擊，1 格擋。
    /// </summary>
    public sealed class ShapeshiftEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
            => throw new InvalidOperationException("变换身形需要选择实际基本行动牌、原效果与转换项");
        public void Execute(PlayerState player, ActionContext ctx, int option = 0) => Validate(player, ctx, option);
    }

    /// <summary>
    /// 隱秘之路：提供移動力並修改山脈與湖泊花費。
    /// option 0 不付藍色；1 付藍色並可進湖泊。
    /// </summary>
    public sealed class HiddenPathEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            RepeatedEffectResolution.RequireCompleted(ctx);
            if (option < 0 || option > 1 || ctx.InBattle || ctx.TurnActionCommitted || ctx.RestedThisTurn)
                throw new InvalidOperationException("隐秘之路须在可移动的战斗外回合选择无费用或支付蓝色魔力");
            if (option == 1 && !player.Mana.TryPreviewExactColors(new[] { ManaColor.Blue }, ctx, out _))
                throw new InvalidOperationException("隐秘之路穿湖需要支付一点蓝色魔力");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (option == 1 && !player.Mana.TryPayExactColors(new[] { ManaColor.Blue }, ctx, player))
                throw new InvalidOperationException("隐秘之路蓝色魔力不足");
            ctx.MovementPool += 1;
            ctx.TerrainCostOverride[TerrainType.Mountain] = 5;
            ctx.MountainSafe = true;
            if (option == 1)
            {
                ctx.TerrainCostOverride[TerrainType.Lake] = 2;
                ctx.LakeSafe = true;
                ctx.RequireBlueForLake = false;
            }
        }
    }

    /// <summary>
    /// 再生：支付任意法力移除創傷，若綠色或最低名望則抽牌。
    /// option 為支付的顏色索引。
    /// </summary>
    public sealed class RegenerationSkillEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => RegenerationSkillRules.Validate(player, ctx, option);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            RegenerationSkillRules.Execute(player, ctx, option, ManaColor.Green);
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
    public sealed class DaySniperEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 0);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.RangedPool += ctx.DayPart == DayPart.Day ? 2 : 1;
        }
    }

    /// <summary>
    /// 重整旗鼓：翻面以重整或治療一支部隊，需在 ctx.TargetUnit 指定。
    /// option 0 重整，1 治療1點。
    /// </summary>
    public sealed class RallyEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            SkillOption.Require(option, 1);
            var unit = ctx.TargetUnit;
            if (ctx.InBattle || unit == null || !player.Units.Any(u => ReferenceEquals(u, unit)) || unit.IsDestroyed)
                throw new System.InvalidOperationException("重整旗鼓须在战斗外选择实际自有部队");
            if (option == 0 && unit.IsReady || option == 1 && unit.Wounds == 0)
                throw new System.InvalidOperationException("目标不需要所选的重整或治疗");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (option == 0) ctx.TargetUnit.Ready();
            else CardHealing.HealUnit(player, ctx, ctx.TargetUnit, 1);
        }
    }

    internal static class RegenerationSkillRules
    {
        internal static void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!System.Enum.IsDefined(typeof(ManaColor), option))
                throw new System.InvalidOperationException("请选择实际支付的魔力颜色");
            var wound = ctx.SelectedSkillWound;
            if (wound == null || wound.Type != CardType.Wound || !player.Deck.Hand.Any(c => ReferenceEquals(c, wound)))
                throw new System.InvalidOperationException("请选择本人手牌中的实际创伤");
            if (!player.Mana.TryPreviewExactColors(new[] { (ManaColor)option }, ctx, out _))
                throw new System.InvalidOperationException("不足以支付所选颜色的魔力");
        }
        internal static void Execute(PlayerState player, ActionContext ctx, int option, ManaColor drawColor)
        {
            Validate(player, ctx, option);
            var color = (ManaColor)option;
            var wound = ctx.SelectedSkillWound;
            if (!player.Mana.TryPayExactColors(new[] { color }, ctx, player))
                throw new System.InvalidOperationException("不足以支付所选颜色的魔力");
            // Removing a wound is not healing and must preserve all same-ID unselected copies.
            int index = player.Deck.Hand.FindIndex(card => ReferenceEquals(card, wound));
            player.Deck.Hand.RemoveAt(index);
            if (color == drawColor || InspirationSkillRules.IsStrictlyLowest(player, ctx))
                player.Deck.DrawExact(1);
        }
    }

    internal static class InspirationSkillRules
    {
        internal static void Validate(PlayerState player, int option)
        {
            SkillOption.Require(option, 0);
            if (player.CompletedPersonalTurns < player.InspirationBlockedUntilTurnEnd)
                throw new System.InvalidOperationException("下回合结束前不能使用其它激励技能");
        }
        internal static bool IsStrictlyLowest(PlayerState player, ActionContext ctx) =>
            ctx.OtherPlayers.Where(other => !ReferenceEquals(other, player)).All(other => player.Fame < other.Fame);
        internal static void DrawAndLock(PlayerState player)
        {
            player.Deck.DrawExact(2);
            player.InspirationBlockedUntilTurnEnd = player.CompletedPersonalTurns + (player.IsPersonalTurnActive ? 2 : 1);
        }
    }

    /// <summary>
    /// 公開談判：白晝影響力3，黑夜影響力2。
    /// </summary>
    public sealed class PublicNegotiationEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 0);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.InfluencePool += ctx.DayPart == DayPart.Day ? 3 : 2;
        }
    }

    /// <summary>
    /// 風中殘葉：獲得綠色晶體並取得白色法力。
    /// </summary>
    public sealed class LeavesInWindEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 0);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            player.Mana.AddCrystal(ManaColor.Green, 1);
            player.Mana.AddToken(ManaColor.White, 1);
        }
    }

    /// <summary>
    /// 樹梢風語：獲得白色晶體並取得綠色法力。
    /// </summary>
    public sealed class TreetopWhisperEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 0);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
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
            => throw new InvalidOperationException("领导需要选择实际部队能力并一同激活");
    }

    /// <summary>
    /// 忠誠契約：獲得永久指揮槽並招募折扣5點。
    /// </summary>
    public sealed class LoyalContractEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            throw new InvalidOperationException("忠诚契约在真实取得时生效；招募时请选择专属指挥槽");
        }
    }

    /// <summary>
    /// 激勵（白色）：抽二牌，名望最低時得白色法力。
    /// </summary>
    public sealed class MotivationWhiteEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => InspirationSkillRules.Validate(player, option);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            bool lowest = InspirationSkillRules.IsStrictlyLowest(player, ctx);
            InspirationSkillRules.DrawAndLock(player);
            if (lowest) player.Mana.AddToken(ManaColor.White, 1);
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
    public sealed class SoulGuideEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            RepeatedEffectResolution.RequireCompleted(ctx);
            if(option!=0 || ctx.InBattle && ctx.CombatPhase!=Phase.Block)
                throw new InvalidOperationException("灵魂向导请在战斗外或格挡阶段发动");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player,ctx,option);
            ctx.MovementPool += 1;
            ctx.SoulGuideBlockPending = true;
        }
    }

    /// <summary>
    /// 身經百戰：忽略物理2或非物理1點傷害。
    /// option 0 物理、1 非物理。
    /// </summary>
    public sealed class BattleHardenedEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            HeroAttackSkillRules.Mode(option);
            if (ctx.CombatPhase != Phase.AssignDamage)
                throw new InvalidOperationException("身经百战须在分配伤害阶段发动");
            int index = HeroAttackSkillRules.Target(ctx, true);
            var element = CardCombatActions.DefenseProfile(ctx.SelectedEnemy, index, ctx, ctx.SelectedEnemyAttackIndex).AttackElement;
            if (ctx.AttackElementChange.TryGetValue(index, out var replacement)) element = replacement;
            if ((element == Element.Physical) != (option == 0))
                throw new InvalidOperationException("身经百战选择必须匹配该次攻击类型");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.HardenedEnemy = ctx.SelectedEnemy;
            ctx.HardenedAttack = ctx.SelectedEnemyAttackIndex;
            if (option == 0) ctx.IgnorePhysicalDamage = 2;
            else ctx.IgnoreNonPhysicalDamage = 1;
        }
    }

    /// <summary>
    /// 戰鬥狂亂：攻擊2或4點。
    /// option 0 普通，1 翻面強化。
    /// </summary>
    public sealed class BattleFrenzyEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player,ActionContext ctx,int option) => ValidateCore(player,ctx,option,false);
        private static void ValidateCore(PlayerState player,ActionContext ctx,int option,bool borrowed)
        {
            if(!ctx.InBattle || ctx.CombatPhase!=Phase.Melee || option<0 || option>1 || !borrowed && player.FlippedSkills.Contains("hero_skill_pool_003:02"))
                throw new InvalidOperationException("战斗狂乱须在近战阶段使用正面技能标记");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ExecuteCore(player,ctx,option,false);
        }
        internal static void Borrow(PlayerState player,ActionContext ctx,int option) => ExecuteCore(player,ctx,option,true);
        private static void ExecuteCore(PlayerState player,ActionContext ctx,int option,bool borrowed)
        {
            ValidateCore(player,ctx,option,borrowed);
            ctx.MeleePool += option == 1 ? 4 : 2;
            if(option==1 && !borrowed)player.FlippedSkills.Add("hero_skill_pool_003:02");
        }
    }

    /// <summary>
    /// 薩滿儀式：取得任意顏色法力標記。
    /// option 為顏色索引。
    /// </summary>
    public sealed class ShamanRitualEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player,ActionContext ctx,int option) => ValidateCore(player,ctx,option,false);
        private static void ValidateCore(PlayerState player,ActionContext ctx,int option,bool borrowed)
        {
            if(borrowed && option==100) throw new InvalidOperationException("借用不能恢复个人萨满仪式标记");
            bool flipped=!borrowed && player.FlippedSkills.Contains("hero_skill_pool_003:03");
            if(option==100)
            {
                if(!flipped || ctx.InBattle || ctx.TurnActionCommitted || ctx.RestedThisTurn)
                    throw new InvalidOperationException("须以未使用的战斗外回合行动翻回萨满仪式");
                RepeatedEffectResolution.RequireCompleted(ctx);
            }
            else if(flipped || !System.Enum.IsDefined(typeof(ManaColor),option))
                throw new InvalidOperationException("请选择正面萨满仪式的一种实际魔力颜色");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            ExecuteCore(player,ctx,option,false);
        }
        internal static void Borrow(PlayerState player,ActionContext ctx,int option) => ExecuteCore(player,ctx,option,true);
        private static void ExecuteCore(PlayerState player,ActionContext ctx,int option,bool borrowed)
        {
            ValidateCore(player,ctx,option,borrowed);
            if(option==100){ctx.TurnActionCommitted=true;player.FlippedSkills.Remove("hero_skill_pool_003:03");return;}
            ManaColor color = (ManaColor)option;
            player.Mana.AddToken(color, 1);
            if(!borrowed)player.FlippedSkills.Add("hero_skill_pool_003:03");
        }
    }

    /// <summary>
    /// 再生（紅）：支付法力移除創傷，使用紅色或最低名望時抽牌。
    /// </summary>
    public sealed class RegenerationRedEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => RegenerationSkillRules.Validate(player, ctx, option);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            RegenerationSkillRules.Execute(player, ctx, option, ManaColor.Red);
        }
    }

    /// <summary>
    /// 奧術偽裝：影響力2或本回合忽略聲譽軌位置的作用，仍保留聲譽增減。
    /// option 0 影響力、1 忽略聲望。
    /// </summary>
    public sealed class ArcaneDisguiseEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var skill=player.Skills.FirstOrDefault(s=>s.SourceId==HeroSocialSkillRules.Disguise);
            HeroSocialSkillRules.Use(player,ctx,skill,option);
        }
    }

    /// <summary>
    /// 傀儡大師：保存或棄置敵人標記獲得攻擊/格擋。
    /// option 0 保存 ctx.KillEnemyIndex 指定的敵人；1 消耗獲得攻擊；2 消耗獲得格擋。
    /// </summary>
    public sealed class PuppetMasterEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player,ActionContext ctx,int option)
            => throw new InvalidOperationException("傀儡大师需要选择实际击败或已保存的敌人标记");
        public void Execute(PlayerState player,ActionContext ctx,int option=0)=>Validate(player,ctx,option);
    }

    /// <summary>
    /// 混亂大師：每次旋轉顏色獲得對應效果，金色由 option 決定。
    /// </summary>
    public sealed class ChaosMasterEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player,ActionContext ctx,int option)=>ChaosSkillRules.Validate(player,ctx,option);
        public void Execute(PlayerState player,ActionContext ctx,int option=0)=>ChaosSkillRules.Apply(player,ctx,option);
    }

    /// <summary>
    /// 詛咒：降低敵人攻擊或護甲。
    /// option 0 减攻，1 减甲；目标来自实际选敌和攻击槽。
    /// </summary>
    public sealed class HexCurseEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            HeroAttackSkillRules.Mode(option);
            int index = HeroAttackSkillRules.Target(ctx, option == 0);
            if (ctx.CombatPhase == Phase.Ranged && CombatMath.IsFortified(ctx.SelectedEnemy, index, ctx))
                throw new InvalidOperationException("诅咒在远程和攻城阶段不能选择有城防敌人");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (option == 1) ctx.AddSkillArmorReduction(ctx.SelectedEnemy, 1);
            else ctx.AddSkillAttackAdjustment(ctx.SelectedEnemy, ctx.SelectedEnemyAttackIndex, -1);
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
            throw new System.InvalidOperationException("互动技能必须通过实际游戏的共享技能区发动");
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
    public sealed class RefreshBathEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.InBattle || option != 0) throw new System.InvalidOperationException("提神沐浴只能在战斗外按原效果使用");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.Healing.Add(1);
            player.Mana.AddCrystal(ManaColor.Blue, 1);
        }
    }

    /// <summary>
    /// 提神微風：治療1並獲得白色晶體。
    /// </summary>
    public sealed class RefreshBreezeEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.InBattle || option != 0) throw new System.InvalidOperationException("提神微风只能在战斗外按原效果使用");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.Healing.Add(1);
            player.Mana.AddCrystal(ManaColor.White, 1);
        }
    }

    /// <summary>
    /// 神鷹之眼：移動力1，夜晚探索減費1，白晝可偵查城防。
    /// </summary>
    public sealed class EagleEyeEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (option != 0 || ctx.InBattle || ctx.TurnActionCommitted || ctx.RestedThisTurn)
                throw new InvalidOperationException("神鹰之眼移动只能在行动前使用");
            RepeatedEffectResolution.RequireCompleted(ctx);
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.MovementPool += 1;
        }
    }

    /// <summary>
    /// 特立獨行：影響力1，或承諾本回合不以影響力招募而取得3。
    /// </summary>
    public sealed class LoneWolfEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            var skill=player.Skills.FirstOrDefault(s=>s.SourceId==HeroSocialSkillRules.LoneWolf);
            HeroSocialSkillRules.Use(player,ctx,skill,option);
        }
    }

    /// <summary>
    /// 百步穿楊：遠程或攻城+1，或近戰+2。
    /// option 0 遠程，1 近戰。
    /// </summary>
    public sealed class BullseyeEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
            => throw new InvalidOperationException("百步穿杨需要选择真实原牌的攻击贡献");
    }

    /// <summary>
    /// 嘲諷：降低敵人攻擊或提高攻擊並削弱護甲。
    /// option 0 减攻，1 增攻并减甲；目标来自实际选敌和攻击槽。
    /// </summary>
    public sealed class TauntEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            HeroAttackSkillRules.Mode(option);
            if (ctx.CombatPhase != Phase.Block) throw new InvalidOperationException("嘲讽须在格挡阶段发动");
            HeroAttackSkillRules.Target(ctx, true);
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.AddSkillAttackAdjustment(ctx.SelectedEnemy, ctx.SelectedEnemyAttackIndex, option == 0 ? -1 : 2);
            if (option == 1) ctx.AddTauntArmorReduction(ctx.SelectedEnemy, ctx.SelectedEnemyAttackIndex);
        }
    }

    internal static class HeroAttackSkillRules
    {
        internal static void Mode(int option)
        {
            if (option != 0 && option != 1) throw new InvalidOperationException("请选择技能的两种原版效果之一");
        }
        internal static int Target(ActionContext ctx, bool attack)
        {
            int index = ctx.Enemies == null ? -1 : Enumerable.Range(0, ctx.Enemies.Count)
                .Where(i => ReferenceEquals(ctx.Enemies[i], ctx.SelectedEnemy)).DefaultIfEmpty(-1).First();
            if (!ctx.InBattle || index < 0 || ctx.DefeatedEnemyIndices.Contains(index)
                || ctx.CombatPhase == Phase.End)
                throw new InvalidOperationException("必须选择本场存活的实际敌人");
            if (attack && (ctx.CombatPhase == Phase.Melee || ctx.SelectedEnemyAttackIndex < 0
                || ctx.SelectedEnemyAttackIndex >= ctx.SelectedEnemy.AttackCount
                || ctx.SkipAttackIndices.Contains(index) || ctx.ColdStareSkips(ctx.SelectedEnemy)
                || ctx.IsAttackCancelled(index, ctx.SelectedEnemyAttackIndex)
                || ctx.SuccessfullyBlockedAttacks.Contains((index, ctx.SelectedEnemyAttackIndex))))
                throw new InvalidOperationException("必须选择尚可发动的实际攻击");
            return index;
        }
    }

    /// <summary>
    /// 決鬥：格擋1並對同一敵人攻擊1，擊殺額外得名望1。
    /// </summary>
    public sealed class DuelEffect : ICardEffect, ICardEffectValidator
    {
        private static SkillCard Owned(PlayerState player) => player.Skills.SingleOrDefault(s=>s.SourceId=="hero_skill_pool_004:07");
        public void Validate(PlayerState player,ActionContext ctx,int option)
        {
            if(option!=0)throw new InvalidOperationException("请选择原决斗效果");
            DuelActions.Validate(player,ctx,Owned(player));
        }
        public void Execute(PlayerState player,ActionContext ctx,int option=0)
        { Validate(player,ctx,option);DuelActions.Use(player,ctx,Owned(player)); }
    }

    /// <summary>
    /// 激勵（名望）：抽二牌，名望最低時獲得1點名望。
    /// </summary>
    public sealed class MotivationFameEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => InspirationSkillRules.Validate(player, option);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            bool lowest = InspirationSkillRules.IsStrictlyLowest(player, ctx);
            InspirationSkillRules.DrawAndLock(player);
            if (lowest) player.Fame += 1;
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
    public sealed class FrozenPowerEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 1);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.CombatPower.AddAttack(1, option == 0 ? Element.Physical : Element.Ice, AttackType.Siege);
        }
    }

    /// <summary>
    /// 藥劑製作：戰鬥外獲得治療2，供玩家選擇英雄或部隊消費。
    /// </summary>
    public sealed class PotionCraftEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (ctx.InBattle || option != 0) throw new System.InvalidOperationException("药剂制作只能在战斗外按原效果使用");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.Healing.Add(2);
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
    public sealed class CrystalLuckEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            SkillOption.Require(option, 0);
            if (!ctx.InNegotiation || ctx.InteractionClosed || ctx.InBattle)
                throw new System.InvalidOperationException("荧光灵运只能在实际交涉期间使用");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
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
            => throw new InvalidOperationException("飞行需要选择真实安全落点并按距离支付移动力");
    }

    /// <summary>
    /// 通用力量：橫置牌支付魔力後值提升為3或4。
    /// 必须通过 SkillActions.PlaySideways 选择实际牌、用途与魔力。
    /// </summary>
    public sealed class UniversalPowerEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            throw new InvalidOperationException("通用力量须选择真实手牌、横置用途和魔力");
        }
    }

    /// <summary>
    /// 激勵（綠色）：抽兩牌，若名望最低則得綠色法力。
    /// </summary>
    public sealed class MotivationGreenEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => InspirationSkillRules.Validate(player, option);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            bool lowest = InspirationSkillRules.IsStrictlyLowest(player, ctx);
            InspirationSkillRules.DrawAndLock(player);
            if (lowest) player.Mana.AddToken(ManaColor.Green, 1);
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
            throw new System.InvalidOperationException("互动技能必须通过实际游戏的共享技能区发动");
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
    public sealed class NightSniperEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 0);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.RangedPool += ctx.DayPart == DayPart.Day ? 1 : 2;
        }
    }

    /// <summary>
    /// 寒冰劍術：攻擊2或寒冰攻擊2。
    /// option 0 物理，1 寒冰。
    /// </summary>
    public sealed class IceSwordEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 1);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.CombatPower.AddAttack(2, option == 0 ? Element.Physical : Element.Ice);
        }
    }

    /// <summary>
    /// 盾牌精通：格擋3，寒冰格擋2或火焰格擋2。
    /// option 0 物理，1 冰，2 火。
    /// </summary>
    public sealed class ShieldMasteryEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => SkillOption.Require(option, 2);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            ctx.CombatPower.AddBlock(option == 0 ? 3 : 2,
                option == 1 ? Element.Ice : option == 2 ? Element.Fire : Element.Physical);
        }
    }

    /// <summary>
    /// 抗性弱化：按抗性種類降低敵人護甲。
    /// option 目標索引。
    /// </summary>
    public sealed class ResistWeakeningEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (!ctx.InBattle || ctx.Enemies == null || option < 0 || option >= ctx.Enemies.Count || ctx.DefeatedEnemyIndices.Contains(option))
                throw new System.InvalidOperationException("请选择本场战斗中存活的敌人");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            var m = ctx.Enemies[option];
            int count = new[] { Element.Physical, Element.Fire, Element.Ice }
                .Count(element => CombatMath.HasResistance(m, option, element, ctx));
            ctx.AddSkillArmorReduction(m, count);
        }
    }

    /// <summary>
    /// 無懼痛苦：戰鬥外棄傷牌並抽牌。
    /// </summary>
    public sealed class FearlessPainEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            SkillOption.Require(option,0);
            var selected = ctx.SelectedSkillWound;
            if(ctx.InBattle || (selected == null ? !player.Deck.Hand.Any(c=>c.Type==CardType.Wound)
                : selected.Type != CardType.Wound || !player.Deck.Hand.Any(c=>ReferenceEquals(c,selected))))
                throw new System.InvalidOperationException("无惧痛苦只能在战斗外弃掉一张实际手牌创伤");
        }
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player,ctx,option);
            var w = ctx.SelectedSkillWound ?? player.Deck.Hand.FirstOrDefault(c => c.Type == CardType.Wound);
            if (w != null)
            {
                player.Deck.TryDiscardExact(w);
                player.Deck.DrawExact(1);
            }
        }
    }

    /// <summary>
    /// 漫不經心：橫置牌+2或+3。
    /// 必须通过 SkillActions.PlaySideways 从实际原卡判定类别。
    /// </summary>
    public sealed class CarelessEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            throw new InvalidOperationException("漫不经心须选择真实手牌与横置用途");
        }
    }

    /// <summary>
    /// 魔法抗拒：若未使用公共骰子橫置牌+3，否則+2。
    /// </summary>
    public sealed class MagicRepelEffect : ICardEffect
    {
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
            => throw new InvalidOperationException("魔法抗拒必须选择实际横置手牌及是否本回合放弃源骰");
    }

    /// <summary>
    /// 激勵（藍色）：名望最低時獲得藍色法力。
    /// </summary>
    public sealed class MotivationBlueEffect : ICardEffect, ICardEffectValidator
    {
        public void Validate(PlayerState player, ActionContext ctx, int option) => InspirationSkillRules.Validate(player, option);
        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            bool lowest = InspirationSkillRules.IsStrictlyLowest(player, ctx);
            InspirationSkillRules.DrawAndLock(player);
            if (lowest) player.Mana.AddToken(ManaColor.Blue, 1);
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
            throw new System.InvalidOperationException("互动技能必须通过实际游戏的共享技能区发动");
        }
    }
}

