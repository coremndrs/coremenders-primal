using System.Collections.Generic;
using System.Text;
using Game.Networking;
using Game.Simulation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-click authoring tool that gives EVERY inventory-capable Def a <c>pickup</c> action binding
    /// (TDD §1.12): all items pick up through the one Interactable path, differing only by their extra
    /// actions. Under the generic-verb model, "pickup" is a single shared verb (<c>ActionDef_Pickup</c>,
    /// ToInventory) and each item carries an <see cref="ItemAction"/> that binds it — there is no
    /// per-item pickup asset.
    ///
    /// Creates the pickup verb if missing and, for each Def with an <see cref="InventoryAspect"/>,
    /// removes any existing pickup binding and prepends a fresh one. World-only Defs (no InventoryAspect
    /// — Fallen Tree, Branch Pile) are left untouched. Idempotent — safe to re-run.
    ///
    /// Editor-only. Menu: Coremenders ▸ Tools ▸ Unify Item Pickup Actions.
    /// </summary>
    public static class PickupActionAssigner
    {
        private const string ActionFolder    = "Assets/Game/Data/ActionDefs";
        private const string PickupAssetPath = ActionFolder + "/ActionDef_Pickup.asset";

        [MenuItem("Coremenders/Tools/Unify Item Pickup Actions")]
        public static void Assign()
        {
            var pickup = EnsurePickupDef();
            var report = new StringBuilder("[PickupActionAssigner] Unified pickup action.\n");

            int scanned = 0, changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Def"))
            {
                var def = AssetDatabase.LoadAssetAtPath<Def>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || !def.HasAspect<InventoryAspect>()) continue;
                scanned++;

                var actions = new List<ItemAction> { new ItemAction { action = pickup } };
                if (def.actions != null)
                    foreach (var ia in def.actions)
                        if (ia != null && ia.actionId != "pickup")
                            actions.Add(ia);

                var newArr = actions.ToArray();
                if (SameActions(def.actions, newArr)) continue;

                def.actions = newArr;
                EditorUtility.SetDirty(def);
                changed++;
                report.AppendLine($"  {def.displayName} (defId {def.defId}) → [{Join(newArr)}]");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.AppendLine($"  Scanned {scanned} inventory Def(s); updated {changed}. " +
                              "REMINDER: no prefab wiring needed — MapEntitySync adds an Interactable + " +
                              "trigger collider to any spawned visual that lacks them.");
            Debug.Log(report.ToString());
        }

        /// <summary>Create-or-load the single shared pickup verb (instant ToInventory, world context).</summary>
        public static ActionDef EnsurePickupDef()
        {
            var asset   = AssetDatabase.LoadAssetAtPath<ActionDef>(PickupAssetPath);
            bool created = asset == null;
            if (created) asset = ScriptableObject.CreateInstance<ActionDef>();

            asset.actionId = "pickup";
            asset.outcome  = ActionOutcome.ToInventory;
            asset.context  = ActionContext.World;

            if (created)
            {
                EnsureFolder(ActionFolder);
                AssetDatabase.CreateAsset(asset, PickupAssetPath);
            }
            else EditorUtility.SetDirty(asset);
            return asset;
        }

        private static bool SameActions(ItemAction[] a, ItemAction[] b)
        {
            if (a == null) return b == null || b.Length == 0;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                // Compare by bound verb + label; a freshly-built pickup binding is a new object, so
                // reference equality would always differ — match on identity that actually matters.
                if (a[i]?.action != b[i]?.action) return false;
                if (a[i]?.labelOverride != b[i]?.labelOverride) return false;
            }
            return true;
        }

        private static string Join(ItemAction[] arr)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < arr.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(arr[i] != null ? arr[i].actionId : "null");
            }
            return sb.ToString();
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            var path  = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{path}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }
    }
}
