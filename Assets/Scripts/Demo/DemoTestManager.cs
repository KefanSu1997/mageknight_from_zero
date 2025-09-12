using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

namespace MK.Demo
{
    public class DemoTestManager : MonoBehaviour
    {
        [Header("Scene Navigation")]
        public Button combatDemoButton;
        public Button mapDemoButton;
        public Button cardEffectsDemoButton;
        public Button integrationDemoButton;
        
        [Header("Debug Panel")]
        public GameObject debugPanel;
        public Text debugLogText;
        public Button toggleDebugButton;
        public Button clearLogButton;
        
        [Header("Performance Monitor")]
        public Text fpsText;
        public Text memoryText;
        
        private static DemoTestManager _instance;
        private List<string> _debugLog = new List<string>();
        private float _deltaTime;
        
        public static DemoTestManager Instance => _instance;
        
        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void Start()
        {
            SetupUI();
            Log("Demo测试管理器已启动");
        }
        
        void Update()
        {
            UpdatePerformanceMonitor();
        }
        
        void SetupUI()
        {
            // 设置场景导航按钮
            if (combatDemoButton != null)
                combatDemoButton.onClick.AddListener(() => LoadDemoScene("CombatDemo"));
                
            if (mapDemoButton != null)
                mapDemoButton.onClick.AddListener(() => LoadDemoScene("MapDemo"));
                
            if (cardEffectsDemoButton != null)
                cardEffectsDemoButton.onClick.AddListener(() => LoadDemoScene("CardEffectsDemo"));
                
            if (integrationDemoButton != null)
                integrationDemoButton.onClick.AddListener(() => LoadDemoScene("IntegrationDemo"));
            
            // 设置调试面板
            if (toggleDebugButton != null)
                toggleDebugButton.onClick.AddListener(ToggleDebugPanel);
                
            if (clearLogButton != null)
                clearLogButton.onClick.AddListener(ClearDebugLog);
                
            if (debugPanel != null)
                debugPanel.SetActive(false);
        }
        
        void LoadDemoScene(string sceneName)
        {
            Log($"加载演示场景: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
        
        void ToggleDebugPanel()
        {
            if (debugPanel != null)
            {
                debugPanel.SetActive(!debugPanel.activeSelf);
                Log($"调试面板已{(debugPanel.activeSelf ? "开启" : "关闭")}");
            }
        }
        
        void ClearDebugLog()
        {
            _debugLog.Clear();
            UpdateDebugLog();
        }
        
        void UpdatePerformanceMonitor()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            float fps = 1.0f / _deltaTime;
            
            if (fpsText != null)
                fpsText.text = $"FPS: {Mathf.CeilToInt(fps)}";
                
            if (memoryText != null)
            {
                long memory = System.GC.GetTotalMemory(false);
                memoryText.text = $"内存: {(memory / 1024 / 1024)}MB";
            }
        }
        
        public void Log(string message)
        {
            string logEntry = $"[{System.DateTime.Now:HH:mm:ss}] {message}";
            _debugLog.Add(logEntry);
            
            // 限制日志数量
            if (_debugLog.Count > 100)
                _debugLog.RemoveAt(0);
                
            UpdateDebugLog();
            Debug.Log(message);
        }
        
        void UpdateDebugLog()
        {
            if (debugLogText != null)
            {
                var recentLogs = _debugLog.Count > 20 ? _debugLog.Skip(_debugLog.Count - 20).ToList() : _debugLog;
                debugLogText.text = string.Join("\n", recentLogs);
            }
        }
        
        public void LogWarning(string message)
        {
            Log($"[警告] {message}");
            Debug.LogWarning(message);
        }
        
        public void LogError(string message)
        {
            Log($"[错误] {message}");
            Debug.LogError(message);
        }
        
        public void ResetAllDemos()
        {
            // 重置所有演示场景
            var combatDemo = FindFirstObjectByType<CombatDemoController>();
            if (combatDemo != null)
            {
                combatDemo.SendMessage("ResetCombat");
            }
            
            var mapDemo = FindFirstObjectByType<MapDemoController>();
            if (mapDemo != null)
            {
                mapDemo.SendMessage("ResetMap");
            }
            
            var cardDemo = FindFirstObjectByType<CardEffectsDemoController>();
            if (cardDemo != null)
            {
                cardDemo.SendMessage("ResetDemo");
            }
            
            Log("所有演示已重置");
        }
    }
}