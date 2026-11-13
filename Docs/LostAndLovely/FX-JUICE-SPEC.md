# FX-JUICE-SPEC: LOST & LOVELY battle presentation v2 (화려하게, 타격감)

Version 1 · 2026-11-04 · FX director · target `D:/UnityProject/unity_idle` (Unity 6000.2.6f2, uGUI, 1080×1920 portrait, **gamma** colour space, `m_ActiveColorSpace: 0`).

**What the user asked for:**
- "연출효과를 좀더화려하게만들어줘. 우리게임에 어울리는연출로 하는데 좀더화려하게만들어줘야해. 타격감같은게밋밋해" (make the effects more spectacular, in a way that suits our game; the hit feel is flat).
- Earlier: "공격이펙트들 전부다 제대로 만들어줘" (make all the attack effects properly).
- On art: "크레딧충전했으니까마음껏써 품질최상으로 전부다" (credits are topped up, use them freely, top quality, everything).

**Inputs:**
- Baseline diagnosis and metrics in `scratchpad/fx_out/baseline/`: `peaks.tsv`, `strips_metrics.tsv`, `sheets/*`, `probe2/*`.
- The hit-feel technique catalog (T01–T22).
- The code, read on 2026-11-04 at about 00:10.
- The art guide (memory `lost-and-lovely-art-style.md`).
- SPEC4 part 2, §3 (battle presentation and audio, in flight).
- UX-SPEC (M27 true-bold font, WP-5 lists).

**How to read:**
- Implementers: §0 → §6 (your package, files and wave) → §3.6 (the API you code against) → your event rows in §1, §2 and §4 → §7 (proof).
- Artist: §5 only. The machine-readable list is `scratchpad/FX-JUICE-ART.jsonl`.

All `scratchpad/` paths mean `C:/Users/PC/AppData/Local/Temp/claude/d--UnityProject-unity-idle/5af81621-3ac1-471f-b619-0795eeedc9ed/scratchpad/`.

---

## 0. Direction

### 0.1 The look in one sentence
**Every hit lights a little lamp.**
- Each impact opens on frame 0 with a burst of warm light. That light is additive, set against a darkened pocket of night.
- The struck object flashes cream and wobbles like a soft toy.
- A defeated "forgotten thing" is released into confetti made of buttons, thread and stars, and a mote of memory flies home to the 수선점.

The spectacle comes from light, timing and contrast. Bigger, muddier sprites are not the answer. This also matches the game's theme of light pushing back forgotten darkness.

### 0.2 Rules for every recipe
1. **Peak on frame 0.**
   - Impact art opens at its biggest frame (×1.30–1.45), holds 2–3 frames, then shrinks and fades.
   - Nothing grows in after contact. This fixes diagnosis #3.
2. **Light, not haze.**
   - Cores, glows, rings, streaks and flares are additive on the new premultiplied material (§3.1).
   - Big flashes use a screen blend, so they brighten instead of laying a milky veil.
   - Dims use alpha. This fixes diagnosis #1.
