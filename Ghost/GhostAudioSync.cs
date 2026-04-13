using Unity.Netcode;
using UnityEngine;

public class GhostAudioSync : NetworkBehaviour
{
    [SerializeField] private AudioClip screamClip;
    [SerializeField] private AudioSource audioSource;

    public void PlayScream()
    {
        if (!IsOwner) return;
        PlayScreamServerRpc();
    }

    [ServerRpc]
    private void PlayScreamServerRpc()
    {
        PlayScreamClientRpc();
    }

    [ClientRpc]
    private void PlayScreamClientRpc()
    {
        audioSource.PlayOneShot(screamClip);
    }
}