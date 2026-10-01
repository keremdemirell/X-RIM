# X-RIM Memory

The living memory shared by every Claude Code session. Read it first; update it after every batch.
Keep this file under about 300 lines. When it grows, compress each completed session's detail into one line in the work log.
The GDD is never edited: decisions and deviations are recorded here. The build plan is `Docs/SESSION_PLAN.md`; session prompts are in `Docs/sessions/`.

---

## Current status

- **Session 00 (build planning):** batch 2 of 3 done. Batch 3 (session prompt files 12–22) is still open; it does not block sessions 01–11.
- **Last completed: Session 02 (feel spike), 2026-10-01**, 6 batches + 3 fixes (commits `35617c5`, `bda68ce`, `6bce333`, `bdd001f`, `6eda8c8`, `76bc780`, `98f0a31`, `a35f92d`, `ca3efb3`, plus the closing memory commit).
- **PT1 reported 2026-10-01** (see *Playtest reports*): feel 2/10 (expected: no body moves yet), no values changed, no bugs, **D1 and D2 not decided** ("too early"). Session 04 needs them: until the designer decides, build D1 (kinematic swing, hand over to physics when the rules stop the weapon) and D2 (10 bodies) as flagged seams with the spike recommendations.
- **Session 03 (match loop and planning rules): IN PROGRESS, started 2026-10-01.** Plan approved 2026-10-01, five batches:
  1. **Planning commands:** `PlanningSession` (all commands, timeout, public state), `PlanningAudit` (switch and Ready times), default `IPublicStatePolicy` and `IIdleTurnPolicy`, D6/D9 flags, `RulePolicies` wiring; tests in `Tests/EditMode/Rules/Planning/`.
  2. **Plan validation:** `PlanValidator` (loadout, constraints, ink, path validity, switch and Ready timing); tests.
  3. **End conditions:** `EndConditionEvaluator` (KO, double KO, forfeit, turn cap, D11 order), sudden-death setup seam; tests.
  4. **Match state machine:** `MatchStateMachine` (all phases, `Tick`, `Apply`, `CompleteExecution`, idle counters); tests with `ManualClock`.
  5. **Authority and bots:** `LocalTurnAuthority`, playback hold hook, `IWeaponTipLocator`, `RandomBotBrain` and `BotPlanSource` emit valid plans; headless bot-vs-bot tests (scripted KO, stalled match to sudden-death setup).
  - **Done: batch 1 (`d5dd47c`), batch 2 (`2c1a1ea`), batch 3 (`63a17f4`) and batch 4 (2026-10-01). Next: batch 5 (authority and bots).**
- **Next session after 03:** Session 04, `Docs/sessions/session-04-turn-simulation-and-playback.md` (needs D1, D2 and D22).
- **Compile-check tool: `python Tools/check.py`** (from the project root; about 6 s, 25 s cold). Run it after every batch; it must print `CHECK PASSED`. Options: `--pass editor|player|dev|all`, `--no-tests`, `--filter TEXT`, `--verbose`. Usage, passes, define lists and limits: `Tools/README.md`.
  - Limits: Windows 64-bit Mono is the player proxy (Android is used automatically once installed); package reference DLLs are Editor builds; PlayMode tests, `[UnityTest]` and tests of Unity-side modules (Config, Input, Presentation, App) are listed as "needs Unity" and must be run in the Editor.
  - Which tests run: those whose namespace names an engine-free module (`XRim.Tests.EditMode.<Module>…`). Keep test namespaces matching their folders.

---

## Completed work log

