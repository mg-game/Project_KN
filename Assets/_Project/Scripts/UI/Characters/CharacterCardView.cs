using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 친구들 탭의 계보 카드. 보유 계보는 초상화·레벨·진화 수, 미보유 계보는 검은 실루엣으로 보여준다.
    /// 왼쪽 위에 계보 특성 칩(천·밤 등)을 달아 특성 시너지를 고르기 쉽게 한다.
    /// 아래 줄 (M45): 보유 = 왼쪽 'Lv.N' (Cream 굵게) + 오른쪽 Lavender 알약 '진화 k/n' (Ink), 미보유 = 가운데 종족 이름 (알약 없음).
    /// 미보유 카드 (M40B): 카드 알파 1 (흐리게 하지 않는다), 특성 칩 0.85, 별 0.45, '미보유' 칩 그대로 — 종족 글은 SubText 그대로(대비 4.5 이상).
    /// 새 직렬화 필드(_evolutionPill · _evolutionText)는 비어 있어도 된다 (다시 빌드하기 전 씬 — 예전처럼 한 줄 'Lv.1 · 진화 1/11').
    /// 전투력 (PRG-01 §7.1 #4, 보유만): 초상화 아래 왼쪽 칼 아이콘 20 + '{FormatCompact(계보 전투력)}' 20 굵게 Cream (밤 카드). 0이면 숨긴다.
    /// 호감도 Lv5 이상 (CR-10): 하트 바느질 카드 틀(UiArt.AffinityFrame9s — 전용 아트가 없으면 빌더가 DustyPink로 칠한 틴트 틀)을 켠다.
    /// 빨간 점 = CharacterUI.HasAlert (진화 · 공명 · 의상 · 받을 호감도 단계 — 친구들 탭 점의 계보 잎 넷).
    /// 새 직렬화 필드(_powerRow · _powerText · _affinityFrame)는 비어 있어도 된다.
    /// </summary>
    public class CharacterCardView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] CanvasGroup _group;
        [SerializeField] FriendPortrait _portrait;
        [SerializeField] Image _gradeGlow;
        [SerializeField] Image _gradeLine;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _starsText;
        [SerializeField] TMP_Text _infoText;
        [Tooltip("'진화 k/n' Lavender 알약 (보유 계보만)")]
        [SerializeField] GameObject _evolutionPill;
        [SerializeField] TMP_Text _evolutionText;
        [SerializeField] GameObject _lockedChip;
        [SerializeField] GameObject _alertDot;
        [SerializeField] CharacterChip[] _traitChips;
        [Tooltip("전투력 줄 (칼 아이콘 + 숫자, 보유 계보만)")]
        [SerializeField] GameObject _powerRow;
        [SerializeField] TMP_Text _powerText;
        [Tooltip("호감도 Lv5 하트 바느질 틀 (꺼 둠)")]
        [SerializeField] GameObject _affinityFrame;

        /// <summary>카드에 하트 틀을 다는 호감도 단계 (CR-10 §6.3 — Lv5)</summary>
        public const int AffinityFrameLevel = 5;

        LineageDef _lineage;
        Action<LineageDef> _onClick;
        /// <summary>마지막으로 쓴 아래 줄 · 전투력 (같으면 문자열을 다시 만들지 않는다 — perf PF-6 H6, 카드 40장 × 1초마다)</summary>
        LineageDef _shownLineage;
        int _shownLevel = -1;
        int _shownUnlocked = -1;
        int _shownForms = -1;
        long _shownPower = -1;

        void Awake() => _button.onClick.AddListener(OnClick);

        public void Set(LineageDef lineage, Action<LineageDef> onClick)
        {
            _lineage = lineage;
            _onClick = onClick;

            bool owned = CharacterUI.IsOwned(lineage);
            var baseForm = CharacterUI.BaseForm(lineage);
            var gradeColor = UITheme.GradeColor(lineage.grade);

            _portrait.Set(CharacterUI.Portrait(baseForm), lineage, lineage.name, owned ? PortraitTone.Normal : PortraitTone.Silhouette);
            _gradeGlow.color = CharacterUI.WithAlpha(gradeColor, owned ? 0.5f : 0.1f);
            _gradeLine.color = owned ? gradeColor : CharacterUI.WithAlpha(gradeColor, 0.35f);

            _nameText.text = owned ? CharacterUI.DisplayName(baseForm) : lineage.name;
            _nameText.color = owned ? UITheme.Cream : UITheme.SubText;
            _starsText.text = UITheme.Stars(lineage.grade);
            _starsText.color = owned ? gradeColor : CharacterUI.WithAlpha(gradeColor, 0.45f);

            bool split = _evolutionPill != null && _evolutionText != null;
            if (owned)
            {
                int unlocked = CharacterUI.CountUnlockedForms(lineage);
                int level = Game.Characters.GetLevel(lineage.id);
                int forms = lineage.forms.Count;
                bool same = _shownLineage == lineage && _shownLevel == level && _shownUnlocked == unlocked && _shownForms == forms;
                if (split)
                {
                    if (!same)
                    {
                        _infoText.text = $"Lv.{level}";
                        _evolutionText.text = $"진화 {unlocked}/{forms}";
                    }
                    SetInfoStyle(TextAlignmentOptions.MidlineLeft, UITheme.Cream);
                }
                else
                {
                    if (!same) _infoText.text = $"Lv.{level} · 진화 {unlocked}/{forms}";
                    _infoText.color = UITheme.SubText;
                }
                _shownLineage = lineage;
                _shownLevel = level;
                _shownUnlocked = unlocked;
                _shownForms = forms;
            }
            else
            {
                _shownLineage = null;
                _infoText.text = lineage.species;
                if (split) SetInfoStyle(TextAlignmentOptions.Center, UITheme.SubText);
                else _infoText.color = CharacterUI.WithAlpha(UITheme.SubText, 0.7f);
            }
            if (split && _evolutionPill.activeSelf != owned) _evolutionPill.SetActive(owned);

            CharacterUI.SetTraitChips(_traitChips, lineage, true, owned ? 1f : UnownedChipAlpha);
            if (_powerRow != null)
            {
                long power = owned && Game.Power != null ? Game.Power.GetLineagePower(lineage.id) : 0L;
                bool showPower = power > 0;
                if (_powerRow.activeSelf != showPower) _powerRow.SetActive(showPower);
                if (showPower && _powerText != null && power != _shownPower) _powerText.text = NumberFormatter.FormatCompact(power);
                _shownPower = showPower ? power : -1;
            }
            if (_affinityFrame != null)
            {
                bool frame = owned && CharacterUI.ShowsAffinityFrame(lineage);
                if (_affinityFrame.activeSelf != frame) _affinityFrame.SetActive(frame);
            }
            _lockedChip.SetActive(!owned);
            _alertDot.SetActive(owned && CharacterUI.HasAlert(lineage));
            _group.alpha = 1f;
        }

        /// <summary>미보유 카드의 특성 칩 알파 (M40B: 0.6 → 0.85)</summary>
        public const float UnownedChipAlpha = 0.85f;

        /// <summary>아래 줄 글 정렬 · 색 (바뀔 때만 — 보유 'Lv.N' 왼쪽 Cream / 미보유 종족 가운데 SubText)</summary>
        void SetInfoStyle(TextAlignmentOptions alignment, Color color)
        {
            if (_infoText.alignment != alignment) _infoText.alignment = alignment;
            if (_infoText.color != color) _infoText.color = color;
            var style = alignment == TextAlignmentOptions.MidlineLeft ? FontStyles.Bold : FontStyles.Normal;
            if (_infoText.fontStyle != style) _infoText.fontStyle = style;
        }

        void OnClick()
        {
            if (_lineage != null) _onClick?.Invoke(_lineage);
        }
    }
}
