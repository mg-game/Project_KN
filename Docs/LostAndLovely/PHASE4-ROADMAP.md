# LOST & LOVELY: PHASE4-ROADMAP — one plan for everything after UX wave 2 and WP-IA

Version 1.1 · 2026-11-04 · role: producer; **critic pass applied (v1.1)**: changed spots carry `(CR4-nn)` tags, and the full list with evidence is in **Critic revisions** at the end (v1.0 backup: `phase4b/critic/PHASE4-ROADMAP.pre-critic.md`) · target `D:/UnityProject/unity_idle` (Unity 6000.2.6f2, uGUI, 1080×1920 portrait). The project was read-only while writing: no project file was edited. The only live call was the read-only Higgsfield `balance` (46.88 credits, plus plan, 2026-11-04 ≈06:05).

**Standing orders (user):**
- "다른 유명한 방치형게임들의 레퍼런스를읽어서 부족한기능들 알아서 다진행해줘": build every missing feature from famous idle games, on our own initiative.
- "크레딧충전했으니까마음껏써 품질최상으로 전부다": use the credits freely, top quality, everything.
- Tone: soft storybook, living forgotten objects, cozy night street.

**Orchestrator decisions applied unchanged:** no NPC league for now; no restyle of the udu/lala lineages (their new art references today's forms); prestige, gear gacha and summon level stay rejected; audio comes from CC0 packs plus original synthesis (no Higgsfield SFX or music model, ever).

**What this is.** One sequenced plan for all remaining work:
- SPEC4 (P4-0…P4-10; 48 open items plus NEW-01…04);
- the PROGRESSION-SIM curve fix;
- SPEC5 (R2-00, 7 P0, 16 P1);
- the ART-UPGRADE-PLAN batches still open, and the FX-JUICE art it could not afford;
- the content expansions: six costumes, six new forms, new places with fields, enemies and bosses, storybook pages and puzzle stages, new lineages;
- the audio follow-ups;
- the P2 waves C and D of IDLE_RESEARCH2, which still need a contract (SPEC6).

For each wave it gives file-disjoint parallel packages (SPEC4's one-owner-per-file model), dependencies and gates, the assets with Higgsfield credits at max quality, agent-day estimates and risks. The waves are ordered so that the most player-visible value lands first.

**Inputs read (scratchpad unless noted):** `SPEC4.md` v1.1 (§0–§2, §9–§14), `SPEC5.md` v1.0 (§0–§4, §8–§11, App. D), `IDLE_RESEARCH2.md` v1.2 (§0, §6, §8, §9, critic and merge log), `phase4b/merge_log_v1_2.md` and `research2/unseen_findings.json` (all 51 are dispositioned in v1.2), `phase4b/PROGRESSION-SIM.md`, `UX-SPEC.md` §4, `UX-SPEC-IA.md` §7–§9, `ART-UPGRADE-PLAN.md` (all), `artmax/qa_report.md` and every `artmax/gen/*/jobs.tsv`, `FX-JUICE-SPEC.md` §0.4, §5, §6 and `fxj_art/report.json`, `AUDIO-SPEC.md` §0, §2.11, §7.10, §8, the four `audio_*_report.json`, `verify.ps1`, `compile_check.py`, the memory notes. Code and assets were checked read-only under `Assets/_Project` (evidence in Appendix B).

**How to read.** Read §0 first, then §2 (rules) and §3 (deltas every package applies). Then read your wave in §4 and your files in §5. Assets are in §7 and risks in §8. §9 explains the value order, and §10 maps every remaining item to a wave.

**Names used here.** Roadmap waves are **RM-1…RM-10** and their gates are **G0…G10**. SPEC4 package names (P4-x) and SPEC5 names (R2P-x) are kept. New names:
- **P4-C** is the curve package;
- **AUD** is the audio workflow, the long-lived owner of `Core/Audio/**`;
- **WP-9** is UX-SPEC's verify package;
- **S6P-x** are the proposed SPEC6 packages;
- **L0…L10** are the rungs of the credit ladder (§7.2);
- **RD-n** are decisions this roadmap takes, and **Q-n** are decisions it asks for.

---

## 0. Summary

### 0.1 The waves

| Wave | Name | Contains | Parallel packages | Effort (agent-days) | Critical path (agent-days) | Art at max (credits) | Exit gate | What the player gets |
|---|---|---|---|---|---|---|---|---|
| RM-1 | Foundation | SPEC4 wave 4.0 contracts; the curve package; UX wave 3 verify; the first art batch | P4-0 ∥ P4-C ∥ WP-9 ∥ P4-10 ∥ AUD | 8 | 2 | **45 (fits now)** | G1 | Progress paced again (단추 no longer explodes, 13k 별조각/week is gone); unlock cards spaced 180 s (if Q2); sheet sounds |
| RM-2 | SPEC4 build | SPEC4 wave 4.1, P0 → P1 → stretch inside every package; **the chapter-chest gem fix (CR4-01)** | P4-1 ∥ P4-2 ∥ P4-3b ∥ P4-4 ∥ P4-5 ∥ P4-6 ∥ P4-7 ∥ P4-8 ∥ P4-10 ∥ AUD | 38.5 | 7 | 612 (art lane during RM-2/3; top-up) | G2 (build) | All 48 SPEC4 items, with sound wired on every screen |
| RM-3 | SPEC4 verify | SPEC4 wave 4.2, projection run #1 **with per-source 별조각 logging and the measured baseline (CR4-02)**, fix lane, SPEC5 critic, R2P-0 staged | P4-9 ∥ fix lane ∥ SPEC5 critic ∥ R2P-0 (scratch only) | 7.75 | 3 | 0 (QA only) | **G3 = SPEC4 accepted** | The SPEC4 build, verified |
| RM-4 | SPEC5 R2-0 | v5 contracts, hub-sheet shell, pacing rows; the income and place hooks (CR4-05, CR4-20) | R2P-0 ∥ R2P-C (confirm) ∥ R2P-6 ∥ AUD | 1.75–3.25 | 1–2.5 | 12 | G4 | Nothing new on screen; the game survives a v4 → v5 save |
| RM-5 | SPEC5 wave A (P0) | weekly guest, techniques, cap breaks, errands 1–2, rounds + banks, 맞춤 편성, six costumes; **a smarter 자동 편성 (CR4-06) and 단추 N분치 that follow real income (CR4-05, Q15)** | R2P-1 ∥ R2P-2 ∥ R2P-3 ∥ R2P-4a ∥ R2P-5 ∥ R2P-6 ∥ AUD → R2P-7 | 32 | 9.75 | 108 (+54 opt) | G5 | 밤 나들이 hub, 그림자 까치 and 빨랫줄 errands, the guest at the shop door, 수선 비법, 한 땀 돌파, 오늘의 순회, a costume for every lineage; 단추 rewards and the night basket worth something again in week 2+ |
| RM-6 | SPEC5 B1 + content first | tower, titles + profile, errand 3, lantern, route marks, market, merge, festival + moon; **place 2 and the six new forms pulled forward (RD-7)**; the place's own night sound (CR4-20) | R2P-1 ∥ R2P-2 ∥ R2P-4b/4c ∥ R2P-5 ∥ R2P-6 ∥ AUD → R2P-7 | 29 | 9 | 119 (+42 opt) | G6 | A new place on the main ladder (야시장 골목: field, magpie and nest enemies, bell boss, its own ambience), six new forms, 보관소 오르기, festival weeks and full-moon nights |
| RM-7 | SPEC5 B2 | 등불 길, 안채 + workbench + keepsakes, skill riders; SPEC6 written in parallel | R2P-1 ∥ R2P-2 ∥ R2P-3 ∥ R2P-4c ∥ R2P-5 ∥ R2P-6 ∥ AUD → R2P-7 ∥ SPEC6 authors | 27.5 (+3 SPEC6) | 11 | 150 (+174 opt) | G7 | The lantern-road roguelite, the shop's back rooms with research and keepsakes, resonance skill riders |
| RM-8 | SPEC6-A content cadence | places 3–6 with storybook pages and their own ambience, place-2 storybook, two new lineages, season costumes, 20 storybook puzzle stages | S6P-0 → S6P-1 ∥ S6P-2 ∥ S6P-4 ∥ S6P-5 ∥ S6P-6 ∥ AUD → S6P-7 | ≈23.5 | ≈7 | ≈355 | G8 | Four more places along the night map, each with its story and sound; two new friends to find; hand-made puzzle stages |
| RM-9 | SPEC6-B wave C | 꿈길 + full 등불 길, boards + share card + **끝없는 밤 and friend codes (CR4-23)** (no league), affinity letters and gifts, hidden records, box hunt, daily request, 한발 먼저, 축음기 음반 | S6P-0 → S6P-1/2/4/5/6 → S6P-7 | ≈33 | ≈10 | ≈100 | G9 | Weekly seeded walk, the endless night, personal boards, letters from friends, collectible records |
| RM-10 | SPEC6-C wave D | 안채 꾸미기 + 밤손님, 원정 엽서, 계절 달력 꾸러미 | S6P-* | ≈22 | ≈8 | ≈640 (second top-up) | G10 | A room to decorate with night visitors, postcards, seasonal packs |

- **Totals.** About **145 agent-days** of specced work (RM-1…RM-7), about **227** with SPEC6. (CR4: +≈4.5 for the critic additions, 3 of them in RM-5.)
- **Critical path.** About **43 agent-days** through RM-7 and about **68** through RM-10. This is the elapsed time if every wave runs its packages fully in parallel.
- **Ranges.** SPEC5's own wave totals (26/18/24) are lower than the sum of its package figures (27/22/30). This roadmap plans with the package figures. RM-8…RM-10 are ±50 % until SPEC6 exists.
- **Entry (G0).** These must all be true before RM-1 starts:
  - UX wave 2 (WP-4…WP-8) has merged and WP-IA acceptance is green;
  - FX-JUICE has merged with its §7 acceptance green;
  - the audio engine compiles and its `Audio*Tests` pass on the copy;
  - `Hud/BossChallengeButton` exists in Main.unity (SPEC4 precondition C-03).

### 0.2 Credits at a glance (Higgsfield `gpt_image_2_5`, quality max, one asset per image)

| Line | Credits |
|---|---|
| Balance now (`balance`, 2026-11-04 ≈06:05; plus plan) | **46.88** |
| Protected floor (ART-UPGRADE-PLAN §1.3: FX-JUICE spent its 220 and the frame outpaint is done, so only SPEC4 P4-10's 40 remains) | 40 |
| **L0, fits now:** SPEC4 A4 gacha bundle ×3, A5 shop crack, A6 yarn death + rust stain + moth hole, with up to 2 re-rolls | **≤ 45** |
| **L1–L7, needs the top-up:** every *specced* asset still missing (SPEC4 at max, ART-UPGRADE remainder, FX remainder, SPEC5 core, eight R2 card banners) | **≈1,000** (999 with every reserve spent) |
| L8 (optional, specced): SPEC5 optional set, ART-UPGRADE OPT extras, enemy hurt faces | ≈351 |
| L9–L10 (SPEC6, not specced): content cadence + wave C ≈455 (CR4-22: +≈60 for the new lineages' FX-JUICE impact sprites and skill icons), wave D ≈640 | ≈1,095 |

- **Recommendation to pass to the user.** One **1,000-credit pack** covers every specced asset (L1–L7). It leaves about 1 credit spare even if every re-roll reserve is spent. The eight batches delivered so far spent 43 of their 67 planned reserve credits, so the real margin is likely larger. A second top-up of ≈1,450 (L8–L10) can wait until SPEC6 is written (RM-7).
- **The ask has grown.** The user was asked about ≈675 credits (ART-UPGRADE-PLAN §1.3). Since then the scope has added SPEC5 core art (≈341), the FX-JUICE remainder (40) and the R2 card banners (48), while B5 came in 14 under plan. The ask for everything specced is now ≈1,000.
- **Without a top-up:** only L0 is made. Every SPEC4 and SPEC5 feature ships on its code fallback. The six new forms cannot ship, because SPEC5 has no shipping fallback for them, so RD-7 stays off.
- **Audio costs 0 credits.** The optional voice barks are Higgsfield speech, which needs a terms check by the user. They are priced with `get_cost` before any job.

### 0.3 Decisions

**Taken by this roadmap (the orchestrator may veto any of them):**

| ID | Decision | Why |
|---|---|---|
| RD-1 | **The curve package lands first, in RM-1, as P4-C (data only).** It adds the second enemy taper at 151 (HP ×1.02, damage ×1.015), sets `goldPerSecondGrowth` 1.06 and the basket's `stageGrowth` 1.0025. The power-rating repair (S5-24) lands in RM-2: P4-0 adds the fields, P4-1 the formula, P4-8 a three-segment fit. SPEC5's R2P-C becomes a confirmation step | The shipped economy makes every later number meaningless: 단추/s reaches 9e9 by the end of day 0, and players earn 13.4k 별조각/week against a 2,640 target (PROGRESSION-SIM §1). SPEC4's own calibration and budgets should run on the fixed curve, not be redone in SPEC5 |
| RD-2 | **SPEC4 stretch items are built, not pre-cut.** They are Q-06, the PRG-06 lost chest, PRG-07, EVT-02, NEW-03, NEW-04 and the FX-05 iOS backend; each comes last in its package's order | SPEC5 builds on NEW-03, NEW-04, PRG-07 and Q-06. If they were cut, SPEC5 would rebuild them (R2-15, R2-07, R2-10 at L+, R2-36a season costumes). They stay cut-safe behind their FeatureIds if RM-2 overruns |
| RD-3 | **P4-3a is withdrawn; AUD owns the audio engine.** P4-10 is art only, extended with the ART-UPGRADE-PLAN remainder and the FX-JUICE remainder | AUDIO-SPEC §0.3 already replaced P4-3a, `BattleSfx` and SPEC4's clip route. The engine exists in `Scripts/Core/Audio` (16 files) |
| RD-4 | **WP-9 (UX wave 3) runs in RM-1, in parallel with P4-0** | Its files are disjoint from P4-0's, and the RM-2 UI packages need its `UiSceneLintTests` before handoff |
| RD-5 | **Every planned new art name gets a fallback-safe `UiArt` accessor in RM-1.** The RM-2 UI packages then do the ART-UPGRADE §4.3 wiring steps whether or not the art exists yet | Several files that wire the new art have no owner after RM-3: `SettingsSceneBuilder`, `SettingsPopup`, `SynergyCodexPopup` and the kit. So the wiring must happen in RM-2, but the art can arrive later. A rebuild then picks it up with no code change |
| RD-6 | **Eight R2 unlock-card banners are added to R2P-6.** They are Outings, LevelBreak, Techniques, DailyRounds, Tower, NightWalk, Keepsakes and Workbench | SPEC5 §9.1 has none. Every SPEC4 card with `Card = on` has a banner (ART-UPGRADE B8a/B8b), so without these the R2 cards would be the only bare ones |
| RD-7 | **Content first.** R2-37 place 2 and R2-36b's six new forms move from B2 into RM-6, if their art is funded by the time RM-6 starts. Otherwise they stay in RM-7 exactly as SPEC5 has them | These are what the player sees on the main screen and in the collection. Both depend only on wave A (magpie/nest art, cap breaks) and on B1's rule effects. The move stays inside the same package owners (R2P-1, R2P-5, R2P-6) |
| RD-8 | **One snapshot per gate.** Robocopy Assets/Packages/ProjectSettings to `scratchpad/snapshots/G<n>/` (≈200 MB) before a wave's first merge, and keep the last three | The project is not a git repository. A snapshot is the only rollback |
| RD-9 | **A SPEC5 critic pass runs in RM-3,** against the real SPEC4 code. **SPEC6 is written during RM-7** (content cadence + waves C/D), followed by a critic pass | SPEC5 was written against SPEC4 as specced, before any of it existed. SPEC6 needs SPEC5's real engines |
| RD-10 | **Overlap mode.** R2P-0 is staged during RM-3 as anchor-checked patch scripts (the `ia/wpia_patch_battle.py` pattern) and applied after G3. Optionally, R2P-2 starts B2's new system files in RM-6 | This saves about 2 days on the critical path (plus about 1 with the optional part) without two owners on one file |
| RD-11 | **Credits are spent in ladder order (§7.2),** after `balance ≥ cost + floor`. The user hears the totals once, when the top-up is answered, never mid-wave | The memory rule and SPEC5 §9.1 |
| RD-12 | **Each gate owner bumps `IdleAutoSetup.SceneVersion` after the copy verifies green** (one line) | The memory rule: the user's open editor then rebuilds data, UI and Main.unity by itself |
| RD-13 (CR4-01) | **SPEC4's chapter-chest default row (ch6+) pays 별조각 only on chapters divisible by 5 (100); the materials stay on every chapter.** P4-8 changes the row in RM-2; P4-1 adds the budget row | SPEC4 B.2 pays 별조각 150 on every first x-10 clear from ch6 on, and its budget row covers ch1–5 only. With R2-04 the engaged player clears 6.5–10.5 chapters a week in weeks 2–4 (sim runs `R_REC_r2`), so that row alone would add ≈975–1,575 별조각/week, more than all of R2 together |
| RD-14 (CR4-02) | **Economy gates use measured numbers.** The harness logs 별조각 per source from RM-3 on; G3 records the measured R2-off baseline; G5–G7 use the re-based bands of SPEC5 §4.4 (week 4: 2,800–3,800, week 8: 2,600–3,600), with the C-01 gem-line cap (1.5 → 1.25 → 1.0) as the first knob | SPEC5's 2,600–3,400 band rests on SPEC4's modelled 2,640/week. The harness already measures 2,399/week from the pre-SPEC4 sources alone and 3,609 with R2-04/05 (no C-01), so the band failed before any SPEC4 or R2 source was added |
| RD-15 (CR4-06) | **자동 편성 v2 in RM-5:** `FormationSystem.AutoFormation` ranks by the offence power that `BuildBiased` uses (style damage scale and 공명 included) | Sim §7.3: AutoFormation-only players reach ch25 on day 20.3 instead of 13.3 and stall ≈150 stages lower, and SPEC4's 강해지기 row pushes players onto it |
| RD-16 (CR4-10, CR4-11) | **Side battles always run in MAIN.** Every live start calls `UINav.GoToMain()` first; while a side battle runs, a `UIManager.PushBackHandler` handler turns Android back into the abandon confirm once no popup or sheet is left | Started from a hub sheet, the fight would run under the sheet with the deck, lamp and side bar covered. IA §4.7's back order ends in the exit confirm |
| RD-17 (CR4-09, CR4-14) | **The ≡ menu belongs to the owner of `FrameSceneBuilder` + `MenuPopup` in each wave** (P4-4 in RM-2, R2P-4a in RM-5, R2P-4c in RM-6/7). The grid has ⌈tiles/3⌉ rows and the window grows with it (`300 + 220·rows`) | P4-6 was given "≡ menu row 3 tiles" in P4-4's files, and SPEC5 sized the menu at 8 tiles / 3 rows without SPEC4's row-3 tiles (UX-SPEC-IA §8.8) |

**Needed from the orchestrator or the user.** Each has a default, so no wave blocks on it.

| ID | Question | Who | Default if unanswered | Needed by |
|---|---|---|---|---|
| Q1 | The art top-up: ≈1,000 for every specced asset (a 1,000-credit pack), and later ≈1,450 for optional and SPEC6 art | user | only L0; fallbacks everywhere; RD-7 off | G1 (the earlier, the more art lands inside its code wave) |
| Q2 | Move SPEC5's pacing core (`CounterKey.PlayMinutes = 57`, `FeatureUnlockDef.alsoRequire`, 180 s card spacing) into P4-0, so SPEC4's own cards are spaced too; confirm the floors 20/30/45/90/150/200/270 min (SPEC5 App. D #3) | orchestrator | move it; spacing on for every card; floors as SPEC5 | RM-1 start |
| Q3 | The power-rating repair, which changes SPEC4 PRG-01's formula (SPEC5 App. D #4) | orchestrator | yes, in RM-2 | RM-2 start |
| Q4 | Shell exceptions: **SX-1**, the IA sheet sounds in `MainTabController`/`SheetController`, only if WP-IA has not added them; **SX-2**, the kit hooks for the B3b art (`UITheme.TabStyle.Apply`, `SceneParts.ButtonStyle.Soft`, new `UI/Common/TabSkin.cs`); and SPEC5's hub-sheet extension (App. D #1) | orchestrator | approve all three (each is member-listed and fallback-safe; the hubs have a popup fallback) | RM-1 / RM-4 |
| Q5 | Overlap mode (RD-10) | orchestrator | yes for R2P-0 staging, no for the B2 pre-work | RM-3 |
| Q6 | Content first (RD-7) | orchestrator | yes, if Q1 is answered by RM-6 | RM-6 |
| Q7 | A weekly limit of 5 on 여행 가방 조각 (SPEC5 App. D #5) | orchestrator | yes, applied by R2P-5 before B1 ships | RM-6 |
| Q8 | One season costume per Q-06 season, ≈5 credits a month (SPEC5 App. D #6) | user | yes | RM-7 |
| Q9 | SPEC5's optional B items (App. D #7) | orchestrator | 8 more guests: yes, if L8 is funded; 3 mode-source costumes: later; rider option B: no | RM-7 |
| Q10 | For the SPEC4 owner (App. D #8): the backup-reminder mail after 14 DayKeys without a backup, and the shared AutoChallenge/ExpeditionBatch card (S5-4) | orchestrator | both in RM-2 (P4-2 + P4-8) | RM-2 |
| Q11 | The new-lineage cadence: two in RM-8 ('한 짝 장화' [비·밤], '풍경' [비·소리]), then one every 4–6 weeks after launch at ≈30 credits each (IDLE_RESEARCH2 open question 4) | user | two in RM-8, cadence decided later | RM-7 |
| Q12 | Voice barks: confirm the Higgsfield preset-voice terms. Also pick audio takes by ear in **IdleGame/Audio/사운드 시험장** | user | barks stay off (silent); the recommended takes stay active | any time |
| Q13 | Send the original concept-sheet files. They make the OPT upscales unnecessary (ART-UPGRADE-PLAN §7 #4) | user | no upscales | RM-2 |
| Q14 | Authorize SPEC6 (content cadence + waves C/D) and its art (L9–L10) | orchestrator / user | SPEC6 is written; RM-8 waits for the answer | G7 |
| Q15 (CR4-05) | Make **단추 N분치** follow the player's real income. `GoldMinutes` today = 수선점 단추/s × 60 × N. Stage clears already earn ≈37× the shop rate per online minute on day 1, and after the CR-07 retune they are ≈100% of 단추 from week 2, so "단추 60분치" falls from 3% of a day's 단추 on day 1 to 0.003% on day 13 and ≈0 from day 20 (sim `R_REC_r2`). Errand 1's whole payout, the guest/tower/walk/lantern 단추 lines and every 단추-minute price (workbench, polish, rename, market, merge; later decor) stop mattering. Fix: SPEC5 §4.6 (`IncomePerSecond` = shop 단추/s + clear 단추 at the best stage ÷ a reference clear time of 180 s; the night basket pays 단추 N분치 per hour away). Early game moves only 5–15% (the shop rate dominates below ≈stage 30); R2P-5 then tunes the reference time and the R2 N values against projection #2 | orchestrator | yes: hook in R2P-0 (RM-4), implementation in RM-5, gated by projection #2 | RM-4 |
| Q16 (CR4-07) | **The ceiling plan.** The curve moves the wall to ≈ch55 (stage 535–571, days 49–71 for the engaged player); it does not remove it, and RD-7's six forms add only ≈5–15 stages. Pick: (a) accept the ceiling and schedule post-launch drops against the measured ceiling day (a new lineage or tier every 4–6 weeks, each worth ≥ +15 stages), or (b) also have SPEC6 evaluate the sim's alternative, a compounding row with a milestone multiplier (it needs chapters 12–17 re-checked) | orchestrator / user | (a), and SPEC6 evaluates (b) with the harness | G7 |

### 0.4 Top risks (full list in §8)
1. **Credits.** The balance is 46.88 and the specced art needs ≈1,000 more. Without the top-up, features ship on fallbacks.
2. **No version control** while about 10 agents edit one working tree. Mitigation: one owner per file, lock files, staged patches, snapshots.
3. **Spec drift.** SPEC4 read the code on 2026-11-03 at 23:30. UX wave 2, WP-IA, FX-JUICE and the audio engine have changed many of those files since. SPEC5 assumed a SPEC4 that does not exist yet.
4. **Scope.** About 145 specced agent-days, plus about 82 for SPEC6.
5. **Economy drift and first-session overload.** Chapters 1–10 take 34 minutes, so SPEC4 and SPEC5 together would fire about 19 unlock cards in the first hour without pacing. (CR4-01/02) Unfixed, the projected week-4 별조각 income is ≈4.6–4.9k against a planned ≈2.85k, because two stage-indexed faucets (SPEC4's ch6+ chapter chest and the shipped shop-level mail) ride R2-04's chapter pace; RD-13 and RD-14 bring it to ≈3.3–3.8k.
6. **(CR4-05) 단추 N분치 is worthless from week 2** on the retuned economy unless Q15 lands; errand 1 and every 단추-minute price would be decorative.

---

## 1. Starting point (2026-11-04, ≈06:05)

| Workflow | State | Evidence (paths under `Assets/_Project` unless noted) |
|---|---|---|
| UX wave 1 / 1b | merged | `UI/Common/{CostButtonView, CurrencyStripView, InfoTooltipView, ReturnChipView}.cs`, `UI/Popups/ShortfallPopup.cs`, `Systems/SourceAvailability.cs` |
| WP-IA (wave 1c) | implemented, acceptance pending | `UI/UIFrame.cs`, `UI/Main/{SheetController, SheetDragHandle, MenuPopup}.cs`, `UI/Meta/{QuickRailView, QuickIconView, RedDotAnyView}.cs`, `UI/Battle/BattleFrameDriver.cs`, `Editor/Builders/FrameSceneBuilder.cs`, `Tests/PlayMode/UiFrameTests.cs` (05:40); B5 frame art wired through `UiArt.{TopBar9s, NavBar9s, NavMainButton, SheetFrame9s}` |
| UX wave 2 (WP-4…WP-8) | next | no `Hud/BossChallengeButton` in `Scenes/Main.unity`; no `UI/Meta/StrengthenPopup.cs` |
| UX wave 3 (WP-9) | not started | no `Tests/EditMode/UiSceneLintTests.cs` |
| FX-JUICE | art delivered, integration in progress | `fxj_art/report.json`: 220 credits, 42 of 49 assets; not made: 6 decals and `fb_light_burst_4x4` (cut by the cap); `Art/FX/Juice` 20 files and `Art/VFX/Juice` 22; `fx_proj` modified at 05:52 |
| ART-UPGRADE-PLAN | W0, W1, B8a and B5 delivered | `artmax/qa_report.md` (90 replacements: 0 regressions, 81 kept, 8 conditional on the zone importer change, which is applied as importer v7, 1 kept after a further QA pass); B5 installed 04:00 (28 credits, not 42); remaining: B9, B3b, B10, B7b, B6b, B8b, OPT |
| Audio | engine being written; most clips made | `Scripts/Core/Audio/*` (16 files, edited at 06:02); `Audio/Sfx` 644, `Audio/Bgm` 10, `Audio/Amb` 30 takes; UISfxKey 81 members; reports: UI 75 keys (156 takes), battle 174 keys (448 takes, 155 "ears needed"), music 10 pieces, 19 stinger keys; 0 credits spent |
| SPEC4 | not started | no `Core/AppSignals.cs`, no `Systems/Meta/IMetaModule.cs` |
| SPEC5 | written, not reviewed | `SPEC5.md` (1,789 lines); its start conditions §0.3 |
| Progression sim | done | `phase4b/PROGRESSION-SIM.md`; 227 runs; harness in `phase4b/sim/` |
| Higgsfield | 46.88 credits | `balance` (530.38 before the FX and art jobs) |
| Scene stamp | `IdleAutoSetup.SceneVersion = 1` | `Editor/IdleAutoSetup.cs:21` |

---

## 2. Rules for the whole roadmap

### 2.1 Ownership (SPEC4 §9.2, reused)
1. **One owner per file per wave.** The owner is listed in §5. A file passes to the next owner only at a gate. A package that needs someone else's file reports the need; it does not edit the file.
2. **Contract first.** Each spec starts with its contract package (P4-0, R2P-0, S6P-0), which lands enums, save, defs, hooks and **inert stubs** alone. Stubs never throw, because GameBootstrap's kill switch turns off all meta on any exception.
3. **Append-only enums with explicit values.** SPEC4's appends come first, then SPEC5's (their numbers are already fixed, e.g. `CounterKey.PlayMinutes = 57`), then SPEC6's. Reserved numbers are never reused.
4. **Lock files.** A package doing multi-file edits writes `scratchpad/locks/<package>.lock` and deletes it when done. `verify.ps1` waits for live locks before copying.
5. **Staged patches for overlap.** Work that must start before its files are handed over is written as re-runnable, anchor-checked patch scripts in the scratchpad (`phase5/r2p0/`) and applied after the gate.
6. **Handoff note.** Every package ends with a list of the files it touched, its compile result and its open requests (`scratchpad/<phase>/handoff/<package>.md`).

### 2.2 Value first
- Inside each package: P0, then P1, then P2/stretch. First-session items come before later-week items.
- Across waves: the dependency order (§6), with RD-7 pulling the visible content forward.
- Credits follow the ladder (§7.2): the art that new features need comes before polish of art that already works.

### 2.3 Art
- `gpt_image_2_5`, quality **max**, one asset per image, 1k = 5 credits and 2k = 9. Image edits are priced like generations. Outpaint costs 2.
- Every batch follows ART-UPGRADE-PLAN §2:
  - the prompt assembly `[MASTER] + style block + SUBJECT + REFERENCES`, with English names only;
  - references by job id;
  - `generate_image_batch` (≤ 12), then `jobs_wait`, then one `show_generation_by_ids`;
  - the "excellent or regenerate" QA gate, with at most 2 re-rolls;
  - staging under `scratchpad/<phase>/art/<batch>/` with a `jobs.tsv`;
  - backups of every replaced file.
- **Balance rule:** before every submission, `balance ≥ cost + floor`. The floor is the protected amount of the next rung of the ladder that a running wave needs.
- **Fallback first:** every asset except the six new forms has a code fallback, so no code waits for art.
- **Accessors before wiring (RD-5):** new names get fallback-safe `UiArt` accessors before the builders reference them.

### 2.4 Audio
- AUD (AUDIO-SPEC) owns the engine, the catalog, the clips and `Audio*Tests` in every wave. File owners wire the call sites in their own files (AUDIO-SPEC §8.1), and AUD never edits them.
- New keys are appended to the catalog. `UISfxKey` is append-only.
- With no clips the game is silent and logs no errors, exactly as today.

### 2.5 Gates
Every gate runs the common checks below. The gate-specific checks are in §6.2.
1. Every package: `python -I scratchpad/compile_check.py` prints `COMPILE OK`, and the allwarn check shows 0 new warnings in its files.
2. Merges follow the wave's order, with a compile check after each one.
3. Take the snapshot (RD-8).
4. Run `scratchpad/verify.ps1 -Stage all` on `verify_proj`:
   1. copy;
   2. art reimport;
   3. `IdleProjectSetup.BatchBuildAll`;
   4. EditMode tests (TestRunnerApi, `runSynchronously`);
   5. a dev Windows player;
   6. AutoShot captures.

   Batch runs always use `-quit` because of the license quirk. PlayMode checks go through the dev player plus AutoShot.
5. The save-migration check of that wave (v3 → v4 at G1/G3, v4 → v5 at G4/G5) on a copy of `ux_shots/autoshot_player.json`, with `GameBootstrap.SavePathOverride` set so the user's save is never touched.
6. The economy and projection checks of that wave.
7. Bump `SceneVersion` (RD-12) and write the gate report `scratchpad/roadmap/gates/G<n>.md`.

---

## 3. Deltas every package applies (consolidated)

Specs were written at different times. This table lists every change since they were written, and where it applies. A package re-reads its own files at its start (they have moved since the specs quoted their lines) and applies the deltas in its rows.

| # | Delta | Source | Applies to | Wave |
|---|---|---|---|---|
| 1 | The curve package lands as **P4-C** in RM-1. Contents: `stageGrowthTaperStage2 = 151`, `stageHpGrowthLate2 = 1.02f`, `stageDamageGrowthLate2 = 1.015f` with the three-segment `TaperedScale` (PROGRESSION-SIM §8.1 code); `goldPerSecondGrowth` 1.06 in both the C# default and the `GameConfig.asset` YAML; `IdleChestConfig.stageGrowth` 1.0025. SPEC5's R2P-C only confirms | PROGRESSION-SIM §0, §8; SPEC5 §0.3 #2; RD-1 | P4-C, P4-9, R2P-C | RM-1, RM-4 |
| 2 | Power-rating repair (S5-24), split across three packages: P4-0 adds the `PowerConfig` fields (`shopShareCap`, `recommendedTaperStage2`, `recommendedGrowthLate2` next to SPEC4's two-segment fields); P4-1 implements the shop-share cap and the three-segment rec in `PowerSystem`; P4-8 fits all three segments in `PowerCalibration` | SPEC5 §4.3; App. D #4; Q3 | P4-0, P4-1, P4-8 | RM-1, RM-2 |
| 3 | CR-07's 15–30 s chapter-1 band test is replaced by the projection checks (SPEC5 §4.4) run with the sim harness. `EconomyFlowTests` stays as a smoke test without the band | SPEC5 §4.4; PROGRESSION-SIM §8.4 | P4-9 | RM-3 |
| 4 | P4-3a is withdrawn: no `AudioBus` in `GameEnums`, no `AudioSystem`/`AudioKeys` stubs. `BattleSfx.cs` and its tests are dropped from P4-3b, because `BattleAudioDirector` listens to `Game.Battle`. UI pitch jitter stays ±5 % | AUDIO-SPEC §0.3; RD-3 | P4-0, P4-3b | RM-1, RM-2 |
| 5 | P4-10 is art only, at max singles. It also takes the ART-UPGRADE-PLAN remainder (it may replace existing files, with backups) and the FX-JUICE remainder | AUDIO-SPEC; ART-UPGRADE-PLAN §7 #3; RD-3 | P4-10 | RM-1…RM-3 |
| 6 | SPEC4 §0.2 rule 6 (30.38 credits, 2×2 sheets, high quality) and §10.1's budget plan are replaced by §7 here. SPEC4 §10.2 audio is replaced by AUDIO-SPEC | ART-UPGRADE-PLAN §1.3; memory | P4-10, AUD | all |
| 7 | SPEC4 §3.1's T3 skill numbers are delivered by FX-JUICE with the same API. P4-3b only verifies them | FX-JUICE §0.4 | P4-3b | RM-2 |
| 8 | PerfProbe reads the `BattleFx` budgets, not `GameSettings.ParticleBudget`. The atlas builder includes and excludes folders per FX-JUICE §3.7 | FX-JUICE §3.4, §3.7 | P4-9 | RM-3 |
| 9 | The BuildAll order loses `AudioTableBuilder`. `AudioDataBuilder` runs from AUD's one line in `IdleDataBuilder.BuildContent` | AUDIO-SPEC App. A | P4-9 | RM-3 |
| 10 | IA frame: the CR-03 `StatsButton` is the R1 rail cell. P4-6 builds the cell; P4-3b owns the popup and `UINav.BattleStatsRequested`. The EventChip no longer exists: event entry is the `EventButton` rail icon with its D-n SubPill. EventHub, SeasonPass, Codex, Achievements and Coupon open from row 3 of the ≡ menu. `TopBarView` targets the compact bar. `GameSettings.SheetAutoReturn` already exists | UX-SPEC-IA §8.8 | P4-3b, P4-4, P4-6, P4-0 | RM-2 |
| 11 | `Editor/Builders/FrameSceneBuilder.cs` and `UI/Main/MenuPopup.cs` belong to **P4-4** in RM-2. IA handed them to WP-4, and SPEC4 predates them | UX-SPEC-IA §7.1 | P4-4 | RM-2 |
| 12 | The AUDIO-SPEC §8.1 call sites are wired by their file owners. This includes the "later features" keys (AchievementClaim, CodexDiscover, MapNode, TierUp, BlessingPick, RewardBig, Countdown, ProgressFill, DefeatAdvice), `PushMusic`/`PopMusic` for gacha, dungeon and story, and `SetBattleFocus` for full-screen evolution | AUDIO-SPEC §8.1 | P4-3b, P4-4, P4-5, P4-6 | RM-2 |
| 13 | Settings: the `GameSettings.VoiceBarks` key comes from P4-0 (§2.10 keys are P4-0's). The '캐릭터 목소리' row, re-enabling the BGM/SFX sliders when `UISfx.HasClips` and the `SliderTick`/`Toggle*` sounds come from P4-4 | AUDIO-SPEC §8.2 #1 | P4-0, P4-4 | RM-1, RM-2 |
| 14 | AUD subscribes to `AppSignals.SleepChanged` and `GachaRevealHigh` once P4-0 has landed them | AUDIO-SPEC §8.2 #4 | AUD | RM-2 |
| 15 | (If Q2) The pacing core lands in P4-0: `CounterKey.PlayMinutes = 57` with the per-minute increment in `MetaSession.Tick`, `FeatureUnlockDef.alsoRequire` (AND), and `FeatureGateSystem.CardSpacingSeconds = 180` with `CardReady` (this needs an exception on a frozen file). P4-6's presenter asks `CardReady`. P4-8 applies the spacing to SPEC4's cards. R2P-0 then adds no gate logic, and R2P-5 adds the R2 pacing rows | SPEC5 §2.8, S5-3 | P4-0, P4-6, P4-8, R2P-0, R2P-5 | RM-1, RM-2, RM-4, RM-5 |
| 16 | S5-4: the AutoChallenge card text covers ExpeditionBatch ('이제 보스 도전과 원정 다시 보내기를 친구들이 알아서 해요'), and the 빠른 수선권 1 gift moves to it. ExpeditionBatch's `showCard = false`, so the B8b `feature_expedition_batch_banner` is skipped (−5 credits; its medal stays) | SPEC5 S5-4; App. D #8; Q10 | P4-8, P4-10 | RM-2 |
| 17 | (If Q10) A backup-reminder mail after 14 DayKeys without a backup (MailSystem, local) | merge log #105; App. D #8 | P4-2 (rule), P4-8 (copy) | RM-2 |
| 18 | The ART-UPGRADE §4.3 wiring steps are part of the RM-2 UI packages. The accessors come from P4-10 in RM-1 (RD-5). Who does what: P4-4 does #8 (`MenuSceneBuilder.Skin`), #9 (settings and synergy-codex tabs, `SettingsPopup`, `SynergyCodexPopup`), #10 (bag header) and #11 (power icon in the top bar); P4-5 does #8 and #9 (`CharacterSceneBuilder`); P4-6 does #4, #5, #6 and #9 (attendance cell, switch, gold tag, tab button, `JournalPopup`/`AttendancePopup`); P4-3b does #17 (nameplate); P4-8 does #3 (mission_kill/boss) | ART-UPGRADE-PLAN §4.3 | P4-3b, P4-4, P4-5, P4-6, P4-8, P4-10 | RM-1, RM-2 |
| 19 | P4-0 shell exceptions (if Q4). **SX-1** (only if WP-IA has not done it): `UISfx.Play(SheetOpen/SheetClose 0.6/MainButton)` plus `AudioService.SetBattleFocus(Dimmed, owner)`/`ClearBattleFocus(owner)` in `MainTabController`/`SheetController`, ≤ 12 lines. **SX-2:** `UITheme.TabStyle.Apply` swaps sprites when a `TabSkin` is present (otherwise today's tint); `SceneParts.ButtonStyle.Soft` uses `UiArt.ButtonSoft9s` (fallback parchment); new `UI/Common/TabSkin.cs` | this roadmap; AUDIO-SPEC §8.2 #2 | P4-0 | RM-1 |
| 20 | SPEC4 stretch is built in RM-2 (RD-2). The SPEC4 rule "wave 4.2 verifies before stretch merges" becomes: stretch code merges with its FeatureId locked until RM-3 verifies it; anything not done at G3 stays locked and gets a status for SPEC5 §0.3 #3 | RD-2; SPEC4 §9.1 | P4-1, P4-2, P4-3b, P4-4, P4-6, P4-7, P4-8 | RM-2, RM-3 |
| 21 | P4-7 adds `IdleGame.Platform` to `compile_check.py`'s assembly table once Unity has regenerated the csproj on the copy (until then the dotnet check cannot see the package) | SPEC4 risk #7 | P4-7 | RM-2 |
| 22 | SPEC5 v1.1 deltas from the RM-3 critic pass (it checks SPEC5's hooks against the real SPEC4 APIs) | RD-9 | all R2P-* | RM-3, RM-4 |
| 23 | **SPEC5 ownership gap:** `Editor/Builders/MenuSceneBuilder.cs` has no phase-5 owner, but it builds the 강화 tab (`BuildGrowthPanel`: the `GrowthPanel/TechniquePage` overlay and the [수선 비법] segment), the shop (weekend market section), the bag ([엮기] tab) and the expedition panel (route marks). It goes to **R2P-4a in RM-5**, then **R2P-4c in RM-6/RM-7** | this roadmap (code: `MenuSceneBuilder.cs:66`) | R2P-4a, R2P-4c | RM-5…RM-7 |
| 24 | R2 card banners (RD-6): R2P-5 adds the eight FeatureIds to `MetaContent.FeatureArtName` and passes `illustration: FeatureBanner(id)`; R2P-6 makes `Meta/feature_{outings, level_break, techniques, daily_rounds, tower, night_walk, keepsakes, workbench}_banner.png` | RD-6 | R2P-5, R2P-6 | RM-5…RM-7 |
| 25 | Content first (RD-7): the place-2 rows (`PlaceDef`, the two alley enemies, `boss_bell`, the 야시장 rule) and the six forms (`Evolve(...)` tier-2 rows, skills) move to RM-6. So do R2P-1's place spawns, the bell boss and `BattleArt` place field, and captures 86–88 (`new_form_tree`, `place2_field`, `place2_boss_hint`) | RD-7; SPEC5 §6.9 | R2P-1, R2P-5, R2P-6, R2P-7 | RM-6 |
| 26 | The candy limit (Q7): the event shop's 여행 가방 조각 is limited to 5 a week before B1 ships | SPEC5 App. D #5 | R2P-5 | RM-6 |
| 27 | AUD for SPEC5: append `MusicState.Guest = 7, Tower = 8, Walk = 9`; promote `bgm_worldboss` (re-voiced tender-urgent, for Guest), `bgm_tower` and `jingle_tower_floor`; `BattleAudioDirector` re-reads `Game.Battle.Sim` on `BattleRestarted` and follows `AppSignals.SideBattleChanged`; add event→key rows for `ObjectivePhase`, `ObjectiveHop` and the place boss | SPEC5 §9.2, §8.4 R2P-1 risks | AUD | RM-4, RM-5 |
| 28 | Each gate owner bumps `SceneVersion` (RD-12) | memory | P4-0, P4-9, R2P-0, R2P-7, S6P-7 | every gate |
| 29 | **(CR4-01) Chapter chest, ch6+ default row:** 별조각 only on chapters divisible by 5 (100); 기억의 실 6 · 오르골 태엽 2 · 달빛 조각 1 stay on every chapter. New `EconomyBudgetTests` row "chapter chests ch6+, 별조각 per 10 chapters ≤ 200" (SPEC4's ch1–5 row unchanged). The R2-16 box hunt (RM-9) keeps living in this popup | SPEC4 App. B.2, §11.2; sim runs `R_REC_r2` (6.5–10.5 chapters/week in weeks 2–4) | P4-8 (row), P4-1 (test) | RM-2 |
| 30 | **(CR4-02) Measured economy.** P4-9 adds per-source 별조각 columns to the harness `daily.csv` (journal, attendance, basket, quick idle, mail incl. shop-level mail, chests, codex, records, affinity, events, season, R2 modes) and records the R2-off week-4/week-8 baseline at G3. G5, G6 and G7 use SPEC5 §4.4's re-based bands. If over: C-01 gem-line `maxStageMultiplier` 1.5 → 1.25 → 1.0 (content), then `GameConfig.levelRewardGem` 50 → 30 | sim `analysis/report_tables.md` (2,399 R2 off / 3,609 R2-04/05 in week 4); SPEC4 §11.2 (modelled 2,640) | P4-9, R2P-5, R2P-7 | RM-3, RM-5…RM-7 |
| 31 | **(CR4-03) R2 guard window.** "Week 4+" means the mean of weeks 5–8 (≤ 250 별조각); week 4 itself is reported with a ≤ 350 burst limit, because errand milestones and tower trial floors track a stage that still climbs ≈85 stages a week in week 4 | SPEC5 §4.5; sim stage 262 on day 14, 457 on day 30 | R2P-7 | RM-5…RM-7 |
| 32 | **(CR4-04) SPEC4 budget rows vs R2 content.** SPEC4's lifetime rows (records ≤ 3,500, codex ≤ 2,700) count SPEC4 ids only (`MetaContent` tags the R2 rows), and the R2 block owns the R2 rows. SPEC5's "Q-04 R2 lines" row becomes ≤ 1,000 so RM-9's 15 hidden records (+150) fit | SPEC4 §11.2; SPEC5 §10.2; IDLE_RESEARCH2 §9.1 | R2P-7, R2P-5 | RM-5 |
| 33 | **(CR4-05, if Q15) 단추 N분치 follow real income** (SPEC5 §4.6): R2P-0 adds `MetaRewardService.IncomePerSecond`, `MetaDatabase.income` and `IdleChestConfig.offlineGoldMinutesPerHour`; R2P-1 exposes `BattleSystem.ClearGoldAt(int)`; R2P-3 resolves `GoldMinutes` through the hook; R2P-2 pays the night basket's 단추 line in 단추 minutes; R2P-5 tunes the two knobs from the harness; R2P-7 models them in projection #2 | sim `R_REC_r2` (60분치 = 3% of a day's 단추 on day 1, 0.003% on day 13); `MetaRewardService.cs:10`; PROGRESSION-SIM §6.1 | R2P-0, R2P-1, R2P-2, R2P-3, R2P-5, R2P-7 | RM-4, RM-5 |
| 34 | **(CR4-06) 자동 편성 v2.** `FormationSystem.AutoFormation` keeps its role and pad order but ranks by `PowerSystem.GetFormPower` (offence with style damage scale, 공명, costume and embroidery; HP only breaks ties). SPEC4's 강해지기 `AutoFormation` row compares offence. Projection #2 adds the `auto` player variant | PROGRESSION-SIM §7.3 | R2P-3, R2P-7 | RM-5 |
| 35 | **(CR4-08) The SPEC5 §4.4 projection targets bind at G5, G6 and G7**, not only at G5. Wave B adds power the sim never modelled (keepsakes, workbench, riders, new forms, codex bonuses); if a run breaks a band, the knobs are, in order, keepsake values, workbench `perLevel`, rider values, codex percentages | PROGRESSION-SIM §8.5 ("re-run the harness when rows change") | R2P-5, R2P-7 | RM-5…RM-7 |
| 36 | **(CR4-09, CR4-14) ≡ menu and event entry.** The ≡ tiles live in `FrameSceneBuilder` + `MenuPopup`, so their owner builds them: SPEC4's row-3 tiles (EventHub, SeasonPass, Codex, Achievements, Coupon; UX-SPEC-IA §8.8) are **P4-4's**, not P4-6's, and P4-6 provides the receivers. Event entry splits the same way: the rail `EventButton` route and its D-n `SubPill` are P4-6's (`QuickIconView`, `MetaSceneBuilder`), the `EventTile` route is P4-4's. Rows = ⌈tiles/3⌉, window `300 + 220·rows`; `AutoShot.MenuTileOrder` is updated by the verify owner | UX-SPEC-IA §3.6, §8.8; `FrameSceneBuilder.cs:222–245`; `AutoShot.cs:75` | P4-4, P4-6, P4-9, R2P-4a, R2P-7 | RM-2, RM-5 |
| 37 | **(CR4-15) RM-1 merge order:** P4-10's `UiArt` accessors merge first, because P4-0's SX-2 (`SceneParts.ButtonStyle.Soft`) compiles against `UiArt.ButtonSoft9s` | §4.1 | P4-0, P4-10 | RM-1 |
| 38 | **(CR4-16) Art lane after G3:** R2P-6 inherits P4-10's unfinished rungs (L1, L2, L4, L7) in ladder order, and S6P-6 inherits R2P-6's. The accessors from RM-1 (RD-5) keep the wiring valid, so late art needs no code | §5 (Art lane row); §7.2 | R2P-6, S6P-6 | RM-4+ |
| 39 | **(CR4-10, CR4-11) Side battles in the frame.** Every live start (errand, guest, tower, walk) calls `UINav.GoToMain()` before `TryBegin*`; the result card's [확인] reopens the hub page as specified. While `IsSideBattle`, `SideBattleBarView` pushes a `UIManager.PushBackHandler` that consumes back only when no popup is open and no sheet is open, and then shows the abandon confirm of SPEC5 §2.6 | UX-SPEC-IA §4.5, §4.7; `UIManager.cs:443` | R2P-4a | RM-5 |
| 40 | **(CR4-12) Place rule on the HUD:** the StagePanel (330×134, M11) has no free slot and the SideHud covers it in side battles, so the place sign is a second 40 px icon inside the SynergyRow's modifier chip (tooltip = the rule). The SynergyRow ≤ 540 px check stays | UX-SPEC §2.1.3, §2.1.6 | R2P-1 | RM-6 |
| 41 | **(CR4-20, CR4-21) Place sound and errand-3 cue.** R2P-0 adds `PlaceDef.ambKey` and `AppSignals.PlaceChanged(string placeId, string ambKey)`; R2P-1 raises it when a main stage of a new place starts; AUD's ambience director swaps the base bed (`amb_market` for 야시장 골목) and adds `dustking_reform` | AUDIO-SPEC §2.8, §3.12 (beds keyed by modifier only) | R2P-0, R2P-1, AUD | RM-4, RM-6 |
| 42 | **(CR4-22) Small art rules:** each place field also ships a 1080×810 crop (rows 619–1428 of its tall field, PIL, 0 credits) for the formation mini-field and the `field.png` fallback; mode intro cards reuse mode art (0 credits); the six new-form skills use existing `vfxId`s and `Icons/skill_*` unless the optional icons are funded | UX-SPEC-IA §1.4 (the mini-field assumes 4:3); SPEC5 §3.5 | R2P-6, R2P-5 | RM-5, RM-6 |
| 43 | **(CR4-17) `UI/Expedition/BlessingPickPopup.cs` goes to R2P-4b in RM-7** (the charm pick reuses it); R2P-4c keeps the rest of `UI/Expedition/**`. (RM-3) `BlessingPickPopup.cs` was not built in 4.1, so R2P-4b **creates** it for R2-10 rather than reusing it. | SPEC5 §6.3, §8.2 | R2P-4b, R2P-4c | RM-7 |
| 44 | **(CR4-24) PlayMinutes on old saves:** the migration that introduces `PlayMinutes` (v4 if Q2, else v5) seeds it with min(270, 30 × install days) for saves past FTUE, so a long-running save gets the new cards on the 180 s spacing instead of after 4.5 h of fresh play | SPEC5 §2.8 | P4-0 or R2P-0 | RM-1 / RM-4 |

---

## 4. Waves in detail

### 4.0 G0: entry gate (all of it is in-flight work, outside this roadmap)
- UX wave 2 (WP-4…WP-8, with the IA §8 deltas) has merged, and the scene rebuilds. WP-IA acceptance (UX-SPEC-IA §7.5) is green.
- FX-JUICE §7 acceptance is green and merged. After that, P4-3b may take `UI/Battle/BattleFx*`, `AttackFxBuilder`, `EnemyFxBuilder` and `BattleSceneBuilder`, and P4-10 may take `ArtImportPostprocessor.cs` and `Art/{FX,VFX}/**`.
- The audio engine prints `COMPILE OK`, and `Audio*Tests` plus `FeedbackGateTests` pass on the copy. With no library, boot stays silent with no errors.
  - Fallback if it is not ready: RM-2 wires only the `UISfx.Play` sites, and the `AudioService.*` calls follow when AUD reports the API is stable.
- `Hud/BossChallengeButton` exists in Main.unity.
- Snapshot S0, then run `verify.ps1 -Stage all` green.

### 4.1 RM-1: Foundation (effort 8, critical path 2)
**Value:** the runway gets fixed before anything is built on it, the verified UI frame becomes the baseline, and the first SPEC4 art is made with the credits we already have.

| Package | Owns (files) | Does | Effort | Done when |
|---|---|---|---|---|
| P4-0 Contracts | SPEC4 §9.2 P4-0 list and critic additions; plus `FeatureGateSystem.cs` (if Q2); the SX-1/SX-2 exception members (if Q4); `Editor/IdleAutoSetup.cs` (one line, at G1) | SPEC4 §2 + §13 contracts in the §9.4 order; `PowerConfig` fields (delta 2); `GameSettings.VoiceBarks` (delta 13); pacing core (delta 15); SX-1/SX-2 (delta 19); no audio stubs (delta 4) | 2.0 | SPEC4 P4-0 done-when (full EditMode green; v3 save loads, `codex.backfillPending`, re-saves v4; stubs inert); if Q2: `FeaturePacingTests` (moved from R2P-0) green |
| P4-C Curve | `Scripts/Data/BattleConfig.cs` (3 fields + `TaperedScale`), `Data/BattleConfig.asset` (re-serialized by the build), `Scripts/Data/GameConfig.cs` + `Data/GameConfig.asset` (YAML edit), `Editor/Content/MetaContent.IdleChest.cs` (the `stageGrowth` line + doc comment only), `Tests/EditMode/IdleChestTests.cs` (its 1.02 assertions), new `Tests/EditMode/CurvePackageTests.cs`, `phase4b/sim/**` | delta 1; one sanity projection (6 seeds, R2 off) | 1.0 | `CurvePackageTests`: continuity at 150/151 (×1.02 / ×1.015 exactly), equality with the harness curve for 1–300, `taper2 = 0` reproduces today. The sim run shows a stall before ch16, ch10 by day 2 and week-4 별조각 within ±25 % of 2.4k |
| WP-9 UX verify | UX-SPEC §4.1 WP-9 list (`Verify/Runtime/AutoShot.cs`, `Verify/Editor/**`, `Tests/PlayMode/MainSceneSmokeTests.cs`, new `Tests/EditMode/UiSceneLintTests.cs`, new `Tests/PlayMode/UiBehaviourTests.cs`, `scratchpad/verify.ps1`) + the IA §8.7 deltas | UX-SPEC §5 captures, lint, smoke-test updates, behaviour tests | 2.5 | UX §5.2 captures and IA captures pass; lint has 0 failures. This may close at **G1w**, inside the first two days of RM-2; the RM-2 UI packages need the lint only at their handoff |
| P4-10 Art lane | `Art/**` (new files, plus ART-UPGRADE replacements with backups), `Editor/UiArt.cs` (new accessors only), `Editor/ArtImportPostprocessor.cs`, `scratchpad/phase4/art/**` | **L0** (A4, A5, A6 at max); fallback-safe accessors for every name in SPEC4 §9.3 and ART-UPGRADE §4.3 #7 (ButtonSoft9s, TagGold9s, TabOn9s/TabOff9s, SwitchOn9s/SwitchOff9s, AttendanceCell9s/Gold9s, Nameplate9s, BagHeader, plus `ShopCrack(int)`, `StoryPanel(ch, i)`, `MapBg`, `MapNode(i)`, `GachaBundle(state)`, `Medal(i)`, `AffinityFrame9s`, `ComebackHeader`, `SeasonBanner`); importer `BorderOverrides` for the attendance cells (L44 B44 R44 T88) and the optional mobile size overrides (§4.3 #19), with one `GetVersion()` bump; A13 notification icons by PIL (0 credits) | 1.0 | L0 passes QA; every accessor compiles and returns its fallback when the file is missing |
| AUD | AUDIO-SPEC §0.3 list | deliver the "ears needed" list to the user (Q12); finish the AUDIO-SPEC wave C remainder; complete `THIRD_PARTY_AUDIO.md` | 1.0 | AUDIO-SPEC §8.4 items 1, 2, 5 and 6 green on the copy |

- **Merge order:** P4-10's `UiArt` accessors → P4-C → P4-0 (delta 37: P4-0's SX-2 compiles against `UiArt.ButtonSoft9s`). The rest of P4-10's work and AUD can merge at any time (their files are disjoint). WP-9 merges at G1w.
- **Assets:** L0 = 45 (§7.3). It fits the current balance.
- **Gate G1** (§6.2). After G1: `BattleConfig.cs` goes to P4-3b, `GameConfig.*` and `CurvePackageTests.cs` to P4-9, `MetaContent.IdleChest.cs` to P4-8 and `IdleChestTests.cs` to P4-2.
- **User checkpoint 1:** a build with the curve. Does progress feel paced, and do the cards arrive spaced? (CR4-25) Tell the user that, by design, this build has a hard wall at chapters 13–15 (stage 129–145, from about day 18–38 of play) until 수선 비법 and 한 땀 돌파 arrive in RM-5; a short checkpoint session never reaches it.

### 4.2 RM-2: SPEC4 build, wave 4.1 (effort 38.5, critical path 7)
**Value:** all 48 SPEC4 items, with sound on every screen. SPEC5 needs their contracts, and most of them are seen in the first hour or the first week.

| Package | Owns | Order (SPEC4 §9.4, with the deltas) | Effort | Done when |
|---|---|---|---|---|
| P4-1 Collection & progression | SPEC4 §9.2 + critic table | PRG-05 → F-03 guards → Codex → Achievements → Affinity → GachaMilestones → **Power (tapered + dungeon + S5-24 cap and three-segment rec, delta 2)** → ChapterChests (chapter chest core; lost chest stretch) → Story → StrengthenAdvisor + Cheer → `CostumeUnlockType.Season` | 5.75 | SPEC4 §11.1 tests; EconomyBudget in band, C-01 rows and the ch6+ chapter-chest row (delta 29) included; `PowerRatingTests` (moved from R2P-C) green |
| P4-2 Return, events, idle | SPEC4 §9.2 + critic table | IDLE-04 → dungeon batch → IdleChest bonus API **+ C-01 cap + 16 h step first** → Comeback → EventCalendar → StarterWeek → DailyGift → backup-reminder rule (delta 17) → Coupons → Season → Blessings → FireflyRules → ticket bank | 5.0 | deterministic schedules in UTC and +09:00 FakeClocks |
| P4-3b Battle | SPEC4 §9.2 + critic table (after the FX-JUICE handoff) | lamp policy → PresentationScale, end pause, protection and `FillLamp` → T3 numbers (verify only, delta 7) → FX-06 gate → sleep → CR-01 → CR-03 (popup + receiver; the rail cell is P4-6's, delta 10) → pet → boss hint + enrage tick → CheerChip → danger arrows + ATK polish → ATK-08 wiring with L0 art (crack, yarn, stickers) → nameplate (§4.3 #17) → FxGallery → DebugStress → lost-chest hook → StrayFireflyView. Battle audio call sites: pet squeak; HUD `SpeedToggle`/`AutoToggle`/`BossChallenge` | 4.5 | §11 battle tests; a 20-minute dev-player battle with 0 exceptions |
| P4-4 Menu UI | SPEC4 §9.2 + `FrameSceneBuilder.cs`, `UI/Main/MenuPopup.cs` (delta 11) | settings rows (incl. '캐릭터 목소리', sliders, delta 13) → save import/backup/coupon popups → expedition ladder + resend-all → dungeon gate/rec/batch/blessings → lineage-card gate → formation slot pulse → **≡ menu: the `EventTile` route to the hub and SPEC4's row-3 tiles (EventHub, SeasonPass, Codex, Achievements, Coupon) calling P4-6's receivers; rows = ⌈tiles/3⌉ (delta 36)** → dev 'play log' → art wiring #8–#11 (delta 18) → audio call sites (expedition, dungeon, mail, shop, formation, settings) | 4.75 | captures 28/28e/28f/16b/16c/17b/17c; the ≡ menu capture with the row-3 tiles inside the window |
| P4-5 Character & gacha UI | SPEC4 §9.2 | codex entry + grid power → affinity section → evolution/costume gates + CR-11 copy → milestone row + popups + friend ticket → gacha reveal with the L0 bundle art → art wiring #8/#9 → audio call sites (gacha, character, evolution, costume; `OnGachaReveal`, `PushMusic(Gacha)`) | 3.5 | `GachaRevealTests`; captures 20b/20c/41 |
| P4-6 Meta UI | SPEC4 §9.2 + critic table | journal records → codex popup → presenter cases (+ `CardReady` if Q2) → notification ask → comeback card → story page + chapter map → chapter chest → event hub + start card → season popup → sleep signal → intro replay → 첫 주 tab → StrengthenPopup advisor + cheer → FTUE switch to the battle API → 16 h copy → **StatsButton rail cell; the rail `EventButton` route to the hub + its D-n `SubPill`** (delta 10; the ≡ tiles are P4-4's, delta 36) → art wiring #4/#5/#6/#9 → audio call sites (meta, tutorial, story; `SetSleep`) | 5.75 | captures 04c/22b/22c/23b/23c/30–36 |
| P4-7 Platform & data | SPEC4 §9.2 + `Core/Haptics.cs` | SaveCode + storage API → reload → share sheet → PlayLog → notification core → platform asmdef + package (delta 21) → icons → FX-05 iOS backend | 2.5 | planner, import and backup tests; the dev-APK check is manual (user checkpoint 2) |
| P4-8 Content | SPEC4 §9.2 + critic table (+ `MetaContent.IdleChest.cs` from P4-C) | features and cards (+ S5-4, + pacing floors and spacing if Q2) → achievements → codex → milestones → affinity → story → events → comeback → chests (**ch6+ default row per delta 29**) → season → blessings → coupons → backup-reminder copy → **three-segment PowerCalibration fit** → art wiring #3 | 3.75 | `MetaDataBuilder.Validate` reports 0 errors; `ContentMetaTests` |
| P4-10 Art lane | as in RM-1 | L1, L2 and L4 (then L7) in ladder order once the top-up lands; QA; PIL slicing of A5 | 2.5 | every SPEC4 §10.1 file exists or is marked "top-up (deferred)" with its fallback seen in a capture |
| AUD | as in RM-1 | subscribe to `AppSignals` (delta 14); `AudioService.VoiceBarks` plumbing; verify the SPEC4 cue aliases (`chest_open`, `unlock_card`, `achievement_claim`, `boss_defeat`, `firefly_*`, `cheer_start` …) | 0.5 | `Audio*Tests` green; no reference to a type that does not compile |
| P4-0 (standby) | its RM-1 files | contract fixes on request only; one owner, so no one else edits them | – | – |

- **Merge order:** P4-8 → P4-1/P4-2 → P4-3b → P4-7 → P4-4/P4-5/P4-6. P4-10 and AUD merge at any time.
- Every UI package runs `UiSceneLintTests` on `verify_proj` before its handoff (SPEC4 critic C-20).
- **Gate G2 (build gate):** everything merged, `COMPILE OK`, `BatchBuildAll` OK, and the EditMode suite run. Failures become RM-3's fix list.

### 4.3 RM-3: SPEC4 verify, wave 4.2, and SPEC5 readiness (effort 7.75, critical path 3)
**Value:** the SPEC4 build is proven, and the economy is measured on real code rather than on the model.

| Package | Owns | Does | Effort |
|---|---|---|---|
| P4-9 Tools, perf & verify | SPEC4 §9.2 P4-9 list (+ `GameConfig.*`, `CurvePackageTests.cs`, `phase4b/sim/**`, `Editor/IdleAutoSetup.cs` line) | BuildAll order (delta 9) → AtlasBuilder (FX §3.7 folders) → FxGallery check → PerfProbe (delta 8) → `EconomyFlowTests` smoke → **projection run #1** (delta 3): engaged player, 6 seeds, R2 off, **with the per-source 별조각 columns and the measured week-4/week-8 baseline recorded for G5 (delta 30)**; replaces SPEC5's run #1 → AutoShot §11.4 (incl. critic shots) → PlayMode via the dev player → report `scratchpad/phase4/verify_4.2.md` | 2.75 |
| Fix lane | RM-2 owners, each on its own files | only bugs from P4-9's list; no features; each fix compile-checked; P4-9 re-runs | ≈2.0 |
| SPEC5 critic | read-only | completeness and code-fact check of SPEC5 against the merged SPEC4. Writes SPEC5 v1.1 (delta 22) and checks the §0.3 start conditions | 1.0 |
| R2P-0 staging (if Q5) | scratch only: `scratchpad/phase5/r2p0/**` | writes R2P-0 as anchor-checked patch scripts plus new files against the post-RM-2 code; nothing touches the project before G3 | 2.0 |

- **Gate G3 = SPEC4 accepted** (§6.2). It also records the SPEC5 §0.3 start conditions:
  1. 4.1 merged and 4.2 green;
  2. the curve package merged;
  3. a status for every stretch item;
  4. WP-IA and UX wave 2 accepted;
  5. the shell-exception answer.
- **User checkpoint 2:** play the SPEC4 build. Run the device checklist (SPEC4 §11.5: Android notifications, share sheet, 2× stress ≥ 55 fps, ear check, grayscale pass). Pick audio takes in 사운드 시험장.

### 4.4 RM-4: SPEC5 R2-0 (effort 1.75–3.25, critical path 1–2.5)
**Value:** none on screen. This is the contract wave that keeps waves A and B file-disjoint.

| Package | Owns | Does | Effort |
|---|---|---|---|
| R2P-0 | SPEC5 §8.2 R2P-0 list (incl. `MainTabController` hub extension if Q4, `FeatureGateSystem`, `UIFrame` doc, `BattleTypes` declarations) | applies the staged patches (or writes them now if Q5 was declined): v5 save + `MigrateToV5`, enums with SPEC5's explicit values (incl. `AchievementCategory` 17–24, CR4-19), all wave A **and** B defs (incl. `PlaceDef.ambKey`, `MetaDatabase.income`, `IdleChestConfig.offlineGoldMinutesPerHour`), `IDayRolloverModule`, `ISideBattleModule`, hooks (members only; incl. `MetaRewardService.IncomePerSecond`, `BattleSystem.ClearGoldAt`, deltas 33 and 41), `DailyClock` events, `ProgressTracker.GetPeriodValue`, `PopupQueue` order table, `RedDotPaths`, `UINav` hub events, `CoachTargets`, `AppSignals.SideBattleChanged` + `PlaceChanged`. If the pacing core came in RM-1, R2P-0 adds no gate logic (R2P-5 adds the R2 pacing rows in RM-5) | 0.75 (staged) / 2.25 |
| R2P-C | – | confirm only: `CurvePackageTests` and `PowerRatingTests` green and projection run #1 on file. No edits | 0.25 |
| R2P-6 | SPEC5 §8.2 R2P-6 list | `Icons/rail_outings`, `Icons/rail_home` (L3 part, 12 credits); the importer rules for `Art/Guests/*` and `Art/Keepsakes/*`; audio briefs to AUD | 0.25 |
| AUD | as before | `MusicState.Guest/Tower/Walk` appends (delta 27) | 0.25 |

- **Gate G4:**
  - a v4 save migrates to v5, plays 60 s and re-saves;
  - `R2SaveMigrationTests`, `FeaturePacingTests` and `PopupOrderTests` are green;
  - the game is visibly unchanged, except card spacing if Q2 was declined.

### 4.5 RM-5: SPEC5 wave A, the P0 systems (effort 32, critical path 9.75)
**Value:** the core verbs, and one daily moment of real play: the guest at the door, errands built on the same combat, an uncapped offense axis (techniques), the end of the Lv100 wall, a costume for every lineage, and one-tap chores.

| Package | Owns (SPEC5 §8.2, with the deltas) | Wave-A order (SPEC5 §8.4) | Effort |
|---|---|---|---|
| R2P-1 Battle | `Systems/Battle/{BattleSystem, BattleSimulator*, BattleTypes}.cs`, new `SideBattleRunner.cs`, `Data/BattleConfig.cs`, `Editor/Builders/{BattleSceneBuilder, EnemyFxBuilder}.cs`, `UI/Battle/**` | `_sideSim` + lifecycle → shared setup → scripts, objectives, phases → Fleeing → Spawner → runner + fast-forward → `BattleRestarted` rebind → `SideHudView` → `GuestDoorView` → counters → `ClearGoldAt(int)` (delta 33) → tests | 5.25 |
| R2P-2 Meta | new `{ErrandSystem, WeeklyGuestSystem, DailyRoundsSystem}.cs`; existing `{IdleChest, Mission, DailyGift, EventCalendar, FireflyRules, Affinity, NotificationPlanner}` | errands → guest → free-quick bank → kept gift → `AutoClaimPeriod` + rollover → rounds → planner fold → the night basket's 단추 line in 단추 minutes (delta 33, if Q15) → tests | 6.5 |
| R2P-3 Growth | new `TechniqueSystem.cs`; `Systems/Characters/{CharacterSystem, CostumeSystem, EvolutionSystem}.cs`, `FormationSystem.cs`, `{PowerSystem, StrengthenAdvisor, MetaRewardService}.cs` | `GetMaxLevel` sweep → breaks + embroidery → techniques → modifiers → `PreviewTechnique` → `BuildBiased` → **자동 편성 v2 (delta 34)** → advisor rows (the `AutoFormation` row compares offence) → `GoldMinutes` through `IncomePerSecond` (delta 33) → `CostumeUnlockType.Reward` | 3.75 |
| R2P-4a UI | SPEC5 list **+ `Editor/Builders/MenuSceneBuilder.cs` (delta 23)** | `OutingsSceneBuilder` → hub registration + rails + menu tiles (rows = ⌈tiles/3⌉, after SPEC4's tiles, delta 36) → navigator/presenter → errand pages → guest page + reveal → side bar + result + explainer (**`GoToMain()` before every live start; the back handler, delta 39**) → rounds → 강화 segment + `TechniquePage` (in `MenuSceneBuilder.BuildGrowthPanel`) → break/embroidery → fitting room → gift/return/widget lines → guide bar/sleep rules | 6.25 |
| R2P-5 Content | `Editor/Content/**`, `Data/ShopDatabase.cs`, `Editor/{MetaDataBuilder, PowerCalibration}.cs`, new `ContentR2Tests.cs` | pacing rows (+ the S5-4 card if not already done in RM-2) → errands → guest + bands → techniques → break costs → six costumes (+ event-shop rows) → missions and records (R2 rows tagged, delta 32) → copy → **card banner rows (delta 24)** → the income knobs from the harness (delta 33) → PowerCalibration re-run | 3.5 |
| R2P-6 Art & audio briefs | SPEC5 list | wave-A art (L3) → QA → `UiArt` accessors; R2 cue briefs to AUD; any unfinished SPEC4 rung inherited from P4-10 (delta 38) | 2 |
| AUD | as before | the 15 wave-A keys (§7.4), the promoted `bgm_worldboss`, side-battle music states, `BattleAudioDirector` rebind (delta 27) | 1 |
| R2P-7 Verify A | `EconomyBudgetTests.cs` (R2 block + the re-based SPEC4 lifetime rows, delta 32), `Tests/PlayMode/**`, `Verify/**`, `phase4b/sim/**`, `scratchpad/verify.ps1` | economy block → AutoShot 50–63 (`MenuTileOrder` updated) → PerfProbe scenarios → harness update (the `R2Systems` model is replaced by the real `TechniqueSystem`; the agent plays errands, the guest and rounds; per-source 별조각; the `auto` variant) → **projection run #2** → report | 2.75 |

- **Merge order:** R2P-5 → R2P-3 → R2P-2 → R2P-1 → R2P-4a. Art and audio can merge at any time.
- **Gate G5** is SPEC5's wave-A done-when list (§8.4):
  - migration;
  - headless == live == fast-forward;
  - banks and rollover;
  - rounds within the time slice;
  - the economy block (≤ 250 별조각 as the weeks 5–8 mean and ≤ 350 in week 4 (delta 31), ≤ 30 선택 기억편, ≤ 80 lineage 기억편 per week; the basket line unchanged);
  - projection #2 with the re-based bands (delta 30) and the `auto` variant (delta 34);
  - the main formation byte-identical;
  - captures 50–63 (53 asserts MAIN: sheet closed, pan 0, delta 39).
- **User checkpoint 3** (SPEC5 §10.6 #1–#2): does the guest fight read as peeling a shadow off the guest? Do R2 cards come no faster than one per 3 minutes?

### 4.6 RM-6: SPEC5 B1 plus content first (effort 29, critical path 9)
**Value:** the main screen changes for the first time since launch. The engaged player reaches chapter 6, and so place 2, in their first session (chapters 1–5 take 14 minutes). The six forms are collection content: a new chase in six trees. (CR4-07) They are **not** the ceiling raise the sim calls for: each is ×1.35 over its tier-1 parent in one of a lineage's two slots, so a full team gains at most ≈+19% attack, worth ≈5–15 stages at the ×1.02 late slope, and a launched player has them from day 0 anyway. The ≈ch55 ceiling is handled by Q16. B1 adds the tower, festivals and the small loops.

| Package | B1 work (SPEC5 §8.4) | Content-first additions (RD-7, if funded) | Effort |
|---|---|---|---|
| R2P-1 | rule effects + `ExtraModifier` → tower floor scripts → Reforming (errand 3) → `QueueBonusEnemies` → title plate → firefly variants | place spawns (`BuildSpawnPools` extra enemies and boss pool) → bell boss (chime haste, 소리 halving) → `BattleArt` place field (1080×2048 guard; the 1080×810 crop for the mini-field, delta 42) → `PlaceRules.For` → `AppSignals.RaisePlace` on the first stage of a place and the place sign in the modifier chip (deltas 40–41) | 4 + 1.75 |
| R2P-2 | Tower, Title, Profile, Lantern, RouteMark, Market, MaterialMerge systems; festival and moon on `EventCalendarSystem`; errand 3 | – (optional pre-work, Q5: the B2 system files `NightWalkSystem`, `WorkbenchSystem`, `KeepsakeSystem` as new files only) | 6 |
| R2P-4b / R2P-4c | tower page; profile, title picker, lantern chip and notes, market section, [엮기] tab, route marks, festival strip (+ `MenuSceneBuilder`, delta 23) | new forms in the evolution tree (`FormInfoPopup` lines) | 5 + 0.5 |
| R2P-5 | tower, titles, profile, lantern, route marks, market, merge, festivals, moon, errand 3; **candy limit (Q7)**; Tower card banner row | `PlaceDef` place 2, the alley enemies, `boss_bell` + `BossHintDef`, the 야시장 rule; six forms (`Evolve(...)` tier-2 rows, skills, codex) | 3 + 2 |
| R2P-6 | B1 art (L5 part) | six forms, `field_market_tall` (2k edit; ring constants within 2 px) + its 1080×810 PIL crop `field_market` (0 credits), `boss_bell` | 2 + 0.5 |
| AUD | B1 keys (§7.4) + `dustking_reform` (CR4-21) | `bell_boss_ring`; the place bed `amb_market` and the director's place layer (delta 41) | 1.25 |
| R2P-7 | B1 tests, captures 70–79, perf | captures 86–88 and the place/forms projection (report the measured ceiling move; ≈5–15 stages expected, CR4-07) | 2 |

- **Merge order:** as in wave A, plus a merge/fix day.
- **Gate G6:** the B1 tests (`TowerTests`, `RuleTableTests`, `TitleTests`, `ProfileTests`, `LanternTests`, `RouteMarkTests`, `MarketTests`, `MergeTests`, `FestivalRoundsTests`, `MoonPhaseTests`, `ErrandDustKingTests`). With RD-7, also `NewFormsContentTests` and `PlaceTests`. Plus the captures and the projection, **which must still meet every SPEC5 §4.4 band (delta 35)**.

### 4.7 RM-7: SPEC5 B2 (effort 27.5, critical path 11)
**Value:** the deep long-term systems: a roguelite run, the shop's back rooms with research and keepsakes, and skill riders.

| Package | Work | Effort |
|---|---|---|
| R2P-1 | crest specials → walk fight scripts → slot modifiers → `DoubleHitChancePercent` → rider triggers. If RD-7 is off, also the place-2 work from RM-6 | 3.5 (5) |
| R2P-2 | NightWalk, Workbench, Keepsake systems (integration only, if the Q5 pre-work ran) | 8 (≈4) |
| R2P-3 | B2 growth hooks (riders, the workbench `EchoMoveSpeedPercent`) | 1 |
| R2P-4c | `HomeSceneBuilder`, `HomePanelView`, workbench and keepsake pages; walk pages (R2P-4b: `WalkPageView`, `WalkTalentPopup`, `MerchantPopup`, and `UI/Expedition/BlessingPickPopup.cs` for the charm pick, delta 43); rider lines | 6.5 |
| R2P-5 | walk, workbench, keepsakes, riders; card banner rows; the first season costume row | 3 (5) |
| R2P-6 | B2 art (L6); optional L8 B2 items if funded | 1.5 (2) |
| AUD | B2 keys + the new score `bgm_lanternroad` (§7.4) | 1 |
| R2P-7 | B2 tests (`NightWalkTests`, `WorkbenchTests`, `KeepsakeTests`, `RiderTests`), captures 80–89, perf `walk_crests`, **projection run #3 with 12 seeds** | 2 |
| SPEC6 authors (parallel, read-only on the project) | write `SPEC6.md` (§4.8–§4.10 scope) against the real B1 code; critic pass; art lists for L9/L10 | 3 |

- **Gate G7** (§6.2). After it, the SPEC5 manual checks #3–#5 run (user checkpoint 4). Q14 goes to the user with the L9/L10 totals. (CR4-07) Projection #3 reports the measured ceiling (stage and day, engaged and `auto` players) with all of B2 in; that day is the input for Q16's post-launch cadence.

### 4.8 RM-8: SPEC6-A, content cadence (≈23.5 agent-days, critical path ≈7; needs SPEC6)
**Value:** IDLE_RESEARCH2's critic: "content is the prestige substitute". More places, stories and friends are the long game, and they reuse SPEC5's engines.

- **Places 3–6** (R2-37 cadence, a new place every 5 chapters: from chapters 11, 16, 21 and 26). (CR4-07) On the measured timeline the engaged player reaches those chapters on days ≈1.3, 8.6, 11.1 and 13.9, so every place of the plan is seen by day 14 and nothing new appears in the five or more weeks before the ≈ch55 ceiling. SPEC6 sets `fromChapter` against the projection so that a new place arrives about every 10 engaged days up to the ceiling (for example chapters 11, 21, 31, 41), and later places go to the post-launch cadence (Q16). Candidates come from IDLE_RESEARCH2: 기차역, 빗물 다리 (one puddle lane slows enemies 15 %), 다락방 거리 / 지붕 위 (the Wind modifier on 50 % of rotation slots), 바닷가 등대 (pairs with 등대 몽당이). Each place has:
  - a field, made as a 2k image edit of `field_tall` that keeps the ring constants, plus its 1080×810 PIL crop for the formation mini-field (delta 42);
  - its own ambience bed through `PlaceDef.ambKey` (delta 41): `amb_station`, `amb_rainbridge`, `amb_rooftop`, `amb_lighthouse` (SYN/HYB, 0 credits);
  - two enemy families from existing gimmick flags plus R2's three;
  - one boss with a `BossHintDef` and one mechanic from the pool: a chime haste (from place 2), a three-lane split at 66 % and 33 % HP, or copying the strongest guardian's attack for 10 s;
  - one place rule from the R2-00 table;
  - a three-panel storybook page;
  - codex rows.

  The skins stay IP-safe: shadows cast by forgotten things, never one of our friends, never the weekly guest.
- **Storybook pages** (R2-27): one page per place, place 2 included. Panel 1 opens at the place's first chapter-boss clear. Panels 2–3 need 이야기 조각 from that place's expeditions or a guest letter. Each page pays 별조각 50, inside a SPEC6 budget row (CR4-04: IDLE_RESEARCH2 §9.1 has no R2-27 row yet; one-time ≤ 50 per page). (CR4-23) Each page, chapters 1–5 included, also mails its one-line letter from that chapter's object ([편지] filter, no reward; v1.2 #113).
- **Storybook puzzle stages** (R2-38): 20 hand-made one-time stages on the R2-00 host. Each has a fixed roster (borrowed forms allowed), a fixed modifier and a goal, and teaches synergies and directions. They need no new art.
- **Two new lineages** (R2-36b step 2, Q11): '한 짝 장화' [비·밤], a fully non-human mascot, and '풍경' [비·소리]. Each has 3 forms, SkillKit skills, 2 traits, a Rare or Epic gacha entry, codex and affinity lines, and a costume. They fill the thin trait graph: today Rain has only 우두커니.
  - (CR4-22) Art beyond the forms: FX-JUICE per-lineage impact and crit sprites (`atk_{lineage}_impact/_crit`) with their `AttackFxCatalog` rows, and 3 skill icons each (fallback: the closest `Icons/skill_*`); ≈+60 credits in L9.
  - (CR4-04) Budget rows: SPEC4's affinity row is "≤ 4,000 for 7 lineages" (3,780 = 7 × 540); nine lineages make 4,860, so SPEC6 re-bases it per lineage (≤ 571) and adds the new codex rows (≈+100 per lineage) to the R2 codex row.
- **Season costumes:** one per Q-06 season (Q8).
- **Packages:**
  - S6P-0: contracts (story-fragment save, puzzle defs, place-boss mechanic types);
  - S6P-1: boss mechanics in the sim;
  - S6P-2: the puzzle system and story fragments;
  - S6P-4: the story-fragment UI, puzzles in the 밤 나들이 hub, place names on the chapter map;
  - S6P-5: content;
  - S6P-6: art (L9 part, ≈355 credits with CR4-22);
  - AUD: lineage signatures, boss cues and the four place beds;
  - S6P-7: verify, projection, budget rows.
- **Entry points:** the rails are full after R2 (merge log note 3), so every new entry lives in a hub page, the ≡ menu or a popup.

### 4.9 RM-9: SPEC6-B, wave C systems (≈33 agent-days, critical path ≈10)
- **R2-21** 꿈길 and the full 등불 길 (L): a weekly seeded walk, 3 attempts, thresholds pay 별빛 사탕 and ≤ 60 별조각/week, and a share code. (CR4-23) The full walk also brings the live branching map (3 alleys × 10 steps), the complete 등불 나무 and the 8-week rotating charm theme.
- **R2-22** boards and the 수선점 명함 share card. No league (orchestrator decision; D-18), and no NPC names on thresholds or ladders. (CR4-23) Also in scope, from v1.2: **끝없는 밤** (`BattleMode.Endless` 6, reserved; 1 등불 per run, a 1-of-3 pick every 5 waves, thread medals at waves 10–50, the '지난주의 나' marker, 0 별조각), **쪽지 코드** friend rows with an AUTO replay (real friends only, ≤ 20 per mode, pay nothing), and the one-line share text.
- **R2-23** the affinity extension: letters as mail, object gestures for the three daily cares, 3 gifts a day in total, anniversaries from `obtainedUnix`. (CR4-23) Also: loved-material gifts as the first gift source (no new item type), the 찾은 날 lore days, the '!' requests (≤ 4 a day) and the Bronze/Silver/Gold badges.
- **R2-26** hidden records: 15 more Q-04 flag lines with clue text (+150 별조각; SPEC5's "Q-04 R2 lines" row is ≤ 1,000 so they fit, delta 32).
- **R2-16** box hunt inside the chapter chest.
- **R2-38b** 오늘 밤의 의뢰: one generated request per DayKey, seed = hash(DayKey). (CR4-23) Plus **의뢰서 만들기**, the 8-character custom request code for friends (pays nothing).
- **R2-39** 한발 먼저: call the next wave early; AUTO never calls.
- **R2-40** 축음기 음반: 10 collectible BGM records, 2 of them new pieces.
- Optional: the R2-37 place interactables.
- **Guards** (IDLE_RESEARCH2 §9.1): P2 ≤ 80 별조각/week (≈67 planned); R2-39, R2-38b, R2-22 and R2-40 grant no 별조각 or shards; the rollover grants at most what the period earned.

### 4.10 RM-10: SPEC6-C, wave D (≈22 agent-days; needs the second top-up)
- **R2-29** 안채 꾸미기 + 밤손님. Decor costs 단추 minutes and commons; no 실밥 by default. Basket bonuses apply to the 단추 and material lines only. Visitors come only for time away. There is no decay ritual. (CR4-05) The 단추-minute prices are only a sink if Q15 landed; otherwise decor is free from week 2. (CR4-23) Also in scope, from v1.2: favourites (each visitor loves one decor piece; the codex shows it), mended visitors (they need per-enemy kill counters, a new S6P-0 contract item), 이웃 수선점 구경 (`LLV1.` invitation codes, 3 built-in neighbour rooms, stamps capped at 3 a day) and the optional MAIN-only shop tap.
- **R2-32** 원정 엽서 + 도시락: 24 postcards with sticker portraits.
- **R2-33** 계절 달력 꾸러미: a lunar date table for 2026–2036 (`EventScheduleType.DateList`, reserved value 6), launching with 3 packs, plus the accessory layer on 29 head anchors.
- **Art** ≈640 credits (L10). Tell the user before generating it (memory rule).

---

## 5. Ownership across waves (the hot files; full lists in SPEC4 §9.2, SPEC5 §8.2 and UX-SPEC §4.1)

"Standby" means the package still owns the file but only makes contract fixes on request. "Frozen" means nobody edits it; needs are reported.

| File or group | G0 (in flight) | RM-1 | RM-2 | RM-3 | RM-4 | RM-5 | RM-6 / RM-7 | RM-8+ |
|---|---|---|---|---|---|---|---|---|
| Contracts: `Core/{GameEnums, SaveData.*, PlayerData, Game}.cs`, new `Core/AppSignals.cs`, `Data/MetaDatabase.cs`, `Systems/Meta/{MetaSession, PopupQueue, MetaTypes, ProgressTracker}.cs`, new `IMetaModule.cs`, `UI/UINav.cs` | done (WP-2, WP-IA) | P4-0 | P4-0 standby | P4-0 (fix lane) | R2P-0 | R2P-0 standby | R2P-0 standby | S6P-0 |
| `Core/GameSettings.cs` | WP-IA (`SheetAutoReturn`) | P4-0 (+ `VoiceBarks`) | P4-0 standby | – | – | – | – | S6P-0 |
| `Systems/Meta/FeatureGateSystem.cs` | frozen | P4-0 (if Q2) | frozen | frozen | R2P-0 | frozen | frozen | S6P-0 |
| `Systems/Meta/RedDotSystem.cs` (`RedDotPaths` constants only) | WP-IA (`IsClaimOn`) | P4-0 | P4-0 standby | – | R2P-0 | – | – | S6P-0 |
| Shell: `UI/Main/{MainTabController, SheetController}.cs`, `UI/UIFrame.cs` | WP-IA | P4-0 (SX-1 only) | frozen | frozen | R2P-0 (hub extension, Q4) | frozen | frozen | frozen |
| Kit: `UI/UITheme.cs`, `Editor/SceneParts.cs`, new `UI/Common/TabSkin.cs` | frozen | P4-0 (SX-2 only) | frozen | frozen | frozen | frozen | frozen | frozen |
| `Data/BattleConfig.cs` + asset | – | P4-C | P4-3b | P4-3b (fix) | – | R2P-1 | R2P-1 | S6P-1 |
| `Data/GameConfig.cs` + asset | – | P4-C | – | P4-9 | R2P-C (confirm) | – | – | – |
| `Systems/Battle/{BattleSystem, BattleSimulator*, BattleTypes}.cs` | WP-2/WP-5 lines | P4-0 (hook members) | P4-3b | P4-3b (fix) | R2P-0 (declarations, hooks) | R2P-1 | R2P-1 | S6P-1 |
| `UI/Battle/**`, `Editor/Builders/{BattleSceneBuilder, AttackFxBuilder, EnemyFxBuilder}.cs` | FX-JUICE, WP-5, WP-IA B | – | P4-3b | P4-3b (fix) | – | R2P-1 | R2P-1 | S6P-1 |
| `Systems/Meta/{PowerSystem, StrengthenAdvisor, MetaRewardService}.cs` | – | – | P4-1 | P4-1 (fix) | R2P-0 (hooks) | R2P-3 | R2P-3 | S6P-3 |
| `Systems/Characters/**`, `Systems/{GachaSystem, InventorySystem}.cs`, `Synergy/SynergySystem.cs`, `Battle/FormationSystem.cs` | – | P4-0 (hooks) | P4-1 (`FormationSystem` frozen) | P4-1 (fix) | R2P-0 (hooks) | R2P-3 (Characters, Formation; Gacha frozen) | R2P-3 | S6P-3 |
| Meta systems: `Systems/Meta/{IdleChest, Mission, DailyClock, EventCalendar, DailyGift, FireflyRules, …}.cs`, `Systems/Battle/{Expedition, Dungeon}System.cs` | – | P4-0 (hooks) | P4-2 | P4-2 (fix) | R2P-0 (hooks, `DailyClock` events) | R2P-2 | R2P-2 | S6P-2 |
| `Editor/Content/**`, `Editor/{MetaDataBuilder, PowerCalibration}.cs`, `Data/ShopDatabase.cs` | WP-2, WP-8 (Ftue) | P4-C (`IdleChest` line only) | P4-8 | P4-8 (fix) | – | R2P-5 | R2P-5 | S6P-5 |
| `Editor/Builders/MetaSceneBuilder.cs`, `UI/Meta/**` (except `SaveCode`), `UI/Tutorial/**`, `TutorialSceneBuilder*` | WP-7, WP-8, WP-IA | – | P4-6 | P4-6 (fix) | – | R2P-4a (listed files; Tutorial frozen) | R2P-4c | S6P-4 |
| `Editor/Builders/{MenuSceneBuilder, SettingsSceneBuilder, FrameSceneBuilder}.cs`, `UI/Main/{GrowthPanel, TopBarView, MenuPopup}.cs`, `UI/{Shop, Expedition, Formation, Synergy, Bag}/**`, `UI/Popups/SettingsPopup.cs` | WP-4 | – | P4-4 (incl. every ≡ tile, delta 36) | P4-4 (fix) | – | R2P-4a (`MenuSceneBuilder`, `FrameSceneBuilder`, `GrowthPanel`, `MenuPopup`, `DailyGiftCardView`) | R2P-4c (`MenuSceneBuilder`, `FrameSceneBuilder`, `TopBarView`, Shop/Bag/Expedition except `BlessingPickPopup`, which is R2P-4b's in RM-7, delta 43); `SettingsSceneBuilder` frozen | S6P-4 |
| `Editor/Builders/CharacterSceneBuilder.cs`, `UI/Characters/**`, `UI/Main/GachaPanel.cs`, `UI/Popups/GachaResultPopup.cs` | WP-6 | – | P4-5 | P4-5 (fix) | – | R2P-4a (listed files) | R2P-4c | S6P-4 |
| Platform: `Packages/manifest.json`, `Scripts/Platform/**`, `Core/{SaveStorage, GameBootstrap, LocalNotifications, Haptics}.cs`, `UI/Meta/SaveCode.cs`, `Systems/Meta/NotificationPlanner.cs` | – | – | P4-7 | P4-7 (fix) | R2P-0 (planner hook) | R2P-2 (planner only) | – | S6P-2 |
| Verify: `Verify/**`, `Tests/PlayMode/**`, `scratchpad/verify.ps1` | – | WP-9 | WP-9 until G1w, then frozen | P4-9 | – | R2P-7 | R2P-7 | S6P-7 |
| `Editor/IdleProjectSetup.cs` | – | – | – | P4-9 | – | – | – | – |
| `Editor/IdleAutoSetup.cs` (`SceneVersion` line) | – | P4-0 (at G1) | – | P4-9 (at G3) | R2P-0 (at G4) | R2P-7 | R2P-7 | S6P-7 |
| Art lane: `Art/**`, `Editor/{UiArt, ArtImportPostprocessor}.cs`, scratch art folders | FX-JUICE (importer, `Art/{FX,VFX}/Juice`), ART-UPGRADE batches, WP-IA0 | P4-10 (accessors merge first, delta 37) | P4-10 | P4-10 | R2P-6 (+ P4-10's unfinished rungs, delta 38) | R2P-6 | R2P-6 | S6P-6 |
| Audio: `Core/Audio/**`, `Core/UISfx*.cs`, `Editor/Audio/**`, `Audio/**`, `Resources/Audio/**`, `Tests/EditMode/Audio*Tests.cs`, one line in `Editor/IdleDataBuilder.cs` | AUD | AUD | AUD | AUD | AUD | AUD | AUD | AUD |
| `Tests/EditMode/EconomyBudgetTests.cs` | – | – | P4-1 (+ the ch6+ chest row, delta 29) | P4-1 (fix) | – | R2P-7 (R2 block + the SPEC4 lifetime rows re-based, delta 32) | R2P-7 | S6P-7 |
| Sim harness `phase4b/sim/**` | sim (done) | P4-C | – | P4-9 | – | R2P-7 | R2P-7 | S6P-7 |

---

## 6. Dependencies and gates

### 6.1 Graph
```
in flight: UX wave 2 (WP-4..8) + WP-IA acceptance + FX-JUICE merge + audio engine
   │ G0  (snapshot S0, verify.ps1 green, BossChallengeButton present)
RM-1 ─┬─ P4-0 contracts (+ pacing core Q2, SX-1/SX-2 Q4, PowerConfig fields, VoiceBarks key)
      ├─ P4-C curve data (taper2 151, gold 1.06, basket 1.0025) + sim sanity run
      ├─ WP-9 UX verify ......................................... (G1w, inside RM-2)
      ├─ P4-10 art L0 (fits now) + all UiArt accessors (fallback-safe)
      └─ AUD ear list, wave-C leftovers
   │ G1  (v3→v4, curve tests, sim sanity; SceneVersion 2)            ◄ user checkpoint 1
RM-2 ── P4-1 · P4-2 · P4-3b · P4-4 · P4-5 · P4-6 · P4-7 · P4-8 · P4-10 · AUD   (P4-0 standby)
   │ G2  (merged, builds, suite runs → fix list)
RM-3 ── P4-9 verify + projection #1 ║ fix lane ║ SPEC5 critic ║ R2P-0 staged in scratch
   │ G3  = SPEC4 accepted + SPEC5 §0.3 start conditions; SceneVersion 4 (G1 took 2 → 3)  ◄ user checkpoint 2
RM-4 ── R2P-0 apply ║ R2P-C confirm ║ R2P-6 rail icons ║ AUD music states
   │ G4  (v4→v5, pacing, popup order)
RM-5 ── R2P-1 · R2P-2 · R2P-3 · R2P-4a · R2P-5 · R2P-6 · AUD  →  R2P-7 verify A
   │ G5  = SPEC5 wave-A done-when + projection #2                       ◄ user checkpoint 3
RM-6 ── B1 + content first (place 2, six forms) ║ optional B2 system pre-work (new files)
   │ G6
RM-7 ── B2 rest ║ SPEC6 authoring + critic ║ L9 art may start if funded
   │ G7  = projection #3 (12 seeds)                                     ◄ user checkpoint 4
RM-8 ── SPEC6-A content cadence     │ G8
RM-9 ── SPEC6-B wave C              │ G9
RM-10 ─ SPEC6-C wave D (top-up #2)  │ G10
```

Hard dependencies:
- **SPEC5 needs SPEC4's APIs:** `IMetaModule`, `EventCalendarSystem`, `CodexSystem`, `AchievementSystem`, `AffinitySystem`, `DailyGiftSystem`, `CheerSystem`, `StrengthenAdvisor`, `ChapterChestSystem`, `SweepMany`, `ResendAll`, `BattleSetup.Protection` and `PresentationScale` (SPEC5 §0.3 #1).
- **The B2 forms need wave A's cap breaks.** Their recipes need `LineageLevel 110`.
- **Place 2 needs two things:** wave A's magpie and nest art and content, and B1's rule effects (the 야시장 rule is `WaveSizeScale 1.2`).
- **R2-15 extends SPEC4 NEW-03, R2-07 extends NEW-04, and R2-10 extends PRG-07** (RD-2).
- **SPEC6 needs SPEC5's engines** (the side host, the rule table, `PlaceDef`, the walk engine), and it is written against the real code.

### 6.2 Gate checklist (in addition to the common steps of §2.5)

| Gate | Specific checks | Owner | Fails if |
|---|---|---|---|
| G0 | UX-SPEC-IA §7.5 captures; FX-JUICE §7.2/§7.3 metrics; AUDIO-SPEC §8.4 #1–#2; `Hud/BossChallengeButton` in Main.unity | orchestrator | any is missing; RM-1 waits |
| G1 | P4-0 done-when (SPEC4 §9.4); `CurvePackageTests`; the sim sanity run (R2 off: stall before ch16; ch10 ≤ day 2; week-4 별조각 within 2.4k ± 25 %); `FeaturePacingTests` (if Q2); L0 QA'd | P4-0 | the v3 save fails to load, or a stub throws |
| G1w | UX-SPEC §5 captures; `UiSceneLintTests` at 0 failures; `UiBehaviourTests` via the dev player | WP-9 | any RM-2 UI package would hand off before this |
| G2 | all RM-2 merged, `COMPILE OK`, `BatchBuildAll` OK, the suite runs (failures are listed, not fixed here) | P4-9 | the build breaks |
| G3 | SPEC4 §11.1 suite (the 659 tests of SPEC4's count plus all new ones); a v3 save loads, plays 60 s headless and re-saves v4; `EconomyBudgetTests` incl. the C-01 basket rows at stages 1/20/50/100/200; **projection #1** (R2 off: a ≥ 7-day stall before ch16; ch1–11 within day 1); `PowerRatingTests`; SPEC4 §11.3 perf (p95 ≤ 8 ms, BuildBatch ≤ 0.6 ms, GC ≤ 64 B/frame, batches −25 % with atlases); SPEC4 §11.4 captures incl. the critic shots, re-measured for the IA frame; the static checks ('개방'/'습니다', '장비', text floor, coach paths); the SPEC5 critic report; stretch statuses; **the measured R2-off 별조각 baseline per source for weeks 1, 4 and 8, and the ch6+ chapter-chest row (deltas 29–30)** | P4-9 | any SPEC4 P0/P1 check fails |
| G4 | `R2SaveMigrationTests`, `FeaturePacingTests`, `PopupOrderTests`; a v4 save → v5, plays 60 s, re-saves; `MetaDataBuilder.ValidateTargetsInScene` reports 0 missing | R2P-0 | – |
| G5 | SPEC5 §8.4 wave-A done-when (all 8 bullets); captures 50–63; perf scenarios `errand_magpie`, `errand_laundry`, `guest_10_layers`, `fastforward`, `techniques_max`; **projection #2** (§4.4 targets: ch10 ≤ day 2, ch15 days 4–10, ch25 days 10–18, no ≥ 7-day stall before day 40, stage 400–520 on day 30, **week-4 별조각 2,800–3,800 and week-8 2,600–3,600 with the per-source log (CR4-02)**, ≥ 30 failures in 90 days; **the `auto` player: ch25 ≤ day 17, no ≥ 7-day stall before day 40 (CR4-06)**; with Q15: "단추 60분치" is worth 30–90 minutes of the engaged player's own measured 단추 per online minute on days 1–40, and the night basket's 단추 is 25–50 % of a day's 단추 (CR4-05)); the R2 economy block (weeks 5–8 mean, delta 31) | R2P-7 | – |
| G6 | B1 tests (+ `NewFormsContentTests`, `PlaceTests` with RD-7); captures 70–79 (+ 86–88); the place-2 ring overlay ratio 1 ± 0.02; `tower_autoclimb` perf; weekly R2 payouts within ±25 % of SPEC5 §4.5 (its week-4 column for stage-tracking sources, delta 31); **every §4.4 band again (delta 35)**; the place bed swaps on the first 6-1 (log line) | R2P-7 | – |
| G7 | B2 tests; captures 80–89; `walk_crests` perf; **projection #3 with 12 seeds** (incl. the material projection and the R2-11 trigger; **every §4.4 band, and the measured ceiling stage and day for Q16**); the SPEC5 manual checks #3–#5 handed to the user | R2P-7 | – |
| G8–G10 | as SPEC6 defines; at minimum the same suite, migration, economy-guard, projection and capture pattern | S6P-7 | – |

---

## 7. Assets

### 7.1 Prices and assumptions
- `gpt_image_2_5` at max: **5** credits at 1k and **9** at 2k (the ledger's only 2k job and the B1/B5 jobs). Image edits are priced like generations. Outpaint costs 2 and upscale 2. Prices were not re-quoted today; run a `get_cost` preflight before the first 2k job of each wave.
- Reserves:
  - ART-UPGRADE batches carry their own reserves;
  - SPEC4 art gets +15 % (ART-UPGRADE §1.3);
  - SPEC5 and new items get +20 % (SPEC5 §9.1).

  In practice, the eight delivered ART-UPGRADE batches (B1, B2, B3a, B4, B5, B6a, B7a, B8a) spent 43 of their 67 planned reserve credits. The batches with on-model characters (B1, B2, B8a) spent all of theirs, so reserves on character art should be kept.
- **What fits now:** only L0 (45 ≤ 46.88). Every other rung needs the top-up (Q1).

### 7.2 The ladder (value order for credits; cumulative)

| Rung | Content | Credits | Cumulative | Needed by | Fallback until then |
|---|---|---|---|---|---|
| **L0 (fits now)** | SPEC4 A4 `UI/gacha_bundle_{closed,glow,open}` (3 × 5); A5 `Battle/shop_crack_{1,2,3}` (1 image edit of `Battle/shop.png` + PIL difference masks, 5); A6 `VFX/enemy_death_yarn`, `VFX/fx_rust_stain`, `VFX/fx_moth_hole` (3 × 5); up to 2 re-rolls | 35 + 10 | **45** | RM-2 (P4-5 reveal, P4-3b ATK-08) | card back; no crack; generic puff |
| L1 | SPEC4 feature art: A1 15 story panels `Story/ch{1..5}_{1..3}` (75), A2 `Story/map_bg` (2k, 9), A3 `UI/map_node_{off,on,boss,chest}` (20), A7 `UI/medal_{bronze,silver,gold,hidden}` (20), A8 `UI/affinity_frame_9s`, `affinity_badge`, `fx_heart_burst`, `affinity_halo` (20); +15 % | 144 + 22 | 211 | RM-2 | text-only story card; plain map; existing icons |
| L2 | ART-UPGRADE **B8b**: banners for the SPEC4 cards (speed, lamp_policy, auto_challenge, achievements, dungeon_batch_sweep; expedition_batch is skipped under delta 16), 4 medals, `UI/bag_header` | 60 | 271 | RM-2 (P4-8 turns the cards on) | medal-only cards |
| L3 | SPEC5 R2-0 + wave A: `Icons/rail_outings`, `Icons/rail_home` (10); `Enemies/enemy_magpie`, `enemy_nest` + `enemy_nest_broken` (15); `Guests/guest_{wardrobe,clock,mirror,bell}` (20); `UI/stitch_mark` (5); six costumes `Costumes/{ttok_pajama, chal_keyring, mong_birthday, udu_raincoat, rouge_stage, lala_lullaby}` (30); four R2 card banners `Meta/feature_{outings, level_break, techniques, daily_rounds}_banner` (20); +20 % | 100 + 20 | 391 | RM-4, RM-5 | tinted moth; tinted shadow + name plate; **base form (a costume without art is invisible)** |
| L4 | Always-visible polish: **B6b** 25 skill icons (145; needed before the 128 px blessing cards look final), **B3b** 9 state 9-slices (55), **B7b** 6 icons incl. `Icons/power` and `Meta/mission_{kill,boss}` (35), **B9** 5 synergy emblems (30) | 275 (+5 `Icons/stats` for P4-6 R-8 L4.stats; +5 `Icons/story`, conditional L4.story) | 666 | any time; wiring done in RM-2 (RD-5) | today's sprites |
| L5 | SPEC5 B1: `UI/tower_bg`, `Enemies/boss_dustking`, `UI/title_plate_9s`, `UI/festival_{a..e}`, `Icons/moon_phase`, `Icons/lantern_chip` (50); Tower card banner (5); **content first:** six forms `Characters/{ttok_yoyo, chal_heartkey, mong_lighthouse, udu_thunder, rouge_compact, lala_carousel}` (30), `Battle/field_market_tall` (2k edit, 9), `Enemies/boss_bell` (5); +20 % | 99 + 20 | 785 | RM-6 | `dungeon_banner`, scaled dust, pill, event banner; **forms: none for shipping**; `field_tall`; tinted shadow |
| L6 | SPEC5 B2: `UI/walk_strip`, `Enemies/npc_shadow_merchant`, `UI/{home_bg, workbench_bg, shelf_bg}` (25); 16 keepsakes `Keepsakes/{id}` (80); 3 card banners `Meta/feature_{night_walk, keepsakes, workbench}_banner` (15); the first season costume (5); +20 % | 125 + 25 | 935 | RM-7 | `bg_night` crop, tinted shadow, `content_bg`, cropped source art |
| L7 | Low-visibility remainder: SPEC4 A9 `UI/comeback_header` (2k, 9), A11 seven `Characters/{base}_pose10` (35), A14 `Costumes/jjak_starwalk` (5), +15 % (56); ART-UPGRADE **B10** items (15); FX-JUICE remainder: `fb_light_burst_4x4` (5) (the 6 decals were delivered in FX round 2 for 30 credits, `scratchpad/fx2/decals/report.json`); OPT `enemy_tangle` (5 + 5) | 86 | **1,021** | any time | sway in code; the tier-30 fallback reward; skipped decals |
| L8 (optional) | SPEC5 optional set +20 %: wave A (errand banners 10, 7 technique icons 35); B1 (4 firefly variants 20, a stall banner 5, 2 mode-source costumes 10); B2 (8 more guests 40, 16 more keepsakes 80, 4 dark charms 20, 1 mode-source costume 5); OPT extras (`combo_seven_lights` 5, codex banner + medal 10, concept upscales 12 if Q13 is not answered); 9 enemy hurt faces (45 + 9; FX-JUICE §0.5 "later") | 351 | 1,372 | RM-5…RM-7 | the listed fallbacks |
| L9 | SPEC6-A/B: places 3–6 (each = 2k field edit 9 + 2 enemies 10 + boss 5 + 3 story panels 15) +20 % (187; each field's 4:3 crop is PIL, 0); place-2 storybook (18); two lineages (3 forms + card + costume = 25 each, +20 %: 60; **CR4-22: + 2 impact/crit sprites and 3 skill icons each, +20 %: ≈60**); 5 season costumes (30); wave C (≈100) | ≈455 | ≈1,827 | RM-8, RM-9 | the closest existing impact set and `Icons/skill_*` |
| L10 | SPEC6-C wave D: 18 visitors, 20–40 decor pieces, 24 postcards, 3 seasonal packs, 12 accessories, October masks, neighbour rooms | ≈640 | ≈2,467 | RM-10 | – |

- **Specced core:** L0–L7 = 1,021 (SPEC4 rungs L0+L1+L2+L4+L7 = 632, or 627 without L4.story).
  - Minus the balance (46.88), that leaves **≈975 needed from the top-up**.
  - That is 5 lower than the plain sum of the source plans, because delta 16 drops one B8b banner (`feature_expedition_batch_banner`).
  - Optional (L8) is ≈351, and SPEC6 (L9–L10) ≈1,095.
  - (CR4-22) Not in any rung, 0 credits: the place fields' 4:3 crops (PIL) and the mode intro cards (reused mode art). Optional and outside the 351: 6 new-form skill icons (30), with the closest `Icons/skill_*` as the fallback.
- (RM-3) Since 2026-11-05 16:25 the Codex lane has delivered 104 files covering most of L0, L1, L2, L4 and L7 (`scratchpad/phase4/art/codex/landed.md`). The user allowed Codex art to ship (2026-11-05). The ladder now prices only an optional Higgsfield re-generation.
- **If the top-up is partial,** buy in rung order. Do not split rungs L3 and L5: costumes and forms with only part of their art look broken.
- Rung order is a credit priority, not a schedule. Art generation is fast (a 12-image batch takes minutes; QA is the slow part), so with a full top-up the art lane works through the rungs as each code wave approaches.

### 7.3 Assets per wave (credits at max; fallbacks in §7.2)

| Wave | Assets | Credits | Fits now? |
|---|---|---|---|
| RM-1 | L0: A4 ×3, A5 ×1 (+3 PIL overlays), A6 ×3; A13 notification icons by PIL (0); accessors and importer rules (0) | ≤ 45 | **yes** (46.88) |
| RM-2/RM-3 (art lane) | L1 (166), L2 (60), L4 (265), L7 (121) | 612 | top-up |
| RM-4 | rail icons ×2 (part of L3) | 12 | top-up |
| RM-5 | the rest of L3: magpie, nest ×2, 4 guests, stitch mark, 6 costumes, 4 card banners; optional (L8) errand banners + technique icons (54) | 108 (+54) | top-up |
| RM-6 | L5: B1 set + Tower banner + forms, market field, bell boss; optional (L8) firefly variants, stall banner, 2 costumes (42) | 119 (+42) | top-up |
| RM-7 | L6: B2 set + 3 banners + first season costume; optional (L8) 8 guests, 16 keepsakes, 4 charms, 1 costume (174) | 150 (+174) | top-up |
| RM-8 | L9 part: places 3–6, place-2 story, 2 lineages (with their impact sprites and skill icons, CR4-22), 5 season costumes | ≈355 | second top-up |
| RM-9 | L9 rest: wave C frames, stickers, envelopes, box + mimic, props, record sleeves (crops) | ≈100 | second top-up |
| RM-10 | L10 | ≈640 | second top-up |

The RM-2/RM-3 lane also takes the L8 OPT extras and hurt faces (81) if they are funded.

### 7.4 Audio follow-ups per wave (0 credits; CC0 + original synthesis through AUDIO-SPEC)

| Wave | Work | Owner |
|---|---|---|
| RM-1 | Deliver the ears list to the user: battle 155, music 11, stingers 14, plus the UI keys. Finish the AUDIO-SPEC wave C remainder. Complete `THIRD_PARTY_AUDIO.md`. SX-1 sheet cues (via P4-0, if needed). If kenney.nl blocks downloads, ask the user to drop the zips; otherwise the CC0 layers are skipped (every HYB key works without them) | AUD; P4-0 |
| RM-2 | The `AppSignals` subscription; `VoiceBarks` plumbing; alias check. Every AUDIO-SPEC §8.1 call site wired by its owner (delta 12) | AUD; P4-3b/4/5/6 |
| RM-3 | Audio in PerfProbe (≤ 0.15 ms main thread, 0 B GC); the device ear and latency check (user checkpoint 2) | P4-9; user |
| RM-4/RM-5 | `MusicState` appends; promoted `bgm_worldboss`; the `BattleAudioDirector` rebind (delta 27). Wave-A keys: `errand_start`, `magpie_flutter`, `magpie_sack_drop`, `nest_spawn`, `nest_break`, `jingle_errand_clear`, `guest_layer_peel`, `guest_shell_crack`, `jingle_guest_reveal`, `jingle_cap_break`, `embroidery_stitch`, `jingle_rank_up`, `rounds_step`, `rounds_done`, alias `fitting_preview` | AUD |
| RM-6 | Promoted `bgm_tower`, `jingle_tower_floor`; `rule_chip`, `title_equip`, `lantern_fill`, `lantern_open`, `note_pick`, `moth_hiss`, `market_open`, `weave`, `moon_rise`, `festival_round`; `bell_boss_ring` (RD-7). (CR4-21) `dustking_reform` (errand 3's re-form: dust gathering whoosh + soft thump, +1 semitone per re-form, SYN). (CR4-20) `amb_market` (야시장 골목 bed: string-light hum, awning flaps, far stall bells, no voices; AMB) and the place layer in the ambience director: `AppSignals.PlaceChanged` swaps the base bed, modifier layers stay on top | AUD |
| RM-7 | `walk_step`, alias `charm_pick`, `crest_light`, `merchant_whisper`, `walk_rest`, `jingle_walk_clear`, the new score `bgm_lanternroad` (3/4, celesta + bowed glass, D Dorian), `research_start`/`research_done`, `keepsake_get`/`keepsake_place`/`keepsake_polish` | AUD |
| RM-8 | Signature sets for the two new lineages (hit, fire, crit keys); place 3–6 boss cues; the place beds `amb_station`, `amb_rainbridge`, `amb_rooftop`, `amb_lighthouse` (CR4-20); reserved `ui_relic_up` mapped to keepsake polish if wanted | AUD |
| RM-9 | `call_early` (R2-39); R2-40's two new pieces ('마지막 음', '괘종시계 왈츠') and the record picker; box open and mimic gulp; letter cues | AUD |
| RM-10 | Reserved `ui_decor_place`; visitor, postcard and seasonal-pack cues | AUD |
| Any time after RM-2 | The optional voice barks: 21 lines, Higgsfield speech models only, `get_cost` first, after the user's terms check (Q12). Silent until then | AUD; user |

### 7.5 Wiring windows (why accessors come first)
- **Replacement art** keeps the same file names and needs no code: B6b, B9, B10, `stage_blackout`, enemies. It can land at any time; the next gate's rebuild picks it up.
- **New names** need builder code. ART-UPGRADE §4.3 #3–#11 and #17 touch several files whose last specced owner is an RM-2 package: `SettingsSceneBuilder`, `SettingsPopup`, `SynergyCodexPopup`, the kit. (`MenuSceneBuilder` gets phase-5 owners only through delta 23.) So the wiring is written in RM-2 against RM-1's fallback-safe accessors, even if the art comes later (RD-5).
- SPEC5's and SPEC6's new names follow the same pattern: R2P-6 or S6P-6 add the accessors, and R2P-4 or S6P-4 use them.

---

## 8. Risks (top first)

| # | Risk | Likelihood · impact | Mitigation | Owner |
|---|---|---|---|---|
| 1 | **Credits.** 46.88 in the balance against ≈1,000 for the specced art. Without the top-up, costumes are invisible, the forms cannot ship, place 2 looks like place 1, and story pages are text-only | high · high | The ladder; fallbacks for everything except the forms; RD-7 only when funded; one message to the user with the totals (Q1) at G1 | orchestrator, art lane |
| 2 | **No version control.** About 10 agents edit one working tree, and the user's editor is open on it | high · high | One owner per file; lock files; staged patches; snapshots per gate; handoff notes; edits only at merge points | every package |
| 3 | **Spec drift.** SPEC4 cites line numbers from 2026-11-03 23:30; UX wave 2, WP-IA, FX-JUICE and audio have moved those files since. SPEC5 assumes SPEC4 as written | high · medium | §3 deltas; every package re-reads its files at start; P4-0 publishes a contract-delta note; the SPEC5 critic in RM-3 (RD-9); SPEC6 written against real code | P4-0, critic |
| 4 | **Scope and time:** ≈145 agent-days specced, ≈227 total (CR4 additions included) | high · medium | Value order; stretch and P2 items are cut-safe behind FeatureIds; overlap mode (RD-10); gates stop a broken foundation from spreading | orchestrator |
| 5 | **Economy.** SPEC4 adds +45 % 별조각/week, SPEC5 +8 %, plus first-month bursts. The curve changes how current builds feel. (CR4-01/02) The content tests sum modelled rows, while the measured flows ride the stage: SPEC4's ch6+ chapter chest and the shipped shop-level mail scale with R2-04's chapter pace | high · high | The curve lands first (RD-1) with C-01; `EconomyBudgetTests` per spec; RD-13 (chest row) and RD-14 (measured per-source bands, the C-01 gem-line cap as the knob); projection runs at G1, G3, G5, G6 and G7; all knobs live in content | P4-C, P4-8, P4-9, R2P-7 |
| 6 | **First-session overload.** Chapters 1–10 take 34 minutes; SPEC4 plus SPEC5 would fire ≈19 unlock cards | medium · medium | The pacing core in RM-1 (Q2); 180 s spacing; play-time floors; FTUE suppression; manual check | P4-0, P4-8, R2P-5 |
| 7 | **Shell and kit exceptions** (SX-1, SX-2, the hub extension) edit frozen files | medium · medium | Member-listed exceptions, fallback-safe; the popup fallback for hubs; no other shell edits | P4-0, R2P-0 |
| 8 | **Audio integration.** The engine is still being written; side battles swap the simulator under `BattleAudioDirector` | medium · medium | Delta 27; `AudioTimingTests`; silent fallback; the call sites belong to the file owners; `UISfxKey` is append-only | AUD, R2P-1 |
| 9 | **The Unity environment.** The batch license works only with `-quit`; PlayMode cannot run in batch; the user's editor can catch half-written files mid-import; a `SceneVersion` bump rebuilds the user's scene | medium · medium | `verify.ps1` on the copy; the dev player for PlayMode checks; check `Editor.log`, touch or Reimport stuck files; bump `SceneVersion` only after green gates; `SavePathOverride` for tests | gate owners |
| 10 | **Untestable here:** notifications, iOS haptics, device performance | medium · low | Pure planner tests; the isolated Platform asmdef; manual device checklists at user checkpoints 2–4 | P4-7, user |
| 11 | **Art QA load and style drift** (≈190 images at max) | medium · medium | The ART-UPGRADE QA gate; references by job id; ≤ 2 re-rolls; parallel lanes split by batch (file-disjoint); no restyle of udu/lala | art lane |
| 12 | **Performance:** side battles, techniques, riders and FX-JUICE density on phones | medium · medium | PerfProbe scenarios at G3, G5, G6 and G7; the FX density governor; the double hit is a proc, not a projectile | P4-9, R2P-7 |
| 13 | **Ownership gaps** like delta 23 (`MenuSceneBuilder` in SPEC5) | medium · low | The §5 matrix is the single source; a package that finds an unowned file reports it before editing | orchestrator |
| 14 | **HUD and rail crowding.** The rails are full after R2 | low · medium | Every SPEC6 entry uses a hub page, the ≡ menu or a popup; IA keep-out validation | SPEC6 authors |
| 15 | **Unspecced waves** (RM-8…RM-10) are ±50 % | – · medium | SPEC6 plus a critic before RM-8; the RM-8 art can be made during RM-7 once SPEC6 lists it | SPEC6 authors |
| 16 | **Name collisions** ('분실물', '산책', '한 땀', '선반') | low · low | Content lint for duplicate display names across systems (SPEC5 risk 11) | R2P-5 |
| 17 | **(CR4-05) 단추 rewards and prices become decorative** from week 2 (`GoldMinutes` follows the shop rate, not clears) | high · medium | Q15 (SPEC5 §4.6) in RM-4/RM-5 with a harness gate; if declined, errand 1 pays materials instead and the 단추-minute sinks (workbench, polish, market, merge, decor) are re-priced | orchestrator, R2P-3, R2P-5 |
| 18 | **(CR4-07, CR4-08) The wall comes back** at ≈ch55, earlier for `auto` players, and wave B's unsimulated power can collapse chapters 12–25 again | medium · high | RD-15 (자동 편성 v2); the §4.4 bands bind at G5, G6 and G7 (delta 35); Q16's post-launch cadence scheduled from projection #3's measured ceiling | R2P-3, R2P-7, SPEC6 authors |

---

## 9. Value order: what the player sees after each gate

Why this order:
1. **Fix the runway before adding wrappers.** The shipped economy makes every 단추 axis free within a day, and the R2 numbers only hold on the fixed curve (IDLE_RESEARCH2 critic, ordering rule 1). It costs about one agent-day.
2. **Prove the frame and turn sound on.** Both are already built; wiring them is cheap, and they are felt every second of play. The user asked for punchier hits, and sound is part of hit feel.
3. **SPEC4 next.** It holds the first-hour and first-week features (story, gacha reveal, first week, daily gift, defeat help, records, codex, affinity, events), and every SPEC5 system builds on its contracts.
4. **SPEC5 wave A** gives the core verbs and one daily moment of real play.
5. **Visible content before deep side systems** (RD-7). A new place and new forms change the main screen and the collection. A tower or a roguelite lives behind a hub.
6. **The deep systems** of B1 and B2.
7. **Content cadence** (places, stories, lineages), then the low-art wave C systems, then the art-heavy wave D.
8. **Inside every wave,** P0 comes before P1 before P2. Credits follow the ladder.

| After | The player gets |
|---|---|
| G1 | The same game, but progression has weight again: no 단추 explosion, no flood of 13k 별조각 a week. Unlock cards arrive spaced (Q2). Sheets and the centre button make sound. The new gacha bundle, shop cracks and yarn death art are ready for RM-2 |
| G3 | 동화책 for chapters 1–5 and the chapter map, the gacha reveal, the codex with rewards, 수선점 기록, affinity and petting, the event calendar and hub, 다시 만난 날, 첫 주, 오늘의 작은 선물, the defeat-analysis ring, the battle record, lamp policies, 강해지기 with advice and 친구들의 응원, boss hints and the enrage tick, the expedition ladder and resend-all, dungeon batch and blessings, save import/backup/share, local notifications, the season track, coupons, fireflies, the ticket bank, iOS haptics. Sound on every screen |
| G5 | 밤 나들이; 그림자 까치 and 빨랫줄 errands; 이번 주의 손님 at the shop door with the Monday reveal; 수선 비법 and 수선공 등급; 한 땀 돌파 and 추억 수놓기; 오늘의 순회; 맞춤 편성 and a 자동 편성 that fields the strongest attackers (CR4-06); 단추 rewards and the night basket that still count in week 2+ (Q15); a costume for all seven lineages, with a fitting room |
| G6 | **야시장 골목**: a new field with its own night sound, magpies and nests on the main ladder, the bell boss with its hint; **six new forms**; 보관소 오르기 with trial floors; titles, profile and shop name; 수선 축제 주간 and 보름달 밤; the lantern and night moth; route marks; the weekend market; 재료 엮기; errand 3 |
| G7 | 등불 길 (charms, crests, a borrowed friend); 수선점 안채 with 밤의 작업대 and 기념품 선반; resonance skill riders |
| G8 | 기차역, 빗물 다리, 다락방 거리 and 바닷가 등대, each with its storybook page; two new friends ('한 짝 장화', '풍경'); 20 storybook puzzle stages; a costume every season |
| G9 | 꿈길 and the full 등불 길; 끝없는 밤, personal boards, friend codes and the share card; letters, gifts and anniversaries; hidden records; the box hunt; 오늘 밤의 의뢰 and custom request codes; 한발 먼저; 축음기 음반 |
| G10 | A room to decorate, night visitors, postcards, seasonal packs |

---

## 10. Coverage: where every remaining item lands

| Source | Items | Wave | Note |
|---|---|---|---|
| SPEC4 §1 + §13, P0/P1 (all) | ATK-06, ATK-07, ATK-08, FX-06, AUD-01, IDLE-06, SET-01, SAVE-01, NOTI-01, CR-09, F-03, IDLE-05, IDLE-04, Q-04, Q-05, CR-10, PRG-04, PRG-05, PRG-01, CR-01…CR-06, CR-07, EVT-01, PRG-06 chapter chest, PRG-02, PRG-03, FX-01, FTUE-01, IDLE-01, F-04, Q-01, CR-11, ATK-01/02/05 polish, NEW-01, NEW-02, Critic-A7 | RM-1 (contracts) → RM-2 (build) → RM-3 (verify) | AUD-01 runs through AUDIO-SPEC (engine) plus RM-2 wiring; CR-07 becomes the projection checks (RM-1, RM-3) |
| SPEC4 stretch | Q-06, PRG-06 lost chest, PRG-07, EVT-02, NEW-03, NEW-04, FX-05 iOS | RM-2, last in each package (RD-2) | status recorded at G3 |
| SPEC4 decision | PRG-08 | – | do not build (SPEC4 §8.7) |
| PROGRESSION-SIM | second taper, CR-07 retune, rating repair, projection checks | RM-1 (P4-C), RM-2 (P4-0/1/8), RM-3 (P4-9) | RD-1 |
| PROGRESSION-SIM follow-ups no wave had (CR4-05…08) | §7.3 "attack first" auto-formation → 자동 편성 v2; §6.1 offline/minute 단추 indexed to clears → Q15; §7.4 ceiling cadence → Q16; per-source 별조각 and the `auto` variant in every projection | RM-5 (R2P-3, R2P-0/1/2/3/5/7), G7 → SPEC6 | RD-14, RD-15, Q15, Q16 |
| SPEC5 R2-0 | R2-00 infrastructure, pacing, hub sheets | RM-4 (+ the pacing core in RM-1 if Q2) | – |
| SPEC5 wave A | R2-03, R2-04, R2-05a, R2-01 (errands 1–2), R2-07, R2-08, R2-36a | RM-5 | – |
| SPEC5 B1 | R2-02, R2-09 + R2-20, R2-13 + R2-14, R2-15, R2-17, R2-18, R2-19, errand 3 | RM-6 | – |
| SPEC5 B2 | R2-10, R2-11, R2-12, R2-05b; R2-37 place 2 and R2-36b step 1 | RM-7; place 2 and the forms in **RM-6** with RD-7 | – |
| Content expansions | six costumes (R2-36a) | RM-5 | the art is L3 |
| | +1 form per 3-form lineage (R2-36b step 1) | RM-6 (RD-7) or RM-7 | the art is L5 |
| | new places, fields and enemies on the main ladder | place 2: RM-6/RM-7; places 3–6: RM-8 | SPEC6-A |
| | storybook pages and stages | ch1–5 storybook: RM-2 (SPEC4 CR-05); one page per place (R2-27): RM-8; puzzle stages (R2-38): RM-8 | SPEC6-A |
| | new lineages (R2-36b step 2) | two in RM-8, then a cadence (Q11) | SPEC6-A |
| | season costumes | first: RM-7; then one per season (Q8) | – |
| IDLE_RESEARCH2 wave C (P2) | R2-21 (incl. the branching map and charm themes), R2-22 (no league; incl. 끝없는 밤, friend codes, the share line), R2-23 (incl. loved-material gifts, lore days, requests, badges), R2-26, R2-16, R2-38b (incl. 의뢰서 만들기), R2-39, R2-40; R2-37 interactables (optional) | RM-9 | SPEC6-B; the v1.2 sub-features listed by CR4-23 |
| IDLE_RESEARCH2 wave D (P2) | R2-29 (incl. favourites, mended visitors, 이웃 수선점 구경, the shop tap), R2-32, R2-33 | RM-10 | SPEC6-C; second top-up |
| ART-UPGRADE-PLAN | W0, W1, B8a, B5: done; B8b L2; B6b, B3b, B7b, B9 L4; B10 and OPT tangle L7; OPT extras L8; udu/lala restyle **rejected** (orchestrator) | §7.2 | – |
| FX-JUICE | 6 decals + flipbook (L7); enemy hurt faces (L8, "later" in FX-JUICE §0.5) | art lane | – |
| AUDIO-SPEC | ears list, wave C remainder, call sites, settings rows, `AppSignals`, R2 keys and music states, R2-40 pieces, reserved P3 keys, voice barks | §7.4 | – |
| Unseen findings (#71–#121) | all 51 dispositioned in IDLE_RESEARCH2 v1.2 (merge log) | through the R2 items above | nothing left outside an R2 item |
| Deferred / rejected (unchanged) | D-01…D-21 (incl. pets, pots, mini-games, attic dig, photo album, errand 4, codex sets and cape, presets, risk contracts, lane geometry); R2-06, R2-24, R2-28 removed; the NPC league; prestige, gear gacha, summon level, guilds/PvP/server features, ads, a free daily pull | – | revisit triggers as written in SPEC4 §13.10 and IDLE_RESEARCH2 App. C |

---

## Appendix A. Effort by package (agent-days)

| Wave | Package | Effort | Basis |
|---|---|---|---|
| RM-1 | P4-0 | 2.0 | SPEC4 1–1.5, plus critic additions, pacing core and SX-1/SX-2 |
| RM-1 | P4-C | 1.0 | SPEC5 R2P-C ≈1 (the rating repair moved to RM-2) |
| RM-1 | WP-9 | 2.5 | UX-SPEC effort M, plus the IA deltas |
| RM-1 | P4-10 (L0, accessors) | 1.0 | – |
| RM-1 | AUD | 1.0 | – |
| RM-1 | slack (sim sanity run, gate) | 0.5 | – |
| RM-2 | P4-1 | 5.75 | SPEC4 4–5, plus S5-24 and the chest row (CR4-01) |
| RM-2 | P4-2 | 5.0 | SPEC4 4–5, plus the reminder mail |
| RM-2 | P4-3b | 4.5 | SPEC4 4–5, minus BattleSfx and T3, plus the nameplate |
| RM-2 | P4-4 | 4.75 | SPEC4 4, plus FrameSceneBuilder, audio settings, art wiring and every ≡ tile (CR4-14) |
| RM-2 | P4-5 | 3.5 | SPEC4 3, plus art and audio wiring |
| RM-2 | P4-6 | 5.75 | SPEC4 5, plus the IA rail cells, art and audio wiring, `CardReady` (the ≡ tiles moved to P4-4) |
| RM-2 | P4-7 | 2.5 | SPEC4 2.5 |
| RM-2 | P4-8 | 3.75 | SPEC4 3, plus the three-segment fit, S5-4, pacing rows and the ch6+ chest row |
| RM-2 | P4-10 | 2.5 | QA and slicing of L1/L2/L4/L7 |
| RM-2 | AUD | 0.5 | – |
| RM-3 | P4-9 | 2.75 | SPEC4 2.5 (the band test replaced by projection #1), plus the per-source 별조각 log (CR4-02) |
| RM-3 | fix lane | 2.0 | estimate |
| RM-3 | SPEC5 critic | 1.0 | – |
| RM-3 | R2P-0 staging | 2.0 | SPEC5 R2P-0 ≈2 |
| RM-4 | R2P-0 apply, R2P-C, R2P-6, AUD, gate | 1.75–3.25 | + the income and place hooks (CR4-05, CR4-20) |
| RM-5 | R2P-1 5.25 · R2P-2 6.5 · R2P-3 3.75 · R2P-4a 6.25 · R2P-5 3.5 · R2P-6 2 · AUD 1 · R2P-7 2.75 · merge 1 | 32 | SPEC5 §8.4 package figures, plus CR4-05/06/09/10/11 (+3) |
| RM-6 | R2P-1 5.75 · R2P-2 6 · R2P-4b/4c 5.5 · R2P-5 5 · R2P-6 2.5 · R2P-7 2 · AUD 1.25 · merge 1 | 29 | SPEC5 B1, plus RD-7 (4.5), plus CR4-12/20/21 (+0.5) |
| RM-7 | R2P-1 3.5 · R2P-2 8 · R2P-3 1 · R2P-4c 6.5 · R2P-5 3 · R2P-6 1.5 · R2P-7 2 · AUD 1 · merge 1; SPEC6 authors 3 | 27.5 + 3 | SPEC5 B2, minus RD-7 |
| RM-8 | S6P-0 1.5 · S6P-1 3 · S6P-2 2 · S6P-4 3 · S6P-5 8 · S6P-6 2 · AUD 1.5 · S6P-7 2 · merge 1 | ≈23.5 | IDLE_RESEARCH2 sizes; ±50 %; + the place beds |
| RM-9 | wave C (R2-21 L, R2-22 M–L, R2-23 M, R2-38b M, five S items, v1.2 extras ≈6), plus contracts and verify | ≈33 | IDLE_RESEARCH2 §6 and §8; ±50 % |
| RM-10 | wave D (R2-29 L, R2-32 M, R2-33 L), plus contracts and verify | ≈22 | ±50 % |

## Appendix B. Evidence (read-only checks on 2026-11-04)
- **Higgsfield `balance`:** `{"credits": 46.88, "subscription_plan_type": "plus"}`.
- **Spend since the 530.38 balance:**
  - FX-JUICE 220 (`fxj_art/report.json` `credits_spent`);
  - ART-UPGRADE batches B1 45, B2 35, B3a 20, B4 15, B6a 45, B7a 5, B8a 60 (from `artmax/gen/*/jobs.tsv`);
  - QA 5 (`artmax/qa_report.md`);
  - B5 28;
  - the frame outpaint and icon about 5.5 (`ia/`).
- **Project facts:**
  - `IdleAutoSetup.cs:21` `SceneVersion = 1`;
  - `MenuSceneBuilder.cs:66` `BuildGrowthPanel`, and `MenuSceneBuilder` also builds `ShopPanel`, `BagPopup` and `ExpeditionPanel`;
  - `UiArt.cs:63–72` has the B5 accessors and none of the B3b/B7b ones;
  - `MetaContent.Features.cs:239–248` `FeatureMedalPath`/`FeatureBanner`;
  - `MetaDatabase.cs:334` `PowerConfig` (`recommendedGrowth = 1.12`);
  - `GameConfig.cs:20` and `GameConfig.asset:21` `goldPerSecondGrowth 1.1`;
  - `MetaContent.IdleChest.cs:41` `stageGrowth = 1.02`;
  - `BattleConfig.cs:84–86` (one taper at 30, late HP 1.07);
  - `Tests/EditMode/IdleChestTests.cs` is the only test touching those constants;
  - `MainTabController.cs:247` plays only `UISfxKey.Tab` (SheetOpen 12, SheetClose 13 and MainButton 14 exist but are not wired);
  - `Scripts/Core/Audio/` has 16 files;
  - `Audio/{Sfx 644, Bgm 10, Amb 30}` takes;
  - `Scenes/Main.unity` contains no `BossChallengeButton`;
  - there is no `Core/AppSignals.cs` and no `Systems/Meta/IMetaModule.cs`.
- **Specs:**
  - SPEC4 §9.1–§9.4 (packages, merge order, ≈49 agent-days), §10.1 (A1–A14), §12, §13.10;
  - SPEC5 §0.3, §0.4 (S5-1…S5-24), §1, §2.6, §2.8, §4, §8, §9, §10, §11, App. D;
  - IDLE_RESEARCH2 §6 (waves C/D), §8, §9, the critic ordering and the v1.2 merge log (51 of 51 dispositioned);
  - ART-UPGRADE-PLAN §1.2–§1.3, §3 (B3b–OPT), §4.3, §5, §7;
  - FX-JUICE-SPEC §0.4–§0.5, §5.1, §6;
  - AUDIO-SPEC §0.1–§0.4, §2.11, §7.10, §8.1–§8.5;
  - UX-SPEC §4.0–§4.3;
  - UX-SPEC-IA §7.0–§7.1, §8.8, §9.0;
  - PROGRESSION-SIM §0, §1, §8, §9.

---

## Critic revisions (v1.1, 2026-11-04, role: critic)

**Scope of the review.**
- **Read in full:** this roadmap v1.0 and `phase4b/PROGRESSION-SIM.md`.
- **Read in the parts the roadmap builds on:**
  - SPEC5 v1.0 §0–§10 and App. C–D;
  - IDLE_RESEARCH2 v1.2 §0, §1, §5 (R2-36b, R2-37), §6, §7, §9, the critic list and the v1.2 merge log;
  - SPEC4 v1.1 §1, §2, §9.2–§9.3, §11.2, §13.10 and App. B;
  - UX-SPEC-IA v1.1 §0–§5 and §8;
  - AUDIO-SPEC §2.8, §2.11 and §3.12;
  - UX-SPEC §2.1.3 and §2.1.6.
- **Measured, not modelled:** the harness runs in `phase4b/runs/` (12-seed `R_REC_r2`, plus `R_REC_none`, `R_REC_r2auto`, `R_REC_r2heavy` and `R_REC_r2light`), recomputed per week from `daily.csv` (`gems_earned`, `shop_level`, `highest_cleared`, `gold_per_s`, `gold_clears`).
- **Code, read-only:**
  - `Core/GameEnums.cs` (every enum end) and `Systems/Meta/RedDotSystem.cs`;
  - `Systems/Meta/IdleChestSystem.cs` (`doubleExcludesGem`, quick idle);
  - `Systems/Meta/MetaRewardService.cs:10` (`GoldMinutes` = `Growth.GoldPerSecond` × 60 × N);
  - `Systems/RewardMailSystem.cs` + `Data/GameConfig.asset:22–23` (별조각 50 every 10 shop levels);
  - `Systems/Battle/BattleSystem.cs:488` (`StageGold`) and `Editor/Content/MetaContent.IdleChest.cs`;
  - `Editor/Builders/FrameSceneBuilder.cs:222–245` (the ≡ tiles), `Scripts/UI/UIManager.cs:443` (`PushBackHandler`) and `Verify/Runtime/AutoShot.cs:75` (`MenuTileOrder`).
- **Edited:**
  - this file;
  - `SPEC5.md` (→ v1.0.1);
  - `IDLE_RESEARCH2.md` (→ v1.2.1);
  - `phase4b/PROGRESSION-SIM.md` (critic notes).

  Backups are in `phase4b/critic/*.pre-critic.md`. No project file was edited, and no Higgsfield call was made.

**Verdict.**
- **What holds up:** the wave order, the one-owner model, the credit ladder and the coverage table. The enum values, save field names, red-dot paths and popup kinds of SPEC4 and SPEC5 agree with each other and with the code.
- **What would have failed a gate or left the game feeling flat** (five problems):
  1. The economy gates compared a model with a measurement. Two stage-indexed faucets that ride R2-04's chapter pace were in nobody's budget.
  2. After the curve retune, every "단추 N분치" in SPEC4 and SPEC5 is decorative from week 2.
  3. The curve fix moves the wall to ≈ch55 but does not remove it.
     - The sim's two follow-ups had no wave: a better 자동 편성, and content cadence planned against the ceiling.
     - RD-7 overstated what the six forms do.
  4. Four spots ignore the new frame: the ≡ menu size, side battles started from a sheet, Android back during a side battle, and the place-rule chip.
  5. There were two ownership overlaps and two ownership gaps.

### Changes made (each tagged `(CR4-nn)` in the body)

| # | Type | Finding | Fix (where) | Evidence |
|---|---|---|---|---|
| CR4-01 | Economy (cap) | SPEC4's chapter-chest default row pays 별조각 150 on **every** first x-10 clear from ch6 on, and SPEC4's budget row covers ch1–5 only. On the package curve the engaged player completes 5.0 / 10.5 / 9.0 / 6.5 / 4.5 chapters in weeks 1–5. The row therefore adds ≈750 / 1,575 / 1,350 / 975 / 675 별조각 a week (a heavy player ≈3,300 in week 1). That is 4–6× R2's whole ≤ 250 guard: R2-04's pace causes it, and no budget counts it | RD-13, delta 29: 별조각 only on chapters divisible by 5 (100), materials on every chapter; a new budget row ≤ 200 per 10 chapters (P4-8, P4-1) | SPEC4 App. B.2 and §11.2; `R_REC_r2` weekly chapter counts (12 seeds, median) |
| CR4-02 | Economy (gate) | G5's band (week-4 별조각 2,600–3,400, SPEC5 §4.4) and SPEC5's "≈2,850/week" rest on SPEC4's **modelled** 2,640. The harness **measures** 2,399/week from the pre-SPEC4 sources alone (R2 off) and 3,609 with R2-04/05 (no C-01). Its basket credits ≈26–39 gem-hours a day (24 h plus the free and ticket quick idles), not SPEC4's 18, and the shop-level mail adds 350–550 a week in weeks 2–4. With SPEC4 built, C-01 on and wave A in, week 4 projects to ≈4.6–4.9k unfixed, ≈3.7–4.1k after CR4-01, and ≈3.3–3.8k with the C-01 gem-line cap at 1.0. PROGRESSION-SIM §8.4 (3k–4.5k) and SPEC5 §4.4 also disagreed | RD-14, delta 30: per-source logging from RM-3 and the measured baseline at G3; SPEC5 §4.4 bands become 2,800–3,800 (week 4) and 2,600–3,600 (week 8). Knobs: the C-01 cap 1.5 → 1.25 → 1.0, then `levelRewardGem` 50 → 30. PROGRESSION-SIM §8.4 is marked as harness-only | `analysis/report_tables.md` gems table; recomputed week deltas; `GameConfig.asset:22–23` |
| CR4-03 | Economy (guard window) | SPEC5 §4.5's "week 4+" rates assume the stage has stopped climbing. G5 measures week 4, when it still climbs ≈85 stages a week. Errand milestones (≈1.7 per errand) and tower trial floors (≈2.9) then pay ≈60 + ≈57 instead of ≈18 + ≈25. R2's own week 4 is therefore ≈230 (A + tower) to ≈295 (with B), not 206 | delta 31: the ≤ 250 guard is the mean of weeks 5–8; week 4 is reported with a ≤ 350 limit; SPEC5 §4.5 gains a week-4 column | sim stages 262 (day 14) and 457 (day 30); SPEC5 S5-8, S5-9 |
| CR4-04 | Contract / test | SPEC4's `EconomyBudgetTests` lifetime rows sum content: records ≤ 3,500 (3,400) and codex ≤ 2,700 (2,615). They fail once R2P-5 adds the Q-04 R2 lines (+800) and the R2 codex rows (+520). SPEC5's "Q-04 R2 lines ≤ 850" leaves no room for RM-9's 15 hidden records (+150), which IDLE_RESEARCH2 budgets inside ≤ 1,000. RM-8's two lineages push SPEC4's affinity row (≤ 4,000 for 7 × 540) to 4,860. R2-27's pages (50 each) have no row in IDLE_RESEARCH2 §9.1 | delta 32: SPEC4 rows count SPEC4 ids, the R2 block owns R2 rows, and Q-04 R2 is ≤ 1,000 (SPEC5 §10.2). SPEC6 re-bases affinity per lineage (≤ 571) and adds the R2-27 row (IDLE_RESEARCH2 §9.1) | SPEC4 §11.2; SPEC5 §4.5, §10.2; IDLE_RESEARCH2 §9.1 |
| CR4-05 | Curve side effect (economy) | `GoldMinutes` resolves at the shop's 단추/s. On day 1, clears already earn ≈37× that rate per online minute; after the retune they are ≈100% of 단추 from week 2. "단추 60분치" is 3.0% of a day's 단추 on day 1, 1.5% on day 6, 0.003% on day 13 and ≈0 from day 20 (6.9e17 against 1.1e27 on day 29). Errand 1 pays only 단추 minutes, so it pays nothing. The guest band, tower floor, walk step 5 and lantern 단추 lines vanish too. Every 단추-minute price becomes free: workbench research, keepsake polish, rename, market, merge, R2-29 decor, and the R2-22 and R2-38b rewards. SPEC5 §4.2 accepted this for the basket only | Q15 (default yes), SPEC5 §4.6 and delta 33: the `IncomePerSecond` hook (R2P-0); `ClearGoldAt` (R2P-1); `GoldMinutes` resolved through it (R2P-3); the night basket in 단추 minutes per hour away (R2P-2); the knobs and N values tuned from projection #2 (R2P-5, R2P-7) | `R_REC_r2` `gold_per_s` vs `gold_clears`; `MetaRewardService.cs:10`; PROGRESSION-SIM §6.1 |
| CR4-06 | Missing feature / curve | PROGRESSION-SIM §7.3: AutoFormation ranks by `attack/interval×10 + hp`, ignores the style damage scales and 공명, and fields tanky Defense forms. An `auto` player reaches ch25 on day 20.3 instead of 13.3 and stalls at 359–403 instead of 535–571. The sim asked for an "attack first" sort, but no wave had one. SPEC5's `BuildBiased` serves side battles only, and SPEC4's 강해지기 row recommends AutoFormation whenever its *total* power preview wins by 5% | RD-15, delta 34: 자동 편성 v2 ranks by offence power (R2P-3, RM-5); the advisor row compares offence; the `auto` variant joins projection #2 with ch25 ≤ day 17 | PROGRESSION-SIM §7.3; `R_REC_r2auto` runs; SPEC4 §13.2 |
| CR4-07 | Curve (wall) | The fix moves the wall to ≈ch55 (stage 535–571, days 49–71). The sim calls this a content problem, not a curve problem, and asks for content scheduled against it. RD-7 called the six forms "the scheduled ceiling raise", but they are not one. Each is ×1.35 over its tier-1 parent in one of a lineage's two slots, which is at most ≈+19% team attack, or 5–15 stages at ×1.02/stage. A launched player also has them from day 0. Places 3–6 at ch11/16/21/26 are all reached by day ≈14 | RD-7 and §4.6 wording fixed. Q16: post-launch cadence set from projection #3's measured ceiling, and SPEC6 evaluates a compounding milestone row. SPEC6 spaces `fromChapter` about every 10 engaged days; G7 reports the ceiling | PROGRESSION-SIM §7.4, §5; IDLE_RESEARCH2 R2-36b; `FormStats.TierGrowth` 1.35 |
| CR4-08 | Curve (validity) | The curve was validated with R2-04/05 only. Wave B adds keepsakes, the workbench, riders, new forms and codex bonuses, and SPEC4's extra gems buy 공명 and breaks sooner. The §4.4 bands were enforced at G5 only, so wave B could turn chapters 12–25 back into a sprint (sim §5: a larger 등급 did exactly that) | delta 35: the bands bind at G5, G6 and G7. Knob order: keepsake values → workbench `perLevel` → riders → codex percentages | PROGRESSION-SIM §5, §8.5, §9 |
| CR4-09 | Frame (IA) | SPEC5 sizes the ≡ menu at "8 tiles, 3 rows, `BuildPopup(900, 960)`", and capture 63 asserts 3 rows. UX-SPEC-IA §8.8 already puts SPEC4's EventHub, SeasonPass, Codex, Achievements and Coupon tiles in the menu. Inserting tiles after `EventTile` also breaks `AutoShot.MenuTileOrder` | RD-17, delta 36: rows = ⌈tiles/3⌉, height `300 + 220·rows`. The R2 tiles go after SPEC4's activity tiles and before 가방 · 공지 · 설정. Capture 63 asserts the tiles and the fit, and the verify owner updates `MenuTileOrder` (SPEC5 §2.6, §7.1, §10.5) | UX-SPEC-IA §3.6, §8.8; `FrameSceneBuilder.cs:222–245`; `AutoShot.cs:75` |
| CR4-10 | Frame (IA) | A side battle started from the 밤 나들이 sheet runs **under the sheet**. Only the top band is visible; the deck strip, lamp and SideBattleBar are covered (IA §4.5: deck interaction needs MAIN). SPEC5 §2.11 #5 assumed "a side battle is always in MAIN anyway", but nothing made it so | RD-16, delta 39: `UINav.GoToMain()` before every live start; capture 53 asserts MAIN (SPEC5 §3.1, §7.1) | UX-SPEC-IA §1.2, §4.5 |
| CR4-11 | Frame (IA) | SPEC5's manual check expects Android back during a side battle to end in the abandon confirm. IA §4.7's order ends in the **exit** confirm instead, and `UIManager` is frozen | delta 39: `SideBattleBarView` pushes a `UIManager.PushBackHandler` that consumes back only when no popup and no sheet is open, then shows the abandon confirm (SPEC5 §2.6, §10.6) | UX-SPEC-IA §4.7; `UIManager.cs:443` |
| CR4-12 | Frame (HUD) | R2-37 and SPEC5 put the place rule "as one chip on the StagePanel". The StagePanel (330×134, M11) is already full (stage, boss progress, shop HP), and the SideHud covers it during side battles | delta 40: a second 40 px icon in the SynergyRow's modifier chip, with the rule in the tooltip; the ≤ 540 px row check stays (SPEC5 §6.9, IDLE_RESEARCH2 R2-37) | UX-SPEC §2.1.3, §2.1.6 |
| CR4-13 | Frame (shell) | R2-40 puts the gramophone button in the 안채's "sheet header". The shared `ContentArea/SheetHeader` is a frozen shell node, only 56 px tall, and its `RightSlot` is reserved for the P2 tall sheet | the button goes in the 64 px slot that SPEC5 §7.3 already reserves in HomePanel's own `PanelHeader` (IDLE_RESEARCH2 R2-40) | UX-SPEC-IA §4.2; SPEC5 §7.3 |
| CR4-14 | Ownership (overlap) | RM-2 gave P4-6 "≡ menu row 3 tiles". The tiles are built in `FrameSceneBuilder` and handled in `MenuPopup`, and both belong to P4-4 (delta 11). Delta 10's event entry had the same ambiguity | delta 36: every ≡ tile is P4-4's, and P4-6 provides the receivers. The rail `EventButton` route and SubPill are P4-6's; the `EventTile` route is P4-4's | `FrameSceneBuilder.cs:222`; SPEC4 §9.2 |
| CR4-15 | Ownership (merge order) | RM-1 merged P4-C → P4-0 with "P4-10 any time". But P4-0's SX-2 compiles against `UiArt.ButtonSoft9s`, which P4-10 adds | delta 37: P4-10's accessors merge first | §4.1, delta 19 |
| CR4-16 | Ownership (gap) | From RM-4 the art lane belongs to R2P-6. If the top-up arrives after G3, nobody owns P4-10's unfinished SPEC4 and ART-UPGRADE rungs (L1, L2, L4, L7) | delta 38: R2P-6 (then S6P-6) inherits them in ladder order | §5 Art lane row; §7.2 |
| CR4-17 | Ownership (overlap) | R2P-4b's charm pick reuses `BlessingPickPopup`. That file sits in `UI/Expedition/**`, which belongs to R2P-4c in wave B | delta 43: the file moves to R2P-4b for RM-7 | SPEC5 §6.3, §8.2 |
| CR4-18 | Ownership (doc) | SPEC5 §8.2 still gives R2P-C the curve, rating and calibration files, and R2P-0 the pacing core. RD-1 and Q2 moved them to RM-1/RM-2 | SPEC5 §0.3 and §8.2 now say "confirm only" and "only if Q2 was declined" | RD-1, Q2, delta 15 |
| CR4-19 | Contract | SPEC5 appends `AchievementCategory` members without explicit values, against its own rule. Its §2.10 "no other GameSettings keys" is stale (`VoiceBarks` comes from P4-0, `SheetAutoReturn` from WP-IA) | SPEC5 §2.2 (Errand = 17 … Firefly = 24) and §2.10 | SPEC4 §2.2 (17 members, 0–16); delta 13; UX-SPEC-IA §8.8 |
| CR4-20 | Audio gap | A new place changes the field but not the sound. AUDIO-SPEC's ambience director keys beds by stage modifier only, and `PlaceDef` has no sound field | delta 41: `PlaceDef.ambKey`, `AppSignals.PlaceChanged`, `amb_market` (RM-6) and four beds in RM-8; SYN/HYB, 0 credits (SPEC5 §2.3, §2.6, §6.9, §9.2) | AUDIO-SPEC §2.8, §3.12 |
| CR4-21 | Audio gap | Errand 3's re-form (`ObjectivePhase` on the dust king) has no cue, and nothing said which sounds the six new-form skills use | `dustking_reform` (B1). The new skills use existing `vfxId`s, so AUDIO-SPEC's per-vfx cues apply; any new vfx gets its own key (SPEC5 §6.9, §9.2) | SPEC5 §6.10, §9.2; AUDIO-SPEC §2.5 |
| CR4-22 | Art gaps | (a) `ModeIntroDef.art` (3 cards × 4 modes) had no list entry and no fallback. (b) Place fields exist only at 1080×2048, but the formation mini-field and the `field.png` fallback assume 4:3 (IA §1.4). (c) The six new-form skills have no icons. (d) RM-8's two lineages had no FX-JUICE impact/crit sprites or skill icons | (a) reuse mode art, 0 credits; (b) a 1080×810 PIL crop per place (rows 619–1428), 0 credits; (c) optional 30 credits, with the closest `skill_*` as fallback; (d) L9 +≈60 (§7.2, SPEC5 §9.1) | SPEC5 §2.3, §3.5, §9.1; FX-JUICE-SPEC per-lineage table |
| CR4-23 | Missing features (P2 scope) | RM-8 to RM-10 dropped v1.2 parts of their items: R2-22's 끝없는 밤, 쪽지 코드 friend rows with AUTO replay, and the share line; R2-38b's 의뢰서 만들기; R2-21's branching map and charm themes; R2-23's loved-material gifts, lore days, requests and badges; R2-27's chapter 1–5 letters; R2-29's favourites, mended visitors (they need per-enemy kill counters), 이웃 수선점 구경 and the shop tap | listed in §4.8–§4.10, §9 and §10, so SPEC6 cannot miss them | IDLE_RESEARCH2 §6 (v1.2 rows); merge log #71, #75, #81, #90, #100, #112, #113, #118, #119 |
| CR4-24 | Pacing (old saves) | `PlayMinutes` starts at 0 on migration. The user's long-running test save would wait up to 4.5 h of fresh play for the R2 cards (and, with Q2, the SPEC4 cards) | delta 44: the migration seeds min(270, 30 × install days) for saves past FTUE | SPEC5 §2.1, §2.8 |
| CR4-25 | Expectation | Between G1 and G5 the build has the curve but neither 수선 비법 nor 한 땀 돌파. By design it stalls at ch13–15 (stage 129–145) from about day 18–38 | stated at user checkpoint 1 (§4.1) | PROGRESSION-SIM §7.2 ("no R2" row) |

### Checked, no change needed
- **Enum values agree across SPEC4, SPEC5 and the code.**

  | Enum | Code ends at | SPEC4 adds | SPEC5 adds from |
  |---|---|---|---|
  | `CounterKey` | 30 | 31–37 | 38 |
  | `FeatureId` | 24 | 25–26 | 27 |
  | `MetaRewardType` | 7 | 8 | 9 |
  | `MetaConditionType` | 21 | 22–24 | 25 |
  | `MetaPopupKind` | 7 (`ChapterChest`) | 8–9 | 10 |
  | `CostumeUnlockType` | 6 | `Season` 7 | `Reward` 8 |
  | `EventScheduleType` | – (new in SPEC4, 0–2) | `Dates` 3 | `MoonPhase` 4 |
  | `AchievementCategory` | – (new in SPEC4, 0–16) | – | 17 (made explicit, CR4-19) |

  - The other code ends match SPEC5's appends: `StatType` 22, `SkillTrigger` 8, `EnemyGimmick` 1<<8, `BattleMode` 1, `StageModifierType` 6, `CurrencyType` 2.
  - No reserved number is used: `CounterKey` 53, 55, 56; `FeatureId` 37; `MetaRewardType` 12, 13; `BattleMode` 6, 7; `EventScheduleType.DateList` 6; `CurrencyType` 3.
- **Save field names do not collide.**
  - SPEC4's `MetaSave` sections (achievements … cheer) and SPEC5's (errands … routes) are disjoint.
  - SPEC5 extends `DailyGiftSave`, `EventInstanceSave`, `IdleChestSave` and `LineageSave` with new names only.
  - SPEC5 drops its earlier duplicates (`firefly`, `bankedTickets`, `zoneCompletions`, `parkedMain`).
- **Red-dot paths are consistent.**
  - `top.outings.*`, `top.home.*`, `hud.lantern` and `tab.characters.lineage.{id}.break` follow the `RedDotPaths` conventions.
  - The tree aggregates a node that is also a leaf (`tab.expedition.dungeon` + `.blessing`), so nothing collides.
  - The rails pick up the new parents through `QuickRailView`, so the 사냥 aggregate needs no change.
- **Popup kinds are consistent.** `GuestReveal = 10` follows SPEC4's `NotificationAsk = 9`. SPEC5's order table replaces "order by enum value" and agrees with the code's 0–7.
- **`CostumeUnlockType.Season` is reward-granted in both specs** (`jjak_starwalk`, then the season costumes), so the season costumes never auto-unlock.
- **Rails:** L2 = 안채 and R2 = 밤 나들이 are the only free cells, and there is no 더보기 overflow.
- **Ladder sums:** L0–L7 = 1,046 (`phase4b/roadmap/check_roadmap.py`). L0 (45) still fits the 46.88 balance.
- **The six forms:** RD-7's funding condition is right, and so is the rule that they have no shipping fallback.

### Open for the orchestrator
- **Q15** (the 단추 N분치 re-index) and **Q16** (the ceiling plan) are new and have defaults (§0.3).
- **RD-13…RD-17** are critic decisions under the same veto rule as RD-1…RD-12.

---

## Appendix C. Amendments after RM-2 (2026-11-06, orchestrator)

Applied in the RM-3 docs pass (T-5) from `scratchpad/roadmap/rm3/triage.md` §3. They record what RM-2 built and G2 verified; where they differ from older text above, they take precedence.
- **A-02 (ii) (R16, R71)** §3 delta 43: `BlessingPickPopup.cs` was not built in 4.1, so R2P-4b creates it.
- **A-10 (R31, R62)** §7.2 ladder: L4 265 → 275 (`Icons/stats`; `Icons/story` conditional); L7 121 → 86 (the 6 FX decals were delivered in FX round 2); cumulatives L4 666, L5 785, L6 935, L7 1,021, and in step L8 1,372, L9 ≈1,827, L10 ≈2,467; specced core 1,021, ≈975 from the top-up; the Codex-lane bullet. Where §0.2, §0.3 Q1, §0.4 and §8 still say ≈1,000, §7.2 takes precedence.
- **A-15** §6.1 graph: G3 bumps `SceneVersion` 3 → 4 (G1 took 2 → 3).
