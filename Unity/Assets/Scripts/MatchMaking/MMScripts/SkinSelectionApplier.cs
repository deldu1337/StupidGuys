using Unity.Netcode;
using UnityEngine;

namespace MatchMaking.MMScripts
{
    public class SkinSelectionApplier : MonoBehaviour
    {
        [SerializeField] private SkinServerSync serverSync;
        
        public void ApplySkinIndex(int index)
        {
            NetworkBlackboard.skinIndex = index;
            LocalSkinSelection.Set(index);

            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (localPlayer != null)
            {
                var appearance = localPlayer.GetComponent<PlayerAppearance>();
                if (appearance != null)
                    appearance.RequestChangeSkin(index);
            }
            
            if (serverSync != null)
             StartCoroutine(serverSync.C_SaveSkinToServer(index));
        }
    }
}
