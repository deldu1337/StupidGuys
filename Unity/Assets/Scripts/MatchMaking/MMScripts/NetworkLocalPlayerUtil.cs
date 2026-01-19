using Unity.Netcode;
using UnityEngine;


public static class NetworkLocalPlayerUtil
{
    public static PlayerAppearance TryGetLocalPlayerAppearance()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return null;


        var localNo = nm.SpawnManager.GetLocalPlayerObject();
        if (localNo == null) return null;


        return localNo.GetComponent<PlayerAppearance>();
    }
}
