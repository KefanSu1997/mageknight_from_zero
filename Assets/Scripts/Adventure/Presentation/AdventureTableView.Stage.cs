using System;
using System.Linq;
using MageKnight.Adventure.Content;
using MK.Logic.Runtime.Adventure;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Adventure.Presentation
{
    public sealed partial class AdventureTableView
    {
        private RectTransform _content;

        private void BuildStage(AdventureSession session)
        {
            if (_content != null) { _content.gameObject.SetActive(false); UnityEngine.Object.Destroy(_content.gameObject); }
            _content = Rect(_stage, "Targets", 0, 58, 1320, 442);
            _actors.Clear(); _sites.Clear(); _siteLabels.Clear();
            bool battle = session.Phase == AdventurePhase.Block || session.Phase == AdventurePhase.Attack || session.Phase == AdventurePhase.Result;
            Location = battle ? _definition.encounter.location : session.Phase == AdventurePhase.Interaction ?
                _definition.sites.First(s => s.id == session.CurrentSite.Id).location ?? _definition.startingBackground : _definition.startingBackground;
            _background.texture = Location.background.texture;
            float sourceAspect = (float)Location.background.texture.width / Location.background.texture.height;
            const float stageAspect = 1320f / 500f;
            _background.uvRect = sourceAspect > stageAspect ? new Rect((1 - stageAspect / sourceAspect) / 2, 0, stageAspect / sourceAspect, 1) :
                new Rect(0, (1 - sourceAspect / stageAspect) / 2, 1, sourceAspect / stageAspect);
            if (battle)
            {
                SpawnActor("Hero", _definition.hero, Location.heroAnchor, "护甲 " + session.Player.Armor, null);
                for (int i = 0; i < _definition.encounter.enemies.Length; i++)
                {
                    int index = i;
                    SpawnActor("Enemy_" + i, _definition.encounter.enemies[i], Location.enemyAnchors[i], "",
                        () => _command("target:enemy:" + index));
                }
            }
            else if (session.Phase == AdventurePhase.Interaction)
            {
                for (int i = 0; i < _definition.offers.Length; i++)
                {
                    var actor = _definition.offers[i];
                    SpawnActor("Offer_" + actor.id, actor, Location.offerAnchors[i], "影响 " + actor.recruitmentCost,
                        () => _command("target:offer:" + actor.id));
                }
            }
            else
            {
                Panel(_content, "MapShade", 0, 0, 1320, 442, new Color(0, .025f, .02f, .24f));
                foreach (var site in _definition.sites)
                {
                    float x = 496 + site.q * 210 + site.r * 105;
                    float y = 221 + site.r * 125;
                    var node = Rect(_content, "Site_" + site.id, x - 72, y - 74, 144, 148);
                    var border = node.gameObject.AddComponent<RuleHexGraphic>(); border.color = Gold;
                    var fillRect = Rect(node, "Fill", 2, 2, 140, 144);
                    var fill = fillRect.gameObject.AddComponent<RuleHexGraphic>(); fill.color = new Color32(18, 43, 37, 242); fill.raycastTarget = false;
                    var button = node.gameObject.AddComponent<Button>(); button.targetGraphic = fill;
                    string id = site.id; button.onClick.AddListener(() => _command("target:site:" + id));
                    _sites[id] = fill;
                    _siteLabels[id] = Text(node, "Label", "", 8, 22, 128, 105, 21, Cream, TextAlignmentOptions.Center);
                }
            }
        }

        private void SpawnActor(string key, ActorDefinition actor, Vector2 anchor, string stats, Action select)
        {
            var view = UnityEngine.Object.Instantiate(_actorPrefab, _content);
            view.name = key;
            var rect = (RectTransform)view.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(anchor.x * 1320, anchor.y * 442);
            rect.sizeDelta = new Vector2(330, 455);
            var portrait = view.portrait.rectTransform;
            portrait.anchorMin = portrait.anchorMax = portrait.pivot = new Vector2(.5f, 0);
            portrait.anchoredPosition = new Vector2(0, 57);
            float aspect = actor.artwork.rect.width / actor.artwork.rect.height;
            float width = Mathf.Min(290, 330 * aspect) * actor.visualScale;
            portrait.sizeDelta = new Vector2(width, width / aspect);
            view.Bind(actor, stats, select); _actors[key] = view;
        }

        private void UpdateStage(AdventureSession session)
        {
            if (session.Battle != null && session.Phase != AdventurePhase.Interaction && session.Phase != AdventurePhase.Travel)
            {
                for (int i = 0; i < session.Battle.Definition.Enemies.Length; i++)
                {
                    var enemy = session.Battle.Definition.Enemies[i];
                    _actors["Enemy_" + i].SetState(session.Target == "enemy:" + i, session.Battle.Defeated[i],
                        session.Battle.Defeated[i] ? "已击败" : "护甲 " + enemy.Armor + "   攻击 " + enemy.Attack + "   名望 " + enemy.Fame);
                }
            }
            foreach (var offer in _definition.offers)
                if (_actors.TryGetValue("Offer_" + offer.id, out var view))
                    view.SetState(session.Target == "offer:" + offer.id, session.HasHired(offer.id),
                        session.HasHired(offer.id) ? "已加入队伍" : "招募 · 影响 " + offer.recruitmentCost);
            foreach (var site in session.Definition.Sites)
                if (_sites.TryGetValue(site.Id, out var image))
                {
                    bool current = site.Id == session.CurrentSite.Id, selected = session.Target == "site:" + site.Id;
                    image.color = selected ? new Color32(104, 88, 48, 255) : new Color32(14, 39, 34, 242);
                    int cost = session.Cost(site);
                    _siteLabels[site.Id].text = (current ? "◆ " : "") + (session.IsRevealed(site) ? site.Name : "迷雾") + "\n" +
                        (current ? "当前位置" : cost == int.MaxValue ? "不可通行" : (session.IsRevealed(site) ? "进入 " : "探索 ") + cost);
                }
        }

        private void BuildUnits(AdventureSession session)
        {
            Clear(_unitRail);
            for (int i = 0; i < session.Player.Units.Count; i++)
            {
                var unit = session.Player.Units[i];
                var actor = _definition.offers.First(a => a.id == unit.Card.Id);
                var b = Button(_unitRail, "Unit_" + actor.id, "", i * 222, 0, 214, 44);
                Sprite(b.transform, "Portrait", actor.artwork, 4, 0, 43, 44);
                Text(b.transform, "UnitName", actor.displayName + (unit.IsReady ? " · 就绪" : " · 已用"), 46, 8, 162, 32, 17, Muted);
                b.onClick.AddListener(() => _command("unit:" + actor.id));
            }
        }
    }
}
