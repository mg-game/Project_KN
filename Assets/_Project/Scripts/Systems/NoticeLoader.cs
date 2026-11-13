using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace IdleGame
{
    /// <summary>
    /// 공지 JSON 로더. URL이 있으면 원격에서 받고, 실패하면 Resources/Data/notices.json을 쓴다.
    /// 형식: { "notices": [ { "id", "title", "body", "date", "popup", "order" } ] }
    /// </summary>
    public static class NoticeLoader
    {
        const string LocalResourcePath = "Data/notices";
        const int TimeoutSeconds = 5;

        public static IEnumerator Load(string remoteUrl, Action<List<NoticeData>> onLoaded)
        {
            if (!string.IsNullOrEmpty(remoteUrl))
            {
                using var request = UnityWebRequest.Get(remoteUrl);
                request.timeout = TimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success && TryParse(request.downloadHandler.text, out var remote))
                {
                    onLoaded(remote);
                    yield break;
                }
                Debug.LogWarning($"[Notice] 원격 공지 로드 실패, 로컬 공지를 사용합니다: {request.error}");
            }

            var asset = Resources.Load<TextAsset>(LocalResourcePath);
            onLoaded(asset != null && TryParse(asset.text, out var local) ? local : new List<NoticeData>());
        }

        public static bool TryParse(string json, out List<NoticeData> notices)
        {
            try
            {
                notices = JsonConvert.DeserializeObject<NoticeList>(json)?.notices ?? new List<NoticeData>();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Notice] 공지 JSON 파싱 실패: {e.Message}");
                notices = null;
                return false;
            }
        }

        [Serializable]
        class NoticeList
        {
            public List<NoticeData> notices;
        }
    }
}
