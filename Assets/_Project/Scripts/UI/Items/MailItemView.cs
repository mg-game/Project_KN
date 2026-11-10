using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 우편 한 줄. [받기](금 버튼 — 줄마다 반복되는 받기는 한 화면 금 하나 규칙의 예외)를 누르면 (우편, 버튼 자리)를 넘긴다 —
    /// 우편함이 그 자리에서 보상을 날린다 (M20). 받은 줄은 버튼 대신 '받음' 도장(금색, 1.4 → 1배 0.2초 OutBack)이 찍히고
    /// 우편함이 잠시 뒤(0.25초) 목록을 다시 그린다. 다시 그릴 때(Set) 도장은 지워진다.
    /// </summary>
    public class MailItemView : MonoBehaviour
    {
        /// <summary>도장 연출 시간 · 시작 크기 · 기울기</summary>
        const float StampTime = 0.2f;
        const float StampFromScale = 1.4f;
        const float StampAngle = -12f;

        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _bodyText;
        [SerializeField] TMP_Text _rewardText;
        [SerializeField] TMP_Text _expireText;
        [SerializeField] Button _claimButton;
        [SerializeField] TMP_Text _claimLabel;
        [Tooltip("받은 뒤 버튼 자리에 찍히는 '받음' 도장 (꺼 둔다)")]
        [SerializeField] RectTransform _stamp;

        MailData _mail;
        Action<MailData, RectTransform> _onClaim;
        Coroutine _stampAnim;

        /// <summary>이 줄이 보이는 우편 (모두 받기 때 받은 줄에 도장을 찍으려고)</summary>
        public MailData Mail => _mail;

        void Awake()
        {
            _claimButton.onClick.AddListener(OnClaim);
        }

        /// <summary>꺼질 때 코루틴은 저절로 멈춘다 — 도장 자국은 다음 Set이 지운다 (꺼지는 중에는 켜고 끄기를 바꾸지 않는다)</summary>
        void OnDisable() => _stampAnim = null;

        public void Set(MailData mail, Action<MailData, RectTransform> onClaim)
        {
            _mail = mail;
            _onClaim = onClaim;
            ResetStamp();

            _titleText.text = mail.title;
            _bodyText.text = mail.body;
            _rewardText.text = mail.HasRewards ? string.Join(" · ", mail.rewards.Select(Game.Rewards.Describe)) : string.Empty;
            _expireText.text = TimeUtil.FormatRemain(Game.Mail.GetRemainTime(mail));
            _claimLabel.text = mail.HasRewards ? "받기" : "확인";
        }

        void OnClaim()
        {
            if (_mail == null || !_claimButton.interactable) return;
            _onClaim?.Invoke(_mail, (RectTransform)_claimButton.transform);
        }

        /// <summary>받은 줄 표시: 버튼을 숨기고 그 자리에 '받음' 도장을 찍는다 (두 번 받지 않게 버튼도 막는다)</summary>
        public void PlayClaimed()
        {
            _claimButton.interactable = false;
            if (_stamp == null || !isActiveAndEnabled)
            {
                _claimButton.gameObject.SetActive(false);
                return;
            }
            if (_stampAnim != null) StopCoroutine(_stampAnim);
            _stampAnim = StartCoroutine(Stamp());
        }

        IEnumerator Stamp()
        {
            _claimButton.gameObject.SetActive(false);
            _stamp.gameObject.SetActive(true);
            _stamp.localRotation = Quaternion.Euler(0f, 0f, StampAngle);
            bool reduced = UIMotion.Reduced;
            for (float t = 0f; t < StampTime && !reduced; t += Time.unscaledDeltaTime)
            {
                _stamp.localScale = Vector3.one * Mathf.LerpUnclamped(StampFromScale, 1f, UIMotion.OutBack(t / StampTime));
                yield return null;
            }
            _stamp.localScale = Vector3.one;
            _stampAnim = null;
        }

        void ResetStamp()
        {
            if (_stampAnim != null)
            {
                StopCoroutine(_stampAnim);
                _stampAnim = null;
            }
            if (_stamp != null && _stamp.gameObject.activeSelf) _stamp.gameObject.SetActive(false);
            if (!_claimButton.gameObject.activeSelf) _claimButton.gameObject.SetActive(true);
            if (!_claimButton.interactable) _claimButton.interactable = true;
        }
    }
}
