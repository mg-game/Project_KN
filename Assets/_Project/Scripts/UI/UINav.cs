using System;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>메인 탭 순서. IdleSceneBuilder가 이 순서대로 탭을 만든다.</summary>
    public enum MainTab
    {
        Growth = 0,     // 강화 (수선점 강화)
        Formation = 1,  // 편성
        Characters = 2, // 친구들 (캐릭터 목록 → 상세/진화 기억도)
        Expedition = 3, // 원정 (+ 잔향이 던전)
        Gacha = 4,      // 소환
        Shop = 5,       // 상점 (+ 이벤트 상점)
    }

    /// <summary>
    /// 화면 간 이동 요청 창구. 예: 진화 레시피의 "재료 수집하러 가기" → 해당 원정/던전/상점으로 이동.
    /// 요청을 보내는 쪽은 받는 쪽을 몰라도 된다.
    /// <para>
    /// 화면 틀 (UX-SPEC-IA §1.2 · §5.2): GoToTab = 그 탭 시트를 열거나 바꾼다 (닫지 않는다 — 같은 탭을 두 번 불러도 열린 채),
    /// GoToMain = 시트를 내려 MAIN(전투를 크게)으로. 둘 다 MainTabController가 받는다.
    /// </para>
    /// <para>
    /// 돌아가기 칸 (M72 · §3.14): 팝업에서 다른 화면으로 건너뛸 때 보내는 쪽이 PushReturn('‹ {형태} 레시피로', 다시 열기)을 넣으면
    /// 시트 머리의 돌아가기 칩(ReturnChipView)이 보인다. 칸은 하나 — 새로 넣으면 바뀐다. 칩을 누르면(TryPopReturn) 먼저 비우고 다시 연다.
    /// 칸은 넣을 때의 화면 틀(UIFrame.SheetTab, −1 = MAIN)도 기억해, 다시 열기 전에 그 틀로 되돌린다 (CR-05 — MAIN 빠른 아이콘에서 연 일지
    /// → 가기 → 강화 시트 → 칩: 시트를 내리고 MAIN 위에 일지가 열린다).
    /// ×를 누르거나, 다른 탭 버튼을 직접 누르거나, MAIN으로 가면(사냥 · 같은 탭 다시 누름 · 손잡이 · 끌어 내림 · 뒤로 · 자동 복귀 · GoToMain) 비운다
    /// (UINav.GoToTab 같은 코드 이동은 비우지 않는다). 시간 제한은 없다. 가이드 퀘스트 · 개방 카드 이동(MetaNavigator)은 넣지 않는다.
    /// 칸은 정적이라 장면이 내려갈 때(UIManager.OnDestroy — 데이터 초기화의 장면 다시 읽기 · 테스트의 장면 다시 읽기) 비운다 —
    /// 새 장면이 옛 장면 팝업을 붙잡은 다시 열기를 물려받지 않게.
    /// </para>
    /// <para>
    /// 캐릭터 · 시너지 열기 (§3.14): 받는 쪽은 새 이벤트 CharacterOpenRequested(계보 ID, 탭) · SynergyOpenRequested(ID, 종류 힌트)를 구독한다.
    /// 예전 CharacterRequested · SynergyRequested(ID만)는 [Obsolete] 이음새다 — 새 이벤트에 구독자가 하나라도 있으면 불리지 않으므로
    /// (같은 화면이 두 번 열리지 않게) 남은 구독자는 컴파일 경고(CS0618)로 드러난다. 라우터를 옮기는 커밋에서 예전 구독을 함께 지운다.
    /// </para>
    /// </summary>
    public static class UINav
    {
        /// <summary>(획득처 종류, 대상 ID) — 메뉴 화면 담당 뷰가 구독해서 탭/팝업을 연다.</summary>
        public static event Action<ItemSourceType, string> SourceRequested;
        /// <summary>메인 탭 시트 열기 · 바꾸기 요청 — MainTabController가 구독한다 (닫지 않는다).</summary>
        public static event Action<MainTab> TabRequested;

        /// <summary>
        /// 캐릭터 상세 열기 요청 (계보 ID, 탭 — CharacterDetailPopup.Tab* 상수, -1이면 상세가 정한다: 다른 계보면 진화 기억도).
        /// 캐릭터 화면 담당(CharacterNavRouter)이 구독한다. 구독자가 있으면 예전 CharacterRequested는 부르지 않는다.
        /// </summary>
        public static event Action<string, int> CharacterOpenRequested;
        /// <summary>
        /// 예전 캐릭터 상세 열기 요청 (계보 ID만 — 탭은 버린다). CharacterOpenRequested 구독자가 없을 때만 부른다
        /// (라우터가 새 이벤트로 옮기기 전까지의 이음새 — 옮긴 뒤 지운다).
        /// </summary>
        [Obsolete("UINav.CharacterOpenRequested (Action<string, int> — 계보 ID, CharacterDetailPopup.Tab* 탭)를 구독하세요. "
                  + "새 이벤트에 구독자가 하나라도 생기면 이 이벤트는 더 이상 불리지 않습니다.")]
        public static event Action<string> CharacterRequested;

        public static void GoToSource(ItemSource source)
        {
            if (source != null) SourceRequested?.Invoke(source.type, source.targetId);
        }

        public static void GoToSource(ItemSourceType type, string targetId) => SourceRequested?.Invoke(type, targetId);

        /// <summary>
        /// 그 탭 시트를 연다 (MAIN이면 열고, 다른 탭이 열려 있으면 바꾸고, 같은 탭이면 그대로 — 절대 닫지 않는다, UX-SPEC-IA §5.2).
        /// 코드 이동이라 돌아가기 칸은 그대로 둔다. 잠긴 탭이면 받는 쪽(탭 잠금)이 토스트를 띄우고 틀은 그대로다.
        /// </summary>
        public static void GoToTab(MainTab tab) => TabRequested?.Invoke(tab);

        /// <summary>
        /// MAIN(전투를 크게, 시트 없음)으로 돌아가기 요청 (인자 = immediate) — MainTabController가 구독해 ShowMain(immediate)을 부른다 (UX-SPEC-IA §5.2).
        /// </summary>
        public static event Action<bool> MainRequested;

        /// <summary>
        /// MAIN으로: 열린 시트를 내린다 (MAIN이면 아무 일 없다). immediate면 연출 없이 바로 (도구 · 테스트).
        /// 받는 쪽(ShowMain)이 돌아가기 칸도 비운다 — MAIN으로 가는 모든 길이 칸을 비운다 (CR-05).
        /// GoToTab과 짝: GoToTab은 시트를 열거나 바꾸기만 하고 닫지 않는다.
        /// </summary>
        public static void GoToMain(bool immediate = false) => MainRequested?.Invoke(immediate);

        /// <summary>캐릭터 상세를 연다 (tab = CharacterDetailPopup.Tab*, -1이면 상세가 정한다)</summary>
        public static void OpenCharacter(string lineageId, int tab = -1)
        {
            var handlers = CharacterOpenRequested;
            if (handlers != null) handlers(lineageId, tab);
#pragma warning disable CS0618 // 예전 구독자에게 넘기는 이음새 (라우터가 옮기면 지운다)
            else CharacterRequested?.Invoke(lineageId);
#pragma warning restore CS0618
        }

        /// <summary>
        /// 시너지 상세/도감 열기 요청 (시너지 ID — null이면 도감 첫 화면, 종류 힌트 — 도감이 그 종류 탭을 고른다).
        /// 편성 화면 담당(SynergyNavigator)이 구독한다. 구독자가 있으면 예전 SynergyRequested는 부르지 않는다.
        /// </summary>
        public static event Action<string, SynergyKind?> SynergyOpenRequested;
        /// <summary>예전 시너지 열기 요청 (ID만 — 힌트는 버린다). SynergyOpenRequested 구독자가 없을 때만 부른다 (옮긴 뒤 지운다)</summary>
        [Obsolete("UINav.SynergyOpenRequested (Action<string, SynergyKind?> — 시너지 ID 또는 null = 도감, 종류 힌트)를 구독하세요. "
                  + "새 이벤트에 구독자가 하나라도 생기면 이 이벤트는 더 이상 불리지 않습니다.")]
        public static event Action<string> SynergyRequested;

        /// <summary>시너지 상세(ID) 또는 도감(null)을 연다. kindHint가 있으면 도감이 그 종류 탭을 고른다 (전투 시너지 칩: 켜진 첫 시너지의 종류)</summary>
        public static void OpenSynergy(string synergyId, SynergyKind? kindHint = null)
        {
            var handlers = SynergyOpenRequested;
            if (handlers != null) handlers(synergyId, kindHint);
#pragma warning disable CS0618 // 예전 구독자에게 넘기는 이음새 (라우터가 옮기면 지운다)
            else SynergyRequested?.Invoke(synergyId);
#pragma warning restore CS0618
        }

        // ───────────── 돌아가기 칸 (M72) ─────────────

        /// <summary>돌아가기 칸이 바뀜 (넣음 · 비움) — 돌아가기 칩이 구독한다</summary>
        public static event Action ReturnChanged;

        static Action _returnReopen;
        /// <summary>칸을 넣을 때의 화면 틀 (UIFrame.SheetTab — −1 = MAIN)</summary>
        static int _returnTab = -1;

        /// <summary>돌아가기 칩 글 ('‹ 수선 일지로' — 보내는 쪽이 '‹'까지 넣는다). 비어 있으면 null</summary>
        public static string ReturnLabel { get; private set; }

        /// <summary>돌아가기 칸이 차 있는지</summary>
        public static bool HasReturn => _returnReopen != null;

        /// <summary>칸을 넣을 때 열려 있던 탭 (MainTab 번호, −1 = MAIN — 비어 있어도 −1). TryPopReturn이 다시 열기 전에 이 틀로 되돌린다</summary>
        public static int ReturnTab => _returnTab;

        /// <summary>
        /// 돌아갈 곳을 넣는다 (화면을 옮기기 전에 — 팝업을 닫기 전, GoToTab 전에 부른다). 칸은 하나라 있던 것은 바뀐다.
        /// 지금 화면 틀(UIFrame.SheetTab, −1 = MAIN)도 함께 기억한다 (CR-05).
        /// reopen은 원래 팝업(과 그 탭)을 다시 연다 — 대상이 더 이상 맞지 않으면(이미 만든 형태 등) 토스트를 띄우고 그냥 돌아온다.
        /// </summary>
        public static void PushReturn(string label, Action reopen)
        {
            if (reopen == null || string.IsNullOrEmpty(label))
            {
                ClearReturn();
                return;
            }
            _returnReopen = reopen;
            _returnTab = UIFrame.SheetTab;
            ReturnLabel = label;
            ReturnChanged?.Invoke();
        }

        /// <summary>
        /// 돌아가기 칩을 눌렀을 때: 다시 열기와 기억한 틀을 읽고 칸을 먼저 비운 뒤, 틀을 되돌리고(CR-05) 다시 연다. 칸이 비었으면 false.
        /// 틀: 기억한 틀이 MAIN(−1)이면 GoToMain(immediate: true), 지금과 다른 탭이면 GoToTab, 지금 탭이면 그대로
        /// (칸을 먼저 비우므로 ShowMain 안의 ClearReturn은 아무 일도 하지 않는다).
        /// 되돌리기 · 다시 열기에서 오류가 나면 토스트를 띄우고 false (칸은 이미 비어 있다).
        /// </summary>
        public static bool TryPopReturn()
        {
            var reopen = _returnReopen;
            if (reopen == null) return false;
            int tab = _returnTab;
            ClearReturn();
            try
            {
                RestoreFrame(tab);
                reopen();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var ui = UIManager.Instance;
                if (ui != null) ui.ShowToast("이전 화면을 다시 열 수 없어요", ToastKind.Error);
                return false;
            }
        }

        /// <summary>칸을 넣을 때의 틀로 되돌린다 (−1 = MAIN, 바로 / 다른 탭 = 그 탭 시트 / 지금 탭 = 그대로)</summary>
        static void RestoreFrame(int tab)
        {
            if (tab == UIFrame.SheetTab) return;
            if (tab < 0) GoToMain(true);
            else GoToTab((MainTab)tab);
        }

        /// <summary>돌아가기 칸을 비운다 (× · 다른 탭 버튼을 직접 누름 · MAIN으로 감 · 장면이 내려감). 이미 비었으면 아무것도 하지 않는다</summary>
        public static void ClearReturn()
        {
            _returnTab = -1;
            if (_returnReopen == null && ReturnLabel == null) return;
            _returnReopen = null;
            ReturnLabel = null;
            ReturnChanged?.Invoke();
        }

        // ───────────── 메타 (수선 일지 / 출석부 / 수선 바구니 / 기능 이동 / 코치마크) ─────────────

        /// <summary>수선 일지 열기 요청 (일일·주간 미션) — 메타 UI(M3)가 구독한다.</summary>
        public static event Action JournalRequested;
        /// <summary>출석부 열기 요청 — 메타 UI(M3)가 구독한다.</summary>
        public static event Action AttendanceRequested;
        /// <summary>수선 바구니 팝업 열기 요청 — 메타 UI(M3)가 구독한다.</summary>
        public static event Action IdleChestRequested;
        /// <summary>
        /// 기능으로 이동 요청 (가이드 퀘스트 [가기], 개방 카드 [가 보기], 미션 [가기]) — 메타 UI(M3)의 MetaNavigator가
        /// 기능 → 탭/팝업으로 바꿔 GoToTab / OpenJournal / OpenIdleChest / GoToSource / OpenCharacter 등을 부른다.
        /// 잠긴 기능이면 받는 쪽이 토스트를 띄운다.
        /// </summary>
        public static event Action<FeatureId> FeatureRequested;
        /// <summary>
        /// 화면 대상을 손가락으로 잠깐 가리키기 요청 (Canvas 기준 경로, CoachTargets 상수) — 튜토리얼 UI(M4)의 코치마크가 구독한다.
        /// 가이드 퀘스트 이동 뒤 3초 손가락 등.
        /// </summary>
        public static event Action<string> FocusRequested;

        public static void OpenJournal() => JournalRequested?.Invoke();

        public static void OpenAttendance() => AttendanceRequested?.Invoke();

        public static void OpenIdleChest() => IdleChestRequested?.Invoke();

        public static void GoToFeature(FeatureId feature) => FeatureRequested?.Invoke(feature);

        /// <summary>targetPath를 손가락으로 가리킨다 (비어 있으면 무시)</summary>
        public static void FocusTarget(string targetPath)
        {
            if (!string.IsNullOrEmpty(targetPath)) FocusRequested?.Invoke(targetPath);
        }

        // ───────────── 4차 (SPEC4 §2.6) — 받는 쪽이 아직 없으면 아무 일도 없다 ─────────────

        /// <summary>도감 열기 요청 (탭 — null이면 도감이 정한다) — 메타 UI의 MetaNavigator(P4-6)가 구독한다.</summary>
        public static event Action<CodexCategory?> CodexRequested;
        /// <summary>이벤트 모아 보기 열기 요청 — MetaNavigator(P4-6)가 구독한다.</summary>
        public static event Action EventsRequested;
        /// <summary>별빛 산책 수첩 열기 요청 — MetaNavigator(P4-6)가 구독한다.</summary>
        public static event Action SeasonRequested;
        /// <summary>챕터 지도 열기 요청 (챕터, 0 = 지금 챕터) — MetaNavigator(P4-6)가 구독한다.</summary>
        public static event Action<int> ChapterMapRequested;
        /// <summary>전투 기록 열기 요청 — 전투 화면(P4-3b)이 구독한다.</summary>
        public static event Action BattleStatsRequested;
        /// <summary>동화 인트로 다시 보기 요청 (설정) — StoryIntroView(P4-6)가 구독한다. 튜토리얼 코치마크는 다시 하지 않는다.</summary>
        public static event Action IntroReplayRequested;
        /// <summary>편성 칸 반짝임 요청 (칸 번호, 초) — 편성 화면(P4-4)이 구독한다 (패배 분석의 약한 방위 안내).</summary>
        public static event Action<int, float> FormationSlotPulseRequested;

        /// <summary>도감을 연다 (tab = 처음 보여 줄 탭, null이면 도감이 정한다)</summary>
        public static void OpenCodex(CodexCategory? tab = null) => CodexRequested?.Invoke(tab);

        public static void OpenEvents() => EventsRequested?.Invoke();

        public static void OpenSeason() => SeasonRequested?.Invoke();

        /// <summary>챕터 지도를 연다 (chapter 0 = 지금 챕터)</summary>
        public static void OpenChapterMap(int chapter = 0) => ChapterMapRequested?.Invoke(chapter);

        /// <summary>
        /// 챕터 지도를 받을 쪽(MetaNavigator, P4-6)이 구독했는지 (RM-2 SB-2). false면 전투 화면(P4-3b)은 스테이지 패널을 눌렀을 때 예전 스테이지 목록을 연다
        /// </summary>
        public static bool HasChapterMap => ChapterMapRequested != null;

        public static void OpenBattleStats() => BattleStatsRequested?.Invoke();

        public static void ReplayIntro() => IntroReplayRequested?.Invoke();

        /// <summary>
        /// 인트로 다시 보기를 받을 쪽(MetaNavigator → StoryIntroView, P4-6)이 구독했는지. false면 설정의 '보기' 단추를 흐리게 둔다 (P4-4)
        /// </summary>
        public static bool HasIntroReplay => IntroReplayRequested != null;

        /// <summary>편성 slot번 칸을 seconds초 동안 반짝인다</summary>
        public static void PulseFormationSlot(int slot, float seconds = 2f) => FormationSlotPulseRequested?.Invoke(slot, seconds);
    }
}
