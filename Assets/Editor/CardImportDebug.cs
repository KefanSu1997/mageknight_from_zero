using UnityEditor;
using UnityEngine;
using System.IO;
using MK.Logic.Data;

/// <summary>
/// 调试卡牌导入过程的工具，用于检测导入失败原因
/// </summary>
public class CardImportDebug : EditorWindow
{
    private string debugLog = "";

    [MenuItem("Tools/MageKnight/Card Import Debug")]
    public static void ShowWindow()
    {
        GetWindow<CardImportDebug>("Card Import Debug");
    }

    private void OnGUI()
    {
        GUILayout.Label("卡牌导入调试工具", EditorStyles.boldLabel);
        
        if (GUILayout.Button("调试JSON加载"))
        {
            DebugJsonLoading();
        }
        
        if (GUILayout.Button("检查文件路径"))
        {
            CheckFilePaths();
        }
        
        if (GUILayout.Button("测试卡牌解析"))
        {
            TestCardParsing();
        }
        
        GUILayout.Space(20);
        GUILayout.Label("调试日志:", EditorStyles.boldLabel);
        GUILayout.TextArea(debugLog, GUILayout.Height(300));
    }

    private void DebugJsonLoading()
    {
        debugLog = "=== JSON加载测试 ===\n";
        
        try
        {
            string testPath = Path.Combine(Application.dataPath, "../resources/text_json");
            debugLog += $"当前路径: {testPath}\n";
            debugLog += $"路径是否存在: {Directory.Exists(testPath)}\n";
            
            CardJsonLoader.SetBaseDirectory(testPath);
            debugLog += $"基础路径设置完成\n";
            
            if (Directory.Exists(testPath))
            {
                string[] files = Directory.GetFiles(testPath, "*.json");
                debugLog += $"找到JSON文件: {files.Length}个\n";
                foreach (var file in files)
                {
                    debugLog += $"  - {Path.GetFileName(file)}\n";
                }
            }
        }
        catch (System.Exception ex)
        {
            debugLog += $"错误: {ex.Message}\n{ex.StackTrace}\n";
        }
    }

    private void CheckFilePaths()
    {
        debugLog = "=== 文件路径检查 ===\n";
        
        string[] requiredFiles = {
            "basic_card.json",
            "advanced_card.json", 
            "items.json",
            "magic.json"
        };
        
        string basePath = Path.Combine(Application.dataPath, "../resources/text_json");
        debugLog += $"基础路径: {basePath}\n";
        debugLog += $"路径存在: {Directory.Exists(basePath)}\n";
        
        foreach (string fileName in requiredFiles)
        {
            string fullPath = Path.Combine(basePath, fileName);
            bool exists = File.Exists(fullPath);
            debugLog += $"{fileName}: {(exists ? "存在" : "不存在")} - {fullPath}\n";
        }
    }

    private void TestCardParsing()
    {
        debugLog = "=== 卡牌解析测试 ===\n";
        
        try
        {
            string testPath = Path.Combine(Application.dataPath, "../resources/text_json");
            CardJsonLoader.SetBaseDirectory(testPath);
            
            debugLog += "尝试读取基础行动牌...\n";
            var basicCards = CardJsonLoader.LoadBasicActions();
            debugLog += $"基础行动牌数量: {basicCards.Count}\n";
            
            if (basicCards.Count > 0)
            {
                debugLog += "第一张卡牌:\n";
                var first = basicCards[0];
                debugLog += $"  ID: {first.Id}\n";
                debugLog += $"  名称: {first.Name}\n";
                debugLog += $"  基础效果: {first.BaseEffect}\n";
            }
            
            debugLog += "尝试读取高级行动牌...\n";
            var advancedCards = CardJsonLoader.LoadAdvancedActions();
            debugLog += $"高级行动牌数量: {advancedCards.Count}\n";
            
            debugLog += "尝试读取物品牌...\n";
            var items = CardJsonLoader.LoadItems();
            debugLog += $"物品牌数量: {items.Count}\n";
            
            debugLog += "尝试读取法术牌...\n";
            var spells = CardJsonLoader.LoadSpells();
            debugLog += $"法术牌数量: {spells.Count}\n";
            
        }
        catch (System.Exception ex)
        {
            debugLog += $"解析错误: {ex.Message}\n{ex.StackTrace}\n";
        }
    }
}