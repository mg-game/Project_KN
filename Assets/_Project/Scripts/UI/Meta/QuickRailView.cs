using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 화면 빠른 아이콘 줄 (UX-SPEC-IA §3.3) — Battle/MetaHud/QuickRail_L · QuickRail_R (Stretch = StageArea)에 하나씩, 같은 루트에 SheetFade 0
    /// (시트가 열리면 사라진다). 한쪽 칸: 위 주머니 1칸 At(TopLeft/TopRight, (±12, −306)) + 아래 2칸 At(BottomLeft/BottomRight, (±12, 412) · (±12, 288)),
    /// 칸 104x116 (UISize.Rail* — PlaceSlot이 놓는다. 칸 피벗은 가운데라 누름 크기 · 나타남이 가운데 기준).
    /// 보이는 아이콘(QuickIconView.IsAvailable)을 _icons 순서(= 우선순위: 이벤트 &gt; 출석부 &gt; 수선 일지 &gt; 나중 것)로 놓는다:
    /// 위 주머니 아이콘 하나가 1칸 (둘째부터는 아래 묶음), 아래 아이콘은 아래부터 (3칸 → 2칸). 한쪽 최대 3개 (+ 바구니 위젯).
    /// 아래가 2개를 넘으면 2칸이 '더보기'(위로 돌린 꺾쇠, 숨은 아이콘 중 받을 것(RedDotSystem.IsClaimOn)이 있는 수 배지)가 되고,
    /// 누르면 그 위로 쌓이는 쟁반(PanelNight9s @0.94, 폭 128, 줄 간격 124, 4줄까지 보이고 그 위는 스크롤)을 연다.
    /// 쟁반 줄은 대리 칸이다 — 원래 아이콘을 복제한 칸(같은 그림 · 이름 · 점 경로 · 할 일)이고, 넘친 원래 아이콘은 줄 아래 꺼진 채 남는다 (경로 고정, CR-11).
    /// 쟁반은 바깥 누름(TrayBlocker) · 뒤로(UIManager 뒤로 처리기) · 줄 누름 · 시트 열림(UIFrame.SheetStateChanged)에 닫힌다.
    /// StageArea(= 이 줄의 높이)가 1500px보다 짧으면(4:3 태블릿) 위 주머니 아이콘도 아래 묶음으로 간다 (넘치면 쟁반). v1에서는 쟁반이 보이지 않는다.
    /// 알림으로만 돈다 (Features.Changed · Tutorial.Finished · RedDots.Changed(더보기 배지만) · 크기 바뀜 · Start — Update 없음).
    /// 처음 놓은 뒤 기능이 열려 새로 나타난 아이콘은 BadgeAppear(0.22초 OutBack)로 튄다. 그 기능의 개방 카드가 날아올 차례면(QuickIconView.WantsHold)
    /// 자리에 놓인 채 그림만 숨겨 두었다가 카드 그림이 닿거나 카드를 건너뛸 때 튄다 (M78 · CR-24 — 칸 사각형은 바꾸지 않는다).
    /// 놓은 아이콘(줄 + 쟁반)의 점 경로가 바뀌면 Changed — 사냥 버튼 모아 보기(RedDotAnyView)가 PlacedDotPaths를 다시 읽는다 (CR-07).
    /// </summary>
    public class QuickRailView : MonoBehaviour
    {
        /// <summary>StageArea가 이보다 짧으면 위 주머니 아이콘도 아래 묶음으로 간다 (4:3 태블릿)</summary>
        public const float ShortStageHeight = 1500f;
        /// <summary>더보기 쟁반 폭 (칸 104 + 양옆 12)</summary>
        public const float TrayWidth = 128f;
        /// <summary>쟁반에 한 번에 보이는 줄 (넘치면 스크롤)</summary>
        const int TrayVisibleRows = 4;
        /// <summary>쟁반 안쪽 위아래 여백</summary>
        const float TrayPad = 6f;

        [Tooltip("오른쪽 줄 (QuickRail_R) — 칸을 화면 오른쪽 끝에 붙인다")]
        [SerializeField] bool _rightSide;
        [Tooltip("이 줄의 아이콘 (우선순위 순서 — 앞이 먼저 자리를 잡는다). 줄의 바로 아래 자식이다 (경로 고정)")]
        [SerializeField] QuickIconView[] _icons;
        [Tooltip("더보기 칸 (아래 아이콘이 2개를 넘을 때 2칸)")]
        [SerializeField] RectTransform _moreCell;
        [SerializeField] Button _moreButton;
        [Tooltip("더보기 배지 (숨은 아이콘 중 받을 것이 있는 수)")]
        [SerializeField] Badge _moreBadge;
        [Tooltip("더보기 쟁반 (스크롤 판, 더보기 칸 위로 쌓인다)")]
        [SerializeField] RectTransform _tray;
        [SerializeField] ScrollRect _trayScroll;
        [Tooltip("쟁반 안 줄 묶음 (아래 붙임 — 대리 칸이 여기 들어간다)")]
        [SerializeField] RectTransform _trayContent;
        [Tooltip("쟁반이 열린 동안 줄 전체를 덮는 투명 막 (바깥을 누르면 닫는다)")]
        [SerializeField] Button _trayBlocker;

        readonly List<string> _placedDotPaths = new();
        readonly List<string> _nextDotPaths = new();
        /// <summary>아래 묶음 (_icons 번호, 우선순위 순서)</summary>
        readonly List<int> _bottom = new();
        /// <summary>쟁반으로 넘친 아이콘 (_icons 번호, 우선순위 순서 — 쟁반 아래 줄부터)</summary>
        readonly List<int> _overflow = new();
        QuickIconView[] _proxies;
        /// <summary>지난번에 칸에 보였는지 (_icons 번호) — 새로 나타난 것만 튄다</summary>
        bool[] _visible;
        bool _laidOut;
        bool _short;
        bool _overflowing;
        bool _trayOpen;
        bool _subscribed;
        TutorialSystem _tutorial;
        Func<bool> _trayBack;

        /// <summary>놓은 아이콘(줄 + 쟁반)이 바뀜</summary>
        public event Action Changed;

        /// <summary>지금 놓은 아이콘(줄 + 쟁반)의 빨간 점 경로 (경로가 빈 아이콘은 빠진다). Changed 뒤에 읽는다</summary>
        public IReadOnlyList<string> PlacedDotPaths => _placedDotPaths;

        /// <summary>더보기 쟁반이 열려 있는지</summary>
        public bool IsTrayOpen => _trayOpen;

        /// <summary>짧은 화면인지 (이 줄 = StageArea 높이 &lt; 1500 — 위 주머니를 쓰지 않는다)</summary>
        public bool IsShortStage => ((RectTransform)transform).rect.height < ShortStageHeight;

        void Awake()
        {
            _trayBack = OnTrayBack;
            if (_moreButton != null) _moreButton.onClick.AddListener(ToggleTray);
            if (_trayBlocker != null) _trayBlocker.onClick.AddListener(CloseTray);
            HideTray();
        }

        void OnEnable() => UIFrame.SheetStateChanged += OnSheetStateChanged;

        void OnDisable()
        {
            UIFrame.SheetStateChanged -= OnSheetStateChanged;
            CloseTray();
        }

        void Start()
        {
            if (MetaUi.IsActive)
            {
                Game.Features.Changed += OnFeaturesChanged;
                Game.RedDots.Changed += RefreshMoreBadge;
                _tutorial = Game.Tutorial;
                if (_tutorial != null) _tutorial.Finished += OnFeaturesChanged;
                _subscribed = true;
            }
            Layout(false);
        }

        void OnDestroy()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (_tutorial != null) _tutorial.Finished -= OnFeaturesChanged;
            _tutorial = null;
            if (!Game.IsReady || Game.Features == null) return;
            Game.Features.Changed -= OnFeaturesChanged;
            if (Game.RedDots != null) Game.RedDots.Changed -= RefreshMoreBadge;
        }

        void OnRectTransformDimensionsChange()
        {
            // 화면 높이가 바뀌어 짧은 화면 규칙이 달라졌을 때만 다시 놓는다 (처음 놓기 전에는 Start가 놓는다)
            if (_laidOut && IsShortStage != _short) Layout(false);
        }

        void OnFeaturesChanged() => Layout(true);

        void OnSheetStateChanged(bool open)
        {
            if (open) CloseTray();
        }

        /// <summary>아이콘을 다시 놓는다 (기능 열림 · 화면 높이가 바뀜). 처음 놓은 뒤라면 새로 보이는 아이콘이 튄다</summary>
        public void Refresh() => Layout(_laidOut);

        // ───────────── 놓기 ─────────────

        void Layout(bool animate)
        {
            int count = _icons != null ? _icons.Length : 0;
            if (_visible == null || _visible.Length != count) _visible = new bool[count];
            _short = IsShortStage;
            _bottom.Clear();
            _overflow.Clear();
            _nextDotPaths.Clear();

            // ① 열린 아이콘을 우선순위대로: 위 주머니 하나 + 아래 묶음 (짧은 화면이면 모두 아래 묶음)
            int top = -1;
            for (int i = 0; i < count; i++)
            {
                var icon = _icons[i];
                if (icon == null || !icon.IsAvailable) continue;
                if (!string.IsNullOrEmpty(icon.DotPath)) _nextDotPaths.Add(icon.DotPath);
                if (top < 0 && icon.TopPocket && !_short) top = i;
                else _bottom.Add(i);
            }

            // ② 아래 묶음: 3칸 = 첫째, 2칸 = 둘째. 셋 이상이면 2칸은 더보기, 둘째부터 쟁반
            _overflowing = _bottom.Count > 2;
            int slot3 = _bottom.Count > 0 ? _bottom[0] : -1;
            int slot2 = _bottom.Count == 2 ? _bottom[1] : -1;
            if (_overflowing)
            {
                for (int i = 1; i < _bottom.Count; i++) _overflow.Add(_bottom[i]);
            }

            // ③ 칸에 놓는다 — 자리가 없는(잠김 · 넘침) 아이콘은 꺼진 채 이 줄 아래 남는다 (CR-11)
            for (int i = 0; i < count; i++)
            {
                var icon = _icons[i];
                if (icon == null) continue;
                int slot = i == top ? 1 : i == slot2 ? 2 : i == slot3 ? 3 : 0;
                bool visible = slot != 0;
                if (visible) PlaceSlot((RectTransform)icon.transform, slot, _rightSide);
                if (icon.gameObject.activeSelf != visible) icon.gameObject.SetActive(visible);
                if (visible && animate && !_visible[i])
                {
                    // 개방 카드가 이 자리로 날아올 차례면 그림만 숨겨 두었다가 닿을 때 나타난다 (M78) — 칸은 이미 제자리
                    if (icon.WantsHold()) icon.HoldThenAppear();
                    else icon.PlayAppear();
                }
                _visible[i] = visible;
            }

            if (_moreCell != null)
            {
                if (_overflowing) PlaceSlot(_moreCell, 2, _rightSide);
                if (_moreCell.gameObject.activeSelf != _overflowing) _moreCell.gameObject.SetActive(_overflowing);
            }
            if (!_overflowing) CloseTray();
            else if (_trayOpen) LayoutTray();
            RefreshMoreBadge();
            _laidOut = true;

            if (SamePaths(_placedDotPaths, _nextDotPaths)) return;
            _placedDotPaths.Clear();
            _placedDotPaths.AddRange(_nextDotPaths);
            Changed?.Invoke();
        }

        /// <summary>
        /// 칸 자리 (이 줄 = StageArea 기준. 1 = 위 주머니 · 2 = 아래 둘째 · 3 = 아래 맨 끝). §3.3 표의 At(…) 사각형과 같은 자리에 피벗만 가운데로 둔다
        /// (누름 크기 · 나타남이 가운데 기준). 빌더도 이것으로 v1 기본 자리를 잡는다. 값이 같으면 쓰지 않는다
        /// </summary>
        public static void PlaceSlot(RectTransform cell, int slot, bool rightSide)
        {
            if (cell == null) return;
            float side = rightSide ? 1f : 0f;
            float x = (UISize.RailEdge + UISize.RailCellWidth * 0.5f) * (rightSide ? -1f : 1f);
            float halfHeight = UISize.RailCellHeight * 0.5f;
            Vector2 anchor;
            float y;
            if (slot <= 1)
            {
                anchor = new Vector2(side, 1f);
                y = -(UISize.RailTopOffset + halfHeight);
            }
            else
            {
                anchor = new Vector2(side, 0f);
                y = UISize.RailBottomOffset + (slot == 2 ? UISize.RailPitch : 0f) + halfHeight;
            }
            Place(cell, anchor, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(UISize.RailCellWidth, UISize.RailCellHeight));
        }

        /// <summary>쟁반 자리: 더보기 칸(2칸) 위 끝에서 위로 쌓인다 (칸 열 가운데, 폭 128, 높이 = 보이는 줄 수만큼)</summary>
        public static void PlaceTray(RectTransform tray, bool rightSide, float height)
        {
            if (tray == null) return;
            float side = rightSide ? 1f : 0f;
            float x = (UISize.RailEdge + UISize.RailCellWidth * 0.5f) * (rightSide ? -1f : 1f);
            float bottom = UISize.RailBottomOffset + UISize.RailPitch + UISize.RailCellHeight;
            Place(tray, new Vector2(side, 0f), new Vector2(0.5f, 0f), new Vector2(x, bottom), new Vector2(TrayWidth, height));
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (rect.anchorMin != anchor) rect.anchorMin = anchor;
            if (rect.anchorMax != anchor) rect.anchorMax = anchor;
            if (rect.pivot != pivot) rect.pivot = pivot;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        }

        static bool SamePaths(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        // ───────────── 더보기 ─────────────

        /// <summary>더보기 배지 = 쟁반으로 숨은 아이콘 중 받을 것(IsClaimOn — 새로 열림 잎은 세지 않는다)이 있는 수. 넘치지 않으면 숨긴다</summary>
        void RefreshMoreBadge()
        {
            if (_moreBadge == null) return;
            int count = 0;
            if (_overflowing && MetaUi.IsActive)
            {
                var dots = Game.RedDots;
                for (int i = 0; i < _overflow.Count; i++)
                {
                    string path = _icons[_overflow[i]].DotPath;
                    if (!string.IsNullOrEmpty(path) && dots.IsClaimOn(path)) count++;
                }
            }
            if (count > 0) _moreBadge.SetCount(count);
            else if (_moreBadge.gameObject.activeSelf) _moreBadge.Hide();
        }

        void ToggleTray()
        {
            if (_trayOpen) CloseTray();
            else OpenTray();
        }

        void OpenTray()
        {
            if (_trayOpen || _tray == null || _trayContent == null || _overflow.Count == 0) return;
            _trayOpen = true;
            if (_trayBlocker != null) _trayBlocker.gameObject.SetActive(true);
            _tray.gameObject.SetActive(true);
            LayoutTray();
            var ui = UIManager.Instance;
            if (ui != null) ui.PushBackHandler(_trayBack);
        }

        /// <summary>쟁반을 닫는다 (바깥 누름 · 뒤로 · 줄 누름 · 시트 열림 · 넘침이 끝남). 닫혀 있으면 아무것도 하지 않는다</summary>
        public void CloseTray()
        {
            if (!_trayOpen) return;
            _trayOpen = false;
            HideTray();
            var ui = UIManager.Instance;
            if (ui != null) ui.RemoveBackHandler(_trayBack);
        }

        void HideTray()
        {
            if (_tray != null && _tray.gameObject.activeSelf) _tray.gameObject.SetActive(false);
            if (_trayBlocker != null && _trayBlocker.gameObject.activeSelf) _trayBlocker.gameObject.SetActive(false);
        }

        bool OnTrayBack()
        {
            if (!_trayOpen) return false;
            CloseTray();
            return true;
        }

        /// <summary>쟁반 줄: 넘친 아이콘의 대리 칸을 아래부터 124 간격으로 쌓는다 (4줄을 넘으면 스크롤)</summary>
        void LayoutTray()
        {
            int rows = _overflow.Count;
            if (rows == 0) return;
            if (_proxies == null || _proxies.Length != _icons.Length) _proxies = new QuickIconView[_icons.Length];
            float pitch = UISize.RailPitch;
            float cell = UISize.RailCellHeight;
            float contentHeight = TrayPad * 2f + (rows - 1) * pitch + cell;
            int shown = Mathf.Min(rows, TrayVisibleRows);
            float viewHeight = TrayPad * 2f + (shown - 1) * pitch + cell;
            PlaceTray(_tray, _rightSide, viewHeight);
            var size = new Vector2(_trayContent.sizeDelta.x, contentHeight);
            if (_trayContent.sizeDelta != size) _trayContent.sizeDelta = size;

            // 지금 넘친 아이콘의 대리 칸만 켠다
            for (int i = 0; i < _proxies.Length; i++)
            {
                var proxy = _proxies[i];
                if (proxy != null && proxy.gameObject.activeSelf && !_overflow.Contains(i)) proxy.gameObject.SetActive(false);
            }
            for (int row = 0; row < rows; row++)
            {
                var proxy = ProxyOf(_overflow[row]);
                if (proxy == null) continue;
                Place((RectTransform)proxy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, TrayPad + row * pitch + cell * 0.5f), new Vector2(UISize.RailCellWidth, cell));
                if (!proxy.gameObject.activeSelf) proxy.gameObject.SetActive(true);
            }
            if (_trayScroll != null)
            {
                _trayScroll.StopMovement();
                _trayScroll.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// 넘친 아이콘의 대리 칸 (처음 한 번 복제해 둔다): 같은 그림 · 이름 · 점 경로 · 할 일, 누르면 쟁반을 닫고 그 아이콘의 할 일을 한다.
        /// 이름에 Proxy를 붙여 고정 경로(QuickRail_L/EventButton 등)와 겹치지 않게 한다 (CR-11)
        /// </summary>
        QuickIconView ProxyOf(int index)
        {
            var proxy = _proxies[index];
            if (proxy != null) return proxy;
            var source = _icons[index];
            if (source == null || _trayContent == null) return null;
            proxy = Instantiate(source, _trayContent);
            proxy.name = source.name + "Proxy";
            var button = proxy.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(CloseTray);
            _proxies[index] = proxy;
            return proxy;
        }
    }
}
