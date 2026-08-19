# Manual Steps Log
Steps that require Unity Editor interaction and cannot be performed by Claude Code.
Checked = done. Each entry records what was done and why, so the project can be reconstructed.

---

## Build 0.0.0 — Scaffolding & plumbing smoke test

- [x] **T1 — Create Unity 6 LTS project**
  - Created via Unity Hub; selected Unity 6000.x LTS; chose URP as render pipeline.

- [x] **T3 — Assembly definitions**
  - Created four folders under `Assets/Game/`: `Simulation`, `Presentation`, `Networking`, `Persistence`.
  - Created a `.asmdef` file in each via *Assets > Create > Assembly Definition*.
  - Set `Game.Simulation` → **No Engine References** = ✅ (enforces the simulation wall).
  - Added references in the other three asmdefs: all reference `Game.Simulation`; `Game.Networking` and `Game.Presentation` also reference NGO.

- [x] **T4 — Wire NetworkManager scene**
  - In `SampleScene`: created an empty GameObject named `NetworkManager`.
  - Added component: `NetworkManager` (from NGO).
  - Added component: `Unity Transport` to the same GameObject; dragged it into the **Network Transport** slot on `NetworkManager`.
  - Created another GameObject; attached `ConnectionBootstrap`.

- [x] **T5 — MPPM two-client smoke test**
  - Window → Multiplayer → Multiplayer Play Mode → enabled **Player 1** (one virtual player).
  - Pressed Play; clicked **Start Host** in the main editor window, **Start Client** in the virtual player window.
  - ✅ Host console logged two distinct client IDs → Build 0.0.0 acceptance met.

---

## Build 0.0.1a — Networked core (single scene, debug connect)

- [x] **A5 — Dreamer prefab**
  - Create a new GameObject in the scene → add a **Capsule** mesh (or use GameObject > 3D Object > Capsule).
  - Add components: `NetworkObject`, `NetworkTransform`, `DreamerNetworkAdapter`.
  - On `NetworkTransform`: leave defaults (world-space, all axes synced).
  - Save as prefab: drag into `Assets/Game/Presentation/Prefabs/DreamerPrefab.prefab` (create the folder if needed).
  - Delete the in-scene instance after saving the prefab.
  - In the scene's `NetworkManager` component → **Network Prefabs** list → **+** → drag in `DreamerPrefab`.

- [x] **Scene wiring — RuntimeDataManager**
  - Create empty GameObject named `RuntimeDataManager`; attach `RuntimeDataManager` component.

- [x] **Scene wiring — DreamerSpawner**
  - Create empty GameObject named `DreamerSpawner`; attach `DreamerSpawner` component.
  - Drag `DreamerPrefab` into the **Dreamer Prefab** slot in the Inspector.

- [x] **Scene wiring — DebugAutoConnect**
  - Create empty GameObject named `DebugAutoConnect`; attach `DebugAutoConnect` component.

- [x] **A7 — MPPM virtual player detection**
  - No tag or argument setup needed.
  - `DebugAutoConnect` uses `CurrentPlayer.IsMainEditor` — main editor → auto-hosts; any virtual player → auto-joins as client.
  - *(A tag was set during investigation but is not required by the current implementation.)*

- [x] **Asmdef — Add MPPM reference to Game.Networking**
  - Select `Assets/Game/Networking/Game.Networking.asmdef` in the Project window.
  - Inspector → **Assembly Definition References** → **+** → search for `Unity.Multiplayer.Playmode` → select it → **Apply**.
  - Fixes `CS0234`: allows `DebugAutoConnect` to call `CurrentPlayer.Tag` under `#if UNITY_EDITOR`.

- [x] **A8 — Two-client test**
  - Press Play with MPPM enabled (one virtual player).
  - No buttons needed — both instances auto-connect.
  - ✅ Acceptance met: host loaded template, slot 0 spawned (ownerClient 0, isOwner True), slot 1 spawned (ownerClient 1, isOwner False on host / True on client). Both clients see both dreamers.
  - ⚠️ Known gap: `DreamerNetworkAdapter.Slot` reads -1 on the client — `Initialize(slot)` is host-only. Fix: promote `_slot` to `NetworkVariable<int>` in Build 0.0.1b alongside the appearance NetworkVariable.

---

## Build 0.0.1b — Character creation sync

- [x] **B3/B5 — Add DreamerCreationUI to scene**
  - Create empty GameObject named `DreamerCreationUI`; attach `DreamerCreationUI` component.
  - No other wiring needed — it finds the owned adapter automatically at runtime.
  - B5 appearance (skin tone color) is applied procedurally via `DreamerNetworkAdapter.ApplyAppearance`; no materials to create for Phase 0.

- [x] **0.0.1b test**
  - Press Play with MPPM (both clients auto-connect as before).
  - Each player sees a creation panel for their own dreamer (Slot 0 / Slot 1).
  - Adjust skin tone with ◀ ▶, click Confirm.
  - ✅ Acceptance: capsule color updates on both clients; host console logs appearance confirmed for each slot; RDM record updated.

---

## Build 0.0.1c — Scene flow & menu chrome

- [x] **Asmdef — Add Game.Persistence reference to Game.Networking**
  - Select `Assets/Game/Networking/Game.Networking.asmdef` in the Project window.
  - Inspector → **Assembly Definition References** → **+** → search for `Game.Persistence` → select it → **Apply**.
  - Required so `GameFlowManager` can call `TemplateLoader.Load()`.

- [x] **C1 — Create and register five scenes**
  - File → New Scene (Empty) for each: `Splash`, `MainMenu`, `HostJoin`, `CharacterCreation`, `Action`.
  - Save each under `Assets/Scenes/`.
  - File → Build Settings → add all five scenes in order: Splash (0), MainMenu (1), HostJoin (2), CharacterCreation (3), Action (4).

- [x] **C2 — Splash scene**
  - Create empty GameObject `SplashController`; attach `SplashController` component.
  - Set **Delay** = 2 (or adjust to taste).

- [x] **C2 — MainMenu scene**
  - Create empty GameObject `MainMenuController`; attach `MainMenuController` component.

- [x] **C2/C3 — Splash scene — persistent managers**
  - Create empty GameObject `GameFlowManager`; attach `GameFlowManager` component; drag `DreamerPrefab` into **Dreamer Prefab** slot.
  - Create empty GameObject `RuntimeDataManager`; attach `RuntimeDataManager` component.
  - *(Both are `DontDestroyOnLoad` — placing them in Splash means they exist for the entire session.)*
  - Also add the `NetworkManager` + `UnityTransport` to Splash (or move it here from SampleScene), and register `DreamerPrefab` in its Network Prefabs list.

- [x] **C2 — HostJoin scene**
  - Create empty GameObject `HostJoinController`; attach `HostJoinController` component.

- [x] **C3/C4 — CharacterCreation scene**
  - Create empty GameObject `CharacterCreationController`; attach **both** `NetworkObject` and `CharacterCreationController` components.
  - *(In-scene NetworkObject — no entry in Network Prefabs needed; NGO syncs it automatically on scene load.)*

- [x] **C3 — Action scene**
  - This scene intentionally has no spawner or dreamer GameObjects — `GameFlowManager.OnActionSceneLoaded` handles spawning after all clients have loaded.
  - Add a floor/plane so spawned capsules have something to land on.

- [x] **C5 — Disable DebugAutoConnect in SampleScene**
  - Select the `DebugAutoConnect` GameObject in SampleScene → Inspector → **Active** checkbox = ☐ (unticked).
  - The real flow now starts from Splash; SampleScene is the debug path only.

- [x] **Fix — Disable ConnectionBootstrap**
  - The `ConnectionBootstrap` GameObject from Build 0.0.0 is still active and shows its OnGUI buttons on top of the new flow.
  - Find `ConnectionBootstrap` in SampleScene (or wherever it lives) → Inspector → uncheck the component (or delete the GameObject).

- [x] **Fix — Verify Enable Scene Management**
  - Select the `NetworkManager` GameObject → Inspector → **NetworkConfig** section → confirm **Enable Scene Management** is ✅ ticked.
  - If unchecked, clients will never receive scene-load notifications from the host and will stay in whatever scene they launched from.

- [x] **Design note — No dreamer preview in CharacterCreation**
  - This is by design for Phase 0. Dreamers are spawned in the Action scene after `OnLoadEventCompleted` fires. The CharacterCreation scene is a pure UI scene — appearance choices are written to the RDM and picked up by the dreamer at spawn time. A live 3D preview is a future feature.

- [x] **0.0.1c test**
  - Set **Build Settings → First Scene** to Splash (index 0); press Play with MPPM.
  - Flow: Splash → MainMenu → HostJoin → (host clicks Host Game, client clicks Join Game) → CharacterCreation → both confirm appearance + click Ready → host clicks Start Game → Action scene loads → both capsules spawn at authored positions with correct colours.
  - ✅ Acceptance: full flow runs end-to-end with two MPPM clients.

---

## Build 0.0.2 — Client-authoritative movement

- [x] **Asmdef — Add Unity.InputSystem to Game.Presentation (if not already)**
  - Select `Assets/Game/Presentation/Game.Presentation.asmdef`.
  - Inspector → **Assembly Definition References** → **+** → search for `Unity.InputSystem` → select → **Apply**.
  - Required for `DreamerMovementController` to use `UnityEngine.InputSystem`.

- [x] **Dreamer prefab — add CharacterController**
  - Open `DreamerPrefab` in the Prefab editor.
  - Add Component → **Character Controller**. Leave defaults (height 2, radius 0.5).
  - Add Component → **DreamerMovementController**. Set **Speed** = 5.

- [x] **Dreamer prefab — set NetworkTransform to owner-authoritative**
  - On the `NetworkTransform` component, find **Authority Mode** (or **In Owner Authoritative** checkbox depending on NGO version) → set to **Owner**.
  - This lets the owning client drive the transform directly; the NetworkTransform replicates it to everyone else.

- [x] **0.0.2 test**
  - Press Play (full flow or debug path).
  - Each client moves their own dreamer with WASD — the other client's dreamer stays still.
  - ✅ Acceptance: movement is smooth on both clients; host console: `RuntimeDataManager.GetDreamerPosition(0)` and `GetDreamerPosition(1)` return correct world positions (verify via a temporary Debug.Log if needed).

---

## Build 0.0.3 — Save & load round-trip

- [x] **D1 — Verify Newtonsoft resolves (already in manifest)**
  - `com.unity.nuget.newtonsoft-json 3.2.2` is in `Packages/manifest.json`.
  - If `Game.Persistence` gets a CS0246 error about `Newtonsoft.Json`, it is not auto-referenced: select `Game.Persistence.asmdef` → **Assembly Definition References** → **+** → search `Newtonsoft.Json` → **Apply**.

- [x] **D5/D7 — Add Game.Networking reference to Game.Presentation**
  - `MainMenuController` now calls `GameFlowManager` and `SaveSystem` — both require cross-assembly visibility.
  - Select `Assets/Game/Presentation/Game.Presentation.asmdef` → **Assembly Definition References** → **+** → add `Game.Networking` → **Apply**.
  - Also add `Game.Persistence` the same way (for `SaveSystem.SlotExists()`).

- [x] **D7 (warm) — Add PauseMenuController to Action scene**
  - Create empty GameObject named `PauseMenuController` in the **Action** scene.
  - Attach `PauseMenuController` component.
  - No other wiring needed — it reads `NetworkManager.IsHost` at runtime.

- [x] **0.0.3 test**
  - **Save:** Full flow into Action → move both dreamers → host clicks **Save** (top-right). Verify `persistentDataPath/Saves/slot_0/` contains three JSON files with updated positions.
  - **Warm load (pause menu):** Move dreamers again → host presses **Escape** → pause menu appears → host clicks **Load Save** → both dreamers snap back in place (no scene transition). Client's dreamer also snaps via SnapToPositionRpc.
  - **Cold load (main menu):** Quit → relaunch → Main Menu → host clicks **Load Game** (greyed if no save) → Action scene loads → both dreamers spawn at saved positions.
  - **Quit/relaunch persistence:** Quit → relaunch → Load → same saved positions.
  - ✅ Acceptance: New Game = authored positions; warm load = in-place snap; cold load = spawn at saved positions; both paths place the client-owned dreamer at the saved position.

---

## Build 0.0.4 — Authoritative clock

- [x] **E2/E3/E5 — Add clock objects to Action scene**
  - Create empty GameObject `WorldClockDriver`; attach `WorldClockDriver` component.
  - Create empty GameObject `WorldClockSync`; attach **both** `NetworkObject` and `WorldClockSync` components.
    *(In-scene NetworkObject — same pattern as CharacterCreationController. No prefab registration.)*

- [x] **0.0.4 test**
  - New Game → Action. Verify "Day 1 — 00:00" HUD at screen centre advances on both clients at the same rate.
  - Host presses **Escape** → clock freezes on both; host clicks **Resume** → clock continues.
  - Host clicks **Save** in pause menu; quit → relaunch → **Load Game** → saved time is restored on both clients.
  - *(E6 schema check)* Edit a save's `world.json` to set `"schemaVersion": 1` → trigger Load → console logs a clear mismatch error and refuses to load.
  - ✅ Acceptance: matching advancing time on both clients; host pause/resume affects both; save/load restores the clock; old saves rejected.

---

## Build 0.0.5 — Disk checkpoint & revert (the spine proof)

- [x] **No new scene objects needed**
  - All 0.0.5 functionality is in code. The Action scene is unchanged.
  - The Checkpoint / Revert buttons appear automatically in the Escape pause menu (Dev section, host-only).

