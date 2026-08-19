using System;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>Which Defs a <see cref="DefIdAttribute"/> picker offers.</summary>
    public enum DefIdFilter
    {
        Any,          // every Def
        Item,         // Defs with an InventoryAspect (craft inputs / item outputs)
        WorldObject,  // Defs with world actions (stations, processables, placed objects)
    }

    /// <summary>
    /// Marks an <c>int</c> field that holds a <see cref="Def.defId"/> so the inspector draws a
    /// searchable Def picklist instead of a raw number (0.2.10 authoring aid). Pick a Def by name and
    /// the field is populated with its id — no going back and forth to look up ids. Use
    /// <see cref="DefIdFilter"/> to scope the list (item inputs vs. world objects/stations).
    ///
    /// Only usable on fields in engine-referencing assemblies (Networking/Presentation). The one
    /// def-id field in the engine-free Simulation layer (ToolAspect.brokenFormDefId) gets the same
    /// picker via a dedicated property drawer instead of this attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class DefIdAttribute : PropertyAttribute
    {
        public readonly DefIdFilter filter;
        public DefIdAttribute(DefIdFilter filter = DefIdFilter.Any) { this.filter = filter; }
    }
}
