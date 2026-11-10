using System;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 화면 틀(IA) 상태. SheetController만 쓰고, 전투 카메라·HUD 흐림·튜토리얼·코치가 읽는다.
    /// <para>
    /// 두 상태 (UX-SPEC-IA §1.2): MAIN = 전투가 세로 전체를 쓰고 작은 아이콘 몇 개만 (시트 없음, SheetTab −1) /
    /// SHEET(탭) = 836px 시트(머리 56 + 탭 내용 780)가 전투 아래쪽을 잠깐 덮는다 (전투는 그 위에서 계속 돈다). 실행하면 언제나 MAIN.
    /// 시트 위치 · 전투 올림 · 띠 아래 여백 · HUD 흐림(SheetFade)은 모두 같은 SheetProgress(p, 보이는 양 0~1)를 따른다.
    /// </para>
    /// <para>
    /// SheetOpen · SheetTab은 목표 상태다 — 고르기(Select) · 닫기(ShowMain)를 부른 그 프레임에 바뀐다 (p는 그 뒤 열기 0.30 / 닫기 0.24초 동안 따라간다).
    /// 알림: SheetStateChanged = 열림/닫힘 목표가 바뀔 때만 (열린 채 탭만 바뀌면 부르지 않는다),
    /// SheetProgressChanged = p가 바뀔 때, 그리고 움직임이 멈춘 프레임(SheetSettled가 켜짐)에 한 번 — p가 그대로면 매 프레임 부르지 않는다.
    /// 구독자가 예외를 내도 시트 연출은 멈추지 않는다 (로그만).
    /// </para>
    /// <para>
    /// 정적 상태라 장면을 다시 읽어도 남는다 (데이터 초기화 · PlayMode 테스트마다 Main을 다시 읽음, CR-20):
    /// Bind(SheetController.Awake)와 Unbind(SheetController.OnDestroy)가 값을 모두 처음(닫힘, −1, 0, 멈춤)으로 되돌린다 (알림 없이),
    /// 실행 시작(SubsystemRegistration)에는 알림 구독자도 지운다. 옛 장면의 SheetOpen · 부서진 SheetRect가 새 장면으로 넘어가지 않게.
    /// </para>
    /// </summary>
    public static class UIFrame
    {
        // ───────────── 틀 높이 (기준 px, 1080x1920 — UX-SPEC-IA §1.1) ─────────────

        /// <summary>짧은 상단 바 (MAIN · SHEET 같음)</summary>
        public const float TopBarHeight = 130f;
        /// <summary>하단 탭 바 (3 + 사냥 + 3)</summary>
        public const float TabBarHeight = 170f;
        /// <summary>전투 아래 잔향이 덱 띠 (전장 아래 끝 = 이 띠 위 끝)</summary>
        public const float StripHeight = 156f;
        /// <summary>시트 머리 (손잡이 · 돌아가기 칩)</summary>
        public const float SheetHeaderHeight = 56f;
        /// <summary>시트 안 탭 내용 (ContentArea — 열리면 지금의 970~1750 자리 그대로)</summary>
        public const float SheetContentHeight = 780f;
        /// <summary>시트 전체 (탭 여섯 개 모두 같은 높이) = 56 + 780</summary>
        public const float SheetHeight = SheetHeaderHeight + SheetContentHeight;   // 836

        /// <summary>다 열린 시트의 위 끝 — SafeArea 아래 끝에서 잰 높이 (170 + 836 = 1006). p와 상관없는 고정값</summary>
        const float OpenSheetTop = TabBarHeight + SheetHeight;

        // ───────────── 상태 ─────────────

        /// <summary>시트 목표 상태 (Open()부터 true, Close()부터 false — p가 아직 움직이는 중이어도)</summary>
        public static bool SheetOpen { get; private set; }
        /// <summary>열린(또는 열리는) 탭 = MainTab 번호, MAIN이면 −1</summary>
        public static int SheetTab { get; private set; } = -1;
        /// <summary>시트가 보이는 양 (이징을 거친 값 0 = 닫힘 ~ 1 = 다 열림)</summary>
        public static float SheetProgress { get; private set; }
        /// <summary>움직이지 않음 (열기 · 닫기 · 끌기가 끝남)</summary>
        public static bool SheetSettled { get; private set; } = true;
        /// <summary>시트 = SafeArea/ContentArea (Bind 전 · 장면이 내려간 뒤에는 null)</summary>
        public static RectTransform SheetRect { get; private set; }
        /// <summary>전투 영역 = SafeArea/StageArea (Bind 전 · 장면이 내려간 뒤에는 null)</summary>
        public static RectTransform StageRect { get; private set; }

        /// <summary>p가 바뀜 (+ 움직임이 멈춘 프레임에 한 번). 인자 = SheetProgress</summary>
        public static event Action<float> SheetProgressChanged;
        /// <summary>목표 열림/닫힘이 바뀜. 인자 = SheetOpen</summary>
        public static event Action<bool> SheetStateChanged;

        /// <summary>IsUnderSheet가 재는 기준 (SafeArea — StageArea · ContentArea의 부모)</summary>
        static RectTransform _safeRect;

        // ───────────── 묻기 ─────────────

        /// <summary>
        /// StageArea 안 대상이 열린(또는 열리는) 시트 밑에 가리는지: 목표 상태(SheetOpen) + 다 열린 시트 위 끝 기준.
        /// 탭 바·상단 바·팝업 대상은 false (CR-02).
        /// 셋이 모두 맞을 때 true — ① SheetOpen (목표 상태라 Select · ShowMain과 같은 프레임에 바뀐다, p는 보지 않는다)
        /// ② 대상이 StageRect 아래 ③ 대상 사각형 가운데가 SafeArea 기준으로 다 열린 시트 위 끝(SafeArea 아래 끝 + 170 + 836)보다 아래.
        /// 전투 카메라(Field/Camera) 아래에는 코치 대상이 없어서 올림이 결과를 바꾸지 않는다. 할당 없음.
        /// </summary>
        public static bool IsUnderSheet(RectTransform target)
        {
            if (!SheetOpen || target == null) return false;
            var stage = StageRect;
            if (stage == null || !target.IsChildOf(stage)) return false;
            var safe = _safeRect;
            if (safe == null) return false;
            Vector3 center = target.TransformPoint(target.rect.center);
            return safe.InverseTransformPoint(center).y < safe.rect.yMin + OpenSheetTop;
        }

        /// <summary>
        /// 보상 날아가기 도착지로 못 쓰는지: IsUnderSheet 또는 SheetFade 알파 &lt; 0.05 (CR-03).
        /// 가장 가까운 SheetFade 조상(자기 포함, 꺼진 것도 찾는다)의 알파가 SheetFade.HiddenBelow 아래면 흐려진 것으로 본다
        /// (시트가 열려 빠른 아이콘 줄이 사라짐 — 오브젝트는 켜진 채 알파 0). 대상이 없으면 true.
        /// 날아가기 한 번에 한 번 부른다 (GetComponentInParent, 할당 없음).
        /// </summary>
        public static bool IsFeedbackHidden(RectTransform target)
        {
            if (target == null) return true;
            if (IsUnderSheet(target)) return true;
            var fade = target.GetComponentInParent<SheetFade>(true);
            return fade != null && fade.Alpha < SheetFade.HiddenBelow;
        }

        // ───────────── 쓰기 (SheetController만) ─────────────

        /// <summary>시트 · 전투 영역을 잇고 상태를 처음으로 (닫힘, −1, 0, 멈춤 — 알림 없이). SheetController.Awake가 부른다</summary>
        internal static void Bind(RectTransform sheetRect, RectTransform stageRect)
        {
            ResetState();
            SheetRect = sheetRect;
            StageRect = stageRect;
            _safeRect = stageRect != null ? stageRect.parent as RectTransform : null;
            if (_safeRect == null && sheetRect != null) _safeRect = sheetRect.parent as RectTransform;
        }

        /// <summary>장면이 내려감 (SheetController.OnDestroy): 잇기를 풀고 상태를 처음으로 (알림 없이 — 내려가는 장면의 구독자를 부르지 않는다)</summary>
        internal static void Unbind()
        {
            ResetState();
            SheetRect = null;
            StageRect = null;
            _safeRect = null;
        }

        /// <summary>목표 상태를 바꾼다 (open이 false면 탭은 −1). 열림/닫힘이 바뀌었을 때만 SheetStateChanged</summary>
        internal static void SetState(bool open, int tab)
        {
            bool changed = open != SheetOpen;
            SheetOpen = open;
            SheetTab = open ? tab : -1;
            if (changed) Raise(SheetStateChanged, open);
        }

        /// <summary>
        /// 보이는 양을 바꾼다 (0~1로 자른다). p가 바뀌었거나 이번에 멈췄으면(settled가 false → true) SheetProgressChanged —
        /// 따라가는 쪽은 그 안에서 SheetSettled를 읽어 마지막 프레임을 안다.
        /// </summary>
        internal static void SetProgress(float p, bool settled)
        {
            if (float.IsNaN(p)) p = 0f;
            p = Mathf.Clamp01(p);
            bool moved = p != SheetProgress;
            bool justSettled = settled && !SheetSettled;
            SheetProgress = p;
            SheetSettled = settled;
            if (moved || justSettled) Raise(SheetProgressChanged, p);
        }

        static void ResetState()
        {
            SheetOpen = false;
            SheetTab = -1;
            SheetProgress = 0f;
            SheetSettled = true;
        }

        static void Raise<T>(Action<T> handler, T value)
        {
            if (handler == null) return;
            try
            {
                handler(value);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>실행 시작 (도메인 다시 읽기를 끈 플레이 모드 포함): 상태 · 잇기 · 구독자를 모두 지운다 (CR-20)</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Unbind();
            SheetProgressChanged = null;
            SheetStateChanged = null;
        }
    }
}
