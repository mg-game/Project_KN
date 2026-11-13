using System.Collections.Generic;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// F-03 기능 개방표 (+ Critic A2). 첫 세션에 정보가 쏟아지지 않게 스테이지·튜토리얼 진행에 맞춰 연다.
    /// 목록 순서 = 열리는 순서 (FeatureGateSystem.TryGetNextLocked "다음 개방" 안내가 이 순서를 쓴다).
    ///
    /// 개방 카드는 한 번에 두 장을 넘지 않게 골랐다 (1-2: 자동 전투 + 수선 바구니 / 1-3: 소환 + 편성 / 1-10: 던전 + 진화).
    /// 4차 기능(SPEC4 §2.8)의 카드는 그 화면이 들어온 기능만 켠다 (<see cref="CardsReady"/>) — 화면이 없으면 [가 보기]를 눌러도
    /// 갈 곳이 없으므로 조용히 열리고 점만 단다. 카드 사이 간격(앞에서 보는 시간 180초)은 FeatureGateSystem.CardReady가 지킨다 (Q2).
    /// 앱을 켜 둔 시간 바닥(alsoRequire PlayMinutes)은 4차 기능에는 두지 않는다 — SPEC5 §2.8의 바닥은 R2 기능 행의 것이다
    /// (<see cref="PlayFloors"/>, R2P-5가 RM-5에서 그 행과 함께 쓴다).
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>
        /// 4차 기능 중 화면이 들어와 개방 카드(와 카드 선물)를 켠 것 (SPEC4 §2.8 — 예전 ReservedFeatureCards 한 스위치를 기능마다로 나눴다).
        /// RM-2 마지막 착지(S-5)에서 화면 담당이 닫힌 기능을 넣었다: 2배속(전투 HUD 버튼) · LampPolicy(CR-02 길게 누르기 고르기 창, P4-3b) ·
        /// AutoChallenge(S5-4 문구가 원정 다시 보내기도 말한다 — ResendAll 단추, P4-4) · Achievements(일지 [수선점 기록] 탭, P4-6) ·
        /// Events(이벤트 모아 보기, P4-6). 카드가 켜지면 그 카드 선물(빠른 수선권 1 · 별빛 사탕 50)도 함께 켜진다.
        /// DungeonBatchSweep은 아직 뺀다: 소탕 장수 고르기(P4-4)는 있지만 카드의 [가 보기]가 갈 길(MetaNavigator.OnFeature · FeatureSlot)이
        /// 없어 눌러도 아무 데도 가지 않는다 — 그 길이 들어오면 여기에 넣는다 (조용히 열리고 원정 탭 점만 단다).
        /// 도감 · 전투 기록 · 원정 한 번에 보내기는 카드가 늘 없다 (표 그대로).
        /// </summary>
        static readonly HashSet<FeatureId> CardsReady = new()
        {
            FeatureId.Speed2x, FeatureId.LampPolicy, FeatureId.AutoChallenge, FeatureId.Achievements, FeatureId.Events,
        };

        /// <summary>그 4차 기능의 개방 카드를 켤지</summary>
        static bool CardOn(FeatureId feature) => CardsReady.Contains(feature);

        static partial void FillFeatures(MetaDatabase db, GameContent content)
        {
            var list = db.features;

            // ── 처음부터 (조건 없음 = 열림, 카드·점 없음) ──
            list.Add(Always(FeatureId.Battle, "전투", Pick(ArtLib.Icon("guardian"))));
            list.Add(Always(FeatureId.ShopUpgrade, "수선점 강화", Pick(ArtLib.UI("tab_growth"))));
            list.Add(Always(FeatureId.Characters, "친구들", Pick(ArtLib.UI("tab_characters"))));

            // ── 1챕터: 첫 10분 (FTUE-01과 같은 박자) ──
            // 개방 카드 그림: 띠 = illustration (FeatureBanner = Meta/feature_{이름}_banner, 없으면 예전 그림 또는 띠 없음),
            // 메달 = icon (FeatureMedal = Meta/feature_{이름} → 기존 아이콘)
            list.Add(AtStage(FeatureId.CharacterLevelUp, "친구 강화", 1, 1,
                "친구 레벨을 올리면 모든 형태가 함께 강해져요",
                Pick(FeatureMedal(FeatureId.CharacterLevelUp), ArtLib.Icon("star")),
                illustration: FeatureBanner(FeatureId.CharacterLevelUp)));

            var auto = AtStage(FeatureId.AutoBattle, "자동 전투", 1, 2,
                "이제 친구들이 알아서 잔향이를 부르고 불을 밝혀요",
                Pick(FeatureMedal(FeatureId.AutoBattle), ArtLib.Icon("auto")),
                illustration: FeatureBanner(FeatureId.AutoBattle));
            // IDLE-05: 직접 해 본 뒤에 주는 자동화는 그 자체가 보상이다 — 작은 축하 선물을 곁들인다
            auto.rewards.Add(Gem(30));
            list.Add(auto);

            list.Add(AtStage(FeatureId.IdleChest, "수선 바구니", 1, 2,
                "자리를 비워도 바구니에 선물이 쌓여요",
                Pick(FeatureMedal(FeatureId.IdleChest), ArtLib.UI("idle_basket_2"), ArtLib.Icon("reward_generic")),
                illustration: FeatureBanner(FeatureId.IdleChest)));

            list.Add(AtStage(FeatureId.Gacha, "잃어버린 친구 찾기", 1, 3,
                "잃어버린 친구를 찾아 수선점으로 데려와요",
                Pick(FeatureMedal(FeatureId.Gacha), ArtLib.UI("tab_gacha")),
                illustration: Pick(ArtLib.UI("gacha_banner_wide"), ArtLib.UI("gacha_banner"))));

            // 편성·시너지: 튜토리얼 11단계(편성)가 시작될 때 연다. 건너뛰기·옛 세이브·튜토리얼이 멈춘 경우를 위해 1-3 클리어도 둔다
            var formation = AtStage(FeatureId.Formation, "편성", 1, 3,
                "친구를 끌어 다른 방위에 세워 보세요",
                Pick(FeatureMedal(FeatureId.Formation), ArtLib.UI("tab_formation")),
                illustration: FeatureBanner(FeatureId.Formation));
            formation.conditions.Insert(0, MetaCondition.Of(MetaConditionType.FtueStep, FtueStepFormation));
            list.Add(formation);

            var synergy = AtStage(FeatureId.Synergy, "시너지", 1, 3,
                "함께 서면 더 반짝이는 친구들이 있어요",
                Pick(FeatureMedal(FeatureId.Synergy), ArtLib.Icon("resonance")));
            synergy.conditions.Insert(0, MetaCondition.Of(MetaConditionType.FtueStep, FtueStepFormation));
            synergy.showCard = false; // 편성 카드와 같은 순간이라 점만 (편성 탭 안 시너지 패널)
            list.Add(synergy);

            list.Add(AtStage(FeatureId.Expedition, "원정", 1, 4,
                "친구들을 바깥으로 보내 재료를 모아 와요",
                Pick(FeatureMedal(FeatureId.Expedition), ArtLib.UI("tab_expedition")),
                illustration: ArtLib.UI("zone_exp_1_1")));

            list.Add(AtStage(FeatureId.QuickIdle, "빠른 수선", 1, 5,
                "바구니를 바로 채울 수 있어요 (하루 1번 무료)",
                Pick(FeatureMedal(FeatureId.QuickIdle), ArtLib.Icon("ticket_quick"), ArtLib.UI("idle_basket_3")),
                illustration: FeatureBanner(FeatureId.QuickIdle)));

            // A2: 출석부는 튜토리얼 졸업(1-5 클리어 뒤) 직후. 카드는 없다 — 졸업 카드와 출석부 팝업이 이어 뜬다
            var attendance = AtStage(FeatureId.Attendance, "수선점 출석부", 1, 5,
                "매일 들르면 출석부에 단추 도장이 하나씩 쌓여요",
                Pick(FeatureMedal(FeatureId.Attendance), ArtLib.Icon("attendance"), ArtLib.Icon("star")));
            attendance.conditions.Insert(0, MetaCondition.Of(MetaConditionType.FtueDone, 1));
            attendance.showCard = false;
            list.Add(attendance);

            // 1-6: 2배속 + 불 밝히기 방식 = 카드 두 장까지 (띠 그림 ART-UPGRADE #1, 없으면 메달만)
            var speed = AtStage(FeatureId.Speed2x, "2배속", 1, 6,
                "전투를 두 배 빠르게 볼 수 있어요",
                Pick(FeatureMedal(FeatureId.Speed2x), ArtLib.Icon("speed")),
                illustration: FeatureBanner(FeatureId.Speed2x));
            speed.showCard = CardOn(FeatureId.Speed2x);
            list.Add(speed);

            var lampPolicy = AtStage(FeatureId.LampPolicy, "불 밝히기 방식", 1, 6,
                "불 밝히기 버튼을 길게 누르면 쓰는 방식을 고를 수 있어요",
                Pick(FeatureMedal(FeatureId.LampPolicy), ArtLib.Icon("lamp")),
                illustration: FeatureBanner(FeatureId.LampPolicy));
            lampPolicy.showCard = CardOn(FeatureId.LampPolicy); // CR-02 고르기 창 LampPolicyPicker (P4-3b) — S-5에 켰다
            list.Add(lampPolicy);

            list.Add(AtStage(FeatureId.Missions, "수선 일지", 1, 7,
                "매일 수선 일지를 채우면 선물 상자를 열 수 있어요",
                Pick(FeatureMedal(FeatureId.Missions), ArtLib.Icon("journal"), ArtLib.Icon("star")),
                illustration: FeatureBanner(FeatureId.Missions)));

            // CR-03 전투 기록: 전투 HUD 버튼이라 카드 · 점 없이 한 번 반짝임(MetaPulse)만 (SPEC4 §2.8, MetaSession.IsHudToggle).
            // 메달은 빠른 아이콘 '전투 기록' 칸과 같은 Icons/stats (P4-10 L4 — 아직 없으면 Icons/info)
            var battleStats = AtStage(FeatureId.BattleStats, "전투 기록", 1, 7, null,
                Pick(FeatureMedal(FeatureId.BattleStats), ArtLib.Icon("stats"), ArtLib.Icon("info"), ArtLib.Icon("star")));
            battleStats.showCard = false;
            list.Add(battleStats);

            list.Add(AtStage(FeatureId.Shop, "수선점 상점", 1, 8,
                "상점 문이 열렸어요. 매일 새 재료가 들어와요",
                Pick(FeatureMedal(FeatureId.Shop), ArtLib.UI("tab_shop")),
                illustration: FeatureBanner(FeatureId.Shop)));

            // ── 첫 챕터 보스 (1-10): 챕터 보스 보상과 함께 ──
            list.Add(AtStage(FeatureId.Dungeon, "잔향이 던전", 1, 10,
                "잔향이만 들어가는 던전에서 귀한 재료를 찾아요",
                Pick(FeatureMedal(FeatureId.Dungeon), ArtLib.Icon("echo")),
                illustration: ArtLib.UI("dungeon_banner")));

            // 띠 그림이 있으면 메달은 기억편 아이콘 (띠가 없을 때만 예전처럼 양말 카라반 초상화를 메달에)
            list.Add(AtStage(FeatureId.Evolution, "진화 제작", 1, 10,
                "단서와 재료를 모으면 친구의 새 모습을 지을 수 있어요",
                Pick(FeatureMedal(FeatureId.Evolution), ArtLib.Icon("shards")),
                illustration: Pick(FeatureBanner(FeatureId.Evolution), ArtLib.Character("jjak_caravan"))));

            // Q-05: 던전 · 진화 카드가 이미 두 장이라 카드 없음 — 친구들 탭 CodexButton의 NEW 표시로 알린다 (SPEC4 §2.8)
            var codex = AtStage(FeatureId.Codex, "도감 보상", 1, 10,
                "찾은 친구와 형태를 도감에 모으면 선물이 있어요",
                Pick(FeatureMedal(FeatureId.Codex), ArtLib.Icon("star")));
            codex.showCard = false;
            list.Add(codex);

            // ── 2챕터 ──
            // S5-4 (델타 16): 자동 보스 도전 카드가 원정 다시 보내기까지 함께 알린다. 빠른 수선권 1장도 이 카드로 옮겼다 (카드가 켜질 때만)
            var autoChallenge = AtStage(FeatureId.AutoChallenge, "자동 보스 도전", 2, 1,
                "이제 보스 도전과 원정 다시 보내기를 친구들이 알아서 해요",
                Pick(FeatureMedal(FeatureId.AutoChallenge), ArtLib.Icon("auto")),
                illustration: FeatureBanner(FeatureId.AutoChallenge));
            autoChallenge.showCard = CardOn(FeatureId.AutoChallenge);
            if (autoChallenge.showCard) autoChallenge.rewards.Add(MetaReward.QuickIdleTicket(1));
            list.Add(autoChallenge);

            // 카드 없음 (S5-4 — 위 카드가 알린다). ResendAll 버튼의 NEW 표시는 그대로 (P4-4). 띠 그림 B8b는 만들지 않는다
            var batch = AtStage(FeatureId.ExpeditionBatch, "원정 한 번에 보내기", 2, 1,
                "이제 원정을 한 번에 다시 보낼 수 있어요",
                Pick(FeatureMedal(FeatureId.ExpeditionBatch), ArtLib.UI("tab_expedition")));
            batch.showCard = false;
            list.Add(batch);

            var achievements = AtStage(FeatureId.Achievements, "수선점 기록", 2, 3,
                "수선점에 쌓인 이야기를 기록하고 선물을 받아요",
                Pick(FeatureMedal(FeatureId.Achievements), ArtLib.Icon("journal"), ArtLib.Icon("star")),
                illustration: FeatureBanner(FeatureId.Achievements));
            achievements.showCard = CardOn(FeatureId.Achievements); // 일지 [수선점 기록] 탭 (P4-6) — S-5에 켰다
            list.Add(achievements);

            // 띠 그림이 있으면 메달은 응원대장 의상 (띠가 없을 때만 예전처럼 밤지기 의상을 메달에)
            list.Add(AtStage(FeatureId.Costumes, "의상", 2, 5,
                "짝짝이에게 이야기가 담긴 새 옷을 입혀 줄 수 있어요",
                Pick(FeatureMedal(FeatureId.Costumes), ArtLib.Costume("jjak_cheer"), ArtLib.Icon("star")),
                illustration: Pick(FeatureBanner(FeatureId.Costumes), ArtLib.Costume("jjak_nightkeeper"), ArtLib.Costume("jjak_cheer"))));

            // 덱 6번째 칸: FormationSystem 기존 규칙(2-10 도달 = 2-9 클리어)과 같은 조건.
            // 문구는 다른 기능과 같은 "클리어" 기준 (M39 — MetaText.UnlockSentence(FormationSystem.EchoSlotUnlockStage)와 같은 문장).
            // 잠금 글자도 클리어 스테이지 "2-9" — 덮개는 "{잠금 글자} 클리어 시 열려요"로 쓴다
            var slot6 = AtStage(FeatureId.EchoSlot6, "잔향이 여섯째 칸", 2, 9,
                "잔향이 덱에 여섯째 칸이 생겼어요",
                Pick(FeatureMedal(FeatureId.EchoSlot6), ArtLib.Icon("echo")));
            slot6.lockedHint = StageLabel(2, 9);
            slot6.lockedToast = StageClearToast(2, 9);
            slot6.showCard = false;
            list.Add(slot6);

            // ── 3챕터 또는 설치 3일째 (날짜 잠금은 이벤트만, §17) ──
            var events = AtStage(FeatureId.Events, "이벤트", 3, 1,
                "별이 머문 밤 산책 같은 작은 축제가 열려요",
                Pick(FeatureMedal(FeatureId.Events), ArtLib.Icon("currency_event")),
                illustration: ArtLib.UI("shop_event_banner"));
            events.conditions.Add(MetaCondition.Of(MetaConditionType.InstallDays, 3));
            events.lockedToast = $"스테이지 {StageLabel(3, 1)}{ObjectParticle(1)} 클리어하거나 수선점에 3일째 들르면 열려요";
            events.showCard = CardOn(FeatureId.Events); // 이벤트 모아 보기 EventHubPopup (P4-6) — S-5에 켰다
            if (events.showCard) events.rewards.Add(MetaReward.Currency(CurrencyType.EventToken, 50));
            list.Add(events);

            // IDLE-05 던전 한 번에 소탕: 스테이지가 아니라 던전 5층 돌파 (하루 입장권 몇 장씩이라 보통 2~3일째 — 3-1보다 늦다).
            // 개방 순서 검사는 스테이지 조건만 보므로 어디 두어도 되지만, "다음 개방" 안내(TryGetNextLocked = 목록에서 처음 잠긴 것)가
            // 던전을 안 하는 동안 3-1 이벤트 대신 이것을 가리키지 않게 맨 뒤에 둔다
            var batchSweep = new FeatureUnlockDef
            {
                feature = FeatureId.DungeonBatchSweep,
                name = "던전 입장권 한 번에 쓰기",
                conditions = new List<MetaCondition> { MetaCondition.Of(MetaConditionType.DungeonFloor, DungeonBatchSweepFloor) },
                lockedHint = $"던전 {DungeonBatchSweepFloor}층",
                lockedToast = $"잔향이 던전 {DungeonBatchSweepFloor}층을 돌파하면 열려요",
                cardText = "이제 던전 입장권을 한 번에 모두 쓸 수 있어요",
                icon = Pick(FeatureMedal(FeatureId.DungeonBatchSweep), ArtLib.Icon("echo")),
                illustration = FeatureBanner(FeatureId.DungeonBatchSweep),
            };
            batchSweep.showCard = CardOn(FeatureId.DungeonBatchSweep); // 소탕 장수 고르기(P4-4)는 있지만 [가 보기] 길이 아직 없어 꺼 둠 (CardsReady)
            list.Add(batchSweep);
        }

        /// <summary>던전 한 번에 소탕이 열리는 층 (SPEC4 §5.2)</summary>
        internal const int DungeonBatchSweepFloor = 5;

        // ───────────── 앱을 켜 둔 시간 바닥 (SPEC5 §2.8 · App. D #3, Q2) ─────────────

        /// <summary>
        /// R2 기능의 앱을 켜 둔 시간 바닥 (분, alsoRequire PlayMinutes). 이 기능들의 FeatureId는 R2P-0(RM-4)이 더하므로
        /// 지금은 값만 둔다 — R2P-5가 그 행을 만들 때 <see cref="AddPlayFloor"/>로 붙인다. 4차 기능에는 바닥이 없다 (카드 간격만).
        /// </summary>
        internal static class PlayFloors
        {
            public const int Outings = 20;
            public const int Errands = 20;
            public const int Techniques = 30;
            public const int WeeklyGuest = 45;
            public const int Tower = 90;
            public const int NightWalk = 150;
            public const int Keepsakes = 200;
            public const int Workbench = 270;
        }

        /// <summary>
        /// 기능 행에 앱을 켜 둔 시간 바닥을 붙인다 (conditions 중 하나 + 이것도 — FeatureGateSystem.AllMet).
        /// 스테이지만 넘고 시간이 남았을 때 잠김 안내는 FeatureGateSystem.PlayFloorMessage
        /// </summary>
        static FeatureUnlockDef AddPlayFloor(FeatureUnlockDef def, int minutes)
        {
            if (def == null || minutes <= 0) return def;
            def.alsoRequire ??= new List<MetaCondition>();
            def.alsoRequire.Add(MetaCondition.Count(CounterKey.PlayMinutes, minutes));
            return def;
        }

        /// <summary>처음부터 열린 기능 (조건 없음)</summary>
        static FeatureUnlockDef Always(FeatureId feature, string name, Sprite icon) => new()
        {
            feature = feature,
            name = name,
            showCard = false,
            icon = icon,
        };

        /// <summary>스테이지 "챕터-번호" 클리어로 열리는 기능. 잠김 글자·토스트는 스테이지 표기로 만든다</summary>
        static FeatureUnlockDef AtStage(FeatureId feature, string name, int chapter, int number, string cardText, Sprite icon,
            Sprite illustration = null) => new()
        {
            feature = feature,
            name = name,
            conditions = new List<MetaCondition> { MetaCondition.StageCleared(chapter, number) },
            lockedHint = StageLabel(chapter, number),
            lockedToast = StageClearToast(chapter, number),
            showCard = true,
            cardText = cardText,
            icon = icon,
            illustration = illustration,
        };

        // ───────────── 기능 그림 (Art/Meta) ─────────────

        /// <summary>
        /// 기능 그림 이름표 (FeatureId → snake_case): 메달 Meta/feature_{이름}.png (256), 개방 카드 띠 Meta/feature_{이름}_banner.png (1280×480).
        /// 카드 메달(def.icon)과 MetaSceneBuilder.FeatureArt(MetaIcons.Feature — 미션 줄 · 가이드 퀘스트 바 · 카드의 대신 그림)가
        /// 이 표 하나를 써서 어디서나 같은 메달이 보인다 (예전 빌더는 enum 이름을 소문자로만 바꿔 feature_characterlevelup을 찾았다).
        /// 표에 없는 새 기능은 enum 이름의 snake_case (DungeonBatchSweep → dungeon_batch_sweep).
        /// </summary>
        static string FeatureArtName(FeatureId feature) => feature switch
        {
            FeatureId.Battle => "battle",
            FeatureId.ShopUpgrade => "shop_upgrade",
            FeatureId.Characters => "characters",
            FeatureId.Formation => "formation",
            FeatureId.CharacterLevelUp => "level_up",
            FeatureId.AutoBattle => "auto_battle",
            FeatureId.IdleChest => "idle_chest",
            FeatureId.Gacha => "gacha",
            FeatureId.Expedition => "expedition",
            FeatureId.QuickIdle => "quick_idle",
            FeatureId.Attendance => "attendance",
            FeatureId.Speed2x => "speed",
            FeatureId.Missions => "missions",
            FeatureId.Shop => "shop",
            FeatureId.Dungeon => "dungeon",
            FeatureId.Evolution => "evolution",
            FeatureId.Codex => "codex",
            FeatureId.Synergy => "synergy",
            FeatureId.Achievements => "achievements",
            FeatureId.Costumes => "costumes",
            FeatureId.EchoSlot6 => "echo_slot",
            FeatureId.Events => "events",
            FeatureId.LampPolicy => "lamp_policy",
            FeatureId.AutoChallenge => "auto_challenge",
            FeatureId.ExpeditionBatch => "expedition_batch",
            _ => SnakeCase(feature.ToString()),
        };

        /// <summary>기능 메달 경로 (Art/ 아래): Meta/feature_{이름}.png. MetaSceneBuilder.FeatureArt도 이 경로를 읽는다</summary>
        internal static string FeatureMedalPath(FeatureId feature) => $"Meta/feature_{FeatureArtName(feature)}.png";

        /// <summary>기능 메달 그림 (아직 없으면 null → Pick의 다음 후보)</summary>
        static Sprite FeatureMedal(FeatureId feature) => ArtLib.Load(FeatureMedalPath(feature));

        /// <summary>
        /// 기능 개방 카드 띠 그림 Meta/feature_{이름}_banner.png (아직 없으면 null → Pick의 다음 후보).
        /// 가로세로비가 1.3을 넘어 FeatureUnlockCard가 640×240 띠에 그리고, 메달 자리에는 def.icon이 남는다
        /// </summary>
        static Sprite FeatureBanner(FeatureId feature) => MetaArt($"feature_{FeatureArtName(feature)}_banner");

        /// <summary>PascalCase → snake_case (AutoBattle → auto_battle, Speed2x → speed2x)</summary>
        static string SnakeCase(string name)
        {
            var builder = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                if (char.IsUpper(ch) && i > 0) builder.Append('_');
                builder.Append(char.ToLowerInvariant(ch));
            }
            return builder.ToString();
        }
    }
}
