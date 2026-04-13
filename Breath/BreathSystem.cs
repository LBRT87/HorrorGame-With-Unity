using Unity.Netcode;
using UnityEngine;

public class BreathSystem : NetworkBehaviour
{
    [Header("Breath Settings")]
    [SerializeField] private float maxBreath = 100f;
    [SerializeField] private float breathDrainRate = 10f;  
    [SerializeField] private float breathFillRate = 15f;  
    [SerializeField] private float breathFillOutOfWater = 30f; 
    [SerializeField] private float damageInterval = 1f; 

    private float _currentBreath;
    private float _damageTimer;
    private PlayerMovement _movement;
    private HealthSystem _health;
    private HUD _hud;

    void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _health = GetComponent<HealthSystem>();
        _currentBreath = maxBreath;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        _hud = HUDManager.Instance?.GetExorcistHUD();

        if (_hud == null)
        {
            StartCoroutine(RetryGetHUD());
        }
    }
    private System.Collections.IEnumerator RetryGetHUD()
    {
        int tries = 0;
        while (tries < 10)
        {
            yield return new WaitForSeconds(0.2f);
            _hud = HUDManager.Instance?.GetExorcistHUD();
            if (_hud != null) yield break;
            tries++;
        }
        Debug.LogWarning("[Breath] Gagal dpt HUD");
    }
    void Update()
    {
        if (!IsOwner) return;

        bool isSwimming = _movement.currMovingStatus ==
                          PlayerMovement.movingStatus.Swimming;
        bool isMovingInWater = isSwimming &&
                               _movement.GetComponent<PlayerInputHandler>()
                               .inputmove.magnitude > 0.1f;

        if (isSwimming)
        {
            _hud?.ShowBreathBar();
            if (isMovingInWater)
            {
                _currentBreath -= breathDrainRate * Time.deltaTime;
                _currentBreath = Mathf.Max(0, _currentBreath);

                if (_currentBreath <= 0)
                {
                    _damageTimer += Time.deltaTime;
                    if (_damageTimer >= damageInterval)
                    {
                        _damageTimer = 0f;
                        _health?.TakeDamageServerRpc(1);
                        Debug.Log("[Breath] Kehabisan napas! -1 HP");
                    }
                }
                else
                {
                    _damageTimer = 0f;
                }
            }
            else
            {
                _currentBreath += breathFillRate * Time.deltaTime;
                _currentBreath = Mathf.Min(maxBreath, _currentBreath);
                _damageTimer = 0f;
            }
        }
        else
        {
            _currentBreath += breathFillOutOfWater * Time.deltaTime;
            _currentBreath = Mathf.Min(maxBreath, _currentBreath);
            _damageTimer = 0f;

            if (_currentBreath >= maxBreath)
                _hud?.HideBreathBar();
        }

        _hud?.UpdateBreath(_currentBreath / maxBreath);
    }
}