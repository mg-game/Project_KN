using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 짧은 상단 바 (UX-SPEC-IA §3.1, 높이 130 — 전투 화면을 크게 보이게): 수선점 레벨 · 전투력 칩 · 단추 / 별조각 칩 · 초당 단추 · 우편 · ≡ 메뉴.
    ///   · 전투력 칩(PowerChip, 버튼)은 TopBarMetaView 몫이다: 숫자(PowerChip/PowerText — 세어 오르기 · 튀기기)와 누름(권장 전투력 토스트, M10). 이 뷰는 듣지 않는다.
    ///   · 초당 단추는 '초당 +{rate}' (§1.10: 1,000 미만은 소수 1~2자리, 그 위는 FormatCurrency). 레벨 · 초당 단추 글은 값이 바뀔 때만 다시 쓴다.
    ///   · 우편 = 숫자 배지 (우편 수). ≡ 메뉴 = MenuPopup (일지 · 출석 · 이벤트 · 가방 · 공지 · 설정) — 메뉴 점은 MenuButton의 RedDotView(top.settings)가 따로 켠다.
    ///   · 재화 칩 누르기 (M08): 단추 칩 → 단추 얻는 곳, 별조각 칩 → '별조각 얻는 곳' (둘 다 UIManager.ShowShortfall(재화, 0) — 얻는 곳 모드).
    ///     0.35초 길게 누르면 칩의 CurrencyStripView가 재화 안내 말풍선을 띄우고 그 누름의 클릭은 막는다 (팝업과 겹치지 않게).
    ///   · 재화 숫자 맞추기 (M21, §3.12): 보이는 값 = max(0, 보유량 − RewardFlyer.PendingOf − RewardPopup.HeldOf) — 날아오는 아이콘이 닿을 때마다 함께 오른다.
    ///     보상 팝업이 열려 있는 동안 확인 때 날릴 양은 붙잡아 둔다 (받자마자 오르고 → 확인 때 내려갔다 → 다시 오르는 두 번 셈 없이, 확인 뒤 아이콘과 한 번만).
    ///     1%(100 미만은 1)보다 크게 바뀌거나 떠 있는 양이 바뀌면(PendingChanged) 지금 보이는 값에서 0.35초 OutCubic으로 세어 오르고,
    ///     그보다 작으면(지나가며 쌓이는 단추) 바로 맞춘다. 1% 넘게 줄면 0.20초에 내려가며 글자가 Negative → 원래 색으로 0.30초 돌아온다.
    ///     움직임 줄이기면 바로 맞춘다. 글은 화면에 보이는 숫자(표시 키)가 바뀔 때만 다시 쓴다. 칩마다 세는 동안만 코루틴이 돈다 (Update 없음).
    ///     게임이 꺼진 뒤(장면을 다시 불러오거나 끝내는 중) 오는 떠 있는 양 · 붙잡음 신호는 무시한다 (보유량을 읽을 곳이 없다 — Wave 3 r1 스모크).
    ///   · '초당 +N' 비켜 주기 (Wave 2 r2): 단추가 날아오는 동안(RewardFlyer.PendingOf(Gold) > 0) 단추 칩 아래 끝의 "+N" 받침이 이 줄을 덮으므로
    ///     CanvasGroup α를 0.12초에 0으로 내리고, 떠 있는 양이 0이 된 뒤 1.1초(받침 머묾 0.75 + 사라짐 0.3) 지나 0.12초에 되돌린다.
    ///     움직임 줄이기면 α만 바로 바꾼다 (기다림은 같다). 그동안만 코루틴 하나가 돌고 할당은 없다.
    /// 가방 · 공지 · 설정 버튼과 별빛 사탕 칩은 상단 바에 없다 (≡ 메뉴와 전투 화면 빠른 아이콘으로 옮겼다 — 사탕 잔액은 상점 이벤트 탭).
    /// 버튼 필드는 비어 있어도 된다 (다시 빌드하기 전 씬).
    /// </summary>
    public class TopBarView : MonoBehaviour
    {
        /// <summary>이만큼(비율) 넘게 바뀌면 세어 오르기 (100 미만은 1보다 크게)</summary>
        const double BigChangeRatio = 0.01;
        const double SmallBasis = 100;
        /// <summary>'초당 +N' 흐려짐 · 되돌림 시간 (초)</summary>
        internal const float PerSecFadeTime = 0.12f;
        /// <summary>단추 떠 있는 양이 0이 된 뒤 '초당 +N'을 되돌리기까지 (RewardFlyer 받침 머묾 0.75 + 사라짐 0.3 + 여유)</summary>
        internal const float PerSecRestoreDelay = 1.1f;

        [SerializeField] TMP_Text _levelText;
        [SerializeField] TMP_Text _goldPerSecText;
        [SerializeField] TMP_Text _goldText;
        [SerializeField] TMP_Text _gemText;
        [SerializeField] Button _mailButton;
        [Tooltip("≡ 메뉴 버튼 (MenuPopup을 연다)")]
        [SerializeField] Button _menuButton;
        [SerializeField] Badge _mailBadge;
        [Tooltip("단추 칩 버튼 (누르면 단추 얻는 곳 — M08). 길게 누르면 칩의 안내 말풍선")]
        [SerializeField] Button _goldChipButton;
        [Tooltip("별조각 칩 버튼 (누르면 '별조각 얻는 곳' — M08). 길게 누르면 칩의 안내 말풍선")]
        [SerializeField] Button _gemChipButton;

        /// <summary>칩 하나의 숫자 맞추기 상태 (보이는 값 · 마지막으로 쓴 표시 키 · 세는 중인 구간)</summary>
        sealed class Counter
        {
            public CurrencyType Type;
            public TMP_Text Label;
            public Color BaseColor;
            /// <summary>지금 보이는 값 (세는 중이면 그 중간값)</summary>
            public double Shown;
            /// <summary>마지막으로 쓴 표시 키 (같으면 글을 다시 쓰지 않는다)</summary>
            public double Key = double.NaN;
            public double From;
            public double To;
            public float Elapsed;
            public float Duration;
            /// <summary>줄어듦 색 (Negative → 원래 색) 시간, 0이면 색 없음</summary>
            public float TintDuration;
            public Coroutine Routine;
        }

        readonly Counter _gold = new() { Type = CurrencyType.Gold };
        readonly Counter _gem = new() { Type = CurrencyType.Gem };
        bool _started;

        /// <summary>마지막으로 쓴 수선점 레벨 · 초당 단추 (같으면 다시 쓰지 않는다)</summary>
        int _shownLevel = -1;
        double _shownRate = double.NaN;

        /// <summary>'초당 +N' 흐림 (Start에서 붙인다 — 없으면 비켜 주지 않는다)</summary>
        CanvasGroup _perSecGroup;
        Coroutine _perSecRoutine;
        /// <summary>단추 떠 있는 양이 0이 된 시각 (아직 떠 있으면 −1)</summary>
        float _perSecClearAt = -1f;

        void Start()
        {
            if (_mailButton != null) _mailButton.onClick.AddListener(OpenMail);
            if (_menuButton != null) _menuButton.onClick.AddListener(OpenMenu);
            if (_goldChipButton != null) _goldChipButton.onClick.AddListener(OpenGoldSources);
            if (_gemChipButton != null) _gemChipButton.onClick.AddListener(OpenGemSources);

            _gold.Label = _goldText;
            _gem.Label = _gemText;
            InitCounter(_gold);
            InitCounter(_gem);
            if (_goldPerSecText != null && !_goldPerSecText.TryGetComponent(out _perSecGroup))
            {
                _perSecGroup = _goldPerSecText.gameObject.AddComponent<CanvasGroup>();
                _perSecGroup.blocksRaycasts = false;
                _perSecGroup.interactable = false;
            }

            Game.Currency.Changed += OnCurrencyChanged;
            RewardFlyer.PendingChanged += OnPendingChanged;
            RewardPopup.HeldChanged += OnHeldChanged;
            Game.Growth.LevelChanged += OnLevelChanged;
            Game.Inventory.Changed += RefreshStats;
            // 초당 단추는 도달한 최고 스테이지에도 비례한다 (최고 스테이지는 다음 전투 시작 전에 갱신된다)
            Game.Battle.StageChanged += RefreshStats;
            Game.Mail.Changed += RefreshMailBadge;
            _started = true;

            RefreshStats();
            RefreshMailBadge();
        }

        void OnDisable()
        {
            // 꺼지면 코루틴이 멈춘다: 세던 칩은 목표값으로 바로 맞춰 둔다
            Snap(_gold);
            Snap(_gem);
            // 비켜 주던 '초당 +N'도 바로 되돌린다 (다시 켜질 때 받침은 이미 사라졌다)
            _perSecRoutine = null;
            _perSecClearAt = -1f;
            if (_perSecGroup != null) _perSecGroup.alpha = 1f;
        }

        void OnDestroy()
        {
            RewardFlyer.PendingChanged -= OnPendingChanged;
            RewardPopup.HeldChanged -= OnHeldChanged;
            if (!_started || !Game.IsReady) return;
            Game.Currency.Changed -= OnCurrencyChanged;
            Game.Growth.LevelChanged -= OnLevelChanged;
            Game.Inventory.Changed -= RefreshStats;
            Game.Battle.StageChanged -= RefreshStats;
            Game.Mail.Changed -= RefreshMailBadge;
        }

        // ───────────── 버튼 ─────────────

        static void OpenMail()
        {
            if (TryGetPopup<MailPopup>(out var mail)) mail.Open();
        }

        static void OpenMenu()
        {
            if (TryGetPopup<MenuPopup>(out var menu)) menu.Open();
        }

        /// <summary>단추 칩: 단추 얻는 곳 (바구니 · 스테이지 · 원정 …, 얻는 곳 모드)</summary>
        static void OpenGoldSources()
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowShortfall(CurrencyType.Gold, 0d);
        }

        /// <summary>별조각 칩: '별조각 얻는 곳' (안 받은 별조각 우편이 있으면 그 줄이 맨 위)</summary>
        static void OpenGemSources()
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowShortfall(CurrencyType.Gem, 0d);
        }

        static bool TryGetPopup<T>(out T popup) where T : UIPopup
        {
            popup = null;
            var ui = UIManager.Instance;
            return ui != null && ui.TryGet(out popup);
        }

        // ───────────── 재화 (M21) ─────────────

        void InitCounter(Counter counter)
        {
            if (counter.Label == null) return;
            counter.BaseColor = counter.Label.color;
            counter.Shown = ShownTarget(counter.Type, 0d);
            counter.To = counter.Shown;
            Write(counter, counter.Shown);
        }

        void OnCurrencyChanged(CurrencyType type, double value)
        {
            var counter = CounterOf(type);
            if (counter != null) Retarget(counter, false);
        }

        void OnPendingChanged(CurrencyType type)
        {
            // 장면이 내려가는 중(게임이 이미 꺼짐 — RewardFlyer가 층을 끄며 떠 있는 양을 비움)이면 맞출 보유량이 없다
            if (!_started || !Game.IsReady || Game.Currency == null) return;
            var counter = CounterOf(type);
            if (counter != null) Retarget(counter, true);
            if (type == CurrencyType.Gold) KickPerSecFade();
        }

        // ───────────── '초당 +N' 비켜 주기 (단추 "+N" 받침 밑) ─────────────

        /// <summary>단추 떠 있는 양이 바뀜: 흐림 코루틴이 돌고 있지 않으면 하나 띄운다 (돌고 있으면 그 코루틴이 다음 프레임에 읽는다)</summary>
        void KickPerSecFade()
        {
            if (_perSecGroup == null || _perSecRoutine != null) return;
            bool pending = RewardFlyer.PendingOf(CurrencyType.Gold) > 0d;
            if (!pending && _perSecGroup.alpha >= 1f) return;
            if (!isActiveAndEnabled)
            {
                _perSecGroup.alpha = 1f;
                return;
            }
            _perSecRoutine = StartCoroutine(PerSecFade());
        }

        IEnumerator PerSecFade()
        {
            while (true)
            {
                float now = Time.unscaledTime;
                bool pending = RewardFlyer.PendingOf(CurrencyType.Gold) > 0d;
                if (pending) _perSecClearAt = -1f;
                else if (_perSecClearAt < 0f) _perSecClearAt = now;
                float since = pending ? 0f : now - _perSecClearAt;
                float alpha = PerSecAlphaStep(_perSecGroup.alpha, pending, since, Time.unscaledDeltaTime, UIMotion.Reduced);
                if (_perSecGroup.alpha != alpha) _perSecGroup.alpha = alpha;
                if (!pending && alpha >= 1f) break;
                yield return null;
            }
            _perSecClearAt = -1f;
            _perSecRoutine = null;
        }

        /// <summary>'초당 +N' 목표 α: 단추가 떠 있거나, 0이 된 뒤 PerSecRestoreDelay가 아직 안 지났으면 0, 아니면 1</summary>
        internal static float PerSecTarget(bool pending, float sinceClear) => pending || sinceClear < PerSecRestoreDelay ? 0f : 1f;

        /// <summary>한 프레임 α (PerSecFadeTime에 0 ↔ 1을 오간다 — 움직임 줄이기면 바로 목표로)</summary>
        internal static float PerSecAlphaStep(float alpha, bool pending, float sinceClear, float dt, bool reduced)
        {
            float target = PerSecTarget(pending, sinceClear);
            return reduced ? target : Mathf.MoveTowards(alpha, target, dt / PerSecFadeTime);
        }

        /// <summary>보상 팝업이 붙잡은 양이 바뀌었다 (열림 · 확인 · 날리지 않고 닫힘) — 떠 있는 양 변화처럼 바로 다시 맞춘다</summary>
        void OnHeldChanged()
        {
            if (!_started || !Game.IsReady || Game.Currency == null) return;
            Retarget(_gold, true);
            Retarget(_gem, true);
        }

        Counter CounterOf(CurrencyType type) => type switch
        {
            CurrencyType.Gold => _gold,
            CurrencyType.Gem => _gem,
            _ => null,
        };

        /// <summary>
        /// 보이는 목표 = 보유량 − 아직 날아오는 양 − 보상 팝업이 확인 때 날릴 양 (0 아래로 내려가지 않는다).
        /// 게임이 꺼졌으면(장면이 내려가는 중) 지금 보이는 값 그대로 (shown).
        /// </summary>
        static double ShownTarget(CurrencyType type, double shown)
        {
            var currency = Game.IsReady ? Game.Currency : null;
            if (currency == null) return shown;
            return Math.Max(0d, currency.Get(type) - RewardFlyer.PendingOf(type) - RewardPopup.HeldOf(type));
        }

        /// <summary>
        /// 목표가 바뀌었다. 세는 중에 작은 변화(지나가는 단추)가 오면 끝값만 옮긴다 (매 프레임 다시 시작하면 끝나지 않는다).
        /// 큰 변화 · 떠 있는 양 변화(force)면 지금 보이는 값에서 다시 센다. 1% 미만 줄어듦과 움직임 줄이기는 바로 맞춘다.
        /// </summary>
        void Retarget(Counter counter, bool force)
        {
            if (counter.Label == null) return;
            double target = ShownTarget(counter.Type, counter.Shown);
            if (counter.Routine != null && !force && !IsBigChange(counter.To, target))
            {
                counter.To = target;
                return;
            }

            double delta = target - counter.Shown;
            bool decrease = delta < 0d;
            bool big = IsBigChange(counter.Shown, target);
            // 날아오는 양이 늘어 줄어드는 것(보상을 이미 받았고 아이콘이 막 떠남)은 잃은 게 아니다 — 빨간 줄어듦 없이 바로 맞춘다
            bool tween = delta != 0d && (decrease ? big && !force : big || force) && !UIMotion.Reduced && isActiveAndEnabled;
            if (!tween)
            {
                StopTween(counter);
                counter.Shown = target;
                counter.To = target;
                Write(counter, target);
                return;
            }

            counter.From = counter.Shown;
            counter.To = target;
            counter.Elapsed = 0f;
            counter.Duration = decrease ? UIMotion.CurrencyDecrease : UIMotion.CurrencyCount;
            // 이미 줄어드는 색이 도는 중이면 그대로 이어 간다 (늘어날 때는 색을 원래대로)
            counter.TintDuration = decrease ? UIMotion.CurrencyDecreaseTint : 0f;
            if (!decrease && counter.Label.color != counter.BaseColor) counter.Label.color = counter.BaseColor;
            if (counter.Routine == null) counter.Routine = StartCoroutine(Tween(counter));
        }

        /// <summary>1%보다 크게 바뀌는지 (기준이 100 미만이면 1보다 크게)</summary>
        internal static bool IsBigChange(double from, double to)
        {
            double change = Math.Abs(to - from);
            double basis = Math.Abs(from);
            return basis < SmallBasis ? change > 1d : change > basis * BigChangeRatio;
        }

        IEnumerator Tween(Counter counter)
        {
            // 같은 프레임에 이어 오는 신호(보상이 들어온 뒤 바로 떠 있는 양이 더해짐)를 먼저 받는다 — 첫 값은 다음 프레임부터 쓴다
            yield return null;
            while (true)
            {
                counter.Elapsed += Time.unscaledDeltaTime;
                float k = counter.Duration > 0f ? Mathf.Clamp01(counter.Elapsed / counter.Duration) : 1f;
                counter.Shown = counter.From + (counter.To - counter.From) * UIMotion.OutCubic(k);
                Write(counter, counter.Shown);
                if (counter.TintDuration > 0f)
                {
                    float tint = Mathf.Clamp01(counter.Elapsed / counter.TintDuration);
                    counter.Label.color = Color.Lerp(UITheme.Negative, counter.BaseColor, UIMotion.OutCubic(tint));
                }
                if (k >= 1f && counter.Elapsed >= counter.TintDuration) break;
                yield return null;
            }
            counter.Shown = counter.To;
            Write(counter, counter.Shown);
            counter.TintDuration = 0f;
            if (counter.Label.color != counter.BaseColor) counter.Label.color = counter.BaseColor;
            counter.Routine = null;
        }

        void StopTween(Counter counter)
        {
            if (counter.Routine == null) return;
            StopCoroutine(counter.Routine);
            counter.Routine = null;
            counter.TintDuration = 0f;
            if (counter.Label != null && counter.Label.color != counter.BaseColor) counter.Label.color = counter.BaseColor;
        }

        void Snap(Counter counter)
        {
            if (counter.Routine == null) return;
            counter.Routine = null;
            counter.TintDuration = 0f;
            if (counter.Label == null) return;
            counter.Shown = counter.To;
            Write(counter, counter.Shown);
            counter.Label.color = counter.BaseColor;
        }

        /// <summary>보이는 숫자가 바뀔 때만 글을 다시 쓴다 (단추는 매 프레임 조금씩 늘어난다)</summary>
        static void Write(Counter counter, double value)
        {
            double key = GoldDisplayKey(value);
            if (key.Equals(counter.Key)) return;
            counter.Key = key;
            counter.Label.text = RewardService.FormatAmount(counter.Type, value);
        }

        /// <summary>
        /// 재화 표기(RewardService.FormatAmount → NumberFormatter.FormatCurrency)가 보여주는 자리까지 내린 값. 이 값이 같으면 표기도 같다.
        /// 100만 미만은 내림 정수(콤마 표기 "12,399" — 12,345 → 12,399가 다른 키), 그 위는 유효숫자 3자리 (1,234,567 → 1,230,000 = "1.23B").
        /// </summary>
        internal static double GoldDisplayKey(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount)) return amount;
            if (Math.Abs(amount) < NumberFormatter.CurrencyCommaLimit) return Math.Floor(amount);
            double step = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(amount))) - 2);
            return Math.Floor(amount / step + 1e-9) * step;
        }

        // ───────────── 레벨 · 초당 단추 ─────────────

        void OnLevelChanged(int oldLevel, int newLevel) => RefreshStats();

        void RefreshStats()
        {
            int level = Game.Growth.Level;
            if (level != _shownLevel && _levelText != null)
            {
                _shownLevel = level;
                _levelText.text = $"수선점 Lv.{level}";
            }
            double rate = Game.Growth.GoldPerSecond;
            if (rate != _shownRate && _goldPerSecText != null)
            {
                _shownRate = rate;
                _goldPerSecText.text = "초당 +" + RateText(rate);
            }
        }

        /// <summary>초당 수치 (§1.10): 1,000 미만은 소수 1~2자리 (MenuText.Rate — 1 → 1.1 → 1.21, 12.3, 456), 그 위는 FormatCurrency (1,800 · 1.23B)</summary>
        internal static string RateText(double perSecond) => MenuText.RateText(perSecond);

        void RefreshMailBadge()
        {
            if (_mailBadge != null) _mailBadge.SetCount(Game.Mail.Count);
        }
    }
}
