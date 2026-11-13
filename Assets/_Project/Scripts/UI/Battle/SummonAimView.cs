using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 잔향이 덱 슬롯을 홀드 → 드래그할 때의 방향 지정 미리보기.
    /// 수선점에서 손가락 방향으로 부채꼴과 화살표를 그리고(정확한 각도 유지), 가장 가까운 방위의 지킴이 받침대를 강조한다.
    /// 손가락을 따라오는 반투명 잔향이 고스트 + 안내 문구. 덱 띠 위에서 놓으면 취소(잔향 소모 없음).
    /// 잔향 부족 / 재소환 대기는 드래그 중에도 매 프레임 갱신된다 (SetState): 놓으면 실제로 소환되는지 그대로 보여 준다.
    /// 안내 띠는 HUD 아래(기준 화면 360~412, UX-SPEC-IA §8.3 — 끄는 동안 권장 전투력 알약을 가리지 않게) 자리를 빌더가 잡는다.
    /// 끌어서 방향 소환에 성공했을 때 세우는 깃발(MetaFlags.DeckDragHint · DirectionalSummon)은 성공을 아는 EchoDeckView가 세운다.
    /// </summary>
    public class SummonAimView : MonoBehaviour
    {
        [SerializeField] BattleFieldMapper _mapper;
        [Tooltip("고스트가 움직이는 화면 오버레이 (전투 화면 전체)")]
        [SerializeField] RectTransform _overlay;
        [Tooltip("여기 위에서 놓으면 취소")]
        [SerializeField] RectTransform _strip;
        [SerializeField] GuardianSlotView[] _guardians;

        [Header("바닥 평면")]
        [SerializeField] Image _fan;
        [SerializeField] RectTransform _arrow;
        [SerializeField] Image _arrowLine;
        [SerializeField] Image _arrowHead;
        [SerializeField] float _fanRadius = 2.4f;
        [SerializeField] float _fanArc = 50f;

        [Header("고스트 / 안내")]
        [SerializeField] RectTransform _ghost;
        [SerializeField] CanvasGroup _ghostGroup;
        [SerializeField] Image _ghostPortrait;
        [SerializeField] Image _ghostFallback;
        [SerializeField] TMP_Text _ghostInitial;
        [SerializeField] GameObject _cancelMark;
        [SerializeField] RectTransform _hintRoot;
        [SerializeField] CanvasGroup _hintGroup;
        [SerializeField] TMP_Text _hintText;

        bool _active;
        bool _affordable;
        bool _ready;
        bool _overField;
        /// <summary>놓으면 소환 가능한 정도 0~1 (색 전환용)</summary>
        float _usable;
        float _angle = Mathf.PI * 0.5f;
        float _shownAngle = Mathf.PI * 0.5f;
        float _show;
        float _fieldBlend;
        int _snapSlot = -1;
        int _hintState = -1;

        void Awake() => SetVisible(false);

        /// <summary>조준 중인지 (Begin ~ End/Cancel). 시트가 올라와 스스로 거두면 false가 되고, EchoDeckView가 이것을 보고 들린 카드를 바로 내려놓는다</summary>
        public bool IsActive => _active;

        /// <summary>방향 지정 미리보기 시작 (위험 방향을 처음 각도로)</summary>
        /// <param name="affordable">잔향이 충분한지</param>
        /// <param name="ready">재소환 대기(쿨다운)가 끝났는지</param>
        public void Begin(string formId, string lineageId, bool affordable, bool ready, float initialAngle)
        {
            _active = true;
            _affordable = affordable;
            _ready = ready;
            _usable = affordable && ready ? 1f : 0f;
            _overField = false;
            _angle = initialAngle;
            _shownAngle = initialAngle;
            _fieldBlend = 0f;
            _hintState = -1;
            BattleUnitArt.Apply(_ghostPortrait, _ghostFallback, _ghostInitial, formId, lineageId);
            _fan.fillAmount = _fanArc / 360f;
            SetVisible(true);
        }

        /// <summary>드래그 중 잔향/재소환 대기 상태 갱신 (매 프레임 호출, 바뀔 때만 문구를 다시 쓴다)</summary>
        public void SetState(bool affordable, bool ready)
        {
            if (!_active || (affordable == _affordable && ready == _ready)) return;
            _affordable = affordable;
            _ready = ready;
            UpdateHint();
        }

        /// <summary>손가락 위치 갱신. 전장 위면 true</summary>
        public bool Move(Vector2 screen, Camera eventCamera)
        {
            if (!_active) return false;
            bool overStrip = _strip != null && RectTransformUtility.RectangleContainsScreenPoint(_strip, screen, eventCamera);
            _overField = !overStrip && _mapper.ContainsScreenPoint(screen, eventCamera);

            // 수선점 바로 위에서는 각도가 불안정하므로 마지막 각도를 유지한다
            if (_mapper.ScreenToWorld(screen, eventCamera, out var world) && world.sqrMagnitude > 0.06f)
            {
                _angle = Mathf.Atan2(world.y, world.x);
            }
            SetSnap(_overField ? BattleGeometry.NearestSlot(_angle) : -1);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, screen, eventCamera, out var local))
            {
                _ghost.anchoredPosition = local - _overlay.rect.center + new Vector2(0f, 40f);
            }
            if (_cancelMark != null) _cancelMark.SetActive(!_overField);
            UpdateHint();
            return _overField;
        }

        /// <summary>손을 뗌. 전장 위면 true + 소환 각도(라디안)</summary>
        public bool End(Vector2 screen, Camera eventCamera, out float angle)
        {
            bool overField = Move(screen, eventCamera);
            angle = _angle;
            Cancel();
            return overField;
        }

        public void Cancel()
        {
            _active = false;
            SetSnap(-1);
        }

        void Update()
        {
            // 시트가 올라오기 시작하면 조준을 거둔다 — 덱은 MAIN에서만 (놓으면 취소로 끝나고 잔향은 그대로, UX-SPEC-IA §4.5 · §6.4).
            // 목표 상태(SheetOpen — 고르기와 같은 프레임에 켜진다)로 본다: p는 닫히는 0.24초 내내 0보다 커서, p로 보면 닫히는 끝자락
            // (p < 0.19 — 덱 띠 위쪽이 시트 밖으로 드러난다)에 MAIN에서 새로 시작한 조준까지 바로 거둔다 (카드는 들린 채, 놓으면 취소 알림만)
            if (_active && UIFrame.SheetOpen) Cancel();
            float dt = Time.unscaledDeltaTime;
            _show = BattleEase.Damp(_show, _active ? 1f : 0f, _active ? 18f : 14f, dt);
            if (!_active && _show < 0.01f)
            {
                if (_fan.enabled) SetVisible(false);
                return;
            }

            _fieldBlend = BattleEase.Damp(_fieldBlend, _overField ? 1f : 0f, 14f, dt);
            _shownAngle += BattleGeometry.DeltaAngle(_shownAngle, _angle) * (1f - Mathf.Exp(-22f * dt));
            float degrees = _shownAngle * Mathf.Rad2Deg;
            float time = Time.unscaledTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 7f);

            _usable = BattleEase.Damp(_usable, _affordable && _ready ? 1f : 0f, 12f, dt);
            var tint = Color.Lerp(UITheme.SubText, UITheme.EchoGlow, _usable);
            float ppu = _mapper.Ppu;

            // 부채꼴: 12시부터 시계 방향으로 채우므로 중심이 (90 - arc/2)도 → 목표 각도로 회전
            float diameter = _fanRadius * 2f * ppu * Mathf.Lerp(0.85f, 1f, _show);
            _fan.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            _fan.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees - (90f - _fanArc * 0.5f));
            _fan.color = BattleEase.WithAlpha(tint, _show * Mathf.Lerp(0.12f, 0.42f + 0.1f * pulse, _fieldBlend));

            _arrow.localRotation = Quaternion.Euler(0f, 0f, degrees);
            _arrow.localScale = new Vector3(ppu / 200f, ppu / 200f, 1f);
            float arrowAlpha = _show * Mathf.Lerp(0.25f, 0.95f, _fieldBlend);
            _arrowLine.color = BattleEase.WithAlpha(Color.Lerp(tint, Color.white, 0.3f), arrowAlpha * 0.8f);
            _arrowHead.color = BattleEase.WithAlpha(Color.Lerp(tint, Color.white, 0.45f), arrowAlpha);
            _arrowHead.rectTransform.anchoredPosition = new Vector2(388f + 10f * pulse, 0f);

            float lift = BattleEase.OutBack(_show, 2f);
            _ghost.localScale = new Vector3(lift, lift, 1f);
            _ghost.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 3f) * 5f);
            _ghostGroup.alpha = _show * Mathf.Lerp(0.55f, 0.85f, _fieldBlend);
            if (_hintGroup != null) _hintGroup.alpha = _show;
        }

        void SetSnap(int slot)
        {
            if (_snapSlot == slot) return;
            _snapSlot = slot;
            for (int i = 0; i < _guardians.Length; i++)
            {
                if (_guardians[i] != null) _guardians[i].SetAimHighlight(i == slot);
            }
        }

        void UpdateHint()
        {
            if (_hintText == null) return;
            int state = !_affordable ? 3 : !_ready ? 2 : _overField ? 1 : 0;
            if (state == _hintState) return;
            _hintState = state;
            _hintText.text = state switch
            {
                3 => "<color=#FF8A9E>잔향이 부족해요</color> · 덱으로 돌아가면 취소",
                2 => "<color=#FF8A9E>다시 부를 준비 중이에요</color> · 덱으로 돌아가면 취소",
                1 => "놓으면 이 방향으로 소환해요",
                _ => "여기서 놓으면 취소 · 잔향은 그대로예요",
            };
        }

        void SetVisible(bool visible)
        {
            _fan.enabled = visible;
            _arrow.gameObject.SetActive(visible);
            _ghost.gameObject.SetActive(visible);
            if (_hintRoot != null) _hintRoot.gameObject.SetActive(visible);
            if (!visible) _show = 0f;
        }
    }
}
