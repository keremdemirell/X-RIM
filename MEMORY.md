# X-RIM Memory

The living memory shared by every Claude Code session. Read it first; update it after every batch.
Keep this file under about 300 lines. When it grows, compress each completed session's detail into one line in the work log.
The GDD is never edited: decisions and deviations are recorded here. The build plan is `Docs/SESSION_PLAN.md`; session prompts are in `Docs/sessions/`.

---

## Current status

- **Session 00 (build planning):** batch 2 of 3 done. Batch 3 (session prompt files 12–22) is still open; it does not block sessions 01–11.
- **Last completed: Session 04 (turn simulation and playback), 2026-10-02**, 6 batches (work-log entry below). **Not committed yet:** the designer commits the whole session at once. Before it: Session 03 (match loop and planning rules, 2026-10-01; commits in its work-log line).
- **PT1 reported 2026-10-01** (see *Playtest reports*). D1, D2 and D22 decided at the start of Session 04 (see *Decisions*).
- **Next session to start: Session 05**, `Docs/sessions/session-05-body-moves.md` (decide D12 and D13 first; see *Open questions* for the wiring Session 04 left for it). No playtest checkpoint is due before it (PT2 follows Session 07).
- **Git (designer, 2026-10-01): no commits after batches.** The designer commits once when a session is done; each batch summary lists the files it changed. This overrides the commit step in the session prompts. Commit only if truly required, and say so first. Never push; no attribution.
- **Compile-check tool: `python Tools/check.py`** (from the project root; about 6 s, 25 s cold). Run it after every batch; it must print `CHECK PASSED`. Options: `--pass editor|player|dev|all`, `--no-tests`, `--filter TEXT`, `--verbose`. Usage, passes, define lists and limits: `Tools/README.md`.
  - Limits: Windows 64-bit Mono is the player proxy (Android is used automatically once installed); package reference DLLs are Editor builds; PlayMode tests, `[UnityTest]` and tests of Unity-side modules (Config, Input, Presentation, App) are listed as "needs Unity" and must be run in the Editor.
  - Which tests run: those whose namespace names an engine-free module (`XRim.Tests.EditMode.<Module>…`). Keep test namespaces matching their folders.

---

## Completed work log

