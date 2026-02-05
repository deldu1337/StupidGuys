using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Multiplayer;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;



public class AuthController : MonoBehaviour
{
    public string jwt => _jwt;
    public bool isLoggedIn => string.IsNullOrEmpty(_jwt) == false;

    //const string BASE_URL = "https://localhost:7018";
    const string BASE_URL = "http://3.37.215.9:5000";
    string _jwt;
    string _userId;

    [SerializeField] AuthView _view;

    private void Awake()
    {
        bool isServer = MultiplayerRolesManager.ActiveMultiplayerRoleMask
            .HasFlag(MultiplayerRoleFlags.Server);

        if (isServer)
        {
            SceneManager.LoadScene("InGame");
            return;
        }
    }

    private void OnEnable()
    {
        // 뷰(UI)에 로그인 버튼과 회원가입 버튼 이벤트 연결
        if (_view != null)
        {
            _view.onConfirm += Login;       // 기존 로그인 버튼
            _view.onRegister += Register;   // (추가 필요) 회원가입 버튼 이벤트
        }
    }



    private void OnDisable()
    {
        if (_view != null)
        {
            _view.onConfirm -= Login;
            _view.onRegister -= Register;
        }
    }

    void Login(string id, string pw)
    {
        StartCoroutine(C_Login(id, pw));
    }

    IEnumerator C_Login(string id, string pw)
    {
        _view.SetLoginInteractables(false);

        // 서버의 LoginDTO가 무엇인지에 따라 변수명(Id, Pw)을 맞춰야 함
        var loginDto = new LoginRequest { id = id, pw = pw };
        string json = JsonUtility.ToJson(loginDto);
        bool success = false;

        using (UnityWebRequest request = new UnityWebRequest($"{BASE_URL}/auth/login", "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            // HTTPS 인증서 문제 해결 (개발 단계용)
            request.certificateHandler = new BypassCertificateHandler();

            Debug.Log($"[Auth] Login request => {request.url}, body: {json}");
            yield return request.SendWebRequest();
            Debug.Log($"[Auth] Login response <= code: {request.responseCode}, result: {request.result}, error: {request.error}, body: {request.downloadHandler.text}");

            //string responseText = request.downloadHandler.text;

            //// [가정] 서버가 중복일 때 JSON 안에 "error"나 "already"라는 단어를 포함한다면
            //if (responseText.Contains("already_active") || responseText.Contains("Conflict"))
            //{
            //    _view.ShowAlertPanel("This account is already logged in.");
            //    success = false; // 로비 이동 방지
            //}

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<LoginResponse>(request.downloadHandler.text);
                    _jwt = response.Jwt;
                    _userId = response.UserId;
                    NetworkBlackboard.jwt = _jwt;
                    NetworkBlackboard.userId = _userId.ToString();
                    NetworkBlackboard.userName = id;
                    NetworkBlackboard.skinIndex = response.SkinIndex;
                    LocalSkinSelection.Set(response.SkinIndex);
                    success = true;
                    _view.ShowAlertPanel("Logged In !");

                }
                catch (Exception e)
                {
                    _view.ShowAlertPanel($"Wrong response{e}");
                    //success = false;
                }
            }
            else if (request.responseCode == 409) // 중복 로그인 조건
            {
                _view.ShowAlertPanel("This account is already logged in.");
                Debug.LogWarning("This account is already logged in.");
            }
            else if (request.responseCode == 401) // 비밀번호 틀림 등 인증 실패 
            {
                _view.ShowAlertPanel("Invalid ID  or Password");
            } 
            else
            {
                _view.ShowAlertPanel("Login Failed");
                Debug.LogError($"Login Error : {request.result} / Code: {request.responseCode}");
            }

        }

        yield return new WaitForSeconds(1f);

