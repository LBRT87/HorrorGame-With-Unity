using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class OuijaBoardManager : NetworkBehaviour
{
    public static OuijaBoardManager Instance { get; private set; }
    public GameObject ouijaBoardPrefab;
    public Transform spawnPoint;

    public float introDuration = 3f;

    public bool ouijaBoardActive = false;

    private GameObject _boardInstance;
    private bool _introPlayed = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (MultiPlayerManager.Instance?.CurrentGameMode == GameMode.SinglePlayer)
        {
            Debug.Log("ouija gabisa di single");
            return;
        }

        bool shouldSpawn = GetOuijaActiveSetting();
        if (!shouldSpawn)
        {
            Debug.Log("[OuijaBoard] Ouija tidak aktif");
            return;
        }

        SpawnBoardServerSide();
        SyncOuijaSettingsClientRpc(true);
        StartCoroutine(RunIntroSequenceServer());
    }


    private IEnumerator RunIntroSequenceServer()
    {
        yield return new WaitForSeconds(0.5f);

        Vector3 boardPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;

        DisablePlayerInputClientRpc();
        StartIntroClientRpc(boardPos, introDuration);

        yield return new WaitForSeconds(introDuration + 0.5f);

        EnablePlayerControlClientRpc();
    }


    private void SpawnBoardServerSide()
    {
        if (ouijaBoardPrefab == null || spawnPoint == null)
        {
            Debug.LogError("[OuijaBoard] nul");
            return;
        }

        _boardInstance = Instantiate(ouijaBoardPrefab, spawnPoint.position, spawnPoint.rotation);
        var netObj = _boardInstance.GetComponent<NetworkObject>();
        if (netObj != null) netObj.Spawn();

        Debug.Log("[OuijaBoard] Board spawned di: " + spawnPoint.position);
    }


    [ClientRpc]
    private void SyncOuijaSettingsClientRpc(bool active)
    {
        OuijaSettings.IsActive = active;
        Debug.Log($"[OuijaBoard] Client sync OuijaSettings: {active}");
    }

    [ClientRpc]
    private void DisablePlayerInputClientRpc()
    {
        var input = FindFirstObjectByType<PlayerInputHandler>();
        if (input != null) input.enabled = false;

        var ghostInput = FindFirstObjectByType<GhostInputHandler>();
        if (ghostInput != null) ghostInput.enabled = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[OuijaBoard] Input disabled intro");
    }

    [ClientRpc]
    private void StartIntroClientRpc(Vector3 boardPosition, float duration)
    {
        if (_introPlayed) return;
        _introPlayed = true;
        StartCoroutine(PlayIntroSequence(boardPosition, duration));
    }

    private IEnumerator PlayIntroSequence(Vector3 boardPosition, float duration)
    {
        Debug.Log("[OuijaBoard] Intro mulai");

        GameObject board = null;
        float timeout = 5f;

        while (board == null && timeout > 0)
        {
            board = GameObject.FindWithTag("OuijaBoard");
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (board != null)
        {
            boardPosition = board.transform.position;
        }
        else
        {
            Debug.LogWarning("[OuijaBoard] Board belum spawn");
        }
    }


        [ClientRpc]
        private void EnablePlayerControlClientRpc()
        {
        var input = FindFirstObjectByType<PlayerInputHandler>();
        if (input != null) input.enabled = true;

        var ghostInput = FindFirstObjectByType<GhostInputHandler>();
        if (ghostInput != null) ghostInput.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[OuijaBoard] Player control aktif");
    }


    private bool GetOuijaActiveSetting()
    {
        if (MultiPlayerManager.Instance?.CurrentGameMode == GameMode.SinglePlayer)
            return false;

        var lobby = MultiPlayerManager.Instance?.CurrLobby;
        if (lobby != null && lobby.Data != null && lobby.Data.ContainsKey("OuijaActive"))
            return lobby.Data["OuijaActive"].Value == "true";

        if (IsServer) return OuijaSettings.IsActive;
        return ouijaBoardActive;
    }

    public static void SetActiveFromLobby(bool active)
    {
        Debug.Log($"[OuijaBoard] Setting dari lobby={active}");
    }
}