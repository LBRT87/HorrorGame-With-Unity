using UnityEngine;

public class StartMenuController : MonoBehaviour
{
    public UIManager nav;
    public GameObject singlePlayerMode;
    public GameObject multiPlayerMode;

    public void KlikPlay()
    {
        nav.ShowMenuMain();
    }

    public void KlikSetting()
    {
        nav.ShowSettingMenu();
    }

    public void Klikquit()
    {
        Application.Quit();
    }

}
