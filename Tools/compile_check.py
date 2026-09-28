#!/usr/bin/env python3
"""Compile Assembly-CSharp (+ Editor) outside the Unity Editor to catch C# errors fast.

Reuses the compiler response files Unity last generated under Library/Bee, swaps in the
current set of .cs files, and compiles into a temp folder with Unity's bundled Roslyn.
It never touches the Editor, Library/ScriptAssemblies or any asset, so parallel agents can
run it while the Editor stays idle. Unity remains the source of truth: new asmdefs or
package changes are only picked up after Unity regenerates the .rsp files.

Usage: python3 Tools/compile_check.py [--warnings] [--filter SUBSTRING]
Exit code 0 = no errors.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT = os.path.join(ROOT, "Starter")
UNITY = "/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Resources/Scripting"
DOTNET = os.path.join(UNITY, "NetCoreRuntime", "dotnet")
CSC = os.path.join(UNITY, "DotNetSdkRoslyn", "csc.dll")

SOURCE_RE = re.compile(r'^"?(Assets/.+\.cs)"?$')


def latest_rsp(name):
    best = None
    artifacts = os.path.join(PROJECT, "Library", "Bee", "artifacts")
    for dag in os.listdir(artifacts):
        path = os.path.join(artifacts, dag, name + ".rsp")
        if os.path.exists(path) and (best is None or os.path.getmtime(path) > os.path.getmtime(best)):
            best = path
    if best is None:
        sys.exit(f"No {name}.rsp found — open the project in Unity once.")
    return best


OWN_ASMDEF_ROOT = "Assets/Scripts/BilliardRogue/"


def asmdef_dirs():
    dirs = []
    for base, _, files in os.walk(os.path.join(PROJECT, "Assets")):
        if any(f.endswith(".asmdef") or f.endswith(".asmref") for f in files):
            dirs.append(os.path.relpath(base, PROJECT) + "/")
    return dirs


def own_asmdefs():
    """Our asmdefs (under Assets/Scripts/BilliardRogue), topologically sorted by references."""
    found = {}
    for base, _, files in os.walk(os.path.join(PROJECT, OWN_ASMDEF_ROOT)):
        for f in files:
            if f.endswith(".asmdef"):
                with open(os.path.join(base, f), encoding="utf-8") as fh:
                    data = json.load(fh)
                rel_dir = os.path.relpath(base, PROJECT) + "/"
                found[data["name"]] = {
                    "dir": rel_dir,
                    "refs": [r for r in data.get("references", []) if not r.startswith("GUID:")],
                    "editor": data.get("includePlatforms") == ["Editor"],
                }
    ordered, seen = [], set()

    def visit(name):
        if name in seen or name not in found:
            return
        seen.add(name)
        for ref in found[name]["refs"]:
            visit(ref)
        ordered.append(name)

    for name in sorted(found):
        visit(name)
    return [(n, found[n]) for n in ordered]


def sources_in(rel_dir, excluded_dirs):
    out = []
    for base, _, files in os.walk(os.path.join(PROJECT, rel_dir)):
        rel_base = os.path.relpath(base, PROJECT) + "/"
        if any(rel_base.startswith(d) and d != rel_dir for d in excluded_dirs):
            continue
        out.extend(rel_base + f for f in files if f.endswith(".cs"))
    return out


def scan_sources():
    owned = asmdef_dirs()
    runtime, editor = set(), set()
    for base, _, files in os.walk(os.path.join(PROJECT, "Assets")):
        rel_base = os.path.relpath(base, PROJECT) + "/"
        if any(rel_base.startswith(d) for d in owned):
            continue
        for f in files:
            if not f.endswith(".cs"):
                continue
            rel = rel_base + f
            parts = rel.split("/")
            if "Editor" in parts[:-1]:
                editor.add(rel)
            elif rel.startswith("Assets/Plugins/") or rel.startswith("Assets/Standard Assets/"):
                continue  # firstpass assembly
            else:
                runtime.add(rel)
    return runtime, editor


def split_rsp(path):
    options, sources = [], []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.rstrip("\n")
            if not line:
                continue
            m = SOURCE_RE.match(line)
            if m:
                sources.append(m.group(1))
            elif line.startswith("-out:") or line.startswith("-refout:") or line.startswith("/out:"):
                continue
            else:
                options.append(line)
    return options, sources


def ref_basename(opt):
    return os.path.basename(opt[len("-r:"):].strip('"')) if opt.startswith("-r:") else None


def write_rsp(options, sources, out_dll, tmp, name, replace_ref=None, extra_refs=(), drop_refs=frozenset()):
    """drop_refs: reference file names (e.g. Foo.dll, Foo.ref.dll) to leave out — Unity's stale copies of the
    assemblies we rebuild into tmp, which would otherwise duplicate every type (CS0436) or shadow the fresh build."""
    rsp = os.path.join(tmp, name + ".rsp")
    with open(rsp, "w", encoding="utf-8") as fh:
        for opt in options:
            if ref_basename(opt) in drop_refs:
                continue
            if replace_ref and opt.startswith("-r:") and replace_ref[0] in opt:
                opt = f'-r:"{replace_ref[1]}"'
            fh.write(opt + "\n")
        for ref in extra_refs:
            fh.write(f'-r:"{ref}"\n')
        fh.write(f'-out:"{out_dll}"\n')
        for src in sorted(sources):
            fh.write(f'"{src}"\n')
    return rsp


def run(rsp):
    proc = subprocess.run([DOTNET, "exec", CSC, "-nologo", "@" + rsp], cwd=PROJECT,
                          capture_output=True, text=True)
    return proc.returncode, proc.stdout + proc.stderr


def report(label, output, show_warnings, flt):
    errors, warnings = [], []
    for line in output.splitlines():
        if flt and flt not in line:
            continue
        if ": error " in line:
            errors.append(line)
        elif ": warning " in line and show_warnings and "Assets/Scripts/" in line:
            warnings.append(line)
    for line in sorted(set(errors)):
        print(f"[{label}] {line}")
    for line in sorted(set(warnings)):
        print(f"[{label}] {line}")
    return len(set(errors))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--warnings", action="store_true", help="also list warnings in Assets/Scripts")
    parser.add_argument("--filter", default="", help="only print diagnostics containing this text")
    parser.add_argument("--extra-editor", action="append", default=[],
                        help="extra folder of editor .cs files to compile (e.g. staged scripts outside Assets)")
    args = parser.parse_args()

    runtime_now, editor_now = scan_sources()
    for extra in args.extra_editor:
        for base, _, files in os.walk(os.path.abspath(extra)):
            editor_now.update(os.path.join(base, f) for f in files if f.endswith(".cs"))
    with tempfile.TemporaryDirectory(prefix="compile_check_") as tmp:
        rt_opts, _ = split_rsp(latest_rsp("Assembly-CSharp"))
        ed_opts, _ = split_rsp(latest_rsp("Assembly-CSharp-Editor"))

        # Our own asmdefs first (e.g. the pure simulation + its tests), so Assembly-CSharp can reference them.
        own = own_asmdefs()
        own_dirs = [info["dir"] for _, info in own]
        # Every own assembly is rebuilt into tmp and referenced from there; Unity's copies under Library/Bee
        # are dropped from all rsp files so no stale duplicate is ever referenced.
        stale_refs = frozenset(n + suffix for n, _ in own for suffix in (".dll", ".ref.dll"))
        built = {}
        runtime_own_refs = []
        for name, info in own:
            srcs = sources_in(info["dir"], own_dirs)
            if not srcs:
                continue
            dll = os.path.join(tmp, name + ".dll")
            refs = [built[r] for r in info["refs"] if r in built]
            opts = ed_opts if info["editor"] else rt_opts
            code, out = run(write_rsp(opts, srcs, dll, tmp, name, extra_refs=refs, drop_refs=stale_refs))
            errors = report(name, out, args.warnings, args.filter)
            if code != 0 and errors == 0:
                print(out)
                errors = 1
            if errors:
                print(f"FAILED: {errors} error(s) in {name}.")
                return 1
            built[name] = dll
            if not info["editor"]:
                runtime_own_refs.append(dll)

        rt_dll = os.path.join(tmp, "Assembly-CSharp.dll")
        code, out = run(write_rsp(rt_opts, runtime_now, rt_dll, tmp, "runtime", extra_refs=runtime_own_refs, drop_refs=stale_refs))
        errors = report("runtime", out, args.warnings, args.filter)
        if code != 0 and errors == 0:
            print(out)
            errors = 1
        if errors:
            print(f"FAILED: {errors} runtime error(s); editor assembly not checked.")
            return 1

        ed_dll = os.path.join(tmp, "Assembly-CSharp-Editor.dll")
        code, out = run(write_rsp(ed_opts, editor_now, ed_dll, tmp, "editor",
                                  replace_ref=("Assembly-CSharp.ref.dll", rt_dll), extra_refs=runtime_own_refs,
                                  drop_refs=stale_refs))
        errors = report("editor", out, args.warnings, args.filter)
        if code != 0 and errors == 0:
            print(out)
            errors = 1
        if errors:
            print(f"FAILED: {errors} editor error(s).")
            return 1
    print(f"OK: runtime {len(runtime_now)} files, editor {len(editor_now)} files compiled without errors.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
