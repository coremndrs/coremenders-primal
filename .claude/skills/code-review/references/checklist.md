# Review checklist — Coremenders: Primal Frost

Four lenses (§A–D) plus a mechanical sweep (§M). Each entry states what to look
for and why it matters here, so a finding can be written with a real failure
scenario rather than a rule citation.

`CLAUDE.md` in the repo root is the authority. Where this file and `CLAUDE.md`
disagree, `CLAUDE.md` wins and this file needs updating.

---

## §M — Mechanical sweep (run first, across the whole scope)

`rg` is ripgrep. `<G>` is the game source folder and `<DOC>` the documentation
folder, both resolved in SKILL.md Step 0 — substitute the real paths before
running (`Assets/Game` / `Docs` from the repo root, which is also the Unity
project root). Narrow `<G>` to the
files in scope when the scope is smaller than the whole tree.

### M1 — Base calls on NGO lifecycle overrides (NGO rule 1)

```bash
rg -n --type cs -A6 "override void OnNetworkSpawn" <G>
rg -n --type cs -A8 "override void OnNetworkDespawn" <G>
```

`OnNetworkSpawn` must call `base.OnNetworkSpawn()` **first**;
`OnNetworkDespawn` must call `base.OnNetworkDespawn()` **last**. A missing base
call silently skips NGO's internal cleanup — same class as the `OnDestroy` bug
already fixed in this project.

### M2 — Unsubscribe-before-subscribe guards (NGO rules 2 and 5)

```bash
rg -n --type cs -B2 "\.OnValueChanged \+=" <G>
rg -n --type cs -B2 "\.OnListChanged \+=" <G>
rg -n --type cs -B2 "OnClientConnectedCallback \+=|OnClientDisconnectCallback \+=|OnServerStarted \+=|OnLoadEventCompleted \+=" <G>
```

Every `+=` needs the matching `-=` on the line immediately before it. Domain
reload is disabled, in-scene NetworkObjects respawn, and `GameFlowManager` entry
points (`BeginHost`/`BeginLoad`/`StartGame`) can be called repeatedly — each
unguarded subscribe stacks another handler. Two handlers means every
NetworkVariable write and RPC fires twice; the transport queue overflows within
minutes. This is the highest-value sweep in the file.

### M3 — Teardown symmetry (NGO rule 3)

For each `+=` inside `OnNetworkSpawn`, confirm a matching `-=` inside
`OnNetworkDespawn` — **not only** in `OnDestroy`. Despawn precedes destroy and
the object can respawn in between.

### M4 — Coroutines on NetworkBehaviours (NGO rule 4)

```bash
rg -ln --type cs "StartCoroutine" <G>
```

Any `NetworkBehaviour` that starts a coroutine must have `StopAllCoroutines()`
as the **first line** of `OnNetworkDespawn`. Between despawn and destroy the
coroutine still runs and can touch null singletons or call `ForcePush()` on a
torn-down sync component.

### M5 — Static instance reset (NGO rule 6)

```bash
rg -n --type cs "static .*Instance" <G>
rg -ln --type cs "RuntimeInitializeOnLoadMethod" <G>
```

