using System;
using System.Collections.Generic;
using System.Linq;
using Game.Networking;
using Game.Simulation;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Game.Editor
{
    // ── Shared picker GUI ─────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a searchable Def picker for an <c>int</c> defId property: a popup-style button showing
    /// "displayName (#id)" that opens an <see cref="AdvancedDropdown"/> (with built-in search); the
    /// chosen Def's id is written back. Reused by <see cref="DefIdDrawer"/> (attributed int fields) and
    /// <see cref="ToolAspectDrawer"/> (the Simulation-layer brokenFormDefId).
    /// </summary>
    public static class DefIdPickerGUI
    {
        // id → displayName, rebuilt lazily and cleared whenever the project changes (a Def added /
        // renamed / deleted). Avoids scanning the AssetDatabase every repaint per field.
        private static Dictionary<int, string> _labels;

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.projectChanged += () => _labels = null;

        private static Dictionary<int, string> Labels()
        {
            if (_labels != null) return _labels;
            _labels = new Dictionary<int, string>();
            foreach (var d in DefIdTools.AllDefs())
                if (d.defId > 0) _labels[d.defId] = d.displayName;
            return _labels;
        }

        private static string LabelForId(int id)
        {
            if (id <= 0) return "(none)";
            return Labels().TryGetValue(id, out var name) ? $"{name}  (#{id})" : $"#{id}  (MISSING)";
        }

        public static void Draw(Rect position, SerializedProperty intProp, GUIContent label, DefIdFilter filter)
        {
            if (intProp.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.PropertyField(position, intProp, label);
                return;
            }

            var fieldRect = EditorGUI.PrefixLabel(position, label);
            int id        = intProp.intValue;

            // Capture object + path (not the SerializedProperty) — the dropdown callback fires later,
            // by which time the property reference can be stale.
            var    so   = intProp.serializedObject;
            string path = intProp.propertyPath;

            if (GUI.Button(fieldRect, new GUIContent(LabelForId(id)), EditorStyles.popup))
            {
                var dropdown = new DefAdvancedDropdown(new AdvancedDropdownState(), filter, chosenId =>
                {
                    so.Update();
                    var p = so.FindProperty(path);
                    if (p != null) { p.intValue = chosenId; so.ApplyModifiedProperties(); }
                    _labels = null; // a freshly authored Def may not be cached yet
                });
                dropdown.Show(fieldRect);
            }
        }
    }

    // ── Searchable dropdown ───────────────────────────────────────────────────────

    /// <summary>Searchable list of Defs (filtered) for the picker. AdvancedDropdown supplies the
    /// search field for free; selection invokes <c>onPick</c> with the chosen defId (0 = none).</summary>
    public class DefAdvancedDropdown : AdvancedDropdown
    {
        private readonly DefIdFilter _filter;
        private readonly Action<int> _onPick;
        private readonly Dictionary<int, int> _defIdByItem = new Dictionary<int, int>(); // dropdown item id → defId

        public DefAdvancedDropdown(AdvancedDropdownState state, DefIdFilter filter, Action<int> onPick)
            : base(state)
        {
            _filter = filter;
            _onPick = onPick;
            minimumSize = new Vector2(260f, 340f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Defs");

            var none = new AdvancedDropdownItem("(none)");
            root.AddChild(none);
            _defIdByItem[none.id] = 0;

            foreach (var def in DefIdTools.AllDefs().Where(Matches).OrderBy(d => d.displayName))
            {
                var item = new AdvancedDropdownItem($"{def.displayName}  (#{def.defId})");
                root.AddChild(item);
                _defIdByItem[item.id] = def.defId;
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (_defIdByItem.TryGetValue(item.id, out var defId)) _onPick?.Invoke(defId);
        }

        private bool Matches(Def d)
        {
            switch (_filter)
            {
                case DefIdFilter.Item:        return d.HasAspect<InventoryAspect>();
                case DefIdFilter.WorldObject: return d.HasWorldActions();
                default:                      return true;
            }
        }
    }

    // ── Attribute drawer (int fields in Networking/Presentation) ─────────────────

    [CustomPropertyDrawer(typeof(DefIdAttribute))]
    public class DefIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (DefIdAttribute)attribute;
            DefIdPickerGUI.Draw(position, property, label, attr.filter);
        }
    }

    // ── ToolAspect drawer (covers the Simulation-layer brokenFormDefId) ──────────

    /// <summary>
    /// Draws <see cref="ToolAspect"/> with the Def picker on <c>brokenFormDefId</c>. ToolAspect lives in
    /// the engine-free Simulation assembly, so its field cannot carry the <see cref="DefIdAttribute"/> —
    /// this drawer supplies the same picker. Applied automatically to the SerializeReference aspect
    /// element inside a Def's aspects list.
    /// </summary>
    [CustomPropertyDrawer(typeof(ToolAspect))]
    public class ToolAspectDrawer : PropertyDrawer
    {
        private const float Pad = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var cat    = property.FindPropertyRelative("toolCategoryId");
            var isDur  = property.FindPropertyRelative("isDurabilityTool");
            var maxDur = property.FindPropertyRelative("maxDurability");
            var broken = property.FindPropertyRelative("brokenFormDefId");

            float line = EditorGUIUtility.singleLineHeight;
            float y    = position.y;
            Rect Row() { var r = new Rect(position.x, y, position.width, line); y += line + Pad; return r; }

            if (cat    != null) EditorGUI.PropertyField(Row(), cat,
                new GUIContent("Tool Category Id", "1=Axe 2=Saw 3=Knife 4=Pickaxe"));
            if (isDur  != null) EditorGUI.PropertyField(Row(), isDur);
            if (maxDur != null) EditorGUI.PropertyField(Row(), maxDur);
            if (broken != null) DefIdPickerGUI.Draw(Row(), broken,
                new GUIContent("Broken Form", "The Def this tool becomes at durability 0 (0.2.10d)"),
                DefIdFilter.Item);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int rows = 0;
            foreach (var f in new[] { "toolCategoryId", "isDurabilityTool", "maxDurability", "brokenFormDefId" })
                if (property.FindPropertyRelative(f) != null) rows++;
            return rows * (EditorGUIUtility.singleLineHeight + Pad);
        }
    }
}
