using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 시너지 도감 줄의 작은 친구 얼굴: 바느질 고리(상태 색 — 편성 중 금빛, 보유 연보라) + 둥근 받침 + 초상화 + 편성 별.
    /// 미보유는 받침을 양피지 색으로 눌러 실루엣이 보이게 한다.
    /// </summary>
    public class SynergyMiniMemberView : MonoBehaviour
    {
        [SerializeField] Image _ring;
        [SerializeField] Image _inner;
        [SerializeField] FormPortraitView _portrait;
        [SerializeField] GameObject _placedDot;

        public void Set(SynergyMember member)
        {
            var state = member.State;
            _ring.color = SynergyUI.MemberStateColor(state);
            _inner.color = state == SynergyMemberState.Unowned ? UITheme.ParchmentDark : Color.white;
            _portrait.SetForm(member.FormId);
            _portrait.SetSilhouette(state == SynergyMemberState.Unowned);
            _portrait.SetAlpha(state == SynergyMemberState.FormLocked ? 0.5f : 1f);
            _placedDot.SetActive(state == SynergyMemberState.Placed);
        }
    }
}
