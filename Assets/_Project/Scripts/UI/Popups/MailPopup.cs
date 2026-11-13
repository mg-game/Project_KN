using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 우편함 (M20 · M51 · M26 · M76).
    ///   · 줄 [받기] (금): 보상을 그 줄 버튼 자리에서 상단 재화 칩 · 가방으로 날리고(RewardFlyer, 토스트 없음) 버튼 자리에 '받음' 도장 →
    ///     0.25초 뒤 목록을 다시 그린다 (그동안 우편 Changed로 오는 다시 그리기는 미뤄 둔다 — 날아가기 출발점이 남아 있게).
    ///     만료된 우편이면 Error 토스트 '만료된 우편이에요.' 후 바로 다시 그린다. 보상 없는 알림 우편은 바로 다시 그린다.
    ///   · 아래 오른쪽 [모두 받기] (UIKit.ClaimAllButton 380x96 금, 개수 배지): 받을 우편이 있을 때만 보인다 (SetActive — 흐리게 두지 않는다).
    ///     열려 있는 동안 0 → 생기면 0.85 → 1 튀어나옴. 누르면 합친 보상을 그 버튼에서 날리고 받은 줄마다 도장.
    ///   · 날아가기 층이 없으면(RewardFlyer.IsAvailable == false) Success 토스트 '획득: 3개까지 · 외 N개'로 대신한다.
    ///   · 소리: 줄 [받기] · [모두 받기] = MailClaim (AUDIO-SPEC §2.1 — 두 버튼 모두 누름 소리를 꺼 두었다). 보상 없는 알림 우편 [확인]은 Tap 0.5.
    /// </summary>
    public class MailPopup : UIPopup
    {
        /// <summary>받은 뒤 목록을 다시 그리기까지 (도장이 보일 시간)</summary>
        const float RefreshDelay = 0.25f;
        /// <summary>대신 띄우는 토스트에 적는 보상 수</summary>
        const int ToastRewardLimit = 3;

        [SerializeField] RectTransform _listContent;
        [SerializeField] MailItemView _itemPrefab;
        [SerializeField] TMP_Text _countText;
        [SerializeField] GameObject _emptyLabel;
        [SerializeField] Button _claimAllButton;
        [Tooltip("모두 받기 개수 배지 (받을 우편 수)")]
        [SerializeField] Badge _claimAllBadge;

        UIListPool<MailItemView> _pool;
        Action<MailData, RectTransform> _onClaim;
        Action _onMailChanged;
        bool _subscribed;
        /// <summary>받은 직후 — 목록 다시 그리기를 미룬다 (날아가기 출발점 · 도장이 남아 있게)</summary>
        bool _deferRefresh;
        bool _suppressPop;
        Coroutine _deferred;
        Coroutine _claimAllPop;
        readonly StringBuilder _toast = new(64);

        protected override void Awake()
        {
            base.Awake();
            _pool = new UIListPool<MailItemView>(_itemPrefab, _listContent);
            _onClaim = Claim;
            _onMailChanged = Refresh;
            _claimAllButton.onClick.AddListener(ClaimAll);
        }

        protected override void OnOpen()
        {
            if (!_subscribed)
            {
                Game.Mail.Changed += _onMailChanged;
                _subscribed = true;
            }
            _deferRefresh = false;
            Game.Mail.RemoveExpired();
            _suppressPop = true;
            Refresh();
            _suppressPop = false;
        }

        protected override void OnClose()
        {
            _deferRefresh = false;
            _deferred = null;
            _claimAllPop = null;
            if (_claimAllButton != null) _claimAllButton.transform.localScale = Vector3.one;
            if (_subscribed && Game.IsReady) Game.Mail.Changed -= _onMailChanged;
            _subscribed = false;
        }

        void Refresh()
        {
            if (_deferRefresh) return;
            var mails = Game.Mail.Mails;
            _pool.SetCount(mails.Count);
            int claimable = 0;
            for (int i = 0; i < mails.Count; i++)
            {
                _pool[i].Set(mails[i], _onClaim);
                if (mails[i].HasRewards) claimable++;
            }

            _countText.text = $"우편 {mails.Count}개";
            _emptyLabel.SetActive(mails.Count == 0);

            bool show = claimable > 0;
            bool appeared = show && !_claimAllButton.gameObject.activeSelf;
            _claimAllButton.gameObject.SetActive(show);
            if (!_claimAllButton.interactable) _claimAllButton.interactable = true;
            if (_claimAllBadge != null) _claimAllBadge.SetCount(claimable);
            if (appeared && !_suppressPop) PopClaimAll();
        }

        void Claim(MailData mail, RectTransform button)
        {
            _deferRefresh = true;
            var rewards = Game.Mail.Claim(mail.id);
            if (rewards == null)
            {
                _deferRefresh = false;
                UIManager.Instance.ShowToast("만료된 우편이에요.", ToastKind.Error);
                Refresh();
                return;
            }
            if (rewards.Count == 0)
            {
                // 보상 없는 알림 우편 ([확인]): 지우기만 했으니 바로 다시 그린다 (버튼 누름 소리는 꺼 두었으니 여기서)
                UISfx.Play(UISfxKey.Tap, 0.5f);
                _deferRefresh = false;
                Refresh();
                return;
            }

            UISfx.Play(UISfxKey.MailClaim);
            Fly(rewards, button);
            var row = button != null ? button.GetComponentInParent<MailItemView>() : null;
            if (row != null) row.PlayClaimed();
            RefreshLater();
        }

        void ClaimAll()
        {
            _deferRefresh = true;
            var rewards = Game.Mail.ClaimAll();
            if (rewards.Count == 0)
            {
                _deferRefresh = false;
                UIManager.Instance.ShowToast("받을 보상이 없어요.");
                Refresh();
                return;
            }

            UISfx.Play(UISfxKey.MailClaim);
            // 모두 받기는 이미 합친 보상 (MailSystem.ClaimAll → RewardService.Merge)
            Fly(rewards, (RectTransform)_claimAllButton.transform);
            for (int i = 0; i < _pool.Count; i++)
            {
                var row = _pool[i];
                if (row.Mail != null && row.Mail.HasRewards) row.PlayClaimed();
            }
            RefreshLater();
        }

        void Fly(List<RewardData> rewards, RectTransform from)
        {
            if (RewardFlyer.IsAvailable) RewardFlyer.Fly(rewards, from);
            else ShowRewardToast(rewards);
        }

        void RefreshLater()
        {
            if (!isActiveAndEnabled)
            {
                _deferRefresh = false;
                Refresh();
                return;
            }
            MenuFx.Restart(this, ref _deferred, RefreshAfterDelay());
        }

        IEnumerator RefreshAfterDelay()
        {
            yield return new WaitForSecondsRealtime(RefreshDelay);
            _deferred = null;
            _deferRefresh = false;
            Refresh();
        }

        /// <summary>모두 받기가 새로 생김: 0.85 → 1 (0.18초)</summary>
        void PopClaimAll()
        {
            if (UIMotion.Reduced || !isActiveAndEnabled) return;
            MenuFx.Restart(this, ref _claimAllPop, Pop(_claimAllButton.transform));
        }

        IEnumerator Pop(Transform target)
        {
            for (float t = 0f; t < UIMotion.ClaimAllPop; t += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * Mathf.LerpUnclamped(UIMotion.ClaimAllPopFrom, 1f, UIMotion.OutBack(t / UIMotion.ClaimAllPop));
                yield return null;
            }
            target.localScale = Vector3.one;
            _claimAllPop = null;
        }

        /// <summary>날아가기 층이 없을 때: '획득: A · B · C 외 N개' (Success)</summary>
        void ShowRewardToast(List<RewardData> rewards)
        {
            _toast.Clear();
            _toast.Append("획득: ");
            int shown = Mathf.Min(rewards.Count, ToastRewardLimit);
            for (int i = 0; i < shown; i++)
            {
                if (i > 0) _toast.Append(" · ");
                _toast.Append(Game.Rewards.Describe(rewards[i]));
            }
            if (rewards.Count > shown) _toast.Append(" 외 ").Append(rewards.Count - shown).Append("개");
            UIManager.Instance.ShowToast(_toast.ToString(), ToastKind.Success);
        }
    }
}
