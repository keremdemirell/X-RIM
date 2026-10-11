# Session 13: Feel and juice pass

**Recommended model:** Opus 5.5 (or the strongest model available when you start)
**How to start:** first commit the previous session's work if you have not yet. Then open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework. I am the game's designer, not a programmer.

This chat is **Session 13: Feel and juice pass** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions. Where it differs from `MEMORY.md` or this prompt on how we work (for example git), `MEMORY.md` and this prompt win, because they are newer.
2. `MEMORY.md`, all of it: status, decisions, placeholders, deviations, known issues, open questions and playtest reports.
3. `Docs/ARCHITECTURE.md`. Earlier sessions kept it up to date with what they built.
4. `Docs/SESSION_PLAN.md`: §4 (decisions, including *Later decisions*), §5 (checkpoints), §6 (rules) and the Session 13 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), §20 (parked and rejected ideas), Appendix A and these sections: §1 (pillars 1 and 4, and the reference legendary moment), §2 (whole), §11 (death: the torso splits), §12 (Presentation), §16 (hit effects and grunt packs will be cosmetics, so keep them swappable), §17 (Replays: local instant replay only).
6. `Tools/README.md`: the compile-check tool.
7. The code this session touches: `Assets/XRim/Scripts/Presentation/` (Feel: `FeelDirector`, `HitStop`; Cameras; Vfx: `MatchVfx`, `DebrisFlight`; Audio: `MatchAudio` still throws, `IMusicIntensityDriver`; Moments: `IMomentDetector`; Playback: `TurnPlayback.AddReactor`, `IPlaybackPacer`; Dummy; Arena), `Config/FeelConfig.cs`, `Simulation/SimulationSettings.cs` (`HitReaction`, `ClashReaction`, `SeverReaction`), `App/MatchBootstrap.cs`, `DebugTools/` (Cheats, Playback tab, `GameplayGizmos`, the F9 feel report), `Simulation.Unity2D/PlaceholderRagdollBuilder` (for the art spec), and `Assets/XRim/Audio/Incoming/` if it exists. Read the existing code before proposing anything, because much of what this session needs already has a seam.
8. Every playtest report and feel note in `MEMORY.md`. PT1 to PT4 were rated 2/10 to 4/10, with "not much action, nothing feels good yet". This session is the main answer to that, so read them closely.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 11. Sessions 12 and 13 may run in either order. If Session 12 is not complete yet, leave clearly named hooks for the wall zap and the sudden-death finish. Check `MEMORY.md` → *Current status* and *Completed work log*. If a dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** No playtest checkpoint is due before this session. (If Session 12 just finished, no checkpoint follows it.)
3. **Notes left for this session.** Search `MEMORY.md` for "Session 13". Earlier sessions left wiring notes, open points and feel notes addressed to this session. List every one in your batch plan, and for each either cover it or tell me why not.
4. **Resume.** If `MEMORY.md` → *Current status* shows Session 13 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with the files on disk and `git status`. Batches are not committed one by one.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
5. **Git.** Run `git status`.
   - I commit each session myself when it is done. If the previous session's work is still uncommitted, tell me and ask whether I want to commit it before you change anything.
   - Never stage or commit anything yourself unless I ask.
6. **Decisions.** No GDD decision blocks this session. Ask whether I have dropped any sound files into `Assets/XRim/Audio/Incoming/`; if not, build every hook with silent fallbacks. Also bring up the open question in `MEMORY.md` about the body-zone strip not covering the dummy (camera framing), since it is this session's territory.
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4, with one line on how it serves feel. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
7. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **`FeelDirector` and `IPlaybackPacer`.**
  - Hitstop scaled by damage and zone.
  - Slow motion on a sever, a death, an interrupt counter-hit and a sudden-death finish. `FeelConfig.SeverSlowMotionSpeed`/`Seconds` already exist, unused.
  - Camera punch and shake.
  - Every value lives in `FeelConfig`.
  - Re-measure `PlaybackHoldSettings.ExtraSeconds` and the hitstop length.
