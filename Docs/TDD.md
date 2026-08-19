# Technical Design Document
## [PROJECT TITLE — TBD]
**Version:** 0.1 — Initial Draft
**Date:** 2026-06-03
**Status:** Work in Progress
**Companion to:** GDD.md (v0.1)
**Engine / Stack:** Unity 6, Netcode for GameObjects (NGO), co-op host-authoritative

---

## 0. About This Document

This is the Technical Design Document (TDD) for the game described in `GDD.md`. Where the GDD defines *what* the game is, this document defines *how* it is built.

### Working method
The TDD is a **living document**, built collaboratively one area at a time — discussing each system (or a cluster of interconnected systems), deciding the best approach, then recording the outcome here. It is expected to grow alongside the implementation, not to be complete up front.

This first revision covers:
- The **architectural foundations** that every later system depends on.
- **Phase 0** — the multiplayer and simulation foundation — broken into incremental, two-client-testable builds.

Later phases and per-system designs (needs, skills, crafting, combat, etc.) will be appended as they are designed.

### Core development principle
The game is built **multiplayer-first**. Every slice is validated with **two networked clients, each owning a dreamer**, before it is considered done. Solo play is treated as a subset of co-op (see §1.9), not a separate code path.

---

## 1. Architectural Foundations

### 1.1 The guiding constraint: total snapshot & revert

The dream/save system (GDD §18) requires that a **Wake Up reverts the entire world plus both dreamers to an earlier snapshot — totally and globally**. This single requirement dictates the architecture more than networking does: the complete authoritative game state must be **serializable and restorable at any instant**.

The same design that satisfies this also makes three other hard requirements fall out almost for free:
- **Skip Time** (GDD §5) — advancing state by a delta without real-time play.
- **Inactive-map resolution** (GDD §20) — computing what happened on an unattended map on re-entry.
- **Networking** (GDD §21) — host-authoritative state mirrored to the client.

All four want the same thing: a clean, authoritative, serializable simulation mutated through **commands/intents** rather than direct pokes.

### 1.2 The four-layer architecture

The codebase is split into four layers, enforced by separate Unity **assembly definitions** so the boundaries are guaranteed by the compiler, not by discipline:

