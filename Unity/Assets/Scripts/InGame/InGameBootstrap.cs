using Microsoft.Playfab.Gaming.GSDK.CSharp;
using System.Collections.Generic;
using Unity.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System.Linq;

public class InGameBootstrap : MonoBehaviour
{
    private bool _isPlayFabServer = false;

    private void Start()
    {
#if UNITY_EDITOR
        bool isClient = MultiplayerRolesManager.ActiveMultiplayerRoleMask.HasFlag(MultiplayerRoleFlags.Client);
        bool isServer = MultiplayerRolesManager.ActiveMultiplayerRoleMask.HasFlag(MultiplayerRoleFlags.Server);

        Debug.Log($"[Bootstrap] (Editor) isServer: {isServer}, isClient: {isClient}");

        if (isServer && !isClient)
        {
            StartPlayFabServer();
        }
        else if (isClient)
        {
            StartClient();
        }
#else
    Debug.Log($"[Bootstrap] (Build) isBatchMode: {Application.isBatchMode}");

    if (Application.isBatchMode)
    {
        StartPlayFabServer();
    }
    else
    {
        StartClient();
    }
#endif
    }

    private void StartClient()
    {
        string serverIP = PlayerPrefs.GetString("GameServerIP", "127.0.0.1");
        int serverPort = PlayerPrefs.GetInt("GameServerPort", 7777);

        Debug.Log($"[Bootstrap] Starting client: {serverIP}:{serverPort}");

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(serverIP, (ushort)serverPort);
        NetworkManager.Singleton.StartClient();
    }

    private void StartPlayFabServer()
    {
        Debug.Log("[Bootstrap] Starting PlayFab Server...");

        if (Application.isBatchMode)
        {
            try
            {
                Debug.Log("[PlayFab] Initializing GSDK...");
                GameserverSDK.Start();
                Debug.Log("[PlayFab] ✓ GSDK initialized successfully");

                var connectionInfo = GameserverSDK.GetGameServerConnectionInfo();
                var gamePort = connectionInfo.GamePortsConfiguration.FirstOrDefault(p => p.Name == "game_port");

                if (gamePort == null)
                {
                    Debug.LogError("[PlayFab] game_port not found!");
                    return;
                }

                ushort port = (ushort)gamePort.ServerListeningPort;
                Debug.Log($"[PlayFab] Assigned port: {port}");

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetConnectionData("0.0.0.0", port);

                _isPlayFabServer = true;

                bool started = NetworkManager.Singleton.StartServer();
                Debug.Log($"[Bootstrap] NetworkManager.StartServer() result: {started}");

                if (started)
                {
                    GameserverSDK.ReadyForPlayers();
                    Debug.Log("[PlayFab] Server is ready for players!");
                    InvokeRepeating(nameof(SendHeartbeat), 1f, 1f);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayFab] ✗ GSDK initialization failed: {ex.Message}");
                Debug.LogError($"[PlayFab] Stack: {ex.StackTrace}");
            }
        }
        else
        {
            Debug.Log("[Bootstrap] Not in BatchMode, starting local server");
            NetworkManager.Singleton.StartServer();
        }
    }

    private void SendHeartbeat()
    {
        if (!_isPlayFabServer) return;

        try
        {
            var players = new List<ConnectedPlayer>();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    players.Add(new ConnectedPlayer(clientId.ToString()));
                }
            }

            GameserverSDK.UpdateConnectedPlayers(players);
            Debug.Log($"[PlayFab] Heartbeat: {players.Count} players");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PlayFab] Heartbeat failed: {ex.Message}");
        }
    }
}   