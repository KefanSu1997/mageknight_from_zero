// Assets/Scripts/Runtime/DeckTester.cs
using UnityEngine;

public class DeckTester : MonoBehaviour
{
    public DeckRuntime deck;   // Inspector 里拖入
    [SerializeField] private bool runOnStart = false;
    [SerializeField, Range(1, 10)] private int cardsToDraw = 5;

    private void Start ()
    {
        if (!runOnStart)
        {
            return;
        }

        RunDeckTest();
    }

    [ContextMenu("Run Deck Test")]
    public void RunDeckTest()
    {
        if (deck == null)
        {
            Debug.LogWarning("DeckTester: DeckRuntime 未配置，无法执行测试。");
            return;
        }

        deck.Shuffle();        // ① 洗一次牌
        for (int i = 0; i < cardsToDraw; i++)
        {
            var card = deck.Draw();                    // ② 抽牌
            if (card == null)
            {
                Debug.LogWarning($"DeckTester: 第{i + 1}次尝试抽牌时牌库已空。");
                break;
            }
            Debug.Log($"第{i+1}张抽到：{card.name}\n详细信息：{card}");   // ③ 打印卡名
        }
    }
}
