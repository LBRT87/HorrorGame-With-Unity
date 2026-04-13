    using UnityEngine;

    public class MultiPlayerMode : MonoBehaviour
    {
        public UIManager nav;
        public GameObject createRoom;
        public GameObject joinRoom;

        public void KlikCreateLobby ()
        {
            nav.ShowDynamicSwitchMenu(nav.createRoom);
        }

        public void KlikJoinLobby ()
        {
            nav.ShowDynamicSwitchMenu(nav.joinRoom);
        }

        public void KlikLobbyList()
        {

        }

        public void KlikMultiPlayer ()
    {

        MultiPlayerManager.Instance.CurrentGameMode = GameMode.Multiplayer;
        nav.ShowDynamicSwitchMenu(nav.multiPlayerMode);
        Debug.Log("Multiplayermode dipilih");
    }
    }