- **Session 03 (match loop and planning rules), in progress, started 2026-10-01.**
  - **Batch 1, planning commands** (`Rules/Planning/`, `Rules/Settings/MatchSettings.cs`, `Rules/RulePolicies.cs`):
    - `PlanningSession` implements every command. Weapon switch: only from the loadout, erases the path, refused from `deadline − WeaponSwitchLockoutSeconds` on (boundary included: exactly 1.5 s left is already locked), public at once; selecting the weapon already held is not a switch. Body move: per `PlanningConstraints`. Path: runs `PathBuilder` (lead-in from the weapon tip, reach, ink cut-off, break cut), so over-budget strokes are cut, not refused; a stroke with no points or no movement is `InvalidPath`; a zero ink budget is `InkBudgetExceeded`; `InkLengthMultiplier` scales the budget through the new `WeaponStats.WithInkLengthMultiplier`. `UseSignature` is always `SignatureUnavailable` until Session 15. Ready: idempotent; cancel per D9 flags; while Ready every plan command is refused with the new `CommandRejection.AlreadyReady`. After the deadline every command is `NotInPlanningPhase`, and `CurrentPlan` still returns whatever was set (timeout executes it).
    - New: `PlanningAudit` (when the weapon, path and Ready last changed, for the validator), `IWeaponTipSource`, `DefaultPublicStatePolicy`, `AnyMoveIdleTurnPolicy` (D10), both wired as `RulePolicies` defaults; `PublicPlanningState` is now `IEquatable` (the authority compares it to raise `PublicStateChanged`).
    - Tests: `Tests/EditMode/Rules/Planning/` (52): lock-out edge (just before, exactly at, inside, tunable length), erase on switch, every body-move/path/ink/Ready/cancel/timeout rule, secrecy of the public state, audit, policies. EditMode run here 142 → 194.
  - **Batch 2, plan validation** (`Rules/Planning/PlanValidator.cs`, `PlanningTiming.cs`):
    - `PlanValidator(settings, policies)` with `Validate(plan, audit, loadout, constraints)` and `Validate(session)`. Checks in order: weapon in the loadout and has stats; timing from the `PlanningAudit` (anything at or after the deadline is `NotInPlanningPhase`; a weapon switch in the lock-out is `WeaponSwitchLockedOut`; drawing in the lock-out is `DrawingLockedOut` only when the flag locks drawing); body move per constraints; no signature move (Session 15); path: ink cost (through `IInkCostModel`, scaled by `InkLengthMultiplier`) within the weapon's budget, no surviving too-sharp turn, every point inside the reach limit. An idle plan is valid.
    - `PlanningTiming` holds the deadline and lock-out boundary, used by both the session and the validator; a test proves they agree at every boundary.
    - The validator checks the path that executes (as `PathBuilder` makes it). Session 19's remote authority must rebuild the path from the raw stroke with `PathBuilder` before validating, and stamp the audit times from the server clock.
    - Tests: `PlanValidatorTests` (19): over-budget plan, late switch, outside the loadout, tunable lock-out, drawing flag, Ready and deadline, rigidity break, reach, body move, signature, session plan valid. EditMode run here 194 → 213. Also committed: the `.meta` files Unity made for batch 1.
  - **Batch 3, end conditions** (`Rules/Match/EndConditionEvaluator.cs`, `BothForfeitRule.cs`, `Rules/SuddenDeath/SuddenDeathSetup.cs`, `MatchSettings`):
    - `EndConditionEvaluator.Evaluate(report, settings)` is a pure read of `report.ResolvedState`. Normal turn, D11 order: double KO → `EnterSuddenDeath`; single KO → `MatchOver` (`KnockOut`, survivor wins); forfeit (idle counter ≥ `RuleConstants.IdleTurnsBeforeForfeit`; one side → `MatchOver` `Forfeit`); turn cap → `EnterSuddenDeath`. `MatchOutcome.TurnCount` = `TurnIndex + 1`. **The machine must update `ConsecutiveIdleTurns` (through `IIdleTurnPolicy`) before calling the evaluator** (batch 4).
    - Both sides forfeiting in one turn: new flag `MatchSettings.BothForfeitRule` (`SuddenDeath`, the designer's pick, or `HigherHpWins`, equal HP still goes to sudden death).
    - Turn cap counts turns played: it fires after turn 30 resolves (`TurnIndex` 29). It never fires inside sudden death.
    - Inside sudden death (the Session 12 seam, marked in code): one dead → `MatchOver` `SuddenDeathHit`; an idle side still forfeits; both dead, nobody dead or both forfeiting → `RepeatSuddenDeathTurn`. Session 12 replaces the both-hit and no-hit cases with `ITiePolicy` (earlier `SimTime` wins) and `ISuddenDeathNoHitPolicy`.
    - `SuddenDeathSetup.Apply(state)`: `IsSuddenDeath = true`, both HP = `RuleConstants.SuddenDeathHp`, idle counters reset (not in the GDD; otherwise two forfeiting sides would forfeit again at once). The board always carries over; `SuddenDeathSettings.BoardMode` is Session 12. Needed now because a double KO leaves both at 0 HP and would re-trigger forever.
    - Tests: `Tests/EditMode/Rules/Match/` (28): each condition, both-forfeit options, tunable cap, the four order cases (KO beats forfeit and cap, double KO beats forfeit, forfeit beats cap), sudden-death edges, setup. EditMode run here 213 → 241.
  - **Batch 4, match state machine** (`Rules/Match/MatchStateMachine.cs`, `LoadoutValidator.cs`):
    - API: `Start()` (MatchSetup → TurnStart; validates both loadouts, throws `InvalidOperationException` on a bad one), `BeginPlanning(PerSide<IWeaponTipSource>)` (TurnStart or SuddenDeathSetup → Planning; opens the sessions and **starts the planning timer at this call**), `Tick()`, `Apply(side, command)` (timed by the machine's clock), `PublicStateOf(side)`, `BeginExecution()` (Locked → Executing, returns the plans), `CompleteExecution(report)` (→ TurnStart, SuddenDeathSetup or MatchOver, returns the `EndCheckResult`). Read-only state: `Phase`, `State` (now `{ get; private set; }`, replaced by a copy of the resolved state), `Sessions`, `Constraints`, `PlanningDeadlineSeconds`, `LockedPlans`, `LockValidation`, `Outcome`. Wrong-phase calls throw; late commands are `NotInPlanningPhase`.
    - Planning locks when each side is Ready **or its own timer has ended** (a stun-shortened side waits for the other). The second Ready locks at once. At lock: every plan is validated; a failing plan runs as an idle plan with the session's weapon (kept because the switch was public); each side's `CurrentWeapon` becomes the locked weapon.
    - The next planning timer starts only at `BeginPlanning`, so the authority holds in TurnStart (or SuddenDeathSetup) until the client's playback is done (batch 5 hook). **Clarification of the ARCHITECTURE diagram:** Resolving happens inside `CompleteExecution` (never rests); SuddenDeathSetup rests like TurnStart, with the setup already applied and the turn counter advanced, and goes straight to Planning; Session 12 adds the board reset there.
    - Per-turn constraints are built at turn start: default plus each fighter's `IStatusEffect.ApplyToNextTurn` (stun and stagger arrive in Session 6, which also owns status lifetime). Electric-wall reaction at lock is a marked Session 12 hook.
    - `CompleteExecution` rejects a report for another turn (`ArgumentException`), updates the idle counters through `IIdleTurnPolicy`, then runs `EndConditionEvaluator`. A repeated sudden-death turn goes back to TurnStart with the turn index advanced.
    - `LoadoutValidator` (§6, D8): exactly 3 slots, known weapons, no duplicates, a shield only if `RequireShieldSlot`. Severed limbs are not picked slots.
    - Tests: `MatchStateMachineTests` (30) and `LoadoutValidatorTests` (6): phase order, timers and timeout, one-Ready plus timeout, shorter timer, command routing and clock, lock-out through the machine, validation fallback, status effects, idle counting and forfeit, KO, double KO, sudden-death hit and repeat, a stalled 30-turn match to SuddenDeathSetup. EditMode run here 241 → 277.
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

Other prototype decisions (D7, D12–D27 in SESSION_PLAN.md §4) are **not decided yet**. Add a row here for each one the designer decides, with the date. Undecided items are built with the recommended default as a flagged `[GddTbd]` seam.

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
  - Weapon-tip seam, refined in batch 1: `IWeaponTipSource` lives in `Rules.Planning` (the session needs it and Rules cannot see boards or poses). The authority builds one per side per turn; batch 5 adds its default, derived from the Session 02 guard-stance numbers and tagged `[Placeholder]`. Session 04 replaces it with the tip read from the frozen pose. Also new in batch 1: `CommandRejection.AlreadyReady` (appended to the enum) and `PlanningAudit`.
  - `ITurnAuthority` gains `NotifyPlaybackFinished(int turnIndex)`. `LocalTurnAuthority` playback hold modes: none (headless), timeline duration, client report (with a placeholder timeout).
  - Assumptions built and flagged: the turn cap fires after turn 30 resolves and never re-triggers inside sudden death; sudden-death setup sets both HP to `RuleConstants.SuddenDeathHp` (otherwise a double KO re-triggers forever), while board carry-over and the first-hit and tie rules stay a flagged Session 12 seam; a locked plan that fails validation executes as an empty plan with the side's current weapon.

---

## Known issues and tech debt

- Unity 6.5 conventions measured in the Editor (2026-10-01): contact normals point from `ContactPoint2D.collider` to `otherCollider` (confirmed, `ThrustIntoTorso_…` green); `HingeJoint2D.jointAngle` grows **clockwise** for the jointed body relative to its parent (measured −30 for a +30 forward swing), so `Ragdoll.JointAngleGrowsCounterClockwise` is false and limits are flipped when applied.
- PT1 note: in the spike the weapon is not jointed to the body (the arm follows it by a hand spring), so a heavy swing does not drag the dummy. If PT1 wants that comedy, add it later as a tuned reaction/knockback rather than a joint.

- Only the **Editor** Bee response files exist (`Library/Bee/artifacts/1900b0aE.dag/*.rsp`). `Tools/check.py` derives the player passes from them. The player-only define list (DEBUG, TRACE, ENABLE_PROFILER and UNITY_ASSERTIONS only in dev players) is derived, not read from a real player build; confirm it once a real build exists (Session 10).
- Only the **Windows standalone** playback engine is installed at `S:/Unity_Editor/Editor/Data/PlaybackEngines/`. Android Build Support is needed by Session 10. iOS builds need a Mac.
- Unity is installed at `S:/Unity_Editor/Editor`, not the Hub default path.
- Unity 6.5: `Object.GetInstanceID()` is a compile error.
- The GDD Appendix A still says 15 turns (see Decisions).

---

## Open questions for the designer

- Prototype decisions D1–D27 in `Docs/SESSION_PLAN.md` §4 (D3, D4, D5 decided 2026-09-29). **D1 and D2 were left open at PT1** (2026-10-01); recommendations are in *Session 02 spike findings*, and Session 04 builds them as flagged seams until decided. D26 (hits per weapon per turn) and D27 (how the no-instant-KO head rule is enforced) are gaps the GDD does not cover.
- Session 03: D6, D8, D9, D10, D11 are flagged seams with the recommended defaults (see *Decisions*); confirm or change them at PT3.
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
