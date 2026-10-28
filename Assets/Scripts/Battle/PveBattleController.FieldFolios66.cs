using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private Sprite _statusFolioSprite66;

        // Authored UI chrome region only. Character/card artwork is untouched.
        private Image StatusFolio66(Transform parent, string name, float x, float y, float width, float height, int sibling = 0)
        {
            var existing = parent.Find(name)?.GetComponent<Image>();
            if (existing != null) return existing;
            var art = Resources.Load<Texture2D>("UI/CombatChrome/illustrated_v1/status_folio");
            if (art == null) return null;
            if (_statusFolioSprite66 == null || _statusFolioSprite66.texture != art)
            {
                float sx = art.width / 2172f, sy = art.height / 724f;
                _statusFolioSprite66 = Sprite.Create(art, new Rect(13 * sx, 208 * sy, 2146 * sx, 311 * sy),
                    new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect,
                    new Vector4(220 * sx, 26 * sy, 220 * sx, 26 * sy));
                _statusFolioSprite66.name = "StatusFolioAuthoredRegion66";
                _statusFolioSprite66.hideFlags = HideFlags.DontSave;
            }
            var image = W.Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = _statusFolioSprite66; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 12 * art.width / 2172f;
            image.raycastTarget = false; image.transform.SetSiblingIndex(sibling);
            return image;
        }

        private static void MarkReadonlyFolio66(Transform owner)
        {
            if (owner != null && owner.GetComponent<BattleReadonlyFolio66>() == null)
                owner.gameObject.AddComponent<BattleReadonlyFolio66>();
        }

        private void InstallReadonlyFolios66()
        {
            var title = _root.Find("Title");
            StatusFolio66(_root, "HeroHeaderFolio66", 24, 12, 474, 104, title != null ? title.GetSiblingIndex() : 1);
            if (title != null) title.GetComponent<TMP_Text>().color = W.Cream;
            foreach (string name in new[] { "FieldFact_HeroArmor", "FieldFact_HeroWounds", "FieldFact_HeroFame" })
                MarkReadonlyFolio66(_root.Find(name));
            var pools = _root.Cast<Transform>().Where(t => t.name.StartsWith("FieldFact_Pool_", StringComparison.Ordinal)).ToArray();
            if (pools.Length > 0)
            {
                // Keep the complete four-ally roster clear of the shared pool.
                foreach (var p in pools) ((RectTransform)p).anchoredPosition += new Vector2(100, 0);
                float left = pools.Min(p => ((RectTransform)p).anchoredPosition.x);
                StatusFolio66(_root, "CombatPoolFolio66", left - 8, 699, pools.Length * 98 + 16, 42, pools.Min(p => p.GetSiblingIndex()));
                foreach (var p in pools) MarkReadonlyFolio66(p);
            }
            var phase = _root.Find("PhaseTrack");
            if (phase != null)
            {
                StatusFolio66(phase, "PhaseStateFolio66", 0, -2, 470, 50);
                _phaseLabel.color = W.Cream;
            }
            for (int i = 0; i < _battle.Enemies.Count; i++)
            {
                var enemy = _root.Find("Enemy_" + i) as RectTransform;
                if (enemy == null) continue;
                var attacks = enemy.Cast<Transform>().Where(t => t.name.StartsWith("FieldFact_Attack_", StringComparison.Ordinal)).ToArray();
                var identity = new[] { enemy.Find("Name"), enemy.Find("State") }
                    .Where(t => t != null && t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.GetComponent<TMP_Text>()?.text)).ToArray();
                if (attacks.Length > 0)
                {
                    var name = enemy.Find("Name") as RectTransform;
                    var state = enemy.Find("State") as RectTransform;
                    bool hasState = state != null && !string.IsNullOrWhiteSpace(state.GetComponent<TMP_Text>()?.text);
                    float nameTop = name != null ? -name.anchoredPosition.y : 46;
                    float nameWidth = name != null ? name.GetComponent<TMP_Text>().GetPreferredValues(name.GetComponent<TMP_Text>().text).x : float.PositiveInfinity;
                    if (attacks.Length == 1 && nameWidth + 112 <= enemy.rect.width)
                    {
                        float rowTop = nameTop - (hasState ? 34 : 0);
                        Place(name, 12, rowTop, enemy.rect.width - 112, 40);
                        Place(attacks[0], enemy.rect.width - 92, rowTop - 1, 84, 44);
                        var value = attacks[0].Find("Value") as RectTransform;
                        if (value != null) value.sizeDelta = new Vector2(45, 44);
                        if (hasState) Place(state, 12, nameTop + 8, enemy.rect.width - 24, 30);
                    }
                    float top = attacks.Min(t => -((RectTransform)t).anchoredPosition.y);
                    float bottom = identity.Length == 0 ? top + 44 : identity.Max(t => -((RectTransform)t).anchoredPosition.y + ((RectTransform)t).rect.height);
                    StatusFolio66(enemy, "EnemyIdentityFolio66", -4, top - 4, enemy.rect.width + 8, bottom - top + 8);
                }
                float footWidth = Mathf.Min(220, enemy.rect.width);
                StatusFolio66(enemy, "EnemyFactsFolio66", (enemy.rect.width - footWidth) / 2, 421, footWidth, 80);
                foreach (var t in enemy.Cast<Transform>().Where(t => t.name.StartsWith("FieldFact_", StringComparison.Ordinal) || t.name.StartsWith("Trait_", StringComparison.Ordinal)))
                    MarkReadonlyFolio66(t);
                foreach (var t in identity) t.GetComponent<TMP_Text>().color = W.Cream;
            }
            var heroName = _root.Find("HeroName") as RectTransform;
            if (heroName != null)
            {
                StatusFolio66(_root, "HeroNameFolio66", heroName.anchoredPosition.x - 4, -heroName.anchoredPosition.y, heroName.rect.width + 8, 40, heroName.GetSiblingIndex());
                heroName.GetComponent<TMP_Text>().color = W.Cream;
            }
            foreach (var actor in _root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Btn_UnitActor_", StringComparison.Ordinal)))
            {
                var identity = actor.transform.Find("Identity")?.GetComponent<TMP_Text>();
                bool twoLines = identity != null && identity.text.Contains("\n");
                if (!twoLines)
                {
                    if (identity != null) Place(identity.transform, 0, 222, 145, 40);
                    Place(actor.transform.Find("ReadinessSymbol"), 23, 264, 30, 30);
                    Place(actor.transform.Find("WoundSymbol"), 72, 264, 28, 30);
                    Place(actor.transform.Find("WoundCount"), 102, 259, 35, 40);
                }
                StatusFolio66(actor.transform, "UnitIdentityFolio66", -4, 219, 153, twoLines ? 100 : 80);
                MarkReadonlyFolio66(actor.transform);
            }
            foreach (var fact in _root.GetComponentsInChildren<RectTransform>().Where(t => t.name.StartsWith("Unit", StringComparison.Ordinal) && t.name.EndsWith("Fact", StringComparison.Ordinal)))
            {
                StatusFolio66(fact.parent, "UnitStateFolio66", 4, -fact.anchoredPosition.y - 2, 344, 84);
                MarkReadonlyFolio66(fact);
            }
            foreach (var group in _root.GetComponentsInChildren<RectTransform>()
                .Where(t => t.name.StartsWith("FieldFact_Decision_", StringComparison.Ordinal)).GroupBy(t => t.parent))
            {
                float left = group.Min(t => t.anchoredPosition.x), top = group.Min(t => -t.anchoredPosition.y);
                float right = group.Max(t => t.anchoredPosition.x + t.rect.width), bottom = group.Max(t => -t.anchoredPosition.y + t.rect.height);
                StatusFolio66(group.Key, "DecisionFactsFolio66", left - 4, top - 3, right - left + 8, bottom - top + 6);
                foreach (var fact in group) MarkReadonlyFolio66(fact);
            }
        }
    }

    // A role marker, not a graphical panel or another click target.
    public sealed class BattleReadonlyFolio66 : MonoBehaviour { }
}
