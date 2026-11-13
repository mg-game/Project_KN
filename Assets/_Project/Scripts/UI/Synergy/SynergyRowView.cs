using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 편성 화면 시너지 칸의 한 줄 (376x52, M44): 아이콘(둘레 실 고리 = 종류 색) · 이름 21 · "3/4" 20 · 단계 점 12 · ×1.5 · 발동 배지 (18 굵게 알약).
    /// 켜진 시너지는 강조색 테두리, 한 명만 더 있으면 켜지는 시너지는 흐리게.
    /// 드래그 미리보기에서는 새로 켜지거나 강해지는 시너지가 금빛으로 빛나고("발동"/"강화"), 꺼지는 시너지는 붉게("해제").
    /// 누르면 시너지 상세를 연다. 계보 공명(지킴이 + 잔향이 같은 계보) 줄도 같은 모양으로 보여준다.
    /// </summary>
    public class SynergyRowView : MonoBehaviour
    {
        const float InactiveAlpha = 0.62f;

        [SerializeField] Button _button;
        [SerializeField] CanvasGroup _group;
        [SerializeField] Image _bg;
        [SerializeField] Image _frame;
        [Tooltip("미리보기 발광 (MenuGlowPulse)")]
        [SerializeField] Image _glow;
        [SerializeField] Image _iconRing;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _iconFallback;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] Image _kindChip;
        [SerializeField] TMP_Text _kindText;
        [SerializeField] TMP_Text _progressText;
        [Tooltip("단계 점 (최대 4)")]
        [SerializeField] Image[] _pips;
        [Tooltip("지금 스테이지 기믹에서 강해지는 시너지 (×1.5)")]
        [SerializeField] GameObject _favoredTag;
        [SerializeField] TMP_Text _favoredText;
        [SerializeField] Image _deltaBadge;
        [SerializeField] TMP_Text _deltaText;
        [Tooltip("아이콘 둘레 실 고리 — 종류 색 (특성 라벤더 · 조합 더스티 핑크 · 역할 소프트 블루 · 공명 금, M44). 종류 칩 대신")]
        [SerializeField] Image _kindRing;

        MenuGlowPulse _glowPulse;
        Coroutine _pop;
        Coroutine _celebrate;
        string _synergyId;
        SynergyInfo _resonance;

        void Awake()
        {
            if (_glow != null) _glowPulse = _glow.GetComponent<MenuGlowPulse>();
            _button.onClick.AddListener(OnClick);
        }

        void OnDisable()
        {
            _pop = null;
            _celebrate = null;
            transform.localScale = Vector3.one;
            if (_deltaBadge != null) _deltaBadge.transform.localScale = Vector3.one;
        }

        /// <param name="before">미리보기 전 상태 (변화 표시용, 미리보기가 아니면 null)</param>
        /// <param name="related">끌고 있는/고른 친구가 이 시너지에 들어가는지</param>
        public void Set(SynergyStatus status, SynergyStatus before, SynergyDelta delta, bool related, StageModifierType stage)
        {
            var def = status.Def;
            _synergyId = def.id;
            _resonance = null;
            StopCelebrate();

            var accent = SynergyUI.AccentColor(def);
            bool active = status.IsActive;
            bool gain = SynergyUI.IsGain(delta);
            bool loss = SynergyUI.IsLoss(delta);

            _group.alpha = active || delta != SynergyDelta.None || related ? 1f : InactiveAlpha;
            // 바탕은 보라 바느질 판 그림 — 어두운 색을 곱하지 않고 강조색을 살짝 섞거나 알파만 낮춘다
            _bg.color = active ? Color.Lerp(Color.white, accent, 0.3f) : WithAlpha(Color.white, 0.72f);
            _frame.color = gain ? UITheme.Lamp
                : loss ? UITheme.Negative
                : related ? WithAlpha(UITheme.Lamp, 0.85f)
                : active ? WithAlpha(accent, 0.9f)
                : WithAlpha(accent, 0.18f);
            _iconRing.color = active ? Color.white : WithAlpha(Color.white, 0.6f);
            SynergyIcons.ApplyInRing(_icon, _iconFallback, def, 6f, 2f);

            _nameText.text = SynergyUI.DisplayName(def);
            _nameText.color = active ? UITheme.Cream : UITheme.SubText;
            // 종류는 아이콘 둘레 고리 색으로 (줄이 좁아 종류 칩은 숨긴다 — 연결은 그대로 둔다)
            _kindChip.gameObject.SetActive(false);
            _kindText.text = UITheme.SynergyKindName(def.kind);
            SetKindRing(SynergyUI.KindColor(def.kind), active);

            _progressText.text = SynergyUI.ProgressLabel(status, active || delta == SynergyDelta.CountUp);
            _progressText.color = loss ? UITheme.Negative : active ? UITheme.Text : UITheme.SubText;

            SetPips(def, status.TierIndex, before != null ? before.TierIndex : status.TierIndex, accent);

            bool favored = active && SynergyUI.IsFavoredNow(def, stage);
            _favoredTag.SetActive(favored);
            if (favored) _favoredText.text = SynergyUI.Multiplier(def);

            ShowDelta(delta);
        }

        /// <summary>계보 공명 줄 (지킴이 + 잔향이로 같은 계보를 함께 편성)</summary>
        /// <param name="delta">드래그 미리보기: 놓으면 새로 생기는 공명 = Activated, 깨지는 공명 = Lost (그 밖엔 None)</param>
        /// <param name="related">끌고 있는/고른 친구가 이 계보인지</param>
        public void SetResonance(SynergyInfo info, SynergyDelta delta = SynergyDelta.None, bool related = false)
        {
            _synergyId = null;
            _resonance = info;
            StopCelebrate();
            var lineage = info.Lineage;
            var color = SynergyUI.ResonanceColor;
            bool gain = SynergyUI.IsGain(delta);
            bool loss = SynergyUI.IsLoss(delta);

            _group.alpha = 1f;
            _bg.color = loss ? WithAlpha(Color.white, 0.72f) : Color.Lerp(Color.white, color, gain ? 0.3f : 0.2f);
            _frame.color = gain ? UITheme.Lamp
                : loss ? UITheme.Negative
                : related ? WithAlpha(UITheme.Lamp, 0.85f)
                : WithAlpha(color, 0.75f);
            // 초상화 뒤는 밝은 둥근 받침 (계보 색을 옅게)
            if (SynergyIcons.Plate != null) _iconRing.sprite = SynergyIcons.Plate;
            _iconRing.enabled = true;
            _iconRing.color = Color.Lerp(Color.white, lineage != null ? lineage.mainColor : color, 0.35f);
            var portrait = lineage != null ? Game.Characters.GetPortrait(lineage.baseFormId) : null;
            SynergyIcons.Apply(_icon, _iconFallback, portrait, lineage != null ? lineage.subColor : color,
                lineage != null && !string.IsNullOrEmpty(lineage.name) ? lineage.name.Substring(0, 1) : "?");
            _icon.rectTransform.offsetMin = new Vector2(3f, 3f);
            _icon.rectTransform.offsetMax = new Vector2(-3f, -3f);

            _nameText.text = lineage != null ? $"{lineage.name} 계보 공명" : info.Label;
            _nameText.color = loss ? UITheme.SubText : UITheme.Cream;
            _kindChip.gameObject.SetActive(false);
            _kindText.text = "공명";
            SetKindRing(color, !loss);
            // 깨지는 공명은 잃는 공격력을 붉게 (진행 텍스트 자체가 붉은색)
            _progressText.text = loss
                ? $"Lv.{info.ResonanceLevel}  공격 -{info.AttackBonusPercent:0.#}%"
                : $"Lv.{info.ResonanceLevel}  <color={UITheme.ColorTag(UITheme.Gold)}>공격 +{info.AttackBonusPercent:0.#}%</color>";
            _progressText.color = loss ? UITheme.Negative : UITheme.Text;

            for (int i = 0; i < _pips.Length; i++) _pips[i].gameObject.SetActive(false);
            _favoredTag.SetActive(false);
            ShowDelta(gain ? SynergyDelta.Activated : loss ? SynergyDelta.Lost : SynergyDelta.None);
        }

        /// <summary>아이콘 둘레 실 고리를 종류 색으로 (꺼진 줄은 옅게)</summary>
        void SetKindRing(Color kind, bool strong)
        {
            if (_kindRing == null) return;
            _kindRing.color = WithAlpha(kind, strong ? 0.95f : 0.5f);
        }

        void SetPips(SynergyDef def, int tier, int beforeTier, Color accent)
        {
            // 조합은 단계가 하나뿐이라 점 대신 "2/3" 진행만 보여준다
            int count = def.kind == SynergyKind.Combo ? 0 : Mathf.Min(SynergyUI.TierCount(def), _pips.Length);
            for (int i = 0; i < _pips.Length; i++)
            {
                var pip = _pips[i];
                bool shown = i < count;
                pip.gameObject.SetActive(shown);
                if (!shown) continue;
                bool reached = i <= tier;
                bool fresh = reached && i > beforeTier;
                bool lost = !reached && i <= beforeTier;
                pip.color = fresh ? UITheme.Lamp : reached ? accent : lost ? WithAlpha(UITheme.Negative, 0.85f) : WithAlpha(UITheme.Text, 0.16f);
                pip.rectTransform.localScale = Vector3.one * (fresh ? 1.25f : 1f);
            }
        }

        void ShowDelta(SynergyDelta delta)
        {
            string label = delta switch
            {
                SynergyDelta.Activated => "발동",
                SynergyDelta.Upgraded => "강화",
                SynergyDelta.Downgraded => "약화",
                SynergyDelta.Lost => "해제",
                _ => null,
            };
            bool loss = SynergyUI.IsLoss(delta);
            bool glow = label != null;

            if (_glow != null)
            {
                _glow.gameObject.SetActive(glow);
                var color = loss ? UITheme.Negative : UITheme.Lamp;
                if (_glowPulse != null) _glowPulse.SetColor(color);
                else _glow.color = color;
            }

            bool wasShown = _deltaBadge.gameObject.activeSelf && _deltaText.text == label;
            _deltaBadge.gameObject.SetActive(label != null);
            if (label == null) return;
            _deltaBadge.color = loss ? UITheme.Negative : UITheme.Gold;
            _deltaText.text = label;
            _deltaText.color = loss ? UITheme.Text : UITheme.Ink;
            if (!wasShown) MenuFx.Restart(this, ref _pop, PopBadge());
        }

        /// <summary>배치 직후 새로 켜진 시너지: 톡 튀고 잠깐 "발동" 배지 + 금빛</summary>
        public void Celebrate() => MenuFx.Restart(this, ref _celebrate, CelebrateRoutine());

        IEnumerator CelebrateRoutine()
        {
            ShowDelta(SynergyDelta.Activated);
            yield return MenuFx.Punch(transform, 0.08f, 0.32f);
            yield return new WaitForSecondsRealtime(1.5f);
            _celebrate = null;
            ShowDelta(SynergyDelta.None);
        }

        void StopCelebrate()
        {
            if (_celebrate == null) return;
            StopCoroutine(_celebrate);
            _celebrate = null;
            transform.localScale = Vector3.one;
        }

        IEnumerator PopBadge()
        {
            yield return MenuFx.PopIn(_deltaBadge.transform, 0.28f);
            _pop = null;
        }

        void OnClick()
        {
            if (!string.IsNullOrEmpty(_synergyId))
            {
                UINav.OpenSynergy(_synergyId);
                return;
            }
            if (_resonance?.Lineage == null) return;
            var name = _resonance.Lineage.name;
            UIManager.Instance.ShowToast(
                $"{MenuText.Object(name)} 지킴이와 잔향이로 함께 편성해 공격력 +{_resonance.AttackBonusPercent:0.#}%! 공명 레벨을 올리면 더 강해져요.");
        }

        static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