- **Session 04 (turn simulation and playback), 2026-10-01 to 2026-10-02, complete.** Plan approved 2026-10-01 (6 batches below). EditMode run by `check.py`: 312 → 359 (377 with the 18 that need Unity); PlayMode 17 → 26. All green in the Editor 2026-10-02 (batches 1–5; batch 6 to be confirmed with the session checklist).
  - **Batch 1, the real turn loop (engine-free)** (`Simulation/Execution/`, `Simulation/Drivers/`, `Simulation/Recording/`):
    - `TurnSimulator(world, policies, TurnSimulatorOptions)`: loads the frozen board (state cloned, input never changed), steps at `StepRateHz` on `SimClock` up to `StepsFor(ExecutionHardCapSeconds)`. Per step: each side's body-move driver gives the root target (at step end), the weapon driver (D1 via `WeaponDriverFactory`) moves the weapon in that frame, the world steps, the pose is recorded (`RecordEveryNthStep`, plus the last), then the step's contacts are measured (`ImpactTimeRefiner` over the last two steps, d = v·t), put in `TurnContactOrder` and handed one by one to `ITurnContactHandler`. Ends when both weapon drivers and body moves are complete and `IsSettled` held for `SettleStepsRequired` steps (`TurnEndReason.Settled`), else `HardCap`. Result: `TurnResult(turnIndex, timeline, FinalBoard = state copy + last pose copy, ExecutionReport(state, no hits), EndReason)`.
    - Weapon used = `state.Fighters[side].CurrentWeapon` (the world loads the same one); no weapon or `HasHeldItem` false → no driver; an empty path holds the weapon.
    - **`TurnStartRoot.Of(pose)`** = frozen pelvis position, upright (0°): the root at turn start and the frame paths are drawn in.
    - Seams: `IBodyMoveDriverFactory` (default `NeutralBodyMoveDriverFactory` → `NeutralBodyMoveDriver` holds the start root for every move; Session 05 replaces it); `ITurnContactHandler` + `TurnContactContext` (`State`, `Rules`, `Record(MatchEvent)`; Sessions 06–07 extend it), default `RecordContactsHandler` → `ContactEvent` (a `MatchEvent` wrapping `TurnContact`).
    - `TurnContactOrder`: time, then first body's owner (Left, Right, arena), role, part, then the second body's; stable insertion sort. Contacts are ordered within the step that reports them (handling order across steps is by reporting step).
    - `TimelineRecorder.Build` sorts events by time (stable), because a contact refined into an earlier step can be recorded after a later one.
    - `IWeaponDriver.Drive` now takes the torso frame at step start and step end (`torsoAtStart`, `torsoAtEnd`); without a path the weapon is held in the torso frame, so it travels with a body move. `TorsoFrame.ToLocal(BodyPose…)` added.
    - D1/D2 defaults: `WeaponDriver = Kinematic`, `Segmentation = TenBodies` (no longer `[Placeholder]`).
    - `SwingSimulator` remained until batch 6.
    - Tests: `TurnSimulatorTests` (22), `TurnContactOrderTests` (4), `TimelineRecorderTests` (1), torso frame and driver additions (5). EditMode run here 312 → 344.
  - **Batch 2, Unity physics for two fighters and arena edges** (`Rules/Arena/`, `Simulation.Unity2D/`):
    - D22: `ArenaEdges` (left, right, `IsSolid`, `ClampX(x, margin)`), `IArenaEdgePolicy.EdgesFor(ArenaSettings)` (`[GddTbd]`), default `SolidStopArenaEdgePolicy` (±`WidthUnits`/2, solid) wired in `RulePolicies`; `ArenaSettings.WidthUnits` also `[GddTbd]`. `IPhysicsWorld.Load` takes the edges; `TurnSimulator` passes them and clamps every root target half a torso width inside solid edges.
    - `Unity2DPhysicsWorld.Load` builds a **fresh hidden physics scene every time** (old one unloaded async), so a turn simulated twice repeats exactly; `Scene` changes on each load. Floor spans the edges; solid edges are static walls (`BoundaryThicknessUnits` 100 thick, 4000 high), tagged `ArenaEdge`, never reported as contacts.
    - Held items set `useFullKinematicContacts`, so two kinematic blades report a contact (for Session 07's clashes).
    - Placeholder severed limbs: `Load` builds each `PoseSnapshot.SeveredLimbs` entry as a loose body of the limb's shape (`PlaceholderRagdollBuilder.BuildSeveredLimb`, no drawing), `CapturePose` writes them back; a fighter's severed parts are left off (`Ragdoll.LeaveOff`) and keep their loaded pose in captures; a severed dominant arm holds nothing (off hand is Session 11).
    - Tests: `ArenaEdgePolicyTests` (6), `TurnSimulatorTests` +2 (edges to the world, root stops at a solid edge). PlayMode `TurnSimulatorPlayModeTests` (7): same board and plans twice (same world and a fresh one) → same contacts in the same order and same end poses within 0.01 units/0.01°; hard cap ends a turn that never settles; loading a captured snapshot (mid-turn and end) reproduces it at zero velocity; the next turn starts from the last end pose; severed limbs rebuilt and lying on the floor; a solid edge stops a knocked dummy (open edges do not, as control); two kinematic blades meeting report a contact. EditMode run here 344 → 352; PlayMode 17 → 24 (Editor only).
  - **Batch 3, stance persistence and match wiring** (`Simulation/Physics/`, `Networking/`, `Presentation/`, `App/`, `Editor/SandboxSceneCreator.cs`):
    - Engine-free `GuardStance.Create(torso, side, dominantArm, weapon, segmentation, body, paths, aim)` (same pivots as the prefab builder; replaces `Ragdoll.CreateRestPose`, removed) and `GuardStance.TipLocal`; `StartingBoard.Create(state, rules, simulation, aim)`: pelvis at ±`StartingGapUnits`/2, height `LegLengthUnits`, upright, each holding its `CurrentWeapon`.
    - **Guard placeholder changed (designer, 2026-10-01: blades start apart):** `GuardAngleDegrees` −20 → −40, `GuardHandReachFraction` 0.5 → 0 (grip at the shoulder). Rapier tips 87 apart at the 700 gap, tip 23 above the floor. (The first idea, −50° keeping the hand out, put the tip below the floor.)
    - `PoseWeaponTipLocator` (tip = grip + weapon length, in the `TurnStartRoot` frame; no held item → guard tip) replaces `GuardStanceWeaponTipLocator` (deleted). `LocalTurnAuthority` defaults: this locator and `StartingBoard` (when `InitialPose` is null).
    - `MatchBootstrap` composes world → `RecordingTurnSimulator(TurnSimulator)` → `LocalTurnAuthority` (hold `ClientReport`, `ManualClock` advanced by unscaled time unless `IsPlanningTimerFrozen`) → `ClientMatchFlow` → `TurnPlayback` on two `DummyView`s (built by `VisualDummyFactory` from the ragdoll prefab of the match's segmentation) + `ArenaView` (floor, faint edge markers). Setup: both right-handed, default loadout, seed from `_randomSeed` (0 = new each match). Modes: `BotVsBot` starts at once; new `MatchMode.Sandbox` freezes the planning timer and waits for `StartMatch(sources)` from DebugTools (batch 5); HumanVsBot/HotSeat log an error until Sessions 08/09. Exposes `Authority`, `Flow`, `Playback`, `LastTurnInput`, `RulesSettings`, `SimulationSettings`, `Policies`, `Space`.
    - `TurnPlayback` (Presentation/Playback): `Play(result)` (weapons from the result's state), `ShowBoard`, `ShowWeapon`, `Advance`, `Finished` once per turn. `DummyView`: `AddHeldItem`/`ShowWeapon(WeaponId)`, poses applied in local space (arena-root flip works). `ArenaView` (new `Presentation/Arena/`).
    - `XRim/Setup/Create Sandbox Scene` now rebuilds the scene (camera ortho 5.75 at y 5.25 framing the whole arena, ArenaRoot, MatchBootstrap wired to the profile, both prefabs and the square sprite, mode BotVsBot).
    - Tests: `StartingBoardTests` (8, incl. default-loadout blades apart and above the floor), `PoseWeaponTipLocatorTests` (6), `LocalTurnAuthorityTests` +2 (starting board, next tips from the last end pose; `ScriptedTurnSimulator.NextPose`), `TurnPlaybackTests` (3, need Unity), `AppSanityTests` +1 (needs Unity); PlayMode `PlaceholderRagdollTests` rest-pose tests ported to `GuardStance` + `GuardStance_MatchesTheBuiltLayout` (6/10 bodies). PlayMode thrust helpers now thrust toward the opponent (the low guard would send "outward" thrusts into the floor). Editor run 2026-10-01: EditMode all green, PlayMode 25/26; `CrossingBlades_…` failed because with the new guard the blades only grazed at the 1200 gap (test geometry, not physics). Fixed: 1000 gap, plus a precondition that checks from the recorded frames that the blades really cross before asking for the contact. EditMode run here 352 → 368; Editor-only EditMode 11 → 15; PlayMode 24 → 26.
  - **Batch 4, Playback tab** (`Presentation/Playback/`, `App/`, `DebugTools/`):
    - `TimelinePlayer`: `Loop` (wraps to the start after reaching the end; events fire again each pass), `Restart()`, `CurrentFrameIndex`. `TurnPlayback`: `Replay()`; a looping or paused turn never raises `Finished`, so the authority (ClientReport hold) waits on it.
    - `MatchBootstrap.ResimulateLastTurn()`: same board and plans (`RecordingTurnSimulator.LastInput`) with a fresh rules and simulation snapshot from the profile; plays the result, **display only** (the match continues from the original result; with unchanged tuning it repeats exactly). `LastResimulation`, `LastSimulationMilliseconds` (Stopwatch in `RecordingTurnSimulator`). Visual dummies are rebuilt when a pose comes from the other segmentation (D2 switched in tuning). After a replay or re-simulation watched during planning, the planning board is shown again.
    - Debug overlay **Playback** tab (`PlaybackPanel`): turn summary (end reason, steps, duration, simulation ms, re-simulated marker), speed slider 0.05×–2× plus presets, Pause/Resume, frame step ◄ ►, Replay, Loop, scrub slider (pauses), re-simulate button, contact list (`ContactText`, ► = reached).
    - Tests: `TurnPlaybackTests` +3 (loop holds the turn, replay finishes again, restart re-fires events), Editor only. Editor-only EditMode 15 → 18.
  - **Batch 5, Sandbox plan input** (`DebugTools/Sandbox/`, `App/`, `Editor/SandboxSceneCreator.cs`):
    - `SandboxPlanSource : IPlanSource` per side: `SelectWeapon`, `SetPath(raw torso-frame stroke)`, `ClearPath`, `Ready`, all as real `PlanningCommand`s through the sink; keeps the weapon, the accepted stroke and the last rejection.
    - `SandboxController` (added by `DebugOverlay` when `MatchBootstrap.Mode == Sandbox`; waits for `IsComposed`, then `StartMatch` with both sources): active side (Tab or HUD buttons), mouse drag draws in that side's `TurnStartRoot` frame (screen → camera → `ArenaRoot` local → arena → torso frame), weapon buttons and 1/2/3, Clear (C), Execute (Space = Ready both). Preview per side through `PathBuilder` with the window's weapon tip and ink multiplier (`SandboxPathDrawer`: raw stroke white, executing path yellow at the hit width, in `ArenaRoot` space); HUD shows ink used/budget, reach clamp, ink cut, sharp-turn cut and rejections. Strokes never start over the HUD or the Debug panel (`DebugOverlay.Covers`).
    - Sandbox mode: `RecordingTurnSimulator.SimulationSettingsOverride` gives every turn the live simulation tuning (rules tuning stays the match's). `MatchBootstrap.ArenaRoot` exposed. `XRim/Setup/Create Sandbox Scene` now sets mode Sandbox.
    - No automated tests (debug UI in `XRim.DebugTools`, which tests do not reference); verified by hand in the Editor 2026-10-02: drawing, weapons, Execute and several turns worked; EditMode and PlayMode all green. Leaving Play mode logged a `MissingReferenceException` from `SandboxPathDrawer.Destroy` (the scene had already destroyed its lines); fixed with null checks.
  - **Batch 6, retire the spike and close** (`Simulation/Execution/`, `DebugTools/`, `Editor/`, `Docs/ARCHITECTURE.md`):
    - Deleted `SwingSimulator`, `SwingInput`, `SwingResult`, `SwingContact`, `SwingEndReason` and `SwingSimulatorTests` (every case already lives in `TurnSimulatorTests`); the PlayMode `Unity2DPhysicsWorldTests` swings now run through `TurnSimulator`.
    - Deleted the spike scene (`Scenes/Spike.unity`), `DebugTools/Spike/` (harness, board, visual dummy, path drawer, contact log, diagnostics) and `XRim/Spike/Create Spike Scene`. The feel report moved to `DebugTools/Diagnostics/FeelDiagnostics.cs` on the real turn loop, in its own physics world; **F9 in the sandbox** saves it to `Logs/XRimFeelReport.txt`. `MatchBootstrap.RagdollPrefabs` exposed for it.
    - `XRim/Spike/Build Placeholder Dummies` → **`XRim/Setup/Build Placeholder Dummies`**; the prefab folder `Prefabs/Spike` was renamed `Prefabs/Dummies` (moved with its `.meta` files, so references survive; `EditorPaths.DummyPrefabsFolder`).
    - `Docs/ARCHITECTURE.md` updated (assembly table, folders, composition, turn loop, edges, loading, presentation, debug tooling, menus, TBD register, decisions, status).
- **Session 03 (match loop and planning rules), 2026-10-01, complete.** Headless, engine-free; commits `d5dd47c`, `2c1a1ea`, `63a17f4`, `eb87585`, `7102dc8`. `PlanningSession` (every command, lock-out boundary included, Ready/cancel per D9, timeout executes what is set), `PlanValidator` + `PlanningTiming` (validates the path `PathBuilder` makes), `EndConditionEvaluator` (D11 order; turn cap fires after turn 30, never inside sudden death; `BothForfeitRule`), `SuddenDeathSetup` (HP 1, idle counters reset), `MatchStateMachine` (planning timer starts at `BeginPlanning`; lock on both Ready or each side's own timer; failing plan → idle plan with the session weapon; `CurrentWeapon` set at lock), `LoadoutValidator`, `LocalTurnAuthority` (simulator runs in `Tick`, one turn per call; playback hold modes None / TimelineDuration / ClientReport), `RandomBotBrain` (always valid, never idle). EditMode run here 142 → 312.
- **Session 02 (feel spike), 2026-09-29 to 2026-10-01.** Details: git log and the code; key pieces:
  - Engine-free (`XRim.Simulation`): `SimulationSettings` (+ `Settings/` driver, segmentation, motor, root drive, ragdoll, gravity, `Validate`), `TorsoFrame`, `PathCursor`, `IWeaponAimModel`/`AimFromShoulderModel`, `IWeaponDriver.Drive` → `HeldItemCommand`, `KinematicPathDriver`, `MotorPathDriver`, `WeaponDriverFactory`, `ArmReach`, `ImpactTimeRefiner`, `SwingSimulator` (spike loop for Session 04), `Rules/Combat/ContactAngle`. `IPhysicsWorld` gained `Load(…, SimulationSettings)`, `TouchDistanceUnits`, `PushHeldItem`, `GetHeldItemState`; `FighterPose` gained lower segments.
  - Unity (`XRim.Simulation.Unity2D`): `PlaceholderRagdollBuilder`, `Ragdoll` (mirror, tuning, servos, hand-follow spring, guard rest pose), `RagdollPrefabSet`, full `Unity2DPhysicsWorld` (hidden scene, root anchor + `RelativeJoint2D`, floor, contact polling).
  - Editor: `XRim/Spike/Build Placeholder Dummies` (6- and 10-body prefabs, placeholder sprites), `XRim/Spike/Create Spike Scene`. DebugTools: `SpikeHarness` (draw, swing, playback, keys), `SpikeVisualDummy`, `SpikePathDrawer`, `SpikeBoard`, `SpikeContactLog`, `SpikeDiagnostics` (F9 → `Logs/XRimSpikeReport.txt`); `TuningPanel` edits nested settings and `Vec2`. `DummyView` shows lower segments.
  - Tests: EditMode 153 (142 run by `check.py`), PlayMode 17. All green in the Editor (2026-10-01).

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

## Session 02 spike findings (2026-10-01; F9 report + the designer's play)

The spike was retired in Session 04; the same report now runs on the real turn loop with F9 in the sandbox (`Logs/XRimFeelReport.txt`). The numbers below were measured with the old −20° guard and 6 bodies by default.


- **Standing** (approach: a dynamic torso pulled to an invisible kinematic root anchor by a strength-limited `RelativeJoint2D`, upright included, plus hinge-motor servos holding every joint; both are solved inside Box2D, so it is stable, yet a hit can still knock the dummy): 6 bodies settle in 0.054 s, 10 bodies in 0.079 s, both stay settled; pelvis drift 1.5 units; torso tilt 0°.
- **Tunnelling at 240 Hz, rapier 10 wide:** no misses. Thrust into the chest and chop through the head registered at 1×, 2×, 4× and 8× rapier speed (up to 30 units per step). Even 60 Hz caught every one (at 60 Hz ×8 the refined distance was 6 units late: 282 vs 276). Not tested yet: blade against blade (Session 07), thin limbs side-on.
- **Kinematic driver:** exact on its path (lag 3.7 units only while starting, end error 0), settles 0.02 s after the path, cheapest. **Ignores weapon mass:** the rapier knocked the target 70 units and tilted it 23°, more than the mace (43 units, 14°). The blade is never deflected.
- **Motor driver:** tracks within 5 units, ends 0.7 off, same hit time as kinematic (308.3 ms). **Mass shows:** rapier 24 units of knockback, mace 42. The rapier was deflected 48 units and, ending its path inside the chest, kept pushing, so the swing never settled (hard cap). Needs D26 (stop at the hit with a recoil) to behave.
- **Repeated contacts:** one rapier thrust produced 8 contacts with the same chest as the target reeled → D19 (only the first contact between two bodies resolves) is needed in Session 07.
- **Cost in the Editor:** 8.3 ms (6 bodies, kinematic) to 14.8 ms (10 bodies, kinematic) per simulated swing on average, worst 18.4 ms, 192–360 steps, including loading both dummies.
- **Recommendation D1 (the designer decides at PT1): kinematic swing, then hand the blade to physics when the rules stop it.** Kinematic keeps t = d / v exact, which priority, interrupts and the sudden-death tie-break (§9, §14) rely on, and it never stalls a turn. The moment the rules stop the weapon (D26), switch the blade to a dynamic body carrying its own speed and mass, so the mace hits harder than the rapier, as the GDD identities want. If PT1 shows that motor hits feel better, that is the weight, and this hybrid keeps it. Of the two pure options, kinematic is the safer one.
- **Recommendation D2: 10 bodies,** unless PT1 shows tangled or noisy limbs. Just as stable (settles 0.08 s), still cheap (about 1.8× the 6-body cost in kinematic), and the elbows and knees give more physical comedy (pillar 4). Hit zones are identical either way.

---

## Verification gate

- **2026-09-29: setup verified clean in the Unity Editor** (reported by the designer):
  - 0 red Console errors.
  - EditMode tests 32/32 pass; PlayMode 1/1 passes.
  - `XRim > Setup > Create Default Tuning Assets` created 27 assets.
  - The Sandbox scene enters Play mode with no errors.
- Git was clean before Session 00 started.
- **2026-10-01: Session 02 verified in the Editor:** PlayMode 17/17 and EditMode 153/153 (two runs each), the spike scene plays with no errors, F9 report produced.

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

| 2026-10-01 | **Both players forfeit in the same turn (not in the GDD): sudden death setup**, treated like a double KO. Built as a flagged seam. | Designer | `EndConditionEvaluator` (Session 03) |
| 2026-10-01 | **D6, D8, D9, D10, D11: not decided; the designer asked for the recommended defaults as flagged `[GddTbd]` seams** (D6 drawing allowed in lock-out; D8 rapier/mace/shield, no shield slot required; D9 Ready cancel allowed until the lock-out starts; D10 any body move, drawn path or signature move resets the forfeit counter; D11 double KO > single KO > forfeit > turn cap). | Designer | `MatchSettings`, `LoadoutSettings`, `IIdleTurnPolicy`, `EndConditionEvaluator` (Session 03) |
| 2026-10-01 | **D1 and D2 not decided at PT1** ("too early"). Seams with the spike recommendations until decided. | Designer | Session 04 |
| 2026-10-01 | **D1 (weapon driver): Decided, kinematic.** The weapon sits exactly on its path (t = d / v exact). The motor driver stays selectable in tuning. Handing the blade to physics when the rules stop it comes with D26 in Session 06. | Designer (approved the recommendation) | `SimulationSettings.WeaponDriver` (Session 04) |
| 2026-10-01 | **D2 (segmentation): Decided, 10 bodies.** 6 bodies stays selectable in tuning. | Designer (approved the recommendation) | `SimulationSettings.Segmentation` (Session 04) |
| 2026-10-01 | **D22 (§13 arena width and edge, TBD): width 2000 (placeholder), the edge is a solid invisible stop.** Built as the `IArenaEdgePolicy` default, flagged `[GddTbd]`. | Designer (approved the recommendation) | `IArenaEdgePolicy`, `ArenaSettings.WidthUnits` (Session 04) |
| 2026-10-01 | **Blades start apart en garde** (not in the GDD): the guard stance keeps the weapons from touching at the starting gap. Built as the placeholder guard −40°, grip at the shoulder. | Designer | `RagdollSettings.GuardAngleDegrees`, `GuardHandReachFraction` (Session 04) |
| 2026-10-01 | **No body move = hold the frozen spot and straighten up** (not in the GDD beyond "the dummy holds its pose"): the root target at turn start is the frozen pelvis position, upright, and paths are drawn in that upright frame. | Claude, approved with the plan | `TurnStartRoot` (Session 04) |

Other prototype decisions (D7, D12–D21, D23–D27 in SESSION_PLAN.md §4) are **not decided yet**. Add a row here for each one the designer decides, with the date. Undecided items are built with the recommended default as a flagged `[GddTbd]` seam.

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

- Session 02 (`SimulationSettings`, approved 2026-09-29): gravity 3924 units/s² (9.81 m/s² at ~2.5 mm/unit). Motor 12 Hz, damping 1.0 (linear and angular), max 60000 units/s² and 60000 °/s². Root drive (powered) max 20000 units/s² and 20000 °/s², correction 0.3. Ragdoll: head Ø50, torso 60×120 (pivot at the pelvis), arm width 24, leg 28×180, upper segment 0.5; masses torso 10, head 2, arm 2, leg 4; limits neck ±30, shoulder −120…220, elbow 0…140, hip −30…90, knee −130…0; servo 20 °/s per °, max 36000 °/s²; weapon arm limp (no servo). Guard stance (batch 4): weapon at −20° from the shoulder, hand at 0.5 of arm length. Weapon arm follow spring (batch 4 fix): 20 Hz, damping 1.0, max 200000 units/s². Spike only (harness fields, not gameplay): target chest at 0.85 of the weapon's reach (574 rapier, 421 mace), step 25; camera ortho size 4.5 at height 3.

- Session 03: `PlaybackHoldSettings.ExtraSeconds` 1 s (stretch for hitstop and slow motion; Session 13 measures it) and `ClientReportTimeoutSeconds` 10 s (no client yet). `GuardStanceWeaponTipLocator`: every weapon rests in the Session 02 guard stance until Session 04 reads the pose (no attribute possible on a class, so it is listed here and in its doc comment). Debug-bot stroke constants in `RandomBotBrain` (aim ±60°, end at 50–100% of the reach, 95% of the ink, bow up to 25% of the length): bot behaviour only, not game rules. Tiny float tolerances in `PlanValidator` (0.01 ink, 0.1 reach).

- Session 04: `RagdollSettings.GuardAngleDegrees` −40 and `GuardHandReachFraction` 0 (blades apart en garde; replaces the Session 02 −20 / 0.5). Technical, not gameplay: edge walls 100 thick × 4000 high; sandbox camera ortho 5.75 at y 5.25; arena edge markers 8 wide × 600 tall.

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
  - `XRim.Editor` now references `XRim.DebugTools` (the spike scene menu adds the harness). The spike scene references a DebugTools component, which is fine because it is never in the build list (ARCHITECTURE says scenes never reference DebugTools; the spike scene is the dev-only exception).
  - Fix after the first Editor run (2026-10-01): the wrist hinge (hand pinned to a sliding point on the blade) blocked axial thrusts with a one-piece arm and yanked the torso with a kinematic weapon. Replaced by a one-way hand spring (`TargetJoint2D` on the arm's last segment, target = `ArmReach` point on the blade); the weapon is driven alone. A side holding a weapon without a path now gets an empty-path driver that holds the weapon still.

---

- **Session 03 (approved 2026-10-01):**
  - `PlanningSession` constructor also takes the loadout, `RulePolicies` and the weapon tip in the torso frame (it runs `PathBuilder`). `PlanValidator.Validate` takes the loadout and a `PlanningAudit` (switch and Ready times).
  - Batch 5 (approved in the plan): `ITurnAuthority.NotifyPlaybackFinished`, the playback hold modes and `PlanningWindow.WeaponTips` (the constructor gained a parameter; the window is how clients and bots get the tip).
  - Weapon-tip seam, refined in batch 1: `IWeaponTipSource` lives in `Rules.Planning` (the session needs it and Rules cannot see boards or poses). The authority builds one per side per turn; batch 5 adds its default, derived from the Session 02 guard-stance numbers and tagged `[Placeholder]`. Session 04 replaces it with the tip read from the frozen pose. Also new in batch 1: `CommandRejection.AlreadyReady` (appended to the enum) and `PlanningAudit`.
  - `ITurnAuthority` gains `NotifyPlaybackFinished(int turnIndex)`. `LocalTurnAuthority` playback hold modes: none (headless), timeline duration, client report (with a placeholder timeout).
  - Assumptions built and flagged: the turn cap fires after turn 30 resolves and never re-triggers inside sudden death; sudden-death setup sets both HP to `RuleConstants.SuddenDeathHp` (otherwise a double KO re-triggers forever), while board carry-over and the first-hit and tie rules stay a flagged Session 12 seam; a locked plan that fails validation executes as an empty plan with the side's current weapon.

- **Session 04 (plan approved 2026-10-01):**
  - Batch 1: new seams `ITurnContactHandler` (+ `TurnContactContext`), `IBodyMoveDriverFactory`, `TurnSimulatorOptions`; `TurnResult.EndReason`; raw contacts are recorded as `ContactEvent`, a `MatchEvent` subclass defined in Simulation (it wraps simulation types, so it cannot live in Rules); `TimelineRecorder.Build` returns events in time order.
  - Batch 1, found while building (not in the plan): `IWeaponDriver.Drive` takes the torso frame at both ends of the step, because a weapon held without a path lagged one step behind a moving body. Same behaviour for a still body.
  - The turn-start root is upright (`TurnStartRoot`), refining A4 (torso frame = root target).
  - Batch 2: `IPhysicsWorld.Load(…, ArenaEdges edges)`; `IArenaEdgePolicy` reshaped from `ResolveEdge(x, arena)` to `EdgesFor(arena)` (no implementation existed); a fresh physics scene per load; held items use full kinematic contacts.
  - Batch 3: `GuardStance`/`StartingBoard` (engine-free) replace `Ragdoll.CreateRestPose`; `PoseWeaponTipLocator` and the starting board are `LocalTurnAuthority`'s defaults; `MatchMode.Sandbox` with `MatchBootstrap.StartMatch(sources)` (App never references DebugTools); `DummyView` draws in local space and owns its weapon visuals; new folder `Presentation/Arena/`.
  - Batch 5: Sandbox mode refreshes simulation settings every turn (`RecordingTurnSimulator.SimulationSettingsOverride`); debug only.

---

## Known issues and tech debt

- Unity 6.5 conventions measured in the Editor (2026-10-01): contact normals point from `ContactPoint2D.collider` to `otherCollider` (confirmed, `ThrustIntoTorso_…` green); `HingeJoint2D.jointAngle` grows **clockwise** for the jointed body relative to its parent (measured −30 for a +30 forward swing), so `Ragdoll.JointAngleGrowsCounterClockwise` is false and limits are flipped when applied.
- Session 04 batch 1: `Assets/XRim/Data/Tuning/Simulation.asset` never stored `WeaponDriver` or `Segmentation`, so it now loads the new code defaults (kinematic, 10 bodies). If the Editor shows 6 bodies, the asset was saved with the old value; switch it in the tuning panel. The Session 02 PlayMode tests build `new SimulationSettings()`, so they now run with 10 bodies by default.
- The spear (placeholder length 500, design TBD) at the −40° guard has its tip below the floor (kinematic, so nothing happens physically). Not in the default loadout; Session 14 (roster) should give long weapons their own guard or length.
- Session 04: a dropped weapon disappears from the captured pose, a severed limb's visual stays attached on the `DummyView`, and the off hand never takes over the weapon: all Session 11 (dismemberment).
- PT1 note: in the spike the weapon is not jointed to the body (the arm follows it by a hand spring), so a heavy swing does not drag the dummy. If PT1 wants that comedy, add it later as a tuned reaction/knockback rather than a joint.

- Only the **Editor** Bee response files exist (`Library/Bee/artifacts/1900b0aE.dag/*.rsp`). `Tools/check.py` derives the player passes from them. The player-only define list (DEBUG, TRACE, ENABLE_PROFILER and UNITY_ASSERTIONS only in dev players) is derived, not read from a real player build; confirm it once a real build exists (Session 10).
- Only the **Windows standalone** playback engine is installed at `S:/Unity_Editor/Editor/Data/PlaybackEngines/`. Android Build Support is needed by Session 10. iOS builds need a Mac.
- Unity is installed at `S:/Unity_Editor/Editor`, not the Hub default path.
- Unity 6.5: `Object.GetInstanceID()` is a compile error.
- The GDD Appendix A still says 15 turns (see Decisions).

---

## Open questions for the designer

- Prototype decisions D1–D27 in `Docs/SESSION_PLAN.md` §4 (D3, D4, D5 decided 2026-09-29; D1, D2, D22 decided 2026-10-01). D26 (hits per weapon per turn) and D27 (how the no-instant-KO head rule is enforced) are gaps the GDD does not cover.
- Session 03: D6, D8, D9, D10, D11 are flagged seams with the recommended defaults (see *Decisions*); confirm or change them at PT3. Also confirm: idle counters reset when sudden death starts, and the both-forfeit pick (sudden death) once a real match shows how it feels.
- Wiring left for later sessions: **Session 09** must call `ITurnAuthority.NotifyPlaybackFinished(turnIndex)` from `ClientMatchFlow.OnPlaybackFinished` and choose the hold mode (`ClientReport` for a client, `None` for headless); **Session 06** owns status lifetime (the machine only calls `ApplyToNextTurn`); **Session 12** replaces the marked sudden-death seam in `EndConditionEvaluator` (both hit, nobody hits) with `ITiePolicy` / `ISuddenDeathNoHitPolicy`, adds `SuddenDeathSettings.BoardMode`, the electric-wall reaction at lock (`ElectricWallRules.OnPlansLocked`) and the sudden-death option flags (switching, signatures) as constraints; **Session 15** replaces the always-refused `UseSignature`; **Session 19** rebuilds the path from the raw stroke before validating and stamps the audit times from the server clock.
- **Wiring Session 04 left (2026-10-02):**
  - **Session 05:** pass a real `IBodyMoveDriverFactory` through `TurnSimulatorOptions.BodyMoves` (composed in `MatchBootstrap.Compose`); body-move roots start from `TurnStartRoot` and `TurnSimulator.RootAt` keeps them inside solid edges; weapon drivers already get the torso frame at both ends of a step. Sandbox body-move pickers: add `SetBodyMove` to `SandboxPlanSource` and buttons or keys to `SandboxController`. `RandomBotBrain` already plans body moves (they play as no move until then).
  - **Session 06:** fill `ITurnContactHandler` (extend `TurnContactContext` with what the rules apply: cancel an attack, impulses, state changes) and `ExecutionReport.FirstValidHitTime`. D1's second half (hand the kinematic blade to physics with its own velocity when the rules stop it, D26) needs a world call that releases the held item with a velocity and keeps it in the hand: today a cancelled weapon goes limp and, not being jointed to the hand, falls free.
  - **Session 07:** two kinematic blades report contacts (full kinematic contacts); repeated contacts between the same pair are D19.
  - **Session 08:** touch input must map screen → `ArenaRoot` local → arena → the side's `TurnStartRoot` frame, as `SandboxController.ToTorsoFrame` does.
- **Knockback persistence (Session 04, not in the GDD; for Sessions 05–06):** when a hit knocks a dummy back, does it stay where it was knocked (and start the next turn there), or spring back to where it stood? Today the standing spring (root anchor joint) pulls it back within the turn. §13's "knocked backwards" suggests it should stay.
- Later decisions by session: `Docs/SESSION_PLAN.md` §4, *Later decisions*.
- Optional: a rough SFX set before Session 13 (`Docs/SESSION_PLAN.md` §3).

---

## Tunable values changed from the GDD

| Date | Value | GDD | Now | Why |
|---|---|---|---|---|
| 2026-09-29 | Turn cap | 15 (Appendix A) | 30 | Designer correction; §3 and §13 already say 30 |
| 2026-10-01 | `MatchSettings.AllowReadyCancel` (code default) | TBD (§3) | `true`, plus new `AllowReadyCancelDuringLockout` = `false` | D9 recommended default as a flagged seam: cancel allowed until the lock-out starts. **The existing `Assets/XRim/Data/Tuning/MatchRules.asset` still stores `AllowReadyCancel: 0`**; tick it in the Inspector or tuning panel (never hand-edited). |

---

## Playtest reports

Report format: `Docs/SESSION_PLAN.md` §5.

```
PT1 report, 2026-10-01
Feel rating (1-10): 2
What felt great: Nothing was moving on the bodies, so the sword just extended along the drawn pattern. Not
  expected to create a legendary moment yet; the designer expects that to come in later sessions.
What surprised me (good or bad): Mechanically nothing seems wrong.
Values I changed in the tuning panel: none. Maybe movement could be faster, but not changed for now.
Bugs: none.
Decisions I made: none. D1 and D2 left open: "very beginning of the mechanics, it will improve so much more".
```
Claude's reading: the 2/10 is about the missing body and ragdoll reaction (the spike has a static target and no body moves), not a fault in the swing. Nothing to fix. The weapon-speed remark ("maybe movement can be faster") is a tuning note for Session 04/13, not a change.
