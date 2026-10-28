using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime.CardEffects;

namespace MK.Logic.Runtime
{
    /// <summary>实际手牌出牌入口：复用原效果系统，成功后把所选实例移到已打出区。</summary>
    public static class CardPlayActions
    {
        /// <summary>保留基础入口，复用同一原武器出牌命令。</summary>
        public static void PlayWeaponBase(PlayerState player, ActionContext context, DeedCard instance, ItemCardData original)
            => PlayWeapon(player, context, instance, original);

        public static void PlayWeapon(PlayerState player, ActionContext context, DeedCard instance, ItemCardData original, bool burn = false)
            => PlayArtifact(player, context, instance, original, burn);

        public static void PlayArtifact(PlayerState player, ActionContext context, DeedCard instance, ItemCardData original, bool burn = false, int option = 0)
        {
            if (context.ManaSkillPayment != null)
                throw new InvalidOperationException("神器内部付款尚不支持魔咒或两极分化，请选择已有整笔费用入口");
            RepeatedEffectResolution.RequireCompleted(context);
            if (instance == null || instance.Id != original.Id || instance.Type != CardType.Artifact
                || !player.Deck.Hand.Any(c => ReferenceEquals(c, instance)))
                throw new InvalidOperationException("武器必须选择实际持有且与原定义一致的神器实例");
            if (!burn && context.CardsToDiscard.Any(c => ReferenceEquals(c, instance)))
                throw new InvalidOperationException("正在使用的武器不能弃掉自身");
            if (original.Id == "items_024")
            {
                MysticBoxEffect.Reveal(player, context, instance, burn);
                return;
            }
            if (original.Id == "items_000" && !burn)
            {
                new HonorBannerEffect(false).Assign(player, context, instance);
                return;
            }
            if (original.Id == "items_001" && !burn)
            {
                BannerActions.AssignFear(player, context.TargetUnit!, instance);
                return;
            }
            if (original.Id == "items_018" && !burn)
            {
                BannerActions.AssignFortitude(player,context.TargetUnit!,instance);
                return;
            }
            if (original.Id == "items_003" && !burn)
            {
                BannerActions.AssignCourage(player,context.TargetUnit!,instance);
                return;
            }
            if (original.Id == "items_002" && !burn)
            {
                BannerActions.AssignProtection(player,context.TargetUnit!,instance);
                return;
            }
            ICardEffect effect = ArtifactEffect(original.Id, burn);
            ManaSkillPaymentActions.ValidateCardMaterial(context, instance);
            using var source = CardContributionSource.Scope(context, instance);
            context.EnsureBattleWounds(player);
            effect.Execute(player, context, option);
            if (!(burn ? player.Deck.TryRemovePermanentlyExact(instance) : player.Deck.TryPutInPlay(instance)))
                throw new InvalidOperationException("武器执行后丢失真实手牌实例");
            if (burn) player.Deck.RecordCardPlayed();
            if (original.Id == "items_017" && !burn) context.CommandBannersPlayedThisTurn.Add(instance);
            context.ResolveBattleWounds(player);
        }

        /// <summary>使用本回合揭示的原神器；执行原效果，普通/焚毁去向均绑定盒本体。</summary>
        public static void UseMysticBox(PlayerState player, ActionContext context, DeedCard box, bool burn = false, int option = 0)
            => MysticBoxEffect.Use(player, context, box, burn, option);

