using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 자동 불 밝히기 방식 고르기 (CR-02, SPEC4 §7.3): 불 밝히기 버튼을 0.5초(실제 시간) 길게 누르면 버튼 위에 밤하늘 판 360×(3×88+16) —
    /// '알아서' · '보스까지 아끼기' · '직접' (고른 줄은 금테 + 체크). 줄을 누르면 Game.Battle.LampPolicy에 넣고 '불 밝히기: {이름}' 토스트 후 닫힌다.
    /// 판 밖(투명 덮개)을 누르면 닫힌다. 끌거나(16px) 손가락이 버튼 밖으로 나가거나 앱이 포커스를 잃으면 길게 누르기를 취소한다.
    /// 짧게 누르면 지금처럼 불을 켠다 (길게 누른 뒤의 눌림은 LampButtonView가 ConsumeClick으로 버린다).
    /// 잠김(1-6 전)이면 길게 누를 때 '스테이지 1-6을 클리어하면 열려요' (오류 토스트 + 가벼운 진동). 튜토리얼 중에는 길게 누르기가 없다.
    /// 버튼 오른쪽 위 32 아이콘(40 둥근 받침)이 지금 방식을 보여 준다 (Auto는 숨김). 빌더가 만들기 전(사용자 씬)에는 없다.
    /// <para>
    /// 그리는 순서 (검증 rm2 r1): 고르기 층(LampPicker)은 Battle의 자식인데, 메타 HUD(MetaHud — 빠른 아이콘 줄 '수선 일지' 칸)는 나중에
    /// 같은 Battle 아래 뒤 형제로 만들어진다. 그대로면 오른쪽 아이콘 칸이 판 위에 그려지고 줄의 누름도 가로챘다. 그래서 열 때마다 고르기 층을
    /// Battle의 맨 뒤 형제로 올린다(RaiseToTop — 이미 맨 뒤면 그대로). 시트 · 팝업 · 토스트 · 튜토리얼은 Battle 밖 뒤 형제라 계속 판 위에 뜬다.
    /// 판 크기는 WindowWidth × WindowHeight (설명 '보스가 나오거나 수선점이 위험할 때만 써요'가 16px 하한에서도 설명 칸 안에 들게 SPEC4의 360보다 넓다).
    /// </para>
    /// </summary>
    public class LampPolicyPicker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] GameObject _panel;
        [SerializeField] Button _catcher;
        [SerializeField] Button[] _rows;
        [SerializeField] Image[] _outlines;
        [SerializeField] Image[] _checks;
        [SerializeField] GameObject _corner;
        [SerializeField] Image _cornerIcon;
        [Tooltip("방식별 모서리 아이콘 (0 알아서 — 쓰지 않음, 1 보스까지 아끼기, 2 직접)")]
        [SerializeField] Sprite[] _cornerSprites;
        [Tooltip("보이는 밤하늘 판 (줄 세 개를 담은 창 — _panel은 판 밖 투명 덮개까지 포함한 층). 비어 있으면 _panel 아래 'Panel'")]
        [SerializeField] RectTransform _window;

        public const float HoldSeconds = 0.5f;
        public const float DragCancel = 16f;
        /// <summary>판 폭 (SPEC4 §7.3의 360은 둘째 설명이 16px 하한에서 ≈ 270px라 설명 칸 226을 넘쳐 체크 · 판 테두리에 닿았다 → 440, 설명 칸 306)</summary>
        public const float WindowWidth = 440f;
        /// <summary>줄 높이 · 판 안쪽 여백 (판 높이 = 3 × RowHeight + 2 × WindowPad)</summary>
        public const float RowHeight = 88f;
        public const float WindowPad = 8f;
        public const float WindowHeight = 3 * RowHeight + 2 * WindowPad;
        /// <summary>줄 안 글자 칸: 왼쪽 아이콘 자리 · 오른쪽 체크 자리 (설명 칸 폭 = WindowWidth − 2 × WindowPad − DescLeft − DescRight)</summary>
        public const float DescLeft = 74f;
        public const float DescRight = 44f;
        public static float DescWidth => WindowWidth - 2f * WindowPad - DescLeft - DescRight;
        public static readonly string[] Names = { "알아서", "보스까지 아끼기", "직접" };
        public static readonly string[] Descriptions =
        {
            "적이 많거나 위험할 때 바로 써요",
            "보스가 나오거나 수선점이 위험할 때만 써요",
            "AUTO가 켜져 있어도 직접 눌러야 써요",
        };

        bool _holding;
        /// <summary>누르고 있는 손가락 (입력 모듈이 매 프레임 자리를 고쳐 쓰는 같은 객체 — 끌기 판정에 읽기만 한다. 끌기 처리기를 달면 짧은 눌림이 끌기로 바뀌어 불이 안 켜질 수 있다)</summary>
        PointerEventData _press;
        float _holdStart;
        Vector2 _holdPos;
        bool _consume;
        int _shownPolicy = -1;
        bool _wired;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        /// <summary>보이는 판 (검증이 잰다 — 판 밖 덮개 _panel이 아닌 창). 없으면 null</summary>
        public RectTransform Window
        {
            get
            {
                if (_window != null) return _window;
                return _panel != null ? _panel.transform.Find("Panel") as RectTransform : null;
            }
        }

        void Awake()
        {
            Wire();
            if (_panel != null) _panel.SetActive(false);
        }

        void Wire()
        {
            if (_wired) return;
            _wired = true;
            if (_catcher != null) _catcher.onClick.AddListener(Close);
            for (int i = 0; _rows != null && i < _rows.Length; i++)
            {
                int policy = i;
                if (_rows[i] != null) _rows[i].onClick.AddListener(() => Pick(policy));
            }
        }

        /// <summary>길게 누른 뒤 따라오는 눌림을 한 번 버린다 (LampButtonView.OnClick이 묻는다)</summary>
        public bool ConsumeClick()
        {
            if (!_consume) return false;
            _consume = false;
            return true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _consume = false;
            if (TutorialActive) return;
            _holding = true;
            _press = eventData;
            _holdStart = Time.unscaledTime;
            _holdPos = eventData != null ? eventData.position : Vector2.zero;
        }

        public void OnPointerUp(PointerEventData eventData) => StopHold();

        public void OnPointerExit(PointerEventData eventData) => StopHold();

        void StopHold()
        {
            _holding = false;
            _press = null;
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) StopHold();
        }

        static bool TutorialActive => Game.IsReady && Game.Tutorial != null && Game.Tutorial.IsActive;

        void Update()
        {
            if (_holding && _press != null && (_press.position - _holdPos).sqrMagnitude > DragCancel * DragCancel) StopHold();
            if (_holding && Time.unscaledTime - _holdStart >= HoldSeconds)
            {
                StopHold();
                _consume = true;
                OnLongPress();
            }
            int policy = Game.IsReady && Game.Battle != null ? (int)Game.Battle.EffectiveLampPolicy : 0;
            if (policy != _shownPolicy) ShowCorner(policy);
            if (IsOpen && (TutorialActive || UIFrame.SheetOpen)) Close();
        }

        void OnLongPress()
        {
            if (!Game.IsReady || Game.Battle == null) return;
            if (!Game.Battle.IsLampPolicyUnlocked)
            {
                Haptics.Play(HapticKind.Light);
                var ui = UIManager.Instance;
                if (ui != null) ui.ShowToast(MetaText.UnlockSentence(7), ToastKind.Error);
                return;
            }
            Open();
        }

        public void Open()
        {
            Wire();
            if (_panel == null) return;
            RaiseToTop(_panel.transform);
            _panel.SetActive(true);
            MarkSelected(Game.IsReady && Game.Battle != null ? (int)Game.Battle.LampPolicy : (int)LampPolicy.Auto);
            UISfx.Play(UISfxKey.Tap, 0.5f);
        }

        /// <summary>
        /// 층을 부모의 맨 뒤 형제로 올린다 (나중에 만들어진 메타 HUD · 빠른 아이콘 줄 위에 그리고 누름도 먼저 받게). 이미 맨 뒤면 건드리지 않는다
        /// (캔버스를 괜히 다시 짜지 않게). 움직였으면 true
        /// </summary>
        internal static bool RaiseToTop(Transform layer)
        {
            if (layer == null) return false;
            var parent = layer.parent;
            if (parent == null || layer.GetSiblingIndex() == parent.childCount - 1) return false;
            layer.SetAsLastSibling();
            return true;
        }

        public void Close()
        {
            if (_panel != null && _panel.activeSelf) _panel.SetActive(false);
        }

        void Pick(int policy)
        {
            if (Game.IsReady && Game.Battle != null) Game.Battle.LampPolicy = (LampPolicy)Mathf.Clamp(policy, 0, 2);
            MarkSelected(policy);
            var ui = UIManager.Instance;
            if (ui != null) ui.ShowToast("불 밝히기: " + Names[Mathf.Clamp(policy, 0, Names.Length - 1)], ToastKind.Info);
            Close();
        }

        void MarkSelected(int policy)
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i == policy;
                if (_outlines != null && i < _outlines.Length && _outlines[i] != null) _outlines[i].enabled = on;
                if (_checks != null && i < _checks.Length && _checks[i] != null) _checks[i].enabled = on;
            }
        }

        void ShowCorner(int policy)
        {
            _shownPolicy = policy;
            if (_corner == null) return;
            bool on = policy != (int)LampPolicy.Auto;
            if (_corner.activeSelf != on) _corner.SetActive(on);
            if (on && _cornerIcon != null && _cornerSprites != null && policy < _cornerSprites.Length && _cornerSprites[policy] != null)
                _cornerIcon.sprite = _cornerSprites[policy];
        }
    }
}
