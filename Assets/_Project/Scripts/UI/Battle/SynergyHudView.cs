using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 스테이지 정보 아래 작은 줄 (SynergyRow — 시트가 열리면 SheetFade 0으로 사라진다): 이번 스테이지 기믹 칩(아이콘 + "비 오는 밤") +
    /// 시너지 요약 칩 하나 (M43, UX-SPEC §2.1.6 — 예전 아이콘 8칸 대신).
    /// <para>
    /// 요약 칩 'SynergySummary': 켜진 시너지 아이콘 최대 3개(40px 원, 3px 시너지 색 테, 가운데 x 30/58/86 — 첫 아이콘이 맨 위) + '시너지 N'(N 금빛) + '›'.
    /// 폭 = 114 + 글자 폭 + 34 (200~280). 켜진 시너지가 없으면 숨는다. 누르면 하나면 그 시너지 상세(UINav.OpenSynergy(id)),
    /// 여럿이면 첫 시너지 종류 쪽 도감(UINav.OpenSynergy(null, kind) → C-19 SynergyOpenRequested → SynergyNavigator가 탭을 고른다).
    /// 새 시너지가 켜지면 칩이 1.12배로 톡 튀고 첫 아이콘에 반짝임이 지난다. 같은 시너지로 새 스테이지를 시작하면(Shimmer) 반짝임만.
    /// 기믹 칩을 누르면 한 줄 설명 토스트.
    /// </para>
    /// 배치는 바뀔 때만 다시 계산하고 (글자 폭은 그때 한 번 잰다), 매 프레임은 기믹 빛 맥박과 톡 · 반짝임만 움직인다. 할당 없음 (요약 글자는 N마다 한 번 만든다).
    /// </summary>
    public class SynergyHudView : MonoBehaviour
    {
        [SerializeField] RectTransform _row;
        [Header("스테이지 기믹 칩")]
        [SerializeField] RectTransform _modifierChip;
        [SerializeField] Image _modifierBack;
        [SerializeField] Image _modifierIcon;
        [SerializeField] Image _modifierGlow;
        [SerializeField] TMP_Text _modifierLabel;
        [SerializeField] Button _modifierButton;
        [Tooltip("true = 칩 바탕이 금테 아트(panel_night_9s)라 흰색에 기믹 색을 살짝만 섞는다")]
        [SerializeField] bool _modifierBackIsArt;

        [Header("시너지 요약 칩 (M43)")]
        [SerializeField] RectTransform _summary;
        [SerializeField] Button _summaryButton;
        [SerializeField] TMP_Text _summaryLabel;
        [Tooltip("아이콘 자리 (최대 3, 0번이 맨 위 — 빌더가 뒤에서부터 만든다)")]
        [SerializeField] RectTransform[] _summaryIcons;
        [SerializeField] Image[] _summaryIconImages;
        [Tooltip("아이콘 테 (시너지 색 원 40px — 안쪽 어두운 원판이 3px만 남긴다)")]
        [SerializeField] Image[] _summaryRims;
        [Tooltip("첫 아이콘 위 반짝임 (새 시너지가 켜질 때 · 같은 시너지로 다시 시작할 때)")]
        [SerializeField] Image _summaryGlint;
        [Tooltip("메달 아이콘(시너지·특성 그림, 자체 금테) 크기 / 어두운 원판 위에 올리는 역할 그림 · 대체 모양 크기")]
        [SerializeField] float _medallionIconSize = 36f;
        [SerializeField] float _socketIconSize = 26f;
        [SerializeField] SkillVfxLibrary _library;

        /// <summary>요약 칩 폭: 글자 시작 x 114 + 글자 폭 + 오른쪽 34 ('›' 26 + 여백 12 — §2.1.6), 200~280</summary>
        internal const float SummaryLabelX = 114f;
        internal const float SummaryRight = 34f;
        internal const float SummaryMinWidth = 200f;
        internal const float SummaryMaxWidth = 280f;
        /// <summary>기믹 칩과 요약 칩 사이</summary>
        internal const float ChipGap = 10f;
        /// <summary>기믹 칩 폭 = 왼쪽(아이콘) 56 + 글자 폭 + 오른쪽 18</summary>
        const float ModifierPadLeft = 56f;
        const float ModifierPadRight = 18f;
        const float PopLength = 0.4f;
        /// <summary>새 시너지가 켜질 때 칩 톡 (1 → 1.12 → 1, UIMotion.PunchDuration)</summary>
        const float PunchScale = 0.12f;
        const float GlintLength = 0.6f;

        /// <summary>'시너지 N' 글자 (N마다 한 번 만든다 — N은 금빛)</summary>
        static readonly string[] SummaryTexts = new string[32];
        /// <summary>N의 금빛 여는 태그 — UITheme.Gold 토큰에서 한 번 만든다 (§1.2 색 토큰: 글자에 색 값을 박지 않는다)</summary>
        internal static readonly string GoldTag = "<color=#" + ColorUtility.ToHtmlStringRGB(UITheme.Gold) + ">";

        readonly string[] _ids = new string[3];
        readonly HashSet<string> _activeIds = new();
        readonly List<string> _scratch = new(8);
        StageModifierType _modifier;
        float _modifierPop;
        float _punch = 1f;
        float _glint = -1f;
        int _count;
        string _singleId;
        SynergyKind _firstKind;
        bool _ready;

        void Awake() => Prepare();

        void Prepare()
        {
            if (_ready) return;
            _ready = true;
            if (_modifierButton != null) _modifierButton.onClick.AddListener(OnModifierClicked);
            if (_summaryButton != null) _summaryButton.onClick.AddListener(OnSummaryClicked);
            if (_modifierChip != null) _modifierChip.gameObject.SetActive(false);
            if (_summary != null) _summary.gameObject.SetActive(false);
            if (_summaryGlint != null) _summaryGlint.enabled = false;
        }

        /// <summary>이번 스테이지 기믹 (None이면 칩을 숨긴다)</summary>
        public void SetModifier(StageModifierType modifier)
        {
            Prepare();
            if (_modifierChip == null) return;
            bool changed = modifier != _modifier || !_modifierChip.gameObject.activeSelf;
            _modifier = modifier;
            bool show = modifier != StageModifierType.None;
            _modifierChip.gameObject.SetActive(show);
            if (!show)
            {
                Relayout();
                return;
            }
            var color = UITheme.StageModifierColor(modifier);
            string name = UITheme.StageModifierName(modifier);
            float labelWidth = 0f;
            if (_modifierLabel != null)
            {
                _modifierLabel.text = name;
                _modifierLabel.color = Color.Lerp(UITheme.Cream, color, 0.3f);
                labelWidth = _modifierLabel.preferredWidth;
            }
            if (_modifierIcon != null)
            {
                var icon = _library != null ? _library.StageIcon(modifier) : null;
                if (icon != null)
                {
                    _modifierIcon.sprite = icon;
                    _modifierIcon.color = Color.white;
                }
                else
                {
                    _modifierIcon.sprite = _library != null ? _library.Motif(FallbackMotif(modifier)) : null;
                    _modifierIcon.color = color;
                }
                _modifierIcon.enabled = _modifierIcon.sprite != null;
            }
            if (_modifierBack != null)
            {
                _modifierBack.color = _modifierBackIsArt ? Color.Lerp(Color.white, color, 0.15f)
                    : BattleEase.WithAlpha(Color.Lerp(UITheme.Panel, color, 0.22f), 0.92f);
            }
            if (_modifierGlow != null) _modifierGlow.color = BattleEase.WithAlpha(color, 0.5f);
            _modifierChip.sizeDelta = new Vector2(ModifierPadLeft + Mathf.Ceil(labelWidth) + ModifierPadRight, _modifierChip.sizeDelta.y);
            if (changed) _modifierPop = 1f;
            Relayout();
        }

        /// <summary>켜진 시너지 (ID, 단계) 목록 → 요약 칩 (바뀔 때만 다시 그린다)</summary>
        public void SetSynergies(IReadOnlyList<KeyValuePair<string, int>> synergies)
        {
            Prepare();
            var db = Game.IsReady && Game.Synergy != null ? Game.Synergy.Db : null;
            _scratch.Clear();
            SynergyDef first = null;
            int total = 0;
            for (int i = 0; synergies != null && i < synergies.Count; i++)
            {
                var def = db != null ? db.Get(synergies[i].Key) : null;
                if (def == null) continue;
                if (first == null) first = def;
                total++;
                _scratch.Add(def.id);
                if (total <= _ids.Length) Fill(total - 1, def);
            }

            // 새로 켜진 시너지가 있으면 톡 + 반짝 (처음 보이는 것도 새로 켜진 것)
            bool turnedOn = false;
            for (int i = 0; i < _scratch.Count; i++)
            {
                if (!_activeIds.Contains(_scratch[i]))
                {
                    turnedOn = true;
                    break;
                }
            }
            _activeIds.Clear();
            for (int i = 0; i < _scratch.Count; i++) _activeIds.Add(_scratch[i]);

            _count = total;
            _singleId = total == 1 && first != null ? first.id : null;
            _firstKind = first != null ? first.kind : default;
            int shown = Mathf.Min(total, _ids.Length);
            for (int i = 0; i < _ids.Length; i++)
            {
                if (i >= shown) _ids[i] = null;
                if (_summaryIcons != null && i < _summaryIcons.Length && _summaryIcons[i] != null) _summaryIcons[i].gameObject.SetActive(i < shown);
            }

            if (_summary != null)
            {
                bool show = total > 0;
                _summary.gameObject.SetActive(show);
                if (show)
                {
                    float labelWidth = 0f;
                    if (_summaryLabel != null)
                    {
                        _summaryLabel.text = SummaryText(total);
                        labelWidth = _summaryLabel.preferredWidth;
                    }
                    _summary.sizeDelta = new Vector2(SummaryWidth(labelWidth), _summary.sizeDelta.y);
                    if (turnedOn)
                    {
                        _punch = UIMotion.Reduced ? 1f : 0f;
                        _glint = 0f;
                    }
                }
            }
            Relayout();
        }

        /// <summary>요약 칩의 반짝임이 첫 아이콘을 지난다 (같은 시너지로 새 스테이지를 시작할 때)</summary>
        public void Shimmer()
        {
            if (_count > 0) _glint = 0f;
        }

        /// <summary>'시너지 N' 칩 폭 (114 + 글자 폭 + 34, 200~280)</summary>
        internal static float SummaryWidth(float labelWidth) =>
            Mathf.Clamp(SummaryLabelX + Mathf.Ceil(Mathf.Max(0f, labelWidth)) + SummaryRight, SummaryMinWidth, SummaryMaxWidth);

        /// <summary>'시너지 N' 글자 (N은 금빛 — 칩 글자 전체가 굵게). 32 이상은 그때그때 만든다</summary>
        internal static string SummaryText(int count)
        {
            if (count < 0) count = 0;
            if (count >= SummaryTexts.Length) return Format(count);
            return SummaryTexts[count] ??= Format(count);

            static string Format(int n) => "시너지 " + GoldTag + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + "</color>";
        }

        /// <summary>요약 칩 아이콘 하나 칠하기: 시너지·특성 그림은 자체 금테 메달이라 크게, 역할 그림·대체 모양은 어두운 원판 위에 작게. 테는 시너지 색</summary>
        void Fill(int index, SynergyDef def)
        {
            if (index < 0 || index >= _ids.Length) return;
            _ids[index] = def.id;
            var accent = SynergyBannerView.AccentOf(def);
            var icon = _library != null ? _library.SynergyIcon(def) : null;
            bool socket = icon == null || (def.kind == SynergyKind.Style && def.icon == null && icon == _library.StyleIcon(def.style));
            if (_summaryIconImages != null && index < _summaryIconImages.Length && _summaryIconImages[index] != null)
            {
                var image = _summaryIconImages[index];
                if (icon != null)
                {
                    image.sprite = icon;
                    image.color = Color.white;
                }
                else
                {
                    image.sprite = _library != null ? _library.Motif(def.kind == SynergyKind.Combo ? VfxMotif.Heart : VfxMotif.Star) : null;
                    image.color = accent;
                }
                image.enabled = image.sprite != null;
                float size = socket ? _socketIconSize : _medallionIconSize;
                image.rectTransform.sizeDelta = new Vector2(size, size);
            }
            if (_summaryRims != null && index < _summaryRims.Length && _summaryRims[index] != null) _summaryRims[index].color = BattleEase.WithAlpha(accent, 1f);
        }

        /// <summary>기믹 칩이 있으면 그 뒤 10px에 요약 칩, 없으면 줄 왼쪽 끝에서 시작</summary>
        void Relayout()
        {
            float x = 0f;
            if (_modifierChip != null && _modifierChip.gameObject.activeSelf)
            {
                _modifierChip.anchoredPosition = new Vector2(x, _modifierChip.anchoredPosition.y);
                x += _modifierChip.sizeDelta.x + ChipGap;
            }
            if (_summary != null) _summary.anchoredPosition = new Vector2(x, _summary.anchoredPosition.y);
        }

        void Update()
        {
            if (!_ready) return;
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            if (_modifierChip != null && _modifierChip.gameObject.activeSelf)
            {
                _modifierPop = Mathf.Max(0f, _modifierPop - dt * 1.6f);
                float scale = 1f + 0.18f * BattleEase.Punch(1f - _modifierPop);
                // 같은 값이면 쓰지 않는다 (perf PF-5 F9)
                if (_modifierChip.localScale.x != scale) _modifierChip.localScale = new Vector3(scale, scale, 1f);
                if (_modifierGlow != null)
                {
                    float pulse = UIMotion.Reduced ? 0.5f : 0.5f + 0.5f * Mathf.Sin(time * 2f);
                    _modifierGlow.color = BattleEase.WithAlpha(_modifierGlow.color, 0.25f + 0.2f * pulse + 0.4f * _modifierPop);
                }
            }

            if (_summary == null || !_summary.gameObject.activeSelf) return;
            // 새 시너지 톡: PunchDuration(0.22초) 동안 1 → 1.12 → 1
            if (_punch < 1f)
            {
                _punch = Mathf.Min(1f, _punch + dt / UIMotion.PunchDuration);
                float scale = _punch >= 1f ? 1f : 1f + PunchScale * Mathf.Sin(_punch * Mathf.PI);
                _summary.localScale = new Vector3(scale, scale, 1f);
            }
            if (_summaryGlint != null && _glint >= 0f)
            {
                _glint += dt;
                float k = _glint / GlintLength;
                bool on = k < 1f && !UIMotion.Reduced;
                if (_summaryGlint.enabled != on) _summaryGlint.enabled = on;
                if (on)
                {
                    _summaryGlint.color = BattleEase.WithAlpha(UITheme.Cream, 0.9f * Mathf.Sin(k * Mathf.PI));
                    _summaryGlint.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 180f);
                    float s = 0.6f + 0.6f * k;
                    _summaryGlint.rectTransform.localScale = new Vector3(s, s, 1f);
                }
                else _glint = -1f;
            }
        }

        void OnSummaryClicked()
        {
            if (_count <= 0) return;
            if (_count == 1 && !string.IsNullOrEmpty(_singleId)) UINav.OpenSynergy(_singleId);
            else UINav.OpenSynergy(null, _firstKind);
        }

        void OnModifierClicked()
        {
            if (_modifier == StageModifierType.None || UIManager.Instance == null) return;
            UIManager.Instance.ShowToast(UITheme.StageModifierName(_modifier) + " · " + SkillVfxLook.ModifierDescription(_modifier));
        }

        public static VfxMotif FallbackMotif(StageModifierType modifier) => modifier switch
        {
            StageModifierType.Rain => VfxMotif.Drop,
            StageModifierType.Blackout => VfxMotif.Keyhole,
            StageModifierType.FullMoon => VfxMotif.Glow,
            StageModifierType.Wind => VfxMotif.Swirl,
            StageModifierType.DustStorm => VfxMotif.Soft,
            StageModifierType.Festival => VfxMotif.Star,
            _ => VfxMotif.Sparkle,
        };
    }
}
