# PROGRESSION-SIM — growth math for LOST & LOVELY (SPEC4 CR-07 + R2 critic CR2-08)

2026-11-04 · phase 4b · project read-only · critic notes (PHASE4-ROADMAP v1.1) are in §11 and tagged `(CR4-nn)` in the text

- **Harness:** `phase4b/sim/`
- **Runs:** `phase4b/runs/`
- **Figures:** `phase4b/figures/`
- **Full tables:** `phase4b/analysis/report_tables.md`

---

## 0. Recommendation: one curve fix

**Add a second enemy-growth taper at 16-1 (stage 151): enemy HP ×1.02 per stage and enemy damage ×1.015 per stage.** Stages 1–150 are unchanged.

```
BattleConfig.stageGrowthTaperStage2 = 151    // new; 0 = off
BattleConfig.stageHpGrowthLate2     = 1.02f  // new; stages 151+ (1.07 before)
BattleConfig.stageDamageGrowthLate2 = 1.015f // new; stages 151+ (1.05 before)
```

R2-04 and R2-05 stay as specced:
- **R2-04 수선 비법:** 등급 ×1.02 per 25 levels; rows cost ×1.10 per level.
- **R2-05a 한 땀 돌파:** unchanged.

Rejected:
- **Taper after chapter 6.**
- **Larger 등급 multiplier** (×1.10).
- **Scheduled content as the curve.**

On both the shipped and the CR-07 economy, the first two collapse chapters 12–25 into 2–6 days. The third barely moves anything (§5).

**This fix is only correct together with two CR-07 tuning results.** Both are CR-07's own knobs or data, not alternative curve choices. On the shipped economy every candidate, this taper included, only makes the game faster.

| CR-07 tuning result | From → to | Where | Why (measured) |
|---|---|---|---|
| 수선점 단추/s growth | `goldPerSecondGrowth` 1.10 → **1.06** | `Data/GameConfig.asset` YAML (IdleDataBuilder keeps inspector values) + the C# default | Income ×1.10 per 수선점 level against cost ×1.12 is near-runaway compounding. 단추/s reaches 9e9 at the end of day 0 and 1.5e26 by day 30, so every 단추-priced axis is free |
| 수선 바구니 per-stage growth | `IdleChestConfig.stageGrowth` 1.02 → **1.0025** | `Editor/Content/MetaContent.IdleChest.cs` | The 별조각 line is 3/h × 1.02^(stage−1), which is ×19 at stage 150. Measured gems: 13k/week without R2 and 119k/week with R2, against SPEC4's 2,640/week |

**Time-to-clear per chapter with the full package.** Engaged player, 2 × 20 min per day, R2-04 + R2-05, median [min–max] of 12 seeds:

| chapters | first cleared on day | days per chapter | what drives it |
|---|---|---|---|
| 1–11 | 0.3 → 1.3 (ch10 after 34 min online) | minutes | Starter surplus and fast early levels. No curve or economy knob tested changes this (§6.3) |
| 12–16 | 1.9 · 3.1 · 4.6 · **6.6 [1.3–8.9] (ch15)** · 8.6 | 0.5–2.0 | **Soft walls that R2 solves.** Without R2 the same player stops at ch13–15 (stall from day 18–38) |
| 17–45 | 9.3 → 29.1 (ch20 10.6 · ch25 13.3 · ch30 16.9 · ch40 24.4) | 0.5–1.0 | Endless ladder after the taper; steady |
| 46–52 | 30.4 → 40.8 | 1.2–2.5 | Gradual hardening |
| ~55 | ceiling: first ≥ 7-day stall at stage 535–571, days 49–71 (median 57) | – | All P0 growth axes saturate. This is the cue for scheduled content (R2-36b), not a curve problem (§7.4) |

