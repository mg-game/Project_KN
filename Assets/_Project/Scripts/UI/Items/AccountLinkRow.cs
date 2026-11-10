using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 설정 팝업의 계정 연동 한 줄 (Google / 카카오). 버튼은 흰색 틴트용 판 그림이라 상태 색을 입힌다:
    /// 연동 안 됨 = [연동] PrimaryButton, 연동됨 = [해제] Secondary. 해제는 되돌릴 수 없는 일 확인(ShowDangerConfirm '해제')을 거친다 (M55 · M82).
    /// </summary>
    public class AccountLinkRow : MonoBehaviour
    {
        [SerializeField] AuthProviderType _provider;
        [SerializeField] TMP_Text _statusText;
        [SerializeField] Button _button;
        [SerializeField] TMP_Text _buttonLabel;

        void Awake()
        {
            _button.onClick.AddListener(OnClick);
        }

        public void Refresh()
        {
            var link = Game.Account.GetLink(_provider);
            bool linked = link != null;
            _statusText.text = linked ? $"연동됨 · {link.displayName}" : "연동 안 됨";
            _statusText.color = linked ? UITheme.Positive : UITheme.SubText;
            _buttonLabel.text = linked ? "해제" : "연동";
            _button.targetGraphic.color = linked ? UITheme.Secondary : UITheme.PrimaryButton;
        }

        void OnClick()
        {
            string name = AccountSystem.ProviderName(_provider);
            if (!Game.Account.IsLinked(_provider))
            {
                Game.Account.Link(_provider, result => ShowResult(result, name));
                return;
            }

            string warning = Game.Account.LinkedCount == 1
                ? "\n해제하면 게스트 계정이 되어 기기를 바꿀 때 데이터를 잃을 수 있어요."
                : string.Empty;
            UIManager.Instance.ShowDangerConfirm("연동 해제", $"{name} 계정 연동을 해제할까요?{warning}", "해제",
                () => Game.Account.Unlink(_provider, result =>
                {
                    if (result == LinkResult.Success) UIManager.Instance.ShowToast($"{name} 계정 연동을 해제했어요.", ToastKind.Success);
                    else ShowResult(result, name);
                }));
        }

        static void ShowResult(LinkResult result, string name)
        {
            var kind = result == LinkResult.Success ? ToastKind.Success : ToastKind.Error;
            string message = result switch
            {
                LinkResult.Success => $"{name} 계정이 연동됐어요.",
                LinkResult.Cancelled => "로그인을 취소했어요.",
                LinkResult.NotAvailable => $"{name} 로그인은 준비 중이에요.",
                LinkResult.AlreadyLinked => $"이미 연동된 {name} 계정이에요.",
                LinkResult.LinkedToOtherAccount => $"다른 게임 계정에 연동된 {name} 계정이에요.",
                LinkResult.Busy => null,
                _ => "처리하지 못했어요. 잠시 후 다시 시도해 주세요.",
            };
            if (result == LinkResult.Cancelled || result == LinkResult.NotAvailable) kind = ToastKind.Info;
            if (message != null) UIManager.Instance.ShowToast(message, kind);
        }
    }
}
