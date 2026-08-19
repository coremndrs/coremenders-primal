using System.Collections.Generic;
using Game.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Id-baking utility for authored in-scene nodes (TDD 0.2.9f, f2). Scans the open scene(s) for
    /// <see cref="Interactable"/> components, marks each as authored, and bakes a unique stable
    /// <c>instanceId</c> into any that lack one — leaving existing ids untouched so a re-run only fills
    /// new nodes. Warns on duplicate ids. The baked id keys the node's delta record in map_{id}.json
    /// and binds the scene visual on clients.
    ///
    /// Authored ids start at <see cref="AuthoredIdBase"/> (100000) so they never collide with runtime
    /// instance ids (allocated from MapEntityLayer.nextInstanceId, which starts at 1).
    /// </summary>
    public static class NodeIdBaker
    {
        public const int AuthoredIdBase = 100000;

        [MenuItem("Coremenders/Tools/Bake Authored Node Ids")]
        public static void BakeAuthoredNodeIds()
        {
            var interactables = Object.FindObjectsByType<Interactable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (interactables.Length == 0)
            {
                Debug.Log("[NodeIdBaker] No Interactable nodes found in the open scene(s).");
                return;
            }

            // Reserve ids already baked, so a re-run leaves them untouched.
            var used = new HashSet<int>();
            foreach (var it in interactables)
                if (it.instanceId > 0) used.Add(it.instanceId);

            int next   = AuthoredIdBase;
            int baked   = 0, marked = 0;
            var dirtyScenes = new HashSet<UnityEngine.SceneManagement.Scene>();

            foreach (var it in interactables)
            {
                var  so      = new SerializedObject(it);
                var  idProp  = so.FindProperty("instanceId");
                var  authProp = so.FindProperty("authored");
                bool changed = false;

                if (!authProp.boolValue) { authProp.boolValue = true; marked++; changed = true; }

                if (idProp.intValue <= 0)
                {
                    while (used.Contains(next)) next++;
                    idProp.intValue = next;
                    used.Add(next);
                    next++;
                    baked++;
                    changed = true;
                    if (it.def == null)
                        Debug.LogWarning($"[NodeIdBaker] '{it.name}' has no Def assigned — it will not resolve at runtime.", it);
                }

                if (changed)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(it);
                    dirtyScenes.Add(it.gameObject.scene);
                }
            }

            // Duplicate report (across everything, including pre-baked ids).
            var byId = new Dictionary<int, Interactable>();
            foreach (var it in interactables)
            {
                if (it.instanceId <= 0) continue;
                if (byId.TryGetValue(it.instanceId, out var first))
                    Debug.LogError($"[NodeIdBaker] Duplicate authored id {it.instanceId}: '{it.name}' collides with '{first.name}'. " +
                                   "Ids must be unique — clear one's instanceId (set 0) and re-bake.", it);
                else
                    byId[it.instanceId] = it;
            }

            foreach (var scene in dirtyScenes) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[NodeIdBaker] Baked {baked} new id(s), marked {marked} as authored, across {interactables.Length} Interactable(s). Save the scene to persist.");
        }
    }
}
