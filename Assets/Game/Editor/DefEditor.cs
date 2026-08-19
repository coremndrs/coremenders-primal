using System;
using System.Linq;
using Game.Networking;
using Game.Simulation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="Def"/> that makes the [SerializeReference] aspects list
    /// authorable. Unity's default drawer for a managed-reference list adds a null "element 0"
    /// with no reliable way to pick the concrete type — this adds an "Add Aspect ▾" dropdown that
    /// appends a typed <see cref="DefAspect"/> (via managedReferenceValue, so Undo + prefab overrides
    /// work), listing each concrete aspect once (a Def holds at most one of each — see Def.GetAspect).
    ///
    /// The rest of the Def (defId, displayName, prefab, actions, and each aspect's own fields once
    /// typed) is drawn by the default inspector.
    /// </summary>
    [CustomEditor(typeof(Def))]
    [CanEditMultipleObjects]
    public class DefEditor : UnityEditor.Editor
    {
        private static Type[] _aspectTypes;

        private static Type[] AspectTypes() =>
            _aspectTypes ??= TypeCache.GetTypesDerivedFrom<DefAspect>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .OrderBy(t => t.Name)
                .ToArray();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            // ── Id status (auto-assigned on create; this is the repair affordance) ──
            if (targets.Length == 1)
            {
                var def = (Def)target;
                if (def.defId <= 0)
                {
                    EditorGUILayout.HelpBox("defId is unassigned. New assets get an id automatically; " +
                        "click to assign one now.", MessageType.Warning);
                    if (GUILayout.Button("Assign unique defId"))
                    {
                        Undo.RecordObject(def, "Assign Def Id");
                        def.defId = DefIdTools.NextFreeDefId();
                        EditorUtility.SetDirty(def);
                        AssetDatabase.SaveAssets();
                    }
                }
                else if (DefIdTools.DefIdInUse(def.defId, def))
                {
                    EditorGUILayout.HelpBox($"defId {def.defId} is DUPLICATED by another Def. Ids must be " +
                        "unique. Reassign THIS one only if it is the newer/duplicate asset (never renumber " +
                        "an id already referenced in saves).", MessageType.Error);
                    if (GUILayout.Button("Reassign this defId to a free value"))
                    {
                        Undo.RecordObject(def, "Reassign Def Id");
                        def.defId = DefIdTools.NextFreeDefId();
                        EditorUtility.SetDirty(def);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            EditorGUILayout.Space();

            var aspects = serializedObject.FindProperty("aspects");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Aspect ▾", GUILayout.Height(22)))
                    ShowAddAspectMenu(aspects);

                using (new EditorGUI.DisabledScope(!HasNullAspect(aspects)))
                    if (GUILayout.Button("Clear empty entries", GUILayout.Width(140), GUILayout.Height(22)))
                        RemoveNullAspects(aspects);
            }

            EditorGUILayout.HelpBox(
                "Each aspect type may appear at most once. Add via the dropdown (not the list's + button, " +
                "which cannot pick a type). Fields for each aspect edit inline above.", MessageType.None);
        }

        private void ShowAddAspectMenu(SerializedProperty aspects)
        {
            var menu = new GenericMenu();
            foreach (var type in AspectTypes())
            {
                var t = type; // capture
                if (AspectPresent(aspects, t))
                    menu.AddDisabledItem(new GUIContent($"{t.Name}  (already added)"));
                else
                    menu.AddItem(new GUIContent(t.Name), false, () => AddAspect(aspects, t));
            }
            if (AspectTypes().Length == 0)
                menu.AddDisabledItem(new GUIContent("(no DefAspect subtypes found)"));
            menu.ShowAsContext();
        }

        private void AddAspect(SerializedProperty aspects, Type t)
        {
            serializedObject.Update();
            int idx = aspects.arraySize;
            aspects.InsertArrayElementAtIndex(idx);
            aspects.GetArrayElementAtIndex(idx).managedReferenceValue = Activator.CreateInstance(t);
            serializedObject.ApplyModifiedProperties();
        }

        private static bool AspectPresent(SerializedProperty aspects, Type t)
        {
            for (int i = 0; i < aspects.arraySize; i++)
            {
                var e = aspects.GetArrayElementAtIndex(i);
                if (e.managedReferenceValue != null && e.managedReferenceValue.GetType() == t) return true;
            }
            return false;
        }

        private static bool HasNullAspect(SerializedProperty aspects)
        {
            for (int i = 0; i < aspects.arraySize; i++)
                if (aspects.GetArrayElementAtIndex(i).managedReferenceValue == null) return true;
            return false;
        }

        private void RemoveNullAspects(SerializedProperty aspects)
        {
            serializedObject.Update();
            for (int i = aspects.arraySize - 1; i >= 0; i--)
                if (aspects.GetArrayElementAtIndex(i).managedReferenceValue == null)
                    aspects.DeleteArrayElementAtIndex(i);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
