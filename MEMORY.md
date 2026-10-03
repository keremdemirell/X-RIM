# X-RIM Memory

The living memory shared by every Claude Code session. Read it first; update it after every batch.
Keep this file under about 300 lines. When it grows, compress each completed session's detail into one line in the work log.
The GDD is never edited: decisions and deviations are recorded here. The build plan is `Docs/SESSION_PLAN.md`; session prompts are in `Docs/sessions/`.

---

## Current status

- **Session 00 (build planning):** batch 2 of 3 done. Batch 3 (session prompt files 12–22) is still open; it does not block sessions 01–11.
- **Last completed: Session 06 (hits, priority and damage), 2026-10-03**, 5 batches (work-log entry below). **Not committed yet:** the designer commits the whole session (including the six weapon assets changed by `Fill New Tuning Fields`). Before it: Session 05 (`78cee9d`, `038b714`).
- **Next session to start: Session 07**, `Docs/sessions/session-07-clashes-and-shield.md` (decide D19, D20, D21 first). **PT2 follows Session 07.** See *Open questions* → *Wiring Session 06 leaves*.
- PT1 reported 2026-10-01. PT2 is due after Session 07.
- **Git (designer, 2026-10-01): no commits after batches.** The designer commits once when a session is done; each batch summary lists the files it changed. This overrides the commit step in the session prompts. Commit only if truly required, and say so first. Never push; no attribution.
- **Compile-check tool: `python Tools/check.py`** (from the project root; about 6 s, 25 s cold). Run it after every batch; it must print `CHECK PASSED`. Options: `--pass editor|player|dev|all`, `--no-tests`, `--filter TEXT`, `--verbose`. Usage, passes, define lists and limits: `Tools/README.md`.
  - Limits: Windows 64-bit Mono is the player proxy (Android is used automatically once installed); package reference DLLs are Editor builds; PlayMode tests, `[UnityTest]` and tests of Unity-side modules (Config, Input, Presentation, App) are listed as "needs Unity" and must be run in the Editor.
  - Which tests run: those whose namespace names an engine-free module (`XRim.Tests.EditMode.<Module>…`). Keep test namespaces matching their folders.

---

## Completed work log