        if (success)
        {
            // 로그인 성공 시 로비로 이동
            yield return SceneManager.LoadSceneAsync("MatchMakingTestScene");
            // yield return SceneManager.LoadSceneAsync("MatchMakingTestScene", LoadSceneMode.Additive);
            yield return SceneManager.UnloadSceneAsync("Auth");
        }
        else
        {
            _view.HideAlertPanel();
            _view.SetLoginInteractables(true);
        }
    }

    // 2. 회원가입 

    public void Register(string id, string pw)
    {
        StartCoroutine(C_Register(id, pw));
    }

    IEnumerator C_Register(string id, string pw)
    {
        _view.SetLoginInteractables(false); // 버튼 비활성화

        // 중요: 서버의 CreateUserDTO와 필드명(username, password)이 정확히 일치해야 함
        var registerDto = new RegisterRequest { username = id, password = pw };
        string json = JsonUtility.ToJson(registerDto);

        // 주소: /user/create (UserController의 라우트와 일치)
        using (UnityWebRequest request = new UnityWebRequest($"{BASE_URL}/user/create", "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            // HTTPS 인증서 무시 (개발용)
            request.certificateHandler = new BypassCertificateHandler();

            Debug.Log($"[Auth] Register request => {request.url}, body: {json}");
            yield return request.SendWebRequest();
            Debug.Log($"[Auth] Register response <= code: {request.responseCode}, result: {request.result}, error: {request.error}, body: {request.downloadHandler.text}");

            if (request.result == UnityWebRequest.Result.Success) // 201 Created
            {
                Debug.Log("Sucess Register: " + request.downloadHandler.text);
                _view.ShowAlertPanel("Success Register! Enter LogIn");
            }
            else
            {
                // 409 Conflict (중복) 또는 400 Bad Request 처리
                if (request.responseCode == 409)
                {
                    _view.ShowAlertPanel("Bad Request");
                }
                else
                {
                    _view.ShowAlertPanel("Failed Register");
                    Debug.LogError($"가입 에러: {request.downloadHandler.text}");
                }
            }
        }

        yield return new WaitForSeconds(1.5f);
        _view.HideAlertPanel();
        _view.SetLoginInteractables(true);
    }

    [Serializable]
    public class LoginRequest
    {
        public string id;
        public string pw;
    }

    [Serializable]
    public class LoginResponse
    {
        [JsonProperty("jwt")]
        public string Jwt;
        [JsonProperty("userId")]
        public string UserId;
        [JsonProperty("nickname")]
        public string Nickname;
        [JsonProperty("skinIndex")]
        public int SkinIndex;
    }

    // [중요] UserController.cs의 CreateUserDTO와 변수명이 똑같아야 함 (대소문자 구분)
    [Serializable]
    public class RegisterRequest
    {
        public string username;
        public string password;
    }

    // 로비나 설정 창에서 계정 삭제를 추가하는것이 일반적
    [Serializable]
    public class DeleteRequest
    {
        public string UserId;
        public string Id;
        public string Pw;
    }

    // JWT 사용예시 (User API 등 에서 DeleteUser 와같이 소유자 권한이 필요한 요청에 사용)
    IEnumerator C_DeleteUser(string userId, string id, string pw)
    {
        // 1. 데이터 준비
        var deleteDto = new DeleteRequest { UserId = userId, Id = id, Pw = pw };
        string json = JsonUtility.ToJson(deleteDto);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        // 2. URL 설정
        string url = $"{BASE_URL}/user/{userId}";

        // 3. UnityWebRequest 생성 ("DELETE" 명시)
        using (UnityWebRequest request = new UnityWebRequest(url, "DELETE"))
        {
            // 4. 업로드/ 다운로드 핸들러 설정
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // 5. 헤더 설정 ( 중요 : JWT와 Content - Type)
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {_jwt}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    Debug.Log("Success Adress Delete!");
                    // 삭제했을 때 뜨는 함수 작성, 
                    // AlterPanel 에 확인 버튼을 추가해서 눌렀을 때  BackToTitle()이 뜨기.
                }
                catch (Exception e)
                {
                    Debug.LogError($"삭제 실패{request.responseCode}- {request.downloadHandler.text}{e}");
                    _view.ShowAlertPanel("삭제 실패: 인증 정보가 올바르지 않습니다.");
                }
            }
            else
            {
            }


        }
    }

    /// <summary>
    ///  계정삭제했을 때 뜨는 함수
    /// </summary>
    public void BackToTitle()
    {
        // 1. (선택) 저장된 로그인 데이터나 JWT 토큰 초기화
        _jwt = string.Empty;

        // 2. 타이틀(Auth) 씬으로 이동
        // "Auth"는 실제 유니티 프로젝트에 있는 씬 파일의 이름과 정확히 같아야 합니다.
        SceneManager.LoadScene("Auth", LoadSceneMode.Single);
    }

    // 개발용 HTTPS 인증서 무시 클래스 (필요 시 파일 하단에 추가)
    public class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true; // 보안상 실제 배포 시에는 제거해야 함
        }
    }
}
