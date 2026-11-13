using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 시트가 열린 만큼 HUD 하나를 흐리게 한다 (UX-SPEC-IA §1.6 · §5.9). 시트가 올라오면 전투 위쪽이 좁아지므로, 보이는 띠의 길을 가리는 것은
    /// 사라지거나(알파 0 — 권장 전투력 알약 · 시너지 줄 · 빠른 아이콘 줄 · 빈 편성 안내) 반투명해진다(0.5 — 수선 바구니 · 보스 도전, 그래도 누를 수 있다).
    /// <para>
    /// UIFrame.SheetProgressChanged(p)마다 알파 = Lerp(1, _sheetAlpha, p). _ignoreDuringTutorial이면 튜토리얼 동안 1 (FTUE 9단계가 바구니를 가리킨다) —
    /// 튜토리얼이 끝나면(Finished) 바로 다시 맞춘다. 알파 0.05(HiddenBelow) 아래면 누름도 끈다 (blocksRaycasts · interactable).
    /// 셋 다 바뀔 때만 쓴다 (할당 없음, Update 없음). 켜질 때(OnEnable) 지금 p로 다시 맞춘다.
    /// </para>
    /// <para>
    /// CanvasGroup은 이것만 쓰는 전용이다 — 뷰가 자기 연출에 쓰는 CanvasGroup과 함께 쓰지 않는다 (서로 알파를 덮어쓴다).
    /// 빌더는 Attach(대상, 알파, 튜토리얼 무시)로 붙인다. UIFrame.IsFeedbackHidden이 Alpha를 읽어 흐려진 날아가기 도착지를 건너뛴다.
    /// </para>
    /// </summary>
    public class SheetFade : MonoBehaviour
    {
        /// <summary>이 알파 아래면 안 보이는 것으로 친다 (누름 끔 · 날아가기 도착지로 쓰지 않음)</summary>
        public const float HiddenBelow = 0.05f;

        [Tooltip("이 흐림 전용 CanvasGroup (뷰의 CanvasGroup과 함께 쓰지 않는다). 비면 켜질 때 하나 붙인다")]
        [SerializeField] CanvasGroup _group;
        [Tooltip("시트가 다 열렸을 때 알파 (0 = 사라짐, 0.5 = 반투명 · 누를 수 있음)")]
        [Range(0f, 1f)]
        [SerializeField] float _sheetAlpha;
        [Tooltip("튜토리얼 동안은 흐리지 않는다 (수선 바구니 — FTUE가 가리킨다)")]
        [SerializeField] bool _ignoreDuringTutorial;

        float _appliedAlpha = -1f;
        bool _appliedRaycast;
        bool _applied;
        bool _subscribed;
        TutorialSystem _hookedTutorial;

        /// <summary>지금 알파 (그룹이 없으면 1)</summary>
        public float Alpha => _group != null ? _group.alpha : 1f;

        /// <summary>시트가 다 열렸을 때 알파</summary>
        public float SheetAlpha => _sheetAlpha;

        /// <summary>
        /// 대상에 흐림을 붙인다 (빌더 · 실행 중 모두). 이미 있으면 값만 바꾼다. 그룹은 언제나 새로 붙인 전용 CanvasGroup이다.
        /// 빌더에서 부르면 장면을 저장할 때 값이 남는다 (프리팹 안이면 부르는 쪽이 EditorUtility.SetDirty).
        /// </summary>
        public static SheetFade Attach(GameObject target, float sheetAlpha, bool ignoreDuringTutorial = false)
        {
            if (target == null) return null;
            if (!target.TryGetComponent(out SheetFade fade)) fade = target.AddComponent<SheetFade>();
            if (fade._group == null) fade._group = target.AddComponent<CanvasGroup>();
            fade._sheetAlpha = Mathf.Clamp01(sheetAlpha);
            fade._ignoreDuringTutorial = ignoreDuringTutorial;
            fade._applied = false;
            if (Application.isPlaying && fade.isActiveAndEnabled) fade.Apply(UIFrame.SheetProgress);
            return fade;
        }

        void Awake()
        {
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            if (!_subscribed)
            {
                UIFrame.SheetProgressChanged += Apply;
                _subscribed = true;
            }
            HookTutorial();
            // 꺼져 있는 동안 바뀌었을 수 있다 — 기억한 값과 상관없이 다시 쓴다
            _applied = false;
            Apply(UIFrame.SheetProgress);
        }

        void OnDisable()
        {
            if (_subscribed)
            {
                UIFrame.SheetProgressChanged -= Apply;
                _subscribed = false;
            }
            UnhookTutorial();
        }

        void Apply(float p)
        {
            if (_group == null) return;
            if (_ignoreDuringTutorial && _hookedTutorial == null) HookTutorial();
            float alpha = IgnoredNow() ? 1f : Mathf.Lerp(1f, _sheetAlpha, Mathf.Clamp01(p));
            bool raycast = alpha >= HiddenBelow;
            if (!_applied || alpha != _appliedAlpha)
            {
                _appliedAlpha = alpha;
                _group.alpha = alpha;
            }
            if (!_applied || raycast != _appliedRaycast)
            {
                _appliedRaycast = raycast;
                _group.blocksRaycasts = raycast;
                _group.interactable = raycast;
            }
            _applied = true;
        }

        bool IgnoredNow() => _ignoreDuringTutorial && MetaUi.IsActive && Game.Tutorial != null && Game.Tutorial.IsActive;

        // ───────────── 튜토리얼이 끝나면 바로 다시 맞춘다 ─────────────

        void HookTutorial()
        {
            if (!_ignoreDuringTutorial || _hookedTutorial != null || !MetaUi.IsActive) return;
            var tutorial = Game.Tutorial;
            if (tutorial == null) return;
            tutorial.Finished += OnTutorialFinished;
            _hookedTutorial = tutorial;
        }

        void UnhookTutorial()
        {
            if (_hookedTutorial == null) return;
            _hookedTutorial.Finished -= OnTutorialFinished;
            _hookedTutorial = null;
        }

        void OnTutorialFinished() => Apply(UIFrame.SheetProgress);
    }
}
