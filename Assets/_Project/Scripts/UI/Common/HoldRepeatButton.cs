using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 길게 누르면 반복한다 (M19, UX-SPEC §3.11 — 강화 x1, 계보 카드 레벨업, 친구 상세 레벨업).
    ///   · 누른 지 _delay초(0.35, 계보 카드 0.45) 뒤부터 반복. 간격은 첫 반복부터 1.8초에 걸쳐 0.17초 → 0.05초로 빨라진다 (UIMotion.HoldInterval).
    ///   · Step이 있으면 반복마다 onClick 대신 Step()을 부른다 (그냥 탭은 여전히 onClick). Step이 false를 돌려주면 누르기를 끝내고
    ///     Denied를 한 번 알린다 — 부르는 쪽이 토스트 한 번 + 흔들기(CostButtonView.Deny)를 한다. Step 안에서는 토스트를 띄우지 않는다.
    ///     Step이 없으면 예전처럼 onClick을 반복한다 (실패를 알 수 없으니 멈추지 않고, 성공도 알 수 없으니 연속 알약 · Combo도 세지 않는다 —
    ///     소비자는 Step으로 옮긴다).
    ///   · Step · onClick · Denied 처리기가 예외를 던지면 기록하고 그 누르기를 깨끗이 끝낸다 (알약도 숨김) — 다음 누름은 처음부터 된다.
    ///   · 첫 반복 때 그 누름의 클릭 자격을 없앤다 (eligibleForClick = false) — 떼어도 한 번 더 오르지 않는다.
    ///   · 멈춤: 뗌 · 밖으로 나감 · 꺼짐 · 드래그 시작(스크롤이 시작되면 EventSystem이 떼기를 보낸다) · 손가락이 흔들림 허용보다 많이 움직임
    ///     (드래그 임계값과 0.12인치 중 큰 것 — 고해상도 폰에서 손떨림으로 끊기지 않게) · 버튼을 누를 수 없게 됨 ·
    ///     창 포커스 잃음 / 앱 백그라운드 (M01: 알트탭 중에는 떼기를 놓쳐 뒤에서 단추를 계속 쓸 수 있다 — 누르기만 끝내고 게임은 멈추지 않는다).
    ///     IBeginDragHandler를 구현하지 않는다 (스크롤을 가로채지 않게).
    ///   · 연속 알약 "HoldCombo": 둘째 성공부터 버튼 위쪽 가운데(_comboOffset, 기본 (0, +26) — 계보 카드 (0, +18))에 112x44 금 알약 "+N"
    ///     (Ink 26 굵게)을 띄우고 성공마다 1.15배로 0.12초 툭 튀긴다 (반복이 그보다 빠르면 튀기가 끝날 때마다 다시). 떼면 0.4초 머물고 0.25초에 사라진다.
    ///     실행 중에 처음 필요할 때 만든다 (빌더 손댐 없음): 흰 알약 아트를 Gold로 물들인 NEW 알약과 같은 모양, 누름 안 받음, 레이아웃 무시.
    ///   · 반복마다 진동하지 않는다 (Handheld.Vibrate도 쓰지 않는다). 움직일 때만 프레임마다 돈다 (FrameTicker, 할당 없음).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const string ComboName = "HoldCombo";
        static readonly Vector2 ComboSize = new(112f, 44f);
        const float ComboFontSize = 26f;
        /// <summary>손가락 흔들림 허용 (인치). 드래그 임계값보다 작으면 임계값</summary>
        const float SlopInches = 0.12f;

        [Tooltip("반복 시작까지 (초). 계보 카드는 0.45")]
        [SerializeField] float _delay = UIMotion.HoldDelay;
        [Tooltip("연속 알약 \"+N\" 자리 (버튼 위쪽 가운데 기준, 위로 +). 계보 카드는 (0, 18)")]
        [SerializeField] Vector2 _comboOffset = new(0f, 26f);
        [Tooltip("연속 알약 바탕 (비우면 실행 중에 흰 알약 아트를 찾아 Gold로 물들인다)")]
        [SerializeField] Sprite _comboSprite;

        Button _button;
        FrameTicker _ticker;

        PointerEventData _ev;
        int _pointerId;
        bool _holding;
        bool _repeating;
        /// <summary>다음 반복까지 남은 시간</summary>
        float _wait;
        /// <summary>첫 반복부터 지난 시간 (빨라지기)</summary>
        float _held;
        /// <summary>이번 누름에서 성공한 반복 수</summary>
        int _combo;
        float _slopSq;

        RectTransform _pill;
        CanvasGroup _pillGroup;
        TMP_Text _pillText;
        float _pillPunch = -1f;
        /// <summary>뗀 뒤 지난 시간 (누르는 중이거나 숨었으면 -1)</summary>
        float _pillLinger = -1f;

        /// <summary>
        /// 반복 한 번 (누르고 있는 동안 onClick 대신). 성공하면 true, 더 할 수 없으면(재화 부족 · 최대 레벨) 토스트 없이 false —
        /// 그러면 누르기가 끝나고 Denied가 한 번 온다.
        /// </summary>
        public Func<bool> Step { get; set; }

        /// <summary>Step이 false를 돌려줘 누르기가 끝났다 (한 번). 토스트 한 번 + 흔들기는 여기서</summary>
        public event Action Denied;

        /// <summary>반복 시작까지 (초) — 빌더 · 화면이 계보 카드에 0.45를 준다</summary>
        public float Delay
        {
            get => _delay;
            set => _delay = Mathf.Max(0f, value);
        }

        /// <summary>연속 알약 자리 (버튼 위쪽 가운데 기준)</summary>
        public Vector2 ComboOffset
        {
            get => _comboOffset;
            set
            {
                _comboOffset = value;
                if (_pill != null) _pill.anchoredPosition = value;
            }
        }

        /// <summary>누르고 있는 중</summary>
        public bool IsHolding => _holding;

        /// <summary>이번(또는 마지막) 누름에서 성공한 반복 수 (Step으로 성공을 알려 준 반복만 센다)</summary>
        public int Combo => _combo;

        void Awake()
        {
            _button = GetComponent<Button>();
            _ticker = new FrameTicker(this, Tick);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!_button.IsInteractable()) return;
            // 틱이 살아 있으면 다른 손가락의 누름이다. 틱 없이 남은 누르기(예외로 끊김)는 버리고 새로 시작한다
            if (_holding && _ticker.Running) return;
            _ev = eventData;
            _pointerId = eventData.pointerId;
            _holding = true;
            _repeating = false;
            _wait = _delay;
            _held = 0f;
            _combo = 0;
            float slop = Slop();
            _slopSq = slop * slop;
            HidePill();
            _ticker.Start();
            if (!_ticker.Running) _holding = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) EndHold();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) EndHold();
        }

        void OnDisable()
        {
            EndHold();
            HidePill();
            _ticker?.Stop();
        }

        /// <summary>포커스를 잃으면 반복을 멈춘다 (누르기만 끝낸다, M01)</summary>
        void OnApplicationFocus(bool focused)
        {
            if (!focused) EndHold();
        }

        /// <summary>모바일 백그라운드에서도 누르기만 끝낸다 (M01)</summary>
        void OnApplicationPause(bool paused)
        {
            if (paused) EndHold();
        }

        void EndHold()
        {
            if (!_holding) return;
            _holding = false;
            _ev = null;
            if (_pill != null && _pill.gameObject.activeSelf)
            {
                _pillLinger = 0f;
                _ticker?.Start();
            }
        }

        /// <summary>손가락 흔들림 허용 (화면 px): 드래그 임계값, DPI를 알면 0.12인치 중 큰 것</summary>
        static float Slop()
        {
            var system = EventSystem.current;
            float threshold = system != null ? system.pixelDragThreshold : 10f;
            float dpi = Screen.dpi;
            float fromDpi = dpi > 1f ? dpi * SlopInches : Mathf.Max(Screen.width, Screen.height) * 0.015f;
            return Mathf.Max(threshold, fromDpi);
        }

        // ───────────────────────────── 프레임 ─────────────────────────────

        bool Tick()
        {
            float dt = Time.unscaledDeltaTime;
            if (_holding) StepHold(dt);
            bool pill = StepPill(dt);
            return _holding || pill;
        }

        void StepHold(float dt)
        {
            if (_ev == null || !_button.IsInteractable())
            {
                EndHold();
                return;
            }
            if (_ev.dragging || (_ev.position - _ev.pressPosition).sqrMagnitude > _slopSq)
            {
                EndHold();
                return;
            }

            if (_repeating) _held += dt;
            _wait -= dt;
            if (_wait > 0f) return;

            if (!_repeating)
            {
                // 첫 반복: 이 누름은 더 이상 클릭이 아니다 (떼어도 한 번 더 오르지 않게)
                _repeating = true;
                _ev.eligibleForClick = false;
            }
            float interval = UIMotion.HoldInterval(_held);
            // 긴 프레임 뒤에 몰아서 여러 번 하지 않게 밀린 시간은 한 간격까지만 둔다
            _wait = Mathf.Max(_wait, -interval) + interval;

            bool repeated;
            try
            {
                repeated = Repeat();
            }
            catch (Exception e)
            {
                // 소비자 코드(Step · onClick)가 던졌다: 이 누르기를 끝내고 알약도 바로 숨긴다 (막힘이 아니므로 Denied는 없다)
                Debug.LogException(e, this);
                EndHold();
                HidePill();
                return;
            }
            if (repeated) return;
            EndHold();
            try
            {
                Denied?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        bool Repeat()
        {
            if (Step == null)
            {
                // 예전 방식: 성공을 알 수 없으니 반복만 하고 연속 알약 · Combo는 세지 않는다 (막혀도 "+N"이 오르지 않게)
                _button.onClick.Invoke();
                return true;
            }
            if (!Step()) return false;
            _combo++;
            if (_combo >= 2 && _holding) ShowPill();
            return true;
        }

        // ───────────────────────────── 연속 알약 ─────────────────────────────

        void ShowPill()
        {
            if (_pill == null && !CreatePill()) return;
            if (!_pill.gameObject.activeSelf)
            {
                _pill.gameObject.SetActive(true);
                _pill.SetAsLastSibling();
            }
            _pillGroup.alpha = 1f;
            _pillLinger = -1f;
            _pillText.SetText("+{0}", _combo);
            // 튀는 중이면 끝까지 둔다: 반복이 튀기(0.12초)보다 빠르면 튀기 한 번이 끝날 때마다 다시 튄다 (매 반복 처음부터면 떨린다)
            if (_pillPunch < 0f) _pillPunch = 0f;
        }

        void HidePill()
        {
            _pillPunch = -1f;
            _pillLinger = -1f;
            if (_pill == null) return;
            _pill.localScale = Vector3.one;
            if (_pill.gameObject.activeSelf) _pill.gameObject.SetActive(false);
        }

        bool StepPill(float dt)
        {
            if (_pill == null || !_pill.gameObject.activeSelf) return false;
            bool busy = false;
            if (_pillPunch >= 0f)
            {
                _pillPunch += dt;
                float k = Mathf.Clamp01(_pillPunch / UIMotion.ComboPunch);
                _pill.localScale = Vector3.one * (1f + (UIMotion.ComboPunchScale - 1f) * Mathf.Sin(k * Mathf.PI));
                if (k < 1f) busy = true;
                else
                {
                    _pill.localScale = Vector3.one;
                    _pillPunch = -1f;
                }
            }
            if (_pillLinger >= 0f)
            {
                _pillLinger += dt;
                float fade = (_pillLinger - UIMotion.ComboHold) / UIMotion.ComboFade;
                if (fade >= 1f)
                {
                    HidePill();
                    return false;
                }
                _pillGroup.alpha = 1f - Mathf.Clamp01(fade);
                busy = true;
            }
            return busy;
        }

        /// <summary>연속 알약을 처음 한 번 만든다 (버튼 자식, 꺼 둔 채)</summary>
        bool CreatePill()
        {
            var host = transform as RectTransform;
            if (host == null) return false;

            var go = new GameObject(ComboName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement))
            {
                layer = gameObject.layer,
            };
            var rect = (RectTransform)go.transform;
            rect.SetParent(host, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ComboSize;
            rect.anchoredPosition = _comboOffset;
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            SkinPill(image);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)) { layer = gameObject.layer };
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<TextMeshProUGUI>();
            // 버튼 글자와 같은 글꼴 (기본 재질 — 외곽선 없음)
            var source = GetComponentInChildren<TMP_Text>(true);
            if (source != null && source.font != null) text.font = source.font;
            text.fontSize = ComboFontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = UITheme.Ink;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.raycastTarget = false;

            _pill = rect;
            _pillGroup = group;
            _pillText = text;
            go.SetActive(false);
            return true;
        }

        /// <summary>금 알약: 지정 그림 → 흰 알약 아트 Gold 틴트 (NEW 알약과 같은 모양) → 금 버튼 아트 → 단색 Gold</summary>
        void SkinPill(Image image)
        {
            var sprite = _comboSprite;
            var color = Color.white;
            if (sprite == null)
            {
                sprite = UIFind.LoadedSprite(UIFind.ArtPillWhite);
                if (sprite != null) color = UITheme.Gold;
                else
                {
                    sprite = UIFind.LoadedSprite(UIFind.ArtButtonGold);
                    if (sprite == null) color = UITheme.Gold;
                }
            }
            UIFind.SkinCapsule(image, sprite, ComboSize.y, color);
        }
    }
}
