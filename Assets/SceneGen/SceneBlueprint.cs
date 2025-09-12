// Assets/SceneGen/SceneBlueprint.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SceneGen/Scene Blueprint", fileName = "SceneBlueprint")]
public class SceneBlueprint : ScriptableObject
{
    [Serializable]
    public class Node
    {
        public string name = "Node";
        [Tooltip("可留空。填 GUID 时优先实例化对应 Prefab。")]
        public string prefabGuid;

        public string parentName; // 可留空
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;

        public List<EventBinding> events = new();
    }

    [Serializable]
    public class EventBinding
    {
        [Tooltip("组件类型名，如 UnityEngine.UI.Button")]
        public string componentType;
        [Tooltip("事件名（目前支持 onClick）")]
        public string eventName = "onClick";
        public List<Call> calls = new(); // 这组调用会依次加入监听
    }

    [Serializable]
    public class Call
    {
        [Tooltip("目标节点名（场景中由蓝图生成的 GameObject）")]
        public string targetNodeName;
        [Tooltip("目标组件类型名，如 GameController")]
        public string targetComponentType;
        [Tooltip("public 无参方法名，如 OnButtonClicked")]
        public string methodName;
    }

    public List<Node> nodes = new();
}
