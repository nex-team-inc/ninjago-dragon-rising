---
name: unity-cli-troubleshooting
description: Use when the Unity CLI is missing, fails, or cannot reach the Editor (command not found, no instances in `unity status`, Safe Mode, missing `com.unity.pipeline`, batch mode refusing an open project, CPU instruction-set errors in a sandbox). Diagnoses step by step without falling back to hand-editing Unity files.
---

# Unity CLI Troubleshooting

Work down this list in order. Never skip ahead to editing files by hand, and never treat a blocked
CLI as permission to author `.meta`, `.unity`, `.prefab`, or `.asset` files yourself — that holds
when the CLI is missing entirely, not just when it is failing.

No step below is a prerequisite for importing: files placed under `Assets/` are imported by the open
Editor with no CLI involved, so reach for that before you consider the task blocked.

1. **Confirm the binary exists** before reading anything into a failure:
   `which unity && unity --version`. A `command not found` is not proof it is absent. The Unity Hub
   installs the CLI automatically, so it is usually present but not yet on `PATH` — a fresh shell
   resolves that, most often on Windows — and a sandbox can hide it exactly as in step 4. Once it
   responds, `unity doctor` checks `PATH` presence and whether a second `unity` binary shadows the
   real one.
2. **If it is genuinely absent, ask the user to install it** rather than installing it yourself. The
   documented install pipes a script from Unity's CDN into a shell and writes a binary onto their
   machine outside this project, so that is their call, not yours — unlike the project-scoped
   package in step 6. The commands are in the [`unity-cli`](../unity-cli/SKILL.md) skill. A new shell
   is needed afterwards, so you will likely not be able to use it until the next session.
3. **Run `unity pipeline list`.** It is the one command that works without the `com.unity.pipeline`
   package, and it reports each instance's `isRunning`, `hasPipelinePackage`,
   `pipelineServer.isReachable`, and `safeMode`.
4. **Rule out sandbox artifacts** before believing a failure. `The required instruction sets are not
   supported by the current CPU.` and `unity status` reporting no instances are both commonly
   produced by a restrictive agent sandbox hiding a healthy Editor. Re-run the same command with
   elevated permissions before concluding anything is unavailable.
5. **Safe Mode means C# compile errors**, and `unity pipeline list` reports it. Fixing the source is
   the fix, not a workaround; the Editor must be restarted afterwards.
6. **A missing `com.unity.pipeline` package** blocks `unity status`, `unity command`, and
   `unity list`. Installing it is Unity's documented setup step, not a workaround: run
   `unity auth login`, then `unity pipeline install`, wait for the Editor to recompile, then confirm
   with `unity pipeline list`. Take the default version rather than pinning one, and pass
   `--project-path` when several projects are open, because non-interactive runs refuse to guess.
   The package resolves from the Unity registry into `Packages/manifest.json`, which is tracked —
   say that you changed it, and keep it out of unrelated commits.
7. **Batch mode refuses a project that is already open** (`already open in a running Editor (PID ...)`).
   Ask the user to close the Editor instead of working around it, and never run `-executeMethod`
   against the project they have open.
8. **If none of that unblocks you, stop** and say plainly what you tried and what you need — install
   the CLI, close the Editor, sign in, or enable Unity MCP. Do not improvise a substitute path.

## References

- General CLI usage, detection, and installation: the [`unity-cli`](../unity-cli/SKILL.md) skill.
- [Use the Unity CLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli)
- [Unity Pipeline package](https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package)
