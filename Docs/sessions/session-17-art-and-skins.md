# Session 17: Art integration and skins

**Recommended model:** Sonnet 5.5
**How to start:** first commit the previous session's work if you have not yet. Then open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework. I am the game's designer, not a programmer.

This chat is **Session 17: Art integration and skins** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions. Where it differs from `MEMORY.md` or this prompt on how we work (for example git), `MEMORY.md` and this prompt win, because they are newer.
2. `MEMORY.md`, all of it: status, decisions, placeholders, deviations, known issues, open questions and playtest reports.
3. `Docs/ARCHITECTURE.md`. Earlier sessions kept it up to date with what they built.
4. `Docs/SESSION_PLAN.md`: §4 (decisions, including *Later decisions*), §5 (checkpoints), §6 (rules) and the Session 17 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), §20 (parked and rejected ideas), Appendix A and these sections: §2 (Art direction), §12 (Presentation), §16 (Cosmetics: no cosmetic may change hitboxes, silhouette, reach or readability), §20 (Rejected: fighters with different gameplay stats).
6. `Tools/README.md`: the compile-check tool.
7. The code this session touches: `Docs/ART_SPEC.md` (from Session 13), `Assets/XRim/Scripts/Presentation/Dummy/DummyView.cs`, `Presentation/Arena/`, `Presentation/Vfx/`, `Config/` (a new skin `*Definition`), `Editor/` (the importer), and `Simulation.Unity2D/PlaceholderRagdollBuilder` (read only: the physics shapes must not change). Read the existing code before proposing anything, because much of what this session needs already has a seam.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 13 (for `Docs/ART_SPEC.md`), and my art delivered in `Assets/XRim/Art/Incoming/`. If that folder is empty, stop and tell me what is missing, using `Docs/SESSION_PLAN.md` §3. Check `MEMORY.md` → *Current status* and *Completed work log*. If a dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** PT6 (after Session 16) must be reported before this session starts. If `MEMORY.md` → *Playtest reports* has no PT6 report, ask me for it (the template is in `Docs/SESSION_PLAN.md` §5) and record it, plus any decisions and changed values it contains, before you start. If I explicitly choose to skip it, record that in `MEMORY.md` and continue. (If Session 16 is not complete yet, no checkpoint is due.)
3. **Notes left for this session.** Search `MEMORY.md` for "Session 17". Earlier sessions left wiring notes, open points and feel notes addressed to this session. List every one in your batch plan, and for each either cover it or tell me why not.
4. **Resume.** If `MEMORY.md` → *Current status* shows Session 17 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with the files on disk and `git status`. Batches are not committed one by one.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
5. **Git.** Run `git status`.
   - I commit each session myself when it is done. If the previous session's work is still uncommitted, tell me and ask whether I want to commit it before you change anything.
   - Never stage or commit anything yourself unless I ask.
6. **Decisions.** This session needs: my art (check it against `Docs/ART_SPEC.md` and list anything missing or off-spec before building), §2 the default arena setting (whatever my background shows), and whether to keep the placeholder look selectable as a debug skin.
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4, with one line on how it serves feel. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
7. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **Importer menu,** for example `XRim/Art/Import Incoming Art`. It applies sprite import settings (pixels per unit matched to the 0.01 world units per arena unit, filter, compression), slices sheets, and sets pivots per `ART_SPEC.md`. It then builds a skin definition asset in code.
- **`DummyView` renders sprites per part:** the 10 segments, the torso halves for the death split, floor limbs and the club. It falls back to the placeholder shape for any missing sprite. The yellow-and-black markers must match the hit zones (§2, Decided).
- **Weapon views** for rapier, sword, spear, mace, shield, club and the body-blow look.
- **Debris sprites** in `MatchVfx`, the electric-wall visual, and the arena background.
- **A skin `*Definition` changes visuals only.** It lives in Presentation, and Economy can sell it later; it can never reach the rules or the physics.
- **Readability check.** An Editor report or test compares each skin's visible silhouette bounds with the physics shapes, within a tolerance, because skins must keep the same silhouette and reach.

## 4. Definition of done

- My art is visible in a full match on the phone.
- A test shows that swapping skins leaves every physics collider and mass identical.
- The silhouette check passes for the delivered skin.
- Missing sprites fall back cleanly.
- `python Tools/check.py` passes, and the Unity-side and PlayMode tests pass in the Editor.

## 5. TBDs in this session

- **§2:** the default arena setting is whatever I deliver; the crash-test facility is the proposal.
- **§16:** which skins exist and their prices are not this session's concern.
- **Rejected; do not build:** fighters with different gameplay stats.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Bring each one with a recommended answer chosen for feel, and record it under *Open questions for the designer* until I answer.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Also list any architectural change (new references between assemblies, reshaped interfaces, new seams), because I approve those explicitly.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 17 in progress", with the batch list.
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

1. Mark Session 17 complete in `MEMORY.md` (*Current status* and *Completed work log*) and set the next session. Write "not committed yet: the designer commits it".
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Include running the importer, checking the sprite pivots on a bent arm and leg, and a death split and sever with real art.
3. List every file the session changed or added, and propose a commit message (a short imperative subject and a few body lines, no attribution) for me to use.
4. Finish with a short **What this means for you** wrap-up in plain language.
5. Name the next session file: `Docs/sessions/session-18-meta-and-economy.md`.
