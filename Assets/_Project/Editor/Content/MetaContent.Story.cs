namespace IdleGame.EditorTools
{
    /// <summary>
    /// CR-05 동화책 (SPEC4 §8.1 · 부록 A.5): 챕터 보스(x-10)를 처음 물리친 뒤 한 장 (1~5장, 6장부터는 동화책 없음 — 쪽을 건너뛴다).
    ///   · 장마다 그림 3칸 + 한 줄씩 (40자 이하). 그림 Art/Story/ch{n}_{1..3} (UiArt.StoryPanel) — 아직 없으면 null → 글만 (PanelInset 카드 + 장 색).
    ///   · 처음 읽고 닫을 때 별조각 50 (한 번). 동화책 5장 250 + 챕터 상자 1~5장 590 = 840 ≤ 900 (§11.2).
    /// 담당: P4-8.
    /// </summary>
    internal static partial class MetaContent
    {
        /// <summary>처음 읽은 선물 (별조각)</summary>
        internal const int StoryFirstReadGem = 50;

        /// <summary>동화책 한 줄 최대 글자 수 (그림 아래 Body 26, 940px)</summary>
        internal const int StoryLineMaxLength = 40;

        static partial void FillStory(MetaDatabase db, GameContent content)
        {
            var list = db.storyChapters;
            list.Add(StoryChapter(1, "불이 켜진 골목", "잃어버린 골목",
                "잊힌 물건들이 굴러드는 골목 끝, 작은 수선점에 불이 켜졌어요.",
                "짝을 잃은 양말이 문을 두드렸어요. \"여기, 가족이 있나요?\"",
                "큰그림자가 물러간 밤, 골목의 가로등이 하나둘 다시 켜졌어요."));
            list.Add(StoryChapter(2, "비 오는 광장의 우산", "비 오는 광장",
                "비 오는 광장 한가운데, 펼쳐진 우산 하나가 주인을 기다렸어요.",
                "\"비가 그치면 나는 쓸모없어질까?\" 우산이 작게 중얼거렸어요.",
                "수선점 식구들이 우산 아래 모여 앉자, 빗소리가 노래처럼 들렸어요."));
            list.Add(StoryChapter(3, "마지막 음", "먼지 쌓인 다락방",
                "다락방 상자 속에서 마지막 음이 고장 난 오르골이 깨어났어요.",
                "태엽을 감을 때마다 오르골은 끝 음 앞에서 멈칫했어요.",
                "\"끝나도 괜찮아. 우리가 다음 노래를 이어 줄게.\""));
            list.Add(StoryChapter(4, "서랍 속 색", "분실물 보관소",
                "선반마다 이름표 없는 물건들이 빼곡한 분실물 보관소예요.",
                "서랍 속 립스틱이 뚜껑을 살짝 열고 바깥을 엿보았어요.",
                "돌려 올린 립스틱이 반짝 웃자, 보관소에 다정한 색이 번졌어요."));
            list.Add(StoryChapter(5, "별빛 세탁소", "별빛 세탁소",
                "밤에만 문을 여는 세탁소에서 별빛에 마른 천 냄새가 났어요.",
                "몽당초는 녹아 작아져도 세탁소 구석을 끝까지 밝혔어요.",
                "수선점에 돌아온 친구들이 말했어요. \"잊혀도, 우리는 여기 있어.\""));
        }

        static StoryChapterDef StoryChapter(int chapter, string title, string place, params string[] lines)
        {
            var def = new StoryChapterDef
            {
                chapter = chapter,
                title = title,
                place = place,
                firstReadRewards = Rewards(Gem(StoryFirstReadGem)),
            };
            for (int i = 0; i < lines.Length; i++)
            {
                def.panels.Add(new StoryPanelDef { art = UiArt.StoryPanel(chapter, i + 1), text = lines[i] });
            }
            return def;
        }
    }
}
