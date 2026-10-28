using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed record PveMindReadResponse(PlayerState Player, DeedCard Card);

    public sealed class PveDeedSelection
    {
        public ManaDie[] Dice { get; init; }=Array.Empty<ManaDie>();
        public ManaColor[] Colors { get; init; }=Array.Empty<ManaColor>();
        public DeedCard[] Cards { get; init; }=Array.Empty<DeedCard>();
        public ManaColor? Crystal { get; init; }
        public AdvActionCard Advanced { get; init; }
        public SpellCard[] Spells { get; init; }=Array.Empty<SpellCard>();
        public PveMindReadResponse[] MindResponses { get; init; }
        public DeedCard MindStealTarget { get; init; }
        public UnitState Unit { get; init; }
        // Null preserves legacy outer targets; an explicit empty array selects no units.
        public UnitState[] Units { get; init; }
        public int[] BonusChoices { get; init; }=Array.Empty<int>();
        public UnitCard OfferedUnit { get; init; }
        public UnitState DisbandUnit { get; init; }
        public string BorrowedChoice { get; init; }
        public PveDeedSelection BorrowedInputs { get; init; }
        public bool WoundToDiscard { get; init; }
        public int TargetIndex { get; init; }=-1;
        public int AttackIndex { get; init; }
        public Map.AxialCoord[] TravelPath { get; init; }=Array.Empty<Map.AxialCoord>();
        public int[] EnemyIndices { get; init; }=Array.Empty<int>();
    }

    /// <summary>Actual player choices scoped around the existing original-card command.</summary>
    public static class PveDeedInputs
    {
        public static bool Supports(string id)=>id is "basic_card_009" or "basic_card_013" or "basic_card_023"
            or "advanced_card_012" or "advanced_card_013" or "advanced_card_021" or "advanced_card_022" or "magic_002"
            or "advanced_card_020" or "advanced_card_029" or "advanced_card_027" or "advanced_card_033"
            or "advanced_card_037" or "advanced_card_038" or "advanced_card_040" or "magic_011" or "magic_020"
            or "magic_005" or "advanced_card_024" or "advanced_card_025" or "advanced_card_030" or "magic_006" or "magic_012" or "magic_018" or "magic_004" or "magic_023";
        public static bool RemovesMaterial(string id)=>id is "advanced_card_020" or "advanced_card_029" or "advanced_card_027";
        public static int ColorCount(string id,bool strong)=>id switch {
            "basic_card_009"=>strong?1:0,"basic_card_013"=>strong?2:0,"advanced_card_012"=>strong?3:1,
            "advanced_card_021" or "advanced_card_040"=>strong?0:1,
            "magic_006"=>strong?1:0,"magic_012" or "magic_018"=>1,_=>0};

        public static int ManaPowerSetsAfterPayment(PlayerState player,ActionContext ctx,BattleCardChoice choice)
        {
            if(!choice.Enhanced && !ctx.NextCardEnhanced)return 0;
            var cost=ctx.NextCardEnhanced?Array.Empty<ManaColor>():new[]{choice.ActionMana??throw new InvalidOperationException("请选择蓝或绿的原费用")};
            if(!player.Mana.TryPreviewExactColors(cost,ctx,out var remaining))throw new InvalidOperationException("所选原费用的魔力不足");
            return Enumerable.Range(0,4).Min(i=>remaining.TryGetValue((ManaColor)i,out var amount)?amount:0);
        }

        public static void Execute(PlayerState player,ActionContext ctx,DeedCard card,BattleCardChoice choice,
            PveDeedSelection selection,Action<int> play)
        {
            if(!Supports(card.Id)){play(choice.Option);return;}
            var input=selection??new PveDeedSelection();
            var colors=input.Colors??Array.Empty<ManaColor>();var dice=input.Dice??Array.Empty<ManaDie>();var cards=input.Cards??Array.Empty<DeedCard>();
            bool strong=choice.Enhanced || card.Type!=CardType.Spell && ctx.NextCardEnhanced;
            if (card.Id == "magic_005")
            {
                var previousUnits = ctx.ReadyUnitTargets;
                var previousUnit = ctx.TargetUnit;
                try
                {
                    if (input.Units != null)
                    {
                        ctx.ReadyUnitTargets = input.Units.ToList();
                        ctx.TargetUnit = input.Units.FirstOrDefault();
                    }
                    else if (input.Unit != null)
                    {
                        ctx.TargetUnit = input.Unit;
                        ctx.ReadyUnitTargets = new System.Collections.Generic.List<UnitState> { input.Unit };
                    }
                    play(choice.Option);
                }
                finally { ctx.ReadyUnitTargets = previousUnits; ctx.TargetUnit = previousUnit; }
                return;
            }
            if(card.Id is "magic_004" or "magic_023")
            {
                var oldPath=ctx.TravelPath.ToArray();var oldTargets=ctx.SpellTargetIndices.ToArray();
                var path=input.TravelPath??Array.Empty<Map.AxialCoord>();var targets=input.EnemyIndices??Array.Empty<int>();
                if(card.Id=="magic_023"&&strong && (targets.Length==0 || targets.Any(i=>ctx.DefeatedEnemyIndices.Contains(i))))
                    throw new InvalidOperationException("请选择尚未击败的实际敌人");
                try
                {
                    ctx.TravelPath.Clear();ctx.TravelPath.AddRange(path);ctx.SpellTargetIndices.Clear();ctx.SpellTargetIndices.AddRange(targets);
                    play(card.Id=="magic_004"?0:strong?targets.Length:path.Length);
                }
                finally {ctx.TravelPath.Clear();ctx.TravelPath.AddRange(oldPath);ctx.SpellTargetIndices.Clear();ctx.SpellTargetIndices.AddRange(oldTargets);}
                return;
            }
            if(card.Id=="advanced_card_024" || card.Id=="advanced_card_030"&&strong)
            {PveCopyInputs.Execute(player,ctx,card,strong,input,play);return;}
            int needed=ColorCount(card.Id,strong),option=choice.Option;
            if(colors.Length!=needed || colors.Any(c=>!Enum.IsDefined(typeof(ManaColor),c)))throw new InvalidOperationException("请逐枚选择原效果要求的魔力颜色");
            if(needed>0)option=(int)colors[0];
            if(card.Id is "magic_006" or "magic_012" or "magic_018")
            {
                if(colors.Any(c=>c<ManaColor.Red || c>ManaColor.White))throw new InvalidOperationException("请选择红蓝绿白基本色");
                if(card.Id=="magic_012")option|=choice.Option;
            }
            if(card.Id=="basic_card_013" && strong)option|=(int)colors[1]<<8;
            if(card.Id=="advanced_card_022" && !strong)
            {
                if(dice.Length!=1 || ctx.ManaSource==null)throw new InvalidOperationException("请选择一颗实际公共源骰");
                option=ctx.ManaSource.Dice.ToList().FindIndex(d=>ReferenceEquals(d,dice[0]));
            }
            if(card.Id=="basic_card_023" && (cards.Length!=1 || ReferenceEquals(cards[0],card)))
                throw new InvalidOperationException("请选择另一张实际非创伤手牌作为随机应变材料");
            if(card.Id=="advanced_card_037" && !strong && cards.Length>1)
                throw new InvalidOperationException("不屈不挠基础至多弃一张实际伤牌");
            if(RemovesMaterial(card.Id) && (cards.Length!=1 || ReferenceEquals(cards[0],card)))
                throw new InvalidOperationException("请选择另一张实际原行动手牌作为永久去除材料");
            if(card.Id=="advanced_card_027")
            {
                option=ctx.AdvActionSupply?.Offer.FindIndex(c=>ReferenceEquals(c,input.Advanced))??-1;
                if(option<0)throw new InvalidOperationException("请选择实际高级行动供应中的同色原牌");
            }
            if(card.Id=="advanced_card_033")
            {
                var selected=input.Spells??Array.Empty<SpellCard>();
                if(selected.Length!=(strong?2:1))throw new InvalidOperationException("请选择原法术熔炉所需数量的实际供应牌");
                var indices=selected.Select(spell=>ctx.SpellSupply?.Offer.FindIndex(c=>ReferenceEquals(c,spell))??-1).ToArray();
                if(indices.Any(i=>i<0))throw new InvalidOperationException("所选法术已不在实际供应中");
                option=indices[0]|(strong?indices[1]<<8:0);
            }
            if(card.Id=="magic_011")
            {
                // The independent crystal choices use the original effect's packed option format.
                var chosen=input.BonusChoices??Array.Empty<int>();
                if(strong)
                {
                    if(chosen.Length!=2)throw new InvalidOperationException("请选择绿或白，再选择红或蓝");
                    option=chosen[0]|(chosen[1]<<8);
                }
                else
                {
                    if(cards.Any(c=>ReferenceEquals(c,card)))throw new InvalidOperationException("献祭不能用本牌作为材料");
                    if(chosen.Length!=cards.Count(c=>c.Type==CardType.Artifact)||chosen.Any(c=>c<0||c>3))
                        throw new InvalidOperationException("请为每件神器独立选择基本色魔晶");
                    option=0;for(int i=0;i<chosen.Length;i++)option|=chosen[i]<<(2*i);
                }
            }
            if(card.Id=="magic_020"&&strong)
            {
                option=ctx.UnitOffer?.ToList().FindIndex(c=>ReferenceEquals(c,input.OfferedUnit))??-1;
                if(option<0)throw new InvalidOperationException("请选择实际部队供应");
            }
            if(card.Id=="advanced_card_025")
            {
                if(strong)
                {
                    var spells=input.Spells??Array.Empty<SpellCard>();
                    int index=spells.Length==1?ctx.SpellSupply?.Offer.FindIndex(s=>ReferenceEquals(s,spells[0]))??-1:-1;
                    if(index<0)throw new InvalidOperationException("请选择一张实际供应法术");
                    option=(int)(CardColorHelper.Get(spells[0].Id)??throw new InvalidOperationException("原法术颜色缺失"))|(index<<8);
                }
                else if(cards.Length!=1||ReferenceEquals(cards[0],card))throw new InvalidOperationException("请选择另一张实际有色手牌");
            }
            if(card.Id=="advanced_card_030")
            {
                int index=ctx.AdvActionSupply?.Offer.FindIndex(a=>ReferenceEquals(a,input.Advanced))??-1;
                if(index<0||input.WoundToDiscard)throw new InvalidOperationException("基础血脉须选实际原行动，伤牌入手");
                option=index|((int)(CardColorHelper.Get(input.Advanced.Id)??throw new InvalidOperationException("原行动颜色缺失"))<<8);
            }
            bool installMind = card.Id == "magic_018" && input.MindResponses != null;
            if (installMind)
            {
                var seen = new System.Collections.Generic.HashSet<PlayerState>();
                foreach (var response in input.MindResponses)
                    if (response == null || response.Player == null
                        || !ctx.OtherPlayers.Any(p => ReferenceEquals(p, response.Player)) || !seen.Add(response.Player))
                        throw new InvalidOperationException("思维读取响应必须来自不同的实际其他玩家");
            }
            var oldMind = installMind ? ctx.MindReadChoices.ToArray() : null;
            var oldSteal = ctx.MindStealTarget;
            var oldDice=ctx.ManaDiceToTransform.ToArray();var oldColors=ctx.BloodRitualColors.ToArray();
            var oldCrystal=ctx.BloodRitualCrystalColor;var oldCard=ctx.CardToDiscard;var oldDiscard=ctx.CardsFromDiscard.ToArray();
            var oldRemove=ctx.CardToRemove;
            var oldMany=ctx.CardsToDiscard.ToArray();var oldUnit=ctx.TargetUnit;var oldBonus=ctx.ManaPowerBonusChoices.ToArray();var oldDisband=ctx.UnitToDisband;
            try
            {
                if (installMind)
                {
                    ctx.MindReadChoices.Clear();
                    foreach (var response in input.MindResponses) ctx.MindReadChoices.Add(response.Player, response.Card);
                    ctx.MindStealTarget = input.MindStealTarget;
                }
                if(card.Id is "basic_card_009" or "basic_card_013") {ctx.ManaDiceToTransform.Clear();ctx.ManaDiceToTransform.AddRange(dice);}
                if(card.Id=="advanced_card_012") {ctx.BloodRitualColors.Clear();if(strong)ctx.BloodRitualColors.AddRange(colors);ctx.BloodRitualCrystalColor=input.Crystal;}
                if(card.Id=="basic_card_023")ctx.CardToDiscard=cards[0];
                if(card.Id=="advanced_card_025"&&!strong)ctx.CardToDiscard=cards[0];
                if(RemovesMaterial(card.Id))ctx.CardToRemove=cards[0];
                if(card.Id=="magic_002") {ctx.CardsFromDiscard.Clear();ctx.CardsFromDiscard.AddRange(cards);}
                if(card.Id=="advanced_card_037") {ctx.CardToDiscard=strong?null:cards.FirstOrDefault();ctx.CardsToDiscard.Clear();if(strong)ctx.CardsToDiscard.AddRange(cards);}
                if(card.Id=="advanced_card_038")ctx.TargetUnit=input.Unit;
                if(card.Id=="advanced_card_040") {ctx.ManaPowerBonusChoices.Clear();ctx.ManaPowerBonusChoices.AddRange(input.BonusChoices??Array.Empty<int>());}
                if(card.Id=="magic_011") {ctx.CardsToDiscard.Clear();if(!strong)ctx.CardsToDiscard.AddRange(cards);}
                if(card.Id=="magic_020"&&strong)ctx.UnitToDisband=input.DisbandUnit;
                play(option);
            }
            finally
            {
                if (installMind)
                {
                    ctx.MindReadChoices.Clear();
                    foreach (var pair in oldMind) ctx.MindReadChoices.Add(pair.Key, pair.Value);
                    ctx.MindStealTarget = oldSteal;
                }
                ctx.ManaDiceToTransform.Clear();ctx.ManaDiceToTransform.AddRange(oldDice);
                ctx.BloodRitualColors.Clear();ctx.BloodRitualColors.AddRange(oldColors);ctx.BloodRitualCrystalColor=oldCrystal;
                ctx.CardToDiscard=oldCard;ctx.CardsFromDiscard.Clear();ctx.CardsFromDiscard.AddRange(oldDiscard);
                ctx.CardToRemove=oldRemove;
                ctx.CardsToDiscard.Clear();ctx.CardsToDiscard.AddRange(oldMany);ctx.TargetUnit=oldUnit;
                ctx.ManaPowerBonusChoices.Clear();ctx.ManaPowerBonusChoices.AddRange(oldBonus);
                ctx.UnitToDisband=oldDisband;
            }
        }
    }
}
