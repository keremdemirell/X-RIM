# X-RIM Memory

The living memory shared by every Claude Code session. Read it first; update it after every batch.
Keep this file under about 300 lines. When it grows, compress each completed session's detail into one line in the work log.
The GDD is never edited: decisions and deviations are recorded here. The build plan is `Docs/SESSION_PLAN.md`; session prompts are in `Docs/sessions/`.

---

## Current status

- **Session 00 (build planning):** batch 2 of 3 done. Batch 3 (session prompt files 12–22) is still open; it does not block sessions 01–11.
- **Last completed: Session 01 (tooling and path rules), 2026-09-29**, 4 batches (commits `498fc06`, `132a19c`, `8c73050`, `f1269a7`). The designer committed the Session 01 `.meta` files and filled weapon assets (`0b31d73`).
- **Session 02 (feel spike) IN PROGRESS**, plan approved 2026-09-29. Batches:
  1. Engine-free path following: settings (driver, motor, root drive, ragdoll), torso frame, path cursor, weapon-aim seam, kinematic + motor drivers, `ContactAngle` rule, impact-time refiner. EditMode tests. — **done**
  2. Swing loop: `IPhysicsWorld`/`FighterPose` changes, `SwingSimulator`, `FakePhysicsWorld` update. EditMode tests. — **done**
  3. Placeholder ragdoll: runtime builder, `Ragdoll` (segments, hand, held items, tuning, mirror, rest pose), menu `XRim/Spike/Build Placeholder Dummies` (6- and 10-body prefabs). — **done**
  4. `Unity2DPhysicsWorld` (Load, targets, Step, contacts, poses, settled) + PlayMode tests. — **next**
  5. Spike scene menu + `SpikeHarness` (draw, keys, execute, playback with `TimelinePlayer`/`DummyView`).
  6. Contact log, tuning panel nested fields, diagnostics report; then the findings in this file after the designer's run.
- **Next session after 02:** Session 03, `Docs/sessions/session-03-match-loop-and-planning.md`. PT1 comes first.
- **Compile-check tool: `python Tools/check.py`** (from the project root; about 6 s, 25 s cold). Run it after every batch; it must print `CHECK PASSED`. Options: `--pass editor|player|dev|all`, `--no-tests`, `--filter TEXT`, `--verbose`. Usage, passes, define lists and limits: `Tools/README.md`.
  - Limits: Windows 64-bit Mono is the player proxy (Android is used automatically once installed); package reference DLLs are Editor builds; PlayMode tests, `[UnityTest]` and tests of Unity-side modules (Config, Input, Presentation, App) are listed as "needs Unity" and must be run in the Editor.
  - Which tests run: those whose namespace names an engine-free module (`XRim.Tests.EditMode.<Module>…`). Keep test namespaces matching their folders.

---

## Completed work log

- **Session 02 batch 3, 2026-09-30: placeholder ragdoll** (`Simulation.Unity2D/`, `Editor/`).
  - `PlaceholderRagdollBuilder.Build(RagdollBuildSpec)` (runtime, so tests can use it): flat hierarchy under a root at the pelvis, facing +X; torso box rising from the pelvis, circle head on the neck, arms from `PathSettings.ShoulderOffsetUnits` with length `ArmLengthUnits`, legs from the pelvis; ten bodies split limbs by `UpperSegmentFraction`. Hinges with limits (no motors, `enableCollision` off); `Hand` anchors at the end of each arm; one inactive, continuous-collision held item per weapon (length × ink thickness, weapon mass). `FitHeldItem` resizes to live stats.
  - `Ragdoll`: serialized parts, lower segments, hands, `HeldItemSlot`s and as-built reach; `MatchesReach`, `AssignOwner`, `MirrorForRightSide` (positions, anchors; limits flip in `ApplyTuning`), `ApplyTuning(body, gravityScale, limpArm)`, `UpdateServos(gain)`, `CreateRestPose` (call on the prefab or an unposed instance), `BreakJoint`.
  - Menu `XRim/Spike/Build Placeholder Dummies` → `Assets/XRim/Prefabs/Spike/PlaceholderDummy6.prefab` and `…10.prefab`, built in a preview scene; `PlaceholderSprites` makes `Assets/XRim/Art/Placeholder/{Square,Circle,CalibrationMarker}.png` (one world unit each).
  - `XRim.Tests.PlayMode` now references `XRim.Rules`. `PlaceholderRagdollTests` (7). PlayMode total 8, all need Unity.

