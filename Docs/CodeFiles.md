# Code File Index

Every source file authored for Coremenders — Primal Frost, grouped by assembly.
Generated 2026-08-17 (Cluster 2, builds 0.2.x). Line counts are approximate and
drift — treat this file as a *map*, not a metric.

Not listed: Unity's `TutorialInfo/` sample scripts (`Readme.cs`,
`Editor/ReadmeEditor.cs`), which ship with the template and are not ours.

**Totals:** 100 `.cs` files + 5 `.asmdef` files, 12,151 lines.

| Assembly | Files | Lines | References |
|---|---|---|---|
| `Game.Simulation` | 43 | 2,120 | *(none — `noEngineReferences: true`)* |
| `Game.Persistence` | 4 | 272 | Simulation |
| `Game.Networking` | 34 | 7,677 | Simulation + NGO |
| `Game.Presentation` | 7 | 630 | Simulation + Networking + Persistence |
| `Game.Editor` | 12 | 1,452 | *(editor-only tooling)* |

---

## Game.Simulation — pure C#, no engine references

`Assets/Game/Simulation/` · `Game.Simulation.asmdef` (14)

### Core state tree

| File | Lines | Purpose |
|---|---|---|
| [WorldState.cs](../Game/Simulation/WorldState.cs) | 19 | Root authoritative state — clock, two dreamers, game-over flag, craft counter. Serialized as `world.json`. |
| [DreamerRecord.cs](../Game/Simulation/DreamerRecord.cs) | 45 | Per-dreamer authoritative state: slot, position, appearance, needs, energy, vitality, afflictions, task, buffs, nightmare tier. Serialized as `dreamer_<slot>.json`. |
| [MapEntityLayer.cs](../Game/Simulation/MapEntityLayer.cs) | 32 | Per-map authoritative entity state (TDD §1.11, 0.2.7a) — `map_<id>.json`. |
| [WorldClock.cs](../Game/Simulation/WorldClock.cs) | 44 | Authoritative in-game world clock (TDD §E1). |
| [Float3.cs](../Game/Simulation/Float3.cs) | 17 | Unity-free 3D vector. Converted to/from `Vector3` only at engine boundaries. |
| [AppearanceData.cs](../Game/Simulation/AppearanceData.cs) | 11 | Skin tone / hair style / hair colour indices. |

### The resolver

| File | Lines | Purpose |
|---|---|---|
| [SimResolver.cs](../Game/Simulation/SimResolver.cs) | 366 | **The single canonical advance function.** 11 ordered stages; every path (real-time, Skip, inactive-map re-entry) calls `Step()`. |
| [NeedsConfig.cs](../Game/Simulation/NeedsConfig.cs) | 71 | Tuning: Tier-1 need drain, activity modifiers, task outputs, affliction/Vitality thresholds. |
| [MovementConfig.cs](../Game/Simulation/MovementConfig.cs) | 27 | Tuning: movement-based time-pool and energy drain (TDD §0.2.3e1). |
| [SpoilageConfig.cs](../Game/Simulation/SpoilageConfig.cs) | 26 | Tuning: item spoilage bucket thresholds (TDD §5.2, 0.2.4c). |
| [SkipTypes.cs](../Game/Simulation/SkipTypes.cs) | 46 | Skip request/result types and early-stop reasons. |

### Needs, tasks & afflictions

| File | Lines | Purpose |
|---|---|---|
| [DreamerNeeds.cs](../Game/Simulation/DreamerNeeds.cs) | 11 | Hunger / thirst / warmth values. |
| [AfflictionSet.cs](../Game/Simulation/AfflictionSet.cs) | 19 | Active Tier-1 needs-based afflictions (TDD §4.4 stages 5–6). |
| [DreamerTask.cs](../Game/Simulation/DreamerTask.cs) | 21 | Authoritative per-dreamer task state (TDD §4.4 stage 4). |
| [TaskType.cs](../Game/Simulation/TaskType.cs) | 12 | Idle / Eating / Sleeping / Resting / Gathering / Crafting. |
| [ConditionBucket.cs](../Game/Simulation/ConditionBucket.cs) | 15 | Qualitative freshness tier for perishables (TDD §5.2, 0.2.4c). |

