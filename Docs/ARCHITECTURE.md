# X-RIM Architecture

This is the approved architecture as built on 2026-09-29. The Game Design Document (`Docs/X-RIM — Game Design Document.pdf`) is the source of truth for rules. This file covers how the code is organised to serve it. Section numbers like §9 refer to the GDD.

Unity 6000.5.2f1 (Unity 6.5), URP 17.6 with the 2D Renderer, Input System only, Unity Test Framework 1.7.

---

## 1. Central decision: a turn is a pure function

```
TurnResult = Resolve(BoardState, TurnPlan left, TurnPlan right, RulesSettings)
```

Stance persistence (§3) makes this possible: every turn starts from a frozen board with zero velocity, so the board, the two plans and the settings are everything a turn depends on.

Execution is **simulated up front, then played back**:

1. The simulator resolves the whole turn in a hidden physics scene, faster than real time.
2. It records a `TurnTimeline`: body poses per step plus time-stamped domain events (hit, clash, block, sever, wall zap, death).
3. The screen only plays that recording.

Consequences:

- **Local and server authority make the same call.** A client always plays back a `TurnResult` and never knows or cares who produced it.
- **Replay is free.** Slow motion, scrubbing, frame-step, "re-simulate the last turn with new tuning" and legendary-moment replays need no extra simulation work.
- **Feel effects cannot change outcomes.** Hitstop, slow motion on a sever and camera punch only bend playback, so they can be pushed hard (pillar 1) without affecting fairness or sync.
- **A saved (board, plans) pair is a reproducible scenario**, usable as a regression test, a feel fixture or input to a bot-vs-bot balance batch.

---

## 2. Layers and assemblies

```
Layer 4   App (composition root)    DebugTools (Editor + dev builds)    Editor (Editor only)
Layer 3   Input    Presentation    Simulation.Unity2D    Config                  ← Unity code
          ─────────────────────── noEngineReferences: true below ───────────────────────
Layer 2   Networking    Bots    Economy
Layer 1   Simulation   (execution loop + physics port)
Layer 0   Rules  →  Core
```

References only point downward. Assemblies below the line set `noEngineReferences: true`, so the compiler rejects `using UnityEngine` there. They can later be built outside Unity, for a server or for CI.

| Assembly | Engine | Contents | References |
|---|---|---|---|
| `XRim.Core` | no | `Vec2`, `XMath`, `Side`, `PerSide<T>`, `SimTime` (integer µs), `IClock`/`ManualClock`, `IRandom`/`XorShiftRandom`, `Guard`, `[GddTbd]`, `[Placeholder]` | – |
| `XRim.Rules` | no | Match state machine, planning session, paths and ink, clash, damage, limbs, status effects, electric wall, sudden death, domain events, `RulePolicies` (every TBD seam), `*Settings` + `GddStartingValues` | Core |
| `XRim.Economy` | no | `EarnedAmount` and `PremiumAmount` as separate types, `Wallet` with no conversion API, `GameplayUnlock` (earned price only), cosmetics, reward and farming seams | Core, Rules (reads `MatchSummary` only) |
| `XRim.Simulation` | no | `IPhysicsWorld` port, `SimClock`, `TurnSimulator` (+ `ITurnContactHandler` seam), weapon drivers, body-move drivers (`StanceBodyMoveDriver`, `NeutralBodyMoveDriver`), `LegPoser`, `TimelineRecorder`, `TurnResult`, `BoardSnapshot`, `PoseSnapshot`, `GuardStance`, `StartingBoard` | Core, Rules |
| `XRim.Networking` | no | `ITurnAuthority`, `LocalTurnAuthority`, `IPlanSource`, `PlanSourceBinder`, `PlanningWindow` | Core, Rules, Simulation |
| `XRim.Bots` | no | `IBotBrain`, `RandomBotBrain` (debug), `BotPlanSource` | Core, Rules, Simulation, Networking |
| `XRim.Config` | yes | Every ScriptableObject, `TuningProfile`, `ArenaSpace`, `TunableFields` | Core, Rules, Simulation, Economy |
| `XRim.Simulation.Unity2D` | yes | `Unity2DPhysicsWorld` (hidden, manually stepped scene), `PhysicsBodyTag`, `Ragdoll` | Core, Rules, Simulation, Config |
| `XRim.Input` | yes | `ScreenLayout` (zones and mirroring), handedness policy, swipe classifier, screen-to-arena mapper, `IInputScheme`, `TouchPlanSource`, `TouchInputReader` | Core, Rules, Config, Networking, Unity.InputSystem |
| `XRim.Presentation` | yes | `TimelinePlayer`, `TurnPlayback`, `DummyView`, `ArenaView`, camera director, `FeelDirector`, audio, VFX, moment detector, `PlanningHud` | Core, Rules, Simulation, Config |
| `XRim.App` | yes | `MatchBootstrap` (composes the whole local loop), `ClientMatchFlow`, `PlanSourceFactory`, `HotSeatCoordinator`, `MatchMode`, `VisualDummyFactory`, `RecordingTurnSimulator` | all runtime |
| `XRim.DebugTools` | yes | `DebugOverlay` (Tuning, Playback, Cheats tabs), `TuningPanel`, `PlaybackPanel`, the sandbox (`SandboxController`, `SandboxPlanSource`), `FeelDiagnostics`, gizmos, `ScenarioStore`. Compiled only when `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`; installs itself at runtime, so scenes never reference it | all runtime |
| `XRim.Editor` | Editor | Setup menus (incl. the placeholder dummy prefab builder and the sandbox scene), reports, `SettingsConfigEditor` | all runtime, and DebugTools |
| `XRim.Tests.EditMode` | Editor | Sanity and GDD-conformance tests, fakes | pure modules, Config, Input, Presentation, App |
| `XRim.Tests.PlayMode` | yes | Unity 2D physics backend tests | Core, Rules, Config, Simulation, Simulation.Unity2D |