- **Session 02 batch 2, 2026-09-29: swing loop** (`Simulation/Execution/Swing*`).
  - `IPhysicsWorld`: `Load(pose, state, rules, simulation)`, `TouchDistanceUnits`, `PushHeldItem`, `GetHeldItemState` (Unity side still throws until batch 4). `FighterPose.LowerSegments` + `HasLowerSegments` (D2 ten bodies).
  - `SwingSimulator.Run(SwingInput)` → `SwingResult` (timeline, `SwingContact`s, steps, `SwingEndReason`, final pose). Roots hold the start torso pose; drivers from `WeaponDriverFactory`; weapon contacts refined over the last two steps, then d = v·t; contacts sorted stably by time within a step; ends when paths are done and settled for `SettleStepsRequired`, or at the 1.5 s cap.
  - `FakePhysicsWorld`: exact moves, integrated pushes, `ScheduleContacts(step, …)`.
  - Tests: `SwingSimulatorTests` (9): t = L/v ± 1 step, impact time within the step (< 20 µs), ordering, body contacts, hard cap, motor, recording, load. 134 run here + 11 need Unity.
  - Note for PT1: with the motor driver the same scripted contact came out ~0.1 ms later than the path schedule (the blade trails its target slightly).

- **Session 02 batch 1, 2026-09-29: engine-free path following** (`Simulation/{Settings,Drivers,Execution}`, `Rules/Combat/ContactAngle.cs`).
  - `SimulationSettings` gains `WeaponDriver` (D1), `Segmentation` (D2), `GravityUnitsPerSecondSquared`, nested `WeaponMotor`, `RootDrive`, `Ragdoll` (sizes, masses, joint limits, pose-holding servo), and `Validate`.
  - `TorsoFrame` (pelvis origin, +X toward the opponent, mirrored for Right), `PathCursor`, `IWeaponAimModel` + `AimFromShoulderModel`.
  - `IWeaponDriver.Drive(stepStart, stepEnd, torso, heldItem)` → `HeldItemCommand` (MoveTo or Push, mass-free accelerations). `PathWeaponDriver` base (tip at d = v·t), `KinematicPathDriver`, `MotorPathDriver` (PD with target-velocity feed-forward, strength-limited), `WeaponDriverFactory`.
  - `ContactAngle.Degrees` (§10 stage 1). `ImpactTimeRefiner` sweeps a `BladeShape` over `PoseSample`s. Core: `Vec2.FromAngleDegrees/AngleDegrees/Rotated`, `XMath.DeltaAngleDegrees/LerpAngleDegrees/TwoPi`.
  - Tests: 61 new (125 run here + 11 need Unity).

- **Setup (architecture), 2026-09-29.**
  - 15 asmdefs, settings and Config SOs, the Unity 2D physics world shell, the timeline player, the debug overlay, the Editor menus, Docs/ARCHITECTURE.md and CLAUDE.md.
  - All gameplay logic throws `NotImplementedException`.
  - Commits: `a8f4ff8`, `63c81d4`, `ba7402f`.
- **Session 00 (build planning), 2026-09-29.**
  - The 22-session build plan, this file, the session prompts and the CLAUDE.md pointers.
  - Commits: see git log ("Add session build plan…").
- **Session 01 batch 4, 2026-09-29: path policies and pipeline** (`Rules/Paths/`).
  - D3 `LeadInFromTipPathStartPolicy`, D4 `ClampToReachPolicy` + `ReachLimit` (arm + weapon length around the shoulder, no lunge), D5 `ReplaceStrokePolicy`; defaults wired as `RulePolicies` property initializers (with `RigidityInkCostModel`). The three interfaces no longer carry `[GddTbd]` (decided).
  - `PathBuilder` → `BuiltPath`: stroke → lead-in → resample → reach clamp → ink cut-off → cut at a too-sharp turn. `Drawn` is kept for the next stroke; `Reachable` is for the preview. Session 03 (validation) and Session 08 (preview) call it.
  - New tunables: `PathSettings.ArmLengthUnits`, `ShoulderOffsetUnits`; `WeaponStats.LengthUnits` (validated > 0). Editor menu `XRim > Setup > Fill New Tuning Fields` fills numbers that are still 0 in weapon/body-move assets from `GddStartingValues`.
  - Tests: `PathPolicyTests`, `PathBuilderTests` (15). Check: EditMode 64 run here + 11 need Unity = 75.