### Actions

| File | Lines | Purpose |
|---|---|---|
| [ActionRecord.cs](../Game/Simulation/ActionRecord.cs) | 39 | One entry in a dreamer's action queue (TDD §0.2.3b1). |
| [ActionPayout.cs](../Game/Simulation/ActionPayout.cs) | 11 | How an action delivers its effect (TDD §0.2.3, §5.5). |
| [ActionEffect.cs](../Game/Simulation/ActionEffect.cs) | 31 | Dreamer resource an action drains or restores. |
| [ActionEndEffect.cs](../Game/Simulation/ActionEndEffect.cs) | 11 | One-shot effect fired when an EndEffect action completes. |
| [ActionEndedResult.cs](../Game/Simulation/ActionEndedResult.cs) | 12 | Produced by `SimResolver.Step` on end-payout completion. |
| [ProcessableInstance.cs](../Game/Simulation/ProcessableInstance.cs) | 73 | Piecewise-linear labor accrual segments (TDD §5.5.5). |
| [ConsumeEffect.cs](../Game/Simulation/ConsumeEffect.cs) | 15 | Instant effect on consuming an item (TDD §0.2.2b1). |

### Buffs

| File | Lines | Purpose |
|---|---|---|
| [BuffConfig.cs](../Game/Simulation/BuffConfig.cs) | 197 | Tuning knobs for the buff/debuff system (TDD §0.1.5a/b/c). |
| [BuffDef.cs](../Game/Simulation/BuffDef.cs) | 18 | Static definition of a buff or debuff. |
| [BuffInstance.cs](../Game/Simulation/BuffInstance.cs) | 23 | Runtime instance — `remainingMinutes` + optional `checkpointId`. |
| [BuffModifier.cs](../Game/Simulation/BuffModifier.cs) | 10 | One `(stat, multiplier)` pair. |
| [BuffStat.cs](../Game/Simulation/BuffStat.cs) | 13 | Which stat a modifier touches (drain multipliers, restore rates). |
| [BuffNetId.cs](../Game/Simulation/BuffNetId.cs) | 22 | Numeric wire identity for buff defs (rule 8 — ids, not strings). |
| [BuffCoverage.cs](../Game/Simulation/BuffCoverage.cs) | 13 | Coverage label for a protection buff — revert-vote HUD only. |

### Definitions & instances

| File | Lines | Purpose |
|---|---|---|
| [Instance.cs](../Game/Simulation/Instance.cs) | 176 | Runtime item/object instance + its location tag (TDD §1.12, 0.2.9b1). |
| [DefAspects.cs](../Game/Simulation/DefAspects.cs) | 147 | Base class + all per-Def aspect data (`[SerializeReference]` composition). |
| [ItemDefData.cs](../Game/Simulation/ItemDefData.cs) | 46 | Pure-data view of a definition consumed by Simulation code. |
| [IItemDefLookup.cs](../Game/Simulation/IItemDefLookup.cs) | 14 | Exposes definition data to Simulation without a Unity dependency. |
| [ItemCategory.cs](../Game/Simulation/ItemCategory.cs) | 13 | Resource / Food / Water / Tool / Container / Weapon / Other. |
| [ItemTypeTag.cs](../Game/Simulation/ItemTypeTag.cs) | 13 | `[Flags]` container-filter tags (General, Ammo, Tool, Food). |
| [EquipSlots.cs](../Game/Simulation/EquipSlots.cs) | 58 | Dreamer equip slots (0.2.5a): backpack + two pouches + quiver. |
| [EquipSlotType.cs](../Game/Simulation/EquipSlotType.cs) | 10 | Enum for the four equip slots. |
| [DamageType.cs](../Game/Simulation/DamageType.cs) | 15 | Damage kinds for the §15 combat seam. |

### Crafting

| File | Lines | Purpose |
|---|---|---|
| [CraftRecord.cs](../Game/Simulation/CraftRecord.cs) | 52 | In-flight craft + materials reserved at commit time (TDD §5.7.0). |
| [CraftRng.cs](../Game/Simulation/CraftRng.cs) | 72 | RNG-as-state for crafting (TDD §5.7.3) — the first stochastic system; seeded from `WorldState.craftCounter` so reverts reproduce. |

