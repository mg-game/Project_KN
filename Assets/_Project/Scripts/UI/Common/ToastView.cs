using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 토스트 (M14, UX-SPEC §3.5). 잠깐 떴다 사라지는 안내 (두 줄까지, 넘치면 말줄임 — 글 상자 설정은 빌더 몫).
    ///   · 종류(ToastKind): Info = 라벤더 점 / Success = Positive 체크 / Error = Negative '!' + 왼쪽 6px 막대.
    ///     Error는 오류 소리(UISfx Error — Deny 소리를 빌려 쓰고 볼륨만 따로) + 약한 진동(Haptics Light)을 낸다 — 새로 보이거나 줄에 든 오류만 (0.3초에 한 번까지).
    ///     Info · Success는 토스트가 실제로 나타날 때(줄에서 차례가 올 때) Toast 소리 하나 (진동 없음, AUDIO-SPEC §8.1).
    ///     합쳐진 같은 토스트(아래)는 튀기기만 하고 소리 · 진동이 없다 (길게 누르는 동안 반복마다 떨지 않게 — §3.11).
    ///   · 줄: 기다리는 토스트 3개까지 (넘치면 가장 오래된 '오류 아닌' 것을 버린다 — 다 오류면 가장 오래된 것).
    ///     보이는 토스트나 기다리는 토스트와 같은 글 · 같은 종류가 다시 오면 줄에 넣지 않는다.
    ///     보이는 것이면 1 → 1.06 → 1로 툭 튀기고, 기다리는 토스트가 없을 때만 남은 시간을 1초 이상으로 늘린다
    ///     (사라지는 중이면 다시 또렷해진다). 기다리는 토스트가 있으면 늘리지도 되살리지도 않는다 — 같은 글이 계속 와도 뒤 토스트가 밀리지 않게.
    ///   · 머무는 시간: Info · Success 1.4초, Error 1.8초, 20자를 넘는 글자마다 +0.04초 (최대 3초).
    ///     뒤에 기다리는 토스트가 있으면 지금 토스트의 남은 시간을 0.6초 이하로 줄인다.
    ///   · 들어옴 0.15초 (알파 + 24px 아래에서 올라옴, OutCubic — 움직임 줄이기면 알파만) / 나감 0.20초 (알파). 모두 unscaled 시간.
    ///   · 자리: TopWindow(맨 위 팝업 창을 돌려주는 함수, UIManager가 연결)가 있으면 토스트가 나타날 때마다 Place로 정한다 —
    ///     팝업 없음 = 안전 영역 위에서 480 / 창 위가 160 이상 비었으면 창 위 20 / 아니면 창 안 위에서 12 (폭 min(720, 창 폭 − 240)).
    ///     팝업 없음인데 MAIN 빠른 아이콘 줄이 보이면 (시트가 다 열리지 않았고 QuickRail_L · R에 켜진 칸이 있음 — RailsVisible) 양옆을 128씩 비운다
    ///     (1080에서 x 128–952 — 이벤트 칸 12–116을 덮지 않게). 시트(다 열림) · 팝업 위에서는 그대로 880 (100–980).
    ///     창 크기는 열고 닫는 연출의 배율(0.90 → 1)을 뺀 원래 크기로 잰다. 들어오는 동안(0.15초) 맨 위 창이 바뀌면
    ///     (같은 프레임에 팝업이 열림 등) 한 번 더 놓는다 — 머무는 동안에는 옮기지 않는다.
    ///     TopWindow가 없으면 빌더(또는 부르는 쪽)가 놓은 자리를 그대로 쓴다.
    ///   · 누름을 받지 않는다 (CanvasGroup blocksRaycasts 끔). 할 일이 없으면 Update를 끈다. 글은 토스트가 바뀔 때만 쓴다.
    /// 빌더(WP-3a)는 ToastLayer(안전 영역) 맨 뒤 형제로 위쪽 가운데 피벗, 폭 880, ContentSizeFitter 높이 88~140으로 만들고
    /// _text(28 Text, 줄바꿈, 자동 크기 없음, 2줄째 말줄임) · _kindIcon(40x40, 왼쪽 24) · _kindGlyph · _errorBar(6px)를 연결한다.
    /// 부르는 곳은 UIManager.ShowToast(글, 종류) 하나다 (Show(string)은 Info).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastView : MonoBehaviour
    {
        /// <summary>기다리는 토스트 최대 수 (보이는 것 빼고)</summary>
        public const int QueueCapacity = 3;

        const float HoldInfo = 1.4f;
        const float HoldError = 1.8f;
        const int HoldFreeChars = 20;
        const float HoldPerChar = 0.04f;
        const float HoldMax = 3f;
        /// <summary>뒤에 기다리는 토스트가 있을 때 지금 토스트에 남기는 시간 최대</summary>
        const float HoldWhenQueued = 0.6f;
        /// <summary>같은 토스트가 다시 왔을 때 남은 시간 최소</summary>
        const float HoldAfterRepeat = 1f;
        /// <summary>오류 소리 · 진동 최소 간격</summary>
        const float ErrorFeedbackGap = 0.3f;

        // ── 자리 (§3.5) ──
        /// <summary>창 위에 놓으려면 안전 영역 위에서 창 위까지 이만큼 비어 있어야 한다 (토스트 최대 140 + 간격 20)</summary>
        const float RoomAboveWindow = 160f;
        const float GapAboveWindow = 20f;
        const float InsideWindowTop = 12f;
        const float InsideWindowMaxWidth = 720f;
        /// <summary>창 안에 놓을 때 좌우로 비우는 폭 합 (닫기 X가 보이게)</summary>
        const float InsideWindowSideRoom = 240f;
        const float InsideWindowMinWidth = 440f;
        /// <summary>
        /// MAIN 빠른 아이콘 줄이 보이는 동안 팝업 없는 토스트의 양옆을 비우는 폭 (줄 끝 12 + 칸 104 + 틈 12 = 128 — 1080에서 x 128–952)
        /// </summary>
        public const float RailInset = UISize.RailEdge + UISize.RailCellWidth + UISize.RailEdge;
        /// <summary>시트가 이만큼 열리면 빠른 아이콘 줄이 사라진 것으로 본다 (UIFrame.SheetProgress — 다 열림 = 1)</summary>
        const float RailsGoneProgress = 0.999f;
        /// <summary>빠른 아이콘 줄 (StageArea 기준 경로 — CoachTargets와 같은 이름, UX-SPEC-IA §5.8 경로 고정)</summary>
        const string RailLeftPath = "Battle/MetaHud/QuickRail_L";
        const string RailRightPath = "Battle/MetaHud/QuickRail_R";

        const string SuccessGlyph = "✓";
        const string ErrorGlyph = "!";

        [SerializeField] TMP_Text _text;
        [Tooltip("종류 아이콘 (40x40, 왼쪽 24): Info 라벤더 점 / Success Positive 체크 / Error Negative '!'")]
        [SerializeField] Image _kindIcon;
        [Tooltip("아이콘 위 글자 (Success '✓' · Error '!') — 그 종류의 그림(_successSprite 등)이 없을 때만 쓴다. 없어도 된다")]
        [SerializeField] TMP_Text _kindGlyph;
        [Tooltip("Error일 때만 켜지는 왼쪽 6px 막대 (색은 빌더가 Negative로 칠한다)")]
        [SerializeField] Graphic _errorBar;
        [Tooltip("종류별 아이콘 그림 (비우면 _kindIcon의 그림을 그대로 쓰고 색만 바꾼다)")]
        [SerializeField] Sprite _infoSprite;
        [SerializeField] Sprite _successSprite;
        [SerializeField] Sprite _errorSprite;
        [Tooltip("아이콘을 종류 색(Lavender · Positive · Negative)으로 물들인다 — 흰 틴트용 아트일 때. 색이 있는 아트면 끈다")]
        [SerializeField] bool _tintKindIcon = true;

        struct Entry
        {
            public string Text;
            public ToastKind Kind;
        }

        enum Phase
        {
            Idle = 0,
            In = 1,
            Hold = 2,
            Out = 3,
        }

        static readonly Vector3[] Corners = new Vector3[4];

        CanvasGroup _group;
        RectTransform _rect;
        readonly List<Entry> _queue = new(QueueCapacity + 1);
        Entry _current;
        Phase _phase;
        float _time;
        float _holdLeft;
        float _fadeFrom;
        bool _slide;
        float _punch = -1f;
        Vector2 _rest;
        float _lastErrorFeedback = float.NegativeInfinity;
        /// <summary>지금 자리를 정할 때 기준으로 삼은 창 (팝업 없음이면 null) — 들어오는 동안 맨 위 창이 바뀌었는지 본다</summary>
        RectTransform _placedWindow;
        /// <summary>새로 나타나 들어오는 중 (이때만 맨 위 창이 바뀌면 다시 놓는다 — 되살아나는 토스트는 옮기지 않는다)</summary>
        bool _settling;
        /// <summary>빠른 아이콘 줄을 찾은 StageArea (바뀌면 다시 찾는다) · 찾은 줄 (없으면 null)</summary>
        RectTransform _railStage;
        Transform _railLeft;
        Transform _railRight;

        /// <summary>
        /// 맨 위 팝업 창 (없으면 null) — UIManager가 연결한다. 있으면 토스트가 나타날 때마다 Place(TopWindow())로 자리를 정한다.
        /// 비어 있으면 자리를 건드리지 않는다.
        /// </summary>
        public Func<RectTransform> TopWindow { get; set; }

        /// <summary>
        /// MAIN 빠른 아이콘 줄이 보이는지 — 참이면 팝업 없는 토스트를 양옆 RailInset만큼 줄인다 (Place). 비어 있으면 스스로 본다:
        /// 시트가 다 열리지 않았고(UIFrame.SheetProgress &lt; 1) StageArea의 QuickRail_L · R에 켜진 칸(QuickIconView)이 있다
        /// </summary>
        public Func<bool> RailsVisible { get; set; }

        /// <summary>토스트가 보이는 중 (들어옴 · 머무름 · 나감)</summary>
        public bool IsShowing => _phase != Phase.Idle;

        /// <summary>보이는 토스트의 글 (없으면 null)</summary>
        public string CurrentText => _phase != Phase.Idle ? _current.Text : null;

        /// <summary>보이는 토스트의 종류</summary>
        public ToastKind CurrentKind => _current.Kind;

        /// <summary>기다리는 토스트 수</summary>
        public int QueuedCount => _queue.Count;

        void Awake()
        {
            EnsureInit();
            if (_phase == Phase.Idle && _punch < 0f) enabled = false;
        }

        void EnsureInit()
        {
            if (_group != null) return;
            _group = GetComponent<CanvasGroup>();
            _rect = (RectTransform)transform;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _rest = _rect.anchoredPosition;
            if (_errorBar != null) _errorBar.gameObject.SetActive(false);
        }

        // ───────────────────────────── 공개 API ─────────────────────────────

        /// <summary>안내 토스트 (Info)</summary>
        public void Show(string message) => Show(message, ToastKind.Info);

        /// <summary>토스트 하나를 띄운다 (보이는 것이 있으면 줄에 넣는다). 같은 글 · 같은 종류는 하나로 합친다</summary>
        public void Show(string message, ToastKind kind)
        {
            if (string.IsNullOrEmpty(message)) return;
            EnsureInit();
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            // 같은 글 · 같은 종류는 합친다 (소리 · 진동 없이 — 처음 보이거나 줄에 들 때 이미 냈다)
            if (_phase != Phase.Idle && _current.Kind == kind && string.Equals(_current.Text, message, StringComparison.Ordinal))
            {
                Repeat();
                return;
            }
            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Kind == kind && string.Equals(_queue[i].Text, message, StringComparison.Ordinal)) return;
            }

            if (kind == ToastKind.Error) ErrorFeedback();
            if (_queue.Count >= QueueCapacity) _queue.RemoveAt(OldestDroppable());
            _queue.Add(new Entry { Text = message, Kind = kind });

            if (_phase == Phase.Idle) ShowNext();
            else if (_phase != Phase.Out) _holdLeft = Mathf.Min(_holdLeft, HoldWhenQueued);
            enabled = true;
        }

        /// <summary>보이는 토스트와 줄을 모두 지운다 (바로 숨김 — 테스트 · 장면 정리용)</summary>
        public void Clear()
        {
            EnsureInit();
            _queue.Clear();
            if (_slide) _rect.anchoredPosition = _rest;
            Finish();
            enabled = false;
        }

        /// <summary>
        /// §3.5 자리 규칙대로 놓는다 (window = 맨 위 팝업 창, 없거나 꺼져 있으면 팝업 없음). 부모(ToastLayer · 안전 영역) 좌표로 계산하고
        /// 앵커 · 피벗을 위쪽 가운데로 바꾼다. 창 위에 놓을 때는 지금 글로 높이를 다시 잰다 (ContentSizeFitter).
        /// </summary>
        public void Place(RectTransform window)
        {
            EnsureInit();
            var parent = _rect.parent as RectTransform;
            if (parent == null) return;
            var topCenter = new Vector2(0.5f, 1f);
            _rect.anchorMin = topCenter;
            _rect.anchorMax = topCenter;
            _rect.pivot = topCenter;

            var area = parent.rect;
            float width = UISize.ToastWidth;
            float x = 0f;
            float fromTop = UISize.ToastTop;
            _placedWindow = window;
            if (window != null && window.gameObject.activeInHierarchy)
            {
                // 열리는 중(0.90 → 1배)이어도 다 열린 창 크기로 잰다 (아니면 창 위 토스트가 자리 잡은 창 제목을 덮는다)
                var box = UIFind.UnscaledRectIn(window, parent, Corners);
                float windowTop = area.yMax - box.yMax;
                if (windowTop >= RoomAboveWindow)
                {
                    // 창 위가 넉넉하다: 토스트 아래 끝이 창 위 20px 위
                    SetWidth(width);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
                    fromTop = windowTop - GapAboveWindow - _rect.rect.height;
                }
                else
                {
                    // 키 큰 창: 창 안 제목 줄 위에 (닫기 X는 보이게)
                    width = Mathf.Max(InsideWindowMinWidth, Mathf.Min(InsideWindowMaxWidth, box.width - InsideWindowSideRoom));
                    x = box.center.x - area.center.x;
                    fromTop = Mathf.Max(0f, windowTop) + InsideWindowTop;
                }
            }
            else if (AreRailsVisible())
            {
                // MAIN: 빠른 아이콘 줄 칸(이벤트 12–116 등)을 덮지 않게 양옆을 비운다 (가운데는 그대로)
                width = MainWidth(area.width, true);
            }
            SetWidth(width);
            _rect.anchoredPosition = new Vector2(x, -Mathf.Max(0f, fromTop));
            _rest = _rect.anchoredPosition;
        }

        /// <summary>팝업 없는 토스트 폭: 빠른 아이콘 줄이 보이면 영역 폭 − 2×RailInset (440 ~ 880 — 1080이면 824 = x 128–952), 아니면 880</summary>
        internal static float MainWidth(float areaWidth, bool railsShown) =>
            railsShown ? Mathf.Clamp(areaWidth - 2f * RailInset, InsideWindowMinWidth, UISize.ToastWidth) : UISize.ToastWidth;

        /// <summary>빠른 아이콘 줄이 보이는지 (순수 — 시험용): 줄에 켜진 칸이 있고 시트가 다 열리지 않았다</summary>
        internal static bool RailsShown(float sheetProgress, bool railCellActive) => railCellActive && sheetProgress < RailsGoneProgress;

        bool AreRailsVisible() => RailsVisible != null ? RailsVisible() : RailsShown(UIFrame.SheetProgress, RailCellActive());

        /// <summary>QuickRail_L · R 중 하나에 켜진 칸이 있는지 (StageArea가 바뀔 때만 줄을 다시 찾는다 — 토스트가 놓일 때만 불린다, 할당 없음)</summary>
        bool RailCellActive()
        {
            var stage = UIFrame.StageRect;
            if (stage == null) return false;
            if (stage != _railStage)
            {
                _railStage = stage;
                _railLeft = stage.Find(RailLeftPath);
                _railRight = stage.Find(RailRightPath);
            }
            return HasActiveCell(_railLeft) || HasActiveCell(_railRight);
        }

        /// <summary>줄이 켜져 있고 바로 아래 자식 중 켜진 아이콘 칸(QuickIconView)이 있는지 (잠김 · 넘침 아이콘은 꺼진 채 남는다 — QuickRailView)</summary>
        static bool HasActiveCell(Transform rail)
        {
            if (rail == null || !rail.gameObject.activeInHierarchy) return false;
            for (int i = 0; i < rail.childCount; i++)
            {
                var cell = rail.GetChild(i);
                if (cell.gameObject.activeSelf && cell.TryGetComponent<QuickIconView>(out _)) return true;
            }
            return false;
        }

        // ───────────────────────────── 줄 ─────────────────────────────

        void Repeat()
        {
            bool queued = _queue.Count > 0;
            if (_phase == Phase.Out)
            {
                // 뒤에 기다리는 토스트가 있으면 되살리지 않는다 (그대로 사라지고 다음 차례 — 같은 글이 계속 와도 뒤가 굶지 않게)
                if (queued) return;
                // 사라지는 중이면 그 자리에서 다시 또렷하게
                _fadeFrom = _group.alpha;
                _phase = Phase.In;
                _time = 0f;
                _slide = false;
            }
            // 기다리는 토스트가 있으면 늘리지 않고 0.6초 이하로 둔다 (§3.5 '줄이 있으면 지금 것을 줄인다')
            _holdLeft = queued ? Mathf.Min(_holdLeft, HoldWhenQueued) : Mathf.Max(_holdLeft, HoldAfterRepeat);
            // 튀는 중 앞쪽 절반이면 그대로 (계속 새로 튀기면 떨린다)
            if (_punch < 0f || _punch >= UIMotion.ToastPunch * 0.5f) _punch = 0f;
            enabled = true;
        }

        int OldestDroppable()
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Kind != ToastKind.Error) return i;
            }
            return 0;
        }

        void ShowNext()
        {
            _current = _queue[0];
            _queue.RemoveAt(0);
            if (_text != null) _text.text = _current.Text;
            ApplyKind(_current.Kind);
            if (TopWindow != null) Place(TopWindow());
            _rest = _rect.anchoredPosition;

            _holdLeft = HoldFor(_current);
            if (_queue.Count > 0) _holdLeft = Mathf.Min(_holdLeft, HoldWhenQueued);
            _phase = Phase.In;
            _time = 0f;
            _fadeFrom = 0f;
            _slide = !UIMotion.Reduced;
            _settling = true;
            _punch = -1f;
            _rect.localScale = Vector3.one;
            _rect.SetAsLastSibling();
            // 안내 · 성공은 나타날 때 '스윽' 하나 (오류는 Show에서 이미 Error 소리를 냈다)
            if (_current.Kind != ToastKind.Error) UISfx.Play(UISfxKey.Toast);
        }

        static float HoldFor(in Entry entry)
        {
            float hold = entry.Kind == ToastKind.Error ? HoldError : HoldInfo;
            int extra = entry.Text.Length - HoldFreeChars;
            if (extra > 0) hold += extra * HoldPerChar;
            return Mathf.Min(hold, HoldMax);
        }

        void ApplyKind(ToastKind kind)
        {
            var sprite = kind == ToastKind.Error ? _errorSprite : kind == ToastKind.Success ? _successSprite : _infoSprite;
            if (_kindIcon != null)
            {
                if (sprite != null && _kindIcon.sprite != sprite) _kindIcon.sprite = sprite;
                var color = !_tintKindIcon ? Color.white
                    : kind == ToastKind.Error ? UITheme.Negative
                    : kind == ToastKind.Success ? UITheme.Positive
                    : UITheme.Lavender;
                if (_kindIcon.color != color) _kindIcon.color = color;
            }
            if (_kindGlyph != null)
            {
                string glyph = sprite != null ? string.Empty
                    : kind == ToastKind.Error ? ErrorGlyph
                    : kind == ToastKind.Success ? SuccessGlyph
                    : string.Empty;
                if (!string.Equals(_kindGlyph.text, glyph, StringComparison.Ordinal)) _kindGlyph.text = glyph;
            }
            if (_errorBar != null)
            {
                bool bar = kind == ToastKind.Error;
                if (_errorBar.gameObject.activeSelf != bar) _errorBar.gameObject.SetActive(bar);
            }
        }

        void ErrorFeedback()
        {
            float now = Time.unscaledTime;
            if (now - _lastErrorFeedback < ErrorFeedbackGap) return;
            _lastErrorFeedback = now;
            // 같은 순간의 막힘 흔들기(CostButtonView.Deny)와 '툭툭'이 두 번 겹치지 않게 (DenySound)
            DenySound.Play(UISfxKey.Error);
            Haptics.Play(HapticKind.Light);
        }

        void SetWidth(float width)
        {
            if (!Mathf.Approximately(_rect.rect.width, width)) _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        void Finish()
        {
            _phase = Phase.Idle;
            _slide = false;
            _settling = false;
            _punch = -1f;
            _group.alpha = 0f;
            _rect.localScale = Vector3.one;
        }

        // ───────────────────────────── 프레임 ─────────────────────────────

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            switch (_phase)
            {
                case Phase.In:
                {
                    // 들어오는 동안 맨 위 창이 바뀌었으면(같은 프레임에 팝업이 열리거나 닫힘) 한 번 더 놓는다 — 창 하나를 찾는 것뿐, 할당 없음
                    if (_settling && TopWindow != null)
                    {
                        var top = TopWindow();
                        if (top != _placedWindow) Place(top);
                    }
                    _time += dt;
                    float k = Mathf.Clamp01(_time / UIMotion.ToastIn);
                    float eased = UIMotion.OutCubic(k);
                    _group.alpha = Mathf.Lerp(_fadeFrom, 1f, eased);
                    if (_slide) _rect.anchoredPosition = _rest + new Vector2(0f, -UIMotion.ToastRise * (1f - eased));
                    if (k >= 1f)
                    {
                        _phase = Phase.Hold;
                        _slide = false;
                        _settling = false;
                    }
                    break;
                }
                case Phase.Hold:
                    _holdLeft -= dt;
                    if (_holdLeft <= 0f)
                    {
                        _phase = Phase.Out;
                        _time = 0f;
                        _fadeFrom = _group.alpha;
                    }
                    break;
                case Phase.Out:
                {
                    _time += dt;
                    float k = Mathf.Clamp01(_time / UIMotion.ToastOut);
                    _group.alpha = Mathf.Lerp(_fadeFrom, 0f, UIMotion.OutCubic(k));
                    if (k >= 1f)
                    {
                        if (_queue.Count > 0) ShowNext();
                        else Finish();
                    }
                    break;
                }
            }

            if (_punch >= 0f)
            {
                _punch += dt;
                float k = Mathf.Clamp01(_punch / UIMotion.ToastPunch);
                _rect.localScale = Vector3.one * (1f + (UIMotion.ToastPunchScale - 1f) * Mathf.Sin(k * Mathf.PI));
                if (k >= 1f)
                {
                    _rect.localScale = Vector3.one;
                    _punch = -1f;
                }
            }

            if (_phase == Phase.Idle && _punch < 0f) enabled = false;
        }
    }
}
