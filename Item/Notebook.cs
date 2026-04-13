using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class NotebookChatManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject notebookPanel;
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private TextMeshProUGUI chatHistoryText;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;

   
     private Color exorcistColor = Color.cyan;
     private Color ghostColor = Color.red;

    [Header("Proximity (Ghost detection)")]
    [SerializeField] private float proximityRadius = 4f;

    private PlayerRole _localRole = PlayerRole.Exorcist;
    private bool _panelOpen = false;
    private bool _exorcistHolding = false;

    public GameObject prefabExorcist;
    public GameObject prefabGhost;
    public static bool IsTyping = false;

    public override void OnNetworkSpawn()
    {
        if (notebookPanel != null)
            notebookPanel.SetActive(false);

        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendClicked);

        if (inputField != null)
            inputField.onSubmit.AddListener(_ => OnSendClicked());

        StartCoroutine(DetectLocalRole());
    }

    private IEnumerator DetectLocalRole()
    {
        float timeout = 10f; 
        PlayerNetwork localPlayer = null;

        while (localPlayer == null && timeout > 0)
        {
            var allPlayers = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);
            foreach (var pn in allPlayers)
            {
                if (pn.IsOwner) 
                {
                    localPlayer = pn;
                    break;
                }
            }

            if (localPlayer == null)
            {
                yield return new WaitForSeconds(0.5f);
                timeout -= 0.5f;
            }
        }

        if (localPlayer != null)
        {
            _localRole = localPlayer.role.Value;
            Debug.Log($"[NotebookChat] berhasil Deteksi Role: {_localRole}");

            if (_localRole == PlayerRole.Ghost)
                StartCoroutine(GhostProximityScanLoop());
        }
        else
        {
            Debug.LogError("[NotebookChat] gagal deteksi role setelah timeout!");
        }
    }

    public void SetExorcistHolding(bool holding)
    {
        _exorcistHolding = holding;

        if (_localRole == PlayerRole.Exorcist)
        {
            _panelOpen = holding;
            SetPanel(holding);
        }

        Debug.Log($"[NotebookChat] Exorcist holding: {holding}");
    }

    private IEnumerator GhostProximityScanLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.3f);

            bool inRange = false;

            foreach (var sis in FindObjectsByType<StarterItemSystem>(FindObjectsSortMode.None))
            {
                if (sis.IsOwner) continue;
                if (!sis.IsHolding(StarterItemType.Notebook)) continue;

                GameObject ghostObj = GetLocalGhostObject();
                if (ghostObj == null) continue;

                float dist = Vector3.Distance(ghostObj.transform.position, sis.transform.position);
                if (dist <= proximityRadius)
                {
                    inRange = true;
                    break;
                }
            }

            if (inRange != _panelOpen)
            {
                _panelOpen = inRange;
                SetPanel(_panelOpen);
                Debug.Log($"[NotebookChat] Ghost proximity: {(_panelOpen ? "masuk range" : "keluar range")}");
            }
        }
    }

    private void SetPanel(bool active)
    {
        if (notebookPanel == null) return;
        if (notebookPanel.activeSelf == active) return;

        notebookPanel.SetActive(active);

        if (active)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            inputField.interactable = true;
            inputField.ActivateInputField();
            inputField.Select();

            IsTyping = true;

            ScrollToBottom();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            inputField.interactable = false;

            IsTyping = false;
        }
    }
    public void OnSendClicked()
    {
        if (inputField == null) return;
        string msg = inputField.text.Trim();

        if (!string.IsNullOrEmpty(msg))
        {
            Debug.Log($"[NotebookChat] Mengirim pesan: {msg}");
            SendMessageServerRpc(msg, _localRole);
        }

        inputField.text = ""; 

        StartCoroutine(RefocusInputField());
    }

    private IEnumerator RefocusInputField()
    {
        yield return new WaitForEndOfFrame();
        if (notebookPanel.activeSelf)
        {
            inputField.ActivateInputField();
            inputField.Select();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendMessageServerRpc(string message, PlayerRole senderRole, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        string senderName = GetPlayerName(senderId);
        ReceiveMessageClientRpc(message, senderRole, senderName);
    }

    [ClientRpc]
    private void ReceiveMessageClientRpc(string message, PlayerRole senderRole, string senderName)
    {
        GameObject prefabToUse = (senderRole == PlayerRole.Ghost) ? prefabGhost : prefabExorcist;
        Color colorToUse = (senderRole == PlayerRole.Ghost) ? ghostColor : exorcistColor;
        string colorHex = ColorUtility.ToHtmlStringRGB(colorToUse);
        GameObject newTextObj = Instantiate(prefabToUse, chatScrollRect.content);
        TextMeshProUGUI textComp = newTextObj.GetComponent<TextMeshProUGUI>();
        textComp.text = $"<color=#{colorHex}><b>{senderName}:</b></color> {message}";
        ScrollToBottom();
    }

    private void AppendMessage(string message, PlayerRole role, string senderName)
    {
        if (chatHistoryText == null) return;

        string colorHex = role == PlayerRole.Exorcist
            ? ColorUtility.ToHtmlStringRGB(exorcistColor)
            : ColorUtility.ToHtmlStringRGB(ghostColor);

        chatHistoryText.text += $"\n<color=#{colorHex}><b>{senderName}:</b> {message}</color>";
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        if (chatScrollRect == null) return;
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        chatScrollRect.normalizedPosition = new Vector2(0, 0);
    }


    private GameObject GetLocalGhostObject()
    {
        foreach (var pn in FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None))
            if (pn.IsOwner && pn.role.Value == PlayerRole.Ghost)
                return pn.gameObject;
        return null;
    }

    private string GetPlayerName(ulong clientId)
    {
        if (MultiPlayerManager.Instance?.CurrLobby != null)
        {
            var ids = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            int idx = ids.IndexOf(clientId);
            if (idx >= 0 && idx < MultiPlayerManager.Instance.CurrLobby.Players.Count)
            {
                var p = MultiPlayerManager.Instance.CurrLobby.Players[idx];
                if (p.Data != null && p.Data.ContainsKey("UsernamePlayer"))
                    return p.Data["UsernamePlayer"].Value;
            }
        }
        return clientId == NetworkManager.Singleton.LocalClientId ? "You" : $"Player {clientId}";
    }

    public void ClosePanel()
    {
        _panelOpen = false;
        _exorcistHolding = false;
        SetPanel(false);
    }
}