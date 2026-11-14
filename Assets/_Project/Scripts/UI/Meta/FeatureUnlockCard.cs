using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 기능 개방 카드 840×680 (F-03 · M78 · M26 · M76, UX-SPEC §2.11): 그림 + "새 기능이 열렸어요!" + 기능 이름 + 한 줄 + [가 보기 ›](Primary) [나중에].
    /// 발표자(MetaPopupPresenter)가 PopupQueue의 FeatureUnlock 요청을 스테이지 사이에 띄운다. 열릴 때 FeatureUnlock 소리.
    /// 같은 틀로 튜토리얼 졸업 카드("이제 수선점 주인이에요!")도 보여 줄 수 있다 (ShowGraduation).
    /// 거절('나중에' · '좋아요' · X · 뒤로)로 닫을 때만 메달 그림이 그 기능 자리(MetaNavigator.FeatureSlot — 빠른 아이콘 · 탭 · HUD 버튼)로 날아간다
    /// (RewardFlyer.FlyTo — 닿으면 FlyTo가 자리를 튀기고 별빛 4개, 여기서는 카드 밖 별빛 층 _slotSparkles에 등불 별빛 8개만 덧붙인다).
    /// 튜토리얼 중 · 졸업 카드 · [가 보기]는 날리지 않는다. 열림 선물은 열리는 순간 이미 들어와 있어 따로 날리지 않는다 (상단 바 숫자가 되돌아가 보이지 않게).
    /// 날아가는 동안(최대 FlightHoldMax) IsFlying — 발표자가 다음 팝업을 붙잡고, 빠른 아이콘 줄은 IsPresenting으로 새 아이콘을 숨겨 둔다 (CR-24).
    /// 전투 HUD 토글(AUTO · 2배속 · 불 밝히기 방식 — MetaSession.IsHudToggle)은 그림이 자리에 닿을 때(날리지 못하면 거절로 닫힐 때) 봤음 처리한다 —
    /// 카드 + 닿는 빛이 알림이라 잠금 덮개 NEW 알약을 남기지 않는다 (M09 · §3.13.4).
    /// </summary>
    public class FeatureUnlockCard : UIPopup
    {
        /// <summary>닫은 뒤 아이콘이 자리에 닿기를 기다리는 최대 시간 (§2.11 — 발표자가 다음 팝업을 이만큼만 붙잡는다)</summary>
        public const float FlightHoldMax = 0.8f;

        [SerializeField] MetaIcons _icons;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] RectTransform _art;
        [Tooltip("메달 안 아이콘 (또는 세로 그림)")]
        [SerializeField] Image _illustration;
        [Tooltip("어두운 메달 받침 (그림 그대로, 색을 바꾸지 않는다)")]
        [SerializeField] Image _illustrationBack;
        [Header("넓은 그림 띠 (가로세로비 > 1.3인 그림)")]
        [SerializeField] Image _banner;
        [SerializeField] RectTransform _bannerRoot;
        [SerializeField] AspectRatioFitter _bannerFitter;
        [Tooltip("띠가 보일 때 카드가 늘어나고 글이 내려가는 높이 (px)")]
        [SerializeField] float _bannerSpace = 256f;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _bodyText;
        [SerializeField] Button _goButton;
        [SerializeField] TMP_Text _goLabel;
        [SerializeField] Button _laterButton;
        [SerializeField] TMP_Text _laterLabel;
        [SerializeField] MetaSparkleBurst _sparkles;
        [Tooltip("날아간 아이콘이 자리에 닿을 때 터지는 별빛 (카드 밖 MetaUI/SlotSparkles, 팝업 층 위 — 그때 카드 창은 이미 꺼져 있다, M78)")]
        [SerializeField] MetaSparkleBurst _slotSparkles;

        static readonly string GiftColor = UITheme.ColorTag(UITheme.Gold);
        /// <summary>이 비율보다 넓은 그림은 메달 대신 띠에 넣는다 (뽑기·던전·원정 그림이 140×55 띠로 줄지 않게)</summary>
        const float WideAspect = 1.3f;
        /// <summary>자리에 닿을 때 덧붙이는 등불 별빛 수 · 퍼짐 (§2.11)</summary>
        const int SlotSparkleCount = 8;
        const float SlotSparkleSpread = 0.8f;

        static FeatureUnlockCard s_instance;

        FeatureId _feature;
        bool _isGraduation;
        Coroutine _intro;
        Vector2 _nameBase;
        Vector2 _bodyBase;
        Vector2 _nameCompact;
        Vector2 _bodyCompact;
        float _windowCompactHeight;
        bool _layoutRead;
        /// <summary>[가 보기]로 닫는 중 (거절이 아니다 → 날리지 않는다)</summary>
        bool _going;
        /// <summary>메달 그림이 자리로 날아가는 중 (닿거나 FlightHoldMax가 지나면 끝)</summary>
        bool _flying;
        float _flightDeadline;
        FeatureId _flightFeature;
        RectTransform _flightTarget;
        Action _onFlightArrive;
        Transform _canvasRoot;

        /// <summary>닫힌 뒤 메달 그림이 아직 자리로 날아가는 중인지 (최대 FlightHoldMax — 발표자가 다음 팝업을 붙잡는다). 할당 없음</summary>
        public bool IsFlying => _flying && Time.unscaledTime < _flightDeadline;

        /// <summary>
        /// 이 기능의 개방 카드가 열려 있거나 그 그림이 자리로 날아가는 중인지 (졸업 카드는 아니다).
        /// 빠른 아이콘 줄이 새로 열린 아이콘을 숨겨 두는 동안 읽는다 (CR-24). 할당 없음
        /// </summary>
        public static bool IsPresenting(FeatureId feature)
        {
            var card = s_instance;
            if (card == null || card._isGraduation) return false;
            if (card.IsOpen) return card._feature == feature;
            return card.IsFlying && card._flightFeature == feature;
        }

        protected override void Awake()
        {
            base.Awake();
            s_instance = this;
            _onFlightArrive = OnFlightArrive;
            EnsureLayoutBases();
            _goButton.onClick.AddListener(OnGo);
            _laterButton.onClick.AddListener(Close);
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        /// <summary>기능 개방 카드를 연다</summary>
        public void Show(FeatureId feature)
        {
            _feature = feature;
            _isGraduation = false;
            _going = false;
            var def = MetaUi.IsActive ? Game.Features.GetDef(feature) : null;
            _titleText.text = def != null && !string.IsNullOrEmpty(def.cardTitle) ? def.cardTitle : "새 기능이 열렸어요!";
            _nameText.text = def != null && !string.IsNullOrEmpty(def.name) ? def.name : feature.ToString();
            string body = def != null ? def.cardText : null;
            if (string.IsNullOrEmpty(body)) body = "이제 쓸 수 있어요. 한번 둘러볼까요?";
            if (def != null && def.rewards != null && def.rewards.Count > 0)
            {
                body += $"\n<color={GiftColor}>열림 선물: {MetaUi.DescribeData(def.rewards)}</color>";
            }
            _bodyText.text = body;

            // 넓은 그림은 띠에, 메달에는 세로 그림(친구 등) → 아이콘 순
            var illustration = def != null ? def.illustration : null;
            bool wide = IsWide(illustration);
            Sprite medal = !wide && illustration != null ? illustration : def != null ? def.icon : null;
            if (medal == null && _icons != null) medal = _icons.Feature(feature);
            ApplyArt(medal, wide ? illustration : null);

            // 이미 켜 둔 HUD 기능(튜토리얼이 켠 AUTO 등)은 [가 보기]가 버튼을 누르라고 가리켜 꺼 버리게 하므로 숨기고 봤음 처리
            bool alreadyOn = MetaUi.IsFeatureAlreadyOn(feature);
            if (alreadyOn && Game.Features.IsNew(feature)) Game.Features.MarkSeen(feature);
            bool canGo = CanGo(feature) && !alreadyOn;
            _goButton.gameObject.SetActive(canGo);
            // [가 보기 ›] = 이동(Primary, M26 · M76) — 이 카드에는 받기(금)가 없다
            _goLabel.text = "가 보기 ›";
            _laterLabel.text = canGo ? "나중에" : "좋아요";
            Open();
        }

        /// <summary>튜토리얼 졸업 카드 ("이제 수선점 주인이에요!" + 받은 보상)</summary>
        public void ShowGraduation(IReadOnlyList<MetaReward> rewards)
        {
            _isGraduation = true;
            _going = false;
            _titleText.text = "축하해요!";
            _nameText.text = "이제 수선점 주인이에요!";
            string body = "친구들과 함께 밤거리의 수선점을 지켜 주세요.\n매일 들르면 출석부와 수선 일지가 기다려요.";
            if (rewards != null && rewards.Count > 0) body += $"\n<color={GiftColor}>졸업 선물: {MetaUi.DescribeData(rewards)}</color>";
            _bodyText.text = body;
            ApplyArt(_icons != null ? _icons.Star : null, null);
            _goButton.gameObject.SetActive(false);
            _laterLabel.text = "좋아요";
            Open();
        }

        /// <summary>
        /// 띠가 없을 때의 배치(이름·글 위치, 카드 높이)를 한 번 읽는다. 빌더가 카드를 꺼 둔 채 저장해서
        /// 처음 Show는 Awake보다 먼저 불리므로 Show 쪽에서도 부른다.
        /// </summary>
        void EnsureLayoutBases()
        {
            if (_layoutRead) return;
            _layoutRead = true;
            _nameBase = _nameCompact = _nameText.rectTransform.anchoredPosition;
            _bodyBase = _bodyCompact = _bodyText.rectTransform.anchoredPosition;
            _windowCompactHeight = _window != null ? _window.sizeDelta.y : 0f;
        }

        static bool IsWide(Sprite sprite) =>
            sprite != null && sprite.rect.height > 0f && sprite.rect.width / sprite.rect.height > WideAspect;

        /// <summary>메달 안 그림 + (있으면) 넓은 그림 띠. 띠가 보이면 카드를 늘리고 이름·글을 띠 아래로 내린다</summary>
        void ApplyArt(Sprite medal, Sprite wide)
        {
            EnsureLayoutBases();
            var sprite = medal != null ? medal : _icons != null ? _icons.Star : null;
            _illustration.sprite = sprite;
            _illustration.enabled = sprite != null;
            _illustration.color = Color.white;
            _illustration.preserveAspect = true;

            bool hasBanner = wide != null && _banner != null && _bannerRoot != null;
            if (_bannerRoot != null) _bannerRoot.gameObject.SetActive(hasBanner);
            if (hasBanner)
            {
                _banner.sprite = wide;
                _banner.color = Color.white;
                if (_bannerFitter != null) _bannerFitter.aspectRatio = wide.rect.width / wide.rect.height;
            }
            float shift = hasBanner ? _bannerSpace : 0f;
            _nameBase = _nameCompact + new Vector2(0f, -shift);
            _bodyBase = _bodyCompact + new Vector2(0f, -shift);
            _nameText.rectTransform.anchoredPosition = _nameBase;
            _bodyText.rectTransform.anchoredPosition = _bodyBase;
            if (_window != null && _windowCompactHeight > 0f)
            {
                _window.sizeDelta = new Vector2(_window.sizeDelta.x, _windowCompactHeight + shift);
            }
        }

        /// <summary>[가 보기]로 갈 곳이 있는 기능인지 (전투 HUD 기능은 손가락만 가리킨다)</summary>
        static bool CanGo(FeatureId feature) => feature switch
        {
            FeatureId.Battle => false,
            FeatureId.LampPolicy => false,
            FeatureId.AutoChallenge => false,
            FeatureId.EchoSlot6 => false,
            FeatureId.Events => false,
            _ => true,
        };

        void OnGo()
        {
            var feature = _feature;
            // 거절이 아니다 — 그 화면으로 바로 가므로 아이콘을 날리지 않는다 (OnClose가 읽는다)
            _going = true;
            Close();
            if (_isGraduation || !MetaUi.IsActive) return;
            if (MetaUi.IsFeatureAlreadyOn(feature))
            {
                // 카드가 열린 뒤 켜졌을 때 (튜토리얼이 AUTO를 켬): 손가락 대신 알림만
                if (Game.Features.IsNew(feature)) Game.Features.MarkSeen(feature);
                MetaUi.Toast(feature == FeatureId.AutoBattle ? "자동 전투가 켜져 있어요. 친구들이 알아서 싸워요" : "2배속이 켜져 있어요");
                return;
            }
            switch (feature)
            {
                case FeatureId.AutoBattle:
                    UINav.FocusTarget(CoachTargets.AutoButton);
                    break;
                case FeatureId.Speed2x:
                    UINav.FocusTarget(CoachTargets.SpeedButton);
                    break;
                default:
                    UINav.GoToFeature(feature);
                    break;
            }
        }

        protected override void OnOpen()
        {
            if (_intro != null) StopCoroutine(_intro);
            _intro = StartCoroutine(Intro());
            // 졸업 카드(ShowGraduation — 졸업 팝업을 못 띄웠을 때의 대신)는 기능 개방 징글 대신 졸업 징글 (AUDIO-SPEC §2.1 #63)
            UISfx.Play(_isGraduation ? UISfxKey.Graduation : UISfxKey.FeatureUnlock);
        }

        protected override void OnClose()
        {
            // 카드를 본 것만으로는 "새로 열림" 점을 끄지 않는다 (그 화면에 들어가야 꺼짐)
            if (_intro != null) StopCoroutine(_intro);
            _intro = null;
            ResetIntro();
            bool declined = !_going;
            _going = false;
            // 닫기 장부 정리 중(닫기 연출 전)이라 메달은 아직 제자리에 보인다 → 거기서 출발
            if (!declined) return;
            FlyToSlot();
            // 날리지 못했으면 (튜토리얼 · 그림이나 자리가 없음) HUD 토글은 여기서 봤음 처리
            if (!_flying && !_isGraduation) MarkHudToggleSeen(_feature);
        }

        /// <summary>전투 HUD 토글(AUTO · 2배속 · 불 밝히기 방식)이면 "새로 열림"을 끈다 — 개방 카드가 알림이라 NEW 알약을 남기지 않는다 (M09)</summary>
        static void MarkHudToggleSeen(FeatureId feature)
        {
            if (!MetaUi.IsActive || !MetaSession.IsHudToggle(feature)) return;
            if (Game.Features.IsNew(feature)) Game.Features.MarkSeen(feature);
        }

        // ───────────── 자리로 날아가기 (M78) ─────────────

        /// <summary>
        /// 거절로 닫힐 때: 메달 그림을 그 기능 자리(MetaNavigator.FeatureSlot)로 날린다. 튜토리얼 중 · 졸업 카드 · 그림이나 자리가 없으면 날리지 않는다.
        /// FlyTo가 날릴 수 없으면(층 없음 · 자리가 안 보임 · 풀이 참) 바로 onArrive를 부르므로 _flying은 날리기에 성공한 뒤에 켠다 (그때 별빛도 없음)
        /// </summary>
        void FlyToSlot()
        {
            _flying = false;
            _flightTarget = null;
            if (_isGraduation || !MetaUi.IsActive) return;
            var tutorial = Game.Tutorial;
            if (tutorial != null && tutorial.IsActive) return;
            var sprite = _illustration != null && _illustration.enabled ? _illustration.sprite : null;
            if (sprite == null) return;
            var target = FindSlot(MetaNavigator.FeatureSlot(_feature));
            if (target == null) return;
            _flightFeature = _feature;
            _flightTarget = target;
            if (!RewardFlyer.FlyTo(sprite, _illustration.rectTransform, target, _onFlightArrive))
            {
                _flightTarget = null;
                return;
            }
            _flying = true;
            _flightDeadline = Time.unscaledTime + FlightHoldMax;
        }

        /// <summary>자리에 닿음: FlyTo가 이미 자리를 튀기고 별빛 4개를 뿌렸다 — 등불 별빛 8개만 덧붙인다</summary>
        void OnFlightArrive()
        {
            // HUD 토글은 닿는 순간 봤음 (FlyTo가 바로 부른 경우 포함 — 그때는 _flying이 아직 꺼져 있다)
            MarkHudToggleSeen(_flightFeature);
            if (!_flying) return;
            _flying = false;
            var target = _flightTarget;
            _flightTarget = null;
            if (target != null && _slotSparkles != null && target.gameObject.activeInHierarchy)
            {
                _slotSparkles.BurstAt(target, SlotSparkleCount, UITheme.Lamp, SlotSparkleSpread);
            }
        }

        /// <summary>Canvas 기준 경로의 자리 (CoachTargets와 같은 경로). 닫을 때만 부른다</summary>
        RectTransform FindSlot(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_canvasRoot == null)
            {
                var canvas = GetComponentInParent<Canvas>(true);
                if (canvas != null) _canvasRoot = canvas.rootCanvas.transform;
            }
            return _canvasRoot != null ? UIFind.Path(_canvasRoot, path) : null;
        }

        void ResetIntro()
        {
            _art.localScale = Vector3.one;
            SetAlpha(_nameText, 1f);
            SetAlpha(_bodyText, 1f);
            _nameText.rectTransform.anchoredPosition = _nameBase;
            _bodyText.rectTransform.anchoredPosition = _bodyBase;
        }

        /// <summary>그림이 톡 튀어나오고 별빛이 터진 뒤, 이름과 글이 차례로 올라온다</summary>
        IEnumerator Intro()
        {
            _art.localScale = Vector3.zero;
            var nameRect = _nameText.rectTransform;
            var bodyRect = _bodyText.rectTransform;
            SetAlpha(_nameText, 0f);
            SetAlpha(_bodyText, 0f);
            yield return null;

            const float pop = 0.42f;
            bool burst = false;
            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                _art.localScale = Vector3.one * MetaUi.EaseOutBack(t / pop, 2.2f);
                if (!burst && t > pop * 0.6f)
                {
                    burst = true;
                    if (_sparkles != null) _sparkles.BurstAt(_art, 14, UITheme.Lamp, 1.1f);
                }
                float a = MetaUi.EaseOutCubic((t - 0.25f) / 0.35f);
                float b = MetaUi.EaseOutCubic((t - 0.4f) / 0.35f);
                SetAlpha(_nameText, a);
                SetAlpha(_bodyText, b);
                nameRect.anchoredPosition = _nameBase + new Vector2(0f, -24f * (1f - a));
                bodyRect.anchoredPosition = _bodyBase + new Vector2(0f, -24f * (1f - b));
                yield return null;
            }
            ResetIntro();
            _intro = null;
        }

        static void SetAlpha(TMP_Text text, float alpha)
        {
            var color = text.color;
            color.a = Mathf.Clamp01(alpha);
            text.color = color;
        }
    }
}
