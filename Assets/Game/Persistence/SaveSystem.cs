using System;
using System.Collections.Generic;
using System.IO;
using Game.Simulation;
using UnityEngine;

namespace Game.Persistence
{
    /// <summary>
    /// Unified save/load system (TDD §1.8, Builds 0.0.3 / 0.0.5).
    ///
    /// One serializer, one target: disk. Disk saves and checkpoints use identical format;
    /// a checkpoint is a save differentiated only by its slot name and retention rules.
    /// All writes are atomic (temp → rename) so a crash mid-write cannot corrupt a checkpoint.
    ///
    /// File layout: persistentDataPath/Saves/{slot}/
    ///   world.json          — global non-positional state (clock, isGameOver, mapId)
    ///   dreamer_0.json      — per-dreamer authoritative state
    ///   dreamer_1.json
    ///   map_{mapId}.json    — map entity layer (ground items, harvest nodes, …) — added 0.2.7a
    ///
    /// D2: writes current RDM state to disk.
    /// D4: reads any save directory (template or real save) into world + map layer.
    /// D6: rejects files with a mismatched schema version immediately.
    /// F1: checkpoint slot distinct from the save slot; same writer.
    ///
    /// SaveSystem.Save — world-only copy trap (CLAUDE.md invariant):
    /// Every field on WorldState must be explicitly listed in the worldOnly constructor.
    /// Adding a field to WorldState without adding it here causes silent data loss on save.
    /// </summary>
    public static class SaveSystem
    {
        public const int    CurrentSchemaVersion = 19; // bumped at 0.2.9f: authored nodes — Instance.authored/depleted (delta records materialised into map_{id}.json worldObjects, keyed by baked id)
        public const string DefaultSlot          = "slot_0";
        public const string CheckpointSlot       = "checkpoint"; // legacy single-slot (kept for reference; use named checkpoints)

        private static readonly ISaveSerializer Serializer = NewtonsoftSerializer.Instance;

        // ── Save (D2) ────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes world state and the map entity layer to the save slot directory.
        /// world.json holds global non-positional state; map_{mapId}.json holds the
        /// entity layer; dreamer files hold per-dreamer state.
        /// </summary>
        public static void Save(WorldState world, MapEntityLayer mapLayer, string slot = DefaultSlot)
        {
            var dir = GetSaveDir(slot);
            Directory.CreateDirectory(dir);

            // worldOnly: every field on WorldState must be listed here — see CLAUDE.md invariant.
            var worldOnly = new WorldState
            {
                schemaVersion = world.schemaVersion,
                mapId         = world.mapId,
                clock         = world.clock,
                isGameOver    = world.isGameOver,
                craftCounter  = world.craftCounter,
            };
            WriteFile(dir, "world.json", worldOnly);

            for (int i = 0; i < world.dreamers.Length; i++)
                WriteFile(dir, $"dreamer_{i}.json", world.dreamers[i]);

            WriteFile(dir, $"map_{world.mapId}.json", mapLayer);

            Debug.Log($"[SaveSystem] Saved slot '{slot}' → {dir}");
        }

        // ── Load (D4 + D6) ───────────────────────────────────────────────────────

        /// <summary>
        /// Reads a save directory (or the StreamingAssets template directory) into a
        /// fully-populated WorldState + MapEntityLayer. Throws if the schema version is unexpected.
        /// </summary>
        public static (WorldState world, MapEntityLayer mapLayer) LoadFromDirectory(string dirPath)
        {
            var world = ReadFile<WorldState>(dirPath, "world.json");

            if (world.schemaVersion != CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"[SaveSystem] Schema mismatch — expected v{CurrentSchemaVersion}, got v{world.schemaVersion}. File: {dirPath}");

            world.dreamers    = new DreamerRecord[2];
            world.dreamers[0] = ReadFile<DreamerRecord>(dirPath, "dreamer_0.json");
            world.dreamers[1] = ReadFile<DreamerRecord>(dirPath, "dreamer_1.json");

            var mapLayer = ReadFile<MapEntityLayer>(dirPath, $"map_{world.mapId}.json");

            Debug.Log($"[SaveSystem] Loaded from {dirPath}");
            return (world, mapLayer);
        }

        public static (WorldState, MapEntityLayer) LoadSlot(string slot = DefaultSlot) =>
            LoadFromDirectory(GetSaveDir(slot));

        /// <summary>Loads the authored template from StreamingAssets.</summary>
        public static (WorldState, MapEntityLayer) LoadTemplate() =>
            LoadFromDirectory(Path.Combine(Application.streamingAssetsPath, "Templates"));

        // ── Helpers ──────────────────────────────────────────────────────────────

        public static bool SlotExists(string slot = DefaultSlot) =>
            File.Exists(Path.Combine(GetSaveDir(slot), "world.json"));

