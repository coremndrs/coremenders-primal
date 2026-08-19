using Game.Networking;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="RecipeDef"/> — surfaces recipeId status (unassigned / duplicate)
    /// with a one-click fix, mirroring <see cref="DefEditor"/>. New recipes get an id automatically on
    /// import (<see cref="DefIdPostprocessor"/>); this is the repair affordance. Ids are stable once
    /// assigned (they identify the craft record in saves), so a valid id is never auto-renumbered.
    /// </summary>
    [CustomEditor(typeof(RecipeDef))]
    public class RecipeDefEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length != 1) return;
            var recipe = (RecipeDef)target;

            if (recipe.recipeId <= 0)
            {
                EditorGUILayout.HelpBox("recipeId is unassigned. New recipes get an id automatically; " +
                    "click to assign one now.", MessageType.Warning);
                if (GUILayout.Button("Assign unique recipeId"))
                {
                    Undo.RecordObject(recipe, "Assign Recipe Id");
                    recipe.recipeId = DefIdTools.NextFreeRecipeId();
                    EditorUtility.SetDirty(recipe);
                    AssetDatabase.SaveAssets();
                }
            }
            else if (DefIdTools.RecipeIdInUse(recipe.recipeId, recipe))
            {
                EditorGUILayout.HelpBox($"recipeId {recipe.recipeId} is DUPLICATED by another recipe. " +
                    "Ids must be unique. Reassign THIS one only if it is the newer/duplicate asset.",
                    MessageType.Error);
                if (GUILayout.Button("Reassign this recipeId to a free value"))
                {
                    Undo.RecordObject(recipe, "Reassign Recipe Id");
                    recipe.recipeId = DefIdTools.NextFreeRecipeId();
                    EditorUtility.SetDirty(recipe);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
