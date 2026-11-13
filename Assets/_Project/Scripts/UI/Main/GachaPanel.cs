using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 소환 탭 "잃어버린 친구 찾기": 배너, 별 개수별 확률 공개, 천장(남은 횟수 + 막대), 1회/10회 소환.
    /// 중복으로 만난 친구는 기억편으로 바뀐다 (계보 공명 · 희귀 재료 대체 · 진화 가속).
    ///   · 소환 버튼은 비용 버튼 세 상태 (CostButtonView, M07): 1회 = 보라(어두운 바탕), 10회 = 금. 모자라도 누를 수 있고,
    ///     누르면 흔들기 + 부족 팝업으로 보낸다 (M08 — 별조각은 ShowShortfall).
    ///   · 1회는 바로 소환한다 (튜토리얼 첫 만남 · FtueDirector의 Pull(1)). 10회 버튼은 쓰기 확인(오늘 다시 묻지 않기 confirm.gacha10)을 거친다 (M55).
    ///     Pull(int) 자체는 확인 없이 바로 소환한다 — 스모크 테스트 · AutoShot이 직접 부른다.
    ///   · 천장 줄 (M63): 윗줄 '★★★★ 확정까지 N회  n/60', 아랫줄 막대 (0이어도 바탕이 보인다). 늘면 0.35초 채움 뒤 0.25초 크림 반짝 —
    ///     소환 결과 창이 열려 있으면 미뤘다가 창이 닫힐 때 보여 준다. 천장을 채워 처음으로 돌아가면(줄어듦) 바로 맞춘다.
    ///     남은 횟수가 10 이하면 숫자가 1.2초마다 크림 ↔ 금으로 숨쉰다 (글자 정점 색만 바꿔 할당 없음).
    ///     움직임 줄이기면 막대는 바로 맞추고 반짝 · 숨쉬기 없이 숫자를 금색으로 둔다. 코루틴은 움직일 때만 돈다. 켜질 때는 바로 맞춘다.
    ///   · 별조각 얻는 곳 링크 (M08): 어느 쪽 소환이든 모자라면 '별조각 얻는 곳 ›' (받지 않은 별조각 우편이 있으면 '우편함에 별조각이 있어요 ›')
    ///     → 얻는 곳 팝업 (ShowShortfall(재화, 0)). 링크가 보이면 아래 안내 줄을 HintTopWithLink로 내리고 20px로 줄인다.
    ///   · 소리 (AUDIO-SPEC §8.1): 패널이 보이는 동안 소환 음악(MusicState.Gacha). 꾸러미 떨어지는 소리(GachaPullStart)는 결과 창 공개가 낸다 (CR-04).
    ///   · 소환 마일스톤 줄 (PRG-04 · SPEC4 §6.4, Top 718): '누적 소환 n' + 다음 단계까지 막대(GoldAccent) + 단계 점 5개 + 다음 보상 칩 + 받을 수 있으면 금 '받기'.
    ///     누르면 GachaMilestonePopup. 단계가 하나도 없으면(콘텐츠 전) 줄을 숨긴다. GachaMilestones.Changed · 켜질 때 · 소환 뒤에만 다시 그린다.
    ///     빌더는 이 줄을 넣으며 아래 안내 줄(중복 → 기억편)을 없앴다 — 그 글은 마일스톤 창 아래로 옮겼다 (M80 한 화면 안내 한 줄).
    /// 글은 값이 바뀔 때만 다시 쓴다 (단추처럼 자주 바뀌는 값은 없다 — 소환 재화가 바뀔 때만 다시 본다).
    /// 새 직렬화 필드(_pull1View · _pull10View · _pityFlash · _gemLink* · _hintText · _milestone*)는 비어 있어도 된다 (다시 빌드하기 전 씬).
    /// </summary>
    public class GachaPanel : MonoBehaviour
    {
        /// <summary>10회 소환 확인의 '오늘 다시 묻지 않기' 키 (소환 결과의 10회 더 소환도 같은 키)</summary>
        public const string Pull10SkipKey = "confirm.gacha10";
        /// <summary>남은 횟수가 이 이하면 숫자가 숨쉰다</summary>
        public const int NearPity = 10;
        /// <summary>숫자 숨쉬기 한 번 (크림 → 금 → 크림, 초)</summary>
        public const float NearPityPulse = 1.2f;

        // 안내 줄 자리 (CharacterSceneBuilder.BuildGachaPanel과 같은 값 — 패널 위에서부터, 좌우 40)
        /// <summary>안내 줄 위치 (링크 없음): Top(688, 76), 22px</summary>
        public const float HintTop = 688f;
        public const float HintHeight = 76f;
        public const float HintFontSize = 22f;
        /// <summary>안내 줄 위치 (별조각 링크가 보일 때): Top(718, 58), 20px</summary>
        public const float HintTopWithLink = 718f;
        public const float HintHeightWithLink = 58f;
        public const float HintFontSizeWithLink = 20f;
        public const float HintSideInset = 40f;

        [SerializeField] GachaTable _table;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] SparkleBurst _bannerSparkles;
        [SerializeField] TMP_Text[] _rateTexts;
        [SerializeField] TMP_Text _pityText;
        [SerializeField] RectTransform _pityFill;
        [Tooltip("천장 막대 채움 위 크림 반짝 (채움의 자식, 알파 0으로 둔다)")]
        [SerializeField] Graphic _pityFlash;
        [SerializeField] Button _pull1Button;
        [SerializeField] Button _pull10Button;
        [SerializeField] TMP_Text _cost1Text;
        [SerializeField] TMP_Text _cost10Text;
        [SerializeField] TMP_Text _discountText;
        [Tooltip("1회 소환 비용 버튼 세 상태 (어두운 바탕)")]
        [SerializeField] CostButtonView _pull1View;
        [Tooltip("10회 소환 비용 버튼 세 상태 (금 바탕)")]
        [SerializeField] CostButtonView _pull10View;
        [Tooltip("'별조각 얻는 곳 ›' 링크 (어느 쪽 소환이든 모자랄 때만 켠다)")]
        [SerializeField] Button _gemLinkButton;
        [SerializeField] TMP_Text _gemLinkLabel;
        [Tooltip("중복 → 기억편 안내 줄 (링크가 보이면 아래로 내리고 작게 — 마일스톤 줄이 있는 씬에는 없다)")]
        [SerializeField] TMP_Text _hintText;

        [Header("소환 마일스톤 (PRG-04 — 없어도 됨)")]
        [Tooltip("줄 전체 (누르면 마일스톤 창)")]
        [SerializeField] Button _milestoneRow;
        [SerializeField] TMP_Text _milestoneText;
        [Tooltip("막대 채움 (UiArt.Bar)")]
        [SerializeField] RectTransform _milestoneFill;
        [Tooltip("단계 점 (막대의 자식, 가로 앵커로 자리를 잡는다)")]
        [SerializeField] Image[] _milestonePips;
        [SerializeField] MetaRewardChip _milestoneReward;
        [Tooltip("금 '받기' 알약 (받을 단계가 있을 때만)")]
        [SerializeField] GameObject _milestoneClaim;

        bool _started;
        bool _staticShown;
        GachaResultPopup _result;
        bool _resultSubscribed;

        int _shownRemain = int.MinValue;
        int _shownCount = int.MinValue;
        bool _shownReduced;
        /// <summary>막대가 지금 보여 주는 값 (0~1, 음수 = 아직 없음 → 다음에 바로 맞춘다)</summary>
        float _pityShown = -1f;
        float _pityTarget;
        /// <summary>결과 창이 닫힐 때 보여 줄 늘어남이 있다</summary>
        bool _pityPending;
        Coroutine _pityRoutine;
        Coroutine _pulseRoutine;
        /// <summary>천장 줄에서 남은 횟수 숫자의 글자 번호 (처음 · 끝). 글을 바꿀 때마다 다시 찾는다 (없으면 -1)</summary>
        int _remainFirst = -1;
        int _remainLast = -1;

        int _linkState = -1;

        readonly List<GachaMilestoneStep> _steps = new();
        bool _milestonesSubscribed;

        void Start()
        {
            _pull1Button.onClick.AddListener(OnPull1Clicked);
            _pull10Button.onClick.AddListener(OnPull10Clicked);
            if (_gemLinkButton != null) _gemLinkButton.onClick.AddListener(OpenGemSources);
            if (_milestoneRow != null)
            {
                _milestoneRow.onClick.AddListener(OpenMilestones);
                if (Game.GachaMilestones != null)
                {
                    Game.GachaMilestones.Changed += OnMilestonesChanged;
                    _milestonesSubscribed = true;
                }
            }
            Game.Currency.Changed += OnCurrencyChanged;
            if (UIManager.Instance != null && UIManager.Instance.TryGet(out _result))
            {
                _result.Closed += OnResultClosed;
                _resultSubscribed = true;
            }
            _started = true;
            Refresh();
        }

        void OnEnable()
        {
            AudioService.PushMusic(MusicState.Gacha, GetInstanceID());
            if (_bannerSparkles != null) _bannerSparkles.SetAmbient(true, UITheme.Cream);
            if (!_started) return;
            // 켜질 때는 막대를 바로 맞춘다 (꺼져 있는 동안 바뀐 값을 그 자리에서 다시 채우지 않게)
            _pityShown = -1f;
            Refresh();
        }

        void OnDisable()
        {
            AudioService.PopMusic(GetInstanceID());
            // 코루틴은 꺼지며 멈췄다 — 다음에 켜질 때 바로 맞춘다
            _pityRoutine = null;
            _pulseRoutine = null;
            _pityPending = false;
            SetFlash(0f);
        }

        void OnDestroy()
        {
            if (_started && Game.IsReady) Game.Currency.Changed -= OnCurrencyChanged;
            if (_resultSubscribed && _result != null) _result.Closed -= OnResultClosed;
            if (_milestonesSubscribed && Game.GachaMilestones != null) Game.GachaMilestones.Changed -= OnMilestonesChanged;
        }

        void OnMilestonesChanged() => RefreshMilestones();

        void OnCurrencyChanged(CurrencyType type, double value)
        {
            if (type == _table.costCurrency) Refresh();
        }

        // ───────────── 소환 ─────────────

        void OnPull1Clicked()
        {
            if (!Game.Gacha.CanAfford(_table, 1))
            {
                Deny(_pull1View, 1);
                return;
            }
            Pull(1);
        }

        /// <summary>10회 버튼: 모자라면 흔들기 + 부족 팝업, 넉넉하면 쓰기 확인 (오늘 다시 묻지 않기) 뒤 Pull(10)</summary>
        void OnPull10Clicked()
        {
            if (!Game.Gacha.CanAfford(_table, 10))
            {
                Deny(_pull10View, 10);
                return;
            }
            var ui = UIManager.Instance;
            if (ui == null)
            {
                Pull(10);
                return;
            }
            ui.ShowSpendConfirm("10회 소환", $"{_table.displayName}에서 10번 소환할까요?", _table.costCurrency,
                Game.Gacha.GetCost(_table, 10), "소환", () => Pull(10), Pull10SkipKey);
        }

        void Deny(CostButtonView view, int count)
        {
            if (view != null) view.Deny();
            if (UIManager.Instance != null) UIManager.Instance.ShowShortfall(_table.costCurrency, Game.Gacha.GetCost(_table, count));
        }

        /// <summary>
        /// 바로 소환하고 결과 창을 연다 (확인 없음 — 10회 확인은 버튼만 묻는다). 모자라면 부족 안내(ShowShortfall)만.
        /// 결과 창의 'N회 더 소환'은 이 표 · 횟수를 받아 10회면 다시 확인을 거친다.
        /// </summary>
        public void Pull(int count)
        {
            if (!Game.Gacha.TryPull(_table, count, out var results))
            {
                var ui = UIManager.Instance;
                if (ui != null) ui.ShowShortfall(_table.costCurrency, Game.Gacha.GetCost(_table, count));
                return;
            }
            string cost = RewardService.FormatAmount(_table.costCurrency, Game.Gacha.GetCost(_table, count));
            UIManager.Instance.Get<GachaResultPopup>().Show(results, $"{count}회 더 소환", cost, Game.Gacha.CanAfford(_table, count), () => Pull(count),
                _table, count);
            Refresh();
        }

        void OpenGemSources()
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowShortfall(_table.costCurrency, 0d);
        }

        // ───────────── 그리기 ─────────────

        void Refresh()
        {
            if (!_started || !isActiveAndEnabled) return;
            if (!_staticShown) ShowStatic();

            RefreshPity();
            bool can1 = Game.Gacha.CanAfford(_table, 1);
            bool can10 = Game.Gacha.CanAfford(_table, 10);
            SetAffordable(_pull1View, _cost1Text, can1, CostSurface.Dark);
            SetAffordable(_pull10View, _cost10Text, can10, CostSurface.Gold);
            RefreshGemLink(!can1 || !can10);
            RefreshMilestones();
        }

        /// <summary>표에서 정해지는 것 (제목 · 확률 · 비용 · 할인) — 처음 한 번</summary>
        void ShowStatic()
        {
            _staticShown = true;
            _titleText.text = _table.displayName;
            RefreshRates();
            _cost1Text.text = RewardService.FormatAmount(_table.costCurrency, Game.Gacha.GetCost(_table, 1));
            _cost10Text.text = RewardService.FormatAmount(_table.costCurrency, Game.Gacha.GetCost(_table, 10));

            double full = Game.Gacha.GetCost(_table, 1) * 10;
            double ten = Game.Gacha.GetCost(_table, 10);
            bool discounted = full > 0 && ten < full;
            _discountText.transform.parent.gameObject.SetActive(discounted);
            if (discounted) _discountText.text = $"{(1.0 - ten / full) * 100.0:0}% 할인";
        }

        void RefreshRates()
        {
            var rates = Game.Gacha.GetRates(_table);
            for (int i = 0; i < _rateTexts.Length; i++)
            {
                bool visible = i < rates.Count;
                _rateTexts[i].transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;
                var rate = rates[i];
                _rateTexts[i].text = $"<color={UITheme.ColorTag(UITheme.GradeColor(rate.Grade))}>{UITheme.Stars(rate.Grade)}</color> {rate.Percent:0.##}%";
            }
        }

        /// <summary>비용 버튼 세 상태 (다시 빌드하기 전 씬이면 예전처럼 비용 글자 색만)</summary>
        static void SetAffordable(CostButtonView view, TMP_Text costText, bool affordable, CostSurface surface)
        {
            if (view != null)
            {
                view.SetAffordable(affordable);
                return;
            }
            var color = affordable ? UITheme.CostAffordable(surface) : UITheme.Negative;
            if (costText.color != color) costText.color = color;
        }

        // ───────────── 별조각 얻는 곳 (M08) ─────────────

        /// <summary>링크 켜고 끄기 + 글 (우편 / 얻는 곳) + 안내 줄 자리. 상태가 바뀔 때만 쓴다</summary>
        void RefreshGemLink(bool show)
        {
            if (_gemLinkButton == null) return;
            bool mail = show && CurrencySources.UnclaimedMailAmount(_table.costCurrency) > 0d;
            int state = !show ? 0 : mail ? 2 : 1;
            if (state == _linkState) return;
            _linkState = state;

            _gemLinkButton.gameObject.SetActive(show);
            if (show && _gemLinkLabel != null) _gemLinkLabel.text = GemLinkText(RewardService.CurrencyName(_table.costCurrency), mail);
            if (_hintText == null) return;
            var rt = _hintText.rectTransform;
            float top = show ? HintTopWithLink : HintTop;
            float height = show ? HintHeightWithLink : HintHeight;
            rt.offsetMin = new Vector2(HintSideInset, -top - height);
            rt.offsetMax = new Vector2(-HintSideInset, -top);
            _hintText.fontSize = show ? HintFontSizeWithLink : HintFontSize;
        }

        /// <summary>링크 글: '별조각 얻는 곳 ›' / 받지 않은 우편이 있으면 '우편함에 별조각이 있어요 ›'</summary>
        internal static string GemLinkText(string currencyName, bool unclaimedMail) =>
            unclaimedMail ? $"우편함에 {KoreanText.WithSubject(currencyName)} 있어요 ›" : $"{currencyName} 얻는 곳 ›";

        // ───────────── 소환 마일스톤 (PRG-04) ─────────────

        /// <summary>마일스톤 줄 글: '누적 소환 n'</summary>
        internal static string MilestoneLine(int pulls) => $"누적 소환 {System.Math.Max(0, pulls):N0}";

        /// <summary>
        /// 막대가 보여 줄 단계 창: 처음으로 받지 않은 단계부터 pipCount개 (모두 받았으면 마지막 하나). 막대 시작 = 그 앞 단계의 소환 수 (없으면 0).
        /// first = 창의 첫 단계 번호, last = 마지막 단계 번호 (steps가 비면 둘 다 -1).
        /// </summary>
        internal static void MilestoneWindow(IReadOnlyList<GachaMilestoneStep> steps, int pipCount, out int first, out int last, out int from)
        {
            first = last = -1;
            from = 0;
            if (steps == null || steps.Count == 0) return;
            first = steps.Count - 1;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && !steps[i].Claimed)
                {
                    first = i;
                    break;
                }
            }
            last = System.Math.Min(steps.Count - 1, first + System.Math.Max(1, pipCount) - 1);
            from = first > 0 && steps[first - 1] != null ? steps[first - 1].Pulls : 0;
        }

        /// <summary>막대 채움 비율 (from → 창 마지막 단계의 소환 수)</summary>
        internal static float MilestoneFill(int pulls, int from, int to) => to > from ? Mathf.Clamp01((float)(pulls - from) / (to - from)) : (pulls >= to ? 1f : 0f);

        void RefreshMilestones()
        {
            if (_milestoneRow == null || !_started) return;
            var milestones = MetaUi.IsActive ? Game.GachaMilestones : null;
            if (milestones != null) milestones.GetSteps(_steps, 2);
            else _steps.Clear();
            bool show = _steps.Count > 0;
            if (_milestoneRow.gameObject.activeSelf != show) _milestoneRow.gameObject.SetActive(show);
            if (!show) return;

            int pulls = milestones.Pulls;
            if (_milestoneText != null) _milestoneText.text = MilestoneLine(pulls);
            int pipCount = _milestonePips != null && _milestonePips.Length > 0 ? _milestonePips.Length : 5;
            MilestoneWindow(_steps, pipCount, out int first, out int last, out int from);
            var lastStep = _steps[last];
            int to = lastStep != null ? lastStep.Pulls : from;
            if (_milestoneFill != null) CharacterUI.SetFill(_milestoneFill, MilestoneFill(pulls, from, to));
            if (_milestonePips != null)
            {
                for (int i = 0; i < _milestonePips.Length; i++)
                {
                    var pip = _milestonePips[i];
                    if (pip == null) continue;
                    int index = first + i;
                    var step = index <= last ? _steps[index] : null;
                    bool on = step != null;
                    if (pip.gameObject.activeSelf != on) pip.gameObject.SetActive(on);
                    if (!on) continue;
                    var rect = pip.rectTransform;
                    float x = MilestoneFill(step.Pulls, from, to);
                    rect.anchorMin = new Vector2(x, rect.anchorMin.y);
                    rect.anchorMax = new Vector2(x, rect.anchorMax.y);
                    pip.color = step.Reached ? UITheme.Gold : UITheme.WithAlpha(UITheme.Cream, 0.45f);
                }
            }
            var next = _steps[first];
            if (_milestoneReward != null)
            {
                var reward = next != null && next.Rewards != null && next.Rewards.Count > 0 ? next.Rewards[0] : null;
                if (reward != null) _milestoneReward.Set(reward);
                else if (_milestoneReward.gameObject.activeSelf) _milestoneReward.gameObject.SetActive(false);
            }
            if (_milestoneClaim != null)
            {
                bool claim = milestones.HasClaimable;
                if (_milestoneClaim.activeSelf != claim) _milestoneClaim.SetActive(claim);
            }
        }

        void OpenMilestones()
        {
            var ui = UIManager.Instance;
            if (ui != null && ui.TryGet(out GachaMilestonePopup popup)) popup.Show();
        }

        // ───────────── 천장 (M63) ─────────────

        void RefreshPity()
        {
            int remain = Game.Gacha.GetRemainToPity(_table);
            bool hasPity = remain > 0 && _table.pityCount > 0;
            var track = _pityFill.parent.gameObject;
            if (track.activeSelf != hasPity) track.SetActive(hasPity);
            if (!hasPity)
            {
                if (_pityText.text.Length > 0) _pityText.text = string.Empty;
                _shownRemain = int.MinValue;
                StopPulse();
                return;
            }

            int count = Game.Gacha.GetPityCount(_table);
            bool reduced = UIMotion.Reduced;
            if (remain != _shownRemain || count != _shownCount || reduced != _shownReduced)
            {
                _shownRemain = remain;
                _shownCount = count;
                _shownReduced = reduced;
                _pityText.text = PityLine(_table.pityGrade, remain, count, _table.pityCount, reduced);
                _remainFirst = _remainLast = -1;
            }
            SetPityTarget(Mathf.Clamp01((float)count / _table.pityCount));
            if (remain <= NearPity && !reduced) StartPulse();
            else StopPulse();
        }

        /// <summary>
        /// 천장 윗줄: '★★★★ 확정까지 <b>N</b>회  <color=SubText><size=85%>n/60</size></color>'.
        /// 움직임 줄이기 + 남은 횟수 10 이하면 숫자를 금색 태그로 (숨쉬기 대신).
        /// </summary>
        internal static string PityLine(ItemGrade grade, int remain, int count, int pityCount, bool goldRemain)
        {
            string stars = $"<color={UITheme.ColorTag(UITheme.GradeColor(grade))}>{UITheme.Stars(grade)}</color>";
            string number = goldRemain && remain <= NearPity ? $"<color={UITheme.ColorTag(UITheme.Gold)}>{remain}</color>" : remain.ToString();
            return $"{stars} 확정까지 <b>{number}</b>회  <color={UITheme.ColorTag(UITheme.SubText)}><size=85%>{count}/{pityCount}</size></color>";
        }

        bool ResultOpen => _result != null && _result.IsOpen;

        void SetPityTarget(float target)
        {
            _pityTarget = target;
            // 처음 · 켜진 직후 · 천장을 넘겨 줄어듦 → 바로
            if (_pityShown < 0f || target < _pityShown - 0.0001f)
            {
                _pityPending = false;
                SnapPity(target);
                return;
            }
            if (target <= _pityShown + 0.0001f) return;
            if (UIMotion.Reduced || !isActiveAndEnabled)
            {
                SnapPity(target);
                return;
            }
            // 결과 창 뒤에서 차오르면 보이지 않는다 → 창이 닫힐 때
            if (ResultOpen)
            {
                _pityPending = true;
                return;
            }
            StartPityTween(0f);
        }

        void OnResultClosed()
        {
            if (!_pityPending) return;
            _pityPending = false;
            if (!isActiveAndEnabled || UIMotion.Reduced || !(_pityTarget > _pityShown)) SnapPity(_pityTarget);
            // 창이 사라지는 동안(0.12초)은 기다렸다가 채운다
            else StartPityTween(UIMotion.PopupOut);
        }

        void StartPityTween(float delay)
        {
            if (_pityRoutine != null) StopCoroutine(_pityRoutine);
            _pityRoutine = StartCoroutine(PityTween(Mathf.Max(0f, _pityShown), _pityTarget, delay));
        }

        /// <summary>0.35초 OutCubic으로 채운 뒤 0.25초 크림 반짝 (α 0.6 → 0)</summary>
        IEnumerator PityTween(float from, float to, float delay)
        {
            for (float d = 0f; d < delay; d += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < UIMotion.PityFill; t += Time.unscaledDeltaTime)
            {
                ApplyPity(Mathf.Lerp(from, to, UIMotion.OutCubic(t / UIMotion.PityFill)));
                yield return null;
            }
            ApplyPity(to);
            for (float t = 0f; t < UIMotion.PityFlash; t += Time.unscaledDeltaTime)
            {
                SetFlash(0.6f * (1f - t / UIMotion.PityFlash));
                yield return null;
            }
            SetFlash(0f);
            _pityRoutine = null;
        }

        void SnapPity(float value)
        {
            if (_pityRoutine != null) StopCoroutine(_pityRoutine);
            _pityRoutine = null;
            ApplyPity(value);
            SetFlash(0f);
        }

        void ApplyPity(float value)
        {
            _pityShown = value;
            CharacterUI.SetFill(_pityFill, value);
        }

        void SetFlash(float alpha)
        {
            if (_pityFlash == null) return;
            bool on = alpha > 0.001f;
            if (_pityFlash.enabled != on) _pityFlash.enabled = on;
            if (!on) return;
            var color = _pityFlash.color;
            color.a = alpha;
            _pityFlash.color = color;
        }

        // ───────────── 천장 가까움 숨쉬기 ─────────────

        void StartPulse()
        {
            if (_pulseRoutine != null || !isActiveAndEnabled) return;
            _pulseRoutine = StartCoroutine(PulseRemain());
        }

        void StopPulse()
        {
            if (_pulseRoutine == null) return;
            StopCoroutine(_pulseRoutine);
            _pulseRoutine = null;
            RestoreRemainColors();
        }

        /// <summary>
        /// 숨쉬기가 덮어쓴 숫자 정점 색을 글 그대로(색 태그 포함 — 움직임 줄이기면 금색)로 되돌린다: 메시를 글에서 다시 만든다 (멈출 때 한 번).
        /// 크림으로 칠하면 방금 쓴 금색 태그 숫자가 TMP가 다시 그릴 때까지 크림으로 남는다.
        /// </summary>
        void RestoreRemainColors()
        {
            if (_pityText != null && _pityText.isActiveAndEnabled) _pityText.ForceMeshUpdate();
        }

        IEnumerator PulseRemain()
        {
            while (true)
            {
                if (UIMotion.Reduced)
                {
                    // 시트가 열린 채 '움직임 줄이기'를 켜면: 숨쉬기를 멈추고 천장 줄을 금색 숫자로 다시 쓴다 (RefreshPity가 줄이기를 보고 글을 바꾼다)
                    _pulseRoutine = null;
                    RefreshPity();
                    yield break;
                }
                float k = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / NearPityPulse));
                TintRemain(Color.Lerp(UITheme.Cream, UITheme.Gold, k));
                yield return null;
            }
        }

        /// <summary>남은 횟수 숫자의 정점 색만 바꾼다 (글을 다시 쓰지 않아 할당 없음). 글이 바뀌면 숫자 자리를 한 번 다시 찾는다</summary>
        void TintRemain(Color color)
        {
            var text = _pityText;
            if (text == null || !text.isActiveAndEnabled) return;
            if (_remainFirst < 0)
            {
                text.ForceMeshUpdate();
                FindRemainRange(text.textInfo);
                if (_remainFirst < 0) return;
            }
            var info = text.textInfo;
            Color32 tint = color;
            bool any = false;
            for (int i = _remainFirst; i <= _remainLast && i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var colors = info.meshInfo[ch.materialReferenceIndex].colors32;
                int v = ch.vertexIndex;
                if (colors == null || v + 3 >= colors.Length) continue;
                colors[v] = tint;
                colors[v + 1] = tint;
                colors[v + 2] = tint;
                colors[v + 3] = tint;
                any = true;
            }
            if (any) text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary>천장 줄에서 처음 나오는 숫자 묶음 = 남은 횟수 (별은 글자 속 그림이라 숫자가 아니다)</summary>
        void FindRemainRange(TMP_TextInfo info)
        {
            _remainFirst = _remainLast = -1;
            if (info == null) return;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                bool digit = ch.elementType == TMP_TextElementType.Character && ch.character >= '0' && ch.character <= '9';
                if (digit)
                {
                    if (_remainFirst < 0) _remainFirst = i;
                    _remainLast = i;
                }
                else if (_remainFirst >= 0) break;
            }
        }
    }
}
