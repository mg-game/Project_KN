using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 화면 HUD: 스테이지 / 보스까지 처치 진행(보스 등장 후엔 보스 체력 %), 수선점 내구도,
    /// 잔향 보유량(아이콘 + 숫자 + 게이지), AUTO / 배속 토글, 위기 시 붉은 테두리.
    /// 숫자는 값이 바뀔 때만 갱신한다.
    /// <para>
    /// 스테이지 판 (M11, UX-SPEC §2.1.3): 진행 줄 '보스까지'(20 굵게 Cream@0.8) + 막대 + '{처치}/{목표}'(22 굵게 Cream, 외곽선).
    /// 보스가 나오면 이름이 '보스 HP'(Negative)로 바뀌고 숫자는 남은 체력 '{n}%' (정수가 바뀔 때만 씀) — '보스 등장!'은 배너 몫이다.
    /// 수선점 내구도 글자는 22 굵게 Cream에 런타임 외곽선 재질(두께 0.3, #21123D, 팽창 0.1 — Awake에서 한 번 만든다)을 입히고,
    /// 채움 색(초록 · 금 · 빨강) 위에서도 읽히게 반투명 어두운 받침 알약(TextPlate)을 깐다 — 받침 폭은 자릿수가 바뀔 때만 글자 폭에 맞춘다.
    /// </para>
    /// <para>
    /// 보스에 막힘 (M13, §2.1.5): 파밍 중(BattleSystem.IsFarming — StageChanged에서 판단)이면 스테이지 이름 앞에 작은 순환 그림(Lavender)과
    /// '1-9 반복 중', 진행 줄은 '보스 도전까지 힘을 모으는 중'. 'BossChallengeButton'(금 단추 230x68, 시트에서는 SheetFade 0.5)이 x−40에서 0.25초
    /// OutCubic으로 미끄러져 들어온다: 1줄 '보스 도전 1-10'(24 굵게 Ink), 2줄 '권장 12,345 ›'(18 굵게, 모자라면 NegativeInk · 넉넉하면 PositiveInk —
    /// 누르면 강해지기 창, 없으면 강화 탭). 힘이 권장의 90% 이상이거나 마지막 실패 때보다 10% 넘게 강해지면(상태가 바뀔 때마다 한 번)
    /// 둘레 빛이 1.6초 동안 두 번 숨쉬고 옅은 금빛(α 0.3)으로 남는다 — 줄이기면 숨쉬지 않고 바로 옅은 빛.
    /// 오른쪽 44px 반복 단추는 자동 보스 도전(BattleSystem.AutoChallenge, 기본 켜짐)을 켜고 끄며, 켜져 있으면 24px Lavender 순환 배지가 붙는다.
    /// 튜토리얼 단계 중에는 숨긴다. 연속 실패로 내려가면(StepBack) 실패 배너를 '아쉬워요! 1-9에서 힘을 모아요'로 바꿔 단다 (BattleBannerView.ArmStepBack).
    /// 스테이지 판을 누르면 최근에 깬 스테이지 10개(가장 최근부터 — 아직 못 깬 앞 스테이지는 보스 도전 버튼 몫이라 빼고)를 골라 다시 갈 수 있다
    /// (튜토리얼 중에는 막힘 — 코치 대상이라). 시트가 열려 있을 때 누르면 시트를 내리고, 다 내려가 멈추면 연다 (시트 밑에서는 열지 않는다).
    /// </para>
    /// <para>
    /// 소리 (AUDIO-SPEC §8.1): AUTO = AutoToggle, 배속 = SpeedToggle, 보스 도전 = BossChallenge (빌더가 이 버튼들의 PressScale 탭 소리를 끈다).
    /// </para>
    /// </summary>
    public class BattleHud : MonoBehaviour
    {
        [Header("스테이지")]
        [SerializeField] TMP_Text _stageText;
        [SerializeField] Image _progressFill;
        [SerializeField] TMP_Text _progressText;
        [SerializeField] RectTransform _stagePanel;
        [Tooltip("진행 줄 이름: '보스까지' / '보스 HP' / 파밍 '보스 도전까지 힘을 모으는 중' (M11 · M13)")]
        [SerializeField] TMP_Text _bossLabel;
        [Tooltip("진행 막대 틀 (파밍 중 보스 전에는 숨긴다)")]
        [SerializeField] RectTransform _progressBar;
        [Tooltip("파밍 중 스테이지 이름 앞 작은 순환 그림 (Lavender — ⟲ 글자는 글꼴에 없다)")]
        [SerializeField] Image _farmIcon;
        [Tooltip("스테이지 판 누르기 → 스테이지 고르기 (튜토리얼 중 막힘)")]
        [SerializeField] Button _stagePanelButton;
        [Tooltip("분노 눈금 (PRG-02 §13.1 — 진행 막대 안 BattleConfig.enrageThreshold 자리, 2×22 Cream 0.85, 보스 단계에만). 없으면 그리지 않는다")]
        [SerializeField] Image _enrageTick;

        [Header("스테이지 고르기 (M13 P2)")]
        [SerializeField] GameObject _stageList;
        [Tooltip("목록 밖을 누르면 닫는 투명 막 (목록 뒤 전체)")]
        [SerializeField] Button _stageListBlocker;
        [SerializeField] Button[] _stageChips;
        [SerializeField] TMP_Text[] _stageChipLabels;
        [SerializeField] Image[] _stageChipBacks;

        [Header("보스 도전 (M13)")]
        [Tooltip("BossChallengeButton (SheetFade 0.5가 붙은 고정 자리 — 파밍 중에만 켠다)")]
        [SerializeField] RectTransform _bossButtonRoot;
        [Tooltip("미끄러져 들어오는 몸통 (금 단추, Button + PressScale)")]
        [SerializeField] RectTransform _bossBody;
        [SerializeField] CanvasGroup _bossBodyGroup;
        [SerializeField] Button _bossButton;
        [SerializeField] TMP_Text _bossTitle;
        [SerializeField] TMP_Text _bossRecommend;
        [SerializeField] Button _bossRecommendButton;
        [SerializeField] Button _bossLoopButton;
        [SerializeField] Image _bossLoopIcon;
        [SerializeField] GameObject _bossLoopBadge;
        [Tooltip("도전해 볼 만할 때 숨쉬는 둘레 빛")]
        [SerializeField] Image _bossGlow;
        [Tooltip("실패 배너를 바꿔 다는 배너 (StepBack)")]
        [SerializeField] BattleBannerView _banner;

        [Header("수선점 내구도")]
        [SerializeField] RectTransform _shopHpRoot;
        [SerializeField] Image _shopHpFill;
        [SerializeField] Image _shopHpTrail;
        [SerializeField] TMP_Text _shopHpText;
        [Tooltip("내구도 글자 받침 (반투명 어두운 알약, 글자 폭 + 28 — 없으면 글자만)")]
        [SerializeField] RectTransform _shopHpPlate;
        [Tooltip("수선점 보호막 (HP 바 아래쪽에 겹치는 하늘색 띠)")]
        [SerializeField] Image _shopShieldFill;

        [Header("잔향")]
        [SerializeField] TMP_Text _resonanceText;
        [SerializeField] Image _resonanceFill;
        [SerializeField] RectTransform _resonanceIcon;
        [SerializeField] Image _resonanceGlow;
        [SerializeField] TMP_Text _resonanceFloat;

        [Header("토글")]
        [SerializeField] Button _autoButton;
        [SerializeField] Image _autoBackground;
        [SerializeField] TMP_Text _autoLabel;
        [SerializeField] RectTransform _autoIcon;
        [SerializeField] Button _speedButton;
        [SerializeField] Image _speedBackground;
        [SerializeField] TMP_Text _speedLabel;
        [Tooltip("켜짐 / 꺼짐 바탕색. 아트 버튼(button_primary_9s)이면 흰색 / 옅은 회보라 (어두운 색을 곱하면 탁해진다)")]
        [SerializeField] Color _toggleOnTint = UITheme.Primary;
        [SerializeField] Color _toggleOffTint = UITheme.Secondary;

        [Header("위기 / 안내")]
        [SerializeField] Image _dangerVignette;
        [SerializeField] Button _emptyHint;

        // ───────────── 문구 · 수치 ─────────────

        const string BossUntilText = "보스까지";
        const string BossHpText = "보스 HP";
        const string FarmLabelText = "보스 도전까지 힘을 모으는 중";
        const string StrengthenLinkText = "강해지는 방법 보기 ›";
        /// <summary>파밍 10분 안내 (PRG-03 §13.2): 같은 막힌 스테이지를 이만큼(전경 실제 시간) 파밍하면 보스 도전 2줄이 6초 동안 '강해지는 방법 보기 ›' — 막힌 스테이지마다 한 번</summary>
        internal const float FarmNudgeAfter = 600f;
        internal const float FarmNudgeTime = 6f;
        /// <summary>분노 눈금 (보스가 분노하면 Danger + 한 번 톡)</summary>
        static readonly Color EnrageTickColor = new(UITheme.Cream.r, UITheme.Cream.g, UITheme.Cream.b, 0.85f);
        /// <summary>자동 보스 도전 켬 안내 — '세 판'은 BattleSystem.AutoChallengeEvery와 맞춘다 (테스트가 지킨다)</summary>
        internal const string AutoChallengeOnText = "자동 보스 도전을 켰어요. 세 판을 이기면 보스에게 다시 도전해요.";
        /// <summary>보스 도전 버튼이 미끄러져 들어오는 거리 · 시간 (x−40 → 0, 0.25초 OutCubic)</summary>
        const float SlideDistance = 40f;
        const float SlideTime = 0.25f;
        /// <summary>파밍 · 보스 도전 버튼 상태를 다시 보는 간격 (튜토리얼 끝 · 힘 변화 · 기능 열림)</summary>
        const float FarmPollInterval = 0.5f;
        /// <summary>도전해 볼 만함: 힘 ≥ 권장 × 9/10, 또는 마지막 실패 때 힘 × 11/10 이상 (10분의 1 단위 정수 — float 1.1f는 1.1보다 살짝 커서 딱 10%가 모자라게 나온다)</summary>
        internal const int ReadyRecommendedTenths = 9;
        internal const int ReadyGrowthTenths = 11;
        /// <summary>도전해 볼 만해진 순간의 둘레 빛: 1.6초 동안 두 번 숨쉬고(최대 α 0.8) 옅은 금빛 α 0.3으로 남는다 (§2.1.5 '상태가 바뀔 때 한 번')</summary>
        internal const float GlowPulseTime = 1.6f;
        const int GlowPulseBreaths = 2;
        const float GlowPeakAlpha = 0.8f;
        internal const float GlowRestAlpha = 0.3f;
        /// <summary>스테이지 고르기에 보이는 최근에 깬 스테이지 수 (가장 최근에 깬 스테이지부터 아래로)</summary>
        internal const int StageListCount = 10;
        /// <summary>수선점 내구도 글자 외곽선 (M11 — 바 채움 색이 금 · 초록 · 빨강으로 바뀌어도 읽히게, 받침 알약과 함께)</summary>
        const float HpOutlineWidth = 0.3f;
        /// <summary>내구도 글자 받침 폭 = 글자 폭 + HpPlatePad (HpPlateMin 이상, 막대 안쪽 = 막대 폭 − 2 × HpPlateInset 이하)</summary>
        internal const float HpPlatePad = 28f;
        internal const float HpPlateMin = 96f;
        const float HpPlateInset = 5f;
        const float HpFaceDilate = 0.1f;
        static readonly Color HpOutlineColor = new Color32(0x21, 0x12, 0x3D, 0xFF);

        Vector2 _shopHpBase;
        int _kills = -1;
        int _killsToBoss = -1;
        int _bossPercent = -1;
        bool _bossMode;
        bool _bossModeInitialized;
        int _shopHp = -1;
        int _shopMaxHp = -1;
        float _shopTrail = 1f;
        float _shopHit;
        float _shopShield;
        int _resonance = -1;
        float _resonancePop;
        float _floatAge = 1f;
        int _autoState = -1;
        int _speedState = -1;
        float _progressPop;
        float _vignetteKick;
        float _lowHp;
        Material _hpMaterial;
        /// <summary>받침 폭을 맞춘 자릿수 (내구도 × 100 + 최대) — 자릿수가 바뀔 때만 글자 폭을 다시 잰다</summary>
        int _hpPlateDigits = -1;

        // 파밍 · 보스 도전
        bool _farming;
        int _progressMode = -1;
        float _stageTextLeft = float.NaN;
        Vector2 _bossLabelSize;
        bool _bossButtonShown;
        float _slide = 1f;
        float _nextFarmPoll;
        int _bossTarget = -1;
        long _bossRecommendValue = -1;
        int _bossRecommendShort = -1;
        int _loopState = -1;
        bool _bossReady;
        /// <summary>파밍 10분 안내: 지금 붙잡힌 막힌 스테이지 · 그 파밍 시간 · 안내를 이미 보여 준 스테이지 · 안내가 끝나는 시각 · 톡 나이</summary>
        int _farmHeldTarget;
        float _farmHeldTime;
        int _nudgedTarget;
        float _nudgeUntil = -1f;
        float _nudgePop = -1f;
        /// <summary>분노 눈금: 켜짐 · 분노 · 톡 나이</summary>
        bool _enrageTickOn;
        bool _enraged;
        float _enragePop = -1f;
        /// <summary>둘레 빛 숨쉬기 나이 (음수 = 숨쉬기 끝 — 빛은 GlowRestAlpha로 멈춰 있고 매 프레임 색을 쓰지 않는다)</summary>
        float _glowAge = -1f;
        long _powerAtFail;
        BattleSystem _battle;
        Action<int, int> _onStepBack;
        Func<bool> _backHandler;
        bool _stageListOpen;
        /// <summary>시트에서 스테이지 판을 눌러 시트를 내리는 중 — 다 내려가 멈추면 목록을 연다 (그 사이 시트 · 팝업 · 튜토리얼이 오면 버린다)</summary>
        bool _stageListPending;
        /// <summary>시트에서 스테이지 판을 눌러 시트를 내리는 중 — 다 내려가 멈추면 챕터 지도를 연다 (CR-05, 목록 대기와 같은 규칙)</summary>
        bool _chapterMapPending;
        readonly int[] _stageListStages = new int[StageListCount];

        void Awake()
        {
            _autoButton.onClick.AddListener(ToggleAuto);
            _speedButton.onClick.AddListener(ToggleSpeed);
            if (_resonanceFloat != null) _resonanceFloat.alpha = 0f;
            if (_shopHpRoot != null) _shopHpBase = _shopHpRoot.anchoredPosition;
            if (_emptyHint != null)
            {
                _emptyHint.gameObject.SetActive(false);
                _emptyHint.onClick.AddListener(() => UINav.GoToTab(MainTab.Formation));
            }
            if (_bossLabel != null) _bossLabelSize = _bossLabel.rectTransform.sizeDelta;
            if (_stageText != null) _stageTextLeft = _stageText.rectTransform.offsetMin.x;
            if (_farmIcon != null) _farmIcon.enabled = false;
            if (_bossButton != null) _bossButton.onClick.AddListener(OnBossChallenge);
            if (_bossRecommendButton != null) _bossRecommendButton.onClick.AddListener(OpenStrengthen);
            if (_bossLoopButton != null) _bossLoopButton.onClick.AddListener(ToggleAutoChallenge);
            if (_bossButtonRoot != null) _bossButtonRoot.gameObject.SetActive(false);
            if (_bossGlow != null) _bossGlow.enabled = false;
            if (_stagePanelButton != null) _stagePanelButton.onClick.AddListener(OnStagePanelClicked);
            if (_stageListBlocker != null) _stageListBlocker.onClick.AddListener(CloseStageList);
            for (int i = 0; _stageChips != null && i < _stageChips.Length; i++)
            {
                int index = i;
                if (_stageChips[i] != null) _stageChips[i].onClick.AddListener(() => OnStageChip(index));
            }
            if (_stageList != null) _stageList.SetActive(false);
            _onStepBack = OnStepBack;
            _backHandler = OnBack;
            ApplyHpOutline();
        }

        void Start()
        {
            if (!Game.IsReady || Game.Battle == null) return;
            _battle = Game.Battle;
            _battle.StepBack += _onStepBack;
        }

        void OnDestroy()
        {
            if (_battle != null) _battle.StepBack -= _onStepBack;
            _battle = null;
            if (UIManager.Instance != null) UIManager.Instance.RemoveBackHandler(_backHandler);
            if (_hpMaterial != null) Destroy(_hpMaterial);
        }

        /// <summary>
        /// 수선점 내구도 글자에 외곽선 재질을 한 번 만들어 입힌다 (M11: OUTLINE_ON · 두께 0.3 · #21123D · 팽창 0.1 — 빌더에 넣지 않는다,
        /// BattleFx.CreateOutlineMaterial과 같은 방식). 글꼴 재질에 외곽선 속성이 없으면 그대로 둔다
        /// </summary>
        void ApplyHpOutline()
        {
            if (_shopHpText == null) return;
            var source = _shopHpText.fontSharedMaterial;
            if (source == null || !source.HasProperty(ShaderUtilities.ID_OutlineWidth)) return;
            _hpMaterial = new Material(source) { name = source.name + " (Shop HP)" };
            _hpMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            _hpMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, HpOutlineWidth);
            _hpMaterial.SetColor(ShaderUtilities.ID_OutlineColor, HpOutlineColor);
            if (_hpMaterial.HasProperty(ShaderUtilities.ID_FaceDilate)) _hpMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, HpFaceDilate);
            _shopHpText.fontSharedMaterial = _hpMaterial;
        }

        /// <summary>편성이 비어 전투가 열리지 않을 때 안내 (누르면 편성 탭)</summary>
        public void SetEmptyHint(bool show)
        {
            if (_emptyHint != null && _emptyHint.gameObject.activeSelf != show) _emptyHint.gameObject.SetActive(show);
        }

        /// <summary>새 전투 시작 (StageChanged · 처음): 스테이지 이름 + 파밍 · 보스 도전 버튼을 여기서 다시 판단한다 (StepBack 때가 아니라 — §2.1.5)</summary>
        public void SetStage(string stageLabel)
        {
            _farming = IsFarmingNow();
            ApplyStageTitle(stageLabel);
            _kills = -1;
            _killsToBoss = -1;
            _bossPercent = -1;
            _bossModeInitialized = false;
            _shopHp = -1;
            _shopTrail = 1f;
            RefreshBossButton(true);
            if (_stageListOpen) CloseStageList();
        }

        public void Tick(BattleSimulator sim, float dt)
        {
            float time = Time.unscaledTime;
            if (sim != null)
            {
                TickProgress(sim, dt, time);
                TickShopHp(sim, dt);
                TickResonance(sim, dt);
            }
            TickToggles(time, dt);
            TickVignette(sim, dt, time);
            TickFarm(time, Time.unscaledDeltaTime);
            if (_stageListOpen && (UIFrame.SheetOpen || PopupOpen || TutorialActive)) CloseStageList();
            if (_stageListPending) TickStageListPending();
            if (_chapterMapPending) TickChapterMapPending();
        }

        // ───────────── 이벤트 반응 ─────────────

        public void OnKill() => _progressPop = 1f;

        public void OnShopDamaged() => _shopHit = 1f;

        public void OnBossSpawned()
        {
            _vignetteKick = 1f;
            _enraged = false;
            _enragePop = -1f;
            if (_enrageTick != null) _enrageTick.color = EnrageTickColor;
        }

        /// <summary>보스가 분노했다 (EnemyEnraged — 분노 눈금이 Danger로 바뀌며 한 번 톡, 말로는 분노 알림이 이미 알린다)</summary>
        public void OnBossEnraged()
        {
            if (_enraged) return;
            _enraged = true;
            _enragePop = UIMotion.Reduced ? -1f : 0f;
            if (_enrageTick != null) _enrageTick.color = UITheme.Danger;
        }

        /// <summary>분노 눈금 (보스 단계에만, 막대 안 enrageThreshold 자리 — 빌더가 앵커를 둔다). 할당 없음</summary>
        void TickEnrageTick(bool bossMode, float dt)
        {
            if (_enrageTick == null) return;
            if (bossMode != _enrageTickOn)
            {
                _enrageTickOn = bossMode;
                _enrageTick.enabled = bossMode;
                if (bossMode)
                {
                    float threshold = Game.IsReady && Game.Battle != null ? Mathf.Clamp01(Game.Battle.Config.enrageThreshold) : 0.5f;
                    var rt = _enrageTick.rectTransform;
                    rt.anchorMin = new Vector2(threshold, rt.anchorMin.y);
                    rt.anchorMax = new Vector2(threshold, rt.anchorMax.y);
                    if (!_enraged) _enrageTick.color = EnrageTickColor;
                }
            }
            if (!bossMode || _enragePop < 0f) return;
            _enragePop += dt;
            float k = Mathf.Clamp01(_enragePop / 0.3f);
            float s = 1f + 0.6f * BattleEase.Punch(k);
            _enrageTick.rectTransform.localScale = new Vector3(s, s, 1f);
            if (k >= 1f)
            {
                _enragePop = -1f;
                _enrageTick.rectTransform.localScale = Vector3.one;
            }
        }

        /// <summary>잔향 변화 표시 (+환급 / -소환 비용)</summary>
        public void ShowResonanceDelta(int delta)
        {
            if (_resonanceFloat == null || delta == 0) return;
            _resonanceFloat.text = delta > 0 ? BattleEase.Plus(delta) : BattleEase.Minus(-delta);
            _resonanceFloat.color = delta > 0 ? UITheme.Resonance : UITheme.Negative;
            _floatAge = 0f;
            _resonancePop = 1f;
        }

        // ───────────── 갱신 ─────────────

        /// <summary>진행 줄 모양: 0 = 보스까지(처치 수), 1 = 파밍 중 보스 전(안내 한 줄), 2 = 보스 체력</summary>
        void ApplyProgressMode(int mode)
        {
            _progressMode = mode;
            _kills = -1;
            _killsToBoss = -1;
            _bossPercent = -1;
            bool farm = mode == 1;
            if (_bossLabel != null)
            {
                _bossLabel.text = mode == 2 ? BossHpText : farm ? FarmLabelText : BossUntilText;
                _bossLabel.color = mode == 2 ? UITheme.Negative : farm ? UITheme.SubText : BattleEase.WithAlpha(UITheme.Cream, 0.8f);
                // 파밍 안내는 줄 전체를 쓴다 (막대 · 숫자를 숨긴다)
                var size = _bossLabelSize;
                if (farm && _stagePanel != null) size.x = Mathf.Max(size.x, _stagePanel.rect.width - _bossLabel.rectTransform.anchoredPosition.x - 14f);
                _bossLabel.rectTransform.sizeDelta = size;
            }
            if (_progressBar != null && _progressBar.gameObject.activeSelf == farm) _progressBar.gameObject.SetActive(!farm);
            if (_progressText.gameObject.activeSelf == farm) _progressText.gameObject.SetActive(!farm);
            _progressText.color = mode == 2 ? UITheme.Negative : UITheme.Cream;
            // 보스 % 맥박이 CanvasRenderer에 남긴 색을 지운다 (perf PF-5 F8)
            _progressText.canvasRenderer.SetColor(Color.white);
            _progressFill.color = mode == 2 ? UITheme.Danger : UITheme.Lavender;
        }

        void TickProgress(BattleSimulator sim, float dt, float time)
        {
            bool bossMode = sim.BossSpawned;
            int mode = bossMode ? 2 : _farming ? 1 : 0;
            if (!_bossModeInitialized || bossMode != _bossMode || mode != _progressMode)
            {
                _bossModeInitialized = true;
                _bossMode = bossMode;
                ApplyProgressMode(mode);
            }

            if (bossMode)
            {
                float ratio = 0f;
                var enemies = sim.Enemies;
                int count = enemies != null ? enemies.Count : 0;
                for (int i = 0; i < count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy == null || !enemy.Alive || !enemy.IsBoss) continue;
                    ratio = enemy.MaxHp > 0f ? Mathf.Clamp01(enemy.Hp / enemy.MaxHp) : 0f;
                    break;
                }
                // 남은 체력 % (정수가 바뀔 때만 — SetText 서식은 할당 없음)
                int percent = BossPercent(ratio);
                if (percent != _bossPercent)
                {
                    _bossPercent = percent;
                    _progressText.SetText("{0}%", percent);
                }
                BattleEase.SetBar(_progressFill, BattleEase.Damp(BattleEase.BarRatio(_progressFill), ratio, 10f, dt));
                float pulse = UIMotion.Reduced ? 0f : 0.5f + 0.5f * Mathf.Sin(time * 6f);
                // 맥박은 CanvasRenderer 색으로 (perf PF-5 F8 — TMP color를 매 프레임 쓰면 글 메시를 매 프레임 새로 만든다). 글 색은 흰색 한 번
                if (_progressText.color != Color.white) _progressText.color = Color.white;
                _progressText.canvasRenderer.SetColor(Color.Lerp(UITheme.Negative, UITheme.Cream, pulse * 0.6f));
            }
            else
            {
                int kills = sim.Kills;
                int total = Mathf.Max(1, sim.KillsToBoss);
                if (mode == 0 && (kills != _kills || total != _killsToBoss))
                {
                    _kills = kills;
                    _killsToBoss = total;
                    _progressText.SetText("{0}/{1}", Mathf.Min(kills, total), total);
                }
                BattleEase.SetBar(_progressFill, BattleEase.Damp(BattleEase.BarRatio(_progressFill), Mathf.Clamp01(kills / (float)total), 10f, dt));
            }

            _progressPop = Mathf.Max(0f, _progressPop - dt * 4f);
            if (_stagePanel != null)
            {
                float scale = 1f + 0.03f * BattleEase.Punch(1f - _progressPop);
                // 같은 값이면 쓰지 않는다 (perf PF-5 F9 — 쓰기만 해도 Battle 루트 Canvas가 매 프레임 다시 묶인다)
                if (_stagePanel.localScale.x != scale) _stagePanel.localScale = new Vector3(scale, scale, 1f);
            }
            TickEnrageTick(bossMode, dt);
        }

        /// <summary>보스 남은 체력 % (올림 — 1%라도 남으면 1, 0~100)</summary>
        internal static int BossPercent(float ratio) => Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(ratio) * 100f - 0.0001f), 0, 100);

        void TickShopHp(BattleSimulator sim, float dt)
        {
            float max = Mathf.Max(1f, sim.ShopMaxHp);
            float ratio = Mathf.Clamp01(sim.ShopHp / max);
            BattleEase.SetBar(_shopHpFill, ratio);
            _shopHpFill.color = ratio > 0.5f ? Color.Lerp(UITheme.Gold, UITheme.Positive, (ratio - 0.5f) * 2f)
                : Color.Lerp(UITheme.Negative, UITheme.Gold, ratio * 2f);
            _shopTrail = ratio >= _shopTrail ? ratio : Mathf.MoveTowards(_shopTrail, ratio, dt * 0.5f);
            BattleEase.SetBar(_shopHpTrail, _shopTrail);

            int hp = Mathf.CeilToInt(sim.ShopHp);
            int maxHp = Mathf.CeilToInt(max);
            if (hp != _shopHp || maxHp != _shopMaxHp)
            {
                _shopHp = hp;
                _shopMaxHp = maxHp;
                _shopHpText.SetText("{0} / {1}", Mathf.Max(0, hp), maxHp);
                int digits = Digits(Mathf.Max(0, hp)) * 100 + Digits(maxHp);
                if (digits != _hpPlateDigits)
                {
                    _hpPlateDigits = digits;
                    FitHpPlate();
                }
            }

            if (_shopShieldFill != null)
            {
                float shield = Mathf.Clamp01(sim.ShopShield / max);
                _shopShield = BattleEase.Damp(_shopShield, shield, 10f, dt);
                bool show = _shopShield > 0.002f;
                if (_shopShieldFill.enabled != show) _shopShieldFill.enabled = show;
                if (show)
                {
                    BattleEase.SetBar(_shopShieldFill, Mathf.Max(0.04f, _shopShield));
                    _shopShieldFill.color = BattleEase.WithAlpha(UITheme.SoftBlue, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f));
                }
            }

            _shopHit = Mathf.Max(0f, _shopHit - dt * 4f);
            if (_shopHpRoot != null)
            {
                _shopHpRoot.anchoredPosition = _shopHpBase + new Vector2(Mathf.Sin(_shopHit * 50f) * 6f * _shopHit, 0f);
            }
            _lowHp = ratio;
        }

        /// <summary>
        /// 내구도 글자 받침(반투명 어두운 바느질 알약, 빌더 'ShopHpBar/TextPlate')을 글자 폭 + HpPlatePad에 맞춘다 (막대 안쪽 폭을 넘지 않게).
        /// 자릿수가 바뀔 때만 부른다 — preferredWidth는 방금 쓴 글을 한 번 잰다 (할당 없음)
        /// </summary>
        void FitHpPlate()
        {
            if (_shopHpPlate == null || _shopHpText == null) return;
            var bar = _shopHpPlate.parent as RectTransform;
            float limit = bar != null ? bar.rect.width - HpPlateInset * 2f : float.MaxValue;
            _shopHpPlate.sizeDelta = new Vector2(HpPlateWidth(_shopHpText.preferredWidth, limit), _shopHpPlate.sizeDelta.y);
        }

        /// <summary>받침 폭 = 글자 폭 + HpPlatePad, HpPlateMin 이상 · limit(막대 안쪽 폭) 이하. 순수 계산</summary>
        internal static float HpPlateWidth(float textWidth, float limit) =>
            Mathf.Min(Mathf.Max(textWidth + HpPlatePad, HpPlateMin), Mathf.Max(0f, limit));

        /// <summary>0 이상 정수의 자릿수 (0 → 1)</summary>
        internal static int Digits(int value)
        {
            int digits = 1;
            for (int v = Mathf.Max(0, value); v >= 10; v /= 10) digits++;
            return digits;
        }

        void TickResonance(BattleSimulator sim, float dt)
        {
            int value = Mathf.FloorToInt(sim.Resonance + 0.001f);
            if (value != _resonance)
            {
                _resonance = value;
                _resonanceText.text = BattleEase.Int(value);
            }
            float max = Mathf.Max(1f, sim.MaxResonance);
            BattleEase.SetBar(_resonanceFill, BattleEase.Damp(BattleEase.BarRatio(_resonanceFill), Mathf.Clamp01(sim.Resonance / max), 10f, dt));

            _resonancePop = Mathf.Max(0f, _resonancePop - dt * 3f);
            float time = Time.unscaledTime;
            if (_resonanceIcon != null)
            {
                float scale = 1f + 0.25f * BattleEase.Punch(1f - _resonancePop) + 0.04f * Mathf.Sin(time * 2.5f);
                _resonanceIcon.localScale = new Vector3(scale, scale, 1f);
            }
            if (_resonanceGlow != null) _resonanceGlow.color = BattleEase.WithAlpha(UITheme.Resonance, 0.3f + 0.15f * Mathf.Sin(time * 2.5f) + 0.4f * _resonancePop);

            if (_resonanceFloat != null && _floatAge < 1f)
            {
                _floatAge += dt / 0.9f;
                float k = Mathf.Clamp01(_floatAge);
                _resonanceFloat.alpha = 1f - k * k;
                _resonanceFloat.rectTransform.anchoredPosition = new Vector2(_resonanceFloat.rectTransform.anchoredPosition.x, 30f + 30f * BattleEase.OutCubic(k));
            }
        }

        void TickToggles(float time, float dt)
        {
            if (!Game.IsReady || Game.Battle == null) return;
            int auto = Game.Battle.AutoMode ? 1 : 0;
            if (auto != _autoState)
            {
                _autoState = auto;
                _autoBackground.color = auto == 1 ? _toggleOnTint : _toggleOffTint;
                _autoLabel.color = auto == 1 ? UITheme.Cream : UITheme.SubText;
            }
            if (_autoIcon != null && auto == 1 && !UIMotion.Reduced) _autoIcon.localRotation = Quaternion.Euler(0f, 0f, -time * 120f);

            int speed = Game.Battle.Speed;
            if (speed != _speedState)
            {
                _speedState = speed;
                _speedLabel.text = speed >= 2 ? "x2" : "x1";
                _speedBackground.color = speed >= 2 ? _toggleOnTint : _toggleOffTint;
                _speedLabel.color = speed >= 2 ? UITheme.Cream : UITheme.SubText;
            }
        }

        /// <summary>
        /// 전장 가장자리 붉은 비네트 (전장만 덮고 HUD·덱은 덮지 않는다): 수선점 내구도 30% 미만이면 1Hz로 맥박치고 (낮을수록 진하게),
        /// 보스 등장·분노 때 한 번 번쩍인다 (ATK-03)
        /// </summary>
        void TickVignette(BattleSimulator sim, float dt, float time)
        {
            if (_dangerVignette == null) return;
            _vignetteKick = Mathf.Max(0f, _vignetteKick - dt * 1.2f);
            float threshold = EnemyFxCatalog.LowHpRatio;
            float low = sim != null && _lowHp < threshold ? 0.4f + 0.6f * Mathf.Clamp01((threshold - _lowHp) / threshold) : 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f);
            float alpha = low * (0.2f + 0.35f * pulse) + _vignetteKick * 0.6f;
            _dangerVignette.enabled = alpha > 0.01f;
            _dangerVignette.color = BattleEase.WithAlpha(UITheme.Danger, Mathf.Clamp01(alpha));
        }

        void ToggleAuto()
        {
            if (!Game.IsReady || Game.Battle == null) return;
            Game.Battle.AutoMode = !Game.Battle.AutoMode;
            UISfx.Play(UISfxKey.AutoToggle);
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowToast(Game.Battle.AutoMode ? "자동 전투: 잔향이를 알아서 부르고 불도 밝혀요." : "자동 전투를 껐어요. 직접 잔향이를 불러 주세요!");
            }
        }

        void ToggleSpeed()
        {
            if (!Game.IsReady || Game.Battle == null) return;
            Game.Battle.Speed = Game.Battle.Speed >= 2 ? 1 : 2;
            UISfx.Play(UISfxKey.SpeedToggle);
        }

        // ───────────── 파밍 · 보스 도전 (M13) ─────────────

        static bool TutorialActive => Game.IsReady && Game.Tutorial != null && Game.Tutorial.IsActive;

        static bool PopupOpen => UIManager.Instance != null && UIManager.Instance.HasOpenPopup;

        static bool IsFarmingNow() => Game.IsReady && Game.Battle != null && Game.Battle.IsFarming;

        /// <summary>스테이지 이름: 평소 '스테이지 1-9', 파밍 중 [순환 그림] '1-9 반복 중' (StageChanged · 파밍 상태가 바뀔 때만 — 그때만 글자를 만든다)</summary>
        void ApplyStageTitle(string stageLabel)
        {
            if (_stageText == null) return;
            bool farm = _farming && !string.IsNullOrEmpty(stageLabel);
            _stageText.text = string.IsNullOrEmpty(stageLabel) ? "스테이지" : farm ? stageLabel + " 반복 중" : "스테이지 " + stageLabel;
            if (_farmIcon != null) _farmIcon.enabled = farm;
            if (!float.IsNaN(_stageTextLeft))
            {
                var rect = _stageText.rectTransform;
                float left = farm && _farmIcon != null ? _stageTextLeft + _farmIcon.rectTransform.sizeDelta.x + 6f : _stageTextLeft;
                if (!Mathf.Approximately(rect.offsetMin.x, left)) rect.offsetMin = new Vector2(left, rect.offsetMin.y);
            }
        }

        void TickFarm(float time, float unscaledDt)
        {
            if (time >= _nextFarmPoll)
            {
                _nextFarmPoll = time + FarmPollInterval;
                // 튜토리얼이 끝나거나 기능이 열려 파밍 상태가 바뀌면 이름 · 버튼을 다시 (보스 도전 버튼은 StageChanged가 먼저 정한다)
                bool farming = IsFarmingNow();
                if (farming != _farming && Game.IsReady && Game.Battle != null && !Game.Battle.IsBetweenStages)
                {
                    _farming = farming;
                    ApplyStageTitle(Game.Battle.StageLabel);
                }
                RefreshBossButton(false);
            }

            TickFarmNudge(time, unscaledDt);
            if (!_bossButtonShown) return;
            // 미끄러져 들어오기 (x−40 → 0, 0.25초 OutCubic — 줄이기면 바로)
            if (_slide < 1f && _bossBody != null)
            {
                _slide = UIMotion.Reduced ? 1f : Mathf.Min(1f, _slide + unscaledDt / SlideTime);
                float k = UIMotion.OutCubic(_slide);
                _bossBody.anchoredPosition = new Vector2(-SlideDistance * (1f - k), 0f);
                if (_bossBodyGroup != null) _bossBodyGroup.alpha = k;
            }
            // 도전해 볼 만해진 순간 한 번만 숨쉰다 (끝나면 옅은 빛으로 멈추고 색을 다시 쓰지 않는다)
            if (_bossGlow != null && _bossReady && _glowAge >= 0f)
            {
                _glowAge += unscaledDt;
                _bossGlow.color = BattleEase.WithAlpha(UITheme.Gold, GlowPulseAlpha(_glowAge));
                if (_glowAge >= GlowPulseTime) _glowAge = -1f;
            }
        }

        /// <summary>
        /// 파밍 10분 안내 (PRG-03 §13.2 — 자동 창은 띄우지 않는다): 같은 막힌 스테이지(challengeTarget)를 앞에서 10분 넘게 파밍하면 보스 도전 버튼 2줄이
        /// 6초 동안 '강해지는 방법 보기 ›'이 되고 버튼이 한 번 톡 (누르면 지금처럼 강해지기 창). 막힌 스테이지마다 한 번. 할당 없음
        /// </summary>
        void TickFarmNudge(float time, float unscaledDt)
        {
            int target = _farming && Game.IsReady && Game.Battle != null ? Game.Battle.ChallengeTarget : 0;
            if (target != _farmHeldTarget)
            {
                _farmHeldTarget = target;
                _farmHeldTime = 0f;
            }
            if (target > 0 && unscaledDt > 0f && unscaledDt < 1f) _farmHeldTime += unscaledDt;
            if (_nudgeUntil >= 0f && time >= _nudgeUntil)
            {
                _nudgeUntil = -1f;
                // 권장 줄을 다시 쓴다
                _bossRecommendValue = -1;
                _bossRecommendShort = -1;
                RefreshBossButton(false);
            }
            if (_nudgePop >= 0f && _bossBody != null)
            {
                _nudgePop += unscaledDt;
                float k = Mathf.Clamp01(_nudgePop / 0.5f);
                float s = 1f + 0.06f * BattleEase.Punch(k);
                _bossBody.localScale = new Vector3(s, s, 1f);
                if (k >= 1f)
                {
                    _nudgePop = -1f;
                    _bossBody.localScale = Vector3.one;
                }
            }
            if (!ShouldNudge(target, _farmHeldTime, _nudgedTarget) || !_bossButtonShown || _bossRecommend == null) return;
            _nudgedTarget = target;
            _nudgeUntil = time + FarmNudgeTime;
            _nudgePop = UIMotion.Reduced ? -1f : 0f;
            _bossRecommend.text = StrengthenLinkText;
            _bossRecommend.color = UITheme.Ink;
        }

        /// <summary>파밍 10분 안내를 지금 띄울지 (순수): 막힌 스테이지가 있고 10분 넘게 붙잡혔고 그 스테이지에 아직 안 띄웠을 때</summary>
        internal static bool ShouldNudge(int target, float heldSeconds, int nudgedTarget) =>
            target > 0 && heldSeconds >= FarmNudgeAfter && target != nudgedTarget;

        /// <summary>둘레 빛 숨쉬기 시작 (도전해 볼 만해진 순간) — 줄이기면 숨쉬지 않고 바로 옅은 빛</summary>
        void StartGlowPulse()
        {
            if (_bossGlow == null) return;
            _glowAge = UIMotion.Reduced ? -1f : 0f;
            _bossGlow.color = BattleEase.WithAlpha(UITheme.Gold, UIMotion.Reduced ? GlowRestAlpha : 0f);
        }

        /// <summary>
        /// 둘레 빛 α (나이 초): GlowPulseTime 동안 GlowPulseBreaths번 0 → 0.8로 숨쉬고, 바닥은 0에서 GlowRestAlpha로 올라와 끝에 그대로 멈춘다.
        /// 순수 계산 — 0 이하 = 0, GlowPulseTime 이상 = GlowRestAlpha
        /// </summary>
        internal static float GlowPulseAlpha(float age)
        {
            if (age >= GlowPulseTime) return GlowRestAlpha;
            float k = Mathf.Clamp01(age / GlowPulseTime);
            float wave = 0.5f - 0.5f * Mathf.Cos(k * GlowPulseBreaths * 2f * Mathf.PI);
            return Mathf.Lerp(GlowRestAlpha * k, GlowPeakAlpha, wave);
        }

        /// <summary>
        /// 보스 도전 버튼을 켜고 끄고 글자 · 색 · 빛을 맞춘다. 켜짐 = 파밍 중 + 튜토리얼 아님. 새로 켜지면 x−40에서 미끄러져 들어온다.
        /// 글자는 막힌 스테이지 · 권장 · 모자람이 바뀔 때만 쓴다
        /// </summary>
        void RefreshBossButton(bool stageChanged)
        {
            if (_bossButtonRoot == null) return;
            bool show = _farming && !TutorialActive && Game.IsReady && Game.Battle != null;
            if (show != _bossButtonShown)
            {
                // 켜짐은 StageChanged(새 전투)나 튜토리얼이 끝난 뒤에만 — 결과 배너 대기 중(IsBetweenStages)에는 새로 켜지 않는다
                if (show && !stageChanged && Game.Battle.IsBetweenStages) return;
                _bossButtonShown = show;
                _bossButtonRoot.gameObject.SetActive(show);
                if (show)
                {
                    _slide = UIMotion.Reduced ? 1f : 0f;
                    if (_bossBody != null) _bossBody.anchoredPosition = new Vector2(_slide >= 1f ? 0f : -SlideDistance, 0f);
                    if (_bossBodyGroup != null) _bossBodyGroup.alpha = _slide >= 1f ? 1f : 0f;
                    _bossTarget = -1;
                    _bossRecommendValue = -1;
                    _bossRecommendShort = -1;
                    _loopState = -1;
                    // 새로 나타난 버튼은 '도전해 볼 만함'을 다시 판단한다 (맞으면 그때 한 번 숨쉰다)
                    _bossReady = false;
                    _glowAge = -1f;
                    if (_bossGlow != null) _bossGlow.enabled = false;
                }
            }
            if (!show) return;

            var battle = Game.Battle;
            int target = battle.ChallengeTarget;
            if (target != _bossTarget)
            {
                _bossTarget = target;
                if (_bossTitle != null) _bossTitle.text = "보스 도전 " + BattleSystem.FormatStage(target);
            }
            long power = Game.Power != null ? Game.Power.TeamPower : 0L;
            long recommended = Game.Power != null ? Game.Power.GetRecommendedPower(target) : 0L;
            if (recommended != _bossRecommendValue && _bossRecommend != null && _nudgeUntil < 0f)
            {
                _bossRecommendValue = recommended;
                _bossRecommend.text = "권장 " + recommended.ToString("N0", CultureInfo.InvariantCulture) + " ›";
            }
            int isShort = power < recommended ? 1 : 0;
            if (isShort != _bossRecommendShort && _bossRecommend != null && _nudgeUntil < 0f)
            {
                _bossRecommendShort = isShort;
                _bossRecommend.color = isShort == 1 ? UITheme.NegativeInk : UITheme.PositiveInk;
            }
            int loop = battle.AutoChallenge ? 1 : 0;
            if (loop != _loopState)
            {
                _loopState = loop;
                if (_bossLoopBadge != null) _bossLoopBadge.SetActive(loop == 1);
                if (_bossLoopIcon != null) _bossLoopIcon.color = loop == 1 ? UITheme.Cream : BattleEase.WithAlpha(UITheme.SubText, 0.6f);
            }
            bool ready = IsChallengeReady(power, recommended, _powerAtFail);
            if (ready != _bossReady)
            {
                _bossReady = ready;
                _glowAge = -1f;
                if (_bossGlow != null)
                {
                    _bossGlow.enabled = ready;
                    if (ready) StartGlowPulse();
                }
            }
        }

        /// <summary>
        /// 도전해 볼 만한지: 힘 ≥ 권장 × 0.9, 또는 마지막 실패 때 힘(0 = 모름)의 1.1배 이상.
        /// 10분의 1 단위 정수로 비교한다 (float 1.1f를 double로 넓히면 1.100000023…이라 1,000 → 딱 1,100이 모자라게 나왔다) —
        /// decimal이라 아주 큰 힘(× 11)에서도 넘치지 않는다
        /// </summary>
        internal static bool IsChallengeReady(long power, long recommended, long powerAtFail)
        {
            if (recommended > 0 && (decimal)power * 10 >= (decimal)recommended * ReadyRecommendedTenths) return true;
            return powerAtFail > 0 && (decimal)power * 10 >= (decimal)powerAtFail * ReadyGrowthTenths;
        }

        /// <summary>연속 실패로 한 단계 내려감 (같은 실패의 StageEnded 바로 앞): 그때 힘을 기억하고 실패 배너를 바꿔 단다</summary>
        void OnStepBack(int blocked, int lowered)
        {
            _powerAtFail = Game.IsReady && Game.Power != null ? Game.Power.TeamPower : 0L;
            if (_banner == null) return;
            _banner.ArmStepBack(StepBackTitle(lowered), StrengthenLinkText, UITheme.DustyPink, OpenStrengthen);
        }

        /// <summary>'아쉬워요! 1-9에서 힘을 모아요' (내려간 스테이지)</summary>
        internal static string StepBackTitle(int lowered) => "아쉬워요! " + BattleSystem.FormatStage(lowered) + "에서 힘을 모아요";

        void OnBossChallenge()
        {
            if (!Game.IsReady || Game.Battle == null) return;
            if (Game.Battle.ChallengeBoss())
            {
                UISfx.Play(UISfxKey.BossChallenge);
                return;
            }
            // 이미 막힌 스테이지 이상이면 버튼만 거둔다 (다음 StageChanged가 다시 판단)
            _farming = false;
            RefreshBossButton(true);
        }

        /// <summary>강해지기 창 (C-12) — 없으면 강화 탭</summary>
        static void OpenStrengthen()
        {
            if (StrengthenPopup.TryOpen()) return;
            UINav.GoToTab(MainTab.Growth);
        }

        void ToggleAutoChallenge()
        {
            if (!Game.IsReady || Game.Battle == null) return;
            bool on = !Game.Battle.AutoChallenge;
            Game.Battle.AutoChallenge = on;
            UISfx.Play(on ? UISfxKey.ToggleOn : UISfxKey.ToggleOff);
            _loopState = -1;
            RefreshBossButton(false);
            if (UIManager.Instance != null)
            {
                // '세 판' = BattleSystem.AutoChallengeEvery (파밍 스테이지를 세 판 이기면 막힌 보스로 간다)
                UIManager.Instance.ShowToast(on ? AutoChallengeOnText : "자동 보스 도전을 껐어요. 준비되면 버튼을 눌러 도전해요.");
            }
        }

        // ───────────── 스테이지 고르기 (M13 P2) ─────────────

        /// <summary>스테이지 판을 눌렀을 때 할 일 (CR-05 · RM-2 S-7)</summary>
        internal enum StagePanelAction
        {
            None,
            CloseList,
            ChapterMap,
            StageList,
        }

        /// <summary>
        /// 스테이지 판 길 (순수): 튜토리얼 · 팝업 위 · 준비 전(blocked)이면 아무것도 / 예전 목록이 열려 있으면 닫기 /
        /// 챕터 지도를 받을 쪽이 있으면(UINav.HasChapterMap — P4-6 ChapterMapPopup) 지도 / 아니면 WP-5A 스테이지 목록 (대신 길 — 목록 지우기는 RM-3 고침 몫)
        /// </summary>
        internal static StagePanelAction RouteStagePanel(bool blocked, bool listOpen, bool hasChapterMap)
        {
            if (blocked) return StagePanelAction.None;
            if (listOpen) return StagePanelAction.CloseList;
            return hasChapterMap ? StagePanelAction.ChapterMap : StagePanelAction.StageList;
        }

        void OnStagePanelClicked()
        {
            // 스테이지 판은 코치 대상(첫 보스 단계)이라 튜토리얼 중에는 아무 일도 없다. 팝업 위에서도 열지 않는다 (Tick의 닫기 규칙과 같게)
            bool blocked = TutorialActive || PopupOpen || !Game.IsReady || Game.Battle == null;
            switch (RouteStagePanel(blocked, _stageListOpen, UINav.HasChapterMap))
            {
                case StagePanelAction.None:
                    return;
                case StagePanelAction.CloseList:
                    CloseStageList();
                    return;
                case StagePanelAction.ChapterMap:
                    OpenChapterMapFromPanel();
                    return;
            }
            var battle = Game.Battle;
            int count = StageListStages(battle.HighestStage, _stageListStages);
            if (!HasStageChoice(_stageListStages, count, battle.Stage))
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowToast("아직 다시 가 볼 스테이지가 없어요.");
                return;
            }
            if (UIFrame.SheetOpen)
            {
                // 시트 밑에서는 열지 않는다 (Tick이 바로 닫는다) — 시트를 내리고, 다 내려가 멈추면 연다
                _stageListPending = true;
                UINav.GoToMain();
                if (UIFrame.SheetOpen) _stageListPending = false;
                return;
            }
            FillStageList(count, battle.Stage, battle.ChallengeTarget);
            OpenStageList();
        }

        /// <summary>
        /// 챕터 지도 (CR-05): 시트 밑에서는 열지 않는다 — 시트를 내리고, 다 내려가 멈추면 연다 (목록과 같은 규칙. 지도는 '아직 다시 가 볼 스테이지가 없어요'
        /// 없이 지금 챕터를 연다 — ChapterMapPopup이 튜토리얼 중에는 스스로 열지 않는다)
        /// </summary>
        void OpenChapterMapFromPanel()
        {
            _stageListPending = false;
            if (UIFrame.SheetOpen)
            {
                _chapterMapPending = true;
                UINav.GoToMain();
                if (UIFrame.SheetOpen) _chapterMapPending = false;
                return;
            }
            _chapterMapPending = false;
            UINav.OpenChapterMap();
        }

        /// <summary>시트를 내리는 중인 챕터 지도: 다 내려가 멈추면 연다. 그 사이 시트가 다시 열리거나 팝업 · 튜토리얼이 오면 버린다</summary>
        void TickChapterMapPending()
        {
            if (UIFrame.SheetOpen || PopupOpen || TutorialActive || !Game.IsReady || Game.Battle == null)
            {
                _chapterMapPending = false;
                return;
            }
            if (!UIFrame.SheetSettled) return;
            _chapterMapPending = false;
            if (UINav.HasChapterMap) UINav.OpenChapterMap();
        }

        /// <summary>시트를 내리는 중인 스테이지 고르기: 다 내려가 멈추면 연다. 그 사이 시트가 다시 열리거나 팝업 · 튜토리얼이 오면 버린다</summary>
        void TickStageListPending()
        {
            if (UIFrame.SheetOpen || PopupOpen || TutorialActive || !Game.IsReady || Game.Battle == null)
            {
                _stageListPending = false;
                return;
            }
            if (!UIFrame.SheetSettled) return;
            _stageListPending = false;
            if (_stageListOpen) return;
            var battle = Game.Battle;
            int count = StageListStages(battle.HighestStage, _stageListStages);
            if (!HasStageChoice(_stageListStages, count, battle.Stage)) return;
            FillStageList(count, battle.Stage, battle.ChallengeTarget);
            OpenStageList();
        }

        /// <summary>
        /// 최근에 깬 스테이지 칩 (_stageListStages 앞 count개 — StageListStages가 채운다): 지금 스테이지는 금빛, 막힌 스테이지는 붉은 글자.
        /// 열 때만 부른다 (스테이지 이름 글자를 그때 만든다)
        /// </summary>
        void FillStageList(int count, int current, int blocked)
        {
            for (int i = 0; _stageChips != null && i < _stageChips.Length; i++)
            {
                var chip = _stageChips[i];
                if (chip == null) continue;
                bool used = i < count;
                chip.gameObject.SetActive(used);
                if (!used) continue;
                int stage = _stageListStages[i];
                bool isCurrent = stage == current;
                if (_stageChipLabels != null && i < _stageChipLabels.Length && _stageChipLabels[i] != null)
                {
                    var label = _stageChipLabels[i];
                    label.text = BattleSystem.FormatStage(stage);
                    label.color = isCurrent ? UITheme.Ink : stage == blocked ? UITheme.Negative : UITheme.Cream;
                }
                if (_stageChipBacks != null && i < _stageChipBacks.Length && _stageChipBacks[i] != null)
                {
                    _stageChipBacks[i].color = isCurrent ? UITheme.Gold : BattleEase.WithAlpha(UITheme.PanelLight, 0.95f);
                }
            }
        }

        /// <summary>
        /// 최근에 깬 스테이지 (§2.1.5 'the last 10 cleared'): highest(= BattleSystem.HighestStage — 아직 깨지 않은 앞 스테이지)는 빼고
        /// highest − 1부터 아래로 최대 buffer.Length개. 깬 스테이지가 없으면 0. 순수 계산 — 채운 수를 돌려준다
        /// </summary>
        internal static int StageListStages(int highest, int[] buffer)
        {
            if (buffer == null) return 0;
            int count = 0;
            for (int stage = highest - 1; stage >= 1 && count < buffer.Length; stage--) buffer[count++] = stage;
            return count;
        }

        /// <summary>지금 스테이지 말고 고를 칩이 하나라도 있는지 (없으면 '아직 다시 가 볼 스테이지가 없어요.')</summary>
        internal static bool HasStageChoice(int[] stages, int count, int current)
        {
            if (stages == null) return false;
            count = Mathf.Min(count, stages.Length);
            for (int i = 0; i < count; i++)
            {
                if (stages[i] != current) return true;
            }
            return false;
        }

        void OpenStageList()
        {
            if (_stageList == null) return;
            _stageListOpen = true;
            _stageList.SetActive(true);
            if (UIManager.Instance != null) UIManager.Instance.PushBackHandler(_backHandler);
        }

        void CloseStageList()
        {
            _stageListOpen = false;
            if (_stageList != null && _stageList.activeSelf) _stageList.SetActive(false);
            if (UIManager.Instance != null) UIManager.Instance.RemoveBackHandler(_backHandler);
        }

        bool OnBack()
        {
            if (!_stageListOpen) return false;
            CloseStageList();
            return true;
        }

        void OnStageChip(int index)
        {
            if (!Game.IsReady || Game.Battle == null || index < 0 || index >= _stageListStages.Length)
            {
                CloseStageList();
                return;
            }
            int stage = _stageListStages[index];
            CloseStageList();
            if (stage <= 0 || stage == Game.Battle.Stage) return;
            Game.Battle.GoToStage(stage);
        }
    }
}
