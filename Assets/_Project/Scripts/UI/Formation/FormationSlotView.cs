using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    public enum FormationSlotHighlight
    {
        None = 0,
        /// <summary>놓을 수 있는 칸</summary>
        Valid = 1,
        /// <summary>규칙상 놓을 수 없는 칸</summary>
        Invalid = 2,
        /// <summary>끌고 있는 카드가 지금 올라가 있는 칸</summary>
        Hover = 3,
    }

    /// <summary>
    /// 편성 칸 하나: 방위 링의 지킴이 칸(받침 + 방위 색 + 요격 범위 부채꼴) 또는 잔향이 덱 칸(소환 비용, 잠금).
    /// 탭과 드롭 판정은 FormationPanel이 처리한다.
    /// 칸 반짝 (CR-01, SPEC4 §7.2 — 패배 분석 '편성 바꾸기 ›'): seconds 동안 금빛 테(_pulseRing, 다시 빌드한 장면) + 1 → 1.06배.
    /// 예전 장면(_pulseRing 없음)은 Highlight 빛을 금색으로 켠다. 끌기 · 고르기 강조가 오면 반짝을 멈추고 그 강조로 바꾼다.
    /// </summary>
    public class FormationSlotView : MonoBehaviour, IPointerClickHandler
    {
        const float MaxFanRange = 2.4f;
        /// <summary>칸 반짝: 커지는 배율 · 커지는 / 돌아오는 시간 (SPEC4 §7.2: 금빛 테, 1 → 1.06, 2초)</summary>
        internal const float SlotPulseScale = 1.06f;
        const float SlotPulseIn = 0.2f;
        const float SlotPulseOut = 0.3f;

        [SerializeField] CharacterRole _role;
        [SerializeField] int _index;
        [SerializeField] FormPortraitView _portrait;
        [SerializeField] GameObject _emptyMark;
        [SerializeField] GameObject _nameBox;
        [SerializeField] TMP_Text _nameText;
        [Tooltip("잔향이 칸: 소환 비용(잔향)")]
        [SerializeField] GameObject _costBadge;
        [SerializeField] TMP_Text _costText;
        [SerializeField] GameObject _lockGroup;
        [SerializeField] TMP_Text _lockText;
        [SerializeField] Image _highlight;
        [Tooltip("방위 링 칸만: 수선점 중심 요격 범위 부채꼴. 부모 레이어 너비 = 링 반지름 x2 (세로로 눌린 원근 레이어)")]
        [SerializeField] Image _fan;
        [Tooltip("방위 링 칸만: CR-01 칸 반짝 금빛 테 (꺼 둠 — 없으면 Highlight 빛을 금색으로)")]
        [SerializeField] Image _pulseRing;

        FormationPanel _panel;
        MenuGlowPulse _highlightPulse;
        Coroutine _punch;
        Coroutine _fanAnim;
        float _fanTarget = -1f;
        Coroutine _slotPulse;
        /// <summary>칸 반짝 중 (금빛 테 또는 금빛 Highlight가 켜져 있다)</summary>
        bool _pulsing;

        public CharacterRole Role => _role;
        public int Index => _index;
        /// <summary>배치된 형태 (빈칸/잠김이면 null)</summary>
        public string FormId { get; private set; }
        public bool IsLocked { get; private set; }
        public bool IsEmpty => !IsLocked && string.IsNullOrEmpty(FormId);
        public string LockHint { get; private set; }

        void Awake()
        {
            if (_highlight != null) _highlightPulse = _highlight.GetComponent<MenuGlowPulse>();
        }

        public void Init(FormationPanel panel) => _panel = panel;

        public void Show(string formId, bool locked, string lockHint)
        {
            IsLocked = locked;
            LockHint = lockHint;
            FormId = locked ? null : formId;
            bool occupied = !string.IsNullOrEmpty(FormId);

            if (_lockGroup != null) _lockGroup.SetActive(locked);
            if (_lockText != null) _lockText.text = locked ? lockHint : string.Empty;
            _emptyMark.SetActive(!locked && !occupied);
            _portrait.gameObject.SetActive(occupied);
            if (_nameBox != null) _nameBox.SetActive(occupied);
            if (_costBadge != null) _costBadge.SetActive(occupied && _role == CharacterRole.Echo);
            if (!occupied) return;

            _portrait.SetForm(FormId);
            // 잔향이는 작고 반투명한 빛의 존재
            _portrait.SetAlpha(_role == CharacterRole.Echo ? 0.86f : 1f);
            if (_nameText != null) _nameText.text = Game.Characters.GetDisplayName(FormId);
            if (_costText != null) _costText.text = Game.Characters.GetStats(FormId).summonCost.ToString();
        }

        public void SetHighlight(FormationSlotHighlight state)
        {
            // 반짝 중에 강조 없음이 오면(목록 갱신 등) 반짝을 그대로 두고, 끌기 · 고르기 강조가 오면 반짝을 멈추고 그 강조로
            if (_pulsing)
            {
                if (state == FormationSlotHighlight.None) return;
                StopSlotPulse();
            }
            transform.localScale = Vector3.one * (state == FormationSlotHighlight.Hover ? 1.1f : 1f);
            if (_highlight == null) return;

            _highlight.gameObject.SetActive(state != FormationSlotHighlight.None);
            var accent = _role == CharacterRole.Guardian ? UITheme.SlotColor(_index) : UITheme.EchoGlow;
            var color = state switch
            {
                FormationSlotHighlight.Hover => UITheme.Lamp,
                FormationSlotHighlight.Invalid => new Color(UITheme.Negative.r, UITheme.Negative.g, UITheme.Negative.b, 0.45f),
                _ => accent,
            };
            _highlight.color = color;
            if (_highlightPulse != null) _highlightPulse.SetColor(color);
        }

        /// <summary>요격 범위 부채꼴 표시 (방위 링 칸만). sweep이면 0에서 펼쳐지는 연출.</summary>
        public void ShowFan(float arcDegrees, float range, float alpha, bool sweep)
        {
            if (_fan == null) return;

            var layer = (RectTransform)_fan.rectTransform.parent;
            float radius = layer.rect.width * 0.5f * Mathf.Clamp(range, 0.3f, MaxFanRange);
            _fan.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);

            // 채움 시작 = 위(12시), 시계 방향 → 부채꼴 가운데가 방위 각도에 오도록 회전
            float arc = Mathf.Clamp(arcDegrees, 6f, 360f);
            float slotDegrees = BattleGeometry.SlotAngle(_index) * Mathf.Rad2Deg;
            _fan.rectTransform.localRotation = Quaternion.Euler(0f, 0f, slotDegrees - 90f + arc * 0.5f);

            var color = UITheme.SlotColor(_index);
            color.a = alpha;
            _fan.color = color;
            _fan.gameObject.SetActive(true);

            float target = arc / 360f;
            if (sweep && isActiveAndEnabled)
            {
                _fanTarget = target;
                MenuFx.Restart(this, ref _fanAnim, SweepFan(target));
                return;
            }
            // 펼쳐지는 중인 같은 부채꼴은 연출을 끊지 않는다 (목록 갱신 등)
            if (_fanAnim != null && Mathf.Approximately(_fanTarget, target)) return;
            if (_fanAnim != null) StopCoroutine(_fanAnim);
            _fanAnim = null;
            _fanTarget = target;
            _fan.fillAmount = target;
        }

        IEnumerator SweepFan(float target)
        {
            yield return MenuFx.FillTo(_fan, 0f, target, 0.4f);
            _fanAnim = null;
        }

        public void HideFan()
        {
            if (_fan == null) return;
            if (_fanAnim != null) StopCoroutine(_fanAnim);
            _fanAnim = null;
            _fanTarget = -1f;
            _fan.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            // 꺼지면 코루틴이 멈추므로 연출 중이던 값을 최종값으로 맞춘다
            if (_fanAnim != null && _fan != null) _fan.fillAmount = _fanTarget;
            _fanAnim = null;
            _punch = null;
            if (_portrait != null) _portrait.transform.localScale = Vector3.one;
            if (_pulsing) StopSlotPulse();
            _slotPulse = null;
        }

        /// <summary>배치 직후 초상화가 툭 튀는 연출</summary>
        public void PlayPlaced() => MenuFx.Restart(this, ref _punch, PlacedRoutine());

        IEnumerator PlacedRoutine()
        {
            yield return MenuFx.PopIn(_portrait.transform, 0.3f);
            _punch = null;
        }

        // ───────────── 칸 반짝 (CR-01) ─────────────

        /// <summary>
        /// 이 칸을 seconds 동안 반짝인다 (FormationPanel.PulseGuardianSlot이 부른다 — 패널이 켜져 있을 때만): 금빛 테를 켜고 칸을 1 → 1.06배로 키웠다가
        /// 끝에 되돌린다 (움직임 줄이기면 크기는 그대로, 테만). 다시 부르면 처음부터. 꺼지면 OnDisable이 되돌린다
        /// </summary>
        public void PulseSlot(float seconds)
        {
            if (!isActiveAndEnabled) return;
            MenuFx.Restart(this, ref _slotPulse, SlotPulseRoutine(Mathf.Max(SlotPulseIn + SlotPulseOut, seconds)));
        }

        IEnumerator SlotPulseRoutine(float seconds)
        {
            ShowPulseMark(true);
            bool animate = !UIMotion.Reduced;
            float elapsed = 0f;
            if (animate)
            {
                for (; elapsed < SlotPulseIn; elapsed += Time.unscaledDeltaTime)
                {
                    transform.localScale = Vector3.one * Mathf.LerpUnclamped(1f, SlotPulseScale, UIMotion.OutBack(elapsed / SlotPulseIn));
                    yield return null;
                }
                transform.localScale = Vector3.one * SlotPulseScale;
            }
            for (; elapsed < seconds - (animate ? SlotPulseOut : 0f); elapsed += Time.unscaledDeltaTime) yield return null;
            if (animate)
            {
                for (float t = 0f; t < SlotPulseOut; t += Time.unscaledDeltaTime)
                {
                    transform.localScale = Vector3.one * Mathf.Lerp(SlotPulseScale, 1f, UIMotion.OutCubic(t / SlotPulseOut));
                    yield return null;
                }
            }
            _slotPulse = null;
            StopSlotPulse();
        }

        /// <summary>반짝을 멈추고 크기 · 테 · 금빛 Highlight를 되돌린다 (강조 없음 상태로)</summary>
        void StopSlotPulse()
        {
            if (_slotPulse != null) StopCoroutine(_slotPulse);
            _slotPulse = null;
            transform.localScale = Vector3.one;
            ShowPulseMark(false);
        }

        /// <summary>금빛 테 (다시 빌드한 장면) 또는 금빛 Highlight 빛 (예전 장면)을 켜고 끈다</summary>
        void ShowPulseMark(bool on)
        {
            _pulsing = on;
            if (_pulseRing != null)
            {
                _pulseRing.gameObject.SetActive(on);
                return;
            }
            if (_highlight == null) return;
            _highlight.gameObject.SetActive(on);
            if (!on) return;
            _highlight.color = UITheme.Gold;
            if (_highlightPulse != null) _highlightPulse.SetColor(UITheme.Gold);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_panel != null) _panel.OnSlotClicked(this);
        }
    }
}
