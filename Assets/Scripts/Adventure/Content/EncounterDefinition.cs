using System.Linq;
using MK.Logic.Runtime.Adventure;
using UnityEngine;

namespace MageKnight.Adventure.Content
{
    [CreateAssetMenu(menuName = "Mage Knight/遭遇组合", fileName = "Encounter")]
    public sealed class EncounterDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public LocationDefinition location;
        public ActorDefinition[] enemies;
        public EncounterSpec Snapshot() => new(id, enemies.Select(e => e.Snapshot()).ToArray());
    }
}
