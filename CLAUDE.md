# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Coremenders — Primal Frost is a 2-player co-op prehistoric survival RPG built on Unity 6 LTS (6000.x), URP, and Netcode for GameObjects 2.x (host-authoritative listen server).

Design documents and logs live in `Docs/` at the repository root (this file sits at the root alongside it):
- `GDD.md` — full game design (what the game is)
- `TDD.md` — architecture and phased build plan (how it is built)
- `TODO.md` — known deferred items
- `ManualSteps.md` — running log of Editor-wiring and playtest-validation steps, per build. **You write to this file** (see "Working with This Project").
- `VersionControl.md` — how git and archives work here; read before touching anything version-control related

**Always read the relevant GDD/TDD section before implementing a feature.** The TDD defines exact build-by-build acceptance criteria; follow them in order.

---

## Working with This Project

The loop for each build: a build's task list is authored into the TDD; you execute the code tasks for that build; you record everything that requires the Editor — both wiring and validation — into `ManualSteps.md` for the human to carry out.

- **Read before building.** Find the relevant GDD/TDD section first. The TDD is the living source of truth for what to build next and what "done" means.
- **Implement the build's code tasks** as specified in the TDD. Tasks marked "Split — logic: Claude Code; wiring: Manual" mean you write the code and the human does the Editor steps.
- **Record manual steps in `Docs/ManualSteps.md`** — do not just list them at the end of your response. Append a new section for the build (`## Build X.Y.Z — Name`) and, under it, an unchecked `- [ ]` entry for every step that needs the Editor: prefab additions, scene/GameObject setup, component wiring, Inspector/config values, asmdef reference changes. Match the existing file's format — a **bold title** that references the TDD task ID where one exists (e.g. `**A5 — Dreamer prefab**`), then sub-bullets giving the exact steps and a short *why*. The human ticks them off as they go.
- **Record playtest-validation steps in `Docs/ManualSteps.md` too.** After the wiring entries for a build, add the steps the human runs to validate it, derived from the build's TDD acceptance criteria. Provide a single-client check first, then the MPPM two-client check (the project is multiplayer-first — assume host + owning client, not single-player). Structure each sub-test as a numbered list of procedural steps, then end with a **separate** `- [ ] ✅ <pass condition>` checkbox on its own line — never inline the pass condition as the last numbered step. The human ticks each `- [ ] ✅` line when the condition is confirmed.
- **Log deferrals in `Docs/TODO.md`.** When you defer or skip something, add an entry to `TODO.md` rather than leaving a silent `// TODO` in code.

---

## Tooling & Build

This is a standard Unity project — all compilation and play-testing happen inside the Unity Editor. There are no CLI build scripts.

| Action | How |
|---|---|
| Compile | Open the project in Unity 6 LTS; the editor compiles on script change. |
| Two-client test | Window → Multiplayer → Multiplayer Play Mode → enable one Virtual Player, then press Play. Both clients run in the same editor process. |
| Single-client test | Press Play normally. |
| Run a specific test | Unity Test Runner (Window → General → Test Runner). No tests exist yet; the framework is ready for use. |

Claude Code edits `.cs` files, `.json` templates, and `.asmdef` files. It **cannot** operate the Unity Editor GUI — component wiring, prefab registration, scene setup, and Inspector values require manual Editor steps.

---

## Version Control

Repository root is the Unity project root — the folder holding `Assets/`, `Packages/`, and `ProjectSettings/`. Full policy in `Docs/VersionControl.md`; the operative rules for you are:

- **Git LFS is deliberately not used.** Never run `git lfs install` or `git lfs track`, and never add a `filter=lfs` entry to `.gitattributes`. Heavy binaries churn too much and LFS objects cannot be deleted once pushed. Archives handle them instead.
- **Heavy binary assets are gitignored and restored from restic archives**, not from git. A `git clone` alone does not yield a runnable project. This is intended — do not "fix" it by committing the missing assets.
- **Ignored assets and their `.meta` files travel together.** Never commit one without the other; never add a `.meta` for an ignored asset.
- **`ProjectSettings/` and `Packages/` are always tracked.** Never add them to `.gitignore` for any reason.
- **Don't commit files over ~10 MB.** If it's that large it belongs in the archive. A local pre-commit hook enforces this.
- Scenes, prefabs, materials and `.asset` files are YAML text under Force Text serialization and must stay diffable. Never introduce anything that serializes them as binary.

---

## Four-Layer Architecture

The codebase is split into four Unity assembly definitions. The compiler enforces the dependency boundaries — violating them is a build error, not a style issue.

```
Game.Simulation   ← no Unity engine references (pure C#)
Game.Persistence  ← references Simulation only
Game.Networking   ← references Simulation + NGO
Game.Presentation ← references Simulation + Networking + Persistence
```

**The critical invariant:** `Game.Simulation` has `noEngineReferences: true`. It must never import `UnityEngine.*` rendering types. This is the compiler-enforced wall that guarantees the simulation is fully serializable and revertable. AI-assisted tooling must not introduce Unity type references into the Simulation assembly.

