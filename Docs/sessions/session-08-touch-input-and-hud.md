# Session 08: Touch input and planning HUD

**Recommended model:** Sonnet 5.5
**How to start:** open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework.

This chat is **Session 08: Touch input and planning HUD** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions.
2. `MEMORY.md`: status, decisions, placeholders, known issues and playtest reports.
3. `Docs/ARCHITECTURE.md`.
4. `Docs/SESSION_PLAN.md`: §4 (decisions), §5 (checkpoints), §6 (rules) and the Session 08 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), Appendix A and these sections: §4 (whole), §5 (swipe input), §6 (Ink budget UI, Path rules, Weapon switching), §18 (Path capture).
6. The code this session touches: `Assets/XRim/Scripts/Input/`, `Presentation/UI/PlanningHud.cs`, `Presentation/Cameras/`, `Config/InputConfig.cs`, `Config/ArenaSpaceConfig.cs`, `Rules/Planning/`, `Rules/Paths/`, `DebugTools/` (sandbox), and the tests.
7. `Tools/README.md`.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Sessions 03 and 05 (Sessions 06 and 07 should also be complete). Check `MEMORY.md` → *Current status* and *Completed work log*. If any dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** PT2 (after Session 07) must be reported before this session starts. If `MEMORY.md` → *Playtest reports* has no PT2 report, ask me for it (the template is in `Docs/SESSION_PLAN.md` §5) and record it, plus any decisions and changed values it contains, before you start. If I explicitly choose to skip it, record that in `MEMORY.md` and continue.
3. **Resume.** If `MEMORY.md` → *Current status* shows Session 08 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with `git log`.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
4. **Git.** Run `git status` and tell me about any uncommitted files. Never stage or commit files you did not change in this session.
5. **Decisions.** This session needs: D4 (reach clip shown in the preview), D7 (ink UI), D14 (no diagonal swipes), D23 (HUD placement) and D24 (handedness change and mirror toggle).
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
6. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **`TouchInputReader`** through EnhancedTouch only (never the legacy `UnityEngine.Input`). The mouse must work in the Editor, for example through touch simulation.
- **`ScreenLayout`.** The body zone is the `BodyZoneWidthFraction` (15%) strip over the player's own dummy; the rest is the weapon zone.
  - A left-handed player gets a mirrored layout (§4, Decided).
  - The flip is applied through `FixedSideCameraDirector` / `ArenaViewFlipped`.
  - For now handedness is a dev setting (D24); Session 18 builds the first-launch pick.
- **`FourWaySwipeClassifier`:** up, down, forward and back within `_swipeWindowSeconds` (about 0.3 s); no diagonals (D14). "Forward" and "back" are relative to the player's facing, including when mirrored.
- **`ScreenToArenaMapper`:** pixels to arena units in the fighter's torso frame (+X toward the opponent), undoing the view flip. The result must be independent of resolution and touch rate (§4, Decided).
- **`DualZoneInputScheme` and `TouchPlanSource`** emit `PlanningCommand`s only; the rules decide what is allowed. Drawing makes one continuous stroke that replaces the old one (D5), with the lead-in from the tip shown per D3.
- **Live path preview:**
  - The remaining ink, while drawing (D7).
  - The path clipped at the reach limit (D4).
  - Rigidity-invalid segments in red (for when the spear arrives).
  - The stroke thickness from the weapon.
- **`PlanningHud` (UI Toolkit),** laid out per D23:
  - A weapon selector showing the loadout, visibly disabled during the lock-out.
  - A Ready button, with cancel if D9 allows it.
  - The planning timer.
  - The opponent's current weapon and Ready state.
  - Signature buttons are Session 15; leave a slot.
- **The HUD and touch input drive the sandbox's left side,** and the right side in a simple hot-seat toggle.

## 4. Definition of done

- **EditMode tests:**
  - The swipe classification table, including mirrored layouts.
  - The body-zone and weapon-zone split.
  - The pixel-to-arena mapping at three resolutions: the same drawn shape costs the same ink.
  - Input samples at 60 Hz vs 120 Hz produce the same path.
- The HUD works with the mouse in the Editor's Game view at 1920×1080 and 2400×1080.
- `python Tools/check.py` passes.

## 5. TBDs in this session

- **§4:** HUD placement (D23), changing handedness later and a separate mirror toggle (D24, both through `IHandednessLayoutPolicy`), and the overall feel of the dual-zone scheme (`IInputScheme`). Orientation stays landscape (assumed).
- **§5:** diagonal or combined swipes (D14, through `ISwipeClassifier`).
- **§6:** the remaining-ink UI (D7, `PlanningHud.ShowInkRemaining`).
- **Rejected; do not build:** a virtual D-pad or direction buttons, and finger drawing speed as weapon speed.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Record them under *Open questions for the designer*.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 08 in progress", with the batch list.
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

1. Mark Session 08 complete in `MEMORY.md` (*Current status* and *Completed work log*, with commit hashes) and set the next session.
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Include testing at two Game view resolutions, drawing, swiping in the body zone, switching weapons into the lock-out, and mirrored (left-handed) mode.
3. Name the next session file: `Docs/sessions/session-09-first-playable.md`.