Rules this graph enforces:

- **Rules never see physics, input, presentation, networking or economy.** Combat code cannot reach money or cosmetics (pillar 5).
- **Cosmetics cannot touch combat.** Skins live only in Presentation and Economy. Hitboxes live on the physics ragdoll.
- **Finger speed cannot leak in.** `TurnPlan` paths hold positions, never timestamps (§4 input fairness).
- **App cannot reference DebugTools.** DebugTools does not exist in release builds.

### How the layers split a turn's execution

```
IPhysicsWorld.Step → raw contact facts (who, zone, normal, relative velocity) + path distance
  → Rules: hit / clash / shield block / wall resolution (pure decisions)
  → outcome: damage, interrupt, crush / rebound / deflect / slide, sever
  → Simulation applies it (knock off path, stagger, break joint by logic) and records the event
```

---

## 3. Folder structure

```
Assets/XRim/
  Scripts/<Module>/        one folder and one .asmdef per module above
    Rules/                 Match/ Planning/ Paths/ Combat/ Damage/ Limbs/ Status/ Arena/ SuddenDeath/ Signature/ Events/ Settings/
    Simulation/            Physics/ Drivers/ Recording/ Execution/
    Presentation/          Playback/ Dummy/ Arena/ Cameras/ Feel/ Audio/ Vfx/ Moments/ UI/
  Tests/EditMode/<Module>/ Tests/PlayMode/<Module>/
  Data/                    created by XRim/Setup/Create Default Tuning Assets: Tuning/ Weapons/ BodyMoves/
  Scenes/                  created by XRim/Setup/Create Sandbox Scene
  Prefabs/Dummies/         created by XRim/Setup/Build Placeholder Dummies (6- and 10-body ragdolls)
  Art/ Audio/ UI/          created when first needed
Assets/Settings/           existing URP assets (unchanged)
```

Namespaces follow folders (`XRim.Rules.Combat`). Folder names avoid Unity type names: `Cameras`, not `Camera`, and `DebugTools`, not `Debug`.

---

## 4. Data and config

- **One source of truth per value.** Each engine-free `*Settings` class in Rules, Simulation or Economy holds its GDD starting values as field initializers. A ScriptableObject in `XRim.Config` (`SettingsConfig<T>`) wraps it for editing and saving. This refines the approved plan: assets no longer copy fields into a separate snapshot class.
- **Snapshots.** `TuningProfile.BuildRulesSettings()` deep-copies every asset through Unity's serializer at match start. Live edits therefore apply to the next match or re-simulation, never mid-turn. A server can load the same snapshot as JSON.
- **Decided, non-tunable numbers are code constants** in `RuleConstants`: forfeit after 3 idle turns, sudden-death HP 1, 3 loadout slots.
- **Values the GDD leaves open get a placeholder** so the prototype runs:
  - They are tagged `[Placeholder("reason")]`.
  - The inspector and the in-game tuning panel tint them orange.
  - **XRim → Reports → Placeholder Values** lists them all.
  - Weapons whose design is TBD set `WeaponStats.DesignIsTbd`.
- **Decided orderings are enforced** by `RulesSettings.Validate` and by tests: rapier speed above mace, rapier base damage below mace, W_m ≠ W_v.
- **`TuningProfile` is the root.** Swap profiles to A/B test feel.
- **Client-only assets** (`InputConfig`, `ArenaSpaceConfig`, `FeelConfig`, `BalanceTargetsConfig`) never affect outcomes.

### Where each Appendix A value lives

