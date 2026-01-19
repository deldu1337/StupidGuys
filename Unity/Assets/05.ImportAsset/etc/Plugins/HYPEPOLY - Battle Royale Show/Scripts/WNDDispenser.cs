using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class WNDDispenser : NetworkBehaviour
{
    [Header("Prefabs")]
    
    //public GameObject particlePrefab;     // not networked, spawned locally via ClientRpc

    [Header("Shoot")]
    public float shootingDelay = 1f;
    public float power = 10f;
    public float randomPowerMinMultiplier = 0.9f;
    public float randomPowerMaxMultiplier = 1.1f;

    private Transform arrowsParent;

    public BallPool ballPool;

    private NetworkVariable<double> nextFireTime =
                                    new NetworkVariable<double>(0,
                                                                NetworkVariableReadPermission.Everyone,
                                                                NetworkVariableWritePermission.Server);

    private void Awake()
    {
        var childs = GetComponentsInChildren<Transform>(true);
        foreach (var tr in childs)
        {
            if (tr.gameObject.name.Contains("[Projectile Parent]"))
            {
                arrowsParent = tr;
                break;
            }
        }

        //if (arrowsParent == null)
        //    arrowsParent = transform;


    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
            return;

    }
    //private void Update()
    //{
    //    if (!IsServer) return;

    //    if (nextFireTime.Value == 0)
    //    {
    //        nextFireTime.Value = NetworkManager.Singleton.ServerTime.Time + shootingDelay;
    //    }

    //    if (NetworkManager.Singleton.ServerTime.Time >= nextFireTime.Value)
    //    {
    //        CreateArrow(arrowsParent.position, arrowsParent.rotation);
    //        nextFireTime.Value = NetworkManager.Singleton.ServerTime.Time + shootingDelay;
    //    }

    //}

    private void Update()
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        double now = NetworkManager.Singleton.ServerTime.Time;

        if (nextFireTime.Value == 0)
            nextFireTime.Value = now + shootingDelay;

        if (now >= nextFireTime.Value)
        {
            if (arrowsParent == null)
                arrowsParent = transform;

            CreateArrow(arrowsParent.position, arrowsParent.rotation);
            nextFireTime.Value = now + shootingDelay;
        }
    }


    private void CreateArrow(Vector3 pos, Quaternion rot)
    {
        // Spawn 호출 전에 반드시 리스닝 상태 확인
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;


        Ball ball = ballPool.GetBallPool().GetComponent<Ball>();
       
        if (ball != null)
        {
            int seed = Random.Range(0, int.MaxValue);
            ball.colorSeed.Value = seed;
        }
        ball.transform.SetPositionAndRotation(pos,rot);
        ball.gameObject.SetActive(true);
        ball.transform.localScale = Vector3.zero;
   
        var networkObject = ball.GetComponent<NetworkObject>();
        networkObject.Spawn(true);
        ball.transform.SetParent(ballPool.transform, true);

        ball.Init(shootingDelay);
        double fireTime = NetworkManager.Singleton.ServerTime.Time + 0.5;
        ball.fireServerTime.Value = fireTime;


        StartCoroutine(FireAfterDelay(ball, fireTime));


        //PlayShootFxClientRPC(arrowsParent.position, arrowsParent.rotation);
    }



    //[ClientRpc]
    //private void PlayShootFxClientRPC(Vector3 pos, Quaternion rot)
    //{
    //    if (particlePrefab == null) return;

    //    var fx = Instantiate(particlePrefab, pos, rot);
    //    fx.transform.localEulerAngles = Vector3.zero;
    //}
    IEnumerator FireAfterDelay(Ball ball, double fireTime)
    {
        while (NetworkManager.Singleton.ServerTime.Time < fireTime)
            yield return null;

        ball.FireServer(ball.transform.up, power);
    }

   

}

