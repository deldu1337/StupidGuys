using System;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public abstract class CharacterController : NetworkBehaviour
{
    // 입력 축 값은 자식 클래스에서 구현한다
    public virtual float horizontal { get; set; }
    public virtual float vertical { get; set; }
    public virtual float moveGain { get; set; }


    [Header("Move")]
    [SerializeField] private float groundSpeed = 10.0f;
    [SerializeField] private float airSpeed = 8f;
    [SerializeField] private float maxVelocityChange = 10.0f;
    [SerializeField] private float rotateSpeed = 25f;

    [Header("Gravity Jump")]
    [SerializeField] private float gravity = 10.0f;
    [SerializeField] private float jumpHeight = 2.0f;
    [SerializeField] private float maxFallSpeed = 20.0f;

    [Header("Ground")]
    [SerializeField] private float groundDetectRadius = 0.25f;
    [SerializeField] private LayerMask groundMask;

    [Header("Refs")]
    public Transform cinemachineCamera;

    protected Animator _animator;
    public StateLayerMaskData stateLayerMaskData;

    public State[] states;
    public State next;

    private Rigidbody rb;
    private float distToGround;

    private Vector3 moveDir;
    private bool localGrounded;

    private bool jumpRequested;

    private CinemachineCamera vcam;

    private readonly NetworkVariable<float> netH =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> netV =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> netMoveGain =
        new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<int> netState =
        new NetworkVariable<int>((int)State.Move, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> netGrounded =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // 로컬 플레이어는 로컬 바닥 판정을 쓰고 원격 플레이어는 네트워크 값을 사용한다
    public bool isGrounded => IsOwner ? localGrounded : netGrounded.Value;

    // summary
    // 컴포넌트를 캐시하고 리지드바디 기본 설정을 적용하며 바닥 판정에 필요한 값과 시네머신 카메라 참조를 준비한다
    protected virtual void Awake()
    {
        _animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        rb.freezeRotation = true;
        rb.useGravity = false;

        var col = GetComponent<Collider>();
        if (col != null)
            distToGround = col.bounds.extents.y;

        vcam = GetComponentInChildren<CinemachineCamera>(true);
        cinemachineCamera = vcam != null ? vcam.transform : null;
    }

    // summary
    // 네트워크 스폰 시 오너만 카메라를 활성화하고 애니메이션 상태 초기화와 상태 동기화 콜백을 연결한다
    public override void OnNetworkSpawn()
    {
        if (vcam != null)
            vcam.gameObject.SetActive(IsOwner);

        InitAnimatorBehaviours();
        InitStateArray();

        netState.OnValueChanged += OnNetStateChanged;

        ApplyState((State)netState.Value, true);
    }

    // summary
    // 네트워크 디스폰 시 카메라를 비활성화하고 상태 동기화 콜백을 해제한다
    public override void OnNetworkDespawn()
    {
        if (vcam != null)
            vcam.gameObject.SetActive(false);

        netState.OnValueChanged -= OnNetStateChanged;
    }

    // summary
    // 오너는 입력값과 바닥 상태를 네트워크 변수로 기록하고 애니메이터 파라미터를 갱신한다
    // 원격은 네트워크 변수로 받은 입력값으로 애니메이터만 갱신한다
    protected virtual void Update()
    {
        if (_animator == null)
            return;

        if (IsOwner)
        {
            // Frozen이면 입력/애니 파라미터를 0으로 고정해서 전 플레이어가 멈춘 것처럼 보이게 한다
            if (IsFrozenNow)
            {
                localGrounded = DetectGround();
                netGrounded.Value = localGrounded;

                netH.Value = 0f;
                netV.Value = 0f;
                netMoveGain.Value = 1f;

                _animator.SetFloat("h", 0f);
                _animator.SetFloat("v", 0f);

                // 점프 예약도 취소
                jumpRequested = false;
                return;
            }

            localGrounded = DetectGround();
            netGrounded.Value = localGrounded;

            float h = horizontal;
            float v = vertical;
            float g = moveGain;

            netH.Value = h;
            netV.Value = v;
            netMoveGain.Value = g;

            _animator.SetFloat("h", h * g);
            _animator.SetFloat("v", v * g);
        }
        else
        {
            _animator.SetFloat("h", netH.Value * netMoveGain.Value);
            _animator.SetFloat("v", netV.Value * netMoveGain.Value);
        }
    }

    // summary
    // 오너만 물리 이동을 수행한다
    // 카메라 기준 이동 방향을 계산하고 점프와 중력 및 지상 공중 이동을 리지드바디로 처리한다
    protected virtual void FixedUpdate()
    {
        if (!IsOwner)
            return;

        if (IsFrozenNow)
        {
            // 물리 이동/회전/중력 적용을 전부 멈춘다
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            jumpRequested = false;

            // 바닥 동기화는 유지(필요 없으면 지워도 됨)
            localGrounded = IsGroundedRigidbody();
            netGrounded.Value = localGrounded;
            return;
        }

        UpdateMoveDirFromCamera();

        bool grounded = IsGroundedRigidbody();

        if (cinemachineCamera != null)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, cinemachineCamera.eulerAngles.y, 0f);
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, rotateSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }

        if (jumpRequested && grounded)
        {
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(v.x, CalculateJumpVerticalSpeed(), v.z);
            jumpRequested = false;
            grounded = false;
        }

        float gMul = Mathf.Max(0f, moveGain);

        if (grounded)
        {
            Vector3 targetVelocity = moveDir * (groundSpeed * gMul);
            Vector3 velocity = rb.linearVelocity;

            Vector3 velocityChange = targetVelocity - velocity;
            velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange);
            velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
            velocityChange.y = 0f;

            rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }
        else
        {
            Vector3 targetVelocity = new Vector3(moveDir.x * (airSpeed * gMul), rb.linearVelocity.y, moveDir.z * (airSpeed * gMul));
            Vector3 velocity = rb.linearVelocity;

            Vector3 velocityChange = targetVelocity - velocity;
            velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange);
            velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);

            rb.AddForce(velocityChange, ForceMode.VelocityChange);

            if (velocity.y < -maxFallSpeed)
                rb.linearVelocity = new Vector3(velocity.x, -maxFallSpeed, velocity.z);
        }

        rb.AddForce(new Vector3(0f, -gravity * rb.mass, 0f));
        localGrounded = grounded;
        netGrounded.Value = localGrounded;
    }

    // summary
    // 입력 축 값을 카메라 기준 월드 방향으로 변환하여 이동 방향 벡터를 만든다
    private void UpdateMoveDirFromCamera()
    {
        float h = horizontal;
        float v = vertical;

        Transform camTr = cinemachineCamera != null ? cinemachineCamera : (Camera.main != null ? Camera.main.transform : null);

        if (camTr != null)
        {
            Vector3 forward = camTr.forward;
            Vector3 right = camTr.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            moveDir = (forward * v + right * h).normalized;
        }
        else
        {
            moveDir = new Vector3(h, 0f, v).normalized;
        }
    }

    // summary
    // 리지드바디 위치 기준으로 스피어 캐스트를 사용해 바닥 여부를 판정한다
    private bool IsGroundedRigidbody()
    {
        float radius = groundDetectRadius;
        Vector3 origin = rb.position + Vector3.up * 0.1f;
        return Physics.SphereCast(origin, radius, Vector3.down, out _, distToGround + 0.2f, groundMask);
    }

    // summary
    // 트랜스폼 위치 기준으로 오버랩스피어를 사용해 바닥 여부를 판정한다
    protected bool DetectGround()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, groundDetectRadius, groundMask);
        return cols.Length > 0;
    }

    // summary
    // 점프 높이와 중력을 이용해 점프 초기 수직 속도를 계산한다
    private float CalculateJumpVerticalSpeed()
    {
        return Mathf.Sqrt(2f * jumpHeight * gravity);
    }

    // summary
    // 애니메이터에 붙은 상태 머신 비헤이비어를 찾아 컨트롤러 참조와 레이어 마스크 데이터를 초기화한다
    protected virtual void InitAnimatorBehaviours()
    {
        if (_animator == null)
            return;

        StateBase[] behaviours = _animator.GetBehaviours<StateBase>();
        for (int i = 0; i < behaviours.Length; i++)
            behaviours[i].Init(this, stateLayerMaskData);
    }

    // summary
    // 애니메이터 레이어 수에 맞게 상태 배열을 생성한다
    protected virtual void InitStateArray()
    {
        Array layers = Enum.GetValues(typeof(AnimatorLayers));
        states = new State[layers.Length - 1];
    }

    // summary
    // 현재 특정 상태인지 확인한다
    // 상태가 매핑된 애니메이터 레이어를 순회하며 해당 레이어의 현재 상태와 비교한다
    public bool IsInState(State state)
    {
        int layerIndex = 0;
        foreach (AnimatorLayers layer in Enum.GetValues(typeof(AnimatorLayers)))
        {
            if (layer == AnimatorLayers.None)
                continue;

            if ((layer & stateLayerMaskData.animatorLayerPairs[state]) > 0)
            {
                if (states[layerIndex] == state)
                    return true;
            }

            layerIndex++;
        }

        return false;
    }

    // summary
    // 오너만 상태 변경을 요청할 수 있다
    // 네트워크 변수로 상태를 동기화하고 필요하면 점프 요청을 예약한다
    public void ChangeState(State newState)
    {
        if (!IsOwner)
            return;

        netState.Value = (int)newState;
        ApplyState(newState, false);

        if (newState == State.Jump)
            RequestJump();
    }

    // summary
    // 오너만 강제 상태 변경을 요청할 수 있다
    // 네트워크 변수로 상태를 동기화하고 필요하면 점프 요청을 예약한다
    public void ChangeStateForcely(State newState)
    {
        if (!IsOwner)
            return;

        netState.Value = (int)newState;
        ApplyState(newState, true);

        if (newState == State.Jump)
            RequestJump();
    }

    // summary
    // 다음 FixedUpdate에서 점프를 실행하도록 요청 플래그를 켠다
    protected void RequestJump()
    {
        if (!IsOwner)
            return;

        jumpRequested = true;
    }

    // summary
    // 네트워크로 상태 값이 바뀌었을 때 원격 오브젝트의 애니메이터 상태를 반영한다
    private void OnNetStateChanged(int prev, int nextValue)
    {
        ApplyState((State)nextValue, true);
    }

    // summary
    // 애니메이터의 state 파라미터와 레이어 가중치를 갱신하여 상태 전환을 적용한다
    // 강제 전환이면 더티 플래그를 켜서 전환을 보장한다
    private void ApplyState(State newState, bool forcely)
    {
        if (_animator == null)
            return;

        _animator.SetInteger("state", (int)newState);
        next = newState;

        int layerIndex = 0;
        foreach (AnimatorLayers layer in Enum.GetValues(typeof(AnimatorLayers)))
        {
            if (layer == AnimatorLayers.None)
                continue;

            if ((layer & stateLayerMaskData.animatorLayerPairs[newState]) > 0)
            {
                if (forcely || states[layerIndex] != newState)
                    _animator.SetBool($"dirty{layer}", true);

                _animator.SetLayerWeight(layerIndex, 1.0f);
            }
            else
            {
                _animator.SetLayerWeight(layerIndex, 0.0f);
            }

            layerIndex++;
        }
    }

    private bool IsFrozenNow =>
    InGameManager.singleton != null && InGameManager.singleton.IsFrozen.Value;
}
