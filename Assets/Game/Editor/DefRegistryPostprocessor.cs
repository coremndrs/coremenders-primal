using Game.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Keeps every DefRegistry in the open scene(s) in sync with the Def asset folder.
    ///
    /// Fires when a Def asset is imported, deleted, or moved, and again just before
    /// entering Play Mode — so a Def authored and immediately playtested is always present.
    /// </summary>
    public class DefRegistryPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (!TouchesDefFolder(imported) && !TouchesDefFolder(deleted) &&
                !TouchesDefFolder(movedTo) && !TouchesDefFolder(movedFrom)) return;

            RebuildOpenRegistries(log: true);
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) RebuildOpenRegistries(log: false);
        };

        private static bool TouchesDefFolder(string[] paths)
        {
            foreach (var p in paths)
                if (p.StartsWith(DefRegistryEditor.DefFolder) && p.EndsWith(".asset")) return true;
            return false;
        }

        private static void RebuildOpenRegistries(bool log)
        {
            var registries = Object.FindObjectsByType<DefRegistry>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var registry in registries)
            {
                if (DefRegistryEditor.Rebuild(registry, log))
                    EditorSceneManager.MarkSceneDirty(registry.gameObject.scene);
            }
        }
    }
}
