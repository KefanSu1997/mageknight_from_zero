using UnityEngine;
using UnityEngine.UI;

public class DeckDemoUI : MonoBehaviour
{
    public HandManagerAdvanced handManager;
    public Button drawButton;
    public Button discardButton;
    public Button playButton;
    public Button shuffleButton;
    public Button recycleButton;
    public Text deckCountText;
    public Text discardCountText;
    public Text statusText;

    void Start()
    {
        if (handManager == null)
            handManager = FindObjectOfType<HandManagerAdvanced>();

        SetupButtons();
        UpdateUI();
    }

    void Update()
    {
        UpdateUI();
    }

    private void SetupButtons()
    {
        if (drawButton != null && handManager != null)
            drawButton.onClick.AddListener(() => {
                handManager.Draw(1);
                UpdateUI();
            });

        if (discardButton != null && handManager != null)
            discardButton.onClick.AddListener(() => {
                handManager.DiscardSelected();
                UpdateUI();
            });

        if (playButton != null && handManager != null)
            playButton.onClick.AddListener(() => {
                handManager.PlaySelected();
                UpdateUI();
            });

        if (shuffleButton != null && handManager != null)
            shuffleButton.onClick.AddListener(() => {
                handManager.ShuffleDeck();
                UpdateUI();
            });

        if (recycleButton != null && handManager != null)
            recycleButton.onClick.AddListener(() => {
                handManager.RecycleDiscard();
                UpdateUI();
            });
    }

    private void UpdateUI()
    {
        if (handManager == null || handManager.deck == null) return;

        if (deckCountText != null)
            deckCountText.text = $"牌组: {handManager.deck.drawPile?.Count ?? 0}";

        if (discardCountText != null)
            discardCountText.text = $"弃牌: {handManager.deck.discard?.Count ?? 0}";

        if (statusText != null)
            statusText.text = GetStatusText();
    }

    private string GetStatusText()
    {
        int totalCards = (handManager.deck.drawPile?.Count ?? 0) + 
                        (handManager.deck.discard?.Count ?? 0) + 
                        (handManager.transform.childCount);
        
        return $"总计 {totalCards} 张卡牌\n手牌: {handManager.transform.childCount}";
    }

    [ContextMenu("Test Cards Move")]
    public void TestCardFlow()
    {
        if (handManager != null)
        {
            // 模拟完整的卡牌流动
            handManager.Draw(8);
        }
    }
}