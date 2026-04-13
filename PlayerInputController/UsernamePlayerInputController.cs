using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UsernamePlayerInputController : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputUsername;
    [SerializeField] private TextMeshProUGUI usernameText;
    private void OnEnable()
    {
        string saved = saveMAnager.Instance.GetUsernamePlayer();
        inputUsername.text = saved;

        inputUsername.onValueChanged.RemoveAllListeners();
        inputUsername.onValueChanged.AddListener(OnUsernamePreview);
    }

    private void OnDisable()
    {
        inputUsername.onValueChanged.RemoveAllListeners();
    }
    private void OnUsernamePreview(string value)
    {
        if (usernameText != null)
            usernameText.text = string.IsNullOrEmpty(value)
                ? saveMAnager.Instance.GetUsernamePlayer()
                : value;
    }


    public void SaveUsername()
    {
        string name = inputUsername.text.Trim();
        saveMAnager.Instance.SetUsername(name);

        if (usernameText != null)
        {
            usernameText.text = name;
        }
        Debug.Log("Saved: " + name);
    }
}