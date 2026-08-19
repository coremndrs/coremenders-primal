using System.Collections.Generic;
using Game.Networking;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Auto-assigns a unique id to any newly-imported <see cref="Def"/> / <see cref="RecipeDef"/> whose
    /// id is still unassigned (≤ 0) — so authoring a new asset never requires hand-picking a number, and
    /// two assets created in the same batch get distinct ids. Existing assets (id > 0) are never touched,
    /// keeping ids stable (they cross the wire and live in saves). Use
    /// <c>Coremenders ▸ Tools ▸ Assign Missing Ids</c> for a bulk pass or to surface duplicates.
    /// </summary>
    public class DefIdPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            // Guard against re-entrancy: assigning + SetDirty can trigger another import pass.
            if (_busy) return;

            // An imported asset needs an id when it is unassigned (≤ 0) OR its id collides with a
            // DIFFERENT existing asset — the latter catches Ctrl+D duplicates, which copy the id.
            var newDefs    = new List<Def>();
            var newRecipes = new List<RecipeDef>();
            foreach (var path in imported)
            {
                if (!path.EndsWith(".asset")) continue;
                var def = AssetDatabase.LoadAssetAtPath<Def>(path);
                if (def != null) { if (def.defId <= 0 || DefIdTools.DefIdInUse(def.defId, def)) newDefs.Add(def); continue; }
                var recipe = AssetDatabase.LoadAssetAtPath<RecipeDef>(path);
                if (recipe != null && (recipe.recipeId <= 0 || DefIdTools.RecipeIdInUse(recipe.recipeId, recipe)))
                    newRecipes.Add(recipe);
            }
            if (newDefs.Count == 0 && newRecipes.Count == 0) return;

            _busy = true;
            try
            {
                var usedDefIds = new HashSet<int>();
                foreach (var d in newDefs)
                {
                    int id = DefIdTools.NextFreeDefId(usedDefIds);
                    d.defId = id;
                    usedDefIds.Add(id);
                    EditorUtility.SetDirty(d);
                    Debug.Log($"[DefIdTools] Auto-assigned defId {id} → new Def '{d.name}'.", d);
                }

                var usedRecipeIds = new HashSet<int>();
                foreach (var r in newRecipes)
                {
                    int id = DefIdTools.NextFreeRecipeId(usedRecipeIds);
                    r.recipeId = id;
                    usedRecipeIds.Add(id);
                    EditorUtility.SetDirty(r);
                    Debug.Log($"[DefIdTools] Auto-assigned recipeId {id} → new Recipe '{r.name}'.", r);
                }

                AssetDatabase.SaveAssets();
            }
            finally { _busy = false; }
        }

        private static bool _busy;
    }
}
