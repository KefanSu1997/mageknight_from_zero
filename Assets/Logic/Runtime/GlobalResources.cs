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
        /// <summary>修道院提供的額外高級行動牌。</summary>
        public List<AdvActionCard> MonasteryOffer { get; } = new();

        public GlobalResources(int playerCount, Func<DayPart> timeProvider, System.Random? rnd = null)
        {
            Mana = new ManaSource(playerCount, timeProvider, rnd);
        }

        /// <summary>
        /// 結束回合時刷新所有公共資源。
        /// </summary>
        public void EndRound(bool coreRevealed)
        {
            Mana.ResetSource();
            AdvActions.Refill(coreRevealed);
            Spells.DiscardUnchosen();
            Spells.Refill();
        }
    }
}