        internal static ICardEffect ArtifactEffect(string id, bool burn)
            => id switch
            {
                "items_000" => new CardEffects.HonorBannerEffect(burn),
                "items_001" => new CardEffects.FearBannerEffect(burn),
                "items_003" => new CardEffects.CourageBannerEffect(burn),
                "items_002" => new CardEffects.FortitudeBannerEffect(burn),
                "items_004" => new CardEffects.RubyRingEffect(burn),
                "items_005" => new CardEffects.SapphireRingEffect(burn),
                "items_006" => new CardEffects.DiamondRingEffect(burn),
                "items_007" => new CardEffects.EmeraldRingEffect(burn),
                "items_008" => new CardEffects.JusticeSwordEffect(burn),
                "items_009" => new CardEffects.WrathHornEffect(burn),
                "items_010" => new CardEffects.GoldPouchEffect(burn),
                "items_011" => new CardEffects.GemBagEffect(burn),
                "items_012" => new CardEffects.HolyGrailEffect(burn),
                "items_013" => new CardEffects.WisdomBookEffect(burn),
                "items_014" => new CardEffects.SunAmuletEffect(burn),
                "items_015" => new CardEffects.DarkAmuletEffect(burn),
                "items_016" => new CardEffects.StarBowEffect(burn),
                "items_017" => new CardEffects.CommandBannerEffect(burn),
                "items_018" => new CardEffects.ToughBannerEffect(burn),
                "items_019" => new CardEffects.SoulHarvesterEffect(burn),
                "items_020" => new CardEffects.NecroShieldEffect(burn),
                "items_021" => new CardEffects.DruidStaffEffect(burn),
                "items_022" => new CardEffects.HeadbandEffect(burn),
                "items_023" => new CardEffects.SpellTomeEffect(burn),
                _ => throw new InvalidOperationException("此原神器尚未接入真实出牌入口")
            };

        public static void Play(PlayerState player, ActionContext context, DeedCard instance,
            ActionCardData original, bool enhanced = false, int option = 0, ActionSystem actionSystem = null)
        {
            RepeatedEffectResolution.RequireCompleted(context);
            if (instance == null || !player.Deck.Hand.Any(c => ReferenceEquals(c, instance))
                || instance.Id != original.Id || instance.Type != (original.Set == CardSet.Spell ? CardType.Spell : CardType.Action))
                throw new InvalidOperationException("出牌必须选择实际持有且与原定义一致的卡牌实例");
            if (ReferenceEquals(context.CardToDiscard, instance) || ReferenceEquals(context.CardToRemove, instance)
                || context.CardsToDiscard.Any(c => ReferenceEquals(c, instance))
                || context.OverloadSelections?.Any(selection => selection?.Uses(instance) == true) == true)
                throw new InvalidOperationException("正在打出的卡不能充当另一张弃牌或去除材料");
            ManaSkillPaymentActions.ValidateCardMaterial(context, instance);
            using var source = CardContributionSource.Scope(context, instance);
            context.EnsureBattleWounds(player);
            bool joyfulManaEnhanced = enhanced || context.NextCardEnhanced;
            (actionSystem ?? new ActionSystem()).Play(original, player, context, enhanced, option);
            if (!player.Deck.TryPutInPlay(instance))
                throw new InvalidOperationException("所选出牌实例在效果执行后不在手牌中");
            if (original.Id == "magic_016" && joyfulManaEnhanced)
                context.TimeBendingCard = instance;
            if (original.Id == "basic_card_019")
                context.JoyfulManaPlayed.Add((instance, joyfulManaEnhanced));
            if (original.Id == "advanced_card_017")
                context.SteadyTempoPlayed.Add((instance, joyfulManaEnhanced));
            // Resolve self-inflicted knockout after this card has reached its real played zone.
            if (context.PendingRepeatedEffect != null)
                context.PendingRepeatedEffect.AfterCompletion += () => context.ResolveBattleWounds(player);
            else context.ResolveBattleWounds(player);
        }

        /// <summary>选择回合结束时收回的实际欢悦魔晶，以及支付的另一张手牌。</summary>
        public static void SelectJoyfulManaReturn(PlayerState player, ActionContext context,
            DeedCard instance, DeedCard discard)
            => JoyfulManaEffect.SelectReturn(player, context, instance, discard);

        public static void SelectSteadyTempoReturn(PlayerState player, ActionContext context,
            DeedCard instance, bool recycle)
            => SteadyTempoEffect.SelectReturn(player, context, instance, recycle);

        public static void ContinueRepeatedEffect(PlayerState player, ActionContext context, RepeatedCardSelection selection)
        {
            if (context.PendingRepeatedEffect == null)
                throw new InvalidOperationException("没有待继续的重复效果");
            context.PendingRepeatedEffect.Continue(player, context, selection);
        }
    }
}
