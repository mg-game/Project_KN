using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 상단 바에 메타가 덧붙이는 것 (UX-SPEC-IA §3.1 · CR-14 — 상단 바 틀과 TopBarView는 화면 틀 담당이라 고치지 않고 따로 붙인다):
    ///   · 전투력 숫자 — 상단 바 PowerChip/PowerText에 숫자만 쓴다 (PowerSystem.Format, 228px 칸에 '전투력 · 초당 단추' 한 줄은 넘친다).
    ///     전투력이 오르면 0.5초 카운트업 + 글자가 튄다 (M10 · FX-04, 떠오르는 "+1,234"는 PowerUpFloatView 담당).
    ///   · PowerChip을 누르면 권장 전투력 토스트 "내 전투력 12,345 · 권장 12,000 (여유)" — 시트가 열리면 권장 알약(RecommendedPower)이 사라지므로
    ///     시트 위에서도 볼 수 있게 (M10).
    /// '초당 +{rate}'(GoldPerSecText)는 TopBarView가 쓰고 늘 보인다 (예전처럼 숨기지 않는다). 수선 일지 · 출석부는 전투 화면 빠른 아이콘으로 옮겼다 (§3.3).
    /// 메타가 꺼져 있으면 전투력 숫자를 숨긴다.
    /// </summary>
    public class TopBarMetaView : MonoBehaviour
    {
        const float CountUpTime = 0.5f;
        const float CountUpStep = 0.05f;

        [Tooltip("전투력 숫자 (상단 바 PowerChip/PowerText — 숫자만)")]
        [SerializeField] TMP_Text _statsText;
        [Tooltip("튀기기를 받는 글자 루트")]
        [SerializeField] RectTransform _statsPunch;
        [Tooltip("누르면 권장 전투력 토스트 (상단 바 PowerChip의 버튼). 없어도 됨")]
        [SerializeField] Button _powerButton;

        bool _subscribed;
        long _shownPower = -1;
        long _countFrom;
        long _countTo;
        Coroutine _countUp;
        /// <summary>마지막으로 쓴 숫자 (같으면 다시 쓰지 않는다)</summary>
        long _writtenPower = long.MinValue;

        void Awake()
        {
            if (_powerButton != null) _powerButton.onClick.AddListener(OnPowerTapped);
        }

        void Start()
        {
            bool active = MetaUi.IsActive;
            if (active)
            {
                Game.Power.Changed += OnPowerChanged;
                _subscribed = true;
            }
            if (_statsText != null) _statsText.gameObject.SetActive(active);
            _shownPower = active ? Game.Power.TeamPower : 0;
            RefreshStats();
        }

        void OnDestroy()
        {
            if (!_subscribed || !Game.IsReady || Game.Power == null) return;
            Game.Power.Changed -= OnPowerChanged;
        }

        void OnDisable()
        {
            // 꺼지면 코루틴이 멈춘다 — 튄 크기 · 세던 숫자를 끝 값으로 맞춘다
            if (_countUp == null) return;
            _countUp = null;
            if (_statsPunch != null) _statsPunch.localScale = Vector3.one;
            _shownPower = _countTo;
            WriteStats();
        }

        /// <summary>PowerChip 누름: 권장 전투력 알약을 누른 것과 같은 토스트 (RecommendedPowerView와 같은 글)</summary>
        void OnPowerTapped()
        {
            if (!MetaUi.IsActive) return;
            int stage = Game.Battle.Stage;
            var rating = Game.Power.GetRating(stage);
            MetaUi.Toast($"내 전투력 {PowerSystem.Format(Game.Power.TeamPower)} · 권장 {PowerSystem.Format(Game.Power.GetRecommendedPower(stage))} ({PowerSystem.RatingName(rating)})");
        }

        // ───────────── 전투력 ─────────────

        void OnPowerChanged(long oldPower, long newPower)
        {
            // 부팅 직후 첫 계산(0 → 값)은 세어 올리지 않는다
            if (!isActiveAndEnabled || oldPower <= 0)
            {
                _shownPower = newPower;
                RefreshStats();
                return;
            }
            _countFrom = _countUp != null ? _shownPower : oldPower;
            _countTo = newPower;
            if (_countUp != null) StopCoroutine(_countUp);
            _countUp = StartCoroutine(CountUp(newPower > oldPower));
        }

        IEnumerator CountUp(bool rising)
        {
            float step = 0f;
            for (float t = 0f; t < CountUpTime; t += Time.unscaledDeltaTime)
            {
                float k = MetaUi.EaseOutCubic(t / CountUpTime);
                if (_statsPunch != null && rising) _statsPunch.localScale = Vector3.one * (1f + 0.12f * MetaUi.Punch(t / CountUpTime));
                step -= Time.unscaledDeltaTime;
                if (step <= 0f)
                {
                    step = CountUpStep;
                    _shownPower = (long)Math.Round(_countFrom + (_countTo - _countFrom) * (double)k);
                    WriteStats();
                }
                yield return null;
            }
            if (_statsPunch != null) _statsPunch.localScale = Vector3.one;
            _shownPower = _countTo;
            WriteStats();
            _countUp = null;
        }

        void RefreshStats()
        {
            if (!MetaUi.IsActive) return;
            if (_countUp == null) _shownPower = Game.Power.TeamPower;
            WriteStats();
        }

        /// <summary>숫자만 쓴다 (PowerSystem.Format). 같은 숫자면 문자열을 다시 만들지 않는다</summary>
        void WriteStats()
        {
            if (_statsText == null || _shownPower == _writtenPower) return;
            _writtenPower = _shownPower;
            _statsText.text = PowerSystem.Format(_shownPower);
        }
    }
}
