using System;

namespace IdleGame.UI
{
    /// <summary>
    /// 스킬 이름 · 대사 말풍선 들여보내기 (FX-06, SPEC4 §3.4 — M41 말풍선 규칙보다 먼저). 순수 C#, 고정 크기 배열 — 할당 없음.
    ///   · 1.2초(Interval)에 하나만 들어간다. 큰 스킬 이름 알림(BattleNoticeView — 30초 AllowBig 규칙은 그대로)도 들어가면 창을 쓴다 (NoteNotice).
    ///   · 같은 스킬은 10초(SameSkill)에 한 번.
    ///   · 우선순위: P0 = 전설 이상 시전자 또는 큰 스킬, P1 = 처음 보는 스킬 (도감 Codex.HasSeenSkill — 들여보낼 때 MarkSkillSeen, 없으면 이 판의 기억),
    ///     P2 = 나머지. 창 안에서 온 P0/P1은 한 칸짜리 대기에 0.6초(PendingLife)까지 기다리고, 더 높은 우선순위가 오면 바뀐다. P2는 기다리지 않는다.
    ///   · 설정 '스킬 이름'(GameSettings.SkillNames)이 꺼지면 아무것도 들여보내지 않는다 (Enabled). 기믹 안내 · 분노 알림 · 쓰다듬기 말은 스킬 이름이 아니라 그대로 (AllowsNotice).
    /// </summary>
    public sealed class CalloutGate
    {
        /// <summary>들여보내기 간격 (초)</summary>
        public const float Interval = 1.2f;
        /// <summary>같은 스킬 다시 들여보내기까지 (초)</summary>
        public const float SameSkill = 10f;
        /// <summary>대기 칸에서 기다리는 최대 시간 (초)</summary>
        public const float PendingLife = 0.6f;
        /// <summary>우선순위 (작을수록 먼저)</summary>
        public const int P0 = 0, P1 = 1, P2 = 2;

        const int SkillMemory = 24;
        const int SeenMemory = 64;

        /// <summary>대기 칸에 있던 요청 (TryTakePending이 꺼내 준다)</summary>
        public struct PendingCallout
        {
            public string SkillId;
            public int Priority;
            /// <summary>요청한 시각</summary>
            public float At;
            /// <summary>부르는 쪽이 붙인 번호 (말풍선 내용은 부르는 쪽이 이 번호로 들고 있다)</summary>
            public int Token;
        }

        readonly string[] _skills = new string[SkillMemory];
        readonly float[] _skillAt = new float[SkillMemory];
        int _skillCursor;
        readonly string[] _seen = new string[SeenMemory];
        int _seenCursor;
        float _lastAdmit = -999f;
        bool _hasPending;
        PendingCallout _pending;

        /// <summary>설정 '스킬 이름' (꺼지면 모두 막는다)</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>처음 보는 스킬인지 묻기 (도감 Codex.HasSeenSkill). null = 이 판의 기억만</summary>
        public Func<string, bool> HasSeen { get; set; }

        /// <summary>처음 본 스킬 기록 (도감 Codex.MarkSkillSeen). null = 이 판의 기억만</summary>
        public Action<string> MarkSeen { get; set; }

        /// <summary>대기 칸에 요청이 있는지</summary>
        public bool HasPending => _hasPending;

        /// <summary>
        /// 이 스킬의 우선순위: 큰 스킬 · 전설 이상 시전자 P0, 처음 보는 스킬 P1, 나머지 P2.
        /// 처음인지는 도감(HasSeen)과 이 판의 기억으로 본다 (기록은 들여보낼 때만)
        /// </summary>
        public int PriorityFor(string skillId, bool bigOrLegendary)
        {
            if (bigOrLegendary) return P0;
            return Seen(skillId) ? P2 : P1;
        }

        /// <summary>
        /// 들여보낸다 → true (그 자리에서 보여 준다). 막히면 false — 창 안에서 온 P0/P1은 대기 칸에 넣고 queued = true
        /// (더 높은 우선순위가 이미 기다리면 넣지 않는다). 꺼짐 · 같은 스킬 10초 안 · P2는 기다리지 않는다
        /// </summary>
        public bool TryAdmit(string skillId, int priority, float now, out bool queued) => TryAdmit(skillId, priority, now, 0, out queued);

