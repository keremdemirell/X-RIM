# Session 05: Body moves

**Recommended model:** Opus 5.5 (or the strongest model available when you start)
**How to start:** open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework.

This chat is **Session 05: Body moves** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions.
2. `MEMORY.md`: status, decisions, placeholders, known issues and playtest reports.
3. `Docs/ARCHITECTURE.md`.
4. `Docs/SESSION_PLAN.md`: §4 (decisions), §5 (checkpoints), §6 (rules) and the Session 05 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), Appendix A and these sections: §5 (whole), §6 (Path rules: anchoring), §9 (Open points: body move timing), §12 (leg-loss open points: seam only), §13 (the backward swipe triggers the wall: fact only).
6. The code this session touches: `Assets/XRim/Scripts/Simulation/Drivers/`, `Simulation/Execution/`, `Simulation.Unity2D/`, `Rules/Settings/BodyMoveStats.cs`, `Rules/BodyMove.cs`, `Rules/Limbs/IMobilityPenaltyPolicy.cs`, `Config/BodyMoveDefinition.cs`, the `Assets/XRim/Data/BodyMoves/` assets (read via code, never hand-edit YAML), `DebugTools/`, and the tests.
7. `Tools/README.md`.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 04. Check `MEMORY.md` → *Current status* and *Completed work log*. If any dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** No playtest checkpoint is due before this session.
3. **Resume.** If `MEMORY.md` → *Current status* shows Session 05 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with `git log`.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
4. **Git.** Run `git status` and tell me about any uncommitted files. Never stage or commit files you did not change in this session.
5. **Decisions.** This session needs: D12 (backward: lean or real step) and D13 (does lunge add speed or damage).
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
6. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **`IBodyMoveDriver` implementations** for crouch/duck, lunge, step back/sway and jump/hop, plus Neutral.
  - Each drives the root target and keeps the ragdoll balanced.
  - They start when execution starts and run alongside the weapon path (§5, Decided).
  - Displacements, heights and durations come from `BodyMoveStats` and `BodyMoveDefinition` (Tunable placeholders).
- **The weapon path travels with the torso throughout the move** (§6, Decided): a duck plus a straight line becomes a low thrust under a high swing.
- **Intended effects are physical outcomes, not special rules:**
  - Crouch drops the centre of gravity so high strikes pass overhead.
  - Lunge pushes forward for reach and momentum. Its "more exposed" downside comes only from position, unless I decide otherwise.
  - Step back slips linear thrusts.
  - Jump clears low sweeps.
- **For Session 12,** the fact "a backward body move was chosen" must be available per side after a turn (in `TurnPlan` or the execution report). Only the player's input counts (§13).
- **`IMobilityPenaltyPolicy`** is a no-penalty default; Session 11 fills it.
- **The sandbox gets per-side body-move pickers,** as buttons or keys.

## 4. Definition of done

- **PlayMode tests:**
  - Each move reaches its configured displacement within its duration and settles.
  - Crouch lowers the head below a configured high-strike line.
  - Jump lifts the feet above a configured low-sweep line.
  - The weapon path stays torso-relative during every move.
- **EditMode tests:** driver maths with the fake physics world.
- The D12 and D13 flags are wired and switchable.
- `python Tools/check.py` passes, and PlayMode tests pass in the Editor.

## 5. TBDs in this session

- **§5 Backward:** lean in place or a real step (D12). Build both behind `BodyMoveStats.IsLeanInPlace`.
- **§5 Lunge:** does it add speed or damage (D13). The flags exist; the default is reach only.
- **§5 Diagonal or combined swipes:** input only (Session 08). Nothing here.
- **§12 Mobility after leg loss:** a seam with a no-penalty default.
- **§9 Body-move timing:** Tunable placeholders.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Record them under *Open questions for the designer*.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 05 in progress", with the batch list.
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

1. Mark Session 05 complete in `MEMORY.md` (*Current status* and *Completed work log*, with commit hashes) and set the next session.
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Include trying each move alone and combined with a straight thrust in the sandbox.
3. Name the next session file: `Docs/sessions/session-06-hits-and-damage.md`.
