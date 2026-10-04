# X-RIM Memory

The living memory shared by every Claude Code session. Read it first; update it after every batch.
Keep this file under about 300 lines. When it grows, compress each completed session's detail into one line in the work log.
The GDD is never edited: decisions and deviations are recorded here. The build plan is `Docs/SESSION_PLAN.md`; session prompts are in `Docs/sessions/`.

---

## Current status

- **Session 00 (build planning):** batch 2 of 3 done. Batch 3 (session prompt files 12–22) is still open; it does not block sessions 01–11.
- **Last completed: Session 08 (touch input and planning HUD), 2026-10-03 to 2026-10-04**, 5 batches (work-log entry below). **Not committed yet: the designer commits the whole session.** Before it: Session 07 (`bf517d6`).
- **Next: Session 09 (first playable)**, `Docs/sessions/session-09-first-playable.md`; decide D25 (prototype opponent) first. See *Open questions* → *Wiring Session 08 leaves*. **PT3** follows Session 09.
- Unity-side tests (Input, App, Presentation) are listed as "needs Unity" by `check.py`. When the code under test is pure, Claude also runs them with the check tool's own runner by promoting the module (a scratch script, not in the repo); the designer runs them in the Editor.
- PT1 reported 2026-10-01, PT2 reported 2026-10-03 (see *Playtest reports*). Next checkpoint: PT3 after Session 09.
- **Git (designer, 2026-10-01): no commits after batches.** The designer commits once when a session is done; each batch summary lists the files it changed. This overrides the commit step in the session prompts. Commit only if truly required, and say so first. Never push; no attribution.
- **Compile-check tool: `python Tools/check.py`** (from the project root; about 6 s, 25 s cold). Run it after every batch; it must print `CHECK PASSED`. Options: `--pass editor|player|dev|all`, `--no-tests`, `--filter TEXT`, `--verbose`. Usage, passes, define lists and limits: `Tools/README.md`.
  - Limits: Windows 64-bit Mono is the player proxy (Android is used automatically once installed); package reference DLLs are Editor builds; PlayMode tests, `[UnityTest]` and tests of Unity-side modules (Config, Input, Presentation, App) are listed as "needs Unity" and must be run in the Editor.
  - Which tests run: those whose namespace names an engine-free module (`XRim.Tests.EditMode.<Module>…`). Keep test namespaces matching their folders.

---

## Completed work log

- **Session 08 (touch input and planning HUD), 2026-10-03 to 2026-10-04, complete; not committed yet (the designer commits it).** Plan, D7/D14/D23/D24 defaults and A1–A10 approved 2026-10-03 (5 batches). EditMode run by `check.py` 652 → 662; Unity-side EditMode tests 19 → 145 (Input 3 → 112, App +15, Presentation +2), all passing when run by hand; PlayMode unchanged at 47.
  - Input: `ScreenLayout` (screen height, `ForwardScreenSign`, `HorizontalMove`, `WithScreenSize`), `SwipeSettings` + `FourWaySwipeClassifier` (0.3 s, 3% of the screen height, 30° axis tolerance: D14's gap does nothing; `ISwipeClassifier` returns `BodyMove?`), `ArenaViewFrame` + `CameraArenaView` + `ScreenToArenaMapper` (scale from what the camera shows, flip undone, torso frame), `TouchSample`/`ITouchSource`/`ITouchBlocker`, `TouchInputReader` (EnhancedTouch with each contact's history, ended contacts never re-read, `MouseActsAsTouch`), `DualZoneSettings`, `DualZoneInputScheme` (zone fixed at landing, HUD touches ignored, body flick fires at once, body tap clears (flagged), one stroke finger sent on lift ending at the lift point, tap = one-point stroke, cancelled touch sends nothing, two thumbs, window resizes), `PlanDraft` (+ `RejectionCount`) + `DraftTrackingSink`, `TouchPlanSource` (`Draft`, `Send`, `IsActive`, `Layout`, `ActiveStroke`); `InputConfig` + `SwipeAxisToleranceDegrees`, `TapMaxTravelScreenFraction`, `TapInBodyZoneClearsMove`, `DevHandedness`.
  - Rules: `BuiltPath.InvalidTail`; `PathPreview` + `PathPreviewBuilder` (same `PathBuilder` as the authority: clip, ink cut, thickness, ink left, red tail, reach). Presentation: `ArenaLines`, `PathPreviewView`, `IPlanningHudView`, `ReadyButtonState`, `RejectionMessages`, `PlanningHud` (UI Toolkit in code, runtime `PanelSettings`, built-in font, D23 layout, `SignatureSlot`, `IsOverControl`). App: `PlanningHudPresenter`, `HudTouchBlocker`, `MatchBootstrap.AuthorityNowSeconds`.
  - Sandbox (`SandboxPlanSource`, `SandboxPathDrawer` deleted): two `TouchPlanSource`s on one reader, Tab plays the other side and flips the view, left-handed toggle, HUD on the side played, timer checkbox, panel below the HUD's top row, HP boxes follow the flip.
  - Batch 5 (2026-10-04): fix from the designer's Editor run: the sandbox's arrow keys decided forward from the arena side, so after the hot-seat flip → stepped the right fighter back; keys and swipes now share `ScreenLayout.HorizontalMove` (+4 tests). HP boxes follow the flip; MatchBootstrap notes; ARCHITECTURE status, TBD register, decisions.