        /// <summary>TryAdmit + 부르는 쪽 번호 (대기 칸에서 꺼낼 때 그대로 돌려준다)</summary>
        public bool TryAdmit(string skillId, int priority, float now, int token, out bool queued)
        {
            queued = false;
            if (!Enabled || string.IsNullOrEmpty(skillId)) return false;
            if (RecentlyAdmitted(skillId, now)) return false;
            if (now - _lastAdmit >= Interval)
            {
                Admit(skillId, priority, now);
                return true;
            }
            if (priority >= P2) return false;
            if (_hasPending && _pending.Priority <= priority && now - _pending.At <= PendingLife) return false;
            _pending = new PendingCallout { SkillId = skillId, Priority = priority, At = now, Token = token };
            _hasPending = true;
            queued = true;
            return false;
        }

        /// <summary>
        /// 대기 칸의 요청을 꺼낸다: 창이 지났으면 들여보내고 true. 0.6초 넘게 기다렸거나 그사이 같은 스킬이 들어갔거나 꺼졌으면 버리고 false
        /// </summary>
        public bool TryTakePending(float now, out PendingCallout callout)
        {
            callout = default;
            if (!_hasPending) return false;
            if (!Enabled || now - _pending.At > PendingLife || RecentlyAdmitted(_pending.SkillId, now))
            {
                _hasPending = false;
                return false;
            }
            if (now - _lastAdmit < Interval) return false;
            callout = _pending;
            _hasPending = false;
            Admit(callout.SkillId, callout.Priority, now);
            return true;
        }

        /// <summary>큰 스킬 이름 알림이 떴다 — 창을 쓴다 (같은 스킬 10초 규칙에도 넣는다)</summary>
        public void NoteNotice(string skillId, float now)
        {
            _lastAdmit = now;
            if (!string.IsNullOrEmpty(skillId)) RememberSkill(skillId, now);
        }

        /// <summary>새 판 (스테이지 시작): 대기 칸 · 창을 비운다 (같은 스킬 · 처음 본 기억은 남긴다)</summary>
        public void Reset()
        {
            _hasPending = false;
            _pending = default;
            _lastAdmit = -999f;
        }

        /// <summary>
        /// 이 알림을 띄워도 되는지: 스킬 이름 알림 · 대사는 설정을 따르고, 기믹 안내 · 분노 알림 · 쓰다듬기 말은 늘 (스킬 이름이 아니다 — SPEC4 §3.4 #1)
        /// </summary>
        public static bool AllowsNotice(bool isSkillName, bool skillNamesOn) => !isSkillName || skillNamesOn;

        void Admit(string skillId, int priority, float now)
        {
            _lastAdmit = now;
            RememberSkill(skillId, now);
            if (priority == P1 && !Seen(skillId))
            {
                _seen[_seenCursor] = skillId;
                _seenCursor = (_seenCursor + 1) % SeenMemory;
                try
                {
                    MarkSeen?.Invoke(skillId);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                    MarkSeen = null;
                }
            }
        }

        bool Seen(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return true;
            for (int i = 0; i < SeenMemory; i++)
            {
                if (string.Equals(_seen[i], skillId, StringComparison.Ordinal)) return true;
            }
            var ask = HasSeen;
            if (ask == null) return false;
            try
            {
                return ask(skillId);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                HasSeen = null;
                return false;
            }
        }

        bool RecentlyAdmitted(string skillId, float now)
        {
            for (int i = 0; i < SkillMemory; i++)
            {
                if (now - _skillAt[i] < SameSkill && string.Equals(_skills[i], skillId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        void RememberSkill(string skillId, float now)
        {
            for (int i = 0; i < SkillMemory; i++)
            {
                if (!string.Equals(_skills[i], skillId, StringComparison.Ordinal)) continue;
                _skillAt[i] = now;
                return;
            }
            _skills[_skillCursor] = skillId;
            _skillAt[_skillCursor] = now;
            _skillCursor = (_skillCursor + 1) % SkillMemory;
        }
    }
}
