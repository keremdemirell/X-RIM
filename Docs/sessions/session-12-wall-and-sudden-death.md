# Session 12: Electric wall, arena edge and sudden death

**Recommended model:** Opus 5.5 (or the strongest model available when you start)
**How to start:** first commit the previous session's work if you have not yet. Then open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework. I am the game's designer, not a programmer.

This chat is **Session 12: Electric wall, arena edge and sudden death** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions. Where it differs from `MEMORY.md` or this prompt on how we work (for example git), `MEMORY.md` and this prompt win, because they are newer.
2. `MEMORY.md`, all of it: status, decisions, placeholders, deviations, known issues, open questions and playtest reports.
3. `Docs/ARCHITECTURE.md`. Earlier sessions kept it up to date with what they built.
4. `Docs/SESSION_PLAN.md`: §4 (decisions, including *Later decisions*), §5 (checkpoints), §6 (rules) and the Session 12 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), §20 (parked and rejected ideas), Appendix A and these sections: §3 (Ending a match), §5 (the backward swipe), §7 (the shield never triggers the wall), §13 (whole), §14 (whole).
6. `Tools/README.md`: the compile-check tool.
7. The code this session touches: `Assets/XRim/Scripts/Rules/Arena/` (`ElectricWallRules` still throws, `ElectricWallState`, `ElectricWallPhase`, `IArenaEdgePolicy`), `Rules/SuddenDeath/`, `Rules/Match/` (`EndConditionEvaluator` and its marked sudden-death seam, `SuddenDeathSetup`, `MatchStateMachine`, `ExecutionReport`), `Rules/Settings/ElectricWallSettings.cs`, `SuddenDeathSettings.cs` and `ArenaSettings.cs`, `Simulation/` (`TurnBoard`, contact handling), `Simulation.Unity2D/`, `App/SuddenDeathTiePlaceholder.cs` and `MatchBootstrap.cs`, `Presentation/UI/`, `DebugTools/`, and the tests. Read the existing code before proposing anything, because much of what this session needs already has a seam.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 11. Sessions 12 and 13 may run in either order. If Session 13 is already complete, also feed the wall zap and the sudden-death finish into its feel hooks (VFX, audio, camera, moment detector). Check `MEMORY.md` → *Current status* and *Completed work log*. If a dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** No playtest checkpoint is due before this session, unless Session 13 is already complete; then PT5 (after Session 13) must be reported before this session starts. If `MEMORY.md` → *Playtest reports* has no PT5 report, ask me for it (the template is in `Docs/SESSION_PLAN.md` §5) and record it, plus any decisions and changed values it contains, before you start. If I explicitly choose to skip it, record that in `MEMORY.md` and continue.
3. **Notes left for this session.** Search `MEMORY.md` for "Session 12". Earlier sessions left wiring notes, open points and feel notes addressed to this session. List every one in your batch plan, and for each either cover it or tell me why not.
4. **Resume.** If `MEMORY.md` → *Current status* shows Session 12 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with the files on disk and `git status`. Batches are not committed one by one.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
5. **Git.** Run `git status`.
   - I commit each session myself when it is done. If the previous session's work is still uncommitted, tell me and ask whether I want to commit it before you change anything.
   - Never stage or commit anything yourself unless I ask.
6. **Decisions.** This session needs the Session 12 row of *Later decisions* in `Docs/SESSION_PLAN.md` §4:
   - §13 whether wall damage grows with each retreat, and whether a dummy knocked into an active wall takes wall damage.
   - §14 the sudden-death board state, what happens when nobody hits (and the fallback when its cap runs out), an exact same-step tie, the electric wall in sudden death, and whether switching and signature moves stay available.
   - Note that the setup code defaults differ from the plan in places: `ElectricWallSettings.ActiveInSuddenDeath` is `true` (the plan recommends off, because at 1 HP a wall hit is fatal) and `SuddenDeathSettings.MaxTurnsWithoutHit` is `0`. Show me both and let me choose.
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4, with one line on how it serves feel. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
7. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

**Electric wall (§13, Decided).** Implement `ElectricWallRules`, one wall per player; both can be active at once. Only the player's input triggers it:
- A backward body move triggers it, read at lock from the locked plan.
- Being knocked backwards by a hit, holding a shield, or standing still never counts.

The sequence:

