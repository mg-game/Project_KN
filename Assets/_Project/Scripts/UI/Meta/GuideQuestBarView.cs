using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 가이드 퀘스트 한 줄 (Q-01 · M04 · M62, UX-SPEC §2.1.9). 전투 화면 왼쪽 아래 468×84 — At(BottomLeft, (16, 덱 띠 + 18)), 화면 x 16~484
    /// (오른쪽 끝이 남쪽 받침 · 지킴이 열 앞에서 멈추고, 덱 안내 글자 탭(띠 위 18)을 가리지 않는다).
    ///   · 보상 그림(첫 보상의 재화 · 재료 그림, 없으면 퀘스트 그림) + 양 '×20' + 문구 두 줄까지 + "2/3" + 얇은 진행 막대. 완료면 금빛으로 반짝이며 "받기".
    ///   · 누르면 완료 → TryClaim + 보상 날아가기 + 0.4초 뒤집기(다음 퀘스트), 아니면 MetaNavigator.GoAndFocus(이동 기능, 대상) —
    ///     이동이 팝업을 열면 손가락은 팝업 안 버튼으로 간다. 이동할 곳이 없는 스테이지 퀘스트는 살짝 흔들고 '전투력 N · 권장 M' 토스트,
    ///     평가가 '어려움'이면 대신 강해지기 시트(StrengthenPopup, M13)를 연다 (튜토리얼 중에는 열지 않는다).
    ///   · 흐림 (M04): 완료면 늘 알파 1 (가까운 적 · 화살표 · 띠와 상관없이). 진행 중에는 판 묶음 "Chrome"(판 · 받침 · 막대 — 두 번째 CanvasGroup)만
    ///     적이 줄에서 48px 안에 있거나 켜진 위험 화살표가 겹치면 0.35, 시너지 발동 띠 아래에서는 0.3으로 흐려지고, 글자(문구 · 진행 · 양)는
    ///     흐려지지 않는다. 시너지 발동 띠(Battle/SynergyBannerArea/SynergyBanner)가 떠 있는 동안은 루트 전체가 0.9 (글자는 0.9 밑으로 내리지 않는다 — 판은 0.9 × 0.3).
    ///     적 위치는 Game.Battle.Sim 읽기 전용, 할당 없음. 전투 UI 파일은 고치지 않고 빌더가 묶어 주거나(없으면) 이름으로 찾는다.
    ///   · 진행 막대는 목표로 미끄러진다 (UIMotion.EaseFill, M62). 퀘스트가 바뀌거나 켜질 때는 바로 맞춘다. 완료로 바뀌는 순간 QuestComplete 소리.
    ///   · GuideQuest.IsVisible이 거짓이면(튜토리얼 첫 보스 전) 숨는다. 글자 갱신은 GuideQuest.Changed에서만.
    ///   · 전투 화면이 이 줄을 피해 그리려면 TryGetRect로 줄의 사각형을 읽는다 (위험 화살표 배치 등).
    /// </summary>
    public class GuideQuestBarView : MonoBehaviour
    {
        /// <summary>적이 줄에서 이만큼(px) 안에 들어오면 판 묶음을 흐린다 (M04: 120 → 48 — 줄 바로 옆을 지날 때만)</summary>
        const float NearDistance = 48f;
        /// <summary>진행 중 판 묶음(Chrome) 알파: 적 · 위험 화살표가 가까울 때 / 시너지 발동 띠 아래</summary>
        const float NearChromeAlpha = 0.35f;
        const float BannerChromeAlpha = 0.3f;
        /// <summary>
        /// 시너지 발동 띠가 떠 있는 동안 루트 알파 (예전 0.2 — 글자까지 거의 사라졌다). S §2.1.9는 '루트 0.6'과 '글자는 0.9 밑으로 곱하지 않는다'가
        /// 서로 맞지 않아 글자 쪽을 따른다 (WP-7 결정): 루트 0.9 × 판 묶음 0.3 = 판 0.27로 띠가 비쳐 보이고, 글자 · 진행 · 양은 0.9로 읽힌다
        /// </summary>
        const float BannerAlpha = 0.9f;
        const float FadeSpeed = 4f;
        const float BannerFadeSpeed = 8f;
        const float ProximityInterval = 0.1f;
        const float FlipHalf = 0.2f;
        const float ShineInterval = 2.4f;
        const float ShineDuration = 0.7f;

        [SerializeField] MetaIcons _icons;
        [SerializeField] Button _button;
        [Tooltip("루트 알파 (시너지 발동 띠 아래에서만 0.9 — 글자가 0.9 밑으로 내려가지 않게)")]
        [SerializeField] CanvasGroup _group;
        [Tooltip("판 · 아이콘 받침 · 진행 막대 묶음 — 가까운 적 · 띠에 흐려진다 (글자는 밖에 있어 흐려지지 않는다). 없으면 루트 알파로 흐린다")]
        [SerializeField] CanvasGroup _chrome;
        [Tooltip("뒤집기 연출을 받는 내용 루트")]
        [SerializeField] RectTransform _content;
        [SerializeField] Image _panel;
        [SerializeField] Image _goldEdge;
        [SerializeField] Image _icon;
        [SerializeField] Image _iconBack;
        [SerializeField] TMP_Text _text;
        [SerializeField] TMP_Text _progressText;
        [Tooltip("보상 그림 오른쪽 아래 양 '×20' (없어도 됨)")]
        [SerializeField] TMP_Text _amountText;
        [SerializeField] Image _progressFill;
        [Tooltip("완료 시 \"받기\" 알약")]
        [SerializeField] GameObject _claimTag;
        [SerializeField] RectTransform _shine;
        [Tooltip("전장 좌표 변환 (적 근접 판정)")]
        [SerializeField] BattleFieldMapper _mapper;
        [Header("비켜 줄 전투 화면 (없으면 Start에서 이름으로 찾음)")]
        [Tooltip("Battle/SynergyBannerArea/SynergyBanner — 켜져 있는 동안 줄을 흐린다")]
        [SerializeField] GameObject _synergyBanner;
        [SerializeField] CanvasGroup _synergyBannerGroup;
        [Tooltip("방위별 위험 화살표 루트 (Battle/.../Danger{i})")]
        [SerializeField] RectTransform[] _dangerArrows = System.Array.Empty<RectTransform>();
        [Tooltip("위험 화살표의 빛 (켜져 있으면 화살표가 보이는 중)")]
        [SerializeField] Image[] _dangerGlows = System.Array.Empty<Image>();

        /// <summary>완료일 때 판 그림에 섞는 금빛 비율 (PanelCompleteColor = Gold@0.18 over 밤 판 — 그림을 단색으로 덮지 않게 옅게)</summary>
        const float CompletePanelTint = 0.18f;
        const float CompletePlateTint = 0.45f;

        static GuideQuestBarView s_instance;
        static readonly Vector3[] s_corners = new Vector3[4];

        RectTransform _rect;
        bool _subscribed;
        bool _covered;
        bool _complete;
        bool _flipping;
        bool _refreshPending;
        bool _primed;
        bool _snapFill = true;
        float _proximityTimer;
        float _targetAlpha = 1f;
        float _chromeTarget = 1f;
        /// <summary>진행 막대가 지금 보여 주는 비율 / 목표 (EaseFill로 따라간다)</summary>
        float _fillShown;
        float _fillTarget;
        float _shineTimer;
        float _pulse;
        Color _panelBase = Color.white;
        Color _iconBackBase = Color.white;
        string _shownText;
        string _shownProgress;
        string _shownQuestId;

        void Awake()
        {
            _rect = (RectTransform)transform;
            s_instance = this;
            // 빌더가 정한 기본 색 (아트면 흰색, 자리표시면 밤색) — 상태는 여기에 금빛을 옅게 섞어서만 바꾼다
            if (_panel != null) _panelBase = _panel.color;
            if (_iconBack != null) _iconBackBase = _iconBack.color;
            if (_button != null) _button.onClick.AddListener(OnClick);
        }

        void Start()
        {
            FindBattleOverlaps();
            if (MetaUi.IsActive)
            {
                Game.GuideQuest.Changed += Refresh;
                _subscribed = true;
            }
            Refresh();
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            if (_subscribed && Game.IsReady && Game.GuideQuest != null) Game.GuideQuest.Changed -= Refresh;
        }

        /// <summary>
        /// 줄이 보이면 그 사각형을 space 좌표계로 돌려준다 (전투 화면이 위험 화살표 등을 이 줄 밖에 두려 할 때). 할당 없음.
        /// 줄이 없거나 숨어 있으면 false.
        /// </summary>
        public static bool TryGetRect(RectTransform space, out Rect rect)
        {
            rect = default;
            var bar = s_instance;
            if (bar == null || space == null || !bar.isActiveAndEnabled) return false;
            rect = UIFind.RectIn(bar._rect, space, s_corners);
            return true;
        }

        /// <summary>빌더가 묶지 못한 전투 화면 조각(시너지 띠·위험 화살표)을 MetaHud의 부모(Battle) 아래에서 한 번 찾는다</summary>
        void FindBattleOverlaps()
        {
            var hud = transform.parent;
            var battle = hud != null ? hud.parent : null;
            if (battle == null) return;
            if (_synergyBanner == null)
            {
                var banner = battle.Find("SynergyBannerArea/SynergyBanner");
                if (banner != null) _synergyBanner = banner.gameObject;
            }
            if (_synergyBanner != null && _synergyBannerGroup == null) _synergyBannerGroup = _synergyBanner.GetComponent<CanvasGroup>();
            if (_dangerArrows.Length == 0)
            {
                var arrows = battle.GetComponentsInChildren<DangerArrowView>(true);
                _dangerArrows = new RectTransform[arrows.Length];
                _dangerGlows = new Image[arrows.Length];
                for (int i = 0; i < arrows.Length; i++)
                {
                    _dangerArrows[i] = (RectTransform)arrows[i].transform;
                    var glow = arrows[i].transform.Find("Glow");
                    _dangerGlows[i] = glow != null ? glow.GetComponent<Image>() : null;
                }
            }
        }

        void OnEnable()
        {
            _group.alpha = _targetAlpha;
            if (_chrome != null) _chrome.alpha = _chromeTarget;
            if (_content != null) _content.localScale = Vector3.one;
            _flipping = false;
            // 다시 켜질 때 막대는 미끄러지지 않고 바로 맞춘다 (M62)
            _snapFill = true;
            SnapFill();
        }

        // ───────────── 표시 ─────────────

        void Refresh()
        {
            if (_flipping)
            {
                _refreshPending = true;
                return;
            }
            var guide = MetaUi.IsActive ? Game.GuideQuest : null;
            bool visible = guide != null && guide.IsVisible && guide.Current != null;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (!visible) return;
            ApplyContent(guide);
        }

        void ApplyContent(GuideQuestSystem guide)
        {
            var quest = guide.Current;
            if (quest == null) return;

            bool sameQuest = _shownQuestId == quest.id;
            if (!sameQuest)
            {
                _shownQuestId = quest.id;
                // 새 퀘스트: 막대는 미끄러지지 않고 바로 (M62), 보상 그림 · 양도 새로
                _snapFill = true;
                ApplyReward(quest);
            }
            if (_shownText != quest.text)
            {
                _shownText = quest.text;
                _text.text = quest.text;
            }
            bool wasComplete = _complete;
            _complete = guide.IsComplete;
            // 진행 중이던 같은 퀘스트가 지금 막 끝남 → 소리 (켤 때 · 다음 퀘스트로 넘어갈 때는 울리지 않는다)
            if (_primed && sameQuest && _complete && !wasComplete) UISfx.Play(UISfxKey.QuestComplete);
            _primed = true;

            string progress = guide.ProgressText;
            if (progress != _shownProgress)
            {
                _shownProgress = progress;
                _progressText.text = progress;
            }
            _fillTarget = _complete ? 1f : Mathf.Clamp01(guide.CurrentRatio);
            if (_snapFill) SnapFill();
            _progressFill.color = _complete ? UITheme.Gold : UITheme.Lavender;

            _claimTag.SetActive(_complete);
            _progressText.gameObject.SetActive(!_complete);
            _panel.color = _complete ? Tint(_panelBase, UITheme.Gold, CompletePanelTint) : _panelBase;
            _goldEdge.enabled = _complete;
            _iconBack.color = _complete ? Tint(_iconBackBase, UITheme.Gold, CompletePlateTint) : _iconBackBase;
            _text.color = _complete ? UITheme.Cream : UITheme.Text;
            if (_shine != null) _shine.gameObject.SetActive(_complete);
            _shineTimer = ShineInterval * 0.5f;
        }

        /// <summary>
        /// 보상 그림 + 양 (M04): 첫 보상의 재화 · 재료 그림 (없으면 퀘스트 그림 → 이동 기능 그림 → 퀘스트 기본 그림 → 별).
        /// 양은 '×20' (UIFormat.TimesSmall + 줄인 숫자 FormatCompact, 단추 N분치는 'N분치', 친구 · 의상은 비움). 퀘스트가 바뀔 때만 부른다
        /// </summary>
        void ApplyReward(GuideQuestDef quest)
        {
            var reward = quest.rewards != null && quest.rewards.Count > 0 ? quest.rewards[0] : null;
            Sprite sprite = reward != null && _icons != null ? _icons.Reward(reward) : null;
            if (sprite == null) sprite = quest.icon;
            if (sprite == null && _icons != null) sprite = _icons.Feature(quest.navFeature);
            if (sprite == null && _icons != null) sprite = _icons.Quest;
            if (sprite == null && _icons != null) sprite = _icons.Star;
            _icon.sprite = sprite;
            _icon.color = Color.white;
            _icon.enabled = sprite != null;
            if (_amountText == null) return;
            string amount = RewardAmountLabel(reward);
            bool hasAmount = !string.IsNullOrEmpty(amount);
            if (_amountText.gameObject.activeSelf != hasAmount) _amountText.gameObject.SetActive(hasAmount);
            if (hasAmount) _amountText.text = amount;
        }

        /// <summary>보상 그림 옆 양 글자 ('×20', '×1.2K', '5분치'; 친구 · 의상 · 없음은 빈 글)</summary>
        internal static string RewardAmountLabel(MetaReward reward)
        {
            if (reward == null || !(reward.amount > 0)) return string.Empty;
            return reward.type switch
            {
                MetaRewardType.Lineage or MetaRewardType.Costume => string.Empty,
                MetaRewardType.GoldMinutes => MetaUi.ShortAmount(reward),
                MetaRewardType.Currency => UIFormat.TimesSmall + NumberFormatter.FormatCompact(reward.amount),
                _ => UIFormat.TimesSmall + NumberFormatter.FormatCompact(System.Math.Floor(reward.amount)),
            };
        }

        /// <summary>진행 막대를 목표 비율로 바로 맞춘다 (새 퀘스트 · 켜질 때)</summary>
        void SnapFill()
        {
            if (!_snapFill || _progressFill == null) return;
            _snapFill = false;
            _fillShown = _fillTarget;
            MetaUi.SetBar(_progressFill, _fillShown);
        }

        /// <summary>기본 색에 금빛을 amount만큼 섞는다 (알파는 기본 그대로)</summary>
        static Color Tint(Color baseColor, Color tint, float amount) => MetaUi.Alpha(Color.Lerp(baseColor, tint, amount), baseColor.a);

        // ───────────── 입력 ─────────────

        void OnClick()
        {
            if (!MetaUi.IsActive || _flipping) return;
            var guide = Game.GuideQuest;
            var quest = guide.Current;
            if (quest == null) return;

            if (guide.IsComplete)
            {
                if (guide.TryClaim(out var granted))
                {
                    MetaUi.FlyRewards(granted, _icon.rectTransform);
                    StartCoroutine(Flip());
                }
                return;
            }

            bool hasNav = quest.navFeature != FeatureId.Battle;
            if (hasNav && !Game.Features.TryUse(quest.navFeature, out var locked))
            {
                MetaUi.Toast(locked);
                return;
            }
            if (!hasNav && string.IsNullOrEmpty(quest.focusTarget))
            {
                OnStageQuestTapped();
                return;
            }
            // 이동이 팝업(바구니·일지·출석부)을 열면 손가락은 그 팝업 안 버튼으로 간다 (팝업 아래에 묻힌 대상 대신)
            MetaNavigator.GoAndFocus(quest.navFeature, quest.focusTarget);
        }

        /// <summary>
        /// 이동할 곳이 없는 스테이지 퀘스트를 누름 (M04 · M13): 평가 '어려움'이면 강해지기 시트 (튜토리얼 밖, 시트가 장면에 있을 때),
        /// 아니면 줄을 살짝 흔들고 '전투력 N · 권장 M' 토스트
        /// </summary>
        void OnStageQuestTapped()
        {
            var power = Game.Power;
            int stage = Game.Battle.Stage;
            long recommended = power.GetRecommendedPower(stage);
            long mine = power.TeamPower;
            bool tutorial = Game.Tutorial != null && Game.Tutorial.IsActive;
            if (!tutorial && power.GetRating(mine, recommended) == PowerRating.Hard && StrengthenPopup.TryOpen()) return;
            StartCoroutine(Nudge());
            MetaUi.Toast($"전투력 {PowerSystem.Format(mine)} · 권장 {PowerSystem.Format(recommended)}");
        }

        // ───────────── 연출 ─────────────

        void Update()
        {
            if (!MetaUi.IsActive) return;
            float dt = Time.unscaledDeltaTime;

            _proximityTimer -= dt;
            if (_proximityTimer <= 0f)
            {
                _proximityTimer = ProximityInterval;
                _covered = IsEnemyNear() || IsDangerArrowOver();
            }
            // 시너지 띠는 짧게(3초 남짓) 뜨므로 매 프레임 확인한다 (bool·float 읽기만)
            bool banner = IsSynergyBannerShown();
            // 완료면 늘 또렷하게 (받기를 놓치지 않게), 진행 중에는 판 묶음만 흐리고 글자는 남긴다 — 띠 아래에서는 루트도 0.9 (글자 0.9 · 판 0.27)
            if (_complete)
            {
                _targetAlpha = 1f;
                _chromeTarget = 1f;
            }
            else
            {
                _targetAlpha = banner ? BannerAlpha : 1f;
                _chromeTarget = banner ? BannerChromeAlpha : _covered ? NearChromeAlpha : 1f;
                // 판 묶음이 없는 예전 장면: 루트로 흐린다 (글자도 같이)
                if (_chrome == null && _covered && !banner) _targetAlpha = NearChromeAlpha;
            }
            float speed = dt * (banner ? BannerFadeSpeed : FadeSpeed);
            float alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, speed);
            if (!Mathf.Approximately(alpha, _group.alpha)) _group.alpha = alpha;
            if (_chrome != null)
            {
                float chrome = Mathf.MoveTowards(_chrome.alpha, _chromeTarget, speed);
                if (!Mathf.Approximately(chrome, _chrome.alpha)) _chrome.alpha = chrome;
            }

            // 진행 막대: 목표로 미끄러진다 (줄어들면 바로, M62)
            if (_fillShown != _fillTarget)
            {
                _fillShown = UIMotion.EaseFill(_fillShown, _fillTarget, dt);
                MetaUi.SetBar(_progressFill, _fillShown);
            }

            if (!_complete || _flipping) return;

            // 받기: 판 뒤 금빛 빛무리가 숨 쉬듯 밝아지고, 빛 줄기가 2.4초마다 훑고 지나간다
            _pulse += dt;
            float glow = 0.55f + 0.45f * Mathf.Sin(_pulse * 4f);
            _goldEdge.color = MetaUi.Alpha(UITheme.Gold, 0.35f + 0.55f * glow);
            if (_shine == null) return;
            _shineTimer += dt;
            if (_shineTimer > ShineInterval) _shineTimer = 0f;
            float k = Mathf.Clamp01(_shineTimer / ShineDuration);
            float width = _rect.rect.width;
            _shine.anchoredPosition = new Vector2(Mathf.Lerp(-width * 0.6f, width * 0.6f, MetaUi.EaseOutCubic(k)), 0f);
        }

        /// <summary>스테이지 시작 "시너지 발동!" 띠가 화면에 보이는 중인지 (지연 중 알파 0이면 아님)</summary>
        bool IsSynergyBannerShown()
        {
            if (_synergyBanner == null || !_synergyBanner.activeInHierarchy) return false;
            return _synergyBannerGroup == null || _synergyBannerGroup.alpha > 0.02f;
        }

        /// <summary>켜진 위험 화살표가 줄과 겹치는지 (화살표 크기는 배율을 반영한 반지름으로 어림). 할당 없음</summary>
        bool IsDangerArrowOver()
        {
            var bounds = _rect.rect;
            for (int i = 0; i < _dangerArrows.Length; i++)
            {
                var arrow = _dangerArrows[i];
                var glow = i < _dangerGlows.Length ? _dangerGlows[i] : null;
                if (arrow == null || glow == null || !glow.enabled || !arrow.gameObject.activeInHierarchy) continue;
                var size = arrow.rect.size;
                float radius = 0.5f * Mathf.Max(size.x, size.y) * Mathf.Abs(arrow.localScale.x);
                Vector2 local = _rect.InverseTransformPoint(arrow.position);
                if (local.x >= bounds.xMin - radius && local.x <= bounds.xMax + radius
                    && local.y >= bounds.yMin - radius && local.y <= bounds.yMax + radius) return true;
            }
            return false;
        }

        /// <summary>살아 있는 적 중 하나라도 줄(화면 사각형)에서 NearDistance 안에 있는지. 할당 없음</summary>
        bool IsEnemyNear()
        {
            if (_mapper == null || Game.Battle == null) return false;
            var sim = Game.Battle.Sim;
            if (sim == null) return false;
            var enemies = sim.Enemies;
            if (enemies == null || enemies.Count == 0) return false;

            var bounds = _rect.rect;
            float minX = bounds.xMin - NearDistance, maxX = bounds.xMax + NearDistance;
            float minY = bounds.yMin - NearDistance, maxY = bounds.yMax + NearDistance;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.Alive) continue;
                // 전투 카메라 기준 (시트가 열리면 전투가 올라간다, UX-SPEC-IA §6.4)
                var world = _mapper.FieldToCanvasWorld(_mapper.WorldToField(enemy.Position));
                var mine = _rect.InverseTransformPoint(world);
                if (mine.x >= minX && mine.x <= maxX && mine.y >= minY && mine.y <= maxY) return true;
            }
            return false;
        }

        /// <summary>0.4초 뒤집기: 반쯤 접혔을 때 다음 퀘스트로 바꾼다</summary>
        IEnumerator Flip()
        {
            _flipping = true;
            for (float t = 0f; t < FlipHalf; t += Time.unscaledDeltaTime)
            {
                _content.localScale = new Vector3(1f, 1f - MetaUi.EaseInCubic(t / FlipHalf), 1f);
                yield return null;
            }
            _content.localScale = new Vector3(1f, 0f, 1f);
            _flipping = false;
            _refreshPending = false;
            Refresh();
            if (!gameObject.activeInHierarchy) yield break;
            _flipping = true;
            for (float t = 0f; t < FlipHalf; t += Time.unscaledDeltaTime)
            {
                _content.localScale = new Vector3(1f, MetaUi.EaseOutBack(t / FlipHalf), 1f);
                yield return null;
            }
            _content.localScale = Vector3.one;
            _flipping = false;
            if (_refreshPending)
            {
                _refreshPending = false;
                Refresh();
            }
        }

        /// <summary>이동할 곳이 없는 퀘스트(스테이지 클리어 등)를 눌렀을 때 살짝 흔들기</summary>
        IEnumerator Nudge()
        {
            const float duration = 0.3f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                _content.anchoredPosition = new Vector2(Mathf.Sin(k * Mathf.PI * 4f) * 8f * (1f - k), 0f);
                yield return null;
            }
            _content.anchoredPosition = Vector2.zero;
        }
    }
}
