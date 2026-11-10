using System.Collections.Generic;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// FTUE-01 첫 10분 (0~16단계, 인덱스 = 단계 번호 = meta.ftue.step).
    /// 원칙: 말풍선 최대 2줄·한 줄 40자·존댓말, 늘 손가락, 전투는 멈추지 않음, 졸업 전에는 시작 공지 없음.
    ///
    /// | # | 단계 | 시작 | 완료 | 할 일 |
    /// |---|---|---|---|---|
    /// | 0 | 동화 인트로 3장 | 바로 | 인트로를 봄 | StoryIntro (text = 장마다 한 줄, \n으로 3줄) |
    /// | 1 | 첫 전투 (슬로건) | 바로 | 적 3 처치 | BattleProtection |
    /// | 2 | 잔향이 소환 (덱 첫 칸 손가락) | 바로 | 소환 1 | — |
    /// | 3 | 방향 소환 힌트 (선택) | 2초 뒤 | 없음 | 토스트 힌트 |
    /// | 4 | 불 밝히기 | 5초 뒤 | 불 밝히기 1 | ForceLampFull |
    /// | 5 | 첫 보스 | 바로 | 1-1 클리어 | 토스트 힌트 → 가이드 퀘스트 줄 등장 |
    /// | 6 | 수선점 강화 (강화 x1 손가락) | 1.5초 뒤 | 강화 3 | 보상 단추 250 (7단계 비용 보장, M06) |
    /// | 7 | 짝짝이 레벨 5 (선물 받은 단추로) | 바로 | 짝짝이 Lv.5 | — |
    /// | 8 | 자동 전투 | 자동 전투 개방(1-2) | 없음 | EnableAutoBattle + 토스트 |
    /// | 9 | 수선 바구니 | 바구니 개방(1-2) | 받기 1 | PrefillIdleChest 2시간 |
    /// | 10 | 첫 만남 소환 | 소환 개방(1-3) | 첫 만남 카드를 엶 (플래그 FirstMeetingSummoned) | FirstMeetingSummon 우두커니(전설) 확정 |
    /// | 11 | 편성 (= 편성·시너지 개방) | 바로 | 지킴이 4명 | — |
    /// | 12 | 시너지 보기 | 바로 | 시너지 상세를 열어 봄 | — |
    /// | 13 | 원정 | 원정 개방(1-4) | 이 단계에서 원정 1 (플래그 ShortFirstExpeditionUsed) | ShortFirstExpedition 1분 |
    /// | 14 | 원정 받기 | 바로 | 원정 받기 1 | 토스트 힌트 (돌아올 때까지 조용히 기다림) |
    /// | 15 | 빠른 수선 | 빠른 수선 개방(1-5) | 빠른 수선 1 | — |
    /// | 16 | 졸업 | 스테이지 사이 | 없음 | Graduation + 별조각 300 |
    ///
    /// 할 일이 플레이어 손을 거쳐야 하는 단계(10 첫 만남 · 13 첫 원정)는 평생 카운터(GachaPull / ExpeditionStart)로 끝내지 않는다.
    /// 단계는 하나씩 진행되고 입력을 막지 않으므로, 앞 단계(예: 7 짝짝이 Lv.5)에서 멈춘 동안 스테이지가 넘어가 소환·원정이 열리고
    /// 플레이어가 직접 1회 소환·원정을 하면, 그 단계가 시작되는 순간 조건이 이미 채워져 같은 프레임에 끝난다 —
    /// 우두커니 확정 소환이 영영 실행되지 않거나(11단계가 없는 친구를 세우라고 함) 1분 원정이 적용되지 않는다.
    /// 그래서 그 할 일이 끝날 때 FtueDirector(M4)가 세우는 플래그로 끝낸다 (MetaDataBuilder.ValidateFtue가 확인).
    /// SPEC3 §4.6 제안 매핑(10 = Counter GachaPull 1, 13 = Counter ExpeditionStart 1)에서 바꾼 부분이다.
    ///
    /// ForceLampFull · BattleProtection은 전투 쪽 정식 훅이 맡는다 (RM-2 S-9). 보호(적 체력 60% · 수선점 최소 1)는 시뮬레이터가
    /// BattleSystem.ProtectionResolver → BattleSetup.Protection으로 건다 — 게임에서는 MetaSession.Connect가 resolver를 넣고
    /// (메인 스테이지 ≤ 3 · 튜토리얼 보호 단계), 메타 연결 전에는 FtueBattleGuard가 같은 규칙의 것을 넣는다.
    /// 4단계 게이지는 FtueDirector가 8마리째 · 4초 뒤 BattleSystem.ForceLampFull로 채운다 (메인 스테이지 · 전투 중 · 게이지 &lt; 1일 때만).
    /// 문구는 게이지가 저절로 차도 맞게 썼다 ("게이지가 차면 버튼이 반짝여요"). ShortFirstExpedition은 FtueDirector가 처음 보낸 원정의 끝 시각을
    /// 당기고 플래그를 세운다 — 13단계가 그 플래그로 끝나므로 이 단계는 FtueDirector가 있어야 넘어간다.
    /// blockInput은 모두 끈다 — 잔향이 모자라거나 게이지가 덜 찬 순간에 화면이 잠겨 멈추지 않게 (구멍 뚫린 어둠 + 손가락만).
    /// 담당: M2.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>편성 단계 인덱스. 편성·시너지 개방 조건 FtueStep(11) = 0~10단계를 끝냄 = 편성 단계가 시작됨</summary>
        internal const int FtueStepFormation = 11;

        /// <summary>가이드 퀘스트 줄이 보이는 조건 FtueStep(6) = 0~5단계(첫 보스)를 끝냄</summary>
        internal const int FtueStepGuideVisible = 6;

        /// <summary>FtueStepFormation이 가리켜야 하는 단계 ID (검증용)</summary>
        internal const string FtueFormationStepId = "ftue_formation";

        /// <summary>FtueStepGuideVisible이 가리켜야 하는 단계 ID = 첫 보스 다음 단계 (검증용)</summary>
        internal const string FtueAfterFirstBossStepId = "ftue_shop_upgrade";

        /// <summary>튜토리얼 첫 만남 확정 친구 (전설 우두커니 — 짝짝이와 "기다림의 끝" 시너지)</summary>
        const string FirstMeetingLineage = "udukeoni";

        static partial void FillFtue(MetaDatabase db, GameContent content)
        {
            var s = db.ftueSteps;

            // 0 동화 인트로 — 밤 골목 → 수선점 불 → 짝짝이가 문을 엶 (장마다 한 줄, 화면을 누르면 넘김, 약 20초)
            s.Add(Step("ftue_intro",
                "잊힌 물건들이 길을 잃는 밤이 있어요\n" +
                "그런 밤이면 골목 끝 작은 수선점에 불이 켜져요\n" +
                "\"어서 와요! 오늘 밤 수선점을 함께 지켜 줄래요?\"",
                MetaCondition.Flag(MetaFlags.IntroSeen), action: FtueAction.StoryIntro));

            // 1 첫 전투 — 지킴이 3명이 이미 서 있다. 슬로건 말풍선 (1-1~1-3 질 수 없음)
            s.Add(Step("ftue_first_battle",
                "지킴이로 방향을 지키고, 잔향이로 위기를 막아\n수선점을 밝혀 주세요!",
                MetaCondition.Count(CounterKey.EnemyKill, 3), action: FtueAction.BattleProtection));

            // 2 잔향이 소환 — 덱 첫 칸(몽당이)에 손가락
            s.Add(Step("ftue_first_summon",
                "잔향을 써서 잔향이를 불러요\n덱의 첫 칸을 톡 눌러 보세요",
                MetaCondition.Count(CounterKey.EchoSummon, 1), target: CoachTargets.DeckCard0));

            // 3 방향 소환 (선택) — 토스트 수준 힌트, 할 일 없음
            var drag = Step("ftue_drag_summon",
                "칸을 꾹 누른 채 끌면 원하는 방향으로 보낼 수 있어요",
                MetaCondition.Always(), target: CoachTargets.DeckCard0, trigger: FtueTrigger.AfterDelay, triggerArg: "2");
            drag.hintOnly = true;
            s.Add(drag);

            // 4 불 밝히기 — 첫 감탄 순간 (게이지 강제 채움 = FtueDirector → BattleSystem.ForceLampFull)
            s.Add(Step("ftue_first_lamp",
                "불빛 게이지가 차면 버튼이 반짝여요\n수선점에 불을 밝혀요!",
                MetaCondition.Count(CounterKey.LampLit, 1), target: CoachTargets.LampButton,
                trigger: FtueTrigger.AfterDelay, triggerArg: "5", action: FtueAction.ForceLampFull));

            // 5 첫 보스 — 전투를 가리지 않게 토스트만. 끝나면 가이드 퀘스트 줄이 나타난다
            var boss = Step("ftue_first_boss",
                "보스를 물리치면 다음 스테이지로 가요!",
                MetaCondition.StageCleared(1, 1), target: CoachTargets.StagePanel);
            boss.hintOnly = true;
            s.Add(boss);

            // 6 수선점 강화 — 클리어 배너·보상이 지나간 뒤 강화 x1 버튼에 손가락.
            //    끝나면 단추 250 (M06): 7단계 짝짝이 Lv.1→5(50+55+60+65 = 230)를 가진 단추가 0이어도 바로 낼 수 있게 —
            //    FtueDirector가 강화 x1 버튼에서 상단 단추 칩으로 날려 보내 어디서 왔는지 보인다. 단계 수·번호는 그대로다
            var shop = Step(FtueAfterFirstBossStepId,
                "강화 탭에서 단추로 수선점을 강화해요\n강화 x1을 3번 눌러 보세요",
                MetaCondition.Count(CounterKey.ShopLevelUp, 3), target: Targets.GrowthLevelUp1,
                trigger: FtueTrigger.AfterDelay, triggerArg: "1.5");
            shop.rewards.Add(Gold(250));
            s.Add(shop);

            // 7 친구 강화 — 같은 강화 탭 아래쪽 친구 목록 (6단계에서 받은 선물 단추로)
            s.Add(Step("ftue_friend_level",
                "선물 받은 단추로 짝짝이를 레벨 5까지 올려 보세요\n레벨은 짝짝이의 모든 형태가 함께 나눠요",
                MetaCondition.Of(MetaConditionType.LineageLevel, 5, "jjakjjak"), target: Targets.GrowthLineageList));

            // 8 자동 전투 — 1-2 클리어로 열리면 AUTO를 켜 준다 (개방 카드가 함께 뜬다, IDLE-05)
            var auto = Step("ftue_auto_battle",
                "AUTO가 켜졌어요! 이제 친구들이 알아서 싸워요",
                MetaCondition.Always(), target: CoachTargets.AutoButton,
                trigger: FtueTrigger.FeatureUnlocked, triggerArg: nameof(FeatureId.AutoBattle), action: FtueAction.EnableAutoBattle);
            auto.hintOnly = true;
            s.Add(auto);

            // 9 수선 바구니 — 2시간치를 미리 넣어 두고 손가락
            s.Add(Step("ftue_idle_chest",
                "친구들이 모아 둔 선물이 바구니에 담겼어요\n바구니를 눌러 받아 보세요",
                MetaCondition.Count(CounterKey.ChestClaim, 1), target: CoachTargets.IdleChestWidget,
                trigger: FtueTrigger.FeatureUnlocked, triggerArg: nameof(FeatureId.IdleChest),
                action: FtueAction.PrefillIdleChest, actionArg: "2"));

            // 10 첫 만남 — 1-3 클리어로 소환이 열리면 무료 1회, 우두커니(전설) 확정 (공개 확률·천장 계산 제외).
            //    소환 탭 위 첫 만남 카드를 눌러야 끝난다 (그 전에 한 유료 소환은 이 단계를 끝내지 않는다)
            s.Add(Step("ftue_first_meeting",
                "잃어버린 친구 찾기가 열렸어요!\n첫 만남은 무료예요. 누가 찾아왔을까요?",
                MetaCondition.Flag(MetaFlags.FirstMeetingSummoned), target: CoachTargets.TabGacha,
                trigger: FtueTrigger.FeatureUnlocked, triggerArg: nameof(FeatureId.Gacha),
                action: FtueAction.FirstMeetingSummon, actionArg: FirstMeetingLineage));

            // 11 편성 — 이 단계가 시작되면 편성·시너지가 열린다 (FtueStep 11). 우두커니 + 짝짝이 = "기다림의 끝"
            s.Add(Step(FtueFormationStepId,
                "새 친구 우두커니를 지킴이로 세워 봐요\n편성에서 친구를 끌어 빈 방위에 놓아요",
                MetaCondition.Of(MetaConditionType.GuardiansPlaced, 4), target: CoachTargets.TabFormation));

            // 12 시너지 보기 — 편성 탭 팀 시너지 패널 → 상세 팝업 (UI가 MetaFlags.SynergyDetailViewed를 알린다)
            s.Add(Step("ftue_synergy",
                "함께 서면 힘이 나는 친구들이 있어요\n시너지를 눌러 자세히 살펴봐요",
                MetaCondition.Flag(MetaFlags.SynergyDetailViewed), target: Targets.FormationSynergy));

            // 13 원정 — 1-4 클리어로 열림. 이 단계에서 처음 보낸 원정만 1분 (FtueDirector가 끝 시각을 당기고 플래그를 세움).
            //    단계 전에 보낸 원정은 세지 않는다 — 그래야 14단계가 그 원정을 끝까지 기다리지 않는다
            s.Add(Step("ftue_expedition",
                "친구들을 원정에 보내 보세요\n돌아올 때 재료를 한가득 들고 와요",
                MetaCondition.Flag(MetaFlags.ShortFirstExpeditionUsed), target: CoachTargets.TabExpedition,
                trigger: FtueTrigger.FeatureUnlocked, triggerArg: nameof(FeatureId.Expedition),
                action: FtueAction.ShortFirstExpedition, actionArg: "1"));

            // 14 원정 받기 — 돌아올 때까지 화면을 가리지 않고 기다린다 (원정 탭 빨간 점이 안내)
            var claim = Step("ftue_expedition_claim",
                "원정이 끝나면 원정 탭에 점이 떠요. 받아 주세요!",
                MetaCondition.Count(CounterKey.ExpeditionClaim, 1), target: CoachTargets.TabExpedition);
            claim.hintOnly = true;
            s.Add(claim);

            // 15 빠른 수선 — 1-5 클리어로 열림, 오늘 무료
            s.Add(Step("ftue_quick_idle",
                "빠른 수선이 열렸어요! 오늘은 무료예요\n바구니에서 2시간치 선물을 바로 받아요",
                MetaCondition.Count(CounterKey.QuickIdle, 1), target: CoachTargets.IdleChestWidget,
                trigger: FtueTrigger.FeatureUnlocked, triggerArg: nameof(FeatureId.QuickIdle)));

            // 16 졸업 — 졸업 카드 + 별조각 300, 이어서 출석부 첫 도장 (F-05 순서)
            var graduation = Step("ftue_graduation",
                "이제 수선점 주인이에요!\n친구들과 함께 밤을 지켜 주세요",
                MetaCondition.Always(), trigger: FtueTrigger.BetweenStages, action: FtueAction.Graduation);
            graduation.rewards.Add(Gem(300));
            s.Add(graduation);
        }

        static FtueStepDef Step(string id, string text, MetaCondition complete, string target = null,
            FtueTrigger trigger = FtueTrigger.Immediate, string triggerArg = null,
            FtueAction action = FtueAction.None, string actionArg = null) => new()
        {
            id = id,
            trigger = trigger,
            triggerArg = triggerArg,
            coachTarget = target,
            text = text,
            blockInput = false,
            hintOnly = false,
            complete = complete,
            action = action,
            actionArg = actionArg,
            rewards = new List<MetaReward>(),
        };
    }
}