3. **Contrast pocket.** A soft ink backing (#2D2458) on the ground layer behind each hit makes pastel art read on the lavender plaza.
4. **The body tells the story.** The struck enemy must stay visible:
   - the impact art is offset 12 px toward the attacker;
   - normal hits use the v2 open-centre art;
   - the flash holds for 2–3 frames;
   - squash springs back with one rebound;
   - knockback launches after hit-stop.

   This fixes diagnosis #4 and #5.
5. **Hierarchy follows importance.**
   - Numbers > crit/kill/skill/boss > normal hits > ambience.
   - Ally effects are bright pastel with cream cores. Enemy effects stay muted: no additive, no gold.
   - Red appears only for shop damage and danger.
6. **Rhythm and budget.**
   - At most one full impact per target per 90 ms, and at most 2 big bursts starting per 100 ms.
   - Busy moments switch to mini variants, and every layer is gated by quality.
   - 20 simultaneous hits must stay readable (§2.4).
7. **Cute, not violent.** Kills are releases (confetti, light, memory motes). No gore and no dark smoke on the ally side.
8. **Settings are law.**
   - ReduceFlash.
   - Shake 0/50/100. This is the project's reduce-motion; at 0 the camera never moves.
   - FX quality Low/Medium/High.
   - Damage numbers All/Crit only/Off.
   - Haptics.

   No PlayerPrefs reads per frame.

### 0.3 Targets, measured with the same probe and keys as the baseline
| Effect (probe key) | Baseline peak area | **Target peak area** | Peak frame | Hot-core px, baseline → target |
|---|---|---|---|---|
| Basic guardian hit (`guardian_*_t0`) | 0.8–1.2 % | **2.0–4.0 %** | p000 or p017 (was p050–p150) | 600–1,100 → **≥ 2,500** |
| Snipe (`guardian_chalkak_snipe_t0`) | 1.7 % | **3–5 %** | p000 | 618 → **≥ 3,500** |
| Echo hit (`echo_*`) | 0.55–0.72 % | **1.2–2.5 %** | p000 or p017 | → ≥ 1,000 |
| Crit (`crit_jjakjjak`) | 3.0 % | **5–9 %** | p000–p033 | → ≥ 6,000 |
| Shield break | 2.0 % | **3.5–6 %** | p000–p050 | → ≥ 2,500 |
| Normal kill (`death_enemy_*`) | 0.9–1.2 % | **4–8 %** | burst frame | → ≥ 4,000 |
| Boss kill (`death_boss_shadow`) | 4.2 % | **≥ 18 %** of the full field | burst frame | → ≥ 25,000 |
| Enemy strike on echo | 0.9 %, Δluma −18 | **≤ 1.5 %, Δluma ≤ +10** (stays muted) | — | ≤ 300 |
| Shop strike | 1.1 % | **1.5–3 %** | — | — |
| Skill bursts (star / candle) | 4.5–5.1 % | **7–12 %** | — | — |
| Lamp | 26.7 % | **≥ 45 %** | release frame | — |

**Clean-up:**
- For hits, the changed area at +600 ms must be ≤ 15 % of the peak, not counting decals and numbers.
- For kills, it must be ≤ 15 % at +900 ms.

**Camera:** the shake and kick peaks in §2.2 must appear in the gallery metrics log.

**Numbers:**
- A normal "128" glyph is ≥ 36 px tall (baseline 31 px).
- A crit settles at ≥ 58 px and peaks at ≥ 90 px during the slam.

### 0.4 Precedence
- Where this spec gives a value it wins over IDLE_RESEARCH FX-01/02/03, ATK-06 and the T01–T22 catalog.
- **SPEC4 §3.1 "T3 skill numbers"** is delivered here with the same API (`DamageNumberKind.Skill`, `NumberScale(Skill) = 1.2`, lineage-accent tint). SPEC4 P4-3b only verifies it.
- **SPEC4 §3.5 audio is not touched.** This spec keeps impact moments exact (arrival times are unchanged), so `BattleSfx.Schedule(cue, delay)` can later hook the same delays.

### 0.5 Out of scope
- Audio (SPEC4 §3.5).
- Any simulator or balance change.
- Enemy "hurt face" art (9 new enemy sprites; later).
- Shop cracks (SPEC4 §3.3).
- Sprite atlases (SPEC4 §3.3). §3.7 lists the new folders the atlas builder should include or exclude.

---

## 1. Event recipes

### 1.0 Notation and shared values
**Sizes**
- **px** is reference px on the 1080-wide canvas × **U**, where U = `BattleFieldMapper.ActorScale` (clamped 0.75–1.3). The exception is anything marked *screen*.

**Time**
- **t** is in ms on the **FX clock**, counted from the anchor. The anchors are:
  - impact: projectile arrival, or the swing peak;
  - kill: the end of KillHold;
  - boss kill: the burst moment.
- The FX clock (`_clock`, scaled by `TimeScale`) freezes during global stops.
- Screen-level effects (flash, dim, kick, zoom, impact frame) run on **real time** (`_realClock`).
- **×P:** hit-level lifetimes are multiplied by PopScale (1.0 at 1×, 0.8 at 2×). Screen-level effects and numbers are not.

**Layers (Lyr)**
- **G** = GroundFx, under the actors.
- **F** = Fx, over the actors.
- **N** = Numbers.
- **D** = FieldDim (new, §3.2).
- **FF** = FieldFlash.
- **SF** = ScreenFlash, app-wide.
- **S** = silhouette pool, on top of Fx while active.

**Additive weight (Add)**
- On the premultiplied material, 0 = normal alpha blend and 1 = pure light.
- When Add > 0, the "α" column means intensity.

**Order:** table rows are listed in **spawn order = draw order**, with later rows on top (§3.2).

**Curves**
- `Impact(first, hold, settle, end)` is defined in §3.3.
- OutQuad, InQuad, OutCubic, OutQuart, OutExpo, InBack and InOutCubic come from `JuiceEase` (§3.6).

**Gates**
- H/M/L = FX quality High/Medium/Low.
- *dens* = density governor (§2.4).
- *halo* = halo cap.
- *P-tier* = which particle priority tier is dropped first (§3.4).

**Colours**

| Name | Hex |
|---|---|
| CoreCream | #FFF8E8 |
| Ink | #2D2458 |
| Dim | #1E1640 |
| Cream | #FFF4DC |
| Lamp | #FFD98A |
| Gold | #F2C46B |
| Lavender | #C4B2F2 |
| DustyPink | #E9A6C6 |
| SoftBlue | #93C8F0 |
| Danger | #D9566E |
| Mint | #8FE3B0 |
| EchoGlow | #C9A6FF |

Lineage Main, Sub and Accent are in §1.21.

**New art:** names like `atk_{L}_crit` and `fx_streak` refer to §5. Every new sprite has a fallback. Code must never require new art.

### 1.1 Master ladder (summary; exact numbers in §2.1)
| | Normal | Heavy | Crit | Skill hit | Kill | Boss hit | Boss kill | Lamp (U) | Stage clear |
|---|---|---|---|---|---|---|---|---|---|
| Settled size (px) | HitSize×1.2 (120–144) | ×style (snipe 1.25, assault 1.1) | 200 | 170 | 200 → swells to 260 | ×1.2 | 420 hero | 110 per enemy | 560 hero |
| Frame-0 size, hold | ×1.30, 33 ms | ×1.35, 33 ms | ×1.45, 50 ms | ×1.35, 33 ms | ×1.35, 33 ms | ×1.25, 33 ms | ×1.30, 50 ms + global stop | ×1.30, 33 ms | pop 0.8 s |
| Additive core (× size / ms) | 0.50 / 50 | 0.60 / 66 | 0.75 / 83 + flare | 0.60 / 66 | 0.80 / 100 + halo | 0.55 / 50 | 1.0 / 150 + flare | 0.6 / 66 (300 px at the shop) | — |
| Ink backing α | 0.24 | 0.32 | 0.40 | 0.34 | 0.45 | 0.30 | 0.50 | none (field dim) | none |
| Rings | 1 thin (H) | 1 | 2 (cream, then gold at +50 ms) | 1 + ground | 1 + ground | 1 | 3 + ground | wave + inner | 1 wide |
| Streaks / shards | 4 (100° cone) | 6 (70°) | 8 (360°) + 4 stars | 6 + 3 motifs | 10 confetti + 4 twinkles + memory mote | 6 | 24 confetti + 6 ribbons + 18 sparkles | 40 star/petal rain | 30 confetti + 12 sparkles |
| Body flash α / hold / total ms | 0.85 / 33 / 70 | 0.95 / 33 / 80 | 1.0 / 50 / 100 + blush | 0.9 / 33 / 80 | 1.0 held through KillHold | 0.7 / 33 / 70 | 1.0 + cream silhouette | 0.85 / 33 / 80 | — |
| Squash peak X/Y (spring) | 1.16 / 0.86 | 1.20 / 0.82 | 1.26 / 0.78 | 1.20 / 0.82 | builds to 1.30 / 0.72, then pop | 1.06 / 0.95 | 1.35 / 0.70 | 1.12 / 0.90 | — |
| Knockback px (after freeze) | 8 | 12 | 16 | 12 | — | 3 | — | 10, outward | — |
| Target freeze / shiver | — | 33 ms / ±3 | 66 ms / ±5 decaying | 50 ms / ±3 (first and last tick) | KillHold ±4 | heavy/crit only, 33 ms / ±2 | global 120 ms + 0.3× for 180 ms | ignite 0.2× for 120 ms (exists) | — |
| Camera | — | snipe kick 2 px | kick 3 px | area trauma 0.40, single-target kick 2 px | crit/skill kill kick 2 px | — | trauma 0.80, zoom 1.03, impact frame | trauma 0.85, zoom 1.035 | zoom 1.015 |
| Number pt | 46 | 53 | 74 slam ★ | 55 tinted | killing blow +15 % | 55 | ribbon (exists) | 51 | ribbon (exists) |
| Haptic | — | — | — | — | — | — | Heavy | Medium | — |

### E1. Guardian launch: anticipation, release, recoil and muzzle [ally]
**Windup.** The windup is W = clamp(0.3 × flight, 0.02, 0.05) s at 1×. At 2× it is min(that, 0.03).
- The release, the muzzle and the projectile spawn are all delayed by W.
- The projectile flies for flight − W.
- `GuardianShot` still **returns `flight`**. The arrival time is unchanged, so ATK-09 kill-hold, numbers and the white flash are untouched.

**Body (GuardianSlotView)** — t is relative to the release:
- **t ∈ [−W, 0]:** squash down to (1.10, 0.88) and lean −4° (away from the target), OutQuad. The halo is +0.2.
- **t = 0:** snap to a stretch of (0.92, 1.12) and lean +7° toward the target.
- **Recoil:** 6 px opposite the shot direction. It goes out in 40 ms (OutExpo) and comes back in 140 ms (OutCubic).
- **Settle:** scale returns to 1 on a damped spring (5 Hz, ζ 0.5) by about 220 ms.
- The pad glow stays at +0.45 × attack (exists).

**Muzzle**
- M = MuzzlePosition + shotDir × 18 px. t = 0 is the release.
- In the table, ctrl = the control point (`P1`) of the projectile's Shot motion.

| # | Layer | Sprite → fallback | Lyr | Size | t | Curve | Add (α) | Colour | n | Gate |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | core pop | `fx_glow_core` → glow_soft | F | 24 → 40 | 0–50 | OutQuad | 1 (0.9) | white | 1 | all |
| 2 | flare | `muzzle_{L}` → `fx_core_star` tinted Accent | F | 40 → 96, rotated to the shot angle | 0–120 | OutCubic; fade from 40 %, InQuad | 0.85 (1.0) | white (art) / Accent | 1 | all |
| 3 | sparks | lineage spark (§1.21) on `fx_streak` | F | length 12–16, thickness 3 | 0–160 | Streak at 260–380 px/s, ±25° around shotDir, drag 6/s | 0.6 (0.9) | lineage spark colour | 2 | H/M |

Snipe guardians scale the muzzle ×1.3. There is no recoil kick on launch: the snipe kick happens on impact (E4).

### E2. Projectile flight [ally]
| # | Layer | Sprite → fallback | Lyr | Size | Motion | Add (α) | Colour | n | Gate |
|---|---|---|---|---|---|---|---|---|---|
| 1 | glow under | `fx_glow_core` → glow_soft | F | 0.7 × head | Shot (same path) | 1 (0.45; +0.25 in Blackout) | Accent (echo: EchoGlow) | 1 | Trail tier |
| 2 | trail | `atk_snipe_trail` (snipe, chalkak) | F | as today | as today | **0.7** (was alpha) (0.95) | white | 1 | Trail tier |
| 3 | afterimage 2 | the head sprite | F | 0.84 × head | Shot; delay +60 ms; same life; fade from 60 % | 0.5 (0.22) | white | 1 | H |
| 4 | afterimage 1 | the head sprite | F | 0.92 × head | Shot; delay +30 ms; fade from 60 % | 0.5 (0.45) | white | 1 | H/M |
| 5 | head (spawned last of the flying layers, so it draws on top) | `atk_{L}_shot` (kept) | F | 104 × form(1.1) × ShotStretch (snipe 1.3) × in-flight stretch X 1.2 / Y 0.85 | Shot; **progress warp p = k·(0.75 + 0.25k)** (snaps into the target); tip pivot 0.97; aligned to path | 0 (1) | white | 1 | Core |
| 6 | shed motes | lineage shard (§1.21) | F | 10–14 | spawned at k = 0.35 and 0.7 at the predicted path point; drift 30 px backwards; gravity 60; life 300 ms | 0.4 (0.8) | lineage shard colour | H 2 / M 1 | Sparkle tier |

- Echo ranged shots: the head is 0.6 × 104 and life stays 0.14 s. Only one afterimage, on H.
- The Rain-stage droplet stays as it is.

### E3. Normal hit [ally art · enemy reaction · show number]
**Size and position**
- S = HitSize × **1.2** × style.HitScale × (form 2: ×1.1) × (boss: ×1.2) × (shielded: ×0.7) × (echo: ×0.65 / token: ×0.45) × U.
  - This gives jjakjjak 132, ttokdan 120, chalkak 144, mongdang 132, udukeoni 132, rouge 138, lalabell 144.
  - The 1.2 is a const `ImpactScale` in `BattleFx.Attack.cs`. The `AttackFxCatalog.HitSize` table stays as it is.
- P = HitPoint + jitter (±10, ±8)·U − shotDir × 12·U (toward the attacker), then clamped to the field (ATK-16).

| # | Layer | Sprite → fallback | Lyr | Size | t | Curve / motion | Add (α) | Colour | n | Gate |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | ink backing | circle_soft | G (at P) | 0.9 S | 0–160 ×P | constant size; α fades from 30 % (InQuad) | 0 (0.24) | Ink | 1 | H/M, not mini |
| 2 | impact art | **`atk_{L}_impact`** → `atk_{L}_hit` | F | S | 0–240 ×P | **Impact(1.30, 33, 70, 0.92)**; rotation ±15° (0 for upright lineages), twist −8° over life; α 1 until 45 %, then InQuad to 0 | 0 (1) | white | 1 | all |
| 3 | core star | `fx_core_star` → sparkle | F | 0.50 S → 0.35 S | 0–50 ×P | full until 33 ms, then InQuad fade | 1 (0.9; ReduceFlash ×0.6) | lerp(Accent, CoreCream, 0.7) | 1 | all |
| 4 | ring | `fx_shockwave` (jjakjjak/ttokdan: fx_ring_thread) → ring_glow | F | 0.4 S → 1.1 S | 16–176 ×P | OutQuart; α 0.6 → 0 from 30 % | 0.85 (0.6) | lerp(Accent, Cream, 0.4) | 1 | H only, dens ≤ 8 |
| 5 | streaks | `fx_streak` → trail | F | length 18–26 × (1 + v/600), thickness 4–6 | 0–(140…180) ×P | Streak at 450–700 px/s, 100° cone on the exit side (shotDir ± 50°), drag 10/s | 1 (0.9) | lerp(Accent, CoreCream, 0.5) | H 4 / M 3 / L 2 | Sparkle tier keeps ≥ 2 |
| 6 | lineage extra | existing `LineageExtra` (stitches, buttons, wax, ripple, hearts, sound ring, lock ring) | F/G | as today | as today | as today | sound/lock rings 0.6; others 0 | as today | as today | Extra tier |
| 7 | sparkles | fx_sparkle | F | 14–22 | 0–(300…420) | Drift 60–110 px/s, upward bias, gravity 30 | 0.7 (1) | Accent / Cream alternating | 2 | Sparkle tier, H/M |

- **Reaction** (enemy, E9 Normal row):
  - flash 0.85, held 33 ms, total 70 ms;
  - spring squash 1.16 / 0.86;
  - knockback 8 px (40 ms out, 120 ms back);
  - no freeze.
- **Number** (show, §4): Normal, 46 pt, spawns at t = +33.
- **Mini variant** (used when `TryFullImpact` is false or the density is > 8):
  - row 2 at 0.65 S with Impact(1.20, 17, 50, 0.92), life 180 ×P;
  - row 3 at 0.35 S, 33 ms;
  - row 5 with 2 streaks;
  - row 6 on every second mini hit only;
  - all other rows off.
- **Particle counts:**
  - H: 11–16 upper particles + 1 ground.
  - M: 9–13.
  - L: 6–9.
  - Mini: 4–5.

### E4. Heavy hit: snipe, assault and shield-break blow [ally · enemy · show]
Same as E3, with these changes:
- Row 1: α 0.32, 0.95 S, 200 ms.
- Row 2: Impact(1.35, 33, 70, 0.92), life 270.
- Row 3: 0.60 S, 66 ms.
- Row 4: on every quality except L; 0.4 → 1.3 S over 180 ms; α 0.8.
- Row 5: 6 streaks, 70° cone, 700–1000 px/s.
- Row 7: 3 sparkles.

**Assault adds a slash.**
- Sprite: `fx_slash_arc` → atk_slash, on layer F.
- Size 1.05 S; rotation facing ± 20°, sweeping +40° over 90 ms (Linear), then fading from 50 %.
- Add 0.85; colour lerp(Main, white, 0.25).

**Snipe adds a pierce flare.**
- Sprite: `fx_flare_cross`, 1.6 S × 0.25 S along shotDir, 0–70 ms.
- Add 1, intensity 0.9, CoreCream.
- `Kick(shotDir, 2 px, Snipe)` at t = 0, at most once per 0.6 s.

**Reaction:**
- flash 0.95, held 33 ms, total 80 ms;
- squash 1.20 / 0.82;
- **freeze 33 ms** with a decaying ±3 px shiver;
- knockback 12 px, launched when the freeze ends.

**Number:** Heavy, 53 pt.

### E5. Crit [ally art · enemy reaction · show number and kick]
Size: S_c = 200 × U, ×1.4 for bosses.
- **Basic attacks:** the same-frame `PromoteImpact` swaps the already-scheduled impact sprite to `atk_{L}_crit`, sets its size to S_c and its curve to the crit curve. It also cancels rows 4–5 of that impact (the crit adds its own).
- **Skill crits:** `CritImpact` spawns everything below, with the generic gold crit (no lineage special).

| # | Layer | Sprite → fallback | Lyr | Size | t | Curve | Add (α) | Colour | n | Gate |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | ink backing | circle_soft | G | 1.0 S_c | 0–200 | InQuad fade from 30 % | 0 (0.40) | Ink | 1 | H/M |
| 2 | halo | `fx_glow_core` | F | 2.2 S_c | 0–200 | size 0.8 → 1.0 (OutQuad); InQuad fade | 1 (0.3) | Gold | 1 | halo |
| 3 | crit art | **`atk_{L}_crit`** → `atk_{L}_hit` ×1.4 + vfx_star_burst (today's CritImpact) | F | S_c | 0–330 | **Impact(1.45, 50, 80, 1.0)**, then a bounce 0.97 → 1.0 at 130–170 ms; fade from 55 % | 0 (1) | white | 1 | all |
| 4 | core star | `fx_core_star` | F | 0.75 → 0.5 S_c | 0–83 | full until 50 ms | 1 (1.0; ReduceFlash ×0.6) | CoreCream | 1 | all |
| 5 | flare | `fx_flare_cross` | F | 2.2 S_c × 0.18 S_c, rotation ±15° | 0–100 | size 0.8 → 1.1 | 1 (0.8) | lerp(Gold, CoreCream, 0.5) | 1 | H/M |
| 6 | ring 1 | `fx_shockwave` | F | 0.3 → 1.5 S_c | 0–220 | OutQuart | 0.9 (0.8) | CoreCream | 1 | all |
| 7 | ring 2 | `fx_star_ring` → fx_shockwave | F | 0.3 → 1.5 S_c | 50–270 | OutQuart | 0.9 (0.8) | Gold | 1 | H/M |
| 8 | speedlines | `fx_speedlines` | F | 2.6 S_c, random rotation | 0–120 | size 0.9 → 1.05 | 1 (0.5) | CoreCream | 1 | H, halo |
| 9 | streaks | `fx_streak` | F | length 22–30, thickness 5–7 | 0–200 | Streak 360° at 600–1000 px/s, drag 8 | 1 (0.9) | lerp(Accent, CoreCream, 0.5) | H 8 / M 6 / L 4 | |
| 10 | stars | motif_star | F | 20–28 | 0–(420…550) | Drift 70–110 px/s, gravity −60 (floats up), spin ±360°/s | 0.5 (1) | Gold / Cream | 4 | Sparkle tier |
| 11 | lineage special | §1.21 | | | | | | | | H/M; L: art only |
| 12 | decal | `decal_{L}` (§1.21) | G | 24–30 | 0–2500 | fade over the last 1 s | 0 (0.45) | white | 1 | H (decal cap) |

**Camera:** `Kick(hitDir, 3 px, Crit)` at t = 0, at most once per 0.5 s. A boss crit kicks 2 px. **No trauma.**

**Reaction:**
- flash 1.0, held 50 ms, total 100 ms;
- then a **blush** in the attacker's Accent at α 0.35 for 60 ms (off with ReduceFlash);
- squash 1.26 / 0.78 with a rebound to about 0.95 / 1.05;
- **freeze 66 ms** (chalkak ×1.2 = 80 ms) with a ±5 px shiver that decays to 0;
- knockback 16 px, launched at t = freeze end.

**Number:** the crit slam (§4).

**Celebration** (ally): the guardian that landed the crit hops (E8b).

**Misprint, P2** (only when the crit kills): see §2.3.

### E6. Multi-hit and per-target rhythm [ally · show · enemy]
- **First hit full, then mini.** On the same target, the first hit gets the full recipe. Hits landing < 90 ms after that target's last full impact get the **mini** variant (`TryFullImpact`).
- **Rhythm accent.**
  - A chain is a run of hits on the same target with gaps ≤ 0.6 s.
  - Every **3rd** hit in a chain is upgraded to a full impact with the core ×1.15 and a ring (on M as well).
  - That hit's number gets a bump of 1.2.
- **Number merge.**
  - Numbers on the same target merge within 0.12 s, or 0.25 s when > 8 numbers are alive (§4).
  - Each merged hit re-pops with a bump of 1.1 + 0.03 × min(chain, 5), capped at 1.25.
- **Body flash** only re-triggers when the new strength ≥ the current alpha (exists).
- **Freeze budget:** ≤ 0.15 s of freeze per 0.5 s per target (§2.1).
- **Multi-tick skills** (chain, rain, orbit, ground ticks) freeze on the **first and last** tick only. Pass `allowFreeze` to `QueueImpact` (§3.6).

### E7. Skill hits by type [show; the base impact is core]
- The per-target skill hit goes through the core seam `SkillHit(in SkillHitFx)`.
- Ally's `OnEnemyHit` uses it in its InCast branch instead of `_fx.Hit(...)`.
- Core draws the **base** impact, then calls the show's `OnSkillHit` for style flavour.
- S_k = 170 × U (boss ×1.2).

**Base impact (core).** Single-target styles (Projectile, Beam) get the full version; area styles (Burst, Ring, Rain, Ground, Orbit) get the mini version.

| # | Layer | Sprite | Lyr | Size | t | Curve | Add (α) | Colour | n | Full / mini |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | ink backing | circle_soft | G | 1.0 S_k | 0–180 | — | 0 (0.34) | Ink | 1 | full, H/M |
| 2 | core star | fx_core_star | F | 0.6 S_k (mini 0.4) | 0–66 (mini 40) | full until 33 ms | 1 (0.9) | lerp(Main, CoreCream, 0.6) | 1 | both |
| 3 | ring | fx_shockwave | F | 0.4 → 1.2 S_k | 0–200 | OutQuart | 0.8 (0.7) | Main | 1 | full |
| 4 | streaks | fx_streak | F | length 20–28 | 0–180 | 70° cone along the cast direction, 600–900 px/s | 1 (0.9) | lerp(Main, Cream, 0.4) | full 6 / mini 2 | |

**Reaction** — Skill tier:
- flash 0.9, held 33 ms, total 80 ms;
- squash 1.20 / 0.82;
- freeze 50 ms on the first and last tick only;
- knockback 12 px.

Ticks of Ground styles use the Tick tier instead.

**Number:** `DamageNumberKind.Skill`, 55 pt.
- Tint: lerp(caster lineage Accent, Cream, 0.25). Use `AttackFxCatalog.Look(casterLineage).Accent`, or `SkillLook.Accent` when the caster is unknown.
- Merge window 0.25 s.

**Per-style flavour** (show, in `OnSkillHit` and the cast functions in `BattleFx.Skills.cs`):

| Style (keys) | Cast-side change | Per-target flavour | Screen |
|---|---|---|---|
| Projectile (thread_stitch, key_beam…) | Head gets a glow under it (Add 1, 0.45). `SkillImpactAt` → Impact(1.35, 33, 70, 1.0) + core 0.6× + ring. | 3 shards (`look.Particle` motif, 12–18 px) on Bounce at 900 g | single target: `Kick(castDir, 2, Skill)` |
| Beam (lantern_beam) | Inner beam on `_soft`, Add 1 (0.9). Outer glow Add 0.6. Along-beam pops on the Impact curve. End: core 0.6× + ring. | 2 streaks perpendicular to the beam, in both directions | `Kick(beamDir, 2, Skill)` |
| Burst (candle, kiss, star, dust, shadow) | **Frame-0 area flash:** fx_glow_core, diameter 1.6 × radius, Add 1 (0.6), 66 ms. Dark keys (shadow, dust): Add 0.3 plus ink backing α 0.35 at 1.2 × radius. `PopArt` → Impact(1.35, 33, 80, 1.08) over 720 ms. Ground ring (GroundAspect, at the radius, 250 ms, Add 0.7). | mini impacts **staggered 25 ms per 100 px** from the centre (`CastDelay`) | area: `AddSkillTrauma(0.40)` (0.5/s channel). Spotlight if big (below). |
| Ring (sound_ring, storm_swirl, reveal_light) | Ring art Add 0.5. Rings 2 and 3 get an inner edge at Add 0.8. | mini impact at the moment the ring's radius reaches the target (radius(t) = R·OutQuad(t/0.62)) | area trauma 0.40 |
| Ground (wax_drip, slow_yarn, summon_circle, moon_glow) | Ground glow pulses Add 0.35 (+0.15 every 0.5 s) | Tick tier: one 24 px ember (Add 0.6), flash 0.35, no impact art | — |
| Dome / Rise / OverHead (ally or status) | Pop on Impact(1.30, 33, 70, 1.0); Add 0.4 glint; domes get a gloss sweep (`vfx_gloss_shine`, Add 0.6, 160 ms) | — | — |
| Orbit (spin_button) | — | a mini impact each time an orbiter passes a target (rate-limited by `TryFullImpact`) | — |
| Rain (raindrops, stars) | Each drop: `Arrival.Splash`, with its ring at Add 0.5 | mini impact on the first drop per target | area trauma 0.40 |

**Skill spotlight** (show).
- Triggers when the caster lineage grade ≥ Legendary (`BattleUnitArt.Lineage(...).grade`) or `IsBigSkill(skill)`.
- At most once per 2 s.
- `FieldDim(0.15, in 60 ms, hold until the cast's first hit or 120 ms, out 200 ms)`. The skill FX stays bright because Fx is above FieldDim.
- With ReduceFlash the dim is ≤ 0.2 (it is already 0.15).

**Big-skill flash.** Keep today's rule of once per 30 s per skill and the notice, but replace `FieldFlash(look.Main, 0.22, 0.55)` with `LightFlash(look.Main, 0.18, 0.5)` (screen blend; it goes through the field-flash governor).

**Cast anticipation** (show, `CastCharge` v2):
- a 120 ms charge in which 6–8 sparkles converge with InQuad;
- a caster ground ring at Add 0.7;
- a caster glow fx_glow_core at Add 1 (0.6).

`PlayCast` squash already exists on GuardianSlotView and EchoView. The charge overlaps the projectile flight, so there is no simulator change.

### E8. Echo attacks [ally]
**a) Melee**

**Lunge and squash.** Lunge 14 px toward the target (exists as `Punch × 14`). Squash (1.10, 0.90) → (0.95, 1.06) on a spring.

**Slash.**
- Sprite: `fx_slash_arc` → atk_slash, on layer F.
- Size 110 × U.
- Tint: lerp(Main, the echo's GlowColor, 0.3); Add 0.85.
- Rotation sweeps from facing −35° to +15° over 120 ×P ms (Linear) and fades from 50 %.
- Spawned at the event. The hit lands at the swing peak, 50 ×P ms later (exists).

**Hit at the swing peak.**
- Echo modifier: sizes ×0.65.
- Impact art 0.65 S with Impact(1.30, 33, 60, 0.92), life 200 ×P.
- Core 0.4 S, 40 ms, Add 0.8.
- 2 streaks along the slash tangent.
- No ring and no backing.
- 1 sparkle.
- The `EchoGlowUnder` glow becomes Add 1 (0.35).

**Reaction:** flash ×0.5 (0.45, held 17 ms, total 60 ms), squash 1.10 / 0.90, knockback 5 px, no freeze.

**b) Ranged**
- Shot 62 px, life 0.14 s.
- Impact 0.65 S.
- Core 0.4×.
- 2 streaks.

**c) Hurt** (an enemy strikes an echo; EchoView)
- `PlayHurt` adds a squash of (0.90, 1.10) → spring.
- Flash 0.5 for 80 ms and a ±2.5 px flinch (both exist).

**d) Guardian celebration** (GuardianSlotView `Celebrate()`).

