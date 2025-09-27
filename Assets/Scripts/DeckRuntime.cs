using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 在 Unity 場景中管理抽牌與棄牌的牌堆物件。
/// 透過 <see cref="CardSO"/> 資料實例化運行時牌組。
/// </summary>
public class DeckRuntime : MonoBehaviour
{
    public DeckListSO deckListSo;         // Drag "StarterDeck.asset" here
    public List<CardSO> drawPile { get; private set; }
    public Stack<CardSO> discard = new();

    public event Action DeckStateChanged;

    private void NotifyStateChanged() => DeckStateChanged?.Invoke();

    /* NEW */ void Awake()
    {
        if (deckListSo == null)
        {
            Debug.LogError($"{name}: DeckListSO not assigned!");
            return;
        }
        Init();                           // ensure drawPile is ready
    }

    public void Init()
    {
        drawPile = new List<CardSO>(deckListSo.cards ?? new()); // null-safe copy
        Shuffle();
    }

    public void Shuffle()
    {
        if (drawPile == null)
        {
            drawPile = new List<CardSO>();
        }

        if (drawPile.Count > 0)
        {
            drawPile = drawPile.OrderBy(_ => UnityEngine.Random.value).ToList(); /* shuffle idiom :contentReference[oaicite:2]{index=2} */
        }

        NotifyStateChanged();
    }

    public CardSO Draw()
    {
        if (drawPile == null || drawPile.Count == 0)
        {
            Recycle();
            if (drawPile == null || drawPile.Count == 0)
            {
                return null;               // still empty
            }
        }
        var card = drawPile[0];
        drawPile.RemoveAt(0);
        NotifyStateChanged();
        return card;
    }

    public void Recycle()
    {
        if (discard.Count == 0) return;
        drawPile = discard.ToList();
        discard.Clear();
        Shuffle();
    }

    public void Discard(CardSO c)
    {
        if (c == null)
        {
            return;
        }

        discard.Push(c);
        NotifyStateChanged();
    }
}
