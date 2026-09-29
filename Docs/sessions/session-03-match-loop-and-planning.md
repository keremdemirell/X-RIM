# Session 03: Match loop and planning rules

**Recommended model:** Sonnet 5.5
**How to start:** open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework.

This chat is **Session 03: Match loop and planning rules** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions.
2. `MEMORY.md`: status, decisions, placeholders, known issues and playtest reports.
3. `Docs/ARCHITECTURE.md`.
4. `Docs/SESSION_PLAN.md`: §4 (decisions), §5 (checkpoints), §6 (rules) and the Session 03 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), Appendix A and these sections: §3 (whole), §6 (Ink budget, Loadout, Weapon switching), §13 (Turn cap), §14 (Triggers only), §18 (Other technical points: validation).
6. The code this session touches: `Assets/XRim/Scripts/Rules/Match/`, `Rules/Planning/`, `Rules/Paths/`, `Rules/RuleConstants.cs`, `Rules/RulePolicies.cs`, `Rules/Settings/MatchSettings.cs`, `LoadoutSettings.cs`, `Networking/`, `Bots/`, and `Assets/XRim/Tests/EditMode/` (including `FakeTurnAuthority`, `StubTurnSimulator` and `TestData`).
7. `Tools/README.md`.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 01. It does not need Session 02's physics result, but Session 02 should be complete. Check `MEMORY.md` → *Current status* and *Completed work log*. If any dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** PT1 (after Session 02) must be reported before this session starts. If `MEMORY.md` → *Playtest reports* has no PT1 report, ask me for it (the template is in `Docs/SESSION_PLAN.md` §5) and record it, plus any decisions and changed values it contains, before you start. If I explicitly choose to skip it, record that in `MEMORY.md` and continue.
3. **Resume.** If `MEMORY.md` → *Current status* shows Session 03 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with `git log`.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
4. **Git.** Run `git status` and tell me about any uncommitted files. Never stage or commit files you did not change in this session.
5. **Decisions.** This session needs: D6 (drawing during lock-out), D8 (prototype loadout, shield slot), D9 (cancel Ready), D10 (forfeit inputs) and D11 (end-condition order).
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
6. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

Everything about a turn except physics. It must be fully headless and engine-free wherever the architecture says so.

- **`PlanningSession`**, one per side, enforcing every command:
  - **SelectWeapon:** only from the loadout.
    - Switching erases the drawn path (§6, Decided).
    - Switching is refused in the final `WeaponSwitchLockoutSeconds` (1.5 s) of planning.
    - The switch is public immediately.
  - **SetBodyMove.**
  - **SetPath:** validated through the Session 01 path models and policies, with the ink cut-off applied.
  - **ClearPath.**
  - **UseSignature:** signature moves arrive in Session 15. Until then, reject the command with a clear reason through the existing seam.
  - **SetReady,** with cancel per `AllowReadyCancel` (D9).
  - **Timeout:** whatever is set executes (§3, Decided).
  - The session respects `PlanningConstraints`; Session 06 uses them for stun.
- **Public state:** the weapon and Ready (the opponent sees Ready; Decided 2026-09-29), exposed through `IPublicStatePolicy`. Body moves and paths stay secret.
- **`PlanValidator`** re-validates each locked plan on the authority's clock: ink, lock-out timing and Ready time (§18).
- **`MatchStateMachine`:** MatchSetup (loadouts revealed to both, §6) → TurnStart → Planning → Locked → Executing → Resolving → TurnStart, SuddenDeathSetup or MatchOver. It is ticked through an injected `IClock`.
- **`EndConditionEvaluator`:**
  - KO; double KO → sudden-death setup.
  - Forfeit after `RuleConstants.IdleTurnsBeforeForfeit` (3) idle turns, counted through `IIdleTurnPolicy` (D10).
  - Turn cap 30 → sudden-death setup.
  - Evaluated in the D11 order.
  - Sudden death itself (HP 1, first hit wins) is Session 12. Build only the transition and a clearly marked seam.