**When it plays:**
- After the guardian's basic attack kills.
- After a basic crit (ally's `OnEnemyHit` crit-echo path).
- At most once per guardian per 2.5 s.

**What it does:**
- A hop of 6 px over 160 ms (sin).
- One sparkle 18 px over the head (Add 0.8, 400 ms).

### E9. Enemy hit reaction [enemy: EnemyView]
All values come from `HitFeel.Feel(tier)` (§2.1). The mechanics are:

**White flash.** Alpha = strength for `FlashHold`, then strength × (1 − InQuad((e − hold) / (life − hold))). Today it fades linearly from the first frame.

**Blush.** Crits only, and off with ReduceFlash. After the flash ends, the flash overlay tints to the attacker Accent at α 0.35 for 60 ms.
- The accent comes from `QueueImpact(..., accent)`.

**Squash spring.**
- Displacement f(t) = e^(−ζωt)·cos(ω_d·t), with ω = 2π·5 and ζ = 0.45. That gives one visible rebound of about −0.2 at 112 ms; it settles by about 250 ms.
- X = 1 + aX·f and Y = 1 − aY·f, where aX = SquashX − 1 and aY = 1 − SquashY.
- A new impulse restarts the spring only if its peak > the current |displacement|.
- Squash stays axis-aligned. Rotating it to the hit direction is P2.

**Hit-stop.**
- `_stop = max(_stop, allowedFreeze)`. While stopped the enemy does not walk (exists).
- **Shiver amplitude** = Shiver × (1 − elapsed/freeze), alternating sign every frame.
- **Shiver axis:** horizontal for walkers (Dust, Metal families) and **vertical for fliers** (Moth family, Shadow family).

**Freeze budget.** A leaky bucket:
- `_stopLoad -= dt × 0.3` every frame, floored at 0.
- A request f is granted min(f, 0.15 − `_stopLoad`), and the granted amount is added to `_stopLoad`.
- So freeze is ≤ about 30 % of any 0.5 s.

**Knockback after the freeze.**
- It starts at impact + granted freeze.
- It goes out to D in `KnockOut` (40 ms, OutExpo) and comes back in `KnockReturn` (120 ms, OutCubic).
- The direction is the hit direction. If there is none, it goes up at ×0.4 (exists).
- Shielded (guarded) hits: no knockback, flash ×0.55.

**Boss.**
- Flash strength ×0.82.
- Squash amplitude ×0.35.
- Knockback 3 px.
- Freeze only on Heavy or Crit, at most 33 ms, with a ±2 px shiver.

**HP ghost bar.** The white trail (`UpdateHp`) holds and then drains:

| Enemy | Hold | Drain |
|---|---|---|
| Normal | 150 ms | 200 ms (OutCubic) |
| Boss | 400 ms | 300 ms (OutCubic) |

Today it drains at 0.9 per second.

**Enemy attacks** (asymmetry, FX side):
- Lunge 12 → 16 px (boss 24 → 30).
- Lunge profile: 20 % of the time going out (OutExpo), then the rest coming back (OutCubic), instead of a sine.
- No additive cores, no gold, art ≤ 110 px against echoes (today 90–100). Muted colours stay.

### E10. Normal kill = "release" [enemy; callout show]
**Timeline:** H = KillHold, 0.14–0.30 s (existing logic). t = 0 is the end of the hold.

**During the hold (EnemyView `TickExit`):**
- The flash is held at α 1.0 for the whole hold.
- Shiver ±4 px, decaying, on the locomotion axis.
- Squash builds from 1/1 to **1.30 / 0.72** with OutQuad.
- The pale tint stays as it is.

**Pop at t = 0:**
- 0–40 ms: uniform scale ×1.15 (OutQuad), flash 1.
- Then the family exit:

| Family | Exit time | What happens |
|---|---|---|
| Dust | 160 ms | InBack shrink to 0 + alpha |
| Moth | 120 ms | the wings art takes over |
| Metal | 140 ms | crush to (1.4, 0.4) + alpha |
| Shadow | 260 ms | unravels upward (exists) |

These exit times keep the test passing: Moth < Dust < Shadow, and all > 0.05.

**Burst** (`BattleFx.EnemyDeath`, Delay = H). K = 200 × U, family from `EnemyFxCatalog.Family`.

| # | Layer | Sprite → fallback | Lyr | Size | t | Curve / motion | Add (α) | Colour | n | Gate |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | ink backing | circle_soft | G | 1.1 K | 0–220 | fade from 30 % | 0 (0.45) | Ink | 1 | H/M |
| 2 | ground ring | `fx_shockwave` | G (aspect GroundAspect, at the feet) | 0.3 → 1.6 K | 0–260 | OutQuart | 0.6 (0.7) | family ring colour (§1.22) → Cream | 1 | all |
| 3 | halo | `fx_glow_core` | F | 1.8 K | 0–220 | OutQuad, then InQuad fade | 1 (0.35) | lerp(Lamp, Cream, 0.5) | 1 | halo |
| 4 | **kill bloom** | **`kill_{family}`** → `enemy_death_{family}` | F | K | 0–420 | **Impact(1.35, 33, 70, 1.30)**; the tail swells with OutQuart; fade from 40 % (InQuad) | 0 (1) | white (tangle: `DeathTint`) | 1 | all |
| 5 | core star | `fx_core_star` | F | 0.8 → 0.5 K | 0–100 | full until 50 ms | 1 (1.0) | CoreCream | 1 | all |
| 6 | ring | `fx_shockwave` | F | 0.5 → 1.6 K | 0–240 | OutQuart | 0.85 (0.8) | CoreCream | 1 | H/M |
| 7 | family layer (kept) | moth wings split / metal bits (6–8 on Bounce) / shadow strands / 2 dust puffs | F | as today | as today | as today | 0 | as today | as today | Extra tier |
| 8 | confetti | `Confetti(set = family)` (§1.22) | F | 14–22 | 0–(450…650) | speed 220–420 px/s in a 140° upward cone, gravity 900, spin ±540°/s | stars 0.5, others 0 | §1.22 | H 10 / M 6 / L 4 (Metal: the bits count) | Sparkle tier |
| 9 | lingering twinkles | `fx_sparkle_cluster` → fx_mote_twinkle | F | 28–40 | 100–(700…900) | rise 30 px; twinkle scale 1 ± 0.15 at 6 Hz | 0.8 (1) | Cream / Lamp | 2 (cluster) or 4 (motes) | Sparkle tier |
| 10 | **memory mote** | `fx_glow_core` 16 px + sparkle | F | 16 | starts at 150 ms, 450 ms flight | Curve to the shop door, **InOutCubic**; on arrival a shop glint (`fx_core_star` 40 px, Add 1, 60 ms) + `ShopView.Absorb(0.22)` | 1 (0.85) | Lamp | 1 (shadow family: 3) | Sparkle tier |
| 11 | reward coin | every 5th kill (exists); trail sparkle Add 0.6 | F | as today | as today | as today | | | | |
| 12 | decals | `decal_glitter` (Moth: decal_glitter; Metal: decal_buttons) | G | 18–30 | 0–2500 | fade over the last 1 s | 0 (0.45) | white | 2 | H (cap) |

**Kill kick:** if `KilledBy` is Crit or Skill, `Kick(outward from the shop, 2 px, Kill)`, at most once per 0.5 s.

**Streak ≥ 10** (`KillStreak`): confetti +2, and 15 % of sparkles and confetti tinted Gold.

**Mini kill** — used when `TryKillBurst` is false (more than 3 full bursts started within 200 ms):
- rows 4 (at 0.6 K), 5 and 6 only;
- 4 confetti;
- no halo, backing, twinkles or memory mote.

**Killing-blow number:** +15 % and a 3-sparkle pop (show, via `OnKillNoted`).

**Footprint:** 4–8 % of the field at High.

### E11. Combo streak and multi-kill callouts [show; counters in core]
**Streak (counted in core `NoteKill`)**
- Kills ≤ 1.2 s apart on the FX clock.
- Milestones: 5, 10, 20 and 40, then every 20.
- Texts (cached `static readonly` strings, no per-frame formatting):

| Milestone | Text |
|---|---|
| 5 | "5연속 수선!" |
| 10 | "10연속 반짝!" |
| 20 | "20연속 대수선!" |
| 40 | "40연속 눈부셔요!" |
| 60–400 (step 20) | "{N}연속!" (precomputed array) |
| beyond 400 | "400+연속!" |

**Multi-kill (core)**
- Kill bursts whose time (clock + delay) falls within 250 ms of each other.
- At ≥ 3 the text is "한꺼번에 {N}!" (cached for 3–20, then "한꺼번에 20+!").
- A multi-kill replaces a streak callout that would fire in the same window.

**Look**
- **Plate:** `plate_combo_9s` (9-slice) as a particle on layer F.
  - Fallback: no plate, text only.
  - Width 120 + 26 × characters, height 84.
  - Tint Lavender below 10, Gold at 10 and above.
- **Text:** a reserved slot in the number pool on layer N.
  - 36 pt bold, Cream (below 10) or Gold gradient (10+), outlined.
- **Anchor:** A = (−FieldHalfWidth + 220, SafeTop − 70) in field-local coordinates. That is just under the stage card at the top-left; never centred.
- **Motion:**
  - slide in from x − 60 over 120 ms (OutBack);
  - scale pop 0.8 → 1.08 → 1.0;
  - hold 700 ms, fade 200 ms.
- **Sparkle burst:** `fx_sparkle_cluster` at the right end of the plate (Add 0.8, 300 ms).

**Multi-kill extra**
- `Shockwave` at the centroid of the kills: 200 → 600 px over 300 ms, Add 0.6, ground aspect.
- `ZoomPunch(1.012)` when N ≥ 5.

**Rate**
- At most 1 callout per 2 s. A pending multi-kill replaces a pending streak callout.
- None for 2 s after a boss kill or for 1.3 s after the lamp.
- None if a skill notice or bubble was admitted in the last 1.2 s. Use SPEC4 `CalloutGate` if present; otherwise a local timer.

### E12. Boss hit [ally art · enemy reaction]
- **Settled size:** ×1.2 (exists). **Frame-0 size:** 1.25.
- **Body:**
  - flash α 0.7 (heavy body), hold 33 ms, total 70 ms;
  - squash 1.06 / 0.95;
  - knockback 3 px;
  - freeze only on Heavy or Crit (33 ms, ±2 px).
- **HP ghost:** 400 ms hold, then a 300 ms drain (E9).
- **Number:** Boss, 55 pt, gradient FFF4DC → D9C8F5, drifting outward.
- **Boss crit:** S_c ×1.4 (exists) and a kick of 2 px.

### E13. Boss death cinematic [enemy: burst; core: the moment]
**Pre-hold** (enemy).
- During the boss KillHold H (≥ 0.14 s):
  - shiver ±6 px;
  - squash builds to 1.35 / 0.70;
  - flash held at 1.0.
- `FieldDim(0.20, in = H, hold 0, out 0)` pre-dims the field (real time).

**t = 0, the moment** (core `FireBossKillMoment`, at most once per 1.5 s):

| Step | Real-time | Spec | ReduceFlash | Shake 0 |
|---|---|---|---|---|
| Global stop | 0–120 ms | `GlobalHitStop` (exists), then 0.3× for 180 ms | same | same (time, not motion) |
| **Impact frame** | 0–33 ms | `FieldDim` α 0.45. Boss silhouette (pool S, WhiteFlash material) in CoreCream at α 1. **Misprint copies**: DustyPink at (−6, +2) px and SoftBlue at (+6, −2) px, α 0.5. | dim 0.2 for 100 ms; no silhouette, no misprint | same as normal |
| | 33–50 ms | dim 0.2; silhouette α 0.5; misprint continues | — | |
| | 50–200 ms | dim fades to 0 (OutQuad); misprint ends at 100 ms | — | |
| Light flash | starts +50 ms | `LightFlash(Cream, 0.35, 0.5 s)` (screen blend) | 0.15 | same |
| Camera | 0 | trauma **0.80** + `ZoomPunch(1.03, pivot = burst point)` | same | none |
| Haptic | 0 | `Haptics.Play(Heavy)` | same | same |

**Burst** (enemy `BossDeathExtras` v2; spawned with Delay = H so it shows at t = 0 and stays frozen on its first frame during the stop).
- B = 420 × U.
- The centre is `ClampToSafe(point, 0.3 B)`, which allows at most 40 % clipping.

| # | Layer | Sprite → fallback | Lyr | Size | t (FX clock) | Curve / motion | Add (α) | Colour | n |
|---|---|---|---|---|---|---|---|---|---|
| 1 | ink backing | circle_soft | G | 1.2 B | 0–400 | — | 0 (0.5) | Ink | 1 |
| 2 | ground ring | fx_shockwave | G | 0.3 → 2.4 B | 0–500 | OutQuart | 0.6 (0.7) | Lamp | 1 |
| 3 | rays | rays.png (exists) | F | 1.6 B | 0–1000 | rotates +30°/s; α in over 80 ms, out over the last 40 % | 0.35 (1) | Lamp | 1 |
| 4 | speedlines | fx_speedlines | F | 2.4 B | 0–260 | size 0.9 → 1.1 | 0.55 (1) | CoreCream | 1 (H/M) |
| 5 | **hero burst** | **`hero_boss_kill`** → vfx_star_burst + `enemy_death_{family}` ×2 | F | B | 0–900 | Impact(1.30, 50, 80, 1.25); OutQuart swell; fade from 45 % | 0 (1) | white | 1 |
| 6 | core | fx_core_star | F | 1.0 B → 0.6 B | 0–150 | full until 80 ms | 1 (1.0) | CoreCream | 1 |
| 7 | flare | fx_flare_cross | F | 3 B × 0.15 B | 0–120 | — | 1 (0.9) | lerp(Gold, Cream, 0.5) | 1 |
| 8 | rings ×3 | fx_shockwave (CoreCream, Gold, Lavender) | F | 0.3 → 3.0 B | 0, 60, 120 → +400 | OutQuart | 0.85 (0.8) | as listed | 3 |
| 9 | confetti | `Confetti(Boss)`: buttons, ribbons, stars, thread, motif_star | F | 18–30 | 0–(800…1300) | 380–760 px/s, 160° up cone, gravity 900, spin ±540°/s | stars 0.5 | mixed pastels + Gold | H 24 / M 16 / L 10 |
| 10 | ribbons | fx_confetti_ribbon | F | 36–48 | 0–1400 | 250–450 px/s, flutter wobble 12 px, gravity 500 | 0 | DustyPink / Lavender / Gold | H 6 / M 4 / L 2 |
| 11 | sparkles | fx_sparkle / motif_star (exists, 18) | F | 18–30 | 12 ms stagger | as today | 0.8 | Gold / Cream | H 18 / M 12 / L 6 |
| 12 | lingering stars | motif_star twinkles | F | 16–24 | 200–1400 | drift up 40 px, twinkle | 0.7 | Lamp | 10 (H) / 6 / 0 |
| 13 | memory motes | RememberMote | F | 16 | 300 + i × 60 ms | to the shop | 1 | Lamp | 8 (shadow family) / 5 |
| 14 | reward coins | `KillCoin` ×3, staggered 80 ms, bypassing the cap | F | 40 | exists | exists | | | 3 |

- The banner "보스 처치!" already exists (ally `OnEnemyKilled` → `ShowBossDefeated`).
- Budget: needs about 75 free upper particles. If fewer are free, confetti drops to 12 and sparkles to 8. The hero rows (1–8) always spawn (Core tier).

### E14. Shop hit [enemy; the "-N" number is show]
**Normal hit on the shop (`ShopImpact`)**

| # | Layer | Sprite | Lyr | Size | t | Curve / motion | Add (α) | Colour | n |
|---|---|---|---|---|---|---|---|---|---|
| 1 | red glow | glow_soft | F | 0.25 → 0.9 × hitSize | 0–250 | — | 0 (0.6) | Danger (tint, never additive) | 1 |
| 2 | hit art | enemy_hit_shop | F | hitSize | 30–380 | **Impact(1.25, 33, 70, 0.95)**; drifts outward 10 px | 0 (1) | white | 1 |
| 3 | splinters | motif_splinter | F | 14–20 (boss ×1.4) | 30–630 | **Bounce**: v 140–260 px/s outward and up, gravity 900, floor = door y − 20, restitution 0.35, spin ±480°/s | 0 | WoodBrown / Cream | 3 (boss 5, siege 7) |
| 4 | dust puff | fx_puff | F | 0.3 → 0.7 × hitSize | 30–530 | as today | 0 (0.35) | Lavender | 1 |

**Camera**
- Trauma 0.30 toward the door, through the **shop channel** (≤ 0.6/s) — `ShopStrikeTrauma` (unchanged formula).
- `Kick(−outDirection, 2 px, Shop)`, at most once per 0.25 s.
- Boss siege: trauma 0.55 and a kick of 4 px.

**ShopView (enemy)**
- `Hit()` squash rises from 0.04 to 0.07 and returns on a spring (5 Hz, ζ 0.5). The red tint stays.
- Boss siege squash: 0.12.

**DirectionRingView:** the slot flash stays as it is.

**Absorbed (shield)**
- Hex ripple at Add 0.6.
- A SoftBlue core star at 0.4 × hitSize (Add 0.8).
- Half trauma, no kick.

**`ShopDamaged` (core)**
- The red glow stays alpha.
- Number: ShopDamage "-N", 46 pt red; pop 1.4 → 1.0 over 100 ms plus a ±3 px x-shake for 120 ms (show).

### E15. Shields [ally ripple · show shatter · enemy reaction]
**Shielded hit**
- Impact ×0.7.
- `ShieldRipple` (hex) at Add 0.6.
- Reaction: flash ×0.55, no knockback, the bubble wobbles (exists).
- Number: "막음 N", Block kind, 39 pt soft blue, ±2 px x-shake for 120 ms.

**Shield break on an enemy** (show `OnShieldBroken` + `ShieldShatter` v2) — reaction is the Heavy tier:

| # | Layer | Sprite | Lyr | Size | t | Curve / motion | Add (α) | Colour | n |
|---|---|---|---|---|---|---|---|---|---|
| 1 | core star | fx_core_star | F | 0.6 × size | 0–66 | — | 1 (0.9) | white | 1 |
| 2 | shockwave | fx_shockwave | F | 0.4 → 1.6 × size | 0–200 | OutQuart | 0.85 (0.8) | SoftBlue | 1 |
| 3 | hex shards | motif_shard | F | 14–22 | 0–500 | Bounce at 300–520 px/s, gravity 900, spin ±540°/s | 0.3 | SoftBlue / white | 8 (M 6, L 4) |

- `Kick(hitDir, 2 px (boss 3), Shield)`.
- A small "깨짐!" chip: Text, 34 pt soft blue, rises 30 px, 600 ms.

**Shop shield break** (`TargetId < 0`)
- `ShieldShatter` at the dome.
- Trauma **0.35** (`HitFeel.ShopShieldBreakTrauma`; replaces the literal 0.08).
- `LightFlash(SoftBlue, 0.15, 0.4)`.

**Echo shield break:** `ShieldShatter` at 90 px (exists) gets the v2 layers above.

### E16. Heal and support pulses [show: BattleFx.Pulse.cs; ShopHealed in core]
These stay gentle: the period is ≥ 1 s, below 60 BPM. Never use core stars, kicks or flashes, and keep Add ≤ 0.5.

**Support pulse**
- Ring at Add 0.4.
- Ribbon and crown as today.
- One glint per receiver: fx_sparkle, Add 0.5, 300 ms.

**Heal pulse**
- Mint ring at Add 0.5.
- Drops (exist); each lands with a mini splash ring, Add 0.4, 46 px.

**`ShopHealed`** (core): sparkles at Add 0.5 and hearts as today.

**Numbers:** Heal, mint, 41 pt; pop 0.7 → 1.05 → 1.0; merge window 0.5 s.

### E17. Lamp ultimate "불 밝히기" [core; ShopView flame is enemy]
`BattleView.cs` (locked) calls `LampBurst(door)`, then `GlobalHitStop(0, 0.2, 0.12)`, then `_shop.LampBurst()`. Everything new lives in `LampBurst`, `LampHit` and `UpdateLampRing`. The whole sequence takes ≤ 1.3 s.

**Ignite (r = real time from 0; the FX clock runs at 0.2× for 120 ms real)**
- `FieldDim(0.35, in 60 ms OutQuad, hold until release, out 200 ms)`.
- Anticipation core at the door: fx_glow_core, Lamp colour, 60 → 160 px, Add 1 (0.6), on the FX clock 0 → 0.06.
- 12 converging sparks (exist), at Add 0.8.
- ShopView (enemy): glow scale +0.4, and the flame flickers at 12 Hz with amplitude 0.08 for 0.4 s.

**Release (FX clock = `LampWave.Start` 0.06 s ≈ r 0.156 s)**

| # | Layer | Sprite → fallback | Lyr | Size | t (from release) | Curve | Add (α) | Colour |
|---|---|---|---|---|---|---|---|---|
| 1 | impact frame | `ImpactFrame(null, …)`: dim only | D | — | 0–33 at 0.45, then fade over 200 ms | — | — | Dim |
| 2 | rays | rays.png | F | 900 *screen* | 0–1000 | rotates 20°/s; in 80 ms; out over the last 40 % | 0.35 (1) | Lamp |
| 3 | **bloom** | **`hero_lamp_bloom`** → glow_soft | F | 0 → 620 *screen* (OutCubic, 200 ms), hold, fade by 700 | 0–700 | rotates 10°/s | 0.7 (1) | white (art) / Lamp |
| 4 | core | fx_core_star | F | 300 → 200 | 0–120 | — | 1 (1.0) | CoreCream |
| 5 | flare | fx_flare_cross | F | 900 × 60 | 0–150 | — | 1 (0.9) | lerp(Lamp, Cream, 0.5) |
| 6 | wave ring | `_lampRing` with **`fx_lamp_wave_ring`** → ring_glow (premul material) | ground plane | LampWave radius (exists) | 0–500 | exists | 0.9 (1 − k²) | Cream → Lamp (exists) |
| 7 | inner ring | fx_shockwave | G | follows 0.8 × the wave radius | 0–500 | same timing | 0.5 (0.6) | Cream |
| 8 | pillar | `_lampPillar` with **`fx_light_pillar`** → glow (premul) | F | 240 × 900 (exists) | 0–900 (exists) | exists | 0.8 | Cream |
| 9 | speedlines | fx_speedlines | F | 1400 *screen* | 0–200 | — | 0.4 (1) | CoreCream |
| 10 | out sparks | exists (16) | F | as today | as today | as today | 0.8 | Lamp / Cream |
| 11 | rain | motif_star + fx_mote_petal | F | 14–24 | 150–1150 | as today | 0.6 | Lamp / Cream |

- Rain count: 40 if the free budget is > 50 %, otherwise 18 (H); 24 on M; 10 on L.

**Screen at release**
- `LightFlash(Cream, 0.45 (ReduceFlash 0.25), 0.6 s, force)` replaces today's alpha `FieldFlash`.
- `ZoomPunch(1.035, pivot = shop, force)`.
- Trauma 0.85, direction up.
- `Haptics.Play(Medium)`.

**Per enemy (`LampHit` at `LampWaveDelay`)**
- core fx_core_star 66 px, Add 1, 66 ms;
- motif_star in Gold, Impact(1.30, 33, 70, 0.9) at 110 px, life 400 ms;
- ring fx_shockwave in Lamp, 40 → 150 px, 220 ms, Add 0.8;
- 3 streaks outward from the shop at 500–800 px/s.

The reaction is the Lamp tier (knockback 10 px outward; exists through `QueueImpact` Lamp). Numbers use the Lamp kind, 51 pt, merged per target with a 0.5 s window.

### E18. Stage clear [enemy: RewardShower; core: ScreenFlash light mode; ShopView]
`BattleView.cs` (locked) calls `ScreenFlash(Cream, 0.3, 0.6)`, then `RewardShower((0, FieldTop × 2/3), 5)`, then `_shop.LampBurst()`.

- **ScreenFlash (core):** a light colour (luminance > 0.6) → screen-blend light mode, app-wide (§2.3). ReduceFlash caps it at 0.3.
- **RewardShower (enemy).** Anchor A = from. This event is exempt from the safe box: it sits behind the ribbon banner and may clip at the top.

| # | Layer | Sprite → fallback | Lyr | Size | t | Curve / motion | Add (α) | Colour | n |
|---|---|---|---|---|---|---|---|---|---|
| 1 | glow | fx_glow_core | F | 900 | 0–600 | in over 80 ms, then fade | 1 (0.35) | Gold | 1 |
| 2 | **hero** | **`hero_stage_clear`** → vfx_star_burst + fx_ring_thread | F | 0 → 560 | 0–800 | Pop (exists; OutBack 2.6 over 22 %), fade from 60 % | 0 (1) | white | 1 |
| 3 | shockwave | fx_shockwave | F | 100 → 760 | 0–450 | OutQuart | 0.7 (0.8) | Gold → Cream | 1 |
| 4 | confetti | `Confetti(Celebration)`: 4 × fx_confetti_* + motif_star | F | 18–30 | 0–(900…1400) | 300–650 px/s, 150° up cone, gravity 700, spin ±540°/s, ribbons flutter 12 px | stars 0.5 | pastels + Gold | H 30 / M 20 / L 10 |
| 5 | sparkle fountain | fx_sparkle | F | 16–26 | 100–1100 | rise 140–260 px/s, gravity 200 | 0.8 | Cream / Lamp | 12 |
| 6 | coins | exists | F | | | | | | 5 |

- `ZoomPunch(1.015, pivot = A)`. No shake.
- ShopView `LampBurst` glow (exists, and gets the E17 flicker).

### E19. Other events (small changes)
- **Enemy spawn** (enemy): the swirl stays as it is.
- **Boss spawn:** the pool stays as it is. Add a dark Ink ring (alpha, not additive) from 0.3 to 1.2 × pool over 500 ms. `BattleView.cs` already adds trauma 0.62 (the new constant) and a Danger tint flash.
- **Fail** (`BattleView.cs`): Danger tint flash (alpha) + trauma 0.65. Nothing else.
- **Echo summon / dissolve** (core): the converging sparkles, the flash and the ring get Add 0.6–0.8. Strands get Add 0.5.
- **Enemy enrage** (show): replace the trauma literal 0.25 with `HitFeel.BossEnrageTrauma = 0.45`.
- **Stage-modifier announcement** (show): keep `FieldFlash(color, 0.18, 0.8)` as an alpha tint. It counts toward the flash governor.

### 1.21 Per-lineage signature table [ally unless noted]
The ring sprite is used in E3 row 4 and E4.

| Lineage | Main / Sub / Accent | Impact v2 / crit art | Spark and shard (muzzle, flight, streak tint) | Crit special (E5 row 11; within 0–330 ms) | Decal | Ring sprite |
|---|---|---|---|---|---|---|
| 짝짝이 jjakjjak | F3E6D0 / 8A63C9 / E79BB8 | `atk_jjakjjak_impact` / `_crit` | thin purple needle ticks (fx_streak in 8A63C9, 3 px) | 2 X-stitch stamps 40 ms apart (`StitchMark` at 1.6× size, Impact(1.3, 16, 50, 1.0)) + a **thread whip**: fx_streak in Sub, 1.6 S long, Line motion across the target, 120 ms, Add 0.4 | decal_thread | fx_ring_thread, tinted Sub |
| 똑단이 ttokdan | 93C8F0 / FFF4DC / F2C46B | `atk_ttokdan_impact` / `_crit` | motif_button 10 px | button "clack" (crit art) + **6 mini buttons** ricochet (`ButtonBounce` ×2, spin ±720°/s) + a stitched gold ring | decal_buttons | fx_ring_thread, tinted Main |
| 찰칵이 chalkak | E8C27A / C4B2F2 / E9A6C6 | `atk_chalkak_impact` / `_crit` (upright) | fx_mote_twinkle, gold | keyhole lock ring closes from 1.6 S to 0.5 S in 90 ms (`LockRing` scaled) + a gold glint sweep (fx_flare_cross in Gold, rotating +45° over 120 ms) + **freeze ×1.2** (80 ms) | decal_glitter | fx_shockwave, Gold |
| 몽당이 mongdang | FFF1D6 / E9A6C6 / FFB86B | `atk_mongdang_impact` / `_crit` | motif_flame 12 px, rising embers (gravity −120) | flame flare: an extra motif_flame stretches Y 1 → 1.6 over 120 ms + a **warm light pool** (G, fx_glow_core in FFB86B, Add 1, 0.4, 1.8 S, 300 ms) + 6 embers | decal_petal | fx_shockwave, FFB86B |
| 우두커니 udukeoni | 7FA8D9 / 2D2458 / F2C46B | `atk_udukeoni_impact` / `_crit` | motif_drop 10 px on parabolas | splash crown (crit art) + a **double ripple** (2 ground shockwaves at 0 and 100 ms, GroundAspect, Main, Add 0.6) + 8 droplets. On a Rain stage everything ×1.2. | decal_puddle | fx_shockwave on the ground, Main |
| 루즈리 rouge | E58BAE / E8C27A / FFF4DC | `atk_rouge_impact` / `_crit` | motif_heart 10 px | kiss stamp (crit art): Impact(1.5, 16, 70, 1.0) with a squash of 1.1 / 0.9 + 3 hearts float (`FloatHearts` ×1.5) + a pink light pool (G, Main, Add 1, 0.3) | decal_heart | fx_shockwave, Main |
| 라라벨 lalabell | C4B2F2 / F2B8D0 / F2C46B | `atk_lalabell_impact` / `_crit` | motif_note 12 px | **3-note chord fan**: motif_note 24 px at −25°, 0° and +25°, 30 ms apart, 120 px/s + a gold staff ring (fx_star_ring or vfx_sound_ring, Gold, Add 0.7) | none (notes float up) | vfx_sound_ring |

Muzzle sparks (E1 row 3) use the same spark column, coloured with the lineage Sub, or Accent for chalkak, ttokdan and lalabell.

### 1.22 Enemy family table [enemy; the confetti sets are defined in core `Confetti`]
| Family (enemies) | Kill bloom | Exit | Confetti set (10 at H) | Kept family layer | Ring colour | Decal |
|---|---|---|---|---|---|---|
| Dust (enemy_dust, enemy_tangle) | `kill_dust` (tangle tinted with `DeathTint`) | 160 ms, InBack shrink | 4 fx_mote_dust (Lavender-grey) + 3 fx_confetti_star (Cream/Gold, Add 0.5) + 3 small fx_puff (α 0.6) | 2 puffs | Lavender | decal_glitter |
| Moth (enemy_moth, boss_mothqueen) | `kill_moth` | 120 ms | 5 motif_scale (ScaleDust/Lavender) + 5 fx_mote_petal (DustyPink/Cream) | wings split | DustyPink | decal_glitter |
| Metal (enemy_rust, enemy_thimble, enemy_windup) | `kill_metal` | 140 ms crush | the existing 6–8 gears, bolts and springs (Bounce) + 2 fx_confetti_button (Gold) | metal flash | Gold | decal_buttons |
| Shadow (enemy_wisp, boss_shadow) | `kill_shadow` | 260 ms unravel | 5 fx_confetti_thread (Lavender/Lamp) + 5 motif_star (Lamp, Add 0.5) | strands + memory orbs | Lamp | none |

`Confetti` sets in core: `Dust`, `Moth`, `Metal`, `Shadow`, `Celebration` (stage clear) and `Boss`.

---

## 2. Global hit feel (HitFeel v2) [core]

### 2.1 The TierFeel table
This table is the single source of truth. It lives in `HitFeel.Feel(HitTier)`, and every owner reads it.

| Field | Normal | Heavy | Crit | Skill | Lamp | Tick |
|---|---|---|---|---|---|---|
| FirstFrame | 1.30 | 1.35 | 1.45 | 1.35 | 1.30 | — |
| Hold s | 0.033 | 0.033 | 0.050 | 0.033 | 0.033 | — |
| Settle s | 0.070 | 0.070 | 0.080 | 0.070 | 0.070 | — |
| Life s (impact art, ×P) | 0.24 | 0.27 | 0.33 | 0.27 | 0.40 | — |
| Core × / life s | 0.50 / 0.050 | 0.60 / 0.066 | 0.75 / 0.083 | 0.60 / 0.066 | 0.60 / 0.066 | — |
| Backing α / × size | 0.24 / 0.90 | 0.32 / 0.95 | 0.40 / 1.0 | 0.34 / 1.0 | 0 | 0 |
| Rings | 1 (H) | 1 | 2 | 1 | 1 | 0 |
| Streaks / cone° / speed px/s | 4 / 100 / 450–700 | 6 / 70 / 700–1000 | 8 / 360 / 600–1000 | 6 / 70 / 600–900 | 3 / 40 / 500–800 | 0 |
| FlashAlpha / FlashHold s / FlashLife s | 0.85 / 0.033 / 0.07 | 0.95 / 0.033 / 0.08 | 1.0 / 0.05 / 0.10 | 0.9 / 0.033 / 0.08 | 0.85 / 0.033 / 0.08 | 0.35 / 0 / 0.05 |
| SquashX / SquashY | 1.16 / 0.86 | 1.20 / 0.82 | 1.26 / 0.78 | 1.20 / 0.82 | 1.12 / 0.90 | 1.04 / 0.97 |
| Knockback px | 8 | 12 | 16 | 12 | 10 | 0 |
| Freeze s / Shiver px | 0 / 0 | 0.033 / 3 | 0.066 / 5 | 0.05 / 3 | 0 / 0 | 0 / 0 |
| NumberScale (× 46 pt) | 1.0 | 1.15 | 1.6 | 1.2 | 1.1 | 0.85 |

**Modifiers** (static helpers `HitFeel.ForBoss(in f)`, `ForEcho(in f)`, `ForGuarded(in f)`):
- **Boss:** FlashAlpha ×0.82, capped at 0.7 for Normal; squash amplitude ×0.35; Knockback 3; Freeze min(0.033, f.Freeze) for Heavy and Crit and 0 otherwise; Shiver 2; FirstFrame −0.05.
- **Echo:** sizes ×0.65; streak counts halved (minimum 1); FlashAlpha ×0.5; Knockback ×0.6 (5 px); Freeze 0.
- **Guarded (shielded):** sizes ×0.7; FlashAlpha ×0.55; Knockback 0.

**Back-compat functions** (tests and old call sites):
- `FlashTime(tier) = FlashLife`.
- `FlashStrength(tier) = FlashAlpha`.
- `TargetStop(tier) = Freeze`.
- `Knockback(tier, boss) = boss && tier != Tick ? 3 : Knockback`.
- `KnockbackTime = 0.16f`, which is `KnockOut + KnockReturn` (0.04 + 0.12).

**Freeze budget:** `FreezeBudget = 0.15f` per `FreezeWindow = 0.5f`, as a leaky bucket (E9).

**Kill values:**
- `KillFlashAlpha = 1f`, `KillShiver = 4f`.
- `KillSquashX = 1.30f`, `KillSquashY = 0.72f`, `KillPop = 0.04f`.
- `KillBurstSize = 200f`, `BossBurstSize = 420f`.

### 2.2 Camera
**Trauma** (`TraumaShake`, Eiserloh)
- `MaxOffset` 18 → **30** px.
- `MaxRotation` 1.5° → **1.0°**.
- `Decay` 1.4 → **1.6** per second, so frequent events don't pile up into a constant wobble.
- The exponent stays 2 and `Frequency` stays 22.
- The effective peak is about 0.6 × 30 × trauma² px.

| Constant | Old → new | Effective peak |
|---|---|---|
| CritTrauma | 0.05 → **0** (a kick instead) | — |
| AreaSkillTrauma | 0.1 → **0.40** (new skill channel, ≤ 0.5/s) | about 2.9 px |
| ShopHitTrauma / ShopTraumaPerSecond | 0.04 / 0.25 → **0.30 / 0.60** | about 1.6 px |
| ShopShieldBreakTrauma (new; replaces the literal 0.08) | **0.35** | about 2.2 px |
| BossSiegeTrauma | 0.2 → **0.55** | about 5.4 px |
| BossEnrageTrauma (new; replaces the literal 0.25) | **0.45** | about 3.6 px |
| BossSpawnTrauma | 0.3 → **0.62** | about 6.9 px |
| FailTrauma | 0.4 → **0.65** | about 7.6 px |
| BossKillTrauma | 0.5 → **0.80** | about 11.5 px |
| LampTrauma | 0.55 → **0.85** | about 13 px |

**Kicks** (`FxKick`, new pure struct)
- A single directional push, not noise.
- `Add(dir, px)` targets v = dir × px, with |v| capped at 4 px.
- Profile: g rises 0 → 1 in 30 ms (OutQuad), then returns critically damped: g(τ) = (1 + 34τ)·e^(−34τ), which is ≈ 0.05 at 140 ms.
- Several kicks do not add up: the live kick is the one with the larger |v|·g(now).
- Per-channel rate limits (`KickChannel`):

| Channel | Interval |
|---|---|
| Crit | 0.5 s |
| Snipe | 0.6 s |
| Shop | 0.25 s |
| Kill | 0.5 s |
| Skill | 0.4 s |
| Shield | 0.5 s |
| Siege | 0.5 s |

- **Kick sizes:** crit 3 (boss crit 2), snipe 2, shop 2, boss siege 4, crit/skill kill 2, single-target skill 2, shield break 2 (boss 3).

**Zoom punch** (`FxZoom`, new pure struct)
- z(t) = 1 + (peak − 1)·h(t), where h rises 0 → 1 in 60 ms (OutQuad) and falls 1 → 0 in 220 ms (OutCubic).
- A new punch replaces the live one only if it is larger at that moment.
- Peaks:

| Event | Peak |
|---|---|
| Default "screen punch" | 1.02 |
| Boss kill | 1.03 |
| Lamp | 1.035 |
| Stage clear | 1.015 |
| Multi-kill (N ≥ 5) | 1.012 |

- At most one per 0.8 s, except when forced (boss kill, lamp).
- **It scales the field only** (ShakeRoot), never the HUD, deck or lamp button. Pivot P is in field-local coordinates.

**Applied in `UpdateShake`:**
```
offset = trauma(dt) + kick(dt) * shakeScale
z = 1 + (zoom(dt) - 1) * shakeScale
_shakeRoot.anchoredPosition = offset + (1 - z) * P
_shakeRoot.localScale = (z, z, 1)
_shakeRoot.localRotation = rot
```
Write the transform only while anything is active, then reset once (as today).

**Global stop**
- `GlobalStop 0.12`, `SlowScale 0.3`, `SlowTime 0.18` — unchanged.
- New gate: at most one global stop per 1.5 s. The lamp ignite trigger is separate and exempt.

**ShakeScale 0 means no camera motion:** trauma, kicks and zoom are all off. Knockback, squash and hit-stop stay, because they carry information. Slow motion stays, because it is time, not motion.

### 2.3 Flash and colour rules
**Three kinds of full-field overlay**

| Kind | API | Blend | Used for | Max (normal / ReduceFlash) |
|---|---|---|---|---|
| **Light** | `LightFlash` (field) / `ScreenFlash` with a light colour (app-wide) | screen (`UIFxScreen`) | lamp 0.45, boss kill 0.35, stage clear 0.30 (app-wide), big skill 0.18, shop shield break 0.15 | 0.45 / 0.30 (lamp 0.25, boss 0.15) |
| **Tint** | `FieldFlash` / `ScreenFlash` with a dark or saturated colour | alpha (default UI material) | danger (fail 0.30, boss spawn 0.22), stage-modifier announcement 0.18 | 0.30 / 0.30 |
| **Dim** | `FieldDim` (new layer under Fx) | alpha (default UI material) | spotlight 0.15, boss pre-dim 0.20, lamp ignite 0.35, impact frame 0.45 | 0.45 / 0.20 |

- **ScreenFlash** (called from the locked `BattleView.cs`) picks the mode by colour: luminance > 0.6 → Light (the screen material), otherwise Tint (the default material). It switches `Image.material` only when the flash starts.

**Flash governor (WCAG 2.3.1)**
- At most **one** field-wide Light flash, Tint flash or impact frame per **1.0 s** of real time.
- The lamp and boss-kill sequences take the slot with `force` and block others for 1.0 s.
- A blocked flash is dropped, not queued. Dims ≤ 0.20 with fades ≥ 60 ms do not count.

**Impact frame** — boss kill, the lamp release, and P2 crit-kills of shielded enemies (≤ 1 per 1.5 s):
- dim 0.45 for 2 frames (33 ms real) → 0.2 for 1 frame → fade out over 150 ms;
- the target is drawn as a CoreCream silhouette (pool S) for 2 frames, then at α 0.5 for one more frame;
- ReduceFlash: a dim of 0.2 for 100 ms only, with no silhouette.

**Misprint ("chromatic" storybook print offset)**
- Two extra silhouette copies, DustyPink at −x and SoftBlue at +x, at α 0.5.
- Boss kill (P1): ±6 px, 100 ms. Crit kills (P2): ±3 px, α 0.4, 50 ms, rate ≤ 1 per 1 s.
- Never full-screen.
- Off with ReduceFlash and on FX Low.

**Colour vocabulary**

| Colour | Meaning |
|---|---|
| Cream / white light | allies and reward light |
| **Gold** | crit, lamp and boss reward only |
| Lineage Accent | signature (cores, rings, streaks, blush) |
| SoftBlue | shields |
| Mint | heals |
| Red (Danger) | shop damage and danger only |
| Muted family colours, never additive | enemies |
| Indigo (Ink, Dim) | contrast and dims, never as "black" |

**Never colour alone**
- Crit = ★ + the slam motion.
- Mark = ♥ prefix (exists).
- Blocked = "막음".
- Streak = text.
- Danger direction = the "!" icon (exists).

### 2.4 Density governor and the 20-hit budget [core API, used by everyone]
| Rule | Value | Who checks |
|---|---|---|
| Hit density | Scheduled arrivals in the 250 ms before this arrival: ≤ 8 full; 9–16 **mini** for Normal tier; > 16 **lean** (Normal tier gets the mini art + core only — no streaks, sparkles or extras) | ally (`Density`), core (`SkillHit`) |
| Per-target full impact | 1 per 90 ms per target id (`TryFullImpact`); others mini | ally, core |
| Big bursts (crit, kill, skill burst) | ≤ 2 start per 100 ms. Extras are delayed +33 ms, then +50 ms; beyond that they are downgraded (`BigBurstDelay` returns −1) | ally (crit), enemy (kill), show (skill burst) |
| Kill bursts | ≤ 3 full per 200 ms (`TryKillBurst`), else a mini kill | enemy |
| Halos (large additive glows ≥ 1.8× size) | alive ≤ 6 / 4 / 2 (H/M/L), via `Halo()` | core primitive |
| Decals | alive ≤ 12 / 6 / 0 | core primitive |
| Number merge window | 0.12 → 0.25 s when > 8 numbers are alive or density > 8 | show |
| Visible numbers | 24 / 16 / 10 | show |
| Field-wide flashes | 1 per 1.0 s (§2.3) | core |
| Global stop | 1 per 1.5 s | core |
| Zoom punch | 1 per 0.8 s (forced: boss, lamp) | core |
| Kicks | per channel (§2.2), magnitude ≤ 4 px | core |
| Skill trauma | ≤ 0.5 per second (new channel) | core |
| Lamp rain | 40 only if free > 50 % | core |

**Worst case: 20 enemies hit in one frame by an area skill.**
- They are staggered 25 ms per 100 px from the centre.
- The first 8 arrivals get full Skill-tier impacts; the rest get minis.
- Numbers merge.
- It reads as a wave, not a white-out.

**Sustained attacking:** 10 attackers at 2× is about 20 arrivals per second, which is about 5 per 250 ms. So every basic hit stays full.

**Estimated average live upper particles** in the stress scenario (5 guardians + 5 echoes + 20 enemies, 2×):
- H about 210, with peaks about 300.
- M about 150.
- L about 100.

All of these are under the new budgets (§3.4).

### 2.5 Settings matrix
**Cached settings.** `BattleFx.ReadSettings` (on `GameSettings.Changed`) caches quality, ReduceFlash, ShakeScale and DamageNumbers.
- Expose the cached ReduceFlash as `public static bool ReduceFlashNow` for EnemyView.
- **A gallery override** (`BattleFx.DevOverride`) lets FxGallery force these values without writing PlayerPrefs. The fx_proj copy shares the user's editor PlayerPrefs keys, so the gallery must **never** call the GameSettings setters.

| Feature | High | Medium | Low | ReduceFlash on | Shake 0 % |
|---|---|---|---|---|---|
| Upper particle budget / ground budget | 420 / 96 | 240 / 64 | 130 / 32 | — | — |
| Frame spawn cap (sparkle/extra) | 48 | 36 | 24 | — | — |
| Ink backing | ✓ | crit, kill, boss only | ✗ | ✓ | ✓ |
| Normal-hit ring | ✓ (dens ≤ 8) | rhythm accents only | ✗ | ✓ | ✓ |
| Streaks per normal hit | 4 | 3 | 2 | ✓ | ✓ |
| Afterimages / shed motes | 2 / 2 | 1 / 1 | 0 / 0 | ✓ | ✓ |
| Halo cap | 6 | 4 | 2 | intensity ×0.7 | ✓ |
| Speedlines | ✓ | boss and lamp only | ✗ | ✓ | ✓ |
| Decal cap | 12 | 6 | 0 | ✓ | ✓ |
| Kill confetti / boss confetti, ribbons, sparkles | 10 / 24, 6, 18 | 6 / 16, 4, 12 | 4 / 10, 2, 6 | ✓ | ✓ |
| Lamp rain | 40 (if free > 50 %) or 18 | 24 | 10 | ✓ | ✓ |
| Visible numbers | 24 | 16 | 10 | ✓ | ✓ |
| Core star intensity | 1.0 | 1.0 | 1.0 | **×0.6** | ✓ |
| Crit blush | ✓ | ✓ | ✓ | **✗** | ✓ |
| Impact-frame silhouette / misprint | ✓ / ✓ | ✓ / ✓ | ✓ / ✗ | **✗ / ✗** (dim 0.2 only) | ✓ |
| Light flashes | per §2.3 | | | **≤ 0.3** (lamp 0.25, boss 0.15) | ✓ |
| Trauma, kicks, zoom | ✓ | ✓ | ✓ | ✓ | **all 0** |
| Flipbook (P2) | ✓ | ✗ | ✗ | ✓ | ✓ |

FxQualityMonitor (UX-owned) keeps lowering quality through GameSettings, and `ReadSettings` picks it up.

---

## 3. Tech

### 3.1 Shaders and materials [core]
**`Scripts/UI/Battle/Shaders/UIFxPremul.shader`** — `"IdleGame/UI/FxPremul"`. This is the one material for **all particles**. Additive and alpha-blended particles batch together.
- The Properties, Tags, Stencil block, `ZTest [unity_GUIZTestMode]` and `ColorMask` are the same as `UIWhiteFlash.shader`.
- `Blend One OneMinusSrcAlpha`.
- `#pragma multi_compile_local _ UNITY_UI_CLIP_RECT`.
- **No `UNITY_UI_ALPHACLIP`**, because additive pixels have alpha ≈ 0.

```hlsl
v2f vert(appdata_t v) { /* as UIWhiteFlash */ OUT.color = v.color * _Color; }
fixed4 frag(v2f IN) : SV_Target
{
    half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
    half4 c;
    c.rgb = tex.rgb * tex.a * IN.color.rgb;   // BattleFx writes vertex rgb already × opacity (premultiplied)
    c.a   = tex.a * IN.color.a;               // coverage of the alpha-blended share (0 = pure light)
    #ifdef UNITY_UI_CLIP_RECT
    c *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);   // premultiplied: clip rgb AND a
    #endif
    return c;
}
```

**Vertex colour encoding.** This is the only place particle colours are written: `UpdateParticles`, plus `Spawn` with o = 0.
```csharp
float o = p.Color.a * fadeIn * fadeOut;              // opacity (Add = 0) or intensity (Add > 0)
p.Image.color = new Color(p.Color.r * o, p.Color.g * o, p.Color.b * o,
                          Mathf.Max(1f / 255f, o * (1f - p.Add)));
```
- The alpha floor is 1/255 and `canvasRenderer.cullTransparentMesh = false` is set on every pooled Image at Prepare. Together they stop pure-additive quads from being culled.
- To fade an additive particle, scale o. Never fade by alpha alone.

**`Scripts/UI/Battle/Shaders/UIFxScreen.shader`** — `"IdleGame/UI/FxScreen"`. This is the soft-add **screen** blend for full-field light flashes.
- `Blend OneMinusDstColor One`.
- frag: `rgb = tex.rgb * tex.a * IN.color.rgb * IN.color.a`, `a = 0`.
- It has the same clip-rect handling: it multiplies rgb by the clip factor.

**Materials.** `AttackFxBuilder` creates them like `WhiteFlashMaterial()` does, and null-checks the shader. On failure it falls back to the default UI material and logs a warning.

| Material | Assigned to |
|---|---|
| `Sprites/Battle/UIFxPremul.mat` | the particle template (and so every pooled particle, upper and ground), `_lampRing`, `_lampPillar` |
| `Sprites/Battle/UIFxScreen.mat` | assigned at runtime by BattleFx to `_fieldFlash` and `_screenFlash` for light flashes |
| `UIWhiteFlash.mat` (exists) | bound to BattleFx `_flashMaterial` for the silhouette pool |

**Known limitations**
- A parent CanvasGroup with alpha < 1 does not fade the additive share. No battle ancestor uses one today; keep it that way.
- The project uses gamma colour space, so additive values are perceptual. That is fine.

### 3.2 Layers and sort order [core: BattleSceneBuilder]
The stack inside ShakeRoot, from bottom to top. Bold entries are new or changed.
```
GroundPlane (lamp ring) < Ambient < Pads < GroundFx[sub-canvas: backing, ground rings, decals, pools, telegraphs]
< ShopBackLayer < Actors < Ambience[sub-canvas]
< **FieldDim** [new Image + sub-canvas, stretched -24 like FieldFlash, colour Dim, alpha 0, disabled]
< Fx[sub-canvas: particles in spawn order; silhouette pool S → SetAsLastSibling when active]
< Edge < FieldFlash < Numbers[sub-canvas: numbers, callout text] < Callouts
```
- **FieldDim is under Fx.** Dims darken the world (backdrop, actors, weather) while the effects and numbers stay bright. This is the spotlight principle.
- **Spawn order = draw order.** `Spawn()` calls `p.Rect.SetAsLastSibling()` on both the upper and ground layers. Every recipe therefore lists its rows bottom to top, and owners spawn in that order.
- **Silhouette pool:** 4 Images (UIWhiteFlash material, `preserveAspect = true`, raycast off, disabled) created by `BattleFx.Prepare` under `_layer`.
  - When active they copy the body Graphic's world position, rotation, pivot, `rect.size`, `lossyScale` (÷ the layer's `lossyScale`) and sprite every frame. There is no allocation; use a prebuilt `Vector3[4]` if corners are needed.
  - They are made last sibling on activation: misprint pink, then blue, then cream.
