using UnityEngine;

public class SettingsApplier : MonoBehaviour
{
    void Start()
    {
        ApplyAllSettings();
    }

    public static void ApplyAllSettings()
    {
        if (saveMAnager.Instance == null) return;

        float vol = saveMAnager.Instance.GetVolume();
        AudioListener.volume = vol / 100f;

        float fov = saveMAnager.Instance.GetFoV();
        if (Camera.main != null)
            Camera.main.fieldOfView = fov;

        float sens = saveMAnager.Instance.GetSensitivity();
        foreach (var pm in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            pm.sensitivity = sens;

        Debug.Log($"[SettingsApplier] Applied — Vol:{vol} FOV:{fov} Sens:{sens}");
    }
}