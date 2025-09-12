// Assets/Scripts/Runtime/GameController.cs
using UnityEngine;

public class GameController : MonoBehaviour
{
    public GameObject targetToToggle;

    public void OnButtonClicked()
    {
        if (targetToToggle != null)
        {
            targetToToggle.SetActive(!targetToToggle.activeSelf);
            Debug.Log($"[GameController] Toggled {targetToToggle.name}");
        }
        else
        {
            Debug.Log("[GameController] Clicked!");
        }
    }
}