Every class in the first list must appear in the second with:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStatics() => Instance = null;
```

Without it the stale reference survives the play session pointing at a destroyed
object; the usual `Instance != null && Instance != this` Awake guard passes
(C# reference equality returns true for a destroyed MonoBehaviour), so the
**fresh** instance destroys itself and the dead one stays live.

### M6 — Simulation purity (invariant 1)

```bash
rg -n --type cs "UnityEngine|using Unity\." <G>/Simulation
```

Expect zero hits. `Game.Simulation` is `noEngineReferences: true` — this is the
compiler-enforced wall that keeps the simulation serializable and revertable.
`Vector3` here means someone is about to break revert.

### M7 — Strings on the wire (invariant 8)

```bash
rg -n --type cs "NetworkVariable<.*string|NetworkVariable<.*FixedString" <G>
rg -n --type cs -A4 "\[Rpc\(|ServerRpc\(|ClientRpc\(" <G> | rg -n "string"
rg -n --type cs -A15 "INetworkSerializable" <G> | rg -n "string"
```

Nothing backed by a def crosses the wire as text — ids, indices, or enum bytes
only, resolved to display strings client-side from SOs. There is currently no
legitimate free-form runtime text in the project, so **any** hit is a finding.

### M8 — The `worldOnly` trap

```bash
rg -n --type cs "" <G>/Simulation/WorldState.cs
rg -n --type cs -A25 "worldOnly" <G>/Persistence/SaveSystem.cs
```

Every field on `WorldState` must be explicitly listed in the `worldOnly` object
that `SaveSystem.Save()` hand-builds. A field that is missing saves as
default/empty on every single write, silently, with no error — and only surfaces
as a mysteriously reset value after a load. Diff the two lists field by field.

### M9 — Numbered fields (invariant 9)

```bash
rg -n --type cs "(input|output|tier|slot|aspect|action|material)[0-9]\b" <G>
```

Any def collection that can grow is a list the code iterates, never `input1`,
`input2`. Adding an element must be pure authoring, not a code change.

### M10 — Layer boundaries (invariant 2)

```bash
rg -n "Game.Persistence" <G>/Networking
cat <G>/Networking/Game.Networking.asmdef
```

`Game.Networking` must not reference `Game.Persistence` — Persistence reaches
Networking by injection (`GameFlowManager`), and debug helpers inline the minimal
logic instead of adding the reference.

### M11 — Namespace convention

```bash
rg -n --type cs "^namespace" <G> | rg -v "namespace Game\."
```

Everything is `Game.<Layer>` matching the assembly. (Known outstanding:
`GameSettingsApplier` sits in the global namespace — report it only if it is in
scope and not yet in `TODO.md`.)

---

## §A — Project invariants (read-level)

- **A1 — `SimResolver.Step()` is the only time advance.** Any code that mutates
  clock, needs, energy, vitality, buff remaining time, task accrual, or spoilage
  outside the resolver breaks the tick-equivalence invariant. Look for arithmetic
  on those fields in sync components, RPC handlers, and UI code.
- **A2 — Tick equivalence.** N seconds in one call must equal N seconds one step
  at a time. Suspects: logic keyed to "this is the first/last chunk", any
  threshold crossing tested with `==`, per-call rather than per-elapsed-time
  effects, and anything that reads wall-clock or `Time.deltaTime` inside the
  resolver.
- **A3 — Fixed entity processing order.** Entities process by slot id.
  Iterating a `Dictionary`, a `HashSet`, or an unsorted `foreach` over dreamers
  or map entities inside the resolver breaks save/revert reproducibility.
- **A4 — No ScriptableObjects for mutable runtime state (invariant 6).** SOs are
  static definitions only. A runtime value written onto an SO does not snapshot,
  does not revert, and persists across play sessions in the editor. Watch for
  writes to `Def`, `ActionDef`, `RecipeDef`, `BuffDef` fields at runtime.
- **A5 — Def vs instance.** `BuffDef`/`BuffInstance`, `Def`/`Instance` — check
  that runtime mutation targets the instance, and that the instance is what gets
  serialized.
- **A6 — RDM is a store, not a god object.** Game logic appearing inside
  `RuntimeDataManager`, or an autonomous update loop in it, contradicts the
  design: time advances only via `WorldClockDriver` or `SkipManager`.
- **A7 — Two dreamers always exist (invariant 5).** Code that assumes a dreamer
  is present only when a human client is connected, or that indexes dreamers by
  connected-client count, is wrong in solo play.
- **A8 — No magic numbers in the simulation.** Tuning values belong in
  `NeedsConfig`, `BuffConfig`, `MovementConfig`, `SpoilageConfig`. A literal in
  `SimResolver` or a sync component is a finding.
- **A9 — Deferrals logged.** A bare `// TODO` in code with no matching entry in
  `TODO.md` is a finding (low, but it is how work gets lost here).

