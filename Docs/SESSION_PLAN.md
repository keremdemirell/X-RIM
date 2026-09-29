# X-RIM Build Plan: Claude Code Sessions

Written 2026-09-29, after the architecture setup was verified clean in the Unity Editor.

This file is the ordered build plan. Each session is one coherent module or milestone that can be finished and verified on its own, in one Claude Code conversation of about 3 to 6 batches. The prompt that starts each session is in `Docs/sessions/session-XX-<name>.md`. Progress, decisions and playtest reports live in `MEMORY.md` at the project root, never in the GDD.

Section numbers such as §9 refer to the GDD (`Docs/X-RIM — Game Design Document.pdf`).

---

## 1. How to run a session

1. Check this plan for **decisions I must make before it starts** and record the answers in `MEMORY.md` → *Decisions made during development*. (Claude can also record them for you at the start of the session if you type them in.)
2. Open a fresh Claude Code chat with the recommended model and paste the whole content of the session's prompt file.
3. Claude reads the docs, checks dependencies in `MEMORY.md`, proposes a batch plan and waits for your approval.
4. Claude does one batch at a time. After each one it runs `python Tools/check.py` (from session 01 on), updates `MEMORY.md`, commits locally and stops. Check what it says to check in Unity, then type `continue`.
5. If the chat closes mid-session, paste the same prompt into a new chat. Claude resumes from `MEMORY.md` at the next unfinished batch.
6. At a **playtest checkpoint (PT)**, test in the Editor (or on a device) and give Claude the report described in §5 below. Do this before starting the next session. The report can go at the start of the next session's chat or into `MEMORY.md` directly.

---

## 2. Session overview

| # | Session | One-line goal | Model | Depends on | Checkpoint |
|---|---|---|---|---|---|
| 01 | Tooling and path rules | Out-of-Editor compile and test tool, then paths, ink, rigidity and path policies | Opus 5.5 | setup | – |
| 02 | Feel spike | One placeholder dummy swings one weapon along a drawn path in the hidden physics scene; choose the weapon driver | Opus 5.5 | 01 | **PT1** |
| 03 | Match loop and planning rules | Planning session, plan validation, match state machine, end conditions and local authority, all headless | Sonnet 5.5 | 01 | – |
| 04 | Turn simulation and playback | Full two-fighter turn simulation, recording, stance persistence, timeline playback and a debug sandbox for plans | Opus 5.5 | 02, 03, PT1 | – |
| 05 | Body moves | Crouch, lunge, step back and jump, with the path travelling with the torso | Opus 5.5 | 04 | – |
| 06 | Hits, priority and damage | Weapon-to-body hits, time-to-impact priority, interrupts, zone damage, limb damage cap, head stun | Opus 5.5 | 04 | – |
| 07 | Clashes and shield | The two-stage clash model, stagger, and the path-based shield with its block model | Opus 5.5 | 06 | **PT2** |
| 08 | Touch input and planning HUD | Dual-zone touch input, swipe classifier, path drawing with ink preview, weapon selector, Ready, timer | Sonnet 5.5 | 03, 05, PT2 | – |
| 09 | First playable | End-to-end offline match (hot-seat and vs bot), HP UI, basic hitstop, match over and rematch | Opus 5.5 | 05–08 | **PT3** |
| 10 | Device build | Android development build, real touch, safe areas, simulation time budget on a phone | Sonnet 5.5 | 09, PT3 | **PT4** |
| 11 | Dismemberment and desperation | Severing, weapon drop, off-hand fighting, severed limb as a club, torso split on death | Opus 5.5 | 09 | – |
| 12 | Electric wall, arena edge and sudden death | Per-player wall sequence, arena edge, double KO and turn cap into sudden death | Sonnet 5.5 | 09 | – |
| 13 | Feel and juice pass | Hitstop, slow motion, camera punch, mechanical VFX, audio hooks, legendary-moment replay, art spec | Opus 5.5 | 11, 12 | **PT5** |
| 14 | Weapon roster and loadout | Sword and spear (rigidity), loadout pick and reveal, trait seams | Sonnet 5.5 | 09, 13 | – |
| 15 | Signature moves | Preset self-drawing patterns, charge model, HUD buttons, first placeholder moves | Sonnet 5.5 | 14 | – |
| 16 | Bots, scenarios and balance | Scenario save and load, regression tests, heuristic bot, headless bot-vs-bot balance batch | Opus 5.5 | 14, 15 | **PT6** |
| 17 | Art integration and skins | Swap placeholders for your art, skin system that cannot touch hitboxes, arena background | Sonnet 5.5 | 13, your art | – |
| 18 | Meta shell, profile and economy | Main menu, first-launch handedness, local profile save, two wallets, earned-only unlock shop | Sonnet 5.5 | 14, 15 | – |
| 19 | Online authority | Server-authoritative turn resolution: protocol, headless host, server-side validation, remote authority | Opus 5.5 | 16, decisions | – |
| 20 | Matchmaking, friends and contacts | Match lobby, friend challenge, contacts permission flow, disconnect handling | Opus 5.5 | 19, decisions | **PT7** |
| 21 | Tutorial and replays | First-match tutorial and local replay save/share seam | Sonnet 5.5 | 13, 18 | – |
| 22 | Release readiness | Cosmetics store with real-money purchases, telemetry for balance targets, privacy, store build settings | Sonnet 5.5 | 18, 20, 21 | – |

**Milestones**
- **Physics de-risked:** after 02 and PT1.
- **Combat sandbox:** after 07 and PT2. Every combat rule plays out with debug-drawn plans.
- **First playable offline prototype:** after 09 and PT3. This is the pillar-1 feel target: turn loop, drawing, body moves, execution, clashes and damage.
- **Complete offline game:** after 16 and PT6. Every GDD combat system, with flagged defaults where the design is still TBD.
- **Shippable online game:** after 22.

**Models.** Opus 5.5 is recommended for physics, simulation, integration and networking sessions, where subtle bugs are expensive. Sonnet 5.5 suits well-specified rules, UI and content sessions. If a newer or stronger model is available when you start a session, prefer it for the Opus rows.

---

## 3. Placeholder art, and when real art is needed

