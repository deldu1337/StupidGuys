using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // 타이머 표시용 남은 시간이며 실제 계산은 서버 기준 시간을 사용한다
    public float limitTime = 180;

    // 타이머 텍스트 컴포넌트
    [SerializeField] Text textTimer;

    // 라운드 종료 연출 오브젝트
    [SerializeField] GameObject roundOver;

    // 성공 연출 오브젝트
    [SerializeField] GameObject success;

    // 실패 연출 오브젝트
    [SerializeField] GameObject failure;

    // 현재 도착 인원 값이며 서버의 CurRank와 동기화된다
    public int curRank { get; set; }

    // 현재 도착 인원 표시 텍스트
    public Text curRankUI;

    // 분모 표시 텍스트 예를 들면  / 2
    public Text headCountRankUI;

    // 싱글톤 접근용 인스턴스
    public static UIManager Instance;

    // CurRank 변경 이벤트를 한 번만 구독하기 위한 플래그
    private bool hookedRank = false;

    // 분 단위 표시용
    int min;

    // 초 단위 표시용
    float sec;

    // 종료 연출 시작 전 대기 시간
    float waitTime = 2f;

    // 종료 연출 타이머
    float curretTime = 0f;

    // 총 인원 수이며 HeadCount 분모로 사용한다
    private int totalHeadCount = 0;

    // 라운드가 종료되었는지 여부
    private bool roundEnded = false;

    // 내 로컬 플레이어가 골인했는지 여부이며 한 번 true가 되면 유지한다
    private bool localIsGoal = false;

    // 결과 UI를 한 번만 보여주기 위한 플래그
    private bool resultShown = false;

    // 씬 로드를 한 번만 수행하기 위한 플래그
    private bool sceneLoading = false;

    /// <summary>
    /// 싱글톤 인스턴스를 초기화한다
    /// </summary>
    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 라운드 시작 시 UI 상태를 초기화하고 HeadCount 텍스트를 캐시한다
    /// </summary>
    void Start()
    {
        if (roundOver) roundOver.SetActive(false);
        if (success) success.SetActive(false);
        if (failure) failure.SetActive(false);

        curRank = 0;
        curretTime = 0f;

        roundEnded = false;
        localIsGoal = false;
        resultShown = false;
        sceneLoading = false;

        CacheHeadCountTextIfNeeded();
    }

    /// <summary>
    /// 서버 CurRank 변화 구독을 연결하고
    /// 라운드 종료 여부에 따라 종료 연출 또는 타이머 갱신을 수행한다
    /// </summary>
    void Update()
    {
        if (!hookedRank && InGameManager.singleton != null)
        {
            InGameManager.singleton.CurRank.OnValueChanged += OnRankChanged;
            OnRankChanged(0, InGameManager.singleton.CurRank.Value);
            hookedRank = true;
        }

        if (roundEnded)
        {
            curretTime += Time.deltaTime;
            HandleRoundEndUI();
            return;
        }

        if (InGameManager.singleton != null && InGameManager.singleton.IsFrozen.Value)
            return;

        UpdateTimerFromServerTime();
    }

    /// <summary>
    /// 오브젝트 파괴 시 CurRank 변경 이벤트 구독을 해제한다
    /// </summary>
    private void OnDestroy()
    {
        if (hookedRank && InGameManager.singleton != null)
            InGameManager.singleton.CurRank.OnValueChanged -= OnRankChanged;
    }

    /// <summary>
    /// 서버의 도착 인원 값이 변경될 때 UI를 갱신하고
    /// 총 인원에 도달했는지 확인한다
    /// </summary>
    private void OnRankChanged(int prev, int next)
    {
        curRank = next;

        if (curRankUI != null)
            curRankUI.text = next.ToString();

        CheckEndByHeadCount();
    }

    /// <summary>
    /// headCountRankUI가 비어있을 경우 씬에서 HeadCount 오브젝트를 찾아 Text를 캐시한다
    /// </summary>
    private void CacheHeadCountTextIfNeeded()
    {
        if (headCountRankUI != null) return;

        var go = GameObject.Find("HeadCount");
        if (go != null)
            headCountRankUI = go.GetComponent<Text>();
    }

    /// <summary>
    /// 총 인원 값을 저장하고 HeadCount 분모 텍스트를 갱신한다
    /// 값이 갱신되는 순간에도 종료 조건을 즉시 확인한다
    /// </summary>
    public void SetHeadCountTotal(int totalPlayers)
    {
        totalHeadCount = Mathf.Max(0, totalPlayers);

        CacheHeadCountTextIfNeeded();
        if (headCountRankUI != null)
            headCountRankUI.text = " / " + totalHeadCount;

        CheckEndByHeadCount();
    }

    /// <summary>
    /// 로컬 플레이어의 골인 여부를 저장한다
    /// 한 번 true가 되면 이후 false로 되돌리지 않는다
    /// </summary>
    public void SetLocalGoal(bool isGoal)
    {
        if (isGoal) localIsGoal = true;
    }

    /// <summary>
    /// 도착 인원이 총 인원에 도달했는지 확인하고 도달했다면 라운드를 종료한다
    /// </summary>
    private void CheckEndByHeadCount()
    {
        if (roundEnded) return;
        if (totalHeadCount <= 0) return;

        if (curRank >= totalHeadCount)
            EndRoundOnce();
    }

    /// <summary>
    /// 라운드 종료를 한 번만 수행하고 종료 UI 상태를 초기화한다
    /// </summary>
    private void EndRoundOnce()
    {
        if (roundEnded) return;

        roundEnded = true;
        curretTime = 0f;
        resultShown = false;
        sceneLoading = false;

        if (roundOver) roundOver.SetActive(true);
        if (success) success.SetActive(false);
        if (failure) failure.SetActive(false);
    }

    /// <summary>
    /// 서버 시간 기준으로 남은 시간을 계산해 타이머 UI에 표시한다
    /// 시간이 0이 되면 라운드를 종료한다
    /// </summary>
    void UpdateTimerFromServerTime()
    {
        if (InGameManager.singleton == null)
            return;

        limitTime = InGameManager.singleton.GetRemainingTimeSeconds();

        if (limitTime > 0f)
        {
            if (limitTime >= 60f)
            {
                min = (int)limitTime / 60;
                sec = limitTime % 60;
                textTimer.text = min + " : " + (int)sec;
            }
            else if (limitTime < 10f)
            {
                textTimer.text = "<color=red>" + (int)limitTime + "</color>";
            }
            else
            {
                textTimer.text = "<color=white>" + (int)limitTime + "</color>";
            }
        }
        else
        {
            textTimer.text = "<color=red>Time Over</color>";
            EndRoundOnce();
        }
    }

    /// <summary>
    /// 라운드 종료 후 일정 시간에 맞춰 roundOver를 숨기고 성공 또는 실패를 표시한 뒤
    /// 더 시간이 지나면 보상 씬으로 이동한다
    /// </summary>
    private void HandleRoundEndUI()
    {
        if (curretTime <= waitTime)
            return;

        if (!resultShown && curretTime > 3f)
        {
            resultShown = true;

            if (roundOver) roundOver.SetActive(false);

            if (localIsGoal)
            {
                if (success) success.SetActive(true);
            }
            else
            {
                if (failure) failure.SetActive(true);
            }
        }

        if (!sceneLoading && curretTime > 5f)
        {
            sceneLoading = true;
            SceneManager.LoadScene("StupidGuysRewardScene");
        }
    }

    /// <summary>
    /// 외부에서 게임 오버를 호출할 수 있도록 남겨둔 호환용 함수이며
    /// 골인 여부를 기록한 뒤 라운드를 종료 처리한다
    /// </summary>
    public void GameOver(bool isGoal)
    {
        if (isGoal) localIsGoal = true;
        EndRoundOnce();
    }
}
