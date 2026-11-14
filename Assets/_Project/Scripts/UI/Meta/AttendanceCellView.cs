using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 출석부 한 칸 128×184(새싹) / 128×150(정규) (Q-03 · M69 · M59 · M61, UX-SPEC §2.11):
    ///   · 위 "3일" 22 굵게 · 가운데 보상 그림 판(Tile, 96 × (높이 − 76), 그림 64 / 52) · 판 아래 양 '×N' 22 굵게 Ink (맞춤 16 — 칸 안에 다 들어간다).
    ///     보상이 둘 이상이면 양 뒤에 '+' ('×15+')와 판 오른쪽 위 22px 라벤더 'i' — 눌러 모두 보기.
    ///   · 강조 칸(7일 등): 날짜 줄이 금 알약 '{날}일 확정!'(20 굵게 Ink)으로 바뀌고 날짜 글은 숨는다, 양 자리에는 보상 이름.
    ///   · 오늘 칸은 빛나고, 도장을 찍으면 단추 모양 도장이 판 위로 '꾹' 떨어지며(0.25초) 잉크가 번지고 별빛이 터진다.
    ///   · 칸 전체가 버튼 (누름 크기 PressScale은 루트) — 누르면 출석부(AttendancePopup)가 오늘이면 도장, 아니면 'N일차 보상' 카드.
    /// </summary>
    public class AttendanceCellView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] RectTransform _visual;
        [SerializeField] Image _background;
        [SerializeField] Image _frame;
        [SerializeField] TMP_Text _dayText;
        [SerializeField] MetaRewardChip _reward;
        [Tooltip("강조 칸 날짜 줄 금 알약 ('7일 확정!')")]
        [SerializeField] GameObject _ribbon;
        [SerializeField] TMP_Text _ribbonText;
        [SerializeField] GameObject _todayGlow;
        [SerializeField] RectTransform _stamp;
        [SerializeField] Image _ink;
        [SerializeField] CanvasGroup _contentGroup;
        [Tooltip("보상이 둘 이상인 칸의 'i' 표시 (없어도 됨)")]
        [SerializeField] GameObject _moreMark;
        [Header("칸 그림 (ART-UPGRADE §4.3 #4 — 없어도 됨)")]
        [Tooltip("평소 칸 그림 (UiArt.AttendanceCell9s)")]
        [SerializeField] Sprite _cellSprite;
        [Tooltip("강조 칸 금 머리띠 그림 (UiArt.AttendanceCellGold9s — 있을 때만, 없으면 테두리를 금색으로 칠한다)")]
        [SerializeField] Sprite _goldCellSprite;

        /// <summary>도장 찍힌 칸: 양피지에 갈색을 옅게 섞는다 (그림을 어둡게 덮지 않음)</summary>
        static readonly Color StampedTint = new(0.62f, 0.52f, 0.42f, 1f);
        const float StampedAmount = 0.35f;

        Action<AttendanceCellView> _onClick;
        Coroutine _anim;
        CanvasGroup _stampGroup;
        bool _basesRead;
        /// <summary>빌더가 정한 바탕 색 (양피지 그림이면 흰색)</summary>
        Color _cellBase = Color.white;
        /// <summary>빌더가 정한 평소 테두리 색 (바느질 테두리 그림이면 투명 — 강조·오늘 칸에만 물든다)</summary>
        Color _frameBase = Color.white;
        /// <summary>빌더가 넣은 바탕 그림 (강조 칸 그림을 바꿔 끼운 뒤 되돌릴 때)</summary>
        Sprite _baseSprite;

        public int Day { get; private set; }

        /// <summary>이 칸의 보상 정의 (Bind 때 받은 것)</summary>
        public AttendanceDayDef Def { get; private set; }

        public RectTransform Visual => _visual;

        void Awake()
        {
            _button.onClick.AddListener(() => _onClick?.Invoke(this));
            _stampGroup = _stamp.GetComponent<CanvasGroup>();
            ReadBases();
        }

        void ReadBases()
        {
            if (_basesRead) return;
            _basesRead = true;
            _cellBase = _background.color;
            _frameBase = _frame.color;
            _baseSprite = _background.sprite;
        }

        Color CellDoneColor => MetaUi.Alpha(Color.Lerp(_cellBase, StampedTint, StampedAmount), _cellBase.a);

        public void Bind(int day, AttendanceDayDef def, bool stamped, bool today, Action<AttendanceCellView> onClick)
        {
            Day = day;
            Def = def;
            _onClick = onClick;
            bool highlight = def != null && def.highlight;
            // 단추 · N분치는 실제 지급과 같게 단추 · 실타래 두 줄로 풀어 첫 줄 + '+' (MetaUi.FirstPreview)
            var reward = MetaUi.FirstPreview(def != null ? def.rewards : null, out int rewardCount);
            if (reward != null)
            {
                _reward.Set(reward);
                // 강조 칸은 양 자리에 보상 이름, 보상이 여럿이면 '+'
                if (highlight) _reward.SetAmountText(MetaUi.NameOf(reward));
                else if (rewardCount > 1) _reward.SetAmountText(MetaUi.ShortAmount(reward) + "+");
            }
            else
            {
                _reward.gameObject.SetActive(false);
            }
            if (_moreMark != null) _moreMark.SetActive(rewardCount > 1);

            // 강조 칸: 날짜 줄이 금 알약 '{날}일 확정!'으로 바뀐다
            _ribbon.SetActive(highlight);
            _dayText.gameObject.SetActive(!highlight);
            if (highlight) _ribbonText.text = $"{day}일 확정!";
            else _dayText.text = $"{day}일";

            ReadBases();
            // 강조 칸: 금 머리띠 그림이 있으면 바꿔 끼우고 테두리는 평소대로, 없으면 지금처럼 테두리를 금색으로
            bool goldArt = highlight && _goldCellSprite != null;
            var sprite = goldArt ? _goldCellSprite : _cellSprite != null ? _cellSprite : _baseSprite;
            if (sprite != null && _background.sprite != sprite) _background.sprite = sprite;
            _background.color = stamped ? CellDoneColor : _cellBase;
            _frame.color = highlight && !goldArt ? UITheme.Gold : today ? UITheme.Lamp : _frameBase;
            _todayGlow.SetActive(today);
            if (_anim == null)
            {
                _stamp.gameObject.SetActive(stamped);
                _ink.enabled = false;
                _contentGroup.alpha = stamped ? 0.55f : 1f;
            }
        }

        /// <summary>도장 '꾹': 0.25초 낙하 → 잉크 번짐 + 별빛 (sparkles는 팝업 공용)</summary>
        public void PlayStamp(MetaSparkleBurst sparkles)
        {
            if (!isActiveAndEnabled) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Stamp(sparkles));
        }

        IEnumerator Stamp(MetaSparkleBurst sparkles)
        {
            _todayGlow.SetActive(false);
            _stamp.gameObject.SetActive(true);
            const float drop = 0.25f;
            for (float t = 0f; t < drop; t += Time.unscaledDeltaTime)
            {
                float k = MetaUi.EaseInCubic(t / drop);
                _stamp.localScale = Vector3.one * Mathf.Lerp(2.1f, 1f, k);
                _stamp.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-40f, -14f, k));
                if (_stampGroup != null) _stampGroup.alpha = Mathf.Clamp01(k * 2.2f);
                yield return null;
            }
            _stamp.localScale = Vector3.one;
            _stamp.localRotation = Quaternion.Euler(0f, 0f, -14f);
            if (_stampGroup != null) _stampGroup.alpha = 1f;
            if (sparkles != null) sparkles.BurstAt(_visual, 10, UITheme.Gold, 0.8f);

            // 잉크 번짐 + 칸이 꾹 눌렸다 돌아옴
            _ink.enabled = true;
            const float spread = 0.4f;
            for (float t = 0f; t < spread; t += Time.unscaledDeltaTime)
            {
                float k = t / spread;
                _ink.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.6f, MetaUi.EaseOutCubic(k));
                _ink.color = MetaUi.Alpha(UITheme.DustyPink, 0.7f * (1f - k));
                _visual.localScale = Vector3.one * (1f - 0.08f * MetaUi.Punch(Mathf.Min(1f, k * 2f)));
                _contentGroup.alpha = Mathf.Lerp(1f, 0.55f, k);
                yield return null;
            }
            _ink.enabled = false;
            _visual.localScale = Vector3.one;
            _background.color = CellDoneColor;
            _contentGroup.alpha = 0.55f;
            _anim = null;
        }

        void OnDisable()
        {
            if (_anim == null) return;
            StopCoroutine(_anim);
            _anim = null;
            _ink.enabled = false;
            _visual.localScale = Vector3.one;
            _stamp.localScale = Vector3.one;
            _stamp.localRotation = Quaternion.Euler(0f, 0f, -14f);
            if (_stampGroup != null) _stampGroup.alpha = 1f;
        }
    }
}
