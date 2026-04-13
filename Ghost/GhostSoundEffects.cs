using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GhostSoundEffects : NetworkBehaviour
{
    public AudioClip screamClip;
    public AudioClip teleportClip;
    public AudioClip attackClip;

    public AudioSource ghostAudioSource;

    public float screamVolume = 1f;
    public float teleportVolume = 0.8f;
    public float attackVolume = 0.9f;

    private void Awake()
    {
        if (ghostAudioSource == null)
        {
            ghostAudioSource = GetComponent<AudioSource>();

        }
    }

    public override void OnNetworkSpawn()
    {
        if (ghostAudioSource != null)
        {
            ghostAudioSource.spatialBlend = 1f;   
            ghostAudioSource.loop = false;
            ghostAudioSource.playOnAwake = false;
        }
    }
    public void PlayScream()
    {
        PlaySoundServerRpc(SoundType.Scream);
    }

    public void PlayTeleport()
    {
        PlaySoundServerRpc(SoundType.Teleport);
    }

    public void PlayAttack()
    {
        PlaySoundServerRpc(SoundType.Attack);
    }

    private enum SoundType { Scream, Teleport, Attack }

    [ServerRpc(RequireOwnership = false)]
    private void PlaySoundServerRpc(SoundType type) => PlaySoundClientRpc(type);

    [ClientRpc]
    private void PlaySoundClientRpc(SoundType type)
    {
        if (ghostAudioSource == null) return;

        AudioClip clip = type switch
        {
            SoundType.Scream => screamClip,
            SoundType.Teleport => teleportClip,
            SoundType.Attack => attackClip,
            _ => null
        };

        float vol = type switch
        {
            SoundType.Scream => screamVolume,
            SoundType.Teleport => teleportVolume,
            SoundType.Attack => attackVolume,
            _ => 1f
        };

        if (clip != null)
            ghostAudioSource.PlayOneShot(clip, vol);
    }
    public void PlayLocalSound(AudioClip clip, float volume = 1f)
    {
        if (ghostAudioSource != null && clip != null)
        {
            ghostAudioSource.PlayOneShot(clip, volume);

        }
    }
}