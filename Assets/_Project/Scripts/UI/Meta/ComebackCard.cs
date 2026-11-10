using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 다시 만난 날 900×1100 (CR-06, SPEC4 §8.2): 머리 그림 (Art/UI/comeback_header — 없으면 return_header, 840×300 덮어 맞춤) +
    /// '다시 만나서 반가워요!' (56 → 44까지) + '{d}일 만이에요. 친구들이 수선점을 지키며 기다렸어요.' + 도장 칸 3개 220×260
    /// (도장 고리 + '{n}일째' + 보상 칩, 오늘 칸은 금빛) + [도장 찍기] Gold 520×112 → ComebackSystem.TryClaimToday → 도장 연출 (Stamp 소리) → 받은 것이 날아간다.
    /// 오늘 도장을 찍었으면 버튼이 '내일 또 만나요' (누를 수 없음). 셋째 날을 찍으면 '이제 다시 매일 만나요!' 토스트와 함께 1초 뒤 닫힌다.
    /// 발표자(MetaPopupPresenter)가 접속 묶음에서 복귀 화면 다음에 띄운다 (Comeback 요청). 다시 만난 날이 아니면 띄우지 않는다.
    /// 보상은 콘텐츠 MetaDatabase.comeback.days (day 1~3).
    /// </summary>
    public class ComebackCard : UIPopup
    {
        public const int Days = 3;
        const float StampSeconds = 0.35f;
        /// <summary>
        /// 날 글 '{n}일째' 색 (RM-3 RI-2): 칸 그림(출석 칸) 위 라벤더 띠 위라 오늘이든 아니든 Ink — 띠에서 5.4:1 이상.
        /// InkSub(#7A6894)는 그 띠에서 2.2:1이라 쓰지 않는다. 지난 날은 도장과 흐린 칩(SetDimmed)으로 보인다. 빌더도 이 값으로 만든다
        /// </summary>
        public static readonly Color DayLabelColor = UITheme.Ink;

        [SerializeField] TMP_Text _bodyText;
        [Tooltip("도장 칸 3개 (1 · 2 · 3일째)")]
        [SerializeField] RectTransform[] _cells = Array.Empty<RectTransform>();
        [SerializeField] Image[] _cellBacks = Array.Empty<Image>();
        [SerializeField] GameObject[] _cellGlows = Array.Empty<GameObject>();
        [SerializeField] TMP_Text[] _cellDays = Array.Empty<TMP_Text>();
        [SerializeField] MetaRewardChip[] _cellRewards = Array.Empty<MetaRewardChip>();
        [Tooltip("찍힌 도장 (칸마다)")]
        [SerializeField] RectTransform[] _cellStamps = Array.Empty<RectTransform>();
        [SerializeField] Button _stampButton;
        [SerializeField] TMP_Text _stampLabel;
        [SerializeField] MetaSparkleBurst _sparkles;

        Coroutine _stamp;

        protected override void Awake()
        {
            base.Awake();
            if (_stampButton != null) _stampButton.onClick.AddListener(OnStamp);
        }

        /// <summary>다시 만난 날 카드를 띄운다. 다시 만난 날이 아니면 false</summary>
        public bool Show()
        {
            var comeback = MetaUi.IsActive ? Game.Comeback : null;
            if (comeback == null || !comeback.IsActive) return false;
            if (_bodyText != null) _bodyText.text = BodyFor(comeback.AwaySeconds);
            Refresh();
            Open();
            return true;
        }

        /// <summary>본문 (순수): 비운 날 수는 내림, 적어도 3일 (72시간 이상일 때만 뜬다)</summary>
        public static string BodyFor(long awaySeconds)
        {
            long days = Math.Max(3, awaySeconds / 86400L);
            return $"{days}일 만이에요. 친구들이 수선점을 지키며 기다렸어요.";
        }

        void Refresh()
        {
            var comeback = Game.Comeback;
            int today = comeback != null ? Mathf.Clamp(comeback.Day, 1, Days) : 1;
            bool canClaim = comeback != null && comeback.CanClaimToday;
            var db = Game.MetaDb;
            for (int i = 0; i < _cells.Length && i < Days; i++)
            {
                int day = i + 1;
                bool stamped = day < today || (day == today && !canClaim);
                bool isToday = day == today && canClaim;
                if (i < _cellGlows.Length && _cellGlows[i] != null) _cellGlows[i].SetActive(isToday);
                if (i < _cellDays.Length && _cellDays[i] != null)
                {
                    _cellDays[i].text = $"{day}일째";
                    _cellDays[i].color = DayLabelColor;
                }
                if (i < _cellBacks.Length && _cellBacks[i] != null) _cellBacks[i].color = isToday ? UITheme.Gold : Color.white;
                if (i < _cellStamps.Length && _cellStamps[i] != null)
                {
                    _cellStamps[i].gameObject.SetActive(stamped);
                    _cellStamps[i].localScale = Vector3.one;
                }
                var chip = i < _cellRewards.Length ? _cellRewards[i] : null;
                if (chip == null) continue;
                var rewards = RewardsFor(db, day);
                bool has = rewards != null && rewards.Count > 0 && rewards[0] != null;
                chip.gameObject.SetActive(has);
                if (has)
                {
                    chip.Set(rewards[0]);
                    if (rewards.Count > 1) chip.SetSubText($"+{rewards.Count - 1}");
                    chip.SetDimmed(stamped);
                }
            }
            if (_stampButton != null) _stampButton.interactable = canClaim;
            if (_stampLabel != null) _stampLabel.text = canClaim ? "도장 찍기" : "내일 또 만나요";
        }

        static List<MetaReward> RewardsFor(MetaDatabase db, int day)
        {
            if (db == null || db.comeback == null || db.comeback.days == null) return null;
            foreach (var def in db.comeback.days)
            {
                if (def != null && def.day == day) return def.rewards;
            }
            return null;
        }

        protected override void OnClose()
        {
            if (_stamp != null) StopCoroutine(_stamp);
            _stamp = null;
        }

        void OnStamp()
        {
            if (!MetaUi.IsActive || _stamp != null) return;
            if (MetaUi.BlockIfTampered()) return;
            var comeback = Game.Comeback;
            if (comeback == null || !comeback.CanClaimToday) return;
            int day = Mathf.Clamp(comeback.Day, 1, Days);
            var granted = comeback.TryClaimToday();
            if (granted == null)
            {
                MetaUi.Toast("오늘 도장은 이미 찍었어요");
                Refresh();
                return;
            }
            if (_stampButton != null) _stampButton.interactable = false;
            if (isActiveAndEnabled) _stamp = StartCoroutine(StampRoutine(day - 1, granted, day >= Days));
            else Close();
        }

        IEnumerator StampRoutine(int index, List<MetaReward> granted, bool last)
        {
            var stamp = index >= 0 && index < _cellStamps.Length ? _cellStamps[index] : null;
            UISfx.Play(UISfxKey.Stamp);
            if (stamp != null)
            {
                stamp.gameObject.SetActive(true);
                if (!UIMotion.Reduced)
                {
                    for (float t = 0f; t < StampSeconds; t += Time.unscaledDeltaTime)
                    {
                        stamp.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, MetaUi.EaseOutCubic(t / StampSeconds));
                        yield return null;
                    }
                }
                stamp.localScale = Vector3.one;
            }
            var cell = index >= 0 && index < _cells.Length ? _cells[index] : null;
            if (_sparkles != null && cell != null) _sparkles.BurstAt(cell, 10, UITheme.Gold, 0.8f);
            if (granted.Count > 0) MetaUi.FlyRewards(granted, cell != null ? cell : Window);
            _stamp = null;
            Refresh();
            if (!last) yield break;
            MetaUi.Toast("이제 다시 매일 만나요!", ToastKind.Success);
            yield return new WaitForSecondsRealtime(1f);
            Close();
        }
    }
}
