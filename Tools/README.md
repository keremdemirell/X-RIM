# Tools

## `check.py`: compile and test X-RIM without the Unity Editor

```
python Tools/check.py                       # all three passes, the guard and the tests
python Tools/check.py --pass editor         # one pass: editor | player | dev | all (default all)
python Tools/check.py --no-tests            # compile and guard only
python Tools/check.py --filter Resampler    # run only tests whose full name contains the text
python Tools/check.py --verbose             # response files, dropped references, define changes, every test
```

Run it from the project root in Git Bash or PowerShell, after every batch of changes. It prints `CHECK PASSED` and exits with code 0, or it prints `CHECK FAILED` and exits with code 1.

- **Speed.** A run takes about 6 s. The first run after a reboot takes about 25 s while the compiler server starts.
- **Unity can be open or closed.** The tool only reads `Library/` and `Assets/`, and it writes only to `Temp/XRimCheck/`, which git ignores and Unity deletes when it closes.
- **Unity location.** The tool expects Unity at `S:/Unity_Editor/Editor`. To use another install, set `XRIM_UNITY_EDITOR` to the folder that contains `Unity.exe`.

### What it does

1. **Discovery.** It finds every `XRim.*.asmdef` under `Assets/`.
   - Each assembly's scripts are the `.cs` files in its folder tree, minus subfolders that own another `.asmdef` or `.asmref`. New files are picked up without opening Unity.
   - Assemblies compile in dependency order. XRim references always point at the tool's own fresh outputs, never at Unity's older copies.
2. **Unity's own flags.** Everything else comes from the response files Unity wrote when it last compiled, `Library/Bee/artifacts/<hash>.dag/XRim.*.rsp`: defines, language version, warning settings, BCL and package references, analyzers and source generators.
   - The compiler is Unity's bundled Roslyn, started the way Unity starts it: `dotnet exec csc.dll -shared`.
   - The analyzers and source generators all run outside Unity. If one ever fails to load, the tool drops it and prints a note.
   - A module that Unity has never compiled borrows the `.rsp` of an XRim assembly of the same kind (engine-free, runtime, editor or test), with a note.
   - A package reference that cannot be found fails the check with a message saying what is missing.
3. **Three compile passes.** A pass fails on any error, and on any warning that the `.rsp` does not suppress. The setup compiled with 0 warnings, so every warning is new. A warning does not stop dependent assemblies from compiling; an error does.
4. **Engine-free guard.** It runs for every asmdef with `noEngineReferences: true`.
   - It confirms the compile had no `UnityEngine*` or `UnityEditor*` reference.
   - It compiles the probe `using UnityEngine; class Probe : MonoBehaviour {}` with that assembly's references. The probe must fail with "type or namespace not found".
5. **Tests.** Each test assembly built in the editor pass is loaded into a small runner, `TestRunner/XRimTestRunner.cs`. The runner hosts NUnit's own framework runner (the `nunit.framework.dll` from Unity's `com.unity.ext.nunit`) on Unity's bundled .NET.
   - Because it is real NUnit, `[Test]`, `[TestCase]`, `[TestCaseSource]`, `[SetUp]`/`[TearDown]`, `[OneTimeSetUp]`/`[OneTimeTearDown]`, `[Ignore]`, `[Explicit]`, `Assert.*` and `Assert.Throws` behave as they do in Unity.
   - **Which tests run here.** A test runs when its namespace names an engine-free module. `XRim.Tests.EditMode.Rules.Paths.*` belongs to `XRim.Rules`, which has `noEngineReferences: true`, so it runs. The longest module match wins.
   - **Which tests need the Editor.** Tests of Unity-side modules (Config, Input, Presentation, App, Simulation.Unity2D, ...), `[UnityTest]` methods and all PlayMode tests are listed as "needs Unity (run in the Editor)". They are compiled but not run.
   - **New folders.** A new test folder for an engine-free module is picked up automatically, as long as its namespace follows its folder, as the project convention says.

### The passes

| Pass | Defines | Engine DLLs | Assemblies |
|---|---|---|---|
| `editor` | Unity's `.rsp` defines, as is | `Editor/Data/Managed/UnityEngine` | all |
| `player` | editor defines minus the Editor-only and development-only lists below, plus the player's platform defines | the player variation's `Data/Managed` (non-development) | no Editor-only or test asmdefs; define constraints evaluated (`XRim.DebugTools` is excluded) |
| `dev` | the `player` defines plus `DEVELOPMENT_BUILD`, keeping the development-only list | the development variation's `Data/Managed` | as `player`, so `XRim.DebugTools` is included |

