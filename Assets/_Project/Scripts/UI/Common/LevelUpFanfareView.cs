using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 성장 팡파르 (FX-04). 강화 탭 화면(GrowthPanel)은 다른 작업 중이라 고치지 않고, 그 위에 이 층이 덮어 그린다.
    ///   · 수선점 강화(Growth.LevelChanged): 수선점 그림 자리에서 금색 고리가 퍼지고 "Lv.UP" 리본이 0.6초 튄다.
    ///     10레벨을 넘을 때마다 내구도 글자 옆에 "10레벨 동안 내구도 +N" 칩 (지난 10레벨 동안 늘어난 양 — 따로 받는 보너스가 아니다, M35).
    ///     칩은 글 폭에 맞춰 줄이고 카드 글(레벨 · 초당 · 내구도 줄) 끝 오른쪽, 내구도 줄 높이에 둔다 — 틈이 좁으면 더 좁히고 (글이 자동 크기로 줄어든다),
    ///     그래도 없으면 카드 오른쪽 끝에 떠오르지 않고. 언제나 카드 안이다 (카드 아래 16에는 강화 버튼 줄이 있다).
    ///     떠오르는 동안에도 'Lv.N' 위에는 오지 않는다 (PlaceMilestoneChip — Wave 3 r1 35_focus_hold).
    ///   · 친구 레벨업(Characters.LevelUp): 그 친구 카드(없으면 방금 누른 자리)가 튀고 별빛이 터진다.
    ///     새 진화 단서가 생긴 레벨이면 "새 진화 단서가 보여요!", 아니면 10레벨마다 "Lv.20 달성! 모든 형태가 함께 강해져요".
    ///   · 진화 제작(Evolution.FormUnlocked): 제작 팝업이 열려 있으면(UIPopup.IsOpen — 닫히는 연출 중인 팝업은 열린 것이 아니다) 그 창 뒤로
    ///     빛살·색종이를 붙이고, 아니면 화면 가운데에 "새 형태 해금!" 카드 + 축하 연출.
    ///   · 공명(Characters.ResonanceUp) / 의상 해금(Costumes.CostumeUnlocked): 짧은 칩.
    /// 길게 눌러 연속 강화해도 조각을 새로 만들지 않고 타이머만 다시 건다. Update는 할당 없음 (칩 문구는 이벤트 때만 만든다).
    /// 진동 · 소리 (M18, UX-SPEC §3.15 · AUDIO-SPEC §8.1): 레벨업 · 공명 · 진화 팡파르에 Medium 진동 + 소리 하나 — 0.6초 넘게 쉬었다 온 팡파르에만
    /// (길게 눌러 연속으로 오르는 동안에는 처음 한 번, 반복마다 떨지 않는다). 소리는 수선점 강화 LevelUp (10레벨 고비를 넘으면 LevelUpMilestone),
    /// 친구 레벨업 FriendLevelUp (새 진화 단서 · 10레벨 고비 칩이 뜨면 LevelUpMilestone), 공명 ResonanceUp,
    /// 진화는 제작 팝업이 열려 있으면 진동만 (EvolveBurst · EvolveReveal은 그 팝업이 낸다 — 겹치지 않게), 아니면 EvolveReveal.
    /// 의상 해금 칩은 CostumeUnlock (진동 없음, 쉼과 상관없이). 클립이 없으면 조용히 지나간다.
    /// 소리를 낸(또는 제작 팝업에 맡긴) 순간은 기록해 둔다 (SoundClaimedWithin) — 한두 프레임 뒤 같은 순간에 오는 전투력 '+N'(PowerUpFloatView)은
    /// PowerUp을 따로 내지 않는다 (LevelUp · PowerUp 둘 다 첼레스타 오름이라 한 번 누름에 반짝 소리가 겹친다).
    /// 할 일이 없으면 Update를 끈다. 별빛 자리를 정할 손가락 위치는 매 프레임 지켜보지 않고 레벨업 순간에 읽는다
    /// (레벨업은 누르는 중이거나 뗀 그 프레임에 온다).
    /// 전투력 "+N"은 PowerUpFloatView가 따로 띄운다.
    /// </summary>
    public class LevelUpFanfareView : MonoBehaviour
    {
        /// <summary>진동 · 소리 사이 최소 쉼 (이보다 촘촘히 오는 팡파르는 조용히 — 길게 누르기 반복)</summary>
        const float CueQuiet = 0.6f;

        // ── 칩 ──
        /// <summary>칩이 머무는 동안 떠오르는 높이 (StepChips)</summary>
        internal const float ChipRise = 60f;
        /// <summary>칩을 화면 안에 둘 때 가운데가 가장자리에서 떨어지는 거리 (원래 폭 600 칩 — 예전 그대로). 좁힌 칩은 반 폭 + ChipScreenMargin</summary>
        const float ChipEdgeX = 260f;
        const float ChipScreenMargin = 12f;
        /// <summary>글에 맞춘 칩의 글 좌우 여백 (밤 알약 앞면 안쪽 26 + 숨 쉴 틈) · 최소 폭</summary>
        const float ChipTextPad = 32f;
        const float ChipMinWidth = 240f;
        /// <summary>10레벨 고비 칩과 그 왼쪽 카드 글 끝 사이 틈 (처음 0.2초 OutBack으로 커져도 글에 닿지 않게)</summary>
        const float MilestoneChipGap = 24f;
        /// <summary>10레벨 고비 칩 오른쪽 끝과 카드 오른쪽 끝 사이 최소 여백</summary>
        const float MilestoneChipCardPad = 16f;
        /// <summary>강화 카드 글 이름 (MenuSceneBuilder.BuildGrowthPanel — ShopCard 바로 아래 자식)</summary>
        const string ShopLevelTextName = "LevelText";
        const string ShopStatTextName = "StatText";
        const string ShopHpTextName = "HpText";

        /// <summary>팡파르가 마지막으로 소리를 맡은 실제 시각 (Cue가 울림 · 진화 제작 팝업에 맡김, 의상 해금 칩)</summary>
        static float s_soundClaimedAt = float.NegativeInfinity;

        /// <summary>
        /// seconds초 안에 팡파르가 소리를 냈는지 (진화 제작 팝업에 맡긴 순간 포함) — PowerUpFloatView가 같은 순간의 '+N' 소리를 건너뛸 때 본다.
        /// 재생을 다시 시작해 시간이 0으로 돌아갔으면 (도메인 다시 불러오기를 끈 에디터) false
        /// </summary>
        internal static bool SoundClaimedWithin(float seconds)
        {
            float since = Time.unscaledTime - s_soundClaimedAt;
            return since >= 0f && since < seconds;
        }

        [SerializeField] RectTransform _canvasRoot;

        [Header("수선점 강화")]
        [SerializeField] Image _ring;
        [SerializeField] Image _ringGlow;
        [SerializeField] RectTransform _ribbon;
        [SerializeField] TMP_Text _ribbonText;
        [SerializeField] CanvasGroup _ribbonGroup;
        [Tooltip("강화 탭 수선점 카드 (Canvas 기준)")]
        [SerializeField] string _shopCardPath = CoachTargets.GrowthPanel + "/ShopCard";
        [Tooltip("수선점 그림 중심 (카드 왼쪽 가운데 기준)")]
        [SerializeField] Vector2 _shopArtOffset = new(133f, 0f);

        [Header("칩 (돌려 쓴다)")]
        [SerializeField] RectTransform[] _chips;
        [SerializeField] TMP_Text[] _chipTexts;
        [SerializeField] CanvasGroup[] _chipGroups;

        [Header("별빛")]
        [SerializeField] Image _sparkTemplate;
        [SerializeField] int _sparkPool = 24;

        [Header("진화")]
        [SerializeField] RectTransform _evolutionCard;
        [SerializeField] CanvasGroup _evolutionGroup;
        [SerializeField] Image _evolutionPortrait;
        [SerializeField] TMP_Text _evolutionName;
        [SerializeField] CelebrationFx _celebration;
        [SerializeField] Sprite _raysSprite;
        [Tooltip("색종이 그림 (흰색, 여러 장)")]
        [SerializeField] Sprite[] _confettiSprites;
        [SerializeField] Sprite _sparkSprite;

        struct Spark
        {
            public bool Active;
            public Image Image;
            public RectTransform Rect;
            public float Time;
            public float Duration;
            public Vector2 From;
            public Vector2 To;
            public float Size;
        }

        struct Chip
        {
            public bool Active;
            public float Time;
            public Vector2 From;
            /// <summary>머무는 동안 떠오르는 높이 (보통 ChipRise — 자리가 모자란 10레벨 고비 칩은 0)</summary>
            public float Rise;
        }

        /// <summary>10레벨 고비 칩 자리 (PlaceMilestoneChip): 가운데 (이 층 좌표) · 폭 · 떠오르는 높이</summary>
        internal readonly struct ChipPlace
        {
            public readonly Vector2 Center;
            public readonly float Width;
            public readonly float Rise;

            public ChipPlace(Vector2 center, float width, float rise)
            {
                Center = center;
                Width = width;
                Rise = rise;
            }
        }

        RectTransform _layer;
        GrowthSystem _growth;
        CharacterSystem _characters;
        EvolutionSystem _evolution;
        CostumeSystem _costumes;

        RectTransform _shopCard;
        RectTransform _shopHpText;
        RectTransform _shopLevelText;
        RectTransform _shopStatText;
        float _shopTime = -1f;
        Vector2 _shopAnchor;

        Spark[] _sparks;
        Chip[] _chipStates;
        /// <summary>칩 원래 폭 (빌더 600 — 고비 칩만 글에 맞춰 줄이고, 돌려 쓸 때 되돌린다)</summary>
        float[] _chipWidths;
        int _nextChip;

        readonly Dictionary<string, int> _knownLevels = new();
        readonly List<GrowthLineageCardView> _cards = new();
        RectTransform _punchTarget;
        Vector3 _punchBaseScale = Vector3.one;
        float _punch = -1f;

        float _evolutionTime = -1f;
        GameObject _craftPopup;
        /// <summary>제작 팝업의 UIPopup (열림은 IsOpen으로 본다 — 닫히는 0.12초 동안은 켜져 있어도 닫힌 것)</summary>
        UIPopup _craftPopupView;
        float _lastCue = float.NegativeInfinity;

        void Awake()
        {
            _layer = (RectTransform)transform;
            _sparks = new Spark[Mathf.Max(8, _sparkPool)];
            for (int i = 0; i < _sparks.Length; i++)
            {
                var image = Instantiate(_sparkTemplate, _layer);
                image.raycastTarget = false;
                image.gameObject.SetActive(false);
                _sparks[i] = new Spark { Image = image, Rect = image.rectTransform };
            }
            _sparkTemplate.gameObject.SetActive(false);
            _chipStates = new Chip[_chips != null ? _chips.Length : 0];
            _chipWidths = new float[_chipStates.Length];
            for (int i = 0; i < _chipStates.Length; i++)
            {
                _chipWidths[i] = _chips[i].rect.width;
                _chips[i].gameObject.SetActive(false);
            }
            SetShopFxVisible(false);
            if (_evolutionCard != null) _evolutionCard.gameObject.SetActive(false);
        }

        void Start()
        {
            if (!Game.IsReady) return;
            _growth = Game.Growth;
            _characters = Game.Characters;
            _evolution = Game.Evolution;
            _costumes = Game.Costumes;
            _growth.LevelChanged += OnShopLevelChanged;
            _characters.LevelUp += OnLineageLevelUp;
            _characters.ResonanceUp += OnResonanceUp;
            _evolution.FormUnlocked += OnFormUnlocked;
            _costumes.CostumeUnlocked += OnCostumeUnlocked;
            _craftPopup = UIFind.Path(_canvasRoot, "Popups/EvolutionCraftPopup")?.gameObject;
            enabled = false; // 팡파르가 올 때만 돈다
        }

        void OnDestroy()
        {
            if (_growth != null) _growth.LevelChanged -= OnShopLevelChanged;
            if (_characters != null)
            {
                _characters.LevelUp -= OnLineageLevelUp;
                _characters.ResonanceUp -= OnResonanceUp;
            }
            if (_evolution != null) _evolution.FormUnlocked -= OnFormUnlocked;
            if (_costumes != null) _costumes.CostumeUnlocked -= OnCostumeUnlocked;
        }

        // ───────────────────────────── 수선점 강화 ─────────────────────────────

        void OnShopLevelChanged(int oldLevel, int newLevel)
        {
            if (newLevel <= oldLevel || !gameObject.activeInHierarchy) return;
            if (_shopCard == null) _shopCard = UIFind.Path(_canvasRoot, _shopCardPath);
            if (!UIFind.IsShown(_shopCard)) return;

            var cardRect = UIFind.RectIn(_shopCard, _layer, Corners);
            _shopAnchor = new Vector2(cardRect.xMin, cardRect.center.y) + _shopArtOffset;
            _shopTime = 0f;
            SetShopFxVisible(true);
            enabled = true;
            // 10레벨 고비를 넘음 (x10 · 최대 강화로 건너뛰어도) — 큰 팡파르 + 내구도 칩
            int milestone = newLevel / 10 * 10;
            bool crossed = milestone > oldLevel && milestone >= 10;
            Cue(crossed ? UISfxKey.LevelUpMilestone : UISfxKey.LevelUp);
            int delta = newLevel - oldLevel;
            if (delta <= 1) _ribbonText.SetText("Lv.UP!");
            else _ribbonText.SetText("Lv.UP +{0:0}", delta);
            for (int i = 0; i < 6; i++) SpawnSpark(_shopAnchor + new Vector2(0f, 20f), UITheme.Lamp, 70f, 150f);

            if (crossed)
            {
                double gain = _growth.GetShopMaxHpAt(milestone) - _growth.GetShopMaxHpAt(milestone - 10);
                ShowMilestoneChip($"10레벨 동안 내구도 +{NumberFormatter.Format(gain)}", cardRect);
            }
        }

        /// <summary>
        /// 10레벨 고비 칩: 글 폭에 맞춰 줄이고 카드 글(레벨 · 초당 · 내구도 줄) 끝 오른쪽 · 내구도 줄 높이에 — 틈이 좁으면 더 좁히고,
        /// 그래도 없거나 글을 못 찾으면 카드 오른쪽 끝에 떠오르지 않고 (PlaceMilestoneChip). 카드 밖(아래 강화 버튼 줄)으로는 내리지 않는다.
        /// 'Lv.N' 위에는 놓지 않는다 (Wave 3 r1 — 예전 내구도 글 가운데 + (60, 34)는 떠오르며 'Lv.10'을 덮었다). 이벤트 때만 (Find · 글 폭 재기)
        /// </summary>
        void ShowMilestoneChip(string message, Rect card)
        {
            if (_chipStates.Length == 0) return;
            int i = _nextChip;
            float width = _chipWidths[i];
            float narrowest = width;
            var label = _chipTexts[i];
            if (label != null)
            {
                // 글을 먼저 넣어 폭을 잰다 (ShowChip이 같은 칩에 같은 글을 다시 넣는다 — 같으면 TMP가 건너뛴다)
                label.text = message;
                float measured = MeasureWidth(label);
                if (measured > 0f)
                {
                    width = FitChipWidth(measured, width);
                    narrowest = NarrowChipWidth(measured, AutoSizeShrink(label), width);
                }
            }
            if (_shopLevelText == null) _shopLevelText = _shopCard.Find(ShopLevelTextName) as RectTransform;
            if (_shopStatText == null) _shopStatText = _shopCard.Find(ShopStatTextName) as RectTransform;
            if (_shopHpText == null) _shopHpText = _shopCard.Find(ShopHpTextName) as RectTransform;
            float ink = Mathf.Max(InkRight(_shopLevelText), Mathf.Max(InkRight(_shopStatText), InkRight(_shopHpText)));
            float lineY = _shopHpText != null && _shopHpText.gameObject.activeInHierarchy ? UIFind.CenterIn(_shopHpText, _layer).y : card.center.y;
            var place = PlaceMilestoneChip(card, ink, lineY, width, narrowest, _chips[i].rect.height);
            ShowChip(message, place.Center, UITheme.SoftBlue, place.Width, place.Rise);
        }

        /// <summary>
        /// 자동 크기 글이 줄어들 수 있는 비율 (최소 / 최대 크기 — 칩 글 18.2 / 26 = 0.7). 최소는 16(UIType.AutoSizeFloor) 아래로 치지 않는다
        /// (좁힌 칩에서도 글은 16 이상). 자동 크기가 아니면 1 (줄어들지 않는다)
        /// </summary>
        static float AutoSizeShrink(TMP_Text text)
        {
            if (!text.enableAutoSizing || !(text.fontSizeMax > 0f)) return 1f;
            return Mathf.Clamp01(Mathf.Max(text.fontSizeMin, UIType.AutoSizeFloor) / text.fontSizeMax);
        }

        /// <summary>왼쪽 정렬 글의 글 끝 x (이 층 좌표 — 글 폭은 자동 크기 최대로 재므로 넉넉하다). 없거나 꺼졌으면 −∞, 왼쪽 정렬이 아니면 글 칸 오른쪽 끝</summary>
        float InkRight(RectTransform label)
        {
            if (label == null || !label.gameObject.activeInHierarchy) return float.NegativeInfinity;
            var box = UIFind.RectIn(label, _layer, Corners);
            if (!label.TryGetComponent<TMP_Text>(out var text) || text.horizontalAlignment != HorizontalAlignmentOptions.Left) return box.xMax;
            float measured = MeasureWidth(text);
            if (!(measured > 0f)) return box.xMax;
            float local = label.rect.width;
            float scale = local > 0f ? box.width / local : 1f;
            return box.xMin + Mathf.Min(box.width, measured * scale);
        }

        /// <summary>글의 바람직한 폭 (자동 크기면 최대 크기로 — 넉넉하다). 재지 못하면 0 (예외는 기록만 — 강화 알림을 끊지 않는다). 이벤트 때만 부른다</summary>
        static float MeasureWidth(TMP_Text text)
        {
            try
            {
                float width = text.preferredWidth;
                return float.IsNaN(width) || float.IsInfinity(width) ? 0f : width;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return 0f;
            }
        }

        /// <summary>
        /// 10레벨 고비 칩 자리 (이 층 좌표, 순수 — 시험용). card = 강화 카드, inkRight = 카드 글 끝 (모르면 −∞ · NaN), lineY = 내구도 줄 가운데 높이,
        /// width = 글에 맞춘 폭 (FitChipWidth), narrowest = 글이 자동 크기 최소로 줄었을 때의 폭 (NarrowChipWidth), height = 칩 높이.
        /// 칩은 언제나 카드 안이다 — 카드 아래 16에 강화 버튼 줄(x1 · x10 · 최대)이 있어 그 위로 내리지 않는다.
        ///   ① 글 끝 + 24 ~ 카드 오른쪽 − 16 틈에 들어가면 그 자리 · 내구도 줄 높이, ChipRise만큼 떠오른다 (글 오른쪽이라 'Lv.N'을 가리지 않는다).
        ///      글에 맞춘 폭이 넘치면 틈만큼 좁힌다 (narrowest까지 — 글은 자동 크기로 줄어든다).
        ///   ② 그래도 모자라면 (긴 수 · 좁은 화면 · 글을 못 찾음) 카드 오른쪽 끝 · 내구도 줄 높이에 가장 좁게, 떠오르지 않는다 —
        ///      'Lv.N' 줄 높이에 닿지 않고 카드 아래 끝 위에 머문다 (내구도 · 초당 줄 끝은 잠깐 가릴 수 있다).
        /// </summary>
        internal static ChipPlace PlaceMilestoneChip(Rect card, float inkRight, float lineY, float width, float narrowest, float height)
        {
            float right = card.xMax - MilestoneChipCardPad;
            narrowest = Mathf.Min(narrowest, width);
            if (!float.IsInfinity(inkRight) && !float.IsNaN(inkRight))
            {
                float left = inkRight + MilestoneChipGap;
                float room = right - left;
                if (room >= narrowest)
                {
                    float fit = Mathf.Min(width, room);
                    return new ChipPlace(new Vector2(left + fit * 0.5f, lineY), fit, ChipRise);
                }
            }
            float tight = Mathf.Min(narrowest, card.width - 2f * MilestoneChipCardPad);
            float half = height * 0.5f;
            float y = Mathf.Clamp(lineY, card.yMin + half, Mathf.Max(card.yMin + half, card.yMax - half));
            return new ChipPlace(new Vector2(right - tight * 0.5f, y), tight, 0f);
        }

        /// <summary>글 폭에 맞춘 칩 폭 (글 + 좌우 32, 최소 240 · 최대 원래 폭)</summary>
        internal static float FitChipWidth(float textWidth, float baseWidth) =>
            Mathf.Clamp(textWidth + 2f * ChipTextPad, Mathf.Min(ChipMinWidth, baseWidth), baseWidth);

        /// <summary>
        /// 글이 자동 크기 최소로 줄었을 때 칩 폭 (글 × shrink + 좌우 32, 최대 baseWidth). shrink = 최소 / 최대 크기 (AutoSizeShrink, 칩 글 0.7).
        /// 앞면 안쪽 26보다 여백이 넉넉해 그 폭에서도 글은 한 줄로 들어간다
        /// </summary>
        internal static float NarrowChipWidth(float textWidth, float shrink, float baseWidth) =>
            Mathf.Min(textWidth * Mathf.Clamp01(shrink) + 2f * ChipTextPad, baseWidth);

        /// <summary>칩이 머무는 동안 지나가는 자리 (처음 자리에서 rise만큼 위까지 — 보통 ChipRise)</summary>
        internal static Rect ChipPath(Vector2 center, Vector2 size, float rise = ChipRise) =>
            new(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y + Mathf.Max(0f, rise));

        void SetShopFxVisible(bool visible)
        {
            if (_ring != null) _ring.gameObject.SetActive(visible);
            if (_ringGlow != null) _ringGlow.gameObject.SetActive(visible);
            if (_ribbon != null) _ribbon.gameObject.SetActive(visible);
        }

        void StepShop(float dt)
        {
            if (_shopTime < 0f) return;
            _shopTime += dt;
            const float duration = 0.6f;
            float k = Mathf.Clamp01(_shopTime / duration);

            var ring = _ring.rectTransform;
            ring.anchoredPosition = _shopAnchor;
            ring.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.55f, UIMotion.OutCubic(k));
            _ring.color = new Color(UITheme.Gold.r, UITheme.Gold.g, UITheme.Gold.b, 0.95f * (1f - k));

            if (_ringGlow != null)
            {
                _ringGlow.rectTransform.anchoredPosition = _shopAnchor;
                _ringGlow.color = new Color(UITheme.Lamp.r, UITheme.Lamp.g, UITheme.Lamp.b, 0.55f * Mathf.Sin(k * Mathf.PI));
            }

            // 리본: 톡 튀어 오르고 머물다 사라진다
            _ribbon.anchoredPosition = _shopAnchor + new Vector2(0f, 120f + 18f * UIMotion.OutCubic(k));
            _ribbon.localScale = Vector3.one * (_shopTime < 0.18f ? UIMotion.OutBack(_shopTime / 0.18f) : 1f);
            _ribbonGroup.alpha = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;

            if (k < 1f) return;
            _shopTime = -1f;
            SetShopFxVisible(false);
        }

        // ───────────────────────────── 친구 레벨업 ─────────────────────────────

        void OnLineageLevelUp(string lineageId, int newLevel)
        {
            if (!gameObject.activeInHierarchy) return;
            int previous = _knownLevels.TryGetValue(lineageId, out var known) ? known : newLevel - 1;
            _knownLevels[lineageId] = newLevel;
            var lineage = Game.Content?.Characters?.GetLineage(lineageId);
            if (lineage == null) return;

            // 카드를 못 찾으면(친구 상세 팝업 등) 누르는 손가락 자리, 그것도 없으면 어디서 올렸는지 모르니 조용히
            if (!TryFindLineageCard(lineage.name, out var card, out var at) && !PointerNow(out at)) return;
            enabled = true;
            string message = ClueMessage(lineage, previous, newLevel);
            if (message == null && newLevel / 10 > previous / 10) message = $"Lv.{newLevel / 10 * 10} 달성! 모든 형태가 함께 강해져요";
            // 새 진화 단서 · 10레벨 고비 (칩이 뜨는 레벨)는 큰 팡파르
            Cue(message != null ? UISfxKey.LevelUpMilestone : UISfxKey.FriendLevelUp);
            var color = Color.Lerp(lineage.accentColor, Color.white, 0.35f);
            for (int i = 0; i < 9; i++) SpawnSpark(at, color, 50f, 130f);
            if (card != null) Punch(card);

            if (message != null) ShowChip(message, at + new Vector2(0f, 110f), UITheme.Lavender);
        }

        string ClueMessage(LineageDef lineage, int previous, int level)
        {
            if (_evolution == null) return null;
            foreach (var form in lineage.forms)
            {
                if (form == null || form.tier == 0 || form.clueLevel <= previous || form.clueLevel > level) continue;
                if (_evolution.GetState(form.id) == EvolutionState.ClueFound) return "새 진화 단서가 보여요!";
            }
            return null;
        }

        bool TryFindLineageCard(string lineageName, out RectTransform card, out Vector2 center)
        {
            card = null;
            center = default;
            var panel = UIFind.Path(_canvasRoot, CoachTargets.GrowthPanel);
            if (panel == null || !panel.gameObject.activeInHierarchy) return false;
            panel.GetComponentsInChildren(false, _cards);
            foreach (var view in _cards)
            {
                var label = view.transform.Find("NameText");
                if (label == null || !label.TryGetComponent<TMP_Text>(out var text) || text.text != lineageName) continue;
                card = (RectTransform)view.transform;
                center = UIFind.CenterIn(card, _layer);
                _cards.Clear();
                return true;
            }
            _cards.Clear();
            return false;
        }

        void Punch(RectTransform target)
        {
            if (_punchTarget != null && _punchTarget != target && _punch >= 0f) _punchTarget.localScale = _punchBaseScale;
            if (_punchTarget != target || _punch < 0f) _punchBaseScale = target.localScale;
            _punchTarget = target;
            _punch = 0f;
        }

        void StepPunch(float dt)
        {
            if (_punch < 0f) return;
            if (_punchTarget == null)
            {
                _punch = -1f;
                return;
            }
            _punch += dt;
            const float time = 0.24f;
            float k = Mathf.Clamp01(_punch / time);
            _punchTarget.localScale = _punchBaseScale * (1f + 0.1f * Mathf.Sin(k * Mathf.PI));
            if (k < 1f) return;
            _punchTarget.localScale = _punchBaseScale;
            _punch = -1f;
        }

        // ───────────────────────────── 공명 / 의상 ─────────────────────────────

        void OnResonanceUp(string lineageId, int level)
        {
            if (!gameObject.activeInHierarchy) return;
            if (!PointerNow(out var at)) at = Vector2.zero;
            enabled = true;
            Cue(UISfxKey.ResonanceUp);
            for (int i = 0; i < 10; i++) SpawnSpark(at, UITheme.Resonance, 60f, 150f);
            ShowChip($"계보 공명 Lv.{level}! 모든 형태가 함께 강해져요", at + new Vector2(0f, 110f), UITheme.Resonance);
        }

        void OnCostumeUnlocked(string costumeId)
        {
            if (!gameObject.activeInHierarchy) return;
            enabled = true;
            UISfx.Play(UISfxKey.CostumeUnlock);
            s_soundClaimedAt = Time.unscaledTime;
            var costume = Game.Content?.Characters?.GetCostume(costumeId);
            var at = new Vector2(0f, _layer.rect.height * 0.18f);
            ShowChip(costume != null ? $"새 의상이 생겼어요! {costume.name}" : "새 의상이 생겼어요!", at, UITheme.DustyPink);
        }

        // ───────────────────────────── 진화 ─────────────────────────────

        void OnFormUnlocked(CharacterFormDef form)
        {
            if (form == null || !gameObject.activeInHierarchy) return;
            if (_craftPopup == null) _craftPopup = UIFind.Path(_canvasRoot, "Popups/EvolutionCraftPopup")?.gameObject;

            if (CraftPopupOpen())
            {
                // 진화 소리(EvolveBurst · EvolveReveal)는 제작 팝업이 낸다 — 여기서는 진동만 (소리가 겹치지 않게)
                Cue(null);
                // 제작 팝업에도 "해금 완료" 화면이 있다: 같은 글을 겹치지 않고 그 창 뒤로 빛살·색종이만 더한다
                CelebrationFx.Attach(_craftPopup, _raysSprite, _sparkSprite, _confettiSprites, playOnEnable: false).Play();
                return;
            }
            Cue(UISfxKey.EvolveReveal);

            if (_evolutionCard == null) return;
            _evolutionPortrait.sprite = form.portrait;
            _evolutionPortrait.enabled = form.portrait != null;
            _evolutionName.text = form.name;
            _evolutionCard.anchoredPosition = Vector2.zero;
            _evolutionCard.gameObject.SetActive(true);
            _evolutionTime = 0f;
            enabled = true;
            if (_celebration != null)
            {
                _celebration.gameObject.SetActive(true);
                _celebration.Play();
            }
        }

        /// <summary>진화 제작 팝업이 지금 열려 있는지 (UIPopup이 없는 옛 장면이면 켜져 있는지로)</summary>
        bool CraftPopupOpen()
        {
            if (_craftPopup == null) return false;
            if (_craftPopupView == null) _craftPopupView = _craftPopup.GetComponent<UIPopup>();
            return _craftPopupView != null ? _craftPopupView.IsOpen && _craftPopup.activeInHierarchy : _craftPopup.activeInHierarchy;
        }

        void StepEvolution(float dt)
        {
            if (_evolutionTime < 0f) return;
            _evolutionTime += dt;
            const float hold = 1.6f;
            float t = _evolutionTime;
            _evolutionCard.localScale = Vector3.one * (t < 0.3f ? UIMotion.OutBack(t / 0.3f) : 1f);
            _evolutionCard.anchoredPosition = new Vector2(0f, 8f * Mathf.Sin(t * 3f));
            _evolutionGroup.alpha = t < hold ? Mathf.Clamp01(t / 0.12f) : 1f - Mathf.Clamp01((t - hold) / 0.3f);
            if (t < hold + 0.3f) return;
            _evolutionTime = -1f;
            _evolutionCard.gameObject.SetActive(false);
            if (_celebration != null) _celebration.Stop();
        }

        // ───────────────────────────── 칩 / 별빛 ─────────────────────────────

        /// <summary>칩 하나를 띄운다 (돌려 쓴다). width &gt; 0이면 그 폭으로 (원래 폭까지), 아니면 원래 폭. rise = 머무는 동안 떠오르는 높이</summary>
        void ShowChip(string message, Vector2 at, Color color, float width = 0f, float rise = ChipRise)
        {
            if (_chipStates.Length == 0) return;
            int i = _nextChip;
            _nextChip = (_nextChip + 1) % _chipStates.Length;
            _chipTexts[i].text = message;
            _chipTexts[i].color = color;
            float chipWidth = width > 0f ? Mathf.Min(width, _chipWidths[i]) : _chipWidths[i];
            if (chipWidth > 0f && !Mathf.Approximately(_chips[i].rect.width, chipWidth))
                _chips[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, chipWidth);
            var rect = _layer.rect;
            // 화면 밖으로 나가지 않게 (원래 폭 칩은 예전처럼 260, 좁힌 칩은 반 폭 + 12)
            float edgeX = Mathf.Min(ChipEdgeX, chipWidth * 0.5f + ChipScreenMargin);
            at.x = Mathf.Clamp(at.x, rect.xMin + edgeX, rect.xMax - edgeX);
            at.y = Mathf.Clamp(at.y, rect.yMin + 120f, rect.yMax - 260f);
            _chipStates[i] = new Chip { Active = true, Time = 0f, From = at, Rise = Mathf.Max(0f, rise) };
            _chips[i].anchoredPosition = at;
            _chips[i].SetAsLastSibling();
            _chips[i].gameObject.SetActive(true);
        }

        void StepChips(float dt)
        {
            for (int i = 0; i < _chipStates.Length; i++)
            {
                ref var chip = ref _chipStates[i];
                if (!chip.Active) continue;
                chip.Time += dt;
                const float duration = 1.4f;
                float k = Mathf.Clamp01(chip.Time / duration);
                _chips[i].anchoredPosition = chip.From + new Vector2(0f, chip.Rise * UIMotion.OutCubic(k));
                _chips[i].localScale = Vector3.one * (chip.Time < 0.2f ? UIMotion.OutBack(chip.Time / 0.2f) : 1f);
                _chipGroups[i].alpha = k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;
                if (k < 1f) continue;
                chip.Active = false;
                _chips[i].gameObject.SetActive(false);
            }
        }

        void SpawnSpark(Vector2 at, Color color, float minDistance, float maxDistance)
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (_sparks[i].Active) continue;
                ref var spark = ref _sparks[i];
                float angle = Random.Range(0f, Mathf.PI * 2f);
                spark.Active = true;
                spark.Time = 0f;
                spark.Duration = Random.Range(0.45f, 0.7f);
                spark.From = at;
                spark.To = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(minDistance, maxDistance);
                spark.Size = Random.Range(26f, 48f);
                spark.Image.color = color;
                spark.Rect.sizeDelta = new Vector2(spark.Size, spark.Size);
                spark.Rect.SetAsLastSibling();
                spark.Image.gameObject.SetActive(true);
                return;
            }
        }

        void StepSparks(float dt)
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                ref var spark = ref _sparks[i];
                if (!spark.Active) continue;
                spark.Time += dt;
                float k = Mathf.Clamp01(spark.Time / spark.Duration);
                spark.Rect.anchoredPosition = Vector2.LerpUnclamped(spark.From, spark.To, UIMotion.OutCubic(k));
                spark.Rect.localScale = Vector3.one * (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f);
                spark.Rect.localRotation = Quaternion.Euler(0f, 0f, 180f * k);
                if (k < 1f) continue;
                spark.Active = false;
                spark.Image.gameObject.SetActive(false);
            }
        }

        // ───────────────────────────── 진동 · 소리 · 손가락 ─────────────────────────────

        /// <summary>
        /// 팡파르 진동(Medium) + 소리 하나 — 0.6초 넘게 쉬었다 온 팡파르에만 (연속으로 오르는 동안은 처음 한 번).
        /// sound가 null이면 진동만 (소리를 다른 화면이 내는 진화 제작). 울린 순간(null이면 맡긴 순간)을 SoundClaimedWithin에 남긴다
        /// </summary>
        void Cue(UISfxKey? sound)
        {
            float now = Time.unscaledTime;
            bool fresh = now - _lastCue >= CueQuiet;
            _lastCue = now;
            if (!fresh) return;
            s_soundClaimedAt = now;
            Haptics.Play(HapticKind.Medium);
            if (sound.HasValue) UISfx.Play(sound.Value);
        }

        /// <summary>
        /// 지금 누르고 있거나 이 프레임에 뗀 손가락 자리 (이 층 가운데 기준). 레벨업은 버튼을 뗀 프레임(탭)이나 누르는 중(길게 누르기)에 오므로
        /// 매 프레임 지켜보지 않고 그때 읽는다 (친구 상세 팝업처럼 카드를 못 찾을 때 별빛 위치). 누르고 있지 않으면 false
        /// </summary>
        bool PointerNow(out Vector2 local)
        {
            local = default;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, Input.mousePosition, null, out var point)) return false;
            local = point - _layer.rect.center;
            return true;
        }

        // ───────────────────────────── 프레임 ─────────────────────────────

        static readonly Vector3[] Corners = new Vector3[4];

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            StepShop(dt);
            StepPunch(dt);
            StepChips(dt);
            StepSparks(dt);
            StepEvolution(dt);
            if (!IsBusy()) enabled = false;
        }

        /// <summary>움직이는 것이 하나라도 있는지 (없으면 Update를 끈다)</summary>
        bool IsBusy()
        {
            if (_shopTime >= 0f || _punch >= 0f || _evolutionTime >= 0f) return true;
            for (int i = 0; i < _chipStates.Length; i++)
            {
                if (_chipStates[i].Active) return true;
            }
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (_sparks[i].Active) return true;
            }
            return false;
        }
    }
}
