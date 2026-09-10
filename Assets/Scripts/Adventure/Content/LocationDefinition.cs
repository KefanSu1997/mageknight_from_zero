using UnityEngine;

namespace MageKnight.Adventure.Content
{
    [CreateAssetMenu(menuName = "Mage Knight/地点背景", fileName = "Location")]
    public sealed class LocationDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public Sprite background;
        [Tooltip("舞台中的归一化落脚点，(0,0)为左下。")]
        public Vector2 heroAnchor = new(.22f, .06f);
        public Vector2[] enemyAnchors = { new(.73f, .06f), new(.88f, .08f) };
        public Vector2[] offerAnchors = { new(.25f, .08f), new(.53f, .08f), new(.81f, .08f) };
    }
}