| Layer | Responsibility | Unity dependency |
|---|---|---|
| **Simulation** | Authoritative mutable world state + systems that mutate it via commands. The single source of truth. | None where avoidable (plain C#) |
| **Presentation** | Rendering, animation, input, HUD. Reads sim state to draw; sends intents to it. Never mutates authoritative state directly. | Full (MonoBehaviours) |
| **Networking** | NGO transport, connection management, ownership, intent RPCs, state sync. | Full |
| **Persistence** | Serialization to disk; snapshots; load/revert. | Minimal |

The Simulation assembly not referencing rendering types is the mechanism that prevents local-mutation shortcuts (including by AI-assisted tooling) later.

### 1.3 The Runtime Data Manager (RDM)

The **Runtime Data Manager** is the host-side authoritative state store — the concrete realization of "one authoritative simulation state." It holds everything related to dreamers, NPCs, and the world, and is the single source for both saving to disk and syncing to clients.

**It is a state store, not a god object.** Its responsibilities are limited to:
- Holding the authoritative state tree.
- Serialization / snapshot.
- Sync orchestration.

The **systems** (needs, combat, crafting, etc.) are separate and operate *on* the state the RDM holds. Game logic does not live inside the RDM.

**It is reactive, not a background loop.** This game has no autonomous background simulation — time only advances via real-time tick or an explicit Skip (GDD §5). The RDM is therefore tick- and event-driven.

### 1.4 Authority model & networking topology

- **Host-authoritative listen server.** One unambiguous owner of truth (the host). NGO's distributed-authority mode is **not** used — save/revert integrity requires a single authority.
- **Custom-synced authoritative simulation.** The authoritative gameplay state lives in the host's RDM and is custom-serialized and synced to the client as a replica. The simulation is **not** forced into per-object NetworkVariables, because full-world snapshot/revert fights that model. Revert becomes "host loads snapshot, re-syncs."
- **NGO is used for what it is good at:** connection management, transport (LAN / Direct IP / Steam adapter), ownership, input RPCs, and high-frequency render-only sync (NetworkTransform / NetworkAnimator).
- Save data is stored locally on the **host**; the client requests state on connection (GDD §21).

### 1.5 Two data tiers

Data is split into two tiers with different homes. **Authority is per data category, not per entity** — a single dreamer is simultaneously client-authoritative for movement and host-authoritative for health.

| Tier | Examples | Home | Characteristics |
|---|---|---|---|
| **Render / feel** | Transform, animation | NGO (NetworkTransform / NetworkAnimator) | High-frequency, latency-sensitive, can be owner-authoritative. Does **not** stream into the RDM continuously. |
| **Authoritative state** | Needs, health/Vitality, inventory, skills, buffs, world objects, time | RDM (host) | Host-authoritative, lower-frequency or event-driven. This tier **is** the save. |

At save/snapshot time, the serializer **samples** the current transform from NGO into the snapshot — render-tier data is not duplicated into the RDM every frame.

### 1.6 Sync model — authority per data category

| Category | Authority | Mechanism | Frequency |
|---|---|---|---|
| **Position — dreamers** | Client (owner) | Owner-auth NetworkTransform | Continuous (NGO-managed) |
| **Position — NPCs / creatures** | Host | Server-auth NetworkTransform | Continuous (NGO-managed) |
| **Animation — cosmetic locomotion** | Client (dreamers) / host (rest) | Deduced from velocity locally; resynced occasionally | Low — periodic correction only |
| **Animation — gameplay events** | **Host** (the event) | Event sync; visual may play client-side | On event |
| **Needs** | Host | Computed by host; client extrapolates between syncs | Dreamers ~1s; NPCs batched/less often; **bulk push after a Skip resolves** |
| **Health / Vitality** | **Host (all entities)** | Event-driven | On change |

**Key rules:**
- A dreamer owns its **live movement**, but the host owns its **health and death** — because a Vitality drop triggers incapacitation and the dream-save flow, which only the host can adjudicate.
- Any animation that carries a **gameplay outcome** (the frame a spear releases, an axe connects) is host-authoritative on the *event*; a client-authoritative animation must never adjudicate damage.
- Needs drain is deterministic, so the owning client can smoothly extrapolate the HUD between host syncs and correct on each update.

### 1.7 The elapsed-time resolver

A single component that advances world state forward by a delta (need drain, NPC task production/consumption, durability decay) **without real-time ticking**. It is invoked by:
- **Skip Time** (GDD §5)
- **The Scheduler** (GDD §5, future)
- **Inactive-map re-entry** (GDD §20)

Real-time play is the same logic invoked incrementally each tick. **Invariant:** ticking forward N seconds one step at a time must equal resolving N seconds in a single jump. This is both the primary correctness risk and a strong automated-test target.

*(Implemented in Cluster 1, not Phase 0.)*

### 1.8 Save, snapshot & revert

- **One serializer, one target: disk.** All persisted state — disk saves *and* dream checkpoints — goes through the same `ISaveSerializer`; there is never a second serializer and no separate in-memory snapshot path. A dream checkpoint (sleep, consumable, etc.) *is* a save: the same (world + 2 dreamer) write, differentiated only by **type metadata and retention rules**, not by serialization. This is also why checkpoints can double as crash-recovery resume points (§21) — they persist.
- **Versioned schema.** Every file carries a schema version; the format will change frequently during development, and the version field manages that (first bump exercised at 0.0.4).
- **File structure** (aligns with §21): one **world file** + two **dreamer files** per save. Checkpoints live in a checkpoint store as a set of typed, expiring saves; Save-and-Exit and Disconnect are singleton slots.
- **New Game = a template save file.** Starting a new game loads a pre-authored save (map + spawn positions + base state) and overlays character-creation edits, so the **load path is built and tested from the first build** and disk Save is the only genuinely new code later.
- **Revert = load a checkpoint through the apply path.** Restoring repopulates the RDM and applies state to the live world (Build 0.0.3's apply path): host-authoritative fields restored directly, the clock restored-and-resynced, and client-authoritative dreamer transforms snapped back via the **authority-override** (host instructs each owner to teleport — built in 0.0.3). Disk Load and Wake Up are the same operation; the chosen checkpoint file is the only variable.
- **Atomic writes.** Because checkpoints double as crash recovery, every save write is atomic (temp file → rename) so a crash mid-write cannot corrupt a checkpoint.

### 1.9 Solo as a subset of co-op

There are always exactly **two dreamers** in the simulation, regardless of player count (GDD §7, §18, §21). Solo play is co-op with the second human absent: the host runs alone and the second dreamer is AI-controlled while retaining its dreamer status and save protection. Building co-op-first and deriving solo from it is the correct reading of the design, not an add-on.

### 1.10 Technology choices (Unity 6)

- **Netcode for GameObjects (NGO)**, host-authoritative listen server.
- **Transports:** Unity Transport for LAN / Direct IP; a Steam transport adapter for friend invites (no public matchmaking — GDD §21).
- **No DOTS/ECS.** Scale is small (8–10 agents, at most 2 active maps); classic MonoBehaviours for presentation + a plain-C# simulation layer is the right fit.
- **ScriptableObjects for static definitions** (recipes, skill XP tables, affliction defs, animal/weather data). **Plain serializable C# objects (not ScriptableObjects) for runtime mutable state** — SOs are assets and do not snapshot/revert cleanly.
- **Additive scene loading per map**, with each map's state persisting in the simulation store when its scene unloads.
- **Testing:** Multiplayer Play Mode for running two virtual clients in one editor.

> *Exact package names and versions are pinned during Build 0.0.0 setup and recorded there; the choices above are at the architectural level.*

### 1.11 World entity state — the Map Entity Layer

All authoritative world state that is positional, belongs to a specific map, persists through
save/revert, and is only relevant to players on that map lives in a **Map Entity Layer**. It
generalises the world-items collection from 0.2.1; from 0.2.7a it is the single home for every
positional map entity.

**What it holds.** §1.12 world-object Instances — items, harvest nodes, processables, animals,
buildings, placed items are all the same Instance model, not separate record types — organised by
**sync profile** rather than by kind:
- **Authored** (delta-only): placed in the scene, position read locally, only the mutable delta
  synced by baked id. O(touched), not O(total).
- **Runtime** (full-state): spawned / dropped / carried-then-dropped, full Instance synced.

Named groupings (items / nodes / animals / buildings) stay useful for organisation and per-category
sync batching, but they are all the one §1.12 model.

**Not in the layer:** authored static geometry (terrain, props, *authored* tree/rock positions —
the map asset, not session state); dreamers (RDM / DreamerRecord).

**Invariants:**
1. **Map-scoped.** Keyed by `mapId`; the RDM holds one layer per loaded map. Switching maps
   does not unload another map's layer — it stays in the RDM and on disk, inactive.
2. **Per-map sync.** Only clients currently on a map receive its updates. A player on map_02
   dropping an item produces no traffic for a player on map_01.
3. **Static nodes — delta only.** Harvest-node authored positions load locally from the map
   asset (identical on all clients, never networked); only mutable state (depleted ids, regen
   timers) is stored and synced. Save and wire cost are O(depleted), not O(total nodes).
4. **Save/revert.** Serialises to `map_{id}.json` alongside `world.json`; revert clears and
   rehydrates it exactly as DreamerRecords are.
5. **Wire format.** Variable-length per sub-collection (the upgraded world-items payload
   pattern); animals and placed items get richer per-entity payloads when those systems land.

**Where state lives, overall:** `world.json` = global non-positional state (time, clock,
weather); `map_{id}.json` = each map's entity layer; DreamerRecords = the dreamers; the map
asset = authored geometry.

**Build phasing.** Introduced at **0.2.7a** (before harvest nodes add the second entity type),
but the map-scoped *runtime* can only be validated once a second map exists at Cross-map
(Cluster 6):
- *0.2.7a:* build the unified layer, its sub-collections, the `mapId` seam, and the migration
  off `WorldItemsSync` — against the single current map.
- *Cross-map (Cluster 6):* activate the multi-map runtime — per-map sync isolation, multiple
  live layers in the RDM, inactive layers persisting on disk — where a real second map
  validates invariants 1–2.

**Migration from `WorldItemsSync` (at 0.2.7a):** it becomes the `groundItems` sub-system;
`WorldState.worldItems` and `nextWorldItemId` move into `MapEntityLayer.groundItems`; the
`WorldItemsSync` NetworkBehaviour is replaced by `MapEntitySync` (one per loaded map); schema
bump, old saves rejected as usual.

### 1.12 Interactable world objects: the Def / Instance / Action model

Everything in the world a dreamer can touch — a dropped item, an authored tree, a spawned fallen
tree, a placed worktable — is one kind of thing: an **Instance** of a **Def**, on which a set of
**Actions** can be performed. This replaces the previously separate ItemDef and GatherableObjectDef
(and the parallel ItemInstance / ProcessableInstance / ground-item records). §1.11 is *where* world
objects live and how they sync; this section is *what* they are.

**The Def (authored SO) is composed, not wide.** Instead of one Def carrying every field for every
kind of thing, it holds basic info plus two open lists:

​```
Def
├─ defId, name, prefab, baseWeight        — always present
├─ aspects[]   — embedded [Serializable] data; only what this thing IS
│    Inventory   (stack rule, item shape, weight) → may sit in a container
│    Consumable  (nutrition, the gradual-buff window)
│    Perishable  (lifespan → the spoilage triple, §5.2)
│    Tool        (category, tier, RNG stat ranges, §5.7)
│    Container   (capacity, filter, spoilage modifier, §5.3)
│    …new aspects added without touching the rest
└─ actions[]   — references to shared ActionDef SOs; what you can DO to it
     Pickup, Consume, Chop, Fell, Debranch, Carry, Craft-with …
​```

Aspects are embedded per-Def data (no asset sprawl); actions are shared SO assets referenced by id
(one "Chop" reused across many Defs — the "ids not strings" rule). What a thing *is* = its aspects;
what you can do to it = its actions. An item may hold several aspects at once (a toolbelt is Tool +
Container). The per-system designs in §5.1 (items) and §5.6 (gatherables) become *descriptions of
specific aspects and actions* on this one Def.

**Actions are outcome- and context-typed.** Each ActionDef carries an **outcome** — `ToInventory`
(pickup → move into a container), `Transform` (chop → consume the instance and spawn its yields),
`Carry` (→ CarriedBy), `Consume` (apply effect, decrement) — and a **context**: *world* actions
(shown when the instance is in the world: pickup, chop, carry) vs *inventory* actions (shown when
it's in a container: consume, equip, drop). One list, filtered by where the instance currently is.
Timed actions (chop, fell, craft) run on the §5.5/§5.6 accrual engine unchanged; instantaneous ones
(pickup, consume) are immediate outcomes on the same machinery.

**The Instance carries a location.** One Instance type replaces ItemInstance, ProcessableInstance,
and the ground-item record:

​```
Instance
├─ defId
├─ per-instance data   — quantity, condition triple, durability, rolled tool stats
└─ location            InContainer(containerId) | InWorld(position, [accrual]) | CarriedBy(dreamerId)
​```

The location is the only thing pickup / drop / carry change; the Def's actions are always present
because they live on the Def, not on whichever system holds the instance. This removes the whole
class of "a dropped object loses its interactions" bug: a carried-then-dropped log lands `InWorld`
and is still choppable, because it was never converted into a different kind of object — only its
location flipped.

**The world surface.** A world prefab carries a collider + one Interactable component holding the
Def reference; a raycast/collider hit reads it and presents the Def's world-context actions — the
single configurable component on many objects. The visual follows the instance's location
(view-follows-state).

**Storage collapses to one model, two sync profiles (§1.11).** The previously separate
groundItems / processables / harvestNodes are one world-object model; only *position sourcing*
branches, by origin:
- **Authored** (delta-only) — placed in a scene; identity + position read locally (the Interactable
  self-registers its baked id, Def, and local position at load), only the mutable delta synced by
  id. Pristine authored objects cost zero traffic.
- **Runtime** (full-state) — spawned, dropped, or carried-then-dropped; the full Instance is synced
  because no client could otherwise know the position.

The model is single; the authored/runtime split is purely how a position is obtained.

---

## 2. Phase 0 — Multiplayer & Simulation Foundation

### 2.1 Goal & exit criteria

**Goal:** a networked, host-authoritative, snapshot-able skeleton with two dreamers — proven end to end, with essentially no gameplay. Everything in later phases lands on top of this.

**Exit criteria:** two clients connect, each owns a dreamer, both move around a map loaded from disk, the host holds all authoritative state in the RDM, the world can save/load to disk and snapshot/revert in memory, and the clock runs and syncs. When this is true, the spine is validated and Cluster 1 can begin.

Each build below is **independently testable with two clients** and adds exactly one new architectural risk.

### 2.2 Builds

#### 0.0.0 — Scaffolding & plumbing smoke test

**Goal:** stand up the project, packages, and the four-layer structure, and prove two clients can connect over loopback. No rendering, no gameplay — this build only proves the plumbing exists.

**Packages (pinned at this build):**

| Package | ID | Version / Source | Notes |
|---|---|---|---|
| Netcode for GameObjects | `com.unity.netcode.gameobjects` | 2.x (latest stable) | NGO 1.x deprecated from editor 6000.3; v2.x required on Unity 6. |
| Unity Transport | `com.unity.transport` | Pulled in with NGO | Low-level transport for LAN / Direct IP / loopback. |
| Multiplayer Play Mode | `com.unity.multiplayer.playmode` | 1.6.x | Up to 4 players (Main + 3 virtual) in one editor. Two-client test tool. |
| Multiplayer Tools | `com.unity.multiplayer.tools` | latest | Optional — network profiler/stats for later. |
| Facepunch Steam transport | `com.community.netcode.transport.facepunch` | git URL (community) | **Deferred** — see T6. |

**Tasks:**

| ID | Task | Executor | Done when |
|---|---|---|---|
| T1 | Create Unity 6 LTS (6000.x) project; choose render pipeline (URP default). | Manual (Hub) | Empty project opens on Unity 6 LTS. |
| T2 | Add packages via `Packages/manifest.json` (NGO 2.x, Unity Transport, MPPM; Multiplayer Tools optional). | Claude Code | Editor resolves all packages, no errors. |
| T3 | Create the four-layer folders + `.asmdef` files (Simulation, Presentation, Networking, Persistence). | Claude Code | Project compiles; **Simulation references no rendering / Unity-engine assemblies**. |
| T4 | Write `ConnectionBootstrap` (Networking layer): `StartHost()`, `StartClient()`, and a host-side `OnClientConnectedCallback` hook logging each client ID. Wire the NetworkManager GameObject + transport + bootstrap into a scene. | Split — script: Claude Code; scene/component wiring: Manual (Editor) | One instance starts as host, another as client; host logs the join. |
| T5 | Enable one Virtual Player in MPPM (Window > Multiplayer > Multiplayer Play Mode); press Play. | Manual (Editor) | See build acceptance. |
| T6 | Steam (Facepunch) transport + `steam_appid.txt` (480 for testing). | Deferred / optional | Only when friend invites are needed; re-verify NGO v2 compatibility then. |

**Reference edges (T3):** Presentation → Simulation; Networking → Simulation (+ NGO); Persistence → Simulation. The "Simulation has no Unity-rendering dependency" rule is the compiler-enforced wall against local-mutation shortcuts. (Keeping Simulation *fully* Unity-free — avoiding `Vector3` etc. — is deferred until Cluster 1 introduces real sim types.)

**Why the executor split:** Claude Code edits files (`manifest.json`, `.asmdef`, C# scripts) but cannot operate the Unity Editor GUI. Project creation, component/inspector wiring, and MPPM are manual Editor steps.

**Build acceptance:** with two MPPM players running, the host console logs **two distinct client IDs** (its own + the joiner). Nothing renders.

**Steam deferral rationale:** MPPM tests over loopback with Unity Transport, so the Steam transport adds nothing to the smoke test and carries community-maintenance risk; it is not on the Phase 0 critical path.

#### 0.0.1 — Two dreamers in a loaded world, seeing each other
Flow: splash → main menu → host/join (NGO bootstrap) → shared character-creation scene (skin/hair, synced on confirm) → ready/start → host loads the template-save file (map + authored spawn positions) → action phase, both spawned correctly and mutually visible.
**Introduces:** the RDM skeleton holding two dreamer records; the player→dreamer **ownership mapping** (required by 0.0.2); networked scene management; the **New Game = template save file** approach (load path built here).
**Implementation tactic:** build the networked core first behind a debug instant-host/join (two clients, shared scene from a file, mutual visibility), prove it, then wrap the menu chrome around it.
**Acceptance (2-client):** both players connect, each edits only their own dreamer, on confirm both see both appearances, host loads the template, both spawn at authored positions and see each other correctly placed.

0.0.1a — Networked core (single scene, debug connect)
IDTaskExecutorDone whenA1Define the Simulation state model: WorldState {schemaVersion, mapId}, DreamerRecord {slot, position, appearance}, AppearanceData {skinTone, hairStyle, hairColor}, and a Float3 value type. Pure C#, [Serializable], in Simulation.Claude CodeCompiles in Simulation with the wall intact.A2Float3 ↔ UnityEngine.Vector3 conversion helpers.Claude Code (engine-ref layer)Round-trips correctly.A3Author the template (new-game) JSON in StreamingAssets (one world file + two dreamer files, distinct spawn positions, default appearance) + the Persistence loader that reads them into the state model.Claude CodeLoader returns a populated WorldState with two dreamers at authored positions.A4RDM skeleton: host-side manager holding the WorldState, populated from A3 on start; queryable (GetDreamer(slot), ownership map).Claude Code (Networking)On host start, RDM holds the loaded world + two dreamer records.A5Dreamer prefab (NetworkObject + placeholder capsule + NetworkTransform) and its adapter NetworkBehaviour (slot, ownership, appearance hook). Register in NetworkManager's NetworkPrefabs.Split — scripts: Claude Code; prefab/mesh/registration: ManualPrefab exists, registered, compiles.A6Spawner + ownership mapping: host spawns slot 0 (host-owned) and, on client connect, slot 1 (client-owned), each at its authored position; record slot→clientId in the RDM.Claude Code (Networking)Two dreamers, each owned by the correct client, at correct positions.A7Debug instant-connect: MPPM-tag-driven auto host/client that runs load→RDM→spawn in one action scene. No menus, no scene transitions.Split — logic: Claude Code; MPPM tags: ManualPressing Play with two MPPM players lands both in the action scene.A8Presentation: render the placeholder dreamers at their positions on both clients.Split — script: Claude Code; prefab visuals: ManualBoth clients see two dreamers at the two authored positions.
Key decisions in A:

A1 is where the Simulation wall forces a non-Unity vector type — hence Float3 in Simulation with conversion helpers (A2) in an engine-referencing layer. This is the deferred math-type decision, arriving exactly here.
A5/A6: do NOT use NGO's auto Player Prefab. One-player-object-per-client gives the wrong count in solo (1 client, but always 2 dreamers). Spawn the two dreamers explicitly from the template and assign ownership yourself.
A3 is the payoff of "New Game = a save file": this loader and format are what 0.0.3's real Save/Load reuse.

0.0.1a acceptance: two MPPM clients auto-connect into one scene; host loads the template; two dreamers spawn at authored positions with correct ownership; both clients see both, correctly placed.
0.0.1b — Character creation sync
IDTaskExecutorDone whenB1Networking-layer INetworkSerializable DTO mapping to/from the pure AppearanceData POCO.Claude CodeAppearance can sync without Simulation referencing NGO.B2Host-written (server-auth) NetworkVariable on the dreamer adapter holding the appearance DTO; mirrored into the RDM record.Claude Code (Networking)Setting it on host propagates to clients.B3Minimal creation UI (skin tone, hair style, hair color) that edits only the player's own dreamer, with a Confirm button.Split — UI: Manual; logic: Claude CodeEach player can change their dreamer's three fields locally.B4Confirm → owning client sends choice via ServerRpc → host validates, writes RDM + sets NetworkVariable.Claude CodeAfter both confirm, both clients see both dreamers updated.B5Presentation applies appearance: skin tone (material), hair style (placeholder mesh/variant), hair color (material).Split — script: Claude Code; hair meshes/materials: ManualAppearance changes render on both clients.
Key decision in B: keep AppearanceData a pure Simulation POCO; the INetworkSerializable DTO lives in Networking (the wall again). Syncing appearance via a per-entity NetworkVariable is fine here — it's static-after-creation entity data, distinct from the bulk mutable state that goes through the custom RDM sync later.
0.0.1b acceptance: each player edits only their own dreamer; on confirm both clients see both with correct skin/hair; appearance is stored in the RDM. (Testable in a standalone creation scene before the full flow exists.)
0.0.1c — Scene flow + menu chrome
IDTaskExecutorDone whenC1Create scenes (splash, main menu, host/join, character creation, action) and add to build settings.Manual + Claude Code (controllers)Scenes exist and build-settings-registered.C2Local pre-network flow: splash → main menu → host/join, invoking the 0.0.0 bootstrap.Split — UI: Manual; logic: Claude CodeA player can reach host/join and start hosting or joining.C3Networked scene management: host drives the transition into the action scene via NGO's NetworkSceneManager; client follows; spawning (A6) now fires after the synced load.Claude Code (Networking) + Manual (build settings)Host loads the action scene, client follows, both with dreamers spawned.C4Wire the full happy path: host/join → shared creation → client Ready → host Start enables → action.Split — UI: Manual; logic: Claude CodeFull flow runs end to end with two clients.C5Keep the A7 debug instant-connect behind a dev flag; make the real flow the default.Claude CodeBoth paths coexist; real flow default.
Key decision in C: C3 (networked scene transitions) carries real risk — NetworkObjects surviving a synchronized scene load — which is exactly why it's placed after the core spawns work, not bundled into A.
0.0.1c acceptance = the full 0.0.1 acceptance: splash → menu → host/join → shared creation (each edits own dreamer) → ready/start → action scene, both spawned at authored positions, mutually visible, with correct appearances.

#### 0.0.2 — Client-authoritative movement, host-readable
Owner-authoritative NetworkTransform for dreamers; movement replicates to host and the other client automatically.
**Key criterion (sets up 0.0.3):** the RDM can *read* each dreamer's live position on demand — no per-frame write channel into the RDM.
**Acceptance:** each client moves only its own dreamer, movement is smooth on the other client, and the host can query both dreamers' current positions through the RDM.

#### 0.0.3 — Save & load round-trip

**Goal:** build the single serializer that disk-save, snapshot, and revert all share, and prove that loading fully overwrites live runtime state for both clients — the dress rehearsal for Wake Up. Load is available from the main menu (cold) and from an in-action pause menu (warm); the warm path is the disk-backed sibling of 0.0.5's revert.

**Tasks:**

| ID | Task | Executor | Done when |
|---|---|---|---|
| D1 | Define `ISaveSerializer` in Persistence (`Serialize`/`Deserialize` over the state types), backed by Newtonsoft JSON. The single serializer reused for disk save now and the in-memory snapshot at 0.0.5. | Claude Code (+ manual package add if needed) | A `WorldState` + 2 `DreamerRecord`s round-trip in memory (serialize → deserialize → equal). |
| D2 | Save writer: write current RDM state to `Application.persistentDataPath/Saves/<slot>/` as `world.json` + `dreamer_0.json` + `dreamer_1.json`, schemaVersion stamped. | Claude Code (Persistence) | A save call writes three well-formed files to persistentDataPath. |
| D3 | Sample live position at save time: for each dreamer, read its current NetworkTransform position (read path proven in 0.0.2) into `DreamerRecord.position` before serializing. | Claude Code (host save flow) | Saving after moving a dreamer records the moved position, not the spawn position. |
| D4 | Path-parameterize the A3 loader to read from a save directory as well as the template; deserialize the three files → populate the RDM. | Claude Code (Persistence) | The loader populates the RDM from a save dir identically to reading the template. |
| D5 | Apply-state path (shared by both load entry points): repopulate RDM from save (D4) → ensure the correct scene/map is active → if dreamers don't exist (cold), spawn them with ownership re-derived by slot; if they exist (warm), reuse in place → position every dreamer via the **authority-override** (host snaps its own directly; instructs each remote owner via ClientRpc to snap; NetworkTransform teleport, no interpolation) → reapply host-authoritative fields (appearance). | Claude Code (Networking) | Applying a save brings both clients to the saved state — fresh or in place — **client-owned dreamer at its saved position**. |
| D6 | Schema-version check on load: verify `schemaVersion`, fail loudly on mismatch (dev behavior). | Claude Code (Persistence) | A file with an unexpected version is rejected with a clear error. |
| D7 | Load entry points: "Load Game" on the **main menu** (cold — spins up the action scene, spawns dreamers, no character creation) AND in an **in-action pause menu** (warm — applies in place to the live session). Both host-only; client follows/receives the applied state. Remove any Load button from character creation. | Split — UI: Manual; logic: Claude Code | Load is reachable from the main menu and the action-phase pause menu, never from creation; both land both clients at the saved state. |

**Key decisions:**
- **D1 — pick the backbone serializer now.** Use Newtonsoft (`com.unity.nuget.newtonsoft-json`, possibly already present transitively) over `JsonUtility`; the state tree will grow into collections, dictionaries, and polymorphic types `JsonUtility` can't handle. Keep it behind the `ISaveSerializer` interface so a later swap to a binary format stays localized.
- **D2 — saves go to `persistentDataPath`, not `StreamingAssets`.** StreamingAssets is read-only at runtime and holds the *template*; real saves write to the host's persistent path (§21). One fixed slot for now.
- **D5 — load requires the authority-override; respawning does NOT sidestep it.** Dreamer position is client-authoritative, so the host cannot dictate a client-owned dreamer's transform — the owner must apply it (host instructs via ClientRpc to snap). Persist the **slot**, not the transient `clientId`. This same apply path serves both load entry points and is the exact mechanism 0.0.5's Wake Up revert reuses.
- **D7 — Load = Wake Up minus the dream rules.** Disk Load and in-memory revert are the same "apply serialized state to the world" operation; `ISaveSerializer` abstracts the source. **Cold** (main menu) spawns dreamers then applies; **warm** (action-phase pause menu) applies in place. Both host-only; never an in-creation action. (Map-change-on-load and the paused dark-overlay UX come with multiple maps and the clock at 0.0.4 — for now warm load is same-map and effectively instant.)

**Build acceptance:** from the main menu, New Game and Load both land both clients in the action scene (authored vs saved positions). In a live session, Load from the pause menu snaps both clients — client-owned dreamer included — to the saved state in place, without leaving the action scene. Save → move both dreamers → Load → snaps back; quit → relaunch → Load → identical state.

#### 0.0.4 — Authoritative clock

**Goal:** the first time the world advances on its own over real time — the host's authoritative real-time tick, which needs, durability, and the elapsed-time resolver all plug into in Cluster 1. Host-driven clock at 6s = 1 in-game minute, synced to the client, with a visible readout and pause/resume hooks for later skip and revert. Also the first deliberate schema-version bump.

**Tasks:**

| ID | Task | Executor | Done when |
|---|---|---|---|
| E1 | Add a `WorldClock` to `WorldState` (Simulation POCO): in-game time, ratio (`realSecondsPerInGameMinute`, default 6), `isPaused`. Pure-C# `Advance(realSeconds)` + a derive-to-`(day, hour, minute)` helper. No Unity dep. | Claude Code (Simulation) | Advancing by real seconds yields correct in-game time; small-step ticking equals one big jump; day/hour/minute derives correctly. |
| E2 | Host clock driver: an engine-layer host updater that each frame feeds the real delta (`Time.deltaTime`) into `WorldClock.Advance` on the RDM's clock while running. Action phase only. | Claude Code (Networking/host loop) | On the host, the clock advances at 6s = 1 in-game minute while in the action phase. |
| E3 | Clock sync: host-authoritative value pushed to the client periodically and on pause/resume; client **extrapolates** locally between syncs using NGO's synchronized network time as the shared real-time reference, correcting on each sync. | Claude Code (Networking) | Both clients show the same in-game time advancing at the same rate, no visible drift over minutes. |
| E4 | Pause/resume hooks: authoritative `isPaused` (host sets, synced; client stops/resumes extrapolating). No skip yet — just the freeze primitive. | Claude Code (Networking/Simulation) | Host pause freezes the clock on both clients; resume continues from the frozen time. |
| E5 | Time-of-day readout: Presentation HUD showing `Day N — HH:MM` from the synced clock. No gameplay effects (no lighting, no task gating). | Split — UI: Manual; logic: Claude Code | Both clients show a live, matching readout. |
| E6 | Schema-version bump: add `clock` to the world file **and the template**, increment `schemaVersion`. The clock now rides D2 save / D4 load automatically. | Claude Code (Persistence + template) | A new save round-trips the clock (save → load → same time); a pre-bump save is rejected with a clear version error. |

**Key decisions:**
- **E1 — the world clock is gameplay state, not NGO's `NetworkTime`.** It has its own ratio, can be paused/skipped/reverted, and lives in the RDM. NGO's network time is used only in E3 as the shared real-time *reference* for extrapolation — never as the clock itself. The advance math stays pure in Simulation while the `Time.deltaTime` source sits at the engine boundary (same pattern as `Float3`).
- **E1/E2 — first instance of the tick→advance-by-delta pattern**, the embryo of the elapsed-time resolver (§1.7). For a linear clock the "small steps == one jump" invariant is trivially true; it becomes the real correctness target in Cluster 1 when non-linear state (needs) advances the same way. Write the advance as a clean `Advance(delta)` now so the resolver generalizes it later.
- **E3 — extrapolation, not per-frame sync** (same philosophy as needs): periodic value + correction, client ticks locally. A small `NetworkVariable` on a clock object is a fine sync vehicle for now, like appearance; the bulk custom RDM state-sync is still a later concern.
- **E6 — the deliberate first version bump.** This exercises D6: during dev, a bump simply invalidates old saves (reject loudly); migration is deferred. Proving the version gate fires when the format changes is the value, since the format will change every build.
- The clock **runs in the action phase only** — paused/not-ticking in menus and character creation, so time doesn't drain during setup.

**Build acceptance:** both clients show the same in-game time advancing at the same rate; host pause/resume freezes and continues on both; save → load restores the clock; a pre-bump save is rejected by the version check.

#### 0.0.5 — Disk checkpoint & revert (the spine proof)

**Goal:** prove the full Wake-Up mechanism end to end — capture a complete checkpoint to disk and restore it to both clients via the apply path. Because the authority-override and in-place apply path were built in 0.0.3, this build adds almost no new machinery; it is the spine proof, not a new system. Snapshots are disk checkpoints (a checkpoint *is* a save), never in-memory — they must persist to double as crash recovery (§21).

**Tasks:**

| ID | Task | Executor | Done when |
|---|---|---|---|
| F1 | Take checkpoint: sample live dreamer positions (reuse D3) and write a save via D2 to a dedicated checkpoint slot (distinct from Save-and-Exit), via an atomic write (temp → rename). Dev trigger. | Claude Code (host) | A checkpoint is written atomically to its slot using the same writer/format as a regular save. |
| F2 | Revert: load the checkpoint via D4 and feed it through the D5 warm-apply path — in-place reposition via the authority-override, host-auth fields restored, clock restored-and-resynced. Dev trigger. | Claude Code (Networking) | Revert restores both clients to the checkpoint exactly — client-owned dreamer position (via override) and the clock — then play resumes from there. |

**Key decisions:**
- **Disk, not memory.** Checkpoints are saves; they persist so they can double as crash-recovery resume points (§21). Reverts and checkpoints are infrequent (death / sleep), so disk I/O cost is irrelevant.
- **No new machinery.** F1 reuses the D2 writer + D3 sampling; F2 reuses the D4 loader + D5 apply path + the authority-override. Revert ≡ warm load with a checkpoint as its source. 0.0.5 is validation, not new systems.
- **Single checkpoint only.** Personal vs shared coverage, protection-buff durations, multiple simultaneous checkpoints, the revert vote, and Nightmares are all **Cluster 1** (the dream rules) — they need sleep triggers, death, and the vote to validate against, none of which exist yet. The versioned format makes adding their metadata later a clean bump, not a refactor.
- **Atomic writes** (temp → rename) on every checkpoint, since checkpoints double as crash recovery.

**What's deliberately NOT here (Cluster 1):** what *triggers* a checkpoint (sleep, consumable), the personal/shared distinction, protection-buff durations, the co-op revert vote, Nightmares, and the paused-vote UX. 0.0.5 is the bare mechanism.

**Build acceptance:** take a checkpoint → move both dreamers and let the clock advance → revert → both clients snap back to the captured positions (client-owned included) and the captured time, then continue ticking from there. **Phase 0 complete.**

### 2.3 Explicitly deferred out of Phase 0

To prevent scope creep, the following are **not** in Phase 0: needs, energy, tasks, NPCs, combat, the elapsed-time resolver, full character creation (attributes / body type), and the disconnect/reconnect and Steam-connection polish. All of that is Cluster 1 and later.

---

## 3. What Comes Next

**Cluster 1 — Core spine (on top of Phase 0):** Time & Energy (GDD §5), Needs (GDD §4), and the Dream rules (GDD §18 — what triggers snapshots, protection buffs, the revert vote, Nightmares), plus the elapsed-time resolver (§1.7). These three GDD systems are inseparable: needs drain over time, Skip resolves needs, and saves snapshot all of it.

## 3. Roadmap — Clusters

Phase 0 (spine) and **Cluster 1 — Core Spine: Time, Needs & Dreams** are complete (§4).
The rest is grouped into the clusters below, in build order. Order is dependency-driven,
chosen playability-first — the hands-on survival loop early, environmental and macro
systems later. Clusters 2–4 are firm by dependency; from 5 on the order is adjustable.

| # | Cluster | GDD | Adds / Cluster-1 deferral it homes |
|---|---|---|---|
| 2 | **Survival Actions** | §10, §13, §17, §11, §12 | Inventory, water, food, durability, crafting — the gather/carry/craft/consume loop. Makes the 0.1.1 placeholder meal/drink tasks real (food/water → needs). Homes the **save-consumable item wrapper**. |
| 3 | **Wildlife, Combat & Harvesting** | §15, §16 | Wildlife, real combat, harvesting. First stochastic system → homes **RNG-as-state**; predators → the first **real skip interrupt**; engaged entities → the **fidelity gradient** + flee-during-skip; the deferred **combat detail**. |
| 4 | **Structures & Building** | §14 | Shelter (sleep locations immediately; warmth/weather protection once Weather lands), storage (food-decay protection), camp, build quality + decay. |
| 5 | **World & Weather** | §2 | Temperature states, weather events, weather → needs (wetness, hypothermia, frostbite into the affliction pipeline), the blizzard interrupt. Single-map; the temperature *cycle* feeds Migration. |
| 6 | **Cross-Map** | §20 | The multi-map foundation — a couple of maps, dreamers on different ones, travel between them. **Extends the save/checkpoint spine** to cover multiple maps + which map each dreamer is on; inactive maps fast-forward via the §1.7 resolver on re-entry. |
| 7 | **Tribe & NPCs** | §7, §9 | NPC tribespeople on the tick + skill progression. Homes the **NPC-guard warnings during skip** and the **full Scheduler** (routines over a horizon). |
| 8 | **Migration** | §19 | The 6-map temperature-driven relocation of the tribe, on Cross-Map's infra, moving the Tribe from C7. |
| 9 | **Tech, Artifacts & Quests** | §8, §22, §23 | Dream-the-future tech discovery, the artifact reveal arc, tiered collective tribe goals. The progression + story capstone. |

**Cross-cutting (slot in anytime, not clusters):** §21 save hardening (retention/pruning,
crash-recovery loadability, disconnect/reconnect); co-op polish (shared-sleep rework,
skip-start consensus vote — reuses 0.1.7's consensus component).

Within-cluster groupings (Skills with Tribe; Combat/Harvesting with Wildlife; Tech +
Narrative + Quests together) are roadmap convenience and will split into builds when each
cluster is scoped.

## 4. Cluster 1 — Core Spine: Time, Needs & Dreams (Design)

This details the Cluster 1 design previewed in §3. It is a design pass — decisions, not yet builds; the build breakdown is appended after, as in Phase 0. It lands directly on the Phase 0 spine (the clock from 0.0.4, the checkpoint/revert from 0.0.5).

### 4.0 Scope & coupling

Three GDD systems that cannot be designed apart: **Time & Energy** (GDD §5), **Needs** (GDD §4), and the **Dream rules** (GDD §18) — bound together by the **elapsed-time resolver** (§1.7). They are inseparable because needs drain over time, Skip resolves needs across a delta, and the dream rules sit on the checkpoint/revert mechanism. Two of the three already have foundations: the dream rules wire onto 0.0.5, and the resolver generalizes 0.0.4's `Advance(delta)`.

### 4.1 The resolver — fixed-step tick

**Decision: a fixed-step resolver, not event-segmented analytical.** The hard requirement (§1.7) is *tick == jump*: playing an interval in real time must equal skipping it. A fixed step makes that true *by construction* rather than by careful integration, which is the right trade for an incrementally built, Claude-Code-implemented project where "easy to keep correct" outweighs efficiency.

- **Canonical step = 1 in-game minute**, aligned with the 0.0.4 clock (1 IG minute per 6 real seconds). The clock tick *is* the resolver's heartbeat.
- **One resolver everywhere** — real-time, Skip, Scheduler, inactive-map re-entry all call the same `Step`. tick == jump holds because there is no second code path to diverge.
- **Real-time** buffers frame deltas and fires one `Step` per IG minute (never per render frame), so need drain never depends on framerate.
- Minute granularity suffices because the only things needing sub-minute precision (combat, a ~90s bleed) occur **only in real time** — Skip is blocked in danger — so the resolver's domain is the slow stuff (need drain, tasks, durability, production).

The event-segmented analytical alternative was considered and rejected: efficient but it makes tick==jump a property to actively maintain (every system must register/order events correctly), inviting subtle divergence bugs. Fixed-step's "wasteful" looping costs microseconds at this scale.

### 4.2 Skip execution

A Skip is simply **N ticks run in a loop**, chunked across frames.

- **Budget by milliseconds per frame, not a fixed tick count** — run ticks until ~X ms spent this frame, then yield (`yield return null`) and resume next frame. This self-adjusts as ticks get heavier (more systems), where a fixed count would need constant re-tuning.
- **Target ≥10 fps** during a skip (100 ms/frame) so the loading spinner animates; aim higher (~30 fps) for a smooth spinner, trading a few more frames. Short skips finish sub-second; multi-week Scheduler runs (thousands of ticks) take several real seconds, which is where the smooth-spinner-vs-fast-skip dial matters.
- **Host-only loop.** The host runs the ticks (so the ms budget is a host concern); the client shows its own overlay and receives the **bulk-pushed final state** at the end (§1.6) — it never replays ticks. Optional polish: host sends periodic progress so the client's clock animates too.
- **Exclusive mode.** While the chunked ticks run, the world is frozen for interaction (the dark overlay), so nothing else mutates state mid-skip — that's what makes "100/frame × 6" identical to "600 in one frame."
- **Test:** assert chunking a skip across N frames yields the exact same result as one frame — the only failure mode the frame-splitting can introduce.

### 4.3 What a tick simulates — the fidelity gradient

A tick advances the world's **simulation state**, not rendering, animation, or full pathfinding (the world is hidden behind the overlay during a skip). Fidelity is gated by proximity to a dreamer and by engagement:

- **Distant / inactive-map entities and routine NPCs** → abstract resolution only: routines progress, needs drain, production/consumption computed. No movement, no AI. (GDD §7, §20.)
- **Engaged entities within a radius of a dreamer** → lightweight behavioral sim each tick: a decision (flee / re-engage / wander) plus **coarse movement** (`speed × tickDuration`, minimal navmesh sampling — not per-frame steering). "Engaged" = unresolved business with the dreamers: an active affliction, or an alerted/hostile/fleeing/stalking state. An idle ambient animal does not qualify.

**Rationale (anti-cheese):** a wounded animal you didn't finish must bleed and flee during a skip, not freeze waiting to be killed at full stamina. Fleeing entities leave the radius within a tick or two, so the engaged set shrinks fast; cost stays bounded.

The lightweight skip-AI is deliberately *plausible*, not bit-identical to real-time AI — tick==jump strictly governs only the deterministic resolvable state (needs, resources, durability, affliction→Vitality), not AI behavior. This is acceptable because you cannot skip through real combat.

### 4.4 The per-tick interlock

One tick = one IG minute, host-side, entities processed in a fixed deterministic order.

| # | Stage | Scope | Notes |
|---|---|---|---|
| 1 | Advance clock (+1 min) | world | Flag midnight (→10) and day/night flips. |
| 2 | Buff/debuff timers + needs-buffs | per entity | Decrement, expire, re-evaluate needs-fulfillment buffs from tick-start values. Locks this minute's modifier set. |
| 3 | Drain Tier-1 needs | per entity | `base × buffMod × envMod × activityMod × 1min`. Activity = current task. |
| 4 | Advance tasks / routines | per entity | Consume time + energy, progress prep/process, apply outputs on completion — incl. need refills (eat/drink) and energy recovery (sleep/nap/rest). |
| 5 | Reconcile needs-driven afflictions | per entity | From post-drain, post-meal values: apply/clear Starving/Dehydrated/etc. |
| 6 | Tick afflictions → Vitality | per entity | Every active affliction applies its per-minute Vitality drain. |
| 7 | Vitality / incapacitation check | per entity | Below threshold → down → route by type (dreamer → dream flow; NPC → down + permadeath countdown; wildlife → down). |
| 8 | Engaged-entity AI + coarse movement | gated | Decision + `speed × 1min`. Reads post-Vitality state. |
| 9 | (weather rolled at world level here or stage 1) | world | Evolve weather per GDD §2 tables; flag major event (blizzard) as interrupt. |
| 10 | Day boundary | world | Midnight-crossing tick only: reset daily time budgets; evaluate streaks. |
| 11 | Skip control | world | Skip mode only — see §4.5. |

> Note: weather (env) must be set before stage 3's drain; in implementation, roll weather at the top (after the clock) so warmth modifiers are current. The table lists it at 9 for readability — sequence it before stage 3.

**Critical ordering: drain → tasks/meals → reconcile afflictions → drain Vitality** (3→4→5→6). A meal eaten this minute clears Starving *before* the Vitality tick lands — eat in time and you take no damage; miss it and you do.

**Cross-cutting rules:**
- **Modifiers are as-of-tick-start** (stage 2 before stage 3), so each minute has one well-defined rate.
- **Fixed deterministic entity order** (by id), so ties (shared-storage draws, the seeded RNG) resolve identically every run — required for tick==jump and save/revert reproducibility.
- **RNG is state.** Any stochastic per-tick behavior (AI decisions, weather rolls, affliction procs) uses a **seeded, deterministic RNG stream that is part of the authoritative state** (rides in the world file alongside the clock), or tick==jump breaks for the random bits and reverts won't reproduce.

### 4.5 Skip control — guards, interrupts, the global stop

Stage 11 runs only in skip mode and decides whether to continue the loop.

- **Dreamer guards = hard cap.** If a tick would push a dreamer's need/debuff past its guard, that is the last tick — the skip stops. This is dreamer death-prevention: a dreamer can only actually go down in real time, where the full dream flow can run.
- **NPC guards = warning, not stop.** An NPC about to die mid-skip shows a death-risk icon but does not halt the skip. NPCs are mortal; that's the players' risk.
- **Interrupts** (blizzard begins, a nearby entity now threatens a dreamer, combat starts) also stop the skip.
- **The stop is atomic and global.** There is one shared world clock, so a skip cannot run for one dreamer while the other is in real time. Any dreamer guard or interrupt halts the skip for *everyone*, dropping all to real time at that IG moment. On the stop, any dreamer who was mid-sleep wakes cut-short and banks a checkpoint scaled to the partial sleep (§4.7); dreamers doing other tasks just stop.
- **Planned transitions are not stops.** A routine wake (sleep 4h → guard 4h) resolves inside the skip — the entity swaps tasks and the skip rolls on. Only unplanned guard-caps/interrupts trigger the global halt.
- **Consequence:** max skip length = the time until the *first* dreamer would hit a guard (or an interrupt fires).
- (GDD §5 also allows "the partner stays in real time while the other sleeps" — that is simply *not skipping*; both are in real time, one rests inactive. The atomic-stop rule governs only actual skips.)

- **Deferred: skip-start consensus.** The co-op "both agree on the same duration"
  selection (GDD §5) is not built — skip is host-initiated for both. When it returns,
  it reuses the **agree-on-a-value consensus component built in 0.1.7** for the revert
  vote (greyed Continue until both pick the same value; solo auto-confirms). The two
  are the same UI pattern over different payloads — a duration vs. a checkpoint — so
  build that component generically. Until then, the host decides and the stop stays
  global via the single host loop.

### 4.6 The Scheduler (future feature)

The Scheduler is not separate math — it is **the resolver run forward over a long horizon**, with a pre-set daily plan supplying scheduled events (meal at 08:00, sleep at 22:00…). It uses the same tick, the same global-stop logic, and the same guard checks. A plan is "sustainable for duration D" iff *neither dreamer trips a guard within D* — i.e., the Scheduler reports the point at which the run would halt. Comparing net daily totals is explicitly wrong: a plan can balance intake against drain and still go critical *between* meals; only the trajectory matters.

### 4.7 Dream rules — attachment

The dream system (GDD §18) adds little new core machinery; it mostly **wires together tasks, buffs, and the 0.0.5 checkpoint/revert**, plus two real-time flow moments.

| Piece | Attachment |
|---|---|
| **First Dream** | Session-start (entering action phase): capture a shared checkpoint + grant both dreamers an initial shared protection buff. |
| **Sleep → checkpoint** | Sleep is a task (a guarded skip that recovers energy). On **waking** (not lie-down — you revert to the rested state) it fires the 0.0.5 capture and grants a protection buff scaled to actual sleep duration. |
| **Save consumable** | Same capture, triggered by an item-use action; personal protection buff, fixed shorter duration. |
| **Protection buffs** | Ordinary timed buffs (stage 2 counts down/expires), each referencing a checkpoint file + coverage tag. The files persist on disk; the buffs are the runtime validity/expiry layer. "Valid checkpoint for dreamer X" = X has a live personal buff, or a live shared buff exists. |
| **Dreamer down (stage 7) → dream flow** | Combat *or* neglect incapacitation enters the GDD §18 flow, real time (skip blocked while a dreamer is down). |
| **Rescue** | Real-time scramble: the other dreamer applies first aid within the affliction's window. Success → revive in place, no revert, **no Nightmare**. |
| **Wake Up (revert vote)** | Rescue fails/declined → pause the world (0.0.4 pause hook) → vote lists the *dead* dreamer's valid checkpoints (from protection buffs); both agree (solo auto) → revert via the apply path (0.0.5) → apply Nightmare to the dead dreamer. |
| **Nightmares** | A debuff in the buff system; re-death while active escalates the tier and refreshes duration, reducing energy. Lives in stage 2/6. |
| **Game Over** | Dreamer down with no live protection buff → no valid checkpoint → run ends, save locked. |
| **Skip guard = death-prevention** | Stage 11 caps a skip before a dreamer would go down (§4.5). |

**Shared vs personal** is the coverage of the *protection buff*, not the file (the file is always a full world + both-dreamers snapshot). Determination: on any tick where one or more sleep tasks complete, collect the set W of dreamers waking *this tick*. If W = both → grant both a buff (**shared**); if W = one → grant that one (**personal**). Remove W from the sleeping set (never clear the whole set). A guard-capped early wake therefore yields a personal, partial-duration checkpoint.

### Checkpoint lifecycle & revert reconciliation (corrects 0.1.5 b6)

> **Supersedes 0.1.5 b6.** The original "delete a checkpoint when its last referencing
> buff expires" rule is wrong in a time-reverting world and is retired. See c1 below.

A checkpoint file is the unit of "a dream you can return to." Its lifecycle obeys:

**a. A checkpoint file always reflects the buffs that reference it.** The protection
buff(s) granted for a checkpoint are present *in that checkpoint's own file* (id assigned
before capture, or re-saved immediately after granting). Reverting therefore restores
protection at full as-of-creation duration — never strips it.

**b. Revert is clear-and-rehydrate, never merge.** A revert clears both dreamers' live
buffs/effects and the world, then rehydrates entirely from the target file. Buff remaining
times come from the file, anchored to the rewound clock, so nothing outlives the rewind
and the non-dying dreamer is rolled back as fully as the dying one.

**c. Validity is computed, retention is conservative — they are separate concerns.**
- *Validity* (which checkpoints a dreamer may revert to) = the checkpoints referenced by
  that dreamer's *current live* protection buffs. It self-heals on revert: rewinding the
  clock restores, from the snapshot, buffs that had expired in forward-time, so a
  checkpoint can become valid again. Validity is never tracked by deleting files.
- *Retention* (file lifetime) is conservative. **Files are not deleted on buff expiry**
  (c1, the retired rule). A file is deleted only when rewound *past* — the discard-futures
  step of a revert, where its timeline is erased and unreachable. Reachability-aware space
  reclamation is deferred to §21.

**d. Discard-futures is keyed on world-clock time.** Reverting to a target deletes
checkpoints whose world-clock timestamp is newer than the target's; older-or-equal
checkpoints are kept (and may be re-validated by the revert per c). Menu-loading an older
slot follows the same rule; the full save-slot/Save-and-Exit relationship remains §21.

**e. Nightmares are origin-tagged and baked into the file via re-save.** Each nightmare
records its origin checkpoint. On death→revert: discard futures → clear-and-rehydrate →
escalate the origin==target nightmare (or apply tier 1) on the dead dreamer → re-save the
target file (world still at target-time, so only the curse accumulates). Nightmares from
*other* slots ride in through the snapshot (captured when the later checkpoint was made),
so the re-save governs only the target's own nightmare — no double-count. Menu-load is
read-only: reapply each baked nightmare at its stored tier, no increment, no re-save. The
tier living in the file is the anti-cheese: closing and reloading cannot shed it.

**Canonical revert order:** discard futures → clear-and-rehydrate both dreamers + world →
escalate/apply the dead dreamer's nightmare → re-save the target file → resume.

### 4.8 New code vs wiring

The only genuinely new code in the dream layer is: the **rescue interaction**, the **revert-vote consensus UI** (reusing the both-agree pattern from the skip dialog), and the **Nightmare escalation logic**. Everything else is hooks into the task, buff, and checkpoint/revert systems already built.

### 4.9 Tuning knobs & deferred

**Tuning (config-driven):** canonical step size; per-frame ms budget + target fps; need drain rates and guard thresholds; protection-buff durations (per difficulty); Nightmare tier penalties/durations; engaged-entity radius; rescue windows.

**Deferred:** combat detail (GDD §15 — Cluster 1 uses the incap/affliction pipeline but not full enemy combat); full Scheduler UI; checkpoint pruning/retention edge cases and crash-recovery menu-loadability (GDD §21 hardening); personal/shared revert-vote UI polish.
### 4.10 Build Breakdown

Cluster 1 builds, numbered 0.1.x (continuing Phase 0's 0.0.x). Each is validated
with two clients via MPPM, as in Phase 0. The arc: assemble the tick in real time
(0.1.0–0.1.2), make Skip run it fast and then safely (0.1.3–0.1.4), then wire the
dream rules (0.1.5–0.1.7).

**0.1.0 — Resolver heartbeat + needs drain**
Generalize 0.0.4's clock-advance into `Step()` (one IG minute), fired once per IG
minute in real time. Add Tier-1 needs (GDD §4) to dreamer authoritative state; each
Step drains them at base rate (stage 3). Host-authoritative, synced via the 0.0.4
host-auth push pattern (host pushes values, client displays — client extrapolation
is later polish).
*Exit:* a need bar ticks down in real time, identical on both clients; the value
round-trips through 0.0.3 load and 0.0.5 checkpoint/revert.

**0.1.1 — Tasks on the tick**
Add the task layer (stage 4, minimal): a dreamer's current activity sets a drain
modifier; completable tasks consume time + energy and apply outputs on completion —
the two that matter here are a meal (refills hunger/thirst) and sleep/rest (recovers
energy), per GDD §5. Full routine *planning* (the Scheduler, §4.6) is deferred. Task
state is host-auth, synced + saved.
*Exit:* assign "eat" → hunger refills on the tick; assign "sleep/rest" → energy
recovers; both synced + saved on two clients.

**0.1.2 — Afflictions + Vitality**
Add stages 5–7: needs crossing thresholds apply/clear afflictions (Starving,
Dehydrated…); afflictions drain Vitality each tick; Vitality past threshold →
incapacitation (dreamer → placeholder "downed" state for now; NPC/wildlife →
removed). Proves the critical 3→4→5→6 ordering. Host-auth state.
*Exit:* starve a dreamer on two clients → affliction appears, Vitality drains, incap
triggers; eating before the Vitality tick prevents the hit; all synced + saved +
revertible.

> At 0.1.2 the real-time interlock is whole (minus skip-control and dream-routing).
> Skip is next: literally running this same Step many times.

**0.1.3 — Skip (the fast-forward)**
Make Skip run `Step()` N times in a host-side loop, chunked by a per-frame ms budget
(§4.2), with the client overlay + bulk-pushed final state (§1.6) + exclusive mode.
No guards yet — test with durations short enough that no need would go critical.
Cluster 1 skip is **abstract-resolution only**; the engaged-entity fidelity gradient
(§4.3) is deferred until wildlife/combat exist.
*Exit:* skip N safe IG hours → host runs chunked ticks, client shows overlay then
converges on the bulk push; **chunking-invariance** holds (skip result == real-time
result for the same interval); both clients identical.

#### 0.1.4 — Skip control (guards, interrupts, global stop) — detailed

The skip is a single host-side loop with a passive client (0.1.3), and currently
**host-initiated for both players** (no agree-on-duration vote yet — deferred, see
notes). So the stop is global by construction. 0.1.4 adds: *when* to stop (a, c) and
the *force-wake* side effect (b). There is no up-front projection — the player picks a
duration, the skip runs, and it stops at runtime the moment a guard trips.

##### 0.1.4a — Guard predicate + per-tick stop

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | Guard = a **per-need percentage of max** (default **0.30**). Config-driven: one global default + per-need override, structured so a future player-facing setting can drive it. Applies to all needs. | Claude Code | Each need exposes a guard % (default 0.30); changing the config changes the stop point. |
| a2 | `IsAtGuard(dreamer)` = any need whose current value is below that need's guard %. Dreamers only. | Claude Code | Returns true the tick a dreamer's first need drops under its guard. |
| a3 | Stage 11 (skip mode only): after stages 1–10, if any dreamer `IsAtGuard`, set `stopRequested` + a typed reason (dreamer, which need). Real-time mode: no-op. | Claude Code | A need crossing 30% during a skip flags stopRequested + reason; real-time unaffected. |
| a4 | Skip loop: after committing each tick, if `stopRequested`, break — this committed tick is the last; the world rests at that IG moment. | Claude Code | The loop halts on the flagged tick, not at the requested duration. |
| a5 | Carry the stop reason in the end-of-skip bulk push; client ends overlay → real time and surfaces the reason. | Split — logic: Claude Code; reason UI: Manual | Both clients end at the same IG moment, same state, reason shown. |

*0.1.4a acceptance:* request an 8h skip with a need that will cross 30% at ~hour 3 →
the skip stops at ~hour 3 (need still above critical), host + client drop to real time
there with the reason. Single-client first, then MPPM.

##### 0.1.4b — Force-wake + planned-transition distinction

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | On stop (a4), force-end any in-progress **sleep** task at the stop tick: mark cut-short, record actual-slept minutes, return the dreamer to awake/idle. | Claude Code | A dreamer sleeping when the skip stops is awake afterward; slept-duration recorded. |
| b2 | Expose the cut-short result (dreamer, actualSleptMinutes, wasCutShort) as an event/query for 0.1.6 to consume. **No checkpoint is banked in 0.1.4.** | Claude Code | The result is observable; nothing is checkpointed yet. |
| b3 | Confirm a planned transition does not stop: a routine `sleep Nh → next task` completes the sleep inside stage 4 and continues, never touching stage 11. | Claude Code (test) | A planned sleep→task transition mid-skip does not halt the skip. |

*0.1.4b acceptance:* a guard stop force-wakes a sleeping dreamer (cut short, duration
recorded, no checkpoint); a planned routine transition mid-skip does not stop.

##### 0.1.4c — Interrupt channel + debug injection

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | Interrupt channel: a host-side pending-interrupt queue (typed reason) that stage 11 checks alongside guards. Weather/wildlife push to it later; for now it's the extensible hook. | Claude Code | Stage 11 stops the skip on a pending interrupt, with its reason. |
| c2 | Debug interrupt producer: a dev-only input (key/console/inspector) that enqueues an interrupt mid-skip. | Split — logic: Claude Code; input: Manual | Triggering it during a skip enqueues an interrupt. |
| c3 | Two-client check: host fires the debug interrupt mid-skip → both clients drop to real time at that IG moment with the reason. | Manual (MPPM) | Both clients halt together on the interrupt. |

*0.1.4c acceptance:* a debug interrupt mid-skip halts the skip globally — both clients,
same moment, reason shown.

> c is the only part with no real consumer yet (no weather/wildlife exist). If you want
> a leaner 0.1.4, a + b are the must-haves and c can wait until the first real interrupt
> source needs the hook.

##### Notes / current state & deferred

- **30% guard sits well above critical**, so a skip always stops before any need-driven
  affliction or incapacitation — need-guards alone suffice for Cluster 1 (no separate
  debuff-guards; combat afflictions are real-time-only, where skip is blocked).
- **Skip is host-initiated for both players; no agree-on-duration vote yet.** That
  consensus (shared with the revert vote, GDD §5) is deferred. The stop stays global
  because the host runs the single loop regardless of who started it.
- **No up-front projection/menu cap** (former d, removed). The skip runs the requested
  duration and stops at runtime on the first guard hit. The projection can return later
  as a UX layer if wanted.

##### 0.1.4 acceptance (combined, 2-client)

Request a skip long enough that a need will cross 30% → it stops at that tick (need
above critical), both clients drop to real time at that IG moment with the reason, and a
dreamer who was sleeping is force-woken (duration recorded, no checkpoint). A debug
interrupt mid-skip halts globally the same way. A planned routine transition mid-skip
does not stop.

#### 0.1.5 — Buff system + checkpoints — detailed

Builds the timed-buff layer that fills stage 2 of the tick (a no-op until now), then
the protection-buff + checkpoint machinery on top of it (b), and the save consumable
(c). Checkpoints are *banked* here but not yet *reverted to* via a Wake Up — that's
0.1.7 — so 0.1.5 is validated through the 0.0.5 dev revert plus buff/validity
inspection.

##### 0.1.5a — Timed buff/debuff system (stage 2)

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | Buff **def** (static / SO): id, display info, duration (minutes), and an effect descriptor (a list of stat modifiers, e.g. need-drain ×, energy ±). | Split — script: Claude Code; SO assets: Manual | A buff def can be authored with a duration and zero-or-more modifiers. |
| a2 | Runtime buff **instance** in the entity's authoritative state (dreamer record): def ref, remainingMinutes, optional payload. Plain serializable C#. | Claude Code | A dreamer can hold a list of live buff instances. |
| a3 | Stage 2 logic: each tick, decrement remainingMinutes, expire (remove) at ≤0, then recompute the entity's **active modifier set** (as-of-tick-start) from its live buffs. | Claude Code | Buffs count down and expire on the tick; the modifier set reflects current buffs. |
| a4 | Feed the modifier set into stage 3 drain (and other consumers): `base × buffMod × …`. Prove with a **test buff** that visibly changes a need's drain rate. | Claude Code | Applying the test buff visibly alters drain; removing/expiring it restores baseline. |
| a5 | Buff API: add/remove a buff on an entity (host-side). | Claude Code | A buff can be applied and removed programmatically. |
| a6 | Sync buffs host→client (host-auth) for the HUD; include buffs in the dreamer-file save. | Claude Code | Buffs appear on the client and round-trip through 0.0.3 save/load + 0.0.5 revert. |
| a7 | Minimal buff-panel HUD: list active buffs with remaining time (GDD §5). | Split — logic: Claude Code; layout: Manual | Both clients see active buffs counting down. |

*0.1.5a acceptance:* apply a test buff → it shows on both clients, ticks down, expires
on schedule, its drain effect is visible, and it survives save/load + dev revert.

##### 0.1.5b — Checkpoints, protection buffs, First Dream

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Move from 0.0.5's single checkpoint to a **checkpoint store**: multiple coexisting checkpoint files keyed by id, each with metadata (timestamp, coverage label). Extend the 0.0.5 writer to stamp id + metadata. | Claude Code | Several checkpoints can exist at once, each identified and timestamped. |
| b2 | **Protection buff** = a timed buff (rides 0.1.5a) carrying a `checkpointId` + a coverage label (Shared / Personal, for UI only). | Claude Code | A protection buff references a checkpoint and shows its label + remaining time. |
| b3 | `ValidCheckpointsFor(dreamer)` = the checkpoint ids referenced by *that dreamer's* live protection buffs. (Coverage is emergent — see decisions.) | Claude Code | Returns exactly the checkpoints the dreamer is currently covered by. |
| b4 | **First Dream:** on entering the action phase, capture a shared checkpoint (0.0.5) and grant **both** dreamers a protection buff to it, at the configured initial duration. | Claude Code | Session start leaves a shared checkpoint + a live protection buff on each dreamer. |
| b5 | Protection-duration **config**: the difficulty→duration table (GDD §18; Hardcore 16h / Hard 24h / Normal 32h / Easy 48h for full sleep) lives in config. First Dream uses an initial-shared value; sleep-scaled durations are 0.1.6. | Split — script: Claude Code; values: Manual | Durations are config-driven; changing difficulty changes coverage length. |
| b6 | Checkpoint cleanup: when a buff expires and no live buff references its checkpoint, delete the file. (Full retention / crash-recovery-loadability policy deferred to GDD §21.) | Claude Code | Orphaned checkpoint files are removed; referenced ones persist. |

*0.1.5b acceptance:* at session start a First Dream shared checkpoint exists and both
dreamers show a shared protection buff counting down; `ValidCheckpointsFor` lists it
for both; it survives save/load; reverting to it via the 0.0.5 dev path restores the
session-start state; on two clients.

##### 0.1.5c — Save consumable

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | Save-consumable **action** (mechanic only): on use, capture a personal checkpoint (0.0.5, tagged personal) and grant the **using** dreamer a personal protection buff at the consumable duration tier (config). | Claude Code | Using it banks a personal checkpoint + a personal buff on that dreamer alone. |
| c2 | Debug trigger for the action (no inventory yet — GDD §10 is a later cluster; the item wrapper comes then, like 0.1.4c's debug interrupt). | Split — logic: Claude Code; input: Manual | A dev input fires the save-consumable action for a chosen dreamer. |
| c3 | Validity check: after A uses it, `ValidCheckpointsFor(A)` includes the new checkpoint and `ValidCheckpointsFor(B)` does not. | Claude Code (test) | The personal checkpoint covers only the user. |

*0.1.5c acceptance:* dreamer A uses the save action → a personal checkpoint is banked,
A gets a personal buff (B does not), A's validity set includes it and B's doesn't;
survives save/load + dev revert; two clients.

##### Key decisions

- **Coverage is emergent from who holds the buff.** "Valid checkpoints for X" = the
  checkpoint ids on *X's own* live protection buffs. A shared sleep grants both dreamers
  a buff to the *same* checkpoint; a personal action grants one. The Shared/Personal
  label is kept only for the revert-vote UI — it is not what the validity query reads.
  This matches the GDD §18 worked examples directly.
- **Checkpoint store, not a single checkpoint.** 0.0.5 held one; 0.1.5 needs several
  coexisting (First Dream + consumables + later sleeps), keyed by id with metadata. A
  checkpoint lives while a buff references it (b6); the timestamp feeds the 0.1.7 vote.
- **Buff effects proven with a test buff (a4)** even though protection buffs carry no
  stat effect — so Nightmares (0.1.7) and needs-fulfillment buffs (later) ride a proven
  modifier path, not a stub. Wiring specific needs-fulfillment buffs (need levels →
  well-fed etc.) is deferred; the system here supports them.
- **No real revert yet.** Wake Up is 0.1.7; 0.1.5 validates checkpoints via the 0.0.5
  dev revert + the validity query.
- **Consumable via debug trigger** since inventory doesn't exist — the *mechanic* is
  built now, the inventory-item wrapper lands with the inventory cluster.

##### 0.1.5 acceptance (combined, 2-client)

Session starts with a First Dream shared checkpoint and both dreamers showing a shared
protection buff in the panel, counting down; the validity query lists it for both. A
dreamer using the debug save-consumable banks a personal checkpoint + personal buff
(that dreamer only). Buffs tick down and expire on the tick (test buff's effect
visible). Everything round-trips through save/load, and reverting to any live checkpoint
via the 0.0.5 dev path restores that exact state. Orphaned checkpoints are cleaned up.

#### 0.1.6 — Sleep → checkpoint — detailed

Layers checkpoint-on-wake onto the existing full-sleep task. Because 0.1.5 made coverage
emergent (a checkpoint covers whoever holds a buff to it), the determination reduces to:
collect the dreamers who wake **this tick**, capture **one** checkpoint at the tick's
state, grant each qualifying waker a protection buff scaled to their own sleep, and label
it shared if two buffs were granted, personal if one. There is no separate shared/personal
code path. Adds an **end-of-tick wake-resolution step** to the interlock (after stage 11),
since shared-vs-personal can only be decided once the full wake set for the tick is known.
Still no real Wake Up (that's 0.1.7) — validated via the 0.0.5 dev revert + the validity
query.

##### 0.1.6a — Sleep→checkpoint mechanism (single + same-tick shared)

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | Unified `OnSleepEnded(dreamer, sleptMinutes, reason)` fired wherever a **full-sleep** task terminates: planned completion (stage 4, real-time or skip) and cut-short force-wake (stage 11 / 0.1.4b). Nap and Rest do **not** fire it. | Claude Code | Every full-sleep termination raises one event with actual slept minutes; naps/rest don't. |
| a2 | Duration scaling `scale(sleptMinutes, difficulty)` off the 0.1.5 config (full 8h → the table value; ~half for a half sleep — linear start), plus a config **min-bank threshold**: below it, wake with no checkpoint/buff. | Split — script: Claude Code; values: Manual | Full sleep yields full duration; shorter scales down; sub-threshold banks nothing. |
| a3 | End-of-tick wake resolution: collect the tick's wakers; if ≥1 slept ≥ threshold, capture **one** checkpoint (0.1.5 store) at this tick's state and grant each qualifying waker a protection buff to it, each scaled to **its own** slept minutes; label = #buffs (2 → shared, 1 → personal). Remove this tick's wakers from the sleeping set. | Claude Code | One waker → personal checkpoint + scaled buff; both waking the same tick → one shared checkpoint + a buff each. |
| a4 | Sync + save + dev-revert validation. | Manual (MPPM) | The checkpoint + buffs round-trip through save/load and the 0.0.5 dev revert restores that state, on two clients. |

*0.1.6a acceptance:* a solo full sleep banks a personal checkpoint + scaled buff; both
sleeping and waking the same tick bank **one** shared checkpoint with a buff each (each
scaled to its own sleep); a nap banks nothing; a sub-threshold sleep banks nothing. Single
client first, then MPPM.

##### 0.1.6b — Staggered & cut-short edges (the determination's hard cases)

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | **Staggered wakes:** A sleeps 8h, B sleeps 4h then continues another task. B's wake (4h tick) → B personal; A's wake (8h tick) → A personal; **A stays asleep when B wakes** (remove-only-W). Two distinct checkpoints. | Claude Code | Two independent personals across two ticks; the still-sleeping dreamer is untouched. |
| b2 | **Cut-short integration:** 0.1.4b's force-wake feeds `OnSleepEnded` with its recorded `sleptMinutes` + cut-short reason → partial-duration buffs through the same resolution. Cut-short with both sleeping → shared partial (one checkpoint, two buffs); cut-short with only one sleeping (other was on a non-sleep task) → personal partial. | Claude Code | A guard/interrupt stop banks correctly-scaled partial checkpoints; coverage matches who was actually sleeping. |
| b3 | **Sub-min mixed case:** both wake the same tick, one slept ≥ threshold and one < → one checkpoint, only the qualifying dreamer gets a buff (label = personal, since one buff). | Claude Code (test) | A sub-threshold co-waker receives no buff; coverage reflects the buff count. |

*0.1.6b acceptance:* the full matrix holds on two clients — staggered → two personals with
the sleeper correctly left asleep; cut-short-both → shared partial; cut-short-one →
personal partial; sub-min → nothing — all save/revert-clean.

##### Key decisions

- **Determination = one checkpoint per wake-tick + a buff per qualifying waker; coverage =
  buff count.** No shared/personal branching — it falls out of 0.1.5's emergent coverage.
- **Each buff scales to its own dreamer's slept minutes,** even in a shared wake (two
  dreamers can wake the same tick having slept different amounts).
- **Resolution runs end-of-tick,** after both wake sources (stage 4 planned, stage 11
  cut-short) are collected — you must know the full wake set before labelling.
- **Only full sleep banks;** nap and rest recover energy only.
- **Buffs accumulate and expire naturally;** "refresh the shared floor" (GDD §18) emerges
  as a newer shared sleep producing a newer, longer-lived shared checkpoint — no explicit
  replacement, so a still-valid older checkpoint is never dropped.
- **Min-bank threshold (config)** prevents checkpoint spam from tiny cut-short sleeps.

##### 0.1.6 acceptance (combined, 2-client)

A solo sleep banks a personal checkpoint; a together-sleep (both wake the same tick) banks
one shared checkpoint with a buff each; staggered sleeps bank two independent personals and
never disturb the dreamer still asleep; a guard/interrupt cut-short banks correctly-scaled
partials (shared if both were sleeping, personal if one); naps and sub-threshold sleeps
bank nothing. All buffs scale to actual sleep, show in the panel, and round-trip through
save/load + the 0.0.5 dev revert.

#### 0.1.7 — Dream flow (down, Rescue, Wake Up, Nightmares, Game Over) — detailed

The last Cluster 1 build, and the only genuinely-new code (§4.8). A downed dreamer (real
time only — the 30% skip guard prevents it during a skip) enters the GDD §18 flow: a
real-time Rescue scramble (a); on failure, a paused Wake Up vote that reverts the whole
world to a checkpoint (b); then the Nightmare/Game-Over consequences (c). The revert reuses
the 0.0.5 apply path, so it rewinds **both** dreamers + world — which is exactly why the
vote is a joint consensus, not the dead dreamer choosing alone.

##### 0.1.7a — Down → dream flow + Rescue (real-time, success path)

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | On incapacitation (replaces 0.1.2's placeholder), enter the dream flow in real time: open a config'd **rescue window** countdown on the downed dreamer, mark them non-controllable, and flag world-state "dreamer down." World keeps running (real-time scramble). | Split — logic: Claude Code; down-state presentation: Manual | A down opens a live rescue window; the dreamer is non-controllable; the flag is set. |
| a2 | **Rescue interaction:** the other dreamer, if alive and in range, performs a revive action within the window → revive in place (restore Vitality to a safe level, clear/reduce the killing affliction). **No revert, no Nightmare.** | Split — logic: Claude Code; input/feedback: Manual | An in-range partner revives the downed dreamer within the window; the flow closes cleanly. |
| a3 | Window expiry / no able partner / declined → transition to Wake Up (0.1.7b). Solo: optional simple AI-partner attempt; otherwise the window lapses → Wake Up. | Claude Code | A lapsed/declined rescue hands off to the Wake Up flow. |
| a4 | Block skip-start while any dreamer is down (rescue window or Wake Up). | Claude Code | Skip cannot be initiated while a dreamer is down. |

*0.1.7a acceptance:* down a dreamer in real time → rescue window opens, skip blocked; an
in-range partner revives within the window → dreamer back up, no revert, no Nightmare; a
lapsed window → hands off to Wake Up. 2-client.

##### 0.1.7b — Wake Up: consensus vote + revert (fail path)

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | **Generic agree-on-a-value consensus component** (the marked shared piece): live mutual selection, Continue greyed until both picks match, solo auto-confirms. Value-agnostic (payload = checkpoint here; deferred skip-start vote reuses it with payload = hours). | Split — logic: Claude Code; UI layout: Manual | Two players can converge on one selected value; mismatch keeps Continue disabled; solo auto-confirms. |
| b2 | Build vote options = the **down dreamer's** valid checkpoints (0.1.5 validity query), each with timestamp + a **cost** proxy (elapsed IG time since that checkpoint — richer accounting deferred). | Claude Code | The vote lists exactly the dead dreamer's covered checkpoints with time + lost-time. |
| b3 | Pause the world (0.0.4) for the vote; resume after. | Claude Code | The world is frozen during the Wake Up vote. |
| b4 | On consensus, revert via the 0.0.5 apply path to the chosen checkpoint (full snapshot → both dreamers + world + clock + buffs as-of-checkpoint), then resume real time. | Claude Code | The world rewinds to the chosen checkpoint and resumes; both dreamers reverted. |
| b5 | Sync the vote to both clients (options + the other's live pick); host adjudicates the revert (host-authoritative). | Claude Code | Both clients see the same options and each other's selection; the host performs the revert. |

*0.1.7b acceptance:* a lapsed rescue → world pauses, both clients see the vote (dead
dreamer's checkpoints, time + cost), Continue greyed until picks match (solo auto), then the
world reverts to the chosen checkpoint and resumes. 2-client.

##### 0.1.7c — Nightmares + Game Over (consequences)

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | **Nightmare debuff** (rides 0.1.5a): a tiered debuff with an energy-reduction effect, applied to the reverted dreamer **after** the revert (the revert restores buffs as-of-checkpoint; the new Nightmare lands on top). | Split — script: Claude Code; def/values: Manual | A Wake Up leaves a tier-1 Nightmare on the reverted dreamer; energy penalty visible. |
| c2 | **Escalation:** on a Wake Up, read the dreamer's current Nightmare; if active → bump tier (deeper penalty) + refresh duration; if none → apply tier 1. | Claude Code | A second death before the Nightmare expires escalates to tier 2 (deeper, refreshed). |
| c3 | **Game Over:** a down dreamer whose validity query is empty (no live protection) → no Wake Up possible → end the run, lock the save (run-level, per §21 / GDD §18). | Claude Code | A down with zero valid checkpoints ends the run for both players and locks the save. |

*0.1.7c acceptance:* a Wake Up applies a tier-1 Nightmare (energy penalty); a re-death before
expiry escalates to tier 2 (refreshed); a down with no valid checkpoint ends the run + locks
the save. 2-client.

#### 0.1.7d — Revert reconciliation, checkpoint lifecycle & nightmare persistence (fix pass)

Corrects three things found in testing: the protection buff missing from its own
checkpoint, the revert not fully rehydrating both dreamers, and the delete-on-expiry
cleanup rule (old 0.1.5 b6) which destroys checkpoints a later revert needs. Also lands
the re-save nightmare model. The nightmare work sits on top of a correct revert, so do
groups a–c before d.

##### a — Checkpoint includes its own protection buff (fixes "dead dreamer loses protection")

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | At checkpoint creation, ensure the protection buff(s) referencing the new checkpoint are present in the file: assign the checkpoint id first, grant the buff, then capture — so the buff is in the initial write (or re-save immediately after granting). | Claude Code | A freshly created checkpoint file contains its own protection buff at full as-of-creation duration. |
| a2 | Verify via dev revert: reverting to a checkpoint restores its protection buff at full duration. | Manual | Reverting to First Dream puts the buff back at 1000 min for the covered dreamer(s). |

##### b — Revert clears and rehydrates the full snapshot (fixes "other dreamer keeps buffs" / "buffs last longer")

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Revert *clears* both dreamers' live buffs/effects and the world, then rehydrates entirely from the checkpoint file. No merge/reconcile of live-against-file. | Claude Code | After revert, each dreamer's buff set equals the file's exactly; nothing live survives. |
| b2 | Confirm buff timing is part of the rehydrated state (remaining time comes from the file, anchored to the rewound clock) — no buff outlives the rewind. | Claude Code | A buff acquired after the checkpoint is gone post-revert; restored buffs show as-of-checkpoint remaining, not inflated. |

##### c — Checkpoint retention correction (fixes the expiry-deletion bug; overturns 0.1.5 b6)

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | **Remove** the "delete a checkpoint when its last referencing buff expires" behavior (old 0.1.5 b6). Checkpoint files are no longer deleted on buff expiry. | Claude Code | A checkpoint whose protection expired in forward-time still exists on disk. |
| c2 | Keep validity dynamic: `ValidCheckpointsFor(dreamer)` continues to read the dreamer's *current live* protection buffs only. No file-based validity tracking. | Claude Code (verify) | A checkpoint is offered iff a live buff references it now; it self-heals after a revert restores the buff. |
| c3 | On revert (going back in world-clock time), discard checkpoints whose world-clock timestamp is *newer* than the target; keep all older-or-equal. | Claude Code | Reverting to a 6 PM slot deletes a 7 PM slot's file but keeps a noon slot's file. |
| c4 | (Menu-load of an older slot follows the same world-clock discard rule for consistency; the broader save-slot/Save-and-Exit relationship stays deferred to §21.) | Claude Code | Loading an older slot doesn't leave newer-timeline checkpoints orphaned. |

##### d — Nightmare persistence via re-save (origin-tagged, baked into the file)

| ID | Task | Executor | Done when |
|---|---|---|---|
| d1 | Add an **origin-checkpoint id** to the nightmare buff so "this slot's own nightmare" is distinguishable from carried-over ones. | Claude Code | A nightmare records which checkpoint produced it. |
| d2 | Death→revert sequence (canonical order): discard futures (c3) → clear-and-rehydrate (b1) → check rehydrated state for an origin==target nightmare → escalate its tier if present, else apply tier 1 to the dead dreamer → **re-save the target file** with the nightmare baked in. World is still target-time (no play yet), so only the curse accumulates. Use the existing atomic write. | Claude Code | Repeated death→revert to the same slot escalates the tier in-file; the world portion stays pristine. |
| d3 | Carried-over nightmares ride in via the snapshot (a nightmare active when a later checkpoint was made is captured into that file). The sidecar/re-save governs only the *target* slot's own nightmare — no double-count. | Claude Code | Reverting to slot 2 restores slot 1's nightmare from slot 2's snapshot and adds slot 2's own → two independent buffs. |
| d4 | Menu-load is read-only for nightmares: reapply each dreamer's baked nightmare at its stored tier — no increment, no re-save. | Claude Code | Closing and reloading a slot preserves its nightmare tier (anti-cheese). |

##### 0.1.7d acceptance (2-client)

- *Eg 1:* First Dream (1000 min) both → one dreamer dies → revert → both back at time 0, First Dream buff at 1000 for both, dead dreamer additionally carries a tier-1 nightmare; the checkpoint is still valid.
- *Eg 2:* slot 1 (D1 consumable) then slot 2 (D2 consumable) → D1 dies → revert to slot 1 → D1 keeps 8h protection, D2 is fully rewound (no consumable, no later items/buffs, no inflated timers), slot 2 (newer) is discarded, slot 1 still valid.
- *Escalation:* die→revert slot 1→tier 1; die→revert slot 1→tier 2 (one buff).
- *Separate dreams:* die→revert slot 1→tier 1; create slot 2; die→revert slot 2→two tier-1 nightmares, independent timers.
- *Anti-cheese:* die→revert slot 1→tier 1→close→menu-load slot 1→tier 1 still present.
- *Retention bug:* slot 1 (noon, protection to 8 PM), slot 2 (6 PM, to 10 PM); at 8:01 PM slot 1's buff expires but its file is kept; at 9 PM die→revert slot 2 (6 PM) → slot 1 valid again (2h); die at 7 PM → slot 1 is offered and revertable.

##### Key decisions

- **Rescue is real-time; only the vote pauses.** The down opens a real-time scramble window;
  the world freezes only once Wake Up begins.
- **The vote is a joint consensus because the revert rewinds both dreamers.** Options are the
  *dead* dreamer's valid checkpoints, but the consequence hits both, so both must agree.
- **Nightmare applies after the revert,** on top of the restored as-of-checkpoint buff state;
  escalation reads the post-revert tier.
- **Cost = elapsed IG time proxy** for now; resource/task-level accounting deferred.
- **Game Over is run-level** (both players, save locked) — flag to confirm if you'd rather a
  dreamer with no coverage be individually lost while the other continues.
- **b1 is value-agnostic** so the deferred skip-start consensus (§4.5) reuses it unchanged.

##### 0.1.7 acceptance (combined, 2-client)

Down a dreamer in real time → rescue window opens, skip blocked. In-range rescue within the
window → revive, no revert, no Nightmare. Lapsed window → world pauses → both clients vote
over the dead dreamer's valid checkpoints (time + cost), Continue greyed until matched (solo
auto) → revert rewinds both dreamers + world to the choice → resume → tier-1 Nightmare on the
reverted dreamer. A re-death before expiry escalates the tier; a down with no valid
checkpoint ends the run and locks the save. All save/revert-clean.

## 5. Cluster 2 — Survival Actions: Design

The cluster previewed in §3. Design pass — not builds yet. Five GDD systems
(Inventory GDD §10, Water GDD §13, Food GDD §17, Durability GDD §11, Crafting GDD §12)
that deliver the gather → carry → craft → consume loop. They all hang off one
abstraction — the item — so the item model is the foundation; everything else is built
on it.

### 5.0 Scope & coupling

The item is to this cluster what the resolver was to Cluster 1: inventory holds items,
durability is a property of items, crafting consumes and produces items, food and water
are items. Spine touch-points, all reusing Phase-0/Cluster-1 infrastructure:

- **Save:** item instances are authoritative state — equipped containers in the dreamer
  files, ground items and storage in the world file. Plain-serializable Simulation POCOs,
  so they ride the existing snapshot / revert.
- **Needs:** eat/drink = consume an item → refill a need, finally making the 0.1.1
  placeholder meal/drink tasks real.
- **Tick:** nothing new on the per-tick loop — spoilage is computed (§5.2), durability is
  use-driven (§5.2), crafting-over-time is an ordinary task (the 0.1.1 task system already
  consumes time + energy and yields outputs).
- **Networking:** items sync host-authoritative via ids, never strings (§5.4).

> **Note (§1.12):** The per-system data designs below — items (§5.1), spoilage (§5.2), containers
> (§5.3), gathering (§5.6) — are now expressed as **aspects and actions on the unified
> Def/Instance/Action model (§1.12)**, not standalone ItemDef/GatherableObjectDef types. Every rule
> here still holds; read "ItemDef field X" as "the corresponding aspect on the unified Def."

### 5.1 The item model

**Definition vs instance.** A static **ItemDef** (ScriptableObject, identical on host and
client) holds immutable content: name, category, weight, stackable flag, max stack, base
durability, base lifespan, and container properties if applicable. A runtime
**ItemInstance** (serializable Simulation POCO) holds mutable state: a def reference (id),
quantity, condition stamp, an optional unique-instance id, and contents if it is a
container. Instances serialize; defs are shared static content — the same split as buffs.

**Three item shapes** (resolving the stacking-vs-condition tension):

| Shape | Examples | Stacks? | Condition |
|---|---|---|---|
| Condition-less stackable | wood, stone, fiber | yes, by def | none |
| Bucketed-condition stackable | meat, berries, cooked food | yes, by (def + condition tier) | spoilage, per stack, quantized to tiers |
| Continuous-durability unique | axe, spear, coat | no — always qty 1 | durability, per instance |

**Stacking rule:** two amounts merge iff same def **and** same condition bucket. A
stackable's condition is quantized into a few tiers (fresh / stale / turning) so same-batch
items actually merge — a raw float would fragment back into one stack per item. Continuous
wear lives only on non-stackable uniques, which never face this.

**Two quantity levels:** the **logical quantity** (RDM + save) is precise and may be
fractional (e.g. liquid volume); the **display quantity** is a read-only floor to whole
numbers. Invariant: all arithmetic — merge, split, craft cost, consumption — runs on the
logical value; display is a pure projection. Floor, not round, so you never show more than
is usable.

### 5.2 Condition — spoilage and durability

Two distinct mechanisms, both deliberately off the per-tick loop.

**Spoilage (perishables) — computed, never ticked.** Condition is a pure function of the
world clock, stored as a re-stampable triple on the instance: `conditionAtStamp`,
`stampTime`, `rate`.
condition(now) = conditionAtStamp − (now − stampTime) × rate     (clamped 0–100)
rate           = 100 / effectiveLifespan

At creation the triple is (initial condition, creation time, def base rate). It is
evaluated lazily — on display, use, or access — so there is no per-tick spoilage sim and no
ongoing condition sync; only the triple is stored, and it is immutable until a re-stamp.
Because host and client compute from the same authoritative world clock, the value is
*exact* on both — no drift, not extrapolation. Skips and reverts are free: it's a function
of the (advanced or rewound) clock and the snapshotted triple.

**Re-stamping** happens only when storage conditions change the decay rate — entering a
colder zone, placing food in cold storage, weather (Cluster 5). On a rate change: set
`conditionAtStamp` = current computed value, `stampTime` = now, `rate` = new rate. Rare
events, not per-tick, and the new triple is snapshotted like any state, so reverts stay
correct. In Cluster 2 the rate is constant (the def base), so this reduces to a fixed
best-before; the triple is the seam that lets Weather modify decay later with no item-model
migration.

**Durability (tools/gear) — use-driven, never ticked.** Durability decrements on use events
(swinging an axe, an action that wears a coat), not over time, so it needs no tick stage.
Time-based wear (rust), if ever needed, uses the same best-before mechanism as spoilage.

Net: the per-tick interlock does not grow for Cluster 2 — both condition types stay off it.

### 5.3 Containers

- A container is an item that holds items. The character has equip slots (backpack,
  pouches, quiver), each filled by a container item; **items belong to a container, not to
  the character directly.**
- Each container has a capacity (slots and/or weight, per GDD §10) and an optional **type
  filter** (a quiver accepts arrows only). **Weight rolls up**: a container's effective
  weight = its own + its contents, so a full pack is heavy to carry and to drop.
- **Nesting is capped at one level** — the character holds containers, containers hold
  plain (non-container) items, no bags-in-bags. Keeps weight rollup and the drop/serialize
  path non-recursive for negligible loss.
- **Drop and pickup are free**: dropping a container moves the whole item-subtree to the
  world (a ground item carrying its contents); picking it up restores it intact, because
  the contents are part of that instance. Equipped containers live in the dreamer files;
  dropped/stored ones in the world file.

### 5.4 Networking

- **Wire format:** an instance crosses the wire as `defId + quantity + conditionStamp`
  (+ unique-instance id). Everything else — name, weight, behavior — is resolved from the
  shared def client-side. Uniques carry a small instance id so the client tracks "this axe"
  across moves; stacks are identified by container slot + (def, tier).
- **Replica model, per container:**
  - The dreamer's **own equipped containers** are replicated to that client (the host-auth-
    replica pattern used for needs and buffs), kept current by incremental add / remove /
    re-stamp deltas — never full-container refetches. Opening is instant.
  - **World and shared containers** (chests, dropped bags, the other dreamer's pack) are
    subscribe-on-open: replicated only while their UI is open or in proximity, then dropped.
    No client holds a live replica of every container in the world.
- **Mutation is one-way authoritative:** the client sends an **intent** (pick up / move /
  drop / use), the host validates and applies it to authoritative state, and the host's
  delta syncs back to the replica. The client may optimistically apply the change locally
  for responsiveness, with the host able to correct — the same prediction model as movement.

### 5.5 Time-cost actions & tasks

Nothing a dreamer does is instant. Every action and task costs time (and usually energy and
items), runs over a duration, and delivers its effect during or at the end of that window. The
day is a finite budget you allocate. This is the substrate the deferred Full Scheduler (§4 UI
backlog) eventually sits on, and it extends GDD §5's energy pool into energy + time.

#### 5.5.0 Core model
An action/task is a timestamp record — `{startTime, duration, …}`; "done" is `clock ≥ start +
duration`. It is never ticked per-record: it's computed from the synced authoritative clock,
the same pattern as spoilage (§5.2), the buff window (§4), and the clock itself. Exact on the
client (computed, not extrapolated), and free under skip and revert by construction.

#### 5.5.1 The day pool
Each dreamer has a **24h time pool** — a daily *labor budget*, not the wall-clock — that resets
at 00:00. Everything draws from it (walking, eating, crafting, felling, and sleep/rest/nap),
with each cost configured per-def. It is *parallelizable*: the two channels (and co-op) let a
dreamer spend labor faster than the clock advances, so the pool can run dry before midnight.

**Overdraft is gated.** By default a dreamer cannot commit work they can't pay for — hard stop
at zero. A per-def `allowsOverdraft` flag, set only on critical survival actions (eat, drink,
bandage, emergency snow shelter), lets them push past zero for the rare miscalculation that
would otherwise be lethal; overdraft applies an exhaustion debuff and is meant to be a painful
exception, not a strategy.

**Refill** is the full 24h at 00:00 for now. If the all-at-once grant feels off in playtesting
it may become segmented/rolling (e.g. 8h every 8h) — tunable, not locked.

#### 5.5.2 Channels
Two independent channels — **Action** (short, survival) and **Task** (long: craft/gather/
build/fell) — each with 1 active slot + FIFO queue that auto-advances. A dreamer runs at most
1 action + 1 task at once. In practice the action channel is sparse and the bulk of time goes
to tasks, so the parallel-overspend case is an edge, not the norm.

#### 5.5.3 Cost, reservation & payout
Time, energy, and item inputs are all reserved/debited at **assign** (queue), not when the slot
reaches the item. Progress = `(clock − start)/duration`. Two payout shapes:
- **Gradual** — installs a timed buff (reuse the §4 buff system) that the relevant resolver
  stage reads as a +rate. An apple = a 5-min nourishment buff at +10 nutrition/min. This is how
  0.1.1's placeholder meal finally becomes real: a rate over a window, not an instant bump.
- **End-payout** — nothing until completion, then deliver the output (spawn item(s), raise a
  structure, fell the tree).

#### 5.5.4 Cancel, pause & resume
Queued → full refund. In-progress → refund the **unelapsed** time + energy and materialize the
spent part as a resumable artifact:
- consumable → a partial item ("apple 30/50") — non-stackable, instance-level `remaining`;
- self-contained craft → an in-progress item in inventory;
- object-bound task → progress saved on the object (§5.6.5).

#### 5.5.5 Object-bound tasks & co-op labor accrual
Felling, station crafting, and building live on the MapEntity object: the dreamer commits labor
and is then free to walk away; the output waits **at the object** for collection. The object
carries `requiredLabor` + `accumulatedLabor` and accrues via piecewise-linear segments
`{segmentStart, rate, accumulatedAtStart}`, where rate = sum of active contributors (1× each,
later tool/skill-scaled). Completion is **re-projected only at contributor-change events**
(`= segmentStart + (required − accumulated)/rate`); between events it's the §5.6.0 timestamp
model. A single-contributor self-contained task is the one-segment degenerate case.

On assign a contributor sets `committedLabor` (default = remaining required), drawn from their
pool; they contribute until that's spent or the task completes, and unspent committed labor
refunds on completion (same rule as cancel). A task left short of `requiredLabor` simply sits
partial on the object, resumable by anyone — unifying pause/resume with co-op.

*Worked example:* a 4h tree. P1 fells [0→2] at 1× (2/4), gap [2→3] at 0× (P1 spent their 2h,
P2 still on a prior task), P2 fells [3→5] (4/4) → felled at 5h. Both felling together instead →
2× rate, done at ~2h.

#### 5.5.6 Where state lives
Self-contained records live on the dreamer (DreamerRecord); object-bound progress lives on the
MapEntity (§1.11 layer), so it rides per-map sync and the partner sees your half-felled tree.
The dreamer's task slot holds a handle while an object-bound task is active.

#### 5.5.7 Movement drain
Walking drains both **time and energy per distance** (rates in a movement config asset, not a
per-action def). It's a real-time-only per-tick debit (distance moved that tick → time +
energy) on the resolver; skip doesn't move the dreamer, so it doesn't apply during skip. This
makes trips and map size genuinely cost something — players plan routes and avoid wasted
travel, which feeds the migration / cross-map design (GDD §19–§20).

#### 5.5.8 Daylight, night & light (→ Cluster 5)
A per-def `timeOfDay` (Daylight | Night | Anytime) gates *when* a task may run against the
world clock's day; Night tasks burn a light-source consumable over their duration (the same
gradual-consume mechanic). Daylight bounds vary by season. This half depends on the world/
season system — ship the `timeOfDay` + light-source def fields now, inert (default Anytime),
and activate them when **Cluster 5** lands day-length and the season curve.

#### 5.5.9 Resolver, skip & save
One resolver still. Each tick it (a) applies active gradual buffs as need-rates and (b) fires
completions whose end ≤ now, in deterministic `(completionTime, owner, channel)` order — so
real-time and skip share one path and tick==jump holds ("queue a 4h build, skip 4h, it's done"
for free). A per-def `interruptsSkip` flag stops an in-flight skip on completion via the 0.1.4c
channel. Records are authoritative state → in the save (self in DreamerRecord, object in the
map layer); revert's clear-and-rehydrate restores in-flight work correctly because everything
is timestamp-derived. (This extends the resolver's per-tick work — new scope beyond the
original C2 plan, and it warrants its own build.)

#### 5.5.10 Networking
Host-authoritative: assign/cancel/complete are host operations; the client sends intents (the
pickup/drop flow from §5.4). A dreamer's owned action/task queues replicate to the owning
client; object-bound progress rides per-map sync. Progress bars are client-computed from the
synced records + clock — exact, no extrapolation.

#### 5.5.11 Per-def fields

| Field | Applies to | Meaning |
|---|---|---|
| `channel` | all | Action or Task |
| `timeCost` | action / self-contained | labor-hours = pool draw = duration at 1× rate |
| `energyCost` | all | energy drawn (GDD §5 pool) |
| `itemInputs` | consume / craft | items consumed on assign |
| `requiredLabor` | object-bound | total labor to complete (on object/recipe) |
| `isObjectBound` | tasks | self-contained vs object-bound |
| `payout` | all | Gradual (buff rate + window) / EndItem / EndEffect |
| `timeOfDay` | all | Daylight / Night / Anytime — inert until C5 |
| `lightBurn` | Night tasks | light-source consumed over duration — inert until C5 |
| `allowsOverdraft` | all | default false; true only for critical survival |
| `overdraftDebuff` | overdraft-allowed | debuff applied when overdrafted |
| `interruptsSkip` | all | default false; stops an in-flight skip on completion |

Movement uses a separate config asset: `timePerMeter`, `energyPerMeter`.

#### 5.5.12 Build phasing

| Piece | Lands |
|---|---|
| Day pool + reserve/spend/refund + overdraft gating + Action channel + eating (gradual buff) | 0.2.3 (action framework) — retrofits the shipped 0.2.2 instant consume |
| Movement drain (time + energy / distance) | 0.2.3 (with the action framework) |
| Task channel + object-binding + co-op labor accrual + in-progress/resumable | gathering / processing (0.2.7–0.2.8) |
| Daylight / night / light-source activation | Cluster 5 (season + day-length); def fields ship inert now |

### 5.6 Gathering

(Builds move to §5.8 — design subsections stay ahead of the build list.)

Gathering is a §5.5 object-bound task with a gathering-specific rate function. The §5.5 engine
(piecewise-linear labor accrual, re-projection at contributor-change events, commit/refund) is
reused as-is; gathering adds the rate function, the GatherableDef, and yields. A gatherable is
authored in-scene (collider + gatherable script + def + stable node id, identical on all
clients); its mutable state lives in the MapEntityLayer (§1.11).

#### 5.6.0 Gatherable object def
An interactable object (authored node or spawned processable) carries one or more **actions** —
most nodes have one (chop, mine), processables have several (a branch pile offers large/medium/
small-stick extraction; a fallen tree offers debranch + buck). Each action is its own
object-bound task:
- `requiredLabor` — labor to fully exhaust this action (HP for tool-required; base-minutes for
  tool-optional).
- `requiredToolType` + `toolOptional`, `defenseTier`, `energyPerMinute`.
- `yieldModel` — Proportional | Atomic (5.6.5).
- `yields` — list of `{ what, amount }`; `what` is an item def or (chains follow-on) a
  processable-object def; `amount` is the full payout at full depletion (rolled from the per-node
  seed), of which proportional actions pay the labor-fraction.
- `payoutDestination` — Inventory | Location.
- `coop`, `interruptsSkip`, `timeOfDay` (seam) — per action.
- `onDepletion` — Remove, or Transform → spawn the listed objects/items (chains follow-on; tree →
  fallen tree + stump).
- (optional, chains) `prerequisite` — an action may gate another (e.g. debranch before buck).

#### 5.6.1 Rate — tool-required
`rateresolver = defenseTier − toolAttackTier`; `rate = 1 − rateresolver × 0.1` (0 → 1×,
over-tier → >1×, under-tier → <1×). `rateresolver ≥ 10` is a start-time gate — the task is
refused with "a better tool is needed." Then `dmgPerMinute = baseChopDmg × rate × skillModifier`,
with `skillModifier` a seam defaulting to ×1 until Cluster 7. `baseChopDmg` and the tool's
attack tier are per-instance tool stats (rolled at craft time, 0.2.10; fixed/debug values here).

#### 5.6.2 Rate — tool-optional
Same engine, different rate function: `requiredLabor` as base-minutes, rate = `1 + optional
tool/skill bonus`, no defense/attack. Mushrooms = 10 min at rate 1; a knife adds a cutting-tier
bonus, foraging skill adds more (both seams until C7).

#### 5.6.3 Commitment resources — time, energy, durability
All three reserved on commit, spent proportionally, refunded on early-finish/cancel (§5.5):
- **Time** — committed minutes from the 24h pool.
- **Energy** — `energyPerMinute × committed minutes`.
- **Durability** — pre-debited from the tool by `committed minutes × lossPerMinute`, where
  `lossPerMinute = 1 + max(0, rateresolver)` (under-tier wears faster; over-tier floors at
  1/min). A commitment is capped by the tool's *available* durability (after prior
  reservations), so one tool can't be double-booked across queued tasks; the tool lands on the
  unused remainder and only breaks (0.2.6) when wear hits exactly 0.

#### 5.6.4 Co-op
`coop` resolves object reservation: `false` = single-occupancy (others see "in use"); `true` =
multi-contributor accrual. Assist = a §5.5 contributor-join (new segment, re-project);
cancel/leave = a contributor-leave (drop that rate, re-project). Each contributor's
time/energy/durability reservation is their own.

#### 5.6.5 Yield models + realization
Both models accrue labor toward `requiredLabor` on the same §5.5 engine; they differ in when
yield realizes:
- **Atomic** (tree) — realizes only at full depletion; partial labor is saved progress, no yield.
  On depletion the object is removed or transformed (5.6.0 `onDepletion`).
- **Proportional** (stone, berries) — realizes ∝ accrued labor; the reserve depletes as it goes;
  removed at full. Realization follows destination: Inventory trickles whole units as earned;
  Location drops a single pile at the node location that grows with accrued labor (all
co-op contributors feed the one pile), present throughout and collectible at any time.
Yield amounts come from the per-node seed; proportional actions pay the labor-fraction.

#### 5.6.6 Node state (MapEntityLayer, §1.11)
Authored in-scene: node id, def, position (never networked). Mutable state in the layer:
accumulated damage, depleted flag, regen timer, yield seed, and the active session (contributors
+ accrual segments). Synced per-map (both see the gather in progress) and saved (revert restores
an in-progress cut). Pristine nodes store nothing — O(damaged + active), not O(total).

#### 5.6.7 Processing chains
A yield can spawn a **processable world object** instead of (or alongside) items, and that object
is itself a gatherable-object def with its own `actions[]` — so resources flow through stages.

**Spawning a processable — two triggers, one mechanism.** An action's `yields` may list a
processable-object def (spawned at the source's location), and an `onDepletion = Transform` may
remove the source and spawn a set of objects/items. Both instantiate into
`MapEntityLayer.processables` (§1.11) at the spawn location. (Transform = whole-object replacement
on depletion; object-valued yield = a stage produced by one action while the source persists for
its other actions.)

**Multi-action objects.** A processable carries several independent actions; the player does all,
some, or part of one. Each action keeps its own §5.5 accrual state (separate accumulated labor /
reserve / session), its own tool/labor/yield, and can be co-op. An optional `prerequisite` gates
one action behind another (buck after debranch), else they're independent.

**Lifecycle.** A spawned processable is created at its location, processed action by action (each
yielding items or spawning further processables), and removed when its actions exhaust. A
partially-processed object persists in the layer, resumable by anyone — same timestamp-based
accrual, so revert/skip-safe.

**Worked chain (tree).** Tree (authored harvestNode, atomic) → on full fell, `onDepletion =
Transform` removes the node and spawns a *fallen tree* + a *stump* at its position. Fallen tree
(processable): `debranch` → a *branch pile* (object); `buck` → *logs* (items), gated behind
debranch. Branch pile (processable): three independent actions → large / medium / small *sticks*.
Each step is just another def on the same engine.

#### 5.6.8 Combat reuse
The tier resolution (`defense − attack → rate`) is the seed of Cluster 3 combat (creature
defense vs weapon attack is identical). Build it as a reusable Simulation resolver so combat
inherits it.


### 5.7 Crafting

Crafting is a recipe-driven configuration of the §5.5 task engine, not a new system. A recipe
consumes materials, time, and energy over a task, and at completion yields an output whose **tier**
is rolled from a skill-weighted table. It is the first stochastic system, so it also lands
RNG-as-state. Scope here is **item outputs** (tools, weapons, food, clothing, materials); world-object
outputs (workbenches, buildings, furniture, traps) wait on the placement/blueprint system (Cluster 4).

#### 5.7.0 The RecipeDef
A recipe is an SO — authoring a new craftable is a new RecipeDef (+ its output Defs), no code:
- `inputs` — a **list** of `{ itemDef, amount }` (§5.5.3), reserved on commit. Consumption code
  **iterates the list** — it never reads numbered fields (`input1`, `input2`) — so a recipe going
  from 2 materials to 5 is pure authoring, no code change.
- `requiredToolCategory` — empty = hand-craftable; else a tool of this category must be present.
- `requiredStation` — empty = anywhere; else the craft is an object-bound task on that station.
- `timeCost` / `energyCost` (hand) or `requiredLabor` / `energyPerMinute` (station) — §5.5.
- `coop` — §5.6.4 flag; single-crafter or combined-labor accrual.
- `unlockCondition` — discovery gate; **inert now** (starting recipes are `known`), activated at
  Cluster 9 (Tech).
- `tierTable` — the outcome model (§5.7.2).
- `rollGranularity` — `PerUnit` | `PerBatch` (§5.7.3): whether a multi-output craft rolls a tier
  per output unit or once for the whole batch.

#### 5.7.1 Requirement gates & execution
The three craft shapes are one task with optional gates:
- **Hand** (no tool, no station) — a self-contained §5.5 action/task on the dreamer.
- **Tool** (tool, no station) — same, gated on carrying a tool of `requiredToolCategory`.
- **Tool + station** (`requiredStation` set) — an **object-bound task on the station** (§5.6.5):
  commit labor, walk away, collect at the station. Identical to felling a tree.
Materials/time/energy reserve on commit and refund on early-finish/cancel (§5.5.3–5.5.4).
**Partial → resume** is §5.5.5 unchanged: a hand-craft materialises an in-progress item in inventory
(finish it later); a station craft saves progress on the station. "1h today, finish tomorrow" is
this rule.

#### 5.7.2 Tiers as outputs (the outcome model)
A craft does not roll a stat within a range — it rolls **which tier** it produced, from a
pre-authored list. Each tier is a **complete output Def** (its own stats, aspects, prefab, icon),
so tiers can differ in anything, not just a number.
- `tierTable` = an ordered list of `{ outputDef, weight }`. Adding a tier later = author one Def +
  add one entry. No stat schema, no code.
- The roll selects an **index** into the table (§5.7.3), weighted by skill (§5.7.4).
- **Stackable outputs stay stackable.** Because same-tier outputs are the *same Def*, a batch (20
  arrows) rolls a handful of discrete tiers and forms a few clean stacks — not 20 unique instances.
  Tiers give variety without shattering stacks (a tier *is* a stack key).
- **Broken is just the lowest table entry** — a Broken Def (§5.7.5) with a small weight; nothing
  special-cased.

#### 5.7.3 RNG-as-state (first stochastic system)
Per-craft deterministic seed — no global stream:
- `craftSeed = f(crafterId, recipeId, startTimestamp, craftCounter)`, where `craftCounter` is a
  small monotonic value in the save for uniqueness.
- The tier index is derived from `craftSeed` (a per-batch draw per output unit, or per-stack — see
  build), so the outcome is a pure function of saved state.
- **Rolled at completion**, so quality is unknown until the craft finishes.
- **Skip/revert-safe by re-derivation**: the craft record is in the save; replaying re-derives the
  same seed and the same tier. Reverting then re-crafting is a new record → a new roll (a reroll
  gated behind the dream/revert cost, not a cheat).
- `rollGranularity` controls the draw: **PerUnit** derives each unit's tier from
  `f(craftSeed, unitIndex)` — the unit index is the varying input, so all N rolls come from the one
  saved seed and re-derive identically on replay (a realistic tier spread → a few clean stacks);
  **PerBatch** draws once, `f(craftSeed)`, and stamps all N identical (one roll, one stack — for
  bulk low-stakes outputs). Both are pure functions of saved state; neither uses a stream.
This is the only genuinely new subsystem in the build; the heavier global-stream RNG-as-state stays
deferred to the first *continuous* stochastic process (weather/AI).

#### 5.7.4 Skill-weighted selection (seam)
Skill shifts the weight distribution over the tier table — unskilled crafters weight low tiers
(mostly T1, rare T3, occasional broken); skilled crafters weight high tiers. This reuses the §5.6
`skillModifier` seam (carried at a fixed unskilled distribution now), activated at Cluster 7. The
tier table ships with a fixed unskilled weighting until then.

#### 5.7.5 Broken items — a shared Def, two roads in
"Broken" is not a crafting concept or an instance flag — it is a **shared item state expressed as a
Def**, reached two ways:
- **Crafting** rolls it as the low `tierTable` entry.
- **Durability** (0.2.6): when a tool hits 0, the instance is **destroyed and replaced** by an
  instance of the tool Def's `brokenForm` (§5.7.6) at the same location.
A Broken Def carries no live Tool aspect (useless as a tool) but is **salvageable**: it exposes a
`dismantle` action (the §5.6 gather/process engine) yielding some materials back. So salvage needs
no new system. "Broken Tier 1" is a single stub tier for now; more broken tiers can be added later
like any tier.

#### 5.7.6 0.2.6 revision — durability break becomes destroy-and-replace
0.2.6's "enter broken state (flag)" is **retired**. The tool Def gains `brokenForm` (→ a Broken
Def); on durability 0 the tool instance is destroyed and a fresh `brokenForm` instance spawns at the
**same location** (container slot / world position / carried) — inheriting location only, not stats.
Different Def brings its own icon, prefab, aspects, and actions for free. There is no lingering
"0-durability tool" state, and revert needs no special-casing (revert before the break restores the
tool instance from the save; after, keeps the broken one) — it's just instances in the save.

#### 5.7.7 Output by aspect (why one system makes everything)
Crafting spawns the output Def; *what the output is* comes from its §1.12 aspects, already defined —
a crafted axe (Tool), food (Inventory+Consumable+Perishable), clothing (Inventory + a wearable
aspect), material (Inventory). Crafting adds no per-category code. World-object outputs
(workbench/building/furniture/trap) spawn a **blueprint** state handed to the Cluster 4 placement
system; a crafted workbench then *becomes a station other recipes require* — the system is
self-referential, and that falls out for free.

#### 5.7.8 Networking & save
Host-authoritative: commit/cancel/complete are host operations, client sends intents (§5.5.10). The
craft record (recipe, contributors, seed, accrual) is authoritative state — self-contained crafts on
the DreamerRecord, station crafts on the station (map layer) — so it saves and reverts by the
existing rules. Progress is client-computed from the synced record + clock.

#### 5.7.9 Deferred
Placement/blueprint for world-object outputs (Cluster 4); skill-shifted weighting (Cluster 7);
recipe unlocks (Cluster 9); repair of broken/damaged tools (a broken Def could later carry a
`repair` action, but salvage-only for now).


### 5.8 Build Breakdown

Cluster 2 builds, 0.2.x (continuing 0.0.x / 0.1.x). Each is 2-client validated via MPPM.
The first three deliver the playtest target: an inventory with items you can pick up, drop,
and consume — water and food in, needs refilled.

#### 0.2.0 — Item model + inventory (foundation) — detailed

The data + sync + save foundation for all of Cluster 2: define items, hold them in the
dreamer's single inventory container, replicate to the owning client, round-trip through
save/revert. No world items, pickup, consume, stacking, or spoilage yet — those are 0.2.1+.

##### 0.2.0a — Data model + def registry

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | **ItemDef** authoring SO: a stable numeric `defId` plus content fields (display name, category, weight, stackable, maxStack — more added by later builds). Author a few test defs. | Split — script: Claude Code; assets: Manual | Item defs author as SOs, each with a stable id. |
| a2 | **Def registry** built from the SO assets at load, exposed to Simulation through an `IItemDefLookup` interface so Simulation stays engine-free; the same registry resolves `defId` → SO for client display. | Claude Code | `defId` resolves to def data on the host (POCO via the interface) and to the SO on the client, identically. |
| a3 | **ItemInstance** POCO (Simulation): `defId`, logical `quantity` (precise), serializable. **Container** POCO: ordered list of ItemInstance + owner id, serializable. | Claude Code | A container holds item instances as pure Simulation data. |
| a4 | Add a single **inventory container** to the dreamer record in the RDM. | Claude Code | Each dreamer has an inventory container in authoritative state. |
| a5 | Host-side **debug add** API: add `(defId, quantity)` to a dreamer's inventory. | Split — logic: Claude Code; input: Manual | A dev action adds an item to a dreamer's inventory on the host. |

*0.2.0a acceptance:* a debug-added item sits in a dreamer's inventory container as
Simulation data, with `defId` resolving through the registry on both sides.

##### 0.2.0b — Owned-container replica (sync)

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Replicate the dreamer's **own** inventory to the owning client via the wire format — per-slot `defId + quantity` — as incremental add/remove deltas (§5.4), not full-container pushes. | Claude Code | The owning client holds a live replica that updates on change. |
| b2 | Minimal inventory display: client resolves `defId` → SO and lists contents (debug-level UI is fine). | Split — logic: Claude Code; UI: Manual | Host and owning client show the same inventory contents. |

*0.2.0b acceptance:* a host-side debug-add appears in the owning client's replica; contents
match on both. (Client → host intent is 0.2.1.)

##### 0.2.0c — Save + revert

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | Serialize the inventory (ItemInstances: `defId + quantity`) into the **dreamer file**; bump the dreamer-file schema version for the new field. | Claude Code | Inventory persists in the dreamer save; schema version bumped. |
| c2 | Load repopulates the inventory into the RDM. | Claude Code | Loading restores inventory contents. |
| c3 | Revert (0.0.5 apply path) **clears and rehydrates** the inventory from the snapshot — the Cluster-1 clear-and-rehydrate rule, not a merge. | Claude Code | Reverting restores inventory to its as-of-checkpoint contents; nothing post-checkpoint survives. |

*0.2.0c acceptance:* inventory round-trips through save/load and reverts cleanly to its
as-of-checkpoint contents, on two clients.

##### Key decisions

- **`defId` is a stable numeric id, not the asset name or reference.** Saves and the wire
  format reference it, so it must survive adding/reordering SO assets without breaking
  existing saves — assign it explicitly on the def, never derive it from asset order.
- **SO = authoring; Simulation reads def data through `IItemDefLookup`.** The SO can't live
  in Simulation (it needs UnityEngine), so a registry built from the SOs at load feeds
  Simulation pure def data via an interface, keeping the wall intact. Host and client build
  the same registry from the same assets — that's what makes `defId` resolve identically and
  lets the wire format ship ids, not strings.
- **One container only.** 0.2.0 instantiates a single inventory container per dreamer; the
  equip-slot / multi-container model (§5.3) is 0.2.4. The abstraction is built once, here.
- **Quantity is the precise logical value;** the display floor (§5.1) is a read-only
  projection, applied even by the minimal 0.2.0b display.

##### 0.2.0 acceptance (combined, 2-client)

A dreamer has an inventory container in authoritative state; a host-side debug-add places an
item in it; the owning client shows the same contents via its replica; and the inventory
round-trips through save/load and reverts to its as-of-checkpoint contents. `defId` resolves
through the shared registry on both sides. No world / pickup / consume yet.

#### 0.2.1 — World items + pickup + drop — detailed

Items can now exist on the ground (world state), and dreamers move them between world and
inventory. First build to use the client → host → replica intent flow (§5.4) in earnest, so
authority arbitration (range, races) lands here. Groups: world-item state + visuals (a),
pickup (b), drop (c). No capacity limit yet — that's 0.2.4.

##### 0.2.1a — World items (state, visuals, save)

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | World-items collection in the RDM world state: a list of `{ ItemInstance, position }`, authoritative, host-owned. | Claude Code | The world holds ground items as authoritative state. |
| a2 | Sync the collection to **all** clients (world state is shared, unlike owner-only inventory). | Claude Code | Both clients receive the world-items collection. |
| a3 | View-follows-state: each client spawns/despawns a local visual to match the synced collection. **Not** per-item NetworkObjects. | Split — logic: Claude Code; item visual prefab: Manual | A ground item shows on both clients; removing it from state despawns the visual. |
| a4 | Serialize the collection into the **world file**; bump the world-file schema version. Revert (0.0.5) clears and rehydrates it. | Claude Code | Ground items persist and revert to their as-of-checkpoint state. |
| a5 | Host-side **debug place** API: drop an `(defId, quantity)` item into the world at a position. | Split — logic: Claude Code; input: Manual | A dev action places a ground item. |

*0.2.1a acceptance:* a debug-placed ground item shows on both clients and round-trips through
save/load + revert. (No pickup yet.)

##### 0.2.1b — Pickup (intent → host, race-safe)

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Interaction: target a nearby world item (proximity + interact input) and send a **pickup intent** to the host. | Split — logic: Claude Code; input: Manual | A dreamer near a ground item can issue a pickup. |
| b2 | Host validates and applies: dreamer in range, **item still exists**; move the ItemInstance world → that dreamer's inventory; sync the collection removal (all clients) + inventory delta (owner). | Claude Code | A valid pickup moves the item; the ground visual despawns everywhere and it enters the picker's inventory. |
| b3 | Race safety: a concurrent pickup of the same item by both dreamers resolves to one winner; the loser finds the item gone and no-ops. | Claude Code | Two simultaneous pickups give the item to exactly one dreamer. |

*0.2.1b acceptance:* a dreamer picks up a ground item — it leaves the world on both clients and
enters the picker's inventory; a simultaneous double-pickup yields one owner, no duplication.

##### 0.2.1c — Drop

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | **Drop intent** → host: remove the item (or quantity) from the dreamer's inventory and add it to the world-items collection at the dreamer's position; sync inventory delta (owner) + collection add (all). | Claude Code | A dropped item leaves inventory and appears on the ground for both clients. |

*0.2.1c acceptance:* a dreamer drops an item — it leaves inventory and appears in the world on
both clients; the full pickup ↔ drop round-trip is save/revert-clean.

##### Key decisions

- **World items are authoritative world state, not per-item NetworkObjects.** They live in the
  RDM / world file and sync as a collection; clients spawn local visuals to match. The
  world-item analog of "authoritative state isn't per-object NetworkVariables" — it keeps
  revert clean (rehydrate the collection, no NetworkObject spawn/despawn churn).
- **Pickup is a client intent the host arbitrates, and must be race-safe.** First valid intent
  wins; a concurrent second finds the item gone and no-ops. First real use of the §5.4 flow.
- **Revert reverses pickup/drop for free.** Both the inventory and the world-items collection
  clear-and-rehydrate from the snapshot, so an item picked up after a checkpoint returns to the
  ground (and leaves inventory) on revert, with no special handling.
- **No capacity check yet** — pickup succeeds if the item exists and the dreamer is in range;
  weight/slots arrive in 0.2.4.

##### 0.2.1 acceptance (combined, 2-client)

A ground item placed in the world shows on both clients; a dreamer picks it up (it leaves the
world everywhere and enters their inventory), concurrent pickups yielding exactly one owner; a
dreamer drops an item (it leaves inventory and reappears on the ground for both); and the whole
pickup ↔ drop cycle round-trips through save/load and reverts to its as-of-checkpoint state.


#### 0.2.2 — Stacking + consume → needs — detailed

Food and water finally feed the Cluster-1 needs system, the 0.1.1 placeholder meal/drink tasks
become real item consumption, and the deferred save-consumable gets its real item form. Groups:
stacking + two-level quantity (a), consume → needs (b), the save-consumable wrapper (c). Consume
reuses the client → host → replica intent flow from 0.2.1.

##### 0.2.2a — Stacking + two-level quantity

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | Stacking rule: an add merges into an existing stack iff **same def AND same condition bucket**, up to the def's max stack; else a new stack. Non-stackable uniques never merge. | Claude Code | Stackables merge by (def, bucket); uniques stay qty-1 and separate. |
| a2 | Two-level quantity: logical quantity (RDM/save) precise; display quantity a read-only floor. All arithmetic (merge, split, consume) runs on the logical value. | Claude Code | Inventory math uses the precise value; the UI shows its floor. |
| a3 | Split: divide a stack into two of the same (def, bucket), for partial drop/trade later. | Claude Code | A stack can be split. |

> Condition buckets don't *vary* until spoilage (0.2.3); until then all condition-bearing items
> sit in one bucket, so this is effectively merge-by-def — but the (def, bucket) key is built
> now, so 0.2.3 changes nothing structural.

*0.2.2a acceptance:* stackables merge by def, uniques stay separate, the display floors the
logical quantity, stacks split; save/revert-clean on two clients.

##### 0.2.2b — Consume → needs

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Add **consume-effect** fields to ItemDef for consumables: which need(s) and how much each restores. | Split — script: Claude Code; def values: Manual | A food/water def declares its need restoration. |
| b2 | **Consume intent** (client → host): use an item → host validates the dreamer has it → applies the need refill (Cluster-1 needs) and decrements/removes the item → syncs the need update + inventory delta. | Claude Code | Consuming food/water refills the need on both clients and reduces the stack. |
| b3 | Realise the 0.1.1 placeholder: eating/drinking is now item consumption, not a direct need bump. (Consume is instant for now; eating-as-a-timed-task is a later refinement.) | Claude Code | The 0.1.1 placeholder meal/drink path is replaced by item consumption. |

*0.2.2b acceptance:* consume food → hunger refills; water → thirst refills; both on two clients;
the item decrements; save/revert-clean.

##### 0.2.2c — Save-consumable item wrapper

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | A consumable whose consume-effect is "fire the dream-save checkpoint" — on consume it calls the existing Cluster-1 host RPC, replacing the debug `UseDebugSaveConsumable`. Backend already complete (0.1.5c). | Claude Code | Consuming a save-consumable banks a personal checkpoint and consumes the item. |

*0.2.2c acceptance:* a dreamer consumes a save-consumable → a valid personal checkpoint is banked
(per the 0.1.5 validity query) and the item is consumed; on two clients.

##### Key decisions

- **Stacking keys on (def, condition bucket)** even though the bucket is constant until spoilage
  (0.2.3) — built now so spoilage adds only varying buckets, no structural change.
- **Consume is a client intent the host applies** — the same authoritative flow as pickup/drop,
  no new networking pattern.
- **Consuming realises the 0.1.1 placeholder** — needs are refilled by real items now. Consume
  is instant in 0.2.2; eating over time (a task) is a later refinement.
- **The save-consumable wrapper is pure wiring** — the checkpoint backend was finished in
  Cluster 1; 0.2.2 only routes a real item to it.

##### 0.2.2 acceptance (combined, 2-client)

Stackables merge by def (uniques stay separate), the display floors the logical quantity, stacks
split; consuming food refills hunger and water refills thirst while decrementing the item;
consuming a save-consumable banks a valid personal checkpoint; all save/revert-clean.

#### 0.2.3 — Action framework (time-cost actions) — detailed

The §5.5 time system, action half. Builds the 24h day pool, the Action channel, cancel/refund
with partial consumables, the save-consumable retrofit, and movement drain. Retrofits the
shipped 0.2.2 instant consume into a timed action. Everything downstream (the task half,
gathering, crafting) leans on this.

##### 0.2.3a — Day pool

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | Pool storage: each DreamerRecord holds `timePool` (remaining labor-hours, authoritative state) with commit/debit, refund, and query API. | Claude Code | A dreamer's pool debits on commit and refunds correctly. |
| a2 | Overdraft gating: a commit needs `pool ≥ timeCost` unless the def sets `allowsOverdraft`; an allowed overdraft drives the pool negative and applies the exhaustion debuff (a §4 negative buff). | Split — logic: Claude Code; debuff tuning: Manual | Non-overdraft actions are rejected at 0; flagged ones go negative + apply the debuff. |
| a3 | Midnight reset: a resolver stage detects the world-clock day rollover and sets the pool to full 24h, clearing any negative (the debuff was the cost, not a carried debt). Fires once per crossing in real-time and once per crossing inside a chunked skip. | Claude Code | Pool returns to 24h at each in-game midnight, including across a multi-day skip. |
| a4 | Energy draw: the same commit/refund path debits `energyCost` from the GDD §5 energy pool. *If the energy pool isn't built, `energyCost` ships inert — confirm.* | Split — Claude Code; confirm energy-pool state: Manual | Energy is debited alongside time where the pool exists. |
| a5 | Save/revert: the pool is in DreamerRecord, so it rides the existing clear-and-rehydrate. | Claude Code | Pool restores exactly on revert. |

*Acceptance:* pool starts at 24h, debits on commit, blocks non-overdraft commits at 0, flagged
commits overdraft + debuff, resets at midnight (including through a skip); save/revert-clean on
two clients.

##### 0.2.3b — Action channel + eating (gradual payout)

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Action record `{defId, owner, channel=Action, startTime, duration, reservedTime, reservedEnergy, consumedItems, payout}` in DreamerRecord. | Claude Code | An action is a timestamp record in authoritative state. |
| b2 | Single active slot + FIFO queue per dreamer; reserve-on-assign debits pool/energy/items at queue time; auto-advance to the next queued action on completion. | Claude Code | Queued actions run one at a time and auto-advance. |
| b3 | Resolver completion: each tick fires actions whose `end ≤ now` in deterministic `(end, owner)` order; the client computes progress `(clock − start)/duration` from the synced clock. | Claude Code | Actions complete on time; progress matches on both clients. |
| b4 | Eating retrofit: the 0.2.2 consume intent stays; the host's apply now creates a timed action that installs a §4 nourishment buff (rate + window) instead of refilling instantly. | Claude Code | Eating queues a timed action, not an instant refill. |
| b5 | Resolver reads the nourishment buff: the needs/meal stage applies active nourishment buffs as a +rate per tick — realizing the 0.1.1 placeholder. | Claude Code | Hunger rises over the eating window; a skip across it lands the right value. |

*Acceptance:* queue two apples → the first runs its window feeding hunger gradually, then the
second auto-starts; progress matches on both clients; a skip across the window lands the correct
hunger; save/revert mid-eat resumes correctly.

##### 0.2.3c — Cancel, refund & partial consumables

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | Cancel a queued action → full refund of time/energy/items. | Claude Code | Cancelling a queued eat returns the whole apple + full costs. |
| c2 | Cancel an in-progress action → refund the unelapsed time/energy and stop the payout (remove the nourishment buff). | Claude Code | Cancelling mid-eat refunds the unspent portion and halts further nutrition. |
| c3 | Partial consumable: the cancelled buff's remaining value spawns a non-stackable instance carrying `remaining` — a distinct item from the fresh original (§5.1/§5.5). | Claude Code | Cancelling at 40% yields an "apple 30/50" that won't stack with fresh apples. |
| c4 | Re-consume a partial: eating it delivers only its `remaining`, over a proportionally shorter window. | Claude Code | Eating the 30/50 apple refills 30 and is gone. |

*Acceptance:* cancel queued → full apple back; cancel mid-eat → partial apple + unspent refund +
nutrition stops; re-eat the partial → remaining delivered; all save/revert-clean on two clients.

##### 0.2.3d — Save-consumable retrofit (end-payout)

| ID | Task | Executor | Done when |
|---|---|---|---|
| d1 | End-payout shape (fired once at completion, vs gradual): the 0.2.2c save-consumable becomes a timed action that fires the existing dream-save host RPC at `end` instead of instantly. | Claude Code | Consuming a save-consumable runs its window, then banks the checkpoint at completion. |
| d2 | Cancel mid-consume → no checkpoint, refund unspent + partial item (reuses 0.2.3c). | Claude Code | Cancelling before completion banks nothing and refunds correctly. |

*Acceptance:* consume a save-consumable → after its window a valid personal checkpoint is banked
(0.1.5 validity); cancel before completion → no checkpoint; both on two clients; save/revert-clean.

##### 0.2.3e — Movement drain

| ID | Task | Executor | Done when |
|---|---|---|---|
| e1 | Movement config asset: `timePerMeter`, `energyPerMeter`. | Manual | Tunable rates exist. |
| e2 | Per-tick drain: the resolver reads each dreamer's distance moved since last tick (from the synced transform) and debits time + energy. Real-time only — skip doesn't move dreamers, so no drain during skip. | Claude Code | Walking steadily drains pool + energy; skipping doesn't. |
| e3 | Movement never hard-blocks: walking past zero overdrafts and applies the exhaustion debuff. | Claude Code | A tapped-out dreamer can still walk, but accrues the debuff. |

*Acceptance:* walking drains time + energy proportional to distance on both clients; standing
still doesn't; a skip causes no movement drain; walking while tapped out overdrafts into the
debuff; save/revert-clean.

##### Key decisions

- The pool is **authoritative DreamerRecord state modified by events** (commit/refund/reset),
  not a timestamp-derived value — so it's in the save, and the midnight reset is a resolver
  stage made skip-safe by the chunked loop.
- **Overdraft's cost is the exhaustion debuff, not a carried time-debt** — the midnight reset
  always returns the pool to a full 24h.
- **Eating flows through the existing §4 buff system + needs resolver** (a +rate buff), not a
  bespoke path — and this is how the 0.1.1 placeholder finally becomes real.
- **Two payout shapes only:** gradual (installs a buff — eating) and end-payout (fires once at
  completion — save-consumable). Crafting (0.2.10) reuses the end-payout path.
- **Movement is the one consumer that never hard-blocks** — freezing a player is worse than
  letting them overdraft.
- `timeOfDay` / daylight / light-source stay inert until Cluster 5.

##### 0.2.3 acceptance (combined, 2-client)

The pool runs the day (debits, blocks, overdrafts where flagged, resets at midnight through
skips); eating is a timed action that feeds hunger gradually over its window and auto-advances
through a queue; cancelling refunds the unspent cost and returns a tracked partial; the
save-consumable banks its checkpoint at completion; walking drains time + energy with distance;
everything is exact on both clients and survives save/revert.

#### 0.2.4 — Spoilage (best-before) — detailed

The re-stampable condition triple (§5.2). Freshness is computed from the clock on read, shown
on items, re-stamped only when the storage rate changes, with rotten-at-0 handling. No tick, no
resolver stage, no sync of the derived value — only the triple is stored and synced, and it
changes rarely.

##### 0.2.4a — Condition triple + formula

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | Perishable ItemDef gains `perishable` + `effectiveLifespan`. ItemInstance carries the triple `{conditionAtStamp, stampTime, rate}`, initialised `{100, now, 100/lifespan}`. | Split — schema: Claude Code; lifespan values: Manual | A perishable item is born fresh with its triple set. |
| a2 | Compute `condition(now) = clamp(conditionAtStamp − (now − stampTime) × rate, 0, 100)`, reading the authoritative clock (Simulation layer, pure C#). Units of `rate` match the clock. | Claude Code | An item's computed condition decreases by the formula as the clock advances. |
| a3 | Spoiled state = `condition ≤ 0` (computed on read, no event needed). | Claude Code | An item reads as spoiled exactly when its computed condition reaches 0. |

*Acceptance:* a perishable item's freshness falls by the formula over time and reads identically
on both clients (computed, never synced); it reaches spoiled at 0.

##### 0.2.4b — Re-stamp + rate-modifier seam

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Rate-modifier seam: effective `rate = (100/lifespan) × storageModifier`, where `storageModifier` is read from the item's current storage (default 1 = ambient). | Claude Code | An item's rate reflects a storage modifier; ambient defaults to base rate. |
| b2 | Re-stamp on rate change: when the storage modifier changes, set `conditionAtStamp = condition(now)`, `stampTime = now`, `rate = new`. Triggers only on the change, never per-tick. | Claude Code | A modifier change re-stamps once; condition is continuous across it (no jump). |
| b3 | Debug trigger to change an item's storage modifier (real containers wire this at 0.2.5). | Claude Code | Re-stamp can be exercised in test without containers existing yet. |

*Acceptance:* changing an item's modifier re-stamps it and changes the aging slope from that
moment, with no discontinuity in the current condition; it re-stamps only on the change.

##### 0.2.4c — Stacking by live bucket + merge reconciliation

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | Condition → bucket mapping (e.g. Fresh / Good / Stale / Spoiled); thresholds tunable. | Split — logic: Claude Code; thresholds: Manual | Each condition value maps to a bucket. |
| c2 | Stacking keys on `(def, live bucket)` — activating the 0.2.2 seam now that buckets vary. A stack shares one triple, so it spoils as a unit and crosses bucket boundaries whole (no splitting). | Claude Code | Same-def items merge only when in the same live bucket; a stack changes bucket as one. |
| c3 | Merge reconciliation: merging two perishable stacks in the same bucket re-stamps the result to the **lower** condition (anti-cheese — fresh food can't refresh old). | Claude Code | Merging a fresh and an older stack yields the older condition. |

*Acceptance:* same-age stacks merge; a fresh + a day-old stack in the same bucket merge to the
worse condition; once they fall into different buckets they won't merge; a spoiling stack moves
buckets as a unit. Save/revert-clean on two clients.

##### 0.2.4d — Spoiled handling + display

| ID | Task | Executor | Done when |
|---|---|---|---|
| d1 | Consuming spoiled food yields no nutrition and applies a tunable sickness debuff (a §4 debuff; severity/duration Manual, can ship at 0). | Split — logic: Claude Code; tuning: Manual | Eating a spoiled item gives no nutrition (+ sickness if enabled). |
| d2 | Display: expose computed condition + bucket for the HUD (placeholder OnGUI). | Claude Code | Freshness shows on items, identical on both clients. |

*Acceptance:* food at 0 reads spoiled and feeds nothing when eaten; freshness displays match
exactly across clients.

##### Key decisions

- **Rate lives in the instance triple, not just the def** — storage modifiers make it
  per-instance, and storing it keeps the formula self-contained.
- **Re-stamp is the only write** — condition, bucket, and spoiled-state are all computed on
  read, so there's no tick, no resolver stage, and no sync of derived values.
- **Merges reconcile to the worse condition** — prevents laundering old food into a fresh stack.
- **Real storage modifiers attach at 0.2.5** — the seam and re-stamp mechanism land here; the
  cold-container value plugs in with containers.
- Skip and revert need no special handling: the triple is in the save and condition is a pure
  function of the clock, so a skip lands the right value and a revert restores as-of-checkpoint
  freshness automatically.

##### 0.2.4 acceptance (combined, 2-client)

Food ages by the formula, exact on both clients with no drift; freshness shows on items;
re-stamping on a modifier change bends the slope without a jump; perishable stacks merge only
within a live bucket and reconcile to the worse condition; spoiled food feeds nothing; a skip
lands freshness at the right value and a revert restores it as-of-checkpoint.

#### 0.2.5 — Multiple containers + equip slots — detailed

A container is an item that holds items; the dreamer has typed equip slots that hold
containers; items belong to a container, not the dreamer (§5.3). Extends the single 0.2.0
inventory into the full multi-container model, with the cold-storage modifier from 0.2.4 plugged
in.

##### 0.2.5a — Container model + equip slots

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | ContainerDef fields: `slotCapacity`, `weightCapacity`, `typeFilter`, `spoilageModifier` (default 1). A container is an item instance carrying a contents list. | Split — schema: Claude Code; values: Manual | Backpack / pouch / quiver exist as container items with their own capacity + filter. |
| a2 | Dreamer equip slots (authored set: backpack, pouches, quiver) in DreamerRecord; equip / unequip a container into a matching slot (client intent → host → sync). | Split — logic: Claude Code; slot layout: Manual | A dreamer equips multiple containers into typed slots. |
| a3 | Ownership chain: items belong to a container, not the dreamer (equip slot → container → items). The 0.2.0 single inventory migrates to a default container. | Claude Code | Every item lives in a container; the old inventory is folded into the new model. |
| a4 | One-level nesting cap: a container may sit in a container, but that nested container holds no further containers. | Claude Code | A pouch fits in a backpack; a third level is rejected. |
| a5 | Multiple owned containers replicate to the owning client (extends the 0.2.0 owned-container replica). | Claude Code | All of a dreamer's equipped containers + contents sync to them. |

*Acceptance:* equip several containers into typed slots, nest one level (not two), items live in
containers; save/revert-clean on two clients.

##### 0.2.5b — Capacity, filters & weight rollup

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Insert/move validation (host-side): reject anything exceeding `slotCapacity` or `weightCapacity`, or violating `typeFilter`. | Claude Code | A full or filtered container rejects the insert. |
| b2 | Weight rollup: container weight = own + Σ(contents) over one nesting level; dreamer carried weight = Σ(equipped rolled-up). | Claude Code | Loading a container raises its rolled-up weight and the dreamer's carried weight. |
| b3 | Expose carried weight for the HUD. Encumbrance *effects* are left as a seam — natural home is scaling 0.2.3e movement drain by carried weight. | Claude Code | Carried weight shows, identical on both clients. |

*Acceptance:* type filters enforced (a quiver takes only arrows), a full pack rejects more,
carried weight rises with load and matches across clients.

##### 0.2.5c — Drop & pickup with contents

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | Drop a loaded container → it and its contents (one nesting level) leave the dreamer and become one world item in groundItems (§1.11 / 0.2.1), serialized as a unit. | Claude Code | Dropping a loaded backpack removes it + contents and places it in the world. |
| c2 | Pick up a container-with-contents → it returns to an equip slot or parent container with contents intact (host-arbitrated, race-safe as in 0.2.1). | Claude Code | Picking it up restores the container and everything in it. |

*Acceptance:* drop a loaded backpack, walk away, pick it up — contents intact; save/revert
mid-state clean; both clients.

##### 0.2.5d — Storage-modifier integration (with 0.2.4)

| ID | Task | Executor | Done when |
|---|---|---|---|
| d1 | A container's `spoilageModifier` feeds the 0.2.4b rate-modifier seam: moving a perishable into or out of a container re-stamps it with the new effective rate. | Claude Code | Food entering a cold container re-stamps and ages slower; leaving re-stamps it back. |

*Acceptance:* place food in a cold container → it re-stamps once and its aging slope drops;
remove it → re-stamps back to ambient. The 0.2.4b debug trigger is now superseded by real
containers.

##### Key decisions

- **Items belong to containers, not the dreamer** — the 0.2.0 inventory becomes a single
  default container under the new model (a state migration; schema bump, old saves rejected).
- **Nesting is hard-capped at one level** (backpack → pouch → items, no deeper) — bounds weight
  rollup and serialization.
- **A dropped container serializes with its contents as one unit**, so drop/pickup stays free —
  no per-item world spawning.
- **Weight is rolled up and shown now; encumbrance effects are deferred to tuning**, with the
  natural hook being 0.2.3e movement drain scaled by carried weight.
- **Containers supply the real spoilage modifiers** the 0.2.4 seam was built around.

##### 0.2.5 acceptance (combined, 2-client)

Equip multiple typed containers, nest one level only, with items living in containers; capacity
and filters are enforced and carried weight rolls up and displays; drop a loaded container and
pick it back up with contents intact; perishables re-stamp to the container's modifier; all
save/revert-clean.

#### 0.2.6 — Durability (use-events) — detailed

Tools and gear carry per-instance durability that decrements on **use events**, not over time
(§5.2) — the opposite of spoilage. Off the tick entirely; plain state mutated on discrete uses.

##### 0.2.6a — Durability value + use-decrement

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | Tool ItemDef gains `maxDurability` (+ a durability flag). The instance is a continuous-durability unique (§5.1 — qty-1, never stacks) carrying current durability as per-instance state. | Split — schema: Claude Code; values: Manual | A tool is born at full durability and won't stack. |
| a2 | Use-decrement API (host-applied): a use event reduces durability by an amount, clamped at 0. | Claude Code | Firing a use event lowers the tool's durability. |
| a3 | Debug use-trigger to fire uses in test; durability replicates to the owner and shows on the tool. Real wear-sources wire at 0.2.7 (felling) / 0.2.8 (crafting). | Claude Code | Debug uses drop durability; the value matches on both clients. |
| a4 | Save/revert: durability is per-instance state, so it rides clear-and-rehydrate. | Claude Code | Durability restores exactly on revert. |

*Acceptance:* debug use-events wear a tool down; durability shows identically on both clients;
save/revert restores the value.

##### 0.2.6b — Breaking at 0

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | At durability 0 the tool enters a broken state (flagged); further use is rejected. | Claude Code | A worn-out tool breaks and can't be used. |
| b2 | A broken tool stays in inventory as a broken instance (display shows broken); salvage/repair deferred. | Claude Code | The broken tool remains, visibly broken, not silently destroyed. |
| b3 | Revert before the break restores the unbroken tool — plain state, no special handling. | Claude Code | Reverting to before the break yields a usable tool again. |

*Acceptance:* wear a tool to 0 → it breaks and use is rejected → it stays as a broken item →
revert to before the break → usable again; both clients.

##### Key decisions

- **Event-driven, not time-computed** — durability is decremented on use, never ticked. Plain
  per-instance state in the save (contrast spoilage's clock-computed condition).
- **Wear comes from the using action/task, not the tool** — discrete actions wear a flat amount
  per use; object-bound labor tasks (0.2.7) wear proportional to labor contributed. The
  decrement API just takes an amount; real wear-sources wire at 0.2.7/0.2.8 (the same
  mechanism-then-real-source pattern as 0.2.4).
- **Break is a reversible state, not a permanent event** — a revert before the break un-breaks
  the tool automatically, because durability is just state.
- **Break-mid-task handling is deferred to 0.2.7** — when felling first uses a tool, it decides
  what happens if the tool breaks mid-task (stall, continue slower, etc.).
- **Salvage / repair deferred** — a broken tool just sits broken for now.

##### 0.2.6 acceptance (combined, 2-client)

Debug use-events wear a tool down with the value matching on both clients; at 0 it breaks and
refuses further use while remaining a visible broken item; a revert before the break restores a
usable tool; all save/revert-clean.

#### 0.2.7a — MapEntityLayer migration — detailed

Restructure the 0.2.1 world-items-as-state into the unified MapEntityLayer (§1.11), single-map
slice. WorldItemsSync becomes the layer's `groundItems` sub-system; MapEntitySync replaces it.
No behavior change — this just rehomes existing state and prepares the layer for harvest nodes.

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | `MapEntityLayer` keyed by `mapId`, holding an extensible sub-collection framework (`groundItems` now; harvestNodes/animals/buildings/placedItems added by later builds without restructuring). The RDM holds one layer per loaded map (one map for now). | Claude Code | The RDM holds a MapEntityLayer for the current map, with a sub-collection framework. |
| a2 | Migrate `groundItems`: move `WorldState.worldItems` + `nextWorldItemId` into `MapEntityLayer.groundItems`. Spawn / pickup (race-safe, 0.2.1b3) / drop behaviour is unchanged — only the home moves. | Claude Code | World items live in the layer; pickup and drop behave exactly as in 0.2.1. |
| a3 | Replace the `WorldItemsSync` NetworkBehaviour with `MapEntitySync` (one per loaded map), owning the per-map sub-collection sync (groundItems now). | Claude Code | One MapEntitySync drives the map's entity sync; WorldItemsSync is removed. |
| a4 | Serialize the layer to `map_{id}.json` alongside `world.json`; revert clears-and-rehydrates it, as DreamerRecords do. | Claude Code | The layer round-trips to its own file and reverts clean. |
| a5 | Schema version bump; a pre-migration save is rejected loudly (dev behaviour). | Claude Code | Loading an old save is rejected. |

##### Key decisions

- **Pure refactor, no behaviour change** — world items (spawn / race-safe pickup / drop) must
  behave identically afterward; only the home (layer), sync owner (MapEntitySync), and file
  (`map_{id}.json`) change.
- **Single-map slice only** — the layer is keyed by `mapId` and MapEntitySync is per-map, but
  per-map sync *isolation*, multiple live layers, and inactive-layers-on-disk are not built here;
  they activate at Cross-map (Cluster 6), where a second map validates them.
- **Sub-collection framework is extensible** so 0.2.7b/c add `harvestNodes` (with its richer
  payload — accumulated damage, yield seed, active session) as a new sub-collection without
  restructuring.
- **Schema bump, old saves rejected** — consistent with prior builds.

##### 0.2.7a acceptance (2-client)

World items spawn, pick up race-safely, and drop exactly as in 0.2.1 — now through the
MapEntityLayer and MapEntitySync; the layer saves to `map_{id}.json` and revert
clear-and-rehydrates it; a pre-migration save is rejected. No new gameplay — this is the
foundation 0.2.7b's harvest nodes sit on.

#### 0.2.8 — Processing chains — detailed

Extends gathering (§5.6.7) with spawned processable objects and multi-action processing. Reuses
the §5.5 accrual engine and the §5.6 gatherable-object def; adds the `processables` sub-collection,
the spawn-a-processable mechanism, multi-action handling, and the tree chain as validating content.

##### 0.2.8a — Processables sub-collection + spawn mechanism

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | Add `processables` to the MapEntityLayer (§1.11 update): runtime-spawned object-bound-task targets, full position + state synced (the groundItems/animals profile), consumed when exhausted (no regen). MapEntitySync's handling of the new sub-collection follows the audit's subscription discipline — any client-path `OnValueChanged` guarded and torn down on despawn. | Claude Code | The layer holds a processables sub-collection that syncs and saves like groundItems, with no new orphaned subscription. |
| a2 | Spawn-a-processable mechanism: instantiate a processable from a def at a given location into `processables`. One mechanism, two callers (a3, a4). | Claude Code | A processable can be spawned at a location and appears on both clients. |
| a3 | `onDepletion = Transform`: on an action's full depletion, remove the source object and spawn the listed objects/items at its position. | Claude Code | A node set to Transform is removed on depletion and spawns its objects/items. |
| a4 | Object-valued yields: an action `yields` entry whose `what` is a processable-object def spawns that processable while the source persists for its other actions. | Claude Code | An action can produce a processable while the source object remains. |
| a5 | Save/revert: processables ride `map_{id}.json` clear-and-rehydrate; timestamp accrual restores in-progress state. | Claude Code | A spawned processable round-trips and reverts mid-process. |

*Acceptance:* a debug Transform/spawn produces a processable at the right position on both clients;
it saves and reverts cleanly; no new subscription leak.

##### 0.2.8b — Multi-action objects

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Per-action accrual: each entry in `actions[]` runs on its own §5.5 accrual state (separate accumulated labor / reserve / session). | Claude Code | Two actions on one object progress independently. |
| b2 | Action selection: the player chooses which action(s) to run — all, some, or part of one. | Split — logic: Claude Code; selection UI: Manual (placeholder) | A player can start any available action on an object. |
| b3 | Prerequisites: an action's `prerequisite` gates it behind another (buck after debranch); independent otherwise. | Claude Code | A gated action is unavailable until its prerequisite completes. |
| b4 | Co-op per action: each action reuses the §5.5 coop/assist accrual independently. | Claude Code | Two players assist on one action; different actions run independently. |

*Acceptance:* a multi-action debug object — start any action, prerequisites enforced, actions
progress independently, co-op works per action, partial persists; save/revert-clean.

##### 0.2.8c — The tree chain (validating content)

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | Tree node `onDepletion = Transform` → fallen tree + stump at the node's position. | Split — wiring: Claude Code; defs/values: Manual | Felling a tree removes the node and spawns a fallen tree + stump. |
| c2 | Fallen-tree def: `debranch` → branch pile (object); `buck` → logs (items), buck gated behind debranch. | Manual (defs) | The fallen tree offers debranch then buck, yielding a branch pile and logs. |
| c3 | Branch-pile def: three independent actions → large / medium / small sticks (items). | Manual (defs) | The branch pile offers three stick actions, each yielding its sticks. |

*Acceptance:* fell a tree → fallen tree + stump; debranch → branch pile; buck → logs; the branch
pile's three actions each yield their sticks; do all/some/part; partial persists, resumable, co-op;
save/revert-clean.

##### Key decisions

- **One spawn mechanism, two triggers** — `onDepletion = Transform` (whole-object replacement) and
  object-valued yields (a stage produced while the source persists) both instantiate into
  `processables`.
- **processables ≠ harvestNodes in storage** — spawned, full-position-synced, consumed (no regen),
  while sharing the actions/accrual engine and the object def.
- **Per-action accrual** — a multi-action object tracks each action separately; §5.5 co-op and
  prerequisites apply per action.
- **The chain is content on the engine** — each stage is another gatherable-object def; no new
  systems beyond the spawn mechanism and multi-action handling.
- **No new subscription leak** — the processables sync reuses MapEntitySync under the post-audit
  lifecycle discipline (guarded client subscriptions, base despawn cleanup).

##### 0.2.8 acceptance (combined, 2-client)

A felled tree transforms into a fallen tree + stump; the fallen tree debranches into a branch pile
and bucks into logs (buck gated behind debranch); the branch pile's three actions each yield their
stick types; any stage can be done partially, persists, and is resumable and co-op; everything
saves and reverts cleanly through the processables sub-collection.

#### 0.2.9 — Unified world-object model (refactor) — detailed

Consolidate the item, gatherable, and world-object systems into the §1.12 Def/Instance/Action
model. No new gameplay — the bar is that every prior behaviour works unchanged through the new
types. a/b/c are **done**; the remaining code is **d + e**, and the refactor is validated as **one
build**: all code lands first, then a single consolidated setup + regression (see Execution
strategy below). Authored placement (former e2) is split out to **0.2.9f** so this build stays a
pure refactor with a clean regression signal.

> **As-built note.** 0.2.9a unified the *world-object* defs (Def / ActionDef / DefRegistry) and
> left ItemDef/ItemDefRegistry separate; 0.2.9b unified the *instances* (one located Instance).
> The item-side def merge that closes that split is **0.2.9d**; the storage collapse + flows are
> **0.2.9e**; authored nodes + id-baking are **0.2.9f** (follow-on).

##### 0.2.9a — The composed Def (world objects) — done

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | Composed `Def` SO: basic info (`defId`, name, prefab) + `aspects[]` (embedded `[Serializable]`) + `actions[]` (ActionDef references). | Claude Code | One composed Def type exists. |
| a2 | `ActionDef` SO carrying an action's data (labor, tool, coop, yieldModel, onDepletion, prerequisite), shared and referenced by id. | Claude Code | Actions are shared SOs referenced by Defs. |
| a3 | `DefRegistry` replaces `GatherableObjectDefRegistry`; convert the GatherableObjectDef assets to composed Defs. **ItemDef/ItemDefRegistry stay separate — merged at 0.2.9d.** | Split — script: Claude Code; author: Manual | World-object defs resolve through DefRegistry; GatherableObjectDef is gone. |

*Milestone:* the world-object Def model exists; GatherableObjectDef is gone. (ItemDef still
separate — merged at 0.2.9d.)

##### 0.2.9b — The located Instance — done

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Define the `Instance` type: `defId` + per-instance data (quantity, condition triple, durability, rolled stats) + `location` = `InContainer(id)` \| `InWorld(position, [accrual])` \| `CarriedBy(dreamerId)`. | Claude Code | One Instance type holds both inventory and world objects, location-tagged. |
| b2 | Refactor ItemInstance + ProcessableInstance + ground-item onto the one Instance type (inventory → InContainer; world → InWorld). Schema bump; no saved-data conversion. | Claude Code | Inventory and world objects are the same Instance, distinguished by location. |
| b3 | Per-action accrual state (§5.5/§5.6) attaches to `InWorld` instances, unchanged. | Claude Code | In-progress actions live on the located instance. |
| b4 | Serialize the unified Instance (one shape in the save, location-tagged). | Claude Code | The instance round-trips through the save. |

*Milestone:* one Instance type, location-tagged, serialising.

##### 0.2.9c — Actions + the world surface

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | ActionDef **outcome** (ToInventory / Transform / Carry / Consume) + **context** (world / inventory). Existing gather + pickup/consume become ActionDefs. | Claude Code | Actions carry outcome + context; existing behaviours map to ActionDefs. |
| c2 | **Interactable** component on world prefabs: holds the Def reference; a collider/raycast hit reads it and presents the Def's world-context actions. | Split — script: Claude Code; prefab/collider wiring: Manual | Interacting with a world object lists its world actions. |
| c3 | Action dispatch: timed ones (chop/fell) on the §5.5 accrual engine, instant ones (pickup/consume) immediately. | Claude Code | Each action produces its correct outcome. |
| c4 | Inventory-context actions (consume/equip/drop) from the inventory side — same ActionDef list, filtered by context. | Split — logic: Claude Code; UI: Manual (placeholder) | The same item shows world vs inventory actions by where it is. |

*Milestone:* def-driven, context-filtered actions dispatch correctly.

##### 0.2.9d — Def unification (the deferred half of 0.2.9a)

Fold ItemDef into the composed Def and collapse the two registries into one — the item-side of the
merge 0.2.9a did for world objects. The instance side (0.2.9b) is already done, so this closes the
gap. No behaviour change.

| ID | Task | Executor | Done when |
|---|---|---|---|
| d1 | Add the item-side aspect classes (Inventory exists; add Consumable, Perishable, Tool, Container) and fold ItemDef fields into them — stack/weight → Inventory; consume-effect/window → Consumable; lifespan triple → Perishable; durability+category → Tool; capacity/filter/modifier → Container. | Claude Code | One Def type carries both item and world-object authoring; no separate ItemDef type. |
| d2 | Migrate the item SO assets (Stone, Wood, food, water, save-consumable, containers, tools, tree-chain items) into composed Defs with the right aspects. Conversion script + manual verify. | Split — script: Claude Code; verify/author: Manual | Every existing item SO is a composed Def, verified in the registry. |
| d3 | Collapse ItemDefRegistry into DefRegistry — one registry, one `defId → Def` lookup, one id namespace. | Claude Code | An `Instance.defId` resolves in exactly one place; ItemDefRegistry is gone. |
| d4 | Repoint every item-system reader at the aspect instead of ItemDef fields — inventory add/stack, consume→needs, spoilage triple, container capacity/filter/weight-rollup, durability use-events. | Claude Code | All item systems read aspects; behaviour is identical. |
| d5 | `IItemDefLookup` (or replacement) resolves the composed Def; serialization unaffected (Instance carries only `defId`). | Claude Code | Defs resolve through the one lookup; saves unaffected. |

*Milestone:* one Def type, one registry; inventory / consume / spoilage / containers / durability all
work through aspects.

##### 0.2.9e — Storage collapse + location flows (minus authored nodes → 0.2.9f)

| ID | Task | Executor | Done when |
|---|---|---|---|
| e1 | MapEntityLayer holds unified Instances under two **sync profiles** — authored (delta-only, position local, baked id) and runtime (full-state synced). groundItems + processables collapse into one world-object model; the **runtime profile** is built here (authored profile activates at 0.2.9f). | Claude Code | The world systems are one model; the runtime profile drives spawned/dropped objects. |
| e3 | Location-change flows: pickup (InWorld→InContainer), drop (InContainer→InWorld, runtime), carry (InWorld→CarriedBy→InWorld). LargeTreeLog becomes **one Def** (world actions + Inventory aspect) whose pickup *flips location* instead of destroy-world + create-item. | Claude Code | A carried-then-dropped object stays interactable (the dropped-log fix); LargeTreeLog is one Def. |
| e4 | MapEntitySync drives the unified collection under the post-audit lifecycle discipline (guarded client subscriptions, base despawn cleanup, StopAllCoroutines first on despawn). | Claude Code | No new orphaned subscriptions; sync is clean. |
| e5 | Save/revert: the unified model round-trips through `map_{id}.json`; schema bump, pre-refactor saves rejected. | Claude Code | The world reverts clean; old saves rejected. |

> **e2 (authored profile + id-baking) is split out to 0.2.9f** so this build is a pure refactor
> (clean regression), and authored placement — new capability absorbed from 0.2.7b — gets its own
> honest test rather than muddying the regression signal.

*Milestone:* one storage model (runtime profile live); the location flows and the dropped-log fix hold.

##### Key decisions

- **Validated as one build, tested once.** a/b/c are done; d + e are written as a single continuous
  code run with a compile check between the phases; all manual setup and testing are deferred to
  one consolidated pass (see Execution strategy). No intermediate wiring is done and re-done.
- **The def merge was deferred, not dropped.** 0.2.9a unified world-object defs and 0.2.9b unified
  instances; without d the model is half-unified (one Instance, two def registries). d is what makes
  an `Instance.defId` resolve in one place.
- **LargeTreeLog proves the flow.** Pre-refactor its pickup destroyed a world object and created a
  separate item; unified, it's one Def whose pickup flips location — the model, demonstrated.
- **Authored nodes are 0.2.9f, not part of this build.** Authored placement is new capability
  (id-baking tool, authored sync profile, absorbed 0.2.7b); bundling it into a pure-refactor
  regression would blur "did the refactor break something" with "is the new path buggy." Split out
  → the refactor gets a clean regression, authored nodes get their own test.
- **Lifecycle discipline carried** — every MapEntitySync change follows the post-audit guard/despawn
  rules (this is the path the RPC-flood audit hardened).
- **Schema bump, old saves rejected** — no saved-data migration; changes are code-side.

##### 0.2.9 acceptance (combined, 2-client, regression)

One Def type and one registry resolve every instance; inventory add/stack/move/drop, pickup
(race-safe), spoilage, durability, containers/equip, gathering, and the processing chain all work
unchanged through Def/Instance/Action. On top of that: a carried-then-dropped object stays
interactable; LargeTreeLog is a single Def whose pickup flips location; runtime objects sync
full-state; everything saves and reverts clean. (Authored delta-only placement is validated at
0.2.9f.)

---

##### 0.2.9 — Execution strategy (single test build)

The refactor is set up and tested **once**. Sequencing that makes that hold:

- **Code d then e (minus e2) as one continuous run.** Each phase reshapes what you'd wire (d
  migrates the item SOs; e collapses storage and makes LargeTreeLog one Def), so any setup done
  against an intermediate state is thrown away. Defer *all* manual setup — including the pending
  0.2.9a Def-SO authoring / DefRegistry population and the 0.2.9c prefab wiring — into the single
  consolidated pass.
- **Compile check between phases.** After d, confirm a clean compile; after e, again. These need no
  scene setup — they just catch a structural break at the phase boundary instead of burying it
  under the end-of-refactor surface. (Same pattern as the 0.2.9b compile check.)
- **One consolidated setup + regression** (below) after e compiles clean.
- **Authored nodes (0.2.9f) follow** with their own small setup + test once the regression is green.

##### 0.2.9 — Claude Code handoff: code 0.2.9d + 0.2.9e (minus e2)

> Hand this to Claude Code as the remaining refactor code.

**Task:** implement **0.2.9d then 0.2.9e (excluding e2)** as one continuous run. Last code of the
unified-model refactor. **Authored nodes / id-baking (e2 → 0.2.9f) are out of scope.**

**In scope:** d1–d5 (fold ItemDef into composed Def; migrate item SOs; collapse ItemDefRegistry
into DefRegistry; repoint item-system readers at aspects); e1 (two sync profiles — build the
**runtime** profile; groundItems + processables collapse); e3 (location flows + LargeTreeLog as one
Def whose pickup flips location); e4 (MapEntitySync lifecycle discipline); e5 (save/revert +
schema bump).

**Out of scope (do NOT build):** e2 authored profile + id-baking editor utility; any authored
in-scene node placement.

**Working instructions:**
1. Write d and e as one continuous run. **Do NOT stop to request manual testing between phases** —
   intermediate setup would be thrown away. All setup/testing is deferred to one pass after e5.
2. **Compile check between phases:** clean compile after d, clean compile after e5. No scene setup,
   no gameplay test — just "does it build."
3. **No new gameplay.** Bar is that every 0.2.0–0.2.8 behaviour works unchanged. Preserve existing
   behaviour where a choice arises.
4. **Report, don't wire.** List every manual step the refactor creates/changes (new/edited SOs,
   registry entries, prefab components, schema version, deleted types/files) for the consolidated
   setup. Assume no Editor wiring is done.
5. Flag ambiguities in the report rather than guessing.

**Deliverable:** code for d + e (minus e2); clean compile at both checkpoints; a manual-steps report.

##### 0.2.9 — Consolidated setup + regression (the single pass)

Run once, top to bottom, after Claude Code reports d + e compiling clean. Setup first, then walk the
regression in one sitting. (Authored-node testing is at 0.2.9f, not here.)

**Part A — Setup (in order)**
- **A1 — Reset saves.** Delete `Application.persistentDataPath/Saves/` (schema bumped; old saves
  rejected). Reconcile the version against Claude Code's report.
- **A2 — Def SOs (world objects).** Confirm/author the composed Defs + ActionDefs from 0.2.9a
  (Def_TestProcessable, Def_FallenTree, Def_LargeTreeLog, Def_BranchPile) with `actions[]` wired.
- **A3 — Migrated item Defs.** Per the 0.2.9d report, verify each former ItemDef is a composed Def
  with the right aspects (Stone/Wood → Inventory; food/water → Inventory+Consumable+Perishable;
  save-consumable → Consumable/effect; containers → Container; tools → Tool; tree-chain items).
  Spot-check a few in the registry.
- **A4 — LargeTreeLog is one Def.** Confirm Def_LargeTreeLog carries an **Inventory aspect** (so
  pickup flips location) alongside its world actions.
- **A5 — DefRegistry populated.** One registry: all world-object Defs **and** migrated item Defs in
  DefRegistry; ItemDefRegistry gone from the scene. Expect `[DefRegistry] Built with N def(s).`,
  no duplicate-id warnings.
- **A6 — Interactable on world prefabs (0.2.9c).** On FallenTree/LargeTreeLog/BranchPile/
  TestProcessable prefabs: add `Interactable` (set **Def**) + ensure a **Collider**.
- **A7 — InteractableDetector on the dreamer (0.2.9c).** Add to the Dreamer prefab; set Interact
  Range (~3.5) + the Interact Layer for world-object colliders.
- **A8 — Enter Play once, sanity line.** Confirm the DefRegistry build line, no missing-script /
  duplicate-id errors, before the regression.

**Part B — Regression (single client unless noted; reuse the existing 0.2.x checklists — note only deviations)**
- **B1 — Inventory core (0.2.0/0.2.2a):** add → appears; stackables merge by def; uniques separate;
  split; display floors logical qty.
- **B2 — World items pickup/drop (0.2.1):** place → visual; pick up → to inventory, visual gone;
  drop → reappears.
- **B3 — Consume → needs (0.2.2b/c):** eat → hunger refills + decrements; drink → thirst; save-
  consumable → personal checkpoint banked + item gone.
- **B4 — Action framework (0.2.3):** pool 1440; timed eat debits pool, feeds hunger gradually;
  queue two → auto-advance; cancel mid-eat → partial item + refund. (Movement drain only if
  `timePerMeter > 0`.)
- **B5 — Spoilage (0.2.4):** ages by formula on read; buckets/merge-reconcile; spoiled → no
  nutrition + sickness. (Short-lifespan item + Skip.)
- **B6 — Containers (0.2.5):** equip backpack; items inside; capacity + filter enforced; weight
  rolls up; unequip drops loaded container; pick up → contents intact; cold container re-stamps
  slower.
- **B7 — Durability (0.2.6):** `[Dur %]`; debug-use wears; breaks at 0; use rejected on broken.
- **B8 — Gathering + chain (0.2.7/0.2.8):** spawn FallenTree; with an axe, `process` → removed,
  LargeTreeLog + BranchPile spawn; BranchPile actions yield; partial gather persists across
  save/reload.
- **B9 — Save/revert (spine):** Save → Load (warm) restores; Checkpoint → change → Revert
  clears-and-rehydrates; `map_{id}.json` round-trips; pre-refactor save rejected.

**Part C — The two refactor-specific checks (the point of the pass)**
- **C1 — Carried-then-dropped stays interactable (the dropped-log fix).** Get a LargeTreeLog into
  inventory, **drop** it, walk up → the Interactable HUD shows its world actions (`pickup`/`split`).
  It's still interactable because dropping only flipped location. *(Pre-refactor: the bug.)*
- **C2 — Pickup flips location, not destroy+create.** With a LargeTreeLog in the world, **pickup** →
  it lands in inventory as the **same Def** (one entry, not a separate "log item"); dropping it
  produces an interactable world log again. No world-def/item-def pair remains — one Def.

**Part D — 2-client confirmation (MPPM)**
Run B2, B8 (co-op — rate = 2 when both contribute), C1, B9 with a virtual player. Confirm
per-dreamer inventory isolation and world-object replication. This is where the two long-standing
solo-unverifiable checks finally land: **race-safe concurrent pickup** and **carried-then-dropped**
with a real second player.

#### 0.2.9f — Authored nodes + id-baking (follow-on) — detailed

The absorbed 0.2.7b work, split out of the refactor so it gets its own honest test. Activates the
**authored** sync profile (delta-only, position local) and the id-baking editor utility, letting
you place a gatherable in the scene and interact with it, pristine ones costing zero traffic.

| ID | Task | Executor | Done when |
|---|---|---|---|
| f1 | Authored sync profile: an authored Interactable self-registers (baked id, Def, local position) with the host layer at load; only its mutable delta syncs. Pristine authored objects send nothing. | Claude Code | Authored objects register locally and sync delta-only. |
| f2 | **Id-baking editor utility:** scans the scene's authored Interactables, assigns each a unique stable numeric id, serializes it into the component, warns on duplicates/gaps, and leaves existing ids untouched on re-run. | Claude Code (editor tool) | Every authored node has a stable baked id; re-running fills only new ones. |
| f3 | Save/revert: authored deltas ride `map_{id}.json` clear-and-rehydrate, keyed by baked id. | Claude Code | An in-progress authored node round-trips and reverts clean. |

##### 0.2.9f setup + acceptance (2-client)

Place a gatherable prefab (Interactable + Def + collider) in the Action scene; run the id-baking
tool; enter Play. A pristine authored node **sends no traffic** until interacted with; chopping it
accrues labor, syncs delta-only, and the node reappears with its progress after save/reload and
reverts to as-of-checkpoint. Ids are stable and identical on both clients.

(Crafting → 0.2.10. Authored nodes are 0.2.9f, split from the refactor; no separate harvest-node
build.)

#### 0.2.10 — Crafting — detailed

Recipe-driven configuration of the §5.5/§5.6 task engine (§5.7). Four sub-builds: the recipe task +
materials-list + gates (a); tiers + RNG-as-state (b); station crafting + partial/resume + co-op (c);
the broken-Def unification + the 0.2.6 revision (d). Item outputs only — world-object outputs wait on
Cluster 4 placement.

##### 0.2.10a — RecipeDef + materials + gates + hand-craft

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | `RecipeDef` SO: `inputs` (list), `requiredToolCategory`, `requiredStation`, time/energy fields, `coop`, `unlockCondition` (inert), `tierTable` (b), `rollGranularity` (b). Recipe registry resolves by id. | Split — schema: Claude Code; author: Manual | A recipe is authored as data and resolves by id. |
| a2 | **Materials by list-iteration:** reserve-on-commit and refund iterate `inputs` with no compile-time knowledge of count. Adding/removing a material is authoring-only. | Claude Code | A recipe with 2 vs 5 inputs works with no code change; costs reserve and refund correctly. |
| a3 | Gate detection: hand (no tool/station), tool (carry a `requiredToolCategory` tool), tool+station (`requiredStation` set → object-bound, built in c). Reject a craft whose gates aren't met, at commit. | Claude Code | A craft is offered/commits only when its gates are satisfied. |
| a4 | Hand-craft execution: a self-contained §5.5 task consuming inputs + time + energy, yielding a **single fixed output** (tier table stubbed to one entry until b). Reuses the §5.5 end-payout shape. | Claude Code | A hand recipe consumes inputs/time/energy and yields its output on both clients. |

*Acceptance:* a hand recipe with a multi-material list crafts its output (inputs/time/energy consumed,
list-iterated); gates enforced; a recipe edited from 2→5 materials needs no code; save/revert-clean.

##### 0.2.10b — Tier table + RNG-as-state

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | `tierTable` = list of `{ outputDef, weight }`; each tier is a full output Def (own stats/aspects/prefab). Adding a tier = author a Def + add an entry. | Split — logic: Claude Code; tier Defs/weights: Manual | A recipe's outcomes are a weighted list of output Defs. |
| b2 | **Per-craft seed:** `craftSeed = f(crafterId, recipeId, startTimestamp, craftCounter)`; `craftCounter` is a monotonic value in the save. | Claude Code | Each craft has a deterministic seed derived from saved state. |
| b3 | Tier selection at **completion**: weighted pick from `tierTable`. `rollGranularity` PerUnit → `f(craftSeed, unitIndex)` per unit; PerBatch → `f(craftSeed)` once for all N. | Claude Code | Completion yields tier(s) per the granularity; quality unknown until done. |
| b4 | Skip/revert-safety: the craft record (recipe, seed, counter, accrual) is in the save; replay re-derives the same tier(s). Revert-then-recraft is a new record → new roll. | Claude Code | A skip across completion and a revert both re-derive identical outputs. |
| b5 | Skill seam: selection takes a skill weighting parameter, defaulting to a fixed unskilled distribution (low tiers common, high rare). Inert until Cluster 7. | Claude Code | Weighting is skill-parameterised but fixed for now. |
| b6 | Stackable outputs: same-tier units are the same Def → they stack; a PerUnit batch forms a few tier stacks, not N uniques. | Claude Code | 20 crafted arrows form a small number of per-tier stacks. |

*Acceptance:* crafting yields a skill-weighted tier; PerUnit gives a tier spread (clean stacks),
PerBatch one uniform stack; a skip across completion and a revert re-derive the exact same result;
save/revert-clean on two clients.

##### 0.2.10c — Station crafting + partial/resume + co-op

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | Station craft = an object-bound §5.6.5 task on the `requiredStation`: commit labor, walk away, collect at the station. Reuses the gathering accrual engine. | Claude Code | A station recipe runs as labor on the station and yields there. |
| c2 | Partial/resume (§5.5.5): a hand-craft partial materialises an in-progress item in inventory; a station partial saves progress on the station. Both resumable. | Claude Code | "1h today, finish tomorrow" works for hand and station crafts. |
| c3 | Co-op (§5.6.4): `coop` recipes accept combined-labor accrual (assist/leave re-project); single-crafter recipes lock. | Claude Code | Two dreamers assist one co-op station craft; non-coop locks. |
| c4 | A crafted workstation Def is a valid `requiredStation` for other recipes (self-referential) — verified once a workbench Def exists; world-object *placement* is Cluster 4, so this uses a debug-placed station for now. | Claude Code | A recipe requiring a station works against a (debug-placed) crafted station. |

*Acceptance:* a station craft accrues labor, is left and resumed, and (if coop) shared by both
dreamers; collect at the station; a partial hand-craft yields a resumable in-progress item;
save/revert-clean on two clients.

##### 0.2.10d — Broken items + salvage + the 0.2.6 revision

| ID | Task | Executor | Done when |
|---|---|---|---|
| d1 | Broken Def: an item Def with no live Tool aspect + a `dismantle` action (§5.6 engine) yielding some materials. "Broken Tier 1" stub. | Split — logic: Claude Code; Def/yields: Manual | A broken item exists and can be dismantled for materials. |
| d2 | `brokenForm` on the tool Def → the Broken Def. Broken is also the low `tierTable` entry for craftable tools (a weighted crafting outcome). | Split — schema: Claude Code; wiring: Manual | A tool names its broken form; crafting can roll broken. |
| d3 | **0.2.6 revision — destroy-and-replace:** retire the broken-bool. On durability 0, destroy the tool instance and spawn a fresh `brokenForm` instance at the **same location** (container slot / world / carried), inheriting location only. | Claude Code | A tool breaking becomes the broken Def in place; no lingering 0-durability tool state. |
| d4 | Revert check: the swap is just instances in the save — revert before the break restores the tool, after keeps the broken item; no special-casing. | Claude Code | Reverting across a break behaves correctly with no bespoke logic. |

*Acceptance:* a tool worn to 0 is destroyed and replaced by its broken form at the same location, with
its new icon/prefab; the broken item dismantles for materials; crafting can roll broken as the low
tier; revert across a break is clean; two clients.

##### Key decisions

- **Crafting is configuration, not a new system** — one recipe task over §5.5/§5.6; the only new
  subsystem is RNG-as-state (b).
- **Materials, tiers, aspects, actions are all iterated lists** — adding one is authoring, never code
  (the CLAUDE.md "lists not numbered fields" rule).
- **Tiers are output Defs, not stat rolls** — variety without shattering stacks; the roll picks an
  index, weighted by (a later) skill.
- **RNG-as-state = per-craft seed, rolled at completion, re-derived on replay** — no global stream;
  `rollGranularity` chooses per-unit vs per-batch, both pure functions of saved state. The global
  stream stays deferred to the first continuous stochastic system.
- **Broken is one shared Def reached two ways** — crafted-low or worn-out (destroy-and-replace);
  salvage is a `dismantle` gather action, not a new system. This retires 0.2.6's broken-bool.
- **Item outputs only** — world-object outputs (workbench/building/furniture/trap) produce a
  blueprint handed to Cluster 4 placement; a crafted station then serves other recipes for free.

##### 0.2.10 acceptance (combined, 2-client)

A data-authored recipe (multi-material list, editable without code) crafts through hand, tool, and
station gates; the output tier is a skill-weighted pick from the recipe's tier table, PerUnit or
PerBatch, rolled at completion and re-derived identically across skip and revert; stackable outputs
form clean per-tier stacks; crafts partial-and-resume and (where flagged) run co-op on a station; a
tool worn to 0 is destroyed and replaced by its broken form in place, which salvages for materials;
everything saves and reverts clean.

### First-person camera & movement (Presentation build)

Replaces the static scene camera + independent movement with an owner-controlled first-person rig:
placeholder-follow camera, mouselook with a cursor-mode split, Character Controller movement + jump,
free-look, center-screen spherecast interaction (replacing proximity), and shadow-only self-body.
Presentation-layer only. **Everything is owner-gated** — camera, input, and self-body culling
activate for the local owner alone; the remote dreamer is a replicated body (full mesh, no camera)
driven by the existing owner-auth NetworkTransform. Two seams to existing systems: movement drain
(0.2.3e) still reads the resulting transform delta untouched; the interaction ray feeds the existing
InteractableDetector action machinery.

#### Group A — Ownership gate & camera rig

| ID | Task | Executor | Done when |
|---|---|---|---|
| a1 | Ownership gate: a controller that enables camera + input **only** for `IsOwner`; remote dreamers get no camera and no input. The static scene camera is removed/disabled. | Claude Code | Only the local owner has an active camera; the remote body is camera-less and visible. |
| a2 | Head placeholder: a transform on the dreamer at head height marking the camera target (camera is **not** parented to it). | Split — script: Claude Code; placeholder placement: Manual | A head-height target transform exists on the dreamer. |
| a3 | Camera rig: in `LateUpdate` (after movement resolves), the camera **position-follows** the head placeholder; **rotation comes from mouselook** (a3 of Group B), not the placeholder. | Claude Code | The camera tracks the head a frame-late with no stutter; look is input-driven. |

*Acceptance:* the owner sees through a head-height camera that follows smoothly with no jitter; the
remote client sees a normal moving body; no camera on the remote dreamer.

#### Group B — Mouselook & cursor mode

| ID | Task | Executor | Done when |
|---|---|---|---|
| b1 | Mouselook: yaw rotates the **body**, pitch rotates the **camera** only (clamped, no over-rotation). | Claude Code | Looking around rotates body yaw + camera pitch within clamps. |
| b2 | Cursor mode split: gameplay = cursor locked/hidden + mouselook active; UI-open (inventory/action menu) = cursor free + mouselook suspended. | Claude Code | Opening a menu frees the cursor and stops mouselook; closing re-locks. |

*Acceptance:* mouselook feels right and clamps at the vertical extremes; opening/closing UI toggles
cursor lock and suspends/resumes look cleanly.

#### Group C — Movement & jump

| ID | Task | Executor | Done when |
|---|---|---|---|
| c1 | Character Controller movement (capsule + `Move()`), body-relative to facing, owner-driven (client-authoritative; NetworkTransform replicates). | Claude Code | The owner walks; the remote client sees the movement replicated. |
| c2 | Jump: vertical velocity + gravity + grounded check; owner-driven. | Claude Code | The owner can jump; the arc replicates to the remote client. |
| c3 | Movement-drain seam check: confirm 0.2.3e still debits time/energy from the transform delta under the new controller (it reads the result, not the driver). | Claude Code | Walking still drains time/energy as before. |

*Acceptance:* movement + jump feel responsive, replicate correctly to the second client, and still
drive movement drain. Jump is free (no energy cost) for now.

#### Group D — Free-look

| ID | Task | Executor | Done when |
|---|---|---|---|
| d1 | Middle-mouse **toggle** into free-look: camera yaw decouples from body yaw, clamped to a ±90° arc; the body keeps its heading. | Claude Code | MMB toggles a look-around mode within a 180° arc; body facing unchanged. |
| d2 | Movement during free-look is **body-relative**: input moves the dreamer along its existing heading regardless of camera yaw. | Claude Code | You walk your original heading while glancing around. |
| d3 | Toggle-off returns camera yaw to body-forward (snap or quick ease). | Claude Code | Exiting free-look realigns the camera with the body. |

*Acceptance:* toggle free-look, walk forward while looking across a 180° arc without changing course,
toggle off and the view realigns; no owl-spin past the clamp.

#### Group E — Center-screen spherecast interaction (replaces proximity)

| ID | Task | Executor | Done when |
|---|---|---|---|
| e1 | Replace the proximity query in InteractableDetector with a **camera center-screen cast** against the interactable layer, max length = interaction range (~3.5). Feeds the **existing** action machinery. | Claude Code | Looking at an interactable surfaces its actions; proximity query removed. |
| e2 | Default **spherecast** with an adjustable radius (forgiving default for easy pickups); **nearest-to-center** disambiguation when several are hit. | Split — logic: Claude Code; default radius: Manual | The center sphere reliably targets what you're looking at; ties resolve to nearest-center. |
| e3 | **Precision mode** (hold key): narrows to a thin ray / tiny sphere for picking one small item from a cluster; releases back to the default sphere. | Claude Code | Holding precision lets you single out a small item between others. |

*Acceptance:* center-screen targeting drives interaction; the forgiving sphere picks up easily;
precision mode isolates a small item in a cluster; the action machinery is unchanged behind it.

#### Group F — Self-body (shadow-only)

| ID | Task | Executor | Done when |
|---|---|---|---|
| f1 | The local owner's own body mesh is **culled from the owner's camera** (layer + culling mask) so you never see your own body — owner-gated, so remote clients still render you. | Claude Code | You don't see your own body; other clients see you normally. |
| f2 | Self-body renders **shadow-only** (ShadowsOnly) for the owner, so you still cast a shadow. | Claude Code | The owner casts a shadow without a visible self-mesh. |

*Acceptance:* the owner sees no self-body but casts a proper shadow; the remote client sees the full
body; culling and shadow-only are owner-local (not applied to remote bodies).

#### Key decisions
- **Owner-gated throughout** — one ownership check governs camera, input, free-look, jump, and
  self-body culling; the remote dreamer is a replicated body with no camera, full mesh.
- **Camera position-follows a placeholder in LateUpdate** (not parented) with input-driven rotation —
  the anti-stutter pattern.
- **Body-relative movement everywhere, including free-look** — free-look decouples *camera* yaw
  (±90° clamp) but not movement; you walk your heading while glancing.
- **Interaction is a center-screen cast, not proximity or cursor** — default forgiving sphere +
  nearest-to-center, hold-to-precision ray for clusters; feeds the existing action machinery.
- **Self-body is shadow-only for the owner** — no visible self-mesh (kills clipping/ inside-head
  bugs), shadow preserved; owner-local so remotes are unaffected.
- **Presentation-only, two seams** — movement drain reads the transform result; the interaction ray
  feeds InteractableDetector. No simulation or host involvement (movement is client-authoritative).