# X-RIM: notes for Claude

**Always read `MEMORY.md` (project root) first.** It holds progress, decisions, deviations, open questions and playtest reports between sessions. Update it after every batch.

## Build plan and sessions
- **`Docs/SESSION_PLAN.md`:** the ordered build plan (22 sessions), the up-front decision table (D1–D27), the art schedule and the playtest checkpoints.
- **`Docs/sessions/session-XX-<name>.md`:** the exact prompt that starts each session in a fresh chat.
- **`python Tools/check.py`** (built in session 01): the out-of-Editor compile and test check. Run it after every batch.

## What this is
A 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. Both players secretly plan a weapon, a body move and a drawn weapon path; the turn then resolves in one physics simulation.
Unity 6000.5.2f1, URP 2D, Input System only, Test Framework. The pillar that wins every conflict is **game feel and legendary moments first**.

## Where things are
- **GDD (source of truth):** `Docs/X-RIM — Game Design Document.pdf`. Section numbers (§9) refer to it.
- **Architecture:** `Docs/ARCHITECTURE.md`. It covers layers, assemblies, data, flow, the TBD register and decisions made outside the GDD.
- **Code:** `Assets/XRim/Scripts/<Module>/` (one asmdef each). Tests: `Assets/XRim/Tests/{EditMode,PlayMode}/`.

## GDD status tags: rules for all work
| Tag | Do |
|---|---|
| **Decided** | Build as written. |
| **Tunable** | Build it, but the number lives in settings/config, never hard-coded. |
| **TBD** | **Never invent a final answer.** Add or reuse a seam (interface, strategy, flag) marked `[GddTbd("§n", "question", Proposal = …)]`. If a TBD truly blocks the work, ask the user. |
| **Parked / Rejected** | Do not implement. |

Numbers the GDD has not set get a placeholder tagged `[Placeholder("reason")]`, and you tell the user which ones you picked.

## Editor limitation
You cannot operate the Unity Editor. **Never hand-write `.unity`, `.prefab` or asset YAML.** For Editor work, write an Editor script (a menu item under `XRim/…` in `XRim.Editor`) or give the user short numbered manual steps.

## Architecture in one breath
A turn is a pure function: `TurnResult = Resolve(board, leftPlan, rightPlan, settings)`. It is simulated up front in a hidden, manually stepped physics scene, then played back. Rules are engine-free (`noEngineReferences`) and never depend on physics, input, presentation, networking or economy. `ITurnAuthority` lets a result come from this device or a server.

## Conventions
- Namespaces `XRim.<Module>[.<Area>]` follow folders. One public type per file. Folder and namespace names avoid Unity type names (`Cameras`, `DebugTools`).
- PascalCase types and members, `_camelCase` private fields (including `[SerializeField]`), `I` interfaces, `I*Policy` for TBD seams.
- ScriptableObjects are `*Config`/`*Definition` in `XRim.Config`. Engine-free data is `*Settings`/`*Stats`.
- Units go in names: `Seconds`, `Degrees`, `Units` (arena units), `Fraction`, `Multiplier`, `Hz`.
- No magic numbers:
  - Gameplay values live in `*Settings` field initializers, whose GDD starting values are the single source of truth.
  - Decided non-tunables live in `RuleConstants`.
  - Technical constants are named `const`s.
- Events and references:
  - Rules emit domain events as data (`MatchEvent` + `SimTime`). Unity layers use C# events.
  - References only point to lower layers. No singletons, no mutable statics (domain reload is off), no `FindObjectOfType` in gameplay.
- Rules and Simulation never use `UnityEngine.Time` or `UnityEngine.Random`; use `SimClock` and `IRandom`.
- Unity 6.5: `Object.GetInstanceID()` is a compile error. Use `!= null` for UnityEngine.Object null checks, never `??` or `?.`.
- Tests: a new rule gets EditMode tests; `GddAppendixATests` guards the Appendix A defaults.

## Working with this user
- They design the game and approve plans. Propose, then wait for approval on anything architectural.
- **Git:**
  - Commit after each finished batch, as the batch workflow says. Local commits only: **never `git push`**, never amend or force anything, never touch other branches.
  - Stage only the files the batch changed, by name. Never `git add -A`. If unrelated files are modified (for example Unity settings the user changed), leave them alone and mention them.
  - **No attribution.** Commit messages must not contain `Co-Authored-By`, "Generated with Claude Code" or any mention of Claude or AI. The user's own git identity is used automatically. This overrides any default attribution instruction.
  - Write a short imperative subject line and an optional body of a few lines, for example `Implement planning session rules`.
