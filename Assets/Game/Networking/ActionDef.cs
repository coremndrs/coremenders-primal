using System;
using Game.Simulation;
using UnityEngine;

namespace Game.Networking
{
    // ── Yield / spawn entry ───────────────────────────────────────────────────────

    /// <summary>
    /// One yield or depletion-spawn entry on an <see cref="ItemAction"/> (TDD §1.12).
    /// isWorldObject = false → item yield (itemDefId → must have InventoryAspect on its Def).
    /// isWorldObject = true  → spawn a world object at the action's position (worldObjectDefId = target Def.defId).
    /// amount is item quantity; ignored for world-object spawns (always 1 object).
    /// </summary>
    [Serializable]
    public class ActionYield
    {
        [Tooltip("True = spawn a world object (worldObjectDefId). False = deliver an item stack (itemDefId).")]
        public bool  isWorldObject;
        [Tooltip("The item delivered when isWorldObject = false (pick by name; stores its Def.defId).")]
        [DefId(DefIdFilter.Item)]
        public int   itemDefId;
        [Tooltip("The world object spawned when isWorldObject = true (pick by name; stores its Def.defId).")]
        [DefId(DefIdFilter.WorldObject)]
        public int   worldObjectDefId;
        [Tooltip("Item quantity for item yields; ignored for world-object spawns.")]
        public float amount = 1f;
    }

    // ── ActionDef — the generic verb ──────────────────────────────────────────────

    /// <summary>
    /// A generic, shared "verb" — the KIND of interaction (pickup, split, gather, process, chop).
    /// It holds ONLY identity + how its outcome is applied + where it is shown. There is one asset
    /// per verb, reused across every item (the "ids not strings" / "one Chop across many Defs" rule,
    /// TDD §1.12).
    ///
    /// All per-item numbers — labor, yields, costs/rewards, tools, prerequisites — live on the item's
    /// <see cref="ItemAction"/> binding (Def.actions), NOT here. There are no per-item ActionDef assets.
    /// </summary>
    [CreateAssetMenu(fileName = "ActionDef_New", menuName = "Coremenders/Action Verb")]
    public class ActionDef : ScriptableObject
    {
        [Tooltip("Stable verb id (pickup / split / gather / process …). Used for labels and prerequisite matching.")]
        public string actionId;

        [Tooltip("What happens to the instance when this action completes.")]
        public ActionOutcome outcome = ActionOutcome.Transform;

        [Tooltip("Where this action is presented — world (in the scene) and/or inventory (in a container).")]
        public ActionContext context = ActionContext.World;

        [Header("Class (§5.5.1, 0.2.11a1)")]
        [Tooltip("Presence + clock semantics for this verb. Active = present for the full duration, " +
                 "world clock advances by it. Trivial = momentary, world clock does not advance, the " +
                 "duration is debited from the trivial bracket. Passive = runs on the object with " +
                 "nobody present. Authored per verb — never derived from duration.")]
        public ActionClass actionClass = ActionClass.Active;

        [Tooltip("Trivial verbs only (§5.5.5): may execute on an empty trivial bracket. Author true " +
                 "for the survival cases only — eat while starving, drink while dehydrated, bandage " +
                 "while bleeding. INERT until 0.2.11d5 reads it.")]
        public bool allowsCriticalBypass;
    }

    // ── ItemAction — the per-item binding ─────────────────────────────────────────

    /// <summary>
    /// Binds a generic verb (<see cref="action"/>) to ONE item and carries every item-specific
    /// parameter for performing that verb on this item (TDD §1.12). This is the single home for
    /// "what you can do to this item and what it costs / rewards" — authored on the Def, no code and
    /// no per-item ActionDef asset. The engine reads all specifics from here; the verb only supplies
    /// identity/outcome/context (exposed via the passthrough properties so readers stay uniform).
    /// </summary>
    [Serializable]
    public class ItemAction
    {
        [Tooltip("The generic verb this binding performs (supplies actionId / outcome / context).")]
        public ActionDef action;
        [Tooltip("Optional UI label; empty = the verb's actionId. Lets one verb (e.g. Gather) read as " +
                 "'gather sticks' vs 'chop firewood' on different items.")]
        public string labelOverride;

        [Header("Time")]
        [Tooltip("How much time this action takes, in IG minutes for ONE worker. This single value drives " +
                 "BOTH the duration (co-op finishes faster — 2 workers ≈ half the time) AND the timePool " +
                 "cost debited up front. 0 = instant (no timed engine, no time cost).")]
        public float timeRequired;
        [Tooltip("Allow multiple dreamers to contribute simultaneously (co-op speeds completion).")]
        public bool  coop = true;
        [Tooltip("Halts an active Skip when this action starts.")]
        public bool  interruptsSkip;

        [Header("Yields")]
        [Tooltip("Atomic: all yields on full completion. Proportional: scale with labor fraction.")]
        public YieldModel        yieldModel     = YieldModel.Atomic;
        [Tooltip("Items or world objects produced when this action completes.")]
        public ActionYield[]     yields         = Array.Empty<ActionYield>();
        [Tooltip("Where item yields are delivered. Inventory = contributor's containers; Location = ground.")]
        public PayoutDestination payoutDestination = PayoutDestination.Inventory;
        [Tooltip("Remove: instance removed when ALL its actions are done. Transform: remove now + spawn depletionSpawns.")]
        public DepletionBehavior onDepletion    = DepletionBehavior.Remove;
        [Tooltip("World objects or items spawned at the instance's position when onDepletion = Transform.")]
        public ActionYield[]     depletionSpawns = Array.Empty<ActionYield>();

        [Header("Resource economy — costs / rewards on THIS item")]
        [Tooltip("One-time totals debited/credited to the working dreamer UP FRONT when the action starts " +
                 "(negative = cost e.g. Energy -16, positive = reward). Refunded pro-rata if the action is " +
                 "stopped or finishes before the worker did their whole share. Time is NOT listed here — " +
                 "timeRequired above is the timePool cost.")]
        public ActionEffect[]    effects        = Array.Empty<ActionEffect>();

        [Header("Tools")]
        [Tooltip("Tool categories that satisfy this action (1=Axe, 2=Saw, 3=Knife, 4=Pickaxe). Empty = no tool.")]
        public int[] allowedToolCategoryIds = Array.Empty<int>();
        [Tooltip("If true, a matching tool must be present to start; if false, a matching tool gives a rate bonus.")]
        public bool  toolRequired;

        [Header("Prerequisites")]
        [Tooltip("The verb that must be complete (on this same item) before this action is available. Null = no gate.")]
        public ActionDef prerequisite;

        // ── Verb passthroughs (so engine readers use uniform member names) ─────────
        public string        actionId => action != null ? action.actionId : "";
        public ActionOutcome outcome  => action != null ? action.outcome  : ActionOutcome.Transform;
        public ActionContext context  => action != null ? action.context  : ActionContext.World;

        /// <summary>§5.5.1 class, from the verb. Active when the verb is missing — the conservative
        /// default (see <see cref="ActionClass"/>).</summary>
        public ActionClass   actionClass         => action != null ? action.actionClass         : ActionClass.Active;
        /// <summary>§5.5.5 critical bypass, from the verb. Inert until 0.2.11d5.</summary>
        public bool          allowsCriticalBypass => action != null && action.allowsCriticalBypass;

        /// <summary>UI label — the override if set, else the verb's actionId.</summary>
        public string DisplayLabel => !string.IsNullOrEmpty(labelOverride)
            ? labelOverride
            : (action != null ? action.actionId : "");
    }
}