---

## §B — General C# / Unity logic bugs

- **B1 — Unity fake-null.** `??`, `?.`, and `is null` bypass Unity's overloaded
  `==`, so a destroyed `UnityEngine.Object` reads as non-null. On MonoBehaviours
  and GameObjects use `== null` / `!= null`. This one is easy to miss and fails
  only after a destroy.
- **B2 — Null derefs on paths that only run once.** Singletons accessed in
  `Awake` before the owner's `Awake`, `NetworkManager.Singleton` during
  shutdown, `DefRegistry.Instance` before registry build, a dreamer looked up
  before spawn completes.
- **B3 — Off-by-one and bounds.** Slot indexing (slots are 0/1 — check both),
  the 4-slot buff payload in `DreamerBuffSync`, container capacity checks,
  variable-length payload cursors in `MapEntitySync` and `DreamerInventorySync`
  (a wrong length or offset here corrupts the read for everything after it).
- **B4 — Inverted or wrong condition.** `<` vs `<=` on thresholds
  (affliction onset, vitality, spoilage buckets, durability zero), `&&` vs `||`,
  a negation added to a condition whose body was not updated.
- **B5 — Wrong variable.** Copy-paste between the two dreamer slots, between
  hunger/thirst/warmth, between the two big sync files. Check that a loop body
  uses the loop variable and not an outer one.
- **B6 — Integer division and truncation.** Minutes-to-hours, percent
  calculations, `qty / stackSize`, labor accrual — `int/int` silently floors.
- **B7 — Float equality and accumulation.** `==` on floats, and repeated `+=` of
  a delta where the total should be derived from an absolute value.
- **B8 — Collection mutation while iterating.** Removing buffs, afflictions,
  instances, or queued actions inside a `foreach` over the same collection.
  Also: two systems holding a reference to the same list.
- **B9 — Unreachable / duplicated branches.** An `if` whose condition is already
  guaranteed, a `switch` case that can never match, an early `return` that
  strands the code after it, a second `if` that repeats the first.
- **B10 — Swallowed exceptions.** `catch { }` or `catch (Exception) { Debug.Log }`
  hiding a save or load failure. Save/load failures must be loud.
- **B11 — Coroutine and lifetime.** A coroutine that outlives its component, a
  `WaitForSeconds` loop with no exit, `StartCoroutine` on a disabled object,
  `async void` where an exception disappears.
- **B12 — Per-frame cost in `Update`.** `GetComponent`, `FindObjectOfType`, LINQ
  allocation, or a physics query per frame — a real bug in
  `InteractableDetector` and movement code specifically.
- **B13 — Serialization surface.** A private field expected to persist without
  `[SerializeField]`, a `[SerializeReference]` aspect list mutated in a way that
  loses the reference, an `enum` whose numeric values were reordered (breaks both
  saves and the wire).
- **B14 — Editor code in runtime.** `UnityEditor` usage outside
  `<G>/Editor` or an `#if UNITY_EDITOR` guard breaks the build.

---

## §C — Multiplayer correctness

- **C1 — Authority.** Match the CLAUDE.md table: dreamer position is
  owner-authoritative; NPC position, needs, vitality, afflictions, buffs, clock,
  incapacitation and dream flow are all host-authoritative. A client writing
  host-owned state, or the host overwriting a client's transform outside the
  deliberate `SnapToPositionRpc` authority-override, is a finding.
- **C2 — Host guards.** Simulation, save, checkpoint, revert, skip, and dream
  flow logic must be behind `IsServer` / `IsHost`. Look for a guard on the RPC
  but not on the direct-call path, or vice versa — both entry points need it.
- **C3 — RPC direction and permission.** `RequireOwnership` correctness on
  ServerRpcs, a ClientRpc sent from a client, an RPC that trusts a
  client-supplied slot id or item id without validating it against the sender's
  ownership.
