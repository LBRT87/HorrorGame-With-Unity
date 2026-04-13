using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OuijaLobbyToggle : MonoBehaviour
{
    [SerializeField] private Button ouijaToggleButton;
    [SerializeField] private TextMeshProUGUI ouijaButtonText;

    private void Start()
    {
        UpdateButtonVisual(OuijaSettings.IsActive);
    }

    public void OnToggleClicked()
    {
        OuijaSettings.IsActive = !OuijaSettings.IsActive;
        UpdateButtonVisual(OuijaSettings.IsActive);
        Debug.Log($"[Ouija] Toggle {OuijaSettings.IsActive}");
    }

    private void UpdateButtonVisual(bool active)
    {
        if (ouijaButtonText != null)
            ouijaButtonText.text = active ? "Ouija: ON" : "Ouija: OFF";
    }
}