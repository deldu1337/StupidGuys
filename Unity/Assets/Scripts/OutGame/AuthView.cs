using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



public class AuthView : MonoBehaviour
{
    [SerializeField] TMP_InputField _id;
    [SerializeField] TMP_InputField _pw;

    [Header("Buttons")]
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _registerButton; // 회원가입 버튼 추가

    // 2. 컨트롤러가 구독할 이벤트 (Action)
    [SerializeField] Button _confirm;
    public event Action<string, string> onConfirm;
    public Action<string, string> onRegister; // 회원가입용 (신규)

    [SerializeField] GameObject _alertPanel;
    [SerializeField] private TMP_Text _alertText;




    private void Awake()
    {
        // 버튼 클릭 리스너 등록
        _loginButton.onClick.AddListener(OnLoginClicked);
        _registerButton.onClick.AddListener(OnRegisterClicked);

        if (_alertPanel != null)
            _alertPanel.SetActive(false);
    }

    /// <summary>
    ///  회원가입 버튼 클릭 시 호출
    /// </summary>
    private void OnRegisterClicked()
    {
        if (ValidateInput())
        {
            onRegister?.Invoke(_id.text, _pw.text);
        }
    }

    /// <summary>
    ///  로그인 버튼 클릭 시 호출
    /// </summary>
    private void OnLoginClicked()
    {

        if (ValidateInput())
        {
            onConfirm?.Invoke(_id.text, _pw.text);
        }
    }

    /// <summary>
    /// 입력값 기초 검증
    /// </summary>
    /// <returns></returns>
    private bool ValidateInput()
    {
        if (string.IsNullOrEmpty(_id.text) || string.IsNullOrEmpty(_pw.text))
        {
            ShowAlertPanel("아이디와 비밀번호를 입력해주세요.");
            return false;
        }
        return true;
    }

    public void SetLoginInteractables(bool interactable)
    {
        _id.interactable = interactable;
        _pw.interactable = interactable;
        _confirm.interactable = interactable;
        _registerButton.interactable = interactable;
    }

    public void ShowAlertPanel(string content)
    {
        _alertPanel.GetComponentInChildren<TextMeshProUGUI>().text = content;
        _alertPanel.SetActive(true);
    }

    public void HideAlertPanel()
    {
        _alertPanel.SetActive(false);
    }
}