### Dream flow & checkpoints

| File | Lines | Purpose |
|---|---|---|
| [DreamFlowTypes.cs](../Game/Simulation/DreamFlowTypes.cs) | 10 | `DreamFlowPhase` — None / RescueWindow / WakeUpVote / GameOver. |
| [CheckpointMeta.cs](../Game/Simulation/CheckpointMeta.cs) | 37 | Metadata sidecar per checkpoint (timestamp, coverage label). |

---

## Game.Persistence — save/load

`Assets/Game/Persistence/` · `Game.Persistence.asmdef` (16) — references Simulation only

| File | Lines | Purpose |
|---|---|---|
| [SaveSystem.cs](../Game/Persistence/SaveSystem.cs) | 194 | Unified save/load (TDD §1.8). Atomic temp → `File.Replace`; owns `CurrentSchemaVersion`. ⚠ `Save()` hand-builds a `worldOnly` object — every new `WorldState` field must be added there. |
| [ISaveSerializer.cs](../Game/Persistence/ISaveSerializer.cs) | 13 | The single serializer contract. |
| [NewtonsoftSerializer.cs](../Game/Persistence/NewtonsoftSerializer.cs) | 14 | The only implementation — indented JSON via Newtonsoft. |
| [TemplateLoader.cs](../Game/Persistence/TemplateLoader.cs) | 12 | Convenience wrapper over `SaveSystem.LoadTemplate()`. |

---

## Game.Networking — host-authoritative runtime

`Assets/Game/Networking/` · `Game.Networking.asmdef` (20) — references Simulation + NGO
(never Persistence — see CLAUDE.md cross-layer rule)

### State store & flow

| File | Lines | Purpose |
|---|---|---|
| [RuntimeDataManager.cs](../Game/Networking/RuntimeDataManager.cs) | 83 | Host-side authoritative state store (TDD §1.3) — holds `WorldState` + the dreamer transform registry. A store, not a god object. |
| [GameFlowManager.cs](../Game/Networking/GameFlowManager.cs) | 460 | `DontDestroyOnLoad` orchestrator for the real game flow (host/join → load → spawn → play). Calls Persistence by injection. |
| [ConnectionBootstrap.cs](../Game/Networking/ConnectionBootstrap.cs) | 45 | Build 0.0.0 smoke-test Start Host / Start Client buttons. |
| [DebugAutoConnect.cs](../Game/Networking/DebugAutoConnect.cs) | 54 | Debug bootstrap for SampleScene — bypasses the real scene flow. |
| [HostJoinController.cs](../Game/Networking/HostJoinController.cs) | 18 | Pre-network UI for the HostJoin scene; delegates to `GameFlowManager`. |
| [CharacterCreationController.cs](../Game/Networking/CharacterCreationController.cs) | 146 | In-scene NetworkBehaviour for the CharacterCreation scene. |
| [DreamerCreationUI.cs](../Game/Networking/DreamerCreationUI.cs) | 69 | *Legacy* — Build 0.0.1b creation UI, superseded at 0.0.1c. |
| [PauseMenuController.cs](../Game/Networking/PauseMenuController.cs) | 82 | Escape opens/closes the in-game menu; does not stop the clock. |
| [UiFocus.cs](../Game/Networking/UiFocus.cs) | 51 | Process-wide "a panel wants the mouse" claim registry (FP build, B b2). |

### Clock & skip

| File | Lines | Purpose |
|---|---|---|
| [WorldClockDriver.cs](../Game/Networking/WorldClockDriver.cs) | 115 | Host-only tick driver — the boundary between `Time.deltaTime` and `SimResolver`. Hosts the `NeedsConfig`/`BuffConfig` inspector containers. |
| [WorldClockSync.cs](../Game/Networking/WorldClockSync.cs) | 141 | Periodic clock push to clients; clients extrapolate (TDD §E3–E5). |
| [SkipManager.cs](../Game/Networking/SkipManager.cs) | 300 | Host-side Skip engine, chunked `SimResolver` loop (TDD §4.2). |

### Per-dreamer sync

