# Session 04: Turn simulation and playback

**Recommended model:** Opus 5.5 (or the strongest model available when you start)
**How to start:** open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework.

This chat is **Session 04: Turn simulation and playback** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions.
2. `MEMORY.md`: status, decisions, placeholders, known issues and playtest reports.
3. `Docs/ARCHITECTURE.md`.
4. `Docs/SESSION_PLAN.md`: §4 (decisions), §5 (checkpoints), §6 (rules) and the Session 04 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), Appendix A and these sections: §3 (Execution phase, Stance persistence), §6 (Path rules: anchoring), §9 (Speed model), §13 (arena width and edge open point), §18 (Simulation).
6. The code this session touches: `Assets/XRim/Scripts/Simulation/`, `Simulation.Unity2D/`, `Presentation/Playback/`, `Presentation/Dummy/`, `DebugTools/`, `App/MatchBootstrap.cs`, `Networking/LocalTurnAuthority.cs`, the Session 02 spike code, and `Assets/XRim/Tests/`.
7. `Tools/README.md`, and the Session 02 spike findings plus the PT1 report in `MEMORY.md`.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Sessions 02 and 03. Check `MEMORY.md` → *Current status* and *Completed work log*. If any dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** PT1 (after Session 02) must be reported before this session starts. If `MEMORY.md` → *Playtest reports* has no PT1 report, ask me for it (the template is in `Docs/SESSION_PLAN.md` §5) and record it, plus any decisions and changed values it contains, before you start. If I explicitly choose to skip it, record that in `MEMORY.md` and continue. **D1 (weapon driver) and D2 (segmentation) must be recorded as decided. If either is missing, stop and ask me.**
3. **Resume.** If `MEMORY.md` → *Current status* shows Session 04 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with `git log`.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
4. **Git.** Run `git status` and tell me about any uncommitted files. Never stage or commit files you did not change in this session.
5. **Decisions.** This session needs: D1 and D2 (must be decided), and D22 (arena width and edge).
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
6. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **Promote the spike to production code.**
  - The D1 driver becomes the default. Keep the other driver as a selectable strategy only if I said so at PT1.
  - The ragdoll builder follows D2.
  - Remove spike-only shortcuts, or move them into DebugTools.
- **Implement `IPhysicsWorld` fully for two fighters.**
  - `Load` rebuilds from a `PoseSnapshot` and `MatchState`: both ragdolls, held items, and a placeholder list of severed limbs, all at zero velocity. Walls come in Session 12.
  - Every other member is implemented too.
- **The real `TurnSimulator`:**
  - A fixed 240 Hz loop on `SimClock`.
  - A weapon driver per side. The body-move drivers arrive in Session 05; use a neutral no-op driver for now.
  - Contacts are refined to path-progress time, sorted by `SimTime` then by stable id, and handed to a contact-handling seam that Sessions 06 and 07 will fill. For now it records raw contact events only.
  - The turn ends when both paths are complete and physics has settled for `SettleStepsRequired` steps, or at the 1.5 s hard cap.
  - `TimelineRecorder` frames and events become a `TurnResult` with the end `BoardSnapshot`.
- **Stance persistence.** The next turn `Load`s that snapshot, so positions carry over (§3, Decided).
- **Arena edges** are solid stops through the `IArenaEdgePolicy` default (D22), with the width from `ArenaSettings`.
- **Wiring.** `LocalTurnAuthority` uses the real Unity simulator in Play mode (composed in `MatchBootstrap`) and the stub in EditMode tests.
- **Presentation.** `DummyView` copies recorded poses onto the visual dummies and weapons, and `TimelinePlayer` plays them back.
- **DebugTools Playback tab:** speed 0.05×–2×, pause, frame step, scrub, loop, and re-simulate the last turn with the current tuning.
- **DebugTools Sandbox plan input**, which is how Sessions 05–07 and PT2 are tested before touch input exists:
  - Draw the left and right paths with the mouse (torso-relative, ink-limited, through the Rules path code) and pick each side's weapon.
  - Press Execute, watch playback, then plan the next turn from the frozen board.
  - It must be an `IPlanSource` that sends `PlanningCommand`s like real input will.

## 4. Definition of done

- **PlayMode tests:**
  - The same (board, plans) simulated twice gives the same event order and end poses within tolerance.
  - The hard cap ends a turn.
  - Loading a captured snapshot reproduces the pose within tolerance, at zero velocity.
- **EditMode tests:** the step loop and contact ordering with `FakePhysicsWorld`.
- The sandbox plays several turns in a row, and the poses carry over.
- `python Tools/check.py` passes, and PlayMode tests pass in the Editor.

## 5. TBDs in this session

- **§13 arena width and edge behaviour (D22):** the `IArenaEdgePolicy` default and a placeholder width.
- No other TBDs. Contact outcomes (hits, clashes, blocks) are deliberately left to Sessions 06 and 07.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Record them under *Open questions for the designer*.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 04 in progress", with the batch list.
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

1. Mark Session 04 complete in `MEMORY.md` (*Current status* and *Completed work log*, with commit hashes) and set the next session.
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Include how to open the sandbox, draw both plans, execute, and use the Playback tab.
3. Name the next session file: `Docs/sessions/session-05-body-moves.md`.
