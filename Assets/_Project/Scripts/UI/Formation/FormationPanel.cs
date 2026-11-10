using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 편성 탭. 하나의 캐릭터 목록에서 카드를 끌어 방위 링(지킴이) 또는 잔향이 덱에 놓는다.
    /// 끌기가 어려우면 카드 탭 → 칸 탭으로도 배치할 수 있다. 배치된 칸을 탭하면 빠지고 안내 줄에 잠깐 "되돌리기"가 뜬다.
    /// 흐름: 목록 → 드래그 → 방위 링/덱 → 반대편 등록 확인 → 차단 안내 → 배치 확정 → 요격 범위 프리뷰 갱신.
    /// 오른쪽 시너지 칸(SynergyPanelView)은 켜진 시너지를 보여주고, 카드를 끌어 칸 위에 올리면
    /// 놓았을 때 켜지거나 강해지는 시너지를 미리 빛내 준다. 시너지 계산은 바뀐 프레임에 한 번만 한다.
    ///   · 전투력 (M46): 미니 전장 오른쪽 위 알약 '지킴이 3/5 · [아이콘] 1,226' (편성 · 전투력이 바뀔 때). 놓을 수 있는 칸 위에서는
    ///     고스트 아래 '전투력 +312' / '전투력 −80' (PowerSystem.PreviewPlace — 칸이 바뀔 때 한 번만 계산, 0이면 숨김, 탭 선택에는 없음).
    ///     제대로 놓은 뒤 전투력이 줄어도 상단 바에 '−N'이 잠깐 뜨게 한다 (PowerUpFloatView.AllowDecreaseOnce — 빼기 · 자동 편성 · 놓지 못한 때는 열지 않는다).
    ///   · 원정 중인 친구도 그대로 놓을 수 있다 (M05) — 목록 순서도 나누지 않는다.
    ///   · 안내 줄 (M80): '카드를 위로 끌어…'는 직접 배치 3번 전까지만, 끄는 중 · 고른 중 안내는 그대로.
    ///   · 잠긴 잔향이 칸은 '스테이지 2-9 클리어 시 / 열려요' (두 줄 — 칸 안에서 세 줄로 접힌다), 누르면 문장 토스트.
    ///   · 다른 화면이 친구 카드를 가리키면(PulseRosterCard) 패널이 켜질 때 그 카드를 보이게 굴리고 반짝인다.
    ///   · 패배 분석(CR-01)이 방위 칸을 가리키면(UINav.PulseFormationSlot → PulseGuardianSlot) 그 칸이 2초 금빛 테 + 1.06배로 반짝인다.
    ///   · 자동 편성으로 팀 전투력이 5% 넘게 오르면 자동 편성 버튼 뒤가 숨 쉬듯 빛난다 (P2 — 한 번 누르거나 세 번 그냥 지나가면 끈다).
    /// 전투는 Formation.Changed를 듣고 BattleSystem이 스스로 다시 시작한다.
    /// </summary>
    public class FormationPanel : MonoBehaviour
    {
        const float FanAlpha = 0.17f;
        const float FanFocusAlpha = 0.45f;
        const float UndoSeconds = 4f;
        /// <summary>직접 배치를 이만큼 하기 전까지만 '카드를 위로 끌어…' 안내를 보인다 (M80)</summary>
        const int TeachPlacements = 3;
        /// <summary>데려온 카드로 굴리는 시간</summary>
        const float PulseScroll = 0.3f;
        /// <summary>자동 편성 빛: 팀 전투력 대비 이만큼 오르면 · 그냥 지나간 방문 수 한도</summary>
        const float AutoPulseGainRatio = 0.05f;
        const int AutoPulseMaxVisits = 3;

        [Header("방위 링 / 잔향이 덱")]
        [SerializeField] FormationSlotView[] _guardianSlots;
        [SerializeField] FormationSlotView[] _echoSlots;
        [SerializeField] TMP_Text _guardianCountText;
        [SerializeField] TMP_Text _echoCountText;
        [Tooltip("미니 전장 알약의 팀 전투력 숫자 (아이콘 옆)")]
        [SerializeField] TMP_Text _teamPowerText;

        [Header("팀 시너지")]
        [SerializeField] SynergyPanelView _synergyPanel;
        [SerializeField] Button _autoButton;
        [Tooltip("자동 편성 버튼 뒤 숨쉬는 빛 (자동 편성 이득이 클 때만, 꺼 둠)")]
        [SerializeField] GameObject _autoPulse;

        [Header("캐릭터 목록")]
        [SerializeField] ScrollRect _cardScroll;
        [SerializeField] RectTransform _cardContent;
        [SerializeField] FormationCardView _cardPrefab;
        [SerializeField] TMP_Text _listTitleText;
        [SerializeField] TMP_Text _hintText;
        [SerializeField] GameObject _emptyLabel;

        [Header("드래그 / 안내")]
        [Tooltip("드래그 고스트가 움직이는 영역 (패널 루트, 피벗 가운데)")]
        [SerializeField] RectTransform _dragLayer;
        [SerializeField] RectTransform _dragGhost;
        [SerializeField] FormPortraitView _dragGhostPortrait;
        [Tooltip("드래그 고스트 위 \"시너지 +1\" 표시 (놓으면 켜지거나 강해지는 시너지)")]
        [SerializeField] Image _ghostSynergyTag;
        [SerializeField] TMP_Text _ghostSynergyText;
        [Tooltip("시너지 표시 8px 아래 \"전투력 +312\" (놓을 수 있는 칸 위에서만)")]
        [SerializeField] Image _ghostPowerTag;
        [SerializeField] TMP_Text _ghostPowerText;
        [SerializeField] FormationCallout _callout;
        [SerializeField] Button _undoButton;
        [SerializeField] TMP_Text _undoText;

        // ───────────── 다른 화면이 부르는 계약 (Wave 2 C-02) ─────────────

        static string s_pulseLineage;
        static float s_pulseSeconds;
        static FormationPanel s_active;
        /// <summary>자동 편성 빛을 이번 실행에서 그만 보일지 (한 번 눌렀다) · 빛을 보인 채 그냥 지나간 방문 수</summary>
        static bool s_autoPulseDone;
        static int s_autoPulseVisits;
        static string s_echoLockLabel;

        /// <summary>
        /// 편성 카드 목록에서 그 계보 카드를 잠깐 반짝인다 (소환 결과 '편성하러 가기 ›' → 편성 시트, M16 — 보통 UINav.GoToTab(MainTab.Formation) 바로 뒤에 부른다).
        /// 패널이 꺼져 있으면(MAIN · 다른 시트) 미뤄 두었다가 켜질 때 카드를 보이게 굴리고 seconds 동안 1 → 1.06배 + 금빛 테로 반짝인다.
        /// 그 계보 카드가 없으면(아직 못 만남) 조용히 지나간다.
        /// </summary>
        public static void PulseRosterCard(string lineageId, float seconds = 3f)
        {
            if (string.IsNullOrEmpty(lineageId)) return;
            s_pulseLineage = lineageId;
            s_pulseSeconds = Mathf.Max(0.5f, seconds);
            var panel = s_active;
            if (panel != null && panel._started && panel.isActiveAndEnabled) panel.StartPulse();
        }

        // ───────────── 칸 반짝 (CR-01 받는 쪽, SPEC4 §7.2) ─────────────

        /// <summary>칸 반짝 기본 시간 · 받는 시간 범위 (초)</summary>
        internal const float SlotPulseDefaultSeconds = 2f;
        internal const float SlotPulseMinSeconds = 0.5f;
        internal const float SlotPulseMaxSeconds = 6f;
        /// <summary>요청 뒤 이만큼 지나도록 패널이 켜지지 않으면(편성 탭이 잠겨 열리지 않음 등) 그 반짝은 버린다 — 한참 뒤 엉뚱하게 반짝이지 않게</summary>
        internal const float SlotPulseStaleSeconds = 3f;

        /// <summary>미뤄 둔 칸 반짝: 방위 칸 번호 (−1 = 없음) · 초 · 요청 시각 (unscaled)</summary>
        static int s_slotPulse = -1;
        static float s_slotPulseSeconds;
        static float s_slotPulseAt;

        /// <summary>
        /// 방위 링 칸 하나를 seconds 동안 반짝인다 (CR-01 패배 분석 '편성 바꾸기 ›' → UINav.GoToTab(Formation) + UINav.PulseFormationSlot(칸, 2) —
        /// 이 패널이 UINav.FormationSlotPulseRequested를 받아 여기로 온다). 칸 번호 = 방위 슬롯 (0 = 위, 시계 반대 방향 — EnemyFxCatalog.ShopStrikeSlot).
        /// 패널이 아직 시작 전이면(시트가 막 열림) 미뤄 두었다가 켜질 때 금빛 테 + 1 → 1.06배로 반짝인다. 범위 밖 칸은 조용히 무시한다
        /// </summary>
        public static void PulseGuardianSlot(int slot, float seconds = SlotPulseDefaultSeconds)
        {
            if (!IsPulseSlot(slot)) return;
            s_slotPulse = slot;
            s_slotPulseSeconds = ClampSlotPulseSeconds(seconds);
            s_slotPulseAt = Time.unscaledTime;
            var panel = s_active;
            if (panel != null && panel._started && panel.isActiveAndEnabled) panel.PlayPendingSlotPulse();
        }

        /// <summary>반짝일 수 있는 방위 칸 번호인지 (0 ~ SlotCount − 1)</summary>
        internal static bool IsPulseSlot(int slot) => slot >= 0 && slot < BattleGeometry.SlotCount;

        /// <summary>반짝 시간 (NaN · 0 이하 → 기본 2초, 0.5 ~ 6초로 자른다)</summary>
        internal static float ClampSlotPulseSeconds(float seconds) =>
            float.IsNaN(seconds) || seconds <= 0f ? SlotPulseDefaultSeconds : Mathf.Clamp(seconds, SlotPulseMinSeconds, SlotPulseMaxSeconds);

        readonly List<CharacterFormDef> _forms = new();
        UIListPool<FormationCardView> _cards;
        bool _started;
        /// <summary>시너지를 다시 계산해야 함 (편성/친구/시너지 변경 신호를 한 프레임에 모아 LateUpdate에서 한 번)</summary>
        bool _synergyDirty;
        /// <summary>
        /// 목록/칸을 다시 그려야 함 (친구 변경 신호를 모아 LateUpdate에서 한 번).
        /// 스테이지 클리어 때 호감도가 편성 계보마다 따로 올라 Characters.Changed가 한 프레임에 여러 번 오기 때문.
        /// 편성 변경은 배치 연출(PlayPlaced)이 바로 새 칸을 봐야 하므로 그대로 즉시 다시 그린다.
        /// </summary>
        bool _refreshDirty;
        /// <summary>시너지 미리보기 중인 칸 (null = 미리보기 없음)</summary>
        FormationSlotView _previewSlot;
        /// <summary>마지막으로 그린 잔향이 덱 개방 칸 수 (스테이지 도달로 바뀌면 다시 그린다)</summary>
        int _shownEchoSlots = -1;
        long _shownTeamPower = -1;

        string _selectedForm;
        /// <summary>요격 범위를 진하게 보여줄 방위 슬롯 (-1 = 모두 옅게)</summary>
        int _focusSlot = -1;

        string _dragForm;
        FormationSlotView _hoverSlot;
        Coroutine _pulseRoutine;

        CharacterRole _undoRole;
        int _undoSlot;
        string _undoForm;
        float _undoTimer;
        /// <summary>UINav.FormationSlotPulseRequested를 듣는 중 (처음 켜질 때 한 번 잇고 OnDestroy에서 끊는다 — 꺼져 있는 동안에도 받아 미뤄 둔다)</summary>
        bool _slotPulseHooked;

        void Start()
        {
            _cards = new UIListPool<FormationCardView>(_cardPrefab, _cardContent);
            foreach (var slot in _guardianSlots) slot.Init(this);
            foreach (var slot in _echoSlots) slot.Init(this);
            _autoButton.onClick.AddListener(AutoFormation);
            _undoButton.onClick.AddListener(Undo);
            _undoButton.gameObject.SetActive(false);
            _dragGhost.gameObject.SetActive(false);
            if (_ghostPowerTag != null) _ghostPowerTag.gameObject.SetActive(false);

            Game.Formation.Changed += Refresh;
            Game.Characters.Changed += MarkRefreshDirty;
            Game.Expedition.Changed += Refresh;
            Game.Synergy.Changed += MarkSynergyDirty;
            Game.Battle.StageChanged += OnStageChanged;
            if (Game.Power != null) Game.Power.Changed += OnPowerChanged;
            _started = true;
            Refresh();
            RefreshAutoPulse(true);
            if (s_pulseLineage != null) StartPulse();
            if (s_slotPulse >= 0) PlayPendingSlotPulse();
        }

        void OnEnable()
        {
            s_active = this;
            if (!_slotPulseHooked)
            {
                _slotPulseHooked = true;
                UINav.FormationSlotPulseRequested += OnSlotPulseRequested;
            }
            if (!_started) return;
            Refresh();
            RefreshAutoPulse(true);
            if (s_pulseLineage != null) StartPulse();
            if (s_slotPulse >= 0) PlayPendingSlotPulse();
        }

        void OnDisable()
        {
            if (s_active == this) s_active = null;
            CancelCardDrag();
            _selectedForm = null;
            HideUndo();
            _pulseRoutine = null;
        }

        void OnDestroy()
        {
            if (s_active == this) s_active = null;
            if (_slotPulseHooked)
            {
                _slotPulseHooked = false;
                UINav.FormationSlotPulseRequested -= OnSlotPulseRequested;
            }
            if (!_started || !Game.IsReady) return;
            Game.Formation.Changed -= Refresh;
            Game.Characters.Changed -= MarkRefreshDirty;
            Game.Expedition.Changed -= Refresh;
            Game.Synergy.Changed -= MarkSynergyDirty;
            Game.Battle.StageChanged -= OnStageChanged;
            if (Game.Power != null) Game.Power.Changed -= OnPowerChanged;
        }

        void Update()
        {
            if (_undoTimer <= 0f) return;
            _undoTimer -= Time.unscaledDeltaTime;
            if (_undoTimer <= 0f) HideUndo();
        }

        void LateUpdate()
        {
            if (_refreshDirty) Refresh();
            if (_synergyDirty) RefreshSynergy();
        }

        // ───────────── 갱신 ─────────────

        /// <summary>
        /// 편성 탭을 보는 중에도 위쪽 전투는 계속 진행된다. 최고 스테이지가 올라 잠긴 덱 칸이 열리면 바로 다시 그린다.
        /// (최고 스테이지는 클리어 보상·호감도 이벤트 뒤에 갱신되므로 다음 전투 시작 신호를 듣는다. 매 스테이지마다 불려서 칸 수가 바뀔 때만)
        /// 스테이지 기믹(비/정전 등)이 바뀌면 시너지 칸의 "×1.5" 표시도 바뀌므로 시너지만 다시 그린다.
        /// </summary>
        void OnStageChanged()
        {
            if (!_started || !isActiveAndEnabled) return;
            if (Game.Formation.UnlockedEchoSlots != _shownEchoSlots) Refresh();
            else _synergyDirty = true;
        }

        /// <summary>팀 전투력이 바뀜 (레벨업 · 편성 · 의상 등): 알약 숫자와 카드 전투력만 (값이 바뀐 것만 다시 쓴다)</summary>
        void OnPowerChanged(long before, long after)
        {
            if (!_started || !isActiveAndEnabled) return;
            RefreshTeamPower();
            for (int i = 0; i < _cards.Count; i++) _cards[i].RefreshPower();
        }

        void MarkRefreshDirty() => _refreshDirty = true;

        void Refresh()
        {
            // 꺼져 있으면 OnEnable에서 다시 그리므로 모아 둔 신호는 버린다
            _refreshDirty = false;
            if (!isActiveAndEnabled || !_started) return;
            RefreshSlots();
            RefreshCards();
            _synergyDirty = true;
            RefreshFans();
            ApplyHighlights();
            RefreshHint();
            RefreshTeamPower();
            RefreshAutoPulse(false);
        }

        void RefreshSlots()
        {
            var formation = Game.Formation;
            int guardians = 0;
            for (int i = 0; i < _guardianSlots.Length; i++)
            {
                var formId = formation.GetGuardian(i);
                _guardianSlots[i].Show(formId, false, null);
                if (!string.IsNullOrEmpty(formId)) guardians++;
            }

            int unlocked = formation.UnlockedEchoSlots;
            _shownEchoSlots = unlocked;
            int echoes = 0;
            for (int i = 0; i < _echoSlots.Length; i++)
            {
                bool locked = i >= unlocked;
                var formId = locked ? null : formation.GetEcho(i);
                _echoSlots[i].Show(formId, locked, locked ? EchoLockLabel(formation) : null);
                if (!string.IsNullOrEmpty(formId)) echoes++;
            }

            _guardianCountText.text = $"지킴이 <color={UITheme.ColorTag(UITheme.Guardian)}>{guardians}</color>/{_guardianSlots.Length}";
            _echoCountText.text = $"잔향이 <color={UITheme.ColorTag(UITheme.EchoGlow)}>{echoes}</color>/{unlocked}";
        }

        /// <summary>잠긴 잔향이 칸 글: '스테이지 2-9 클리어 시 / 열려요' — '2-9'는 줄이 갈리지 않게 &lt;nobr&gt; (M28). 한 번 만들어 둔다</summary>
        static string EchoLockLabel(FormationSystem formation)
        {
            if (s_echoLockLabel != null) return s_echoLockLabel;
            string hint = formation.EchoSlotUnlockHint;
            string code = FormationSystem.EchoSlotUnlockCode;
            s_echoLockLabel = string.IsNullOrEmpty(hint) || string.IsNullOrEmpty(code) ? hint : hint.Replace(code, UIFormat.Keep(code));
            return s_echoLockLabel;
        }

        /// <summary>잠긴 칸을 누르거나 놓았을 때 토스트 (한 줄 문장 — 칸 글은 두 줄이라 토스트에 쓰지 않는다)</summary>
        static string LockToast(FormationSlotView slot) =>
            slot.Role == CharacterRole.Echo ? Game.Formation.EchoSlotUnlockSentence : "아직 열리지 않은 칸이에요.";

        /// <summary>알약 '[아이콘] 1,226' — 값이 바뀔 때만 다시 쓴다</summary>
        void RefreshTeamPower()
        {
            if (_teamPowerText == null || Game.Power == null) return;
            long power = Game.Power.TeamPower;
            if (power == _shownTeamPower) return;
            _shownTeamPower = power;
            _teamPowerText.text = PowerSystem.Format(power);
        }

        void RefreshCards()
        {
            // 원정 중인 친구도 함께 싸우고 놓을 수 있어 목록을 나누지 않는다 (M05 — 해금 순서 그대로)
            _forms.Clear();
            _forms.AddRange(Game.Characters.GetUnlockedForms());

            if (_selectedForm != null && !_forms.Exists(f => f.id == _selectedForm)) _selectedForm = null;

            _cards.SetCount(_forms.Count);
            for (int i = 0; i < _forms.Count; i++)
            {
                var card = _cards[i];
                card.Init(this, _cardScroll);
                card.Set(_forms[i], Game.Characters.Db.GetLineageOfForm(_forms[i].id));
                card.SetSelected(_forms[i].id == _selectedForm);
            }

            _listTitleText.text = $"친구 목록 <size=80%><color={UITheme.ColorTag(UITheme.SubText)}>{_forms.Count}</color></size>";
            _emptyLabel.SetActive(_forms.Count == 0);
        }

        void MarkSynergyDirty() => _synergyDirty = true;

        /// <summary>시너지 칸 + 카드 특성 칩. 미리보기 중이면 새 편성 기준으로 미리보기도 다시 계산</summary>
        void RefreshSynergy()
        {
            _synergyDirty = false;
            if (!_started) return;
            _synergyPanel.Recalculate();
            int traits = _synergyPanel.ActiveTraitMask;
            for (int i = 0; i < _cards.Count; i++) _cards[i].SetActiveTraits(traits);
            ApplySynergyFocus();
        }

        /// <summary>
        /// 드래그 중 놓을 수 있는 칸 위 → 놓았을 때의 시너지 미리보기,
        /// 그 밖에 끌거나 고른 친구가 있으면 → 그 친구가 들어가는 시너지 강조, 아니면 평소 모습.
        /// </summary>
        void ApplySynergyFocus()
        {
            if (!_started || !Game.IsReady) return;
            if (_dragForm != null && _previewSlot != null) _synergyPanel.ShowPreview(_previewSlot.Role, _previewSlot.Index, _dragForm);
            else _synergyPanel.ShowFocus(_dragForm ?? _selectedForm);
            RefreshGhostSynergyTag();
        }

        /// <summary>손가락 바로 위 고스트에도 "시너지 +1 / -1"을 띄워 시선을 옮기지 않아도 알 수 있게</summary>
        void RefreshGhostSynergyTag()
        {
            if (_ghostSynergyTag == null) return;
            int gains = _synergyPanel.PreviewGains;
            int losses = _synergyPanel.PreviewLosses;
            bool show = _dragForm != null && (gains > 0 || losses > 0);
            bool wasShown = _ghostSynergyTag.gameObject.activeSelf;
            _ghostSynergyTag.gameObject.SetActive(show);
            if (!show) return;
            _ghostSynergyTag.color = gains > 0 ? UITheme.Gold : UITheme.Negative;
            _ghostSynergyText.color = gains > 0 ? UITheme.Ink : UITheme.Text;
            _ghostSynergyText.text = gains > 0 ? $"시너지 +{gains}" : $"시너지 -{losses}";
            if (!wasShown) StartCoroutine(MenuFx.PopIn(_ghostSynergyTag.transform, 0.2f));
        }

        /// <summary>
        /// 고스트 아래 '전투력 +312' / '전투력 −80' (M46): 놓을 수 있는 칸 위에서만, 칸이 바뀔 때 한 번 계산 (PreviewPlace — 시너지까지 정확).
        /// 바뀌지 않으면(0) 숨긴다. 탭으로 고를 때는 보이지 않는다.
        /// </summary>
        void RefreshGhostPower(FormationSlotView slot, bool canPlace)
        {
            if (_ghostPowerTag == null) return;
            long delta = 0;
            if (canPlace && slot != null && _dragForm != null && Game.Power != null)
            {
                long team = Game.Power.TeamPower;
                delta = Game.Power.PreviewPlace(slot.Role, slot.Index, _dragForm) - team;
            }
            bool show = delta != 0;
            bool wasShown = _ghostPowerTag.gameObject.activeSelf;
            _ghostPowerTag.gameObject.SetActive(show);
            if (!show) return;
            bool up = delta > 0;
            // 감소는 U+2212 '−' (놓은 뒤 뜨는 PowerUpFloatView의 '−N'과 같은 부호)
            _ghostPowerText.text = up ? $"전투력 +{PowerSystem.Format(delta)}" : $"전투력 −{PowerSystem.Format(-delta)}";
            _ghostPowerText.color = up ? UITheme.Positive : UITheme.Negative;
            if (!wasShown) StartCoroutine(MenuFx.PopIn(_ghostPowerTag.transform, 0.2f));
        }

        /// <param name="sweepSlot">펼쳐지는 연출을 줄 방위 슬롯 (-1 없음)</param>
        void RefreshFans(int sweepSlot = -1, bool sweepAll = false)
        {
            for (int i = 0; i < _guardianSlots.Length; i++)
            {
                var slot = _guardianSlots[i];
                // 놓을 수 있는 칸 위에 있을 때만 끌고 있는 친구의 범위를 미리 보여준다
                bool preview = _dragForm != null && _hoverSlot == slot
                    && Game.Formation.CanPlace(slot.Role, slot.Index, _dragForm) == PlaceResult.Ok;
                var formId = preview ? _dragForm : slot.FormId;
                if (string.IsNullOrEmpty(formId))
                {
                    slot.HideFan();
                    continue;
                }
                var stats = Game.Characters.GetStats(formId);
                float alpha = preview || i == _focusSlot ? FanFocusAlpha : FanAlpha;
                slot.ShowFan(stats.arcDegrees, stats.range, alpha, sweepAll || i == sweepSlot);
            }
        }

        void ApplyHighlights()
        {
            var formId = _dragForm ?? _selectedForm;
            foreach (var slot in AllSlots()) slot.SetHighlight(HighlightFor(slot, formId));
        }

        FormationSlotHighlight HighlightFor(FormationSlotView slot, string formId)
        {
            if (string.IsNullOrEmpty(formId) || slot.IsLocked) return FormationSlotHighlight.None;
            bool ok = Game.Formation.CanPlace(slot.Role, slot.Index, formId) == PlaceResult.Ok;
            if (slot == _hoverSlot) return ok ? FormationSlotHighlight.Hover : FormationSlotHighlight.Invalid;
            return ok ? FormationSlotHighlight.Valid : FormationSlotHighlight.None;
        }

        void RefreshHint()
        {
            string gold = UITheme.ColorTag(UITheme.Gold);
            if (_dragForm != null)
            {
                _hintText.text = $"<color={gold}>방위 링이나 잔향이 덱의 빛나는 칸 위에서 놓아 주세요</color>";
            }
            else if (_selectedForm != null)
            {
                var name = Game.Characters.GetDisplayName(_selectedForm);
                _hintText.text = $"<color={gold}>{MenuText.Object(name)} 놓을 칸을 눌러 주세요</color>";
            }
            else
            {
                // 끄는 법을 배운 뒤(직접 배치 3번)에는 안내 줄을 비운다 (M80 — 패널마다 안내는 한 줄, 몸에 익으면 사라진다)
                _hintText.text = Game.Formation.PlacementsDone < TeachPlacements
                    ? "카드를 위로 끌어 방위 링(지킴이)이나 잔향이 덱에 놓아 주세요"
                    : string.Empty;
            }
        }

        /// <summary>
        /// 자동 편성 빛 (M16 P2): 자동 편성이 팀 전투력을 5% 넘게 올릴 때만 버튼 뒤가 숨 쉬듯 빛난다.
        /// 한 번 누르면 이번 실행에서는 그만, 빛을 보인 채 세 번 그냥 지나가도 그만. visit = 패널이 켜졌다 (한 번 센다).
        /// </summary>
        void RefreshAutoPulse(bool visit)
        {
            if (_autoPulse == null) return;
            bool show = false;
            if (!s_autoPulseDone && s_autoPulseVisits < AutoPulseMaxVisits && Game.Power != null)
            {
                long team = Game.Power.TeamPower;
                // FR-1: PowerSystem.PreviewAutoFormationGain (A-1, P4-1) — 본문이 채워지기 전에는 0이라 빛은 꺼져 있다
                long gain = Game.Power.PreviewAutoFormationGain();
                show = gain > 0 && gain >= team * AutoPulseGainRatio;
            }
            if (show && visit) s_autoPulseVisits++;
            if (_autoPulse.activeSelf != show) _autoPulse.SetActive(show);
        }

        // ───────────── 탭 ─────────────

        public void OnCardClicked(FormationCardView card)
        {
            if (_dragForm != null || string.IsNullOrEmpty(card.FormId)) return;
            // 원정 중인 친구도 고를 수 있다 (M05 — 원정 중에도 편성에서 함께 싸운다)
            _selectedForm = _selectedForm == card.FormId ? null : card.FormId;
            if (_selectedForm != null && Game.Formation.IsPlaced(_selectedForm, out var role, out int slot) && role == CharacterRole.Guardian)
            {
                _focusSlot = slot;
            }
            ApplySelection();
        }

        public void OnSlotClicked(FormationSlotView slot)
        {
            if (_dragForm != null) return;
            if (slot.IsLocked)
            {
                UIManager.Instance.ShowToast(LockToast(slot));
                return;
            }
            if (_selectedForm != null)
            {
                Place(slot.Role, slot.Index, _selectedForm);
                return;
            }
            if (!slot.IsEmpty)
            {
                RemoveWithUndo(slot);
                return;
            }
            UIManager.Instance.ShowToast("아래 목록에서 친구를 골라 이 칸에 놓아 주세요.");
        }

        void ApplySelection()
        {
            for (int i = 0; i < _cards.Count; i++) _cards[i].SetSelected(_cards[i].FormId == _selectedForm);
            ApplyHighlights();
            RefreshFans();
            RefreshHint();
            ApplySynergyFocus();
        }

        void AutoFormation()
        {
            _selectedForm = null;
            _focusSlot = -1;
            HideUndo();
            s_autoPulseDone = true;
            if (_autoPulse != null) _autoPulse.SetActive(false);
            Game.Formation.AutoFormation();
            RefreshFans(sweepAll: true);
            UIManager.Instance.ShowToast("규칙에 맞춰 전투력 순으로 자동 편성했어요.");
        }

        // ───────────── 배치 / 해제 ─────────────

        bool Place(CharacterRole role, int index, string formId)
        {
            var occupant = role == CharacterRole.Guardian ? Game.Formation.GetGuardian(index) : Game.Formation.GetEcho(index);
            bool swap = !string.IsNullOrEmpty(occupant) && occupant != formId;
            var result = Game.Formation.TryPlace(role, index, formId);
            if (result == PlaceResult.Ok)
            {
                // 놓은 뒤 전투력이 줄어도 상단 바에 '−N'이 잠깐 보이게 (M46 — 제대로 놓인 때만. 전투력 Changed는 다음 MetaSession.Tick에 오므로 놓은 뒤에 열어도 된다)
                PowerUpFloatView.AllowDecreaseOnce();
                _selectedForm = null;
                if (role == CharacterRole.Guardian) _focusSlot = index;
                HideUndo();
                UISfx.Play(swap ? UISfxKey.DragSwap : UISfxKey.DragDrop);
                var placed = SlotOf(role, index);
                if (placed != null) placed.PlayPlaced();
                ApplySelection();
                RefreshFans(role == CharacterRole.Guardian ? index : -1);
                return true;
            }

            if (result == PlaceResult.OnOtherSide) ShowBlockedCallout(formId, result);
            else UIManager.Instance.ShowToast(FormationSystem.Describe(result));
            return false;
        }

        /// <summary>"동시 등록 차단" 안내: 이미 반대편에 등록된 친구</summary>
        void ShowBlockedCallout(string formId, PlaceResult result)
        {
            var name = Game.Characters.GetDisplayName(formId);
            string body = FormationSystem.Describe(result);
            if (Game.Formation.IsPlaced(formId, out var otherRole, out _))
            {
                body += $"\n<size=85%>{MenuText.Topic(name)} 이미 {MenuText.Toward(UITheme.RoleName(otherRole))} 등록돼 있어요.</size>";
            }
            _callout.Show(formId, "동시 등록 차단", body);
        }

        void RemoveWithUndo(FormationSlotView slot)
        {
            var formId = slot.FormId;
            var name = Game.Characters.GetDisplayName(formId);
            _undoRole = slot.Role;
            _undoSlot = slot.Index;
            _undoForm = formId;
            if (slot.Role == CharacterRole.Guardian && _focusSlot == slot.Index) _focusSlot = -1;

            // 빼기는 '−N'을 띄우지 않는다 (M46 — 감소 숫자는 놓은 직후만). 빠진 것은 토스트와 되돌리기 버튼이 알린다
            Game.Formation.Remove(slot.Role, slot.Index);

            _undoText.text = $"{name} 되돌리기";
            _undoButton.gameObject.SetActive(true);
            _hintText.gameObject.SetActive(false);
            _undoTimer = UndoSeconds;
            StartCoroutine(MenuFx.PopIn(_undoButton.transform, 0.2f));
            UIManager.Instance.ShowToast($"{MenuText.Topic(name)} {UITheme.RoleName(slot.Role)} 자리에서 빠졌어요.");
        }

        void Undo()
        {
            var formId = _undoForm;
            HideUndo();
            if (!string.IsNullOrEmpty(formId)) Place(_undoRole, _undoSlot, formId);
        }

        void HideUndo()
        {
            _undoTimer = 0f;
            _undoForm = null;
            if (_undoButton != null) _undoButton.gameObject.SetActive(false);
            if (_hintText != null) _hintText.gameObject.SetActive(true);
        }

        // ───────────── 드래그 (FormationCardView가 호출) ─────────────

        public bool BeginCardDrag(FormationCardView card, PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(card.FormId)) return false;

            _dragForm = card.FormId;
            _selectedForm = null;
            _hoverSlot = null;
            _previewSlot = null;
            for (int i = 0; i < _cards.Count; i++) _cards[i].SetSelected(false);

            _dragGhostPortrait.SetForm(_dragForm);
            _dragGhost.gameObject.SetActive(true);
            _dragGhost.SetAsLastSibling();
            MoveGhost(eventData);
            StartCoroutine(MenuFx.PopIn(_dragGhost, 0.18f));
            RefreshGhostPower(null, false);
            UISfx.Play(UISfxKey.DragPick);

            ApplyHighlights();
            RefreshHint();
            ApplySynergyFocus();
            return true;
        }

        public void DragCard(PointerEventData eventData)
        {
            if (_dragForm == null) return;
            MoveGhost(eventData);

            var slot = SlotUnder(eventData);
            if (slot == _hoverSlot) return;
            _hoverSlot = slot;
            ApplyHighlights();
            // 놓을 수 있는 방위 링 칸 위에 올리면 그 방향 요격 범위를 미리 펼쳐 보여준다
            bool canPlace = slot != null && !slot.IsLocked && Game.Formation.CanPlace(slot.Role, slot.Index, _dragForm) == PlaceResult.Ok;
            RefreshFans(canPlace && slot.Role == CharacterRole.Guardian ? slot.Index : -1);
            // 놓을 수 있는 칸(방위 링/덱) 위면 놓았을 때 켜지거나 강해지는 시너지를 미리 빛낸다
            _previewSlot = canPlace ? slot : null;
            ApplySynergyFocus();
            RefreshGhostPower(slot, canPlace);
        }

        public void EndCardDrag(PointerEventData eventData)
        {
            if (_dragForm == null) return;
            var slot = SlotUnder(eventData);
            var formId = _dragForm;
            ClearDrag();

            if (slot == null) return;
            if (slot.IsLocked)
            {
                UIManager.Instance.ShowToast(LockToast(slot));
                return;
            }
            Place(slot.Role, slot.Index, formId);
        }

        public void CancelCardDrag()
        {
            if (_dragForm == null) return;
            ClearDrag();
        }

        void ClearDrag()
        {
            _dragForm = null;
            _hoverSlot = null;
            _previewSlot = null;
            _dragGhost.gameObject.SetActive(false);
            if (_ghostPowerTag != null) _ghostPowerTag.gameObject.SetActive(false);
            ApplyHighlights();
            RefreshFans();
            RefreshHint();
            ApplySynergyFocus();
        }

        void MoveGhost(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, eventData.position, eventData.pressEventCamera, out var local))
            {
                // 손가락에 가리지 않게 살짝 위에
                _dragGhost.anchoredPosition = local + new Vector2(0f, 56f);
            }
        }

        static FormationSlotView SlotUnder(PointerEventData eventData)
        {
            var target = eventData.pointerCurrentRaycast.gameObject;
            return target != null ? target.GetComponentInParent<FormationSlotView>() : null;
        }

        // ───────────── 데려오기 (C-02) ─────────────

        void StartPulse() => MenuFx.Restart(this, ref _pulseRoutine, PulseRoutine());

        IEnumerator PulseRoutine()
        {
            // 시트가 막 열렸으면 목록 레이아웃이 자리를 잡을 때까지 한 프레임
            yield return null;
            Canvas.ForceUpdateCanvases();
            string lineageId = s_pulseLineage;
            float seconds = s_pulseSeconds;
            s_pulseLineage = null;
            FormationCardView card = null;
            for (int i = 0; i < _cards.Count && card == null; i++)
            {
                if (_cards[i].LineageId == lineageId) card = _cards[i];
            }
            if (card == null || _cardScroll == null)
            {
                _pulseRoutine = null;
                yield break;
            }

            float to = CenteredPosition((RectTransform)card.transform);
            float from = _cardScroll.horizontalNormalizedPosition;
            if (!UIMotion.Reduced)
            {
                for (float t = 0f; t < PulseScroll; t += Time.unscaledDeltaTime)
                {
                    _cardScroll.horizontalNormalizedPosition = Mathf.Lerp(from, to, UIMotion.OutCubic(t / PulseScroll));
                    yield return null;
                }
            }
            _cardScroll.horizontalNormalizedPosition = to;
            _cardScroll.velocity = Vector2.zero;
            card.Pulse(seconds);
            _pulseRoutine = null;
        }

        /// <summary>UINav.FormationSlotPulseRequested (CR-01) → 칸 반짝 (패널이 꺼져 있으면 미뤄 둔다)</summary>
        void OnSlotPulseRequested(int slot, float seconds) => PulseGuardianSlot(slot, seconds);

        /// <summary>미뤄 둔 칸 반짝을 그 방위 칸에 튼다 (한 번 쓰면 비운다, 오래된 요청은 버린다)</summary>
        void PlayPendingSlotPulse()
        {
            int slot = s_slotPulse;
            s_slotPulse = -1;
            if (slot < 0 || Time.unscaledTime - s_slotPulseAt > SlotPulseStaleSeconds || _guardianSlots == null) return;
            foreach (var view in _guardianSlots)
            {
                if (view == null || view.Index != slot) continue;
                view.PulseSlot(s_slotPulseSeconds);
                return;
            }
        }

        /// <summary>카드가 보이는 칸 가운데에 오는 가로 스크롤 위치 (0~1)</summary>
        float CenteredPosition(RectTransform card)
        {
            float viewport = _cardScroll.viewport.rect.width;
            float scrollable = _cardContent.rect.width - viewport;
            if (scrollable <= 0f) return 0f;
            float center = card.localPosition.x + (0.5f - card.pivot.x) * card.rect.width;
            return Mathf.Clamp01((center - viewport * 0.5f) / scrollable);
        }

        // ───────────── 도우미 ─────────────

        IEnumerable<FormationSlotView> AllSlots()
        {
            foreach (var slot in _guardianSlots) yield return slot;
            foreach (var slot in _echoSlots) yield return slot;
        }

        FormationSlotView SlotOf(CharacterRole role, int index)
        {
            var slots = role == CharacterRole.Guardian ? _guardianSlots : _echoSlots;
            return index >= 0 && index < slots.Length ? slots[index] : null;
        }
    }
}
