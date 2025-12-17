using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class CardRuntime : MonoBehaviour
{
    public CardSO Data { get; private set; }

    [Header("UI References")]
    [SerializeField] private Image artwork;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text costLabel;

    [Header("Selection Settings")]
    [SerializeField] private Image selectionHighlight;
    [SerializeField] private Color selectedColor = new Color(1f, 1f, 0f, 0.5f);
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0f);

    private AsyncOperationHandle<Sprite> _handle;
    private Sprite _frontSprite;
    private bool isSelected;

#nullable enable
    public event Action<CardSO> OnCardInfoChanged = delegate { };
#nullable disable

    public Image ArtworkImage => artwork;
    public Image SelectionHighlight => selectionHighlight;
    public bool IsSelected => isSelected;

    public void Init(CardSO data)
    {
        Data = data;

        if (artwork != null)
        {
            var fallback = DeckUiThemeCache.CardBackSprite;
            SetArtworkSprite(fallback);
            artwork.material = null;
        }

        RefreshFace();
        OnCardInfoChanged?.Invoke(Data);

        if (nameLabel != null)
        {
            nameLabel.maxVisibleLines = 1;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private void RefreshFace()
    {
        if (Data == null)
        {
            return;
        }

        if (nameLabel != null)
        {
            nameLabel.text = Data.NameCn;
        }

        if (costLabel != null)
        {
            if (Data is ActionCardSO action)
            {
                costLabel.text = string.Join(", ", action.RequiredCrystals.Select(c => c.ToString()));
            }
            else if (Data is SpellCardSO spell)
            {
                costLabel.text = string.Join(", ", spell.Top.ManaCost.Select(c => c.ToString()));
            }
            else
            {
                costLabel.text = "—";
            }
        }

        if (_handle.IsValid())
        {
            Data.ReleaseSprite();
        }

        _frontSprite = null;

        _handle = Data.LoadSpriteAsync();
        _handle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded && h.Result != null)
            {
                _frontSprite = h.Result;
                SetArtworkSprite(_frontSprite);
                if (artwork != null)
                {
                    artwork.material = null;
                }
            }
            else
            {
                Debug.LogWarning($"Load card sprite failed: {Data.ImagePath}");
                if (DeckUiThemeCache.CardBackSprite != null)
                {
                    SetArtworkSprite(DeckUiThemeCache.CardBackSprite);
                }
            }
        };

        if (selectionHighlight != null)
        {
            selectionHighlight.color = normalColor;
        }
    }

    public void ShowCardBack(Sprite backSprite)
    {
        if (backSprite == null)
        {
            return;
        }

        SetArtworkSprite(backSprite);
    }

    public void ShowCardFront()
    {
        if (_frontSprite != null)
        {
            SetArtworkSprite(_frontSprite);
        }
        else if (DeckUiThemeCache.CardBackSprite != null)
        {
            SetArtworkSprite(DeckUiThemeCache.CardBackSprite);
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionHighlight != null)
        {
            selectionHighlight.color = selected ? selectedColor : normalColor;
        }
    }

    private void SetArtworkSprite(Sprite sprite)
    {
        if (artwork == null)
        {
            return;
        }

        artwork.sprite = sprite;
        artwork.enabled = sprite != null;
    }

    private void OnDestroy()
    {
        if (Data != null)
        {
            Data.ReleaseSprite();
        }
    }
}
