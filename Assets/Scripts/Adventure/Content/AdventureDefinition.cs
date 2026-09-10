using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Adventure;
using MK.Logic.Runtime.Map;
using UnityEngine;

namespace MageKnight.Adventure.Content
{
    [Serializable]
    public sealed class AdventureSite
    {
        public string id;
        public string displayName;
        public int q, r;
        public TerrainType terrain;
        public bool revealed = true;
        public RecruitLocation recruitAt;
        public LocationDefinition location;
        public SiteSpec Snapshot() => new(id, displayName, new AxialCoord(q, r), terrain, revealed, recruitAt);
    }

    [CreateAssetMenu(menuName = "Mage Knight/冒险配置", fileName = "Adventure")]
    public sealed class AdventureDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string objective;
        public AdventureMode mode;
        public ActorDefinition hero;
        public LocationDefinition startingBackground;
        public EncounterDefinition encounter;
        public ActionCardDefinition[] deck;
        public ActorDefinition[] offers = Array.Empty<ActorDefinition>();
        public AdventureSite[] sites;
        public string startSite;
        public DayPart time;
        public int movement, influence, fame, reputation;
        public int redCrystals = 1;
        public int blueCrystals = 1, greenCrystals = 1, whiteCrystals = 1;

        public AdventureSpec Snapshot() => new(id, mode, hero.Snapshot(), deck.Select(c => c.Snapshot()).ToArray(),
            sites.Select(s => s.Snapshot()).ToArray(), startSite, offers.Select(a => a.Snapshot()).ToArray(),
            encounter == null ? null : encounter.Snapshot(), time, movement, influence, fame, reputation,
            redCrystals, blueCrystals, greenCrystals, whiteCrystals);
    }
}
