using UnityEngine;
using Unity.Cinemachine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    public CinemachineCamera firstPersonCam;
    public CinemachineCamera thirdPersonCam;

    public float currentSensitivity;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        LoadAndApplySettings();
    }

    public void LoadAndApplySettings()
    {
        float fov = saveMAnager.Instance.GetFoV();
        float sens = saveMAnager.Instance.GetSensitivity();
        float vol = saveMAnager.Instance.GetVolume();

        ApplyFOV(fov);
        ApplySensitivity(sens);
        ApplyVolume(vol);
    }
    public void ApplyFOV(float fov)
    {
        if (firstPersonCam != null)
            firstPersonCam.Lens.FieldOfView = fov;

        if (thirdPersonCam != null)
            thirdPersonCam.Lens.FieldOfView = fov;
    }

    public void ApplySensitivity(float sens)
    {
        currentSensitivity = sens;
    }

    public void ApplyVolume(float vol)
    {
        AudioListener.volume = vol / 100f;
    }


    public void SetFOV(float value)
    {
        saveMAnager.Instance.SetFoV(value);
        ApplyFOV(value);
    }

    public void SetSensitivity(float value)
    {
        saveMAnager.Instance.SetSensitivity(value);
        ApplySensitivity(value);
    }

    public void SetVolume(float value)
    {
        saveMAnager.Instance.SetVolume(value);
        ApplyVolume(value);
    }
}