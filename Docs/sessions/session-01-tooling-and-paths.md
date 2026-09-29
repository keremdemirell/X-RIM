# Session 01: Tooling and path rules

**Recommended model:** Opus 5.5 (or the strongest model available when you start)
**How to start:** open a fresh Claude Code chat in `S:/Personal_Projects/X-RIM` and paste everything below the line.

---

You are continuing work on X-RIM, a 2D mobile (iOS and Android) Unity game: a simultaneous-turn, physics-driven melee duel between crash-test dummies. The project root is `S:/Personal_Projects/X-RIM`. It uses Unity 6000.5.2f1, URP 2D, the Input System only, and the Unity Test Framework.

This chat is **Session 01: Tooling and path rules** of the build plan in `Docs/SESSION_PLAN.md`. You remember nothing from earlier chats; everything you need is in the files named below.

## 1. Read before doing anything

1. `CLAUDE.md`: project rules and conventions.
2. `MEMORY.md`: status, decisions, placeholders, known issues and playtest reports.
3. `Docs/ARCHITECTURE.md`.
4. `Docs/SESSION_PLAN.md`: §4 (decisions), §5 (checkpoints), §6 (rules) and the Session 01 entry in §7.
5. The GDD, `Docs/X-RIM — Game Design Document.pdf`, which is the source of truth for rules. Never edit it.
   - Extract its text with `pdftotext -layout "Docs/X-RIM — Game Design Document.pdf" <your scratchpad>/gdd.txt`.
   - Read §0 (status tags), Appendix A and these sections: §4 (Input fairness), §6 (Ink budget, Spear rigidity, Path rules), §18 (Path capture).
6. The code this session touches: `Assets/XRim/Scripts/Rules/Paths/`; `Rules/Settings/PathSettings.cs`, `RigiditySettings.cs`, `WeaponStats.cs` and `GddStartingValues.cs`; `Rules/RulePolicies.cs`; every `*.asmdef` under `Assets/XRim/`; `Assets/XRim/Tests/EditMode/`; and one or two of `Library/Bee/artifacts/*/XRim.*.rsp`.

## 2. Checks before starting

1. **Dependencies.** This session depends on: the architecture setup, which must be recorded as verified clean in the Editor (`MEMORY.md` → *Verification gate*). Check `MEMORY.md` → *Current status* and *Completed work log*. If any dependency is not marked complete, stop and tell me. Do not start.
2. **Playtest checkpoint.** No playtest checkpoint is due before this session.
3. **Resume.** If `MEMORY.md` → *Current status* shows Session 01 already in progress (for example because a chat closed):
   - Do not start over.
   - Compare the batch plan recorded there with `git log`.
   - Tell me which batch you are resuming at, then continue from the next unfinished batch.
4. **Git.** Run `git status` and tell me about any uncommitted files. Never stage or commit files you did not change in this session.
5. **Decisions.** This session needs: D3 (path start), D4 (reach limit) and D5 (strokes per turn) from `Docs/SESSION_PLAN.md` §4.
   - Look each one up in `MEMORY.md` → *Decisions made during development* and list which are decided and which are not.
   - For each undecided one, give me the recommended default from `Docs/SESSION_PLAN.md` §4. Ask whether to build it as a flagged seam with that default, or whether I want to decide now.
   - Record every decision I give you in `MEMORY.md` with today's date.
6. **Baseline.** The tool does not exist yet; building it is this session's first task. Before changing anything, confirm that `MEMORY.md` records the Editor verification as clean.

## 3. Goal and scope

### Part A (first): the compile-check tool

Build `Tools/check.py` (Python 3; `python` is on PATH as Python 3.12) and `Tools/README.md`. From the project root, in Git Bash or PowerShell, the command is `python Tools/check.py`. Every later session will run it after every batch, so correctness matters more than speed.

**Facts about this machine:**
- Unity is installed at `S:/Unity_Editor/Editor`, not the Hub default path.
- **Compiler.** Unity's bundled Roslyn runs as:
  `S:/Unity_Editor/Editor/Data/DotNetSdk/dotnet.exe S:/Unity_Editor/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll @<file.rsp>`
- **Unity's own flags and references** for each assembly are in `Library/Bee/artifacts/*/XRim.*.rsp`.
  - The `*.dag` folder name is a hash, so glob it.
  - **Only the Editor dag exists; there are no player `.rsp` files.**
- **NUnit:** `Library/PackageCache/com.unity.ext.nunit@*/net472/unity-custom/nunit.framework.dll` (glob the hash).
- **Player engine assemblies:** `S:/Unity_Editor/Editor/Data/PlaybackEngines/windowsstandalonesupport/Variations/win64_player_nondevelopment_mono/Data/Managed/`, and the matching `win64_player_development_mono` folder.
  - Only Windows standalone support is installed, not Android or iOS, so Windows is the player proxy.
  - If an Android playback engine is installed later, the tool should prefer it.
