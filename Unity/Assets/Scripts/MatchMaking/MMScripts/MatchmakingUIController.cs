using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class MatchmakingUIController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private List<Button> _buttons;

    [Header("(Optional) Preview")]
    [SerializeField] private SkinnedMeshRenderer _previewSkin;

    private SkinDatabaseSO _db;


    private void Awake()
    {
        _db = SkinDatabaseProvider.Get();

        LocalSkinSelection.Load();
        BindButtons();

        ApplyPreview(LocalSkinSelection.SelectedIndex);
    }


    private void BindButtons()
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            int index = i;

            var btn = _buttons[index];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClickSkin(index));
        }
    }


    private void OnClickSkin(int index)
    {
        LocalSkinSelection.Set(index);

        ApplyPreview(index);

        var appearance = NetworkLocalPlayerUtil.TryGetLocalPlayerAppearance();
        if (appearance != null) appearance.RequestChangeSkin(index);
    }


    private void ApplyPreview(int index)
    {
        if (_previewSkin == null) return;
        if (_db == null)
        {
            _db = SkinDatabaseProvider.Get();
            if (_db == null) return;
        }

        index = _db.ClampIndex(index);
        if (_db.TryGetMaterial(index, out var mat))
            _previewSkin.material = mat;
    }
}
