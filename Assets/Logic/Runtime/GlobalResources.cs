using MK.Logic.Core;
using MK.Logic.Data;
using System;
using System.Collections.Generic;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 封裝所有全局共享的資源堆疊，包含公共魔力池及獎勵牌堆。
    /// </summary>
    public sealed class GlobalResources
    {
        public ManaSource Mana { get; }
        public AdvActionSupply AdvActions { get; } = new();
        public SpellSupply Spells { get; } = new();
        public ArtifactSupply Artifacts { get; } = new();
        public SkillSupply Skills { get; } = new();
        public UnitSupply Units { get; } = new();
        public EnemyTokenSupply Enemies { get; set; } = new();
        public RuinTokenSupply Ruins { get; set; }
        /// <summary>修道院提供的額外高級行動牌。</summary>
        public List<AdvActionCard> MonasteryOffer { get; } = new();

        public GlobalResources(int playerCount, Func<DayPart> timeProvider, System.Random? rnd = null)
        {
            Mana = new ManaSource(playerCount, timeProvider, rnd);
        }
        public void RefreshMonasteryOffer(int activeMonasteries)
        {
            if(activeMonasteries<0)throw new ArgumentOutOfRangeException(nameof(activeMonasteries));
            foreach(var card in MonasteryOffer)AdvActions.PutUnder(card);
            MonasteryOffer.Clear();
            for(int i=0;i<activeMonasteries&&AdvActions.TryDraw(out var card);i++)MonasteryOffer.Add(card);
        }

        /// <summary>
        /// 轮次结束时轮换公共供给；每种供给仅最底一张回到对应牌库底。
        /// </summary>
        public void EndRound(bool coreRevealed, bool advancedBottomTransferredToDummy = false)
        {
            Mana.ReturnRoundClaims();
            Mana.ResetSource();
            if (advancedBottomTransferredToDummy) AdvActions.FillVacancies();
            else AdvActions.Refill(coreRevealed);
            Spells.Refill();
        }
    }
}