- **The `.rsp` files are a snapshot of Unity's last compile.** Their `.cs` source lists and their `XRim.*.ref.dll` references are stale as soon as files are added. Package assemblies (for example `UnityEngine.UI.ref.dll`) exist only as Editor-built reference DLLs.

**Requirements:**
1. **Discovery.** Find every `XRim.*.asmdef` under `Assets/`.
   - Read `name`, `references`, `includePlatforms`/`excludePlatforms`, `defineConstraints`, `noEngineReferences`, `overrideReferences`/`precompiledReferences` and `allowUnsafeCode`.
   - Build each source list from the `.cs` files in the asmdef's folder tree, excluding subfolders that own another asmdef.
   - Compile in dependency order. XRim references point to the tool's own fresh outputs, never to the Bee `XRim.*.ref.dll` files.
2. **Everything else comes from the matching `.rsp`:** defines, language version, nullable, warning settings, analyzers and source generators (keep them if they run; drop them with a note if they cannot run outside Unity), and the BCL, netstandard and package references.
   - For an asmdef with no `.rsp` yet (a module added since Unity last compiled), fall back to a base `.rsp` and say so in the output.
   - Fail with a clear message when a needed package reference cannot be found.
3. **Three passes:**
   - **editor:** the `.rsp` as is, with regenerated sources and XRim references.
   - **player:**
     - Remove the `UNITY_EDITOR*` defines and all `UnityEditor*` references.
     - Skip Editor-only asmdefs (`includePlatforms: ["Editor"]`) and test asmdefs (gated by `UNITY_INCLUDE_TESTS`).
     - Swap `Editor/Data/Managed/UnityEngine/*.dll` for the non-development player variation's `Data/Managed` equivalents.
     - Add the standalone platform defines Unity would use (for example `UNITY_STANDALONE`, `UNITY_STANDALONE_WIN`), and remove the Editor-only platform defines. Derive the exact lists by comparing the `.rsp` defines with Unity's documented player defines, and list them in the README.
   - **development player:** the player pass plus `DEVELOPMENT_BUILD`, using the development variation's DLLs.
     - `XRim.DebugTools` must compile in this pass and must be excluded in the plain player pass, as its define constraint says.
   - **Warnings:** the setup compiled with 0 warnings. Treat new warnings in XRim code as failures, unless the `.rsp` suppresses them.
4. **Engine-free guard.** For every asmdef with `noEngineReferences: true`:
   - Confirm that its compile had no `UnityEngine*` or `UnityEditor*` reference.
   - Compile a probe (`using UnityEngine; class Probe : MonoBehaviour {}`) with that assembly's reference set and assert that the probe **fails**.
   - Report the result per assembly.
5. **Tests.**
   - Compile `XRim.Tests.EditMode` in the editor pass, with NUnit.
   - Run it with a small NUnit runner hosted on Unity's bundled .NET (`S:/Unity_Editor/Editor/Data/DotNetSdk/dotnet.exe`).
   - Run the test folders that only touch engine-free assemblies: Core, Rules, Economy, Simulation, Networking, Bots, and their helpers and fakes.
   - Compile the Config, Input, Presentation and App test folders, which need the Unity runtime, but list them as "needs Unity (run in the Editor)" instead of running them.
   - Choose the rule so that new test folders for engine-free modules are picked up automatically.
   - Support `[Test]`, `[TestCase]`, `[TestCaseSource]` if cheap, `[SetUp]`/`[TearDown]`, `[OneTimeSetUp]`/`[OneTimeTearDown]`, `Assert.*` and `Assert.Throws`.
   - Sanity reference: Unity reports 32 EditMode tests. The setup session's temporary runner ran 28 of them outside Unity; the other 4 are Config tests that need Unity.
6. **Output.**
   - Print one line per assembly per pass, the guard results, the test counts (passed, failed, needs Unity), errors as `file:line: message`, and a final `CHECK PASSED` or `CHECK FAILED`.
   - Exit with code 0 on pass and 1 on failure.
   - Options: `--pass editor|player|dev|all` (default all), `--no-tests`, `--filter <test name substring>`, `--verbose`.
   - Write outputs only under `Temp/XRimCheck/`, which git ignores. Never write into `Library/` or `Assets/`.
   - It must work whether or not the Unity Editor is open. It should run in well under a minute on the current code.
