using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class ExorcistSoundEffects : NetworkBehaviour
{
    public AudioClip heartbeatClip;
    public AudioClip walkClip;
    public AudioClip swimClip;
    public AudioClip hurtClip;
    public AudioClip crouchClip;
    public AudioSource heartbeatSource;
    public AudioSource worldSoundSource; 
    public float ghostDetectRadius = 15f;
    public float heartbeatMinVol = 0.1f;
    public float heartbeatMaxVol = 1f;

    public float walkVolumeNormal = 0.7f;
    public float walkVolumeCrouch = 0.1f; 
    public float swimVolumeBase = 0.6f;
    public float swimVolumeMax = 1f;
    public float crouchVolume = 0.2f;

    private PlayerMovement _movement;
    private bool _isCrouching = false;

    private bool _wasWalking = false;
    private bool _wasSwimming = false;

    private float _heartbeatCheckTimer = 0f;
    private const float HEARTBEAT_CHECK_INTERVAL = 0.2f;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (heartbeatSource != null)
            {
                heartbeatSource.enabled = false;
            }
            return;
        }

        if (heartbeatSource != null)
        {
            heartbeatSource.spatialBlend = 0f; 
            heartbeatSource.loop = true;
            heartbeatSource.clip = heartbeatClip;
            heartbeatSource.volume = 0f;
            if (heartbeatClip != null) heartbeatSource.Play();
        }

        if (worldSoundSource != null)
        {
            worldSoundSource.spatialBlend = 1f; 
            worldSoundSource.loop = true;
            worldSoundSource.volume = 0f;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleHeartbeat();
        HandleMovementSound();
    }

    private void HandleHeartbeat()
    {
        if (heartbeatSource == null || heartbeatClip == null) return;

        _heartbeatCheckTimer -= Time.deltaTime;
        if (_heartbeatCheckTimer > 0f) return;
        _heartbeatCheckTimer = HEARTBEAT_CHECK_INTERVAL;

        float closestDist = GetClosestGhostDistance();

        if (closestDist >= ghostDetectRadius)
        {
            heartbeatSource.volume = 0f;
            return;
        }

        float t = 1f - Mathf.Clamp01(closestDist / ghostDetectRadius);
        heartbeatSource.volume = Mathf.Lerp(heartbeatMinVol, heartbeatMaxVol, t);

        heartbeatSource.pitch = Mathf.Lerp(1f, 1.4f, t);
    }
    public void PlayHurtSound()
    {
        if (!IsOwner) return;
        if (heartbeatSource == null || hurtClip == null) return;
        heartbeatSource.PlayOneShot(hurtClip);
    }
    private float GetClosestGhostDistance()
    {
        float closest = float.MaxValue;

        foreach (var ghost in FindObjectsByType<GhostBasic>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, ghost.transform.position);
            if (d < closest) closest = d;
        }

        foreach (var ai in FindObjectsByType<GhostAIBase>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, ai.transform.position);
            if (d < closest) closest = d;
        }

        return closest;
    }
    private void HandleMovementSound()
    {
        if (worldSoundSource == null || _movement == null) return;

        bool isMoving = _movement.inputPlayerHandler != null &&
                         _movement.inputPlayerHandler.inputmove.magnitude > 0.1f;
        bool isSwimming = _movement.currMovingStatus == PlayerMovement.movingStatus.Swimming;

        bool isCrouching = _isCrouching; 

        if (isSwimming)
        {
            if (!_wasSwimming)
            {
                _wasSwimming = true;
                _wasWalking = false;
                PlayWorldSound(swimClip);
            }

            float swimT = Mathf.Clamp01(_movement.swimSpeed > 0
                ? _movement.inputPlayerHandler.inputmove.magnitude
                : 0f);
            worldSoundSource.volume = Mathf.Lerp(swimVolumeBase, swimVolumeMax, swimT);

            SyncWorldSoundServerRpc(worldSoundSource.volume, SoundType.Swim);
        }
        else if (isMoving && !isCrouching)
        {
            if (!_wasWalking)
            {
                _wasWalking = true;
                _wasSwimming = false;
                PlayWorldSound(walkClip);
                SyncWorldSoundServerRpc(walkVolumeNormal, SoundType.Walk);
            }

            worldSoundSource.volume = walkVolumeNormal;
        }
        else if (isMoving && isCrouching)
        {
            if (!_wasWalking)
            {
                _wasWalking = true;
                _wasSwimming = false;
                PlayWorldSound(crouchClip);
            }

            worldSoundSource.volume = walkVolumeCrouch;
            SyncWorldSoundServerRpc(walkVolumeCrouch, SoundType.Crouch);
        }
        else
        {
            if (_wasWalking || _wasSwimming)
            {
                _wasWalking = false;
                _wasSwimming = false;
                StopWorldSound();
                SyncWorldSoundServerRpc(0f, SoundType.Stop);
            }
        }
    }

    private void PlayWorldSound(AudioClip clip)
    {
        if (worldSoundSource == null || clip == null) return;
        if (worldSoundSource.clip == clip && worldSoundSource.isPlaying) return;
        worldSoundSource.clip = clip;
        worldSoundSource.Play();
    }

    private void StopWorldSound()
    {
        if (worldSoundSource != null)
            worldSoundSource.Stop();
    }

    private enum SoundType { Walk, Swim, Stop , Crouch }

    [ServerRpc]
    private void SyncWorldSoundServerRpc(float volume, SoundType sType)
        => SyncWorldSoundClientRpc(volume, sType);

    [ClientRpc]
    private void SyncWorldSoundClientRpc(float volume, SoundType sType)
    {
        if (IsOwner) return;

        if (worldSoundSource == null) return;

        switch (sType)
        {
            case SoundType.Walk:
                if (!worldSoundSource.isPlaying || worldSoundSource.clip != walkClip)
                {
                    worldSoundSource.clip = walkClip;
                    worldSoundSource.Play();
                }
                worldSoundSource.volume = volume;
                break;

            case SoundType.Swim:
                if (!worldSoundSource.isPlaying || worldSoundSource.clip != swimClip)
                {
                    worldSoundSource.clip = swimClip;
                    worldSoundSource.Play();
                }
                worldSoundSource.volume = volume;
                break;

            case SoundType.Stop:
                worldSoundSource.Stop();
                worldSoundSource.volume = 0f;
                break;
        }
    }

    public void SetCrouching(bool crouching)
    {
        _isCrouching = crouching;
    }
}