| File | Lines | Purpose |
|---|---|---|
| [DreamerNetworkAdapter.cs](../Game/Networking/DreamerNetworkAdapter.cs) | 131 | NetworkBehaviour on every dreamer prefab instance. |
| [DreamerSpawner.cs](../Game/Networking/DreamerSpawner.cs) | 43 | Host-side spawner driven by `OnClientConnectedCallback`. |
| [DreamerNeedsSync.cs](../Game/Networking/DreamerNeedsSync.cs) | 115 | Syncs needs, energy, Vitality, afflictions. |
| [DreamerBuffSync.cs](../Game/Networking/DreamerBuffSync.cs) | 156 | Compact 4-slot buff payload. |
| [DreamerTaskSync.cs](../Game/Networking/DreamerTaskSync.cs) | 399 | Task state + dev UI. |
| [DreamerInventorySync.cs](../Game/Networking/DreamerInventorySync.cs) | 1383 | Multi-container inventory wire payload and all inventory RPCs (0.2.5 / TDD §5.4). Largest file in the project. |
| [AppearanceDataDto.cs](../Game/Networking/AppearanceDataDto.cs) | 37 | `INetworkSerializable` bridge for the `AppearanceData` POCO. |
| [Float3Extensions.cs](../Game/Networking/Float3Extensions.cs) | 10 | `Float3` ↔ `Vector3` conversion at the engine boundary. |

### World objects & interaction

| File | Lines | Purpose |
|---|---|---|
| [MapEntitySync.cs](../Game/Networking/MapEntitySync.cs) | 1291 | Variable-length full-state payload for a map's unified world-object collection (0.2.7a). |
| [Interactable.cs](../Game/Networking/Interactable.cs) | 57 | Placed on any interactable world prefab (TDD §1.12, 0.2.9c2). |
| [InteractableDetector.cs](../Game/Networking/InteractableDetector.cs) | 216 | Per-dreamer look-target resolution and prompt presentation. |
| [WorldItemsSync.cs](../Game/Networking/WorldItemsSync.cs) | 4 | **Obsolete stub** — replaced by `MapEntitySync` at 0.2.7a; delete after the component swap. |

### Definitions (ScriptableObjects & registries)

| File | Lines | Purpose |
|---|---|---|
| [Def.cs](../Game/Networking/Def.cs) | 141 | Unified SO definition for any world object — item, tool, container, station. Aspect-composed. |
| [DefRegistry.cs](../Game/Networking/DefRegistry.cs) | 76 | Singleton registry for all `Def` assets (TDD §1.12, 0.2.9a4). |
| [ActionDef.cs](../Game/Networking/ActionDef.cs) | 106 | Generic action "verbs" + yield / depletion-spawn entries (TDD §1.12). |
| [RecipeDef.cs](../Game/Networking/RecipeDef.cs) | 91 | Craft recipe: material list (ids, not strings) + tier outputs (TDD §5.7.0). |
| [RecipeRegistry.cs](../Game/Networking/RecipeRegistry.cs) | 41 | Singleton registry for all `RecipeDef` assets. |
| [CraftResolver.cs](../Game/Networking/CraftResolver.cs) | 63 | Rolls output tier(s) at craft completion and builds output Instances (TDD §5.7.2/3). |
| [DefIdAttribute.cs](../Game/Networking/DefIdAttribute.cs) | 28 | `[DefId]` marker + `DefIdFilter` — turns an `int` field into a searchable Def picker. |

### Dream flow

| File | Lines | Purpose |
|---|---|---|
| [DreamFlowManager.cs](../Game/Networking/DreamFlowManager.cs) | 391 | Host-authoritative orchestrator for the GDD §18 dream flow (TDD §0.1.7). |
| [ConsensusVoteComponent.cs](../Game/Networking/ConsensusVoteComponent.cs) | 211 | Two-player checkpoint revert vote — host-authoritative pick state. |
| [CheckpointCleaner.cs](../Game/Networking/CheckpointCleaner.cs) | 17 | Checkpoint retention helper (TDD §0.1.7d c1). |

---

## Game.Presentation — client-side view

`Assets/Game/Presentation/` · `Game.Presentation.asmdef` (20) — references Simulation + Networking + Persistence

