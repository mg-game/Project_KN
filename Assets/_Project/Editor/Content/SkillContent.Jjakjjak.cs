using static IdleGame.EditorTools.SkillKit;
using V = IdleGame.SkillVfx;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 짝짝이 계보 스킬 11종. "한 짝만 남은 외로움"이 가족을 부르고(기본형), 식구를 태우고(카라반),
    /// 마음을 전하고(우편배달부 → 공방가), 상처를 꿰매고(의무병 → 연금술사), 앞장서는(행진병 → 기수) 힘이 된다.
    /// </summary>
    internal static partial class SkillContent
    {
        static SkillDef[] Jjakjjak() => new[]
        {
            Form("jjak_base", "가족 부르기", V.SummonCircle, "우리 식구, 모여라!",
                Every(16f, 5f), Self,
                "짝을 잃은 양말이라 다른 양말만 보면 무조건 가족이라고 불러요. \"우리 식구!\" 하고 외치면 길 잃은 아기 양말들이 쪼르르 달려와요.",
                Summon(0.4f, 2, 8f)),

            // ── 1차 진화 ──
            Form("jjak_caravan", "식구 열차", V.SummonCircle, "식구들, 문 열어!",
                ShopHit(14f), Self,
                "가족 부르기가 이동집이 되었어요. 수선점이 흔들리면 마디마디 방에 살던 짝 양말 식구들이 창문을 열고 우르르 뛰어나와요.",
                Summon(0.5f, 3, 9f)),

            Form("jjak_marcher", "쿵쿵 행진", V.DustPoof, "하나, 둘! 쿵!",
                OnKill(2.5f), Around(0.45f),
                "적을 하나 쓰러뜨릴 때마다 뒤꿈치를 맞부딪치며 더 힘차게 발을 굴러요. 행진이 이어질수록 먼지가 풀썩풀썩 일어나요.",
                Damage(1.6f), Knockback(0.15f)),

            Form("jjak_acrobat", "고무줄 올가미", V.SlowYarn, "휘리릭, 잡았다!",
                Every(8f, 2f), AroundTarget(0.45f),
                "늘어나 버린 양말목을 고리처럼 휘휘 돌려 던져요. 공중제비 한 번이면 흩어져 있던 적들이 한데 엉켜 꼼짝 못 해요.",
                Pull(0.25f), Stun(1f)),

            Form("jjak_postman", "마음 배달", V.HeartHeal, "편지 왔어요!",
                Dissolved, Shop,
                "빛으로 풀려 돌아가는 길에도 배달은 잊지 않아요. 양말 주머니 속 편지가 수선점에 닿으면 모두의 마음이 따뜻해져요.",
                Resonance(6f), Lamp(0.08f)),

            Form("jjak_medic", "천 덧대기", V.PatchHeal, "꿰매 줄게!",
                Every(10f, 4f), Shop,
                "커프에 꽂아 둔 바늘과 반창고 천으로 수선점의 찢어진 곳을 덧대요. 한 땀 한 땀 기운 자리는 처음보다 더 튼튼해요.",
                Heal(3f), Shield(1.5f, 6f)),

            // ── 2차 진화 ──
            // 공격력 강화는 양말 공방가 하나만 쓴다 (더 센 강화가 약한 강화를 덮어써서, 겹치면 한쪽이 사라진다)
            Form("jjak_signal", "불빛 신호", V.BuffCrown, "이쪽이야, 다 같이!",
                LampLit, AllGuards,
                "수선점에 불이 밝혀지면 카라반 꼭대기에서 색실 깃발을 힘껏 흔들어요. 깃발 신호를 본 지킴이들은 박자를 맞춰 더 빠르게 공격해요.",
                BuffSpeed(35f, 10f)),

            Form("jjak_rider", "목마 돌격", V.StarBurst, "이랴, 달려라!",
                Summoned, Strongest,
                "행진하던 발이 털실 목마에 올라탔어요. 소환되자마자 풀린 올 갈기를 휘날리며 가장 위험한 적에게 곧장 돌진해요.",
                Damage(6f), Stun(1f)),

            Form("jjak_sniper", "바늘땀 관통", V.ThreadStitch, "한 땀에 꿰뚫어!",
                Every(10f, 4f), Lane,
                "빨랫줄 위에서 바늘땀 눈 하나로 겨눈 뒤, 팽팽하게 당긴 양말목 새총으로 은바늘을 쏘아요. 실을 끌고 날아간 바늘이 한 줄로 늘어선 적을 모두 꿰어요.",
                Damage(4.5f)),

            Form("jjak_artisan", "출발 전 손질", V.CleanseSparkle, "다 고쳐 놨어!",
                BattleStart, AllGuards,
                "편지 대신 망가진 물건을 받아 오던 손재주로, 전투가 시작되기 전 모두의 장비를 꼼꼼히 손봐요. 골무 투구 아래 눈빛이 반짝여요.",
                BuffAttack(20f, 30f), Cleanse(30f)),

            Form("jjak_alchemist", "마지막 묘약", V.MoonGlow, "한 모금씩 나눠 마셔!",
                Dissolved, AllEchoes,
                "의무병 시절의 약상자가 반짝이는 묘약 병이 되었어요. 빛으로 풀리는 순간 마지막 병이 깨지며 달빛 가루가 친구들에게 쏟아져요.",
                Heal(2.5f), Cleanse(4f)),
        };
    }
}
