using UnityEngine;

public class MainMenuSceneController : MonoBehaviour
{
    public static string TargetPanel = "Title";

    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject lobbyPanel;

    private void Start()
    {
        titlePanel.SetActive(false);
        lobbyPanel.SetActive(false);

        if (TargetPanel == "Lobby")
            lobbyPanel.SetActive(true);
        else
            titlePanel.SetActive(true);
    }
}