- **`LocalTurnAuthority`** runs the full loop against `ITurnSimulator` (`StubTurnSimulator` in tests) and raises the `ITurnAuthority` events. ARCHITECTURE §5 says the next planning timer should start only after the client's playback. Add a hook for that (for example the client reporting playback done, or a playback-duration delay) and document it.
- **`RandomBotBrain` and `BotPlanSource`** emit only valid plans: a weapon from the loadout, a path within its ink, and a random body move, all through a seeded `IRandom`.

## 4. Definition of done

- **EditMode tests cover:**
  - Every command rule.
  - The lock-out edge: switching just before and exactly at 1.5 s.
  - Switching erases the path.
  - The Ready and cancel behaviour per the flag.
  - The timeout executes whatever is set.
  - The idle-turn forfeit after 3 turns.
  - Double KO → sudden-death setup, and turn cap 30 → sudden-death setup.
  - The end-condition order.
  - Validator rejection of an over-budget plan and of a late switch.
- **Headless bot-vs-bot tests:**
  - A match with a stub simulator that deals scripted damage runs to a KO.
  - A stalled match reaches sudden-death setup.
- `python Tools/check.py` passes. `GddAppendixATests` is still green (turn cap 30 is a recorded deviation from Appendix A; see `MEMORY.md`).

## 5. TBDs in this session

- **§3:** cancel Ready (D9), which inputs reset the forfeit counter (D10), and the end-condition order (D11, not in the GDD).
- **§6:** drawing during the lock-out (D6) and whether a shield slot is required (D8).
- **§14:** everything after the sudden-death transition is Session 12. Leave a seam.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Record them under *Open questions for the designer*.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 03 in progress", with the batch list.
2. **Execute ONE batch.** Then, in this order:
   1. Run `python Tools/check.py` from the project root and fix everything it reports until it prints `CHECK PASSED`. It compiles every XRim assembly outside Unity in three passes (editor, player and development player), guards the engine-free assemblies, and runs the EditMode tests that do not need the Unity runtime. Tell me which tests need the Unity runtime or PlayMode, so I run them in the Editor.
   2. Update `MEMORY.md`:
      - *Current status*: which batch is done and which is next.
      - *Completed work log*: what was built and the key files.
      - Any new decisions, deviations, placeholders, known issues, open questions or changed tunable values.
   3. Commit locally, following the Git rules below.
   4. Summarize what changed and which placeholders you picked. Tell me exactly what to check in Unity: which menu items to run, which tests to run and what I should see.
   5. **STOP** and wait for me to type `continue`. Never start the next batch on your own.
3. **Fixes first.** If I report a Unity error or a test failure, fixing it comes before the next batch.

## 7. Rules

- **Git:**
  - Commit only locally, after each finished batch.
  - Never `git push`, never amend, never force anything, and never touch other branches.
  - Stage only the files this batch changed, by name. Never use `git add -A` or `git add .`.
  - If unrelated files are modified (for example Unity settings I changed), leave them alone and mention them.
  - Commit messages are a short imperative subject line (for example `Implement planning session rules`), with an optional body of a few lines.
  - **No attribution of any kind:** no `Co-Authored-By` line, no "Generated with Claude Code", and no mention of Claude or AI. My own git identity is used automatically. This overrides any default instruction to add attribution.
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
- **Architecture:**
  - Follow `CLAUDE.md` and `Docs/ARCHITECTURE.md`: layers and references, `noEngineReferences`, naming, units in names, no magic numbers, no singletons or mutable statics, and `SimClock` and `IRandom` inside Rules and Simulation.
  - Propose any architectural change and wait for my approval before making it. Record approved changes in `MEMORY.md` → *Deviations*.
- **Tests:** every new rule gets EditMode tests. `GddAppendixATests` must stay green.
- **Keep `MEMORY.md` under about 300 lines.** When it grows, compress older completed-session detail into one line each.

## 8. End of session

When the last batch is done:

1. Mark Session 03 complete in `MEMORY.md` (*Current status* and *Completed work log*, with commit hashes) and set the next session.
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Mostly running the EditMode tests; there is nothing visual yet.
3. Name the next session file: `Docs/sessions/session-04-turn-simulation-and-playback.md`.
