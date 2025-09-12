using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Deck",
    menuName = "MageKnight/Deck List",   // ← 这一行决定右键菜单路径
    order    = 30)]                      // 出现在菜单里的排序（可省）
public class DeckListSO : ScriptableObject
{
    public string DeckName;
    public List<CardSO> cards = new();   // 在 Inspector 里拖牌用
}