**Namespace convention:** always use `Game.<Layer>` to match the assembly name (e.g. `Game.Simulation`, `Game.Networking`). Never use the project name as a namespace prefix.

**Cross-layer rule:** `Game.Networking` does NOT reference `Game.Persistence`. The real load path calls `TemplateLoader`/`SaveSystem` (Persistence) from `GameFlowManager` (Networking) through dependency injection, not an asmdef reference. Debug-only helpers that need both layers inline the minimal logic rather than adding a cross-layer reference.

---

## Runtime Data Manager (RDM)

`RuntimeDataManager` (Networking layer) is the **host-side authoritative state store** — the single source of truth for all gameplay state. It holds the `WorldState` tree and the dreamer transform registry.

- It is a **state store, not a god object**. Game logic (needs drain, buff ticks, etc.) lives in `SimResolver` and the sync components — not inside the RDM.
- It is **reactive, not a background loop**. Time only advances via `WorldClockDriver`'s real-time tick or `SkipManager`'s chunked loop; there is no autonomous update.

---

## Simulation & Tick

`SimResolver.Step(WorldState, NeedsConfig, BuffConfig, elapsed)` is the **single canonical advance function**. It runs 11 ordered stages (clock → buff tick → drain → tasks → affliction reconcile → Vitality drain → incapacitation → AI → weather → day boundary → skip control). All code paths — real-time, Skip Time, and inactive-map re-entry — call this same function.

**Invariant:** ticking forward N seconds one step at a time must equal resolving N seconds in a single call. Any change to `SimResolver` must preserve this property.

**Fixed entity processing order** (by slot id) is required for determinism and save/revert reproducibility.

---

## Save, Snapshot & Revert

- **One serializer, one target: disk.** `ISaveSerializer` → `NewtonsoftSerializer` → JSON. All persisted state goes through this path.
- **Checkpoints are saves.** A dream checkpoint uses the same writer/format as a regular save, differentiated only by the slot name and metadata. There is no separate in-memory snapshot path.
- **Revert = warm load.** `RevertToCheckpoint` reuses the `D5` apply path from `GameFlowManager`: repopulate RDM → restore clock → snap dreamer positions via `SnapToPositionRpc` (authority-override).
- **Atomic writes.** Every save uses temp-file → `File.Replace` to prevent corruption on crash.
- **Schema versioning.** The authoritative schema version is the `SaveSystem.CurrentSchemaVersion` constant — treat that constant (and the template file) as the source of truth, not any number written here. On every structural change to the save format: bump the constant and update the template file in the same change. Old saves are rejected loudly during development; migration is deferred until Early Access.
- **`SaveSystem.Save` — world-only copy trap.** `Save()` manually constructs a `worldOnly` object that contains only world-level fields (not dreamers, which go in separate files). **Every field on `WorldState` must be explicitly listed in that constructor.** Adding a field to `WorldState` without adding it to `worldOnly` causes it to silently save as default/empty on every write. Always check `SaveSystem.Save` when adding a field to `WorldState`.

---

## Networking Authority Model

| Data | Authority | Mechanism |
|---|---|---|
| Dreamer position | Client (owner) | Owner-auth `NetworkTransform` |
| NPC / creature position | Host | Server-auth `NetworkTransform` |
| Needs, Vitality, afflictions, buffs | Host | Periodic `NetworkVariable` push + `ForcePush()` after Skip/revert |
| World clock | Host | `WorldClockSync` periodic push; client extrapolates |
| Dreamer incapacitation / dream flow | Host | Event-driven |

NGO's distributed-authority mode is **not** used. The host is the single authority because save/revert integrity requires one unambiguous owner.

Dreamer movement is client-authoritative. **But the host owns health and death.** The incapacitation check and dream flow run only on the host.

### Wire format — ids, not strings

Sync the smallest stable reference, never display text. Anything backed by a
static definition or an enumerable option set crosses the wire as a numeric
id, index, or enum byte; the receiver resolves names/icons/labels from its own
SO defs (identical on host and client, per rule 6). Keeps DTOs blittable,
avoids the `Unity.Collections` FixedString dependency, and cuts bandwidth.

- `NetworkVariable<T>`, RPC parameters, and `INetworkSerializable` fields:
  ids / indices / enums only.
- Resolve display strings client-side from SOs.
- **Exception:** genuinely free-form runtime text with no def backing
  (player-entered names, chat). Only then use a managed `string`
  (RPC / `INetworkSerializable`) or `FixedString` (`NetworkVariable`). We have
  none yet — so no string should currently cross the wire.
- Apply this by default without asking; it follows from the SO-backed
  static-content model.

---

## Data Model Highlights

**`WorldState`** — root authoritative state; serialized as `world.json`.

**`DreamerRecord`** — per-dreamer authoritative state (slot, position, appearance, needs, energy, vitality, afflictions, task, buffs); serialized as `dreamer_<slot>.json`.

**`Float3`** — Unity-free 3D vector used inside `Game.Simulation`. Convert to/from `Vector3` only at engine-layer boundaries (in Networking/Presentation).

