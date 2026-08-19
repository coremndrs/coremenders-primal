using System.Collections.Generic;
using System.Text;
using Game.Networking;
using Game.Simulation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-click authoring tool that builds the tree-chain world-object Defs and wires their
    /// actions under the generic-verb model (TDD §1.12): a small palette of shared verb ActionDefs
    /// (Pickup / Split / Process / Gather) plus per-item <see cref="ItemAction"/> bindings on each
    /// Def carrying THAT item's labor / yields / costs-rewards / tool. There are no per-item
    /// ActionDef assets — the old ones are deleted on run.
    ///
    /// Chain produced:
    ///   Fallen Tree ──process(axe)──▶ transforms into Large Tree Log + Branch Pile
    ///   Large Tree Log ──pickup(instant)──▶ inventory   ──split(axe)──▶ 3× Medium Log
    ///   Branch Pile ──gather sticks──▶ 5× Stick        ──gather firewood(axe)──▶ 3× Firewood
    ///   Test Processable ──test action──▶ 1× Stone
    ///
    /// Every item/world cross-reference resolves by Def displayName, so it always wires this
    /// project's real ids. Idempotent — safe to re-run.
    ///
    /// Editor-only. Menu: Coremenders ▸ Tools ▸ Build Tree-Chain Defs + ActionDefs.
    /// </summary>
    public static class TreeChainBuilder
    {
        private const string DefFolder    = "Assets/Game/Data/Defs";
        private const string ActionFolder = "Assets/Game/Data/ActionDefs";
        private const int    AxeCategory  = 1;

        [MenuItem("Coremenders/Tools/Build Tree-Chain Defs + ActionDefs")]
        public static void Build()
        {
            EnsureFolder(DefFolder);
            EnsureFolder(ActionFolder);

            var report = new StringBuilder();
            var byName = LoadDefsByName();
            int nextWorldId = NextWorldId(byName.Values);

            // 1. World-object Defs (create-or-update; actions wired in step 4). ─────────
            var fallenTree = EnsureWorldDef(byName, "Fallen Tree",      inventory: false, ref nextWorldId, report);
            var largeLog   = EnsureWorldDef(byName, "Large Tree Log",   inventory: true,  ref nextWorldId, report);
            var branchPile = EnsureWorldDef(byName, "Branch Pile",      inventory: false, ref nextWorldId, report);
            var testProc   = EnsureWorldDef(byName, "Test Processable", inventory: false, ref nextWorldId, report);

            // 2. Resolve item yields by displayName. ────────────────────────────────────
            int mediumLog = ItemId(byName, "Medium Log", report);
            int stick     = ItemId(byName, "Stick",      report);
            int firewood  = ItemId(byName, "Firewood",   report);
            int stone     = ItemId(byName, "Stone",      report);

            // 3. Generic verbs — shared across items (id + outcome + context only). ───────
            var pickup  = PickupActionAssigner.EnsurePickupDef();                             // ToInventory
            var split   = EnsureVerb("ActionDef_Split",   "split",   ActionOutcome.Transform);
            var process = EnsureVerb("ActionDef_Process", "process", ActionOutcome.Transform);
            var gather  = EnsureVerb("ActionDef_Gather",  "gather",  ActionOutcome.ToInventory);

            // 4. Per-item ItemAction bindings — this is where each item's costs/rewards/yields live.
            SetActions(fallenTree.def,
                new ItemAction
                {
                    action                 = process,
                    timeRequired          = 10f,
                    coop                   = true,
                    onDepletion            = DepletionBehavior.Transform,
                    depletionSpawns        = new[] { WorldYield(largeLog.defId), WorldYield(branchPile.defId) },
                    allowedToolCategoryIds = new[] { AxeCategory },
                    toolRequired           = true,
                    effects                = WorkCost(10f),
                });

            SetActions(largeLog.def,
                new ItemAction { action = pickup },   // instant ToInventory (verb); no labor/effects
                new ItemAction
                {
                    action                 = split,
                    timeRequired          = 8f,
                    coop                   = true,
                    yields                 = new[] { ItemYield(mediumLog, 3f) },
                    onDepletion            = DepletionBehavior.Transform,
                    allowedToolCategoryIds = new[] { AxeCategory },
                    toolRequired           = true,
                    effects                = WorkCost(8f),
                });

            SetActions(branchPile.def,
                new ItemAction
                {
                    action        = gather,
                    labelOverride = "gather sticks",
                    timeRequired = 3f,
                    coop          = true,
                    yields        = new[] { ItemYield(stick, 5f) },
                    onDepletion   = DepletionBehavior.Remove,
                    effects       = WorkCost(3f),
                },
                new ItemAction
                {
                    action                 = gather,
                    labelOverride          = "gather firewood",
                    timeRequired          = 5f,
                    coop                   = true,
                    yields                 = new[] { ItemYield(firewood, 3f) },
                    onDepletion            = DepletionBehavior.Remove,
                    allowedToolCategoryIds = new[] { AxeCategory },
                    toolRequired           = true,
                    effects                = WorkCost(5f),
                });

            SetActions(testProc.def,
                new ItemAction
                {
                    action        = process,
                    labelOverride = "test action",
                    timeRequired = 5f,
                    coop          = true,
                    yields        = new[] { ItemYield(stone, 1f) },
                    onDepletion   = DepletionBehavior.Remove,
                    effects       = WorkCost(5f),
                });

            foreach (var wd in new[] { fallenTree, largeLog, branchPile, testProc })
                EditorUtility.SetDirty(wd.def);

            // 5. Delete the superseded per-item ActionDef assets from the old model.
            foreach (var legacy in new[]
                     {
                         "ActionDef_FallenTree_Process", "ActionDef_LargeTreeLog_Split",
                         "ActionDef_LargeTreeLog_Pickup", "ActionDef_BranchPile_GatherSticks",
                         "ActionDef_BranchPile_GatherFirewood", "ActionDef_TestProcessable_Test",
                     })
                DeleteLegacyActionDef(legacy, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Insert(0,
                $"[TreeChainBuilder] Built tree chain (generic verbs + per-item ItemAction bindings).\n" +
                $"  Verbs: pickup, split, process, gather.\n" +
                $"  Fallen Tree      defId {fallenTree.defId}  actions[process]\n" +
                $"  Large Tree Log   defId {largeLog.defId}  actions[pickup, split]\n" +
                $"  Branch Pile      defId {branchPile.defId}  actions[gather sticks, gather firewood]\n" +
                $"  Test Processable defId {testProc.defId}  actions[test action]\n" +
                $"  Item yields: Medium Log={mediumLog}, Stick={stick}, Firewood={firewood}, Stone={stone}\n");
            report.AppendLine(
                "REMINDER: add any newly-created Defs to the DefRegistry `_defs` list in the Action scene, " +
                "and set each world Def's `prefab`. No Interactable/collider wiring needed — MapEntitySync " +
                "adds them at spawn. Per-item costs/rewards are on each Def's actions[] (ItemAction.effects).");
            Debug.Log(report.ToString());
        }

        // ── Per-minute action economy (placeholder; tune per action in the Def Inspector) ──

        /// <summary>
        /// Placeholder up-front energy cost for a timed action, scaled by its time. Time cost itself is
        /// the ItemAction.timeRequired (→ timePool), so it is NOT listed here. Tune per action in the
        /// Def Inspector. Signed total: negative drains, positive rewards.
        /// </summary>
        private static ActionEffect[] WorkCost(float timeRequired, float energyPerMinute = 2f) => new[]
        {
            new ActionEffect { stat = ResourceStat.Energy, amount = -energyPerMinute * timeRequired },
        };

        // ── World Def create-or-update ──────────────────────────────────────────────

        private struct WorldDef { public Def def; public int defId; }

        private static WorldDef EnsureWorldDef(Dictionary<string, Def> byName, string displayName,
                                               bool inventory, ref int nextId, StringBuilder report)
        {
            byName.TryGetValue(displayName, out var def);
            bool created = def == null;

            if (created)
            {
                def = ScriptableObject.CreateInstance<Def>();
                def.defId       = nextId++;
                def.displayName = displayName;
                string path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{DefFolder}/Def_{Sanitize(displayName)}.asset");
                AssetDatabase.CreateAsset(def, path);
                byName[displayName] = def;
                report.AppendLine($"  CREATED Def '{displayName}' defId {def.defId} → {path}");
            }
            else
            {
                report.AppendLine($"  UPDATED Def '{displayName}' defId {def.defId} (kept id + prefab)");
            }

            // Rebuild aspects: LargeTreeLog is inventory-capable (so pickup can flip it into a
            // container); the others are world-only.
            def.aspects = inventory
                ? new List<DefAspect> { new InventoryAspect
                    { weight = 5f, stackable = false, maxStack = 1, itemTypeTag = ItemTypeTag.General } }
                : new List<DefAspect>();

            EditorUtility.SetDirty(def);
            return new WorldDef { def = def, defId = def.defId };
        }

        private static void SetActions(Def def, params ItemAction[] actions) => def.actions = actions;

        // ── Generic verb create-or-update ───────────────────────────────────────────

        private static ActionDef EnsureVerb(string assetName, string actionId, ActionOutcome outcome,
                                            ActionContext context = ActionContext.World)
        {
            string path    = $"{ActionFolder}/{assetName}.asset";
            var    asset   = AssetDatabase.LoadAssetAtPath<ActionDef>(path);
            bool   created = asset == null;
            if (created) asset = ScriptableObject.CreateInstance<ActionDef>();

            asset.actionId = actionId;
            asset.outcome  = outcome;
            asset.context  = context;

            if (created) AssetDatabase.CreateAsset(asset, path);
            else         EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void DeleteLegacyActionDef(string assetName, StringBuilder report)
        {
            string path = $"{ActionFolder}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<ActionDef>(path) == null) return;
            AssetDatabase.DeleteAsset(path);
            report.AppendLine($"  Deleted legacy {assetName} (superseded by generic verb + ItemAction).");
        }

        private static ActionYield ItemYield(int itemDefId, float amount) => new ActionYield
        {
            isWorldObject = false, itemDefId = itemDefId, worldObjectDefId = 0, amount = amount,
        };

        private static ActionYield WorldYield(int worldObjectDefId) => new ActionYield
        {
            isWorldObject = true, itemDefId = 0, worldObjectDefId = worldObjectDefId, amount = 1f,
        };

        // ── Lookups ─────────────────────────────────────────────────────────────────

        private static Dictionary<string, Def> LoadDefsByName()
        {
            var map = new Dictionary<string, Def>();
            foreach (var guid in AssetDatabase.FindAssets("t:Def"))
            {
                var def = AssetDatabase.LoadAssetAtPath<Def>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || string.IsNullOrEmpty(def.displayName)) continue;
                if (map.ContainsKey(def.displayName))
                    Debug.LogWarning($"[TreeChainBuilder] Duplicate Def displayName '{def.displayName}' — first wins.");
                else
                    map[def.displayName] = def;
            }
            return map;
        }

        private static int ItemId(Dictionary<string, Def> byName, string displayName, StringBuilder report)
        {
            if (byName.TryGetValue(displayName, out var def)) return def.defId;
            report.AppendLine($"  ⚠ WARNING: no Def named '{displayName}' — its yield id left 0 (fix the yield after authoring it).");
            Debug.LogWarning($"[TreeChainBuilder] No Def named '{displayName}'; yield id left 0.");
            return 0;
        }

        private static int NextWorldId(IEnumerable<Def> defs)
        {
            int max = 1000;
            foreach (var d in defs) if (d.defId > max) max = d.defId;
            return max + 1;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString().Trim('_');
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            var path = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{path}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }
    }
}
