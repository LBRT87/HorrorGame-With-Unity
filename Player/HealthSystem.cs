using Unity.Netcode;
using UnityEngine;

public class HealthSystem : NetworkBehaviour
{
    public static int MAX_HEALTH = 3;

    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float fallStartY = 0f;
    private bool isFalling = false;
    private const float FALL_DAMAGE_THRESHOLD = 4f;

    private CharacterController _controller;
    private PlayerMovement _movement;
    private Animator _animator;

    private float _baseSpeed = -1f;

    public System.Action<int> OnHealthChanged;

    public override void OnNetworkSpawn()
    {
        _controller = GetComponent<CharacterController>();
        _movement = GetComponent<PlayerMovement>();
        _animator = GetComponent<Animator>();

        if (_movement != null && _baseSpeed < 0f)
            _baseSpeed = _movement.speed;

        currentHealth.OnValueChanged += (oldVal, newVal) =>
        {
            OnHealthChanged?.Invoke(newVal);
            if (IsOwner)
            {
                UpdateHealthBarUI(newVal);
                ApplySpeedPenalty(newVal);
                ApplyAnimationState(newVal);
                if (newVal < oldVal && newVal > 0)  
                    GetComponent<ExorcistSoundEffects>()?.PlayHurtSound();
            }
        };
        if (IsOwner)
        {
            UpdateHealthBarUI(currentHealth.Value);
            ApplySpeedPenalty(currentHealth.Value);
            ApplyAnimationState(currentHealth.Value);
            RegisterToHUD();
        }
    }

    private void ApplySpeedPenalty(int currentHp)
    {
        if (_movement == null) return;

        if (_baseSpeed < 0f) _baseSpeed = _movement.speed;

        float targetSpeed = currentHp switch
        {
            3 => _baseSpeed,          
            2 => _baseSpeed,         
            1 => _baseSpeed * 0.5f,   
            _ => 0f                   
        };

        _movement.speed = targetSpeed;
    }

    private void ApplyAnimationState(int currentHp)
    {
        if (_animator == null) return;

        _animator.SetBool("InjuredLight", false);
        _animator.SetBool("InjuredHeavy", false);
        _animator.SetBool("IsDead", false);

        switch (currentHp)
        {
            case 3:
                break;
            case 2:
                _animator.SetBool("InjuredLight", true);
                break;
            case 1:
                _animator.SetBool("InjuredHeavy", true);
                break;
            case 0:
                _animator.SetBool("IsDead", true);
                break;
        }

        SyncAnimationServerRpc(currentHp);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SyncAnimationServerRpc(int hp) => SyncAnimationClientRpc(hp);

    [ClientRpc]
    private void SyncAnimationClientRpc(int hp)
    {
        if (IsOwner) return;

        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator == null) return;

        _animator.SetBool("InjuredLight", hp == 2);
        _animator.SetBool("InjuredHeavy", hp == 1);
        _animator.SetBool("IsDead", hp <= 0);
    }

    private void UpdateHealthBarUI(int hp)
    {
        if (!IsOwner) return;
        float normalized = (float)hp / MAX_HEALTH;
        HUDManager.Instance?.GetExorcistHUD()?.UpdateHealthBar(normalized);
    }

    private void RegisterToHUD()
    {
        var hud = HUDManager.Instance?.GetExorcistHUD();
        if (hud != null)
        {
            hud.RegisterHealthSystem(this);
            return;
        }
        StartCoroutine(RetryRegisterHUD());
    }

    private System.Collections.IEnumerator RetryRegisterHUD()
    {
        int tries = 0;
        while (tries < 15)
        {
            yield return new WaitForSeconds(0.2f);
            var hud = HUDManager.Instance?.GetExorcistHUD();
            if (hud != null)
            {
                hud.RegisterHealthSystem(this);
                yield break;
            }
            tries++;
        }
        Debug.LogWarning("[Health] Gagal register ke HUD");
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int amount)
    {
        int newHp = Mathf.Clamp(currentHealth.Value - amount, 0, MAX_HEALTH);
        currentHealth.Value = newHp;
        Debug.Log($"[Health] TakeDamage {amount} = HP: {newHp}");

        if (newHp <= 0)
            OnDeadClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void HealServerRpc(int amount)
    {
        int newHp = Mathf.Clamp(currentHealth.Value + amount, 0, MAX_HEALTH);
        currentHealth.Value = newHp;
        Debug.Log($"[Health] Heal {amount} = HP: {newHp}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void DespawnMedkitServerRpc(ulong medkitNetworkObjectId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(medkitNetworkObjectId, out var netObj)) return;

        Debug.Log($"[Health] Despawn medkit: {medkitNetworkObjectId}");
        netObj.Despawn(true); 
    }

    [ClientRpc]
    private void OnDeadClientRpc()
    {
        Debug.Log("[Health] Player mati!");
        if (IsOwner)
            FindFirstObjectByType<HUD>()?.ShowDeathPopup();
    }
    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!IsOwner) return;
        TrackFall();
    }

    private void TrackFall()
    {
        var movement = GetComponent<PlayerMovement>();
        bool grounded = movement?.isNyentuhTanah ?? false;

        if (movement != null &&
            movement.currMovingStatus != PlayerMovement.movingStatus.Walking)
        {
            isFalling = false;
            return;
        }

        if (!grounded && !isFalling)
        {
            fallStartY = transform.position.y;
            isFalling = true;
        }

        if (grounded && isFalling)
        {
            float fallDist = fallStartY - transform.position.y;
            if (fallDist > FALL_DAMAGE_THRESHOLD)
                TakeDamageServerRpc(1);
            isFalling = false;
        }
    }

    public bool IsDead() => currentHealth.Value <= 0;
    public bool IsAlmostDead() => currentHealth.Value == 1;
    public bool IsInjured() => currentHealth.Value < MAX_HEALTH;
}