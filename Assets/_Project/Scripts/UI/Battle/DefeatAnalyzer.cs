using System;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 패배 분석 (CR-01, SPEC4 §7.2 — 순수 C#, 고정 크기, 할당 없음): 수선점을 친 적의 공격(EnemyStrike, TargetId −1)을 방향 칸(0 위 · 1 왼쪽 위 ·
    /// 2 왼쪽 아래 · 3 오른쪽 아래 · 4 오른쪽 위 — EnemyFxCatalog.ShopStrikeSlot)마다 더하고, 칸마다 가장 많이 친 적(9종까지)을 기억한다. 스테이지가 바뀌면 비운다.
    /// 보여 주기: 튜토리얼이 끝났고 피해 합 ≥ 1이고, 같은 스테이지에서 10분 안에 이미 보여 주지 않았을 때 (파밍 반복에 다시 뜨지 않게 — 저장 안 함)
    /// </summary>
    public sealed class DefeatAnalyzer
    {
        public const int Slots = BattleGeometry.SlotCount;
        /// <summary>칸마다 기억하는 적 종류 수</summary>
        public const int EnemyKinds = 9;
        /// <summary>같은 스테이지에서 다시 보여 주기까지 (초, 실제 시간)</summary>
        public const float RepeatWindow = 600f;
        const int ShownMemory = 8;

        readonly float[] _slotDamage = new float[Slots];
        readonly string[] _enemyIds = new string[Slots * EnemyKinds];
        readonly float[] _enemyDamage = new float[Slots * EnemyKinds];
        float _total;
        readonly int[] _shownStage = new int[ShownMemory];
        readonly float[] _shownAt = new float[ShownMemory];
        int _shownCursor;

        public DefeatAnalyzer()
        {
            for (int i = 0; i < ShownMemory; i++)
            {
                _shownStage[i] = -1;
                _shownAt[i] = -1e9f;
            }
        }

        /// <summary>이번 판 수선점 피해 합</summary>
        public float Total => _total;

        /// <summary>보여 줄 자료가 있는지 (합 ≥ 1)</summary>
        public bool HasData => _total >= 1f;

        /// <summary>새 판 (StageChanged): 칸 · 적 기록을 비운다 (보여 준 기억은 남긴다)</summary>
        public void Reset()
        {
            Array.Clear(_slotDamage, 0, _slotDamage.Length);
            Array.Clear(_enemyIds, 0, _enemyIds.Length);
            Array.Clear(_enemyDamage, 0, _enemyDamage.Length);
            _total = 0f;
        }

        /// <summary>수선점을 친 공격 (월드 자리 → 방향 칸). 자리가 가운데(0)이거나 값이 0 이하면 무시</summary>
        public void AddShopStrike(Vector2 worldPosition, float value, string enemyId) =>
            AddSlotDamage(EnemyFxCatalog.ShopStrikeSlot(worldPosition), value, enemyId);

        /// <summary>방향 칸에 피해를 더한다 (적 종류는 칸마다 9개까지 — 넘치면 칸 합에만 더한다)</summary>
        public void AddSlotDamage(int slot, float value, string enemyId)
        {
            if (slot < 0 || slot >= Slots || !(value > 0f) || float.IsInfinity(value)) return;
            _slotDamage[slot] += value;
            _total += value;
            if (string.IsNullOrEmpty(enemyId)) return;
            int start = slot * EnemyKinds;
            for (int i = start; i < start + EnemyKinds; i++)
            {
                if (_enemyIds[i] == null)
                {
                    _enemyIds[i] = enemyId;
                    _enemyDamage[i] = value;
                    return;
                }
                if (string.Equals(_enemyIds[i], enemyId, StringComparison.Ordinal))
                {
                    _enemyDamage[i] += value;
                    return;
                }
            }
        }

        /// <summary>칸의 피해 합</summary>
        public float SlotDamage(int slot) => slot >= 0 && slot < Slots ? _slotDamage[slot] : 0f;

        /// <summary>가장 많이 뚫린 칸 (자료가 없으면 −1, 같으면 앞 칸)</summary>
        public int TopSlot
        {
            get
            {
                int best = -1;
                float max = 0f;
                for (int i = 0; i < Slots; i++)
                {
                    if (_slotDamage[i] <= max) continue;
                    max = _slotDamage[i];
                    best = i;
                }
                return best;
            }
        }

        /// <summary>칸의 몫 (0~1)</summary>
        public float Share(int slot) => _total > 0f ? SlotDamage(slot) / _total : 0f;

        /// <summary>가장 많이 뚫린 칸의 몫 % (반올림, 0~100)</summary>
        public int TopPercent => RoundPercent(Share(TopSlot));

        /// <summary>그 칸을 가장 많이 친 적 ID (없으면 null)</summary>
        public string TopEnemy(int slot)
        {
            if (slot < 0 || slot >= Slots) return null;
            string best = null;
            float max = 0f;
            int start = slot * EnemyKinds;
            for (int i = start; i < start + EnemyKinds; i++)
            {
                if (_enemyIds[i] == null || _enemyDamage[i] <= max) continue;
                max = _enemyDamage[i];
                best = _enemyIds[i];
            }
            return best;
        }

        /// <summary>몫 → % (반올림, 0~100)</summary>
        public static int RoundPercent(float share) => Mathf.Clamp(Mathf.RoundToInt(share * 100f), 0, 100);

        /// <summary>보여 줄지: 튜토리얼이 끝났고 자료가 있고, 이 스테이지에서 10분 안에 보여 준 적이 없을 때</summary>
        public bool ShouldShow(bool ftueDone, int stage, float now) => ftueDone && HasData && !ShownRecently(stage, now);

        /// <summary>보여 줬다 (같은 스테이지 10분 규칙)</summary>
        public void MarkShown(int stage, float now)
        {
            for (int i = 0; i < ShownMemory; i++)
            {
                if (_shownStage[i] != stage) continue;
                _shownAt[i] = now;
                return;
            }
            _shownStage[_shownCursor] = stage;
            _shownAt[_shownCursor] = now;
            _shownCursor = (_shownCursor + 1) % ShownMemory;
        }

        bool ShownRecently(int stage, float now)
        {
            for (int i = 0; i < ShownMemory; i++)
            {
                if (_shownStage[i] == stage && now - _shownAt[i] < RepeatWindow) return true;
            }
            return false;
        }

        /// <summary>방향 칸 이름 (0 위쪽 · 1 왼쪽 위 · 2 왼쪽 아래 · 3 오른쪽 아래 · 4 오른쪽 위)</summary>
        public static string DirectionName(int slot) => slot switch
        {
            0 => "위쪽",
            1 => "왼쪽 위",
            2 => "왼쪽 아래",
            3 => "오른쪽 아래",
            4 => "오른쪽 위",
            _ => "어느 쪽",
        };

        /// <summary>
        /// 설명 판 제목 '{방향}이/가 많이 뚫렸어요' (SPEC4 §7.2 — 조사는 받침으로: 위쪽이 · 왼쪽 위가 · 오른쪽 아래가). 고리를 띄울 때 한 번
        /// </summary>
        public static string Title(int slot) => KoreanText.WithSubject(DirectionName(slot)) + " 많이 뚫렸어요";

        /// <summary>
        /// 그 칸을 가장 많이 친 적에 맞춘 조언 (SPEC4 부록 A.6 — 콘텐츠 정의가 생기기 전까지 코드 기본값). 모르는 적은 일반 조언
        /// </summary>
        public static string Advice(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return "그 방향에 지킴이를 세우거나 강화해 보세요";
            if (enemyId.StartsWith("boss_", StringComparison.Ordinal)) return "보스가 문을 두드려요. 불 밝히기를 보스까지 아껴 보세요";
            return enemyId switch
            {
                "enemy_moth" => "빠른 적이에요. 그 방향에 감속(방어) 지킴이를 세워요",
                "enemy_thimble" => "보호막 적이에요. 저격이나 표식으로 먼저 깨요",
                "enemy_tangle" => "갈라지는 적이에요. 돌격(범위) 친구가 좋아요",
                "enemy_wisp" => "숨는 적이에요. 빛 특성(몽당이·라라벨)이 드러내요",
                "enemy_windup" => "뒤쪽 태엽쥐를 저격으로 먼저 노려요",
                "enemy_rust" => "녹을 묻혀요. 힐이나 정화 친구가 좋아요",
                "enemy_dust" => "수가 많아요. 범위 공격과 불 밝히기로 막아요",
                _ => "그 방향에 지킴이를 세우거나 강화해 보세요",
            };
        }
    }
}

