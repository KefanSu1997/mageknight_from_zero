// MageKnightLogicProj/IsExternalInit.cs
// 在舊版 Unity 編譯環境中缺乏 System.Runtime.CompilerServices.IsExternalInit
// 會導致使用 init 設定子時編譯失敗，此處提供簡易定義以相容。

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// 標記支援 init 只寫入屬性，僅供編譯期使用。
    /// </summary>
    internal static class IsExternalInit { }
}
