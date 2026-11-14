using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 수선 일지 미션 한 줄 860×150 (Q-02 · M62 · M76): 아이콘 · 문구 · 진행 막대 "12/30" · 점수 "+20점" (+ 보상 칩) · [가기 ›]/[받기]/[완료].
    /// 받을 수 있으면 금테가 숨 쉬고, 받으면 "완료" 도장이 찍히며 줄이 흐려진다. 목록은 JournalPopup이 재사용한다.
    /// 진행 막대는 목표로 미끄러진다 (UIMotion.EaseFill — 줄어들면 바로). 다른 미션으로 다시 묶이거나 켜질 때는 바로 맞춘다.
    /// Update는 금테가 숨 쉬거나 막대가 움직이는 동안만 일한다.
    /// 4차 (SPEC4 §6.1 · §8.3 · §13.6): Bind(in JournalRowModel, …)로 수선점 기록 · 이벤트 미션 · 첫 주 과제도 같은 줄로 그린다 — 그때 Status는 null이고
    /// Model · HasModel을 본다. 기록 단계 점(_tierPips, 14 · 간격 18 — 닿은 단계 Gold, 아니면 SubText@0.5) · 숨은 기록 힌트(_hintText, Hint 20 Lavender) ·
    /// 잠김 알약(_lockPill — M39)은 틀에 있을 때만 (다시 만들기 전 장면은 없어도 된다). 점수 알약은 PointsText가 비면 숨는다.
    /// </summary>
    public class MissionRowView : MonoBehaviour
    {
        [SerializeField] MetaIcons _icons;
        [SerializeField] CanvasGroup _group;
        [SerializeField] Image _background;
        [SerializeField] Image _goldEdge;
        [SerializeField] Image _icon;
        [SerializeField] Image _iconBack;
        [SerializeField] TMP_Text _text;
        [SerializeField] Image _progressFill;
        [SerializeField] TMP_Text _progressText;
        [SerializeField] RectTransform _pointsPill;
        [SerializeField] TMP_Text _pointsText;
        [SerializeField] MetaRewardChip _rewardChip;
        [SerializeField] Button _button;
        [SerializeField] Image _buttonImage;
        [SerializeField] TMP_Text _buttonLabel;
        [Header("버튼 그림 (상태마다 바꿔 끼움, 없으면 예전 색)")]
        [SerializeField] Sprite _claimSprite;
        [SerializeField] Sprite _goSprite;
        [SerializeField] Sprite _doneSprite;
        [SerializeField] RectTransform _stamp;
        [Header("4차 줄 (없어도 됨)")]
        [Tooltip("수선점 기록 단계 점 (왼쪽부터, 14px · 간격 18)")]
        [SerializeField] Image[] _tierPips = Array.Empty<Image>();
        [Tooltip("숨은 기록 힌트 (Hint 20 Lavender)")]
        [SerializeField] TMP_Text _hintText;
        [Tooltip("기능 잠김 알약 (M39 — 첫 주 과제)")]
        [SerializeField] GameObject _lockPill;
        [SerializeField] TMP_Text _lockText;

        static readonly Color ClaimColor = UITheme.Gold;
        static readonly Color GoColor = UITheme.PrimaryButton;
        static readonly Color DoneColor = new(0.36f, 0.31f, 0.52f, 1f);
        static readonly Color IdleColor = new(0.29f, 0.23f, 0.49f, 1f);
        /// <summary>받을 수 있는 줄의 바탕·아이콘 받침에 섞는 금빛 (그림을 단색으로 덮지 않게 옅게)</summary>
        const float ClaimableBackgroundTint = 0.22f;
        const float ClaimablePlateTint = 0.45f;

        MissionStatus _status;
        JournalRowModel _model;
        bool _hasModel;
        Action<MissionRowView> _onButton;
        float _pulse;
        bool _claimable;
        /// <summary>진행 막대 지금 비율 / 목표 (EaseFill)</summary>
        float _fillShown;
        float _fillTarget;
        bool _snapFill = true;
        string _boundId;
        Coroutine _stampAnim;
        Color _backgroundBase = Color.white;
        Color _iconBackBase = Color.white;
        bool _basesRead;

        public MissionStatus Status => _status;

        /// <summary>4차 줄로 묶였는지 (기록 · 이벤트 미션 · 첫 주 — 그때 Status는 null)</summary>
        public bool HasModel => _hasModel;

        /// <summary>4차 줄 내용 (HasModel일 때만 뜻이 있다)</summary>
        public JournalRowModel Model => _model;

        public RectTransform PointsPill => _pointsPill;

        public RectTransform Rect => (RectTransform)transform;

        void Awake()
        {
            _button.onClick.AddListener(() => _onButton?.Invoke(this));
            ReadBases();
        }

        /// <summary>빌더가 정한 기본 색 (아트면 흰색) — 상태는 여기에 금빛을 옅게 섞어서만 바꾼다</summary>
        void ReadBases()
        {
            if (_basesRead) return;
            _basesRead = true;
            if (_background != null) _backgroundBase = _background.color;
            if (_iconBack != null) _iconBackBase = _iconBack.color;
        }

        public void Bind(MissionStatus status, Action<MissionRowView> onButton)
        {
            _status = status;
            _hasModel = false;
            _model = default;
            _onButton = onButton;
            var def = status.Def;
            ShowModelParts(0, 0, null, null);
            if (_pointsPill != null) _pointsPill.gameObject.SetActive(true);

            _text.text = def.text;
            // 다른 미션으로 다시 묶임 → 막대는 미끄러지지 않고 바로 (M62)
            if (_boundId != def.id)
            {
                _boundId = def.id;
                _snapFill = true;
            }
            _fillTarget = status.IsClaimed ? 1f : Mathf.Clamp01(status.Ratio);
            if (_snapFill)
            {
                _snapFill = false;
                _fillShown = _fillTarget;
                MetaUi.SetBar(_progressFill, _fillShown);
            }
            _progressFill.color = status.IsComplete ? UITheme.Gold : UITheme.Lavender;
            long shown = Math.Min(status.Progress, def.target);
            _progressText.text = $"{MetaUi.Int(shown)}/{MetaUi.Int(def.target)}";
            _pointsText.text = $"+{def.points}점";

            bool hasReward = def.rewards != null && def.rewards.Count > 0;
            if (_rewardChip != null)
            {
                _rewardChip.gameObject.SetActive(hasReward);
                if (hasReward) _rewardChip.Set(MetaUi.FirstPreview(def.rewards, out _));
            }

            var sprite = def.icon != null ? def.icon : _icons != null ? _icons.Feature(def.navFeature) : null;
            if (sprite == null && _icons != null) sprite = _icons.Journal;
            _icon.sprite = sprite != null ? sprite : _icons != null ? _icons.Star : null;
            _icon.color = Color.white;
            _icon.preserveAspect = true;

            ReadBases();
            _claimable = status.CanClaim;
            _goldEdge.enabled = _claimable;
            _background.color = _claimable ? Tint(_backgroundBase, ClaimableBackgroundTint) : _backgroundBase;
            _iconBack.color = _claimable ? Tint(_iconBackBase, ClaimablePlateTint) : _iconBackBase;
            _group.alpha = status.IsClaimed ? 0.62f : 1f;
            if (_stampAnim == null) _stamp.gameObject.SetActive(status.IsClaimed);

            if (status.IsClaimed)
            {
                SetButton("완료", _doneSprite, DoneColor, false);
            }
            else if (status.CanClaim)
            {
                SetButton("받기", _claimSprite, ClaimColor, true);
                _buttonLabel.color = UITheme.Ink;
            }
            else if (def.navFeature != FeatureId.Battle || !string.IsNullOrEmpty(def.focusTarget))
            {
                SetButton("가기 ›", _goSprite, GoColor, true);
            }
            else
            {
                SetButton("진행 중", _doneSprite, IdleColor, false);
            }
        }

        /// <summary>
        /// 4차 줄 (수선점 기록 · 이벤트 미션 · 첫 주 과제 — SPEC4 §6.1): 아이콘 · 한 줄 · 진행 막대 + '12,340/100,000' (10만부터 짧은 표기) ·
        /// 점수 알약 (PointsText가 있을 때만) · 보상 칩 · [받기] Gold / [가기 ›] Primary / [완료] / 잠김. 열 때 · 바뀔 때만 부른다
        /// </summary>
        public void Bind(in JournalRowModel model, Action<MissionRowView> onButton)
        {
            _status = null;
            _model = model;
            _hasModel = true;
            _onButton = onButton;

            _text.text = model.Text ?? string.Empty;
            if (_boundId != model.Id)
            {
                _boundId = model.Id;
                _snapFill = true;
            }
            _fillTarget = model.Ratio;
            if (_snapFill)
            {
                _snapFill = false;
                _fillShown = _fillTarget;
                MetaUi.SetBar(_progressFill, _fillShown);
            }
            bool complete = model.State == JournalRowState.Claimable || model.State == JournalRowState.Done;
            _progressFill.color = complete ? UITheme.Gold : UITheme.Lavender;
            long target = Math.Max(0, model.Target);
            long shown = target > 0 ? Math.Min(model.Value, target) : model.Value;
            _progressText.text = target > 0 ? $"{CompactCount(shown)}/{CompactCount(target)}" : CompactCount(shown);

            bool points = !string.IsNullOrEmpty(model.PointsText);
            if (_pointsPill != null) _pointsPill.gameObject.SetActive(points);
            if (points) _pointsText.text = model.PointsText;
            if (_rewardChip != null)
            {
                _rewardChip.gameObject.SetActive(model.Reward != null);
                if (model.Reward != null) _rewardChip.Set(model.Reward);
            }

            var sprite = model.Icon != null ? model.Icon : _icons != null ? _icons.Feature(model.Nav) : null;
            if (sprite == null && _icons != null) sprite = _icons.Journal;
            _icon.sprite = sprite != null ? sprite : _icons != null ? _icons.Star : null;
            _icon.color = Color.white;
            _icon.preserveAspect = true;

            ShowModelParts(model.TierCount, model.TierReached, model.Hint, model.State == JournalRowState.Locked ? model.LockText : null);

            ReadBases();
            _claimable = model.CanClaim;
            _goldEdge.enabled = _claimable;
            _background.color = _claimable ? Tint(_backgroundBase, ClaimableBackgroundTint) : _backgroundBase;
            _iconBack.color = _claimable ? Tint(_iconBackBase, ClaimablePlateTint) : _iconBackBase;
            _group.alpha = model.IsDone ? 0.62f : model.State == JournalRowState.Locked ? 0.8f : 1f;
            if (_stampAnim == null) _stamp.gameObject.SetActive(model.IsDone);

            switch (model.State)
            {
                case JournalRowState.Done:
                    SetButton("완료", _doneSprite, DoneColor, false);
                    break;
                case JournalRowState.Claimable:
                    SetButton("받기", _claimSprite, ClaimColor, true);
                    _buttonLabel.color = UITheme.Ink;
                    break;
                case JournalRowState.Go:
                    SetButton("가기 ›", _goSprite, GoColor, true);
                    break;
                case JournalRowState.Locked:
                    SetButton("잠김", _doneSprite, IdleColor, true);
                    break;
                default:
                    SetButton("진행 중", _doneSprite, IdleColor, false);
                    break;
            }
        }

        /// <summary>진행 숫자 (10만 아래는 쉼표, 그 위는 짧은 표기 — SPEC4 §6.1 FormatCompact)</summary>
        public static string CompactCount(long value) => value < 100_000 ? MetaUi.Int(value) : NumberFormatter.FormatCompact(value);

        /// <summary>4차 줄 부품: 단계 점 · 숨은 기록 힌트 · 잠김 알약 (틀에 없으면 건너뛴다)</summary>
        void ShowModelParts(int tierCount, int tierReached, string hint, string lockText)
        {
            for (int i = 0; i < _tierPips.Length; i++)
            {
                var pip = _tierPips[i];
                if (pip == null) continue;
                bool used = i < tierCount;
                if (pip.gameObject.activeSelf != used) pip.gameObject.SetActive(used);
                if (used) pip.color = i < tierReached ? UITheme.Gold : MetaUi.Alpha(UITheme.SubText, 0.5f);
            }
            if (_hintText != null)
            {
                bool showHint = !string.IsNullOrEmpty(hint);
                if (_hintText.gameObject.activeSelf != showHint) _hintText.gameObject.SetActive(showHint);
                if (showHint) _hintText.text = hint;
            }
            if (_lockPill != null)
            {
                bool locked = lockText != null;
                if (_lockPill.activeSelf != locked) _lockPill.SetActive(locked);
                if (locked && _lockText != null) _lockText.text = lockText;
            }
        }

        /// <summary>기본 색에 금빛을 amount만큼 섞는다 (알파는 기본 그대로)</summary>
        static Color Tint(Color baseColor, float amount) => MetaUi.Alpha(Color.Lerp(baseColor, UITheme.Gold, amount), baseColor.a);

        /// <summary>버튼 상태: 그림이 있으면 그림을 바꿔 끼우고 흰색 그대로, 없으면 예전 색</summary>
        void SetButton(string label, Sprite sprite, Color fallback, bool interactable)
        {
            _buttonLabel.text = label;
            _buttonLabel.color = UITheme.Text;
            if (sprite != null)
            {
                _buttonImage.sprite = sprite;
                _buttonImage.type = Image.Type.Sliced;
                _buttonImage.color = Color.white;
            }
            else
            {
                _buttonImage.color = fallback;
            }
            _button.interactable = interactable;
        }

        /// <summary>받음: "완료" 도장이 위에서 찍힌다</summary>
        public void PlayStamp()
        {
            if (!isActiveAndEnabled) return;
            if (_stampAnim != null) StopCoroutine(_stampAnim);
            _stampAnim = StartCoroutine(Stamp());
        }

        IEnumerator Stamp()
        {
            _stamp.gameObject.SetActive(true);
            const float drop = 0.22f;
            for (float t = 0f; t < drop; t += Time.unscaledDeltaTime)
            {
                float k = MetaUi.EaseInCubic(t / drop);
                _stamp.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, k);
                _stamp.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-30f, -12f, k));
                yield return null;
            }
            _stamp.localScale = Vector3.one;
            _stamp.localRotation = Quaternion.Euler(0f, 0f, -12f);
            const float settle = 0.18f;
            var rect = Rect;
            for (float t = 0f; t < settle; t += Time.unscaledDeltaTime)
            {
                rect.localScale = Vector3.one * (1f - 0.03f * MetaUi.Punch(t / settle));
                yield return null;
            }
            rect.localScale = Vector3.one;
            _stampAnim = null;
            // 4차 줄: 받은 뒤 줄이 다음 단계로 바뀌었으면 (수선점 기록) 도장은 찍힌 뒤 지금 줄 상태를 따른다 — 진행 중 단계에 '완료'가 남지 않게
            if (_status == null && _hasModel) _stamp.gameObject.SetActive(_model.IsDone);
        }

        void Update()
        {
            if (_fillShown != _fillTarget)
            {
                _fillShown = UIMotion.EaseFill(_fillShown, _fillTarget, Time.unscaledDeltaTime);
                MetaUi.SetBar(_progressFill, _fillShown);
            }
            if (!_claimable) return;
            _pulse += Time.unscaledDeltaTime;
            _goldEdge.color = MetaUi.Alpha(UITheme.Gold, 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(_pulse * 4f)));
        }

        void OnEnable()
        {
            // 다시 켜질 때 막대는 바로 목표로 (M62)
            if (_status == null && !_hasModel) return;
            _fillShown = _fillTarget;
            MetaUi.SetBar(_progressFill, _fillShown);
        }

        void OnDisable()
        {
            if (_stampAnim != null) StopCoroutine(_stampAnim);
            _stampAnim = null;
            if (_status != null) _stamp.gameObject.SetActive(_status.IsClaimed);
            else if (_hasModel) _stamp.gameObject.SetActive(_model.IsDone);
            _stamp.localScale = Vector3.one;
            Rect.localScale = Vector3.one;
        }
    }
}
