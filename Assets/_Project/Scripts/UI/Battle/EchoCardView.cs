using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 잔향이 덱 카드 한 장: 초상화, 소환 비용 배지, 재소환 쿨다운(원형), 잔향 부족 시 어둡게, 준비되면 은은한 빛.
    /// 입력: 탭 = 위험한 방향 자동 소환 / 홀드 후 드래그 = 방향 지정 (EchoDeckView로 전달).
    /// 누른 손가락이 조금 흔들려 드래그가 시작돼도(EventSystem 임계값은 고밀도 화면에서 1mm도 안 됨) 바로 조준하지 않고,
    /// 홀드로 카드가 들린 뒤 움직였거나 / DPI 기준 탭 흔들림을 넘었거나 / 카드 밖으로 나갔을 때만 방향 지정을 시작한다. 그 전에 떼면 탭.
    /// 한 번에 한 손가락만 카드를 다룬다 (방향 지정 중 다른 손가락의 누름은 무시).
    /// 6번째 칸은 해금 전까지 자물쇠 + 짧은 스테이지 알약 '2-9' (FormationSystem.EchoSlotUnlockCode, 빌더가 쓴다).
    /// 오른쪽 위 작은 스킬 배지: 이 친구가 소환되면 쓰는 고유 스킬 그림 (상시 패시브는 보랏빛 테, 발동 스킬은 금빛 테).
    /// <para>
    /// M36 · M07: 빈칸의 '+'는 넣을 잔향이가 있으면(SetCandidate, EchoDeckView가 편성이 바뀔 때 잰다) 1.6초 주기로 숨쉬고(α 0.35↔0.8 — 줄이기면 가운데),
    /// 없으면 α 0.3으로 멈춰 있다. 비용 배지는 보라 원판(#4B2A8C) 위 Cream 숫자 — 잔향이 모자라면 숫자만 CostShort(Dark)로 (어둡게 · 흔들기는 그대로).
    /// 별은 어둡게 덮개 위에 그려지고, 4성 이상은 '★4'처럼 별 하나 + 숫자로 줄인다.
    /// 소환 실패 흔들림(Deny)은 약한 진동 하나 (M18 — 오류 토스트의 진동과 겹치면 Haptics 게이트가 하나로 줄인다).
    /// 창 포커스를 잃으면 누름 · 들림을 꺼질 때처럼 버린다 (M01 — 알트탭 중 놓친 떼기를 탭으로 보지 않게).
    /// </para>
    /// </summary>
    public class EchoCardView : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        enum CardState
        {
            None = -1,
            Locked,
            Empty,
            Filled,
        }

        [SerializeField] EchoDeckView _deck;
        [SerializeField] int _index;

        [Header("모양")]
        [Tooltip("눌림/들림/팝 연출이 적용되는 내용물 (카드 자리 자체는 고정)")]
        [SerializeField] RectTransform _visual;
        [SerializeField] Image _tint;
        [Tooltip("_tint가 초상화 배경 그림(portrait_bg)이면 켠다 — 흰색에 계보 색을 옅게만 섞는다 (그림에 진한 색을 곱하면 탁해진다)")]
        [SerializeField] bool _tintIsArt;
        [SerializeField] Image _portrait;
        [SerializeField] Image _fallback;
        [SerializeField] TMP_Text _fallbackInitial;
        [SerializeField] Image _costBadge;
        [SerializeField] TMP_Text _costText;
        [SerializeField] TMP_Text _starsText;
        [SerializeField] Image _cooldown;
        [SerializeField] TMP_Text _cooldownText;
        [SerializeField] Image _dim;
        [SerializeField] Image _readyGlow;
        [SerializeField] GameObject _content;
        [SerializeField] GameObject _empty;
        [SerializeField] GameObject _locked;
        [Tooltip("빈칸 '+' 그림 (바느질 + 아이콘 하나, 없으면 가로 · 세로 막대 둘) — 넣을 잔향이가 있으면 숨쉬고 없으면 흐리게 멈춘다 (M36)")]
        [SerializeField] Graphic[] _emptyPlus;

        [Header("고유 스킬 배지")]
        [SerializeField] RectTransform _skillBadge;
        [SerializeField] Image _skillIcon;
        [SerializeField] Image _skillRim;
        [SerializeField] SkillVfxLibrary _library;

        [Header("입력")]
        [Tooltip("이보다 오래 누른 뒤 드래그 없이 떼면 탭으로 보지 않는다 (취소)")]
        [SerializeField] float _holdCancelTime = 0.75f;
        [Tooltip("홀드로 인식해 카드가 살짝 들리는 시간. 이만큼 누른 뒤 움직이면 방향 지정을 시작한다")]
        [SerializeField] float _holdLiftTime = 0.22f;
        [Tooltip("홀드 전에는 이 거리(인치)보다 크게 움직이거나 카드 밖으로 나가야 방향 지정을 시작한다. 그보다 작은 흔들림은 탭으로 본다")]
        [SerializeField] float _tapSlopInches = 0.1f;

        const int NoPointer = int.MinValue;
        /// <summary>빈칸 '+' 알파: 넣을 잔향이가 있으면 숨쉬기 범위, 없으면 멈춘 값 (M36)</summary>
        const float PlusBreatheMin = 0.35f;
        const float PlusBreatheMax = 0.8f;
        const float PlusIdle = 0.3f;
        /// <summary>비용 배지 숫자: 넉넉 = Cream, 모자람 = CostShort(Dark) (M07)</summary>
        static readonly Color CostReady = UITheme.Cream;

        /// <summary>4성 이상 줄임 별 '★4'(별 그림 하나 + 숫자) — 등급 별 수마다 한 번만 만든다</summary>
        static readonly string[] ShortStars = new string[8];

        CardState _state = CardState.None;
        bool _candidate;
        float _plusAlpha = -1f;
        string _formId;
        int _cost = -1;
        int _cooldownSeconds = -1;
        bool _affordable = true;
        bool _costColorDirty = true;
        /// <summary>이 카드를 다루는 손가락 (다른 손가락의 이벤트는 무시)</summary>
        int _pointerId = NoPointer;
        bool _pressed;
        /// <summary>드래그는 시작됐지만 아직 탭 흔들림 범위 안 (떼면 탭)</summary>
        bool _dragPending;
        /// <summary>방향 지정 중 (EchoDeckView의 조준과 짝)</summary>
        bool _aiming;
        /// <summary>덱이 조준을 취소함 (전투 재시작 등) → 이번 누름은 손을 뗄 때까지 무시</summary>
        bool _aborted;
        float _slopSq;
        float _pressTime;
        float _pop;
        float _deny;
        float _lift;
        float _ready;

        /// <summary>시뮬레이터 상태로 카드 갱신 (매 프레임)</summary>
        public void Refresh(DeckSlotState slot, bool locked, float resonance, float dt)
        {
            var state = locked ? CardState.Locked : slot == null || string.IsNullOrEmpty(slot.FormId) ? CardState.Empty : CardState.Filled;
            if (state != _state)
            {
                _state = state;
                _content.SetActive(state == CardState.Filled);
                _empty.SetActive(state == CardState.Empty);
                _locked.SetActive(state == CardState.Locked);
                _formId = null;
            }

            if (state == CardState.Filled) RefreshFilled(slot, resonance);
            else
            {
                _ready = BattleEase.Damp(_ready, 0f, 8f, dt);
                if (_tint != null) _tint.color = _tintIsArt ? Color.white : BattleEase.WithAlpha(UITheme.PanelLight, 0.6f);
                if (state == CardState.Empty) AnimatePlus(Time.unscaledTime);
            }
            Animate(dt);
        }

        void RefreshFilled(DeckSlotState slot, float resonance)
        {
            if (_formId != slot.FormId)
            {
                _formId = slot.FormId;
                BattleUnitArt.Apply(_portrait, _fallback, _fallbackInitial, slot.FormId, slot.LineageId);
                if (_tint != null)
                {
                    var main = BattleUnitArt.MainColor(slot.LineageId, slot.FormId);
                    _tint.color = _tintIsArt ? Color.Lerp(Color.white, main, 0.3f) : BattleEase.WithAlpha(main, 0.55f);
                }
                if (_starsText != null) _starsText.text = StarsLabel(slot.LineageId, slot.FormId);
                BindSkill(slot.FormId);
                _cost = -1;
                // 다시 칠할 때 비용 색도 다시 정한다 (빈칸 → 채움)
                _costColorDirty = true;
            }
            if (_cost != slot.Cost)
            {
                _cost = slot.Cost;
                _costText.text = BattleEase.Int(slot.Cost);
            }

            float cooldown = slot.CooldownMax > 0f ? Mathf.Clamp01(slot.Cooldown / slot.CooldownMax) : 0f;
            bool cooling = slot.Cooldown > 0.01f;
            _cooldown.enabled = cooling;
            if (cooling) _cooldown.fillAmount = cooldown;
            int seconds = cooling ? Mathf.CeilToInt(slot.Cooldown) : 0;
            if (seconds != _cooldownSeconds)
            {
                _cooldownSeconds = seconds;
                _cooldownText.text = seconds > 0 ? BattleEase.Int(seconds) : string.Empty;
            }

            bool affordable = resonance + 0.001f >= slot.Cost;
            if (affordable != _affordable || _costColorDirty)
            {
                _affordable = affordable;
                _costColorDirty = false;
                // M07: 모자람은 숫자 색만 (보라 원판이 알약 몫) — 어둡게(_dim) · 흔들기는 카드가 따로 한다
                _costText.color = affordable ? CostReady : UITheme.CostShort(CostSurface.Dark);
            }
            _dim.enabled = !affordable;
            _ready = affordable && !cooling ? 1f : 0f;
        }

        /// <summary>
        /// 등급 별 글자: 3성까지는 별 그림 n개, 4성 이상은 별 하나 + 숫자 '★4' (98px 카드 아래 띠에 별 4~5개가 들어가지 않는다).
        /// 형태가 바뀔 때만 부른다 — 줄임 글자는 별 수마다 한 번만 만든다.
        /// </summary>
        internal static string StarsLabel(string lineageId, string formId)
        {
            var lineage = BattleUnitArt.Lineage(lineageId, formId);
            return lineage != null ? StarsLabel((int)lineage.grade + 1) : string.Empty;
        }

        /// <summary>별 수 → 글자 (0 이하 = 빈 글자, 1~3 = 별 그림 n개, 4 이상 = 별 그림 하나 + 숫자)</summary>
        internal static string StarsLabel(int count)
        {
            if (count <= 0) return string.Empty;
            if (count < 4) return UITheme.Stars(count);
            if (count >= ShortStars.Length) return UITheme.StarSprite + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return ShortStars[count] ??= UITheme.StarSprite + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>빈칸에 넣을 잔향이가 있는지 (EchoDeckView가 편성 · 친구가 바뀔 때 잰다). 있으면 '+'가 숨쉰다</summary>
        public void SetCandidate(bool candidate) => _candidate = candidate;

        /// <summary>빈칸 '+' 알파 (빈칸일 때 매 프레임): 넣을 잔향이가 있으면 숨쉬기, 없으면 0.3. 값이 바뀔 때만 색을 쓴다</summary>
        void AnimatePlus(float time)
        {
            if (_emptyPlus == null || _emptyPlus.Length == 0) return;
            float alpha = _candidate ? UIMotion.Breathe(time, PlusBreatheMin, PlusBreatheMax) : PlusIdle;
            if (Mathf.Abs(alpha - _plusAlpha) < 0.004f) return;
            _plusAlpha = alpha;
            for (int i = 0; i < _emptyPlus.Length; i++)
            {
                var plus = _emptyPlus[i];
                if (plus != null) plus.color = BattleEase.WithAlpha(plus.color, alpha);
            }
        }

        void BindSkill(string formId)
        {
            if (_skillBadge == null) return;
            var skill = BattleUnitArt.FormSkill(formId);
            Sprite icon = null;
            bool isArt = false;
            if (skill != null && _library != null) _library.TryGetSkillIcon(skill.vfx, out icon, out isArt);
            bool show = skill != null && icon != null;
            _skillBadge.gameObject.SetActive(show);
            if (!show) return;
            if (_skillIcon != null)
            {
                _skillIcon.sprite = icon;
                _skillIcon.color = isArt ? Color.white : SkillVfxLook.Get(skill.vfx).Main;
            }
            if (_skillRim != null) _skillRim.color = skill.trigger == SkillTrigger.Passive ? UITheme.Lavender : UITheme.Gold;
        }

        void Animate(float dt)
        {
            float time = Time.unscaledTime;
            _pop = Mathf.Max(0f, _pop - dt * 3.2f);
            _deny = Mathf.Max(0f, _deny - dt * 3.5f);
            bool pressing = _pressed && !_aiming && !_aborted;
            bool holding = pressing && time - _pressTime > _holdLiftTime && _state == CardState.Filled;
            _lift = BattleEase.Damp(_lift, holding || _aiming ? 1f : 0f, 16f, dt);

            float press = pressing ? 1f : 0f;
            float scale = 1f - 0.06f * press * (1f - _lift) + 0.08f * _lift + 0.22f * BattleEase.Punch(1f - _pop);
            _visual.localScale = new Vector3(scale, scale, 1f);
            _visual.anchoredPosition = new Vector2(Mathf.Sin(_deny * 40f) * 7f * _deny, 14f * _lift);

            if (_readyGlow != null)
            {
                float glow = _state == CardState.Filled && _ready > 0.5f ? 0.35f + 0.25f * Mathf.Sin(time * 3f + _index) : 0f;
                glow = Mathf.Max(glow, Mathf.Max(_lift * 0.9f, _pop));
                _readyGlow.enabled = glow > 0.01f;
                _readyGlow.color = BattleEase.WithAlpha(UITheme.EchoGlow, glow);
            }
            if (_costBadge != null)
            {
                float badge = 1f + 0.25f * BattleEase.Punch(1f - _pop);
                _costBadge.rectTransform.localScale = new Vector3(badge, badge, 1f);
            }
        }

        /// <summary>소환 성공 (자동 소환 포함)</summary>
        public void Pop() => _pop = 1f;

        /// <summary>소환 실패 흔들림 + 약한 진동 (M18 — 설정 · 간격 규칙은 Haptics가 따른다)</summary>
        public void Deny()
        {
            _deny = 1f;
            Haptics.Play(HapticKind.Light);
        }

        // ───────────── 입력 ─────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            // 다른 손가락이 이 카드로 방향을 지정하는 중이면 그 손가락이 끝날 때까지 새 누름을 받지 않는다
            if (eventData.button != PointerEventData.InputButton.Left || _aiming) return;
            _pointerId = eventData.pointerId;
            _pressed = true;
            _dragPending = false;
            _aborted = false;
            _pressTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Owns(eventData)) _pressed = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 방향 지정으로 넘어간 누름은 OnEndDrag에서 끝낸다. 탭 흔들림 안의 드래그(대기)는 탭으로 본다
            if (!Owns(eventData) || _aiming || _aborted) return;
            // 길게 눌렀다가 그대로 떼면 취소로 본다 (잔향 소모 없음)
            if (Time.unscaledTime - _pressTime > _holdCancelTime && _state == CardState.Filled) return;
            _deck.Tap(_index);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // pointerDrag를 끊으면 EventSystem이 클릭 자격까지 지워 탭이 사라지므로, 드래그는 항상 받고 여기서 판단한다
            if (!Owns(eventData) || _aiming || _aborted) return;
            _dragPending = true;
            float slop = TapSlopPixels();
            _slopSq = slop * slop;
            TryBeginAim(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!Owns(eventData)) return;
            if (_aiming) _deck.UpdateAim(eventData.position, eventData.pressEventCamera);
            else if (_dragPending) TryBeginAim(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!Owns(eventData)) return;
            bool aiming = _aiming;
            _aiming = false;
            _dragPending = false;
            _pressed = false;
            if (aiming) _deck.EndAim(_index, eventData.position, eventData.pressEventCamera);
        }

        /// <summary>덱이 방향 지정을 끝냄 (전투 재시작 등). 이번 누름은 손을 뗄 때까지 탭/드래그로 쓰지 않는다</summary>
        public void AbortAim()
        {
            if (!_aiming) return;
            _aiming = false;
            _dragPending = false;
            _aborted = true;
        }

        /// <summary>
        /// 드래그가 탭 흔들림을 넘었으면 방향 지정을 시작한다:
        /// 홀드로 카드가 들린 뒤 움직였거나, DPI 기준 탭 흔들림보다 멀리 움직였거나, 카드 밖으로 나갔을 때.
        /// </summary>
        void TryBeginAim(PointerEventData eventData)
        {
            if (!_deck.CanAim(_index)) return;
            bool held = Time.unscaledTime - _pressTime >= _holdLiftTime;
            bool moved = (eventData.position - eventData.pressPosition).sqrMagnitude >= _slopSq;
            bool left = !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, eventData.position, eventData.pressEventCamera);
            if (!held && !moved && !left) return;
            if (!_deck.BeginAim(_index, eventData.position, eventData.pressEventCamera)) return;
            _aiming = true;
            _dragPending = false;
        }

        /// <summary>탭으로 인정하는 손가락 흔들림 (화면 픽셀). DPI를 모르면 화면 크기 비율로, 최소 EventSystem 드래그 임계값</summary>
        float TapSlopPixels()
        {
            float dpi = Screen.dpi;
            float slop = dpi > 1f ? dpi * _tapSlopInches : Mathf.Max(Screen.width, Screen.height) * 0.015f;
            var system = EventSystem.current;
            return system != null ? Mathf.Max(slop, system.pixelDragThreshold) : slop;
        }

        bool Owns(PointerEventData eventData) => _pointerId != NoPointer && eventData.pointerId == _pointerId;

        void OnDisable() => DropPress();

        /// <summary>
        /// 창 포커스를 잃으면 누름 · 들림을 꺼질 때처럼 버린다 (M01 · UX-SPEC §3.16): 알트탭 중에는 EventSystem이 떼기를 놓쳐
        /// 돌아와서 늦게 온 떼기가 탭(소환)이 되거나 카드가 들린 채 남을 수 있다. 전투는 그대로 흐른다
        /// </summary>
        void OnApplicationFocus(bool focused)
        {
            if (!focused && (_pressed || _aiming || _dragPending)) DropPress();
        }

        /// <summary>진행 중이던 누름을 버린다 (꺼짐 · 포커스 잃음 — 다시 켜진 뒤 늦게 온 떼기를 탭으로 보지 않도록). 조준 중이었으면 덱에 조용히 알린다</summary>
        void DropPress()
        {
            bool aiming = _aiming;
            _aiming = false;
            _dragPending = false;
            _aborted = false;
            _pressed = false;
            _pointerId = NoPointer;
            if (aiming && _deck != null) _deck.CancelAim();
        }
    }
}
