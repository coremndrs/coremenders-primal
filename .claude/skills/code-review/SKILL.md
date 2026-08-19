---
name: code-review
description: Review Coremenders C# code for logical errors, bugs, and violations of the project's architecture invariants, then write a dated findings report to <DOC>/CodeReviews/. Use when the user asks to review code, audit code, check for bugs, sanity-check a build, look for logical issues, or says "/code-review". Takes an optional scope argument (a file, folder, assembly, TDD build id, or "all"); with no argument it reviews uncommitted changes.
---

# Code Review — Coremenders: Primal Frost

Find real bugs. Write a report. **Do not change any code.**

This skill is a read-and-report pass. Even when a fix is obvious, it goes in the
report as a proposed fix — never as an edit. The only file you write is the
report (and `TODO.md` if the user asks).

---

## Step 0 — Resolve the paths

This skill writes `<G>` for the game source folder and `<DOC>` for the
documentation folder. Resolve both once, at the start of every run, and
substitute the real paths into every command and path below.

```bash
pwd
ls
```

- If the current folder contains `Assets/` and `Docs/`, you are at the project
  root (which is also the repo root): `<G>` = `Assets/Game`, `<DOC>` = `Docs`.
- Otherwise move to the project root — the folder holding `Assets/`, `Packages/`
  and `ProjectSettings/` as siblings — and resolve from there:
  `git rev-parse --show-toplevel`, or `find . -maxdepth 3 -type d -name Docs`.

`CLAUDE.md` sits at the project root, next to `Docs/` — read it from there, not
from an assumed location.

**Git paths are always repo-root-relative.** `git` works from any subfolder, but
`git diff --name-only` prints paths from the repository root (e.g.
`Assets/Game/Networking/Foo.cs`) even when your working directory is `Assets/`.
Convert them before reading: `git rev-parse --show-prefix` prints your
working-directory prefix, and `git rev-parse --show-toplevel` the repo root.
Simplest reliable move — run git commands with `git -C "$(git rev-parse --show-toplevel)"`
and read the resulting files by their repo-root-relative path.

---

## Step 1 — Resolve the scope

Read the argument the user passed (`$ARGUMENTS`, may be empty):

| Argument | Scope |
|---|---|
| *(empty)* | `git diff HEAD --name-only` + `git ls-files --others --exclude-standard`, filtered to `*.cs`. If that is empty, fall back to the files touched by the last commit (`git show --stat HEAD`). |
| a path (`<G>/Networking/SkipManager.cs`, `<G>/Simulation/`) | That file, or every `.cs` under that folder. |
| an assembly name (`Simulation`, `Networking`, `Persistence`, `Presentation`, `Editor`) | Every `.cs` in that assembly folder. |
| a TDD build id (`0.2.7a`, `0.2.9e`) | Read the build's task table in `<DOC>/TDD.md`, then find the files it names or implies. Cross-check with `git log --oneline --grep=<id>` if commits reference build ids. |
| `all` | Every `.cs` under `<G>/`. |

State the resolved scope (file count + list) before reviewing. If the scope is
empty, say so and stop — do not silently widen it.

**Always skip:** `TutorialInfo/`, `<G>/Networking/WorldItemsSync.cs`
(known obsolete stub), and anything under `Library/`, `Temp/`, `obj/`.
**Known-legacy, review only if explicitly named:** `DreamerCreationUI.cs`,
`ConnectionBootstrap.cs`, `DebugAutoConnect.cs`.

---

## Step 2 — Load the ground truth

Before reading any reviewed file, read:

1. `CLAUDE.md` (repo root) — the invariants are the spec. A violation is a bug
   even if the code "works".
2. `references/checklist.md` in this skill folder — the full check catalogue.
3. The TDD section governing the code in scope. `<DOC>/TDD.md`
   defines what "correct" means for each build; a behaviour that contradicts its
   acceptance criteria is a finding.
4. `<DOC>/TODO.md` — an item already logged there is **not** a new
   finding. Note it as "already tracked" and move on.

If `CLAUDE.md` and the code disagree about intent, the finding is "code
contradicts CLAUDE.md" — do not assume CLAUDE.md is stale.

---

## Step 3 — Run the mechanical sweep

Some invariants are grep-checkable across the whole scope in seconds. Run these
first — they are cheap and they catch the highest-frequency real defects in this
codebase. The exact commands are in `references/checklist.md` §M.

Cover at minimum:

- `OnNetworkSpawn` / `OnNetworkDespawn` base-call and teardown discipline
  (CLAUDE.md NGO rules 1–4)
- unsubscribe-before-subscribe guards on every `+=` (rules 2 and 5)
- `StopAllCoroutines()` first line of `OnNetworkDespawn` on coroutine users (rule 4)
- `ResetStatics()` on every class with a `static ... Instance` (rule 6)
- `UnityEngine` references inside `Game.Simulation` (rule 1)
- `string` / `FixedString` on `NetworkVariable`, RPC params, `INetworkSerializable` (rule 8)
- `WorldState` fields absent from the `worldOnly` construction in `SaveSystem.Save`
- numbered fields (`input1`, `slot2`, `tier3`) where a list is required (rule 9)

