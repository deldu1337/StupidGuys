using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 모든 클라이언트가 동일하게 가지고 있어야 하는 스킨 DB.
/// 네트워크에서는 Material이 아니라 "인덱스"만 전송하고,
/// 각 클라이언트가 이 DB에서 인덱스로 Material을 꺼내 적용한다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Skin Database", fileName = "SkinDatabaseSO")]
public class SkinDatabaseSO : ScriptableObject
{
    [Tooltip("인덱스로 접근할 스킨 데이터 목록(모든 클라 동일 순서/내용 필수)")]
    public List<ColorCustomData> skins;


    public int Count => skins == null ? 0 : skins.Count;


    public int ClampIndex(int index)
    {
        if (Count <= 0) return 0;
        if (index < 0) return 0;
        if (index >= Count) return Count - 1;
        return index;
    }


    public bool TryGetMaterial(int index, out Material mat)
    {
        mat = null;
        if (skins == null) return false;
        if (index < 0 || index >= skins.Count) return false;


        var data = skins[index];
        if (data == null) return false;


        mat = data.colorMaterial;
        return mat != null;
    }
}