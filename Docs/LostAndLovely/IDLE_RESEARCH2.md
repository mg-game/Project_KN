# LOST & LOVELY — IDLE_RESEARCH2: famous idle games, 2nd sweep → merged design backlog

Version 1.2 · 2026-11-04 · role: game designer / synthesizer; **revised by the critic** (v1.1, see "Critic revisions"; changed spots are tagged **(critic)**); **v1.2 merges the 51 findings v1 never saw** (role: R2 merger; changed spots are tagged **(v1.2)**; full table in the "v1.2 merge log" at the end) · **v1.2.1: the PHASE4-ROADMAP critic's corrections to the P2 items and budgets, tagged `(CR4-nn)`** (list at the very end; v1.2 backup: `phase4b/critic/IDLE_RESEARCH2.pre-critic.md`) · target `D:/UnityProject/unity_idle` (read-only; no project file was edited) · v1.1 backup: `research2/IDLE_RESEARCH2.v1_1.bak.md`

> **(v1.2) Read this second.** v1's input stopped after finding 71. The other 51 (`research2/unseen_findings.json`: the full text of #71 plus #72–#121, from the cozy, tower-defense and code-gap sweeps) are now triaged with the §2.4 rule: **9 already in SPEC4, 28 already in R2 (25 with a gap merged into the item, 3 fully covered), 2 new P2 items (R2-39 한발 먼저, R2-40 축음기 음반), 6 rejected, 6 deferred**. No P0/P1 priority changes, no new currency, no new growth axis, and the R2 별조각 and shard guards still hold (§9.1). Orchestrator decisions applied: no NPC league; no restyle of the udu/lala lineages; prestige, gear gacha and summon level stay rejected; audio comes from CC0 packs plus original synthesis.

> **(critic) Read this first.** v1 was merged against SPEC4 v1. SPEC4 v1.1 (§13, §14 C-15–C-19) already owns four of v1's systems: 첫 주 = v1 R2-06, 오늘의 작은 선물 = the free gift in R2-18, 길 잃은 반딧불 = the firefly in R2-15, 입장권 모아 두기 = the dungeon bank in R2-07. Its 친구들의 응원 (§13.2) also overlaps R2-28. SPEC4 §13.10 triages six more of them (weekly boss, research tree, roguelite, garden, presets, relics). This version removes the duplicates, makes the economy fit SPEC4 v1.1's real baseline (2,640 별조각/week), closes the basket and 선택 기억편 leaks, and adds the collection content the game lacks most (7 lineages, 29 forms, every costume on 짝짝이). Priorities are in §0; reasons and evidence are in "Critic revisions".

**Relayed user request:** "다른 유명한 방치형게임들의 레퍼런스를읽어서 부족한기능들 알아서 다진행해줘" (read the references of other famous idle games and build every missing feature yourself).
**What this is:** the second reference sweep (KR 키우기 games, global idle, cozy collectors) merged into one deduplicated backlog. Every system is adapted to our IP, sized, prioritised, and ordered in waves that start **after SPEC4** (phase 4). SPEC4 items are cross-referenced, not repeated.
**Inputs:** `IDLE_RESEARCH.md` (v1 backlog, §3 table, §17 deferred list), `SPEC4.md` (coverage §1, contracts §2, economy §11.2, art §10.1), `research2/kr_kiugi_notes.md`, `research2/global-idle-notes.md`, the art memory, the IA drafts `ia/ia_03_new_main.png` and `ia/ia_04_sheet_variants.png`, and code read on 2026-11-04 (`Editor/Content/**`, `Data/*.asset`, `Scripts/Systems/**`).

---

## 0. Top 15 at a glance (critic-revised order)

All of wave A needs **R2-00** first: the side-battle host, the two hub sheets and the v5 contracts (§3). Wave A also may not start until SPEC4 4.1 has merged **and SPEC4 CR-07 (`EconomyFlowTests`) has run**, because the R2-04 and R2-05 numbers depend on it (Critic revisions, CR2-08).

| # | ID | Name (KO) | What it is | P | Size | Wave |
|---|---|---|---|---|---|---|
| 1 | R2-03 | 이번 주의 손님 | Weekly big forgotten object to mend. A daily 90 s "peel the shadow" fight plus a week-long stitch meter, with a Monday reveal. **(critic)** Shell HP is locked for the week. The guest stands at the shop door and its shell peels off visibly. Every owned form adds +1% ('모두 함께'). Gems are trimmed to ≈90 a week | P0 | M | A |
| 2 | R2-04 | 수선 비법 | 단추 → stat technique tree, the first uncapped offense axis. **(critic)** v1's constants don't track the 1.0741 recommended-power curve, so CR-07 sets the curve first | P0 | M | A |
| 3 | R2-05a | 한 땀 돌파 | Lineage cap 100 → 200 in 5 breaks (기억편 sink). After the 5th break, '추억 수놓기' takes surplus shards. **(critic)** The skill riders move to R2-05b (P1), because they need new effect types | P0 | S–M | A |
| 4 | R2-01 | 밤 골목 심부름 (MVP) | **(critic)** Errands 1–2 ship in wave A: 그림자 까치 → 단추, 빨랫줄 → materials. Tickets store, failure is free, cleared levels sweep. Errand 3 comes in wave B. Errand 4 (shards of a chosen lineage) is deferred | P0 | M | A |
| 5 | R2-07 | 오늘의 순회 (+ banking extended) | A one-tap bundle of daily chores. **(critic)** The banking part reuses SPEC4 NEW-04 (`ticketBank`) for errands and guest attempts. The guest's live fight is off by default | P0 | S | A |
| 6 | R2-36a | 옷장 넓히기 (new) | **(critic, missing)** 6 costumes, one for each lineage that has none. All 10 existing costumes are 짝짝이's. They use existing unlock types plus `MetaReward.Costume` and give R2 modes rewards that are not gems | P0 | S + 30 credits | A |
| 7 | R2-08 | 맞춤 편성 | **(critic)** Replaces v1's manual memos (SPEC4 §13.10: revisit presets at >10 lineages). One-tap auto-formation for the mode (the guest's favoured traits, the floor's rule), and the main formation is restored on exit | P0 (support) | S | A |
| 8 | R2-02 | 분실물 보관소 | Ticket-free tower with trial floors. **(critic)** Moves to P1: the main ladder is already endless and ticket-free. The broken '특성 2종까지' rule is replaced, and floors sit under the CR-07 wall | P1 | M | B |
| 9 | R2-10 | 등불 길 (was 밤 산책) | Roguelite run: 1-of-3 charms, crests, a borrowed friend. **(critic)** Renamed (four '산책' names collided). Talents come from depth first-clears, so there is no 발자국 currency. Rewards are capped at 10 walks a week | P1 | L (MVP) | B |
| 10 | R2-12 | 기념품 선반 | Keepsakes with conditional placed effects and deterministic sources. **(critic)** No owned effects, because Q-05 owns those. Polish uses shine marks, not stars | P1 | L | B |
| 11 | R2-37 | 골목 지도 넓히기 (new) | **(critic, missing)** The endless ladder has 1 field, 7 enemies and 2 bosses. Add a new place every 5 chapters: background, 2 enemies, 1 boss with a hint. Pairs with the R2-27 storybook | P1 | M + art | B |
| 12 | R2-36b | 진화 가지 잇기 (new) | **(critic, missing)** +1 form for each of the six 3-form lineages, then one new lineage every 4–6 weeks. Its traits fill the thin trait graph (Rain has only 우두커니) | P1 | M + art | B |
| 13 | R2-11 | 밤의 작업대 + 안채 | Long-timer research. **(critic)** Trimmed to 3 branches. The combat branch duplicated R2-04, and nodes that touch the 별조각 line are gone | P1 | M–L | B |
| 14 | R2-13 | 수선 축제 주간 | Growth-event rounds on EVT-01. **(critic)** One festival week a month (days 15–21), not two, so the calendar is not always on | P1 | M | B |
| 15 | R2-15 | 반딧불 등롱 | **(critic)** Now an extension of SPEC4 NEW-03: the kill-gauge lantern with 1-of-4 notes and the night moth. The firefly rules and cap stay SPEC4's. No 빠른 수선권 note | P1 | S | B |

Next in line (P1 · S): R2-05b 스킬 각성, R2-17 단골 길, R2-19 재료 엮기, R2-20 대표 친구 + 수선점 이름, R2-14 보름달 밤, R2-18 주말 골목 장터, R2-09 수선공 이름표.
**Removed or merged (critic):** R2-06 → SPEC4 NEW-01 · R2-18 free gift → NEW-02 · R2-28 → SPEC4 §13.2 cheer · R2-24 cut · R2-25, R2-30, R2-31, R2-34, R2-35, R2-22's league and errand 4 deferred (Appendix C).
**(v1.2)** The 51 unseen findings leave this table unchanged. Wave A gains four small deltas (≈+2 agent-days, §8): rule-table types in R2-0, the guest's weekly rumour in errands, missed-gift auto-claim plus a one-day gift bank in R2-07, and a costume fitting room in R2-36a. New P2 items: **R2-39 한발 먼저** (call the next wave early, S) and **R2-40 축음기 음반** (collectible BGM records, S), both wave C (§6.3).

---

## 1. Scope, rules and baselines

### 1.1 Input coverage — read this first
- The findings JSON for this step announced **121** systems but arrived **truncated after the 71st** (it stops inside '만난 날 기념일').
- This document merges those 71: kr-kiugi 38, global-idle 20, cozy 13. Items 72–121 were not visible here.
- §2.4 gives the triage rule for slotting them in later without reopening the design.
- **(v1.2)** The missing 51 arrived as `research2/unseen_findings.json`: the full text of #71 (C13, truncated in v1) and #72–#121 (cozy 6, tower-defense 20, code-gap audit 24). All are merged in v1.2 (§2.5 and the v1.2 merge log), so **all 121 findings are now covered**.

### 1.2 Hard constraints (all designs obey these)
1. **No backend.** Saves are local, the device clock is guarded by `DailyClock` (05:00 day, `EffectiveUnix = max(now, maxSeen)`, `IsTampered`). Real PvP, guilds, chat, server rankings, raids and plunder are out. Offline alternatives: seeded challenges, the player's own past formations and records, share codes with real friends. (critic: fictional NPC neighbours only as story flavour, never as ranked rivals; see D-18.)
2. **No real money, no ads.** Every "pass", "pack" or "premium track" is free and earned by play. ~~Ad buffs become free daily uses.~~ (critic) Ad buffs map to SPEC4's chosen free equivalents: the cheer, the daily gift and the fireflies (§13.10). R2 adds no new buff-uptime loop.
3. **Deterministic evolution stays.** Recipes, forms and the 진화 기억도 are untouched. Luck lives only in material and reward layers (IDLE_RESEARCH PRG-06 principle). Main-stage prestige stays deferred (§17). Resets happen only inside side modes.
4. **Tone.** Soft storybook, cute but slightly eerie. We mend, we don't destroy. Names use world words, and copy is short 해요체.
5. **Currency guardrail (critic: tightened).** **No new spendable currency in waves A–B.** 발자국 is dropped: 등불 길 talents unlock through depth first-clears. **실밥** stays reserved for wave D, and only if the user approves that wave. Everything else reuses 단추, 별조각, 별빛 사탕, the 9 materials, 기억편 / 선택 기억편, 빠른 수선권 and 두 배 수선권. Meters (바늘땀) are not spendable.
   - Stone Age Idle's launch reviews blame "growth content and currencies split too finely" for confusing beginners (`research2/kr_kiugi_notes.md`).
   - So waves A–B also add **at most 3 new permanent growth axes**: 수선 비법, 한 땀 돌파 (with its 추억 수놓기 tail) and 기념품 선반. 밤의 작업대 adds no combat stats.
6. **Append-only enums, safe save defaults.** This follows SPEC4 §0.2. R2 numbering starts after SPEC4's appends (§3.4).
7. **(critic) Never touch the 별조각 basket line.** R2 bonuses to "basket rate", basket cap hours, quick idle, night or moon must apply to the 단추 and material lines only.
   - The basket's 별조각 line (`MetaContent.IdleChest.cs:31` `Line(Gem(3))`) is the SPEC4 C-01 ceiling.
   - Quick idle pays every line, gems included (`IdleChestSystem.cs:44`). So every 빠른 수선권 is worth ≈9 별조각 and counts in §9.1.
   - SPEC4's `EconomyBudgetTests.BasketGemLine_IsBoundedAtStage1_20_50_100_200` must still pass with every R2 modifier at its maximum.
