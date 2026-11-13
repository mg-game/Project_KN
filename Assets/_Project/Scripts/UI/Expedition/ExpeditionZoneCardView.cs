using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 원정 구역 카드. 상태: 잠김 / 보내기 / 진행 중(멤버·남은 시간·진행 바) / 완료(빛나는 "받기").
    ///   · 잠김 (M40A · M39): 그림을 (0.55, 0.52, 0.62)로 흐리게 + '스테이지 2-9 클리어 시 / 열려요' (MetaText.UnlockShort, 도달 기준).
    ///   · 보내기 (M57): 지난번 친구 그대로 보낼 수 있으면(ExpeditionSystem.CanResend) 버튼이 '다시 보내기' — 누르면 바로 출발(TryResend),
    ///     아래 '친구 바꾸기 ›'는 지난 친구가 채워진 준비 팝업을 연다. 한 명이라도 못 가면 보통 '보내기' (준비 팝업).
    ///     버튼 이름 · 자리는 그대로 SendButton (코치 ExpeditionStart).
    ///   · 진행 중 · 완료 (IDLE-04): 기본보다 긴 단계로 보냈으면 그림 왼쪽 위에 단계 칩 '길게' / '밤새' (밤 알약 96x32 — 다시 빌드한 씬부터).
    /// </summary>
    public class ExpeditionZoneCardView : MonoBehaviour
    {
        enum State
        {
            Locked,
            Ready,
            Running,
            Done,
        }

        /// <summary>잠긴 구역 그림 색 (흐린 보라 회색 — 덮개는 옅게 두고 그림이 흐림을 맡는다)</summary>
        static readonly Color LockedArtTint = new(0.55f, 0.52f, 0.62f, 1f);
        const string SendLabel = "보내기";
        const string ResendLabel = "다시 보내기";

        [SerializeField] Image _art;
        [SerializeField] TMP_Text _artFallbackText;
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _infoText;
        [SerializeField] RectTransform _dropContent;
        [SerializeField] RewardSlotView _dropPrefab;

        [Header("상태")]
        [SerializeField] GameObject _lockedGroup;
        [SerializeField] TMP_Text _lockText;
        [SerializeField] GameObject _dim;
        [SerializeField] GameObject _readyGroup;
        [SerializeField] Button _sendButton;
        [Tooltip("보내기 버튼 이름 ('보내기' / '다시 보내기')")]
        [SerializeField] TMP_Text _sendLabel;
        [Tooltip("'친구를 골라 출발해요' 안내 (보통 보내기일 때)")]
        [SerializeField] GameObject _sendHint;
        [Tooltip("'친구 바꾸기 ›' 링크 (다시 보내기일 때 — 지난 친구가 채워진 준비 팝업)")]
        [SerializeField] Button _changeButton;
        [SerializeField] GameObject _runningGroup;
        [SerializeField] FormPortraitView[] _memberPortraits;
        [SerializeField] TMP_Text _timeText;
        [SerializeField] Image _progressFill;
        [SerializeField] Button _cancelButton;
        [SerializeField] GameObject _doneGroup;
        [SerializeField] Button _claimButton;
        [SerializeField] Image _flash;
        [Tooltip("다녀올 시간 단계 칩 '길게' / '밤새' (진행 중 · 완료, 기본 단계면 숨김 — IDLE-04)")]
        [SerializeField] GameObject _tierChip;
        [SerializeField] TMP_Text _tierChipText;

        ExpeditionPanel _panel;
        ExpeditionZoneDef _zone;
        UIListPool<RewardSlotView> _drops;
        State _state;
        bool _resend;
        bool _sendKnown;
        bool _hasArt;
        Color _artColor = Color.white;
        long _shownSeconds = -1;
        int _shownTier = -1;
        Coroutine _flashRoutine;

        public ExpeditionZoneDef Zone => _zone;
        public Sprite Art => _art.sprite;
        public RectTransform Rect => (RectTransform)transform;

        void Awake()
        {
            _drops = new UIListPool<RewardSlotView>(_dropPrefab, _dropContent);
            _sendButton.onClick.AddListener(OnSend);
            if (_changeButton != null) _changeButton.onClick.AddListener(() => _panel.OpenSetup(this));
            _claimButton.onClick.AddListener(() => _panel.Claim(_zone));
            _cancelButton.onClick.AddListener(() => _panel.ConfirmCancel(_zone));
            if (_flash != null) _flash.gameObject.SetActive(false);
        }

        public void Init(ExpeditionPanel panel) => _panel = panel;

        public void Set(ExpeditionZoneDef zone, Sprite art, Color fallbackColor)
        {
            _zone = zone;
            _hasArt = art != null;
            _artColor = _hasArt ? Color.white : fallbackColor;
            _art.sprite = art;
            _artFallbackText.gameObject.SetActive(!_hasArt);
            _artFallbackText.text = zone.place;

            _titleText.text = string.IsNullOrEmpty(zone.place) ? zone.name : $"{zone.name} · {zone.place}";
            _infoText.text = $"소요 {MenuText.Minutes(zone.durationMinutes)} · 최대{UIFormat.Nbsp}{zone.maxMembers}명";
            FillDrops();
            Refresh();
        }

        /// <summary>원정 상태가 바뀌었을 수 있을 때 (이벤트, 1초 틱)</summary>
        public void Refresh()
        {
            if (_zone == null) return;
            var expedition = Game.Expedition;
            var active = expedition.GetActive(_zone.id);
            var state = active != null
                ? (expedition.IsComplete(active) ? State.Done : State.Running)
                : (expedition.IsZoneUnlocked(_zone.id) ? State.Ready : State.Locked);
            bool changed = state != _state;
            _state = state;

            _lockedGroup.SetActive(_state == State.Locked);
            _dim.SetActive(_state == State.Locked);
            _readyGroup.SetActive(_state == State.Ready);
            _runningGroup.SetActive(_state == State.Running);
            _doneGroup.SetActive(_state == State.Done);

            // 잠긴 구역은 그림을 흐리게 (색 원 자리표시는 그 색을 같은 만큼 흐리게)
            var artColor = _state == State.Locked ? _artColor * LockedArtTint : _artColor;
            if (_art.color != artColor) _art.color = artColor;

            if (_state == State.Locked) _lockText.text = LockLabel(_zone.requiredStage);
            if (_state == State.Ready) RefreshSend();
            if (_state == State.Running || _state == State.Done) ShowMembers(active);
            RefreshTierChip(_state == State.Running || _state == State.Done ? active.durationTier : 0);
            if (_state == State.Running)
            {
                if (changed) _shownSeconds = -1;
                UpdateTimer(active);
            }
        }

        /// <summary>보내기 / 다시 보내기 (지난 친구가 모두 지금 보낼 수 있고 자리가 있을 때만 다시 보내기 — 일부만 몰래 보내지 않는다)</summary>
        void RefreshSend()
        {
            bool resend = Game.Expedition.CanResend(_zone.id);
            if (_sendKnown && resend == _resend) return;
            _sendKnown = true;
            _resend = resend;
            if (_sendLabel != null) _sendLabel.text = resend ? ResendLabel : SendLabel;
            if (_sendHint != null) _sendHint.SetActive(!resend);
            if (_changeButton != null) _changeButton.gameObject.SetActive(resend);
        }

        /// <summary>단계 칩 (기본 단계 · 보내지 않음이면 숨김). 바뀔 때만 쓴다</summary>
        void RefreshTierChip(int tier)
        {
            if (_tierChip == null || tier == _shownTier) return;
            _shownTier = tier;
            bool show = tier > 0;
            if (_tierChip.activeSelf != show) _tierChip.SetActive(show);
            if (show && _tierChipText != null) _tierChipText.text = ExpeditionSetupPopup.TierLabel(tier);
        }

        void OnSend()
        {
            if (_panel == null || _zone == null) return;
            // 누르는 순간 다시 확인 (그사이 친구가 다른 원정에 나갔을 수 있다)
            if (Game.Expedition.CanResend(_zone.id)) _panel.Resend(this);
            else _panel.OpenSetup(this);
        }

        /// <summary>
        /// 잠긴 구역 안내 '스테이지 2-9 클리어 시 / 열려요' (MetaText.UnlockShort와 같은 도달 기준 — 열리는 스테이지의 앞 스테이지를 깨면).
        /// 좁은 알약이라 스테이지 번호 '2-9'는 줄이 갈리지 않게 묶는다 (M28).
        /// </summary>
        internal static string LockLabel(int requiredStage) =>
            requiredStage <= 1 ? string.Empty : MenuText.StageKeep(requiredStage - 1) + " 클리어 시\n열려요";

        /// <summary>1초마다: 남은 시간/진행 바 갱신. 진행 중 → 완료로 바뀌면 true</summary>
        public bool Tick()
        {
            if (_zone == null) return false;
            if (_state == State.Ready)
            {
                // 다른 구역이 끝나 자리가 나거나 친구가 돌아오면 '다시 보내기'가 될 수 있다 (바뀔 때만 글을 쓴다)
                RefreshSend();
                return false;
            }
            if (_state != State.Running) return false;
            var active = Game.Expedition.GetActive(_zone.id);
            if (active == null || Game.Expedition.IsComplete(active))
            {
                Refresh();
                return true;
            }
            UpdateTimer(active);
            return false;
        }

        /// <summary>획득처 이동 후 시선을 끄는 반짝임</summary>
        public void Highlight()
        {
            if (_flash == null) return;
            MenuFx.Restart(this, ref _flashRoutine, FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            yield return MenuFx.Flash(_flash, new Color(UITheme.Lamp.r, UITheme.Lamp.g, UITheme.Lamp.b, 0.45f), 3, 0.45f);
            _flashRoutine = null;
        }

        void OnDisable()
        {
            if (_flash != null) _flash.gameObject.SetActive(false);
            _flashRoutine = null;
        }

        void FillDrops()
        {
            int dropCount = _zone.drops != null ? _zone.drops.Count : 0;
            bool hasGold = _zone.goldReward > 0;
            _drops.SetCount(dropCount + (hasGold ? 1 : 0));
            int index = 0;
            for (int i = 0; i < dropCount; i++)
            {
                var drop = _zone.drops[i];
                var def = Game.Items.Get(drop.itemId);
                if (def == null) continue;
                _drops[index].SetItem(def, DropLabel(drop));
                _drops[index].SetItemInfoClick();
                index++;
            }
            if (hasGold)
            {
                // 작은 칩은 줄임 표기 (M30 — 3,000 → 3K)
                _drops[index].SetCurrency(CurrencyType.Gold, RewardService.FormatAmountCompact(CurrencyType.Gold, _zone.goldReward));
                _drops[index].SetClick(null);
                index++;
            }
            _drops.SetCount(index);
        }

        void ShowMembers(ExpeditionSave active)
        {
            for (int i = 0; i < _memberPortraits.Length; i++)
            {
                bool has = i < active.lineageIds.Count;
                var lineage = has ? Game.Characters.Db.GetLineage(active.lineageIds[i]) : null;
                _memberPortraits[i].gameObject.SetActive(lineage != null);
                if (lineage != null) _memberPortraits[i].SetLineage(lineage);
            }
        }

        void UpdateTimer(ExpeditionSave active)
        {
            var remaining = Game.Expedition.GetRemaining(active);
            long seconds = (long)remaining.TotalSeconds;
            // 초가 바뀔 때만 글을 다시 쓴다
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _timeText.text = UIFormat.ClockMono(remaining);
            }
            // 캡슐 막대 그림이라 Filled 대신 너비(anchorMax.x)로 채운다 (틀 안쪽 여백은 빌더 값 그대로)
            float progress = Mathf.Clamp01(Game.Expedition.GetProgress(active));
            var fill = _progressFill.rectTransform;
            fill.anchorMax = new Vector2(progress, fill.anchorMax.y);
            _progressFill.enabled = progress > 0f;
        }

        /// <summary>예) 확률 드롭 "30%", 고정 "×2", 범위 "1~3" (작은 칩 — 곱하기는 작게, M30)</summary>
        public static string DropLabel(ExpeditionDrop drop)
        {
            if (drop.chance < 1f) return $"{Mathf.RoundToInt(drop.chance * 100f)}%";
            return drop.min == drop.max ? UIFormat.TimesSmall + drop.min : $"{drop.min}~{drop.max}";
        }
    }
}
