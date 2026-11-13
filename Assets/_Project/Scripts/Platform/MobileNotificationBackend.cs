// 이 파일은 IdleGame.Platform 어셈블리(Android · iOS 전용)에만 들어간다. com.unity.mobile.notifications 패키지가 있을 때만
// LL_NOTIFICATIONS가 정의된다 (asmdef versionDefines). 에디터 · PC · 패키지 전에는 본문이 비어 LocalNotifications.Backend는 null로 남는다.
// compile_check.py는 Scripts/Platform/**를 IdleGame 글롭에서 뺀다 — 진짜 확인은 rm2_proj의 Android · iOS 스크립트 컴파일 (V2 CompileAndroidScripts).
#if LL_NOTIFICATIONS && (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using System;
using System.Collections;
using Unity.Notifications;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#else
using Unity.Notifications.iOS;
#endif
using UnityEngine;

namespace IdleGame.Platform
{
    /// <summary>
    /// 기기 로컬 알림 백엔드 (NOTI-01, SPEC4 §4.3): Unity Mobile Notifications 2.4 통합 API (NotificationCenter).
    ///   · 채널 ll_reminders '수선점 소식' / '바구니·원정·출석 알림', 중요도 Default (Badge | Sound — Alert를 넣으면 Android에서 High가 된다).
    ///   · Android 예약은 작은 아이콘 ll_notify_small · 큰 아이콘 ll_notify_large (Project Settings › Mobile Notifications에 등록한 ID).
    ///   · iOS: 실행 때 권한을 묻지 않는다(설정에서 끔) — 게임 안 미리 묻기(P4-6)에서 Alert | Badge | Sound를 묻는다. 배지는 복귀 때 지운다.
    /// 예외는 LocalNotifications 창구가 경고 한 줄로 삼킨다.
    /// </summary>
    public sealed class MobileNotificationBackend : INotificationBackend
    {
        public const string ChannelId = "ll_reminders";
        public const string ChannelName = "수선점 소식";
        public const string ChannelDescription = "바구니·원정·출석 알림";
        public const string SmallIconId = "ll_notify_small";
        public const string LargeIconId = "ll_notify_large";

        bool _initialized;

        /// <summary>장면을 읽기 전에 창구에 넣는다 (이미 다른 백엔드가 있으면 그대로)</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (LocalNotifications.Backend == null) LocalNotifications.Backend = new MobileNotificationBackend();
        }

        public bool Initialize()
        {
            if (_initialized) return true;
            var args = NotificationCenterArgs.Default;
            args.AndroidChannelId = ChannelId;
            args.AndroidChannelName = ChannelName;
            args.AndroidChannelDescription = ChannelDescription;
#if UNITY_IOS
            args.PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Badge | NotificationPresentation.Sound;
#else
            args.PresentationOptions = NotificationPresentation.Badge | NotificationPresentation.Sound;
#endif
            NotificationCenter.Initialize(args);
            _initialized = true;
            return true;
        }

        public NotificationPermission Permission
        {
            get
            {
                if (!Initialize()) return NotificationPermission.Unknown;
#if UNITY_ANDROID
                switch (AndroidNotificationCenter.UserPermissionToPost)
                {
                    case PermissionStatus.Allowed:
                        return NotificationPermission.Granted;
                    case PermissionStatus.Denied:
                    case PermissionStatus.DeniedDontAskAgain:
                    case PermissionStatus.NotificationsBlockedForApp:
                        return NotificationPermission.Denied;
                    default:
                        return NotificationPermission.Unknown;
                }
#else
                switch (iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus)
                {
                    case AuthorizationStatus.Authorized:
                    case AuthorizationStatus.Provisional:
                    case AuthorizationStatus.Ephemeral:
                        return NotificationPermission.Granted;
                    case AuthorizationStatus.Denied:
                        return NotificationPermission.Denied;
                    default:
                        return NotificationPermission.Unknown;
                }
#endif
            }
        }

        public IEnumerator RequestPermission(Action<NotificationPermission> done)
        {
            Initialize();
            var request = NotificationCenter.RequestPermission();
            while (request.Status == NotificationsPermissionStatus.RequestPending) yield return null;
            done?.Invoke(request.Status == NotificationsPermissionStatus.Granted ? NotificationPermission.Granted : NotificationPermission.Denied);
        }

        public void Schedule(in PlannedNotification notification)
        {
            Initialize();
            var fire = DateTime.SpecifyKind(notification.FireLocal, DateTimeKind.Local);
#if UNITY_ANDROID
            var android = new AndroidNotification
            {
                Title = notification.Title,
                Text = notification.Body,
                FireTime = fire,
                SmallIcon = SmallIconId,
                LargeIcon = LargeIconId,
                ShowTimestamp = true,
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(android, ChannelId, notification.Id);
#else
            var unified = new Notification
            {
                Identifier = notification.Id,
                Title = notification.Title,
                Text = notification.Body,
                Badge = 1,
                ShowInForeground = false,
            };
            NotificationCenter.ScheduleNotification(unified, new NotificationDateTimeSchedule(fire));
#endif
        }

        public void CancelAll()
        {
            Initialize();
            NotificationCenter.CancelAllScheduledNotifications();
        }

        public void ClearDelivered()
        {
            Initialize();
            NotificationCenter.CancelAllDeliveredNotifications();
            NotificationCenter.ClearBadge();
        }

        public void OpenSystemSettings()
        {
            Initialize();
            NotificationCenter.OpenNotificationSettings();
        }
    }
}
#endif
