using IdleGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// WP-4 메뉴 (UX Wave 2) 순수 규칙 — 장면 없이 도우미 함수만 본다.
    ///   · M21: 상단 바 재화 표시 키 (100만 미만은 내림 정수 — 12,345와 12,399가 다른 키, 그 위는 유효숫자 3자리) · 세어 오르기 문턱 (1%, 100 미만은 1).
    ///   · M34: 강화 탭 친구 카드 정렬 (가리키는 계보 → 편성 칸 순서 → 편성 밖 등급 높은 순 → ID).
    ///   · M57 · M05: 원정 자동 선택 순서 (나가 있지 않음 → '원정 N회' 많이 남음 → 전투력 → ID).
    ///   · M58: 원정 탭 처음 쪽 (입장권이 남았는데 원정 자리가 모두 찼을 때만 던전).
    ///   · M39 · M28: 잠긴 구역 안내 '스테이지 2-9 클리어 시 / 열려요' (도달 기준 — 앞 스테이지, 번호는 줄이 갈리지 않게).
    ///   · M30: 보상 칸 수량 '×5' (작은 칩은 작은 곱하기).
    ///   · M74: 튜토리얼 건너뛰기 확인 글 (6줄 이하, 첫 만남 안내 두 갈래, 졸업 선물).
    ///   · M28: 시너지 설명의 능력치 이름과 수치('공격력 +8%' · '-8%' · '×1.5')를 줄바꿈 없는 빈칸으로 붙인다.
    ///   · RM-2 P4-4 (SPEC4): 보상 창 한 줄 높이 · 설정 알림 상태 / 백업 줄 / 목소리 클립 · 세이브 가져오기 글 · 쿠폰 입력 거르기 / 결과 ·
    ///     원정 다녀올 시간 단계 · 모두 다시 보내기 보임 · 던전 여러 장 소탕 · 권장 줄 · 새 글의 말투 규칙('습니다' · '개방' · '장비' · '하시겠' 없음).
    ///   · RM-2 P4-4 Part B: ≡ 메뉴 SPEC4 칸 (줄 수 · 창 1180 · EventTile 길) · 편성 칸 반짝 (CR-01 칸 범위 · 시간) · 오늘의 작은 선물 (다음 선물 줄 · 3초 소리 ·
    ///     상품 목록 윗변) · 개발 '플레이 기록' 본문 · Part B 글의 말투 규칙.
    /// </summary>
    public class UiWave2_WP4Tests
    {
        // ───────────── M21 상단 바 재화 ─────────────

        [Test]
        public void GoldDisplayKey_BelowMillion_IsFlooredInteger()
        {
            Assert.AreEqual(12345d, TopBarView.GoldDisplayKey(12345.9));
            Assert.AreNotEqual(TopBarView.GoldDisplayKey(12345), TopBarView.GoldDisplayKey(12399),
                "100만 미만은 콤마 정수 표기 — 12,345 → 12,399가 같은 키면 글자가 멈춘다");
            Assert.AreEqual(999999d, TopBarView.GoldDisplayKey(999999.99));
        }

        [Test]
        public void GoldDisplayKey_FromMillion_KeepsThreeSignificantDigits()
        {
            Assert.AreEqual(1000000d, TopBarView.GoldDisplayKey(1000000), 1e-6);
            Assert.AreEqual(1230000d, TopBarView.GoldDisplayKey(1234567), 1e-3);
            Assert.AreEqual(TopBarView.GoldDisplayKey(1234567), TopBarView.GoldDisplayKey(1239999), 1e-3,
                "같은 '1.23B' 표기면 같은 키 (매 프레임 글을 다시 쓰지 않는다)");
            Assert.AreNotEqual(TopBarView.GoldDisplayKey(999999), TopBarView.GoldDisplayKey(1000000));
        }

        [Test]
        public void IsBigChange_UsesOnePercentOrOneBelowHundred()
        {
            Assert.IsTrue(TopBarView.IsBigChange(1000, 1011));
            Assert.IsFalse(TopBarView.IsBigChange(1000, 1009));
            Assert.IsTrue(TopBarView.IsBigChange(1000, 980), "줄어듦도 같은 문턱");
            Assert.IsFalse(TopBarView.IsBigChange(50, 51), "100 미만은 1보다 크게 바뀌어야");
            Assert.IsTrue(TopBarView.IsBigChange(50, 52));
            Assert.IsTrue(TopBarView.IsBigChange(0, 5));
        }

        // ───────────── M34 강화 탭 친구 정렬 ─────────────

        [Test]
        public void CompareLineages_PriorityThenSlotThenGradeThenId()
        {
            var priority = new GrowthPanel.LineageSortKey("z_pick", true, -1, 0);
            var slot0 = new GrowthPanel.LineageSortKey("b", false, 0, 0);
            var slot3 = new GrowthPanel.LineageSortKey("a", false, 3, 4);
            var legendary = new GrowthPanel.LineageSortKey("c", false, -1, (int)ItemGrade.Legendary);
            var rareA = new GrowthPanel.LineageSortKey("d1", false, -1, (int)ItemGrade.Rare);
            var rareB = new GrowthPanel.LineageSortKey("d2", false, -1, (int)ItemGrade.Rare);

            Assert.Less(GrowthPanel.CompareLineages(priority, slot0), 0, "가리키는 계보가 맨 앞");
            Assert.Less(GrowthPanel.CompareLineages(slot0, slot3), 0, "편성 칸 순서");
            Assert.Less(GrowthPanel.CompareLineages(slot3, legendary), 0, "편성된 계보가 편성 밖보다 앞 (등급과 상관없이)");
            Assert.Less(GrowthPanel.CompareLineages(legendary, rareA), 0, "편성 밖은 등급 높은 순");
            Assert.Less(GrowthPanel.CompareLineages(rareA, rareB), 0, "같으면 ID 순");
            Assert.AreEqual(0, GrowthPanel.CompareLineages(rareA, rareA));
        }

        // ───────────── M57 · M05 원정 자동 선택 ─────────────

        [Test]
        public void AutoPickKey_NotDispatchedThenNeedThenPowerThenId()
        {
            var away = new ExpeditionSetupPopup.AutoPickKey(true, 9, 99999, "a");
            var needy = new ExpeditionSetupPopup.AutoPickKey(false, 3, 100, "b");
            var strong = new ExpeditionSetupPopup.AutoPickKey(false, 0, 5000, "c");
            var weak = new ExpeditionSetupPopup.AutoPickKey(false, 0, 100, "d");
            var weakTwin = new ExpeditionSetupPopup.AutoPickKey(false, 0, 100, "e");

            Assert.Greater(ExpeditionSetupPopup.AutoPickKey.Compare(away, weak), 0, "원정에 나가 있는 친구는 맨 뒤");
            Assert.Less(ExpeditionSetupPopup.AutoPickKey.Compare(needy, strong), 0, "'원정 N회'가 많이 남은 친구 먼저");
            Assert.Less(ExpeditionSetupPopup.AutoPickKey.Compare(strong, weak), 0, "그다음 전투력");
            Assert.Less(ExpeditionSetupPopup.AutoPickKey.Compare(weak, weakTwin), 0, "같으면 ID");
        }

        // ───────────── M58 원정 탭 처음 쪽 ─────────────

        [Test]
        public void LaunchPage_DungeonOnlyWhenTicketsAndAllSlotsBusy()
        {
            Assert.AreEqual(ExpeditionPanel.PageExpedition, ExpeditionPanel.LaunchPage(2, 1, 3));
            Assert.AreEqual(ExpeditionPanel.PageExpedition, ExpeditionPanel.LaunchPage(0, 3, 3), "입장권이 없으면 원정 쪽");
            Assert.AreEqual(ExpeditionPanel.PageDungeon, ExpeditionPanel.LaunchPage(1, 3, 3));
            // 셋 다 돌아와 받기만 남았으면(가는 중 3 − 받을 3 = 0) 자리가 빈 것 — 받기가 있는 원정 쪽
            Assert.AreEqual(ExpeditionPanel.PageExpedition, ExpeditionPanel.LaunchPage(2, 3 - 3, 3), "받을 원정이 있으면 원정 쪽");
            Assert.AreEqual(ExpeditionPanel.PageExpedition, ExpeditionPanel.LaunchPage(2, 3 - 1, 3), "하나라도 받을 수 있으면 원정 쪽");
        }

        // ───────────── M39 · M28 잠긴 구역 ─────────────

        [Test]
        public void LockLabel_UsesPreviousStageAndKeepsNumberTogether()
        {
            Assert.AreEqual(string.Empty, ExpeditionZoneCardView.LockLabel(1), "처음부터 열린 구역은 안내가 없다");
            string label = ExpeditionZoneCardView.LockLabel(10);
            StringAssert.Contains("클리어 시\n열려요", label);
            StringAssert.Contains("<nobr>" + BattleSystem.FormatStage(9) + "</nobr>", label, "열리는 스테이지의 앞 스테이지를 깨면 (도달 기준)");
            StringAssert.DoesNotContain("개방", label);
        }

        // ───────────── M30 수량 ─────────────

        [Test]
        public void CountLabel_UsesTimesSign()
        {
            Assert.AreEqual(UIFormat.Times + "5", RewardSlotView.CountLabel(5, false));
            Assert.AreEqual(UIFormat.Times + "1,200", RewardSlotView.CountLabel(1200, false));
            StringAssert.StartsWith(UIFormat.TimesSmall, RewardSlotView.CountLabel(5, true), "작은 칩은 작은 곱하기");
            StringAssert.DoesNotContain("x", RewardSlotView.CountLabel(3, true));
        }

        // ───────────── M74 튜토리얼 건너뛰기 ─────────────

        [Test]
        public void SkipTutorialBody_AtMostSixLines()
        {
            string body = SettingsPopup.SkipTutorialBody(true, false, "별조각 300");
            Assert.LessOrEqual(body.Split('\n').Length, 6);
            StringAssert.StartsWith("남은 안내 말풍선이 더 나오지 않아요.", body);
            StringAssert.Contains("첫 만남 소환(전설 우두커니 확정)을 받을 수 없어요.", body);
            StringAssert.Contains("졸업 선물(별조각 300)은 그대로 받아요", body);
            StringAssert.EndsWith("건너뛸까요?", body);
        }

        [Test]
        public void SkipTutorialBody_FirstMeetingKeptOrAlreadyPast()
        {
            string kept = SettingsPopup.SkipTutorialBody(true, true, string.Empty);
            StringAssert.Contains("첫 만남 소환은 소환이 열리면 그대로 받을 수 있어요", kept);
            StringAssert.DoesNotContain("받을 수 없어요", kept);
            StringAssert.DoesNotContain("졸업 선물", kept, "선물이 없으면 그 줄도 없다");

            string past = SettingsPopup.SkipTutorialBody(false, false, string.Empty);
            StringAssert.DoesNotContain("첫 만남", past, "첫 만남을 지났으면 안내하지 않는다");
            Assert.AreEqual(3, past.Split('\n').Length);
        }

        // ───────────── M28 시너지 수치 붙이기 ─────────────

        [Test]
        public void KeepStatNumbers_JoinsLabelAndNumber()
        {
            string nb = UIFormat.Nbsp;
            // Wave 3 WP-P: 여러 낱말 능력치 이름('잔향이 유지 시간' · '스킬 쿨타임')도 안 빈칸까지 묶는다
            Assert.AreEqual("밤 특성 친구 잔향이" + nb + "유지" + nb + "시간" + nb + "+15%, 공격력" + nb + "+8%",
                SynergyUI.KeepStatNumbers("밤 특성 친구 잔향이 유지 시간 +15%, 공격력 +8%"), "'공격력' / '+8%'로 갈라지지 않게");
            Assert.AreEqual("스킬" + nb + "쿨타임" + nb + "-8%", SynergyUI.KeepStatNumbers("스킬 쿨타임 -8%"));
            Assert.AreEqual("효과" + nb + "×1.5 · 전투력" + nb + "−80", SynergyUI.KeepStatNumbers("효과 ×1.5 · 전투력 −80"));
            StringAssert.Contains("<color=#AABBCC> · </color>", SynergyUI.KeepStatNumbers("2<color=#AABBCC> · </color>3 공격력 +5%"),
                "다른 빈칸 (가운뎃점 둘레 · 태그 안)은 그대로");
        }

        [Test]
        public void KeepStatNumbers_ReturnsSameStringWhenNothingToJoin()
        {
            const string plain = "천 특성 친구들이 서로 기워 줘요.";
            Assert.AreSame(plain, SynergyUI.KeepStatNumbers(plain), "바꿀 것이 없으면 새 문자열을 만들지 않는다");
            Assert.AreEqual(string.Empty, SynergyUI.KeepStatNumbers(null));
            Assert.AreEqual("+5%", SynergyUI.KeepStatNumbers("+5%"), "맨 앞 수치는 그대로");
        }

        // ───────────── RM-2 P4-4 (SPEC4) ─────────────

        [Test]
        public void RewardPopup_OneRowShrinksWindow_TwoRowsKeepBuiltHeight()
        {
            const float built = 860f;
            float compact = RewardPopup.WindowChrome + RewardPopup.CompactBody;
            Assert.AreEqual(648f, compact, 0.01f, "머리 172 + 글 92 + 칸 한 줄 216 + 40 + 버튼 줄 128");
            for (int cells = 0; cells <= RewardPopup.CompactMaxCells; cells++)
                Assert.AreEqual(compact, RewardPopup.WindowHeightFor(cells, built), 0.01f, $"{cells}칸 = 한 줄");
            Assert.AreEqual(built, RewardPopup.WindowHeightFor(RewardPopup.CompactMaxCells + 1, built), 0.01f, "두 줄이면 빌드 높이");
            Assert.AreEqual(500f, RewardPopup.WindowHeightFor(1, 500f), 0.01f, "빌드 높이가 더 작으면 키우지 않는다");
            Assert.AreEqual(0f, RewardPopup.WindowHeightFor(1, 0f), "창이 없으면 그대로");
        }

        [Test]
        public void SettingsNotifyStatus_SameRuleAsPlanner()
        {
            // P4-7 R2: 설정 창 권한 줄 = 예약 계획과 같은 규칙 (LocalNotifications.EffectivePermission)
            Assert.AreEqual(NotificationPermission.SoftDeclined,
                SettingsPopup.EffectivePermission(NotificationPermission.Granted, NotificationPermission.SoftDeclined),
                "기기가 허용해도 게임 안에서 허락받기 전에는 '허용하기'");
            Assert.AreEqual(NotificationPermission.Unknown,
                SettingsPopup.EffectivePermission(NotificationPermission.Granted, NotificationPermission.Unknown),
                "Android 12 이하 (기기는 늘 허용) · 아직 묻지 않음 = '허용하기'");
            Assert.AreEqual(NotificationPermission.Denied,
                SettingsPopup.EffectivePermission(NotificationPermission.Denied, NotificationPermission.Granted), "기기가 꺼 두면 저장 값과 상관없이 꺼짐");
            Assert.AreEqual(NotificationPermission.Granted,
                SettingsPopup.EffectivePermission(NotificationPermission.Granted, NotificationPermission.Denied), "기기 설정에서 다시 켰으면 켜짐");
            Assert.AreEqual(NotificationPermission.SoftDeclined,
                SettingsPopup.EffectivePermission(NotificationPermission.Unknown, NotificationPermission.SoftDeclined), "기기가 모르면 저장된 값");
            foreach (var device in new[] { NotificationPermission.Unknown, NotificationPermission.Granted, NotificationPermission.Denied })
            foreach (var saved in new[] { NotificationPermission.Unknown, NotificationPermission.SoftDeclined, NotificationPermission.Granted, NotificationPermission.Denied })
                Assert.AreEqual(LocalNotifications.EffectivePermission(device, saved), SettingsPopup.EffectivePermission(device, saved),
                    $"계획과 같은 답 ({device}, {saved})");
            Assert.AreEqual("알림이 켜져 있어요", SettingsPopup.NotifyStatusText(NotificationPermission.Granted));
            Assert.IsNull(SettingsPopup.NotifyStatusButtonText(NotificationPermission.Granted), "켜져 있으면 버튼 없음");
            Assert.AreEqual("기기 설정에서 알림이 꺼져 있어요", SettingsPopup.NotifyStatusText(NotificationPermission.Denied));
            Assert.AreEqual("설정 열기 ›", SettingsPopup.NotifyStatusButtonText(NotificationPermission.Denied));
            foreach (var asked in new[] { NotificationPermission.Unknown, NotificationPermission.SoftDeclined })
            {
                Assert.AreEqual("아직 알림을 허용하지 않았어요", SettingsPopup.NotifyStatusText(asked));
                Assert.AreEqual("허용하기", SettingsPopup.NotifyStatusButtonText(asked));
            }
        }

        [Test]
        public void SettingsBackupLine_NoneOrNewestWithStage()
        {
            Assert.AreEqual("아직 백업이 없어요", SettingsPopup.BackupLine(null));
            var info = new SaveBackupInfo { Path = "p", Label = "어제", Summary = new SaveSummary { highestStage = 24, shopLevel = 87 } };
            Assert.AreEqual("최근 백업 · 스테이지 " + BattleSystem.FormatStage(24), SettingsPopup.BackupLine(info), "시각이 없으면 시각을 뺀다");
            info.SavedUnix = 1762085640; // 2025-11-02 12:14 UTC
            string line = SettingsPopup.BackupLine(info);
            StringAssert.StartsWith("최근 백업: ", line);
            StringAssert.Contains("월 ", line);
            StringAssert.EndsWith(" · 스테이지 " + BattleSystem.FormatStage(24), line);
        }

        [Test]
        public void SettingsVoiceClips_OnlyVoxKeysWithClips()
        {
            Assert.IsFalse(SettingsPopup.HasVoiceClips(null));
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            var clip = AudioClip.Create("vox_test", 64, 1, 8000, false);
            try
            {
                library.entries = new[]
                {
                    new AudioLibrary.Entry { key = "ui_tap", takes = new[] { new AudioLibrary.Take { clip = clip } } },
                    new AudioLibrary.Entry { key = "vox_jjakjjak_hello" },
                };
                Assert.IsFalse(SettingsPopup.HasVoiceClips(library), "목소리 키에 클립이 없으면 '준비 중'");
                library.entries[1].takes = new[] { new AudioLibrary.Take { clip = clip } };
                Assert.IsTrue(SettingsPopup.HasVoiceClips(library), "목소리 클립이 하나라도 있으면 켤 수 있다");
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void SaveImport_ErrorLinesAndPreview()
        {
            Assert.AreEqual(SaveImportPopup.ErrorBroken, SaveImportPopup.ErrorText(SaveCodeError.Checksum));
            Assert.AreEqual(SaveImportPopup.ErrorBroken, SaveImportPopup.ErrorText(SaveCodeError.BadFormat));
            Assert.AreEqual(SaveImportPopup.ErrorNewer, SaveImportPopup.ErrorText(SaveCodeError.NewerVersion));
            Assert.AreEqual(SaveImportPopup.ErrorUnreadable, SaveImportPopup.ErrorText(SaveCodeError.TooLarge));
            Assert.AreEqual(SaveImportPopup.ErrorUnreadable, SaveImportPopup.ErrorText(SaveCodeError.Corrupt));
            Assert.AreEqual(SaveImportPopup.ErrorEmpty, SaveImportPopup.ErrorText(SaveCodeError.Empty));
            var summary = new SaveSummary { highestStage = 24, shopLevel = 87, lineages = 6, accountIdShort = "1a2b3c4d" };
            Assert.AreEqual($"스테이지 {BattleSystem.FormatStage(24)} · 수선점 Lv.87 · 친구 6명", SaveImportPopup.SummaryLine(summary));
            Assert.AreEqual("ID 1a2b3c4d", SaveImportPopup.SavedLine(summary), "저장 시각이 없으면 ID만");
            summary.savedUnix = 1762085640;
            string saved = SaveImportPopup.SavedLine(summary);
            StringAssert.StartsWith("저장 20", saved);
            StringAssert.EndsWith(" · ID 1a2b3c4d", saved);
            summary.accountIdShort = null;
            StringAssert.DoesNotContain("ID", SaveImportPopup.SavedLine(summary));
        }

        /// <summary>Replace만 흉내 내는 저장소 (SaveImportPopup.TryReplace 시험용 — P4-7 R1)</summary>
        sealed class FakeReplaceStorage : ISaveStorage
        {
            public bool Result;
            public bool Throws;
            public int Calls;
            public string Reason;

            public SaveLoadResult LastLoadResult => SaveLoadResult.Loaded;
            public PlayerData Load() => null;
            public void Save(PlayerData data) { }
            public void Delete() { }

            public bool Replace(PlayerData data, string reason)
            {
                Calls++;
                Reason = reason;
                if (Throws) throw new System.InvalidOperationException("replace boom");
                return Result;
            }

            public void SnapshotDaily(string dayKey) { }
            public System.Collections.Generic.IReadOnlyList<SaveBackupInfo> ListBackups() => System.Array.Empty<SaveBackupInfo>();
            public bool RestoreBackup(string path) => false;
        }

        [Test]
        public void SaveImport_ReplaceFalse_DoesNotReload()
        {
            var data = new PlayerData();
            int reloads = 0;
            var refuses = new FakeReplaceStorage { Result = false };
            Assert.IsFalse(SaveImportPopup.TryReplace(refuses, data, () => reloads++), "Replace false = 지금 진행을 백업하지 못해 아무것도 바뀌지 않음");
            Assert.AreEqual(1, refuses.Calls);
            Assert.AreEqual(0, reloads, "false면 다시 불러오지 않는다 ('진행을 옮겨 왔어요'가 뜨지 않게)");
            Assert.IsFalse(SaveImportPopup.TryReplace(null, data, () => reloads++), "저장소가 없으면 실패");
            var unused = new FakeReplaceStorage { Result = true };
            Assert.IsFalse(SaveImportPopup.TryReplace(unused, null, () => reloads++), "읽은 진행이 없으면 실패");
            Assert.AreEqual(0, unused.Calls, "진행이 없으면 저장소를 부르지 않는다");
            Assert.AreEqual(0, reloads);

            var ok = new FakeReplaceStorage { Result = true };
            Assert.IsTrue(SaveImportPopup.TryReplace(ok, data, () => reloads++));
            Assert.AreEqual(1, reloads, "바꿨을 때만 한 번 다시 불러온다");
            Assert.AreEqual("import", ok.Reason, "가져오기 백업 이름 (player.json.import-{유닉스}.bak)");
        }

        [Test]
        public void SaveImport_ReplaceThrows_DoesNotReload()
        {
            int reloads = 0;
            var throws = new FakeReplaceStorage { Throws = true };
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[SaveImport\]"));
            bool replaced = SaveImportPopup.TryReplace(throws, new PlayerData(), () => reloads++);
            Assert.IsFalse(replaced, "예외도 실패로 — 밖으로 내지 않는다 (킬 스위치)");
            Assert.AreEqual(1, throws.Calls);
            Assert.AreEqual(0, reloads, "예외면 다시 불러오지 않는다");
        }

        [Test]
        public void Coupon_InputFilterAndFeedback()
        {
            Assert.AreEqual('A', CouponPopup.ValidateChar(string.Empty, 0, 'a'), "소문자는 대문자로");
            Assert.AreEqual('Z', CouponPopup.ValidateChar(string.Empty, 0, 'Z'));
            Assert.AreEqual('7', CouponPopup.ValidateChar(string.Empty, 0, '7'));
            Assert.AreEqual('\0', CouponPopup.ValidateChar(string.Empty, 0, '-'), "기호는 버린다 (다듬기는 CouponSystem)");
            Assert.AreEqual('\0', CouponPopup.ValidateChar(string.Empty, 0, ' '));
            Assert.AreEqual('\0', CouponPopup.ValidateChar(string.Empty, 0, '가'));
            Assert.AreEqual(16, CouponPopup.MaxLength);
            Assert.AreEqual("쿠폰 선물을 우편함에 넣어 두었어요", CouponPopup.FeedbackText(CouponResult.Ok));
            Assert.AreEqual("쿠폰 번호를 다시 확인해 주세요", CouponPopup.FeedbackText(CouponResult.Invalid));
            Assert.AreEqual("이미 받은 쿠폰이에요", CouponPopup.FeedbackText(CouponResult.AlreadyUsed));
            Assert.AreEqual("사용 기간이 아닌 쿠폰이에요", CouponPopup.FeedbackText(CouponResult.OutOfPeriod));
            Assert.AreEqual("잠시 뒤에 다시 입력해 주세요", CouponPopup.FeedbackText(CouponResult.Locked));
            Assert.AreEqual(DailyClock.TamperedMessage, CouponPopup.FeedbackText(CouponResult.Tampered));
        }

        [Test]
        public void ExpeditionTier_DefaultLabelsAndScale()
        {
            const int all = 0b111;
            Assert.AreEqual(2, ExpeditionSetupPopup.DefaultTier(2, all), "지난번 단계 그대로");
            Assert.AreEqual(0, ExpeditionSetupPopup.DefaultTier(2, 0b011), "지난번 단계를 고를 수 없으면 (12시간 넘음) 가장 낮은 단계");
            Assert.AreEqual(1, ExpeditionSetupPopup.DefaultTier(0, 0b110), "기본도 막혔으면 고를 수 있는 가장 낮은 단계");
            Assert.AreEqual(0, ExpeditionSetupPopup.DefaultTier(7, 0b001), "모르는 단계는 기본");
            Assert.AreEqual(0, ExpeditionSetupPopup.DefaultTier(1, 0), "고를 수 있는 단계가 없으면 0");
            Assert.AreEqual("기본", ExpeditionSetupPopup.TierLabel(0));
            Assert.AreEqual("길게", ExpeditionSetupPopup.TierLabel(1));
            Assert.AreEqual("밤새", ExpeditionSetupPopup.TierLabel(2));
            Assert.AreEqual("보상 " + UIFormat.Times + "1", ExpeditionSetupPopup.TierScaleLabel(0));
            Assert.AreEqual("보상 " + UIFormat.Times + "2.8", ExpeditionSetupPopup.TierScaleLabel(1));
            Assert.AreEqual("보상 " + UIFormat.Times + "5.4", ExpeditionSetupPopup.TierScaleLabel(2));
        }

        [Test]
        public void ResendAll_ShownOnlyInClaimAllsPlaceWhenNothingToClaim()
        {
            Assert.IsTrue(ExpeditionPanel.ShowResendAll(true, 0, true, 2));
            Assert.IsFalse(ExpeditionPanel.ShowResendAll(true, 1, true, 2), "받을 원정이 있으면 모두 받기 차례");
            Assert.IsFalse(ExpeditionPanel.ShowResendAll(true, 0, false, 2), "잠겼으면 숨김");
            Assert.IsFalse(ExpeditionPanel.ShowResendAll(true, 0, true, 0), "다시 보낼 구역이 없으면 숨김");
            Assert.IsFalse(ExpeditionPanel.ShowResendAll(false, 0, true, 2), "던전 쪽에서는 숨김");
        }

        [Test]
        public void DungeonSweep_CountClampAndLabel()
        {
            Assert.AreEqual(1, DungeonSweepPopup.ClampCount(5, 3, false), "잠겼으면 1장");
            Assert.AreEqual(3, DungeonSweepPopup.ClampCount(5, 3, true), "남은 입장권까지");
            Assert.AreEqual(1, DungeonSweepPopup.ClampCount(0, 3, true), "최소 1장");
            Assert.AreEqual(1, DungeonSweepPopup.ClampCount(4, 0, true), "입장권이 없어도 1 (버튼은 꺼진다)");
            Assert.AreEqual("소탕", DungeonSweepPopup.SweepLabel(3, false));
            Assert.AreEqual("3장 소탕", DungeonSweepPopup.SweepLabel(3, true));
            Assert.AreEqual("12,345", PowerSystem.Format(12345));
            Assert.AreEqual("권장 12,345 · 잔향이 9,876", DungeonCardView.RecLine(12345, 9876));
        }

        [Test]
        public void DungeonTicketBank_LabelUsesCapAndRefillLine()
        {
            // NEW-04 (스트레치, SPEC4 §13.9): 켜지면 '입장권 {n}/9' · '새벽 5시에 +3장', 꺼져 있으면 지금처럼 '{n}/{하루}'
            Assert.AreEqual(3, DungeonCardView.TicketMax(false, 9, 3), "모아 두기가 꺼져 있으면 하루 입장권");
            Assert.AreEqual(9, DungeonCardView.TicketMax(true, 9, 3), "켜지면 상한 (9/3 같은 글이 나오지 않게)");
            Assert.IsTrue(DungeonCardView.TicketLabel(7, DungeonCardView.TicketMax(true, 9, 3)).EndsWith("7</color>/9"));
            Assert.AreEqual(3, DungeonCardView.BankRefill(2, 9, 3), "모자란 만큼이 하루 입장권보다 많으면 +3");
            Assert.AreEqual(1, DungeonCardView.BankRefill(8, 9, 3), "상한까지 남은 만큼만");
            Assert.AreEqual(0, DungeonCardView.BankRefill(11, 9, 3), "수요일 +2로 상한을 넘었으면 0 (알약 숨김)");
            Assert.AreEqual("새벽 5시에 +3장", DungeonCardView.BankRefillLine(3));
            Assert.AreEqual(0, DungeonCardView.TicketMax(null), "던전이 없으면 0 (예외 없음)");
        }

        // ───────────── RM-2 P4-4 Part B ─────────────

        [Test]
        public void MenuTiles_RowsWindowAndEventRoute()
        {
            Assert.AreEqual(1, MenuPopup.RowsFor(0), "칸이 없어도 한 줄");
            Assert.AreEqual(2, MenuPopup.RowsFor(6), "v1 여섯 칸 = 두 줄");
            Assert.AreEqual(4, MenuPopup.RowsFor(10), "수첩(스트레치)이 닫힌 SPEC4 메뉴 열 칸 = 네 줄");
            Assert.AreEqual(4, MenuPopup.RowsFor(11));
            Assert.AreEqual(740f, MenuPopup.WindowHeight(MenuPopup.RowsFor(6)), 1e-3f);
            Assert.AreEqual(1180f, MenuPopup.WindowHeight(MenuPopup.RowsFor(11)), 1e-3f, "SPEC4 칸까지 = 네 줄 1180 (AutoShot IA-menu)");
            Assert.IsTrue(MenuPopup.EventTileGoesToShop(true, true), "이벤트 모음 칸이 보이면 EventTile은 이벤트 상점 (두 칸이 같은 곳으로 가지 않게)");
            Assert.IsFalse(MenuPopup.EventTileGoesToShop(false, true), "모음 칸이 없는 예전 장면에서 모음이 열렸으면 EventTile이 모음으로");
            Assert.IsTrue(MenuPopup.EventTileGoesToShop(false, false), "모음이 아직 없으면 지금처럼 이벤트 상점");
            Assert.AreNotEqual(MenuPopup.EventShopTileLabel, MenuPopup.EventHubTileLabel, "두 이벤트 칸의 이름이 달라야 한다");
            Assert.IsFalse(MenuPopup.EventTileDotSuppressed(false), "모음 칸이 숨은 동안은 EventTile이 이벤트 점(NEW)을 보여 준다");
            Assert.IsTrue(MenuPopup.EventTileDotSuppressed(true), "모음 칸이 보이면 그 칸이 점을 맡는다 (이벤트 상점 길에는 점 없음)");
        }

        [Test]
        public void FormationSlotPulse_SlotRangeAndSeconds()
        {
            Assert.IsFalse(FormationPanel.IsPulseSlot(-1));
            Assert.IsTrue(FormationPanel.IsPulseSlot(0));
            Assert.IsTrue(FormationPanel.IsPulseSlot(BattleGeometry.SlotCount - 1));
            Assert.IsFalse(FormationPanel.IsPulseSlot(BattleGeometry.SlotCount), "방위 칸은 다섯 (0~4)");
            Assert.AreEqual(2f, FormationPanel.ClampSlotPulseSeconds(float.NaN), 1e-6f);
            Assert.AreEqual(2f, FormationPanel.ClampSlotPulseSeconds(0f), 1e-6f, "0 이하는 기본 2초");
            Assert.AreEqual(2f, FormationPanel.ClampSlotPulseSeconds(2f), 1e-6f);
            Assert.AreEqual(0.5f, FormationPanel.ClampSlotPulseSeconds(0.1f), 1e-6f);
            Assert.AreEqual(6f, FormationPanel.ClampSlotPulseSeconds(60f), 1e-6f);
            Assert.AreEqual(1.06f, FormationSlotView.SlotPulseScale, 1e-6f, "SPEC4 §7.2: 1 → 1.06");
            Assert.DoesNotThrow(() => FormationPanel.PulseGuardianSlot(BattleGeometry.SlotCount, 2f), "범위 밖 칸은 조용히 무시");
        }

        [Test]
        public void DailyGift_NextLineCountdownAndListTop()
        {
            Assert.AreEqual(DailyGiftCardView.NextLineTomorrow, DailyGiftCardView.NextLine(System.TimeSpan.FromHours(20)));
            Assert.AreEqual(DailyGiftCardView.NextLineToday, DailyGiftCardView.NextLine(System.TimeSpan.FromHours(4)), "자정이 지난 새벽에는 '내일' 없이");
            Assert.AreEqual(DailyGiftCardView.NextLineToday, DailyGiftCardView.NextLine(System.TimeSpan.FromHours(1)));
            string clock = DailyGiftCardView.NextLine(System.TimeSpan.FromMinutes(12) + System.TimeSpan.FromSeconds(34));
            StringAssert.StartsWith(DailyGiftCardView.CountdownPrefix, clock, "1시간 안이면 시계");
            Assert.AreEqual(DailyGiftCardView.CountdownPrefix + UIFormat.ClockMono(System.TimeSpan.Zero), DailyGiftCardView.NextLine(System.TimeSpan.FromSeconds(-5)),
                "음수는 0");
            Assert.IsFalse(DailyGiftCardView.IsCountdownTick(0));
            Assert.IsTrue(DailyGiftCardView.IsCountdownTick(1));
            Assert.IsTrue(DailyGiftCardView.IsCountdownTick(3));
            Assert.IsFalse(DailyGiftCardView.IsCountdownTick(4), "소리는 3 · 2 · 1초만");
            Assert.AreEqual(168f, ShopPanel.ListTop(false, false), 1e-3f);
            Assert.AreEqual(300f, ShopPanel.ListTop(false, true), 1e-3f, "선물 카드가 보이면 상품 목록이 132 내려간다 (SPEC4 §13.7)");
            Assert.AreEqual(312f, ShopPanel.ListTop(true, true), 1e-3f, "이벤트 쪽은 카드가 없다");
        }

        [Test]
        public void PlayLogDevBody_EmptyFallsBack()
        {
            Assert.AreEqual(SettingsPopup.PlayLogEmptyText, SettingsPopup.PlayLogBody(null));
            Assert.AreEqual(SettingsPopup.PlayLogEmptyText, SettingsPopup.PlayLogBody(string.Empty));
            Assert.AreEqual(SettingsPopup.PlayLogEmptyText, SettingsPopup.PlayLogBody(" \n"));
            const string summary = "최근 24시간 · 스테이지 클리어 42 · 실패 3 · 시간당 6.1스테이지";
            Assert.AreEqual(summary, SettingsPopup.PlayLogBody(summary));
        }

        [Test]
        public void Spec4PartBCopy_FollowsToneRules()
        {
            string[] lines =
            {
                MenuPopup.EventTileLabel, MenuPopup.EventShopTileLabel, MenuPopup.EventHubTileLabel, MenuPopup.SeasonTileLabel, MenuPopup.CodexTileLabel,
                MenuPopup.AchievementsTileLabel, MenuPopup.CouponTileLabel,
                DailyGiftCardView.TitleText, DailyGiftCardView.ReadyLine, DailyGiftCardView.NextLineTomorrow, DailyGiftCardView.NextLineToday,
                DailyGiftCardView.CountdownPrefix, ShopPanel.FestivalPillText,
                SettingsPopup.PlayLogTitle, SettingsPopup.PlayLogEmptyText, SettingsPopup.PlayLogExportLabel, SettingsPopup.PlayLogCopiedText,
                SaveImportPopup.ReplaceFailedText,
            };
            foreach (var line in lines)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(line));
                foreach (var banned in new[] { "습니다", "개방", "장비", "하시겠" })
                    StringAssert.DoesNotContain(banned, line, $"'{line}'");
            }
        }

        [Test]
        public void Spec4Copy_FollowsToneRules()
        {
            string[] lines =
            {
                SettingsPopup.NotifyGrantedText, SettingsPopup.NotifyDeniedText, SettingsPopup.NotifyAskText, SettingsPopup.NoBackupText,
                SaveImportPopup.HintEmpty, SaveImportPopup.ErrorEmpty, SaveImportPopup.ErrorBroken, SaveImportPopup.ErrorNewer, SaveImportPopup.ErrorUnreadable,
                CouponPopup.OkText, CouponPopup.EmptyText, CouponPopup.UnavailableText, BackupRestorePopup.FallbackLabel,
                CouponPopup.FeedbackText(CouponResult.Invalid), CouponPopup.FeedbackText(CouponResult.AlreadyUsed),
                CouponPopup.FeedbackText(CouponResult.OutOfPeriod), CouponPopup.FeedbackText(CouponResult.Locked),
                ExpeditionSetupPopup.TierLabel(1), ExpeditionSetupPopup.TierLabel(2), DungeonSweepPopup.SweepLabel(2, true),
                DungeonCardView.BankRefillLine(3),
            };
            foreach (var line in lines)
            {
                Assert.IsFalse(string.IsNullOrEmpty(line));
                foreach (var banned in new[] { "습니다", "개방", "장비", "하시겠" })
                    StringAssert.DoesNotContain(banned, line, $"'{line}'");
            }
        }
    }
}