- `FieldFlash` and `ScreenFlash` keep their positions in the stack.

### 3.3 Particle system extensions [core: BattleFx.cs]
**New `Particle` fields** (all reset in `Spawn`):

| Field | Default | Meaning |
|---|---|---|
| `float Add` | 0 | additive weight |
| `float Hold, Settle, EndScale` | — | for `SizeCurve.Impact` |
| `float Drag, Gravity` | — | for `Motion.Streak` |
| `float Warp` | 0 | progress warp for `Motion.Shot`: k' = k·(1 − Warp + Warp·k); 0.25 for guardian shots |
| `byte FadeKind` | Smooth | Smooth (as today) or InQuad |
| `Sprite[] Frames; float FrameRate` | — | P2 flipbook; the array reference comes from JuiceArt, so no allocation |

**`SizeCurve.Impact`** is age-based:
```
a = Age; life = Life
size = a < Hold                 ? Size0                                    // Size0 = settled × FirstFrame
     : a < Hold + Settle        ? lerp(Size0, Size1, OutCubic((a-Hold)/Settle))   // Size1 = settled
     : EndScale < 1 ? lerp(Size1, Size1*EndScale, InQuad(u)) : lerp(Size1, Size1*EndScale, OutQuart(u))
       where u = (a - Hold - Settle) / max(0.001, life - Hold - Settle)
```

