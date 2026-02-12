using System.Threading.Tasks;
using TMPro;
using Unity.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MatchmakingView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button _findMatchButton;
    [SerializeField] private Button _cancelMatchButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI lobbyInfoText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Dependencies")]
    [SerializeField] private MatchmakingClient client;

    private MatchmakingResultData _matchResult;

    private void Awake()
    {
        client = MatchmakingClient.Instance != null ? MatchmakingClient.Instance : client;

        if (client == null)
        {
            if (MatchmakingClient.Instance != null) client = MatchmakingClient.Instance;
            else
            {
                var go = new GameObject("MatchmakingClient");
                client = go.AddComponent<MatchmakingClient>();
            }
        }

        if (client != null) DontDestroyOnLoad(client.gameObject);

        var _ = UnityMainThreadDispatcher.Instance;
    }

    private void Start()
    {
        Debug.Log("[UI] MatchmakingView Start");

        bool isServer = MultiplayerRolesManager.ActiveMultiplayerRoleMask.HasFlag(MultiplayerRoleFlags.Server);
        if (isServer)
        {
            SceneManager.LoadScene("InGame");
            return;
        }

        UpdateStatus("Ready to find match");
        UpdateLobbyInfo("");
        SetLoading(false);

        Debug.Log($"[UI] findBtn null? {_findMatchButton == null}, cancelBtn null? {_cancelMatchButton == null}");

        _findMatchButton.onClick.AddListener(OnFindMatchButtonClicked);
        _cancelMatchButton.onClick.AddListener(OnCancelMatchButtonClicked);

        client.OnLobbyUpdated += OnLobbyUpdated;
        client.OnMatchAllocated += OnMatchAllocated;
        client.OnError += OnError;
        client.OnConnected += OnConnected;
        client.OnDisconnected += OnDisconnected;

        _ = ConnectToServerAsync();
    }

    private async Task ConnectToServerAsync()
    {
        UpdateStatus("Connecting to server...");
        SetLoading(true);

        bool success = await client.ConnectAsync();

        SetLoading(false);
        UpdateStatus(success ? "Connected! Click 'Find Match' to start" : "Failed to connect to server");
        _findMatchButton.interactable = true;
    }

    private async void OnFindMatchButtonClicked()
    {
        Debug.Log("[UI] FindMatch clicked");

        _findMatchButton.interactable = false;
        _matchResult = null;

        UpdateStatus("Searching for match...");
        UpdateLobbyInfo("");
        SetLoading(true);

        try
        {
            var result = await client.StartMatchmakingAsync();

            if (result != null && result.Success)
            {
                _matchResult = result;
                UpdateStatus($"Joined lobby #{result.LobbyId}");
            }
            else
            {
                UpdateStatus("Failed to find match");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UI] FindMatch exception: {ex}");
            UpdateStatus($"Error: {ex.Message}");
        }
        finally
        {
            if (_matchResult == null || !_matchResult.Success)
                SetLoading(false);

            _findMatchButton.interactable = true;
        }
    }

    private async void OnCancelMatchButtonClicked()
    {
        Debug.Log("[UI] Cancel clicked");

        UpdateStatus("Cancelling matchmaking...");
        SetLoading(true);

        try
        {
            bool success = await client.CancelMatchmakingAsync();
            UpdateStatus(success ? "Matchmaking cancelled" : "Failed to cancel matchmaking");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UI] Cancel exception: {ex}");
            UpdateStatus($"Cancel error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void OnLobbyUpdated(LobbyStatusData status)
    {
        Debug.Log($"[UI] Lobby updated: {status.CurrentPlayers}/{status.MaxPlayers}");
        UpdateLobbyInfo($"Players: {status.CurrentPlayers}/{status.MaxPlayers}");
        UpdateStatus($"Waiting for players... ({status.CurrentPlayers}/{status.MaxPlayers})");
    }

    private void OnMatchAllocated(MatchmakingResultData result)
    {
        Debug.Log($"[UI] Match allocated! Lobby {result.LobbyId}");

        if (string.IsNullOrEmpty(result.GameServerIP) || result.GameServerPort == 0)
        {
            Debug.LogError("[MatchmakingView] No server info in allocation result!");
            UpdateStatus("Error: No server info");
            SetLoading(false);
            return;
        }

        PlayerPrefs.SetString("GameServerIP", result.GameServerIP);
        PlayerPrefs.SetInt("GameServerPort", result.GameServerPort);
        PlayerPrefs.SetInt("LobbyId", result.LobbyId);
        PlayerPrefs.Save();

        UpdateStatus("Match found! Loading game...");
        UpdateLobbyInfo($"Lobby #{result.LobbyId} - Ready!");
        SetLoading(false);

        // 씬 넘어가기 전 구독 해제
        client.OnLobbyUpdated -= OnLobbyUpdated;
        client.OnMatchAllocated -= OnMatchAllocated;
        client.OnError -= OnError;
        client.OnConnected -= OnConnected;
        client.OnDisconnected -= OnDisconnected;

        SceneManager.LoadScene("InGame");
    }

    private void OnError(string message)
    {
        Debug.LogError($"[UI] Error: {message}");
        UpdateStatus($"Error: {message}");
        _findMatchButton.interactable = true;
        SetLoading(false);
    }

    private void OnConnected()
    {
        Debug.Log("[UI] Connected");
        UpdateStatus("Connected! Click 'Find Match' to start");
        _findMatchButton.interactable = true;
    }

    private void OnDisconnected()
    {
        Debug.LogWarning("[UI] Disconnected");
        UpdateStatus("Disconnected from server. Reconnecting...");
        _findMatchButton.interactable = false;
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log($"[Status] {message}");
    }

    private void UpdateLobbyInfo(string info)
    {
        if (lobbyInfoText != null) lobbyInfoText.text = info;
    }

    private void SetLoading(bool isLoading)
    {
        if (loadingIndicator != null) loadingIndicator.SetActive(isLoading);
    }

    private void OnDestroy()
    {
        if (client != null)
        {
            client.OnLobbyUpdated -= OnLobbyUpdated;
            client.OnMatchAllocated -= OnMatchAllocated;
            client.OnError -= OnError;
            client.OnConnected -= OnConnected;
            client.OnDisconnected -= OnDisconnected;
        }

        if (_findMatchButton != null)
            _findMatchButton.onClick.RemoveListener(OnFindMatchButtonClicked);

        if (_cancelMatchButton != null)
            _cancelMatchButton.onClick.RemoveListener(OnCancelMatchButtonClicked);
    }
}
