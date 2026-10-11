# Session 20: Matchmaking, friends and contacts

**Recommended model:** Opus 5.5 (or the strongest model available when you start)
**How to start:** first commit the previous session's work if you have not yet. Then open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework. I am the game's designer, not a programmer.

This chat is **Session 20: Matchmaking, friends and contacts** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions. Where it differs from `MEMORY.md` or this prompt on how we work (for example git), `MEMORY.md` and this prompt win, because they are newer.
2. `MEMORY.md`, all of it: status, decisions, placeholders, deviations, known issues, open questions and playtest reports.
3. `Docs/ARCHITECTURE.md`. Earlier sessions kept it up to date with what they built.
4. `Docs/SESSION_PLAN.md`: §4 (decisions, including *Later decisions*), §5 (checkpoints), §6 (rules) and the Session 20 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), §20 (parked and rejected ideas), Appendix A and these sections: §17 (whole), §3 (Disconnects), §18 (Other technical points: contacts permission flow and privacy).
6. `Tools/README.md`: the compile-check tool.
7. The code this session touches: `Assets/XRim/Scripts/Networking/` (Session 19's remote authority, protocol and transport), `App/` (menu and match flow), `Economy/` (`IFarmingGuard` for friend matches), the profile code from Session 18, and `Editor/` (Android build menus). Read the existing code before proposing anything, because much of what this session needs already has a seam.
8. `MEMORY.md`'s Session 10 notes on the phone build and the Android edge-back gesture.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 19. Check `MEMORY.md` → *Current status* and *Completed work log*. If a dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** No playtest checkpoint is due before this session.
3. **Notes left for this session.** Search `MEMORY.md` for "Session 20". Earlier sessions left wiring notes, open points and feel notes addressed to this session. List every one in your batch plan, and for each either cover it or tell me why not.
4. **Resume.** If `MEMORY.md` → *Current status* shows Session 20 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with the files on disk and `git status`. Batches are not committed one by one.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
5. **Git.** Run `git status`.
   - I commit each session myself when it is done. If the previous session's work is still uncommitted, tell me and ask whether I want to commit it before you change anything.
   - Never stage or commit anything yourself unless I ask.
6. **Decisions.** This session needs the Session 20 row of *Later decisions*: §17 random matchmaking with strangers or friends only, ranked or not, challenging phone contacts directly (proposed, TBD), and a friends list beyond contacts. Also: how players are identified (an account or sign-in provider). Present the options for identity with their privacy cost, and wait.
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4, with one line on how it serves feel. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
7. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **Lobby and matchmaking** behind an interface, per my §17 decision.
- **Friend challenge** through an invite code or link. This is the path that needs no permission.
- **Phone contacts (§17, Decided: shows contacts who also play).**
  - Explicit opt-in with a clear explanation.
  - It must follow GDPR, KVKK and the App Store and Google Play policies; propose a privacy-preserving matching approach (for example hashed identifiers) and ask me.
  - The game works fully without the permission.
  - The Android contacts permission is requested only when I tap the feature.
- **Reconnect** behaviour through Session 19's seams, tested on two phones.
- **Friend matches** go through `IFarmingGuard` (§16: two friends farming each other).

## 4. Definition of done

- Two phones find each other (or use an invite) and play a full match.
- Refusing the contacts permission loses nothing else.
- A dropped connection reconnects within the window.
- EditMode tests cover matchmaking state, invite codes and the permission-off path.
- `python Tools/check.py` passes, and the Unity-side and PlayMode tests pass in the Editor.

## 5. TBDs in this session

- **§17:** matchmaking, ranked play, challenging contacts directly, and a friends list. Only the decided pieces are built fully; the rest are seams.
- **§3:** disconnects use Session 19's flags.
- **§16:** farming protection in friend matches is the `IFarmingGuard` default.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Bring each one with a recommended answer chosen for feel, and record it under *Open questions for the designer* until I answer.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Also list any architectural change (new references between assemblies, reshaped interfaces, new seams), because I approve those explicitly.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 20 in progress", with the batch list.
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

1. Mark Session 20 complete in `MEMORY.md` (*Current status* and *Completed work log*) and set the next session. Write "not committed yet: the designer commits it".
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. This is PT7, on two phones: include matchmaking, an invite, a deliberate disconnect and reconnect, and refusing the contacts permission.
3. List every file the session changed or added, and propose a commit message (a short imperative subject and a few body lines, no attribution) for me to use.
4. Finish with a short **What this means for you** wrap-up in plain language.
5. Name the next session file: `Docs/sessions/session-21-tutorial-and-replays.md`.
6. **Playtest checkpoint PT7 follows this session.** Tell me what to test (see `Docs/SESSION_PLAN.md` §5) and show me the report template:
   ```
   PT7 report, <date>
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
