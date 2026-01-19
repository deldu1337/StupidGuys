using Unity.Netcode;
using UnityEngine;

public class PlayerAppearance : NetworkBehaviour
{
    [Header("Target Renderer")]
    [SerializeField] private SkinnedMeshRenderer _skin;

    private SkinDatabaseSO _db;

    // 서버만 쓰기 가능 / 모두 읽기 가능
    private readonly NetworkVariable<int> _skinIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        // 렌더러 자동 탐색(인스펙터 할당이 최우선)
        if (_skin == null)
            _skin = GetComponentInChildren<SkinnedMeshRenderer>();

        _db = SkinDatabaseProvider.Get();
    }

    public override void OnNetworkSpawn()
    {
        _skinIndex.OnValueChanged += HandleSkinIndexChanged;

        // 현재 값 즉시 적용 (늦게 들어온 클라 포함)
        ApplySkin(_skinIndex.Value);

        // 오너: 현재 로컬 선택값을 서버에 제출해서 모두에게 동기화
        if (IsOwner)
        {
            LocalSkinSelection.Load();

            int desiredIndex = LocalSkinSelection.SelectedIndex;

            if (NetworkBlackboard.skinIndex >= 0)
                desiredIndex = NetworkBlackboard.skinIndex;
            
            LocalSkinSelection.Set(desiredIndex);
            
            RequestChangeSkin(desiredIndex);
        }
    }

    public override void OnNetworkDespawn()
    {
        _skinIndex.OnValueChanged -= HandleSkinIndexChanged;
    }

    private void HandleSkinIndexChanged(int prev, int cur)
    {
        ApplySkin(cur);
    }

    private void ApplySkin(int index)
    {
        if (_db == null)
        {
            _db = SkinDatabaseProvider.Get();
            if (_db == null) return;
        }

        if (_skin == null)
        {
            Debug.LogWarning("[PlayerAppearance] SkinnedMeshRenderer가 없어 스킨 적용을 건너뜁니다.");
            return;
        }

        // 인덱스 검증/클램프
        index = _db.ClampIndex(index);


        if (_db.TryGetMaterial(index, out var mat))
        {
            _skin.material = mat;
        }
        else
        {
            Debug.LogWarning($"[PlayerAppearance] index={index} 머티리얼 로드 실패");
        }
    }

    /// <summary>
    /// UI에서 호출할 공개 메서드
    /// - 반드시 오너만 호출하도록 가드
    /// </summary>
    public void RequestChangeSkin(int index)
    {
        if (!IsOwner) return;
        SubmitSkinServerRpc(index);
    }

    [ServerRpc]
    private void SubmitSkinServerRpc(int index)
    {
        if (_db == null)
            _db = SkinDatabaseProvider.Get();

        if (_db == null)
        {
            _skinIndex.Value = 0;
            return;
        }

        // 서버에서 검증/클램프
        index = _db.ClampIndex(index);

        // 머티리얼이 실제 존재하는지까지 체크(데이터가 비어있을 때 0으로 폴백)
        if (!_db.TryGetMaterial(index, out _))
            index = 0;

        _skinIndex.Value = index;
    }
}