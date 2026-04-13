using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuTitle : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider sliderFOV;
    [SerializeField] private Slider sliderVolume;
    [SerializeField] private Slider sliderSensitivity;
    [Header("Value Labels")]
    [SerializeField] private TextMeshProUGUI labelFOV;
    [SerializeField] private TextMeshProUGUI labelVolume;
    [SerializeField] private TextMeshProUGUI labelSensitivity;

    private bool isInitializing = false;

    private void OnEnable()
    {
        isInitializing = true;
        sliderVolume.minValue = 0f; 
        sliderVolume.maxValue = 100f;
        sliderSensitivity.minValue = 1f; 
        sliderSensitivity.maxValue = 100f;
        sliderFOV.minValue = 10f; 
        sliderFOV.maxValue = 100f;

        sliderVolume.value = saveMAnager.Instance.GetVolume();
        sliderSensitivity.value = saveMAnager.Instance.GetSensitivity();
        sliderFOV.value = saveMAnager.Instance.GetFoV();

        UpdateLabels();

        isInitializing = false;


        sliderVolume.onValueChanged.RemoveAllListeners();
        sliderSensitivity.onValueChanged.RemoveAllListeners();
        sliderFOV.onValueChanged.RemoveAllListeners();

        sliderVolume.onValueChanged.AddListener(OnVolumeChanged);
        sliderSensitivity.onValueChanged.AddListener(OnSensitivityChanged);
        sliderFOV.onValueChanged.AddListener(OnFOVChanged);
    }
    private void UpdateLabels()
    {
        if (labelVolume != null) labelVolume.text = $"{(int)sliderVolume.value}";
        if (labelSensitivity != null) labelSensitivity.text = $"{(int)sliderSensitivity.value}";
        if (labelFOV != null) labelFOV.text = $"{(int)sliderFOV.value}";
    }

    private void OnDisable()
    {
        sliderVolume.onValueChanged.RemoveAllListeners();
        sliderSensitivity.onValueChanged.RemoveAllListeners();
        sliderFOV.onValueChanged.RemoveAllListeners();
    }

    private void OnVolumeChanged(float value)
    {

        if (isInitializing) return;
        AudioListener.volume = value / 100f;
        saveMAnager.Instance.SetVolume(value);
        if (labelVolume != null) labelVolume.text = $"{(int)value}";
    }

    private void OnSensitivityChanged(float value)
    {
        if (isInitializing) return;
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.sensitivity = value;
        saveMAnager.Instance.SetSensitivity(value);
        if (labelSensitivity != null) labelSensitivity.text = $"{(int)value}";
    }

    private void OnFOVChanged(float value)
    {
        if (isInitializing) return;
        Camera.main.fieldOfView = value;
        saveMAnager.Instance.SetFoV(value);
        if (labelFOV != null) labelFOV.text = $"{(int)value}";
    }

}