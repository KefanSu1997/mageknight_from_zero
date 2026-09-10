using System;
using MK.Logic.Core;
using MK.Logic.Runtime.Adventure;
using UnityEngine;

namespace MageKnight.Adventure.Content
{
    [CreateAssetMenu(menuName = "Mage Knight/角色或怪物", fileName = "Actor")]
    public sealed class ActorDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public Sprite artwork;
        [Range(.3f, 1.6f)] public float visualScale = 1;
        [Min(1)] public int armor = 2;
        [Min(0)] public int attack;
        [Min(0)] public int block;
        [Min(0)] public int fame;
        public Element attackElement;
        public Ability[] abilities = Array.Empty<Ability>();
        [Min(0)] public int recruitmentCost;
        public RecruitLocation recruitAt = RecruitLocation.Village;

        public ActorSpec Snapshot() => new(id, displayName, armor, attack, block, fame,
            attackElement, (Ability[])abilities.Clone(), recruitmentCost, recruitAt);
    }
}
