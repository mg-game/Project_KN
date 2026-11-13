using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 강화 탭 "친구 레벨" 카드 (212x196, M34): 초상화 + 'Lv.N' 받침 · 이름 · 별 · 전투력 증가(+N) · 레벨업 버튼.
    ///   · 전투력 칸: 편성된 계보는 레벨업하면 오르는 팀 전투력 (아이콘 + '+312', PositiveInk), 편성 밖은 '미편성' (InkSub), 최대 레벨은 'MAX'.
    ///     값은 Set()에서만 계산한다 (단추가 바뀔 때마다 하지 않는다 — PowerSystem.PreviewLevelUp은 할당이 없다).
    ///   · 레벨업 버튼은 비용 세 상태 (CostButtonView: 넉넉함 / 부족함 — 누르면 흔들기 + 단추 부족 토스트 / 최대 레벨).
    ///     길게 누르면 연속 레벨업 (HoldRepeatButton 0.45초 — 목록을 옆으로 끌면 멈춘다. 반복은 토스트 없이, 막히면 한 번).
    ///   · 다른 화면에서 데려오면(GrowthPanel.FocusLineage) 금빛 테 + 펀치로 잠깐 알려 준다.
    ///   · 잠김 (F-03, SPEC4 §5.1 — 친구 레벨업 CharacterLevelUp은 1-1 클리어로 열린다): 버튼은 그대로 보이고 CostButtonView 쓸 수 없음 +
    ///     비용 줄 자리에 '1-1' 알약 (Micro 18 Gold, 밤 알약), 길게 누르기 끔. 누르면 (버튼 위 투명 LockHit) Error 토스트 '스테이지 1-1을 클리어하면 열려요'
    ///     + 약한 진동 — 레벨은 그대로. 다시 빌드하기 전 씬(알약 · LockHit 없음)이면 버튼만 쓸 수 없게 둔다 (시스템 거절은 CharacterSystem, P4-1).
    /// </summary>
    public class GrowthLineageCardView : MonoBehaviour
    {
        /// <summary>전투력 칸 아이콘 자리 (아이콘이 보일 때 글자를 이만큼 오른쪽으로)</summary>
        const float GainIconSpace = 24f;

        [SerializeField] FormPortraitView _portrait;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _starsText;
        [Tooltip("초상화 아래 받침 위 'Lv.N'")]
        [SerializeField] TMP_Text _levelText;
        [SerializeField] Button _levelUpButton;
        [SerializeField] TMP_Text _buttonLabel;
        [SerializeField] TMP_Text _costText;
        [SerializeField] GameObject _costRow;
        [Tooltip("레벨업하면 오르는 팀 전투력 '+312' / '미편성' / 'MAX'")]
        [SerializeField] TMP_Text _gainText;
        [SerializeField] GameObject _gainIcon;
        [Tooltip("데려왔을 때 잠깐 켜는 금빛 테")]
        [SerializeField] GameObject _focusRing;
        [SerializeField] CostButtonView _costView;
        [SerializeField] HoldRepeatButton _hold;
        [Header("잠김 (F-03)")]
        [Tooltip("비용 줄 자리의 '1-1' 알약 (잠겼을 때만)")]
        [SerializeField] GameObject _lockPill;
        [SerializeField] TMP_Text _lockPillText;
        [Tooltip("잠겼을 때 레벨업 버튼을 덮는 투명 누르기 (잠김 토스트)")]
        [SerializeField] Button _lockHit;

        string _lineageId;
        double _cost;
        bool _isMax;
        bool _locked;
        Coroutine _focus;

        /// <summary>친구 레벨업이 아직 잠겼는지 (메타가 꺼져 있으면 잠그지 않는다 — 예전과 같다)</summary>
        internal static bool LevelUpLocked => Game.IsMetaActive && Game.Features != null && !Game.Features.IsUnlocked(FeatureId.CharacterLevelUp);

        void Awake()
        {
            _levelUpButton.onClick.AddListener(LevelUp);
            if (_hold == null) _hold = _levelUpButton.GetComponent<HoldRepeatButton>();
            if (_hold != null)
            {
                _hold.Step = StepLevelUp;
                _hold.Denied += OnHoldDenied;
            }
            if (_focusRing != null) _focusRing.SetActive(false);
            if (_lockHit != null) _lockHit.onClick.AddListener(DenyLocked);
            if (_lockPill != null) _lockPill.SetActive(false);
            if (_lockHit != null) _lockHit.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            _focus = null;
            transform.localScale = Vector3.one;
            if (_focusRing != null) _focusRing.SetActive(false);
        }

        public void Set(LineageDef lineage)
        {
            _lineageId = lineage.id;
            var characters = Game.Characters;
            int level = characters.GetLevel(lineage.id);
            _isMax = level >= characters.MaxLevel;
            _cost = _isMax ? 0 : characters.GetLevelUpCost(lineage.id);
            _locked = !_isMax && LevelUpLocked;

            _portrait.SetLineage(lineage);
            _nameText.text = lineage.name;
            _starsText.text = UITheme.Stars(lineage.grade);
            // 양피지 위 별 (M83): 등급 색을 Ink 쪽으로 35% (금 · 하늘색 별이 크림 바탕에서 흐려지지 않게)
            _starsText.color = Color.Lerp(UITheme.GradeColor(lineage.grade), UITheme.Ink, 0.35f);
            _levelText.text = $"Lv.{level}";
            SetGain(lineage.id);

            _buttonLabel.text = "레벨업";
            _costText.text = NumberFormatter.FormatCurrency(_cost);
            if (_costView != null)
            {
                if (_isMax) _costView.SetState(CostButtonView.State.Unavailable, "최대 레벨");
                else if (_locked) _costView.SetState(CostButtonView.State.Unavailable, "레벨업");
                else _costView.SetAffordable(Game.Currency.Has(CurrencyType.Gold, _cost));
            }
            else
            {
                _buttonLabel.text = _isMax ? "최대 레벨" : "레벨업";
                _costRow.SetActive(!_isMax && !_locked);
                _levelUpButton.interactable = !_locked;
                RefreshAffordable(Game.Currency.Get(CurrencyType.Gold));
            }
            ApplyLock();
        }

        /// <summary>잠김 모양: '1-1' 알약 · 덮는 누르기 · 길게 누르기 (바뀔 때만 켜고 끈다)</summary>
        void ApplyLock()
        {
            if (_hold != null && _hold.enabled == _locked) _hold.enabled = !_locked;
            if (_lockPill != null)
            {
                if (_locked && _lockPillText != null) _lockPillText.text = LockHint();
                if (_lockPill.activeSelf != _locked) _lockPill.SetActive(_locked);
            }
            if (_lockHit != null && _lockHit.gameObject.activeSelf != _locked) _lockHit.gameObject.SetActive(_locked);
        }

        /// <summary>알약 글 '1-1' (콘텐츠 잠금 글, 없으면 1-1)</summary>
        static string LockHint()
        {
            string hint = Game.Features != null ? Game.Features.GetLockedHint(FeatureId.CharacterLevelUp) : null;
            return string.IsNullOrEmpty(hint) ? BattleSystem.FormatStage(1) : hint;
        }

        /// <summary>잠긴 레벨업을 눌렀다: Error 토스트 '스테이지 1-1을 클리어하면 열려요' + 약한 진동 (레벨 · 단추는 그대로)</summary>
        void DenyLocked()
        {
            string text = Game.Features != null ? Game.Features.GetRequirementText(FeatureId.CharacterLevelUp) : null;
            if (string.IsNullOrEmpty(text)) text = MetaText.UnlockSentence(2);
            UIManager.Instance.ShowToast(text, ToastKind.Error);
            Haptics.Play(HapticKind.Light);
        }

        /// <summary>전투력 칸: 편성된 계보 = '+N' (아이콘과 함께), 편성 밖 = '미편성', 최대 레벨 = 'MAX'</summary>
        void SetGain(string lineageId)
        {
            if (_gainText == null) return;
            bool showIcon = false;
            if (_isMax)
            {
                _gainText.text = "MAX";
                _gainText.color = UITheme.InkSub;
            }
            else if (!Game.Formation.IsLineagePlaced(lineageId))
            {
                _gainText.text = "미편성";
                _gainText.color = UITheme.InkSub;
            }
            else
            {
                long gain = Game.Power != null ? Game.Power.PreviewLevelUp(lineageId) : 0;
                _gainText.text = "+" + PowerSystem.Format(gain);
                _gainText.color = UITheme.PositiveInk;
                showIcon = true;
            }
            if (_gainIcon != null && _gainIcon.activeSelf != showIcon) _gainIcon.SetActive(showIcon);
            var rect = _gainText.rectTransform;
            float left = showIcon && _gainIcon != null ? GainIconSpace : 0f;
            if (!Mathf.Approximately(rect.offsetMin.x, left)) rect.offsetMin = new Vector2(left, rect.offsetMin.y);
        }

        /// <summary>단추 변화마다 비용 상태만 바꾼다 (매 프레임 호출될 수 있어 가볍게 — 같은 상태면 아무것도 안 한다)</summary>
        public void RefreshAffordable(double gold)
        {
            if (_isMax || _locked) return;
            bool affordable = gold >= _cost;
            if (_costView != null) _costView.SetAffordable(affordable);
            else _costText.color = affordable ? UITheme.Text : UITheme.CostShortText;
        }

        void LevelUp()
        {
            if (_lineageId == null) return;
            if (_isMax)
            {
                UIManager.Instance.ShowToast("최대 레벨이에요.");
                return;
            }
            if (_locked)
            {
                DenyLocked();
                return;
            }
            if (!Game.Characters.TryLevelUp(_lineageId))
            {
                Deny();
                return;
            }
            if (_costView != null) _costView.Spent();
        }

        /// <summary>길게 누르기 반복 한 번 — 토스트 없이 (막히면 false → 누르기 끝 + Denied 한 번)</summary>
        bool StepLevelUp()
        {
            if (_lineageId == null || _isMax || _locked || !Game.Characters.TryLevelUp(_lineageId)) return false;
            if (_costView != null) _costView.Spent();
            return true;
        }

        void OnHoldDenied()
        {
            if (_isMax) UIManager.Instance.ShowToast("최대 레벨이에요.");
            else if (_locked) DenyLocked();
            else Deny();
        }

        void Deny()
        {
            if (_costView != null) _costView.Deny();
            UIManager.Instance.ShowShortfall(CurrencyType.Gold, Game.Characters.GetLevelUpCost(_lineageId));
        }

        /// <summary>데려왔을 때: 금빛 테를 seconds 동안 켜고 한 번 툭 튄다 (움직임 줄이기면 테만)</summary>
        public void PlayFocus(float seconds)
        {
            if (!isActiveAndEnabled) return;
            MenuFx.Restart(this, ref _focus, FocusRoutine(seconds));
        }

        IEnumerator FocusRoutine(float seconds)
        {
            if (_focusRing != null) _focusRing.SetActive(true);
            float elapsed = 0f;
            if (!UIMotion.Reduced)
            {
                for (float t = 0f; t < UIMotion.PunchDuration; t += Time.unscaledDeltaTime)
                {
                    float k = t / UIMotion.PunchDuration;
                    transform.localScale = Vector3.one * (1f + UIMotion.PunchM * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                    yield return null;
                }
                transform.localScale = Vector3.one;
                elapsed = UIMotion.PunchDuration;
            }
            for (; elapsed < seconds; elapsed += Time.unscaledDeltaTime) yield return null;
            if (_focusRing != null) _focusRing.SetActive(false);
            _focus = null;
        }
    }
}