- **The 60-day player** (the R2 critic's yardstick) sits at stage **547** (ch55) and has not met a hard wall before ~day 49.
- **별조각 income:** 3.6k/week in week 4 and 3.9k/week in week 8, about 40 pulls/week, so a 60-pull pity about every 10 days.
- **Stage failures are back:** 93 in 90 days, where the shipped economy has 0, so 수선점 durability matters again.

![Highest stage cleared vs day](figures/fig1_stage_vs_day.png)

---

## 1. Headline findings (shipped constants, run on the real code)

1. **There is no wall at 65–70.**
   - The engaged player clears **chapters 1–5 in the first 14 minutes online** and stage 65–70 at **18–28 minutes on day 0**.
   - Every placed lineage is **Lv100 after 13–20 minutes online** (stage 51–65).
   - CR2-08's estimate was static: it scaled a calibrated need with a uniformly scaled starter team, shop HP included. In play the shop is a separate, uncapped, compounding axis, and on top of it come synergies, 6–7 lineages from ≈30–40 pulls on day 0, and a DPS line-up.
2. **The real wall is battle time, not defeat** (Fig. 5).
   - At the wall, 수선점 내구도 = 300 × 1.04^(Lv−1) is 4.6–5.5 × 10¹¹ at 수선점 Lv 540–545, which is **10⁷ times a boss_shadow hit**. No stage is lost after chapter ~8.
   - Stage battles have no time limit, and leaked enemies do not count as kills.
   - boss_shadow, on every odd stage, has `LampDrain`, which keeps the 불 밝히기 gauge empty and removes the lamp's %-HP floor.
   - Once DPS stops growing (Lv100, all forms, slow 공명), odd stages take 1,250–2,170 battle-seconds by 15-1…16-1. Even stages take 340–420 s. One 20-minute session at 2× is 2,400 s, and a cold start restarts the battle.
3. **Where it stops (shipped, no R2).**
   - DPS line-up: **stage 153 (16-3)** in 5 of 6 seeds (145 in the sixth), with the first ≥ 7-day stall on days 9–16.
   - The game's **AutoFormation**: **13-5 / 13-7 (stage 125–127) on days 5–9**. It ranks forms by `attack/interval×10 + hp`, so it fields tanky Defense forms.
4. **The 단추 engine compounds** (Fig. 4).
   - 수선점 payback (cost / Δincome) = 100 s × 1.0182^(Lv−1) / stage multiplier. It starts at 100 s.
   - 수선점 level: Lv 218 at the end of day 0, Lv 604 by day 30.
   - **92–96% of all 단추 comes from the 바구니's offline line**; stage clears are <0.01%.
   - CR-07's own band test fails by ~50× (60-minute 1× run, shop-first policy): the median purchase gap in chapter 1 is **0.5 s** against a 15–30 s target, and 2.0 s with same-tick bursts removed.
5. **Gems and pity.**
   - No R2: **13.4k 별조각/week**, ≈150 pulls, so a 60-pull pity every 2.8 days.
   - With R2: **119k/week** in week 4 and 133k in week 8, a pity several times a day, because the 바구니 별조각 line grows ×1.02 per stage.
   - SPEC4 §1.5 assumed ≈2,640/week.
6. **R2-04 as specced rides the 단추 engine.**
   - Rows cost ×1.10 per level, and 단추/s grows ×1.10 per 수선점 level, so row levels track the 수선점 level: 2,386 levels (등급 95, ×6.6 attack) on day 6.
   - Shipped + R2-04/05: **chapter 15 on day 2.1, chapter 25 on day 10.1**, then a stall at 271–275 (days 24–35).
   - CR2-08's "R2-04 cannot track" assumed 단추 ∝ 1.13^stage. That is only true after the CR-07 retune (§4).
7. **R2-05 alone barely helps.** Breaks are 기억편-gated. Shipped + R2-05 stalls at 159–165, and on the recommended curve breaks reach 13 by day 30.
8. **The 1% Mythic decides the early pace.** On the recommended package, ch15 falls on:
   - **day 1.6** with 라라벨 on day 0 (2/12 seeds);
   - **day 4.3** with her on days 1–7;
   - **day 7.3** when she comes later or never (8/12).

   Her lala_finale (snipe) is the strongest DPS form.
9. **PRG-01's recommended-power rating is uninformative.** TeamPower counts shop HP × 0.3, so on the shipped economy power/rec reaches 30–900,000× by chapter 12–13 ("여유" always). R2-04's need/rec 0.80–1.05 criterion cannot be checked with today's formula.

---

## 2. Method

### 2.1 The harness compiles the real game

`phase4b/sim/ProgSim.csproj` (.NET 8) compiles the project code **read-only**:
- `Scripts/Core`, `Data`, `Systems/**`, `Account`
- `Editor/Content/*` (CharacterContent + FormStats + SkillContent, SynergyContent, ItemContent, GachaContent, MetaContent) and `ArtLib`
- Unity stubs in `sim/stubs/UnityStub.cs`

Content is built the way `IdleDataBuilder` and `MetaDataBuilder` build it: 7 lineages, 29 forms, 25 synergies, 25 features, 30 guide quests, 19 missions, attendance, idle chest. `Game.Setup()` creates the same object graph as `GameBootstrap`. Every battle is the real `BattleSystem` + `BattleSimulator`.

**Patched copies.** Two files compile from copies made by `sim/tools/patch_sources.py`, which asserts its anchors:
- `BattleSystem.cs` gains hooks: R2-04 stats are added to the built `BattleModifiers.Team`, and 등급 becomes an `AttackMultiplier` and shop-HP factor.
- `BattleConfig.cs` gains the optional second taper.
- With hooks at default the patched curve equals the original (`ProgSim selftest`: 0 mismatches over stages 1–300).

**Modeled, because they are not in code yet:**
- **R2-04:** rows, opening chains, caps, 등급 and costs per IDLE_RESEARCH2 §R2-04 (`sim/src/R2Systems.cs`). 두 땀 뜨기 counts as +0.1% attack per level.
- **R2-05a:**
  - Cap 100 → 200 in 5 breaks.
  - Each break costs 기억편 40/60/80/100/120, 오르골 태엽 2/3/4, then 달빛 조각 1/2, and 단추 = 10× the next level.
  - Levels follow the real formula 50 × 1.09^(Lv−1), +8% per level. 추억 수놓기 is included.

### 2.2 The engaged player

**Sessions**
- 2 sessions per day, 08:00 and 21:00 KST, 20 minutes each; 2× speed from 1-6.
- **Cold start every session**: the battle restarts, so a stage must fit in one session (2,400 battle-seconds).
- Offline time feeds the 바구니 through the real `MetaSession.OnPause/OnResume`.

**Claims, through the systems' public API**
- 바구니 (free double, tickets), 출석부, mail, guide quests
- Journal daily + weekly via `ClaimAll` (chests included); free 빠른 수선 + tickets
- Expeditions: best zones for missing materials, 3 concurrent
- Dungeon: one challenge, then sweeps
- Daily 단추 materials; gem materials only when they gate the next evolution
- Event shop (짝짝이 costumes, 달빛, 가방); costume unlocks
- FTUE 첫 만남 (우두커니 at 1-3) and the 300 별조각 graduation gift

**Spending**
- **단추: cheapest-first** across the 수선점 level, the levels of every owned lineage, and open R2-04 rows. This is SPEC4 CR-07's policy under trickle income; the in-game MAX and hold buttons make it human-feasible.
  - Owned, not just placed: an early pass leveled only placed friends, so a 라라벨 pulled on day 20 stayed Lv1 and was never fielded.
- **Gems:** 10-pulls whenever ≥ 900 별조각 (F2P, no ads or IAP).
- **기억편:** 공명 first, then breaks and 수선. 선택 기억편 go to the strongest placed lineage that is still growing.
- **Evolutions** are crafted when possible, with shard acceleration when it is cheap.

**Formation**
- The player re-forms when the last session gained fewer than 3 stages, or a form or lineage was added.
- It tries 5 line-ups on the frontier stage with the real simulator: AutoFormation, current, DPS-first, DPS + lineage pairs, DPS + healer. It keeps the fastest clear.
- The **auto** variant uses only `AutoFormation()`.

**Variants:** light (1 × 15 min), heavy (3 × 30 min), long (2 × 60 min), warm (sleep-mode idler, no cold start), auto formation.

**Runs:** 90 days or until the target stage, stopped after 21 days without progress. 3–12 seeds per configuration. The report cites 227 player-runs (another ~300 exploratory runs were used while building the model), at 10–150 s each on 24 cores.

### 2.3 Definitions
- **Day** = day index + local hour / 24. 0.35 is the day-0 morning session and 0.9 the day-0 evening session.
- A **chapter is cleared** when its 10th stage is cleared for the first time.
- A **hard wall** is a stall of ≥ 7 days. A **soft wall** is a multi-session stall that the player's own investment resolves.

---

## 3. Shipped constants

| chapter | no R2 (DPS line-up) | no R2, AutoFormation | R2-05 only | R2-04 only | R2-04 + R2-05 |
|---|---|---|---|---|---|
| 5 | 0.3 (14 min online) | 0.3 | 0.3 | 0.3 | 0.3 |
| 10 | 1.3 (55 min) | 1.9 | 1.3 | 0.9 | 0.9 (30 min) |
| 12 | 2.8 | 4.3 | 2.3 | 1.3 | 1.1 |
| 13 | 4.1 | stall at 125–127 (d5–9) | 3.3 | 1.3 | 1.3 |
| 15 | **8.9** (350 min) | – | 6.9 | 2.3 | **2.1** (79 min) |
| 16 | stall at 153 (d9–16) | – | 23.9 | 2.3 | 2.3 |
| 20 | – | – | stall at 159–165 (d16–24) | 4.9 | 4.3 |
| 25 | – | – | – | 13.3 | **10.1** |
| stage on day 30 / 60 | 152 / 152 | 126 / 126 | 164 / 168 | 258 / 266 | 270 / 274 |

![Day each chapter is cleared](figures/fig2_chapter_days.png)

![Battle time per stage](figures/fig5_battle_time.png)

---

## 4. Why the late-curve question was posed against the wrong economy

CR2-08 asked which late curve keeps R2-04 at need/rec 0.80–1.05. Its model was 단추 ∝ 1.13^stage, from stage clears. The shipped economy does not work that way:

- 단추 ∝ 1.10^(수선점 Lv), and the 수선점 level grows like ln(t)/0.018 regardless of stage.
- R2-04 rows (cost ×1.10) climb with it, so R2-04 + R2-05 erase every wall up to stage ~270. Chapter 15 falls on day 2 and chapter 25 on day 10.
- The 바구니's gem line (×1.02 per stage) floods the gacha with 1,330 pulls/week, so 공명 10 and R2-05 breaks saturate in about a week.
- Shop HP never limits anything, failures are 0, and the rec rating means nothing.

The critic's model only holds once the 단추 engine is bounded (§6). From week 2 on, stage clears then supply ≈100% of 단추, so 단추 ∝ 1.13^stage.
- R2-04 at ×1.02 per 25 levels adds about 2% power per stage against a 7% need, and the wall returns at stage 181–221 (§6).
- In that setting a late curve has to be chosen, and the sustainable slope is about 2% per stage. That is where ×1.02 comes from.

---

## 5. Candidate fixes, each on both economies

![Candidates](figures/fig3_candidates.png)

On the CR-07 economy, the "none / taper@61 / rank / content / taper@151 ×1.04–1.015" rows use basket growth ×1.005. The recommended package row uses ×1.0025 (§6.2).

| fix (with R2-04 + R2-05) | economy | ch15 day | ch25 day | stage d30 / d60 / d90 | first ≥ 7-day stall |
|---|---|---|---|---|---|
| none | shipped | 2.1 | 10.1 | 270 / 274 / 274 | 271–275 @ d24–35 |
| 2nd taper @61, HP ×1.045 (the critic's example) | shipped | 1.9 | 4.9 | 387 / 394 / 398 | 391 @ d30–32 |
| 등급 ×1.10 per 25 levels | shipped | 1.3 | 3.3 | 410 / 426 / 440 | 425 @ d47 |
| content ×1.35 every 30 days | shipped | 2.3 | 10.3 | 270 / 280 / 290 | 271–277 @ d24–35 |
| 2nd taper @151, HP ×1.02 | shipped | 2.3 | 5.9 | 582 / 608 / 608 | 603 @ d36–39 |
| none | CR-07 | 7.1 | – | 190 / 210 / 224 | 185–221 @ d14–74 |
| 2nd taper @61, HP ×1.045 | CR-07 | 1.3 | 4.9 | 326 / 340 / 340 | 325–331 @ d19–40 |
| 등급 ×1.10 | CR-07 | 1.3 | 5.9 | 330 / 340 / 348 | 327–341 @ d12–41 |
| content ×1.35 / 30 d | CR-07 | 4.9 | – | 190 / 200 / 214 | 185–225 @ d14–65 |
| 2nd taper @151, HP ×1.04 | CR-07 | 5.9 | 15.8 | 284 / 290 / 298 | 281–285 @ d8–37 (median ≈ 21) |
| 2nd taper @151, HP ×1.035 | CR-07 | 7.1 | 17.1 | 302 / 324 / 326 | 299–315 @ d15–42 (≈ 31) |
| 2nd taper @151, HP ×1.03 | CR-07 | 7.1 | 13.8 | 345 / 364 / 376 | 341–353 @ d27–43 (≈ 34) |
| 2nd taper @151, HP ×1.025 | CR-07 | 5.9 | 11.1 | 400 / 446 / 459 | 427–453 @ d34–69 (≈ 45) |
| 2nd taper @151, HP ×1.02 | CR-07 | 7.1 | 12.8 | 472 / 588 / 602 | 595–603 @ d54–68 in 9/12; none in 3/12 |
| 2nd taper @151, HP ×1.015 | CR-07 | 5.9 | 10.1 | 618 / 878 / 900 | none (runs away: stage 900 by day 90) |
| **package: taper @151 ×1.02, basket ×1.0025** | **CR-07** | **6.6** | **13.3** | **457 / 547 / 575** | **535–571 @ d49–71 (median 57)** |

How to read the table:
- **Taper after chapter 6, and a bigger 등급 multiplier.** On either economy they turn chapters 12–25 into a 2–6-day sprint and remove the soft walls R2 is meant to solve.
- **Content ×1.35 a month.** Changes almost nothing; the wall is set by stage-indexed growth, not by a monthly step.
- **The taper at 151.** It leaves chapters 1–15 untouched because it starts after the authored content. Its value sets how long the endless ladder runs before the economy saturates:
  - ×1.035–1.04: the ceiling arrives in weeks 3–5.
  - **×1.02: the slope R2-04 can sustain on clear 단추** (≈2% power per stage). Chapters stay at 0.5–1 day each to ~ch45, and the ceiling moves to week 7–10.
  - ×1.015: runs away, i.e. no wall within 90 days.

---

## 6. CR-07 economy retune (the prerequisite)

### 6.1 `goldPerSecondGrowth` 1.10 → 1.06

- **Payback.** The per-level payback ratio rises from 1.0182 to 1.0566. 수선점 levels now follow stage-clear 단추 instead of compounding by themselves:

  | day | 0 | 6 | 29 | 59 |
  |---|---|---|---|---|
  | 수선점 Lv | 114 | 178 | 498 | 596 |
  | 단추/s | 7.9e3 | 4.7e5 | 1.9e14 | 6.3e16 |
  | shipped 단추/s | 9e9 | 9e20 | 1.5e26 | 3.7e26 |

- **Shop HP matters again.** Shop HP no longer outruns enemy damage, so failures happen: 93–191 in 90 days.
- **The exact value is not critical.**
  - 1.05 and 1.07 behave the same as 1.06 within gacha noise (§7.5).
  - The shipped 1.10 is the runaway.
  - 1.06 is the round middle of the safe band.
- **Side effect: late-game offline 단추 is cosmetic.** Stage clears become ≈100% of 단추 from week 2, so the basket's 단추 line stops mattering; its gems and materials still count.
  - If offline 단추 should stay meaningful, index the basket's 단추 to "N minutes of current-stage clears" instead of 수선점 단추/s.
  - That is a later code change; this curve does not depend on it.
- **(CR4-05) The same defect reaches every "단추 N분치".** `MetaReward.GoldMinutes` resolves at the shop's 단추/s (`MetaRewardService.cs:10`).
  - On day 1 clears already earn ≈37× that rate per online minute. In `R_REC_r2` (medians), "단추 60분치" is 3.0 % of that day's 단추 on day 1, 1.5 % on day 6, 0.003 % on day 13 and ≈0 from day 20 (6.9e17 against 1.1e27 on day 29).
  - Every R2 단추-minute reward (errand 1, guest bands, tower floors, walk, lantern) and every 단추-minute price (workbench, polish, rename, market, merge, decor) stops mattering after week 1.
  - SPEC5 §4.6 (PHASE4-ROADMAP Q15) indexes `GoldMinutes` and the basket's offline line to the shop rate plus clear 단추 at the best stage per reference clear time. Below ≈stage 30 the shop rate dominates, so this curve's early numbers stand; the projection runs gate the rest.

### 6.2 `IdleChestConfig.stageGrowth` 1.02 → 1.0025

- The basket's growth trades the gem budget against how late the ceiling arrives, because gems → pulls → 기억편 → 공명 and breaks. Measured with the ×1.02 taper (6 seeds):

| basket growth | 별조각 week 4 / week 8 | first ≥ 7-day stall |
|---|---|---|
| 1.0 | 2.5k / 2.2k | 509–527 @ d38–51 (1 seed none) |
| **1.0025** | **3.6k / 3.9k** (12-seed package: 3.6k / 3.9k) | **535–553 @ d49–64** |
| 1.005 | 7.1k / 11.7k | 595–603 @ d54–68 |

- **Why 1.0025.** Its gem income is ≈1.3× R2's budgeted ≈2,875/week (SPEC4 2,640 + R2 ≈235) and holds from week 4 to week 8. Every higher value drifts upward as the stage climbs.
  - A cap on the stage factor (×2) would be cleaner. It needs a per-line field, so it is a code change.
- **Pity cadence:** ≈40 pulls/week, a 60-pull pity every ≈10.5 days.
- **기억편 sinks stay meaningful.** 공명 averages 3.5 on day 29 and 5.2 on day 59, against 8.8 / 9.4 shipped.
- **Evolutions:** 24 forms on day 6 and 27 by day 59.

### 6.3 What no economy knob changed

Chapters 1–11 always fall by about day 1, ≈34 minutes online for ch10. Tested:
- 단추/s growth 1.00–1.10
- 수선점 cost growth 1.12–1.18
- base cost ×10–×1000
- clear 단추 ×1.10
- lineage cost growth 1.11–1.12
- offline rate 0.1–0.5

The sprint comes from:
- **The starter surplus:** power/rec 2.6–10× through chapter 3.
- **Clear 단추 outrunning level costs:** clear 단추 grows ×1.13 per stage while lineage cost grows ×1.09 per level, so about +1.4 levels per stage.
- **Day-0 gems:** ≈3,000–4,000 별조각 (welcome 1,000, attendance day 1 300, graduation 300, quests, level mail). That is 30–40 pulls, so 6–7 lineages on day 0.

Two consequences:
- **For R2 §3.5.** Every unlock up to 5-1 fires inside the **first ~15 minutes**: techniques 3-4, rounds 3-2, tower 3-8, 등불 길 4-1, keepsakes 4-5, workbench 5-1. The "≤ 4 cards per chapter" rule should be re-based to minutes since install.
- **For CR-07's band test.**
  - On the retuned economy the chapter-1 median gap is still 0 s (3.5 s with bursts removed). The causes are the 20-pt journal chest paying `GoldMinutes(30)` at 1-7, plus quest 단추.
  - Replace the band with a per-chapter time-to-clear assertion (§8).

---

## 7. The recommended package in detail

### 7.1 Time-to-clear per chapter

Engaged player, 2 × 20 min per day, R2-04 + R2-05, 12 seeds. "Days" is the median day each chapter is first cleared; "in ch" is the median time spent in that chapter. The full table is in `analysis/report_tables.md`.

| ch | day [min–max] | days in ch | online min in ch |
|---|---|---|---|
| 1 | 0.3 | 0.3 | 4 |
| 5 | 0.3 | 0.0 | 3 |
| 10 | 0.9 [0.9–1.3] | 0.0 | 5 |
| 11 | 1.3 | 0.5 | 11 |
| 12 | 1.9 [0.9–2.3] | 0.5 | 26 |
| 13 | 3.1 [1.3–3.3] | 1.2 | 44 |
| 14 | 4.6 [1.3–5.3] | 1.5 | 63 |
| **15** | **6.6 [1.3–8.9]** | 2.0 | 91 |
| 16 | 8.6 [1.9–10.9] | 2.0 | 71 |
| 17 | 9.3 [2.3–11.9] | 0.7 | 34 |
| 18 | 9.9 [2.3–12.9] | 0.5 | 17 |
| 20 | 10.6 [2.9–13.9] | 0.3 | 15 |
| 25 | 13.3 [5.9–15.9] | 0.5 | 25 |
| 30 | 16.9 [8.9–19.3] | 0.8 | 28 |
| 35 | 20.4 [12.3–23.9] | 0.8 | 29 |
| 40 | 24.4 [16.3–28.9] | 0.8 | 32 |
| 45 | 29.1 [21.9–35.3] | 1.0 | 45 |
| 50 | 36.4 [30.3–45.9] | 2.0 | 75 |
| 52 | 40.8 [35.3–50.9] | 2.5 | 103 |
| 53 | 46.8 [39.9–55.3] | 6.0 | 232 |
| 54 | 50.3 [42.3–64.3] | 3.5 | 145 |

- **Stage reached on day 7 / 14 / 30 / 60 / 90:** 150 / 262 / 457 / 547 / 575.
- **Online minutes to ch15 / ch25 / ch50:** 269 / 530 / 1,446.
- **First Lv100:** 21–29 minutes online (stage 74–84), against 13–20 minutes shipped.

### 7.2 The walls R2 solves

| recommended package, engaged player (seeds 1–3) | ch15 day | stage d30 / d90 | first ≥ 7-day stall |
|---|---|---|---|
| no R2 | – | 130 / 154 | **129–145 @ d18–38** (ch13–15) |
| R2-05 only | 54.9 | 140 / 176 | 135–145 @ d30–34 |
| R2-04 only | 5.3 | 482 / 526 | 525–531 @ d46–50 |
| R2-04 + R2-05 (12 seeds) | 6.6 | 457 / 575 | 535–571 @ d49–71 |

![R2 contribution](figures/fig6_r2_contribution.png)

R2-04 does the work, turning a 2–5-week wall in chapters 12–15 into 1–2-day chapters. R2-05 adds ≈50 stages at the end and almost nothing on its own, because breaks wait on 기억편 (13 breaks by day 30, 22 by day 59).

### 7.3 Other player types (same package)

| player | ch10 | ch15 | ch25 | stage d30 / d90 | first ≥ 7-day stall |
|---|---|---|---|---|---|
| engaged, DPS line-up (main, 12 seeds) | 0.9 | 6.6 | 13.3 | 457 / 575 | 535–571 @ d49–71 |
| engaged, **AutoFormation only** | 0.9 | 8.9 | 20.3 | 320 / 409 | 359–403 @ d32–62 |
| light (1 × 15 min per day) | 2.9 | 13.9 | 29.9 | 253 / 514 | none within 90 days (3/3) |
| heavy (3 × 30 min per day) | 0.5 | 0.9 | 2.5 | 552 / 576 | 527–559 @ d27–36 (1 none) |
| long sessions (2 × 60 min) | 0.4 | 0.9 | 1.9 | 540 / 598 | 553 @ d39–41 (1 none) |
| warm (sleep mode, no cold start) | 0.9 | 2.9 | 6.9 | 504 / 576 | 531–553 @ d41–45 |

- **AutoFormation is costly from chapter 15 on.** It takes 20 days to ch25 instead of 13 and stalls about 150 stages lower. It ignores style damage scales (snipe ×1.7 / 1.5, assault ×1.6) and 계보 공명.
  - A cheap UX follow-up, not a curve change: an "attack first" sort, or a formation suggestion in the 강해지기 sheet.
  - (CR4-06) Assigned: SPEC5 R2P-3 ships 자동 편성 v2 in wave A. `AutoFormation` ranks by offence power with style scale and 공명, SPEC4's 강해지기 row compares offence, and every projection runs this `auto` variant (target: ch25 ≤ day 17).
- **Heavy and long sessions** turn the endless ladder into a 4–6-week climb to the same ceiling. Expected: the time wall scales with session length.

### 7.4 The ceiling (~ch55, weeks 7–10)

- **It is not a curve choice.**
  - Every late slope from ×1.04 to ×1.02 ends in a first ≥ 7-day stall, from median day ≈21 at ×1.04 to ≈57 at ×1.02. Only ×1.015 runs away.
  - The ceiling is the point where the P0 growth axes saturate: R2-04's capped rows are full (손놀림 100, 매듭 확률 300, 심지 100, 두 땀 250), the uncapped rows are +1% linear per level, R2-05 approaches Lv200, and 공명 is gem-limited. From then on power grows ~log(time) while the player farms a fixed stage.
- **The right tool is R2-36b's planned cadence**: "+1 form per short tree, then a new lineage every 4–6 weeks". It lands just before the ceiling and should be scheduled against it.
- (CR4-07) Scheduled means post-launch. SPEC5 ships the six step-1 forms before launch, so the measured ceiling already contains them.
  - Each form is ×1.35 over its tier-1 parent in one of a lineage's two slots, at most ≈ +19 % team attack, ≈ 5–15 stages at ×1.02.
  - The ceiling raise has to come from drops timed against the ceiling day that projection #3 measures (PHASE4-ROADMAP Q16).
- **Alternative, later:** a compounding row with a milestone multiplier. It would need chapters 12–17 re-checked.

### 7.5 Seed spread

- **Gacha luck is amplified by the clear-단추 feedback** (stage → 단추 → R2-04 → stage).

  | 라라벨 pulled on | ch15 | ch25 | ch50 |
  |---|---|---|---|
  | day 0 (2/12) | 1.6 | 5.9 | 30.6 |
  | days 1–7 (2/12) | 4.3 | 8.1 | 31.1 |
  | later or never (8/12) | 7.3 | 13.6 | 40.4 |

- The day-0 rate is ≈26% (1% per pull, 30 pulls), so **the typical player is the "later" row**.
- **Medians are robust; ranges are wide.** Comparisons with 3 seeds can flip order (e.g. 단추/s growth 1.05 vs 1.06 vs 1.07), so every claim here uses 6–12 seeds or same-seed pairs.

---

## 8. Implementation notes (for whoever owns CR-07 and the R2 constants)

### 8.1 `BattleConfig.cs` (data only)
Add three fields and extend `TaperedScale`. This is the code that ran in the harness (`sim/patched/BattleConfig.cs`), with the new fields in place of the harness hooks:

```csharp
[Tooltip("두 번째 완만 구간 시작 스테이지 (0 = 없음). 16-1 = 151 — 저작 챕터(1~15) 뒤 끝없는 사다리의 기울기")]
public int stageGrowthTaperStage2 = 151;
[Tooltip("두 번째 완만 구간 스테이지마다 적 체력 배율 (복리) — 수선 비법이 클리어 단추로 버틸 수 있는 기울기")]
public float stageHpGrowthLate2 = 1.02f;
[Tooltip("두 번째 완만 구간 스테이지마다 적 피해 배율 (복리)")]
public float stageDamageGrowthLate2 = 1.015f;

public float GetStageHpScale(int stage) => TaperedScale(stageHpGrowth, stageHpGrowthLate, stageHpGrowthLate2, stage);
public float GetStageDamageScale(int stage) => TaperedScale(stageDamageGrowth, stageDamageGrowthLate, stageDamageGrowthLate2, stage);

float TaperedScale(float growth, float lateGrowth, float lateGrowth2, int stage)
{
    int steps = Mathf.Max(1, stage) - 1;
    int taper = Mathf.Max(1, stageGrowthTaperStage) - 1;
    int early = Mathf.Min(steps, taper);
    int late = steps - early;
    int late2 = 0;
    if (stageGrowthTaperStage2 > 0)
    {
        int taper2 = Math.Max(taper, stageGrowthTaperStage2 - 1);
        late2 = Math.Max(0, steps - Math.Max(early, taper2));
        late -= late2;
    }
    double scale = Math.Pow(Mathf.Max(1f, growth), early) * Math.Pow(Mathf.Max(1f, lateGrowth), late)
                   * (late2 > 0 ? Math.Pow(Math.Max(1.0, lateGrowth2), late2) : 1.0);
    return (float)Math.Min(scale, 1e30);
}
```

- Existing assets load the C# initializers. Re-run IdleDataBuilder so `BattleConfig.asset` serializes the new fields.
- Dungeon floors have their own growth and are unaffected.

### 8.2 Economy constants
- **`Data/GameConfig.asset`:** `goldPerSecondGrowth: 1.06`. Edit the YAML (see SPEC4 §7.6), and set the C# default to 1.06 so new assets match.
- **`MetaContent.IdleChest.cs`:** `stageGrowth = 1.0025`. Update the doc comment too, ×1.02 → ×1.0025 per stage.

### 8.3 PRG-01 / PowerCalibration
- `rec` is a single exponential (1.0741 per stage). It overstates the need past 151 even more than past 30; MetaContent.Power's comment already asks for a segmented `PowerConfig`.
- TeamPower is dominated by its shop-HP term once 수선점 Lv > ~150.
- Until both are fixed, ratings are advisory, and R2-04's need/rec acceptance criterion should be replaced by §8.4.

### 8.4 CR-07 tests
- Keep `EconomyFlowTests` as a smoke test, but drop the chapter-1 15–30 s band. It is unreachable while the journal chest pays `GoldMinutes(30)` at 1-7.
- Add a **projection test** driven by this harness's player (or a trimmed copy). On the recommended constants, median of 3 seeds:
  - ch10 by day 2;
  - ch15 within days 4–10;
  - no ≥ 7-day stall before ch45;
  - 별조각 in week 4 between 3k and 4.5k;
  - and, with R2 off, a stall before ch16.
- **(CR4-02) Scope of the 별조각 band above.** It holds for this harness: R2-04/05 only, no SPEC4 v1.1 sources and no C-01 cap.
  - The binding band once SPEC4 is built, C-01 is on and wave A is in is SPEC5 §4.4: week 4 2,800–3,800, week 8 2,600–3,600.
  - Every projection must log 별조각 per source (journal, attendance, basket, quick idle, mail incl. the shop-level mail, chests, codex, records, affinity, events, season, each R2 mode), because the totals above hide which line moved.

### 8.5 R2-04 / R2-05 constants
- Unchanged. On the CR-07 economy the critic's per-stage math applies, and the taper is set to the slope R2-04 sustains (≈2% per stage).
- If R2-04's rows, caps or 등급 change, re-run the harness. A bigger 등급 needs a steeper taper, or chapters 12–25 collapse (§5).

---

## 9. Caveats

- **Systems not in code yet are not simulated.**
  - Not simulated: SPEC4 v1.1 items (codex rewards, achievements, chapter chests, 첫 주 NEW-01, events, fireflies, comeback) and R2 modes (tower, guests, errands, 등불 길, festival).
  - They mostly add 별조각 and 기억편: R2 §9.1 counts ≈+235/week, plus one-offs. That would move every curve a little earlier and narrow the seed spread.
  - (CR4-01) "A little" understates one source. SPEC4's chapter-chest default row pays 별조각 150 on every first x-10 clear from ch6 on.
    - This harness's package player completes 5.0 / 10.5 / 9.0 / 6.5 / 4.5 chapters in weeks 1–5, so the row would add ≈750 / 1,575 / 1,350 / 975 / 675 별조각 a week (a heavy player ≈3,300 in week 1).
    - That is +27–61 % gems in the very weeks where the gacha drives the pace (§7.5).
    - PHASE4-ROADMAP RD-13 cuts the row to 100 on every fifth chapter.
  - The shipped shop-level mail (별조각 50 per 10 shop levels) *is* in these runs: ≈350–550 a week in weeks 2–4 with R2, ≈0–100 without.
  - Not modeled: R2-05b riders. 두 땀 뜨기 is treated as +attack. 3× speed does not exist in the game.
- **The engaged player is strong.**
  - It levels and claims every 15 s and tries line-ups whenever it stalls.
  - Real engaged players will be somewhat slower, and AutoFormation-only players clearly slower from chapter 15 (§7.3).
- **Cold start is the default.** An app killed between sessions restarts the battle. Sleep-mode idlers who keep the app open (the "warm" variant) are faster, and battle time stops being a hard cap for them.
- **Noise.** See §7.5. Single-seed results can move a chapter by days.
- **Number range.**
  - The shipped economy reaches 단추 ≈1e27–1e31 by days 30–40. That is fine for `double`, and `NumberFormatter` handles it.
  - Enemy HP is `float`, capped at 1e30 by `TaperedScale`. No run reaches the cap.

---

## 10. Files

**Harness — `phase4b/sim/ProgSim.csproj`**
- `sim/src/` — Player (agent), FormationOptimizer, R2Systems, Scenario, ContentFactory, Hooks, Program
- `sim/stubs/UnityStub.cs`
- `sim/patched/` — generated by `sim/tools/patch_sources.py`

Build and run:
```
dotnet build -c Release
dotnet bin/Release/net8.0/ProgSim.dll run name=X seed=1 days=90 shopgold=1.06 basketgrowth=1.0025 taper2=151 hplate2=1.02 dmglate2=1.015 r204=1 r205=1 out=../runs
dotnet bin/Release/net8.0/ProgSim.dll selftest
```

**Batch and analysis — `sim/tools/`**
- `runmany.py` — parallel runner
- `summarize.py` — chapter days and stalls
- `econ.py`
- `report_data.py` — writes `analysis/report_tables.md` and `analysis/report_data.json`
- `plots.py` — writes `figures/fig1…fig6`
- `cr07_report.py` — CR-07 band check

**Runs — `phase4b/runs/<scenario>_s<seed>/`**

Each run folder holds:
- `daily.csv`
- `clears.csv` — first clear of every stage: day, online minutes, battle seconds, shop level, levels, 공명, R2 state, enemy scales
- `stages.csv`, `sessions.csv`, `purchases.csv`, `summary.txt`

Scenario groups:
- `R_*` — final package, by R2 option and player type
- `Z_*` — shipped economy, CR-07 economy, candidates
- `X_*`, `Y_*`, `W_*`, `V_*` — late-growth and basket sensitivity
- `CR07_SHIP`, `CR07_REC` — the 60-minute band test

Exploratory runs from building the model are in `phase4b/sim/runs/`. Superseded; not cited above.

---

## 11. Critic notes (PHASE4-ROADMAP v1.1, 2026-11-04)

The roadmap critic recomputed weekly figures from this report's own runs (`runs/R_REC_*`, `daily.csv`). The conclusions of §0–§8 stand. Five notes for whoever uses them:

| Tag | Note | Where it is handled |
|---|---|---|
| CR4-01 | SPEC4's ch6+ chapter chest would add ≈975–1,575 별조각 a week in weeks 2–4 at this curve's chapter pace (§9) | PHASE4-ROADMAP RD-13, delta 29 |
| CR4-02 | §8.4's band is harness-only. The measured R2-off baseline (2,399 in week 4) is already ≈575 above SPEC4's modelled pre-phase-4 income (≈1,825), and the basket credits ≈26–39 gem-hours a day | SPEC5 §4.4 bands; per-source logging from RM-3 |
| CR4-05 | "단추 N분치" (rewards and prices) is decorative from week 2 on this economy, not only the basket's offline line (§6.1) | SPEC5 §4.6, PHASE4-ROADMAP Q15 |
| CR4-06 | The AutoFormation follow-up (§7.3) now has an owner and a projection target | SPEC5 §5.6, PHASE4-ROADMAP RD-15 |
| CR4-07 | The ceiling (§7.4) is untouched by the pre-launch forms; only a post-launch cadence or a later compounding row raises it | PHASE4-ROADMAP Q16 |

Weekly 별조각 (median, from `gems_earned` deltas, days 20→27 and 48→55):

| Run | Week 4 | Week 8 | Stage at week 4 (median) |
|---|---|---|---|
| `R_REC_none` (3 seeds) | 2,399 | 2,479 | 129 |
| `R_REC_r2` (12 seeds) | 3,609 | 3,905 | ≈400 |
| `R_REC_r2auto` (3 seeds) | 3,066 | – | ≈280 |
| `R_REC_r2light` (3 seeds) | 2,021 | – | ≈210 |