| Appendix A | Start value | Home |
|---|---|---|
| Planning phase | 10–12 s | `MatchSettings.PlanningDurationSeconds` = 12 |
| Idle turns before forfeit | 3 (Decided) | `RuleConstants.IdleTurnsBeforeForfeit` |
| Turn cap | 30 (see §13 of this doc) | `MatchSettings.TurnCap` |
| Execution hard cap | 1.5 s | `MatchSettings.ExecutionHardCapSeconds` |
| Body zone width | ~15% | `InputConfig._bodyZoneWidthFraction` |
| Stance swipe input time | ~0.3 s | `InputConfig._swipeWindowSeconds` |
| Rapier / Mace ink | 600×10 / 200×30 | `WeaponStats.InkLengthUnits`, `InkThicknessUnits` via `GddStartingValues` |
| Rapier / Mace mass | 2 / 10 | `WeaponStats.Mass` |
| Spear bend threshold, k | 15°, open | `RigiditySettings.BendThresholdDegrees`, `BendCostK` (placeholder) |
| Weapon switch lock-out | 1.5 s | `MatchSettings.WeaponSwitchLockoutSeconds` |
| Clash angle threshold | 30° | `ClashSettings.HardClashAngleDegrees` |
| Crush ratio | 1.5 | `ClashSettings.CrushRatio` |
| Crush-through damage | −30% | `ClashSettings.CrushThroughDamageMultiplier` = 0.7 |
| W_m, W_v | unequal, open | `ClashSettings.MassWeight`, `SpeedWeight` (placeholders) |
| Head / torso / arm / leg | 2.5 / 1.0 / 0.8 / 0.7 | `HitZoneSettings` |
| Per-hit limb cap | 35% | `DamageSettings.PerHitLimbCapFraction` |
| Dismemberment target | 10–20% of matches | `BalanceTargetsConfig` (telemetry and bot batches only) |
| Off-hand damage | 80% | `DamageSettings.OffHandDamageMultiplier` |
| Sudden-death HP | 1 (Decided) | `RuleConstants.SuddenDeathHp` |

### Placeholder numbers chosen during setup

These are not GDD values; every one is tagged `[Placeholder]`.

- HP 100; arm/leg durability 40/50; head stun threshold 30.
- Speeds in arena units/s: rapier 900, sword 600, spear 700, mace 250, shield 400, severed limb 300. At these speeds a full rapier path lands within the 1.5 s cap.
- Base damage: rapier 8, sword 12, spear 10, mace 20, shield 3, limb 10.
- Clash weights 1.0 (mass) / 0.005 (speed), so the mace still crushes the rapier. Glancing mass band 20%.
- Sword, spear, shield and limb ink and mass.
- Body-move displacements, 0.4 s move duration (reshaped in Session 05: see `MEMORY.md` → *Placeholder values*).
- Wall: damage 10, bounce 5, advance 50, spawn offset 60.
- Arena width 2000, starting gap 700, path sample spacing 10.
- Added in Session 01: arm length 240, shoulder at (0, 100) in the torso frame, weapon lengths (rapier 400, sword 320, spear 500, mace 220, shield 150, severed limb 200), sharp-turn window 20.
- 0.01 world units per arena unit.

---

## 5. Match and turn flow

The authoritative state machine is `XRim.Rules.Match.MatchStateMachine`. It is pure C#, advanced by `Tick()` with an injected `IClock`, so tests control time and a server can host it unchanged.

```
MatchSetup → TurnStart → Planning ──(both Ready || now ≥ deadline)──→ Locked → Executing → Resolving
 (loadouts                  │                                                                │
  revealed)                 └─ one PlanningSession per side                                  ├→ TurnStart (next turn)
                                                                                             ├→ SuddenDeathSetup → TurnStart
                                                                                             └→ MatchOver (KO | Forfeit | SuddenDeathHit)
```

- **Planning.** `PlanningSession` enforces the rules for every command:
  - Weapon switch: erases the path, is refused in the final lock-out window, and is shown to the opponent immediately (§6).
  - Ink budget (§6).
  - Ready, with an optional cancel flag (§3).
  - On timeout, whatever is set executes.
