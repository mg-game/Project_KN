using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 연속 처치 · 한꺼번에 처치 알림 (FX-JUICE E11) + 마지막 일격 숫자 (§4.2). 코어 NoteKill이 수를 세고 OnKillNoted로 부른다.
    ///   연속: 1.2초 안에 이어진 처치 5 · 10 · 20 · 40, 그 뒤 20마다 ("5연속 수선!" … "400+연속!"). 한꺼번에: 0.25초 안에 3마리 이상 ("한꺼번에 N!") —
    ///   같은 창의 연속 알림을 대신하고, 처치들 가운데에 라벤더빛 크림 바닥 충격파(200 → 600px, 가산 0.5 · 세기 0.55) + 5마리 이상이면 줌 1.012.
    ///   모양: 왼쪽 위 스테이지 카드 바로 아래(가운데가 아니다)에 리본 판(plate_combo_9s, 9-slice, 10 미만 라벤더 / 10 이상 금빛) 위 36pt 굵은 글자
    ///   (크림 / 금빛 그라데이션)가 왼쪽 60px에서 미끄러져 들어오며 0.8 → 1.08 → 1.0 톡, 0.7초 머물고 0.2초에 사라진다. 판 뒤에 은은한 빛무리(가산 —
    ///   10 이상은 등불빛), 판 오른쪽 끝에 반짝 뭉치.
    ///   간격: 알림은 2초에 하나 (떠 있는 동안 더 큰 단계가 오면 그 자리에서 바꾸며 다시 톡 — 판이 넓어지면 자리도 다시 잰다), 보스 처치 뒤 2초 · 불 밝히기 뒤 1.3초 ·
    ///   스킬 알림/말풍선 뒤 1.2초는 쉰다.
    ///   불 밝히기 합계 (Wave 3): 숫자 8개 이상 뜬 빛 물결은 끝나고 수선점 지붕 위에 금빛 "+합계" 판을 0.9초 (같은 판 · 글자 칸, 연속 알림과 섞이지 않는다).
    /// 문구는 미리 만든 문자열 (매 프레임 할당 없음). 글자 칸은 숫자 풀의 비워 둔 2칸, 판·반짝은 숫자 레이어의 이미지 2벌.
    /// </summary>
    public partial class BattleFx
    {
        struct PendingCallout
        {
            public bool Active;
            public bool Multi;
            public int Count;
            /// <summary>이때(연출 시계) 띄운다 / 처음 잡힌 처치 꽃 시각</summary>
            public float FireAt;
            public float First;
        }

        const int CalloutLanes = 2;
        /// <summary>36pt (숫자 46pt 기준 배율)</summary>
        const float CalloutScale = 36f / NumberFontSize;
        const float CalloutLife = 1.02f;
        const float CalloutFadeAt = 0.82f;
        const float CalloutSlide = 60f;
        const float CalloutPlateHeight = 84f;
        /// <summary>알림 판과 HUD 칸 사이 틈 (CalloutAnchor)</summary>
        const float CalloutHudGap = 6f;
        /// <summary>알림 사이 최소 간격 (실제 시간) / 보스 처치 뒤 / 불 밝히기 뒤 / 스킬 알림·말풍선 뒤 쉬는 시간</summary>
        const float CalloutInterval = 2f;
        const float CalloutAfterBoss = 2f;
        const float CalloutAfterLamp = 1.3f;
        const float CalloutAfterSkill = 1.2f;
        /// <summary>막힌 알림은 이만큼 기다렸다가 버린다 (연출 시계)</summary>
        const float CalloutPendingLife = 0.6f;
        /// <summary>한꺼번에: 3마리부터 · 마지막 처치 뒤 이만큼 더 기다려 숫자를 모은다 (첫 처치 뒤 최대)</summary>
        const int MultiKillMin = 3;
        const float MultiSettle = 0.06f;
        const float MultiSettleMax = 0.2f;
        const float CalloutSparkLife = 0.3f;
        /// <summary>
        /// 한꺼번에 바닥 충격파 색 · 가산 · 세기: 라벤더 쪽 크림 (lerp(라벤더, 코어 크림, 0.45)) · 0.5 · 0.55 — 갈래 색으로 물든 처치 고리들 사이에서
        /// 600px 하얀 타원 하나가 튀지 않게 (예전 크림 · 0.6 · 0.7)
        /// </summary>
        static readonly Color MultiWaveTint = Color.Lerp(UITheme.Lavender, JuiceColors.CoreCream, 0.45f);
        const float MultiWaveAdd = 0.5f;
        const float MultiWaveIntensity = 0.55f;
        /// <summary>떠 있는 알림이 더 큰 단계로 바뀌며 판이 넓어져 자리를 옮길 때 미끄러지는 시간 (OutCubic)</summary>
        const float CalloutShiftTime = 0.12f;
        /// <summary>
        /// 불 밝히기 합계 알림 (ShowLampSum): 마지막 숫자가 닿고 이만큼 뒤 · 수명(마지막 0.2초에 사라짐) · 글자 · 판 배율(36pt × 1.2 ≈ 43pt) ·
        /// 수선점 지붕 위 틈 · 수명 동안 떠오르는 높이 · 처음 쾅 크기
        /// </summary>
        public const float LampSumDelay = 0.12f;
        public const float LampSumLife = 0.9f;
        const float LampSumFade = 0.2f;
        public const float LampSumScale = 1.2f;
        const float LampSumAbove = 18f;
        const float LampSumRise = 12f;
        const float LampSumSlam = 1.35f;

        static readonly string[] StreakSteps = BuildStreakSteps();
        static readonly string[] MultiTexts = BuildMultiTexts();

        PendingCallout _pendingCallout;
        /// <summary>떠 있는 알림 글자 칸 (없으면 −1) / 그 알림의 수 (더 큰 수로만 그 자리에서 바꾼다)</summary>
        int _calloutNumber = -1;
        int _calloutCount;
        int _calloutLaneCursor;
        float _lastCalloutReal = -99f;
        float _lastSkillNoticeReal = -99f;
        /// <summary>한꺼번에 무리: 창 끝(연출 시계) / 충격파·줌을 이미 냈는지</summary>
        float _multiClusterEnd = -99f;
        bool _multiWaveFired;
        bool _multiZoomFired;

        Image[] _calloutPlates;
        Image[] _calloutSparks;
        /// <summary>판 뒤 빛무리 (fx_glow_core 가산 — 10 이상은 등불빛으로 금빛 판을 밝힌다)</summary>
        Image[] _calloutAuras;
        bool[] _calloutGold;
        int[] _calloutLaneOwner;
        float[] _calloutPlateWidth;
        Color[] _calloutPlateTint;
        float[] _calloutSparkAt;
        /// <summary>이 판이 불 밝히기 합계 알림인지 (미끄러지지 않고 쾅 · 떠오름) / 자리를 옮긴 뒤 남은 미끄러짐 (옛 자리 − 새 자리, CalloutShiftTime에 0으로)</summary>
        bool[] _calloutSum;
        Vector2[] _calloutShift;

        // ───────────── 준비 ─────────────

        /// <summary>알림 판(9-slice, 그림 비율 그대로 높이 84px)과 반짝 뭉치 이미지 2벌 (숫자 레이어 — 글자 아래·위)</summary>
        void PrepareCallouts(Transform parent)
        {
            _calloutPlates = new Image[CalloutLanes];
            _calloutSparks = new Image[CalloutLanes];
            _calloutAuras = new Image[CalloutLanes];
            _calloutGold = new bool[CalloutLanes];
            _calloutLaneOwner = new int[CalloutLanes];
            _calloutPlateWidth = new float[CalloutLanes];
            _calloutPlateTint = new Color[CalloutLanes];
            _calloutSparkAt = new float[CalloutLanes];
            _calloutSum = new bool[CalloutLanes];
            _calloutShift = new Vector2[CalloutLanes];
            var plate = _juice != null ? _juice.ComboPlate : null;
            var spark = SparkleClusterSprite;
            var aura = GlowCoreSprite;
            for (int i = 0; i < CalloutLanes; i++)
            {
                _calloutLaneOwner[i] = -1;
                if (aura != null)
                {
                    var image = Instantiate(_particleTemplate, parent);
                    image.name = "CalloutAura";
                    image.raycastTarget = false;
                    image.enabled = false;
                    image.sprite = aura;
                    image.canvasRenderer.cullTransparentMesh = false;
                    _calloutAuras[i] = image;
                }
                if (plate != null)
                {
                    var image = Instantiate(_particleTemplate, parent);
                    image.name = "CalloutPlate";
                    image.raycastTarget = false;
                    image.enabled = false;
                    image.sprite = plate;
                    image.type = Image.Type.Sliced;
                    image.preserveAspect = false;
                    // 9-slice 테두리(150/40px)를 그림 비율 그대로 줄인다 (높이 160 → 84px)
                    float height = plate.rect.height;
                    image.pixelsPerUnitMultiplier = height > 1f ? height / CalloutPlateHeight : 1f;
                    _calloutPlates[i] = image;
                }
                if (spark != null)
                {
                    var image = Instantiate(_particleTemplate, parent);
                    image.name = "CalloutSpark";
                    image.raycastTarget = false;
                    image.enabled = false;
                    image.sprite = spark;
                    image.canvasRenderer.cullTransparentMesh = false;
                    _calloutSparks[i] = image;
                }
            }
        }

        // ───────────── 이음매 (코어 NoteKill → 여기) ─────────────

        /// <summary>
        /// 처치 하나가 세어진 뒤 (delay = 처치 꽃이 터지는 순간까지): 마지막 일격 숫자 +15% · 한꺼번에(3+, 그 무리가 연속 처치의 거의 전부)는 충격파·줌과 알림 예약 ·
        /// 연속 단계면 알림 예약 (떠 있는 알림은 그 자리에서 더 큰 단계로 바뀐다 — "한꺼번에 3!" → "한꺼번에 5!" → "10연속 반짝!").
        /// 보스 처치는 알림을 비운다 (보스 처치 연출 뒤 2초 쉰다)
        /// </summary>
        partial void OnKillNoted(int enemyId, Vector2 at, float delay, bool boss, HitTier byTier)
        {
            MarkKillingBlow(enemyId);
            if (boss)
            {
                _pendingCallout.Active = false;
                return;
            }
            float wait = Mathf.Max(0f, delay);
            float burst = _clock + wait;
            int multi = _multiKill;
            int streak = KillStreak;
            bool milestone = HitFeel.IsStreakMilestone(streak);
            // 한꺼번에(3+) — 이 처치가 연속 단계이기도 하면, 길게 이어진 연속 처치 중의 작은 무리일 때는 연속 단계가 이긴다 (10연속 > 한꺼번에 3)
            if (multi >= MultiKillMin && (!milestone || streak <= multi + 1))
            {
                MultiKillWave(multi, wait, burst);
                ref var p = ref _pendingCallout;
                // 처치는 꽃이 터지기 전에 세어지므로 기다리는 알림이 자주 겹친다 — 더 큰 수만 바꾼다 (기다리던 5연속을 한꺼번에 3이 지우지 않게)
                if (p.Active && !p.Multi && p.Count >= multi) return;
                if (!(p.Active && p.Multi))
                {
                    // 같은 창에 기다리던 연속 알림은 한꺼번에가 대신한다
                    p.Active = true;
                    p.Multi = true;
                    p.Count = 0;
                    p.First = burst;
                    p.FireAt = burst;
                }
                p.Count = Mathf.Max(p.Count, multi);
                p.FireAt = Mathf.Min(Mathf.Max(p.FireAt, burst + MultiSettle), p.First + MultiSettleMax);
                return;
            }
            if (!milestone) return;
            // 기다리던 알림이 같거나 더 크면 그대로 (아니면 더 큰 연속 단계가 대신한다)
            if (_pendingCallout.Active && _pendingCallout.Count >= streak) return;
            _pendingCallout = new PendingCallout { Active = true, Multi = false, Count = streak, First = burst, FireAt = burst + 0.03f };
        }

        /// <summary>
        /// 한꺼번에 처치 무리: 처음 3마리째에 가운데 바닥 충격파(200 → 600px 0.3초, 라벤더빛 크림 · 가산 0.5 · 세기 0.55 — MultiWaveTint),
        /// 5마리째에 줌 1.012 — 무리마다 한 번씩
        /// </summary>
        void MultiKillWave(int count, float wait, float burst)
        {
            if (burst > _multiClusterEnd)
            {
                _multiWaveFired = false;
                _multiZoomFired = false;
            }
            _multiClusterEnd = Mathf.Max(_multiClusterEnd, burst + HitFeel.MultiKillWindow);
            var centroid = _multiKillCentroid;
            float unit = UnitScale;
            if (!_multiWaveFired)
            {
                _multiWaveFired = true;
                Shockwave(centroid + Vector2.down * (34f * unit), MultiWaveTint, 200f * unit, 600f * unit, 0.3f, wait, MultiWaveAdd, MultiWaveIntensity, GroundAspect, true);
            }
            if (count >= 5 && !_multiZoomFired)
            {
                _multiZoomFired = true;
                ZoomPunch(HitFeel.ZoomMultiKill, centroid, wait);
            }
        }

        /// <summary>스킬 이름 알림이나 시전 말풍선이 떴다 (BattleView가 알린다 — 그 뒤 1.2초는 연속 알림을 쉰다)</summary>
        public void NoteSkillNotice()
        {
            if (!_ready) Prepare();
            _lastSkillNoticeReal = _realClock;
        }

        // ───────────── 예약 → 띄우기 ─────────────

        void TickCallouts()
        {
            TickLampSum();
            ref var p = ref _pendingCallout;
            if (!p.Active || _clock < p.FireAt) return;
            if (CalloutBlocked(out bool upgrade))
            {
                if (_clock > p.FireAt + CalloutPendingLife) p.Active = false;
                return;
            }
            p.Active = false;
            // 떠 있는 알림보다 작은 수로는 바꾸지 않는다 (10연속 → 한꺼번에 3 같은 거꾸로 가는 일 없이)
            if (upgrade && p.Count <= _calloutCount) return;
            bool gold = p.Count >= 10;
            string text = p.Multi ? MultiKillText(p.Count) : StreakText(p.Count);
            if (upgrade) UpgradeCallout(text, gold);
            else ShowCallout(text, gold);
            _calloutCount = p.Count;
            _lastCalloutReal = _realClock;
        }

        /// <summary>지금 알림을 띄울 수 없는지. upgrade = 2초 간격 안이지만 아직 떠 있는 알림을 그 자리에서 더 큰 단계로 바꿀 수 있다</summary>
        bool CalloutBlocked(out bool upgrade)
        {
            upgrade = false;
            if (SinceBossMoment < CalloutAfterBoss || SinceLamp < CalloutAfterLamp || _realClock - _lastSkillNoticeReal < CalloutAfterSkill) return true;
            if (_realClock - _lastCalloutReal >= CalloutInterval) return false;
            if (_calloutNumber >= 0 && _calloutNumber < _numbers.Length && _numbers[_calloutNumber].Alive && _numbers[_calloutNumber].Callout &&
                _numbers[_calloutNumber].Age < _numbers[_calloutNumber].FadeAt)
            {
                upgrade = true;
                return false;
            }
            return true;
        }

        /// <summary>알림 하나: 글자 칸(숫자 풀 몫) + 판 + 반짝. 자리 = CalloutAnchor (화면 (740, 300), 보이는 HUD 칸 밖)</summary>
        void ShowCallout(string text, bool gold)
        {
            int index = AcquireCalloutNumber();
            if (index < 0) return;
            var anchor = CalloutAnchor(text.Length);
            CalloutColors(gold, out var top, out var bottom);
            ShowNumber(index, anchor, text, top, bottom, DamageNumberKind.Normal, CalloutScale, 0f);
            ref var n = ref _numbers[index];
            n.Callout = true;
            n.Plain = true;
            n.Life = CalloutLife;
            n.FadeAt = CalloutFadeAt;
            n.End = CalloutLife;
            n.Rise = 0f;
            n.Shake = 0f;
            n.Lane = TakeCalloutLane(index);
            _calloutNumber = index;
            if (n.Lane >= 0) DressCalloutLane(n.Lane, text.Length, gold);
            // 그리는 순서: 빛무리 → 판 → 글자 → 반짝
            var aura = n.Lane >= 0 ? _calloutAuras[n.Lane] : null;
            if (aura != null) aura.rectTransform.SetAsLastSibling();
            var plate = n.Lane >= 0 ? _calloutPlates[n.Lane] : null;
            if (plate != null) plate.rectTransform.SetAsLastSibling();
            n.Rect.SetAsLastSibling();
            var spark = n.Lane >= 0 ? _calloutSparks[n.Lane] : null;
            if (spark != null) spark.rectTransform.SetAsLastSibling();
        }

        /// <summary>
        /// 연속 · 한꺼번에 알림 자리 (판 가운데, 전장 로컬): JuiceLayout.CalloutOffset = (−반폭 + 740, 안전선 − 50) — 화면 (740, 300), 두 상태 같은 자리.
        /// 그 자리의 판(폭 120 + 26 × 글자 수 · 높이 84)이 지금 보이는 HUD 칸(BattleFx.KeepOut — 시너지 줄 칩 · 바구니 · 추천 알약 · 배속/AUTO …)과
        /// 겹치면 옆 · 아래로 가장 조금 비킨다 (Wave 3 · ia_verify #3 '연속 수선!'이 시너지 줄 위에 앉았다 — 빌더 자리가 바뀌어도 실제 칸을 읽는다.
        /// 9~10글자 판이 바구니에 11~24px 닿던 것도 왼쪽으로 비킨다). SHEET에서 사라진 시너지 줄 · 추천 알약은 빠진다
        /// </summary>
        Vector2 CalloutAnchor(int chars)
        {
            var anchor = new Vector2(-FieldHalfWidth + JuiceLayout.CalloutOffset.x, SafeTop - JuiceLayout.CalloutOffset.y);
            var half = new Vector2((120f + 26f * chars) * 0.5f, CalloutPlateHeight * 0.5f);
            float limit = Mathf.Max(0f, FieldHalfWidth - half.x - 8f);
            int count = HudKeepOutCount;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < count; i++)
                    anchor = BattleKeepOut.PushOut(anchor, half, HudKeepOut(i), CalloutHudGap, BattleKeepOut.Down | BattleKeepOut.Side, limit, FieldBottom + half.y,
                        anchor.y);
            }
            return anchor;
        }

        /// <summary>
        /// 떠 있는 알림을 더 큰 단계로: 글자·색·판 폭을 바꾸고 다시 톡 + 반짝 (사라지기 전이면 조금 더 머문다).
        /// 판이 넓어지면('5연속 수선!' → '20연속 대수선!' 354px · '40연속 눈부셔요!' 380px) 자리를 CalloutAnchor로 다시 재 — 바구니 같은 HUD 칸에 닿지 않게 —
        /// 0.12초에 미끄러져 옮기고(움직임 줄이기면 바로), 넓어진 판에 가릴 숫자는 판 밑 · 옆으로 보낸다 (RaiseNumbersOverCallout)
        /// </summary>
        void UpgradeCallout(string text, bool gold)
        {
            ref var n = ref _numbers[_calloutNumber];
            n.Text.text = text;
            n.Chars = VisibleChars(text);
            CalloutColors(gold, out var top, out var bottom);
            n.Text.colorGradient = new VertexGradient(top, top, bottom, bottom);
            n.Bump = 1.18f;
            n.Repop = 1f;
            float stay = Mathf.Max(0.2f, n.Age);
            n.FadeAt = Mathf.Max(n.FadeAt, stay + 0.6f);
            n.End = n.FadeAt + (CalloutLife - CalloutFadeAt);
            if (n.Lane < 0) return;
            var residual = CalloutShiftNow(n.Lane);
            DressCalloutLane(n.Lane, text.Length, gold);
            var anchor = CalloutAnchor(text.Length);
            if ((anchor - n.Position).sqrMagnitude > 0.25f)
            {
                if (!UIMotion.Reduced) _calloutShift[n.Lane] = residual + n.Position - anchor;
                n.Position = anchor;
            }
            RaiseNumbersOverCallout(n.Lane);
        }

        /// <summary>자리를 옮긴 판의 남은 미끄러짐 (옛 자리 쪽으로, CalloutShiftTime 동안 OutCubic으로 0이 된다)</summary>
        Vector2 CalloutShiftNow(int lane)
        {
            var shift = _calloutShift[lane];
            if (shift.x == 0f && shift.y == 0f) return shift;
            float t = (_clock - _calloutSparkAt[lane]) / CalloutShiftTime;
            if (t >= 1f)
            {
                _calloutShift[lane] = Vector2.zero;
                return Vector2.zero;
            }
            return shift * (1f - JuiceEase.OutCubic(t));
        }

        // ───────────── 불 밝히기 합계 (Wave 3 · fx2 leftovers nice) ─────────────

        /// <summary>예약된 불 밝히기 합계 알림을 제 시각(연출 시계)에 띄운다 (BattleFx.Numbers ThinLampWall이 예약)</summary>
        void TickLampSum()
        {
            if (!_lampSumPending || _clock < _lampSumAt) return;
            _lampSumPending = false;
            ShowLampSum();
        }

        /// <summary>
        /// 불 밝히기 합계 알림 — 방치형 궁극기의 '한 번의 큰 합계': 숫자 8개 이상 뜬 빛 물결은 마지막 숫자가 닿고 0.12초 뒤 수선점 지붕 위에
        /// 금빛 판 + "+합계"(BattleEase.Plus — 물결마다 한 번, 캐시된 글자) 43pt를 0.9초 띄운다 (빛무리 등불빛 · 반짝 뭉치는 연속 알림과 같다).
        /// 미끄러지지 않고 1.35배에서 쾅 내려앉아 1.0, 12px 떠오르며 마지막 0.2초에 사라진다 (움직임 줄이기면 쾅 · 떠오름 없이).
        /// 알림 칸(숫자 풀 몫 · 판 2벌, 비어 있는 판 먼저)을 쓰지만 연속 알림과 섞이지 않는다 — 떠 있는 연속 알림으로 잡지 않으므로 더 큰 단계로 바뀌지 않고,
        /// 불 밝히기 뒤 1.3초는 연속 알림이 쉰다 (CalloutAfterLamp)
        /// </summary>
        /// <summary>
        /// 불 밝히기 '+합계' 판이 떴다 (인자 = 합계). 소리 쪽(AUD)이 짧은 차임을 붙일 수 있게 (WP-5B 요청, 선택). 받는 쪽 예외는 연출을 멈추지 않는다
        /// </summary>
        public static event System.Action<double> LampSumShown;

        void ShowLampSum()
        {
            if (!(_lampWallSum >= 0.5d)) return;
            int index = AcquireCalloutNumber();
            if (index < 0) return;
            try
            {
                LampSumShown?.Invoke(_lampWallSum);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
            string text = BattleEase.Plus(_lampWallSum);
            var anchor = LampSumAnchor(text.Length);
            CalloutColors(true, out var top, out var bottom);
            ShowNumber(index, anchor, text, top, bottom, DamageNumberKind.Normal, CalloutScale * LampSumScale, 0f);
            ref var n = ref _numbers[index];
            n.Callout = true;
            n.Plain = true;
            n.Life = LampSumLife;
            n.FadeAt = LampSumLife - LampSumFade;
            n.End = LampSumLife;
            n.Rise = 0f;
            n.Shake = 0f;
            n.Lane = TakeQuietCalloutLane(index);
            if (n.Lane >= 0)
            {
                DressCalloutLane(n.Lane, text.Length, true);
                _calloutPlateWidth[n.Lane] *= LampSumScale;
                _calloutSum[n.Lane] = true;
            }
            // 합계 판이 실제로 떴다: 짧은 차임 (AUD R1 — lamp_sum, 연출 쪽은 부르기만)
            AudioService.OnLampSumShown();
            // 그리는 순서: 빛무리 → 판 → 글자 → 반짝
            var aura = n.Lane >= 0 ? _calloutAuras[n.Lane] : null;
            if (aura != null) aura.rectTransform.SetAsLastSibling();
            var plate = n.Lane >= 0 ? _calloutPlates[n.Lane] : null;
            if (plate != null) plate.rectTransform.SetAsLastSibling();
            n.Rect.SetAsLastSibling();
            var spark = n.Lane >= 0 ? _calloutSparks[n.Lane] : null;
            if (spark != null) spark.rectTransform.SetAsLastSibling();
        }

        /// <summary>
        /// 불 밝히기 합계 판 자리 (판 가운데, 전장 로컬): 수선점 지붕 위 + 18px + 판 반높이 (MAIN 화면 y ≈ 660). 위는 안전선 − 8px − 반높이 아래
        /// (SHEET에서는 수선점이 위로 올라가 안전선 밑에 붙는다), 아래는 보이는 띠 아래 끝 위. 보이는 HUD 칸(옆 · 아래로) · 떠 있는 배너(위 · 아래로) ·
        /// 떠 있는 연속 알림 판(옆 · 아래로)을 가장 조금 비킨다
        /// </summary>
        Vector2 LampSumAnchor(int chars)
        {
            var half = new Vector2((120f + 26f * chars) * LampSumScale * 0.5f, CalloutPlateHeight * 0.5f);
            float limit = Mathf.Max(0f, FieldHalfWidth - half.x - 8f);
            float bottom = FieldBottom + half.y;
            float top = Mathf.Max(bottom, SafeTop - 8f - half.y);
            var anchor = _shop != null ? _shop.RoofPosition + Vector2.up * (LampSumAbove + half.y) : new Vector2(0f, (top + bottom) * 0.5f);
            anchor.x = Mathf.Clamp(anchor.x, -limit, limit);
            anchor.y = Mathf.Clamp(anchor.y, bottom, top);
            int count = HudKeepOutCount;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < count; i++)
                    anchor = BattleKeepOut.PushOut(anchor, half, HudKeepOut(i), CalloutHudGap, BattleKeepOut.Down | BattleKeepOut.Side, limit, bottom, top);
            }
            if (BannerKeepOut(out var banner)) anchor = BattleKeepOut.PushOut(anchor, half, banner, CalloutHudGap, BattleKeepOut.Up | BattleKeepOut.Down, limit, bottom, top);
            for (int lane = 0; lane < CalloutLanes; lane++)
            {
                if (!CalloutPlate(lane, out var center, out var plateHalf)) continue;
                anchor = BattleKeepOut.PushOut(anchor, half, new Rect(center - plateHalf, plateHalf * 2f), CalloutHudGap, BattleKeepOut.Down | BattleKeepOut.Side, limit,
                    bottom, top);
            }
            return anchor;
        }

        /// <summary>합계 알림 판: 비어 있는 판을 먼저 (떠 있는 연속 알림의 판을 빼앗지 않게), 없으면 차례대로 (TakeCalloutLane)</summary>
        int TakeQuietCalloutLane(int owner)
        {
            if (_calloutLaneOwner == null) return -1;
            for (int lane = 0; lane < CalloutLanes; lane++)
            {
                if (_calloutLaneOwner[lane] >= 0) continue;
                _calloutLaneOwner[lane] = owner;
                return lane;
            }
            return TakeCalloutLane(owner);
        }

        /// <summary>합계 알림 크기: 1.35배에서 80ms에 0.94로 쾅 내려앉아 100ms에 1.0 (움직임 줄이기면 1)</summary>
        static float LampSumPop(float age)
        {
            if (UIMotion.Reduced) return 1f;
            if (age < 0.08f) return Mathf.LerpUnclamped(LampSumSlam, 0.94f, JuiceEase.OutQuad(age / 0.08f));
            if (age < 0.18f) return Mathf.LerpUnclamped(0.94f, 1f, BattleEase.InOutSine((age - 0.08f) / 0.1f));
            return 1f;
        }

        static void CalloutColors(bool gold, out Color top, out Color bottom)
        {
            top = gold ? GradCritTop : GradCream;
            bottom = gold ? GradCritBottom : GradNormalBottom;
        }

        /// <summary>알림 글자 칸: 빈 칸 (없으면 가장 오래된 알림, 그것도 없으면 가장 낮은 순위 숫자)</summary>
        int AcquireCalloutNumber()
        {
            // 잠자기 모드: 연속 · 한꺼번에 · 불 밝히기 합계 알림도 끈다 (숫자와 같다)
            if (_sleepMode) return -1;
            int oldest = -1;
            int oldestSequence = int.MaxValue;
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref var n = ref _numbers[i];
                if (!n.Alive) return i;
                if (n.Callout && n.Sequence < oldestSequence)
                {
                    oldest = i;
                    oldestSequence = n.Sequence;
                }
            }
            if (oldest < 0) oldest = NumberVictim(int.MaxValue);
            if (oldest >= 0) HideNumber(oldest);
            return oldest;
        }

        int TakeCalloutLane(int owner)
        {
            if (_calloutLaneOwner == null) return -1;
            int lane = _calloutLaneCursor;
            _calloutLaneCursor = (_calloutLaneCursor + 1) % CalloutLanes;
            int previous = _calloutLaneOwner[lane];
            if (previous >= 0 && previous != owner && previous < _numbers.Length && _numbers[previous].Lane == lane)
            {
                _numbers[previous].Lane = -1;
                HideCalloutLane(lane, previous);
            }
            _calloutLaneOwner[lane] = owner;
            return lane;
        }

        /// <summary>판 폭 = 120 + 26 × 글자 수, 색 = 10 미만 라벤더 · 10 이상 금빛. 반짝은 지금부터 0.3초</summary>
        void DressCalloutLane(int lane, int chars, bool gold)
        {
            _calloutPlateWidth[lane] = 120f + 26f * chars;
            // 판 그림이 라벤더라 금빛은 곱하면 탁해진다 — 따뜻하게 0.7만 물들이고 뒤의 등불빛 빛무리로 금빛을 낸다
            _calloutPlateTint[lane] = gold ? Color.Lerp(Color.white, UITheme.Gold, 0.7f) : UITheme.Lavender;
            _calloutGold[lane] = gold;
            _calloutSparkAt[lane] = _clock;
            // 연속 · 한꺼번에 알림 모양 (합계 알림은 ShowLampSum이 다시 켠다) · 미끄러짐 없음 (UpgradeCallout이 옮길 때 다시 쓴다)
            _calloutSum[lane] = false;
            _calloutShift[lane] = Vector2.zero;
        }

        void HideCalloutLane(int lane, int owner)
        {
            if (_calloutLaneOwner == null || lane < 0 || lane >= CalloutLanes || _calloutLaneOwner[lane] != owner) return;
            _calloutLaneOwner[lane] = -1;
            if (_calloutPlates[lane] != null) _calloutPlates[lane].enabled = false;
            if (_calloutSparks[lane] != null) _calloutSparks[lane].enabled = false;
            if (_calloutAuras[lane] != null) _calloutAuras[lane].enabled = false;
            if (_calloutNumber == owner) _calloutNumber = -1;
        }

        /// <summary>
        /// 알림 그리기: 왼쪽 60px에서 120ms OutBack으로 미끄러져 들어오며 0.8 → 1.08(80ms) → 1.0(160ms) 톡, 60ms에 나타나 끝 0.2초에 사라진다.
        /// 판은 글자와 같이 움직이고, 반짝 뭉치는 판 오른쪽 끝에서 0.3초 (가산 0.8). 더 큰 단계로 바뀌며 자리를 옮겼으면 남은 미끄러짐을 더한다.
        /// 불 밝히기 합계 판은 미끄러지지 않고 첫 프레임부터 보이며 1.35배에서 쾅 (LampSumPop), 수명 동안 12px 떠오른다
        /// </summary>
        void DriveCallout(int index)
        {
            ref var n = ref _numbers[index];
            float age = n.Age;
            int lane = n.Lane;
            bool owned = lane >= 0 && _calloutLaneOwner[lane] == index;
            bool sum = owned && _calloutSum[lane];
            float slide = sum ? 0f : -CalloutSlide * (1f - JuiceEase.OutBack(Mathf.Clamp01(age / 0.12f), 1.4f));
            float pop = sum ? LampSumPop(age)
                : age < 0.08f ? Mathf.LerpUnclamped(0.8f, 1.08f, JuiceEase.OutQuad(age / 0.08f))
                : age < 0.16f ? Mathf.LerpUnclamped(1.08f, 1f, BattleEase.InOutSine((age - 0.08f) / 0.08f)) : 1f;
            if (n.Repop > 0f) pop *= 1f + (n.Bump - 1f) * RepopEnvelope(1f - n.Repop);
            float alpha = sum ? 1f : Mathf.Clamp01(age / 0.06f);
            if (age > n.FadeAt) alpha *= 1f - Mathf.SmoothStep(0f, 1f, (age - n.FadeAt) / Mathf.Max(0.01f, n.End - n.FadeAt));
            float lift = sum && !UIMotion.Reduced ? LampSumRise * JuiceEase.OutCubic(age / Mathf.Max(0.01f, n.Life)) : 0f;
            var position = n.Position + new Vector2(slide, lift);
            if (owned) position += CalloutShiftNow(lane);
            n.Rect.anchoredPosition = position;
            float scale = n.Scale * pop;
            n.Rect.localScale = new Vector3(scale, scale, 1f);
            // 묶음 알파로 (TMP alpha를 매 프레임 쓰면 글자 메시를 통째로 다시 짠다 — 숫자와 같은 길, 같은 값이면 쓰지 않는다)
            SetNumberAlpha(ref n, alpha);

            if (!owned) return;
            var plate = _calloutPlates[lane];
            float width = _calloutPlateWidth[lane];
            var aura = _calloutAuras[lane];
            if (aura != null)
            {
                bool gold = _calloutGold[lane];
                var rect = aura.rectTransform;
                rect.anchoredPosition = position;
                // 빛무리는 판보다 조금 크게, 처음 톡 할 때와 더 큰 단계로 바뀔 때 한 번 더 밝게
                float since = _clock - _calloutSparkAt[lane];
                float breathe = 1f + 0.5f * Mathf.Clamp01(1f - since / 0.3f);
                rect.sizeDelta = new Vector2(width * 1.3f * pop, CalloutPlateHeight * 2.2f * pop);
                if (!aura.enabled) aura.enabled = true;
                WriteColor(aura, gold ? UITheme.Lamp : UITheme.Lavender, (gold ? 0.5f : 0.22f) * breathe * alpha, 1f);
            }
            if (plate != null)
            {
                var rect = plate.rectTransform;
                rect.anchoredPosition = position;
                rect.sizeDelta = new Vector2(width, CalloutPlateHeight);
                rect.localScale = new Vector3(pop, pop, 1f);
                if (!plate.enabled) plate.enabled = true;
                WriteColor(plate, _calloutPlateTint[lane], alpha, 0f);
            }
            var spark = _calloutSparks[lane];
            if (spark == null) return;
            float t = (_clock - _calloutSparkAt[lane]) / CalloutSparkLife;
            if (t < 0f || t >= 1f)
            {
                if (spark.enabled) spark.enabled = false;
                return;
            }
            var sparkRect = spark.rectTransform;
            sparkRect.anchoredPosition = position + new Vector2((width * 0.5f - 22f) * pop, 24f * pop);
            float size = Mathf.Lerp(92f, 60f, t);
            sparkRect.sizeDelta = new Vector2(size, size);
            sparkRect.localRotation = Quaternion.Euler(0f, 0f, 40f * t);
            if (!spark.enabled) spark.enabled = true;
            WriteColor(spark, t < 0.5f ? UITheme.Lamp : UITheme.Cream, (1f - t * t) * 0.9f * alpha, 0.8f);
        }

        /// <summary>알림 예약·무리 기록·판을 모두 비운다 (ClearNumberStacks)</summary>
        void ClearCallouts()
        {
            _pendingCallout = default;
            _calloutNumber = -1;
            _calloutCount = 0;
            _lastCalloutReal = -99f;
            _lastSkillNoticeReal = -99f;
            _multiClusterEnd = -99f;
            _multiWaveFired = false;
            _multiZoomFired = false;
            // 불 밝히기 합계 예약 · 물결 기록도 (스테이지 재시작 뒤 지난 물결의 합계가 뜨지 않게)
            _lampSumPending = false;
            _lampWallFrom = -99f;
            _lampWallCount = 0;
            _lampWallSum = 0d;
            if (_calloutLaneOwner == null) return;
            for (int i = 0; i < CalloutLanes; i++)
            {
                _calloutLaneOwner[i] = -1;
                _calloutSum[i] = false;
                _calloutShift[i] = Vector2.zero;
                if (_calloutPlates[i] != null) _calloutPlates[i].enabled = false;
                if (_calloutSparks[i] != null) _calloutSparks[i].enabled = false;
                if (_calloutAuras[i] != null) _calloutAuras[i].enabled = false;
            }
        }

        // ───────────── 문구 (미리 만든다) ─────────────

        /// <summary>연속 처치 문구: 5 "5연속 수선!" · 10 "10연속 반짝!" · 20 "20연속 대수선!" · 40 "40연속 눈부셔요!" · 60~400 "{N}연속!" · 그 위 "400+연속!"</summary>
        public static string StreakText(int streak)
        {
            switch (streak)
            {
                case 5: return "5연속 수선!";
                case 10: return "10연속 반짝!";
                case 20: return "20연속 대수선!";
                case 40: return "40연속 눈부셔요!";
            }
            if (streak > 400) return "400+연속!";
            int step = Mathf.Clamp((streak - 60) / 20, 0, StreakSteps.Length - 1);
            return StreakSteps[step];
        }

        /// <summary>한꺼번에 문구: 3~20 "한꺼번에 N!", 그 위 "한꺼번에 20+!"</summary>
        public static string MultiKillText(int count)
        {
            if (count > 20) return "한꺼번에 20+!";
            return MultiTexts[Mathf.Clamp(count, MultiKillMin, 20) - MultiKillMin];
        }

        static string[] BuildStreakSteps()
        {
            var texts = new string[(400 - 60) / 20 + 1];
            for (int i = 0; i < texts.Length; i++) texts[i] = (60 + i * 20) + "연속!";
            return texts;
        }

        static string[] BuildMultiTexts()
        {
            var texts = new string[20 - MultiKillMin + 1];
            for (int i = 0; i < texts.Length; i++) texts[i] = "한꺼번에 " + (MultiKillMin + i) + "!";
            return texts;
        }
    }
}
