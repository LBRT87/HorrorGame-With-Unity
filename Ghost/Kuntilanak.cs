using Unity.Netcode;
using UnityEngine;

public class Kuntilanak : GhostBasic
{
    [Header("Fly")]
    public float speedFly = 8f;
    public float speedNormal = 4f;

    [Header("Slow Aura (Passive)")]
    public float slowRadius = 5f;
    public float slowAmount = 0.5f;

    private PlayerMovement[] _cachedPlayers;
    private float _playerCacheTimer = 0f;
    private const float PlayerCacheInterval = 3f;
    private System.Collections.Generic.Dictionary<PlayerMovement, float> _originalSpeeds
        = new System.Collections.Generic.Dictionary<PlayerMovement, float>();

    private float _lastSpacePressTime = -99f;
    private const float DoubleTapWindow = 0.35f;

    protected override void Awake()
    {
        base.Awake();
        ghostType = GhostType.Kuntilanak;
        speedMove = speedNormal;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsOwner) return;
        StartCoroutine(FixSpawnPosition());
    }

    private System.Collections.IEnumerator FixSpawnPosition()
    {
        yield return new WaitForSeconds(0.1f);
        if (controller != null && Physics.Raycast(
            transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, groundLayer))
        {
            controller.enabled = false;
            transform.position = hit.point + Vector3.up * 1f;
            controller.enabled = true;
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!IsOwner) return;

        HandleFlyDoubleTap();
        ApplySlowAura();
    }

    protected override void HandleGroundMovement()
    {
        if (ghostInputHandler == null) return;
        Transform camT = GetCameraTransform();
        if (camT == null) return;

        if (isFly)
        {
            Vector3 right = camT.right; right.y = 0; right.Normalize();
            Vector3 forward = camT.forward; 
            Vector3 moveDir = forward * ghostInputHandler.InputMove.y
                            + right * ghostInputHandler.InputMove.x;

            if (controller != null) controller.Move(moveDir * speedMove * Time.deltaTime);

            Vector3 flatMove = new Vector3(moveDir.x, 0, moveDir.z);
            if (flatMove.magnitude > 0.1f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(flatMove),
                    speedRotate * Time.deltaTime);

            SyncAnimSpeed(moveDir.magnitude);
            SyncAnimIsFly(true);
        }
        else
        {
            base.HandleGroundMovement();
        }
    }

    protected override void HandleFlyMovement()
    {
        base.HandleFlyMovement();
        SyncAnimIsFly(true);
    }

    private void HandleFlyDoubleTap()
    {
        if (ghostInputHandler == null) return;
        if (!ghostInputHandler.FlyPressed) return;

        ghostInputHandler.ResetFly();

        float now = Time.time;
        if (now - _lastSpacePressTime <= DoubleTapWindow)
        {
            ToggleFly();
            _lastSpacePressTime = -99f; 
        }
        else
        {
            _lastSpacePressTime = now;
        }
    }

    private void ToggleFly()
    {
        isFly = !isFly;
        speedMove = isFly ? speedFly : speedNormal;
        if (!isFly) _velocityY = 0f;
        SyncAnimIsFly(isFly);
        SyncFlyAnimServerRpc(isFly);
        Debug.Log($"[Kuntilanak] Fly toggle: {isFly}");
    }

    [ServerRpc] private void SyncFlyAnimServerRpc(bool fly) => SyncFlyAnimClientRpc(fly);
    [ClientRpc]
    private void SyncFlyAnimClientRpc(bool fly)
    { if (!IsOwner && animator != null) animator.SetBool(AnimIsFly, fly); }

    protected override void UseSpecialSkill()
    {
    }

    protected override bool TryUseSpecialSkillNearby()
    {
        return false;
    }

    private void ApplySlowAura()
    {
        _playerCacheTimer -= Time.deltaTime;
        if (_playerCacheTimer <= 0f || _cachedPlayers == null)
        {
            _cachedPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
            _playerCacheTimer = PlayerCacheInterval;
        }

        foreach (var exorcist in _cachedPlayers)
        {
            if (exorcist == null) continue;
            float dist = Vector3.Distance(transform.position, exorcist.transform.position);

            if (dist <= slowRadius)
            {
                if (!_originalSpeeds.ContainsKey(exorcist))
                    _originalSpeeds[exorcist] = exorcist.speed;

                float target = _originalSpeeds[exorcist] * slowAmount;
                if (exorcist.speed > target)
                {
                    float newSpeed = Mathf.Lerp(exorcist.speed, target, Time.deltaTime * 5f);
                    ApplySlowServerRpc(exorcist.GetComponent<NetworkObject>().NetworkObjectId, newSpeed);
                }
            }
            else if (_originalSpeeds.TryGetValue(exorcist, out float orig))
            {
                if (exorcist.speed < orig)
                    ApplySlowServerRpc(exorcist.GetComponent<NetworkObject>().NetworkObjectId, orig);
                _originalSpeeds.Remove(exorcist);
            }
        }
    }

    [ServerRpc]
    private void ApplySlowServerRpc(ulong netId, float newSpeed)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out var no)) return;
        var pm = no.GetComponent<PlayerMovement>();
        if (pm != null) pm.speed = newSpeed;
    }

    protected override void HandleAttack()
    {
        if (animator != null) animator.SetTrigger(AnimAttack);
        base.HandleAttack();
    }

    public override void ApplyPassiveSkillToGhost(PlayerMovement target)
    {
        if (!_originalSpeeds.ContainsKey(target)) _originalSpeeds[target] = target.speed;
        target.speed = _originalSpeeds[target] * slowAmount;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
        Gizmos.DrawSphere(transform.position, slowRadius);
    }
}