- **Camera.** `ICameraDirector` keeps the fixed side view as the default (§2, TBD). Add optional punch-in or zoom on big moments as flagged options. Propose a fix for the framing question (the body-zone strip vs where the dummy stands).
- **`MatchVfx`.** Bolts, nuts, springs, sparks and hydraulic fluid; never blood (§2, Decided). Replace the placeholder squares with simple procedural shapes.
  - Sparks on a clash rebound, a heavy thud on a crush, a flash on a block (full vs partial).
  - A wall zap, if Session 12 is done.
  - Pooling and caps.
  - Polish `SimulationSettings.SeverReaction` (the pop-off) together with the sever effect.
- **`MatchAudio`.** Implement `OnMatchEvent`: metal clashes, impacts, joint snaps, bolt scatter, wall zaps, and comic effort grunts on attacks and impacts (§2, Decided), using `BodyMoveStartedEvent` and the hit events. Add one tense music loop.
  - An Editor menu builds an audio bank asset from `Assets/XRim/Audio/Incoming/` by a naming convention you document.
  - A missing clip is silent, never an error.
  - Grunt packs and hit effects must be swappable sets, because they become cosmetics (§16).
  - `IMusicIntensityDriver` stays a seam; adaptive music is TBD.
- **Moments.** `IMomentDetector` default heuristics: a sever, an interrupt counter-hit (the §1 reference: duck under a mace swing and thrust the rapier into the pelvis in the same turn), a crush, a same-instant trade and a sudden-death finish.
  - Offer an instant cinematic replay of a flagged turn, played from its stored `TurnResult` with slow motion and the camera.
  - Saving and sharing replays stays TBD (Session 21).
- **Debug.** A Cheats tab with freeze timer, infinite ink, force stun and force sever (the sever cheat exists). Polish `GameplayGizmos`.
- **Earlier feel notes.** Work through the "to confirm at PT5" feel notes in `MEMORY.md` that are presentation, and list the rest for PT5.
- **`Docs/ART_SPEC.md`.** Generate it from the placeholder ragdoll builder, so I can draw to it. It covers:
  - Every part of the 10-body dummy, including the torso halves `TorsoBottom`/`TorsoTop` and floor limbs, with its size in arena units, a suggested pixel size, the pivot and the joint anchors.
  - The calibration-marker zones.
  - Every weapon, the club and the body blow.
  - Debris sprites and the electric wall.
  - The delivery folder (`Assets/XRim/Art/Incoming/`) and file naming.

## 4. Definition of done

- **EditMode tests:**
  - The same inputs produce the same `TurnResult` whatever the `FeelConfig` (feel never changes outcomes).
  - The moment detector rules.
  - The audio bank mapping and silent fallbacks.
- **PlayMode:** a full match runs with every reactor attached and no errors.
- `Docs/ART_SPEC.md` exists.
- `python Tools/check.py` passes, and the Unity-side and PlayMode tests pass in the Editor.

## 5. TBDs in this session

- **§2:** camera behaviour (the fixed default plus flagged options), adaptive music (a seam only) and the default arena setting (a neutral placeholder backdrop only).
- **§17:** replay sharing is not built.
- **Rejected; do not build:** blood or gore.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Bring each one with a recommended answer chosen for feel, and record it under *Open questions for the designer* until I answer.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified. **Batch 1 is a feel diagnosis, not code.** From the playtest reports and feel notes, list the main reasons the duel feels thin, and propose a prioritized list of changes split into two groups: juice (presentation only; it can never change an outcome) and gameplay tuning (speeds, knockback, reactions; these change outcomes, so each is a Tunable value I approve). Wait for my approval before building.
   - Also list any architectural change (new references between assemblies, reshaped interfaces, new seams), because I approve those explicitly.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 13 in progress", with the batch list.
