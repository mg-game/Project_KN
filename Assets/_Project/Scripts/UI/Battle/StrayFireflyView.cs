using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 길 잃은 반딧불 (NEW-03, SPEC4 §13.8 — 확장 항목, 콘텐츠로 꺼져 있다): 규칙(Game.Fireflies — P4-2)이 허락하면 앞에서 보는 시간 NextDelay초 뒤 한 마리가
    /// 전장 띠(전투 화면 위에서 250~400px)에 옆에서 들어와 느린 사인 곡선(진폭 40px, 140px/s)으로 떠다니다 반대쪽 화면 밖으로 날아간다
    /// (수명 LifeFor(반폭) — 1080 폭이면 1200px ÷ 140 ≈ 8.6초, 움직임 줄이기면 곧게).
    /// 56px 금빛 반딧불 + 120px 빛무리(1.2초마다 숨쉼) — 정전 스테이지는 2px 크림 고리. 120×120 누름 자리(그 그림만 입력을 받는다).
    /// 누르면 규칙의 TryCatch(보상 · 하루 횟수) + firefly_catch 소리 + 가벼운 진동, 처음 잡으면 토스트 '길 잃은 반딧불이 선물을 두고 갔어요'.
    /// 쓰다듬기 · 조준 중에는 흐려지고 누름을 받지 않는다. 개발 빌드의 '-forceFirefly' 인자는 규칙과 상관없이 3초 뒤 띄운다 (캡처 42, 로그 '[Verify] firefly_*').
    /// 빌더가 만들기 전(사용자 씬)에는 없다
    /// </summary>
    public class StrayFireflyView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] RectTransform _root;
        [SerializeField] CanvasGroup _group;
        [SerializeField] Image _glow;
        [SerializeField] Image _ring;
        [SerializeField] Image _hit;
        [SerializeField] SummonAimView _aim;

        /// <summary>SPEC4 §13.8의 이름뿐인 비행 시간 (1080 폭 기준 ≈ 8초). 실제 수명은 날 때마다 LifeFor(반폭) — 반대쪽 화면 밖까지</summary>
        public const float Life = 8f;
        public const float Speed = 140f;
        /// <summary>화면 밖 여유 (px): 반폭 + 이만큼 바깥에서 들어와 반대쪽 같은 거리 밖에서 사라진다 (120px 빛무리 반지름)</summary>
        public const float EdgeMargin = 60f;
        public const float Amplitude = 40f;
        public const float BandTop = -250f;
        public const float BandBottom = -400f;
        public const string FirstCatchToast = "길 잃은 반딧불이 선물을 두고 갔어요";
        const string FirstCatchFlag = "fx.firefly_first_catch";
        const float ForceDelay = 3f;

        float _wait = -1f;
        float _age = -1f;
        float _dir = 1f;
        float _baseY;
        float _startX;
        /// <summary>이번 비행의 수명 (Spawn이 LifeFor로 정한다)</summary>
        float _life = Life;
        bool _forced;
        bool _forceChecked;
        int _caught;

        public bool IsFlying => _age >= 0f;

        /// <summary>이번 비행의 수명 (초, 확인용)</summary>
        public float FlightLife => _life;

        /// <summary>
        /// 비행 수명 (순수 — StrayFireflyViewTests, RI-6a): 반폭 + EdgeMargin 바깥에서 들어와 반대쪽 같은 거리 밖까지 Speed로 가는 시간
        /// = 2 × (반폭 + 60) ÷ 140. 1080 폭이면 1200 ÷ 140 ≈ 8.57초 (예전 8초는 +520에서, 화면 안에서 사라졌다)
        /// </summary>
        public static float LifeFor(float halfWidth) => 2f * (Mathf.Max(0f, halfWidth) + EdgeMargin) / Speed;

        AudioId _appearSound;
        AudioId _catchSound;

        void Awake()
        {
            // 소리 키는 한 번만 찾는다 (AUD R3 — 매 프레임 문자열 찾기 없음)
            _appearSound = AudioService.Resolve("firefly_appear");
            _catchSound = AudioService.Resolve("firefly_catch");
            Hide();
        }

        void Hide()
        {
            _age = -1f;
            if (_root != null && _root.gameObject.activeSelf) _root.gameObject.SetActive(false);
        }

        /// <summary>개발 빌드: 명령줄 '-forceFirefly' (한 번만 읽는다)</summary>
        bool Forced
        {
            get
            {
                if (_forceChecked) return _forced;
                _forceChecked = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                foreach (var arg in System.Environment.GetCommandLineArgs())
                {
                    if (string.Equals(arg, "-forceFirefly", System.StringComparison.OrdinalIgnoreCase)) _forced = true;
                }
#endif
                return _forced;
            }
        }

        /// <summary>BattleView가 매 프레임 (할당 없음): 기다림 · 날기 · 숨김 규칙</summary>
        public void Tick(float dt, bool asleep, bool popupOpen, bool blackout)
        {
            if (_root == null) return;
            bool aiming = _aim != null && _aim.IsActive;
            if (_age >= 0f)
            {
                Fly(dt, aiming);
                return;
            }
            var rules = Game.IsReady && Game.IsMetaActive ? Game.Fireflies : null;
            bool forced = Forced;
            bool can = forced ? !asleep : rules != null && SafeCanSpawn(rules, asleep, popupOpen, aiming);
            if (!can)
            {
                if (!forced) _wait = -1f;
                return;
            }
            if (_wait < 0f) _wait = forced ? ForceDelay : Mathf.Max(1f, rules.NextDelay());
            _wait -= dt;
            if (_wait <= 0f) Spawn(blackout);
        }

        static bool SafeCanSpawn(FireflyRules rules, bool asleep, bool popupOpen, bool aiming)
        {
            try
            {
                return rules.CanSpawn(asleep, popupOpen, aiming);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        /// <summary>한 마리 띄운다 (Tick — 기다림이 끝나면. StrayFireflyViewTests가 직접 부른다)</summary>
        internal void Spawn(bool blackout)
        {
            if (_root == null) return;
            _wait = -1f;
            _age = 0f;
            _dir = Random.value < 0.5f ? 1f : -1f;
            _baseY = Random.Range(BandBottom, BandTop);
            var parent = _root.parent as RectTransform;
            float half = parent != null && parent.rect.width > 1f ? parent.rect.width * 0.5f : 540f;
            _startX = -_dir * (half + EdgeMargin);
            // 반대쪽 화면 밖까지 날아간 뒤 숨는다 (RI-6a — 예전 고정 8초는 1120px만 가서 +520, 화면 안에서 사라졌다)
            _life = LifeFor(half);
            _root.gameObject.SetActive(true);
            if (_ring != null) _ring.enabled = blackout;
            _root.anchoredPosition = new Vector2(_startX, _baseY);
            AudioService.Play(_appearSound, 0.25f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_forced) Debug.Log("[Verify] firefly_spawn");
#endif
        }

        void Fly(float dt, bool aiming)
        {
            _age += dt;
            if (_age >= _life)
            {
                Hide();
                return;
            }
            float x = _startX + _dir * Speed * _age;
            float y = UIMotion.Reduced ? _baseY : _baseY + Amplitude * Mathf.Sin(_age * 1.6f);
            _root.anchoredPosition = new Vector2(x, y);
            if (_glow != null)
            {
                float breathe = 0.5f + 0.5f * Mathf.Sin(_age * Mathf.PI * 2f / 1.2f);
                float s = 0.9f + 0.2f * breathe;
                _glow.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            // 조준 중에는 흐려지고 누름을 받지 않는다
            if (_group != null) _group.alpha = Mathf.MoveTowards(_group.alpha, aiming ? 0.3f : 1f, dt * 4f);
            if (_hit != null) _hit.raycastTarget = !aiming;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_age < 0f) return;
            var rules = Game.IsReady && Game.IsMetaActive ? Game.Fireflies : null;
            System.Collections.Generic.List<MetaReward> rewards = null;
            if (rules != null)
            {
                try
                {
                    rewards = rules.TryCatch();
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            _caught++;
            AudioService.Play(_catchSound);
            Haptics.Play(HapticKind.Light);
            var flags = Game.Data != null ? Game.Data.flags : null;
            if (rewards != null && flags != null && flags.Add(FirstCatchFlag))
            {
                var ui = UIManager.Instance;
                if (ui != null) ui.ShowToast(FirstCatchToast, ToastKind.Success);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_forced) Debug.Log($"[Verify] firefly_catch count={_caught}/6 reward={(rewards != null ? rewards.Count : 0)}");
#endif
            Hide();
        }
    }
}