        public static string GetSaveDir(string slot = DefaultSlot) =>
            Path.Combine(Application.persistentDataPath, "Saves", slot);

        // ── Named checkpoint store (0.1.5b) ─────────────────────────────────────
        // Each checkpoint lives in its own slot directory "checkpoint_{id}" alongside
        // a meta.json sidecar. Multiple checkpoints coexist (First Dream, consumables,
        // sleeps) and are deleted when no live protection buff references them.

        public static string GetCheckpointDir(string id) =>
            GetSaveDir($"checkpoint_{id}");

        /// <summary>
        /// Writes the world state and map layer to a named checkpoint directory and stamps a metadata sidecar.
        /// The Save() writer is reused: the checkpoint directory IS the slot directory.
        /// </summary>
        public static void TakeNamedCheckpoint(WorldState world, MapEntityLayer mapLayer, string id, string coverageLabel)
        {
            string slot = $"checkpoint_{id}";
            Save(world, mapLayer, slot);

            var meta = new CheckpointMeta
            {
                id                      = id,
                timestampUtc            = DateTime.UtcNow.Ticks,
                coverageLabel           = coverageLabel,
                capturedAtInGameMinutes = world.clock?.totalInGameMinutes ?? 0f,
            };
            WriteFile(GetSaveDir(slot), "meta.json", meta);
            Debug.Log($"[SaveSystem] Checkpoint '{id}' ({coverageLabel}) → {GetSaveDir(slot)}");
        }

        public static (WorldState, MapEntityLayer) LoadNamedCheckpoint(string id) =>
            LoadFromDirectory(GetCheckpointDir(id));

        public static bool NamedCheckpointExists(string id) =>
            File.Exists(Path.Combine(GetCheckpointDir(id), "world.json"));

        /// <summary>Reads only the metadata sidecar for a named checkpoint (no schema check).</summary>
        public static CheckpointMeta LoadCheckpointMeta(string id) =>
            ReadFile<CheckpointMeta>(GetCheckpointDir(id), "meta.json");

        /// <summary>Enumerates all checkpoint metadata entries currently on disk.</summary>
        public static IEnumerable<CheckpointMeta> ListCheckpoints()
        {
            var savesDir = Path.Combine(Application.persistentDataPath, "Saves");
            if (!Directory.Exists(savesDir)) yield break;

            foreach (var dir in Directory.EnumerateDirectories(savesDir))
            {
                if (!Path.GetFileName(dir).StartsWith("checkpoint_")) continue;
                var metaPath = Path.Combine(dir, "meta.json");
                if (File.Exists(metaPath))
                    yield return ReadFile<CheckpointMeta>(dir, "meta.json");
            }
        }

        public static void DeleteCheckpoint(string id)
        {
            var dir = GetCheckpointDir(id);
            if (!Directory.Exists(dir)) return;
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Could not delete checkpoint '{id}' — {ex.Message}. Continuing.");
            }
        }

        /// <summary>
        /// Deletes all checkpoint files whose capturedAtInGameMinutes is strictly greater than
        /// <paramref name="targetMinutes"/>. Called during revert to prune the future timeline
        /// (TDD §0.1.7d c3/c4 — "discard the future so revert is truly destructive").
        /// Deletion failures are logged but do not abort the revert.
        /// </summary>
        public static void DiscardCheckpointsNewerThan(float targetMinutes)
        {
            var toDelete = new List<string>();
            try
            {
                foreach (var meta in ListCheckpoints())
                    if (meta.capturedAtInGameMinutes > targetMinutes)
                        toDelete.Add(meta.id);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Could not enumerate checkpoints for discard — {ex.Message}.");
            }

            foreach (var id in toDelete)
                DeleteCheckpoint(id); // already guarded internally

            if (toDelete.Count > 0)
                Debug.Log($"[SaveSystem] Discarded {toDelete.Count} checkpoint(s) newer than {targetMinutes:F1} min.");
        }

        private static void WriteFile<T>(string dir, string filename, T obj) =>
            AtomicWrite(Path.Combine(dir, filename), Serializer.Serialize(obj));

        // Atomic write: write to a temp file then rename so a crash cannot leave a partial file.
        // On Windows, File.Replace(src, dest, null) calls DeleteFile internally, which can fail
        // when any process holds a brief lock (Defender, indexer). Providing a .bak path makes
        // it use MoveFileEx for both operations — rename-over-rename, no delete required.
        private static void AtomicWrite(string path, string content)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content);
            if (File.Exists(path))
            {
                var bak = path + ".bak";
                File.Replace(tmp, path, bak);
                try { File.Delete(bak); } catch { } // best-effort cleanup; ignore if locked
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        private static T ReadFile<T>(string dir, string filename) =>
            Serializer.Deserialize<T>(File.ReadAllText(Path.Combine(dir, filename)));
    }
}
