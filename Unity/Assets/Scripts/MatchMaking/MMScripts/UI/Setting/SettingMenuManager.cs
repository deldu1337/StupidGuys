using System.Text;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public class SettingMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject _settingCanvas;
    [SerializeField] private GameObject _settingButton;
    [SerializeField] private GameObject _exitButton;

    private bool _isSettingMenuOpen = false;
    
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) SettingMenu(_isSettingMenuOpen);
    }

    private void SettingMenu(bool _isOpen)
    {
        _isSettingMenuOpen = !_isOpen;
        _settingCanvas.SetActive(_isSettingMenuOpen);
    }

    public void OnSettingButtonClicked()
    {
        SettingMenu(_isSettingMenuOpen);
    }

    public void OnExitButtonClicked()
    {
        StartCoroutine(C_LogoutAndQuit());
    }

    private IEnumerator C_LogoutAndQuit()
    {
        Debug.Log("=== Logout process started ===");

        string username = NetworkBlackboard.userName;
        Debug.Log($"Username from NetworkBlackboard: {username}");

        if (!string.IsNullOrEmpty(username))
        {
            Debug.Log("Starting logout coroutine...");
            yield return StartCoroutine(C_Logout(username));
        }
        else
        {
            Debug.LogWarning("Username is null or empty, skipping logout API call");
        }

        try
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.Log("Shutting down NetworkManager...");
                NetworkManager.Singleton.Shutdown();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"NetworkManager shutdown failed: {e.Message}");
        }

        // 3. ���� �α��� ���� �ʱ�ȭ
        NetworkBlackboard.userId = null;
        NetworkBlackboard.userName = null;
        Debug.Log("NetworkBlackboard cleared");

        Debug.Log("Quitting game...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }

    private IEnumerator C_Logout(string userId)
    {
        var logoutDto = new LogoutRequest { id = userId };
        string json = JsonUtility.ToJson(logoutDto);

        using (UnityWebRequest request = new UnityWebRequest($"{AuthServerConfig.BaseUrl}/auth/logout", "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Logout successful");
            }
            else
            {
                Debug.LogWarning($"Logout failed: {request.error}");
            }
        }
    }

    [System.Serializable]
    public class LogoutRequest
    {
        public string id;
    }
}
