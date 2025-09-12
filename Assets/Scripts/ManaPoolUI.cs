using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using MK.Logic.Runtime;

public class ManaPoolUI : MonoBehaviour
{
    [Header("Mana Displays")]
    public TextMeshProUGUI redManaText;
    public TextMeshProUGUI blueManaText;
    public TextMeshProUGUI whiteManaText;
    public TextMeshProUGUI greenManaText;
    public TextMeshProUGUI colorlessManaText;
    
    [Header("Mana Containers")]
    public GameObject redManaContainer;
    public GameObject blueManaContainer;
    public GameObject whiteManaContainer;
    public GameObject greenManaContainer;
    public GameObject colorlessManaContainer;
    
    [Header("Test Controls")]
    public Button testRedButton;
    public Button testBlueButton;
    public Button testWhiteButton;
    public Button testGreenButton;
    public Button testResetButton;
    
    private ManaPool manaPool;
    
    void Start()
    {
        manaPool = new ManaPool();
        SetupUI();
        UpdateDisplay();
    }
    
    void SetupUI()
    {
        testRedButton.onClick.AddListener(() => TestAddMana(MK.Logic.Core.ManaColor.Red));
        testBlueButton.onClick.AddListener(() => TestAddMana(MK.Logic.Core.ManaColor.Blue));
        testWhiteButton.onClick.AddListener(() => TestAddMana(MK.Logic.Core.ManaColor.White));
        testGreenButton.onClick.AddListener(() => TestAddMana(MK.Logic.Core.ManaColor.Green));
        testResetButton.onClick.AddListener(ResetMana);
    }
    
    public void UpdateDisplay()
    {
        if (redManaText != null)
            redManaText.text = $"{manaPool.Crystals.GetValueOrDefault(MK.Logic.Core.ManaColor.Red)}";
        
        if (blueManaText != null)
            blueManaText.text = $"{manaPool.Crystals.GetValueOrDefault(MK.Logic.Core.ManaColor.Blue)}";
        
        if (whiteManaText != null)
            whiteManaText.text = $"{manaPool.Crystals.GetValueOrDefault(MK.Logic.Core.ManaColor.White)}";
        
        if (greenManaText != null)
            greenManaText.text = $"{manaPool.Crystals.GetValueOrDefault(MK.Logic.Core.ManaColor.Green)}";
        
        if (colorlessManaText != null)
            colorlessManaText.text = $"{manaPool.Crystals.GetValueOrDefault(MK.Logic.Core.ManaColor.Gold)}";
    }
    
    void TestAddMana(MK.Logic.Core.ManaColor color)
    {
        manaPool.AddCrystal(color, 1);
        UpdateDisplay();
        Debug.Log($"Added 1 {color} mana. Total: {manaPool.Crystals.GetValueOrDefault(color)}");
    }
    
    void ResetMana()
    {
        manaPool.Crystals.Clear();
        UpdateDisplay();
        Debug.Log("Mana pool reset");
    }
}