**`Motion.Streak`.** P0 = start, P1 = v0 (px/s), with `Drag` (1/s) and `Gravity` (px/s², downward).
- Position: x(t) = P0 + v0·(1 − e^(−d·t))/d + (0, −½·g·t²).
- Velocity: v(t) = v0·e^(−d·t) + (0, −g·t).
- Rotation is aligned to v(t).
- Width = Size × (1 + |v|/600); height = `Height0` → `Height1`.

**FadeKind InQuad:** fadeOut = 1 − InQuad(InverseLerp(fadeStart, 1, k)).

**`Spawn()`** keeps its signature. New fields get defaults, so every existing call site behaves as today until its owner opts in.

**Rules for time:**
- Use `_clock` for the FX clock and the new `_realClock` (the sum of real dt) for real time.
- **Never use `Time.time` inside BattleFx.** Gallery capture drives `Advance()` directly, so `Time.time` does not move during capture.
  - `KillCoin`'s `Time.time` must move to `_realClock` (enemy).
- **Delayed screen cues** (kick, zoom, light flash, dim, impact frame, haptic with a delay): core keeps a 16-slot `ScreenCue` ring buffer, fired when `_clock` reaches the cue time. This generalises today's `_critTraumaTimer` and `_bossMomentTimer`.

### 3.4 Budgets and pools [core]
**Pools**

