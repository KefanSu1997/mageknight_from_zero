using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Rulebook pp15/19. Reads actual ownership; never writes scoring points into fame.</summary>
    public sealed record SoloConquestScore(SoloInventoryScore Inventory, int FortifiedSites,
        int AdventureSites, int Cities, int EarlyRounds, int DummyCards, int NoAnnouncement,
        bool Terminal)
    {
        public int Total => Inventory.Total + 2*FortifiedSites + 2*AdventureSites + 10*Cities
            + (Cities==2?15:0) + EarlyRounds + DummyCards + NoAnnouncement;

        internal static SoloConquestScore Capture(PlayerState player, Map.MapState map,
            ISet<string> advancedIds, bool terminal, int round, int dummyCards, bool roundAnnounced)
        {
            if(map==null || round<1 || round>6 || dummyCards<0)throw new ArgumentException("Invalid scenario score state.");
            var sites=map.Places.Values.ToArray();
            if(sites.Where((s,i)=>sites.Take(i).Any(x=>ReferenceEquals(x,s))).Any())
                throw new InvalidOperationException("A physical site cannot occupy two scoring positions.");
            bool Owned(PlaceState s)=>s.OwnerId==player.Id && s.Conquered;
            int fortified=sites.Count(s=>Owned(s) && s.Type is PlaceType.Keep or PlaceType.MageTower or PlaceType.Monastery);
            int adventure=sites.Count(s=>Owned(s) && s.Type is PlaceType.AncientRuin or PlaceType.Dungeon
                or PlaceType.Tomb or PlaceType.MonsterDen or PlaceType.SpawningGround);
            int cities=sites.Count(s=>Owned(s) && s.Type==PlaceType.City && s.CityShields.Contains(player.Id));
            if(cities>2 || terminal && round<6 && cities!=2)
                throw new InvalidOperationException("Not an official completed solo conquest state.");
            return new SoloConquestScore(SoloInventoryScore.Capture(player,advancedIds),fortified,adventure,cities,
                terminal?30*(6-round):0,terminal?dummyCards:0,terminal&&!roundAnnounced?5:0,terminal);
        }
    }

    /// <summary>Native telescoping reward, gamma=1. Snapshot this ledger with the actual environment.</summary>
    public sealed class SoloScoreRewardLedger
    {
        public int InitialScore { get; }
        public int PreviousScore { get; private set; }
        public bool Closed { get; private set; }
        public double Scale { get; }
        public SoloScoreRewardLedger(SoloConquestScore initial, double scale=0.01)
        {
            if(initial==null || initial.Terminal || double.IsNaN(scale) || double.IsInfinity(scale) || scale<=0)
                throw new ArgumentException("Need live initial score and finite positive scale.");
            InitialScore=PreviousScore=initial.Total;Scale=scale;
        }
        public double Advance(SoloConquestScore current)
        {
            if(Closed || current==null)throw new InvalidOperationException("Reward ledger is closed or score absent.");
            double delta=Scale*(current.Total-PreviousScore);
            PreviousScore=current.Total;Closed=current.Terminal;
            return delta;
        }
    }

    /// <summary>
    /// Native inventory contribution to solo scoring (rulebook p15).
    /// This is reward/accounting data, not an observation of the hidden draw order.
    /// Site shields, cities and time bonuses are scored separately by the scenario.
    /// </summary>
    public sealed class SoloInventoryScore
    {
        private SoloInventoryScore() { }
        [Newtonsoft.Json.JsonConstructor]
        public SoloInventoryScore(int fame,int spells,int advancedActions,int artifacts,int crystals,
            int unitPoints,int deckWounds)
        {
            Fame=fame;Spells=spells;AdvancedActions=advancedActions;Artifacts=artifacts;
            Crystals=crystals;UnitPoints=unitPoints;DeckWounds=deckWounds;
        }
        public int Fame { get; private set; }
        public int Spells { get; private set; }
        public int AdvancedActions { get; private set; }
        public int Artifacts { get; private set; }
        public int Crystals { get; private set; }
        public int UnitPoints { get; private set; }
        public int DeckWounds { get; private set; }
        public int Total => Fame + 2 * Spells + AdvancedActions + 2 * Artifacts
            + Crystals / 2 + UnitPoints - 2 * DeckWounds;

        public static SoloInventoryScore Capture(PlayerState player, ISet<string> advancedActionIds)
        {
            if (player == null || advancedActionIds == null) throw new ArgumentNullException();
            var deck = player.Deck;
            var cards = deck.DrawPile.Concat(deck.Hand).Concat(deck.DiscardPile)
                .Concat(deck.PlayedCards).Concat(deck.RoundAsideCards).Concat(deck.TacticAsideCards)
                .Concat(player.Units.Where(u => !u.IsDestroyed && u.Banner != null).Select(u => u.Banner)).ToArray();
            // Do not deduplicate by record equality: two physical cards with identical IDs both count.
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null || cards.Take(i).Any(c => ReferenceEquals(c, cards[i])))
                    throw new InvalidOperationException("Physical card occurs in multiple owned zones.");
                if (deck.RemovedCards.Any(c => ReferenceEquals(c, cards[i])))
                    throw new InvalidOperationException("Removed physical card remains in an owned zone.");
            }
            if (player.Mana.Crystals.Any(kv => kv.Value < 0 ||
                (kv.Key == ManaColor.Black || kv.Key == ManaColor.Gold) && kv.Value != 0))
                throw new InvalidOperationException("Invalid crystal inventory for official scoring.");
            return new SoloInventoryScore
            {
                Fame = player.Fame,
                Spells = cards.Count(c => c.Type == CardType.Spell),
                AdvancedActions = cards.Count(c => c.Type == CardType.Action && advancedActionIds.Contains(c.Id)),
                Artifacts = cards.Count(c => c.Type == CardType.Artifact),
                Crystals = player.Mana.Crystals.Values.Sum(),
                UnitPoints = player.Units.Where(u => !u.IsDestroyed).Sum(u => u.Wounds > 0 ? u.Card.Level / 2 : u.Card.Level),
                DeckWounds = cards.Count(c => c.Type == CardType.Wound)
            };
        }
    }
}
