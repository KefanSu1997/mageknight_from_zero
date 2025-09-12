using UnityEngine;

public class Part1TestSceneSetup : MonoBehaviour
{
    [Header("Test Components")]
    public bool autoSetupUI = true;
    
    void Start()
    {
        if (autoSetupUI)
        {
            SetupTestScene();
        }
    }
    
    void SetupTestScene()
    {
        // Add UI setup if not already present
        var uiSetup = FindFirstObjectByType<Part1TestUISetup>();
        if (uiSetup == null)
        {
            gameObject.AddComponent<Part1TestUISetup>();
        }
        
        Debug.Log("Part 1 Test Scene Setup Complete!");
        Debug.Log("Use the buttons in the UI to test individual systems.");
        Debug.Log("Or use Unity Menu: Mage Knight → Test → Run Part 1 Validation");
    }
}