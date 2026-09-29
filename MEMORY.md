# X-RIM Memory

The living memory shared by every Claude Code session. Read it first; update it after every batch.
Keep this file under about 300 lines. When it grows, compress each completed session's detail into one line in the work log.
The GDD is never edited: decisions and deviations are recorded here. The build plan is `Docs/SESSION_PLAN.md`; session prompts are in `Docs/sessions/`.

---

## Current status

- **Last completed:** Session 00 (build planning), batch 1 of 3.
- **In progress:** Session 00 (build planning). Batch plan:
  1. `Docs/SESSION_PLAN.md`, `MEMORY.md`, `CLAUDE.md` pointers (DONE).
  2. Session prompt files 01–11.
  3. Session prompt files 12–22.
- **Next session to start:** Session 01, `Docs/sessions/session-01-tooling-and-paths.md` (after Session 00 finishes).
- **Compile-check tool:** not built yet (Session 01 builds `Tools/check`).

---

## Completed work log

- **Setup (architecture), 2026-09-29.**
  - 15 asmdefs, settings and Config SOs, the Unity 2D physics world shell, the timeline player, the debug overlay, the Editor menus, Docs/ARCHITECTURE.md and CLAUDE.md.
  - All gameplay logic throws `NotImplementedException`.
  - Commits: `a8f4ff8`, `63c81d4`, `ba7402f`.
- **Session 00 (build planning), 2026-09-29.**
  - The 22-session build plan, this file, the session prompts and the CLAUDE.md pointers.
  - Commits: see git log ("Add session build plan…").

---

## Verification gate

- **2026-09-29: setup verified clean in the Unity Editor** (reported by the designer):
  - 0 red Console errors.
  - EditMode tests 32/32 pass; PlayMode 1/1 passes.
  - `XRim > Setup > Create Default Tuning Assets` created 27 assets.
  - The Sandbox scene enters Play mode with no errors.
- Git was clean before Session 00 started.

---

## Decisions made during development

| Date | Decision | By | Where |
|---|---|---|---|
| 2026-09-29 | Turn cap default is **30** (§3, §13). Appendix A's "15 turns" is outdated. | Designer | `MatchSettings.TurnCap` |
| 2026-09-29 | The opponent **sees when a player presses Ready** (Decided, not a flag). | Designer | `PublicPlanningState.IsReady` |
| 2026-09-29 | Landscape is locked through `XRim > Setup > Apply Project Settings` (landscape is still "assumed", TBD §4). | Claude, delegated | Editor menu |
| 2026-09-29 | Removed the packages Visual Scripting, Unity Version Control (collab-proxy) and Multiplayer Center. | Claude, delegated | `Packages/manifest.json` |
| 2026-09-29 | Session plan adopted: 22 sessions, feel spike at 02, first playable at 09 (Docs/SESSION_PLAN.md). | Designer asked for the plan | – |

Prototype decisions D1–D25 (SESSION_PLAN.md §4) are **not decided yet**. Add a row here for each one the designer decides, with the date. Undecided items are built with the recommended default as a flagged `[GddTbd]` seam.

---

## Placeholder values in code (not GDD values; all tagged `[Placeholder]`)

Chosen during setup (ARCHITECTURE.md §4). `XRim > Reports > Placeholder Values` lists the live set.
- HP 100; arm/leg durability 40/50; head stun threshold 30.
- Speeds (arena units/s): rapier 900, sword 600, spear 700, mace 250, shield 400, severed limb 300.
- Base damage: rapier 8, sword 12, spear 10, mace 20, shield 3, limb 10.
- Clash weights: mass 1.0, speed 0.005 (the mace still crushes the rapier). Glancing mass band 20%.
- Sword, spear, shield and limb ink and mass.
- Body-move displacements; move duration 0.4 s.
- Wall: damage 10, bounce 5, advance 50, spawn offset 60.
- Arena width 2000, starting gap 700, path sample spacing 10; 0.01 world units per arena unit.

Add new placeholders here, with the session that introduced them.

---

## Deviations from the GDD or architecture

- ARCHITECTURE.md §4: `*Settings` field initializers are the single source of truth. SOs wrap them (`SettingsConfig<T>`) instead of copying into a snapshot class. This refines the approved plan.

---

## Known issues and tech debt

- Only the **Editor** Bee response files exist (`Library/Bee/artifacts/1900b0aE.dag/*.rsp`). The player compile passes must be derived from them (Session 01).
- Only the **Windows standalone** playback engine is installed at `S:/Unity_Editor/Editor/Data/PlaybackEngines/`. Android Build Support is needed by Session 10. iOS builds need a Mac.
- Unity is installed at `S:/Unity_Editor/Editor`, not the Hub default path.
- Unity 6.5: `Object.GetInstanceID()` is a compile error.
- The GDD Appendix A still says 15 turns (see Decisions).

---

## Open questions for the designer

- Prototype decisions D1–D25 in `Docs/SESSION_PLAN.md` §4. D1 and D2 are answered at PT1.
- Later decisions by session: `Docs/SESSION_PLAN.md` §4, *Later decisions*.
- Optional: a rough SFX set before Session 13 (`Docs/SESSION_PLAN.md` §3).

---

## Tunable values changed from the GDD

| Date | Value | GDD | Now | Why |
|---|---|---|---|---|
| 2026-09-29 | Turn cap | 15 (Appendix A) | 30 | Designer correction; §3 and §13 already say 30 |

---

## Playtest reports

None yet. PT1 follows Session 02. Report format: `Docs/SESSION_PLAN.md` §5.
