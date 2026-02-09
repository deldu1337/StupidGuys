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
        if (client == null)
        {
            var go = new GameObject("MatchmakingClient");
            client = go.AddComponent<MatchmakingClient>();
            DontDestroyOnLoad(go);
        }

        var _ = UnityMainThreadDispatcher.Instance;
    }

    private void Start()
    {
        bool isServer = MultiplayerRolesManager.ActiveMultiplayerRoleMask.HasFlag(MultiplayerRoleFlags.Server);

        if (isServer)
        {
            SceneManager.LoadScene("InGame");
            return;
        }

        UpdateStatus("Ready to find match");
        UpdateLobbyInfo("");
        SetLoading(false);

        _findMatchButton.onClick.AddListener(OnFindMatchButtonClicked);
        _cancelMatchButton.onClick.AddListener(OnCancelMatchButtonClicked);

        if (client != null)
        {
            client.OnLobbyUpdated += OnLobbyUpdated;
            client.OnMatchAllocated += OnMatchAllocated;
            client.OnError += OnError;
            client.OnConnected += OnConnected;
            client.OnDisconnected += OnDisconnected;

            _ = ConnectToServerAsync();
        }
    }

    private async Task ConnectToServerAsync()
    {
        UpdateStatus("Connecting to server...");

        bool success = await client.ConnectAsync();

        if (success)
        {
            await ReportPendingMatchCompletionAsync();
            UpdateStatus("Connected! Click 'Find Match' to start");
        }
        else
        {
            UpdateStatus("Failed to connect to server");
        }
    }


    private async Task ReportPendingMatchCompletionAsync()
    {
        int pending = PlayerPrefs.GetInt("PendingMatchCompletion", 0);
        int lobbyId = PlayerPrefs.GetInt("LobbyId", 0);

        if (pending != 1 || lobbyId <= 0)
        {
            return;
        }

        UpdateStatus($"Finalizing previous match (Lobby #{lobbyId})...");

        bool completed = await client.CompleteMatchAsync(lobbyId);
        if (completed)
        {
            PlayerPrefs.SetInt("PendingMatchCompletion", 0);
            PlayerPrefs.SetInt("LobbyId", 0);
            PlayerPrefs.Save();
            Debug.Log($"[MatchmakingView] Previous lobby {lobbyId} completion reported");
        }
        else
        {
            Debug.LogWarning($"[MatchmakingView] Failed to finalize previous lobby {lobbyId}");
        }
    }

    private async void OnFindMatchButtonClicked()
    {
        UpdateStatus("Searching for match...");
        UpdateLobbyInfo("");
        SetLoading(true);

        var result = await client.StartMatchmakingAsync();

        if (result != null && result.Success)
        {
            _matchResult = result;
            UpdateStatus($"Joined lobby #{result.LobbyId}");
        }
        else
        {
            UpdateStatus("Failed to find match");
            _findMatchButton.interactable = true;
        }
    }

    private async void OnCancelMatchButtonClicked()
    {
        UpdateStatus("Cancelling matchmaking...");

        bool success = await client.CancelMatchmakingAsync();
        if (success)
        {
            UpdateStatus("Matchmaking cancelled");
        }
        else
        {
            UpdateStatus("Failed to cancel matchmaking");
        }

        SetLoading(false);
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
            return;
        }

        PlayerPrefs.SetString("GameServerIP", result.GameServerIP);
        PlayerPrefs.SetInt("GameServerPort", result.GameServerPort);
        PlayerPrefs.SetInt("LobbyId", result.LobbyId);
        PlayerPrefs.SetInt("PendingMatchCompletion", 1);
        PlayerPrefs.Save();

        Debug.Log($"[MatchmakingView] Loading game with server: {result.GameServerIP}:{result.GameServerPort}");

        UpdateStatus("Match found! Loading game...");
        UpdateLobbyInfo($"Lobby #{result.LobbyId} - Ready!");

        if (client != null)
        {
            client.OnLobbyUpdated -= OnLobbyUpdated;
            client.OnMatchAllocated -= OnMatchAllocated;
            client.OnError -= OnError;
            client.OnConnected -= OnConnected;
            client.OnDisconnected -= OnDisconnected;

            _ = client.DisconnectAsync();
        }

        try
        {
            SceneManager.LoadScene("InGame");
            Debug.Log("[MatchmakingView] Scene load initiated");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MatchmakingView] Failed to load scene: {e.Message}");
            UpdateStatus($"Error loading scene: {e.Message}");
        }
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
        Debug.Log("[UI] Connected to server");
        UpdateStatus("Connected! Click 'Find Match' to start");
        _findMatchButton.interactable = true;
    }

    private void OnDisconnected()
    {
        Debug.LogWarning("[UI] Disconnected from server");
        UpdateStatus("Disconnected from server. Reconnecting...");
        _findMatchButton.interactable = false;
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[Status] {message}");
    }

    private void UpdateLobbyInfo(string info)
    {
        if (lobbyInfoText != null)
        {
            lobbyInfoText.text = info;
        }
    }

    private void SetLoading(bool isLoading)
    {
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(isLoading);
        }
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
        {
            _findMatchButton.onClick.RemoveListener(OnFindMatchButtonClicked);
        }
    }
}