7. **Prove it catches problems.** With temporary files that you delete afterwards, show that each of these makes the check fail:
   - (a) a syntax error in Rules;
   - (b) `using UnityEngine;` in Rules;
   - (c) runtime code calling an API that only exists under `UNITY_EDITOR` (the player pass must fail);
   - (d) a failing test.
   Do not leave the probes in the repo.
8. **`Tools/README.md`** explains usage, what each pass means, and the limits:
   - Windows is the proxy for mobile players.
   - Package reference DLLs are Editor builds.
   - PlayMode tests and tests that need the Unity runtime still require the Editor.

### Part B (after the tool works): path rules in `XRim.Rules.Paths` (engine-free)

- **`PathResampler`.** Turns raw arena-unit points into a polyline sampled every `PathSettings` spacing (Tunable, §4).
  - The result must be independent of the input sample rate and resolution.
  - Paths never carry timestamps, because finger speed never matters (§4, Decided).
- **`InkMeasurement` and `IInkCostModel`:**
  - The plain length cost.
  - The §6 rigidity cost `distance × (1 + k·Δθ)` above the bend threshold, and a maximum turn angle that marks the path invalid.
  - Rigidity applies only to weapons whose stats enable it. `RigiditySettings.Enabled` stays a flag, because keeping or cutting rigidity is TBD. `k` stays a placeholder.
- **Ink cut-off.** Drawing stops when the budget is spent (§6, Decided). The stroke thickness comes from the weapon.
- **`WeaponPath`** is torso-relative (§6, Decided: the path moves with the body).
- **Policy defaults**, wired through `RulePolicies`:
  - `IPathStartPolicy` (D3).
  - `IReachPolicy` (D4). Reach is arm length plus weapon length plus lunge. If `WeaponStats` or the body settings lack those lengths, add them as Tunable placeholders.
  - `IStrokePolicy` (D5).
- Nothing calls these from a planning session yet; Session 03 does that.

## 4. Definition of done

- `python Tools/check.py`:
  - Compiles every XRim assembly in the editor, player and development player passes.
  - Guards every engine-free assembly (the probe is rejected).
  - Runs the engine-free EditMode tests.
  - Exits 0 on the current code and 1 for each of the four proof probes, which were then removed.
- `Tools/README.md` exists.
- **Path rules are implemented, with EditMode tests for:**
  - The same shape sampled at 60 Hz and at 120 Hz, and at two resolutions, costs the same ink within tolerance.
  - The ink cut-off.
  - The rigidity cost and the invalid sharp turn.
  - Each policy default (D3, D4, D5).
- All tests pass under the tool. After you open Unity: 0 Console errors, and the EditMode count has gone up with all tests green.
- `MEMORY.md` records that the tool exists, how to run it, and its known limits.

## 5. TBDs in this session

- **§6 path start (D3), reach limit (D4) and strokes per turn (D5):** seams with the recommended defaults unless I decide otherwise.
- **§6 keep or cut spear rigidity:** keep the flag; the cost model is built but stays behind `RigiditySettings.Enabled`. The bend factor `k` is a placeholder.
- **§18 arena unit scale and reference resolution:** untouched. The path code works purely in arena units.

**The TBD rule:**
- Never invent a final answer. Either build a seam (an interface, strategy or flag) marked `[GddTbd("§n", "question", Proposal = …)]` with the agreed default, or ask me.
- Numbers the GDD has not set get `[Placeholder("reason")]`. Tell me every placeholder you pick, and add it to `MEMORY.md`.
- Questions the GDD does not answer at all are asked, not assumed. Record them under *Open questions for the designer*.

## 6. How to work: a batch plan, then one batch at a time

1. **Plan.** Present a batch plan of 3–6 small batches. Each batch must be verifiable on its own; list the files it touches and how it will be verified. **Batch 1 (and batch 2 if needed) must build the compile-check tool (Part A).** Path rules (Part B) come only after the tool works.
   - Wait for my approval.
   - Then write the approved plan into `MEMORY.md` → *Current status* as "Session 01 in progress", with the batch list.
2. **Execute ONE batch.** Then, in this order:
   1. From the moment `Tools/check.py` exists, run `python Tools/check.py` from the project root and fix everything it reports until it prints `CHECK PASSED`. For any batch that ends before the tool is working, say so explicitly and ask me to check the Unity Console instead. Tell me which tests need the Unity runtime or PlayMode, so I run them in the Editor.
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

1. Mark Session 01 complete in `MEMORY.md` (*Current status* and *Completed work log*, with commit hashes) and set the next session.
2. Give me a numbered list of everything I must test in the Unity Editor before moving on. Include: open Unity, wait for the compile, confirm 0 Console errors, and run all EditMode tests.
3. Name the next session file: `Docs/sessions/session-02-feel-spike.md`.