- **Session 01 batch 1, 2026-09-29: `Tools/check.py`.**
  - Compiles every `XRim.*.asmdef` with Unity's Roslyn (`dotnet exec csc.dll -shared`, as Bee does). Sources come from the asmdef folders; XRim references come from the tool's fresh outputs; all other flags, analyzers and source generators come from `Library/Bee/artifacts/<hash>.dag/XRim.*.rsp`. A module with no `.rsp` falls back to a same-kind base `.rsp`, with a note.
  - Passes: editor (as is), player and dev (Editor/test asmdefs skipped, define constraints evaluated, `UnityEditor*` refs and Editor-only plugins/package asmdefs dropped, engine DLLs swapped for the player variation's).
  - Warnings fail the check; a warning does not block dependents, errors do.
  - Engine-free guard: no Unity refs, and a `MonoBehaviour` probe must fail with CS0246.
  - Current code: 15/11/12 assemblies OK, 6 guards OK, about 6 s warm (about 23 s when the compiler server starts cold). Outputs only in `Temp/XRimCheck/`.
- **Session 01 batch 2, 2026-09-29: tests, README, proof.**
  - `Tools/TestRunner/XRimTestRunner.cs` hosts NUnit's own framework runner (Unity's `nunit.framework.dll` 3.5) on Unity's .NET 8, so all NUnit attributes behave as in Unity. `check.py` builds it into `Temp/XRimCheck/runner/`.
  - Current code: EditMode 21 passed, 11 need Unity (App 1, Config 4, Input 3, Presentation 3) = Unity's 32; PlayMode 1 needs Unity.
  - Proof (temporary files, deleted): (a) syntax error in Rules, (b) `using UnityEngine;` in Rules, (c) runtime code calling a `#if UNITY_EDITOR`-only method (editor pass OK, player and dev fail), (d) a failing test: each made the check print `CHECK FAILED` and exit 1.
- **Session 01 batch 3, 2026-09-29: paths, ink, rigidity** (`Rules/Paths/`).
  - `PathResampler`: arc-length resampling, a point every `PathSettings.SampleSpacingUnits`, ends kept, duplicates dropped, no timing.
  - `InkMeasurement` now carries per-point cumulative cost. `LengthInkCostModel` (plain) and `RigidityInkCostModel` (§6 formula as written, only when `weapon.Rigidity.Enabled`; sharp-turn break measured over `BreakWindowUnits` so a corner between two samples still counts; the invalid index is where the sharp turn completes).
  - `InkCutoff` → `InkedPath` (path cut exactly where cost reaches `InkLengthUnits`, thickness from the weapon, remaining ink).
  - `RulesSettings.Validate` checks rigidity values. Tests: `Tests/EditMode/Rules/Paths/` (28). 60 Hz vs 120 Hz and two resolutions differ by about 0.2% of ink (tolerance 1%).

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
| 2026-09-29 | **D3 (§6 path start): Decided, anywhere.** An automatic straight lead-in from the current weapon tip to the first drawn point is added; it costs ink and time. | Designer | `IPathStartPolicy` default (Session 01) |
| 2026-09-29 | **D4 (§6 reach limit): Decided, clip at the reach limit** by clamping: points beyond reach are pulled onto the reach limit and the path continues when it comes back (not cut at the first exit). | Designer | `IReachPolicy` default (Session 01) |
| 2026-09-29 | **D4 detail: the lunge does not enlarge the torso-frame reach limit** (arm + weapon). A lunge extends reach in the arena because the torso-relative path moves with the body. | Designer | Reach limit (Session 01) |
| 2026-09-29 | **D5 (§6 strokes): Decided, one continuous stroke per turn; redrawing replaces it.** | Designer | `IStrokePolicy` default (Session 01) |
| 2026-09-29 | **An invalid (too-sharp) path is cut at the break**, not rejected: it executes up to the point where the sharp turn completes. | Designer | `PathBuilder` (Session 01) |
| 2026-09-29 | §6 rigidity formula read as written: Δθ is the whole turn angle, applied to segments turning more than the threshold (cost jumps at the threshold). Keep/cut rigidity stays TBD; k stays a placeholder. | Designer | `RigidityInkCostModel` (Session 01) |
| 2026-09-29 | **Weapon orientation along the path (not in the GDD): aim from the shoulder** as the default seam. The path is the tip's path; the weapon lies on the line shoulder → path point, the hand slides along it within arm reach. Every such choice must maximize feel, rush and legendary moments. | Designer | `IWeaponAimModel` (Session 02) |

