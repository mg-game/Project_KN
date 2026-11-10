using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 지원·힐 지킴이 맥박 연출 (ATK-10 / FX-JUICE E16). 이 지킴이들의 본업은 공격이 아니라 맥박이므로, 수선점이 가득 차 회복량이 0이어도 맥박은 보인다.
    ///   지원: 발판에서 빛 고리(가산 0.4)가 퍼지고, 계보 강조색 리본 2줄이 양옆 지킴이 머리 위로 높이 40px 호를 그리며 0.35초 날아가 닿으면
    ///         강화 왕관(vfx_buff_crown 56px)이 0.5초 + 받는 친구마다 반짝 하나(fx_sparkle 가산 0.5, 0.3초), 강화 반경 안 잔향이마다 작은 별 하나 + 반짝.
    ///   힐: 발판 둘레에서 민트 고리(가산 0.5)가 healEchoRadius까지 0.4초 동안 퍼지고 민트 방울 3개가 수선점 문으로 흘러 들어가
    ///       닿을 때마다 작은 물결 고리(46px, 가산 0.4)가 피고 작은 하트가 피어오른다.
    /// 순하게 — 주기 1초 이상(60BPM 아래), 코어 별·킥·번쩍임 없음, 가산 0.5 이하, 흔들림 없음.
    /// </summary>
    public partial class BattleFx
    {
        /// <summary>지원 리본이 옆 지킴이에게 닿기까지</summary>
        public const float RibbonTravel = 0.35f;
        const float RibbonArc = 40f;
        const float CrownSize = 56f;
        const float CrownLife = 0.5f;
        /// <summary>힐 고리가 끝까지 퍼지는 시간</summary>
        public const float HealRingTime = 0.4f;
        /// <summary>힐 방울이 수선점 문에 닿기까지 (가장 늦은 방울 기준)</summary>
        public const float HealDropTravel = 0.55f;
        /// <summary>맥박 빛의 가산 상한 (E16: 순하게)</summary>
        const float PulseAdd = 0.5f;

        /// <summary>지원 맥박의 시작: 발판에 강조색 빛 고리(가산 0.4) + 가슴에 순한 빛</summary>
        public void SupportPulse(Vector2 feet, Vector2 chest, Color accent)
        {
            if (!_ready) Prepare();
            float unit = UnitScale;
            SkillLightGlow(chest, Color.Lerp(accent, Color.white, 0.35f), 30f * unit, 120f * unit, 0.35f, 0f, 0.6f, PulseAdd);
            int ring = Spawn(_ring, BattleEase.WithAlpha(accent, 0.85f), feet, 30f * unit, 150f * unit, 0.45f, 0f, true);
            if (ring < 0) return;
            ref var p = ref _particles[ring];
            p.Aspect = GroundAspect;
            p.Rotation = 0f;
            p.FadeIn = 0.01f;
            p.Add = 0.4f;
        }

        /// <summary>지원 리본 2줄: from(가슴) → 옆 지킴이 머리 위로 호를 그리며 날아가고, 닿으면 강화 왕관이 톡 + 반짝 하나</summary>
        public void SupportRibbon(Vector2 from, Vector2 head, Color accent)
        {
            if (!_ready) Prepare();
            float unit = UnitScale;
            var to = head + Vector2.up * 10f * unit;
            var delta = to - from;
            var side = delta.sqrMagnitude > 1f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;
            for (int i = 0; i < 2; i++)
            {
                float lean = i == 0 ? 1f : -1f;
                var control = (from + to) * 0.5f + Vector2.up * RibbonArc * unit * (i == 0 ? 1f : 0.6f) + side * 14f * lean * unit;
                int ribbon = Curve(_trail, BattleEase.WithAlpha(i == 0 ? accent : Color.Lerp(accent, UITheme.Cream, 0.5f), 0.9f), from, control, to,
                    54f * unit, 34f * unit, RibbonTravel, i * 0.04f);
                if (ribbon < 0) continue;
                ref var p = ref _particles[ribbon];
                p.AlignToPath = true;
                p.Aspect = 0.26f;
                p.FadeIn = 0.05f;
                p.FadeOut = 0.85f;
                FaceTo(ribbon, from, control);
            }
            int spark = Curve(_sparkle, UITheme.Cream, from, (from + to) * 0.5f + Vector2.up * RibbonArc * unit, to, 20f * unit, 12f * unit, RibbonTravel, 0f);
            if (spark >= 0)
            {
                _particles[spark].Spin = 360f;
                _particles[spark].Add = PulseAdd;
            }

            Sprite crown = null;
            bool isArt = false;
            if (_library != null) _library.TryGetVfx(SkillVfx.BuffCrown, out crown, out isArt);
            if (crown == null) crown = Motif(VfxMotif.Crown);
            int pop = PopAt(crown, isArt ? Color.white : UITheme.Gold, to + Vector2.up * 16f * unit, CrownSize * unit, CrownLife, RibbonTravel);
            if (pop >= 0) _particles[pop].P1 = Vector2.up * 12f * unit;
            SkillLightGlow(to, UITheme.Lamp, 20f * unit, 90f * unit, 0.3f, RibbonTravel, 0.5f, PulseAdd);
            PulseGlint(to + new Vector2(18f, 30f) * unit, RibbonTravel + 0.04f);
        }

        /// <summary>지원 맥박이 닿은 잔향이: 머리 위에 작은 금빛 별 하나 + 반짝 하나</summary>
        public void SupportSpark(EchoView echo, float delay)
        {
            if (echo == null || !Allow(FxPriority.Sparkle)) return;
            int star = Spawn(Motif(VfxMotif.Star), UITheme.Gold, echo.HeadPoint, 10f, 22f * UnitScale, 0.45f, delay, sparkle: true);
            if (star < 0) return;
            ref var p = ref _particles[star];
            p.Curve = SizeCurve.Back;
            p.Rotation = 0f;
            p.Spin = 120f;
            p.FadeIn = 0.01f;
            p.FadeOut = 0.6f;
            p.P1 = Vector2.up * 10f;
            AnchorTo(star, echo);
            AnchorTo(PulseGlint(echo.HeadPoint + new Vector2(14f, 10f) * UnitScale, delay + 0.04f), echo);
        }

        /// <summary>받는 친구의 반짝 하나 (fx_sparkle, 가산 0.5, 0.3초 — 맥박이 닿았다는 표시)</summary>
        int PulseGlint(Vector2 at, float delay)
        {
            float unit = UnitScale;
            int index = Spawn(_sparkle, JuiceColors.CoreCream, at, 30f * unit, 10f * unit, 0.3f, delay, sparkle: true);
            if (index < 0) return index;
            ref var p = ref _particles[index];
            p.Add = PulseAdd;
            p.Curve = SizeCurve.Back;
            p.Rotation = 0f;
            p.Spin = 140f;
            p.FadeIn = 0.02f;
            p.FadeOut = 0.5f;
            return index;
        }

        /// <summary>
        /// 힐 맥박: 발판 둘레 민트 고리(바닥, 가산 0.5)가 radius(px)까지 0.4초 + 안쪽 민트 빛 웅덩이(가산 0.4) + 민트 방울 3개가 수선점 문으로 흘러 들어가
        /// 닿을 때마다 작은 물결 고리(46px, 가산 0.4). 방울이 문에 닿기까지 걸리는 시간을 돌려준다 (뒤따르는 수선점 회복 숫자를 그때 띄운다)
        /// </summary>
        public float HealPulse(Vector2 feet, Vector2 chest, float radius, Vector2 door)
        {
            if (!_ready) Prepare();
            float unit = UnitScale;
            float diameter = Mathf.Max(60f, radius * 2f);
            int ring = Spawn(_ring, BattleEase.WithAlpha(UITheme.Positive, 0.9f), feet, 16f, diameter, HealRingTime, 0f, true);
            if (ring >= 0)
            {
                ref var p = ref _particles[ring];
                p.Aspect = GroundAspect;
                p.Rotation = 0f;
                p.FadeIn = 0.01f;
                p.FadeOut = 0.6f;
                p.Add = PulseAdd;
            }
            // 고리 안 회복 빛 웅덩이 (빛 번짐 — 평평한 원판이 아니게)
            int fill = Spawn(GlowCoreSprite, BattleEase.WithAlpha(UITheme.Positive, 0.3f), feet, diameter * 0.3f, diameter * 0.95f, HealRingTime + 0.1f, 0f, true);
            if (fill >= 0)
            {
                _particles[fill].Aspect = GroundAspect;
                _particles[fill].FadeIn = 0.05f;
                _particles[fill].Rotation = 0f;
                _particles[fill].Add = 0.4f;
            }
            SkillLightGlow(chest, UITheme.Positive, 30f * unit, 110f * unit, 0.35f, 0f, 0.6f, PulseAdd);

            var drop = Motif(VfxMotif.Drop);
            const int drops = 3;
            float flight = HealDropTravel - (drops - 1) * 0.06f;
            for (int i = 0; i < drops; i++)
            {
                var start = chest + new Vector2(Random.Range(-14f, 14f), Random.Range(-4f, 10f)) * unit;
                var end = door + new Vector2(Random.Range(-12f, 12f), Random.Range(4f, 18f));
                var mid = (start + end) * 0.5f;
                var away = (start - end).normalized;
                var control = mid + new Vector2(-away.y, away.x) * Random.Range(-50f, 50f) + Vector2.up * Random.Range(40f, 80f);
                int index = Curve(drop, i == 1 ? HealPink : UITheme.Positive, start, control, end, 18f * unit, 12f * unit, flight, i * 0.06f);
                if (index < 0) continue;
                ref var p = ref _particles[index];
                p.Arrival = Arrival.ShopAbsorb;
                p.Rotation = 0f;
                p.FadeIn = 0.08f;
                // 방울이 문에 닿는 순간 작은 물결 고리 (46px, 가산 0.4)
                int splash = Spawn(_ring, BattleEase.WithAlpha(UITheme.Positive, 0.85f), end + Vector2.down * 6f, 8f, 46f, 0.3f, flight + i * 0.06f, sparkle: true);
                if (splash < 0) continue;
                ref var s = ref _particles[splash];
                s.Aspect = GroundAspect;
                s.Rotation = 0f;
                s.FadeIn = 0.01f;
                s.Add = 0.4f;
            }
            // 방울이 닿으면 문 위로 작은 하트 둘 (수선점이 가득 차 있어도 맥박이 닿았다는 표시)
            var heart = Motif(VfxMotif.Heart);
            for (int i = 0; i < 2; i++)
            {
                int index = Drift(heart, i == 0 ? HealPink : UITheme.Positive, door + new Vector2(i == 0 ? -22f : 20f, 30f) * unit,
                    new Vector2(Random.Range(-10f, 10f), Random.Range(50f, 70f)), Vector2.zero, 16f * unit, 10f, 0.75f, HealDropTravel + i * 0.08f);
                if (index < 0) continue;
                _particles[index].Spin = Random.Range(-20f, 20f);
                _particles[index].Rotation = 0f;
                _particles[index].Wobble = 6f;
                _particles[index].FadeOut = 0.55f;
            }
            return HealDropTravel;
        }
    }
}
