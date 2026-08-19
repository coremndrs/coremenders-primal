using Game.Networking;
using Game.Simulation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Minimal authoring tool for generic action "verbs" (TDD §1.12): a verb is just
    /// actionId + outcome + context, shared across items (one "Chop" reused everywhere).
    ///
    /// Per-item parameters — labor, yields, costs/rewards, tools, prerequisites — are NOT set here.
    /// They live on each item Def's Actions list (<see cref="ItemAction"/>) in the Inspector, so the
    /// same verb costs/gives different things on different items.
    ///
    /// Editor-only. Menu: Coremenders ▸ Tools ▸ Action Verb Creator.
    /// </summary>
    public class ActionDefCreatorWindow : EditorWindow
    {
        private const string DefaultFolder = "Assets/Game/Data/ActionDefs";

        [MenuItem("Coremenders/Tools/Action Verb Creator")]
        public static void Open() => GetWindow<ActionDefCreatorWindow>("Action Verb Creator");

        private string        _actionId   = "chop";
        private ActionOutcome _outcome    = ActionOutcome.Transform;
        private ActionContext _context    = ActionContext.World;
        private string        _saveFolder = DefaultFolder;
        private bool          _overwrite;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Create a generic action verb", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "A verb is shared across items (one 'Chop' reused everywhere). Per-item labor, yields, " +
                "costs/rewards and tools are authored on each item Def's Actions list (ItemAction), not here.",
                MessageType.Info);

            _actionId   = EditorGUILayout.TextField("actionId", _actionId);
            _outcome    = (ActionOutcome)EditorGUILayout.EnumPopup("outcome", _outcome);
            _context    = (ActionContext)EditorGUILayout.EnumPopup("context", _context);
            _saveFolder = EditorGUILayout.TextField("Save folder", _saveFolder);

            string assetName = "ActionDef_" + Capitalize(_actionId);
            string path      = $"{_saveFolder.TrimEnd('/')}/{assetName}.asset";
            bool   exists    = AssetDatabase.LoadAssetAtPath<ActionDef>(path) != null;

            if (exists)
            {
                EditorGUILayout.HelpBox($"An asset already exists at {path}.", MessageType.Warning);
                _overwrite = EditorGUILayout.ToggleLeft("Overwrite existing asset", _overwrite);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_actionId) || (exists && !_overwrite)))
            {
                if (GUILayout.Button(exists ? "Overwrite Verb" : "Create Verb", GUILayout.Height(28)))
                    Create(path, exists);
            }
        }

        private void Create(string path, bool exists)
        {
            EnsureFolder(_saveFolder);
            var asset = exists
                ? AssetDatabase.LoadAssetAtPath<ActionDef>(path)
                : ScriptableObject.CreateInstance<ActionDef>();

            asset.actionId = _actionId;
            asset.outcome  = _outcome;
            asset.context  = _context;

            if (exists) EditorUtility.SetDirty(asset);
            else        AssetDatabase.CreateAsset(asset, path);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
            Debug.Log($"[ActionVerbCreator] {(exists ? "Updated" : "Created")} {path}");
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

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
