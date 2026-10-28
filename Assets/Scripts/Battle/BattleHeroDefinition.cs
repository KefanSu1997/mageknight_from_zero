using System;
using System.Linq;
using MK.Logic.Data;
using UnityEngine;

namespace MageKnight.Battle
{
    [CreateAssetMenu(menuName = "MageKnight/Battle/Hero")]
    public sealed class BattleHeroDefinition : ScriptableObject
    {
        public string heroId, displayName, skillPoolId;
        public TextAsset originalSkillsJson;
        public SkillArtwork[] skillArtwork = Array.Empty<SkillArtwork>();

        public SkillCard LearnedSkill(int index)
        {
            if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(displayName) || originalSkillsJson == null)
                throw new InvalidOperationException("缺少原英雄与技能定义");
            var pool = CardJsonLoader.ParseHeroSkills(originalSkillsJson.text, skillPoolId);
            if (index < 0 || index >= pool.Count) throw new InvalidOperationException("技能不在英雄原技能池中");
            var art = skillArtwork.SingleOrDefault(a => a.skillIndex == index);
            if (art == null || art.originalSheet == null || art.uvRect.width <= 0 || art.uvRect.height <= 0)
                throw new InvalidOperationException("已学技能缺少对应原素材");
            return pool[index];
        }

        [Serializable]
        public sealed class SkillArtwork
        {
            public int skillIndex;
            public Texture2D originalSheet;
            [Tooltip("原技能参考页上的对应区域，只裁视图，不重绘原素材。")]
            public Rect uvRect = new Rect(0, 0, 1, 1);
        }
    }
}
