using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전장 가장자리의 위험 표시 (방위 슬롯 하나). 그 방향 위협도(0~1)만큼 커지고 밝아지며,
    /// 수선점 쪽으로 행진하는 화살촉과 붉은 빛으로 "이쪽이 위험해요"를 알린다.
    /// 안내 줄(가이드 퀘스트 — GuideQuestBarView.TryGetRect, 0.5초마다 다시 본다)에 8px 안으로 들어오면 전장 가장자리를 따라 줄 위로 비킨다 (§13.5 e, Q-01).
    /// </summary>
    public class DangerArrowView : MonoBehaviour
    {
        [SerializeField] BattleFieldMapper _mapper;
        [SerializeField] int _slot;
        [SerializeField] Image _glow;
        [SerializeField] Image[] _chevrons;
        [Tooltip("표시할 월드 반지름 (적 생성 반지름 2.5 바로 안쪽)")]
        [SerializeField] float _radius = 2.2f;
        [SerializeField] float _edgeMargin = 46f;
        [Tooltip("위쪽 HUD 아래로 내려오도록 남기는 여백")]
        [SerializeField] float _topMargin = 56f;
        [SerializeField] float _chevronSpacing = 24f;

        int _layoutVersion = -1;
        float _danger;
        Color _slotColor;
        /// <summary>안내 줄과 띄우는 틈 (px) / 안내 줄 자리를 다시 보는 간격 (초)</summary>
        public const float GuideGap = 8f;
        const float GuideCheckInterval = 0.5f;
        /// <summary>화살표가 가장 커질 때 배율 (Tick: 0.75 + 0.55 × 위협도)</summary>
        const float MaxScale = 1.3f;
        float _guideTimer;
        bool _guideOn;
        Rect _guideRect;

        RectTransform Rect => (RectTransform)transform;

        void Awake() => _slotColor = UITheme.SlotColor(_slot);

        public void Tick(float danger, float dt)
        {
            if (_mapper.Version != _layoutVersion | GuideChanged(dt)) Layout();
            _danger = BattleEase.Damp(_danger, Mathf.Clamp01(danger), 5f, dt);

            bool visible = _danger > 0.04f;
            if (_glow.enabled != visible)
            {
                _glow.enabled = visible;
                for (int i = 0; i < _chevrons.Length; i++) _chevrons[i].enabled = visible;
            }
            if (!visible) return;

            float time = Time.time;
            var color = Color.Lerp(_slotColor, UITheme.Danger, 0.35f + 0.5f * _danger);
            float scale = 0.75f + 0.55f * _danger;
            Rect.localScale = new Vector3(scale, scale, 1f);

            float pulse = 0.5f + 0.5f * Mathf.Sin(time * (4f + 4f * _danger));
            _glow.color = BattleEase.WithAlpha(color, (0.25f + 0.35f * pulse) * _danger);

            // 화살촉이 수선점 쪽(로컬 +y)으로 행진한다
            for (int i = 0; i < _chevrons.Length; i++)
            {
                float phase = Mathf.Repeat(time * (0.9f + _danger) + i / (float)_chevrons.Length, 1f);
                _chevrons[i].rectTransform.anchoredPosition = new Vector2(0f, (phase - 0.5f) * _chevronSpacing * _chevrons.Length);
                float alpha = Mathf.Sin(phase * Mathf.PI) * Mathf.Clamp01(0.35f + _danger);
                _chevrons[i].color = BattleEase.WithAlpha(Color.Lerp(color, Color.white, 0.25f), alpha);
            }
        }

        void Layout()
        {
            _layoutVersion = _mapper.Version;
            var position = _mapper.WorldToField(BattleGeometry.FromAngle(BattleGeometry.SlotAngle(_slot), _radius));
            var half = _mapper.Rect.rect.size * 0.5f - new Vector2(_edgeMargin, _edgeMargin);
            position.x = Mathf.Clamp(position.x, -half.x, half.x);
            float top = _mapper.Rect.rect.height * 0.5f - _topMargin;
            position.y = Mathf.Clamp(position.y, -half.y, Mathf.Min(half.y, top));
            if (_guideOn) position = AvoidGuide(position, ArrowHalf(), _guideRect, GuideGap, Mathf.Min(half.y, top));
            Rect.anchoredPosition = position;

            var inward = _mapper.Origin - position;
            float degrees = Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg - 90f;
            Rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        /// <summary>안내 줄 자리가 바뀌었는지 (0.5초마다 — 할당 없음). 바뀌었으면 Layout을 다시</summary>
        bool GuideChanged(float dt)
        {
            _guideTimer -= dt;
            if (_guideTimer > 0f) return false;
            _guideTimer = GuideCheckInterval;
            var space = Rect.parent as RectTransform;
            var rect = default(Rect);
            bool on = space != null && GuideQuestBarView.TryGetRect(space, out rect) && rect.width > 0f;
            if (!on) rect = default;
            if (on == _guideOn && (!on || (Mathf.Abs(rect.xMin - _guideRect.xMin) < 0.5f && Mathf.Abs(rect.yMin - _guideRect.yMin) < 0.5f &&
                                           Mathf.Abs(rect.xMax - _guideRect.xMax) < 0.5f && Mathf.Abs(rect.yMax - _guideRect.yMax) < 0.5f))) return false;
            _guideOn = on;
            _guideRect = rect;
            return true;
        }

        /// <summary>가장 커졌을 때의 반크기 (돌아가도 덮도록 긴 변의 정사각형)</summary>
        Vector2 ArrowHalf()
        {
            var size = Rect.rect.size;
            float h = Mathf.Max(size.x, size.y) * 0.5f * MaxScale;
            return new Vector2(h, h);
        }

        /// <summary>
        /// 안내 줄 비키기 (순수, 할당 없음): 화살표 상자(가운데 position, 반크기 half)가 줄(bar)에 gap 안으로 들어오면 가로는 그대로(전장 가장자리) 두고
        /// 줄 위 끝 + gap 위로 올린다 (top을 넘지 않게). 겹치지 않으면 그대로
        /// </summary>
        public static Vector2 AvoidGuide(Vector2 position, Vector2 half, Rect bar, float gap, float top)
        {
            if (position.x + half.x <= bar.xMin - gap || position.x - half.x >= bar.xMax + gap) return position;
            if (position.y + half.y <= bar.yMin - gap || position.y - half.y >= bar.yMax + gap) return position;
            position.y = Mathf.Min(bar.yMax + gap + half.y, top);
            return position;
        }
    }
}
