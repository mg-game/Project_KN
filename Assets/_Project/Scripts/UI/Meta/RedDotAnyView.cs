using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 여러 빨간 점 경로를 한 점으로 모아 보여 준다 (UX-SPEC-IA §2.3 · §5.9) — 가운데 사냥 버튼 Tab_Main/MetaDot.
    /// 경로 = _paths (v1: hud.guidequest) ∪ _rails가 지금 놓은 아이콘(줄 + 쟁반)의 점 경로 (QuickRailView.PlacedDotPaths, CR-07 —
    /// Phase 4에 아이콘이 늘어도 목록을 고치지 않는다). 받을 것만 센다 (RedDotSystem.IsClaimOn — 새로 열림 잎은 사냥에 점을 켜지 않는다,
    /// 막 열린 빠른 아이콘의 NEW가 사냥 버튼에 점을 영영 남기지 않게).
    /// SetDot(받을 것이 하나라도 &amp;&amp; (!_onlyWhileSheetOpen || UIFrame.SheetOpen)) — 사냥 버튼은 시트가 열린 동안만
    /// (MAIN에서는 그 점들이 이미 빠른 아이콘 · 가이드 퀘스트 줄에 보인다). RedDots.Changed · QuickRailView.Changed ·
    /// UIFrame.SheetStateChanged에만 다시 본다 (매 프레임 아님, 할당 없음). 메타가 꺼져 있으면 숨긴다.
    /// 배지가 꺼졌다 켜져도 알림을 놓치지 않게 늘 켜진 오브젝트(Tab_Main)에 붙인다 — 배지는 자식.
    /// </summary>
    public class RedDotAnyView : MonoBehaviour
    {
        [Tooltip("늘 모아 보는 경로 (v1: hud.guidequest)")]
        [SerializeField] string[] _paths;
        [Tooltip("놓은 아이콘의 점 경로를 더할 빠른 아이콘 줄 (QuickRail_L · QuickRail_R)")]
        [SerializeField] QuickRailView[] _rails;
        [Tooltip("점 (점 모드)")]
        [SerializeField] Badge _badge;
        [Tooltip("시트가 열린 동안만 보인다 (사냥 버튼)")]
        [SerializeField] bool _onlyWhileSheetOpen;

        bool _subscribed;
        /// <summary>마지막으로 보인 상태 (같으면 배지를 다시 건드리지 않는다)</summary>
        bool _shown;
        bool _applied;

        void OnEnable()
        {
            if (MetaUi.IsActive && !_subscribed)
            {
                Game.RedDots.Changed += Refresh;
                if (_rails != null)
                {
                    for (int i = 0; i < _rails.Length; i++)
                    {
                        if (_rails[i] != null) _rails[i].Changed += Refresh;
                    }
                }
                UIFrame.SheetStateChanged += OnSheetStateChanged;
                _subscribed = true;
            }
            _applied = false;
            Refresh();
        }

        void OnDisable() => Unsubscribe();

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            UIFrame.SheetStateChanged -= OnSheetStateChanged;
            if (_rails != null)
            {
                for (int i = 0; i < _rails.Length; i++)
                {
                    if (_rails[i] != null) _rails[i].Changed -= Refresh;
                }
            }
            if (Game.IsReady && Game.RedDots != null) Game.RedDots.Changed -= Refresh;
        }

        void OnSheetStateChanged(bool open) => Refresh();

        /// <summary>모은 점을 다시 본다</summary>
        public void Refresh()
        {
            if (_badge == null) return;
            bool on = MetaUi.IsActive && (!_onlyWhileSheetOpen || UIFrame.SheetOpen) && AnyClaim();
            if (_applied && on == _shown) return;
            _applied = true;
            _shown = on;
            _badge.SetDot(on);
        }

        /// <summary>모으는 경로 중 받을 것이 하나라도 켜졌는지 (할당 없음)</summary>
        bool AnyClaim()
        {
            var dots = Game.RedDots;
            if (dots == null) return false;
            if (_paths != null)
            {
                for (int i = 0; i < _paths.Length; i++)
                {
                    string path = _paths[i];
                    if (!string.IsNullOrEmpty(path) && dots.IsClaimOn(path)) return true;
                }
            }
            if (_rails == null) return false;
            for (int r = 0; r < _rails.Length; r++)
            {
                var rail = _rails[r];
                if (rail == null) continue;
                IReadOnlyList<string> placed = rail.PlacedDotPaths;
                for (int i = 0; i < placed.Count; i++)
                {
                    if (dots.IsClaimOn(placed[i])) return true;
                }
            }
            return false;
        }
    }
}