| Turn | Player's input | What happens |
|---|---|---|
| N | backward swipe | A wall appears behind the dummy as a warning. No damage. |
| N+1 | any other move, including no body move | The wall goes away completely. |
| N+1 | another backward swipe | The dummy hits the wall: electric damage plus a bounce, and the wall moves further forward. |
| N+2 and later | backward again | Damage and bounce again, and the wall advances again. |

- Damage, bounce and advance are Tunable placeholders in `ElectricWallSettings`. Growth per retreat and damage when knocked into the wall are TBD flags.
- **In the simulation:** an active wall is a physics body at its position. It is part of the board (captured in the snapshot, rebuilt by `Load`).
- A contact between the dummy and the wall is resolved by the rules: `ElectricWallEvent`, damage that bypasses zone multipliers unless you propose otherwise, and the bounce impulse.
- Propose the exact geometry and wait for my approval: "behind the dummy" measured from where, "forward" toward the opponent, and how the wall interacts with the D22 arena edge (a solid stop).

**Sudden death (§14).** The Decided parts:
- Triggers: a double KO, or the turn cap.
- Both dummies are set to 1 HP.
- The first valid hit wins.
- If both hit, the hit with the earlier time-to-impact wins, compared in microseconds of `SimTime` (`ExecutionReport.FirstValidHitTime`, `FirstHitsOnSameStep`).

Replace the marked seam in `EndConditionEvaluator` with the TBD policies:
- `SuddenDeathSettings.BoardMode`, carry over or reset. Decide what carries over: floor limbs, clubs and walls. A split torso from a double KO is rebuilt whole (the Session 11 rule, S5).
- `ISuddenDeathNoHitPolicy`: repeat the turn, with a cap and a fallback.
- `ITiePolicy`: an exact same-step tie.
- `ElectricWallSettings.ActiveInSuddenDeath`.
- The option flags (weapon switching, signature moves), as `PlanningConstraints`.

Nothing may loop for ever. `MEMORY.md` notes that a shield standoff in sudden death currently never ends.

**Client.** Retire `App/SuddenDeathTiePlaceholder` and the interim banner text. Add:
- A clear sudden-death banner.
- A visible wall warning on the board (a placeholder look until Session 13 or 17).
- Wall hits in the match-event text and the debug panels.

**Debug.** Add cheats: set the turn counter (to reach the cap quickly), force a double KO, and show each wall's phase.

## 4. Definition of done

- **EditMode tests:**
  - Every row of the wall sequence table.
  - Knockback, the shield and standing still never trigger the wall.
  - Both walls active at once.
  - The growth and knocked-into flags.
  - Sudden-death setup (HP 1), first hit wins, the earlier `SimTime` wins, the same-step tie policy, and the no-hit policy with its cap and fallback.
  - Each board mode and the option flags.
- **PlayMode:**
  - Two retreats in a row → zap and bounce.
  - The wall persists across turns through the snapshot.
- **Headless:** a 30-turn stall enters sudden death and always ends.
- `python Tools/check.py` passes, and the Unity-side and PlayMode tests pass in the Editor.

## 5. TBDs in this session

- **§13:** whether wall damage grows per consecutive retreat (`DamageGrowthPerConsecutiveRetreat`), wall damage when knocked into it (`DamageWhenKnockedInto`), and the arena width (still the D22 placeholder).
- **§14:** board state, no hit, exact tie, the wall in sudden death, and the options. Each is a flag or policy with my decision or the plan default.
- **Not in the GDD:** the exact wall geometry, and whether wall damage uses zone multipliers. Propose and ask.
- **Rejected; do not build:** an arena that shrinks every turn.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Bring each one with a recommended answer chosen for feel, and record it under *Open questions for the designer* until I answer.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Also list any architectural change (new references between assemblies, reshaped interfaces, new seams), because I approve those explicitly.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 12 in progress", with the batch list.
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

1. Mark Session 12 complete in `MEMORY.md` (*Current status* and *Completed work log*) and set the next session. Write "not committed yet: the designer commits it".
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Include: retreat twice in a match and watch the zap; reach the turn cap with the cheat; force a double KO; play a sudden-death standoff to its end.
3. List every file the session changed or added, and propose a commit message (a short imperative subject and a few body lines, no attribution) for me to use.
4. Finish with a short **What this means for you** wrap-up in plain language.
5. Name the next session file: `Docs/sessions/session-13-feel-pass.md`, or `Docs/sessions/session-14-roster-and-loadout.md` if Session 13 is already complete.
