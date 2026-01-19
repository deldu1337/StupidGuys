using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    private Rigidbody rb;
    private Coroutine chargeRoutine;

    private WNDPrefabVariator variator;

    public NetworkVariable<double> fireServerTime = 
        new NetworkVariable<double>(0,
                                     NetworkVariableReadPermission.Everyone,
                                     NetworkVariableWritePermission.Server);

    public NetworkVariable<int> colorSeed = 
        new NetworkVariable<int>(0,
                                 NetworkVariableReadPermission.Everyone,
                                 NetworkVariableWritePermission.Server);
    private float _chargeDuration;
    public void Init(float duration)
    {
        _chargeDuration = duration;
    }
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        variator = GetComponent<WNDPrefabVariator>();
    }
    public override void OnNetworkSpawn()
    {
        rb.isKinematic = !IsServer;

        ApplyColor(colorSeed.Value);
        colorSeed.OnValueChanged += (_, next) => {ApplyColor(next); };

        fireServerTime.OnValueChanged += OnFireTimeChaged;
    }
    private void ApplyColor(int seed)
    {
        if (seed == 0 || variator == null)
            return;

        Random.InitState(seed);
        variator.RandomPrefab();
    }
    private void OnFireTimeChaged(double prev, double next)
    {
        if(next > 0)
        {
            if(chargeRoutine != null)
                StopCoroutine(chargeRoutine);

            chargeRoutine = StartCoroutine(ClientChargeRoutine(_chargeDuration));
        }
      
    }
    public void FireServer(Vector3 dir, float power)
    {
        if (!IsServer)
            return;

        rb.AddForce(dir * power, ForceMode.Impulse);
    }
    IEnumerator ClientChargeRoutine(float chargeDuration)
    {
        double startTime = fireServerTime.Value - chargeDuration;
        while (NetworkManager.Singleton.ServerTime.Time < fireServerTime.Value)
        {
            float t = Mathf.Clamp01((float)(NetworkManager.ServerTime.Time - startTime)/chargeDuration);
            transform.localScale = Vector3.one * t;
            yield return null;
        }
    }
    
}
