using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject startMainMenu;
    public GameObject settingsMainMenu;
    public GameObject menuMain;

    public GameObject controlMenu;
    public GameObject howToPlayMenu;

    public GameObject stats_bg;
    public GameObject item_bg;
    public GameObject gender_bg;
    public GameObject ghost_bg;
    public GameObject singleplayer_btn;
    public GameObject multiplayer_btn;
    public GameObject usernameinput_btn;
    public GameObject garisMenu_btn;
    public GameObject level;
    public GameObject exp_slider;

    public GameObject singlePlayerMode;
    public GameObject multiPlayerMode;
    public GameObject createRoom;
    public GameObject joinRoom;
    public GameObject lobbyHost;
    public GameObject lobbyClient;

    private GameObject currMain;
    private GameObject prevMain;

    private GameObject currMenuDynamic;
    private GameObject previousMenuDynamic;
    private GameObject currSetDynamic;
    private GameObject prevSetDynamic;

    void Start()
    {
        ShowStartMenu();
    }

    public void ShowStartMenu()
    {
        DisableMain();
        startMainMenu.SetActive(true);
        currMain = startMainMenu;
    }

    public void ShowSettingDynamic(GameObject objk)
    {   
        if (currSetDynamic != null)
        {
            prevSetDynamic = currSetDynamic;
            currSetDynamic.SetActive(false);
        }

        currSetDynamic = objk;
        currSetDynamic.SetActive(true);
    }
    public void ShowSettingMenu()
    {
        DisableMain();
        settingsMainMenu.SetActive(true);
        currMain = settingsMainMenu;

        if (currSetDynamic != null)
        {
            currSetDynamic.SetActive(false);
            
        }
        currSetDynamic = null;
        prevSetDynamic = null;
         
    }
    public void DisableMain()
    {
        startMainMenu.SetActive(false);
        settingsMainMenu.SetActive(false);
        menuMain.SetActive(false);

    }
    
    public void EnableStaticMenuMain()
    {
        stats_bg.SetActive(true);
        item_bg.SetActive(true);
        gender_bg.SetActive(true);
        ghost_bg.SetActive(true);
        singleplayer_btn.SetActive(true);
        multiplayer_btn.SetActive(true);
        usernameinput_btn.SetActive(true);
        garisMenu_btn.SetActive(true);
        level.SetActive(true);
        exp_slider.SetActive(true);
    }

    public void ShowDynamicSwitchMenu(GameObject objek)
    {
        if (currMenuDynamic != null)
        {
            previousMenuDynamic = currMenuDynamic;
            currMenuDynamic.SetActive(false);
        }

        currMenuDynamic = objek;
        currMenuDynamic.SetActive(true);
    }
    public void ShowMenuMain()
    {
        DisableMain();
        menuMain.SetActive(true);
        currMain = menuMain;
        EnableStaticMenuMain();
        ShowDynamicSwitchMenu(singlePlayerMode);
    }

    public void Back()
    {
        if (currMain == menuMain && previousMenuDynamic != null)
        {
            currMenuDynamic.SetActive(false);
            currMenuDynamic = previousMenuDynamic;
            currMenuDynamic.SetActive(true);
            previousMenuDynamic = null;
            return;
        }

        if (currMain == settingsMainMenu && prevSetDynamic != null)
        {
            currMenuDynamic.SetActive(false);
            currMenuDynamic = previousMenuDynamic ;
            currSetDynamic.SetActive(true);
            prevSetDynamic = null;
            return;
        }

        if (currMain == menuMain || currMain == settingsMainMenu)
        {
            ShowStartMenu();
            return;
        }

        if (currMain == settingsMainMenu)
        {
            ShowStartMenu();
            return;
        }
    }
}
