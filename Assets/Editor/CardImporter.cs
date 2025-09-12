using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using MK.Logic.Data;
using MK.Logic.Data.Cards;

/// <summary>
/// 從 resources/text_json 解析卡牌資料並產生 ScriptableObject 檔案的編輯器工具。
/// ImagePath 中的反斜杠會被替換成正斜杠，以保證地址一致。
/// </summary>
public class CardImporter : EditorWindow
{
    [MenuItem("Tools/MageKnight/Card Importer")]
    public static void ShowWindow()
    {
        GetWindow<CardImporter>("Card Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("讀取 JSON 並生成卡牌資源", EditorStyles.boldLabel);
        if (GUILayout.Button("開始匯入"))
        {
            ImportAll();
        }
    }

    /// <summary>
    /// 依序載入 JSON 並寫入或更新 ScriptableObject 檔案。
    /// </summary>
    public static void ImportAll()
    {
        try
        {
            Debug.Log("開始卡牌匯入流程...");
            
            // 確保目標目錄存在
            Directory.CreateDirectory("Assets/GameData/CardsAssets");
            Debug.Log("目標目錄已確定: Assets/GameData/CardsAssets");
            
            // 設置JSON文件目錄
            string jsonPath = Path.Combine(Application.dataPath, "../resources/text_json");
            Debug.Log($"JSON文件目錄: {jsonPath}");
            
            // 檢查路徑是否存在
            if (!Directory.Exists(jsonPath))
            {
                Debug.LogError($"JSON目錄不存在: {jsonPath}");
                return;
            }
            
            CardJsonLoader.SetBaseDirectory(jsonPath);
            Debug.Log("基礎路徑設置完成");

            int count = 0;

            // 基礎行動牌
            try
            {
                var basicCards = CardJsonLoader.LoadBasicActions();
                Debug.Log($"基礎行動牌數量: {basicCards.Count}");
                foreach (var data in basicCards)
                {
                    SaveAsset(ActionCardSO.FromData(data), data.Id);
                    count++;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"載入基礎行動牌失敗: {ex.Message}\n{ex.StackTrace}");
            }

            // 高級行動牌
            try
            {
                var advancedCards = CardJsonLoader.LoadAdvancedActions();
                Debug.Log($"高級行動牌數量: {advancedCards.Count}");
                foreach (var data in advancedCards)
                {
                    SaveAsset(ActionCardSO.FromData(data), data.Id);
                    count++;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"載入高級行動牌失敗: {ex.Message}\n{ex.StackTrace}");
            }

            // 物品牌
            try
            {
                var itemCards = CardJsonLoader.LoadItems();
                Debug.Log($"物品牌數量: {itemCards.Count}");
                foreach (var data in itemCards)
                {
                    SaveAsset(ItemCardSO.FromData(data), data.Id);
                    count++;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"載入物品牌失敗: {ex.Message}\n{ex.StackTrace}");
            }

            // 法術牌
            try
            {
                var spellCards = CardJsonLoader.LoadSpells();
                Debug.Log($"法術牌數量: {spellCards.Count}");
                foreach (var data in spellCards)
                {
                    SaveAsset(SpellCardSO.FromData(data), data.Id);
                    count++;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"載入法術牌失敗: {ex.Message}\n{ex.StackTrace}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"卡牌匯入完成，總共處理了 {count} 張卡牌");
            
            EditorUtility.DisplayDialog("成功", $"卡牌匯入完成！\n\n總共處理了 {count} 張卡牌", "確定");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"卡牌匯入發生錯誤: {ex.Message}\n{ex.StackTrace}");
            EditorUtility.DisplayDialog("錯誤", $"卡牌匯入失敗！\n\n錯誤信息:\n{ex.Message}", "確定");
        }
    }

    /// <summary>
    /// 如果資源已存在則覆寫，否則新建。
    /// </summary>
    private static void SaveAsset(CardSO card, string id)
    {
        try
        {
            // 将任何 Windows 路徑中的反斜杠替換為正斜杠，避免 Addressables 查找失敗
            if (!string.IsNullOrEmpty(card.ImagePath))
            {
                card.ImagePath = card.ImagePath.Replace("\\", "/");

                // 有些外部資料可能包含檔案副檔名（如 .png），Addressables 預設使用不帶副檔名的地址名稱。
                // 使用 Path.ChangeExtension 去掉副檔名，保持路徑其餘部分不變。
                card.ImagePath = Path.ChangeExtension(card.ImagePath, null)?.Replace("\\", "/");
            }

            // 确保 ImagePath 有前缀，如 cards/xxx
            if (!string.IsNullOrEmpty(card.ImagePath) && !card.ImagePath.StartsWith("cards/"))
            {
                card.ImagePath = $"cards/{card.ImagePath}";
            }

            string path = $"Assets/GameData/CardsAssets/{id}.asset";
            
            // 设置卡牌名称以避免警告
            card.name = id;
            
            // Ensure EnImagePath also uses forward slashes and proper path
            if (!string.IsNullOrEmpty(card.EnImagePath))
            {
                card.EnImagePath = card.EnImagePath.Replace("\\", "/");
                
                // Strip extension and add prefix if needed
                if (!card.EnImagePath.StartsWith("cards/"))
                {
                    card.EnImagePath = Path.ChangeExtension($"cards/{card.EnImagePath}", null);
                }
            }
            
            var existing = AssetDatabase.LoadAssetAtPath<CardSO>(path);
            
            if (existing == null)
            {
                AssetDatabase.CreateAsset(card, path);
                Debug.Log($"創建新卡牌資源: {id}.asset");
            }
            else
            {
                EditorUtility.CopySerialized(card, existing);
                Debug.Log($"更新現有卡牌資源: {id}.asset");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"保存卡牌資源 {id} 失敗: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }
}