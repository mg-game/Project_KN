using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 강해지기 시트 (M13 2단계, UX-SPEC §2.1.5) 860×760 — 지금 스테이지 권장 전투력에 모자랄 때 어디서 강해질지 한곳에 모아 보여 준다.
    ///   · 머리: '권장 {rec} / 내 {power}' + 모자란 만큼 '-{gap}' (Negative) + 평가 칩(여유 · 도전 · 어려움) + 내 전투력 / 권장 비율 막대(평가 색) + 한 줄 안내.
    ///   · 갈 곳 줄 (줄마다 '가기 ›' Primary 160×64 — 누르면 시트를 닫고 간다, 잠긴 기능은 토스트만). 줄은 세로 줄 묶음 안에 있어 숨은 줄 자리는 위로 당겨진다.
    ///     (UX Wave 2의 고정 3줄 — 수선점 강화 · 가장 낮은 친구 · 자동 편성 — 은 RM-2 part B에서 지웠다: 조언자가 같은 줄을 전투력 순으로 낸다.)
    /// 여는 곳: 권장 전투력 알약(평가 '어려움') · 가이드 줄의 이동 없는 스테이지 퀘스트 (나중에 WP-5 보스 도전 버튼의 권장 줄 · 실패 띠).
    /// 4차 (PRG-03, SPEC4 §13.2 · C.5, RM-2 P4-6):
    ///   · 강해지기 줄 = StrengthenAdvisor.GetRows (최대 4줄 — 열 때와 열린 동안 1초마다, 바뀐 줄만 다시 쓴다). 줄: 종류 아이콘 · 제목 28 · 한 줄 22
    ///     ('전투력 +{gain}' — C.5 문구, n = StrengthenRow.Amount) · [가기 ›] (기능 이동 + 손가락 · 친구 레벨 줄은 그 친구 카드(GrowthPanel.FocusLineage) ·
    ///     약한 방향 줄은 편성 탭 + 그 칸 반짝(UINav.PulseFormationSlot — 패배 분석 고리와 같은 길, §7.2) · 모자란 재료는 그 재료 얻는 곳).
    ///     줄이 없으면(조언자 없음 · 할 일 없음) C.5 빈 글. 조언자가 예외를 내면 한 번 남기고 이 시트에서는 빈 글로 둔다 (새 코드는 던지지 않는다).
    ///   · 머리의 [친구들의 응원 받기] Gold 300×72 + '10분 동안 공격력 +20% · 이 장에서 한 번' (Hint 20): Cheer.TryUse. 쓴 뒤에는 누를 수 없고
    ///     '다음 장에서 다시 받을 수 있어요'. 튜토리얼 중 · 응원 시스템이 없으면 숨긴다. 누르면 cheer_start 소리 + 가벼운 진동.
    /// 장면 경로 Popups/StrengthenPopup — MetaSceneBuilder.BuildPopups가 만들어 ctx.Popups에 넣는다.
    /// 열 때 한 번 쓰고, 열린 동안은 전투력 · 스테이지 알림에만 다시 쓴다 (매 프레임 아님). 메타가 꺼져 있으면 열리지 않는다.
    /// </summary>
    public class StrengthenPopup : UIPopup
    {
        /// <summary>빌더가 처음 켜 두는 줄 수 (예전 장면은 줄이 3개뿐 — 열면 조언 줄 수만큼 다시 켠다)</summary>
        public const int RowCount = 3;
        /// <summary>강해지기 줄 최대 수 (StrengthenAdvisor.GetRows max — 빌더가 이만큼 만든다, 예전 장면은 3줄)</summary>
        public const int MaxRows = 4;
        /// <summary>줄을 다시 셈하는 간격 (초, §13.2 — 열린 동안 1Hz)</summary>
        const float AdviceInterval = 1f;
        public const string EmptyText = "지금은 더 할 일이 없어요. 바구니와 원정으로 재료를 모아 봐요";
        public const string CheerLabel = "친구들의 응원 받기";
        public const string CheerUsedText = "다음 장에서 다시 받을 수 있어요";
        /// <summary>응원을 받는 순간의 소리 키 (AUDIO 'cheer_start' — 전투 쪽 응원 칩은 소리를 내지 않는다)</summary>
        public const string CheerStartKey = "cheer_start";

        /// <summary>약한 방향 줄 [가기]: 편성 칸 반짝 시간 (초, §7.2 고리와 같은 값)</summary>
        public const float WeakSlotPulseSeconds = 2f;

        static readonly string NegativeTag = UITheme.ColorTag(UITheme.Negative);

        [Header("머리")]
        [Tooltip("'권장 {rec} / 내 {power}  -{gap}' (리치 텍스트)")]
        [SerializeField] TMP_Text _headerText;
        [SerializeField] TMP_Text _hintText;
        [Tooltip("평가 칩 바탕 (평가 색을 옅게)")]
        [SerializeField] Image _ratingChip;
        [SerializeField] TMP_Text _ratingText;
        [Tooltip("내 전투력 / 권장 비율 막대 채움 (MetaUi.SetBar)")]
        [SerializeField] Image _ratioFill;

        [Header("갈 곳 줄 (MaxRows개 — 예전 장면은 3개)")]
        [SerializeField] GameObject[] _rows = Array.Empty<GameObject>();
        [SerializeField] Image[] _rowIcons = Array.Empty<Image>();
        [SerializeField] TMP_Text[] _rowTitles = Array.Empty<TMP_Text>();
        [SerializeField] TMP_Text[] _rowDescs = Array.Empty<TMP_Text>();
        [SerializeField] Button[] _rowButtons = Array.Empty<Button>();
        [SerializeField] TMP_Text _emptyText;

        [Header("4차 (없어도 됨)")]
        [Tooltip("종류 아이콘 (기능 그림)")]
        [SerializeField] MetaIcons _icons;
        [SerializeField] Button _cheerButton;
        [SerializeField] TMP_Text _cheerLabel;
        [SerializeField] TMP_Text _cheerSubText;

        bool _subscribed;
        readonly List<StrengthenRow> _advice = new(MaxRows);
        /// <summary>줄마다 마지막으로 쓴 조언 (종류 · 대상 · 전투력 · 수 — 바뀐 줄만 다시 쓴다)</summary>
        readonly StrengthenKind[] _shownKind = new StrengthenKind[MaxRows];
        readonly string[] _shownTarget = new string[MaxRows];
        readonly long[] _shownGain = new long[MaxRows];
        readonly int[] _shownAmount = new int[MaxRows];
        /// <summary>지금 보이는 조언 줄 수 (−1 = 다시 다 쓴다, 0 = 빈 글)</summary>
        int _shownAdvice = -1;
        /// <summary>조언 줄 [가기]가 갈 곳 (줄 번호마다)</summary>
        readonly FeatureId[] _adviceNav = new FeatureId[MaxRows];
        readonly string[] _adviceFocus = new string[MaxRows];
        readonly StrengthenKind[] _adviceKind = new StrengthenKind[MaxRows];
        readonly string[] _adviceTarget = new string[MaxRows];
        readonly int[] _adviceSlot = new int[MaxRows];
        /// <summary>조언자가 예외를 냈다 (이 시트에서는 다시 부르지 않는다)</summary>
        bool _adviceFailed;
        float _adviceTimer;
        int _cheerState = -1;
        /// <summary>응원 소리 번호 (처음 누를 때 한 번 찾는다 — AUD R4)</summary>
        AudioId _cheerStartId;

        /// <summary>
        /// 장면의 강해지기 시트를 연다. 시트가 없으면(다시 빌드하기 전 장면 · UIManager 없음) 오류 로그 없이 false —
        /// 부르는 쪽이 대신할 일(토스트 · 강화 탭 등)을 한다. 열었다가 OnOpen 안에서 바로 닫혔어도(메타 꺼짐) false.
        /// </summary>
        public static bool TryOpen()
        {
            var ui = UIManager.Instance;
            if (ui == null || !ui.TryGet<StrengthenPopup>(out var popup)) return false;
            popup.Open();
            return popup.IsOpen;
        }

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < _rowButtons.Length; i++)
            {
                int row = i;
                if (_rowButtons[i] != null) _rowButtons[i].onClick.AddListener(() => OnGo(row));
            }
            if (_cheerButton != null) _cheerButton.onClick.AddListener(OnCheer);
        }

        protected override void OnOpen()
        {
            if (!MetaUi.IsActive)
            {
                Close();
                return;
            }
            if (!_subscribed)
            {
                Game.Power.Changed += OnPowerChanged;
                Game.Battle.StageChanged += Refresh;
                if (Game.Cheer != null) Game.Cheer.Changed += RefreshCheer;
                _subscribed = true;
            }
            _shownAdvice = -1;
            _cheerState = -1;
            _adviceTimer = AdviceInterval;
            Refresh();
        }

        void Update()
        {
            if (!IsOpen || !MetaUi.IsActive) return;
            _adviceTimer -= Time.unscaledDeltaTime;
            if (_adviceTimer > 0f) return;
            _adviceTimer = AdviceInterval;
            RefreshRows(false);
            RefreshCheer();
        }

        protected override void OnClose()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (!Game.IsReady || Game.Power == null) return;
            Game.Power.Changed -= OnPowerChanged;
            if (Game.Battle != null) Game.Battle.StageChanged -= Refresh;
            if (Game.Cheer != null) Game.Cheer.Changed -= RefreshCheer;
        }

        void OnPowerChanged(long oldPower, long newPower) => Refresh();

        // ───────────── 표시 ─────────────

        void Refresh()
        {
            if (!IsOpen || !MetaUi.IsActive) return;
            var power = Game.Power;
            int stage = Game.Battle.Stage;
            long recommended = power.GetRecommendedPower(stage);
            long mine = power.TeamPower;
            var rating = power.GetRating(mine, recommended);
            var ratingColor = RecommendedPowerView.RatingColor(rating);

            if (_headerText != null) _headerText.text = HeaderText(recommended, mine);
            if (_ratingText != null)
            {
                _ratingText.text = PowerSystem.RatingName(rating);
                _ratingText.color = ratingColor;
            }
            if (_ratingChip != null) _ratingChip.color = MetaUi.Alpha(ratingColor, 0.2f);
            if (_ratioFill != null)
            {
                MetaUi.SetBar(_ratioFill, recommended > 0 ? (float)((double)mine / recommended) : 1f);
                _ratioFill.color = ratingColor;
            }
            if (_hintText != null)
            {
                _hintText.text = rating switch
                {
                    PowerRating.Hard => "보스가 아직 버거워요. 아래에서 하나씩 강해져 봐요",
                    PowerRating.Challenge => "조금만 더 강해지면 훨씬 수월해져요",
                    _ => "지금도 넉넉해요! 더 키우면 더 빨리 나아가요",
                };
            }

            _shownAdvice = -1;
            RefreshRows(true);
            RefreshCheer();
        }

        /// <summary>
        /// 줄: 조언자(StrengthenAdvisor) 줄 (최대 4줄 — 예전 장면은 3줄), 없으면 C.5 빈 글. 바뀐 줄만 다시 쓴다 (1초마다 불려도 같으면 글을 만들지 않는다)
        /// </summary>
        void RefreshRows(bool force)
        {
            // GetRows가 목록을 비우고 그 안에 있던 줄 객체를 다시 쓴다 (1초마다 새 줄을 만들지 않게 — 여기서 먼저 비우지 않는다).
            // 줄 값은 다음 GetRows까지만 유효하므로 [가기]에 쓰는 값은 BindAdvice가 배열에 옮겨 둔다
            var advisor = _adviceFailed ? null : Game.Strengthen;
            if (advisor == null) _advice.Clear();
            else
            {
                try
                {
                    advisor.GetRows(_advice, Math.Min(MaxRows, _rows.Length));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _adviceFailed = true;
                    _advice.Clear();
                }
            }
            if (_advice.Count > 0)
            {
                BindAdvice();
                return;
            }
            if (!force && _shownAdvice == 0) return;
            _shownAdvice = 0;
            for (int i = 0; i < _rows.Length; i++) SetRowVisible(i, false);
            if (_emptyText != null)
            {
                _emptyText.gameObject.SetActive(true);
                _emptyText.text = EmptyText;
            }
        }

        void BindAdvice()
        {
            bool full = _shownAdvice != _advice.Count;
            _shownAdvice = _advice.Count;
            int count = Math.Min(_advice.Count, _rows.Length);
            long team = Game.Power != null ? Game.Power.TeamPower : 0L;
            for (int i = 0; i < _rows.Length; i++)
            {
                bool used = i < count;
                SetRowVisible(i, used);
                if (!used) continue;
                var row = _advice[i];
                if (row == null) continue;
                if (!full && i < MaxRows && _shownKind[i] == row.Kind && _shownTarget[i] == row.TargetId && _shownGain[i] == row.Gain
                    && _shownAmount[i] == row.Amount && _adviceSlot[i] == row.Slot) continue;
                if (i < MaxRows)
                {
                    _shownKind[i] = row.Kind;
                    _shownTarget[i] = row.TargetId;
                    _shownGain[i] = row.Gain;
                    _shownAmount[i] = row.Amount;
                    _adviceNav[i] = row.Nav;
                    _adviceFocus[i] = row.Focus;
                    _adviceKind[i] = row.Kind;
                    _adviceTarget[i] = row.TargetId;
                    _adviceSlot[i] = row.Slot;
                }
                RowText(row, team, out var title, out var desc);
                SetRowText(i, title, desc);
                SetRowIcon(i, KindIcon(row));
            }
            if (_emptyText != null) _emptyText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 조언 줄 글 (SPEC4 C.5). 대상 이름은 콘텐츠에서 찾는다 (계보 · 형태 · 시너지 · 재료). n = StrengthenRow.Amount (수선점 레벨 수 · 올릴 레벨 수 ·
        /// 약한 방향 몫 %, 0이면 숫자 없이). 순수에 가깝다 (Game.Content만 읽는다)
        /// </summary>
        public static void RowText(StrengthenRow row, long teamPower, out string title, out string desc)
        {
            string gain = row.Gain > 0 ? $"전투력 +{MetaUi.Int(row.Gain)}" : string.Empty;
            string name = TargetName(row);
            switch (row.Kind)
            {
                case StrengthenKind.ShopUpgrade:
                    title = row.Amount > 0 ? $"수선점 {row.Amount}번 강화" : "수선점 강화";
                    desc = gain;
                    break;
                case StrengthenKind.LineageLevel:
                    title = row.Amount > 0
                        ? $"{(string.IsNullOrEmpty(name) ? "친구" : name)} 레벨 +{row.Amount}"
                        : string.IsNullOrEmpty(name) ? "친구 레벨 올리기" : $"{name} 레벨 올리기";
                    desc = gain;
                    break;
                case StrengthenKind.FillSlot:
                {
                    // C.5 '빈 {지킴이|잔향이} 자리 채우기' / '{이름}{을/를} 세우면 전투력 +n' — Role = 그 빈 칸의 쪽, CandidateId = 세울 형태 (P4-1)
                    title = $"빈 {UITheme.RoleName(row.Role)} 자리 채우기";
                    string who = FormName(row.CandidateId) ?? name;
                    desc = string.IsNullOrEmpty(who) ? gain
                        : row.Gain > 0 ? $"{MetaUi.WithObject(who)} 세우면 {gain}" : $"{MetaUi.WithObject(who)} 세워 봐요";
                    break;
                }
                case StrengthenKind.SynergyOneShort:
                {
                    // C.5 '{시너지} 한 명만 더' / '{이름}{을/를} 세우면 켜져요 · 전투력 +n' — 이름 = CandidateId (세울 형태, P4-1)
                    title = string.IsNullOrEmpty(name) ? "시너지 한 명만 더" : $"{name} 한 명만 더";
                    string who = FormName(row.CandidateId);
                    string lead = string.IsNullOrEmpty(who) ? "한 명 더 세우면 켜져요" : $"{MetaUi.WithObject(who)} 세우면 켜져요";
                    desc = row.Gain > 0 ? $"{lead} · {gain}" : lead;
                    break;
                }
                case StrengthenKind.ResonanceReady:
                    title = string.IsNullOrEmpty(name) ? "공명을 올릴 수 있어요" : $"{name} 공명을 올릴 수 있어요";
                    desc = gain;
                    break;
                case StrengthenKind.AutoFormation:
                {
                    double pct = teamPower > 0 ? row.Gain * 100.0 / teamPower : 0;
                    title = "자동 편성으로 바꾸기";
                    desc = $"지금보다 {Math.Max(1, (int)Math.Round(pct))}% 강해요 · {gain}";
                    break;
                }
                case StrengthenKind.EvolutionReady:
                    title = string.IsNullOrEmpty(name) ? "진화를 만들 수 있어요" : $"{name} 진화를 만들 수 있어요";
                    desc = "재료가 모두 모였어요";
                    break;
                case StrengthenKind.MissingMaterial:
                {
                    title = string.IsNullOrEmpty(name) ? "재료가 조금 모자라요" : $"{MetaUi.WithSubject(name)} 조금 모자라요";
                    string source = SourceLabel(row.TargetId);
                    desc = string.IsNullOrEmpty(source) ? "바구니와 원정에서 모아요" : $"{source}에서 모아요";
                    break;
                }
                case StrengthenKind.WeakDirection:
                    title = string.IsNullOrEmpty(row.TargetId) ? "약한 방향 보강" : $"약한 방향 보강 · {row.TargetId}";
                    desc = row.Amount > 0 ? $"지난 패배 피해의 {Math.Min(100, row.Amount)}%가 이쪽으로 왔어요" : "편성에서 그 방향 칸을 채워 봐요";
                    break;
                default:
                    title = "강해지기";
                    desc = gain;
                    break;
            }
        }

        /// <summary>세울 형태의 이름 (StrengthenRow.CandidateId — 형태, 없으면 계보 이름, 못 찾으면 null)</summary>
        static string FormName(string id)
        {
            if (string.IsNullOrEmpty(id) || !Game.IsReady) return null;
            var characters = Game.Content?.Characters;
            var form = characters?.GetForm(id);
            if (form != null) return form.name;
            return characters?.GetLineage(id)?.name;
        }

        /// <summary>조언 대상 이름 (계보 → 형태 → 시너지 → 재료 순으로 찾는다, 없으면 null)</summary>
        static string TargetName(StrengthenRow row)
        {
            string id = row.TargetId;
            if (string.IsNullOrEmpty(id) || !Game.IsReady) return null;
            var characters = Game.Content?.Characters;
            var lineage = characters?.GetLineage(id);
            if (lineage != null) return lineage.name;
            var form = characters?.GetForm(id);
            if (form != null) return form.name;
            var synergy = Game.Content?.Synergies?.Get(id);
            if (synergy != null) return synergy.name;
            var item = Game.Items?.Get(id);
            return item?.name;
        }

        /// <summary>재료의 첫 얻는 곳 이름 (없으면 null)</summary>
        static string SourceLabel(string itemId)
        {
            var item = !string.IsNullOrEmpty(itemId) && Game.IsReady ? Game.Items?.Get(itemId) : null;
            if (item?.sources == null) return null;
            foreach (var source in item.sources)
            {
                if (source != null && !string.IsNullOrEmpty(source.label)) return source.label;
            }
            return null;
        }

        Sprite KindIcon(StrengthenRow row)
        {
            switch (row.Kind)
            {
                case StrengthenKind.LineageLevel:
                case StrengthenKind.ResonanceReady:
                {
                    var lineage = Game.Content?.Characters?.GetLineage(row.TargetId);
                    var portrait = lineage != null && Game.Characters != null ? Game.Characters.GetPortrait(lineage.baseFormId) : null;
                    if (portrait != null) return portrait;
                    break;
                }
                case StrengthenKind.MissingMaterial:
                {
                    var item = Game.Items?.Get(row.TargetId);
                    if (item?.icon != null) return item.icon;
                    break;
                }
                case StrengthenKind.EvolutionReady:
                {
                    var form = Game.Content?.Characters?.GetForm(row.TargetId);
                    if (form?.portrait != null) return form.portrait;
                    break;
                }
            }
            if (_icons == null) return null;
            var feature = row.Kind switch
            {
                StrengthenKind.ShopUpgrade => FeatureId.ShopUpgrade,
                StrengthenKind.LineageLevel or StrengthenKind.ResonanceReady => FeatureId.CharacterLevelUp,
                StrengthenKind.EvolutionReady => FeatureId.Evolution,
                StrengthenKind.SynergyOneShort => FeatureId.Synergy,
                _ => FeatureId.Formation,
            };
            return _icons.Feature(feature);
        }

        void SetRowIcon(int row, Sprite sprite)
        {
            var icon = row < _rowIcons.Length ? _rowIcons[row] : null;
            if (icon == null) return;
            icon.enabled = sprite != null;
            icon.sprite = sprite;
            icon.preserveAspect = true;
        }

        // ───────────── 친구들의 응원 (§13.2) ─────────────

        /// <summary>응원 버튼 (상태가 바뀔 때만 다시 쓴다): 쓸 수 있음 · 썼음(누를 수 없음) · 숨김(튜토리얼 · 시스템 없음)</summary>
        void RefreshCheer()
        {
            if (_cheerButton == null) return;
            var cheer = MetaUi.IsActive ? Game.Cheer : null;
            var tutorial = Game.Tutorial;
            int state = cheer == null || (tutorial != null && !tutorial.IsDone) ? 0 : cheer.CanUse ? 1 : 2;
            if (state == _cheerState) return;
            _cheerState = state;
            _cheerButton.gameObject.SetActive(state != 0);
            if (_cheerSubText != null) _cheerSubText.gameObject.SetActive(state != 0);
            if (state == 0) return;
            _cheerButton.interactable = state == 1;
            if (_cheerLabel != null) _cheerLabel.text = CheerLabel;
            if (_cheerSubText != null) _cheerSubText.text = state == 1 ? CheerSubLine() : CheerUsedText;
        }

        /// <summary>'10분 동안 공격력 +20% · 이 장에서 한 번' (콘텐츠 cheer 값)</summary>
        static string CheerSubLine()
        {
            var config = Game.MetaDb != null ? Game.MetaDb.cheer : null;
            int minutes = config != null ? Math.Max(1, config.durationSeconds / 60) : 10;
            float percent = config != null ? config.attackPercent : 20f;
            return $"{minutes}분 동안 공격력 +{percent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}% · 이 장에서 한 번";
        }

        void OnCheer()
        {
            var cheer = MetaUi.IsActive ? Game.Cheer : null;
            if (cheer == null || !cheer.CanUse) return;
            if (MetaUi.BlockIfTampered()) return;
            if (!cheer.TryUse())
            {
                MetaUi.Toast("지금은 응원을 받을 수 없어요");
                return;
            }
            if (!_cheerStartId.IsValid) _cheerStartId = AudioService.Resolve(CheerStartKey);
            AudioService.Play(_cheerStartId);
            Haptics.Play(HapticKind.Light);
            MetaUi.Toast("친구들이 응원하고 있어요! 공격력 +" + (Game.MetaDb != null ? Game.MetaDb.cheer.attackPercent : 20f).ToString("0.#",
                System.Globalization.CultureInfo.InvariantCulture) + "%", ToastKind.Success);
            _cheerState = -1;
            RefreshCheer();
        }

        /// <summary>'권장 12,000 / 내 9,500  -2,500' (모자라면 빨간 차이, 넉넉하면 차이 없음)</summary>
        internal static string HeaderText(long recommended, long mine)
        {
            string text = $"권장 <b>{PowerSystem.Format(recommended)}</b>  /  내 <b>{PowerSystem.Format(mine)}</b>";
            long gap = recommended - mine;
            return gap > 0 ? $"{text}  <color={NegativeTag}>-{PowerSystem.Format(gap)}</color>" : text;
        }

        void SetRowVisible(int row, bool visible)
        {
            var go = row < _rows.Length ? _rows[row] : null;
            if (go != null && go.activeSelf != visible) go.SetActive(visible);
        }

        void SetRowText(int row, string title, string desc)
        {
            if (row < _rowTitles.Length && _rowTitles[row] != null) _rowTitles[row].text = title;
            if (row < _rowDescs.Length && _rowDescs[row] != null) _rowDescs[row].text = desc;
        }

        // ───────────── 가기 ─────────────

        void OnGo(int row)
        {
            if (!MetaUi.IsActive || row < 0 || row >= MaxRows || row >= _shownAdvice) return;
            GoAdvice(row);
        }

        /// <summary>
        /// 조언 줄 [가기 ›]: 모자란 재료는 그 재료의 첫 얻는 곳, 약한 방향은 편성 탭 + 그 칸 반짝(§7.2 고리와 같은 길), 친구 레벨은 강화 탭 + 그 친구 카드,
        /// 그 밖은 기능 이동 + 손가락 (자동 편성 줄은 자동 편성 버튼)
        /// </summary>
        void GoAdvice(int row)
        {
            var kind = _adviceKind[row];
            var nav = _adviceNav[row];
            string focus = _adviceFocus[row];
            string target = _adviceTarget[row];
            int slot = _adviceSlot[row];
            Close();
            if (kind == StrengthenKind.MissingMaterial)
            {
                var item = !string.IsNullOrEmpty(target) ? Game.Items?.Get(target) : null;
                if (item?.sources != null && item.sources.Count > 0 && item.sources[0] != null)
                {
                    UINav.GoToSource(item.sources[0]);
                    return;
                }
            }
            if (kind == StrengthenKind.WeakDirection) nav = FeatureId.Formation;
            if (kind == StrengthenKind.AutoFormation && string.IsNullOrEmpty(focus)) focus = StrengthenAdvisor.FormationAutoPath;
            if (kind == StrengthenKind.ShopUpgrade && string.IsNullOrEmpty(focus)) focus = StrengthenAdvisor.GrowthLevelUp1Path;
            if (nav == FeatureId.Battle && string.IsNullOrEmpty(focus)) return;
            if (nav != FeatureId.Battle && Game.Features != null && !Game.Features.TryUse(nav, out var locked))
            {
                MetaUi.Toast(locked);
                return;
            }
            MetaNavigator.GoAndFocus(nav, focus);
            // 강화 패널이 켜지면 그 친구 카드로 굴려 금테로 반짝인다 (계약 C-03 — 패널이 꺼져 있으면 기다렸다가)
            if (kind == StrengthenKind.LineageLevel && !string.IsNullOrEmpty(target)) GrowthPanel.FocusLineage(target);
            if (kind == StrengthenKind.WeakDirection && slot >= 0) UINav.PulseFormationSlot(slot, WeakSlotPulseSeconds);
        }
    }
}
