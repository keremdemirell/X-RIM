# Session 14: Weapon roster and loadout

**Recommended model:** Sonnet 5.5
**How to start:** first commit the previous session's work if you have not yet. Then open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework. I am the game's designer, not a programmer.

This chat is **Session 14: Weapon roster and loadout** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions. Where it differs from `MEMORY.md` or this prompt on how we work (for example git), `MEMORY.md` and this prompt win, because they are newer.
2. `MEMORY.md`, all of it: status, decisions, placeholders, deviations, known issues, open questions and playtest reports.
3. `Docs/ARCHITECTURE.md`. Earlier sessions kept it up to date with what they built.
4. `Docs/SESSION_PLAN.md`: §4 (decisions, including *Later decisions*), §5 (checkpoints), §6 (rules) and the Session 14 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), §20 (parked and rejected ideas), Appendix A and these sections: §6 (Roster, Spear rigidity, Loadout, Weapon switching), §7 (shield variants: designs TBD), §15 (whole), §16 (unlockables are sidegrades).
6. `Tools/README.md`: the compile-check tool.
7. The code this session touches: `Assets/XRim/Scripts/Rules/Settings/WeaponStats.cs`, `GddStartingValues.cs`, `LoadoutSettings.cs` and `RigiditySettings.cs`, `Rules/Paths/` (`RigidityInkCostModel`), `Rules/Damage/` (`IWeaponTrait`, `IDamageModifier`), `Rules/Match/LoadoutValidator`, `Config/WeaponDefinition.cs` and `LoadoutRulesConfig.cs`, the `Assets/XRim/Data/Weapons/` assets (through code), `Simulation.Unity2D/PlaceholderRagdollBuilder` (held items), `Simulation/` (`HeldItemShape`, guard stance), `App/` (match setup), `Presentation/UI/`, and the tests. Read the existing code before proposing anything, because much of what this session needs already has a seam.

## 2. Checks before starting

1. **Dependencies.** This session depends on: Sessions 12 and 13. Check `MEMORY.md` → *Current status* and *Completed work log*. If a dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** PT5 (after Session 13) must be reported before this session starts. If `MEMORY.md` → *Playtest reports* has no PT5 report, ask me for it (the template is in `Docs/SESSION_PLAN.md` §5) and record it, plus any decisions and changed values it contains, before you start. If I explicitly choose to skip it, record that in `MEMORY.md` and continue.
3. **Notes left for this session.** Search `MEMORY.md` for "Session 14". Earlier sessions left wiring notes, open points and feel notes addressed to this session. List every one in your batch plan, and for each either cover it or tell me why not.
4. **Resume.** If `MEMORY.md` → *Current status* shows Session 14 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with the files on disk and `git status`. Batches are not committed one by one.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
5. **Git.** Run `git status`.
   - I commit each session myself when it is done. If the previous session's work is still uncommitted, tell me and ask whether I want to commit it before you change anything.
   - Never stage or commit anything yourself unless I ask.
6. **Decisions.** This session needs the Session 14 row of *Later decisions*: §6 keep or cut spear rigidity, the sword and spear stats (placeholders), the starting weapons for a new player, whether a shield slot is required (D8), and §15 whether traits appear in this build at all (default: seams only).
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4, with one line on how it serves feel. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
7. **Baseline.** Run `python Tools/check.py` once before changing anything. If it does not print `CHECK PASSED`, stop and show me the output.

## 3. Goal and scope

- **Sword and spear become playable** with placeholder stats (`DesignIsTbd`), held-item shapes and guard poses.
  - `MEMORY.md` → *Known issues*: the spear's tip sits below the floor at the −40° guard. Give long weapons their own guard or length, and propose it.
  - Sword and spear clash and block through their mass and speed with no extra rule code. Prove it with tests.
- **Spear rigidity goes live** behind `RigiditySettings.Enabled`, as a per-weapon rigidity: the rapier has almost no bend penalty, the spear a strong one. The path preview already draws rigidity-invalid segments in red; check it on a phone-sized view.
- **Validation.** Extend `RulesSettings.Validate` to the new weapons, keeping the Decided orderings (rapier faster than mace, rapier base damage below mace).
- **Pre-match loadout screen.**
  - Each player picks 3 of their owned weapons; the shield counts as a weapon (§6, Decided).
  - Both loadouts are revealed at match start (Decided).
  - `RequireShieldSlot` is honoured.
  - For now "owned" means every weapon (ownership arrives in Session 18).
  - In hot-seat, each player picks behind the cover screen. Bots pick a valid loadout.
- **Traits (§15).** `IWeaponTrait` implementations for lifesteal and build-up, behind flags and assigned to no weapon by default.
  - Lifesteal: after 3 successful hits, the dummy recovers part of the damage it dealt. Consecutive or total hits is TBD.
  - Build-up: damage potential grows with the total damage taken, up to a cap. A low-HP rage version is Rejected.
  - Traits are unrelated to currency rewards.
- **Tuning assets.** New stats must reach the assets through the existing menus (`Create Default Tuning Assets` never overwrites tuned assets; `Fill New Tuning Fields`).

## 4. Definition of done

- **EditMode tests:**
  - The loadout rules: 3 slots, the reveal, the optional shield requirement, a valid bot loadout.
  - The rigidity cost and invalid break for a spear S-curve.
  - The rapier tolerating curves.
  - Sword and spear clash outcomes from mass and speed.
  - The trait maths behind their flags.
  - The extended validation.
- A match can be played with any of the 5 weapons, and the long-weapon guard is fixed.
- `python Tools/check.py` passes, and the Unity-side and PlayMode tests pass in the Editor.

## 5. TBDs in this session

- **§6:** sword and spear stats, keeping or cutting spear rigidity (and a general rigidity stat), the starting weapons, and the shield slot.
- **§7:** shield variant designs. Not built; a variant would be a `WeaponKind.Shield` item with its own stats and shape.
- **§15:** every trait detail, including which weapons carry them.
- **Rejected; do not build:** a low-HP rage multiplier, and fighters or weapons that are strictly stronger.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Bring each one with a recommended answer chosen for feel, and record it under *Open questions for the designer* until I answer.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified.
   - Also list any architectural change (new references between assemblies, reshaped interfaces, new seams), because I approve those explicitly.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 14 in progress", with the batch list.
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

1. Mark Session 14 complete in `MEMORY.md` (*Current status* and *Completed work log*) and set the next session. Write "not committed yet: the designer commits it".
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Include rerunning the tuning-asset menus, picking loadouts in hot-seat, and drawing an S-curve with the spear and the rapier.
3. List every file the session changed or added, and propose a commit message (a short imperative subject and a few body lines, no attribution) for me to use.
4. Finish with a short **What this means for you** wrap-up in plain language.
5. Name the next session file: `Docs/sessions/session-15-signature-moves.md`.
