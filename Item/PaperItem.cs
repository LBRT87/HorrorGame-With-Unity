using TMPro;
using UnityEngine;

public class PaperItem : MonoBehaviour
{
    [Header("UI Text")]
    public TextMeshProUGUI ghostHintText;
    public TextMeshProUGUI itemHintText;

    [Header("World Space Canvas")]
    public GameObject contentCanvas; 

    public string _ghostHint;
    public string _itemHint;

    public void SetContent(string ghostHint, string itemHint)
    {
        _ghostHint = ghostHint;
        _itemHint = itemHint;

        if (ghostHintText != null) ghostHintText.text = ghostHint;
        if (itemHintText != null) itemHintText.text = itemHint;
    }

    public void ShowPaper()
    {
        if (contentCanvas != null) contentCanvas.SetActive(true);
    }

    public void HidePaper()
    {
        if (contentCanvas != null) contentCanvas.SetActive(false);
    }

    public string GetContent() => $"{_ghostHint}\n{_itemHint}";
}