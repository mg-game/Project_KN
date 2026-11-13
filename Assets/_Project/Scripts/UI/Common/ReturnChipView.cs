using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 돌아가기 칩 (M72, UX-SPEC §2.1.16 · §3.14). 다른 화면으로 건너뛴 뒤(재료 모으러 가기, 일지 [가기] 등) 전투 화면 오른쪽 아래에
    /// "‹ 수선 일지로" 칩을 띄워 한 번 눌러 원래 팝업으로 돌아가게 한다. 자리는 하나뿐이다 (새로 오면 바뀐다, 쌓지 않는다, 시간 제한 없음).
    ///   · 칸(돌아갈 곳)은 UINav가 가진다: PushReturn / TryPopReturn / ClearReturn, 바뀌면 ReturnChanged. 이 칩은 그것만 비춘다.
    ///     칩 본체를 누르면 UINav.TryPopReturn (칸을 먼저 비우고 원래 팝업을 다시 연다 — 실패 토스트도 UINav 몫),
    ///     ×(48x48)를 누르면 UINav.ClearReturn. 다른 탭 버튼을 직접 누르면 MainTabController가 비운다.
    ///   · 칸이 차 있어도 팝업이 열려 있거나, 튜토리얼 중이거나, 코치마크(안내 · 손가락)가 보이면 숨는다 — 지우지 않는다 (조건이 풀리면 다시 보인다).
    ///   · 나타날 때 0.2초 페이드 + 1.06배 한 번 (반복 없음, 움직임 줄이기면 페이드만). 다른 곳으로 바뀌면 한 번 더 튄다. 숨을 때 0.12초 페이드.
    ///     누름은 보일 때만 받는다.
    ///   · ReturnChanged는 Awake ~ OnDestroy 동안 듣는다 (쉬는 동안에도). 칸이 비고 다 숨으면 Update를 끈다 —
    ///     찬 동안만 숨김 조건을 매 프레임 확인한다 (팝업 수 · 플래그 몇 개, 할당 없음).
    /// 빌더(WP-3a, IdleSceneBuilder)가 BattleSceneBuilder.Build 뒤에 StageArea 아래 "ReturnChip"으로 만든다:
    /// 앵커 · 피벗 (1, 0), (-24, 196), 360x64, PanelLight 둥근 판 + 2px GoldAccent 테두리, 이름 26 Cream (자동 크기 최소 20, 왼쪽 여백 20),
    /// 오른쪽 × 48x48 (글자 22). 이 컴포넌트와 CanvasGroup은 칩 루트에 두고 루트는 켜 둔다 (보임은 CanvasGroup으로 정한다).
    /// 이름은 보내는 쪽이 '‹'까지 넣는다 — 없으면 앞에 붙인다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ReturnChipView : MonoBehaviour
    {
        const char ChevronChar = '‹';
        const string ChevronPrefix = "‹ ";
        /// <summary>숨을 때 페이드 (초)</summary>
        const float FadeOut = 0.12f;
        /// <summary>나타날 때 한 번 1 → 1.06 → 1 (초)</summary>
        const float PulseTime = 0.3f;

        [Tooltip("칩 본체 버튼 (누르면 돌아간다)")]
        [SerializeField] Button _button;
        [SerializeField] TMP_Text _label;
        [Tooltip("오른쪽 × (48x48 누르는 자리) — 누르면 돌아갈 곳을 지운다")]
        [SerializeField] Button _closeButton;
        [Tooltip("한 번 튀길 대상 (비우면 이 오브젝트)")]
        [SerializeField] RectTransform _pulseTarget;
        [Tooltip("코치마크 (보이는 동안 숨는다). 비우면 처음 한 번 장면에서 찾는다")]
        [SerializeField] CoachMarkView _coach;

        CanvasGroup _group;
        bool _visible;
        float _alphaFrom;
        float _alphaTo;
        float _fadeTime = -1f;
        float _fadeDuration;
        float _pulse = -1f;
        /// <summary>칸이 채워질 때마다 하나씩 (새 칸이면 다시 한 번 튀긴다)</summary>
        int _version;
        int _pulsedVersion = -1;
        Vector3 _baseScale = Vector3.one;
        bool _coachSearched;

        /// <summary>칩이 지금 보이는지 (칸이 차 있고 숨김 조건이 없음)</summary>
        public bool IsVisible => _visible;

        // ───────────────────────────── 수명 ─────────────────────────────

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_pulseTarget == null) _pulseTarget = (RectTransform)transform;
            _baseScale = _pulseTarget.localScale;
            if (_button != null) _button.onClick.AddListener(OnTap);
            if (_closeButton != null) _closeButton.onClick.AddListener(OnDismiss);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _visible = false;
            UINav.ReturnChanged += OnReturnChanged;
            OnReturnChanged();
        }

        void OnDestroy() => UINav.ReturnChanged -= OnReturnChanged;

        void OnDisable()
        {
            if (_pulseTarget != null) _pulseTarget.localScale = _baseScale;
            _pulse = -1f;
        }

        /// <summary>칸이 바뀜 (넣음 · 비움 · 바꿈)</summary>
        void OnReturnChanged()
        {
            if (UINav.HasReturn)
            {
                _version++;
                if (_label != null) _label.text = WithChevron(UINav.ReturnLabel);
                // 보이는 중에 다른 곳으로 바뀌면 바뀐 것을 알리게 한 번 더 튀긴다
                if (_visible) StartPulse();
            }
            else if (_visible)
            {
                // 누름을 바로 막는다 (사라지는 동안 두 번 눌리지 않게)
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }
            enabled = UINav.HasReturn || _visible || _fadeTime >= 0f;
        }

        static string WithChevron(string label)
        {
            if (string.IsNullOrEmpty(label)) return string.Empty;
            return label[0] == ChevronChar ? label : ChevronPrefix + label;
        }

        void OnTap()
        {
            if (!_visible || !UINav.HasReturn) return;
            UINav.TryPopReturn();
        }

        void OnDismiss()
        {
            if (!_visible) return;
            UINav.ClearReturn();
        }

        // ───────────────────────────── 프레임 ─────────────────────────────

        void Update()
        {
            bool has = UINav.HasReturn;
            bool want = has && !Blocked();
            if (want != _visible) SetVisible(want);

            float dt = Time.unscaledDeltaTime;
            bool busy = StepFade(dt);
            busy |= StepPulse(dt);
            if (!has && !_visible && !busy) enabled = false;
        }

        /// <summary>팝업 · 튜토리얼 · 코치마크가 있으면 숨는다</summary>
        bool Blocked()
        {
            var ui = UIManager.Instance;
            if (ui != null && ui.HasOpenPopup) return true;
            if (Game.IsReady && Game.Tutorial != null && Game.Tutorial.IsActive) return true;
            var coach = Coach();
            return coach != null && ((coach.IsActive && !coach.Suppressed) || (coach.FocusTarget != null && !coach.FocusSuppressed));
        }

        CoachMarkView Coach()
        {
            if (_coach == null && !_coachSearched)
            {
                _coachSearched = true;
                _coach = FindFirstObjectByType<CoachMarkView>(FindObjectsInactive.Include);
            }
            return _coach;
        }

        void SetVisible(bool visible)
        {
            _visible = visible;
            _alphaFrom = _group.alpha;
            _alphaTo = visible ? 1f : 0f;
            _fadeTime = 0f;
            _fadeDuration = visible ? UIMotion.ReturnChipFade : FadeOut;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
            if (visible && _pulsedVersion != _version) StartPulse();
        }

        void StartPulse()
        {
            _pulsedVersion = _version;
            if (UIMotion.Reduced) return;
            _pulse = 0f;
        }

        bool StepFade(float dt)
        {
            if (_fadeTime < 0f) return false;
            _fadeTime += dt;
            float k = _fadeDuration > 0f ? Mathf.Clamp01(_fadeTime / _fadeDuration) : 1f;
            _group.alpha = Mathf.Lerp(_alphaFrom, _alphaTo, UIMotion.OutCubic(k));
            if (k < 1f) return true;
            _fadeTime = -1f;
            return false;
        }

        bool StepPulse(float dt)
        {
            if (_pulse < 0f) return false;
            _pulse += dt;
            float k = Mathf.Clamp01(_pulse / PulseTime);
            _pulseTarget.localScale = _baseScale * (1f + (UIMotion.ReturnChipPulseScale - 1f) * Mathf.Sin(k * Mathf.PI));
            if (k < 1f) return true;
            _pulseTarget.localScale = _baseScale;
            _pulse = -1f;
            return false;
        }
    }
}