**`BuffDef`** / **`BuffInstance`** — static definition (SO asset) vs runtime instance. Instances carry `remainingMinutes` and an optional `checkpointId` for protection buffs.

**`NeedsConfig`** / **`BuffConfig`** — Inspector-editable tuning containers on `WorldClockDriver`. No magic numbers in the simulation.

**`CheckpointMeta`** — metadata sidecar for each checkpoint file (timestamp, coverage label, etc.).

---

## NGO Lifecycle Rules

These rules exist because domain reload is disabled in this project. Without domain reload, static fields survive between play sessions, and a missed teardown accumulates subscriptions until the transport queue floods.

**1. Every `OnNetworkSpawn` override must call `base.OnNetworkSpawn()` first; every `OnNetworkDespawn` override must call `base.OnNetworkDespawn()` last.** NGO 2.x relies on these base calls for its own internal cleanup. Omitting them silently skips NGO housekeeping — same class of bug as the `OnDestroy` fix applied earlier.

**2. Any subscription made in `OnNetworkSpawn` must use the unsubscribe-before-subscribe guard:**
```csharp
_someNv.OnValueChanged -= MyHandler;
_someNv.OnValueChanged += MyHandler;
```
This applies to NetworkVariable `OnValueChanged`, `NetworkList.OnListChanged`, C# events on sibling components, and any other delegate subscription. In-scene NetworkObjects can despawn and respawn (scene reload, reconnect); a missed `OnNetworkDespawn` leaves the old subscription alive, so the next spawn doubles the handler count. Every subscription that writes a NetworkVariable or sends an RPC will then fire twice per event — the transport queue overflows within minutes.

**3. All subscriptions made in `OnNetworkSpawn` must have a matching `-=` in `OnNetworkDespawn`, not only `OnDestroy`.** Despawn precedes destroy and objects can re-spawn.

**4. `StopAllCoroutines()` must be the first line of `OnNetworkDespawn` on any `NetworkBehaviour` that starts a coroutine.** Between despawn and destroy the coroutine is still running; it can access null singletons and call `ForcePush()` on already-torn-down sync components.

**5. Every `NetworkManager` callback (`OnClientConnectedCallback`, `OnClientDisconnectCallback`, `OnServerStarted`, `SceneManager.OnLoadEventCompleted`, etc.) must use the same unsubscribe-before-subscribe guard at its subscribe site**, regardless of whether the subscriber is a `NetworkBehaviour`:
```csharp
NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
```
`GameFlowManager` is `DontDestroyOnLoad` and its `BeginHost`/`BeginLoad`/`StartGame` methods are entry points that can be called repeatedly. Without the guard each call stacks another copy of the handler.

**6. Every class with `public static … Instance` (or any static field holding a MonoBehaviour reference) must include:**
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStatics() => Instance = null;
```
With domain reload disabled, the static reference outlives the play session and points to a destroyed object. The standard `Instance != null && Instance != this` Awake guard uses C# reference equality — it returns `true` for a destroyed MonoBehaviour — so without this reset the fresh new instance destroys itself and the dead instance remains as the live singleton.

---

## Key Invariants & Rules

1. `Game.Simulation` must never reference `UnityEngine.*` rendering types.
2. `Game.Networking` must never reference `Game.Persistence` via asmdef.
3. `SimResolver.Step()` is the only path that advances world time. Do not advance state outside it.
4. All save writes must be atomic (temp → `File.Replace`).
5. Two dreamers always exist in the simulation, regardless of player count. Solo = co-op with second dreamer AI-controlled.
6. Static definitions (recipes, affliction defs, buff defs) use ScriptableObjects. **Runtime mutable state never uses ScriptableObjects** — they don't snapshot/revert cleanly.
7. The RNG stream that drives stochastic tick behavior (weather, AI decisions) must be part of the saved authoritative state so reverts reproduce identically (deferred until first stochastic system lands).
8. Nothing crosses the wire as a display string. Sync ids / indices / enum
   bytes and resolve text from SO defs client-side. Strings only for
   free-form runtime text (names, chat) — of which there are none yet.
9. **Lists, not numbered fields.** Any def collection that may grow — recipe inputs, tier tables,
   aspect lists, action lists — is a list the code *iterates*, never fixed numbered fields
   (`input1`, `input2`…). Adding an element must be pure authoring, never a code change.
10. **Git LFS is never used.** No `git lfs track`, no `filter=lfs` in `.gitattributes`. See `Docs/VersionControl.md`.
11. **`ProjectSettings/` and `Packages/` are always tracked in git**, and heavy binary assets never are.

---

## Current Build Status

Phase 0 and Cluster 1 are complete. We are in **Cluster 2** (Survival Actions, builds 0.2.x).

Do not rely on a hardcoded checklist here — it drifts. Instead, **read TDD §4** to find the next unchecked build and its detailed task breakdown, and consult `Docs/TODO.md` for known deferred items, before starting any work. Implement builds in order; the next build to implement is the one immediately after the last completed build recorded in the TDD.
