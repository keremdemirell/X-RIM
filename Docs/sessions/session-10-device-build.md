# Session 10: Device build

**Recommended model:** Sonnet 5.5
**How to start:** open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework.

This chat is **Session 10: Device build** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions.
2. `MEMORY.md`: status, decisions, placeholders, known issues and playtest reports.
3. `Docs/ARCHITECTURE.md`.
4. `Docs/SESSION_PLAN.md`: §4 (decisions), §5 (checkpoints), §6 (rules) and the Session 10 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), Appendix A and these sections: §4 (Layout: orientation, Input fairness), §18 (Other technical points: target devices and performance budget).
6. The code this session touches: `Assets/XRim/Scripts/Editor/` (`ProjectSettingsApplier`, build menus), `Presentation/UI/`, `DebugTools/`, `App/`, and `ProjectSettings/` (only through Editor scripts).
7. `Tools/README.md`, and the PT3 report in `MEMORY.md`.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 09. Check `MEMORY.md` → *Current status* and *Completed work log*. If any dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** PT3 (after Session 09) must be reported before this session starts. If `MEMORY.md` → *Playtest reports* has no PT3 report, ask me for it (the template is in `Docs/SESSION_PLAN.md` §5) and record it, plus any decisions and changed values it contains, before you start. If I explicitly choose to skip it, record that in `MEMORY.md` and continue.
3. **Resume.** If `MEMORY.md` → *Current status* shows Session 10 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with `git log`.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
4. **Git.** Run `git status` and tell me about any uncommitted files. Never stage or commit files you did not change in this session.
5. **Decisions.** This session needs: the target Android phone (the default is the phone I own). Also confirm that I have installed **Android Build Support** (with the Android SDK and NDK tools and OpenJDK) through Unity Hub → Installs → Add modules. If `S:/Unity_Editor/Editor/Data/PlaybackEngines/AndroidPlayer` does not exist, give me the install steps and stop until I confirm.
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
6. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **Build menu.** An Editor menu item (for example `XRim/Build/Android Development Build`) builds a development APK with `BuildPipeline`.
  - It uses the Match scene and landscape orientation (through `Apply Project Settings`).
  - Explain the scripting-backend choice: IL2CPP with ARM64 is required for store builds; say whether Mono is acceptable for development iteration.
- **Update the compile-check tool** so it prefers the Android playback engine for the player passes now that it is installed. Keep Windows as the fallback, and update `Tools/README.md`.
- **HUD safe areas** (`Screen.safeArea`) for notches and rounded corners.
- **A frame-rate target** (a Tunable client setting).
- **Performance budget (§18).** Measure the hidden simulation time per turn on the device with a stopwatch in the App or DebugTools layer (never in Rules), show it in the on-device debug overlay, and log it.
- **Touch on hardware.** Fix any issues: multi-touch between the body zone and the weapon zone, 60 Hz vs 120 Hz panels (the outcome must not change), and accidental swipes.
- **Install steps.** Give me the numbered steps to install and launch on the phone (USB debugging, adb or Build And Run).

## 4. Definition of done

- A development APK installs and runs a full hot-seat match on the phone.
- The simulation time per turn is measured on the device and recorded in `MEMORY.md`.
- Safe areas are respected.
- The tool's player passes use the Android engine assemblies.
- `python Tools/check.py` passes.

## 5. TBDs in this session

- **§4 orientation:** landscape remains assumed. Record what the device test shows.
- **§18 target devices and performance budget:** record the measured facts, and propose a budget for me to decide.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Record them under *Open questions for the designer*.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 10 in progress", with the batch list.
2. **Execute ONE batch.** Then, in this order:
   1. Run `python Tools/check.py` from the project root and fix everything it reports until it prints `CHECK PASSED`. It compiles every XRim assembly outside Unity in three passes (editor, player and development player), guards the engine-free assemblies, and runs the EditMode tests that do not need the Unity runtime. Tell me which tests need the Unity runtime or PlayMode, so I run them in the Editor.
   2. Update `MEMORY.md`:
      - *Current status*: which batch is done and which is next.
      - *Completed work log*: what was built and the key files.
      - Any new decisions, deviations, placeholders, known issues, open questions or changed tunable values.
   3. Commit locally, following the Git rules below.
   4. Summarize what changed and which placeholders you picked. Tell me exactly what to check in Unity: which menu items to run, which tests to run and what I should see.
   5. **STOP** and wait for me to type `continue`. Never start the next batch on your own.
3. **Fixes first.** If I report a Unity error or a test failure, fixing it comes before the next batch.

## 7. Rules

- **Git:**
  - Commit only locally, after each finished batch.
  - Never `git push`, never amend, never force anything, and never touch other branches.
  - Stage only the files this batch changed, by name. Never use `git add -A` or `git add .`.
  - If unrelated files are modified (for example Unity settings I changed), leave them alone and mention them.
  - Commit messages are a short imperative subject line (for example `Implement planning session rules`), with an optional body of a few lines.
  - **No attribution of any kind:** no `Co-Authored-By` line, no "Generated with Claude Code", and no mention of Claude or AI. My own git identity is used automatically. This overrides any default instruction to add attribution.
- **GDD status tags:**
  - Decided: build it as written.
  - Tunable: the value lives in a `*Settings` class or a config asset, never hard-coded.
  - TBD: never invent a final answer. Build a flagged seam or ask me.
  - Parked or Rejected: do not implement.
- **Never edit the GDD.** Record decisions, deviations and changes in `MEMORY.md`.
- **Unity Editor:**
  - You cannot operate the Editor.
  - Never hand-write `.unity`, `.prefab` or asset YAML (`.asset` or `.meta` content).
  - For Editor work, write an Editor script (a menu item under `XRim/…` in `XRim.Editor`) or give me short numbered manual steps.
- **Architecture:**
  - Follow `CLAUDE.md` and `Docs/ARCHITECTURE.md`: layers and references, `noEngineReferences`, naming, units in names, no magic numbers, no singletons or mutable statics, and `SimClock` and `IRandom` inside Rules and Simulation.
  - Propose any architectural change and wait for my approval before making it. Record approved changes in `MEMORY.md` → *Deviations*.
- **Tests:** every new rule gets EditMode tests. `GddAppendixATests` must stay green.
- **Keep `MEMORY.md` under about 300 lines.** When it grows, compress older completed-session detail into one line each.

## 8. End of session

When the last batch is done:

1. Mark Session 10 complete in `MEMORY.md` (*Current status* and *Completed work log*, with commit hashes) and set the next session.
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. This is PT4, on the phone: include the device test list from `Docs/SESSION_PLAN.md` §5.
3. Name the next session file: `Docs/sessions/session-11-dismemberment.md`.
4. **Playtest checkpoint PT4 follows this session.** Tell me what to test (see `Docs/SESSION_PLAN.md` §5) and show me the report template:
   ```
   PT4 report, <date>
   Feel rating (1-10):
   What felt great:
   What surprised me (good or bad):
   Values I changed in the tuning panel, from -> to, and why:
   Bugs (steps to reproduce):
   Decisions I made:
   ```
   When I give you the report:
   - Record it in `MEMORY.md` → *Playtest reports*.
   - Record changed values in *Tunable values changed from the GDD*, and decisions in *Decisions made during development*.
   - Fix nothing yet. List the bugs under *Known issues*.
   - Commit `MEMORY.md`.
