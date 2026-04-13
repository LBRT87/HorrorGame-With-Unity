using UnityEngine;

public class SettingMenuController : MonoBehaviour
{
    public UIManager nav;
    public GameObject ControlMenu;
    public GameObject HowtoPlayMenu;

    public void OpenControlMenu()
    {
        Debug.Log("Open control menu");
        nav.ShowSettingDynamic(nav.controlMenu);
    }

    public void OpenHowtoPlayMenu()
    {
        Debug.Log("Open How To Play");
        nav.ShowSettingDynamic(nav.howToPlayMenu);
    }

    public void Back()
    {
        Debug.Log("Balik");
        nav.Back();
    }
}