| Pool | Capacity | Notes |
|---|---|---|
| Upper particles | **440** (builder `_particleCapacity` 340 → 440) | |
| Ground particles | **96** (48 → 96) | |
| Numbers | 48 (unchanged) | 2 slots are reserved for callouts |
| Silhouettes | 4 | |

**Budgets:** `HitFeel.ParticleBudget(q)` gives 420 / 240 / 130, with a minimum of 60.

| Function | High | Medium | Low |
|---|---|---|---|
| `HitFeel.GroundBudget` | 96 | 64 | 32 |
| `HitFeel.MaxNumbers` | 24 | 16 | 10 |
| `HitFeel.HaloCap` | 6 | 4 | 2 |
| `HitFeel.DecalCap` | 12 | 6 | 0 |
| `HitFeel.FrameSpawnCap` | 48 | 36 | 24 |

`GameSettings.ParticleBudget` (Core, not ours) stays as it is. BattleFx stops using it. The SPEC4 P4-9 PerfProbe must read `BattleFx` budgets.

**Why High goes up (340 → 420)**
- The new layering adds about +6 short-lived upper particles per hit (core 50 ms, ring 160 ms, +2–4 streaks about 170 ms). That is about +0.9 particle-seconds per hit on the old ~3.5, or **+25 %**.
- Kills add confetti, about +5 particle-seconds per kill.
- Boss kill needs about 75 at once and the lamp needs 60–110.
- The governor caps peaks. 420 keeps the same headroom ratio as today.
- **Fallback:** if PerfProbe shows `Canvas.BuildBatch` average > 0.6 ms (desktop proxy gate) or more than 2 ms on the reference Android, set High to 380 with one constant.

**Drop order when the budget is full** (unchanged): Sparkle → Extra → Trail → Core. New layers map to tiers like this:

| Tier | Layers |
|---|---|
| Sparkle | streaks, sparkles, confetti, twinkles, shed motes |
| Extra | ink backing, lineage extras, decals, rings on normal hits |
| Trail | glow under the shot, afterimages |
| Core | impact art, core stars, crit/kill/boss hero layers, rings on crit/kill/boss, numbers |

### 3.5 Zero allocations and performance [everyone]
**Per-frame code must not allocate.**
- No LINQ, closures, boxing, string concat or `new` of reference types.
- Callout and streak strings are precomputed.
- Primitives take structs, enums and preallocated arrays only.
- Confetti sets are static readonly arrays built once in `Prepare`.

**Glyph pre-warm** (show). At `PrepareNumbers`, call `TryAddCharacters` once on the number font and its Bold weight face (`font.fontWeightTable[7].regularTypeface`) for:
- `0123456789,.+-%★♥A–Z`;
- `막음깨짐연속수선반짝대눈부셔요한꺼번에`.

This avoids dynamic-atlas hitches in battle. The one-time allocation is fine.

**Verification (core, FxGallery)**
- The `busy_20` entry runs 60 warm-up frames.
- Then it sums `System.GC.GetAllocatedBytesForCurrentThread()` around each `fx.Advance` over 120 frames.
- It logs `[Verify] fx_alloc_bytes=N`. **N must be 0.**

**Device targets** (from SPEC4/ATK-06; checked by SPEC4 P4-9 PerfProbe):
- 2× with 20 enemies at ≥ 55 fps on a mid-range Android.
- BuildBatch ≤ 2 ms on device.
- Desktop proxy: BuildBatch ≤ 0.6 ms average.

### 3.6 API contract [core lands this in Wave 0; everyone codes against it]
```csharp
namespace IdleGame.UI
{
    // ───── HitFeel.cs (core) ─────
    public readonly struct TierFeel
    {
        public readonly float FirstFrame, Hold, Settle, Life, Core, CoreLife, Backing, BackingSize;
        public readonly int Rings, Streaks;
        public readonly float StreakCone, StreakSpeedMin, StreakSpeedMax;
        public readonly float FlashAlpha, FlashHold, FlashLife, SquashX, SquashY, Knockback, Freeze, Shiver, NumberScale;
        /* ctor */
    }
    public enum KickChannel : byte { Crit, Snipe, Shop, Kill, Skill, Shield, Siege }
    public enum ConfettiSet : byte { Dust, Moth, Metal, Shadow, Celebration, Boss }
    public static partial class HitFeel   // keep existing members (values per §2), add:
    {
        public static TierFeel Feel(HitTier tier);
        public static TierFeel ForBoss(in TierFeel f); public static TierFeel ForEcho(in TierFeel f); public static TierFeel ForGuarded(in TierFeel f);
        public const float KnockOut = 0.04f, KnockReturn = 0.12f, FreezeBudget = 0.15f, FreezeWindow = 0.5f;
        public const float KillFlashAlpha = 1f, KillShiver = 4f, KillSquashX = 1.30f, KillSquashY = 0.72f, KillPop = 0.04f;
        public const float KillBurstSize = 200f, BossBurstSize = 420f, ImpactTowardAttacker = 12f;
        public const float SkillTraumaPerSecond = 0.5f, ShopShieldBreakTrauma = 0.35f, BossEnrageTrauma = 0.45f;
        public const float KickCrit = 3f, KickBossCrit = 2f, KickSnipe = 2f, KickShop = 2f, KickSiege = 4f, KickKill = 2f, KickSkill = 2f, KickShield = 2f, KickMax = 4f;
        public const float ZoomDefault = 1.02f, ZoomBossKill = 1.03f, ZoomLamp = 1.035f, ZoomStageClear = 1.015f, ZoomMultiKill = 1.012f;
        public const float GlobalStopInterval = 1.5f, FieldFlashInterval = 1.0f, ZoomInterval = 0.8f;
        public const float DensityWindow = 0.25f; public const int DensityMini = 8, DensityLean = 16;
        public const float FullImpactInterval = 0.09f, RhythmChainGap = 0.6f; public const int RhythmEvery = 3;
        public const float BigBurstWindow = 0.1f; public const int BigBurstsPerWindow = 2;
        public const float KillBurstWindow = 0.2f; public const int KillBurstsPerWindow = 3;
        public const float StreakGap = 1.2f, MultiKillWindow = 0.25f;
        public static int ParticleBudget(FxQuality q); public static int GroundBudget(FxQuality q); public static int MaxNumbers(FxQuality q);
        public static int HaloCap(FxQuality q); public static int DecalCap(FxQuality q); public static int FrameSpawnCap(FxQuality q);
        public static float KickInterval(KickChannel c);
    }
    public static class JuiceEase { OutQuad, InQuad, OutQuart, OutExpo, InBack, InOutCubic (float→float);
        public static float SpringDisplacement(float t, float hz, float zeta);   // e^(−ζωt)·cos(ω_d t)
        public static float ImpactSize(float age, float life, float hold, float settle, float size0, float size1, float endScale); }
    public static class JuiceColors { CoreCream #FFF8E8, Ink #2D2458, Dim #1E1640, MisprintPink = UITheme.DustyPink, MisprintBlue = UITheme.SoftBlue }
    public static class JuiceLayout { public const float TopHud = 120f; public const float SideMargin = 90f; public static readonly Vector2 CalloutOffset = new(220f, 70f); }
    public struct FxKick { public void Add(Vector2 dir, float px); public Vector2 Tick(float dt); public void Clear(); }   // pure C#, testable
    public struct FxZoom { public void Punch(float peak); public float Tick(float dt); public void Clear(); }
    // TraumaShake: MaxOffset 30, MaxRotation 1.0, Decay 1.6 + public void AddSkill(float amount, Vector2 direction) (0.5/s channel)

    // ───── BattleFx.Juice.cs (new, core) ─────
    public struct SkillHitFx { public Vector2 Point; public EnemyView Target; public Color Main, Accent; public SkillVfxStyle Style;
        public VfxMotif Shard; public Vector2 Direction; public float Delay; public bool Boss, Tick; }
    public partial class BattleFx
    {
        // settings / layout (read-only)
        public FxQuality Quality { get; }   public bool ReduceFlashOn { get; }   public static bool ReduceFlashNow { get; }
        public float ShakeScaleNow { get; } public float SafeTop { get; }       // FieldTop − JuiceLayout.TopHud
        public JuiceArt Juice { get; }      public float Clock { get; }         public float RealClock { get; }
        // primitives (index or −1; all respect budget tiers, quality, ReduceFlash; sprites fall back internally)
        public int SpawnFx(Sprite s, Color c, Vector2 at, float size0, float size1, float life, float delay, float add = 0f, bool ground = false);
        public int ImpactArt(Sprite s, Color c, Vector2 at, float settled, in TierFeel f, float life, float delay, float endScale = 0.92f, float add = 0f);
        public int CoreStar(Vector2 at, Color tint, float size0, float size1, float life, float delay, float intensity = 1f);
        public int Halo(Vector2 at, Color tint, float size, float life, float delay, float intensity);
        public int InkBacking(Vector2 at, float size, float alpha, float life, float delay);
        public int Shockwave(Vector2 at, Color tint, float from, float to, float life, float delay, float add = 0.85f, float intensity = 0.8f,
                             float aspect = 1f, bool ground = false, Sprite sprite = null);
        public void Streaks(Vector2 at, Vector2 dir, int count, float coneDeg, float speedMin, float speedMax, Color tint, float delay,
                            float length = 22f, float thickness = 5f, float add = 1f, EnemyView anchor = null, Sprite sprite = null);
        public void Confetti(Vector2 at, ConfettiSet set, int count, float power, float delay);
        public void Twinkles(Vector2 at, float spread, int count, Color tint, float delay);
        public int Decal(Vector2 groundAt, Sprite s, Color tint, float size, float delay);
        public void AnchorFx(int index, EnemyView enemy);      // public wrapper of AnchorTo
        // screen (delay = FX-clock seconds until the moment; durations real time)
        public void Kick(Vector2 dir, float px, KickChannel channel, float delay = 0f);
        public void ZoomPunch(float peak, Vector2 pivotField, float delay = 0f, bool force = false);
        public void FieldDim(float alpha, float fadeIn, float hold, float fadeOut, float delay = 0f);
        public void LightFlash(Color color, float intensity, float duration, float delay = 0f, bool force = false);
        public void ImpactFrame(Graphic body, float delay, bool misprint, bool force = false);
        public void Misprint(Graphic body, float delay, float offsetPx, float life, float alpha);
        public void AddSkillTrauma(float amount, Vector2 direction = default);
        public void Haptic(HapticKind kind, float delay = 0f);
        // governor
        public bool TryFullImpact(int targetId, float delay); public int Density(float delay); public void NoteHit(float delay);
        public float BigBurstDelay(float delay); public bool TryKillBurst(float delay); public int KillStreak { get; }
        public Vector2 ClampToSafe(Vector2 at, float radius);
        // seams: public entry → core work → partial hook (unimplemented hooks compile away)
        public void NoteKill(int enemyId, Vector2 at, float delay, bool boss, HitTier byTier);   // ally calls; show implements the hook
        public void SkillHit(in SkillHitFx r);                                                   // ally calls; core base; show adds flavour
        partial void OnKillNoted(int enemyId, Vector2 at, float delay, bool boss, HitTier byTier);
        partial void OnSkillHit(SkillHitFx r);
    }
}
```

**Wave-0 API-only pre-edits in other owners' files.** Core makes these before Wave 1 starts. Each is a compile stub; after Wave 0 the file belongs to its owner, who replaces the body.

| File (owner after Wave 0) | Stub |
|---|---|
| `BattleFx.Numbers.cs` (show) | Append `Skill, Lamp` to `DamageNumberKind`. Add cases:<ul><li>`NumberColor`: Skill → `UITheme.Lavender`, Lamp → `UITheme.Lamp` (not Gold, so the test passes)</li><li>`NumberScale`: Skill 1.2, Lamp 1.1</li><li>`MergeWindowOf`: Skill 0.25, Lamp 0.5</li><li>`Format`: Int</li></ul>Add an optional parameter `Color tint = default` to `DamageNumber(...)` (ignored for now). |
| `EnemyView.cs` (enemy) | `public Graphic Body => _sprite != null && _sprite.enabled ? _sprite : _blob;` · `public HitTier KilledBy { get; private set; }` · `public void MarkKilled(float hold, HitTier byTier) { KilledBy = byTier; MarkKilled(hold); }` · `public void QueueImpact(float delay, HitTier tier, Vector2 direction, Color accent, bool allowFreeze = true) => QueueImpact(delay, tier, direction);` |
| `EnemyViewPool.cs` (enemy) | `public void MarkKilled(int id, float hold, HitTier byTier)` overload |
| `BattleFx.Enemy.cs` (enemy) | `EnemyDeathRequest`: `public Graphic Body; public HitTier KilledBy;` |
| `GuardianSlotView.cs` (ally) | `public void PlayAttack(Vector2 targetFieldPosition, float windup) => PlayAttack(targetFieldPosition);` · `public void Celebrate() { }` |

### 3.7 JuiceArt, the builder and import rules [core]
**`Scripts/UI/Battle/JuiceArt.cs`** (new MonoBehaviour)
- Serialized fields:
  - `LineageJuice[] _lineages`, where each entry is `{ string lineageId; Sprite impact; Sprite crit; Sprite muzzle; }`;
  - `Sprite[] _killBlooms` (by `EnemyDeathFamily`);
  - `Sprite[] _decals` (by `JuiceDecal`: Petal, Puddle, Thread, Buttons, Glitter, Heart);
  - single sprites: `CoreStar`, `GlowCore`, `Shockwave`, `StarRing`, `Streak`, `SparkleCluster`, `Speedlines`, `FlareCross`, `SlashArc`, `Afterimage`, `LampWaveRing`, `LightPillar`, `CritStarBadge`, `ComboPlate`, `HeroBossKill`, `HeroLampBloom`, `HeroStageClear`;
  - `Sprite[] _lightBurstFrames` (P2).
- Getters return **null** when art is missing:
  - `Impact(id)`, `Crit(id)`, `Muzzle(id)`, `KillBloom(family)`, `Decal(kind)`;
  - a property for each single sprite.
- Core primitives fall back internally:

| Missing | Fallback |
|---|---|
| CoreStar | sparkle |
| GlowCore | glow_soft |
| Shockwave / StarRing | ring_glow |
| Streak / Afterimage | trail |
| SparkleCluster | fx_mote_twinkle |
| Speedlines, FlareCross | skip the layer |

- For lineage, family, hero and plate art, owners use the fallbacks named in the recipes.

**`Editor/Builders/JuiceArtBuilder.cs`** (new)
- `public static JuiceArt Build(RectTransform root)` loads every file in §5 by exact path through `ArtLib.Load("FX/Juice/…")` and `ArtLib.Load("VFX/Juice/…")`.
- It logs one `[JuiceArt] missing: a, b, …` line.
- `BattleSceneBuilder.BuildFx` binds `_juice`, `_fieldDim`, `_flashMaterial`, `_premulMaterial` and `_screenMaterial`.
- **Art added later needs a scene rebuild** (BuildAll; fx_capture does this on fx_proj).

**Import rules** (`Editor/ArtImportPostprocessor.cs`, core; bump `GetVersion()` to 6):

| Folder or pattern | Rule |
|---|---|
| `Art/FX/Juice/*` | already Glow kind: uncompressed, mips (box), Clamp, FullRect |
| `Art/VFX/Juice/*` | already Vfx kind: high-quality compression, mips (trilinear), max 512 |
| `hero_*` | max **1024** (Android override ASTC 4×4) |
| `fb_*_4x4` | max 1024 (P2); Multiple sprites in a 4×4 grid |
| `plate_combo_9s` | the existing NineSlice rule with `BorderOverrides["plate_combo_9s"] = (150, 40, 150, 40)` |

