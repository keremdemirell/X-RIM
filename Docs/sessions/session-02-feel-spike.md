# Session 02: Feel spike (one dummy, one weapon)

**Recommended model:** Opus 5.5 (or the strongest model available when you start)
**How to start:** open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework.

This chat is **Session 02: Feel spike (one dummy, one weapon)** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions.
2. `MEMORY.md`: status, decisions, placeholders, known issues and playtest reports.
3. `Docs/ARCHITECTURE.md`.
4. `Docs/SESSION_PLAN.md`: §4 (decisions), §5 (checkpoints), §6 (rules) and the Session 02 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), Appendix A and these sections: §1 (pillars 1 and 4), §2 (art direction: markers as hit zones), §6 (ink, path anchoring), §9 (speed model), §10 (Stage 1: contact angle), §12 (Presentation), §18 (Simulation).
6. The code this session touches: `Assets/XRim/Scripts/Simulation/`, `Simulation.Unity2D/`, `Presentation/Playback/`, `Presentation/Dummy/`, `DebugTools/`, `Editor/`, `Config/SimulationConfig.cs`, `Config/WeaponDefinition.cs` and `Assets/XRim/Tests/PlayMode/`.
7. `Tools/README.md` (the compile-check tool from Session 01).

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 01 (the tool and the path rules). Check `MEMORY.md` → *Current status* and *Completed work log*. If any dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** No playtest checkpoint is due before this session.
3. **Resume.** If `MEMORY.md` → *Current status* shows Session 02 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with `git log`.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
4. **Git.** Run `git status` and tell me about any uncommitted files. Never stage or commit files you did not change in this session.
5. **Decisions.** No decision is needed before this session. It produces the evidence for D1 (weapon driver) and D2 (ragdoll segmentation), which I decide at PT1. If `MEMORY.md` already records either one, tell me and follow it.
6. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

This is a **deliberately small spike**. Its only goal is to de-risk the physics and to let me choose between a kinematic and a motor weapon driver, before the combat rules exist.

**Out of scope:** damage, clashes, the planning session, the two-fighter turn loop, the HUD and touch input. Where a step needs something from a later session, stub it and note that.

1. **Placeholder ragdoll builder.** An Editor menu item, for example `XRim/Spike/Build Placeholder Dummy`, builds the prefab in code with `PrefabUtility.SaveAsPrefabAsset`, from primitive sprites:
   - One `Rigidbody2D` per part, `HingeJoint2D` limbs with angle limits, and a collider and `PhysicsBodyTag` per part.
   - Yellow-and-black marker colours per hit zone (§2: the markers are the hit zones).
   - A hand anchor on the dominant arm, and a held weapon body for the rapier and for the mace, sized from `WeaponStats` (ink thickness as the hit width; length a Tunable placeholder).
   - Every dimension, mass and limit is a placeholder in config; none are magic numbers.
   - Make **segmentation a builder option**: 6 bodies (one per `BodyPart`), or 10 bodies (upper and lower limb segments mapped to the same `BodyPart`). I decide D2 at PT1.
2. **Standing.** The dummy must stand still and stable in the hidden scene, for example with the pelvis or root driven and an upright torque or motor. Record the approach and why.
3. **`Unity2DPhysicsWorld`.** Implement what the spike needs: `Load` (spawn dummies from the prefab at the given poses, zero velocity), `SetHeldItemTarget`, `SetRootTarget`, `Step`, `DrainContacts`, `GetPose`, `CapturePose` and `IsSettled`.
   - `DrainContacts` returns `ContactFacts` with the parts, normal, relative velocity and point, in a stable order.
   - Everything stays in the hidden `LocalPhysicsMode.Physics2D` scene, stepped only through `PhysicsScene2D.Simulate` at `SimulationSettings.StepRateHz` (240).
   - Members the spike does not need may keep throwing `NotImplementedException`; list them.
4. **Two drivers behind `IWeaponDriver`**, in engine-free `XRim.Simulation`:
   - A **kinematic** path follower: the weapon target is exactly the path point at distance d = v·t, applied with `MovePosition` and `MoveRotation`.
   - A **motor** driver: a PD or target-joint force chases the same target, with Tunable gains.
   - The driver choice and the gains live in `SimulationSettings`/config and can be switched at runtime in the spike.
   - The weapon speed is the weapon's stat (§9, Decided), never finger speed.
5. **Time-to-impact.** For each contact, refine t from path progress inside the step (t = d / v, §9) and stamp it as `SimTime` in microseconds.
6. **Spike scene.** A menu item, for example `XRim/Spike/Create Spike Scene`, builds the scene in code: a camera, one dummy holding a rapier or mace, and a static or passive target dummy. A spike harness (dev-only, so in `XRim.DebugTools` or another place that respects the layer rules) supports:
   - Dragging with the mouse to draw a torso-relative path, resampled and ink-limited by the Session 01 path code.
   - Keys for weapon (rapier or mace), driver (kinematic or motor), execute, reset and playback speed.
   - Execution simulates the whole swing up front in the hidden scene, records per-step poses with `TimelineRecorder`, and plays them back in the visual scene with `TimelinePlayer` and `DummyView` at adjustable speed.
   - An on-screen contact log with the parts, normal, relative speed, clash angle (§10 Stage 1: the angle between relative motion and the contact surface) and refined time-to-impact in ms.
7. **Tuning.** Expose the spike's knobs (motor gains, joint limits, masses, step rate) through the existing config and tuning panel, so I can change them in Play mode.

## 4. Definition of done

- Both drivers work behind `IWeaponDriver`, with a runtime toggle.
- Time-to-impact from path distance matches the step of the contact within one step.
- **PlayMode tests:**
  - The weapon reaches the end of a straight path at t = length / speed, ± one step.
  - The hidden scene still never moves the visual scene.
  - The dummy stays standing (settled) with no input.
- `python Tools/check.py` passes, and PlayMode tests pass in the Editor.
- The spike findings are written into `MEMORY.md`:
  - Standing stability.
  - Tunnelling at 240 Hz with the 10-unit-wide rapier.
  - Pros and cons of each driver.
  - Milliseconds per simulated turn in the Editor.
  - Your recommendation for D1 and D2. It is a recommendation only; I decide.

## 5. TBDs in this session

- No GDD TBDs are resolved here.
- D1 (weapon driver) and D2 (segmentation) are **mine to decide at PT1**. Keep both options working until then.
- Every size, mass, gain and joint limit is a `[Placeholder]` in config.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Record them under *Open questions for the designer*.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 02 in progress", with the batch list.
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

1. Mark Session 02 complete in `MEMORY.md` (*Current status* and *Completed work log*, with commit hashes) and set the next session.
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Include the exact menu items to build the dummy and the spike scene, the controls, and what the contact log should show.
3. Name the next session file: `Docs/sessions/session-03-match-loop-and-planning.md`.
4. **Playtest checkpoint PT1 follows this session.** Tell me what to test (see `Docs/SESSION_PLAN.md` §5) and show me the report template:
   ```
   PT1 report, <date>
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
