using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 첫 만남 확정 소환 카드 (FTUE 10단계). 소환 탭 화면 위(ContentArea 자리)를 덮고
    /// "누군가 수선점 문을 두드려요…" + 실루엣 + [문 열어 주기 (무료)]를 보여 준다. 덮개가 소환 버튼을 막아
    /// 별조각 소환을 먼저 눌러 확정 만남을 놓치는 일이 없다.
    /// 누르면 OpenPressed → FtueDirector가 GachaSystem.PullGuaranteed를 부르고 결과 팝업을 띄운다.
    /// 결과 팝업이 열려 있는 동안 화면 위쪽에 "첫 만남 확정 소환" 리본(공개 확률·천장 계산 제외 안내)을 띄운다.
    /// 탭 화면은 시트라 열리는 0.30초 동안 올라온다 (UX-SPEC-IA §5.7): 떠 있는 동안 덮을 자리(_coverArea)가 움직였으면 다시 맞춘다 —
    /// LateUpdate라 시트가 이번 프레임 자리를 쓴 뒤에 따라가고, 자리가 그대로면 맞추지 않는다 (할당 없음).
    /// 튜토리얼 중 떠 있는 동안 뒤로가기를 먹는다 (M15): 토스트 '먼저 새 친구를 만나 볼까요?'(2초에 한 번) + 카드가 바로 한 번 더 문을 두드린다.
    /// 건너뛴 뒤에도 카드는 남지만(M74 3b) 그때 뒤로가기는 평소대로 소환 시트를 닫는다 (카드도 함께 내려간다).
    /// 처음 뜰 때 첫 만남 징글(UISfxKey.FirstMeeting, 이 장면에서 한 번 — 탭을 오가며 다시 떠도 되풀이하지 않는다).
    /// 움직임 줄이기면 튀어나옴 · 두드림 · 빛 숨 쉬기 없이 알파만.
    /// 덮개(카드 뒤 어둠)는 거의 불투명(CoverAlpha 0.94)하다 — 카드(860) 양옆으로 소환 시트 글('잃…' · '새로' · '1%' · 할인 · 별)이
    /// 반쯤 비쳐 어수선했다 (WP-8 r1 18 화면). 이 값 전에 빌드한 장면(덮개 0.84)도 Awake에서 올린다.
    /// </summary>
    public class FirstMeetingCardView : MonoBehaviour
    {
        [SerializeField] RectTransform _root;
        [SerializeField] CanvasGroup _group;
        [SerializeField] RectTransform _card;
        [SerializeField] Image _silhouette;
        [SerializeField] Image _glow;
        [SerializeField] TMP_Text _hintText;
        [SerializeField] Button _openButton;
        [Tooltip("이 영역에 맞춰 덮는다 (탭 패널 영역)")]
        [SerializeField] RectTransform _coverArea;

        [Header("결과 리본")]
        [SerializeField] RectTransform _ribbon;
        [SerializeField] CanvasGroup _ribbonGroup;

        static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>뒤로가기 안내 (M15) — 카드가 떠 있는 동안 뒤로가기를 먹고 이 토스트를 BackToastInterval(2초)에 한 번</summary>
        internal const string BackToast = "먼저 새 친구를 만나 볼까요?";
        internal const float BackToastInterval = 2f;
        /// <summary>
        /// 덮개 알파 (카드 뒤 어둠, 0.85 이상 — 시트 글이 카드 양옆으로 비치지 않게). 빌더도 쓴다.
        /// 0.94면 크림색 글이 덮개 밑에서 약 6%만 남아 보이지 않고, 시트 틀(머리 · 손잡이)은 덮개 밖이라 '잠깐 덮은 창'으로 읽힌다
        /// </summary>
        public const float CoverAlpha = 0.94f;
        /// <summary>두드림 주기 (2초마다 두 번 톡톡)</summary>
        const float KnockPeriod = 2f;

        float _time;
        bool _shown;
        bool _ribbonShown;
        float _ribbonAlpha;
        /// <summary>마지막으로 맞춘 때 덮을 자리의 위치 (월드)</summary>
        Vector3 _fittedAt;
        /// <summary>두드림 기준 시각 (_time) — 뒤로가기를 누르면 지금으로 옮겨 바로 한 번 두드린다</summary>
        float _knockFrom;
        Func<bool> _backHandler;
        bool _backRegistered;
        float _lastBackToast = float.NegativeInfinity;
        bool _jinglePlayed;

        /// <summary>[문 열어 주기]를 누름</summary>
        public event Action OpenPressed;

        /// <summary>카드가 떠 있음</summary>
        public bool IsShown => _shown;

        /// <summary>[문 열어 주기] 버튼 (손가락 대상)</summary>
        public RectTransform OpenButton => _openButton != null ? (RectTransform)_openButton.transform : null;

        void Awake()
        {
            if (_root == null) _root = (RectTransform)transform;
            // 이 값 전에 빌드한 장면은 덮개가 옅다 (0.84) — 다시 빌드하지 않아도 시트 글이 비치지 않게 한 번 올린다
            if (_root.TryGetComponent<Image>(out var cover) && cover.color.a < CoverAlpha)
            {
                var c = cover.color;
                cover.color = new Color(c.r, c.g, c.b, CoverAlpha);
            }
            if (_openButton != null) _openButton.onClick.AddListener(() => OpenPressed?.Invoke());
            _backHandler = OnBackPressed;
            _root.gameObject.SetActive(false);
            if (_ribbon != null) _ribbon.gameObject.SetActive(false);
        }

        void OnDestroy() => RegisterBack(false);

        /// <summary>뒤로가기 토스트를 다시 띄울 때인지 (마지막 토스트에서 BackToastInterval이 지남 — 테스트용 순수 함수)</summary>
        internal static bool BackToastDue(float now, float lastToast) => now - lastToast >= BackToastInterval;

        /// <summary>
        /// 뒤로가기 처리기 (M15): 튜토리얼 중에 카드가 떠 있으면 먹는다 — 첫 만남 단계에서 시트가 닫혀 확정 소환 흐름을 놓치지 않게.
        /// 토스트는 2초에 한 번, 카드는 매번 바로 한 번 두드린다 ("여기예요").
        /// 튜토리얼이 꺼진 뒤(건너뜀 · 옛 세이브 — M74 3b로 카드가 남는다)에는 먹지 않는다: 뒤로가기가 평소대로 소환 시트를 닫고
        /// (UX-SPEC-IA §4.7 ④ → ⑤ 종료 확인) 카드도 함께 내려간다 (MAIN에는 덮을 자리가 없다 — 소환 시트를 다시 열면 다시 뜬다).
        /// 종료 확인은 SystemPopups 층이라 카드보다 위에 떠서 카드 밑에 숨지 않는다.
        /// </summary>
        bool OnBackPressed()
        {
            if (!_shown || !isActiveAndEnabled || !TutorialRunning) return false;
            float now = Time.unscaledTime;
            if (BackToastDue(now, _lastBackToast))
            {
                _lastBackToast = now;
                if (UIManager.Instance != null) UIManager.Instance.ShowToast(BackToast, ToastKind.Info);
            }
            _knockFrom = _time;
            return true;
        }

        /// <summary>튜토리얼이 돌고 있음 (뒤로가기를 먹는 동안 — 건너뛰었거나 끝났으면 false)</summary>
        static bool TutorialRunning => Game.IsReady && Game.Tutorial != null && Game.Tutorial.IsActive;

        /// <summary>뒤로 처리기를 넣거나 뺀다 (떠 있는 동안만 — 같은 대리자 인스턴스)</summary>
        void RegisterBack(bool on)
        {
            if (on == _backRegistered) return;
            var ui = UIManager.Instance;
            if (on)
            {
                if (ui == null) return;
                _backHandler ??= OnBackPressed;
                ui.PushBackHandler(_backHandler);
                _backRegistered = true;
                return;
            }
            _backRegistered = false;
            if (ui != null && _backHandler != null) ui.RemoveBackHandler(_backHandler);
        }

        /// <summary>카드를 띄운다 (lineage = 확정으로 만날 친구, 실루엣으로만 보인다)</summary>
        public void Show(LineageDef lineage, Sprite portrait)
        {
            if (_silhouette != null)
            {
                _silhouette.sprite = portrait;
                _silhouette.enabled = portrait != null;
                _silhouette.color = new Color(0.1f, 0.07f, 0.22f, 0.92f);
            }
            if (_glow != null && lineage != null)
            {
                var main = lineage.mainColor;
                _glow.color = new Color(main.r, main.g, main.b, 0.7f);
            }
            if (_hintText != null)
                _hintText.text = lineage != null ? $"{UITheme.Stars(lineage.grade)} 기다림 끝에 찾아온 친구" : "기다림 끝에 찾아온 친구";
            if (!_shown)
            {
                _time = 0f;
                _knockFrom = 0f;
                _shown = true;
                _root.gameObject.SetActive(true);
                if (_group != null) _group.alpha = 0f;
                RegisterBack(true);
                if (!_jinglePlayed)
                {
                    // 수줍은 오르골 "반가워요" (탭을 오가며 다시 떠도 이 장면에서 한 번만)
                    _jinglePlayed = true;
                    UISfx.Play(UISfxKey.FirstMeeting);
                }
            }
            Fit();
        }

        /// <summary>카드를 숨긴다</summary>
        public void Hide()
        {
            _shown = false;
            RegisterBack(false);
            _root.gameObject.SetActive(false);
        }

        /// <summary>결과 팝업이 열려 있는 동안 리본. window가 있으면 그 창의 위쪽 변에 걸치게 놓는다</summary>
        public void SetRibbon(bool on, RectTransform window = null)
        {
            if (_ribbon == null) return;
            if (on && window != null)
            {
                var parent = (RectTransform)_ribbon.parent;
                var rect = UIFind.RectIn(window, parent, Corners);
                _ribbon.anchorMin = _ribbon.anchorMax = new Vector2(0.5f, 0.5f);
                _ribbon.anchoredPosition = new Vector2(rect.center.x, rect.yMax + 4f) - parent.rect.center;
            }
            if (_ribbonShown == on) return;
            _ribbonShown = on;
            if (on)
            {
                _ribbon.gameObject.SetActive(true);
                _ribbon.SetAsLastSibling();
            }
        }

        void Fit()
        {
            if (_coverArea == null) return;
            _fittedAt = _coverArea.position;
            var parent = (RectTransform)_root.parent;
            var rect = UIFind.RectIn(_coverArea, parent, Corners);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = rect.size;
            _root.anchoredPosition = rect.center - parent.rect.center;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            StepRibbon(dt);
            if (!_shown) return;
            // 시트가 미끄러지는 동안 카드가 탭 화면 자리를 따라간다 (멈춰 있으면 맞추지 않는다)
            if (_coverArea != null && _coverArea.position != _fittedAt) Fit();
            _time += dt;
            if (_group != null) _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, dt / 0.25f);
            bool reduced = UIMotion.Reduced;
            if (_card != null)
            {
                _card.localScale = Vector3.one * (!reduced && _time < 0.35f ? Mathf.Lerp(0.8f, 1f, UIMotion.OutBack(_time / 0.35f)) : 1f);
                // 문 두드리는 느낌: 2초마다 두 번 톡톡 (뒤로가기를 누르면 그 순간부터 다시)
                float knock = Mathf.Repeat(_time - _knockFrom, KnockPeriod);
                float shake = !reduced && knock < 0.3f ? Mathf.Sin(knock / 0.3f * Mathf.PI * 4f) * (1f - knock / 0.3f) : 0f;
                _card.localRotation = Quaternion.Euler(0f, 0f, shake * 2.2f);
            }
            if (_glow != null)
            {
                float pulse = reduced ? 1f : 0.75f + 0.25f * Mathf.Sin(_time * 3.1f);
                _glow.rectTransform.localScale = Vector3.one * (0.95f + 0.08f * pulse);
            }
        }

        void StepRibbon(float dt)
        {
            if (_ribbon == null) return;
            _ribbonAlpha = Mathf.MoveTowards(_ribbonAlpha, _ribbonShown ? 1f : 0f, dt / 0.2f);
            if (_ribbonGroup != null) _ribbonGroup.alpha = _ribbonAlpha;
            if (!_ribbonShown && _ribbonAlpha <= 0f && _ribbon.gameObject.activeSelf) _ribbon.gameObject.SetActive(false);
        }
    }
}
