using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using MK.Logic.Core;
using System.Text;
#nullable enable

/// <summary>
/// 所有卡牌 ScriptableObject 的共同基底，僅保存識別與圖片資訊。
/// </summary>
public abstract class CardSO : ScriptableObject
{
    [Tooltip("唯一識別碼")] public string Id = "";
    [Tooltip("中文名稱")] public string NameCn = "";
    [Tooltip("牌組來源")] public CardSet Set;
    [Tooltip("圖像路徑 (對應 Addressable 的地址)")] public string ImagePath = "";
    [Tooltip("英文圖像 (對應 Addressable 的地址)")] public string EnImagePath = "";

    // 非序列化字段用于跟踪加载句柄，避免重复加载/泄漏
    [System.NonSerialized] private AsyncOperationHandle<Sprite> _artHandle;

    /// <summary>
    /// 通过 Addressables 按 ImagePath 异步加载卡图。调用者应在合适时机释放句柄。
    /// </summary>
    public AsyncOperationHandle<Sprite> LoadSpriteAsync()
    {
        // 已经加载过则先释放旧句柄
        if (_artHandle.IsValid())
            Addressables.Release(_artHandle);

        _artHandle = Addressables.LoadAssetAsync<Sprite>(ImagePath);
        return _artHandle;
    }

    /// <summary>
    /// 如果已经加载过资源则释放它。
    /// </summary>
    public void ReleaseSprite()
    {
        if (_artHandle.IsValid())
        {
            Addressables.Release(_artHandle);
            _artHandle = default;
        }
    }

    public override string ToString()
    {
        return $"[{Set}] {NameCn} ({Id})";
    }
}