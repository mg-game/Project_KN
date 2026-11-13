using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 기기 알림 백엔드 (NOTI-01, SPEC4 §4.3). 모바일 구현(MobileNotificationBackend)은 Platform 어셈블리에 있고
    /// [RuntimeInitializeOnLoadMethod]에서 LocalNotifications.Backend에 넣는다 (IdleGame 어셈블리는 알림 패키지를 참조하지 않는다).
    /// </summary>
    public interface INotificationBackend
    {
        /// <summary>채널 등록 등 준비. 쓸 수 없으면 false</summary>
        bool Initialize();

        /// <summary>지금 기기 권한</summary>
        NotificationPermission Permission { get; }

        /// <summary>시스템 권한 창을 띄우고 결과를 done으로 알린다 (코루틴)</summary>
        IEnumerator RequestPermission(Action<NotificationPermission> done);

        void Schedule(in PlannedNotification notification);

        void CancelAll();

        /// <summary>이미 온 알림과 배지를 지운다 (복귀 때)</summary>
        void ClearDelivered();

        /// <summary>기기 알림 설정 화면을 연다 (권한이 거절됐을 때)</summary>
        void OpenSystemSettings();
    }

    /// <summary>예약할 알림 하나 (NotificationPlanner.Plan이 만든다)</summary>
    public struct PlannedNotification
    {
        public NotificationKind Kind;
        /// <summary>종류마다 고정 ID (바구니 1001 · 원정 1002 · 던전 1003 · 출석 1004)</summary>
        public int Id;
        /// <summary>울릴 현지 시각</summary>
        public DateTime FireLocal;
        public string Title;
        public string Body;
    }

    /// <summary>
    /// 로컬 알림 창구 (NOTI-01). 백엔드가 없으면(에디터 · PC · 패키지 전) 아무것도 하지 않는다 — 예외를 내지 않고, 백엔드 오류는 경고 한 줄로 삼킨다.
    /// 보내도 되는지는 EffectivePermission 한 규칙으로 정한다 (계획과 설정 창이 같이 쓴다 — 게임 안에서 허락받은 뒤에만 예약).
    /// GameBootstrap: 부팅 때 Initialize · SyncSavedPermission, 일시정지 · 종료 때 계획(NotificationPlanner)을 Reschedule(= CancelAll 뒤 예약),
    /// 복귀 때 CancelAll · ClearDelivered · SyncSavedPermission (켜 둔 동안에는 예약을 두지 않는다 — 게임 중에 알림이 울리지 않게).
    /// 모바일 백엔드(MobileNotificationBackend)는 Platform 어셈블리가 [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]에서 Backend에 넣는다
    /// (com.unity.mobile.notifications 패키지가 있을 때만, Android · iOS).
    /// </summary>
    public static class LocalNotifications
    {
        /// <summary>기기 백엔드 (없으면 null — 에디터 · PC)</summary>
        public static INotificationBackend Backend;

        /// <summary>백엔드가 있는지 (에디터 · PC · 패키지 전 = false)</summary>
        public static bool HasBackend => Backend != null;

        static INotificationBackend _initializedBackend;
        static bool _initializeResult;

        /// <summary>
        /// 백엔드를 준비한다 (채널 등록 등, 백엔드마다 한 번만 — 두 번째부터는 첫 결과). 백엔드가 없거나 실패하면 false (예외 없음)
        /// </summary>
        public static bool Initialize()
        {
            var backend = Backend;
            if (backend == null) return false;
            if (ReferenceEquals(backend, _initializedBackend)) return _initializeResult;
            _initializedBackend = backend;
            try
            {
                _initializeResult = backend.Initialize();
            }
            catch (Exception e)
            {
                Warn("준비", e);
                _initializeResult = false;
            }
            return _initializeResult;
        }

        /// <summary>
        /// 저장된 권한(GameSettings.NotifyPermission)을 기기 설정에 맞춘다: 저장 Granted인데 기기가 Denied면 Denied로,
        /// 저장 Denied인데 기기가 Granted면 Granted로 (기기 설정에서 바꾼 경우). Unknown · SoftDeclined(아직 게임 안에서 묻지 않음 · '괜찮아요')는 그대로.
        /// 백엔드가 없으면 아무것도 하지 않는다. 바꿨으면 true
        /// </summary>
        public static bool SyncSavedPermission()
        {
            if (Backend == null) return false;
            var device = Permission;
            var saved = GameSettings.NotifyPermission;
            if (saved == NotificationPermission.Granted && device == NotificationPermission.Denied)
            {
                GameSettings.NotifyPermission = NotificationPermission.Denied;
                return true;
            }
            if (saved == NotificationPermission.Denied && device == NotificationPermission.Granted)
            {
                GameSettings.NotifyPermission = NotificationPermission.Granted;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 알림 권한을 정하는 한 가지 규칙 (예약 계획 NotificationContext.Capture와 설정 창의 권한 줄이 함께 쓴다):
        /// 기기가 Denied면 Denied. 저장 Denied인데 기기가 Granted면 Granted (기기 설정에서 다시 켬 — SyncSavedPermission과 같은 규칙).
        /// 나머지는 저장된 값: 기기가 Granted여도(Android 12 이하는 늘 허용, 기기 설정에서 먼저 켬) 게임 안에서 허락받기 전(Unknown · SoftDeclined)이면
        /// 아직 허락 전 — 설정 창은 '허용하기'를 보여 주고, 누르면 RequestPermission이 바로 Granted를 돌려줘 저장된다
        /// </summary>
        public static NotificationPermission EffectivePermission(NotificationPermission device, NotificationPermission saved)
        {
            if (device == NotificationPermission.Denied) return NotificationPermission.Denied;
            if (saved == NotificationPermission.Denied && device == NotificationPermission.Granted) return NotificationPermission.Granted;
            return saved;
        }

        /// <summary>지금 기기 권한(Permission)과 저장된 권한(GameSettings.NotifyPermission)으로 정한 권한 (EffectivePermission). Granted일 때만 예약한다</summary>
        public static NotificationPermission Effective => EffectivePermission(Permission, GameSettings.NotifyPermission);

        /// <summary>기기 권한 (백엔드가 없으면 Unknown)</summary>
        public static NotificationPermission Permission
        {
            get
            {
                var backend = Backend;
                if (backend == null) return NotificationPermission.Unknown;
                try
                {
                    return backend.Permission;
                }
                catch (Exception e)
                {
                    Warn("권한 읽기", e);
                    return NotificationPermission.Unknown;
                }
            }
        }

        /// <summary>예약을 모두 지우고 list를 다시 예약한다</summary>
        public static void Reschedule(IReadOnlyList<PlannedNotification> list)
        {
            var backend = Backend;
            if (backend == null) return;
            try
            {
                backend.CancelAll();
                if (list == null) return;
                for (int i = 0; i < list.Count; i++)
                {
                    var notification = list[i];
                    backend.Schedule(in notification);
                }
            }
            catch (Exception e)
            {
                Warn("예약", e);
            }
        }

        /// <summary>예약을 모두 지운다</summary>
        public static void CancelAll()
        {
            var backend = Backend;
            if (backend == null) return;
            try
            {
                backend.CancelAll();
            }
            catch (Exception e)
            {
                Warn("예약 지우기", e);
            }
        }

        /// <summary>이미 온 알림 · 배지를 지운다 (복귀 때)</summary>
        public static void ClearDelivered()
        {
            var backend = Backend;
            if (backend == null) return;
            try
            {
                backend.ClearDelivered();
            }
            catch (Exception e)
            {
                Warn("받은 알림 지우기", e);
            }
        }

        /// <summary>
        /// 시스템 권한을 묻는다 (코루틴). 백엔드가 없으면 바로 Unknown으로 끝난다. done은 꼭 한 번만 불린다:
        /// 백엔드 코루틴은 여기서 한 걸음씩 직접 돌려(안에 든 IEnumerator도 펼쳐서) 어느 걸음에서 예외가 나도 삼키고 Unknown으로 끝내고,
        /// 백엔드가 done을 부르지 않고 끝나면 그때 권한(Permission)으로 알린다. done이 낸 예외도 경고 한 줄로 삼킨다.
        /// 권한 창을 띄울 때만 돌아 할당(스택 · 감싼 콜백)은 신경 쓰지 않는다
        /// </summary>
        public static IEnumerator RequestPermission(Action<NotificationPermission> done)
        {
            var backend = Backend;
            if (backend == null)
            {
                Notify(done, NotificationPermission.Unknown);
                yield break;
            }

            bool reported = false;
            Action<NotificationPermission> once = permission =>
            {
                if (reported) return;
                reported = true;
                Notify(done, permission);
            };

            IEnumerator request = null;
            try
            {
                request = backend.RequestPermission(once);
            }
            catch (Exception e)
            {
                Warn("권한 묻기", e);
                once(NotificationPermission.Unknown);
                yield break;
            }

            // 백엔드 코루틴을 직접 돌린다 (yield는 try 밖에서만 할 수 있어 걸음마다 MoveNext만 감싼다)
            var stack = new Stack<IEnumerator>();
            if (request != null) stack.Push(request);
            while (stack.Count > 0)
            {
                object current;
                try
                {
                    var top = stack.Peek();
                    if (!top.MoveNext())
                    {
                        stack.Pop();
                        continue;
                    }
                    current = top.Current;
                }
                catch (Exception e)
                {
                    Warn("권한 묻기", e);
                    once(NotificationPermission.Unknown);
                    yield break;
                }
                // 안에 든 코루틴(IEnumerator · CustomYieldInstruction)도 여기서 돌려 그 예외까지 삼킨다. 나머지(WaitForSeconds 등)는 Unity에 맡긴다
                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return current;
            }

            if (!reported) once(Permission);
        }

        /// <summary>권한 결과를 알린다 (콜백이 낸 예외는 경고 한 줄로 삼킨다 — 이 창구는 예외를 내지 않는다)</summary>
        static void Notify(Action<NotificationPermission> done, NotificationPermission permission)
        {
            if (done == null) return;
            try
            {
                done(permission);
            }
            catch (Exception e)
            {
                Warn("권한 결과 알리기", e);
            }
        }

        /// <summary>기기 알림 설정 화면을 연다 (백엔드가 없으면 아무것도 하지 않는다)</summary>
        public static void OpenSystemSettings()
        {
            var backend = Backend;
            if (backend == null) return;
            try
            {
                backend.OpenSystemSettings();
            }
            catch (Exception e)
            {
                Warn("설정 열기", e);
            }
        }

        static void Warn(string what, Exception e) => Debug.LogWarning($"[Notify] 알림 {what} 실패: {e.Message}");
    }
}
