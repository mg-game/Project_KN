using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// "수선점 불 밝히기" 버튼 + 불빛 게이지. 게이지는 아래에서 차오르고 등불 둘레 고리도 함께 찬다.
    /// 가득 차면 버튼이 금빛으로 맥동하며 반짝임이 지나간다. 누르면 TryLightLamp.
    /// M37: 이름은 Cream(가득 차면 흰빛 쪽으로), 게이지 창은 어둡게(빌더 α0.68), % 글자는 24 굵게 Lamp + 외곽선, 누르면 바탕이 살짝 눌린다(PressScale).
    /// 불이 켜지는 순간의 중간 진동(M18)은 BattleFx.LampBurst가 이미 낸다 — 여기서는 내지 않는다.
    /// </summary>
    public class LampButtonView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] RectTransform _visual;
        [SerializeField] Image _background;
        [SerializeField] Image _gaugeFill;
        [SerializeField] Image _ringFill;
        [SerializeField] RectTransform _icon;
        [SerializeField] Image _iconGlow;
        [SerializeField] Image _outerGlow;
        [SerializeField] RectTransform _shine;
        [SerializeField] TMP_Text _label;
        [SerializeField] TMP_Text _percentText;
        [Tooltip("게이지가 덜 찼을 때 / 가득 찼을 때 버튼 바탕색")]
        [SerializeField] Color _idleTint = new(0.72f, 0.66f, 0.86f);
        [SerializeField] Color _readyTint = Color.white;
        [Tooltip("불빛을 빼앗길 때 잠깐 섞는 보랏빛 (아트 단추면 옅게 — 어두운 색을 곱하면 금 단추가 갈색으로 탁해진다)")]
        [SerializeField] Color _drainTint = new(0.42f, 0.32f, 0.62f);
        [Tooltip("길게 누르기 → 자동 불 밝히기 방식 고르기 (CR-02). 없으면 짧은 누름만")]
        [SerializeField] LampPolicyPicker _picker;

        float _gauge;
        float _ready;
        float _burst;
        float _deny;
        float _drain;
        int _percent = -1;
        bool _canLight;
        float _labelWhite = -1f;

        void Awake() => _button.onClick.AddListener(OnClick);

        public void Tick(BattleSimulator sim, float dt)
        {
            float target = sim != null ? Mathf.Clamp01(sim.LampGauge) : 0f;
            _canLight = sim != null && sim.CanLightLamp;
            _gauge = target < _gauge ? target : BattleEase.Damp(_gauge, target, 6f, dt);
            _ready = BattleEase.Damp(_ready, _canLight ? 1f : 0f, 8f, dt);
            _burst = Mathf.Max(0f, _burst - dt * 1.5f);
            _deny = Mathf.Max(0f, _deny - dt * 3.5f);
            _drain = Mathf.Max(0f, _drain - dt * 1.6f);

            float time = Time.unscaledTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 5f);

            _gaugeFill.fillAmount = _gauge;
            _gaugeFill.color = BattleEase.WithAlpha(Color.Lerp(UITheme.Lamp, UITheme.Cream, _ready * pulse * 0.5f), 0.32f + 0.25f * _ready);
            if (_ringFill != null)
            {
                _ringFill.fillAmount = _gauge;
                _ringFill.color = Color.Lerp(UITheme.Lamp, UITheme.Cream, _ready * pulse);
            }

            // 퍼센트가 바뀔 때만 갱신 (SetText 서식은 할당 없음)
            int percent = _canLight ? 101 : Mathf.FloorToInt(_gauge * 100f + 0.001f);
            if (percent != _percent)
            {
                _percent = percent;
                if (_canLight) _percentText.text = "준비 완료!";
                else _percentText.SetText("{0}%", percent);
            }

            if (_background != null)
            {
                // 불빛을 빼앗기면 잠깐 보랏빛으로 어두워진다
                var tint = Color.Lerp(_idleTint, _readyTint, _ready);
                _background.color = Color.Lerp(tint, _drainTint, _drain * 0.6f);
            }
            // M37: 이름은 늘 Cream (예전 SubText 섞기는 금 단추 위에서 대비가 모자랐다) — 가득 차면 흰빛 쪽으로 살짝. 값이 바뀔 때만 쓴다
            float labelWhite = Mathf.Round(_ready * 20f) / 20f;
            if (!Mathf.Approximately(labelWhite, _labelWhite))
            {
                _labelWhite = labelWhite;
                _label.color = Color.Lerp(UITheme.Cream, Color.white, 0.6f * labelWhite);
            }

            float scale = 1f + 0.035f * _ready * pulse + 0.12f * BattleEase.Punch(1f - _burst);
            _visual.localScale = new Vector3(scale, scale, 1f);
            float wobble = Mathf.Sin(_deny * 40f) * 6f * _deny + Mathf.Sin(_drain * 34f) * 5f * _drain;
            _visual.anchoredPosition = new Vector2(wobble, 0f);

            if (_outerGlow != null)
            {
                float glow = _ready * (0.45f + 0.4f * pulse) + _burst;
                _outerGlow.enabled = glow > 0.01f;
                _outerGlow.color = BattleEase.WithAlpha(UITheme.Lamp, Mathf.Clamp01(glow));
            }
            if (_iconGlow != null)
            {
                var glowColor = Color.Lerp(UITheme.Lamp, new Color(0.55f, 0.4f, 0.8f), _drain);
                _iconGlow.color = BattleEase.WithAlpha(glowColor, 0.25f + 0.5f * _gauge + 0.25f * _ready * pulse + 0.3f * _drain);
            }
            if (_icon != null)
            {
                float sway = Mathf.Sin(time * 2.2f) * (3f + 5f * _ready);
                _icon.localRotation = Quaternion.Euler(0f, 0f, sway);
            }
            if (_shine != null)
            {
                // 준비되면 반짝임이 비스듬히 훑고 지나간다
                bool show = _ready > 0.5f;
                if (_shine.gameObject.activeSelf != show) _shine.gameObject.SetActive(show);
                if (show)
                {
                    float sweep = Mathf.Repeat(time * 0.7f, 1.6f) - 0.3f;
                    float width = _visual.rect.width;
                    _shine.anchoredPosition = new Vector2(Mathf.LerpUnclamped(-width * 0.5f, width * 0.5f, sweep), 0f);
                }
            }
        }

        /// <summary>적이 불빛을 빨아들임 (LampDrain 기믹) — 버튼이 떨리며 잠깐 어두워진다</summary>
        public void OnDrained() => _drain = 1f;

        /// <summary>불 밝히기 발동 (자동 포함)</summary>
        public void OnLampLit()
        {
            _burst = 1f;
            _gauge = 0f;
        }

        void OnClick()
        {
            // 길게 눌러 방식 고르기를 연 뒤의 눌림은 불을 켜지 않는다
            if (_picker != null && _picker.ConsumeClick()) return;
            if (!Game.IsReady || Game.Battle == null) return;
            if (Game.Battle.TryLightLamp()) return;
            _deny = 1f;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowToast(_canLight ? "지금은 불을 밝힐 수 없어요." : "불빛 게이지가 아직 부족해요. 적을 물리치고 잔향이를 보내 채워 주세요!");
            }
        }
    }
}
