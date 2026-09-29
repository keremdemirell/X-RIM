# Session 11: Dismemberment and desperation

**Recommended model:** Opus 5.5 (or the strongest model available when you start)
**How to start:** open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework.

This chat is **Session 11: Dismemberment and desperation** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions.
2. `MEMORY.md`: status, decisions, placeholders, known issues and playtest reports.
3. `Docs/ARCHITECTURE.md`.
4. `Docs/SESSION_PLAN.md`: §4 (decisions), §5 (checkpoints), §6 (rules) and the Session 11 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), Appendix A and these sections: §11 (Limb damage), §12 (whole), §18 (Simulation: joint breaks), §20 (Parked: emergency screwdriver; Rejected: throwing, breakForce).
6. The code this session touches: `Assets/XRim/Scripts/Rules/Limbs/`, `Rules/Damage/`, `Rules/Settings/LoadoutSettings.cs`, `Rules/Planning/`, `Simulation/`, `Simulation.Unity2D/` (`Ragdoll.BreakJoint`), `Presentation/Dummy/`, `Presentation/Vfx/`, `DebugTools/`, and the tests.
7. `Tools/README.md`.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 09. If Session 10 is complete, its PT4 report is due. Check `MEMORY.md` → *Current status* and *Completed work log*. If any dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** If Session 10 is complete: PT4 (after Session 10) must be reported before this session starts. If `MEMORY.md` → *Playtest reports* has no PT4 report, ask me for it (the template is in `Docs/SESSION_PLAN.md` §5) and record it, plus any decisions and changed values it contains, before you start. If I explicitly choose to skip it, record that in `MEMORY.md` and continue.
3. **Resume.** If `MEMORY.md` → *Current status* shows Session 11 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with `git log`.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
4. **Git.** Run `git status` and tell me about any uncommitted files. Never stage or commit files you did not change in this session.
5. **Decisions.** This session needs: the Session 11 row of *Later decisions* in `Docs/SESSION_PLAN.md` §4: §12 leg-loss penalty, body blows without arms, severed-limb stats and slot, and retrieving a limb that flew out of reach.
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
6. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **Sever.** When a limb's durability reaches 0, game logic breaks its joint through `Ragdoll.BreakJoint` / `IPhysicsWorld.BreakJoint`. Never use `breakForce`; it is Rejected.
  - The limb falls under gravity with a spray of bolts, nuts and hydraulic fluid. The VFX is placeholder here; Session 13 polishes it.
  - The limb stays on the arena floor, persists in `BoardSnapshot`, and is rebuilt by `Load`.
  - Emit `LimbSeveredEvent`.
- **Dominant arm severed (§12, Decided):**
  - The weapon drops (`WeaponDroppedEvent`, `DropHeldItem`).
  - The dummy fights on with its other hand at `OffHandDamageMultiplier` (80%) through the damage modifier list.
  - The dominant hand comes from handedness (§4, §12).
- **Both arms severed:** body blows only, through `IArmlessAttackMode` (the leg-loss decision row gives the default).
- **Leg severed:** that leg can no longer be used, through `IMobilityPenaltyPolicy` (the default from the decision row).
- **Severed limb as a weapon (§12, Decided):** a severed arm or leg on the floor can be wielded as a melee club by a remaining arm.
  - It is selectable per `LoadoutSettings.SeveredLimbIsExtraOption` (proposed: a temporary extra option beyond the 3 slots).
  - It is retrieved per `ILimbRetrievalPolicy` (proposed: automatic on selection).
  - It is **never thrown**; throwing is Rejected.
- **Death (§11, Decided):** at 0 HP the torso splits in two as the death animation. It is logic-driven, using a pre-split torso or an equivalent physics approach.
- **Debug:** a force-sever cheat in the sandbox or debug overlay.

## 4. Definition of done

- **EditMode tests:**
  - The off-hand multiplier.
  - The dominant-arm weapon drop.
  - Each policy default.
  - The limb club selectable per the flag.
  - No throw path exists.
  - Sever only after durability reaches 0 through capped hits.
- **PlayMode scenarios:**
  - Three arm hits → sever → the weapon drops → the next turn uses the off hand.
  - A severed limb persists across turns and can be picked up as a club.
- `python Tools/check.py` passes, and PlayMode tests pass in the Editor.

## 5. TBDs in this session

- **§12:** the leg-loss options, body-blow controls without arms, the limb weapon's stats and slot, and retrieval (including when the limb flew out of reach). Each is a seam with the default from *Later decisions*, unless I decide.
- **§11:** limb durability stays a placeholder; it is tuned at PT6 against the 10–20% target.
- **Parked; do not build:** the emergency screwdriver. **Rejected:** throwing anything, and `breakForce`.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Record them under *Open questions for the designer*.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 11 in progress", with the batch list.
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

1. Mark Session 11 complete in `MEMORY.md` (*Current status* and *Completed work log*, with commit hashes) and set the next session.
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Include using the force-sever cheat, then severing a limb in a real match.
3. Name the next session file: `Docs/sessions/session-12-wall-and-sudden-death.md`.