**Atlases (SPEC4 §3.3):**
- add `Art/VFX/Juice/*` to `Atlas_BattleFx`;
- exclude `Art/FX/Juice/*` (light gradients, same reason Art/FX is excluded) and `hero_*`.

---

## 4. Damage numbers v2 [show; the material values are core because `CreateOutlineMaterial` lives in BattleFx.cs]

### 4.1 Font, material and template
**Font**
- Keep the template's `bold:true` (`FontStyles.Bold`).
- With UX M27, TMP renders it with the real **NotoSansKR-Bold SDF** through the Regular font's weight table (IdleProjectSetup `EnsureBoldWeight`). `TMP_Settings.matchMaterialPreset` copies our runtime outline material to the Bold atlas.
- **Do not swap the font asset.** Verify true-bold numbers in the capture (UX-SPEC asks for that too).
- If the Bold SDF is missing, fake bold still works.

**Size**
- The show sets `_numberTemplate.fontSize = 46` and `rectTransform.sizeDelta = (300, 80)` in `PrepareNumbers`, before instantiating the pool. The builder stays as it is.
- Kind scales are applied through `localScale` (as today).

**Material** (core, in `CreateOutlineMaterial`):

| Property | Value |
|---|---|
| `OUTLINE_ON` width | 0.28 |
| Outline colour | Ink #2D2458 |
| `_FaceDilate` | 0.15 |
| `UNDERLAY_ON` | `_UnderlayColor` #140D2A α 0.6, `_UnderlayOffsetX` 0, `_UnderlayOffsetY` −0.55, `_UnderlayDilate` 0.2, `_UnderlaySoftness` 0.35 |

Then call `ShaderUtilities.UpdateShaderRatios`. If the underlay clips with the dynamic SDF padding (6), reduce the offset to −0.4.

**Gradient**
- `enableVertexGradient = true`, with `colorGradient = new VertexGradient(top, top, bottom, bottom)` per kind (no allocation).
- Keep `Text.color` white and fade through `alpha`. If a capture shows that the vertex gradient breaks fades, fall back to the solid `NumberColor`.
- **`NumberColor(kind)` keeps today's base colours**, so the `NumberColors_GoldIsCritOnly` test stays green. The gradient is a separate `NumberGradient(kind, tint)`.

### 4.2 Kinds
Every number spawns **+33 ms after its impact** (`NumberLag = 0.033f` is added to the delay; not for Heal, ShopDamage or Text).

| Kind | Text | pt (scale × 46) | Gradient top → bottom | Motion (scale keyframes, ms) | Rise / life | Merge |
|---|---|---|---|---|---|---|
| Normal | 1,234 | 46 (1.0) | FFF8E8 → F4DEC4 | 0:0.55 → 70:1.12 (OutQuad) → 120:1.0 (InOutSine) | 64 px OutCubic / 0.75 s; fade over the last 30 % | 0.12 s (0.25 when busy) |
| Heavy | 1,234 | 53 (1.15) | FFF8E8 → F9CFB4 | 0:0.5 → 75:1.18 → 130:1.0 | 70 / 0.80 | 0.12 |
| **Crit** | ★1,234 | **74 (1.6)** | FFE9A8 → F2C46B | **slam:** 0:1.8 (squash X 1.2 / Y 0.85) → 70:0.92 (X/Y 1, OutCubic) → 140:1.0; **wiggle** 7°·sin(2π·t/110)·(1 − t/220) for t < 220; **badge:** `crit_star_badge` on layer F behind the number, 90 → 150 px over 200 ms (OutCubic), Add 0.5, Gold, then a 150 ms fade; **gloss:** `vfx_gloss_shine` 60 px, Add 0.6, sweeps from the left edge to the right edge of the number over 160 ms at +90 | hold 280 ms (rising 18 px), then +90 px / 1.15 s | never |
| Boss | 1,234 | 55 (1.2) | FFF4DC → D9C8F5 | as Heavy | 70 / 0.85, drifting outward | 0.12 |
| Skill | 1,234 | 55 (1.2) | lerp(tint, Cream, 0.25) → tint × 0.85 (tint = caster lineage Accent) | as Normal | 64 / 0.80 | 0.25 |
| Mark | ♥1,234 | 46 (1.0) | F7C1D4 → E58BAE | as Normal | 64 / 0.75 | 0.12 |
| Dot | 1,234 | 39 (0.85) | FFD29A → FFB86B | fade in over 80 ms, no pop | 40 / 0.70 | 0.5 |
| Heal | +1,234 | 41 (0.9) | D6F7E5 → 8FE3B0 | 0:0.7 → 80:1.05 → 130:1.0 | 70 / 0.85 | 0.5 |
| Block | 막음 1,234 | 39 (0.85) | E2F1FF → 93C8F0 | 0:0.8 → 80:1.0 + x-shake ±2 px over 120 ms | 40 / 0.70 | 0.12 |
| ShopDamage | -1,234 | 46 (1.0) | FFC4CB → D9566E | 0:1.4 → 100:1.0 + x-shake ±3 px over 120 ms | 40 / 0.80 | 0.12 (shop key) |
| Lamp | 1,234 | 51 (1.1) | FFF1C9 → FFD98A | 0:0.6 → 70:1.12 → 120:1.0 | 70 / 0.85 | 0.5 |

**Precedence** (SPEC4): Crit > Mark > Skill > Lamp > Boss > Heavy > Normal. Dot applies when the tier is Tick. Ally's `NumberKind` implements it.

**Merged re-pop:** bump 1.1 + 0.03 × min(chain, 5), capped at 1.25 (E6). Rhythm accents use 1.2.

**Killing blow** (`OnKillNoted`): the latest alive number of that target, if spawned within 0.3 s, gets scale ×1.15, and when it shows, 3 `fx_mote_twinkle` (Add 0.8, 300 ms) appear around it.

**Placement**
- Drift outward from the shop centre by 14 px, alternating ±16 px for consecutive numbers on the same target (index parity).
- **Top clamp:**
  - if spawnY + rise > SafeTop − 10, then rise = max(16, SafeTop − 10 − spawnY);
  - spawnY ≤ SafeTop − 26.

**Visible caps:** 24 / 16 / 10 (`HitFeel.MaxNumbers`).
- Drop order: Dot < Normal < Mark < Heal < Heavy < Skill < Lamp < Boss < Block < ShopDamage < Crit.
- Crit and ShopDamage always show.
- Among equal ranks, drop the smallest scale first, then the oldest.

**Callout text slots** (E11) use the same pool, with a "callout" flag so they are never merged or dropped.

**The DamageNumbers setting:** All / Crit only (hides Normal, Heavy, Boss, Skill, Mark, Dot, Lamp) / Off. That is today's rule plus the new kinds.

---

## 5. New art (Higgsfield) [artist]

### 5.1 Budget and method
**Budget:** **220 credits.** The balance on 2026-11-04 was 530.38.
- Model `gpt_image_2_5`, `quality: "max"`, `resolution: "1k"`, variant default (`flare`).
- Assumed cost: about 5 credits per image.
- **38 images = about 190 credits**, plus a **30-credit re-roll reserve** (6 re-rolls).

**Start with one image.**
1. Generate `fx_core_star` alone.
2. Check its real credit cost and the quality of the black-key result.
3. Then batch P0 (`generate_image_batch`), review, re-roll from the reserve, then P1, then P2.

If the real price is higher, cut from the bottom of the list (P2, then the end of P1). Never cut P0.

**One asset per image.**
- The only exceptions are the two sheets of small, non-prominent pieces: the 7 muzzles and the 6 decals.
- They are split into separate files by alpha islands, so code still gets one file per asset.

**Two production classes**

| Class | Assets | How to generate | Rendering |
|---|---|---|---|
| **S** (painted sprites: impacts, crits, kill blooms, heroes, badge, plate, decals) | painted | `background: "transparent"` | alpha blended |
| **L** (pure light: cores, glows, rings, streaks, flares, speedlines, pillars, muzzles, flipbook) | light | `background: "opaque"` on a pure-black prompt, then **black-to-alpha**: α = max(r, g, b), rgb = rgb / α | additive (Add ≥ 0.5) |

**Why L-class deviates from "transparent for everything":**
- Transparent mode tends to cut soft glows into hard stickers with fringes.
- The luminance key is the standard way to make additive textures and gives exact soft falloff.
- The credit cost is the same.
- If the orchestrator insists, L-class can be generated transparent. The code path does not change.

### 5.2 Shared prompt prefixes
**STYLE_S:**
> 2D mobile game VFX sprite for the collectible RPG 'LOST & LOVELY', a soft storybook fairy-tale night world where forgotten objects come alive. Game-ready: clean readable shapes, medium-weight clear outer line in a deeper shade of each colour (never pure black), soft cel shading with gentle gradients, warm dreamy light, cute but slightly strange. Night-pastel palette: lavender #C4B2F2, dusty pink #E9A6C6, soft blue #93C8F0, cream #FFF4DC, restrained gold #F2C46B accents. One single effect, centered, filling about 85% of the canvas with nothing cropped, isolated on a fully transparent background. No text, no letters, no numbers, no frame, no ground, no cast shadow, no photorealism, no 3D render look.

**STYLE_L:**
> Light-effect texture for an additive glow layer in a 2D storybook fairy-tale mobile game ('LOST & LOVELY'). Pure glowing light drawn on a solid pure black #000000 background. Simple clean storybook shape, bright white-hot core with a smooth soft falloff into a gentle coloured glow; no outlines, no dark areas inside the shape, no texture noise, no other objects. Centered, nothing touches the canvas edges (keep at least a 6% black margin). No text, no letters, no frame.

Each prompt in the JSON lines is the prefix plus `SUBJECT: …`. The full text is written out in `FX-JUICE-ART.jsonl`.

### 5.3 The list
Paths are under `D:/UnityProject/unity_idle/Assets/_Project/Art/`. The credits column is an estimate.

| # | id | Pri | Cl | File | Final px | Aspect | Cr | Used by (fallback) |
|---|---|---|---|---|---|---|---|---|
| 1 | fx_core_star | P0 | L | FX/Juice/fx_core_star.png | 512×512 | 1:1 | 5 | all core stars (sparkle) |
| 2 | fx_glow_core | P0 | L | FX/Juice/fx_glow_core.png | 256×256 | 1:1 | 5 | halos, shot glow, pools, memory mote (glow_soft) |
| 3 | fx_shockwave | P0 | L | FX/Juice/fx_shockwave.png | 512×512 | 1:1 | 5 | all rings (ring_glow) |
| 4 | fx_streak | P0 | L | FX/Juice/fx_streak.png | 256×64 | 21:9, crop | 5 | streaks, sparks, thread whip (trail) |
| 5 | fx_sparkle_cluster | P0 | L | FX/Juice/fx_sparkle_cluster.png | 256×256 | 1:1 | 5 | twinkles, callout sparkle (fx_mote_twinkle) |
| 6 | fx_speedlines | P0 | L | FX/Juice/fx_speedlines.png | 512×512 | 1:1 | 5 | crit, boss, lamp (skip) |
| 7 | crit_star_badge | P0 | S | VFX/Juice/crit_star_badge.png | 256×256 | 1:1 | 5 | crit number badge (motif_star in Gold) |
| 8–14 | atk_{L}_crit ×7 | P0 | S | VFX/Juice/atk_{jjakjjak,ttokdan,chalkak,mongdang,udukeoni,rouge,lalabell}_crit.png | 512×512 | 1:1 | 35 | E5 row 3 (atk_{L}_hit ×1.4 + vfx_star_burst) |
| 15–21 | atk_{L}_impact ×7 | P0 | S | VFX/Juice/atk_{…}_impact.png | 512×512 | 1:1 | 35 | E3 row 2, **open centre** (atk_{L}_hit) |
| 22–25 | kill_{family} ×4 | P0 | S | VFX/Juice/kill_{dust,moth,metal,shadow}.png | 512×512 | 1:1 | 20 | E10 row 4 (enemy_death_{family}) |
| 26 | hero_boss_kill | P0 | S | VFX/Juice/hero_boss_kill.png | 1024×1024 | 1:1 | 5 | E13 row 5 (vfx_star_burst + death art) |
| 27 | hero_lamp_bloom | P0 | L | FX/Juice/hero_lamp_bloom.png | 768×768 | 1:1 | 5 | E17 row 3 (glow_soft) |
| 28 | fx_slash_arc | P1 | L | FX/Juice/fx_slash_arc.png | 512×256 | 21:9, crop | 5 | echo melee, assault (atk_slash) |
| 29 | fx_flare_cross | P1 | L | FX/Juice/fx_flare_cross.png | 512×128 | 21:9, crop | 5 | crit, snipe, boss, lamp flares (skip) |
| 30 | fx_afterimage | P1 | L | FX/Juice/fx_afterimage.png | 256×64 | 21:9, crop | 5 | reserved for afterimage smears (the head sprite is used today) (trail) |
| 31 | fx_lamp_wave_ring | P1 | L | FX/Juice/fx_lamp_wave_ring.png | 512×512 | 1:1 | 5 | `_lampRing` (ring_glow) |
| 32 | fx_light_pillar | P1 | L | FX/Juice/fx_light_pillar.png | 256×1024 | 9:16, crop | 5 | `_lampPillar` (glow) |
| 33 | hero_stage_clear | P1 | S | VFX/Juice/hero_stage_clear.png | 1024×1024 | 1:1 | 5 | E18 row 2 (vfx_star_burst + fx_ring_thread) |
| 34 | plate_combo_9s | P1 | S | VFX/Juice/plate_combo_9s.png | 512×160 | 21:9, crop | 5 | E11 plate (text only) |
| 35 | muzzle sheet → 7 files | P1 | L | FX/Juice/muzzle_{jjakjjak,…,lalabell}.png | 256×256 each | 1:1 (3×3 grid) | 5 | E1 row 2 (fx_core_star in Accent) |
| 36 | decal sheet → 6 files | P1 | S | VFX/Juice/decal_{petal,puddle,thread,buttons,glitter,heart}.png | 128×128 each | 3:2 (3×2 grid) | 5 | E5/E10 decals (skip) |
| 37 | fx_star_ring | P2 | L | FX/Juice/fx_star_ring.png | 512×512 | 1:1 | 5 | crit ring 2 (fx_shockwave) |
| 38 | fb_light_burst_4x4 | P2 | L | VFX/Juice/fb_light_burst_4x4.png | 1024×1024 (16 cells of 256) | 1:1 | 5 | optional flipbook core for crit and kill (procedural) |

Total: 190 credits plus 30 in reserve = **220**.

**Reuse as-is.** These are already of equal quality:
- fx_confetti_{button, ribbon, star, thread}, fx_mote_{dust, petal, twinkle, firefly, raindrop}, fx_sparkle, fx_puff, fx_ring_thread, circle_soft (ink backing), rays.png (boss and lamp rays);
- motif_* (all 26), vfx_gloss_shine (number gloss), vfx_star_burst (fallback), vfx_sound_ring;
- atk_{L}_shot (projectiles), enemy_atk_* (muted strikes), enemy_hit_shop, enemy_death_* (fallbacks and family layers);
- glow_soft, ring_glow, sparkle, trail (fallbacks).

### 5.4 Post-processing and QA
The artist's working folder is `scratchpad/juice_art/` with `raw/`, `out/`, `preview/` and `jobs.txt` (`id → job id → credits`). Reuse:
- `scratchpad/attack_art/process.py` (`center` mode: trim, centre, 4 % margin, premultiplied Lanczos, 3 px edge fade);
- `scratchpad/polish_art/tutorial-fx/cut_sheet.py` and `fxtool.py` (split sheets by components; "halo" mode for L-class);
- `rays_proc.py`, if a radial fade is needed.

**New: `juice_art/black_to_alpha.py`** (L-class):
1. Load RGB as float.
2. α = max(r, g, b); then α = clip((α − 0.015) / 0.985, 0, 1) to kill the noise floor.
3. rgb = rgb / max(α, 1e-4), clipped.
4. Pixels with α < 2/255: set rgb to white (255, 255, 255) and α to 0, to avoid dark fringes when additive.
5. Trim to the α > 0.02 bounding box plus a 6 % margin.
6. Premultiplied resize to the final size.
7. 3 px edge fade.

Wide or tall L-class assets are cropped to their final aspect *before* resizing. Their direction convention is: streak, flare and afterimage point **right**; the slash goes lower-left → upper-right; the pillar points **up**.

**QA checks.** Any failure → re-roll from the reserve.
1. Alpha in the 3 px border is 0 (Clamp-safe).
2. Centred items: bbox centre within ±2 % of the canvas centre. Muzzles: the bright source sits on the canvas centre and points right.
3. **Open centre** (`atk_*_impact`): mean α inside the central disc (radius 20 % of the side) is ≤ 0.15.
4. L-class: max α ≥ 0.95 at the core; no fringe darker than the glow.
5. S-class: no white matte. Pixels with 8 < α < 128 must not be near-white unless the subject itself is white or cream.
6. **Composite previews** at in-game size: impacts at 132 px, crits at 200, kill blooms at 200, hero at 420–560, badge at 120. Composite over a crop of `Art/Battle/field.png` (lavender plaza) and over the same crop darkened 45 % (dim). L-class is composited additively. Save `juice_art/preview/sheet_{P0,P1,P2}.png`.
7. **Silhouette test:** the 7 crit arts in grayscale with a 4 px blur must stay distinguishable (SPEC4 §3.2 blind check).
8. Flipbook: frame centres within ±4 px, monotonic scale progression, no style drift. On failure, **discard** it; no code depends on it.