| File | Lines | Purpose |
|---|---|---|
| [DreamerMovementController.cs](../Game/Presentation/DreamerMovementController.cs) | 112 | Client-authoritative movement for the owned dreamer (0.0.2, reworked for FP Group C). |
| [FirstPerson/FirstPersonRig.cs](../Game/Presentation/FirstPerson/FirstPersonRig.cs) | 135 | FP Group A — the single ownership gate and the camera rig. |
| [FirstPerson/FirstPersonLook.cs](../Game/Presentation/FirstPerson/FirstPersonLook.cs) | 151 | FP Groups B & D — mouselook, cursor mode, free-look. |
| [FirstPerson/SelfBodyVisibility.cs](../Game/Presentation/FirstPerson/SelfBodyVisibility.cs) | 64 | FP Group F — owner never sees their own body but still casts a shadow. |
| [MainMenuController.cs](../Game/Presentation/MainMenuController.cs) | 31 | Main menu, three entry points (D7). |
| [SplashController.cs](../Game/Presentation/SplashController.cs) | 15 | Timed splash → MainMenu. |
| [Settings/GameSettingsApplier.cs](../Game/Presentation/Settings/GameSettingsApplier.cs) | 30 | `DontDestroyOnLoad` frame-rate cap applier, re-applied per scene load. |

---

## Game.Editor — authoring tooling (editor-only)

`Assets/Game/Editor/` · `Game.Editor.asmdef` (19)

### Def authoring

| File | Lines | Purpose |
|---|---|---|
| [DefEditor.cs](../Game/Editor/DefEditor.cs) | 123 | Custom inspector making the `[SerializeReference]` aspects list workable. |
| [DefRegistryEditor.cs](../Game/Editor/DefRegistryEditor.cs) | 89 | Auto-populates `DefRegistry._defs` from the asset folder. |
| [DefRegistryPostprocessor.cs](../Game/Editor/DefRegistryPostprocessor.cs) | 44 | Keeps every open-scene `DefRegistry` in sync with the Def folder on import. |
| [RecipeDefEditor.cs](../Game/Editor/RecipeDefEditor.cs) | 47 | Surfaces `recipeId` status (unassigned / duplicate). |
| [RecipeRegistryEditor.cs](../Game/Editor/RecipeRegistryEditor.cs) | 105 | Auto-populates `RecipeRegistry._recipes`. |
| [ActionDefCreatorWindow.cs](../Game/Editor/ActionDefCreatorWindow.cs) | 85 | Minimal authoring tool for generic action verbs (TDD §1.12). |
| [PickupActionAssigner.cs](../Game/Editor/PickupActionAssigner.cs) | 110 | One-click: give every inventory-capable Def a `pickup` action binding. |
| [TreeChainBuilder.cs](../Game/Editor/TreeChainBuilder.cs) | 262 | One-click build of the tree-chain world-object Defs and their wiring. |

### Id assignment

| File | Lines | Purpose |
|---|---|---|
| [DefIdTools.cs](../Game/Editor/DefIdTools.cs) | 115 | Automatic id assignment for `Def.defId` and `RecipeDef.recipeId`. |
| [DefIdPostprocessor.cs](../Game/Editor/DefIdPostprocessor.cs) | 62 | Assigns a unique id to any newly-imported Def/RecipeDef lacking one. |
| [DefIdDrawer.cs](../Game/Editor/DefIdDrawer.cs) | 155 | Property drawer behind `[DefId]` — searchable Def picker for `int` fields. |
| [NodeIdBaker.cs](../Game/Editor/NodeIdBaker.cs) | 78 | Bakes ids onto authored in-scene nodes (TDD 0.2.9f, f2). |

---

## Notes

- **`WorldItemsSync.cs`** is a 4-line namespace stub left as a deletion reminder;
  the live path is `MapEntitySync`. See its header comment for the swap steps.
- **`DreamerCreationUI.cs`** is superseded by `CharacterCreationController` and
  kept only for the 0.0.1b debug path.
- **`GameSettingsApplier`** sits outside the `Game.Presentation` namespace (it is
  in the global namespace) — an outstanding inconsistency with the
  `Game.<Layer>` convention.
- The two biggest files, `DreamerInventorySync.cs` (1383) and
  `MapEntitySync.cs` (1291), together account for ~21% of the codebase.
