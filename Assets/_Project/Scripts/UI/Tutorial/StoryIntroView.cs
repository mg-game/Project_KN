using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 동화 인트로 (FTUE 0단계): 그림 3장 (밤 골목 → 수선점 불 → 짝짝이가 문을 엶), 장마다 한 줄, 약 20초.
    ///   · 화면을 누르면 다음 장 (글이 덜 나왔으면 먼저 다 보여 준다, 0.35초 안의 연타는 무시). 6.5초가 지나면 저절로 넘어간다.
    ///   · [건너뛰기 ›] (M79, 오른쪽 위 안전 영역 안): 연 지 1초 뒤 0.3초 동안 나타나고 반쯤 보이면 눌린다 — 누르면 남은 장 없이 닫는다
    ///     (닫힘 0.5초 뒤 onDone — 인트로를 본 것으로 친다). 일찍 누른 자리는 화면 누름으로 넘어간다.
    ///   · 안내 줄 '화면을 눌러 주세요  ›' (M79): 글이 다 나온 뒤 0.3초 동안 나타나 0.8±0.2로 숨 쉰다 (1.4초 주기, 움직임 줄이기면 0.9 고정).
    ///   · 뒤로가기 (M15): 떠 있는 동안 늘 먹는다 (종료 확인이 인트로 밑에 숨어 뜨지 않게). 누름과 같다 — 글을 다 보여 주고, 다음 장.
    ///     닫히는 중이거나 0.35초 안이면 먹기만 한다. 대리자는 한 번 만들어 두고 연 동안만 UIManager에 넣는다.
    ///   · 소리: 여는 동안 이야기 음악(MusicState.Story) + 전장 소리 숨김(BattleAudioFocus.Hidden), 장을 넘길 때 책장 소리(StoryPage). 닫으면 되돌린다.
    ///   · 그림은 Art/Story/intro_{1..3}.png, 없으면 빌더가 기존 아트(밤하늘·수선점·짝짝이)로 장면을 짜 둔다.
    ///   · 장면 연출: 그림이 천천히 다가오고(켄 번스), 장마다 불빛(glow)이 서서히 켜지고, 친구가 살짝 둥실거린다.
    ///     움직임 줄이기(UIMotion.Reduced)면 알파만: 그림 1.0 고정, 불빛은 켜지기만 하고(0.85, 숨 쉬지 않음), 친구는 제자리, 안내 줄 0.9 고정.
    ///   · 끝나면 onDone (FtueDirector가 Tutorial.Signal(MetaFlags.IntroSeen)). 전투는 뒤에서 계속 돈다. Update는 할당 없음.
    /// </summary>
    public class StoryIntroView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] CanvasGroup _group;
        [SerializeField] CanvasGroup[] _pages;
        [Tooltip("장마다 천천히 다가오는 그림 (켄 번스)")]
        [SerializeField] RectTransform[] _pageArt;
        [Tooltip("장마다 서서히 켜지는 불빛 (비어도 됨)")]
        [SerializeField] Image[] _pageGlows;
        [Tooltip("장마다 둥실거리는 친구 (비어도 됨)")]
        [SerializeField] RectTransform[] _pageBobs;
        [SerializeField] TMP_Text _caption;
        [TextArea] [SerializeField] string[] _lines =
        {
            "잊힌 물건들이 하나둘 밤 골목으로 흘러들었어요.",
            "골목 끝 작은 수선점에 따뜻한 불이 켜졌어요.",
            "짝을 잃은 양말 짝짝이가 문을 열었어요. \"어서 와요!\"",
        };
        [SerializeField] Image[] _dots;
        [SerializeField] TMP_Text _hint;
        [Tooltip("안내 줄 띠 (글과 함께 나타나고 사라진다). 없으면 안내 글 알파만 바꾼다")]
        [SerializeField] CanvasGroup _hintGroup;
        [Tooltip("오른쪽 위 [건너뛰기 ›] (M79)")]
        [SerializeField] Button _skipButton;
        [Tooltip("건너뛰기 버튼 전용 CanvasGroup (나타남 · 누를 수 있음)")]
        [SerializeField] CanvasGroup _skipGroup;
        [SerializeField] float _pageSeconds = 6.5f;
        [SerializeField] float _crossfade = 0.45f;
        [SerializeField] float _charsPerSecond = 20f;

        /// <summary>건너뛰기가 나타나기 시작하는 때 (연 뒤 초, 장마다 되돌리지 않는다) · 나타나는 시간 · 누를 수 있게 되는 알파 (M79)</summary>
        internal const float SkipDelay = 1f;
        internal const float SkipFade = 0.3f;
        internal const float SkipInteractableAlpha = 0.5f;
        /// <summary>건너뛰기 시계가 한 프레임에 세는 최대 시간 (무거운 첫 프레임 보호)</summary>
        const float SkipMaxStep = 0.1f;
        /// <summary>누름 · 뒤로가기 사이 최소 간격 (연타로 장을 건너뛰지 않게)</summary>
        internal const float TapDebounce = 0.35f;
        /// <summary>안내 줄: 글이 다 나온 뒤 나타나는 시간 · 숨 쉬기 (0.8 + 0.2·sin(t·4.49) = 1.4초 주기) · 움직임 줄이기 때 고정 알파</summary>
        internal const float HintFadeIn = 0.3f;
        internal const float HintPulseSpeed = 4.49f;
        internal const float HintReducedAlpha = 0.9f;
        /// <summary>움직임 줄이기 때 장면 불빛(glow)이 다 켜진 알파 — 숨 쉬기(0.85 ± 0.15)의 가운데</summary>
        const float GlowReducedAlpha = 0.85f;

        Action _onDone;
        int _page = -1;
        int _previous = -1;
        float _pageTime;
        /// <summary>Play부터 흐른 시간 (건너뛰기 나타남 — 장마다 되돌리지 않는다)</summary>
        float _elapsed;
        float _visible;
        int _total;
        /// <summary>이 장 글이 다 나온 때 (_pageTime, 아직이면 −1) — 안내 줄이 그때부터 나타난다</summary>
        float _typedAt = -1f;
        bool _closing;
        float _closeTime;
        float _sinceTap;
        Vector2[] _bobBase;
        Func<bool> _backHandler;
        bool _backRegistered;
        bool _audioHeld;
        float _skipAlpha = -1f;
        float _hintAlpha = -1f;

        /// <summary>인트로가 보이는 중</summary>
        public bool IsPlaying => gameObject.activeSelf && _page >= 0;

        void Awake()
        {
            if (_pageBobs != null)
            {
                _bobBase = new Vector2[_pageBobs.Length];
                for (int i = 0; i < _pageBobs.Length; i++) _bobBase[i] = _pageBobs[i] != null ? _pageBobs[i].anchoredPosition : default;
            }
            _backHandler = OnBackPressed;
            if (_skipButton != null) _skipButton.onClick.AddListener(Skip);
        }

        void OnEnable()
        {
            // 보이던 중에 위 층이 꺼졌다 다시 켜졌으면 놓았던 것을 다시 잡는다 (처음 켜질 때는 _page = −1 — Play가 잡는다)
            if (_page < 0 || _closing) return;
            RegisterBack(true);
            HoldAudio(true);
        }

        void OnDisable()
        {
            // 누가 밖에서 꺼도(장면 내림 등) 뒤로 처리기 · 음악 · 전장 소리 숨김을 붙잡아 두지 않는다
            Release();
        }

        /// <summary>건너뛰기 알파: 연 지 SkipDelay(1초)까지 0, 그 뒤 SkipFade(0.3초) 동안 1로 (테스트용 순수 함수)</summary>
        internal static float SkipAlpha(float elapsed) => Mathf.Clamp01((elapsed - SkipDelay) / SkipFade);

        /// <summary>건너뛰기를 누를 수 있는지 (반쯤 보이고 나서 — 일찍 누른 손가락은 화면 누름으로 간다)</summary>
        internal static bool SkipInteractable(float alpha) => alpha > SkipInteractableAlpha;

        /// <summary>
        /// 안내 줄 알파 (테스트용 순수 함수): sinceTyped = 이 장 글이 다 나온 뒤 흐른 시간 (음수 = 아직 타자 중 → 0).
        /// 0.3초 동안 나타나며 0.8 + 0.2·sin(t·4.49)로 숨 쉰다. 움직임 줄이기면 숨 쉬지 않고 0.9.
        /// </summary>
        internal static float HintAlpha(float sinceTyped, bool reduced)
        {
            if (sinceTyped < 0f) return 0f;
            float fade = Mathf.Clamp01(sinceTyped / HintFadeIn);
            float pulse = reduced ? HintReducedAlpha : 0.8f + 0.2f * Mathf.Sin(sinceTyped * HintPulseSpeed);
            return fade * pulse;
        }

        /// <summary>
        /// 처음 장부터 보여 준다. lines가 있으면 장마다 한 줄씩 쓴다 (콘텐츠 인트로 단계 text를 줄바꿈으로 나눈 것),
        /// 없거나 비면 씬에 저장된 기본 문구.
        /// </summary>
        public void Play(Action onDone, string lines = null)
        {
            if (!string.IsNullOrEmpty(lines))
            {
                var split = lines.Split('\n');
                if (split.Length >= _pages.Length) _lines = split;
            }
            _onDone = onDone;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _closing = false;
            _elapsed = 0f;
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = true;
            }
            for (int i = 0; i < _pages.Length; i++) _pages[i].alpha = 0f;
            _previous = -1;
            _skipAlpha = -1f;
            SetSkip(0f);
            _hintAlpha = -1f;
            SetHint(0f);
            RegisterBack(true);
            HoldAudio(true);
            ShowPage(0);
        }

        /// <summary>
        /// 다시 보기 (설정 '이야기 다시 보기' — SET-01, MetaNavigator가 UINav.IntroReplayRequested로 부른다): 처음 장부터, 끝나도 아무 신호 없음
        /// (onDone 없음 → 튜토리얼 플래그 · 보상 없음). 이미 보이는 중이면 false
        /// </summary>
        public bool Replay(string lines = null)
        {
            if (IsPlaying) return false;
            Play(null, lines);
            return true;
        }

        /// <summary>콜백 없이 닫는다 (메타가 꺼졌을 때 등)</summary>
        public void Stop()
        {
            _onDone = null;
            _page = -1;
            Release();
            gameObject.SetActive(false);
        }

        /// <summary>[건너뛰기 ›]: 남은 장을 건너뛰고 닫는다 (닫힘 0.5초 뒤 onDone — 끝까지 본 것과 같다). 닫는 중 · 꺼져 있으면 아무것도 하지 않는다</summary>
        public void Skip()
        {
            if (_closing || _page < 0) return;
            BeginClose();
        }

        void ShowPage(int index)
        {
            // 책장 넘김 (첫 장은 화면이 열리는 것 자체가 시작이라 소리 없음)
            if (index > 0 && _page >= 0) UISfx.Play(UISfxKey.StoryPage);
            _previous = _page;
            _page = index;
            _pageTime = 0f;
            _sinceTap = 0f;
            _typedAt = -1f;
            _caption.text = index < _lines.Length ? _lines[index] : string.Empty;
            // 켜진 뒤라 글자 수를 바로 잴 수 있다 (Play가 먼저 켠다)
            _caption.ForceMeshUpdate(true);
            _total = _caption.textInfo.characterCount;
            _visible = 0f;
            _caption.maxVisibleCharacters = 0;
            if (_pageGlows != null && index < _pageGlows.Length && _pageGlows[index] != null) SetAlpha(_pageGlows[index], 0f);
            for (int i = 0; _dots != null && i < _dots.Length; i++)
            {
                if (_dots[i] == null) continue;
                // 흰 단추: 지금 장은 또렷하게, 나머지는 옅게
                _dots[i].color = i == index ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                _dots[i].rectTransform.localScale = Vector3.one * (i == index ? 1.25f : 1f);
            }
        }

        /// <summary>누르면 글을 다 보여 주거나 다음 장 (0.35초 안의 연타는 무시)</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_closing || _page < 0 || _sinceTap < TapDebounce) return;
            Advance();
        }

        /// <summary>한 번 넘기기: 글이 덜 나왔으면 다 보여 주고, 다 나왔으면 다음 장 (누름 · 뒤로가기 공통)</summary>
        void Advance()
        {
            _sinceTap = 0f;
            if (_visible < _total)
            {
                _visible = _total;
                _caption.maxVisibleCharacters = _total;
                return;
            }
            Next();
        }

        /// <summary>
        /// 뒤로가기 처리기 (M15 — UIManager.PushBackHandler, 나중에 넣은 것부터): 인트로가 떠 있으면 늘 먹는다.
        /// 닫히는 중이거나 방금 넘겼으면(0.35초 안) 먹기만 하고, 아니면 누름과 같다 (Advance).
        /// </summary>
        bool OnBackPressed()
        {
            if (_page < 0 || !isActiveAndEnabled) return false;
            // 인트로보다 위 층(SystemPopups — 확인 창)에 열린 창이 있으면 그 창이 뒤로가기를 받는다 (보이는 창부터 닫히게)
            if (PopupAboveIntro()) return false;
            if (!_closing && _sinceTap >= TapDebounce) Advance();
            return true;
        }

        /// <summary>
        /// 맨 위 열린 팝업이 인트로보다 위 층인지 (캔버스 아래 층 순서 — 팝업의 부모 층 vs 인트로의 부모 TutorialOverlay).
        /// Popups 층의 팝업은 인트로 밑이라 false (인트로가 뒤로가기를 먹는다). 할당 없음.
        /// </summary>
        bool PopupAboveIntro()
        {
            var ui = UIManager.Instance;
            var top = ui != null ? ui.TopPopup : null;
            if (top == null) return false;
            var popupLayer = top.transform.parent;
            var introLayer = transform.parent;
            return popupLayer != null && introLayer != null && popupLayer.parent == introLayer.parent
                   && popupLayer.GetSiblingIndex() > introLayer.GetSiblingIndex();
        }

        void Next()
        {
            if (_page + 1 < _pages.Length) ShowPage(_page + 1);
            else BeginClose();
        }

        void BeginClose()
        {
            if (_closing) return;
            _closing = true;
            _closeTime = 0f;
            if (_group != null) _group.blocksRaycasts = false;
            SetSkip(0f);
        }

        /// <summary>뒤로 처리기를 넣거나 뺀다 (같은 대리자 — UIManager는 인스턴스로 찾아 지운다)</summary>
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

        /// <summary>이야기 음악 + 전장 소리 숨김을 잡거나 놓는다 (주인 = 이 컴포넌트, 소리 파일이 없으면 조용히 지나간다)</summary>
        void HoldAudio(bool on)
        {
            if (on == _audioHeld) return;
            _audioHeld = on;
            int owner = GetInstanceID();
            if (on)
            {
                AudioService.PushMusic(MusicState.Story, owner);
                AudioService.SetBattleFocus(BattleAudioFocus.Hidden, owner);
            }
            else
            {
                AudioService.PopMusic(owner);
                AudioService.ClearBattleFocus(owner);
            }
        }

        /// <summary>닫힘 · 멈춤 · 꺼짐에서 붙잡은 것을 모두 놓는다 (여러 번 불러도 된다)</summary>
        void Release()
        {
            RegisterBack(false);
            HoldAudio(false);
        }

        /// <summary>건너뛰기 버튼 알파 · 누를 수 있음 (값이 바뀔 때만 쓴다)</summary>
        void SetSkip(float alpha)
        {
            if (_skipGroup == null || Mathf.Approximately(alpha, _skipAlpha)) return;
            _skipAlpha = alpha;
            _skipGroup.alpha = alpha;
            bool live = SkipInteractable(alpha);
            if (_skipGroup.interactable != live) _skipGroup.interactable = live;
            // 반쯤 보이기 전에는 누름도 막지 않는다 — 그 자리를 눌러도 화면 누름(다음 장)으로 간다
            if (_skipGroup.blocksRaycasts != live) _skipGroup.blocksRaycasts = live;
        }

        /// <summary>안내 줄 알파 (띠가 있으면 띠째, 없으면 글만 — 값이 바뀔 때만 쓴다)</summary>
        void SetHint(float alpha)
        {
            if (Mathf.Approximately(alpha, _hintAlpha)) return;
            _hintAlpha = alpha;
            if (_hintGroup != null)
            {
                _hintGroup.alpha = alpha;
                return;
            }
            if (_hint == null) return;
            var c = _hint.color;
            _hint.color = new Color(c.r, c.g, c.b, alpha);
        }

        void Update()
        {
            if (_page < 0) return;
            float dt = Time.unscaledDeltaTime;
            _pageTime += dt;
            _sinceTap += dt;

            if (_closing)
            {
                _closeTime += dt;
                if (_group != null) _group.alpha = 1f - Mathf.Clamp01(_closeTime / 0.5f);
                if (_closeTime < 0.5f) return;
                var done = _onDone;
                _onDone = null;
                _page = -1;
                Release();
                gameObject.SetActive(false);
                done?.Invoke();
                return;
            }

            // 장면을 읽은 직후의 긴 첫 프레임이 1초 기다림을 건너뛰지 않게 한 프레임에 0.1초까지만 센다
            _elapsed += Mathf.Min(dt, SkipMaxStep);
            SetSkip(SkipAlpha(_elapsed));
            if (_group != null) _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, dt / 0.4f);

            float fade = Mathf.Clamp01(_pageTime / _crossfade);
            for (int i = 0; i < _pages.Length; i++)
            {
                float target = i == _page ? fade : i == _previous ? 1f - fade : 0f;
                if (!Mathf.Approximately(_pages[i].alpha, target)) _pages[i].alpha = target;
            }

            // 켄 번스: 장이 보이는 동안 1.0 → 1.07. 움직임 줄이기면 장면은 가만히 — 그림 1.0, 불빛은 켜지기만(숨 쉬지 않고 0.85), 친구는 제자리
            bool reduced = UIMotion.Reduced;
            if (_pageArt != null && _page < _pageArt.Length && _pageArt[_page] != null)
                _pageArt[_page].localScale = reduced ? Vector3.one : Vector3.one * Mathf.Lerp(1f, 1.07f, Mathf.Clamp01(_pageTime / _pageSeconds));
            if (_pageGlows != null && _page < _pageGlows.Length && _pageGlows[_page] != null)
                SetAlpha(_pageGlows[_page], Mathf.Clamp01((_pageTime - 0.3f) / 2.2f) * (reduced ? GlowReducedAlpha : 0.85f + 0.15f * Mathf.Sin(_pageTime * 3f)));
            if (_pageBobs != null && _page < _pageBobs.Length && _pageBobs[_page] != null)
                _pageBobs[_page].anchoredPosition = reduced ? _bobBase[_page] : _bobBase[_page] + new Vector2(0f, 10f * Mathf.Sin(_pageTime * 2.6f));

            if (_visible < _total && _pageTime > _crossfade * 0.6f)
            {
                _visible = Mathf.Min(_total, _visible + _charsPerSecond * dt);
                _caption.maxVisibleCharacters = (int)_visible;
            }

            // 안내 줄: 글이 다 나온 순간부터 0.3초 동안 나타나 숨 쉰다 (타자 중에는 숨김)
            if (_typedAt < 0f && _visible >= _total) _typedAt = _pageTime;
            SetHint(HintAlpha(_typedAt < 0f ? -1f : _pageTime - _typedAt, reduced));

            if (_pageTime >= _pageSeconds) Next();
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            var c = graphic.color;
            graphic.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
