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


def asmdef_dirs():
    dirs = []
    for base, _, files in os.walk(os.path.join(PROJECT, "Assets")):
        if any(f.endswith(".asmdef") or f.endswith(".asmref") for f in files):
            dirs.append(os.path.relpath(base, PROJECT) + "/")
    return dirs


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


def write_rsp(options, sources, out_dll, tmp, name, replace_ref=None):
    rsp = os.path.join(tmp, name + ".rsp")
    with open(rsp, "w", encoding="utf-8") as fh:
        for opt in options:
            if replace_ref and opt.startswith("-r:") and replace_ref[0] in opt:
                opt = f'-r:"{replace_ref[1]}"'
            fh.write(opt + "\n")
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
    args = parser.parse_args()

    runtime_now, editor_now = scan_sources()
    with tempfile.TemporaryDirectory(prefix="compile_check_") as tmp:
        rt_opts, _ = split_rsp(latest_rsp("Assembly-CSharp"))
        rt_dll = os.path.join(tmp, "Assembly-CSharp.dll")
        code, out = run(write_rsp(rt_opts, runtime_now, rt_dll, tmp, "runtime"))
        errors = report("runtime", out, args.warnings, args.filter)
        if code != 0 and errors == 0:
            print(out)
            errors = 1
        if errors:
            print(f"FAILED: {errors} runtime error(s); editor assembly not checked.")
            return 1

        ed_opts, _ = split_rsp(latest_rsp("Assembly-CSharp-Editor"))
        ed_dll = os.path.join(tmp, "Assembly-CSharp-Editor.dll")
        code, out = run(write_rsp(ed_opts, editor_now, ed_dll, tmp, "editor",
                                  replace_ref=("Assembly-CSharp.ref.dll", rt_dll)))
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
