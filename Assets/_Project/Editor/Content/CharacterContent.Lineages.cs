using static IdleGame.CharacterRole;
using static IdleGame.CombatStyle;
using M = IdleGame.EditorTools.MaterialIds;
using P = IdleGame.ProgressType;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 짝짝이 외 계보 6종 (기본형 + 1차 진화 2종씩).
    /// 원래 사물의 구조가 몸이 되고(아트 가이드), 사물의 결함이 성격이 된다.
    /// 특성: 똑단이 [천·치장] · 찰칵이 [금속·소리] · 몽당이 [빛·밤] · 우두커니 [천·비] · 루즈리 [치장·금속] · 라라벨 [소리·빛]
    /// </summary>
    internal static partial class CharacterContent
    {
        static LineageDef Ttokdan() => Lineage(
            id: "ttokdan", name: "똑단이", species: "단추족", origin: "외투에서 떨어진 단추",
            grade: ItemGrade.Common, body: BodyType.Mascot,
            traits: new[] { TraitType.Fabric, TraitType.Charm },
            palette: "93C8F0 FFF4DC F2C46B 4A3B7C",
            personality:
                "외투에서 떨어져 나온 그날의 자리를 잊지 못해서, 무엇이든 한번 붙잡으면 놓지 않아요. " +
                "친구의 옷자락도, 문고리도, 지나가던 바람도 네 개의 구멍에 실을 걸어 꼭 붙들지요.\n" +
                "다시는 아무것도 잃고 싶지 않은 작은 단추의 고집은, 수선점 앞에서 누구보다 든든한 버팀이 되어 줘요.",
            forms: new[]
            {
                Base("ttok_base", "똑단이", Defense, Guardian,
                    "외투에서 떨어진 둥근 단추예요. 네 개의 구멍은 동그란 두 눈과 콧구멍이 되고, " +
                    "구멍에 남은 실 가닥은 짧은 팔다리가 되었어요.\n" +
                    "데굴데굴 굴러와 방위 앞에 딱 버티고 서면, 다가오는 적에게 꼭 매달려 놓지 않아 발걸음을 늦춰요."),

                Evolve("ttok_shield", "방패 단추", 1, "ttok_base", Defense, Guardian,
                    "코트에서 가장 큰 단추에 실을 겹겹이 감아, 몸 전체가 둥근 방패가 되었어요. " +
                    "단추 테두리의 도톰한 턱이 그대로 방패의 가장자리예요.\n" +
                    "지킴이로 서면 한층 단단해진 몸으로 방위를 막아서고, 부딪친 적을 꼭 붙잡아 느리게 만들어요.")
                    .Needs((M.Cloth, 10), (M.Thread, 6))
                    .When((P.ExpeditionCount, 5), (P.LineageLevel, 12)),

                Evolve("ttok_spinner", "팽이 단추", 1, "ttok_base", Assault, Echo,
                    "구멍에 꿴 실을 당겼다 놓으면 윙윙 돌아가던 옛날 단추 팽이처럼, " +
                    "똑단이는 몸에 감은 실을 힘껏 당겨 스스로 팽이가 되었어요.\n" +
                    "잔향이로 소환되면 맹렬하게 회전하며 적에게 파고들어, 가까운 거리에서 큰 피해를 줘요.")
                    .Needs((M.Thread, 8), (M.Ribbon, 10))
                    .When((P.EchoDeployCount, 15), (P.LineageLevel, 12)),
            });

        static LineageDef Chalkak() => Lineage(
            id: "chalkak", name: "키키", species: "열쇠족", origin: "맞는 문을 잃은 열쇠",
            grade: ItemGrade.Rare, body: BodyType.Mascot,
            traits: new[] { TraitType.Metal, TraitType.Sound },
            palette: "E8C27A C4B2F2 E9A6C6 3D2B5E",
            personality:
                "맞는 자물쇠를 잃어버린 열쇠라, 눈에 띄는 문마다 똑똑 두드리고 열쇠 구멍마다 몸을 맞춰 봐요. " +
                "백 번을 틀려도 \"이번엔 열릴지도 몰라!\" 하며 홈 파인 꼬리를 흔들지요.\n" +
                "그 끈질긴 호기심 덕분에, 남들이 지나친 작은 틈과 숨은 약점을 누구보다 먼저 찾아내요.",
            forms: new[]
            {
                Base("chal_base", "키키", Snipe, Guardian,
                    "둥근 열쇠 머리가 얼굴이 되고, 울퉁불퉁한 홈이 등뼈처럼 꼬리 끝까지 이어진 작은 열쇠예요. " +
                    "열쇠 구멍을 들여다보던 버릇 때문에, 한쪽 눈을 찡그리고 먼 곳을 겨누는 데 익숙해요.\n" +
                    "지킴이로 서면 뾰족한 꼬리 끝을 쏘아, 멀리 있는 적의 약점을 정확히 찔러요."),

                Evolve("chal_master", "마스터키 키키", 1, "chal_base", Support, Echo,
                    "수많은 문을 두드린 끝에, 키키의 홈은 어떤 자물쇠에도 조금씩 맞는 모양으로 닳았어요. " +
                    "어떤 문이든 열 수 있으니, 숨은 것들도 더는 숨을 수 없지요.\n" +
                    "잔향이로 소환되면 숨어 있던 적의 문을 활짝 열어 드러내고, 열쇠 빛을 적에게서 적으로 튕겨 친구들의 길을 열어 줘요.")
                    .Needs((M.Rib, 8), (M.Ribbon, 10), (M.Spring, 2))
                    .When((P.EchoDeployCount, 20), (P.LineageLevel, 15)),

                Evolve("chal_lock", "자물쇠 기사", 1, "chal_base", Defense, Guardian,
                    "맞는 자물쇠를 끝내 찾지 못한 키키는, 스스로 자물쇠가 되기로 했어요. " +
                    "묵직한 자물통을 갑옷처럼 두르고, 둥근 걸쇠를 투구처럼 썼지요.\n" +
                    "지킴이로 서면 방위를 철컥 잠그듯 막아서서, 들어오는 적을 묵직하게 늦춰요.")
                    .Needs((M.Rib, 10), (M.Cloth, 12))
                    .When((P.ExpeditionCount, 8), (P.LineageLevel, 15)),
            });

        static LineageDef Mongdang() => Lineage(
            id: "mongdang", name: "몽실", species: "촛불족", origin: "다 타버린 몽당초",
            grade: ItemGrade.Rare, body: BodyType.Mascot,
            traits: new[] { TraitType.Light, TraitType.Night },
            palette: "FFF1D6 E9A6C6 FFB86B 6B5A8E",
            personality:
                "녹을수록 몸이 작아지는 걸 알면서도, 끝까지 주변을 밝히는 몽당초예요. " +
                "촛농이 뚝뚝 흘러 키가 한 뼘씩 줄어도 \"괜찮아, 아직 심지가 남았는걸\" 하고 웃어요.\n" +
                "겁이 많아 바람 소리에도 불꽃이 움츠러들지만, 어두운 곳에 친구가 있으면 가장 먼저 불꽃을 키우는 작고 다정한 용기를 가졌어요.",
            forms: new[]
            {
                Base("mong_base", "몽실", Heal, Echo,
                    "다 타 버린 몽당초예요. 녹아내린 촛농이 동글동글한 몸이 되고, " +
                    "심지 끝의 작은 불꽃은 기분에 따라 커졌다 작아지는 더듬이가 되었어요.\n" +
                    "잔향이로 소환되면 녹아내린 촛농으로 수선점의 상처를 따스하게 메워 주고, 녹아 나온 불빛을 모아 수선점의 등불을 채워요."),

                Evolve("mong_lantern", "초롱 몽실", 1, "mong_base", Support, Guardian,
                    "바람에 꺼지지 않으려고, 몽실은 종이 초롱 속에 들어가 살기로 했어요. " +
                    "초롱의 가는 뼈대가 갈비뼈가 되고, 둥근 갓은 커다란 머리가 되었지요.\n" +
                    "지킴이로 서면 길 잃은 친구들의 길잡이가 되어, 수선점의 불빛을 받아 먼 어둠 속에 숨은 적까지 환히 비춰요.")
                    .Needs((M.Wax, 15), (M.Lace, 6), (M.Rib, 4))
                    .When((P.ExpeditionCount, 6), (P.LineageLevel, 15)),

                Evolve("mong_firefly", "반딧불 몽실", 1, "mong_base", Assault, Echo,
                    "녹고 또 녹아 손톱만큼 작아진 몽실은, 불꽃 하나 남은 몸으로 날아오르는 법을 배웠어요. " +
                    "촛농으로 빚은 얇은 날개를 달고, 반딧불처럼 깜빡이며 어둠 속을 누벼요.\n" +
                    "잔향이로 소환되면 재빠르게 적에게 날아들어, 뜨거운 불꽃으로 들이받아요.")
                    .Needs((M.Wax, 12), (M.Thread, 6))
                    .When((P.EchoDeployCount, 20), (P.LineageLevel, 15)),
            });

        static LineageDef Udukeoni() => Lineage(
            id: "udukeoni", name: "레이니", species: "우산족", origin: "주인을 기다린 우산",
            grade: ItemGrade.Legendary, body: BodyType.SemiHuman,
            traits: new[] { TraitType.Fabric, TraitType.Rain },
            palette: "7FA8D9 2D2458 F2C46B FFF4DC",
            personality:
                "주인을 기다리며 우산꽂이에서 몇 계절을 보낸 우산이에요. " +
                "비가 오지 않는 날엔 자신이 쓸모없다고 생각해, 구석에 우두커니 서서 하늘만 올려다보지요.\n" +
                "하지만 하늘이 흐려지고 첫 빗방울이 떨어지면, 접어 두었던 살을 활짝 펴고 누구보다 먼저 친구들의 머리 위를 덮어요. " +
                "기다림에 익숙한 만큼, 한번 지키기로 한 자리는 절대 떠나지 않아요.",
            forms: new[]
            {
                Base("udu_base", "레이니", Defense, Guardian,
                    "우산살이 갈비뼈가 되고, 펼친 우산천이 넓은 어깨와 망토가 되었으며, 구부러진 손잡이는 지팡이 같은 꼬리로 남았어요. " +
                    "비 오는 날에만 쓸모 있다고 믿어서 늘 고개를 푹 숙이고 있지요.\n" +
                    "그래도 지킴이로 서면 우산을 활짝 펴 방위를 덮고, 빗물에 젖은 듯 적의 발걸음을 무겁게 만들어요."),

                Evolve("udu_storm", "폭풍우산", 1, "udu_base", Capture, Echo,
                    "기다리던 비가 폭풍이 되어 찾아온 날, 레이니는 뒤집힌 우산처럼 하늘을 향해 몸을 활짝 열었어요. " +
                    "휘어진 살 사이로 비바람이 소용돌이쳐요.\n" +
                    "잔향이로 소환되면 회오리치는 우산 속에 적을 휘말아, 잠시 그 자리에 붙잡아 둬요.")
                    .Needs((M.Rib, 15), (M.Thread, 10), (M.Spring, 3))
                    .When((P.EchoDeployCount, 30), (P.ExpeditionCount, 6), (P.LineageLevel, 20)),

                Evolve("udu_parasol", "양산 레이니", 1, "udu_base", Heal, Guardian,
                    "비가 오지 않는 날에도 누군가를 지킬 수 있다는 걸, 레이니는 햇살 아래에서 처음 알았어요. " +
                    "레이스를 덧댄 양산이 된 몸은 그늘을 드리우는 법을 익혔지요.\n" +
                    "지킴이로 서면 시원한 그늘로 수선점을 쉬게 하고, 지친 잔향이 친구들의 상처를 보살펴요.")
                    .Needs((M.Rib, 12), (M.Lace, 12), (M.Ribbon, 8))
                    .When((P.ExpeditionCount, 12), (P.LineageLevel, 20)),
            });

        static LineageDef Rouge() => Lineage(
            id: "rouge", name: "루루팝", species: "립스틱족", origin: "서랍 속에 잊힌 립스틱",
            grade: ItemGrade.Epic, body: BodyType.SemiHuman,
            traits: new[] { TraitType.Charm, TraitType.Metal },
            palette: "E58BAE E8C27A FFF4DC 3D2B5E",
            personality:
                "화장대 서랍 깊은 곳에서 오래 잊혀 있던 립스틱이에요. " +
                "뚜껑이 닫히면 수줍어서 케이스 속으로 쏙 숨어 버리지만, 누군가 살짝 돌려 올려 주면 누구보다 당당해져요.\n" +
                "한번 무대에 오르면 반짝이는 입술 자국으로 자기 이름을 남기고 가는, 수줍음과 대담함 사이를 오가는 친구예요.",
            forms: new[]
            {
                Base("rouge_base", "루루팝", Support, Echo,
                    "금빛 케이스가 몸통이 되고, 돌려 올라오는 립스틱 심이 머리이자 창끝이 되었어요. " +
                    "뚜껑이 닫히면 수줍게 숨고, 비틀어 올리면 당당하게 솟아올라요.\n" +
                    "잔향이로 소환되면 쏙 솟은 붉은 심으로 적에게 입술 자국을 꾹 찍어, 친구들의 공격이 더 아프게 들어가게 해요."),

                Evolve("rouge_velvet", "벨벳 루루팝", 1, "rouge_base", Snipe, Guardian,
                    "매트한 벨벳으로 다시 태어난 루루팝은, 한번 그은 선을 절대 번지지 않게 남겨요. " +
                    "케이스의 회전 장치를 조준경처럼 돌려 거리를 재지요.\n" +
                    "지킴이로 서면 멀리 있는 적에게 짙은 입술 자국을 한 점 찍어, 단숨에 지워 내요.")
                    .Needs((M.Lace, 10), (M.Wax, 8), (M.Cloth, 6))
                    .When((P.ExpeditionCount, 8), (P.LineageLevel, 15)),

                Evolve("rouge_gloss", "글로스 루루팝", 1, "rouge_base", Capture, Echo,
                    "촉촉하게 녹아 글로스가 된 루루팝이에요. 반짝이는 몸에서 끈적한 광택이 뚝뚝 흘러내려요.\n" +
                    "잔향이로 소환되면 미끄러지듯 다가가 끈적한 광택을 칠해, 적의 발을 바닥에 꼭 붙여 둬요.")
                    .Needs((M.Wax, 14), (M.Lace, 8))
                    .When((P.EchoDeployCount, 25), (P.LineageLevel, 15)),
            });

        static LineageDef Lalabell() => Lineage(
            id: "lalabell", name: "엘리제", species: "오르골족", origin: "마지막 음이 고장 난 오르골",
            grade: ItemGrade.Mythic, body: BodyType.Humanoid,
            traits: new[] { TraitType.Sound, TraitType.Light },
            palette: "C4B2F2 F2B8D0 F2C46B FFF4DC",
            personality:
                "마지막 음이 고장 난 오르골이에요. 태엽을 감으면 누구보다 아름다운 왈츠를 연주하지만, " +
                "곡의 끝에 다다르면 언제나 멈칫하며 숨을 고르지요. 마지막 음을 연주하면 모든 것이 끝나 버릴까 봐 두렵거든요.\n" +
                "그래서 엘리제의 노래는 늘 끝나지 않은 채 맴돌아요. 그 긴 여운이 친구들의 등을 오래도록 다정하게 밀어 준답니다.",
            forms: new[]
            {
                Base("lala_base", "엘리제", Heal, Guardian,
                    "오르골의 회전판이 치맛자락처럼 빙글 도는 하체가 되고, 등의 태엽 열쇠는 심장처럼 똑딱이며 감겨요. " +
                    "빗살 같은 음판과 작은 핀들이 노래하는 목소리예요.\n" +
                    "지킴이로 서면 끝나지 않는 자장가로 수선점의 상처를 어루만져, 스르르 아물게 해요."),

                Evolve("lala_waltz", "왈츠 엘리제", 1, "lala_base", Support, Echo,
                    "멈춘 줄 알았던 태엽이 다시 감기고, 엘리제는 회전판 위에서 왈츠를 추기 시작했어요. " +
                    "한 바퀴 돌 때마다 음표가 리본처럼 풀려 나와요.\n" +
                    "잔향이로 소환되면 적들을 왈츠 속으로 끌어모아, 친구들이 한꺼번에 공격할 무대를 만들어 줘요.")
                    .Needs((M.Spring, 6), (M.Lace, 10), (M.Ribbon, 12))
                    .When((P.EchoDeployCount, 30), (P.LineageLevel, 20)),

                Evolve("lala_finale", "피날레 엘리제", 1, "lala_base", Snipe, Guardian,
                    "그토록 두려워하던 마지막 음을, 엘리제는 친구들을 지키기 위해 처음으로 연주하기로 했어요. " +
                    "음판의 마지막 빗살이 은빛 활처럼 길게 뻗어 나왔지요.\n" +
                    "지킴이로 서면 멀리 있는 적에게 단 한 음의 피날레를 울려, 가장 깊은 곳까지 꿰뚫어요.")
                    .Needs((M.Spring, 8), (M.Thread, 12), (M.Moonlight, 2))
                    .When((P.ExpeditionCount, 12), (P.StageReached, 20), (P.LineageLevel, 25)),
            });
    }
}
