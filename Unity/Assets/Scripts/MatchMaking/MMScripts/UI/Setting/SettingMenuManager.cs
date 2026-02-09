//using System.Text;
//using System.Collections;
//using Unity.Netcode;
//using UnityEngine;
//using UnityEngine.Networking;

//public class SettingMenuManager : MonoBehaviour
//{
//    [SerializeField] private GameObject _settingCanvas;
//    [SerializeField] private GameObject _settingButton;
//    [SerializeField] private GameObject _exitButton;

//    private bool _isSettingMenuOpen = false;
//    const string BASE_URL = "http://3.37.215.9:5000";

//    //const string BASE_URL = "http://localhost:7018";

//    private void Update()
//    {
//        if (Input.GetKeyDown(KeyCode.Escape)) SettingMenu(_isSettingMenuOpen);
//    }

//    private void SettingMenu(bool _isOpen)
//    {
//        _isSettingMenuOpen = !_isOpen;
//        _settingCanvas.SetActive(_isSettingMenuOpen);
//    }

//    public void OnSettingButtonClicked()
//    {
//        SettingMenu(_isSettingMenuOpen);
//    }

//    public void OnExitButtonClicked()
//    {
//        StartCoroutine(C_LogoutAndQuit());
//    }

//    private IEnumerator C_LogoutAndQuit()
//    {
//        Debug.Log("=== Logout process started ===");

//        string username = NetworkBlackboard.userName;
//        Debug.Log($"Username from NetworkBlackboard: {username}");

//        if (!string.IsNullOrEmpty(username))
//        {
//            Debug.Log("Starting logout coroutine...");
//            yield return StartCoroutine(C_Logout(username));
//        }
//        else
//        {
//            Debug.LogWarning("Username is null or empty, skipping logout API call");
//        }

//        try
//        {
//            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
//            {
//                Debug.Log("Shutting down NetworkManager...");
//                NetworkManager.Singleton.Shutdown();
//            }
//        }
//        catch (System.Exception e)
//        {
//            Debug.LogWarning($"NetworkManager shutdown failed: {e.Message}");
//        }

//        // 3. ���� �α��� ���� �ʱ�ȭ
//        NetworkBlackboard.userId = null;
//        NetworkBlackboard.userName = null;
//        Debug.Log("NetworkBlackboard cleared");

//        Debug.Log("Quitting game...");

//#if UNITY_EDITOR
//        UnityEditor.EditorApplication.isPlaying = false;
//#else
//    Application.Quit();
//#endif
//    }

//    private IEnumerator C_Logout(string userId)
//    {
//        var logoutDto = new LogoutRequest { id = userId };
//        string json = JsonUtility.ToJson(logoutDto);

//        using (UnityWebRequest request = new UnityWebRequest($"{BASE_URL}/auth/logout", "POST"))
//        {
//            byte[] body = Encoding.UTF8.GetBytes(json);
//            request.uploadHandler = new UploadHandlerRaw(body);
//            request.downloadHandler = new DownloadHandlerBuffer();
//            request.SetRequestHeader("Content-Type", "application/json");

//            yield return request.SendWebRequest();

//            if (request.result == UnityWebRequest.Result.Success)
//            {
//                Debug.Log("Logout successful");
//            }
//            else
//            {
//                Debug.LogWarning($"Logout failed: {request.error}");
//            }
//        }
//    }

//    [System.Serializable]
//    public class LogoutRequest
//    {
//        public string id;
//    }
//}

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

    const string BASE_URL = "http://3.37.215.9:5000";
    // const string BASE_URL = "http://localhost:7018";

    // X 버튼 등 “앱 종료 시도”가 중복으로 들어오는 것 방지
    private bool _isQuittingFlowRunning = false;

    // wantsToQuit에서 막아놓고, 우리가 정리 끝난 후 진짜 Quit할 때 사용
    private bool _forceQuit = false;

    private void Awake()
    {
        // 윈도우 X 버튼, Alt+F4, OS 종료 등 "quit 시도"를 가로챔
        Application.wantsToQuit += OnWantsToQuit;
    }

    private void OnDestroy()
    {
        Application.wantsToQuit -= OnWantsToQuit;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SettingMenu(_isSettingMenuOpen);
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
        // 버튼 클릭도 동일한 종료 플로우로 통일
        StartQuitFlow();
    }

    /// <summary>
    /// 윈도우 X 버튼, Alt+F4 등 앱 종료 이벤트가 들어올 때 호출됨
    /// true를 리턴하면 Unity가 종료를 진행하고,
    /// false면 종료를 막는다.
    /// </summary>
    private bool OnWantsToQuit()
    {
        // 우리가 정리 끝내고 "진짜 종료" 시도할 때는 통과
        if (_forceQuit)
            return true;

        // 이미 종료 플로우 실행 중이면 종료 막고 기다림
        if (_isQuittingFlowRunning)
            return false;

        // 여기서 종료를 잠깐 막고, 코루틴으로 로그아웃/정리 후 Quit을 다시 호출
        StartQuitFlow();
        return false;
    }

    private void StartQuitFlow()
    {
        if (_isQuittingFlowRunning)
            return;

        _isQuittingFlowRunning = true;
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

        // 로그인 정보 초기화
        NetworkBlackboard.userId = null;
        NetworkBlackboard.userName = null;
        Debug.Log("NetworkBlackboard cleared");

        Debug.Log("Quitting game...");

        // wantsToQuit를 통과시키기 위한 플래그
        _forceQuit = true;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif

        yield break;
    }

    private IEnumerator C_Logout(string userId)
    {
        var logoutDto = new LogoutRequest { id = userId };
        string json = JsonUtility.ToJson(logoutDto);

        using (UnityWebRequest request = new UnityWebRequest($"{BASE_URL}/auth/logout", "POST"))
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
