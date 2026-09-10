using System;
using MageKnight.Adventure.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MageKnight.Adventure.Presentation
{
    /// <summary>可直接放入Prefab的独立角色视图，同一资产可用于战斗、招募及队伍栏。</summary>
    public sealed class AdventureActorView : MonoBehaviour
    {
        public Image portrait;
        public Image selection;
        public TextMeshProUGUI caption;
        public TextMeshProUGUI stats;
        public Button button;
        public ActorDefinition Definition { get; private set; }

        public void Bind(ActorDefinition definition, string summary, Action selected)
        {
            Definition = definition; portrait.sprite = definition.artwork;
            portrait.preserveAspect = true; caption.text = definition.displayName; stats.text = summary;
            button.onClick.RemoveAllListeners(); if (selected != null) button.onClick.AddListener(() => selected());
            button.interactable = selected != null;
        }
        public void SetState(bool selected, bool unavailable, string summary)
        {
            selection.color = selected ? new Color32(201, 166, 98, 160) : new Color(0, 0, 0, 0);
            portrait.color = unavailable ? new Color(.50f, .54f, .53f, .65f) : Color.white;
            stats.text = summary;
        }
    }
}
