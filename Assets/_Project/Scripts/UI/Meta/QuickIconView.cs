using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 화면 빠른 아이콘 한 칸 (UX-SPEC-IA §3.3, 104x116 — 누르는 자리는 칸 전체, Button + PressScale은 칸 루트에 있다):
    /// Visual(나타남 연출을 받는 묶음) 안에 Plate 어두운 메달 88 (위 가운데) · Icon 62 (판 가운데) · LabelPill 100x28 (아래 가운데, 이름 20 굵게 Cream) ·
    /// 배지 (판 오른쪽 위 (−4, −4), 판의 RedDotView가 _dotPath를 보여 준다) · 선택 SubPill 64x26 (칸 위로 10 솟음, 'D-n' — Phase 4).
    /// 보이는지는 IsAvailable(메타 켜짐 + 기능 열림 + _afterFtue면 튜토리얼 끝)이고, 자리는 QuickRailView가 정한다 (_topPocket = 위 주머니).
    /// 누르면 그 기능이 새로 열림(IsNew)이면 먼저 본 것으로 표시하고 (Game.Features.MarkSeen — CR-06: 이벤트는 개방 카드가 꺼져 있어
    /// 아무도 본 것으로 표시하지 않았다, 그대로면 NEW가 영영 남는다) 할 일을 한다:
    /// 수선 일지 · 기록 → UINav.OpenJournal · 출석부 → UINav.OpenAttendance (Open — 자동 열기 아님, M69) ·
    /// 이벤트 → UINav.OpenEvents (MetaNavigator가 이벤트 모아 보기를 연다 — 모아 보기가 장면에 없거나 이벤트가 잠겨 있으면 이벤트 상점, 이벤트 길은 한 곳) ·
    /// 그 밖(Phase 4 아이콘) → UINav.GoToFeature.
    /// 더보기 쟁반의 대리 칸도 이 컴포넌트다 (QuickRailView가 복제해 쟁반에 넣는다 — 이름 · 경로가 고정된 원래 칸은 옮기지 않는다, CR-11).
    /// v1: EventButton 이벤트 (Events, 점 top.event, 위 주머니) · AttendanceButton 출석부 (Attendance + FTUE 끝, 점 top.attendance) ·
    /// JournalButton 수선 일지 (Missions, 숫자 top.journal). 이름 · 경로는 고정 (CoachTargets).
    /// 4차: StatsButton 전투 기록 (BattleStats, 점 없음, 오른쪽 위 주머니 R1) → UINav.OpenBattleStats (P4-3b 팝업) ·
    /// 이벤트 칸의 'D-n' SubPill (_subPill — 별이 머문 밤 산책(EventKind.Festival)이 진행 중일 때만, 남은 날 올림, 5초마다 · 글은 n이 바뀔 때만).
    /// 숨겨 두기 (M78 · CR-24 — HoldThenAppear): 새로 열린 아이콘은 자리에 놓인 채(칸 사각형은 그대로 — 개방 카드가 날아올 자리를 읽는다) 그림만 크기 0으로 숨고
    /// 누름도 받지 않다가, 그 기능의 개방 카드가 닫히고 그림이 닿으면(FeatureUnlockCard.IsPresenting이 꺼지면) BadgeAppear로 나타난다.
    /// 카드가 줄에서 기다리는 동안(PopupQueue에 기능 개방 요청이 있음)도 기다리고, 최대 HoldMax초면 그냥 나타난다. 기다리는 동안만 코루틴이 돈다.
    /// </summary>
    public class QuickIconView : MonoBehaviour
    {
        /// <summary>나타남 정점이 1.2(UIMotion.BadgeAppearPeak)가 되는 OutBack s — Badge와 같은 값</summary>
        const float AppearBack = 2.59f;
        /// <summary>개방 카드를 기다리며 숨겨 두는 최대 시간 (카드는 스테이지 사이에만 뜬다 — 그 뒤에는 카드 없이 나타난다)</summary>
        public const float HoldMax = 60f;

        [Tooltip("보이게 하는 기능 (잠겨 있으면 숨는다 · 누르면 본 것으로 표시)")]
        [SerializeField] FeatureId _feature;
        [Tooltip("배지와 사냥 버튼 모아 보기(RedDotAnyView)가 읽는 빨간 점 경로 (RedDotPaths.Top*)")]
        [SerializeField] string _dotPath;
        [Tooltip("위 주머니(1칸)에 놓는다 — 아니면 아래 묶음 (3칸부터 위로)")]
        [SerializeField] bool _topPocket;
        [Tooltip("튜토리얼(FTUE)이 끝나야 보인다 (출석부 — 예전 상단 바 규칙)")]
        [SerializeField] bool _afterFtue;
        [Tooltip("칸 전체 버튼 (비면 같은 오브젝트에서 찾는다)")]
        [SerializeField] Button _button;
        [Tooltip("나타남 연출(0 → 1.2 → 1)을 받는 그림 묶음 — 누름 크기(PressScale)는 칸 루트가 받는다")]
        [SerializeField] RectTransform _visual;
        [Tooltip("'D-n' 알약 (이벤트 칸만, 없어도 됨)")]
        [SerializeField] GameObject _subPill;
        [SerializeField] TMP_Text _subPillText;

        /// <summary>SubPill을 다시 셈하는 간격 (초 — 날이 바뀔 때만 글이 바뀐다)</summary>
        const float SubPillInterval = 5f;
        static readonly List<ActiveEventStatus> s_active = new();
        float _subPillTimer;
        int _subPillDays = -1;

        Coroutine _appear;
        /// <summary>개방 카드를 기다리며 숨어 있는 중</summary>
        bool _held;

        /// <summary>보이게 하는 기능</summary>
        public FeatureId Feature => _feature;

        /// <summary>빨간 점 경로 (없으면 null 또는 빈 글)</summary>
        public string DotPath => _dotPath;

        /// <summary>위 주머니(1칸) 아이콘인지 (화면이 짧으면 QuickRailView가 아래 묶음으로 보낸다)</summary>
        public bool TopPocket => _topPocket;

        /// <summary>지금 보여도 되는지: 메타 켜짐 + 기능 열림 (+ _afterFtue면 튜토리얼 끝). 할당 없음</summary>
        public bool IsAvailable
        {
            get
            {
                if (!MetaUi.IsActive) return false;
                var features = Game.Features;
                if (features == null || !features.IsUnlocked(_feature)) return false;
                if (!_afterFtue) return true;
                var tutorial = Game.Tutorial;
                return tutorial == null || tutorial.IsDone;
            }
        }

        void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button != null) _button.onClick.AddListener(Activate);
        }

        void Update()
        {
            if (_subPill == null) return;
            _subPillTimer -= Time.unscaledDeltaTime;
            if (_subPillTimer > 0f) return;
            _subPillTimer = SubPillInterval;
            RefreshSubPill();
        }

        void OnEnable()
        {
            _subPillTimer = 0f;
            _subPillDays = -1;
        }

        /// <summary>'D-n' 알약: 별이 머문 밤 산책(축제)이 진행 중이면 남은 날(올림, 1 이상), 아니면 숨김. n이 바뀔 때만 글을 쓴다</summary>
        void RefreshSubPill()
        {
            int days = FestivalDaysLeft();
            bool show = days > 0;
            if (_subPill.activeSelf != show) _subPill.SetActive(show);
            if (!show || days == _subPillDays) return;
            _subPillDays = days;
            if (_subPillText != null) _subPillText.text = SubPillText(days);
        }

        /// <summary>'D-{n}' (순수)</summary>
        public static string SubPillText(int days) => $"D-{Math.Max(1, days)}";

        /// <summary>남은 날 (순수): 남은 시간을 하루 단위로 올림 (끝나기 3시간 전 = 1), 끝났으면 0</summary>
        public static int DaysLeft(TimeSpan left) => left <= TimeSpan.Zero ? 0 : Math.Max(1, (int)Math.Ceiling(left.TotalDays - 1e-9));

        static int FestivalDaysLeft()
        {
            if (!MetaUi.IsActive || Game.Events == null || !Game.Events.IsUnlocked) return 0;
            Game.Events.GetActive(s_active);
            for (int i = 0; i < s_active.Count; i++)
            {
                var status = s_active[i];
                if (status != null && status.Kind == EventKind.Festival) return DaysLeft(status.TimeLeft);
            }
            return 0;
        }

        void OnDisable()
        {
            // 꺼지면 코루틴도 멈춘다 — 다음에 켜질 때 반쯤 자란 크기로 남지 않게 (숨겨 두기도 끝 — 다시 켜지면 그냥 보인다)
            _appear = null;
            ReleaseHold();
            if (_visual != null) _visual.localScale = Vector3.one;
        }

        /// <summary>누름: 새로 열림이면 본 것으로 표시한 뒤 그 기능으로 간다 (쟁반 대리 칸도 같은 길)</summary>
        public void Activate()
        {
            if (!MetaUi.IsActive) return;
            var features = Game.Features;
            if (features != null && features.IsNew(_feature)) features.MarkSeen(_feature);
            switch (_feature)
            {
                case FeatureId.Missions:
                case FeatureId.Achievements:
                    UINav.OpenJournal();
                    break;
                case FeatureId.Attendance:
                    UINav.OpenAttendance();
                    break;
                case FeatureId.Events:
                    // 이벤트 모아 보기 (SPEC4 §8.3 · UX-SPEC-IA §8.8 — 받는 쪽 MetaNavigator: 모아 보기가 없으면 이벤트 상점)
                    UINav.OpenEvents();
                    break;
                case FeatureId.BattleStats:
                    // 전투 기록 (CR-03 — 팝업 · 받는 쪽은 전투 화면 P4-3b)
                    UINav.OpenBattleStats();
                    break;
                default:
                    UINav.GoToFeature(_feature);
                    break;
            }
        }

        /// <summary>
        /// 열려서 새로 나타남: 그림 묶음이 0 → 1.2 → 1 (UIMotion.BadgeAppear 0.22초 OutBack, unscaled). 움직임 줄이기면 바로 1.
        /// QuickRailView가 기능이 열린 알림(Features.Changed)에서 부른다 — 처음 놓을 때는 부르지 않는다 (CR-24).
        /// </summary>
        public void PlayAppear()
        {
            if (_visual == null || !isActiveAndEnabled) return;
            if (_appear != null) StopCoroutine(_appear);
            _appear = null;
            ReleaseHold();
            if (UIMotion.Reduced)
            {
                _visual.localScale = Vector3.one;
                return;
            }
            _appear = StartCoroutine(Appear());
        }

        /// <summary>
        /// 열려서 새로 나타날 때 개방 카드가 이 기능 자리로 날아올 차례인지 (M78 · CR-24): 튜토리얼 밖 + 메타 켜짐 +
        /// 이 기능의 카드가 열려 있거나 그림이 날아가는 중, 또는 카드를 보여 주는 기능이고 기능 개방 요청이 줄에서 기다리는 중. 할당 없음
        /// </summary>
        public bool WantsHold()
        {
            if (!MetaUi.IsActive) return false;
            var tutorial = Game.Tutorial;
            if (tutorial != null && tutorial.IsActive) return false;
            return CardPending(_feature);
        }

        static bool CardPending(FeatureId feature)
        {
            if (FeatureUnlockCard.IsPresenting(feature)) return true;
            // 카드 간격을 기다리며 발표자 곁에 비켜 선 카드도 '줄에서 기다림'이다 (SPEC5 §2.8)
            if (MetaPopupPresenter.IsCardParked(feature)) return true;
            var def = Game.Features != null ? Game.Features.GetDef(feature) : null;
            return def != null && def.showCard && Game.Popups != null && Game.Popups.Contains(MetaPopupKind.FeatureUnlock);
        }

        /// <summary>
        /// 새로 열렸지만 개방 카드가 날아올 때까지 숨겨 둔다: 칸 자리는 그대로(카드가 읽는다), 그림 크기 0 · 누름 끔.
        /// 카드가 닫히고 그림이 닿으면(또는 카드를 건너뛰면 · HoldMax가 지나면) BadgeAppear로 나타난다. QuickRailView가 부른다
        /// </summary>
        public void HoldThenAppear()
        {
            if (_visual == null || !isActiveAndEnabled)
            {
                PlayAppear();
                return;
            }
            if (_appear != null) StopCoroutine(_appear);
            _held = true;
            _visual.localScale = Vector3.zero;
            if (_button != null) _button.interactable = false;
            _appear = StartCoroutine(Hold());
        }

        IEnumerator Hold()
        {
            float until = Time.unscaledTime + HoldMax;
            // 한 프레임은 꼭 기다린다 — 같은 프레임에 열린 기능의 카드 요청이 줄에 들어가는 순서를 타지 않게
            yield return null;
            while (_held && Time.unscaledTime < until && CardPending(_feature)) yield return null;
            _appear = null;
            PlayAppear();
        }

        void ReleaseHold()
        {
            if (!_held) return;
            _held = false;
            if (_button != null) _button.interactable = true;
        }

        IEnumerator Appear()
        {
            float start = Time.unscaledTime;
            _visual.localScale = Vector3.zero;
            while (true)
            {
                float k = (Time.unscaledTime - start) / UIMotion.BadgeAppear;
                if (k >= 1f) break;
                _visual.localScale = Vector3.one * UIMotion.OutBack(k, AppearBack);
                yield return null;
            }
            _visual.localScale = Vector3.one;
            _appear = null;
        }
    }
}
