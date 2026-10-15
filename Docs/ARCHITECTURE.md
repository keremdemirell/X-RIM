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
| `XRim.Core` | no | `Vec2`, `XMath`, `Side`, `PerSide<T>`, `SimTime` (integer µs), `IClock`/`ManualClock`, `IRandom`/`XorShiftRandom`, `ScreenInsets` (the safe-area arithmetic), `RollingStatistics` (the arithmetic of on-device measurements), `Guard`, `[GddTbd]`, `[Placeholder]` | – |
| `XRim.Rules` | no | Match state machine, planning session, paths and ink, clash, damage, limbs, status effects, electric wall, sudden death, domain events, `RulePolicies` (every TBD seam), `*Settings` + `GddStartingValues`; the infinite-ink cheat's `FreeInkCostModel` (Session 13, debug only) | Core |
| `XRim.Economy` | no | `EarnedAmount` and `PremiumAmount` as separate types, `Wallet` with no conversion API, `GameplayUnlock` (earned price only), cosmetics, reward and farming seams | Core, Rules (reads `MatchSummary` only) |
| `XRim.Simulation` | no | `IPhysicsWorld` port, `SimClock`, `TurnSimulator` (+ `ITurnContactHandler` seam, `HitContactHandler`: hits, clashes, blocks), weapon drivers and aim models (`AimFromShoulderModel`, `ShieldFaceAimModel`), `HeldItemShape`, body-move drivers (`StanceBodyMoveDriver`, `NeutralBodyMoveDriver`), `LegPoser`, `TimelineRecorder`, `TurnResult`, `BoardSnapshot`, `PoseSnapshot`, `GuardStance`, `StartingBoard`; lost limbs (Session 11): `TurnBoard`, `SeverApplier`, `ScriptedSeverContactHandler`, `SeveredLimbLayout`, `HeadStrikeAimModel`, `HeadbuttLean`; electric wall and sudden death (Session 12): `TurnWalls`, `ElectricWallPose`, `SuddenDeathBoard`, `ScriptedKnockOutContactHandler`; Session 13: `ScriptedStatusContactHandler` (the force-stun cheat), `DummyLayout` (the dummy's bodies, joints and draw order as the builder lays them out; the art spec and the hit-zone overlay read it) | Core, Rules |
| `XRim.Networking` | no | `ITurnAuthority`, `LocalTurnAuthority`, `IPlanSource`, `PlanSourceBinder`, `PlanningWindow` | Core, Rules, Simulation |
| `XRim.Bots` | no | `IBotBrain`, `RandomBotBrain` (debug), `BotPlanSource` | Core, Rules, Simulation, Networking |
| `XRim.Config` | yes | Every ScriptableObject, `TuningProfile`, `ArenaSpace`, `TunableFields` | Core, Rules, Simulation, Economy |
| `XRim.Simulation.Unity2D` | yes | `Unity2DPhysicsWorld` (hidden, manually stepped scene), `PhysicsBodyTag`, `Ragdoll` | Core, Rules, Simulation, Config |
| `XRim.Input` | yes | `ScreenLayout` (zones and mirroring), handedness policy, swipe classifier (`SwipeSettings`), screen-to-arena mapper (`ArenaViewFrame`), `IInputScheme`, `TouchPlanSource`, `TouchInputReader`. The logic classes use plain data (`Vec2`, `SwipeSettings`), so their EditMode tests can run outside the Editor with the check tool's runner | Core, Rules, Config, Simulation, Networking, Unity.InputSystem |
| `XRim.Presentation` | yes | `TimelinePlayer`, `TurnPlayback` (event reactors, `IPlaybackPacer`), `PlaybackHitPoints`, `DummyView`, `ArenaView`, `PathPreviewView` and `ArenaLines` (live path preview, shared line set-up), `ElectricWallView` (the walls on the board), camera director, `FeelDirector` + `HitStop` + `PacingBreakdown`, audio, VFX, moment detector and `InstantReplay`, `PlanningHud`, `MatchHud`, `MatchScreens` (UI Toolkit in code, shared `HudElements`), `ArtSpecDocument` (writes Docs/ART_SPEC.md, Session 13) | Core, Rules, Simulation, Config |
| `XRim.App` | yes | `MatchBootstrap` (composes the scene once and each match fresh; exposes the authority's clock), `ClientMatchFlow`, `PlanSourceFactory`, `HotSeatCoordinator`, `LocalPlayers`, `PlanPreviewPresenter`, `PlanningHudPresenter`, `MatchHudPresenter`, `MatchLabels`, `MatchOverText`, `HudTouchBlocker`, `ReplayTouchBlocker`, `TouchBlockerSet`, `MatchMode`, `VisualDummyFactory`, `RecordingTurnSimulator`, `PlayedTurnPacing` | all runtime |
| `XRim.DebugTools` | yes | `DebugOverlay` (Tuning, Playback, Cheats tabs), `TuningPanel`, `PlaybackPanel`, the sandbox (`SandboxController`, `SandboxPlanSource`), `FeelDiagnostics`, gizmos, `ScenarioStore`. Compiled only when `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`; installs itself at runtime, so scenes never reference it | all runtime |
| `XRim.Editor` | Editor | Setup menus (incl. the placeholder dummy prefab builder and the sandbox scene), reports, `SettingsConfigEditor` | all runtime, and DebugTools |
| `XRim.Tests.EditMode` | Editor | Sanity and GDD-conformance tests, fakes | pure modules, Config, Input, Presentation, App |
| `XRim.Tests.PlayMode` | yes | Unity 2D physics backend tests; the match scene smoke test and the explicit bot-vs-bot batch (Session 09) | Core, Rules, Config, Simulation, Simulation.Unity2D, Networking, Bots, Input, Presentation, App |

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
    Presentation/          Playback/ Dummy/ Arena/ Cameras/ Feel/ Audio/ Vfx/ Moments/ ArtSpec/ UI/
  Tests/EditMode/<Module>/ Tests/PlayMode/<Module>/
  Data/                    created by XRim/Setup/Create Default Tuning Assets: Tuning/ Weapons/ BodyMoves/ (Audio/ by Build Audio Sets)
  Scenes/                  created by XRim/Setup/Create Sandbox Scene
  Prefabs/Dummies/         created by XRim/Setup/Build Placeholder Dummies (6- and 10-body ragdolls)
  Audio/Incoming/          the designer's sound files (named as Docs/SOUND_GUIDE.md says)
  Art/Incoming/            the designer's art (as Docs/ART_SPEC.md says; created when first needed)
  Art/Placeholder/ UI/     generated placeholder art, the HUD panel settings
Assets/Settings/           existing URP assets (unchanged)
Docs/                      GDD (never edited), ARCHITECTURE, SESSION_PLAN, sessions/, ART_SPEC (generated), SOUND_GUIDE, CREDITS
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
- **Client-only assets** (`InputConfig`, `ArenaSpaceConfig`, `FeelConfig`, `BalanceTargetsConfig`, `DisplayConfig`, `AudioConfig` with its `SoundSetDefinition`s) never affect outcomes. `DisplayConfig` (Session 10) holds the frame-rate target (`Application.targetFrameRate`, vertical sync off; default 60, a placeholder under the §18 target-devices TBD), applied by `MatchBootstrap` when the scene is built and put back when it goes.

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
- Wall: damage 10, bounce 5 (Session 12: replaced by a bounce distance of 80), advance 50, spawn offset 60.
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
- **Pre-match loadout pick (Session 14, GDD §6; designer 2026-10-15).** Before a match each human locks in 3 of the weapons they own (the shield counts as one; `LoadoutSettings.RequireShieldSlot` off by default, D8), blind, then both loadouts are revealed (Decided) and the match starts with them (the first pick is the starting weapon). The rules are engine-free and tested without an Editor: `IWeaponOwnership` (`AllWeaponsOwned` until the profile of Session 18), `LoadoutOptions` (only weapons and shields can be picked, never the club or the body blow; the pre-filled default), `LoadoutValidator`, `LoadoutSelection` (each side locks in once, the opponent's picks are hidden until both are locked, `Reveal`, `CreateFighters`) and `LoadoutPickSession` (hand-over in hot-seat → picking → reveal → done). Bots pick through `IBotLoadoutPicker` (`RandomLoadoutPicker`, always valid). The screens only draw it: `LoadoutPickPresenter` (App) maps the session to `IMatchScreensView` (`ShowLoadoutPicker`, `ShowLoadoutReveal`), and `MatchBootstrap.BeginLoadoutPick` runs mode → pick → reveal → `BeginMatch(mode, loadouts)` (Rematch picks again from the last picks; the picker and the match read one settings snapshot). Without screens (the sandbox, scenes built in code) the default loadout starts at once.
- **Locked.** Plans are frozen and validated by `PlanValidator` on the authority's clock (§18). Each side's electric wall then reacts to its locked body move (`ElectricWallRules.OnPlansLocked`, §13; Session 12).
- **Resolving.** `EndConditionEvaluator` is the single place that checks KO, double KO (→ sudden death), forfeit and turn cap (→ sudden death).
- **Weapon traits (Session 14, GDD §15; designer 2026-10-15: seams only).** Lifesteal and build-up are `IWeaponTrait`s: stateless strategies (a match is a pure function of cloned state), registered in `RulePolicies.WeaponTraits`, doing nothing until `TraitSettings.LifestealEnabled`/`BuildUpEnabled` is on **and** a weapon lists the trait in `WeaponStats.TraitIds` (none does). They are damage modifiers after the rules' own and see each landed hit (`OnHitLanded` returns HP to recover). What they count lives on `FighterState` (`HpDamageTaken`, from hits and wall zaps; `LifestealHitCount`, `LifestealDamageDealt`; `HitLandedThisTurn`, cleared by `WeaponTraitRules.EndTurn` in `MatchStateMachine.CompleteExecution`, which also restarts "consecutive" counting after a swing that landed nothing). `HitResolver` reads the damage taken before the instant and applies recoveries after it (never above max HP, never to a dead dummy, none in sudden death), emitting `WeaponTraitHealEvent`, which `PlaybackHitPoints` shows as the HP bar rising.
- **Sudden death (Session 12, GDD §14; the designer's choices of 2026-10-11).** A double KO or the turn cap enters it through `SuddenDeathSetup.Apply`: both to 1 HP (Decided), the HP each had kept (`FighterState.HpBeforeSuddenDeath`), the rules half of `SuddenDeathSettings.BoardMode` (P5: carry over by default; a reset makes every limb whole and undamaged, drops the club, clears lost weapons and statuses, and a club or body blow gives way to loadout slot 1; `LocalTurnAuthority` then puts the pose in place with the Simulation's `SuddenDeathBoard`: the match's start lines on a reset, the pose the turn ended in otherwise, the walls drawn as the state has them), the walls down unless live in sudden death (off), idle counters reset. The options are planning limits (`AllowWeaponSwitching`, `AllowSignatureMoves` → `PlanningConstraints`, both on). `EndConditionEvaluator` decides each sudden-death turn: the first decisive moment wins (P4: a side's first valid hit, even one under 1 HP of damage, or the other side's death without a hit, from `ExecutionReport.DeathTime`; microseconds compared, so two hits in one step are still ordered); exactly the same instant goes to `ITiePolicy` (`ReplayTiePolicy`); then idle forfeits; then a turn without a winner (no hit, a tie replayed, both idle) goes to `ISuddenDeathNoHitPolicy` (`CappedRepeatNoHitPolicy`: played again, at 1 HP and counted in `MatchState.SuddenDeathRepeats`, at most `MaxRepeatsWithoutHit` = 3 times; then more HP when sudden death began wins, level HP a coin toss from the match seed: `MatchEndReason.SuddenDeathMoreHp`, `SuddenDeathCoinToss`, both flagged because they bend §3's "no points win"). So sudden death lasts at most 1 + 3 turns.
- **Client flow (`XRim.App.ClientMatchFlow`).** Idle → Planning → WaitingForAuthority → Playback → … → MatchOver. It only mirrors the authority. It holds the next planning phase and the match end until playback has finished, then reports the turn to the authority (`NotifyPlaybackFinished`, Session 09).
  - The authority starts the next planning timer only after playback (Session 03): `MatchStateMachine.BeginPlanning` is what starts the timer, and `LocalTurnAuthority` holds in TurnStart until its `PlaybackHoldSettings` mode says playback is over (none, the recorded timeline duration, or the client's `NotifyPlaybackFinished` with a timeout). SuddenDeathSetup is a resting phase like TurnStart; Resolving happens inside `CompleteExecution`.
- **Composition (`XRim.App.MatchBootstrap`, Sessions 04 and 09).** Built once per scene: the hidden `Unity2DPhysicsWorld`, the visible `DummyView`s and `TurnPlayback` (with `FeelDirector` as reactor and pacer), the `TouchInputReader`, and outside the sandbox the `PlanningHud`, `MatchHud` and `MatchScreens`. Built fresh for every match (`PrepareMatch` / `BeginMatch` / Rematch / a new mode), so tuning edits apply from the next match: the settings snapshot, `RulePolicies`, `TurnSimulator` (wrapped by `RecordingTurnSimulator` for debug re-simulation) → `LocalTurnAuthority` (hold mode `ClientReport`, a `ManualClock` advanced by unscaled time) → `ClientMatchFlow` (subscribed before any plan source) → playback; the plan sources from `PlanSourceFactory` (the humans on this device as `TouchPlanSource`s held by `LocalPlayers` with their `PlanPreviewPresenter`, the random bot), the HUD presenters. Touches pending when a player's input starts are discarded, so a tap on a closing screen never becomes a stroke.
  - **Modes (D25, flagged: dev opponents only).** The Match scene (`XRim/Setup/Create Match Scene`) opens on a mode picker: **hot-seat**, **vs random bot** (the human plays Left) and **bot vs bot** to watch (the frozen board stays 1 s between turns, a viewing aid). In `Sandbox` the planning timer is frozen and DebugTools hands its plan sources to `MatchBootstrap.StartMatch` (App never references DebugTools).
  - **Hot-seat (`HotSeatCoordinator`, a dev mode; designer 2026-10-04).** The players plan one after the other behind an opaque cover screen, each with the full planning time and the real lock-out, enforced by the authority: the coordinator drives the authority's clock, stops it behind the cover and turns it back to the opening for the second player. Ready ends a slot (no Ready cancel in hot-seat); the first player's time running out ends their slot with whatever is set (Ready is pressed for them just before their deadline). The first planner alternates each turn; a side with less planning time goes first. Each player sees their own view while planning; playback uses the plain view.
  - **End of match.** `MatchScreens` shows the winner, the reason, the turns and the real time, with Rematch and Change mode. Sudden death plays as the rules have it (Session 12) under a banner ("1 HP each: the first clean hit wins · try k of 4"); its fallback endings have their own words (`MatchOverText`). The interim tie placeholder screen is retired.
- **Starting board and weapon tips.** A match starts on `StartingBoard` (engine-free): both dummies in `GuardStance`, `StartingGapUnits` apart, mirrored around x = 0, blades apart en garde. Each turn's weapon tips come from the frozen pose (`PoseWeaponTipLocator`), in the turn-start frame.

---

## 6. Execution simulation

- **Physics is isolated behind `IPhysicsWorld`**, the only door to a physics engine. Operations: load a frozen board, set weapon and root targets, set limb joint targets (body moves bend the legs), raise, move or remove an electric wall (`SetElectricWall`), `Step`, drain contacts, break a joint, drop the held item, impulse, capture pose, is-settled.
- **`Unity2DPhysicsWorld`** creates a hidden scene with `LocalPhysicsMode.Physics2D` and steps it only through `PhysicsScene2D.Simulate`.
  - Global `Physics2D.simulationMode = Script`, so nothing auto-steps.
  - Cosmetic debris in the visual scene is stepped by playback, so slow motion slows it too.
  - Ragdolls are Rigidbody2D joined by HingeJoint2D (§18).
  - It requires Play mode.
- **The turn loop (`TurnSimulator`, Session 04).** Load the frozen board at zero velocity, then each fixed step: body-move drivers set root targets and bend the legs (`IBodyMoveDriverFactory`, `LegPoser`; see *Body moves*), weapon drivers move the weapons in the path frame, the world steps, the pose is recorded, and the step's new contacts are refined, then held until no earlier or equally early contact can still be reported (one or two steps), and handed in `TurnContactOrder` to `ITurnContactHandler` one instant at a time (`BeginTurn` once, then `Handle(contacts)`; Session 12), the seam for the hit rules (Session 06) and the clash and block rules (Session 07). The result is a `TurnResult`: timeline, end board (stance persistence, §3), execution report (each side's first valid hit, same-step flag) and end reason.
- **Hits (Session 06, GDD §9, §11).** `HitContactHandler` (the default) records every raw contact as a `ContactEvent` for the debug list and turns a held weapon touching another dummy's body part into `HitFacts` (zone from the part tag, weapon from what the attacker holds, refined time). One instant at a time it asks the rules' `HitResolver` (engine-free, `XRim.Rules.Combat`), then applies the verdict through `ITurnActions`:
  - Priority: instants in time order, so the first weapon to reach a hitbox lands first; hits at exactly the same instant all land (a trade).
  - Interrupts: `IInterruptPolicy` (default `SettingsInterruptPolicy`: D15 `DamageSettings.InterruptRule`, weapon arm or head; D16 `WeaponStats.HasSwingArmour`); a killing hit always cancels the dead dummy's attack. A cancelled weapon stops where it is.
  - Several hits (D26): each body part once per turn, at most `MaxHitsPerWeaponPerTurn`; after each hit the weapon keeps `WeaponStats.SpeedKeptAfterHitFraction` of its speed (`IWeaponDriver.SlowTo`) and the next hit's damage follows the speed left (`FollowThroughDamageModifier`); its last hit stops it with a recoil (`IWeaponDriver.Stop`).
  - Only a weapon travelling its drawn path lands hits (`IWeaponDriver.IsTravelling`; flag `RestingWeaponsDealDamage`).
  - Damage: `DamageCalculator` (base × zone × `RulePolicies.DamageModifiers`), limb cap (`LimbRules`), no instant KO (`IInstantKoPolicy`, D27), head stun (`IStatusEffectFactory`, D17); `HitLandedEvent`, `AttackInterruptedEvent`, `StatusAppliedEvent`, `FighterDiedEvent`.
  - Reactions (`SimulationSettings.HitReaction`): the struck part gets a share of the weapon's momentum along its motion; the victim is knocked away from the attacker by weapon mass × a distance and stays there (E1, flag `KnockbackPersists`), shifting its root and soles in the turn loop.
  - Status lifetime: effects are immutable; `MatchStateMachine.PrepareTurn` applies them to the next turn's `PlanningConstraints` (named there for the HUD) and uses them up.
- **Clashes and blocks (Session 07, GDD §7, §10).** In each instant `HitContactHandler` resolves held item against held item first (A2: the blade or shield in the way wins a tie), then the hits; the rules' engine-free `WeaponContactResolver` shares each side's `TurnAttack` with `HitResolver`, so a clash or block carries into the hits after it.
  - Clash (§10, Decided two-stage model): `ClashResolver` takes the contact's angle (stage 1: ≥ `ClashSettings.HardClashAngleDegrees` is hard) and each weapon's power P = W_m·m + W_v·v, where v is the speed the rules move the weapon along its path now (A1: stat × D13 bonus × D26 share left; 0 when not travelling, so a clash needs a travelling weapon). Crush-through: the winner continues (`TurnAttack.CrushedThrough`, −30% on its later hits), the loser is knocked off and its dummy staggered (`IStatusEffectFactory`, the stun's effect, D17); a rebound stops both; a glancing contact lets the lighter weapon deflect the heavier unless their masses are within the band, when both slide past. `WeaponClashEvent` carries the angle, both powers and the outcome.
  - Block (§7): a travelling weapon meeting the other side's shield goes to `IShieldBlockModel` (default `SettingsShieldBlockModel`, `ClashSettings.ShieldBlock`), never the clash model, measured from where the shield is at that instant: the angle of the relative motion against its face and where on the face it landed. D20 default: a full block square on the face, a partial block (× `PartialBlockDamageMultiplier` on every later hit of that weapon this turn, `ShieldBlockDamageModifier`, A5) at the rim or glancing; D21 default: no special rule. Two shields meeting have no rule (A7). Using a shield never reaches the electric wall's trigger (§13).
  - Repeats (D19): `IRepeatContactPolicy` (default: only the first resolved contact between the two held items per turn; clashes and blocks share the count).
  - Reactions (`SimulationSettings.ClashReaction`, A6): a rebound or full block bounces the weapon back (`WeaponStopKind.Rebounded`), then the hand holds it backed off by the recoil; a knock-off kicks it away from the winner along the contact normal (winner momentum over loser mass) with its own motion along the kick removed, lets it fly free briefly, then holds it where it ended (`WeaponStopKind.KnockedOff`); a full block pushes the shield holder back like a scaled-down hit. Survivors carry on along their paths; kinematic blades that both carry on pass through each other.
- **Held items (Session 07, A3).** `HeldItemShape` is the one layout of a held item for the physics box, its drawing, the impact sweep (`BladeShape`) and the point that follows the path (`PoseWeaponTipLocator`): a weapon or club runs from the grip to its tip and its tip follows the path; the shield is centred on the grip, its face looking along the body's rotation, and its centre follows the path. `ShieldFaceAimModel` (`TurnSimulatorOptions.ShieldAim`) holds it like a shield: the hand reaches toward the drawn point up to arm length and the face turns outward, square to the arm; the shield adds no reach (`WeaponStats.ReachBeyondHandUnits`). A tap is a short lead-in to the tapped spot (D3); the shield then holds there (A4).
- **Turn-start root.** A dummy's root at turn start is its frozen pelvis position, upright (`TurnStartRoot`); without a body move it holds that spot and straightens up. Paths are drawn in this upright frame, which travels with the root during the turn.
- **Body moves (Session 05, GDD §5).** A move starts with execution and runs alongside the weapon path. Each is pure data (`BodyMoveStats`: displacement, duration, lean, stride, foot lift, the D12/D13 flags), played by one data-driven `StanceBodyMoveDriver`; no move is `NeutralBodyMoveDriver`. A driver returns a `BodyMoveFrame` per step: the root target and where each foot goes.
  - X is a step toward or away from the opponent that carries over (§3). Y is measured from standing height (pelvis one leg length above the floor): below it a low stance that is held (the crouch is a stance: no move keeps it, designer 2026-10-02), above it a hop that lands standing by the end of the duration. Leans are positive toward the opponent. D12's lean in place keeps the pelvis and tilts the torso.
  - The path frame travels with the root and keeps the angle the path was drawn at; `PathSettings.PathTiltsWithTorsoLean` (flagged) lets a lean tilt it. D13's speed bonus scales the weapon's path speed for the turn; its damage bonus is exposed on `TurnContactContext.BodyMoves` for the damage rules.
  - Legs: `LegPoser` turns each foot target into hip and knee angles (knee toward the opponent, the lowest corner of the leg's flat end on the target, clamped to the joint limits; a one-piece leg swings instead), sent with `IPhysicsWorld.SetLimbTarget` to the joint servos every step. The front leg is the dominant side's.
  - Facts: each chosen move is a `BodyMoveStartedEvent` at t = 0 (the player's input, so §13's wall reads "a backward swipe was chosen" from it or from `TurnPlan.BodyMove.IsBackward()`), and each drawn path an `AttackStartedEvent` at t = 0 (side, weapon, kind; Session 13: for sound and the moment detector; the rules never read it). Leg-loss limits come from `RulePolicies.MobilityPenalty` (no penalty until Session 11), applied when each turn's planning limits are built.
- **Fixed step.** `SimulationSettings.StepRateHz` defaults to 240, because a thin, fast rapier would tunnel at the default 50 Hz. Time is the integer step index converted to `SimTime`; never `Time.deltaTime`.
- **Time to impact.** Weapons travel their paths at constant weapon speed, so a contact inside a step is refined from path progress, matching the GDD's t = d / v (§9).
  - Contacts in a step are processed in time order, then by a stable id independent of the engine (owner, role, part of each body: `TurnContactOrder`). Timeline events are stored in time order.
  - Two held items touch at a point only once both are there, so their contact takes the later of their two refined arrivals (a still shield touches the point all along).
  - Priority, interrupts and the sudden-death tie-break all compare `SimTime` in microseconds.
- **Drivers are strategies.** `IWeaponDriver` and `IBodyMoveDriver`. D1 (2026-10-01): kinematic path following; the motor driver stays selectable. When the rules stop a weapon, a kinematic blade is handed to physics carrying its own speed (`HeldItemCommand.Release` → `IPhysicsWorld.ReleaseHeldItem`), then `WeaponMotorControl` (the motor driver's spring-damper) holds it in the hand at the stop point, backed off along the path by the recoil after a last hit. Paths are torso-relative (§6, Decided). Weapon drivers get the path frame at both ends of each step. The clash and block rules also stop a weapon by bouncing it back or knocking it off (see *Clashes and blocks*).
- **A turn ends** when both paths are done and physics stays settled for `SettleStepsRequired` steps, or at the 1.5 s hard cap.
- **Standing and the weapon arm (Session 02).** Each torso is pulled to an invisible kinematic root anchor by a strength-limited `RelativeJoint2D` (upright included), and hinge-motor servos hold every joint at its target (the rest pose, or the bent legs a body move asks for), so dummies stand still yet hits still knock them. The weapon is not jointed to the body: its driver moves it, and the weapon arm follows with a one-way hand spring onto the blade where the arm can reach (`ArmReach`).
- **Contacts (Session 02).** After each step the world polls contacts and reports pairs of different owners that were not touching after the previous step, floor and edges excluded, in load-time body order. The normal points from A to B; relative velocity comes from each body's motion over the previous step. Held items use full kinematic contacts, so two kinematic blades meeting is a contact. Unity 6.5 measures `HingeJoint2D.jointAngle` clockwise, so joint limits are flipped when applied.
- **Electric wall (Session 12, GDD §13).** One wall per player (`MatchState.Walls`, `ElectricWallState`: phase, up, position, retreats in a row), changed only by the engine-free `ElectricWallRules`. Only the input counts: at lock the planned body move alone decides the turn's wall (a backward swipe with a wall standing behind makes it active; a first one raises the warning this turn; anything else, or walls off in sudden death, removes it and the sequence starts over, P3), so knockback, a shield or standing still can never trigger it. Designer's geometry (P1, 2026-10-11): the wall stands on the floor behind its owner, away from the opponent; the warning springs up `SpawnOffsetBehindUnits` behind the pelvis when the step back ends (`RaiseWarning`, never beyond a solid edge: on the edge line there), solid but harmless; the owner's body touching its active wall is the zap (`OnTouched` → `ElectricWallZap`): flat HP damage (P2: no zone multiplier, no limb damage, no interrupt or stun, no D27 clamp; it can knock out), a forward throw of `BounceDistanceUnits` that stays, and the wall advancing `AdvanceDistanceUnits` toward the opponent; a later touch in the same turn deals damage only with the §13 TBD flag `DamageWhenKnockedInto`; `DamageGrowthPerConsecutiveRetreat` adds damage per zap in a row (TBD, 0). `ElectricWallEvent` carries the phase, the damage, the wall's position and the knocked-into flag.
  - *Simulation* (`TurnWalls`, run by `TurnSimulator` itself, not by the optional contact handler): a wall the state has up is built by `Load`; a warning raised this turn springs up when its dummy's step back completes (before that step's frame is captured), at the root target's x; each instant's contacts go to the contact handler first, then each dummy touching its own wall goes to `ElectricWallRules` (`TurnWalls.TryReadTouch`: the wall's owner and a body part of the same owner). The retreat's zap is the first touch: the dummy gets a forward knockback that stays (E1's mechanism, over `HitReaction.KnockbackSeconds`) and the wall advances over the same time (`IPhysicsWorld.SetElectricWall` each step, a kinematic move that pushes). A standing wall keeps its dummy's root and soles a half-width in front of it, like a solid edge (a warning from when it springs up, an active wall from its zap), so the advance shoves the dummy forward: two retreats in a row end about 20 units ahead of where the dummy started. A later touch zaps again (damage only) only after a hit or full-block push (`ITurnActions.KnockBack`) since the last zap, and only with `DamageWhenKnockedInto`. A step back that never reaches its active wall gets the zap 0.1 s after it ends (only the input counts), and the turn cannot settle while one is owed. A zap that kills goes through `ITurnActions.Die`. Every captured pose marks each wall (`PoseSnapshot.LeftWall`/`RightWall`, `ElectricWallPose`); the final board's are the rules state's.
  - *Unity world*: each player's wall is a kinematic box the size of an edge wall (100 × 4100, technical), its face at the wall's x and its body behind it, tagged `BodyRole.ElectricWall` with its owner; both are built at every load (off when not up) and tracked from the start, so contact bookkeeping never changes mid-turn; unlike the edges, their contacts are reported, the owner's included.
- **Arena edges (D22, §13 TBD).** `IArenaEdgePolicy` describes the edges (`ArenaEdges`) and `IPhysicsWorld.Load` builds them; the default is a solid invisible wall at ±`ArenaSettings.WidthUnits`/2. Root targets are kept half a torso width inside solid edges.
- **Loading.** Every `Load` builds a fresh hidden physics scene, so a turn simulated twice from the same inputs repeats exactly. Ten bodies per dummy by default (D2). Severed limbs on the board are rebuilt loose (in two hinged pieces when they kept their forearm or shin) and never collide with the dummy they came from.
- **Lost limbs (Session 11, GDD §11, §12).**
  - *Rules* (engine-free): a hit that brings a limb to its durability severs it through `SeverRules`, the one place a sever changes the rules state: the limb is marked, `LimbSeveredEvent`; the arm holding the item (`FighterState.HeldItemArm`: the dominant arm while intact, then the other) drops it (`WeaponDroppedEvent`, the attack ends with `AttackStop.WeaponDropped`); the weapon comes back in the other hand from the next turn (G2, `LimbLossSettings.DroppedWeaponIsLost` off); a dropped club goes back to the floor. `HitResolution.Severed` carries the outcomes. Each turn's planning limits come from `DesperationOptions` (the club offered beyond the 3 slots while a free limb lies on the floor, anyone's by G1; the body blow the only option without arms; lost weapons forbidden; `PlanningConstraints.ExtraWeapons`, `CanUseLoadoutWeapons`, `AvailableWeapons`), then the statuses, then the leg-loss penalty (`SettingsMobilityPenaltyPolicy`: no jump or lunge on one leg, no moves on none). The off hand deals `OffHandDamageMultiplier` (80%) through the modifier list (`HitFacts.IsOffHand`).
  - *Simulation*: `TurnBoard` prepares the board before the load (a club switched away from drops where it was held; a dummy that picked the club takes the nearest free floor limb its `ILimbRetrievalPolicy` allows, automatic by default; the held item keeps its grip or, after a drop or a fresh pickup, rests at the guard; an armless body blow holds the head; a dropped item is not rebuilt; a living dummy's split torso is whole again) and finishes it after (a club dropped with its arm lies on the floor). `HitContactHandler` applies severs (`SeverApplier`: `IPhysicsWorld.BreakJoint`, `DropHeldItem`) and deaths (`ITurnActions.Die`: drop, then `SplitTorso`). A severed leg is no longer posed; no legs sit the pelvis on the floor; a dropped item is no longer driven; a dead dummy is left limp. Without arms the head strikes (`IArmlessAttackMode`, `StrikeWithBodyPart`): driven as the held item in the item convention (`HeldItemShape.HeadAsItem`), its contacts read as body-blow hits, the body leaning into it (`HeadbuttLean`, used by `HeadStrikeAimModel` and the root target; a feel choice). Every captured pose marks the limbs each dummy has lost (`FighterPose.SeveredParts`).
  - *Unity world*: severing is logic-driven: `Ragdoll.BreakJoint` disables the hinge, then the limb pops off backward and up (`SimulationSettings.SeverReaction`) so it always falls; `breakForce` is never used (Rejected). The torso is pre-split: two inactive halves take its place at death (`Ragdoll.SplitTorso`), the head and arms re-jointed to the top half, the legs to the bottom; every motor goes limp. Captures list this turn's severed limbs, the dropped item and the split.
  - *Throwing* is Rejected: no API names a throw (`NoThrowingTests`); dropping when the holding arm is severed is the only release.
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
  - **The body zone covers the player's own dummy (§4 Decided; designer 2026-10-11, F3, `InputConfig.BodyZoneCoversDummy`).** When planning opens, `MatchBootstrap` measures from the frozen board and the camera's framing how far from the player's own screen edge the zone must reach to cover their dummy's head, torso and legs plus `BodyZoneMarginBeyondDummyUnits` (`BodyZoneCover`, pure; the arms never widen it), and gives each human `ScreenLayout.WithDummyCover`: the zone is the larger of that and `BodyZoneWidthFraction` (`EffectiveBodyZoneFraction`, also what the HUD strip shows; the weapon selector stays at the configured strip's edge, `IPlanningHudView.ShowLayout(mirrored, zone, selectorStart)`, so it never runs into the centred timer). The sandbox keeps the plain strip.
  - `ArenaViewFlipped` is true when the view must flip so the player's own dummy is on their side. Presentation applies the flip via `FixedSideCameraDirector` on `ArenaRoot`.
- **Input only emits `PlanningCommand`s**; the rules decide what is allowed. The legacy `UnityEngine.Input` is never used; touch goes through EnhancedTouch (`TouchInputReader`).
- **Planning HUD (Session 08, batch 4).** `PlanningHud` (Presentation, UI Toolkit built in code: no UXML, stylesheet or panel asset; its `PanelSettings` are copies of one Editor-made asset (`Assets/XRim/UI/HudPanelSettings.asset`, `XRim/Setup/Create HUD Panel Settings`, wired to `MatchBootstrap` by Create Match Scene: only an asset carries the ICU data UI Toolkit's text needs in a player build, so panel settings made purely in code drew no text on the phone, Session 10) and scale with the screen height from `InputConfig.ReferenceResolution` (`HudPanelSettings.Create`); UI Toolkit's default Input System backend needs no `EventSystem`) implements `IPlanningHudView`: it only displays and reports presses (`WeaponChosen`, `ReadyPressed`). `PlanningHudPresenter` (App) serves one `TouchPlanSource` (`Source`, switchable for hot-seat) and listens to the authority's `PublicStateChanged` and `PlanningLocked`:
  - each frame it shows the countdown (the authority's clock against the window's deadline) and the lock-out, the weapon selector disabled for the final `WeaponSwitchLockoutSeconds`, a status effect or Ready, Ready / Cancel / locked in by the D9 flags, the weapon and body move set, the ink left (D7) and the opponent's public state (weapon and Ready; the board's weapon until they change something);
  - presses go out as the same planning commands as touches; every refusal (`PlanDraft.RejectionCount`) shows as a short note (`RejectionMessages`);
  - `HudTouchBlocker` makes a touch on a HUD control a press, not a stroke or a swipe.
  - **Safe areas (Session 10, GDD §4).** The player renders outside the safe area (a notch or cut-out never letterboxes the arena), so every HUD keeps its controls out of `Screen.safeArea`: `ScreenInsets` (Core, pure, tested) turns the safe-area rectangle into the space covered on each edge, `HudSafeArea` (Presentation) scales it to the HUD's reference-resolution units and says when it changes (a phone turned between landscape-left and landscape-right), and the HUDs apply it: `PlanningHud` puts the timer, opponent box, message and Ready corner in an inset container, `MatchHud` insets its whole root, `MatchScreens` pads each screen so backgrounds (the opaque cover especially) reach every edge while content stays inside. The body strip stays at the screen edge, because it is the touch zone made visible (the zone is a share of the screen width, Decided §4), and the weapon selector starts at the zone's edge, clear of a side notch. The debug overlay's button follows the same insets.
- **`ScreenToArenaMapper`** converts pixels to arena units in the fighter's torso frame (+X toward the opponent), undoing the view flip. Screen size, resolution and touch rate must not change any budget or outcome.
  - The scale comes from what the screen shows, not from the pixel count: `ArenaViewFrame` (the arena point at the screen centre and the arena units across the full screen height, read from the camera by `CameraArenaView`), so 1920×1080 and 2400×1080 give the same ink for the same drawn shape. Positions are Unity screen pixels (origin bottom-left, Y up).
  - `ScreenLayout` also holds the screen height (the swipe distance is a share of it) and `ForwardScreenSign`: forward is screen-right unless mirrored, whichever arena side the player controls.
  - **Touch to commands (Session 08, batch 2).** `TouchInputReader` (an `ITouchSource`) reads EnhancedTouch: each contact's history as well as its latest state, so the readings between frames are kept (touch rate and frame rate change nothing); with `MouseActsAsTouch` on, touch simulation makes the mouse a finger (Editor, desktop builds); it switches the `Mouse` device off for everything else, so it stays off until the old sandbox mouse code is gone (batch 3). `DualZoneInputScheme` turns `TouchSample`s into commands:
    - A finger keeps the zone it landed in; one that lands on a HUD control (`ITouchBlocker`) does nothing.
    - Body zone: a flick the classifier recognises sets the body move at once; a quick tap sets it back to neutral (`InputConfig.TapInBodyZoneClearsMove`, flagged).
    - Weapon zone: one finger draws one stroke (D5), sent when it lifts (the lift position always ends it); a finger that did not move (`TapMaxTravelScreenFraction`) sends a one-point stroke, which the rules turn into a lead-in (D3) and a shield raise (§7). A touch the system cancels sends nothing; a stroke still being drawn when planning ends is not part of the plan.
    - `TouchPlanSource` (an `IPlanSource`) runs the scheme for one side and is also where the HUD sends the weapon and Ready (`Send`); `PlanDraft` keeps what the rules accepted (weapon, stroke, body move, Ready, last rejection) through `DraftTrackingSink`. `IsActive` switches a source off for the other side of a hot-seat device.
  - `FourWaySwipeClassifier` (D14, flagged): down crouches, up jumps, forward lunges, back steps back; a flick more than `InputConfig.SwipeAxisToleranceDegrees` (30°, placeholder) off a straight direction, too short (a share of the screen height) or slower than the swipe window does nothing (`ISwipeClassifier` returns null).

---

## 9. Presentation and feel

- **`TimelinePlayer`** plays a `TurnTimeline` at any speed, with pause, seek, frame-step, restart and loop.
  - Events fire once, in order, as playback passes them. Scrubbing back does not re-fire them; replaying forward does.
- **`IMatchEventReactor`s react to events** (`TurnPlayback.AddReactor`): `FeelDirector` (the hitstops), the camera director (shake and punch-in, Session 13), `MatchVfx` (see *Mechanical effects*) and `MatchAudio`. `MatchEventLocator` (Playback) says where an event happened (a clash's or block's point, a hit's raw contact at the same instant or the struck part, a limb's joint, the torso's middle), for every effect that points at it.
- **Pacing (Session 13).** An `IPlaybackPacer` (`TurnPlayback.Pacer`, the `FeelDirector`) bends how fast a turn plays: `Begin(TurnResult)` when a play starts, then `Pace(real seconds, turn time now)` each frame; a turn never finishes while it holds, so even a hit on the last frame gets its freeze. Only playback timing changes, never the result (`TurnOutcomeIndependenceTests`, `FeelNeverChangesTheOutcomeTests`). The `FeelDirector`, its values read from `FeelConfig` at each play:
  - **Hitstops** (`HitStopSettings`): a hit freezes for a base plus a share per point of damage (before the D27 clamp), ×1.5 on the head, capped; a kill, a sever, a crush, a rebound or deflection, a full or partial block and a wall zap have their own freezes; freezes at one instant share the longest.
  - **Look-ahead slow motion** (`SlowMotionPlanner` → `SlowMotionPlan` of `SlowMotionWindow`s): the whole turn is known before it plays, so playback starts slowing just *before* a sever, a death, an interrupt counter-hit (`AttackInterruptedEvent`) and a sudden-death turn's decisive moment (its earliest first valid hit or death), each with its speed, time on screen and share of anticipation, easing in and out; the slowest stretch wins where two overlap. Real time becomes turn time exactly where the speed is steady and in 0.5 ms steps while it eases, so it is frame-rate independent.
  - It holds the turn while the camera still moves, and reports the turn's planned time on screen (`PlannedRealSeconds`; `TurnPlayback.PlayRealSeconds` is what it really took; both in the per-turn log line and the Playback tab).
- **Mechanical effects (Session 13, GDD §2 Decided: bolts, nuts, springs, sparks and hydraulic fluid; never blood).** `VfxRecipes` (pure) says what each event sprays as `VfxBurst`s and `VfxArc`s: a hit's sparks by its damage, flying on through the struck part, a flash, bolts and nuts off a heavy hit; a rebound's shower of sparks, a crush's dust shock ring, sparks and debris, a slide's skid; a full block's bright ring and a partial block's small orange one; a sever's bolts, nuts, springs and gear from the joint with a jet of hydraulic fluid leaving backward; a death's doubled burst with steam; arcs from the wall into the dummy on a zap and a crackle up the wall on a warning; dust under a body move. `MatchVfx` draws them from a pool (`VfxSlots`, capped by `FeelConfig._maxVfxPieces`, the oldest reused) with generated shapes (`VfxSprites`), each a pure function of turn time (`DebrisFlight` with drops flattening into puddles, `SparkFlight`, puffs, rings, flashes, `LightningArc`), seeded per event, so slow motion, hitstops, scrubbing and replays show the same effect; a new pass over the turn (`TimelinePlayer.PassCount`: a replay, a loop) clears what it will fire again. It also draws each swung weapon's fading trail from the recording (`WeaponTrail`; not the shield or a headbutt), flashes the struck part (`DummyView.SetPartFlash`) and flinches the struck dummy's drawing about its pelvis (`HitFlinch`, `DummyView.SetFlinch`; the physics never moves). Colours come from each side's `VfxStyle`, a cosmetic hit-effect set (§16; `MatchVfx.SetStyle`, the attacker's for a blow). Debris stays on the floor through planning until the next turn plays. `ElectricWallView` springs a wall up from the floor and, in real time, pulses it and crackles arcs up a live wall; `ArenaView` draws a neutral placeholder backdrop (§2 arena setting TBD: dark bands, a faint calibration grid, a stripe along the floor's edge).
- **Sound (Session 13, GDD §2 Decided: tense duel music, metal clashes, impacts, joint snaps, bolt scatter, wall zaps, comic effort grunts on attacks and impacts).** `SoundCueMapper` (pure) turns each played event into `SoundRequest`s of a `SoundCue`: a swing (`AttackStartedEvent`) whooshes and its attacker grunts (not for a shield); a lunge or jump takes an effort grunt; a hit's light, heavy or head impact (louder with more damage) and the victim's grunt or cry; a clash's rebound, crush or scrape; a full or partial block; a sever's snap, scattering bolts and cry; a death's split, bolts and KO scream; a dropped weapon; the wall's warning crackle and zap. `MatchAudio` plays them from swappable `SoundSetDefinition`s (an effects set and a grunt pack per dummy, the Audio asset's by default: grunt packs and hit effects are cosmetics, §16), a pool of effect sources, one voice per dummy (a KO scream cuts off an effort grunt, never the reverse: `SoundCues`), a short repeat guard, a little pitch jitter, deeper pitch in slow motion; a cue without a clip plays nothing and is only counted. The music loop plays during a match and stops at its end; adapting it is TBD (`IMusicIntensityDriver` → `SteadyMusicIntensity`, told each shown board). `AudioConfig` (a client tuning asset in the profile) holds the sets, the loop and the volumes. **XRim → Setup → Build Audio Sets From Incoming** (`AudioSetBuilder`) sorts the files in `Assets/XRim/Audio/Incoming/` by name (`AudioFileNaming`: a cue's snake-case name, then anything after an underscore; `grunt_…` to the grunt pack; `music…` the loop; the folder's README lists every name) into `Assets/XRim/Data/Audio/Effects.asset` and `Grunts.asset` and the Audio asset, and reports what is still silent. The match camera carries the audio listener. Batch 7 (sounds come from the designer, `Docs/SOUND_GUIDE.md`: a shopping list in priority order, where to find sounds and which licences are safe, recording grunts on a phone, trimming and converting with Audacity, naming, the menus click by click; `Docs/CREDITS.md` records each file's source): names are read forgivingly (case, spaces, dashes and dots between words, capitals starting words, a number straight after the name), and a name that matches nothing gets a suggestion (`AudioFileNaming.Suggest`: a typo fixed, or the sounds its first word belongs to); every file in the folder is imported with phone settings at every import (`AudioImportRules`, an `AssetPostprocessor`, from `AudioImportProfile`: effects and grunts mono and decompressed on load, the music stereo and streamed, Vorbis); **XRim → Setup → Open Sound Folder**; **XRim → Reports → Audio Coverage** (`AudioCoverage`: what plays, what is silent in priority order, the music, unusable files with the fix, files not built yet).
- **Camera (Session 13, GDD §2 camera behaviour TBD; designer 2026-10-11).** `ICameraDirector` → `FixedSideCameraDirector` (on the scene camera, configured by `MatchBootstrap`): a fixed side view that never pans, tilts or follows bodies, with three switchable parts in `FeelConfig`: the **framing** (`CameraFramingMode`: the scene's whole-arena view, or the default **duel framing**, `DuelFraming`: about 700 arena units tall, centred between the pelvises, widened only when the fighters need more room behind them, never beyond the whole-arena view, kept within 60 of the arena's edges, the floor 90 above the screen's bottom; set from each turn's first pose, gliding to the next board's framing after playback reaches its end, and never moving during planning, so touches always map to what the player sees); **shake** on impacts (`CameraShake`: fading tremors as a share of the screen height, seeded per event so a replay shakes the same way); and a **punch-in** toward big moments (`CameraPunch`: a sever or a death the most, a crush or a counter-hit a little, zooming about the moment's point). Framings live in the canonical arena and are placed through the arena root, so the view flip mirrors them.
- **Match HUD (Session 09).** `MatchHud` (never catches a touch): an HP bar under each dummy at the bottom, following the view flip and dropping as playback passes each hit or wall zap (`PlaybackHitPoints`), the turn counter, and the sudden-death banner (Session 12: the rule and which of its capped tries this is).
- **Electric walls on the board (Session 12).** `ElectricWallView` (Presentation/Arena, under the arena root so it flips with the view) draws each wall from every pose `TurnPlayback` shows (`TurnPlayback.PoseShown`): a pale amber bar for a warning, bright cyan once live, nothing when down; placeholder look until Session 13 or 17. The planning HUD says "ELECTRIC WALL BEHIND YOU" as planning opens while the player's own wall stands (`PlanningHudPresenter.WallBehindYouMessage`). `MatchScreens`: the mode picker, the hot-seat cover, the match-over screen. Both are fed by App presenters (`MatchHudPresenter`, `MatchLabels`, `MatchOverText`).
- **`TurnPlayback`** drives the two `DummyView`s from a `TimelinePlayer` and raises `Finished` once per play; a looping or paused turn never finishes, so the match waits on it.
- **`DummyView`** only copies recorded poses onto transforms, in the arena root's local space (so flipping the arena root mirrors the view), and shows the held weapon by id. Skins change visuals only. Until Session 17 it is the ragdoll prefab with its physics stripped (`VisualDummyFactory`). Lost limbs (Session 11): a limb the pose marks severed is drawn where it lies (`PoseSnapshot.SeveredLimbs`) or hidden while someone wields it (the club is drawn as the held item); a dropped item where it falls; a split torso as its two halves.
- **Legendary moments and the instant replay (Session 13; GDD pillar 1; §17: a local instant replay only, nothing saved or shared, designer 2026-10-11).** `IMomentDetector.TryDetect(TurnResult, out LegendaryMoment)` → `HeuristicMomentDetector` (pure, from the turn's events and poses only), most important first: a sudden-death finish (its first decisive hit or death; a tie is nobody's), a double KO, a KO (every one: the match-winning blow), a sever, the §1 dodge and counter (a crouch, jump or step back while the other swings a weapon, then a hit, taking none), a counter-hit (`AttackInterruptedEvent`, the interrupted side landing nothing), a same-instant trade, a crush; each kind switchable in `FeelConfig` (`MomentSettings`). A `LegendaryMoment` has its kind, time, hero (null for both), focus point (`MatchEventLocator`) and a plain reason. `MatchBootstrap`, when a live turn's playback finishes (not in the sandbox, never a turn twice, `FeelConfig._autoReplayLegendaryTurns`), starts `InstantReplay`: `TurnPlayback.PlayFrom` (`TimelinePlayer.PlayFrom`: the events before the start do not fire) from `ReplaySettings` lead before the moment, `FeelDirector.SlowWholeTurn` (the replay speed as a whole-turn window, so the moment's own slow motion still goes slower), `ICameraDirector.FocusOn` (the camera eases in on the moment and holds); every effect fires again (sparks, sounds, freezes, shake; `MatchVfx` clears the new pass's effects first); `MatchScreens.ShowReplay` shows a see-through screen with the banner ("INSTANT REPLAY", who did what from `MatchLabels.MomentCaption`, "tap to skip") that skips on a tap. The replay ends after its tail (once any freeze lets go) or on the skip, cuts to the turn's final board, and only then does the match move on (the planning timer, the match-over screen), so it never eats planning time: the authority's clock stands still while any replay plays (Session 13 batch 6: before that, a long KO replay could run out the 10 s wait for the playback report). While a replay plays every touch is the tap that skips it (`ReplayTouchBlocker`). A replay asked for between turns (the Playback tab, `MatchBootstrap.ReplayLastTurn`) plays the last turn's moment, or its hardest hit when it has none (`RequestedMoment`, `MomentKind.Requested`, caption "REPLAY"), over the planning phase, then shows the board being planned again. `InstantReplay.Cancel` drops a replay when the match is torn down or another turn plays.

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
- PlayMode: the hidden physics scene moves only when stepped and never moves the visual scene; the match scene smoke test (`MatchScenePlayModeTests`: three turns planned by scripts through the real `MatchBootstrap` and the project's tuning); the feel pass end to end (`FeelPassPlayModeTests`, Session 13: a whole match with the camera director, the feel layer, the effects, the sounds and the replay attached, steered by the cheats through a sever, a stun, a replay asked for with the timer frozen, a double KO into sudden death and a winning KO, with no error logged).
- **Bot-vs-bot batch (Session 09):** `BotBatchPlayModeTests` is `[Explicit]`: picked by hand in the Test Runner, it plays 20 random-bot matches on real physics, headless, and writes `Logs/XRimBotBatch.txt` (ends, turns, sudden death, hits, would-sever, simulation cost, match length estimates). It measures; it decides nothing (§1 target match length is TBD).
- Planned:
  - Scenario tests from saved (board, plans).
  - A same-inputs-twice repeatability check.
  - A headless bot-vs-bot batch that reports dismemberment rate and match length against `BalanceTargetsConfig`.

**Debug tooling** (Editor and development builds only):
- An in-game **Debug** button. Its **Tuning** tab edits every asset in the profile live; placeholders are orange and TBD fields say [TBD]. In the Editor, edits persist after Play mode.
- The **Playback** tab: speed 0.05×–2×, pause, frame step, scrub, loop, replay, re-simulate the last turn with the current tuning (display only), the turn's time on screen against its plan (Session 13: `FeelDirector.Pacing`, a `PacingBreakdown` of the turn, its slow motion and its freezes), the **instant replay of the last turn** on demand (between turns; a skip button while one plays), the last 8 turns' time on screen split into turn + slow motion + hitstops + the rest (the camera settling; `MatchBootstrap.RecentPacing`), and the turn's raw contact list. `MatchBootstrap` also logs `[XRim] Turn N … played in X s for Y s of turn (planned …)` after each playback.
- The **Device** tab (Session 10, `DevicePanel`): the device (model, OS, CPU, RAM, GPU, battery, scripting backend), the screen (resolution, refresh rate, dpi, frame-rate target, unsafe edges), frame statistics (fps, 95th percentile and worst frame over the last 120 frames), the hidden simulation's time per turn (`MatchBootstrap.SimulationTiming`, a `RollingStatistics` fed by the stopwatch in `RecordingTurnSimulator`: count, last, mean, 95th percentile, min and max), and the fingers on the screen (for the multi-touch test); "Log this report" writes it all to the log. On a phone the overlay is scaled from the screen's dpi (`ReferenceDpi` 200, at most 3×) and keeps clear of a notch. `MatchBootstrap` logs one `[XRim] Turn N … simulated in X ms` line per turn, and `DeviceLogWriter` keeps every `[XRim]` line and every error in `XRimDeviceLog.txt` in the app's data folder on the device (at most 1 MB a run, started over past 2 MB), so measurements outlive the system log's buffer.
- The **sandbox** (MatchBootstrap in `Sandbox` mode, Session 08): plan both sides hot-seat style, one side at a time through the real touch scheme (`TouchPlanSource`s: the mouse is a finger; drag in the arena to draw, flick in the body zone at the screen edge for a body move). Tab or the buttons switch the side played and flip the view as that player sees it; a dev toggle mirrors the layout (left-handed, D24). Real `PlanningCommand`s all the way, with the live path preview (`PathPreviewBuilder` + `PathPreviewView`: clipped at reach, cut at the ink, weapon width, red after a too-sharp turn, a reach ring while drawing, a faint copy at the body move's full extent), weapon pick and body-move buttons and keys (↓ crouch, ↑ jump, →/← lunge or step back toward or away from the opponent, N none) and Execute; simulation and body-move tuning apply from the next turn. At the bottom of the screen (`SandboxReadout`): each dummy's HP bar, each limb's damage against its durability (* = weapon arm, SEVER at 0) and its stun or KO; during playback the numbers change as playback passes each hit. **F9** saves the feel report (`FeelDiagnostics`: standing, tunnelling, kinematic vs motor, cost per turn; raw physics, without the hit rules) to `Logs/XRimFeelReport.txt`.
- The **Playback** tab lists the turn's hits, clashes, blocks and outcomes (`MatchEventText`: damage, limb damage, D27 clamp, stun or stagger, would-sever, clash angle, powers and outcome, block kind, cancelled attacks, KO) above the raw contacts, and toggles the gizmos (contacts, hit zones, the legendary moment).
- `GameplayGizmos` (Session 07): every contact a held item made in the turn being shown, as playback reaches it: the contact normal and the relative motion, labelled with the hit, clash (angle, hard or glancing, outcome, both powers) or block (kind, angle to the face, face position), the refined time-to-impact and the path distance. Session 13: the **hit zones** (`HitZoneOverlay`: every body of both dummies outlined where the pose on screen has it, from `DummyLayout` and the current tuning, each zone labelled with its multiplier and an arm's or leg's damage against its durability, red from half) and the **legendary moment** (`MomentMarker`: a ring where the detector found it, its kind, time and reason, once playback reaches it; the replayed moment during a replay; `TurnPlayback.ShownPose` is the pose they draw over). The sandbox's readout also notes knock-offs, rebounds, crush-throughs and blocks. A click without a drag in the sandbox is a tap.
- The **Cheats** tab (Session 13 additions): **freeze the planning timer** (`MatchBootstrap.IsPlanningTimerFrozen`, hot-seat too), **infinite ink** for both sides (`MatchBootstrap.InfiniteInk` swaps the match's `RulePolicies.InkCost` for a `FreeInkCostModel` around it, kept for later matches; reach, the execution hard cap and a too-sharp spear turn still apply), **force stun or stagger** (`MatchBootstrap.QueueStatus` → `ScriptedStatusContactHandler`: the status lands at the next turn's start through the match's status factory and a real `StatusAppliedEvent`, so the planning after that turn is limited as by a real one; a dummy knocked out first gets none), and **force a KO** of one dummy (`MatchBootstrap.QueueKnockOut`: the other wins, the match-winning replay). The handlers wrap one another: KO, sever, status, then the game's `HitContactHandler`.
- The **Cheats** tab (Session 12 additions): the **turn counter** (make the next turn played turn 30, so the turn cap's sudden death is one turn away: `MatchBootstrap.SetTurnCounter` → `LocalTurnAuthority.SetTurnIndexForDebug`), **force double KO** (`MatchBootstrap.QueueDoubleKnockOut` → `ScriptedKnockOutContactHandler`, wrapping the force-sever handler: both die at the next turn's start through the real death path), and each **electric wall**'s phase, position and backward swipes in a row. The Playback tab lists wall events (`MatchEventText`). **Force sever** (Session 11): pick a side and a limb; `MatchBootstrap.QueueSever` hands it to the match's `ScriptedSeverContactHandler`, which wraps the game's contact handler and cuts it at the start of the next turn through the real sever path (the limb falls, a held weapon drops, events and debris play). Cheats act through seams (the contact handler, clock, settings, the ink policy), never through rule code.
- `GameplayGizmos`, still planned: rigidity-invalid segments in red (paths and ink are drawn by the sandbox).
- `ScenarioStore` (planned): save and load a board plus both plans.

**Editor menus**
- **XRim → Setup:**
  - **Create Default Tuning Assets.** Safe to rerun; never overwrites tuned assets.
  - **Apply Project Settings.** Landscape only; Physics 2D in Script mode; the Android player settings of a device build (`AndroidPlayerSettingsApplier`: IL2CPP, ARM64, no system bars at start, render outside the safe area, optimized frame pacing, a valid package name).
  - **Create Match Scene.** The first playable (Session 09): camera with the fixed-side director, arena root, MatchBootstrap opening on the mode picker; first in the build list. Generated; running it again rebuilds it.
  - **Build Audio Sets From Incoming** (Session 13): the sound sets and music from `Assets/XRim/Audio/Incoming/`, by file name; safe with an empty folder. **Open Sound Folder** (batch 7) shows that folder in the file explorer.
  - **Create HUD Panel Settings.** The one PanelSettings asset the HUDs copy (Session 10); never overwrites it; Create Match Scene also makes it when missing.
  - **Create Sandbox Scene.** Generated by the same builder (`MatchSceneBuilder`), MatchBootstrap in Sandbox mode; appended to the build list.
  - **Fill New Tuning Fields.**
  - **Build Placeholder Dummies.** The 6- and 10-body ragdoll prefabs, from the live tuning.
  - **Reset Body Moves To Starting Values.** Asks, then overwrites every body-move asset (adding missing ones) from `GddStartingValues`; undoable.
- **XRim → Build (Session 10):** **Android Development Build** and **Android Development Build And Run** (`AndroidBuildMenu`): a development APK of the Match scene into `Builds/Android/XRim-dev.apk` (git-ignored), IL2CPP and ARM64 (the Mono variation of the Android player ships only 32-bit libraries); the DebugTools overlay is in it because a development build defines `DEVELOPMENT_BUILD`. The menu applies the project settings above, switches the Editor's build target to Android first when needed (it re-imports, then builds by itself), and prints the adb install command. "And Run" installs and starts it on a connected phone.
- **XRim → Reports:** **TBD Seams** and **Placeholder Values**, both read from the code attributes; **Audio Coverage** (Session 13, `AudioCoverageReport`): which sounds play, which are still silent, the music, and every file in the sound folder the game cannot use, with the fix; **Write Art Spec** (Session 13, `ArtSpecWriter`): writes `Docs/ART_SPEC.md` (`ArtSpecDocument`, pure) from the tuning profile: every dummy piece from `DummyLayout` (size, suggested pixels at 4 per arena unit, pivot, draw order, calibration-marker size), the joints and their ranges, the torso split and loose limbs, every held item from `HeldItemShape`, the debris and effect sprites from the effect code's sizes, the electric wall, the arena background's area (the most the camera can show), and the delivery folder `Assets/XRim/Art/Incoming/` and file names. The placeholder builder takes its draw order and marker size from `DummyLayout`, and a PlayMode test holds its bodies and joints to it.

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
| 1 | Target match length | `BalanceTargetsConfig._targetMatchLengthSeconds` (0 = undecided); measured, not decided, by the bot-vs-bot batch (Session 09) |
| 2 | Camera behaviour (designer 2026-10-11: the fixed side view stays the default; the duel framing, shake and punch-in are on, flagged) | `ICameraDirector` → `FixedSideCameraDirector`; `FeelConfig._cameraFraming`, `_shakeOnImpacts`, `_punchInOnBigMoments` |
| 2 | Adaptive music (designer 2026-10-11: a seam only, a steady loop) | `IMusicIntensityDriver` → `SteadyMusicIntensity` |
| 2 | Default arena setting (crash-test facility proposed; designer 2026-10-11: a neutral placeholder backdrop only) | `ArenaView` |
| 3 | Cancel Ready to edit | `MatchSettings.AllowReadyCancel` |
| 3 | Inputs that reset the forfeit counter | `IIdleTurnPolicy` |
| 3 | Order of end conditions in the same turn (not in the GDD) | `EndConditionEvaluator` |
| 3, 18 | Disconnects and reconnection | `ITurnAuthority` implementations (not yet designed) |
| 4 | Orientation | Project setting via Apply Project Settings (landscape assumed). Session 10 device facts: both landscape orientations work on a 20:9 phone (the notch moves with the turn, the HUDs re-check `Screen.safeArea`); the body strip sits in the empty margin beside the arena on such a screen (the dummy stands about a quarter of the way in), a feel question for PT4; edge-swipe system gestures were not testable (the test phone uses 3-button navigation). Still assumed, not decided |
| 4 | Placement of weapon selector, signature buttons, Ready (D23 default: selector along the top of the weapon zone, Ready in the far bottom corner, timer top centre) | `PlanningHud` (UI Toolkit layout in code), `SignatureSlot` left empty for Session 15 |
| 4 | Change handedness later; separate mirror toggle (D24 default: changeable in settings, Session 18; no separate toggle) | `IHandednessLayoutPolicy` → `MirrorWhenLeftHanded`; dev stand-in `InputConfig.DevHandedness` (layout only) |
| 4–5 | Overall feel of the dual-zone scheme | `IInputScheme` → `DualZoneInputScheme` (`InputConfig`: swipe window, minimum distance, `TapMaxTravelScreenFraction`, `TapInBodyZoneClearsMove`, `BodyZoneCoversDummy` (Session 13, on)) |
| 5 | Lean vs real step back (D12); lunge speed/damage bonus (D13) | `BodyMoveStats.IsLeanInPlace` (default: real step), `WeaponSpeedBonusFraction`, `DamageBonusFraction` (default: 0, reach only) |
| 5 | Diagonal or combined swipes (D14 default: not allowed; a flick in the gap between directions does nothing) | `ISwipeClassifier` → `FourWaySwipeClassifier`, `InputConfig.SwipeAxisToleranceDegrees` |
| 5, 12 | Moves after losing a leg; one leg / both legs (designer 2026-10-10, the default: no jump or lunge on one leg, no moves on none) | `IMobilityPenaltyPolicy` → `SettingsMobilityPenaltyPolicy` (`LimbLossSettings.LegLossPenalty`) |
| 5 | Does a crouch last into later turns (not in the GDD) | `BodyMoveStats.StandsUpFromLowStance` on the neutral move (default: a stance, stays low) |
| 6 | Does the path tilt with a body move's lean (not in the GDD) | `PathSettings.PathTiltsWithTorsoLean` (default: no) |
| 6 | Weapon stats, speeds, base damage (Session 14: sword and spear stay placeholders, playable; validated orderings: spear longest reach, shield damage lowest, rapier no stiffer than the spear, no weapon strictly stronger) | `WeaponStats` assets (placeholders; `DesignIsTbd`), `RulesSettings.Validate` |
| 6 | Keep or cut spear rigidity (designer 2026-10-15: keep, a per-weapon switch; the rapier's is off) | `IInkCostModel` → `RigidityInkCostModel`, `WeaponStats.Rigidity` (`RigiditySettings.Enabled`) |
| 6 | ~~Path start; reach limit; strokes per turn~~ Decided 2026-09-29 (D3–D5, see §13) | `IPathStartPolicy`, `IReachPolicy`, `IStrokePolicy` keep the decided rules |
| 6 | Drawing during lock-out | `MatchSettings.AllowDrawingDuringLockout` |
| 6 | Starting weapons (designer 2026-10-15: rapier, mace, shield; sword and spear unlockables), default loadout, shield slot required (not) | `LoadoutSettings`, `IWeaponOwnership` (`AllWeaponsOwned` until Session 18) |
| 6 | Remaining-ink UI (D7 default: shown while drawing) | `IPlanningHudView.ShowInkRemaining` fed by `PathPreview.InkRemainingFraction` |
| 7 | Shield block model (D20 default: full square on the face, partial at the rim or glancing); mace vs shield (D21 default: no special rule) | `IShieldBlockModel` → `SettingsShieldBlockModel` (`ClashSettings.ShieldBlock.Rule`, `HeavyWeapon`) |
| 7 | How the shield is held (not in the GDD; designer: like a shield, A3) | `TurnSimulatorOptions.ShieldAim` → `ShieldFaceAimModel`, `HeldItemShape` |
| 7 | Shield against shield (not in the GDD; no rule, A7) | `HitContactHandler` (open question) |
| 8 | Charge model; moves per match | `SignatureChargeMode`, `ISignatureChargeModel`, `SignatureSettings.MovesPerMatch` |
| 8 | Weapon ties, ink use, body-move interaction, catalogue | `SignatureMoveStats` fields |
| 8 | Opponent sees a charged move | `SignatureSettings.OpponentSeesCharged`, `IPublicStatePolicy` |
| 9 | ~~Which hits interrupt~~ Decided 2026-10-03 (D15: weapon arm or head); heavy swing armour (D16 default off) | `IInterruptPolicy` → `SettingsInterruptPolicy` (`DamageSettings.InterruptRule`), `WeaponStats.HasSwingArmour` |
| 9 | Does a resting weapon deal damage (not in the GDD; designer: no) | `DamageSettings.RestingWeaponsDealDamage` |
| – | Hits per weapon per turn (D26, not in the GDD; designer: drag-through) | `DamageSettings.MaxHitsPerWeaponPerTurn`, `WeaponStats.SpeedKeptAfterHitFraction` |
| 10 | Effect of a stagger (shares D17) | `IStatusEffectFactory` with `StatusKind.Staggered` (shared with the stun) |
| 10 | Repeated contacts between two weapons (D19 default: only the first resolves) | `IRepeatContactPolicy` → `SettingsRepeatContactPolicy` (`ClashSettings.MaxResolvedContactsPerWeaponPair`) |
| 10 | A weapon's speed in a clash when it is not travelling (not in the GDD; designer: 0, mass only, A1) | `WeaponContactResolver.SpeedOf` |
| 11 | HP scale, base damage, durability, stun threshold | `DamageSettings` (placeholders; D18 keeps them) |
| 11 | Effect of a stun (D17 default: no body move next turn) | `IStatusEffectFactory` → `SettingsStatusEffectFactory` (`DamageSettings.StunEffect`) → `IStatusEffect` edits next turn's `PlanningConstraints` |
| 11 | How "no instant KO from one head hit" is enforced (D27: no single hit from full HP to 0) | `IInstantKoPolicy` → `NoKoFromFullHpPolicy` |
| 13 | Does a knocked-back dummy stay there (not in the GDD; designer: yes) | `SimulationSettings.HitReaction.KnockbackPersists` |
| 12 | Armless body blows (default: the path steers the head; only the head is built) | `IArmlessAttackMode` → `HeadbuttArmlessAttackMode`; `body_blow` stats (placeholders); `HeadbuttLean` (how the body follows, a feel choice) |
| 12 | Severed limb stats and slot (default: placeholder stats, a temporary extra option; off: it takes the held weapon's slot, S4) | `severed_limb` stats, `LoadoutSettings.SeveredLimbIsExtraOption`, `DesperationOptions` |
| 12 | Retrieving a severed limb, out of reach included (default: automatic, anywhere) | `ILimbRetrievalPolicy` → `SettingsLimbRetrievalPolicy` (`LimbLossSettings.Retrieval`) |
| 12 | Whose limbs can be wielded; is a dropped weapon lost (not in the GDD; G1 anyone's, G2 not lost) | `LimbLossSettings.AnySeveredLimbCanBeWielded`, `DroppedWeaponIsLost` |
| 13 | Wall damage growth; damage when knocked into it (designer 2026-10-11, the defaults: flat; no) | `ElectricWallSettings.DamageGrowthPerConsecutiveRetreat`, `DamageWhenKnockedInto` |
| 13 | Wall geometry and damage model (not in the GDD; designer 2026-10-11, P1–P3) | `ElectricWallRules`, `ElectricWallSettings` |
| 13 | Arena width and edge behaviour | `ArenaSettings.WidthUnits`, `IArenaEdgePolicy` (D22 default: 2000, solid invisible stop) |
| 14 | Board state (carry over); no hit (repeat 3 times, then more HP when it began, then a seeded coin toss); exact tie (replay); wall (off); options (switching and signatures stay): designer 2026-10-11, the recommended defaults | `SuddenDeathSettings.BoardMode`, `MaxRepeatsWithoutHit`, `AllowWeaponSwitching`, `AllowSignatureMoves`; `ISuddenDeathNoHitPolicy` → `CappedRepeatNoHitPolicy`; `ITiePolicy` → `ReplayTiePolicy`; `ElectricWallSettings.ActiveInSuddenDeath` |
| 14 | Who wins a sudden-death turn when a death is not a hit, and what counts as a tie (not in the GDD; designer 2026-10-11, P4) | `EndConditionEvaluator`, `ExecutionReport.DeathTime` |
| 15 | Lifesteal and build-up traits (designer 2026-10-15: seams only; lifesteal: every 3rd hit pays 30% of those hits' damage, total counting, consecutive switchable; build-up +0.5% per HP taken, cap +50%: placeholders) | `IWeaponTrait` → `LifestealTrait`, `BuildUpTrait`; `TraitSettings`, `WeaponStats.TraitIds`, `RulePolicies.WeaponTraits` |
| 16 | Currency sources and amounts; premium earnable; farming | `IRewardPolicy`, `IFarmingGuard`, `EconomySettings` |
| 16 | Hit and impact effects and grunt packs as cosmetics (Decided as categories; the sets are not designed) | `VfxStyle` per side (`MatchVfx.SetStyle`); `SoundSetDefinition` per side (`MatchAudio.SetEffectsSet`, `SetGruntPack`) |
| 17 | Bots and practice (D25 default: dev opponents only, hot-seat and the random bot); replays and sharing (designer 2026-10-11: a local instant replay only, automatic and skippable, every KO replayed) | `IBotBrain`, `PlanSourceFactory`, `MatchMode`; `IMomentDetector` → `HeuristicMomentDetector`, `InstantReplay`, `FeelConfig._autoReplayLegendaryTurns`, stored `TurnResult`s |
| 18 | Target devices and performance budget | `DisplayConfig._targetFrameRate` (60, placeholder), the on-device overlay and `[XRim] Display` log line (Session 10: measured facts, no budget decided) |
| 18 | Networking model | `ITurnAuthority` |
| 18 | Arena unit scale and reference resolution | `ArenaSpaceConfig`, `InputConfig._referenceResolution` (the HUD's reference screen), `ScreenToArenaMapper` (scale from what the camera shows, so it needs no reference resolution) |

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
| 2026-10-03 | D15: only hits on the weapon arm or the head interrupt; D16, D17, D18, D27 on their recommended defaults (flagged) | Designer |
| 2026-10-03 | D26 drag-through: several hits per swing, each slower and softer (speed kept per weapon), each part once, at most 3 | Designer (Claude's proposal) |
| 2026-10-03 | A knocked-back dummy stays where it was knocked; a resting weapon deals no damage (both flagged) | Designer |
| 2026-10-03 | D19, D20, D21 on their recommended defaults (flagged): only the first contact between two held items resolves; full block square on the face, partial (×0.5) otherwise; no special mace rule | Designer |
| 2026-10-03 | D7, D14, D23, D24 on their recommended defaults (flagged): ink shown while drawing; no diagonal swipes; selector along the top of the weapon zone, Ready in the far bottom corner, timer top centre; handedness changeable later in settings, no separate mirror toggle | Designer |
| 2026-10-03 | Session 08 A1–A10: the mouse is a finger by touch simulation; a touch keeps the zone it landed in and a touch on a HUD control draws nothing; a body swipe fires as soon as it is recognised; a quick tap in the body zone clears the move (flagged); a weapon-zone tap is a one-point stroke; the stroke is sent when the finger lifts; the HUD shows the body move; dev handedness mirrors the layout only | Designer |
| 2026-10-04 | D25 on its recommended default (flagged): the prototype's opponents are dev-only: hot-seat with a cover screen and the random bot (plus bot vs bot to watch) | Designer |
| 2026-10-04 | Hot-seat timing (dev mode, not a GDD rule): sequential, each player alone with the full timer and the real lock-out; the timer stops behind the cover; Ready or running out ends a slot; the first planner alternates | Designer (Claude's proposal) |
| 2026-10-04 | Sudden death in the first playable: play the interim rules under a banner; a sudden-death turn both end dead stops the match on a placeholder screen until Session 12 | Designer |
| 2026-10-10 | Session 11 (§12 TBDs on their recommended defaults, flagged): leg loss no jump or lunge on one leg and no moves on none; armless body blows steered by the path as a headbutt; the severed-limb club with placeholder stats as a temporary extra option; retrieval automatic even out of reach. Not in the GDD: G1 any limb on the floor can be wielded; G2 a dropped weapon is not lost (the other hand holds it from the next turn); S1–S6 (a cut limb keeps its bend; a dead dummy goes limp and drops its weapon; the nearest limb is retrieved; S4's slot option; a split dummy revived for sudden death is whole; body-blow placeholders) | Designer |
| 2026-10-10 | Session 10: device builds are IL2CPP + ARM64 (Mono ships only 32-bit libraries); the Android package name is Unity's default `com.DefaultCompany.XRIM` (from the product name), a development placeholder until the store name is decided (§0, Session 22); the first test phones are relatives' Androids (a Samsung Galaxy A32 and a weaker OPPO) | Designer (approved the plan) |
| 2026-10-11 | Session 12 (§13, §14 TBDs on the recommended defaults, flagged): wall damage flat; no damage when knocked into it; the sudden-death board carries over; no hit: repeat 3 times, then more HP when sudden death began wins, level HP a coin toss from the match seed; exact tie: replay; walls off in sudden death; switching and signatures stay. Not in the GDD, P1–P6: the wall springs up 60 behind the pelvis when the step back ends and zaps on the owner's touch (throw forward 80, advance 50), never beyond the edge; flat damage that can knock out; any other move ends the sequence; the first decisive moment wins a sudden-death turn (a death without a hit loses; ties only at exactly the same instant); what each board mode keeps; every repeat counts toward one cap | Designer |
| 2026-10-11 | Session 13 (§2, §17 TBDs on the recommended defaults, flagged): the fixed side view stays the default camera, with shake and a punch-in on big moments; adaptive music a seam only; a neutral placeholder backdrop; a local instant replay only. Not in the GDD: the replay plays automatically and a tap skips it, the match-winning KO always gets one; F3 framing (the closer duel camera and a body zone that covers the player's own dummy); gameplay tuning T1–T5 (knockback 6 → 9 per mass, part kick 0.5 → 0.8, rebound 0.5 → 0.75, knock-off 0.5 → 0.8 and 0.15 → 0.25 s, sever pop-off 120/80/270 → 200/320/600) | Designer |
| 2026-10-11 | Session 13 batch 6: the authority's clock stands still while any replay plays (a replay never eats planning time or the playback report's wait); a replay asked for plays the turn's moment or its hardest hit; the cheats chain KO, sever, status around the game's contact handler. The art spec's conventions: draw facing right at 4 px per arena unit (the background at 2), pivots at the joints, lower-case file names with underscores in `Assets/XRim/Art/Incoming/`, one drawing for both left and right limbs, the background sized for a 20:9 screen (the numbers are placeholders until the first art) | Designer (approved the batch plan) |
| 2026-10-15 | Session 14 (§6, §15 TBDs on the recommended defaults, flagged): spear rigidity kept (per weapon; the rapier's off), sword and spear stats stay placeholders, starting weapons rapier, mace, shield, no required shield slot, traits seams only | Designer |
| 2026-10-15 | Session 14 (not in the GDD): a long weapon rests with its tip at most `RagdollSettings.GuardMaxTipDistanceUnits` (400) from the shoulder and its butt behind the hand (the spear's tip no longer sits below the floor, two spears start apart; the reach limit is unchanged); loadout picks are blind, then revealed, a bot picks at random; the sandbox keeps the default loadout; lifesteal and build-up readings and placeholders (see §12) | Designer (approved) |
| 2026-10-03 | Session 07 A1–A8: clash speed is the rules' path speed (0 when not travelling); blade or shield contacts beat body hits at the same instant; the shield is held like a shield and adds no reach; a tap is a short lead-in, then held; a partial block reduces every later hit that turn; knock-offs fly free then are held where they end, rebounds bounce back, a full block pushes the holder back; shield vs shield has no rule | Designer |

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
  - Electric wall, arena edge and sudden death (Session 12): `ElectricWallRules` (the §13 sequence at lock, the warning, the zap with growth and knocked-into flags), the wall as a physics body run by `TurnSimulator` through `TurnWalls` (raise, zap per instant, bounce, advance, back limit, owed zap) and drawn in every pose; sudden death's setup, repeats and board (`SuddenDeathSetup`, `SuddenDeathBoard`), `EndConditionEvaluator`'s first decisive moment with `ReplayTiePolicy` and `CappedRepeatNoHitPolicy`, death times in `ExecutionReport`; `ElectricWallView`, the banner, the wall note, the Cheats tab's turn counter, double KO and wall readout; `ElectricWallPlayModeTests` and the headless 30-turn stall.
  - Weapon roster and loadout (Session 14): the guard tip cap (`GuardStance.TipLocal`), roster orderings in `RulesSettings.Validate`, proofs that the sword and spear clash, block and bend through mass, speed and rigidity alone (`SwordAndSpearClashTests`, `WeaponRigidityPathTests`), the pre-match pick (`LoadoutSelection`, `LoadoutPickSession`, `LoadoutPickPresenter`, the picker and reveal screens), the random bot loadout, traits behind flags (`LifestealTrait`, `BuildUpTrait`), `RosterPlayModeTests`, `RandomLoadoutBotMatchTests`, a placeholder colour per weapon (`PlaceholderRagdollBuilder.HeldItemColor`).
  - Dismemberment and desperation (Session 11): `SeverRules`, `DesperationOptions`, the leg-loss, retrieval and armless policies, the off hand, the club and the body blow as planning options, `TurnBoard` around each turn, severs, drops, the limp death and the torso split in the simulation and the Unity world, the leaning headbutt, two-piece floor limbs that ignore their former owner, the pop-off, `DummyView` drawing lost limbs, `MatchVfx` placeholder debris, the HUD's extra buttons, the force-sever cheat, `DismembermentPlayModeTests`.
  - Device build (Session 10): `check.py` compiles the player passes against the Android IL2CPP engine and verifies its development defines against a real device build; `XRim/Build/Android Development Build` (and And Run) with `AndroidPlayerSettingsApplier`; the HUDs' PanelSettings copied from an Editor-made asset (UI Toolkit text needs ICU data in a player); `DisplayConfig` frame-rate target; safe-area insets in every HUD (`ScreenInsets`, `HudSafeArea`); the Device tab, per-turn simulation timing (`RollingStatistics`) and the device log file; run on a Samsung Galaxy A32 (simulation 58 ms mean, 100 ms worst per turn).
  - First playable (Session 09): the Match scene with the mode picker (hot-seat with the cover screen, vs the random bot, bot vs bot), `MatchBootstrap` composing each match fresh (Rematch, Change mode), the client flow reporting playback, the hitstop, the match HUD (HP bars, turn counter, sudden-death banner), the match-over screen, the sudden-death tie placeholder, the PlayMode smoke test and the explicit bot-vs-bot batch.
  - Touch input and planning HUD (Session 08): `ScreenLayout` (zones, mirroring, the forward direction on screen), `FourWaySwipeClassifier` (D14), `ScreenToArenaMapper` + `ArenaViewFrame` (resolution-independent, flip-aware), `TouchInputReader` (EnhancedTouch with history, mouse as a finger), `DualZoneInputScheme`, `TouchPlanSource` + `PlanDraft`, the live path preview (`PathPreviewBuilder`, `PathPreviewView`: reach clip, ink cut, weapon width, red invalid tail), `PlanningHud` (UI Toolkit in code, D23) with `PlanningHudPresenter`, and the sandbox planned through all of it (hot-seat toggle with view flip, left-handed toggle, timer checkbox).
  - Clashes and shield (Session 07): `ClashResolver` (two stages), `WeaponContactResolver` (clashes, blocks, D19, stagger), `SettingsShieldBlockModel` (D20, D21), the partial-block damage modifier, `HitContactHandler` resolving blade and shield contacts before hits with knock-off, rebound and push-back reactions, the shield held like a shield (`ShieldFaceAimModel`, `HeldItemShape`), taps, contact gizmos and the clash and block lines in the debug panels.
  - Hits, priority and damage (Session 06): `DamageCalculator`, modifiers, limb cap, D27, stun and status lifetime, `HitResolver` (priority, trades, D15/D16 interrupts, D26 drag-through, E2), `HitContactHandler` (contacts to hits, weapon slow/stop/hand-off/recoil, part impulse, knockback), sandbox HP/limb/stun readout and the Playback tab's outcome list.
  - Turn simulation and playback (Session 04): `TurnSimulator` (two fighters, contacts to the `ITurnContactHandler` seam, settle or hard cap), `Unity2DPhysicsWorld` complete for two fighters (edges, severed-limb placeholders, fresh scene per load), stance persistence (`StartingBoard`, `GuardStance`, `PoseWeaponTipLocator`), `MatchBootstrap` running the local loop, `TurnPlayback`, the Playback tab, the sandbox and the feel report.
  - Match loop and planning rules (Session 03, headless): `PlanningSession` (every command, lock-out, Ready and cancel, timeout), `PlanValidator`, `EndConditionEvaluator` (D11 order), `SuddenDeathSetup`, `MatchStateMachine` (all phases), `LoadoutValidator`, `LocalTurnAuthority` with the playback hold, `RandomBotBrain` and `BotPlanSource` (valid plans only). Placeholders inside it: the guard-stance weapon tip (`GuardStanceWeaponTipLocator`, until Session 04 reads the pose) and the sudden-death seams (Session 12).
- **Placeholders that throw `NotImplementedException`:** the remaining gameplay logic.
  - Presentation (Session 13, complete): hitstops, slow motion, the camera, the mechanical effects, the sound (and the designer's sound guide and import aids), legendary moments and the instant replay, the cheats, gizmos, pacing record and art spec are in. Real sounds and art come from the designer (`Docs/SOUND_GUIDE.md`, `Docs/ART_SPEC.md`).
  - Tooling: scenarios.
