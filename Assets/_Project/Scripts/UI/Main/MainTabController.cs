using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 하단 탭 바 (UX-SPEC-IA §2 · §5.3): 강화 · 편성 · 친구들 | 사냥 | 원정 · 소환 · 상점. 옆 탭 여섯(순서 = MainTab)은 시트를 열고,
    /// 가운데 사냥(Tab_Main)은 MAIN(전투를 크게, 시트 없음)으로 돌아간다. MAIN = SelectedIndex −1 (목표 상태 — 닫히는 연출 중에도 −1).
    /// 실행하면 언제나 MAIN (시작에 Selected를 부르지 않는다). 같은 SafeArea/TabBar의 SheetController가 시트를 움직인다.
    /// <para>
    /// 고르기: Select(i) (코드 · UINav.GoToTab) = 잠금 확인(SelectGuard) → 패널 바꾸기 → 시트 열기. 절대 닫지 않는다.
    /// 손으로 누름: 열린 탭을 다시 누르면 Reselected 뒤 닫기 (다시 누름은 닫기 하나뿐 — 맨 위로 스크롤은 없다, §4.3),
    /// 다른 탭이면 고르고 돌아가기 칸을 비운다. 사냥: 시트가 열려 있으면 닫기, MAIN이면 누름 표시만.
    /// MAIN으로 가는 모든 길(사냥 · 다시 누름 · 손잡이 · 끌어 내림 · 뒤로 · 자동 복귀 · UINav.GoToMain)은 ShowMain을 지나 돌아가기 칸을 비운다 (CR-05).
    /// 닫히는 동안 다시 고르면 지금 p에서 다시 연다. 패널은 닫기가 끝날 때(SheetController.Closed) 꺼진다.
    /// </para>
    /// <para>
    /// 선택 표시 (M52 · §2.3): 고른 옆 탭 = 받침(SelectedPlate) + Cream 글자(어두운 외곽선 머티리얼 그대로 — 보라 받침 위 금 글자는 2.3:1이라 읽히지 않았다,
    /// 금색은 막대 · 빛 · 사냥 칸에만) + 아이콘 1.12배 · 6px 위로 (0.20초 OutBack, 풀림 0.12초 OutCubic),
    /// 탭 바 위 끝 막대(Indicator 104x6)는 열린 탭 가운데로 0.18초 OutCubic. MAIN에서는 막대가 0.12초에 사라지고(자리는 그대로),
    /// 사냥 칸이 MAIN 모양(빛 α1 · 실 고리 0.55 · 아이콘 1 · 이름 Gold)이 된다 — 시트가 열리면 빛 0 · 고리 0.3 · 아이콘 0.8 · 이름 SubText (0.12초 OutCubic).
    /// 시트가 열린 채 탭을 바꾸면 새 패널만 알파 0 → 1 (0.12초, 누름은 그대로 받는다). 줄이기(UIMotion.Reduced)면 연출 없이 바로.
    /// </para>
    /// <para>
    /// 소리 · 전장 집중 (AUDIO-SPEC §8.1): MAIN(닫힘 · 닫히는 중)에서 시트가 열리면 SheetOpen + AudioService.SetBattleFocus(Dimmed) — 손 · 코드 이동 모두,
    /// 이때 Tab은 내지 않는다. 열린 시트에서 손으로 탭을 바꾸면 Tab. MAIN으로 닫히면(목표 상태가 바뀐 그 프레임 — 0.24초 내려가는 동안 함께 울린다)
    /// SheetClose(0.6) + ClearBattleFocus — 바로 닫기(도구 · 돌아가기 칩)는 미끄러짐이 없어 소리 없이 집중만 푼다.
    /// 사냥을 누르면 MainButton (메달 PressScale의 Tap은 Awake에서 끈다 — 소리가 겹치지 않게, 옆 탭 · 받기 버튼과 같은 규칙).
    /// </para>
    /// <para>
    /// 강화 탭 빛 (M35d): 강화 시트가 열려 있지 않은 동안(MAIN 또는 다른 탭, CR-08) x10 강화를 살 수 있으면(단추 ≥ GrowthCost10)
    /// 강화 탭 아이콘 밑에 금빛이 숨쉰다 (α 0.30 ↔ 0.55, 1.6초). 빨간 점과 상관없다. 단추가 바뀔 때(Currency.Changed) · 탭이 바뀔 때만 값 하나를 비교한다.
    /// </para>
    /// 시트(SheetController)가 없는 예전 장면이면 예전 6칸 탭 바처럼 늘 한 탭이 열려 있다 (_defaultIndex, MAIN 없음).
    /// </summary>
    public class MainTabController : MonoBehaviour
    {
        /// <summary>강화 탭 빛 숨쉬기 알파 (§1.8 탭 빛)</summary>
        const float GlowAlphaMin = 0.30f;
        const float GlowAlphaMax = 0.55f;
        /// <summary>사냥 칸 SHEET 모양 (§2.3): 아이콘 알파 / 실 고리 알파 MAIN · SHEET</summary>
        const float MainIconSheetAlpha = 0.8f;
        const float MainRingMainAlpha = 0.55f;
        const float MainRingSheetAlpha = 0.3f;
        /// <summary>안 고른 탭의 아트 아이콘 알파</summary>
        const float IdleIconAlpha = 0.55f;
        /// <summary>시트 닫는 소리 볼륨 (AUDIO-SPEC §2.1 — 여는 소리보다 가볍게)</summary>
        const float SheetCloseVolume = 0.6f;

        [SerializeField] Button[] _tabButtons;
        [SerializeField] GameObject[] _panels;
        [Tooltip("탭별 선택 받침 (탭 버튼과 같은 순서, 없으면 생략)")]
        [SerializeField] GameObject[] _selectedPlates;
        [Tooltip("시작 탭 — −1 = MAIN. 시트가 있는 장면은 언제나 MAIN으로 시작한다 (시트가 없는 예전 장면만 이 값을 쓴다)")]
        [SerializeField] int _defaultIndex = -1;
        [Tooltip("선택 탭 위 막대 (탭 바 위 끝, 앵커 x를 탭 가운데로 옮긴다 — MAIN에서는 보이지 않는다)")]
        [SerializeField] RectTransform _indicator;
        [Tooltip("패널 바꿈 페이드 (탭 순서, 비면 시작할 때 붙인다)")]
        [SerializeField] CanvasGroup[] _panelGroups;
        [Tooltip("강화 탭 아이콘 밑 금빛 (강화 시트가 아닐 때 x10 강화를 살 수 있으면)")]
        [SerializeField] Image _growthGlow;

        [Header("가운데 사냥 (Tab_Main — UX-SPEC-IA §2.2 · §2.3)")]
        [Tooltip("사냥 버튼 (누르면 MAIN). 탭 버튼 배열 · TabGate에는 넣지 않는다")]
        [SerializeField] Button _mainButton;
        [Tooltip("MAIN에서만 보이는 빛 (멈춰 있다 — 반복 없음)")]
        [SerializeField] GameObject _mainGlow;
        [SerializeField] TMP_Text _mainLabel;
        [Tooltip("SHEET에서 0.8로 흐려지는 그림 (전용 사냥 메달이면 메달, 아니면 수선점 아이콘)")]
        [SerializeField] Graphic _mainIcon;
        [Tooltip("메달 뒤 실 고리 (MAIN 0.55 / SHEET 0.3, 없어도 됨)")]
        [SerializeField] Graphic _mainRing;
        [Tooltip("탭 시트 (같은 TabBar의 SheetController). 비면 예전 6칸 탭 바처럼 늘 한 탭이 열려 있다")]
        [SerializeField] SheetController _sheet;

        /// <summary>열린(또는 열리는) 탭 = MainTab 번호, MAIN이면 −1 (목표 상태)</summary>
        public int SelectedIndex { get; private set; } = -1;

        /// <summary>MAIN(시트 없음, 사냥 화면)인지 = SelectedIndex −1 (UX-SPEC-IA §5.3 — 목표 상태라 닫히는 연출 중에도 true)</summary>
        public bool IsMain => SelectedIndex < 0;

        /// <summary>
        /// MAIN이 됨 (사냥 · 같은 탭 다시 누름 · 손잡이 · 끌어 내림 · 뒤로 · 자동 복귀 · UINav.GoToMain) — 목표 상태가 바뀐 그 프레임에
        /// (UIFrame.SheetOpen은 이미 false, 시트는 그 뒤 내려간다). 이미 MAIN이면 부르지 않는다
        /// </summary>
        public event Action MainShown;

        /// <summary>
        /// 탭 선택 확인 (기능 개방 F-03). false를 돌려주면 선택하지 않는다 — 확인하는 쪽(메타 UI의 탭 잠금, M3)이 토스트를 띄운다.
        /// 시작 MAIN에는 쓰지 않는다. 열린 탭을 다시 눌러 닫을 때도 묻지 않는다.
        /// </summary>
        public Func<MainTab, bool> SelectGuard { get; set; }

        /// <summary>탭 시트가 열림 · 바뀜 (새 탭, 시트를 연 뒤) — "새로 열림" 점 끄기(FeatureGate.MarkSeen), 튜토리얼 진행 확인용. 시작에는 부르지 않는다</summary>
        public event Action<MainTab> Selected;

        /// <summary>열린 탭을 손으로 다시 누름 (닫기 바로 전) — 탭 배지 다시 확인(TabGateView.MarkSeenTab)용. Selected는 부르지 않는다</summary>
        public event Action<MainTab> Reselected;

        /// <summary>
        /// 강화 x10 비용 (단추) — 강화 화면이 넣는다 (계속 넣어 둔다: 다른 탭 · MAIN에서도 이 값으로 빛을 정한다).
        /// 0 이하 · NaN · ∞ = 살 것이 없음(최대 레벨) → 빛 없음. 비싼 계산은 하지 말고 캐시해 둔 값을 돌려준다 (단추가 바뀔 때마다 불린다).
        /// 아직 아무도 넣지 않았으면(null) 빛은 꺼진 채다 (오류 없음).
        /// </summary>
        public static Func<double> GrowthCost10 { get; set; }

        public int TabCount => _tabButtons != null ? _tabButtons.Length : 0;

        /// <summary>탭 버튼 (순서 = MainTab). 범위 밖이면 null</summary>
        public Button GetTabButton(MainTab tab) => _tabButtons != null && (int)tab >= 0 && (int)tab < _tabButtons.Length ? _tabButtons[(int)tab] : null;

        /// <summary>가운데 사냥 버튼 (Tab_Main, 없으면 null)</summary>
        public Button MainButton => _mainButton;

        /// <summary>탭 시트 (없는 예전 장면이면 null)</summary>
        public SheetController Sheet => _sheet;

        RectTransform[] _icons = Array.Empty<RectTransform>();
        Vector2[] _iconBase = Array.Empty<Vector2>();
        TMP_Text[] _labels = Array.Empty<TMP_Text>();
        Coroutine[] _iconAnims = Array.Empty<Coroutine>();
        Coroutine[] _panelFades = Array.Empty<Coroutine>();
        Coroutine _indicatorAnim;
        Coroutine _mainAnim;
        Coroutine _glowAnim;
        Graphic _indicatorGraphic;
        Color _indicatorColor = Color.white;
        Graphic _mainGlowGraphic;
        float _mainGlowAlpha = 1f;
        /// <summary>사냥 칸 모양 1 = MAIN ~ 0 = SHEET (막대 알파는 1 − 이 값). 처음 맞추기 전에는 −1</summary>
        float _mainLook = -1f;
        /// <summary>지금 켜 둔 패널 (닫히는 동안에도 남아 있다, 다 닫히면 −1)</summary>
        int _shownPanel = -1;
        bool _applied;
        bool _glowOn;
        bool _currencySubscribed;
        Func<bool> _sheetBack;

        // ───────────── 수명 ─────────────

        void Awake()
        {
            EnsureCaches();
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                int index = i;
                _tabButtons[i].onClick.AddListener(() => OnTabClicked(index));
            }
            if (_mainButton != null)
            {
                _mainButton.onClick.AddListener(OnMainClicked);
                // 사냥은 자기 소리(MainButton)를 낸다 — 메달(Hit)의 PressScale Tap을 꺼 겹치지 않게
                var press = _mainButton.GetComponentInChildren<PressScale>(true);
                if (press != null) press.TapSound = false;
            }
            if (_indicator != null && _indicator.TryGetComponent(out _indicatorGraphic)) _indicatorColor = _indicatorGraphic.color;
            if (_mainGlow != null && _mainGlow.TryGetComponent(out _mainGlowGraphic)) _mainGlowAlpha = _mainGlowGraphic.color.a;
            if (_sheet != null) _sheet.Closed += OnSheetClosed;
            UINav.TabRequested += OnTabRequested;
            UINav.MainRequested += ShowMain;
            _sheetBack = SheetBack;
        }

        void Start()
        {
            if (Game.IsReady && Game.Currency != null)
            {
                Game.Currency.Changed += OnCurrencyChanged;
                _currencySubscribed = true;
            }
            var ui = UIManager.Instance;
            if (ui != null && _sheet != null) ui.SheetBackHandler = _sheetBack;
            if (_growthGlow != null && !_glowOn) _growthGlow.gameObject.SetActive(false);
            // 탭 칸 폭을 먼저 계산해 막대 자리를 바로 잴 수 있게 (탭 바 하나만 — 전체 캔버스를 다시 그리지 않는다)
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            if (!_applied)
            {
                if (_sheet != null)
                {
                    // 언제나 MAIN으로 시작한다: 패널은 시트가 처음 열릴 때 켜진다 (ContentArea가 꺼져 있어 OnEnable도 그때)
                    HidePanels();
                    Apply(-1, false);
                }
                else
                {
                    Apply(Mathf.Clamp(_defaultIndex, 0, Mathf.Max(0, _tabButtons.Length - 1)), false);
                }
            }
            RefreshGrowthGlow();
        }

        void OnDestroy()
        {
            UINav.TabRequested -= OnTabRequested;
            UINav.MainRequested -= ShowMain;
            if (_sheet != null) _sheet.Closed -= OnSheetClosed;
            var ui = UIManager.Instance;
            if (ui != null && ui.SheetBackHandler == _sheetBack) ui.SheetBackHandler = null;
            if (_currencySubscribed && Game.Currency != null) Game.Currency.Changed -= OnCurrencyChanged;
            // 시트가 열린 채 사라짐 (테스트 · 장면 정리): 전장 집중 요청만 지운다 — 소리는 내지 않는다
            AudioService.ClearBattleFocus(GetInstanceID());
        }

        // ───────────── 고르기 · MAIN ─────────────

        void OnTabRequested(MainTab tab) => Select((int)tab);

        public void Select(MainTab tab) => Select((int)tab);

        /// <summary>
        /// 그 탭 시트를 연다 (코드 이동도 이것 — MAIN이면 열고, 다른 탭이면 바꾸고, 같은 탭이면 그대로). 절대 닫지 않는다.
        /// SelectGuard가 막으면 아무것도 하지 않는다 (틀은 그대로). index −1은 ShowMain과 같다.
        /// </summary>
        public void Select(int index) => TrySelect(index);

        /// <summary>
        /// MAIN으로 (UX-SPEC-IA §5.3): SelectedIndex = −1 (목표 상태), UINav.ClearReturn() (CR-05), 시트 닫기 (immediate면 연출 없이 — 도구 · 테스트),
        /// MainShown. 패널은 닫기가 끝날 때 꺼진다. 닫히는 동안 Select가 오면 지금 p에서 다시 연다.
        /// 이미 MAIN이면 아무 일 없다 (immediate면 아직 닫히는 중인 시트를 바로 끝낸다). UINav.MainRequested가 이것을 부른다.
        /// </summary>
        public void ShowMain(bool immediate = false)
        {
            if (_sheet == null) return;
            UINav.ClearReturn();
            bool wasMain = IsMain;
            if (wasMain && (!immediate || _sheet.IsClosed)) return;
            if (!wasMain) Apply(-1, !immediate);
            _sheet.Close(immediate);
            if (wasMain) return;
            SheetCloseAudio(immediate);
            MainShown?.Invoke();
        }

        /// <summary>고른다 (Select). 선택이 실제로 바뀌었으면 true</summary>
        bool TrySelect(int index)
        {
            if (index < 0)
            {
                ShowMain();
                return false;
            }
            if (index >= _tabButtons.Length) return false;
            if (SelectGuard != null && !SelectGuard((MainTab)index)) return false;
            // MAIN(닫힘 · 닫히는 중)에서 연다 — 여는 소리 · 전장 집중은 여기서 한 번 (손 · 코드 이동 모두)
            bool opening = _sheet != null && IsMain;
            bool changed = Apply(index, true);
            if (_sheet != null) _sheet.Open(index);
            if (opening) SheetOpenAudio();
            // 시트를 연 뒤에 알린다 — 받는 쪽이 UIFrame.SheetOpen · 켜진 패널을 본다
            if (changed) Selected?.Invoke((MainTab)index);
            return changed;
        }

        /// <summary>
        /// 손으로 옆 탭을 누름: 열린 탭이면 Reselected 뒤 닫기, 다른 탭이면 고르고 돌아가기 칸을 비운다.
        /// 소리: MAIN에서 열면 SheetOpen (TrySelect) 하나, 열린 시트에서 바꾸면 Tab (시트가 없는 예전 장면은 늘 바꾸기)
        /// </summary>
        void OnTabClicked(int index)
        {
            if (index == SelectedIndex)
            {
                Reselected?.Invoke((MainTab)index);
                ShowMain();
                return;
            }
            bool switching = _sheet == null || !IsMain;
            if (!TrySelect(index)) return;
            if (switching) UISfx.Play(UISfxKey.Tab);
            UINav.ClearReturn();
        }

        /// <summary>사냥을 누름: MainButton 소리, 시트가 열려 있으면 닫기 (SheetClose도 함께 — MAIN이면 소리와 누름 표시만)</summary>
        void OnMainClicked()
        {
            UISfx.Play(UISfxKey.MainButton);
            if (IsMain || _sheet == null) return;
            UINav.ClearReturn();
            ShowMain();
        }

        /// <summary>뒤로가기 4단계 (UIManager.SheetBackHandler): 시트가 열려 있으면 MAIN으로 (끄는 중이면 지금 p에서 닫힌다)</summary>
        bool SheetBack()
        {
            if (_sheet == null || IsMain || !UIFrame.SheetOpen) return false;
            ShowMain();
            return true;
        }

        /// <summary>닫기가 끝남: 패널을 끈다 (그 사이 다시 골랐으면 그대로)</summary>
        void OnSheetClosed()
        {
            if (IsMain) HidePanels();
        }

        void HidePanels()
        {
            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] != null && _panels[i].activeSelf) _panels[i].SetActive(false);
            }
            _shownPanel = -1;
        }

        /// <summary>선택 표시를 index로 맞춘다 (−1 = MAIN). 선택이 바뀌었으면 true (알림은 부르는 쪽이 시트를 연 뒤에)</summary>
        bool Apply(int index, bool animate)
        {
            EnsureCaches();
            int previous = SelectedIndex;
            // 같은 탭을 다시 고름 (코드 이동 두 번 등): 표시는 이미 맞다
            if (_applied && previous == index) return false;
            SelectedIndex = index;
            bool motion = animate && _applied && !UIMotion.Reduced;
            _applied = true;

            if (index >= 0) ShowPanel(index, motion);

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                bool selected = i == index;
                if (_selectedPlates != null && i < _selectedPlates.Length && _selectedPlates[i] != null)
                    _selectedPlates[i].SetActive(selected);
                var label = i < _labels.Length ? _labels[i] : null;
                if (label != null) label.color = SideLabelColor(selected);
                if (i < _icons.Length && _icons[i] != null && _icons[i].TryGetComponent<Image>(out var iconImage))
                {
                    // 아트 아이콘이 있으면 흰색(원본), 없으면 자리표시 원을 탭 색으로
                    iconImage.color = iconImage.sprite != null && iconImage.sprite.name != "UI_Circle"
                        ? (selected ? Color.white : new Color(1f, 1f, 1f, IdleIconAlpha))
                        : (selected ? UITheme.Gold : UITheme.SubText);
                }
                // 처음 · 줄이기는 모두 제자리로, 아니면 상태가 바뀐 아이콘만 움직인다
                if (!motion) SnapIcon(i, selected);
                else if (i == index || i == previous) AnimateIcon(i, selected);
            }
            // 막대는 사냥 칸 모양보다 먼저 (MAIN에서 열 때 안 보이는 동안 새 탭으로 옮긴다)
            MoveIndicator(index, motion);
            SetMainLook(index < 0 && _sheet != null, motion);
            RefreshGrowthGlow();
            return previous != index;
        }

        // ───────────── 소리 · 전장 집중 (AUDIO-SPEC §8.1) ─────────────

        /// <summary>MAIN에서 시트가 열림: 여는 소리 + 전장 소리 흐림 (시트가 전장을 40% 넘게 덮는다 — 주인 = 이 컨트롤러)</summary>
        void SheetOpenAudio()
        {
            UISfx.Play(UISfxKey.SheetOpen);
            AudioService.SetBattleFocus(BattleAudioFocus.Dimmed, GetInstanceID());
        }

        /// <summary>시트가 MAIN으로 닫힘: 닫는 소리(0.6, 내려가는 0.24초와 함께) + 전장 집중 풀기. 바로 닫기(immediate)는 미끄러짐이 없어 집중만 푼다</summary>
        void SheetCloseAudio(bool immediate)
        {
            if (!immediate) UISfx.Play(UISfxKey.SheetClose, SheetCloseVolume);
            AudioService.ClearBattleFocus(GetInstanceID());
        }

        // ───────────── 패널 ─────────────

        /// <summary>그 패널만 켠다. 시트가 보이는 동안 바뀌었으면 새 패널만 알파 0 → 1</summary>
        void ShowPanel(int index, bool motion)
        {
            int previous = _shownPanel;
            for (int i = 0; i < _panels.Length; i++)
            {
                bool on = i == index;
                if (_panels[i] != null && _panels[i].activeSelf != on) _panels[i].SetActive(on);
            }
            _shownPanel = index;
            if (previous == index) return;
            bool switching = previous >= 0 && (_sheet == null || _sheet.IsShown);
            FadeInPanel(index, previous, motion && switching);
        }

        void CachePanelGroups()
        {
            int count = _panels.Length;
            if (_panelGroups == null || _panelGroups.Length != count) Array.Resize(ref _panelGroups, count);
            _panelFades = new Coroutine[count];
            for (int i = 0; i < count; i++)
            {
                if (_panelGroups[i] != null || _panels[i] == null) continue;
                // 다시 빌드하기 전 씬: 패널 루트에 페이드용 CanvasGroup을 붙인다 (누름은 그대로 받는다)
                if (!_panels[i].TryGetComponent(out _panelGroups[i])) _panelGroups[i] = _panels[i].AddComponent<CanvasGroup>();
            }
        }

        void FadeInPanel(int index, int previous, bool motion)
        {
            if (previous >= 0 && previous < _panelFades.Length)
            {
                if (_panelFades[previous] != null)
                {
                    StopCoroutine(_panelFades[previous]);
                    _panelFades[previous] = null;
                }
                if (_panelGroups[previous] != null) _panelGroups[previous].alpha = 1f;
            }
            if (index < 0 || index >= _panelFades.Length || _panelGroups[index] == null) return;
            var group = _panelGroups[index];
            group.blocksRaycasts = true;
            if (!motion)
            {
                if (_panelFades[index] != null)
                {
                    StopCoroutine(_panelFades[index]);
                    _panelFades[index] = null;
                }
                group.alpha = 1f;
                return;
            }
            MenuFx.Restart(this, ref _panelFades[index], FadePanel(index, group));
            if (_panelFades[index] == null) group.alpha = 1f;
        }

        IEnumerator FadePanel(int index, CanvasGroup group)
        {
            for (float t = 0f; t < UIMotion.PanelFade; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / UIMotion.PanelFade;
                yield return null;
            }
            group.alpha = 1f;
            _panelFades[index] = null;
        }

        // ───────────── 아이콘 ─────────────

        void EnsureCaches()
        {
            if (_tabButtons == null) _tabButtons = Array.Empty<Button>();
            if (_panels == null) _panels = Array.Empty<GameObject>();
            if (_icons.Length != _tabButtons.Length) CacheIcons();
            if (_panelFades.Length != _panels.Length) CachePanelGroups();
        }

        /// <summary>아이콘 제자리 · 이름 글자를 한 번만 잰다 (Start 전에 Apply가 먼저 돌아도 — 다시 재면 올라간 자리를 제자리로 안다)</summary>
        void CacheIcons()
        {
            int count = _tabButtons.Length;
            _icons = new RectTransform[count];
            _iconBase = new Vector2[count];
            _labels = new TMP_Text[count];
            _iconAnims = new Coroutine[count];
            for (int i = 0; i < count; i++)
            {
                if (_tabButtons[i] == null) continue;
                var icon = _tabButtons[i].transform.Find("Icon") as RectTransform;
                _icons[i] = icon;
                if (icon != null) _iconBase[i] = icon.anchoredPosition;
                var label = _tabButtons[i].transform.Find("Label");
                _labels[i] = label != null ? label.GetComponent<TMP_Text>() : _tabButtons[i].GetComponentInChildren<TMP_Text>(true);
            }
        }

        /// <summary>고른 아이콘 자리: 아이콘 가운데를 기준으로 커진 만큼 피벗 보정 + 6px 위로 (피벗이 위에 있어도 가운데가 6px 올라간다)</summary>
        Vector2 SelectedIconPosition(int i)
        {
            var icon = _icons[i];
            float grow = (UIMotion.TabIconScale - 1f) * icon.rect.height;
            return _iconBase[i] + new Vector2(0f, UIMotion.TabIconLift + (icon.pivot.y - 0.5f) * grow);
        }

        void SnapIcon(int i, bool selected)
        {
            if (i >= _icons.Length || _icons[i] == null) return;
            if (_iconAnims[i] != null)
            {
                StopCoroutine(_iconAnims[i]);
                _iconAnims[i] = null;
            }
            _icons[i].localScale = Vector3.one * (selected ? UIMotion.TabIconScale : 1f);
            _icons[i].anchoredPosition = selected ? SelectedIconPosition(i) : _iconBase[i];
        }

        void AnimateIcon(int i, bool selected)
        {
            if (i < 0 || i >= _icons.Length || _icons[i] == null) return;
            if (_iconAnims[i] != null) StopCoroutine(_iconAnims[i]);
            _iconAnims[i] = isActiveAndEnabled ? StartCoroutine(IconTo(i, selected)) : null;
            if (_iconAnims[i] == null) SnapIcon(i, selected);
        }

        IEnumerator IconTo(int i, bool selected)
        {
            var icon = _icons[i];
            float fromScale = icon.localScale.x;
            var fromPosition = icon.anchoredPosition;
            float toScale = selected ? UIMotion.TabIconScale : 1f;
            var toPosition = selected ? SelectedIconPosition(i) : _iconBase[i];
            float duration = selected ? UIMotion.TabIconSelect : UIMotion.TabIconDeselect;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = selected ? UIMotion.OutBack(t / duration) : UIMotion.OutCubic(t / duration);
                icon.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, toScale, k);
                icon.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, k);
                yield return null;
            }
            icon.localScale = Vector3.one * toScale;
            icon.anchoredPosition = toPosition;
            _iconAnims[i] = null;
        }

        // ───────────── 선택 막대 · 사냥 칸 ─────────────

        /// <summary>막대를 열린 탭 가운데로 (MAIN이면 자리는 그대로 두고 사냥 칸 모양이 알파를 내린다). 안 보이던 막대는 바로 옮긴다</summary>
        void MoveIndicator(int index, bool motion)
        {
            if (_indicator == null || index < 0 || index >= _tabButtons.Length) return;
            float target = TabCenter01(index);
            bool hidden = _sheet != null && _mainLook > 0.99f;
            if (!motion || hidden)
            {
                if (_indicatorAnim != null)
                {
                    StopCoroutine(_indicatorAnim);
                    _indicatorAnim = null;
                }
                SetIndicatorX(target);
                return;
            }
            MenuFx.Restart(this, ref _indicatorAnim, IndicatorTo(target));
            if (_indicatorAnim == null) SetIndicatorX(target);
        }

        IEnumerator IndicatorTo(float target)
        {
            float from = _indicator.anchorMin.x;
            for (float t = 0f; t < UIMotion.TabIndicator; t += Time.unscaledDeltaTime)
            {
                SetIndicatorX(Mathf.Lerp(from, target, UIMotion.OutCubic(t / UIMotion.TabIndicator)));
                yield return null;
            }
            SetIndicatorX(target);
            _indicatorAnim = null;
        }

        /// <summary>막대 앵커 x (탭 바 폭에 대한 비율 — 안전 영역 · 화면 폭이 바뀌어도 탭 가운데에 남는다)</summary>
        void SetIndicatorX(float x01)
        {
            _indicator.anchorMin = new Vector2(x01, _indicator.anchorMin.y);
            _indicator.anchorMax = new Vector2(x01, _indicator.anchorMax.y);
        }

        /// <summary>탭 가운데의 탭 바 비율 위치 (레이아웃이 아직이면 탭 바만 다시 계산하고, 그래도 모르면 같은 폭 칸으로 본다)</summary>
        float TabCenter01(int index)
        {
            var bar = (RectTransform)transform;
            var button = _tabButtons[index] != null ? (RectTransform)_tabButtons[index].transform : null;
            if (button == null) return (index + 0.5f) / Mathf.Max(1, _tabButtons.Length);
            if (bar.rect.width <= 1f || button.rect.width <= 1f) LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
            float width = bar.rect.width;
            if (width > 1f && button.rect.width > 1f)
            {
                float x = bar.InverseTransformPoint(button.TransformPoint(button.rect.center)).x;
                return Mathf.Clamp01((x - bar.rect.xMin) / width);
            }
            return (index + 0.5f) / Mathf.Max(1, _tabButtons.Length);
        }

        /// <summary>사냥 칸 · 막대 모양을 MAIN(1) 또는 SHEET(0)로 (0.12초 OutCubic, 알파만 — 빛은 멈춰 있다)</summary>
        void SetMainLook(bool main, bool motion)
        {
            float target = main ? 1f : 0f;
            if (_mainLook == target && _mainAnim == null) return;
            if (!motion || _mainLook < 0f)
            {
                if (_mainAnim != null)
                {
                    StopCoroutine(_mainAnim);
                    _mainAnim = null;
                }
                ApplyMainLook(target);
                return;
            }
            MenuFx.Restart(this, ref _mainAnim, MainLookTo(target));
            if (_mainAnim == null) ApplyMainLook(target);
        }

        IEnumerator MainLookTo(float target)
        {
            float from = _mainLook;
            for (float t = 0f; t < UIMotion.MainGlowFade; t += Time.unscaledDeltaTime)
            {
                ApplyMainLook(Mathf.Lerp(from, target, UIMotion.OutCubic(t / UIMotion.MainGlowFade)));
                yield return null;
            }
            ApplyMainLook(target);
            _mainAnim = null;
        }

        /// <summary>k = 1 MAIN 모양 (빛 · 고리 0.55 · 아이콘 1 · Gold, 막대 0) ~ 0 SHEET 모양 (빛 0 · 고리 0.3 · 아이콘 0.8 · SubText, 막대 1)</summary>
        void ApplyMainLook(float k)
        {
            _mainLook = k;
            if (_mainGlow != null)
            {
                bool on = k > 0.001f;
                if (_mainGlow.activeSelf != on) _mainGlow.SetActive(on);
                if (_mainGlowGraphic != null) SetAlpha(_mainGlowGraphic, _mainGlowAlpha * k);
            }
            if (_mainRing != null) SetAlpha(_mainRing, Mathf.Lerp(MainRingSheetAlpha, MainRingMainAlpha, k));
            if (_mainIcon != null) SetAlpha(_mainIcon, Mathf.Lerp(MainIconSheetAlpha, 1f, k));
            if (_mainLabel != null) _mainLabel.color = Color.Lerp(UITheme.SubText, UITheme.Gold, k);
            if (_indicatorGraphic != null) SetAlpha(_indicatorGraphic, _indicatorColor.a * (1f - k));
        }

        /// <summary>
        /// 옆 탭 이름 글자 색: 고름 = Cream, 아님 = SubText. 고른 이름의 읽힘은 글자 머티리얼(UiArt.Outline)의 어두운 외곽선이 맡는다 —
        /// 글자 면 ↔ 외곽선 ≈15:1, 외곽선 ↔ 보라 받침 ≥3.9:1. 받침(ui_tab_selected) 위 Cream 면만으로는 ≈2.8:1 (아래 따뜻한 빛 위 ≈1.3:1)이라
        /// 외곽선 없이는 읽히지 않는다 (받침 0.75 틴트는 IdleSceneBuilder 요청).
        /// 금색은 막대 · 강화 빛 · 사냥 칸에만 쓴다 (보라 받침 위 금 글자 ≈2.3:1, Wave 2 r2)
        /// </summary>
        internal static Color SideLabelColor(bool selected) => selected ? UITheme.Cream : UITheme.SubText;

        static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            if (color.a == alpha) return;
            color.a = alpha;
            graphic.color = color;
        }

        // ───────────── 강화 탭 빛 (M35d) ─────────────

        void OnCurrencyChanged(CurrencyType type, double value)
        {
            if (type == CurrencyType.Gold) RefreshGrowthGlow();
        }

        /// <summary>강화 탭 빛을 지금 값으로 다시 정한다 (강화 화면이 x10 비용을 바꾼 뒤 불러도 된다). 강화 시트가 열려 있지 않은 동안만 (CR-08)</summary>
        public void RefreshGrowthGlow()
        {
            if (_growthGlow == null) return;
            bool show = SelectedIndex != (int)MainTab.Growth && CanAffordGrowthBatch();
            if (show == _glowOn) return;
            _glowOn = show;
            if (_glowAnim != null)
            {
                StopCoroutine(_glowAnim);
                _glowAnim = null;
            }
            _growthGlow.gameObject.SetActive(show);
            if (!show) return;
            if (UIMotion.Reduced || !isActiveAndEnabled) SetGlowAlpha((GlowAlphaMin + GlowAlphaMax) * 0.5f);
            else _glowAnim = StartCoroutine(BreatheGlow());
        }

        static bool CanAffordGrowthBatch()
        {
            var cost10 = GrowthCost10;
            if (cost10 == null || !Game.IsReady || Game.Currency == null) return false;
            double cost = cost10();
            if (!(cost > 0d) || double.IsInfinity(cost)) return false;
            return Game.Currency.Get(CurrencyType.Gold) >= cost;
        }

        IEnumerator BreatheGlow()
        {
            while (true)
            {
                SetGlowAlpha(UIMotion.Breathe(Time.unscaledTime, GlowAlphaMin, GlowAlphaMax));
                yield return null;
            }
        }

        void SetGlowAlpha(float alpha)
        {
            var color = _growthGlow.color;
            color.a = alpha;
            _growthGlow.color = color;
        }
    }
}