- **Session 07 (clashes and shield), 2026-10-03, complete; committed by the designer (`bf517d6`).** 6 batches; D19–D21 defaults and A1–A8 approved 2026-10-03. EditMode run by `check.py` 539 → 652 (+19 need Unity), PlayMode 42 → 47. `ClashResolver` (§10 two stages), `WeaponContactResolver` (clashes and blocks, D19 repeat count, stagger via the D17 factory), `SettingsShieldBlockModel` (D20/D21, `ClashSettings.ShieldBlock`) with the partial-block damage modifier, `HeldItemShape` and `ShieldFaceAimModel` (the shield held as a shield, taps, `WeaponStats.ReachBeyondHandUnits`), `HitContactHandler` resolving held-item contacts before hits with knock-off, rebound and push-back (`SimulationSettings.ClashReaction`, `WeaponStopKind`), a fix so two held items touching use the later refined time, `GameplayGizmos` and clash/block lines in the debug panels, `ClashPlayModeTests` (5). Details: git log and *Deviations*.
- **Session 06 (hits, priority and damage), 2026-10-03, complete; committed by the designer (`d5dd74e`).** EditMode 414 → 539, PlayMode 37 → 42. `DamageCalculator` (base × zone × `RulePolicies.DamageModifiers`), limb cap, D27 `NoKoFromFullHpPolicy`, status effects (D17) with lifetime in `MatchStateMachine.PrepareTurn`, `HitResolver` (priority, same-instant trades, D15/D16 interrupts, D26 drag-through, E2), `HitContactHandler` (contacts to hits; slow, stop, hand-off to physics, recoil; part impulse and E1 knockback), `SandboxReadout`, Playback outcome list, `HitPlayModeTests` (5; the knockback test's mace path ends 15 past the chest). Details: git log.

- **Session 05 (body moves), 2026-10-02 to 2026-10-03, complete; committed by the designer (`78cee9d`, `038b714`).** EditMode 359 → 414, PlayMode 26 → 37. `BodyMoveStats` from data played by `StanceBodyMoveDriver` (x step that stays, low stance held, hop, lean, stride, D12 lean in place), `NeutralBodyMoveDriver`; `LegGeometry`/`LegPoser` + `IPhysicsWorld.SetLimbTarget`; root and soles kept inside solid edges; path frame at the turn-start angle (`PathTiltsWithTorsoLean`); D13 speed; `BodyMoveStartedEvent`; sandbox move keys and ghost path; `XRim/Setup/Reset Body Moves To Starting Values`. Measured pelvis lag behind its root: crouch 28.5, jump 36.4, lunge 13.1. Details: git log.
- **Session 04 (turn simulation and playback), 2026-10-01 to 2026-10-02, complete; committed by the designer (`f239938`, `d01fe56`).** EditMode 312 → 359, PlayMode 17 → 26. `TurnSimulator` (fixed steps on `SimClock`, body moves and weapon drivers in the torso frame, contacts refined, ordered and handed to `ITurnContactHandler`, ends Settled or HardCap), `Unity2DPhysicsWorld` (fresh hidden scene per load, floor, D22 edges, held items with full kinematic contacts), `GuardStance`/`StartingBoard`/`PoseWeaponTipLocator`, `MatchBootstrap` local loop, `TurnPlayback`, Playback tab, sandbox, F9 feel report; spike retired. Details: git log.
- **Session 03 (match loop and planning rules), 2026-10-01, complete.** Headless, engine-free; commits `d5dd47c`, `2c1a1ea`, `63a17f4`, `eb87585`, `7102dc8`. `PlanningSession` (every command, lock-out boundary included, Ready/cancel per D9, timeout executes what is set), `PlanValidator` + `PlanningTiming` (validates the path `PathBuilder` makes), `EndConditionEvaluator` (D11 order; turn cap fires after turn 30, never inside sudden death; `BothForfeitRule`), `SuddenDeathSetup` (HP 1, idle counters reset), `MatchStateMachine` (planning timer starts at `BeginPlanning`; lock on both Ready or each side's own timer; failing plan → idle plan with the session weapon; `CurrentWeapon` set at lock), `LoadoutValidator`, `LocalTurnAuthority` (simulator runs in `Tick`, one turn per call; playback hold modes None / TimelineDuration / ClientReport), `RandomBotBrain` (always valid, never idle). EditMode run here 142 → 312.
- **Session 02 (feel spike), 2026-09-29 to 2026-10-01.** Kinematic and motor weapon drivers, aim from the shoulder, impact-time refinement, contact angle, placeholder ragdoll (6 or 10 bodies) and the full `Unity2DPhysicsWorld`; spike loop, scene and harness retired in Session 04. EditMode 153, PlayMode 17, green in the Editor 2026-10-01. Details: git log.
- **Setup (2026-09-29, `a8f4ff8`, `63c81d4`, `ba7402f`) and Session 00 (build plan, 2026-09-29):** 15 asmdefs, settings and Config SOs, the debug overlay, Editor menus, ARCHITECTURE.md, CLAUDE.md; the 22-session plan and session prompts.
- **Session 01 (tooling and path rules), 2026-09-29.** `Tools/check.py` (Unity's Roslyn; editor, player and dev passes; engine-free guard; NUnit runner on .NET 8; warnings fail). `Rules/Paths/`: `PathResampler`, `LengthInkCostModel`, `RigidityInkCostModel` (§6 as written), `InkCutoff`, D3–D5 policies, `PathBuilder` (stroke → lead-in → resample → reach clamp → ink cut → break cut); `XRim > Setup > Fill New Tuning Fields`. Details: git log.

---

## Session 02 spike findings (2026-10-01; F9 report + the designer's play)

Retired in Session 04; the same report now runs on the real turn loop with F9 in the sandbox (`Logs/XRimFeelReport.txt`). Measured with the old −20° guard and 6 bodies by default.

- **Standing** (a dynamic torso pulled to an invisible kinematic root anchor by a strength-limited `RelativeJoint2D`, upright included, plus hinge-motor servos on every joint, all solved inside Box2D): 6 bodies settle in 0.054 s, 10 in 0.079 s, both stay settled; pelvis drift 1.5 units; torso tilt 0°.
- **Tunnelling at 240 Hz, rapier 10 wide:** no misses at 1×–8× rapier speed (up to 30 units per step); even 60 Hz caught every one (at 60 Hz ×8 the refined distance was 6 units late). Not tested then: blade against blade, thin limbs side-on.
- **Drivers:** kinematic is exact on its path (end error 0, cheapest) but ignores weapon mass (the rapier knocked the target further than the mace); motor tracks within 5 units and shows mass (rapier 24 units of knockback, mace 42) but a deflected rapier kept pushing and never settled (needed D26). One thrust produced 8 contacts as the target reeled → D19.
- **Cost in the Editor:** 8.3 ms (6 bodies) to 14.8 ms (10 bodies) per simulated swing, worst 18.4 ms, 192–360 steps.
- **D1 (kinematic, then hand the blade to physics when the rules stop it) and D2 (10 bodies)** were recommended and are decided (see *Decisions*).

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

| 2026-10-03 | **D19, D20, D21: recommended defaults as flagged seams.** D19: only the first contact between the two held items in a turn resolves (weapon–weapon and weapon–shield share the count); later ones pass through (physics only). D20: full block for square hits on the shield face, partial (×0.5 placeholder) for rim or glancing hits; "always full" and "always partial" switchable. D21: no special rule (the block holds, the holder is pushed back); "a heavy weapon staggers the holder" and "a heavy weapon breaks through" switchable (heavy = mass ≥ 10, placeholder). | Designer (approved the defaults) | `IRepeatContactPolicy`, `IShieldBlockModel` (Session 07) |
| 2026-10-03 | **Session 07 A1–A8 approved (not in the GDD).** A1: clash speed v = the speed the rules move the weapon along its path now (stat × D13 bonus × D26 share left), 0 when not travelling; mass always counts; a clash needs at least one weapon travelling. A2: at the same instant, weapon–weapon and weapon–shield contacts resolve before body hits. A3: the shield is held like a shield (path = its centre's path, face turned outward square to the arm, adds no reach, lead-in from its centre). A4: a tap is a short lead-in (D3) to the tapped spot, then the shield holds there for the whole execution. A5: a partial block's ×0.5 applies to every later hit of that weapon this turn (`ShieldBlockDamageModifier`, stacks with crush-through and follow-through); a full block stops the weapon. A6: knocked-off weapons fly free briefly then are held where they ended; rebounds and full blocks bounce back then hold backed off by the recoil; a full block pushes the holder back (attacker mass × knockback × fraction); survivors continue unchanged; a stagger is only the D17 status. A7: shield vs shield has no rule (open question). A8: shield values nested in the Clash asset, reactions in Simulation, D19 as a clash setting. | Designer | Session 07 |

| 2026-10-03 | **D7, D14, D23, D24: recommended defaults as flagged seams** (Session 08). D7: remaining ink shown while drawing (bar + the line stops at the budget). D14: no diagonals; a flick in the dead wedge between two directions does nothing. D23: weapon selector along the top of the weapon zone, Ready in the far bottom corner, timer top centre. D24: handedness changeable later in settings (Session 18), no separate mirror toggle; a dev setting for now. (D4 was already decided 2026-09-29.) | Designer | `PlanningHud.ShowInkRemaining`, `ISwipeClassifier`, `PlanningHud` layout, `IHandednessLayoutPolicy` (Session 08) |
| 2026-10-03 | **Session 08 assumptions A1–A10 and architecture changes approved** (plan below in *Current status*): mouse = finger by touch simulation; a touch keeps the zone it landed in and a touch on a HUD control draws nothing; a body swipe fires at 3% of screen height within 0.3 s, 30° axis tolerance (placeholder); a tap in the body zone clears the body move (flagged); a weapon-zone tap is a one-point stroke; the stroke is sent on lift with a local preview; body-move indicator in the HUD; dev handedness in `InputConfig` mirrors the layout only; the old sandbox mouse drawing is replaced by the new scheme; HUD PanelSettings created at runtime. Input references Simulation, `IInputScheme.Begin` gets the window, `ISwipeClassifier` returns "no swipe" or a move, `PlanningHud` has an event API, `MatchBootstrap` exposes the authority time, `BuiltPath` gains the invalid tail. | Designer | Session 08 |

Other prototype decisions (D12, D13, D16, D25 in SESSION_PLAN.md §4) are **not decided yet** (D12 and D13 run on their flagged defaults since 2026-10-02, D16 since 2026-10-03, D19–D21 since 2026-10-03, D7, D14, D23, D24 since 2026-10-03). Add a row here for each one the designer decides, with the date. Undecided items are built with the recommended default as a flagged `[GddTbd]` seam.

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

- Session 07 batch 4 (`SimulationSettings.ClashReaction`): `ReboundSpeedFraction` 0.5, `KnockOffMomentumFraction` 0.5 (a mace swats a rapier back at 625, a rapier pushes a mace sideways at 90), `KnockOffFreeSeconds` 0.15, `BlockKnockbackFraction` 0.5 (a full block pushes the holder back 30 for a mace, 6 for a rapier, plus half a hit's impulse on the torso).
- Session 07 batch 2 (`ClashSettings.ShieldBlock`): `PartialBlockDamageMultiplier` 0.5 (D20's suggestion), `SquareHitMinAngleDegrees` 30 (the clash threshold), `RimFraction` 0.2 (outer fifth of the face at each end), `HeavyWeaponMinMass` 10 (only the mace; used only by D21's options).
- Session 08 (`InputConfig`): `SwipeAxisToleranceDegrees` 30 (how far off a straight line a body swipe may be; D14 dead wedge between directions), `DevHandedness` Right (developer stand-in for the first-launch pick; mirrors the layout only). Batch 2: `TapMaxTravelScreenFraction` 0.01 (finger jitter allowance for a tap), `TapInBodyZoneClearsMove` true (flagged, A4); stroke point spacing 1 arena unit (a named constant in `DualZoneInputScheme`). Existing: `MinSwipeDistanceScreenFraction` 0.03, `SwipeWindowSeconds` 0.3, `BodyZoneWidthFraction` 0.15. The Input asset shows the new fields with these defaults. Batch 4: HUD sizes, colours and the 2.5 s refusal note are technical constants in `PlanningHud` (laid out on a 1920×1080 reference screen scaled by height), not gameplay numbers.

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

- **Session 08 (approved 2026-10-03):**
  - Batch 1: `XRim.Input` now references `XRim.Simulation` (for `BodyPose`, `TorsoFrame`, later `BoardSnapshot`); `ScreenLayout` constructor takes the screen height; `ISwipeClassifier.Classify` returns `BodyMove?`; `FourWaySwipeClassifier` takes `SwipeSettings`; `ScreenToArenaMapper` is stateless (no config/space; the scale is `ArenaViewFrame`); new `SwipeSettings`, `ArenaViewFrame`, `CameraArenaView`. No implementations existed before. New `.cs` files get their `.meta` files when Unity next opens the project.
  - Batch 2: `IInputScheme` gained `SetLayout`, `CancelGestures`, `ActiveStroke` and `Begin` takes the window and layout (no implementation existed); `DualZoneInputScheme`'s constructor takes the touch source, classifier, `SwipeSettings`, `DualZoneSettings`, mapper, a view function and an optional `ITouchBlocker`; `TouchPlanSource` gains `Draft`, `Send`, `IsActive`, `Layout`, `ActiveStroke`; `TouchInputReader` implements `ITouchSource`. Limitation: a stroke still being drawn when the planning timer ends is not part of the plan (the old stroke stays).
  - Batch 3: `BuiltPath` gained `InvalidTail` (optional constructor parameter); new Rules `PathPreview`/`PathPreviewBuilder`; new Presentation `ArenaLines`/`PathPreviewView` (`DebugLines` delegates to `ArenaLines`); `SandboxPlanSource` and `SandboxPathDrawer` retired; `SandboxReadout` reads `TouchPlanSource`s.
  - Batch 4: `PlanningHud` reshaped (event API through `IPlanningHudView`: `WeaponChosen`, `ReadyPressed`; `ShowOpponent` replaces `ShowPublicState`; `Create` builds the document and panel settings in code); `MatchBootstrap` exposes `AuthorityNowSeconds`; new App `PlanningHudPresenter`/`HudTouchBlocker`; the sandbox panel moved down so it does not cover the HUD.
  - Batch 5: `ScreenLayout.HorizontalMove(screenDx)` is the one rule for a sideways input (swipe or key); `SandboxReadout.Draw` takes the view flip.

- **Session 07 (approved 2026-10-03):**
  - Batch 4: `IWeaponDriver.Stop` gains an optional knock velocity; `WeaponStopKind` + `KnockedOff`, `Rebounded`; `ITurnActions.StopWeapon` gains the knock, plus `HeldItemPoseAt(side, time)`; `PathWeaponDriver`, `KinematicPathDriver`, `MotorPathDriver` take `ClashReactionSettings` (optional on the concrete drivers); `SimulationSettings.ClashReaction`; `WeaponContactResolver.SpeedOf` public; `TurnSimulator` refines a held-item pair to the later arrival (a fix, see the work log).
  - Batch 3: new engine-free `HeldItemShape` is the one layout of a held item (physics box, drawing, impact sweep, path point); `BladeShape` gains `Of(WeaponStats)` (old constructor kept); `TurnSimulatorOptions.ShieldAim`; `GuardStance.Create` and `StartingBoard.Create` gain an optional `shieldAim`; `WeaponStats.ReachBeyondHandUnits` (computed, not serialized) feeds `ReachLimit`; `IWeaponAimModel` docs: the rotation is the body's (a shield: the way its face looks).
  - Batch 2: `BlockFacts` (angle against the face, face position, attacker weapon and rules speed, shield), `BlockResult` (stopped, damage multiplier, holder staggered), `IShieldBlockModel.Resolve(facts, ShieldBlockSettings)`, `ShieldBlockEvent` (+ point, angle, face position) reshaped; `DamageContext` gains `shieldBlockDamageMultiplier` (last optional parameter); `TurnAttack.ShieldBlockDamageMultiplier`; `AttackStop.Blocked`. No implementations existed.
  - Batch 1: `ClashResult` reshaped (no `WinnerDamageMultiplier`: the crush-through modifier reads the settings; adds both powers and the angle); `WeaponClashEvent(time, result, point)`; `IRepeatContactPolicy.ShouldResolve(earlierResolvedContacts, ClashSettings)`; new `WeaponContactResolver` sharing the turn's attacks with `HitResolver`; `AttackStop` + `KnockedOff`, `Rebounded`. No implementations existed.

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

- Session 08: on a narrow screen (4:3 tablets) the HUD's weapon selector, which starts at the body zone's edge, can run into the centred timer; 16:9 and wider phones are fine. Safe areas and aspect ratios are Session 10. Not yet confirmed in the Editor: HUD text without a theme asset, and the screen-to-panel Y convention in `PlanningHud.IsOverControl` (a HUD click that also drew would mean it is flipped). The sandbox reads the swipe and tap numbers once, at Play.
- Unity 6.5 conventions measured in the Editor (2026-10-01): contact normals point from `ContactPoint2D.collider` to `otherCollider` (confirmed, `ThrustIntoTorso_…` green); `HingeJoint2D.jointAngle` grows **clockwise** for the jointed body relative to its parent (measured −30 for a +30 forward swing), so `Ragdoll.JointAngleGrowsCounterClockwise` is false and limits are flipped when applied.
- Session 04 batch 1: `Assets/XRim/Data/Tuning/Simulation.asset` never stored `WeaponDriver` or `Segmentation`, so it now loads the new code defaults (kinematic, 10 bodies). If the Editor shows 6 bodies, the asset was saved with the old value; switch it in the tuning panel. The Session 02 PlayMode tests build `new SimulationSettings()`, so they now run with 10 bodies by default.
- The spear (placeholder length 500, design TBD) at the −40° guard has its tip below the floor (kinematic, so nothing happens physically). Not in the default loadout; Session 14 (roster) should give long weapons their own guard or length.
- Session 05: the body-move assets were reset to the Session 05 starting values by the designer (2026-10-03, `XRim/Setup/Reset Body Moves To Starting Values`; `None.asset` added to the profile). After a future change to `GddStartingValues.CreateBodyMoves`, run that menu again.
- Session 05: crouching from the low guard (−40°, Session 04) pushes the blade tip about 47 units below the floor (tip 23 above it when standing). The weapon is kinematic, so nothing happens physically, but it is visible. Possible fixes (the designer's call, PT2): a higher guard, or a held weapon that keeps its tip above the floor.
- Session 05 (feel note for PT2): the body trails its move: a crouch's pelvis is up to 28 units behind its target as the descent ends (the knees fold slower than the hips sink), a jump's up to 36 at take-off (the root starts at full upward speed). Every move still reaches its extent within 0.05 s of its duration. If ducks feel late, raise `RagdollSettings.JointServoGainPerSecond` or the root-drive strength, or shorten the crouch.
- Session 05: the two-segment leg is very sensitive near full stretch, so a pelvis frozen a unit or two below standing height bends the knees a few degrees on the next turn (the soles are kept on the floor). Physically right; watch it at PT2.
- Session 04: a dropped weapon disappears from the captured pose, a severed limb's visual stays attached on the `DummyView`, and the off hand never takes over the weapon: all Session 11 (dismemberment).
- PT1 note: in the spike the weapon is not jointed to the body (the arm follows it by a hand spring), so a heavy swing does not drag the dummy. If PT1 wants that comedy, add it later as a tuned reaction/knockback rather than a joint.

- Session 07 (feel notes for PT2): kinematic blades that both carry on (a slide, a partial block at the rim, a crush winner over the loser) pass through each other; a knocked-off blade flies limp (gravity on) before the hand takes it; a still blade or shield is only an obstacle for a dynamic (released) weapon.
- Session 07 (feel note for PT2): switching to the shield keeps the old weapon's grip pose, so on the first step of the shield's path its rotation snaps to face outward (one step, 1/240 s). A tap exactly on the shield's centre is an empty path (refused, as before).
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

- Prototype decisions D1–D27 in `Docs/SESSION_PLAN.md` §4 (D3, D4, D5 decided 2026-09-29; D1, D2, D22 decided 2026-10-01; D12 and D13 on flagged defaults since 2026-10-02; D15 and D26 decided, D16, D17, D18, D19, D20, D21, D27 on flagged defaults since 2026-10-03).
- **Session 07 (A7):** shield against shield has no rule (both carry on; physics only). Should two shields meeting do something (a rebound, a shove)?
- **Session 08 (found in batch 2):** the body zone is the 15% strip at the screen edge (GDD §4: "covering the player's own dummy"), but with the sandbox camera (ortho 5.75, 16:9) and the 700 starting gap the dummy stands about a third of the screen in, not inside the strip. Camera framing or the gap is Session 10/13 territory; tell Claude if the strip should follow the dummy instead.
- **Session 08 (to confirm):** a stroke still being drawn when the planning timer ends is not part of the plan (the previous stroke stays); should a partial stroke count? Also A4 (a quick tap in the body zone clears the move) and the 30° swipe tolerance are feel calls for PT3.
- Session 03: D6, D8, D9, D10, D11 are flagged seams with the recommended defaults (see *Decisions*); confirm or change them at PT3. Also confirm: idle counters reset when sudden death starts, and the both-forfeit pick (sudden death) once a real match shows how it feels.
- Wiring left for later sessions: **Session 09** must call `ITurnAuthority.NotifyPlaybackFinished(turnIndex)` from `ClientMatchFlow.OnPlaybackFinished` and choose the hold mode (`ClientReport` for a client, `None` for headless); **Session 06** owns status lifetime (the machine only calls `ApplyToNextTurn`); **Session 12** replaces the marked sudden-death seam in `EndConditionEvaluator` (both hit, nobody hits) with `ITiePolicy` / `ISuddenDeathNoHitPolicy`, adds `SuddenDeathSettings.BoardMode`, the electric-wall reaction at lock (`ElectricWallRules.OnPlansLocked`) and the sudden-death option flags (switching, signatures) as constraints; **Session 15** replaces the always-refused `UseSignature`; **Session 19** rebuilds the path from the raw stroke before validating and stamps the audit times from the server clock.
- **Wiring Session 04 left (2026-10-02):** **Session 07:** two kinematic blades report contacts (full kinematic contacts); repeated contacts between the same pair are D19. **Session 08:** touch input must map screen → `ArenaRoot` local → arena → the side's `TurnStartRoot` frame, as `SandboxController.ToTorsoFrame` does.
- **Wiring Session 08 leaves (2026-10-04):** **Session 09:** compose human play outside the sandbox: a `TouchInputReader` (with `MouseActsAsTouch` in the Editor), a `DualZoneInputScheme` per human (`CameraArenaView.Read` for the view, an `ITouchBlocker` that includes `HudTouchBlocker`), `TouchPlanSource` from `PlanSourceFactory`, `PlanningHud.Create` + `PlanningHudPresenter` ticked with `MatchBootstrap.AuthorityNowSeconds` and the human's `PathPreview` (`PathPreviewBuilder` + `PathPreviewView`, shown for the local human only), `FixedSideCameraDirector.SetArenaFlipped(layout.ArenaViewFlipped)`; `HotSeatCoordinator` can switch `TouchPlanSource.IsActive` and `PlanningHudPresenter.Source` behind its cover screen; the real HP UI must follow the view flip. **Session 10:** safe areas for the HUD; real touch on a phone. **Session 15:** signature buttons go in `PlanningHud.SignatureSlot`. **Session 18:** the first-launch handedness pick replaces `InputConfig.DevHandedness` (and sets `FighterSetup.Handedness`).
- **Wiring Session 07 leaves (2026-10-03):** **Session 08:** a touch tap is a one-point stroke (`SetPathCommand` with one point; `PathBuilder` adds the D3 lead-in), as the sandbox click does. **Session 11:** a severed-limb club is a held item of `WeaponKind.SeveredLimb` and clashes like a weapon (its mass and speed); a dropped weapon (`DropHeldItem`) takes part in no rule. **Session 13:** feel hooks on `WeaponClashEvent` (kind, angle, powers: sparks on a rebound, a heavy thud on a crush) and `ShieldBlockEvent` (full or partial); `SimulationSettings.ClashReaction` for the knock-off, rebound and push-back feel. **Session 14:** sword and spear clash through their mass and speed with no extra code; shield variants (design TBD) are `WeaponKind.Shield` items with their own stats and `HeldItemShape`.
- **Session 07 to confirm at PT2:** the clash speed rule (A1: a still weapon clashes with its mass only, so a held mace stops a rapier poke and a held rapier is crushed by a thrust), the shield held like a shield (A3) and taps (A4), the block numbers (partial ×0.5, square ≥ 30°, rim 20%), D19–D21 defaults, the reactions (knock-off, rebound, push-back), kinematic blades passing through each other, shield vs shield (A7).
- **Wiring Session 06 leaves (2026-10-03):** (Session 07 resolved its clash and block wiring.)
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

```
PT2 report, 2026-10-03 (written 3.10.2026)
Feel rating (1-10): 4? "There is a lot of space to develop and it's fine, we are not even through halfway."
What felt great: (not given)
What surprised me (good or bad): (not given)
Values I changed in the tuning panel: none.
Bugs: none.
Decisions I made: none.
```
Claude's reading: 4/10 is a combat sandbox with no juice yet (hitstop, camera, VFX, sound are Session 13), so nothing to fix. No value was changed and no decision was made, so every "to confirm at PT2" item (Sessions 05, 06, 07 in *Open questions*) stays a flagged default until the designer says otherwise.