Until session 17 everything is **programmatic**: Editor scripts under `XRim/…` build the ragdoll prefab from primitive sprites (capsules and boxes with yellow-and-black marker colours), the weapons from simple shapes, and the VFX from built-in particle shapes. You never need to author a prefab by hand.

| When | What I need from you | Format |
|---|---|---|
| Before 13 (optional) | A rough SFX set: metal clash, body impact, joint snap, bolt scatter, wall zap, 3–4 effort grunts, one tense music loop. Free or temporary sounds are fine; session 13 builds the audio hooks either way. | `.wav` or `.ogg` in `Assets/XRim/Audio/Incoming/` |
| After 13 | Session 13 writes `Docs/ART_SPEC.md`: the exact size, pivot and joint anchor of every ragdoll part and weapon, taken from the placeholder builder. Draw to that spec. | – |
| Before 17 | Dummy body parts to the spec: head, torso (top and bottom halves for the death split), each arm and leg, with the yellow-and-black calibration markers matching the hit zones. Weapons: rapier, sword, spear, mace, shield, and a severed-limb club look. Debris sprites: bolt, nut, spring, gear, fluid droplet. Electric wall. One arena background (crash-test facility, which is TBD §2). | PNG, transparent, one file per part, or a sprite sheet with named slices |
| Before 18 | UI art: buttons, weapon icons, Ready, HP bar, currency icons. Placeholder UI Toolkit styles are used until then. | PNG or SVG |
| Before 22 | App icon, store screenshots, a final music track and the full grunt packs. | Store sizes |

---

## 4. Decisions to make up front (prototype blockers)

These TBDs, and two gaps the GDD does not cover (D26, D27), block the playable prototype (sessions 02–09). Decide them in one sitting, then paste or write the answers into `MEMORY.md` → *Decisions made during development*. Anything left undecided is built with the **recommended default as a flagged seam** (a `[GddTbd]` switch you can flip in the tuning panel), never as a final answer.

| # | § | Decision | Recommended default | Why | Needed by |
|---|---|---|---|---|---|
| D1 | – | Weapon driver: kinematic path following, or a physics motor chasing the path | Decided by you at PT1 from the spike | Pure feel question; the spike builds both | 04 |
| D2 | – | Ragdoll segmentation: 6 bodies (one per hit zone), or 10 (upper and lower limbs, both mapped to the same zone) | Decided by you at PT1 from the spike | More segments look better when flailing but cost stability | 04 |
| D3 | 6 | Path start: at the weapon tip, or anywhere | Anywhere; an automatic straight lead-in from the current tip to the first drawn point is added and **costs ink and time** | The finger is free, but reach cannot be gained for free | 01 (seam), 04 |
| D4 | 6 | Path beyond reach (arm + weapon + lunge) | **Clip** the path at the reach limit, shown in the preview | Pulling the body would fight body moves and the wall trigger | 01 (seam), 08 |
| D5 | 6 | Strokes per turn | One continuous stroke; redrawing replaces it (the GDD proposal) | Simple and readable | 01 |
| D6 | 6 | Drawing during the switch lock-out | Allowed (the GDD proposal) | The lock-out targets switching, not drawing | 03 |
| D7 | 6 | Remaining-ink UI | Shown while drawing (the GDD proposal) | Players cannot budget blind | 08 |
| D8 | 6 | Prototype loadout; is a shield slot required | Rapier, mace, shield for both players; no required shield slot | Covers fast, heavy and defensive play | 03 |
| D9 | 3 | Can a player cancel Ready | Allowed until the lock-out starts | The opponent sees Ready, so fake-Ready becomes a bluff (pillar 2) | 03 |
| D10 | 3 | Which inputs reset the forfeit counter | Any body move, drawn path or signature move (the GDD proposal) | – | 03 |
| D11 | 3 | Order of end conditions in one turn | Double KO → sudden death; single KO → match over; then forfeit; then turn cap → sudden death | A KO always beats the clock | 03 |
| D12 | 5 | Backward swipe: lean in place, or a real step | A real short step back | The wall "behind the dummy" implies actually retreating | 05 |
| D13 | 5 | Does lunge add speed or damage | Reach only | Weapon speed is a Decided weapon stat | 05 |
| D14 | 5 | Diagonal or combined swipes | Not allowed (the GDD proposal) | – | 08 |
| D15 | 9 | Which hits interrupt an attack | Any clean body hit interrupts | Simplest to read; PT2 checks whether rapier pokes dominate | 06 |
| D16 | 9 | Heavy weapon swing armour | Off, but the toggle exists (`WeaponStats.HasSwingArmour`) to test in PT2 | – | 06 |
| D17 | 10, 11 | What a stun or stagger does | No body move next turn | Visible, easy to read, and needs no timer or ink changes | 06 |
| D18 | 11 | HP scale, base damage, limb durability, stun threshold | Keep the setup placeholders (HP 100, arm/leg 40/50, stun 30, damage in ARCHITECTURE §4) | Tuned at PT2 and PT3 | 06 |
| D19 | 10 | Several contacts between the same two weapons in one turn | Only the first contact resolves; later ones pass through | Avoids jitter loops | 07 |
| D20 | 7 | Shield block model | Full block for square hits on the face, partial reduction (placeholder 50%) for edge or glancing hits (the GDD proposal) | – | 07 |
| D21 | 7 | Can a mace break through or stagger a shield | No special rule: the block holds, and physics knockback does the rest | Keeps one rule; PT2 decides if the mace needs more | 07 |
| D22 | 13 | Arena width, and the edge when no wall is active | Width 2000 (placeholder); the edge is a solid invisible stop | Lunges can reach it before session 12 | 04 |
| D23 | 4 | HUD placement | Weapon selector along the top of the weapon zone; Ready in the far bottom corner; timer top centre | Keeps the drawing area clear | 08 |
| D24 | 4 | Change handedness later; separate mirror toggle | Changeable in settings; no separate toggle | – | 08 (dev setting), 18 |
| D25 | 17 | Prototype opponent | Hot-seat (cover screen between players) and a random debug bot | Both already have seams | 09 |
| D26 | – (not in the GDD) | How many hits a weapon can land per turn, and whether it continues its path after a hit | One damaging hit per weapon per turn; the weapon stops at the hit point with an impact recoil | §9 speaks of landing "its hit"; this keeps damage readable and the limb cap meaningful | 06 |
| D27 | 11 | How "a single head hit can never cause an instant KO" is enforced | A single hit cannot take a dummy from full HP to 0: its damage is clamped to leave 1 HP. Later hits can finish a damaged dummy. | The Decided rule does not say how | 06 |

