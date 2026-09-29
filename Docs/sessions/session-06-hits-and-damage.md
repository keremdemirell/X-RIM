# Session 06: Hits, priority and damage

**Recommended model:** Opus 5.5 (or the strongest model available when you start)
**How to start:** open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework.

This chat is **Session 06: Hits, priority and damage** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions.
2. `MEMORY.md`: status, decisions, placeholders, known issues and playtest reports.
3. `Docs/ARCHITECTURE.md`.
4. `Docs/SESSION_PLAN.md`: §4 (decisions), §5 (checkpoints), §6 (rules) and the Session 06 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), Appendix A and these sections: §9 (Priority and counter-hits, Open points), §11 (whole), §12 (limb damage only; no severing yet), Appendix A.
6. The code this session touches: `Assets/XRim/Scripts/Rules/Combat/`, `Rules/Damage/`, `Rules/Status/`, `Rules/Events/`, `Rules/Limbs/LimbRules.cs`, `Rules/Settings/DamageSettings.cs` and `HitZoneSettings.cs`, `Rules/Planning/PlanningConstraints.cs`, `Simulation/Execution/`, `Simulation.Unity2D/`, `DebugTools/`, and the tests.
7. `Tools/README.md`.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Session 04. Session 05 is recommended; if it is not complete, tell me and ask whether to continue. Check `MEMORY.md` → *Current status* and *Completed work log*. If any dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** No playtest checkpoint is due before this session.
3. **Resume.** If `MEMORY.md` → *Current status* shows Session 06 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with `git log`.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
4. **Git.** Run `git status` and tell me about any uncommitted files. Never stage or commit files you did not change in this session.
5. **Decisions.** This session needs: D15 (which hits interrupt), D16 (heavy swing armour), D17 (stun and stagger effect), D18 (HP scale and damage placeholders), D26 (hits per weapon per turn, and whether the weapon continues after a hit) and D27 (how "no instant KO from one head hit" is enforced).
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
6. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **Rules (engine-free):**
  - **`HitFacts` → `DamageCalculator`:** Damage = BaseDamage × ZoneMultiplier × Modifiers (§11).
    - Base damage is designer-set, never physics energy (the ½mv² model is Rejected).
    - Modifiers are an `IDamageModifier` list: the off-hand penalty and the crush-through reduction are hooks here (their values live in settings); shield reduction arrives in Session 07 and traits in Session 14.
  - **Zone multipliers:** head, torso, arm and leg, from `HitZoneSettings`.
  - **Limb damage:** one global HP pool, plus per-limb damage for arms and legs. Limb damage never heals. A single hit adds at most `PerHitLimbCapFraction` (35%) of the limb's durability, so severing needs at least 3 hits. The sever itself is Session 11; just emit the fact that durability reached 0.
  - **Head:** a single head hit can never cause an instant KO (§11, Decided); enforce it per D27. A head hit above the stun threshold applies a stun. `IStatusEffect` (D17) edits the next turn's `PlanningConstraints`, and `PlanningSession` respects them.
  - **Priority:** the first weapon to reach a valid hitbox lands its hit (§9, Decided). A dummy hit before its own attack lands has that attack interrupted, as `IInterruptPolicy` (D15) and `WeaponStats.HasSwingArmour` (D16) decide.
  - **Hits per weapon (D26):** how many times a weapon can deal damage in one turn, and whether it continues its path after a hit. The GDD does not say; use the D26 answer or its flagged default.
  - **Death:** HP reaching 0 emits `FighterDiedEvent`. What follows is handled by the Session 03 end conditions.
  - Only weapons (held items) deal damage. Body-to-body bumps deal none. Armless body blows are Session 11.
- **Simulation:**
  - Weapon-to-body contacts become `HitFacts`: the zone from the part tag, the weapon from the held item, and the time refined to path progress. Ignore a fighter's contacts with its own body.
  - Apply the outcomes: cancel the interrupted driver, apply a hit impulse (Tunable), and record `HitLandedEvent`, `AttackInterruptedEvent`, `StatusAppliedEvent` and `FighterDiedEvent` with their `SimTime`.
- **Sandbox:** placeholder HP bars, a limb-damage readout, and a stun indicator on the next turn.

## 4. Definition of done

- **EditMode tests:**
  - The damage formula for every zone.
  - The per-hit limb cap: severing needs at least 3 hits.
  - No instant KO from one head hit (D27).
  - The stun threshold and the stun effect on the next turn's constraints.
  - Each interrupt policy option.
  - Priority ordering, including same-step ties (stable order).
  - The D26 behaviour.
- **PlayMode scenario:** a short rapier thrust interrupts a slow mace swing.
- `GddAppendixATests` is still green. `python Tools/check.py` passes, and PlayMode tests pass in the Editor.

## 5. TBDs in this session

- **§9:** which hits interrupt (D15), and heavy swing armour (D16, a flag, off by default).
- **§10/§11:** what a stun (and stagger) does (D17), through `IStatusEffect`.
- **§11:** HP scale, base damage, limb durability and the stun threshold (D18). These are placeholders already in `DamageSettings` and `WeaponStats`.
- **Not in the GDD (D26, D27):** ask me if they are not in `MEMORY.md`.
  - D26 recommended default: one damaging hit per weapon per turn; the weapon stops at the hit point with an impact recoil.
  - D27 recommended default: a single hit cannot take a dummy from full HP to 0; the damage is clamped to leave 1 HP. Later hits can finish a damaged dummy.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Record them under *Open questions for the designer*.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 06 in progress", with the batch list.
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

1. Mark Session 06 complete in `MEMORY.md` (*Current status* and *Completed work log*, with commit hashes) and set the next session.
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Include hitting each zone in the sandbox and watching the HP, limb damage and stun readouts.
3. Name the next session file: `Docs/sessions/session-07-clashes-and-shield.md`.
