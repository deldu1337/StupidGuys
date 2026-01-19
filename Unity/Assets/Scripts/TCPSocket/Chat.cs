using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 채팅 UI를 화면에 출력하고 스크롤을 자동으로 아래로 내리는 역할을 한다
/// 다른 스크립트에서 Chat instance ShowMessage 형태로 메시지를 추가할 수 있다
/// </summary>
public class Chat : MonoBehaviour
{
    // 전역에서 접근하기 위한 싱글턴 인스턴스
    public static Chat instance;

    // 입력창
    public InputField SendInput;

    // 채팅 내용이 들어갈 컨텐츠 영역
    public RectTransform ChatContent;

    // 실제 채팅 문자열을 표시할 텍스트
    public Text ChatText;

    // 스크롤 영역 제어용
    public ScrollRect ChatScrollRect;

    /// <summary>
    /// 씬에서 이 오브젝트가 생성될 때 인스턴스를 등록한다
    /// </summary>
    void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// 채팅 메시지를 화면에 추가로 표시하고 레이아웃과 스크롤을 갱신한다
    /// </summary>
    public void ShowMessage(string data)
    {
        // 기존 텍스트가 비어있으면 그대로 넣고 비어있지 않으면 줄바꿈 후 추가한다
        ChatText.text += ChatText.text == "" ? data : "\n" + data;

        // 텍스트 영역과 컨텐츠 영역의 레이아웃을 즉시 다시 계산한다
        Fit(ChatText.GetComponent<RectTransform>());
        Fit(ChatContent);

        // 레이아웃 갱신 이후에 스크롤을 내리기 위해 약간 지연 호출한다
        Invoke("ScrollDelay", 0.03f);
    }

    /// <summary>
    /// 유니티 레이아웃 시스템을 즉시 갱신하여 크기 계산이 바로 반영되게 한다
    /// </summary>
    void Fit(RectTransform Rect)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
    }

    /// <summary>
    /// 스크롤바 값을 가장 아래로 이동시켜 최신 메시지가 보이도록 한다
    /// </summary>
    void ScrollDelay()
    {
        ChatScrollRect.verticalScrollbar.value = 0;
    }
}