### Later decisions, by session

| Session | Decisions needed before it starts (defaults Claude will use if you do not decide) |
|---|---|
| 10 | Target minimum Android device (default: whatever phone you own). Install Android Build Support. iOS needs a Mac and is not planned from this Windows machine. |
| 11 | §12 leg-loss penalty (default: no jump or lunge with one leg; no body moves with none). Body blows without arms (default: the path steers the head as a headbutt). Severed-limb stats and slot (default: the placeholder stats, as a temporary extra option). What if the limb flew out of reach (default: auto-retrieve anyway). |
| 12 | §13 wall damage growth (default: flat). Wall damage when knocked into it (default: no). §14 sudden-death board (default: the current board carries over). No hit (default: repeat, with a cap of 3 repeats, then the player with more HP before sudden death wins, flagged). Same-step tie (default: replay). Wall in sudden death (default: off). Options (default: switching and signatures stay). |
| 14 | §6 spear rigidity: keep or cut (default: keep, flagged, off-switch in tuning). Sword and spear stats (placeholders). Starting weapons for new players. §15: do traits appear in this build at all (default: seams only, no weapon carries a trait). |
| 15 | §8 charge model (default: once per match). Moves per match (default: 1). Weapon ties (default: any weapon). Ink use (default: ignores ink). Opponent sees charged (default: no). Catalogue: give 2–4 absurd names and what each does, or Claude makes clearly-flagged placeholders. |
| 16 | §17 is a bot opponent a real game mode (default: practice only). Target match length (§1). |
| 17 | Your art (see §3). §2 default arena setting. |
| 18 | §16 currency names, sources and amounts, farming guard, progression structure, prices. Defaults: placeholder names, match-win reward only, no levels. |
| 19 | §18 networking model (default: server-authoritative, the GDD recommendation). Hosting and backend stack: Claude presents 2–3 options with costs in batch 1 and waits. Reconnect window and missed turns (§3). |
| 20 | §17 random matchmaking or friends only; ranked or not; challenge contacts directly; friends list. |
| 21 | §17 tutorial approach; replay saving and sharing. |
| 22 | §1 free-to-play or paid. Store names and trademark (§0). Age rating (§2). Premium currency earnable in play (§16). |

---

## 5. Playtest checkpoints

At each checkpoint, play in the Unity Editor (or on the phone for PT4 and PT7) and give Claude this report. Claude writes it into `MEMORY.md` → *Playtest reports*, and any value you changed into *Tunable values changed from the GDD*.

```
PT<n> report, <date>
Feel rating (1-10):
What felt great:
What surprised me (good or bad):
Values I changed in the tuning panel, from -> to, and why:
Bugs (steps to reproduce):
Decisions I made (for example D1 driver = motor):
```

| PT | After | What to test |
|---|---|---|
| PT1 | 02 | The spike scene: draw paths with the mouse, swing the rapier and the mace with both drivers, hit the target dummy. Decide D1 (driver) and D2 (segmentation). Is the ragdoll stable standing still? Does the contact log show sensible normals and angles? |
| PT2 | 07 | The combat sandbox: debug-draw both plans, play out hits, interrupts, clashes (crush, rebound, deflect, slide) and shield blocks. Does the rapier dominate? Try `HasSwingArmour`. Are the damage placeholders in the right range? |
| PT3 | 09 | Full hot-seat matches and matches against the random bot. Is 12 s of planning right? Does drawing feel good with a mouse? How long is a match? Are there legendary moments yet? |
| PT4 | 10 | The same on an Android phone: touch drawing, swipes in the body zone, handedness mirroring, frame rate, time between Ready and playback. |
| PT5 | 13 | The feel pass: hitstop, slow motion, camera, VFX, sound. Try to create the reference moment from §1 (duck under a mace, rapier to the pelvis). |
| PT6 | 16 | Bot-vs-bot batch report: dismemberment rate against 10–20%, match length, weapon win rates. Tune durability. |
| PT7 | 20 | Two phones in an online match: latency feel, disconnect and reconnect. |

Between checkpoints you still check each batch briefly in Unity (0 red errors, tests green, whatever that batch summary names).

---

## 6. Rules every session follows

`Tools/check` below is shorthand for `python Tools/check.py`, run from the project root.

- **Read first:** `CLAUDE.md`, `MEMORY.md`, `Docs/ARCHITECTURE.md`, this file, and the GDD sections the session lists.
- **Git:** commit locally after each finished batch. Never push, amend or force anything, and never touch other branches. Stage only the files the batch changed, by name. Commit messages contain **no attribution**: no `Co-Authored-By`, no "Generated with Claude Code", no mention of Claude or AI.
- **GDD tags:** Decided → build as written. Tunable → the value lives in config. TBD → never invent a final answer; build a flagged seam with a default, or ask. Parked or Rejected → do not implement.
- **The GDD file is never edited.** Decisions and deviations go into `MEMORY.md`.
- **No hand-written `.unity`, `.prefab` or asset YAML.** Use Editor scripts (menu items under `XRim/…`) or give short numbered manual steps.
- **After every batch:** run `python Tools/check.py` (from session 01 on) and fix everything it reports, update `MEMORY.md`, commit, summarize, then stop and wait for `continue`.
- **New placeholders** are tagged `[Placeholder("reason")]` and reported to the user in the batch summary.

---

## 7. Sessions in detail

### Session 01: Tooling and path rules
- **Prompt:** `Docs/sessions/session-01-tooling-and-paths.md`
- **Model:** Opus 5.5 (the tool gates every later session, so it must be right).
- **Module and goal:**
  - `Tools/` (new): a compile-check and test tool that runs outside the Unity Editor.
  - `XRim.Rules.Paths`: drawn paths, ink and rigidity, and the path TBD seams, built on top of that tool.
