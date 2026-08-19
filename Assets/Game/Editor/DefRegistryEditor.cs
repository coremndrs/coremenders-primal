using System.Collections.Generic;
using System.Linq;
using Game.Networking;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="DefRegistry"/> that auto-populates the _defs list
    /// from every Def asset under <see cref="DefFolder"/>.
    ///
    /// Rationale: hand-maintaining the Inspector list means a newly authored Def silently
    /// fails to resolve at runtime. The registry is derived data — it should be rebuilt,
    /// not curated.
    ///
    /// Rebuild happens: on the button below, on scene save, and whenever a Def asset is
    /// added/deleted/moved while a scene containing a DefRegistry is open
    /// (see DefRegistryPostprocessor).
    /// </summary>
    [CustomEditor(typeof(DefRegistry))]
    [CanEditMultipleObjects]
    public class DefRegistryEditor : UnityEditor.Editor
    {
        public const string DefFolder = "Assets/Game/Data/Defs";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"_defs is auto-populated from {DefFolder} (sorted by defId). " +
                "Manual edits are overwritten on rebuild.", MessageType.Info);

            if (GUILayout.Button("Rebuild From Def Folder"))
            {
                foreach (var t in targets)
                    Rebuild(t as DefRegistry, log: true);
            }
        }

        /// <summary>
        /// Loads every Def asset under the def folder, sorts by defId, and writes the result
        /// into the registry's serialized _defs array. Returns true if the array changed.
        /// </summary>
        public static bool Rebuild(DefRegistry registry, bool log)
        {
            if (registry == null) return false;

            var defs = AssetDatabase.FindAssets("t:Def", new[] { DefFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Def>)
                .Where(d => d != null)
                .OrderBy(d => d.defId)
                .ToArray();

            WarnOnDuplicateIds(defs);

            var so   = new SerializedObject(registry);
            var prop = so.FindProperty("_defs");

            bool changed = prop.arraySize != defs.Length;
            if (!changed)
            {
                for (int i = 0; i < defs.Length; i++)
                {
                    if (prop.GetArrayElementAtIndex(i).objectReferenceValue as Def != defs[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed) return false;

            prop.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(registry);

            if (log) Debug.Log($"[DefRegistry] Rebuilt with {defs.Length} def(s) from {DefFolder}.", registry);
            return true;
        }

        private static void WarnOnDuplicateIds(IReadOnlyList<Def> defs)
        {
            var seen = new Dictionary<int, Def>();
            foreach (var def in defs)
            {
                if (seen.TryGetValue(def.defId, out var first))
                    Debug.LogError(
                        $"[DefRegistry] Duplicate defId {def.defId}: '{def.name}' collides with '{first.name}'. " +
                        "defIds share one namespace across all Defs — fix before playing.", def);
                else
                    seen[def.defId] = def;
            }
        }
    }
}