Record what each sweep returned, including "clean". A sweep you skipped is not a
sweep that passed.

---

## Step 4 — Read for logic, one file at a time

The sweep finds pattern violations. Actual logic bugs need reading. For each file
in scope, read it in full and work the four lenses in
`references/checklist.md` §§A–D:

- **A — Project invariants**: layer boundaries, `SimResolver.Step()` as the only
  time-advance, no ScriptableObjects holding mutable runtime state, ids not
  strings on the wire.
- **B — General C#/Unity logic**: null derefs, off-by-one, inverted conditions,
  wrong variable used, unreachable or duplicated branches, integer division,
  float equality, mutating a collection while iterating it, `??` vs Unity's
  fake-null, coroutine/async lifetime, swallowed exceptions.
- **C — Multiplayer correctness**: host-vs-client authority mistakes, RPC
  ownership and permission, ordering assumptions between spawn and first data
  arrival, missing `ForcePush()` after Skip or revert, client-side writes to
  host-authoritative state, per-frame RPC/NetworkVariable writes.
- **D — Determinism, save & revert**: non-deterministic iteration order,
  unsaved RNG state, fields that never round-trip, revert paths that merge live
  state instead of clearing and rehydrating, non-atomic writes, schema version
  not bumped alongside a format change.

When a file is large (`DreamerInventorySync.cs`, `MapEntitySync.cs`,
`GameFlowManager.cs`, `SkipManager.cs`, `DreamFlowManager.cs`), read it in
sections but read all of it — the bugs in this codebase live in the seams
between a spawn path and a despawn path, and a partial read misses them.

**Follow the call, not just the file.** Before writing a finding about a method,
grep its callers. Half of the plausible-looking findings die here: the null is
checked upstream, the host guard is on the caller, the coroutine is never started
on a client.

### Parallelising a wide scope

If the scope is more than ~12 files, dispatch subagents — one per assembly or per
coherent subsystem — each returning findings in the report's finding format.
Give each subagent `CLAUDE.md` and `references/checklist.md` verbatim as its
brief. Then verify their findings yourself (Step 5); do not paste subagent output
into the report unverified.

---

## Step 5 — Verify before you write

This is the step that decides whether the report is worth reading. A report with
three real bugs beats one with three real bugs and nine plausible guesses,
because the guesses cost trust and hours.

For every candidate finding, before it enters the report:

1. **Re-read the actual lines.** Not your memory of them.
2. **Write the concrete failure**: specific inputs or sequence → specific wrong
   outcome. "Could be null" is not a failure scenario. "Client joins before the
   host spawns dreamer slot 1, `_registry[slot]` throws in `OnValueChanged`, the
   client's needs HUD never populates" is.
3. **Try to refute it.** Look for the guard that makes it impossible: an
   `IsServer` check on the caller, an `Awake` that guarantees the reference, a
   TDD note saying the case cannot arise. If you cannot decide in a reasonable
   read, keep it but mark it **Plausible** rather than **Confirmed**.
4. **Drop pure style.** Naming, formatting, "could be more efficient", and
   "consider extracting a method" are not findings. Only report a readability
   issue when it is actively hiding a correctness risk.

Findings that survive get a verdict: **Confirmed** (you traced the failure path)
or **Plausible** (it looks wrong but one branch is unverified — say which).

---

## Step 6 — Rank

| Severity | Meaning |
|---|---|
| **Critical** | Corrupts saves, breaks revert reproducibility, desyncs host and client, or floods the transport. Data loss or an unshippable state. |
| **High** | Wrong gameplay behaviour, a crash or exception on a reachable path, or a violation of a CLAUDE.md invariant that will bite later. |
| **Medium** | Bug on an edge path, a guard that is missing but currently unreachable, or a TDD acceptance criterion not actually met. |
| **Low** | Latent risk, dead code, an inconsistency that could mislead the next change. |

Rank across the whole report, most severe first — not grouped by file.

---

## Step 7 — Write the report

Write to `<DOC>/CodeReviews/Review_YYYY-MM-DD.md`, creating the
folder if needed. If a report for that date exists, append `_2`, `_3`, ….
Use `references/report-template.md` exactly.

Then, **in chat, keep it short**: scope reviewed, count by severity, the one or
two findings worth acting on today, and the report path. Do not restate the
report in the chat — Mike will open the file.

If the user asked for TODO entries, append each Confirmed Critical/High finding
to `<DOC>/TODO.md` in that file's existing format, referencing the
report by filename.

---

## The bar

A finding earns its place only if a reader can go to the cited line, see the
problem, and know what to do about it. Nine findings you were unsure about are
worse than the three you traced. If the scope is genuinely clean, the correct
report says so and lists the sweeps that ran — a short honest report is a good
outcome, not a failed run.