- **GDD:** §4 (input fairness, sampling every N units), §6 (ink budget, rigidity, path rules), §18 (path capture).
- **Depends on:** architecture setup (verified 2026-09-29).
- **Definition of done:**
  - `python Tools/check.py` (one command from Git Bash or PowerShell) does all of the following:
    - Compiles every `XRim.*` asmdef with Unity's bundled Roslyn in three passes: editor, player and development player.
    - Rebuilds each source list from the asmdef folders, so new files are included without opening Unity.
    - Verifies that every `noEngineReferences` assembly compiles without UnityEngine and that a probe file using `UnityEngine` is rejected.
    - Runs the EditMode tests that do not need the Unity runtime.
    - Prints a pass/fail summary and exits non-zero on any failure.
  - `Tools/README.md` explains usage and limits.
  - `PathResampler`, `InkMeasurement`, `WeaponPath` and `IInkCostModel` (plain and rigidity cost, §6 formula) are implemented. Seam defaults exist for `IPathStartPolicy` (D3), `IReachPolicy` (D4) and `IStrokePolicy` (D5).
  - EditMode tests cover:
    - Resampling is independent of input sample rate and resolution (60 Hz vs 120 Hz input gives the same ink).
    - Ink cut-off.
    - Rigidity cost and invalid sharp turns.
    - Each policy default.
  - All tests pass under `python Tools/check.py`, and in Unity.
- **Unity Editor steps:** open Unity once at the end; confirm 0 errors and that the EditMode test count went up.
- **TBDs:** §6 path start, reach limit, strokes per turn and spear rigidity. Each gets a seam plus the recommended default, marked `[GddTbd]`.
- **Decide before:** D3, D4, D5 (defaults are used if you have not).

### Session 02: Feel spike (one dummy, one weapon)
- **Prompt:** `Docs/sessions/session-02-feel-spike.md`
- **Model:** Opus 5.5.
- **Module and goal:** `XRim.Simulation.Unity2D`, `XRim.Editor`, `XRim.DebugTools`. Deliberately small. The goal is to de-risk the physics before the full rules exist:
  - An Editor script builds a placeholder ragdoll prefab from primitive sprites (HingeJoint2D limbs with angle limits, a hand anchor and a held weapon).
  - `Unity2DPhysicsWorld` implements `Load`, `SetHeldItemTarget`, `SetRootTarget`, `Step`, `DrainContacts`, `GetPose`, `CapturePose` and `IsSettled` for this case.
  - Two `IWeaponDriver`s exist: kinematic path following, and a physics motor chasing the path target.
  - A spike scene, created by a menu item, holds one dummy with a rapier or mace and a static target dummy:
    - Draw a path with the mouse (torso-relative), press a key, and the hidden scene simulates the swing.
    - The swing plays back in the visual scene at adjustable speed.
    - Contacts are logged with part, normal, relative velocity, clash angle and refined time-to-impact.
- **GDD:** §6 (torso-relative path), §9 (t = d / v), §10 (contact angle from 2D contact data), §18 (ragdoll with HingeJoint2D, logic-driven joint breaks).
- **Depends on:** 01.
- **Definition of done:**
  - Both drivers work behind `IWeaponDriver`, and a debug toggle switches between them.
  - Time-to-impact from path distance matches the contact step within one step.
  - A PlayMode test covers "the weapon reaches the end of a straight path at t = length / speed ± one step".
  - A PlayMode test covers "the hidden scene still never moves the visual scene".
  - `Tools/check` is green.
  - The spike findings (stability, tunnelling at 240 Hz, driver pros and cons) are written into `MEMORY.md`.
- **Unity Editor steps:** run `XRim > Spike > Create Spike Scene` (or similar), enter Play mode, then test as PT1 describes.
- **TBDs:** none from the GDD. D1 and D2 are the checkpoint's output.
- **Decide before:** nothing. **PT1 follows this session.**

### Session 03: Match loop and planning rules
- **Prompt:** `Docs/sessions/session-03-match-loop-and-planning.md`
- **Model:** Sonnet 5.5.
- **Module and goal:** `XRim.Rules` (Match, Planning), `XRim.Networking`, `XRim.Bots`. Everything about a turn except physics, fully headless:
  - `PlanningSession`:
    - Weapon switch erases the path.
    - Switching is refused in the last 1.5 s lock-out.
    - Ink is enforced through the session 01 models.
    - Ready, with the cancel flag (D9).
    - On timeout, whatever is set executes.
    - Public state shows the weapon and Ready.
  - `PlanValidator` checks each plan on the authority clock.
  - `MatchStateMachine` implements every transition.
  - `EndConditionEvaluator` implements KO, double KO, forfeit after 3 idle turns and turn cap 30, in the D11 order.
  - `LocalTurnAuthority` runs the loop with `StubTurnSimulator`.
  - `RandomBotBrain` emits only valid plans.
- **GDD:** §3 (whole), §6 (loadout, weapon switching, lock-out), §13 (turn cap), §14 (triggers only), §18 (validation).
- **Depends on:** 01. It does not depend on PT1's result, but PT1 is reported before it starts.
- **Definition of done:**
  - EditMode tests cover every command rule, the lock-out edge at exactly 1.5 s, the idle-turn forfeit, a 30-turn match that enters sudden-death setup, and a double KO.
  - A stub-simulated bot-vs-bot match runs headless to its end.
  - `Tools/check` is green.
- **Unity Editor steps:** run the tests only.
- **TBDs:**
  - §3 cancel Ready (D9), forfeit inputs (D10), end-condition order (D11).
  - §6 drawing during lock-out (D6) and required shield slot (D8).
  - Each is a flag or policy with the recommended default.
- **Decide before:** D6, D8, D9, D10, D11.

### Session 04: Turn simulation and playback
- **Prompt:** `Docs/sessions/session-04-turn-simulation-and-playback.md`
- **Model:** Opus 5.5.
- **Module and goal:** `XRim.Simulation`, `XRim.Simulation.Unity2D`, `XRim.Presentation` (Playback, Dummy), `XRim.DebugTools`.
  - The real `TurnSimulator` runs two fighters with the D1 driver and D2 ragdoll:
    - Fixed 240 Hz step.
    - Contacts are refined to path-progress time and processed in time order, then by stable id.
    - The turn ends when both paths are done and physics has settled, or at the 1.5 s hard cap.
    - `TimelineRecorder` produces `TurnResult`.
    - `BoardSnapshot` and `Load` give stance persistence: the next turn starts from the frozen pose at zero velocity.
  - Arena edges are solid stops (D22).
  - `DummyView` and `TimelinePlayer` play it back in the visual scene.
  - Debug **Playback tab**: speed 0.05×–2×, pause, frame step, scrub, loop, and re-simulate with current tuning.
  - Debug **Sandbox plan input**: draw the left and right paths with the mouse, pick weapons, press Execute. This is how sessions 05–07 and PT2 are tested before touch input exists.
