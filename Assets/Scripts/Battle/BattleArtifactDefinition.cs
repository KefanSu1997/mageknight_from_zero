using System;
using MK.Logic.Data.Cards;
using UnityEngine;

namespace MageKnight.Battle
{
    [CreateAssetMenu(menuName = "MageKnight/Battle/Original Artifact")]
    public sealed class BattleArtifactDefinition : ScriptableObject
    {
        public ItemCardSO source;
        public Sprite artwork;
        public ItemCardData Snapshot()
        {
            if (source == null || artwork == null || artwork.name != source.Id)
                throw new InvalidOperationException("神器必须绑定同源原定义与原卡面");
            return new ItemCardData(source.Id, source.NameCn, source.Set, source.ImagePath, source.EnImagePath,
                source.ItemType, source.AssignEffect, source.OncePerRound);
        }
    }
}