Other prototype decisions (D1, D2, D6–D27 in SESSION_PLAN.md §4) are **not decided yet**. Add a row here for each one the designer decides, with the date. Undecided items are built with the recommended default as a flagged `[GddTbd]` seam.

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

- Session 01: `PathSettings.ArmLengthUnits` 240, `ShoulderOffsetUnits` (0, 100); `WeaponStats.LengthUnits` rapier 400, sword 320, spear 500, mace 220, shield 150, severed limb 200 (at roughly 2.5 mm per unit, the rapier nearly reaches across the 700 starting gap, the mace must close in). `RigiditySettings.BreakWindowUnits` = 20 (arc length over which a "very sharp turn" is measured, about two samples). `BendCostK` is documented as per degree.

- Session 02 (`SimulationSettings`, approved 2026-09-29): gravity 3924 units/s² (9.81 m/s² at ~2.5 mm/unit). Motor 12 Hz, damping 1.0 (linear and angular), max 60000 units/s² and 60000 °/s². Root drive (powered) max 20000 units/s² and 20000 °/s², correction 0.3. Ragdoll: head Ø50, torso 60×120 (pivot at the pelvis), arm width 24, leg 28×180, upper segment 0.5; masses torso 10, head 2, arm 2, leg 4; limits neck ±30, shoulder −120…220, elbow 0…140, hip −30…90, knee −130…0; servo 20 °/s per °, max 36000 °/s²; weapon arm limp (no servo).

Add new placeholders here, with the session that introduced them.

---

## Deviations from the GDD or architecture

- ARCHITECTURE.md §4: `*Settings` field initializers are the single source of truth. SOs wrap them (`SettingsConfig<T>`) instead of copying into a snapshot class. This refines the approved plan.
- **Session 02 (approved 2026-09-29):**
  - A1 `IPhysicsWorld.Load` also takes `SimulationSettings`; new `GetHeldItemState`, `PushHeldItem`; `SetHeldItemTarget` is the kinematic move.
  - A2 `IWeaponDriver.Drive(...)` returns a move (kinematic) or push (motor) command; both drivers are engine-free.
  - A3 `FighterPose` has optional lower-limb segment poses (10-body option).
  - A4 Torso frame = root target at the pelvis; shoulder and arm length come from `PathSettings`. Standing: dynamic torso tied to a kinematic root anchor by a strength-limited `RelativeJoint2D`; joints hold pose with hinge-motor servos; a kinematic-torso fallback toggle.
  - A5 Contacts: new pairs per step by polling, floor excluded, no self-collision, relative velocity from pre-step body velocities, order by load-time indices.
  - A6 Impact time: swept blade over the last two recorded steps (Box2D reports a contact one step late), then d = path distance at that time.
  - A7 `SwingSimulator` (spike loop, folded into `TurnSimulator` in Session 04); runtime ragdoll builder in `Simulation.Unity2D`; harness in `XRim.DebugTools` (references Input System).
  - A8 Gravity per body via gravity scale, tunable in arena units.

---

## Known issues and tech debt

- Only the **Editor** Bee response files exist (`Library/Bee/artifacts/1900b0aE.dag/*.rsp`). `Tools/check.py` derives the player passes from them. The player-only define list (DEBUG, TRACE, ENABLE_PROFILER and UNITY_ASSERTIONS only in dev players) is derived, not read from a real player build; confirm it once a real build exists (Session 10).
- Only the **Windows standalone** playback engine is installed at `S:/Unity_Editor/Editor/Data/PlaybackEngines/`. Android Build Support is needed by Session 10. iOS builds need a Mac.
- Unity is installed at `S:/Unity_Editor/Editor`, not the Hub default path.
- Unity 6.5: `Object.GetInstanceID()` is a compile error.
- The GDD Appendix A still says 15 turns (see Decisions).

---

## Open questions for the designer

- Prototype decisions D1–D27 in `Docs/SESSION_PLAN.md` §4 (D3, D4, D5 decided 2026-09-29). D1 and D2 are answered at PT1. D26 (hits per weapon per turn) and D27 (how the no-instant-KO head rule is enforced) are gaps the GDD does not cover.
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