- **GDD:** §3 (execution, stance persistence), §6 (path moves with the body), §9, §18.
- **Depends on:** 02, 03, PT1 (D1, D2 recorded).
- **Definition of done:**
  - A PlayMode test shows the same (board, plans) simulated twice gives the same event order.
  - A PlayMode test covers the hard cap.
  - A PlayMode test shows that loading a captured snapshot reproduces the pose within tolerance.
  - EditMode tests cover the step loop with `FakePhysicsWorld`.
  - `Tools/check` is green.
- **Unity Editor steps:** open the Sandbox scene; draw two paths; execute several turns in a row and confirm the poses carry over.
- **TBDs:** none new (the arena edge uses D22 through `IArenaEdgePolicy`).
- **Decide before:** D22. D1 and D2 must be in `MEMORY.md`.

### Session 05: Body moves
- **Prompt:** `Docs/sessions/session-05-body-moves.md`
- **Model:** Opus 5.5.
- **Module and goal:**
  - `IBodyMoveDriver` implementations for crouch, lunge, step back and jump, driving the root target. Each keeps the ragdoll balanced and makes the weapon path travel with the torso (§6 Decided: duck plus a straight line becomes a low thrust).
  - The move set comes from the `BodyMoveDefinition` assets.
  - The backward move emits the domain fact that session 12's wall rules will read.
  - The sandbox gets body-move pickers.
- **GDD:** §5, §6 (path anchoring), §9 (body-move timing), §12 (mobility seam only).
- **Depends on:** 04.
- **Definition of done:**
  - Each move reaches its configured displacement within its duration and settles.
  - Crouch lowers the head below a configured high-strike line.
  - Jump lifts the feet above a low-sweep line.
  - The path stays torso-relative during every move (PlayMode tests).
  - The D12 and D13 flags are wired.
  - `Tools/check` is green.
- **Unity Editor steps:** in the sandbox, try each move alone and with a straight thrust.
- **TBDs:** §5 lean vs step (D12), lunge bonus (D13) and the mobility penalty after leg loss (seam only; its default "no penalty" is filled in by session 11).
- **Decide before:** D12, D13.

### Session 06: Hits, priority and damage
- **Prompt:** `Docs/sessions/session-06-hits-and-damage.md`
- **Model:** Opus 5.5.
- **Module and goal:** `XRim.Rules` (Combat, Damage, Status, Events), `XRim.Simulation`.
  - Weapon-to-body contacts become `HitFacts`, with the zone taken from the part tag.
  - The earliest time-to-impact wins priority.
  - `IInterruptPolicy` (D15) and swing armour (D16) are applied.
  - `DamageCalculator` applies base damage × zone multiplier × modifiers.
  - Limb damage with the 35% per-hit cap.
  - Head hits: no instant KO, and a stun above the threshold. `IStatusEffect` (D17) edits the next turn's `PlanningConstraints`.
  - HP reaching 0 emits `FighterDiedEvent`.
  - The simulation applies interrupts (cancels the driver) and hit impulses.
  - Placeholder HP bars appear in the sandbox.
- **GDD:** §9 (priority and counter-hits), §11 (whole), §12 (limb damage feeds severing, but no sever yet).
- **Depends on:** 04 (05 is not required, but is usually done first).
- **Definition of done:**
  - EditMode tests cover the damage formula for every zone, the limb cap (severing needs at least 3 hits), no one-hit head KO, stun threshold, the interrupt policy options and priority tie ordering.
  - A PlayMode scenario shows that a short rapier thrust interrupts a slow mace swing.
  - `GddAppendixATests` is still green.
  - `Tools/check` is green.
- **Unity Editor steps:** in the sandbox, hit each zone and watch the HP and limb-damage readout.
- **TBDs:** §9 which hits interrupt (D15) and heavy armour (D16); §10/§11 stun effect (D17); §11 HP scale (D18). Not in the GDD: hits per weapon per turn (D26) and how the no-instant-KO head rule is enforced (D27).
- **Decide before:** D15, D16, D17, D18, D26, D27.

### Session 07: Clashes and shield
- **Prompt:** `Docs/sessions/session-07-clashes-and-shield.md`
- **Model:** Opus 5.5.
- **Module and goal:**
  - `ClashResolver`, the two-stage model:
    - Stage 1: the contact angle against 30° picks hard or glancing.
    - Stage 2: power P = W_m·m + W_v·v. A crush happens at ratio ≥ 1.5 (the weaker weapon is knocked off its path, its dummy is staggered, and the winner continues at −30%); below that, both rebound. On a glancing contact the lighter weapon deflects the heavier; within the mass band, both slide.
  - The simulation applies each outcome and continues surviving weapons from the contact point.
  - `IRepeatContactPolicy` (D19).
  - The shield is a held item that follows its path and then holds its end point; a tap raises it in place.
  - `IShieldBlockModel` (D20, D21): shield contacts use the block rule, never the clash model. A shield bash does low damage.
  - Using a shield never counts as retreating.
- **GDD:** §7, §10.
- **Depends on:** 06.
- **Definition of done:**
  - EditMode tests cover every branch of the flowchart in §10, W_m ≠ W_v validation, the block model options and the repeat-contact policy.
  - PlayMode scenarios cover a mace crushing a rapier, two rapiers sliding past each other, and a shield arc blocking a thrust.
  - Clash events carry the angle and powers for the gizmos.
  - `Tools/check` is green.
- **Unity Editor steps:** the whole of PT2.
- **TBDs:** §7 block model and mace vs shield (D20, D21); §10 stagger (shares D17) and repeat contacts (D19).
- **Decide before:** D19, D20, D21. **PT2 follows this session.**

