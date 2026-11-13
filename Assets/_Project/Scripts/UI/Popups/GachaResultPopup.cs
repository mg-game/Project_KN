using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 소환 결과 (와이어프레임 05). 분실물 꾸러미 공개(CR-04, SPEC4 §7.5 — 박자는 GachaRevealTiming) 뒤 대표 결과(새 계보 / 최고 등급)를 크게 보여준다.
    /// 신규 계보는 "새로운 계보의 지킴이를 만났어요!" + '전투력 N' (PRG-01) + 따라잡기 레벨이면 '친구들 평균의 80%인 Lv.n부터 함께해요' (PRG-05),
    /// 중복은 "…기억편으로 바꿨어요". 대표 결과에는 계보 특성 칩과 고유 스킬을 함께 보여준다.
    /// <para>
    /// 공개 (CR-04): 1회 = 무대(_revealStage)에서 꾸러미가 떨어져 흔들리고(실제 ★4 이상이면 크게 + 그 등급 빛 — 실제보다 높게 예고하지 않는다)
    /// 열린 뒤 초상화 그림자가 흰색이 된다. 10회 = 카드(뒷면 = 꾸러미 그림)가 0.04초 간격으로 나타나 차례로 0.08초에 뒤집히고,
    /// ★4 이상은 멈춰서 무대에서 1.6초 크게 보여 준다. 중복은 뒤집힌 뒤 기억편이 요약 줄로 0.4초 날아가 '기억편 +n'이 쌓인다.
    /// 꾸러미 그림은 빌더가 UiArt.GachaBundle(묶임 · 빛남 · 열림)을 _bundleSprites에 굽는다 (아트가 없으면 카드 뒷면).
    /// [건너뛰기 ›]는 0.5초부터 오른쪽 위에 보인다 — 누르거나 본문을 탭하면 남은 카드를 한 번에 공개한다.
    /// 무대가 없는 예전 씬(다시 빌드하기 전)은 예전처럼 카드를 한 장씩 뒤집는다. 새 직렬화 필드는 모두 비어 있어도 된다.
    /// </para>
    /// <para>
    /// 버튼 (M16 · M26 · M47): 아래 줄 [확인 (보조, 왼쪽)] [N회 더 소환 (금, 오른쪽 — 비용 버튼 세 상태)], 그 위 작은 줄
    /// [진화 기억도 보기 › (대표 결과가 있으면)] [편성하러 가기 › (빈 칸이 있고 결과 중 편성 안 된 친구가 있을 때만)] — 둘 다 같은 보조 알약 300x88.
    /// 창 왼쪽 위 별조각 잔액 칩 — 첫 만남 확정 소환(결과가 모두 IsGuaranteed)에서는 위쪽 '첫 만남 확정 소환' 리본이 덮으므로 숨긴다.
    /// 그 창에서 [1회 더 소환]으로 다시 열어도 창이 닫힐 때까지 숨겨 둔다 (리본은 FtueDirector가 창이 닫힐 때 내린다 — ShowsFirstMeeting으로 볼 수 있다).
    /// 결과가 5장 이하면 카드 격자를 한 줄로 접고 요약 · 버튼을 끌어올린다 (창을 한 줄만큼 줄인다 — 빈 띠가 남지 않게).
    /// 다시 소환이 모자라면 비용이 붉어지지만 누를 수 있고, 누르면 흔들기 + 부족 팝업으로 보낸다.
    /// 10회 더 소환은 쓰기 확인(오늘 다시 묻지 않기 confirm.gacha10, 시스템 팝업 층 위)을 거치고 확인이 닫힐 때까지 버튼을 막는다 (M55). 1회는 바로.
    /// </para>
    /// <para>
    /// 닫기 정책 (M49): 공개 중에 어둡게 덮기 · 뒤로가기 = 건너뛰기 (창은 그대로), 공개가 끝난 뒤 = 닫기. X는 언제나 바로 닫기.
    /// 튜토리얼 첫 만남(FtueDirector)은 Closed가 진짜 닫힐 때만 오는 것과 Window 자식에 기댄다. Show(…, onRetry) 앞 다섯 인자는 그대로 (C-10).
    /// </para>
    /// 소리 (AUDIO-SPEC §8.1): 열려 있는 동안 소환 음악 + 전장 소리 가림(Hidden), 꾸러미 떨어짐 · 열림 · 등급 공개 · 건너뛰기 ·
    /// 대표 결과(새 친구 징글 / 기억편 녹음) · 등급 징글(AudioService.OnGachaReveal). ★4 이상 공개마다 AppSignals.RaiseGachaRevealHigh(등급) — 소리 엔진의
    /// 배경음 줄이기 (jingle_gacha_high를 따로 틀지 않는다). 10회 뒤집기 소리는 1초에 12번을 넘지 않는다 (GachaRevealTiming.TenFlipStep).
    /// 공개 중 프레임마다 하는 일은 위치 · 색 쓰기뿐이다 (할당 없음 — 녹는 기억편 조각은 처음 한 번만 만들어 다시 쓴다).
    /// </summary>
    public class GachaResultPopup : UIPopup
    {
        /// <summary>'편성하러 가기 ›' 뒤에 가리킬 자동 편성 버튼 (CoachTargets.FormationPanel 기준, C-05)</summary>
        static readonly string FormationAutoButton = CoachTargets.FormationPanel + "/TeamSynergy/AutoButton";

        static readonly Func<string, bool> s_isOwned = id => Game.Characters != null && Game.Characters.IsOwned(id);
        static readonly Func<string, bool> s_isPlaced = id => Game.Formation != null && Game.Formation.IsLineagePlaced(id);

        /// <summary>이 수 이하면 카드 격자를 한 줄로 접는다 (격자 한 줄 = 5장)</summary>
        public const int CompactMaxResults = 5;

        /// <summary>지금 보여 주는 결과가 첫 만남 확정 소환인지 (열려 있고 이번 결과가 모두 IsGuaranteed — 다시 소환한 결과면 false)</summary>
        public bool ShowsFirstMeeting => IsOpen && _showsFirstMeeting;

        [Header("카드")]
        [SerializeField] RectTransform _cardContent;
        [SerializeField] GachaCardView _cardPrefab;
        [SerializeField] Button _skipButton;
        [SerializeField] TMP_Text _skipHint;
        [SerializeField] float _revealInterval = 0.14f;

        [Header("대표 결과")]
        [SerializeField] CanvasGroup _highlight;
        [SerializeField] FriendPortrait _highlightPortrait;
        [SerializeField] Image _highlightGlow;
        [SerializeField] TMP_Text _highlightTitle;
        [SerializeField] TMP_Text _highlightStars;
        [SerializeField] TMP_Text _highlightMessage;
        [SerializeField] GameObject _highlightNewTag;
        [SerializeField] CharacterChip[] _highlightTraitChips;
        [SerializeField] TMP_Text _highlightSkillText;
        [SerializeField] SparkleBurst _highlightBurst;
        [SerializeField] TMP_Text _summaryText;
        [SerializeField] GameObject _shardInfo;
        [Tooltip("새 친구 '전투력 N' 줄 (아이콘 + 글, PRG-01 — 없어도 됨)")]
        [SerializeField] GameObject _highlightPower;
        [SerializeField] TMP_Text _highlightPowerText;
        [Tooltip("따라잡기 줄 '친구들 평균의 80%인 Lv.n부터 함께해요' (PRG-05 — 없어도 됨)")]
        [SerializeField] TMP_Text _highlightCatchUp;

        [Header("버튼")]
        [SerializeField] Button _confirmButton;
        [SerializeField] Button _retryButton;
        [SerializeField] TMP_Text _retryLabel;
        [SerializeField] TMP_Text _retryCostText;
        [SerializeField] Button _treeButton;
        [Tooltip("다시 소환 비용 버튼 세 상태 (금 바탕)")]
        [SerializeField] CostButtonView _retryView;
        [Tooltip("'편성하러 가기 ›' (빈 칸이 있고 결과 중 보유 · 미배치 계보가 있을 때만 켠다)")]
        [SerializeField] Button _formationButton;

        [Header("공개 무대 (CR-04 — 없으면 예전 공개)")]
        [Tooltip("카드 자리 위에 겹치는 무대 (꾸러미 · 초상화 · 별가루)")]
        [SerializeField] RectTransform _revealStage;
        [SerializeField] CanvasGroup _revealGroup;
        [Tooltip("10회 중 ★4 이상을 크게 보여 줄 때 카드를 가리는 어둠")]
        [SerializeField] Image _revealDim;
        [SerializeField] RectTransform _bundle;
        [SerializeField] Image _bundleImage;
        [SerializeField] Image _bundleGlow;
        [Tooltip("꾸러미 그림 (0 묶임 · 1 빛남 · 2 열림 — 빌더가 UiArt.GachaBundle로 굽는다)")]
        [SerializeField] Sprite[] _bundleSprites;
        [SerializeField] Image _revealPortrait;
        [SerializeField] SparkleBurst _revealBurst;
        [Tooltip("[건너뛰기 ›] (오른쪽 위, 0.5초부터)")]
        [SerializeField] Button _skipChip;
        [Tooltip("중복이 요약 줄로 날아가는 기억편 조각 (꺼 둔 본보기 — 처음 한 번 복제해 다시 쓴다)")]
        [SerializeField] RectTransform _meltTemplate;

        UIListPool<GachaCardView> _pool;
        readonly List<GachaResult> _results = new();
        Action _onRetry;
        Coroutine _reveal;
        Coroutine _skipLater;
        GachaResult _best;
        int _bestIndex = -1;
        /// <summary>대표 결과가 새 친구일 때 시작 레벨 (Show 때 읽은 계보 레벨 — 따라잡기면 1보다 크다)</summary>
        int _bestStartLevel = 1;
        GachaTable _retryTable;
        int _retryCount;
        bool _canRetry;
        bool _buttonsVisible;
        /// <summary>10회 더 소환 확인 창이 열려 있다 (닫힐 때까지 버튼을 막는다)</summary>
        bool _retryPending;
        string _formationLineage;
        Action _runRetry;
        Action _onRetryConfirmClosed;
        /// <summary>이번 결과가 첫 만남 확정 소환이다</summary>
        bool _showsFirstMeeting;
        /// <summary>첫 만남 창 (열린 뒤 한 번이라도 첫 만남 결과를 보였다) — 창이 닫힐 때까지 잔액 칩을 숨긴다</summary>
        bool _firstMeetingSession;

        // 공개 중 상태
        /// <summary>이 결과의 등급 징글 · 높은 등급 신호를 이미 보냈다 (10회 중 크게 보여 줄 때 — 대표 결과에서 다시 보내지 않는다)</summary>
        readonly List<bool> _signalled = new();
        readonly List<RectTransform> _meltFlyers = new();
        readonly List<Coroutine> _melts = new();
        int _meltShards;
        bool _meltSoundPlayed;
        Vector2 _bundleHome;
        int _bundleState = -1;

        // 한 줄 접기 (M-carry-over): 처음 모양을 기억했다가 접고 편다
        Transform _balanceChip;
        bool _layoutCaptured;
        bool _compact;
        float _windowHeight;
        float _cardsHeight;
        float _stageHeight;
        float _rowStep;
        int _columns = CompactMaxResults;
        float _summaryY;
        float _skipHintY;
        float _shardInfoY;

        protected override void Awake()
        {
            base.Awake();
            _pool = new UIListPool<GachaCardView>(_cardPrefab, _cardContent);
            _confirmButton.onClick.AddListener(Close);
            _retryButton.onClick.AddListener(OnRetryClicked);
            _treeButton.onClick.AddListener(OpenTree);
            _skipButton.onClick.AddListener(Skip);
            if (_skipChip != null)
            {
                _skipChip.onClick.AddListener(Skip);
                _skipChip.gameObject.SetActive(false);
            }
            if (_formationButton != null) _formationButton.onClick.AddListener(GoToFormation);
            _runRetry = RunRetry;
            _onRetryConfirmClosed = OnRetryConfirmClosed;
            _balanceChip = _window != null ? _window.Find("BalanceChip") : null;
            if (_bundle != null) _bundleHome = _bundle.anchoredPosition;
            if (_revealStage != null) _revealStage.gameObject.SetActive(false);
            if (_meltTemplate != null) _meltTemplate.gameObject.SetActive(false);
            CaptureLayout();
        }

        /// <param name="retryLabel">예: "10회 더 소환"</param>
        /// <param name="retryCost">다시 소환 비용 표시 (예: "900")</param>
        /// <param name="canRetry">비용이 충분한지 (부족하면 비용을 붉게 — 버튼은 누를 수 있다)</param>
        /// <param name="retryTable">다시 소환할 표 (있으면 누를 때 비용을 다시 확인하고, 모자라면 흔들기 + 부족 팝업, 10회면 쓰기 확인을 거친다)</param>
        /// <param name="retryCount">다시 소환 횟수 (retryTable과 함께)</param>
        public void Show(IReadOnlyList<GachaResult> results, string retryLabel, string retryCost, bool canRetry, Action onRetry,
            GachaTable retryTable = null, int retryCount = 0)
        {
            bool wasOpen = IsOpen;
            Open();
            StopRevealExtras();
            _onRetry = onRetry;
            _retryTable = retryTable;
            _retryCount = retryCount;
            _canRetry = canRetry;
            _retryPending = false;
            _retryLabel.text = retryLabel;
            _retryCostText.text = retryCost;
            SetRetryAffordable(canRetry);
            // 다시 소환이 없는 결과 (친구 선택권): [확인] 하나만 (줄이 넓힌다)
            bool retry = onRetry != null;
            if (_retryButton.gameObject.activeSelf != retry) _retryButton.gameObject.SetActive(retry);

            _results.Clear();
            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    if (results[i] != null && results[i].Lineage != null) _results.Add(results[i]);
                }
            }
            _bestIndex = PickBestIndex(_results);
            _best = _bestIndex >= 0 ? _results[_bestIndex] : null;
            _bestStartLevel = _best != null && _best.IsNew && Game.Characters != null ? Game.Characters.GetLevel(_best.Lineage.id) : 1;
            _signalled.Clear();
            for (int i = 0; i < _results.Count; i++) _signalled.Add(false);
            _meltShards = 0;
            _meltSoundPlayed = false;

            _pool.SetCount(_results.Count);
            for (int i = 0; i < _results.Count; i++) _pool[i].Set(_results[i]);

            ApplyLayout(_results.Count <= CompactMaxResults);
            _showsFirstMeeting = IsFirstMeeting(_results);
            _firstMeetingSession = FirstMeetingSession(_showsFirstMeeting, wasOpen, _firstMeetingSession);
            bool chip = !_firstMeetingSession;
            if (_balanceChip != null && _balanceChip.gameObject.activeSelf != chip) _balanceChip.gameObject.SetActive(chip);

            _highlight.alpha = 0f;
            _summaryText.text = string.Empty;
            _shardInfo.SetActive(false);
            SetButtonsVisible(false);

            AudioService.PushMusic(MusicState.Gacha, GetInstanceID());
            AudioService.SetBattleFocus(BattleAudioFocus.Hidden, GetInstanceID());

            if (_reveal != null) StopCoroutine(_reveal);
            _reveal = StartCoroutine(RevealSequence());
        }

        protected override void OnClose()
        {
            if (_reveal != null) StopCoroutine(_reveal);
            _reveal = null;
            StopRevealExtras();
            _onRetry = null;
            _retryTable = null;
            _retryPending = false;
            _showsFirstMeeting = false;
            _firstMeetingSession = false;
            AudioService.PopMusic(GetInstanceID());
            AudioService.ClearBattleFocus(GetInstanceID());
        }

        /// <summary>어둡게 덮기 · 뒤로가기 (M49): 공개 중이면 건너뛰기만 하고 창은 남긴다, 끝났으면 닫는다</summary>
        protected override void OnDimOrBack()
        {
            if (_reveal != null)
            {
                Skip();
                return;
            }
            Close();
        }

        /// <summary>대표 결과 번호: 새 계보 중 최고 등급, 없으면 전체 최고 등급 (같으면 앞의 것, 결과가 없으면 -1)</summary>
        internal static int PickBestIndex(IReadOnlyList<GachaResult> results)
        {
            int best = -1;
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (best < 0) { best = i; continue; }
                var current = results[best];
                if (result.IsNew != current.IsNew)
                {
                    if (result.IsNew) best = i;
                    continue;
                }
                if (result.Lineage.grade > current.Lineage.grade) best = i;
            }
            return best;
        }

        /// <summary>첫 만남 확정 소환인지 (결과가 모두 IsGuaranteed — FtueDirector의 PullGuaranteed 한 장)</summary>
        internal static bool IsFirstMeeting(IReadOnlyList<GachaResult> results)
        {
            if (results == null || results.Count == 0) return false;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i] == null || !results[i].IsGuaranteed) return false;
            }
            return true;
        }

        /// <summary>
        /// 잔액 칩을 숨기는 첫 만남 창인지: 이번 결과가 첫 만남이거나, 열린 채 다시 Show한 창이 이미 첫 만남 창이었다
        /// ('첫 만남 확정 소환' 리본이 창이 닫힐 때까지 남아 칩 자리를 덮는다). 닫혔다 새로 열린 창은 이번 결과만 본다
        /// </summary>
        internal static bool FirstMeetingSession(bool firstMeeting, bool wasOpen, bool session) => firstMeeting || (wasOpen && session);

        // ───────────── 한 줄 접기 ─────────────

        /// <summary>처음(빌더) 모양을 기억한다: 창 높이 · 카드 칸 높이 · 격자 한 줄 높이 · 요약 · 안내 · 기억편 칸 자리</summary>
        void CaptureLayout()
        {
            if (_layoutCaptured) return;
            _layoutCaptured = true;
            _windowHeight = _window != null ? _window.sizeDelta.y : 0f;
            _cardsHeight = _cardContent != null ? _cardContent.sizeDelta.y : 0f;
            _stageHeight = _revealStage != null ? _revealStage.sizeDelta.y : 0f;
            var grid = _cardContent != null ? _cardContent.GetComponent<GridLayoutGroup>() : null;
            if (grid != null)
            {
                _rowStep = grid.cellSize.y + grid.spacing.y;
                if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0) _columns = grid.constraintCount;
            }
            _summaryY = _summaryText != null ? _summaryText.rectTransform.anchoredPosition.y : 0f;
            _skipHintY = _skipHint != null ? _skipHint.rectTransform.anchoredPosition.y : 0f;
            _shardInfoY = _shardInfo != null && _shardInfo.transform is RectTransform info ? info.anchoredPosition.y : 0f;
        }

        /// <summary>줄일 높이 (격자 한 줄 — 그 줄에 다 들어가고 창 · 카드 칸이 위에서 기대한 꼴일 때만, 아니면 0)</summary>
        float CompactShrink()
        {
            if (_window == null || _cardContent == null || _rowStep <= 0f || _columns < CompactMaxResults) return 0f;
            // 격자가 두 줄 이상 들어가는 칸일 때만 (한 줄짜리 칸이면 접을 것이 없다)
            if (_cardsHeight < _rowStep * 2f - 1f) return 0f;
            return _rowStep;
        }

        /// <summary>5장 이하면 한 줄로 접고(창 · 카드 칸을 한 줄만큼 줄이고 그 아래 묶음을 올린다), 아니면 처음 모양으로</summary>
        void ApplyLayout(bool compact)
        {
            float shrink = CompactShrink();
            if (shrink <= 0f) compact = false;
            if (compact == _compact) return;
            _compact = compact;
            float d = compact ? shrink : 0f;
            var window = _window.sizeDelta;
            _window.sizeDelta = new Vector2(window.x, _windowHeight - d);
            var cards = _cardContent.sizeDelta;
            _cardContent.sizeDelta = new Vector2(cards.x, _cardsHeight - d);
            if (_revealStage != null)
            {
                var stage = _revealStage.sizeDelta;
                _revealStage.sizeDelta = new Vector2(stage.x, Mathf.Max(0f, _stageHeight - d));
            }
            MoveY(_summaryText != null ? _summaryText.rectTransform : null, _summaryY + d);
            MoveY(_skipHint != null ? _skipHint.rectTransform : null, _skipHintY + d);
            MoveY(_shardInfo != null ? _shardInfo.transform as RectTransform : null, _shardInfoY + d);
        }

        static void MoveY(RectTransform rect, float y)
        {
            if (rect == null) return;
            var position = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(position.x, y);
        }

        // ───────────── 공개 ─────────────

        static float Now => Time.unscaledTime;

        IEnumerator RevealSequence()
        {
            _skipButton.gameObject.SetActive(true);
            _skipHint.gameObject.SetActive(true);
            SetSkipChip(false);
            if (_skipChip != null) _skipLater = StartCoroutine(ShowSkipLater());

            IEnumerator body;
            if (_revealStage == null || _pool.Count == 0) body = LegacyReveal();
            else if (_pool.Count == 1) body = SingleReveal(0);
            else body = MultiReveal();
            // 한 코루틴 안에서 돌린다 (건너뛰기 StopCoroutine 한 번에 모두 멈춘다)
            while (body.MoveNext()) yield return body.Current;

            _reveal = null;
            Finish();
        }

        /// <summary>무대가 없는 예전 씬: 카드를 한 장씩 뒤집는다 (예전 공개 그대로)</summary>
        IEnumerator LegacyReveal()
        {
            UISfx.Play(UISfxKey.GachaPullStart);
            UISfx.Play(UISfxKey.GachaBundleOpen);
            yield return new WaitForSecondsRealtime(0.12f);
            for (int i = 0; i < _pool.Count; i++)
            {
                var card = _pool[i];
                card.PlayReveal();
                float wait = _results.Count == 1 ? card.RevealDuration : Mathf.Max(_revealInterval, card.RevealDuration - 0.3f);
                yield return new WaitForSecondsRealtime(wait);
            }
            yield return new WaitForSecondsRealtime(0.15f);
        }

        /// <summary>1회: 떨어짐 → 흔들림 → 열림 → 그림자 → 흰색 (1.35초), 그다음 카드를 앞면으로 두고 대표 결과로</summary>
        IEnumerator SingleReveal(int index)
        {
            var result = _results[index];
            var grade = result.Lineage.grade;
            bool reduced = UIMotion.Reduced;
            PrepareStage(result, false);
            UISfx.Play(UISfxKey.GachaPullStart);
            bool opened = false;
            bool appeared = false;
            float start = Now;
            while (true)
            {
                float t = Now - start;
                StepStage(t, grade, reduced, 1f, ref opened, ref appeared);
                if (t >= GachaRevealTiming.SingleDuration) break;
                yield return null;
            }
            _pool[index].RevealInstant();
            HideStage();
        }

        /// <summary>
        /// 여러 장: 0.04초 간격으로 나타난 뒤 차례로 0.08초에 뒤집는다 (간격 TenFlipStep). ★4 이상은 멈추고 무대에서 1.6초,
        /// 중복은 뒤집힌 뒤 기억편이 요약 줄로 날아간다. 끝나면 Settle만큼 쉰다 (마지막 기억편이 닿게).
        /// 마감은 모두 공개 시작에서 GachaRevealTiming의 모형(AppearStart · FlipStart · MultiDuration — GachaRevealTimingTests가 고정)으로 잰다:
        /// 박자를 바꾸면 테스트가 같은 값을 보고, 긴 프레임이 와도 늦음이 장마다 쌓이지 않는다.
        /// </summary>
        IEnumerator MultiReveal()
        {
            int count = _pool.Count;
            UISfx.Play(UISfxKey.GachaPullStart);
            UISfx.Play(UISfxKey.GachaCardAppear);
            float start = Now;
            for (int i = 0; i < count; i++)
            {
                float appear = start + GachaRevealTiming.AppearStart(i);
                while (Now < appear) yield return null;
                _pool[i].PlayAppear(GachaRevealTiming.TenAppearPop);
            }

            int highBefore = 0;
            for (int i = 0; i < count; i++)
            {
                // 이 장의 차례 (★4 이상이면 여기서 크게 보여 주고 1.6초 뒤에 뒤집는다)
                float turn = start + GachaRevealTiming.FlipStart(count, i, highBefore);
                while (Now < turn) yield return null;
                var result = _results[i];
                bool high = GachaRevealTiming.IsHigh(result.Lineage.grade);
                if (high)
                {
                    var full = HighReveal(i);
                    while (full.MoveNext()) yield return full.Current;
                    highBefore++;
                }
                // 크게 보여 준 장은 무대가 등급 소리를 냈다 — 뒤집기는 조용히
                _pool[i].PlayQuickFlip(GachaRevealTiming.TenFlip, !high);
                if (!result.IsNew) StartMelt(i);
            }
            float end = start + GachaRevealTiming.MultiDuration(count, highBefore);
            while (Now < end) yield return null;
        }

        /// <summary>10회 중 ★4 이상 한 장: 카드를 어둠으로 가리고 1회 박자의 0.70초부터(열림 → 흰색)를 앞 0.9초에, 나머지는 머무름 (1.6초)</summary>
        IEnumerator HighReveal(int index)
        {
            var result = _results[index];
            var grade = result.Lineage.grade;
            bool reduced = UIMotion.Reduced;
            PrepareStage(result, true);
            bool opened = false;
            bool appeared = false;
            bool signalled = false;
            float start = Now;
            while (true)
            {
                float u = Now - start;
                float t = GachaRevealTiming.HighRevealBeatTime(u);
                // 들어올 때 0.15초, 나갈 때 마지막 0.2초 동안 무대 알파
                float alpha = Mathf.Min(Mathf.Clamp01(u / 0.15f), Mathf.Clamp01((GachaRevealTiming.HighReveal - u) / 0.2f));
                StepStage(t, grade, reduced, alpha, ref opened, ref appeared);
                if (!signalled && t >= GachaRevealTiming.SilhouetteEnd)
                {
                    signalled = true;
                    SignalReveal(index);
                }
                if (u >= GachaRevealTiming.HighReveal) break;
                yield return null;
            }
            if (!signalled) SignalReveal(index);
            HideStage();
        }

        /// <summary>
        /// 그 결과의 등급 징글 + 높은 등급 신호 (AudioService.OnGachaReveal · AppSignals.RaiseGachaRevealHigh) + 강한 진동 (★4 이상, M18) — 한 결과에 한 번
        /// </summary>
        void SignalReveal(int index)
        {
            if (index < 0 || index >= _results.Count || _signalled[index]) return;
            _signalled[index] = true;
            var grade = _results[index].Lineage.grade;
            AudioService.OnGachaReveal(grade);
            if (GachaRevealTiming.IsHigh(grade))
            {
                AppSignals.RaiseGachaRevealHigh((int)grade);
                Haptics.Play(HapticKind.Heavy);
            }
        }

        /// <summary>무대를 그 결과로 맞춘다 (꾸러미 묶임 · 제자리, 빛 끔, 초상화 그림자 · 숨김). dim = 카드를 가리는 어둠</summary>
        void PrepareStage(GachaResult result, bool dim)
        {
            _revealStage.gameObject.SetActive(true);
            _revealStage.SetAsLastSibling();
            if (_meltTemplate != null) _meltTemplate.SetAsLastSibling();
            _skipButton.transform.SetAsLastSibling();
            if (_revealGroup != null) _revealGroup.alpha = 1f;
            if (_revealDim != null)
            {
                _revealDim.gameObject.SetActive(dim);
                _revealDim.color = CharacterUI.WithAlpha(_revealDim.color, dim ? 0f : _revealDim.color.a);
            }
            _bundleState = -1;
            SetBundleState(UiBundle.Closed);
            if (_bundleImage != null) _bundleImage.color = Color.white;
            if (_bundle != null)
            {
                _bundle.anchoredPosition = _bundleHome;
                _bundle.localRotation = Quaternion.identity;
                _bundle.localScale = Vector3.one;
                _bundle.gameObject.SetActive(true);
            }
            if (_bundleGlow != null)
            {
                _bundleGlow.color = CharacterUI.WithAlpha(GachaRevealTiming.GradeLight(GachaRevealTiming.AnticipationGrade(result.Lineage.grade)), 0f);
                _bundleGlow.gameObject.SetActive(true);
            }
            if (_revealPortrait != null)
            {
                var sprite = CharacterUI.Portrait(CharacterUI.BaseForm(result.Lineage));
                _revealPortrait.sprite = sprite;
                _revealPortrait.enabled = sprite != null;
                _revealPortrait.color = GachaRevealTiming.SilhouetteTint;
                _revealPortrait.transform.localScale = Vector3.one * 0.6f;
                _revealPortrait.gameObject.SetActive(false);
            }
        }

        /// <summary>꾸러미 그림 번호 (UiArt.BundleState와 같은 순서)</summary>
        static class UiBundle
        {
            public const int Closed = 0;
            public const int Glow = 1;
            public const int Open = 2;
        }

        /// <summary>꾸러미 그림을 바꾼다 (바뀔 때만 — 그 그림이 없으면 묶인 그림, 그것도 없으면 그대로)</summary>
        void SetBundleState(int state)
        {
            if (state == _bundleState || _bundleImage == null) return;
            _bundleState = state;
            Sprite sprite = null;
            if (_bundleSprites != null && _bundleSprites.Length > 0)
            {
                int index = Mathf.Clamp(state, 0, _bundleSprites.Length - 1);
                sprite = _bundleSprites[index] != null ? _bundleSprites[index] : _bundleSprites[0];
            }
            if (sprite != null && _bundleImage.sprite != sprite) _bundleImage.sprite = sprite;
        }

        /// <summary>
        /// 무대 한 프레임 (t = 1회 박자 시각): 꾸러미 위치 · 기울기 · 그림 · 빛, 열림 소리 · 별가루(한 번), 초상화 크기 · 색. alpha = 무대 전체 알파.
        /// 할당 없음 (값 쓰기만).
        /// </summary>
        void StepStage(float t, ItemGrade grade, bool reduced, float alpha, ref bool opened, ref bool appeared)
        {
            if (_revealGroup != null) _revealGroup.alpha = alpha;
            if (_revealDim != null && _revealDim.gameObject.activeSelf) _revealDim.color = CharacterUI.WithAlpha(_revealDim.color, 0.9f * alpha);
            bool glow = GachaRevealTiming.AnticipationGlow(grade);
            float open = GachaRevealTiming.OpenProgress(t);
            if (_bundle != null)
            {
                float drop = reduced ? 0f : GachaRevealTiming.DropOffset(t);
                _bundle.anchoredPosition = new Vector2(_bundleHome.x, _bundleHome.y + drop);
                _bundle.localRotation = Quaternion.Euler(0f, 0f, GachaRevealTiming.ShakeAngle(t, grade, reduced));
                _bundle.localScale = Vector3.one * (1f + 0.12f * open);
            }
            if (_bundleImage != null)
            {
                SetBundleState(t >= GachaRevealTiming.ShakeEnd ? UiBundle.Open : glow && t >= GachaRevealTiming.DropEnd ? UiBundle.Glow : UiBundle.Closed);
                // 움직임 줄이기면 떨어지지 않고 알파로 나타난다. 열리는 동안 사라지며 초상화에게 자리를 준다
                float fadeIn = reduced ? Mathf.Clamp01(t / GachaRevealTiming.DropEnd) : 1f;
                _bundleImage.color = CharacterUI.WithAlpha(Color.white, fadeIn * (1f - open));
            }
            if (_bundleGlow != null)
            {
                float k = glow && t >= GachaRevealTiming.DropEnd ? Mathf.Clamp01((t - GachaRevealTiming.DropEnd) / (GachaRevealTiming.ShakeEnd - GachaRevealTiming.DropEnd)) : 0f;
                _bundleGlow.color = CharacterUI.WithAlpha(_bundleGlow.color, 0.85f * k * (1f - open));
            }
            if (!opened && t >= GachaRevealTiming.ShakeEnd)
            {
                opened = true;
                OnBundleOpened(grade, reduced);
            }
            if (_revealPortrait != null && t >= GachaRevealTiming.ShakeEnd)
            {
                if (!_revealPortrait.gameObject.activeSelf) _revealPortrait.gameObject.SetActive(true);
                float scale = reduced ? 1f : Mathf.LerpUnclamped(0.6f, 1f, UIMotion.OutBack(open));
                _revealPortrait.transform.localScale = Vector3.one * scale;
                _revealPortrait.color = GachaRevealTiming.PortraitTint(t);
            }
            if (!appeared && t >= GachaRevealTiming.OpenEnd)
            {
                appeared = true;
                UISfx.Play(GachaCardView.RevealKey(grade));
            }
        }

        /// <summary>꾸러미가 열림: 열림 소리 + 등급 빛 별가루 (★4 12 · ★5 무지개 18, 움직임 줄이기 절반 — 그 아래 등급은 작은 터짐)</summary>
        void OnBundleOpened(ItemGrade grade, bool reduced)
        {
            UISfx.Play(UISfxKey.GachaBundleOpen);
            if (_revealBurst == null) return;
            var light = GachaRevealTiming.GradeLight(grade);
            int count = GachaRevealTiming.StardustCount(grade, reduced);
            if (count <= 0)
            {
                _revealBurst.Play(light, reduced ? 4 : 8, 0.7f);
                return;
            }
            if (!GachaRevealTiming.RainbowStardust(grade))
            {
                _revealBurst.Play(light, count, 1f);
                return;
            }
            // 무지개: 여섯 빛깔로 나눠 터뜨린다 (나머지는 앞 빛깔부터 하나씩 — 합이 별가루 수, 움직임 줄이기 9 = 2·2·2·1·1·1)
            for (int i = 0; i < GachaRevealTiming.RainbowHues; i++)
            {
                int share = GachaRevealTiming.RainbowShare(count, i);
                if (share > 0) _revealBurst.Play(GachaRevealTiming.RainbowHue(i), share, 1f);
            }
        }

        void HideStage()
        {
            if (_revealStage != null && _revealStage.gameObject.activeSelf) _revealStage.gameObject.SetActive(false);
        }

        IEnumerator ShowSkipLater()
        {
            float until = Now + GachaRevealTiming.SkipAppear;
            while (Now < until) yield return null;
            _skipLater = null;
            if (_reveal != null) SetSkipChip(true);
        }

        void SetSkipChip(bool on)
        {
            if (_skipChip != null && _skipChip.gameObject.activeSelf != on) _skipChip.gameObject.SetActive(on);
        }

        // ───────────── 중복 → 기억편 녹음 ─────────────

        /// <summary>중복 한 장: 기억편 조각이 카드에서 요약 줄로 0.4초 날아가고(카드는 살짝 작아진다), 닿으면 '기억편 +n'이 늘어난다</summary>
        void StartMelt(int index)
        {
            int shards = _results[index].ShardsGained;
            if (_meltTemplate == null || _summaryText == null || !isActiveAndEnabled)
            {
                AddMeltShards(shards);
                return;
            }
            var flyer = GetFlyer(_melts.Count);
            _melts.Add(StartCoroutine(Melt(index, flyer)));
        }

        RectTransform GetFlyer(int slot)
        {
            while (_meltFlyers.Count <= slot)
            {
                var copy = Instantiate(_meltTemplate, _meltTemplate.parent);
                copy.gameObject.SetActive(false);
                _meltFlyers.Add(copy);
            }
            return _meltFlyers[slot];
        }

        IEnumerator Melt(int index, RectTransform flyer)
        {
            var card = _pool[index].transform;
            // 뒤집기 절반(옆면)이 지난 뒤 떠난다
            float until = Now + GachaRevealTiming.TenFlip;
            while (Now < until) yield return null;
            Vector3 from = card.position;
            Vector3 to = _summaryText.transform.position;
            flyer.SetAsLastSibling();
            flyer.position = from;
            flyer.localScale = Vector3.one;
            flyer.gameObject.SetActive(true);
            float start = Now;
            while (true)
            {
                float k = Mathf.Clamp01((Now - start) / GachaRevealTiming.DupMelt);
                float e = UIMotion.OutCubic(k);
                flyer.position = Vector3.LerpUnclamped(from, to, e);
                flyer.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, e);
                card.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, e);
                if (k >= 1f) break;
                yield return null;
            }
            flyer.gameObject.SetActive(false);
            AddMeltShards(_results[index].ShardsGained);
        }

        void AddMeltShards(int shards)
        {
            // 공개가 끝난 뒤 닿은 조각 (긴 프레임에 마지막 녹음이 Settle 끝과 같은 프레임에 끝남): Finish → ShowSummary가 쓴 전체 요약
            // ('새로운 계보 · 중복 → 기억편')을 '기억편 +n'으로 덮지 않는다 — 조각은 그대로 날아가 사라진다
            if (_reveal == null) return;
            _meltShards += Mathf.Max(0, shards);
            // 요약 줄이 '탭하면 바로 확인' 안내 자리와 겹친다 — 녹음이 시작되면 안내를 내린다 ([건너뛰기 ›]가 남는다)
            if (_skipHint != null && _skipHint.gameObject.activeSelf) _skipHint.gameObject.SetActive(false);
            if (_summaryText != null) _summaryText.text = MeltLine(_meltShards);
            if (!_meltSoundPlayed)
            {
                _meltSoundPlayed = true;
                UISfx.Play(UISfxKey.GachaDuplicate);
            }
        }

        /// <summary>공개 중 요약 줄 '기억편 +n' (Lavender — 끝나면 ShowSummary가 전체 요약으로 바꾼다)</summary>
        internal static string MeltLine(int shards) => $"기억편 <color={UITheme.ColorTag(UITheme.Lavender)}>+{shards:N0}</color>";

        /// <summary>공개 곁가지(건너뛰기 버튼 예약 · 녹는 조각 · 무대)를 멈추고 치운다 (카드 크기는 RevealInstant가 되돌린다)</summary>
        void StopRevealExtras()
        {
            if (_skipLater != null) StopCoroutine(_skipLater);
            _skipLater = null;
            for (int i = 0; i < _melts.Count; i++)
            {
                if (_melts[i] != null) StopCoroutine(_melts[i]);
            }
            _melts.Clear();
            for (int i = 0; i < _meltFlyers.Count; i++)
            {
                if (_meltFlyers[i] != null && _meltFlyers[i].gameObject.activeSelf) _meltFlyers[i].gameObject.SetActive(false);
            }
            SetSkipChip(false);
            HideStage();
        }

        /// <summary>남은 카드를 한 번에 공개한다 (공개 중에만 — 끝난 뒤에는 아무 일 없음)</summary>
        void Skip()
        {
            if (_reveal == null) return;
            StopCoroutine(_reveal);
            _reveal = null;
            StopRevealExtras();
            UISfx.Play(UISfxKey.GachaSkip);
            for (int i = 0; i < _pool.Count; i++) _pool[i].RevealInstant();
            Finish();
        }

        void Finish()
        {
            _skipButton.gameObject.SetActive(false);
            _skipHint.gameObject.SetActive(false);
            SetSkipChip(false);
            HideStage();
            ShowHighlight();
            ShowSummary();
            SetButtonsVisible(true);
            RefreshFormationButton();
        }

        void SetButtonsVisible(bool visible)
        {
            _buttonsVisible = visible;
            _confirmButton.interactable = visible;
            RefreshRetryInteractable();
            bool tree = _best != null;
            if (_treeButton.gameObject.activeSelf != tree) _treeButton.gameObject.SetActive(tree);
            _treeButton.interactable = visible && tree;
            if (!visible && _formationButton != null && _formationButton.gameObject.activeSelf) _formationButton.gameObject.SetActive(false);
        }

        void RefreshRetryInteractable() => _retryButton.interactable = _buttonsVisible && _onRetry != null && !_retryPending;

        /// <summary>다시 소환 비용 모양 (비용 버튼 세 상태 — 다시 빌드하기 전 씬이면 예전처럼 비용 글자 색만)</summary>
        void SetRetryAffordable(bool affordable)
        {
            if (_retryView != null)
            {
                _retryView.SetAffordable(affordable);
                return;
            }
            _retryCostText.color = affordable ? UITheme.Text : UITheme.Negative;
        }

        // ───────────── 다시 소환 (M55) ─────────────

        void OnRetryClicked()
        {
            if (_onRetry == null || _retryPending) return;
            var ui = UIManager.Instance;
            if (_retryTable != null)
            {
                double cost = Game.Gacha.GetCost(_retryTable, _retryCount);
                if (!Game.Gacha.CanAfford(_retryTable, _retryCount))
                {
                    if (_retryView != null) _retryView.Deny();
                    if (ui != null) ui.ShowShortfall(_retryTable.costCurrency, cost);
                    return;
                }
                if (_retryCount >= 10 && ui != null)
                {
                    _retryPending = true;
                    RefreshRetryInteractable();
                    ui.ShowSpendConfirm($"{_retryCount}회 더 소환", $"{_retryTable.displayName}에서 {_retryCount}번 더 소환할까요?", _retryTable.costCurrency,
                        cost, "소환", _runRetry, GachaPanel.Pull10SkipKey, null, _onRetryConfirmClosed);
                    return;
                }
            }
            else if (!_canRetry && _retryView != null)
            {
                // 표를 모르는 다시 소환 (튜토리얼 첫 만남의 1회 더 소환): 흔들고 넘긴다 — 부족 안내는 받는 쪽(GachaPanel.Pull)이 띄운다
                _retryView.Deny();
            }
            _onRetry();
        }

        void RunRetry() => _onRetry?.Invoke();

        /// <summary>확인 창이 어떻게든 닫혔다 (소환 · 취소 · 덮기 · 모두 닫기): 버튼을 다시 연다 — 새 공개 중이면 공개가 끝날 때 열린다</summary>
        void OnRetryConfirmClosed()
        {
            _retryPending = false;
            RefreshRetryInteractable();
        }

        // ───────────── 편성하러 가기 (M16) ─────────────

        void RefreshFormationButton()
        {
            if (_formationButton == null) return;
            bool formationOpen = Game.IsReady && (Game.Features == null || Game.Features.IsUnlocked(FeatureId.Formation));
            _formationLineage = formationOpen ? FormationCandidate(_results, _best, HasEmptyFormationSlot(), s_isOwned, s_isPlaced) : null;
            bool show = _formationLineage != null;
            if (_formationButton.gameObject.activeSelf != show) _formationButton.gameObject.SetActive(show);
            _formationButton.interactable = show;
        }

        /// <summary>
        /// '편성하러 가기 ›'가 가리킬 계보 (없으면 null): 편성에 빈 칸이 있을 때만, 결과 중 보유 · 미배치 계보 — 대표 결과 먼저, 그다음 결과 순서.
        /// 원정 중인 친구도 편성할 수 있으므로 원정 여부는 보지 않는다 (M05 이후, WP-2 fixer).
        /// </summary>
        internal static string FormationCandidate(IReadOnlyList<GachaResult> results, GachaResult best, bool hasEmptySlot,
            Func<string, bool> isOwned, Func<string, bool> isPlaced)
        {
            if (!hasEmptySlot || isOwned == null || isPlaced == null) return null;
            if (IsCandidate(best, isOwned, isPlaced)) return best.Lineage.id;
            if (results == null) return null;
            for (int i = 0; i < results.Count; i++)
            {
                if (IsCandidate(results[i], isOwned, isPlaced)) return results[i].Lineage.id;
            }
            return null;
        }

        static bool IsCandidate(GachaResult result, Func<string, bool> isOwned, Func<string, bool> isPlaced) =>
            result?.Lineage != null && !string.IsNullOrEmpty(result.Lineage.id) && isOwned(result.Lineage.id) && !isPlaced(result.Lineage.id);

        /// <summary>지킴이 칸 또는 열린 잔향이 칸 중 빈 곳이 있는지 (편성 빨간 점 tab.formation.empty와 같은 규칙)</summary>
        static bool HasEmptyFormationSlot()
        {
            var formation = Game.Formation;
            if (formation == null) return false;
            for (int i = 0; i < FormationSystem.GuardianSlotCount; i++)
            {
                if (formation.GetGuardian(i) == null) return true;
            }
            int echoSlots = formation.UnlockedEchoSlots;
            for (int i = 0; i < echoSlots; i++)
            {
                if (formation.GetEcho(i) == null) return true;
            }
            return false;
        }

        /// <summary>
        /// 편성 시트를 열고(UX-SPEC-IA §8.4) 그 친구 카드를 반짝인다 (FormationPanel.PulseRosterCard — 패널이 켜질 때까지 기다린다).
        /// 자동 편성이 전투력을 올리면 자동 편성 버튼도 손가락으로 가리킨다 (FR-1: PowerSystem.PreviewAutoFormationGain — 0이면 가리키지 않는다).
        /// </summary>
        void GoToFormation()
        {
            string lineageId = _formationLineage;
            if (string.IsNullOrEmpty(lineageId)) return;
            CharacterUI.CloseAllPopups();
            UINav.GoToTab(MainTab.Formation);
            FormationPanel.PulseRosterCard(lineageId);
            if (Game.Power != null && Game.Power.PreviewAutoFormationGain() > 0) UINav.FocusTarget(FormationAutoButton);
        }

        // ───────────── 대표 결과 · 요약 ─────────────

        void ShowHighlight()
        {
            if (_best == null) return;
            var lineage = _best.Lineage;
            var baseForm = CharacterUI.BaseForm(lineage);
            var gradeColor = UITheme.GradeColor(lineage.grade);
            bool lines = _highlightPower != null;

            _highlightPortrait.Set(CharacterUI.Portrait(baseForm), lineage, lineage.name, PortraitTone.Normal);
            _highlightGlow.color = CharacterUI.WithAlpha(gradeColor, _highlightGlow.color.a);
            _highlightStars.text = $"{UITheme.Stars(lineage.grade)} <size=70%>{UITheme.GradeName(lineage.grade)}</size>";
            _highlightStars.color = gradeColor;
            _highlightNewTag.SetActive(_best.IsNew);
            CharacterUI.SetTraitChips(_highlightTraitChips, lineage, false);
            var skill = baseForm != null ? baseForm.skill : null;
            _highlightSkillText.text = CharacterSkillUI.HasSkill(skill)
                ? $"<color={UITheme.ColorTag(UITheme.SubText)}>고유 스킬</color> <color={UITheme.ColorTag(CharacterSkillUI.AccentColor(skill))}><b>「{skill.name}」</b></color>"
                : string.Empty;

            if (_best.IsNew)
            {
                string role = baseForm != null ? UITheme.RoleName(baseForm.recommendedRole) : "친구";
                _highlightTitle.text = $"{lineage.name} 획득!";
                // 전투력 · 따라잡기 줄이 있는 씬은 안내 둘째 줄 대신 그 줄들을 보여 준다
                _highlightMessage.text = lines
                    ? $"새로운 계보의 {CharacterUI.WithObject(role)} 만났어요!"
                    : $"새로운 계보의 {CharacterUI.WithObject(role)} 만났어요!\n<size=85%>진화 기억도에서 앞으로의 모습을 확인해 보세요.</size>";
            }
            else
            {
                _highlightTitle.text = lineage.name;
                _highlightMessage.text = $"{CharacterUI.WithObject(lineage.name)} 기억편으로 바꿨어요. <color={UITheme.ColorTag(UITheme.Lavender)}>×{_best.ShardsGained}</color>";
            }

            long power = _best.IsNew && Game.Power != null ? Game.Power.GetLineagePower(lineage.id) : 0L;
            if (lines)
            {
                bool showPower = _best.IsNew && power > 0;
                if (_highlightPower.activeSelf != showPower) _highlightPower.SetActive(showPower);
                if (showPower && _highlightPowerText != null) _highlightPowerText.text = PowerLine(power);
            }
            if (_highlightCatchUp != null)
            {
                string catchUp = _best.IsNew ? CatchUpLine(_bestStartLevel) : string.Empty;
                bool show = !string.IsNullOrEmpty(catchUp);
                if (_highlightCatchUp.gameObject.activeSelf != show) _highlightCatchUp.gameObject.SetActive(show);
                if (show) _highlightCatchUp.text = catchUp;
            }

            if (!_signalled[_bestIndex])
            {
                SignalReveal(_bestIndex);
            }
            UISfx.Play(_best.IsNew ? UISfxKey.GachaNewFriend : UISfxKey.GachaDuplicate);

            StartCoroutine(CharacterMotion.Fade(_highlight, 0f, 1f, 0.3f));
            if (UIMotion.Reduced) _highlightPortrait.transform.localScale = Vector3.one;
            else StartCoroutine(CharacterMotion.Pop(_highlightPortrait.transform, 0.35f, 0.7f));
            _highlightBurst.Play(gradeColor, CharacterMotion.BurstCount(lineage.grade >= ItemGrade.Legendary ? 18 : 10), 0.9f);
        }

        /// <summary>새 친구 전투력 줄 (PRG-01 §7.1 #3): '전투력 1,234'</summary>
        internal static string PowerLine(long power) => $"전투력 {PowerSystem.Format(power)}";

        /// <summary>따라잡기 줄 (PRG-05 §6.5): 시작 레벨이 1보다 크면 '친구들 평균의 80%인 Lv.{n}부터 함께해요', 아니면 빈 문자열</summary>
        internal static string CatchUpLine(int startLevel) => startLevel > 1 ? $"친구들 평균의 80%인 Lv.{startLevel}부터 함께해요" : string.Empty;

        void ShowSummary()
        {
            int newCount = 0;
            int duplicates = 0;
            int shards = 0;
            foreach (var result in _results)
            {
                if (result.IsNew) newCount++;
                else
                {
                    duplicates++;
                    shards += result.ShardsGained;
                }
            }

            string gold = UITheme.ColorTag(UITheme.Gold);
            string lavender = UITheme.ColorTag(UITheme.Lavender);
            if (duplicates == 0) _summaryText.text = $"새로운 계보 <color={gold}>{newCount}</color>";
            else if (newCount == 0) _summaryText.text = $"중복 {duplicates}  →  기억편 <color={lavender}>+{shards:N0}</color>";
            else _summaryText.text = $"새로운 계보 <color={gold}>{newCount}</color>   ·   중복 {duplicates}  →  기억편 <color={lavender}>+{shards:N0}</color>";
            _shardInfo.SetActive(true);
            if (UIMotion.Reduced) _shardInfo.transform.localScale = Vector3.one;
            else StartCoroutine(CharacterMotion.Pop(_shardInfo.transform, 0.3f, 0.85f));
        }

        void OpenTree()
        {
            if (_best == null) return;
            string lineageId = _best.Lineage.id;
            Close();
            UINav.OpenCharacter(lineageId, CharacterDetailPopup.TabTree);
        }
    }
}