In both player passes:
- **Dropped references.** `UnityEditor*` references, Editor tooling DLLs, package assemblies whose asmdef is Editor-only or whose define constraints fail, and precompiled plugins whose `.meta` does not enable them for the player (for example `Mono.Cecil.dll`).
- **Platform rules for asmdefs.** An asmdef is compiled when Unity would build it for Android or iOS: `includePlatforms` is empty or names Android or iOS, and `excludePlatforms` does not name both.

**Removed in both player passes (Editor-only):**
- every define starting with `UNITY_EDITOR`
- `UNITY_INCLUDE_TESTS`, `UNITY_TEAM_LICENSE`, `UNITY_PRO_LICENSE`
- `ENABLE_EDITOR_GAME_SERVICES`, `ENABLE_CLOUD_LICENSE`, `ENABLE_EDITOR_HUB_LICENSE`
- `ENABLE_UNITY_COLLECTIONS_CHECKS`, `ENABLE_ACCELERATOR_CLIENT_DEBUGGING`, `ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API`, `EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED`

**Removed in `player`, kept in `dev` (development only):** `ENABLE_PROFILER`, `DEBUG`, `TRACE`, `UNITY_ASSERTIONS`.

**Added:**
- `dev` adds `DEVELOPMENT_BUILD`.
- When the proxy player differs from the Editor's active build target, the proxy's platform defines replace the target's:
  - **Windows proxy:** `UNITY_STANDALONE`, `UNITY_STANDALONE_WIN`, `PLATFORM_STANDALONE`, `PLATFORM_STANDALONE_WIN`.
  - **Android proxy:** `UNITY_ANDROID` and `PLATFORM_ANDROID`, plus `ENABLE_IL2CPP` in place of `ENABLE_MONO` for an IL2CPP variation.

**How these lists were derived.** They come from comparing Unity's Editor `.rsp` defines with Unity's documented player defines (`UNITY_EDITOR*` only in the Editor, `DEVELOPMENT_BUILD` only in development players) and with the order in which Unity's native define table emits them. There, `ENABLE_PROFILER`, `DEBUG`/`TRACE` and `UNITY_ASSERTIONS` sit in the development-or-Editor group, and the license, services and collections-check defines are Editor-only. No player build exists yet to confirm this against a real player `.rsp`; Session 10 will produce one.

### Limits

- **Windows is the stand-in for mobile players.** Only the Windows standalone playback engine is installed, so the player passes compile against the Windows 64-bit Mono player DLLs.
  - When an Android playback engine is installed (`Editor/Data/PlaybackEngines/AndroidPlayer`), the tool uses it automatically. That path is untested until then.
  - Code inside `#if UNITY_ANDROID` or `#if UNITY_IOS` is not compiled while Windows is the proxy.
  - iOS cannot be checked from this Windows machine.
- **Package references are Editor builds.** Package assemblies (for example `UnityEngine.UI.ref.dll` and `Unity.InputSystem.ref.dll`) exist only as reference DLLs built for the Editor. The player passes compile against those Editor builds, so a package API that exists only in the Editor is not caught.
- **The `.rsp` files are a snapshot.** Defines, language settings and package references are those of Unity's last compile. The tool rebuilds source lists and XRim references itself, adds package references that an asmdef gained since then, and notes when an asmdef is newer than its `.rsp`.
  - It does not notice a package reference that was *removed* from an asmdef, or `versionDefines` of a module Unity has not compiled yet.
  - Open Unity once to refresh the snapshot.
- **Some tests still need the Editor.** PlayMode tests, `[UnityTest]` methods and tests of Unity-side modules need the Unity runtime. Run them in the Unity Test Runner.
- **Assembly-CSharp is not checked.** Scripts that belong to no asmdef are not compiled; the tool counts them in a note.

### Files

- `check.py`: the tool. It needs Python 3.8 or newer and only the standard library.
- `TestRunner/XRimTestRunner.cs`: the test runner. `check.py` builds it into `Temp/XRimCheck/runner/`, and rebuilds it only when this file or the NUnit DLL changes.
- `Temp/XRimCheck/<pass>/`: compiled DLLs, the generated `.rsp` files (useful with `--verbose`), and `guard/` and `tests/` outputs.