**Deliver:** copy finals to the exact paths above. Unity creates the `.meta` files. Do not edit code. Report `juice_art/report.json` (`id`, file, px, credits, QA pass/fail, re-rolls).

### 5.5 JSON lines
These are in `scratchpad/FX-JUICE-ART.jsonl`, one object per line, with full prompts. The orchestrator's return value repeats them.

---

## 6. Ownership and waves

### 6.1 Packages (no overlapping files)
**Do not edit:**
- `BattleView.cs`, `FxQualityMonitor.cs`, `EchoDeckView.cs` (UI/UX);
- `Scripts/UI/{UITheme, UIManager, UIPopup, UINav}.cs`, `Scripts/UI/Common/**`, `Main/**`, `Popups/**`, `Items/**`, `Meta/SleepModeView.cs`;
- `Editor/{UIKit, SceneParts, SceneBuildContext, IdleSceneBuilder, IdleProjectSetup}.cs`;
- `Scripts/Systems/**`, `Scripts/Core/**`, `Editor/Content/**`;
- every test except the two FX test files below (core only).

**Do not rename either:**
- the FxGallery keys of the 19 baseline probe entries;
- GuardianSlotView's private `_portrait`, `_fallback`, `_badge` and `_halo` (the probe hides them).

**Core (FX-core)**
- `Scripts/UI/Battle/BattleFx.cs`: Particle fields and curves, colour write, flashes, shake/kick/zoom application, `ReadSettings` budgets, the number material values in `CreateOutlineMaterial`, `Hit()`, `ShopDamaged`, `ShopHealed`, `Summon`, `EchoDissolve`, `LampBurst`, `LampHit`, `UpdateLampRing`, `BossKillMoment` / `FireBossKillMoment`, `ScreenFlash` / `FieldFlash`.
- `BattleFx.Juice.cs` (new).
- `HitFeel.cs`.
- `JuiceArt.cs` (new).
- `Shaders/UIFxPremul.shader` and `Shaders/UIFxScreen.shader` (new).
- `FxGalleryView.cs`.
- `Editor/FxGallery.cs`.
- `Editor/Builders/{AttackFxBuilder, EnemyFxBuilder, BattleSceneBuilder}.cs`. BattleSceneBuilder only for: FieldDim, materials, capacities, JuiceArt binding, `_flashMaterial`.
- `Editor/Builders/JuiceArtBuilder.cs` (new).
- `Editor/ArtImportPostprocessor.cs`.
- `Tests/EditMode/AttackFxCatalogTests.cs` and `EnemyFxCatalogTests.cs` (**sole editor**).
- Plus the Wave-0 stubs (§3.6).

**Ally**
- `BattleFx.Attack.cs`: E1, E2, E3, E4, E5 (`PromoteImpact`, `CritImpact`, lineage specials), E8 hit and slash, `ShieldRipple`, the `LineageExtra` tweaks, the mini variant.
- `AttackFxCatalog.cs`: lineage spark, shard, decal and ring data; windup ratio.
- `BattleView.Attack.cs`:
  - windup to `PlayAttack(target, W)`;
  - `NumberKind` precedence;
  - the InCast branch → `SkillHit`;
  - `NoteKill` + `MarkKilled(id, hold, byTier)` in `OnEnemyKilled`;
  - celebrations.
- `BattleUnitArt.cs` (only if needed).
- `GuardianSlotView.cs`: anticipation, release, recoil, spring, `Celebrate`.
- `EchoView.cs`: lunge and squash, hurt squash.

**Enemy**
- `BattleFx.Enemy.cs`: `EnemyStrike` asymmetry, `ShopImpact` v2, `EnemyDeath` v2 (all families, mini kill, kill kick), `BossDeathExtras` v2, `RememberMote` InOutCubic, `KillCoin` (use `_realClock`), `RewardShower` (E18), `EnemySpawn` boss ring, the `EnemyDeathRequest` fields.
- `EnemyFxCatalog.cs`: family ring colours, exit constants; `ShopStrikeTrauma` formula unchanged.
- `BattleView.Enemy.cs`: `RewardKill`, strike calls.
- `EnemyView.cs`: E9 in full, KillHold v2, exit times, HP ghost, `Body` / `KilledBy`, the `QueueImpact` accent and `allowFreeze`.
- `EnemyViewPool.cs`: passes `Body` and `KilledBy`.
- `ShopView.cs`: hit spring, lamp flicker.
- `DirectionRingView.cs` (only if needed).

**Show**
- `BattleFx.Numbers.cs`: §4, the callout text slots.
- `BattleFx.Skills.cs`: E7 cast styles, `OnSkillHit` hook, `SkillImpactAt` v2, `ShieldShatter` v2, `CritBurst` v2, `CastCharge` v2, `EnrageBurst`.
- `BattleFx.Pulse.cs`: E16.
- `BattleFx.Callouts.cs` (new): E11, the `OnKillNoted` hook, killing blow.
- `BattleView.Skills.cs`:
  - `OnCriticalHit`: kick and number;
  - `OnShieldBroken`: E15;
  - `OnSkillCast`: spotlight, LightFlash, skill trauma;
  - the enrage constant.
- `BattleView.Cast.cs`: stagger, big rules.
- `SkillVfxLibrary.cs`, `SkillVfxLook.cs`, `SkillCalloutView.cs`, `StageAmbienceView.cs` (only if needed).

**Artist:** `Art/FX/Juice/**`, `Art/VFX/Juice/**` and `scratchpad/juice_art/**`. No code.

### 6.2 Waves
| Wave | Who | Content | Exit gate |
|---|---|---|---|
| **0** (blocking, short) | core | §3.6 API with working bodies (primitives, governor, kick/zoom/dim/light flash/impact frame), Particle extensions (§3.3), shaders + materials, FieldDim + silhouettes + capacities in the builder, JuiceArt + builder (binds whatever art exists), budgets, TraumaShake retune, Wave-0 stubs, test edits (Appendix A) | `compile_check.py` → COMPILE OK; EditMode FX tests green; one `fx_capture -CodeOnly` run shows no regressions |
| **A** (from now, in parallel) | artist | §5 | `report.json` with every P0 asset passing QA |
| **1** (in parallel after Wave 0) | ally, enemy, show; core continues | ally E1–E6, E8 · enemy E9, E10, E12–E14, E18, E19 · show E7, E11, E15, E16, §4 · core E17, boss moment, Gallery v2 (§7.1), `SkillHit` base, `Hit()` | each owner: COMPILE OK + their gallery entries look right |
| **2** | core + FX director | integrate art (rebuild), full capture, metrics vs §0.3, tuning (constants only), acceptance §7 | §7 all green |

---

## 7. Acceptance

### 7.1 Capture protocol (core: FxGallery v2 + FxGalleryView)
**Running a capture**
- `fx_capture.ps1 -Out <name> [-CodeOnly]`.
- If `capture.log` says "씬에 FxGalleryView / BattleFx가 없습니다" on a fresh fx_proj, run again with `-CodeOnly`.
- If PowerShell hangs after Unity exits, the cause is the `Start-Process -Wait` / VBCSCompiler issue. Use the `-PassThru` + `WaitForExit()` pattern from `fxj/run_probe.ps1`.

**Before each entry**
- **Hide overlays** using the logic of `HideOverlays` in `fxj/FxjProbe.cs`.
- `Random.InitState(1234 + index)`.

**Outputs, all under `fx_out/<run>/fx/`**
1. `{key}.png` — 200×200 at arrival + 50 ms (as today).
2. `strips/{key}.png` — 12 crops of 300×300 around FocusPoint at −50, 0, 17, 33, 50, 83, 117, 167, 250, 400, 600 and 900 ms on the FX clock.
3. `full/{key}_{ms}.jpg` — the field band (fxLayer rect), clean plus −100, 0, 33, 100, 250, 500 and 900 ms. For screen-scale keys only:
   - `death_boss_*`, `lamp`, `stage_clear`, `multikill_5`, `kill_streak_10`, `busy_20`, `skill_vfx_star_burst`, `skill_vfx_candle_burst`;
   - and the settings variants.
4. `metrics.tsv`, one row per key:
   - max |ShakeRoot.anchoredPosition|;
   - max ShakeRoot scale;
   - max FieldDim alpha;
   - max light-flash intensity;
   - max live upper and ground particles;
   - max visible numbers;
   - `SafeTop`.
5. `[Verify] fx_alloc_bytes=N` from `busy_20` (§3.5).
6. **probe2-compatible frames** for the 19 baseline keys, written to `fx_out/<run>/probe2/` (same naming as the baseline). Then `fxj/strips.py` and `fxj/peaks.py` produce `strips_metrics.tsv` and `peaks.tsv` that are comparable with the baseline.

**New gallery entries (keys)**
- `kill_streak_10`: 10 kills across scarecrows over 1.0 s.
- `multikill_5`: 5 kills within 0.2 s.
- `stage_clear`: `ScreenFlash` + `RewardShower` + shop `LampBurst`.
- `busy_20`: 20 scarecrows, 5 guardians and 5 echoes attacking at 2× for 1.5 s, plus one area skill and one crit.
- `numbers_all`: every kind in a row.
- `crit_kill_{lineage}`: a crit that kills.
- `echo_melee_{lineage}`, if missing.

**Settings variants** (`BattleFx.DevOverride`; never PlayerPrefs):
- `lamp@reduce`, `death_boss_shadow@reduce`, `death_boss_shadow@shake0`, `busy_20@low`, `crit_jjakjjak@reduce`.

### 7.2 What the frames must show
**Normal hit (`guardian_*_t0`)**
- p000: a bright cream core plus the art at ×1.3, the largest frame.
- p017–p033: the art shrinking, 4 streaks outward on the exit side, and the enemy silhouette **white and visible** through the open centre.
- p050–p117: the enemy squashed wide, then rebounding; knocked back about 8 px.
- p167: the number popped above.
- p250: almost clean.

**Snipe:** a cross flare along the shot on p000; `metrics.tsv` shake max between 1.5 and 3 px (kick).

**Crit (`crit_*`)**
- p000: a gold halo, a cream core and a flare.
- p050: the second gold ring.
- Through p066: the enemy shivers in place, then is knocked back 16 px.
- The number slams in at about p033, ≥ 90 px tall at its peak, with the star badge behind it and a gloss at p117–p250.
- The lineage special is visible: X-stitch / buttons / keyhole / flame / splash / kiss / chord.

**Kill (`death_enemy_*`)**
- Hold frames: a white body squashing.
- Burst frame: the bloom at ×1.35 with a core and halo.
- +200 ms: ≥ 6 confetti pieces in the air plus a ground ring.
- +450–700 ms: the memory mote travelling to or reaching the shop door, with a glint.
- +900 ms: clean except the decals.

**Boss kill (`full/death_boss_shadow_*`)**
- 0 ms: the field dimmed (mean luma ≥ 25 % lower than clean), a cream boss silhouette, pink and blue misprint edges, and the hero burst frozen at its first frame.
- 100 ms: the dim gone and the screen-blend light up (mean luma ≥ 10 % above clean, luma std-dev ≥ 70 % of clean: no milky veil).
- 250–500 ms: 3 rings, confetti, ribbons and rays.
- Not more than 40 % of the burst clipped by the field edge or `SafeTop`.
- Metrics: zoom max 1.028–1.032; shake max 8–14 px.

**Lamp (`full/lamp_*`)**
- −100 ms: the dim, the shop glowing more strongly, and the sparks converging.
- 0–33 ms: the release core, flare, bloom and rays.
- 100–500 ms: the wave ring sweeping, gold stars on the enemies, merged numbers.
- 900 ms: the star rain.
- Changed area ≥ 45 %. Zoom max 1.033–1.037.

**Stage clear:** the hero burst behind the ribbon zone, confetti fountaining up and outward, coins, and no shake (shake max ≤ 0.1 px).

**`multikill_5` / `kill_streak_10`:** the plate and text at the top-left under the stage card, clear of the HUD in the real build. One callout per 2 s.

**`busy_20`**
- No frame changes more than 60 % of the field (no white-out).
- Every enemy is distinguishable.
- Live upper particles ≤ 420; with `@low`, ≤ 130.
- Numbers visible ≤ 24.
- `fx_alloc_bytes=0`.

**Enemy strikes (`strike_echo_*`):** stay muted. No near-white core; Δluma ≤ +10.

**Settings variants**
- `@reduce`: light flash ≤ 0.30 (boss 0.15, lamp 0.25); dims ≤ 0.20; no silhouette or misprint; core stars visibly softer.
- `@shake0`: ShakeRoot offset = 0 and scale = 1 at every frame.

### 7.3 Metric thresholds
These are measured with `fxj/strips.py` and `peaks.py` against the baseline: the §0.3 table.
- **Pass:** every row inside its target band.
- **Plus:** `peak_tag` ∈ {p000, p017} for the basic, echo and snipe keys; `visible_s` ≤ 0.45 for the basic keys.

### 7.4 Real build
The orchestrator's `verify.ps1` produces 540×960 shots `05_battle…11_battle_fx_5`. In them:
- hits read as light bursts ≥ 65 px wide (≥ 130 px at 1080);
- normal numbers ≥ 18 px tall (≥ 36 px at 1080) and visibly bolder, with an outline and shadow;
- no number clipped at the field top.

`[Verify] shake_off_max_field_offset=0.0` and `reduce_flash_max_alpha<=0.30` (SPEC4 §3.2.4) must still hold.

### 7.5 Tests and compile
- `python -I scratchpad/compile_check.py` → COMPILE OK after every wave.
- The EditMode FX tests pass after core's edits (Appendix A).
- No new warnings in the touched files (`compile_check_allwarn.py`).

---

## Appendix A. Test edits (core only: `AttackFxCatalogTests.cs`, `EnemyFxCatalogTests.cs`)
**Changes**
- `HitLadder_GrowsWithTier`:
  - Knockback: Normal 8, Heavy 12, Crit 16, (Crit, boss) 3, Tick 0.
  - TargetStop: Normal 0, Heavy 0.033, Crit 0.066, Skill 0.05.

**Still green without edits**
- `Trauma_IsClampedAndDecays` uses the `Decay` symbol.
- `Trauma_OffsetStaysInsideBudget` uses the `MaxOffset` and `MaxRotation` symbols.
- `ShopHits_AreCappedPerSecond`: 0.30 + 0.30 = 0.6 cap.
- `CritTrauma_OncePerInterval`: 0 = 0.
- `NumberColors_GoldIsCritOnly`: Skill is Lavender, Lamp is Lamp; Crit 1.6 and Boss 1.2 are unchanged.
- `EnemyFxCatalogTests`: they derive from the constants. `ExitTime` ordering is kept.

**New tests (pure C#)**
- `TierFeel_LadderMonotonic`: Normal < Heavy < Crit for FirstFrame, Core, Knockback, Streaks and FlashAlpha.
- `ImpactSize_PeaksOnFirstFrame`: size0 at age 0 and at hold, size1 at hold + settle, ≈ size1 × end at life.
- `Kick_DecaysAndCaps`: ≤ 4 px; ≈ 0 after 0.2 s; never sums.
- `Zoom_PeaksAndReturns`.
- `ShakeScaleZero_NoCameraMotion`: trauma + kick + zoom.
- `Budgets_PerQuality`: 420 / 240 / 130, numbers 24 / 16 / 10.
- `Governor_FullImpactEvery90ms`.
- `Governor_KillBurstsCapped`.
- `NumberGradient_CritIsGold`.
- `StreakMilestones`: 5, 10, 20, 40, 60, 80.
- `NumberPrecedence`: Crit > Mark > Skill > Lamp > Boss > Heavy > Normal.

Owners expose these pure functions as `public static`. Core writes the tests in Wave 2.

## Appendix B. Risks
- **Premultiplied particles and parent CanvasGroups** (§3.1): keep battle ancestors at alpha 1.
- **Pure-additive quads culled:** solved by the alpha floor plus `cullTransparentMesh = false`.
- **Additive on the light plaza clips to white:** keep core-star intensity ≤ 1, halos ≤ 0.35 and screen-blend flashes. ReduceFlash cuts all of it.
- **Ink backing reads as dirt:** it is a single constant (TierFeel Normal backing → 0). Decide in the Wave 2 review.
- **TMP vertex gradient vs fades, and underlay with SDF padding 6:** both have fallbacks in §4.1.
- **`SetAsLastSibling` per spawn:** cheap (sibling memmove), but Wave 2 checks batch counts in PerfProbe.
- **Bold numbers depend on the UX IdleProjectSetup weight table:** fake bold still works if it is missing.
- **Art lands after code:** every lookup is nullable with a fallback. Bind on a scene rebuild.

## Appendix C. Baseline references
`fx_out/baseline/`:
- `sheets/peak_frames.png`, `sheets/impact_zoom.png`, `sheets/real_build_battle.png`;
- `peaks.tsv`, `strips_metrics.tsv`;
- `probe2/probe.txt` (field band y 1106–1730 in capture space, Ppu 205, Squash 0.726, shop origin (−0.5, −63.5)).