- **Session 06 (hits, priority and damage), 2026-10-03, complete; not committed yet (the designer commits it).** Plan and A1–A8 approved 2026-10-03 (5 batches). EditMode run by `check.py` 414 → 539 (+19 need Unity); PlayMode 37 → 42. Editor 2026-10-03: EditMode green after batches 1–3; PlayMode 41/42, one test-geometry fix (batch 4), then continued by the designer (42/42 expected; confirm with the session checklist).
  - Batch 5 (sandbox and close): `SandboxReadout` (bottom of the screen: HP bar, limb damage against durability with * for the weapon arm and SEVER at 0, the stun read back from this turn's `PlanningConstraints` or a status / KO reached in playback; during playback HP and limbs are rewound to the playback time from the turn's hits), `MatchEventText` + the Playback tab's "Hits and outcomes" list, a match-over line in the sandbox HUD (to fight again: stop and Play); ARCHITECTURE.md (hits, drivers, TBD register, decisions, status).
  - Batch 4 (PlayMode): `HitPlayModeTests` (5, log "[XRim hits] …"): a short rapier poke to the head (raised 15° guard, 450 apart) interrupts a slow mace swing that lands when unopposed; a chest thrust hits the torso for base damage; a low thrust under the hanging hands hits a leg and adds limb damage; one hit per turn recoils back along the path with the blade still in the hand; a mace hit knocks an unarmed target back about 60, the next idle turn starts there, and with the flag off it springs back. `TurnSimulatorPlayModeTests` repeatability also compares HP. PlayMode 37 → 42. Editor run 2026-10-03: 41/42; the knockback test's mace path ended 4 short of the chest (a held item's box ends flat at its tip; its thickness only widens it sideways), fixed to end 15 past it; rerun pending.
  - Batch 3 (simulation): `HitContactHandler` (new `TurnSimulatorOptions` default: raw `ContactEvent` + weapon-on-other-body → `HitFacts`, one instant at a time to `HitResolver`; applies slow/stop, interrupts, part impulse = mass × path speed × `PartImpulseMomentumFraction` × speed left along the weapon's motion, E1 knockback = mass × `KnockbackUnitsPerWeaponMass` × speed left; records events; fills `FirstValidHitTime`). `ITurnContactHandler.BeginTurn` + `Handle(list)`; `TurnSimulator` holds contacts until final (time < start of the step before the latest; one or two steps), `FighterActions : ITurnActions`, knockback offsets on root and soles (ease-out), `ExecutionReport` filled (`SimClock.StepContaining` for the same-step flag). Drivers: `IWeaponDriver.SlowTo`, `Stop(time, WeaponStopKind)`, `IsTravelling`; `PathWeaponDriver` paces; after a stop a kinematic blade is released once (`HeldItemCommand.Release` → `IPhysicsWorld.ReleaseHeldItem`, last move's velocity × `ReleaseSpeedFraction`) then held by `WeaponMotorControl` (shared PD) at the stop point minus `RecoilDistanceUnits` (last hit). `SimulationSettings.HitReaction` (`HitReactionSettings`). `FeelDiagnostics` and `BodyMovePlayModeTests` use `RecordContactsHandler` (raw physics). Tests `HitContactHandlerTests` (16), `WeaponDriverHitTests` (10), `TurnSimulatorTests` (hold, record-only handler). EditMode 511 → 539.
  - Batch 2 (rules): `HitResolver` (one instant at a time: priority, same-instant trades, D15 via `IInterruptPolicy` → `SettingsInterruptPolicy` (`DamageSettings.InterruptRule`, threshold), D16 `WeaponStats.HasSwingArmour`, a killing hit always cancels, D26 once per part / `MaxHitsPerWeaponPerTurn` / `WeaponStats.SpeedKeptAfterHitFraction` + `FollowThroughDamageModifier`, E2 `RestingWeaponsDealDamage`, stun unless killed, `FirstHitTime`), `TurnAttack` (+ `CrushedThrough` hook for Session 07), `AttackStop`, `InterruptCheck`, `InterruptRule`, `LandedHit`, `HitResolution`. Tests `HitResolverTests` (31), `SettingsInterruptPolicyTests`, `DamageCalculatorTests` +1. EditMode 469 → 511.
  - Batch 1 (rules): `DamageCalculator(IInstantKoPolicy)` (base × zone × `RulePolicies.DamageModifiers`: `OffHandDamageModifier`, `CrushThroughDamageModifier`, `BodyMoveDamageBonusModifier`; `ApplyTo`), `DamageContext` (hit, weapon, settings, attacker body move, crushed through), `DamageResult` (`Damage`, `HpDamage`, `LimbDamage`, `Stuns`, `SeversLimb`), `NoKoFromFullHpPolicy` (D27), `LimbRules` (durability, cap); status effects `TimedStatusEffect` → `NoBodyMoveStatus`, `ShorterPlanningStatus`, `LessInkStatus`, `SettingsStatusEffectFactory` (`DamageSettings.StunEffect`), `IStatusEffect.WithOneTurnUsed`; `PlanningConstraints.ForbidAllBodyMoves`, `Statuses`; `MatchStateMachine` applies then uses up statuses; validation (limb cap below 0.5, stun fractions, durability, base damage). Tests `DamageCalculatorTests`, `LimbRulesTests`, `StatusEffectTests`, `MatchStateMachineTests` +2.

