using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BallPool : NetworkBehaviour
{

    public GameObject projectilePrefab;   // must have NetworkObject (and be in NetworkPrefabs list)

    public Queue<GameObject> ballPool;
    public int ballPoolCount = 10;
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            ballPool = new Queue<GameObject>();
            for(int i=0; i<ballPoolCount; i++)
            {
                InitBallPool();
            }

        }
    }
    private void InitBallPool()
    {
        GameObject go = Instantiate(projectilePrefab);
        go.SetActive(false);
        ballPool.Enqueue(go);
        
                
    }

    public GameObject GetBallPool()
    {
        if (ballPool.Count <= 0)
        {
            GameObject extraGo = Instantiate(projectilePrefab);
            return extraGo;
        }
        GameObject go = ballPool.Dequeue();
        return go;
    }
    
    public void ReturnBallPool(GameObject ball)
    {
        var netObj = ball.GetComponent<NetworkObject>();
        if (netObj.IsSpawned)
            netObj.Despawn(false);

        ball.SetActive(false);
        ballPool.Enqueue(ball);
    }

}
