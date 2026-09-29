# Session 07: Clashes and shield

**Recommended model:** Opus 5.5 (or the strongest model available when you start)
**How to start:** open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework.

This chat is **Session 07: Clashes and shield** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions.
2. `MEMORY.md`: status, decisions, placeholders, known issues and playtest reports.
3. `Docs/ARCHITECTURE.md`.
4. `Docs/SESSION_PLAN.md`: §4 (decisions), §5 (checkpoints), §6 (rules) and the Session 07 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), Appendix A and these sections: §7 (whole), §10 (whole), §13 (the shield never triggers the wall), Appendix A (clash values).
6. The code this session touches: `Assets/XRim/Scripts/Rules/Combat/` (`ClashResolver`, `IShieldBlockModel`, `IRepeatContactPolicy`), `Rules/Settings/ClashSettings.cs`, `WeaponStats.cs`, `Rules/Events/`, `Simulation/`, `Simulation.Unity2D/`, `DebugTools/` (sandbox, `GameplayGizmos`), and the tests.
7. `Tools/README.md`.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 06. Check `MEMORY.md` → *Current status* and *Completed work log*. If any dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** No playtest checkpoint is due before this session.
3. **Resume.** If `MEMORY.md` → *Current status* shows Session 07 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with `git log`.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
4. **Git.** Run `git status` and tell me about any uncommitted files. Never stage or commit files you did not change in this session.
5. **Decisions.** This session needs: D17 (stagger shares the stun effect), D19 (repeat contacts between the same two weapons), D20 (shield block model) and D21 (mace vs shield).
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
6. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **`ClashResolver`: the two-stage model (§10, Decided; every threshold and weight is Tunable).**
  - **Priority comes first.** A weapon that reaches a body before the paths cross simply lands its hit.
  - **Stage 1.** θ is the angle between the relative motion and the contact surface. θ ≥ `HardClashAngleDegrees` (30°) is a hard clash; below it is glancing.
  - **Stage 2.** P = W_m·m + W_v·v, where v is the weapon's speed stat (§9: speed is a weapon stat) and W_m ≠ W_v (validated).
  - **Hard clash, ratio ≥ `CrushRatio` (1.5):** the stronger weapon crushes through. The weaker weapon is knocked off its path and its dummy is **staggered** (same effect as the stun, D17). The stronger continues from the contact point at `CrushThroughDamageMultiplier` (0.7).
  - **Hard clash, ratio below the crush ratio:** both rebound in sparks, and neither continues.
  - **Glancing, masses outside the band:** the lighter weapon deflects the heavier off its path and continues; the heavier misses.
  - **Glancing, masses within the band:** both slide past and continue.
- **Simulation.** Apply every outcome (cancel, knock-off impulse, continue from the contact point along the rest of the path) and record `WeaponClashEvent` with the angle, both powers and the outcome, for the gizmos.
- **`IRepeatContactPolicy` (D19)** handles more contacts between the same two weapons in one turn.
- **Shield (§7, Decided).** The shield is a loadout item that replaces the weapon for the turn.
  - It follows its drawn path, then holds at the end point.
  - A tiny stroke or tap raises it in place for the whole execution.
  - A straight forward line is a bash that deals low damage.
  - It protects only where it actually is at each moment.
  - Weapon-to-shield contacts use `IShieldBlockModel` (D20, D21), **never** the clash model. Default: a full block for square hits on the face, and a partial reduction (a placeholder fraction) for edge or glancing hits. Propose in your batch plan how a partially blocked weapon carries its reduction into a later body hit.
  - Using a shield never counts as retreating (§13).
- **Gizmos.** `GameplayGizmos` draws the contact normals, the clash angle and the time-to-impact labels in the sandbox.

## 4. Definition of done

- **EditMode tests:**
  - Every branch of the §10 flowchart, including the exact threshold edges.
  - The W_m ≠ W_v validation.
  - The stagger effect.
  - Each block-model option.
  - The repeat-contact policy.
- **PlayMode scenarios:**
  - A mace crushes a rapier.
  - Two rapiers slide past each other.
  - A glancing rapier deflects a mace.
  - A shield arc blocks a thrust.
  - A shield tap holds its position through the whole execution.
- `python Tools/check.py` passes, and PlayMode tests pass in the Editor.

## 5. TBDs in this session

- **§7:** the block model (D20) and whether a mace breaks or staggers a shield (D21), through `IShieldBlockModel`. Shield variants are not built (their designs are TBD).
- **§10:** what a stagger does (D17, shared with the stun) and repeat contacts (D19).
- **Rejected; do not build:** a static 120° shield barrier, arc-only shield drawing, and a single clash formula with an angle multiplier.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Record them under *Open questions for the designer*.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 07 in progress", with the batch list.
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

1. Mark Session 07 complete in `MEMORY.md` (*Current status* and *Completed work log*, with commit hashes) and set the next session.
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. This is PT2: include a script of sandbox situations to try (crush, rebound, deflect, slide, shield block, rapier vs mace interrupts, the swing-armour toggle).
3. Name the next session file: `Docs/sessions/session-08-touch-input-and-hud.md`.
4. **Playtest checkpoint PT2 follows this session.** Tell me what to test (see `Docs/SESSION_PLAN.md` §5) and show me the report template:
   ```
   PT2 report, <date>
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
