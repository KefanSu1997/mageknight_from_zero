using System.Collections.Generic;
using MageKnight.SceneAutomation;
using System;
using MageKnight.Adventure.Content;
using MageKnight.Adventure.Presentation;
using MK.Logic.Runtime.Adventure;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum RuleScenarioKind { Combat, Exploration, Recruitment, Journey }

public sealed class RuleScenarioController : MonoBehaviour, ISceneAutomationStateSource
{
    [SerializeField] private RuleScenarioKind kind;
    [SerializeField] private AdventureDefinition adventure;
    private AdventureSession _session;
    private AdventureTableView _view;
    public RuleScenarioKind Kind { get => kind; set => kind = value; }
    public string LastRule => _session.Rule;

    private void Awake()
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        _view = new AdventureTableView(canvas, OnAction);
        string[] names = { "01_combat", "02_exploration", "03_recruitment", "04_journey" };
        Load(adventure != null ? adventure : Resources.Load<AdventureDefinition>("Adventure/Scenarios/" + names[(int)kind]));
    }

    private void OnAction(string action)
    {
        if (action == "reset" || action == "confirm" && _session.Phase == AdventurePhase.Result) { Load(adventure); return; }
        if (action.StartsWith("scenario:")) { Load(Resources.Load<AdventureDefinition>("Adventure/Scenarios/" + action.Substring(9))); return; }
        _session.Execute(action);
        _view.Refresh(adventure, _session);
    }

    private void Load(AdventureDefinition definition)
    {
        adventure = definition != null ? definition : throw new InvalidOperationException("冒险配置缺失，请执行Build Reusable Content菜单。");
        _session = new AdventureSession(adventure.Snapshot());
        _view.Refresh(adventure, _session);
    }

    public Dictionary<string, string> ReadAutomationState()
    {
        var values = _session.ReadState();
        foreach (var entry in _view.ReadDisplayedValues()) values["ui:" + entry.Key] = entry.Value;
        values["background"] = _view.Location.id;
        values["heroArt"] = adventure.hero.artwork.name;
        if (_session.Battle != null)
            for (int i = 0; i < adventure.encounter.enemies.Length; i++)
                values["enemyArt" + i] = adventure.encounter.enemies[i].artwork.name;
        return values;
    }
}
