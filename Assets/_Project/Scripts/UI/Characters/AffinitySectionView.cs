using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 호감도 칸 (CR-10, SPEC4 §6.3 — 친구 상세 정보 탭 맨 위, PanelInset9s). 보유 계보만 보인다 (호감도 시스템이 없으면 숨김).
    ///   · 'Lv.n 호감도' (Title 28 굵게 Cream) + 하트 배지 40 (UiArt.AffinityBadge, 뒤 후광 UiArt.AffinityHalo — 전용 아트가 없으면 빌더가 DustyPink로 칠한다).
    ///   · 막대 560×20 DustyPink + '{지금}/{다음}' (Label 24) — 최고 단계면 'MAX'.
    ///   · 받을 단계가 있으면 금 보상 칩(별조각 gemPerLevel × 단계 합) + [받기] 금 180×80 → Affinity.TryClaim (보상이 날아가고 AffinityUp 소리 + 하트 터짐).
    ///   · '기억 조각 k/5' + 다섯 줄 (Lv.2/4/6/8/10): 열린 줄은 Body 26 기울임, 잠긴 줄은 '…  Lv.n에 들을 수 있어요' (Hint 20 SubText).
    ///   · '오늘 쓰다듬기 k/3' (Caption 22 SubText — 전투에서 친구를 쓰다듬으면 오른다).
    /// 친구 상세가 다시 그릴 때만 (Show) 글을 쓴다 — 프레임마다 하는 일은 없다 (하트 터짐 코루틴 동안만).
    /// </summary>
    public class AffinitySectionView : MonoBehaviour
    {
        /// <summary>기억 조각 줄 수 (Lv.2 · 4 · 6 · 8 · 10)</summary>
        public const int StoryCount = 5;
        /// <summary>호감도 최고 단계</summary>
        public const int MaxLevel = 10;

        [SerializeField] TMP_Text _levelText;
        [SerializeField] RectTransform _barFill;
        [SerializeField] TMP_Text _progressText;
        [SerializeField] MetaRewardChip _rewardChip;
        [SerializeField] Button _claimButton;
        [SerializeField] TMP_Text _storyHeader;
        [SerializeField] TMP_Text[] _storyLines;
        [SerializeField] TMP_Text _petsText;
        [Tooltip("받을 때 터지는 하트 (UiArt.HeartBurst, 꺼 둠)")]
        [SerializeField] Image _heartBurst;

        string _lineageId;
        Coroutine _burst;

        void Awake()
        {
            if (_claimButton != null) _claimButton.onClick.AddListener(Claim);
            if (_heartBurst != null) _heartBurst.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            _burst = null;
            if (_heartBurst != null) _heartBurst.gameObject.SetActive(false);
        }

        /// <summary>기억 조각 index번째(0~4)가 열리는 단계 (Lv.2 · 4 · 6 · 8 · 10)</summary>
        internal static int StoryLevel(int index) => (Mathf.Clamp(index, 0, StoryCount - 1) + 1) * 2;

        /// <summary>단계 글: 'Lv.n 호감도'</summary>
        internal static string LevelLabel(int level) => $"Lv.{Mathf.Clamp(level, 1, MaxLevel)} 호감도";

        /// <summary>진행 글: 최고 단계 'MAX', 다음 단계를 알면 '{지금}/{다음}', 모르면 '{지금}'</summary>
        internal static string ProgressLabel(long current, long next, int level) =>
            level >= MaxLevel ? "MAX" : next > 0 ? $"{current:N0}/{next:N0}" : $"{current:N0}";

        /// <summary>막대 비율: 그 단계 시작 → 다음 단계 (최고 단계면 1, 다음을 모르면 0)</summary>
        internal static float ProgressFill(long current, long start, long next, int level)
        {
            if (level >= MaxLevel) return 1f;
            if (next <= start) return 0f;
            return Mathf.Clamp01((float)(current - start) / (next - start));
        }

        /// <summary>잠긴 기억 조각 줄: '…  Lv.n에 들을 수 있어요'</summary>
        internal static string LockedStoryLine(int level) =>
            $"…  <size=77%><color={UITheme.ColorTag(UITheme.SubText)}>Lv.{level}에 들을 수 있어요</color></size>";

        /// <summary>받을 별조각 합 (받은 단계 다음부터 지금 단계까지 gemPerLevel × 단계)</summary>
        internal static int ClaimGems(int claimedLevel, int level, int gemPerLevel)
        {
            int sum = 0;
            for (int l = Mathf.Max(2, claimedLevel + 1); l <= Mathf.Min(level, MaxLevel); l++) sum += gemPerLevel * l;
            return sum;
        }

        /// <summary>그 계보의 호감도 칸을 그린다 (보유 계보 + 호감도 시스템이 있을 때만 보인다)</summary>
        public void Show(LineageDef lineage)
        {
            var affinity = MetaUi.IsActive ? Game.Affinity : null;
            bool show = lineage != null && affinity != null && CharacterUI.IsOwned(lineage);
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
            if (!show) return;
            _lineageId = lineage.id;
            string id = lineage.id;

            int level = Mathf.Clamp(affinity.GetLevel(id), 1, MaxLevel);
            long current = Game.Characters.GetAffinity(id);
            long start = affinity.GetLevelStart(level);
            long next = affinity.GetNextThreshold(id);
            if (_levelText != null) _levelText.text = LevelLabel(level);
            if (_barFill != null) CharacterUI.SetFill(_barFill, ProgressFill(current, start, next, level));
            if (_progressText != null) _progressText.text = ProgressLabel(current, next, level);

            bool claimable = affinity.HasClaimable(id);
            if (_claimButton != null && _claimButton.gameObject.activeSelf != claimable) _claimButton.gameObject.SetActive(claimable);
            if (_rewardChip != null)
            {
                int perLevel = Game.MetaDb != null && Game.MetaDb.affinity != null ? Game.MetaDb.affinity.gemPerLevel : 10;
                int gems = claimable ? ClaimGems(affinity.GetClaimedLevel(id), level, perLevel) : 0;
                if (gems > 0) _rewardChip.Set(MetaReward.Gem(gems));
                else if (_rewardChip.gameObject.activeSelf) _rewardChip.gameObject.SetActive(false);
            }

            int unlocked = 0;
            int lines = _storyLines != null ? _storyLines.Length : 0;
            for (int i = 0; i < StoryCount; i++)
            {
                string story = affinity.GetStoryLine(id, i);
                bool open = !string.IsNullOrEmpty(story);
                if (open) unlocked++;
                if (i >= lines || _storyLines[i] == null) continue;
                var text = _storyLines[i];
                text.text = open ? story : LockedStoryLine(StoryLevel(i));
                text.fontStyle = open ? FontStyles.Italic : FontStyles.Normal;
                text.color = open ? UITheme.Text : UITheme.SubText;
            }
            if (_storyHeader != null) _storyHeader.text = $"기억 조각 {unlocked}/{StoryCount}";

            int perDay = Game.MetaDb != null && Game.MetaDb.affinity != null ? Mathf.Max(0, Game.MetaDb.affinity.petsPerDay) : 3;
            int left = Mathf.Clamp(affinity.PetsLeftToday(id), 0, perDay);
            if (_petsText != null) _petsText.text = $"오늘 쓰다듬기 {perDay - left}/{perDay}";
        }

        void Claim()
        {
            var affinity = MetaUi.IsActive ? Game.Affinity : null;
            if (affinity == null || string.IsNullOrEmpty(_lineageId)) return;
            var rewards = affinity.TryClaim(_lineageId);
            if (rewards == null || rewards.Count == 0) return;
            UISfx.Play(UISfxKey.AffinityUp);
            if (_claimButton != null) MetaUi.FlyRewards(rewards, (RectTransform)_claimButton.transform);
            PlayHeartBurst();
            var lineage = Game.Characters.Db.GetLineage(_lineageId);
            if (lineage != null) Show(lineage);
        }

        void PlayHeartBurst()
        {
            if (_heartBurst == null || !isActiveAndEnabled) return;
            if (_burst != null) StopCoroutine(_burst);
            _burst = StartCoroutine(HeartBurst());
        }

        /// <summary>하트가 0.6 → 1.4배로 부풀며 사라진다 (0.6초, 움직임 줄이기면 크기 그대로 알파만)</summary>
        IEnumerator HeartBurst()
        {
            var rect = _heartBurst.rectTransform;
            _heartBurst.gameObject.SetActive(true);
            bool reduced = UIMotion.Reduced;
            const float duration = 0.6f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rect.localScale = Vector3.one * (reduced ? 1f : Mathf.Lerp(0.6f, 1.4f, UIMotion.OutCubic(k)));
                _heartBurst.color = CharacterUI.WithAlpha(_heartBurst.color, 1f - k);
                yield return null;
            }
            _heartBurst.gameObject.SetActive(false);
            _burst = null;
        }
    }
}