### Session 08: Touch input and planning HUD
- **Prompt:** `Docs/sessions/session-08-touch-input-and-hud.md`
- **Model:** Sonnet 5.5.
- **Module and goal:** `XRim.Input`, `XRim.Presentation.UI`.
  - `TouchInputReader` (EnhancedTouch, and mouse in the Editor).
  - `ScreenLayout` zones with handedness mirroring (a dev setting for now, D24).
  - `FourWaySwipeClassifier` (D14: no diagonals).
  - `ScreenToArenaMapper` (torso frame, flip-aware, resolution-independent).
  - `DualZoneInputScheme` and `TouchPlanSource` emit `PlanningCommand`s only.
  - Live path preview:
    - Remaining ink shown (D7).
    - Clipped past reach (D4).
    - Rigidity-invalid segments drawn in red.
  - `PlanningHud` (UI Toolkit), laid out per D23: weapon selector, Ready, planning timer, and the opponent's weapon and Ready state.
- **GDD:** §4, §5 (swipe input), §6 (drawing, switching UI, ink UI).
- **Depends on:** 03, 05, PT2.
- **Definition of done:**
  - EditMode tests cover the swipe classification table, mirroring, and the pixel-to-arena mapping at three resolutions (the same drawn shape costs the same ink).
  - The HUD works with mouse input in the Editor.
  - `Tools/check` is green.
- **Unity Editor steps:** the Game view at two resolutions (for example 1920×1080 and 2400×1080); draw, swipe and switch weapons.
- **TBDs:**
  - §4 HUD placement (D23), handedness change and mirror toggle (D24) and scheme feel (`IInputScheme`).
  - §5 diagonals (D14).
  - §6 ink UI (D7).
- **Decide before:** D7, D14, D23, D24.

### Session 09: First playable
- **Prompt:** `Docs/sessions/session-09-first-playable.md`
- **Model:** Opus 5.5.
- **Module and goal:** `XRim.App`, `XRim.Presentation`, `XRim.Bots`. Wires everything into a match:
  - `MatchBootstrap` composition and `ClientMatchFlow` loop: plan → lock → simulate → playback → next turn.
  - The next planning timer starts after playback.
  - `HotSeatCoordinator`, with a cover screen.
  - A random bot opponent (D25).
  - HP bars and a basic `FeelDirector` hitstop.
  - Match-over screen with rematch.
  - A mode picker in the Sandbox scene (or a `Match` scene built by a menu item).
- **GDD:** §1 core loop, §3, §17 (bot as a dev opponent only).
- **Depends on:** 05, 06, 07, 08.
- **Definition of done:**
  - A full hot-seat match and a full match against the bot run to a KO or forfeit with no errors.
  - An EditMode test covers the flow ordering with the real authority and a stub simulator.
  - A PlayMode smoke test runs three turns with scripted plan sources.
  - `Tools/check` is green.
- **Unity Editor steps:** build the match scene through the menu, then play PT3.
- **TBDs:** §17 bot and practice (D25, dev-only), §1 match length (measured, not decided).
- **Decide before:** D25. **PT3 follows this session.**

### Session 10: Device build
- **Prompt:** `Docs/sessions/session-10-device-build.md`
- **Model:** Sonnet 5.5.
- **Module and goal:**
  - An Editor build menu for Android development builds.
  - Landscape lock (`Apply Project Settings`).
  - Safe-area handling in the HUD.
  - A frame-rate target.
  - Measuring and logging the hidden simulation time per turn on the device (the §18 performance budget).
  - An on-device debug overlay.
  - Fixes for touch issues found on hardware.
- **GDD:** §4 (orientation, touch), §18 (target devices, performance).
- **Depends on:** 09, PT3.
- **Definition of done:**
  - An APK installs and runs a full hot-seat match on your phone.
  - Simulation time per turn is logged and recorded in `MEMORY.md`.
  - `Tools/check` is green.
- **Unity Editor steps:**
  1. Install Android Build Support (with SDK, NDK and OpenJDK) through Unity Hub → Installs → Add modules.
  2. Enable developer mode and USB debugging on the phone.
  3. Run the build menu item.
- **TBDs:** §4 orientation (landscape assumed) and §18 target devices; both are recorded as measured facts.
- **Decide before:** the target device. **PT4 follows this session.**

### Session 11: Dismemberment and desperation
- **Prompt:** `Docs/sessions/session-11-dismemberment.md`
- **Model:** Opus 5.5.
- **Module and goal:** `XRim.Rules.Limbs`, `XRim.Simulation`, `XRim.Presentation`.
  - A limb is severed when its durability reaches 0, through logic-driven `BreakJoint` (never `breakForce`).
  - Severed limbs fall and stay on the floor, and persist in `BoardSnapshot`.
  - Losing the dominant arm drops the weapon; the dummy fights with the off hand at 80%. Losing both arms uses `IArmlessAttackMode`.
  - Leg loss uses `IMobilityPenaltyPolicy`.
  - The severed limb becomes a selectable club (`LoadoutSettings.SeveredLimbIsExtraOption` and `ILimbRetrievalPolicy`).
  - On death, the torso splits in two.
  - Debris spray is placeholder here; session 13 polishes it.
- **GDD:** §11 (limb rules), §12, §18 (joint breaks).
- **Depends on:** 09.
- **Definition of done:**
  - EditMode tests cover the off-hand multiplier, the dominant-arm weapon drop, the policy defaults and that a limb weapon is never thrown.
  - A PlayMode scenario covers three arm hits → sever → weapon drops → next turn uses the off hand.
  - `Tools/check` is green.
- **Unity Editor steps:** the sandbox's force-sever cheat, then a real match.
- **TBDs:** §12 leg-loss options, armless body blows, limb stats and slot, and retrieval. Seams use the defaults in §4 → *Later decisions*.
- **Decide before:** the session 11 row in *Later decisions*.

### Session 12: Electric wall, arena edge and sudden death
- **Prompt:** `Docs/sessions/session-12-wall-and-sudden-death.md`
- **Model:** Sonnet 5.5.
- **Module and goal:**
  - `ElectricWallRules`, one per player:
    - Turn N: a backward swipe raises the warning.
    - Turn N+1: any other move removes the wall; another backward swipe gives damage, a bounce and an advance.
    - Only the input counts: knockback, the shield and standing still do not trigger it.
  - A physics wall body in the simulation.
  - The arena edge policy.
  - Sudden-death setup: HP 1; the first valid hit wins, and the earlier `SimTime` wins if both hit.
  - Policies for the board state, no hit, same-step tie and the wall in sudden death.
  - HUD warnings.
