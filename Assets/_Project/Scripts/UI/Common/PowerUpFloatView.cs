using TMPro;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투력 변화 숫자 (FX-04 / PRG-01 · M46). Game.Power.Changed(이전, 새)가 오르면 상단 가운데(안전 영역 위에서 230)에
    /// 초록 "+1,234"가 0.9초 동안 떠오르고, 0.5초 동안 0에서 카운트업한다. 0.4초 안에 연달아 오른 변화는 하나로 합친다.
    /// 새로 뜨는 "+N"에 PowerUp 소리 하나 (AUDIO-SPEC §8.1 — 합쳐진 변화는 조용히, 길게 눌러 연속으로 올라도 처음 한 번).
    /// 강화 · 친구 레벨업 · 공명 · 의상 해금 · 진화처럼 팡파르(LevelUpFanfareView)가 막 소리를 낸 순간이면 그 소리 하나로 둔다 (FanfareOverlap).
    /// 상단 바 전투력 글자(메타 HUD의 PowerChip/PowerText, 없으면 상단 바 안 "Power"가 들어간 첫 오브젝트)를 함께 툭 튀긴다.
    ///   · 내려간 변화는 띄우지 않는다 — 단, 편성에서 카드를 놓은 직후(AllowDecreaseOnce, 0.5초 안 한 번)만
    ///     Negative "−80"을 0.8배 크기로 띄운다 (상단 바 글자는 튀기지 않는다, 소리 없음).
    ///   · 부팅 첫 계산(이전 값 0)과 메타가 꺼져 있을 때는 아무것도 하지 않는다.
    /// Update는 할당 없음, 할 일이 없으면 Update를 끈다.
    /// </summary>
    public class PowerUpFloatView : MonoBehaviour
    {
        /// <summary>AllowDecreaseOnce 뒤 감소를 받아 주는 시간 (초)</summary>
        public const float DecreaseWindow = 0.5f;
        /// <summary>감소 숫자 크기 배율</summary>
        const float DecreaseScale = 0.8f;
        /// <summary>
        /// 팡파르가 이만큼(초) 안에 소리를 냈으면 새 '+N'의 PowerUp을 건너뛴다 — 레벨업 이벤트 뒤 MetaSession.Tick(Power.Refresh)이 한두 프레임 늦게 같은 순간을 알린다
        /// </summary>
        const float FanfareOverlap = 0.25f;
        const char PlusSign = '+';
        /// <summary>감소 부호 (U+2212 빼기 — 하이픈보다 넓어 '+'와 폭이 맞는다)</summary>
        const char MinusSign = '−';

        [SerializeField] RectTransform _root;
        [SerializeField] CanvasGroup _group;
        [SerializeField] TMP_Text _amountText;
        [Tooltip("이 시간(초) 안의 변화는 합친다")]
        [SerializeField] float _mergeWindow = 0.4f;
        [SerializeField] float _duration = 0.9f;
        [SerializeField] float _countUpTime = 0.5f;
        [SerializeField] float _rise = 46f;

        [Header("상단 바 전투력 글자 (있으면 튀긴다)")]
        [SerializeField] RectTransform _canvasRoot;
        [Tooltip("Canvas 기준 후보 경로. 없으면 상단 바 안에서 이름에 \"Power\"가 들어간 첫 오브젝트")]
        [SerializeField] string[] _powerTextPaths =
        {
            "SafeArea/TopBar/PowerChip/PowerText",
            "SafeArea/TopBar/PowerText",
            "SafeArea/TopBar/PowerLabel",
            "SafeArea/TopBar/Power",
        };

        /// <summary>M10 상단 바 전투력 글자 (빌더가 옛 경로 목록을 저장했어도 먼저 본다)</summary>
        const string PowerChipTextPath = "SafeArea/TopBar/PowerChip/PowerText";

        static float s_allowDecreaseUntil = float.NegativeInfinity;

        PowerSystem _power;
        Vector2 _basePosition;
        Color _positiveColor = UITheme.Positive;
        readonly char[] _buffer = new char[FanfareFormat.BufferSize];
        bool _playing;
        bool _negative;
        float _time;
        float _sinceChange;
        double _countFrom;
        double _countTo;
        float _countTime;
        long _lastWritten = -1;

        RectTransform _powerText;
        bool _powerTextSearched;
        Vector3 _powerTextScale = Vector3.one;
        float _punch = -1f;
        /// <summary>할 일이 없어 스스로 끄는 중 (OnDisable에서 진짜 꺼짐과 구별)</summary>
        bool _sleeping;

        /// <summary>
        /// 다음 전투력 감소 한 번(0.5초 안)을 띄워도 된다 — 편성에서 카드를 놓은 직후 FormationPanel이 부른다 (M46).
        /// 그 감소는 Negative "−80"으로 0.8배, 상단 바 글자를 튀기지 않고 조용히 떠오른다. 다른 곳에서 온 감소는 그대로 무시한다.
        /// </summary>
        public static void AllowDecreaseOnce() => s_allowDecreaseUntil = Time.unscaledTime + DecreaseWindow;

        void Awake()
        {
            if (_root == null) _root = (RectTransform)transform;
            _basePosition = _root.anchoredPosition;
            if (_amountText != null) _positiveColor = _amountText.color;
            if (_group != null) _group.alpha = 0f;
        }

        void Start()
        {
            if (Game.IsReady && Game.Power != null)
            {
                _power = Game.Power;
                _power.Changed += OnPowerChanged;
            }
            Sleep();
        }

        void OnDestroy()
        {
            if (_power != null) _power.Changed -= OnPowerChanged;
        }

        void OnDisable()
        {
            if (_sleeping) return;
            _playing = false;
            if (_group != null) _group.alpha = 0f;
            if (_powerText != null && _punch >= 0f) _powerText.localScale = _powerTextScale;
            _punch = -1f;
        }

        void Sleep()
        {
            _sleeping = true;
            enabled = false;
            _sleeping = false;
        }

        void OnPowerChanged(long previous, long current)
        {
            if (!Game.IsMetaActive || previous <= 0 || current == previous || !gameObject.activeInHierarchy) return;

            if (current < previous)
            {
                // 감소는 편성에서 놓은 직후 한 번만
                if (Time.unscaledTime > s_allowDecreaseUntil) return;
                s_allowDecreaseUntil = float.NegativeInfinity;
                Begin(previous - current, true);
                return;
            }
            Begin(current - previous, false);
            PunchPowerText();
        }

        void Begin(long delta, bool negative)
        {
            if (_playing && _negative == negative && _sinceChange <= _mergeWindow)
            {
                // 연달아 온 같은 쪽 변화는 하나로: 지금 보이는 값에서 새 합계까지 다시 센다
                _countFrom = Shown();
                _countTo += delta;
                _countTime = 0f;
                _time = Mathf.Min(_time, 0.12f);
            }
            else
            {
                _playing = true;
                _negative = negative;
                _time = 0f;
                _countFrom = 0;
                _countTo = delta;
                _countTime = 0f;
                _lastWritten = -1;
                if (_amountText != null) _amountText.color = negative ? UITheme.Negative : _positiveColor;
                // 새로 뜨는 '+N'만 반짝 소리 (감소는 소리 없음). 팡파르가 막 울렸으면 그 소리 하나로 — LevelUp · PowerUp이 한 번 누름에 겹치지 않게
                if (!negative && !LevelUpFanfareView.SoundClaimedWithin(FanfareOverlap)) UISfx.Play(UISfxKey.PowerUp);
            }
            _sinceChange = 0f;
            enabled = true;
        }

        double Shown() => _countFrom + (_countTo - _countFrom) * UIMotion.OutCubic(_countTime / Mathf.Max(0.01f, _countUpTime));

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            StepPunch(dt);
            if (_playing) StepFloat(dt);
            if (!_playing && _punch < 0f) Sleep();
        }

        void StepFloat(float dt)
        {
            _time += dt;
            _sinceChange += dt;
            _countTime += dt;

            long whole = (long)System.Math.Floor(Shown());
            if (whole != _lastWritten)
            {
                _lastWritten = whole;
                FanfareFormat.Set(_amountText, _buffer, whole, _negative ? MinusSign : PlusSign);
            }

            float k = Mathf.Clamp01(_time / _duration);
            _root.anchoredPosition = _basePosition + new Vector2(0f, _rise * UIMotion.OutCubic(k));
            float pop = _time < 0.18f ? Mathf.Lerp(0.7f, 1f, UIMotion.OutBack(_time / 0.18f)) : 1f;
            _root.localScale = Vector3.one * (pop * (_negative ? DecreaseScale : 1f));
            float fadeIn = Mathf.Clamp01(_time / 0.08f);
            float fadeOut = 1f - Mathf.Clamp01((_time - (_duration - 0.3f)) / 0.3f);
            if (_group != null) _group.alpha = Mathf.Min(fadeIn, fadeOut);

            if (_time < _duration) return;
            _playing = false;
            if (_group != null) _group.alpha = 0f;
        }

        void PunchPowerText()
        {
            if (!_powerTextSearched || _powerText == null || !_powerText.gameObject.activeInHierarchy)
            {
                _powerTextSearched = true;
                var found = FindPowerText();
                if (found != _powerText)
                {
                    if (_powerText != null && _punch >= 0f) _powerText.localScale = _powerTextScale;
                    _powerText = found;
                    _punch = -1f;
                    if (_powerText != null) _powerTextScale = _powerText.localScale;
                }
            }
            if (_powerText == null) return;
            _punch = 0f;
            enabled = true;
        }

        RectTransform FindPowerText()
        {
            if (_canvasRoot == null) return null;
            var chipText = UIFind.Path(_canvasRoot, PowerChipTextPath);
            if (chipText != null && chipText.gameObject.activeInHierarchy) return chipText;
            foreach (var path in _powerTextPaths)
            {
                var found = UIFind.Path(_canvasRoot, path);
                if (found != null && found.gameObject.activeInHierarchy) return found;
            }
            return UIFind.DeepContains(UIFind.Path(_canvasRoot, CoachTargets.TopBar), "Power");
        }

        void StepPunch(float dt)
        {
            if (_punch < 0f) return;
            if (_powerText == null)
            {
                _punch = -1f;
                return;
            }
            _punch += dt;
            const float time = 0.26f;
            float k = Mathf.Clamp01(_punch / time);
            _powerText.localScale = _powerTextScale * (1f + 0.14f * Mathf.Sin(k * Mathf.PI));
            if (k < 1f) return;
            _powerText.localScale = _powerTextScale;
            _punch = -1f;
        }
    }
}
