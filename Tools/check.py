#!/usr/bin/env python3
"""X-RIM compile and test check that runs without the Unity Editor.

Usage, from the project root, in Git Bash or PowerShell:

    python Tools/check.py [--pass editor|player|dev|all] [--no-tests] [--filter TEXT] [--verbose]

Every XRim.*.asmdef under Assets/ is compiled with Unity's bundled Roslyn compiler and Unity's own flags
(taken from the .rsp files Unity writes under Library/Bee), in up to three passes: editor, player and
development player. The engine-free assemblies are also proven unable to see UnityEngine.
Outputs go to Temp/XRimCheck/ only. Exit code 0 means CHECK PASSED, 1 means CHECK FAILED.
Tools/README.md explains each pass, the define lists and the limits.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import os
import re
import shutil
import subprocess
import sys
import threading
import time
from dataclasses import dataclass, field
from datetime import datetime
from typing import Dict, Iterable, List, Optional, Set, Tuple

# ----------------------------------------------------------------------------------------------------
# Locations and constants
# ----------------------------------------------------------------------------------------------------

PROJECT_ROOT = os.path.normpath(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))).replace("\\", "/")
OUTPUT_ROOT = PROJECT_ROOT + "/Temp/XRimCheck"
ASSETS_ROOT = PROJECT_ROOT + "/Assets"
BEE_ARTIFACTS_ROOT = PROJECT_ROOT + "/Library/Bee/artifacts"
PACKAGE_ROOTS = (PROJECT_ROOT + "/Library/PackageCache", PROJECT_ROOT + "/Packages")

DEFAULT_UNITY_EDITOR_DIR = "S:/Unity_Editor/Editor"
UNITY_EDITOR_DIR_ENV = "XRIM_UNITY_EDITOR"

XRIM_ASSEMBLY_PREFIX = "XRim."
TEST_DEFINE = "UNITY_INCLUDE_TESTS"
EDITOR_PLATFORM = "Editor"
# The game ships on these platforms. The player passes compile for them through a proxy player.
SHIP_PLATFORMS = ("Android", "iOS")

COMPILE_TIMEOUT_SECONDS = 300
RUNNER_TIMEOUT_SECONDS = 600
RUNNER_SOURCE = PROJECT_ROOT + "/Tools/TestRunner/XRimTestRunner.cs"
SCRIPT_ASSEMBLIES_DIR = PROJECT_ROOT + "/Library/ScriptAssemblies"   # read only: full package DLLs for tests
NUNIT_FILE_NAME = "nunit.framework.dll"
PLAYMODE_REASON = "PlayMode test: run it in the Unity Test Runner"
MAX_PARALLEL_COMPILES = 8
NAME_COLUMN_WIDTH = 30
MAX_RAW_OUTPUT_LINES = 30

# csc reports these when an analyzer or source generator cannot be loaded outside Unity.
ANALYZER_LOAD_CODES = {"CS8032", "CS8033", "CS8034"}
# csc reports this when a type or namespace (here: UnityEngine) cannot be found.
TYPE_NOT_FOUND_CODES = {"CS0246", "CS0234"}

# Defines Unity sets only in the Editor; both player passes remove them. Every define starting with
# UNITY_EDITOR (UNITY_EDITOR, UNITY_EDITOR_64, UNITY_EDITOR_WIN, ...) is removed as well.
EDITOR_ONLY_DEFINES = {
    "UNITY_INCLUDE_TESTS",
    "UNITY_TEAM_LICENSE",
    "UNITY_PRO_LICENSE",
    "ENABLE_EDITOR_GAME_SERVICES",
    "ENABLE_CLOUD_LICENSE",
    "ENABLE_EDITOR_HUB_LICENSE",
    "ENABLE_UNITY_COLLECTIONS_CHECKS",
    "ENABLE_ACCELERATOR_CLIENT_DEBUGGING",
    "ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API",
    "EDITOR_ONLY_NAVMESH_BUILDER_DEPRECATED",
}
EDITOR_DEFINE_PREFIX = "UNITY_EDITOR"
# Defines Unity sets in the Editor and in development players, but not in release players.
DEVELOPMENT_ONLY_DEFINES = {"ENABLE_PROFILER", "DEBUG", "TRACE", "UNITY_ASSERTIONS"}
DEVELOPMENT_BUILD_DEFINE = "DEVELOPMENT_BUILD"
SCRIPTING_BACKEND_DEFINES = {"ENABLE_MONO", "ENABLE_IL2CPP"}
# Defines that name the target platform. A player pass for another platform replaces them.
PLATFORM_IDENTITY_DEFINES = {
    "UNITY_STANDALONE", "UNITY_STANDALONE_WIN", "UNITY_STANDALONE_OSX", "UNITY_STANDALONE_LINUX",
    "PLATFORM_STANDALONE", "PLATFORM_STANDALONE_WIN", "PLATFORM_STANDALONE_OSX", "PLATFORM_STANDALONE_LINUX",
    "UNITY_ANDROID", "PLATFORM_ANDROID", "UNITY_IOS", "UNITY_IPHONE", "PLATFORM_IOS",
    "UNITY_TVOS", "PLATFORM_TVOS", "UNITY_VISIONOS", "PLATFORM_VISIONOS",
    "UNITY_WEBGL", "PLATFORM_WEBGL", "UNITY_WSA", "UNITY_WSA_10_0", "UNITY_SERVER",
}
# The Editor's active build target, read from the identity defines in its .rsp files.
BUILD_TARGET_BY_DEFINE = (
    ("UNITY_ANDROID", "Android"),
    ("UNITY_IOS", "iOS"),
    ("UNITY_STANDALONE_WIN", "WindowsStandalone64"),
    ("UNITY_STANDALONE_OSX", "macOSStandalone"),
    ("UNITY_STANDALONE_LINUX", "LinuxStandalone64"),
    ("UNITY_WEBGL", "WebGL"),
)

ENGINE_PROBE_SOURCE = (
    "// Written by Tools/check.py. This file must NOT compile: its assembly may not see UnityEngine.\n"
    "using UnityEngine;\n"
    "internal sealed class XRimEngineFreeProbe : MonoBehaviour {}\n"
)

DIAGNOSTIC_RE = re.compile(
    r"^(?P<file>.+?)\((?P<line>\d+),(?P<column>\d+)(?:,\d+,\d+)?\): "
    r"(?P<severity>error|warning|info|hidden) (?P<code>[A-Za-z]+\d+): (?P<message>.*)$")
BARE_DIAGNOSTIC_RE = re.compile(
    r"^(?:[^:]+ : )?(?P<severity>error|warning|info|hidden) (?P<code>[A-Za-z]+\d+): (?P<message>.*)$")
GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)
DEFINE_TOKEN_RE = re.compile(r"\s*(\|\||&&|!|\(|\)|[A-Za-z_][A-Za-z0-9_]*)")


class CheckError(Exception):
    """A problem with the environment or the project setup that stops the check."""


# ----------------------------------------------------------------------------------------------------
# Small helpers
# ----------------------------------------------------------------------------------------------------

def norm(path: str) -> str:
    return os.path.normpath(path).replace("\\", "/")


def absolute(path: str) -> str:
    return norm(path if os.path.isabs(path) else os.path.join(PROJECT_ROOT, path))


def project_relative(path: str) -> str:
    normalized = norm(path)
    root = PROJECT_ROOT + "/"
    if normalized.lower().startswith(root.lower()):
        return normalized[len(root):]
    return normalized


def is_under(path: str, folder: str) -> bool:
    return norm(path).lower().startswith(norm(folder).lower().rstrip("/") + "/")


def read_text(path: str) -> str:
    with open(path, encoding="utf-8-sig") as handle:
        return handle.read()


def write_text(path: str, text: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def read_guid(meta_path: str) -> str:
    if not os.path.isfile(meta_path):
        return ""
    match = GUID_RE.search(read_text(meta_path))
    return match.group(1).lower() if match else ""


def unquote(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == '"' and value[-1] == '"':
        return value[1:-1]
    return value


def is_ignored_by_unity(name: str) -> bool:
    """Unity skips hidden files and folders, names ending in '~' and folders named cvs."""
    return name.startswith(".") or name.endswith("~") or name.lower() == "cvs"


def dotted(name: str, width: int = NAME_COLUMN_WIDTH) -> str:
    return (name + " ").ljust(width, ".")


def evaluate_define_expression(expression: str, defines: Set[str]) -> bool:
    """Evaluates a Unity define constraint such as 'UNITY_EDITOR || DEVELOPMENT_BUILD' or '!UNITY_IOS'."""
    tokens: List[str] = []
    position = 0
    text = expression.strip()
    while position < len(text):
        match = DEFINE_TOKEN_RE.match(text, position)
        if not match:
            raise ValueError(f"cannot parse define constraint '{expression}'")
        tokens.append(match.group(1))
        position = match.end()
        while position < len(text) and text[position].isspace():
            position += 1
    index = 0

    def peek() -> Optional[str]:
        return tokens[index] if index < len(tokens) else None

    def take() -> str:
        nonlocal index
        if index >= len(tokens):
            raise ValueError(f"cannot parse define constraint '{expression}'")
        index += 1
        return tokens[index - 1]

    def parse_or() -> bool:
        value = parse_and()
        while peek() == "||":
            take()
            value = parse_and() or value
        return value

    def parse_and() -> bool:
        value = parse_unary()
        while peek() == "&&":
            take()
            value = parse_unary() and value
        return value

    def parse_unary() -> bool:
        token = take()
        if token == "!":
            return not parse_unary()
        if token == "(":
            value = parse_or()
            if take() != ")":
                raise ValueError(f"cannot parse define constraint '{expression}'")
            return value
        if token in ("||", "&&", ")"):
            raise ValueError(f"cannot parse define constraint '{expression}'")
        return token in defines

    result = parse_or()
    if index != len(tokens):
        raise ValueError(f"cannot parse define constraint '{expression}'")
    return result


# ----------------------------------------------------------------------------------------------------
# Unity install, compiler and player proxy
# ----------------------------------------------------------------------------------------------------

@dataclass
class Toolchain:
    unity_dir: str
    dotnet: str
    csc: str
    sdk_version: str


def version_key(text: str) -> Tuple[int, ...]:
    return tuple(int(part) if part.isdigit() else 0 for part in re.split(r"[.\-]", text))


def find_toolchain() -> Toolchain:
    unity_dir = norm(os.environ.get(UNITY_EDITOR_DIR_ENV, DEFAULT_UNITY_EDITOR_DIR))
    if not os.path.isdir(unity_dir + "/Data"):
        raise CheckError(f"Unity Editor not found at {unity_dir}. Set {UNITY_EDITOR_DIR_ENV} to the folder that "
                         f"contains Unity.exe and Data/.")
    sdk_root = unity_dir + "/Data/DotNetSdk"
    dotnet = sdk_root + "/dotnet.exe"
    if not os.path.isfile(dotnet):
        raise CheckError(f"Unity's bundled .NET was not found at {dotnet}.")
    versions = []
    if os.path.isdir(sdk_root + "/sdk"):
        for version in os.listdir(sdk_root + "/sdk"):
            if os.path.isfile(f"{sdk_root}/sdk/{version}/Roslyn/bincore/csc.dll"):
                versions.append(version)
    if not versions:
        raise CheckError(f"Unity's bundled Roslyn compiler was not found under {sdk_root}/sdk/*/Roslyn/bincore/.")
    version = max(versions, key=version_key)
    return Toolchain(unity_dir, dotnet, f"{sdk_root}/sdk/{version}/Roslyn/bincore/csc.dll", version)


@dataclass
class PlayerProxy:
    """The installed player whose engine DLLs and platform defines stand in for the mobile players."""
    label: str
    asmdef_platform: str               # name used in asmdef includePlatforms/excludePlatforms
    plugin_platform: Tuple[str, str]   # (group, name) used in plugin .meta platformData
    plugin_exclude_key: str            # suffix of the 'Exclude <key>' setting of an 'Any Platform' plugin
    release_managed_dir: str
    development_managed_dir: str
    identity_defines: List[str]
    backend_define: str


def _find_android_proxy(android_dir: str) -> Optional[PlayerProxy]:
    managed_dirs = []
    for folder, _, files in os.walk(android_dir + "/Variations"):
        if "UnityEngine.CoreModule.dll" in files:
            managed_dirs.append(norm(folder))

    def is_development(path: str) -> bool:
        return any("development" in part and "nondevelopment" not in part for part in path.lower().split("/"))

    def preference(path: str) -> Tuple[int, str]:
        return (0 if "il2cpp" in path.lower() else 1, path)

    development = sorted((path for path in managed_dirs if is_development(path)), key=preference)
    release = sorted((path for path in managed_dirs if not is_development(path)), key=preference)
    if not development or not release:
        return None
    backend = "ENABLE_IL2CPP" if "il2cpp" in release[0].lower() else "ENABLE_MONO"
    return PlayerProxy(
        label=f"Android player ({'IL2CPP' if backend == 'ENABLE_IL2CPP' else 'Mono'})",
        asmdef_platform="Android", plugin_platform=("Android", "Android"), plugin_exclude_key="Android",
        release_managed_dir=release[0], development_managed_dir=development[0],
        identity_defines=["UNITY_ANDROID", "PLATFORM_ANDROID"], backend_define=backend)


def find_player_proxy(toolchain: Toolchain) -> PlayerProxy:
    engines = toolchain.unity_dir + "/Data/PlaybackEngines"
    entries = {name.lower(): f"{engines}/{name}" for name in os.listdir(engines)} if os.path.isdir(engines) else {}
    if "androidplayer" in entries:
        proxy = _find_android_proxy(entries["androidplayer"])
        if proxy is not None:
            return proxy
    if "windowsstandalonesupport" in entries:
        variations = entries["windowsstandalonesupport"] + "/Variations"
        release = variations + "/win64_player_nondevelopment_mono/Data/Managed"
        development = variations + "/win64_player_development_mono/Data/Managed"
        if os.path.isdir(release) and os.path.isdir(development):
            return PlayerProxy(
                label="Windows 64-bit Mono player, standing in for Android and iOS",
                asmdef_platform="WindowsStandalone64", plugin_platform=("Standalone", "Win64"),
                plugin_exclude_key="Win64", release_managed_dir=release, development_managed_dir=development,
                identity_defines=["UNITY_STANDALONE", "UNITY_STANDALONE_WIN", "PLATFORM_STANDALONE",
                                  "PLATFORM_STANDALONE_WIN"],
                backend_define="ENABLE_MONO")
    raise CheckError(f"No usable player playback engine under {engines} (looked for AndroidPlayer and "
                     f"WindowsStandaloneSupport win64 Mono variations).")


# ----------------------------------------------------------------------------------------------------
# Project scan: asmdefs, asmrefs and scripts under Assets/
# ----------------------------------------------------------------------------------------------------

@dataclass
class Asmdef:
    name: str
    path: str                        # absolute path of the .asmdef file
    folder: str                      # absolute folder that owns the scripts
    guid: str
    root_namespace: str
    raw_references: List[str]
    include_platforms: List[str]
    exclude_platforms: List[str]
    define_constraints: List[str]
    no_engine_references: bool
    override_references: bool
    precompiled_references: List[str]
    allow_unsafe_code: bool
    has_version_defines: bool
    references: List[str] = field(default_factory=list)   # resolved assembly names
    sources: List[str] = field(default_factory=list)      # project-relative .cs paths

    @property
    def is_xrim(self) -> bool:
        return self.name.startswith(XRIM_ASSEMBLY_PREFIX)

    @property
    def is_test(self) -> bool:
        return any(TEST_DEFINE in constraint for constraint in self.define_constraints)

    @property
    def is_editor_only(self) -> bool:
        return self.include_platforms == [EDITOR_PLATFORM]

    @property
    def kind(self) -> str:
        if self.no_engine_references:
            return "engine-free"
        return ("test " if self.is_test else "") + ("editor" if self.is_editor_only else "runtime")


def load_asmdef(path: str) -> Asmdef:
    try:
        data = json.loads(read_text(path))
    except (OSError, ValueError) as error:
        raise CheckError(f"{project_relative(path)}: cannot read asmdef ({error}).")
    name = data.get("name") or os.path.splitext(os.path.basename(path))[0]
    return Asmdef(
        name=name, path=norm(path), folder=norm(os.path.dirname(path)), guid=read_guid(path + ".meta"),
        root_namespace=data.get("rootNamespace") or name,
        raw_references=list(data.get("references", [])),
        include_platforms=list(data.get("includePlatforms", [])),
        exclude_platforms=list(data.get("excludePlatforms", [])),
        define_constraints=[c for c in data.get("defineConstraints", []) if c and c.strip()],
        no_engine_references=bool(data.get("noEngineReferences", False)),
        override_references=bool(data.get("overrideReferences", False)),
        precompiled_references=list(data.get("precompiledReferences", [])),
        allow_unsafe_code=bool(data.get("allowUnsafeCode", False)),
        has_version_defines=bool(data.get("versionDefines", [])))


@dataclass
class PackageAsmdef:
    name: str
    path: str
    include_platforms: List[str]
    exclude_platforms: List[str]
    define_constraints: List[str]


class PackageIndex:
    """Asmdefs and DLLs of the installed packages, loaded on first use (a walk of Library/PackageCache)."""

    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._loaded = False
        self._asmdefs: Dict[str, PackageAsmdef] = {}
        self._guid_to_name: Dict[str, str] = {}
        self._dlls: Dict[str, List[str]] = {}

    def _ensure_loaded(self) -> None:
        with self._lock:
            if self._loaded:
                return
            for root in PACKAGE_ROOTS:
                if not os.path.isdir(root):
                    continue
                for folder, subfolders, files in os.walk(root):
                    subfolders[:] = [name for name in subfolders if not is_ignored_by_unity(name)]
                    for file_name in files:
                        path = norm(os.path.join(folder, file_name))
                        lower = file_name.lower()
                        if lower.endswith(".asmdef"):
                            try:
                                data = json.loads(read_text(path))
                            except (OSError, ValueError):
                                continue
                            name = data.get("name", "")
                            if not name:
                                continue
                            self._asmdefs.setdefault(name, PackageAsmdef(
                                name, path, list(data.get("includePlatforms", [])),
                                list(data.get("excludePlatforms", [])),
                                [c for c in data.get("defineConstraints", []) if c and c.strip()]))
                            guid = read_guid(path + ".meta")
                            if guid:
                                self._guid_to_name.setdefault(guid, name)
                        elif lower.endswith(".dll"):
                            self._dlls.setdefault(lower, []).append(path)
            self._loaded = True

    def asmdef(self, name: str) -> Optional[PackageAsmdef]:
        self._ensure_loaded()
        return self._asmdefs.get(name)

    def name_for_guid(self, guid: str) -> Optional[str]:
        self._ensure_loaded()
        return self._guid_to_name.get(guid.lower())

    def find_dll(self, file_name: str) -> List[str]:
        self._ensure_loaded()
        return list(self._dlls.get(file_name.lower(), []))


class ProjectScan:
    """Every asmdef, asmref and script under Assets/, and which XRim assembly owns each script."""

    def __init__(self, packages: PackageIndex) -> None:
        self.xrim: Dict[str, Asmdef] = {}
        self.all_asmdefs: Dict[str, Asmdef] = {}
        self.unowned_scripts: List[str] = []
        self.problems: List[str] = []
        self._packages = packages
        self._guid_to_name: Dict[str, str] = {}
        self._scan()

    def _scan(self) -> None:
        owners: Dict[str, Tuple[str, str]] = {}   # folder -> ("asmdef", name) | ("asmref", reference)
        scripts: List[str] = []
        for folder, subfolders, files in os.walk(ASSETS_ROOT):
            subfolders[:] = sorted(name for name in subfolders if not is_ignored_by_unity(name))
            folder = norm(folder)
            for file_name in sorted(files):
                if is_ignored_by_unity(file_name):
                    continue
                path = f"{folder}/{file_name}"
                lower = file_name.lower()
                if lower.endswith(".asmdef"):
                    asmdef = load_asmdef(path)
                    if folder in owners:
                        self.problems.append(f"{project_relative(folder)} holds more than one .asmdef/.asmref.")
                    owners[folder] = ("asmdef", asmdef.name)
                    if asmdef.name in self.all_asmdefs:
                        self.problems.append(f"Two asmdefs are named {asmdef.name}.")
                    self.all_asmdefs[asmdef.name] = asmdef
                    if asmdef.guid:
                        self._guid_to_name[asmdef.guid] = asmdef.name
                    if file_name.startswith(XRIM_ASSEMBLY_PREFIX):
                        self.xrim[asmdef.name] = asmdef
                elif lower.endswith(".asmref"):
                    try:
                        reference = json.loads(read_text(path)).get("reference", "")
                    except (OSError, ValueError):
                        reference = ""
                    if folder in owners:
                        self.problems.append(f"{project_relative(folder)} holds more than one .asmdef/.asmref.")
                    owners[folder] = ("asmref", reference)
                elif lower.endswith(".cs"):
                    scripts.append(path)

        for asmdef in self.all_asmdefs.values():
            asmdef.references = [self.resolve_reference(asmdef.name, raw) for raw in asmdef.raw_references]

        for script in scripts:
            owner = self._owner_of(script, owners)
            if owner is None:
                self.unowned_scripts.append(project_relative(script))
            elif owner in self.xrim:
                self.xrim[owner].sources.append(project_relative(script))
        for asmdef in self.xrim.values():
            asmdef.sources.sort()

    def _owner_of(self, script: str, owners: Dict[str, Tuple[str, str]]) -> Optional[str]:
        folder = os.path.dirname(script)
        while True:
            if folder in owners:
                kind, value = owners[folder]
                if kind == "asmdef":
                    return value
                return self.resolve_reference(project_relative(folder) + " (.asmref)", value)
            if norm(folder).lower() == ASSETS_ROOT.lower() or len(folder) <= len(ASSETS_ROOT):
                return None
            folder = norm(os.path.dirname(folder))

    def resolve_reference(self, owner: str, raw: str) -> str:
        if not raw.upper().startswith("GUID:"):
            return raw
        guid = raw[5:].strip().lower()
        name = self._guid_to_name.get(guid) or self._packages.name_for_guid(guid)
        if name is None:
            self.problems.append(f"{owner}: reference {raw} does not match any asmdef.")
            return raw
        return name


def topological_order(asmdefs: Dict[str, Asmdef]) -> List[str]:
    order: List[str] = []
    state: Dict[str, int] = {}   # 1 = visiting, 2 = done

    def visit(name: str, chain: List[str]) -> None:
        if state.get(name) == 2:
            return
        if state.get(name) == 1:
            raise CheckError("Cyclic asmdef references: " + " -> ".join(chain + [name]))
        state[name] = 1
        for reference in sorted(asmdefs[name].references):
            if reference in asmdefs:
                visit(reference, chain + [name])
        state[name] = 2
        order.append(name)

    for name in sorted(asmdefs):
        visit(name, [])
    return order


# ----------------------------------------------------------------------------------------------------
# Unity's response files (Library/Bee/artifacts/<hash>.dag/XRim.*.rsp)
# ----------------------------------------------------------------------------------------------------

@dataclass
class ResponseFile:
    path: str
    defines: List[str]
    references: List[str]        # absolute paths
    analyzers: List[str]         # absolute paths
    additional_files: List[str]  # absolute paths, Unity's own additional file excluded
    options: List[str]           # every other option, verbatim
    mtime: float


def parse_response_file(path: str) -> ResponseFile:
    defines: List[str] = []
    references: List[str] = []
    analyzers: List[str] = []
    additional_files: List[str] = []
    options: List[str] = []
    for raw_line in read_text(path).splitlines():
        line = raw_line.strip()
        if not line:
            continue
        if line.startswith('"') or (line[0] not in "-/" and line.lower().endswith(".cs")):
            continue   # a source file; the tool rebuilds the source list from the asmdef folders
        key, value = None, None
        if line[0] in "-/":
            body = line[1:]
            key, _, value = body.partition(":")
            key = key.lower()
        if key in ("define", "d"):
            defines.extend(part for part in re.split(r"[;,]", value) if part)
        elif key in ("reference", "r"):
            references.append(absolute(unquote(value)))
        elif key in ("analyzer", "a"):
            analyzers.append(absolute(unquote(value)))
        elif key == "additionalfile":
            file_path = absolute(unquote(value))
            if not file_path.endswith("UnityAdditionalFile.txt"):
                additional_files.append(file_path)
        elif key in ("out", "refout", "target", "t", "unsafe", "unsafe+", "unsafe-"):
            continue   # set by the tool
        else:
            options.append(line)
    return ResponseFile(norm(path), defines, references, analyzers, additional_files, options,
                        os.path.getmtime(path))


class BeeSnapshot:
    """The Editor compile Unity ran last: one .rsp per assembly, plus the package reference DLLs."""

    def __init__(self) -> None:
        if not os.path.isdir(BEE_ARTIFACTS_ROOT):
            raise CheckError("Library/Bee/artifacts does not exist. Open the project in Unity once so it compiles "
                             "the scripts and writes its compiler response files.")
        candidates = []
        for entry in os.listdir(BEE_ARTIFACTS_ROOT):
            dag = f"{BEE_ARTIFACTS_ROOT}/{entry}"
            if not entry.endswith(".dag") or not os.path.isdir(dag):
                continue
            rsps = [f"{dag}/{name}" for name in os.listdir(dag)
                    if name.startswith(XRIM_ASSEMBLY_PREFIX) and name.endswith(".rsp") and ".dll." not in name]
            if not rsps:
                continue
            newest = max(rsps, key=os.path.getmtime)
            if "UNITY_EDITOR" in parse_response_file(newest).defines:
                candidates.append((os.path.getmtime(newest), dag))
        if not candidates:
            raise CheckError("No Editor compile of the XRim assemblies was found under Library/Bee/artifacts. "
                             "Open the project in Unity once so it writes XRim.*.rsp.")
        self.mtime, self.dag = max(candidates)
        self._cache: Dict[str, ResponseFile] = {}

    def response_file(self, assembly_name: str) -> Optional[ResponseFile]:
        path = f"{self.dag}/{assembly_name}.rsp"
        if not os.path.isfile(path):
            return None
        if assembly_name not in self._cache:
            self._cache[assembly_name] = parse_response_file(path)
        return self._cache[assembly_name]

    def reference_dll(self, assembly_name: str) -> Optional[str]:
        path = f"{self.dag}/{assembly_name}.ref.dll"
        return path if os.path.isfile(path) else None

    def is_bee_artifact(self, path: str) -> bool:
        return is_under(path, self.dag)

    def active_build_target(self, defines: Iterable[str]) -> str:
        present = set(defines)
        for define, target in BUILD_TARGET_BY_DEFINE:
            if define in present:
                return target
        return "unknown"


# ----------------------------------------------------------------------------------------------------
# Plugin .meta files: which precompiled DLLs a player build may use
# ----------------------------------------------------------------------------------------------------

@dataclass
class PluginPlatformEntry:
    group: str
    name: str
    enabled: bool = False
    settings: Dict[str, str] = field(default_factory=dict)


@dataclass
class PluginMeta:
    define_constraints: List[str]
    entries: List[PluginPlatformEntry]


def parse_plugin_meta(text: str) -> PluginMeta:
    constraints: List[str] = []
    entries: List[PluginPlatformEntry] = []
    reading_constraints = False
    platform_indent = -1
    current: Optional[PluginPlatformEntry] = None
    section = ""
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped:
            continue
        indent = len(line) - len(line.lstrip())
        if stripped.startswith("defineConstraints:"):
            reading_constraints = stripped == "defineConstraints:"
            continue
        if reading_constraints:
            if stripped.startswith("- "):
                constraints.append(unquote(stripped[2:].strip().strip("'")))
                continue
            reading_constraints = False
        if stripped == "platformData:":
            platform_indent = indent
            continue
        if platform_indent < 0:
            continue
        if indent <= platform_indent and not stripped.startswith("- "):
            platform_indent = -1
            current = None
            continue
        if stripped == "- first:":
            current = PluginPlatformEntry("", "")
            entries.append(current)
            section = "first"
            continue
        if current is None:
            continue
        if stripped == "second:":
            section = "second"
            continue
        if section == "first" and ":" in stripped:
            group, _, name = stripped.partition(":")
            current.group, current.name = group.strip().strip("'\""), name.strip()
        elif stripped.startswith("enabled:"):
            current.enabled = stripped.partition(":")[2].strip() == "1"
        elif stripped.startswith("settings:"):
            section = "settings"
        elif section == "settings" and ":" in stripped:
            key, _, value = stripped.partition(":")
            current.settings[key.strip()] = value.strip()
    return PluginMeta(constraints, entries)


def plugin_usable_in_player(dll_path: str, proxy: PlayerProxy, defines: Set[str]) -> Tuple[bool, str]:
    meta_path = dll_path + ".meta"
    if not os.path.isfile(meta_path):
        return True, ""
    meta = parse_plugin_meta(read_text(meta_path))
    for constraint in meta.define_constraints:
        try:
            if not evaluate_define_expression(constraint, defines):
                return False, f'plugin define constraint "{constraint}" not met'
        except ValueError as error:
            return False, str(error)
    if not meta.entries:
        return True, ""
    for entry in meta.entries:
        if entry.group == "" and entry.name == "Any" and entry.enabled:
            excluded = entry.settings.get("Exclude " + proxy.plugin_exclude_key, "0") == "1"
            return (not excluded), ("plugin excludes this player" if excluded else "")
        if entry.group == "Any" and entry.enabled:
            return True, ""
    for entry in meta.entries:
        if (entry.group, entry.name) == proxy.plugin_platform and entry.enabled:
            return True, ""
    return False, "plugin is not enabled for this player (Editor-only)"


# ----------------------------------------------------------------------------------------------------
# Passes
# ----------------------------------------------------------------------------------------------------

@dataclass
class PassSpec:
    key: str
    title: str
    is_player: bool
    development: bool


PASS_SPECS = {
    "editor": PassSpec("editor", "editor", False, False),
    "player": PassSpec("player", "player", True, False),
    "dev": PassSpec("dev", "dev player", True, True),
}
PASS_ORDER = ("editor", "player", "dev")


def platform_inclusion(include: List[str], exclude: List[str], spec: PassSpec,
                       proxy: PlayerProxy) -> Tuple[bool, str]:
    if not spec.is_player:
        if include and EDITOR_PLATFORM not in include:
            return False, "not built for the Editor (includePlatforms)"
        if EDITOR_PLATFORM in exclude:
            return False, "excludes the Editor (excludePlatforms)"
        return True, ""
    if include:
        if include == [EDITOR_PLATFORM]:
            return False, "Editor-only"
        if not set(include) & set(SHIP_PLATFORMS) and proxy.asmdef_platform not in include:
            return False, "not built for Android or iOS (includePlatforms)"
    elif set(SHIP_PLATFORMS) <= set(exclude):
        return False, "excludes Android and iOS (excludePlatforms)"
    return True, ""


def constraint_inclusion(constraints: List[str], defines: Set[str]) -> Tuple[bool, str]:
    for constraint in constraints:
        try:
            if not evaluate_define_expression(constraint, defines):
                return False, f'define constraint "{constraint}" not met'
        except ValueError as error:
            return False, str(error)
    return True, ""


def transform_defines(defines: List[str], spec: PassSpec, proxy: PlayerProxy,
                      active_target: str) -> List[str]:
    if not spec.is_player:
        return list(defines)
    result = []
    for define in defines:
        if define.startswith(EDITOR_DEFINE_PREFIX) or define in EDITOR_ONLY_DEFINES:
            continue
        if not spec.development and define in DEVELOPMENT_ONLY_DEFINES:
            continue
        if active_target != proxy.asmdef_platform and define in PLATFORM_IDENTITY_DEFINES:
            continue
        if define in SCRIPTING_BACKEND_DEFINES and define != proxy.backend_define:
            continue
        result.append(define)
    extra = proxy.identity_defines + [proxy.backend_define]
    if spec.development:
        extra.append(DEVELOPMENT_BUILD_DEFINE)
    for define in extra:
        if define not in result:
            result.append(define)
    return result


# ----------------------------------------------------------------------------------------------------
# Compile units
# ----------------------------------------------------------------------------------------------------

@dataclass
class CompileUnit:
    asmdef: Asmdef
    spec: PassSpec
    output_dir: str
    defines: List[str]
    references: List[str]           # non-XRim references, absolute
    xrim_dependencies: List[str]    # XRim assemblies compiled in this pass
    analyzers: List[str]
    additional_files: List[str]
    options: List[str]
    notes: List[str] = field(default_factory=list)
    dropped: List[str] = field(default_factory=list)
    error: str = ""                 # set when the unit cannot be built at all

    @property
    def output(self) -> str:
        return f"{self.output_dir}/{self.asmdef.name}.dll"

    @property
    def reference_output(self) -> str:
        return f"{self.output_dir}/{self.asmdef.name}.ref.dll"

    def all_references(self) -> List[str]:
        return self.references + [f"{self.output_dir}/{name}.ref.dll" for name in self.xrim_dependencies]


@dataclass
class CompileResult:
    status: str                       # OK | FAILED | SKIPPED
    detail: str = ""
    errors: List[str] = field(default_factory=list)
    warnings: List[str] = field(default_factory=list)
    notes: List[str] = field(default_factory=list)
    raw_output: List[str] = field(default_factory=list)
    seconds: float = 0.0
    produced_output: bool = False     # True when the DLL was written (warnings still fail the check)


class Checker:
    def __init__(self, arguments: argparse.Namespace) -> None:
        self.arguments = arguments
        self.verbose: bool = arguments.verbose
        self.toolchain = find_toolchain()
        self.proxy = find_player_proxy(self.toolchain)
        self.packages = PackageIndex()
        self.scan = ProjectScan(self.packages)
        self.bee = BeeSnapshot()
        self.order = topological_order(self.scan.xrim)
        self.bad_analyzers: Set[str] = set()
        self.bad_analyzer_lock = threading.Lock()
        self.failed = False
        self.environment = dict(os.environ)
        self.environment.update({
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1", "DOTNET_MULTILEVEL_LOOKUP": "0",
            "DOTNET_ROOT": os.path.dirname(self.toolchain.dotnet), "DOTNET_CLI_UI_LANGUAGE": "en-US",
        })
        self.environment.pop("DOTNET_ROOT(x86)", None)
        first_rsp = next((self.bee.response_file(name) for name in self.order if self.bee.response_file(name)), None)
        self.active_target = self.bee.active_build_target(first_rsp.defines if first_rsp else [])

    # -- output --------------------------------------------------------------------------------------

    def print_header(self) -> None:
        version = "unknown"
        version_file = PROJECT_ROOT + "/ProjectSettings/ProjectVersion.txt"
        if os.path.isfile(version_file):
            match = re.search(r"m_EditorVersion:\s*(\S+)", read_text(version_file))
            version = match.group(1) if match else version
        written = datetime.fromtimestamp(self.bee.mtime).strftime("%Y-%m-%d %H:%M")
        print("X-RIM check")
        print(f"  project:      {PROJECT_ROOT} (Unity {version})")
        print(f"  compiler:     {project_relative(self.toolchain.csc)} (.NET SDK {self.toolchain.sdk_version})")
        print(f"  Unity flags:  {project_relative(self.bee.dag)}/XRim.*.rsp, written {written}")
        print(f"  player proxy: {self.proxy.label}")
        print(f"  assemblies:   {len(self.order)} XRim asmdefs under Assets/")
        for problem in self.scan.problems:
            print(f"  PROBLEM: {problem}")
            self.failed = True
        if self.scan.unowned_scripts:
            print(f"  note: {len(self.scan.unowned_scripts)} script(s) under Assets/ belong to no asmdef "
                  f"(Assembly-CSharp) and are not checked, e.g. {self.scan.unowned_scripts[0]}")

    # -- building units ------------------------------------------------------------------------------

    def base_response_file(self, asmdef: Asmdef) -> Tuple[Optional[ResponseFile], Optional[Asmdef]]:
        candidates = [self.scan.xrim[name] for name in self.order
                      if name != asmdef.name and self.bee.response_file(name) is not None]
        same_kind = [candidate for candidate in candidates if candidate.kind == asmdef.kind]
        pool = same_kind or [c for c in candidates if c.no_engine_references == asmdef.no_engine_references]
        pool = pool or candidates
        if not pool:
            return None, None

        def package_reference_count(candidate: Asmdef) -> int:
            return sum(1 for ref in candidate.references if ref not in self.scan.xrim)

        base = min(pool, key=lambda candidate: (package_reference_count(candidate), candidate.name))
        return self.bee.response_file(base.name), base

    def build_unit(self, asmdef: Asmdef, spec: PassSpec, output_dir: str,
                   included: Dict[str, bool]) -> Tuple[Optional[CompileUnit], str]:
        """Returns (unit, "") or (None, reason the assembly is skipped in this pass)."""
        rsp = self.bee.response_file(asmdef.name)
        notes: List[str] = []
        base: Optional[Asmdef] = None
        if rsp is None:
            rsp, base = self.base_response_file(asmdef)
            if rsp is None:
                raise CheckError(f"{asmdef.name}: Unity has not compiled any XRim assembly yet; open Unity once.")
            notes.append(f"Unity has not compiled {asmdef.name} yet; its flags come from {base.name}.rsp "
                         f"(open Unity once to refresh them)")
            if asmdef.has_version_defines:
                notes.append("versionDefines are not applied until Unity compiles this asmdef")
        elif os.path.getmtime(asmdef.path) > rsp.mtime:
            notes.append("the asmdef changed after Unity last compiled it; references follow the asmdef, "
                         "other flags come from the older .rsp")

        defines = transform_defines(rsp.defines, spec, self.proxy, self.active_target)
        define_set = set(defines)
        ok, reason = platform_inclusion(asmdef.include_platforms, asmdef.exclude_platforms, spec, self.proxy)
        if ok:
            ok, reason = constraint_inclusion(asmdef.define_constraints, define_set)
        if not ok:
            return None, reason
        if not asmdef.sources:
            return None, "no scripts"

        unit = CompileUnit(asmdef, spec, output_dir, defines, [], [], [], [], list(rsp.options), notes)

        # References Unity resolved last time, minus its (stale) XRim reference DLLs.
        references = [path for path in rsp.references
                      if not (self.bee.is_bee_artifact(path) and os.path.basename(path).startswith(XRIM_ASSEMBLY_PREFIX))]
        if base is not None:
            references = self.strip_base_specific_references(references, base, asmdef)

        # Explicit references of the asmdef: XRim ones point at this pass's fresh outputs.
        for name in asmdef.references:
            if name in self.scan.xrim:
                if included.get(name):
                    unit.xrim_dependencies.append(name)
                else:
                    unit.notes.append(f"reference to {name} ignored: it is not compiled in this pass")
                continue
            if any(os.path.basename(path).lower() in (f"{name}.ref.dll".lower(), f"{name}.dll".lower())
                   for path in references):
                continue
            bee_dll = self.bee.reference_dll(name)
            if bee_dll is None:
                unit.error = (f"{asmdef.name} references '{name}', but Library/Bee has no compiled reference for it "
                              f"({project_relative(self.bee.dag)}/{name}.ref.dll). Open Unity once so it compiles "
                              f"that package, or fix the reference.")
                return unit, ""
            references.append(bee_dll)
            unit.notes.append(f"added {name}.ref.dll, which the older .rsp did not reference")

        if asmdef.override_references:
            for dll_name in asmdef.precompiled_references:
                if any(os.path.basename(path).lower() == dll_name.lower() for path in references):
                    continue
                found = [path for path in self.packages.find_dll(dll_name)] + [
                    norm(os.path.join(folder, dll_name)) for folder, _, files in os.walk(ASSETS_ROOT)
                    if dll_name in files]
                if not found:
                    unit.error = (f"{asmdef.name} lists precompiled reference '{dll_name}', which was not found "
                                  f"in Assets/ or the installed packages.")
                    return unit, ""
                references.append(found[0])

        if spec.is_player:
            references = self.player_references(unit, references, define_set)

        unit.references = references
        with self.bad_analyzer_lock:
            unit.analyzers = [path for path in rsp.analyzers if path not in self.bad_analyzers]
        additional_file = f"{output_dir}/{asmdef.name}.UnityAdditionalFile.txt"
        write_text(additional_file, PROJECT_ROOT.replace("/", "\\"))
        unit.additional_files = rsp.additional_files + [additional_file]
        return unit, ""

    def strip_base_specific_references(self, references: List[str], base: Asmdef, asmdef: Asmdef) -> List[str]:
        own = {name.lower() for name in asmdef.references}
        base_only = {name.lower() for name in base.references if name not in self.scan.xrim} - own
        result = []
        for path in references:
            file_name = os.path.basename(path).lower()
            stem = file_name[:-len(".ref.dll")] if file_name.endswith(".ref.dll") else file_name[:-len(".dll")]
            if self.bee.is_bee_artifact(path) and stem in base_only:
                continue
            is_plugin = any(is_under(path, root) for root in PACKAGE_ROOTS + (ASSETS_ROOT,))
            if is_plugin and asmdef.override_references and file_name not in {
                    name.lower() for name in asmdef.precompiled_references}:
                continue
            result.append(path)
        return result

    def player_references(self, unit: CompileUnit, references: List[str], defines: Set[str]) -> List[str]:
        managed_engine = self.toolchain.unity_dir + "/Data/Managed/UnityEngine"
        managed = self.toolchain.unity_dir + "/Data/Managed"
        playback = self.toolchain.unity_dir + "/Data/PlaybackEngines"
        player_managed = (self.proxy.development_managed_dir if unit.spec.development
                          else self.proxy.release_managed_dir)
        result = []
        for path in references:
            file_name = os.path.basename(path)
            if file_name.lower().startswith("unityeditor"):
                unit.dropped.append(f"{file_name} (Editor-only)")
                continue
            if is_under(path, managed_engine):
                swapped = f"{player_managed}/{file_name}"
                if os.path.isfile(swapped):
                    result.append(swapped)
                else:
                    unit.dropped.append(f"{file_name} (no player equivalent)")
                continue
            if is_under(path, managed) or is_under(path, playback):
                unit.dropped.append(f"{file_name} (Editor tooling)")
                continue
            if self.bee.is_bee_artifact(path) and file_name.lower().endswith(".ref.dll"):
                package = self.packages.asmdef(file_name[:-len(".ref.dll")])
                if package is not None:
                    ok, reason = platform_inclusion(package.include_platforms, package.exclude_platforms,
                                                    unit.spec, self.proxy)
                    if ok:
                        ok, reason = constraint_inclusion(package.define_constraints, defines)
                    if not ok:
                        unit.dropped.append(f"{file_name} ({reason})")
                        continue
                result.append(path)
                continue
            if any(is_under(path, root) for root in PACKAGE_ROOTS + (ASSETS_ROOT,)):
                ok, reason = plugin_usable_in_player(path, self.proxy, defines)
                if not ok:
                    unit.dropped.append(f"{file_name} ({reason})")
                    continue
            result.append(path)
        return result

    # -- compiling -----------------------------------------------------------------------------------

    def write_response_file(self, unit: CompileUnit, path: str, sources: List[str], output: str,
                            reference_output: Optional[str], analyzers: List[str],
                            additional_files: List[str]) -> None:
        lines = ["-target:library", f'-out:"{output}"']
        if reference_output:
            lines.append(f'-refout:"{reference_output}"')
        lines += [f"-define:{define}" for define in unit.defines]
        lines += [f'-r:"{reference}"' for reference in unit.all_references()]
        lines += [f'-analyzer:"{analyzer}"' for analyzer in analyzers]
        lines += [f'"{source}"' for source in sources]
        if unit.asmdef.allow_unsafe_code:
            lines.append("-unsafe")
        lines += unit.options
        lines += [f'/additionalfile:"{additional}"' for additional in additional_files]
        write_text(path, "\n".join(lines) + "\n")

    def run_compiler(self, response_file: str) -> Tuple[int, List[str]]:
        command = [self.toolchain.dotnet, "exec", self.toolchain.csc, "-nostdlib", "-noconfig", "-shared",
                   "@" + response_file]
        try:
            completed = subprocess.run(command, cwd=PROJECT_ROOT, env=self.environment, capture_output=True,
                                       text=True, encoding="utf-8", errors="replace",
                                       timeout=COMPILE_TIMEOUT_SECONDS)
        except subprocess.TimeoutExpired:
            return -1, [f"the compiler did not finish within {COMPILE_TIMEOUT_SECONDS} s"]
        lines = [line.rstrip() for line in (completed.stdout + "\n" + completed.stderr).splitlines() if line.strip()]
        return completed.returncode, lines

    @staticmethod
    def parse_diagnostics(lines: List[str]) -> List[Tuple[str, str, str]]:
        """Returns (severity, code, formatted text) for every csc diagnostic line."""
        diagnostics = []
        for line in lines:
            match = DIAGNOSTIC_RE.match(line)
            if match:
                text = (f"{project_relative(match.group('file'))}:{match.group('line')}: {match.group('severity')} "
                        f"{match.group('code')}: {match.group('message')}")
                diagnostics.append((match.group("severity"), match.group("code").upper(), text))
                continue
            match = BARE_DIAGNOSTIC_RE.match(line)
            if match:
                text = f"{match.group('severity')} {match.group('code')}: {match.group('message')}"
                diagnostics.append((match.group("severity"), match.group("code").upper(), text))
        return diagnostics

    def compile_unit(self, unit: CompileUnit) -> CompileResult:
        started = time.monotonic()
        if unit.error:
            return CompileResult("FAILED", detail="cannot be compiled", errors=[unit.error], notes=list(unit.notes))
        response_file = f"{unit.output_dir}/{unit.asmdef.name}.rsp"
        analyzers = list(unit.analyzers)
        notes = list(unit.notes)
        for _ in range(2):
            self.write_response_file(unit, response_file, unit.asmdef.sources, unit.output, unit.reference_output,
                                     analyzers, unit.additional_files)
            code, lines = self.run_compiler(response_file)
            diagnostics = self.parse_diagnostics(lines)
            unloadable = self.unloadable_analyzers(diagnostics, analyzers)
            if not unloadable:
                break
            for analyzer in unloadable:
                notes.append(f"dropped analyzer {os.path.basename(analyzer)}: it cannot run outside Unity")
            with self.bad_analyzer_lock:
                self.bad_analyzers.update(unloadable)
            analyzers = [path for path in analyzers if path not in unloadable]
        errors = [text for severity, _, text in diagnostics if severity == "error"]
        warnings = [text for severity, code, text in diagnostics
                    if severity == "warning" and code not in ANALYZER_LOAD_CODES]
        result = CompileResult("OK", errors=errors, warnings=warnings, notes=notes,
                               seconds=time.monotonic() - started,
                               produced_output=code == 0 and not errors and os.path.isfile(unit.reference_output))
        if code != 0 or errors:
            result.status = "FAILED"
            result.detail = f"{len(errors)} error(s)" + (f", {len(warnings)} warning(s)" if warnings else "")
            if not errors:
                result.raw_output = lines[:MAX_RAW_OUTPUT_LINES]
        elif warnings:
            result.status = "FAILED"
            result.detail = f"{len(warnings)} new warning(s); warnings count as failures"
        else:
            result.detail = f"{len(unit.asmdef.sources)} files"
        return result

    @staticmethod
    def unloadable_analyzers(diagnostics: List[Tuple[str, str, str]], analyzers: List[str]) -> Set[str]:
        found: Set[str] = set()
        for _, code, text in diagnostics:
            if code not in ANALYZER_LOAD_CODES:
                continue
            lowered = text.replace("\\", "/").lower()
            for analyzer in analyzers:
                if analyzer.lower() in lowered or os.path.basename(analyzer).lower() in lowered:
                    found.add(analyzer)
        return found

    def run_pass(self, spec: PassSpec) -> Dict[str, Tuple[Optional[CompileUnit], CompileResult]]:
        output_dir = f"{OUTPUT_ROOT}/{spec.key}"
        shutil.rmtree(output_dir, ignore_errors=True)
        os.makedirs(output_dir, exist_ok=True)

        units: Dict[str, CompileUnit] = {}
        results: Dict[str, Tuple[Optional[CompileUnit], CompileResult]] = {}
        included: Dict[str, bool] = {}
        for name in self.order:   # dependencies first, so 'included' is known for every reference
            unit, reason = self.build_unit(self.scan.xrim[name], spec, output_dir, included)
            included[name] = unit is not None
            if unit is None:
                results[name] = (None, CompileResult("SKIPPED", detail=reason))
            else:
                units[name] = unit

        pending = dict(units)
        with concurrent.futures.ThreadPoolExecutor(max_workers=MAX_PARALLEL_COMPILES) as pool:
            running: Dict[concurrent.futures.Future, str] = {}
            while pending or running:
                progressed = True
                while progressed:
                    progressed = False
                    for name in list(pending):
                        dependencies = pending[name].xrim_dependencies
                        failed = [dep for dep in dependencies
                                  if dep in results and not results[dep][1].produced_output]
                        if failed:
                            results[name] = (pending.pop(name), CompileResult(
                                "SKIPPED", detail=f"not compiled: it needs {failed[0]}, which did not compile"))
                            progressed = True
                        elif all(dep in results for dep in dependencies):
                            running[pool.submit(self.compile_unit, pending[name])] = name
                            units[name] = pending.pop(name)
                            progressed = True
                if not running:
                    break
                finished, _ = concurrent.futures.wait(running, return_when=concurrent.futures.FIRST_COMPLETED)
                for future in finished:
                    name = running.pop(future)
                    results[name] = (units[name], future.result())
        for name in pending:
            results[name] = (pending[name], CompileResult("FAILED", detail="dependency cycle"))
        return results

    def print_pass(self, spec: PassSpec, results: Dict[str, Tuple[Optional[CompileUnit], CompileResult]]) -> None:
        if spec.is_player:
            managed = self.proxy.development_managed_dir if spec.development else self.proxy.release_managed_dir
            settings = "UNITY_EDITOR off, DEVELOPMENT_BUILD " + ("on" if spec.development else "off")
            print(f"\n{spec.title} pass ({settings}; engine DLLs from {project_relative(managed)})")
            if self.active_target != self.proxy.asmdef_platform:
                print(f"  note: the Editor's active build target is {self.active_target}; other platform feature "
                      f"defines come from it")
        else:
            print(f"\n{spec.title} pass (Unity's Editor flags as is)")
        compiled = 0
        for name in self.order:
            unit, result = results[name]
            if result.status == "OK":
                compiled += 1
            suffix = f" ({result.seconds:.1f} s)" if self.verbose and result.seconds else ""
            print(f"  {dotted(name)} {result.status:<7} {result.detail}{suffix}")
            if result.status == "FAILED":
                self.failed = True
            for line in result.errors + result.warnings:
                print(f"      {line}")
            for line in result.raw_output:
                print(f"      | {line}")
            for note in result.notes:
                print(f"      note: {note}")
            if self.verbose and unit is not None:
                print(f"      response file: {project_relative(unit.output_dir)}/{name}.rsp")
                for dropped in unit.dropped:
                    print(f"      dropped reference: {dropped}")
        print(f"  {compiled} of {len(self.order)} assemblies compiled")
        if self.verbose and spec.is_player:
            self.print_define_changes(spec)

    def print_define_changes(self, spec: PassSpec) -> None:
        rsp = next((self.bee.response_file(name) for name in self.order if self.bee.response_file(name)), None)
        if rsp is None:
            return
        after = transform_defines(rsp.defines, spec, self.proxy, self.active_target)
        removed = [define for define in rsp.defines if define not in after]
        added = [define for define in after if define not in rsp.defines]
        print(f"  defines removed: {' '.join(removed) or '-'}")
        print(f"  defines added:   {' '.join(added) or '-'}")

    # -- engine-free guard ---------------------------------------------------------------------------

    def run_guard(self, spec: PassSpec, results: Dict[str, Tuple[Optional[CompileUnit], CompileResult]]) -> None:
        engine_free = [name for name in self.order if self.scan.xrim[name].no_engine_references]
        print(f"\nengine-free guard ({len(engine_free)} assemblies, {spec.title} pass reference sets)")
        guard_dir = f"{OUTPUT_ROOT}/guard"
        shutil.rmtree(guard_dir, ignore_errors=True)
        jobs = {}
        with concurrent.futures.ThreadPoolExecutor(max_workers=MAX_PARALLEL_COMPILES) as pool:
            for name in engine_free:
                unit, result = results[name]
                if unit is None or not result.produced_output:
                    continue
                jobs[name] = pool.submit(self.guard_one, unit, f"{guard_dir}/{name}")
        for name in engine_free:
            if name not in jobs:
                print(f"  {dotted(name)} NOT RUN not checked: the assembly did not compile in this pass")
                self.failed = True
                continue
            ok, detail, lines = jobs[name].result()
            print(f"  {dotted(name)} {'OK' if ok else 'FAILED':<7} {detail}")
            for line in lines:
                print(f"      {line}")
            if not ok:
                self.failed = True

    def guard_one(self, unit: CompileUnit, folder: str) -> Tuple[bool, str, List[str]]:
        engine_references = [
            os.path.basename(path) for path in unit.all_references()
            if os.path.basename(path).lower().startswith(("unityengine", "unityeditor"))
            or is_under(path, self.toolchain.unity_dir + "/Data/Managed")]
        if engine_references:
            return False, "compiled with Unity engine references", engine_references
        probe = f"{folder}/XRimEngineFreeProbe.cs"
        write_text(probe, ENGINE_PROBE_SOURCE)
        response_file = f"{folder}/probe.rsp"
        self.write_response_file(unit, response_file, [probe], f"{folder}/Probe.dll", None, [], [])
        code, lines = self.run_compiler(response_file)
        diagnostics = self.parse_diagnostics(lines)
        if code == 0:
            return False, "the UnityEngine probe COMPILED: this assembly can see UnityEngine", []
        rejected = any(severity == "error" and code_ in TYPE_NOT_FOUND_CODES and "UnityEngine" in text
                       for severity, code_, text in diagnostics)
        if not rejected:
            return False, "the probe failed for an unexpected reason", [text for _, _, text in diagnostics][:5]
        return True, "no Unity references; UnityEngine probe rejected", []

    # -- tests ---------------------------------------------------------------------------------------

    def reference_pack(self) -> Tuple[str, str]:
        packs = os.path.dirname(self.toolchain.dotnet) + "/packs/Microsoft.NETCore.App.Ref"
        versions = sorted(os.listdir(packs), key=version_key, reverse=True) if os.path.isdir(packs) else []
        for version in versions:
            major, minor = (version.split(".") + ["0"])[:2]
            ref_dir = f"{packs}/{version}/ref/net{major}.{minor}"
            if os.path.isdir(ref_dir):
                return ref_dir, f"{major}.{minor}"
        raise CheckError(f"No .NET reference pack under {packs}; the test runner cannot be built.")

    def build_runner(self, nunit: str) -> str:
        folder = f"{OUTPUT_ROOT}/runner"
        runner = f"{folder}/XRimTestRunner.dll"
        ref_dir, framework = self.reference_pack()
        stamp = f"{os.path.getmtime(RUNNER_SOURCE)}|{nunit}|{ref_dir}|{os.path.getmtime(nunit)}"
        stamp_file = f"{folder}/build.stamp"
        if os.path.isfile(runner) and os.path.isfile(stamp_file) and read_text(stamp_file) == stamp:
            return runner
        shutil.rmtree(folder, ignore_errors=True)
        lines = ["-target:exe", "-nologo", "-langversion:latest", "-deterministic", "-debug:portable",
                 f'-out:"{runner}"', f'"{RUNNER_SOURCE}"', f'-r:"{nunit}"']
        lines += [f'-r:"{norm(os.path.join(ref_dir, name))}"' for name in sorted(os.listdir(ref_dir))
                  if name.endswith(".dll")]
        response_file = f"{folder}/runner.rsp"
        write_text(response_file, "\n".join(lines) + "\n")
        code, output = self.run_compiler(response_file)
        if code != 0:
            details = "\n".join(text for _, _, text in self.parse_diagnostics(output)) or "\n".join(output)
            raise CheckError(f"The test runner ({project_relative(RUNNER_SOURCE)}) did not compile:\n{details}")
        shutil.copy2(nunit, f"{folder}/{NUNIT_FILE_NAME}")
        write_text(f"{folder}/XRimTestRunner.runtimeconfig.json", json.dumps({"runtimeOptions": {
            "tfm": f"net{framework}", "framework": {"name": "Microsoft.NETCore.App", "version": f"{framework}.0"}}}))
        write_text(stamp_file, stamp)
        return runner

    def run_tests(self, results: Dict[str, Tuple[Optional[CompileUnit], CompileResult]]) -> None:
        test_names = [name for name in self.order if self.scan.xrim[name].is_test]
        modules = [(name[len(XRIM_ASSEMBLY_PREFIX):], self.scan.xrim[name].no_engine_references)
                   for name in self.order if not self.scan.xrim[name].is_test]
        engine_free = ", ".join(module for module, free in modules if free)
        print(f"\ntests (run here: tests whose namespace names an engine-free module: {engine_free})")
        matched_any = False
        for name in test_names:
            asmdef = self.scan.xrim[name]
            unit, result = results[name]
            if unit is None:
                print(f"  {dotted(name)} SKIPPED {result.detail}")
                continue
            if not result.produced_output:
                print(f"  {dotted(name)} NOT RUN the test assembly did not compile")
                self.failed = True
                continue
            nunit = next((path for path in unit.references if os.path.basename(path).lower() == NUNIT_FILE_NAME), None)
            if nunit is None:
                print(f"  {dotted(name)} NOT RUN it does not reference {NUNIT_FILE_NAME}")
                self.failed = True
                continue
            report = self.run_test_assembly(self.build_runner(nunit), unit, asmdef, modules)
            matched_any |= self.print_test_report(name, report)
        if self.arguments.filter and not matched_any:
            print(f"  no test name contains '{self.arguments.filter}'")
            self.failed = True

    def run_test_assembly(self, runner: str, unit: CompileUnit, asmdef: Asmdef,
                          modules: List[Tuple[str, bool]]) -> dict:
        folder = f"{OUTPUT_ROOT}/tests/{asmdef.name}"
        shutil.rmtree(folder, ignore_errors=True)
        os.makedirs(folder, exist_ok=True)
        results_file = f"{folder}/results.json"
        command = [self.toolchain.dotnet, runner, "--assembly", unit.output, "--results", results_file,
                   "--work-directory", folder, "--test-root", asmdef.root_namespace]
        for directory in (unit.output_dir, os.path.dirname(runner), SCRIPT_ASSEMBLIES_DIR,
                          self.toolchain.unity_dir + "/Data/Managed/UnityEngine",
                          self.toolchain.unity_dir + "/Data/Managed"):
            command += ["--probe", directory]
        for module, free in modules:
            command += ["--engine-free-module" if free else "--engine-module", module]
        if self.arguments.filter:
            command += ["--filter", self.arguments.filter]
        if not asmdef.is_editor_only:
            command += ["--list-only", PLAYMODE_REASON]
        try:
            completed = subprocess.run(command, cwd=PROJECT_ROOT, env=self.environment, capture_output=True,
                                       text=True, encoding="utf-8", errors="replace", timeout=RUNNER_TIMEOUT_SECONDS)
        except subprocess.TimeoutExpired:
            return {"Error": f"the tests did not finish within {RUNNER_TIMEOUT_SECONDS} s"}
        if not os.path.isfile(results_file):
            output = (completed.stdout + completed.stderr).strip()
            return {"Error": f"the test runner exited with code {completed.returncode} and wrote no results.\n{output}"}
        return json.loads(read_text(results_file))

    def print_test_report(self, name: str, report: dict) -> bool:
        passed, failed = report.get("Passed", []), report.get("Failed", [])
        skipped, inconclusive = report.get("Skipped", []), report.get("Inconclusive", [])
        needs_unity = report.get("NeedsUnity", [])
        if report.get("Error"):
            print(f"  {dotted(name)} FAILED  the test runner stopped")
            for line in str(report["Error"]).splitlines()[:MAX_RAW_OUTPUT_LINES]:
                print(f"      | {line}")
            self.failed = True
            return True
        total = len(passed) + len(failed) + len(skipped) + len(inconclusive) + len(needs_unity)
        summary = f"{len(passed)} passed, {len(failed)} failed, {len(needs_unity)} need Unity"
        if skipped:
            summary += f", {len(skipped)} skipped"
        if inconclusive:
            summary += f", {len(inconclusive)} inconclusive"
        status = "FAILED" if failed else "OK"
        print(f"  {dotted(name)} {status:<7} {summary} ({total} test{'' if total == 1 else 's'})")
        for entry in failed:
            location = entry.get("Location", "")
            where = ""
            if location:
                file_name, _, line = location.rpartition(":")
                where = f"{project_relative(file_name)}:{line}: "
            message = " ".join(entry.get("Message", "").split()) or "(no message)"
            print(f"      FAILED {entry['Name']}")
            print(f"          {where}{message}")
            if self.verbose and entry.get("StackTrace"):
                for line in entry["StackTrace"].splitlines()[:MAX_RAW_OUTPUT_LINES]:
                    print(f"          | {line.strip()}")
        if failed:
            self.failed = True
        if needs_unity:
            counts: Dict[str, int] = {}
            for entry in needs_unity:
                counts[entry["Module"]] = counts.get(entry["Module"], 0) + 1
            listed = ", ".join(f"{module} {count}" for module, count in counts.items())
            print(f"      needs Unity (run in the Editor): {listed}")
        for entry in skipped + inconclusive:
            print(f"      not run: {entry['Name']} {entry.get('Message', '')}".rstrip())
        if self.verbose:
            for entry in passed:
                print(f"      pass {entry['Name']}")
            for entry in needs_unity:
                print(f"      needs Unity {entry['Name']}: {entry['Reason']}")
        return total > 0

    # -- main ----------------------------------------------------------------------------------------

    def run(self) -> int:
        started = time.monotonic()
        self.print_header()
        passes = PASS_ORDER if self.arguments.pass_name == "all" else (self.arguments.pass_name,)
        guarded = False
        editor_results = None
        for key in passes:
            spec = PASS_SPECS[key]
            results = self.run_pass(spec)
            self.print_pass(spec, results)
            if key == "editor":
                editor_results = results
            if not guarded:
                self.run_guard(spec, results)
                guarded = True
        if self.arguments.no_tests:
            print("\ntests: skipped (--no-tests)")
        elif editor_results is None:
            print("\ntests: skipped (tests run after the editor pass; use --pass editor or all)")
        else:
            self.run_tests(editor_results)
        elapsed = time.monotonic() - started
        print(f"\n{'CHECK FAILED' if self.failed else 'CHECK PASSED'} in {elapsed:.1f} s")
        return 1 if self.failed else 0


def parse_arguments(argv: List[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="python Tools/check.py",
        description="Compile every XRim assembly outside the Unity Editor, guard the engine-free assemblies "
                    "and run the engine-free EditMode tests. See Tools/README.md.")
    parser.add_argument("--pass", dest="pass_name", choices=("editor", "player", "dev", "all"), default="all",
                        help="which compile pass to run (default: all)")
    parser.add_argument("--no-tests", action="store_true", help="compile only; do not run tests")
    parser.add_argument("--filter", metavar="TEXT", default="",
                        help="run only tests whose full name contains TEXT")
    parser.add_argument("--verbose", action="store_true",
                        help="show response files, dropped references, define changes and timings")
    return parser.parse_args(argv)


def main(argv: List[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="replace")
    arguments = parse_arguments(argv)
    try:
        return Checker(arguments).run()
    except CheckError as error:
        print(f"\nERROR: {error}")
        print("\nCHECK FAILED")
        return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