- **Opponent sees Ready.** Decided by the designer on 2026-09-29; not yet in the GDD.
- **Locked.** Plans are frozen and validated by `PlanValidator` on the authority's clock (§18).
- **Resolving.** `EndConditionEvaluator` is the single place that checks KO, double KO (→ sudden death), forfeit and turn cap (→ sudden death).
- **Sudden death.** HP is set to 1. The first valid hit wins; if both land, the earlier `SimTime` wins. The no-hit and same-step-tie cases go through policies.
- **Client flow (`XRim.App.ClientMatchFlow`).** Idle → Planning → WaitingForAuthority → Playback → … → MatchOver. It only mirrors the authority. It holds the next planning phase and the match end until playback has finished.
  - The authority starts the next planning timer only after playback (Session 03): `MatchStateMachine.BeginPlanning` is what starts the timer, and `LocalTurnAuthority` holds in TurnStart until its `PlaybackHoldSettings` mode says playback is over (none, the recorded timeline duration, or the client's `NotifyPlaybackFinished` with a timeout). SuddenDeathSetup is a resting phase like TurnStart; Resolving happens inside `CompleteExecution`.
- **Composition (`XRim.App.MatchBootstrap`, Session 04).** Hidden `Unity2DPhysicsWorld` → `TurnSimulator` (wrapped by `RecordingTurnSimulator` for debug re-simulation) → `LocalTurnAuthority` (hold mode `ClientReport`, a `ManualClock` advanced by unscaled time) → `ClientMatchFlow` → `TurnPlayback` on two `DummyView`s. When playback finishes, the bootstrap tells the flow and calls `NotifyPlaybackFinished`. Modes now: `BotVsBot`, and `Sandbox`, where the planning timer is frozen and DebugTools hands its plan sources to `MatchBootstrap.StartMatch` (App never references DebugTools). Touch input is Session 08, hot-seat Session 09.
- **Starting board and weapon tips.** A match starts on `StartingBoard` (engine-free): both dummies in `GuardStance`, `StartingGapUnits` apart, mirrored around x = 0, blades apart en garde. Each turn's weapon tips come from the frozen pose (`PoseWeaponTipLocator`), in the turn-start frame.

---

## 6. Execution simulation

- **Physics is isolated behind `IPhysicsWorld`**, the only door to a physics engine. Operations: load a frozen board, set weapon and root targets, set limb joint targets (body moves bend the legs), `Step`, drain contacts, break a joint, drop the held item, impulse, capture pose, is-settled.
- **`Unity2DPhysicsWorld`** creates a hidden scene with `LocalPhysicsMode.Physics2D` and steps it only through `PhysicsScene2D.Simulate`.
  - Global `Physics2D.simulationMode = Script`, so nothing auto-steps.
  - Cosmetic debris in the visual scene is stepped by playback, so slow motion slows it too.
  - Ragdolls are Rigidbody2D joined by HingeJoint2D (§18).
  - It requires Play mode.
- **The turn loop (`TurnSimulator`, Session 04).** Load the frozen board at zero velocity, then each fixed step: body-move drivers set root targets and bend the legs (`IBodyMoveDriverFactory`, `LegPoser`; see *Body moves*), weapon drivers move the weapons in the path frame, the world steps, the pose is recorded, and the step's new contacts are refined, ordered and handed to `ITurnContactHandler`, the seam where Sessions 06–07 put the hit, clash and block rules (today `RecordContactsHandler` records each as a `ContactEvent`). The result is a `TurnResult`: timeline, end board (stance persistence, §3), execution report and end reason.
- **Turn-start root.** A dummy's root at turn start is its frozen pelvis position, upright (`TurnStartRoot`); without a body move it holds that spot and straightens up. Paths are drawn in this upright frame, which travels with the root during the turn.
- **Body moves (Session 05, GDD §5).** A move starts with execution and runs alongside the weapon path. Each is pure data (`BodyMoveStats`: displacement, duration, lean, stride, foot lift, the D12/D13 flags), played by one data-driven `StanceBodyMoveDriver`; no move is `NeutralBodyMoveDriver`. A driver returns a `BodyMoveFrame` per step: the root target and where each foot goes.
  - X is a step toward or away from the opponent that carries over (§3). Y is measured from standing height (pelvis one leg length above the floor): below it a low stance that is held (the crouch is a stance: no move keeps it, designer 2026-10-02), above it a hop that lands standing by the end of the duration. Leans are positive toward the opponent. D12's lean in place keeps the pelvis and tilts the torso.
  - The path frame travels with the root and keeps the angle the path was drawn at; `PathSettings.PathTiltsWithTorsoLean` (flagged) lets a lean tilt it. D13's speed bonus scales the weapon's path speed for the turn; its damage bonus is exposed on `TurnContactContext.BodyMoves` for the damage rules.
  - Legs: `LegPoser` turns each foot target into hip and knee angles (knee toward the opponent, the lowest corner of the leg's flat end on the target, clamped to the joint limits; a one-piece leg swings instead), sent with `IPhysicsWorld.SetLimbTarget` to the joint servos every step. The front leg is the dominant side's.
  - Facts: each chosen move is a `BodyMoveStartedEvent` at t = 0 (the player's input, so §13's wall reads "a backward swipe was chosen" from it or from `TurnPlan.BodyMove.IsBackward()`). Leg-loss limits come from `RulePolicies.MobilityPenalty` (no penalty until Session 11), applied when each turn's planning limits are built.
- **Fixed step.** `SimulationSettings.StepRateHz` defaults to 240, because a thin, fast rapier would tunnel at the default 50 Hz. Time is the integer step index converted to `SimTime`; never `Time.deltaTime`.
- **Time to impact.** Weapons travel their paths at constant weapon speed, so a contact inside a step is refined from path progress, matching the GDD's t = d / v (§9).
  - Contacts in a step are processed in time order, then by a stable id independent of the engine (owner, role, part of each body: `TurnContactOrder`). Timeline events are stored in time order.
  - Priority, interrupts and the sudden-death tie-break all compare `SimTime` in microseconds.
- **Drivers are strategies.** `IWeaponDriver` and `IBodyMoveDriver`. D1 (2026-10-01): kinematic path following; the motor driver stays selectable; handing the blade to physics when the rules stop it comes with D26 (Session 06). Paths are torso-relative (§6, Decided). Weapon drivers get the path frame at both ends of each step.
- **A turn ends** when both paths are done and physics stays settled for `SettleStepsRequired` steps, or at the 1.5 s hard cap.
- **Standing and the weapon arm (Session 02).** Each torso is pulled to an invisible kinematic root anchor by a strength-limited `RelativeJoint2D` (upright included), and hinge-motor servos hold every joint at its target (the rest pose, or the bent legs a body move asks for), so dummies stand still yet hits still knock them. The weapon is not jointed to the body: its driver moves it, and the weapon arm follows with a one-way hand spring onto the blade where the arm can reach (`ArmReach`).
- **Contacts (Session 02).** After each step the world polls contacts and reports pairs of different owners that were not touching after the previous step, floor and edges excluded, in load-time body order. The normal points from A to B; relative velocity comes from each body's motion over the previous step. Held items use full kinematic contacts, so two kinematic blades meeting is a contact. Unity 6.5 measures `HingeJoint2D.jointAngle` clockwise, so joint limits are flipped when applied.
- **Arena edges (D22, §13 TBD).** `IArenaEdgePolicy` describes the edges (`ArenaEdges`) and `IPhysicsWorld.Load` builds them; the default is a solid invisible wall at ±`ArenaSettings.WidthUnits`/2. Root targets are kept half a torso width inside solid edges.
- **Loading.** Every `Load` builds a fresh hidden physics scene, so a turn simulated twice from the same inputs repeats exactly. Ten bodies per dummy by default (D2). Severed limbs on the board are rebuilt as loose placeholder bodies (Session 11 builds dismemberment).
- **Severing is logic-driven.** `Ragdoll.BreakJoint` disables the hinge when durability reaches 0. `breakForce` is never used (it is Rejected in the GDD).
- **Determinism.** It is not required for server authority, but ordering is kept deterministic: seeded `IRandom`, stable sorts, no Unity time or random calls, a fresh physics scene per turn. Unity's newer low-level 2D physics module (`physicscore2d` is in the manifest) could become a second backend later without touching the rules.

---

## 7. Networking seam

`ITurnAuthority` (GDD §18 TBD, proposal: server-authoritative):

- **Events:** `MatchStarted`, `PlanningStarted`, `PublicStateChanged`, `PlanningLocked`, `TurnResolved`, `MatchEnded`.
- **Calls:** `Start`, `Tick`, `Send(side, command)`, `NotifyPlaybackFinished(turnIndex)`. `Send` only records; the simulator runs inside `Tick`, at most one turn per call.
- **Implementations:**
  - `LocalTurnAuthority` runs the rules state machine and an `ITurnSimulator` in-process: the offline prototype, bots, hot-seat and tests.
  - A future `RemoteTurnAuthority` would talk to a server hosting the same core headless. Host-authoritative or lockstep would be different hosts of the same core. No transport package is chosen yet.
- **Plan sources (`IPlanSource`):** `TouchPlanSource` (human), `BotPlanSource`, `HotSeatCoordinator` (one person enters both sides, with a cover screen between), and scripted or recorded sources. `PlanSourceBinder` connects them to any authority.
- **Secrecy.** During planning only public state crosses to the opponent: weapon, Ready, and a charged signature move if that policy allows. Body moves, paths and chosen signature moves arrive only inside the `TurnResult`.

---

## 8. Input and handedness

- **`ScreenLayout`:** the body zone is the strip of `BodyZoneWidthFraction` (15%) at the player's own screen edge; the rest is the weapon zone.
  - The layout is mirrored for left-handed players (`MirrorWhenLeftHanded`, Decided §4).
  - `ArenaViewFlipped` is true when the view must flip so the player's own dummy is on their side. Presentation applies the flip via `FixedSideCameraDirector` on `ArenaRoot`.
- **Input only emits `PlanningCommand`s**; the rules decide what is allowed. The legacy `UnityEngine.Input` is never used; touch goes through EnhancedTouch (`TouchInputReader`).
- **`ScreenToArenaMapper`** converts pixels to arena units in the fighter's torso frame (+X toward the opponent), undoing the view flip. Screen size, resolution and touch rate must not change any budget or outcome.

---

## 9. Presentation and feel

- **`TimelinePlayer`** plays a `TurnTimeline` at any speed, with pause, seek, frame-step, restart and loop.
  - Events fire once, in order, as playback passes them. Scrubbing back does not re-fire them; replaying forward does.
- **`IMatchEventReactor`s react to events:** `FeelDirector` (hitstop and slow motion from `FeelConfig`), `MatchVfx` (bolts, springs, sparks, hydraulic fluid; no blood), `MatchAudio`, and the camera director.
- **`TurnPlayback`** drives the two `DummyView`s from a `TimelinePlayer` and raises `Finished` once per play; a looping or paused turn never finishes, so the match waits on it.
- **`DummyView`** only copies recorded poses onto transforms, in the arena root's local space (so flipping the arena root mirrors the view), and shows the held weapon by id. Skins change visuals only. Until Session 17 it is the ragdoll prefab with its physics stripped (`VisualDummyFactory`).
- **`IMomentDetector`** flags legendary turns for cinematic replay (pillar 1). Replay sharing is TBD (§17).

---

## 10. Testing and debug tooling

**Out-of-Editor check.** `python Tools/check.py` compiles every XRim assembly in the editor, player and development player passes, proves the engine-free assemblies cannot see UnityEngine, and runs the EditMode tests of engine-free modules. See `Tools/README.md`.

**Tests**
- EditMode (fast, no scene):
  - The GDD Appendix A conformance test (code defaults equal Appendix A).
  - Settings validation.
  - Match state cloning.
  - Wallet separation.
  - `SimClock` drift.
  - Timeline player.
  - Screen layout and mirroring.
  - Client flow ordering.
  - Bot and plan-source routing.
  - `FakePhysicsWorld`, `StubTurnSimulator` and `FakeTurnAuthority` let the execution loop and flow be tested without Unity physics.
- PlayMode: the hidden physics scene moves only when stepped and never moves the visual scene.
- Planned:
  - Scenario tests from saved (board, plans).
  - A same-inputs-twice repeatability check.
  - A headless bot-vs-bot batch that reports dismemberment rate and match length against `BalanceTargetsConfig`.

**Debug tooling** (Editor and development builds only):
- An in-game **Debug** button. Its **Tuning** tab edits every asset in the profile live; placeholders are orange and TBD fields say [TBD]. In the Editor, edits persist after Play mode.
- The **Playback** tab: speed 0.05×–2×, pause, frame step, scrub, loop, replay, re-simulate the last turn with the current tuning (display only), and the turn's raw contact list.
- The **sandbox** (MatchBootstrap in `Sandbox` mode): plan both sides with the mouse through `SandboxPlanSource`s (real `PlanningCommand`s), with path and ink preview, weapon pick, body moves (↓ crouch, ↑ jump, →/← lunge or step back toward or away from the opponent, N none, or the HUD buttons; a faint copy shows the path at the move's full extent) and Execute; simulation and body-move tuning apply from the next turn. **F9** saves the feel report (`FeelDiagnostics`: standing, tunnelling, kinematic vs motor, cost per turn) to `Logs/XRimFeelReport.txt`.
- The **Cheats** tab (planned): freeze timer, infinite ink, force stun or sever. Cheats act through seams (clock, settings), never through rule code.
- `GameplayGizmos` (planned): path and ink, rigidity-invalid segments in red, hit zones, contact normals and clash angles, time-to-impact labels.
- `ScenarioStore` (planned): save and load a board plus both plans.

**Editor menus**
- **XRim → Setup:**
  - **Create Default Tuning Assets.** Safe to rerun; never overwrites tuned assets.
  - **Apply Project Settings.** Landscape only; Physics 2D in Script mode.
  - **Create Sandbox Scene.** Generated; running it again rebuilds it (camera, arena root, MatchBootstrap in Sandbox mode).
  - **Fill New Tuning Fields.**
  - **Build Placeholder Dummies.** The 6- and 10-body ragdoll prefabs, from the live tuning.
  - **Reset Body Moves To Starting Values.** Asks, then overwrites every body-move asset (adding missing ones) from `GddStartingValues`; undoable.
- **XRim → Reports:** **TBD Seams** and **Placeholder Values**, both read from the code attributes.

---

## 11. Coding conventions

- **Namespaces** are `XRim.<Module>[.<Area>]`, matching folders. One public type per file.
- **Naming:**
  - PascalCase for types and members, `_camelCase` for private fields (including `[SerializeField]`), camelCase for locals.
  - `I`-prefixed interfaces; policies are named `I*Policy` or `I*Model`.
  - ScriptableObjects end in `*Config`/`*Definition`; engine-free data ends in `*Settings`/`*Stats`.
- **Units go in names:** `…Seconds`, `…Degrees`, `…Units` (arena units), `…Fraction`, `…Multiplier`, `…Hz`.
- **No magic numbers:**
  - Gameplay numbers live only in settings.
  - Decided constants live in `RuleConstants` with a GDD reference.
  - Technical constants are named `const`s.
  - New unknown values get `[Placeholder]`.
  - Seams for open questions get `[GddTbd("§n", "question", Proposal = …)]`.
- **Events and references:**
  - Rules emit domain events as data (`MatchEvent` lists stamped with `SimTime`), never C# events.
  - Unity layers use C# `event`s on plain services.
  - Direct references only point to lower layers.
  - No singletons, no `FindObjectOfType` in gameplay code (debug tools may look up `MatchBootstrap` once).
  - Constructor injection; one composition root per scene; no DI framework for now.
- **Domain reload is disabled** (Enter Play Mode options), so there is no mutable static state.
- **Inside Rules and Simulation:**
  - No `UnityEngine.Time` and no `UnityEngine.Random`.
  - No dependence on `Dictionary` ordering.
  - No LINQ in per-step code.
- **Unity 6.5 notes:**
  - `Object.GetInstanceID()` is obsolete-as-error.
  - Null checks on `UnityEngine.Object` use `!= null`, not `??` or `?.`.
- **Placeholders for unimplemented gameplay** throw `NotImplementedException` with a "Placeholder: architecture setup only" comment.

---

## 12. TBD register: every GDD TBD the code touches, and its seam

The code is the live register: **XRim → Reports → TBD Seams** prints this list from the `[GddTbd]` attributes. The TBD rule applies: the first wired option is never the final answer.

| § | Open question | Seam |
|---|---|---|
| 1 | Target match length | `BalanceTargetsConfig._targetMatchLengthSeconds` (0 = undecided) |
| 2 | Camera behaviour | `ICameraDirector` → `FixedSideCameraDirector` |
| 2 | Adaptive music | `IMusicIntensityDriver` |
| 3 | Cancel Ready to edit | `MatchSettings.AllowReadyCancel` |
| 3 | Inputs that reset the forfeit counter | `IIdleTurnPolicy` |
| 3 | Order of end conditions in the same turn (not in the GDD) | `EndConditionEvaluator` |
| 3, 18 | Disconnects and reconnection | `ITurnAuthority` implementations (not yet designed) |
| 4 | Orientation | Project setting via Apply Project Settings (landscape assumed) |
| 4 | Placement of weapon selector, signature buttons, Ready | `PlanningHud` (UI Toolkit layout) |
| 4 | Change handedness later; separate mirror toggle | `IHandednessLayoutPolicy` |
| 4–5 | Overall feel of the dual-zone scheme | `IInputScheme` → `DualZoneInputScheme` |
| 5 | Lean vs real step back (D12); lunge speed/damage bonus (D13) | `BodyMoveStats.IsLeanInPlace` (default: real step), `WeaponSpeedBonusFraction`, `DamageBonusFraction` (default: 0, reach only) |
| 5 | Diagonal or combined swipes | `ISwipeClassifier` |
| 5, 12 | Moves after losing a leg; one leg / both legs | `IMobilityPenaltyPolicy` (default `NoMobilityPenaltyPolicy`) |
| 5 | Does a crouch last into later turns (not in the GDD) | `BodyMoveStats.StandsUpFromLowStance` on the neutral move (default: a stance, stays low) |
| 6 | Does the path tilt with a body move's lean (not in the GDD) | `PathSettings.PathTiltsWithTorsoLean` (default: no) |
| 6 | Weapon stats, speeds, base damage | `WeaponStats` assets (placeholders; `DesignIsTbd`) |
| 6 | Keep or cut spear rigidity | `IInkCostModel`, `RigiditySettings.Enabled` |
| 6 | ~~Path start; reach limit; strokes per turn~~ Decided 2026-09-29 (D3–D5, see §13) | `IPathStartPolicy`, `IReachPolicy`, `IStrokePolicy` keep the decided rules |
| 6 | Drawing during lock-out | `MatchSettings.AllowDrawingDuringLockout` |
| 6 | Starting weapons, default loadout, shield slot required | `LoadoutSettings` |
| 6 | Remaining-ink UI | `PlanningHud.ShowInkRemaining` |
| 7 | Shield block model; mace vs shield | `IShieldBlockModel` |
| 8 | Charge model; moves per match | `SignatureChargeMode`, `ISignatureChargeModel`, `SignatureSettings.MovesPerMatch` |
| 8 | Weapon ties, ink use, body-move interaction, catalogue | `SignatureMoveStats` fields |
| 8 | Opponent sees a charged move | `SignatureSettings.OpponentSeesCharged`, `IPublicStatePolicy` |
| 9 | Which hits interrupt; heavy swing armour | `IInterruptPolicy`, `WeaponStats.HasSwingArmour` |
| 10 | Effect of a stagger | `IStatusEffect` (shared with stun) |
| 10 | Repeated contacts between two weapons | `IRepeatContactPolicy` |
| 11 | HP scale, base damage, durability, stun threshold | `DamageSettings` (placeholders) |
| 11 | Effect of a stun | `IStatusEffect` → edits next turn's `PlanningConstraints` |
| 12 | Armless body blows; limb weapon slot; limb retrieval | `IArmlessAttackMode`, `LoadoutSettings.SeveredLimbIsExtraOption`, `ILimbRetrievalPolicy` |
| 13 | Wall damage growth; damage when knocked into it | `ElectricWallSettings` fields |
| 13 | Arena width and edge behaviour | `ArenaSettings.WidthUnits`, `IArenaEdgePolicy` (D22 default: 2000, solid invisible stop) |
| 14 | Board state; no hit; exact tie; wall; options | `SuddenDeathBoardMode`, `ISuddenDeathNoHitPolicy`, `ITiePolicy`, `ElectricWallSettings.ActiveInSuddenDeath`, `SuddenDeathSettings` flags |
| 15 | Lifesteal and build-up traits | `IWeaponTrait`, `WeaponStats.TraitIds` |
| 16 | Currency sources and amounts; premium earnable; farming | `IRewardPolicy`, `IFarmingGuard`, `EconomySettings` |
| 17 | Bots and practice; replays and sharing | `IBotBrain`, `IMomentDetector`, stored `TurnResult`s |
| 18 | Networking model | `ITurnAuthority` |
| 18 | Arena unit scale and reference resolution | `ArenaSpaceConfig`, `InputConfig._referenceResolution`, `ScreenToArenaMapper` |

**Parked and Rejected ideas are not implemented.** Several are blocked by the structure itself:
- No conversion API from premium to earned currency.
- Skins cannot reach the rules.
- Plans carry no finger timing.
- There is no throwing, and no `breakForce` joints.

---

## 13. Decisions made outside the GDD

Record these in the GDD when convenient.

| Date | Decision | By |
|---|---|---|
| 2026-09-29 | Turn cap default is **30** (§3, §13); Appendix A's "15 turns" is outdated | Designer |
| 2026-09-29 | The opponent **sees when you press Ready** (Decided; `PublicPlanningState.IsReady`) | Designer |
| 2026-09-29 | Landscape lock offered as an Editor menu item (landscape is "assumed", TBD) | Claude, delegated |
| 2026-09-29 | Removed Visual Scripting, Unity Version Control (collab-proxy), Multiplayer Center packages | Claude, delegated |
| 2026-09-29 | Placeholder numbers listed in §4 of this doc | Claude, placeholders only |
| 2026-09-29 | §6 path start (D3): anywhere; a lead-in from the weapon tip is added and costs ink and time | Designer |
| 2026-09-29 | §6 reach (D4): clamp the path onto the reach limit (arm + weapon length around the shoulder); the lunge does not enlarge the torso-frame limit | Designer |
| 2026-09-29 | §6 strokes (D5): one continuous stroke per turn; redrawing replaces it | Designer |
| 2026-09-29 | §6 rigidity formula as written (the whole Δθ counts above the threshold); a too-sharp turn cuts the path at the break | Designer |
| 2026-10-01 | Both players forfeit in the same turn (not in the GDD): sudden death, like a double KO (`MatchSettings.BothForfeitRule`; the other option is higher HP wins) | Designer |
| 2026-10-01 | D1 weapon driver: kinematic (motor stays selectable) | Designer |
| 2026-10-01 | D2 ragdoll: ten bodies (six stays selectable) | Designer |
| 2026-10-01 | D22 (§13 TBD): arena width 2000 (placeholder), each edge a solid invisible stop, as the flagged `IArenaEdgePolicy` default | Designer |
| 2026-10-01 | Blades start apart en garde (placeholder guard: −40°, grip at the shoulder) | Designer |
| 2026-10-01 | No body move: the dummy holds its frozen spot and straightens up; paths are drawn upright | Claude, approved with the plan |
| 2026-10-02 | D12 and D13 on their recommended defaults as flagged seams: a real step back; the lunge adds reach only | Designer |
| 2026-10-02 | A crouch is a stance: it holds through the turn and stays until another body move (flagged) | Designer |
| 2026-10-02 | The path does not tilt with a body move's lean (flagged; to confirm) | Claude |

---

## 14. Implementation status

- **Working infrastructure:**
  - Settings, validation, `GddStartingValues`, the tuning assets and profile, and snapshots.
  - `SimTime`, `SimClock`, the timeline recorder and `TimelinePlayer`.
  - `ScreenLayout` and mirroring.
  - `ClientMatchFlow`, `PlanSourceBinder`, `PlanSourceFactory`.
  - `Unity2DPhysicsWorld` scene creation and stepping; `Ragdoll.BreakJoint`.
  - Wallets.
  - The debug overlay and tuning panel.
  - All Editor menus and reports.
  - Path rules (Session 01): `PathResampler`, ink cost models, ink cut-off, the D3–D5 policies and `PathBuilder` (order: stroke, lead-in, resample, reach, ink cut-off, break cut).
  - Feel spike (Session 02): kinematic and motor weapon drivers, the aim model, impact-time refinement, the contact angle and the placeholder ragdoll (6 or 10 bodies). Its swing loop, scene and harness were retired in Session 04.
  - Body moves (Session 05): crouch, lunge, step back (both D12 options) and jump from data, legs posed by `LegPoser`, the path travelling with the body, the backward fact for the wall, the mobility seam, sandbox pickers.
  - Turn simulation and playback (Session 04): `TurnSimulator` (two fighters, contacts to the `ITurnContactHandler` seam, settle or hard cap), `Unity2DPhysicsWorld` complete for two fighters (edges, severed-limb placeholders, fresh scene per load), stance persistence (`StartingBoard`, `GuardStance`, `PoseWeaponTipLocator`), `MatchBootstrap` running the local loop, `TurnPlayback`, the Playback tab, the sandbox and the feel report.
  - Match loop and planning rules (Session 03, headless): `PlanningSession` (every command, lock-out, Ready and cancel, timeout), `PlanValidator`, `EndConditionEvaluator` (D11 order), `SuddenDeathSetup`, `MatchStateMachine` (all phases), `LoadoutValidator`, `LocalTurnAuthority` with the playback hold, `RandomBotBrain` and `BotPlanSource` (valid plans only). Placeholders inside it: the guard-stance weapon tip (`GuardStanceWeaponTipLocator`, until Session 04 reads the pose) and the sudden-death seams (Session 12).
- **Placeholders that throw `NotImplementedException`:** the remaining gameplay logic.
  - Rules: clash, damage, limb cap, wall.
  - Simulation: the rules' contact decisions (Sessions 06–07).
  - Input and presentation: swipe classification, screen mapping, the input scheme, feel, VFX, audio and HUD.
  - Tooling: hot-seat and scenarios.
