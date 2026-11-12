using IdleGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static IdleGame.EditorTools.SceneParts;
using static IdleGame.EditorTools.UIKit;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 설정 팝업 빌더 (IdleSceneBuilder에서 옮겨 옴, 담당: M3). SET-01.
    /// 탭 [일반][전투 연출][알림][계정·데이터] (+ 에디터/Development Build는 [개발]). 값은 GameSettings, 화면은 SettingsPopup.
    /// 공지사항과 튜토리얼 건너뛰기(Game.Tutorial)도 [일반]에 있다.
    /// 컨트롤(스위치·칸 고르기·슬라이더)은 MetaSceneBuilder의 공용 조각을 쓴다 (계약 C-11).
    /// 그림: 탭 = 흰 바느질 탭(ui_tab_9s, SettingsPopup이 TabStyle 쪽 탭 모양으로 칠한다 — M53), 버튼 = SceneParts.SkinButton(보조 · 위험),
    /// 계정 연동 줄 = 보라 바느질 판. 연동 버튼은 AccountLinkRow가 색을 바꾸므로 흰 틴트용 탭 그림을 쓴다.
    /// 줄 (M80 · M22): 높이 96, 이름 28 + 설명 한 줄 20(최소 18, 380x40). 스위치 · 칸 고르기 줄은 줄 전체(100)가 누르는 자리 —
    /// 줄을 누르면 스위치를 바꾸거나 다음 칸을 고른다 (SettingsPopup). 칸 고르기 칸은 위아래 64까지 누르는 자리를 넓히고, 왼쪽 끝은 설명 칸(404) 밖(≥412).
    /// SPEC4 §4.1 (P4-4): [일반] '캐릭터 목소리'(VoiceRow, 소리 다음 — 목소리 클립이 없으면 흐리게 + '준비 중') · '처음 이야기 다시 보기'(IntroRow, 튜토리얼 다음),
    /// [알림] 상태 줄 NotifyStatus(+ '허용하기' / '설정 열기 ›') · 맨 아래 QuietNote, [계정·데이터] SaveButtons에 '공유'(Android만 보인다) ·
    /// BackupRow(712) · CouponRow(832) · PlayLogRow(952, D-58 #1 '플레이 기록 보내기' — 출시 빌드에도) · 초기화는 1188로.
    /// 세이브 가져오기 · 백업 복구 · 쿠폰 입력 팝업도 여기서 만든다 (ctx.Popups에 넣는다).
    /// [개발] 치트 버튼 8개 (에디터 / Development Build 전용). 탭은 켜짐 · 꺼짐 전용 그림이 둘 다 있으면 TabSkin을 붙인다 (ART-UPGRADE §4.3 #9 — MenuSceneBuilder.PageTabSkin).
    /// </summary>
    internal static class SettingsSceneBuilder
    {
        const float RowHeight = 96f;
        const float RowStep = 108f;
        /// <summary>설명 한 줄 칸 (M80: 380x40, 20 → 최소 18)</summary>
        static readonly Vector2 DescSize = new(380, 40);
        /// <summary>칸 고르기 줄 높이 (칸 = 54) · 칸 누르는 자리 높이 (M22)</summary>
        const float SegmentHeight = 62f;
        const float SegmentHitHeight = 64f;
        /// <summary>칸 고르기 사이 간격 (MetaSceneBuilder.Segmented의 줄 간격) · 안쪽 여백</summary>
        const float SegmentGap = 4f;
        const float SegmentInset = 4f;
        /// <summary>오른쪽 컨트롤 여백</summary>
        const float RightMargin = 20f;
        /// <summary>'준비 중' 알약 (소리가 없는 빌드 — 슬라이더 오른쪽 끝)</summary>
        static readonly Vector2 ReadyPillSize = new(120, 40);
        /// <summary>
        /// 설정 창 940x1576 (본문 1404 − 탭 96 = 쪽 1308): 가장 긴 쪽은 [계정 · 데이터] 1278 (D-58 #1 '플레이 기록 보내기' 줄 +120 → 30 남음).
        /// [일반] 쪽(줄 11개 → 1176 + '버전' 줄 40)은 92 남는다 (예전 1516: SPEC4 §4.1 '캐릭터 목소리' · '처음 이야기 다시 보기'로 1300에서 216 키움,
        /// D-58로 60 더 키움). 다른 쪽(개발 544 · 전투 연출 604 · 알림 560)도 들어간다
        /// </summary>
        static readonly Vector2 SettingsWindowSize = new(940, 1576);
        /// <summary>
        /// 계정·데이터 쪽 SPEC4 줄 (백업 복구 712 · 쿠폰 입력 832) · D-58 '플레이 기록 보내기' 952 ·
        /// 그 아래 구분선 / 초기화 머리 / 설명 / 버튼 (예전 자리 + 360)
        /// </summary>
        const float BackupRowTop = 712f;
        const float CouponRowTop = 832f;
        const float PlayLogRowTop = 952f;
        const float ResetShift = 360f;
        /// <summary>알림 쪽 상태 줄 버튼 ('허용하기' / '설정 열기 ›') 220x72 — 누르는 자리는 위아래 88까지</summary>
        static readonly Vector2 NotifyButtonSize = new(220, 72);
        /// <summary>줄 오른쪽 스위치 그림 (MetaSceneBuilder.Switch) — 누르는 자리는 위아래 18씩 88 (M22, 줄 96 안)</summary>
        static readonly Vector2 SwitchSize = new(108, 52);
        /// <summary>줄 오른쪽 버튼: 튜토리얼 '건너뛰기' · 계정 '연동' 180x80, 공지 '보기' 170x66 — 누르는 자리는 위아래로 88까지 (M22)</summary>
        static readonly Vector2 RowButtonSize = new(180, 80);
        static readonly Vector2 NoticeButtonSize = new(170, 66);

        public static UIPopup BuildSettingsPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<SettingsPopup>(ctx.PopupRoot, "SettingsPopup", "설정", SettingsWindowSize, out var body, out _);

            // 탭 (쪽 탭 모양 — 색 · 굵기는 SettingsPopup이 TabStyle.Apply로)
            string[] tabNames = { "일반", "전투 연출", "알림", "계정·데이터", "개발" };
            var tabRow = NewRect("Tabs", body).Top(0, 76);
            AddRow(tabRow, 10);
            var tabButtons = new Object[tabNames.Length];
            var tabImages = new Object[tabNames.Length];
            var tabLabels = new Object[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                var tab = Btn(tabRow, $"Tab{i}", tabNames[i], UITheme.TabNormal, 26, out var label);
                UiArt.Skin9((Image)tab.targetGraphic, UiArt.Tab9s, 1.8f, UITheme.TabNormal, UITheme.TabNormal);
                // 켜짐 · 꺼짐 전용 탭 그림이 둘 다 있으면 TabSkin (ART-UPGRADE §4.3 #9 — 없으면 지금처럼 색으로 칠한다)
                MenuSceneBuilder.PageTabSkin((Image)tab.targetGraphic, 1.8f);
                MetaSceneBuilder.Fit(label, 18);
                label.color = UITheme.SubText;
                tabButtons[i] = tab;
                tabImages[i] = tab.targetGraphic;
                tabLabels[i] = label;
            }
            var devTab = ((Button)tabButtons[tabNames.Length - 1]).gameObject;

            var general = Page(body, "GeneralPage");
            var battle = Page(body, "BattleFxPage");
            var notify = Page(body, "NotifyPage");
            var account = Page(body, "AccountPage");
            var dev = Page(body, "DevPage");

            // ── 일반 ── (잠자기 모드 아래에 '전투 화면으로 돌아가기' — UX-SPEC-IA §4.8)
            var bgm = VolumeRow(general, "BgmRow", "배경음", 0, out var bgmValue, out var bgmGroup, out var bgmReady);
            var sfx = VolumeRow(general, "SfxRow", "효과음", RowStep, out var sfxValue, out var sfxGroup, out var sfxReady);
            var soundRow = Row(general, "SoundRow", "소리", RowStep * 2, "끄면 볼륨과 상관없이 조용해져요");
            var sound = RowSwitch(soundRow);
            // 캐릭터 목소리 (AUDIO-SPEC §8.2 · SPEC4): 목소리 클립(vox_*)이 없으면 SettingsPopup이 스위치를 α0.5 · 누를 수 없게 + '준비 중' 알약
            var voiceRow = Row(general, "VoiceRow", "캐릭터 목소리", RowStep * 3, "친구들이 짧게 말해요");
            var voice = RowSwitch(voiceRow);
            var voiceGroup = voice.gameObject.AddComponent<CanvasGroup>();
            var voiceReady = ReadyPill(voiceRow, new Vector2(-(RightMargin + SwitchSize.x + 12f), 0));
            var hapticsRow = Row(general, "HapticsRow", "진동", RowStep * 4, "큰 순간에만 짧게");
            var haptics = RowSwitch(hapticsRow);
            var sleepRow = Row(general, "SleepRow", "잠자기 모드", RowStep * 5, "손대지 않으면 전투만 남기고 어둡게 해요");
            var sleep = Segment(sleepRow, new[] { "끄기", "1분", "2분", "3분", "5분" }, 22, 436);
            var autoReturnRow = Row(general, "SheetAutoReturnRow", "전투 화면으로 돌아가기", RowStep * 6, "1분 동안 손대지 않으면 열린 창을 내려요");
            var autoReturn = RowSwitch(autoReturnRow);
            var frameRow = Row(general, "FrameRow", "프레임", RowStep * 7, "30이면 배터리를 덜 써요");
            var frame = Segment(frameRow, new[] { "30", "60" }, 24, 220);
            var noticeRow = Row(general, "NoticeRow", "공지사항", RowStep * 8, "새 소식과 업데이트 안내");
            var notice = SceneParts.Button(noticeRow, "NoticeButton", "보기", ButtonStyle.Primary, UIType.ButtonM, out _);
            PlaceRight(notice.transform, NoticeButtonSize);
            notice.targetGraphic.HitPad(NoticeButtonSize, 0f, UISize.MinHit);
            UiArt.SetButtonArt(notice, UiArt.ButtonPrimary9s, UiArt.ButtonPpu(66));
            MetaSceneBuilder.AttachRedDot(notice.transform, RedDotPaths.TopSettingsNotice, false, new Vector2(8, 8), 26);
            // 튜토리얼 (M74): 설명은 SettingsPopup이 바꾼다 ('남은 안내 말풍선을 꺼요' / '처음 안내를 모두 마쳤어요')
            var tutorialRow = Row(general, "TutorialRow", "튜토리얼", RowStep * 9, "남은 안내 말풍선을 꺼요");
            var tutorialDesc = tutorialRow.Find("Desc").GetComponent<TextMeshProUGUI>();
            var tutorial = SceneParts.Button(tutorialRow, "TutorialButton", "건너뛰기", ButtonStyle.Secondary, UIType.Label, out var tutorialLabel);
            PlaceRight(tutorial.transform, RowButtonSize);
            tutorial.targetGraphic.HitPad(RowButtonSize, 0f, UISize.MinHit);
            UiArt.SetButtonArt(tutorial, UiArt.ButtonSecondary9s, UiArt.ButtonPpu(80));
            // 처음 이야기 다시 보기 (SPEC4 §4.1): 설정을 닫고 UINav.ReplayIntro (보상 · 깃발 없음). 튜토리얼 중에는 누를 수 없다 (인트로가 그 일부)
            var introRow = Row(general, "IntroRow", "처음 이야기 다시 보기", RowStep * 10, "수선점의 첫 이야기를 다시 볼 수 있어요");
            var intro = RowButton(introRow, "IntroButton", "보기", ButtonStyle.Secondary, UiArt.ButtonSecondary9s);
            var version = Txt(general, "VersionText", "버전", 24, UITheme.SubText, TextAlignmentOptions.MidlineRight);
            version.rectTransform.Bottom(0, 40);

            // ── 전투 연출 ──
            var shakeRow = Row(battle, "ShakeRow", "화면 흔들림", 0, "맞거나 불 밝힐 때 흔들림");
            var shake = Segment(shakeRow, new[] { "끄기", "50%", "100%" }, 24, 330);
            // 번쩍임 줄이기 (M64): '전투·뽑기·축하 연출의 번쩍임과 움직임을 줄여요 (눈 보호)'는 380 한 줄에 18로도 넘쳐 짧게 줄였다
            var flashRow = Row(battle, "FlashRow", "번쩍임 줄이기", RowStep, "번쩍임과 움직임을 줄여요 (눈 보호)");
            var flash = RowSwitch(flashRow);
            var damageRow = Row(battle, "DamageRow", "피해 숫자", RowStep * 2, "전투 중 떠오르는 숫자");
            var damage = Segment(damageRow, new[] { "모두", "치명타만", "끄기" }, 22, 380);
            var skillRow = Row(battle, "SkillNameRow", "스킬 이름 표시", RowStep * 3, "스킬 이름표와 대사 말풍선을 보여 줘요");
            var skillNames = RowSwitch(skillRow);
            // 이펙트 품질은 설명이 없어 칸 고르기가 왼쪽으로 더 넓어도 된다 (설명은 아래 줄)
            var qualityRow = Row(battle, "QualityRow", "이펙트 품질", RowStep * 4, null);
            var quality = Segment(qualityRow, new[] { "자동", "낮음", "보통", "높음" }, 22, 470);
            var qualityHint = Txt(battle, "QualityHint", "자동: 기기에 맞춰 정해요", UIType.Caption, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true);
            qualityHint.rectTransform.Top(RowStep * 5 - 6, 70, 24, 24);

            // ── 알림 ── 상태 줄 (SPEC4 §4.1 · §4.3): 권한에 따라 SettingsPopup이 글과 버튼('허용하기' / '설정 열기 ›', 켜져 있으면 숨김)을 바꾼다
            var notifyStatus = Txt(notify, "NotifyStatus", "아직 알림을 허용하지 않았어요", 24, UITheme.SubText, TextAlignmentOptions.MidlineLeft, false, true)
                .FitFloor(UIType.HintMin);
            notifyStatus.rectTransform.Top(0, 76, 8, NotifyButtonSize.x + 24);
            var notifyButton = SceneParts.Button(notify, "NotifyStatusButton", "허용하기", ButtonStyle.Primary, UIType.ButtonS, out var notifyButtonLabel);
            // 누르는 자리 88: 위아래 8 (위는 탭 줄과 쪽 사이 20 안, 아래는 첫 알림 줄 92 위에서 멈춘다)
            ((RectTransform)notifyButton.transform).At(TopRight, new Vector2(0, -6), NotifyButtonSize);
            notifyButton.targetGraphic.HitPad(NotifyButtonSize, 0f, UISize.MinHit, 0f, 0f, 8f, 8f);
            UiArt.SetButtonArt(notifyButton, UiArt.ButtonPrimary9s, UiArt.ButtonPpu(NotifyButtonSize.y));
            notifyButtonLabel.FitFloor(UIType.Hint);
            string[] notifyNames = { "수선 바구니가 가득 찼을 때", "원정이 돌아왔을 때", "던전 입장권이 새로 생겼을 때", "출석 도장을 아직 안 찍었을 때" };
            var notifySwitches = new Object[notifyNames.Length];
            var notifyRows = new Object[notifyNames.Length];
            for (int i = 0; i < notifyNames.Length; i++)
            {
                var row = Row(notify, $"NotifyRow{i}", notifyNames[i], 92 + RowStep * i, null);
                var view = RowSwitch(row);
                notifySwitches[i] = view;
                notifyRows[i] = RowHit(row);
            }
            Txt(notify, "QuietNote", "밤 10시부터 아침 8시까지는 알림을 보내지 않아요.", UIType.Hint, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true)
                .FitFloor(UIType.HintMin).rectTransform.Top(92 + RowStep * notifyNames.Length + 4, 40, 8, 8);

            // ── 계정·데이터 ──
            Txt(account, "AccountHeader", "계정 연동", 30, UITheme.Text, TextAlignmentOptions.MidlineLeft, true).rectTransform.Top(0, 50);
            var accountText = Txt(account, "AccountText", "게스트 계정", 26, UITheme.SubText, TextAlignmentOptions.MidlineLeft);
            accountText.rectTransform.Top(52, 44);
            var guestWarning = Txt(account, "GuestWarning", "게스트 계정은 앱 삭제·기기 변경 시 데이터를 잃을 수 있어요. 계정을 연동하거나 세이브 코드를 보관해 주세요.",
                22, UITheme.Negative, TextAlignmentOptions.TopLeft, false, true);
            guestWarning.rectTransform.Top(98, 64);
            var googleRow = BuildAccountRow(account, AuthProviderType.Google, "Google", Color.white, "G", new Color(0.26f, 0.52f, 0.96f), 176);
            var kakaoRow = BuildAccountRow(account, AuthProviderType.Kakao, "카카오", new Color(1f, 0.9f, 0f), "K", new Color(0.1f, 0.1f, 0.1f), 300);
            Img(account, "Divider", UITheme.Divider).rectTransform.Top(432, 2);

            Txt(account, "SaveHeader", "세이브 코드", 30, UITheme.Text, TextAlignmentOptions.MidlineLeft, true).rectTransform.Top(452, 50);
            Txt(account, "SaveDesc", "서버 없이 기기를 옮길 때 쓰는 코드예요. 내보낸 코드는 안전한 곳에 보관해 주세요.", 22, UITheme.SubText,
                TextAlignmentOptions.TopLeft, false, true).rectTransform.Top(504, 64);
            // 내보내기 · 공유(Android만 — 아니면 SettingsPopup이 숨겨 둘이 나눠 쓴다) · 가져오기 (SPEC4 §4.2: 셋이면 한 줄 3칸)
            var saveRow = NewRect("SaveButtons", account).Top(580, 100);
            AddRow(saveRow, 20);
            var export = Btn(saveRow, "ExportButton", "내보내기 (복사)", UITheme.Primary, 28);
            var share = Btn(saveRow, "ShareButton", "공유", UITheme.Secondary, 28);
            var import = Btn(saveRow, "ImportButton", "가져오기", UITheme.Secondary, 28);
            UiArt.SetButtonArt(export, UiArt.ButtonPrimary9s, UiArt.ButtonPpu(100));
            UiArt.SetButtonArt(share, UiArt.ButtonSecondary9s, UiArt.ButtonPpu(100));
            UiArt.SetButtonArt(import, UiArt.ButtonSecondary9s, UiArt.ButtonPpu(100));
            // 백업에서 복구 (SPEC4 §4.2): 최근 백업 한 줄 + 위험 색 보조 '복구' (백업이 없으면 누를 수 없음 — BackupRestorePopup)
            var backupRow = Row(account, "BackupRow", "백업에서 복구", BackupRowTop, "아직 백업이 없어요");
            var backupDesc = backupRow.Find("Desc").GetComponent<TextMeshProUGUI>();
            var backup = RowButton(backupRow, "BackupButton", "복구", ButtonStyle.Secondary, UiArt.ButtonSecondary9s);
            backup.GetComponentInChildren<TextMeshProUGUI>(true).color = UITheme.Negative;
            // 쿠폰 입력 (EVT-02 — 확장 항목: 줄은 보이지만 콘텐츠에 쿠폰이 없으면 '다시 확인해 주세요')
            var couponRow = Row(account, "CouponRow", "쿠폰 입력", CouponRowTop, "받은 쿠폰 번호를 넣으면 우편으로 선물이 와요");
            var coupon = RowButton(couponRow, "CouponButton", "입력", ButtonStyle.Primary, UiArt.ButtonPrimary9s);
            // 플레이 기록 보내기 (D-58 #1 — 개발 탭에서 옮김, 출시 빌드에도 보임): 최근 24시간 요약 → [내보내기] 공유 시트 · 복사 (SettingsPopup.ShowPlayLog)
            var playLogRow = Row(account, "PlayLogRow", "플레이 기록 보내기", PlayLogRowTop, "최근 기록을 공유하거나 복사해요");
            var playLog = RowButton(playLogRow, "PlayLogButton", "보내기", ButtonStyle.Secondary, UiArt.ButtonSecondary9s);
            Img(account, "Divider2", UITheme.Divider).rectTransform.Top(704 + ResetShift, 2);
            Txt(account, "ResetHeader", "데이터 초기화", 30, UITheme.Text, TextAlignmentOptions.MidlineLeft, true).rectTransform.Top(724 + ResetShift, 50);
            Txt(account, "ResetDesc", "모든 진행이 지워지고 처음부터 시작해요. 되돌릴 수 없어요.", 22, UITheme.SubText, TextAlignmentOptions.TopLeft, false, true)
                .rectTransform.Top(776 + ResetShift, 40);
            // 위험 버튼 (M55 · M82): DangerButton + Cream 글자 — 확인도 위험 확인(0.6초 잠금)
            var reset = SceneParts.Button(account, "ResetButton", "데이터 초기화", ButtonStyle.Danger, UIType.ButtonM, out _);
            ((RectTransform)reset.transform).At(TopLeft, new Vector2(0, -(828 + ResetShift)), new Vector2(320, 90));

            // ── 개발 ──
            Txt(dev, "Header", "개발자 메뉴 (에디터 / Development Build 전용)", 26, UITheme.Gold, TextAlignmentOptions.MidlineLeft, true)
                .rectTransform.Top(0, 50);
            var grid = NewRect("Buttons", dev).Top(64, 600);
            AddGrid(grid, new Vector2(424, 104), new Vector2(20, 18), 2, new RectOffset());
            var addGold = Btn(grid, "AddGoldButton", "단추 +10분치", UITheme.Secondary, 28);
            var addGem = Btn(grid, "AddGemButton", "별조각 +1,000", UITheme.Secondary, 28);
            var testMail = Btn(grid, "TestMailButton", "테스트 우편 받기", UITheme.Secondary, 28);
            var offline = Btn(grid, "OfflineTestButton", "오프라인 1시간 경과", UITheme.Secondary, 28);
            var nextDay = Btn(grid, "NextDayButton", "하루 넘기기", UITheme.Secondary, 28);
            var fillChest = Btn(grid, "FillChestButton", "바구니 가득 채우기", UITheme.Secondary, 28);
            var unlockAll = Btn(grid, "UnlockAllButton", "기능 모두 열기", UITheme.Secondary, 28);
            var tickets = Btn(grid, "TicketsButton", "수선권 받기", UITheme.Secondary, 28);
            // CR-09 '플레이 기록'은 D-58 #1로 [계정 · 데이터] 쪽 '플레이 기록 보내기'로 옮겼다 (격자 4줄 = 470)
            foreach (var devButton in new[] { addGold, addGem, testMail, offline, nextDay, fillChest, unlockAll, tickets })
                UiArt.SetButtonArt(devButton, UiArt.ButtonSecondary9s, UiArt.ButtonPpu(104));

            new Binder(popup)
                .SetArray("_tabButtons", tabButtons)
                .SetArray("_tabImages", tabImages)
                .SetArray("_tabLabels", tabLabels)
                .SetArray("_pages", general.gameObject, battle.gameObject, notify.gameObject, account.gameObject, dev.gameObject)
                .Set("_bgmSlider", bgm)
                .Set("_bgmValue", bgmValue)
                .Set("_sfxSlider", sfx)
                .Set("_sfxValue", sfxValue)
                .Set("_bgmGroup", bgmGroup)
                .Set("_sfxGroup", sfxGroup)
                .Set("_bgmReady", bgmReady)
                .Set("_sfxReady", sfxReady)
                .Set("_soundSwitch", sound)
                .Set("_hapticsSwitch", haptics)
                .Set("_sleepSegment", sleep)
                .Set("_sheetAutoReturnSwitch", autoReturn)
                .Set("_frameSegment", frame)
                .Set("_noticeButton", notice)
                .Set("_tutorialButton", tutorial)
                .Set("_tutorialLabel", tutorialLabel)
                .Set("_tutorialDesc", tutorialDesc)
                .Set("_voiceSwitch", voice)
                .Set("_voiceGroup", voiceGroup)
                .Set("_voiceReady", voiceReady)
                .Set("_voiceRow", RowHit(voiceRow))
                .Set("_introButton", intro)
                .Set("_versionText", version)
                .Set("_shakeSegment", shake)
                .Set("_reduceFlashSwitch", flash)
                .Set("_damageSegment", damage)
                .Set("_skillNamesSwitch", skillNames)
                .Set("_qualitySegment", quality)
                .Set("_qualityHint", qualityHint)
                .SetArray("_notifySwitches", notifySwitches)
                .Set("_notifyStatusText", notifyStatus)
                .Set("_notifyStatusButton", notifyButton)
                .Set("_notifyStatusLabel", notifyButtonLabel)
                // 줄 전체 누르기 (M22): 스위치 줄 → 스위치, 칸 고르기 줄 → 다음 칸 (같은 순서로 짝)
                .SetArray("_switchRows", RowHit(soundRow), RowHit(hapticsRow), RowHit(autoReturnRow), RowHit(flashRow), RowHit(skillRow),
                    notifyRows[0], notifyRows[1], notifyRows[2], notifyRows[3])
                .SetArray("_rowSwitches", sound, haptics, autoReturn, flash, skillNames,
                    notifySwitches[0], notifySwitches[1], notifySwitches[2], notifySwitches[3])
                .SetArray("_segmentRows", RowHit(sleepRow), RowHit(frameRow), RowHit(shakeRow), RowHit(damageRow), RowHit(qualityRow))
                .SetArray("_rowSegments", sleep, frame, shake, damage, quality)
                .Set("_accountText", accountText)
                .Set("_guestWarning", guestWarning.gameObject)
                .SetArray("_accountRows", googleRow, kakaoRow)
                .Set("_exportButton", export)
                .Set("_shareButton", share)
                .Set("_importButton", import)
                .Set("_backupButton", backup)
                .Set("_backupDesc", backupDesc)
                .Set("_couponButton", coupon)
                .Set("_resetButton", reset)
                .Set("_devTab", devTab)
                .Set("_addGoldButton", addGold)
                .Set("_addGemButton", addGem)
                .Set("_testMailButton", testMail)
                .Set("_offlineTestButton", offline)
                .Set("_nextDayButton", nextDay)
                .Set("_fillChestButton", fillChest)
                .Set("_unlockAllButton", unlockAll)
                .Set("_ticketsButton", tickets)
                .Set("_playLogButton", playLog)
                .Apply();

            // 계정·데이터 쪽에서 여는 팝업 (SPEC4 §4.2 · §4.5) — 설정보다 뒤에 만들어 위에 뜬다 (열 때 UIManager가 맨 위로 올린다)
            ctx.Popups.Add(BuildSaveImportPopup(ctx));
            ctx.Popups.Add(BuildBackupRestorePopup(ctx));
            ctx.Popups.Add(BuildCouponPopup(ctx));
            return popup;
        }

        // ═════════════════════════ SPEC4 팝업 (SAVE-01 · EVT-02) ═════════════════════════

        static readonly Vector2 SaveImportWindowSize = new(820, 900);
        static readonly Vector2 BackupWindowSize = new(820, 1000);
        static readonly Vector2 CouponWindowSize = new(820, 560);
        /// <summary>큰 버튼 520x112 (붙여 넣기 · 바꾸기 · 받기)</summary>
        static readonly Vector2 WideButtonSize = new(520, 112);
        /// <summary>백업 목록 줄 740x120 · 사이 12 · 미리 만들어 두는 줄 수 (매일 3 + 가져오기 전 + 마지막 저장 직전 — 넉넉히 8, 넘치면 최근 8개)</summary>
        static readonly Vector2 BackupRowSize = new(740, 120);
        const float BackupRowGap = 12f;
        const int BackupRows = 8;

        /// <summary>
        /// 세이브 코드 가져오기 (SPEC4 §4.2, 820x900): 안내 Body 26 · [클립보드에서 붙여 넣기] Primary 520x112 ·
        /// 미리보기 판 740x260 (PanelInset9s — 요약 Title 28 / 저장 시각 · ID Caption 22, 아니면 안내 · 오류 한 줄) · [이 진행으로 바꾸기] Danger 520x112 (읽기 전에는 누를 수 없음).
        /// 큰 입력 칸은 두지 않는다 (코드는 100KB까지 된다)
        /// </summary>
        static UIPopup BuildSaveImportPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<SaveImportPopup>(ctx.PopupRoot, "SaveImportPopup", "세이브 코드 가져오기", SaveImportWindowSize, out var body, out _);
            Txt(body, "Desc", "다른 기기에서 복사한 세이브 코드를 붙여 넣어 주세요.", UIType.Body, UITheme.Text, TextAlignmentOptions.Center, false, true)
                .FitFloor(UIType.Hint).rectTransform.Top(0, 44);
            var paste = SceneParts.Button(body, "PasteButton", "클립보드에서 붙여 넣기", ButtonStyle.Primary, UIType.ButtonM, out _);
            ((RectTransform)paste.transform).At(TopCenter, new Vector2(0, -64), WideButtonSize);
            UiArt.SetButtonArt(paste, UiArt.ButtonPrimary9s, UiArt.ButtonPpu(WideButtonSize.y));

            var card = UiArt.Panel(body, "PreviewCard", UiArt.PanelInset9s, 2f, null, MetaUi.Alpha(UITheme.PanelLight, 0.6f));
            card.rectTransform.At(TopCenter, new Vector2(0, -200), new Vector2(740, 260));
            var preview = NewRect("Preview", card.transform).Stretch(28, 28, 0, 0);
            var summary = Txt(preview, "Summary", string.Empty, UIType.Title, UITheme.Cream, TextAlignmentOptions.Center, true).Ellipsis().FitFloor(UIType.Label);
            summary.rectTransform.At(Center, new Vector2(0, 26), new Vector2(684, 44));
            var saved = Txt(preview, "Saved", string.Empty, UIType.Caption, UITheme.SubText, TextAlignmentOptions.Center).Ellipsis().FitFloor(UIType.HintMin);
            saved.rectTransform.At(Center, new Vector2(0, -26), new Vector2(684, 34));
            preview.SetInactive();
            var hint = Txt(card.transform, "Hint", SaveImportPopup.HintEmpty, UIType.Label, UITheme.SubText, TextAlignmentOptions.Center, false, true)
                .FitFloor(UIType.HintMin);
            hint.rectTransform.Stretch(28, 28, 20, 20);

            var replace = SceneParts.Button(body, "ReplaceButton", "이 진행으로 바꾸기", ButtonStyle.Danger, UIType.ButtonM, out _);
            ((RectTransform)replace.transform).At(BottomCenter, Vector2.zero, WideButtonSize);

            new Binder(popup)
                .Set("_pasteButton", paste)
                .Set("_previewGroup", preview.gameObject)
                .Set("_summaryText", summary)
                .Set("_savedText", saved)
                .Set("_hintText", hint)
                .Set("_replaceButton", replace)
                .Apply();
            return popup;
        }

        /// <summary>
        /// 백업에서 복구 (SPEC4 §4.2, 820x1000): 안내 한 줄 + 백업 줄 740x120 × 8 (스크롤 — 이름 Title 28 '어제' / '가져오기 전' … · 요약 Caption 22 ·
        /// 보조 '복구' 180x80, 위험 색 글자) + 백업이 없을 때 '아직 백업이 없어요'. 줄은 BackupRestorePopup이 채우고 남는 줄은 숨긴다
        /// </summary>
        static UIPopup BuildBackupRestorePopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<BackupRestorePopup>(ctx.PopupRoot, "BackupRestorePopup", "백업에서 복구", BackupWindowSize, out var body, out _);
            Txt(body, "Desc", "복구하기 전 진행도 따로 남겨 둬요", UIType.Caption, UITheme.SubText, TextAlignmentOptions.Center)
                .FitFloor(UIType.HintMin).rectTransform.Top(0, 36);
            var list = NewRect("List", body).Stretch(0, 0, 52, 0);
            var content = ScrollList(list, out _);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = BackupRowGap;
            layout.padding = new RectOffset(4, 4, 4, 12);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            // (ScrollList가 붙인 ContentSizeFitter가 높이를 맞춘다)

            var rows = new Object[BackupRows];
            var labels = new Object[BackupRows];
            var summaries = new Object[BackupRows];
            var buttons = new Object[BackupRows];
            for (int i = 0; i < BackupRows; i++)
            {
                var row = UiArt.Panel(content, $"BackupRow_{i}", UiArt.PanelInset9s, 1.8f, null, MetaUi.Alpha(UITheme.PanelLight, 0.55f));
                row.rectTransform.sizeDelta = BackupRowSize;
                var label = Txt(row.transform, "Label", "백업", UIType.Title, UITheme.Text, TextAlignmentOptions.MidlineLeft, true).Ellipsis().FitFloor(UIType.Label);
                label.rectTransform.At(TopLeft, new Vector2(24, -14), new Vector2(500, 44));
                var summary = Txt(row.transform, "Summary", string.Empty, UIType.Caption, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis()
                    .FitFloor(UIType.HintMin);
                summary.rectTransform.At(TopLeft, new Vector2(24, -64), new Vector2(500, 40));
                var button = SceneParts.Button(row.transform, "RestoreButton", "복구", ButtonStyle.Secondary, UIType.Label, out var buttonLabel);
                ((RectTransform)button.transform).At(MiddleRight, new Vector2(-RightMargin, 0), RowButtonSize);
                button.targetGraphic.HitPad(RowButtonSize, 0f, UISize.MinHit);
                UiArt.SetButtonArt(button, UiArt.ButtonSecondary9s, UiArt.ButtonPpu(RowButtonSize.y));
                buttonLabel.color = UITheme.Negative;
                row.SetInactive();
                rows[i] = row.gameObject;
                labels[i] = label;
                summaries[i] = summary;
                buttons[i] = button;
            }
            var empty = Txt(list, "EmptyLabel", "아직 백업이 없어요", UIType.Body, UITheme.SubText);
            empty.rectTransform.Stretch();
            empty.SetInactive();

            new Binder(popup)
                .SetArray("_rows", rows)
                .SetArray("_rowLabels", labels)
                .SetArray("_rowSummaries", summaries)
                .SetArray("_rowButtons", buttons)
                .Set("_emptyLabel", empty.gameObject)
                .Apply();
            return popup;
        }

        /// <summary>
        /// 쿠폰 입력 (SPEC4 §4.5, 820x560): TMP_InputField 600x96 (영문 대문자 · 숫자 16자, 자리표시 '쿠폰 번호') · 결과 한 줄 Caption 22 · [받기] Gold 520x112.
        /// 대문자 · 글자 거르기는 CouponPopup이 onValidateInput으로 한다 (번호 다듬기는 CouponSystem)
        /// </summary>
        static UIPopup BuildCouponPopup(SceneBuildContext ctx)
        {
            var popup = BuildPopup<CouponPopup>(ctx.PopupRoot, "CouponPopup", "쿠폰 입력", CouponWindowSize, out var body, out _);
            var input = BuildCodeInput(body, "CodeInput", "쿠폰 번호", CouponInputSize);
            ((RectTransform)input.transform).At(TopCenter, new Vector2(0, -8), CouponInputSize);
            var feedback = Txt(body, "Feedback", string.Empty, UIType.Caption, UITheme.SubText, TextAlignmentOptions.Center, false, true).FitFloor(UIType.HintMin);
            feedback.rectTransform.Top(116, 60);
            var redeem = SceneParts.Button(body, "RedeemButton", "받기", ButtonStyle.Gold, UIType.ButtonL, out _);
            ((RectTransform)redeem.transform).At(BottomCenter, Vector2.zero, WideButtonSize);

            new Binder(popup)
                .Set("_input", input)
                .Set("_redeemButton", redeem)
                .Set("_feedbackText", feedback)
                .Apply();
            return popup;
        }

        /// <summary>쿠폰 번호 칸 600x96 (SPEC4 §4.5)</summary>
        static readonly Vector2 CouponInputSize = new(600, 96);

        /// <summary>
        /// 한 줄 입력 칸 (TMP_InputField): 보라 바느질 안쪽 판 + TextArea(RectMask2D) 안 자리표시(SubText Body) · 글(Cream 36 굵게, 가운데).
        /// 한 줄 · 영숫자 · 16자 (빌드 값 — CouponPopup이 Awake에서 다시 정한다)
        /// </summary>
        static TMP_InputField BuildCodeInput(RectTransform parent, string name, string placeholder, Vector2 size)
        {
            var bg = UiArt.Panel(parent, name, UiArt.PanelInset9s, 1.8f, null, MetaUi.Alpha(UITheme.PanelLight, 0.8f), true);
            bg.rectTransform.sizeDelta = size;
            var area = NewRect("TextArea", bg.transform).Stretch(24, 24, 10, 10);
            area.gameObject.AddComponent<RectMask2D>();
            var hintText = Txt(area, "Placeholder", placeholder, UIType.Body, UITheme.SubText, TextAlignmentOptions.Center);
            hintText.rectTransform.Stretch();
            var text = Txt(area, "Text", string.Empty, 36, UITheme.Cream, TextAlignmentOptions.Center, true);
            text.rectTransform.Stretch();
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = hintText;
            input.fontAsset = text.font;
            input.pointSize = 36;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Alphanumeric;
            input.characterLimit = CouponPopup.MaxLength;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            return input;
        }

        /// <summary>줄 오른쪽 버튼 180x80 (튜토리얼 '건너뛰기'와 같은 모양) — 누르는 자리는 위아래 88까지 (M22)</summary>
        static Button RowButton(RectTransform row, string name, string label, ButtonStyle style, Sprite art)
        {
            var button = SceneParts.Button(row, name, label, style, UIType.Label, out _);
            PlaceRight(button.transform, RowButtonSize);
            button.targetGraphic.HitPad(RowButtonSize, 0f, UISize.MinHit);
            UiArt.SetButtonArt(button, art, UiArt.ButtonPpu(RowButtonSize.y));
            return button;
        }

        /// <summary>'준비 중' 알약 120x40 Caption 22 (꺼 둠 — 소리 · 목소리 클립이 없을 때만, M18). position = 줄 오른쪽 끝에서</summary>
        static GameObject ReadyPill(RectTransform row, Vector2 position)
        {
            var pill = UiArt.Panel(row, "ReadyPill", UiArt.PillNight9s, UiArt.CapsulePpu(ReadyPillSize.y), MetaUi.Alpha(Color.white, 0.92f),
                MetaUi.Alpha(UITheme.Background, 0.9f));
            pill.rectTransform.At(MiddleRight, position, ReadyPillSize);
            Txt(pill.transform, "Text", "준비 중", UIType.Caption, UITheme.Cream, TextAlignmentOptions.Center, true).rectTransform.Stretch(8, 8, 0, 0);
            pill.SetInactive();
            return pill.gameObject;
        }

        static RectTransform Page(RectTransform body, string name)
        {
            var page = NewRect(name, body).Stretch(0, 0, 96, 0);
            return page;
        }

        /// <summary>
        /// 설정 한 줄 (보라 바느질 안쪽 판 + 왼쪽 이름 28 + 설명 한 줄 20(최소 18) 380x40). 컨트롤은 오른쪽에 붙인다. 아트가 없으면 옅은 둥근 판.
        /// 설명은 줄을 바꾸지 않는다 (넘치면 … — 문구는 380에 들어가게 짧게 쓴다, M80).
        /// </summary>
        static RectTransform Row(RectTransform page, string name, string label, float top, string desc)
        {
            // 계정 연동 줄과 같은 판 (높이 96 → 테두리 48 / 1.8 ≈ 27px)
            var row = UiArt.Panel(page, name, UiArt.PanelInset9s, 1.8f, null, MetaUi.Alpha(UITheme.PanelLight, 0.55f)).rectTransform.Top(top, RowHeight);
            bool hasDesc = !string.IsNullOrEmpty(desc);
            var title = MetaSceneBuilder.Fit(Txt(row, "Label", label, 28, UITheme.Text, TextAlignmentOptions.MidlineLeft, true), 20);
            title.rectTransform.At(TopLeft, new Vector2(24, hasDesc ? -10 : -24), new Vector2(330, 46));
            if (hasDesc)
            {
                var sub = Txt(row, "Desc", desc, UIType.Hint, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis();
                MetaSceneBuilder.Fit(sub, UIType.HintMin);
                sub.rectTransform.At(TopLeft, new Vector2(24, -52), DescSize);
            }
            return row;
        }

        /// <summary>줄 전체를 누르는 자리로 (판 그림에 누르기 + 위아래 2px — 줄 사이 12의 가운데까지, 100). 컨트롤은 자식이라 그 위에서 먼저 받는다</summary>
        static Button RowHit(RectTransform row)
        {
            var existing = row.GetComponent<Button>();
            if (existing != null) return existing;
            var image = row.GetComponent<Image>();
            image.raycastTarget = true;
            image.raycastPadding = new Vector4(0f, -2f, 0f, -2f);
            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        static void PlaceRight(Transform control, Vector2 size)
        {
            ((RectTransform)control).At(MiddleRight, new Vector2(-RightMargin, 0), size);
        }

        /// <summary>
        /// 줄 오른쪽 스위치 (MetaSceneBuilder.Switch 108x52): 막대 그림의 누르는 자리를 위아래 18씩 넓혀 88 (M22 — 줄 96 안이라 이웃 줄과 겹치지 않는다).
        /// 스위치를 눌러도 줄을 눌러도 같은 일 (SettingsPopup의 줄 전체 누르기)
        /// </summary>
        static MetaSwitch RowSwitch(RectTransform row)
        {
            var view = MetaSceneBuilder.Switch(row, "Switch");
            PlaceRight(view.transform, SwitchSize);
            var button = view.GetComponent<Button>();
            if (button != null && button.targetGraphic != null) button.targetGraphic.HitPad(SwitchSize, 0f, UISize.MinHit);
            return view;
        }

        /// <summary>
        /// 오른쪽 칸 고르기 (높이 62): 칸마다 누르는 자리를 위아래 64까지, 옆으로는 칸 사이 간격의 절반까지 (M22).
        /// width는 설명이 있는 줄이면 왼쪽 끝이 412 이상이 되게 (868 − 20 − width ≥ 412 → width ≤ 436).
        /// </summary>
        static MetaSegmentedControl Segment(RectTransform row, string[] labels, float fontSize, float width)
        {
            var view = MetaSceneBuilder.Segmented(row, "Segment", labels, fontSize);
            PlaceRight(view.transform, new Vector2(width, SegmentHeight));
            float optionWidth = (width - SegmentInset * 2f - SegmentGap * (labels.Length - 1)) / labels.Length;
            var optionSize = new Vector2(optionWidth, SegmentHeight - SegmentInset * 2f);
            foreach (var button in view.GetComponentsInChildren<Button>(true))
            {
                if (button.targetGraphic == null) continue;
                button.targetGraphic.HitPad(optionSize, UISize.HitTarget, SegmentHitHeight,
                    SegmentGap * 0.5f, SegmentGap * 0.5f, SegmentHitHeight * 0.5f, SegmentHitHeight * 0.5f);
            }
            return view;
        }

        /// <summary>
        /// 볼륨 줄: 이름 + 슬라이더(380x60, CanvasGroup — 소리가 없는 빌드면 SettingsPopup이 α0.5 · 누를 수 없게) + 값 +
        /// 슬라이더 오른쪽 끝의 '준비 중' 알약 22 (꺼 둠 — 소리가 없을 때만, M18).
        /// </summary>
        static Slider VolumeRow(RectTransform page, string name, string label, float top, out TextMeshProUGUI value, out CanvasGroup group, out GameObject ready)
        {
            var row = Row(page, name, label, top, null);
            var slider = MetaSceneBuilder.BuildSlider(row, "Slider");
            ((RectTransform)slider.transform).At(MiddleRight, new Vector2(-100, 0), new Vector2(380, 60));
            group = slider.gameObject.AddComponent<CanvasGroup>();
            value = Txt(row, "Value", "70", 28, UITheme.Cream, TextAlignmentOptions.MidlineRight, true);
            value.rectTransform.At(MiddleRight, new Vector2(-RightMargin, 0), new Vector2(70, 50));
            ready = ReadyPill(row, new Vector2(-100, 0));
            return slider;
        }

        /// <summary>계정 연동 한 줄: 아이콘 / 이름 / 상태 / 연동·해제 버튼</summary>
        static AccountLinkRow BuildAccountRow(RectTransform parent, AuthProviderType provider, string label,
            Color iconColor, string letter, Color letterColor, float top)
        {
            var card = UiArt.Panel(parent, $"{provider}LinkRow", UiArt.PanelInset9s, 1.8f, null, UITheme.PanelLight).rectTransform.Top(top, 110);
            var row = card.gameObject.AddComponent<AccountLinkRow>();

            // 계정 표시: 어두운 메달 홈(ui_medallion_dark, 흰색 그대로) 안에 계정 색 글자.
            // 흰 원 계정(Google)은 글자 색을, 색 원 계정(카카오)은 원 색을 글자에 써서 어두운 홈 위에서 읽히게 한다. 아트가 없으면 예전 단색 원
            var medallion = UiArt.MedallionDark;
            bool medallionArt = UiArt.IsArt(medallion);
            var iconImage = Img(card, "Icon", medallionArt ? Color.white : iconColor, medallion);
            iconImage.preserveAspect = medallionArt;
            var icon = iconImage.rectTransform.At(MiddleLeft, new Vector2(20, 0), new Vector2(72, 72));
            var glyphColor = !medallionArt ? letterColor
                : iconColor.grayscale > 0.95f ? Color.Lerp(letterColor, Color.white, 0.3f) : iconColor;
            Txt(icon, "Letter", letter, medallionArt ? 32 : 34, glyphColor, TextAlignmentOptions.Center, true).rectTransform.Stretch();
            Txt(card, "NameText", label, 30, UITheme.Text, TextAlignmentOptions.MidlineLeft, true)
                .rectTransform.At(TopLeft, new Vector2(108, -12), new Vector2(400, 46));
            var status = Txt(card, "StatusText", "연동 안 됨", 24, UITheme.SubText, TextAlignmentOptions.MidlineLeft).Ellipsis();
            status.rectTransform.At(BottomLeft, new Vector2(108, 12), new Vector2(480, 40));
            var button = Btn(card, "LinkButton", "연동", UITheme.Primary, 30, out var buttonLabel);
            ((RectTransform)button.transform).At(MiddleRight, new Vector2(-20, 0), RowButtonSize);
            button.targetGraphic.HitPad(RowButtonSize, 0f, UISize.MinHit);
            // AccountLinkRow가 연동 상태에 따라 버튼 색(보라/어두운 보라)을 바꾸므로 흰 틴트용 바느질 판 그림을 쓴다
            UiArt.Skin9((Image)button.targetGraphic, UiArt.Tab9s, UiArt.ButtonPpu(80), UITheme.Primary, UITheme.Primary);

            new Binder(row)
                .SetInt("_provider", (int)provider)
                .Set("_statusText", status)
                .Set("_button", button)
                .Set("_buttonLabel", buttonLabel)
                .Apply();
            return row;
        }
    }
}
