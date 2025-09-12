using UnityEditor;
using UnityEngine;
using System.IO;
using MK.Logic.Data;

/// <summary>
/// 用于自动测试卡牌导入功能的快速测试类
/// </summary>
public class QuickImportTest : EditorWindow
{
    private Vector2 scrollPosition = Vector2.zero;
    
    [MenuItem("Tools/MageKnight/Quick Import Test")]
    public static void ShowWindow()
    {
        GetWindow<QuickImportTest>("Quick Import Test");
    }

    private void OnGUI()
    {
        GUILayout.Label("快速卡牌导入测试", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        GUILayout.Label("点击按钮执行快速测试:", EditorStyles.label);
        
        if (GUILayout.Button("测试JSON文件读取", GUILayout.Height(30)))
        {
            TestJsonReading();
        }
        
        if (GUILayout.Button("运行完整导入流程", GUILayout.Height(30)))
        {
            RunFullImport();
        }
        
        GUILayout.Space(20);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.Label(logText, EditorStyles.wordWrappedLabel);
        GUILayout.EndScrollView();
    }
    
    private static string logText = "";
    
    private void TestJsonReading()
    {
        logText = "=== 测试JSON文件读取 ===\n";
        
        string baseDir = Path.Combine(Application.dataPath, "../resources/text_json");
        logText += $"基础目录: {baseDir}\n";
        logText += $"目录存在: {Directory.Exists(baseDir)}\n";
        
        if (Directory.Exists(baseDir))
        {
            string[] jsonFiles = Directory.GetFiles(baseDir, "*.json");
            logText += $"找到JSON文件: {jsonFiles.Length}\n";
            
            foreach (string file in jsonFiles)
            {
                string fileName = Path.GetFileName(file);
                logText += $"\n处理文件: {fileName}\n";
                
                try
                {
                    CardJsonLoader.SetBaseDirectory(baseDir);
                    logText += $"测试文件: {fileName}\n";
                    
                    switch (fileName.ToLower())
                    {
                        case "basic_card.json":
                            var basics = CardJsonLoader.LoadBasicActions();
                            logText += $"基础行动牌: {basics.Count} 张\n";
                            break;
                        case "advanced_card.json":
                            var advanceds = CardJsonLoader.LoadAdvancedActions();
                            logText += $"高级行动牌: {advanceds.Count} 张\n";
                            break;
                        case "items.json":
                            var items = CardJsonLoader.LoadItems();
                            logText += $"物品牌: {items.Count} 张\n";
                            break;
                        case "magic.json":
                            var magics = CardJsonLoader.LoadSpells();
                            logText += $"法术牌: {magics.Count} 张\n";
                            break;
                    }
                }
                catch (System.Exception ex)
                {
                    logText += $"错误处理 {fileName}: {ex.Message}\n";
                }
            }
        }
        else
        {
            logText += "目录不存在，请检查resources/text_json文件夹\n";
        }
    }
    
    private void RunFullImport()
    {
        logText = "=== 运行完整导入流程 ===\n";
        
        try
        {
            logText += "开始直接调用CardImporter.ImportAll()...\n";
            CardImporter.ImportAll();
            logText += "导入完成！\n";
            
            EditorUtility.DisplayDialog("导入测试", "完整导入流程已完成！\n\n请查看控制台获取更多详细信息。", "确定");
        }
        catch (System.Exception ex)
        {
            logText += $"导入失败: {ex.Message}\n{ex.StackTrace}\n";
            EditorUtility.DisplayDialog("导入错误", $"导入失败:\n{ex.Message}", "确定");
        }
    }
}