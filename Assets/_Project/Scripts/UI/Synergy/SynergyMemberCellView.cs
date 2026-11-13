using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 시너지 상세의 친구 칸: 초상화 · 이름 · 상태(지킴이 편성 / 편성 중 ×2 / 보유 / 진화 전 / 미보유).
    /// 미보유 친구는 실루엣. 누르면 보유 친구는 캐릭터 상세, 아니면 만나는 방법 안내.
    /// </summary>
    public class SynergyMemberCellView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] Image _frame;
        [SerializeField] Image _inner;
        [SerializeField] GameObject _placedGlow;
        [SerializeField] FormPortraitView _portrait;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] Image _stateChip;
        [SerializeField] TMP_Text _stateText;
        [SerializeField] GameObject _lockMark;
        [Tooltip("편성 중 표시: 칸 밖 6px Lamp 테 (테두리 색은 등급 색 그대로 — 금빛 테가 전설 등급과 헷갈리지 않게)")]
        [SerializeField] GameObject _placedRing;
        [Tooltip("편성 중 표시: 오른쪽 위 체크 (Icons/check)")]
        [SerializeField] GameObject _placedCheck;

        SynergyMember _member;

        void Awake() => _button.onClick.AddListener(OnClick);

        public void Set(SynergyMember member)
        {
            _member = member;
            var state = member.State;
            bool placed = state == SynergyMemberState.Placed;
            bool known = state != SynergyMemberState.Unowned;

            // 테두리는 늘 등급 색 (C-06: 편성 중을 금빛 테두리로 칠하면 편성 안 된 전설 친구와 같아 보였다) — 편성 중은 바깥 테 + 체크로
            _frame.color = known ? UITheme.GradeColor(member.Lineage.grade) : UITheme.Divider;
            if (_placedRing != null) _placedRing.SetActive(placed);
            if (_placedCheck != null) _placedCheck.SetActive(placed);
            // 안쪽은 보라 바느질 판 그림 — 모르는 친구는 알파만 낮춘다 (어두운 색을 곱하지 않는다)
            _inner.color = known ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            _placedGlow.SetActive(placed);

            _portrait.SetForm(member.FormId);
            _portrait.SetSilhouette(state == SynergyMemberState.Unowned);
            _portrait.SetAlpha(state == SynergyMemberState.FormLocked ? 0.55f : 1f);

            // 조합에 필요한 친구를 찾아갈 수 있게 미보유여도 이름은 보여준다 (초상화만 실루엣)
            _nameText.text = Game.Characters.GetDisplayName(member.FormId);
            _nameText.color = known ? UITheme.Cream : UITheme.SubText;

            _stateChip.color = SynergyUI.MemberStateColor(state);
            _stateText.text = SynergyUI.MemberStateName(member);
            _stateText.color = placed || state == SynergyMemberState.Owned ? UITheme.Ink : UITheme.Text;
            _lockMark.SetActive(!known || state == SynergyMemberState.FormLocked);
        }

        void OnClick()
        {
            if (_member.Lineage == null) return;
            switch (_member.State)
            {
                case SynergyMemberState.Unowned:
                    UIManager.Instance.ShowToast($"{MenuText.Topic(_member.Lineage.name)} 아직 만나지 못한 친구예요. 소환에서 만날 수 있어요.");
                    break;
                case SynergyMemberState.FormLocked:
                    var name = Game.Characters.Db.GetForm(_member.FormId)?.name ?? _member.FormId;
                    UIManager.Instance.ShowToast($"{MenuText.Topic(name)} 진화 제작으로 해금할 수 있어요.");
                    UINav.OpenCharacter(_member.Lineage.id);
                    break;
                default:
                    UINav.OpenCharacter(_member.Lineage.id);
                    break;
            }
        }
    }
}
