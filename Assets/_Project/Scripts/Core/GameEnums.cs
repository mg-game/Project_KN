namespace IdleGame
{
    public enum CurrencyType
    {
        /// <summary>표시명 "단추" (일반 재화)</summary>
        Gold = 0,
        /// <summary>표시명 "별조각" (유료 재화)</summary>
        Gem = 1,
        /// <summary>표시명 "별빛 사탕" ('별이 머문 밤 산책' 이벤트 재화)</summary>
        EventToken = 2,
    }

    /// <summary>
    /// 재화 지급 출처 (I-14 · SYS_Economy §2.1). 별조각만 쓴다: Purchase면 유료 풀(PlayerData.gemPaid), 그 밖은 무료 풀(gemFree).
    /// 결제 코드는 아직 없다 — Purchase는 자리만 (SYS_Shop에서 연결).
    /// </summary>
    public enum CurrencySource
    {
        /// <summary>게임 안 보상 · 시작 지급 · 우편 등 결제가 아닌 모든 지급</summary>
        Game = 0,
        /// <summary>결제 (아직 쓰는 곳 없음)</summary>
        Purchase = 1,
    }

    /// <summary>등급. 캐릭터는 별 개수로 표시한다 (Common=1성 ~ Mythic=5성).</summary>
    public enum ItemGrade
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
        Mythic = 4,
    }

    public enum RewardType
    {
        Currency = 0,
        Item = 1,
    }

    public enum ItemCategory
    {
        Material = 0,
        Equipment = 1,
    }

    /// <summary>재료 획득처 종류 ("재료 수집하러 가기" 이동에 사용)</summary>
    public enum ItemSourceType
    {
        Stage = 0,
        Expedition = 1,
        Dungeon = 2,
        Shop = 3,
        Event = 4,
    }

    /// <summary>전투 배치 역할. 같은 형태를 두 역할에 동시에 둘 수 없다.</summary>
    public enum CharacterRole
    {
        /// <summary>지킴이: 크고 불투명. 수선점 둘레 방위 링에 고정 배치, 해당 방향 부채꼴 범위를 요격</summary>
        Guardian = 0,
        /// <summary>잔향이: 작고 반투명, 발광. 잔향을 소모해 전투 중 소환, 시간이 지나면 빛으로 풀려 사라짐</summary>
        Echo = 1,
    }

    /// <summary>진화형이 나누는 전투 성향</summary>
    public enum CombatStyle
    {
        Defense = 0,  // 방어: 체력 높음, 범위 안 적 감속
        Assault = 1,  // 돌격: 근접 고공격
        Capture = 2,  // 포획: 적을 묶어 둠(기절/감속)
        Snipe = 3,    // 저격: 사거리 김, 단일 고피해
        Support = 4,  // 지원(서포터): 아군 강화 / 적 약화 / 잔향·불빛 보조
        Heal = 5,     // 힐: 수선점·잔향이 회복, 보호막
    }

    /// <summary>아트 가이드 체형 분류 (비중 35/45/20)</summary>
    public enum BodyType
    {
        Mascot = 0,     // 완전 비인간 마스코트형
        SemiHuman = 1,  // 준인간 이형형
        Humanoid = 2,   // 미형 인간형
    }

    /// <summary>진화 기억도 노드 상태</summary>
    public enum EvolutionState
    {
        Undiscovered = 0, // 미발견
        ClueFound = 1,    // 단서 발견 (레시피 확인 가능)
        Unlocked = 2,     // 해금
    }

    /// <summary>진화 조건 / 계보별 누적 기록 종류</summary>
    public enum ProgressType
    {
        ExpeditionCount = 0,  // 원정 N회 (이 계보가 참여해 완료한 원정)
        EchoDeployCount = 1,  // 잔향이 출격 N회 (이 계보 형태를 잔향이로 소환한 횟수)
        LineageLevel = 2,     // 캐릭터 레벨 N 달성 (계보 공유 레벨, 누적값 아님)
        StageReached = 3,     // 스테이지 N 도달 (전역 최고 스테이지, 누적값 아님)
        DungeonFloor = 4,     // 잔향이 던전 N층 돌파 (전역, 누적값 아님)
        GuardianKillCount = 5,// 지킴이로 처치한 적 수
    }

    public enum CostumeUnlockType
    {
        Default = 0,        // 처음부터 보유
        NightVisit = 1,     // 수선집 야간 방문 (현지 20:00~05:59 접속)
        EventShop = 2,      // '별이 머문 밤 산책' 이벤트 상점 구매
        StageReached = 3,   // 스테이지 N 도달
        ExpeditionCount = 4,// 계보 원정 N회
        Resonance = 5,      // 계보 공명 Lv.N
        FormUnlocked = 6,   // 특정 진화형 해금 (target = 0, targetId = formId)
        Season = 7,         // 별빛 산책 수첩 30칸 보상 (Q-06, 시즌 한정 의상 jjak_starwalk — MetaReward.Costume으로 받는다)
    }

    public enum BattleMode
    {
        Stage = 0,
        EchoDungeon = 1,
    }

    // ───────────── 스킬 / 시너지 / 기믹 ─────────────

    /// <summary>계보 특성(재질/성질). 같은 특성을 가진 친구를 함께 편성하면 특성 시너지가 생긴다.</summary>
    public enum TraitType
    {
        Fabric = 0, // 천: 짝짝이, 똑단이, 우두커니
        Metal = 1,  // 금속: 찰칵이, 루즈리
        Light = 2,  // 빛: 몽당이, 라라벨
        Sound = 3,  // 소리: 찰칵이, 라라벨
        Night = 4,  // 밤: 짝짝이, 몽당이
        Rain = 5,   // 비: 우두커니
        Charm = 6,  // 치장: 똑단이, 루즈리
    }

    /// <summary>스킬 발동 시점</summary>
    public enum SkillTrigger
    {
        /// <summary>항상 적용 (오라/특성). cooldown 무시</summary>
        Passive = 0,
        /// <summary>쿨타임마다 자동 시전 (대상이 있을 때)</summary>
        Cooldown = 1,
        /// <summary>잔향이로 소환될 때 1회</summary>
        OnSummon = 2,
        /// <summary>잔향이가 빛으로 풀릴 때 1회</summary>
        OnDissolve = 3,
        /// <summary>이 유닛이 적을 처치할 때 (cooldown이 있으면 그 간격 이하로는 다시 안 터짐)</summary>
        OnKill = 4,
        /// <summary>수선점 불 밝히기가 발동될 때</summary>
        OnLampLit = 5,
        /// <summary>수선점이 피해를 입을 때 (cooldown 간격)</summary>
        OnShopDamaged = 6,
        /// <summary>전투 중 1회: 수선점 내구도가 30% 아래로 떨어질 때 (예: 라라벨의 마지막 음)</summary>
        OnShopLowHp = 7,
        /// <summary>전투(스테이지) 시작 시 1회</summary>
        OnBattleStart = 8,
    }

    /// <summary>스킬 대상 범위</summary>
    public enum SkillTargetType
    {
        Self = 0,
        Shop = 1,
        /// <summary>지킴이: 자기 부채꼴 안의 적 / 잔향이: radius 안의 적</summary>
        EnemiesInRange = 2,
        /// <summary>시전자 주변 radius 안의 적</summary>
        EnemiesNearSelf = 3,
        /// <summary>현재 공격 대상 주변 radius 안의 적</summary>
        EnemiesNearTarget = 4,
        /// <summary>체력이 가장 많은 적 1 (보스 우선)</summary>
        StrongestEnemy = 5,
        /// <summary>체력 비율이 가장 낮은 적 1</summary>
        WeakestEnemy = 6,
        AllEnemies = 7,
        /// <summary>시전자 주변 radius 안의 아군 잔향이</summary>
        AlliesNearSelf = 8,
        AllEchoes = 9,
        /// <summary>양옆 방위의 지킴이 (지킴이 시전자 기준)</summary>
        AdjacentGuardians = 10,
        AllGuardians = 11,
        /// <summary>지킴이 + 잔향이 전부</summary>
        AllAllies = 12,
        /// <summary>시전자 방향(레인) 위의 모든 적</summary>
        Lane = 13,
    }

    /// <summary>
    /// 스킬 효과 종류. value/duration/count 의미는 SkillEffectDef 주석 참고.
    /// </summary>
    public enum SkillEffectType
    {
        Damage = 0,
        Heal = 1,
        Shield = 2,
        Slow = 3,
        Stun = 4,
        Knockback = 5,
        Pull = 6,
        Mark = 7,
        BuffAttack = 8,
        BuffAttackSpeed = 9,
        Summon = 10,
        ResonanceGain = 11,
        LampGain = 12,
        Reveal = 13,
        DamageOverTime = 14,
        Execute = 15,
        Chain = 16,
        AttackRamp = 17,
        Cleanse = 18,
        HealOverTime = 19,
    }

    /// <summary>시너지/스킬이 올려 주는 능력치 종류 (값은 대부분 %)</summary>
    public enum StatType
    {
        AttackPercent = 0,
        AttackSpeedPercent = 1,
        HpPercent = 2,
        RangePercent = 3,
        SkillCooldownReductionPercent = 4,
        HealPowerPercent = 5,
        ShieldPowerPercent = 6,
        ShopMaxHpPercent = 7,
        ShopDamageReductionPercent = 8,
        /// <summary>초당 수선점 회복 (수선점 최대 내구도의 %)</summary>
        ShopRegenPercentPerSecond = 9,
        ResonanceRegenPercent = 10,
        /// <summary>전투 시작 잔향 (고정값)</summary>
        StartResonanceFlat = 11,
        /// <summary>잔향이 소환 비용 감소 (고정값)</summary>
        EchoCostReductionFlat = 12,
        EchoLifetimePercent = 13,
        EchoMoveSpeedPercent = 14,
        LampGainPercent = 15,
        LampDamagePercent = 16,
        StunDurationPercent = 17,
        SlowPowerPercent = 18,
        BossDamagePercent = 19,
        CritChancePercent = 20,
        CritDamagePercent = 21,
        GoldGainPercent = 22,
    }

    public enum SynergyKind
    {
        /// <summary>특성 (TraitType) 보유 유닛 수</summary>
        Trait = 0,
        /// <summary>전투 성향 (CombatStyle) 유닛 수 — 방어/힐/지원 등 역할 시너지</summary>
        Style = 1,
        /// <summary>특정 계보 조합이 모두 편성됨</summary>
        Combo = 2,
    }

    /// <summary>스테이지 기믹(날씨/사건). 스테이지마다 정해지며 특성/적에게 영향을 준다.</summary>
    public enum StageModifierType
    {
        None = 0,
        /// <summary>비 오는 밤: 적 이동 느려짐, 비 특성 강화, 빛 특성 약화</summary>
        Rain = 1,
        /// <summary>정전: 망각의 그림자가 보이지 않음(빛 근처에서만 드러남), 빛 특성 크게 강화</summary>
        Blackout = 2,
        /// <summary>보름달: 불빛 게이지 충전 증가, 잔향이 유지 시간 증가, 밤 특성 강화</summary>
        FullMoon = 3,
        /// <summary>바람: 적과 잔향이 이동 빨라짐</summary>
        Wind = 4,
        /// <summary>먼지 폭풍: 먼지뭉치 대량 등장, 사거리 감소, 천 특성 강화</summary>
        DustStorm = 5,
        /// <summary>축제의 밤: 단추 보상 증가, 치장/소리 특성 강화</summary>
        Festival = 6,
    }

    /// <summary>적 기믹 (여러 개 조합 가능)</summary>
    [System.Flags]
    public enum EnemyGimmick
    {
        None = 0,
        /// <summary>보호막: shieldAmount만큼 피해를 먼저 흡수</summary>
        Shielded = 1 << 0,
        /// <summary>처치 시 splitInto 적 splitCount마리로 나뉨</summary>
        Splitter = 1 << 1,
        /// <summary>보이지 않음: 빛 특성 유닛/드러냄 효과/수선점 근처에서만 공격 가능</summary>
        Phasing = 1 << 2,
        /// <summary>천 특성 유닛에게 추가 피해 (좀나방)</summary>
        FabricEater = 1 << 3,
        /// <summary>주변 금속 특성 유닛 공격 속도 감소 (녹)</summary>
        Rusting = 1 << 4,
        /// <summary>주변 적 회복 (auraRadius / auraValue)</summary>
        Mender = 1 << 5,
        /// <summary>주변 적 이동 속도 증가</summary>
        Hastener = 1 << 6,
        /// <summary>체력 50% 이하에서 분노 (속도·피해 증가)</summary>
        Enrage = 1 << 7,
        /// <summary>수선점에 닿거나 처치되지 않고 버티면 불빛 게이지를 빨아들임</summary>
        LampDrain = 1 << 8,
    }

    // ───────────── 메타 (일일 시계 · 진행 기록 · 기능 개방 · 미션 · 출석 · 바구니) ─────────────

    /// <summary>
    /// 진행 기록기(ProgressTracker) 카운터. 키마다 평생 / 오늘 / 이번 주 3벌을 센다 (오늘·이번 주는 05:00 초기화).
    /// 세이브에 멤버 "이름"으로 저장되므로 이름을 바꾸지 말고, 새 키는 끝에 값을 붙여 추가한다 (모르는 키는 로드 때 건너뜀).
    /// "합계" 키(AnyUpgrade 등)는 ProgressTracker가 원래 키와 함께 더한다.
    /// </summary>
    public enum CounterKey
    {
        /// <summary>접속한 날 (05:00 기준 하루 1회)</summary>
        Login = 0,
        /// <summary>메인 스테이지 클리어</summary>
        StageClear = 1,
        /// <summary>메인 스테이지 실패</summary>
        StageFail = 2,
        /// <summary>메인 스테이지 보스 처치 (= 스테이지 클리어 순간)</summary>
        BossKill = 3,
        /// <summary>메인 스테이지 적 처치 (분열 개체 포함)</summary>
        EnemyKill = 4,
        /// <summary>잔향이 소환 (자동 소환 포함, 토큰 제외)</summary>
        EchoSummon = 5,
        /// <summary>수선점 불 밝히기 (자동 포함)</summary>
        LampLit = 6,
        /// <summary>수선점 강화 (오른 레벨 수만큼)</summary>
        ShopLevelUp = 7,
        /// <summary>친구(계보) 레벨업 (오른 레벨 수만큼)</summary>
        LineageLevelUp = 8,
        /// <summary>잃어버린 친구 찾기 (1회 = 1, 10연속 = 10, 첫 만남 확정 소환 포함)</summary>
        GachaPull = 9,
        /// <summary>원정 보내기</summary>
        ExpeditionStart = 10,
        /// <summary>원정 보상 받기 (완료된 원정 1개당 1)</summary>
        ExpeditionClaim = 11,
        /// <summary>잔향이 던전 도전 (승패 무관)</summary>
        DungeonChallenge = 12,
        /// <summary>잔향이 던전 새 층 돌파</summary>
        DungeonClear = 13,
        /// <summary>잔향이 던전 소탕</summary>
        DungeonSweep = 14,
        /// <summary>진화 제작 (형태 해금)</summary>
        EvolutionCraft = 15,
        /// <summary>계보 공명 레벨 상승</summary>
        ResonanceUp = 16,
        /// <summary>의상 해금</summary>
        CostumeUnlock = 17,
        /// <summary>의상 갈아입기</summary>
        CostumeEquip = 18,
        /// <summary>상점 구매 (일반 + 이벤트)</summary>
        ShopBuy = 19,
        /// <summary>수선 바구니 받기 (복귀 화면 받기 포함)</summary>
        ChestClaim = 20,
        /// <summary>빠른 수선</summary>
        QuickIdle = 21,
        /// <summary>우편 받기 (우편 1통당 1)</summary>
        MailClaim = 22,
        /// <summary>전투 시작 때 최대 단계로 켜진 시너지 (시너지 1개당 1)</summary>
        SynergyTierMax = 23,
        /// <summary>가이드 퀘스트 보상 받기</summary>
        GuideQuestClaim = 24,
        /// <summary>일일·주간 미션 보상 받기</summary>
        MissionClaim = 25,
        /// <summary>수선 일지 그날 점수 100 달성 (하루 1회, 주간 미션 "일지 100점 5일")</summary>
        JournalComplete = 26,
        /// <summary>출석 도장 (하루 1회)</summary>
        AttendanceCheckIn = 27,
        /// <summary>합계: 수선점 강화 + 친구 레벨업 (일일 미션 "친구·수선점 강화 5번")</summary>
        AnyUpgrade = 28,
        /// <summary>합계: 진화 제작 + 공명 + 의상 해금 (주간 미션 "진화·공명·의상 중 1번")</summary>
        AnyGrowthMilestone = 29,
        /// <summary>합계: 던전 도전 + 소탕 (일일 미션 "잔향이 던전 1번")</summary>
        DungeonRun = 30,

        // ── 4차 (SPEC4 §2.2) ──
        /// <summary>일일 미션을 받아 얻은 일지 점수 (받은 점수만큼, 별빛 산책 수첩 경험치 Q-06) — MissionSystem이 일일 미션을 받을 때 더한다</summary>
        JournalPoints = 31,
        /// <summary>수선점 기록 받기 (단계 1개당 1, Q-04)</summary>
        AchievementClaim = 32,
        /// <summary>도감 보상 받기 (Q-05)</summary>
        CodexClaim = 33,
        /// <summary>쓰다듬기 (호감도가 오른 것만, CR-10)</summary>
        AffinityPet = 34,
        /// <summary>이벤트 미션 받기 (EVT-01)</summary>
        EventMissionClaim = 35,
        /// <summary>수첩 칸 받기 (Q-06)</summary>
        SeasonTierClaim = 36,
        /// <summary>잃어버린 상자 열기 (PRG-06)</summary>
        LostChestOpen = 37,

        // 38~56: 5차(SPEC5 §2.2)가 값을 정해 두었다 — 다른 키에 쓰지 않는다

        /// <summary>
        /// 앱을 켜 둔 시간 (분, 잠자기 포함 · 백그라운드 제외). MetaSession.Tick이 앞에 있는 시간을 모아 1분마다 1번 더한다 —
        /// 기능 개방 속도 조절 (FeatureUnlockDef.alsoRequire, SPEC5 §2.8). 4차 마이그레이션이 튜토리얼을 마친 옛 세이브에 min(270, 30 × 설치 일수)를 넣는다
        /// </summary>
        PlayMinutes = 57,
    }

    /// <summary>카운터를 읽는 범위</summary>
    public enum CounterScope
    {
        Lifetime = 0,
        /// <summary>오늘 (현지 05:00 ~ 다음 날 05:00)</summary>
        Daily = 1,
        /// <summary>이번 주 (월요일 05:00 ~ 다음 월요일 05:00)</summary>
        Weekly = 2,
    }

    /// <summary>
    /// 단계적으로 열리는 기능 (FeatureGateSystem). 개방 조건은 MetaDatabase.features (콘텐츠: Editor/Content/MetaContent).
    /// 정의가 없는 기능은 처음부터 열린 것으로 본다. 세이브에 숫자로 저장되므로 값을 바꾸지 말 것.
    /// </summary>
    public enum FeatureId
    {
        /// <summary>전투 (항상)</summary>
        Battle = 0,
        /// <summary>강화 탭 — 수선점 강화 (항상)</summary>
        ShopUpgrade = 1,
        /// <summary>친구들 탭 (항상)</summary>
        Characters = 2,
        /// <summary>편성 탭 (FTUE 3단계)</summary>
        Formation = 3,
        /// <summary>친구 레벨업 (1-1 클리어)</summary>
        CharacterLevelUp = 4,
        /// <summary>자동 전투 AUTO (1-2 클리어)</summary>
        AutoBattle = 5,
        /// <summary>수선 바구니 (1-2 클리어)</summary>
        IdleChest = 6,
        /// <summary>소환 탭 — 잃어버린 친구 찾기 (1-3 클리어)</summary>
        Gacha = 7,
        /// <summary>원정 탭 (1-4 클리어)</summary>
        Expedition = 8,
        /// <summary>빠른 수선 (1-5 클리어)</summary>
        QuickIdle = 9,
        /// <summary>출석부 (FTUE 졸업 = 1-5 클리어)</summary>
        Attendance = 10,
        /// <summary>2배속 (1-6 클리어)</summary>
        Speed2x = 11,
        /// <summary>수선 일지 — 일일·주간 미션 (1-7 클리어)</summary>
        Missions = 12,
        /// <summary>상점 탭 (1-8 클리어)</summary>
        Shop = 13,
        /// <summary>잔향이 던전 (1-10 클리어)</summary>
        Dungeon = 14,
        /// <summary>진화 제작 (1-10 클리어)</summary>
        Evolution = 15,
        /// <summary>도감 보상 (1-10 클리어)</summary>
        Codex = 16,
        /// <summary>시너지 보기 (편성과 함께)</summary>
        Synergy = 17,
        /// <summary>수선점 기록 — 업적 (2-3 클리어)</summary>
        Achievements = 18,
        /// <summary>의상 (2-5 클리어)</summary>
        Costumes = 19,
        /// <summary>잔향이 덱 6번째 칸 (기존 규칙: 2-10 도달)</summary>
        EchoSlot6 = 20,
        /// <summary>이벤트 (3-1 클리어 또는 설치 3일째)</summary>
        Events = 21,
        /// <summary>자동 불 밝히기 방식 고르기 (1-6 클리어)</summary>
        LampPolicy = 22,
        /// <summary>자동 보스 도전 (2-1 클리어)</summary>
        AutoChallenge = 23,
        /// <summary>원정 모두 받기 / 모두 다시 보내기 (2-1 클리어)</summary>
        ExpeditionBatch = 24,
        /// <summary>던전 입장권 한 번에 쓰기 (던전 5층 돌파, IDLE-05)</summary>
        DungeonBatchSweep = 25,
        /// <summary>전투 기록 (1-7 클리어, CR-03 — 전투 HUD 버튼이라 "새로 열림" 점 없음)</summary>
        BattleStats = 26,
    }

    /// <summary>수선 일지 미션 주기</summary>
    public enum MissionPeriod
    {
        /// <summary>매일 05:00 초기화</summary>
        Daily = 0,
        /// <summary>매주 월요일 05:00 초기화</summary>
        Weekly = 1,
    }

    /// <summary>출석부 종류</summary>
    public enum AttendanceTrack
    {
        /// <summary>새싹 출석 7일 (한 번만)</summary>
        Starter = 0,
        /// <summary>정규 출석 28일 (반복, 누적 일수)</summary>
        Monthly = 1,
    }

    /// <summary>
    /// 메타 조건 종류 (가이드 퀘스트 / 기능 개방 / 튜토리얼 완료 조건). MetaCondition.target / targetId / counter 의미:
    /// </summary>
    public enum MetaConditionType
    {
        /// <summary>항상 충족</summary>
        None = 0,
        /// <summary>counter 평생 값 ≥ target</summary>
        Counter = 1,
        /// <summary>counter 오늘 값 ≥ target</summary>
        DailyCounter = 2,
        /// <summary>counter 이번 주 값 ≥ target</summary>
        WeeklyCounter = 3,
        /// <summary>메인 스테이지 target 클리어 (target = (챕터-1)×10 + 번호, 1-4 = 4). highestStage &gt; target이면 충족</summary>
        StageCleared = 4,
        /// <summary>수선점 레벨 ≥ target</summary>
        ShopLevel = 5,
        /// <summary>targetId 계보 레벨 ≥ target (targetId가 비면 가장 높은 계보)</summary>
        LineageLevel = 6,
        /// <summary>보유 계보 레벨 합 ≥ target</summary>
        LineageLevelSum = 7,
        /// <summary>보유 계보 수 ≥ target</summary>
        OwnedLineages = 8,
        /// <summary>해금 형태 수 ≥ target</summary>
        UnlockedForms = 9,
        /// <summary>편성된 지킴이 수 ≥ target</summary>
        GuardiansPlaced = 10,
        /// <summary>편성된 잔향이(덱) 수 ≥ target</summary>
        EchoesPlaced = 11,
        /// <summary>켜진 시너지 수 ≥ target</summary>
        ActiveSynergies = 12,
        /// <summary>켜진 시너지 단계(1부터) ≥ target. targetId가 있으면 그 시너지, 비면 가장 높은 것</summary>
        SynergyTier = 13,
        /// <summary>잔향이 던전 최고 층 ≥ target</summary>
        DungeonFloor = 14,
        /// <summary>수선 일지 점수 ≥ target (targetId "weekly"면 이번 주, 아니면 오늘)</summary>
        JournalPoints = 15,
        /// <summary>PlayerData.flags에 targetId가 있음 (MetaFlags 상수, 예: 진화 기억도 열어 봄)</summary>
        Flag = 16,
        /// <summary>튜토리얼 target단계까지 끝냄 (meta.ftue.step ≥ target, 끝났으면 항상 충족)</summary>
        FtueStep = 17,
        /// <summary>튜토리얼 졸업 (또는 건너뜀)</summary>
        FtueDone = 18,
        /// <summary>설치 후 target일째 이상 (설치한 날 = 1일째, 05:00 기준)</summary>
        InstallDays = 19,
        /// <summary>targetId(FeatureId 이름) 기능이 열림</summary>
        FeatureUnlocked = 20,
        /// <summary>전투력 ≥ target</summary>
        Power = 21,
        /// <summary>호감도 targetId단계("10") 이상인 계보 수 ≥ target (ProgressTracker.ExtraConditionValue로 호감도 모듈이 답한다)</summary>
        AffinityLevelCount = 22,
        /// <summary>한 번이라도 켜 본 시너지 종류 수 ≥ target (CodexSave.found의 "synergy:*")</summary>
        SynergyKindsSeen = 23,
        /// <summary>보유 의상 수 ≥ target</summary>
        CostumesOwned = 24,
    }

    /// <summary>메타 보상 종류 (MetaReward). 재화/재료는 RewardService로, 나머지는 MetaRewardService가 처리한다.</summary>
    public enum MetaRewardType
    {
        /// <summary>재화 (currency, amount)</summary>
        Currency = 0,
        /// <summary>재료 (itemId, amount)</summary>
        Item = 1,
        /// <summary>단추 N분치 (amount = 분). 받는 순간의 초당 단추 × 60 × N으로 바뀐다</summary>
        GoldMinutes = 2,
        /// <summary>빠른 수선권 (amount장, 보관 상한 IdleChestConfig.maxQuickIdleTickets)</summary>
        QuickIdleTicket = 3,
        /// <summary>두 배 수선권 (amount장)</summary>
        DoubleTicket = 4,
        /// <summary>선택 기억편 (amount개, 나중에 원하는 계보에 넣는다)</summary>
        SelectShards = 5,
        /// <summary>친구 확정 (itemId = 계보 ID, 이미 있으면 등급별 기억편으로 변환)</summary>
        Lineage = 6,
        /// <summary>의상 (itemId = 의상 ID)</summary>
        Costume = 7,
        /// <summary>친구 선택권 (itemId = 고를 수 있는 최고 등급 이름 "Legendary", amount = 장 수 → meta.wallet.friendSelectTickets, PRG-04)</summary>
        FriendSelectTicket = 8,
    }

    /// <summary>튜토리얼 단계가 시작되는 때</summary>
    public enum FtueTrigger
    {
        /// <summary>앞 단계가 끝나면 바로</summary>
        Immediate = 0,
        /// <summary>스테이지 사이(결과 배너 중)에 시작</summary>
        BetweenStages = 1,
        /// <summary>triggerArg(FeatureId 이름) 기능이 열렸을 때</summary>
        FeatureUnlocked = 2,
        /// <summary>앞 단계가 끝나고 triggerArg초 뒤</summary>
        AfterDelay = 3,
    }

    /// <summary>
    /// 튜토리얼 단계가 시작될 때 하는 일. 전투에 손대는 동작(ForceLampFull, BattleProtection)은
    /// 전투 시스템 훅이 생길 때까지 UI 쪽에서 가능한 만큼만 흉내 낸다 (SPEC3 참고).
    /// </summary>
    public enum FtueAction
    {
        None = 0,
        /// <summary>동화 인트로 3장</summary>
        StoryIntro = 1,
        /// <summary>불빛 게이지를 가득 채움 (첫 불 밝히기)</summary>
        ForceLampFull = 2,
        /// <summary>자동 전투 켜기</summary>
        EnableAutoBattle = 3,
        /// <summary>바구니에 actionArg시간치 선물을 미리 넣어 둠</summary>
        PrefillIdleChest = 4,
        /// <summary>첫 만남 확정 소환 (actionArg = 계보 ID, 무료, 천장·확률 계산 제외)</summary>
        FirstMeetingSummon = 5,
        /// <summary>첫 원정만 actionArg분으로 짧게</summary>
        ShortFirstExpedition = 6,
        /// <summary>졸업 카드 + 보상</summary>
        Graduation = 7,
        /// <summary>1-1~1-3 패배 불가 보호 (수선점 내구도 최소 1, 적 체력 60%)</summary>
        BattleProtection = 8,
    }

    /// <summary>메타 팝업 큐(PopupQueue)에 들어가는 팝업 종류. 값이 작을수록 먼저 띄운다 (F-05 순서).</summary>
    public enum MetaPopupKind
    {
        /// <summary>복귀 화면 "다녀오셨어요!" (수선 바구니 + 두 배 받기)</summary>
        Return = 0,
        /// <summary>다시 만난 날 (72시간 이상 비움, CR-06 예약)</summary>
        Comeback = 1,
        /// <summary>출석부 (오늘 도장을 안 찍었으면)</summary>
        Attendance = 2,
        /// <summary>시작 공지 (FTUE 뒤에만)</summary>
        Notice = 3,
        /// <summary>이벤트 시작 카드 (이벤트당 1번, EVT-01 예약)</summary>
        EventStart = 4,
        /// <summary>새 기능이 열렸어요! (스테이지 사이에만)</summary>
        FeatureUnlock = 5,
        /// <summary>튜토리얼 졸업 카드</summary>
        FtueGraduation = 6,
        /// <summary>챕터 상자 (PRG-06 · PRG-02, 챕터 보스 첫 클리어 — 스테이지 사이에만, 동화책보다 먼저)</summary>
        ChapterChest = 7,
        /// <summary>동화책 한 쪽 (CR-05, 챕터 상자 다음 — 스테이지 사이에만)</summary>
        StoryPage = 8,
        /// <summary>알림 권한 미리 묻기 (NOTI-01 — 스테이지 사이에만)</summary>
        NotificationAsk = 9,
    }

    /// <summary>전투력 대비 권장 전투력 평가 (PRG-01)</summary>
    public enum PowerRating
    {
        /// <summary>권장의 100% 이상 — 초록 "여유"</summary>
        Comfortable = 0,
        /// <summary>80~100% — 노랑 "도전"</summary>
        Challenge = 1,
        /// <summary>80% 미만 — 빨강 "어려움"</summary>
        Hard = 2,
    }

    // ───────────── 기기 설정 (GameSettings) ─────────────

    /// <summary>피해 숫자 표시 (FX-03, SET-01)</summary>
    public enum DamageNumberMode
    {
        All = 0,
        CritOnly = 1,
        Off = 2,
    }

    /// <summary>이펙트 품질 (ATK-06, CR-08). 동시에 띄우는 파티클 수 상한이 달라진다</summary>
    public enum FxQuality
    {
        /// <summary>파티클 120</summary>
        Low = 0,
        /// <summary>파티클 200</summary>
        Medium = 1,
        /// <summary>파티클 340</summary>
        High = 2,
    }

    /// <summary>로컬 알림 항목 (NOTI-01 예약)</summary>
    public enum NotificationKind
    {
        /// <summary>수선 바구니가 가득 참</summary>
        IdleChestFull = 0,
        /// <summary>원정 완료</summary>
        ExpeditionDone = 1,
        /// <summary>던전 입장권 초기화</summary>
        DungeonTickets = 2,
        /// <summary>출석 도장 (20:00까지 안 찍었으면)</summary>
        Attendance = 3,
    }

    // ───────────── 4차 (SPEC4 §2.2) — 오디오 버스(AudioBus)는 Core/Audio/AudioTypes.cs에 있다 (AUDIO-SPEC §0.3) ─────────────

    /// <summary>자동 불 밝히기 방식 (CR-02, BattleSave.lampPolicy에 숫자로 저장). 잠겨 있으면(LampPolicy 1-6 전) 늘 Auto</summary>
    public enum LampPolicy
    {
        /// <summary>알아서: 적이 많거나(5 이상) 보스가 있거나 수선점이 35% 아래일 때 (지금 규칙)</summary>
        Auto = 0,
        /// <summary>보스까지 아끼기: 보스가 있거나 수선점이 25% 아래일 때만</summary>
        SaveForBoss = 1,
        /// <summary>직접: AUTO가 켜져 있어도 저절로 쓰지 않는다 (소환은 그대로 자동)</summary>
        Manual = 2,
    }

    /// <summary>수선점 기록(업적) 종류 (Q-04, 줄 아이콘 · 정렬)</summary>
    public enum AchievementCategory
    {
        Stage = 0,
        Kill = 1,
        Lamp = 2,
        Echo = 3,
        Friend = 4,
        Form = 5,
        Expedition = 6,
        Dungeon = 7,
        Gacha = 8,
        Synergy = 9,
        ShopLevel = 10,
        LevelSum = 11,
        Journal = 12,
        Attendance = 13,
        Costume = 14,
        Affinity = 15,
        /// <summary>숨은 기록 (조건 = 플래그, 찾기 전에는 '???')</summary>
        Hidden = 16,
    }

    /// <summary>도감 탭 (Q-05). 항목 키는 CodexSystem.Key(종류, ID)</summary>
    public enum CodexCategory
    {
        /// <summary>친구 형태 (form:{formId})</summary>
        Friend = 0,
        /// <summary>의상 (costume:{costumeId})</summary>
        Costume = 1,
        /// <summary>적 (enemy:{enemyId})</summary>
        Enemy = 2,
        /// <summary>시너지 (synergy:{id}, 최고 단계 보상은 synergymax:{id})</summary>
        Synergy = 3,
        /// <summary>재료 (material:{itemId})</summary>
        Material = 4,
        /// <summary>동화책 (story:{chapter})</summary>
        Story = 5,
    }

    /// <summary>이벤트 효과 종류 (EVT-01)</summary>
    public enum EventKind
    {
        /// <summary>원정 재료 ×multiplier (주말 원정 축제)</summary>
        DropBoost = 0,
        /// <summary>던전 입장권 +bonus (수요 던전 데이)</summary>
        TicketBonus = 1,
        /// <summary>미션 · 도장 · 별빛 사탕 ×multiplier (별이 머문 밤 산책)</summary>
        Festival = 2,
        /// <summary>시간대 바구니 보너스 (밤의 수선점)</summary>
        NightBonus = 3,
    }

    /// <summary>이벤트 일정 종류 (EVT-01, DailyClock 날 키 기준 — DailyHours만 현지 시각)</summary>
    public enum EventScheduleType
    {
        /// <summary>weekdayMask 요일 (bit0 = 월요일, 날 키의 요일)</summary>
        Weekdays = 0,
        /// <summary>매달 fromDay ~ toDay일 (날 키 기준, 양끝 포함)</summary>
        MonthDays = 1,
        /// <summary>매일 fromHour ~ toHour시 (현지 시각, 자정을 넘을 수 있음)</summary>
        DailyHours = 2,
        /// <summary>fromMonth/fromDay ~ toMonth/toDay (날 키 기준, 해를 넘을 수 있음 — 계절 이벤트용 예약, SPEC4 §13.5(i))</summary>
        Dates = 3,
    }

    /// <summary>알림 권한 (NOTI-01, GameSettings.NotifyPermission)</summary>
    public enum NotificationPermission
    {
        /// <summary>아직 묻지 않음</summary>
        Unknown = 0,
        Granted = 1,
        Denied = 2,
        /// <summary>게임 안 미리 묻기에서 '괜찮아요' (시스템 창은 띄우지 않음)</summary>
        SoftDeclined = 3,
    }

    /// <summary>강해지기 줄 종류 (PRG-03, StrengthenAdvisor)</summary>
    public enum StrengthenKind
    {
        ShopUpgrade = 0,
        LineageLevel = 1,
        FillSlot = 2,
        SynergyOneShort = 3,
        EvolutionReady = 4,
        ResonanceReady = 5,
        MissingMaterial = 6,
        AutoFormation = 7,
        WeakDirection = 8,
    }
}