8. **(v1.2) The cozy sandwich** (from #77, the Project Horseshoe coziness report via Lost Garden and Game Developer). Threat stays outside; the rooms stay safe.
   - Enemies, timers that can fail, decay and "sad" states never appear in the 안채's decor and visitors, letters, records (R2-40), postcards or the codex story tab. Mended enemies may visit as friends (R2-29), never as threats.
   - Decor, visitors, letters, records and postcards never add combat stats. The workbench (R2-11) and the keepsake shelf (R2-12) are the two 안채 rooms that do, because they are tools, not decoration.
   - Cozy dailies bank or wait; nothing streaks: missed anniversaries trigger within 7 DayKeys (R2-23), guests return every 12 weeks (R2-03), the daily gift keeps one day (R2-07).
   - If players start min-maxing a cozy activity, retune it so the cozy behaviour stays the best one (the report's rule).
   - #77's own numbers are not adopted: its ≈300 별조각/week from cozy sources breaks the R2 guard (§1.5), its +19% basket bonuses break rule 7, and its 실밥 economy stays a wave-D question (R2-29).

### 1.3 Priority and size
- **P0**: the next wave after SPEC4. Biggest retention gain per cost: daily and weekly appointments, plus a growth runway past the current caps.
- **P1**: needed for a polished launch. Depth, rhythm, delight.
- **P2**: post-launch expansion. Mostly art-heavy cozy systems, or systems that need a P1 engine first.
- **Size** (one agent): S ≤1 day · M 2–4 days · L 5–10 days · XL >10 days. This matches IDLE_RESEARCH §1.

### 1.4 UI frame (new main) and entry budget
The new frame, from the IA drafts:
- Battle map full screen (y 130–1594) with a compact TopBar of 130.
- Top HUD band 130–356: StagePanel, RecPower, ×1/AUTO, Synergy, IdleChest.
- Four "quick" icon columns: L-top (12,360)–(124,552), R-top (956,430)–(1068,552), L-bottom (12,1214)–(124,1480), R-bottom (956,1214)–(1068,1480). That is about 8 icon cells in total.
- Bottom HUD band 1300–1594: GuideQuestBar, DeckTab, ReturnChip.
- Deck strip 156.
- Bottom nav: 4 buttons + the centre **MAIN (hunt)** button.
- Features open as **sheets** (top y 890 = 53.6% incl. tab bar; header 80 + content 780, existing tab panels unchanged), while the map pans up.

Entry types used below:
- **Map icon**: a cell in a quick column (88 visual / 112 hit).
- **Sheet**: a new sheet, or a segment inside an existing sheet.
- **In-screen**: inside an existing popup.
- **HUD chip**: a small element in the bottom HUD band.

**Entry budget for all of R2:**
- **2 permanent map icons**: '밤 나들이' (R-bottom) and '안채' (L-bottom, wave B).
- ~~1 temporary icon: 새싹 축제~~ **(critic)** None. The first-week sheet is SPEC4 NEW-01's '첫 주' journal tab.
- **1 HUD chip**: 반딧불 등롱 (it sits beside SPEC4 NEW-03's firefly band and must keep ≥8 px from CheerChip, SPEC4 §13.2).
- Everything else lives inside sheets or existing screens.
- While a side battle runs, the centre MAIN button reads '사냥으로' and returns to the main hunt (R2-00).

**(v1.2) Reconciled with `UX-SPEC-IA.md` v1** (the frame spec that replaced the IA drafts):
- Numbers that changed: TopBar 130; the sheet is **836** tall (header **56** + content 780), so its top is y 914 (not 890/80); the nav is 3+1+3 (강화 · 편성 · 친구들 · **사냥** · 원정 · 소환 · 상점); 설정, 공지 and 가방 live in the ≡ 메뉴 popup (IA §3.6).
- Rails (IA §3.3): at most 3 icons per side. L1 = 이벤트, L3 = 출석부, R1 = SPEC4 StatsButton (reserved), R3 = 수선 일지. **The only free cells are L2 and R2**, so R2's two permanent icons take exactly them: '안채' = **L2** (12–116 × 1222–1338), '밤 나들이' = **R2** (964–1068 × 1222–1338). Both rails are then full. **No later R2 item may add a rail icon**; every v1.2 addition lives in a sheet, a popup, the ≡ menu or Settings.
- The HUD chip of R2-15 (반딧불 등롱) can no longer sit "left, above the GuideQuestBar", because rail L3 occupies 12–116 × 1346–1462. It moves to the bottom band right of the guide bar (R2-15).
- **Hub sheets need a contract decision in R2-0.** IA v1 binds the sheet to the six `MainTab` panels (`UIFrame.SheetTab` = tab index or −1). R2-0 either adds a non-tab sheet (`UIFrame.SheetPanel` id + two ContentArea panels, nav selection −1, 사냥 and back close it like a tab sheet), or opens the two hubs as `BuildPopup 980×1500` (the battle is then covered). R2 prefers the sheet: the battle stays visible above it. The card layouts below (860 wide in a 780-tall area) fit either.

### 1.5 Economy baseline (from code and SPEC4)

| Item | Value (source) |
|---|---|
| Gacha | 100 / 900 per 10 별조각. Rates 45/33/15/6/1. Pity 60 → Legendary (`GachaContent.cs`). 7 lineages: 똑단이 C, 찰칵이 R, 몽당이 R, 짝짝이 E, 루즈리 E, 우두커니 L, 라라벨 M (`CharacterContent*.cs`) |
| Duplicates → 기억편 | 5/10/20/40/80 by grade. Resonance costs 20×(lv+1), max 10, so 1,100 per lineage. Substitution costs 5/10/20/40 per rare+ material (`GameConfig.cs`) |
| Expected shards per pull (all owned) | 똑단이 2.25 · 찰칵이 1.65 · 몽당이 1.65 · 짝짝이 1.5 · 루즈리 1.5 · 우두커니 2.4 · 라라벨 0.8 |
| 단추 (gold) | Shop level cost 10×1.12^(L−1), max 1000. Gold/s 1×1.1^(L−1)×(1+0.1×(highestStage−1)). Lineage level cost 50×1.09^(lv−1), **cap 100**. Shop levels pay 별조각 50 every 10 levels (`RewardMailSystem.cs:23`). Stage clear gold 60×1.13^(s−1) |
| Attack scaling | form × (1 + 8%×(lv−1)) × (1 + (resonance% + costume%)/100) (`CharacterSystem.cs:295`). Every attack term is capped. Enemy HP grows 1.11^s, then 1.07^s after stage 30 (`BattleConfig.asset`) |
| 별빛 사탕 | 3 per clear, +10 every 10th stage; basket 2/h during events. Event shop: 여행 가방 조각 20, 달빛 조각 60 (total 10), costumes 150/300 |
| Materials (9) | Common 수선 천, 리본 조각, 촛농 방울 · rare 기억의 실, 레이스 조각, 우산살 · epic 여행 가방 조각, 오르골 태엽 · legendary 달빛 조각. Shop rares 30 별조각 each, 3/day. A tier-2 evolution needs e.g. cloth 30 + ribbon 25 + suitcase 10 + moonlight 3 |
| 별조각 income | ≈1,825/week before phase 4 → ~~≈2,500/week after SPEC4~~ **(critic) ≈2,640/week after SPEC4 v1.1** (journal 1,000, basket ≈560 at the C-01 ×1.5 ceiling, night ≈20, attendance ≈325, affinity ≈265, season ≈110, events ≈60, daily gift 20, fireflies ≈28, trickle ≈250). That is a 60-pull pity (5,400) every ≈2.0 weeks (SPEC4 §11.2, critic revision). SPEC4 risk #3 already flags +45% |
| 선택 기억편 income (critic) | ≈25–30/week today (weekly mission chest 20, plus season and comeback spread over time). Value: 라라벨 shards drop at 0.8 per pull, so for a Mythic one 선택 기억편 is worth ≈112 별조각 of pulling (≈37 for 우두커니) |
| Roster (critic) | 7 lineages, 29 forms: 짝짝이 11, the other six 3 each (`Art/Characters`). 10 costumes, all 짝짝이 (`CharacterContent.Jjakjjak.cs:104–161`; `CharacterContent.Lineages.cs` has no `costumes:`). Each lineage has exactly 2 traits, and any two lineages share at most one (`CharacterContent.Lineages.cs:18,49,80,111,143,173`, `Jjakjjak.cs:18`). The battlefield has 1 field background, 7 enemies and 2 bosses (`Art/Battle`, `Art/Enemies`) |
| Missions | Daily 11 (170 pts), weekly 8 (140 pts), chests 20/40/60/80/100. Daily 100 = 별조각 70, weekly 100 = 선택 기억편 20 + 별조각 100 (`MetaContent.Missions.cs`) |
| Echo dungeon | 3 tickets/day (reset 05:00), spent only on clear. Gold 150×1.15^f. Thread from 3F, spring 5F, moonlight 12% at 10F (+1%/floor). Time limit 120 s |
| Expeditions | 8 zones, 10–180 min, stages 1–20 (`ExpeditionDatabase.cs`). SPEC4 IDLE-04 adds ×3/×6 tiers |
| Recommended power | ≈532×1.0741^(s−1) (`MetaContent.Power.cs`, envelope 1.264) |

**R2 budget rule (critic: tightened).**
- All R2 P0+P1 systems together may add at most **+250 별조각/week** at steady state (+9.5% on 2,640), and P2 at most **+80** more.
  - That gives the pity cadence v1 promised (≈1.9 weeks), measured against the corrected baseline.
  - v1's +400 on top of 2,640, plus the basket leaks, would have meant ≈1.6 weeks.
- 별조각 is the code's paid currency (`GameEnums.cs:7` "유료 재화"). Free income fixed now becomes the price anchor for any future shop, so R2 pays mostly in other things.
- **Shard budget (new):** R2 adds at most **+30 선택 기억편/week** and **+80 lineage 기억편/week**.
  - Lineage 기억편 go only to lineages that took part. Players never choose the lineage.
  - v1 added ≈90–150 선택 기억편 and ≈150–300 lineage 기억편/week (Critic revisions, CR2-06).
- **빠른 수선권 = 9 별조각** in every budget row, because quick idle pays the 별조각 line.

R2 rewards prefer:
- 단추 N분치 (`MetaReward.GoldMinutes`, which scales by itself);
- materials, and lineage 기억편 for lineages that took part;
- cosmetics: titles, frames and **costumes** (R2-36a).

The table is in §9.1. `EconomyBudgetTests` gets an R2 block with three asserts: 별조각/week, 선택 기억편/week and "R2 modifiers never change the 별조각 line".

---

## 2. Dedupe map (71 findings → R2 systems)

### 2.1 Result
- v1: 71 findings → **35 R2 systems** (7 P0, 13 P1, 15 P2) plus shared infrastructure **R2-00**.
- **(critic) v1.1:** 27 of the 35 remain. R2-06 and R2-28 are merged into SPEC4. R2-24 is cut. R2-25, R2-30, R2-31, R2-34 and R2-35 are deferred.
  - 3 systems the sweep missed are added: R2-36 roster and wardrobe, R2-37 chapter places, R2-38 storybook puzzles.
  - Total **30** (R2-05 and R2-36 counted once each): **7 P0** (R2-01 MVP, 03, 04, 05a, 07, 08, 36a) plus infrastructure R2-00; **13 P1** (02, 09–15, 17–20, 37), plus the P1 halves R2-05b and R2-36b; **10 P2** (16, 21, 22, 23, 26, 27, 29, 32, 33, 38).
  - Four more systems keep only the part SPEC4 lacks: R2-07 (NEW-04), R2-15 (NEW-03), R2-18 (NEW-02) and R2-08 (§13.10).
- **10 deferred** items in v1 (Appendix C). The critic adds D-11 to D-19.
- **7 findings dropped as already built or covered** (Appendix A): 5 fully built, plus gacha pity/rates (K37, milestones go to SPEC4) and the level-pass idea (K10).
- **13 findings partly covered by SPEC4** (K7, K8, K12, K30, K37, G3, G13, G17, G20, C1, C2, C10, C11). They keep only the gap (Appendix B).
  - **(critic)** Add K11/G19 (= NEW-01), K31 (= NEW-02), G9/K23 (= NEW-03), G11 (= NEW-04) and K22 (= §13.2 cheer). SPEC4 v1.1 §13 owns them.

### 2.2 Map
K = kr-kiugi sweep, G = global-idle, C = cozy. "→" gives the R2 system that absorbs the finding. **(v1.2)** C13 is now read in full (#71), and C14–C19, TD1–TD20 (tower-defense sweep) and GP1–GP24 (code-gap audit) are #72–#121; their rows are in the v1.2 merge log (summary in §2.5).

| # | Finding | → | Status today |
|---|---|---|---|
| K1 | Daily resource dungeons (일일 재화 던전) | R2-01 | partial (echo dungeon only) |
| K2 | Infinite tower (무한의 탑) | R2-02 | partial (echo ladder is echo-only and ticketed) |
| K3 | Weekly world boss | R2-03 | missing |
| K4 | Hard / Nightmare replays (깊은 밤) | D-01 deferred | – |
| K5 | 9-floor pick-a-buff tower (꿈길) | R2-21 | missing (PRG-07 is dungeon-only) |
| K6 | Draft mode with borrowed units | R2-10 borrowed friend + R2-21 | missing |
| K7 | Codex set bonuses / owned effect | SPEC4 Q-05 + R2-26 (set pages) | SPEC4 |
| K8 | Achievements | SPEC4 Q-04 + new lines (§3.7) | SPEC4 |
| K9 | Titles (칭호) | R2-09 | missing |
| K10 | Free growth / level passes | dropped: covered (Appendix A-7, SPEC4 PRG-04/PRG-06/Q-06) | covered |
| K11 | Newbie 14-day mission event | **SPEC4 NEW-01 '첫 주'** (critic: R2-06 removed) | SPEC4 v1.1 §13.6 |
| K12 | Rush / rotating growth events | R2-13 | missing (EVT-01 is in SPEC4) |
| K13 | Gold stat tree | R2-04 | missing |
| K14 | Promotion / awakening / cap breaks | R2-05 | partial (cap 100, resonance only) |
| K15 | Relics (유물) | R2-12 | missing |
| K16 | Free gear gacha + auto-dismantle | D-02 deferred | – (CR-11) |
| K17 | Pets | ~~R2-30~~ **deferred D-14 (critic)** | missing |
| K18 | Mounts → pad pedestals | R2-11 (골무 branch, per-direction nodes); ~~skins in R2-22 cosmetics~~ | missing |
| K19 | Mine + research | R2-11 (research); ~~R2-35 dig toy~~ **deferred D-16 (critic)** | missing |
| K20 | Random stat lines + crop timers | ~~R2-31 (pots)~~ **deferred D-15 (critic)**; wishing well → D-03 | missing |
| K21 | Kill-gauge treasure (여정의 보물) | R2-15 lantern (on top of SPEC4 NEW-03) | missing |
| K22 | Timed buffs (축복) | **SPEC4 §13.2 친구들의 응원** (critic: R2-28 removed) | SPEC4 v1.1 |
| K23 | Drifting tappable gift | **SPEC4 NEW-03** (R2-15 adds only the lantern) | SPEC4 v1.1 §13.8 |
| K24 | Daily mini-games | ~~R2-34~~ **deferred D-17; the roulette is cut (critic)** | missing |
| K25 | Leaderboards | R2-22 (personal boards + share card only) | missing |
| K26 | Arena → shadow sparring | ~~R2-22 (league)~~ league **deferred D-18 (critic)**; standalone arena → D-04 | missing |
| K27 | Home-base hub | R2-11 (안채 shell) + R2-29 (decor) | missing |
| K28 | Merge / combine (합성) | R2-19 | missing |
| K29 | Formation presets | R2-08 **맞춤 편성** (auto per mode). Manual presets wait for SPEC4 §13.10's trigger (>10 lineages) | missing |
| K30 | Stored keys + bulk sweep | **SPEC4 NEW-04** (dungeon bank 9) + R2-07 (errand / guest banking, rounds) + SPEC4 IDLE-05 SweepMany | SPEC4 v1.1 §13.9 |
| K31 | Daily free gift + rotating shop | **SPEC4 NEW-02** (free gift) + R2-18 (weekend market only) | SPEC4 v1.1 §13.7 |
| K32 | Re-acquire missed event rewards | R2-33 | missing (needed only once events end) |
| K33 | Daily/weekly missions + vouchers | – | **have** (A-1) |
| K34 | Attendance (day 7 unit, 28-day board) | – | **have** (A-2); day-28 keepsake once R2-12 ships |
| K35 | Idle cap + ×2 + quick idle | – | **have** (A-3) |
| K36 | Timed boss gate + repeat + auto | – | **have** (A-4) |
| K37 | Gacha pity, rates, milestones | SPEC4 PRG-04 (milestones) | pity/rates **have** (A-6) |
| K38 | Staggered unlocks | §3.5 new gates | **have** (A-5) |
| G1 | 밤 산책 roguelite | R2-10 **등불 길** (MVP) + R2-21 (full) | partial (fixed-seed headless sim exists) |
| G2 | 끝없는 밤 + 밤시장 리그 | R2-22 (endless config + personal boards); the NPC league is **deferred D-18 (critic)** | missing |
| G3 | 수선 축제 주간 | R2-13 | partial (counters + event shop exist) |
| G4 | 오늘의 큰 손님 (daily boss) | R2-03 | missing |
| G5 | 밤의 작업대 (long research) | R2-11 | missing |
| G6 | 유품 (per-lineage keepsakes) | R2-12 (lineage keepsake at affinity Lv6, no random roll) | missing |
| G7 | 손때 숙련 (form mastery) | D-05 deferred | partial (affinity unused today) |
| G8 | 달빛 화분 + 달 사탕 | ~~R2-31 (pots)~~ **deferred D-15 (critic; SPEC4 §13.10 Stone Age warning)**; 달 사탕 → D-06 | missing |
| G9 | 길 잃은 반딧불 (golden cookie) | **SPEC4 NEW-03** (critic); R2-15 adds the lantern | SPEC4 v1.1 §13.8 |
| G10 | 오늘의 순회 (daily bundle) | R2-07 | partial (separate claim-alls) |
| G11 | 쉬어도 괜찮아 (carry-over) | **SPEC4 NEW-04** for the dungeon; R2-07 for errands and the guest | SPEC4 v1.1 §13.9 |
| G12 | 분실물 상자 뒤지기 | R2-16 | missing (PRG-06 single roll in SPEC4) |
| G13 | Route stars + duration ladder | R2-17 (stars); ladder = SPEC4 IDLE-04 | partial |
| G14 | 주간 큰 잠꾸러기 (weekly meter) | R2-03 | missing |
| G15 | Seasonal album + season switcher | R2-33 | missing |
| G16 | 수선 시험 (rule-restricted replays) | R2-02 trial floors | missing |
| G17 | Hidden trophies + completion % + cape | R2-26 (extends Q-04/Q-05) | SPEC4 partly |
| G18 | 진열장 손님 (window visitors) | R2-29 | missing |
| G19 | 새싹 7일 축제 | **SPEC4 NEW-01 '첫 주'** (critic: R2-06 removed; same Capybara source and numbers) | SPEC4 v1.1 §13.6 |
| G20 | 계절 수선록 (side-mode seasons) | SPEC4 Q-06 XP sources + R2-21 charm themes; separate season → D-07 | SPEC4 partly |
| C1 | 마음의 실 (friendship levels, gifts) | SPEC4 CR-10 + R2-23 (gifts, letters, badges) | SPEC4 partly |
| C2 | 수선 손길 (care gestures, requests) | SPEC4 CR-10 tap-to-pet + R2-23 | SPEC4 partly |
| C3 | 수선점 안채 (decor, coziness) | R2-11 shell + R2-29 | missing |
| C4 | 밤손님 (visitors while away) | R2-29 | missing |
| C5 | 추억 사진첩 (photo moments) | ~~R2-25~~ **deferred D-13 (critic)** | missing |
| C6 | 원정 엽서·기념품 | souvenirs → R2-12; postcards, lunchbox, log → R2-32 | partial (zones exist) |
| C7 | 친구의 편지 + 수선 일기 | letters → R2-23 (reuses SPEC4 A.3 lines); ~~diary → R2-24~~ **cut (critic): its name clashes with 수선 일지** | partial (mail exists) |
| C8 | 이번 주의 손님 (weekly commission) | R2-03 | missing |
| C9 | 보름달 밤 + 계절 달력 | moon → R2-14; seasonal packs → R2-33 | partial (FullMoon modifier) |
| C10 | 분실물 대장 + 수선점 등급 | ledger = SPEC4 Q-05 CodexPopup (new tabs appended); rank → R2-26 | SPEC4 partly |
| C11 | 동화책 조각 | R2-27 (chapters 6+); ch1–5 stay as SPEC4 CR-05 | SPEC4 partly |
| C12 | 대표 친구 + 수선점 이름 | R2-20 | partial (random return line) |
| C13 | 만난 날 기념일 (truncated) | R2-23 | missing |
| **X1 (critic)** | Roster breadth. Cookie Run: Crumble launched with 69 cookies and 54 pets, Stone Age Idle with 67 pets; MapleStory Idle has 18 jobs (`research2/kr_kiugi_notes.md`) | **R2-36** (a: wardrobe P0, b: forms and lineages P1) | **missing.** 7 lineages, 29 forms, all 10 costumes on one lineage |
| **X2 (critic)** | Region and chapter variety (MapleStory Idle 47 chapters; Legend of Mushroom regions) | **R2-37** | **missing.** 1 field, 7 enemies, 2 bosses for an endless ladder |
| **X3 (critic)** | Hand-made one-time puzzle stages (AFK Journey Voyage of Wonders / Peaks of Time, `research2/global-idle-notes.md`) | **R2-38** (P2) | missing |
| **X4 (critic)** | Boss fought with the whole collection (Crumble dungeon, "ALL owned cookies", `kr_kiugi_notes.md`) | R2-03 '모두 함께' (+1% per owned form) | missing |

### 2.3 What changed versus the raw findings
- **Four boss/meter findings became one weekly loop (R2-03).** K3 (world boss), G4 (daily boss), G14 (weekly sleeper) and C8 (weekly guest) share one cause: a weekly guest covered by forgetting.
  - The fight targets the shadow, never the guest.
  - One guest illustration per week replaces 7 daily boss sprites.
- **Three tower-like modes became one tower (R2-02).** The rule-restricted trials (G16) became every-10th "trial floors". The hard tier (K4) is deferred, because our main ladder is already endless.
- **Five run-like modes share one engine (R2-10 → R2-21/R2-22).** These are the roguelite (G1), the 9-floor buff tower (K5), the draft (K6), endless waves + league (G2) and side seasons (G20).
- **Three relic-like findings became one shelf (R2-12):** relics (K15), lineage keepsakes (G6) and expedition souvenirs (C6). Every source is **deterministic** (Nth trip, milestone, affinity level) instead of a random roll.
- **Two research findings became one workbench (R2-11):** mine research (K19) and long-timer labs (G5). It costs 단추 and materials, so it needs no new currency. Pedestals (K18) and moon-candy tools (G8) became nodes in it.
- **Three attention findings became one visitor (R2-15):** kill-gauge treasure (K21), drifting gift (K23) and firefly (G9).

### 2.4 Triage rule for the unseen findings (72–121)
Map each by its core verb to an existing cluster, then keep only the gap:

| Core verb | Cluster |
|---|---|
| Daily ticketed mode | R2-01 |
| Climb | R2-02 |
| Weekly boss or meter | R2-03 |
| Permanent stat sink | R2-04, R2-05, R2-11 |
| Choice run | R2-10 / R2-21 |
| Competition | R2-22 (personal boards + share codes; the NPC league is deferred, D-18) |
| Relic / collection | R2-12, R2-26 (hidden records), R2-36 (roster/wardrobe) |
| Timer appointment | R2-11 (R2-31 deferred; expeditions already fill this role) |
| Cozy relationship | R2-23 |
| Decor / visitors | R2-29 |
| Calendar | R2-13, R2-14, R2-33 |
| QoL / bulk | R2-07, R2-08 |

Anything needing a server goes to §7 with an offline alternative. Anything that resets main progress goes to §17 / D-list. Only a truly new verb gets a new R2 ID (critic: R2-39+; R2-36 to R2-38 are taken).
- **(critic)** Before adding any item from 72–121, check it against SPEC4 v1.1 §13 (NEW-01 to NEW-04, the cheer) and the §13.10 table first. Four of v1's 35 systems turned out to be SPEC4 items.
- **(critic)** Any new reward source must also pass the three §9.1 guards (별조각, 선택 기억편, basket line).

### 2.5 (v1.2) The unseen findings (#71–#121), merged
- **IDs.** Cozy continues the C series: **C13–C19** = #71–#77. The tower-defense sweep (Bloons TD 6, Kingdom Rush, Arknights, Rush Royale, Lucky Defense, Random Dice, PvZ2) is **TD1–TD20** = #78–#97. The code-gap audit (genre standards checked against the code and SPEC4) is **GP1–GP24** = #98–#121.
- **Result (primary disposition, 51):**

| Disposition | Count | Findings |
|---|---|---|
| Already in SPEC4 | 9 | #98 Q-04 · #99 Q-05 · #100 CR-10 · #102 EVT-01 · #104 NEW-03 · #105 SAVE-01 · #106 Q-06/PRG-04 · #113 CR-05 · #117 NOTI-01 (5 of them also hand a small content gap to an R2 item) |
| Already in R2, gap merged | 25 | #71 · #72 · #73 · #74 · #75 · #77 · #78 · #79 · #80 · #81 · #84 · #85 · #89 · #90 · #95 · #97 · #101 · #103 · #107 · #108 · #110 · #112 · #114 · #116 · #118 |
| Already in R2, fully covered | 3 | #94 (R2-03) · #109 (R2-02) · #111 (R2-03) |
| **New R2 item** | 2 | #83 → **R2-39 한발 먼저** · #76 → **R2-40 축음기 음반** (§6.3) |
| Rejected | 6 | #82 · #87 · #88 · #91 · #120 · #121 (§7) |
| Deferred | 6 | #86 (D-05) · #92 (**D-20**, new) · #93 (D-01) · #96 (D-10) · #115 (D-16) · #119 (D-14) |

- **What the unseen set mostly was.** The code-gap audit re-derived SPEC4 from the genre (achievements, codex, affinity, events, notifications, save import, passes). The TD sweep proposed one rule engine and a dozen modes on top of it; most of them are R2 modes already (weekly boss = R2-03, endless = R2-22, roguelite = R2-10, draft = D-10), and v1.2 keeps the engine (R2-00 rule table) without the modes' gem tables (#78's own budget was **+685 별조각/week**).
- **Items that changed** (22, each tagged **(v1.2)** in its section): R2-00, R2-01, R2-02 (coverage note), R2-03, R2-05b (option), R2-07, R2-08 (one field), R2-09, R2-10, R2-11, R2-15, R2-19, R2-22, R2-23, R2-26, R2-27, R2-29, R2-32, R2-33, R2-36a, R2-37, R2-38; plus rule 8 (§1.2), the frame (§1.4), the hub (§3.2), contracts (§3.3–§3.7), §7, §8, budgets (§9) and Appendices B–C.
- **Triage notes that apply to later findings too:**
  - A trait-only roster rule ('천 친구만') can lock players out, the same flaw as CR2-09: each trait belongs to 1–3 lineages (비 1, 천 3, the other five 2 each). Generated rules use one only when the player owns ≥3 lineages with that trait.
  - Fictional neighbours stay story flavour (lenders, room owners, letter writers). They never post scores, thresholds or ranks (D-18 and the orchestrator's decision).
  - "Tap for a bonus" features never let AUTO tap (NEW-03 rule, R2-39), so CR-07's idle gold model stays valid.

---

## 3. R2-00 Shared infrastructure (build first, contract-first like SPEC4 P4-0)

### 3.1 Side-battle host (needed by R2-01/02/03/10)
- **What exists:** the echo dungeon runs a `BattleSimulator` headless (`DungeonSystem.Challenge`). The battle view renders only the main stage (`BattleSystem.Sim`, `BattleMode.Stage`).
- **Add** `BattleSystem.BeginSideBattle(SideBattleRequest req)`. It does the following:
  1. Parks the main stage (keeps `highestStage` and the challenge pointer; the current stage restarts later). The basket keeps filling normally.
  2. Builds the side `BattleSetup` (mode, seed, rules).
  3. Runs it **live** in the same BattleView with a mode HUD: timer, score or tier, and the rule chips.
  4. The centre MAIN button reads '사냥으로'. Tapping it asks '나들이를 멈추고 돌아갈까요?' (critic: the hub word, valid in every mode) and either abandons the attempt (no ticket spent) or returns after the result.
  5. On end, raises `SideBattleEnded(SideBattleResult)` and resumes the main stage at its start.
- **Headless path:** `SideBattleRunner.RunHeadless(setup, maxSeconds)` serves sweeps, 오늘의 순회 and tower auto-retry. It reuses the DungeonSystem pattern (0.1 s chunks).
  - **(critic) Time-slice it.** `DungeonSystem.Challenge` runs the whole 120 s fight inside one call (`DungeonSystem.cs:144–170`, 0.5 s chunks). That is fine on a tap. It is not fine for automatic runs after a main-stage clear or inside 오늘의 순회.
  - So the runner advances at most ≈2 ms of sim per frame (or runs on resume behind the return screen) and never blocks a frame.
- **Speed:** side battles honour Speed2x and SPEC4's `PresentationScale` (hit-stop).
  - A '결과 바로 보기' button (after 5 s) finishes the fight.
  - **(critic)** It must fast-forward **the same `BattleSimulator` instance** with AUTO on, unrendered. It must never re-simulate from the start.
  - The sim is fixed-step (`BattleSimulator.StepSeconds = 1/60`, `:39`, `:703–716`), so a rerun is identical only when the inputs are identical. Taps, drags and lamp presses in the first 5 s would make a rerun differ.
- **(critic) One setup path.** Side setups go through the same `BuildSetup` modifiers as the main stage (codex `ExtraTeamModifiers`, cheer `TempAttackPercent`, techniques, keepsakes, costumes). Then power ratings and the rec-power colours mean the same thing in every mode.
- **Never during FTUE.** All side entries refuse while `Tutorial.IsActive`.
- **(critic) First-run explainer.** Each new mode (errands, guest, tower, 등불 길) shows a 3-card '이렇게 해요' strip on its first entry.
  - It reuses the FeatureUnlock card layout and is skippable.
  - The unlock card says *that* a mode opened, not how its rules work: layers, favoured traits, trial rules, charms.
- **(v1.2) One rule table for every side mode** (from #78 '밤의 규칙', after the BTD6 challenge rules, Arknights contract tags and Kingdom Rush iron rules). v1.1 scattered rule instances over four items: tower trial rules (R2-02), walk depth rules (R2-10), puzzle constraints (R2-38) and endless perks (R2-22). They become rows of one content table, so a new mode or floor is data, not code.
  - `SideRuleDef { string id; string name; string icon; int risk (1–3, shown only); string group (rows in one group exclude each other); SideRuleEffect effect; float value; }`, baked in `Editor/Content` like other content.
  - `BattleSetup.Rules` (applied in the shared `BuildSetup`): enemy HP / damage / speed scale, wave-size scale, an elite every N spawns, shop HP scale, resonance regen scale, echo cost delta, a closed-pad mask, lamp off, echo lifetime scale, one extra `StageModifierType`, and a roster predicate that `FormationSystem` checks. Enemy HP reuses SPEC4's `EnemyHpScale` spawn path (§13.3), so split children inherit it.
  - Starter rows (≈20, drafted from #78): 단단한 먼지 (enemy HP +25/50/100%), 날카로운 녹 (enemy damage +30/60/100%), 재촉하는 바람 (speed +10/20/30%), 몰려드는 밤 (wave size +25/50%), 정예 그림자 (every 5th enemy ×3 HP), 낡은 벽 (shop durability −25/50%), 잔향 가뭄 (resonance regen −30/60%), 비싼 부름 (echo cost +1/+2), 빈 방위 (1–2 pads closed by seed), 꺼진 등불 (no lamp), 짧은 꿈 (echo lifetime −40%), the v1.1 trial rules, and weather (one forced modifier, or two stacked). Practice-only support rows: 튼튼한 문 (durability +50%), 든든한 바늘 (attack +30%).
  - Trait-only roster rows follow §2.5: only when the player owns ≥3 lineages with that trait.
  - **R2-0 lands the types; the effects ship with the tower (wave B)**, the first mode that needs them. Wave A modes use gimmicks (`ErrandRule`), not rules. A summed-risk picker with a '오늘 밤은 조금 무서울지도 몰라요' warning belongs to the deferred contracts (D-20).
  - Rejected inside: #78's +685 별조각/week across its modes, and its hub in the 원정 tab (R2 modes live in the '밤 나들이' sheet, so the 원정 sheet's [원정][던전] segments and FTUE coach paths stay as they are).

### 3.2 Two hub sheets (entry budget §1.4)
- **'밤 나들이' sheet** (map icon R-bottom, signpost with a hanging lantern).
  - Header: '밤 나들이' + the [오늘의 순회 ▶] CTA (R2-07, when unlocked).
  - Content: a vertical list of mode cards 860×140:
    - 잔향이 던전: links to the existing page in the 원정 sheet and shows tickets 'n/9';
    - 밤 골목 심부름: ~~4~~ 2–3 mini tiles (critic: errands 1–2 in wave A, 3 in wave B; errand 4 deferred);
    - 분실물 보관소: '{n}층 · 다음 상자 {m}층';
    - 이번 주의 손님: guest name, tier, '오늘 2/3';
    - 등불 길 (critic rename of 밤 산책): '등불 2/3'.
  - Locked cards show the M39 unlock sentence. The 원정 sheet keeps its [원정][던전] segments unchanged (FTUE coach paths).
  - **(v1.2) Header line '이번 주 소문'** (R2-03): '이번 주 소문 · {특성}·{특성} 친구들이 힘을 내요', with the two trait chips. It shows from the guest's unlock on.
  - **(v1.2) One card for the wave C challenges: '골목 게시판'.** 꿈길 (R2-21), 끝없는 밤 (R2-22), 동화책 수수께끼 and 오늘 밤의 의뢰 (R2-38) share one card 860×140 ('골목 게시판 · 오늘 의뢰 완료 · 끝없는 밤 최고 {n}웨이브'). It opens a list page whose rows are the modes, each with its personal-best line (R2-22 boards). The hub therefore stays at six cards: dungeon, errands, tower, guest, 등불 길, 게시판.
- **'수선점 안채' sheet** (map icon L-bottom, a warm door). This is a cut-away storybook view of the back rooms; each room is a card:
  - 재봉틀 방 (R2-11);
  - 기념품 선반 (R2-12);
  - ~~창가 (R2-31)~~, ~~다락방 (R2-35)~~, ~~주머니 친구 집 (R2-30)~~: deferred by the critic (D-14 to D-16);
  - 꾸미기 (R2-29, wave D).
  Rooms appear one by one through FeatureGate, each with a one-line "next useful thing" hint.
  - **(critic)** The 안채 icon appears with the first room (Workbench or Keepsakes), so `FeatureId.Home` is derived and not stored.
  - **(v1.2)** The 안채 header's right side holds a 64 px gramophone button (R2-40 records). Wave D's room visits (R2-29 '이웃 수선점 구경') open from the same header.

### 3.3 Save schema v5
- `PlayerData.CurrentVersion = 5`. A v4 save must load, play and re-save (same test as SPEC4 §11.1).
- Every field has a safe default and a `Sanitize` guard.

| Class | New fields (default) |
|---|---|
| `MetaSave` | `ErrandSave errands`, `WeeklyGuestSave guest`, `RoundsSave rounds`, `TowerSave tower` (B), `TitleSave titles` (B), `ProfileSave profile` (B), `LanternSave lantern` (B), `WalkSave walk` (B), `WorkbenchSave workbench` (B), `KeepsakeSave keepsakes` (B), `MarketSave market` (B), `MergeSave merge` (B) (all `= new()`) |
| `LineageSave` | `int capBreaks` (0..5), `int embroidery` (critic: 추억 수놓기 count, 0..20) |
| new `TechniqueSave` (in `PlayerData`) | `Dictionary<string,int> levels` |
| `FormationSave` | **(critic)** `ParkedFormationSave parkedMain` (null = none): a snapshot of the main 5 directions + deck, taken when 맞춤 편성 swaps for a mode and restored on exit or on the next load. Without it, a killed app would leave the mode formation in the main hunt |
| `IdleChestSave` | `int bankedFreeQuick` (0..1) |
| `ExpeditionSave` | `Dictionary<string,int> zoneCompletions` (v4→v5: start at 0) |
| `EventInstanceSave` (SPEC4) | `int roundsClaimed`, `long roundPoints` (festival rounds, R2-13) |
| **(v1.2) wave A** `DailyGiftSave` (SPEC4 NEW-02) | + `string keptDayKey` (null = none; yesterday's unclaimed gift kept for one day, R2-07) |
| **(v1.2) wave A** `RoundsSave` | + `List<RewardSummary> missedSummary` (R2-07 놓친 선물: what the rollover auto-claimed; cleared when the Return popup shows it). `RewardSummary { MetaRewardType type; string id; long amount }` |
| **(v1.2) wave B** `MergeSave` | + `bool autoWeave` (false; R2-19 [모두 엮기] on basket claim) |
| **(v1.2) wave C** `MetaSave` | + `BoardSave boards` (R2-22: top 10 runs and ≤20 friend rows per mode), `EndlessSave endless` (R2-22: `int bestWave; int weekBest; string weekKey; int weekMilestones` bitmask), `RequestSave request` (R2-38b: `string dayKey; bool cleared; string weekKey; int weekClears`), `MusicSave music` (R2-40: `HashSet<string> owned; string battleTrack; string menuTrack`) |
| **(v1.2) wave C** `AffinitySave` (SPEC4 CR-10) | + `Dictionary<string,int> anniversaries` (bits 1/2/4 = 만난 지 100일 / 200일 / 1년), `string giftDayKey`, `int giftsToday` (R2-23) |
| **(v1.2) wave C** `TitleSave` | + `string lampTint`, `string trailTint` (R2-09 tints; null = default) |
| **(v1.2) wave D** `MetaSave` | + `VisitSave visits` (R2-29: `string dayKey; HashSet<string> codesToday; int stamps; HashSet<string> neighboursSeen`), `AccessorySave accessories` (R2-33: `HashSet<string> owned; Dictionary<string,string> byLineage`) |

**(v1.2)** Wave C and D fields join v5 as plain appends with safe defaults and `Sanitize` guards. They need no migration code, so `CurrentVersion` stays 5 unless a later wave needs one. No v1.2 item adds a `CurrencyType`.

**(critic) Removed because SPEC4 v1.1 already has the field.** Adding them again would collide by name or keep two sources of truth:
- `MetaSave.firefly` → SPEC4 `FireflyDaySave firefly`. The lantern state moves to `LanternSave`.
- `SproutSave sprout` → SPEC4 `StarterWeekSave starterWeek`.
- `ShopExtraSave.freeDayKey` → SPEC4 `DailyGiftSave.lastDayKey`. The rest becomes `MarketSave { string marketWeekKey; Dictionary<string,int> marketBought }`.
- `DungeonSave.bankedTickets / lastRefillDayKey` → SPEC4 `DungeonSave.ticketBank / bankDayKey` (NEW-04). R2 errands copy that pattern inside `ErrandProgress`.
- `FormationSave.presets / modePreset` → deferred with manual presets (SPEC4 §13.10).

### 3.4 Enum appends (after SPEC4's appends; explicit values)

| Enum | Members |
|---|---|
| `CounterKey` (SPEC4 ends at 37) | ErrandRun 38 · ErrandClear 39 · TowerFloorClear 40 · GuestAttempt 41 · GuestStitches 42 · WalkStart 43 · WalkClear 44 · FireflyTap 45 · LanternOpen 46 · TechniqueLevelUp 47 · CapBreak 48 · ResearchStart 49 · ResearchDone 50 · KeepsakeGet 51 · MaterialMerge 52 · BoxHuntOpen 53 · MaterialSpent 54 |
| `FeatureId` (SPEC4 ends at 26) | **(critic, renumbered; nothing has shipped yet)** Outings 27 · Errands 28 · WeeklyGuest 29 · DailyRounds 30 · Techniques 31 · LevelBreak 32 · Tower 33 · NightWalk 34 (등불 길) · Workbench 35 · Keepsakes 36 · DreamWalk 37 (P2). Dropped: SproutCarnival (NEW-01 is gated by Missions), Presets (맞춤 편성 rides on Outings), Firefly (NEW-03 is gated by `ftue.done`), Home (derived), League (deferred) |
| `MetaRewardType` (SPEC4 ends at 8) | Title 9 · Keepsake 10 · WalkLantern 11. Costumes use the existing `MetaReward.Costume` (SPEC4 §8.4) |
| `MetaConditionType` (SPEC4 ends at 24) | MaxLineageLevel 25 · TowerFloor 26 · GuestTier 27 · ErrandLevel 28 |
| `MetaPopupKind` (SPEC4 ends at 9) | GuestReveal 10. (critic: BoxHunt dropped; the board opens inside its source's popup, R2-16) |
| `BattleMode` (code: Stage 0, EchoDungeon 1) | Errand 2 · Tower 3 · GuestShadow 4 · NightWalk 5 |
| `EnemyGimmick` (flags; ends at `LampDrain` 1<<8, `GameEnums.cs:262–289`) | Fleeing 1<<9 (never attacks, hops lanes) · Spawner 1<<10 (stationary nest) · Reforming 1<<11 (re-forms stronger) |
| `CurrencyType` | **(critic)** none in waves A–B. `Lint 3` (실밥) is reserved for wave D only if that wave is approved. Footprint is dropped |
| SPEC4 `EventKind` / `EventScheduleType` | FestivalRounds 4, MoonNight 5 / **(critic)** MoonPhase **4**, MonthRotation **5**, DateList **6** (P2, yearly lunar-holiday table). v1's MoonPhase 3 collided with SPEC4 v1.1's reserved `Dates = 3` (SPEC4 §2.2) |
| `CostumeUnlockType` (code ends at FormUnlocked 6; SPEC4 adds Season 7) | **(critic)** none needed. R2-36a uses Resonance / Stage / EventShop / Expeditions, plus `MetaReward.Costume` for mode rewards |
| SPEC4 `AchievementCategory` / `CodexCategory` | Errand, Tower, Guest, Walk, Technique, Keepsake / Keepsake 6, Guest 7 |
| **(v1.2)** `CounterKey` (after MaterialSpent 54) | CallEarly 55 (R2-39) · RequestClear 56 (R2-38b, lifetime daily-request clears) |
| **(v1.2)** `MetaRewardType` (after WalkLantern 11) | Track 12 (R2-40 record; itemId = track key) · Tint 13 (R2-09 lamp/trail tint, wave C) |
| **(v1.2)** `BattleMode` (reserved for wave C) | Endless 6 (R2-22) · Request 7 (R2-38 puzzles and daily requests share it) |
| **(v1.2)** new `SideRuleEffect` (R2-0, explicit values) | EnemyHpScale 0 · EnemyDamageScale 1 · EnemySpeedScale 2 · WaveSizeScale 3 · EliteEvery 4 · ShopHpScale 5 · ResonanceRegenScale 6 · EchoCostDelta 7 · ClosedPads 8 · LampOff 9 · EchoLifetimeScale 10 · ExtraModifier 11 · Roster 12 |

### 3.5 Unlock schedule, merged with SPEC4 (max 2 cards per moment; critic: max 4 new cards per chapter, SPEC4's included)

**(critic) Why the pacing changed.** v1 put a new feature on almost every stage of chapter 2. There were 8 cards in total there: SPEC4's 2-1 ×2, 2-3 and 2-5, plus R2's 2-2, 2-4, 2-6, 2-8 and 2-10. That is the "too finely split, beginners confused" pattern from the Stone Age Idle reviews. The revised table spreads R2 over chapters 2–4, and it opens growth sinks when the player actually needs them: gold piling up, the Lv100 cap.

| Moment | SPEC4 / existing cards | R2 additions (critic-revised) |
|---|---|---|
| FtueDone (after 1-5) | graduation card + attendance popup; SPEC4 NEW-03 fireflies start; NEW-01 '첫 주' tab is already open from 1-7 | – |
| 1-6 | Speed2x, LampPolicy (2) | **(v1.2)** R2-39 한발 먼저 rides on Speed2x (wave C): no card, one in-battle hint the first time its lantern lights |
| 1-8 | Shop (+ SPEC4 NEW-02 gift) | – |
| 1-10 | Dungeon, Evolution (2) | R2-19 재료 엮기 (wave B; NEW pill in the bag, no card) |
| 2-1 | AutoChallenge, ExpeditionBatch (2) | – |
| **2-2** | – | **Outings + Errands** (1 card: '밤 나들이 지도가 생겼어요') |
| 2-3 | Achievements | – |
| 2-5 | Costumes | – |
| **2-6** | – | **WeeklyGuest** (card) + 맞춤 편성 button on the guest page (no card) |
| 3-1 / install day 3 | Events | R2-13 festival weeks + R2-14 moon nights appear in the event hub (wave B, no card). **(v1.2)** 3-1 also opens 오늘 밤의 의뢰 (R2-38b, wave C) on the 골목 게시판 card: NEW pill, no card |
| **3-2** or ErrandClear ≥10, whichever is later | – | **DailyRounds** (card '이제 매일 할 일을 한 번에 끝낼 수 있어요'). It needs ≥3 daily modes, so it never opens before the guest |
| **3-4**, or any placed lineage Lv60 (whichever is first) | – | **Techniques** (card). It opens when gold starts to pile up, not at 2-8, where gold still goes to levels and shop |
| **3-8** | – | **Tower** (wave B, card) |
| Any lineage Lv100 | – | **LevelBreak** (card '한 땀 돌파로 레벨 한계를 넘을 수 있어요') |
| **4-1** | – | **등불 길** (wave B, card). **(v1.2)** 끝없는 밤 (R2-22, wave C) opens after the first finished walk, because it spends the same 등불: NEW pill on the 골목 게시판 card, no card |
| **4-5**, or the first keepsake earned | – | **Keepsakes + 안채** (wave B, card; burst of pending keepsakes) |
| **5-1** | – | **Workbench** room in the 안채 (wave B, card) |
| FtueDone + 1 day | – | R2-20 naming bubble on the profile chip (wave B, no card) |

No day-gates are added (§17). Per-errand stages (2-2 / 2-4 / 2-7 / 3-3) are data on the errand, the way expedition zones use `requiredStage`, so they need no FeatureIds.

**(v1.2)**
- R2-40 records need no gate: the Settings picker exists from the start with two default records, and each later record unlocks on its own milestone (toast + NEW pill, no card).
- **Card-count check (found while merging).** In code, chapter 2 already has four card-capable unlocks: AutoChallenge and ExpeditionBatch at 2-1, Achievements at 2-3 and Costumes at 2-5 (`MetaContent.Features.cs:119–138`; SPEC4 §2.8 turns the reserved ones on). With R2's Outings card (2-2) and WeeklyGuest card (2-6), chapter 2 shows **6** cards, above the critic's own "≤4 per chapter, SPEC4's included". Proposed fix, R2 side: the WeeklyGuest unlock at 2-6 becomes a NEW pill on the '밤 나들이' guest card plus the 2-2 card's second line '2-6에는 이번 주의 손님이 찾아와요'; the guest's own explainer strip (§3.1) teaches it on first entry. That gives 5. SPEC4 side, to suggest to its owner: one shared 2-1 card for AutoChallenge + ExpeditionBatch ('이제 보스 도전과 원정 보내기를 친구들이 알아서 해요'), which gives 4. Decide in R2-0.

### 3.6 Red dots (F-04 rules: only claimable, wasting or finished)
| Path | Rule |
|---|---|
| `map.outings` | any child below |
| `outings.errand.{id}` | tickets at cap (6), so more would be wasted |
| `outings.guest` | a weekly reward is unclaimed, **or** (critic) today's attempts would be lost at 05:00 because the bank is already full (4). v1's "unused after 18:00" was a nag dot, not a wasting one |
| `outings.walk` | lanterns at cap |
| `map.home` | research finished, a new keepsake, a slot free |
| ~~`map.sprout`~~ | (critic) removed. SPEC4's `top.journal.starter` covers 첫 주 |
| `hud.lantern` | the lantern is full |
| ~~`tab.shop.free`~~ | (critic) registered by SPEC4 `DailyGiftSystem` (NEW-02), not by R2 |

Never: "affordable" technique rows, tower floors, or rounds (M09 noise rule).
**(v1.2)** No new dot paths. The daily request (R2-38b) is optional and never claimable before it is played, so it gets a NEW pill on its first day only. New records (R2-40), tints and accessories get NEW pills. R2-39 has no UI outside the battle.

### 3.7 Hooks into SPEC4 systems (content appends, no new systems)
- **Q-02 missions** (rows stay hidden while locked, A4 rule):
  - daily '밤 골목 심부름 1번' (ErrandRun 1, 20 pts);
  - weekly '분실물 보관소 3층 오르기' (TowerFloorClear 3, 20);
  - weekly '손님의 그림자 5번 떼어 내기' (GuestAttempt 5, 20);
  - weekly '등불 길 3번' (WalkStart 3, 20; wave B).
  - The daily total becomes 190 and the weekly 200 when everything is unlocked. Chest thresholds stay, giving more slack.
- **Q-04 records**, new lines of 4 tiers each, ~~≈1,600~~ **(critic) ≤1,000 별조각 lifetime** (one-time; SPEC4's own records are 3,400):
  - `ach_errand` clears 10/50/200/500;
  - `ach_tower` floors 10/20/40/60. **(critic)** Floors must stay below the CR-07 wall (R2-02).
  - `ach_guest` weeks at 새것처럼+ 1/4/12/26;
  - `ach_walk` walks 10/50/200 + depth 10;
  - `ach_technique` total levels 100/500/1,000/2,000;
  - `ach_capbreak` 1/7/21/35;
  - `ach_keepsake` 5/15/25/31;
  - ~~`ach_firefly` 50/500/2,000/5,000~~ **(critic)** 30/100/300/600. SPEC4 caps catches at 6/day, so 5,000 would take ≈2.3 years.
  - The final tier of each pays a title (R2-09).
  - **(v1.2)** R2-26's 15 hidden records pay a title + 별조각 10 each (150). They count inside the same ≤1,000 lifetime line, so the eight lines above get ≤850 between them.
  - **(v1.2)** No mission rows for the daily request (R2-38b), the rumour (R2-03) or 끝없는 밤: #79 and #84 proposed them, but a mission turns an optional puzzle into a daily chore.
- **Q-05 codex:** tabs [기념품] and [손님] appended to `CodexPopup` (CodexCategory 6/7).
- **강해지기 sheet** (UX M13 phase 2), new rows:
  - '수선 비법 {row} +{n}' (cheapest power per 단추);
  - '한 땀 돌파 가능';
  - on tower trial floors, the rule hint.
- **NOTI-01** kinds (append after SPEC4's): ResearchDone, LanternsFull, ErrandTicketsFull. Same quiet hours.

### 3.8 Tests and guards
- `R2SaveMigrationTests` (v4→v5).
- `SideBattleHostTests`: park/resume keeps `highestStage` and challenge state; headless == live result for the same seed.
- `EconomyBudgetTests` R2 block (§9.1).
- Every claim checks `!DailyClock.IsTampered`. Every timer uses `EffectiveUnix`.

---

## 4. Wave A — P0 systems

### R2-01 밤 골목 심부름 (Night-alley errands) — P0 · L (critic: MVP M = errands 1–2 in wave A · errand 3 in wave B · errand 4 deferred, D-12)
- **From:** K1 — Legend of Slime dungeons (Gold Rush, Village Raid, Dwarven King, Boss Rush), MapleStory Idle growth dungeons (3 keys/day, store 10, failure free, bulk sweep), Legend of Mushroom Lamp Thief / Molten Ruins, 7K and Crumble daily dungeons.
- **Have / gap:** partial.
  - Have: 잔향이 던전 (echo-only, 3/day, ticket on success, per-floor sweep).
  - Gap: no full-team daily mode; no 단추-, material- or shard-specific dungeons; no ticket storage; no bulk sweep across modes.
- **Loop:**
  - Pick an errand → pick a level (any cleared level, or the next one) → **live** 60 s fixed-seed fight with the current formation + echo deck (R2-00 host) → reward by result.
  - A ticket is spent only on a qualifying result; failure is free.
  - Cleared levels can be swept (headless, ticket spent). '한 번에 다녀오기' (all errands at best level) lives in R2-07.
- **Errands:**

| # | Name | Unlock | Gimmick (reuses the sim) | Pays | Level-up rule |
|---|---|---|---|---|---|
| 1 | 그림자 까치 쫓기 (critic rename) | 2-2 | A **shadow magpie made of forgetting** with a button sack (`Fleeing`). We hit the shadow, never a living bird, which is the "we mend, we don't destroy" rule. When it is beaten it melts into a cream button sack. It hops between the 5 lanes every 3 s and never attacks. Only guardians whose fan holds it, and echoes that chase, can hit it. HP pool = 3× boss HP at s(L) | 단추 (20+2L)분치 × p, where p = HP fraction removed (min 0.3 once ≥10% is dealt) | p = 1.0 opens L+1 |
| 2 | 빨랫줄 걷기 | 2-4 | 5 laundry-basket nests (`Spawner`) at the spawn points, active one at a time clockwise G0→G4; each spawns 먼지뭉치 every 2 s. Nest HP = 8× a normal enemy at s(L) | Per clear: 수선 천 6+L, 리본 조각 6+L, 촛농 방울 4+⌊L/2⌋, 레이스 조각 1+⌊L/5⌋ (L≥5), 우산살 1+⌊L/8⌋ (L≥10). Partial = 20% per nest | all 5 nests down |
| 3 | 다시 뭉치는 먼지 왕 | 2-7 | A crowned 먼지 king (`Reforming`, +10% HP/ATK per re-form) for 60 s. Score = total damage, 10 tiers vs the level's par | Per run, tier t: 우산살 ⌈t/2⌉, 레이스 조각 ⌈t/2⌉, 기억의 실 ⌊t/4⌋; t = 10 and L ≥ 10 adds 오르골 태엽 1 | tier ≥7 opens L+1 |
| 4 | ~~큰그림자 행진 (wave B)~~ **deferred D-12 (critic)** | 3-3 | 10 chapter bosses in a row (boss_shadow / boss_mothqueen alternating with stage modifiers), 90 s | ~~기억편 of **one lineage chosen at entry**: 3 + ⌊L/10⌋ per clear~~. A chosen lineage could be 라라벨: 84 shards/week ≈ 105 pulls ≈ 9,450 별조각 of pulling, every week | all 10 down |

- **Scaling:**
  - Stage equivalent s(L) = requiredStage + 2(L−1), using the BattleConfig stage curves.
  - Each level shows the recommended power via `PowerSystem.GetRecommendedPower(s(L))` with the 여유 / 도전 / 어려움 colour.
- **Tickets:** 2 per DayKey per errand, stored up to **6** (refill = min(6, tickets + 2 × days elapsed)). Tampered → no refill.
- **Milestone chest** every 10 levels per errand: 별조각 30 + an errand-specific bundle (errand 1: 빠른 수선권 1; errand 2: 레이스 조각 10; errand 3: 기억의 실 3; ~~errand 4: 선택 기억편 5~~ deferred).
- **Economy:** ≈40 별조각/week early, ≈20 late. Errand 1 gives ≈45–240 min of 단추/day. Errand 3 gives ≤12 rares per run. Tuning knobs: `ErrandDef.rewardScale`, tickets/day.
  - **(critic)** v1's errand 4 (42–84 shards/week of a chosen lineage) is deferred. It turns the gacha's Mythic chase into a ticket chore and breaks the shard budget (§1.5).
  - Errand 1's milestone 빠른 수선권 counts as 9 별조각 each (§1.2 rule 7).
- **UI:**
  - The '밤 나들이' sheet → [밤 골목 심부름] page shows ~~4~~ 2–3 tiles 410×300 (critic): banner, '입장권 n/6', '최고 Lv.n', NEW pill.
  - The errand page has:
    - the level stepper ‹ Lv.L ›;
    - a rec-power line;
    - a reward preview (MetaRewardChip);
    - [다녀오기] Gold (live), [소탕] Secondary (cleared levels; stepper ×n once DailyRounds unlocks);
    - a result card with tier pips.
  - Copy (critic): '그림자 까치가 단추 자루를 물고 골목을 뛰어다녀요. 놓치기 전에 떨어뜨려 주세요!'
- **Art:**
  - New (max quality, ≈5 credits each): a shadow magpie with a button sack (battle sprite; indigo/lavender shadow body, cream sack, gold-button eyes, per the art memory palette); laundry-basket nest (intact/broken; fallback `UI/idle_basket_2`); dust-king crown overlay (fallback `enemy_dust` ×1.6 + tint).
  - **(critic)** All three double as chapter enemies in R2-37, so they are not one-mode art.
  - Banners reuse `UI/zone_exp_*`.
  - 3 images ≈ 15 credits (+4 optional banners ≈ 20).
- **Save:** `ErrandSave { Dictionary<string, ErrandProgress> byId }`, where `ErrandProgress { int highestLevel; int tickets; string lastRefillDayKey; int manualClears; HashSet<int> milestones }`.
- **Sim work:** the 3 `EnemyGimmick` flags (critic: Fleeing and Spawner in wave A, Reforming with errand 3 in wave B); `BattleSetup.ErrandRule` (nest order, re-form step); score capture.
- **(v1.2) One favoured style per errand** (from #110, Seven Knights Idle's resource dungeons: the gold dungeon gives ranged heroes +230%, so benched units get a job). Shown as a chip on the errand page and fed to 맞춤 편성 (R2-08 `FormationBias.favouredStyles`):
  - 그림자 까치 쫓기: 저격 damage ×2 (it flees between lanes, so reach matters);
  - 빨랫줄 걷기: 돌격 damage ×1.5 (nests stand still in groups);
  - 다시 뭉치는 먼지 왕: 지원 buffs and 힐 ×1.5 (a long fight against a growing boss).
  - The par of each level (s(L), tier thresholds) is tuned with the favour on, so rewards do not inflate.
  - #110's third errand (a 5-boss rush paying 선택 기억편) is v1.1's errand 4 and stays deferred (D-12).
- **Conflicts:** none. Rewards are side resources; evolution is untouched.
- **Why P0:**
  - It is the KR genre's core daily appointment (Maple's ≈7 sessions × 12 min/day is driven by daily dungeons; DoF calls key-gated mini-dungeons LoM's strongest comeback loop).
  - Each errand is a new gimmick on the same combat, so content cost is low.
  - It feeds the material bottleneck of the deterministic evolution crafts (commons, rares), and gold once R2-04 makes gold matter again. (critic: shards are removed together with errand 4.)
  - Today the only daily mode is echo-only.
  - **(critic) Why only 2 errands in wave A.** Together with the dungeon (3/day) and the guest (3/day), 4 errands × 2 tickets would mean 14 side fights a day. That is exactly the '방치 못하는 방치형' clutter this doc rejects for gear gacha. Errand 3 follows in wave B. Errand 4 waits for a shard-sink review.

### R2-02 분실물 보관소 (Lost & Found tower, with trial floors) — ~~P0~~ **P1 (critic)** · M · wave B first

> **(critic) Why P1.** "An always-on, ticket-free goal" is exactly what the endless main ladder already is: AutoChallenge, repeat farming, rec power (A-4, PRG-02). The tower's own value is its rule floors and its stage-modifier rotation, which is real but not P0. It also inherits the main-ladder wall (CR2-08), so its floor curve must come from CR-07. Three fixes below are marked (critic).
- **From:** K2 (7K Tower of Infinity, Crumble Implant Tower, Stone Age 천공의 탑, LoM floor ladder, Maple 10-stage milestones) + G16 (NGU challenges, Idle Champions variants).
- **Have / gap:** partial. Have: the echo dungeon ladder. Gap: no full-team climb, no ticket-free retries, no milestone chests, no floor rules, no permanent first-clear bonuses.
- **Loop:**
  - Climb numbered shelves of the night lost-and-found office with the full team.
  - Retries are unlimited and free.
  - Every floor pays once; every 10th floor is a "trial floor" with a roster rule and a small permanent reward.
- **Floors:**
  - s(f) = 6 + 1.5f. Floor 1 ≈ stage 7.5, so a 2-4 player clears a few floors at once (a burst of first clears).
  - Floor 50 ≈ stage 81. Floor 100 ≈ stage 156.
  - **(critic)** With today's axes the main ladder walls at roughly stage 70, and at ≈95–105 with R2-04/05 (CR2-08). Floors 50+ would then be unreachable walls, along with their titles and keepsakes.
  - Use s(f) = 6 + k·f, with k chosen by CR-07 so that floor 60 sits at the projected day-60 stage (k ≈ 0.75–1.0). Milestone rewards move to floors 20/40/60.
  - 45 s per normal floor: defeat the floor's wave with the shop alive.
  - Floors 5/15/25… are boss floors (60 s).
  - Floor names cycle the object families: '{f}층 · 우산 칸 / 장갑 칸 / 열쇠 칸 / 단추 칸 / 양말 칸'.
- **Rules:**
  - Each block of 5 floors carries one existing `StageModifierType`, cycling Rain → Blackout → Wind → DustStorm → FullMoon → Festival.
  - Every 10th floor is a **시험 칸** with one roster rule from this pool:
    - '지킴이 3명만' (2 directions forced empty);
    - '잔향이 없이';
    - '불 밝히기 금지';
    - '같은 역할 2명까지' (6 `CombatStyle`s, so 5 pads stay fillable);
    - '내구도 50%로 시작';
    - ~~'특성 2종까지'~~ **(critic) '잔향이 덱 3칸만'**. Every lineage has exactly 2 traits and no two lineages share both, so 2 traits allow exactly **one** lineage: one guardian on 5 pads. Even '특성 4종까지' allows only 3 lineages;
    - '비 오는 정전' (Rain + Blackout).
  - Rules, not trait-only rosters, because LineageLimit (1 guardian + 1 echo form per lineage) leaves some traits with only 1–2 lineages.
- **Rewards:**

| Floor | First clear |
|---|---|
| every floor | 단추 10분치 |
| boss floors (5, 15, 25 …) | 별조각 20 |
| trial floors (10, 20, 30 …) | chest: 별조각 50 + 선택 기억편 5 + 기억의 실 2 + 우산살 3, **plus '보관소 감사장' +0.5% team attack (permanent; critic: cap +5% at floor 100, because floor 200 is far past the CR2-08 wall)** |
| ~~50 / 100 / 150 …~~ **20 / 40 / 60 … (critic)** | title (R2-09) + a keepsake (R2-12; stored as pending until the shelf unlocks) |

  - Lifetime for the first 100 floors: 700 별조각. **(critic)** Steady ≈35/week, not 50: 7 별조각 per floor (boss 20 + trial 50 per 10 floors) × 3–6 floors/week. The trial chest's 선택 기억편 5 gives ≈2/week.
- **Auto-retry** (toggle '자동 재도전'): after each main-stage clear, if `TeamPower` rose ≥3% since the last failed attempt, the current floor is simulated headless once. A win → toast '보관소 {f}층을 정리했어요!' and the rewards.
  - **(critic)** The run uses the time-sliced runner (§3.1), at most one per 10 minutes, so a stage clear never hitches.
  - Trial floors are never auto-retried. Their rule is the point, and 맞춤 편성 has to apply it.
- **UI:**
  - '밤 나들이' → [분실물 보관소] page.
  - Shelf background with the current floor ±3 as rows 860×130: floor label, rule chips (modifier icon / rule icon), rec power + rating, reward chips.
  - [도전] Gold (live) · '자동 재도전' toggle · '다음 상자까지 {n}층' line.
- **Art:** new: shelf-office background (1 image ≈5 credits; fallback `UI/dungeon_banner`). Rule icons reuse modifier and `Icons/skill_*` art.
- **Save:** `TowerSave { int highestFloor; bool autoRetry; long lastFailPower; int trialBonusCount; HashSet<int> pendingKeepsakes }`.
- **Conflicts:** none. The trial bonus is small and capped, and the main stage is never reset.
- **(v1.2) Covered: #109 (AFK Arena King's Tower, AFK Journey Legend Trials).** The permanent climb and the restricted floors are this item. Its three weekday towers filtered by style (방어·포획 / 돌격·저격 / 지원·힐, 5 clears a day each, ≤10 floors ahead of the lowest) are not adopted: three more daily-capped climbs are the overscope CR2-12 cut, and a two-style filter can leave pads unfillable for a 7-lineage roster, the CR2-09 flaw. The style idea survives as the errand favours (R2-01). Trial rules move into the R2-00 rule table.
- **Why (v1: P0; critic: P1):** the cheapest endless content (updates just add floors); an always-on, ticket-free goal; a clean power check after every upgrade; and it gives stage modifiers and synergies a second stage.

### R2-03 이번 주의 손님 (This week's big guest) — P0 · M
- **From:**
  - K3: LoM Abyssal Portal, Maple World Boss, Crumble all-cookie boss, Slime Dwarven King.
  - G4: AFK Journey Dream Realm, Capybara Seal Battle.
  - G14: Pokémon Sleep weekly Snorlax.
  - C8: Pokémon Sleep, Animal Restaurant advance orders, Pocket Camp requests.
- **Have / gap:** missing. Only main chapter bosses (challenge/repeat) exist. Trait synergies (SynergySystem) and the weekly clock (`DailyClock.WeekChanged`) are ready to build on.
- **Story:**
  - Every Monday 05:00 a big forgotten object checks into the shop for a week of mending. It is wrapped in a **망각의 껍질** (forgetting shell).
  - We never fight the guest; we peel the shadow off it.
  - Sunday night's card shows it mended.
- **Daily fight '그림자 떼어 내기'** (live, 90 s, at the shop door):
  - The shell has **5 layers**, and each layer adds one existing behaviour:
    1. plain;
    2. calls 좀나방 ×6 (`FabricEater`);
    3. `LampDrain` aura;
    4. splits on peel (`Splitter`);
    5. `Enrage`.
  - A new shell (layers ×1.5 HP) follows. **Tier = layers peeled (1–10)**.
  - Layer HP = 2 × boss HP at stage max(1, highestStage − 5), so tiers scale with each player.
    - **(critic)** That stage is **snapshotted at the week's first attempt** (`WeeklyGuestSave.hpStage`). If it were read live, every stage the player cleared during the week would make the guest harder. The week would then never show growth, which is the opposite of a power check.
  - The guest's **2 favoured traits** give +30% damage to units with either trait. This is the weekly rotating weakness, and it gives the formation screen a weekly job.
    - **(critic)** Rain has one lineage, 우두커니 (Legendary). Weeks with a 비 trait therefore lean on the other trait, so the guest table keeps every 비 guest paired with a 2-lineage trait (already true).
    - The page's [맞춤 편성] button (R2-08) builds the trait-weighted formation in one tap.
  - **(critic) '모두 함께' (from Crumble's all-cookie boss).** Every *owned* form adds +1% damage in this fight (cap +30%), shown as a chip '모두 함께 +{n}%'. It ties the weekly fight to collection breadth (evolution, gacha) and needs no new UI.
  - **(critic) The guest is on the field.** Its portrait stands at the shop door wrapped in the shell (tinted `boss_shadow` overlay). Each peeled layer fades one overlay ring with the existing shield-break FX (ATK-11).
    - v1 left the guest's field silhouette for P2. But "we peel the shadow off the guest" is the whole fantasy, and a tinted boss_shadow alone looks the same every week.
    - It costs no new art beyond the 4 portraits.
  - The shop can fall: the attempt ends, and peeled layers still count.
  - **3 attempts per DayKey.** The best of the day counts. A fully unused day banks +1 (max 4; R2-07).
- **Stitches (바늘땀) into the weekly meter:**

| Source | Stitches |
|---|---|
| best attempt of the day | 4 × tier (max 40) |
| stage clear with ≥1 favoured-trait guardian / ≥3 | +1 / +2 (cap 60 per day) |
| expedition claim | +3 (cap 24 per day) |
| basket claim | +2 per hour away (cap 24 per claim) |

- **Weekly tiers** (Snorlax-style, 25):
  - 실금 1–5 = 15/35/60/90/125;
  - 반짝 1–5 = 165/210/260/315/375;
  - 새것처럼 1–5 = 440/510/585/665/750;
  - 추억 가득 1–10 = 840/930/1,020/1,110/1,200/1,290/1,380/1,470/1,540/1,600.
  - An engaged player (≈120/day) reaches 추억 가득 1–2. Idle-only play (≈60/day) reaches 반짝 4 – 새것처럼 1.
- **Reward = final tier only** (granted in the reveal; unclaimed → permanent mail):

| Band | 별조각 (critic-trimmed; v1 in brackets) | Other (critic-trimmed) |
|---|---|---|
| 실금 1–5 | 10–20 (15–35) | 단추 60분치 + 수선 천 20 |
| 반짝 1–5 | 30–50 (50–90) | 선택 기억편 3–5 (5–10) + 레이스 조각 5 |
| 새것처럼 1–5 | 60–80 (100–140) | 선택 기억편 8–10 (12–20) + the guest's **memento** (keepsake, R2-12) |
| 추억 가득 1–10 | 90–110 (160–200) | 선택 기억편 12–15 (25–30) + memento + 달빛 조각 1. **(critic)** The first 추억 가득 ever also grants an R2-36a costume (later: the guest's decor piece, R2-29) |

  - Typical ~~≈140~~ **≈90 별조각/week (critic)** and ≈10 선택 기억편/week. That is still the largest single R2 line; v1's band table alone used 35% of the old +400 budget.
- **Guests (12; cycle = weeks since anchor Monday 2026-11-02, mod 12; each returns every 12 weeks, so nothing is lost for good):**
  - 낡은 옷장 할아버지 [천·밤]
  - 멈춘 괘종시계 [금속·소리]
  - 금 간 거울 [빛·치장]
  - 빈 새장 [밤·소리]
  - 구멍 난 곰인형 [천·치장]
  - 녹슨 자전거 종 [금속·비]
  - 바랜 지도 [밤·비]
  - 깨진 램프갓 [빛·천]
  - 찢어진 연 [비·치장]
  - 고장 난 회중시계 [금속·밤]
  - 낡은 피아노 의자 [소리·천]
  - 잃어버린 트리 장식 [빛·치장]
  - Each trait appears 3–4 times.
- **Reveal** (`MetaPopupKind.GuestReveal`, startup kind after Return; the next Monday's first session):
  - a before/after card (guest portrait + stitched patch overlay);
  - a thank-you line ('고마워요. 이제 다시 시간을 셀 수 있어요.' — 괘종시계);
  - tier, rewards → fly.
- **UI:**
  - '밤 나들이' card '이번 주의 손님 · {이름} · {등급} · 오늘 {k}/3'.
  - The guest page shows: portrait, favoured-trait chips, the meter bar with tier pips, a stitches breakdown, [그림자 떼어 내기] Gold, a ~~'편성 메모' quick-swap~~ **[맞춤 편성]** button (R2-08, critic).
  - ~~P2 extra: the guest's faint silhouette in the plaza background art.~~ (critic: replaced by the P0 guest-at-the-door view above.)
- **Art:**
  - The guest portraits are new (1024², max quality): **4 at launch (≈20 credits)**, the rest later (≈40). With 4 portraits the first month cycles 4 guests; the full 12 cycle switches on when all exist.
  - Shadow boss = `boss_shadow` tinted to the guest's palette (0 credits). (critic) The field view also places the guest portrait at the shop door under the shell overlay (0 extra credits).
- **Save:** `WeeklyGuestSave { string weekKey; int guestIndex; int hpStage (critic: snapshot at the week's first attempt, 0 = not yet); long stitches; int stageStitchesToday, expStitchesToday; string stitchDayKey; int bestTierToday; int attemptsLeft; int bankedAttempts; string attemptDayKey; HashSet<string> rewardedWeeks }`.
- **(critic) SPEC4 §13.10 alignment.** SPEC4 parks this system ('주간 큰 잠꾸러기') until "CR-09 shows the median player at chapter 5+". Before launch there are no players, so that trigger cannot fire. R2 overrides it on the standing order and meets SPEC4's two stated needs: a boss-only sim mode (R2-00 `BattleMode.GuestShadow`) and art (4 portraits).
- **Conflicts:**
  - No backend: the world boss's server totals and ranking become personal tiers. ~~An optional NPC comparison comes later (R2-22).~~ (critic) Instead the page shows a personal best line ('지난번 최고 {tier}단계'), with no NPC comparison (D-18).
  - Evolution: none.
- **(v1.2) 이번 주 소문 (from #84; Rush Royale's weekly faction buff, PvZ2 Penny's Pursuit's featured plant).** The guest's 2 favoured traits are also the week's rumour outside the guest fight:
  - +15% damage for units with either trait in errands (wave A), the tower's normal floors and 등불 길 fights (wave B). Never in the main stage (idle balance), on trial floors, or in R2-38 puzzles and requests (their rules are the point).
  - The '밤 나들이' header carries the line (§3.2). [맞춤 편성] on the errand and tower pages uses the same traits.
  - No featured lineage: #84's +50% for one lineage punishes players who don't own it, and the traits already spread the week across 2–4 lineages.
- **(v1.2) Covered here, nothing added:** #80 (weekly tiered boss: the layers are the tiers, and the personal-best line replaces leaderboards), #94 (a weekly cumulative pool: the stitch meter, the week HP snapshot and the shell that visibly peels at the door) and #111 (a daily score attack with threshold rewards: the final tier). Their NPC rankings (D-18) and gem tables (§9.1) are rejected. #80's three boss mechanics go to R2-37's boss pool.
- **Why P0:**
  - One loop gives a 2-minute daily appointment *and* a weekly arc with a Monday reveal.
  - It never fails (damage always counts), so it is a weekly power check without frustration.
  - Its rotating weakness gives the formation and synergy systems a weekly reason to exist.
  - It is tone-perfect ("we mend") and art-light (one portrait per guest).

### R2-04 수선 비법 (Mending techniques, 단추 stat tree) — P0 · M
- **From:** K13 (Legend of Slime 10 gold stats with unlock chains; MapleStory Idle stat points + Maple grade).
- **Have / gap:** missing.
  - The 단추 sinks are 수선점 레벨 (HP + 단추/s) and 계보 레벨 (cap 100).
  - **Attack has no uncapped term** (`CharacterSystem.cs:295`: level-linear to Lv100, resonance and costume capped), while enemy HP grows exponentially per stage. This is the mid-game wall the Stone Age Idle review warns about.
- **Rows** (cost 단추 = base × 1.10^(lv−1); HoldRepeatButton with ×1 / ×10 / MAX):

| Row | Effect per level | Cap | Opens | Base cost |
|---|---|---|---|---|
| 바늘 끝 | 지킴이·잔향이 공격 +1% | – | with feature | 2,000 |
| 촘촘한 바느질 | 수선점 내구도 +1% | – | with feature | 2,000 |
| 손놀림 | 공격 속도 +0.5% | Lv100 | with feature | 5,000 |
| 매듭 확률 | 치명 확률 +0.1% | Lv300 | with feature | 8,000 |
| 심지 손질 | 불빛 충전 +0.5% | Lv100 | with feature | 8,000 |
| 단단한 매듭 | 치명 피해 +1% | – | 매듭 확률 Lv50 | 10,000 |
| 그림자 다림질 | 보스 피해 +1% | – | 바늘 끝 Lv100 | 20,000 |
| (B) 두 땀 뜨기 | 2연타 확률 +0.1% (new StatType + sim) | Lv250 | 손놀림 Lv100 | 50,000 |

- **수선공 등급:** every 25 total levels = 1 rank. Each rank gives ×1.02 attack and shop HP, **compounding** (rank 40 ≈ ×2.2). A badge shows next to the power score.
  - The compounding is what gives an exponential offense term that tracks income.
  - Final constants are tuned by extending SPEC4 CR-07 `EconomyFlowTests`: the need/rec ratio stays 0.80–1.05 through stage 150.
- **(critic) Math check: v1's constants cannot meet that target.**
  - Row costs grow ×1.10 per level and stage-clear gold grows ×1.13 per stage. So a row gains ≈ln 1.13 / ln 1.10 ≈ 1.28 levels per stage.
  - +1%/level is therefore *linear* in stage. The rank gives ≈0.2 ranks per stage × ln 1.02, which is ≈ ×1.004 per stage. All rows together add roughly 1.5–2% power per stage.
  - The needed power grows **7.4% per stage**: `MetaContent.Power.cs` rec 532 × 1.0741^(s−1), and calibration measured need 21,832 at 6-10.
  - The ceiling today is about 1,017 starter power × 8.92 (Lv100) × ≈2 (forms; ×1.35 per evolution tier, `FormStats.cs`) × 1.5 (resonance) ≈ 27–35k, which is reached at about **stage 65–70**.
  - R2-05 (Lv200, ×1.9) and R2-04 together push that to about **stage 95–105**. Past it, power grows ≈2% per stage while the need grows 7%. That is the hard wall of chapter ≈10.
  - **CR-07 must pick the late curve before any number here is fixed**, from one or more of:
    - a second enemy-growth taper (`BattleConfig.stageGrowthTaperStage` already tapers 1.11 → 1.07 at 30; e.g. 1.07 → 1.045 from stage 80);
    - a larger rank multiplier (≈×1.10–1.15 per rank) or per-row milestones (e.g. ×1.25 per 25 levels of 바늘 끝, AdCap style);
    - content that raises the ceiling on a schedule: new forms and lineages (R2-36b).
  - The pass criterion becomes "the need/rec ratio stays 0.80–1.05 through the stage a 60-day player reaches in the CR-07 projection", not a fixed stage 150.
- **(critic) Performance.** 손놀림 +50% attack speed and 두 땀 뜨기 (double hit, +25% at Lv250) multiply projectile and number events by up to ≈1.9×. That goes against the ATK-06 FX budget (particle cap 340; SPEC4 §11.3 p95 ≤ 8 ms).
  - 두 땀 뜨기 is a damage proc on the same projectile (one projectile, two numbers merged into one '×2' number), never a second projectile.
  - Re-run the SPEC4 perf capture with both rows maxed.
- **Integration:** a `TechniqueSystem` builds a team `StatBlock` (same path as Codex: `SynergySystem.ExtraTeamModifiers`). PowerSystem picks it up through the dirty flag. Counter TechniqueLevelUp also feeds `AnyUpgrade`.
- **UI:**
  - 강화 sheet: a new segment [수선 비법] beside the existing panels.
  - Rows 860×120: icon, name, Lv, effect 'now → next', cost chip, Hold button.
  - Locked rows show their opening condition.
  - The 수선공 등급 badge + progress '다음 등급까지 {n}레벨' sits at the top.
- **Art:** reuse `Icons/skill_*` and `Icons/star` (0 credits). Optional 7 new icons ≈35 credits.
- **Save:** `TechniqueSave.levels`.
- **Conflicts:** none. Evolution is untouched, and nothing resets. (critic) The R2-11 combat branch that duplicated these rows is removed.
- **Unlock (critic):** 3-4, or the first placed lineage at Lv60, whichever comes first (§3.5). Before that, 단추 still has two good uses: levels and shop. A third sink at 2-8 would only split it.
- **Why P0:** it is the genre's most basic "always something to buy" loop; it is the only infinite offense axis; it is cheap (data + one segment); and the 강해지기 sheet can point here when a wall hits.

### R2-05 한 땀 돌파 + 스킬 각성 (Cap breaks + resonance skill riders) — (critic, split) **R2-05a 돌파 P0 · S–M (wave A) · R2-05b 각성 P1 · M (wave B)**

> **(critic) Why split.** The riders are not "data-only on SkillDefs". At least six of the 21 need simulator work: Pull (똑단이), a stacking timed buff (우두커니 '기다림'), a per-cast counter (루즈리 '세 번째 시전마다'), a once-per-battle trigger below 30% shop HP (라라벨), a dissolve heal (몽당이) and a token-burst AoE (짝짝이). They also need VFX, text and tests. Breaks ship first. Riders ship with their own sim package.
- **From:** K14 — MapleStory weapon awakening (+20 max level per star, 5★ = Lv200), LoM evolution levels and seals, 7K Awakening, Crumble 용병단 level cap, Stone Age pet stars.
- **Have / gap:** partial.
  - Have: 계보 공명 Lv10 (+5%/lv, 20×(lv+1) 기억편) and `characterMaxLevel = 100`.
  - Gap: no cap breaks; no shard sink after resonance 10; no skill progression.
  - By the formulas in §1.5, the gold cost of lineage levels becomes trivial once shop income climbs, so the Lv100 wall arrives early (week 1–2 for placed lineages; confirm with CR-07).
- **Cap breaks (5):** cap 100 → 120 → 140 → 160 → 180 → 200.

| Break | 기억편 (own lineage) | Material | 단추 |
|---|---|---|---|
| 1 | 40 | 오르골 태엽 2 | 10 × next-level cost |
| 2 | 60 | 오르골 태엽 3 | same |
| 3 | 80 | 오르골 태엽 4 | same |
| 4 | 100 | 달빛 조각 1 | same |
| 5 | 120 | 달빛 조각 2 | same |

  - Allowed only at the current cap. Levels 101–200 keep 50×1.09^(lv−1) 단추 and +8%/level.
  - Each break stitches a ~~gold star~~ **gold cross-stitch mark (critic)** on the friend card ('한 땀'). Stars already mean grade: `GameEnums.cs:13` "캐릭터는 별 개수로 표시한다 (Common=1성 ~ Mythic=5성)". A 1★ friend with 5 gold stars would read as a 5★.
  - **(critic) 추억 수놓기**, moved here from R2-26's '한 땀 등록', is the shard sink after the 5th break: 30 기억편 → +0.5% that lineage's attack, up to 20 times (+10%, 600 shards per lineage). It uses the same detail-popup button.
    - R2 raises shard income, and resonance (1,100) plus the breaks (400) end at 1,500 per lineage. Without a sink, duplicates would turn worthless again.
- **Skill riders (스킬 각성, R2-05b, wave B)** at 공명 Lv 3 / 6 / 9 (21 riders, no RNG; ~~data-only on SkillDefs~~ critic: at least 6 need new sim effect types, see the split note):
  - 짝짝이: 가족 부르기 양말 토큰 +1 / 토큰 지속 +2 s / 토큰이 풀릴 때 털실 폭발 (40% 공격, 범위).
  - 똑단이: 보호막 +20% / 기절 +0.2 s / 웨이브마다 수선점에 처음 닿는 적을 끌어당김 (Pull).
  - 찰칵이: 표식 +1 중첩 / 표식 대상 치명 +5% / 표식 보스가 받는 피해 +15%.
  - 몽당이: 촛농 웅덩이 +25% / 드러냄 반경 +20% / 사라질 때 수선점 3% 회복.
  - 우두커니: 비 보너스 +10%p / 비 아닌 날 '기다림' 10초마다 공격 +2% (최대 10중첩) / 옆 지킴이에게 보호막 10%.
  - 루즈리: 버프 +5%p / 버프 지속 +1.5 s / 세 번째 시전마다 표식.
  - 라라벨: 마지막 음 범위 +30% / 회복 +15% / ~~'일곱째 음'~~ **(critic) '여섯째 음의 메아리'** — once per battle, at shop <30%: heal 20% + stun all 1 s.
    - **(critic, tone)** "A broken music box is afraid to play its last note" is the IP's signature beat (art memory). Its payoff belongs to story: the CR-10 Lv10 letter and BGM '마지막 음' (R2-23), or a storybook page. A resonance-9 stat rider must not play it.
    - Other systems keep the "never the 7th" rule too: R2-23's music-box gesture, and R2-12's '빠진 일곱째 음' keepsake.
  - The content package verifies each rider against the real SkillDefs and the synergy vocabulary.
  - **(v1.2) Option for the R2-05b sim package (from #86; Rush Royale's switchable talents, Arknights modules).** At 공명 9 only, the rider is a choice of two switchable '버릇' rooted in the object's flaw, free to switch outside battle. The listed rider stays option A; option B is new, e.g. 우두커니 '옆 지킴이에게 보호막 10%' or '빗속의 영웅' (+15% more on Rain stages); 몽당이 '사라질 때 수선점 3% 회복' or '아껴 쓰는 불' (lifetime +25%). This adds only 7 riders (one per lineage at rank 9), not 21. #86's mastery XP, ranks and unit trials stay deferred (D-05).
- **UI:**
  - CharacterDetailPopup: at the cap, the level-up button becomes **[한 땀 돌파]** Gold with cost chips, then a stitch FX (reuse VFX).
  - 진화 기억도 lineage header shows stitch marks (critic: not stars).
  - FormInfoPopup lists the riders, with lock lines at 공명 3/6/9.
  - The 강해지기 sheet gets a row.
- **Art:** gold cross-stitch badge, not a star (1 image ≈5 credits).
- **Save:** `LineageSave.capBreaks`, `LineageSave.embroidery` (critic). Riders derive from resonance (no save).
- **Conflicts:** none. It is deterministic and raises no gacha odds.
- **Why P0 (R2-05a):** it removes the earliest hard wall, gives duplicates value after resonance 10 (400 more shards per lineage, then up to 600 through 추억 수놓기), and needs almost no art. (critic: the break costs and the 101–200 level curve are tuned by CR-07 together with R2-04, CR2-08.)

### R2-06 ~~새싹 7일 축제~~ → merged into SPEC4 NEW-01 '첫 주' (critic) — removed from R2
- **Why removed.** SPEC4 v1.1 §13.6 already ships the same system from the same sources: Capybara Go 7-Day Carnival, Slime Diary and MapleStory Idle's 14-day mission.
  - It has the same numbers: 4 tasks a day, 5 points (15 on day 7), 180 in total, chests at 30/60/90/120/150 and a 14-DayKey window.
  - It is a fourth journal tab (`StarterWeekSave`, `top.journal.starter`), with its tasks and chests in SPEC4 Appendix C.2.
  - SPEC4 named it '첫 주' on purpose, so it does not clash with '새싹 출석'. v1's '새싹 7일 축제' would bring that clash back.
  - v1 would also add a second system, a 14-day map icon, `SproutSave` and `FeatureId.SproutCarnival` for the same week.
- **Final prize rejected.** v1's 150-point prize was 친구 선택권 (≤Legendary).
  - The roster has one Legendary and one Mythic. SPEC4 §2.11 #4 notes that every ≤Epic lineage is a starter or the attendance friend. So in week 1 the ticket always means "take 우두커니 for free".
  - That removes one of the two gacha chase targets from every new player's first week.
  - SPEC4's chest (별조각 150 + 달빛 조각 2 + 선택 기억편 20) stays.
- **What R2 adds to NEW-01:** nothing in wave A. NEW-01's tasks are final (C.2), and its locked-feature rule (M39 pill, claimable later in the window) already copes with R2 modes unlocking mid-week.

### R2-07 입장권 모아 두기 + 오늘의 순회 (Banking + Daily rounds) — P0 · S (critic: the dungeon bank is SPEC4 NEW-04, so R2-07 is one S)
- **From:** K30 (Maple: keys store to 10, failure free, one bulk sweep; LoM 'Sweep Last'), G11 (Capybara energy inbox ×6 days, AFK Labyrinth skipped-cycle bonus), G10 (Capybara bulk actions keep the routine at 15–20 min; AFK free fast reward).
- **Have / gap:** partial. Have: ticket-on-success, per-floor sweep, SPEC4 `SweepMany` (IDLE-05), expedition ClaimAll and SPEC4 ResendAll, mission claim-all. **(critic)** SPEC4 v1.1 §13.9 NEW-04 banks dungeon tickets up to 9 (P2 stretch). Gap: errand and guest banking, and no single action covers the chores.
- **Banking:**

| Thing | Today | R2 |
|---|---|---|
| 잔향이 던전 | 3/day, lost at 05:00 | **(critic) SPEC4 NEW-04, unchanged:** `ticketBank` refills +3 per DayKey to 9, and the Wednesday cap is 11. R2 adds no field. If NEW-04 is cut as a stretch item, R2-07 ships it exactly as SPEC4 §13.9 specifies it |
| 심부름 | – | 2/day, store 6 (R2-01) |
| 손님 attempts | – | A fully unused day banks +1 (max 4) |
| 빠른 수선 (free use) | 1 free/day, lost | 1 unused free use banks for one day (max 2 free) |
| 등불 길 등불 | – | Overflow beyond cap 3 → mail, kept 6 days (R2-10) |
| **(v1.2)** 오늘의 작은 선물 (SPEC4 NEW-02) | 1 per DayKey, lost if unclaimed | Yesterday's unclaimed gift waits one day beside today's (max 2 claims on a DayKey; '어제 선물도 남아 있어요'). From #73 (Animal Restaurant, Usagi Shima: banked dailies remove streak anxiety). Save `DailyGiftSave.keptDayKey` |
| **(v1.2)** 수선 일지 rewards | completed but unclaimed daily/weekly missions and reached chests are **wiped** at the 05:00 rollover (`MissionSystem.cs:341–353` → `Reset`) | **놓친 선물** (from #103; Firestone mails unclaimed free rewards): before `Reset`, the rollover claims every completed mission and every reached chest of the old period, unless the clock is tampered. The next session shows one line in the Return popup, '어제 못 받은 수선 일지 선물을 챙겨 뒀어요', with the rewards (MetaRewards, because mail cannot carry wallet items, SPEC4 §13.6). `JournalComplete` and `JournalPoints` increment as for a manual claim, so Q-04, NEW-01 and Q-06 stay consistent |

  - Toast at the first open of a day when anything banked: '어제 못 쓴 입장권을 모아 뒀어요'.
  - (critic) Banking returns value to people who skip a day. It creates none for daily players, so its §9.1 line is 0.
  - **(v1.2)** The same holds for both new rows: the kept gift is bounded by NEW-02's ≤30/week, and 놓친 선물 only pays what the journal row (≈1,000/week in SPEC4 §11.2) already assumes. Rejected from #73: its random decor capsule (one more random-reveal mechanic after the critic trimmed v1's six, one more '분실물' name after CR2-15, and it needs wave D decor and 실밥) and its 보관함 열쇠.
- **오늘의 순회** (critic unlock: 3-2 or lifetime ErrandClear ≥10, whichever is later, §3.5):
  - One tap runs, in order:
    1. claim the basket (free ×2 if available);
    2. use the free quick idle;
    3. claim all expeditions and resend all (SPEC4 ResendAll, lastTier);
    4. sweep the echo dungeon with all tickets at the highest floor (`SweepMany`);
    5. sweep each errand at its best level;
    6. auto-attempt the guest shadow headless with 맞춤 편성 (if attempts are left; best tier recorded). **(critic) This step is off by default.** The guest's live 90 s fight is R2's one designed daily moment of play. An AUTO headless run scores lower than play with lamp timing and echo drags, and banking (+1 for a fully unused day) already protects players who skip;
    7. tower auto-retry once (wave B; never on trial floors);
    8. claim missions, attendance and mail.
  - **Never** spends 별조각 (paid quick idle excluded), pulls gacha or crafts.
  - Each step has its own toggle.
  - (critic) Headless steps use the time-sliced runner behind the summary sheet's progress bar (§3.1).
  - One summary sheet lists every step and the merged rewards (RewardFlyer once).
  - Target: chores ≤3 min/day, whole routine ≤15–20 min as modes grow.
- **UI:** CTA [오늘의 순회 ▶] in the '밤 나들이' header (Primary 300×88), plus a long-press on the 나들이 map icon. The settings gear opens the toggles.
- **Save:** `RoundsSave { HashSet<string> disabledSteps; string lastRunDayKey }`, plus the bank fields in §3.3. **(v1.2)** + `missedSummary` and `DailyGiftSave.keptDayKey` (§3.3). Tests: a completed unclaimed mission is granted once at rollover, never twice; a tampered clock grants nothing; the kept gift expires after one DayKey.
- **Conflicts:** none. Bank caps and the tamper guard prevent clock abuse.
- **Why P0:** without it, every new daily mode adds friction. It is automation-as-reward (IDLE-05), and the Capybara analysis ties day-30 retention to a bounded routine.

### R2-08 ~~편성 메모~~ → 맞춤 편성 (Mode-aware auto-formation) — P0 support · S (ships in wave A with the guest) (critic: redesigned)
- **From:** K29 (LoM skill presets, Maple presets, Slime trait-set save/load).
- **Have / gap:** missing. `FormationSystem` holds one formation; `AutoFormation()` exists.
- **(critic) Why not manual memos.** SPEC4 v1.1 §13.10 rejects presets for now: "7 lineages × 1 guardian + 1 echo form leave few combinations · revisit when the roster passes 10 lineages".
  - The real need in R2 is narrower. Each mode wants a known bias (the guest's 2 favoured traits, a trial floor's rule) and then wants the main formation back.
  - A one-tap auto-formation does that with no memo management.
- **Design:**
  - `FormationSystem.AutoFormation(FormationBias bias)`. `FormationBias { TraitType[] favoured; float traitWeight; SideRule rule; }`. It scores with the existing power preview plus the bias, and it obeys LineageLimit and the rule (e.g. '같은 역할 2명까지', '잔향이 덱 3칸만').
  - **[맞춤 편성]** sits on the guest page, the tower page (trial floors) and the errand page. On tap it snapshots the main formation into `FormationSave.parkedMain` and places the biased formation. It shows '이번 손님에게 맞춰 세웠어요'.
  - On leaving the mode, or on the next load if the app was killed, `parkedMain` is restored and cleared. The toast is '원래 편성으로 돌려놨어요'.
  - Members away on an expedition are skipped, with the toast '원정 중인 친구는 빼고 세웠어요'.
- **Save:** `FormationSave.parkedMain` (§3.3). **Unlock:** rides on Outings (no FeatureId).
- **Manual memos** (3 per mode) stay deferred until the roster passes 10 lineages (SPEC4 §13.10; R2-36b makes that likely around launch +4 months).
- **(v1.2)** `FormationBias` gains `CombatStyle[] favouredStyles` and `float styleWeight` for the errand style favours (R2-01). The week's rumour traits (R2-03) reuse `favoured`.

### R2-09 수선공 이름표 (Titles) — P1 · S (~~ships in wave A as a reward type~~ critic: wave B, with the tower)
- **From:** K9 (Stone Age festival title; LoM leaderboard exclusives).
- **Have / gap:** missing (FTUE graduation only sets a flag).
- **Design:**
  - ~30 titles at launch. Equip 1. It shows on the profile chip, the share card (R2-22) and the shop sign plate.
  - Sources:
    - the final tier of each Q-04 line (17 SPEC4 lines + 8 R2 lines);
    - 보관소 ~~50/100/150층~~ **20/40/60층 (critic)**;
    - 손님 추억 가득 ×4 '손님맞이 장인';
    - 등불 길 depth 10 '밤길의 친구';
    - ~~새싹 축제 '새싹 수선공'~~ (critic: if wanted, SPEC4 NEW-01's 150 chest grants '새싹 수선공'; that is a content row for P4-8);
    - festival themes (R2-13);
    - chapter 5 '밤 골목 지킴이'.
  - ~~Owned titles give +0.1% basket rate each (cap +3%)~~ **(critic) Titles are cosmetic only.** A basket bonus would touch the 별조각 line (rule 7), and +3% is too small to feel but still one more stacking modifier to test.
  - `MetaRewardType.Title`. This lets new modes pay status instead of 별조각.
  - **(critic) Wave B.** Titles ship with the first source that needs them (tower floor 20). The guest's 손님맞이 장인 needs 4 weeks anyway.
- **Art:** title plate 9-slice (1 image ≈5 credits). **Save:** `TitleSave { HashSet<string> owned; string equipped }`.
- **(v1.2) '수선점 주인' is title #1 (from #107).** The FTUE graduation card already prints the pill '칭호 · 수선점 주인' (`TutorialSceneBuilder.cs:420`), but no title exists behind it. It is granted at `ftue.done` and backfilled on the v4→v5 load for saves past FTUE, so the card's promise becomes true.
- **(v1.2) Placement in the new frame.** The compact TopBar has no free line under '수선점 Lv.{n}' (LevelText 24–316 × 10–50, PowerChip below it; UX-SPEC-IA §3.1), so #107's TopBar title is not adopted. The title shows on the shop's sign plate in battle (ShopView), the profile chip and the share card.
- **(v1.2) Tints, wave C (from #91; BTD6's trophy-store cosmetics).** Two palette-only cosmetic kinds join titles in this sheet: **등불 빛깔** (the lamp flame tint) and **잔향 꼬리** (the echo trail tint), 3 each at launch, 0 credits. They are headline rewards for wave C modes (끝없는 밤's 오색실 medal, 꿈길 9/9, the 365th daily request). `MetaRewardType.Tint 13`; equipping changes only colours inside the existing FX budget.
- **(v1.2) Rejected here:** the ownership bonus '+0.5% 단추 per 5 titles' (#107; critic: titles are cosmetic only), titles for NPC ranks (#107, #111; D-18), and #91's 밤길 도장 currency with its souvenir shop (rule 5: no new currency in waves A–B, and `CurrencyType 3` is reserved for 실밥; its 선택 기억편 ×10 for 120 stamps would break the shard guard).

### R2-36a 옷장 넓히기 (A costume for every lineage) — P0 · S (+≈30 credits) — new (critic, missing from v1)
- **From:** X1. Collectible idle games sell their world through outfits. Cookie Run: Crumble launched with 69 cookies and 54 pets. The art memory makes the IP "suitable for … stickers, plush toys and merchandise".
- **Have / gap:** missing for 6 of 7 lineages.
  - All 10 costumes belong to 짝짝이 (`CharacterContent.Jjakjjak.cs:104–161`). The other six lineages define no `costumes:` (`CharacterContent.Lineages.cs`).
  - Costumes already carry an attack bonus (`CharacterSystem.GetCostumeAttackBonusPercent`, `:295`), a codex entry (Q-05, 30 별조각 each) and an achievement line (`ach_costume`). So six lineages are missing a whole existing axis, not just cosmetics.
- **Design:** 6 costumes, one per lineage. Each has 짝짝이's emotion arc "flaw → comfort" and a 3–4 line storybook text (the `Costume(...)` pattern).
  - It uses only existing unlock types plus `MetaReward.Costume` (SPEC4 §8.4), so the code size is S:

| Lineage (object) | Costume (draft name) | Arc | Unlock (existing type) |
|---|---|---|---|
| 똑단이 (snap button) | 잠옷 단추 똑단이 | 떨어짐 → 다시 채움 | `Resonance(3)` |
| 찰칵이 (key) | 열쇠고리 여행자 찰칵이 | 잠김 → 열림 | `Expeditions(10)` |
| 몽당이 (candle stub) | 생일 초 몽당이 | 사라짐 → 축하 | `EventShop(150)` 별빛 사탕 |
| 우두커니 (umbrella) | 비 갠 날의 우비 우두커니 | 기다림 → 마중 | `MetaReward.Costume` from the first 추억 가득 guest week (R2-03) |
| 루즈리 (lipstick) | 무대 화장 루즈리 | 감춤 → 드러냄 | `Stage(40)` |
| 라라벨 (music box) | 자장가 라라벨 | 두려움 → 위로 (never the 7th note, R2-05 tone rule) | `EventShop(300)` 별빛 사탕 |

  - Wave B adds more `MetaReward.Costume` sources (tower floor 40, 등불 길 depth 5, a festival-theme final round). Each R2 mode then has one non-gem headline reward.
  - The two event-shop rows give 별빛 사탕 a sink again. 짝짝이's two event costumes are one-time buys, and R2-13/R2-14 add candy income.
- **Economy:** codex +6 × 30 = +180 별조각 lifetime (one-time row in §9.1). The attack bonus uses 짝짝이's per-grade values.
- **Art:** 6 costume battle sprites, `gpt_image_2_5` max, one per image. The lineage's base art and concept sheet go in as `image_references`, and the art memory's rules apply: object structure becomes the body, silhouette test, 1/1/1/1 palette. ≈30 credits; card variants are optional. **(v1.2)** 우두커니 and 라라벨 are referenced as they are today (no lineage restyle, orchestrator decision).
- **Save:** none (`unlockedCostumes` exists). **Tests:** the content validator accepts one costume per lineage; the codex total is 17; `ach_costume` thresholds are re-checked.
- **(v1.2) Fitting room (from #74; Cats & Soup's 2026 Fitting Room, Pocket Camp dress-up).** Today a costume row equips at once (`CostumeItemView` → `CostumeSystem.TryEquip`) and nothing previews. In the detail popup's costume list, a tap on any costume, owned or locked, previews it on the portrait and on the guardian pad if that lineage is placed: [입히기] (owned) / [되돌리기], and the unlock line for locked ones ('비 갠 날의 우비 · 손님 추억 가득으로 받아요'). Preview is transient, so no save. S, wave A, because six new costumes make the choice real.
- **(v1.2) Season costumes (from #106, MapleStory Idle / Firestone free passes; wave B).** SPEC4 Q-06 pays `jjak_starwalk` at tier 30 of season 1 and only a fallback (별조각 50 + 선택 기억편 30) afterwards, so every later season has no headline.
  - From season 2, tier 30 grants the next unowned **season costume** in a fixed order: 우두커니 → 몽당이 → 루즈리 → 찰칵이 → 똑단이 → 라라벨 (`CostumeUnlockType.Season`, granted through `MetaReward.Costume`).
  - A season the player does not finish moves that costume to the next season's tier 30, so nothing is lost for good. The fallback pays only once all six are owned.
  - Economy: it *removes* the fallback while costumes remain, about −12 별조각 and −7.5 선택 기억편 a week on SPEC4's season row, and adds the usual +30 codex 별조각 per costume (one-time).
  - Art: one costume per season, ≈5 credits, same pipeline as above. **The udu and lala lineages are not restyled** (orchestrator decision): their costumes use the current forms as `image_references`.
- **Why P0:** S-size content-in-code that runs end to end through existing systems (unlock, codex, records, event shop, attack bonus). It fixes the most visible collection gap: 짝짝이 holds 21 of the 39 collectibles. It also gives R2 modes rewards that are not 별조각.

---

## 5. Wave B — P1 systems

### R2-10 등불 길 (was '밤 산책'; lantern-road roguelite run) — P1 · L (MVP)
- **(critic) Rename.** '밤 산책' collided with three existing names: '별이 머문 밤 산책' (the event and event shop), '별빛 산책 수첩' (SPEC4 Q-06 season) and v1's '꿈길 산책'. The run is now **등불 길**: you walk from lamp to lamp, and its energy is already called 등불. R2-21 becomes '꿈길'.
- **(critic) SPEC4 §13.10 alignment.** SPEC4 waits for PRG-07 (dungeon blessings, a 4.1 *stretch*) "to test the pick feel first". If PRG-07 ships, this run reuses its `BlessingPickPopup`. If it is cut, R2-10's MVP builds that popup, and R2-10 grows to L+.
- **From:** G1 (Capybara Go runs + 1-of-3 skills + angel/demon pacts + energy inbox; AFK Arena / Journey Arcane Labyrinth crests; AFK Journey Honor Duel borrowed heroes; Idle Heroes Aspen Dungeon; Soda Dungeon 2), K6 (7K draft), Slime Legion.
- **Have / gap:** partial. Have: the seeded headless sim, and PRG-07 blessing vocabulary (SPEC4 B.4). Gap: no run mode, no charms or crests, no borrowed friends.
- **MVP walk** (one walk ≈8–10 min, AUTO allowed, choice nodes pause):
  - 15 steps:
    1. 전투
    2. 부적
    3. 전투
    4. 길가 상자 (critic rename; '분실물' is overloaded)
    5. 엘리트
    6. 부적
    7. 전투
    8. 가로등 쉼터
    9. 전투
    10. 엘리트
    11. 부적
    12. 그림자 상인
    13. 전투
    14. 쉼터
    15. 보스
  - 전투: a live 25–35 s seeded mini-wave via the R2-00 host. **Shop durability carries over** between fights.
  - 부적 고르기: 1 of 3 run-only charms.
  - 엘리트: golden box, 1 of 3 rare charms.
  - 가로등 쉼터: +30% durability.
  - 그림자 상인 (eerie pact): pay 20% durability for a dark charm, max 1 per walk ('그림자 상인이 손을 내밀어요. 조금 아플지도 몰라요.').
  - 길가 상자: materials.
  - 보스: a chapter boss with its gimmick.
- **Team:** the current formation + **1 빌린 친구**.
  - A seeded random form from all 29, including unowned 우두커니 / 라라벨 forms.
  - Level = PRG-05 catch-up formula.
  - It sits in an empty direction or as echo slot 7.
  - The first time an unowned form joins, the codex records the clue '??? — 등불 길에서 만나 봤어요' (no form is granted; evolution is untouched).
  - (critic) 짝짝이 has 11 of the 29 forms, so a uniform draw borrows a 짝짝이 form 38% of the time. Draw the **lineage** first (uniform over 7), then a form inside it.
  - **(v1.2) Neighbour lenders (from #89; Arknights support units, the co-op games' partners).** The borrowed friend arrives with a one-line note from one of three neighbour shops, picked by the seed: 우산가게 할머니 ('비 오는 날은 내게 맡겨요'), 시계방 쌍둥이 ('오늘 밤만 함께해요'), 빵집 고양이 꼬미. They are story flavour only: no scores, no ranks (D-18). When the form is unowned, the walk result card adds [{이름} 찾으러 가기 ›] to the 소환 tab (no purchase prompt).
  - **(v1.2) Rejected from #89:** a 6th '막아 주는 이웃' guardian post between two pads (`BattleGeometry` has 5 slots, and the pads, the IA keep-out validation and the frozen FTUE coach paths assume them) and a daily helper per mode (R2-10 keeps one borrowed friend per walk; R2-38 puzzles can fix a roster that includes one).
- **Charms:**
  - 40 charms: the 12 SPEC4 B.4 blessings + 28 new, written in StatType / SkillEffect vocabulary.
  - Each charm carries 1 trait tag (천/금속/빛/소리/밤/비/치장). Rarity common / rare / dark.
  - A duplicate merges into tier 2.
- **Crests (문장):** 2/4/6 charms of one trait unlock trait bonuses. Example: 천 2 잔향이 체력 +10% · 천 4 소환 비용 −1 · 천 6 수선점이 한 번 30%로 되살아남.
- **Energy:** 등불, 1 per walk, +1 per 8 h, cap 3. Overflow goes to mail for 6 days.
- **Rewards** (kept on failure). **(critic) Only the first 10 walks of each WeekKey pay rewards.** Later walks still count for depth first-clears and practice. The weekly chest needs exactly those 10, so it is reachable and nothing is lost.
  - step 5: 단추 30분치 + 수선 천 10;
  - step 10: ~~기억편 2 to each lineage in the walk~~ **(critic)** 리본 조각 10 + 촛농 방울 6;
  - step 15: ~~선택 기억편 5~~ **(critic) 기억편 1 to each owned lineage in the walk team (≤7)** + 기억의 실 2 + 우산살 3. ~~+ 발자국 10–20~~.
  - Weekly chest at 10 walks: 별조각 50 + 선택 기억편 10.
  - First clear of each **밤의 깊이** (depth 1–10, one trade-off rule each, e.g. '보스 체력 ×2', '불 밝히기 없음'): 별조각 50 (500 lifetime) **+ one 등불 나무 talent (critic)**.
    - **(v1.2)** Depth rules are R2-00 rule rows. One new row comes from #95 (BTD6 Odyssey's fixed crew, Rush Royale's energy): **'졸음 길'**, a friend that fought two fights in a row dozes through the next one, so a deep walk asks for the whole roster. #95's separate weekly escort mode and its 별조각/도장 rewards are not adopted: 등불 길 already carries durability across steps.
  - Steady ~~≈70~~ **≈50 별조각/week (critic)**: the weekly chest only; depth gems are one-time.
  - **(critic) Why.** With 3 lanterns a day (21 walks a week), v1 paid up to ≈115 선택 기억편 and ≈290 lineage 기억편 a week. That is about 4× all of today's selectable-shard income, and for a Mythic it is worth more than the week's whole 별조각 income (§1.5). v1's §9.1 listed "≈30 shards/week". The fixed version pays ≤10 선택 기억편 and ≤70 lineage 기억편 a week.
- ~~발자국 →~~ **등불 나무** (run-only talents, 8 nodes). **(critic)** Talents unlock **one per new depth first-clear (depths 1–8)**, in an order the player picks, so no currency is needed: +1 reroll · 4 choices · guaranteed rare first pick · +10% starting durability · lantern cap +1 · chests +20% · choose 1 of 2 borrowed friends · one crest threshold −1.
- **Seed:** hash(DayKey, walkIndex), deterministic.
- **UI:**
  - '밤 나들이' card → walk sheet showing the street strip (current step, next 2 lamps), a charm bar (icons + tier), crest pips by trait, [걷기] Gold.
  - During fights the battle view shows the run HUD.
- **Art:**
  - New: street-strip background, shadow merchant sprite (2 images ≈10 credits).
  - Node icons reuse SPEC4 A3 map nodes. Charms reuse `Icons/skill_*` (33) + trait icons. Optional 8 dark-charm icons ≈40 credits.
- **Save:** `WalkSave { RunState current (seed, step, durability, charms, crests, borrowedFormId, history); int lanterns; long lastLanternUnix; int highestDepth; HashSet<string> talents; int walksThisWeek; string weekKey }`. (critic: no `CurrencyType.Footprint`; `walksThisWeek` also enforces the 10 reward walks.)
- **Conflicts:**
  - §17: run-only power, nothing resets on the main stage, so this is the safe "prestige-like" outlet.
  - No backend: seeded and offline.
- **Why P1:** it is the decision-dense 8–12-minute session that day-30+ players lack (the Gold & Goblins "pendulum"). Random choices tell a new story every run without new art, and a borrowed unowned form advertises gacha targets without selling. It is L, so it follows the cheaper P0 loops.

### R2-11 밤의 작업대 + 수선점 안채 shell (Long research + home hub) — P1 · ~~L~~ **M–L (critic: trimmed)**
- **From:** G5 (The Tower labs, Egg Inc Epic Research, AFK Elder Tree), K19 (LoM Tech Park, Slime research), K27 (LoM Manor, 7K Pet Ranch, Stone Age facilities), K18 (Maple slot enhancement → per-direction nodes).
- **Have / gap:** missing. The shop has a level (GrowthSystem) and one static sprite (ShopView).
- **(critic) SPEC4 §13.10 alignment.** SPEC4 parks the research tree as "a new long-term sink and yet another timer next to the expedition ladder · revisit when materials pile up". R2's material sources (errands, the guest, 등불 길, merge) make the pile-up likely, so the trigger becomes a CR-07 projection: common materials owned at day 14 are more than 3× what the next evolution tier needs. The tree is cut to the parts no other system does.
- **Workbench:**
  - Research slots: 1 at unlock (critic: 5-1), 2nd at 7-1, 3rd at 9-1.
  - ~~4 branches × 8 nodes~~ **(critic) 3 branches, 19 nodes** (실 6 · 골무 8 · 바구니 5; 3–10 levels each):

| Branch | Nodes (per-level effect) |
|---|---|
| ~~바늘 (combat)~~ | **(critic) removed.** Its rows (attack, crit damage, boss damage, lamp charge) duplicate R2-04's 단추 rows with a second currency and a timer: the "growth split too finely" pattern. 시험 칸 보너스 stays on the tower, and 엘리트 피해 moves to an 등불 나무 talent if wanted |
| 실 (echo) | 소환 비용 −1 (1 lv) · 잔향 지속 +3% · 잔향 회복 +2% · 시작 잔향 +2 · 잔향이 체력 +2% · 귀환 잔향 +5%. ~~덱 7칸 해금~~ (critic: it changes the 156 px deck strip and the frozen FTUE coach paths, so it needs its own UX item). ~~산책 부적 3→4~~ (critic: duplicates the 등불 나무 '4 choices' talent) |
| 골무 (shop) | 내구도 +1% · 피해 감소 +0.5% · **방위 받침 G0–G4** (사거리 +2% per level for that direction; 5 nodes; this is K18's pedestal idea attached to the direction, so it survives formation swaps) · 수선 회복 +0.1%/s |
| 바구니 (economy) | 바구니 적립 +2% (×5) **on 단추 and material lines only** · 바구니 상한 +1 h (2 lv) **on 단추 and material lines only; it stacks on SPEC4's 12 h / 16 h steps (§13.4)** · 원정 재료 +5% (×4) · ~~빠른 수선 무료 +1/일~~ (critic: +2 h of every basket line a day, gems included, ≈+63 별조각/week off the books) · 심부름 입장권 보관 +2 (1 lv) · **작업대 속도 −2% (×10, −20%)** |

  - **Timers:** tier base T1 20 min / T2 1 h / T3 3 h / T4 8 h, ×1.5 per level, cap 72 h.
  - **(v1.2) Tier gating (from #116; Egg, Inc. common research).** A node tier opens after 10 levels bought in the tier below, across the 3 branches, so T3/T4 nodes become a visible goal ('{n}레벨 더 배우면 다음 칸이 열려요'). Rejected from #116: its combat branch (removed by the critic as a duplicate of R2-04), its 비법 쪽지 currency (fed by the deferred attic dig, D-16), its 'basket cap +30 min per level on every line' (rule 7) and its name '수선 비법서', which collides with R2-04 '수선 비법'.
  - **Costs:** 단추 (20 × level × tier)분치 (it scales with income, so it never trivialises), plus materials:
    - T1: 수선 천 15n;
    - T2: 리본 15n + 레이스 3n;
    - T3: 촛농 15n + 우산살 3n + 기억의 실 n;
    - T4: 오르골 태엽 n + 기억의 실 2n.
    - Research is the main **sink for surplus commons**.
  - **빠른 수선권** can instead skip 2 h of research (a second use for tickets).
  - NOTI-01 'ResearchDone' notifications.
  - Tampered clock → timers pause.
- **안채 shell:** the second map icon (L-bottom) → '수선점 안채' sheet (§3.2). The first rooms are 재봉틀 방 (this) and 기념품 선반 (R2-12).
- **UI:** the room view has the slot bar on top ('바느질 중: 바구니 적립 Lv.3 · 2:14:05', [빨리 끝내기]); below it, 3 branch columns (critic) of node cards (icon, Lv, effect, cost, time) and [시작] Gold.
- **Art:** new: a home cut-away background and a workbench-room background (2 images ≈10 credits). Branch icons reuse art.
- **Save:** `WorkbenchSave { Dictionary<string,int> nodeLevels; List<ResearchJob> jobs (nodeId, toLevel, startUnix, endUnix) }`.
- **Conflicts:** none. It never resets and never touches evolution. Equipment (CR-11) stays deferred: the 골무 / 바구니 nodes deliver the "slot upgrade" feel deterministically.
- **Why P1:** timers of hours to days create the "start before bed, collect in the morning" rhythm for day 7–180. It is a material sink, account-wide and offline-native. It is L, so wave B.

### R2-12 기념품 선반 (critic: '기억의' dropped; Keepsake shelf, relics) — P1 · L
- **(critic) Name.** '기억' is already in 기억편, 기억의 실, 진화 기억도 and SPEC4's 기억 조각 lines. The system is just **기념품 선반**.
- **(critic) SPEC4 §13.10 alignment.** SPEC4 parks relics because they "overlap the codex collection bonuses (Q-05) and add a currency · revisit after Q-05 is tuned". This version adds no currency: polish uses materials. It drops the **owned** effects, so Q-05 stays the only "owned bonus" layer, and it waits for Q-05's PowerCalibration re-run (SPEC4 §9.4 P4-1 risk).
- **From:** K15 (MapleStory relics: conditional effects, awakening stars, resonance slots 2/3/4; LoM relics; Slime goblin relics), G6 (TT2 artifacts, The Tower relics, Melvor ancient relics), C6 (souvenirs), C8 (mementos).
- **Have / gap:** missing.
- **Collection** (31 at launch content; art can phase in):
  - **7 lineage keepsakes**, each granted at **affinity Lv6** (adds a reward to SPEC4 CR-10). Examples: 짝짝이 '짝 잃은 단추 구멍', 우두커니 '마른 날의 우산끈', 라라벨 '빠진 일곱째 음'.
  - **8 expedition souvenirs**, one per zone: 10% per trip, **guaranteed on the 10th trip** to that zone.
  - **4 tower keepsakes** at floors ~~50/100/150/200~~ **20/40/60/80 (critic, R2-02)**.
  - **12 guest mementos** (R2-03 새것처럼+).
  - Event keepsakes later. Attendance day 28 gets one keepsake (Maple pattern).
  - Every source is deterministic or guaranteed by count. (critic: the souvenir's 10% per trip *is* a roll, but it has a hard guarantee by trip 10. The rule is "never a random roll on the effect or its strength".)
- **Effects:**
  - ~~**Owned effect** (always on, small): +0.5% to one of attack / HP / basket / expedition loot.~~ **(critic) Removed.** Q-05 owns always-on collection bonuses, and a basket owned effect would touch the 별조각 line (rule 7).
  - **Placed effect** (shelf slot, conditional, larger, applied at battle setup through StatType modifiers). Examples:
    - 할머니의 골무: 보스 피해 +8%;
    - 녹슨 자물쇠: 찰칵이가 있으면 치명 +5%;
    - 빗방울 유리병: 비 스테이지에서 우두커니 공격 +50%;
    - 반쪽 이어폰: 라라벨이 있으면 스킬 쿨 −8%;
    - 꺼진 성냥: 정전 스테이지에서 빛 특성 공격 +30%;
    - 여행 엽서: 원정 보상 +10%;
    - 멈춘 손목시계: 심부름 시간 +5 s.
  - Condition types: stage modifier, mode (stage / tower / guest / errand / walk), boss, real night hours 21–05, lineage present.
- **광택 (shine 0–5; critic: shown as shine dots, not stars):** each step adds +20% to the placed effect (step 5 = ×2). Stars already mean character grade, and 별 is in 별조각 / 별빛.
  - A step comes from a duplicate (repeat souvenir every further 20 trips; a guest memento on the guest's return) **or** a deterministic polish: 수선 천 20n + 레이스 조각 5n + 단추 (30n)분치.
- **Shelf slots:** 2 → 3 at 10 shine total → 4 at 25 → 5 at 45. ~~+1% HP per 5★ total~~ (critic: removed, another always-on bonus).
- **No rerolls**, which keeps it cozy and fair.
- **UI:** 안채 → [기념품 선반]: 5 shelf slots on top (drag or tap to place), a collection grid below (silhouette + hint for missing ones, '별빛 세탁소에 열 번 다녀오면 꼭 찾아요'), and a detail card with its placed effect (critic: owned effects removed) and [닦기]. Codex tab [기념품] (SPEC4 Q-05, category 6).
- **Art:** keepsake icons are new, 512², one per image at max ≈5 credits each: **16 at launch ≈80 credits**, all 31 ≈155. Shelf background 1 ≈5.
- **Save:** `KeepsakeSave { Dictionary<string,int> shine (critic: was stars); List<string> placed; Dictionary<string,int> zoneTrips; HashSet<string> pending }`.
- **Conflicts:** none. Luck is removed, and evolution is untouched.
- **Why P1:** build decisions per mode ("which keepsake for the rain floor"); one collection that every new mode feeds; a long tail that is still bounded. L and art-heavy, so wave B.

### R2-13 수선 축제 주간 (Festival weeks) — P1 · M
- **From:** K12 (LoM Pal / Mount / Prayer Rush laps, 7K relay, Maple Pink Bean stamp board) + G3 (Capybara 3-week growth-event rotation, The Tower event medals, Gold & Goblins short events, Idle Miner and AdCap event worlds).
- **Have / gap:** partial. Counters, 별빛 사탕 and the event shop exist, and SPEC4 EVT-01 builds the calendar and hub. Gap: no rotating focus weeks, no rounds.
- **Schedule:**
  - The month's first half belongs to SPEC4's '별이 머문 밤 산책' (days 1–14).
  - ~~The second half runs two festival weeks (days 15–21, 22–end).~~ **(critic) One festival week a month (days 15–21); days 22–end stay quiet.** The weekly guest still runs then.
    - With v1's two weeks, some event mission list would run on every day of the month. That sits on top of the weekly guest, the 28-day season, the weekend and Wednesday events, and nightly 밤의 수선점.
    - An always-on calendar stops feeling special, and it turns events into chores. Capybara Go, v1's own source, rotates growth events every 3 weeks.
  - Theme = rotation index (year×12 + month) mod 5 (`EventScheduleType.MonthRotation`, critic value 5).
- **Themes** (points from ProgressTracker; baseline at instance start, the SPEC4 model):
  - A '실 엮는 주': 진화 제작 100 · 공명 50 · 재료 사용 1/10개 (new counter MaterialSpent).
  - B '친구 찾는 주': 소환 1 per pull.
  - C '불빛 주간': 불 밝히기 1 · 보스 처치 3.
  - D '원정 주간': 원정 받기 5 · 보내기 2.
  - E '나들이 주간': 심부름 5 · 보관소 층 10 · 손님 도전 3.
- **Rounds:**
  - 4 rounds × 5 milestones. One round ≈ 25% of an engaged player's normal week for that theme, so all 4 finish by about day 5 (two days of slack).
  - Each milestone pays 별빛 사탕 10–20 instantly (RewardFlyer).
  - Each round pays 별빛 사탕 60 + 별조각 15 + a stamp.
  - All 4 rounds pay 선택 기억편 15 + the theme's title (first time only). (critic: wave B adds an R2-36a costume as the final round's prize the first time each theme runs.)
  - ~~≈30~~ **≈15 별조각/week (critic: one festival a month; 4 × 15 = 60 per festival)**, plus ≈4 선택 기억편/week.
- **UI:** inside SPEC4 EventHubPopup: a rounds strip (4 medallions + the current round bar) above the missions list. Start card via SPEC4 `EventStart`.
- **Art:** 5 theme banners (≈25 credits; fallback `shop_event_banner`).
- **Save:** SPEC4 `EventInstanceSave` + `roundsClaimed` / `roundPoints`.
- **Why P1:** it amplifies existing loops instead of adding new ones, sets a predictable live-ops rhythm, and runs on infrastructure SPEC4 is already building.

### R2-14 보름달 밤 (Real full-moon nights) — P1 · S
- **From:** C9 (Pokémon Sleep Good Sleep Day, Pocket Camp Complete real calendar), G15 (Cookie Clicker calendar seasons).
- **Have / gap:** partial. The FullMoon StageModifier exists (`fullMoonLampGainPercent 30`, `…EchoLifetimePercent 25`, `…NightBonusPercent 20`), and `CostumeSystem.IsNightNow` checks 20:00–05:59. Gap: nothing follows the real sky.
- **Moon math** (local, no server): age = (days since 2000-01-06 18:14 UTC) mod 29.530588853; full ≈ 14.77. **보름달 밤** = the DayKey containing the full moon ±1 (3 DayKeys, 05:00 boundaries, tamper-checked).
- **Effects** (SPEC4 hooks; `EventScheduleType.MoonPhase`):
  - The FullMoon modifier fills ≈50% of rotation slots.
  - 별빛 사탕 ×1.5 on stage clears (×2 on the full-moon DayKey) via `Battle.CandyMultiplier`.
    - **(critic)** Only on moon DayKeys **outside** the starlit walk (days 1–14). Inside it, `CandyMultiplier` stays SPEC4's ×2.
    - SPEC4 §13.5(i) says seasonal events are "never a second candy multiplier". A full moon falls in days 1–14 in about half of all months, and ×2 × ×2 would mean ×4.
  - Basket night bonus +10% (`IdleChest.NightBonusHours`) **on the 단추 and material lines only (critic, rule 7)**. SPEC4's 밤의 수선점 +20% already includes the 별조각 line and has its own budget row.
  - Later (when the systems exist): undiscovered photo moments and visitors ×2.
- **UI:** a moon-phase chip in the SPEC4 event hub's effects strip ('보름달까지 D-3'); the field already renders FullMoon ambience. Art: moon-phase chip (1 image ≈5 credits).
- **Why P1:** the real sky gives an offline game a monthly heartbeat, it is on-brand for a night-themed IP, and it costs only data plus one schedule type.

### R2-15 반딧불 등롱 (critic: the lantern + night moth on top of SPEC4 NEW-03) — P1 · S
- **From:** G9 (Cookie Clicker golden cookie, The Tower floating gems, Idle Slayer boxes, TT2 fairies), K21 (MapleStory Journey's Treasure), K23 (Slime flying boxes).
- **Have / gap:** ~~missing~~ **(critic) SPEC4 v1.1 §13.8 NEW-03 '길 잃은 반딧불' is the firefly.**
  - Its rules: `FireflyRules`; after `ftue.done`; every 360–720 s; 8 s life; ≤6 catches per DayKey.
  - Its rewards: 단추 10분치 w55 · material w35 · 별조각 10 w10. Its budget is ≤60 별조각/week.
  - So v1's firefly block below is **superseded**. Its numbers (13 s, 15 a day, a 5/30 별조각 table, the '친구들 응원' ×2 result, which also clashed with SPEC4's cheer name) are not built.
  - R2-15 adds only two things: the **kill-gauge lantern** and the **night moth** variant.
  - If NEW-03 is cut as a 4.1 stretch item, R2-15 ships NEW-03 exactly as SPEC4 §13.8 and C.4 specify it, then adds those two.
- ~~**Firefly:**~~ (v1 text kept for reference; superseded by SPEC4 NEW-03 except the night variant)
  - While the battle view is visible, sleep is off and FTUE is done, a firefly drifts across the field every 6–12 min (seeded per session) and stays 13 s.
  - It never crosses the boss HP bar and fades while a danger arrow is active.
  - Tap (88 px hit) → one of:

| Result | Chance |
|---|---|
| 단추 3분치 | 50% |
| 불빛 +40% | 20% |
| '친구들 응원' all attack ×2 for 30 s | 20% |
| 별조각 5 (once per DayKey; otherwise it becomes 단추) | 9% |
| 황금 반딧불: 별조각 30 + 기억편 5 (once per week) | 1% |

  - Cap 15 per day. ≤45 별조각/week, typical ≈25.
  - **Night variant** (local 21:00–05:00): 10% of spawns are a **그림자 나방**. Tapping it doubles the reward but 3 extra moths join the next wave (cute-eerie risk). (critic: kept. The doubled reward stays within NEW-03's 6/day cap, and a doubled 별조각 roll stays inside NEW-03's ≤60/week row.)
  - AUTO never taps it. Sleep mode keeps up to 3 '잠든 반딧불' at 50% to collect on waking.
- **Lantern (kill gauge):**
  - Kills fill a 72 px lantern chip. Full = 300 kills (+20 per chapter, cap 500), ≈8–10 min of active play. A tapped firefly adds 10%.
  - Offline: basket hours × the stage's average kills/min × 50%.
  - Stores up to 3 full lanterns. **(critic) At most 6 opens per DayKey.**
  - Open → pick **1 of 4 ~~분실물~~ 반딧불 쪽지** (critic rename: '분실물' already names the tower, the box hunt and the summon bundle).
    - The notes: 단추 N분치 · 재료 묶음 · 별빛 사탕 · ~~빠른 수선권 or~~ 기억편 1 (an owned lineage that was in the formation; **critic: offered at most once per DayKey**, so lantern shards stay ≤7/week; on other opens that slot is a second 재료 묶음).
    - **(critic)** No 빠른 수선권 note. Each ticket pays 2 h of every basket line (≈9 별조각), and with ~10 opens a day a ticket note could leak ≈60–90 별조각 a day.
  - The **3 unchosen notes gain 1 반짝 tier** (max 5; tier 5 = ×3). Skipping is never wasted.
- **UI:** the lantern chip sits in the bottom HUD band, left, above the GuideQuestBar, with a fill ring and a red dot only when full. The notes popup is 860×700 with 4 cards.
  - **(v1.2) New frame.** UX-SPEC-IA's rail L3 (출석부) now fills 12–116 × 1346–1462, so the chip moves right of the guide bar: **64×64 at screen x 500–564, y 1500–1564** (16 px from the bar, 12 px above the DeckTab at y 1576, below the battle envelope that ends at y 1292). MAIN only (`SheetFade 0`; the sheet covers this band anyway). Its CheerChip clearance rule no longer applies.
- **(v1.2) Firefly variants by stage modifier (from #72; Cats & Soup's drifters, Penguin Isle).** The SPEC4 NEW-03 firefly takes the stage's look: 떠다니는 종이배 on Rain, 바람에 날린 손수건 on Wind, 별똥별 on FullMoon, 비눗방울 on Festival, the firefly otherwise. SPEC4's rules, cap (6 a day) and reward table are unchanged; #72's own numbers (15 a day, a 별조각 table, 실밥) are not adopted. The first catch of each kind sets a flag for R2-26's hidden record '밤하늘의 손님들' (all five kinds). Art: 4 small sprites ≈20 credits, optional; fallback = the tinted firefly.
- **Art:** reuse `_firefly`; `enemy_moth` tint for the shadow moth; lantern chip from `Icons/lamp` (optional new chip ≈5). **Audio:** chime, moth hiss, lantern-full.
- **Save:** ~~`FireflySave`~~ **(critic)** `LanternSave { float lanternPoints; int storedLanterns; int[] noteTiers; string openDayKey; int opensToday; int sleeping }`. The firefly count stays in SPEC4 `FireflyDaySave firefly`. v1's `MetaSave.firefly` would have collided with it by name.
- **Why P1:** it rewards watching the idle screen, adds surprise at almost no cost, pays directly instead of through ads, and merges three findings into one HUD element.

### R2-16 먹보 상자 뒤지기 (critic rename; box hunt) — ~~P1~~ **P2 (critic) · S · wave C**

> **(critic) Why P2.**
> - **Popup stacking.** After a chapter boss the player already gets SPEC4's ChapterChest popup and then the StoryPage. A third between-stages popup (`MetaPopupKind.BoxHunt`) is too many.
> - **Fix when it ships:** the board opens *inside* the chapter-chest popup, as an optional '더 뒤져 볼까요?' step. It is never a separate queue entry.
> - **Luck must not buy permanent power.** v1 gave a perfect board +1% basket rate forever. A perfect board depends on luck: 4 mimics among 20 boxes, 1 needle. That breaks this doc's own rule that "luck lives only in reward layers", and the basket bonus would also touch the 별조각 line.
> - The perfect board now pays a title ('먹보도 못 막은 손') the first time, and nothing permanent.
- **From:** G12 (Idle Slayer Chest Hunt), AFK chests, Capybara roulette.
- **Have / gap:** missing. SPEC4 PRG-06 gives a guaranteed chapter chest (stretch).
- **Design:**
  - A **bonus board** after a guaranteed chest. It never replaces SPEC4's guaranteed contents.
  - 4×5 = 20 boxes, seeded by source id (reloading can't reroll):
    - 14 rewards;
    - 1 수선 바늘 (blocks one mimic);
    - 1 두 배 단추 (doubles later picks);
    - **4 망각 먹보** (cardboard boxes with button eyes and a zipper mouth; one ends the hunt unless the needle blocks it).
  - [그만 열기] keeps everything found.
  - ~~A perfect board (all 16 good boxes) gives +1% basket rate permanently (cap 10 perfects).~~ (critic) A perfect board grants a title once; no permanent bonus.
  - Expected value ≈60% of the board.
- **Sources:**
  - first clear of each chapter boss (after the SPEC4 chest);
  - 보관소 trial-floor chests;
  - 손님 추억 가득;
  - 등불 길 boss.
  - ~~`MetaPopupKind.BoxHunt` (between stages)~~ (critic) The board opens inside the source's own result card or popup (chapter chest, trial-floor chest, guest reveal, walk result), so no new popup kind is needed.
- **Art:** box + mimic (2 images ≈10 credits). Audio: open / gulp. **Save:** `BoxHuntSave { HashSet<string> huntsDone; bool perfectTitle }` (critic: `ShopExtraSave` no longer exists).
- **Why (v1: P1; critic: P2):** stop-or-continue tension turns flat reward screens into memorable moments; it costs little and fits the eerie-cute tone.

### R2-17 원정 길 익히기 / 단골 길 (Route familiarity marks; critic: not stars) — P1 · S
- **From:** G13 (Egg Inc ship stars, Idle Slayer minion levels, AFK bounty board).
- **Have / gap:** partial. 8 zones with fixed durations and ClaimAll. SPEC4 IDLE-04 adds the ×1/×3/×6 ladder and ResendAll. Gap: repeat trips advance nothing.
- **Design:**
  - Each zone earns ~~stars~~ **가로등 marks (critic: not stars; stars mean grade)** 0–5 at **3/10/25/60/120** completions. Each mark gives +8% loot (gold and drops).
  - 3 marks add a rare line: that zone's souvenir keepsake chance (R2-12), or 오르골 태엽 5% before keepsakes exist.
  - 5 marks = '단골 길': auto-resend on claim when the members are free.
  - Marks show on the zone card, with the tooltip '{n}번 더 다녀오면 가로등이 하나 더 켜져요'.
  - (critic) The +40% at 5 marks multiplies SPEC4's weekend ×2 material drops and the ×3/×6 duration tiers. CR-07's material projection (R2-11 trigger) must include it.
- **Save:** `ExpeditionSave.zoneCompletions`. Art: none.

### R2-18 주말 골목 장터 (Weekend market) — P1 · S (critic: the free daily gift is SPEC4 NEW-02)
- **From:** K31 (Slime free gems daily, Stone Age daily/weekly/jackpot shops, LoM limited shop).
- **Have / gap:** partial. `shop_normal` has daily limits (`ShopDatabase.cs`). **(critic)** SPEC4 v1.1 §13.7 NEW-02 '오늘의 작은 선물' is the free daily gift: a 7-day rotation (C.3), `DailyGiftCardView`, the `tab.shop.free` dot and ≤30 별조각/week.
- ~~**오늘의 덤:** the top row of the shop gives 1 free claim per DayKey, rotating on a 3-day cycle: 단추 30분치 / 수선 천 10 + 리본 조각 10 / 별조각 10 (≈23/week). Red dot `tab.shop.free`.~~ (critic: duplicate of NEW-02, removed. Its ≈23/week was also counted on top of NEW-02's 20.)
- **주말 골목 장터:** Sat–Sun DayKeys, 6 slots seeded by WeekKey from a pool, 20–40% off. It is a **별조각 sink**. Examples: 기억의 실 ×3 for 75 별조각, 빠른 수선권 for 40, common bundles for 단추, keepsake polish kit. Limit 1–3 each.
  - ~~선택 기억편 ×5 for 150~~ **(critic)** 선택 기억편 ×5 for **250**, limit 1 per weekend.
  - At 30 별조각 a shard, v1's price was ≈3.7× cheaper than pulling for 라라벨 shards (≈112 each, §1.5). It would make "buy selectable shards" the dominant gem use and hollow out the gacha.
- Art: optional stall banner (≈5). **Save:** ~~`ShopExtraSave`~~ `MarketSave { string marketWeekKey; Dictionary<string,int> marketBought }` (critic).

### R2-19 재료 엮기 (Material merge) — P1 · S
- **From:** K28 (Journey of Monarch combine/dissolve, 7K Elke's Cauldron, Maple 5 dupes → next grade).
- **Have / gap:** missing (기억편 can substitute rares in crafts).
- **Recipes** (a [엮기] tab in BagPopup; daily limit 5 each, 05:00):
  - 수선 천 20 → 레이스 조각 1
  - 리본 조각 20 → 우산살 1
  - 촛농 방울 15 → 기억의 실 1
  - 기억의 실 5 + 단추 60분치 → 오르골 태엽 1 (critic: **limit 1/day**, not 5. The basket gives ≈2–3 태엽/day at stage ≈50 (0.05/h × 1.02^49 × ≈18 credited hours), so 5 merges a day would roughly triple the epic supply that R2-05 breaks and evolutions are meant to gate.)
  - No 달빛 조각 recipe: it stays scarce.
- Counter MaterialMerge. **Save:** `MergeSave { string dayKey; Dictionary<string,int> used }`.
- **(v1.2) [모두 엮기] (from #108; Habby-style Merge All, Legend of Mushroom's auto-use filters).** One button weaves only the surplus above what the currently craftable and the next evolution recipes still need (it reads the `EvolutionSystem` recipes), so it never eats materials meant for the next form, and it respects the daily limits above. A toggle '바구니를 받을 때 자동으로 엮기' (off by default, `MergeSave.autoWeave`) does the same on each basket claim. The ratios stay as tuned: #108's 5:1 would flood rares and epics.
- **Why P1:** a deterministic path to rares, nothing piles up uselessly, and it costs about one day.

### R2-20 대표 친구 + 수선점 이름 (Partner friend + shop name) — P1 · S
- **From:** C12 (Tabikaeru naming, Neko Atsume renaming and Myneko, 고양이 별장, Usagi Shima residents).
- **Have / gap:** partial. ReturnPopup shows a random owned lineage's line; SleepModeView shows the shop.
- **Design:**
  - Name the shop (≤8 chars, default '작은 수선점'; 1 free rename, then 단추 60분치). It appears on the field signboard plate, the profile chip, letters and the share card.
  - Choose one owned lineage as **대표 친구**:
    - its line opens the return popup;
    - it dozes by the window in sleep mode ('zzz', wakes on tap);
    - it is the profile portrait;
    - it gets +10 affinity on the first login each day;
    - it signs local notifications: '우두커니: 비가 와요! 잠깐 들러 주실래요?'.
  - No stats.
- **Save:** `ProfileSave { string shopName; int renames; string partnerLineageId }`.
- (critic) The shop name must pass a local word filter and is ≤8 chars. It ends up on share cards (SAVE-01 share sheet), so it must not carry anything abusive out of the game.

### R2-36b 진화 가지 잇기 + 새 친구 (Deeper trees, then new lineages) — P1 · M + art — new (critic, missing from v1)
- **From:** X1. MapleStory Idle has 18 jobs. Crumble launched with 69 cookies and 54 pets, Stone Age Idle with 67 pets. Every reference keeps adding collectibles after launch.
- **Have / gap:** missing.
  - Six of the seven lineages have 3 forms (`Art/Characters`: chal/lala/mong/rouge/ttok/udu × 3), against 짝짝이's 11.
  - A typical player owns all 7 lineages within about a month (≈29 pulls/week; Legendary pity 60; the Mythic 1%). After that, every pull, friend ticket and borrowed-friend tease is shards only, and the gacha, the main 별조각 sink, has no new target.
- **Step 1 (wave B): +1 form per short tree (6 forms).** Each is a deterministic 진화 기억도 recipe at tier 3: ×1.35 base, `FormStats.TierGrowth`.
  - ~~This is the scheduled ceiling raise that the R2-04 math check calls for (CR2-08).~~ It sinks the 오르골 태엽 / 달빛 조각 that R2 modes now pay.
  - (CR4-07) It is **not** the ceiling raise. PROGRESSION-SIM puts the ceiling at ≈ch55 (stage 535–571, days 49–71 for the engaged player).
    - Six forms at ×1.35 over their tier-1 parents, each in one of a lineage's two slots, add at most ≈ +19 % team attack, ≈ 5–15 stages at the ×1.02 late slope.
    - SPEC5 ships them before launch, so a launched player has them from day 0.
    - The ceiling raise is step 2's post-launch cadence, scheduled from the measured ceiling day (PHASE4-ROADMAP Q16).
  - Art: 6 forms × ≈5 credits, with the base art as reference.
- **Step 2 (post-launch cadence): one new lineage every 4–6 weeks.** Each has 3 forms, skills from `SkillKit`, 2 traits, a `GachaContent` entry at Rare or Epic grade (a new chase without pushing the top end), codex and affinity lines, and one costume (R2-36a pattern).
  - The first two fill the thin trait graph. Rain has only 우두커니 today, and Night, Light, Sound, Metal and Charm have two lineages each.
  - Draft 1: '한 짝 장화', a lost rain boot [비·밤], body type *fully non-human mascot*.
  - Draft 2: '풍경', a wind chime [비·소리].
  - Both move the roster toward the art memory's 35/45/20 body-type mix.
  - Art per lineage: 3 forms + card ≈20 credits.
- **Effects elsewhere:** with 10+ lineages, SPEC4 §13.10's trigger for manual presets fires (R2-08). The trait-rule trial floors (R2-02) become viable again.
- **Why P1:** in a collection RPG the collection is the content. R2 v1 added 31 keepsakes, 18 visitors, 12 guests and 6 pets, but not one new playable friend.

### R2-37 골목 지도 넓히기 (New places on the main ladder) — P1 · M + art — new (critic, missing from v1)
- **From:** X2. MapleStory Idle has 47 chapters with new maps and monsters, and Legend of Mushroom moves through regions. A region change is the genre's basic "I travelled somewhere" reward.
- **Have / gap:** missing. The endless ladder runs on **1 field** (`Art/Battle/field.png`, plus the IA's tall outpaint), **7 enemies and 2 bosses** (`Art/Enemies`). Stage modifiers (6) are the only visual change from 1-1 to 10-10.
- **Design:** every 5 chapters a new *place* on the night map, for example 골목 → 시장 길 → 기차역 → 다락방 거리 → 바닷가 등대.
  - (CR4-07) On the simulated engaged timeline, chapters 6/11/16/21/26 fall on days ≈0.3/1.3/8.6/11.1/13.9, so a strict five-chapter step shows every place within two weeks, and then nothing new appears until the ≈ch55 ceiling.
  - Space the places by engaged days instead, about one new place every 10 days up to the ceiling (e.g. chapters 6, 11, 21, 31, 41). Later places go to the post-launch cadence.

  Each place has:
  1. a field background in the place's palette (max quality, outpainted from the tall field for the same pad layout; ≈10 credits);
  2. 2 enemy families built from existing gimmick flags plus R2's three new ones. R2-01's 그림자 까치, nest and dust king are the first place's new enemies at no extra cost;
  3. 1 boss with a `BossHintDef` line (SPEC4 §13.1 pattern);
  4. its storybook chapter (R2-27) and a name on SPEC4's chapter map (CR-05);
  5. (CR4-20) its own ambience bed (`PlaceDef.ambKey`, e.g. `amb_market`), made by synthesis or from CC0 packs, 0 credits. AUDIO-SPEC's director otherwise keys beds by stage modifier only, so without one a new place looks new but sounds the same;
  6. (CR4-22) a 1080×810 crop of its tall field (rows 619–1428, PIL, 0 credits) for the 4:3 formation mini-field and the `field.png` fallback (UX-SPEC-IA §1.4).
- **Cost:** ≈25 credits per place (background 10, 2 enemies 10, boss 5) plus content rows. Every enemy also gets a codex entry (Q-05 enemyGem 10 → +30 별조각 lifetime per place).
- **(v1.2) A place that plays differently (from #97; Kingdom Rush's unique maps and props, BTD6 map tiers).** Without touching the battle geometry:
  - **One place rule**, always on in that place's chapters, taken from the R2-00 rule table: e.g. 빗물 다리, one puddle lane slows enemies 15% (a per-lane speed scalar); 야시장, crowds +20% wave size; 지붕 위, the Wind modifier at 50% of rotation slots. ~~The stage panel shows it as one chip.~~ (CR4-12) The StagePanel (330×134, UX-SPEC M11) has no free slot, and the side-battle HUD covers it. The place sign is a second icon inside the SynergyRow's modifier chip, with the rule in its tooltip.
  - **One interactable per place (optional, wave C).** Example: '고장 난 가로등' at a lane mouth; a tap (5 잔향) lights that lane for 10 s, revealing 망각의 그림자 and giving light-trait friends +20%. AUTO uses it when that lane's danger is highest, so idle play keeps its value. It follows the NEW-03 tap-pad rules (MAIN only, ≥8 px from rails, pads and the guide bar). ≈5 credits per prop.
  - **Boss mechanic pool (from #80; BTD6 boss events, Kingdom Rush set-piece bosses).** Place bosses draw on: a chime every 15 s that hastes all enemies for 3 s (소리 friends muffle it); splitting into 3 lanes at 66% and 33% HP; mirroring the strongest guardian's attack for 10 s. Each comes with a `BossHintDef` line and a counter trait. Skins stay IP-safe: shadows cast by forgotten things ('괘종시계의 그림자'), never an object that is one of our friends (no umbrella swarm next to 우두커니), and never the weekly guest itself (R2-03: we never fight the guest).
  - **Deferred (D-21):** per-place lane geometry (closed lanes, tunnels where enemies skip ahead, spiral approaches) and a per-map medal grid. They change `BattleGeometry` (5 slots, SpawnRadius 2.5), every pad and keep-out validation (UX-SPEC-IA §1.3, Appendix B) and the FTUE coach rects.
- **Why P1:** it makes the long run feel like a journey without new systems, and it gives the story (CR-05 / R2-27) and the boss hints new places to happen.

---

## 6. Waves C and D — P2 systems (compact specs)

### Wave C (low art; needs the wave A/B engines)

| ID | Name | From | Design essentials | Size |
|---|---|---|---|---|
| R2-21 | 꿈길 + 등불 길 완전판 (critic rename) | K5, K6, G1, G20 | **Full walk:** live branching map (3 alleys × 10 steps), depth 1–10, 등불 나무 complete, an 8-week rotating charm theme (e.g. '장마의 장' = rain charms; absorbs G20's side season). **꿈길 (weekly seeded):** 9 steps, same seed for everyone (WeekKey), 3 attempts/week, score = floors × 1,000 + durability + charm bonus; thresholds 3k/5k/7k/9k → 별빛 사탕 50/80/120/200 + 별조각 20/40 (≤60/week) + a weekly title at 9/9; share code 'LL-W45-8210' | L |
| R2-22 | ~~밤시장 리그 +~~ 골목 게시판 + 수선점 명함 (critic: league deferred, D-18) | G2, K25, K26; (v1.2) #81, #90, #112 | **(critic)** Keep the endless-wave config, personal boards and the 명함. The 29-neighbour league with promotion and demotion is deferred. Fictional rivals ranked against you are a competition-anxiety loop in a cozy mending game, and "clearly fictional" scores still read as fake social proof. Compare with real friends through 꿈길 share codes instead. v1 text follows for the record. Endless-wave config of the walk engine with a perk pick every 10 waves (trade-off perks allowed). ~~**League:**~~ rounds open Wed / Sat 05:00, 48 h, 1 scored attempt (+1 retry ticket from weekly missions), seed hash(weekKey, round). Bracket = you + 29 **clearly fictional** night-market menders (portrait + one-line personality; tooltip '이웃들의 점수는 이야기 속 기록이에요'). Their scores are drawn from a fixed per-league distribution (median = par ±20%), never from your result. Leagues 실밥 → 단추 → 리본 → 레이스 → 달빛 → 별빛; top 6 promote, bottom 6 demote, 실밥 protected. Rank pays only cosmetics + 발자국. Score **thresholds** pay 별조각 (≤80/week). **Boards:** personal records per mode with history. **명함:** a PNG share card (title, power, best floor, seed + score hash) through the SAVE-01 share sheet. Standalone arena: deferred (D-04). **(v1.2) 끝없는 밤 in detail (from #81; Rush Royale co-op waves, Random Dice solo, Lucky Defense):** start at a stage equivalent of 60% of `highestStage`, +1 per wave; a boss every 10 waves (the boss roster plus R2-37 place bosses), every 2 waves after wave 50; every 5 waves a 1-of-3 '등불 기름' pick from PRG-07's B.4 pool and R2-10's charms (auto-pick toggle for idle play); '졸음 안개' at waves 25/50/75 (the two strongest guardians doze for 15 s unless the lamp is lit); no regen between waves except picks; wave rest 6 s (R2-39 matters here); ×3 speed in this mode only. It costs **1 등불** (R2-10's energy, no new energy). Pay: 단추 (5 + wave)분치 (CR4-05: worth something late only if PHASE4-ROADMAP Q15 indexes `GoldMinutes` to real income) + materials by wave reached; the first time each WeekKey a run reaches wave 10/20/30/40/50, 별빛 사탕 10/15/20/25/30; **0 별조각**. Permanent medals at best wave 10/20/30/40/50: 무명실 · 은실 · 금실 · 달빛 실 · 오색실 frames (thread, not stars; 오색실 also grants a tint, R2-09). A '지난주의 나: 37' marker on the wave counter shows the player's own best, never an NPC's. **Friend codes (from #90, #112; BTD6 share codes, AFK Arena friend replays):** a '쪽지 코드' (`LLN1.` + the SaveCode packer: gzip, Base64, CRC32) carries mode, period key, seed, rules, the formation (11 form ids with levels and resonance) and the score. Importing adds that friend's row (≤20 per mode, real friends only); [다시 보기] re-simulates an AUTO run on the same seed, a watchable ghost that also checks the score (a run with manual inputs shows '직접 플레이한 기록이라 다시 볼 수 없어요'). Imported rows pay nothing. **Share line (#112, after Wordle):** one line of text for 꿈길, 끝없는 밤 and 오늘 밤의 의뢰 (date, mode, result, two weather symbols) through the SAVE-01 share sheet. Rejected: #90's NPC-named medal thresholds and #112's 9-shop rival ladder (D-18 and the orchestrator's 'no NPC league'), and a fight against a friend's formation (D-04) | L |
| R2-23 | 마음의 실 확장 | C1, C2, C7, C13 (#71 in full); (v1.2) #100 | Builds on SPEC4 CR-10 (levels, 기억 조각 lines, frame, pose, tap-to-pet 3/day). **Gifts (작은 선물)** use the 7 traits as tags; value = 8 + 12 per tag matching the lineage; one loved item = 60 (짝짝이 '짝 맞는 장갑', 우두커니 '빗방울 유리병', 라라벨 '악보 한 장' …); ~~3 per lineage per day~~ **(critic) 3 gifts per day in total**. Per lineage that would be 21 gifts on top of SPEC4's 21 daily pets, ≈42 micro-taps a day; sources: souvenirs, visitor tips, workbench crafts (수선 천 3 → 손수건 [천]). **Letters:** the CR-10 even-level lines are delivered as mail (expireDays 0, a [편지] filter, envelope with the lineage seal); 라라벨's Lv10 letter unlocks BGM '마지막 음'. **Badges:** Bronze Lv3 / Silver Lv6 / Gold Lv10. **Object gestures** replace the generic squash for the 3 daily cares (sock pull swipe-up, key polish rub, candle hold, umbrella shake, music-box wind 2 circles playing 6 notes, never the 7th). **Requests:** a '!' bubble about every 20 min, max 4/day, +10 affinity, ignoring costs nothing. **Anniversaries:** the meeting day (grant DayKey) yearly, gifts ×3. No decay. ~~Currency 실밥~~ **(critic) No 실밥 in wave C.** Gift items come from souvenirs, workbench crafts and the weekly guest's thank-you card; visitor tips and 실밥 arrive only with wave D. **(v1.2) Anniversaries (from #71 in full; Stardew birthdays, Cats & Soup star-cat days):** the meeting day is `LineageSave.obtainedUnix`, already stored at grant (`CharacterSystem.cs:364`) and unused today (0 → the install day). 만난 지 100일: a '만난 날' card (the friend's portrait + one line) + a frame + 별빛 사탕 30; 200일: 별빛 사탕 50, and that DayKey counts as its 찾은 날; **1년: a title '{이름}{와/과} 함께한 1년' + 별조각 50, once per lineage**. A date missed while away fires on the next login within 7 DayKeys. **찾은 날 (lore days):** one fixed date per lineage, e.g. 우두커니 6/19 (장마의 첫날), 몽당이 12/22 (동지), 라라벨 2/14 (선물하는 날); content picks the other four away from R2-33's lunar-table days. On that DayKey its gifts count ×3 and a short letter arrives ([편지] filter); a cake decor joins only once R2-29 exists. **Loved-material gifts (from #100; Blue Archive favourite gifts, Pocket Camp):** the first gift source is the 9 existing materials, so no new item type is needed. One gift = 10 commons, 2 rares or 1 epic, worth 20 affinity; the lineage's loved material is worth 60 (draft: 짝짝이 수선 천, 똑단이 기억의 실, 찰칵이 여행 가방 조각, 몽당이 촛농 방울, 우두커니 우산살, 루즈리 리본 조각, 라라벨 오르골 태엽). Still 3 gifts a day in total (critic). It sinks surplus commons (R2-11's trigger); souvenir and craft gifts keep their trait-tag values. Save fields: §3.3 | M |
| R2-24 | ~~수선 일기~~ **cut (critic)** | C7 | '수선 일기' sits next to the existing '수선 일지' (missions), so the two names would be confused. Templated diary text adds little. If wanted later, it becomes one '어젯밤' line in the journal header. v1: At 05:00, a 3-line entry composed from yesterday's daily counters + the top highlight (60 templates with slots), kept 30 days in a calendar (attendance stamp art). First open per day: 단추 10분치 (실밥 5 once 실밥 exists). Pure memory, on theme | S |
| R2-25 | 추억 사진첩 **(deferred D-13, critic)** | C5 | Deferred. It is L, and on-device capture of the uGUI battle (RenderTexture readback, JPG encode, 150 files ≈7.5 MB) is untested in this pipeline. v1: Photo moments from existing BattleView events (StageModifierStarted, SynergyActivated, SkillCast, LampLit, boss kill): a 📷 bubble for 8 s → a soft shutter (flash α ≤0.3, respects reduce-flash) → a 540×390 JPG ≤50 KB in persistentDataPath, cap 150. ≈55 moments at launch (35 lineage, 12 pair, 8 modifier). First capture pays 단추 / 실밥 + 3 affinity. Milestones 5/10/20/35/55 → 별조각 30/50/80/100/150 (410 lifetime). Frames and stickers; share via the SAVE-01 sheet. **No new character art** | L |
| R2-26 | 숨은 기록 (critic: trimmed to hidden records) | K7, G17, C10; (v1.2) #85, #98 | **(critic)** Keep only the **hidden records**: 5 → 20 Q-04 Flag lines with clue text, a content-only S task. '한 땀 등록' moved into R2-05a as '추억 수놓기'. Set pages, 완성 망토 (+5% basket cap, which touches the 별조각 line) and '수선점 등급' are deferred (D-19). They stack a third collection-bonus layer on Q-05 and R2-12, and '수선점 등급' sits next to R2-04's '수선공 등급' and the shop's '수선점 레벨'. v1: **Set pages** in the SPEC4 codex ('비 오는 날', '가족 앨범', '화장대', '자장가') with small conditional bonuses. **'한 땀 등록':** a shard sink after resonance 10 **and** all cap breaks (30 shards/stitch, 5 per page, +1% each). **Hidden records** 5 → 20 (Q-04 Flag lines with clue text, e.g. '비 오는 밤에 우산을 접어 본 적 있나요?'). **완성 망토:** a frame + +5% basket cap at 100% of a category. **수선점 등급** = Σ lineage levels + 10 × forms + 2 × ledger entries + shop Lv/5, with a reward every 50 points and rank titles (동네 수선점 → 골목의 등불 → 밤거리의 수선점 → 잊힌 것들의 집). **(v1.2) Hidden-record candidates** (pick 15, so the total with SPEC4's 5 is 20; from #85's BTD6 and Kingdom Rush feats, #98's Realm Grinder secret trophies, #72 and #83): '한 줄기 빛' (one lamp hits 25 enemies), '빈 방위의 기적' (a chapter boss cleared with ≤3 guardians), '마지막 음까지' (win a stage in which 라라벨's skill 마지막 음 played: the skill, not the story beat), '새벽 다섯 시의 손님' (open the game between 05:00 and 05:10), '잠든 수선점' (60 minutes of sleep mode), '반딧불 백 마리' (100 NEW-03 catches), '밤하늘의 손님들' (all five firefly kinds, R2-15), '서두르는 수선공' (한발 먼저 100 times, R2-39), '오색실' (끝없는 밤 wave 50), '단골 손님' (the same weekly guest at 추억 가득 twice, 12 weeks apart). SPEC4's h2 and h3 already cover #85's '한 짝도 잃지 않았어요' and #98's '자장가 끝까지'. Each pays a title + 별조각 10 (§3.7). Existing counters and flags come first (SPEC4 `AffinityPet`, the sleep view, `CostumeSystem.IsNightNow`). Rejected: feats that grant 설계도 stars (#87 is rejected) and #85's 별조각 10–50 per feat | M |
| R2-27 | 동화책 조각 (6장+) | C11; (v1.2) #113 | **(critic)** Ships together with each R2-37 place, so every new place has its story. For chapters 6+: panel 1 at the first x-10 clear; panels 2–3 need 이야기 조각 (expeditions in that chapter's zones 20%/claim, guaranteed by the 6th, or a guest letter). SPEC4 CR-05 chapters 1–5 stay unchanged. Each page: 별조각 50 + a themed reward. **(v1.2, from #113; Tsuki's diary pages):** each page, chapters 1–5 included, also mails a one-line letter from that chapter's object ([편지] filter, R2-23). No reward | S |
| R2-28 | ~~친구들의 응원~~ **removed (critic)** | K22 | SPEC4 v1.1 §13.2 already ships 친구들의 응원: once per chapter, +20% for 600 s, CheerChip with `jjak_cheer`. SPEC4 §13.10 names it, the daily gift and the fireflies as the free equivalents of ad boosts. v1's version adds 3 buffs × 3 free uses a day (≈3 h of daily buff uptime to keep re-tapping), which is the ad-buff chore loop without the ad. v1: 3 cheers (단추 +50% / 바늘 +25% attack / 별빛 +30% lamp), 20 min, 3 free uses each per day, 응원 Lv1–10 by use (+5% and +2 min per level), the 짝짝이 응원대장 pom-pom on the HUD (costume art exists). Folds in PRG-03's once-per-chapter cheer | – |
| **R2-38 (critic, new)** | 동화책 수수께끼 (storybook puzzles) + (v1.2) 오늘 밤의 의뢰 | X3; (v1.2) #79, #101, #90 | AFK Journey Peaks of Time / Voyage of Wonders. Hand-made **one-time** stages, each with a fixed roster (it may include borrowed unowned forms through the R2-10 code), a fixed modifier and a goal: '불 밝히기 없이 보스를', '동쪽 길만 막아요', '잔향이 둘로 버텨요'. First clear pays 단추 / materials, and every 10 puzzles pay a title. It teaches synergies and directions and shows off forms. It uses the R2-00 host and needs no new art. Content is S per 10 puzzles. **(v1.2) R2-38b 오늘 밤의 의뢰 (from #79 and #101; BTD6 Daily Challenge, Slay the Spire Daily Climb, Wordle, PvZ2 Penny's Pursuit).** Every 05:00 a small forgotten object leaves a request letter at the shop door: one generated puzzle per DayKey, the same on every device that day (seed = hash(DayKey)). Client pool ≈30, each a silhouette from existing art plus one line ('금 간 찻잔: 손잡이를 찾을 때까지 문을 지켜 주세요', '태엽 빠진 오르골', '한쪽 귀 떨어진 토끼 인형'). The generator picks a cleared stage between `highestStage` − 8 and − 2 (chapter-boss stages weighted ×2), one weather, two R2-00 rules (risk 3–5 in total, exclusive groups respected), one roster rule (trait rules only when ≥3 owned lineages have the trait) and a goal: '내구도 50% 이상으로 지키기' / '90초 버티기' / '보스를 60초 안에'. The team is the player's own, so the puzzle uses the collection (맞춤 편성 may help). Attempts are unlimited and failure is free. **Pay:** the first clear of the DayKey gives 단추 30분치 + 수선 천 5 + 별빛 사탕 10; 5 clears in a WeekKey give 기억의 실 2 + 별빛 사탕 30; lifetime clears 10/50/100/365 give titles '단골 수선공' / '골목의 수선공' / '밤의 수선공' / '일 년의 밤' (the last also a tint, R2-09). **0 별조각**, no streak, no bank: a missed day loses only that day's letter. **의뢰서 만들기 (from #90; BTD6 Challenge Editor):** pick a cleared stage, ≤3 rules and a roster rule to get an 8-character code for a friend (pasted from KakaoTalk via the share sheet); custom requests pay nothing. Not in 오늘의 순회 (a puzzle, not a chore), no mission row, no dot. Unlock 3-1 (§3.5); entry: the 골목 게시판 card. `BattleMode.Request 7`, `CounterKey.RequestClear 56`, `RequestSave` (§3.3). Rejected: #79's daily 별조각 15, '완벽한 한 주' streak and journal mission; #101's ≈210 별조각/week, its 7-day-streak 선택 기억편 10 and its loaner team (comparable scores live in 꿈길; try-before-pull lives in R2-10's borrowed friend) | S per 10 + M (38b) |
| **R2-39 (v1.2, new)** | 한발 먼저 (call the next wave early) | #83 | Kingdom Rush / BTD6 early call on the main battle; AUTO never calls; 잔향 and 불빛 only, no currency. Full spec in §6.3 | S |
| **R2-40 (v1.2, new)** | 축음기 음반 (collectible BGM records) | #76 | Neko Atsume 2 / Tsuki / Animal Crossing music collection; 10 records from the AUDIO-SPEC loops plus 2 new pieces; cosmetic only. Full spec in §6.3 | S |

### Wave D (art-heavy; needs a credit top-up decision, §9.2)

| ID | Name | From | Design essentials | Size |
|---|---|---|---|---|
| R2-29 | 안채 꾸미기 + 밤손님 | C3, C4, G18, K27; (v1.2) #75, #118, #119 | **Decor:** a 1080×1320 warm room; 8 spots → 12 (shop Lv50) → 16 (Lv150); MVP 20 pieces in 4 sets of 5 named after our places, each with 1–2 trait tags + 포근함 5–30; crafted with 실밥 (80/250/700) + commons on a 30 min–4 h timer; 3 '오늘의 가구' rotate at 05:00. 포근함 tiers 20/50/100/160/240 → +2% basket each (max +10%; **critic: 단추 and material lines only**) + 1 visitor slot. ~~Mend ritual: after 7 days a loose thread appears; 실밥 5 restores it. It never breaks.~~ (critic: removed. It punishes absence in a cozy room; Neko Atsume and Cats & Soup never do.) Shop exterior growth states at Lv 1/20/60/120/200. **Visitors:** 18 non-fighting forgotten objects (12 common + 6 rare with conditions: last stage had rain, away across 20:00–05:59, real full moon …), rolled **only for time away / sleep**: visits = min(2 + tier, ⌊awayHours/1.5⌋), max 7, ×3 weight for liked tags, seed hash(dayKey, returnIndex). The return popup adds '밤손님 3명이 다녀갔어요' with tap-to-reveal silhouettes; tips of 실밥 5–15; a memento at 8 visits (4 for rares) gives +0.5% basket (max +9%; **critic: 단추 and material lines only**). Benched friends wander the room. **(v1.2)** (a) **No new currency by default (from #118; Usagi Shima, Pocket Camp):** decor costs 단추 (N분치, N 60–480 by size) + commons (CR4-05: after the CR-07 retune this is a real sink only if `GoldMinutes` follows real income, PHASE4-ROADMAP Q15; otherwise decor is free from week 2), and event decor costs 별빛 사탕 in R2-33's packs; visitor tips become 단추 minutes and commons. 실밥 is needed only if wave-D playtests show decor losing to R2-04 for 단추 (rule 5 unchanged). (b) **Favourites:** each visitor loves one decor piece; after its first visit the codex shows it ('{손님}은 {가구}를 좋아해요', Neko Atsume's Catbook), and placing it triples that visitor's weight. (c) **Mended visitors (from #119):** some of the 18 visitors are mended enemies (보송 먼지, 감은 태엽쥐, 풀린 실뭉치), unlocked after 1,000 / 300 / 500 kills of that enemy (needs per-enemy kill counters; today only totals exist) and drawn from the existing enemy art with a soft 'mended' tint, 0–5 credits each. Cosmetic: 'we mend, we don't destroy' made visible. (d) **이웃 수선점 구경 (from #75; Neko Atsume 2 'Go Out', Pocket Camp Complete camper cards):** an '초대장' code (`LLV1.`, SaveCode packer, ≈60 bytes: shop name, ≤16 decor ids, wallpaper and floor, 대표 친구, title) opens a read-only view of a friend's 안채 with their 대표 친구 waving. 1 이웃 도장 per unique code per DayKey, at most 3 a day; 10 stamps → a decor piece or sticker (cosmetic, so self-made codes are harmless). Three built-in neighbour rooms (우체통 할머니네, 등대지기네, 빵집 다락방) give solo players somewhere to visit; they show rooms, never scores. (e) **Optional entry:** a tap on the shop building in battle (MAIN only, not during FTUE or while aiming) opens the 안채 sheet; the L2 rail icon stays the main entry. Rejected: visitor tips of 별조각 10–30 and 선택 기억편 1–3 (#118; §1.5 guards), #118's 아늑함 thresholds (basket cap on every line, expedition −5%; the 포근함 tiers above stay), and #77's 실밥 earn and sink table (kept only as input if 실밥 is ever approved) | XL |
| R2-30 | 주머니 친구 **(deferred D-14, critic)** | K17 | Deferred. It adds a third combat layer next to guardians and echoes, plus the lamp. It is L with art (6 pals + idle loops), and its 8 h rest is yet another timer. v1: 6 pocket pals (fully non-human mascots: 구슬 다람쥐, 병뚜껑 거북, 클립 잠자리, 지우개 강아지, 반지 고양이, 성냥갑 개구리), 3 slots (2nd ch3, 3rd ch6), a timed small help (lane roll knockback every 12 s / 5% shop shield every 20 s / mark the strongest). Sources: expeditions 3%/claim, records, event shop, tower milestones (no new gacha). Level with 단추 + 리본 조각; duplicates add stars. '주머니 친구 집': a benched pal returns 단추 + 응원 EXP after exactly 8 h (no stealing) | L |
| R2-31 | 창가 화분 **(deferred D-15, critic)** | K20, G8 | Deferred per SPEC4 §13.10 (Stone Age warning; the expedition ladder already is the come-back timer). With R2-11 that would be the third timer garden. Its 24 h 달빛 넝쿨 paid 기억편 of a *chosen* lineage, the same leak as errand 4. v1: 3 pots (+1 ch5, +1 ch8). Seeds from the basket and expeditions. 들꽃 1 h → 단추 30분치 · 실타래 콩 4 h → 천 10 + 리본 6 · 촛불 튤립 8 h → 촛농 8 + 실 1 · 달빛 넝쿨 24 h → 기억편 5 (lineage chosen at planting) + 20% 달빛 조각. Yield ×1–×3. Neighbour pots cross-pollinate 10% → a 12-seed album. Pots pause on clock rollback | M |
| R2-32 | 원정 엽서 + 도시락 | C6; (v1.2) #114 | 24 postcards (3 per zone) at 25%/claim (+10% trait match, +15% with a packed 도시락), undiscovered ×3 weight; template '{장소}에서 — {이름}: {한 줄}' (56 lineage × zone lines); 3-line expedition log from a 40-line pool; a mid-trip '도착 소식' mail for trips ≥3 h; a full zone set → a storybook page + +5% material drops there. **(v1.2, from #114; Travel Frog, Neko Atsume):** the friends who went appear on the card as stickers (their portraits composited, no per-friend art), and a duplicate card becomes 별빛 사탕 10, never 별조각. #114's 32 cards stay 24 (art budget); its 그림일기 snapshot diary joins D-13 | M |
| R2-33 | 계절 달력 꾸러미 | G15, C9, K32; (v1.2) #74, #102 | **(critic)** 설날, 정월대보름, 칠석 and 추석 are lunar dates that move every Gregorian year. Use a hard-coded date table for 2026–2036 (`EventScheduleType.DateList`, §3.4), not SPEC4's fixed month/day `Dates`, and not the moon-phase math (the astronomical full moon can be a day off from 보름). Packs are cosmetic plus stamps only (SPEC4 §13.5(i)). 12 monthly mini-seasons (14 days, repeat every year: 설날 연, 정월대보름 부럼, 봄비 장화, 벚꽃 손수건, 어린이날 종이배, 장마 우산꽂이, 칠석 별다리, 반딧불 여름밤, 추석 송편 달, '잊힌 것들의 밤', 첫 서리 털실, 첫눈·성탄 양말). Each: a 7-day login stamp, 3 decor, 1 sticker sheet, 1 visitor, 1 accessory, earned with 별빛 사탕 in the event shop. **'작년 장식'** shelf at 50%; **'지난 축제 상자'**: ended event costumes and keepsakes at 1.5× 별빛 사탕; **계절 태엽** after a year: replay one past season for 24 h, once a week. Launch with 3 packs (10월, 12월, 2월). **(v1.2) Accessory layer (from #74; Usagi Shima hats, Cats & Soup, Pocket Camp dress-up):** each pack's accessory is a hat, scarf or ribbon drawn at one head anchor per form (29 content rows `FormAnchorDef { formId; Vector2 head; float scale; float angle }`), so one sprite fits every lineage. Shown on guardians in battle at 0.8 scale, previewed in R2-36a's fitting room, purely cosmetic (no affinity, no stats). 12 accessories over the 12 packs, one per image, ≈5 credits each. **October '잊힌 것들의 밤' (from #102):** enemies wear paper masks (one overlay sprite on the enemy anchor, ≈5 credits), cosmetic only. #102's other Korean seasonals (설날·추석, 첫눈, 선물 포장) are this item's lunar table and packs already | M + art |
| R2-34 | 미니게임 **(deferred D-17, critic; the roulette is cut)** | K24 | '단추 뽑기 통' is cut: a spin wheel is a lottery device with no place in the mending fiction, and it keeps the 'wheel' habit ready for later monetisation. Fishing and the board game are deferred. They are daily-capped chores, and the board belongs to an EVT event shell, not a permanent mode. v1: '빗물 웅덩이 낚시' (after rain stages; 5 casts/day; a timing bar; 단추 / 기억의 실 / a rare 기념품 상자 / joke catches like '한 짝 장화'). '단추 뽑기 통' (1 free/day + spins from weekly missions; full table always shown). '골목 말판 놀이' (an event shell: event dice → 30-tile board → event-shop rewards). All with daily caps | M |
| R2-35 | 다락방 발굴 **(deferred D-16, critic)** | K19 | Deferred. 핀셋 charges (+1/20 min, cap 30) are a stamina timer: a fifth come-back timer next to expeditions, research, pots and pal rests. It also had a 별조각 trickle. v1: A 7-wide attic grid; 핀셋 charges max 30 (+1/20 min); dust 1 / trunk 2; finds 단추, 기억의 실, a capped 별조각 trickle, research speed-ups; a shelf milestone every 10 (50 = 오르골 태엽, 100 = 선택 기억편 20); 자석 / 먼지떨이 2/day free | L |

### 6.3 (v1.2) New items in full

#### R2-39 한발 먼저 (Call the next wave early) — P2 · S · wave C — new (v1.2, from #83)
- **From:** #83 (tower-defense sweep). Kingdom Rush: calling a wave early pays gold for the seconds skipped and recharges spells by the same seconds (KR5: double-tap the skull). Bloons TD 6: send rounds early. Players report the payout is tiny when called at once; it is a tempo and greed tool, not an economy pillar.
- **Have / gap:** missing.
  - Wave rest is fixed: `waveRestBase` 3.5 s − 0.05 s per stage, floor `waveRestMin` 2 s; `firstWaveDelay` 1.5 s (`BattleConfig.cs:96–112`; `BattleSimulator.cs:431, 470, 802–825`).
  - A stage is ≈3 waves before its boss: `killsToBoss` 15 + 0.6 per stage (cap 40), wave size 5 + 0.25 per stage (cap 14).
  - The next wave's pressure lane is planned when a wave ends (`PlanNextWave`, `pressureBias` 0.7, `BattleSimulator.cs:789–833`), so the game already knows where the next group will mostly come from.
  - An active player has the lamp, echo drags, SPEC4's tap-to-pet and fireflies, but no tempo choice.
- **Design (IP):** between waves, a small paper lantern lights at the mouth of the lane the next group will mostly use: '다음 무리가 이쪽으로 와요'. A tap means the friends step out to meet the shadows before they finish gathering, and the next wave starts now.
  - **Reward per second of rest skipped** (the Kingdom Rush rule in our resources): 잔향 +2 (cap +8) and 불빛 +1% (cap +4%), granted at once with a '+잔향 6' number.
  - **No 단추, no 별조각, no clear bonus.** The time saved is the reward. #83's '+1% stage-clear 단추 per call (up to +10%)' is not adopted.
  - **AUTO never calls early.** #83's AUTO '서두름' toggle is not adopted. Idle income, CR-07's gold-flow model and every economy test stay as they are. It is an active-play bonus, like the firefly.
  - **Refused, lantern hidden:** during the boss phase; while 20+ enemies are alive (half of `maxEnemies` 40, to protect SPEC4's FX budget, §11.3); during FTUE; while asleep; in SHEET state; in the weekly guest fight (one boss, no waves); and in any mode whose rules say so (R2-02 trial floors, R2-38 puzzles and requests keep their designed tempo).
  - **Modes:** main stages, errands and 끝없는 밤 (R2-22, wave rest 6 s, where it matters most).
- **Numbers:** at a 3.5 s rest an instant call gives 잔향 +7 and 불빛 +3.5%; at the 2 s floor, 잔향 +4 and 불빛 +2%. With ≈3 waves a stage, a player who taps every time clears ≈5–10% faster. Calls are capped by the rest itself, so there is no farm.
- **Unlock:** after Speed2x (1-6) and `ftue.done`. No FeatureId and no card (1-6 already has two). The first lantern shows one `BattleNoticeView` line, '등불을 누르면 다음 무리를 지금 맞이해요 · 잔향과 불빛을 조금 얻어요' (2.4 s, once; flag `hint.call_early` in `PlayerData.flags`).
- **UI in the new frame:**
  - On the battle field, **MAIN state only** (`SheetFade 0`; the deck strip and lamp are under the sheet in SHEET, so stacked waves there would be unfair).
  - A 72 px lantern (`Icons/lamp` tinted Cream) with a 112×112 hit pad, placed on the pressure lane 0.45 world units inside the spawn circle. Its rect is clamped to x 24–1056 and kept ≥8 px from the rail cells (UX-SPEC-IA §3.3), the GuideQuestBar rect (`GuideQuestBarView.TryGetRect`), guardian pad hit areas and a live NEW-03 firefly.
  - A 1.2 s breathing glow while more than 0.5 s of rest remains (one of the allowed loops); it fades when the wave starts. Reduced motion: a static icon.
  - Tap → `call_early` cue + Light haptic + the resource number.
- **Sim:** `BattleSimulator.CallNextWave()` returns the skipped seconds, or 0 on refusal, and sets `_waveRest = 0`. It is a sim input like a lamp press, so headless == live holds only for identical inputs, and '결과 바로 보기' fast-forwards the same instance (R2-00). `BattleSystem.CallNextWave()` forwards it in Stage, Errand and Endless modes.
- **Art:** 0 credits (reuse `Icons/lamp`). Optional paper-lantern icon ≈5.
- **Audio:** `call_early`, a soft paper whoosh + small bell, through the AUDIO-SPEC route (CC0 packs + original synthesis).
- **Save:** none. `CounterKey.CallEarly 55` (§3.4) feeds R2-26's hidden record '서두르는 수선공' (100 calls) and nothing else.
- **Tests (`CallEarlyTests`):** each refusal case; bonus = min(8, 2 × skipped) 잔향 and min(4, skipped)% 불빛; the AUTO path never calls (EconomyFlowTests unchanged); a scripted call on a fixed seed gives a different but repeatable result; the lantern rect never overlaps a rail cell, pad or the guide bar (capture check).
- **Size / priority:** S · P2 · wave C.
- **Why P2:** it is the genre's proven tempo verb on our core battle, it is small, and the AUTO rule makes it economy-neutral. Our wave rests are short (2–3.5 s), so the gain is modest: delight for active players, not something launch needs.

#### R2-40 축음기 음반 (Gramophone records: collectible BGM) — P2 · S · wave C — new (v1.2, from #76)
- **From:** #76 (cozy sweep). Neko Atsume 2 sells BGM tracks for fish, with one track unlocked by default. Tsuki's Odyssey gives a villager's first mixtape as a friendship reward. Animal Crossing's K.K. songs are collectibles played at home.
- **Have / gap:** partial.
  - The audio pipeline in progress (AUDIO-SPEC: CC0 packs + original synthesis) plans 9 music loops: `bgm_battle_night` ×3 takes ('등불 광장', '실과 바늘 왈츠', '한밤의 수선 행진'), `bgm_boss`, `bgm_dungeon`, `bgm_gacha`, `bgm_story`, `bgm_event` and `bgm_sleep` (`scratchpad/audio/audio_catalog.json`).
  - The player cannot choose any of them. R2-23 already promises that 라라벨's Lv10 letter unlocks '마지막 음'. A game that stays on screen for hours has no ambient personalisation.
- **Design (IP):** an old gramophone in the 안채, itself a forgotten object the shop once mended, plays the shop's memories. Each record is a night the player has lived through. The player picks the battle music and, separately, the menu-and-sleep music from owned records. Boss, gacha and event contexts keep their own music.

| Record | Source | Unlock |
|---|---|---|
| 등불 광장 | `bgm_battle_night` 01 | default |
| 잠든 수선점 | `bgm_sleep` | default |
| 실과 바늘 왈츠 | `bgm_battle_night` 02 | 수선점 Lv.50 |
| 한밤의 수선 행진 | `bgm_battle_night` 03 | first clear of 3-10 |
| 문 앞의 그림자 | `bgm_boss` | first clear of 7-10 |
| 어느 밤 골목에서 | `bgm_story` | storybook chapter 5 read (CR-05) |
| 잊힌 것들의 창고 | `bgm_dungeon` | 잔향이 던전 20층 |
| 별이 머문 밤 산책 | `bgm_event` | first 7-stamp card of the starlit walk (EVT-01) |
| 마지막 음 (new piece) | music-box piece | 라라벨 호감도 Lv10 letter (R2-23). This is the sanctioned story payoff of the last note (CR2-14); it is never a stat rider |
| 괘종시계 왈츠 (new piece) | waltz | the weekly guest 멈춘 괘종시계 finished at 새것처럼 or better (R2-03), a non-gem headline reward |

  - `bgm_gacha` stays tied to the gacha screen and is not a record.
- **Economy:** cosmetic only: 0 별조각, 0 shards. `MetaRewardType.Track 12` lets any mode pay a record instead of gems.
- **Unlock:** no FeatureId and no card. The picker exists from the start with the two defaults. A new record shows the toast '새 음반이 생겼어요: {곡}' and a NEW pill on the picker row. No red dot (F-04: a new record is neither claimable nor wasting).
- **UI in the new frame:**
  - ≡ 메뉴 › 설정 › 소리 tab: a row '배경 음악' (Label 24) with the current record name and [고르기 ›].
  - It opens `RecordPickerPopup` (`BuildPopup 820×1100`, title '축음기'). A segment [전투][메뉴·잠] (M53 Option, 2×200×64) picks the context. Rows 740×110: a sleeve 88 (the record's cover), name (Title 28), source line (Caption 22, '라라벨의 편지에서'), and [틀기] Primary / '듣는 중' (Gold pill) / locked α0.55 with its unlock hint.
  - Once the 안채 exists (R2-11 or R2-12), ~~its sheet header gets a 64 px gramophone button on the right~~ (CR4-13) the HomePanel's own `PanelHeader` gets the 64 px gramophone button in the TopRight slot SPEC5 §7.3 reserves for it, and the button opens the same popup (§3.2). It cannot go on the shared `ContentArea/SheetHeader`: that is a frozen shell node only 56 px tall, and its `RightSlot` is reserved for the P2 tall sheet (UX-SPEC-IA §4.2). The 안채 cut-away art shows the gramophone in a corner (part of the home background prompt, no extra image).
  - Tapping an owned record cross-fades over 1.2 s on the `AudioSystem` BGM bus. A locked one plays nothing (no spoilers).
- **Art:** the sleeves reuse existing art (story panels, guest portraits, costumes, 라라벨's base form) cropped to 256²: 0 credits. Optional gramophone icon ≈5.
- **Audio:** two new pieces through the AUDIO-SPEC route: '마지막 음' (a music-box loop, 60–90 s) and '괘종시계 왈츠' (3/4, 60–90 s). The other 8 records are AUDIO-SPEC's planned loops. No Higgsfield audio model is used.
- **Save:** `MusicSave { HashSet<string> owned; string battleTrack; string menuTrack }` in `MetaSave` (§3.3). Defaults: the two default records. `Sanitize` drops unknown ids and falls back to the defaults.
- **Tests (`MusicRecordTests`):** each unlock fires once; the selection survives a reload; an unknown id falls back; with no clips (`AudioSystem.HasClips` false) the row stays visible but disabled with '소리가 준비되면 들을 수 있어요'.
- **Size / priority:** S · P2 · wave C, after the AUDIO-SPEC music loops exist.
- **Why P2:** it personalises a screen players keep open for hours, and it gives R2 modes one more reward that is not 별조각. It depends on the audio pipeline, so it cannot come earlier.

---

## 7. Conflicts and offline alternatives

| Reference mechanic | Conflict | What R2 does instead |
|---|---|---|
| Main-stage prestige / rebirth (Egg Inc, TT2, Cookie Clicker, Idle Slayer) | §17: deterministic evolution and "old forms keep value" | Resets only inside side modes: walk depths and talents (R2-10/21) and the Q-06 season (critic: ~~the league (R2-22)~~ deferred). Main progress gets permanent axes instead (R2-04/05). **(critic)** The real prestige substitute is scheduled content that raises the ceiling (R2-36b forms and lineages, R2-37 places) plus a decided late enemy curve (CR2-08) |
| Summon level raising gacha odds (Maple, Slime) | §17 trust rule | PRG-04 milestones (SPEC4) + walk borrowed friend. (critic: ~~새싹 축제 friend ticket~~ rejected, R2-06) |
| Day-gated features (LoM mounts on day 4) | §17 offline binge players | Stage and counter gates only (§3.5) |
| Gear gacha + auto-open (LoM lamp, Crumble oven, JoM dice) | CR-11 deferral; a big RNG layer; the '방치 못하는 방치형' clutter critique | D-02 deferred. Workbench nodes (R2-11) + keepsakes (R2-12) give the slot-upgrade and loot feel deterministically. **(v1.2)** #120 '수선 부적' (a lighter, lineage-shared charm layer from keyed boxes with grade odds) is the same thing: rejected (orchestrator: gear gacha stays rejected) |
| Random stat-line rerolls with locks (Slime traits, Maple abilities, LoM statue) | Gambling-like power sink; trust | D-03 deferred. Keepsake polish is deterministic |
| Random relic discovery (TT2, Melvor) | Luck on permanent power | Guaranteed counts (10th trip, affinity Lv6, floor 20 (critic), guest tier) |
| Server world boss totals and rank prizes | No backend | Personal tiers + weekly meter (R2-03) + a personal-best line. (critic: ~~optional NPC league~~ deferred, D-18) |
| Leaderboards / brackets / tournaments | No backend; fake competition must not deceive | ~~Fictional neighbours with fixed distributions, labelled in-fiction; rank pays cosmetics only;~~ **(critic) personal boards + shareable seeded scores only** (R2-21 꿈길 share codes, R2-22 boards and 명함). The NPC league is deferred (D-18): even labelled, ranked rivals are fake social proof and off-tone |
| PvP arena, guild, co-op raids, chat | No backend | Deferred (D-04; the league is D-18). '함께 막는 밤' NPC neighbour guardians may appear as event flavour later: cooperative, never ranked |
| Plunder / crop stealing (LoM Parking Wars, farm) | No backend; not cozy | Removed. (critic: the pal rests and pots that replaced them are deferred too, D-14/D-15) |
| Ads for buffs, extra keys, double rewards | No ads | **(critic)** SPEC4's chosen free equivalents (§13.10): the cheer (§13.2), the daily gift (NEW-02), the fireflies (NEW-03); plus banking (NEW-04 / R2-07) and the existing free ×2. ~~Free daily uses (R2-28)~~ removed |
| (critic) Spin wheels, roulette, dice-for-rewards (Slime roulette, JoM dice) | Tone (a lottery device in a mending story); future probability-item scrutiny if anything is ever sold | Cut (R2-34 roulette). Push-your-luck stays only as the P2 box hunt, inside the chapter chest |
| (critic) Absence penalties (decay, loose threads, "your pet is sad") | Cozy tone; offline-friendly promise (§17 spirit) | None anywhere. Banking returns missed value instead |
| Paid pass tracks | No money | All tracks free: Q-06, festival rounds, 꿈길 thresholds |
| Device clock tricks | Offline | DailyClock `EffectiveUnix` + `IsTampered` on every claim, refill and timer (research pauses, tickets don't refill) |
| **(v1.2)** Weekly premium-currency faucet with kill-scaled pay and auto-claim after mastery (Arknights Annihilation, #82) | It alone would add 100–150 별조각/week, 40–60% of R2's whole budget (§1.5) | Rejected. "Master once, then claim cheaply" already exists as sweeps (R2-01, R2-07, SPEC4 IDLE-05) and tower auto-retry; a non-gem version would duplicate errand 2 |
| **(v1.2)** Campaign star ratings, two harder trials per stage and a star-funded upgrade tree (Kingdom Rush, BTD6 Monkey Knowledge, #87) | Stars mean grade (`GameEnums.cs:13`); on an endless idle ladder old stages become 3★ by power alone, so ratings measure nothing; the tree is a 4th permanent axis (rule 5 allows 3 in A–B) | Rejected. Restricted replays live in R2-02 trial floors and R2-38 puzzles; the '강철의 밤' rule mix is an R2-38 puzzle template |
| **(v1.2)** Summonable mid-stage hunts and two micro-request chips per stage (Lucky Defense, #88) | ≈70 별조각/week; duplicates NEW-03, R2-15 and errand 1's fleeing target; chips that AUTO completes anyway are HUD noise (M09) with no free band in the new frame | Rejected |
| **(v1.2)** A battle-mode cosmetic currency and its shop (BTD6 trophies, the Arknights contract shop, #91) | Rule 5 (no new currency in A–B; `CurrencyType 3` is reserved for 실밥); its 선택 기억편 ×10 for 120 stamps breaks the shard guard | Rejected. Its cosmetics become R2-09 rewards (lamp and trail tints) |
| **(v1.2)** Side-layer prestige: a 28-day fold that resets tower floors and boss tiers for 50% re-claims and banks permanent star nodes (AdCap angels, Realm Grinder, AFK Journey seasons, #121) | Orchestrator: prestige stays rejected. Re-claiming the tower's ≈700 별조각 at 50% every 28 days adds up to ≈+87 별조각/week; the nodes are a 4th axis with a basket term (rule 7) and another '별' name | Rejected. Runs still reset only inside 등불 길 and 꿈길 (R2-10, R2-21) |
| **(v1.2)** Fictional rivals as medal thresholds or rank ladders (#90, #111, #112) | D-18 and the orchestrator's 'no NPC league'; labelled or not, rival scores are fake social proof | Medals use plain thread tiers. Comparison uses real friends' codes (R2-22) and the player's own past ('지난주의 나'). Neighbours stay story flavour (R2-10 lenders, R2-29 rooms) |

---

## 8. Implementation order (waves after SPEC4; critic-revised)

**Start condition:** SPEC4 wave 4.1 merged. R2 depends on IMetaModule, EventCalendarSystem, CodexSystem, AchievementSystem, AffinitySystem, GachaMilestoneSystem (friend ticket), ChapterChestSystem and `SweepMany`. If SPEC4 cuts a stretch item, the R2 item that leans on it notes a fallback: R2-16 then attaches only to tower / guest / walk.
- **(critic) Extra start conditions.**
  1. SPEC4 **CR-07 `EconomyFlowTests` has run on SPEC4's numbers**, and the late-curve decision of CR2-08 (second enemy taper, rank multiplier or content cadence) is written down. R2-04 and R2-05 constants come from it.
  2. SPEC4 §13 stretch items have a status: NEW-03 and NEW-04 shipped or cut, and PRG-07 shipped or cut. R2-15, R2-07 and R2-10 then either extend them or ship them as SPEC4 specifies.
  3. The audio route is decided. SPEC4 §0.2 rule 7 forbids `mirelo_text_to_audio` / `sonilo_music`, while the 2026-11-04 art memory puts Higgsfield audio in scope. R2 schedules no audio generation until the orchestrator resolves this. SPEC4 route A (procedural) is the fallback.
     - **(v1.2) Decided by the orchestrator:** audio comes from CC0 packs plus original synthesis (AUDIO-SPEC, in progress). Higgsfield SFX and music models are not used standalone. Every R2 cue and R2-40's two new music pieces go through that pipeline, so this condition is met once AUDIO-SPEC's cue table exists.

```
(SPEC4 4.1 merged + CR-07 run; ideally 4.2 verified)
Wave R2-0 ── contracts: save v5 (§3.3, no SPEC4 duplicates), enums (§3.4, renumbered), SideBattleHost +
             time-sliced headless runner, two hub sheet shells, FeatureDefs/cards (§3.5 pacing), red-dot paths
             (§3.6), Q-02/Q-04/Q-05 content hooks (§3.7), EconomyBudgetTests R2 block (gems, 선택 기억편,
             "R2 never touches the 별조각 line"), inert stubs (never throw: meta kill switch)
Wave R2-A (P0) ─┬─ battle modes: Fleeing/Spawner gimmicks (Reforming with errand 3 in B), Errand/GuestShadow setups
                ├─ meta: ErrandSystem (errands 1–2), WeeklyGuestSystem (+ week HP snapshot, 모두 함께),
                │        banking (errands/guest; dungeon = SPEC4 NEW-04) + DailyRounds
                ├─ growth: TechniqueSystem (CR-07 constants), cap breaks + 추억 수놓기, 맞춤 편성
                ├─ UI: 밤 나들이 sheet pages, first-run explainers, result cards, GuestReveal + guest-at-the-door
                │      view, 수선 비법 segment, 돌파 UI
                ├─ content: errand / guest / technique tables, 6 costumes (R2-36a), copy
                └─ art (Higgsfield max): 그림자 까치, nest, 4 guest portraits, stitch badge, 2 hub icons,
                   6 costumes (≈80 credits); audio per the route decision
Wave R2-B (P1) ─┬─ tower (+ titles), errand 3, skill riders (R2-05b sim work)
                ├─ 등불 길 MVP (no currency; 10 reward walks/week)
                ├─ keepsakes (placed effects only) + 안채 hub, workbench (3 branches)
                ├─ festival week (monthly) + moon nights (EVT-01 kinds/schedules, candy rule)
                ├─ lantern on NEW-03, route marks, weekend market, merge, partner + shop name
                └─ content: +1 form per short tree (R2-36b step 1), first new place (R2-37)
Wave R2-C (P2, low art) ── 꿈길 + full walk, boards + share card (no league), affinity extension (letters,
                           gestures, 3 gifts/day), hidden records, storybook fragments with each place,
                           box hunt (inside the chapter chest), storybook puzzles (R2-38)
Wave R2-D (P2, art; top-up first) ── decor + visitors (basket bonuses on 단추/material lines only), postcards,
                                     seasonal packs (lunar date table)
Deferred / cut (Appendix C): photo album, pets, pots, mini-games (roulette cut), attic dig, NPC league,
                             errand 4, codex sets / cape / shop rank, diary (cut), manual presets

(v1.2 additions, by wave)
R2-0  + SideRuleDef / BattleSetup.Rules / SideRuleEffect types (effects land with the tower), the hub-sheet
        decision with WP-IA (UIFrame panel or popup), the chapter-2 card fix (§3.5)
R2-A  + 이번 주 소문 in errands (R2-03), errand style favours (R2-01, FormationBias.favouredStyles),
        놓친 선물 + one-day gift bank (R2-07), costume fitting room (R2-36a)
R2-B  + rule effects with the tower, firefly variants + lantern chip in the new frame (R2-15), neighbour
        lenders + '졸음 길' (R2-10), season costume cadence (R2-36a), place rule + boss mechanic pool (R2-37),
        [모두 엮기] (R2-19), tier gating (R2-11), '수선점 주인' title (R2-09), rumour in tower/walk
R2-C  + 골목 게시판 card, 끝없는 밤 details + friend codes + AUTO replay + share line (R2-22), 오늘 밤의 의뢰 +
        의뢰서 만들기 (R2-38b), anniversaries + lore days + material gifts (R2-23), hidden-record candidates
        (R2-26), chapter letters (R2-27), tints (R2-09), place interactables (R2-37), R2-39 한발 먼저,
        R2-40 축음기 음반
R2-D  + 이웃 수선점 구경, 단추/사탕 decor pricing, favourites, mended visitors (R2-29), accessory layer +
        October masks (R2-33), postcard stickers (R2-32)
Deferred (v1.2): self-chosen risk contracts (D-20), per-place lane geometry + medal grid (D-21)
```

**Done-when, per wave:**
- **R2-A:**
  - A v4 save migrates.
  - Headless == live for a fixed seed in each mode, with AUTO inputs. '결과 바로 보기' fast-forwards the live instance (critic).
  - Ticket banking survives a 3-day jump and refuses a clock rollback.
  - 오늘의 순회 completes in one tap with every toggle on. No frame takes longer than the time-slice budget (critic).
  - The R2 economy block passes: ≤250 별조각/week, ≤30 선택 기억편/week, and the basket 별조각 line is unchanged with every R2 modifier maxed (critic).
  - The need/rec ratio stays 0.80–1.05 through the CR-07 day-60 stage with 수선 비법 + breaks (critic: not a fixed stage 150).
  - `parkedMain` restores the main formation after a kill during a side mode (critic).
  - AutoShot captures: outings sheet, errand page, guest page + reveal + door view, technique segment, cap-break button, one new costume on the field.
- **R2-B:**
  - A walk run is deterministic (same seed, same charm offers and results). Reward walks stop at 10 per WeekKey.
  - Research timers are correct across sleep and resume, and pause on tamper.
  - Keepsake pending grants fire once.
  - The festival week starts and ends on DayKeys. The moon candy bonus never stacks with the starlit walk.
  - Moon dates match a published table for 2026–2028 (±1 day).
- **C/D:** each system ships with its own tests and capture, behind its FeatureId.

**File-disjoint packages for R2-A** (the SPEC4 pattern, one owner per file):
- R2P-0 contracts;
- R2P-1 battle (sim + host);
- R2P-2 meta systems;
- R2P-3 growth systems;
- R2P-4 UI;
- R2P-5 content;
- R2P-6 art and audio;
- R2P-7 verify.

**(critic) Size check.** Wave A as revised is ≈18–24 agent-days (R2-00 L, four M, three S). That is on top of SPEC4's ≈49 agent-days, so wave B starts only after wave A's done-when list is green.
**(v1.2)** The v1.2 deltas add ≈2 agent-days to wave A (≈20–26), ≈3 to wave B and ≈6 to wave C (two new S items, R2-38b's generator, R2-22's codes). Wave A's done-when list gains: a completed unclaimed mission is granted exactly once at rollover; the kept gift expires after one DayKey; the fitting-room preview never equips; errand style favours appear in 맞춤 편성.

---

## 9. Budgets

### 9.1 Economy (별조각 per week at steady state, typical engaged player, week 4+; critic-revised)

Baseline: **SPEC4 v1.1 ≈2,640/week** (§1.5; v1 used SPEC4 v1's 2,500). R2 guard: **≤250** for P0+P1 and **≤80** for P2. 빠른 수선권 = 9 별조각 each (rule 7).

| Source | Steady /week (v1 → v1.1) | One-time | 선택 기억편 /week | Lineage 기억편 /week | Notes (critic) |
|---|---|---|---|---|---|
| R2-01 errands 1–3 | ~40 early → ~20, plus ~5 from 빠른 수선권 | – | 0 | 0 | errand 4 deferred (v1: 42–84 shards of a *chosen* lineage) |
| R2-02 tower | ~50 → **~35** | ~200–300 burst | ~2 | 0 | 7 별조각/floor × 3–6 floors/week |
| R2-03 weekly guest | ~140 → **~90** | – | **~10** (v1 10–25) | 0 | band table trimmed |
| R2-10 등불 길 | ~70 → **~50** | 500 (depth first clears) | **10** (v1 up to ≈115) | **≤70** (v1 up to ≈290) | 10 reward walks per WeekKey |
| R2-13 festival week | ~30 → **~15** | – | ~4 | 0 | one festival a month |
| R2-15 lantern | ~25 → **0** | – | 0 | **≤7** | the firefly is SPEC4 NEW-03's row (≤60); no 빠른 수선권 note; the shard note once per DayKey |
| ~~R2-18 오늘의 덤~~ | ~23 → **0** | – | – | – | SPEC4 NEW-02's row (≤30); the weekend market is a sink |
| ~~R2-06 carnival~~ | – | ~~100 + friend ticket~~ → **0** | – | – | SPEC4 NEW-01's row (≤350) |
| R2-36a / R2-36b / R2-37 codex entries | 0 | +180 (6 costumes) · ≈+300 (6 forms) · +30 per place · ≈+100 per new lineage | 0 | 0 | SPEC4 Q-05 `CodexConfig` values |
| Q-04 R2 lines | – | ~~≈1,600~~ **≤1,000** lifetime | – | – | trickle ≈20/week over a year |
| Banking (R2-07) | 0 | – | – | – | returns missed value; adds none for daily players |
| **P0+P1 total** | ~~≈378~~ → **≈235 (+8.9%)** | | **≈26** (guard ≤30) | **≤77** (guard ≤80) | guard ≤250 |
| ~~R2-22 league (P2)~~ | ~~≤80~~ → **0** | | | | deferred (D-18) |
| R2-21 꿈길 (P2) | ≤60 | | | | P2 guard ≤80 |
| **(v1.2)** R2-23 anniversaries (P2) | ≈7 in year 1 | 50 per lineage at 1년 (350 for 7) | 0 | 0 | 100일/200일 pay 별빛 사탕 and gift days only |
| **(v1.2)** R2-38b 오늘 밤의 의뢰 (P2) | 0 | titles | 0 | 0 | 단추 minutes, commons, 기억의 실, ≤100 별빛 사탕/week |
| **(v1.2)** R2-22 끝없는 밤 (P2) | 0 | – | 0 | 0 | ≤100 별빛 사탕/week; medals and tints are cosmetic |
| **(v1.2)** R2-26 hidden records | – | 150, inside the Q-04 R2 ≤1,000 line (CR4-04: SPEC5 v1.0 had tightened that row to ≤ 850 with 800 used; v1.0.1 restores ≤ 1,000) | – | – | titles |
| **(CR4-04)** R2-27 동화책 조각 pages (P2) | 0 | 50 per page (one page per place: 250 for places 2–6); this row was missing | 0 | 0 | the chapter 1–5 letters pay nothing |
| **(CR4-04)** R2-36b step 2 new lineages (P2 cadence) | affinity ≈ +38 per lineage in its first months | codex ≈ +100 and affinity 540 per lineage | 0 | 0 | SPEC4's affinity row (≤ 4,000 for 7 lineages) must be re-based per lineage (≤ 571); nine lineages make 4,860 |
| **(v1.2)** R2-39 / R2-40 | 0 | – | 0 | 0 | in-battle 잔향 and 불빛 / cosmetic records |
| **(v1.2)** R2-07 kept gift + 놓친 선물 | 0 | – | 0 | 0 | returns missed value inside NEW-02's ≤30 and SPEC4's journal row |
| **(v1.2)** R2-36a season costumes (on SPEC4's Q-06 row) | **−≈12** while season costumes remain | +30 codex per costume | **−≈7.5** | 0 | replaces Q-06's fallback; lowers the baseline, not an R2 line |
| **(v1.2) P2 total** | **≈67** (guard ≤80) | | 0 | 0 | 꿈길 ≤60 + anniversaries ≈7 |

- **Result (critic):** ≈2,640 → ≈2,875/week, so a 60-pull pity (5,400) every ≈1.9 weeks, the cadence v1 aimed for.
  - **(CR4-01, CR4-02) Measured against the real code, the baseline is higher, and R2 moves money through other systems.**
    - The progression harness measures 2,399/week from the pre-SPEC4 sources alone, against SPEC4's modelled ≈1,825. The basket credits ≈26–39 gem-hours a day, not 18.
    - With R2-04/05 the measurement is 3,609/week (no C-01).
    - The shipped shop-level mail (별조각 50 per 10 levels) pays 350–550 a week in weeks 2–4.
    - SPEC4's ch6+ chapter chest (150 each) would pay ≈975–1,575 a week at R2-04's chapter pace.
    - None of these is an R2 row, but R2 causes all of them. PHASE4-ROADMAP RD-13 cuts the chest to 100 on every fifth chapter, and SPEC5 §4.4 now uses measured bands (week 4: 2,800–3,800, week 8: 2,600–3,600).
  - v1 as written would have been ≈3,020/week plus the basket leaks below, a pity every ≈1.6–1.8 weeks.
  - Its 선택 기억편 income would have been ≈4–6× today's.
- **Leaks closed (critic, rule 7):** v1's R2 basket modifiers would have raised the capped 별조각 line, and none of them was in the table:
  - titles +3%, box-hunt perfects +10%, workbench 적립 +10% and cap +2 h, the R2-26 cape cap +5%, decor +10% and visitors +9%, keepsake owned +0.5%, moon nights +10%;
  - plus the workbench's free quick idle (+2 h of all lines a day) and lantern 빠른 수선권 notes.
  - Together that is ≈+250–350 별조각/week on top of the table, about as much as all of R2's listed income.
- **Shards:** inflows rise ≈+26 선택 and ≤+77 lineage 기억편/week. New sinks: cap breaks 400 and 추억 수놓기 600 per lineage, so 7,000 in total across 7 lineages.
- **Knobs** (all in content): errand reward scale and tickets, tower boss / trial gems, guest band table, reward walks per week / weekly chest, festival round gems, the market's 선택 기억편 price.
- **(v1.2) The 51 unseen findings, priced.** Taken at face value they proposed about **+1,300 별조각/week**: the tower-defense sweep's own total of +685 for its modes (#78: requests 155, clean-up 150, escort 100, hunts 70, raid 60, parade 50, endless 50, contracts ≈50), the daily-seed audit's ≈210 (#101), the cozy guardrail's ≈300 (#77: drifters ≤70 + weekly guest ≤200 + the rest) and up to ≈87 from the side-layer fold (#121). On top came a stamp currency, a 실밥 economy and selectable-shard shops. v1.2 keeps their play and pays it in 단추 minutes, materials, 별빛 사탕 and cosmetics:
  - P0+P1 stay at **≈235/week** (guard ≤250); P2 rises from ≤60 to **≈67** (guard ≤80); 선택 기억편 stays ≈26 (guard ≤30) and lineage 기억편 ≤77 (guard ≤80).
  - Candy: the v1.2 P2 sources add up to ≈200 별빛 사탕/week. The event shop sells 여행 가방 조각 for 20 사탕, so CR-07's material projection (R2-11 trigger) must count this candy as ≈10 epic materials a week.
  - SPEC4's baseline gets slightly smaller, not larger: R2-36a's season costumes replace Q-06's tier-30 fallback for about six seasons.
  - The `EconomyBudgetTests` R2 block gains: R2-39 grants no currency; R2-38b, R2-22 and R2-40 grant no 별조각 or shards; the rollover grants at most what the period had earned.
- `EconomyBudgetTests` asserts each line, the totals and the two shard guards (like SPEC4 §11.2). It also asserts that **the basket 별조각 per-hour is identical with every R2 modifier at max**.

### 9.2 Art (Higgsfield `gpt_image_2_5`, quality **max**, one asset per image ≈5 credits; reuse first; critic-revised)

| Wave | New images (count) | Credits (≈) |
|---|---|---|
| R2-0 | 2 hub icons | 10 |
| R2-A | 그림자 까치, nest, 4 guest portraits, stitch badge, **6 costumes (R2-36a)** (+optional 4 errand banners, 7 technique icons) | 65 (+55 optional) |
| R2-B | dust-king crown, shelf-office bg, walk strip bg, shadow merchant, home + workbench bgs, 16 keepsakes, shelf bg, 5 festival banners, moon chip, lantern chip, title plate, **6 new forms (R2-36b)**, **first new place: bg + boss (R2-37; its 2 enemies reuse 까치 and nest)** (≈40 images; +8 guests, 15 keepsakes, 8 dark charms, stall banner optional) | ≈200 (+160 optional) |
| R2-C | frames and stickers, envelopes (≈15), box + mimic (2) | 85 |
| R2-D | visitors 18, decor 20–40, postcards 24, seasonal packs (≈15 per pack × 3). ~~pets 6, pots~~ deferred | ≈550+ |
| Cadence (post-launch) | per new place ≈25; per new lineage ≈20 + 1 costume 5 | ≈50 / month |
| **(v1.2)** R2-A | none (the fitting room and the other wave A deltas reuse art) | 0 |
| **(v1.2)** R2-B | firefly variants ×4 (optional), one season costume per season (R2-36a), R2-37 boss mechanics (no new art) | ≈20 optional + ≈5 per season |
| **(v1.2)** R2-C | R2-37 interactable props (≈5 per place, optional), gramophone icon (optional), R2-39 lantern (reuses `Icons/lamp`), record sleeves (crops of existing art), tints (palette only) | ≈5–15 |
| **(v1.2)** R2-D | accessories ×12 (one per pack), October mask overlay, 3 neighbour rooms, mended-visitor tints | ≈80–95 |

- The balance was ≈530 credits on 2026-11-04 after the user's top-up, and SPEC4's own art comes first.
- (critic) R2-0 + A + B core ≈275. That fits only if SPEC4's max-quality pass stays under ≈250. Check `balance` before wave B starts, and tell the user then, not mid-wave.
- **Wave D needs another top-up.** Tell the user before generating it, per the art memory.
- **(v1.2) Balance and decisions.** `ART-UPGRADE-PLAN.md` §1.3 measured 530.38 credits before the current FX and art jobs; only its first 235 fit, and "everything at max" needs ≈675 more, which the user has been asked about. v1.2 adds **no wave A art** and keeps every new image optional or behind a fallback. **The udu and lala lineages are not restyled** (orchestrator decision, ART-UPGRADE-PLAN OPT): every R2 asset for them (R2-36a costumes, R2-36b forms, season costumes, accessories) uses the current forms as `image_references`.
- Every asset has a code fallback (listed per system), so no system waits on art to compile or play.
- **Audio:** ≈18 new cues routed through the SPEC4 AudioSystem table: errand start/end, shadow-magpie flutter, nest break, floor clear, layer peel, reveal fanfare, moth hiss, lantern full, charm pick, crest light, research done, keepsake place, costume reveal. Box open and mimic gulp move to wave C.
  - **(critic)** SPEC4 §0.2 rule 7 says not to call `mirelo_text_to_audio` / `sonilo_music` (the tool calls them game-pipeline-only), while the 2026-11-04 memory says audio through them is in scope. No R2 audio is generated until the orchestrator settles this. SPEC4 route A (procedural) is the fallback.
  - **(v1.2) Settled:** CC0 packs + original synthesis (AUDIO-SPEC); Higgsfield SFX and music models are not used standalone. v1.2 adds the cue `call_early` (R2-39) and two music pieces for R2-40 ('마지막 음', '괘종시계 왈츠'); R2-40's other eight records are AUDIO-SPEC's planned loops.

---

## Appendix A. Already fully built (dropped), with evidence

| # | Reference system | Evidence in code |
|---|---|---|
| A-1 | Daily/weekly missions + completion chests + time vouchers (K33) | `Systems/Meta/MissionSystem.cs:43`; `Editor/Content/MetaContent.Missions.cs` (11 daily = 170 pts, 8 weekly = 140 pts, chests 20/40/60/80/100; 빠른 수선권 / 두 배 수선권 vouchers = `MetaRewardType` 3/4) |
| A-2 | Login attendance, unit on day 7, 28-day board (K34) | `Systems/Meta/AttendanceSystem.cs:32`; `MetaContent.Attendance.cs` (새싹 7 with 루즈리 on day 7; monthly 28, day 28 = 별조각 500 + 오르골 태엽 2) |
| A-3 | Idle cap + double claim + quick idle + return screen (K35) | `Systems/Meta/IdleChestSystem.cs:58`; `MetaContent.IdleChest.cs` (cap 8 h → 12 h at 2-1, quick idle 2 h at 0/50/100, free ×2 once a day, return popup ≥600 s); `UI/Meta/ReturnPopup.cs` |
| A-4 | Timed boss gate + repeat farming + auto challenge (K36) | `Core/SaveData.Battle.cs:55,62` (`challengeTarget`, `autoChallenge`); `FeatureId.AutoChallenge = 23` (2-1) |
| A-5 | Staggered feature unlocks (K38) | `Systems/Meta/FeatureGateSystem.cs:22`; `MetaContent.Features.cs` (25 FeatureIds; Events = 3-1 or install day 3) |
| A-6 | Gacha pity + rate disclosure (K37, partly) | `Editor/Content/GachaContent.cs` (100/900, 45/33/15/6/1, pity 60 → Legendary); GachaPanel pity text. Milestones = SPEC4 PRG-04 |
| A-7 | "Level pass" rewards on a metric the player already pushes (K10) | `Systems/RewardMailSystem.cs:23–32` (별조각 50 every 10 shop levels); `Systems/Meta/GuideQuestSystem.cs:22` (30 steps + repeat); SPEC4 PRG-04 (pull ladder), PRG-06 (chapter chests), Q-04 (stage tiers), Q-06 (free track) |
| – | 05:00 daily clock + rollback guard (foundation for every R2 timer) | `Systems/Meta/DailyClock.cs:26,28` (ResetHour 5, RollbackTolerance 300 s, `WeekKey`, `EffectiveUnix`) |

## Appendix B. SPEC4 items these findings touch (no duplication)
- **Q-04** 수선점 기록: R2 only appends lines and titles (§3.7).
- **Q-05** 도감: owned-form bonuses already cover K7's "owned effect". R2 appends tabs [기념품] [손님]. (critic) Q-05 stays the **only** always-on collection bonus layer: R2-12 has placed effects only, and R2-26's set pages are deferred (D-19). +6 costumes, +6 forms and new enemies add codex rows (§9.1).
- **CR-10** 호감도 (levels, 기억 조각 lines, frame, pose, tap-to-pet): R2-12 adds the Lv6 keepsake. Gifts, letters, badges, gestures, requests and anniversaries are R2-23.
- **PRG-04** 소환 마일스톤 + 친구 선택권: ~~R2-06 reuses the ticket reward type~~ (critic: R2 grants no 친구 선택권; R2-06 is removed).
- **PRG-05** catch-up: R2-10 borrowed friend uses the same level formula.
- **PRG-06** 잃어버린 상자 / 챕터 상자: R2-16 (P2) adds an optional board *inside* the chapter-chest popup, never replacing it and never as a third popup (critic).
- **PRG-07** 잔향 축복 (B.4 pool): R2-10 charms reuse its 12 entries. (critic) PRG-07 is a SPEC4 stretch item, and SPEC4 §13.10 wants it to "test the pick feel first". If it is cut, R2-10 builds the pick popup.
- **CR-05** 동화책 ch1–5: unchanged. R2-27 covers ch6+.
- **CR-06** 다시 만난 날: unchanged. R2-07 banking covers short absences; CR-06 covers 72 h+.
- **EVT-01** calendar (weekend expedition, Wednesday dungeon, starlit walk 1–14, night shop): R2-13/R2-14 add kinds and schedules. The Wednesday bonus stays unbanked.
- **Q-06** free season track: R2 side modes may add XP sources later (R2-21). No second track.
- **IDLE-04** duration ladder + ResendAll: R2-17 adds route marks only (critic: lamp-post marks, not stars).
- **IDLE-05** `SweepMany` (floor 5): R2-07 builds 오늘의 순회 on top of it.
- **NOTI-01, SAVE-01, EVT-02, CR-07, CR-09:** R2 appends notification kinds, uses the share sheet for 명함 / 꿈길, extends the economy test, and logs new counters. (critic) CR-07 is now a **start gate** for wave A (§8), not just a test to extend.
- **(critic) SPEC4 v1.1 §13 items R2 must not rebuild:**
  - **NEW-01 첫 주** (§13.6) replaces R2-06.
  - **NEW-02 오늘의 작은 선물** (§13.7) replaces R2-18's free gift.
  - **NEW-03 길 잃은 반딧불** (§13.8) is R2-15's firefly; R2-15 adds the lantern and moth only.
  - **NEW-04 입장권 모아 두기** (§13.9) is the dungeon bank; R2-07 extends the pattern to errands and the guest.
  - **§13.2 친구들의 응원** replaces R2-28.
  - **§13.4 basket ceiling (C-01)** is protected by rule 7.
  - **§13.1 boss hints** are the pattern for R2-37 bosses.
- **(critic) SPEC4 §13.10 phase-5 table, reconciled.**
  - Weekly boss → R2-03 (P0, override with reasons).
  - Research tree → R2-11 (P1, trimmed, CR-07 material trigger).
  - Roguelite → R2-10 (P1, after PRG-07).
  - Garden → deferred (D-15).
  - Presets → R2-08 replaced by 맞춤 편성; manual memos wait for >10 lineages.
  - Relics → R2-12 (P1, no owned effects, after the Q-05 calibration).
  - §13.10's rejected list (summon level, gear gacha, prestige, server features, ad boosts, free daily summon, the Lucky Defense merge loop) is respected everywhere.
- **(v1.2) SPEC4 items the 51 unseen findings touch.** Nine of them re-derive a SPEC4 item; R2 rebuilds none.
  - **Q-04** (#98, #85): Q-04 as specced. R2-26 takes the hidden-record ideas; #98's three extra counters are used only where no existing counter or flag fits.
  - **Q-05** (#99): the same discovery numbers and collection bonuses. Its 15 themed sets with +2% bonuses and the duplicate-star vault stay deferred (D-19).
  - **CR-10** (#100): the same levels, pets and rewards. R2-23 makes loved materials the first gift source.
  - **EVT-01** (#102): the same calendar. Korean seasonals are R2-33's lunar table; October masks are added there.
  - **NEW-02** (#73): R2-07 keeps one missed gift for a day.
  - **NEW-03** (#104, #72): the same firefly. R2-15 adds modifier variants; SPEC4's rules, cap and rewards stay.
  - **SAVE-01** (#105): import, preview, backup and restore are all specced. **Suggestion for the SPEC4 owner, not an R2 item:** a quiet backup reminder every 14 DayKeys (a mail with [세이브 코드 복사], no dot), the only protection against a lost phone without a backend. QR scanning is out (no camera plugin).
  - **Q-06 / PRG-04** (#106): the same free tracks. R2-36a's season costumes fill tier 30 from season 2; #106's stage-stamp track stays covered (D-08).
  - **CR-05** (#113): the same storybook and map. R2-27 mails a one-line letter with each page.
  - **NOTI-01** (#117): identical; v1.2 adds no notification kinds.
  - **MissionSystem rollover** (#103, existing code): R2-07's 놓친 선물 grants completed rewards before `Reset`.
  - **EVT-02** (#103): coupon codes as specced.

## Appendix C. Deferred / not recommended

| ID | Item | Why | Revisit when |
|---|---|---|---|
| D-01 | 깊은 밤 hard tier (K4) | Our main ladder is endless, so replaying chapter 1 at ×8 HP adds little. The tower's deep floors + trial floors give the late challenge. **(v1.2)** #93 (Kingdom Rush Veteran/Impossible, Lucky Defense Hell) adds '새벽 직전' at ×8 HP + 2 rules; its 별조각 15 per stage + 100 per chapter would add ≈2,500 lifetime | Chapter content stops at a fixed end |
| D-02 | 분실물 꾸러미 gear gacha + auto-dismantle (K16) | CR-11 equipment deferral; the biggest new RNG layer; inventory clutter ('방치 못하는 방치형'); XL | Never as gacha; a deterministic "부자재" slot layer only if R2-11 proves too thin |
| D-03 | 소원 우물 random stat lines with locks (K20) | Gambling-like reroll sink on permanent power; trust (§17 spirit) | – |
| D-04 | 그림자 대련 standalone arena (K26) | Duplicates R2-22's ladder with a less cozy fiction | – |
| D-05 | 손때 숙련 form mastery (G7) | A 4th per-character axis next to lineage level, resonance and affinity (CR-10); Q-05 + R2-05 already keep old forms useful. **(v1.2)** #86 (Arknights Trust and modules, BTD6 tower XP, Rush Royale talents) adds ranks 1–10 with 별조각 10 × rank (3,850 lifetime for 7 lineages), switchable habit talents and unit trials. Still deferred; the switchable-habit idea is an option for R2-05b at 공명 9 | CR-09 shows old forms unused at ch4+ |
| D-06 | 달 사탕 수선 도구 (G8) | A third permanent stat tree next to R2-04 and R2-11; its 24 h ripening rhythm moved to pots (R2-31, now deferred too, D-15) | – |
| D-07 | Separate side-mode season 계절 수선록 (G20) | Would be a second season track next to SPEC4 Q-06 | R2-21 charm themes are not enough |
| D-08 | 성장 수첩 multi-tab passes (K10) | Already covered (A-7) | – |
| D-09 | 지킴이 받침 standalone pedestals (K18) | Folded into R2-11 골무 per-direction nodes; skins become R2-22 cosmetics | – |
| D-10 | 빌린 친구 대회 standalone draft (K6) | Folded into the R2-10 borrowed friend and R2-21. **(v1.2)** #96 '제비뽑기 밤' (Rush Royale Rush for Glory, Random Dice solo, Arknights IS recruiting: a normalized-level 1-of-3 draft from all 29 forms) stays folded: R2-10 lends unowned forms, R2-38b gives a daily puzzle, and a third run mode would add daily side fights | – |
| **D-11 (critic)** | 편성 메모 manual presets (v1 R2-08) | SPEC4 §13.10: few combinations with 7 lineages. Replaced by 맞춤 편성 (auto per mode + restore) | Roster passes 10 lineages (R2-36b) |
| **D-12 (critic)** | 큰그림자 행진, errand 4 (shards of a chosen lineage) | 42–84 targeted shards/week; for 라라벨 ≈105 pulls' worth a week. It turns the Mythic chase into a ticket chore | Shards saturate (CR-09: median lineage past 추억 수놓기 20) or the roster passes 12 lineages; then pay **선택 기억편 ≤10/week** instead |
| **D-13 (critic)** | 추억 사진첩 photo album (v1 R2-25) | L; untested on-device capture of the uGUI battle; 150 JPGs of storage; low retention leverage before launch. **(v1.2)** #114's 그림일기 (automatic 540×540 battlefield snapshots at milestones, 60 kept, with captions) joins this deferral for the same capture reason | The SAVE-01 share sheet proves out on device, and a capture spike succeeds |
| **D-14 (critic)** | 주머니 친구 pets (v1 R2-30) | A third combat layer next to guardians, echoes and the lamp; L with art; another 8 h timer. **(v1.2)** #119 '고쳐 준 길손들' (Legend of Mushroom avians, Seven Knights pets) is the better fiction if pets ever return: enemies mended after enough kills, reusing enemy art. Its daycare currency (별빛 부스러기) and the shop-HP account level are rejected; for now mended enemies appear only as R2-29 visitors | After R2-36b; if the CR-09 day-30 cohort lacks a collection goal |
| **D-15 (critic)** | 창가 화분 pots (v1 R2-31) | SPEC4 §13.10 Stone Age warning; a third timer garden; its 24 h crop paid chosen-lineage shards (the D-12 leak) | D7/D30 data asks for a second come-back timer |
| **D-16 (critic)** | 다락방 발굴 attic dig (v1 R2-35) | A stamina timer (핀셋 +1/20 min); a fifth come-back timer; a 별조각 trickle. **(v1.2)** #115 '다락방 뒤적이기' (Legend of Mushroom's mine) is the same: battery regen 1 per 20 min to 12, 별조각 1–3 and 빠른 수선권 finds (≈9 별조각 each), and a 비법 쪽지 research currency. Still deferred | – |
| **D-17 (critic)** | 미니게임 (v1 R2-34): fishing, board game. **The roulette is cut** | Daily-capped chores; a spin wheel is a lottery device | The board game can return as an EVT event shell (cosmetic + candy only) |
| **D-18 (critic)** | 밤시장 리그 NPC league (v1 R2-22 part) | Ranked fictional rivals with promotion and demotion: competition anxiety and fake social proof in a cozy mending game | A real backend exists (then real, opt-in friend boards) |
| **D-19 (critic)** | 도감 세트 pages, 완성 망토, 수선점 등급 (v1 R2-26 parts) | A third collection-bonus layer next to Q-05 and R2-12; the cape's basket cap touches the 별조각 line; '수선점 등급' is a near-duplicate name of '수선공 등급' / '수선점 레벨'. **(v1.2)** #99 (Monopoly GO albums, Neko Atsume's Catbook) adds ≈15 themed sets with 별조각 50 + a permanent +2% each and a duplicate-star vault (선택 기억편 20 at 100 stars, a costume token at 300); the bonuses and the vault stay deferred (shard guard, a third bonus layer, and 'stars' again) | After Q-05 + R2-12 tuning, as cosmetics-only set rewards |
| **D-20 (v1.2)** | 수선 계약서 self-chosen risk seasons (#92; Arknights Contingency Contract, Endfield) | L; a third seasonal calendar next to Q-06 and R2-13 (the critic already thinned the calendar); it needs the rejected stamp currency and shop; it serves a hardcore cohort the cozy core does not target before launch. The R2-00 rule table, R2-22 boards and R2-38b's custom codes keep it buildable later as content | CR-09 shows an end-game cohort (≥10% of day-60 players past tower floor 60 or depth 10); then a 14-day season paying cosmetics and 별빛 사탕 only |
| **D-21 (v1.2)** | Per-place lane geometry and a medal grid (#97; Kingdom Rush maps, BTD6 per-map medals) | Closed lanes, tunnels and spiral approaches change `BattleGeometry` (5 slots, SpawnRadius 2.5), every pad and keep-out validation (UX-SPEC-IA §1.3, Appendix B) and the FTUE coach rects; XL with art; the medal grid needs the rejected star ratings (#87). R2-37 keeps the light part: one place rule, one optional interactable, a boss mechanic pool | A place needs a mechanic the rule table cannot express |
| **Cut (critic)** | v1 R2-06 (→ SPEC4 NEW-01), R2-18 free gift (→ NEW-02), R2-28 (→ SPEC4 §13.2 cheer), R2-24 diary (name clash with 수선 일지) | Duplicates or near-duplicate names | – |
| **Rejected (v1.2)** | #82 대청소의 밤 · #87 별 세 개와 두 시험 + 수선점 설계도 · #88 그림자 사냥 + 밤손님 부탁 · #91 기념품 가게 + 밤길 도장 · #120 수선 부적 · #121 계절 갈무리 + 꿈길 산책 | Economy guards, rule 5, the grade-star rule, the orchestrator's decisions (gear gacha and prestige stay rejected); reasons in §7 | – |
| §17 | Main-stage prestige · summon level · day gates (inherited) | unchanged | – |
| PRG-08 | 수선점 공명 level sync | SPEC4 §8.7 "do not build" | – |

## Appendix D. Sources by sweep
- **kr-kiugi:**
  - namu.wiki (레전드 오브 슬라임, 메이플 키우기/시스템, 버섯커 키우기, 세븐나이츠 키우기); levelwinner Legend of Slime guide; theriagames LoM dungeons and Abyssal Portal; Deconstructor of Fun "The Magic of Legend of Mushroom" (2024);
  - talkandroid LoM guides (Shroomie Farm / Prayer Statue, Parking Wars); alucare LoM rush events; gmspot LoM mine/Tech Park; pocketgamer LoM and 7K updates; 148apps 7K; vortexgaming 7K modes; insight 7K 신기루의 탑/투기장;
  - invenglobal Maple Journey's Treasure, Pink Bean, Stone Age 100-day; bluestacks (Maple, 7K, JoM); byline.network (Crumble 2026-08, Stone Age 2026-03); inews24; v.daum; mt.co.kr (LoM critique); Sensor Tower (Maple Idle $100M); haveagood-holiday 7K anniversary; animenewsnetwork 7K 1-year.
- **global-idle:**
  - theriagames Capybara Go (?p=58587/66883/75018/85174/90668/109448); meowdb Capybara references; capybara-go.game-vault wiki; gamingonphone; gamigion ?p=24377; mechanics-playbook Capybara case study;
  - afk.guide labyrinth guides; gfinityesports Honor Duel; prydwen AFK Journey (Dream Realm, returnee); twinfinite Dream Realm; gameleap AFK Journey seasons; mobi.gg Aspen Dungeon;
  - oslink The Tower guide + game-vault Tower wiki (tournament, perks, events) + the-tower.notion perks; TT2 wiki mirrors; cookieclicker wiki (sugar lumps, garden, golden cookie, seasons); sayolove NGU Yggdrasil; ngu-idle wiki challenges; egg-inc wiki (prestige, prophecy eggs); howtofarm Egg Inc; melvor wiki (mastery pool, ancient relics, completion cape); idleslayer wiki (minions) + Steam threads; idlechampions patrons; gamedeveloper "Exploring motivation" (Realm Grinder) + gamertweak trophies;
  - appgamer Idle Miner events; gameplay.tips / fandom AdCap badges; game8 / automaton / bulbapedia Pokémon Sleep; pocketgamer.biz Gold & Goblins 5 years; pcgamesn Slay the Spire daily; tap-titans fandom tournament.
- **cozy:**
  - animalcrossingpocketcamp wiki; hellokittyislandadventure wiki (gifting); stardewvalleywiki friendship; serebii / bulbapedia Pokémon Sleep (befriending, badges, Snorlax, Good Sleep Day); pokemon.com Good Sleep Day 2024-01; levelwinner Usagi Shima / Cats & Soup; App Store 고양이 별장, Neko Atsume 2, Usagi Shima, Tsuki;
  - techradar / imore / wikipedia / tvtropes Neko Atsume; nwzrise Cats & Soup notices; pocketgamer Cats & Soup; appquantum Animal Restaurant deconstruction; talkandroid Animal Restaurant; apps.apple Penguin Isle; wikipedia + thechinaproject Travel Frog; apkmirror Battle Cats; mejoress Battle Cats user rank; animalcrossingworld / macrumors Pocket Camp Complete; lostgarden "Cozy games" (2018); gamedeveloper "Designing for coziness".

---

## Critic revisions (v1.1, 2026-11-04, role: critic)

**Scope of the review.**
- **Read in full:** IDLE_RESEARCH2 v1, plus SPEC4 **v1.1**: §1, §2, §11.2, §12, and the new §13.1–§13.10, §14 and Appendix C, written at 00:58, after v1 (00:49).
- **Also read:** `IDLE_RESEARCH.md` §3, §17 and Critic additions; `research2/{kr_kiugi,global-idle}-notes.md`; the art, IA, Higgsfield and standing-order memories.
- **Code, read-only:**
  - `Editor/Content/{MetaContent.IdleChest, MetaContent.Power, FormStats, CharacterContent*, GachaContent}.cs`
  - `Scripts/Core/GameEnums.cs`, `Scripts/Core/SaveData.Meta.cs`
  - `Scripts/Systems/{Meta/IdleChestSystem, Characters/CharacterSystem, Battle/BattleSimulator, Battle/DungeonSystem, Battle/FormationSystem}.cs`
  - `Art/{Characters,Costumes,Enemies,Battle,Story}`
- **Backup:** v1 is saved as `research2/IDLE_RESEARCH2.v1.bak.md`. No project file was edited.

**Verdict.** v1 is a strong research synthesis, and its best ideas fit the IP well: the weekly guest, techniques plus breaks, the errand gimmicks, keepsakes and the roguelite. As a plan it had four structural problems:
1. It rebuilt five systems SPEC4 v1.1 already owns.
2. Its economy leaked far more than its table showed: 별조각 through basket modifiers, and especially 선택 기억편.
3. Its growth math cannot reach its own target.
4. It added ≈35 meta systems while the actual collection is 7 lineages and 10 costumes, all on 짝짝이.

The fixes below keep the user's "build everything that's missing" intent. 27 of the 35 systems stay and 3 missing ones are added. They make the plan buildable and keep the economy sound.

### Changes made (each is tagged **(critic)** in the body)

| # | Type | Finding | Fix (where) | Evidence |
|---|---|---|---|---|
| CR2-01 | Duplicate | R2-06 새싹 7일 축제 is SPEC4 NEW-01 '첫 주': same sources, 4 tasks/day, 180 pts, 30–150 chests, 14 DayKeys. Its final 친구 선택권 hands every week-1 player the only Legendary (우두커니) | R2-06 removed and the friend-ticket prize rejected (§0, §2.2, §3.3–§3.6, R2-06 stub, §9.1) | SPEC4 §13.6, C.2; §2.11 #4 ("every ≤Epic lineage is a starter or the attendance friend") |
| CR2-02 | Duplicate | R2-18's free gift = NEW-02; R2-15's firefly = NEW-03; R2-07's dungeon bank = NEW-04; R2-28 = §13.2 cheer | Each keeps only the delta: weekend market, lantern + moth, errand/guest banking, nothing (R2-07/15/18/28, §2.2, §7) | SPEC4 §13.2, §13.7–§13.9, §13.10 "free equivalents of ad boosts" |
| CR2-03 | Contract collision | `MetaSave.firefly` (SPEC4 `FireflyDaySave firefly`), `DungeonSave.bankedTickets/lastRefillDayKey` (SPEC4 `ticketBank/bankDayKey`), `SproutSave`, `ShopExtraSave.freeDayKey`; `EventScheduleType` MoonPhase **3** = SPEC4's reserved `Dates = 3` | Fields removed or renamed (`LanternSave`, `MarketSave`); enums renumbered (FeatureId 27–37, MoonPhase 4, MonthRotation 5, DateList 6); no new `CurrencyType` (§3.3, §3.4) | SPEC4 §2.1 critic rows, §2.2 `Dates = 3` |
| CR2-04 | Priority alignment | SPEC4 §13.10 already triaged six v1 systems with "revisit when" triggers: weekly boss, research, roguelite, garden, presets, relics. v1 ignored them | Each reconciled in its section and in Appendix B: weekly boss kept P0 with reasons; research trimmed with a CR-07 material trigger; roguelite after PRG-07; garden deferred; presets → 맞춤 편성; relics without owned effects | SPEC4 §13.10 table |
| CR2-05 | Economy (baseline) | v1 used SPEC4 v1's 2,500/week; v1.1 is **2,640** (basket ceiling C-01, gift, fireflies). v1 also double-counted firefly 25, gift 23 and carnival 100 | Baseline fixed; guard **≤250/week** P0+P1, ≤80 P2. Revised total ≈235, so a pity every ≈1.9 weeks (§1.5, §9.1) | SPEC4 §11.2 revised projection; risk #3 (+45% already) |
| CR2-06 | Economy (shards) | 선택 기억편 is worth ≈112 별조각 each for a Mythic (0.8 라라벨 shards per pull). v1 paid it far too freely: walk up to ≈115/week + ≈290 lineage shards; errand 4 42–84 *chosen-lineage* shards; pots likewise; market 5 for 150. Its own table said "~30" | Shard guards (≤30 선택, ≤80 lineage per week); 10 reward walks per week; errand 4 and pots deferred; guest bands trimmed; market 250, 1 per weekend (§1.5, R2-01/03/10/18, §9.1) | `GachaContent` rates and shard values (§1.5 table); R2-10 v1 reward list × 3 lanterns/day |
| CR2-07 | Economy (basket leak) | Quick idle pays every basket line, gems included (`IdleChestSystem.cs:44`), and the gem line is `Line(Gem(3))` (`MetaContent.IdleChest.cs:31`). v1 stacked ≈10 R2 "basket rate / cap / night / moon" modifiers, a free extra quick idle a day and 빠른 수선권 lantern notes on top: ≈+250–350 별조각/week, none of it in the table | Rule 7 (§1.2): R2 modifiers affect 단추/material lines only; 빠른 수선권 = 9 별조각; the test asserts the gem line is unchanged with every modifier maxed. Removed: title/box-hunt/keepsake/cape basket bonuses, the workbench free quick idle, the lantern ticket note | SPEC4 §13.4 C-01 ceiling and the `BasketGemLine_*` test |
| CR2-08 | Correctness (math) | R2-04 cannot keep need/rec at 0.80–1.05 to stage 150. Row levels grow ≈1.28 per stage (cost ×1.10 vs clear gold ×1.13), so +1%/level is linear and the ×1.02 rank is ≈×1.004/stage, while the need grows ×1.0741/stage. Today's ceiling (≈1,017 × 8.92 × ≈2 × 1.5 ≈ 27–35k) is reached at ≈stage 65–70; R2-04 + R2-05 move it to ≈95–105. Tower floors 50+ (≈stage 81+) would be walls | CR-07 becomes a start gate and picks the late curve: second taper / bigger rank multiplier / content cadence (R2-36b). The tower uses s(f) = 6 + k·f from CR-07, with milestones at 20/40/60 (R2-02, R2-04, §8) | `MetaContent.Power.cs:20–31` (1.0741; need 21,832 at 6-10); `FormStats.cs` (1.35 per tier); `GameConfig.attackPerLevelPercent 8`; `CharacterSystem.cs:295` |
| CR2-09 | Correctness | Trial rule '특성 2종까지' can only be met by **one** lineage: every lineage has exactly 2 traits and no two share both. Even 4 traits allow only 3 lineages | Replaced with '잔향이 덱 3칸만' (R2-02) | `CharacterContent.Lineages.cs:18,49,80,111,143,173`, `Jjakjjak.cs:18` |
| CR2-10 | Correctness | '결과 바로 보기 … same seed, identical result' holds only for identical inputs, so it must fast-forward the live instance. Headless sims run synchronously (`DungeonSystem.Challenge`: the whole 120 s in one call). Tower auto-retry after every stage clear would hitch | Fast-forward the same `BattleSimulator`; time-sliced runner (≈2 ms/frame); auto-retry ≤1 per 10 min and never on trial floors (§3.1, R2-02, R2-07) | `BattleSimulator.cs:39, 703–716` (fixed 1/60 step); `DungeonSystem.cs:144–170` |
| CR2-11 | Correctness | Guest shell HP read live from `highestStage` gets harder as you grow within the week. The 18:00 guest dot is a nag (F-04 allows only claimable / wasting / finished). A killed app mid-mode would leave the mode formation in the hunt. The 21 "data-only" riders need ≥6 new effect types | HP snapshot per week; dot only when the bank is full; `FormationSave.parkedMain`; R2-05 split into 05a (P0) and 05b (P1) (R2-03, §3.3, §3.6, R2-05) | R2-05 rider list (Pull, stacking buff, cast counter, once-per-battle trigger …) |
| CR2-12 | Overscope | On top of SPEC4's ≈49 agent-days, v1 added ≈14 new permanent growth axes, 5 come-back timers (expeditions, research, pots, attic, pal rests), 6 random-reveal mechanics, 7 narrative containers, ≈14 side fights a day (dungeon 3 + errands 8 + guest 3) and up to 42 affinity taps a day. That is the "growth split too finely" critique of Stone Age Idle that v1 cites | ≤3 new permanent axes in A–B; R2-02 → P1; R2-01 MVP 2 errands; R2-11 3 branches (combat branch removed as a duplicate of R2-04); R2-13 monthly; R2-16 → P2 inside the chest popup; R2-23 3 gifts/day total; R2-26 hidden records only; deferred D-11 to D-19 | `research2/kr_kiugi_notes.md` Stone Age critique; SPEC4 §12 #10 scope |
| CR2-13 | Overscope (pacing) | Chapter 2 would show 8 feature cards: SPEC4's 4 + R2's 2-2/2-4/2-6/2-8/2-10 | ≤4 new cards per chapter. Techniques open at 3-4 or the first Lv60, rounds at 3-2+, tower 3-8, 등불 길 4-1, keepsakes 4-5, workbench 5-1 (§3.5) | SPEC4 §2.8 unlock table |
| CR2-14 | Tone | NPC promotion/demotion league (fake rivals); 단추 roulette; hitting a living magpie; 라라벨's "last note" spent as a resonance-9 stat; a decor decay "loose thread"; firefly '친구들 응원' clashing with SPEC4's cheer; a permanent bonus bought by box-hunt luck | League deferred (D-18), roulette cut, 그림자 까치, rider renamed, decay removed, firefly table = SPEC4's, perfect board → title (§6, §7, R2-01/05/15/16) | Art memory ("A broken music box is afraid to play its last note"); §1.2 tone rule |
| CR2-15 | Naming | Collisions: '산책' ×4 (event, season, walk, 꿈길 산책); 수선 일기 vs 수선 일지; 수선공 등급 vs 수선점 등급 vs 수선점 레벨; '한 땀' ×3 (돌파, 등록, 바늘땀); '기억' ×5; '분실물' ×5; stars for grade, breaks, polish and routes next to 별조각/별빛; 새싹 7일 축제 vs 새싹 출석 (SPEC4 renamed to avoid exactly this) | 등불 길 / 꿈길; diary cut; shop rank deferred; '한 땀 등록' → 추억 수놓기; 기념품 선반; 반딧불 쪽지 / 먹보 상자; stitch marks, shine dots, lamp-post marks (§0, R2-05/10/12/15/16/17/21) | `GameEnums.cs:13` (stars = grade); SPEC4 §13.6 naming note |
| CR2-16 | Missing | (a) **Roster/wardrobe**: 7 lineages; 29 forms (짝짝이 11, the rest 3 each); 10 costumes, all 짝짝이. References launch with 54–69 collectibles. (b) **Places**: 1 field, 7 enemies and 2 bosses for an endless ladder. (c) **Puzzle stages** (AFK Peaks of Time). (d) **All-collection boss** (Crumble). (e) Per-mode rules explainers. (f) Rule-aware auto-formation | New R2-36a (P0), R2-36b and R2-37 (P1), R2-38 (P2); '모두 함께' in R2-03; first-run explainers (§3.1); 맞춤 편성 (R2-08) | `Art/Characters` (29), `Art/Costumes` (10, all jjak_*), `Art/Enemies` (7 + 2), `Art/Battle/field.png`; `CharacterContent.Lineages.cs` (no `costumes:`) |
| CR2-17 | Process | 50 of the 121 findings were never seen. `research2/` has no raw cozy-sweep notes, so the C-findings cannot be re-checked, and the gap folder is empty | Triage rule extended: check SPEC4 §13 and §13.10 first and pass the 3 economy guards; new IDs start at R2-39 (§2.4) | `research2/` listing |
| CR2-18 | Budget / process | Art recount: A+B ≈275 credits against ≈530, and SPEC4's own max-quality pass comes first. Audio conflict: SPEC4 §0.2 rule 7 forbids mirelo/sonilo, while the 2026-11-04 memory allows them | §9.2 recount and a balance check before wave B; no R2 audio until the orchestrator decides (§8, §9.2) | SPEC4 §0.2 rule 7, §10.2; `graphics-via-higgsfield` memory |

### Are the priorities right for a game at our stage?
Our stage: no backend, no monetisation yet, pre-launch, a strong collection + defense core. The critic's ordering rules:
1. **Fix the runway before adding wrappers.** v1 was right that the Lv100 cap is a structural wall. But the wall is shaped by the late curve, so CR-07 comes first, then R2-04/R2-05a with constants that actually track the need. Without that, every side mode built on `highestStage` (guest, errands, tower) inherits the same wall.
2. **Spend on the core verbs.** The 5-direction defense, traits, gimmicks, the lamp and the collection come first. That is why the guest (favoured traits, '모두 함께', the visible shell), errands (new gimmicks on the same combat) and the wardrobe and roster lead. Shops, calendars and rooms follow.
3. **One daily moment of real play, one bundle for chores.** The guest's live fight is the moment, and 오늘의 순회 is the bundle. Every other daily mode must survive being swept.
4. **Pay in things that aren't the paid currency.** The code labels 별조각 "유료 재화". Whatever free players earn now becomes the anchor for any later shop. So R2 pays in 단추 minutes, materials, lineage shards for lineages that took part, titles and costumes, and keeps 별조각 ≤+9.5%.
5. **No fake social layer.** Offline means personal bests, seeded weekly challenges and share codes with real friends, never ranked fictional rivals.
6. **Content is the prestige substitute.** §17 rules out main-stage prestige. So the long game needs scheduled ceiling raises: new forms and lineages (R2-36b), new places (R2-37) and a decided late enemy curve. More meta systems cannot replace them.

**Resulting order:** R2-00 → **A:** R2-03, R2-04, R2-05a, R2-01 (1–2), R2-07, R2-36a, R2-08 → **B:** R2-02, R2-10, R2-12, R2-37, R2-36b, R2-11, R2-13, R2-15, R2-05b, R2-17/19/20/14/18/09, errand 3 → **C:** R2-21, R2-22 (boards), R2-23, R2-26 (hidden), R2-27, R2-16, R2-38 → **D:** R2-29, R2-32, R2-33. Deferred: D-11 to D-19.

### Checked and found consistent (no change)
- **Enum bases match the code.** `BattleMode` (Stage 0, EchoDungeon 1), `EnemyGimmick` (ends at `LampDrain` 1<<8), `CurrencyType` (Gold 0, Gem 1, EventToken 2).
- **SPEC4 append ends match SPEC4 §2.2.** CounterKey 37, FeatureId 26, MetaRewardType 8, MetaConditionType 24, MetaPopupKind 9.
- **The simulator is fixed-step**, so headless == live holds when the inputs are identical.
- **Guest calendar and moon math.** The guest anchor 2026-11-02 is a Monday. The moon constants (2000-01-06 18:14 UTC, 29.530588853 d) are the standard mean lunation; the ±1 DayKey window absorbs the ≈±0.6-day true-phase error.
- **Guest traits.** Every 비 guest pairs with a 2-lineage trait.
- **Formation rules.** 6 `CombatStyle`s make '같은 역할 2명까지' satisfiable.
- **Code facts.** LineageLimit = 1 guardian form + 1 echo form per lineage (`FormationSystem.cs:108, 382`). The attack formula is at `CharacterSystem.cs:295`.
- **SPEC4 §13.10's rejected list** (summon level, gear gacha, prestige, server features, ad boosts, free daily pull, the Lucky Defense merge loop) is respected by every R2 item.
- **v1's offline-first rules hold.** DailyClock everywhere, tamper blocks, append-only enums and inert stubs.

### Open questions for the orchestrator / user
1. **Late curve (CR2-08):** a second enemy taper, a bigger technique rank multiplier, or scheduled content raises (or a mix)? R2-02, R2-04 and R2-05 numbers wait on this.
2. **Audio route:** SPEC4 rule 7 vs the 2026-11-04 memory (mirelo / sonilo). **(v1.2) Answered:** CC0 packs + original synthesis; no standalone Higgsfield audio.
3. **Art:** how many credits SPEC4's max-quality pass takes. R2 A+B needs ≈275 of ≈530.
4. **New-lineage cadence:** is one lineage every 4–6 weeks (≈20 credits each) the content rhythm the user wants? It drives R2-36b and the long-term gacha value.
5. **NPC league:** deferred on tone grounds. If the user wants competition anyway, it should come back as opt-in and clearly fictional, and pay cosmetics only. **(v1.2) Answered:** no NPC league for now (orchestrator). v1.2 also keeps NPC names out of medal thresholds and rank ladders (§7).

---

## v1.2 merge log (2026-11-04, role: R2 merger)

**Scope.**
- **Read in full:** this document at v1.1 (its §2.4 triage rule and the critic revisions CR2-01 to CR2-18) and all 51 entries of `research2/unseen_findings.json` (#71 in full, #72–#121; sweeps: cozy 7, tower-defense 20, code-gap audit 24). The numbering was checked against `research2/all_findings.json` (121 entries; #71 is the entry v1 saw cut off).
- **Also read:** SPEC4 v1.1 (§1, §2.1–§2.2, §2.8, §2.11–§2.12, §4.2–§4.5, §6, §8, §11.2, §13, §14, Appendices A–C); UX-SPEC-IA v1 (§0–§5, §8.8); ART-UPGRADE-PLAN §1.3 and its OPT batch; `scratchpad/audio/` (catalog and counts).
- **Code, read-only:** `Systems/Meta/MissionSystem.cs` (rollover `Reset`, :341–353); `Systems/Battle/BattleSimulator.cs` (wave rest and lane planning, :431–470, :789–835); `Data/BattleConfig.cs` (wave, kill and enemy caps); `Editor/Content/MetaContent.Features.cs` (chapter-2 cards, :119–138); `Core/SaveData.Characters.cs` and `Systems/Characters/CharacterSystem.cs:364` (`obtainedUnix`); `Systems/Characters/CostumeSystem.cs` and `UI/Characters/CostumeItemView.cs` (no costume preview); `Editor/Builders/TutorialSceneBuilder.cs:420` (the '수선점 주인' pill).
- **Backup:** v1.1 → `research2/IDLE_RESEARCH2.v1_1.bak.md` (byte-identical copy). No project file was edited.

**Orchestrator decisions applied.**
- No NPC league for now: D-18 stays, and v1.2 also keeps NPC names out of thresholds and ladders.
- No restyle of the udu/lala lineages: every R2 asset for them references the current forms.
- Prestige, gear gacha and summon level stay rejected (#120, #121).
- Audio = CC0 packs + original synthesis (§8 start condition 3, §9.2).

**Counts (primary disposition).**

| Disposition | Count | Notes |
|---|---|---|
| Findings merged | **51** | #71–#121 |
| Already in SPEC4 | **9** | 5 of them also pass a small content gap to an R2 item (#98 → R2-26, #100 → R2-23, #102 → R2-33, #106 → R2-36a, #113 → R2-27) |
| Already in R2, gap merged into the item | **25** | – |
| Already in R2, fully covered | **3** | #94, #109, #111 |
| **New R2 items** | **2** | R2-39 한발 먼저, R2-40 축음기 음반 (both P2 · S · wave C) |
| Rejected | **6** | #82, #87, #88, #91, #120, #121 |
| Deferred | **6** | D-05 (#86), D-01 (#93), D-10 (#96), D-16 (#115), D-14 (#119) updated; **D-20** (#92) new. **D-21** is also new, as the deferred part of #97 |
| R2 items changed | **22** | listed in §2.5 |

**Economy after v1.2.** P0+P1 ≈235 별조각/week (guard ≤250), unchanged. P2 ≈67 (guard ≤80). 선택 기억편 ≈26 (≤30). Lineage 기억편 ≤77 (≤80). The basket's 별조각 line is untouched (rule 7). No new currency and no new growth axis. Taken at face value the 51 findings proposed ≈+1,300 별조각/week (§9.1).

**All 51, one row each.**

| # | ID | Finding (references) | Disposition | Where | What was merged, or why not |
|---|---|---|---|---|---|
| 71 | C13 | 만난 날 기념일 (Stardew, Animal Crossing, Cats & Soup) | R2, merged | R2-23 | 100일/200일/1년 from `obtainedUnix`, lore days, a 7-day grace; 1년 = 별조각 50 once per lineage (P2 ≈7/week in year 1). #71's 50/50/200 gems trimmed |
| 72 | C14 | 반짝 손님 (Cats & Soup, Animal Restaurant, Penguin Isle, The Tower) | R2, merged (on SPEC4 NEW-03) | R2-15 | modifier variants (종이배, 손수건, 별똥별, 비눗방울) + one hidden record. Its 15 a day, 실밥 and 별조각 table rejected |
| 73 | C15 | 분실물 보관함 (Animal Restaurant, Usagi Shima, Cats & Soup, Neko Atsume) | R2, merged (on SPEC4 NEW-02) | R2-07 | the daily gift keeps one day. The random decor capsule and its keys rejected |
| 74 | C16 | 계절 소품 (Usagi Shima, Cats & Soup, Pocket Camp, Pokémon Café) | R2, merged | R2-33, R2-36a | accessory layer on 29 head anchors (wave D); fitting room (wave A). Affinity for dressing up rejected |
| 75 | C17 | 이웃 수선점 구경 (Neko Atsume 2, Pocket Camp Complete) | R2, merged | R2-29 | '초대장' room codes, 3 stamps a day, a cosmetic stamp card, 3 built-in neighbour rooms without scores |
| 76 | C18 | 축음기 음반 (Neko Atsume 2, Tsuki's Odyssey, Animal Crossing) | **New** | **R2-40** | 10 records (8 AUDIO-SPEC loops + 2 new pieces); picker in Settings and the 안채; cosmetic only |
| 77 | C19 | 포근함 원칙 + 실밥 경제 (Project Horseshoe coziness report) | R2, merged | §1.2 rule 8 | the cozy sandwich and "keep the cozy behaviour the best one". Its ≈300 별조각/week, +19% basket and 실밥 economy rejected; its gate table is superseded by §3.5 |
| 78 | TD1 | 밤의 규칙 + 도전 무대 (BTD6 rules, Arknights CC/SSS, Kingdom Rush Iron) | R2, merged | R2-00 | `SideRuleDef` table + `BattleSetup.Rules` (types in R2-0, effects with the tower). Its +685/week and its 원정-tab hub rejected |
| 79 | TD2 | 오늘 밤의 의뢰 (BTD6 daily, Arknights CC sites, PvZ2 Penny's Pursuit) | R2, merged | R2-38b | daily request letters, the generator, lifetime titles. 별조각 15 a day, the weekly streak and a journal mission rejected |
| 80 | TD3 | 잊힌 것들의 행진 (BTD6 boss events, Lucky Defense, Kingdom Rush bosses) | R2, merged | R2-03 (covered), R2-37 | the weekly tiered boss is R2-03; its three boss mechanics join R2-37's pool with IP-safe skins. Gems and Elite stamps rejected |
| 81 | TD4 | 끝없는 밤 (Rush Royale, Random Dice, Lucky Defense, Arknights SSS) | R2, merged | R2-22 | start at 60%, bosses every 10 waves, picks every 5, 졸음 안개, 1 등불 per run, thread medals, '지난주의 나'. 별조각 rejected (candy instead) |
| 82 | TD5 | 대청소의 밤 (Arknights Annihilation) | Rejected | §7 | a weekly 100–150 별조각 faucet (40–60% of R2's budget); sweeps already give "master once, claim later" |
| 83 | TD6 | 서둘러 부르기 (Kingdom Rush, BTD6) | **New** | **R2-39** | a tap starts the next wave; 잔향 and 불빛 per second skipped; AUTO never calls; no clear-gold bonus |
| 84 | TD7 | 이번 주 소문 (Rush Royale, PvZ2, Arknights, BTD6) | R2, merged | R2-03 | the guest's traits +15% in errands, tower normal floors and walks. No featured lineage, no mission |
| 85 | TD8 | 수선점 위업 (BTD6, Arknights medals, Kingdom Rush) | R2, merged (on SPEC4 Q-04) | R2-26 | battle feats as hidden-record candidates. 설계도 stars and 10–50 gems per feat rejected |
| 86 | TD9 | 손때 숙련 (BTD6 tower XP, Arknights Trust and modules, Rush Royale talents) | Deferred | D-05 | still a 4th per-character axis (3,850 gems lifetime as written); the switchable-habit idea becomes an R2-05b option at 공명 9 |
| 87 | TD10 | 별 세 개와 두 시험 + 수선점 설계도 (Kingdom Rush stars, BTD6 Monkey Knowledge) | Rejected | §7 | stars mean grade; ratings are trivial on an endless idle ladder; a 4th permanent axis |
| 88 | TD11 | 그림자 사냥 + 밤손님 부탁 (Lucky Defense) | Rejected | §7 | ≈70 별조각/week; duplicates NEW-03, R2-15 and errand 1; per-stage chips are noise that AUTO completes |
| 89 | TD12 | 이웃 수선점 도우미 (Lucky Defense, Rush Royale, Random Dice, Arknights supports) | R2, merged | R2-10 | neighbour lenders as story flavour + a link to the 소환 tab for unowned forms. The 6th guardian post rejected |
| 90 | TD13 | 밤거리 게시판 + 쪽지 코드 (BTD6 boards and Challenge Editor, wave rankings) | R2, merged | R2-22, R2-38b | friend codes with AUTO replay; 의뢰서 만들기 custom codes. NPC-named thresholds rejected |
| 91 | TD14 | 기념품 가게 + 밤길 도장 (BTD6 trophies, Arknights CC shop) | Rejected | §7 (tints → R2-09) | a new currency (rule 5; `CurrencyType 3` is 실밥's) and a 선택 기억편 shop. Its cosmetic tints survive as R2-09 rewards |
| 92 | TD15 | 수선 계약서 (Arknights Contingency Contract) | Deferred | **D-20** (new) | L, a third seasonal calendar, needs the rejected currency; buildable later on the rule table |
| 93 | TD16 | 깊은 밤 / 새벽 직전 (Kingdom Rush Veteran/Impossible, Lucky Defense Hell) | Deferred | D-01 | the endless ladder and the tower already give the late challenge; ≈2,500 gems lifetime as written |
| 94 | TD17 | 거대한 망각 (Rush Royale Dragon Rift) | R2, covered | R2-03 | the weekly cumulative pool is the stitch meter with the week HP snapshot; nothing to add |
| 95 | TD18 | 긴 밤 산책 (BTD6 Odyssey, Rush Royale trials) | R2, merged | R2-10 | the '졸음 길' depth rule. The separate weekly escort mode and its gems rejected (also a '산책' clash, CR2-15) |
| 96 | TD19 | 제비뽑기 밤 (Rush Royale, Random Dice, Arknights IS) | Deferred | D-10 | stays folded into R2-10's borrowed friend and R2-38b |
| 97 | TD20 | 밤거리 지도 (Kingdom Rush maps, BTD6 medals, Arknights tiles) | R2, merged | R2-37 (+ **D-21**) | one place rule, one optional interactable, a boss mechanic pool. Lane geometry and the medal grid deferred |
| 98 | GP1 | Achievements, 수선점 기록 (Realm Grinder, MapleStory) | SPEC4 | Q-04 (+ R2-26) | identical to Q-04; the hidden-record ideas go to R2-26 |
| 99 | GP2 | Codex rewards + sets (Monopoly GO, Neko Atsume) | SPEC4 | Q-05 (+ D-19) | identical numbers; sets and the duplicate-star vault stay deferred |
| 100 | GP3 | Friendship / affinity (Blue Archive, Pocket Camp, Tsuki) | SPEC4 | CR-10 (+ R2-23) | identical levels and pets; loved-material gifts go to R2-23 |
| 101 | GP4 | Daily challenge seeds, 오늘의 골목 (Slay the Spire, Wordle) | R2, merged | R2-38b | merged with #79: a shared daily seed and a share line. ≈210 gems/week, streak shards and the loaner team rejected |
| 102 | GP5 | Events, the local calendar (Gold & Goblins, Cookie Clicker, Pocket Camp Complete) | SPEC4 | EVT-01 (+ R2-33) | identical calendar; Korean seasonals are R2-33's table; October masks added there |
| 103 | GP6 | Mail rewards (Firestone, Travel Frog) | R2, merged | R2-07 (+ SPEC4 EVT-02, R2-23) | 놓친 선물: completed rewards are auto-claimed before the 05:00 reset. Coupons are EVT-02; letters are R2-23 |
| 104 | GP7 | Random tappable visitor, 길 잃은 반딧불 (Cookie Clicker) | SPEC4 | NEW-03 | identical; its 12 s and instant-buff table are superseded by SPEC4's |
| 105 | GP8 | Device transfer (Pocket Camp Complete) | SPEC4 | SAVE-01 | covered; a 14-day backup reminder is suggested to the SPEC4 owner (Appendix B) |
| 106 | GP9 | Passes, free tracks only (MapleStory Idle, Firestone) | SPEC4 | Q-06, PRG-04 (+ R2-36a) | covered; season costumes for other lineages fill tier 30 from season 2 |
| 107 | GP10 | Titles, 수선점 간판 (MapleStory, AFK Journey) | R2, merged | R2-09 | '수선점 주인' becomes title #1 (the FTUE card already prints it). The ownership bonus and the TopBar line rejected |
| 108 | GP11 | Auto-merge, 실 엮기 (Capybara Go, Legend of Mushroom) | R2, merged | R2-19 | [모두 엮기] weaves only the surplus above recipe needs, plus an auto toggle (off by default). 5:1 ratios rejected |
| 109 | GP12 | Tower variants, 시계탑 (AFK Arena, AFK Journey) | R2, covered | R2-02 | the climb and restricted floors exist. Three weekday style towers rejected (overscope; unfillable pads) |
| 110 | GP13 | Resource dungeons, 밤 심부름 (Seven Knights Idle) | R2, merged | R2-01 | one favoured style per errand, fed to 맞춤 편성. Its shard boss rush is errand 4 (D-12) |
| 111 | GP14 | World boss score attack (AFK Journey Dream Realm) | R2, covered | R2-03 | tiers and threshold rewards exist. The 9-NPC weekly ranking rejected (D-18) |
| 112 | GP15 | Offline leaderboards + async ghosts (Wordle, Slay the Spire, Pocket Camp, AFK Arena) | R2, merged | R2-22 | the share line, friend rows, '지난주의 나'. The NPC rival ladder rejected |
| 113 | GP16 | Story chapters (Tsuki Adventure, Blue Archive) | SPEC4 | CR-05 (+ R2-27) | identical; a one-line chapter letter goes to R2-27 |
| 114 | GP17 | Photo / diary (Neko Atsume, Travel Frog, Usagi Shima, Tsuki) | R2, merged | R2-32 (+ D-13) | party stickers on postcards, duplicates → candy. The snapshot diary joins D-13 |
| 115 | GP18 | Mini-games, 다락방 뒤적이기 (Legend of Mushroom mine, Tsuki, Usagi Shima) | Deferred | D-16 | the same stamina timer, gem trickle and research currency as v1's attic dig |
| 116 | GP19 | Research tree, 수선 비법서 (Legend of Mushroom Tech Park, Egg, Inc.) | R2, merged | R2-11 | tier gating. The combat branch, 비법 쪽지 currency, all-line basket cap and colliding name rejected |
| 117 | GP20 | Local notifications (Travel Frog, Tsuki, Gold & Goblins) | SPEC4 | NOTI-01 | identical (kinds, cap 3, quiet hours, permission timing) |
| 118 | GP21 | Decorating + night visitors (Neko Atsume, Usagi Shima, Pocket Camp, Cats & Soup) | R2, merged | R2-29 | 단추/사탕 pricing (no 실밥 needed), the favourite reveal, a shop-tap entry. Gem and shard tips rejected |
| 119 | GP22 | Pets, 고쳐 준 길손들 (Legend of Mushroom, Seven Knights) | Deferred | D-14 (+ R2-29) | pets stay deferred; mended enemies appear as cosmetic visitors |
| 120 | GP23 | Equipment, 수선 부적 (Legend of Mushroom lamp) | Rejected | §7 | gear gacha (orchestrator; CR-11; D-02) |
| 121 | GP24 | Rebirth variants, side layer only (AdVenture Capitalist, Realm Grinder, AFK Journey) | Rejected | §7 | prestige (orchestrator); re-claims break the gem guard; a 4th axis with a basket term. Its roguelite is R2-10/R2-21 |

**New items (full specs in §6.3).**
- **R2-39 한발 먼저** (P2 · S · wave C). During wave rest a paper lantern lights at the lane the next group will mostly use. A tap starts the wave at once and grants 잔향 +2 per second skipped (cap 8) and 불빛 +1% per second (cap 4%). It is refused in boss phases, with 20+ enemies alive, in FTUE, asleep, in SHEET and in rule-bound modes. AUTO never calls, so idle income and CR-07 are unchanged. No currency; `CounterKey.CallEarly 55`; 0 credits.
- **R2-40 축음기 음반** (P2 · S · wave C). Ten collectible BGM records: 2 defaults, 6 more of AUDIO-SPEC's planned loops, and 2 new pieces ('마지막 음', '괘종시계 왈츠'), each unlocked by a milestone. The player picks battle music and menu/sleep music in Settings or in the 안채. Cosmetic only; `MetaRewardType.Track 12`, `MusicSave`; 0–5 credits.

**Consistency notes found while merging** (fixed on the R2 side, or flagged).
1. **Chapter 2 card count** (§3.5). The code already has 4 card-capable unlocks in chapter 2; with R2's 2-2 and 2-6 cards that is 6, against the critic's ≤4 rule. Proposed: R2's guest unlock becomes a NEW pill (5); SPEC4 may share one 2-1 card (4).
2. **Hub sheets vs UX-SPEC-IA** (§1.4). IA v1 binds the sheet to the six tab panels; R2-0 must extend `UIFrame` or fall back to popups.
3. **The rails are full** (§1.4). With '안채' on L2 and '밤 나들이' on R2, no further rail icon is possible; every later item must live in a sheet, a popup, the ≡ menu or Settings.
4. **R2-15's lantern chip** would have overlapped IA rail L3; it moves to x 500–564, y 1500–1564.
5. **§1.4's sheet numbers** (top 890, header 80) predate IA v1 (top 914, header 56); the v1.2 block there gives the current ones.

**Open questions for the orchestrator.**
1. Chapter-2 cards (note 1): accept the R2-side NEW pill, and pass the shared 2-1 card to SPEC4's owner?
2. Hub surface (note 2): a non-tab sheet in `UIFrame` (preferred) or popups?
3. Season costumes (R2-36a): one costume per Q-06 season, ≈5 credits a month for six seasons. Inside the ≈50/month content cadence?
4. Daily request (R2-38b): it stays P2 / wave C to respect "one daily moment of real play" (the guest). If playtests show the daily loop is thin, it is the cheapest daily-play item to pull into wave B.
5. Candy: v1.2's P2 sources add up to ≈200 별빛 사탕/week. Should the event shop's 여행 가방 조각 (20 candy) get a weekly limit before wave C?

---

## v1.2.1 critic notes (2026-11-04, PHASE4-ROADMAP critic)

Corrections from the phase-4b roadmap review. Inline spots are tagged `(CR4-nn)`; the evidence is in PHASE4-ROADMAP.md, "Critic revisions". The P0/P1 contract is SPEC5 (now v1.0.1). This list covers what SPEC6 will read from here, the P2 items and the budgets.

| Tag | Item | Correction |
|---|---|---|
| CR4-01, CR4-02 | §9.1 | The 2,640 baseline is SPEC4's model; the measured numbers are higher, and two stage-indexed faucets (SPEC4's ch6+ chapter chest, the shop-level mail) ride R2-04's chapter pace. Note added under "Result"; the binding bands are SPEC5 §4.4 |
| CR4-04 | §9.1 | Added the missing R2-27 page row and the new-lineage row (SPEC4's affinity row needs re-basing per lineage). The Q-04 R2 line stays ≤ 1,000, so R2-26's +150 fits (SPEC5 v1.0.1 restores it) |
| CR4-05 | R2-22, R2-29 (also R2-38b's 단추 30분치) | `GoldMinutes` follows the shop's 단추/s, which is decorative from week 2 after the CR-07 retune. 단추-minute rewards and prices count only if PHASE4-ROADMAP Q15 lands |
| CR4-07 | R2-36b, R2-37 | Step 1's six forms are not the ceiling raise (≈ 5–15 stages); step 2's cadence is, scheduled from the measured ceiling. Places are spaced by engaged days (about one every 10 days up to the ceiling), not strictly every 5 chapters |
| CR4-12 | R2-37 | The place rule is shown in the SynergyRow's modifier chip; the StagePanel has no free slot |
| CR4-13 | R2-40 | The gramophone button lives in HomePanel's `PanelHeader` slot, not the frozen 56 px shared `SheetHeader` |
| CR4-20, CR4-22 | R2-37 | Every place gets an ambience bed (`PlaceDef.ambKey`) and a 1080×810 crop of its field |
| CR4-23 | §6 (wave C/D) | PHASE4-ROADMAP RM-9/RM-10 now list the v1.2 parts SPEC6 must keep: R2-22's 끝없는 밤, friend codes and share line; R2-38b's 의뢰서 만들기; R2-21's branching map and charm themes; R2-23's loved-material gifts, lore days, requests and badges; R2-27's chapter 1–5 letters; R2-29's favourites, mended visitors (they need per-enemy kill counters), 이웃 수선점 구경 and the shop tap |
