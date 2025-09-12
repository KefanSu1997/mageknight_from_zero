// Assets/Scripts/Runtime/DeckTester.cs
using UnityEngine;

public class DeckTester : MonoBehaviour
{
    public DeckRuntime deck;   // Inspector 里拖入

    void Start ()
    {
        deck.Shuffle();        // ① 洗一次牌
        for (int i = 0; i < 5; i++)
        {
            var card = deck.Draw();                    // ② 抽牌
            Debug.Log($"第{i+1}张抽到：{card.name}\n详细信息：{card}");   // ③ 打印卡名
        }
    }
}
