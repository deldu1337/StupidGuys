using UnityEngine;

public static class SkinDatabaseProvider
{
    private const string RESOURCE_PATH = "Configs/SkinDatabaseSO";
    private static SkinDatabaseSO _cached;


    public static SkinDatabaseSO Get()
    {
        if (_cached != null) return _cached;


        _cached = Resources.Load<SkinDatabaseSO>(RESOURCE_PATH);
        if (_cached == null)
        {
            Debug.LogError($"[SkinDatabaseProvider] SkinDatabaseSO를 찾지 못했습니다. Resources 경로를 확인하세요: {RESOURCE_PATH}");
        }


        return _cached;
    }
}
