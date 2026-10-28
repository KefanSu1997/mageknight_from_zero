using System;
using System.Linq;
using MK.Logic.Data;
using UnityEngine;

namespace MageKnight.Battle
{
    [Serializable]
    public sealed class OriginalEnemyView
    {
        public string originalId;
        public string displayName;
        public Texture2D originalToken;
    }

    [CreateAssetMenu(menuName = "MageKnight/Battle/Encounter")]
    public sealed class BattleEncounter : ScriptableObject
    {
        public string displayName;
        public TextAsset originalMonsterJson;
        public OriginalEnemyView[] enemies;
        public Texture2D background;
        public MK.Logic.Core.DayPart dayPart = MK.Logic.Core.DayPart.Day;

        public Monster[] LoadEnemies()
        {
            if (originalMonsterJson == null || enemies == null || enemies.Length == 0 || enemies.Length > 3)
                throw new InvalidOperationException("遭遇须绑定原怪物数据和1至3名原敌人");
            // Formal loader also used by game rules and backend tests; package a TextAsset for standalone use.
            var originals = MonsterJsonLoader.ParseMonsters(originalMonsterJson.text);
            return enemies.Select(binding =>
            {
                if (binding.originalToken == null) throw new InvalidOperationException("原敌人标记缺失：" + binding.originalId);
                var original = originals.Single(m => m.Id == binding.originalId);
                return original with { }; // Each spawn owns a distinct original enemy instance.
            }).ToArray();
        }
    }
}
