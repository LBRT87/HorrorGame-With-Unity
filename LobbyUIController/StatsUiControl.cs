using TMPro;
using UnityEngine;

public class StatsUIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI matchSingleText;
    [SerializeField] private TextMeshProUGUI matchMultiText;
    [SerializeField] private TextMeshProUGUI winSingleText;
    [SerializeField] private TextMeshProUGUI winMultiText;

    private void OnEnable() => RefreshStats();

    public void RefreshStats()
    {
        if (saveMAnager.Instance == null) return;
        matchSingleText.text = saveMAnager.Instance.GetTotalSingleMatch().ToString();
        matchMultiText.text = saveMAnager.Instance.GetTotalMultiMatch().ToString();
        winSingleText.text = saveMAnager.Instance.GetTotalSingleWins().ToString();
        winMultiText.text = saveMAnager.Instance.GetTotalMultiWins().ToString();
    }
}