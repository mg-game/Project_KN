using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 수선 일지 점수 상자 하나 (20/40/60/80/100점 · M60A · M61). 점수 막대 위 제자리에 놓인다.
    /// 잠김(흐림) → 받을 수 있음(빛이 숨 쉬고 들썩임 + 점) → 받음(열린 상자 + 체크). 누르면 받기, 아직이면 내용 미리보기 토스트.
    /// 상자 위 보상 이름표 'Reward' (첫 보상 그림 24 + 양 + 더 있으면 '+1'): 아직이면 Cream, 받을 수 있으면 Gold, 받았으면 SubText@0.6 (그림 @0.5).
    /// 누름 크기(PressScale)는 상자 루트 (빌더).
    /// </summary>
    public class MissionChestView : MonoBehaviour
    {
        [SerializeField] MetaIcons _icons;
        [SerializeField] Button _button;
        [SerializeField] RectTransform _visual;
        [SerializeField] Image _chest;
        [Tooltip("상자 아트가 없을 때 보이는 자리표시 (뚜껑)")]
        [SerializeField] Image _placeholderLid;
        [SerializeField] Image _glow;
        [SerializeField] TMP_Text _pointsText;
        [SerializeField] GameObject _check;
        [Tooltip("받을 수 있음 점 (SceneParts.BuildBadge 점 모드 — Badge가 없으면 켜고 끈다)")]
        [SerializeField] GameObject _dot;
        [Header("보상 이름표 (상자 위, 없어도 됨)")]
        [SerializeField] GameObject _rewardRoot;
        [SerializeField] Image _rewardIcon;
        [SerializeField] TMP_Text _rewardAmount;
        [SerializeField] TMP_Text _rewardMore;

        /// <summary>아직 점수가 모자란 상자: 그림에 라벤더를 옅게 섞고 살짝 투명하게 (어둡게 덮지 않음)</summary>
        static readonly Color LockedArtColor = new(0.84f, 0.82f, 0.9f, 0.8f);

        MissionChestStatus _status;
        Action<MissionChestView> _onClick;
        bool _claimable;
        float _time;
        Coroutine _open;
        Badge _dotBadge;
        bool _dotChecked;

        public MissionChestStatus Status => _status;

        public RectTransform Visual => _visual;

        void Awake() => _button.onClick.AddListener(() => _onClick?.Invoke(this));

        public void Bind(MissionChestStatus status, Action<MissionChestView> onClick)
        {
            _status = status;
            _onClick = onClick;
            _pointsText.text = status.Def.points.ToString();
            _claimable = status.CanClaim;
            bool claimed = status.IsClaimed;

            var sprite = _icons != null ? (claimed ? _icons.ChestOpen : _icons.ChestClosed) : null;
            bool hasArt = sprite != null;
            _chest.sprite = hasArt ? sprite : _icons != null ? _icons.Round : null;
            _chest.color = hasArt
                ? (status.IsReached || claimed ? Color.white : LockedArtColor)
                : (claimed ? new Color(0.62f, 0.55f, 0.48f, 1f) : status.IsReached ? UITheme.Gold : new Color(0.45f, 0.4f, 0.62f, 1f));
            if (_placeholderLid != null)
            {
                _placeholderLid.gameObject.SetActive(!hasArt);
                _placeholderLid.rectTransform.localRotation = Quaternion.Euler(0f, 0f, claimed ? 24f : 0f);
                _placeholderLid.color = status.IsReached && !claimed ? UITheme.Cream : new Color(0.7f, 0.64f, 0.82f, 1f);
            }
            _pointsText.color = status.IsReached ? UITheme.Gold : UITheme.SubText;
            BindReward(status, claimed);
            _check.SetActive(claimed);
            SetDot(_claimable);
            _glow.enabled = _claimable;
            if (!_claimable && _open == null) _visual.localRotation = Quaternion.identity;
        }

        /// <summary>받을 수 있음 점: 공용 배지면 점 모드(나타남 연출 포함, 숫자 없음), 아니면 켜고 끈다 (§3.13.10)</summary>
        void SetDot(bool on)
        {
            if (_dotBadge == null && !_dotChecked)
            {
                _dotChecked = true;
                _dotBadge = _dot.GetComponent<Badge>();
            }
            if (_dotBadge != null) _dotBadge.SetDot(on);
            else if (_dot.activeSelf != on) _dot.SetActive(on);
        }

        /// <summary>상자 위 보상 이름표: 첫 보상 그림 + 양 (+ 나머지 수). 아직 = Cream, 받을 수 있음 = Gold, 받음 = SubText@0.6 (그림 @0.5)</summary>
        void BindReward(MissionChestStatus status, bool claimed)
        {
            if (_rewardRoot == null) return;
            // 단추 · N분치는 실제 지급과 같게 단추 · 실타래 두 줄로 풀어 첫 줄 + 나머지 수 (MetaUi.FirstPreview)
            var first = MetaUi.FirstPreview(status.Def.rewards, out int rewardCount);
            _rewardRoot.SetActive(first != null);
            if (first == null) return;
            Color text = claimed ? MetaUi.Alpha(UITheme.SubText, 0.6f) : status.IsReached ? UITheme.Gold : UITheme.Cream;
            if (_rewardIcon != null)
            {
                var sprite = _icons != null ? _icons.Reward(first) : null;
                if (sprite == null && _icons != null) sprite = _icons.RewardGeneric != null ? _icons.RewardGeneric : _icons.Star;
                _rewardIcon.sprite = sprite;
                _rewardIcon.enabled = sprite != null;
                _rewardIcon.preserveAspect = true;
                _rewardIcon.color = MetaUi.Alpha(MetaIcons.RewardTint(first), claimed ? 0.5f : 1f);
            }
            if (_rewardAmount != null)
            {
                _rewardAmount.text = MetaUi.ShortAmount(first);
                _rewardAmount.color = text;
            }
            if (_rewardMore != null)
            {
                int more = rewardCount - 1;
                _rewardMore.gameObject.SetActive(more > 0);
                if (more > 0) _rewardMore.text = "+" + more;
                _rewardMore.color = claimed ? MetaUi.Alpha(UITheme.SubText, 0.6f) : UITheme.SubText;
            }
        }

        /// <summary>받음: 상자가 들썩이며 열린다</summary>
        public void PlayOpen()
        {
            if (!isActiveAndEnabled) return;
            if (_open != null) StopCoroutine(_open);
            _open = StartCoroutine(Open());
        }

        IEnumerator Open()
        {
            const float duration = 0.45f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                _visual.localScale = new Vector3(1f + 0.3f * MetaUi.Punch(k), 1f + 0.18f * MetaUi.Punch(k), 1f);
                _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 4f) * 10f * (1f - k));
                yield return null;
            }
            _visual.localScale = Vector3.one;
            _visual.localRotation = Quaternion.identity;
            _open = null;
        }

        void Update()
        {
            if (!_claimable || _open != null) return;
            _time += Time.unscaledDeltaTime;
            float wave = 0.5f + 0.5f * Mathf.Sin(_time * 3.4f);
            _glow.color = MetaUi.Alpha(UITheme.Lamp, 0.35f + 0.45f * wave);
            // 1.6초마다 한 번 들썩
            float hop = Mathf.Repeat(_time, 1.6f);
            _visual.localRotation = hop < 0.4f ? Quaternion.Euler(0f, 0f, Mathf.Sin(hop / 0.4f * Mathf.PI * 3f) * 7f) : Quaternion.identity;
        }

        void OnDisable()
        {
            if (_open != null) StopCoroutine(_open);
            _open = null;
            _visual.localScale = Vector3.one;
            _visual.localRotation = Quaternion.identity;
        }
    }
}
