using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerController : CharacterController
{
    [HideInInspector] public Vector3 respawnPosition;
    [HideInInspector] public GameObject destination;

    private bool isGoal = false;

    private NetworkObject netObj;
    private bool IsLocalPlayer => netObj == null || netObj.IsOwner;
    private bool goalReported = false;

    float currentTime;
    float limitTime = 1.0f;
    float limitValue = 3.0f;
    float moveValue = 1.0f;

    AudioSource playerAudio;
    [SerializeField] AudioClip jumpSFX;

    // 수평 입력 값
    public override float horizontal
    {
        get => IsLocalPlayer ? Input.GetAxis("Horizontal") : 0f;
        set { }
    }

    // 수직 입력 값
    public override float vertical
    {
        get => IsLocalPlayer ? Input.GetAxis("Vertical") : 0f;
        set { }
    }

    // 이동 애니메이션 블렌딩 가중치 및 이동 강도에 쓰는 값
    public override float moveGain
    {
        get => moveValue;
        set { }
    }

    // 이 스크립트에서 필요한 컴포넌트를 캐시하고 리스폰 기준 위치를 저장한다
    void Start()
    {
        netObj = GetComponent<NetworkObject>();

        respawnPosition = transform.position;
        playerAudio = GetComponent<AudioSource>();
    }

    // 로컬 플레이어 입력을 읽고 상태 전환 및 이동 가중치를 갱신한다
    protected override void Update()
    {
        base.Update();

        if (!IsLocalPlayer)
            return;

        // 카운트다운/정지 상태면 로컬 입력 기반 상태 전환을 전부 막는다
        if (InGameManager.singleton != null && InGameManager.singleton.IsFrozen.Value)
        {
            MoveValueInit(); // moveGain 관련 값 정리
            return;
        }

        // W 키를 오래 누르면 moveValue를 점차 올려 달리기처럼 보이게 한다
        if (Input.GetKey(KeyCode.W))
        {
            currentTime += Time.deltaTime;

            if (currentTime > limitTime)
            {
                if (moveValue >= limitValue)
                {
                    moveValue = limitValue;
                }
                else
                {
                    moveValue = Mathf.Lerp(1.0f, limitValue, (currentTime - limitTime) * 2f);
                }
            }
        }

        // W 키를 떼면 기본 이동 값으로 복귀한다
        if (Input.GetKeyUp(KeyCode.W))
        {
            moveValue = 1.0f;
            currentTime = 0f;
        }

        // 점프 입력 처리
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded && _animator.GetInteger("state") != (int)State.Slide)
            {
                ChangeState(State.Jump);

                if (playerAudio != null && jumpSFX != null)
                    playerAudio.PlayOneShot(jumpSFX);
            }
        }

        // 왼쪽 마우스 버튼으로 슬라이드 상태로 강제 전환한다
        if (Input.GetMouseButtonDown(0))
        {
            if (_animator.GetInteger("state") != (int)State.Slide)
            {
                ChangeStateForcely(State.Slide);

                if (playerAudio != null && jumpSFX != null)
                    playerAudio.PlayOneShot(jumpSFX);
            }
        }

        // 오른쪽 마우스 버튼을 누르고 있는 동안 잡기 상태로 전환한다
        if (Input.GetMouseButton(1))
        {
            if (isGrounded)
            {
                moveValue = 2f;
                ChangeState(State.Grab);
            }
        }

        // 오른쪽 마우스 버튼을 누르는 순간 이동 값을 올려준다
        if (Input.GetMouseButtonDown(1))
        {
            if (isGrounded)
            {
                moveValue = 2f;
            }
        }

        // 오른쪽 마우스 버튼을 떼면 이동 상태로 복귀한다
        if (Input.GetMouseButtonUp(1))
        {
            if (isGrounded)
            {
                ChangeState(State.Move);
            }
        }

        // 제한 시간이 끝나면 게임 오버 처리한다
        if (UIManager.Instance != null && UIManager.Instance.limitTime <= 0)
        {
            UIManager.Instance.GameOver(isGoal);
        }
    }

    // 이동 가중치와 누적 시간을 초기화한다
    public void MoveValueInit()
    {
        moveValue = 1.0f;
        currentTime = 0f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsOwner)
            return;

        if (!collision.gameObject.CompareTag("Destination"))
            return;

        // 로컬에서도 1회만 신고
        if (goalReported)
            return;

        goalReported = true;
        isGoal = true;

        // 로컬 UI에 골인 확정
        if (UIManager.Instance != null)
            UIManager.Instance.SetLocalGoal(true);


        ReportGoalServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ReportGoalServerRpc()
    {
        if (InGameManager.singleton != null)
            InGameManager.singleton.Server_RegisterGoal(OwnerClientId);
    }
}
