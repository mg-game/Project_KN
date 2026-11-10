using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 쿠폰 입력 (EVT-02, SPEC4 §4.5 — 확장 항목, 설정 [계정·데이터] '입력'이 연다, 820x560).
    ///   · 입력 칸 600x96: 영문 · 숫자만, 소문자는 대문자로 (onValidateInput), 16자, 자리표시 '쿠폰 번호'. [받기] Gold 520x112 (키보드 완료도 같다).
    ///   · Game.Coupons.TryRedeem → 받음: RewardBig 소리 + Success 토스트 '쿠폰 선물을 우편함에 넣어 두었어요' + 닫기 (보상은 우편, 30일).
    ///     아니면 결과 한 줄 (Caption 22): 다시 확인 / 이미 받음 / 기간 아님 / 잠시 뒤에 / 기기 시간(DailyClock.TamperedMessage) + 막힘 소리.
    ///   · 번호 다듬기(공백 · '-' 빼기, 대문자, 6~16자)와 해시는 CouponSystem 몫 (P4-2). 콘텐츠에 쿠폰이 없으면 늘 '다시 확인해 주세요'.
    /// </summary>
    public class CouponPopup : UIPopup
    {
        /// <summary>입력 칸 최대 글자 수 (SPEC4 §4.5)</summary>
        public const int MaxLength = 16;
        internal const string OkText = "쿠폰 선물을 우편함에 넣어 두었어요";
        internal const string EmptyText = "쿠폰 번호를 넣어 주세요";
        internal const string UnavailableText = "지금은 쿠폰을 쓸 수 없어요";

        [SerializeField] TMP_InputField _input;
        [SerializeField] Button _redeemButton;
        [Tooltip("결과 한 줄 (Caption 22)")]
        [SerializeField] TMP_Text _feedbackText;

        protected override void Awake()
        {
            base.Awake();
            if (_input != null)
            {
                _input.characterLimit = MaxLength;
                _input.onValidateInput = ValidateChar;
                _input.onSubmit.AddListener(OnSubmit);
            }
            if (_redeemButton != null) _redeemButton.onClick.AddListener(Redeem);
        }

        protected override void OnOpen()
        {
            if (_input != null) _input.SetTextWithoutNotify(string.Empty);
            SetFeedback(string.Empty, false);
        }

        /// <summary>입력 글자 거르기 (테스트용 순수 함수): 영문은 대문자로, 숫자는 그대로, 나머지는 버린다 ('\0')</summary>
        internal static char ValidateChar(string text, int index, char c)
        {
            if (c >= 'a' && c <= 'z') return (char)(c - 'a' + 'A');
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return c;
            return '\0';
        }

        /// <summary>결과 글 (SPEC4 §4.5, 테스트용 순수 함수)</summary>
        internal static string FeedbackText(CouponResult result) => result switch
        {
            CouponResult.Ok => OkText,
            CouponResult.AlreadyUsed => "이미 받은 쿠폰이에요",
            CouponResult.OutOfPeriod => "사용 기간이 아닌 쿠폰이에요",
            CouponResult.Locked => "잠시 뒤에 다시 입력해 주세요",
            CouponResult.Tampered => DailyClock.TamperedMessage,
            _ => "쿠폰 번호를 다시 확인해 주세요",
        };

        void OnSubmit(string _) => Redeem();

        void Redeem()
        {
            string code = _input != null ? _input.text : string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                SetFeedback(EmptyText, true);
                return;
            }
            if (!Game.IsMetaActive || Game.Coupons == null)
            {
                SetFeedback(UnavailableText, true);
                return;
            }
            CouponResult result;
            try
            {
                result = Game.Coupons.TryRedeem(code);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Coupon] 쿠폰을 확인하지 못했어요: {e.Message}");
                result = CouponResult.Invalid;
            }
            if (result == CouponResult.Ok)
            {
                UISfx.Play(UISfxKey.RewardBig);
                UIManager.Instance.ShowToast(OkText, ToastKind.Success);
                Close();
                return;
            }
            SetFeedback(FeedbackText(result), true);
        }

        void SetFeedback(string text, bool error)
        {
            if (error) UISfx.Play(UISfxKey.Deny);
            if (_feedbackText == null) return;
            _feedbackText.text = text;
            _feedbackText.color = error ? UITheme.Negative : UITheme.SubText;
        }
    }
}
