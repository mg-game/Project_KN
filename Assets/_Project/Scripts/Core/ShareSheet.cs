using System;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 기기 공유 시트 (SAVE-01 §4.2 세이브 코드 · CR-09 플레이 기록 내보내기): 안드로이드는 ACTION_SEND text/plain + createChooser,
    /// 다른 플랫폼(에디터 · PC · iOS)은 클립보드로 대신한다. 예외를 내지 않는다 — 공유 시트를 띄우지 못하면 클립보드로 복사한다.
    /// 버튼을 누를 때만 부른다 (Java 객체 몇 개를 만든다).
    /// </summary>
    public static class ShareSheet
    {
        /// <summary>기기 공유 시트를 띄울 수 있는지 (안드로이드 기기만). false면 화면은 공유 버튼을 숨기고 복사만 보여 준다</summary>
        public static bool CanShare
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>title · text를 공유 시트로 보낸다 (못 하면 클립보드로 복사)</summary>
        public static void Share(string title, string text) => TryShare(title, text);

        /// <summary>
        /// title · text를 공유 시트로 보낸다. 공유 시트를 띄웠으면 true, 클립보드로 복사했으면(다른 플랫폼 · 실패) false.
        /// text가 비어 있으면 아무것도 하지 않고 false
        /// </summary>
        public static bool TryShare(string title, string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                OpenAndroidChooser(title ?? string.Empty, text);
                return true;
            }
            catch (Exception e)
            {
                // 아주 긴 글(바인더 한도)이나 공유할 앱이 없으면 여기로 온다 — 복사로 대신한다
                Debug.LogWarning($"[Share] 공유 시트를 띄우지 못해 복사로 대신해요: {e.Message}");
            }
#endif
            CopyToClipboard(text);
            return false;
        }

        /// <summary>클립보드에 복사한다 (예외를 내지 않는다)</summary>
        public static void CopyToClipboard(string text)
        {
            try
            {
                GUIUtility.systemCopyBuffer = text ?? string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Share] 클립보드에 복사하지 못했어요: {e.Message}");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void OpenAndroidChooser(string title, string text)
        {
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                // Intent의 setter는 자기 자신을 돌려준다 — 돌아온 래퍼도 바로 닫는다
                using (intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"))) { }
                using (intent.Call<AndroidJavaObject>("setType", "text/plain")) { }
                if (title.Length > 0)
                {
                    using (intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), title)) { }
                }
                using (intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text)) { }
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, title))
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity.Call("startActivity", chooser);
                }
            }
        }
#endif
    }
}