- **Session 05 (body moves), 2026-10-02 to 2026-10-03, complete; committed by the designer (`78cee9d`, `038b714`).** Plan approved 2026-10-02 (5 batches: move maths, turn loop, sandbox, PlayMode tests, close). EditMode run by `check.py` 359 → 414 (+19 need Unity); PlayMode 26 → 37. Editor 2026-10-03: EditMode all green; PlayMode 36/37 twice, each time a test mistake (fixed below), then continued by the designer (37/37 expected; confirm with the session checklist).
  - Data and drivers: `BodyMoveStats` (+ `LeanDegrees`, `StrideUnits`, `FootLiftUnits`, `LeanInPlaceDegrees`, `StandsUpFromLowStance`), a neutral entry in `GddStartingValues`, body-move validation. `IBodyMoveDriver` = `Begin(stats, BodyMoveStart)`, `Evaluate(time)` → `BodyMoveFrame` (root + both foot targets), `IsComplete`. `StanceBodyMoveDriver` plays every swipe from data: an x step that stays; y from standing height (below = a held low stance, above = a hop on two half-parabolas that lands standing with the feet tucked); a lean, positive toward the opponent; feet sliding to a stride; smoothstep eases; D12 lean in place. `NeutralBodyMoveDriver` holds the pose and plants the feet. `StanceBodyMoveDriverFactory` is the `TurnSimulatorOptions` default.
  - Legs: `LegGeometry` (floor y = 0, standing height = leg length, front leg = dominant side, `SoleOf`), `LegPoser` (two-segment leg, knee toward the opponent, the lowest corner of the flat leg end on the target, joint limits; a one-piece leg swings, forward when the hip cannot swing back far enough), `LimbAngles`, `IPhysicsWorld.SetLimbTarget` → per-joint servo targets in `Ragdoll` (flipped like the limits).
  - Turn loop: root kept inside solid edges; both legs posed every step (severed legs skipped, feet kept inside the edges); path frame = the root's position at the turn-start angle (`PathSettings.PathTiltsWithTorsoLean` flag); D13 speed via `WeaponStats.WithSpeedMultiplier`; `BodyMoveStartedEvent` at t = 0; `TurnContactContext.BodyMoves`. Rules: `BodyMoveExtensions.IsBackward()`, `NoMobilityPenaltyPolicy` applied in `MatchStateMachine.PrepareTurn`.
  - Sandbox: ↓ crouch, ↑ jump, →/← lunge or step back (mirrored for the right side), N none, move buttons per side ("lean" with D12's flag), a faint copy of the path at the move's full extent; body-move tuning live from the next Execute (`RecordingTurnSimulator.BodyMovesOverride`, `RulesSettings.WithBodyMoves`). Editor menu `XRim/Setup/Reset Body Moves To Starting Values` (`BodyMoveStartingValuesResetter`).
  - Tests: EditMode `StanceBodyMoveDriverTests`, `NeutralBodyMoveDriverTests`, `LegPoserTests`, `LegGeometryTests`, `TurnSimulatorTests` +10, `MatchStateMachineTests` +2, `RulesSanityTests` +3, `AppSanityTests` +1. PlayMode `BodyMovePlayModeTests` (11): full extent within duration + 0.05 s and settling for every move on both sides, jump peak, crouched head under a standing neck (300), knees toward the opponent, feet over a shin-high sweep (60), lean in place, the path on the move's frame, body lag ≤ 50, the duck-under scenario, six bodies. Each logs "[XRim body moves] …".
  - Fixes after the Editor runs (tests only): the path test's thrust was beyond reach (now 200 long); Unity moves a kinematic body up to one step late, so the tip may be on the path one step earlier (the Session 02 spike measured the same 3.7). Measured lag of the pelvis behind its root: crouch 28.5, jump 36.4, lunge 13.1, step back 2.7, lean 2.2.
- **Session 04 (turn simulation and playback), 2026-10-01 to 2026-10-02, complete; committed by the designer (`f239938`, `d01fe56`).** EditMode run by `check.py` 312 → 359 (+18 need Unity); PlayMode 17 → 26; all green in the Editor 2026-10-02.
  - `TurnSimulator(world, policies, TurnSimulatorOptions)`: frozen board at zero velocity, fixed steps on `SimClock` up to the hard cap. Per step: body-move root target (`IBodyMoveDriverFactory`), weapon driver in that torso frame (frame at both ends of the step), world step, record, then contacts refined (`ImpactTimeRefiner`, d = v·t), ordered (`TurnContactOrder`) and handed to `ITurnContactHandler` (default `RecordContactsHandler` → `ContactEvent`). Ends `Settled` (moves done + `SettleStepsRequired` settled steps) or `HardCap`. `TurnStartRoot.Of(pose)` = frozen pelvis, upright: the turn's start root and the frame paths are drawn in.
  - `Unity2DPhysicsWorld`: a fresh hidden scene per `Load` (a turn repeats exactly), floor at y = 0 and D22 solid edges (`IArenaEdgePolicy` → `ArenaEdges`; root targets kept half a torso inside), held items with full kinematic contacts, severed limbs rebuilt as loose placeholder bodies.
  - Stance persistence and wiring: engine-free `GuardStance` and `StartingBoard` (blades apart en garde: guard −40°, grip at the shoulder), `PoseWeaponTipLocator`; `MatchBootstrap` composes world → `RecordingTurnSimulator` → `LocalTurnAuthority` (ClientReport hold) → `ClientMatchFlow` → `TurnPlayback` on `DummyView`s and `ArenaView`; modes BotVsBot and Sandbox.
  - Debug: Playback tab (speed, pause, frame step, scrub, loop, replay, re-simulate with live tuning, contact list); sandbox (`SandboxPlanSource` per side sends real `PlanningCommand`s; `SandboxController`: mouse drawing in the `TurnStartRoot` frame, weapons 1/2/3, Clear C, Execute Space, Tab switches side, live simulation tuning per turn); F9 feel report (`FeelDiagnostics` → `Logs/XRimFeelReport.txt`).
  - Retired the Session 02 spike (`SwingSimulator`, spike scene and harness); `XRim/Setup/Build Placeholder Dummies` builds into `Prefabs/Dummies`.
- **Session 03 (match loop and planning rules), 2026-10-01, complete.** Headless, engine-free; commits `d5dd47c`, `2c1a1ea`, `63a17f4`, `eb87585`, `7102dc8`. `PlanningSession` (every command, lock-out boundary included, Ready/cancel per D9, timeout executes what is set), `PlanValidator` + `PlanningTiming` (validates the path `PathBuilder` makes), `EndConditionEvaluator` (D11 order; turn cap fires after turn 30, never inside sudden death; `BothForfeitRule`), `SuddenDeathSetup` (HP 1, idle counters reset), `MatchStateMachine` (planning timer starts at `BeginPlanning`; lock on both Ready or each side's own timer; failing plan → idle plan with the session weapon; `CurrentWeapon` set at lock), `LoadoutValidator`, `LocalTurnAuthority` (simulator runs in `Tick`, one turn per call; playback hold modes None / TimelineDuration / ClientReport), `RandomBotBrain` (always valid, never idle). EditMode run here 142 → 312.
- **Session 02 (feel spike), 2026-09-29 to 2026-10-01.** Kinematic and motor weapon drivers, aim from the shoulder, impact-time refinement, contact angle, placeholder ragdoll (6 or 10 bodies) and the full `Unity2DPhysicsWorld`; spike loop, scene and harness retired in Session 04. EditMode 153, PlayMode 17, green in the Editor 2026-10-01. Details: git log.
- **Setup (2026-09-29, `a8f4ff8`, `63c81d4`, `ba7402f`) and Session 00 (build plan, 2026-09-29):** 15 asmdefs, settings and Config SOs, the debug overlay, Editor menus, ARCHITECTURE.md, CLAUDE.md; the 22-session plan and session prompts.
- **Session 01 (tooling and path rules), 2026-09-29.** `Tools/check.py` (Unity's Roslyn; editor, player and dev passes; engine-free guard; NUnit runner on .NET 8; warnings fail). `Rules/Paths/`: `PathResampler`, `LengthInkCostModel`, `RigidityInkCostModel` (§6 as written), `InkCutoff`, D3–D5 policies, `PathBuilder` (stroke → lead-in → resample → reach clamp → ink cut → break cut); `XRim > Setup > Fill New Tuning Fields`. Details: git log.

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
| 2026-10-02 | **D12 (§5 backward: lean or step) not decided; recommended default as a flagged seam:** a real short step back. Lean in place stays switchable. | Designer (approved the default) | `BodyMoveStats.IsLeanInPlace` (Session 05) |
| 2026-10-02 | **D13 (§5 lunge speed or damage) not decided; recommended default as a flagged seam:** reach only (bonuses 0). The flags are wired: speed scales the path speed for that turn; damage is handed to Session 06. | Designer (approved the default) | `BodyMoveStats.WeaponSpeedBonusFraction`, `DamageBonusFraction` (Session 05) |
| 2026-10-02 | **A crouch is a stance (not in the GDD):** it holds to the end of the turn and stays until another body move (lunge, step back or jump stand the dummy up); no body move keeps it low (§3: an idle dummy holds its pose). Flagged seam; "stands up by itself next turn" is switchable. A jump always lands within its duration; lunge and step-back positions carry over (§3). | Designer (approved the recommendation) | Neutral move's `StandsUpFromLowStance` (Session 05) |
| 2026-10-02 | **The path does not tilt with a body move's lean (not in the GDD):** §6 says the path travels with the torso; it moves with the pelvis and keeps the angle it was drawn at, so a lunge carries a level thrust forward level (a 12° lean would dip a 640-unit thrust by about 135). Flagged seam, switchable. | Claude, to confirm by the designer | `PathSettings.PathTiltsWithTorsoLean` (Session 05) |
| 2026-10-02 | **Session 05 architecture approved** (leg joint targets in `IPhysicsWorld`, reshaped `IBodyMoveDriver`, one data-driven move driver, real moves by default, `BodyMoveStartedEvent`, body moves in `TurnContactContext`, mobility default). See *Deviations*. | Designer | Session 05 |

| 2026-10-03 | **D17 (§10/§11 stun effect), D18 (§11 HP scale etc.), D27 (no instant KO): recommended defaults as flagged seams.** D17: a stun means no body move next turn (shorter planning and less ink stay switchable). D18: keep the setup placeholders. D27: a single hit cannot take a dummy from full HP to 0 (leaves 1 HP), every zone; later hits can finish. | Designer | `DamageSettings.StunEffect`, `IInstantKoPolicy` (Session 06) |
| 2026-10-03 | **D15 (§9 which hits interrupt): Decided, only hits on the weapon arm or the head**, so both dummies can be damaged in the same turn. "Any hit" and "above a damage threshold" stay switchable. | Designer | `DamageSettings.InterruptRule` |
| 2026-10-03 | **D16 (§9 swing armour): recommended default, off for every weapon** (not answered explicitly; a toggle per weapon). | Designer (default kept) | `WeaponStats.HasSwingArmour` |
| 2026-10-03 | **D26 (hits per weapon per turn, not in the GDD): Decided, "drag-through".** A weapon carries on after a hit at the speed it keeps (per weapon: a heavy weapon ploughs through, a light one sticks); each later hit deals damage scaled by the speed left; each body part at most once per swing; at most 3 hits, the last one stops it with a recoil; a slowed follow-through can still be interrupted. | Designer (approved Claude's proposal, per weapon) | `DamageSettings.MaxHitsPerWeaponPerTurn`, `WeaponStats.SpeedKeptAfterHitFraction` |
| 2026-10-03 | **E1 knockback (not in the GDD): stays knocked back.** A landed hit pushes the victim's standing point away from the attacker (heavier weapon farther); the next turn starts there. Flag to spring back instead. | Designer (approved the recommendation) | `SimulationSettings.HitReaction` (Session 06) |
| 2026-10-03 | **E2 resting weapons (not in the GDD): harmless.** Only a weapon travelling its path lands hits; a held or idle weapon is an obstacle. Flag to let resting weapons deal damage. | Designer (approved the recommendation) | `DamageSettings` (Session 06) |
| 2026-10-03 | Readings confirmed: hits at exactly the same instant both land (a trade); a killing hit always stops the dead dummy's attack, even through swing armour; the stun compares the hit's damage after modifiers and before the D27 clamp, strictly above the threshold. | Designer | `HitResolver`, `DamageCalculator` (Session 06) |

Other prototype decisions (D7, D12–D14, D16, D19–D21, D23–D25 in SESSION_PLAN.md §4) are **not decided yet** (D12 and D13 run on their flagged defaults since 2026-10-02, D16 since 2026-10-03). Add a row here for each one the designer decides, with the date. Undecided items are built with the recommended default as a flagged `[GddTbd]` seam.

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

- Session 05 (body moves, `GddStartingValues.CreateBodyMoves`): crouch down 70 (was 40), 0.25 s, lean 10° in, stride 40; lunge 120 forward and 20 down, 0.35 s, lean 12° in, stride 140; step back 100 back and 10 down, 0.3 s, lean 8° away, stride 60, lean in place 25° away; jump peak 80, 0.4 s, soles tucked to 100; neutral stand-up 0.3 s (only with `StandsUpFromLowStance`). Not numbers but choices: the front leg is the dominant side's.

- Session 06 batch 3 (`SimulationSettings.HitReaction`): `ReleaseSpeedFraction` 1 (a stopped blade carries all its speed into the hit), `RecoilDistanceUnits` 30, `PartImpulseMomentumFraction` 0.5 (rapier 900, mace 1250 on the struck part), `KnockbackUnitsPerWeaponMass` 6 (rapier 12, mace 60), `KnockbackSeconds` 0.15.
- Session 06: `DamageSettings.StunPlanningDurationFraction` 0.5 and `StunInkLengthFraction` 0.5 (only used if D17 is switched to "shorter planning" or "less ink"); `InterruptDamageThreshold` 10 (only with D15's threshold option); D26 `MaxHitsPerWeaponPerTurn` 3 and `WeaponStats.SpeedKeptAfterHitFraction` rapier 0.3, spear 0.3, sword 0.5, shield 0.5, severed limb 0.5, mace 0.7.

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

- **Session 06 (approved 2026-10-03):**
  - A1 `ITurnContactHandler` gets `BeginTurn(context)` and receives contacts in batches in true time order: `TurnSimulator` holds contacts until no earlier or equally early contact can still be reported (one or two steps, found while building batch 3).
  - A2 `TurnContactContext` gains the policies, simulation settings, each side's attack and actions (stop a weapon, impulse, knockback).
  - A3 `IWeaponDriver.Cancel()` → `Stop(time, kind)`; afterwards a hold motor keeps the weapon in hand; the kinematic driver first hands the blade to physics (`HeldItemCommand.Release`, `IPhysicsWorld.ReleaseHeldItem`).
  - A4 Rules: `HitResolver`, `TurnAttack`, `InterruptCheck`, `IInstantKoPolicy` (D27), `IStatusEffectFactory` (D17), `RulePolicies.DamageModifiers`; `IInterruptPolicy` and `DamageContext` reshaped (no implementations existed).
  - A5 Status effects are immutable and expire after the turn they affect (`MatchStateMachine.PrepareTurn` consumes them); `PlanningConstraints` gains `ForbidAllBodyMoves()` and the statuses active this turn.
  - A6 `SimulationSettings.HitReaction` (release speed, recoil, part impulse, knockback), live in the sandbox. A7 `ExecutionReport` filled. A8 raw `ContactEvent`s stay recorded next to the domain events.

- **Session 05 (approved 2026-10-02):**
  - `IPhysicsWorld.SetLimbTarget(side, limb, LimbAngles)`: joint servos turn toward target angles instead of only the rest pose (reset on `Load`). Engine-free `LegPoser` bends hips and knees so the soles reach their targets (floor, or tucked in a hop); the front leg is the dominant side's.
  - `IBodyMoveDriver` reshaped: `Begin(BodyMoveStats, BodyMoveStart)`, `Evaluate(time)` → `BodyMoveFrame` (root and both sole targets), `IsComplete`. One data-driven `StanceBodyMoveDriver` plays every move; `NeutralBodyMoveDriver` holds the pose. `TurnSimulatorOptions.BodyMoves` defaults to the real moves.
  - `BodyMoveStartedEvent` (Rules.Events) at t = 0 per side from the plan (the §13 backward fact, also a feel hook); `TurnContactContext` exposes each side's body move (D13 damage for Session 06); `NoMobilityPenaltyPolicy` default applied when each turn's limits are built.
  - Found while building batch 2 (small): `TurnContactContext` constructor takes the per-side moves; `WeaponStats.WithSpeedMultiplier`; new flag `PathSettings.PathTiltsWithTorsoLean` (see *Decisions*).
  - Batch 3 (debug only, as planned): `RecordingTurnSimulator.BodyMovesOverride` and `RulesSettings.WithBodyMoves`, so the sandbox plays body moves with the live tuning.

---

## Known issues and tech debt

- Unity 6.5 conventions measured in the Editor (2026-10-01): contact normals point from `ContactPoint2D.collider` to `otherCollider` (confirmed, `ThrustIntoTorso_…` green); `HingeJoint2D.jointAngle` grows **clockwise** for the jointed body relative to its parent (measured −30 for a +30 forward swing), so `Ragdoll.JointAngleGrowsCounterClockwise` is false and limits are flipped when applied.
- Session 04 batch 1: `Assets/XRim/Data/Tuning/Simulation.asset` never stored `WeaponDriver` or `Segmentation`, so it now loads the new code defaults (kinematic, 10 bodies). If the Editor shows 6 bodies, the asset was saved with the old value; switch it in the tuning panel. The Session 02 PlayMode tests build `new SimulationSettings()`, so they now run with 10 bodies by default.
- The spear (placeholder length 500, design TBD) at the −40° guard has its tip below the floor (kinematic, so nothing happens physically). Not in the default loadout; Session 14 (roster) should give long weapons their own guard or length.
- Session 05: the body-move assets were reset to the Session 05 starting values by the designer (2026-10-03, `XRim/Setup/Reset Body Moves To Starting Values`; `None.asset` added to the profile). After a future change to `GddStartingValues.CreateBodyMoves`, run that menu again.
- Session 05: crouching from the low guard (−40°, Session 04) pushes the blade tip about 47 units below the floor (tip 23 above it when standing). The weapon is kinematic, so nothing happens physically, but it is visible. Possible fixes (the designer's call, PT2): a higher guard, or a held weapon that keeps its tip above the floor.
- Session 05 (feel note for PT2): the body trails its move: a crouch's pelvis is up to 28 units behind its target as the descent ends (the knees fold slower than the hips sink), a jump's up to 36 at take-off (the root starts at full upward speed). Every move still reaches its extent within 0.05 s of its duration. If ducks feel late, raise `RagdollSettings.JointServoGainPerSecond` or the root-drive strength, or shorten the crouch.
- Session 05: the two-segment leg is very sensitive near full stretch, so a pelvis frozen a unit or two below standing height bends the knees a few degrees on the next turn (the soles are kept on the floor). Physically right; watch it at PT2.
- Session 04: a dropped weapon disappears from the captured pose, a severed limb's visual stays attached on the `DummyView`, and the off hand never takes over the weapon: all Session 11 (dismemberment).
- PT1 note: in the spike the weapon is not jointed to the body (the arm follows it by a hand spring), so a heavy swing does not drag the dummy. If PT1 wants that comedy, add it later as a tuned reaction/knockback rather than a joint.

- Session 06 (feel notes for PT2): an interrupted weapon is held by the motor where it stopped, so it freezes in the air instead of dropping; the struck part gets both the released blade's momentum (physics) and the extra hit impulse; a knockback slides the feet back with the pelvis. Tune `SimulationSettings.HitReaction` live in the sandbox.
- Session 06 (2D side view): both legs (and both hanging arms) overlap in x, so one thrust can touch both at the same instant; D26 then lands two hits, the second at the slowed share. Watch at PT2 whether that reads well.
- Session 06: the default contact handler is now the hit rules, so `TurnSimulatorPlayModeTests` run with hits (repeatability, reload, idle turn still hold), while `BodyMovePlayModeTests` and the F9 feel report keep raw contacts (`RecordContactsHandler`) to measure geometry and drivers.

- Only the **Editor** Bee response files exist (`Library/Bee/artifacts/1900b0aE.dag/*.rsp`). `Tools/check.py` derives the player passes from them. The player-only define list (DEBUG, TRACE, ENABLE_PROFILER and UNITY_ASSERTIONS only in dev players) is derived, not read from a real player build; confirm it once a real build exists (Session 10).
- Only the **Windows standalone** playback engine is installed at `S:/Unity_Editor/Editor/Data/PlaybackEngines/`. Android Build Support is needed by Session 10. iOS builds need a Mac.
- Unity is installed at `S:/Unity_Editor/Editor`, not the Hub default path.
- Unity 6.5: `Object.GetInstanceID()` is a compile error.
- The GDD Appendix A still says 15 turns (see Decisions).

---

## Open questions for the designer

- Prototype decisions D1–D27 in `Docs/SESSION_PLAN.md` §4 (D3, D4, D5 decided 2026-09-29; D1, D2, D22 decided 2026-10-01; D12 and D13 on flagged defaults since 2026-10-02; D15 and D26 decided, D16, D17, D18, D27 on flagged defaults since 2026-10-03). Still open for Session 07: D19, D20, D21.
- Session 03: D6, D8, D9, D10, D11 are flagged seams with the recommended defaults (see *Decisions*); confirm or change them at PT3. Also confirm: idle counters reset when sudden death starts, and the both-forfeit pick (sudden death) once a real match shows how it feels.
- Wiring left for later sessions: **Session 09** must call `ITurnAuthority.NotifyPlaybackFinished(turnIndex)` from `ClientMatchFlow.OnPlaybackFinished` and choose the hold mode (`ClientReport` for a client, `None` for headless); **Session 06** owns status lifetime (the machine only calls `ApplyToNextTurn`); **Session 12** replaces the marked sudden-death seam in `EndConditionEvaluator` (both hit, nobody hits) with `ITiePolicy` / `ISuddenDeathNoHitPolicy`, adds `SuddenDeathSettings.BoardMode`, the electric-wall reaction at lock (`ElectricWallRules.OnPlansLocked`) and the sudden-death option flags (switching, signatures) as constraints; **Session 15** replaces the always-refused `UseSignature`; **Session 19** rebuilds the path from the raw stroke before validating and stamps the audit times from the server clock.
- **Wiring Session 04 left (2026-10-02):** **Session 07:** two kinematic blades report contacts (full kinematic contacts); repeated contacts between the same pair are D19. **Session 08:** touch input must map screen → `ArenaRoot` local → arena → the side's `TurnStartRoot` frame, as `SandboxController.ToTorsoFrame` does.
- **Wiring Session 06 leaves (2026-10-03):**
  - **Session 07:** weapon-to-weapon and shield contacts arrive at `HitContactHandler` today as raw `ContactEvent`s only; extend it (or wrap it) so each instant resolves clashes and blocks with the hits, in time order. A crush-through sets `TurnAttack.CrushedThrough` (the −30% modifier exists); knocking a weapon off its path needs a `HitResolver` call that stops an attack (add an `AttackStop` reason) and `ITurnActions.StopWeapon`; a stagger uses `IStatusEffectFactory` with `StatusKind.Staggered` (same effect as the stun); shield reduction is a new `IDamageModifier` in `RulePolicies.DamageModifiers`.
  - **Session 11:** `DamageResult.SeversLimb` is only reported; break the joint by logic, `MarkSevered`, record `LimbSeveredEvent`, drop the weapon when the dominant arm goes (`HitFacts.IsOffHand` and the off-hand modifier already work from `FighterState.HasDominantArm`).
  - **Session 12:** `ExecutionReport.FirstValidHitTime` and `FirstHitsOnSameStep` are filled. In sudden death every hit kills, and a killing hit cancels the other attack, so "both hit" only happens for hits at exactly the same instant.
  - **Session 13:** feel hooks on `HitLandedEvent` (damage, zone, stun), `AttackInterruptedEvent`, `FighterDiedEvent` (the torso split); `SimulationSettings.HitReaction` for knockback feel.
- **Session 05 to confirm at PT2:** the path does not tilt with a body move's lean (`PathSettings.PathTiltsWithTorsoLean`, Claude's pick); the crouch is a stance (designer's pick, flagged); the low guard's blade dips through the floor in a crouch (see *Known issues*).
- **Wiring Session 05 leaves (2026-10-02):** (Session 06 applies the D13 damage bonus: done.) **Session 11** fills `RulePolicies.MobilityPenalty` (today `NoMobilityPenaltyPolicy`); the interface only edits planning limits, so a "shorter steps" option would also need the simulation to scale the move. **Session 12** reads the backward fact from `LockedPlans[side].BodyMove.IsBackward()` at lock (rules) or `BodyMoveStartedEvent.IsBackward` in the turn result (clients). **Session 13** can hook effort sounds and camera on `BodyMoveStartedEvent`.
- **Session 06 to confirm at PT2:** the drag-through numbers (speed kept per weapon, 3 hits), the interrupt rule (D15 weapon arm or head) and swing armour on the mace (D16), the hit reactions (an interrupted weapon freezes in the air; a struck part gets both the blade's momentum and the extra kick), overlapping legs taking two hits from one thrust.
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