namespace IdleGame
{
    /// <summary>
    /// 마지막 실패의 약한 방향 (CR-01 → PRG-03 강해지기 '약한 방향 보강' 줄, P4-1 StrengthenAdvisor가 읽기만 한다).
    /// 전투 화면이 실패할 때마다 패배 분석 자료로 채우고(보여 주기 10분 규칙과 상관없이), 그 스테이지를 클리어하면 비운다. 저장 안 함, 할당 없음
    /// </summary>
    public static class LastDefeat
    {
        /// <summary>자료가 있는지 (마지막 실패에 수선점 피해 합 ≥ 1)</summary>
        public static bool HasData { get; private set; }
        /// <summary>가장 많이 뚫린 방향 칸 (0 위 · 1 왼쪽 위 · 2 왼쪽 아래 · 3 오른쪽 아래 · 4 오른쪽 위, 없으면 −1)</summary>
        public static int Slot { get; private set; } = -1;
        /// <summary>그 칸의 몫 % / 실패한 스테이지</summary>
        public static int Percent { get; private set; }
        public static int Stage { get; private set; }

        /// <summary>방향 이름 ('왼쪽 위' 등, 없으면 null)</summary>
        public static string Direction => HasData ? UI.DefeatAnalyzer.DirectionName(Slot) : null;

        public static void Record(int stage, int slot, int percent)
        {
            HasData = slot >= 0;
            Slot = slot;
            Percent = percent;
            Stage = stage;
        }

        public static void Clear()
        {
            HasData = false;
            Slot = -1;
            Percent = 0;
            Stage = 0;
        }
    }
}
