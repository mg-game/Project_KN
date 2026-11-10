using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// UI 효과음 종류 (UISfx.Play). 값은 표 순서·게이트 배열 인덱스·AudioCatalog.UiMap 인덱스로 쓰므로 끝에만 더한다 (AUDIO-SPEC §2.1).
    /// 0~11은 예전 그대로. 12~80은 AUD-01에서 더했다 — 소리 파일이 없으면 조용히 지나간다.
    /// 5개(FeatureUnlock · EvolveReveal · GachaNewFriend · FirstMeeting · Graduation)는 징글(Stinger 버스)로, Error는 Deny 소리를 빌려 쓴다.
    /// </summary>
    public enum UISfxKey
    {
        /// <summary>버튼 누름 (PressScale, 손을 뗀 곳이 버튼 안일 때 · 볼륨 0.5)</summary>
        Tap = 0,
        /// <summary>하단 탭 바꿈 (MainTabController)</summary>
        Tab = 1,
        PopupOpen = 2,
        PopupClose = 3,
        /// <summary>막힘 (잠긴 기능, 재화 부족)</summary>
        Deny = 4,
        /// <summary>단추가 상단 바에 닿음 (RewardFlyer — 0.3초 안에 이어지면 음이 조금씩 올라간다)</summary>
        CoinLand = 5,
        /// <summary>별조각이 상단 바에 닿음</summary>
        GemLand = 6,
        /// <summary>레벨업 팡파르 (≤1.2초)</summary>
        LevelUp = 7,
        /// <summary>출석 도장</summary>
        Stamp = 8,
        /// <summary>받기 버튼</summary>
        Claim = 9,
        /// <summary>소환 카드 뒤집기</summary>
        GachaFlip = 10,
        /// <summary>높은 등급 소환 공개 (전설, ≤1.2초)</summary>
        GachaRare = 11,

        // ── AUD-01 추가 (12~80, 값 고정) ──

        /// <summary>IA 시트 열림 (리넨 판이 스르륵 올라옴)</summary>
        SheetOpen = 12,
        /// <summary>IA 시트 닫힘 (부르는 쪽 볼륨 0.6)</summary>
        SheetClose = 13,
        /// <summary>가운데 큰 버튼 (사냥)</summary>
        MainButton = 14,
        /// <summary>확인 버튼 / MessagePopup 확인</summary>
        Confirm = 15,
        /// <summary>재화 사용 성공 (CostButtonView)</summary>
        Spend = 16,
        /// <summary>오류 토스트 (Deny 소리를 빌려 쓰고 볼륨만 따로)</summary>
        Error = 17,
        /// <summary>안내 토스트</summary>
        Toast = 18,
        /// <summary>빨간 점이 새로 생김 (2초에 한 번)</summary>
        RedDotNew = 19,
        /// <summary>기능 열림 카드 (징글)</summary>
        FeatureUnlock = 20,
        /// <summary>가이드 퀘스트 완료</summary>
        QuestComplete = 21,
        /// <summary>미션 상자 · 챕터/잃어버린 상자 열기</summary>
        MissionChest = 22,
        /// <summary>출석 7·28일째 · 복귀 3일째 큰 도장</summary>
        StampSpecial = 23,
        /// <summary>수선 바구니 받기</summary>
        BasketClaim = 24,
        /// <summary>빠른 수선</summary>
        QuickIdle = 25,
        /// <summary>복귀 화면 숫자 올라감 한 칸 (음이 사다리로 올라감, 70ms에 하나)</summary>
        CountTick = 26,
        /// <summary>복귀 화면 숫자 다 올라감</summary>
        CountFinish = 27,
        /// <summary>×2 받기</summary>
        RewardDouble = 28,
        /// <summary>전투력 '+N'</summary>
        PowerUp = 29,
        /// <summary>10레벨마다 · 새 진화 단서</summary>
        LevelUpMilestone = 30,
        /// <summary>친구 레벨업</summary>
        FriendLevelUp = 31,
        /// <summary>공명 단계 오름</summary>
        ResonanceUp = 32,
        /// <summary>진화 시작 (실 감기)</summary>
        EvolveCharge = 33,
        /// <summary>진화 변신 번쩍임</summary>
        EvolveBurst = 34,
        /// <summary>진화 공개 (징글)</summary>
        EvolveReveal = 35,
        /// <summary>의상 입기</summary>
        CostumeEquip = 36,
        /// <summary>의상 열림</summary>
        CostumeUnlock = 37,
        /// <summary>소환 시작 (보따리 떨어짐)</summary>
        GachaPullStart = 38,
        /// <summary>보따리 풀림</summary>
        GachaBundleOpen = 39,
        /// <summary>소환 카드 톡 나타남</summary>
        GachaCardAppear = 40,
        /// <summary>전설 이상 카드 기대 (0.45초)</summary>
        GachaAnticipate = 41,
        /// <summary>카드 공개 ★1</summary>
        GachaRevealCommon = 42,
        /// <summary>카드 공개 ★2</summary>
        GachaRevealRare = 43,
        /// <summary>카드 공개 ★3</summary>
        GachaRevealEpic = 44,
        /// <summary>카드 공개 신화 (오르골이 마지막 음을 낸다)</summary>
        GachaRevealMythic = 45,
        /// <summary>새 친구 (징글)</summary>
        GachaNewFriend = 46,
        /// <summary>중복 카드가 기억편으로 녹음</summary>
        GachaDuplicate = 47,
        /// <summary>소환 결과 건너뛰기</summary>
        GachaSkip = 48,
        /// <summary>편성 끌기 시작</summary>
        DragPick = 49,
        /// <summary>편성 놓기</summary>
        DragDrop = 50,
        /// <summary>편성 자리 바꿈</summary>
        DragSwap = 51,
        /// <summary>스위치 켬</summary>
        ToggleOn = 52,
        /// <summary>스위치 끔</summary>
        ToggleOff = 53,
        /// <summary>설정 볼륨 미리 듣기 (150ms에 하나)</summary>
        SliderTick = 54,
        /// <summary>원정 출발</summary>
        ExpeditionDepart = 55,
        /// <summary>원정 귀환 받기</summary>
        ExpeditionReturn = 56,
        /// <summary>던전 소탕</summary>
        DungeonSweep = 57,
        /// <summary>우편 받기 / 모두 받기</summary>
        MailClaim = 58,
        /// <summary>상점 구매 성공</summary>
        Purchase = 59,
        /// <summary>코치 마크 · 말풍선 등장</summary>
        CoachPop = 60,
        /// <summary>튜토리얼 한 단계 완료</summary>
        TutorialStep = 61,
        /// <summary>첫 만남 카드 (징글)</summary>
        FirstMeeting = 62,
        /// <summary>졸업 카드 (징글)</summary>
        Graduation = 63,
        /// <summary>이야기 책장 넘김</summary>
        StoryPage = 64,
        /// <summary>잠자기 모드 들어감</summary>
        SleepEnter = 65,
        /// <summary>잠자기 모드 깨어남</summary>
        SleepWake = 66,
        /// <summary>전투 2배속 토글</summary>
        SpeedToggle = 67,
        /// <summary>전투 AUTO 토글</summary>
        AutoToggle = 68,
        /// <summary>보스 도전 버튼</summary>
        BossChallenge = 69,
        /// <summary>업적 받기</summary>
        AchievementClaim = 70,
        /// <summary>도감 새로 발견</summary>
        CodexDiscover = 71,
        /// <summary>호감도 단계 오름</summary>
        AffinityUp = 72,
        /// <summary>쓰다듬기 (말랑 삑)</summary>
        PetSqueak = 73,
        /// <summary>챕터 지도 칸 열림</summary>
        MapNode = 74,
        /// <summary>시즌 단계 오름</summary>
        TierUp = 75,
        /// <summary>던전 축복 고르기</summary>
        BlessingPick = 76,
        /// <summary>큰 보상 팝업</summary>
        RewardBig = 77,
        /// <summary>3-2-1 카운트다운 한 칸</summary>
        Countdown = 78,
        /// <summary>진행 막대 차오름</summary>
        ProgressFill = 79,
        /// <summary>패배 조언 고리</summary>
        DefeatAdvice = 80,
    }

    /// <summary>
    /// 예전 UI 효과음·배경음 표 (M18 Phase B, 선택 사항 — 빌드하지 않는다). Resources/Audio/UISfxTable.asset에 두면 UISfx가 부팅 때 읽는다.
    /// 지금 소리는 AudioLibrary(Resources/Audio/AudioLibrary, AudioDataBuilder가 Assets/_Project/Audio에서 만든다)가 먼저이고,
    /// 이 표는 라이브러리에 그 소리가 없을 때만 쓴다 (AUDIO-SPEC §3.2). 배경음은 라이브러리에 bgm_battle_night가 없을 때만 Main 음악으로 튼다.
    /// 칸이 비었거나 clips가 비면 그 소리는 조용히 건너뛴다.
    /// </summary>
    [CreateAssetMenu(fileName = "UISfxTable", menuName = "IdleGame/UI Sfx Table")]
    public class UISfxTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public UISfxKey key;
            [Tooltip("여러 개면 무작위로 하나 (같은 소리가 기계적으로 들리지 않게)")]
            public AudioClip[] clips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Tooltip("배경음 (반복). 비면 배경음 없음")]
        public AudioClip bgm;
        [Range(0f, 1f)] public float bgmVolume = 1f;
        public List<Entry> entries = new();

        Entry[] _byKey;

        /// <summary>그 소리 칸 (없으면 null). 같은 키가 둘이면 앞의 것</summary>
        public Entry Get(UISfxKey key)
        {
            EnsureCache();
            int index = (int)key;
            return index >= 0 && index < _byKey.Length ? _byKey[index] : null;
        }

        /// <summary>그 소리 칸의 클립 하나 (random01 = 0~1로 고름). 없으면 null</summary>
        public AudioClip Pick(UISfxKey key, float random01)
        {
            var entry = Get(key);
            var clips = entry?.clips;
            if (clips == null || clips.Length == 0) return null;
            int i = Mathf.Clamp((int)(random01 * clips.Length), 0, clips.Length - 1);
            return clips[i];
        }

        /// <summary>클립이 하나라도 있는지 (배경음 포함)</summary>
        public bool HasAnyClip
        {
            get
            {
                if (bgm != null) return true;
                if (entries == null) return false;
                for (int i = 0; i < entries.Count; i++)
                {
                    var clips = entries[i]?.clips;
                    if (clips == null) continue;
                    for (int c = 0; c < clips.Length; c++)
                    {
                        if (clips[c] != null) return true;
                    }
                }
                return false;
            }
        }

        /// <summary>entries를 코드로 바꾼 뒤 부른다 (키 조회 캐시 비우기)</summary>
        public void ClearCache() => _byKey = null;

        void OnValidate() => ClearCache();

        void EnsureCache()
        {
            if (_byKey != null) return;
            _byKey = new Entry[UISfxGate.KeyCount];
            if (entries == null) return;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                int index = (int)entry.key;
                if (index >= 0 && index < _byKey.Length && _byKey[index] == null) _byKey[index] = entry;
            }
        }
    }
}
