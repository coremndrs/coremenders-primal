using System.Collections.Generic;
using System.Linq;
using Game.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="RecipeRegistry"/> that auto-populates the _recipes list
    /// from every RecipeDef asset under <see cref="RecipeFolder"/> — mirrors DefRegistryEditor.
    /// The registry is derived data: rebuild it, don't hand-curate it.
    /// </summary>
    [CustomEditor(typeof(RecipeRegistry))]
    [CanEditMultipleObjects]
    public class RecipeRegistryEditor : UnityEditor.Editor
    {
        public const string RecipeFolder = "Assets/Game/Data/Recipes";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"_recipes is auto-populated from {RecipeFolder} (sorted by recipeId). " +
                "Manual edits are overwritten on rebuild.", MessageType.Info);

            if (GUILayout.Button("Rebuild From Recipe Folder"))
                foreach (var t in targets)
                    Rebuild(t as RecipeRegistry, log: true);
        }

        /// <summary>
        /// Loads every RecipeDef under the recipe folder, sorts by recipeId, and writes the result
        /// into the registry's serialized _recipes array. Returns true if the array changed.
        /// </summary>
        public static bool Rebuild(RecipeRegistry registry, bool log)
        {
            if (registry == null) return false;

            var recipes = AssetDatabase.FindAssets("t:RecipeDef", new[] { RecipeFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<RecipeDef>)
                .Where(r => r != null)
                .OrderBy(r => r.recipeId)
                .ToArray();

            WarnOnDuplicateIds(recipes);

            var so   = new SerializedObject(registry);
            var prop = so.FindProperty("_recipes");

            bool changed = prop.arraySize != recipes.Length;
            if (!changed)
                for (int i = 0; i < recipes.Length; i++)
                    if (prop.GetArrayElementAtIndex(i).objectReferenceValue as RecipeDef != recipes[i])
                    { changed = true; break; }
            if (!changed) return false;

            prop.arraySize = recipes.Length;
            for (int i = 0; i < recipes.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(registry);

            if (log) Debug.Log($"[RecipeRegistry] Rebuilt with {recipes.Length} recipe(s) from {RecipeFolder}.", registry);
            return true;
        }

        private static void WarnOnDuplicateIds(IReadOnlyList<RecipeDef> recipes)
        {
            var seen = new Dictionary<int, RecipeDef>();
            foreach (var r in recipes)
            {
                if (seen.TryGetValue(r.recipeId, out var first))
                    Debug.LogError(
                        $"[RecipeRegistry] Duplicate recipeId {r.recipeId}: '{r.name}' collides with '{first.name}'.", r);
                else
                    seen[r.recipeId] = r;
            }
        }
    }

    /// <summary>
    /// Keeps every RecipeRegistry in the open scene(s) in sync with the recipe asset folder —
    /// on import/delete/move and just before entering Play Mode (mirrors DefRegistryPostprocessor).
    /// </summary>
    public class RecipeRegistryPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (!Touches(imported) && !Touches(deleted) && !Touches(movedTo) && !Touches(movedFrom)) return;
            RebuildOpen(log: true);
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) RebuildOpen(log: false);
        };

        private static bool Touches(string[] paths)
        {
            foreach (var p in paths)
                if (p.StartsWith(RecipeRegistryEditor.RecipeFolder) && p.EndsWith(".asset")) return true;
            return false;
        }

        private static void RebuildOpen(bool log)
        {
            var registries = Object.FindObjectsByType<RecipeRegistry>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var registry in registries)
                if (RecipeRegistryEditor.Rebuild(registry, log))
                    EditorSceneManager.MarkSceneDirty(registry.gameObject.scene);
        }
    }
}
