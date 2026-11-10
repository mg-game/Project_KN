using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 빨간 점 트리(RedDotSystem)의 한 경로를 Badge에 보여 준다 (F-04 · M09, UX-SPEC §3.13). 아무 버튼에나 붙인다.
    ///   · 숫자 모드(_showCount)는 개수 잎(우편, 일지)의 합을 숫자로, 아니면 점.
    ///   · 받을 것이 없고 "새로 열림"(FeatureDot 잎)만 켠 경로는 점 대신 NEW 알약 (isNew = IsOn &amp;&amp; !IsClaimOn — 빨간 점 · 숫자는 받을 것에만, §3.13.3).
    ///   · Suppressed면 숨긴다 — 탭 바의 선택된 탭(TabGateView, §3.13.1: 고른 탭에는 배지가 없다).
    ///   · _mainRole이 None이 아니면(탭 · 빠른 아이콘 · 바구니 — MetaSceneBuilder가 정한다) MAIN 배지 예산(MainBadgeBudget, M09 · UX-SPEC-IA §8.5)을 따른다:
    ///     MAIN에는 배지 묶음 5개까지(받을 것 먼저), NEW 알약은 화면 전체에 하나(가장 최근에 열린 기능) — 나머지 NEW(탭 포함)는 점도 없이 쉰다.
    ///     예산 밖(None — 메뉴 칸 · 팝업 · 상단 바)은 예전처럼 늘 보인다.
    ///   · 켜져 있는 동안 점이 새로 켜지면 RedDotNew 소리 (UISfxGate가 2초에 한 번으로 묶는다 — 켜질 때 · 다시 보일 때는 울리지 않는다).
    /// RedDots.Changed가 올 때만 다시 읽는다 (매 프레임 아님 — 예산 참여 뷰는 Features.Changed · UIFrame.SheetStateChanged에도). 메타가 꺼져 있으면 점을 숨긴다.
    /// 직렬화 이름 _path · _badge · _showCount는 FrameSceneBuilder · SettingsSceneBuilder · MetaSceneBuilder가 묶는다 (계약 C-14 — 바꾸지 않는다).
    /// </summary>
    public class RedDotView : MonoBehaviour
    {
        [SerializeField] string _path;
        [SerializeField] Badge _badge;
        [Tooltip("개수 잎을 숫자로 보여 준다 (수선 일지, 우편)")]
        [SerializeField] bool _showCount;
        [Tooltip("MAIN 배지 예산에서의 자리 (None = 예산 밖 — 메뉴 칸 · 팝업 · 상단 바는 늘 보인다)")]
        [SerializeField] MainBadgeRole _mainRole;

        bool _subscribed;
        /// <summary>예산 참여 구독(Features.Changed · SheetStateChanged · 예산 명부)을 걸었는지</summary>
        bool _budgetHooked;
        bool _suppressed;
        /// <summary>켜진 뒤 첫 Refresh를 지났는지 (그 전 상태 변화는 소리 없이)</summary>
        bool _primed;
        /// <summary>마지막으로 읽은 점 상태 (숨김과 상관없이 — 다시 보일 때 소리를 내지 않게)</summary>
        bool _wasOn;
        Action<bool> _onSheetStateChanged;

        public string Path => _path;

        /// <summary>MAIN 배지 예산에서의 자리 (None = 예산 밖)</summary>
        public MainBadgeRole MainRole => _mainRole;

        /// <summary>
        /// 점을 잠시 숨긴다 (경로 상태는 그대로 읽어 둔다). 탭 바의 고른 탭 — TabGateView가 고름이 바뀔 때 켜고 끈다.
        /// 바뀔 때만 다시 그린다
        /// </summary>
        public bool Suppressed
        {
            get => _suppressed;
            set
            {
                if (_suppressed == value) return;
                _suppressed = value;
                Refresh();
            }
        }

        /// <summary>경로를 바꾼다 (같은 뷰를 다른 대상에 재사용할 때)</summary>
        public void SetPath(string path)
        {
            _path = path;
            _primed = false;
            Refresh();
        }

        void OnEnable()
        {
            if (MetaUi.IsActive && !_subscribed)
            {
                Game.RedDots.Changed += Refresh;
                if (_mainRole != MainBadgeRole.None)
                {
                    // 예산은 기능 개방(잠금 덮개 NEW) · 시트 열림에도 바뀐다 — 그때도 스스로 다시 묻는다
                    _onSheetStateChanged ??= OnSheetStateChanged;
                    if (Game.Features != null) Game.Features.Changed += Refresh;
                    UIFrame.SheetStateChanged += _onSheetStateChanged;
                    MainBadgeBudget.Register(this);
                    _budgetHooked = true;
                }
                _subscribed = true;
            }
            _primed = false;
            Refresh();
        }

        void OnDisable() => Unsubscribe();

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (_budgetHooked)
            {
                _budgetHooked = false;
                UIFrame.SheetStateChanged -= _onSheetStateChanged;
                MainBadgeBudget.Unregister(this);
                if (Game.IsReady && Game.Features != null) Game.Features.Changed -= Refresh;
            }
            if (Game.IsReady && Game.RedDots != null) Game.RedDots.Changed -= Refresh;
        }

        void OnSheetStateChanged(bool open) => Refresh();

        public void Refresh()
        {
            if (_badge == null) return;
            if (!MetaUi.IsActive || string.IsNullOrEmpty(_path))
            {
                _badge.SetDot(false);
                _wasOn = false;
                return;
            }
            var dots = Game.RedDots;
            bool on = dots.IsOn(_path);
            int count = _showCount ? dots.GetCount(_path) : 0;
            bool shown = on || count > 0;
            // 받을 것(점 · 숫자) — 아니면 "새로 열림"만 (NEW 알약)
            bool claim = count > 0 || (on && dots.IsClaimOn(_path));
            bool appeared = _primed && shown && !_wasOn;
            _wasOn = shown;
            _primed = true;
            if (_suppressed || (shown && _mainRole != MainBadgeRole.None && !MainBadgeBudget.Allows(this, claim)))
            {
                if (_badge.gameObject.activeSelf) _badge.Hide();
                return;
            }
            // 받을 것이 없는 "새로 열림"만이면 NEW 알약 (숫자가 있으면 숫자가 먼저)
            _badge.Show(on, count, on && !claim);
            if (appeared && isActiveAndEnabled) UISfx.Play(UISfxKey.RedDotNew);
        }
    }

    /// <summary>MAIN 배지 예산(MainBadgeBudget)에서 RedDotView의 자리 (MetaSceneBuilder가 정한다)</summary>
    public enum MainBadgeRole
    {
        /// <summary>예산 밖 (메뉴 칸 · 팝업 · 상단 바 — 늘 보인다)</summary>
        None = 0,
        /// <summary>하단 탭 (받을 것 점은 MAIN 예산 안에서, NEW는 화면 전체에 하나인 자리를 얻었을 때만 — 고른 탭은 TabGateView가 숨긴다)</summary>
        Tab = 1,
        /// <summary>
        /// 빠른 아이콘 (칸마다 한 묶음으로 센다 — 칸은 StageArea 귀퉁이에 떨어져 있다. 받을 것 점 · 숫자는 상단 바 다음으로 먼저 자리를 얻고,
        /// 줄 안 받을 것이 2개 이상이면 줄 NEW는 쉰다. 시트가 열려도 NEW 후보로 남는다: 이기면 NEW는 아무 데도 없다)
        /// </summary>
        Rail = 2,
        /// <summary>수선 바구니 위젯 (모서리 배지 하나: 점 &gt; '무료'(시간 알약 글자) &gt; NEW)</summary>
        Basket = 3,
    }

    /// <summary>
    /// MAIN 배지 예산 (M09 · UX-SPEC §3.13 · UX-SPEC-IA §8.5 — MAIN에 배지가 쏟아지지 않게 한 곳에서 고른다).
    ///   · MAIN(시트 닫힘)의 배지 묶음은 MaxMain(5)까지, 받을 것 먼저. 자리 하나 = 화면에서 떨어져 보이는 묶음 하나 (S-badges가 160px 안을 한 묶음으로 센다):
    ///     상단 바 우편 · ≡ 점(예산 밖이라 늘 보이지만 자리는 먼저 센다 — 두 단추가 104px 붙어 있어 둘 중 하나라도 켜지면 한 자리) →
    ///     빠른 아이콘 받을 것 칸(칸마다 한 자리 — 이벤트 L1 · 출석부 L3 · 일지 R3은 서로 900px 넘게 떨어져 있다, 명부 순서) → 바구니 점 →
    ///     탭 받을 것 점(강화 → 상점 순) → 마지막으로 NEW 하나. 셈은 순수 함수 Seat (V2 r4/r5: 이벤트 칸 점이 켜지자 줄을 한 자리로 세던 예산이
    ///     화면 묶음 6개를 냈다 — 우편+≡ · 이벤트 · 출석부 · 일지 · 탭 둘).
    ///   · NEW 알약은 화면 전체에 하나: 지금 보이는 NEW 후보 중 가장 최근에 열린 기능 (이번 실행에서 열린 차례 — FeatureGateSystem.Unlocked,
    ///     모르면 FeatureId 순서 = 진행 순서). 후보: 탭(고른 탭 빼고) · 빠른 아이콘(시트가 열려도 — 이기면 줄이 SheetFade 0이라 NEW가 화면에 없을 뿐, 다른 후보로 넘기지 않는다) ·
    ///     바구니('무료'가 시간 알약에 떠 있으면 빠진다) · 잠금 덮개(AUTO · x1). 진 NEW는 점도 없이 쉰다 (이긴 NEW를 보거나 그 시트를 열면 —
    ///     TabGateView가 처음 열 때 봤음 처리 — 다음 차례가 뜬다). 건너뛰기가 남긴 NEW 하나(§3.13.2 — 가이드가 가리키는 탭)는 그대로 MAIN에 보인다.
    ///     줄 NEW는 다른 칸이라 제 자리가 남아야 보인다 (받을 것 칸의 자리를 빌리지 않는다). 줄 안 받을 것이 MaxRailMarks(2) 이상이면 줄 NEW는 쉰다
    ///     (Wave 2 r2 이벤트 NEW · 출석 점 · 일지 '3' 세 개가 한꺼번에 떴다). 줄 안 받을 것 점 · 숫자는 상단 바 바로 다음에 앉아 줄지 않는다
    ///     (v1 칸 셋 + 상단 바 하나 = 4 ≤ 5) — MAIN에서 받을 보상을 알리는 곳은 그 칸뿐이라서다 (사냥 칸 모음 점은 시트가 열렸을 때만,
    ///     UX-SPEC-IA §2.3 · ≡ 점은 공지만). 자리가 모자라면 바구니 · 탭 점이 먼저 쉰다.
    ///   · 시트가 열린 동안 받을 것 점은 모두 보인다 (고른 탭만 숨김 — TabGateView).
    /// 상태를 들고 있지 않고 물을 때마다 빨간 점 트리 · 기능 개방 · 시트 상태에서 바로 계산한다 (사전 조회 몇십 번, 할당 없음).
    /// 참여 뷰(RedDotView · FeatureLockCover)는 RedDots.Changed · Features.Changed · UIFrame.SheetStateChanged마다 스스로 다시 묻고,
    /// 같은 상태에서는 모두 같은 답을 얻으므로 따로 알릴 필요가 없다.
    /// </summary>
    public static class MainBadgeBudget
    {
        /// <summary>MAIN에 보이는 배지 묶음 상한 (IA §8.5 M09 — 빠른 아이콘 줄이 한 묶음을 더해 4 → 5). 자리 하나 = 화면 묶음 하나</summary>
        public const int MaxMain = 5;

        static readonly List<RedDotView> Views = new();
        static readonly List<FeatureLockCover> Covers = new();
        static readonly FeatureId[] AllFeatures = (FeatureId[])Enum.GetValues(typeof(FeatureId));
        /// <summary>탭 바 순서(강화 · 편성 · 친구들 · 원정 · 소환 · 상점)의 탭 점 경로 — 받을 것 자리를 이 순서로 준다</summary>
        static readonly string[] TabPaths =
        {
            RedDotPaths.TabGrowth, RedDotPaths.TabFormation, RedDotPaths.TabCharacters,
            RedDotPaths.TabExpedition, RedDotPaths.TabGacha, RedDotPaths.TabShop,
        };
        /// <summary>FeatureId → 그 기능의 점이 올라가는 화면 경로 (RedDotPaths.ParentOf, 한 번만 만든다)</summary>
        static readonly string[] Parents = BuildParents();
        /// <summary>FeatureId → 이번 실행에서 열린 차례 (0 = 모름)</summary>
        static readonly int[] Order = new int[Parents.Length];
        static readonly Action<FeatureId> OnUnlockedHandler = OnUnlocked;
        static int s_counter;
        static FeatureGateSystem s_tracked;

        /// <summary>예산 참여 뷰 (RedDotView.OnEnable — _mainRole이 None이 아닐 때)</summary>
        public static void Register(RedDotView view)
        {
            if (view != null && !Views.Contains(view)) Views.Add(view);
        }

        public static void Unregister(RedDotView view) => Views.Remove(view);

        /// <summary>잠금 덮개 (AUTO · x1의 NEW 알약 후보)</summary>
        public static void Register(FeatureLockCover cover)
        {
            if (cover != null && !Covers.Contains(cover)) Covers.Add(cover);
        }

        public static void Unregister(FeatureLockCover cover) => Covers.Remove(cover);

        /// <summary>
        /// 이 배지가 지금 보여도 되는지. claim = 받을 것(점 · 숫자), 아니면 NEW 알약. 예산 밖 뷰 · 메타가 꺼져 있으면 늘 true. 할당 없음
        /// </summary>
        public static bool Allows(RedDotView view, bool claim)
        {
            if (view == null || view.MainRole == MainBadgeRole.None || !MetaUi.IsActive) return true;
            Track();
            bool sheet = UIFrame.SheetOpen;
            if (claim) return sheet || SeatClaims(view, out _);
            if (!ReferenceEquals(NewWinner(), view)) return false;
            int railClaims = view.MainRole == MainBadgeRole.Rail ? CountClaims(MainBadgeRole.Rail, Game.RedDots) : 0;
            int left = 0;
            if (!sheet) SeatClaims(null, out left);
            // 줄 NEW는 받을 것이 없는 다른 칸이다 — 칸마다 자리를 세므로 받을 것 칸의 자리를 빌리지 않는다 (railSeated = false)
            return WinnerShows(view.MainRole, sheet, railClaims, left, false);
        }

        /// <summary>잠금 덮개의 NEW 알약이 지금 보여도 되는지 (가장 최근 NEW이고 MAIN이면 자리가 남았을 때). 할당 없음</summary>
        public static bool AllowsNew(FeatureLockCover cover)
        {
            if (cover == null || !MetaUi.IsActive) return true;
            Track();
            bool sheet = UIFrame.SheetOpen;
            if (!ReferenceEquals(NewWinner(), cover)) return false;
            int left = 0;
            if (!sheet) SeatClaims(null, out left);
            return WinnerShows(MainBadgeRole.None, sheet, 0, left, false);
        }

        /// <summary>
        /// 줄 안 받을 것이 이만큼 켜져 있으면 빠른 아이콘 줄의 NEW는 쉰다 (Wave 2 r2 — 받을 것 먼저). 받을 것 점 · 숫자 자체는 줄이지 않는다:
        /// 받을 것이 셋이면 묶음 표시도 셋이다 (묶음 전체 상한이 아니다 — 이름은 예전 그대로)
        /// </summary>
        public const int MaxRailMarks = 2;

        /// <summary>
        /// 이긴 NEW가 보이는지 (NewWinner가 고른 뒤의 규칙, 할당 없는 순수 함수). role = 이긴 쪽 자리 (잠금 덮개는 None).
        ///   · 빠른 아이콘 줄의 NEW는 줄 안 받을 것이 MaxRailMarks 이상이면 쉰다 (받을 것 먼저 — 받을 것 점 · 숫자는 그대로 다 보인다).
        ///   · 시트가 열려 있으면 이긴 쪽 그대로 (빠른 아이콘이 이겼으면 줄은 SheetFade 0이라 화면에 NEW가 없다 — 다른 후보로 넘기지 않아
        ///     시트를 열고 닫을 때 NEW가 x1 ↔ 이벤트로 뛰지 않는다).
        ///   · MAIN이면 받을 것을 앉히고 남은 자리가 있거나, railSeated일 때 (예전 '줄 묶음 하나' 셈의 자리 — 칸마다 자리를 세는 지금은
        ///     Allows가 늘 false를 넘긴다: 줄 NEW 칸은 받을 것 칸과 다른 귀퉁이라 제 자리가 따로 든다).
        /// </summary>
        internal static bool WinnerShows(MainBadgeRole role, bool sheet, int railClaims, int left, bool railSeated)
        {
            if (role == MainBadgeRole.Rail && railClaims >= MaxRailMarks) return false;
            if (sheet) return true;
            return left > 0 || (role == MainBadgeRole.Rail && railSeated);
        }

        /// <summary>
        /// MAIN 자리 셈 (순수, 할당 없음 — 자리 하나 = 화면 묶음 하나). 차례: 상단 바(우편 · ≡ — 붙어 있어 켜진 쪽이 있으면 한 자리) →
        /// 빠른 아이콘 받을 것 칸(칸마다 한 자리) → 바구니 → 탭 받을 것(탭마다 한 자리). 반환값 = NEW에 남은 자리.
        /// railSeats = 앉은 빠른 아이콘 칸 수 (명부 앞쪽부터), basketSeat = 바구니가 앉았는지, tabSeats = 앉은 탭 수 (탭 바 순서 앞쪽부터)
        /// </summary>
        internal static int Seat(bool topBar, int railCells, bool basket, int tabs, out int railSeats, out bool basketSeat, out int tabSeats)
        {
            int budget = MaxMain;
            if (topBar) budget--;
            railSeats = Math.Max(0, Math.Min(railCells, budget));
            budget -= railSeats;
            basketSeat = basket && budget > 0;
            if (basketSeat) budget--;
            tabSeats = Math.Max(0, Math.Min(tabs, budget));
            budget -= tabSeats;
            return Math.Max(0, budget);
        }

        /// <summary>
        /// MAIN에서 받을 것 배지를 우선순위대로 앉힌다 (Seat 차례: 상단 바 → 빠른 아이콘 칸 → 바구니 → 탭). target이 자리를 얻었으면 true.
        /// left = NEW에 남은 자리. 할당 없음
        /// </summary>
        static bool SeatClaims(RedDotView target, out int left)
        {
            var dots = Game.RedDots;
            // 상단 바 (FrameSceneBuilder — 예산 밖이라 늘 보인다, 자리만 먼저 센다)
            bool topBar = dots.IsOn(RedDotPaths.TopMail) || dots.IsOn(RedDotPaths.TopSettings);
            int railCells = 0, railIndex = -1;
            for (int i = 0; i < Views.Count; i++)
            {
                var view = Views[i];
                if (view == null || view.MainRole != MainBadgeRole.Rail || view.Suppressed || !dots.IsClaimOn(view.Path)) continue;
                if (ReferenceEquals(view, target)) railIndex = railCells;
                railCells++;
            }
            bool basket = AnyClaim(MainBadgeRole.Basket, dots);
            int tabs = 0, tabIndex = -1;
            for (int t = 0; t < TabPaths.Length; t++)
            {
                var tab = FindTab(TabPaths[t]);
                if (tab == null || !dots.IsClaimOn(TabPaths[t])) continue;
                if (ReferenceEquals(tab, target)) tabIndex = tabs;
                tabs++;
            }
            left = Seat(topBar, railCells, basket, tabs, out int railSeats, out bool basketSeat, out int tabSeats);
            var role = target != null ? target.MainRole : MainBadgeRole.None;
            switch (role)
            {
                case MainBadgeRole.Rail: return railIndex >= 0 && railIndex < railSeats;
                case MainBadgeRole.Basket: return basketSeat;
                case MainBadgeRole.Tab: return tabIndex >= 0 && tabIndex < tabSeats;
                default: return false;
            }
        }

        /// <summary>그 자리의 (숨기지 않은) 뷰 중 받을 것이 켜진 것이 있는지</summary>
        static bool AnyClaim(MainBadgeRole role, RedDotSystem dots)
        {
            for (int i = 0; i < Views.Count; i++)
            {
                var view = Views[i];
                if (view == null || view.MainRole != role || view.Suppressed) continue;
                if (dots.IsClaimOn(view.Path)) return true;
            }
            return false;
        }

        /// <summary>그 자리의 (숨기지 않은) 뷰 중 받을 것이 켜진 수 (빠른 아이콘 줄 NEW가 쉴지 — MaxRailMarks)</summary>
        static int CountClaims(MainBadgeRole role, RedDotSystem dots)
        {
            if (dots == null) return 0;
            int count = 0;
            for (int i = 0; i < Views.Count; i++)
            {
                var view = Views[i];
                if (view == null || view.MainRole != role || view.Suppressed) continue;
                if (dots.IsClaimOn(view.Path)) count++;
            }
            return count;
        }

        /// <summary>그 경로의 탭 뷰 (고른 탭 = 숨김은 빠진다)</summary>
        static RedDotView FindTab(string path)
        {
            for (int i = 0; i < Views.Count; i++)
            {
                var view = Views[i];
                if (view == null || view.MainRole != MainBadgeRole.Tab || view.Suppressed) continue;
                if (string.Equals(view.Path, path, StringComparison.Ordinal)) return view;
            }
            return null;
        }

        /// <summary>
        /// 지금 NEW 알약을 달 하나 (RedDotView 또는 FeatureLockCover, 없으면 null): 후보 중 가장 최근에 열린 기능.
        /// 시트가 열려 있어도 빠른 아이콘을 빼지 않는다 (Wave 2 r2 — 빼면 시트를 열 때마다 NEW가 x1 잠금 덮개로 건너갔다가 닫으면 돌아왔다).
        /// 이긴 쪽이 지금 보일 수 없으면(시트 밑 빠른 아이콘 · 줄 안 받을 것 2개 이상) NEW는 아무 데도 없다 — WinnerShows.
        /// 같은 차례면 명부 앞쪽 (모두 같은 상태에서 같은 답)
        /// </summary>
        static object NewWinner()
        {
            var dots = Game.RedDots;
            var features = Game.Features;
            if (dots == null || features == null) return null;
            var chest = Game.IdleChest;
            // '무료'가 바구니 시간 알약에 떠 있으면 바구니 NEW는 쉰다 (모서리 배지는 하나 — 점 > 무료 > NEW)
            bool freeInPill = chest != null && chest.ShowsFreeTag;
            object best = null;
            int bestRank = int.MinValue;
            for (int i = 0; i < Views.Count; i++)
            {
                var view = Views[i];
                if (view == null || view.Suppressed) continue;
                if (view.MainRole == MainBadgeRole.Basket && freeInPill) continue;
                string path = view.Path;
                if (string.IsNullOrEmpty(path) || !dots.IsOn(path) || dots.IsClaimOn(path)) continue;
                // 0 = 점 트리가 아직 옛 값(봤음 처리 직후 — 다음 Tick에 꺼진다): 후보가 아니다
                int rank = PathRank(path, features);
                if (rank <= 0 || rank <= bestRank) continue;
                best = view;
                bestRank = rank;
            }
            for (int i = 0; i < Covers.Count; i++)
            {
                var cover = Covers[i];
                if (cover == null || !cover.WantsNewMark) continue;
                int rank = Rank(cover.Feature);
                if (rank <= bestRank) continue;
                best = cover;
                bestRank = rank;
            }
            return best;
        }

        /// <summary>경로 아래 "새로 열림" 기능 중 가장 최근 차례 (새로 열린 기능이 없으면 0)</summary>
        static int PathRank(string path, FeatureGateSystem features)
        {
            int best = 0;
            for (int i = 0; i < AllFeatures.Length; i++)
            {
                var feature = AllFeatures[i];
                int index = (int)feature;
                if (index < 0 || index >= Parents.Length || !string.Equals(Parents[index], path, StringComparison.Ordinal)) continue;
                if (!features.IsNew(feature)) continue;
                int rank = Rank(feature);
                if (rank > best) best = rank;
            }
            return best;
        }

        /// <summary>최근일수록 크다: 이번 실행에서 열린 기능은 그 차례(1000 + n), 아니면 FeatureId 순서(진행 순서와 같다) + 1</summary>
        static int Rank(FeatureId feature)
        {
            int index = (int)feature;
            if (index < 0 || index >= Order.Length) return 0;
            return Order[index] > 0 ? 1000 + Order[index] : index + 1;
        }

        /// <summary>기능 개방 시스템이 바뀌면(새 게임 · 다시 불러오기) 열린 차례를 새로 센다</summary>
        static void Track()
        {
            var features = Game.Features;
            if (ReferenceEquals(features, s_tracked)) return;
            if (s_tracked != null) s_tracked.Unlocked -= OnUnlockedHandler;
            s_tracked = features;
            Array.Clear(Order, 0, Order.Length);
            s_counter = 0;
            if (features != null) features.Unlocked += OnUnlockedHandler;
        }

        static void OnUnlocked(FeatureId feature)
        {
            int index = (int)feature;
            if (index >= 0 && index < Order.Length) Order[index] = ++s_counter;
        }

        static string[] BuildParents()
        {
            int max = 0;
            foreach (var feature in AllFeatures) max = Math.Max(max, (int)feature);
            var parents = new string[max + 1];
            foreach (var feature in AllFeatures)
            {
                if ((int)feature >= 0) parents[(int)feature] = RedDotPaths.ParentOf(feature);
            }
            return parents;
        }
    }
}
