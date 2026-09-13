# TODO — Deferred Tasks
Items that are known gaps or planned improvements, deferred to keep the current build focused.
Organised by system. Add new items here rather than inline in code where possible.

---

## Pause System

- **Freeze all gameplay on pause** — currently only the world clock pauses. Movement (CharacterController), NPC AI, physics, damage, crafting timers, and the elapsed-time resolver all need to stop while `WorldClock.isPaused` is true. Likely implemented via a shared `GameplayPaused` flag checked in `Update`/`FixedUpdate` across all relevant systems.
- **Client-side movement freeze on host pause** — when the host pauses, the client's `DreamerMovementController` should also stop accepting input. Currently the client can still move freely. Consider syncing pause state to a persistent singleton and having `DreamerMovementController.Update` respect it.

---

## Save / Load

- **Multiple save slots** — currently hardcoded to `slot_0`. The §21 multi-session system needs a slot selector on the main menu.
- **Save migration** — schema version bumps currently reject old saves loudly (dev behaviour). A proper migration path needs designing before Early Access.
- **Disconnect save** — graceful host disconnect should write a separate safety snapshot that never overwrites the main save (GDD §21).
- **Client-side crash recovery** — the client doesn't store world state; on reconnect it re-requests from the host. The host's crash recovery (Save and Exit + disconnect save) needs hardening.
- **RNG-as-state (global stream)** — a seeded deterministic *stream* stored in the save. The per-craft variant landed at 0.2.10b (`CraftRng` + `WorldState.craftCounter`: each craft's tier is a pure function of `f(crafterSlot, recipeId, startTime, craftCounter)`, no stream). The heavier global-stream form stays deferred to the first *continuous* stochastic process (weather, AI decisions).

---

## Character & Movement

- **Animation** — capsule placeholder has no animation. Wire NetworkAnimator when character meshes exist.
- **Body type / attribute-driven physique** — GDD §6: body type derived from STR/END/AGI ratio; character snaps to nearest model as ratio shifts.

---

## First-person camera & movement (FP build deferrals)

- **Mouse sensitivity / invert-Y are per-prefab Inspector fields** — `FirstPersonLook._sensitivity` and `_invertY` are serialized on the dreamer prefab, not player settings. Route them through `GameSettingsApplier` + PlayerPrefs (like `MaxFps`) when the settings menu is built, so each player can tune their own without touching the prefab.

- **Input bindings are hardcoded defaults** — free-look (MMB), precision aim (Left Ctrl), action menu (E), and free-cursor (Tab) are serialized defaults with no rebinding. Fold into a proper Input Actions asset + rebinding UI in the input/settings pass. The project also still mixes the legacy `Input` API (Networking HUDs) with the Input System package (Presentation); unify on the package at the same time.

- **Tab free-cursor is a dev-HUD stopgap** — gameplay locks the cursor, but the always-on OnGUI dev HUDs (task buttons, dev menu, action ✕ buttons, buff panel) are click-driven and have no open/close state to claim `UiFocus` with. Tab toggles a manual claim so they stay reachable. Remove Tab once the dev HUD is replaced by real UI that claims `UiFocus` per panel.

- **Self-body is renderer-wide ShadowsOnly, not a layer + culling mask** — the FP build's f1 named "layer + culling mask", but in URP an object excluded from a camera's culling mask is dropped from that camera's shadow pass too, which would take f2's shadow with it. `SelfBodyVisibility` uses `ShadowCastingMode.ShadowsOnly` instead, which satisfies both. Revisit if first-person arms/held-item meshes are added: those need a *visible* self-layer, at which point a real layer split becomes worthwhile.

- **No head bob, camera shake, FOV or near-clip control** — the rig position-follows the head placeholder exactly. Gait-driven bob, landing shake, sprint FOV kick, and a per-player FOV setting are all deferred to the game-feel pass.

- **Interaction is still menu-driven** — looking at an object and pressing E opens the existing action-list HUD. A direct "hold E to gather" binding for the primary action (no menu) is deferred to the interaction UI pass; the center-screen cast already resolves the target for it.

- **Movement is not suspended while a UI panel is open** — only mouselook is (per the FP build's b2). WASD still walks while the inventory or a modal panel is up. Revisit alongside the pause-system TODO ("Freeze all gameplay on pause") so both use one gameplay-input gate.

---

## NPC / Tribe

- **NPC spawning** — tribespeople are not yet in the simulation at all. Cluster 1 adds the tribe agent system.
- **AI routines** — NPC auto-task, survival override, and couple mechanics (GDD §7).
- **Chieftain role** — migration trigger, couple assignment, multiplayer role-passing (GDD §7).

---

## World / Environment

- **Temperature system** — 6-map temperature cycle, weather events, wildlife migration (GDD §2).
- **Wildlife** — AI behaviour types, aggression, fire-as-deterrent, spawn/despawn with temperature (GDD §15).
- **Combat** — GDD §15; Cluster 1 only built the incapacitation/affliction pipeline. Full combat (attack, dodge, weapon use, creature vs dreamer resolution) is deferred. **Data seam authored (0.2.10):** `WeaponAspect` (Simulation) carries `attackTier`, a `List<WeaponDamage>` of typed `{ DamageType (Slash/Pierce/Bash), amount }` (multiple types per weapon), `reach`, `isRanged`, and an ammo impact-wear seam `wearsOnImpact` + `impactBreakChance` (0..1). Two wear triggers, one destroy-and-replace target: melee/tools wear via `ToolAspect` durability→0; ammo (arrows) wear via per-impact break chance — **both resolve to `ToolAspect.brokenFormDefId`** (a stackable arrow adds a `ToolAspect` with `isDurabilityTool = false` only to name its broken form, so stacking is preserved — a durability pool would shatter the stack). Combat/projectile resolution (which fires the impact roll, drops a recoverable arrow on survive, spawns the broken form on break) is deferred to §15. When §5.6.1 gathering-rate wiring lands it can also read `attackTier`/damage from here (replacing the fixed/debug `baseChopDmg`/attack-tier values). Consider `DamageType` resistances/weaknesses, weapon categories (sword/axe/spear parallel to tool categories), and whether "brokenForm" should move to its own aspect now that two triggers share it.
- **Additive scene loading per map** — currently single-scene; multi-map needs additive loads with per-map state in the RDM (TDD §1.10).
- **Inactive-map resolution** — elapsed-time resolver applied on map re-entry (TDD §1.7, GDD §20).

---

## MapEntityLayer — Design decision (implement at Build 0.2.6)

Before Build 0.2.6 (harvest nodes) touches any world-entity code, replace `WorldItemsSync` with a unified `MapEntityLayer` architecture. The following decisions are locked:

**What belongs in MapEntityLayer:**
Everything that exists at a position in the world, belongs to a specific map, persists through save/revert, and is only relevant to players currently on that map:
- `groundItems` — dropped/harvested loot on the ground (current `WorldItemsSync` scope)
- `harvestNodes` — mutable delta state for trees/rocks/plants (depleted id + regen timer only; authored positions come from the map file, same on all clients, never networked)
- `animals` — spawned animals (position, health, AI state)
- `buildings` — player-constructed structures
- `placedItems` — crafted furniture, worktables, traps placed in the world

**What does NOT belong in MapEntityLayer:**
- Authored static geometry (terrain, decorative props, tree/rock positions — these are the map file, not session state)
- Player dreamers (those are in RDM / DreamerRecord as before)

**Key invariants:**
1. **Map-scoped**: `MapEntityLayer` is keyed by `mapId`. The RDM holds one layer per currently loaded map. Switching maps does not unload the other map's layer — it stays in the RDM and on disk, inactive.
2. **Per-map sync**: only clients currently on a map receive that map's network updates. A player on map_02 dropping an item produces no traffic for the player on map_01.
3. **Static nodes — delta only**: harvest node authored positions are loaded locally from the map file (identical on all clients). Only the mutable state (which nodes are depleted, regen timers) is stored in the map layer save and synced. This bounds the save size and wire cost to O(depleted nodes), not O(total nodes).
4. **Save/revert**: `MapEntityLayer` serializes to `map_{id}.json` alongside `world.json`. Revert clears and rehydrates the layer exactly as dreamer records are cleared and rehydrated.
5. **Wire format**: variable-length per sub-collection (same pattern as the upgraded `WorldItemsPayload`). Animals and placed items may need richer per-entity state — design their payloads when those systems land.

**Migration path from WorldItemsSync:**
- `WorldItemsSync` becomes the `groundItems` sub-system of `MapEntityLayer`.
- `WorldState.worldItems` and `WorldState.nextWorldItemId` move into `MapEntityLayer.groundItems`.
- The `WorldItemsSync` NetworkBehaviour is replaced by `MapEntitySync` (one per loaded map), which owns all sub-collection NVs for that map.
- Schema version bump required at migration time; old saves rejected as usual.

---

## Needs & Energy

- **Needs system** — hunger, thirst, sleep, temperature; Vitality drain on critical needs (GDD §4). Core of Cluster 1.
- **Energy pool** — daily energy budget, recovery via sleep/rest/food (GDD §5).
- **Skip Time** — fast-forward with need/debuff projection and guard thresholds (GDD §5).
- **Skip projection / menu cap** — the dry-run that computes per-need costs and caps the requestable duration was removed in 0.1.4 to unblock later builds. Needs to be re-added before Skip Time is player-facing.
- **Fidelity gradient during skip** — engaged-entity lightweight AI + coarse movement for the player's own dreamer during fast-forward. Blocked on wildlife/combat systems existing first.
- **Needs-fulfillment buffs** — well-fed, well-rested, etc.: need levels above thresholds grant positive buffs. The buff system supports them; wiring is deferred until the needs system is tuned.
- **Chunking-invariance validation (Build 0.1.3)** — confirm skip N ticks == N real-time ticks yield identical final state. Deferred: requires ~24 real minutes of play to verify. Re-run once a time-scale / accelerated-tick debug mode exists.

---

## UI / HUD

- **Proper UI pass** — all current UI is placeholder OnGUI. Replace with Unity UI (uGUI/UI Toolkit) once layout is locked.
- **Needs panel** — HUD showing time, energy, hunger, thirst, warmth, Vitality, Stamina, Courage (GDD §5).
- **Buff/debuff display** — active streaks, protection buffs (dream save timer), Nightmare tiers (GDD §18).
- **Clock pause indicator for client** — client has no visual feedback that the host paused. Add a centre-screen "Paused" overlay on the client side.
- **Full Scheduler** — long-horizon daily-plan UI: assign tasks across the coming day(s), multi-day sustainability view. Blocked on the task system and needs projection being complete.

---

## Dream Save System

- ~~**Wake Up flow**~~ — implemented in 0.1.7.
- ~~**Protection buffs**~~ — implemented in 0.1.5/0.1.6.
- ~~**Nightmares debuff**~~ — implemented in 0.1.7.
- **Skip-start consensus vote** — both players agree on the skip duration before fast-forward begins; currently the host initiates unilaterally. `ConsensusVoteComponent` from 0.1.7 is ready to reuse. Wire at the point where the skip UI is built properly (TDD §4.5).
- **Game Over solo-dreamer vs run-level clarification** — currently Game Over ends the run for both players (TDD §0.1.7c). Confirm whether a dreamer with no coverage should be individually lost while their partner continues, or if run-level game over is correct (TDD notes this as an open question).
- **Wake Up vote UI polish** — the consensus vote uses a debug OnGUI panel. Replace with proper uGUI when the UI pass happens. The logic (ConsensusVoteComponent) is layout-independent and ready for the wrapper.
- **Shared-sleep rework** — same-tick coincidence is too strict; a shared sleep currently requires both dreamers to finish on the exact same tick, which is near-impossible unless they started together. Options: explicit "sleep together" action that aligns start times, or a tolerance window (N IG minutes) in `HandleSleepWake` that groups near-simultaneous wakes into one shared checkpoint.
- ~~**Save-consumable item wrapper**~~ — implemented in 0.2.2c. Consuming an `ItemDef` with `consumeEffect = SaveConsumable` calls `UseDebugSaveConsumable` via `ConsumeItemServerRpc`. The `DevSaveConsumableServerRpc` debug shortcut remains for testing.
- **§21 checkpoint retention** — reachability-aware pruning (keep only checkpoints reachable from a valid buff chain) + crash-recovery menu-loadability. Interim rule in place: files persist until a revert discards future-timeline checkpoints.

---

## Networking

- **Disconnect / reconnect** — client disconnect → dreamer reverts to AI; reconnect → reclaims dreamer (GDD §21). Deferred from Phase 0.
- **Steam transport** — Facepunch adapter for friend invites; deferred from Build 0.0.0 (TDD T6).
- **Networking hardening** — cheat prevention, authority validation, bandwidth optimisation. Post-gameplay-feature milestone.
- **Bulk-push coalescing** — batch multiple `ForcePush()` calls that fire in the same frame into a single send. Only necessary if the Max Packet Queue Size bump proves insufficient in stress testing.
- **Race-safety validation (0.2.1b3)** — the concurrent-pickup test (both dreamers click Pick Up on the same item simultaneously) cannot be performed solo. Requires two people at two input devices, or a scripted bot that fires `PickupItemServerRpc` on both dreamer objects in the same frame. Validate with a second player before shipping Cluster 2. The host-side logic is correct (item removed from `worldItems` before the second RPC can find it), but the test has never been run.

---

## Containers (0.2.5 deferrals)

- **MoveItemServerRpc — nested target** — `MoveItemServerRpc` only supports moving items between top-level equip-slot containers. Moving an item into a nested container (a container-type item inside another container) is not yet wired. Add a `toNestedIndex` parameter for this path when the use case arises.

- **Per-item Drop from nested containers** — the OnGUI Drop button is only wired for top-level container items. Items inside nested containers have no Drop button yet. Add `DropNestedItemServerRpc(byte slotType, int containerItemIndex, int nestedItemIndex)` when needed.

- **Equip-slot re-stamp on container arrival** — `EquipContainerServerRpc` / `PickupItemServerRpc` re-stamps perishable contents when a container with a spoilageModifier != 1 is equipped. However, if a container was previously at ambient rate (on the ground) and then placed in a cold slot, contents whose rate was already at ambient get re-stamped. This is the correct behaviour, but the inverse (picking up a warm container from a cold environment) is not yet handled. Deferred until temperature system lands.

- **WeightCapacity enforcement for nested items** — `CanAddToContainer` computes the weight of top-level contents only. Items inside nested containers are not counted toward the outer container's `weightCapacity`. Fix when nesting rollup is required.

---

## Spoilage (0.2.4 deferrals)

- **Partial items do not inherit condition from parent** — when a consumed item yields a partial (`remaining != null`), the `ActionRecord` does not carry the condition triple of the source `ItemInstance`. The partial item is created with `rate = 0` (non-perishable). Fix: store `conditionAtStamp / stampTime / rate` on `ActionRecord` and copy them to the partial on creation.

- **DevReStampItemServerRpc — OnGUI surface** — the re-stamp debug RPC exists but has no OnGUI button. A minimal trigger (key binding or debug button in the inventory panel) would make rate-modifier testing easier without needing a custom test script.

---

## Action Framework (0.2.3 deferrals)

- **interruptsSkip integration (0.2.3b inert flag)** — `ActionRecord.interruptsSkip` and `ItemDef.interruptsSkip` are authored and stored but the flag has no effect yet. When Skip Time integration is added, SkipManager should check for any active or queued action with `interruptsSkip = true` before starting a skip, and halt a running skip if such an action is queued while it is in progress.

- **Action queue sync progress bar** — the `ActionQueuePayload` NV carries `ActiveStart`, `ActiveDuration`, and the current `WorldClock` value (available via `WorldClockSync`). Use these to render a per-tick progress bar on the owning client once the UI passes happen. The data pipeline is complete; the display is placeholder OnGUI only.

- **Per-queued-item cancel buttons in OnGUI** — `CancelActionServerRpc(queueIndex)` supports any queue index, but the current OnGUI only exposes the active-slot cancel button. A future UI pass should expose a cancel button per queued entry (indices 1…N), especially once the queue can grow beyond 2 entries.

- **Action pool / energy tuning** — `timeCost`, `energyCost`, `allowsOverdraft`, and `MovementConfig` rates are all set to design-placeholder values (timeCost = 10 min for food, energyCost = 0, movement rates = 0). Real GDD tuning deferred to the balance pass.

- **Nourishment buff display** — the "Nourishing (Hunger)" and "Nourishing (Thirst)" buffs appear in the buff panel as-installed, but have no custom display names or icons. Fine for dev; needs art/text pass before player-facing.

- **Exhaustion debuff magnitude** — the "exhaustion" BuffDef SO is authored at Inspector time with a placeholder modifier value. Actual penalty (drain rate increase, energy recovery reduction, etc.) needs GDD sign-off before tuning.

---

## Processing Chains (0.2.8 deferrals)

- **Double-yield edge case — same-tick dual Transform completion** — if two separate `onDepletion = Transform` actions on the same processable both complete in the same tick (two dreamers each finish a different action simultaneously), `TriggerActionComplete` for the second action will find the processable already removed (first action's Transform already ran). The `layer.processables.items.Contains(proc)` guard in `TriggerActionComplete` prevents the crash and silently skips the second action's yields. Document and test explicitly; the correct resolution (second action should still deliver its yields even though the processable is gone) may require processing yields before the Contains check.

- **Tool selection UI** — `StartContributor` currently auto-selects the first matching tool in the dreamer's inventory (lowest-index item with a matching `toolCategoryId`). Player tool selection from the Dev panel or gameplay UI is deferred to Cluster 7. The seam is the `allowedToolCategoryIds` / `toolCategoryId` already being on the data types.

- **Tool rate bonus** — `GatherableActionDef.allowedToolCategoryIds` supports multiple categories (e.g., Axe or Saw), and different tools providing different rates is noted as a design intent. The actual per-tool rate modifier is not wired; all matching tools currently accrue at the base `ratePerMinute = contributors.Count`. Wire the rate scalar in Cluster 7 alongside the tool selection UI.

- **Tree stump → felling integration** — `TreeStumpDef` (objectDefId = 10) is authored with zero actions. The felling sequence (in-scene tree → chop → remove tree + spawn stump + spawn fallen tree) requires the 0.2.7b `HarvestNode` system. Until that system is implemented, the stump is never spawned in play; the FallenTree chain is entered via debug spawn.

---

## Interaction / §1.12 unification (0.2.9 deferrals)

- **Inventory-context actions not yet unified as ActionDefs** — world pickup is now the single shared `pickup` ActionDef on every inventory Def (Interactable HUD → `DispatchWorldAction` → `InstantPickup`). But **consume / drop / split / equip** are still bespoke buttons + RPCs in `DreamerInventorySync.OnGUI` (`ConsumeItemServerRpc`, `DropItemServerRpc`, `SplitItemServerRpc`, `MoveItemServerRpc`), not `context = Inventory` ActionDefs surfaced by `Interactable.InventoryActions()`. Completing §1.12 means modelling these as inventory ActionDefs with outcomes (`Consume`, plus new Drop/Equip outcomes) and driving the inventory HUD from that one list — the same way the world HUD is now driven. Deferred per the scope decision when world pickup was unified.

- **Dev Gather panel lists instant actions** — now that every item carries the instant `pickup` action, `GetNearbyProcessables` surfaces plain items in the Dev → Gather panel with a "pickup 0%" + Go/Stop row. Harmless (StartContributor redirects instant actions to `DispatchWorldAction`), but the dev panel should hide/relabel `requiredLabor == 0` actions to avoid the misleading progress affordance.

- **World-object range validation on dispatch** — `MapEntitySync.DispatchWorldAction` / `InstantPickup` no longer range-check the dreamer (the removed `PickupItemServerRpc` did). The client only targets within `InteractableDetector._interactRange`, but the host should re-validate distance for authority once interaction leaves the dev-tool stage.

---

## Action economy / generic-verb model (0.2.9 deferrals)

- **Reward timing (implemented) — remaining polish.** Costs are up-front + pro-rata refund; rewards (positive `effects` and `yields`) follow `yieldModel`: **Atomic** delivers on completion, **Proportional** delivers over the work (`MapEntitySync.DeliverActionRewards` / `DeliverProportionalProgress`). Open items: (a) **co-op reward distribution** — reward *effects* are applied to each active contributor (full each), while *yields* go only to the primary contributor; revisit whether yields should split. (b) **proportional discrete yields** round down (cumulative `floor(fraction × amount)`), so a 1-item proportional yield lands only at completion — fine, but document per-def. (c) Instant actions (pickup) still have no cost/reward hook.

- **Proportional delivered-fraction not persisted** — like the join clock, `_proportionalDelivered` is a non-persisted per-(instance,action) dict; a save/load mid-proportional-action re-seeds to current progress (no double-delivery, but the exact split across a reload boundary is approximate). Persist on the accrual state (schema bump) if it matters.

- **Fold eating/drinking onto the ItemAction model** — `ConsumeItemServerRpc` still uses the bespoke `ConsumableAspect` fields (`timeCost`, `energyCost`, `hungerRestore`, `thirstRestore`) + the ActionRecord queue, rather than an inventory-context `ItemAction` (verb = `consume`) whose `timeRequired` + `effects` declare the costs/rewards. Both now use the same **up-front debit + refund** shape, so this migration is mostly plumbing; it completes the one-economy vision (§1.12 inventory-action unification).

- **Prerequisite matches by verb** — `ItemAction.prerequisite` is an `ActionDef` (verb); `StartContributor` gates on the sibling ItemAction that binds that verb (first match). If an item ever has two bindings of the same verb with different prerequisites, disambiguation by index/label is needed.

- **Effect tuning pass** — `TreeChainBuilder.WorkCost(timeRequired)` seeds a placeholder energy total of `2 × timeRequired` on the timed tree-chain bindings; `timeRequired` itself is the timePool cost. Real values need the GDD balance pass.

- **Join clocks are not persisted** — the up-front-cost refund uses a non-persisted per-(instance,action,slot) join clock in `MapEntitySync`. If a save/load or revert happens mid-action, the join clock is lost, so a subsequent stop refunds 0 (the worker keeps the full up-front debit for that action). Edge case; persist the join clock on the accrual state (schema bump) if it matters. **Station crafts (0.2.10c) share this same `_joinClock`, so the same edge applies to a save/load mid-station-craft.**

---

## Authored nodes (0.2.9f deferrals)

- **Node regen after depletion** — a depleted authored node stays hidden (its `depleted` delta persists); there is no regrow timer yet. The MapEntityLayer design (TODO above) lists `regen timer` as node state — wire it (a resolver stage or timestamp check that clears `depleted` + resets accrual after N IG-minutes) when the gathering economy is tuned.

- **Materialised authored position synced redundantly** — an authored node, once materialised, rides the runtime `WorldObjectPayload` which includes its position (static, matches the scene). The TDD's "position local" is honoured for pristine nodes (nothing sent) but a materialised node sends its position too. Harmless (O(damaged)); drop authored positions from the wire (client already uses the scene GameObject transform) if bandwidth matters.

- **Authored instant actions** — `DispatchWorldAction` materialises an authored node for any action, including instant outcomes (`InstantPickup`/`ToggleCarry`). Authored gatherables don't carry a pickup action today, but if one ever does, picking it up would remove the delta while the scene GameObject (still registered) re-shows on the next payload — reconcile (hide the scene GO or forbid pickup on authored) if that case arises.

## Crafting (0.2.10 deferrals)

- **Hand-craft partial → in-progress item (c2 for hand)** — `CancelCraftServerRpc` currently refunds the unelapsed time+energy **and returns the materials in full**. §5.5.4/§5.7.1 call for an in-progress craft item to materialise in inventory instead (resumable "1h today, finish tomorrow"). **Station** partial/resume is fully implemented (progress persists on the station `Instance.craft` + accrual, resumable by anyone); only the **hand** in-progress-item form is deferred. Implementing it needs an item Instance that carries recipe id + accumulated progress and a resume path that continues rather than restarts.

- **Station-craft progress display** — the client HUD (`InteractableDetector` / dev Gather panel) computes a craft action's % as `labor / ItemAction.timeRequired`, but a station craft's real threshold is `Instance.craft.requiredLabor` (recipe-driven), so the displayed % is wrong for the `craft` action. The craft completes correctly (host uses `requiredLabor`); only the bar is off. Also the dev Gather panel's **Go** on the `craft` action is intentionally rejected by `StartContributor` (station crafts commit via `StartStationCraft`). Fix when the station UI is built (below): read `requiredLabor` for craft-action progress, and hide/relabel the craft action in the generic gather panel.

- **Station-craft UI (recipe selection at a station)** — dispatch is currently a dev-panel affordance (`DreamerTaskSync` page 4 → `FindNearbyStation` → `RequestStationCraftServerRpc`). A player picks a recipe from a list and it targets the nearest matching station. A proper station UI (open the station's Interactable → choose from the recipes it supports) is deferred to the UI pass / Cluster 4 placement. The host entry point (`MapEntitySync.StartStationCraft`) is UI-agnostic and ready.

- **Station-craft materials come from the committing crafter only** — the recipe's `inputs` are consumed from the dreamer who *starts* the craft (`obj.craft` is set once). Assisting co-op crafters contribute labor but no materials, and the output collects **at the station** (not auto-delivered). Revisit whether co-op should pool materials / split output when the co-op economy is tuned.

- **Broken destroy-and-replace is wired only for the inventory path** — `ReplaceToolWithBrokenForm` fires from `DevUseToolServerRpc` (the only current durability-loss path). When gathering tool-wear (§5.6.3 durability pre-debit) lands, wire the same destroy-and-replace for the **world** (dropped/placed tool at durability 0) and **carried** locations — the `brokenFormDefId` field and the resolver logic are ready; only the world/carried call sites are missing. If a tool has no `brokenFormDefId` (0), it is simply destroyed on break (no replacement) — confirm that is desired vs. a default broken form.

- **Skill-weighted tier selection (b5 seam)** — `CraftRng.TierIndex` takes a `skillModifier` parameter, currently inert (the authored `tierTable` weights are the fixed unskilled distribution). Activate the reshape at Cluster 7 alongside the §5.6 `skillModifier` gathering seam.

- **Recipe unlocks (a1 `unlockConditionId`)** — authored but inert; all recipes are `known`. Activate the discovery gate at Cluster 9 (Tech).

- **World-object crafting outputs** — 0.2.10 is **item outputs only**. World-object outputs (workbench/building/furniture/trap) produce a blueprint handed to the Cluster 4 placement system; a crafted station then serves other recipes for free (c4 is verified with a debug-placed station until placement exists).

- **Per-instance tool stats rolled at craft (§5.6.1)** — `baseChopDmg` / attack-tier are noted as per-instance tool stats "rolled at craft time (0.2.10)". The current `ToolAspect` has no such fields (combat rate uses fixed/debug values); crafting rolls **which tier Def** you get, not a stat within a Def. Add per-instance rolled stats if/when combat needs them.

## Dead code (CodeFiles scan deferrals)

- **`DreamerCreationUI.cs` still referenced by a scene** — the Build 0.0.1b creation UI, superseded by `CharacterCreationController` at 0.0.1c, could not be deleted in the cleanup pass because its GUID (`a6f71806eee2ba1489bf23cd57d70cdc`) is still bound to a live component. The one reference is **`Assets/Game/Scenes/BootstrapScene.unity`** — GameObject **`DreamerCreationUI`** (MonoBehaviour fileID `934584652`, GameObject fileID `934584651`), a root-level object with no children. No prefabs and no `.asset` files reference it. Deleting the script now would leave a missing-script component in BootstrapScene. **To resolve:** in the Editor, delete that GameObject from BootstrapScene, save the scene, then delete `Assets/Game/Networking/DreamerCreationUI.cs` and its `.meta`. `WorldItemsSync.cs` had zero references and was deleted in the same pass.

## Action & time model revision (0.2.11 deferrals)

- **b4 and c7 disagree about guard-stopped skips, and c7 won.** b4 lists "a guard trip" among the events that end contribution; c7 says a guard-capped skip "advances the clock only as far as it ran… and the remainder continues at 1×", which requires the commitment to survive. They cannot both hold. As built: an **Interrupt** stop releases all presence (`MapEntitySync.ReleaseAllPresenceOnInterrupt`), a **guard** stop leaves the commitment running at 1×. That reading keeps the acceptance script coherent — nothing in it says a guard stop drops the commitment — and is the more forgiving of the two. Reconcile the TDD wording next time §5.5 is edited.

- **Sleep and rest still cost no day-pool time** — §5.5.2 puts sleep/rest/nap on the day pool along with everything else, but the shipped Task channel never debited it and 0.2.11 did not change the economy. `NeedsConfig.CreateTaskAction` mints them with `timeCost = 0` and a comment saying so. Decide the intended cost and wire the debit + pro-rata refund when the sleep economy is tuned.

- **Queue-ahead is consumables-only** — 0.2.11a3 preserved the FIFO queue on the surviving slot, but only duration-driven entries auto-advance: SimResolver stage 4 starts the head entry when it times the previous one out. Object-bound labor and crafts are `externallyResolved` and need an RPC to open their accrual segment, so they are *rejected* while the slot is busy rather than queued behind it. §5.5.16 wants "line up the next two actions while standing at the tree" — that needs a host-side pending-commit queue that re-issues the commit when the slot frees. Not hard, but it is new machinery rather than a tweak.

- **A non-Trivial consumable still occupies the slot** — eat/drink are Trivial after the 0.2.11a2 pass, so they execute instantly and no longer block work. But a consumable authored Active (the save-consumable ritual), or a Trivial one that fell back to Active on a short bracket (d6), takes the single slot for its full duration and blocks gathering until it finishes. That is the §5.5 rule working as designed, not a bug — noted because it reads like one in a playtest.

- **The trivial bracket is unbounded within a day** — d3 converts one pool minute per idle skip minute with no ceiling, and only the wake reset (d4) brings it back to base. Two long skips while idle can bank a very large bracket. §5.5.5 calls the bracket "explicitly bounded", which the wake reset satisfies day-to-day, but a per-day conversion cap may still be wanted once skip lengths are tuned.

- **The skip-consent flow does not reuse `ConsensusVoteComponent`** — §4.5 and that component's own doc-comment say the skip vote should reuse it, but the single instance is owned by `DreamFlowManager` for the Wake Up vote, and a skip request arriving mid-rescue would fight it for the same NetworkVariables. 0.2.11c5/c6 therefore use their own pair of NetworkVariables plus accept/decline/withdraw RPCs on `SkipManager`. Unify when the component is generalised to multiple concurrent votes (it needs an owner/topic key).

- **The can-skip indicator's in-combat term is a seam** — `DreamerNeedsSync.DrawCanSkipIndicator` computes guard-tripped and downed for real; `inCombat` is a hardcoded `false` because there is no combat system until Cluster 3. OR it in there when combat lands — the indicator is already the single place the answer is computed, so it is a one-line change.

- **b5's UI gating was a no-op in code** — the non-modal guarantee (camera, inventory, map, panels, queueing and cancel all live while committed) holds because nothing in the codebase ever gated those on task state. Movement is not suppressed either; walking simply cancels the action via the presence monitor, which is what b5 asks for. If a future build adds a "you are busy" input gate, this guarantee has to be defended explicitly.

- **v19 → v20 save migration is a one-off, not a framework** — `SaveSystem.PromoteTwoChannelDreamer` + `TwoChannelSchemaVersion` accept a single legacy schema so pre-0.2.11 dev saves still load (a5). Only sleep/rest are promoted with their remaining duration; a legacy Gathering/Crafting marker is dropped (its real state lives on the Instance/CraftRecord and re-opening it needs the accrual segment and join clock the marker never held). **Delete both at the next schema bump** — the standing rule (CLAUDE.md) is that old saves are rejected loudly until Early Access.

- **`ActionClass` is authored in three places, not one** — `ActionDef` (world/inventory verbs), `ConsumableAspect` (eat/drink), and `RecipeDef` (crafts), because those are three independent def kinds with no common base. Sleep/rest are config fields on `NeedsConfig`. Nothing derives a class implicitly, but a single authoring surface — or an editor validator that lists every def and its class — would make the reclassification pass auditable rather than manual.

- **Presence range and tolerance are global, not per-action** — b1 says "within the task's interaction range", implying a per-def range. As built there are two `NeedsConfig` values (`presenceRange`, `presenceMoveTolerance`) shared by every action. Per-action ranges belong on `ItemAction` alongside `timeRequired` when an action needs a genuinely different reach (a long saw vs. a berry bush).

- **Instant eating retires the gradual-nourishment buff for food** — 0.2.3b5 delivered a meal's hunger/thirst over its duration via the `nourishment_hunger` / `nourishment_thirst` buffs, tick by tick. Food and drink are now Trivial and unconditionally instant, so there is no window to spread restoration over: `ConsumeItemServerRpc` applies `hungerTotal` / `thirstTotal` whole. The buff defs, `ActionRecord.HungerPerMin/ThirstPerMin` and `SimResolver.InstallNourishmentBuffs` all still exist and still work — they are simply unreachable for Food/Drink now, since those can no longer take the Active path. Decide whether gradual restoration comes back as a *post-meal* buff (eat instantly, digest over the next hour, which is both more realistic and keeps the Trivial rule intact), or whether the machinery should be deleted. Do not leave it in limbo indefinitely — unreachable code that looks live is worse than either outcome.

- **Partial food items restore by `hungerTotal`, not `def.hungerRestore`** — the pre-0.2.11 instant-consume branch (`duration <= 0`) applied the Def's *full* restore values even when the item was a partial, so eating half a berry ration healed like a whole one. The unified instant path uses the partial-aware `hungerTotal` / `thirstTotal`. Called out because it is a silent behaviour fix riding along with 0.2.11d, not an intended change of that build — verify it against the intended partial-item economics rather than assuming the new number is right.

## Dreamer spawn points (scene-authored spawn locations)

- **Spawn facing is not persisted.** A `DreamerSpawnPoint`'s yaw is applied at spawn (FirstPersonLook adopts the body's authored heading on enable), but `DreamerRecord` carries a position and no rotation, so a loaded save restores where the dreamer stood and *not* which way they faced. Add a yaw field to `DreamerRecord` at the next schema bump if restored facing matters — it is a one-field change plus the template update, deliberately not taken now to avoid a schema bump for a testing convenience.

- **Spawn points are scene objects, not world data.** The markers live in the Action scene and are resolved at spawn time by `DreamerSpawnPoints`. That is the right shape for one hand-built map, but the eventual needs — per-region entry points, dream/rescue wake locations, moving the "camp" as the story progresses — want spawn locations as authored *data* keyed by id, with the scene markers reduced to their visual representation. Revisit when world authoring (Cluster 4 placement / the map pass) lands.

- **Ground snap needs a collider.** `_snapToGround` is a physics raycast, so it resolves against the TerrainCollider or any mesh collider below the marker. A marker placed over a collider-less visual (or outside the terrain) keeps its authored height and logs nothing. If spawning over holes/water becomes routine, add a validation pass that flags markers with no ground under them.

- **`Policy.Always` is a testing mode that silently defeats save restore.** Left on, every load puts the dreamers at the marker instead of where the save says. It is documented in the Inspector tooltip and logged on each spawn (`(spawn point)` vs `(record)` in the spawn log line); no harder guard than that. Set the shipping scene back to `NewGameOnly` before any build that is not a test.

## Ground snap for world placements

- **Ground snap is a raycast, not physics settling.** `GroundSnap` puts an object's base on the first
  qualifying surface directly beneath (or, if buried, above) its pivot. It does not orient to the
  slope normal, does not resolve horizontal overlap, and will happily rest a long log's midpoint on a
  ridge with both ends in the air. If yields start looking wrong on rough terrain, the next step is
  orienting to the surface normal and/or a brief rigidbody settle at placement — both are bigger
  changes than this was, and both need a decision about whether the settled position (not the
  authored one) is what gets saved.

- **The above-fallback can lift a placement onto a roof.** When nothing qualifies *below* a position,
  the resolve uses the lowest surface *above* it — which is what rescues a yield from inside the
  terrain. Inside an enclosed space with no floor collider beneath (a future cave, a raised platform
  with an open underside), the same rule would put the object on the ceiling instead. Probe Above on
  `MapEntitySync` bounds how far that can reach; revisit if interiors become real geometry.

- **`Align To Prefab Base` trusts renderer bounds.** The per-Def base offset comes from the combined
  renderer bounds of the Def's prefab, cached on first use. A prefab carrying an oversized renderer
  (an effect volume, a debug visual) reads as taller than the object looks and will hover. The offset
  is clamped to "base at or below pivot, within 50 m" and otherwise falls back to placing by pivot —
  so a bad prefab degrades to the old behaviour rather than flinging things into the sky, but it is
  still worth an authoring check when a Def gets its real art.

- **Authored nodes are deliberately not snapped.** `ResolveWorldObject` materialises an authored node
  at its scene GameObject's position, on the grounds that a hand-placed node's position is authored
  intent. That means an authored node sunk into the terrain stays sunk — only what it *yields* is
  lifted out. If authored nodes should also be corrected, that belongs in an editor-time validation
  pass over the scene, not at runtime.
