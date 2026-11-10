# AUDIO-SPEC — LOST & LOVELY sound, music and 타격감 (AUD-01, complete)

Version 2 · 2026-11-04 · role: audio director · target `D:/UnityProject/unity_idle` (Unity 6000.2, uGUI, portrait 1080×1920)

**Status.** This is the contract for the audio workflow. For audio only, it supersedes SPEC4 §3.1 ("tier→sound map"), §3.5, the audio half of §3.6 and the cue list and route table of §10.2. It also supersedes UX-SPEC §3.15 Phase B ("generate clips via Higgsfield"), which cannot run under the binding sourcing rules. SPEC4 cue names are kept, either as keys or as aliases (§2.12), so P4 packages that already say `chest_open`, `basket_claim`, `unlock_card` and so on keep working.

**Inputs read on 2026-11-04:**
- Code: `Scripts/Core/{UISfx, UISfxTable, Haptics, GameSettings, GameBootstrap, Game}.cs`; `Systems/Battle/{BattleTypes, BattleSystem, BattleSimulator (state API), DungeonSystem}.cs`.
- Code, read only: `UI/Battle/{AttackFxCatalog, EnemyFxCatalog, SkillVfxLook, HitFeel, BattleFieldMapper, EnemyViewPool, BattleView(.Attack/.Enemy/.Skills/.Cast), BattleFx(.Attack/.Skills/.Pulse)}.cs`, `UI/{UIManager, UIPopup, UINav}.cs`, `UI/Meta/SleepModeView.cs`.
- Data, editor and settings: `Data/SkillDefs.cs`; `Editor/{IdleDataBuilder, ArtImportPostprocessor}.cs`; `Tests/EditMode/FeedbackGateTests.cs`; `ProjectSettings/AudioManager.asset` (32 real voices, DSP buffer 1024).
- Specs and memory: `IDLE_RESEARCH.md` (AUD-01, ATK-01/03/04/06/09, FX-01/02/05); `UX-SPEC.md` §3.15; `UX-SPEC-IA.md` §1.5 (sheet frame); `FX-JUICE-SPEC.md` (arrival times unchanged, FX clock freezes during global stops, the memory mote); SPEC4 §3 and §10.2; the art-style memory.
- Live checks: Higgsfield `models_explore(type: audio)`, and the kenney.nl audio category and pack pages (§7.1, §7.2).

**Machine-readable twins (in `scratchpad/audio/`):**
- `audio_catalog.json` has every key: bus, class, tier, loudness, gates, ladders, flags, `UISfxKey` values and the production recipe. It is built by `build_catalog.py` from `catalog_data.py` and `recipes_data.py`.
- `scores.json` has all 24 scores (10 music, 14 jingles) in LLN v1 (§7.6.0). It is built and validated by `scores_build.py` from `scores_data.py`: every bar's beat sum, chord grammar, instrument ranges and the lullaby rule. The run reports 0 errors and 0 warnings.
- The tables in §2, §7.5, §7.6 and §7.7 are generated from the same data. **Edit the data files and rebuild; never hand-edit those tables.** The implementation may generate `AudioCatalog.cs` once from the JSON. After that the C# file is the source of truth in the repo (content-in-code rule).

---

## 0. Summary, decisions, ownership

### 0.1 What ships
**306 sound keys**, plus **8 aliases** and **7 reserved** keys (P3):

| Group | Keys |
|---|---|
| UI | 75 |
| Ally attacks | 41 |
| Enemies and bosses | 34 |
| Shop, shield, support, echo and lamp | 22 |
| Skills | 51 |
| Status effects | 12 |
| Flow, combo and stage modifiers | 14 |
| Ambience | 10 loops + 4 emitter sets |
| Music | 8 (7 loops = 9 pieces, plus 1 intro) |
| Jingles | 14 |
| Voice barks (optional) | 21 |

That is **694 shipped takes**, chosen from **1,646 rendered candidates** (§2.13).

- **How every clip gets made.** These are the binding sourcing rules (§7.1):
  - **198 keys are pure original synthesis (SYN).**
  - **65 are hybrid (HYB):** synthesis plus an optional, processed Kenney CC0 foley layer. The synthesis part alone is always shippable.
  - **22 are original compositions (MUS):** every piece and jingle has a complete, machine-checked score.
  - **21 are voice barks (VOX):** Higgsfield speech models only, optional.
  - No Higgsfield SFX or music model is used anywhere.
- **`UISfxKey` grows from 12 to 81 members**, append-only. `UISfx.Play(UISfxKey, float)`, `Boot()`, `SetTable()` and `ApplySettings()` keep their signatures and behaviour, and `FeedbackGateTests` stays green unchanged.
- **The engine.** A code-bus audio engine sits behind the existing `[Audio]` object:
  - buses Ui / Sfx / Stinger / Bgm / Amb / Loop, with 31 pooled voices and priority stealing;
  - per-key gates, shuffle bags, tuned pitch ladders and field-x panning;
  - 2× rules, hit-stop-aware scheduling with sub-frame `PlayScheduled`, and ducking;
  - a music director (states, crossfades, rotation and rests, intro → loop) and an ambience director (stage-modifier layers and emitters).
- **`BattleAudioDirector`** is created at runtime by the audio boot. It subscribes to `Game.Battle` and times every hit to the projectile arrival with `AttackFxCatalog`'s static rules, read only. **No edit to BattleView, BattleFx or the scene builders.**
- **Data and tools:**
  - `AudioLibrary` (in Resources), built by `AudioDataBuilder` from `Assets/_Project/Audio/**/{key}_{NN}.wav`;
  - `AudioImportPostprocessor`;
  - an `AudioSelection` asset;
  - the **"IdleGame/Audio/사운드 시험장"** audition window, where the user picks takes by ear.
- **The production kit:** a Python synthesis toolkit and an 80-patch library (§7.3–§7.4), per-key recipes (§7.5), the LLN sequencer and 24 scores (§7.6–§7.7), and an objective QC and ranking pipeline (§7.8). Agents cannot listen.

### 0.2 Sourcing decision (binding rules applied; nothing blocks production)
- **Higgsfield audio, re-checked live on 2026-11-04** with `models_explore(type: audio)`. The catalog has:
  - speech models: `seed_audio`, `qwen_audio_tts`, `elevenlabs_v4`, `elevenlabs_v4_turbo`, `text2speech_v2`;
  - `sonilo_music` and `mirelo_text_to_audio` (both marked "Game pipeline only");
  - `inworld_text_to_speech` ("Game pipeline only").

  `suno_music` is **not** listed. So `sonilo_music`, `mirelo_text_to_audio` and `inworld_text_to_speech` are **never called**, and no Higgsfield model makes any SFX, ambience, music or jingle.
- **SFX and ambience:** original synthesis, rendered locally with Python (numpy and scipy only). For 65 foley-like keys an optional CC0 layer is added from Kenney packs. Those packs' pages state "Creative Commons CC0": Impact Sounds (130 files), Interface Sounds (100), UI Audio (50), RPG Audio (50) and Casino Audio (50). Each pack's `License.txt` is verified again at download (§7.2). Every HYB recipe still works with the CC0 layer removed, so production never waits on a download.
- **Music and jingles:** original compositions written in this spec (§7.6–§7.7) and rendered by the §7.6.0 sequencer with the §7.4 instruments. Kenney Music Jingles (CC0) is reference only and is not shipped.
- **Credits.** "크레딧충전했으니까마음껏써" can only go to the **optional voice barks** (§7.9): 21 short Korean lines from `seed_audio`, `text2speech_v2` or `elevenlabs_v4`, 6 candidates each. Everything else is free and unlimited. That is why every key gets 2–3× more rendered candidates than it ships. "품질최상" is reached through takes, QC and the user's ear, not through a generator.
- **One opt-in remains, and it does not block anything.** Barks ship **silent** until the settings package adds a `GameSettings.VoiceBarks` toggle (default on; settings row '캐릭터 목소리'), §8.2. With no clips at all, the game stays silent with no errors, exactly as today.

### 0.3 Ownership
| This workflow owns | Read-only dependencies (never edited) |
|---|---|
| `Scripts/Core/UISfx.cs`, `Scripts/Core/UISfxTable.cs` | `UI/Battle/AttackFxCatalog.cs`, `EnemyFxCatalog.cs`, `SkillVfxLook.cs`, `HitFeel.cs` (`HitFeel`, `LampWave`, `FxSlowMotion`), `BattleFieldMapper.cs`, `EnemyView.KillHold`, `BattleFx` public statics (`StraightFlight`, `BeamReach`, `HealRingTime`, `RibbonTravel`) |
| new `Scripts/Core/Audio/**` | `Systems/Battle/BattleTypes.cs`, `BattleSystem` events/state, `BattleSimulator` read-only state, `Data/SkillDefs.cs`, `Data/BattleConfig.cs` |
| `Editor/IdleDataBuilder.cs`: **one added line** in `BuildContent` | `Core/GameSettings.cs` (volumes), `Core/GameBootstrap.cs` (already calls `UISfx.Boot()`), `Core/Game.cs` |
| new `Editor/Audio/**` | everything under `Scripts/UI/**` and the scene builders (the UI and FX workflows wire the hooks in §8.1) |
| `Assets/_Project/Audio/**` (clips, `AudioSelection.asset`, `audio_qc.json`, **`THIRD_PARTY_AUDIO.md`**), `Assets/_Project/Resources/Audio/**` | `ProjectSettings/AudioManager.asset` (32 real voices: the engine stays within it) |
| new `Tests/EditMode/Audio*Tests.cs` | `Tests/EditMode/FeedbackGateTests.cs` (must keep passing unchanged) |
| production scripts and masters under `scratchpad/audio/**` (never under `Assets`) | downloaded CC0 archives: each in its own empty `scratchpad/audio/cc0/<pack>/` folder, data only (§7.2) |

**Withdrawn SPEC4 items (tell P4-0 and P4-3a):**
- P4-0 must **not** add `AudioBus` to `GameEnums.cs`. It lives in `Core/Audio/AudioTypes.cs` (§3.2).
- P4-0 must **not** stub `Core/Audio/{AudioSystem,AudioKeys}.cs`.
- SPEC4 conflict #3 (±6 % for every cue, edit `FeedbackGateTests`) is replaced. UI keeps ±5 %, which a test this workflow does not own pins. Battle cues use ±6 %.
- SPEC4 §3.5's `UI/Battle/BattleSfx.cs`, ticked by BattleView, is replaced by `Core/Audio/BattleAudioDirector.cs` (§5).
- `AppSignals` does not exist yet. The engine exposes `AudioService.SetSleep` and `AudioService.OnGachaReveal`. Once P4-0 lands `AppSignals.SleepChanged` and `GachaRevealHigh`, the engine subscribes to them in a later change. It never references a type that does not compile today.

### 0.4 Deltas against earlier specs
| Topic | Earlier | Now | Why |
|---|---|---|---|
| Clip source | UX-SPEC M18-B: Higgsfield `generate_audio`; SPEC4 §10.2 routes A/B/C | SYN + HYB (Kenney CC0) + MUS (original scores); VOX barks only via speech models | binding sourcing rules; no permitted Higgsfield SFX/music model |
| Battle hooks | SPEC4: `BattleSfx` in `UI/Battle`, ticked by BattleView | `Core/Audio/BattleAudioDirector`, created at runtime, subscribes to `Game.Battle` | file ownership; no FX-workflow edits |
| Pitch jitter | SPEC4: ±6 % for all cues | UI ±5 % (legacy gate, tested) · battle ±6 % · tuned 0 | keep `FeedbackGateTests` green |
| Loudness | SPEC4: −14 LUFS music/jingles, ≈−16 dBFS RMS SFX | tiers §1.8: BGM −18 (battle) / −16 (menus) LUFS-I, jingles −13 S-max, SFX −12/−14/−17/−20 M-max | SFX sit on top of the bed |
| File names | SPEC4: `{key}.wav`, `{key}_2.wav` | `{key}_{NN}.wav` (both SPEC4 forms still parse) | many takes per key |
| Boss music | SPEC4: `bgm_boss` on chapter bosses only | kept; regular bosses get `jingle_boss_appear` + `amb_boss` tension layer | idle pacing (a boss every stage) |
| Main music | one battle loop | 3 rotating pieces + rests (§3.11) | hours-long idle sessions |
| Voices | SPEC4: 12 shared | Ui 8 · Sfx 14 · Stinger 1 · Bgm 2 · Amb 4 · Loop 2 = 31 ≤ 32 real voices | battle density + ambience |
| Speech | none anywhere | optional barks, outside battle, behind a setting (§7.9) | rule 3 allows them; the user asked for "everything" |

### 0.5 Non-negotiables
1. No `AudioMixer` asset (a builder cannot create one through public API). Buses are code.
2. With no clips (library missing or empty) nothing is created and nothing logs an error (today's behaviour).
3. Zero per-frame allocations in steady state; all tables are arrays sized at boot; idle components disable themselves.
4. Focus rule (M01): never pause, mute, dim or slow anything on `OnApplicationFocus(false)`. Only `OnApplicationPause(true)` fades BGM/AMB (0.2 s) and pauses.
5. `UISfx` public API stays source-compatible; the 12 old `UISfxKey` values keep their numbers and meaning.
6. Never launch Unity on the user's project. Compile-check with `python -I scratchpad/compile_check.py` (expect `COMPILE OK`); real Unity runs only on a project copy.
7. Sourcing rules (§7.1) are binding. Never call `sonilo_music`, `mirelo_text_to_audio` or `inworld_text_to_speech`. Speech exists only as the optional barks; SFX, ambience, music and jingles contain no voice, and QC screens every take for speech (§7.8).
8. Downloads are untrusted data: each archive in its own new empty folder, nothing executed from it, processed only by `python -I` scripts kept elsewhere; every external file used is listed in `Assets/_Project/Audio/THIRD_PARTY_AUDIO.md` (§7.2).

---

## 1. Sonic identity

### 1.1 Concept: "a music box that guards the night"
The shop at the end of a night alley is a music box that never finished its song. Everything that protects it sounds like the object it once was, close-miked and warm; everything that attacks it sounds like *forgetting*.

Three rules decide every sound:
1. **Material truth.** A sock sounds like felt and thread, a key like a lock, a candle like flame and wax. Allies are bright, clean and (when tonal) tuned. Enemies are dust, paper, rust and shadow: duller, noisier, never tuned. A player with eyes closed should hear who is fighting.
2. **One harmonic world.** Every pitched sound lives in the **F-major pentatonic set {F, G, A, C, D}** (§1.3). Twenty chimes over any BGM never clash.
3. **Soft is not weak.** Crisp 1–5 ms transients for clarity and 타격감, round bodies, short tails (hits ≤ 250 ms), and nothing that stings at 6–10 kHz. The battle runs for hours; it must stay pleasant at 2× speed with twenty enemies.

### 1.2 Material palette
| Family | Sources (synthesise these; CC0 foley layers where §7.5 says HYB) | Used for |
|---|---|---|
| Felt & fabric | felt mallet thumps, linen swishes, plush squeezes, thread zips, stitch ticks, ribbon flutters, yarn whips | 짝짝이, 우두커니 canopy, UI sheets/drag, echo-token socks, costume |
| Wood | beads on felt, abacus clicks, spoon knocks, drawer slides, door knocks and creaks, marimba | UI taps/deny/toggles, shop hits, shop heal |
| Paper | card flicks, page turns, envelopes, paper pops, confetti | tabs, gacha cards, toasts, story pages |
| Buttons & small hard things | shell/plastic buttons clacking, rattling in tins, poured into jars | 똑단이, coins (단추 currency), spend, coin ladder |
| Metal (small, warm) | brass keys, latch and deadbolt clicks, tiny tumblers, thimbles (muted), springs, wind-up ratchets | 찰칵이, 루즈리 case, 태엽쥐, 골무 |
| Glass & water | glass beads and stars, bubbles, droplets, splashes, puddle ripples, glass harmonica | 우두커니, shields, gems, echoes (beings of light), lamp wave |
| Flame & wax | match strikes, candle flicks, soft flame blooms, ember crackles, wax plips | 몽당이, lamp, burn |
| Bells & tuned percussion | music-box comb, celesta, glockenspiel, small handbells, brass shop bell, tuning bells, wind chimes | 라라벨, ladders, rewards, UI confirms, jingles |
| Magic | sparkle shimmers (granular glass), swells, reverse shimmers, soft "fwoom" blooms | skills, summons, lamp, evolution, gacha |
| Enemy matter | dust puffs, papery moth wings and powder, wet rust splats and scrapes, dull tin, reversed airy shadow whooshes, unravelling | enemies, bosses, deaths |

**Never:** human voices or mouths (including "chu" kisses — 루즈리's kiss is a cork-like pop), laughter, crowds, gunshots, explosions, lasers, sci-fi beeps, electric buzzers, harsh metal screeches, big reverbs, EDM risers/drops. The only voice in the game is the optional barks of §7.9 (outside battle events, behind a setting).

### 1.3 Tonal system
- **Home pitch set:** F G A C D. The battle and map tracks are in F major or D minor/Dorian (the same pentatonic). Other tracks use keys that contain the set (Bb major, C major, G minor).
- **Tuned keys (8):** `atk_musicbox`, `atk_musicbox_crit`, `atk_musicbox_finale`, `hit_lamp`, `kill_chain`, `ui_count_tick`, `lamp_tick`, `status_chain`. Each take stores its measured fundamental `rootHz`; the engine retunes with `pitch = targetHz / rootHz` (exact, no jitter). Takes are rendered at the middle of their ladder (`renderRoots` in the JSON); the multi-root keys `hit_lamp` and `kill_chain` ship takes at two roots and the engine uses the nearest one, so no note is resampled by more than 7 semitones.
- **In-key keys (57, flag `inkey`):** clips with a melody or clear pitch (fanfares, chimes, jingles) must be authored in F major/D minor; QC detects the dominant pitch classes and rejects or re-pitches (≤ ±1 semitone resample) off-set takes.
- **Track transposition:** every shipped piece is composed inside the F-pentatonic world (§7.6), so `transposeSemitones` is 0 for all of them; the field exists only for future tracks, and tuned keys follow it while such a track plays.
- **Ladders** (semitones; ratio = 2^(n/12)):

| Ladder | Root | Steps | Rule |
|---|---|---|---|
| lullaby (`atk_musicbox`) | F5 698.46 Hz | +7 +4 0 +2 +4 +2 · (rest) | C6 A5 F5 G5 A5 G5, then the 7th hit is a **rest** (only the shimmer layer plays). Per source (guardian slot / echo id). `lala_finale` plays the 7th = F5 (`atk_musicbox_finale`): the song finally resolves |
| kill (`kill_chain`) | F5 | 0 2 4 7 9 12 14 16 | +1 step per kill within 0.8 s; reset after 1.2 s quiet; hold the top |
| lamp (`hit_lamp`) | C5 523.25 Hz | 0 5 9 12 17 21 24 | C5 F5 A5 C6 F6 A6 C7 by wave arrival order, max 7 notes |
| count (`ui_count_tick`) | C6 1046.5 Hz | 0 2 5 7 9 12 | one step per tick (≤ 1 per 70 ms), hold the top; `ui_count_finish` resolves |
| gauge (`lamp_tick`) | F5 | 0 4 7 | 25 / 50 / 75 % → F5 A5 C6; full → `lamp_full` (F chord) |
| chain (`status_chain`) | C6 | 0 2 5 7 9 | one step per hop inside one cast |
| coin (`ui_coin_land`) | clip pitch | +3 % per landing ≤ 0.3 s apart, cap +18 % | legacy `UISfxGate`, unchanged |

- **The lullaby is the game's leitmotif.** `bgm_story` states it (C A F G A G and a held breath), 라라벨's hits play it and always stop one note short, and only `lala_finale` (and `ui_gacha_reveal_mythic` / `jingle_gacha_mythic`) let the last note sound. Lore: "마지막 음이 고장 난 오르골" — she fears the last note; the finale form plays it to protect her friends.

### 1.4 Lineage signatures (allies)
Spectral lanes keep seven lineages separable in a crowd: each owns a band for its hit transient.

| Lineage | Object | Launch `fire_*` | Hit (`atk_*`) | Crit | Extra layer | Transient lane | Lore twist |
|---|---|---|---|---|---|---|---|
| 짝짝이 jjakjjak | sock | thread zip + needle glint | needle "칭" + felt "퐁" + 2 stitch ticks (`atk_needle`) | scissor snip + ringing needle | — (stitch ticks are in the hit) | 2.5–4 kHz ping over a 300 Hz felt pop | the two ticks = the two X stitches drawn on the enemy |
| 똑단이 ttokdan | button | thumb flick + whirr | hollow button "딸깍" (`atk_button`) | big coat button "딱!" + rattle | `atk_button_bounce` "또르르" (50 %) | 1.5–3 kHz click | the little buttons that fall off and bounce away |
| 찰칵이 chalkak | key | bright key zip | two-stage latch "찰칵" + tumbler rattle (`atk_key`) | deadbolt "철컥!" + bell | — | 3–6 kHz metal click | every hit "locks" the target (lock ring) |
| 몽당이 mongdang | candle | flame flick "화륵" | flame bloom "후앙" + wax plop (`atk_candle`) | flare + ember crackle | `atk_candle_drip` "똑" (50 %); burn ticks `hit_dot` | broadband 0.5–2 kHz bloom, crackle on top | small, warm, a little afraid of wind |
| 우두커니 udukeoni | umbrella | canopy flick + water flick | drop "톡" + splash "찰방" (`atk_rain`) | water crown + droplet ring | Rain stages swap the hit for `atk_rain_big` | 1–2 kHz drop + splash noise | louder and happier when it rains |
| 루즈리 rouge | lipstick | case twist + cap click | cork-like smack-pop "쪽" (`atk_kiss`) | bigger pop + heart sparkles | — | 0.8–2 kHz pop + 6 kHz sparkle | shy/bold: tiny twist, confident pop |
| 라라벨 lalabell | music box | wind-up ratchet "끼릭" | tuned comb note from the lullaby (`atk_musicbox`) + `atk_musicbox_ring` | three-tine F chord (`atk_musicbox_crit`) | finale note (`lala_finale` only) | tonal 0.7–2 kHz | the missing 7th note (§1.3) |
| generic | unknown | soft orb whoosh | lavender orb "뽁" | `hit_crit` only | — | 1–2 kHz | fallback for future lineages |

### 1.5 Modifiers on top of the lineage
| Case | Sound rule |
|---|---|
| Defense | lineage hit as is; the slow ring is heard through `status_slow` (throttled) |
| Assault (돌격) | + `atk_layer_slash` at the hit; hit +1 dB (T1 heavy feel) |
| Capture (포획) | + `atk_layer_thread` (taut thread twang) at the hit |
| Snipe (저격) | + `atk_layer_snipe` zip at the launch; hit +2 dB and + `atk_layer_heavy` (T1) |
| Support / Heal | hit −2 dB, pitch +1 semitone (light, lobbed) |
| Critical (T2) | lineage `*_crit` + universal `hit_crit` "팅!" at the same instant (`hit_crit` ≤ 1 per 0.12 s); 60 ms micro-duck of other battle voices (−4 dB) so the crit pops |
| Enemy shield fully absorbs | `shield_block` instead of the lineage hit; shield break = `shield_break` + `atk_layer_heavy` |
| Echo (잔향이) melee | `echo_slash` at t=0; lineage hit at the swing midpoint, −3 dB, pitch +2 st ("smaller, lighter, made of light") |
| Echo ranged | lineage `fire_*` −6 dB +3 st; hit −3 dB +2 st at 0.14 s × speed scale |
| Token (sock family) | `echo_token_pop` "퐁" only |
| Form tier 2 | + `atk_layer_gold` on 35 % of hits (−10 dB) |
| Enemy material | + `hurt_{family}` under the hit, −6 dB, ≤ 1 per 0.25 s per enemy, 50 % at 1×, 30 % at 2× |

### 1.6 Enemy families
| Family | Members | Strike (`EnemyAttackLook.Sfx`, kept as keys) | Hurt layer | Death | Character |
|---|---|---|---|---|---|
| Dust | 먼지뭉치 `enemy_dust`, 엉킨 실뭉치 `enemy_tangle` | `enemy_puff` "푸슉", `enemy_whip` "휙-탁" | `hurt_dust` "푸" | `death_dust` poof + rising cream sparkles ("잊힘이 기억으로 풀림"); tangle overrides with `death_tangle` (yarn unravel) | dry, soft, woolly |
| Moth | 좀나방 `enemy_moth`, 좀나방 여왕 `boss_mothqueen` | `enemy_flutter` "파닥파닥", `boss_vortex` | `hurt_moth` "파사" | `death_moth` wing tear + powder | papery, fluttering, powdery |
| Metal | 녹얼룩 `enemy_rust`, 골무 껍데기 `enemy_thimble`, 태엽쥐 `enemy_windup` | `enemy_splat` "철퍽", `enemy_clang` "깡" (felt-muted, never ringing), `enemy_boing` "끼릭-보잉" | `hurt_metal` "똥" | `death_metal` screws/gears scatter; windup overrides with `death_windup` (spring boing) | rusty, dull, toy-mechanical |
| Shadow | 망각의 그림자 `enemy_wisp`, 망각의 큰그림자 `boss_shadow` | `enemy_scratch` "스윽", `boss_wave` "웅— 쿵" | `hurt_shadow` "쉬익" | `death_shadow` unravel upward ending in a tiny bell: the forgotten becomes light again | airy, reversed, muffled |

Enemy sounds stay **below** ally sounds in brightness (QC: spectral centroid of a family's takes < the ally median) and level (tier B, ally crits A).

### 1.7 Mix philosophy
- **Hierarchy (what must always cut through):** (1) danger and state — boss windup, shop hit, low-HP heartbeat, lamp ready; (2) the player's own actions — taps, summons, lamp release, skill casts; (3) the auto-battle rhythm — hits, kills, enemy strikes; (4) the bed — ambience and BGM.
- **Space for SFX in the music:** battle tracks are arranged with an uncluttered 2–5 kHz band (no busy bright percussion) and sit at −18 LUFS-I; menu tracks may be fuller (−16).
- **Phone speakers:** every P0/P1 cue must keep its identity through a 400 Hz high-pass (QC "phone check", §7.8). Boss weight comes from cello/bow/felt-timpani harmonics, not sub bass.
- **Fatigue control:** short tails, shuffle bags (never the same take twice in a row), per-key gates, per-category budgets, a crowd aggregator (§5.6), music rotation with silent "rests" (§3.11).
- **Stereo:** SFX are mono, panned at runtime from the field x position (max ±0.35, bosses ±0.18, UI and shop centre). BGM and ambience are stereo but mono-compatible (correlation ≥ 0.2). Nothing hard-panned.

### 1.8 Loudness and format targets
Short clips are measured by **momentary-max loudness (M-max, 400 ms window, BS.1770)**, because integrated loudness is unreliable under 3 s. True peak is measured 4× oversampled. Files are normalised to their tier target; runtime trims only cut (an `AudioSource` cannot exceed volume 1), so the authored level is the loudest a key can be.

| Tier | Use | Measure | Target | Tol | Peak ceiling |
|---|---|---|---|---|---|
| S | hero one-shots: `lamp_lit`, `boss_appear`, `boss_death`, `ui_gacha_rare`, mythic reveal, milestones, evolve burst | M-max | −12 LUFS | ±1 | −1.0 dBTP |
| A | primary feedback: crits, casts, shop hit, summon, confirm, claim, stamp | M-max | −14 | ±1 | −1.0 |
| B | frequent combat: lineage hits, enemy strikes, deaths, statuses; mid UI | M-max | −17 | ±1 | −1.0 |
| C | micro: taps/ticks, launches, hurt layers, DOT ticks, coin land | M-max | −20 | ±1.5 | −1.0 |
| L | battle loops: heartbeat, boss windup | M-max | −18 | ±1 | −1.0 |
| J | jingles/stingers (stereo) | short-term max (3 s) | −13 | ±1 | −1.0 |
| M | battle-context BGM (battle_night, boss, dungeon) | integrated | −18 | ±0.5 | −1.5 (LRA 3–8 LU) |
| M+ | menu BGM (gacha, story, event); sleep −22 | integrated | −16 | ±0.5 | −1.5 |
| AMB | ambience loops (blackout/sleep −30, boss −28, festival −24) | integrated | −26 | ±1 | −3.0 |
| E | ambience emitters | M-max | −24 | ±2 | −3.0 |

`ui_tap` is authored in tier B because `PressScale` plays it at volume 0.5 (−6 dB). Defaults `BgmVolume 0.7` / `SfxVolume 0.8` give SFX a further +1.2 dB over the bed.

**Formats.** Renders: 48 kHz float; masters: 44.1 kHz 24-bit WAV in `scratchpad/audio/masters/` (outside `Assets`). Imported files: 44.1 kHz 16-bit WAV — SFX **mono**, BGM/ambience/jingles **stereo**. Import settings in §4.7. Estimated build size ≈ 26 MB (SFX ≈ 8 MB ADPCM, BGM ≈ 12 MB Vorbis, ambience ≈ 5 MB, jingles ≈ 1 MB); resident memory ≈ 13 MB SFX + the ambience layers in use (≈ 1 MB) + streaming buffers.

---

## 2. Complete sound list

### 2.0 Conventions
- **Key** = library key = the file stem before `_{NN}` (§4.1). Keys are lower-case, `_`-separated, and never end in a number token. Keys reserved by code are used verbatim:
  - `AttackFxCatalog` `Sfx` (`atk_needle` … `atk_musicbox`, `atk_generic`) is each lineage's **hit** key;
  - `EnemyFxCatalog` `Sfx` (`enemy_puff` … `boss_vortex`) is each enemy's **strike** key.
- **P** = production and implementation priority:
  - **P0** = the game is no longer silent and battle has 타격감 (54 keys);
  - **P1** = complete experience (148);
  - **P2** = polish, including the optional voice barks (104);
  - **P3** = reserved for features not built yet.
- **Bus · class:** the bus is Ui / Sfx / Stinger / Bgm / Amb / Loop (§3.3). The class sets the steal order: Critical > High > Normal > Low (§3.4).
- **Tier** = loudness tier (§1.8). **Len** = shipped length in seconds; for loops, the loop length.
- **Var** = takes shipped for round-robin. **Gen** = candidates rendered per key. QC and the user's ear keep the best Var (§7.8, §6).
- **Flags:**
  - `tuned`: retuned at runtime from the measured `rootHz`;
  - `ladder:*`: §1.3;
  - `inkey`: authored in the F-pentatonic world;
  - `loop`, `intro`, `override` (replaces a family default), `emitter`.
- **Gates** (min interval, max voices, pitch/volume jitter) per key are in `audio_catalog.json` (`gate`), derived from the rules in §3.5. Each key's production recipe is in §7.5.

### 2.1 UI (`UISfxKey` → library key, Ui bus)

| # | UISfxKey | Library key | P | Tier | Len s | Var | Gen | Sound | Hook (owner wires it) |
|---|---|---|---|---|---|---|---|---|---|
| 0 | Tap | `ui_tap` | P0 | B | 0.06 | 4 | 8 | '톡' wooden bead on felt (caller plays it at 0.5) | PressScale up-inside |
| 1 | Tab | `ui_tab` | P0 | B | 0.12 | 3 | 6 | '샥' paper card flick + tiny wood tap | MainTabController / IA dock |
| 2 | PopupOpen | `ui_popup_open` | P0 | B | 0.18 | 2 | 5 | felt swish up + tiny bell '딩' | UIPopup.Open |
| 3 | PopupClose | `ui_popup_close` | P0 | C | 0.14 | 2 | 5 | felt swish down, no bell | UIPopup.Close |
| 4 | Deny | `ui_deny` | P0 | B | 0.18 | 2 | 5 | '툭툭' two muted wooden knocks (never a buzzer) | Deny sites (CostButtonView, FeatureLockCover, TabGateView, EchoCardView) |
| 5 | CoinLand | `ui_coin_land` | P0 | C | 0.12 | 4 | 8 | sewing-button click + faint bell (runtime +3% ladder) `ladder:coin` | RewardFlyer arrive (단추), battle kill-coin arrival |
| 6 | GemLand | `ui_gem_land` | P0 | B | 0.25 | 3 | 6 | '찰랑' glass star charm | RewardFlyer arrive (별조각) |
| 7 | LevelUp | `ui_level_up` | P0 | A | 0.6 | 3 | 6 | celesta F-A-C rise '띠링' (hold-repeat safe) `inkey` | LevelUpFanfareView: 수선점 강화 |
| 8 | Stamp | `ui_stamp` | P0 | A | 0.3 | 2 | 5 | '꾹' rubber stamp + ink squish + twinkle | AttendancePopup stamp |
| 9 | Claim | `ui_claim` | P0 | A | 0.35 | 3 | 6 | handbell chime + buttons in a tin `inkey` | Claim buttons |
| 10 | GachaFlip | `ui_gacha_flip` | P0 | B | 0.15 | 3 | 6 | '팔락' card flip | GachaCardView flip (ShowFront moment) |
| 11 | GachaRare | `ui_gacha_rare` | P0 | S | 1.2 | 2 | 6 | Legendary reveal: music-box run + gold bell + glitter `inkey` | GachaCardView ShowFront, grade = Legendary |
| 12 | SheetOpen | `ui_sheet_open` | P0 | B | 0.32 | 2 | 5 | '스르륵' linen panel slides up + soft settle tap | IA sheet open |
| 13 | SheetClose | `ui_sheet_close` | P0 | C | 0.26 | 2 | 5 | linen slide down, lighter | IA sheet close |
| 14 | MainButton | `ui_main_button` | P0 | A | 0.45 | 2 | 5 | '퐁-딩' plush thump + round bell `inkey` | IA centre main button |
| 15 | Confirm | `ui_confirm` | P1 | A | 0.4 | 2 | 5 | wood tock + celesta C→F `inkey` | Confirm buttons / MessagePopup OK |
| 16 | Spend | `ui_spend` | P1 | B | 0.4 | 3 | 6 | '착착착' buttons into a glass jar | Currency spent (CostButtonView success) |
| 17 | Error | `ui_error` → `ui_deny` | P1 | — | — | — | — | alias (own trim, borrows clips) | UISfxKey.Error (error toast) — separately trimmable |
| 18 | Toast | `ui_toast` | P1 | C | 0.28 | 2 | 5 | '스윽' paper slip + tick | ToastView info |
| 19 | RedDotNew | `ui_red_dot` | P2 | C | 0.15 | 2 | 5 | '뽁' bead bloop + glint (gate 2 s) | RedDotView appears (new) |
| 20 | FeatureUnlock | `jingle_new_feature` | P1 | J | 2.2 | — | — | stinger, see §2.9b | FeatureUnlockCard (UISfxKey.FeatureUnlock) |
| 21 | QuestComplete | `ui_quest_complete` | P1 | A | 0.7 | 2 | 5 | pencil check + celesta '띠리링' `inkey` | GuideQuestBarView complete |
| 22 | MissionChest | `chest_open` | P1 | A | 1.2 | 2 | 5 | lid creak + latch + sparkle burst | MissionChestView, chapter/lost chest |
| 23 | StampSpecial | `ui_stamp_special` | P2 | S | 1.2 | 1 | 4 | big stamp + bell + confetti pop `inkey` | Attendance day 7/28, comeback day 3 |
| 24 | BasketClaim | `basket_claim` | P1 | A | 1.0 | 2 | 5 | wicker rustle + button cascade + sparkle | IdleChestPopup claim (수선 바구니) |
| 25 | QuickIdle | `quick_idle` | P1 | A | 1.2 | 2 | 5 | clockwork ticks accelerate → whoosh → chime `inkey` | 빠른 수선 |
| 26 | CountTick | `ui_count_tick` | P1 | C | 0.06 | 2 | 6 | TUNED glass-bead 'tink' (count ladder) `tuned, ladder:count` | ReturnPopup count-up, ≤1 per 70 ms |
| 27 | CountFinish | `ui_count_finish` | P1 | A | 0.5 | 2 | 5 | soft ding + button clink (lands on F) `inkey` | ReturnPopup count-up end |
| 28 | RewardDouble | `ui_reward_double` | P1 | S | 0.9 | 1 | 4 | shimmer sweep + two chimes a fifth apart '팅-팅!' `inkey` | ×2 claim (Return, basket, ads-free double) |
| 29 | PowerUp | `ui_power_up` | P1 | B | 0.45 | 2 | 5 | three sparkle notes up `inkey` | PowerUpFloatView '+N' |
| 30 | LevelUpMilestone | `ui_level_milestone` | P1 | S | 1.2 | 2 | 5 | glockenspiel + harp roll fanfare `inkey` | LevelUpFanfareView: every 10 levels / new evolution clue |
| 31 | FriendLevelUp | `ui_friend_level_up` | P1 | A | 0.6 | 2 | 5 | plush bounce + star sparkle | LevelUpFanfareView: Characters.LevelUp |
| 32 | ResonanceUp | `ui_resonance_up` | P1 | A | 1.0 | 2 | 5 | two bells a third apart + shimmer `inkey` | LevelUpFanfareView: ResonanceUp |
| 33 | EvolveCharge | `ui_evolve_charge` | P1 | A | 1.4 | 1 | 4 | thread winding onto spool + rising shimmer | EvolutionCraftPopup craft start |
| 34 | EvolveBurst | `ui_evolve_burst` | P1 | S | 0.8 | 2 | 5 | bright 'fwoom' + glass chime cluster `inkey` | Evolution transform flash |
| 35 | EvolveReveal | `jingle_evolution` | P1 | J | 3.0 | — | — | stinger, see §2.9b | Evolution reveal (UISfxKey.EvolveReveal) |
| 36 | CostumeEquip | `ui_costume_equip` | P1 | B | 0.6 | 3 | 6 | fabric swish + button snap + sparkle | CostumeItemView equip |
| 37 | CostumeUnlock | `ui_costume_unlock` | P2 | A | 0.8 | 2 | 5 | ribbon untie + chime `inkey` | CostumeUnlocked chip |
| 38 | GachaPullStart | `gacha_bundle_drop` | P1 | B | 0.5 | 2 | 5 | '폭' cloth bundle lands + string jiggle | Gacha pull start |
| 39 | GachaBundleOpen | `gacha_bundle_open` | P1 | A | 0.7 | 2 | 5 | string untie + magic swell | Gacha bundle opens |
| 40 | GachaCardAppear | `ui_gacha_card` | P1 | C | 0.15 | 3 | 6 | '뽁' paper card pops in | GachaCardView Pop (0.2 s) |
| 41 | GachaAnticipate | `ui_gacha_anticipate` | P1 | A | 0.5 | 2 | 5 | rising gold shimmer + card rattle | GachaCardView Anticipate (≥Legendary, 0.45 s) |
| 42 | GachaRevealCommon | `ui_gacha_reveal_common` | P1 | B | 0.35 | 2 | 5 | soft paper pop + tick | ShowFront ★1 |
| 43 | GachaRevealRare | `ui_gacha_reveal_rare` | P1 | B | 0.5 | 2 | 5 | pop + small glass chime `inkey` | ShowFront ★2 |
| 44 | GachaRevealEpic | `ui_gacha_reveal_epic` | P1 | A | 0.8 | 2 | 5 | chime pair + sparkle burst `inkey` | ShowFront ★3 |
| 45 | GachaRevealMythic | `ui_gacha_reveal_mythic` | P1 | S | 1.6 | 1 | 5 | music box finally plays its last note + glass harmonica + bells `inkey` | ShowFront ★5 (Mythic) |
| 46 | GachaNewFriend | `jingle_new_friend` | P1 | J | 2.0 | — | — | stinger, see §2.9b | Gacha new lineage (UISfxKey.GachaNewFriend) |
| 47 | GachaDuplicate | `ui_gacha_dup_melt` | P1 | B | 0.6 | 2 | 5 | glassy dissolve + crystal tink | Duplicate melts into 기억편 |
| 48 | GachaSkip | `ui_gacha_skip` | P2 | B | 0.5 | 2 | 4 | card riffle | GachaResultPopup skip |
| 49 | DragPick | `ui_drag_pick` | P1 | C | 0.15 | 2 | 5 | '뽀옥' plush lift | Formation drag start |
| 50 | DragDrop | `ui_drag_drop` | P1 | B | 0.2 | 3 | 6 | felt thud + button click | Formation drop / place |
| 51 | DragSwap | `ui_drag_swap` | P2 | B | 0.25 | 2 | 4 | two quick soft taps | Formation swap |
| 52 | ToggleOn | `ui_toggle_on` | P1 | C | 0.1 | 1 | 4 | bright wooden 'tik' | Switches on (MetaSwitch, settings) |
| 53 | ToggleOff | `ui_toggle_off` | P1 | C | 0.1 | 1 | 4 | lower wooden 'tok' | Switches off |
| 54 | SliderTick | `ui_slider_tick` | P1 | C | 0.06 | 2 | 4 | abacus bead tick (preview ≤1 per 150 ms) | Settings BGM/SFX sliders |
| 55 | ExpeditionDepart | `ui_expedition_depart` | P2 | A | 1.0 | 2 | 4 | plush pitter-patter + door bell | Expedition start |
| 56 | ExpeditionReturn | `ui_expedition_return` | P2 | A | 1.0 | 2 | 4 | sack thump + rustle + chime `inkey` | Expedition claim |
| 57 | DungeonSweep | `ui_dungeon_sweep` | P2 | A | 0.9 | 2 | 4 | whoosh + rapid button chinks | Dungeon sweep |
| 58 | MailClaim | `ui_mail_claim` | P2 | A | 0.8 | 2 | 4 | envelope unfold + buttons | Mail claim / claim all |
| 59 | Purchase | `ui_purchase` | P1 | A | 0.7 | 2 | 5 | toy-shop drawer + brass bell '딸랑' | Shop purchase success |
| 60 | CoachPop | `ui_coach_pop` | P1 | C | 0.2 | 2 | 5 | '뽀' soft bubble pop | CoachMarkView / SpeechBubbleView appear |
| 61 | TutorialStep | `ui_tutorial_step` | P1 | B | 0.4 | 2 | 4 | marimba C→F `inkey` | FTUE step done |
| 62 | FirstMeeting | `jingle_first_meeting` | P2 | J | 2.0 | — | — | stinger, see §2.9b | FirstMeetingCardView |
| 63 | Graduation | `jingle_graduation` | P2 | J | 3.0 | — | — | stinger, see §2.9b | GraduationCardPopup |
| 64 | StoryPage | `ui_story_page` | P1 | B | 0.6 | 3 | 6 | storybook page turn | StoryIntroView next page, storybook pages |
| 65 | SleepEnter | `ui_sleep_enter` | P1 | B | 1.2 | 1 | 4 | music box A-F-C falling + felt sigh (no voice) `inkey` | SleepModeView asleep |
| 66 | SleepWake | `ui_sleep_wake` | P1 | B | 0.5 | 1 | 4 | chime C→F + sparkle `inkey` | SleepModeView wake |
| 67 | SpeedToggle | `ui_speed_toggle` | P1 | B | 0.4 | 2 | 4 | '위잉' wind-up motor revs | Battle HUD 2× toggle |
| 68 | AutoToggle | `ui_auto_toggle` | P1 | B | 0.4 | 2 | 4 | clockwork ratchet + click | Battle HUD AUTO toggle |
| 69 | BossChallenge | `ui_boss_challenge` | P1 | A | 0.8 | 1 | 4 | toy snare roll + bell | 보스 도전 button |
| 70 | AchievementClaim | `achievement_claim` | P1 | A | 0.5 | 2 | 5 | medal clink + sparkle | Q-04 records claim |
| 71 | CodexDiscover | `ui_codex_discover` | P1 | A | 0.8 | 2 | 5 | album stamp + discovery sparkle | Q-05 codex discovery |
| 72 | AffinityUp | `ui_affinity_up` | P1 | A | 0.8 | 2 | 5 | heart-bell two notes + fluffy shimmer `inkey` | CR-10 affinity level up |
| 73 | PetSqueak | `pet_squeak` | P1 | B | 0.2 | 4 | 8 | soft plush squeak | CR-10 tap-to-pet |
| 74 | MapNode | `ui_map_node` | P2 | A | 0.6 | 2 | 4 | thread knot on a map pin + chime `inkey` | CR-05 chapter map node unlock |
| 75 | TierUp | `ui_tier_up` | P2 | A | 0.8 | 2 | 4 | ribbon flutter + rising chime `inkey` | Q-06 season tier up |
| 76 | BlessingPick | `ui_blessing_pick` | P2 | A | 0.7 | 2 | 4 | card slide shimmer | PRG-07 dungeon blessing pick |
| 77 | RewardBig | `ui_reward_big` | P2 | S | 1.2 | 2 | 4 | whoomp + bell + confetti `inkey` | Big reward popups |
| 78 | Countdown | `ui_countdown` | P2 | C | 0.12 | 2 | 4 | single clock tick | Timers 3-2-1 |
| 79 | ProgressFill | `ui_progress_fill` | P2 | B | 0.6 | 2 | 4 | little gear whirr rising | Progress bars filling |
| 80 | DefeatAdvice | `ui_defeat_advice` | P2 | B | 0.6 | 1 | 4 | marimba fall-rise 'hmm' (no voice) `inkey` | CR-01 defeat ring/advice |

### 2.2 Battle — ally attacks (Sfx bus)

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `fire_needle` | P0 | Sfx · Low | C | 0.15 | 4 | 8 | '슉' thread zip + needle glint | GuardianAttack t=0 (jjakjjak) |
| `atk_needle` | P0 | Sfx · Normal | B | 0.22 | 5 | 10 | '칭+퐁' needle ping + felt pop + 2 stitch ticks | Hit at projectile arrival (jjakjjak) |
| `atk_needle_crit` | P1 | Sfx · High | A | 0.35 | 3 | 6 | '싹뚝' scissor snip + ringing needle | Crit at arrival (jjakjjak) |
| `fire_button` | P0 | Sfx · Low | C | 0.15 | 4 | 8 | '톡' thumb flick + short whirr | GuardianAttack t=0 (ttokdan) |
| `atk_button` | P0 | Sfx · Normal | B | 0.18 | 5 | 10 | '딸깍' hollow button clack | Hit at arrival (ttokdan) |
| `atk_button_crit` | P1 | Sfx · High | A | 0.32 | 3 | 6 | '딱!' big coat button + small-button rattle | Crit (ttokdan) |
| `atk_button_bounce` | P2 | Sfx · Low | C | 0.4 | 3 | 6 | '또르르' three tiny buttons bounce (Extra=Buttons) | Hit +0.05 s, 50 % |
| `fire_key` | P0 | Sfx · Low | C | 0.12 | 4 | 8 | '칭—' bright key zip | GuardianAttack t=0 (chalkak) |
| `atk_key` | P0 | Sfx · Normal | B | 0.2 | 5 | 10 | '찰칵' two-stage latch + tumbler rattle | Hit (chalkak) |
| `atk_key_crit` | P1 | Sfx · High | A | 0.35 | 3 | 6 | '철컥!' deadbolt + bell shimmer | Crit (chalkak) |
| `fire_candle` | P0 | Sfx · Low | C | 0.18 | 4 | 8 | '화륵' flame flick | GuardianAttack t=0 (mongdang) |
| `atk_candle` | P0 | Sfx · Normal | B | 0.3 | 5 | 10 | '후앙' flame bloom + wax plop | Hit (mongdang) |
| `atk_candle_crit` | P1 | Sfx · High | A | 0.4 | 3 | 6 | flame flare + ember crackle | Crit (mongdang) |
| `atk_candle_drip` | P2 | Sfx · Low | C | 0.2 | 3 | 6 | '똑' round wax drip (Extra=Wax) | Hit +0.12 s, 50 % |
| `fire_rain` | P0 | Sfx · Low | C | 0.15 | 4 | 8 | '펑' canopy flick + water flick | GuardianAttack t=0 (udukeoni) |
| `atk_rain` | P0 | Sfx · Normal | B | 0.25 | 5 | 10 | '톡+찰방' drop + small splash | Hit (udukeoni) |
| `atk_rain_crit` | P1 | Sfx · High | A | 0.4 | 3 | 6 | water crown splash + droplet ring | Crit (udukeoni) |
| `atk_rain_big` | P2 | Sfx · Normal | B | 0.4 | 3 | 6 | rain-stage bigger splash + ripple (replaces hit on Rain) | Hit on Rain stages |
| `fire_kiss` | P0 | Sfx · Low | C | 0.15 | 4 | 8 | lipstick twist + cap click | GuardianAttack t=0 (rouge) |
| `atk_kiss` | P0 | Sfx · Normal | B | 0.15 | 5 | 10 | '쪽' cork-like smack-pop (never a human mouth) | Hit (rouge) |
| `atk_kiss_crit` | P1 | Sfx · High | A | 0.3 | 3 | 6 | bigger pop + heart sparkle burst | Crit (rouge) |
| `fire_musicbox` | P0 | Sfx · Low | C | 0.15 | 3 | 6 | '끼릭' wind-up key ratchet | GuardianAttack t=0 (lalabell) |
| `atk_musicbox` | P0 | Sfx · Normal | B | 0.4 | 2 | 8 | TUNED single comb note — lullaby ladder, 7th note always missing `tuned, ladder:lullaby` | Hit (lalabell) |
| `atk_musicbox_ring` | P1 | Sfx · Low | C | 0.3 | 3 | 6 | soft sound-ring shimmer under the note (Extra=SoundRing) | Hit layer (lalabell) |
| `atk_musicbox_crit` | P1 | Sfx · High | A | 0.6 | 2 | 6 | TUNED three-tine F-major chord `tuned` | Crit (lalabell) |
| `atk_musicbox_finale` | P1 | Sfx · High | S | 0.9 | 2 | 6 | TUNED the resolving last note + warm bell bloom `tuned` | 7th hit / crit of lala_finale only |
| `atk_generic` | P0 | Sfx · Normal | B | 0.15 | 3 | 6 | '뽁' lavender orb pop | Hit (unknown lineage) |
| `fire_generic` | P1 | Sfx · Low | C | 0.12 | 3 | 6 | soft orb whoosh | Launch (unknown lineage) |
| `hit_crit` | P0 | Sfx · High | A | 0.2 | 3 | 8 | '팅!' gold star crystal (universal crit layer) `inkey` | Every crit, ≤1 per 0.12 s |
| `atk_layer_slash` | P1 | Sfx · Low | C | 0.15 | 3 | 6 | '샥' felt slash swish | Assault style, at hit |
| `atk_layer_snipe` | P1 | Sfx · Low | C | 0.15 | 3 | 6 | '쮸웅' thin bright zip streak | Snipe style, at launch |
| `atk_layer_thread` | P1 | Sfx · Low | C | 0.25 | 3 | 6 | '띠잉' taut thread twang | Capture style, at hit |
| `atk_layer_heavy` | P1 | Sfx · Normal | B | 0.15 | 3 | 6 | '퉁' felt mallet body thump (T1) | Snipe/Assault hit, enemy shield break |
| `atk_layer_gold` | P2 | Sfx · Low | C | 0.25 | 3 | 6 | tiny gold twinkle | Tier-2 forms, 35 % of hits |
| `echo_slash` | P1 | Sfx · Low | C | 0.15 | 4 | 8 | '슈욱' glassy light swish | EchoAttack melee t=0 |
| `echo_token_pop` | P1 | Sfx · Low | C | 0.15 | 3 | 6 | '퐁' sock token pop | EchoAttack by token |
| `hit_multi` | P1 | Sfx · Normal | B | 0.3 | 3 | 6 | cluster of 4–6 tiny pops in 120 ms | Crowd aggregator (≥4 hits in 60 ms) |
| `hit_skill` | P1 | Sfx · Low | C | 0.15 | 4 | 8 | '톡-샤' small magic impact | Skill damage per target (first 3) |
| `hit_dot` | P1 | Sfx · Low | C | 0.15 | 4 | 8 | '탁' ember crackle | Burn ticks (≤1 per 0.5 s per enemy) |
| `hit_lamp` | P1 | Sfx · Normal | B | 0.35 | 2 | 8 | TUNED warm celesta note (lamp wave arpeggio) `tuned, ladder:lamp` | Lamp wave reaches each enemy |
| `shield_block` | P1 | Sfx · Normal | B | 0.15 | 3 | 6 | '붕' hex bubble deflect | Enemy shield fully absorbs a hit |

### 2.3 Battle — enemies and bosses (Sfx bus)

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `hurt_dust` | P1 | Sfx · Low | C | 0.12 | 4 | 8 | '푸' dry dust puff | Hit layer, dust family |
| `hurt_moth` | P1 | Sfx · Low | C | 0.12 | 4 | 8 | '파사' papery wing flick | Hit layer, moth family |
| `hurt_metal` | P1 | Sfx · Low | C | 0.12 | 4 | 8 | '똥' dull tin ping | Hit layer, metal family |
| `hurt_shadow` | P1 | Sfx · Low | C | 0.12 | 4 | 8 | '쉬익' cold dark air | Hit layer, shadow family |
| `death_dust` | P0 | Sfx · Normal | B | 0.5 | 4 | 8 | poof + cream sparkles rising ('잊힘이 기억으로') | Kill pop, 먼지뭉치 / 실뭉치 |
| `death_moth` | P0 | Sfx · Normal | B | 0.6 | 3 | 6 | paper wing tear + powder drift | Kill pop, 좀나방 / 여왕 |
| `death_metal` | P0 | Sfx · Normal | B | 0.5 | 4 | 8 | '짤그랑' screws, gears, springs scatter + one bounce | Kill pop, 녹얼룩 / 골무 / 태엽쥐 |
| `death_shadow` | P0 | Sfx · Normal | B | 0.7 | 3 | 6 | '스르르' unravel upward + light-orb bell | Kill pop, 그림자 |
| `death_tangle` | P2 | Sfx · Normal | B | 0.5 | 3 | 6 | yarn unravel + soft pop (override of dust) `override` | Kill pop, enemy_tangle |
| `death_windup` | P2 | Sfx · Normal | B | 0.6 | 3 | 6 | spring '보잉' + gear clatter (override of metal) `override` | Kill pop, enemy_windup |
| `enemy_puff` | P0 | Sfx · Normal | B | 0.3 | 4 | 8 | '푸슉' dust clump | EnemyStrike enemy_dust |
| `enemy_flutter` | P0 | Sfx · Normal | B | 0.3 | 4 | 8 | '파닥파닥' papery wings | EnemyStrike enemy_moth |
| `enemy_splat` | P0 | Sfx · Normal | B | 0.35 | 4 | 8 | '철퍽' rusty wet splat + scrape | EnemyStrike enemy_rust |
| `enemy_whip` | P0 | Sfx · Normal | B | 0.3 | 4 | 8 | '휙-탁' yarn whip | EnemyStrike enemy_tangle |
| `enemy_scratch` | P0 | Sfx · Normal | B | 0.3 | 4 | 8 | '스윽' three shadow scratches | EnemyStrike enemy_wisp |
| `enemy_clang` | P0 | Sfx · Normal | B | 0.3 | 4 | 8 | '깡' felt-muted thimble clang | EnemyStrike enemy_thimble |
| `enemy_boing` | P0 | Sfx · Normal | B | 0.35 | 4 | 8 | '끼릭-보잉' ratchet + spring | EnemyStrike enemy_windup |
| `boss_wave` | P0 | Sfx · High | A | 1.2 | 2 | 6 | '웅— 쿵' shadow wave swell + muffled thump | EnemyStrike boss_shadow |
| `boss_vortex` | P0 | Sfx · High | A | 1.0 | 2 | 6 | wing-powder vortex swirl | EnemyStrike boss_mothqueen |
| `enemy_spawn` | P1 | Sfx · Low | C | 0.35 | 4 | 8 | '스르' dusty swirl in | EnemySpawned (≤1 per 0.3 s) |
| `enemy_split` | P1 | Sfx · Normal | B | 0.4 | 3 | 6 | '퐁퐁' yarn ball splits in two | EnemySplit |
| `enemy_reveal` | P1 | Sfx · Normal | B | 0.5 | 2 | 5 | veil lifted shimmer | EnemyRevealed / Reveal status |
| `enemy_enrage` | P1 | Sfx · High | A | 0.6 | 2 | 5 | angry cloth rustle + rising bowed scrape | EnemyEnraged (normal enemy) |
| `enemy_gnaw` | P1 | Sfx · Low | C | 0.3 | 3 | 6 | '사각' nibble tear in fabric | 좀나방 갉음 (FabricEater on fabric echo) |
| `enemy_rust` | P1 | Sfx · Low | C | 0.4 | 3 | 6 | '찌익' rust stain fizz | Rusting debuff applied to an ally |
| `enemy_mend` | P2 | Sfx · Low | C | 0.5 | 2 | 5 | '끼릭끼릭' winding another enemy | Mender/Hastener aura (≤1 per 0.9 s) |
| `boss_appear` | P0 | Sfx · Critical | S | 1.6 | 2 | 6 | low cello swell + felt timpani + reversed whoosh | BossSpawned (with jingle_boss_appear) |
| `boss_windup` | P1 | Sfx · Critical | L | 1.0 | 1 | 5 | seamless creak + bowed-string tension texture (runtime volume/pitch = SiegeWindup) `loop` | Boss siege windup 0→1 |
| `boss_siege_hit` | P1 | Sfx · Critical | A | 0.6 | 2 | 5 | heavy shadow thud on the door + rattle | Boss EnemyStrike on the shop (layer) |
| `boss_enrage` | P1 | Sfx · Critical | S | 1.0 | 1 | 4 | big rising bowed swell + rattling dust (no voice) | EnemyEnraged (boss) |
| `boss_summon` | P1 | Sfx · High | A | 0.8 | 2 | 5 | vortex opening rumble | Boss minion summon |
| `boss_lamp_drain` | P1 | Sfx · High | A | 0.8 | 2 | 5 | '슈우욱' light sucked into dark + fading chime | LampDrained (≤1 per 0.6 s) |
| `boss_death` | P0 | Sfx · Critical | S | 2.0 | 2 | 6 | shadow unravels into a cascade of glass bells | Boss kill pop |
| `boss_kill_impact` | P1 | Sfx · Critical | S | 0.5 | 2 | 5 | freeze-frame '쿵' felt timpani + soft glass crack | Boss kill moment (global hit-stop start) |

### 2.4 Battle — shop, shields, support, echoes, lamp

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `shop_hit` | P0 | Sfx · High | A | 0.4 | 4 | 8 | '쿵-삐걱' door knock + wood creak | EnemyStrike on the shop (≤1 per 0.12 s) |
| `shop_hit_boss` | P1 | Sfx · Critical | S | 0.6 | 2 | 5 | deep knock + rattling shelves | Boss strike on the shop |
| `shop_shield_hit` | P1 | Sfx · Normal | B | 0.3 | 3 | 6 | '부웅' dome absorbs (muffled) | Shop strike fully absorbed by shield |
| `shop_heal` | P1 | Sfx · Normal | B | 0.5 | 2 | 5 | warm house creak + healing chime `inkey` | ShopHealed (≤1 per 0.55 s) |
| `shop_low_enter` | P1 | Sfx · Critical | A | 0.8 | 1 | 4 | two soft descending warning bells `inkey` | Shop HP crosses below 30 % (≤1 per 20 s) |
| `shop_heartbeat` | P1 | Loop · Critical | L | 1.0 | 1 | 4 | '통…통' wooden heartbeat loop, 60 BPM `loop` | Shop HP < 30 % (loop, speeds up to ×1.25) |
| `shield_gain_button` | P1 | Sfx · Normal | B | 0.5 | 2 | 5 | big button clack + dome hum up | ShieldGained, vfx_button_shield |
| `shield_gain_umbrella` | P1 | Sfx · Normal | B | 0.5 | 2 | 5 | '팡' umbrella opens + drops slide | ShieldGained, vfx_umbrella_shield |
| `shield_gain_bubble` | P1 | Sfx · Normal | B | 0.5 | 2 | 5 | '뽀옹' soap bubble forms | ShieldGained, other keys |
| `shield_break` | P1 | Sfx · High | A | 0.4 | 3 | 6 | '쨍' soft bubble shatter + tinkles | ShieldBroken (shop/echo/enemy) |
| `pulse_support` | P1 | Sfx · Normal | B | 0.5 | 2 | 5 | ribbon flutter + rising twinkle `inkey` | GuardianPulse 'support' |
| `pulse_heal` | P1 | Sfx · Normal | B | 0.6 | 2 | 5 | soft harp arpeggio swell, minty `inkey` | GuardianPulse 'heal' |
| `heal_tick` | P1 | Sfx · Low | C | 0.25 | 3 | 6 | '링' tiny glimmer `inkey` | Heal numbers (shop/echo) |
| `echo_summon` | P0 | Sfx · High | A | 0.4 | 3 | 6 | '샤랑' glassy light + tiny door bell | EchoSummoned |
| `echo_dissolve` | P0 | Sfx · Normal | B | 0.5 | 3 | 6 | reverse shimmer flowing home + tiny chime | EchoDissolved |
| `echo_hurt` | P1 | Sfx · Low | C | 0.15 | 4 | 8 | '치익' soft glassy fizz | EnemyStrike on an echo (layer) |
| `token_summon` | P1 | Sfx · Normal | B | 0.4 | 2 | 5 | summon circle pop + '퐁퐁' socks | TokenSummoned |
| `echo_refund` | P2 | Sfx · Low | C | 0.25 | 2 | 5 | tiny '링' of light arriving `inkey` | EchoDissolved refund > 0 (+0.35 s) |
| `lamp_tick` | P1 | Sfx · Low | C | 0.25 | 2 | 6 | TUNED glass bell tink (gauge 25/50/75 %) `tuned, ladder:gauge` | Lamp gauge thresholds |
| `lamp_full` | P1 | Sfx · High | A | 1.0 | 2 | 5 | '딩-샤라' warm bell + sparkle (lantern ready) `inkey` | Lamp gauge full |
| `lamp_lit` | P0 | Sfx · Critical | S | 2.2 | 2 | 6 | match strike → '후웅' flame → glowing bell chord `inkey` | LampLit |
| `lamp_wave` | P1 | Sfx · High | A | 0.6 | 2 | 5 | warm light wave whoosh outward | Lamp wave start (+LampWave.Start) |

### 2.5 Battle — skills (style fallbacks + per-vfx)

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `skill_charge` | P1 | Sfx · Low | C | 0.35 | 3 | 6 | '샤아아' sparkles gather (short riser) | SkillCast t=0 (echo: −4 dB, +2 st) |
| `skill_cast` | P1 | Sfx · Normal | B | 0.4 | 3 | 6 | generic cast whoosh + sparkle (fallback) | SkillCast with no style/vfx clip |
| `skill_big` | P1 | Sfx · High | A | 1.0 | 2 | 5 | soft gong + long shimmer | Big skill name notice (≤1 per 30 s per skill) |
| `skill_gift` | P2 | Sfx · Low | C | 0.3 | 3 | 6 | '휘리릭' gift twinkle flies | Ally-group gift (≤3 per cast) |
| `skill_fizzle` | P2 | Sfx · Low | C | 0.4 | 2 | 5 | '피슉' sad fizzle | Ally-group cast with no recipient |
| `skill_style_projectile` | P1 | Sfx · Normal | B | 0.5 | 2 | 5 | launch whoosh + sparkle trail (impacts = hit_skill at arrival) | SkillCast fallback for SkillVfxStyle.Projectile |
| `skill_style_beam` | P1 | Sfx · Normal | A | 0.6 | 2 | 5 | '시잉' sustained light sweep | SkillCast fallback for SkillVfxStyle.Beam |
| `skill_style_burst` | P1 | Sfx · Normal | A | 0.5 | 2 | 5 | '후웅' round bloom + scatter | SkillCast fallback for SkillVfxStyle.Burst |
| `skill_style_ring` | P1 | Sfx · Normal | B | 0.6 | 2 | 5 | expanding floor shimmer | SkillCast fallback for SkillVfxStyle.Ring |
| `skill_style_ground` | P1 | Sfx · Normal | B | 0.7 | 2 | 5 | gooey spread + shimmer | SkillCast fallback for SkillVfxStyle.Ground |
| `skill_style_dome` | P1 | Sfx · Normal | B | 0.6 | 2 | 5 | humming swell, glassy top | SkillCast fallback for SkillVfxStyle.Dome |
| `skill_style_rise` | P1 | Sfx · Normal | B | 0.7 | 2 | 5 | rising twinkles | SkillCast fallback for SkillVfxStyle.Rise |
| `skill_style_overhead` | P1 | Sfx · Normal | C | 0.4 | 2 | 5 | quick pop + twinkle | SkillCast fallback for SkillVfxStyle.Overhead |
| `skill_style_orbit` | P1 | Sfx · Normal | B | 0.6 | 2 | 5 | circling 'wing-wing' whoosh | SkillCast fallback for SkillVfxStyle.Orbit |
| `skill_style_rain` | P1 | Sfx · Normal | B | 0.8 | 2 | 5 | sparkly drop patter | SkillCast fallback for SkillVfxStyle.Rain |
| `skill_thread_stitch` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | arc swish with rapid stitch clicks | SkillCast vfx_thread_stitch (Projectile) |
| `skill_patch_heal` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | patch press + warm heart chime | SkillCast vfx_patch_heal (Rise) |
| `skill_button_shield` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | giant button snaps into a shield | SkillCast vfx_button_shield (Dome) |
| `skill_spin_button` | P2 | Sfx · Normal | B | 0.7 | 2 | 4 | saw-like whirr with clicking edge | SkillCast vfx_spin_button (Orbit) |
| `skill_keyhole_lock` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | heavy lock turns, rings | SkillCast vfx_keyhole_lock (OverHead) |
| `skill_key_beam` | P2 | Sfx · Normal | B | 0.4 | 2 | 4 | bright metallic zing | SkillCast vfx_key_beam (Projectile) |
| `skill_candle_burst` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | flame petals bloom + crackle | SkillCast vfx_candle_burst (Burst) |
| `skill_wax_drip` | P2 | Sfx · Normal | B | 0.7 | 2 | 4 | drips + gooey spread | SkillCast vfx_wax_drip (Ground) |
| `skill_firefly` | P2 | Sfx · Normal | B | 0.8 | 2 | 4 | blinking firefly chimes | SkillCast vfx_firefly (Rise) |
| `skill_lantern_beam` | P2 | Sfx · Normal | B | 0.7 | 2 | 4 | warm swelling light sweep | SkillCast vfx_lantern_beam (Beam) |
| `skill_umbrella_shield` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | fabric whoosh + pop | SkillCast vfx_umbrella_shield (Dome) |
| `skill_raindrops` | P2 | Sfx · Normal | B | 0.9 | 2 | 4 | patter on fabric | SkillCast vfx_raindrops (Rain) |
| `skill_storm_swirl` | P2 | Sfx · Normal | B | 0.8 | 2 | 4 | whirling wind + spray | SkillCast vfx_storm_swirl (Ring) |
| `skill_lipstick_heart` | P2 | Sfx · Normal | B | 0.4 | 2 | 4 | pop + heart ping | SkillCast vfx_lipstick_heart (OverHead) |
| `skill_kiss_burst` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | several cute pops + sparkle | SkillCast vfx_kiss_burst (Burst) |
| `skill_gloss_shine` | P2 | Sfx · Normal | B | 0.5 | 2 | 4 | smooth bright glint | SkillCast vfx_gloss_shine (Burst) |
| `skill_music_notes` | P2 | Sfx · Normal | B | 0.7 | 2 | 4 | short twinkly music-box run | SkillCast vfx_music_notes (Rise) |
| `skill_sound_ring` | P2 | Sfx · Normal | B | 0.7 | 2 | 4 | resonant hum, shimmering edge | SkillCast vfx_sound_ring (Ring) |
| `skill_waltz_ribbon` | P2 | Sfx · Normal | B | 0.8 | 2 | 4 | flowing swirl + chime | SkillCast vfx_waltz_ribbon (Orbit) |
| `skill_star_burst` | P2 | Sfx · Normal | B | 0.5 | 2 | 4 | gold stars pop + glitter | SkillCast vfx_star_burst (Burst) |
| `skill_moon_glow` | P2 | Sfx · Normal | B | 0.8 | 2 | 4 | calm glassy swell | SkillCast vfx_moon_glow (Ground) |
| `skill_heart_heal` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | warm soft chime + sparkle | SkillCast vfx_heart_heal (Rise) |
| `skill_buff_crown` | P2 | Sfx · Normal | B | 0.5 | 2 | 4 | bright rising twinkle | SkillCast vfx_buff_crown (OverHead) |
| `skill_debuff_mark` | P2 | Sfx · Normal | B | 0.5 | 2 | 4 | descending wobbly tone | SkillCast vfx_debuff_mark (OverHead) |
| `skill_stun_stars` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | spinning 'ting-ting' stars | SkillCast vfx_stun_stars (OverHead) |
| `skill_slow_yarn` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | stretchy elastic web | SkillCast vfx_slow_yarn (Ground) |
| `skill_summon_circle` | P2 | Sfx · Normal | B | 0.7 | 2 | 4 | rising shimmer + click | SkillCast vfx_summon_circle (Ground) |
| `skill_shield_bubble` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | soft round bloop + shimmer | SkillCast vfx_shield_bubble (Dome) |
| `skill_cleanse_sparkle` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | fresh bright wash | SkillCast vfx_cleanse_sparkle (Rise) |
| `skill_dust_poof` | P2 | Sfx · Normal | B | 0.4 | 2 | 4 | dry poof | SkillCast vfx_dust_poof (Burst) |
| `skill_shadow_burst` | P2 | Sfx · Normal | B | 0.6 | 2 | 4 | muffled dark whoosh | SkillCast vfx_shadow_burst (Burst) |
| `skill_split_yarn` | P2 | Sfx · Normal | B | 0.4 | 2 | 4 | woolly pop | SkillCast vfx_split_yarn (Burst) |
| `skill_reveal_light` | P2 | Sfx · Normal | B | 0.7 | 2 | 4 | expanding bright shimmer | SkillCast vfx_reveal_light (Ring) |
| `skill_thread_stitch_hit` | P2 | Sfx · Normal | B | 0.3 | 2 | 4 | needle pins into cloth, stitch pull | Thread stitch arrival (0.38 s) |
| `skill_key_beam_hit` | P2 | Sfx · Normal | B | 0.3 | 2 | 4 | light key locks in, bright ring | Key beam arrival (0.26 s) |
| `skill_lantern_beam_hit` | P2 | Sfx · Normal | B | 0.3 | 2 | 4 | warm bright flare | Lantern beam reach (BeamReach) |

### 2.6 Battle — status effects

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `status_burn` | P1 | Sfx · Low | C | 0.4 | 2 | 5 | '화르륵' ember catches | DamageOverTime applied |
| `status_slow` | P1 | Sfx · Low | C | 0.4 | 2 | 5 | '쭈욱' sticky yarn web | Slow applied |
| `status_stun` | P1 | Sfx · Low | B | 0.5 | 2 | 5 | lock clicks + dizzy stars | Stun applied |
| `status_mark` | P1 | Sfx · Low | B | 0.4 | 2 | 5 | '쪽' mark pop + heart ping | Mark applied |
| `status_knockback` | P1 | Sfx · Low | C | 0.3 | 2 | 5 | '퍽' dusty shove | Knockback applied |
| `status_pull` | P1 | Sfx · Low | C | 0.5 | 2 | 5 | '휘이익' inward swirl | Pull applied |
| `status_buff` | P1 | Sfx · Low | B | 0.5 | 2 | 5 | '띠링' crown twinkle up `inkey` | BuffAttack / BuffAttackSpeed on allies |
| `status_debuff` | P1 | Sfx · Low | C | 0.4 | 2 | 5 | soft sour descending toy horn | Debuff mark on enemies |
| `status_cleanse` | P1 | Sfx · Low | B | 0.5 | 2 | 5 | '샤라랑' clean sparkle wash | Cleanse |
| `status_resonance` | P1 | Sfx · Low | C | 0.3 | 2 | 5 | tuning-bell hum chime `inkey` | ResonanceGain |
| `status_lamp_gain` | P1 | Sfx · Low | C | 0.3 | 2 | 5 | '또롱' firefly glow chime `inkey` | LampGain |
| `status_chain` | P1 | Sfx · Low | C | 0.15 | 2 | 6 | TUNED '찡' spark hop `tuned, ladder:chain` | Chain hop arrival (ladder per hop) |

### 2.7 Battle — flow, combo, rewards, stage modifiers

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `battle_stage_start` | P1 | Sfx · High | A | 1.0 | 2 | 5 | '딸랑' shop door bell + night-air whoosh | StageChanged |
| `battle_wave` | P1 | Sfx · Normal | B | 0.6 | 2 | 5 | '댕' small distant bell `inkey` | sim.Wave increments (wave ≥ 2) |
| `battle_synergy` | P1 | Sfx · High | A | 0.9 | 2 | 5 | '링-링-링' three linked chimes `inkey` | Synergy banner (first/changed) |
| `battle_step_back` | P2 | Sfx · Normal | B | 0.8 | 1 | 4 | two soft descending wooden notes `inkey` | BattleSystem.StepBack |
| `kill_chain` | P1 | Sfx · Normal | C | 0.3 | 2 | 8 | TUNED glass chime (pentatonic kill ladder) `tuned, ladder:kill` | Kill pop when streak ≥ 2 within 0.8 s |
| `kill_multi` | P1 | Sfx · High | A | 0.7 | 2 | 5 | '또로롱' harp gliss up `inkey` | ≥5 kills within 1.0 s |
| `coin_pop` | P1 | Sfx · Low | C | 0.25 | 3 | 6 | '톡' button pops out + twinkle | Every 5th kill (KillCoin) +0.08 s |
| `twinkle` | P2 | Sfx · Low | C | 0.4 | 3 | 6 | cream sparkle shower | Reward shower / clear burst |
| `mod_rain` | P2 | Sfx · Normal | A | 1.5 | 1 | 4 | distant soft thunder roll + rain onset | StageModifierStarted (rain) |
| `mod_wind` | P2 | Sfx · Normal | A | 1.5 | 1 | 4 | gust sweep + wind chimes | StageModifierStarted (wind) |
| `mod_blackout` | P2 | Sfx · Normal | A | 1.5 | 1 | 4 | lights power down hum + click + hush | StageModifierStarted (blackout) |
| `mod_fullmoon` | P2 | Sfx · Normal | A | 1.5 | 1 | 4 | glassy moonlight swell + chime | StageModifierStarted (fullmoon) |
| `mod_dust` | P2 | Sfx · Normal | A | 1.5 | 1 | 4 | dry sandy whoosh + papery debris | StageModifierStarted (dust) |
| `mod_festival` | P2 | Sfx · Normal | A | 1.5 | 1 | 4 | paper poppers + tiny toy party horn | StageModifierStarted (festival) |

### 2.8 Ambience loops (Amb bus, stereo)

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `amb_night` | P0 | Amb · Critical | AMB | 40 | 1 | 3 | night street bed: breeze, far crickets, sign creak, rare wind chime `loop` | Battle/map always (base) |
| `amb_rain` | P1 | Amb · Critical | AMB | 40 | 1 | 3 | steady gentle rain on stones and awning, gutter drips `loop` | StageModifier Rain |
| `amb_wind` | P1 | Amb · Critical | AMB | 40 | 1 | 3 | alley gusts, paper lanterns, loose shutter tap `loop` | StageModifier Wind |
| `amb_dust` | P1 | Amb · Critical | AMB | 40 | 1 | 3 | dry sandy wind, skittering paper debris, hazy `loop` | StageModifier DustStorm |
| `amb_blackout` | P1 | Amb · Critical | AMB | 40 | 1 | 3 | hush: near silence, faint low hum, far moth flutter `loop` | StageModifier Blackout |
| `amb_fullmoon` | P1 | Amb · Critical | AMB | 40 | 1 | 3 | crystalline shimmer, soft insects, still air `loop` | StageModifier FullMoon |
| `amb_festival` | P1 | Amb · Critical | AMB | 40 | 1 | 3 | distant soft fireworks, lanterns, tiny bells `loop` | StageModifier Festival |
| `amb_sleep` | P2 | Amb · Critical | AMB | 40 | 1 | 3 | very quiet crickets + slow breeze `loop` | Sleep mode |
| `amb_dungeon` | P2 | Amb · Critical | AMB | 40 | 1 | 3 | hollow storeroom: slow drips, far creaks, cool air `loop` | Dungeon screen |
| `amb_boss` | P2 | Amb · Critical | AMB | 20 | 1 | 3 | low uneasy shadow drone, slow pulse `loop` | Boss alive (layer under the bed) |

### 2.8b Ambience emitters (random one-shots on the Amb emitter voice)

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `amb_emit_night` | P2 | Amb · Low | E | 3.0 | 6 | 12 | one distant detail: clock-tower bell, sign creak, moth on lamp, cat-flap clack `emitter` | Every 12–30 s on night bed |
| `amb_emit_rain` | P2 | Amb · Low | E | 3.0 | 4 | 8 | drip into tin bucket, gutter gurgle, far thunder `emitter` | Every 12–30 s on rain bed |
| `amb_emit_wind` | P2 | Amb · Low | E | 3.0 | 4 | 8 | one wind-chime phrase `emitter` | Every 12–30 s on wind bed |
| `amb_emit_festival` | P2 | Amb · Low | E | 3.0 | 4 | 8 | one far firework pop + crackle `emitter` | Every 12–30 s on festival bed |

### 2.9 Music loops (Bgm bus, stereo)

| Key | P | Takes (pieces) | Gen | Key/tempo | Bars | Loop s | Tier | Use |
|---|---|---|---|---|---|---|---|---|
| `bgm_battle_night` | P0 | 3 | 12 | F major (01), F major waltz (02), D minor→F (03) · 92 / 132 (3/4) / 100 BPM | 32 / 48 / 32 | 83.48 / 65.45 / 76.8 | M | 3 rotation pieces: 'Lantern Plaza' / 'Thread & Needle Waltz' / 'Midnight Mending March' — Main (map + battle) |
| `bgm_boss` | P1 | 1 | 4 | D minor · 116 BPM | 24 | 49.66 | M | dark storybook toy march (chapter bosses x-10 only) — Chapter boss alive |
| `bgm_dungeon` | P1 | 1 | 4 | D Dorian · 100 BPM | 32 | 76.8 | M | mysterious storeroom of forgotten things — Dungeon screen |
| `bgm_gacha` | P1 | 1 | 4 | Bb major · 84 BPM | 16 (+2 intro) | 45.71 | M+ | 'wishing' theme, harp + celesta, anticipation — Gacha panel / result |
| `bgm_gacha_intro` | P1 | 1 | 4 | Bb major · 84 BPM | 2 (intro) | 5.71 | M+ | 2-bar music-box intro of the wishing theme (plays once) — Gacha state entry; bgm_gacha is PlayScheduled at its exact end |
| `bgm_story` | P1 | 1 | 4 | F major · 72 BPM | 24 | 80.0 | M+ | title/intro storybook theme carrying the 7-note lullaby — StoryIntroView / title |
| `bgm_event` | P2 | 1 | 4 | C major · 104 BPM | 32 | 73.85 | M+ | '별이 머문 밤 산책' playful starry walk — Event hub / event shop |
| `bgm_sleep` | P2 | 1 | 3 | F major (3/4) · 60 BPM | 32 | 96.0 | M+ | sparse music-box lullaby (−22 LUFS) — Sleep mode (optional; otherwise muffled main) |

### 2.9b Jingles / stingers (Stinger bus, stereo)

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `jingle_stage_clear` | P0 | Stinger · Critical | J | 2.2 | 1 | 4 | celesta + harp rise → F chord + bell `inkey` | StageEnded Cleared (not boss/chapter) |
| `jingle_stage_fail` | P0 | Stinger · Critical | J | 2.4 | 1 | 4 | music box winding down, unresolved `inkey` | StageEnded Failed |
| `jingle_boss_appear` | P1 | Stinger · Critical | J | 2.0 | 1 | 4 | low tremolo + timpani roll + dissonant music box `inkey` | BossSpawned |
| `jingle_boss_defeated` | P1 | Stinger · Critical | J | 2.6 | 1 | 4 | glockenspiel + strings + bells cadence `inkey` | Boss kill pop (+0.3 s) |
| `jingle_chapter_clear` | P1 | Stinger · Critical | J | 4.0 | 1 | 4 | toy-orchestra chapter fanfare `inkey` | First clear of x-10 |
| `jingle_new_feature` | P1 | Stinger · Critical | J | 2.2 | 1 | 4 | harp gliss + 3-note celesta motif + bell `inkey` | FeatureUnlockCard (UISfxKey.FeatureUnlock) |
| `jingle_evolution` | P1 | Stinger · Critical | J | 3.0 | 1 | 4 | rising strings → music-box melody → big warm bell `inkey` | Evolution reveal (UISfxKey.EvolveReveal) |
| `jingle_new_friend` | P1 | Stinger · Critical | J | 2.0 | 1 | 4 | music box + pizzicato + gentle bell `inkey` | Gacha new lineage (UISfxKey.GachaNewFriend) |
| `jingle_gacha_legend` | P1 | Stinger · Critical | J | 2.6 | 1 | 4 | soft horns + harp gliss + glockenspiel + timpani roll `inkey` | Legendary result highlight |
| `jingle_gacha_mythic` | P1 | Stinger · Critical | J | 3.5 | 1 | 4 | celesta + glass harmonica + bells; the last note finally sounds `inkey` | Mythic result highlight |
| `jingle_first_meeting` | P2 | Stinger · Critical | J | 2.0 | 1 | 4 | shy music box + soft strings `inkey` | FirstMeetingCardView |
| `jingle_graduation` | P2 | Stinger · Critical | J | 3.0 | 1 | 4 | pizzicato + glockenspiel proud cadence `inkey` | GraduationCardPopup |
| `jingle_dungeon_clear` | P2 | Stinger · Critical | J | 2.4 | 1 | 4 | kalimba + music box + bright bell `inkey` | Dungeon floor cleared |
| `jingle_event_open` | P2 | Stinger · Critical | J | 2.0 | 1 | 4 | sleigh bells + glockenspiel + pizzicato `inkey` | Event start popup |

### 2.10 Voice barks (optional; Ui bus; silent until `GameSettings.VoiceBarks` exists and is on)

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `vox_jjakjjak_hello` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 짝짝이 hello bark (short, cute; Korean) | GachaNewFriend reveal: 1.4 s into jingle_new_friend, on the held C6 |
| `vox_jjakjjak_cheer` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 짝짝이 cheer bark (short, cute; Korean) | Evolution reveal / level milestone of that lineage |
| `vox_jjakjjak_pet` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 짝짝이 pet bark (short, cute; Korean) | CR-10 tap-to-pet (≤ 1 per 8 s, else pet_squeak) |
| `vox_ttokdan_hello` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 똑단이 hello bark (short, cute; Korean) | GachaNewFriend reveal: 1.4 s into jingle_new_friend, on the held C6 |
| `vox_ttokdan_cheer` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 똑단이 cheer bark (short, cute; Korean) | Evolution reveal / level milestone of that lineage |
| `vox_ttokdan_pet` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 똑단이 pet bark (short, cute; Korean) | CR-10 tap-to-pet (≤ 1 per 8 s, else pet_squeak) |
| `vox_chalkak_hello` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 찰칵이 hello bark (short, cute; Korean) | GachaNewFriend reveal: 1.4 s into jingle_new_friend, on the held C6 |
| `vox_chalkak_cheer` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 찰칵이 cheer bark (short, cute; Korean) | Evolution reveal / level milestone of that lineage |
| `vox_chalkak_pet` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 찰칵이 pet bark (short, cute; Korean) | CR-10 tap-to-pet (≤ 1 per 8 s, else pet_squeak) |
| `vox_mongdang_hello` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 몽당이 hello bark (short, cute; Korean) | GachaNewFriend reveal: 1.4 s into jingle_new_friend, on the held C6 |
| `vox_mongdang_cheer` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 몽당이 cheer bark (short, cute; Korean) | Evolution reveal / level milestone of that lineage |
| `vox_mongdang_pet` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 몽당이 pet bark (short, cute; Korean) | CR-10 tap-to-pet (≤ 1 per 8 s, else pet_squeak) |
| `vox_udukeoni_hello` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 우두커니 hello bark (short, cute; Korean) | GachaNewFriend reveal: 1.4 s into jingle_new_friend, on the held C6 |
| `vox_udukeoni_cheer` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 우두커니 cheer bark (short, cute; Korean) | Evolution reveal / level milestone of that lineage |
| `vox_udukeoni_pet` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 우두커니 pet bark (short, cute; Korean) | CR-10 tap-to-pet (≤ 1 per 8 s, else pet_squeak) |
| `vox_rouge_hello` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 루즈리 hello bark (short, cute; Korean) | GachaNewFriend reveal: 1.4 s into jingle_new_friend, on the held C6 |
| `vox_rouge_cheer` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 루즈리 cheer bark (short, cute; Korean) | Evolution reveal / level milestone of that lineage |
| `vox_rouge_pet` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 루즈리 pet bark (short, cute; Korean) | CR-10 tap-to-pet (≤ 1 per 8 s, else pet_squeak) |
| `vox_lalabell_hello` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 라라벨 hello bark (short, cute; Korean) | GachaNewFriend reveal: 1.4 s into jingle_new_friend, on the held C6 |
| `vox_lalabell_cheer` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 라라벨 cheer bark (short, cute; Korean) | Evolution reveal / level milestone of that lineage |
| `vox_lalabell_pet` | P2 | Ui · Normal | B | 0.9 | 2 | 6 | 라라벨 pet bark (short, cute; Korean) | CR-10 tap-to-pet (≤ 1 per 8 s, else pet_squeak) |

### 2.11 Reserved for RESEARCH2 features (P3 — produce only when the feature is built)

| Key | P | Bus · class | Tier | Len s | Var | Gen | Sound | Trigger |
|---|---|---|---|---|---|---|---|---|
| `bgm_tower` | P3 | Bgm | M | 71.11 (32 bars) | 1 | 4 | G minor · 108 BPM — climbing ostinato, G minor | Infinite tower (if built) |
| `bgm_worldboss` | P3 | Bgm | M | 60.95 (32 bars) | 1 | 4 | D minor · 126 BPM — big toy-orchestra battle, D minor | World boss / ghost duel (if built) |
| `jingle_tower_floor` | P3 | Stinger · Critical | J | 2.0 | 1 | 4 | short climb success `inkey` | Tower floor clear |
| `ui_equip` | P3 | Ui · Normal | B | 0.4 | 2 | 4 | relic/equipment snap-on | Equipment/relic equip |
| `ui_relic_up` | P3 | Ui · Normal | A | 0.8 | 2 | 4 | relic upgrade shimmer `inkey` | Relic upgrade |
| `ui_set_complete` | P3 | Ui · Normal | S | 1.2 | 1 | 4 | collection set completed `inkey` | Dex set effect unlocked |
| `ui_decor_place` | P3 | Ui · Normal | B | 0.3 | 2 | 4 | decoration placed | Shop decoration placed |

### 2.12 Aliases

| Alias | Plays the clips of | Why |
|---|---|---|
| `kill_mote` | `echo_refund` | FX-JUICE memory mote reaches the shop door (kill pop + 0.60 s), ≤ 2/s, −14 dB |
| `ui_error` | `ui_deny` | UISfxKey.Error (error toast) — separately trimmable |
| `status_reveal` | `enemy_reveal` | Reveal status uses the reveal shimmer |
| `unlock_card` | `jingle_new_feature` | SPEC4 §3.5 name |
| `jingle_gacha_high` | `jingle_gacha_legend` | SPEC4 §10.2 name (4★+) |
| `stage_clear` | `jingle_stage_clear` | SPEC4 name |
| `stage_fail` | `jingle_stage_fail` | SPEC4 name |
| `guardian_pulse` | `pulse_support` | SPEC4 name |

### 2.13 Counts
| Group | Keys | P0 | P1 | P2 | Shipped takes (Var) | Rendered candidates (Gen) | Shipped audio (s, Var × len) |
|---|---|---|---|---|---|---|---|
| UI | 75 | 15 | 44 | 16 | 156 | 371 | 81 |
| Ally attacks | 41 | 16 | 21 | 4 | 140 | 294 | 33 |
| Enemies & bosses | 34 | 15 | 16 | 3 | 101 | 222 | 49 |
| Shop/shield/support/echo/lamp | 22 | 4 | 17 | 1 | 51 | 121 | 26 |
| Skills | 51 | 0 | 13 | 38 | 105 | 222 | 61 |
| Status | 12 | 0 | 12 | 0 | 24 | 61 | 9 |
| Flow/combo/modifiers | 14 | 0 | 6 | 8 | 23 | 68 | 19 |
| Ambience loops | 10 | 1 | 6 | 3 | 10 | 30 | 380 |
| Ambience emitters | 4 | 0 | 0 | 4 | 18 | 36 | 54 |
| Music (7 loops + 1 intro) | 8 | 1 | 5 | 2 | 10 | 39 | 653 |
| Jingles | 14 | 2 | 8 | 4 | 14 | 56 | 36 |
| Voice barks (optional) | 21 | 0 | 0 | 21 | 42 | 126 | 38 |
| **Total (clip keys)** | **306** | **54** | **148** | **104** | **694** | **1646** | |

- **`UISfxKey`:** 81 members = 12 existing (values 0–11, unchanged) + **69 appended** (12–80):
  - 75 map to UI clip keys;
  - 5 map to jingles (`FeatureUnlock`, `EvolveReveal`, `GachaNewFriend`, `FirstMeeting`, `Graduation`);
  - 1 maps to an alias (`Error` → `ui_deny`).
- **Aliases: 8** (§2.12). **Reserved: 7** keys (2 BGM, 1 jingle, 4 UI; 28 candidates). Produce them only when the feature exists.
- **Production routes:** SYN 198 · HYB 65 · MUS 22 · VOX 21 (§7.1).
- **Tuned: 8** keys (`ui_count_tick`, `atk_musicbox`, `atk_musicbox_crit`, `atk_musicbox_finale`, `hit_lamp`, `lamp_tick`, `status_chain`, `kill_chain`). **In-key: 57** keys.
- **Loops:** 10 ambience, 2 battle loops (`shop_heartbeat`, `boss_windup`) and 7 music loop keys (9 pieces), plus 1 music intro.
- **Music:** 10 scores (9 loop pieces + the gacha intro), ≈ 653 s. **Jingles:** 14 scores, ≈ 36 s. All 24 are validated in `scores.json` (§7.6–§7.7).
- **Ambience:** 10 loops × 40 s (`amb_boss` 20 s) + 4 emitter sets (18 takes).
- **Voice barks (optional):** 21 keys × 2 shipped takes (126 generations); silent until `AudioService.VoiceBarks` is set (§7.9).
- **P0 slice** (the "not silent + 타격감" set): 54 keys, 172 shipped takes, 380 candidates:
  - 15 UI;
  - 16 ally attack (7 launches, 7 hits, generic, `hit_crit`);
  - 15 enemy (9 strikes, 4 deaths, `boss_appear`, `boss_death`);
  - 4 defence (`shop_hit`, `echo_summon`, `echo_dissolve`, `lamp_lit`);
  - `amb_night`, `bgm_battle_night`, `jingle_stage_clear`, `jingle_stage_fail`.

---

## 3. Architecture

### 3.1 Object tree and classes
```
[Audio]  (DontDestroyOnLoad; created by UISfx.Boot() only if the library or the legacy table has ≥1 clip)
 ├─ UISfx                 component: host of Update / OnApplicationPause, legacy static API → AudioEngine
 ├─ BattleAudioDirector   component: attaches to Game.Battle lazily (§5)
 ├─ Bgm A, Bgm B          AudioSource + AudioLowPassFilter (filter disabled unless muffling)
 ├─ Stinger               AudioSource
 ├─ Amb Base, Amb A, Amb B, Amb Boss   AudioSource + AudioLowPassFilter
 ├─ Loop 0, Loop 1        AudioSource (heartbeat, boss windup)
 ├─ Ui 0…7                AudioSource
 └─ Sfx 0…13              AudioSource
```
Every source sits on its **own child GameObject** so filters stay per source. Total 31 sources ≤ `m_RealVoiceCount` 32 (`ProjectSettings/AudioManager.asset`).

| File (`Scripts/Core/Audio/` unless noted) | Type | Role |
|---|---|---|
| `Core/UISfx.cs` (existing) | `UISfx : MonoBehaviour` + static API | boot; legacy-table fallback; forwards to `AudioEngine` |
| `Core/UISfxTable.cs` (existing) | `UISfxKey` (+69 members), `UISfxTable` | enum; legacy table still honoured |
| `AudioTypes.cs` | `AudioBus`, `AudioVoiceClass`, `MusicState`, `BattleAudioFocus`, `AudioKeyFlags`, `AudioId`, `AudioStats` | shared types |
| `AudioCatalog.cs` | `AudioCatalog` (static `AudioKeyDef[]`, ladders, `UiMap`) | code source of truth: keys, bus, class, tier, gates, flags |
| `AudioLibrary.cs` | `AudioLibrary : ScriptableObject` | built clips per key (§4.3) |
| `AudioService.cs` | static facade | public API (§3.2) |
| `AudioEngine.cs` | plain class owned by `UISfx` | buses, voices, gates, scheduler, ducks, focus, sleep |
| `AudioVoicePool.cs` | plain class | allocation + stealing (pure decision function, tested) |
| `AudioGate.cs` | `AudioGate`, `TokenBucket`, `ShuffleBag`, `PitchLadder` | pure logic, tested |
| `AudioScheduler.cs` | plain class | FX-clock queue, `PlayScheduled` commit, latency compensation |
| `AudioDuck.cs` | `DuckStack` | reason-stacked envelopes per bus |
| `MusicDirector.cs` | plain class | music states, crossfades, rotation and rests, intro→loop, jingles |
| `AmbienceDirector.cs` | plain class | base bed, modifier layers, boss layer, emitters |
| `BattleAudioDirector.cs` | `MonoBehaviour` (`[DefaultExecutionOrder(1000)]`) | battle events → sounds (§5) |
| `BattleAudioTiming.cs` | static | mirrors of the FX timing rules (§5.3), guarded by tests |

### 3.2 Public API
```csharp
namespace IdleGame
{
    public enum AudioBus : byte { Ui = 0, Sfx = 1, Stinger = 2, Bgm = 3, Amb = 4, Loop = 5 }
    public enum AudioVoiceClass : byte { Critical = 0, High = 1, Normal = 2, Low = 3 }
    public enum MusicState : byte { Silent = 0, Main = 1, Boss = 2, Dungeon = 3, Gacha = 4, Story = 5, Event = 6 }
    public enum BattleAudioFocus : byte { Full = 0, Dimmed = 1, Hidden = 2 }
    public readonly struct AudioId { public readonly short Index; public bool IsValid => Index >= 0; }

    public static class AudioService
    {
        public static bool HasClips { get; }                         // ≥1 clip (library or legacy) → settings sliders live, pills off
        public static AudioId Resolve(string key);                   // once at init; invalid if unknown; never per frame
        public static void Play(AudioId id, float volume = 1f, float pan = 0f, float pitch = 1f);
        public static void PlayAfter(AudioId id, float seconds, float volume = 1f, float pan = 0f);  // unscaled UI clock
        public static void PushMusic(MusicState state, int owner);   // owner = caller.GetInstanceID(); stacked
        public static void PopMusic(int owner);
        public static void PlayJingle(AudioId id);                   // Stinger bus + that key's duck rule
        public static void SetSleep(bool asleep);
        public static void SetBattleFocus(BattleAudioFocus focus, int owner);  // sheets / full-screen popups over the field
        public static void ClearBattleFocus(int owner);
        public static void OnGachaReveal(ItemGrade grade);           // duck for ≥ Legendary, picks the reveal sting
        public static void Duck(AudioBus bus, float gainDb, float hold, float attack = 0.05f, float release = 0.4f, int reason = 0);
        public static bool LatencyCompensation { get; set; }         // default: on for Android/iOS players
        public static void ReloadLibrary(AudioLibrary library = null); // audition window / tests
        public static bool VoiceBarks { get; set; }                  // false until the settings package sets it (§7.9, §8.2); vox_* keys are silent while false
        public static AudioStats Stats { get; }                      // dev HUD: voices in use, drops, budget hits, missing keys
    }
}
```
**`UISfx` stays as is** (signatures and semantics): `Table`, `Boot()`, `SetTable(UISfxTable)`, `Play(UISfxKey, float)`, `ApplySettings()`, `SfxGain`, `BgmGain`, consts `TableResourcePath`, `SfxVoices` (= 8, now the Ui pool size), `BgmPauseFadeSeconds`. Added: `UISfx.HasClips` (= `AudioService.HasClips`).

`UISfx.Play(key, volume)` resolution: (1) `AudioCatalog.UiMap[(int)key]` → library entry with ≥1 clip → engine (bus of that entry: Ui, or Stinger for the 5 jingle-mapped keys), gated by the existing `UISfxGate`; (2) otherwise the legacy `UISfxTable` entry (today's path); (3) otherwise silence. `SetTable(null)` clears only the legacy table. The legacy table's `bgm` plays as Main music only when the library has no `bgm_battle_night` clip.

### 3.3 Buses, gain staging, settings
`voice volume = busGain × duck(bus) × focus(bus) × dB(entry trim) × call volume × jitter`, clamped to ≤ 1.

| Bus | Gain source | Pool | Notes |
|---|---|---|---|
| Ui | SfxVolume × SoundOn | 8 | `UISfxGate` (40 ms, 3 per key, ±5 %, coin ladder) |
| Sfx | SfxVolume × SoundOn | 14 | battle and borrowed ambience emitters; subject to battle focus and sleep |
| Stinger | SfxVolume × SoundOn | 1 | jingles are event feedback, so they follow 효과음 |
| Bgm | BgmVolume × SoundOn | 2 | equal-power crossfade pair; low-pass for "muffled" states |
| Amb | BgmVolume × SoundOn × 0.9 | 4 | 배경음 covers the background bed too |
| Loop | SfxVolume × SoundOn | 2 | heartbeat, boss windup (continuous control) |

- `SoundOn` false: `GameSettings.Apply` already sets `AudioListener.volume = 0`; the engine additionally stops starting voices and pauses Bgm/Amb (no decode CPU). `BgmVolume == 0`: Bgm/Amb paused, positions kept. `SfxVolume == 0`: Ui/Sfx/Loop/Stinger voices are not allocated.
- `GameSettings.Apply → UISfx.ApplySettings()` pushes all bus gains (one call, unchanged).
- Settings preview: the settings rows call `UISfx.Play(UISfxKey.SliderTick)` at most once per 150 ms while dragging (owner: settings package).

### 3.4 Voices, priority, stealing
- Sources: 2D (`spatialBlend 0`), `dopplerLevel 0`, `bypassReverbZones`, `playOnAwake false`. Unity `priority`: Bgm 0, Stinger 8, Amb 16, Loop 24, Ui 48, Sfx by class Critical 32 / High 64 / Normal 128 / Low 192, borrowed emitters 220.
- Allocation inside the bus pool: a free voice, else **steal**. Candidates: playing voices whose class is not higher than the new sound's class; prefer (1) voices past 70 % of their clip (quiet tails, no audible cut), (2) the lowest class, (3) the oldest start. Never steal Critical. Never steal a voice younger than 50 ms unless the class gap is ≥ 2. No candidate → drop the new sound (`Stats.drops[bus]++`).
- `PlayScheduled` voices count as busy from the commit time.

### 3.5 Gates, jitter, shuffle bags, ladders, budgets
- **Per key** (arrays by `AudioId`): min interval, max simultaneous voices, pitch jitter (uniform ±), volume jitter (uniform ± dB). Defaults (the JSON has every key resolved):

| Category | Min interval | Max voices | Pitch | Volume |
|---|---|---|---|---|
| UI (`UISfxGate`, legacy) | 40 ms (count tick 70, slider 150, red dot 2000) | 3 | ±5 % | 0 |
| Lineage hits | 30 ms | 4 | ±6 % | ±1.5 dB |
| Launches `fire_*` | 50 ms | 3 | ±6 % | ±1.5 dB |
| Lineage crits / `hit_crit` | 60 / 120 ms | 2 | ±4 % | ±1 dB |
| Layers (slash/snipe/thread/heavy/bounce/drip/ring) | 50 ms | 2 | ±6 % | ±1.5 dB |
| Enemy strikes | 90 ms | 2 | ±6 % | ±1.5 dB |
| Hurt layers | 80 ms | 2 | ±8 % | ±2 dB |
| Deaths | 50 ms | 3 | ±6 % | ±1 dB |
| Statuses | 200 ms | 2 | ±4 % | 0 |
| Skills | 100 ms (skill_big 1 s) | 2 | ±3 % | 0 |
| Tuned keys | 30 ms | 3 | 0 | 0 |
| Boss one-shots, lamp, stage flow | 0.3–1 s | 1 | 0 | 0 |

- **Shuffle bag:** never repeat the last take (≥ 2 takes); with ≥ 4 takes never repeat either of the last two. State = 2 bytes per key.
- **Pitch ladders:** `PitchLadder { window, reset, sbyte[] steps }` with per-source state (lullaby: up to 16 sources by guardian slot / echo id; others global). Tuned keys pick the take whose `rootHz` is nearest (in semitones) to the target note, then `pitch = targetHz / take.rootHz` with `targetHz = ladderRootHz × 2^(step/12)` (+ the playing track's `transposeSemitones`, 0 for every shipped piece).
- **Budgets** (token buckets, continuous refill): hits 14/s (burst 6), launches 8/s (3), enemy strikes 6/s (3), hurt 5/s (2), deaths 8/s (4), statuses 4/s (2), spawns 3/s (1); global battle starts 28/s. 2× speed: rates ×1.3. Sleep: ×0.5. Over budget: Low/Normal sounds are dropped; hits are folded into the crowd aggregator (§5.6).

### 3.6 Scheduler (sync with the visuals)
- Battle sounds use the **FX clock**, the same clock `BattleFx.Advance` uses: `fxDt = Time.deltaTime × slowMo.Tick(Time.deltaTime)`. The scheduler owns a private `FxSlowMotion` (public struct in `HitFeel.cs`) triggered on the same events as BattleFx (§3.9), so frozen visuals and pending sounds stay together.
- Pending ring of 96 `{dueFx, id, gain, pitch, pan, flags, enemyId}`; O(1) insert, O(n) scan.
- **Commit:** when `dueFx − nowFx ≤ lookahead` (`max(12 ms, 1.5 × Time.unscaledDeltaTime)`) and no slow-motion is active, allocate a voice and call `AudioSource.PlayScheduled(AudioSettings.dspTime + max(0, remaining − latencyComp))` — sub-frame accurate at 30 or 60 fps. During slow-motion, commit frame-quantised with `Play()`.
- **Latency compensation:** `latencyComp = clamp(bufferLength × numBuffers / outputSampleRate − 0.025, 0, 0.06)` from `AudioSettings.GetDSPBufferSize`. Applied only to predicted events (projectile hits, lamp wave, kill pops) whose remaining time ≥ `latencyComp`. Audio arriving early is more noticeable than late (perception thresholds ≈ +45 ms early / −125 ms late), so the compensation is capped below the measured buffer latency.
- `PlayAfter` (UI) uses unscaled time and the same commit path.
- Flush pending battle sounds on `StageChanged`, `OnApplicationPause(true)` and battle focus `Hidden` — no catch-up burst. Overflow drops the oldest entry of the lowest class.

### 3.7 Pan and proximity
- `pan = clamp(worldX / 2.35, −1, 1) × 0.35` (2.35 = the field's visible half-width, `BattleFieldMapper._extentX`). Bosses ×0.5. Shop-centred sounds, UI and jingles 0. Emitters random ±0.6.
- Proximity emphasis for enemy-side sounds: −2 dB at the spawn radius (2.5) rising linearly to 0 dB at the ring (1.0). Threats near the shop are louder.

### 3.8 2× speed rules
| Aspect | 1× | 2× |
|---|---|---|
| Hit / kill timing | `AttackFxCatalog` rules with speed 1 | same functions with speed 2 (flights ×0.6, pops ×0.8) |
| Budgets | §3.5 | ×1.3 (not ×2) |
| Launch sounds | all within budget | alternate: each guardian skips every other non-crit launch |
| Hurt layers | 50 % | 30 % |
| Enemy strike sounds | per-enemy gap = look interval | gap = interval / 2 (mirror of `StrikeArtDue`) |
| Pitch, BGM | unchanged | unchanged (no tempo or chipmunk change) |

### 3.9 Hit-stop interplay
- **Global stops** (mirrored): `LampLit` → `Trigger(0, LampWave.IgniteScale 0.2, LampWave.IgniteTime 0.12)`; boss kill moment (at the boss death pop) → `Trigger(HitFeel.GlobalStop 0.12, HitFeel.SlowScale 0.3, HitFeel.SlowTime 0.18)`.
- The moment sound (`lamp_lit`; `boss_kill_impact` + `boss_death`) starts at the trigger in real time; pending battle sounds advance at the mirrored scale.
- During the slow window, newly started Sfx voices play at pitch ×0.94 ("time drag") and BGM dips −6 dB for 120 ms, releasing over 0.5 s, unless a deeper duck is active.
- Target-only stops (T1 0.03 s, T2 0.06 s, skill 0.04 s) never pause audio. Crits instead micro-duck the other Sfx voices by −4 dB for 60 ms (attack 5 ms, release 120 ms).

### 3.10 Ducking (per bus, reasons stack, the deepest active duck wins)
| Trigger | Ducked bus · amount | Attack | Hold | Release |
|---|---|---|---|---|
| `lamp_lit` | Bgm −8 dB · Sfx −3 dB (except lamp sounds) | 50 ms | 1.0 s | 0.9 s |
| `boss_appear` + `jingle_boss_appear` | Bgm −7 dB · Sfx −4 dB | 80 ms | jingle | 0.8 s |
| Boss kill moment | Bgm −10 dB · Sfx −6 dB (except boss sounds) | 30 ms | 1.2 s | 1.0 s |
| `jingle_stage_clear` / `jingle_boss_defeated` / `jingle_chapter_clear` | Bgm −10 dB | 60 ms | jingle | 0.7 s |
| `jingle_stage_fail` | Bgm −14 dB | 100 ms | jingle | 2.0 s |
| Gacha ≥ Legendary reveal (`ui_gacha_rare`, mythic, `jingle_gacha_*`) | Bgm −8 dB | 50 ms | 1.8 s | 0.8 s |
| `jingle_new_feature` / `_evolution` / `_new_friend` / `_first_meeting` / `_graduation` | Bgm −10 dB | 80 ms | jingle | 0.8 s |
| `ui_level_milestone`, `ui_evolve_burst`, `ui_reward_double` | Bgm −5 dB | 40 ms | 0.8 s | 0.6 s |
| Crit impact (micro) | other Sfx −4 dB | 5 ms | 60 ms | 120 ms |
| `skill_big` | Bgm −4 dB | 50 ms | 0.8 s | 0.6 s |
| Sleep | Bgm ×0.35 (−9 dB) + LPF 1.2 kHz · Sfx ×0.4 (−8 dB) | 0.6 s | until wake | 0.4 s |
| Focus Dimmed (sheet) | Sfx −6 dB · Amb −4 dB · Bgm LPF 6 kHz | 150 ms | while open | 250 ms |
| Focus Hidden (full-screen) | no new Sfx · Amb −12 dB | 200 ms | while open | 300 ms |
| `OnApplicationPause(true)` | Bgm/Amb/Loop → 0 then pause | 0.2 s | — | 0.4 s on resume |

### 3.11 Music director
- **States** (priority): Story 60 > Gacha 50 > Event 40 > Dungeon 30 > Boss 20 > Main 10 > Silent 0. Requests come from an owner stack (`PushMusic`/`PopMusic`, ≤ 8 entries) plus one battle-derived request: **Boss** while a **chapter boss** (stage % 10 == 0) is alive and battle focus is not Hidden. Regular stage bosses do not change the music (they get `jingle_boss_appear` + `amb_boss`). Effective state = highest priority.
- **Sleep** modifies the current state: muffled main (×0.35, LPF 1.2 kHz) or, if `bgm_sleep` has clips, a 2 s crossfade to it.
- **Crossfades** (equal-power sin/cos): Main↔Boss 1.2 s, starting on the Main track's next beat when its beat grid is known (wait ≤ 0.6 s) else immediately; Main→Gacha: Main out 0.4 s while the gacha theme starts; Gacha→Main 1.2 s; into Story/Dungeon/Event 1.0 s; to Silent 0.8 s.
- **Resume:** every music key remembers `timeSamples`; returning to Main resumes the same piece 1.5 s before where it left, fading in over 1.2 s.
- **Intro → loop:** optional `{key}_intro` key (e.g. `bgm_gacha_intro`). The intro plays on the active source; the loop is `PlayScheduled` on the idle source at `dspStart + intro.length` (sample-exact) and the roles swap. A crossfade request during an intro waits for the loop start (≤ 1 bar).
- **Rotation and rests (Main only, idle sessions last hours):** each active take of `bgm_battle_night` is a different piece. Play a piece for 2 full loops (≈ 2.2–2.8 min), crossfade 4 s to another piece (shuffle, no immediate repeat). After every 3 pieces (≈ 8 min) **rest**: fade out over 6 s, 45–75 s of ambience only (Amb +3 dB), then fade the next piece in over 4 s. No rest while a boss is alive. Interruptions (boss, gacha) keep the rotation position.
- **Jingles:** one Stinger voice; a new jingle replaces the old with a 60 ms fade; ducks per §3.10. `battle_stage_start` plays at −6 dB if a jingle is still sounding.
- **Boot:** music starts 0.5 s after `Boot()` so an early `PushMusic(Story)` (first run) wins without an audible switch.

### 3.12 Ambience director
- **Base:** `amb_night` whenever the map/battle is the background (×0.9 of the Amb bus; −6 dB under Gacha/Story; `amb_dungeon` replaces it while Dungeon is the music state; `amb_sleep` +3 dB while asleep).
- **Modifier layer** from `sim.StageModifier` at each `StageChanged`, crossfaded on Amb A/B over 2.0 s: Rain→`amb_rain`, Wind→`amb_wind`, DustStorm→`amb_dust`, Blackout→`amb_blackout` (+ base −6 dB and BGM LPF 5 kHz: the "hush"), FullMoon→`amb_fullmoon`, Festival→`amb_festival`, None→silence.
- **Boss layer:** `amb_boss` on Amb Boss while any boss is alive (1.5 s fades).
- **Emitters:** every U(12, 30) s one take from the set of the current layer (rain/wind/festival, else night), random pan ±0.6, gain −6…−12 dB, on a borrowed Low-class Sfx voice with Amb gain. Skipped during jingles, the boss-kill moment and focus Hidden; they keep playing through music rests (they are what the rest is made of).
- Clips are `CompressedInMemory` with `preloadAudioData = false`; the director calls `LoadAudioData()` for the next layer at `StageChanged` and `UnloadAudioData()` for layers unused for 2 stages.

### 3.13 Sleep, battle focus, background
- `AudioService.SetSleep(true)` (SleepModeView, or `AppSignals.SleepChanged` once it exists): ducks per §3.10, battle budgets ×0.5, launches muted, hurt layers off, `ui_sleep_enter`. `SetSleep(false)`: restore over 0.4 s, `ui_sleep_wake`.
- **Battle focus** (owner-stacked, the most restrictive wins): Full (default), Dimmed (a sheet covers ≥ 40 % of the field), Hidden (full-screen: gacha result, evolution craft, story intro). Until UI owners call it, focus stays Full.
- **Background:** `OnApplicationPause(true)` → Bgm/Amb/Loop fade to 0 in 0.2 s then `Pause()`, Ui/Sfx stopped, scheduler flushed; `OnApplicationPause(false)` → `UnPause()` and fade in over 0.4 s. `OnApplicationFocus` is ignored (M01; `Application.runInBackground` stays true, so PC/editor keep playing).
- iOS: keep the default Ambient session ("Mute Other Audio Sources" off): respects the silent switch and mixes with the player's own music.

### 3.14 Performance and allocation rules
- Steady state allocates nothing: arrays sized at boot; no LINQ; no runtime string building (lineage/enemy/vfx keys are resolved once into id tables); `IReadOnlyList` iterated by index (an interface `foreach` boxes); event delegates created once.
- The engine `Update` runs only while something is active (fades, ducks, pending queue, loops) — the existing `enabled = false` pattern. The director polls six values per frame when attached.
- Low-pass filters are enabled only while muffling.
- Budget: ≤ 0.15 ms main thread per frame at 2× with 20 enemies (dev `PerfProbe`, SPEC4 §3.1 harness).

### 3.15 Boot and failure modes
1. `GameBootstrap.Awake → UISfx.Boot()` (unchanged call site): load `Resources/Audio/AudioLibrary` and the legacy `Resources/Audio/UISfxTable`. If neither has a clip → return (silent, no object).
2. Else create `[Audio]`, children, `ApplySettings`, the `BattleAudioDirector`, start music after 0.5 s.
3. Listener guard: after `SceneManager.sceneLoaded`, if no enabled `AudioListener` exists, add one to `[Audio]`; remove it when a scene listener appears (the generated scene's Main Camera has one).
4. Scene reload (`ResetData`): `[Audio]` persists; the director sees a new `Game.Battle` and reattaches; music returns to Main.
5. Unknown key or empty entry → silent no-op; one warning per key in editor/development builds only (`Stats.missing`).
6. Library older than the catalog (key order differs) → remap by name once at load; editor warning "rebuild the audio library".

---

## 4. Data

### 4.1 Folders and file names
```
Assets/_Project/Audio/
  Sfx/Ui/          ui_*, chest_open, basket_claim, quick_idle, gacha_bundle_*, achievement_claim, pet_squeak
  Sfx/Battle/      fire_*, atk_*, hit_*, echo_*, shield_*, shop_* (one-shots), pulse_*, heal_tick, lamp_*, token_summon,
                   skill_*, status_*, battle_*, kill_*, coin_pop, twinkle, mod_*
  Sfx/Enemy/       hurt_*, death_*, enemy_*, boss_* (one-shots)
  Sfx/Loop/        shop_heartbeat, boss_windup
  Vox/             vox_{lineage}_{hello|cheer|pet}_{NN}.wav (optional barks, §7.9)
  Bgm/             bgm_*_{NN}.wav  (+ optional bgm_*_intro_{NN}.wav)
  Bgm/Jingles/     jingle_*_{NN}.wav
  Amb/             amb_*_{NN}.wav, amb_emit_*_{NN}.wav
  AudioSelection.asset   editor-owned take choices + trims (never overwritten by the builder)
  audio_qc.json          QC metrics per file (written by the QC script, read by the builder and the window)
  THIRD_PARTY_AUDIO.md   every external file used: CC0 pack, original file, URL, licence, processing; Higgsfield speech lines (§7.2, §7.9)
Assets/_Project/Resources/Audio/
  AudioLibrary.asset     built; overwritten on every build (file and GUID kept)
  UISfxTable.asset       legacy, optional, never built
```
- Folders are for people; the **key comes only from the file name**. Accepted names: `{key}_{NN}.wav|.ogg` (NN = 01–99) and the SPEC4 forms `{key}.wav` (take 1) and `{key}_{N}.wav` (take N). Keys never end in a number token, so the parse is unambiguous: last `_` token all digits → take number, else take 1.
- Intro clips use the key `{musicKey}_intro` (e.g. `bgm_gacha_intro_01.wav`).
- Masters (24-bit, untrimmed) and their render sidecars (seed, route, recipe hash, CC0 files used) stay outside `Assets` in `scratchpad/audio/masters/`.

### 4.2 `AudioCatalog` (code, runtime)
```csharp
public readonly struct AudioKeyDef
{
    public readonly string Key;            // "atk_needle"
    public readonly AudioBus Bus;
    public readonly AudioVoiceClass Class;
    public readonly byte UnityPriority;    // AudioSource.priority (0 = most important)
    public readonly sbyte TargetLufs;      // tier target (documentation + audition meter)
    public readonly float MinInterval;     // same-key gate (s)
    public readonly byte MaxVoices;
    public readonly float PitchJitter;     // ± fraction
    public readonly float VolumeJitterDb;  // ± dB
    public readonly AudioKeyFlags Flags;   // Tuned, Loop, InKey, Override, Emitter, Music, Stereo
    public readonly byte Ladder;           // 0 none, else PitchLadder index
    public readonly float TargetRootHz;    // tuned keys
    public readonly short AliasOf;         // −1 or the target key index
    public readonly byte Stage;            // 0..3 = P0..P3 (reports, audition filters)
}
public static class AudioCatalog
{
    public const int Version = 1;                    // bump when keys are added/reordered
    public static readonly AudioKeyDef[] Keys;       // order defines AudioId.Index; append-only
    public static readonly AudioId[] UiMap;          // index = (int)UISfxKey; length == UISfxKey count (tested)
    public static readonly PitchLadderDef[] Ladders; // lullaby, kill, lamp, count, gauge, chain (coin stays in UISfxGate)
    public static AudioId Find(string key);          // dictionary built once at type init
    public static string[] FinaleFormIds = { "lala_finale" };
}
```
Generate the first version from `audio_catalog.json` (`keys[].gate`, `unityPriority`, `ladder`, `flags`, `uiSfxKey`), then maintain it in C#. Append new keys at the end only (ids are array indices).

### 4.3 `AudioLibrary` asset (built)
```csharp
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "IdleGame/Audio Library")]
public sealed class AudioLibrary : ScriptableObject
{
    public const string ResourcePath = "Audio/AudioLibrary";
    public int catalogVersion;                 // AudioCatalog.Version when built
    public Entry[] entries;                    // one per catalog key, catalog order

    [Serializable] public sealed class Entry
    {
        public string key;
        public float trimDb;                   // user trim from AudioSelection (−12…0)
        public Take[] takes;                   // ACTIVE takes only, sorted by take number
        public MusicInfo music;                // music keys only (null otherwise)
    }
    [Serializable] public struct Take
    {
        public AudioClip clip;
        public byte number;                    // take number from the file name
        public float rootHz;                   // tuned keys (QC); 0 = untuned
        public float gainDb;                   // per-take correction from QC (≤ 0)
    }
    [Serializable] public sealed class MusicInfo
    {
        public float bpm; public int beatsPerBar; public float beatOffset; public int bars;
        public int transposeSemitones;         // vs the F-pentatonic world; tuned keys follow it
        public float loudnessLufs;
    }
    public bool HasAnyClip { get; }            // cached on first read
}
```
Only active takes are referenced, so **inactive takes never ship** (nothing in a build references them).

### 4.4 `AudioSelection` asset (editor, `Editor/Audio/AudioSelection.cs`)
```csharp
public sealed class AudioSelection : ScriptableObject
{
    public const string AssetPath = "Assets/_Project/Audio/AudioSelection.asset";
    [Serializable] public sealed class KeyChoice
    {
        public string key;
        public bool custom;          // false = follow the QC recommendation
        public byte[] activeTakes;   // used when custom
        public float trimDb;         // −12…0, set by ear in the audition window
        public byte[] ratedTakes;    // parallel arrays: take number → rating ★1–5
        public byte[] ratings;
        public string note;          // free text ("too bright", "use for 2× only")
    }
    public List<KeyChoice> keys = new();
}
```
Effective active takes of a key: `custom` → `activeTakes` ∩ existing files; else the QC `recommended` takes; else every take passing the QC hard gates; else every take.

### 4.5 `audio_qc.json` (written by `synth/qc.py`, §7.8)
```json
{ "version": 1, "generated": "2026-11-04T12:00:00",
  "files": {
    "Sfx/Battle/atk_needle_03.wav": {
      "key": "atk_needle", "take": 3, "duration": 0.214, "lufsMmax": -17.1, "lufsS": null, "lufsI": null, "truePeak": -1.6,
      "attackMs": 2.1, "leadSilenceMs": 0.8, "centroidHz": 3120, "harsh": 0.62, "phoneLossLu": 3.2, "noiseFloorDb": -71,
      "clippedRuns": 0, "dcOffset": 0.0004, "speechScore": 0.02, "rootHz": null, "pitchStdCents": null, "pitchClasses": null,
      "loop": null, "bpm": null, "beatOffset": null, "bars": null, "keyDetected": null,
      "passes": true, "failReasons": [], "score": 87.5, "recommended": true,
      "source": "hyb:atk_needle take=3 seed=0x5f3a91c2 + cc0:ifc.tick/tick_002.ogg", "recipe": "R.atk_needle#4c1e", "variation": "ping 2.95k, tick gap 34 ms" } } }
```

### 4.6 `AudioDataBuilder` (`Editor/Audio/AudioDataBuilder.cs`)
- `public static AudioLibrary Build()`; menu **IdleGame/Audio/라이브러리 다시 만들기**; batch `-executeMethod IdleGame.EditorTools.AudioDataBuilder.BuildBatch`.
- Steps: (1) `AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project/Audio" })` → parse names → group by key; (2) load `AudioSelection` and `audio_qc.json` (TextAsset via `AssetDatabase.LoadAssetAtPath`); (3) for each catalog key in order build its `Entry` (effective takes, `rootHz`/`gainDb` from QC, trim from the selection, music info); (4) log a report: files with unknown keys (warning), missing P0 keys (warning — silence is legal), keys with fewer takes than Var, takes excluded by QC; (5) `LoadOrCreate` `Resources/Audio/AudioLibrary.asset`, overwrite contents, `SetDirty`.
- **IdleDataBuilder hook (the only edit there):** in `BuildContent()`, after `Validate(content);` add `AudioDataBuilder.Build();`. Callers already save (`RebuildContent`, `BatchBuildAll`, `IdleAutoSetup`).
- **Auto rebuild on import:** `AudioImportPostprocessor.OnPostprocessAllAssets` → any imported/deleted/moved path under `Assets/_Project/Audio` → debounce 1 s (`EditorApplication.delayCall` loop) → `Build()` + `SaveAssets()`; deferred while `EditorApplication.isCompiling || isUpdating`. In play mode it also calls `AudioService.ReloadLibrary()` so the user hears new takes immediately.

### 4.7 `AudioImportPostprocessor` (`Editor/Audio/AudioImportPostprocessor.cs`)
Applies to `Assets/_Project/Audio/**` only. The preset comes from the catalog entry of the parsed key (folder only as a fallback for unknown keys).

| Preset | Keys | Load type | Format | Quality | Channels | Preload | Load in bg | Sample rate |
|---|---|---|---|---|---|---|---|---|
| SfxHot | battle cues ≤ 0.6 s (groups ally, enemy, status, flow, defense one-shots) + `ui_tap`, `ui_coin_land`, `ui_count_tick` | DecompressOnLoad | ADPCM | — | force mono | yes | no | preserve (44.1 kHz) |
| Sfx | all other Ui/Sfx/Loop keys | CompressedInMemory | ADPCM | — | force mono | yes | no | preserve |
| Jingle | Stinger bus | CompressedInMemory | Vorbis | 0.70 | stereo | yes | no | preserve |
| Ambience | Amb loops | CompressedInMemory | Vorbis | 0.50 | stereo | no | yes | override 44 100 |
| Emitter | `amb_emit_*` | CompressedInMemory | ADPCM | — | force mono | no | yes | preserve |
| Voice | `vox_*` | CompressedInMemory | Vorbis | 0.70 | force mono | no | yes | preserve |
| Music | Bgm bus (incl. `_intro`) | Streaming | Vorbis | 0.60 | stereo | n/a | yes | override 44 100 |

- `normalize = false` always (Unity's peak normalisation during mono fold-down would undo the loudness tiers). `ambisonic = false`. No per-platform overrides unless a device test needs one. In Unity 6 `preloadAudioData` belongs to `AudioImporterSampleSettings` (the importer-level property is obsolete) — use whichever member compiles without an obsolete warning.
- **Ambience stays CompressedInMemory on purpose.** Up to 4 ambience layers plus 2 music sources would be 6 concurrent streams on mobile. A 40 s stereo loop at Vorbis 0.50 is about 0.5 MB, and `preloadAudioData = false` plus the director's `LoadAudioData()` keep only the layers in use resident. The const `AmbienceStreaming` (default false) switches the preset to Streaming if a device test shows memory pressure.
- **Loop metadata.** Unity clips have no loop markers. `AudioSource.loop = true` loops the whole clip, which is exactly the rendered loop (§7.3.5). Musical metadata (bpm, beats per bar, bars, beat offset, loudness) goes into `AudioLibrary.MusicInfo`. It comes from `audio_qc.json`, where `qc.py` measures it and cross-checks it against `scores.json`.
- `GetVersion()` = 1, bumped when rules change. Menu **IdleGame/Audio/오디오 임포트 규칙 다시 적용** reimports the folder.
- Warnings: unparsable name, unknown key, imported length > 2 × catalog length (probably untrimmed).

---

## 5. Battle listener (`BattleAudioDirector`)

### 5.1 Lifecycle (no scene or BattleView edits)
- A component added to `[Audio]` by the boot, `[DefaultExecutionOrder(1000)]` (after `GameBootstrap` (−1000) has ticked the battle and after `BattleView`). The logic lives in a plain class `BattleAudioLogic` that reads the battle through `IBattleAudioState` (an adapter over `BattleSimulator`/`BattleSystem`), so EditMode tests can drive it with fake events and a fake clock.
- `Update`: if `!Game.IsReady || Game.Battle == null` → detach if attached. If `Game.Battle` is a new instance (first run, scene reload after `ResetData`) → detach the old one, subscribe to `Event`, `StageChanged`, `StageEnded`, `StepBack`, reset state.
- **Never start the battle from audio.** `BattleSystem.Sim` calls `EnsureStarted()`. The director sets `_simSafe` only after it has received an event or `StageChanged`, or sees `IsBetweenStages`; until then it does not touch `Sim`.
- At attach, resolve every id once: per lineage (7 known via `AttackFxCatalog.KnownLineages` + generic) `fire_*`, hit = `Look(id).Sfx`, `{hit}_crit`, extras; per `EnemyDef` in `Game.Battle.Config.enemies` the strike key `EnemyFxCatalog.Look(def.id, boss).Sfx`, its family (`EnemyFxCatalog.Family`) and death override; `SkillVfx.All` → `skill_{name}` ids (+ `_hit` ids) and `skill_style_*` fallbacks; status ids. String work happens only here.
- Detach: unsubscribe, flush the queue, stop the loops.

### 5.2 Context machine (mirror of `BattleView.TrackContext`)
The simulator emits one action's results in the same frame, in order (`GuardianAttack → (CriticalHit) → (ShieldBroken) → EnemyHit → (EnemyKilled)`).
- Opened by: `GuardianAttack` (Basic: slot, target, flight), `EchoAttack` (EchoBasic: echo, target, delay), `SkillCast` (Cast: caster, skill, style, vfx, flight, segment, primary, ally-group), `LampLit` (Lamp), `GuardianPulse` (Pulse: slot, kind).
- Kept open, same frame only, by: `EnemyHit`, `EnemyKilled`, `CriticalHit`, `StatusApplied`, `ShieldGained`, `ShieldBroken`, `EchoHealed`, `ShopHealed`, `TokenSummoned`, `EnemySplit`, `EnemySpawned`, `EnemyRevealed`, `EnemyEnraged`, `BossSpawned`. Any other type, or a new frame, closes it (an ally-group cast that reached nobody plays `skill_fizzle` on close).
- Per-enemy "latest incoming impact" table (32 entries, ring) mirrors `EnemyView.ExpectImpact / IncomingRemaining` for the kill-pop timing.

### 5.3 Timing (read-only mirrors; all FX-clock seconds)
| Quantity | Formula | Source |
|---|---|---|
| Guardian shot arrival | `AttackFxCatalog.FlightTime(Look(lineage), Style(style), distPx, speed, ActorScale)` | `AttackFxCatalog` (public static) |
| `distPx` | `length((WorldToField(enemy.Position) + up·40s) − (WorldToField(GuardianPosition(slot)) + up·70s))`, `s = ActorScale`; mapper = `FindAnyObjectByType<BattleFieldMapper>()` cached, re-found when null; fallback Ppu 205, Squash 0.72, s 1 (the sqrt/clamp in `FlightTime` makes ±20 px ≈ ±8 ms) | `BattleFieldMapper` public API |
| Echo melee hit | `0.05 × AttackFxCatalog.SpeedPopScale(speed)` | mirror of `BattleFx.EchoAttack` literal |
| Echo ranged hit | `0.14 × AttackFxCatalog.SpeedFlightScale(speed)` | mirror of `BattleFx.EchoShotLife` (private) |
| Token hit | 0 | `BattleFx.EchoAttack` |
| Kill pop | `clamp(max(KillHold(latestIncoming − now) if incoming, castDelay(enemy), lampDelay + 0.02), EnemyView.KillHold 0.14, 0.9)` | `AttackFxCatalog.KillHold`, `EnemyView.KillHold`, `EnemyView.MarkKilled` clamp |
| Lamp wave arrival | `LampWave.Start + LampWave.Delay(enemy.Position.magnitude)` (world distance from the shop) | `HitFeel.cs` `LampWave` |
| Skill projectile | `vfx_thread_stitch` 0.38 s, other projectile vfx 0.26 s; lane skills `BattleFx.StraightFlight(fromPx, laneEndPx)` | `BattleFx.SkillProjectile` literals (mirror) + public static |
| Skill beam | `0.03 + BattleFx.BeamReach × along` (`along` = enemy projection on the caster→lane-end segment, 0–1) | `BattleView.Cast.CastDelay` |
| All-enemies splash | `0.08 + min(0.45, distPx(centre → enemy) / 1400)` | `BattleView.Skills.SplashAllEnemies` |
| Chain hop | first hop = cast flight (projectile) or 0.08; then +0.09 per hop | `BattleView.Cast` `FirstHop` / `HopTime` (mirror) |
| Ally gift | 0.3 s when the recipient is ≥ 60 px from the caster | `BattleView.Cast.GiftTravel` (mirror) |
| Heal pulse | echo: `BattleFx.HealRingTime × clamp01(dist / radius)`; shop drop ≈ 0.4 s | `BattleFx.Pulse` public const |
| Support ribbon | `BattleFx.RibbonTravel` (0.35 s) | public const |
| Kill coin | every `EnemyFxCatalog.RewardEvery` (5) non-split, non-boss kills: `coin_pop` at pop + 0.08, `ui_coin_land` at + `RewardCoinFlight` (0.7) | `EnemyFxCatalog` public consts |
| Enemy strike gap | `EnemyFxCatalog.Look(id, boss).Interval / speed − 0.03` per enemy | mirror of `BattleView.Enemy.StrikeArtDue` |
| Global stops | §3.9 | `HitFeel`, `LampWave` |

Private literals are copied into `BattleAudioTiming` with a comment naming their source. `AudioTimingTests` pins them, so a change on the FX side shows up as a failing test (and §8 asks the FX workflow to expose them as public consts).

### 5.4 Event → sound
| Event | Condition | Sound(s) | When | Notes |
|---|---|---|---|---|
| `GuardianAttack` | target alive | `fire_{lineage}` (+ `atk_layer_snipe` for Snipe) | 0 | pan by guardian; launch bucket; 2× alternate skip; muted in sleep and during lamp release |
| `CriticalHit` | Basic/EchoBasic/Cast on that target | marks the pending hit as crit | — | shield absorbing everything → no crit sound (mirrors `OnCriticalHit`) |
| `EnemyHit` | Basic, `SourceId == slot`, same target | lineage hit — or `*_crit` + `hit_crit` + micro-duck; or `shield_block` if the enemy shield absorbs all — + style layer (§1.5) + `hurt_{family}` + extra (`atk_button_bounce`, `atk_candle_drip`) + 라라벨 ladder + tier-2 gold | flight | pan + proximity; hit bucket; crowd aggregator (§5.6) |
| `EnemyHit` | EchoBasic, same target | echo rule (§1.5) | echo delay | per-echo gate 0.12 s |
| `EnemyHit` | Cast | first 3 targets `hit_skill` −3 dB; others folded | `CastDelay` | |
| `EnemyHit` | Lamp | `hit_lamp` lamp ladder by arrival order (max 7), others silent | lamp delay | High class |
| `EnemyHit` | no context, `BurnTimer > 0` | `hit_dot` −3 dB | 0 | ≤ 1 per 0.5 s per enemy, 4/s |
| `EnemyHit` | no context, other | `hit_skill` −6 dB | 0 | 4/s |
| `ShieldBroken` | enemy | `shield_break` −3 dB + `atk_layer_heavy` | context delay | |
| `ShieldBroken` | shop / echo | `shield_break` (shop 0 dB, echo −5 dB) | 0 | |
| `EnemyKilled` | non-boss | `death_{family}` (or override) + kill ladder (`kill_chain` when the streak ≥ 2 within 0.8 s) + `kill_multi` when ≥ 5 in 1.0 s (then no chain for 0.5 s) + kill coin + `kill_mote` at pop + 0.60 s (FX-JUICE memory mote reaching the shop door; ≤ 2/s, −14 dB) | kill pop | deaths bucket |
| `EnemyKilled` | boss (`Value ≥ 0.5` or `IsBoss`) | `boss_kill_impact` + `boss_death`, global-stop mirror, Bgm/Sfx duck; `jingle_boss_defeated` at +0.3 s; chapter boss → music back to Main after the jingle | kill pop | Critical |
| `EnemySpawned` | not split, not boss | `enemy_spawn` −4 dB | 0 | ≤ 1 per 0.3 s |
| `EnemySpawned` | boss minion (`minionId` match within 0.6 world of the boss) | `boss_summon` once per frame | 0 | mirrors `OnEnemySpawned` |
| `BossSpawned` | — | `boss_appear` + `jingle_boss_appear` (+0.2 s) + duck; chapter boss → `PushMusic(Boss)` after 1.0 s; `amb_boss` on | 0 | |
| `EnemyStrike` | target = echo | strike key −3 dB + `echo_hurt` −6 dB | 0 | per-enemy gap; strike bucket |
| `EnemyStrike` | target = shop | strike key + `shop_hit`; absorbed (`ShopShield ≥ Value − 0.01`) → `shop_shield_hit`; boss → `shop_hit_boss` + `boss_siege_hit` and the windup loop stops | 0 | pan by the hit edge; `shop_hit` ≤ 1 per 0.12 s, never dropped by budgets |
| `ShopDamaged` | no `EnemyStrike` from that source this frame | same as the shop strike | 0 | mirrors `StruckShopThisFrame` |
| `ShopHealed` | pulse result / other | `heal_tick` at ≈ 0.4 s / `shop_heal` | — | ≤ 1 per 0.55 s |
| `EchoHealed` | — | `heal_tick` −3 dB | ring or gift delay | per echo 0.55 s, 3/s |
| `GuardianPulse` | `support` / `heal` | `pulse_support` / `pulse_heal` | 0 | pan by guardian |
| `ShieldGained` | shop once per frame; echo via status gate | `shield_gain_button` (vfx_button_shield), `shield_gain_umbrella` (vfx_umbrella_shield), else `shield_gain_bubble` | gift delay | |
| `StatusApplied` | no cast context, or the first target of a cast whose vfx differs from the status vfx | `status_*` by `SkillEffectType` (Stun, Slow, Mark, DamageOverTime→burn, Knockback, Pull, BuffAttack(Speed)→buff, Reveal→`status_reveal`, Cleanse, ResonanceGain, LampGain) | context delay | per (target, type) 0.9 s; per type 0.2 s; in-cast −4 dB |
| `StatusApplied` | enemy debuff on an ally (`SourceId < 0`, key ends with `vfx_debuff_mark`) | Slow → `enemy_rust`, Mark → `enemy_gnaw` | 0 | |
| `StatusApplied` | Heal from a Mender enemy | `enemy_mend` | 0 | 0.9 s |
| `StatusApplied` | Chain inside a cast | `status_chain` chain ladder | hop arrival | |
| `SkillCast` | — | `skill_charge` (echo −4 dB, +2 st) + `skill_{vfx}` → else `skill_style_{style}` → else `skill_cast`; `skill_{vfx}_hit` at the style delay when it exists; big skill (`IsBigSkill` mirror, ≤ 1 per 30 s per skill) → `skill_big` + duck | 0 | same frame + same skill id + shared target (mirror of `RememberCast`) → skip |
| ally-group cast | `AllEchoes` / `AllGuardians` without enemy effects | `skill_gift` per recipient (≤ 3) at 0.3 s; nobody → `skill_fizzle` | — | |
| `TokenSummoned` | — | `token_summon` | 0 | 0.15 s |
| `EchoSummoned` | — | `echo_summon` | 0 | centre pan, 0.12 s |
| `EchoDissolved` | — | `echo_dissolve`; refund > 0 → `echo_refund` at +0.35 s | 0 | 0.15 s |
| `LampLit` | — | `lamp_lit`, global-stop mirror, `lamp_wave` at `LampWave.Start`, Lamp context, ducks; launches muted 0.8 s, other hits at 50 % for 1.0 s | 0 | Critical |
| `LampDrained` | — | `boss_lamp_drain` | 0 | ≤ 1 per 0.6 s (DrainInterval) |
| `EnemySplit` / `EnemyRevealed` | — | `enemy_split` / `enemy_reveal` | 0 | reveal per enemy 0.9 s |
| `EnemyEnraged` | boss / normal | `boss_enrage` + Bgm −4 dB 1 s / `enemy_enrage` | 0 | |
| `SynergyActivated` | first in frame, only when the active set changed or it is the session's first stage | `battle_synergy` | +0.3 s | |
| `StageModifierStarted` | — | `mod_{rain, wind, blackout, fullmoon, dust, festival}` + ambience layer switch | +0.5 s | |
| `StageChanged` (system) | — | `battle_stage_start` (−6 dB if a jingle still sounds); reset streaks, gauge thresholds, low-HP state; record `HighestStage` | 0 | flush queue first |
| `StageEnded` Cleared | — | `jingle_stage_clear`; skipped if `jingle_boss_defeated` started < 1.5 s ago; first clear of an x-10 stage (`HighestStage` grew) → `jingle_chapter_clear` | 0 | stop heartbeat |
| `StageEnded` Failed | — | `jingle_stage_fail`; music Boss → Main | 0 | stop loops |
| `StepBack` | — | `battle_step_back` | +1.0 s | P2 |

### 5.5 Polled state (only when `_simSafe`)
| Poll | Rule |
|---|---|
| Boss windup | `w = sim.Boss?.SiegeWindup ?? 0`. `w > 0.02` → `boss_windup` on a Loop voice, volume = `w^1.3 × 0.8`, pitch = `0.9 + 0.3·w`, 30 ms fade-in; `w == 0` or the boss strike → 40 ms fade-out. Follows enrage speed-ups, stuns and knockbacks automatically |
| Shop HP | `r = ShopHp / ShopMaxHp`. Crossing below `EnemyFxCatalog.LowHpRatio` (0.3) → `shop_low_enter` (≤ 1 per 20 s) and the `shop_heartbeat` loop: volume lerp(−14 → −6 dB), pitch lerp(1.0 → 1.25) as `r` → 0. Exit at `r ≥ 0.35`, `ShopHp ≤ 0`, or stage end (0.5 s fade) |
| Lamp gauge | crossing 0.25 / 0.5 / 0.75 → `lamp_tick` gauge ladder; reaching 1 → `lamp_full` unless `LampLit` follows within 0.4 s (AUTO); still full and unlit → reminder `lamp_full` −8 dB every 12 s, max 3 |
| Wave | `sim.Wave` increases and > 1 → `battle_wave` |
| Boss alive | `sim.Boss != null` → `amb_boss` layer; chapter boss → Boss music request |
| Speed | `Game.Battle.Speed` → budgets and timing scale |
| Stage modifier | read at `StageChanged` → ambience layer |

### 5.6 Crowd control
- **Hit aggregator** (at commit time): hits due in the same 60 ms window — if ≥ 4, play the 2 most important individually (crit > boss target > nearest to the shop) plus one `hit_multi` at the window's mean pan, gain `−9 + 3·log2(N/2)` dB (cap 0 dB); drop the rest.
- Per-entity gates: hurt layer 0.25 s per enemy; launch 0.1 s per guardian; echo hit 0.12 s per echo; status 0.9 s per (target, type).
- Deaths: ≥ 3 in the same 80 ms → keep 2 + let `kill_multi` speak.
- Token buckets per category (§3.5); `shop_hit`, boss sounds, lamp and jingles bypass budgets (their own gates only).

### 5.7 Priority moments
| Moment | Window | Rule |
|---|---|---|
| Lamp release | 1.2 s | launches muted, hits 50 %, lamp notes High class, Bgm −8 dB |
| Boss kill | 0.8 s | only boss sounds + kill ladder at full level; others −6 dB |
| Boss appear | 2.0 s | Sfx −4 dB under the jingle |
| Boss windup / heartbeat | while active | Critical (never stolen) |

### 5.8 라라벨 sequence state
Per source (guardian slot 0–4, or echo id) a ring of 16 `{sourceId, index}`. Index 0–5 → `atk_musicbox` pitched C6 A5 F5 G5 A5 G5; index 6 → **rest** (only `atk_musicbox_ring`) for every form except `lala_finale`, which plays `atk_musicbox_finale` (F5) — then the sequence restarts. A crit plays `atk_musicbox_crit` (F chord) and does not advance the index. Several 라라벨 sources interleave like a round.

---

## 6. Audition window — **IdleGame/Audio/사운드 시험장**

`Editor/Audio/AudioAuditionWindow.cs` (`EditorWindow`, Korean labels, works in Edit and Play mode). Purpose: the user picks takes **by ear**; agents use the same data (QC metrics) to pre-select.

**Layout**
- **Toolbar:** search box; group filter (UI / 아군 공격 / 적·보스 / 수선점·보호막·잔향이·불빛 / 스킬 / 상태 / 흐름 / 환경음 / 배경음 / 징글 / 목소리 / 예약); priority filter (P0/P1/P2/P3); "문제만" toggle (missing, too few takes, QC fails); preview volume slider; **배경음 깔기** toggle (plays `bgm_battle_night` + `amb_night` underneath at the current settings, to judge the mix); **라이브러리 다시 만들기** (calls `AudioDataBuilder.Build()`); **폴더 열기**; **보고서 복사** (markdown summary of choices to the clipboard).
- **Left list:** every catalog key grouped as in §2, with a status dot (● ok, ◐ fewer takes than Var, ○ missing, ⚠ QC fail), active/total take counts and the user's average rating.
- **Right panel (selected key):** description, hook, bus/class/tier, target length and loudness, gates, ladder; **trim slider** (−12…0 dB, written to `AudioSelection`); "QC 추천으로 되돌리기"; note field.
- **Take rows:** ▶ play / ■ stop; **active toggle** (writes `AudioSelection.custom = true` + `activeTakes`); ★1–5 rating; waveform thumbnail (`AssetPreview.GetAssetPreview(clip)`); QC line (length · M-max/I LUFS · TP · attack ms · centroid · score · flags) from `audio_qc.json`; "masters" link to the master file and its render sidecar (route, seed, recipe, CC0 files).

**Players**
- Edit mode: hidden preview `AudioSource`s on a `HideFlags.HideAndDontSave` GameObject (no `AudioUtil` reflection), applying the same gain staging, trim, jitter, shuffle bag and ladder as runtime.
- **런타임 규칙으로 재생** plays as the game would (gate + jitter + bag). **사다리 ×8** plays a tuned key through its ladder (lullaby with the rest, kill ladder, lamp arpeggio, count ticks). **연타 테스트** fires the key 12 times at its hottest real rate (e.g. hits at 14/s) to expose machine-gun repetition. **루프 이음매** plays the last 2 s then the first 2 s of a loop back to back.
- **전투 미리 듣기 (10 s):** a scripted battle (5 guardians, one per lineage, launching and hitting at realistic rates with `AttackFxCatalog` timings, 2 echoes, enemy strikes, 6 kills, one crit chain, one lamp release, `bgm_battle_night` + `amb_night`) through the edit-mode player — the fastest way to judge the mix by ear. ×2 toggle replays it at 2× rules.
- **블라인드 A/B:** plays two random active-candidate takes of the key labelled A and B; the user picks; the winner's rating +1. Ten rounds settle an 8-candidate key.
- Play mode: toggles apply live (`AudioService.ReloadLibrary()` after a rebuild of that entry); "게임 안에서 재생" routes through `AudioService.Play`.

**Writes:** only `Assets/_Project/Audio/AudioSelection.asset` (and the library via the builder). The builder never overwrites the selection.

---

## 7. Production (binding AUDIO SOURCING RULES)

### 7.1 Routes and rules
| Route | Keys | What it is | Who/what makes it |
|---|---|---|---|
| **SYN** | 198 | Original synthesis rendered locally: modal, FM and physical-modelling plucks for music box, celesta, glockenspiel, harp and bells; filtered noise for whooshes, rustles, rain and wind; layered transients for impacts; convolution with synthetic IRs (§7.3) | producer agents, `python -I` scripts in `scratchpad/audio/synth/` |
| **HYB** | 65 | SYN recipe **plus** one optional CC0 foley layer from a Kenney pack (§7.2), always pitched, filtered, trimmed and layered (never shipped raw). Every HYB key is rendered both ways (with and without the CC0 layer) and QC/the user pick | same; CC0 files read only from `scratchpad/audio/cc0/**` |
| **MUS** | 22 | Original compositions written in this spec (§7.6–§7.7), rendered by the LLN sequencer with §7.4 instruments | same |
| **VOX** | 21 | Optional short Korean voice barks from Higgsfield **speech** models only (`seed_audio`, `text2speech_v2`, `elevenlabs_v4`), §7.9 | producer agent with the Higgsfield tools |

**Forbidden (binding):**
- Calling `sonilo_music`, `mirelo_text_to_audio` or `inworld_text_to_speech` for anything. `generate_audio` itself says they exist only for Higgsfield's game-generation pipeline.
- Using any speech model for SFX, ambience, music or jingles.
- Using `suno_music` while `models_explore(type: 'audio')` does not list it. It was not listed on 2026-11-04. Re-check before any music job; if it ever appears, it may only add *alternative* music takes, never replace the scores.
- Using any other AI generator, local or online.
- Using any non-CC0 material: Freesound non-CC0, Pixabay, Sonniss and "royalty-free" packs all count. A CC0 source other than Kenney is allowed only when its licence is recorded in `THIRD_PARTY_AUDIO.md`.
- Shipping a CC0 file unprocessed.
- Bypassing a website's bot check to download anything (§7.2).

### 7.2 CC0 sources (Kenney; ids used in §7.5)
All five packs below show **"License: Creative Commons CC0"** on their kenney.nl pages (checked 2026-11-04). The producer must open each archive's `License.txt` (or `license.txt`) and confirm the CC0 1.0 text before using any file. If the text is missing or different, the pack is not used.

| Pack (kenney.nl/assets/…) | Files | Set id → file family (search terms) | Used for |
|---|---|---|---|
| Impact Sounds (`impact-sounds`, v1.0, 2019) | 130 | `imp.soft` `impactSoft_heavy_*`, `impactSoft_medium_*` · `imp.wood` `impactWood_{light,medium,heavy}_*`, `impactPlank_medium_*` · `imp.glass` `impactGlass_{light,medium,heavy}_*` · `imp.metal` `impactMetal_{light,medium}_*`, `impactTin_medium_*` · `imp.carpet` `footstep_carpet_*` | felt thumps, wood knocks (deny, shop hit), glass tinks/shatter, tin (thimble, metal death), soft steps |
| Interface Sounds (`interface-sounds`, v1.0, 2020) | 100 | `ifc.click` `click_*`, `select_*` · `ifc.tick` `tick_*` · `ifc.toggle` `toggle_*`, `switch_*` · `ifc.scroll` `scroll_*` · `ifc.drop` `drop_*` · `ifc.glass` `glass_*` · `ifc.scratch` `scratch_*` | taps, ticks, toggles, ratchets, bloops, glass, pencil/zip texture. **Never** `error_*`, `glitch_*`, `question_*`, `confirmation_*` (tonal/buzzy) |
| UI Audio (`ui-audio`, v1.0, 2012) | 50 | `uia.click` `click*`, `mouseclick*` · `uia.switch` `switch*` | taps, detents, snaps |
| RPG Audio (`rpg-audio`, v1.0, 2014) | 50 | `rpg.cloth` `cloth*`, `clothBelt*` · `rpg.book` `bookFlip*`, `bookOpen`, `bookClose`, `bookPlace*` · `rpg.creak` `creak*`, `doorOpen_*`, `doorClose_*` · `rpg.coins` `handleCoins*` · `rpg.leather` `dropLeather`, `handleSmallLeather*` · `rpg.metal` `metalClick`, `metalLatch` · `rpg.pot` `metalPot*` | fabric swishes, pages, creaks, buttons-in-a-tin, bundles, latches |
| Casino Audio (`casino-audio`, v1.1) | 50 | `cas.card` `card-*`, `cards-pack-*` · `cas.chips` `chip-lay-*`, `chips-{collide,handle,stack}-*` · `cas.dice` `dice-*`, `die-*` | card flips/riffles, **plastic button clacks** (똑단이, coins), bouncing buttons |
| Music Jingles (`music-jingles`, 2014) | 85 | `jingles_{PIZZI,STEEL,HIT,NES,SAX}*` | **reference only** (not shipped): tempo/feel comparison for §7.7 |
| Digital Audio, Sci-fi Sounds, Voiceover packs | — | — | **not used** (sci-fi/speech do not fit the palette) |

**File names are search terms, not promises.** The families above are this pack's naming scheme. After extraction the producer lists the folder with a `python -I` script, writes `cc0/<pack>/MANIFEST.txt` (file, duration, peak, spectral centroid), and picks files by the §7.5 description plus the objective metrics.

**Download and safety procedure:**
1. Get the zip only through a normal browser-equivalent fetch of the official pack page (kenney.nl, or the pack's official mirror uploaded by Kenney on OpenGameArt). If the site refuses an automated fetch (HTTP 403 or a bot check), **stop**: do not spoof user agents or bypass the check. Ask the user to download the zip into the folder, or skip the CC0 layer. Every HYB key has a SYN path.
2. Save each archive into its **own new, empty folder** `scratchpad/audio/cc0/<pack-slug>/`.
3. Extract there with a `python -I` script kept in `scratchpad/audio/tools/`, using `zipfile`. Reject entries with absolute paths or `..`, and extract only `.ogg` / `.wav` / `.txt`. Nothing from the archive is executed or imported.
4. Convert to 48 kHz float with the toolkit's reader (OGG through `soundfile` if available, else `scipy` after a one-time ffmpeg-free decode path). If no safe decoder is available, skip the CC0 layer.
5. Record each used file in `Assets/_Project/Audio/THIRD_PARTY_AUDIO.md` **before** the take that uses it is imported.

**`THIRD_PARTY_AUDIO.md` format** (one row per external file actually used in a shipped take; the builder warns if a HYB take's sidecar names a file that is not listed):
```
# Third-party audio (all CC0 1.0 unless stated)
| Our key / take | Pack | Original file | Source URL | Licence (verified in) | Processing |
|---|---|---|---|---|---|
| atk_needle_03 | Kenney Interface Sounds 1.0 | tick_002.ogg | https://kenney.nl/assets/interface-sounds | CC0 1.0 (License.txt, 2026-11-xx) | +3 st, HP 2 kHz, −14 dB layer under SYN ping |
```
Kenney's CC0 needs no credit, but the in-game credits line "효과음 일부: Kenney (kenney.nl), CC0" is recommended (owner: settings/credits package).

### 7.3 Synthesis toolkit (`scratchpad/audio/synth/`, `python -I`, numpy + scipy only)
**Files:**

| File | Contents |
|---|---|
| `dsp.py` | primitives (§7.3.1) |
| `ir.py` | synthetic impulse responses (§7.3.2) |
| `patches.py` | the §7.4 instruments and foley models |
| `recipes.py` | one render function per key, written from §7.5 |
| `seq.py` | the LLN sequencer (§7.6.0) |
| `master.py` | §7.3.4 |
| `loops.py` | §7.3.5 |
| `qc.py` | §7.8 |
| `render_all.py` | CLI, e.g. `python -I render_all.py --priority P0 --takes gen --out ../masters` |

Scripts run from `scratchpad/audio/synth/` and read `../audio_catalog.json` and `../scores.json`.

**Conventions:**
- Internal sample rate 48,000 Hz, float64. Export at 44,100 Hz with `scipy.signal.resample_poly(x, 147, 160)`, then 16-bit TPDF dither. Masters are 24-bit and go to `scratchpad/audio/masters/{key}_{NN}.wav`.
- Deterministic seeds: `seed = zlib.crc32(key) ^ (take × 7919)`, using `numpy.random.Generator(PCG64(seed))`.
- Each render writes a sidecar `{key}_{NN}.json` with key, take, seed, route, the recipe text hash, any CC0 files with their processing, and all randomised parameter values.

#### 7.3.1 DSP primitives
| Function | Definition |
|---|---|
| `exp_decay(t60)` | `exp(−6.9078·t/T60)` (reaches −60 dB at T60) |
| `adsr(a,d,s,r)`, `ar(a,r,curve)` | linear/exponential segments; `curve` 0 = linear, 1 = exponential |
| `sine(f_t)` | phase accumulator `2π·cumsum(f_t)/SR`, so any pitch envelope is click-free |
| `saw_blep`, `square_blep` | PolyBLEP band-limited |
| `noise(kind)` | white (PCG64), pink (Paul Kellet 7-pole), brown (leaky integrator, a = 0.997, normalised) |
| `biquad(type,f,Q,gain)` | RBJ cookbook LP/HP/BP/peak/low- and high-shelf; `svf(x, fc_t, Q)` = TPT state-variable filter with per-sample cutoff (sweeps); one-pole LP/HP; `dc_block` (R 0.995) |
| `modal(f0, ratios, amps, t60s, hardness, seed)` | Σ aᵢ·sin(2π·f0·rᵢ·t + φᵢ)·exp_decay(T60ᵢ). Random φᵢ (seeded). Excitation is a raised-cosine strike of length `0.5 + 2.5·(1 − hardness)` ms; upper-mode amplitudes scale by `hardness^(i/2)`. Optional `beat_cents` adds a detuned twin of mode 1 at 25 % (shimmer) |
| `ks(f0, t60, brightness, pick, seed)` | Extended Karplus–Strong (Jaffe–Smith). Delay N = SR/f0 with a fractional allpass tuner. Loop filter `ρ·((1−S)+S·z⁻¹)` with `S = 0.5·(1 − brightness)` and `ρ = 10^(−3/(f0·T60))`. Pick-position comb `1 − z^(−⌊pick·N⌋)`. Excitation is a noise burst low-passed at `1.5 + 5·brightness` kHz |
| `fm2(fc, ratio, index_t)` | two-operator FM with an index envelope |
| `grains(src, n, span, pitches, dur, density_curve)` | granular scatter: Hann-windowed grains from a source function, pitches drawn from a list (in-key sets) |
| `chirp(f0, f1, dur, curve)` | exponential or linear frequency sweep (water drops, zips, bubbles) |
| `stick_slip(rate_t, jitter, body)` | impulse train with jittered period (±15 %) through a modal body, for creaks |
| `soft_clip(x, drive)` | `tanh(drive·x)/tanh(drive)`, warmth only (drive ≤ 1.5) |
| `widen(x, amount)`, `decorrelate(x, seed)` | mid/side width; allpass-chain decorrelation, for stereo beds only |

#### 7.3.2 Synthetic impulse responses (`ir.py`)
Each IR is a few early-reflection taps plus a late tail. The tail is stereo noise with three-band exponential decay (< 500 Hz, 0.5–4 kHz, > 4 kHz), a density ramp over the first 20 ms, and the given pre-delay. Normalise to unit energy. A recipe's `IR kind N %` means `out = dry + (N/100)·conv(dry, IR)` via `scipy.signal.fftconvolve`; mono SFX use the left channel.

| IR | Pre-delay | T60 low / mid / high | Early taps | Used for |
|---|---|---|---|---|
| `box` | 2 ms | 0.18 / 0.12 / 0.06 s | 4 taps, 3–9 ms, modal colour 180/420/950 Hz | music box body, small wooden objects |
| `shop` | 6 ms | 0.45 / 0.35 / 0.20 s | 6 taps, 8–25 ms | UI, shop knocks, bells inside the shop |
| `plaza` | 14 ms | 1.1 / 0.9 / 0.5 s | 8 sparse taps, 15–60 ms | night street, battle-wide sounds, ambience |
| `plate` | 4 ms | 1.4 / 1.2 / 0.8 s | none (dense at once) | bells, sparkles, magic |
| `hall` | 18 ms | ×(1.0–1.8 s)/1.4 of 1.8 / 1.4 / 0.9 s | 10 taps | music (the score's `reverb` field gives T60 mid) |
| `storeroom` | 22 ms | 2.6 / 2.4 / 1.2 s (dark) | 10 taps | dungeon music and ambience |

#### 7.3.3 Transient and envelope rules (where 타격감 comes from)
- **Onsets:** peak within 1–5 ms of the file start. Lead silence ≤ 1 ms below −60 dBFS, so scheduled hits land exactly. Clicks attack in ≤ 3 ms; felt, pops and puffs in ≤ 8 ms.
- **Weight without sub bass:** a pitch drop of 2–8 % over the first 30 ms on impacts, plus a 1–3 ms click layer aligned to the body's first peak (±1 ms).
- **Short and dry:** battle hit bodies ≤ 250 ms. Reverb on battle one-shots is ≤ 30 % of the shipped length. Tails reach −60 dB by the catalog length.
- **Separation:** each lineage's hit transient sits in its §1.4 lane (QC checks the onset band, §7.8).
- **Variety without randomness:** takes differ by the §7.5 "vary" axes and seeds. Tonal re-voicing stays inside the F-pentatonic set (F G A C D).

#### 7.3.4 Mastering chain (`master.py`, every take, in order)
1. **DC block and high-pass.** Each recipe sets its own HP. Otherwise: UI 120 Hz; battle one-shots 120 Hz; boss, timpani and heartbeat 45 Hz; ambience 40 Hz; music 35 Hz.
2. **"Nothing harsh":**
   - dynamic EQ on 2.8–4.5 kHz (Q 1.2, up to −4 dB) when its 50 ms band level exceeds the take's 1–2 kHz band by more than 3 dB;
   - static −1.5 dB shelf above 9 kHz on sparkle and glass keys;
   - de-ess at 6–9 kHz on sustained tails.
3. **Mono fold for SFX:** (L+R)/2 after a polarity check. BGM, ambience and jingles stay stereo, with correlation ≥ 0.2.
4. **Gain to the tier target** (§1.8): M-max for SFX, S-max for jingles, integrated for music and ambience.
5. **True-peak limiter:** 4× oversampled detector, 1 ms lookahead, 30 ms release. Ceiling −1.0 dBTP for SFX and jingles, −1.5 dBTP for music, −3.0 dBTP for ambience.
6. **Fades and trim:**
   - fade-in 0.3 ms only (never dull an onset);
   - fade-out 10 ms, or 20 % of the tail if shorter;
   - trim the tail where it stays below −60 dBFS for more than 30 ms.
7. **Export:** 44.1 kHz 16-bit WAV, TPDF dither, plus the 24-bit master.

#### 7.3.5 Loop construction (`loops.py`)
**Music:**
- Render **three contiguous passes** of the score and export pass 2: from bar 1 of pass 2 up to (not including) bar 1 of pass 3.
- The export is exactly the §7.6 sample count. Pass 1's tails ring into pass 2's head exactly as pass 2's tails would ring into pass 3, so the loop is seamless with no fades.
- Intros render alone with their tail and are cut at exactly 2 bars. The tail carries into the loop as the loop's own pass-1 tail.

**Ambience:**
- Render 2 × loop length + 4 s with an event schedule that keeps one-shot events (creaks, chimes, fireworks) out of the last and first 2.5 s.
- Take a loop-length window and fold the 2 s after it back over its head with an equal-power cos/sin crossfade.
- Length is exactly 40.000 s (1,764,000 samples), or 20.000 s for `amb_boss`.

**Battle loops** (`shop_heartbeat`, `boss_windup`): render 4 s and cut exactly 1.000 s at zero crossings with a 5 ms seam crossfade. The heartbeat holds exactly one "통…통" pair per loop.

**Seam QC:**
- RMS step < 0.5 dB between the last and first 50 ms;
- spectral flux at the seam < 1.5 × the take's median;
- no sample step > 0.02 FS;
- for music, no note onset within 3 ms of the seam unless it is bar 1's downbeat.

### 7.4 Patch library (`patches.py`)
Every patch is deterministic given a seed. Parameters in parentheses are the defaults; recipes override them. Pitches are equal-tempered (A4 = 440 Hz).

**Tuned instruments (used by recipes and by the sequencer):**

| Patch | Model | Defaults |
|---|---|---|
| `MBOX` | music-box comb tine: `modal` | ratios 1 / 5.93 / 16.6; amps 1 / .35 / .12; T60 1.6·√(523/f0) s / 0.25 / 0.06; `beat_cents` 2; pluck click 2 ms HP 3 kHz −24 dB; pitch settles −3 c over 30 ms; body = IR box 10 % + 3 resonances (180/420/950 Hz, Q 8, −18 dB) |
| `CELESTA` | struck bar + resonator: `modal` + FM shimmer | ratios 1 / 2.756 / 5.404; amps 1 / .18 / .06; T60 1.2 / .35 / .12 s; felt hammer 3 ms LP 4 kHz; FM ratio 1, index 0.6 → 0 in 80 ms |
| `GLOCK` | steel bar: `modal` | ratios 1 / 2.756 / 5.404 / 8.933; amps 1 / .5 / .25 / .1; T60 2.5 / .8 / .3 / .12 s; hard mallet 1 ms; LP 7 kHz; level −3 dB vs MBOX |
| `HANDBELL` | small bell: `modal` | ratios .5 / 1 / 1.25 / 1.5 / 2 / 2.51 / 2.67 (major-third tierce, so happy and in key); amps .3 / 1 / .5 / .25 / .6 / .2 / .15; T60 scale 1.0 (≈2 s prime); strike 2 ms. `large` = f0/2 and T60 ×1.6 |
| `SHOPBELL` | brass door bell | 2 detuned `HANDBELL`s (±7 c) + clapper re-strikes at 40/90/150 ms (−6/−12/−18 dB) + 1 kHz brass ping T60 0.3 s |
| `GONG` | soft gong: `modal` | ratios 1 / 1.47 / 2.09 / 2.56 / 3.39; T60 1.4 s; felt mallet 6 ms; slow pitch glide +4 c over 0.5 s |
| `CHIME` | tube/wind chime: `modal` | ratios 1 / 2.76 / 5.40 / 8.93; T60 2–4 s; random notes of the set C6–D7 |
| `GLASS` | glass tink/star: `modal` | ratios 1 / 2.32 / 4.25 / 6.63; T60 0.25–0.6 s; stiff click 0.7 ms |
| `GHARM` | glass harmonica / bowed glass | sine + octave (−12 dB) + 3rd harmonic (−24 dB); attack 60–300 ms; vibrato 4.5 Hz ±6 c after 0.4 s; airy noise BP f0×2 at −36 dB |
| `HARP` | `ks` | brightness 0.55; T60 4 s at A3 scaling ∝ (220/f0)^0.6; pick 0.12; soundboard modal 120/260/520 Hz (Q 6); letRing |
| `PIZZ` | violin/viola pizz: `ks` | brightness 0.45; T60 0.45 s; pick 0.2; finger noise 3 ms; body peaks 280/450 Hz and a small 2.4 kHz bite at −14 dB; damped 120 ms after the notated length |
| `BASSPIZZ` | double bass pizz: `ks` | brightness 0.35; T60 0.9 s; LP 1.8 kHz; thump 1 ms; damped 150 ms after the notated length |
| `COLLEGNO` | wood on string | click (noise 2 ms BP 2 kHz) + `ks` with T60 0.08 s |
| `FELTPIANO` | additive piano with felt | partials n = 1…12 at n·f0·√(1 + 0.0004·n²); amps n^−1.4; felt LP 1.8 kHz on attack falling to 900 Hz; hammer thump 60–120 Hz −18 dB; 2 strings ±1.2 c; T60 3 s (low) to 0.9 s (high); release 0.25 s |
| `PAD` | warm muted strings | 3 PolyBLEP saws ±6 c per note → LP 1.6 kHz (24 dB/oct, Q 0.7); attack 0.8 s; release 1.5 s; chorus 0.3 Hz; ties identical pitches; `art trem` = 8 Hz amplitude modulation 70 % |
| `CELLO` | bowed low string | PolyBLEP saw → body formants 200/500/1200 Hz; bow noise BP 2 kHz −30 dB; vibrato 5 Hz ±12 c after 0.3 s; `art stacc` = 0.12 s envelope; `art trem` = bow-stroke tremolo 8 Hz |
| `REED` | soft clarinet-like | odd harmonics 1/3/5/7 at 1 / .32 / .14 / .06; breath noise BP 1.5 kHz −32 dB; attack 40 ms; vibrato 5.2 Hz ±7 c after 250 ms |
| `FLUTE` | wooden flute / ocarina | sine + 2nd (−18 dB) + 3rd (−26 dB); 20 ms chiff (noise burst); breath BP f0×2 −28 dB; vibrato 5 Hz ±10 c |
| `HORN` | soft horn | PolyBLEP saw → LP 1.2 kHz with envelope-tracked brightness; attack 120 ms; ±4 c ensemble of 2 |
| `ORGAN` | reed organ | square + saw mix → LP 2.2 kHz; slight air; attack 30 ms; ties identical pitches |
| `KALIMBA` | `modal` tine + box | ratios 1 / 5.9 / 15.4; T60 1.2 s; soft thumb pluck 3 ms; box resonance 300 Hz |
| `MARIMBA` | wooden bar: `modal` | ratios 1 / 4 / 10; amps 1 / .25 / .08; T60 0.9 s (low) to 0.35 s (high); rubber mallet LP 3 kHz |
| `TIMP` | felt timpani | membrane ratios 1 / 1.5 / 2 / 2.5; T60 1.5 s; felt attack LP 600 Hz; `ROLL` = 16 strokes/s with ±2 dB randomness |
| `TOYSNARE` | toy snare with brushes | noise burst BP 1.8 kHz (40 ms) + 220 Hz shell modal T60 60 ms; ghost = noise only; `ROLL` = 24 strokes/s |
| `FELTBD` | felt bass drum | sine 70 → 50 Hz over 60 ms, T60 0.3 s; felt click LP 400 Hz |
| `FELTTOM` | felt tom | sine 98 → 85 Hz, T60 0.25 s; membrane 1.59 ratio −10 dB |
| `TRIANGLE` | `modal` | ratios 1 / 2.9 / 5.6 (f0 1.3 kHz), T60 1.2 s, very soft, LP 9 kHz |
| `SLEIGH` | sleigh bells | 8 tiny `GLASS` jingles (3–6 kHz, T60 60 ms) with random onsets in 15 ms; LP 8 kHz |
| `CLAP` | soft hand clap | 3 noise bursts 8–12 ms apart, BP 1.2 kHz Q 0.8 + IR shop 10 % |
| `WOODBLOCK` | `modal` | ratios 1 / 2.57 / 4.6; f0 800 Hz; T60 60 ms; LP 3 kHz |

**Foley, impact and magic models:**

| Patch | Model | Defaults |
|---|---|---|
| `WOODTICK` | small wood: `modal` | ratios 1 / 2.57 / 4.6; f0 0.6–2.2 kHz; T60 15–60 ms; 1 ms click |
| `FELT` | felt mallet / plush thump | sine with pitch drop ×0.7 over 40 ms; A 2 ms, D 20–150 ms; + noise LP 800 Hz for 20 ms; no click |
| `PLUSH` | plush squeeze/bounce/squeak | `FELT` + rubbery formant BP (600–1300 Hz, Q 4) swept with the envelope; air noise −24 dB |
| `CLICK` | plastic click | two transients 6–12 ms apart; each `modal` 1 / 1.47 / 2.31 at f0 1.6–3.2 kHz, T60 15 ms + 1 ms noise |
| `CLACK` | hollow button/chip | `CLICK` + hollow body `modal` 400–800 Hz, T60 30–60 ms; option `jar` adds `GLASS` body 1.1/2.3 kHz T60 120 ms |
| `LATCH` | key/lock mechanism | 2–3 steel clicks `modal` 1 / 2.6 / 5.1 at 2.8–4.6 kHz (T60 25 ms); tumbler rattle 5 micro-clicks 7 ms apart (−16 dB); `deadbolt` adds a slide (noise BP 2 kHz, 40 ms) and a `FELT` 140 Hz thunk |
| `SNIP` | scissors | 2 blade `LATCH` clicks 25 ms apart + shear noise BP 4 kHz 30 ms |
| `RATCHET` | wind-up / detents | N clicks with spacing gliding from s₀ to s₁ and pitch rising up to +2 st; brass f0 2.2 kHz (plastic option 1.6 kHz) |
| `SPRING` | coil spring 'boing' | triangle at f0 300–450 Hz with FM vibrato 12–18 Hz, depth ±4 st decaying over 0.4 s + coil partials 1 / 2.1 / 3.3 |
| `MOTOR` | toy motor / whirr | pulse train 30–60 Hz (gliding) through BP 700–1400 Hz; AM by the gear-tooth rate |
| `TIN` | dull tin/thimble | `modal` 1 / 1.6 / 2.3 (/ 3.1) at f0 0.8–1.5 kHz, heavily damped (T60 50–90 ms) + felt damp. **Never rings** |
| `METALPING` | small bright metal (needle, key, medal) | `modal` 1 / 2.76 / 5.4 at f0 2.6–4.4 kHz, T60 40–220 ms, hard strike |
| `JINGLE` | screws/gears scatter | N `TIN`/`METALPING` micro-hits at random 1.5–4 kHz over a span + optional bounce |
| `KNOCK` | door/box knock | wood `modal` (1 / 2.3 / 3.8 or 1 / 2.4 / 3.9) f0 110–360 Hz, T60 45–120 ms + `FELT` 80–100 Hz |
| `CREAK` | wood creak | `stick_slip` 30–120 Hz through wood modes 250/610/1100 Hz |
| `HEART` | wooden heartbeat | two `FELT` thumps (95 Hz, then 80 Hz, 130 ms apart) + box body 220/480 Hz T60 80 ms |
| `GLINT` | tiny bright ping | `GLASS` at C7–C8 (in the set), T60 40–150 ms, soft hammer |
| `SPARKLE` | granular glitter | 6–60 `GLASS`/`GLOCK` grains of 20–60 ms, notes from the set C6–C8, density envelope; HP 2.5 kHz, LP 9 kHz; IR plate 15 % |
| `SHIMMER` | swell or reverse swell | `SPARKLE` or a `GLASS` cluster through a reversed plate IR (rise 0.08–1.2 s), optional pitch sweep |
| `HUM` | soft hum / dome | sine + octave, slow attack, optional vibrato/AM; LP 1.5–3 kHz |
| `WHOOSH` | air | pink noise → `svf` BP sweep (Q 1.2) with a bell envelope (40/60 % rise/fall) |
| `SWISH` | fabric | `WHOOSH` at BP 0.4–2.2 kHz, Q 0.8 + flap AM 14–30 Hz + rustle grains |
| `ZIP` | thread zip / light streak | noise BP sweep 2→4.5 kHz (or reverse) + quiet sine chirp; LP 7–8 kHz |
| `PAPER` | flick/flip/slide/turn/rustle | white noise BP 1.8–3 kHz with flap AM 25–45 Hz, crinkle impulses 5–30 ms; page turn 0.4–0.6 s |
| `STAMP` | rubber stamp | `FELT` 110–140 Hz + paper slap (noise LP 3 kHz, 20 ms) + squish (BP sweep 600 → 400 Hz, 50 ms) |
| `POP` | cute pop / bloop | sine 300–700 Hz with fast downward chirp (25–40 ms) + 1 ms click |
| `CORK` | smack-pop '쪽' (not a mouth) | suction noise HP 1.2 kHz rising 15–25 ms → `POP` 550–780 Hz → glint 6 kHz −22 dB |
| `BUBBLE` | soap/water bubble | sine with upward chirp 250 → 900 Hz (60–120 ms) + glassy edge |
| `DROP` | water drop | sine chirp up (600 → 1.6 kHz, exponential, 15–25 ms), A 1 ms, D 30 ms (bubble-resonance model) |
| `SPLASH` | splash/crown | noise BP 1–4 kHz decay 80–160 ms + 5–12 `DROP` droplets; `big` adds a 200 Hz plunk |
| `FLAME` | flame flick/bloom/flare | pink noise LP sweep 400 → 2.4–3.2 kHz → 700 Hz; 'fwump' sine 130 → 90 Hz −8 dB; optional `EMBER` |
| `MATCH` | match strike | scratchy noise BP 2.5 kHz with grainy AM 200 Hz (70–80 ms) → `FLAME` ignition |
| `EMBER` | crackle | sparse clicks BP 1.8–4 kHz, Poisson rate 20–40/s, decaying |
| `WAX` | wax plop/drip/goo | `DROP` lowered (380–900 Hz), LP 3 kHz; goo = BP 300–700 Hz with 8 Hz wobble |
| `DUST` | dust puff/poof/swirl | brown + pink noise LP 1.2–1.5 kHz, A 5 ms, D 60–150 ms; 'poof' sine 90 Hz −12 dB; swirl = BP sweep with a reversed envelope |
| `MOTH` | papery wing beats | beats at 18–28 Hz: each BP noise 1.5–3 kHz (12 ms) + 280–300 Hz flap |
| `POWDER` | fine grains | HP 4–5 kHz grains, very low level (−18 to −30 dB), LP 8 kHz |
| `RUST` | wet rusty splat/fizz | squelch (BP sweep 900 → 400 Hz, Q 6) + noise LP 2.2 kHz + scrape AM 60 Hz; fizz = noise HP 1.5 kHz LP 4 kHz with crackle |
| `YARN` | yarn/thread/ribbon | whip (BP sweep 0.8 → 2.6 kHz, 90 ms + felt snap), unravel (rapid pulls), stretch (rubbery sine 220 → 120 Hz), flutter (AM 20 Hz swish) |
| `SHADOW` | airy dark whoosh | pink noise LP 0.9–2.5 kHz with a **reversed** envelope; sub-octave sine glide 160 → 90 Hz −14 dB; muffled |
| `DRONE` | low uneasy drone | sines 110/165 Hz (or 73/110) beating 0.7 Hz + noise LP 500 Hz swelling at 0.5 Hz |
| `THUNDER` | far soft thunder | brown noise LP 300 Hz, 1.2 s rumble envelope + crackle LP 1 kHz |
| `FIREWORK` | far firework | soft thump LP 200 Hz + crackle (2–5 kHz, LP, decaying 0.8 s) + IR plaza 40 % |
| `POPPER` | paper party popper | soft crack (noise HP 800 Hz, 10 ms) + confetti rustle grains (300 ms) |
| `TOYHORN` | tiny party/sour horn | square-ish reed with a pitch bend, LP 1.5–3 kHz, very short |
| `CRICKET` | far crickets | 4.2–4.8 kHz sine pulses (3–5 per chirp at 30 Hz), chirps every 0.5–1.2 s per insect, LP 6 kHz, IR plaza |
| `WIND` | wind bed / gust | pink/brown noise → BP with cutoff on a smoothed random walk (0.05–0.2 Hz, 250–900 Hz, Q 2–4); gust amplitude walk |
| `RAIN` | rain bed / patter | Poisson `DROP`s and ticks (300–800/s) + pink bed HP 200 Hz LP 6 kHz; awning = LP 1 kHz thuds; gutter = slow `DROP`s + trickle |
| `SAND` | sandy grit | HP 3 kHz grains, LP 5–6 kHz, low level |
| `PLUCK` | taut thread twang | `ks` with brightness 0.8, T60 0.2 s, pitch bend −40 c |

That makes **80 patches**: 30 tuned instruments and 50 foley/magic models. `GONG`, `SHOPBELL` and `SNIP` are compositions of other patches.

### 7.5 Per-key recipes
#### 7.5.0 How to read the recipe tables
- **Recipe** = layers joined by `+`. `@N` is the start offset in ms from the file start (the onset is at 0 unless stated). `→` is processing applied to the sum, before the §7.3.4 chain. Patch names are §7.4. Notes are scientific pitch; `k` = kHz; `st` = semitones; `c` = cents.
- **CC0 start** = an optional layer from a §7.2 set id. It is always processed as written, then layered under or over the SYN layers. HYB keys render **both** versions (with and without CC0), and both count toward Gen.
- **Var/Gen** = shipped takes / rendered candidates. QC (§7.8) ranks the candidates, and the user's picks in the audition window (§6) override.
- **Takes vary by** — "default" means the group rule:

| Group | Default variation between takes |
|---|---|
| UI | seed; layer offsets ±10 %; untuned f0 ±3 %; hammer hardness ±0.1; level ±1 dB. Tonal keys re-voice inside the F-pentatonic set (F G A C D) instead of detuning |
| Ally attacks | seed; untuned f0 ±4 %; click length ±0.5 ms; body T60 ±15 %; layer offsets ±8 ms. Tuned keys vary hammer hardness only |
| Enemies, bosses | seed; f0 ±6 %; envelope times ±15 %; noise colour (pink ↔ brown mix ±20 %) |
| Defence, skills, status, flow | seed; layer timing ±10 %; tonal layers re-voiced within the set; sparkle density ±20 % |
| Ambience | each take is a different event schedule (seed); bed levels ±1 dB |
| Music, jingles | 4 render candidates: (A) as written; (B) humanise seed 2; (C) melody +1.5 dB and sends ×0.85; (D) MBOX hardness 0.6 → 0.75 and celesta FM index ×1.2 |
| Voice barks | 3 reads × 2 voices; pacing and pitch hints in the model's own parameters (§7.9) |

- **Tuned keys** (§1.3) store `rootHz` measured by QC (YIN with sub-cent parabolic refinement) in the sidecar and in `audio_qc.json`. Multi-root keys (`hit_lamp`, `kill_chain`) ship takes at both listed roots, and the engine picks the take nearest to the target note (§3.5).
- **Tonal check:** every `inkey` take must have ≥ 85 % of its tonal energy on the pitch classes F G A C D (± 30 c). Jingles and music are checked against their score's key instead.

#### 7.5.1 UI (`UISfxKey` → library key, Ui bus)

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `ui_tap` | HYB | 0.06 s · 4/8 | WOODTICK(f0 1.25k, T60 30 ms) + FELT(190 Hz, 20 ms, −10 dB) → HP 150, LP 6k, IR box 5 % | ifc.click / uia.click: −2…+3 st, LP 5k, first 60 ms, under the FELT layer | f0 1.1–1.4k · FELT ±2 dB |
| `ui_tab` | HYB | 0.12 s · 3/6 | PAPER(flick 50 ms, BP 2.4k) + WOODTICK(950 Hz, 25 ms, −6 dB)@55 → LP 7k | cas.card (card-place-*) or rpg.book (bookPlace*): first 120 ms, HP 300, LP 7k | flick BP 2.1–2.8k · tick 0.9–1.05k |
| `ui_popup_open` | HYB | 0.18 s · 2/5 | SWISH(up 600→1.6k, 140 ms, felt AM 22 Hz) + HANDBELL(C6, major-third bell, T60 0.35 s, −8 dB)@120 → IR shop 8 % | rpg.cloth (cloth1–4): swish part only, +2 st, HP 250 | bell C6 or F6 · swish 120–160 ms |
| `ui_popup_close` | HYB | 0.14 s · 2/5 | SWISH(down 1.4k→500, 110 ms) + FELT(150 Hz, 30 ms, −12 dB)@100 → LP 5k | rpg.cloth: tail of cloth2/cloth3, −1 st | default |
| `ui_deny` | HYB | 0.18 s · 2/5 | 2× KNOCK(wood box 330 Hz, modes 1/2.4/3.9, T60 45 ms, felt-damped)@0/85 (2nd −3 dB, −1 st) → HP 120, LP 3.5k. Never tonal, never a buzzer | imp.wood (impactWood_light_*, impactPlank_medium_*): −5 st, LP 3k, first 70 ms per knock | gap 75–95 ms · f0 300–360 Hz |
| `ui_coin_land` | HYB | 0.12 s · 4/8 | CLICK(plastic 2.4k, 8 ms) + GLINT(C7, T60 60 ms, −14 dB)@3 → HP 300, LP 8k. Runtime adds the +3 % coin ladder | cas.chips (chip-lay-*, chips-collide-*): first 90 ms, +4 st | click 2.2–2.8k · glint level ±2 dB |
| `ui_gem_land` | HYB | 0.25 s · 3/6 | GLASS(star charm f0 2.6k, T60 0.18 s) + SPARKLE(6 grains C7–G7, 150 ms, −10 dB)@20 → LP 9k, IR plate 10 % | imp.glass (impactGlass_light_*) +5 st first 200 ms, or ifc.glass (glass_00*) | default |
| `ui_level_up` | SYN | 0.6 s · 3/6 | CELESTA arpeggio F5 A5 C6 (80 ms apart, vel .80/.85/1.0, T60 0.4 s) + GLOCK(C7, −12 dB)@170 + SPARKLE(8 grains, 250 ms, −14 dB)@160 → IR plate 12 %. Short tail: safe when held/repeated | — | spacing 70–90 ms · top note C6 or F6 |
| `ui_stamp` | HYB | 0.3 s · 2/5 | STAMP(thump 140 Hz + paper slap + ink squish) + GLOCK(F6, −14 dB)@180 + GLOCK(C7, −16 dB)@230 → IR shop 6 % | imp.soft (impactSoft_medium_*) −3 st as the thump; cas.card (card-place-*) as the slap | default |
| `ui_claim` | HYB | 0.35 s · 3/6 | HANDBELL(F6, major-third, T60 0.6 s) + 4× CLACK(buttons in a tin, 40–70 ms apart, −12 dB)@20 → IR shop 8 % | rpg.coins (handleCoins, handleCoins2): 250 ms, HP 400, +2 st, as the tin rattle | bell F6 or A6 · 3–5 clacks |
| `ui_gacha_flip` | HYB | 0.15 s · 3/6 | PAPER(card flip: 3 flaps at 45 Hz, BP 2.2k, 90 ms) + WHOOSH(air 400→1.2k, 80 ms, −10 dB) → LP 7k | cas.card (card-slide-*, card-fan-*): 150 ms | default |
| `ui_gacha_rare` | SYN | 1.2 s · 2/6 | MBOX run F5 G5 A5 C6 D6 F6 (45 ms apart) + HANDBELL(F6 + C6, T60 1.0 s)@300 + SPARKLE(30 grains, 800 ms, LP 9k)@250 + SHIMMER(reverse plate, 200 ms)@100 → IR plate 18 % | — | run 40–55 ms · bell voicing F6/C6 or A6/F6 |
| `ui_sheet_open` | HYB | 0.32 s · 2/5 | SWISH(linen up 400→1.3k, 260 ms, AM 14 Hz) + WOODTICK(800 Hz, 25 ms, −10 dB)@280 → LP 6k | rpg.cloth (cloth*, clothBelt*): time-stretch ×1.2, HP 200, LP 5k | default |
| `ui_sheet_close` | HYB | 0.26 s · 2/5 | SWISH(linen down 1.1k→400, 200 ms, −3 dB) → LP 5k | rpg.cloth: as ui_sheet_open, −2 st | default |
| `ui_main_button` | SYN | 0.45 s · 2/5 | PLUSH(pom 160 Hz, 60 ms) + HANDBELL(round C6, T60 0.5 s, −4 dB)@40 → IR shop 10 % | — | bell C6 or F6 |
| `ui_confirm` | SYN | 0.4 s · 2/5 | WOODBLOCK(tock 1.0k, 40 ms) + CELESTA(C6, −4 dB)@60 + CELESTA(F6)@150 → IR plate 10 % | — | default |
| `ui_spend` | HYB | 0.4 s · 3/6 | 4× CLACK(plastic click into a glass jar: click + GLASS body 1.1k/2.3k T60 120 ms)@0/70/130/190, each −2 dB → IR shop 6 % | cas.chips (chips-stack-*, chips-handle-*): cut 4 clacks; imp.glass −8 dB as the jar body | 3–5 clacks · spacing ±15 % |
| `ui_toast` | HYB | 0.28 s · 2/5 | PAPER(slip slide 200 ms, BP 1.8k, soft) + WOODTICK(1.4k, −8 dB)@220 | cas.card (card-shove-*) | default |
| `ui_red_dot` | HYB | 0.15 s · 2/5 | POP(bead bloop 600→300 Hz, 30 ms) + GLINT(C7, −16 dB)@20 → LP 7k | ifc.drop (drop_00*): +2 st, first 120 ms | default |
| `ui_quest_complete` | HYB | 0.7 s · 2/5 | PAPER(pencil check: 2 strokes, BP 3k, 60 + 90 ms) + CELESTA F5 A5 C6 (90 ms apart)@180 → IR plate 10 % | ifc.scratch (scratch_00*) as the pencil | default |
| `chest_open` | HYB | 1.2 s · 2/5 | CREAK(lid, 300 ms, wood 250/610 Hz) + LATCH(brass, 2-stage)@280 + SPARKLE(40 grains, 600 ms)@380 + HANDBELL(F6, −6 dB)@400 → IR shop 10 % | rpg.creak (creak1–3, doorOpen_*) as the lid; rpg.metal (metalLatch) as the latch | default |
| `ui_stamp_special` | SYN | 1.2 s · 1/4 | STAMP(big, 110 Hz) + HANDBELL chord F5+A5+C6 (T60 1.2 s)@120 + POPPER(confetti)@150 + SPARKLE(25 grains)@200 → IR shop 12 % | — | default |
| `basket_claim` | HYB | 1.0 s · 2/5 | SWISH(wicker rustle: BP 1.2k noise + crinkle grains, 300 ms) + 8× CLACK cascade (50–80 ms apart, −2 dB slope)@200 + SPARKLE(20 grains)@500 + HANDBELL(A6, −10 dB)@600 | rpg.coins (handleCoins*) as the cascade; rpg.cloth as the rustle | default |
| `quick_idle` | HYB | 1.2 s · 2/5 | 8× WOODTICK clock ticks accelerating (150→40 ms apart, +1 st each) + WHOOSH(rise 300→2.4k, 300 ms)@700 + CHIME(F6, T60 0.8 s)@1000 → IR plate 12 % | ifc.tick (tick_00*) as the ticks | default |
| `ui_count_tick` | SYN | 0.06 s · 2/6 | GLASS(bead tink, render at F6 1396.9 Hz, partials 1/2.32, T60 45 ms) + CLICK(2 ms, −14 dB) → HP 400. Tuned: store rootHz | — | 2 takes, same root, hammer hardness 0.4/0.7 |
| `ui_count_finish` | SYN | 0.5 s · 2/5 | HANDBELL(F6, T60 0.5 s) + CLACK(button clink, −8 dB)@10 + SPARKLE(10 grains, −14 dB)@40 | — | default |
| `ui_reward_double` | SYN | 0.9 s · 1/4 | SHIMMER(sweep up, 400 ms) + HANDBELL(F6)@380 + HANDBELL(C7, a fifth up)@520 → IR plate 15 % | — | default |
| `ui_power_up` | SYN | 0.45 s · 2/5 | CELESTA C6 D6 F6 (60 ms apart, soft) + SPARKLE(8 grains) → IR plate 8 % | — | default |
| `ui_level_milestone` | SYN | 1.2 s · 2/5 | GLOCK F5 A5 C6 F6 (70 ms) + HARP roll F3→F5 (12 notes, 25 ms)@0 + HANDBELL(F5 large, T60 1.4 s)@300 + SPARKLE(30 grains) → IR plate 15 % | — | default |
| `ui_friend_level_up` | SYN | 0.6 s · 2/5 | PLUSH(bounce: 2 squeezes 180→220 Hz, 50 ms apart) + SPARKLE(12 grains)@80 + GLINT(C7)@120 | — | default |
| `ui_resonance_up` | SYN | 1.0 s · 2/5 | HANDBELL(A5) + HANDBELL(C6)@120 + SHIMMER(600 ms)@100 + HUM(glass F4, 800 ms, −14 dB) → IR plate 18 % | — | default |
| `ui_evolve_charge` | SYN | 1.4 s · 1/4 | YARN(thread winding onto a spool: AM 8→30 Hz on BP 1.2→2k noise, 1.2 s) + SHIMMER(riser 1.2 s, +12 st sweep) → IR plate 12 % | — | default |
| `ui_evolve_burst` | SYN | 0.8 s · 2/5 | WHOOSH(bloom 200 Hz→3k, attack 30 ms, decay 400 ms) + GLASS cluster F6 A6 C7 D7 (T60 0.6 s) + SPARKLE(40 grains) → IR plate 20 % | — | default |
| `ui_costume_equip` | HYB | 0.6 s · 3/6 | SWISH(fabric, 250 ms) + CLICK(button snap 1.8k, 10 ms)@220 + SPARKLE(10 grains)@240 | rpg.cloth (cloth*, clothBelt*); uia.switch (switch*) as the snap | default |
| `ui_costume_unlock` | SYN | 0.8 s · 2/5 | YARN(ribbon untie: soft pull + flutter, 300 ms) + CELESTA A5 C6 F6 (70 ms)@300 → IR plate 10 % | — | default |
| `gacha_bundle_drop` | HYB | 0.5 s · 2/5 | PLUSH(cloth bundle lands: FELT 120 Hz + fabric noise 80 ms) + 3× WOODTICK(string beads 1.6–2k, 40 ms apart, −14 dB)@90 | rpg.leather (dropLeather, handleSmallLeather*): −2 st; imp.soft (impactSoft_medium_*) | default |
| `gacha_bundle_open` | SYN | 0.7 s · 2/5 | YARN(string untie pull, 150 ms) + SHIMMER(swell, 400 ms)@150 + SPARKLE(20 grains)@450 → IR plate 15 % | — | default |
| `ui_gacha_card` | HYB | 0.15 s · 3/6 | PAPER(card pop: flick 40 ms) + POP(400 Hz, −10 dB) | cas.card (card-place-*) | default |
| `ui_gacha_anticipate` | HYB | 0.5 s · 2/5 | SHIMMER(rising gold, 450 ms, +12 st sweep) + PAPER(card rattle: 6 flaps 25 ms apart, BP 2.5k, −10 dB) | cas.card (card-shove-*, card-fan-*): jittered slices | default |
| `ui_gacha_reveal_common` | HYB | 0.35 s · 2/5 | PAPER(soft pop) + WOODTICK(1.2k)@30 → LP 6k | cas.card (card-place-*) | default |
| `ui_gacha_reveal_rare` | SYN | 0.5 s · 2/5 | POP(450 Hz) + GLASS(C6 chime, T60 0.3 s)@30 → IR plate 8 % | — | default |
| `ui_gacha_reveal_epic` | SYN | 0.8 s · 2/5 | HANDBELL A5 + C6 (60 ms apart) + SPARKLE burst (25 grains)@40 → IR plate 12 % | — | default |
| `ui_gacha_reveal_mythic` | SYN | 1.6 s · 1/5 | MBOX full lullaby C6 A5 F5 G5 A5 G5 F5 (90 ms apart; the final F5 is held, T60 1.5 s) + GHARM pad F4 A4 C5 (attack 300 ms)@540 + HANDBELL F6/C7 shimmer@540 + SPARKLE cloud (60 grains)@560 → IR plate 25 %. The only UI sound that plays the missing 7th note | — | default |
| `ui_gacha_dup_melt` | SYN | 0.6 s · 2/5 | SHIMMER(glassy dissolve: reversed GLASS cluster, 400 ms) + GLINT(C7, −8 dB)@450 | — | default |
| `ui_gacha_skip` | HYB | 0.5 s · 2/4 | PAPER(card riffle: 12 flaps accelerating 30→18 ms, BP 2.6k) → LP 7k | cas.card (card-fan-*, card-shove-*): time-compressed ×0.7 | default |
| `ui_drag_pick` | SYN | 0.15 s · 2/5 | PLUSH(lift: squeeze 200→260 Hz, 60 ms + fabric whisper) → LP 4k | — | default |
| `ui_drag_drop` | HYB | 0.2 s · 3/6 | FELT(thud 130 Hz, 50 ms) + CLICK(button 2.0k, 8 ms, −8 dB)@25 | imp.carpet (footstep_carpet_*): −4 st as the thud | default |
| `ui_drag_swap` | SYN | 0.25 s · 2/4 | 2× WOODTICK(1.1k then 1.3k, 70 ms apart, 2nd −2 dB) | — | default |
| `ui_toggle_on` | HYB | 0.1 s · 1/4 | WOODTICK(1.5k, 20 ms) + CLICK(5 ms, −10 dB) | uia.switch (switch*) or ifc.toggle (toggle_00*): +2 st | default |
| `ui_toggle_off` | HYB | 0.1 s · 1/4 | WOODTICK(1.0k, 25 ms) | same as ui_toggle_on, −3 st | default |
| `ui_slider_tick` | HYB | 0.06 s · 2/4 | WOODTICK(abacus bead 1.8k, 15 ms, felt-damped) → HP 500 | ifc.tick (tick_00*), ifc.scroll (scroll_00*) | default |
| `ui_expedition_depart` | HYB | 1.0 s · 2/4 | PLUSH pitter-patter (6 soft steps 90 ms apart) + SHOPBELL(door bell A5/C6 + clapper)@500 → IR shop 12 % | imp.carpet (footstep_carpet_*) +6 st as tiny steps | default |
| `ui_expedition_return` | HYB | 1.0 s · 2/4 | FELT(sack thump 100 Hz) + SWISH(rustle, 300 ms)@80 + HANDBELL(A5)@500 → IR shop 10 % | rpg.leather (dropLeather, handleSmallLeather*) | default |
| `ui_dungeon_sweep` | HYB | 0.9 s · 2/4 | WHOOSH(fast, 200 ms) + 10× CLACK (35 ms apart, alternating ±2 st)@150 + GLINT(F7)@600 | rpg.coins (handleCoins2) time-compressed; cas.chips (chips-handle-*) | default |
| `ui_mail_claim` | HYB | 0.8 s · 2/4 | PAPER(envelope unfold: 2 crinkles, 120 ms) + 4× CLACK(buttons)@300 + GLINT@520 | rpg.book (bookOpen) as the unfold; cas.chips | default |
| `ui_purchase` | SYN | 0.7 s · 2/5 | SWISH(wooden drawer slide: BP 600 Hz rumble + wood rattle, 250 ms) + SHOPBELL('딸랑': C6 + A5 pair + clapper)@300 → IR shop 12 % | — | default |
| `ui_coach_pop` | HYB | 0.2 s · 2/5 | BUBBLE(pop 250→700 Hz, 50 ms) + GLINT(−18 dB) | ifc.drop (drop_00*) | default |
| `ui_tutorial_step` | SYN | 0.4 s · 2/4 | MARIMBA C5 → F5 (90 ms apart) → IR shop 8 % | — | default |
| `ui_story_page` | HYB | 0.6 s · 3/6 | PAPER(page turn: 450 ms rustle sweep + soft flap) | rpg.book (bookFlip1–3, bookOpen): HP 200, LP 7k | default |
| `ui_sleep_enter` | SYN | 1.2 s · 1/4 | MBOX A5 F5 C5 falling (220 ms apart, ritardando, last T60 1.2 s) + FELT(sigh: noise swell LP 600 Hz, 500 ms, −16 dB)@200 → IR plate 20 %. No voice | — | default |
| `ui_sleep_wake` | SYN | 0.5 s · 1/4 | CELESTA C6 → F6 (80 ms) + SPARKLE(8 grains) → IR plate 10 % | — | default |
| `ui_speed_toggle` | HYB | 0.4 s · 2/4 | RATCHET(wind-up: 10 clicks accelerating 40→15 ms, rising) + MOTOR(toy motor 30→60 Hz pulse train through BP 700 Hz, −14 dB) | ifc.scroll (scroll_00*) as ratchet clicks | default |
| `ui_auto_toggle` | HYB | 0.4 s · 2/4 | RATCHET(clockwork, 5 clicks 45 ms apart) + CLICK(lock 2.2k)@260 | rpg.metal (metalClick) as the lock | default |
| `ui_boss_challenge` | SYN | 0.8 s · 1/4 | TOYSNARE(roll 400 ms, crescendo) + SHOPBELL(C6/F6)@420 → IR shop 10 % | — | default |
| `achievement_claim` | HYB | 0.5 s · 2/5 | 2× METALPING(medal clink, brass 2.6k, modes 1/2.7/5.2, T60 120 ms, 50 ms apart) + SPARKLE(12 grains)@60 → IR plate 8 % | imp.metal (impactMetal_light_*) +5 st, first 150 ms | default |
| `ui_codex_discover` | SYN | 0.8 s · 2/5 | STAMP(light album stamp) + SHIMMER(discovery, 400 ms)@120 + GLASS(C7)@450 | — | default |
| `ui_affinity_up` | SYN | 0.8 s · 2/5 | HANDBELL(A5 → C6, 110 ms apart) + SHIMMER(fluffy: pink noise LP 2k swell + sparkle, 500 ms)@100 | — | default |
| `pet_squeak` | SYN | 0.2 s · 4/8 | PLUSH(squeak: sine 900→1300 Hz through a rubbery BP formant, 120 ms, + air) → LP 5k. Not a voice | — | pitch 0.85–1.2 · 4 shapes (up, up-down, double, long) |
| `ui_map_node` | SYN | 0.6 s · 2/4 | YARN(thread knot pull, 120 ms) + WOODTICK(pin)@110 + CHIME(F6)@140 | — | default |
| `ui_tier_up` | SYN | 0.8 s · 2/4 | YARN(ribbon flutter, 250 ms) + CELESTA C6 D6 F6 A6 (60 ms)@200 | — | default |
| `ui_blessing_pick` | HYB | 0.7 s · 2/4 | PAPER(card slide, 250 ms) + SHIMMER(300 ms)@150 + GLINT(F7)@450 | cas.card (card-slide-*) | default |
| `ui_reward_big` | SYN | 1.2 s · 2/4 | FELT(whoomp 90 Hz, 150 ms) + HANDBELL(F5 large)@60 + POPPER(confetti)@80 + SPARKLE(35 grains)@100 → IR shop 15 % | — | default |
| `ui_countdown` | HYB | 0.12 s · 2/4 | WOODTICK(clock tick 2.2k, 10 ms) + body (500 Hz, 30 ms, −12 dB) | ifc.tick (tick_00*) | default |
| `ui_progress_fill` | SYN | 0.6 s · 2/4 | MOTOR(gear whirr rising 200→500 Hz, 550 ms) + 6 gear-tooth ticks → LP 4k | — | default |
| `ui_defeat_advice` | SYN | 0.6 s · 1/4 | MARIMBA A4 → G4 → C5 (fall–rise 'hmm', 120 ms apart, soft) → IR shop 8 %. No voice | — | default |

#### 7.5.2 Battle — ally attacks (Sfx bus)

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `fire_needle` | HYB | 0.15 s · 4/8 | ZIP(thread zip: BP sweep 2→4.5k, 90 ms) + GLINT(needle 3.4k, T60 40 ms, −10 dB)@60 → HP 600, LP 7k | ifc.scratch (scratch_00*): +5 st as zip texture, −10 dB | sweep 1.8–2.3k start · glint ±150 Hz |
| `atk_needle` | HYB | 0.22 s · 5/10 | METALPING(needle '칭': modes 1/2.76/5.4, f0 2.9k, T60 70 ms) + FELT('퐁' 300 Hz, 25 ms, −4 dB)@3 + 2× CLICK(stitch tick 4.2k, 3 ms, −14 dB)@45/80 → HP 180, LP 7.5k | ifc.tick (tick_00*) +3 st, HP 2k as the stitch ticks; imp.soft (impactSoft_medium_*) +7 st, first 40 ms, LP 2k as the felt pop | ping 2.7–3.1k · tick gap 30–40 ms · pop 260–340 Hz |
| `atk_needle_crit` | HYB | 0.35 s · 3/6 | SNIP(scissor: 2 blade clicks 25 ms apart + shear noise BP 4k 30 ms) + METALPING(3.1k, T60 220 ms)@20 + FELT(260 Hz, −6 dB) → LP 8k | rpg.metal (metalClick) ×2, +4 st, as blade clicks | default |
| `fire_button` | HYB | 0.15 s · 4/8 | CLICK(thumb flick 1.6k) + MOTOR(spinning button: 40 Hz AM on BP 1.2k noise, 100 ms, −8 dB)@10 → LP 5k | cas.chips (chip-lay-*) as the flick; ifc.scroll as the whirr | default |
| `atk_button` | HYB | 0.18 s · 5/10 | CLACK(hollow button: click 2.2k modes 1/1.47/2.31 T60 18 ms + body 700 Hz T60 35 ms + micro-click @6) → HP 200, LP 7k, IR box 4 % | cas.chips (chips-collide-*): first 120 ms, +2 st | body 620–780 Hz · click 2.0–2.5k |
| `atk_button_crit` | HYB | 0.32 s · 3/6 | CLACK(big coat button: body 420 Hz T60 60 ms, click 1.6k) + 4× CLACK(small buttons 2.6–3.2k, 25–40 ms apart, −10 dB)@40 + FELT(160 Hz, −6 dB) → IR box 6 % | cas.chips (chips-stack-*) as the rattle | default |
| `atk_button_bounce` | HYB | 0.4 s · 3/6 | 3 tiny buttons, each CLICK(2.4–3.2k) on bouncing-ball timing (gaps 120/85/60/42 ms, ×0.7 per bounce), offsets 0/30/55 ms → LP 7k | cas.dice (dice-throw-*): +6 st, LP 6k (alternative take) | default |
| `fire_key` | SYN | 0.12 s · 4/8 | ZIP(bright key zip: chirp 2.5→5k, 70 ms) + GLINT(4.2k, T60 50 ms, −12 dB) → LP 8k | — | default |
| `atk_key` | HYB | 0.2 s · 5/10 | LATCH('찰-칵': click 4.4k @0 + click 3.6k @38, steel modes 1/2.6/5.1 T60 25 ms) + tumbler rattle (5 micro-clicks 7 ms apart, −16 dB)@8 + body (brass 900 Hz, T60 40 ms, −10 dB) → HP 300, LP 8k | rpg.metal (metalLatch first 150 ms +3 st, metalClick) | click gap 32–44 ms · ±3 % pitch |
| `atk_key_crit` | HYB | 0.35 s · 3/6 | LATCH(deadbolt '철컥!': slide noise BP 2k 40 ms + heavy click 2.8k + FELT thunk 140 Hz) + HANDBELL(F6 shimmer, T60 0.3 s, −10 dB)@60 → IR shop 6 % | rpg.metal (metalLatch) −2 st; rpg.creak (doorClose_*) first 80 ms LP 1.5k as the thunk | default |
| `fire_candle` | SYN | 0.18 s · 4/8 | FLAME(flick '화륵': pink noise BP 600→1.8k→900 Hz, 150 ms) + EMBER(3 crackles) → LP 5k | — | default |
| `atk_candle` | SYN | 0.3 s · 5/10 | FLAME(bloom '후앙': noise LP 400→2.4k→700 Hz, A 25 ms, D 250 ms + fwump sine 130→90 Hz, −8 dB) + WAX(plop 380→700 Hz chirp, 30 ms, −10 dB)@120 → LP 6k, IR box 4 % | — | bloom peak 2.0–2.8k · plop on 60 % of takes |
| `atk_candle_crit` | SYN | 0.4 s · 3/6 | FLAME(flare: LP sweep to 3.2k, 350 ms) + EMBER(12 crackles over 300 ms, BP 2–4k, −10 dB)@40 + FELT(160 Hz, −6 dB) → LP 7k | — | default |
| `atk_candle_drip` | SYN | 0.2 s · 3/6 | WAX(drip '똑': chirp 500→900 Hz, 25 ms, LP 3k) + sizzle (noise HP 3k, 60 ms, −20 dB)@20 | — | default |
| `fire_rain` | SYN | 0.15 s · 4/8 | SWISH(canopy flick: fabric snap 60 ms) + DROP(flick 900→1.8k, 15 ms, −8 dB)@40 → LP 6k | — | default |
| `atk_rain` | SYN | 0.25 s · 5/10 | DROP('톡': 700→1.6k chirp, 18 ms) + SPLASH('찰방': noise BP 1–4k, decay 90 ms, 6 droplets)@15 → HP 200, LP 7k | — | drop start 600–800 Hz · 4–8 droplets |
| `atk_rain_crit` | SYN | 0.4 s · 3/6 | SPLASH(water crown: 160 ms, 12 droplets) + 3× DROP ring (40 ms apart, rising) + FELT(130 Hz, −8 dB) → LP 7k | — | default |
| `atk_rain_big` | SYN | 0.4 s · 3/6 | SPLASH(big: 'plunk' bubble 200 Hz + 10 droplets + ripple tail 250 ms) → LP 6k | — | default |
| `fire_kiss` | HYB | 0.15 s · 4/8 | RATCHET(lipstick twist: 3 smooth plastic detents 30 ms apart) + CLICK(cap 2.6k)@100 → LP 7k | uia.switch (switch*) as detents; cas.chips (chip-lay-*) as the cap | default |
| `atk_kiss` | SYN | 0.15 s · 5/10 | CORK(smack-pop '쪽': suction noise HP 1.2k rising 20 ms + pop sine 700→350 Hz 25 ms + glint 6k −22 dB) → LP 7k. Never a human mouth | — | pop 620–780 Hz start · suction 15–25 ms |
| `atk_kiss_crit` | SYN | 0.3 s · 3/6 | CORK(bigger pop 550 Hz) + SPARKLE(hearts: 8 GLASS grains C7–F7, 200 ms)@30 + BUBBLE(−10 dB)@20 | — | default |
| `fire_musicbox` | SYN | 0.15 s · 3/6 | RATCHET(wind-up key '끼릭': 4 brass clicks 35→25 ms apart, 2.2k) → IR box 6 % | — | default |
| `atk_musicbox` | SYN | 0.4 s · 2/8 | MBOX(single comb note rendered at A5 880.0 Hz; partials 1/5.93/16.6, T60 1.1 s/0.2 s/0.05 s, 2-cent beating twin) → box resonance + IR box 10 %, release fade from 400 ms (150 ms). Tuned: store rootHz; runtime plays the lullaby ladder | — | 2 takes: hammer hardness 0.5 / 0.75 (same root) |
| `atk_musicbox_ring` | SYN | 0.3 s · 3/6 | HUM(sound ring: glass hum A5 + A6, swell 50 ms, decay 250 ms, AM 6 Hz) → IR plate 10 % | — | default |
| `atk_musicbox_crit` | SYN | 0.6 s · 2/6 | MBOX chord F5 + A5 + C6 (strummed 12 ms) + GLASS(C7, −12 dB) → IR box 10 % + IR plate 8 %. rootHz = 698.46 | — | default |
| `atk_musicbox_finale` | SYN | 0.9 s · 2/6 | MBOX(F5, soft hammer) + HANDBELL(F5 warm, T60 1.2 s) + GHARM(F4 + C5 swell, 400 ms) → IR plate 20 %. rootHz = 698.46 | — | default |
| `atk_generic` | SYN | 0.15 s · 3/6 | POP(lavender orb '뽁': sine 650→320 Hz, 30 ms + click) + GLINT(−16 dB) → LP 6k | — | default |
| `fire_generic` | SYN | 0.12 s · 3/6 | WHOOSH(soft orb 300→900 Hz, 90 ms) → LP 4k | — | default |
| `hit_crit` | SYN | 0.2 s · 3/8 | GLINT('팅!': GLASS star modes 1/2.32/4.25 at C7 2093 Hz, T60 120 ms) + METALPING(F6 1396.9 Hz, T60 90 ms, −6 dB) + click 1 ms → HP 500, LP 9k, IR plate 6 % | — | 3 takes: C7/F6, C7/A6, D7/A6 (all in the set) |
| `atk_layer_slash` | SYN | 0.15 s · 3/6 | SWISH(felt slash: BP 900→2.2k, 90 ms, fast attack) → LP 6k | — | default |
| `atk_layer_snipe` | SYN | 0.15 s · 3/6 | ZIP('쮸웅': thin streak chirp 4k→2k, 100 ms) + LP sweep 7.5k→3k (doppler feel) | — | default |
| `atk_layer_thread` | SYN | 0.25 s · 3/6 | PLUCK(taut thread twang '띠잉': KS f0 220–330 Hz, brightness 0.8, T60 200 ms, bend −40 c) | — | f0 within F/G/A/C/D 3–4 |
| `atk_layer_heavy` | HYB | 0.15 s · 3/6 | FELT(mallet thump 120 Hz, 60 ms) + noise LP 800 Hz (20 ms) | imp.soft (impactSoft_heavy_*): −2 st, first 120 ms | default |
| `atk_layer_gold` | SYN | 0.25 s · 3/6 | GLINT(tiny gold twinkle: GLASS F7 2793.8 Hz + C8 −8 dB, T60 80 ms) → IR plate 6 % | — | default |
| `echo_slash` | SYN | 0.15 s · 4/8 | SWISH(glassy light swish: BP 1.5→3.5k, 100 ms) + SHIMMER(30 ms tail, −12 dB) → IR plate 8 % | — | default |
| `echo_token_pop` | SYN | 0.15 s · 3/6 | POP(sock token '퐁': 420→260 Hz, 40 ms) + fabric puff (noise LP 1.5k, 30 ms, −10 dB) | — | default |
| `hit_multi` | SYN | 0.3 s · 3/6 | 5 micro-hits within 120 ms, random from {CLACK, POP, GLINT}, each −2 dB, pitch spread ±3 st → LP 7k | — | default |
| `hit_skill` | SYN | 0.15 s · 4/8 | POP('톡-샤': 500 Hz, 20 ms) + SPARKLE(5 grains, 100 ms, −8 dB)@10 | — | default |
| `hit_dot` | SYN | 0.15 s · 4/8 | EMBER('탁': 3 crackles 15 ms apart, BP 1.8–3.5k) + sizzle (−16 dB) | — | default |
| `hit_lamp` | SYN | 0.35 s · 2/8 | CELESTA(warm, soft hammer, T60 0.5 s) + GLINT(−14 dB) → IR plate 12 %. Tuned multi-root: render roots F5 and F6 (2 takes each) | — | default |
| `shield_block` | SYN | 0.15 s · 3/6 | HUM('붕' hex bubble deflect: sine 320 Hz with fast vibrato ±50 c, 60 ms) + GLASS ping 1.8k (−10 dB) → LP 5k | — | default |

#### 7.5.3 Battle — enemies and bosses (Sfx bus)

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `hurt_dust` | SYN | 0.12 s · 4/8 | DUST(puff '푸', 60 ms, LP 1.2k) | — | default |
| `hurt_moth` | HYB | 0.12 s · 4/8 | MOTH(papery flick: 2 wing beats, BP 2k, 12 ms each) | cas.card (card-slide-*): −3 st, first 80 ms | default |
| `hurt_metal` | HYB | 0.12 s · 4/8 | TIN(dull tin ping '똥': modes 1/1.6/2.3, f0 1.1k, T60 50 ms, felt-damped) | imp.metal (impactTin_medium_*): −2 st, first 90 ms, LP 3.5k | default |
| `hurt_shadow` | SYN | 0.12 s · 4/8 | SHADOW(cold air '쉬익': noise BP 900 Hz, reversed envelope 80 ms) → LP 2.5k | — | default |
| `death_dust` | SYN | 0.5 s · 4/8 | DUST(poof 150 ms, LP 1.5k + low poof 90 Hz) + SPARKLE(cream sparkles rising: 10 grains C6→F7 over 300 ms, −10 dB)@120 → IR plate 10 % | — | default |
| `death_moth` | HYB | 0.6 s · 3/6 | MOTH(wing tear: paper rip BP 2.5k 60 ms + 6 slowing wing beats) + POWDER(drift, 400 ms, HP 4k, −18 dB)@100 | cas.card (card-*) as the rip | default |
| `death_metal` | HYB | 0.5 s · 4/8 | JINGLE(screws/gears scatter: 8 TIN/METALPING micro-hits 1.5–4k over 0–250 ms + one bounce '통' 350 Hz @180) → LP 7k | imp.metal (impactMetal_light_*) +4 st slices; rpg.coins (handleCoins*) −3 st as scatter | default |
| `death_shadow` | SYN | 0.7 s · 3/6 | SHADOW(unravel upward: noise BP 400→2.8k over 500 ms, reversed envelope) + GLASS(tiny bell C7, T60 0.4 s)@520 → IR plate 14 % | — | default |
| `death_tangle` | SYN | 0.5 s · 3/6 | YARN(unravel: 8 rapid string pulls 30 ms apart, rising) + POP(woolly 300 Hz)@380 | — | default |
| `death_windup` | SYN | 0.6 s · 3/6 | SPRING('보잉' 380 Hz, FM vibrato 14 Hz ±4 st decaying, 400 ms) + JINGLE(gear clatter, 5 hits)@60 | — | default |
| `enemy_puff` | SYN | 0.3 s · 4/8 | DUST(clump '푸슉': two-stage puff 40 ms + 140 ms, LP 1.4k) + FELT(70 Hz, −10 dB) | — | default |
| `enemy_flutter` | HYB | 0.3 s · 4/8 | MOTH('파닥파닥': 6 wing beats at 22 Hz, each BP 1.6–2.4k + low flap 280 Hz) | cas.card (card-fan-*) −4 st as wing paper (−8 dB) | default |
| `enemy_splat` | SYN | 0.35 s · 4/8 | RUST('철퍽': squelch BP sweep 900→400 Hz Q 6 + noise LP 2.2k 80 ms + scrape 120 ms AM 60 Hz −8 dB + TIN ping −16 dB) | — | default |
| `enemy_whip` | SYN | 0.3 s · 4/8 | YARN(whip '휙-탁': BP sweep 800→2.6k, 90 ms + felt snap @100) | — | default |
| `enemy_scratch` | SYN | 0.3 s · 4/8 | SHADOW(3 scratches '스윽': noise BP 1.8k sweeping down, 60 ms each, 45 ms apart) → LP 3k | — | default |
| `enemy_clang` | HYB | 0.3 s · 4/8 | TIN(felt-muted thimble '깡': modes 1/1.9/2.8/3.7, f0 820 Hz, T60 90 ms) + FELT(160 Hz) → LP 4k. Never ringing | imp.metal (impactMetal_medium_*) −3 st, LP 3k, gated at 120 ms; rpg.pot (metalPot*) | default |
| `enemy_boing` | SYN | 0.35 s · 4/8 | RATCHET('끼릭': 3 clicks 25 ms apart) + SPRING('보잉' 420 Hz, 250 ms)@80 | — | default |
| `boss_wave` | SYN | 1.2 s · 2/6 | SHADOW(swell '웅—': noise LP 900 Hz rising 700 ms + sine pair 98/104 Hz beating + reversed plate) + TIMP(muffled '쿵' 70 Hz)@750 → LP 3k, IR plaza 15 % | — | default |
| `boss_vortex` | SYN | 1.0 s · 2/6 | POWDER/MOTH swirl: noise BP 1.2k with 6 Hz tremolo rising 0.8 s + wing-beat texture 26 Hz (−10 dB) + low sweep 150→250 Hz (−12 dB) | — | default |
| `enemy_spawn` | SYN | 0.35 s · 4/8 | DUST(swirl in '스르': noise BP sweep 600→1.4k, reversed envelope 250 ms) → LP 2k | — | default |
| `enemy_split` | SYN | 0.4 s · 3/6 | 2× POP(yarn ball '퐁퐁': 380 Hz then 450 Hz, 90 ms apart) → LP 2k | — | default |
| `enemy_reveal` | SYN | 0.5 s · 2/5 | SHIMMER(veil lifted: reversed GLASS shimmer, 350 ms) + SWISH(fabric lift, 200 ms, −8 dB) | — | default |
| `enemy_enrage` | SYN | 0.6 s · 2/5 | SWISH(angry cloth rustle, 300 ms, AM 30 Hz) + CELLO(rising bowed scrape: +3 st over 500 ms, LP 900 Hz, −6 dB) → IR plaza 10 % | — | default |
| `enemy_gnaw` | SYN | 0.3 s · 3/6 | PAPER(nibble '사각': 4 tiny tearing crunches 50 ms apart, BP 3k) | — | default |
| `enemy_rust` | SYN | 0.4 s · 3/6 | RUST(fizz '찌익': noise HP 1.5k LP 4k 300 ms + crackle grains) + CREAK(tin creak, short, −10 dB) | — | default |
| `enemy_mend` | SYN | 0.5 s · 2/5 | RATCHET(winding '끼릭끼릭': 2 × 4 clicks, 120 ms gap, 1.8k) | — | default |
| `boss_appear` | SYN | 1.6 s · 2/6 | CELLO(low swell D2 + A2 bowed, attack 0.6 s, vibrato 5 Hz) + TIMP(felt D2)@500 + SHADOW(reversed whoosh 500 ms into @500) + DRONE(73 + 110 Hz, −10 dB) → IR plaza 20 %. Weight from cello/timpani harmonics, not sub bass | — | default |
| `boss_windup` | SYN | 1.0 s · 1/5 | CREAK(stick-slip creak texture: rate 40–90 Hz drifting, wood modes 300/700/1.1k) + CELLO(tremolo 8 Hz on D3, −8 dB) → seamless 1.000 s loop (render 4 s, 0.25 s equal-power seam, cut at zero crossings) | — | default |
| `boss_siege_hit` | HYB | 0.6 s · 2/5 | KNOCK(heavy thud on the door: wood modes 180/420 Hz + FELT 80 Hz) + JINGLE(shelf rattle: 6 TIN hits 30 ms apart, −12 dB)@60 → IR shop 12 % | imp.wood (impactWood_heavy_*) −4 st; rpg.creak (doorClose_*) | default |
| `boss_enrage` | SYN | 1.0 s · 1/4 | CELLO(two-voice rising bowed swell, +5 st over 0.9 s) + DUST(rattling dust: noise AM 20 Hz, LP 2k, −8 dB) → IR plaza 15 %. No voice | — | default |
| `boss_summon` | SYN | 0.8 s · 2/5 | SHADOW(vortex opening rumble: noise LP 600 Hz swelling + 80 Hz sine gliding up) → IR plaza 15 % | — | default |
| `boss_lamp_drain` | SYN | 0.8 s · 2/5 | SHIMMER(light sucked into dark: GLASS shimmer pitch-falling −12 st over 600 ms, LP 8k→800 Hz) + GLASS(fading chime C6, −10 dB)@650 | — | default |
| `boss_death` | SYN | 2.0 s · 2/6 | SHADOW(unravel, 600 ms) + cascade of 8 GLASS/HANDBELL notes C6 D6 F6 G6 A6 C7 D7 F7 (70 ms apart) + SPARKLE(60 grains) + GHARM(F major swell)@500 → IR plate 25 % | — | default |
| `boss_kill_impact` | SYN | 0.5 s · 2/5 | TIMP(felt timpani F2 87.3 Hz, T60 0.6 s) + soft glass crack (noise HP 2k 15 ms + 8 crackle grains, −8 dB) → IR shop 10 % | — | default |

#### 7.5.4 Battle — shop, shields, support, echoes, lamp

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `shop_hit` | HYB | 0.4 s · 4/8 | KNOCK(door knock '쿵': wood modes 1/2.3/3.8, f0 160 Hz, T60 0.12 s + FELT 90 Hz) + CREAK(wood creak, 250 ms, −8 dB)@60 → IR shop 10 % | imp.wood (impactWood_heavy_*, impactPlank_medium_*) −3 st; rpg.creak (creak1–3) first 300 ms | knock 140–180 Hz · creak on 3 of 4 takes |
| `shop_hit_boss` | HYB | 0.6 s · 2/5 | KNOCK(deeper 110 Hz) + JINGLE(rattling shelves, 8 hits) + DUST(falling dust, −12 dB)@150 → IR shop 14 % | imp.wood (impactWood_heavy_*) −6 st | default |
| `shop_shield_hit` | SYN | 0.3 s · 3/6 | HUM('부웅' dome absorbs: sine 240 Hz, LP 1.2k + muffled noise thump) → LP 2.5k | — | default |
| `shop_heal` | SYN | 0.5 s · 2/5 | CREAK(warm house creak, 150 ms, LP 2k, −8 dB) + HANDBELL(A5 → C6, 90 ms)@80 → IR shop 10 % | — | default |
| `shop_low_enter` | SYN | 0.8 s · 1/4 | 2× HANDBELL(soft descending C6 → A5, 200 ms apart) → IR plaza 10 %. Not an alarm | — | default |
| `shop_heartbeat` | SYN | 1.0 s · 1/4 | HEART('통…통': FELT 95 Hz + 80 Hz, 130 ms apart, wood body 220/480 Hz) → exact 1.000 s loop (60 BPM); runtime pitch ≤ ×1.25 | — | default |
| `shield_gain_button` | SYN | 0.5 s · 2/5 | CLACK(big button, 450 Hz body) + HUM(dome up: sine 300→450 Hz, 300 ms, LP 1.5k, −6 dB)@40 | — | default |
| `shield_gain_umbrella` | SYN | 0.5 s · 2/5 | SWISH(umbrella opens '팡': fabric snap 40 ms + whoosh 150 ms) + 4× DROP(slide off, 60 ms apart, −10 dB)@150 | — | default |
| `shield_gain_bubble` | SYN | 0.5 s · 2/5 | BUBBLE('뽀옹': 250→650 Hz, 120 ms) + SHIMMER(−10 dB)@60 | — | default |
| `shield_break` | HYB | 0.4 s · 3/6 | BUBBLE(pop 300 Hz) + GLASS(soft shatter '쨍': noise HP 2k 20 ms + 10 tinks 2–5k over 0–200 ms, T60 60 ms) → LP 8k, IR plate 8 % | imp.glass (impactGlass_light_*) +3 st, soft; ifc.glass (glass_00*) | default |
| `pulse_support` | SYN | 0.5 s · 2/5 | YARN(ribbon flutter, 200 ms) + CELESTA C6 D6 F6 (60 ms)@120 → IR plate 10 % | — | default |
| `pulse_heal` | SYN | 0.6 s · 2/5 | HARP arpeggio F4 A4 C5 F5 (70 ms, soft) + HUM(minty: sine F5 + air 2k, −14 dB) → IR plate 14 % | — | default |
| `heal_tick` | SYN | 0.25 s · 3/6 | GLINT('링': GLASS A6 1760 Hz, T60 120 ms, soft hammer) | — | default |
| `echo_summon` | SYN | 0.4 s · 3/6 | SHIMMER('샤랑': GLASS cluster rising A5 C6 F6, 30 ms apart) + SHOPBELL(tiny door bell C7, −10 dB)@120 → IR plate 14 % | — | default |
| `echo_dissolve` | SYN | 0.5 s · 3/6 | SHIMMER(reverse shimmer flowing home: reversed plate of a GLASS cluster, 350 ms) + GLINT(F6)@380 | — | default |
| `echo_hurt` | SYN | 0.15 s · 4/8 | GLASS fizz '치익': noise BP 3k 60 ms + glass micro-tinks (−14 dB) | — | default |
| `token_summon` | SYN | 0.4 s · 2/5 | POP(circle pop) + 2× POP(socks '퐁퐁' 400/480 Hz, 80 ms apart)@60 + SPARKLE(−12 dB) | — | default |
| `echo_refund` | SYN | 0.25 s · 2/5 | SHIMMER(reversed, 80 ms, −14 dB) + GLINT('링' of light: GLASS C7, T60 150 ms)@80 | — | default |
| `lamp_tick` | SYN | 0.25 s · 2/6 | GLASS bell tink (render A5 880 Hz, partials 1/2.32/4.25, T60 0.25 s). Tuned | — | default |
| `lamp_full` | SYN | 1.0 s · 2/5 | HANDBELL('딩': F5 warm, T60 1.0 s) + SPARKLE('샤라' 25 grains, 500 ms)@80 + HUM(lantern glow F4, −14 dB) → IR plate 16 % | — | default |
| `lamp_lit` | SYN | 2.2 s · 2/6 | MATCH(strike, 70 ms) + FLAME('후웅' bloom, 400 ms)@60 + HANDBELL chord F4 + F5 A5 C6 (T60 1.8 s)@150 (= the wave start in real time) + GHARM(F3 C4 A4 swell)@150 + SPARKLE rain (40 grains, 300–1300 ms) → IR plate 22 % | — | default |
| `lamp_wave` | SYN | 0.6 s · 2/5 | WHOOSH(warm wave outward: pink noise BP 500→2k→1.2k, 500 ms, OutQuad envelope like LampWave.Radius) + HUM(F4/C5, −12 dB) → IR plaza 10 % | — | default |

#### 7.5.5 Battle — skills (style fallbacks + per-vfx)

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `skill_charge` | SYN | 0.35 s · 3/6 | SPARKLE gather (15 grains converging: rising pitch + crescendo, 300 ms) + riser (noise BP 1→3k, −12 dB) | — | default |
| `skill_cast` | SYN | 0.4 s · 3/6 | WHOOSH(300→1.6k, 200 ms) + SPARKLE(12 grains)@150 | — | default |
| `skill_big` | SYN | 1.0 s · 2/5 | GONG(soft gong G3 196 Hz, modes 1/1.47/2.09/2.56/3.39, T60 1.4 s, soft mallet) + SHIMMER(long, 800 ms) → IR plate 25 % | — | default |
| `skill_gift` | SYN | 0.3 s · 3/6 | WHOOSH(light, 150 ms) + SPARKLE(6 grains) | — | default |
| `skill_fizzle` | SYN | 0.4 s · 2/5 | deflating hiss (noise HP 1k, LP falling 2k→500 Hz, 300 ms) + GLINT(sad, −2 st, −14 dB)@280 | — | default |
| `skill_style_projectile` | SYN | 0.5 s · 2/5 | WHOOSH(launch, 120 ms) + SPARKLE trail (8 grains, 200 ms, −8 dB). Launch only: impacts come from hit_skill / skill_*_hit at arrival | — | default |
| `skill_style_beam` | SYN | 0.6 s · 2/5 | HUM('시잉': sustained C6 + G6 tone, attack 30 ms) + noise BP sweep 1→4k (−10 dB), 500 ms → IR plate 12 % | — | default |
| `skill_style_burst` | SYN | 0.5 s · 2/5 | WHOOSH(bloom '후웅', attack 30 ms) + SPARKLE scatter (20 grains)@60 + FELT(110 Hz, −8 dB) | — | default |
| `skill_style_ring` | SYN | 0.6 s · 2/5 | WHOOSH(expanding floor shimmer: BP 400→1.2k, OutQuad) + SPARKLE(low density) → IR plaza 8 % | — | default |
| `skill_style_ground` | SYN | 0.7 s · 2/5 | WAX(goo spread: BP 300–700 Hz, wobble AM 8 Hz, 600 ms) + SPARKLE(−14 dB) | — | default |
| `skill_style_dome` | SYN | 0.6 s · 2/5 | HUM(swell 220→330 Hz) + GLASS(C7 glassy top, −12 dB)@450 | — | default |
| `skill_style_rise` | SYN | 0.7 s · 2/5 | SPARKLE rising (15 grains ascending) + soft noise swell (−14 dB) | — | default |
| `skill_style_overhead` | SYN | 0.4 s · 2/5 | POP + GLINT twinkle | — | default |
| `skill_style_orbit` | SYN | 0.6 s · 2/5 | 3 WHOOSH passes ('wing-wing', AM 5 Hz, BP 1–2k) + SPARKLE | — | default |
| `skill_style_rain` | SYN | 0.8 s · 2/5 | 12× DROP sparkly patter (random 0–700 ms, GLASS-tinted, C6–F7 in the set) → IR plate 10 % | — | default |
| `skill_thread_stitch` | SYN | 0.6 s · 2/4 | ZIP(arc swish, 300 ms) + 8 stitch CLICKs (35 ms apart, −8 dB) + METALPING(needle, −8 dB). Launch only (arrival = skill_thread_stitch_hit @0.38 s) | — | default |
| `skill_patch_heal` | SYN | 0.6 s · 2/4 | FELT(patch press 200 Hz) + HANDBELL(warm A5 → C6)@100 + SPARKLE | — | default |
| `skill_button_shield` | SYN | 0.6 s · 2/4 | CLACK(giant button snap: body 300 Hz + click) + HUM(soft hum 260 Hz, swell 400 ms) | — | default |
| `skill_spin_button` | SYN | 0.7 s · 2/4 | MOTOR(saw-like whirr: AM 30→45 Hz on BP 1.4k noise, 600 ms) + edge CLICKs every 40 ms (−12 dB) | — | default |
| `skill_keyhole_lock` | HYB | 0.6 s · 2/4 | LATCH(heavy lock turn: 3 clicks + deadbolt) + HANDBELL(ring C6, −8 dB)@250 | rpg.metal (metalLatch) −3 st | default |
| `skill_key_beam` | SYN | 0.4 s · 2/4 | ZIP(bright metallic zing, chirp 2→6k, LP 7k) + METALPING(4k, T60 120 ms) | — | default |
| `skill_candle_burst` | SYN | 0.6 s · 2/4 | FLAME(petal bloom, 450 ms) + EMBER(15 crackles) | — | default |
| `skill_wax_drip` | SYN | 0.7 s · 2/4 | 3× WAX(drips @0/150/320) + goo spread (BP 400 Hz wobble, 300 ms)@350 | — | default |
| `skill_firefly` | SYN | 0.8 s · 2/4 | 8 GLINT blinks (F6–D7 in the set, random 0–700 ms, soft) + HUM(glow, −16 dB) | — | default |
| `skill_lantern_beam` | SYN | 0.7 s · 2/4 | GHARM swell + WHOOSH(warm sweep BP 600→2k), 600 ms | — | default |
| `skill_umbrella_shield` | SYN | 0.6 s · 2/4 | SWISH(fabric whoosh, 200 ms) + POP(dome 300 Hz)@180 + 3× DROP | — | default |
| `skill_raindrops` | SYN | 0.9 s · 2/4 | RAIN patter on fabric (40 drops over 800 ms: awning thuds LP 1k + DROP chirps) | — | default |
| `skill_storm_swirl` | SYN | 0.8 s · 2/4 | WIND swirl (BP 500→1.5k, AM 4 Hz) + SPLASH spray (−8 dB)@300 | — | default |
| `skill_lipstick_heart` | SYN | 0.4 s · 2/4 | CORK(mark pop) + GLINT(heart ping A6)@40 | — | default |
| `skill_kiss_burst` | SYN | 0.6 s · 2/4 | 4× CORK pops (60 ms apart, rising) + SPARKLE | — | default |
| `skill_gloss_shine` | SYN | 0.5 s · 2/4 | SHIMMER(smooth glint: sine sweep C6→C7, 300 ms + sparkle) | — | default |
| `skill_music_notes` | SYN | 0.7 s · 2/4 | MBOX run C6 D6 F6 G6 A6 (60 ms apart) → IR box 8 % | — | default |
| `skill_sound_ring` | SYN | 0.7 s · 2/4 | HUM(resonant F4 + C5 fifth, AM 5 Hz) + GLASS sparkle ring edge → IR plate 14 % | — | default |
| `skill_waltz_ribbon` | SYN | 0.8 s · 2/4 | YARN(ribbon swirl: 3 swishes in 3/4 @0/250/500) + CELESTA(F6)@600 | — | default |
| `skill_star_burst` | SYN | 0.5 s · 2/4 | POP(bright) + SPARKLE(gold stars, 25 grains) + GLINT(C7) | — | default |
| `skill_moon_glow` | SYN | 0.8 s · 2/4 | GHARM(calm glassy F5 + A5, attack 300 ms) + SPARKLE(low density) | — | default |
| `skill_heart_heal` | SYN | 0.6 s · 2/4 | HANDBELL(warm A5) + SPARKLE + HUM(−16 dB) | — | default |
| `skill_buff_crown` | SYN | 0.5 s · 2/4 | CELESTA C6 F6 A6 rising + GLINT | — | default |
| `skill_debuff_mark` | SYN | 0.5 s · 2/4 | descending wobbly tone (sine 700→420 Hz, vibrato 7 Hz ±30 c, LP 2k) + soft POP | — | default |
| `skill_stun_stars` | SYN | 0.6 s · 2/4 | 6 GLASS tinks alternating C7/A6 at 70 ms + 6 Hz tremolo | — | default |
| `skill_slow_yarn` | SYN | 0.6 s · 2/4 | YARN(stretchy elastic: sine 180→120 Hz with rubbery wobble + fabric creak) | — | default |
| `skill_summon_circle` | SYN | 0.7 s · 2/4 | SHIMMER(rising, 500 ms) + CLICK(chalk circle closes, 1.4k)@550 | — | default |
| `skill_shield_bubble` | SYN | 0.6 s · 2/4 | BUBBLE(bloop) + SHIMMER | — | default |
| `skill_cleanse_sparkle` | SYN | 0.6 s · 2/4 | SPARKLE wash (40 grains, HP 2k, broad) + WHOOSH(soft, fresh) | — | default |
| `skill_dust_poof` | SYN | 0.4 s · 2/4 | DUST(dry poof) | — | default |
| `skill_shadow_burst` | SYN | 0.6 s · 2/4 | SHADOW(muffled dark whoosh) + FELT(80 Hz thump) | — | default |
| `skill_split_yarn` | SYN | 0.4 s · 2/4 | POP(woolly) + YARN(snap) | — | default |
| `skill_reveal_light` | SYN | 0.7 s · 2/4 | SHIMMER(reverse → forward expanding brightness) + GLASS cluster | — | default |
| `skill_thread_stitch_hit` | SYN | 0.3 s · 2/4 | METALPING(tick) + FELT(pop) + YARN(thread tighten, 80 ms) | — | default |
| `skill_key_beam_hit` | HYB | 0.3 s · 2/4 | LATCH(click) + METALPING(ring) | rpg.metal (metalClick) +3 st | default |
| `skill_lantern_beam_hit` | SYN | 0.3 s · 2/4 | FLAME(small flare) + GLINT | — | default |

#### 7.5.6 Battle — status effects

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `status_burn` | SYN | 0.4 s · 2/5 | FLAME(small catch '화르륵', 250 ms) + EMBER(crackle) | — | default |
| `status_slow` | SYN | 0.4 s · 2/5 | YARN(sticky web '쭈욱': rubbery stretch sine 220→160 Hz + fabric) | — | default |
| `status_stun` | HYB | 0.5 s · 2/5 | LATCH(lock click) + 3 GLASS tinks circling (stun stars) | rpg.metal (metalClick) | default |
| `status_mark` | SYN | 0.4 s · 2/5 | CORK(mark pop '쪽') + GLINT(heart ping) | — | default |
| `status_knockback` | SYN | 0.3 s · 2/5 | DUST(shove '퍽': FELT 120 Hz + dust 60 ms) | — | default |
| `status_pull` | SYN | 0.5 s · 2/5 | WHOOSH(inward swirl '휘이익': reversed, BP 1.5k→600 Hz, 400 ms) | — | default |
| `status_buff` | SYN | 0.5 s · 2/5 | CELESTA(crown twinkle '띠링' C6 → F6) | — | default |
| `status_debuff` | SYN | 0.4 s · 2/5 | TOYHORN(soft sour descending 400→330 Hz, LP 1.5k, −6 dB). Cute, never mocking | — | default |
| `status_cleanse` | SYN | 0.5 s · 2/5 | SPARKLE wash '샤라랑' | — | default |
| `status_resonance` | SYN | 0.3 s · 2/5 | HUM(tuning-bell A5 hum) + CHIME | — | default |
| `status_lamp_gain` | SYN | 0.3 s · 2/5 | GLINT(firefly glow '또롱': GLASS F6 → A6) | — | default |
| `status_chain` | SYN | 0.15 s · 2/6 | ZIP(short spark chirp) + GLASS tink '찡' (render F6). Tuned | — | default |

#### 7.5.7 Battle — flow, combo, rewards, stage modifiers

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `battle_stage_start` | HYB | 1.0 s · 2/5 | SHOPBELL('딸랑': A5 + C6 + clapper, 3 re-strikes) + WHOOSH(night air 400 ms, LP 1.5k, −10 dB)@100 → IR plaza 14 % | rpg.creak (doorOpen_*) at −12 dB as the door | default |
| `battle_wave` | SYN | 0.6 s · 2/5 | HANDBELL(small distant '댕' C5, LP 3k) → IR plaza 30 % | — | default |
| `battle_synergy` | SYN | 0.9 s · 2/5 | 3 linked chimes '링-링-링': HANDBELL F5, A5, C6 @0/150/300 + ZIP thread link between (−14 dB) | — | default |
| `battle_step_back` | SYN | 0.8 s · 1/4 | MARIMBA C5 → A4 (200 ms apart, soft) | — | default |
| `kill_chain` | SYN | 0.3 s · 2/8 | GLASS chime (T60 0.3 s). Tuned multi-root: render roots F5 and D6 (2 takes each) | — | default |
| `kill_multi` | SYN | 0.7 s · 2/5 | HARP gliss up F4→F6 (10 notes of the set, 30 ms apart) '또로롱' + GLINT at the top | — | default |
| `coin_pop` | SYN | 0.25 s · 3/6 | POP(button pops out '톡' 500 Hz) + GLINT(F7, −10 dB)@20 | — | default |
| `twinkle` | SYN | 0.4 s · 3/6 | SPARKLE(cream shower: 12 grains descending C8→C6, 350 ms) | — | default |
| `mod_rain` | SYN | 1.5 s · 1/4 | THUNDER(distant soft roll 1.2 s, LP 300 Hz) + RAIN onset (density 0→150 drops/s over 1.2 s, −6 dB) → IR plaza 15 % | — | default |
| `mod_wind` | SYN | 1.5 s · 1/4 | WIND gust sweep (1.2 s, BP 300→900→400 Hz) + CHIME(5 notes of the set, random, −8 dB)@500 | — | default |
| `mod_blackout` | SYN | 1.5 s · 1/4 | HUM power-down (sine 220→80 Hz over 600 ms, LP closing) + CLICK(switch 1.2k)@650 + hush (soft air −20 dB, then silence) | — | default |
| `mod_fullmoon` | SYN | 1.5 s · 1/4 | GHARM(glassy moonlight swell F5 A5 C6 D6, attack 0.8 s) + CHIME(C7)@900 | — | default |
| `mod_dust` | SYN | 1.5 s · 1/4 | SAND(dry sandy whoosh, LP 5k) + PAPER debris skitter (8 grains) | — | default |
| `mod_festival` | SYN | 1.5 s · 1/4 | 2× POPPER(@0/350) + TOYHORN(tiny party horn 520→600 Hz, 300 ms, LP 2.5k)@500 + SPARKLE | — | default |

#### 7.5.8 Ambience loops (Amb bus, stereo)

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `amb_night` | SYN | 40 s · 1/3 | WIND bed (gentle breeze, LP 800 Hz, −34 dBFS RMS) + CRICKET(2–3 far insects, −40) + CREAK(sign creak every 8–15 s, −36) + CHIME(1 phrase / 20 s, −38) + city hush (brown noise LP 200 Hz, −42) → IR plaza 30 %, stereo decorrelated | — | default |
| `amb_rain` | SYN | 40 s · 1/3 | RAIN bed (400 drops/s, pink bed HP 200 LP 6k) + awning patter (fabric thuds LP 1k) + gutter (DROP every 0.8–2 s + trickle) → stereo | — | default |
| `amb_wind` | SYN | 40 s · 1/3 | WIND bed with gusts (smoothed random walk, 0.05–0.2 Hz) + lantern rustle (PAPER grains) + loose shutter tap (WOODTICK every 4–9 s) → stereo | — | default |
| `amb_dust` | SYN | 40 s · 1/3 | WIND bed (dry, BP 400–1.2k) + SAND grains (HP 3k, LP 6k, −36) + PAPER debris skitter (every 2–5 s) → stereo, hazy LP 7k | — | default |
| `amb_blackout` | SYN | 40 s · 1/3 | near silence: soft hum 55 Hz + 110 Hz (−50 dBFS) + far MOTH flutter every 6–10 s (−44) + air (−52) → −30 LUFS | — | default |
| `amb_fullmoon` | SYN | 40 s · 1/3 | GHARM high pad (slow random notes of the set, C6–D7, −40) + soft CRICKET (−44) + still air | — | default |
| `amb_festival` | SYN | 40 s · 1/3 | FIREWORK(far, every 4–8 s) + lantern hum (−40) + tiny HANDBELLs (random, −36) + soft paper flutter. No crowd voices → −24 LUFS | — | default |
| `amb_sleep` | SYN | 40 s · 1/3 | very quiet CRICKET (−46) + slow breeze (WIND LP 500 Hz, −40) → −30 LUFS | — | default |
| `amb_dungeon` | SYN | 40 s · 1/3 | slow drips (DROP through IR storeroom, every 2–5 s) + far CREAK (every 6–12 s) + cool air (brown noise LP 400 Hz, −40) → IR storeroom | — | default |
| `amb_boss` | SYN | 20 s · 1/3 | DRONE(110 + 165 Hz with 0.7 Hz beating) + noise LP 500 Hz swelling at 0.5 Hz ('slow pulse') + faint paper wind → 20 s loop, −28 LUFS | — | default |

#### 7.5.9 Ambience emitters (random one-shots on the Amb emitter voice)

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `amb_emit_night` | SYN | 3.0 s · 6/12 | 6 takes, one detail each: far clock-tower bell (HANDBELL F3, LP 1.5k, IR plaza 60 %) · sign creak (CREAK) · moth on a lamp (MOTH + glass tick) · cat flap clack (WOODTICK pair) · far wind chime (CHIME) · shutter tap | — | default |
| `amb_emit_rain` | SYN | 3.0 s · 4/8 | 4 takes: drip into a tin bucket (DROP + TIN) · gutter gurgle (BUBBLE chain) · far thunder (THUNDER) · awning shake (fabric + drops) | — | default |
| `amb_emit_wind` | SYN | 3.0 s · 4/8 | 4 takes: one wind-chime phrase each (CHIME, 4–7 notes of the set, different phrases) | — | default |
| `amb_emit_festival` | SYN | 3.0 s · 4/8 | 4 takes: one far FIREWORK pop + crackle (different sizes) | — | default |

#### 7.5.10 Music loops (Bgm bus, stereo)

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `bgm_battle_night` | MUS | 83.48 s · 3/12 | 3 pieces: §7.6.1 'Lantern Plaza' (take 01), §7.6.2 'Thread & Needle Waltz' (02), §7.6.3 'Midnight Mending March' (03) | — | 4 render candidates per piece (mix/humanise seeds) |
| `bgm_boss` | MUS | 49.66 s · 1/4 | §7.6.4 'Shadow at the Door' | — | default |
| `bgm_dungeon` | MUS | 76.8 s · 1/4 | §7.6.5 'Storeroom of Forgotten Things' | — | default |
| `bgm_gacha` | MUS | 45.71 s · 1/4 | §7.6.6 'Wishing for Lost Friends' (+ bgm_gacha_intro) | — | default |
| `bgm_gacha_intro` | MUS | 5.71 s · 1/4 | §7.6.6 intro score (2 bars, one-shot; the loop is PlayScheduled at its exact end) | — | default |
| `bgm_story` | MUS | 80.0 s · 1/4 | §7.6.7 'Once, in a Night Alley' | — | default |
| `bgm_event` | MUS | 73.85 s · 1/4 | §7.6.8 'Starry Night Walk' | — | default |
| `bgm_sleep` | MUS | 96.0 s · 1/3 | §7.6.9 'The Shop Is Sleeping' | — | default |

#### 7.5.11 Jingles / stingers (Stinger bus, stereo)

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `jingle_stage_clear` | MUS | 2.2 s · 1/4 | §7.7 score J-stage_clear | — | default |
| `jingle_stage_fail` | MUS | 2.4 s · 1/4 | §7.7 score J-stage_fail | — | default |
| `jingle_boss_appear` | MUS | 2.0 s · 1/4 | §7.7 score J-boss_appear | — | default |
| `jingle_boss_defeated` | MUS | 2.6 s · 1/4 | §7.7 score J-boss_defeated | — | default |
| `jingle_chapter_clear` | MUS | 4.0 s · 1/4 | §7.7 score J-chapter_clear | — | default |
| `jingle_new_feature` | MUS | 2.2 s · 1/4 | §7.7 score J-new_feature | — | default |
| `jingle_evolution` | MUS | 3.0 s · 1/4 | §7.7 score J-evolution | — | default |
| `jingle_new_friend` | MUS | 2.0 s · 1/4 | §7.7 score J-new_friend | — | default |
| `jingle_gacha_legend` | MUS | 2.6 s · 1/4 | §7.7 score J-gacha_legend | — | default |
| `jingle_gacha_mythic` | MUS | 3.5 s · 1/4 | §7.7 score J-gacha_mythic | — | default |
| `jingle_first_meeting` | MUS | 2.0 s · 1/4 | §7.7 score J-first_meeting | — | default |
| `jingle_graduation` | MUS | 3.0 s · 1/4 | §7.7 score J-graduation | — | default |
| `jingle_dungeon_clear` | MUS | 2.4 s · 1/4 | §7.7 score J-dungeon_clear | — | default |
| `jingle_event_open` | MUS | 2.0 s · 1/4 | §7.7 score J-event_open | — | default |

#### 7.5.12 Voice barks (optional; Ui bus; silent until `GameSettings.VoiceBarks` exists and is on)

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `vox_jjakjjak_hello` | VOX | 0.9 s · 2/6 | '짝꿍 찾았다!' · voice: bright, bouncy child-like, a little lonely · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_jjakjjak_cheer` | VOX | 0.9 s · 2/6 | '같이 가자!' · voice: bright, bouncy child-like, a little lonely · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_jjakjjak_pet` | VOX | 0.9 s · 2/6 | '헤헤, 간지러워' · voice: bright, bouncy child-like, a little lonely · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_ttokdan_hello` | VOX | 0.9 s · 2/6 | '똑딱!' · voice: small, crisp, earnest · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_ttokdan_cheer` | VOX | 0.9 s · 2/6 | '단단하게!' · voice: small, crisp, earnest · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_ttokdan_pet` | VOX | 0.9 s · 2/6 | '데구르르~' · voice: small, crisp, earnest · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_chalkak_hello` | VOX | 0.9 s · 2/6 | '찰칵!' · voice: cool, clipped, secretly kind · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_chalkak_cheer` | VOX | 0.9 s · 2/6 | '열어 줄게!' · voice: cool, clipped, secretly kind · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_chalkak_pet` | VOX | 0.9 s · 2/6 | '철컥…?' · voice: cool, clipped, secretly kind · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_mongdang_hello` | VOX | 0.9 s · 2/6 | '포옥…' · voice: tiny, warm, a little timid · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_mongdang_cheer` | VOX | 0.9 s · 2/6 | '따뜻하게!' · voice: tiny, warm, a little timid · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_mongdang_pet` | VOX | 0.9 s · 2/6 | '바람은 싫어…' · voice: tiny, warm, a little timid · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_udukeoni_hello` | VOX | 0.9 s · 2/6 | '기다렸어.' · voice: calm, slow, patient · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_udukeoni_cheer` | VOX | 0.9 s · 2/6 | '비 온다!' · voice: calm, slow, patient · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_udukeoni_pet` | VOX | 0.9 s · 2/6 | '…톡.' · voice: calm, slow, patient · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_rouge_hello` | VOX | 0.9 s · 2/6 | '쪽!' · voice: playful, confident, sparkly · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_rouge_cheer` | VOX | 0.9 s · 2/6 | '반짝반짝!' · voice: playful, confident, sparkly · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_rouge_pet` | VOX | 0.9 s · 2/6 | '어머~' · voice: playful, confident, sparkly · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_lalabell_hello` | VOX | 0.9 s · 2/6 | '라라~' · voice: soft, sing-song, shy · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_lalabell_cheer` | VOX | 0.9 s · 2/6 | '들어 줘!' · voice: soft, sing-song, shy · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |
| `vox_lalabell_pet` | VOX | 0.9 s · 2/6 | '랄…라?' · voice: soft, sing-song, shy · ≤ 1.0 s · then §7.9 chain (trim, HP 150, LP 9k, de-ess, IR box 8 %) | — | 3 reads (pace/pitch) |

#### 7.5.13 Reserved for RESEARCH2 features (P3 — produce only when the feature is built)

| Key | Route | Len · Var/Gen | Recipe (layers @ms → chain) | CC0 start (optional layer) | Takes vary by |
|---|---|---|---|---|---|
| `bgm_tower` | MUS | 71.11 s · 1/4 | compose when the feature exists (G minor 108 BPM climbing ostinato, §7.6 rules) | — | default |
| `bgm_worldboss` | MUS | 60.95 s · 1/4 | compose when the feature exists (D minor 126 BPM toy orchestra, §7.6 rules) | — | default |
| `jingle_tower_floor` | MUS | 2.0 s · 1/4 | compose when the feature exists (pizz + glock climb → bell) | — | default |
| `ui_equip` | SYN | 0.4 s · 2/4 | CLICK(snap) + METALPING(brass) + SPARKLE | — | default |
| `ui_relic_up` | SYN | 0.8 s · 2/4 | SHIMMER + CELESTA(C6) | — | default |
| `ui_set_complete` | SYN | 1.2 s · 1/4 | 3× CLICK + HANDBELL chord + SPARKLE | — | default |
| `ui_decor_place` | SYN | 0.3 s · 2/4 | WOODTICK(knock) + GLINT | — | default |

### 7.6 Music: original compositions (MUS)
#### 7.6.0 LLN v1 notation, sequencer and loop rules
Every score lives in `scratchpad/audio/scores.json`, generated and validated from `scores_data.py` by `scores_build.py`. The validation reports 0 errors and 0 warnings for all 24 scores. Producers render from the JSON. The tables below show the same data for reading.

**Score fields:** `id` (library file stem), `key`, `tempo` (BPM), `meter` (beats per bar; for a jingle, its total beats), `bars`, `loop`, `length` (jingles), `chords` (one cell per bar), `form`, `mood`, `reverb` (IR kind + T60), `master`. `parts[]` holds `{name, inst (§7.4), vol dB, pan −1…+1, send % to the score reverb, art, cresc, seg[]}`.

**Note tokens** (inside `notes` segments; bars are separated by `|`, and each bar must sum exactly to the meter):

| Token | Meaning |
|---|---|
| `C5:1` | pitch : duration in beats. Pitch = letter + optional `#`/`b` + octave; C4 = MIDI 60 = 261.63 Hz, A4 = 440 Hz, equal temperament. Durations: integer, decimal or fraction `a/b` |
| `F4+A4+C5:2` | chord: simultaneous notes. Harp rolls up over 12 ms, music box over 8 ms, everything else is exact |
| `r:1` | rest |
| flags `^` `_` `<` `~` | `^` accent (velocity 1.0), `_` soft (0.55), default 0.8; `<` crescendo through the note (0.4 → 1.0); `~` tie into the next note of the same pitch |
| `GL(F4>F6):1` | glissando through every note of the piece's pentatonic set between the two pitches (inclusive), evenly spaced over the duration; the last note rings. F, Bb and C pieces use their major pentatonic; D-minor and Dorian pieces use the F-major pentatonic (the same notes) |
| `ROLL(D2):2`, `ROLL<(A2):2` | roll: timpani 16 strokes/s, snare and sleigh 24 strokes/s. Velocity 0.5, or a crescendo 0.25 → 0.9 with `<`. `x` as the pitch = unpitched |
| `x` `X` `o` as note heads | unpitched hit at velocity 0.65 / 1.0 / 0.35 |

**Patterns** (`pat` segments, with a register centre R): tokens `deg(+deg)*:dur flags` are evaluated against the chord sounding at each token's onset.
- **Chord grammar:** root `[A-G](#|b)?`, then a quality from `"" m 7 maj7 m7 m6 6 sus4 7sus4 m7b5 dim add9`, then an optional `/bass`. `N.C.` means no chord, and the pattern stays silent. Two symbols in one cell split the bar evenly.
- **Degrees:**

| Degree | Pitch |
|---|---|
| `1` | root |
| `2` | +2 st |
| `3` | the chord's third (sus4 chords: the fourth) |
| `4` | +5 st |
| `5` | the chord's fifth |
| `6` | +9 st |
| `7` | the chord's seventh; if the chord has none, the octave |
| `8` | octave |
| `9` | +14 st |
| `10` | third + 12 |
| `12` | fifth + 12 |
| `15` | root + 24 |
| `−3`, `−5` | third or fifth an octave down |
| `b` | slash bass (or root), at or below the root |
| `a` | approach tone: the next bar's beat-1 bass − 1 st if that note is in the bar's `walk_scales` set, else − 2 st. "Chromatic approach" scales always take − 1 st |

- **Register:** the chord root is placed in [R − 6, R + 5] semitones, in the highest octave that fits.

**Grids** (`grid` segments): one character per sixteenth (16 per 4/4 bar, 12 per 3/4 bar). `.` is a rest; `o` 0.35, `x` 0.65 and `X` 1.0 are hit velocities. `|` separates the bars of a multi-bar cycle.

**Segments:** text with fewer bars than its range **cycles**. `copy` duplicates another part's notes for the same bars, transposed.

**Sequencer rules (`seq.py`):**
1. **Exact length.** The loop length is `N = round(bars × meter × 60 / BPM × 44100)` samples (the §7.6 table). One beat is `N / (bars × meter)` samples, a float. Notes start at fractional sample positions: oscillators take a sub-sample phase offset, and KS and modal voices are rendered at 48 kHz and placed by fractional delay.
2. **Humanise.** Onset jitter is N(0, 4 ms), clipped to ±8 ms. Bar downbeats and every grid hit that falls on a beat get ±2–3 ms only. Velocity jitter is ±6 %. The seed is per candidate (§7.5.0).
3. **Velocity.** Gain = `20·log10(velocity)` dB. Hammer hardness / KS brightness = `0.3 + 0.6·velocity`, so louder is brighter, like real instruments.
4. **Note lengths.**
   - Plucked, struck and bell instruments ring naturally. A notated length only sets when dampers may act: PIZZ 120 ms after the note, BASSPIZZ 150 ms; MBOX, HARP, CELESTA, GLOCK, KALIMBA and bells are never damped.
   - Sustained instruments (PAD, ORGAN, GHARM, REED, FLUTE, HORN, CELLO) hold the notated length plus their release.
   - PAD, ORGAN and GHARM tie identical consecutive pitches, so there is no re-attack.
5. **Mix.**
   - Constant-power pan, per-part `vol`, and sends to one shared reverb per score (§7.3.2 IR given by `reverb`).
   - The `master` chain, then the §7.3.4 loudness and true-peak steps. Targets: battle tracks −18 LUFS-I, menu tracks −16, sleep −22, jingles −13 S-max.
6. **SFX pocket.** For battle tracks (`bgm_battle_night_*`, `bgm_boss`, `bgm_dungeon`), long-term band energy in 2–5 kHz must sit ≥ 3 dB below the 1–2 kHz band. If not, raise the master dip, or re-voice the brightest part an octave down.
7. **Seamless loops** by the 3-pass method (§7.3.5). Library `MusicInfo` gets `bpm`, `beatsPerBar`, `beatOffset 0`, `bars`, `transposeSemitones 0` and the measured `loudnessLufs`.
8. **The lore check is part of validation.** Outside `jingle_gacha_mythic`, no music-box part ever completes the lullaby: C-A-F-G-A-G is never followed by its tonic, and the same holds transposed. The SFX `ui_gacha_reveal_mythic` and `atk_musicbox_finale` are the only other places the last note sounds.

**Music render list:**

| Key · take | Title | Key / tempo / meter / bars | Loop s (samples @44.1 k) | Ensemble | Score |
|---|---|---|---|---|---|
| `bgm_battle_night_01` | Lantern Plaza (등불 광장) | F major · 92 BPM · 4/4 · 32 | 83.478 (3681391) | music box, celesta, violin pizz, bass pizz, harp, felt piano, muted string pad, woodblock | §7.6.1 |
| `bgm_battle_night_02` | Thread & Needle Waltz (실과 바늘 왈츠) | F major · 132 BPM · 3/4 · 48 | 65.455 (2886545) | music box, soft reed, bass pizz, pizz/col legno chords, harp, celesta, triangle, pad | §7.6.2 |
| `bgm_battle_night_03` | Midnight Mending March (한밤의 수선 행진) | D minor -> F major · 100 BPM · 4/4 · 32 | 76.800 (3386880) | glockenspiel + music box, walking bass pizz, toy snare (brushes), felt bass drum, harp, string pad | §7.6.3 |
| `bgm_boss_01` | Shadow at the Door (문 앞의 그림자) | D minor · 116 BPM · 4/4 · 24 | 49.655 (2189793) | pizz ostinato, col legno, staccato low strings, felt timpani, felt bass drum, music box, celesta stabs, tremolo strings | §7.6.4 |
| `bgm_dungeon_01` | Storeroom of Forgotten Things (잊힌 것들의 창고) | D Dorian · 100 BPM · 4/4 · 32 | 76.800 (3386880) | kalimba, music box (long reverb), glass harmonica pad, felt toms, bass pizz, wooden flute, high pizz drips | §7.6.5 |
| `bgm_gacha_01` | Wishing for Lost Friends (잃어버린 친구를 바라며) | Bb major · 84 BPM · 4/4 · 16 | 45.714 (2016000) | music box, celesta arpeggios, harp glissandi, glockenspiel, swelling strings, bass pizz (+ 2-bar intro clip) | §7.6.6 |
| `bgm_story_01` | Once, in a Night Alley (어느 밤 골목에서) | F major · 72 BPM · 4/4 · 24 | 80.000 (3528000) | solo music box, felt piano, soft strings, harp, celesta | §7.6.7 |
| `bgm_event_01` | Starry Night Walk (별이 머문 밤 산책) | C major · 104 BPM · 4/4 · 32 | 73.846 (3256615) | glockenspiel, wooden flute, walking bass pizz, sleigh bells, soft claps, celesta, reed organ | §7.6.8 |
| `bgm_sleep_01` | The Shop Is Sleeping (잠든 수선점) | F major · 60 BPM · 3/4 · 32 | 96.000 (4233600) | sparse music box, warm pad, harp | §7.6.9 |

#### 7.6.1 Lantern Plaza (등불 광장)

**`bgm_battle_night_01`** — Lantern Plaza (등불 광장)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 92 BPM · 4/4 | 32 | 83.478 s = 3681391 samples @ 44.1 kHz | seamless | hall 1.4 s | HP 40 Hz · dip −3 dB @ 3 kHz Q 1 (SFX pocket) · shelf −3 dB above 7 kHz · glue 2:1 · −18 LUFS-I |

- **Form:** A 1–8 · A' 9–16 · B 17–24 (celesta leads) · A'' 25–32; bar 32 (C7) turns back to bar 1
- **Mood:** cozy night plaza, lullaby with a gentle eighth-note pulse; the lullaby motif always stops one note short
- **Chords:**

  | Bars | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | 1–8 | F | Dm | Bb | C | F | Am | Bb C | F |
  | 9–16 | F | Dm | Gm | C | Am | Dm | Gm7 C7 | F |
  | 17–24 | Bb | C | Am | Dm | Gm | C | F/A Bb | Csus4 C |
  | 25–32 | F | Dm | Bb | C | Dm | Bb | Gm7 | C7 |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBOX | MBOX | +0 · +0.00 · 18 | 1-16 | `C5:1 A4:1 F4:1 G4:1 \| A4:2 G4:1 r:1 \| D5:1 C5:1 Bb4:1 A4:1 \| G4:3 r:1 \| C5:1 A4:1 F4:1 G4:1 \| A4:1 C5:1 E5:1 D5:0.5 C5:0.5 \| D5:1 Bb4:1 C5:1 E4:1 \| F4:3 r:1 \| C5:1 A4:1 F4:1 G4:1 \| A4:2 G4:1 r:1 \| Bb4:1 D5:1 G5:1.5 F5:0.5 \| E5:3 r:1 \| E5:1 C5:1 A4:1 C5:1 \| D5:1.5 C5:0.5 A4:1 F4:1 \| G4:1 Bb4:1 C5:1 E4:1 \| F4:2 r:2` |
| MBOX | MBOX | +0 · +0.00 · 18 | 17-24 | `r:4` |
| MBOX | MBOX | +0 · +0.00 · 18 | 25-32 | `C5:1 A4:1 F4:1 G4:1 \| A4:2 G4:1 r:1 \| D5:1 C5:1 Bb4:1 A4:1 \| G4:1.5 A4:0.5 Bb4:1 G4:1 \| A4:1 F4:1 D4:1 F4:1 \| Bb4:1 A4:1 G4:1 F4:1 \| G4:2 Bb4:2 \| C5:2 Bb4:1 G4:1` |
| CELESTA | CELESTA | -6 · +0.15 · 22 | 1-8 | `r:4` |
| CELESTA | CELESTA | -6 · +0.15 · 22 | 9-16 | `A5:2_ C6:2_ \| F5:2_ A5:2_ \| D6:2_ Bb5:2_ \| G5:2_ C6:2_ \| E5:2_ A5:2_ \| F5:2_ A5:2_ \| Bb5:2_ G5:2_ \| A5:4_` |
| CELESTA | CELESTA | -6 · +0.15 · 22 | 17-24 | `F5:0.5 D5:0.5 Bb4:0.5 D5:0.5 F5:1 D5:1 \| E5:0.5 C5:0.5 G4:0.5 C5:0.5 E5:1 C5:1 \| E5:0.5 C5:0.5 A4:0.5 C5:0.5 E5:1 A5:1 \| F5:2 D5:2 \| D5:0.5 Bb4:0.5 G4:0.5 Bb4:0.5 D5:1 G5:1 \| E5:1.5 D5:0.5 C5:1 G4:1 \| A4:1 C5:1 D5:1 F5:1 \| G5:2 E5:2` |
| CELESTA | CELESTA | -6 · +0.15 · 22 | 25-32 | `r:4 \| r:4 \| F5:2_ D5:2_ \| E5:2_ G5:2_ \| F5:2_ A5:2_ \| D5:2_ F5:2_ \| Bb5:2_ D6:2_ \| E5:2_ G5:2_` |
| PIZZ | PIZZ | -9 · -0.25 · 12 | 1-32 | pattern @G3: `1:0.5^ 5:0.5 8:0.5 5:0.5 3:0.5 5:0.5 8:0.5 5:0.5` |
| BASS | BASSPIZZ | -6 · +0.00 · 8 | 1-32 | pattern @F2: `b:1^ r:1 5:1 r:1` |
| HARP | HARP | -8 · +0.30 · 25 | 1-15 | pattern @C4: `1:0.25 3:0.25 5:0.25 8:0.25 r:3` |
| HARP | HARP | -8 · +0.30 · 25 | 16-16 | `F3:0.25 A3:0.25 C4:0.25 F4:0.25 r:2 GL(F4>F6):1` |
| HARP | HARP | -8 · +0.30 · 25 | 17-23 | pattern @G3: `1:0.25 5:0.25 8:0.25 10:0.25 12:0.25 10:0.25 8:0.25 5:0.25 1:0.25 5:0.25 8:0.25 10:0.25 12:0.25 10:0.25 8:0.25 5:0.25` |
| HARP | HARP | -8 · +0.30 · 25 | 24-24 | `C4:0.25 G4:0.25 C5:0.25 E5:0.25 r:2 GL(C6>C4):1` |
| HARP | HARP | -8 · +0.30 · 25 | 25-32 | pattern @C4: `1:0.25 3:0.25 5:0.25 8:0.25 r:3` |
| PIANO | FELTPIANO | -12 · -0.10 · 15 | 1-8 | `r:4` |
| PIANO | FELTPIANO | -12 · -0.10 · 15 | 9-32 | pattern @B3: `1+3+5:2_ 1+3+5:2_` |
| PAD | PAD | -15 · +0.00 · 30 | 1-32 | pattern @F3: `1+5+10:2 1+5+10:2` |
| WOOD | WOODBLOCK | -16 · +0.20 · 10 | 1-32 | grid: `....o.......o...` |

#### 7.6.2 Thread & Needle Waltz (실과 바늘 왈츠)

**`bgm_battle_night_02`** — Thread & Needle Waltz (실과 바늘 왈츠)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 132 BPM · 3/4 | 48 | 65.455 s = 2886545 samples @ 44.1 kHz | seamless | hall 1.4 s | as 7.6.1 · −18 LUFS-I |

- **Form:** A 1–16 · B 17–32 (music box quotes the lullaby, the reed answers in the gaps) · A' 33–48 (celesta doubles, reed counter-line)
- **Mood:** lilting oom-pah-pah, playful and tender, chromatic turns (F7, Bbm, D7) for a hint of moonlit mystery
- **Chords:**

  | Bars | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | 1–8 | F | F | C7 | C7 | C7 | C7 | F | F |
  | 9–16 | F | F7 | Bb | Bbm | F/C | D7 | Gm7 | C7 |
  | 17–24 | F | Bbmaj7 | F | C7 | Dm | A7 | Dm | Dm/C |
  | 25–32 | Bb | F/A | Gm | C7 | F | D7 | Gm7 | C7 |
  | 33–40 | F | F | C7 | C7 | C7 | C7 | F | F |
  | 41–48 | F | F7 | Bb | Bbm | F/C | D7 | Gm7 | C7 |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBOX | MBOX | +0 · +0.00 · 18 | 1-16 | `A4:2 C5:1 \| F5:2 E5:1 \| D5:2 C5:1 \| Bb4:3 \| G4:2 Bb4:1 \| E5:2 D5:1 \| C5:2 A4:1 \| F4:3 \| A4:2 C5:1 \| Eb5:2 D5:1 \| D5:2 Bb4:1 \| Db5:3 \| C5:2 A4:1 \| F#4:2 A4:1 \| Bb4:1 A4:1 G4:1 \| E4:2 G4:1` |
| MBOX | MBOX | +0 · +0.00 · 18 | 17-32 | `C5:1 A4:1 F4:1 \| G4:1 A4:1 G4:1 \| r:3 \| r:3 \| D5:1 F5:1 A5:1 \| G5:2 E5:1 \| r:3 \| r:3 \| Bb4:1 D5:1 F5:1 \| A5:2 F5:1 \| r:3 \| r:3 \| C5:1 A4:1 F4:1 \| F#4:1 A4:1 C5:1 \| r:3 \| r:3` |
| MBOX | MBOX | +0 · +0.00 · 18 | 33-48 | `A4:2 C5:1 \| F5:2 E5:1 \| D5:2 C5:1 \| Bb4:3 \| G4:2 Bb4:1 \| E5:2 D5:1 \| C5:2 A4:1 \| F4:3 \| A4:2 C5:1 \| Eb5:2 D5:1 \| D5:2 Bb4:1 \| Db5:3 \| C5:2 A4:1 \| F#4:2 A4:1 \| Bb4:1 A4:1 G4:1 \| E4:2 G4:1` |
| REED | REED | -5 · -0.20 · 20 | 1-16 | `r:3` |
| REED | REED | -5 · -0.20 · 20 | 17-32 | `r:3 \| r:3 \| A4:1 Bb4:1 C5:1 \| D5:2 C5:1 \| r:3 \| r:3 \| F5:2 D5:1 \| C5:3 \| r:3 \| r:3 \| G5:1 F5:1 D5:1 \| E5:2 C5:1 \| r:3 \| r:3 \| Bb4:2 A4:1 \| G4:2 r:1` |
| REED | REED | -5 · -0.20 · 20 | 33-48 | `F4:3_ \| A4:3_ \| Bb4:3_ \| G4:3_ \| E4:3_ \| G4:3_ \| A4:3_ \| C5:3_ \| C5:3_ \| A4:3_ \| F4:3_ \| F4:3_ \| A4:3_ \| C5:3_ \| D5:3_ \| C5:3_` |
| CELESTA | CELESTA | -9 · +0.20 · 22 | 1-32 | `r:3` |
| CELESTA | CELESTA | -9 · +0.20 · 22 | 33-48 | copy of MBOX, transpose +12 st |
| BASS | BASSPIZZ | -6 · +0.00 · 8 | 1-48 | pattern @F2: `b:1^ r:2 \| -5:1 r:2` |
| CHORDS | PIZZ | -11 · -0.30 · 12 | 1-16 | pattern @G3: `r:1 3+5+8:1 3+5+8:1_` |
| CHORDS | PIZZ | -11 · -0.30 · 12 | 17-32 | `r:3` |
| CHORDS | PIZZ | -11 · -0.30 · 12 | 33-48 | pattern @G3: `r:1 3+5+8:1 3+5+8:1_` |
| LEGNO | COLLEGNO | -13 · +0.30 · 10 | 1-16 | `r:3` |
| LEGNO | COLLEGNO | -13 · +0.30 · 10 | 17-32 | pattern @G3: `r:1 5:1 5:1_` |
| LEGNO | COLLEGNO | -13 · +0.30 · 10 | 33-48 | `r:3` |
| HARP | HARP | -8 · +0.30 · 25 | 1-16 | pattern @C3: `1:0.5 5:0.5 8:0.5 10:0.5 12:1 \| r:3` |
| HARP | HARP | -8 · +0.30 · 25 | 17-32 | pattern @C3: `1:0.5 5:0.5 8:0.5 10:0.5 12:1` |
| HARP | HARP | -8 · +0.30 · 25 | 33-48 | pattern @C3: `1:0.5 5:0.5 8:0.5 10:0.5 12:1 \| r:3` |
| TRI | TRIANGLE | -18 · +0.40 · 20 | 1-48 | grid: `o........... \| ............ \| ............ \| ............` |
| PAD | PAD | -14 · +0.00 · 30 | 1-32 | `r:3` |
| PAD | PAD | -14 · +0.00 · 30 | 33-48 | pattern @F3: `1+5+10:3` |

#### 7.6.3 Midnight Mending March (한밤의 수선 행진)

**`bgm_battle_night_03`** — Midnight Mending March (한밤의 수선 행진)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| D minor → F major | 100 BPM · 4/4 | 32 | 76.800 s = 3386880 samples @ 44.1 kHz | seamless | hall 1.2 s | as 7.6.1 · −18 LUFS-I |

- **Form:** D minor 1–16 (bar 16 turns to F) · F major 17–32 (bar 32 A7 turns back to D minor); harp glissandi mark both turns
- **Mood:** gentle toy march for little defenders: determined, cozy, a little mysterious, never aggressive
- **Walking bass scales (`a` = approach tone):** bars 1-16: D E F G A Bb C C#; bars 17-32: F G A Bb C D E C#
- **Chords:**

  | Bars | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | 1–8 | Dm | C | Bb | A7 | Dm | Gm | Bb A7 | Dm |
  | 9–16 | Dm | F | Gm | A | Bb | Gm | Em7b5 A7 | Dm C |
  | 17–24 | F | C/E | Dm | Am | Bb | F/A | Gm7 | C |
  | 25–32 | F | Am | Bb | F | Dm | Bb | Gm7 | A7 |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBOX | MBOX | +0 · +0.00 · 16 | 1-32 | `D5:1 A4:0.75 A4:0.25 D5:1 E5:1 \| F5:1.5 E5:0.5 D5:1 C5:1 \| D5:1 Bb4:0.75 Bb4:0.25 C5:1 D5:1 \| E5:2 C#5:1 A4:1 \| D5:1 A4:0.75 A4:0.25 D5:1 F5:1 \| G5:1.5 F5:0.5 E5:1 D5:1 \| F5:1 D5:1 E5:1 C#5:1 \| D5:3 r:1 \| A5:1 F5:0.75 F5:0.25 D5:1 F5:1 \| A5:1.5 G5:0.5 F5:1 C5:1 \| Bb5:1 G5:1 D5:1 G5:1 \| A5:2 E5:1 C#5:1 \| D5:1 F5:1 Bb5:1 A5:0.5 G5:0.5 \| G5:1 D5:1 Bb4:1 G4:1 \| Bb4:1 G4:1 A4:1 C#5:1 \| D5:2 C5:1 E5:1 \| F5:1 C5:0.75 C5:0.25 F5:1 G5:1 \| A5:1.5 G5:0.5 F5:1 E5:1 \| F5:1 D5:0.75 D5:0.25 E5:1 F5:1 \| E5:2 C5:2 \| D5:1 F5:1 Bb5:1 A5:1 \| A5:1 G5:0.5 F5:0.5 C5:2 \| Bb4:1 D5:1 F5:1 E5:1 \| G5:3 r:1 \| F5:1 C5:0.75 C5:0.25 F5:1 A5:1 \| C6:1.5 B5:0.5 A5:1 E5:1 \| D5:1 F5:1 Bb5:1.5 A5:0.5 \| A5:2 F5:2 \| F5:1 D5:1 A4:1 D5:1 \| F5:1 D5:1 Bb4:1 D5:1 \| G5:1 F5:1 E5:1 D5:1 \| C#5:2 E5:1 A4:1` |
| GLOCK | GLOCK | -8 · +0.15 · 16 | 1-32 | copy of MBOX, transpose +0 st |
| BASS | BASSPIZZ | -5 · +0.00 · 8 | 1-32 | pattern @D2: `b:1^ 5:1 8:1 a:1` |
| AFTER | PIZZ | -12 · -0.30 · 12 | 1-32 | pattern @A3: `r:0.5 3+5:0.5_ r:0.5 3+5:0.5_ r:0.5 3+5:0.5_ r:0.5 3+5:0.5_` |
| SNARE | TOYSNARE | -12 · +0.10 · 10 | 1-7 | grid: `X.ox..o.x.ox..oo` |
| SNARE | TOYSNARE | -12 · +0.10 · 10 | 8-8 | grid: `x.ox..o.x.oxoooo` |
| SNARE | TOYSNARE | -12 · +0.10 · 10 | 9-15 | grid: `X.ox..o.x.ox..oo` |
| SNARE | TOYSNARE | -12 · +0.10 · 10 | 16-16 | grid: `x.ox..o.x.oxoooo` |
| SNARE | TOYSNARE | -12 · +0.10 · 10 | 17-23 | grid: `X.ox..o.x.ox..oo` |
| SNARE | TOYSNARE | -12 · +0.10 · 10 | 24-24 | grid: `x.ox..o.x.oxoooo` |
| SNARE | TOYSNARE | -12 · +0.10 · 10 | 25-31 | grid: `X.ox..o.x.ox..oo` |
| SNARE | TOYSNARE | -12 · +0.10 · 10 | 32-32 | grid: `x.ox..o.x.oxoooo` |
| BD | FELTBD | -10 · +0.00 · 6 | 1-32 | grid: `X.......x.......` |
| HARP | HARP | -8 · +0.30 · 25 | 1-15 | `r:4` |
| HARP | HARP | -8 · +0.30 · 25 | 16-16 | `r:2 GL(C4>C6):2` |
| HARP | HARP | -8 · +0.30 · 25 | 17-31 | `r:4` |
| HARP | HARP | -8 · +0.30 · 25 | 32-32 | `r:2 GL(D6>D4):2` |
| PAD | PAD | -15 · +0.00 · 30 | 1-32 | pattern @D3: `1+5+10:2 1+5+10:2` |

#### 7.6.4 Shadow at the Door (문 앞의 그림자)

**`bgm_boss_01`** — Shadow at the Door (문 앞의 그림자)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| D minor | 116 BPM · 4/4 | 24 | 49.655 s = 2189793 samples @ 44.1 kHz | seamless | hall 1.0 s | as 7.6.1 · −18 LUFS-I · no sub below 45 Hz |

- **Form:** 3 × 8 bars; tremolo strings swell in bars 7–8, 15–16, 23–24; bar 24 (A7) turns back to bar 1
- **Mood:** toy soldiers defending a nursery: insistent, tense, still cute; the lullaby in minor, again missing its last note
- **Chords:**

  | Bars | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | 1–8 | Dm | Dm | Bb | A | Dm | Dm | Gm | A7 |
  | 9–16 | Dm | F | Gm | A | Bb | C | A7sus4 | A7 |
  | 17–24 | Gm | Dm | Bb | A | Gm | Dm/F | Em7b5 | A7 |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBOX | MBOX | +0 · +0.00 · 14 | 1-24 | `A4:1 F4:1 D4:1 E4:1 \| F4:2 E4:1 r:1 \| F4:1 G4:1 A4:1 Bb4:1 \| A4:2 C#5:1 r:1 \| A4:1 F4:1 D4:1 E4:1 \| F4:2 E4:1 r:1 \| Bb4:1 A4:1 G4:1 Bb4:1 \| A4:3 r:1 \| D5:1 A4:1 F4:1 A4:1 \| C5:1 A4:1 F4:1 C5:1 \| D5:1 Bb4:1 G4:1 Bb4:1 \| C#5:2 E5:2 \| F5:1 D5:1 Bb4:1 D5:1 \| E5:1 C5:1 G4:1 C5:1 \| D5:2 D5:1 D5:1 \| C#5:3 r:1 \| G5:1 F5:1 D5:1 Bb4:1 \| A4:1 F4:1 D4:1 E4:1 \| F4:2 E4:1 r:1 \| E4:1 A4:1 C#5:1 E5:1 \| G5:1.5 F5:0.5 E5:1 D5:1 \| F5:2 A4:2 \| G4:1 Bb4:1 D5:1 E5:1 \| C#5:2 A4:1 E4:1` |
| OST | PIZZ | -8 · -0.25 · 10 | 1-24 | pattern @F3: `1:0.25^ 1:0.25 5:0.25 1:0.25 3:0.25 1:0.25 5:0.25 1:0.25 1:0.25^ 1:0.25 5:0.25 1:0.25 2:0.25 3:0.25 2:0.25 1:0.25` |
| LEGNO | COLLEGNO | -14 · +0.30 · 8 | 1-24 | pattern @D4: `r:0.5 1:0.5 r:0.5 1:0.5 r:0.5 1:0.5 r:0.5 1:0.5` |
| LOW | CELLO (art stacc) | -8 · +0.00 · 10 | 1-24 | pattern @A2: `b:0.5^ r:0.5 b:0.5 r:0.5 b:0.5^ r:0.5 b:0.5 r:0.5` |
| TIMP | TIMP | -6 · +0.00 · 14 | 1-7 | `D2:1^ r:1 A2:1 r:1` |
| TIMP | TIMP | -6 · +0.00 · 14 | 8-8 | `D2:1^ r:1 ROLL<(A2):2` |
| TIMP | TIMP | -6 · +0.00 · 14 | 9-15 | `D2:1^ r:1 A2:1 r:1` |
| TIMP | TIMP | -6 · +0.00 · 14 | 16-16 | `D2:1^ r:1 ROLL<(A2):2` |
| TIMP | TIMP | -6 · +0.00 · 14 | 17-23 | `D2:1^ r:1 A2:1 r:1` |
| TIMP | TIMP | -6 · +0.00 · 14 | 24-24 | `D2:1^ r:1 ROLL<(A2):2` |
| BD | FELTBD | -10 · +0.00 · 6 | 1-24 | grid: `X.......x.......` |
| STAB | CELESTA | -12 · +0.25 · 20 | 1-24 | `r:4 \| r:3.5 D6+Eb6:0.5_` |
| TREM | CELLO (art trem, cresc each segment −20 → −10 dB) | -10 · +0.00 · 20 | 1-6 | `r:4` |
| TREM | CELLO (art trem, cresc each segment −20 → −10 dB) | -10 · +0.00 · 20 | 7-8 | pattern @D3: `1+5+8+10:4` |
| TREM | CELLO (art trem, cresc each segment −20 → −10 dB) | -10 · +0.00 · 20 | 9-14 | `r:4` |
| TREM | CELLO (art trem, cresc each segment −20 → −10 dB) | -10 · +0.00 · 20 | 15-16 | pattern @D3: `1+5+8+10:4` |
| TREM | CELLO (art trem, cresc each segment −20 → −10 dB) | -10 · +0.00 · 20 | 17-22 | `r:4` |
| TREM | CELLO (art trem, cresc each segment −20 → −10 dB) | -10 · +0.00 · 20 | 23-24 | pattern @D3: `1+5+8+10:4` |

#### 7.6.5 Storeroom of Forgotten Things (잊힌 것들의 창고)

**`bgm_dungeon_01`** — Storeroom of Forgotten Things (잊힌 것들의 창고)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| D Dorian | 100 BPM · 4/4 | 32 | 76.800 s = 3386880 samples @ 44.1 kHz | seamless | storeroom 2.4 s (dark) | as 7.6.1 · −18 LUFS-I |

- **Form:** A 1–16 (music box over the kalimba) · B 17–24 (wooden flute) · A' 25–32
- **Mood:** curious and a little eerie, a steady pulse for battles in the dark; long soft reverb
- **Chords:**

  | Bars | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | 1–8 | Dm | G | Dm | G | Am | G | C | Dm |
  | 9–16 | Dm | G | Dm | G | F | C | G | Dm |
  | 17–24 | Am | Em | F | G | Am | Em | F | G |
  | 25–32 | Dm | G | Dm | G | F | C | G | G |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| KALIMBA | KALIMBA | -8 · -0.20 · 20 | 1-32 | pattern @F4: `1:0.5 5:0.5 3:0.5 5:0.5 8:0.5 5:0.5 3:0.5 5:0.5` |
| BASS | BASSPIZZ | -6 · +0.00 · 10 | 1-32 | pattern @D2: `1:1^ r:1 1:0.5 r:0.5 5:1` |
| TOM | FELTTOM | -12 · +0.00 · 15 | 1-32 | grid: `X.....x...x.....` |
| GLASSPAD | GHARM | -16 · +0.00 · 40 | 1-32 | pattern @F3: `1+5+10:4` |
| MBOX | MBOX | +0 · +0.10 · 45 | 1-16 | `r:2 A5:1 F5:1 \| D5:2 B4:2 \| r:2 A5:1 C6:1 \| B5:3 r:1 \| r:2 E5:1 G5:1 \| D5:2 B4:1 G4:1 \| E5:2 G5:2 \| F5:4 \| A5:1 F5:1 D5:1 E5:1 \| F5:2 E5:1 r:1 \| r:4 \| r:2 B5:1 D6:1 \| C6:2 A5:2 \| G5:2 E5:2 \| D5:3 B4:1 \| A4:4` |
| MBOX | MBOX | +0 · +0.10 · 45 | 17-24 | `r:4` |
| MBOX | MBOX | +0 · +0.10 · 45 | 25-32 | `r:2 A5:1 F5:1 \| D5:2 B4:2 \| A5:1 F5:1 D5:1 E5:1 \| F5:2 E5:1 r:1 \| C6:2 A5:2 \| G5:2 E5:2 \| D5:4 \| B4:2 r:2` |
| FLUTE | FLUTE | -4 · -0.10 · 30 | 1-16 | `r:4` |
| FLUTE | FLUTE | -4 · -0.10 · 30 | 17-24 | `E5:2 A5:1 G5:1 \| G5:2 E5:2 \| F5:1 A5:1 C6:1.5 B5:0.5 \| A5:2 G5:2 \| E5:1 A5:1 B5:1 C6:1 \| B5:3 G5:1 \| A5:1 F5:1 C5:1 F5:1 \| G5:2 D5:2` |
| FLUTE | FLUTE | -4 · -0.10 · 30 | 25-32 | `r:4` |
| DRIPS | PIZZ | -14 · +0.35 · 50 | 1-32 | `r:4 \| r:4 \| r:2.5 D6:0.5_ r:1 \| r:4 \| r:4 \| r:1.5 A5:0.5_ r:2 \| r:4 \| r:4 \| r:4 \| r:4 \| r:0.5 F6:0.5_ r:3 \| r:4 \| r:4 \| r:2.5 C6:0.5_ r:1 \| r:4 \| r:4 \| r:4 \| r:4 \| r:1.5 D6:0.5_ r:2 \| r:4 \| r:4 \| r:0.5 A5:0.5_ r:3 \| r:4 \| r:4 \| r:4 \| r:4 \| r:2.5 F6:0.5_ r:1 \| r:4 \| r:4 \| r:1.5 C6:0.5_ r:2 \| r:4 \| r:4` |

#### 7.6.6 Wishing for Lost Friends

**`bgm_gacha_intro_01`** — Wishing for Lost Friends — intro (bgm_gacha_intro)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| Bb major | 84 BPM · 4/4 | 2 | 5.714 s | one-shot | plate 1.6 s | −16 LUFS-I (measured together with the loop) |

- **Form:** 2 bars, plays once; the loop (bgm_gacha_01) is PlayScheduled at its exact end
- **Mood:** a wish being made: the lullaby in Bb, still without its last note
- **Chords:**

  | Bars | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | 1–2 | Bb | F7sus4 F7 |  |  |  |  |  |  |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBOX | MBOX | +0 · +0.00 · 25 | 1-2 | `F5:1 D5:1 Bb4:1 C5:1 \| D5:2 C5:1 r:1` |
| HARP | HARP | -6 · +0.30 · 30 | 1-2 | `r:4 \| r:3 GL(F4>F6):1` |
| PAD | PAD | -16 · +0.00 · 35 | 1-2 | pattern @F3: `1+5+10:2 1+5+10:2` |

**`bgm_gacha_01`** — Wishing for Lost Friends (잃어버린 친구를 바라며)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| Bb major | 84 BPM · 4/4 | 16 | 45.714 s = 2016000 samples @ 44.1 kHz | seamless | plate 1.6 s | HP 40 Hz · shelf −2 dB above 8 kHz · −16 LUFS-I |

- **Form:** 4 × 4 bars; strings swell over each 4-bar phrase; bar 16 (F7sus4 F7) turns back to bar 1
- **Mood:** wonder and anticipation, rippling celesta, harp glissandi at every phrase end
- **Chords:**

  | Bars | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | 1–8 | Bb | Gm | Eb | F | Bb | Dm | Eb | F7 |
  | 9–16 | Gm | Dm | Eb | Bb/D | Cm7 | F | Ebmaj7 | F7sus4 F7 |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBOX | MBOX | +0 · +0.00 · 22 | 1-16 | `D5:1.5 C5:0.5 Bb4:1 F5:1 \| G5:2 F5:1 D5:1 \| Eb5:1 G5:1 Bb5:1 G5:1 \| F5:3 r:1 \| D5:1.5 C5:0.5 Bb4:1 F5:1 \| A5:2 F5:1 D5:1 \| G5:1 Bb5:1 C6:1 Bb5:1 \| A5:3 r:1 \| Bb5:1.5 A5:0.5 G5:1 D5:1 \| F5:1.5 E5:0.5 D5:1 A4:1 \| G5:1 F5:1 Eb5:1 G5:1 \| F5:2 D5:2 \| Eb5:1 G5:1 Bb5:1 G5:1 \| A5:2 C6:2 \| Bb5:2 G5:1 D5:1 \| C5:2 A4:2` |
| CELESTA | CELESTA | -12 · +0.20 · 25 | 1-16 | pattern @F4: `1:0.25 3:0.25 5:0.25 8:0.25 10:0.25 8:0.25 5:0.25 3:0.25 1:0.25 3:0.25 5:0.25 8:0.25 10:0.25 8:0.25 5:0.25 3:0.25` |
| HARP | HARP | -7 · +0.30 · 30 | 1-3 | pattern @Bb2: `b:4` |
| HARP | HARP | -7 · +0.30 · 30 | 4-4 | `F2:3 GL(F4>F6):1` |
| HARP | HARP | -7 · +0.30 · 30 | 5-7 | pattern @Bb2: `b:4` |
| HARP | HARP | -7 · +0.30 · 30 | 8-8 | `F2:3 GL(F4>F6):1` |
| HARP | HARP | -7 · +0.30 · 30 | 9-11 | pattern @Bb2: `b:4` |
| HARP | HARP | -7 · +0.30 · 30 | 12-12 | `D3:3 GL(F6>F4):1` |
| HARP | HARP | -7 · +0.30 · 30 | 13-15 | pattern @Bb2: `b:4` |
| HARP | HARP | -7 · +0.30 · 30 | 16-16 | `F2:2 GL(Bb3>Bb5):2` |
| GLOCK | GLOCK | -14 · -0.20 · 25 | 1-16 | `r:4 \| r:4 \| r:4 \| r:2 Bb6:0.5_ F6:0.5_ r:1` |
| STRINGS | PAD (cresc each 4-bar phrase −20 → −9 dB, reset at bars 5, 9, 13) | -9 · +0.00 · 30 | 1-16 | pattern @F3: `1+5+10:2 1+5+10:2` |
| BASS | BASSPIZZ | -9 · +0.00 · 10 | 1-16 | pattern @F2: `b:1^ r:3` |

#### 7.6.7 Once, in a Night Alley (어느 밤 골목에서)

**`bgm_story_01`** — Once, in a Night Alley (어느 밤 골목에서)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 72 BPM · 4/4 | 24 | 80.000 s = 3528000 samples @ 44.1 kHz | seamless | hall 1.8 s | HP 40 Hz · −16 LUFS-I |

- **Form:** 1–8 solo music box states the lullaby and holds its breath (pad from bar 3) · 9–16 felt piano answers · 17–24 all together, ends open on Bbmaj7/C
- **Mood:** nostalgic, tender, a little mysterious: opening a picture book at night
- **Chords:**

  | Bars | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | 1–8 | N.C. | N.C. | F | Dm | Bb | C | F/A | Bbmaj7 |
  | 9–16 | Dm | Am | Bb | F | Gm7 | C7sus4 | Bb | Csus4 C |
  | 17–24 | F | Dm7 | Bbmaj7 | C | Am7 | Dm7 | Gm7 | Bbmaj7/C |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBOX | MBOX | +0 · +0.00 · 22 | 1-8 | `C5:1 A4:1 F4:1 G4:1 \| A4:1 G4:1 r:2 \| C5:1 A4:1 F4:1 G4:1 \| A4:1 G4:1 r:2 \| D5:1 C5:1 Bb4:1 A4:1 \| G4:2 A4:1 Bb4:1 \| A4:1 C5:1 F5:1 E5:1 \| D5:3 r:1` |
| MBOX | MBOX | +0 · +0.00 · 22 | 9-16 | `r:4 \| r:3 E6:0.5_ C6:0.5_ \| r:4 \| r:3 A5:0.5_ F5:0.5_ \| r:4 \| r:3 G5:0.5_ C6:0.5_ \| r:4 \| r:4` |
| MBOX | MBOX | +0 · +0.00 · 22 | 17-24 | `C5:1 A4:1 F4:1 G4:1 \| A4:1 G4:1 r:2 \| D5:1 F5:1 A5:1 G5:1 \| E5:2 C5:2 \| C5:1 A4:1 E4:1 G4:1 \| A4:2 F4:2 \| Bb4:1 A4:1 G4:1 D4:1 \| C5:4` |
| PIANO_RH | FELTPIANO | -3 · -0.10 · 20 | 1-8 | `r:4` |
| PIANO_RH | FELTPIANO | -3 · -0.10 · 20 | 9-16 | `F4:1 A4:1 D5:1.5 C5:0.5 \| C5:2 E4:2 \| F4:1 Bb4:1 D5:1.5 C5:0.5 \| C5:2 A4:2 \| Bb4:1 A4:1 G4:1 F4:1 \| G4:2 C5:2 \| D5:1 C5:1 Bb4:1 A4:1 \| G4:4` |
| PIANO_RH | FELTPIANO | -3 · -0.10 · 20 | 17-24 | `r:4` |
| PIANO_LH | FELTPIANO | -10 · -0.10 · 20 | 1-8 | `r:4` |
| PIANO_LH | FELTPIANO | -10 · -0.10 · 20 | 9-16 | pattern @F3: `b:2 5+8:2_` |
| PIANO_LH | FELTPIANO | -10 · -0.10 · 20 | 17-24 | `r:4` |
| STRINGS | PAD | -14 · +0.00 · 35 | 1-2 | `r:4` |
| STRINGS | PAD | -14 · +0.00 · 35 | 3-24 | pattern @F3: `1+5+10:4` |
| HARP | HARP | -8 · +0.30 · 30 | 1-16 | `r:4` |
| HARP | HARP | -8 · +0.30 · 30 | 17-23 | pattern @F2: `b:0.5 5:0.5 8:0.5 10:0.5 12:2` |
| HARP | HARP | -8 · +0.30 · 30 | 24-24 | `GL(C4>C6):1 r:3` |
| CELESTA | CELESTA | -10 · +0.20 · 28 | 1-16 | `r:4` |
| CELESTA | CELESTA | -10 · +0.20 · 28 | 17-24 | `r:2 A5:2_ \| r:2 F5:2_ \| r:2 C6:2_ \| r:2 G5:2_ \| r:2 E5:2_ \| r:2 D5:2_ \| r:2 Bb5:2_ \| r:4` |

#### 7.6.8 Starry Night Walk (별이 머문 밤 산책)

**`bgm_event_01`** — Starry Night Walk (별이 머문 밤 산책)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| C major | 104 BPM · 4/4 | 32 | 73.846 s = 3256615 samples @ 44.1 kHz | seamless | hall 1.2 s | as 7.6.1 · −16 LUFS-I |

- **Form:** A 1–8 (glockenspiel) · A' 9–16 (wooden flute) · B 17–24 (glock + flute in thirds, sleigh bells enter) · A'' 25–32; bar 32 (G7) turns back
- **Mood:** a playful festival walk under the stars; cozy and soft, never loud
- **Walking bass scales (`a` = approach tone):** bars 1-32: chromatic approach (next bass − 1 st)
- **Chords:**

  | Bars | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | 1–8 | C | Am | F | G | C | Am | Dm7 G7 | C |
  | 9–16 | C | E7 | Am | C7 | F | Fm | C/G G7 | C |
  | 17–24 | F | G | Em | Am | Dm7 | G7 | Cmaj7 | C7 |
  | 25–32 | F | Fm | C/E | A7 | Dm7 | G7 | C | G7 |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| GLOCK | GLOCK | -2 · +0.10 · 18 | 1-8 | `E5:1 G5:1 C6:1 G5:1 \| A5:1.5 G5:0.5 E5:1 C5:1 \| F5:1 A5:1 C6:1 A5:1 \| G5:3 r:1 \| E5:1 G5:1 C6:1 E6:1 \| D6:1.5 C6:0.5 A5:1 E5:1 \| F5:1 A5:1 G5:1 B4:1 \| C5:3 r:1` |
| GLOCK | GLOCK | -2 · +0.10 · 18 | 9-16 | `r:4` |
| GLOCK | GLOCK | -2 · +0.10 · 18 | 17-32 | `A5:1 C6:1 A5:1 F5:1 \| B5:1 D6:1 B5:1 G5:1 \| G5:1 B5:1 E6:1 B5:1 \| C6:2 A5:2 \| F5:1 A5:1 C6:1 A5:1 \| B5:1 D6:1 F6:1 D6:1 \| E6:2 B5:2 \| Bb5:2 G5:2 \| A5:1.5 G5:0.5 F5:1 C5:1 \| Ab5:1.5 G5:0.5 F5:1 C5:1 \| G5:1 E5:1 C5:1 E5:1 \| C#6:2 A5:2 \| D6:1 C6:1 A5:1 F5:1 \| G5:1 F5:1 D5:1 B4:1 \| C5:2 E5:1 G5:1 \| F5:1 D5:1 B4:1 G4:1` |
| FLUTE | FLUTE | -4 · -0.15 · 22 | 1-8 | `r:4` |
| FLUTE | FLUTE | -4 · -0.15 · 22 | 9-16 | `G4:1 C5:1 E5:1.5 D5:0.5 \| B4:2 G#4:2 \| A4:1 C5:1 E5:1 A5:1 \| G5:2 Bb4:2 \| A4:1 C5:1 F5:1.5 E5:0.5 \| Ab4:2 C5:2 \| E5:1 D5:1 G4:1 B4:1 \| C5:3 r:1` |
| FLUTE | FLUTE | -4 · -0.15 · 22 | 17-24 | `F5:1 A5:1 F5:1 C5:1 \| G5:1 B5:1 G5:1 D5:1 \| E5:1 G5:1 B5:1 G5:1 \| A5:2 E5:2 \| D5:1 F5:1 A5:1 F5:1 \| G5:1 B5:1 D6:1 B5:1 \| C6:2 G5:2 \| G5:2 E5:2` |
| FLUTE | FLUTE | -4 · -0.15 · 22 | 25-32 | `r:4` |
| BASS | BASSPIZZ | -5 · +0.00 · 8 | 1-32 | pattern @E2: `b:1^ 3:1 5:1 a:1` |
| SLEIGH | SLEIGH | -20 · +0.30 · 12 | 1-16 | grid: `................` |
| SLEIGH | SLEIGH | -20 · +0.30 · 12 | 17-32 | grid: `o.o.X.o.o.o.X.o.` |
| CLAP | CLAP | -16 · -0.20 · 15 | 1-8 | grid: `................` |
| CLAP | CLAP | -16 · -0.20 · 15 | 9-32 | grid: `....x.......x...` |
| CELESTA | CELESTA | -12 · +0.25 · 25 | 1-32 | `r:4 \| r:4 \| r:4 \| r:2 G6:0.5_ E6:0.5_ C6:1_ \| r:4 \| r:4 \| r:4 \| r:2 E6:0.5_ G6:0.5_ C7:1_ \| r:4 \| r:4 \| r:4 \| r:4 \| r:4 \| r:4 \| r:4 \| r:2 E6:0.5_ G6:0.5_ C7:1_ \| r:4 \| r:4 \| r:4 \| r:4 \| r:4 \| r:4 \| r:4 \| r:2 Bb6:0.5_ G6:0.5_ E6:1_ \| r:4 \| r:4 \| r:4 \| r:4 \| r:4 \| r:4 \| r:4 \| r:2 D6:0.5_ B5:0.5_ G5:1_` |
| ORGAN | ORGAN | -15 · +0.00 · 25 | 1-32 | pattern @C3: `1+5+10:2 1+5+10:2` |

#### 7.6.9 The Shop Is Sleeping (잠든 수선점)

**`bgm_sleep_01`** — The Shop Is Sleeping (잠든 수선점)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 60 BPM · 3/4 | 32 | 96.000 s = 4233600 samples @ 44.1 kHz | seamless | hall 2.2 s | LP 6 kHz gentle · −22 LUFS-I |

- **Form:** 8 phrases of 4 bars: 2 bars of music box, 2 bars of breathing room (pad + one harp note)
- **Mood:** almost silent, calm and safe
- **Chords:**

  | Bars | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | 1–8 | F | F | Bbmaj7 | Bbmaj7 | Dm7 | Dm7 | Csus4 | C |
  | 9–16 | F | F | Gm7 | Gm7 | Bbmaj7 | Bbmaj7 | Csus4 | Csus4 |
  | 17–24 | Dm | Dm | Bb | Bb | F/A | F/A | Gm7 | Gm7 |
  | 25–32 | Bbmaj7 | Bbmaj7 | F/C | F/C | Gm7 | Gm7 | Csus4 | Csus4 |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBOX | MBOX | +0 · +0.00 · 30 | 1-32 | `C5:1 A4:1 F4:1 \| G4:1 A4:1 G4:1 \| r:3 \| r:3 \| D5:2 C5:1 \| A4:3 \| r:3 \| r:3 \| C5:1 A4:1 F4:1 \| G4:2 A4:1 \| Bb4:3 \| r:3 \| D5:1 F5:1 A5:1 \| G5:3 \| r:3 \| r:3 \| F5:2 E5:1 \| D5:3 \| r:3 \| r:3 \| C5:1 A4:1 F4:1 \| G4:1 A4:1 G4:1 \| r:3 \| r:3 \| A4:2 C5:1 \| D5:3 \| r:3 \| r:3 \| Bb4:2 A4:1 \| G4:3 \| r:3 \| r:3` |
| HARP | HARP | -10 · +0.30 · 40 | 1-32 | pattern @F2: `r:3 \| r:3 \| b:3 \| r:3` |
| PAD | PAD | -12 · +0.00 · 40 | 1-32 | pattern @F3: `1+5+10:3` |

### 7.7 Jingles (MUS, one-shot, Stinger bus)
Rendered by the same sequencer. `meter` is the jingle's total beats, and the length includes the tail; every jingle ends with a 150 ms fade.
Every jingle uses only notes of F major, so it never fights the ducked bed. Its listed key is where it comes to rest. The one deliberate exception is the D–Eb–A music-box cluster in `jingle_boss_appear`. The stricter F-pentatonic rule (§1.3) applies to short SFX, because they overlap the music freely.

**`jingle_stage_clear`** — Stage clear

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 120 BPM · 4 beats | 1 | 2.20 s (4 beats at 120 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

- **Note:** SPARKLE layer (20 grains) from beat 1.5 for 1.0 s

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| CEL | CELESTA | +0 · +0.10 · 20 | 1-1 | `C5:0.5 F5:0.5 A5:0.5 F5+A5+C6:2.5` |
| GLK | GLOCK | -8 · -0.10 · 20 | 1-1 | `r:1.5 C6:2.5_` |
| HRP | HARP | -6 · +0.30 · 25 | 1-1 | `GL(F3>F5):1.5 F3+C4+F4:2.5` |
| BAS | BASSPIZZ | -6 · +0.00 · 10 | 1-1 | `r:1.5 F2:2.5^` |
| BEL | HANDBELL | -4 · +0.00 · 22 | 1-1 | `r:1.5 F5:2.5` |

**`jingle_stage_fail`** — Stage failed (music box winding down)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| D minor | 80 BPM · 3 beats | 1 | 2.40 s (3 beats at 80 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

- **Note:** ritardando: beat lengths 0.75 / 0.86 / 1.0 s; music-box pitch droops 0 / −5 / −20 cents; no resolution

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBX | MBOX | +0 · +0.00 · 25 | 1-1 | `A4:1 G4:1 E4:1` |
| PAD | PAD | -14 · +0.00 · 30 | 1-1 | `D3+A3+E4+F4:3` |

**`jingle_boss_appear`** — Boss appears

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| D minor | 60 BPM · 2 beats | 1 | 2.00 s (2 beats at 60 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

- **Note:** SHADOW reversed whoosh layer ending exactly at 1.2 s; CELLO line 1 is tremolo, crescendo −24 → −8 dB

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| TRM | CELLO | -8 · +0.00 · 20 | 1-1 | `D2+A2:1.2< D2:0.8^` |
| TMP | TIMP | -4 · +0.00 · 15 | 1-1 | `ROLL<(D2):1.2 D2:0.8^` |
| MBX | MBOX | -6 · +0.10 · 20 | 1-1 | `r:1.2 D5+Eb5+A5:0.8^` |

**`jingle_boss_defeated`** — Boss defeated

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 112 BPM · 4 beats | 1 | 2.60 s (4 beats at 112 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| HRP | HARP | -6 · +0.30 · 25 | 1-1 | `GL(F4>F6):0.5 r:3.5` |
| GLK | GLOCK | -2 · -0.10 · 20 | 1-1 | `A5:0.5 C6:0.5 F6:3` |
| STR | PAD | -8 · +0.00 · 25 | 1-1 | `Bb3+D4+F4:1 C4+E4+G4:1 F3+A3+C4+F4:2` |
| BEL | HANDBELL | -4 · +0.00 · 22 | 1-1 | `r:2 F5+C6:2` |
| BAS | BASSPIZZ | -6 · +0.00 · 10 | 1-1 | `Bb2:1 C3:1 F2:2^` |

**`jingle_chapter_clear`** — Chapter clear (toy-orchestra fanfare)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 104 BPM · 6 beats | 1 | 4.00 s (6 beats at 104 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| SNR | TOYSNARE | -10 · +0.10 · 10 | 1-1 | `ROLL<(x):1 X:0.5 r:4.5` |
| GLK | GLOCK | -2 · -0.10 · 20 | 1-1 | `r:1 C5:0.5 F5:0.5 A5:0.5 C6:1 Bb5:0.25 A5:0.25 G5:0.5 F5:1.5` |
| PIZ | PIZZ | -6 · -0.30 · 12 | 1-1 | `r:1 F4:0.5 A4:0.5 C5:0.5 F4+A4:1 G4:0.5 E4:0.5 F4+A4+C5:1.5` |
| HRP | HARP | -6 · +0.30 · 25 | 1-1 | `r:4.5 GL(F3>F5):0.5 F3+C4+A4:1` |
| TMP | TIMP | -6 · +0.00 · 15 | 1-1 | `r:4.5 F2:1.5^` |
| BEL | HANDBELL | -4 · +0.00 · 22 | 1-1 | `r:4.5 F5+A5:1.5` |
| STR | PAD | -10 · +0.00 · 25 | 1-1 | `r:1 F3+C4+A4:2.5 C4+E4+Bb4:1 F3+C4+A4:1.5` |
| BAS | BASSPIZZ | -6 · +0.00 · 10 | 1-1 | `r:1 F2:1 C3:1 F2:0.5 C2:1 F2:1.5` |

**`jingle_new_feature`** — New feature unlocked

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 120 BPM · 4 beats | 1 | 2.20 s (4 beats at 120 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

- **Note:** SPARKLE layer at 1.4 beats (0.7 s)

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| HRP | HARP | -6 · +0.30 · 25 | 1-1 | `GL(F4>F6):0.8 r:3.2` |
| CEL | CELESTA | +0 · +0.10 · 22 | 1-1 | `r:0.8 F5:0.3 A5:0.3 C6:2.6` |
| BEL | SHOPBELL | -4 · +0.00 · 20 | 1-1 | `r:1.4 A5+C6:2.6` |

**`jingle_evolution`** — Evolution reveal

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 100 BPM · 5 beats | 1 | 3.00 s (5 beats at 100 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

- **Note:** the music box still stops on G; the bell blooms F major underneath (only finale/mythic let the music box itself play the F)

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| STR | PAD | -6 · +0.00 · 30 | 1-1 | `C3+G3+E4:2< F3+C4+A4:3` |
| GHM | GHARM | -10 · +0.00 · 35 | 1-1 | `r:2 F4+A4+C5:3` |
| MBX | MBOX | +0 · +0.00 · 25 | 1-1 | `r:2 C6:0.2 A5:0.2 F5:0.2 G5:0.2 A5:0.2 G5:2` |
| BEL | HANDBELL | -3 · +0.00 · 25 | 1-1 | `r:3 A4+C5+F5:2` |

**`jingle_new_friend`** — New friend (gacha, new lineage)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 132 BPM · 4 beats | 1 | 2.00 s (4 beats at 132 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

- **Note:** vox_*_hello (if enabled) starts at +1.4 s, on the held C6

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| PIZ | PIZZ | -6 · -0.30 · 12 | 1-1 | `C4:0.5 F4:0.5 r:3` |
| MBX | MBOX | +0 · +0.00 · 22 | 1-1 | `r:1 A5:0.5 G5:0.5 A5:0.5 C6:1.5` |
| BEL | HANDBELL | -6 · +0.00 · 22 | 1-1 | `r:2.5 F6:1.5` |
| HRP | HARP | -8 · +0.30 · 25 | 1-1 | `r:2.5 F3+C4+A4:1.5` |

**`jingle_gacha_legend`** — Legendary reveal

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| Bb major | 96 BPM · 4 beats | 1 | 2.60 s (4 beats at 96 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| TMP | TIMP | -6 · +0.00 · 15 | 1-1 | `ROLL<(Bb2):1.3 Bb2:2.7^` |
| HRP | HARP | -6 · +0.30 · 25 | 1-1 | `GL(F4>F6):0.8 r:3.2` |
| HRN | HORN | -6 · +0.00 · 22 | 1-1 | `Bb3+D4+F4:1.3< Bb3+D4+F4+Bb4:2.7` |
| GLK | GLOCK | -3 · -0.10 · 20 | 1-1 | `r:1.3 F5:0.2 Bb5:0.2 D6:0.2 F6:2.1` |
| BEL | HANDBELL | -4 · +0.00 · 22 | 1-1 | `r:1.3 Bb5:2.7` |

**`jingle_gacha_mythic`** — Mythic reveal (the last note finally sounds)

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 80 BPM · 4 beats | 1 | 3.50 s (4 beats at 80 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

- **Note:** SPARKLE cloud (60 grains) from beat 1.5 for 1.5 s

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBX | MBOX | +0 · +0.00 · 25 | 1-1 | `C6:0.25 A5:0.25 F5:0.25 G5:0.25 A5:0.25 G5:0.25 F5:2.5` |
| GHM | GHARM | -8 · +0.00 · 35 | 1-1 | `r:1.5 F4+A4+C5:2.5` |
| CEL | CELESTA | -8 · +0.20 · 25 | 1-1 | `r:1.5 F6:0.5 A6:0.5 C7:1.5_` |
| BEL | HANDBELL | -4 · +0.00 · 25 | 1-1 | `r:1.5 F5+C6:2.5` |
| HRP | HARP | -6 · +0.30 · 28 | 1-1 | `GL(C4>C6):1.5 F3+C4+A4:2.5` |

**`jingle_first_meeting`** — First meeting

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 100 BPM · 3 beats | 1 | 2.00 s (3 beats at 100 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

- **Note:** PAD swells −20 → −12 dB; shy: ends on the third

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| MBX | MBOX | +0 · +0.00 · 22 | 1-1 | `C5:0.5 D5:0.5 C5:0.5 A4:1.5` |
| PAD | PAD | -12 · +0.00 · 30 | 1-1 | `F3+C4+A4:3` |
| HRP | HARP | -8 · +0.30 · 25 | 1-1 | `r:1.5 F3:1.5` |

**`jingle_graduation`** — FTUE graduation

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| F major | 116 BPM · 5 beats | 1 | 3.00 s (5 beats at 116 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| BAS | BASSPIZZ | -6 · +0.00 · 10 | 1-1 | `F2:1 C3:1 F2:1 C3:0.5 F2:1.5` |
| GLK | GLOCK | -2 · -0.10 · 20 | 1-1 | `C5:0.5 F5:0.5 A5:0.5 C6:0.5 Bb5:0.5 A5:0.5 G5:0.5 F5:1.5` |
| PIZ | PIZZ | -6 · -0.30 · 12 | 1-1 | `F4+A4:0.5 r:0.5 F4+A4:0.5 r:0.5 G4+Bb4:0.5 r:0.5 E4+G4:0.5 F4+A4+C5:1.5` |
| HRP | HARP | -6 · +0.30 · 25 | 1-1 | `r:3.5 GL(F3>F5):0.5 F3+C4+A4:1` |

**`jingle_dungeon_clear`** — Dungeon floor cleared

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| D Dorian (open D) | 108 BPM · 4 beats | 1 | 2.40 s (4 beats at 108 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

- **Note:** ends on an open Dsus2 (no F#: stays inside the F-pentatonic world)

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| KAL | KALIMBA | -4 · -0.20 · 20 | 1-1 | `D4:0.25 F4:0.25 A4:0.25 C5:0.25 D5:0.5 r:2.5` |
| MBX | MBOX | +0 · +0.10 · 30 | 1-1 | `r:1 E5:0.5 G5:0.5 A5:2` |
| BEL | HANDBELL | -5 · +0.00 · 30 | 1-1 | `r:2 D6:2` |
| PAD | GHARM | -12 · +0.00 · 35 | 1-1 | `D3+A3+E4:4` |

**`jingle_event_open`** — Event opens

| Key | Tempo · meter | Bars | Length | Loop | Reverb | Master |
|---|---|---|---|---|---|---|
| C major | 120 BPM · 4 beats | 1 | 2.00 s (4 beats at 120 BPM + tail) | one-shot | plate 1.4 s | −13 LUFS short-term max · −1 dBTP · tail fades over the last 150 ms |

| Part | Instrument | dB · pan · rev % | Bars | Content (LLN) |
|---|---|---|---|---|
| SLG | SLEIGH | -14 · +0.30 · 12 | 1-1 | `ROLL(x):0.8 r:3.2` |
| GLK | GLOCK | -2 · -0.10 · 20 | 1-1 | `r:0.8 G5:0.25 C6:0.25 E6:0.25 G6:2.45` |
| PIZ | PIZZ | -6 · -0.30 · 12 | 1-1 | `r:0.8 C4:0.4 G4:0.4 C5+E5:2.4` |
| BEL | HANDBELL | -6 · +0.00 · 22 | 1-1 | `r:1.55 C6+G6:2.45` |


### 7.8 QC and ranking (objective; agents cannot listen)
`qc.py` measures every candidate. It writes `Assets/_Project/Audio/audio_qc.json` (§4.5) for the files that ship, and `scratchpad/audio/qc/qc_{wave}.md` for all candidates.

**Metrics:**

| Metric | How |
|---|---|
| duration, lead silence, tail | samples above/below −60 dBFS |
| `lufsMmax`, `lufsS` (max), `lufsI`, LRA | ITU-R BS.1770-4: K-weighting, 400 ms momentary, 3 s short-term, gated integrated |
| `truePeak`, `clippedRuns`, `dcOffset` | 4× oversampled peak; runs of ≥ 3 samples at ≥ 0.999 FS; mean |
| `attackMs`, `onsetBandHz` | 10 % → 90 % rise of the first envelope peak; spectral centroid of the first 15 ms |
| `centroidHz`, `brightness` | whole-file centroid; energy above 4 kHz / total |
| `harsh` | energy 2.8–4.5 kHz ÷ energy 1–2 kHz in the loudest 100 ms |
| `noiseFloorDb` | 10th percentile of 20 ms RMS frames outside the event (SFX) |
| `phoneLossLu` | M-max difference after HP 400 Hz + LP 8 kHz |
| `speechScore` | 0–1. Combines 3–6 Hz syllabic modulation energy in 300–3,000 Hz with voiced periodicity (YIN, 80–400 Hz) lasting more than 150 ms |
| `rootHz`, `pitchStdCents`, `pitchClasses` | YIN f0 track with parabolic refinement; 12-bin chroma |
| `loop` | §7.3.5 seam metrics |
| `bpm`, `gridErrorMs` | onset autocorrelation against the score tempo (music) |
| `correlation`, `monoLossDb` | stereo files only |
| `diversity` | minimum MFCC distance to the key's other candidates |

**Hard gates** (a failed candidate is excluded and its `failReasons` are recorded):

| Gate | Passes when |
|---|---|
| Length | within ±25 % of the catalog length; loops and intros exact to the sample |
| Lead silence | ≤ 2 ms (SFX); ≤ 20 ms (music intros) |
| Loudness | inside the tier target ± tolerance (§1.8) |
| Peak | true peak ≤ ceiling; no clipped runs; abs DC < 0.002 |
| Noise floor | ≤ −70 dBFS (SFX); not applied to ambience |
| Speech | `speechScore` < 0.2 for every non-VOX key; > 0.5 for VOX keys |
| Harshness | `harsh` ≤ 1.6, or ≤ 2.5 for keys whose recipe is mainly GLASS / GLINT / SPARKLE |
| Phone check | `phoneLossLu` ≤ 6 for P0/P1 keys (boss low layers ≤ 9) |
| Tuned keys | `pitchStdCents` ≤ 8 over the stable part, and `rootHz` within 15 c of the render root |
| `inkey` keys | ≥ 85 % of chroma energy on F G A C D |
| Loops | all seam metrics pass |
| Battle tracks | the SFX pocket rule (§7.6.0 rule 6) |
| Enemy keys (soft gate: a warning, not an exclusion) | centroid below the median centroid of the ally hits |

**Score** (0–100, passing candidates only):

`score = 30·loudnessFit + 20·onsetFit + 15·spectralFit + 15·cleanliness + 10·phone + 10·diversity`

| Term | Meaning |
|---|---|
| `loudnessFit` | 1 − abs(error) ÷ tolerance |
| `onsetFit` | attack inside the family target (clicks ≤ 3 ms, felt/pops ≤ 8 ms, swells: by recipe) |
| `spectralFit` | centroid inside the family's §1.4 lane |
| `cleanliness` | noise floor, DC and peak-to-loudness margin |
| `phone` | 1 − loss ÷ 6 |
| `diversity` | distance to the takes already selected |

**Selection and reports:**
- **Selection is greedy:** take the best candidate, then repeatedly add the one that maximises score with the diversity term, up to Var. The chosen takes get `recommended: true`. The builder follows that unless `AudioSelection` says otherwise (§4.4).
- **"Ears needed" list:** each wave report ends with the keys whose top two candidates are within 3 points of each other. Those are what the user should audition first in 사운드 시험장.

### 7.9 Voice barks (VOX, optional; Higgsfield speech models only)
**Models and voices:**
- Use `generate_audio` with exactly one of `seed_audio`, `text2speech_v2` or `elevenlabs_v4`. Never `inworld_text_to_speech`, `qwen_audio_tts` or any SFX/music model.
- Choose voices with `list_voices` or the model's preset ids: 2 candidate voices per lineage that match the character column below, Korean.
- Generate **3 reads per voice**: neutral, brighter, slower. Use the model's own rate/pitch parameters (e.g. `seed_audio` `speech_rate` / `pitch_rate`).

**Budget:** 21 lines × 2 voices × 3 reads = 126 generations. Before the batch, check the price with the tool's cost query (if it offers one) and with `balance`. This is the only place credits are spent.

**Lines:**

| Lineage | Voice character | hello (new friend) | cheer (evolution / milestone) | pet (tap-to-pet) |
|---|---|---|---|---|
| 짝짝이 jjakjjak | bright, bouncy, child-like, a little lonely | 짝꿍 찾았다! | 같이 가자! | 헤헤, 간지러워 |
| 똑단이 ttokdan | small, crisp, earnest | 똑딱! | 단단하게! | 데구르르~ |
| 찰칵이 chalkak | cool, clipped, secretly kind | 찰칵! | 열어 줄게! | 철컥…? |
| 몽당이 mongdang | tiny, warm, a little timid | 포옥… | 따뜻하게! | 바람은 싫어… |
| 우두커니 udukeoni | calm, slow, patient | 기다렸어. | 비 온다! | …톡. |
| 루즈리 rouge | playful, confident, sparkly | 쪽! | 반짝반짝! | 어머~ |
| 라라벨 lalabell | soft, sing-song, shy | 라라~ | 들어 줘! | 랄…라? |

**Processing:**
1. Trim to the words (≤ 1.0 s).
2. HP 150 Hz, LP 9 kHz, de-ess 6–8 kHz, compression 3:1 (soft knee).
3. IR box 8 %.
4. Tier B (−17 LUFS M-max). A pitch lift of +2…+4 st is allowed only with a formant-preserving shifter. Otherwise none: no chipmunk voices.

**Runtime rules** (§3, §5):
- Ui bus, Normal class.
- Plays only when `AudioService.VoiceBarks` is true. It defaults to false until the settings package sets it from a new `GameSettings.VoiceBarks` (§8.2).
- At most 1 bark per 6 s globally.
- `pet` at most once per 8 s per lineage; otherwise `pet_squeak`.
- `hello` starts 1.4 s into `jingle_new_friend`, on the held note.
- No barks from battle events. Only the user's own tap-to-pet plays on the battle screen.

**Rights:** record model, voice id and text for every shipped bark in a "Generated speech (Higgsfield)" section of `THIRD_PARTY_AUDIO.md`. These are not CC0. **The user confirms the commercial-use terms of the chosen preset voices before barks are enabled in a release build.**

### 7.10 Production order and effort
| Wave | Scope | Keys / takes / candidates | Done when |
|---|---|---|---|
| A (P0) | toolkit (`dsp`, `ir`, `master`, `loops`, `qc`); the P0 patches; P0 UI, battle, ambience; `bgm_battle_night_01`; `jingle_stage_clear` / `_fail` | 54 / 172 / 380 | QC report, library built on a project copy, compile OK, tests green, user notified to open 사운드 시험장 |
| B (P1) | all P1 SFX; music `bgm_battle_night_02` and `_03`, boss, dungeon, gacha (+ intro), story; P1 jingles; P1 ambience layers | 148 / 314 / 771 | same |
| C (P2) | per-vfx skills, modifiers, emitters, event and sleep music, P2 jingles, voice barks (after the user's terms check) | 104 / 208 / 495 | same, plus `THIRD_PARTY_AUDIO.md` complete |

**Effort:** SFX candidates take ≈ 0.1–0.5 s of CPU each in numpy, so all SFX candidates fit in about 15 minutes. A music candidate of 70–96 s (≈ 1,500–2,500 notes plus convolution) takes ≈ 1–3 minutes; 10 scores × 4 candidates ≈ 1–2 hours. Rendering runs in the scratchpad. Only selected 44.1 kHz files are copied into `Assets/_Project/Audio/**`, and only when the user's editor is not mid-import (or into a project copy first).

## 8. Integration, hand-offs, tests, acceptance

### 8.1 Call sites other workflows wire (the audio side never edits these files)
The audio implementation's **first change** appends the 69 `UISfxKey` members with the exact names and values of §2.1. Their call sites can then compile right away. Missing clips stay silent.

| Owner (files) | Call | Rule |
|---|---|---|
| UI · `UI/Common/PressScale` | `UISfx.Play(UISfxKey.Tap, 0.5f)` | up-inside within the drag threshold (existing plan) |
| UI · `UI/Main/MainTabController`, IA sheet frame | `Tab` on a tab switch with the sheet open; **`SheetOpen` on open, `SheetClose` (0.6) on close**; `MainButton` on 사냥; `AudioService.SetBattleFocus(BattleAudioFocus.Dimmed, owner)` on open and `ClearBattleFocus(owner)` on close | UX-SPEC-IA §1.5 currently says "Tab on open, nothing on close". Switching to the new keys is this spec's request |
| UI · `UIPopup` | `PopupOpen` / `PopupClose` | existing plan |
| UI · `MessagePopup` | `Confirm` on OK/confirm | |
| UI · `CostButtonView` | `Spend` on a successful purchase (`Deny` already wired) | |
| UI · `ToastView` | `Toast` (info), `Error` (error kind) | |
| UI · `RedDotView` | `RedDotNew` when a dot appears as *new* | gate 2 s |
| UI · `RewardFlyer` | `CoinLand` / `GemLand` on arrival | coin ladder in `UISfxGate` |
| UI · `LevelUpFanfareView` | `LevelUp`; `LevelUpMilestone` on every 10th level; `FriendLevelUp` for friends | Medium haptic unchanged |
| UI · `FeatureUnlockCard` | `FeatureUnlock` (jingle) | |
| UI · Meta (`GuideQuestBarView`, `MissionChestView`, `AttendancePopup`, `IdleChestPopup`, `ReturnPopup`, `SleepModeView`, `JournalPopup`) | `QuestComplete`, `MissionChest`, `Stamp` / `StampSpecial` (day 7/28), `BasketClaim`, `QuickIdle`, `CountTick` per counted step (≤ 1 per 70 ms) + `CountFinish`, `RewardDouble`; `SleepEnter`/`SleepWake` + `AudioService.SetSleep(bool)` | |
| UI · characters / evolution / costume panels | `PowerUp`, `ResonanceUp`, `EvolveCharge` → `EvolveBurst` → `EvolveReveal`, `CostumeEquip`, `CostumeUnlock`, `AffinityUp` | full-screen evolution: `SetBattleFocus(Hidden, owner)` |
| UI · `GachaPanel`, `GachaResultPopup`, `GachaCardView` | `GachaPullStart` (bundle drop), `GachaBundleOpen`, `GachaCardAppear`, `GachaAnticipate`, `GachaRevealCommon/Rare/Epic`, `GachaRare` (legendary), `GachaRevealMythic`, `GachaNewFriend`, `GachaDuplicate`, `GachaSkip`; **`AudioService.OnGachaReveal(grade)`** on each highlight; **`AudioService.PushMusic(MusicState.Gacha, owner)`** while the panel or result is open, `PopMusic(owner)` on close | 10-pull: one reveal sound per flip, at most 12 per second (the 0.08 s stagger) |
| UI · formation | `DragPick`, `DragDrop`, `DragSwap` | |
| UI · settings | `ToggleOn`/`ToggleOff`, `SliderTick` (≤ 1 per 150 ms while dragging); re-enable the BGM/SFX sliders and remove the '준비 중' pills when `UISfx.HasClips` | §8.2 |
| UI · expedition, dungeon, mail, shop | `ExpeditionDepart`/`ExpeditionReturn`, `DungeonSweep`, `PushMusic(MusicState.Dungeon)` while the dungeon panel is open, `AudioService.PlayJingle(Resolve("jingle_dungeon_clear"))` on a cleared floor, `MailClaim`, `Purchase` | |
| UI · tutorial / story | `CoachPop`, `TutorialStep`, `FirstMeeting`, `Graduation`, `StoryPage`; `PushMusic(MusicState.Story)` + `SetBattleFocus(Hidden)` in `StoryIntroView` | |
| UI or FX · battle HUD buttons | `SpeedToggle`, `AutoToggle`, `BossChallenge`; pet tap → `PetSqueak` (+ vox `pet` when enabled) | |
| UI · later features | `AchievementClaim`, `CodexDiscover`, `MapNode`, `TierUp`, `BlessingPick`, `RewardBig`, `Countdown`, `ProgressFill`, `DefeatAdvice` | as the features land |
| FX workflow | **nothing required**: `BattleAudioDirector` listens to `Game.Battle` and mirrors the timing (§5) | optional: §8.2 |

### 8.2 Requests to other owners
1. **Settings package** (`GameSettings`, `SettingsPopup`):
   - add `GameSettings.VoiceBarks` (PlayerPrefs `settings.voice_barks`, default on) and a row '캐릭터 목소리' (desc '친구들이 짧게 말해요');
   - in `Apply()`, set `AudioService.VoiceBarks = SoundOn && VoiceBarks`;
   - re-enable the BGM/SFX sliders when `UISfx.HasClips`, and preview `SliderTick`.
2. **UI workflow (IA):** the sheet keys in §8.1, and battle focus Dimmed while a sheet covers the field.
3. **FX workflow (optional):** expose these mirrored literals as `public const`, so `AudioTimingTests` can reference them instead of copying them:
   - `BattleFx.EchoShotLife` (0.14 s);
   - the echo melee swing (0.05 s);
   - the skill projectile lives (thread 0.38 s, others 0.26 s);
   - the all-enemies splash delay;
   - `BattleView.Cast` `HopTime` / `FirstHop` / `GiftTravel`.
4. **P4-0:** once `AppSignals.SleepChanged` / `GachaRevealHigh` exist, tell the audio workflow, which then subscribes in one small change.
5. **User:**
   - confirm the Higgsfield preset-voice terms before barks ship in a release;
   - if kenney.nl blocks automated downloads, optionally drop the five zips into `scratchpad/audio/cc0/<pack>/` yourself. Otherwise the CC0 layers are skipped.

### 8.3 Tests (`Tests/EditMode/Audio*Tests.cs`, pure C# where possible; `FeedbackGateTests` unchanged)
| File | Covers |
|---|---|
| `AudioCatalogTests` | unique keys; append-only order (a pinned hash of the first 306 keys); `UiMap.Length == UISfxKey count` and every entry resolves; aliases resolve; ladders valid; `ParseFileName` (`atk_needle_03` → take 3, `atk_needle` → 1, `ui_tap_2` → 2, `bgm_gacha_intro_01` → key `bgm_gacha_intro`) |
| `AudioGateTests` | min interval, max voices, jitter ranges, shuffle bag never repeats, token-bucket refill, ladder step/reset/hold, nearest-root take for multi-root tuned keys |
| `AudioVoicePoolTests` | steal order (late tails > lower class > oldest), never steals Critical, 50 ms young-voice protection, drop counters |
| `AudioDuckTests` | reason stacking, the deepest duck wins, release curves, sleep and focus ducks, background fade |
| `AudioSchedulerTests` | FX-clock advance with the mirrored `FxSlowMotion`: lamp ignite delays pending hits by 0.096 s, a boss-kill stop by 0.246 s; ordering; overflow drops the lowest class; flush on `StageChanged`; latency compensation cap |
| `AudioTimingTests` | the director's mirrors equal `AttackFxCatalog.FlightTime` / `KillHold`, `LampWave.Delay`, `EnemyView.KillHold` over an input grid; copied literals pinned |
| `BattleAudioLogicTests` | fake same-frame streams (GuardianAttack → CriticalHit → EnemyHit → EnemyKilled) give the right keys at the right times; 2× rules; crowd aggregator; 라라벨 rest on the 7th and the finale's F; lamp ladder in arrival order; boss music only on x-10 |
| `MusicDirectorTests` | priorities, owner stack, crossfade timing, rotation and rests, resume position, intro → loop scheduling |
| `AudioBootTests` | no clips → no `[Audio]` object and no log errors; legacy table only → legacy path; library → engine |

### 8.4 Acceptance (done when)
1. `python -I compile_check.py` prints `COMPILE OK`, and all EditMode tests pass on a project copy (never the user's open project).
2. With zero clips, behaviour is exactly today's: silent, no object, no errors.
3. With the P0 wave imported:
   - every §5.4 P0 event sounds;
   - hits start within ±15 ms of the visual arrival (`AudioTimingTests` plus the audition window's 10 s battle preview);
   - the battle never exceeds 28 new battle voices per second.
4. Audio adds 0 B of GC allocation per frame in steady state at 2× with 20 enemies, and ≤ 0.15 ms of main-thread time (Profiler / PerfProbe on the copy).
5. Every shipped take passes the §7.8 hard gates **or a documented exception** (`scratchpad/audio/rm1/qc_recheck.json`: 100 takes, 111 exceptions; see the RM-1 note below). Ears may still overrule any take in 사운드 시험장 (user checkpoint 2). The QC reports and the "ears needed" list are written.
6. `THIRD_PARTY_AUDIO.md` lists every external file used, and the builder's cross-check reports 0 warnings.
7. The production log contains no call to `sonilo_music`, `mirelo_text_to_audio` or `inworld_text_to_speech`.
8. The user can open **IdleGame/Audio/사운드 시험장**, hear every key and take, toggle the active takes and rebuild. `AudioSelection` survives every rebuild.

### 8.5 Risks (top first)
| Risk | Mitigation |
|---|---|
| Synthesised foley (cloth, paper, wicker) sounds "synthy" | HYB CC0 layers for exactly those keys; 2–3× candidates; the user's picks |
| Kenney downloads are blocked by bot protection | never bypass; manual drop or SYN-only (every HYB key works without CC0) |
| Additive/KS music lacks the depth of sampled orchestras | the scores are instrument-agnostic, so the same LLN files can later be rendered with CC0 multisamples (licence recorded) or replayed by a human; the music director, loops and loudness stay the same |
| Mirrored FX timing drifts when the FX workflow changes literals | `AudioTimingTests` fail loudly; request public consts (§8.2) |
| Parallel workflows compile against `UISfxKey` names before the engine lands | enum append first, with the fixed names and values of §2.1 |
| Android output latency varies by device | compensation capped at 60 ms and only for predicted events (§3.6) |
| Voice-bark terms or taste | barks default off (silent) until the setting exists and the user confirms the terms |
| 31 sources vs 32 real voices | no `PlayOneShot` anywhere; the pool never exceeds the source count |

---

## Appendix A. New and changed files
```
Assets/_Project/Scripts/Core/UISfx.cs                 (changed: facade → AudioEngine; API unchanged)
Assets/_Project/Scripts/Core/UISfxTable.cs            (changed: UISfxKey +69 members, values 12–80)
Assets/_Project/Scripts/Core/Audio/                   AudioTypes, AudioCatalog, AudioLibrary, AudioService, AudioEngine, AudioVoicePool,
                                                      AudioGate (gate, token bucket, shuffle bag, ladder), AudioScheduler, AudioDuck,
                                                      MusicDirector, AmbienceDirector, BattleAudioDirector (+ BattleAudioLogic), BattleAudioTiming
Assets/_Project/Editor/IdleDataBuilder.cs              (one line: AudioDataBuilder.Build(); after Validate)
Assets/_Project/Editor/Audio/                          AudioDataBuilder, AudioImportPostprocessor, AudioSelection, AudioAuditionWindow (사운드 시험장)
Assets/_Project/Audio/{Sfx/{Ui,Battle,Enemy,Loop},Bgm,Bgm/Jingles,Amb,Vox}/  {key}_{NN}.wav
Assets/_Project/Audio/{AudioSelection.asset, audio_qc.json, THIRD_PARTY_AUDIO.md}
Assets/_Project/Resources/Audio/AudioLibrary.asset    (built)
Assets/_Project/Tests/EditMode/Audio*Tests.cs          (§8.3)
scratchpad/audio/{catalog_data,recipes_data,scores_data}.py, {build_catalog,scores_build}.py,
                 audio_catalog.json, scores.json, synth/**, tools/**, cc0/<pack>/**, masters/**, qc/**
```

## Appendix Z: Acceptance note after RM-1 (2026-11-05, orchestrator)

- **§8.4 #5 is recorded as "green with documented exceptions".** After RM-1, 0 of 684 takes fail the §7.8 hard gates outright. 100 takes pass through 111 documented exceptions (loud-straddle 33, loud-w100 29, lead-jingle 29, inkey-jingle 9, inkey-peak 4, harsh-ill 4, inkey-cluster 2, intro-loud 1); evidence is in `scratchpad/audio/rm1/qc_recheck.json`.
- **Rule for short peak-limited UI clips:** clips under 400 ms that sit at the −1 dBTP ceiling are judged on the 100 ms loudness window. Jingles are exempt from the lead-silence rule and are checked in-key with the listed tolerances.
- **Final sign-off is the user's ears check** (`scratchpad/audio/EARS-NEEDED.md`). The 33 loud-straddle takes are first in line if any sound too loud.

## Appendix Z2: Amendments after RM-2 (2026-11-06, orchestrator)

Applied in the RM-3 docs pass (T-5) from `scratchpad/roadmap/rm3/triage.md` §3. They record what RM-2 built and G2 verified; where they differ from older text above, they take precedence.
- **A-11 (R39)** §8.4 #5: acceptance is "green with documented exceptions", as recorded in Appendix Z; the user's ears may still overrule any take at user checkpoint 2.
