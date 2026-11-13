using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 알림 미리 묻기 760×600 (NOTI-01, SPEC4 §4.3): 바구니 그림(Art/UI/idle_basket_3) + '알림을 보내 드릴까요?' +
    /// '바구니가 가득 차거나 원정이 돌아오면 살짝 알려 드릴게요.\n밤 10시부터 아침 8시까지는 보내지 않아요.' + [괜찮아요] Secondary (왼쪽) · [알려 주세요] Primary (오른쪽).
    /// 발표자(MetaPopupPresenter)가 NotificationAsk 요청을 스테이지 사이에 띄운다 (튜토리얼 뒤, 바구니가 처음 가득 찼을 때 — ShouldAsk).
    ///   · 열 때 GameSettings.NotifyLastAskUnix = 지금 (7일 안에는 다시 묻지 않는다).
    ///   · '알려 주세요' → LocalNotifications.RequestPermission (UIManager에서 돌린다 — 이 창은 바로 닫힌다): Granted · Denied만 저장한다
    ///     (백엔드가 없는 에디터 · PC는 Unknown — 아무것도 바꾸지 않는다).
    ///   · '괜찮아요' · X · 뒤로 · 어둠 → SoftDeclined + NotifySoftAsks++ (두 번까지만 묻는다).
    /// 미리 묻기 조건 (ShouldAsk): 권한이 아직 없음(Unknown · SoftDeclined — 시스템 창은 아직 띄우지 않음) + NotifySoftAsks &lt; 2 + 마지막으로 물은 지 7일 이상.
    /// SPEC4의 'Permission Unknown'은 '시스템 창을 아직 안 띄움'으로 읽는다 — SoftDeclined를 빼면 NotifySoftAsks &lt; 2가 뜻이 없다 (한 번만 묻게 된다).
    /// </summary>
    public class NotificationAskPopup : UIPopup
    {
        /// <summary>다시 묻기까지 (초, 7일)</summary>
        public const long AskCooldownSeconds = 7L * 86400L;
        /// <summary>'괜찮아요' 최대 횟수</summary>
        public const int MaxSoftAsks = 2;

        public const string TitleText = "알림을 보내 드릴까요?";
        public const string BodyText = "바구니가 가득 차거나 원정이 돌아오면 살짝 알려 드릴게요.\n밤 10시부터 아침 8시까지는 보내지 않아요.";

        [SerializeField] Button _laterButton;
        [SerializeField] Button _allowButton;
        [SerializeField] TMP_Text _bodyText;

        bool _answered;

        protected override bool CloseButtonUsesDismiss => true;

        protected override void Awake()
        {
            base.Awake();
            if (_laterButton != null) _laterButton.onClick.AddListener(Decline);
            if (_allowButton != null) _allowButton.onClick.AddListener(Allow);
        }

        /// <summary>지금 미리 물어도 되는지 (순수 — 테스트용)</summary>
        public static bool ShouldAsk(NotificationPermission permission, int softAsks, long lastAskUnix, long nowUnix)
        {
            if (permission != NotificationPermission.Unknown && permission != NotificationPermission.SoftDeclined) return false;
            if (softAsks >= MaxSoftAsks) return false;
            return lastAskUnix <= 0 || nowUnix - lastAskUnix >= AskCooldownSeconds;
        }

        /// <summary>지금 미리 물어도 되는지 (설정 · 시계 · 튜토리얼). 할당 없음</summary>
        public static bool ShouldAskNow()
        {
            if (!MetaUi.IsActive) return false;
            var tutorial = Game.Tutorial;
            if (tutorial != null && !tutorial.IsDone) return false;
            return ShouldAsk(GameSettings.NotifyPermission, GameSettings.NotifySoftAsks, GameSettings.NotifyLastAskUnix, NowUnix());
        }

        /// <summary>지금 (유닉스 초, Game.Clock — 시계 바꾸기 FakeClock을 따른다)</summary>
        public static long NowUnix()
        {
            var clock = Game.Clock;
            var now = clock != null ? clock.UtcNow : DateTime.UtcNow;
            return new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeSeconds();
        }

        /// <summary>띄운다 (물은 시각을 적는다)</summary>
        public void Show()
        {
            _answered = false;
            GameSettings.NotifyLastAskUnix = NowUnix();
            if (_bodyText != null) _bodyText.text = BodyText;
            Open();
        }

        protected override void OnDimOrBack() => Decline();

        protected override void OnClose()
        {
            // X · 다른 길로 닫혀도 '괜찮아요'와 같다 (한 번만 센다)
            if (!_answered) RecordDecline();
        }

        void Decline()
        {
            RecordDecline();
            Close();
        }

        void RecordDecline()
        {
            if (_answered) return;
            _answered = true;
            GameSettings.NotifyPermission = NotificationPermission.SoftDeclined;
            GameSettings.NotifySoftAsks = GameSettings.NotifySoftAsks + 1;
        }

        void Allow()
        {
            if (_answered) return;
            _answered = true;
            var host = UIManager.Instance;
            Close();
            if (host == null || !host.isActiveAndEnabled) return;
            host.StartCoroutine(LocalNotifications.RequestPermission(OnPermission));
        }

        static void OnPermission(NotificationPermission permission)
        {
            if (permission == NotificationPermission.Granted || permission == NotificationPermission.Denied)
            {
                GameSettings.NotifyPermission = permission;
                if (permission == NotificationPermission.Granted) MetaUi.Toast("알림을 켰어요. 바구니가 가득 차면 알려 드릴게요", ToastKind.Success);
            }
        }
    }
}
