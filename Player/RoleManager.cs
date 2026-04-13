using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RoleManager : NetworkBehaviour
{
    public static RoleManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void AssignRoles (int ghostcount)
    {
        if (!IsServer)
        {
            return;
        }

        List<ulong> players = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        Shuffle(players);

        for (int i=0; i<players.Count; i++)
        {
            ulong clientId = players[i];

            var playerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
            var playerNetwork = playerObject.GetComponent<PlayerNetwork>();

            if (i < ghostcount)
            {
                playerNetwork.role.Value = PlayerRole.Ghost;
            }
            else
            {
                playerNetwork.role.Value = PlayerRole.Exorcist;
            }
        }
    }

    private void Shuffle(List<ulong> listPlayers)
    {
        for (int i=0; i<listPlayers.Count; i++)
        {
            int randomNumber = Random.Range(i, listPlayers.Count);
            ulong temp = listPlayers[i];
            listPlayers[i] = listPlayers[randomNumber];
            listPlayers[randomNumber] = temp;
        }
    }
}
