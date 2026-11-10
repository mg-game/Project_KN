using System.Collections.Generic;
using static IdleGame.EditorTools.SkillKit;
using static IdleGame.StatType;
using V = IdleGame.SkillVfx;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 시너지 콘텐츠 원본: 특성 7 (trait_{fabric}) + 역할 6 (style_{defense}) + 조합 12 (combo_*).
    ///   · 특성: 단계 2/3/4 (비는 1/2). 구성원(그 특성을 가진 형태)에게만 붙는다.
    ///   · 역할: 단계 2/3 또는 2/4. 방어/힐/지원은 수선점·잔향·불빛 같은 팀 전체 능력치라 팀 전체에, 돌격/포획/저격은 구성원에게.
    ///   · 조합: 계보가 모두 편성되면 켜지는 한 단계. 능력치는 팀 전체에 붙고, 보너스 스킬은 조합 계보 유닛이 저마다 쓴다.
    /// 특성 시너지는 어울리는 스테이지 기믹에서 효과가 ×1.5 (비→비 오는 밤, 빛→정전, 밤→보름달, 치장/소리→축제의 밤, 천→먼지 폭풍).
    /// 단계 설명과 보너스 스킬 설명은 수치에서 만든다 (SynergyText / SkillText).
    /// <para>
    /// 보너스 스킬은 구성원 계보의 유닛(지킴이 · 잔향이)마다 따로 시전된다 (SynergySystem.BuildModifiers → BattleSimulator.CollectSkillInfos).
    /// 그래서 ① 수치는 "한 번 시전" 값으로 적고 여럿이 함께 쓴다는 전제로 낮게 잡으며, 설명 끝에 "…마다 따로 써요" 문장을 붙인다 (Finish).
    /// ② 쌓이는 효과(피해 · 회복 · 보호막 · 잔향 · 불빛 · 소환)는 팀 전체 대상에 써도 되지만,
    ///    겹치지 않는 효과(강화 · 정화 · 지속 회복 · 표식 · 기절 · 감속 · 화상 · 드러냄)는 같은 순간에 같은 대상(모든 아군 · 모든 적 · 가장 강한 적)에게
    ///    여러 번 걸리기만 하므로, 자기 범위 대상(요격 범위 · 공격 대상 주변 · 자신)으로 쓰거나 단계 능력치로 옮긴다 (ContentValidator가 경고).
    /// ③ 공격력 강화(BuffAttack)는 더 센 강화가 약한 강화를 덮어쓰므로 형태 스킬(양말 공방가) 하나만 쓴다. 조합 공격력은 단계 능력치로 준다.
    /// </para>
    /// </summary>
    internal static class SynergyContent
    {
        public const float FavoredMultiplier = 1.5f;

        /// <param name="lineages">조합 조건 문장에 쓸 계보 이름 (CharacterContent.Build 결과)</param>
        public static List<SynergyDef> Build(IEnumerable<LineageDef> lineages)
        {
            var names = new Dictionary<string, string>();
            foreach (var lineage in lineages)
            {
                if (lineage != null && !string.IsNullOrEmpty(lineage.id)) names[lineage.id] = lineage.name;
            }

            var list = new List<SynergyDef>();
            list.AddRange(Traits());
            list.AddRange(Styles());
            list.AddRange(Combos());
            foreach (var def in list) Finish(def, names);
            return list;
        }

        public static string TraitId(TraitType trait) => "trait_" + trait.ToString().ToLowerInvariant();
        public static string StyleId(CombatStyle style) => "style_" + style.ToString().ToLowerInvariant();

        // ───────────────────────── 특성 ─────────────────────────

        static IEnumerable<SynergyDef> Traits() => new[]
        {
            Trait(TraitType.Fabric, "포근한 천", StageModifierType.DustStorm,
                "짝짝이·똑단이·우두커니처럼 천과 실로 이루어진 친구들이에요. 폭신하고 질겨서, 함께 모일수록 해진 곳을 서로 기워 주며 오래 버텨요. " +
                "천을 갉아먹는 좀나방은 조심하세요!",
                Members(2, (HpPercent, 15), (ShieldPowerPercent, 10)),
                Members(3, (HpPercent, 25), (ShieldPowerPercent, 20), (AttackPercent, 8)),
                Members(4, (HpPercent, 40), (ShieldPowerPercent, 30), (AttackPercent, 15))
                    .With(Bonus("trait_fabric", 4, "올 풀어 감기", V.SplitYarn, "풀려라, 엉켜라!",
                        Every(14f, 6f), InRange(),
                        "몸에서 풀어낸 올을 휘휘 감아, 다가오는 적의 발목에 실타래를 엉겨 붙여요.",
                        Slow(0.4f, 3f), Damage(1f)))),

            Trait(TraitType.Metal, "단단한 금속", StageModifierType.None,
                "열쇠 찰칵이와 금빛 케이스 루즈리처럼 금속으로 된 친구들이에요. 단단하고 날카로워서, 서로를 비추며 적의 약점을 정확히 찔러요. " +
                "녹얼룩 곁에서는 몸이 뻑뻑해지니, 함께 광택을 내 녹을 막아요.",
                Members(2, (CritDamagePercent, 30), (AttackPercent, 6)),
                Members(3, (CritDamagePercent, 45), (AttackPercent, 10), (BossDamagePercent, 12))
                    .With(Bonus("trait_metal", 3, "녹 방지 광택", V.GlossShine, "반짝반짝, 녹은 저리 가!",
                        Every(12f, 3f), Self,
                        "부드러운 천으로 몸을 반짝반짝 닦아, 녹얼룩이 남긴 녹을 털어 내요.",
                        Cleanse(6f))),
                Members(4, (CritDamagePercent, 70), (AttackPercent, 15), (BossDamagePercent, 25))
                    .With(Bonus("trait_metal", 4, "거울 광택", V.GlossShine, "녹슬 틈은 없어!",
                        Every(10f, 3f), Self,
                        "서로를 거울처럼 비출 만큼 광을 내면, 녹은 아예 들러붙지 못해요.",
                        Cleanse(8f)))),

            Trait(TraitType.Light, "꺼지지 않는 빛", StageModifierType.Blackout,
                "몽당이의 촛불과 라라벨의 반짝이는 음판처럼 빛을 품은 친구들이에요. 빛 특성 친구는 어둠에 숨은 망각의 그림자도 찾아내 공격할 수 있어요. " +
                "비가 오면 불빛이 조금 흐려져요.",
                Members(2, (AttackPercent, 10), (RangePercent, 8)),
                Members(3, (AttackPercent, 16), (RangePercent, 12), (HealPowerPercent, 15))
                    .With(Bonus("trait_light", 3, "불씨 나눔", V.CandleBurst, "불씨 하나 더!",
                        OnKill(4f), Self,
                        "쓰러진 적이 남긴 작은 불씨를 주워 수선점의 등불에 옮겨 담아요.",
                        Lamp(0.02f))),
                Members(4, (AttackPercent, 24), (RangePercent, 18), (HealPowerPercent, 25))
                    .With(Bonus("trait_light", 4, "빛의 고리", V.RevealLight, "어둠아, 물러가!",
                        LampLit, InRange(),
                        "수선점의 불빛이 빛 특성 친구들을 타고 고리처럼 번져, 친구들이 저마다 지키던 거리의 어둠을 걷어 내요.",
                        Damage(2f), Reveal(10f)))),

            Trait(TraitType.Sound, "울려 퍼지는 소리", StageModifierType.Festival,
                "'찰칵' 소리를 내는 찰칵이와 노래하는 오르골 라라벨처럼 소리를 가진 친구들이에요. 박자가 맞으면 모두의 손이 빨라지고, 스킬도 더 자주 울려 퍼져요.",
                Members(2, (AttackSpeedPercent, 12), (SkillCooldownReductionPercent, 8)),
                Members(3, (AttackSpeedPercent, 20), (SkillCooldownReductionPercent, 12), (StunDurationPercent, 15)),
                Members(4, (AttackSpeedPercent, 28), (SkillCooldownReductionPercent, 18), (StunDurationPercent, 25))
                    .With(Bonus("trait_sound", 4, "공명 음파", V.SoundRing, "찰칵, 딩~!",
                        Every(12f, 4f), InRange(),
                        "열쇠 소리와 오르골 소리가 겹쳐 커다란 음파가 되어 퍼져요. 음파에 맞은 적은 귀가 멍해져 잠깐 멈춰 서요.",
                        Stun(0.5f), Damage(1f)))),

            Trait(TraitType.Night, "밤을 지키는 친구들", StageModifierType.FullMoon,
                "긴 밤을 지새워 본 짝짝이와 어둠 속에서도 불을 켜는 몽당이예요. 밤이 익숙한 친구들은 잔향이로 더 오래 머물고, 큰 그림자 앞에서도 물러서지 않아요.",
                Members(2, (EchoLifetimePercent, 15), (AttackPercent, 8)),
                Members(3, (EchoLifetimePercent, 25), (AttackPercent, 12), (BossDamagePercent, 15)),
                Members(4, (EchoLifetimePercent, 35), (AttackPercent, 18), (BossDamagePercent, 25))
                    .With(Bonus("trait_night", 4, "한밤의 기습", V.ShadowBurst, "밤은 우리 편이야!",
                        OnKill(5f), AroundTarget(0.5f),
                        "밤그림자 속에 숨어 있다가, 적이 쓰러지는 틈을 타 곁의 적들을 덮쳐요.",
                        Damage(1.8f), Mark(15f, 4f)))),

            Trait(TraitType.Rain, "비 오는 날의 약속", StageModifierType.Rain,
                "비 오는 날에만 쓸모 있다고 믿는 우두커니예요. 빗소리가 들리면 누구보다 든든해져, 우산 보호막이 단단해지고 적의 발걸음이 무거워져요.",
                Members(1, (ShieldPowerPercent, 25), (SlowPowerPercent, 15)),
                Members(2, (ShieldPowerPercent, 45), (SlowPowerPercent, 25), (HpPercent, 20))
                    .With(Bonus("trait_rain", 2, "폭풍 전야", V.StormSwirl, "바람이 불어온다…",
                        Every(16f, 6f), InRange(),
                        "지킴이와 잔향이, 두 우산이 함께 빙글 돌면 작은 비바람이 일어 적을 밀어내요.",
                        Slow(0.5f, 4f), Knockback(0.25f)))),

            Trait(TraitType.Charm, "반짝이는 치장", StageModifierType.Festival,
                "외투를 꾸미던 똑단이와 화장대의 주인공 루즈리처럼, 누군가를 꾸며 주던 친구들이에요. 함께 모이면 반짝반짝 눈길을 끌어 적의 빈틈을 놓치지 않아요.",
                Members(2, (CritChancePercent, 10), (AttackPercent, 6)),
                Members(3, (CritChancePercent, 15), (AttackPercent, 10), (SkillCooldownReductionPercent, 8)),
                Members(4, (CritChancePercent, 22), (AttackPercent, 15), (SkillCooldownReductionPercent, 12))
                    .With(Bonus("trait_charm", 4, "반짝 눈길", V.DebuffMark, "여기 좀 봐!",
                        Every(12f, 4f), AroundTarget(0.5f),
                        "반짝이는 장식으로 적의 눈길을 빼앗아, 빈틈이 드러난 자리에 표식을 남겨요.",
                        Mark(20f, 5f)))),
        };

        // ───────────────────────── 역할 ─────────────────────────

        static IEnumerable<SynergyDef> Styles() => new[]
        {
            Style(CombatStyle.Defense, "철벽 방어",
                "방어 친구들이 수선점 앞을 겹겹이 막아서요. 수선점이 더 튼튼해지고, 받는 피해가 줄어요.",
                Team(2, (ShopDamageReductionPercent, 10), (ShopMaxHpPercent, 10)),
                Team(4, (ShopDamageReductionPercent, 20), (ShopMaxHpPercent, 20), (SlowPowerPercent, 15))
                    .With(Bonus("style_defense", 4, "겹겹 방벽", V.ButtonShield, "한 발짝도 못 들어와!",
                        BattleStart, Shop,
                        "전투가 시작되기 전, 방어 친구들이 저마다 수선점 문 앞에 방벽을 한 겹씩 덧대요.",
                        Shield(2.5f, 30f)))),

            Style(CombatStyle.Heal, "따뜻한 손길",
                "힐 친구들이 서로 손을 맞잡으면 회복이 더 따뜻해져요. 수선점의 상처도 저절로 조금씩 아물어요.",
                Team(2, (HealPowerPercent, 15), (ShopRegenPercentPerSecond, 0.25f)),
                Team(3, (HealPowerPercent, 30), (ShieldPowerPercent, 15), (ShopRegenPercentPerSecond, 0.5f))
                    .With(Bonus("style_heal", 3, "포근한 숨결", V.HeartHeal, "괜찮아, 우리가 있잖아.",
                        ShopLow, Shop,
                        "수선점이 무너지려는 순간, 힐 친구들이 저마다 달려와 갈라진 벽에 따뜻한 숨결을 불어넣어요.",
                        Heal(4f), Shield(1.5f, 8f)))),

            Style(CombatStyle.Support, "든든한 응원",
                "지원 친구들이 박자를 맞춰 응원하면, 잔향이 더 빨리 차오르고 불빛 게이지도 금세 가득 차요.",
                Team(2, (ResonanceRegenPercent, 15), (LampGainPercent, 10)),
                Team(4, (ResonanceRegenPercent, 30), (LampGainPercent, 20), (StartResonanceFlat, 10), (EchoCostReductionFlat, 1))
                    .With(Bonus("style_support", 4, "응원 박수", V.MusicNotes, "잘한다, 잘한다!",
                        LampLit, Self,
                        "수선점에 불이 밝혀지면 지원 친구들이 저마다 신나게 박수를 쳐요. 박수 소리가 잔향이 되어 수선점에 모여, 잔향이 친구들을 더 많이 부를 수 있어요.",
                        Resonance(2f)))),

            Style(CombatStyle.Assault, "앞장서는 용기",
                "돌격 친구들이 함께 뛰어들면 기세가 올라요. 더 세게, 더 빠르게 적에게 달려들어요.",
                Members(2, (AttackPercent, 15), (EchoMoveSpeedPercent, 10)),
                Members(3, (AttackPercent, 25), (EchoMoveSpeedPercent, 20), (AttackSpeedPercent, 10))
                    .With(Bonus("style_assault", 3, "기세 등등", V.BuffCrown, "다음은 누구야!",
                        OnKill(4f), Self,
                        "적을 쓰러뜨린 기세를 몰아, 숨 돌릴 틈도 없이 다음 적에게 달려들어요.",
                        BuffSpeed(30f, 3f)))),

            Style(CombatStyle.Capture, "꽁꽁 묶기",
                "포획 친구들이 손발을 맞추면, 한번 붙잡은 적을 더 오래, 더 단단히 묶어 둬요.",
                Members(2, (StunDurationPercent, 20), (SlowPowerPercent, 15)),
                Members(3, (StunDurationPercent, 35), (SlowPowerPercent, 25), (SkillCooldownReductionPercent, 10))
                    .With(Bonus("style_capture", 3, "실타래 덫", V.SlowYarn, "걸렸다!",
                        Every(15f, 5f), AroundTarget(0.5f),
                        "미리 깔아 둔 실타래 덫이 적의 발을 휘감아요.",
                        Slow(0.5f, 3f), Stun(0.4f)))),

            Style(CombatStyle.Snipe, "멀리 보는 눈",
                "저격 친구들이 서로 거리를 재 주면 더 멀리 보고, 큰 적의 약점을 더 정확히 노려요.",
                Members(2, (RangePercent, 10), (BossDamagePercent, 15)),
                Members(3, (RangePercent, 18), (BossDamagePercent, 30), (CritChancePercent, 10))
                    .With(Bonus("style_snipe", 3, "약점 조준", V.DebuffMark, "거기, 약점이 보여!",
                        Every(14f, 5f), AroundTarget(0.35f),
                        "저격 친구들이 저마다 겨누던 적의 약점에 조준 표식을 새겨, 친구들의 공격이 그곳에 꽂히게 해요.",
                        Mark(25f, 5f)))),
        };

        // ───────────────────────── 조합 ─────────────────────────

        static IEnumerable<SynergyDef> Combos() => new[]
        {
            Combo("combo_mending_duo", "수선 듀오", new[] { "jjakjjak", "ttokdan" }, StageModifierType.None,
                "양말과 단추는 수선점에서 가장 오래된 단짝이에요. 바늘 한 땀에 단추 하나, 둘이 함께면 무엇이든 다시 꿰매요.",
                ComboTier((HpPercent, 15), (ShieldPowerPercent, 15), (ShopRegenPercentPerSecond, 0.3f))
                    .With(ComboBonus("combo_mending_duo", "한 땀 한 단추", V.PatchHeal, "꿰매고, 채우고!",
                        ShopHit(15f), Shop,
                        "짝짝이가 찢어진 곳을 꿰매면, 똑단이가 그 자리에 단추를 꼭 채워요.",
                        Heal(1f), Shield(1.5f, 6f)))),

            Combo("combo_rainy_lantern", "빗속의 등불", new[] { "udukeoni", "mongdang" }, StageModifierType.Rain,
                "우두커니가 빗줄기를 막아 주면, 몽당이는 꺼질 걱정 없이 불꽃을 키워요. 비 오는 밤의 등불은 더 멀리까지 번져요.",
                ComboTier((AttackPercent, 5), (LampGainPercent, 20), (LampDamagePercent, 25))
                    .With(ComboBonus("combo_rainy_lantern", "꺼지지 않는 불꽃", V.CandleBurst, "우산 아래라면 안 꺼져!",
                        Always, Self,
                        "우산 아래에서는 비바람도 불꽃을 꺼뜨리지 못해요. 몽당이는 녹을수록 불꽃이 커지고, 우두커니의 우산은 오래 펼칠수록 단단해져요. 공격력이 오르면 스킬의 회복과 보호막도 함께 커져요.",
                        Ramp(2f, 40)))),

            Combo("combo_locked_musicbox", "잠긴 오르골", new[] { "chalkak", "lalabell" }, StageModifierType.None,
                "오래전 잠겨 버린 오르골의 열쇠가 바로 찰칵이였을지도 몰라요. 찰칵, 태엽이 풀리면 멈췄던 노래가 다시 흘러요.",
                ComboTier((AttackSpeedPercent, 10), (SkillCooldownReductionPercent, 15), (StunDurationPercent, 15))
                    .With(ComboBonus("combo_locked_musicbox", "다시 흐르는 노래", V.MusicNotes, "찰칵, 딩동댕~",
                        OnKill(6f), Self,
                        "적을 쓰러뜨릴 때마다 오르골 태엽이 한 칸씩 감겨요. 흘러나온 음표는 잔향이 되어 수선점으로 모여요.",
                        Resonance(2f)))),

            Combo("combo_vanity_secret", "화장대의 비밀", new[] { "rouge", "ttokdan" }, StageModifierType.Festival,
                "화장대 서랍 속, 립스틱과 단추는 오래도록 서로의 비밀을 지켜 왔어요. 함께 꾸미고 나서면 누구보다 반짝여요.",
                ComboTier((CritChancePercent, 10), (CritDamagePercent, 25), (GoldGainPercent, 15))
                    .With(ComboBonus("combo_vanity_secret", "비밀의 입맞춤", V.KissBurst, "쉿, 우리만의 비밀!",
                        OnKill(6f), AroundTarget(0.5f),
                        "적을 쓰러뜨린 자리에서 서랍 속에 숨겨 둔 입맞춤 자국이 팡 터져, 곁의 적들에게 붉은 자국을 남겨요.",
                        Damage(1.2f), Mark(20f, 4f)))),

            Combo("combo_night_walk", "별이 머문 밤 산책", new[] { "jjakjjak", "mongdang" }, StageModifierType.FullMoon,
                "짝짝이가 몽당이의 불빛에 기대어 밤길을 걸어요. 별이 머문 밤이면 잔향이들도 산책하듯 더 오래 머물러요.",
                ComboTier((EchoLifetimePercent, 20), (ResonanceRegenPercent, 15), (StartResonanceFlat, 5))
                    .With(ComboBonus("combo_night_walk", "별빛 산책", V.MoonGlow, "오늘 밤 별이 참 예쁘다.",
                        Dissolved, Shop,
                        "산책을 마친 잔향이가 빛으로 풀려 돌아올 때, 주머니 가득 별빛을 담아 와요.",
                        Lamp(0.04f), Resonance(2f)))),

            Combo("combo_parade", "축제 퍼레이드", new[] { "jjakjjak", "lalabell", "rouge" }, StageModifierType.Festival,
                "짝짝이가 지휘봉을 들고, 라라벨이 노래하고, 루즈리가 맨 앞에서 손을 흔들어요. 오늘 밤은 아무도 혼자가 아니에요!",
                ComboTier((AttackPercent, 15), (AttackSpeedPercent, 15), (GoldGainPercent, 25))
                    .With(ComboBonus("combo_parade", "퍼레이드 행렬", V.StarBurst, "자, 다 같이 출발!",
                        BattleStart, Self,
                        "퍼레이드의 첫 북소리가 울리면, 지킴이 친구들이 저마다 작은 악단 친구를 하나씩 데리고 나와 함께 행진해요.",
                        Summon(0.35f, 1, 15f)))),

            Combo("combo_key_and_lock", "열쇠와 자물쇠", new[] { "chalkak", "rouge" }, StageModifierType.None,
                "맞는 자물쇠를 찾던 열쇠와, 누군가 돌려 열어 주길 기다리던 립스틱. 서로가 서로를 여는 딱 맞는 짝이에요.",
                ComboTier((AttackPercent, 8), (CritDamagePercent, 40), (BossDamagePercent, 20))
                    .With(ComboBonus("combo_key_and_lock", "딱 맞는 짝", V.KeyholeLock, "찰칵, 딱 맞았다!",
                        Every(18f, 6f), InRange(),
                        "찰칵이와 루즈리가 저마다 자기 앞으로 다가온 적들을 '찰칵' 잠그고, 잠긴 자리에 붉은 표식을 꾹 찍어요.",
                        Stun(1f), Mark(20f, 5f)))),

            Combo("combo_shop_family", "수선점 식구들", new[] { "jjakjjak", "ttokdan", "chalkak", "mongdang" }, StageModifierType.None,
                "수선점에 가장 먼저 모인 네 친구예요. 문을 열면 언제나 이 넷이 반겨 주고, 넷이 함께라면 수선점의 불은 꺼지지 않아요.",
                ComboTier((AttackPercent, 10), (ShopMaxHpPercent, 15), (ShopDamageReductionPercent, 8), (StartResonanceFlat, 8))
                    .With(ComboBonus("combo_shop_family", "어서 와, 수선점이야", V.HeartHeal, "어서 와, 기다렸어!",
                        BattleStart, Shop,
                        "지킴이로 선 식구들이 저마다 등불 심지를 하나씩 손봐 두어, 식구가 많이 설수록 불빛 게이지가 더 많이 차오른 채로 전투가 시작돼요.",
                        Lamp(0.05f)))),

            Combo("combo_clear_sky", "비 갠 하늘", new[] { "udukeoni", "lalabell" }, StageModifierType.None,
                "기다리던 비가 그치고, 라라벨의 노래에 무지개가 떠요. 우두커니는 처음으로 맑은 하늘이 싫지 않았어요.",
                ComboTier((HealPowerPercent, 25), (ShieldPowerPercent, 25), (ShopRegenPercentPerSecond, 0.3f))
                    .With(ComboBonus("combo_clear_sky", "무지개 자장가", V.CleanseSparkle, "하늘이 개었어!",
                        Every(20f, 8f), EchoesNear(0.9f),
                        "비 갠 하늘 아래 무지개 자장가가 흐르면, 곁에 머문 잔향이들의 상처와 녹이 말끔히 씻겨 나가요.",
                        Heal(1.5f), Cleanse(4f)))),

            Combo("combo_last_song", "마지막 노래", new[] { "lalabell", "mongdang" }, StageModifierType.Blackout,
                "다 타 가는 촛불 앞에서, 라라벨은 마지막 음을 연주할 용기를 얻어요. 끝은 또 다른 시작이니까요.",
                ComboTier((SkillCooldownReductionPercent, 10), (LampGainPercent, 15), (LampDamagePercent, 35))
                    .With(ComboBonus("combo_last_song", "끝나지 않는 여운", V.SoundRing, "이 노래는 끝나지 않아.",
                        Dissolved, Around(0.7f),
                        "잔향이가 빛으로 풀리는 자리마다 마지막 노래의 여운이 남아요. 여운에 닿은 적들은 넋을 잃고 멈춰 서요.",
                        Stun(0.8f), Slow(0.3f, 3f)))),

            Combo("combo_waiting_end", "기다림의 끝", new[] { "udukeoni", "jjakjjak" }, StageModifierType.Rain,
                "주인을 기다린 우산과 짝을 기다린 양말. 서로를 만난 날, 둘의 긴 기다림이 끝났어요.",
                ComboTier((HpPercent, 20), (EchoLifetimePercent, 15), (ShopDamageReductionPercent, 10))
                    .With(ComboBonus("combo_waiting_end", "드디어 만났어", V.UmbrellaShield, "이제 혼자가 아니야.",
                        ShopLow, Shop,
                        "수선점이 무너지려는 순간, 우산과 양말 친구들이 저마다 달려와 함께 문 앞을 덮어요.",
                        Shield(3f, 12f), Heal(1.5f)))),

            Combo("combo_seven_lights", "일곱 개의 불빛",
                new[] { "jjakjjak", "ttokdan", "chalkak", "mongdang", "udukeoni", "rouge", "lalabell" }, StageModifierType.Blackout,
                "잃어버린 일곱 친구가 모두 수선점에 모였어요. 일곱 개의 불빛이 하나로 이어지면, 잊힌 것들도 다시 이름을 찾아요.",
                ComboTier((AttackPercent, 25), (HpPercent, 25), (AttackSpeedPercent, 15),
                        (ShopMaxHpPercent, 20), (LampGainPercent, 25), (GoldGainPercent, 30))
                    .With(ComboBonus("combo_seven_lights", "일곱 빛의 축복", V.StarBurst, "우리 모두 여기 있어!",
                        OnKill(6f), AroundTarget(0.6f),
                        "일곱 계보의 불빛이 하나로 이어져, 친구가 적을 쓰러뜨린 자리마다 일곱 빛깔 별이 터져 곁의 적들을 휩쓸어요.",
                        Damage(1.8f)))),
        };

        // ───────────────────────── 만들기 도우미 ─────────────────────────

        /// <param name="lore">시너지 이야기 (Finish가 조합 조건 / 기믹 배율 문장을 뒤에 붙인다)</param>
        static SynergyDef Trait(TraitType trait, string name, StageModifierType favored, string lore, params SynergyTier[] tiers) =>
            new()
            {
                id = TraitId(trait),
                name = name,
                description = lore,
                kind = SynergyKind.Trait,
                trait = trait,
                favoredModifier = favored,
                tiers = new List<SynergyTier>(tiers),
            };

        static SynergyDef Style(CombatStyle style, string name, string lore, params SynergyTier[] tiers) =>
            new()
            {
                id = StyleId(style),
                name = name,
                description = lore,
                kind = SynergyKind.Style,
                style = style,
                favoredModifier = StageModifierType.None,
                tiers = new List<SynergyTier>(tiers),
            };

        /// <summary>조합: 한 단계, 단계 수 = 계보 수</summary>
        static SynergyDef Combo(string id, string name, string[] lineageIds, StageModifierType favored, string lore, SynergyTier tier)
        {
            tier.count = lineageIds.Length;
            return new SynergyDef
            {
                id = id,
                name = name,
                description = lore,
                kind = SynergyKind.Combo,
                lineageIds = new List<string>(lineageIds),
                favoredModifier = favored,
                tiers = new List<SynergyTier> { tier },
            };
        }

        /// <summary>
        /// 기믹 배율, 단계 설명(수치에서 생성), 보너스 스킬의 "누가 쓰는지" 문장, 설명 뒤 조합 조건 / 기믹 배율 문장을 채운다
        /// </summary>
        static void Finish(SynergyDef def, Dictionary<string, string> lineageNames)
        {
            def.favoredMultiplier = FavoredMultiplier;
            var names = new List<string>();
            if (def.kind == SynergyKind.Combo)
            {
                foreach (var id in def.lineageIds) names.Add(lineageNames.TryGetValue(id, out var lineageName) ? lineageName : id);
            }

            string memberLabel = def.kind switch
            {
                SynergyKind.Trait => $"{SynergyText.TraitName(def.trait)} 특성 친구",
                SynergyKind.Style => $"{SynergyText.StyleName(def.style)} 친구",
                _ => "조합 친구",
            };
            // 보너스 스킬은 특성/조합 시너지면 구성원 계보의 유닛마다, 역할 시너지면 그 성향 형태의 유닛마다만 시전된다
            // (SynergySystem.AddMemberSkill → FormBonusSkills — 같은 계보의 다른 성향 형태는 쓰지 않는다)
            string casterGroup = def.kind switch
            {
                SynergyKind.Trait => $"{SynergyText.TraitName(def.trait)} 특성",
                SynergyKind.Style => SynergyText.StyleName(def.style),
                _ => SynergyText.ComboGroup(names),
            };
            foreach (var tier in def.tiers)
            {
                tier.description = SynergyText.DescribeTier(tier, memberLabel);
                var skill = tier.bonusSkill;
                if (skill != null && !skill.IsEmpty)
                    skill.description += " " + SynergyText.CasterLine(casterGroup, skill.trigger);
            }

            var lines = new List<string> { def.description };
            if (def.kind == SynergyKind.Combo) lines.Add(SynergyText.ComboCondition(names));
            string favored = SynergyText.FavoredLine(def.favoredModifier, def.favoredMultiplier);
            if (favored.Length > 0) lines.Add(favored);
            def.description = string.Join("\n", lines);
        }

        /// <summary>구성원(특성/성향을 가진 유닛)에게만 붙는 단계. 유닛 능력치만 넣는다 (SynergyText.IsTeamStat 참고)</summary>
        static SynergyTier Members(int count, params (StatType stat, float value)[] modifiers) => Tier(count, true, modifiers);

        /// <summary>팀 전체에 붙는 단계 (수선점/잔향/불빛 능력치 포함 가능)</summary>
        static SynergyTier Team(int count, params (StatType stat, float value)[] modifiers) => Tier(count, false, modifiers);

        /// <summary>조합 단계 (팀 전체). count는 Combo()가 계보 수로 채운다</summary>
        static SynergyTier ComboTier(params (StatType stat, float value)[] modifiers) => Tier(0, false, modifiers);

        static SynergyTier Tier(int count, bool membersOnly, (StatType stat, float value)[] modifiers)
        {
            var tier = new SynergyTier { count = count, membersOnly = membersOnly, modifiers = new List<StatModifier>() };
            foreach (var (stat, value) in modifiers) tier.modifiers.Add(new StatModifier { stat = stat, value = value });
            return tier;
        }

        static SynergyTier With(this SynergyTier tier, SkillDef bonusSkill)
        {
            tier.bonusSkill = bonusSkill;
            return tier;
        }

        /// <summary>특성/역할 단계 보너스 스킬 (ID = "bonus_{시너지 ID}_{단계 수}")</summary>
        static SkillDef Bonus(string synergyId, int count, string name, string vfx, string castText, When when, Aim aim, string story,
            params SkillEffectDef[] effects) =>
            Make($"bonus_{synergyId}_{count}", name, vfx, castText, when, aim, story, effects);

        /// <summary>조합 보너스 스킬 (ID = "bonus_{조합 ID}")</summary>
        static SkillDef ComboBonus(string comboId, string name, string vfx, string castText, When when, Aim aim, string story,
            params SkillEffectDef[] effects) =>
            Make($"bonus_{comboId}", name, vfx, castText, when, aim, story, effects);
    }
}