- [x] **0.0.5 test**
  - Full flow into Action → move both dreamers → let the clock advance a few minutes.
  - Host presses **Escape** → click **Checkpoint**. Verify `persistentDataPath/Saves/checkpoint/` contains `world.json` + `dreamer_0.json` + `dreamer_1.json`.
  - Move both dreamers again and let more time pass.
  - Host **Escape** → click **Revert**. Both clients should snap back to the positions and time at checkpoint time, then the clock resumes ticking from there.
  - Check the client-owned dreamer also snapped (not just the host's).
  - Verify `world.json` in the checkpoint slot was not corrupted (open and inspect — it should be valid JSON matching the captured state, never a truncated file).
  - ✅ Acceptance: both clients snap to the captured positions (client-owned dreamer included) and the captured clock time; ticking resumes. Phase 0 complete.

---

## Build 0.1.0 — Resolver heartbeat + needs drain

- [x] **Dreamer prefab — add DreamerNeedsSync**
  - Open `DreamerPrefab` in the Prefab editor.
  - Add Component → **DreamerNeedsSync**.
  - Leave **Sync Interval Seconds** = 1 (default). No other wiring needed — it finds `DreamerNetworkAdapter` via `GetComponent` on spawn.

- [x] **0.1.0 test**
  - Full flow into Action (New Game). Verify three need bars appear in the bottom-left corner of the owning client's screen: `Hunger`, `Thirst`, `Warmth`, all starting at 100.
  - Wait ~6 real seconds (= 1 IG minute). All three values should tick down by their drain rate (Hunger −0.14, Thirst −0.21, Warmth −0.10 approximately).
  - Verify both clients show matching values (the non-owning client sees the other dreamer's needs update too, though only the owner sees the HUD for their own dreamer).
  - **Save/load round-trip:** in Action → let needs drain a few steps → host **Escape** → **Save** → open `persistentDataPath/Saves/slot_0/dreamer_0.json` and confirm `"needs"` is present with drained values. Then **Load Save** (warm) → needs values snap back to the saved values on both clients.
  - **Checkpoint/revert:** take a checkpoint after some drain → drain more → revert → needs snap back to the checkpoint values on both clients.
  - *(Schema check)* Edit any save's `world.json` to set `"schemaVersion": 2` → trigger Load → console logs a clear version mismatch error (schema is now 3).
  - ✅ Acceptance: need bars visible and draining in real time, identical on both clients; values round-trip through save/load and checkpoint/revert.

---

## Build 0.1.1 — Tasks on the tick

- [x] **Dreamer prefab — add DreamerTaskSync**
  - Open `DreamerPrefab` in the Prefab editor.
  - Add Component → **DreamerTaskSync**.
  - Leave **Needs Config** defaults and **Sync Interval** = 1. No other wiring needed.
  - *(DreamerNeedsSync now also shows an Energy bar — no extra wiring needed there, it reads from the same `DreamerRecord.energy`.)*

- [x] **0.1.1 test**
  - Full flow into Action (New Game). Verify four bars bottom-left: Hunger, Thirst, Warmth, Energy (starts at 80). Task UI appears bottom-right: `Task: Idle` + four buttons (Eat / Rest / Sleep / Idle).
  - **Eat:** click **Eat** on one client. `Task: Eating` appears; progress counts up. After 10 IG minutes (= 60 real seconds), task completes → Hunger jumps up by ~50, Thirst by ~15, task resets to Idle. Verify on both clients.
  - **Rest:** click **Rest**. After 30 IG minutes (= 3 real minutes), Energy jumps up by ~25, resets to Idle. Verify on both clients.
  - **Activity modifier:** start Eat or Rest, observe that needs drain slightly slower during task (hunger/thirst drain at 1× while eating, 0.6× while resting — compare tick-to-tick values).
  - **Save/load:** assign Rest → save mid-task → warm load → task progress restored, energy/needs at saved values on both clients.
  - **Checkpoint/revert:** take a checkpoint mid-task → let task complete → revert → task state returns to mid-progress, energy/needs match the checkpoint.
  - *(Schema check)* Edit a save's `world.json` to `"schemaVersion": 3` → Load → version mismatch error (schema is now 4).
  - ✅ Acceptance: Eat refills hunger/thirst on completion; Rest recovers energy on completion; activity modifier reduces drain while task is active; task state synced + saved on two clients.

---

## Build 0.1.2 — Afflictions + Vitality

- [x] **No new prefab components needed**
  - All changes are in existing scripts. No manual scene or prefab wiring required.

- [x] **0.1.2 test**
  - Full flow into Action (New Game). Verify the HUD now shows Vitality (100) below Energy, with no affliction tags.
  - **Trigger afflictions:** click **Set Critical** (bottom-right dev section). Within 1–2 ticks (~6–12 real seconds), `[STARVING]`, `[DEHYDRATED]`, and `[HYPOTHERMIC]` labels should appear. Vitality begins draining (~6 Vitality/tick with all three active).
  - **Verify Vitality drain:** with three afflictions active, Vitality drains at 6/min; at ~17 ticks (~100 real seconds) Vitality reaches 0 → `⚠ DOWNED` appears on the owning client.
  - **Verify ordering proof:** click **Set Critical** → immediately click **Eat** (10 min task). After 10 ticks, hunger jumps +50. If hunger was 5, it becomes ~53.6 (above the 20 threshold) → `[STARVING]` clears that same tick → no Vitality drain that tick.
  - **Revive:** click **Revive** (dev button) → Vitality resets to 100, `⚠ DOWNED` clears, all needs return to 100, affliction tags clear on the next tick.
  - **Both clients:** verify all HUD values are identical on host and client (not just the owner's view).
  - **Save/load + revert:** Set Critical → let Vitality drain to ~50 → save → Revive → warm load → Vitality back to ~50, afflictions active again. Checkpoint before Set Critical → trigger incap → revert → downed state cleared.
  - *(Schema check)* Edit a save's `world.json` to `"schemaVersion": 4` → Load → version mismatch error (schema is now 5).
  - ✅ Acceptance: afflictions appear when needs are critical, drain Vitality, and self-resolve when needs are met; incap triggers at 0 Vitality; the 3→4→5→6 ordering is validated; all state synced + saved + revertible on two clients.

---

## Build 0.1.3 — Skip (the fast-forward)

- [x] **Action scene — add SkipManager in-scene NetworkObject**
  - Create empty GameObject `SkipManager` in the **Action** scene.
  - Add components: **NetworkObject** and **SkipManager**.
  - Leave **Ms Budget Per Frame** = 33 (default, ~30 fps spinner). No other wiring needed.
  - *(Same pattern as WorldClockSync — in-scene NetworkObject, auto-synced on scene load.)*

- [x] **0.1.3 test**
  - Full flow into Action. Verify the pause menu now has a **— Skip —** section with "Skip 1h", "Skip 4h", "Skip 8h" buttons (host only).
  - **Basic skip:** note current Hunger, Thirst, Warmth values. Press **Escape** → **Skip 1h** (= 60 ticks). A dark overlay appears with "Skipping… X%" while the host runs ticks. After a few real seconds the overlay disappears. Verify needs drained by exactly 60 × drain rate on both clients (e.g. Hunger − 60 × 0.139 ≈ −8.3).
  - **Clock advance:** verify the clock advanced by exactly 60 IG minutes on both clients after the skip.
  - **Task progress in skip:** assign Rest task (30 min) → Skip 1h. After skip: task should be complete (30/30), energy jumped +25, rest of skip (30 more ticks) continued draining at resting rate. Verify on both clients.
  - **Chunking invariance:** skip 4h, then revert to checkpoint → let 4h of real-time tick pass at 6s/min (= 24 real minutes) → compare final needs values. They should be equal (same Step() calls, same results). *(Deferred — see TODO.md)*
  - **Client overlay:** verify the virtual player (client) also sees the dark overlay during the skip and gets the correct final values immediately after.
  - **Skip hides pause menu:** while a skip is in progress, pressing Escape should have no effect; the skip overlay owns the screen.
  - ✅ Acceptance: host runs chunked ticks, client shows overlay then converges on bulk push; clock + needs + tasks advance correctly; chunking-invariance holds; both clients identical after skip.

---

## Build 0.1.4 — Skip control (guards, interrupts, global stop)

- [x] **0.1.4a — Guard stop (single client first)**
  - Enter the Action scene as host only (no MPPM yet).
  - Use **Set Critical** to drop a dreamer's Hunger to 5. Then use **Revive** to restore Vitality / isIncapacitated (so the dreamer is alive again but Hunger stays at ~5).
  - Note current Hunger value (should be ~5, well below the 30% guard floor of 30).
  - Open pause menu → **Skip 8h**. The skip should stop almost immediately (within the first tick or two) because Hunger (5) < guard floor (30).
  - Verify: overlay disappears, both the HUD and the on-screen needs values match; the stop reason label appears for ~3 seconds: "Skip stopped — Dreamer 0 hunger low".
  - ✅ Guard fires before needs hit critical; need value is above 20 (affliction threshold) if starting from ≥ 30; stop reason shown.

- [x] **0.1.4a — Guard stop (MPPM two-client)**
  - Same test with both clients running. Verify both clients drop to real time at the same IG moment and both see the same stop reason label.

- [x] **0.1.4b — Force-wake sleeping dreamer on guard stop**
  - Assign **Sleep** task to the dreamer (8h task). Wait ~2 IG minutes so `elapsedMinutes > 0`.
  - Use **Set Critical** to put a second need (e.g. Thirst) at 5.
  - Open pause menu → **Skip 8h**. Guard fires for Thirst; dreamer is force-woken.
  - Verify: after skip stops, the dreamer's task shows as **Idle** (not Sleeping). Check the Unity console — `[SkipManager] Skip stopped early: GuardThirst` should be logged with actual slept minutes.
  - ✅ Sleeping dreamer is awake/idle after guard stop; actual slept duration logged.

- [x] **0.1.4b — Planned sleep transition mid-skip does not stop**
  - Assign **Sleep** task and note `durationMinutes` (480). Start a **Skip 8h** with all needs well above 30 (≥ 80 recommended — use Revive to top up first).
  - The skip should run to full duration even though the sleep task will complete mid-skip (stage 4 transition, not a guard).
  - Verify: skip runs to 100%, dreamer is Idle (sleep completed), energy jumped +80.
  - ✅ Planned routine transition does not halt the skip.

- [x] **0.1.4c — Debug interrupt (single client)**
  - Start a **Skip 8h** with all needs high. While the overlay is showing, press **F5**.
  - The skip should halt immediately. Overlay disappears; stop reason label reads "Skip interrupted" for ~3 seconds. Console logs `[SkipManager] Skip stopped early: Interrupt`.
  - ✅ Debug interrupt stops the skip; reason surfaced.

- [x] **0.1.4c — Debug interrupt (MPPM two-client)**
  - Same test with both clients. Verify both clients drop to real time at the same IG moment and both see "Skip interrupted".
  - ✅ Interrupt halts globally; both clients identical.

---

## Build 0.1.5 — Buff system + checkpoints

- [x] **Dreamer prefab — add DreamerBuffSync**
  - Select the **DreamerPrefab** in the Project window.
  - Add component: **DreamerBuffSync** (leave _Sync Interval_ = 1, default).
  - *(Same prefab as DreamerNeedsSync / DreamerTaskSync — same GameObject, same pattern.)*

- [x] **0.1.5a — Test buff (buff system smoke test, single client)**
  - Enter Action as host only.
  - Note current Hunger drain: ~0.139 per IG minute at baseline.
  - In the dev panel, click **Test Buff** (slot 0). Verify: "── Buffs ──" section appears above the needs panel showing "Test: ½ Drain: 60 min".
  - Watch for 1–2 IG minutes: Hunger drain should visibly slow to ~0.07 per minute (half rate).
  - Let the buff tick down (or skip 2h). After expiry: buff label disappears, drain returns to baseline.
  - **Save / Load** with the buff active: buff count and remaining time survive the round-trip.
  - **Dev Checkpoint / Revert** with buff active: buff state is preserved in the checkpoint.
  - ✅ Buff shows, counts down, expires on schedule, modifies drain, survives save/load.

- [x] **0.1.5a — Test buff (MPPM two-client)**
  - Same test. Verify both clients see the buff panel updating in sync.

- [x] **0.1.5b — First Dream (two-client)**
  - Start a **new game** (not Load) with two clients.
  - Immediately after entering Action: check console for `[GameFlowManager] First Dream: shared checkpoint banked`.
  - Both dreamers should show a "Protection: 1920 min" buff in the panel.
  - Open pause menu → **Save** then **Load Save** → protection buff survives with correct remaining time.
  - Open pause menu → **Checkpoint** (banks "dev") → play a few minutes → **Revert** → world reverts to dev-checkpoint state.
  - ✅ First Dream checkpoint banked; both dreamers show shared protection buff counting down; buff survives save/load.

- [x] **0.1.5c — Save consumable (two-client)**
  - In the dev panel on **dreamer A (slot 0)**, click **Bank Save**.
  - Verify: dreamer A gains a second "Protection" buff (personal, 960 min). Dreamer B still has only the First Dream buff.
  - Console should log `[GameFlowManager] Save consumable: slot 0 banked checkpoint 'personal_0_...'`.
  - **Validity check:** dreamer A has two valid checkpoint IDs (first_dream + personal); dreamer B has one (first_dream only). *(Manually inspect via console logs / `ValidCheckpoints()` call.)*
  - Save, Load → both buffs survive on A; B unchanged.
  - ✅ Personal checkpoint banked; A has extra buff, B does not; save/load round-trips.

- [x] **0.1.5 — Checkpoint cleanup**
  - Apply a test buff (60 min). Skip 2h. After skip: test buff expired, no outstanding test buff checkpoint (protection buffs remain). Verify the `checkpoint_first_dream` directory still exists (protection buff still live).
  - Let or skip the First Dream protection buff to expire (1920 IG minutes ≈ 192 real minutes at 6s/min, so use Skip 32h). After expiry: `checkpoint_first_dream/` should be deleted from disk.
  - *(This test is long — use multiple skips or defer; the key check is that deletion happens when ALL referencing buffs expire.)*

---

## Build 0.1.6 — Sleep → checkpoint

### Inspector tweak (a2 — min-bank threshold)
- Select **WorldClockDriver** in the Action scene hierarchy.
- In the Inspector, expand **Buff Config** → verify **Sleep Min Bank Threshold Minutes** = 60 (default). Adjust if desired.

### 0.1.6a acceptance — single client

**Solo sleep banks a personal checkpoint:**
1. Assign dreamer 0 to **Sleep** (`DreamerTaskSync` dev panel → **Sleep** button).
2. Skip exactly 8 IG hours (480 min). Skip should complete normally (no guard triggered if needs are healthy).
3. Console: `[GameFlowManager] Sleep checkpoint banked: 'sleep_XXX' (Personal), 1 dreamer(s).`
4. Dreamer 0 gains a second "Protection" buff (Personal, ~1920 min full-duration since slept full 8h). Dreamer 1 unchanged.
5. Dev Checkpoint (F1) → Revert (F2) → state restores; sleep protection buff still present on dreamer 0.
6. ✅ Solo sleep: personal checkpoint banked, personal buff on waker only, survives revert.

**Nap banks nothing:**
1. Assign dreamer 0 to **Rest** (30-min rest task).
2. Skip 30 min. Console: no "Sleep checkpoint banked" message.
3. ✅ Nap/rest: no checkpoint, no sleep buff.

**Sub-threshold sleep banks nothing:**
1. Assign dreamer 0 to **Sleep** but skip only 30 min (a guard will interrupt, or assign a 30-min sleep duration directly).
2. After stop: if slept < 60 min → console logs "below min-bank threshold — no checkpoint."
3. ✅ Sub-threshold: no buff granted.

### 0.1.6a acceptance — MPPM two-client (together-sleep → shared)

1. Assign **both** dreamers to **Sleep**.
2. Skip 8h. Both sleep tasks complete on the same tick.
3. Console: `[GameFlowManager] Sleep checkpoint banked: 'sleep_XXX' (Shared), 2 dreamer(s).`
4. **Both** dreamers gain a "Protection [Shared]" buff.
5. Save → Load: both buffs survive. Dev revert to the sleep checkpoint restores that state.
6. ✅ Together-sleep: one shared checkpoint, a buff each, both clients show it.

### 0.1.6b acceptance — staggered wakes (two-client)

1. Assign dreamer 0 to Sleep (full 8h). Assign dreamer 1 to Sleep then immediately change to another task mid-sleep (or assign dreamer 1 to Rest only). Goal: dreamer 0 wakes later than dreamer 1 (or dreamer 1 never sleeps).
2. Wait / skip so dreamer 1's task completes at one tick (personal: 1 dreamer) and dreamer 0 completes at a later tick (personal: 1 dreamer).
3. Two separate `sleep_XXX` checkpoint log messages, different IG-minute IDs.
4. Each dreamer has their own protection buff; neither dreamer was disturbed during the other's wake.
5. ✅ Staggered: two independent personal checkpoints, sleeper left untouched.

### 0.1.6b acceptance — cut-short integration (two-client)

1. Assign **both** dreamers to **Sleep**. Start a skip long enough that a need will hit 30% guard before 8h.
2. Guard fires → skip stops → both dreamers are force-woken.
3. Console: `[GameFlowManager] Sleep checkpoint banked: 'sleep_XXX' (Shared), 2 dreamer(s).` (if both were sleeping)
4. Both dreamers receive a "Protection [Shared]" buff scaled to their actual slept minutes (e.g. slept 3h out of 8 → ~720 min out of 1920 → ~720 min buff).
5. ✅ Cut-short-both: shared partial checkpoint banked, buffs scaled to actual sleep.

---

## Build 0.1.7 — Dream flow (down, Rescue, Wake Up, Nightmares, Game Over)

### Scene wiring

- [x] **a1 / b1 / c — DreamFlowManager NetworkObject**
  - In the **Action** scene, create an empty GameObject named `DreamFlowManager`.
  - Add components: **NetworkObject** and **DreamFlowManager**.
  - *(In-scene NetworkObject — same pattern as WorldClockSync / SkipManager. No prefab registration needed.)*
  - *Why: DreamFlowManager is a NetworkBehaviour; its NetworkVariables (phase, downSlot, rescue timer) need NGO replication so clients see the rescue countdown and vote state.*

- [x] **b1 — ConsensusVoteComponent NetworkObject**
  - In the **Action** scene, create an empty GameObject named `ConsensusVoteComponent`.
  - Add components: **NetworkObject** and **ConsensusVoteComponent**.
  - On the **DreamFlowManager** Inspector, drag the `ConsensusVoteComponent` GameObject into the **Vote Component** slot.
  - *Why: ConsensusVoteComponent is a separate NetworkBehaviour; the vote options and picks need to replicate to both clients. DreamFlowManager holds a [SerializeField] reference to it.*

- [x] **c1 — Author Nightmare buff defs (optional override)**
  - The nightmare defs fall back to `DefaultNightmareTier1` / `DefaultNightmareTier2` in `BuffConfig` automatically (no Inspector authoring needed for default values).
  - If you want to override durations or energy-penalty multipliers: select **WorldClockDriver** in the Action scene → expand **Buff Config** → add entries to the **Defs** list with id `nightmare_tier1` / `nightmare_tier2` and custom modifier values.
  - *Why: The fallback defs have tier-1 = 0.5× energy recovery / 24h, tier-2 = 0.25× / 48h. Authoring overrides here is optional.*

- [x] **c1 — Author rescue config (optional)**
  - Select **WorldClockDriver** → expand **Buff Config** to verify or adjust: **Rescue Window Seconds** = 120, **Rescue Range Units** = 3, **Rescue Vitality Restore** = 30.
  - These can be left at defaults for first playtest.

---

### 0.1.7a acceptance — Down → Rescue (single client first, then MPPM)

**Single client — rescue window opens on incapacitation:**
1. Full flow into Action. Ensure dreamer 0 has a valid checkpoint (First Dream buff is enough).
2. Use **Set Critical** in the dev panel to drop all needs to critical, then let 2–3 ticks pass until Vitality reaches 0 → dreamer 0 goes down.
3. Verify: "Dreamer 0 is DOWN — rescue in 120s" overlay appears at top of screen. Console logs `[DreamFlowManager] Dreamer 0 is down. Rescue window: 120s`.
4. The rescue countdown ticks down in real time.
5. Skip is blocked: try **Escape** → **Skip 1h** → console should log `[SkipManager] Skip blocked — a dreamer is currently down`. Skip does not run.

- [x] ✅ Down triggers rescue window; countdown visible; skip blocked.

**Single client — successful rescue via debug button:**
1. Down dreamer 0 as above (rescue window open).
2. Click the **DEBUG: Rescue** button that appears in the rescue overlay.
3. Verify: overlay disappears; dreamer 0's Vitality restores to ~30; afflictions clear; console logs `[DreamFlowManager] Rescue succeeded for dreamer 0. No revert, no Nightmare.`.
4. No Nightmare buff on dreamer 0; clock continues from where it stopped.

- [x] ✅ Rescue: dreamer revived in place; no revert; no Nightmare applied.

**MPPM two-client — rescue window expiry → Wake Up vote:**
1. Down dreamer 0 (2-client run, dreamer 0 owned by host, dreamer 1 owned by virtual client).
2. Do NOT click the rescue button. Wait for the window to expire (120 real seconds, or reduce **Rescue Window Seconds** in Inspector to 5 for the test).
3. Verify: rescue overlay disappears; world clock pauses; "— Wake Up —" vote panel appears on both clients.
4. The vote panel shows the dead dreamer's valid checkpoints as buttons (e.g. "Shared — 0.5h ago").

- [x] ✅ Window expiry → world paused → Wake Up vote panel visible on both clients.

---

### 0.1.7b acceptance — Wake Up vote + revert (MPPM two-client)

**Consensus required:**
1. With the vote open, have only ONE client click a checkpoint option. Verify the option button turns yellow for the client who picked it and the status line shows their pick (e.g. "Dreamer 0: #0 | Dreamer 1: –"). Consensus must NOT fire yet.
2. Have the SECOND client click the same option. Verify the button turns green, consensus fires immediately: vote panel disappears; world reverts to the chosen checkpoint; clock resumes.
3. After revert: both dreamers are at the checkpoint positions (client-owned dreamer snapped via authority override); clock is back to the checkpoint time; the formerly-downed dreamer now has a Nightmare (I) buff.

- [x] ✅ Both clients must agree; mismatch keeps vote open; consensus triggers revert + Nightmare.

**Revert is world-wide:**
1. After the Wake Up revert, verify that dreamer 1 (the live partner) was also reverted — their needs/position match the checkpoint, not the post-down values.

- [x] ✅ Both dreamers reverted; clock reverted; both clients see the same result.

**Solo selection (no vote partner):**
1. With only 1 client (host), down dreamer 0. Let the rescue window expire.
2. Verify: the selection panel appears showing the available checkpoints — it does NOT auto-select.
3. Click one of the checkpoint options. Revert happens immediately (no partner pick needed).

- [x] ✅ Solo: panel appears and waits for the player's pick; clicking any option immediately triggers revert.

---

### 0.1.7c acceptance — Nightmares + Game Over (two-client)

**Tier-1 Nightmare after Wake Up:**
1. After a Wake Up revert, inspect dreamer 0's buff panel: a "Nightmare (I): 1440 min" buff should appear.
2. Assign dreamer 0 to **Sleep** → skip 8h → energy restore should be ~40 (80 × 0.5 = 40, half the normal 80) because the nightmare's 0.5× EnergyRecoveryMultiplier is applied.

- [x] ✅ Nightmare (I) applied; energy penalty visible in sleep recovery.

**Tier-2 escalation:**
1. After the first Wake Up, let the clock run so the tier-1 nightmare is still active.
2. Down dreamer 0 again (let rescue window expire, vote, revert).
3. Verify: dreamer 0's buff panel now shows "Nightmare (II): 2880 min" (tier escalated from 1 → 2, previous nightmare removed).
4. Sleep 8h again → energy restore should be ~20 (80 × 0.25 = 20).

- [x] ✅ Escalation: second death escalates to tier 2; tier-1 nightmare removed.

**Game Over — no valid checkpoints:**
1. Start a **New Game** and immediately let the First Dream buff expire or force an incapacitation before any checkpoint is banked.
   - Easiest: reduce **First Dream Duration Minutes** on WorldClockDriver → Buff Config to a very small value (e.g. 1 min), skip until it expires, then down dreamer 0.
   - Alternatively, use dev tooling to remove all protection buffs manually.
2. Let the rescue window expire. The vote panel should NOT appear.
3. Console: `[DreamFlowManager] Dreamer 0 has no valid checkpoints — Game Over.`
4. A "GAME OVER" overlay appears on both clients.
5. Check disk: `persistentDataPath/Saves/slot_0/world.json` → `"isGameOver": true`.

- [x] ✅ No valid checkpoints → Game Over overlay; save locked (isGameOver = true on disk).

**Save/revert round-trip:**
1. After a successful Wake Up revert, open pause menu → **Save** → verify `world.json` and both `dreamer_X.json` files are correct (Vitality/needs match checkpoint, Nightmare buff present in dreamer file).
2. Quit → relaunch → **Load Game** → world resumes from the post-revert state with Nightmare buff intact.

---

## Build 0.1.7d — Revert reconciliation, checkpoint lifecycle & nightmare persistence (fix pass)

No scene wiring required — all tasks are code changes. One manual verification step (a2) and six acceptance scenarios below.

---

### 0.1.7d — a2 manual verification

**a2 — Buff restored at full duration after revert:**
1. Start a New Game → Action scene loads. Press F5 (dev checkpoint). Wait a few in-game minutes (skip 30 min) so some time has passed since First Dream.
2. Press R (dev revert) to warm-apply the dev checkpoint.
3. Open the buff debug panel (or inspect the dreamer record in the console). Verify the protection buff's `remainingMinutes` equals the FULL duration as captured at checkpoint time — not an inflated "current time + original duration" or a deflated "original remaining − elapsed".

- [x] ✅ After revert the protection buff reads the as-of-checkpoint remaining minutes, not current-time-relative.

---

### 0.1.7d acceptance

**Eg 1 — First Dream revert (two-client):**
1. Start a New Game. Both dreamers spawn; First Dream checkpoint is banked with both dreamers holding a 1000 min protection buff.
2. Skip a few in-game hours so the clock advances.
3. Down dreamer 0 (reduce Vitality via debug), let rescue window expire, vote to revert to First Dream.
4. After revert: inspect dreamer 0 and dreamer 1.

- [x] ✅ Both dreamers are back at time-0 positions. Dreamer 0 carries "First Dream" protection buff at 1000 min AND a tier-1 Nightmare buff. Dreamer 1 carries only the 1000 min protection buff (no nightmare). First Dream checkpoint file still exists on disk.

**Eg 2 — Two personal checkpoints; revert discards newer one (two-client):**
1. Start a New Game. Press the debug save-consumable key for dreamer 0 → creates `personal_0_...` (slot 1). Advance the clock ~30 min. Press debug save-consumable again → creates a second `personal_0_...` (slot 2).
2. Down dreamer 0, let rescue window expire, vote to revert to **slot 1** (the older one).
3. After revert: check the Saves directory in Explorer (`%AppData%/../LocalLow/…/Saves/`).

- [x] ✅ Slot 2's checkpoint directory is deleted (pruned by the revert-timeline rule — newer than the target). Slot 1 exists. Dreamer 1 is fully rewound (no consumable buff, needs at slot-1 values). Dreamer 0 has the protection buff from slot 1 at as-of-checkpoint duration plus a tier-1 nightmare.

**Escalation — repeated death at the same checkpoint:**
1. Down dreamer 0 → revert to slot 1 → tier-1 nightmare applied.
2. Without advancing past the checkpoint, down dreamer 0 again (clock is rewound so force another incapacitation via debug) → rescue window expires → vote to revert to the same slot 1.
3. Inspect dreamer 0's buffs after the second revert.

- [x] ✅ Dreamer 0 has exactly ONE nightmare buff, now at tier 2 (2880 min). Tier-1 nightmare is gone. Slot 1's checkpoint file on disk contains the tier-2 nightmare baked in.

**Separate dreams — two independent nightmares:**
1. Down dreamer 0 → revert to slot 1 → tier-1 nightmare (origin = slot 1's id) is applied.
2. Clock resumes. Bank slot 2 (use debug save-consumable). Down dreamer 0 again → revert to slot 2.
3. Inspect dreamer 0's buffs.

- [x] ✅ Dreamer 0 carries TWO independent nightmare buffs: one with `checkpointId = slot1Id` (tier 1, timer as-of-slot-2-capture) and one with `checkpointId = slot2Id` (tier 1, full new duration). Neither cancels the other.

**Anti-cheese — menu-load preserves nightmare tier:**
1. After a revert that gave dreamer 0 a tier-1 nightmare, note the `remainingMinutes` on disk.
2. Quit to desktop. Relaunch → **Load Game** from the main menu.
3. Inspect dreamer 0's nightmare buff after loading.

- [x] ✅ Tier-1 nightmare is present at the same `remainingMinutes` as on disk. Tier was NOT incremented by the load. No re-save occurs on a plain menu load.

**Retention bug — checkpoint survives buff expiry and remains revertable:**
1. Bank slot 1 (noon, protection buff expiry at 8 PM in-game). Bank slot 2 (6 PM, protection buff expiry at 10 PM).
2. Skip time to 8:01 PM. Verify: slot 1's protection buff has expired (not on dreamer 0's buff list). Check disk — **slot 1's checkpoint directory still exists** (c1: file no longer deleted on expiry).
3. Skip to 9 PM. Down dreamer 0. Let rescue window expire. Verify: only slot 2 is offered in the vote (slot 1's buff is gone — not on the live buff list — so it is NOT offered). Revert to slot 2.
4. After revert the clock is back at 6 PM. Inspect dreamer 0's protection buff — the slot-1 buff should be present again (restored from slot 2's snapshot). Verify slot 1 is now offered if you down dreamer 0 again.

- [x] ✅ Slot 1's file was never deleted when its buff expired (c1 fixed). After revert to 6 PM the buff is restored and slot 1 is once again valid/offered.

- [x] ✅ Nightmare and post-revert state survive save/load.

---

## Build 0.2.0 — Item model + inventory (foundation)

### Scene wiring

- [x] **a1 — Author test ItemDef SO assets**
  - In the Project window: *Assets > Create > Coremenders > Item Def* (twice — one for each test item).
  - Set on the first: **Def Id** = `1`, **Display Name** = `Stone`, **Category** = `Resource`, **Weight** = `0.5`, **Stackable** = ✅, **Max Stack** = `99`.
  - Set on the second: **Def Id** = `2`, **Display Name** = `Wood`, **Category** = `Resource`, **Weight** = `1.0`, **Stackable** = ✅, **Max Stack** = `64`.
  - Save assets to `Assets/Game/Data/Items/` (create the folder if needed).
  - *Why: the dev-add buttons hardcode defId 1 and 2; these are the matching defs. Ids must be stable — never reorder.*

- [x] **a2 — Wire ItemDefRegistry in the Action scene**
  - Create empty GameObject `ItemDefRegistry` in the **Action** scene.
  - Attach `ItemDefRegistry` component.
  - In the Inspector, expand **Defs** → **+** twice → drag each ItemDef SO into the slots.
  - *Why: the registry builds its defId→SO lookup at Awake; without it, inventory names display as "def#1" instead of "Stone".*

- [x] **b2 / a5 — Add DreamerInventorySync to DreamerPrefab**
  - Open **DreamerPrefab** in the Prefab editor.
  - Add Component → **DreamerInventorySync**. Leave **Sync Interval** = 1 (default).
  - *Why: DreamerInventorySync is the per-dreamer NetworkBehaviour that replicates inventory to the owning client. Must be on the prefab alongside DreamerNetworkAdapter.*

---

### 0.2.0a acceptance — Data model + def registry

1. Full flow into Action (New Game).
2. On the owning client's screen, below the buff panel and inventory header, verify the "── Inventory ──" label and "(empty)" appear.
3. Console: `[ItemDefRegistry] Built with 2 def(s).` logged at startup.

- [x] ✅ Registry built; inventory section visible (empty) on owning client.

---

### 0.2.0b acceptance — Owned-container replica (sync)

1. On the owning client, click **Add Item(1)** (dev button below the inventory section). Console: `[DreamerInventorySync] Dev: added defId=1 qty=1 to slot 0.`
2. The inventory section updates to show `Stone ×1`. Click again → `Stone ×2`. Click **Add Item(2)** → `Stone ×2`, `Wood ×1`.
3. Verify the **other** client (dreamer 1 owner) does NOT see dreamer 0's inventory in their panel — the owning client only sees their own inventory.

- [x] ✅ Debug-add appears in the owning client's replica; contents match on host and owning client.

---

### 0.2.0c acceptance — Save + revert

**Save/load round-trip:**
1. Debug-add a few items to dreamer 0. Host presses **Escape** → **Save**.
2. Open `persistentDataPath/Saves/slot_0/dreamer_0.json` → verify an `"inventory"` key with `"items"` array is present and contains the added items with correct `defId` and `quantity`.
3. Add more items, then **Load Save** (warm). Inventory reverts to the saved count on both clients.
4. Quit → relaunch → **Load Game** (cold) → dreamer 0 spawns with the saved inventory.

- [x] ✅ Inventory persists in the dreamer file; warm and cold load restore it correctly.

**Checkpoint / revert:**
1. Debug-add items → take **Checkpoint** (F5 / pause menu dev button).
2. Debug-add more items.
3. Press **Revert** (R / dev button). Inventory reverts to the as-of-checkpoint contents; nothing post-checkpoint survives.

- [x] ✅ Inventory reverts cleanly to its checkpoint state; clear-and-rehydrate confirmed.

**Schema check:**
- Edit any existing `slot_0/world.json` to set `"schemaVersion": 7` → trigger Load → console logs a clear version mismatch error (schema is now 8).

- [x] ✅ Old saves rejected with a clear schema-version error.

**(MPPM two-client)** — repeat the save/load and revert tests with two clients running. Verify that dreamer 1's inventory also round-trips correctly, and that each client sees only their own inventory in the HUD.

- [x] ✅ Two-client: each dreamer's inventory replicates to its owner only; both save/load and revert cleanly.

---

## Build 0.2.1 — World items + pickup + drop

### Scene wiring

- [x] **a3/a5 — Create GroundItemVisual prefab**
  - In the Project window: *GameObject > 3D Object > Sphere*; rename to `GroundItemVisual`; scale to (0.3, 0.3, 0.3).
  - Drag into `Assets/Game/Prefabs/` to create the prefab; delete the scene instance.
  - *Why: WorldItemsSync instantiates this prefab client-side for each ground item. No NetworkObject component — these are local visuals only.*

- [x] **a3 — Wire WorldItemsSync in the Action scene**
  - Create empty GameObject `WorldItemsSync` in the **Action** scene.
  - Add Component → **NetworkObject** (required for NetworkBehaviour).
  - Add Component → **WorldItemsSync**.
  - In the Inspector, drag the `GroundItemVisual` prefab into the **Ground Item Prefab** slot. Leave **Sync Interval** = 1.
  - Do **not** add it to the NetworkManager Network Prefabs list — it is an in-scene NetworkObject, not a spawned prefab. NGO tracks in-scene NetworkObjects automatically on scene load (same pattern as `CharacterCreationController`).
  - *Why: WorldItemsSync is the singleton NetworkBehaviour that replicates the ground-item collection to all clients and manages local visuals.*

---

### 0.2.1a acceptance — World items (state, visuals, save)

**Single client:**

1. Start host, enter Action scene.
2. On the host's screen, click **Place(1) @ Dreamer0** (top-center dev button). Console: `[WorldItemsSync] Placed defId=1 qty=1 at ...`
3. A GroundItemVisual sphere spawns near dreamer 0 in the scene view.
4. **Save** (pause menu). Open `slot_0/world.json` → verify `"worldItems"` array contains one entry with `defId`, `quantity`, `position`, and `id`. Verify `"nextWorldItemId": 2`.
5. Place another item, then **Load Save** (warm). The second item disappears; only the original item remains — confirming revert to saved state.

- [x] ✅ Ground item shows in the scene; persists in world.json; load restores the as-saved state.

**Checkpoint / revert:**

1. Place an item → take **Checkpoint**. Place a second item.
2. Press **Revert**. Second item disappears; first item remains.

- [x] ✅ Revert restores world items to the as-of-checkpoint collection.

**(MPPM two-client):**

1. Enable Virtual Player, press Play. Host clicks **Place(1) @ Dreamer0**.
2. Both the host window and the virtual-player window show the GroundItemVisual sphere in the same position.

- [x] ✅ Ground item synced to both clients; visual appears on both.

---

### 0.2.1b acceptance — Pickup (intent → host, race-safe)

**Single client:**

1. Start host, place a ground item. Walk dreamer 0 within 3 m of the item.
2. The right-side panel shows **"── Ground (nearby) ──"** with a **Pick up Stone×1** button.
3. Click **Pick up Stone×1**. Console: `[DreamerInventorySync] Picked up item ... for dreamer 0.`
4. The ground visual despawns. The inventory left panel gains `Stone ×1`.

- [x] ✅ Pickup moves the item from world to inventory; visual despawns.

**Race safety:**

1. Place one item. In MPPM, ensure both dreamers are within 3 m of it.
2. Both owning clients simultaneously click **Pick up** on the item.
3. Exactly one dreamer's inventory gains the item. The other's console logs `Pickup race: item ... already gone`.

- [x] ✅ Concurrent pickups yield exactly one owner; no duplication. - will be verified with multiple players, can't do it alone

**(MPPM two-client) full sync:**

1. Dreamer 0 (host) places an item. Both clients see the visual.
2. Dreamer 1 (client) picks up the item. The visual despawns on both clients. Dreamer 1's inventory shows the item; dreamer 0's inventory is unchanged.

- [x] ✅ Pickup syncs to both clients (world visual gone, picker's inventory updated).

---

### 0.2.1c acceptance — Drop

**Single client:**

1. Debug-add an item to dreamer 0's inventory (`Add Item(1)` dev button).
2. Click **Drop** next to the inventory slot. Console: `[DreamerInventorySync] Dropped def=1 from dreamer 0 at ...`
3. The inventory is now empty. A GroundItemVisual sphere spawns at dreamer 0's position.

- [x] ✅ Drop moves item from inventory to world; visual appears.

**Round-trip (pickup ↔ drop):**

1. Place item → pick it up → drop it → pick it up again. Inventory count is correct at each step.
2. Save after a pickup → Revert → item is back on the ground (pre-pickup state).

- [x] ✅ Full pickup ↔ drop round-trip is correct and save/revert-clean.

**(MPPM two-client) drop sync:**

1. Dreamer 1 drops an item. Both clients see the ground visual appear at dreamer 1's position.

- [x] ✅ Drop syncs to both clients (inventory removed on owner, visual appears on both).

**Schema check:**

- Edit any existing `slot_0/world.json` to set `"schemaVersion": 8` → trigger Load → console logs a clear version mismatch error (schema is now 9).

- [x] ✅ v8 saves rejected with a clear schema-version error.

---

## Build 0.2.2 — Stacking + consume → needs

### Scene / asset wiring

- [x] **b1 — ItemDef consume-effect fields (existing defs)**
  - Open each existing `ItemDef` SO asset (in `Assets/Game/Data/Items/` or wherever they live).
  - For a food item (e.g. "Stone" is not food — this requires a food-type ItemDef to be created):
    - **consumeEffect** → `Food`
    - **hungerRestore** → e.g. `40`
    - **thirstRestore** → e.g. `5`
  - For a water/drink item:
    - **consumeEffect** → `Drink`
    - **thirstRestore** → e.g. `40`
    - **hungerRestore** → e.g. `0`
  - For the save-consumable item:
    - **consumeEffect** → `SaveConsumable`
    - hungerRestore / thirstRestore → `0` (not used)
  - *Why: ConsumeItemServerRpc reads these fields from the ItemDef SO. Without them set, the Use button stays greyed out.*

- [x] **b1 — Create food and drink ItemDef SOs if they don't exist yet**
  - *Assets → Create → Coremenders → Item Def*
  - Set unique stable `defId` values (e.g. 3 = "Berries", 4 = "Water Skin"); fill display name, category (Food/Water), stackable = true, maxStack = e.g. 20.
  - Wire them into `ItemDefRegistry`'s **Defs** list in the Inspector.
  - *Why: The acceptance test requires food and drink items. Using def IDs 1 and 2 for Stone/Flint won't have consume effects; new defs needed.*

---

### 0.2.2a acceptance — Stacking + two-level quantity

**Single client:**

1. Start host. Click **Add Item(1)** twice in the inventory panel. The display should show `Stone ×2` in a single row (not two rows), confirming stacking merged the two adds.
2. Click **Split** on the Stone ×2 row. The display should split into `Stone ×1` and `Stone ×1` on two separate rows.
3. Drop one stack; pick up the other. On pickup the two stacks in inventory merge back into `Stone ×2`.

- [x] ✅ Same-def stackable items merge on add; Split divides a stack in two; pickup re-merges.

**Non-stackable:**

1. If a non-stackable ItemDef exists (stackable = false), add it twice. Should appear as two separate rows of ×1, not merged.

- [x] ✅ Non-stackable items create separate qty-1 stacks.

**Two-level quantity:**

1. A stack of ×3 food items — display shows `3` (integer floor). The logical value in the RDM is `3.0` (float). Consume one → display shows `2`.

- [x] ✅ Display floors the logical quantity; arithmetic is precise.

**Save / revert:**

1. Merge a stack. Save → add items → Revert → merged state is restored.

- [x] ✅ Stacking state round-trips through save and revert.

**(MPPM two-client):**

1. Host adds a stackable item (merges into existing stack). Virtual player's inventory for same dreamer shows the merged count.

- [x] ✅ Stacked inventory syncs to both clients.

---

### 0.2.2b acceptance — Consume → needs

**Single client:**

1. Start host. Open needs panel (Task panel shows hunger/thirst values).
2. Set needs to critical via **Set Critical** dev button. Hunger and thirst drop to 5.
3. Add a food ItemDef item (e.g. Berries) to dreamer 0's inventory via **Add Item(N)** where N is the food defId.
4. The inventory row shows a greyed-out **Use** button for non-consumables and an active **Use** button for the food item.
5. Click **Use** on the Berries item. Console: `[DreamerInventorySync] Consumed def=3 effect=Food slot 0.`
6. Inventory decrements (or removes if qty reaches 0). Hunger increases by `hungerRestore` (e.g. +40 → from 5 to 45). Thirst increases by `thirstRestore` (e.g. +5 → from 5 to 10).
7. Repeat with a drink item. Thirst increases by `thirstRestore`.

- [x] ✅ Consuming food refills hunger; consuming drink refills thirst; item decrements.

**Item decrements to zero:**

1. Add one unit of food. Consume it. Inventory is now empty (`(empty)` shown). No ghost row lingers.

- [x] ✅ Consuming the last unit removes the stack entirely; inventory shows empty.

**Save / revert:**

1. Consume food → save → consume more → revert. Needs and inventory revert to the as-of-save state.

- [x] ✅ Consume state is save/revert-clean.

**(MPPM two-client):**

1. Dreamer 1 (virtual client) consumes food. Needs panel for dreamer 1 updates on both host and virtual-player windows.

- [x] ✅ Consume syncs needs to both clients.

---

### 0.2.2c acceptance — Save-consumable item wrapper

**Single client:**

1. Author a `SaveConsumable` ItemDef (consumeEffect = SaveConsumable). Add one to dreamer 0's inventory.
2. Click **Use** on it. Console: `[DreamerInventorySync] Consumed def=X effect=SaveConsumable slot 0.` and `[GameFlowManager] Save consumable: slot 0 banked checkpoint '...'`.
3. The item is removed from inventory. Dreamer 0 now has a personal protection buff (visible in buff panel).
4. The checkpoint is valid: use **Revert** (pause menu) and the game reverts to the post-consume state (buff present, item gone).

- [x] ✅ Consuming a SaveConsumable item banks a valid personal checkpoint and removes the item.

**(MPPM two-client):**

1. Dreamer 1 (virtual client) consumes a save-consumable. Buff panel on both windows shows the protection buff for dreamer 1.

- [x] ✅ Personal checkpoint and buff sync to both clients after item consume.

---

## Build 0.2.3 — Action framework (time-cost actions)

### Scene / Inspector wiring

- [x] **0.2.3a1 — NeedsConfig: new pool/exhaustion fields on WorldClockDriver**
  - Select **WorldClockDriver** in the Action scene hierarchy.
  - In the Inspector, expand **Needs Config** → verify (or set):
    - **Time Pool Max Minutes** = `1440` (24 × 60 — one full day)
    - **Exhaustion Duration Minutes** = `1440` (24h exhaustion debuff if pool goes negative)
  - *Why: DreamerRecord.timePool initialises to 1440 from the template; these two fields cap + control the pool.*

- [x] **0.2.3b — DreamerInventorySync: NeedsConfig reference**
  - Open **DreamerPrefab** in the Prefab editor.
  - Select the **DreamerInventorySync** component.
  - In the Inspector, expand **Needs Config** → verify the values match the WorldClockDriver's config (or drag the same NeedsConfig asset if you split it into an SO — currently it is an inline `[SerializeField]`, so set values manually):
    - **Exhaustion Duration Minutes** = `1440`
  - *Why: DreamerInventorySync.ApplyExhaustionDebuff uses `_needsConfig.exhaustionDurationMinutes`. The host applies this when a consume overdrafts the pool.*

- [x] **0.2.3b — WorldClockDriver: MovementConfig**
  - Select **WorldClockDriver** in the Action scene hierarchy.
  - In the Inspector, expand **Movement Config** → set:
    - **Time Per Meter** = `0` (inert by default — tune up when you want movement to cost pool)
    - **Energy Per Meter** = `0` (same — inert until tuned)
  - *Why: Both rates default to 0 so movement drain is a no-op until you decide to tune it. Setting non-zero values enables pool drain per metre walked.*

- [x] **0.2.3b — ItemDef SO: action fields for food/drink/save-consumable items**
  - Open each existing `ItemDef` SO asset (in `Assets/Game/Data/Items/`).
  - For each food/drink item (e.g. "Berries", "Water Skin"):
    - **Time Cost** = `10` (10 IG minutes to eat — adjust to taste)
    - **Energy Cost** = `0`
    - **Allows Overdraft** = ☐ (unchecked — eating blocks if pool is exhausted)
    - **Payout Shape** = `Gradual`
    - **Interrupts Skip** = ☐ (inert for now)
  - For the save-consumable item:
    - **Time Cost** = `10` (10 IG minutes to use the save item)
    - **Energy Cost** = `0`
    - **Allows Overdraft** = ☐
    - **Payout Shape** = `EndEffect` *(must be EndEffect — end-payout fires the dream save)*
    - **Interrupts Skip** = ☐
  - *Why: timeCost > 0 triggers the timed action path in ConsumeItemServerRpc. Without it set, items fall back to the 0.2.2 instant path. PayoutShape = Gradual installs the nourishment buff; EndEffect defers the dream-save to completion.*

- [x] **0.2.3b/a2 — New buff defs (code-defined, Inspector override optional)**
  - `BuffDef` is a plain `[Serializable]` class, not a ScriptableObject. All three new defs (`nourishment_hunger`, `nourishment_thirst`, `exhaustion`) are now registered as static fallbacks in `BuffConfig.cs` — they work without any Inspector step.
  - **Optional tuning:** select **WorldClockDriver** in the Action scene → expand **Buff Config → Defs** → add entries if you want to override the defaults:
    - `nourishment_hunger` — override `HungerRestoreRate` value (default: 1 per min placeholder; `magnitudeOverride` on the instance will always take precedence at runtime)
    - `nourishment_thirst` — same for `ThirstRestoreRate`
    - `exhaustion` — override the drain multipliers or duration (default: +50% hunger/thirst drain for 1440 min)
  - *Why: `GetDef()` checks the Inspector `defs` list first, then falls back to the static defaults. No Editor step is required for the buffs to be functional.*

---

### 0.2.3a acceptance — Day pool (single client first)

**Pool initialises and decrements on consume:**
1. Full flow into Action (New Game). Verify the "── Actions (pool:Xm) ──" section shows `pool:1440m` (full 24-hour pool).
2. Debug-add a food item. Click **Use**. The action appears in the action panel as `[act] Berries` with a cancel button. Pool decrements by `timeCost` (e.g. `pool:1430m` if timeCost = 10).

- [x] ✅ Pool initialises to 1440; consuming a timed item debits the pool at queue time.

**Action completes and nourishment delivers over the window:**
3. Wait for the action to complete (timeCost IG minutes = timeCost × 6 real seconds at default speed).
4. Hunger should have risen gradually over the window (Gradual payout). Action panel returns to `(none)`.
5. Final hunger value should equal the food's `hungerRestore`.

- [x] ✅ Gradual payout delivers the correct total over the action window; action clears on completion.

**Midnight pool reset (Stage 10):**
6. Skip to IG day boundary (skip enough minutes that `totalInGameMinutes` crosses a 1440-multiple).
7. Pool resets to `1440m` (any partially-used pool is replenished at midnight).
8. If the "exhaustion" buff was active, it is removed at the same moment.

- [x] ✅ Pool resets to full at midnight; exhaustion clears.

**Pool gate (no overdraft):**
9. Manually set pool to near zero (debug skip until pool is low, or add many consume actions until pool is nearly exhausted).
10. Attempt to consume a food item whose `timeCost` exceeds the remaining pool. The item should stay in inventory; console logs `Action blocked: pool=... < cost=...`.

- [x] ✅ Pool gate blocks a non-overdraft consume when pool is insufficient.

---

### 0.2.3b acceptance — Action channel + eating retrofit (single client)

**Queue multiple actions:**
1. Debug-add 2 food items. Click **Use** on both in quick succession.
2. Action panel shows `[act] Berries` + `+1 queued`. Pool debited by both timeCosts immediately.
3. After the active action completes, the queued one starts automatically.

- [x] ✅ Queue accepts multiple actions; auto-advance starts the next on completion.

**Nourishment buff rate (Gradual payout):**
4. While an action is active, open the buff panel: a "Nourishing (Hunger)" buff should be present counting down.
5. Hunger increases each tick by `hungerTotal / duration` per IG minute.
6. When the action completes the nourishment buff is removed.

- [x] ✅ Nourishment buff present during action; removed on completion; hunger increases gradually.

**Save-consumable end-payout (0.2.3d):**
7. Add a save-consumable item (timeCost > 0, payoutShape = EndEffect). Click **Use**.
8. Action appears in the panel. Wait for it to complete.
9. Console: `[WorldClockDriver] Action end-payout DreamSave for slot X.` A personal checkpoint is banked and a protection buff appears.
10. Verify no checkpoint was banked *before* the action completed (no premature payout).

- [x] ✅ Dream save fires at action completion, not at queue time.

---

### 0.2.3c acceptance — Cancel / refund (single client)

**Cancel queued action (full refund):**
1. Debug-add 2 food items. Use both → both queued. Click the ✕ button on the active action *while the second is still queued*.
2. Active action cancels. If the active action was gradual and had partially delivered nourishment, the nourishment buff is removed. Pool is refunded by the remaining (unelapsed) fraction.
3. The queued action now becomes active automatically (auto-advance).

- [x] ✅ Cancel active: partial pool refund based on elapsed time; nourishment buff removed; queue advances.

**Cancel active action (partial item returned):**
4. Use one food item (timeCost = 10 min). After 5 IG minutes (halfway), click ✕.
5. A partial food item appears in inventory (`remaining ≈ hungerRestore × 0.5`). Half the pool is refunded.
6. If you now click **Use** on the partial item, the action duration is proportionally shorter (5 min instead of 10).

- [x] ✅ Cancel mid-action: partial item spawned in inventory; pool refund proportional to unelapsed time; re-consuming partial delivers only the remaining payout.

**Cancel queued (full refund + item returned):**
7. Queue two food items. Press ✕ on the second queued item (index 1 in the queue if visible, or click cancel when it becomes the active slot).
   - Note: the current OnGUI only shows one cancel button for the active slot. To test a queued cancel via the RPC directly, you can test this once OnGUI exposes per-queued-item cancel buttons in a future build.

- [x] ✅ Queued cancel: full timeCost refund; original item (or partial if it was a partial) returned to inventory.

---

### 0.2.3e acceptance — Movement drain (single client, optional)

*(Only meaningful if `MovementConfig.timePerMeter > 0`. Leave both rates at 0 to skip this test.)*

1. Set **Time Per Meter** = `0.01` on WorldClockDriver → Movement Config.
2. Walk dreamer 0 a significant distance (100 m should cost 1 IG minute of pool).
3. Verify pool decrements as dreamer moves.
4. If pool goes negative, an "Exhausted" buff appears and pool reads a negative value.
5. Movement is never hard-blocked — dreamer can still walk even with a depleted pool.

- [x] ✅ Movement drains pool; overdraft triggers exhaustion buff; movement never hard-blocks.

---

### 0.2.3 acceptance — MPPM two-client

1. Enable Virtual Player. Press Play. Full flow → Action scene with both dreamers.
2. Dreamer 0 (host) queues a food action. Verify dreamer 0's action panel shows the active action and pool value.
3. Dreamer 1 (virtual client) queues a food action independently. Verify dreamer 1's panel on the virtual-player window updates.
4. Both actions complete independently; needs update only on the respective dreamer.
5. Save → Load: both dreamers' `timePool` and `actionQueue` fields are present in their dreamer JSON files and restored correctly.
6. Checkpoint → let actions advance → Revert: action queues reset to the as-of-checkpoint state (empty or partially advanced); pool values restored.

- [x] ✅ Two-client: each dreamer's action queue and pool are independent, synced to the owner, and save/revert-clean.

**Schema check:**
- Edit any existing `slot_0/world.json` to set `"schemaVersion": 9` → trigger Load → console logs a clear version mismatch error (schema is now 10).

- [x] ✅ v9 saves rejected with a clear schema-version error (schema is now 10).

---

## Build 0.2.4 — Spoilage (best-before)

- [x] **a1 — Perishable ItemDef fields**
  - Open an existing food `ItemDef` asset (e.g., the test meat/food SO used in 0.2.2).
  - In the **Spoilage (0.2.4)** header: check **Perishable** ✅, set **Effective Lifespan** to a short test value (e.g., `60` IG minutes so items spoil within one play session).
  - Optionally create a second perishable def with a different lifespan (e.g., berries at `30` IG min) to test bucket divergence.
  - *Why:* the condition triple is initialized from `effectiveLifespan` at item creation; without this set, all items are non-perishable (rate = 0).

- [x] **SpoilageConfig — WorldClockDriver**
  - Select the `WorldClockDriver` GameObject in the Action scene.
  - Under **Spoilage Config**: review default thresholds (`freshThreshold = 70`, `goodThreshold = 40`). Adjust if desired. These map condition values to Fresh / Good / Stale / Spoiled buckets.
  - *Why:* thresholds are configurable and exposed here alongside `NeedsConfig` and `MovementConfig`.

- [x] **Sickness buff tuning**
  - In `WorldClockDriver → Buff Config → Defs list`, optionally add a `BuffDef` with id `sickness` to override the default placeholder (2h, +50% thirst drain). Or leave the built-in fallback.
  - *Why:* the static `DefaultSickness` fallback ships with placeholder values; severity and duration should match GDD intent once balanced.

- [x] **Schema bump check**
  - Delete or rename any existing `slot_0/world.json` so New Game triggers a fresh load from the template.
  - The template `world.json` now carries `"schemaVersion": 11`. Any old saves (version 10) are rejected with a clear error in the console.

---

### 0.2.4 validation — single client

#### 0.2.4a — Condition triple + formula

1. Enter Play mode. Open the **Debug Add** panel (dev input) and add a perishable item (e.g., Test Meat) to dreamer 0's inventory.
2. Open the Inventory panel (`[I]`). Verify the item shows a freshness label like `[Fresh 100%]`.
3. Let the clock advance (wait ~6 real seconds per IG minute at default rate). Re-open inventory. Verify the percentage decreases.
4. Compute expected: `100 - (elapsed_IG_min / lifespan) * 100` should match the displayed value within 1%.

- [x] ✅ Condition decreases over time and matches the formula.

5. Save → Load. Re-open the inventory. Verify the condition value is restored to the as-of-save value (not re-initialized to 100%).

- [x] ✅ Condition triple round-trips through save/load.

6. Take a checkpoint, let more time pass, Revert. Verify condition reverts to the as-of-checkpoint value.

- [x] ✅ Condition reverts correctly to checkpoint state.

#### 0.2.4b — Re-stamp + rate-modifier seam

1. Add a perishable item to inventory. Note its current freshness %.
2. In the Inventory panel, the `DevReStampItemServerRpc` is not directly exposed in OnGUI — call it programmatically from the Inspector via a temporary trigger on a dev component, or hook it to a key in the action scene.
   - To test without a custom trigger: use the debug RPC from a test script calling `dreamerInventorySync.DevReStampItemServerRpc(0, 2f)` on the host (modifier=2 → double rate → spoils twice as fast).
3. After the re-stamp, note freshness drops faster than before.
4. Verify condition is continuous across the re-stamp (no jump at the re-stamp moment — condition at the instant of re-stamp matches the computed value just before).

- [x] ✅ Re-stamping changes the aging slope without a discontinuity in current condition.

#### 0.2.4c — Stacking by 5-minute time window + merge reconciliation

Merge rule: two stacks of the same def merge only when their conditions are within `rate × 5` of each other (≤ 5 IG minutes apart in age). The merged stack takes the fresher (higher) condition.

1. Add 1× perishable food (fresh, condition 100%). Let it age significantly — well past the 5-minute window (e.g., wait 10+ IG minutes with a 60-min lifespan item so condition drops ~17%).
2. Add another 1× fresh item (condition 100%) of the same def.
3. Verify the two stacks are **not merged** (they are more than 5 IG minutes apart in age).

- [x] ✅ Stacks more than 5 IG minutes apart do not merge.

4. Add two fresh items in quick succession (both near 100%, well within 5 IG minutes of each other). Verify they **do** merge into one stack.

- [x] ✅ Stacks within 5 IG minutes of each other merge.

5. Add a slightly-older item (condition ~90%) to a 100% fresh stack where the gap is ≤ 5 IG minutes. Verify the merged stack's condition is updated to the **fresher** value (100%), not the lower — skip enough time and confirm it spoils later than a pure-90% stack would.

- [x] ✅ Merge reconciles condition to the fresher (higher) value.

#### 0.2.4d — Spoiled handling + display

1. Let a perishable item age until it shows `[Spoiled 0%]` in the inventory.
2. Attempt to consume it. Verify:
   - No hunger/thirst is restored (watch the needs bars — no change after the consume).
   - The item is consumed (removed from inventory).
   - The dreamer gains the **Sick** debuff (visible in the buff panel on the left).

- [x] ✅ Eating spoiled food yields no nutrition and applies the Sick debuff.

3. Eat a fresh perishable item. Verify it delivers normal nutrition as before.

- [x] ✅ Fresh food still works normally.

---

### 0.2.4 acceptance — MPPM two-client

1. Enable Virtual Player. Press Play. Full flow → Action scene.
2. Host adds a perishable item to dreamer 0's inventory via the dev add panel.
3. On the **virtual client window**, verify the same item shows a freshness label and the percentage is identical to the host's display (both computed from the same synced clock).
4. Let time pass. Verify the freshness % decreases identically on both client windows.
5. Host player lets the item spoil. Consumes it. Sickness debuff visible in the buff panel on both clients.

- [x] ✅ Two-client: freshness displays identically on both clients; spoiled-eat applies sickness to both.

**Schema check:**
- Edit any existing `slot_0/world.json` to set `"schemaVersion": 10` → trigger Load → console logs a clear version mismatch error (schema is now 11).

- [x] ✅ v10 saves rejected with a clear schema-version error (schema is now 11).

---

## Build 0.2.5 — Multiple containers + equip slots

- [x] **a1 — Container ItemDef SOs**
  - For each container type the game needs, create a new `ItemDef` asset (Assets > Create > Coremenders > Item Def).
  - Minimum for testing: one **Basic Backpack** (equipsIntoSlot = Backpack, isContainer ✅, slotCapacity = 20, weightCapacity = 0 (unlimited), typeFilter = None, spoilageModifier = 1, weight = 0.5).
  - Optionally add a **Small Pouch** (equipsIntoSlot = Pouch0 or Pouch1, slotCapacity = 10) and a **Quiver** (equipsIntoSlot = Quiver, typeFilter = Ammo, slotCapacity = 40).
  - Optionally add a **Cold Pack** (equipsIntoSlot = Backpack, spoilageModifier = 0.25) to test 0.2.5d freshness extension.
  - Register all new container ItemDefs in `ItemDefRegistry` (add to the list in the Inspector).
  - *Why:* containers are items with their own defIds; all container validation (capacity, filter, modifier) is driven by the SO values.

- [x] **a1 — itemTypeTag on existing ItemDef SOs**
  - Open each existing food/consumable ItemDef and set **Item Type Tag** = `Food`.
  - Open each tool/weapon ItemDef (if any exist) and set **Item Type Tag** = `Tool`.
  - Leave general items at **Item Type Tag** = `None` (treated as General — passes any filter that includes General or None).
  - *Why:* the typeFilter on container ItemDefs uses bitfield matching against itemTypeTag; without tags set, all items pass any filter.

- [x] **Schema bump check**
  - Delete or rename any existing save slot (`slot_0/world.json`) so New Game loads fresh from the template.
  - The template now carries `"schemaVersion": 12`. Old saves (v11) are rejected with a clear error.

---

### 0.2.5 validation — single client

#### 0.2.5a — Container model + equip slots

1. Enter Play mode. Open the Inventory panel (`[I]`).
2. In the **Dev** section, enter the Basic Backpack defId, click **B** (Backpack slot). Verify the `[Backpack] Basic Backpack` header appears in the inventory list with an **Unequip** button.

- [x] ✅ Container equips into the Backpack slot via the dev shortcut.

3. Use **DevAdd** (item defId + qty) to add a food item. Verify it appears indented under `[Backpack]` with freshness label if perishable.

- [x] ✅ Items added via DevAdd land in the equipped container.

4. Equip a second container (e.g., Small Pouch) into Pouch0. Verify both containers appear with their own item lists.

- [x] ✅ Multiple containers display independently.

5. Add a container-type item (e.g., Small Pouch defId) via DevAdd. Verify it appears in the Backpack's item list with `[bag]` suffix.

- [x] ✅ One-level nesting: a container item appears inside the backpack.

6. Save → Load. Verify both equipped containers and all items are restored correctly.

- [x] ✅ Equip slots + nested contents round-trip through save/load.

7. Take a checkpoint, add more items, Revert. Verify equip state returns to checkpoint.

- [x] ✅ Equip state reverts correctly.

#### 0.2.5b — Capacity, filters & weight rollup

1. Equip a container with `slotCapacity = 3`. Fill it with 3 separate item stacks. Attempt to add a 4th. Verify the 4th is silently rejected (no crash, item not added).

- [x] ✅ Slot capacity enforced: container rejects beyond its limit.

2. Equip a Quiver (typeFilter = Ammo). Attempt to add a food item via DevAdd. Verify food is rejected and does not appear in the quiver.

- [x] ✅ Type filter enforced: non-matching items cannot enter filtered container.

3. Add items of known weight to a container. Verify **Carried: X.X kg** in the inventory header changes accordingly. Check values match: Σ(container weight) + Σ(item weight × qty).

- [x] ✅ Carried weight rolls up and displays identically on both clients.

#### 0.2.5c — Drop & pickup with contents

1. Add 3 food items to the Backpack via DevAdd. Click **Unequip** on the Backpack header. Verify the Backpack disappears from the inventory and appears on the ground (a ground visual spawns near the dreamer).

- [x] ✅ Unequipping drops the container as a single world item.

2. Walk to the dropped Backpack. In the Ground panel, verify it shows **Equip Basic Backpack** (not "Pick up"). Click it. Verify the Backpack re-equips into the Backpack slot with all 3 food items intact.

- [x] ✅ Picking up a container restores it with contents.

3. Drop a single food item from inside the container (click **Drop** on the item row). Verify it spawns on the ground. Pick it back up. Verify it returns to the container.

- [x] ✅ Individual item drop/pickup works within containers.

4. Save mid-state (Backpack on ground with items inside). Reload. Verify the Backpack is still on the ground and the items are intact.

- [x] ✅ Container-with-contents round-trips through save/load on the ground.

#### 0.2.5d — Storage-modifier integration

1. Equip a **Cold Pack** (spoilageModifier = 0.25). Add a perishable food item (e.g., Test Meat, 60-min lifespan). Note the freshness % in the inventory.
2. Wait 10 IG minutes. Verify the freshness drops only ~4% (instead of ~17% at ambient rate).

- [x] ✅ Food in cold storage ages at reduced rate (spoilageModifier applied on insert).

3. Click **Unequip** on the Cold Pack. In the Ground panel, wait 5 more IG minutes. Then re-equip it. Note the food condition drops at ambient rate while the container was on the ground (rate reset on unequip).

- [x] ✅ Unequipping a cold container re-stamps perishables to ambient rate.

---

### 0.2.5 acceptance — MPPM two-client

1. Enable Virtual Player. Press Play. Full flow → Action scene. Both clients connect.
2. Host equips a Backpack (dev shortcut), adds food. Virtual client opens inventory — verifies the same Backpack + food appear with identical freshness %.

- [x] ✅ Two-client: multi-container inventory replicates to the owning client with correct freshness.

3. Host unequips the Backpack (drops to ground). Virtual client sees the ground item appear in the Ground panel.

- [x] ✅ Two-client: drop + ground visual replicates correctly.

4. Virtual client (dreamer 1) equips their own Backpack. Both clients see each dreamer's own inventory without cross-contamination.

- [x] ✅ Two-client: per-dreamer inventory stays isolated.

**Schema check:**
- Edit any existing `slot_0/world.json` to set `"schemaVersion": 11` → trigger Load → console logs a clear version mismatch error (schema is now 12).

- [x] ✅ v11 saves rejected with a clear schema-version error (schema is now 12).

---

## Build 0.2.6 — Durability (use-events)

- [x] **a1 — Tool ItemDef SOs**
  - Create at least one tool `ItemDef` asset (Assets > Create > Coremenders > Item Def) to use as a test tool (e.g. **Stone Axe**).
  - Set: **stackable** = ☐, **maxStack** = 1, **isDurabilityTool** = ✅, **maxDurability** = 100, **weight** = 1.5.
  - Set **itemTypeTag** = `Tool` (so it passes `Tool` type filters on containers if needed).
  - Set **consumeEffect** = None (tools are not consumables).
  - Register the new ItemDef in `ItemDefRegistry` (add to the list in the Inspector).
  - *Why:* `isDurabilityTool` enables the durability wire encoding and `DevUseToolServerRpc` validation; `maxDurability` sets the starting/max value.

- [x] **Schema bump check**
  - Delete or rename any existing save slot (`slot_0/world.json`) so New Game loads fresh from the template.
  - The template now carries `"schemaVersion": 13`. Old saves (v12) are rejected with a clear error.

---

### 0.2.6 validation — single client

#### 0.2.6a — Durability value + use-decrement

1. Enter Play mode. Open the Inventory panel (`[I]`). Equip a container via the Dev panel (if not already done).
2. In the **Dev** section, enter the Stone Axe defId, qty = 1, click **Add**. Verify the axe appears in the container with label `[Dur 100%]`.

- [x] ✅ Tool item added at full durability, label shows `[Dur 100%]`.

3. Click **Use** on the Stone Axe row. Verify the label changes to `[Dur 90%]` (10% per debug use).
4. Click **Use** eight more times. Verify label shows `[Dur 10%]` after nine total clicks.

- [x] ✅ Each Use click reduces durability by 10%.

5. Save → Load. Verify the Stone Axe restores to the saved durability value (not reset to 100 or 0).

- [x] ✅ Durability persists through save/load.

6. Click **Use** once more (tenth click). Verify the label changes to `[BROKEN]` and the **Use** button becomes disabled.

#### 0.2.6b — Breaking at 0

- [x] ✅ Tool breaks at 0 durability; Use button is disabled on a broken tool; item remains in inventory as `[BROKEN]`.

7. Take a checkpoint before breaking the axe. Break it (10 clicks). Revert. Verify the axe is restored at its pre-break durability with the **Use** button enabled again.

- [x] ✅ Revert before the break restores the usable tool (durability state clear-and-rehydrated).

---

### 0.2.6 acceptance — MPPM two-client

1. Enable Virtual Player. Press Play. Both clients connect and enter the action scene.
2. Host equips a Backpack (if needed) and adds the Stone Axe via DevAdd. Host sees `[Dur 100%]`.
3. Virtual client (dreamer 1) opens their inventory — Stone Axe is **not** visible there (belongs to host's dreamer). Confirms per-dreamer isolation.
4. Host clicks **Use** three times. Both host and virtual client (via their own inventory sync) should reflect the update; check the host's inventory shows `[Dur 70%]`.

- [x] ✅ Two-client: durability replicates to the owning client; label matches on both.

5. Host takes a checkpoint. Host breaks the tool (10 clicks total from fresh). Verifies `[BROKEN]` + Use disabled. Host reverts. Verifies tool restored at `[Dur 100%]`.

- [x] ✅ Two-client: durability save/revert-clean.

**Schema check:**
- Edit any existing `slot_0/world.json` to set `"schemaVersion": 12` → trigger Load → console logs a clear version mismatch error (schema is now 13).

- [x] ✅ v12 saves rejected with a clear schema-version error (schema is now 13).

---

## Build 0.2.7a — MapEntityLayer migration

### Wiring

- [x] **a3 — Replace WorldItemsSync with MapEntitySync in the Action scene**
  - In the Action scene, find the in-scene `NetworkObject` that currently has the `WorldItemsSync` component.
  - Remove the `WorldItemsSync` component (the class is now an empty placeholder).
  - Add a `MapEntitySync` component to the same GameObject.
  - Wire the `_groundItemPrefab` field on `MapEntitySync` to the same fallback ground-item prefab that was on `WorldItemsSync`.
  - Leave `_syncInterval` at `1` (default).
  - *Why:* `WorldItemsSync` was replaced by `MapEntitySync` at 0.2.7a; the old component must be removed so the obsolete file can be deleted.
  - After confirming play works, delete `Assets/Game/Networking/WorldItemsSync.cs` from the project.

### Validation — single-client

1. Press Play (single-client). Open the Dev → Items menu (top-right Dev button). Click **+World** on any item. Verify the item visual appears on the ground near the dreamer.

   - [x] ✅ Item spawns on the ground via MapEntitySync.

2. Walk up to the dropped item. The Ground panel should list it. Click Pick up. Verify the item moves into inventory and the ground visual disappears.

   - [x] ✅ Pickup removes item from ground and adds it to inventory.

3. Drop an item from inventory (Drop button). Verify it appears in the ground panel and a visual spawns.

   - [x] ✅ Drop places item on ground.

4. Save the game. Open `persistentDataPath/Saves/slot_0/` and confirm `map_map_01.json` exists alongside `world.json`. Confirm `world.json` contains `"schemaVersion": 14` and does **not** contain `worldItems` or `nextWorldItemId`.

   - [x] ✅ map_map_01.json created; world.json is clean.

5. Exit Play. Edit `slot_0/world.json` to set `"schemaVersion": 13`. Press Play and trigger a load. Confirm the console logs a schema mismatch error and the load is rejected.

   - [x] ✅ Old saves (schema 13) rejected with a clear error.

6. Reload from the correct v14 save. Confirm items saved to `map_map_01.json` reappear on the ground.

   - [x] ✅ Map layer round-trips: items restore from map_map_01.json after reload.

7. Take a checkpoint, drop an item, revert. Confirm the dropped item is gone (ground state cleared and rehydrated from the checkpoint).

   - [x] ✅ Revert clears and rehydrates the map layer.

### Validation — MPPM two-client

1. Enable Virtual Player. Press Play. Both clients connect.
2. Host drops an item from inventory. Verify the ground visual appears in both client views.

   - [x] ✅ Two-client: ground items replicate to both clients via MapEntitySync.

3. Virtual client walks to the item and picks it up. Verify the item disappears from the ground on both clients and appears in the virtual client's inventory.

   - [x] ✅ Two-client: pickup is race-safe; item removed on both clients.

**Schema check:**
- Edit any existing `slot_0/world.json` to set `"schemaVersion": 13` → trigger Load → console logs a clear version mismatch error (schema is now 14).

- [x] ✅ v13 saves rejected with a clear schema-version error (schema is now 14).

---

## Build 0.2.8 — Processing chains

### Superseded (by 0.2.9a + 0.2.9d)

> ⚠️ **The original 0.2.8 wiring + validation is removed — it no longer applies.** Those steps were
> built on the `GatherableObjectDef` / `GatherableObjectDefRegistry` model, replaced by the composed
> `Def` / `ActionDef` / `DefRegistry` model at **0.2.9a**, and on separate tree-chain `ItemDef` yields
> (StickItem, FirewoodItem, MediumLogItem, LargeTreeLogItem), folded into composed `Def`s at **0.2.9d**.
> The world-object Defs + ActionDefs are now built in one click by **Coremenders ▸ Tools ▸ Build
> Tree-Chain Defs + ActionDefs** (step **0.2.9d-3**); the item Defs came from the **0.2.9d** migration.
> Validate the whole processing chain via the **0.2.9 consolidated regression** (TDD §5.7, Part B8 +
> Parts C/D). Schema is now **17**.

---

## Build 0.2.9a — Composed Def (Unified SO Model)

> **What changed:** `GatherableObjectDef.cs` and `GatherableObjectDefRegistry.cs` are deleted. All world-object definitions now use the new `Def` SO (with `ActionDef` SOs for each action). `DefRegistry` replaces `GatherableObjectDefRegistry` in the scene. `ItemDef`/`ItemDefRegistry` are **unchanged** for this sub-build; items stay in their own registry until 0.2.9b/d.
>
> **defId namespace note:** `DefRegistry` is the eventual unified namespace for all Defs (items + world objects). For now only world-object Defs go in DefRegistry. Use IDs ≥ 100 for world objects to leave 1–99 for future item Def migration without collisions.

---

### Scene: Replace GatherableObjectDefRegistry with DefRegistry

- [x] **A — Remove old registry**
  - In the Action scene hierarchy, find the GameObject that holds `GatherableObjectDefRegistry`.
  - Remove the `GatherableObjectDefRegistry` component (or delete the entire GameObject if it only held that component).
  - *Why: the type is deleted; leaving it in the scene causes a missing-script error.*

- [x] **B — Add DefRegistry**
  - Create an empty GameObject named `DefRegistry` (or reuse the same GO).
  - Add component: `DefRegistry` (Networking layer).
  - *Why: DefRegistry is the new host-side SO registry; MapEntitySync and DreamerTaskSync both call `DefRegistry.Instance`.*

---

### Superseded — authoring is now automated

> ⚠️ **The ActionDef (C1–C6) + Def (D1–D4) hand-authoring, the DefRegistry-populate step, and the
> 0.2.9a smoke validations are removed.** All of it is now produced in one click by
> **Coremenders ▸ Tools ▸ Build Tree-Chain Defs + ActionDefs** (`TreeChainBuilder`, step **0.2.9d-3**),
> which creates the world Defs, overwrites the six ActionDefs to the corrected spec, and wires each
> Def's `actions[]` — resolving all ids by displayName. Registry population is now **0.2.9d-4**; the
> tree chain is validated by the **0.2.9 consolidated regression** (TDD §5.7, Part B8 + Parts C/D).
> The only manual scene work left is per-Def prefab + `Interactable`/`Collider` wiring (**0.2.9c** below).

---

## Build 0.2.9b — The Located Instance

> **What changed (code only — no new scene objects):** `ItemInstance`, `WorldItem`, and `ItemContainer` are deleted. All three replaced by the single `Instance` class with an `InstanceLocation` field (`InContainer` / `InWorld` / `CarriedBy`). `MapEntityLayer` gains a unified `nextInstanceId` counter; `GroundItemCollection`/`ProcessableCollection` replaced by `InstanceCollection` (same class, two separate fields). Schema bumped to **v16**. No new prefabs, no new SOs, no new scene objects required.

### Compile check

- [x] **A — Open Unity and confirm clean compile**
  - Open the project in Unity 6 LTS.
  - Observe the Console; wait for compilation to finish.
  - *Why: no Editor wiring is needed for this sub-build — but a clean compile with zero errors confirms the type migration succeeded before the 0.2.9a wiring is carried out.*
  - [x] ✅ Console shows 0 compiler errors after all scripts reload.

### Save-file reset

- [x] **B — Delete any existing save files**
  - In the file system (outside Unity), delete `%APPDATA%\..\LocalLow\<CompanyName>\<ProductName>\` (or wherever `Application.persistentDataPath` resolves) to remove old saves.
  - *Why: schema bumped 15 → 16 here; old saves throw a loud mismatch at load. There is no migration path during development.*
  - **Superseded:** the current schema is **17** (bumped again at 0.2.9e). Do the save reset once, at **0.2.9e-1**, against v17.

### Smoke-test validation (single client)

> Minimal smoke-test only — full end-to-end regression is deferred to 0.2.9d per TDD §1.12.

1. Press Play (single client). Start Host. Enter the Action scene.

2. Open Dev → Items → add a Backpack container (dev button). Verify the Inventory HUD shows it.

   - [x] ✅ EquipSlots work: container appears in Inventory HUD.

3. Add a food item to the backpack. Verify it appears in the Inventory HUD with correct freshness %.

   - [x] ✅ Instance in container: item appears with quantity and freshness.

4. Open Dev → Gather. Spawn a processable (e.g. FallenTree). Verify the visual appears near the dreamer.

   - [ ] ✅ MapEntitySync spawns processable as Instance; visual appears.

5. Drop the food item (Drop button in HUD). Verify it appears in the **Ground** panel. Pick it back up. Verify it returns to the backpack.

   - [x] ✅ Drop assigns world id via `nextInstanceId`; pickup clears world id; round-trip succeeds.

---

## Build 0.2.9c — Actions + the world surface

> **What changed:**
> - `Interactable.cs` — new component on world prefabs; holds Def reference + instanceId; exposes `WorldActions()` / `InventoryActions()` filtered by context.
> - `InteractableDetector.cs` — new per-dreamer NetworkBehaviour; camera raycast → targeted Interactable → dev HUD showing world-context action buttons.
> - `MapEntitySync` — sets `Interactable.instanceId` and `.def` on runtime-spawned processable GOs; new `DispatchWorldAction()` routes instant vs timed; new `TryGetProcessableActionState()` for client-side progress display.
> - `DreamerTaskSync` — two new RPCs: `DispatchWorldActionServerRpc` (instant or timed dispatch) and `StopWorldActionServerRpc`.
> - `DefRegistry` — `GetActions(defId, ctx)` filter method for inventory-context action lookup (c4 logic).

---

### Prefab wiring — Interactable component (c2)

Add `Interactable` to every world-object prefab that should be interactable. The prefab must also have a **Collider** (any type) so the camera raycast hits it.

- [x] **A — FallenTree prefab**
  - Open the FallenTree prefab in the Prefab Editor.
  - Add component: `Interactable` (Networking layer).
  - Set **Def** = `Def_FallenTree` SO.
  - Ensure a Collider component is present (add `BoxCollider` or `MeshCollider` if absent).
  - *Why: Interactable.def is used by InteractableDetector to show world actions. instanceId is set at runtime by MapEntitySync.*

- [x] **B — LargeTreeLog prefab**
  - Add `Interactable` component; set **Def** = `Def_LargeTreeLog`.
  - Ensure Collider present.

- [x] **C — BranchPile prefab**
  - Add `Interactable` component; set **Def** = `Def_BranchPile`.
  - Ensure Collider present.

- [x] **D — TestProcessable prefab** *(if it has a distinct prefab)*
  - Add `Interactable` component; set **Def** = `Def_TestProcessable`.
  - Ensure Collider present.

---

### Dreamer prefab wiring — InteractableDetector (c2)

- [x] **E — Add InteractableDetector to the Dreamer prefab**
  - Open the Dreamer prefab.
  - Add component: `InteractableDetector` (Networking layer).
  - Set **Interact Range** = `3.5` (metres; tune to match the visual scale of your scene).
  - Set **Interact Layer** = the layer your world-object colliders are on (e.g., `Default` or a dedicated `Interactable` layer). Restricting the layer prevents the raycast from hitting terrain or the dreamer itself.
  - Leave **Override Camera** empty to use `Camera.main`.
  - *Why: `InteractableDetector` is per-dreamer; it reads the Def from the hit Interactable and shows world-context action buttons in the centre-bottom HUD.*

---

### Smoke-test validation (single client, 0.2.9c)

> Minimal check only — full regression at 0.2.9d.

1. Press Play (single client). Start Host. Enter the Action scene.
2. Spawn a FallenTree via Dev → Gather → **+Spawn**.
3. Walk the dreamer to within ~3.5 m of the FallenTree. Verify the **centre-bottom** HUD shows `── Fallen Tree ──` and a `process` button (and a `Stop` button next to it).

   - [x] ✅ InteractableDetector detects the FallenTree Interactable; world-context action list appears.

4. Ensure a Stone Axe is in inventory (Dev → Items → +Inv). Click **process** in the centre-bottom HUD. Let clock advance ≥ 10 IG minutes. Verify: FallenTree disappears; LargeTreeLog + BranchPile appear; their HUD entries show `pickup` / `split` / `gather_sticks` / `gather_firewood` buttons when approached.

   - [x] ✅ Timed action (process) dispatches through DispatchWorldAction → StartContributor; completes with correct spawns.

5. Approach the LargeTreeLog. Click **pickup** in the HUD (instant action). Verify: LargeTreeLog visual disappears; a LargeTreeLog item appears in the dreamer's backpack in the inventory HUD.

   - [x] ✅ Instant ToInventory: processable removed from world and delivered to dreamer's container.

6. Drop the LargeTreeLog from inventory (Drop button). Verify a new Interactable visual appears at the drop position and its world action(s) show in the centre-bottom HUD when approached. *(0.2.9e note: because its Def has world actions it is NOT listed in the ground-item "Ground" panel — that panel now shows only plain items with no world actions.)*

   - [x] ✅ Drop creates a world Instance; the Interactable (on runtime-spawned visual) is detectable by the raycast and shows its world actions.

---

### MPPM two-client (0.2.9c)

1. Enable Virtual Player. Press Play. Both clients enter the Action scene.
2. Host spawns a FallenTree. Host approaches and clicks **process** from the centre-bottom HUD.
3. Virtual client also approaches and clicks **process**. Verify console shows `ratePerMinute = 2`.

   - [x] ✅ Two-client: both dreamers contribute; co-op accrual rate = 2.

4. Host approaches the spawned LargeTreeLog and clicks **pickup**. Verify: LargeTreeLog disappears on both clients; item appears in host dreamer's inventory.

   - [x] ✅ Two-client: instant pickup replicates; processable visual removed on both clients.

---

## Build 0.2.9d + 0.2.9e (minus e2) — Unified world-object model: def unification + storage collapse

> **What changed (code, by Claude Code):**
> - **0.2.9d** — `ItemDef` folded into the composed `Def` via aspects; `ItemDefRegistry` deleted;
>   every item-system reader repointed to `DefRegistry` (`Get(defId)` → `Def`, reading aspects
>   through new convenience properties on `Def`); `IItemDefLookup` still resolves the composed Def.
> - **0.2.9e1/e3/e4/e5** — `MapEntityLayer.groundItems` + `.processables` collapsed into a single
>   `worldObjects` collection; `MapEntitySync` rewritten to one full-state runtime payload; visual
>   routing keyed on whether a Def has world actions; `LargeTreeLog`-style pickup flips location;
>   Carry outcome (InWorld↔CarriedBy) implemented (dormant until a Carry ActionDef is authored);
>   schema bumped **16 → 17**; templates updated. **Authored delta-only nodes (e2) are 0.2.9f — not built.**
>
> **Final schema version: 17.** (One bump for the whole d+e refactor, on top of 0.2.9b's 16.)
>
> **Compile status:** this project has no CLI compiler (all compilation is in-Editor). Claude Code
> performed a full static reference audit at both checkpoints (after d, after e5) — every removed
> symbol (`ItemDefRegistry`, `GetItemDefSO`, `groundItems`, `processables`, old payload structs)
> resolves to its replacement. Confirm a clean in-Editor compile as the first setup step below.

### Wiring / setup (do these before the consolidated regression pass)

- [x] **0.2.9d-1 — Compile check.** Open the project; confirm **0 compile errors** in the Console.
  A missing-script warning on the old `ItemDefRegistry` scene GameObject is expected (removed in the next step). *Why: the CLI cannot invoke the Unity compiler; this is the real compile checkpoint.*

- [x] **0.2.9d-2 — Run the ItemDef → Def converter.**
  - Menu: **Coremenders ▸ Tools ▸ Migrate ItemDefs → Defs**.
  - It writes one composed `Def` per legacy `ItemDef` into `Assets/Game/Data/Defs/Migrated/`,
    preserving `defId` and mapping fields to aspects (Inventory / Consumable / Perishable / Tool /
    Container). Read the Console report — it lists every conversion and the aspects assigned.
  - Verify a spot-check: Stone/Wood → Inventory only; food/water → Inventory + Consumable + Perishable;
    save-consumable → Inventory + Consumable(SaveConsumable); containers → Inventory + Container; Stone Axe → Inventory + Tool.
  - *Why: d2 migration — items must exist as composed Defs before DefRegistry can resolve them.*

- [x] **0.2.9d-3 — Build the tree chain (world Defs + ActionDefs) in one click.**
  - Menu: **Coremenders ▸ Tools ▸ Build Tree-Chain Defs + ActionDefs** (`TreeChainBuilder`).
  - It creates/updates the four world-object Defs (**Fallen Tree**, **Branch Pile**, **Test Processable**,
    and the unified **Large Tree Log**), overwrites all six ActionDefs to the corrected spec, and wires
    each Def's `actions[]`. Every item/world reference is resolved by Def **displayName**, so it uses
    this project's real ids. Read the Console report for the assigned defIds + any missing-yield warnings.
  - **Large Tree Log reconciliation is automated here:** the builder rebuilds the migrated `Def_Large_Tree_Log`
    (defId **14** kept) as the unified Def — Inventory aspect + `pickup` (instant `ToInventory`, flips
    location) + `split` — and points FallenTree's `process` depletion-spawn at defId 14. This is what
    satisfies the C1/C2 "one Def" regression. (There is no separate `LargeTreeLogItem` any more.)
  - This **supersedes the manual 0.2.9a ActionDef (C1–C6) and Def (D1–D4) authoring** below and the
    former hand-reconciliation of LargeTreeLog.
  - **After running:** set each world Def's `prefab` (+ add `Interactable` + a `Collider` on that prefab —
    0.2.9e-2 / 0.2.9c), then add the new Defs to `DefRegistry` in 0.2.9d-4.
  - ⚠️ **Flag:** confirm the auto-assigned defIds for the newly-created world Defs (Fallen Tree / Branch
    Pile / Test Processable start at 1001+) don't collide with anything you intend to author by hand.

- [ ] **0.2.9d-4 — Populate `DefRegistry`, remove `ItemDefRegistry` GameObject.**
  - In the **Action** scene: add every migrated item `Def` **and** every world-object `Def` to the
    `DefRegistry` component's `_defs` list (one registry, one defId namespace).
  - Delete the old **ItemDefRegistry** GameObject from the Action scene (its component no longer exists).
  - Expect at startup: `[DefRegistry] Built with N def(s).` and **no duplicate-id warnings**.
  - *Why: d3 — one registry resolves every `Instance.defId`.*

- [x] **0.2.9d-5 — Deleted `ItemDef.cs` + converter + old ItemDef assets (done by Claude Code).**
  - Migration verified: all 14 defIds (1–14) exist as composed `Def`s under `Assets/Game/Data/Defs/`
    with correct aspects. Removed: the 14 old `ItemDef` `.asset` files (+metas) in `Game/Data/Items/`,
    `ItemDef.cs`, and `ItemDefToDefConverter.cs` (+metas). `1001_FallenTree.asset` (a `Def`) kept.
  - The only remaining `ItemDef` mentions are historical doc comments; `ItemDefData` / `IItemDefLookup`
    (the pure-data view) stay — they now resolve the composed `Def` via `Def.ToData()`.
  - *Why: d1 — no separate ItemDef type remains.*

- [x] **0.2.9e-1 — Reset saves.** Delete `Application.persistentDataPath/Saves/` — schema bumped to **17**;
  pre-refactor saves are rejected loudly on load. *Why: e5 — no migration; old saves invalid.*

- [x] **0.2.9e-2 — Interactable prefabs carry an Inventory-capable Def where needed.**
  - Confirm runtime world-object prefabs (FallenTree, LargeTreeLog, BranchPile, TestProcessable) have an
    `Interactable` component + a `Collider`; `MapEntitySync` sets `instanceId`/`def` at spawn.
  - Confirm the dreamer prefab has `InteractableDetector` (interact range ~3.5, interact layer set).
  - *Why: e3 — dropped world objects must be raycast-detectable to stay interactable.*

### Playtest validation (single client, then MPPM)

Run the existing **0.2.9 Consolidated setup + regression** (TDD §5.7, Parts B–D). Deviations introduced by d+e:

- [x] ✅ **B1–B7 (inventory / consume / spoilage / containers / durability)** behave exactly as before — now resolved through composed Defs + aspects (the +Inv dev page lists only inventory-capable Defs).
- [x] ✅ **B2 ground pickup/drop** of plain items (Stone/Wood) works via the Ground HUD; **B8 gathering + tree chain** (FallenTree → LargeTreeLog + BranchPile → sticks) works through the unified `worldObjects` collection.
- [x] ✅ **C1 — carried-then-dropped stays interactable:** get a LargeTreeLog into inventory, **drop** it, walk up → the Interactable HUD shows its world action(s). It's interactable because dropping only flipped its location.
- [x] ✅ **C2 — pickup flips location, not destroy+create:** pick up a world LargeTreeLog → it lands in inventory as the **same Def** (one entry); drop → an interactable world log again. No world-def/item-def pair.
- [x] ✅ **B9 save/revert:** map layer round-trips through `map_{id}.json` (now `worldObjects`); Checkpoint → change → Revert clears-and-rehydrates; a pre-refactor (v16) save is rejected.
- [x] ✅ **MPPM (Part D):** race-safe concurrent ground pickup; co-op gather rate = 2; world-object replication; per-dreamer inventory isolation.

---

## Build 0.2.9 — Bugfix: instant-action routing + drop rehydrates world accrual

> **What changed (code, by Claude Code):** two defects in the world-object interaction path.
> - **Instant-action misrouting** — the Dev → Gather **Go** button routes every action through
>   `RequestGatherServerRpc` → `MapEntitySync.StartContributor`, the *timed* path. For an instant
>   action (`requiredLabor 0`, e.g. Large Tree Log `pickup`) this opened a labor segment that
>   "completed" immediately into nothing (empty `yields`, `onDepletion Remove`, other actions
>   still open) and left the object in the world. Fix: `StartContributor` now redirects any
>   `requiredLabor <= 0` action to `DispatchWorldAction` (→ `InstantPickup` / `ToggleCarry`), so an
>   instant action resolves the same way from every entry point. No recursion (`DispatchWorldAction`
>   only calls `StartContributor` for `requiredLabor > 0`).
> - **Drop dropped accrual** — a picked-up world object loses its `location.accrual` when it enters
>   a container as a plain item. `DropItemServerRpc` (and dev `PlaceItem`) rebuilt the ground
>   Instance with `accrual == null`, so it reported `ActionCount 0`, never entered the processable
>   registry, and became non-interactable (couldn't be processed or re-picked-up). Fix: new
>   `MapEntitySync.InitWorldAccrual(inst)` rebuilds one `ActionAccrualState` per world action on
>   every InWorld placement (plain items stay `accrual`-null); called from `DropItemServerRpc` and
>   `PlaceItem`. Carry-drop already preserved accrual and is unchanged.
>
> **Pure code change — no schema bump, no new wiring.** These make the existing **C1/C2** checks
> above pass; the round-trip below is the added regression.

### Playtest validation (single client)

1. Start Host, enter the Action scene. Equip a container (Dev → Items → enter Basic Backpack defId → **B**).
2. Spawn a Large Tree Log: Dev → Gather → **+Spawn** on `Large Tree Log` (or process a Fallen Tree).
3. Approach it. In Dev → Gather, click **Go** on the `pickup` action. Verify: the log disappears from the world and a Large Tree Log appears in the backpack. *(If instead the Console logs `Instant pickup failed: no container space` the log stays — that's a full/absent container, not this bug; free a slot and retry.)*

   - [x] ✅ Instant `pickup` via the Gather **Go** button removes the world object and delivers it to the container (no longer "completes into nothing").

4. **Drop** the Large Tree Log from the inventory HUD. Walk back to it and open Dev → Gather.

   - [x] ✅ The dropped log is listed again as a processable with `pickup` / `split` actions (accrual rebuilt on drop).

5. Click **Go** on `split` (with a Stone Axe equipped) and let it complete; then repeat step 3–4 to confirm the drop → re-pickup → drop loop is stable.

   - [x] ✅ Drop → re-process / re-pickup round-trip works repeatedly; a dropped world object is never inert.

### Follow-up: world objects were only actionable via the Dev Gather panel

> **Symptom:** a dropped Large Tree Log could be picked up/split only through Dev → Gather **Go**, while
> a Backpack / Medium Log / berries showed a normal "Pick up" button in the top-right panel.
> **Cause (not a data bug):** a Def *without* world actions (`actions: []`) is a plain ground item and
> appears in the always-on **distance-based** *Ground (nearby)* panel (no camera needed). A Def *with*
> world actions (Large Tree Log) is excluded from that panel by design (0.2.9e) and routed to the
> centre-bottom **Interactable HUD**, which `InteractableDetector` selected via a **camera raycast**.
> The project's camera is static and untagged, so `Camera.main` was null → that HUD never resolved a
> target → no button. (The `[probe]` readout below reports `NO CAMERA` in this state.)
>
> **Fix (code, by Claude Code):** `InteractableDetector` now selects its target with **camera-aim when a
> camera exists** (SphereCast via `_aimAssistRadius`, default **0.4**, so thin/low colliders don't need
> pixel-perfect aim) **and a distance-based fallback** (`OverlapSphereNonAlloc`, nearest Interactable
> within `_interactRange`) when there is no camera or nothing is aimed at. World-action objects are now
> reachable exactly like ground items — no camera or follow-cam required. A `_showProbeDebug` toggle
> draws an owner-only `[probe]` readout (camera status / hit count / resolved target) for diagnosis.
> All new serialized fields default via their initializers — existing Dreamer prefabs need no re-wiring.

- [x] **Detector fields sanity (optional).** On the **Dreamer** prefab's `InteractableDetector`, confirm **Aim Assist Radius = 0.4** and **Show Probe Debug** is on for now (uncheck once interaction is confirmed). Later, when a follow-camera exists, tag it **MainCamera** or assign `_overrideCamera` to get precise aim on top of the proximity fallback. *Why: new SerializeFields; verify defaults + know the camera knob.*

6. Walk up to a **dropped Large Tree Log** (no dev panel, static camera is fine). Verify the **centre-bottom** HUD shows `── Large Tree Log ──` with `pickup` / `split` buttons, and the `[probe]` line reads `nearest…: Large Tree Log`.

   - [x] ✅ World-action objects are selectable by proximity without a camera; the Large Tree Log has a non-dev pickup/split HUD, matching how ground items already work.

---

## Build 0.2.9 — Unify item pickup: one interaction path for every item

> **Why:** we had **two** pickup systems — plain items (Stone / Wood / berries / containers) used the
> distance-based *Ground (nearby)* panel + `PickupItemServerRpc`, while world-action objects (Large
> Tree Log) used the Interactable HUD + `DispatchWorldAction`. That fork is the source of the "different
> behavior per item" bugs. The TDD §1.12 model is one Def/Instance/Action system where **Pickup is a
> shared ActionDef** and "existing pickup become ActionDefs" (build task 0.2.9c1) — this finishes that
> migration.
>
> **What changed (code, by Claude Code):**
> - **Shared `pickup` ActionDef** (instant, ToInventory, world context) now carried by **every**
>   inventory-capable Def. Items differ only by their *extra* actions.
> - **One pickup path:** Interactable → `DispatchWorldActionServerRpc` → `MapEntitySync.InstantPickup`,
>   which auto-equips containers (`DreamerInventorySync.TryEquipContainerFromWorld`) and delivers all
>   other items (`TryDeliverItem`).
> - **Retired the legacy path:** removed the top-right *Ground (nearby)* panel, `PickupItemServerRpc`,
>   `EquipContainerServerRpc`, and `MapEntitySync.GetNearbyItems` / `_currentItems`.
> - **No prefab wiring:** `MapEntitySync` adds an `Interactable` + a trigger `SphereCollider` to any
>   spawned visual that lacks them; `InteractableDetector` probes with `QueryTriggerInteraction.Collide`.
> - **Out of scope (deferred, see TODO):** inventory-context actions (consume / drop / split / equip)
>   stay as the current inventory-HUD buttons; only *world* pickup was unified this pass.
>
> **No schema bump.** Old saves still pick up fine (an item with null accrual still resolves its
> instant pickup); freshly dropped/placed items rebuild accrual via `InitWorldAccrual`.

### Wiring / setup

- [x] **U1 — Run the pickup unifier.** Menu: **Coremenders ▸ Tools ▸ Unify Item Pickup Actions**
  (`PickupActionAssigner`). It creates `ActionDef_Pickup`, prepends it to every Def with an Inventory
  aspect (de-duping any existing `pickup`), and deletes the superseded `ActionDef_LargeTreeLog_Pickup`.
  Read the Console report — it lists each updated Def and its new action list. Idempotent; re-runnable.
  *Why: gives every item the one shared pickup; ActionDefs are referenced by Defs, so no DefRegistry change.*
- [x] **U2 — (Optional) Re-run the tree chain.** **Coremenders ▸ Tools ▸ Build Tree-Chain Defs + ActionDefs**
  now wires the Large Tree Log to the *shared* pickup too. Not required (U1 already reconciles it), but
  keeps the one-click chain consistent. *Why: TreeChainBuilder now references the shared pickup asset.*

### Playtest validation (single client)

1. Start Host, enter the Action scene. **Do not** equip a backpack yet.
2. Dev → Items → **+World** a **Stone** (or **Berries**) near the dreamer. Walk up to it.

   - [x] ✅ The **centre-bottom Interactable HUD** shows the item with a `pickup` button (the top-right *Ground* panel is gone).

3. Click `pickup` with no container equipped → Console: `Instant pickup failed: no room/slot…`, item stays. Equip a Basic Backpack (Dev → Items → defId → **B**), pick up again.

   - [x] ✅ Plain items pick up through the same Interactable HUD path as the Large Tree Log; delivery still requires container space.

4. **+World** a **Basic Backpack**, walk up, `pickup`.

   - [x] ✅ A container's `pickup` auto-equips it into its slot (replacing the old Equip button), with contents intact.

5. Drop each of a plain item, a container, and a Large Tree Log; re-approach and re-pick-up each.

   - [x] ✅ Every item type drops and re-picks through the one path; none is inert; behavior is identical across item types (only the action list differs).

### MPPM two-client

1. Enable a Virtual Player. Host and client each `pickup` different nearby items.

   - [x] ✅ Pickup replicates (visual removed on both clients); per-dreamer inventory isolation holds; no duplication on a concurrent double-pickup of the same item.

---

## Build 0.2.9 — Generalized action economy (SUPERSEDED — history only)

> ⚠️ **Superseded; kept only as a record of the first attempt — do not action.** This pass made action
> costs/rewards data-driven but drained them **per minute** (`ActionEffect.perMinute`, `SimResolver`
> Stage 4c, `MapEntitySync.BuildActiveWorkEffects`) with the list on `ActionDef`. It was replaced by the
> two builds below: **(1) Generic action verbs + per-item ItemAction** moved the list to `ItemAction`,
> removed `energyPerMinute`, and deleted the per-item ActionDef assets; **(2) Up-front action costs**
> replaced per-minute drain with an **up-front debit + pro-rata refund** and turned `perMinute` into a
> one-time `amount`, with rewards delivered by `yieldModel`. Use those two sections for the current model,
> wiring, and validation.

---

## Build 0.2.9 — Generic action verbs + per-item ItemAction (costs/rewards live on the item)

> **Why:** costs/rewards (and labor, yields, tools) were on **per-item ActionDef assets**
> (`ActionDef_LargeTreeLog_Split`, …). The intended §1.12 model is **generic shared verbs** with the
> per-item data on the **item's Def**. This moves everything item-specific onto the item and collapses
> the action assets to a tiny reusable palette.
>
> **What changed (code, by Claude Code):**
> - **`ActionDef` is now just a generic verb** — `actionId` + `outcome` + `context`. One asset per verb
>   (Pickup / Split / Process / Gather), reused across items.
> - **`Def.actions` is now `ItemAction[]`** — each binding names a verb and carries THIS item's
>   `timeRequired`, `yields`, `depletionSpawns`, **`effects` (costs/rewards)**, `allowedToolCategoryIds`,
>   `toolRequired`, `prerequisite`, `coop`, and an optional `labelOverride` (so one `Gather` verb reads
>   as "gather sticks" vs "gather firewood").
> - **Every engine reader** (Interactable, MapEntitySync dispatch/accrual/complete/economy, the HUDs,
>   DefRegistry.GetActions) now reads specifics from the item's `ItemAction`, not the verb.
> - **`energyPerMinute` removed** — superseded by `ItemAction.effects` (an `Energy` entry).
> - **Authoring tools rebuilt:** `TreeChainBuilder` creates the verbs + wires per-item `ItemAction`
>   bindings and **deletes the old per-item ActionDef assets**; `PickupActionAssigner` prepends a
>   `pickup` binding to every inventory Def; the paste-based creator is now a minimal **Action Verb
>   Creator** (verbs are 3 fields).
>
> **No save-schema change** — Defs/ActionDefs are authored assets, not part of the runtime save.

### Wiring / setup (migration — required, old action wiring is now invalid)

- [x] **V1 — Re-run the tree chain.** **Coremenders ▸ Tools ▸ Build Tree-Chain Defs + ActionDefs**.
  Rebuilds each world Def's `actions[]` as `ItemAction` bindings against the generic verbs and deletes
  the six legacy `ActionDef_<Item>_<Action>` assets. Read the Console report. *Why: `Def.actions` changed
  type; the old per-item ActionDef references no longer deserialize and must be rebuilt.*
- [x] **V2 — Re-run the pickup unifier.** **Coremenders ▸ Tools ▸ Unify Item Pickup Actions**. Prepends
  a `pickup` `ItemAction` (bound to the shared Pickup verb) to every inventory-capable Def. *Why: same
  type change; rebuilds the pickup binding for non-tree-chain items.*
- [x] **V3 — Spot-check a Def in the Inspector.** Select `Def_Large_Tree_Log` → **Actions**: two entries,
  `action` = ActionDef_Pickup and ActionDef_Split, with Split's `timeRequired`, `yields` (3× Medium Log),
  `effects`, and tool set on the **binding**. *Why: confirms per-item data now lives on the item.*

### Playtest validation (single client)

1. Run V1–V2, enter Play. Process a Fallen Tree → Large Tree Log + Branch Pile; pick up / split the log;
   gather sticks and firewood from the pile.

   - [x] ✅ The whole tree chain works through the generic verbs; labels read correctly (incl. "gather
     sticks" vs "gather firewood" from one Gather verb via `labelOverride`).

2. Confirm costs still apply — they come from the item's `ItemAction` (`timeRequired` + `effects`). *(See the next build for the exact up-front/refund behaviour.)*

   - [x] ✅ Costs are read from the item's binding, not a per-item ActionDef.

3. Author a new interaction with **zero code**: on any item Def, add an `ItemAction` (e.g. verb = Split)
   and set its `timeRequired`/yields/effects; on another item add the same verb with different numbers.

   - [x] ✅ The same verb produces different cost/reward/yield per item, driven entirely by the item Def.

---

## Build 0.2.9 — Up-front action costs (single `timeRequired` + refund on stop)

> **Why:** two knobs expressed "time" (`requiredLabor` for duration + a `TimePool` per-minute effect for
> cost), and costs drained per minute rather than being committed up front. Now: one `timeRequired` per
> action = duration **and** timePool cost; costs are debited **up front** and **refunded pro-rata** if the
> action is stopped or finishes early — exactly like eating.
>
> **What changed (code, by Claude Code):**
> - **`ItemAction.requiredLabor` → `timeRequired`** — the single time value: it sets how long the action
>   takes (co-op still finishes faster) AND how much `timePool` is debited. No separate TimePool effect.
> - **`ActionEffect.perMinute` → `amount`** — effects are now **one-time totals** (energy, warmth, …),
>   negative = cost / positive = reward.
> - **Up-front debit + refund:** `MapEntitySync.StartContributor` debits the whole cost when a worker
>   starts (`timeRequired` → timePool, plus `effects` totals; `timePool < 0` → exhaustion).
>   `StopContributor` and `TriggerActionComplete` refund the **unused share** (`1 − activeMinutes ⁄
>   timeRequired`), tracked by a non-persisted per-(instance,action,slot) join clock. **Co-op:** each
>   worker debits the full cost and is refunded their unused share, so each nets the cost of the minutes
>   they personally worked.
> - **Removed the per-minute `SimResolver` Stage 4c** (and its `Step` param / `BuildActiveWorkEffects`).
> - **`TreeChainBuilder`** now seeds `timeRequired` + an energy total of `2 × timeRequired` (no TimePool
>   effect).
>
> - **Rewards follow `yieldModel`** (never granted up front): reward `effects` (positive amount) and
>   `yields` are delivered **on completion** when `Atomic`, or **over the work** when `Proportional`
>   (`DeliverActionRewards` / `DeliverProportionalProgress`). Reward effects go to each active
>   contributor; item yields to the primary contributor. `depletionSpawns` still spawn fully on completion.
>
> **No save-schema change.** (Join clocks and the proportional delivered-fraction are intentionally not
> persisted — see TODO.)

### Wiring / setup

- [x] **U1 — Re-run the tree chain** so bindings carry `timeRequired` + the energy-total effects:
  **Coremenders ▸ Tools ▸ Build Tree-Chain Defs + ActionDefs**. *(Same builder as V1 above — one run
  covers both; the builder is idempotent, so running it once after all 0.2.9 code changes is enough.)*

### Playtest validation (single client)

1. Note **Energy** and **Pool**. Start **split** on a Large Tree Log (axe). Watch the HUD **at the moment work starts**.

   - [x] ✅ Pool drops by `timeRequired` and Energy by the effect total **immediately** (up front), not gradually.

2. **Stop** the split about halfway (Stop button / walk away).

   - [x] ✅ Roughly half the time+energy is **refunded** (you're charged only for the portion worked).

3. Let a split run to completion.

   - [x] ✅ Solo: full cost kept, no refund. The log transforms into 3 Medium Logs.

   - [x] ✅ **Atomic reward timing:** the 3 Medium Logs arrive **only at completion** — nothing is delivered mid-work (and no reward is granted at start).

4. **(Reward timing — Proportional)** Author a test: on a Def's action set `yieldModel = Proportional`, give it `yields` (e.g. 6× a stackable item) and/or a positive `effect` (e.g. Warmth +30), and work it partway.

   - [x] ✅ Items/effects arrive **incrementally as the bar fills** (≈ `floor(progress × amount)` for items; the effect trickles in), not all at the end. Stopping partway keeps what was already delivered; costs still refund their unused share.

### MPPM two-client (co-op)

1. Host + client both work the **same** split from the start; it finishes in ~half the time.

   - [x] ✅ Each worker is refunded ~half (they each worked ~half the minutes); the job's total cost is shared, not doubled.

---

## Build 0.2.10 — Crafting (a: RecipeDef + hand-craft · b: tiers + RNG-as-state · c: station · d: broken items)

> **What changed (code, by Claude Code):** crafting is a recipe-driven configuration of the §5.5/§5.6
> task engine — no new engine.
> - **New SOs:** `RecipeDef` (`Coremenders/Recipe`) with an *iterated* `inputs` list, `requiredToolCategoryId`,
>   `requiredStationDefId`, hand cost (`timeCost`/`energyCost`) + station cost (`requiredLabor`/`energyPerMinute`),
>   `coop`, inert `unlockConditionId`, `outputAmount`, a `tierTable` (`{outputDefId, weight}` list) and
>   `rollGranularity` (PerUnit/PerBatch). `RecipeRegistry` (mirrors `DefRegistry`) resolves recipes by id and
>   is auto-populated from `Assets/Game/Data/Recipes` by `RecipeRegistryEditor`.
> - **RNG-as-state (b):** `CraftRng` (Simulation, pure C#) — `craftSeed = f(crafterSlot, recipeId, startTime,
>   craftCounter)`; `WorldState.craftCounter` is the monotonic save value. Tier rolled **at completion**;
>   `CraftResolver.BuildOutputs` re-derives identically on skip/replay; PerUnit groups same-tier units into
>   clean stacks. Skill weighting is a seam (inert).
> - **Hand craft (a):** `DreamerInventorySync.RequestHandCraftServerRpc` / `CancelCraftServerRpc`; a
>   self-contained timed craft stored on `DreamerRecord.craft`, completed on the host tick, output delivered to
>   inventory. Materials/time/energy reserved on commit; unelapsed time+energy + materials refunded on cancel.
> - **Station craft (c):** `MapEntitySync.StartStationCraft` / `StopStationCraft` / `TriggerStationCraftComplete`
>   — object-bound labor on the station `Instance` (`Instance.craft` + `location.accrual[craftIdx]`), reusing the
>   §5.5.5 accrual engine (co-op = faster; assist/leave re-project; single-crafter locks). Partial persists on the
>   station, resumable by anyone. Output materialises **at the station** for collection.
> - **Broken (d):** `ToolAspect.brokenFormDefId`; on durability 0 the tool instance is **destroyed and replaced**
>   by its broken Def in place (`ReplaceToolWithBrokenForm`) — the 0.2.6 broken-bool is retired. Salvage is the
>   existing gather engine: a broken Def with a `dismantle` action + material yields (drop it → dismantle → mats).
> - **Save schema bumped 17 → 18** (WorldState.craftCounter, DreamerRecord.craft, Instance.craft); `world.json`
>   template + `SaveSystem.Save` worldOnly updated.

### Wiring / setup

> **Authoring aids (editor tooling added alongside 0.2.10):**
> - **Aspects:** selecting a `Def` shows an **"Add Aspect ▾"** dropdown (`DefEditor`) — use it, not the
>   aspects list's `+` button (which can't pick a type). "Clear empty entries" removes stray null aspects.
> - **Ids auto-assign:** new `Def` / `RecipeDef` assets get a unique `defId` / `recipeId` automatically on
>   creation (and a Ctrl+D duplicate is re-assigned). So "assign a unique id" below usually needs no manual
>   number. To bulk-fill or surface collisions, run **Coremenders ▸ Tools ▸ Assign Missing Ids (Defs +
>   Recipes)**; the Def/Recipe inspectors also warn + offer a one-click fix. Ids are never auto-renumbered
>   once valid (they're on the wire / in saves).
> - **Def-id fields are searchable picklists** (`DefId` drawer): every field that references another Def —
>   recipe `inputs`/`tierTable` outputs, `requiredStationDefId`, action `yields` (item + world object), and a
>   tool's Broken Form — shows a **"displayName (#id)" button** that opens a searchable list; pick by name and
>   the id is filled in. Item fields list only inventory Defs; station/world-object fields list only world-action
>   Defs. So you never look up or type raw ids in these fields.

- [x] **Scene — add RecipeRegistry** *(0.2.10a1)*
  - In the Action scene, add a `RecipeRegistry` component (Networking) — reuse the `DefRegistry` GameObject or a
    fresh `RecipeRegistry` GO. It is `DontDestroyOnLoad` and exposes `RecipeRegistry.Instance`.
  - Create the folder `Assets/Game/Data/Recipes`. The registry auto-populates from it (on asset import and before
    Play); use **Rebuild From Recipe Folder** on the component if needed.
  - *Why: hand + station crafts resolve recipes via `RecipeRegistry.Instance`; without it every craft is rejected.*

- [x] **A1 — Author the shared verbs** (`Coremenders ▸ Action Verb`, in `Assets/Game/Data/ActionDefs`)
  - **`craft`** — `actionId = "craft"`, `context = World`, `outcome = Transform` (unused by station completion, but
    set it). Used as the station's craft action so it gets an accrual slot. The verb id **must be exactly `craft`**
    (`MapEntitySync.CraftActionId`) — the engine routes it only through `StartStationCraft`.
  - **`dismantle`** — `actionId = "dismantle"`, `context = World`, `outcome = Transform`. The salvage action on broken Defs.
  - *Why: verbs are shared SOs; recipes/Defs reference them by asset.*

- [x] **A2/A4 — Author a hand recipe + its output Def** *(hand-craft path)*
  - Author an **output Def** (`Coremenders/Def`, in `Assets/Game/Data/Defs`) — e.g. `Def_Crude_Torch`: give it an
    `InventoryAspect` (stackable as desired). Assign a **unique `defId`**.
  - Author a **RecipeDef** (`Coremenders/Recipe`, in `Assets/Game/Data/Recipes`) — e.g. `Recipe_Crude_Torch`:
    unique `recipeId`; `inputs` = **2+ existing item Defs** (e.g. a Stick ×2 + Berries ×1 — use real defIds so you
    can gather/dev-add them); `requiredToolCategoryId = 0` (hand); `requiredStationDefId = 0`; `timeCost` (e.g. 30),
    `energyCost` (e.g. 5); `outputAmount = 1`; `tierTable` = **one entry** `{outputDefId = the output Def, weight = 1}`;
    `rollGranularity = PerBatch`.
  - *Why: proves a4 (hand recipe consumes inputs/time/energy, yields the output) with a1's iterated inputs.*

- [x] **A2 — Prove list-iteration** *(a2 acceptance)*
  - Duplicate the recipe (or edit it) so `inputs` goes from **2 → 5** materials. No code change is needed — the
    consume/refund iterate the list.

- [x] **B1/B6 — Author a multi-tier recipe** *(tier table + RNG)*
  - Author **2–3 output Defs** as tiers (e.g. `Def_Arrow_T1`, `Def_Arrow_T2`, `Def_Arrow_T3`), all stackable.
  - Author a recipe (e.g. `Recipe_Arrows`): `outputAmount = 20`; `tierTable` = the 3 tiers with weights (e.g. T1
    weight 70, T2 25, T3 5 — the fixed unskilled distribution); `rollGranularity = PerUnit`.
  - *Why: b6 — a PerUnit batch of 20 forms a few clean per-tier stacks, not 20 uniques. Also author a `PerBatch`
    variant to see one uniform stack.*

- [x] **C1/C4 — Author a station Def + a station recipe** *(station-craft path)*
  - Author a **station Def** (e.g. `Def_Workbench`): unique `defId`; a `prefab` (any visual); its `actions` list
    contains **one `ItemAction` bound to the `craft` verb** (any `timeRequired > 0`, e.g. 60 — it is ignored for
    station crafts but keep it non-zero; set `coop` to taste). No InventoryAspect needed (it is a world object).
  - On the station **prefab**: ensure an `Interactable` + a (trigger) `Collider` — or rely on `MapEntitySync`'s
    fallback which adds them. Assign the Def's `prefab`.
  - Author a **station recipe** (e.g. `Recipe_StoneAxe`): `requiredStationDefId = the station Def's defId`;
    `requiredToolCategoryId` as desired; `requiredLabor` (e.g. 120); `energyPerMinute` (e.g. 0.5); `coop = true`;
    `tierTable` + `outputAmount` + `rollGranularity` as above.
  - *Why: c1 (labor on the station, collect there), c3 (coop), c4 (a crafted-station Def is a valid requiredStation).*

- [x] **D1/D2 — Author a broken Def + wire brokenForm + low tier** *(broken items)*
  - Author a **Broken Def** (e.g. `Def_Broken_Axe`): `InventoryAspect` (non-stackable), **no `ToolAspect`** (useless
    as a tool); its `actions` list has **one `ItemAction` bound to `dismantle`** with `timeRequired > 0` and `yields`
    = some materials (e.g. 1× Stick). Assign a unique `defId`.
  - On the **tool Def(s)** (e.g. an Axe with `ToolAspect`): set `ToolAspect.brokenFormDefId = the Broken Def's defId`.
  - Optionally add the **Broken Def as the low `tierTable` entry** (small weight) of a tool-crafting recipe — broken
    is just a weighted outcome (b/d2), nothing special-cased.
  - *Why: d1 (salvageable broken item), d2 (a tool names its broken form; crafting can roll broken), d3 (destroy-and-replace uses brokenFormDefId).*

- [x] **Registries — rebuild + reference** *(all)*
  - Rebuild `DefRegistry` (**Rebuild From Def Folder**) and `RecipeRegistry` (**Rebuild From Recipe Folder**) so every
    new Def/RecipeDef is registered. (Both also auto-rebuild before Play.)

- [x] **Save reset — schema 18**
  - Delete existing save files under `Application.persistentDataPath/Saves` (old saves reject loudly at load; schema
    bumped 17 → 18). The `world.json` template already carries `craftCounter: 0`.

### Playtest validation — single client

1. **Hand craft (a).** Dev-add the recipe's materials to inventory. Open **Dev ▸ Craft**, press **Craft** on the hand
   recipe. Watch the craft HUD (⚒ line) and the inventory.

   - [x] ✅ Materials + timePool + energy are consumed **on commit**; after the window the output appears in inventory (a4).

2. **List-iteration (a2).** Repeat with the 5-material variant.

   - [x] ✅ Crafts identically with no code change; all 5 materials consumed, output delivered.

3. **Gates (a3).** Try a tool recipe (`requiredToolCategoryId ≠ 0`) with **no** matching tool carried, then with one.

   - [x] ✅ Rejected without the tool; accepted (and crafts) with it.

4. **Cancel (a).** Start a hand craft, **✕** it partway.

   - [x] ✅ Unelapsed time+energy **and** the materials are refunded; the craft clears.

5. **Tiers PerUnit (b6).** Craft the 20-arrow PerUnit recipe.

   - [x] ✅ The result is a **few per-tier stacks** (not 20 uniques), weighted toward the low tier.

6. **Tiers PerBatch.** Craft the PerBatch variant.

   - [x] ✅ One uniform stack of `outputAmount`.

7. **RNG re-derivation across skip (b4).** Note the recipe. Start a craft, then **Skip Time** across its completion.

   - [x] ✅ The craft completes during/after the skip and yields the **same** tier(s) it would have in real time.

8. **RNG re-derivation across revert (b4).** Bank a checkpoint (Dev ▸ Other ▸ Bank Save) **before** starting a craft,
   complete a craft, note the tier(s), then **revert** to that checkpoint and re-craft.

   - [x] ✅ Revert restores the pre-craft state (materials back, no output). Re-crafting is a **new roll** (new
     startTime → new seed) — the outcome may differ; both are internally consistent.

9. **Station craft (c1).** Dev-spawn the station (Dev ▸ Gather ▸ +Spawn on the station Def) and stand near it. In
   **Dev ▸ Craft**, press **Craft** on the station recipe; walk away; return when done.

   - [x] ✅ Labor accrues on the station; committed time/energy debited; the output appears **at the station** to collect.

10. **Partial / resume (c2).** Start a station craft, **Stop** partway (walk away), then **Craft** it again later.

    - [x] ✅ Progress persisted on the station; resuming continues from where it left off; the unused share was refunded on Stop.

11. **Broken — destroy & replace (d3).** Dev-add a tool with a `brokenFormDefId`. Use it (Inventory ▸ **Use**) until
    durability hits 0.

    - [x] ✅ At 0, the tool **disappears and is replaced in the same slot** by its broken Def (its own name/icon); no
      lingering 0-durability tool.

12. **Salvage (d1).** **Drop** the broken item, then **dismantle** it via its world Interactable (or Dev ▸ Gather).

    - [x] ✅ Dismantling yields the authored materials.

13. **Revert across a break (d4).** Bank a checkpoint with the tool intact, break it (→ broken item), then revert.

    - [x] ✅ Revert **restores the tool**; reverting to *after* the break keeps the broken item — no special-casing.

### MPPM two-client (host + owning client)

1. **Hand craft, both clients.** Each dreamer crafts a hand recipe.

   - [x] ✅ Each sees their own craft progress + output; save/reload preserves an in-flight craft and completes it.

2. **Co-op station craft (c3).** Both dreamers **Craft** the same `coop` station recipe from the start.

   - [x] ✅ It finishes in ~half the labor-time; each is refunded ~half; the output collects at the station.

3. **Single-crafter lock (c3).** With a **non-coop** station recipe in progress by one dreamer, the other tries to assist.

   - [x] ✅ The second dreamer is locked out ("single-crafter"); once the first leaves, the partial is resumable by either.

4. **Determinism across clients (b).** Both observe a completed craft's tier(s).

   - [x] ✅ Identical on both clients (rolled host-side from saved state); a skip/revert re-derives the same result on both.

---

## Build 0.2.9f — Authored nodes + id-baking (follow-on; built after 0.2.10)

> **What changed (code, by Claude Code):** in-scene gatherable nodes now work without dev-spawning —
> the **authored** sync profile + id-baking (the split-out 0.2.7b / 0.2.9e2 work).
> - **`Interactable`** gains an `authored` flag and self-registers with `MapEntitySync`'s static
>   `_authoredById` registry on enable (host + client), keyed by its baked `instanceId`.
> - **`Instance`** gains `authored` + `depleted` (Simulation). An authored node is **materialised into
>   `MapEntityLayer.worldObjects` on FIRST interaction** (`MapEntitySync.ResolveWorldObject` reads its
>   Def + local position from the scene Interactable) — so **pristine authored nodes are never stored
>   and send no traffic** (delta-only, O(damaged)). On depletion it is marked `depleted` and **kept**
>   (not removed like a runtime object), so the delta persists + reverts.
> - **Sync:** `WorldObjectPayload` gains a per-instance `Flags` byte (authored / depleted). The client
>   binds an authored instance to its **scene GameObject** (never spawns a duplicate visual) and hides
>   it when depleted by disabling renderers + colliders (keeping the GameObject active so it stays
>   registered and can be restored on revert). Nodes absent from the payload are shown (pristine/reverted).
> - **Id-baking tool (f2):** **Coremenders ▸ Tools ▸ Bake Authored Node Ids** scans the open scene's
>   `Interactable`s, marks them authored, assigns a unique stable `instanceId` from base **100000**
>   (so authored ids never collide with runtime `nextInstanceId`, which starts at 1), leaves existing
>   ids untouched on re-run, and errors on duplicates.
> - **Save/revert (f3):** authored deltas ride `map_{id}.json` `worldObjects` keyed by baked id — no new
>   collection; the existing clear-and-rehydrate restores them. **Save schema bumped 18 → 19** (Instance
>   gained authored/depleted); template `world.json` updated.
>
> **Known limits (logged in TODO.md):** node **regen** after depletion is deferred (a depleted node
> stays hidden); materialised authored nodes sync their (static) position redundantly; an abandoned
> in-progress node stays a permanent delta until depleted. (Authored **stations** DO materialise on
> first craft — `StartStationCraft` resolves them, so a workbench can be placed in-scene.)

### Wiring / setup

- [x] **Author a gatherable node in the Action scene** *(0.2.9f f1)*
  - Drag a prefab (or create a GameObject) into the Action scene that has: a **mesh/renderer**, a
    **Collider** (any; the detector probes with `QueryTriggerInteraction.Collide`), and an **`Interactable`**
    component with its **`def`** set to a world-action Def (e.g. a harvest-node Def with a `chop`/`gather`
    action, or reuse `Def_Test_Processable` / a tree Def). Place a couple at known spots.
  - *Why: the scene GameObject IS the authored node's visual + interaction surface; the host reads its Def
    + position from this component.*

- [x] **Bake the ids** *(0.2.9f f2)*
  - Run **Coremenders ▸ Tools ▸ Bake Authored Node Ids**. Confirm the Console reports the baked count and
    **no duplicate errors**. Each node's `Interactable.authored` is now true with a stable `instanceId` (≥100000).
  - Re-run it after adding more nodes — existing ids stay put; only new nodes get ids.
  - *Why: the baked id keys the node's delta in the save and binds its visual on clients; stable + identical on all clients.*

- [x] **Save reset — schema 19**
  - Delete existing saves under `Application.persistentDataPath/Saves` (schema bumped 18 → 19; old saves reject).

### Playtest validation — single client

1. **Pristine = silent.** Enter Play with authored nodes placed but untouched. (Optional: watch the world-object
   NV / logs.)

   - [x] ✅ Pristine authored nodes exist + are interactable, but nothing about them is stored/synced (no materialised instance until touched).

2. **Interact → materialise + accrue.** Walk to a node; start its action (chop/gather) via the Interactable HUD.

   - [x] ✅ Progress accrues on the node; a Console line confirms it materialised on first interaction; the Actions HUD lists the world task.

3. **Partial persists across save/reload.** Work a node partway, walk away (✕/stop), Save & reload.

   - [x] ✅ The node reappears **with its in-progress labor** (delta round-tripped through map_{id}.json).

4. **Deplete → hide.** Work a node to completion.

   - [x] ✅ Yields delivered; the scene node hides (renderers + collider off), can't be re-targeted; it stays depleted after reload.

5. **Revert.** Bank a checkpoint before touching a node, chop it (partial or to depletion), then revert.

   - [x] ✅ Revert restores the node to its as-of-checkpoint state (pristine/partial shown again; a depleted node comes back).

### MPPM two-client

1. **Identical ids + state.** Both clients see the same authored nodes; one client chops, the other observes.

   - [x] ✅ Progress + depletion appear on both clients (delta synced); ids are identical (baked, not per-client).

2. **Co-op on an authored node.** Both work the same node (if its action is `coop`).

   - [x] ✅ Faster completion; both see the node deplete + hide; save/revert-clean.

---

## Build FP — First-person camera & movement (Presentation build)

> *(The TDD section is unnumbered — "First-person camera & movement (Presentation build)", placed after
> 0.2.10. Logged here as "Build FP" so it does not squat on a Cluster 2 build number.)*

> **What changed (code, by Claude Code):** the static scene camera + world-space movement are replaced
> by an owner-gated first-person rig. Presentation-layer, plus two small Networking touches; no
> simulation, save, or schema change — **no save reset needed**.
>
> - **`FirstPersonRig`** (new, Presentation, `NetworkBehaviour`) — **the single ownership check** (a1).
>   In `OnNetworkSpawn` it enables/disables `FirstPersonLook`, `DreamerMovementController` and
>   `SelfBodyVisibility` from one `IsOwner`, then acquires the camera. A remote dreamer gets no camera
>   and no input — it is a plain replicated body on the existing owner-auth NetworkTransform.
>   In `LateUpdate` (after `CharacterController.Move` has resolved) the camera **position-follows the
>   head placeholder** and takes its **rotation from mouselook** (a3). The camera is never parented —
>   that is the anti-stutter pattern. Camera acquisition: instantiate `_cameraPrefab` if assigned (and
>   deactivate the old scene camera), otherwise **take over `Camera.main`**, detaching it from any
>   parent. It carries `[RequireComponent]` on the other three components, so adding the rig adds them all.
> - **`FirstPersonLook`** (new, Presentation) — mouselook, cursor mode, free-look. Yaw rotates the
>   **body**; pitch is held here and applied to the camera by the rig (b1), clamped at ±85°.
>   **Middle-mouse toggles free-look** (d1): mouse X then drives a camera-only yaw offset clamped to
>   ±90°, the body keeps its heading, and toggling off `SmoothDamp`s the offset back to body-forward
>   (d3). Runs at execution order **-100** so the motor sees this frame's heading.
> - **`UiFocus`** (new, Networking) — a tiny static claim registry: "a panel currently wants the mouse".
>   `FirstPersonLook` is the **only** writer of `Cursor.lockState`/`visible`, so nothing can fight over
>   it (b2). Gameplay = locked + hidden + mouselook live; any claim = free cursor + look suspended (no
>   delta accumulated, so the view does not jump on close). Claims added to: **inventory**
>   (`DreamerInventorySync`, I), **pause menu** (`PauseMenuController`, Esc), the new **action menu**
>   (`InteractableDetector`, E), the **rescue overlay** (`DreamFlowManager`) and the **Wake Up vote**
>   (`ConsensusVoteComponent`). Destroyed claimants are pruned on read, so a panel can never strand the
>   cursor. **Tab toggles a manual free-cursor claim** for the always-on dev HUD (stopgap — in TODO.md).
> - **`DreamerMovementController`** (rewritten in place — same file and class name, so the prefab
>   reference is unchanged) — movement is now **body-relative** (`transform.right/forward`, which
>   mouselook yaws), which is also what makes free-look movement work for free (d2). **Jump** added
>   (c2): Space / gamepad South, `v = sqrt(2·h·−g)` into the existing gravity integration,
>   grounded-gated. Gait tiers (Alt walk / default run / Shift sprint) are unchanged so the 0.2.3e
>   drain thresholds still apply.
> - **`SelfBodyVisibility`** (new, Presentation) — the owner's own renderers switch to
>   `ShadowCastingMode.ShadowsOnly` (f1 + f2): no visible self-mesh (which also kills inside-the-head
>   near-plane clipping), shadow preserved. Applied **only on the owning client's copy**, so remote
>   clients still see a full body. *Deviation from the TDD's "layer + culling mask": in URP an object
>   outside a camera's culling mask is dropped from that camera's shadow pass too, which would take
>   f2's shadow with it. Logged in TODO.md.*
> - **`InteractableDetector`** (Networking, retargeted) — the **proximity query is gone** (e1).
>   Targeting is a **center-screen cast** from the camera capped at `_interactRange`: a forgiving
>   `SphereCast` of `_aimAssistRadius` by default, narrowing to `_precisionRadius` while **Left Ctrl**
>   is held (e3). Disambiguation is **angular** — among everything the sphere sweeps up, the
>   Interactable **nearest to the crosshair** wins, not the nearest in depth (e2). The action machinery
>   behind it is untouched (same `DispatchWorldActionServerRpc` / `StopWorldActionServerRpc`). Because
>   gameplay locks the cursor, the action list is now a menu you **open with E** (which claims
>   `UiFocus`); closed, it shows an `[E] <name>` look-at prompt. While the menu is open the target is
>   **frozen** so the buttons cannot change under the cursor. A `+` crosshair draws while locked.
> - **`WorldClockDriver`** (Networking, c3) — movement drain now measures **horizontal (XZ)
>   displacement only**. Jump/fall are vertical and must not bill as travel ("jump is free"), and gait
>   bucketing on a slope should reflect ground speed. The seam is otherwise untouched: the host still
>   samples the transform result through the RDM registry, not the driver.

### Wiring / setup

- [x] **Head placeholder on the dreamer prefab** *(FP a2)*
  - Open the dreamer prefab. Add an empty child GameObject named **`Head`** at local position
    `(0, 1.65, 0)` — adjust to eye height for the capsule, roughly `CharacterController.height − 0.15`.
    Leave its rotation identity; it marks position only.
  - *Why: the camera position-follows this transform every LateUpdate. It is deliberately not the
    camera's parent — the camera takes its rotation from mouselook, not from the placeholder.*

- [x] **Add `FirstPersonRig` to the dreamer prefab** *(FP a1/a3)*
  - Add Component → **First Person Rig**. `[RequireComponent]` pulls in **First Person Look** and
    **Self Body Visibility** automatically (`DreamerMovementController` is already on the prefab).
  - On the rig: assign **Head Target** = the `Head` child from the step above. Leave **Camera Prefab**
    empty unless you want a dedicated camera prefab (optional step below).
  - Leave the other three components **enabled** in the Inspector — the rig turns them off for
    non-owners at spawn; that is the one ownership check.
  - *Why: one component add wires the whole rig, and the rig is the single `IsOwner` gate for camera,
    input, free-look, jump and self-body culling.*

- [x] **Retire the static scene camera** *(FP a1)*
  - In the **Action** scene: make sure there is exactly **one** camera tagged **MainCamera** carrying
    the **AudioListener**, and **remove any follow / look-at / static-framing script** on it. If it is
    parented to something that is fine — the rig detaches it at spawn and re-parents it on despawn.
  - Confirm no other active GameObject in the scene has an AudioListener.
  - *Why: with no Camera Prefab assigned the rig takes over `Camera.main` and drives it itself; a
    leftover follow script would fight the rig's LateUpdate.*

- [x] *(Optional)* **Dedicated camera prefab**
  - If you prefer a purpose-built FP camera (post-processing volume, custom FOV / near clip): make a
    prefab with a `Camera` **tagged MainCamera** + `AudioListener`, near clip ≈ `0.05`, and assign it to
    the rig's **Camera Prefab**. The rig instantiates it unparented for the owner and deactivates the
    scene camera.
  - *Why: `InteractableDetector` resolves its cast camera via `Camera.main`, so the tag matters.*

- [x] **NetworkTransform — replicate body yaw and jump height** *(FP c1/c2)*
  - On the dreamer prefab's **NetworkTransform** (owner-authoritative): confirm **Sync Rotation Y** is
    ticked (body yaw is now player-driven and must replicate) and **Sync Position Y** is ticked (the
    jump arc). Leave rotation X/Z unsynced. Interpolate on.
  - *Why: before this build the body never turned, so a missing Sync Rotation Y would not have shown up.*

- [x] **`InteractableDetector` probe values on the dreamer prefab** *(FP e1/e2/e3)*
  - **Interact Layer**: set to the layer(s) your Interactables live on. Leaving it `Everything` still
    works (non-Interactable hits are filtered out) but costs more per frame.
  - **Aim Assist Radius** — *this is the e2 "default radius: Manual" value*: start at **0.4**; raise
    toward 0.5–0.6 if small ground items feel fiddly, lower toward 0.25 if you keep catching the wrong
    object.
  - **Precision Radius** `0.02` (near-hairline), **Precision Key** `LeftControl`, **Action Menu Key**
    `E`, **Interact Range** `3.5`.
  - Turn **Show Probe Debug** off once targeting is confirmed working.
  - *Why: the probe is the whole of interaction now — the proximity fallback is gone, so these values
    are the difference between "picks up easily" and "cannot hit anything".*

- [x] **Gait speeds vs movement-drain thresholds** *(FP c3)*
  - On the dreamer prefab's `DreamerMovementController` and the `WorldClockDriver`'s **Movement
    Config**, confirm the bands still straddle correctly: walk `2.5` < `runSpeedThreshold 3.5` <
    run `5` < `sprintSpeedThreshold 6.5` < sprint `8`.
  - Set **Jump Height** (default `1.1` m) to taste.
  - *Why: drain is bucketed by measured speed, so changing a gait speed silently re-buckets its drain.*

- [x] *(Optional)* **Self-body renderer list**
  - `SelfBodyVisibility` auto-collects every `Renderer` under the dreamer when **Body Renderers** is
    left empty. Once real character meshes exist, populate it explicitly so held items and any future
    first-person arms are excluded from the shadow-only switch.
  - *Why: auto-collect is right for the capsule placeholder, wrong once you want something visible in
    your own hands.*

### Playtest validation — single client

1. **Camera + mouselook.** Press Play. Look around with the mouse; push the view to the top and bottom
   extremes.

   - [x] ✅ You see through a head-height camera, cursor hidden and locked; yaw turns the body, pitch
     tilts the view only, and pitch stops cleanly at both vertical extremes (no over-rotation, no roll).

   - [x] ✅ The view is smooth while walking — no per-frame stutter or jitter against the controller.

2. **Movement + jump.** Walk with WASD in several directions, then turn 90° and walk again. Hold Alt
   (walk), then Shift (sprint). Press Space, standing and while moving.

   - [x] ✅ Movement is body-relative — "forward" always means where you are looking, at every heading.

   - [x] ✅ Jump arcs and lands, cannot be repeated in mid-air, and all three gaits feel distinct.

3. **Movement-drain seam (0.2.3e).** With non-zero `MovementConfig` rates, watch the time/energy HUD
   while running a long straight line, then while standing still and jumping repeatedly.

   - [x] ✅ Running still debits time/energy at the same rate as before this build.

   - [x] ✅ Jumping on the spot debits **nothing** — drain is horizontal-only, so jump is free.

4. **Cursor mode split.** Press **I** (inventory), then **Esc** (pause menu), closing each. Then press
   **Tab**.

   - [x] ✅ Opening either frees and shows the cursor and freezes the view; the buttons are clickable;
     closing re-locks and hides the cursor and look resumes **without the view jumping**.

   - [x] ✅ Tab toggles a free cursor for the always-on dev HUD, and back.

5. **Free-look.** Face a landmark. Press **middle mouse**, then walk forward while sweeping the mouse
   fully left and right. Press middle mouse again.

   - [x] ✅ The view sweeps but the body does not turn — you keep walking your original heading.

   - [x] ✅ The sweep stops at roughly 90° each side (a 180° arc); no owl-spin past the clamp.

   - [x] ✅ Toggling off returns the view to body-forward.

6. **Center-screen interaction.** Stand near several Interactables. Look directly at one, look away,
   then look at a small item lying between two others.

   - [x] ✅ Looking at an object surfaces `[E] <name>`; looking away drops it. Standing right next to an
     object you are **not** looking at surfaces nothing — proximity targeting is gone.

   - [x] ✅ With two objects in view, the one nearest the **crosshair** is targeted, not the nearer one
     off to the side.

7. **Precision mode.** Aim at a cluster of small items with the default sphere, then hold **Left Ctrl**
   and sweep slowly across them.

   - [x] ✅ The default sphere grabs a target forgivingly; holding precision isolates one item at a time
     and lets you single out the small one; releasing restores the forgiving sphere.

8. **Action menu.** Look at a gatherable and press **E**. Run an action, then press **E** again.

   - [x] ✅ E frees the cursor and opens the action list; the buttons dispatch exactly as before
     (progress %, Stop, yields all unchanged); the target stays frozen while the menu is open; E closes
     it and re-locks the cursor.

9. **Self-body.** Look straight down at your feet and around your torso, then find a directional light
   and check the ground beside you.

   - [x] ✅ You see no part of your own body from any angle, and never the inside of the head mesh.

   - [x] ✅ You still cast a normal shadow on the ground.

### MPPM two-client

1. **Owner gating.** Enable one Virtual Player and press Play. On each client, watch the *other*
   dreamer move.

   - [x] ✅ Each client controls only its own dreamer; the remote dreamer is a **full visible body** with
     **no camera**, and neither client's input moves the other.

   - [x] ✅ Only the local body is invisible-to-self — the remote body is never shadow-only.

2. **Replication.** On client A: walk, turn on the spot, sprint and jump while client B watches.

   - [x] ✅ Position, **body yaw** and the jump arc all replicate to the other client smoothly.

   - [x] ✅ Free-look on A produces **no** visible turn on B (camera-only yaw).

3. **Interaction on both clients.** Each client targets and works a different node; then both target
   the same node.

   - [x] ✅ Each client's center-screen cast resolves independently; actions dispatch and progress on
     both; co-op accrual behaves as in 0.2.9f.

4. **Cursor + modal panels.** Trigger a dream-flow event — run one dreamer to incapacitation so the
   **rescue overlay** appears, and on Wake Up the **consensus vote** panel.

   - [x] ✅ Both panels free the cursor on the client that sees them and are clickable; dismissing them
     re-locks the cursor and resumes mouselook.

5. **Save / revert clean.** Take a checkpoint, move both dreamers somewhere else, then Revert.

   - [x] ✅ Both dreamers snap to their checkpoint positions (`SnapToPositionRpc`) with the camera
     following correctly and no lingering look or velocity artefacts; movement resumes normally.

---

## Setup — Git & Archives

Infrastructure task, not a gameplay build. Phases 1–7 of `Docs/Setup-Git-Migration.md`
were executed by Claude Code; everything below needs the Unity Editor GUI or
machine-local configuration, so it falls to you.

- [ ] **Push the initial commit.** The commit exists locally (`main`, 464 files,
  "Initial commit — Unity 6 project, Cluster 2 in progress") but `git push` failed
  authentication — Git Credential Manager holds no valid github.com credential and
  cannot prompt from a non-interactive session.
  - Open a normal terminal at the project root.
  - `gh auth login` (the GitHub CLI 2.97 is installed) — choose GitHub.com, HTTPS,
    authenticate in the browser. Or clear the stale github.com entry in Windows
    Credential Manager and let GCM's browser prompt fire on the next push.
  - `git push -u origin main`
  - *Why:* the repo is initialised and verified but nothing is on GitHub yet.

- [ ] **Confirm the push landed.** Open `https://github.com/coremndrs/coremenders-primal`.
  - `Assets/`, `Packages/`, `ProjectSettings/`, `Docs/`, `.claude/skills/` and
    `CLAUDE.md` are all present.
  - `Library/`, `Logs/`, `UserSettings/`, `obj/`, `.vs/` and every `.csproj`/`.sln`
    are absent.
  - `.claude/settings.local.json` is absent (machine-local, deliberately ignored).
  - *Why:* the only cheap moment to catch a mis-scoped ignore rule is before history
    accumulates on top of it.

- [ ] **Verify Asset Serialization.** Unity → Edit → Project Settings → Editor.
  - *Asset Serialization Mode* must be **Force Text**.
  - *Version Control Mode* must be **Visible Meta Files**.
  - *Why:* binary serialization turns every scene and prefab into an unmergeable
    blob. `EditorSettings.asset` currently reads `m_SerializationMode: 2` (Force
    Text) and metas are visible on disk, so this is a confirmation, not a change —
    but confirm it in the GUI rather than assume.

- [ ] **Reopen the project and check the Console is clean.**
  - `Assets/Documentation/`, `Assets/CLAUDE.md` and `Assets/code-review-skill/` were
    removed in Phase 1, along with their `.meta` files.
  - Expect **no** "missing script", "meta file exists but its asset can't be found",
    or broken-reference warnings.
  - Six previously-empty folders (`Game/Art/Models`, `Game/Art/Textures`,
    `Game/Audio`, `Game/Data/Buffs`, `Plugins`, `StreamingAssets/Templates/NewGame`)
    now hold a `.gitkeep`. Unity ignores dot-files, so none should appear in the
    Project window and no new `.meta` should be generated — confirm that.
  - *Why:* a stale meta or a regenerated GUID would show up here first, and it is
    much cheaper to fix before the folder GUIDs are baked into history.

- [ ] **Configure UnityYAMLMerge** so the `merge=unityyamlmerge` attributes in
  `.gitattributes` actually resolve to a driver. Run at the project root — the
  installed editor is **6000.0.47f1**, so the path below is already correct:
  ```
  git config merge.unityyamlmerge.name "Unity SmartMerge"
  git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.0.47f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
  git config merge.unityyamlmerge.recursive binary
  ```
  - Verify: `git config --get merge.unityyamlmerge.driver` echoes the path, and the
    `.exe` exists at it.
  - *Why:* without the driver, a scene or prefab conflict falls back to a plain text
    merge and will corrupt the YAML. This is local config — it is not versioned, so
    every machine that clones the repo must repeat it.

- [ ] **Install restic and initialise the primary archive repo** per
  `Docs/VersionControl.md`.
  - Store the restic password in a password manager — **not** in the project folder,
    and not only inside the backup it protects.
  - *Why:* heavy binaries are gitignored by design, so a `git clone` alone does not
    yield a runnable project. The archive is the other half of the recovery story.

- [ ] **Initialise the second, off-site archive destination.** One local, one remote.
  - *Why:* a single archive on the same machine as the project is not a backup.

- [ ] **Run the first full restore test.** Restore to a scratch folder, open it in
  Unity, and work through the four-point verification checklist in
  `Docs/VersionControl.md`.
  - *Why:* an untested restore is an assumption. Do it now, while the project is
    small and a failed restore costs minutes.

- [ ] **Delete the Phase 0 manual copy** at `f:\Latest\Coremenders - Primal Frost - Copy`
  once the push has landed, the Console is clean and the restore test has passed.
  - *Why:* it is a pre-git snapshot with no history; leaving it around invites
    editing the wrong copy.

### Notes on what was already done for you

- `.git/hooks/pre-commit` is installed and executable — it blocks any commit
  containing a file over 10 MB. Bypass deliberately with `git commit --no-verify`.
  The hook is local-only and not versioned, so it must be recreated on any other
  machine that clones the repo (script is in `Docs/Setup-Git-Migration.md`).
- **Git LFS is not used and must not be introduced.** Verify with
  `git ls-files -z | xargs -0 git check-attr filter | grep "filter: lfs"` — empty
  output is correct. Do **not** verify with `git lfs ls-files`: any `git lfs`
  command writes an `[lfs]` marker into `.git/config`.
