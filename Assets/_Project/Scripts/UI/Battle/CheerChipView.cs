using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 친구들의 응원 칩 (PRG-03 §13.2): 112×48 StageArea TopRight(AnchorX −132, AnchorY −310) — 밤하늘 알약 + 응원대장 짝짝이 40 + 'm:ss' (Label 22 굵게 Cream, 1초에 한 번).
    /// SPEC4의 x −96은 IA §3.3 R1 칸(StatsButton)과 겹쳐 왼쪽으로 옮겼다 (V2 R6 — 화면 836–948 × 440–488, R1 964와 16px).
    /// 응원이 이어지는 동안만 (Cheer.IsActive) · 튜토리얼 중 · 잠자기 중에는 숨긴다 (효과는 그대로 흐른다). 누르면 토스트 '친구들이 응원하고 있어요! 공격력 +20%'.
    /// 응원 시작 소리(cheer_start) · 진동은 응원 받기 단추(StrengthenPopup, P4-6)가 낸다 — 여기서는 두 번 울리지 않게 내지 않는다 (AUD 안내).
    /// 빌더가 만들기 전(사용자 씬)에는 없다 — BattleView가 null이면 건너뛴다
    /// </summary>
    public class CheerChipView : MonoBehaviour
    {
        [SerializeField] RectTransform _root;
        [SerializeField] TMP_Text _time;
        [SerializeField] Button _button;

        public const string TapToast = "친구들이 응원하고 있어요! 공격력 +20%";
        /// <summary>빌더 자리 (StageArea TopRight 기준, 칩 오른쪽 위 모서리)와 크기</summary>
        public const float AnchorX = -132f;
        public const float AnchorY = -310f;
        public const float Width = 112f;
        /// <summary>IA §3.3 R1 칸 (StatsButton) 왼쪽 끝 = 1080 − 12 − 104 (StageArea 오른쪽 기준 −116)</summary>
        public const float RailCellLeftFromRight = -116f;
        /// <summary>R1 칸과 띄울 최소 간격 (SPEC4 §11.4 비평 정적 검사)</summary>
        public const float RailClearance = 8f;

        int _shownSeconds = -1;
        bool _visible = true;
        bool _wired;

        void Awake()
        {
            Wire();
            SetVisible(false);
        }

        void Wire()
        {
            if (_wired) return;
            _wired = true;
            if (_button != null) _button.onClick.AddListener(OnTap);
        }

        /// <summary>BattleView가 매 프레임 (할당 없음): 응원 상태 · 남은 시간 · 숨김 규칙</summary>
        public void Tick(bool asleep)
        {
            Wire();
            var cheer = Game.IsReady && Game.IsMetaActive ? Game.Cheer : null;
            bool active = cheer != null && cheer.IsActive;
            bool tutorial = Game.IsReady && Game.Tutorial != null && Game.Tutorial.IsActive;
            bool show = active && !tutorial && !asleep;
            SetVisible(show);
            if (!show || _time == null) return;
            int seconds = Mathf.Max(0, Mathf.CeilToInt(cheer.Remaining));
            if (seconds == _shownSeconds) return;
            _shownSeconds = seconds;
            int length = FormatClock(seconds, _clock);
            _time.SetCharArray(_clock, 0, length);
        }

        readonly char[] _clock = new char[8];

        /// <summary>'m:ss' (분은 자릿수 그대로, 초는 두 자리) — 할당 없이 buffer에 쓰고 길이를 돌려준다 (순수)</summary>
        public static int FormatClock(int seconds, char[] buffer)
        {
            seconds = Mathf.Max(0, seconds);
            int minutes = Mathf.Min(seconds / 60, 9999);
            int s = seconds % 60;
            int n = 0;
            if (minutes >= 1000) buffer[n++] = (char)('0' + minutes / 1000 % 10);
            if (minutes >= 100) buffer[n++] = (char)('0' + minutes / 100 % 10);
            if (minutes >= 10) buffer[n++] = (char)('0' + minutes / 10 % 10);
            buffer[n++] = (char)('0' + minutes % 10);
            buffer[n++] = ':';
            buffer[n++] = (char)('0' + s / 10);
            buffer[n++] = (char)('0' + s % 10);
            return n;
        }

        void SetVisible(bool on)
        {
            if (_visible == on) return;
            _visible = on;
            var target = _root != null ? _root.gameObject : gameObject;
            if (target.activeSelf != on) target.SetActive(on);
            if (!on) _shownSeconds = -1;
        }

        void OnTap()
        {
            var ui = UIManager.Instance;
            if (ui != null) ui.ShowToast(TapToast, ToastKind.Info);
        }
    }
}
