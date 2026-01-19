//using Unity.Netcode;
//using UnityEngine;

//public class MapSelectionSync : NetworkBehaviour
//{
//    [SerializeField] private GameObject _map1;
//    [SerializeField] private GameObject _map2;

//    // 0 = Map1, 1 = Map2
//    private NetworkVariable<byte> _selectedMap = new NetworkVariable<byte>(
//        0,
//        NetworkVariableReadPermission.Everyone,
//        NetworkVariableWritePermission.Server
//    );

//    private void Awake()
//    {
//        if (_map1 == null) _map1 = GameObject.Find("Map1");
//        if (_map2 == null) _map2 = GameObject.Find("Map2");
//    }

//    public override void OnNetworkSpawn()
//    {
//        // 값 변화 이벤트 등록
//        _selectedMap.OnValueChanged += OnSelectedMapChanged;

//        // 늦게 접속한 클라도 현재 값으로 즉시 적용
//        Apply(_selectedMap.Value);

//        // 서버는 최초 1회 랜덤 선택
//        if (IsServer)
//        {
//            byte pick = (byte)Random.Range(0, 2);
//            _selectedMap.Value = pick; // 여기서 모든 클라에게 전파됨
//        }
//    }

//    public override void OnNetworkDespawn()
//    {
//        _selectedMap.OnValueChanged -= OnSelectedMapChanged;
//    }

//    private void OnSelectedMapChanged(byte prev, byte next)
//    {
//        Apply(next);
//    }

//    private void Apply(byte mapIndex)
//    {
//        if (_map1 == null || _map2 == null) return;

//        bool chooseMap1 = mapIndex == 0;
//        _map1.SetActive(chooseMap1);
//        _map2.SetActive(!chooseMap1);

//        Debug.Log($"[MapSelectionSync] Apply Map: {(chooseMap1 ? "Map1" : "Map2")}");
//    }
//}

using Unity.Netcode;
using UnityEngine;

public class MapSelectionSync : NetworkBehaviour
{
    [SerializeField] private GameObject _map1;
    [SerializeField] private GameObject _map2;
    [SerializeField] private GameObject _map3;

    // 0 = Map1, 1 = Map2, 2 = Map3
    private NetworkVariable<byte> _selectedMap = new NetworkVariable<byte>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (_map1 == null) _map1 = GameObject.Find("Map1");
        if (_map2 == null) _map2 = GameObject.Find("Map2");
        if (_map3 == null) _map3 = GameObject.Find("Map3");
    }

    public override void OnNetworkSpawn()
    {
        _selectedMap.OnValueChanged += OnSelectedMapChanged;

        // 늦게 접속한 클라도 현재 값으로 즉시 적용
        Apply(_selectedMap.Value);

        // 서버는 최초 1회 랜덤 선택
        if (IsServer)
        {
            byte pick = (byte)Random.Range(0, 3); // 0,1,2
            _selectedMap.Value = pick;
        }
    }

    public override void OnNetworkDespawn()
    {
        _selectedMap.OnValueChanged -= OnSelectedMapChanged;
    }

    private void OnSelectedMapChanged(byte prev, byte next)
    {
        Apply(next);
    }

    private void Apply(byte mapIndex)
    {
        if (_map1 == null || _map2 == null || _map3 == null)
            return;

        bool chooseMap1 = mapIndex == 0;
        bool chooseMap2 = mapIndex == 1;
        bool chooseMap3 = mapIndex == 2;

        _map1.SetActive(chooseMap1);
        _map2.SetActive(chooseMap2);
        _map3.SetActive(chooseMap3);

        string mapName = chooseMap1 ? "Map1" : (chooseMap2 ? "Map2" : "Map3");
        Debug.Log($"[MapSelectionSync] Apply Map: {mapName}");
    }
}

