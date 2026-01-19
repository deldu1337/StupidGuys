using System;
using UnityEngine;


/// <summary>
/// 매치메이킹/인게임 어디서든 공통으로 사용할 로컬 선택값 저장소.
/// - PlayerPrefs에 저장해서 씬 전환/재실행에도 유지
/// - 값 변경 이벤트 제공
/// </summary>
public static class LocalSkinSelection
{
    public static int SelectedIndex;
    public static event Action<int> OnChanged;

    private static string Key
    {
        get
        {
            var uid = NetworkBlackboard.userId;
            return string.IsNullOrEmpty(uid) ? "SkinIndex_UNKNOWN" : $"SkinIndex_{uid}";
        }
    }


    public static void Load()
    {
        SelectedIndex = PlayerPrefs.GetInt(Key, 0);
    }


    public static void Set(int index)
    {
        SelectedIndex = index;
        PlayerPrefs.SetInt(Key, index);
        PlayerPrefs.Save();

        OnChanged?.Invoke(index);
    }
}
