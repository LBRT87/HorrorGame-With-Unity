using UnityEngine;

public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public string RoomName = "";

    public string UsernamePlayer
    {
        get => saveMAnager.Instance.GetUsernamePlayer();
        set => saveMAnager.Instance.SetUsername(value);
    }

    public string GenderPlayer
    {
        get => saveMAnager.Instance.GetGenderPlayer();
        set => saveMAnager.Instance.SetGender(value);

    }
}