- **C4 — Spawn/data ordering.** Client code that assumes a NetworkVariable holds
  real data in `OnNetworkSpawn`, or that a sibling NetworkObject already exists.
  The first value arrives after spawn. Handlers must tolerate the initial
  default value.
- **C5 — Late join and reconnect.** A client joining mid-session must converge
  from the synced state alone. Anything applied only via a one-shot RPC at
  session start is invisible to a late joiner.
- **C6 — `ForcePush()` after discontinuities.** Skip Time and revert jump state
  discontinuously; the periodic push is not enough. Every sync component whose
  state can change during a skip or revert needs an explicit force-push on that
  path — check `DreamerNeedsSync`, `DreamerBuffSync`, `DreamerTaskSync`,
  `WorldClockSync`, `DreamerInventorySync`, `MapEntitySync`.
- **C7 — Write volume.** A NetworkVariable written every frame, an RPC in
  `Update`, or a full-state payload resent on every small change. `MapEntitySync`
  and `DreamerInventorySync` are full-state payloads — confirm they are dirty-gated.
- **C8 — Race on shared world objects.** Two dreamers picking up the same
  instance, or gathering the same node, in the same tick. The host must resolve
  it; both clients must reconcile to the host's answer.
- **C9 — Client-side prediction drift.** The clock is extrapolated client-side;
  check that a host correction snaps rather than blends, and that client
  extrapolation never feeds back into authoritative state.
- **C10 — Solo path.** With one client, the second dreamer is AI-controlled but
  still a full dreamer. Verify the code path exists and does not depend on a
  second connection.

---

## §D — Determinism, save and revert

- **D1 — RNG as state (invariant 7).** Every stochastic system's seed and
  counter live in the saved authoritative state (`CraftRng`,
  `WorldState.craftCounter`). `System.Random` created ad hoc, `UnityEngine.Random`
  anywhere in the sim, or a seed derived from wall-clock time breaks revert
  reproducibility.
- **D2 — Round-trip completeness.** For any state field: is it written on save,
  read on load, and cleared or restored on revert? All three. §M8 covers
  `WorldState`; do the same reading for `DreamerRecord` and `MapEntityLayer`.
- **D3 — Revert is clear-then-rehydrate.** Revert clears live buffs, effects and
  world state, then rehydrates entirely from the checkpoint file. Any merge or
  reconcile of live state against the file is the bug class that was already
  fixed once (buffs surviving a rewind, the other dreamer keeping its state).
- **D4 — Checkpoint self-containment.** A checkpoint contains its own protection
  buff — the checkpoint id is assigned, then the buff granted, then the state
  captured. Order matters; check it.
- **D5 — Checkpoint retention.** Checkpoints are not deleted when a referencing
  buff expires. Validity is computed live from current protection buffs. On
  revert, checkpoints newer than the target world-clock time are discarded and
  older-or-equal kept.
- **D6 — Atomic writes (invariant 4).** Every save write is temp file →
  `File.Replace`. A direct `File.WriteAllText` to the live path is a finding.
- **D7 — Schema version.** A structural change to any saved type must bump
  `SaveSystem.CurrentSchemaVersion` and update the template file in the same
  change. A changed record shape with an unchanged constant means old saves load
  and silently misbehave instead of being rejected loudly.
- **D8 — Time anchoring.** Restored durations (`remainingMinutes`) are anchored
  to the rewound clock, not to real time and not to the pre-revert clock — a
  restored buff must not outlive the rewind.
- **D9 — Iteration order in anything saved or replayed.** Same as A3, applied to
  save writing and load application: a `Dictionary` enumeration order that
  differs between runs makes two identical reverts produce different files.

---

## Not findings

Do not report: naming, formatting, comment style, missing XML docs, "consider
extracting", micro-optimisations with no measured cost, or a rule violation in a
file explicitly marked legacy or obsolete in `CodeFiles.md`. Anything already
listed in `TODO.md` is noted as tracked, not reported as new.
