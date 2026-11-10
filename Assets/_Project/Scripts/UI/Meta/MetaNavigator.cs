using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 메타 이동 처리기. UINav.FeatureRequested / JournalRequested / AttendanceRequested / IdleChestRequested를 받아
    /// 기능을 탭·팝업으로 바꾼다 (가이드 퀘스트 줄, 미션 [가기], 개방 카드 [가 보기], 빠른 아이콘). 잠긴 기능이면 토스트만 띄운다.
    ///   탭 기능 → GoToTab (열린 팝업은 UIManager.CloseAll로 닫는다 — 시트가 열리거나 바뀐다, 닫지는 않는다), 수선 일지·기록 → 일지, 출석부 → 출석부,
    ///   바구니·빠른 수선 → 바구니, 던전 → GoToSource(Dungeon), 시너지 → 편성 탭 + 시너지 도감,
    ///   진화 → 친구들 탭 + 그 친구 상세의 진화 기억도 쪽 · 의상 → 의상 쪽 (M73 — PickLineage: 그 점이 켜진 첫 친구 → 짝짝이 → 처음 가진 친구, 없으면 탭만),
    ///   도감 → 친구들 탭에 머문다 (상세를 열지 않는다),
    ///   이벤트 → 이벤트 상점 (GoToSource(Shop, ShopDatabase.EventShopId) — 빠른 아이콘과 같은 길, 새로 열림은 본 것으로, CR-06),
    ///   자동 전투·2배속 → HUD 버튼을 손가락으로 가리킴, 전투 → 아무것도 하지 않음.
    /// 항상 켜진 오브젝트에 붙는다.
    /// "가기"(가이드 퀘스트 줄·일지 [가기])는 GoAndFocus를 쓴다 — 이동이 팝업을 열면 손가락을 팝업 안 버튼으로 옮기고,
    /// 대상이 전투 화면인데 열린 시트 밑에 가리면 먼저 MAIN으로 내린다 (UX-SPEC-IA §5.6).
    /// 개방 카드가 날아갈 자리는 FeatureSlot (M78 — 시트가 열려 있으면 빠른 아이콘 · 덱 띠 대신 사냥 버튼).
    /// 4차 받는 쪽 (SPEC4 §2.6, RM-2 P4-6): 챕터 지도(UINav.ChapterMapRequested → ChapterMapPopup.Show) · 도감(CodexRequested → CodexPopup.Show) ·
    /// 이벤트(EventsRequested → EventHubPopup, 없거나 잠겼으면 이벤트 상점) · 수첩(SeasonRequested → SeasonPassPopup, 스트레치) ·
    /// 인트로 다시 보기(IntroReplayRequested → StoryIntroView.Replay — 보상 · 플래그 없음, 튜토리얼 중에는 아무것도 하지 않는다). 4차 팝업은 [SerializeField] 없이
    /// UIManager.TryGet으로 찾고, 팝업이 장면에 있을 때만 구독한다 (Start) — 장면을 다시 만들기 전(G3)에는 UINav.HasChapterMap이 false로 남아
    /// 전투 화면이 예전 스테이지 목록을 쓴다 (S-7). 지도는 메타가 꺼져 있어도 연다 (튜토리얼 중에는 열지 않는다).
    /// </summary>
    public class MetaNavigator : MonoBehaviour
    {
        const string GuideLineage = "jjakjjak";
        /// <summary>전투 영역 경로 머리 (IdleSceneBuilder 이름, 고정 — §5.8)</summary>
        const string StageAreaPrefix = "SafeArea/StageArea/";

        [SerializeField] JournalPopup _journal;
        [SerializeField] AttendancePopup _attendance;
        [SerializeField] IdleChestPopup _idleChest;

        static MetaNavigator s_instance;

        /// <summary>이번 이동(OnFeature)이 연 메타 팝업 (없으면 null)</summary>
        UIPopup _opened;
        /// <summary>챕터 지도 (장면에 있을 때만 — Start에서 UIManager.TryGet)</summary>
        ChapterMapPopup _chapterMap;
        bool _chapterMapSubscribed;
        /// <summary>도감 (장면에 있을 때만)</summary>
        CodexPopup _codex;
        bool _codexSubscribed;
        /// <summary>별빛 산책 수첩 (장면에 있을 때만 — 스트레치)</summary>
        SeasonPassPopup _season;
        bool _seasonSubscribed;
        /// <summary>동화 인트로 (튜토리얼 UI — 꺼져 있어도 찾는다, Start에서 한 번)</summary>
        StoryIntroView _intro;
        bool _introSubscribed;
        Transform _canvas;
        Transform _popupRoot;

        void Awake()
        {
            s_instance = this;
            UINav.FeatureRequested += OnFeature;
            UINav.JournalRequested += OpenJournal;
            UINav.AttendanceRequested += OpenAttendance;
            UINav.IdleChestRequested += OpenIdleChest;
            UINav.EventsRequested += OpenEvents;
        }

        void Start()
        {
            // 4차 팝업은 장면에 있을 때만 받는다 (UIManager.Awake가 팝업 목록을 들고 있다 — Start는 모든 Awake 뒤)
            var ui = UIManager.Instance;
            if (ui != null && ui.TryGet(out _chapterMap))
            {
                UINav.ChapterMapRequested += OpenChapterMap;
                _chapterMapSubscribed = true;
            }
            if (ui != null && ui.TryGet(out _codex))
            {
                UINav.CodexRequested += OpenCodex;
                _codexSubscribed = true;
            }
            if (ui != null && ui.TryGet(out _season))
            {
                UINav.SeasonRequested += OpenSeason;
                _seasonSubscribed = true;
            }
            _intro = FindAnyObjectByType<StoryIntroView>(FindObjectsInactive.Include);
            if (_intro != null)
            {
                UINav.IntroReplayRequested += ReplayIntro;
                _introSubscribed = true;
            }
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            UINav.FeatureRequested -= OnFeature;
            UINav.JournalRequested -= OpenJournal;
            UINav.AttendanceRequested -= OpenAttendance;
            UINav.IdleChestRequested -= OpenIdleChest;
            UINav.EventsRequested -= OpenEvents;
            if (_chapterMapSubscribed) UINav.ChapterMapRequested -= OpenChapterMap;
            _chapterMapSubscribed = false;
            if (_codexSubscribed) UINav.CodexRequested -= OpenCodex;
            _codexSubscribed = false;
            if (_seasonSubscribed) UINav.SeasonRequested -= OpenSeason;
            _seasonSubscribed = false;
            if (_introSubscribed) UINav.IntroReplayRequested -= ReplayIntro;
            _introSubscribed = false;
        }

        /// <summary>도감 (tab = 처음 탭). 메타가 꺼져 있으면 아무것도 하지 않는다 — 잠겨 있어도 연다 (기록은 처음부터 모이고 받기만 막힌다, SPEC4 §2.11 #8)</summary>
        void OpenCodex(CodexCategory? tab)
        {
            if (_codex == null || !MetaUi.IsActive || Game.Codex == null) return;
            _codex.Show(tab);
        }

        /// <summary>별빛 산책 수첩 (스트레치 — 콘텐츠 칸이 없거나 이벤트가 잠겨 있으면 토스트만)</summary>
        void OpenSeason()
        {
            if (_season == null || !MetaUi.IsActive) return;
            var season = Game.Season;
            if (season == null || !season.IsUnlocked)
            {
                MetaUi.Toast("별빛 산책 수첩은 곧 열려요");
                return;
            }
            _season.Open();
        }

        /// <summary>
        /// 동화 인트로 다시 보기 (설정 — SET-01, SPEC4 §2.11 #13): 튜토리얼 중이거나 이미 보이는 중이면 아무것도 하지 않는다.
        /// 끝나도 아무 신호를 보내지 않는다 (보상 · 플래그 없음). 글은 튜토리얼 인트로 단계 글 (없으면 장면 기본 글)
        /// </summary>
        void ReplayIntro()
        {
            if (_intro == null) return;
            var tutorial = MetaUi.IsActive ? Game.Tutorial : null;
            if (tutorial != null && tutorial.IsActive) return;
            CloseAllPopups();
            _intro.Replay(IntroLines());
        }

        /// <summary>튜토리얼 인트로 단계 글 (줄바꿈으로 장마다, 없으면 null)</summary>
        static string IntroLines()
        {
            var db = Game.IsReady ? Game.MetaDb : null;
            if (db == null || db.ftueSteps == null) return null;
            foreach (var step in db.ftueSteps)
            {
                if (step != null && step.action == FtueAction.StoryIntro && !string.IsNullOrEmpty(step.text)) return step.text;
            }
            return null;
        }

        /// <summary>챕터 지도 (chapter 0 = 지금 챕터). 튜토리얼 중 · 전투 없음이면 아무것도 하지 않는다 (ChapterMapPopup.CanOpen)</summary>
        void OpenChapterMap(int chapter)
        {
            if (_chapterMap == null || !ChapterMapPopup.CanOpen()) return;
            _chapterMap.Show(chapter);
        }

        // ───────────── 가기 + 손가락 ─────────────

        /// <summary>
        /// 이동 기능(nav)으로 간 뒤 focusPath(CoachTargets, Canvas 기준 경로)를 손가락으로 3초 가리킨다 (가이드 퀘스트 줄, 일지 [가기]).
        /// 이동이 팝업을 열었으면(일지·출석부·바구니·시너지 도감 등) 원래 대상은 그 팝업 아래에 묻혀 누를 수 없으므로
        /// 팝업 안의 알맞은 버튼(바구니 [받기] / [빠른 수선] / 출석 [도장 찍기] / 일지 [모두 받기])을 가리키고, 없으면 손가락을 띄우지 않는다.
        /// 튜토리얼 첫 만남 카드가 소환 탭을 덮는 동안에도 띄우지 않는다 (카드 버튼은 튜토리얼 코치마크가 이미 가리킨다).
        /// nav가 Battle이면 이동 대신 MAIN으로 (UINav.GoToMain — 이미 MAIN이면 아무것도 안 한다) 내린 뒤 손가락만.
        /// 그 밖에도 대상이 전투 화면(SafeArea/StageArea/…)인데 열린 시트 밑(UIFrame.IsUnderSheet — 덱 · 램프 · 아래 빠른 아이콘)이면
        /// 먼저 MAIN으로 내린다 (§5.6 — 손가락은 대상이 보일 때까지 기다린다). 탭 이동은 그대로 시트를 연다. 잠긴 기능 확인은 부르는 쪽이 먼저 한다.
        /// </summary>
        public static void GoAndFocus(FeatureId nav, string focusPath)
        {
            var self = s_instance;
            if (self != null) self._opened = null;
            bool battle = nav == FeatureId.Battle;
            if (battle) UINav.GoToMain();
            else UINav.GoToFeature(nav);
            if (string.IsNullOrEmpty(focusPath) && !OpensPopup(nav)) return;
            string path = self != null ? self.ResolveFocus(nav, focusPath) : FallbackFocus(focusPath);
            if (!battle && IsUnderSheet(path)) UINav.GoToMain();
            UINav.FocusTarget(path);
        }

        /// <summary>경로(Canvas 기준)의 대상이 전투 영역 안에 있고 열린(또는 열리는) 시트 밑에 가리는지. 누를 때만 부른다</summary>
        static bool IsUnderSheet(string path)
        {
            if (!UIFrame.SheetOpen || string.IsNullOrEmpty(path) || !path.StartsWith(StageAreaPrefix, StringComparison.Ordinal)) return false;
            var stage = UIFrame.StageRect;
            if (stage == null) return false;
            var target = stage.Find(path.Substring(StageAreaPrefix.Length)) as RectTransform;
            return target != null && UIFrame.IsUnderSheet(target);
        }

        // ───────────── 개방 카드가 날아갈 자리 (M78) ─────────────

        /// <summary>잔향이 덱 여섯째 칸 (EchoSlot6)</summary>
        static readonly string DeckCard5 = CoachTargets.DeckCard(5);

        /// <summary>
        /// 기능 개방 카드의 아이콘이 날아가 앉을 자리 (Canvas 기준 경로, UX-SPEC-IA §5.6 표 — WP-7 M78이 쓴다):
        /// 일지 · 기록 → 빠른 아이콘 수선 일지, 출석부 → 빠른 아이콘 출석부, 이벤트 → 빠른 아이콘 이벤트, 편성 · 시너지 → 편성 탭,
        /// 원정 · 던전 · 한 번에 보내기 → 원정 탭, 소환 → 소환 탭, 상점 → 상점 탭, 진화 · 의상 · 도감 · 친구 강화 → 친구들 탭,
        /// 자동 전투 → AUTO, 2배속 → x1, 바구니 · 빠른 수선 → 바구니 위젯, 불 밝히기 → 램프, 덱 여섯째 칸 → Card5, 자동 보스 도전 → 스테이지 패널, 그 밖 → 사냥 버튼.
        /// 시트가 열려 있으면(목표 상태) 빠른 아이콘(흐려져 사라짐) · 덱 띠(시트 밑) 자리는 사냥 버튼(Tab_Main)으로 바꾼다. 할당 없음.
        /// </summary>
        public static string FeatureSlot(FeatureId feature)
        {
            bool sheet = UIFrame.SheetOpen;
            switch (feature)
            {
                case FeatureId.Missions:
                case FeatureId.Achievements:
                    return sheet ? CoachTargets.TabMain : CoachTargets.JournalButton;
                case FeatureId.Attendance:
                    return sheet ? CoachTargets.TabMain : CoachTargets.AttendanceButton;
                case FeatureId.Events:
                    return sheet ? CoachTargets.TabMain : CoachTargets.EventButton;
                case FeatureId.Formation:
                case FeatureId.Synergy:
                    return CoachTargets.TabFormation;
                case FeatureId.Expedition:
                case FeatureId.Dungeon:
                case FeatureId.ExpeditionBatch:
                    return CoachTargets.TabExpedition;
                case FeatureId.Gacha:
                    return CoachTargets.TabGacha;
                case FeatureId.Shop:
                    return CoachTargets.TabShop;
                case FeatureId.Evolution:
                case FeatureId.Costumes:
                case FeatureId.Codex:
                case FeatureId.CharacterLevelUp:
                    return CoachTargets.TabCharacters;
                case FeatureId.AutoBattle:
                    return CoachTargets.AutoButton;
                case FeatureId.Speed2x:
                    return CoachTargets.SpeedButton;
                case FeatureId.IdleChest:
                case FeatureId.QuickIdle:
                    return CoachTargets.IdleChestWidget;
                case FeatureId.LampPolicy:
                    return sheet ? CoachTargets.TabMain : CoachTargets.LampButton;
                case FeatureId.EchoSlot6:
                    return sheet ? CoachTargets.TabMain : DeckCard5;
                case FeatureId.AutoChallenge:
                    return CoachTargets.StagePanel;
                default:
                    return CoachTargets.TabMain;
            }
        }

        /// <summary>이동하면 팝업이 열리는 기능 (대상이 비어 있어도 팝업 안 버튼을 가리킬 수 있다)</summary>
        static bool OpensPopup(FeatureId nav) => nav switch
        {
            FeatureId.Missions or FeatureId.Achievements or FeatureId.Attendance or FeatureId.IdleChest or FeatureId.QuickIdle => true,
            _ => false,
        };

        /// <summary>내비게이터가 없을 때: 팝업이 열려 있으면 손가락을 띄우지 않는다</summary>
        static string FallbackFocus(string focusPath)
        {
            var ui = UIManager.Instance;
            return ui != null && ui.HasOpenPopup ? null : focusPath;
        }

        string ResolveFocus(FeatureId nav, string focusPath)
        {
            if (IsFirstMeetingCovering(nav, focusPath)) return null;
            EnsureRoots();

            // 이번 이동이 연 메타 팝업: 원래 대상이 그 안에 있으면 그대로, 아니면 팝업 안의 버튼
            if (_opened != null && _opened.IsOpen)
            {
                var target = UIFind.Path(_canvas, focusPath);
                if (target != null && target.IsChildOf(_opened.transform)) return focusPath;
                var inner = InPopupTarget(nav, _opened);
                return inner != null ? CanvasPath(inner) : null;
            }

            // 다른 팝업이 열려 있음(시너지 도감 등): 대상이 팝업 층 안에 있을 때만
            var ui = UIManager.Instance;
            if (ui != null && ui.HasOpenPopup)
            {
                var target = UIFind.Path(_canvas, focusPath);
                return target != null && _popupRoot != null && target.IsChildOf(_popupRoot) ? focusPath : null;
            }
            return focusPath;
        }

        void EnsureRoots()
        {
            if (_canvas == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) _canvas = canvas.rootCanvas.transform;
            }
            if (_popupRoot == null && _journal != null) _popupRoot = _journal.transform.parent;
        }

        /// <summary>
        /// 튜토리얼 첫 만남 카드(확정 소환 전)가 소환 탭을 덮는 중이거나 이 이동으로 덮게 되는지.
        /// FtueDirector와 같은 조건: 튜토리얼 중 + 첫 만남 단계가 있음 + 확정 소환을 아직 안 받음 + 소환 개방이면 대기,
        /// 대기 중 팝업이 없고 소환 시트가 열려 있을 때만 카드가 탭 화면 자리(ContentArea)를 덮는다 (UX-SPEC-IA §5.7 (b) —
        /// MAIN · 다른 탭에는 덮을 자리가 없다. 지금 단계에 대상이 없다고 덮지 않는다: 대상이 없는 첫 만남 단계는 감독이 소환 시트를 연다).
        /// 카드는 감독의 다음 Tick에야 뜨고 지므로(이 이동 직후에는 아직 이동 전 상태) 카드 상태 대신 이동을 본다 —
        /// 소환 탭으로 가거나(GoToTab이 팝업을 닫고 소환 시트를 연다, 소환 탭으로 가는 이동은 이것뿐) 대상이 소환 화면 안이면 소환 시트로 본다.
        /// 덮이는 대상(탭 화면 안)일 때만 true — 전투 화면·탭 버튼은 카드 밖이라 그대로 가리킨다. 누를 때만 부른다.
        /// </summary>
        static bool IsFirstMeetingCovering(FeatureId nav, string focusPath)
        {
            if (!MetaUi.IsActive || Game.Progress.HasFlag(MetaFlags.FirstMeetingSummoned)) return false;
            var tutorial = Game.Tutorial;
            if (tutorial == null || !tutorial.IsActive || !HasFirstMeetingStep()) return false;
            if (Game.Features != null && !Game.Features.IsUnlocked(FeatureId.Gacha)) return false;

            bool gachaSheet = nav == FeatureId.Gacha
                              || (!string.IsNullOrEmpty(focusPath) && focusPath.StartsWith(CoachTargets.GachaPanel, StringComparison.Ordinal));
            if (!gachaSheet) return false;
            return string.IsNullOrEmpty(focusPath) || focusPath.StartsWith(ContentAreaPrefix, StringComparison.Ordinal);
        }

        /// <summary>탭 화면 자리 경로 머리 ("SafeArea/ContentArea/")</summary>
        static readonly string ContentAreaPrefix = CoachTargets.GachaPanel.Substring(0, CoachTargets.GachaPanel.LastIndexOf('/') + 1);

        static bool HasFirstMeetingStep()
        {
            var db = Game.MetaDb;
            if (db == null) return false;
            for (int i = 0; i < db.ftueSteps.Count; i++)
            {
                var step = db.ftueSteps[i];
                if (step != null && step.action == FtueAction.FirstMeetingSummon && !string.IsNullOrEmpty(step.actionArg)) return true;
            }
            return false;
        }

        /// <summary>이동이 연 팝업 안에서 가리킬 버튼 (켜져 있고 누를 수 있는 것만)</summary>
        static RectTransform InPopupTarget(FeatureId nav, UIPopup popup)
        {
            string name = nav switch
            {
                FeatureId.QuickIdle => "QuickButton",
                FeatureId.IdleChest => "ClaimButton",
                FeatureId.Attendance => "CheckInButton",
                FeatureId.Missions or FeatureId.Achievements => "ClaimAllButton",
                _ => null,
            };
            var target = name != null ? UIFind.Deep(popup.transform, name) : null;
            if (target == null || !target.gameObject.activeInHierarchy) return null;
            var button = target.GetComponent<Button>();
            return button == null || button.IsInteractable() ? target : null;
        }

        /// <summary>Canvas 기준 경로 (UINav.FocusTarget은 경로 문자열을 받는다). 누를 때 한 번만 만든다</summary>
        string CanvasPath(Transform target)
        {
            if (target == null || _canvas == null || target == _canvas || !target.IsChildOf(_canvas)) return null;
            var builder = new StringBuilder(96);
            for (var t = target; t != null && t != _canvas; t = t.parent)
            {
                if (builder.Length > 0) builder.Insert(0, '/');
                builder.Insert(0, t.name);
            }
            return builder.ToString();
        }

        static bool CheckUnlocked(FeatureId feature)
        {
            if (Game.Features.TryUse(feature, out var message)) return true;
            MetaUi.Toast(message);
            return false;
        }

        void OpenJournal()
        {
            if (!MetaUi.IsActive || !CheckUnlocked(FeatureId.Missions)) return;
            _journal.Open();
        }

        void OpenAttendance()
        {
            if (!MetaUi.IsActive || !CheckUnlocked(FeatureId.Attendance)) return;
            _attendance.Open();
        }

        void OpenIdleChest()
        {
            if (!MetaUi.IsActive || !CheckUnlocked(FeatureId.IdleChest)) return;
            _idleChest.Open();
        }

        void OnFeature(FeatureId feature)
        {
            if (!MetaUi.IsActive || feature == FeatureId.Battle) return;
            if (!CheckUnlocked(feature)) return;

            switch (feature)
            {
                case FeatureId.ShopUpgrade:
                case FeatureId.CharacterLevelUp:
                    GoToTab(MainTab.Growth);
                    break;
                case FeatureId.Formation:
                case FeatureId.EchoSlot6:
                    GoToTab(MainTab.Formation);
                    break;
                case FeatureId.Synergy:
                    GoToTab(MainTab.Formation);
                    UINav.OpenSynergy(null);
                    break;
                case FeatureId.Characters:
                    GoToTab(MainTab.Characters);
                    break;
                case FeatureId.Evolution:
                case FeatureId.Costumes:
                {
                    GoToTab(MainTab.Characters);
                    bool evolution = feature == FeatureId.Evolution;
                    string lineage = PickLineage(evolution);
                    if (lineage != null) UINav.OpenCharacter(lineage, evolution ? CharacterDetailPopup.TabTree : CharacterDetailPopup.TabCostume);
                    else MetaUi.Toast("먼저 친구를 만나 봐요");
                    break;
                }
                case FeatureId.Codex:
                    // 도감은 친구들 탭 자체 — 상세를 열지 않는다 (M73)
                    GoToTab(MainTab.Characters);
                    break;
                case FeatureId.Expedition:
                case FeatureId.ExpeditionBatch:
                    GoToTab(MainTab.Expedition);
                    break;
                case FeatureId.Dungeon:
                    GoToTab(MainTab.Expedition);
                    UINav.GoToSource(ItemSourceType.Dungeon, string.Empty);
                    break;
                case FeatureId.Gacha:
                    GoToTab(MainTab.Gacha);
                    break;
                case FeatureId.Shop:
                    GoToTab(MainTab.Shop);
                    break;
                case FeatureId.Missions:
                    // 미션 쪽으로 (가이드 퀘스트 18 '일지 미션 받기' — [첫 주]부터 열면 [모두 받기]가 첫 주 과제를 받아 퀘스트가 오르지 않는다)
                    _journal.OpenMissions();
                    _opened = _journal;
                    break;
                case FeatureId.Achievements:
                    // 수선점 기록 쪽으로 (Q-04)
                    _journal.OpenRecords();
                    _opened = _journal;
                    break;
                case FeatureId.Attendance:
                    _attendance.Open();
                    _opened = _attendance;
                    break;
                case FeatureId.IdleChest:
                case FeatureId.QuickIdle:
                    _idleChest.Open();
                    _opened = _idleChest;
                    break;
                case FeatureId.AutoBattle:
                case FeatureId.Speed2x:
                    CloseAllPopups();
                    if (MetaUi.IsFeatureAlreadyOn(feature))
                    {
                        // 이미 켜진 버튼을 가리키면 눌러서 꺼 버리게 된다 → 알림만 (새로 열림도 봤음 처리)
                        if (Game.Features.IsNew(feature)) Game.Features.MarkSeen(feature);
                        MetaUi.Toast(feature == FeatureId.AutoBattle ? "자동 전투가 켜져 있어요. 친구들이 알아서 싸워요" : "2배속이 켜져 있어요");
                        break;
                    }
                    UINav.FocusTarget(feature == FeatureId.AutoBattle ? CoachTargets.AutoButton : CoachTargets.SpeedButton);
                    break;
                case FeatureId.Events:
                    OpenEvents();
                    break;
            }
        }

        /// <summary>
        /// 이벤트 (UINav.EventsRequested · 빠른 아이콘 · 기능 이동 — 이벤트 길은 여기 한 곳, SPEC4 §2.11 #2 · UX-SPEC-IA §8.8):
        /// 이벤트가 열렸고 모아 보기(EventHubPopup)가 장면에 있으면 모아 보기, 아니면 (이벤트가 열리기 전 · 메타가 꺼짐 · 다시 만들기 전 장면)
        /// 지금처럼 이벤트 상점 (GoToSource(Shop, ShopDatabase.EventShopId)). 이벤트는 개방 카드가 없을 수 있어 여기서 본 것으로 표시한다 (CR-06)
        /// </summary>
        void OpenEvents()
        {
            var features = MetaUi.IsActive ? Game.Features : null;
            bool unlocked = features != null && features.IsUnlocked(FeatureId.Events);
            if (unlocked && features.IsNew(FeatureId.Events)) features.MarkSeen(FeatureId.Events);
            var ui = UIManager.Instance;
            if (unlocked && Game.Events != null && ui != null && ui.TryGet<EventHubPopup>(out var hub))
            {
                hub.Open();
                return;
            }
            UINav.GoToSource(ItemSourceType.Shop, ShopDatabase.EventShopId);
        }

        static void GoToTab(MainTab tab)
        {
            CloseAllPopups();
            UINav.GoToTab(tab);
        }

        /// <summary>탭으로 갈 때 위에 열린 팝업을 닫는다 (탭이 팝업 아래에서 바뀌지 않게 — 닫기 정책을 거치지 않는 UIManager.CloseAll)</summary>
        static void CloseAllPopups()
        {
            var ui = UIManager.Instance;
            if (ui != null) ui.CloseAll();
        }

        // ───────────── 진화 · 의상으로 갈 친구 (M73) ─────────────

        /// <summary>누를 때 한 번 채워 쓰는 가진 친구 id 목록 (할당은 처음 한 번)</summary>
        static readonly List<string> s_owned = new(16);

        /// <summary>
        /// 진화(evolution) · 의상 쪽으로 열 친구: 그 빨간 점(RedDotPaths.LineageEvolution / LineageCostume)이 켜진 첫 친구 →
        /// 짝짝이(가졌으면) → 처음 가진 친구 → 없으면 null (부르는 쪽이 토스트). 순서는 CharacterSystem.GetOwnedLineages(등급 높은 순 → ID 순). 누를 때만 부른다
        /// </summary>
        static string PickLineage(bool evolution)
        {
            var characters = Game.Characters;
            if (characters == null) return null;
            s_owned.Clear();
            foreach (var lineage in characters.GetOwnedLineages())
            {
                if (lineage != null && !string.IsNullOrEmpty(lineage.id)) s_owned.Add(lineage.id);
            }
            var dots = Game.RedDots;
            return PickLineage(s_owned, dots == null ? null : id => dots.IsOn(evolution ? RedDotPaths.LineageEvolution(id) : RedDotPaths.LineageCostume(id)),
                GuideLineage);
        }

        /// <summary>
        /// PickLineage 규칙 (순수 — 테스트용): owned(가진 순서) 중 dotOn이 켜진 첫 id → guide가 owned에 있으면 guide → owned 첫 id → null.
        /// dotOn이 null이면 점 단계를 건너뛴다
        /// </summary>
        internal static string PickLineage(IReadOnlyList<string> owned, Func<string, bool> dotOn, string guide)
        {
            if (owned == null || owned.Count == 0) return null;
            if (dotOn != null)
            {
                for (int i = 0; i < owned.Count; i++)
                {
                    if (!string.IsNullOrEmpty(owned[i]) && dotOn(owned[i])) return owned[i];
                }
            }
            if (!string.IsNullOrEmpty(guide))
            {
                for (int i = 0; i < owned.Count; i++)
                {
                    if (string.Equals(owned[i], guide, StringComparison.Ordinal)) return guide;
                }
            }
            for (int i = 0; i < owned.Count; i++)
            {
                if (!string.IsNullOrEmpty(owned[i])) return owned[i];
            }
            return null;
        }
    }
}
