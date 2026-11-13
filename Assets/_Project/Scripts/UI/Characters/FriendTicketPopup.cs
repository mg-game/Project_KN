using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 친구 선택권 (PRG-04, SPEC4 §6.4 — BuildPopup 900×1100, '친구 선택권'). 소환 마일스톤 100회 보상으로 받은 선택권으로 전설 이하 친구 하나를 고른다.
    ///   · 칸 (빌더가 미리 만든 칸을 다시 쓴다): 초상화 180 · 이름 · 별, 이미 함께인 친구는 '기억편 +{중복 기억편}' (Caption 22), 처음 만나면 '새 친구!' 리본.
    ///   · [이 친구로 할게요] (금) → GachaMilestones.TryUseFriendTicket(계보) — 중복이면 기억편으로 바뀐다 — 그다음 소환 결과 창처럼 크게 보여 준다
    ///     (GachaResultPopup, 다시 소환 버튼 없이).
    ///   · 고르기 전에는 선택권이 줄지 않는다: 창을 닫으면 그대로 남는다.
    /// 칸 수보다 고를 친구가 많으면 앞(높은 등급 → 데이터 순)부터 칸 수만큼만 보여 준다.
    /// </summary>
    public class FriendTicketPopup : UIPopup
    {
        /// <summary>선택권으로 고를 수 있는 가장 높은 등급 (§2.11 #4)</summary>
        public const ItemGrade MaxGrade = ItemGrade.Legendary;

        [SerializeField] TMP_Text _ticketText;
        [Header("칸 (같은 순서)")]
        [SerializeField] Button[] _cells;
        [SerializeField] FriendPortrait[] _portraits;
        [SerializeField] TMP_Text[] _names;
        [SerializeField] TMP_Text[] _stars;
        [SerializeField] TMP_Text[] _subTexts;
        [SerializeField] GameObject[] _newRibbons;
        [SerializeField] GameObject[] _selectFrames;
        [SerializeField] Button _confirmButton;

        readonly List<LineageDef> _choices = new();
        int _selected = -1;

        protected override void Awake()
        {
            base.Awake();
            if (_cells != null)
            {
                for (int i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i] == null) continue;
                    int index = i;
                    _cells[i].onClick.AddListener(() => Select(index));
                }
            }
            if (_confirmButton != null) _confirmButton.onClick.AddListener(Confirm);
        }

        public void Show()
        {
            Open();
            BuildChoices();
            _selected = -1;
            Refresh();
        }

        /// <summary>선택권으로 고를 수 있는 계보인지 (전설 이하)</summary>
        internal static bool Eligible(LineageDef lineage) => lineage != null && !string.IsNullOrEmpty(lineage.id) && lineage.grade <= MaxGrade;

        /// <summary>칸 아래 글: 함께인 친구 '기억편 +n', 처음이면 '새 친구'</summary>
        internal static string SubLine(bool owned, int duplicateShards) => owned ? $"기억편 +{duplicateShards:N0}" : "새 친구";

        void BuildChoices()
        {
            _choices.Clear();
            var db = Game.IsReady && Game.Characters != null ? Game.Characters.Db : null;
            if (db == null) return;
            var all = db.lineages;
            for (int i = 0; i < all.Count; i++)
            {
                if (Eligible(all[i])) _choices.Add(all[i]);
            }
            // 높은 등급 먼저, 같으면 데이터 순 (안정 정렬 — 삽입 정렬, 많아야 수십)
            for (int i = 1; i < _choices.Count; i++)
            {
                var item = _choices[i];
                int j = i - 1;
                while (j >= 0 && _choices[j].grade < item.grade)
                {
                    _choices[j + 1] = _choices[j];
                    j--;
                }
                _choices[j + 1] = item;
            }
        }

        void Refresh()
        {
            int tickets = MetaUi.IsActive && Game.GachaMilestones != null ? Game.GachaMilestones.FriendTickets : 0;
            if (_ticketText != null) _ticketText.text = $"선택권 <color={UITheme.ColorTag(UITheme.Gold)}>{tickets}</color>장 · ★4 전설 이하에서 한 친구를 골라요";
            int cells = _cells != null ? _cells.Length : 0;
            for (int i = 0; i < cells; i++)
            {
                var lineage = i < _choices.Count ? _choices[i] : null;
                bool on = lineage != null;
                if (_cells[i] != null && _cells[i].gameObject.activeSelf != on) _cells[i].gameObject.SetActive(on);
                if (!on) continue;
                bool owned = CharacterUI.IsOwned(lineage);
                var portrait = At(_portraits, i);
                if (portrait != null) portrait.Set(CharacterUI.Portrait(CharacterUI.BaseForm(lineage)), lineage, lineage.name, PortraitTone.Normal);
                var name = At(_names, i);
                if (name != null) name.text = lineage.name;
                var stars = At(_stars, i);
                if (stars != null)
                {
                    stars.text = UITheme.Stars(lineage.grade);
                    stars.color = UITheme.GradeColor(lineage.grade);
                }
                var sub = At(_subTexts, i);
                if (sub != null) sub.text = SubLine(owned, owned ? Game.Characters.GetDuplicateShards(lineage.grade) : 0);
                var ribbon = At(_newRibbons, i);
                if (ribbon != null && ribbon.activeSelf == owned) ribbon.SetActive(!owned);
                var frame = At(_selectFrames, i);
                if (frame != null && frame.activeSelf != (i == _selected)) frame.SetActive(i == _selected);
            }
            if (_confirmButton != null) _confirmButton.interactable = _selected >= 0 && tickets > 0;
        }

        static T At<T>(T[] items, int index) where T : class => items != null && index >= 0 && index < items.Length ? items[index] : null;

        void Select(int index)
        {
            if (index < 0 || index >= _choices.Count) return;
            _selected = index;
            UISfx.Play(UISfxKey.Tap);
            Refresh();
        }

        void Confirm()
        {
            if (_selected < 0 || _selected >= _choices.Count)
            {
                CharacterUI.Toast("먼저 함께할 친구를 골라 주세요.");
                return;
            }
            var lineage = _choices[_selected];
            var milestones = MetaUi.IsActive ? Game.GachaMilestones : null;
            var grant = milestones != null ? milestones.TryUseFriendTicket(lineage.id) : null;
            if (grant == null)
            {
                CharacterUI.Toast("지금은 선택권을 쓸 수 없어요.");
                Refresh();
                return;
            }
            Close();
            var ui = UIManager.Instance;
            if (ui == null || !ui.TryGet(out GachaResultPopup result)) return;
            var shown = grant.Lineage != null ? grant.Lineage : lineage;
            result.Show(new[] { new GachaResult(shown, grant.IsNew, false, grant.ShardsGained) }, string.Empty, string.Empty, false, null);
        }
    }
}
