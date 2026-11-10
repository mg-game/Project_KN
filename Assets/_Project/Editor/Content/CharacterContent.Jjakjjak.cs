using static IdleGame.CharacterRole;
using static IdleGame.CombatStyle;
using M = IdleGame.EditorTools.MaterialIds;
using P = IdleGame.ProgressType;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 짝짝이 계보 [천 · 밤]: 진화 기억도 10종(1차 5 + 2차 5) + 외형 10종.
    /// "한 짝만 남은 외로움"에서 출발해, 밤을 지키고 마음을 전하고 친구들을 이끄는 존재로 확장된다.
    /// 의상 문구는 사용자 컨셉 시트(방치/*.png)의 이야기를 따른다.
    /// </summary>
    internal static partial class CharacterContent
    {
        static LineageDef Jjakjjak() => Lineage(
            id: "jjakjjak", name: "짝짝이", species: "양말족", origin: "짝을 잃은 양말",
            grade: ItemGrade.Epic, body: BodyType.SemiHuman,
            traits: new[] { TraitType.Fabric, TraitType.Night },
            palette: "F3E6D0 8A63C9 E79BB8 8FB8DE",
            personality:
                "짝을 잃은 양말이라, 다른 양말만 보면 무조건 가족이라고 불러요. 빨래 바구니 속 낯선 양말에게도, " +
                "길가에 떨어진 아기 양말에게도 먼저 손을 흔들며 \"우리 식구!\" 하고 반기지요.\n" +
                "한 짝만 남은 외로움을 누구보다 잘 알기에, 혼자 남은 친구를 절대 그냥 지나치지 못해요. " +
                "보라 단추 눈과 바늘땀 눈으로 웃는 얼굴은, 그래서 늘 조금 짝짝이랍니다.",
            forms: new[]
            {
                Base("jjak_base", "짝짝이", Defense, Guardian,
                    "니트 양말의 발목이 목이 되고, 둥근 뒤꿈치가 등이 되고, 보라색 커프는 모자처럼 머리 위에 얹혔어요. " +
                    "짧은 팔다리는 발가락 솔기를 꿰매어 만든 거예요.\n" +
                    "폭신한 몸으로 수선점 앞을 막아서면, 몸에 일어난 보풀이 다가오는 적의 발을 붙잡아 느리게 만들어요."),

                // ── 1차 진화 ──
                Evolve("jjak_caravan", "양말 카라반", 1, "jjak_base", Defense, Guardian,
                    "길을 잃은 짝 양말들이 하나둘 짝짝이에게 이어 붙어, 몸이 마디마디 방이 달린 긴 이동집이 되었어요. " +
                    "마디마다 둥근 창문이 나고, 발바닥 대신 실뭉치 바퀴가 데굴데굴 굴러가요.\n" +
                    "지킴이로 서면 긴 몸으로 방위를 빙 둘러 감싸, 적의 발걸음을 늦추는 단단한 방벽이 돼요.")
                    .Needs((M.Cloth, 15), (M.Thread, 20), (M.Suitcase, 8))
                    .When((P.ExpeditionCount, 10), (P.EchoDeployCount, 30), (P.LineageLevel, 20)),

                Evolve("jjak_marcher", "양말 행진병", 1, "jjak_base", Assault, Echo,
                    "목이 긴 양말을 끝까지 당겨 신고, 커프를 행진 모자처럼 높이 세운 짝짝이예요. " +
                    "뒤꿈치를 맞부딪치는 발소리가 북소리가 되고, 돌돌 만 양말목은 짧은 지휘봉이 되었어요.\n" +
                    "잔향이로 소환되면 위험한 방향으로 가장 먼저 뛰어나가, 가까이 붙은 적을 힘껏 들이받아요.")
                    .Needs((M.Cloth, 12), (M.Ribbon, 15))
                    .When((P.EchoDeployCount, 20), (P.LineageLevel, 15)),

                Evolve("jjak_acrobat", "양말 곡예사", 1, "jjak_base", Capture, Echo,
                    "고무줄이 늘어나 버린 양말목을 용수철처럼 쓰는 곡예사예요. " +
                    "발끝으로 공중제비를 돌다가, 쭉 늘어난 몸을 고리처럼 던져 무엇이든 휘감아요.\n" +
                    "잔향이로 소환되면 적 사이를 통통 튀어 다니며 잠깐씩 꽁꽁 묶어 두어, 친구들이 공격할 틈을 만들어요.")
                    .Needs((M.Ribbon, 12), (M.Lace, 8), (M.Thread, 6))
                    .When((P.EchoDeployCount, 25), (P.LineageLevel, 15)),

                Evolve("jjak_postman", "양말 우편배달부", 1, "jjak_base", Support, Echo,
                    "양말 속 깊은 주머니가 그대로 우편 가방이 되었어요. " +
                    "짝을 기다리던 날들에 부치지 못한 편지를 모아, 이제는 다른 친구들의 마음을 대신 전하러 다녀요.\n" +
                    "잔향이로 소환되면 수선점에 따뜻한 소식을 배달해 내구도를 채우고, 곁의 친구들에게 힘을 실어 줘요.")
                    .Needs((M.Cloth, 10), (M.Lace, 6), (M.Suitcase, 3))
                    .When((P.ExpeditionCount, 8), (P.LineageLevel, 15)),

                Evolve("jjak_medic", "양말 의무병", 1, "jjak_base", Heal, Guardian,
                    "구멍 난 친구를 보면 그냥 지나치지 못해서, 커프에 바늘과 실을 꽂고 다니기 시작했어요. " +
                    "앞섶의 패치는 언제든 떼어 붙일 수 있는 반창고 천이에요.\n" +
                    "지킴이로 자리를 지키며 수선점의 찢어진 곳을 주기적으로 꿰매고, 해진 자리에 반창고 천을 덧대 처음보다 튼튼하게 만들어요.")
                    .Needs((M.Cloth, 15), (M.Thread, 10), (M.Wax, 6))
                    .When((P.ExpeditionCount, 6), (P.EchoDeployCount, 10), (P.LineageLevel, 15)),

                // ── 2차 진화 ──
                Evolve("jjak_signal", "양말 신호수", 2, "jjak_caravan", Support, Guardian,
                    "카라반 맨 꼭대기 깃대에 올라선 짝짝이예요. " +
                    "양쪽 발목에 감은 색실 깃발을 흔들어, 흩어진 친구들에게 \"이쪽이야!\" 하고 길을 알려요.\n" +
                    "지킴이로 서면 깃발 신호로 모두의 공격 박자를 맞추고, 수선점의 불빛이 꺼지지 않도록 지켜 내요.")
                    .Needs((M.Cloth, 30), (M.Ribbon, 25), (M.Suitcase, 10), (M.Moonlight, 3))
                    .When((P.LineageLevel, 40), (P.StageReached, 25), (P.GuardianKillCount, 400)),

                Evolve("jjak_rider", "양말 기수", 2, "jjak_marcher", Assault, Echo,
                    "행진하던 발이 지칠 무렵, 짝짝이는 털실로 엮은 목마 양말에 올라탔어요. " +
                    "목마의 갈기는 풀린 올이고, 고삐는 끊어진 양말목이에요.\n" +
                    "잔향이로 소환되면 누구보다 빠르게 전장을 가로질러, 가장 위험한 적에게 돌격해 큰 피해를 줘요.")
                    .Needs((M.Cloth, 25), (M.Ribbon, 20), (M.Spring, 6))
                    .When((P.EchoDeployCount, 80), (P.DungeonFloor, 5), (P.LineageLevel, 40)),

                Evolve("jjak_sniper", "양말 저격수", 2, "jjak_acrobat", Snipe, Guardian,
                    "곡예사 시절의 균형 감각으로, 높은 빨랫줄 위에 꼼짝 않고 서 있는 짝짝이예요. " +
                    "한쪽 단추 눈을 손으로 가리고, 바늘땀 눈 하나로 아주 먼 곳까지 겨눠요.\n" +
                    "지킴이로 서면 팽팽하게 당긴 양말목을 새총처럼 튕겨, 멀리 있는 적 하나를 정확히 꿰뚫어요.")
                    .Needs((M.Lace, 18), (M.Rib, 12), (M.Thread, 15), (M.Moonlight, 2))
                    .When((P.GuardianKillCount, 500), (P.StageReached, 28), (P.LineageLevel, 40)),

                Evolve("jjak_artisan", "양말 공방가", 2, "jjak_postman", Defense, Guardian,
                    "편지 대신, 이제는 망가진 물건들이 짝짝이를 찾아와요. " +
                    "골무 투구를 쓰고 실패를 방패 삼아, 몸통에 반짇고리를 매단 채 작은 공방을 차렸지요.\n" +
                    "지킴이로 서면 무엇이든 고쳐 내는 손재주로 방위를 촘촘히 꿰매어, 적을 늦추고 끝까지 버텨 내요.")
                    .Needs((M.Cloth, 35), (M.Thread, 20), (M.Spring, 8))
                    .When((P.ExpeditionCount, 25), (P.DungeonFloor, 6), (P.LineageLevel, 40)),

                Evolve("jjak_alchemist", "양말 연금술사", 2, "jjak_medic", Heal, Echo,
                    "의무병의 약상자가 어느새 반짝이는 병들로 가득 찼어요. " +
                    "양말 속에 별빛 가루와 촛농, 기억의 실을 넣고 흔들면 달콤한 묘약이 보글보글 끓어올라요.\n" +
                    "잔향이로 소환되면 다친 친구들에게 묘약을 나눠 주고, 녹 같은 나쁜 기운을 말끔히 씻어 내요.")
                    .Needs((M.Wax, 25), (M.Lace, 12), (M.Moonlight, 4))
                    .When((P.EchoDeployCount, 60), (P.DungeonFloor, 8), (P.LineageLevel, 40)),
            },
            costumes: WithSeasonCostume(new[]
            {
                Costume("jjak_nightkeeper", "밤지기 짝짝이", "외로움 → 위로", ItemGrade.Rare, Unlock.NightVisit,
                    "짝짝이는 수많은 외로운 밤을 수선집 창가에서 보냈어요. 긴 밤이 찾아와도 짝을 기다리는 일을 포기하지 않았지요.\n" +
                    "마을 사람들은 그가 잠들지 않고 지켜볼 수 있도록 달과 별 조각을 꿰매어 주었고, 짝짝이는 더욱 밝게 빛날 수 있었어요. " +
                    "달 장식이 달린 밤모자는 짝짝이가 밤을 지켜보는 힘이 되어 줘요.\n" +
                    "그렇게 짝짝이는 자신의 외로움을, 새로 찾아온 외로운 아이들을 따뜻하게 맞이하고 지켜 주는 밤지기로 바꾸었답니다."),

                Costume("jjak_cheer", "응원대장 짝짝이", "두려움 → 용기", ItemGrade.Common, Unlock.Stage(15),
                    "새로 살아난 친구들에게 첫 밤은 늘 무서워요. 짝짝이도 처음엔 그랬지요.\n" +
                    "그래서 짝짝이는 보라색 커프를 깃발 모자처럼 높이 세우고, 풀린 털실을 동글동글 뭉쳐 응원 방울을 달았어요. " +
                    "낯선 친구에게 가장 먼저 손을 흔드는 쪽이 되기로 한 거예요.\n" +
                    "\"괜찮아, 무서우면 내 손 잡아! 하나, 둘, 셋, 같이 가자!\""),

                Costume("jjak_mail", "우편배달 짝짝이", "단절 → 연결", ItemGrade.Common, Unlock.Expeditions(5),
                    "짝을 기다리는 동안 짝짝이는 매일 편지를 썼어요. 부칠 곳을 몰라 양말 속에 차곡차곡 쌓아 두기만 했지요.\n" +
                    "어느 날 짝짝이는 깨달았어요. 전하지 못한 마음을 품은 건 자기만이 아니라는 걸요. " +
                    "이제 짝짝이는 우편배달 모자를 쓰고, 양말 속 깊은 주머니에 모두의 편지를 담아 끊어진 마음을 이어 주러 떠나요.\n" +
                    "\"받는 사람이 없는 편지는 없어요. 아직 못 찾았을 뿐이에요.\""),

                Costume("jjak_traveler", "여행자 짝짝이", "기다림 → 탐색", ItemGrade.Rare, Unlock.Expeditions(15),
                    "창가에서 기다리기만 하던 짝짝이가 처음으로 수선점 문을 열고 나섰어요. 낡은 여행 가방에 지도와 실뭉치, 작은 랜턴을 챙겨서요.\n" +
                    "아직 아무도 찾지 못한 친구들이 어딘가에서 기다리고 있을 테니까요. " +
                    "짝짝이가 지나간 길에는, 돌아오는 길을 잃지 않도록 실 한 가닥이 길게 남아요.\n" +
                    "\"기다리는 것도 좋지만, 이번엔 내가 먼저 찾으러 갈래!\""),

                Costume("jjak_festival", "축제 지휘자 짝짝이", "외로움 → 축제", ItemGrade.Legendary, Unlock.EventShop(300),
                    "혼자였던 짝짝이 곁에 하나둘 친구들이 모이자, 수선점 골목은 어느새 작은 축제가 되었어요.\n" +
                    "짝짝이는 별 장식 지휘봉을 들고 퍼레이드 맨 앞에 섰어요. " +
                    "짝을 잃은 양말도, 떨어진 단추도, 다 타 버린 초도 모두 한 줄로 이어 걷는 밤을 위해서요.\n" +
                    "\"자, 다 같이! 오늘 밤은 아무도 혼자가 아니야!\""),

                Costume("jjak_caterpillar", "애벌레형 이동집 짝짝이", "작은 외로움 → 함께 가는 여행", ItemGrade.Legendary,
                    Unlock.Form("jjak_caravan"),
                    "여러 친구들을 싣고 기어가는 이동식 집이 된 짝짝이예요.\n" +
                    "많은 짝 양말들이 모여 느슨하게 이어지면서, 모두의 보금자리가 되는 여행하는 집이 되었어요. " +
                    "마디마다 각자의 방이 있고, 아늑한 출입구와 따뜻한 랜턴이 친구들을 맞이해요.\n" +
                    "작은 외로움도 연결되면, 세상을 바꾸는 축제가 된답니다!"),

                Costume("jjak_plush", "봉제친구 짝짝이", "텅 빔 → 나눔", ItemGrade.Epic, Unlock.Resonance(3),
                    "짝짝이가 이제 작은 친구들을 안고, 만들어 줘요.\n" +
                    "텅 비었던 몸 안에 솜과 천 조각, 따뜻한 마음을 채워 넣고, 바늘과 실로 작은 봉제 친구들을 하나둘 지어 주었어요. " +
                    "혼자였던 짝짝이가 이제는 함께하는 기쁨을 나눠요.\n" +
                    "작은 천 조각도, 외로운 마음도 사랑으로 이어지면 친구가 될 수 있어요."),

                Costume("jjak_puppet", "손인형 짝짝이", "외로움 → 웃음", ItemGrade.Epic, Unlock.Stage(30),
                    "손에 끼우기 쉬운 양말의 구조를 이야기를 전하는 손인형으로 바꾸어, 짝짝이만의 장점인 '함께하는 즐거움'을 더욱 키웠어요.\n" +
                    "입을 크게 벌렸다 닫으며 여러 목소리로 동화와 노래를 들려주고, 리본과 커튼으로 꾸민 작은 극장에서 " +
                    "외로운 아이들의 마음을 웃게 만들어요. 오늘의 이야기는 용기, 우정, 그리고 꿈이에요.\n" +
                    "\"자, 모두 모여 봐~ 이야기 한 편 들려줄게!\""),

                Costume("jjak_sleepcap", "수면모자 짝짝이", "불안 → 안심", ItemGrade.Epic, Unlock.EventShop(150),
                    "짝짝이가 수면모자 모양의 밤지기가 되어, 외로운 밤을 지켜봐요.\n" +
                    "양말의 긴 형태와 부드러운 밴드를 수면모자로 바꾸어, 잠든 아이들을 지켜 주는 밤지기로 다시 태어났어요. " +
                    "달빛 드림 랜턴과 꿈 베개를 곁에 두고, 별과 달 장식이 흔들릴 때마다 불안한 마음이 조용히 가라앉아요.\n" +
                    "\"쉿… 잘 자요. 짝짝이가 있잖아요.\""),

                Costume("jjak_threadweb", "실타래 연결망 짝짝이", "길 잃음 → 연결", ItemGrade.Mythic, Unlock.Resonance(5),
                    "길을 잃은 친구들을 잇는 실타래 안내자예요.\n" +
                    "짝짝이의 몸이 풀린 실타래로 되돌아가, 마법으로 빛나는 실이 되었어요. " +
                    "빛나는 실은 별자리처럼 이어져 보이지 않는 길을 밝히고, 친구들이 안전하게 도착할 수 있도록 길과 연결을 다시 엮어 내요.\n" +
                    "실은 이어져도, 마음은 자유롭게. 함께라서 길을 찾을 수 있어요."),
            }));

        /// <summary>
        /// Q-06 시즌 한정 의상 '별빛 산책 짝짝이' (별빛 산책 수첩 30칸, CostumeUnlockType.Season — SPEC4 §8.4 C-06, 그림 A14).
        /// 수첩이 켜졌고(MetaContent.Stretch.Season) 그림 Art/Costumes/jjak_starwalk가 있을 때만 줄을 둔다 —
        /// 그 전에는 도감 · 의상 목록에 얻을 수 없는 칸이 생기므로 두지 않고, 수첩 30칸이 대신 보상(별조각 50 + 선택 기억편 30)을 준다.
        /// 줄이 생기면 도감 의상 11종(별조각 +30)과 ach_costume에 함께 센다
        /// </summary>
        static CostumeDef[] WithSeasonCostume(CostumeDef[] costumes)
        {
            if (!HasSeasonCostume) return costumes;
            var list = new System.Collections.Generic.List<CostumeDef>(costumes)
            {
                Costume(MetaContent.StarwalkCostumeId, "별빛 산책 짝짝이", "길 잃음 → 함께 걷는 밤", ItemGrade.Epic, Unlock.Season,
                    "별빛 산책 수첩을 끝까지 채운 밤, 짝짝이는 별 조각을 꿰매 단 산책 망토를 선물로 받았어요.\n" +
                    "망토 자락이 스칠 때마다 골목에 작은 별빛 발자국이 남아, 길 잃은 친구들이 그 빛을 따라 수선점으로 돌아와요.\n" +
                    "\"오늘 밤엔 같이 걸을래? 별이 길을 알려 줄 거야.\""),
            };
            return list.ToArray();
        }

        /// <summary>시즌 의상 줄을 둘지 (수첩 확장 항목이 켜졌고 그림이 있을 때 — 그림 확인은 UiArt 한곳, P4-10)</summary>
        internal static bool HasSeasonCostume => MetaContent.Stretch.Season && UiArt.HasStarwalkCostume;
    }
}
