using System.Linq;
using MK.Logic.Data;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        // Reuse the original hero-specific card illustration; never substitute another hero.
        private void DrawHeroPortrait()
        {
            var hero = OriginalHeroes.All.SingleOrDefault(h => h.Id == _battle.Player.HeroId);
            if (hero == null)
            {
                if (Loadout.heroArtwork != null) W.Sprite(_root, "Hero", Loadout.heroArtwork, 58, 226, 112, 112);
                return;
            }
            string id = hero.Id switch
            {
                "Arythea" => "basic_card_013", "Goldyx" => "basic_card_019",
                "Krang" => "basic_card_026", _ => hero.UniqueCards[0]
            };
            if (!hero.UniqueCards.Contains(id)) throw new System.InvalidOperationException("英雄头像必须来自该英雄原专属卡");
            var art = deedCatalog.Single(d => d.id == id).artwork;
            // The illustration is the upper half of the existing full-card sprite.
            // Render its UV region without duplicating or changing the original asset.
            var rect = art.rect;
            float height = rect.height * .5f;
            var panel = W.Panel(_root, "HeroPortraitFrame", 58, 226, 112, 112, W.Ink, true);
            var image = W.Rect(_root, "Hero", 62, 230, 104, 104 * height / rect.width).gameObject.AddComponent<RawImage>();
            image.texture = art.texture;
            image.uvRect = new Rect(rect.x / art.texture.width, (rect.y + rect.height - height) / art.texture.height,
                rect.width / art.texture.width, height / art.texture.height);
            image.raycastTarget = false;
        }
    }
}
