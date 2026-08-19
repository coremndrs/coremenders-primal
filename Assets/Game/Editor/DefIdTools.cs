using System.Collections.Generic;
using System.Linq;
using Game.Networking;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Automatic id assignment for <see cref="Def"/> (defId) and <see cref="RecipeDef"/> (recipeId).
    ///
    /// Ids share one numeric namespace and must be UNIQUE and STABLE — they cross the wire and live in
    /// saves, so a valid id is never renumbered. These tools only ever FILL an unassigned id (≤ 0) with
    /// the next free value (max existing + 1, mirroring TreeChainBuilder.NextWorldId), and REPORT any
    /// duplicates for the author to resolve. New assets get an id automatically on import
    /// (see <see cref="DefIdPostprocessor"/>); the menu command below is the bulk / repair path.
    /// </summary>
    public static class DefIdTools
    {
        // ── Asset scans (project-wide, so nothing collides regardless of folder) ──────

        public static List<Def> AllDefs() =>
            AssetDatabase.FindAssets("t:Def")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Def>)
                .Where(d => d != null)
                .ToList();

        public static List<RecipeDef> AllRecipes() =>
            AssetDatabase.FindAssets("t:RecipeDef")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<RecipeDef>)
                .Where(r => r != null)
                .ToList();

        // ── Next free id ──────────────────────────────────────────────────────────────

        /// <summary>Smallest id > max(existing valid ids), min 1. Skips <paramref name="alsoUsed"/>
        /// (ids already handed out this batch but not yet written back).</summary>
        public static int NextFreeDefId(HashSet<int> alsoUsed = null)
            => NextFree(AllDefs().Select(d => d.defId), alsoUsed);

        public static int NextFreeRecipeId(HashSet<int> alsoUsed = null)
            => NextFree(AllRecipes().Select(r => r.recipeId), alsoUsed);

        private static int NextFree(IEnumerable<int> existing, HashSet<int> alsoUsed)
        {
            int max = 0;
            foreach (var id in existing) if (id > max) max = id;
            if (alsoUsed != null) foreach (var id in alsoUsed) if (id > max) max = id;
            return max + 1;
        }

        // ── Bulk assign + duplicate report (menu) ─────────────────────────────────────

        [MenuItem("Coremenders/Tools/Assign Missing Ids (Defs + Recipes)")]
        public static void AssignMissingIdsMenu()
        {
            int defs    = AssignMissingDefIds();
            int recipes = AssignMissingRecipeIds();
            AssetDatabase.SaveAssets();

            int dupD = ReportDuplicates(AllDefs().Select(d => (d.name, d.defId)),    "Def",    "defId");
            int dupR = ReportDuplicates(AllRecipes().Select(r => (r.name, r.recipeId)), "Recipe", "recipeId");

            Debug.Log($"[DefIdTools] Assigned {defs} missing Def id(s) + {recipes} missing Recipe id(s). " +
                      $"Duplicates — Def: {dupD}, Recipe: {dupR} (see errors above; fix duplicates manually so ids stay stable).");
        }

        public static int AssignMissingDefIds()
        {
            var defs = AllDefs();
            var used = new HashSet<int>(defs.Where(d => d.defId > 0).Select(d => d.defId));
            int count = 0;
            foreach (var d in defs.Where(d => d.defId <= 0))
            {
                int id = NextFree(used, null);
                Undo.RecordObject(d, "Assign Def Id");
                d.defId = id;
                used.Add(id);
                EditorUtility.SetDirty(d);
                count++;
                Debug.Log($"[DefIdTools] Assigned defId {id} → '{d.name}'.", d);
            }
            return count;
        }

        public static int AssignMissingRecipeIds()
        {
            var recipes = AllRecipes();
            var used = new HashSet<int>(recipes.Where(r => r.recipeId > 0).Select(r => r.recipeId));
            int count = 0;
            foreach (var r in recipes.Where(r => r.recipeId <= 0))
            {
                int id = NextFree(used, null);
                Undo.RecordObject(r, "Assign Recipe Id");
                r.recipeId = id;
                used.Add(id);
                EditorUtility.SetDirty(r);
                count++;
                Debug.Log($"[DefIdTools] Assigned recipeId {id} → '{r.name}'.", r);
            }
            return count;
        }

        private static int ReportDuplicates(IEnumerable<(string name, int id)> items, string kind, string field)
        {
            var byId = new Dictionary<int, string>();
            int dupes = 0;
            foreach (var (name, id) in items)
            {
                if (id <= 0) continue;
                if (byId.TryGetValue(id, out var first))
                {
                    Debug.LogError($"[DefIdTools] Duplicate {kind} {field} {id}: '{name}' collides with '{first}'. " +
                                   "Ids must be unique — change one manually (do NOT renumber one that is already saved/referenced).");
                    dupes++;
                }
                else byId[id] = name;
            }
            return dupes;
        }

        /// <summary>True if another Def/Recipe already uses <paramref name="id"/> (excluding <paramref name="self"/>).</summary>
        public static bool DefIdInUse(int id, Object self)
            => id > 0 && AllDefs().Any(d => d.defId == id && d != self);

        public static bool RecipeIdInUse(int id, Object self)
            => id > 0 && AllRecipes().Any(r => r.recipeId == id && r != self);
    }
}
