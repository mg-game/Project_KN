using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 스테이지 시작 때 전장 아래쪽에 잠깐 뜨는 "시너지 발동!" 띠.
    /// 켜진 특성/역할/조합 시너지를 아이콘 칩으로 하나씩 톡톡 띄우고 (특성·역할은 단계 인원 배지), 잠시 뒤 스르르 내려가며 사라진다.
    /// 칩 배치는 보여줄 때 한 번만 계산한다.
    /// </summary>
    public class SynergyBannerView : MonoBehaviour
    {
        [SerializeField] RectTransform _root;
        [SerializeField] CanvasGroup _group;
        [SerializeField] TMP_Text _title;
        [SerializeField] Image _glow;
        [SerializeField] Image _sweep;
        [SerializeField] RectTransform[] _chips;
        [SerializeField] CanvasGroup[] _chipGroups;
        [SerializeField] Image[] _chipBacks;
        [SerializeField] Image[] _chipIcons;
        [SerializeField] Image[] _chipIconGlows;
        [SerializeField] TMP_Text[] _chipLabels;
        [SerializeField] GameObject[] _chipBadges;
        [SerializeField] TMP_Text[] _chipBadgeTexts;
        [SerializeField] Image[] _chipEdges;
        [Tooltip("true = 칩 바탕이 아트(ui_pill_night_9s)라 흰색에 시너지 색을 살짝만 섞는다 (어두운 Panel 색을 곱하지 않는다)")]
        [SerializeField] bool _chipBackIsArt;
        [SerializeField] TMP_Text _more;
        [SerializeField] SkillVfxLibrary _library;
        [SerializeField] float _hold = 2.6f;
        [SerializeField] float _maxRowWidth = 1010f;
        [SerializeField] float _charWidth = 21f;
        [SerializeField] float _spacing = 10f;

        const float InTime = 0.3f;
        const float OutTime = 0.4f;
        const float ChipStagger = 0.08f;

        float _age = -1f;
        float _delay;
        int _shown;
        float _baseY;
        bool _ready;

        void Awake() => Prepare();

        void Prepare()
        {
            if (_ready) return;
            _ready = true;
            if (_root != null)
            {
                _baseY = _root.anchoredPosition.y;
                _root.gameObject.SetActive(false);
            }
        }

        /// <summary>켜진 시너지 (ID, 단계 인덱스) 목록을 delay초 뒤에 띄운다. 비어 있으면 아무것도 하지 않는다</summary>
        public void Show(IReadOnlyList<KeyValuePair<string, int>> synergies, float delay = 0f)
        {
            Prepare();
            if (_root == null || _chips == null || synergies == null || synergies.Count == 0) return;
            var db = Game.IsReady && Game.Synergy != null ? Game.Synergy.Db : null;

            float x = 0f;
            int shown = 0;
            int total = 0;
            for (int i = 0; i < synergies.Count; i++)
            {
                var def = db != null ? db.Get(synergies[i].Key) : null;
                if (def == null) continue;
                total++;
                if (shown >= _chips.Length) continue;
                float width = ChipWidth(def);
                if (shown > 0 && x + width > _maxRowWidth - 80f) continue;
                Fill(shown, def, synergies[i].Value);
                SetChipX(shown, x, width);
                x += width + _spacing;
                shown++;
            }
            if (shown == 0) return;
            for (int i = shown; i < _chips.Length; i++)
            {
                if (_chips[i] != null) _chips[i].gameObject.SetActive(false);
            }

            int hidden = total - shown;
            if (_more != null)
            {
                _more.gameObject.SetActive(hidden > 0);
                if (hidden > 0)
                {
                    _more.SetText("+{0}", hidden);
                    _more.rectTransform.anchoredPosition = new Vector2(x + 24f, _more.rectTransform.anchoredPosition.y);
                    x += 56f;
                }
            }

            // 가운데 정렬: 칩 묶음 전체를 왼쪽으로 절반만큼 민다
            float shift = -(x - _spacing) * 0.5f;
            for (int i = 0; i < shown; i++) _chips[i].anchoredPosition += Vector2.right * shift;
            if (_more != null && hidden > 0) _more.rectTransform.anchoredPosition += Vector2.right * shift;
            if (_title != null) _title.SetText(total > 1 ? "시너지 {0}개 발동!" : "시너지 발동!", total);

            _shown = shown;
            _age = 0f;
            _delay = Mathf.Max(0f, delay);
            _root.gameObject.SetActive(true);
            Apply();
        }

        public void Hide()
        {
            _age = -1f;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        float ChipWidth(SynergyDef def)
        {
            int length = string.IsNullOrEmpty(def.name) ? 2 : def.name.Length;
            bool badge = def.kind != SynergyKind.Combo;
            return 14f + 44f + 10f + length * _charWidth + (badge ? 38f : 0f) + 16f;
        }

        void Fill(int index, SynergyDef def, int tier)
        {
            var accent = AccentOf(def);
            _chips[index].gameObject.SetActive(true);
            var icon = _library != null ? _library.SynergyIcon(def) : null;
            if (index < _chipIcons.Length && _chipIcons[index] != null)
            {
                var image = _chipIcons[index];
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
            }
            if (index < _chipIconGlows.Length && _chipIconGlows[index] != null) _chipIconGlows[index].color = BattleEase.WithAlpha(accent, 0.55f);
            if (index < _chipLabels.Length && _chipLabels[index] != null)
            {
                _chipLabels[index].text = def.name;
                _chipLabels[index].color = Color.Lerp(UITheme.Cream, accent, 0.25f);
            }
            if (index < _chipEdges.Length && _chipEdges[index] != null) _chipEdges[index].color = BattleEase.WithAlpha(accent, 0.95f);
            if (index < _chipBacks.Length && _chipBacks[index] != null)
            {
                _chipBacks[index].color = _chipBackIsArt ? Color.Lerp(Color.white, accent, 0.15f)
                    : BattleEase.WithAlpha(Color.Lerp(UITheme.Panel, accent, 0.18f), 0.95f);
            }

            bool badge = def.kind != SynergyKind.Combo && def.tiers != null && tier >= 0 && tier < def.tiers.Count;
            if (index < _chipBadges.Length && _chipBadges[index] != null) _chipBadges[index].SetActive(badge);
            if (badge && index < _chipBadgeTexts.Length && _chipBadgeTexts[index] != null) _chipBadgeTexts[index].SetText("{0}", def.tiers[tier].count);
        }

        void SetChipX(int index, float x, float width)
        {
            var chip = _chips[index];
            chip.sizeDelta = new Vector2(width, chip.sizeDelta.y);
            chip.anchoredPosition = new Vector2(x + width * 0.5f, chip.anchoredPosition.y);
            if (index < _chipLabels.Length && _chipLabels[index] != null)
            {
                bool badge = index < _chipBadges.Length && _chipBadges[index] != null && _chipBadges[index].activeSelf;
                var label = _chipLabels[index].rectTransform;
                label.sizeDelta = new Vector2(width - 68f - (badge ? 38f : 0f) - 12f, label.sizeDelta.y);
            }
        }

        public static Color AccentOf(SynergyDef def) => def.kind switch
        {
            SynergyKind.Trait => UITheme.TraitColor(def.trait),
            SynergyKind.Style => UITheme.StyleColor(def.style),
            _ => UITheme.Gold,
        };

        void Update()
        {
            if (_age < 0f) return;
            if (_delay > 0f)
            {
                _delay -= Time.unscaledDeltaTime;
                if (_group != null) _group.alpha = 0f;
                return;
            }
            _age += Time.unscaledDeltaTime;
            if (_age >= InTime + _hold + OutTime)
            {
                Hide();
                return;
            }
            Apply();
        }

        void Apply()
        {
            if (_delay > 0f)
            {
                if (_group != null) _group.alpha = 0f;
                return;
            }
            float alpha;
            float offset;
            if (_age < InTime)
            {
                float k = _age / InTime;
                alpha = BattleEase.OutCubic(k);
                offset = -26f * (1f - BattleEase.OutCubic(k));
            }
            else if (_age < InTime + _hold)
            {
                alpha = 1f;
                offset = 0f;
            }
            else
            {
                float k = (_age - InTime - _hold) / OutTime;
                alpha = 1f - k;
                offset = -22f * BattleEase.InCubic(k);
            }
            if (_group != null) _group.alpha = alpha;
            _root.anchoredPosition = new Vector2(_root.anchoredPosition.x, _baseY + offset);

            float time = Time.unscaledTime;
            if (_glow != null) _glow.color = BattleEase.WithAlpha(UITheme.Lavender, (0.35f + 0.12f * Mathf.Sin(time * 3f)) * alpha);
            if (_sweep != null)
            {
                // 빛 한 줄기가 칩 위를 한 번 훑고 지나간다
                float k = Mathf.Clamp01((_age - 0.35f) / 0.9f);
                _sweep.enabled = k > 0f && k < 1f;
                if (_sweep.enabled)
                {
                    float half = _root.rect.width * 0.5f;
                    _sweep.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, BattleEase.InOutSine(k)), _sweep.rectTransform.anchoredPosition.y);
                    _sweep.color = BattleEase.WithAlpha(Color.white, 0.35f * Mathf.Sin(k * Mathf.PI));
                }
            }

            for (int i = 0; i < _shown; i++)
            {
                float local = Mathf.Clamp01((_age - 0.1f - i * ChipStagger) / 0.3f);
                float scale = Mathf.LerpUnclamped(0.4f, 1f, BattleEase.OutBack(local, 2.6f));
                _chips[i].localScale = new Vector3(scale, scale, 1f);
                if (i < _chipGroups.Length && _chipGroups[i] != null) _chipGroups[i].alpha = Mathf.Clamp01(local * 2f);
                if (i < _chipIconGlows.Length && _chipIconGlows[i] != null)
                {
                    float pulse = 0.9f + 0.15f * Mathf.Sin(time * 4f + i);
                    _chipIconGlows[i].rectTransform.localScale = new Vector3(pulse, pulse, 1f);
                }
            }
        }
    }
}
