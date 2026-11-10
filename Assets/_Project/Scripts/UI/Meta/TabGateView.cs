using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 하단 탭 바의 기능 개방 표시와 확인 (F-03 · M09). 탭 바에 붙는다 (MainTabController는 공용 계약 파일이라 고치지 않는다).
    ///   · 잠긴 탭: 아이콘·글자 알파 0.45 + 자물쇠 48px + 작은 글자 "1-4". 누르면 MainTabController.SelectGuard에서 막고
    ///     "스테이지 1-4를 클리어하면 열려요" 토스트 + 자물쇠 막힘 흔들기(UIMotion.DenyOffset) + Deny 소리 + 약한 진동 (M18).
    ///   · 고른 탭에는 배지가 없다 (§3.13.1): 그 탭에 속한 기능의 "새로 열림"을 끄고(MarkSeenTab — 고를 때 · 시작할 때 · 고른 채 기능이 열릴 때 ·
    ///     다시 누를 때(Reselected)) 그 탭의 RedDotView를 고른 동안 숨긴다 (받을 것 점도 — 탭을 떠나면 다시 보인다).
    ///     MarkSeen이 Features.Changed를 다시 부르므로 _markingSeen으로 다시 들어오지 않게 막는다 — 같은 프레임에 꺼진다.
    ///   · 탭 → 기능: 편성 = Formation, 원정 = Expedition, 소환 = Gacha, 상점 = Shop. 강화·친구들은 항상 열림.
    /// 메타가 꺼져 있으면 잠금도 확인도 하지 않는다.
    /// </summary>
    public class TabGateView : MonoBehaviour
    {
        [SerializeField] MainTabController _controller;
        [Tooltip("탭 순서(MainTab)대로: 아이콘 알파를 바꿀 묶음")]
        [SerializeField] CanvasGroup[] _iconGroups = Array.Empty<CanvasGroup>();
        [Tooltip("탭 순서대로: 글자 알파를 바꿀 묶음")]
        [SerializeField] CanvasGroup[] _labelGroups = Array.Empty<CanvasGroup>();
        [Tooltip("탭 순서대로: 자물쇠 덮개 (없는 탭은 비움)")]
        [SerializeField] GameObject[] _locks = Array.Empty<GameObject>();
        [SerializeField] TMP_Text[] _hints = Array.Empty<TMP_Text>();
        [SerializeField] RectTransform[] _lockIcons = Array.Empty<RectTransform>();
        [Tooltip("메타가 켜지면 치울 예전 탭 배지 컴포넌트 (원정 탭의 숫자 배지 — 빨간 점 트리가 대신한다). 없어도 됨")]
        [SerializeField] MonoBehaviour[] _legacyBadgeOwners = Array.Empty<MonoBehaviour>();
        [SerializeField] GameObject[] _legacyBadges = Array.Empty<GameObject>();
        [Tooltip("탭 순서대로: 탭 버튼의 빨간 점 (고른 탭은 숨긴다). 없어도 됨")]
        [SerializeField] RedDotView[] _tabDots = Array.Empty<RedDotView>();

        const float LockedAlpha = 0.45f;

        bool _subscribed;
        bool _markingSeen;
        Func<MainTab, bool> _guard;
        bool[] _wasLocked = Array.Empty<bool>();
        Coroutine[] _shakes = Array.Empty<Coroutine>();

        /// <summary>탭이 여는 기능 (항상 열린 탭은 Battle)</summary>
        public static FeatureId FeatureOf(MainTab tab) => tab switch
        {
            MainTab.Formation => FeatureId.Formation,
            MainTab.Expedition => FeatureId.Expedition,
            MainTab.Gacha => FeatureId.Gacha,
            MainTab.Shop => FeatureId.Shop,
            _ => FeatureId.Battle,
        };

        /// <summary>탭의 빨간 점 경로</summary>
        public static string RedDotPathOf(MainTab tab) => tab switch
        {
            MainTab.Growth => RedDotPaths.TabGrowth,
            MainTab.Formation => RedDotPaths.TabFormation,
            MainTab.Characters => RedDotPaths.TabCharacters,
            MainTab.Expedition => RedDotPaths.TabExpedition,
            MainTab.Gacha => RedDotPaths.TabGacha,
            _ => RedDotPaths.TabShop,
        };

        static readonly FeatureId[] AllFeatures = (FeatureId[])Enum.GetValues(typeof(FeatureId));

        void Start()
        {
            _wasLocked = new bool[_locks.Length];
            _shakes = new Coroutine[_locks.Length];
            if (MetaUi.IsActive) RetireLegacyBadges();
            if (MetaUi.IsActive && _controller != null)
            {
                _guard = Guard;
                _controller.SelectGuard = _guard;
                _controller.Selected += OnSelected;
                _controller.Reselected += OnReselected;
                _controller.MainShown += ApplySelection;
                Game.Features.Changed += Refresh;
                _subscribed = true;
            }
            Refresh();
            ApplySelection();
        }

        void OnDestroy()
        {
            if (!_subscribed) return;
            if (_controller != null)
            {
                _controller.Selected -= OnSelected;
                _controller.Reselected -= OnReselected;
                _controller.MainShown -= ApplySelection;
                if (_controller.SelectGuard == _guard) _controller.SelectGuard = null;
            }
            if (Game.IsReady && Game.Features != null) Game.Features.Changed -= Refresh;
        }

        /// <summary>
        /// 예전 탭 숫자 배지(원정 탭)를 치운다 — 같은 버튼에 빨간 점 트리 점이 대신 붙는다.
        /// Destroy는 프레임 끝에야 적용되므로, 그 사이 같은 프레임의 Start/Update(타이머 0에서 바로 Refresh)가 배지를 다시 켜지 않게
        /// 먼저 컴포넌트를 끈다. 이미 구독한 이벤트(원정·던전 Changed)가 이번 프레임에 한 번 더 켤 수도 있어 다음 프레임에 한 번 더 숨긴다.
        /// </summary>
        void RetireLegacyBadges()
        {
            foreach (var owner in _legacyBadgeOwners)
            {
                if (owner == null) continue;
                owner.enabled = false;
                Destroy(owner);
            }
            HideLegacyBadges();
            if (_legacyBadges.Length > 0) StartCoroutine(HideLegacyBadgesNextFrame());
        }

        void HideLegacyBadges()
        {
            foreach (var badge in _legacyBadges)
            {
                if (badge != null && badge.activeSelf) badge.SetActive(false);
            }
        }

        IEnumerator HideLegacyBadgesNextFrame()
        {
            yield return null;
            HideLegacyBadges();
        }

        bool Guard(MainTab tab)
        {
            if (!MetaUi.IsActive) return true;
            var feature = FeatureOf(tab);
            if (feature == FeatureId.Battle) return true;
            if (Game.Features.TryUse(feature, out var message)) return true;
            MetaUi.Toast(message);
            // 막힘 (M18): Deny 소리 + 약한 진동 + 자물쇠 흔들기
            UISfx.Play(UISfxKey.Deny);
            Haptics.Play(HapticKind.Light);
            int index = (int)tab;
            if (index >= 0 && index < _lockIcons.Length && _lockIcons[index] != null && isActiveAndEnabled)
            {
                if (_shakes[index] != null) StopCoroutine(_shakes[index]);
                _shakes[index] = StartCoroutine(Shake(_lockIcons[index]));
            }
            return false;
        }

        /// <summary>탭에 들어감 (고름이 바뀜): 그 탭의 "새로 열림"을 끄고 고른 탭의 배지를 숨긴다</summary>
        void OnSelected(MainTab tab)
        {
            MarkSeenTab(tab);
            ApplySelection();
        }

        /// <summary>열린 탭을 다시 누름 (닫기 바로 전): 그 사이 열린 기능의 "새로 열림"도 끈다</summary>
        void OnReselected(MainTab tab) => MarkSeenTab(tab);

        /// <summary>
        /// 그 탭에 속한 기능의 "새로 열림"을 끈다 (Features.MarkSeen). MarkSeen이 Features.Changed → Refresh → 다시 여기로 오므로
        /// _markingSeen으로 다시 들어오지 않게 막는다 (바깥 호출이 남은 기능을 마저 끈다)
        /// </summary>
        public void MarkSeenTab(MainTab tab)
        {
            if (_markingSeen || !MetaUi.IsActive) return;
            _markingSeen = true;
            try
            {
                var features = Game.Features;
                string path = RedDotPathOf(tab);
                foreach (var feature in AllFeatures)
                {
                    if (RedDotPaths.ParentOf(feature) == path && features.IsNew(feature)) features.MarkSeen(feature);
                }
            }
            finally
            {
                _markingSeen = false;
            }
        }

        /// <summary>고른 탭(시트가 열린 탭)의 빨간 점만 숨긴다 — MAIN이면 모두 보인다 (§3.13.1)</summary>
        void ApplySelection()
        {
            int selected = _controller != null ? _controller.SelectedIndex : -1;
            for (int i = 0; i < _tabDots.Length; i++)
            {
                if (_tabDots[i] != null) _tabDots[i].Suppressed = i == selected;
            }
        }

        void Refresh()
        {
            bool active = MetaUi.IsActive;
            for (int i = 0; i < _locks.Length; i++)
            {
                var feature = FeatureOf((MainTab)i);
                bool locked = active && feature != FeatureId.Battle && !Game.Features.IsUnlocked(feature);
                if (_locks[i] != null)
                {
                    if (_wasLocked[i] && !locked && isActiveAndEnabled) StartCoroutine(Unlock(i));
                    else _locks[i].SetActive(locked);
                }
                SetDim(i, locked ? LockedAlpha : 1f);
                if (locked && i < _hints.Length && _hints[i] != null) _hints[i].text = Game.Features.GetLockedHint(feature);
                _wasLocked[i] = locked;
            }
            // 고른 탭에 속한 기능이 지금 열렸으면 같은 프레임에 "새로 열림"을 끈다 (고른 탭에는 배지가 없다)
            if (active && _controller != null && _controller.SelectedIndex >= 0) MarkSeenTab((MainTab)_controller.SelectedIndex);
        }

        /// <summary>막힘 흔들기 (UIMotion.DenyOffset — 0.26초 좌우, 자물쇠 묶음만)</summary>
        IEnumerator Shake(RectTransform icon)
        {
            float start = Time.unscaledTime;
            for (float k = 0f; k < 1f; k = (Time.unscaledTime - start) / UIMotion.Deny)
            {
                icon.anchoredPosition = new Vector2(UIMotion.DenyOffset(k), 0f);
                yield return null;
            }
            icon.anchoredPosition = Vector2.zero;
        }

        /// <summary>열림: 자물쇠가 튀어 오르며 사라지고 아이콘이 밝아진다</summary>
        IEnumerator Unlock(int index)
        {
            var lockRoot = _locks[index];
            var icon = index < _lockIcons.Length ? _lockIcons[index] : null;
            const float duration = 0.6f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                if (icon != null)
                {
                    icon.localScale = Vector3.one * (1f + 0.7f * MetaUi.EaseOutCubic(k));
                    icon.localRotation = Quaternion.Euler(0f, 0f, -18f * k);
                }
                SetDim(index, Mathf.Lerp(LockedAlpha, 1f, k));
                yield return null;
            }
            lockRoot.SetActive(false);
            if (icon != null)
            {
                icon.localScale = Vector3.one;
                icon.localRotation = Quaternion.identity;
            }
            SetDim(index, 1f);
        }

        void SetDim(int index, float alpha)
        {
            if (index < _iconGroups.Length && _iconGroups[index] != null) _iconGroups[index].alpha = alpha;
            if (index < _labelGroups.Length && _labelGroups[index] != null) _labelGroups[index].alpha = alpha;
        }
    }
}
