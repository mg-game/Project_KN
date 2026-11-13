using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 잠긴 전투 HUD 버튼(AUTO / 2배속) 위에 덮는 덮개 (F-03 · M38 · M22). 버튼은 전투 화면 담당 것이라 건드리지 않고 (CanvasGroup도 붙이지 않는다),
    /// 같은 자리에 입력을 받는 불투명 덮개(TabNormal + 안쪽 2px Gold@0.35 테두리 — 아래 AUTO 글자가 비치지 않게) + 자물쇠 + 짧은 "1-2"를 올린다.
    /// 덮개의 누르는 자리는 원래 버튼의 raycastPadding을 그대로 베낀다 (빌더). 누르면 "스테이지 1-2를 클리어하면 열려요" 토스트 +
    /// 막힘 흔들기(자물쇠 · 글자 묶음, UIMotion.DenyOffset) + Deny 소리 + 약한 진동 (M18).
    /// 열리는 순간 자물쇠가 튀어 사라지고 버튼 둘레 빛 테가 2초 숨 쉰 뒤 꺼진다 (메타 HUD 캔버스라 버튼 위에 그려진다 — 가운데가 빈 테라 글자를 덮지 않는다). 아직 안 눌러 본 동안 "NEW" 알약을 단다 —
    /// 처음 누르면(원래 버튼) 봤음 처리되어 사라진다 (§3.13: HUD 토글은 빨간 점 트리에서 빠졌다, 개방 카드 + 이 표시면 충분).
    /// 첫 전투를 넘기지 않는다 (M09): 표시가 붙은 뒤 스테이지가 한 번 끝나면(Battle.StageEnded — 열린 그 끝남은 빼려고 NewMinShow초 뒤부터) 봤음 처리한다.
    /// 개방 카드의 그림이 이 자리에 닿을 때도 봤음 처리된다 (FeatureUnlockCard). NEW 알약은 MAIN 배지 예산(MainBadgeBudget)을 따른다 —
    /// 화면 전체에 NEW는 가장 최근 하나, MAIN 배지 5개 안에서만 (빨간 점 · 시트 상태가 바뀌어도 다시 묻는다).
    /// 게임이 이미 켜 둔 기능(튜토리얼 8단계가 켠 AUTO 등)은 표시를 달지 않고 봤음 처리한다 — 표시를 따라 누르면 꺼져 버리기 때문.
    /// 켜짐 여부는 이벤트가 없어 "새로 열림"인 동안만 0.25초마다 읽는다 (bool 읽기, 할당 없음). 봤음 처리는 Update에서 해서
    /// Features.Changed 처리 도중 다시 Changed를 부르지 않는다.
    /// </summary>
    public class FeatureLockCover : MonoBehaviour
    {
        [SerializeField] FeatureId _feature;
        [Tooltip("덮개 (입력을 받는 반투명 판 + 자물쇠 + 글자)")]
        [SerializeField] GameObject _cover;
        [SerializeField] RectTransform _lockIcon;
        [SerializeField] TMP_Text _hintText;
        [SerializeField] Button _coverButton;
        [Tooltip("덮는 원래 버튼 (누르면 \"새로 열림\" 봤음 처리). 없어도 됨")]
        [SerializeField] Button _target;
        [Tooltip("새로 열림 표시 (배지 오브젝트 — _newBadge가 있으면 그쪽으로 켜고 끈다)")]
        [SerializeField] GameObject _newDot;
        [Tooltip("새로 열림 'NEW' 알약 (SceneParts.BuildBadge, 없으면 _newDot을 켜고 끈다)")]
        [SerializeField] Badge _newBadge;
        [Tooltip("막힘 흔들기를 받는 자물쇠 · 글자 묶음 (없으면 자물쇠를 돌린다)")]
        [SerializeField] RectTransform _content;
        [Tooltip("열린 뒤 2초 숨 쉬는 빛 테 (버튼 위에 그려진다 — 가운데가 빈 GlowFrame이라 둘레만 빛난다, 없어도 됨)")]
        [SerializeField] Image _glow;

        const float AlreadyOnPollInterval = 0.25f;
        /// <summary>표시가 붙은 뒤 이 시간(실시간 초)이 지나서 끝난 스테이지부터 "첫 전투"로 센다 (기능을 연 그 스테이지 끝남은 빼고)</summary>
        const float NewMinShow = 2f;
        /// <summary>열린 뒤 버튼 둘레 빛 테가 숨 쉬는 시간 (§3.13.4 — 개방 카드 + 한 번의 2초 빛)</summary>
        const float UnlockGlowTime = 2f;

        bool _subscribed;
        bool _locked;
        bool _initialized;
        bool _isNew;
        bool _coverDirty;
        /// <summary>스테이지가 끝나 봤음 처리할 차례 (Update에서 — 전투 알림 도중 Features.Changed를 부르지 않게)</summary>
        bool _seenPending;
        /// <summary>"새로 열림" 표시가 붙은 시각 (unscaled, 없으면 −1)</summary>
        float _newSince = -1f;
        float _pollTimer;
        Action<bool> _onSheetStateChanged;
        Action<BattleOutcome, List<RewardData>> _onStageEnded;
        BattleSystem _battle;
        Coroutine _burst;
        Coroutine _deny;

        void Awake()
        {
            if (_coverButton != null) _coverButton.onClick.AddListener(OnCoverClick);
            if (_target != null) _target.onClick.AddListener(OnTargetClick);
        }

        /// <summary>덮는 기능</summary>
        public FeatureId Feature => _feature;

        /// <summary>NEW 알약 후보인지 (열렸고 · 아직 안 가 봄 · 게임이 켜 두지 않음 · 켜진 덮개) — MainBadgeBudget이 읽는다. 할당 없음</summary>
        public bool WantsNewMark =>
            MetaUi.IsActive && isActiveAndEnabled && Game.Features.IsNew(_feature) && !MetaUi.IsFeatureAlreadyOn(_feature);

        void Start()
        {
            if (MetaUi.IsActive)
            {
                Game.Features.Changed += Refresh;
                // NEW 알약은 MAIN 배지 예산을 따른다 — 다른 배지 · 시트가 바뀌어도 다시 묻는다
                Game.RedDots.Changed += RefreshNewMark;
                _onSheetStateChanged = OnSheetStateChanged;
                UIFrame.SheetStateChanged += _onSheetStateChanged;
                _battle = Game.Battle;
                if (_battle != null)
                {
                    _onStageEnded = OnStageEnded;
                    _battle.StageEnded += _onStageEnded;
                }
                MainBadgeBudget.Register(this);
                _subscribed = true;
            }
            Refresh();
        }

        void OnDestroy()
        {
            if (!_subscribed) return;
            _subscribed = false;
            UIFrame.SheetStateChanged -= _onSheetStateChanged;
            MainBadgeBudget.Unregister(this);
            if (_battle != null && _onStageEnded != null) _battle.StageEnded -= _onStageEnded;
            _battle = null;
            if (!Game.IsReady) return;
            if (Game.Features != null) Game.Features.Changed -= Refresh;
            if (Game.RedDots != null) Game.RedDots.Changed -= RefreshNewMark;
        }

        void OnSheetStateChanged(bool open) => RefreshNewMark();

        /// <summary>스테이지가 끝남: 표시가 붙은 지 NewMinShow초가 지났으면 다음 Update에서 봤음 처리 (첫 전투를 넘기지 않는다)</summary>
        void OnStageEnded(BattleOutcome outcome, List<RewardData> rewards)
        {
            if (!_isNew || _locked || _newSince < 0f) return;
            if (Time.unscaledTime - _newSince < NewMinShow) return;
            _seenPending = true;
        }

        void Refresh()
        {
            if (!MetaUi.IsActive)
            {
                _cover.SetActive(false);
                SetNewMark(false);
                return;
            }
            var features = Game.Features;
            bool locked = !features.IsUnlocked(_feature);
            if (_initialized && _locked && !locked && isActiveAndEnabled)
            {
                if (_burst != null) StopCoroutine(_burst);
                _burst = StartCoroutine(UnlockBurst());
            }
            else if (_burst == null)
            {
                // 열림 연출 도중 들어온 다른 Changed(같은 클리어에 바구니도 함께 열림 등)가 덮개를 갑자기 끄지 않게
                _cover.SetActive(locked);
            }
            _locked = locked;
            _initialized = true;
            if (locked && _hintText != null) _hintText.text = features.GetLockedHint(_feature);
            _isNew = !locked && features.IsNew(_feature);
            if (!_isNew)
            {
                _newSince = -1f;
                _seenPending = false;
            }
            else if (_newSince < 0f) _newSince = Time.unscaledTime;
            // 이미 켜진 기능은 표시를 달지 않는다 (봤음 처리는 다음 Update에서). 표시는 MAIN 배지 예산 안에서만
            RefreshNewMark();
            _pollTimer = 0f;
        }

        /// <summary>NEW 알약만 다시 정한다: 새로 열림 · 게임이 켜 두지 않음 · MAIN 배지 예산이 허락 (가장 최근 NEW 하나, MAIN 5개 안)</summary>
        void RefreshNewMark()
        {
            if (!MetaUi.IsActive || !_initialized) return;
            SetNewMark(_isNew && !_locked && !MetaUi.IsFeatureAlreadyOn(_feature) && MainBadgeBudget.AllowsNew(this));
        }

        /// <summary>새로 열림 표시: NEW 알약 배지가 있으면 그것, 없으면 예전 점 오브젝트</summary>
        void SetNewMark(bool on)
        {
            if (_newBadge != null)
            {
                if (on) _newBadge.SetNew(true);
                else if (_newBadge.gameObject.activeSelf) _newBadge.Hide();
                return;
            }
            if (_newDot != null && _newDot.activeSelf != on) _newDot.SetActive(on);
        }

        void Update()
        {
            if (_coverDirty)
            {
                _coverDirty = false;
                if (_burst == null) _cover.SetActive(_locked);
            }
            // "새로 열림"인 동안만: 게임이 기능을 켜 두었으면(튜토리얼 EnableAutoBattle) 봤음 처리 → Changed → Refresh가 점을 끈다
            if (!_isNew || _locked) return;
            if (_seenPending)
            {
                // 첫 전투가 끝났다 — NEW를 다음 전투까지 끌고 가지 않는다
                _seenPending = false;
                _isNew = false;
                SetNewMark(false);
                if (MetaUi.IsActive && Game.Features.IsNew(_feature)) Game.Features.MarkSeen(_feature);
                return;
            }
            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = AlreadyOnPollInterval;
            if (!MetaUi.IsActive || !MetaUi.IsFeatureAlreadyOn(_feature)) return;
            _isNew = false;
            SetNewMark(false);
            if (Game.Features.IsNew(_feature)) Game.Features.MarkSeen(_feature);
        }

        void OnDisable()
        {
            if (_deny != null)
            {
                _deny = null;
                ResetDeny();
            }
            // 열림 연출이 끊기면(코루틴은 꺼질 때 멈춘다) 모양만 되돌리고, 덮개 켜고 끄기는 다시 켜진 뒤 Update에서 한다
            // (꺼지는 도중에는 SetActive를 부르지 않는다)
            if (_burst == null) return;
            _burst = null;
            ResetBurstVisuals();
            _coverDirty = true;
        }

        void OnCoverClick()
        {
            if (!MetaUi.IsActive) return;
            if (!Game.Features.TryUse(_feature, out var message))
            {
                MetaUi.Toast(message);
                Deny();
            }
        }

        /// <summary>막힘 (M07 · M18): 자물쇠 · 글자 묶음을 0.26초 좌우로 흔들고 Deny 소리 + 약한 진동</summary>
        void Deny()
        {
            UISfx.Play(UISfxKey.Deny);
            Haptics.Play(HapticKind.Light);
            if (!isActiveAndEnabled) return;
            if (_deny != null) StopCoroutine(_deny);
            _deny = StartCoroutine(_content != null ? DenyShake() : Shake());
        }

        IEnumerator DenyShake()
        {
            float start = Time.unscaledTime;
            for (float k = 0f; k < 1f; k = (Time.unscaledTime - start) / UIMotion.Deny)
            {
                _content.anchoredPosition = new Vector2(UIMotion.DenyOffset(k), 0f);
                yield return null;
            }
            ResetDeny();
            _deny = null;
        }

        void ResetDeny()
        {
            if (_content != null) _content.anchoredPosition = Vector2.zero;
            if (_lockIcon != null) _lockIcon.localRotation = Quaternion.identity;
        }

        void OnTargetClick()
        {
            if (!MetaUi.IsActive || _locked) return;
            if (Game.Features.IsNew(_feature)) Game.Features.MarkSeen(_feature);
        }

        /// <summary>예전 장면 (묶음 없음): 자물쇠만 돌려 흔든다</summary>
        IEnumerator Shake()
        {
            if (_lockIcon == null)
            {
                _deny = null;
                yield break;
            }
            for (float t = 0f; t < UIMotion.Deny; t += Time.unscaledDeltaTime)
            {
                float k = t / UIMotion.Deny;
                _lockIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 6f) * 14f * (1f - k));
                yield return null;
            }
            _lockIcon.localRotation = Quaternion.identity;
            _deny = null;
        }

        /// <summary>열림: 자물쇠가 커졌다가 사라지고 덮개가 걷힌다</summary>
        IEnumerator UnlockBurst()
        {
            var group = _cover.GetComponent<CanvasGroup>();
            const float duration = 0.5f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                if (_lockIcon != null)
                {
                    _lockIcon.localScale = Vector3.one * (1f + 0.8f * MetaUi.EaseOutCubic(k));
                    _lockIcon.localRotation = Quaternion.Euler(0f, 0f, -20f * k);
                }
                if (group != null) group.alpha = 1f - k;
                yield return null;
            }
            _cover.SetActive(_locked);
            ResetBurstVisuals();
            // 열린 버튼 둘레 빛 테가 2초 숨 쉬고 사라진다 (움직임 줄이기면 없음)
            if (_glow != null && !_locked && !UIMotion.Reduced)
            {
                _glow.enabled = true;
                for (float t = 0f; t < UnlockGlowTime; t += Time.unscaledDeltaTime)
                {
                    float fade = Mathf.Clamp01((UnlockGlowTime - t) / 0.4f);
                    _glow.color = MetaUi.Alpha(UITheme.Lamp, fade * UIMotion.Breathe(t, 0.25f, 0.7f, 0.8f));
                    yield return null;
                }
                _glow.enabled = false;
            }
            _burst = null;
        }

        void ResetBurstVisuals()
        {
            if (_glow != null) _glow.enabled = false;
            var group = _cover.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
            if (_lockIcon != null)
            {
                _lockIcon.localScale = Vector3.one;
                _lockIcon.localRotation = Quaternion.identity;
            }
        }
    }
}
