using UnityEngine;

public class JoinRoomController : MonoBehaviour
{
    public UIManager nav;
    public GameObject lobbyPovClient;

    public void KlikJoinRoomButton()
    {
        Debug.Log("Join Room");
        nav.ShowDynamicSwitchMenu(nav.lobbyClient);
    }

    public void Back()
    {
        Debug.Log("Back");
        nav.Back();
    }
}
