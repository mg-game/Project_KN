using static IdleGame.EditorTools.SkillKit;
using V = IdleGame.SkillVfx;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 짝짝이 외 계보 6종의 스킬 (기본형 + 1차 진화 2종씩).
    /// 계보마다 기본형 스킬의 성질을 두 갈래로 나눈다: 붙잡기 → 막기 / 돌기, 잠그기 → 열기 / 걸어 잠그기,
    /// 녹아드는 빛 → 모은 빛 / 옮겨 붙는 불씨, 우산 → 소나기 / 그늘, 입맞춤 → 마무리 / 반사, 자장가 → 왈츠 / 마지막 음.
    /// </summary>
    internal static partial class SkillContent
    {
        static SkillDef[] Ttokdan() => new[]
        {
            Form("ttok_base", "꼭 붙잡기", V.StunStars, "꼭 잡았다, 안 놔!",
                Every(9f, 3f), InRange(),
                "떨어진 자리를 잊지 못해, 한번 잡으면 절대 놓지 않아요. 네 개의 구멍에 실을 걸어 다가오는 적들을 단단히 붙들어요.",
                Stun(0.8f)),

            Form("ttok_shield", "단추 방패", V.ButtonShield, "단추 잠금, 철컥!",
                ShopHit(10f), Shop,
                "꼭 붙잡던 손이 이제는 문을 막아요. 수선점이 공격받으면 코트에서 가장 큰 단추가 문 앞을 덮고 단단히 잠가요.",
                Shield(3f, 6f)),

            Form("ttok_spinner", "회전 단추", V.SpinButton, "윙윙윙~!",
                Every(5f, 1.5f), Around(0.5f),
                "구멍에 꿴 실을 힘껏 당겼다 놓으면, 옛날 단추 팽이처럼 맹렬하게 돌아요. 가까이 다가온 적은 모두 튕겨 나가요.",
                Damage(1.8f), Knockback(0.3f)),
        };

        static SkillDef[] Chalkak() => new[]
        {
            Form("chal_base", "찰칵 잠금", V.KeyholeLock, "찰칵! 잠갔다.",
                Every(11f, 4f), Strongest,
                "열쇠 구멍마다 몸을 맞춰 보던 버릇으로 적의 약점을 찾아내요. 홈 파인 꼬리를 꽂고 '찰칵' 돌리면 가장 강한 적도 꼼짝 못 해요.",
                Stun(1.5f), Mark(25f, 5f)),

            Form("chal_master", "모든 문 열기", V.KeyBeam, "어떤 문이든 열어 줄게!",
                Every(9f, 2f), Around(1f),
                "닳고 닳아 어떤 자물쇠에도 맞게 된 홈으로, 숨은 문까지 모두 열어젖혀요. 열린 문틈으로 튄 열쇠 빛이 적에게서 적으로 옮겨 다녀요.",
                Reveal(6f), Chain(1.5f, 4)),

            Form("chal_lock", "최후의 빗장", V.KeyholeLock, "여기서부터는 못 지나가!",
                ShopLow, AllFoes,
                "맞는 자물쇠를 끝내 찾지 못한 찰칵이는 스스로 자물쇠가 되었어요. 수선점이 위험해지면 온몸의 걸쇠를 한꺼번에 철컥 잠가 버려요.",
                Stun(2.5f), Knockback(0.5f)),
        };

        static SkillDef[] Mongdang() => new[]
        {
            // 시작 친구의 힐 잔향이: 수명(14초) 동안 두 번 시전되게 첫 시전을 당긴다. 녹을수록 커지는 불꽃은 조합 '빗속의 등불'(우두커니)이 이어 준다
            Form("mong_base", "녹아드는 빛", V.WaxDrip, "아직 심지가 남았는걸!",
                Every(8f, 2f), Shop,
                "녹을수록 몸은 작아지지만, 흘러내린 촛농은 수선점의 갈라진 틈을 따뜻하게 메워요. 녹아 나온 불빛도 버리지 않고 수선점 등불에 한 방울씩 모아 둬요.",
                Heal(2f), Lamp(0.03f)),

            Form("mong_lantern", "등불 빛줄기", V.LanternBeam, "길을 밝혀 줄게!",
                LampLit, Lane,
                "녹아드는 빛을 초롱 안에 차곡차곡 모아 두었어요. 수선점에 불이 밝혀지면 그 빛을 받아 자기 방향으로 커다란 빛줄기를 쏘아, 숨어 있던 적까지 환히 비춰요.",
                Damage(4f), Reveal(10f), Mark(30f, 8f)),

            Form("mong_firefly", "반딧불 떼", V.Firefly, "반짝, 옮겨 붙어라!",
                OnKill(1.5f), AroundTarget(0.5f),
                "손톱만큼 작아진 몸으로 날아올랐어요. 적을 쓰러뜨리면 남은 불씨가 반딧불 떼가 되어 주변 적들에게 옮겨 붙어요.",
                Burn(0.8f, 4f)),
        };

        static SkillDef[] Udukeoni() => new[]
        {
            Form("udu_base", "우산 펼치기", V.UmbrellaShield, "비가 와도 괜찮아.",
                Every(14f, 3f), Shop,
                "접어 두었던 우산살을 활짝 펴 수선점을 덮어요. 비 오는 날에만 쓸모 있다고 믿지만, 우산 아래는 언제나 포근해요. 비 오는 밤에는 비 특성 시너지로 더 단단해져요.",
                Shield(3.5f, 10f)),

            Form("udu_storm", "소나기", V.Raindrops, "쏴아아~!",
                Summoned, AllFoes,
                "뒤집힌 우산 속에 모아 둔 비바람을, 소환되자마자 하늘 높이 쏘아 올려요. 온 거리에 소나기가 쏟아지면 적들의 발이 무거워져요.",
                Slow(0.35f, 5f), Damage(0.6f)),

            Form("udu_parasol", "그늘", V.ShieldBubble, "잠깐 쉬어 가.",
                Every(12f, 4f), AllEchoes,
                "비가 오지 않는 날에도 누군가를 지킬 수 있다는 걸 알았어요. 레이스 양산을 기울여 잔향이 친구들에게 시원한 그늘을 드리워요.",
                Shield(1.5f, 6f), Regen(0.4f, 6f)),
        };

        static SkillDef[] Rouge() => new[]
        {
            Form("rouge_base", "입맞춤 표식", V.LipstickHeart, "쪽! 여기 찜했어.",
                Every(7f, 2f), AroundTarget(0.5f),
                "돌려 올린 붉은 심으로 입술 자국을 꾹 찍어요. 자국이 남은 적은 친구들의 공격을 더 아프게 맞아요.",
                Mark(30f, 5f)),

            // 패시브라 전투 연출은 없다 (KissBurst 아이콘/대사는 캐릭터 화면용, 전투에서는 조합 '화장대의 비밀'이 터뜨린다)
            Form("rouge_velvet", "벨벳 마무리", V.KissBurst, "번지지 않게, 깔끔하게.",
                Always, Self,
                "벨벳 립스틱은 한번 그은 선이 절대 번지지 않아요. 약해진 적일수록 짙은 입술 자국으로 단숨에 지워 내요.",
                Execute(60f)),

            Form("rouge_gloss", "광택 반사", V.GlossShine, "반짝, 미끄러져!",
                OnKill(2f), AroundTarget(0.6f),
                "촉촉한 광택이 빛을 튕겨 내요. 적을 쓰러뜨리면 반사된 빛이 적 사이를 튀어 다니고, 흘러내린 글로스에 발이 끈적하게 붙어요.",
                Chain(1.3f, 4), Slow(0.45f, 3f)),
        };

        static SkillDef[] Lalabell() => new[]
        {
            Form("lala_base", "자장가", V.MusicNotes, "라- 라- 라-…",
                Every(15f, 5f), Shop,
                "태엽을 감으면 끝나지 않는 자장가가 흘러나와요. 노래가 닿는 곳마다 수선점의 상처가 스르르 아물어요.",
                Heal(3f), Regen(0.6f, 6f)),

            Form("lala_waltz", "회전 왈츠", V.WaltzRibbon, "하나, 둘, 셋, 빙글!",
                Every(9f, 2f), Around(0.8f),
                "멈춘 줄 알았던 태엽이 다시 감기고, 회전판 위에서 왈츠가 시작돼요. 리본처럼 풀려 나온 음표가 적들을 춤 속으로 끌어들여요.",
                Pull(0.35f), Slow(0.4f, 3f)),

            Form("lala_finale", "마지막 음", V.SoundRing, "이게… 나의 마지막 음이야.",
                ShopLow, AllFoes,
                "그토록 두려워하던 마지막 음을, 친구들을 지키기 위해 처음으로 연주해요. 수선점이 무너지려는 순간 단 한 음이 온 거리에 울려 퍼져요.",
                Damage(12f), Stun(1f)),
        };
    }
}