- **GDD:** §13, §14.
- **Depends on:** 09.
- **Definition of done:**
  - EditMode tests cover the full wall sequence table and each sudden-death policy.
  - A PlayMode scenario covers two retreats → zap and bounce.
  - A headless 30-turn stall enters sudden death.
  - `Tools/check` is green.
- **Unity Editor steps:** play a match retreating twice; play to the turn cap with the cheat that sets the turn counter.
- **TBDs:** §13 wall growth, knocked-into-wall damage and arena width; §14 every open point. Each is a flag with the default in *Later decisions*.
- **Decide before:** the session 12 row in *Later decisions*.

### Session 13: Feel and juice pass
- **Prompt:** `Docs/sessions/session-13-feel-pass.md`
- **Model:** Opus 5.5.
- **Module and goal:** `XRim.Presentation` (Feel, Cameras, Vfx, Audio, Moments).
  - `FeelDirector`: hitstop scaled by damage, slow motion on sever and death, and camera punch or shake. All values live in `FeelConfig`, and none of them affects outcomes.
  - `MatchVfx`: bolts, nuts, springs, sparks and hydraulic fluid (no blood), with pooling.
  - `MatchAudio`: clash, impact, joint snap, bolt scatter, wall zap and effort grunts, with silent fallbacks when a clip is missing.
  - `IMusicIntensityDriver` stays a seam.
  - `IMomentDetector` default heuristics: sever, interrupt counter-hit, crush, sudden-death finish. An instant cinematic replay of flagged turns.
  - Debug **Cheats tab**: freeze timer, infinite ink, force stun or sever.
  - `GameplayGizmos`.
  - Writes `Docs/ART_SPEC.md`.
- **GDD:** §1 (pillars 1 and 4), §2 (art and audio), §17 (replays: local instant replay only; sharing stays TBD).
- **Depends on:** 11, 12.
- **Definition of done:**
  - EditMode tests show that feel effects never change a `TurnResult` (the same inputs produce the same outcome regardless of `FeelConfig`), and cover the moment detector rules.
  - `ART_SPEC.md` exists.
  - `Tools/check` is green.
- **Unity Editor steps:** drop any SFX into `Assets/XRim/Audio/Incoming/` and run the audio-bank menu item; then play PT5.
- **TBDs:** §2 camera behaviour (`ICameraDirector`; the fixed default plus optional punch), adaptive music (a seam only), and §17 replay sharing (not built).
- **Decide before:** nothing required. Supply the optional SFX. **PT5 follows this session.**

### Session 14: Weapon roster and loadout
- **Prompt:** `Docs/sessions/session-14-roster-and-loadout.md`
- **Model:** Sonnet 5.5.
- **Module and goal:**
  - Sword and spear become playable with placeholder stats (`DesignIsTbd`).
  - Spear rigidity is live behind `RigiditySettings.Enabled`, with invalid-path feedback.
  - A pre-match loadout screen: pick 3 of the owned weapons, then both loadouts are revealed.
  - `IWeaponTrait` seams for lifesteal and build-up (§15), not assigned to any weapon by default.
  - The `Validate` orderings are extended to the new weapons.
- **GDD:** §6 (roster, loadout), §15.
- **Depends on:** 09, 13.
- **Definition of done:**
  - EditMode tests cover the loadout rules (3 slots, reveal, optional shield requirement), the rigidity cost for a spear S-curve and trait math behind flags.
  - A match can be played with any of the 5 weapons.
  - `Tools/check` is green.
- **Unity Editor steps:** rerun `Create Default Tuning Assets` (it never overwrites tuned assets), then play with each weapon.
- **TBDs:** §6 stats, spear rigidity, starting weapons and the shield slot; §15 every trait detail.
- **Decide before:** the session 14 row.

### Session 15: Signature moves
- **Prompt:** `Docs/sessions/session-15-signature-moves.md`
- **Model:** Sonnet 5.5.
- **Module and goal:**
  - `SignatureMoveDefinition` assets hold preset torso-relative patterns.
  - `UseSignatureCommand` draws the pattern.
  - `ISignatureChargeModel`, both options: once per match, and a Kinetic Energy Bar fed by damage dealt and taken.
  - `IPublicStatePolicy` for charged visibility.
  - HUD buttons.
  - 2–4 placeholder moves with absurd names, each with one special property built as a modifier.
- **GDD:** §8.
- **Depends on:** 14.
- **Definition of done:**
  - EditMode tests cover both charge models, hidden until execution, the body move still being allowed, ink handling per flag, and the forfeit counter counting a signature.
  - A PlayMode scenario covers one move.
  - `Tools/check` is green.
- **Unity Editor steps:** play a match using each move.
- **TBDs:** all of §8, each as a flag or policy with the defaults in *Later decisions*.
- **Decide before:** the session 15 row (the catalogue especially).

### Session 16: Bots, scenarios and balance
- **Prompt:** `Docs/sessions/session-16-bots-and-balance.md`
- **Model:** Opus 5.5.
- **Module and goal:** `XRim.DebugTools`, `XRim.Bots`, `XRim.Tests`.
  - `ScenarioStore` saves and loads (board, plans) as JSON.
  - Scenario regression tests replay saved fixtures.
  - A heuristic `IBotBrain` reads distance, weapons and HP, and mixes thrusts, swings, shields and body moves.
  - A headless bot-vs-bot batch, run from an Editor menu and in PlayMode, reports dismemberment rate, match length, weapon pick and win rates and sudden-death frequency against `BalanceTargetsConfig`.
- **GDD:** §11 (the 10–20% dismemberment target), §17 (bot), §1 (match length).
- **Depends on:** 14, 15.
- **Definition of done:**
  - At least 5 saved scenarios run as tests.
  - The batch of N matches writes a report to `Logs/` or `Docs/balance/`.
  - The heuristic bot beats the random bot most of the time.
  - `Tools/check` is green.
