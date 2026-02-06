using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MatchMaking.MMScripts
{
    public class SkinServerSync : MonoBehaviour
    {
        const string BASE_URL = "http://3.37.215.9:5000";
        //const string BASE_URL = "https://localhost:7018";

        [System.Serializable]
        private class UpdateSkinRequest
        {
            public int SkinIndex;
        }

        public IEnumerator C_SaveSkinToServer(int index)
        {
            var dto = new UpdateSkinRequest { SkinIndex = index };
            string json = JsonUtility.ToJson(dto);

            using (var req = new UnityWebRequest($"{BASE_URL}/user/skin", "PUT"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {NetworkBlackboard.jwt}");

                req.certificateHandler = new AuthController.BypassCertificateHandler();

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                    Debug.LogError($"[SkinServerSync] 실패: {req.responseCode} / {req.downloadHandler.text}");
            }
        }
    }
}