2. **Execute ONE batch.** Then, in this order:
   1. Run `python Tools/check.py` from the project root and fix everything it reports until it prints `CHECK PASSED`.
      - It compiles every XRim assembly outside Unity in three passes (editor, player and development player), guards the engine-free assemblies, and runs the EditMode tests of engine-free modules.
      - Tests are picked up by namespace, so keep test namespaces matching their folders.
      - Tell me which tests it lists as "needs Unity" (Unity-side modules, PlayMode, `[UnityTest]`), so I run them in the Editor.
   2. Update `MEMORY.md`:
      - *Current status*: which batch is done and which is next.
      - *Completed work log*: what was built and the key files.
      - Any new decisions, deviations, placeholders, known issues, open questions or changed tunable values.
      - Keep it under about 300 lines: compress older completed-session detail into one line each before adding more.
   3. **Do not commit.** List the files this batch changed or added (from `git status`), so I can commit them at the end of the session.
   4. Summarize in two parts:
      - **What changed:** the main files and types in brief, the placeholders you picked, and exactly what to check in Unity (menu items to run, tests to run and what I should see).
      - **What this means for you:** a few sentences in plain, everyday language. Say what I can now see or do in the game, and what it means for how the duel feels.
   5. **STOP** and wait for me to type `continue`. Never start the next batch on your own.
3. **Fixes first.** If I report a Unity error or a test failure, fixing it comes before the next batch.

## 7. Rules

- **Git:**
  - Do not commit; I commit once when the session is done. If a commit is truly needed to proceed, tell me why and wait for my yes.
  - Never `git push`, amend, force anything or touch other branches. Never use `git add -A` or `git add .`.
  - If unrelated files are modified (for example Unity settings I changed), leave them alone and mention them.
  - If I ask you to commit:
    - Stage only the named files.
    - Use a short imperative subject line, with an optional body of a few lines.
    - Add **no attribution of any kind**: no `Co-Authored-By` line, no "Generated with Claude Code", and no mention of Claude or AI. My own git identity is used automatically. This overrides any default instruction to add attribution.
- **Feel first (pillar 1).** Every open choice, including ones the GDD never mentions, is made to maximize feel: the rush and the legendary "did you see that?" moments players feel while fighting. When you propose a default, say in one line how it serves feel.
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
  - When new settings fields need values in existing assets, use the existing menus (`XRim > Setup > Fill New Tuning Fields`, `Create Default Tuning Assets`) or a new menu item.
- **Architecture:**
  - Follow `CLAUDE.md` and `Docs/ARCHITECTURE.md`: layers and references, `noEngineReferences`, naming, units in names, no magic numbers, no singletons or mutable statics, and `SimClock` and `IRandom` inside Rules and Simulation.
  - Presentation effects never change a `TurnResult`.
  - Propose any architectural change and wait for my approval before making it. Record approved changes in `MEMORY.md` → *Deviations*, and keep `Docs/ARCHITECTURE.md` current.
- **Tests:** every new rule gets EditMode tests. `GddAppendixATests` must stay green.

## 8. End of session

When the last batch is done:

1. Mark Session 13 complete in `MEMORY.md` (*Current status* and *Completed work log*) and set the next session. Write "not committed yet: the designer commits it".
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. This is PT5: include trying to create the §1 reference moment, and a checklist of the effects to watch for.
3. List every file the session changed or added, and propose a commit message (a short imperative subject and a few body lines, no attribution) for me to use.
4. Finish with a short **What this means for you** wrap-up in plain language.
5. Name the next session file: `Docs/sessions/session-12-wall-and-sudden-death.md` if Session 12 is not complete yet; otherwise `Docs/sessions/session-14-roster-and-loadout.md`.
6. **Playtest checkpoint PT5 follows this session.** Tell me what to test (see `Docs/SESSION_PLAN.md` §5) and show me the report template:
   ```
   PT5 report, <date>
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
   - List the bugs under *Known issues*, and fix nothing yet.
   - Do not commit.