- **Unity Editor steps:** run the batch menu item; read the report (PT6).
- **TBDs:** §17 bot as a game mode (still a practice option only), §1 match length (a measured value is proposed, not decided).
- **Decide before:** the session 16 row. **PT6 follows this session.**

### Session 17: Art integration and skins
- **Prompt:** `Docs/sessions/session-17-art-and-skins.md`
- **Model:** Sonnet 5.5.
- **Module and goal:**
  - An importer menu maps your sprites onto `DummyView` and the weapon views according to `ART_SPEC.md`.
  - A skin definition (a `*Definition` asset) changes visuals only. A test proves that hitboxes, silhouette bounds and reach are unchanged.
  - The arena background, the electric wall visual and debris sprites.
- **GDD:** §2 (art direction), §16 (cosmetics cannot change hitboxes or readability).
- **Depends on:** 13, and your art (§3).
- **Definition of done:**
  - The art is visible in a match.
  - A test shows that swapping skins leaves every physics collider identical.
  - `Tools/check` is green.
- **Unity Editor steps:** put your PNGs in `Assets/XRim/Art/Incoming/`, run the importer menu, and check the sprite pivots.
- **TBDs:** §2 arena setting (whatever you supply).
- **Decide before:** the art is delivered.

### Session 18: Meta shell, profile and economy
- **Prompt:** `Docs/sessions/session-18-meta-and-economy.md`
- **Model:** Sonnet 5.5.
- **Module and goal:**
  - Main menu, settings (handedness change per D24, audio) and a first-launch handedness pick.
  - Local profile persistence (owned items, wallets, settings).
  - `Wallet` with earned and premium currency kept separate, and no conversion API.
  - `IRewardPolicy` default (match win only, flagged) and `IFarmingGuard` seam.
  - An unlock shop: weapons, shields and signature moves for earned currency only; cosmetics for either currency.
- **GDD:** §4 (handedness), §16, §2 (cosmetic audio packs as items).
- **Depends on:** 14, 15.
- **Definition of done:**
  - EditMode tests show gameplay unlocks cannot be bought with premium currency, that there is no conversion path, and that rewards follow the policy.
  - A profile save and load round-trip.
  - `Tools/check` is green.
- **Unity Editor steps:** build the menu scene through the menu item; test first launch by clearing the profile with a debug menu item.
- **TBDs:** all of §16 except the Decided rules, as flags or placeholders.
- **Decide before:** the session 18 row.

### Session 19: Online authority
- **Prompt:** `Docs/sessions/session-19-online-authority.md`
- **Model:** Opus 5.5.
- **Module and goal:** `XRim.Networking`.
  - Batch 1 presents backend and hosting options and **waits for your choice**.
  - A plan and result protocol (serialized `TurnPlan` and `TurnResult`).
  - A headless Unity server build that hosts `LocalTurnAuthority` and the simulator.
  - `RemoteTurnAuthority` on the client.
  - Server-clock validation of ink, lock-out and Ready time (§18).
  - Only public state crosses during planning.
  - The disconnect and reconnect seam.
- **GDD:** §18, §3 (disconnects), §7 in ARCHITECTURE.md.
- **Depends on:** 16, and the session 19 decisions.
- **Definition of done:**
  - Two Editor instances (or an Editor and a build) play a match through a locally hosted server.
  - EditMode tests cover protocol round-trips and the rejection of an over-budget or late plan.
  - `Tools/check` is green.
- **Unity Editor steps:** install the Dedicated Server build module; run the server and client launch steps Claude provides.
- **TBDs:** §18 networking model (server-authoritative recommended), disconnects.
- **Decide before:** the session 19 row.

### Session 20: Matchmaking, friends and contacts
- **Prompt:** `Docs/sessions/session-20-matchmaking-and-social.md`
- **Model:** Opus 5.5.
- **Module and goal:**
  - Lobby and matchmaking per your §17 decision.
  - Friend challenge.
  - The phone contacts feature, which is opt-in, works fully without the permission, and needs privacy review (GDPR, KVKK, store policies).
  - Reconnect behaviour.
- **GDD:** §17, §3 (disconnects), §18 (contacts permission).
- **Depends on:** 19.
- **Definition of done:**
  - Two phones match and play.
  - Refusing the contacts permission loses nothing else.
  - `Tools/check` is green.
- **Unity Editor steps:** device builds; the permission prompts on the device.
- **TBDs:** all §17 modes. Only the decided pieces are built fully.
- **Decide before:** the session 20 row. **PT7 follows this session.**

### Session 21: Tutorial and replays
- **Prompt:** `Docs/sessions/session-21-tutorial-and-replays.md`
- **Model:** Sonnet 5.5.
- **Module and goal:**
  - A guided first match teaching free drawing, body moves, clashes and the shield, per your §17 decision.
  - Saving replays of flagged legendary moments locally, using stored `TurnResult`s.
  - The share-clip seam (§17 TBD).
- **GDD:** §17, §1 pillar 1.
- **Depends on:** 13, 18.
- **Definition of done:**
  - The tutorial completes end to end.
  - A saved replay plays back identically.
  - `Tools/check` is green.
- **Unity Editor steps:** play the tutorial on a fresh profile.
- **TBDs:** §17 tutorial and replay sharing.
- **Decide before:** the session 21 row.

### Session 22: Release readiness
- **Prompt:** `Docs/sessions/session-22-release-readiness.md`
- **Model:** Sonnet 5.5.
- **Module and goal:**
  - Real-money purchases for premium currency and cosmetics only (Unity IAP), guarded by tests that nothing combat-related is purchasable.
  - Telemetry for the balance targets (dismemberment rate, match length).
  - Crash reporting.
  - A privacy policy hook and consent flow.
  - Store build settings, icons and versioning.
- **GDD:** §16, §17 (privacy), §0 (name and trademark check).
- **Depends on:** 18, 20, 21.
- **Definition of done:**
  - A sandbox purchase grants only premium currency.
  - Telemetry events are validated against a test sink.
  - A release build is produced.
  - `Tools/check` is green.
- **Unity Editor steps:** store accounts, the IAP package and the signing keys. Claude gives numbered steps.
- **TBDs:** §1 free-to-play or paid, age rating, currency names and prices.
- **Decide before:** the session 22 row.
