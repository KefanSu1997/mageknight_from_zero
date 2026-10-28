using System;
using MK.Logic.Data;
using UnityEngine;

namespace MageKnight.Battle
{
    [CreateAssetMenu(menuName = "MageKnight/Battle/Original Unit")]
    public sealed class BattleUnitDefinition : ScriptableObject
    {
        public string originalId;
        public TextAsset originalJson;
        public Sprite artwork;

        public UnitCard Snapshot()
        {
            if (originalJson == null || artwork == null || artwork.name != originalId)
                throw new InvalidOperationException("部队必须绑定同源原定义和原卡面：" + originalId);
            return UnitJsonLoader.Parse(originalJson.text, originalId);
        }
    }
}
