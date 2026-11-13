using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 화면 오른쪽의 수선 바구니 위젯 (IDLE-01 · M32, UX-SPEC §2.1.10) 150×170. 바구니 그림 4단계 + 바구니 아래 시간 알약 + 얇은 채움 막대.
    ///   · 시간은 UIFormat.ShortElapsed ('방금' / 'N분' / 'N시간' — '0:00'은 쓰지 않는다): 104×32 둥근 알약(Background@0.65 + 2px Lavender@0.5 테두리) 위 22 굵게 Cream.
    ///   · 가득 차면 금빛으로 빛나며 가끔 흔들리고 알약 대신 '가득!'.
    ///   · 알림은 한 번에 하나 (M09 — 점 &gt; 무료 &gt; NEW): 빨간 점(hud.idlechest = IdleChestSystem.HasRedDot — 가득 · 오래 쌓임, 판의 RedDotView) >
    ///     '무료'(IdleChestSystem.ShowsFreeTag — 빠른 수선 무료가 남았고 빨간 점이 아닐 때: 모서리 알약이 아니라 시간 알약 글자가 Gold '무료') >
    ///     NEW(수선 바구니 · 빠른 수선이 막 열림 — 팝업을 한 번 열면 꺼진다, MAIN 배지 예산이 '무료'가 떠 있으면 쉬게 한다).
    ///     예전 장면의 모서리 'FreeTag'가 묶여 있으면 늘 꺼 둔다.
    /// 누르면 UINav.OpenIdleChest(). 타이머는 1초마다 폴링하고, 글자는 바뀔 때만 쓴다 (ShortElapsed는 만들어 둔 글을 돌려줘 할당이 없다).
    /// 수선 바구니가 잠겨 있으면 숨는다.
    /// </summary>
    public class IdleChestWidgetView : MonoBehaviour
    {
        const float PollInterval = 1f;
        const float FullWiggleInterval = 2.6f;

        [SerializeField] MetaIcons _icons;
        [SerializeField] Button _button;
        [Tooltip("흔들림·튀기기를 받는 그림 루트 (버튼 루트는 PressScale이 크기를 쓴다)")]
        [SerializeField] RectTransform _visual;
        [SerializeField] Image _basket;
        [Tooltip("바구니 아트가 없을 때 보이는 자리표시 묶음")]
        [SerializeField] GameObject _placeholder;
        [SerializeField] Image[] _placeholderFill = Array.Empty<Image>();
        [SerializeField] Image _glow;
        [SerializeField] TMP_Text _timeText;
        [Tooltip("시간 글자를 얹는 알약 (가득이면 '가득!'과 자리를 바꾼다). 없으면 글자만 끈다")]
        [SerializeField] GameObject _timePill;
        [SerializeField] Image _fill;
        [SerializeField] GameObject _fullTag;
        [Tooltip("예전 모서리 '무료' 알약 (이제 쓰지 않는다 — 묶여 있으면 꺼 둔다. '무료'는 시간 알약 글자로 보인다)")]
        [SerializeField] GameObject _freeTag;

        /// <summary>시간 알약의 '무료' 글자 (빠른 수선 무료가 남았을 때 — 시간 대신)</summary>
        const string FreeLabel = "무료";

        bool _subscribed;
        bool _wasVisible;
        float _pollTimer;
        float _wiggleTimer;
        float _pulse;
        string _shownTime;
        int _shownStage = -1;
        bool _full;
        bool _freeShown;
        Coroutine _anim;

        void Awake()
        {
            if (_button != null) _button.onClick.AddListener(OnClick);
        }

        void Start()
        {
            if (MetaUi.IsActive)
            {
                Game.IdleChest.Changed += OnChestChanged;
                Game.IdleChest.Claimed += OnClaimed;
                Game.Features.Changed += OnChestChanged;
                _subscribed = true;
            }
            Poll(true);
        }

        void OnDestroy()
        {
            if (!_subscribed || !Game.IsReady || Game.IdleChest == null) return;
            Game.IdleChest.Changed -= OnChestChanged;
            Game.IdleChest.Claimed -= OnClaimed;
            Game.Features.Changed -= OnChestChanged;
        }

        void OnChestChanged() => Poll(true);

        void OnClaimed(IdleChestClaimResult result)
        {
            Poll(true);
            Play(Bounce(1.18f));
        }

        void OnClick()
        {
            if (!MetaUi.IsActive) return;
            Game.Features.MarkSeen(FeatureId.IdleChest);
            UINav.OpenIdleChest();
        }

        void Update()
        {
            if (!MetaUi.IsActive) return;
            float dt = Time.unscaledDeltaTime;
            _pollTimer -= dt;
            if (_pollTimer <= 0f) Poll(false);

            if (!_full) return;
            _pulse += dt;
            if (_glow != null) _glow.color = MetaUi.Alpha(UITheme.Gold, 0.35f + 0.25f * Mathf.Sin(_pulse * 3.2f));
            _wiggleTimer -= dt;
            if (_wiggleTimer <= 0f && _anim == null)
            {
                _wiggleTimer = FullWiggleInterval;
                Play(Wiggle());
            }
        }

        void Poll(bool force)
        {
            _pollTimer = PollInterval;
            var chest = MetaUi.IsActive ? Game.IdleChest : null;
            bool visible = chest != null && chest.IsUnlocked;
            if (_visual.gameObject.activeSelf != visible) _visual.gameObject.SetActive(visible);
            if (_button != null) _button.enabled = visible;
            if (!visible)
            {
                _wasVisible = false;
                return;
            }
            if (!_wasVisible)
            {
                _wasVisible = true;
                force = true;
                Play(PopIn());
            }

            // '방금' / 'N분' / 'N시간' (만들어 둔 글 — 바뀔 때만 쓴다). 빠른 수선 무료가 남았으면 시간 대신 Gold '무료' (모서리 배지는 하나 — M09)
            bool free = chest.ShowsFreeTag;
            string time = free ? FreeLabel : UIFormat.ShortElapsed(chest.Elapsed);
            if (force || !ReferenceEquals(time, _shownTime))
            {
                _shownTime = time;
                _timeText.text = time;
            }
            if (force || free != _freeShown)
            {
                _freeShown = free;
                _timeText.color = free ? UITheme.Gold : UITheme.Cream;
                if (_freeTag != null && _freeTag.activeSelf) _freeTag.SetActive(false);
            }
            float ratio = Mathf.Clamp01(chest.FillRatio);
            MetaUi.SetBar(_fill, ratio);

            bool full = chest.IsFull;
            if (force || full != _full)
            {
                _full = full;
                _fullTag.SetActive(full);
                if (_timePill != null) _timePill.SetActive(!full);
                else _timeText.gameObject.SetActive(!full);
                _fill.color = full ? UITheme.Gold : UITheme.Lavender;
                if (_glow != null)
                {
                    _glow.enabled = full;
                    _glow.color = MetaUi.Alpha(UITheme.Gold, 0.45f);
                }
                _wiggleTimer = 0.4f;
            }

            int stage = chest.ArtStage;
            if (force || stage != _shownStage)
            {
                _shownStage = stage;
                ApplyStage(stage);
            }
        }

        void ApplyStage(int stage)
        {
            var sprite = _icons != null ? _icons.Basket(stage) : null;
            bool hasArt = sprite != null;
            _basket.enabled = hasArt;
            if (hasArt) _basket.sprite = sprite;
            if (_placeholder != null) _placeholder.SetActive(!hasArt);
            // 자리표시: 단계만큼 실타래·단추 덩어리가 바구니 위로 차오른다
            for (int i = 0; i < _placeholderFill.Length; i++)
            {
                if (_placeholderFill[i] != null) _placeholderFill[i].enabled = i < stage;
            }
        }

        void Play(IEnumerator routine)
        {
            if (!isActiveAndEnabled || !_visual.gameObject.activeInHierarchy) return;
            if (_anim != null) StopCoroutine(_anim);
            _visual.localScale = Vector3.one;
            _visual.localRotation = Quaternion.identity;
            _anim = StartCoroutine(Run(routine));
        }

        IEnumerator Run(IEnumerator routine)
        {
            yield return routine;
            _visual.localScale = Vector3.one;
            _visual.localRotation = Quaternion.identity;
            _anim = null;
        }

        IEnumerator Wiggle()
        {
            const float duration = 0.55f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float angle = Mathf.Sin(k * Mathf.PI * 5f) * 9f * (1f - k);
                _visual.localRotation = Quaternion.Euler(0f, 0f, angle);
                _visual.localScale = Vector3.one * (1f + 0.06f * MetaUi.Punch(k));
                yield return null;
            }
        }

        IEnumerator Bounce(float peak)
        {
            const float duration = 0.35f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float squash = MetaUi.Punch(k);
                _visual.localScale = new Vector3(1f + (peak - 1f) * squash, 1f - (peak - 1f) * 0.6f * squash, 1f);
                yield return null;
            }
        }

        IEnumerator PopIn()
        {
            const float duration = 0.45f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                _visual.localScale = Vector3.one * MetaUi.EaseOutBack(t / duration, 2.2f);
                yield return null;
            }
        }
    }
}
