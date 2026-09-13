using System;

namespace Game.Simulation
{
    /// <summary>
    /// One entry in a dreamer's single action slot + FIFO queue (TDD §0.2.3b1, collapsed 0.2.11a3).
    /// Index 0 in <see cref="DreamerRecord.actionQueue"/> is the active slot; the rest are queued.
    ///
    /// <b>0.2.11a3 — the channel collapse.</b> Before this build a dreamer had two independent
    /// active slots: this queue (consumables) and <c>DreamerRecord.task</c> (sleep / rest / gather /
    /// craft). Two slots let one dreamer spend labor faster than the clock advanced; the trivial
    /// bracket (§5.5.5) now does that job with a bound on it, and a presence-bound dreamer is by
    /// definition doing one thing. So <c>DreamerTask</c> is gone and every form of committed work —
    /// consumable, needs task, world labor, hand craft, station craft — occupies THIS record. The
    /// FIFO queue survives on the surviving slot as a queue-ahead convenience.
    ///
    /// Pool-minutes and energy are reserved (debited) at queue time, not at start.
    /// itemQuantity is stored so a queue-cancel can return the full item.
    /// consumedItemRemaining: non-null when the consumed item was itself a partial, so a
    /// queue-cancel correctly returns the partial (not a fresh copy) to the inventory.
    /// hungerTotal/thirstTotal drive the nourishment buff installed at action start.
    /// </summary>
    [Serializable]
    public class ActionRecord
    {
        // Buff def ids installed by gradual-payout eating actions
        public const string NourishmentHungerBuffId = "nourishment_hunger";
        public const string NourishmentThirstBuffId = "nourishment_thirst";
        // Debuff applied when pool goes negative (overdraft cost)
        public const string ExhaustionBuffId        = "exhaustion";

        public int          itemDefId;               // item def consumed to create this action
        public float        itemQuantity;             // quantity consumed; returned on full-refund cancel
        public float?       consumedItemRemaining;    // non-null if the consumed item was a partial
        public float        timeCost;                 // pool-minutes debited at queue time
        public float        energyCost;               // energy debited at queue time
        public float        startTime;                // clock.totalInGameMinutes when action became active
        public float        duration;                 // total IG minutes for this action
        public ActionPayout payout;
        public ActionEndEffect endEffect;
        public float        hungerTotal;              // for Gradual: total hunger to deliver over window
        public float        thirstTotal;              // for Gradual: total thirst to deliver over window
        public bool         started;                  // false = queued not yet active
        public bool         interruptsSkip;           // inert until skip integration

        // ── Single-slot fields (0.2.11a1 / a3) ───────────────────────────────────

        /// <summary>Presence / clock semantics of this action (§5.5.1). Resolved from the def at
        /// commit time and baked in, so a def re-authored mid-action does not change it in flight.</summary>
        public ActionClass    actionClass = ActionClass.Active;

        /// <summary>Which subsystem owns this slot entry and resolves its completion.</summary>
        public ActionSlotKind kind        = ActionSlotKind.Consumable;

        /// <summary>What the slot reads as for the activity drain multiplier (§4.4 stage 3) and the
        /// HUD label. Replaces the retired <c>DreamerTask.type</c>.</summary>
        public TaskType       taskType    = TaskType.Idle;

        /// <summary>Object-bound handle: the world Instance id being worked. -1 = self-contained.</summary>
        public int            instanceId  = -1;

        /// <summary>Object-bound handle: index into the Def's action list. -1 = self-contained.</summary>
        public int            actionIndex = -1;

        /// <summary>Station/hand craft: the recipe being made. 0 = not a craft.</summary>
        public int            recipeId;

        /// <summary>
        /// True when completion is NOT driven by <see cref="duration"/>. Object-bound labor accrues
        /// piecewise-linearly on the Instance (§5.5.5) and hand crafts resolve against their own
        /// clock window, so SimResolver must never time these out — their owner clears the slot.
        /// </summary>
        public bool           externallyResolved;

        // ── Commitment (0.2.11c) ─────────────────────────────────────────────────

        /// <summary>
        /// IG minutes the dreamer committed to this work (§5.5.3). Capped at commit time to the
        /// lesser of the remaining labor and their available day pool, so over-commitment is
        /// impossible by construction rather than by a later check. This — not the action's full
        /// <c>timeRequired</c> — is what is reserved from the pool, and the unspent part refunds on
        /// completion or cancel. 0 = open-ended (a needs task or a consumable, which run to their
        /// own duration).
        /// </summary>
        public float          committedMinutes;

        /// <summary>Clock time the commitment was made. The commitment expires at
        /// <c>commitStart + committedMinutes</c>, at which point contribution ends whether or not
        /// the work finished — the remainder stays on the object, resumable.</summary>
        public float          commitStart;

        /// <summary>True once <paramref name="clockMinutes"/> has passed the committed window. Always
        /// false for open-ended entries.</summary>
        public bool IsCommitmentExpiredAt(float clockMinutes) =>
            committedMinutes > 0f && clockMinutes >= commitStart + committedMinutes;

        /// <summary>IG minutes still owed on the commitment — the default length of a skip request
        /// attached to it (§5.5.3, c5).</summary>
        public float CommittedRemainingAt(float clockMinutes) =>
            committedMinutes <= 0f ? 0f : Math.Max(0f, commitStart + committedMinutes - clockMinutes);

        // ── Presence anchor (0.2.11b3) ───────────────────────────────────────────

        /// <summary>
        /// True when this action binds the dreamer to a spot with no object to measure against —
        /// a hand craft, rest or sleep. Object-bound work needs no anchor: its presence test is
        /// distance to the Instance itself (b1).
        /// </summary>
        public bool           presenceAnchored;

        /// <summary>Where the dreamer stood when they committed. Moving away from it by more than
        /// the tolerance cancels the action per §5.5.4 (b3).</summary>
        public Float3         presenceAnchor;

        // Per-minute rates derived from totals; used to set nourishment buff magnitudeOverride
        public float HungerPerMin => duration > 0f ? hungerTotal / duration : 0f;
        public float ThirstPerMin => duration > 0f ? thirstTotal / duration : 0f;

        /// <summary>IG minutes this action has been active, given the current clock. 0 while queued.</summary>
        public float ElapsedAt(float clockMinutes) =>
            started ? Math.Max(0f, clockMinutes - startTime) : 0f;

        /// <summary>True when a duration-driven action has reached its end time. Always false for
        /// <see cref="externallyResolved"/> entries.</summary>
        public bool IsCompleteAt(float clockMinutes) =>
            started && !externallyResolved && clockMinutes >= startTime + duration;

        /// <summary>True when this entry binds the given world-object action (0.2.11a3 slot lookup).</summary>
        public bool BindsWorldAction(int instance, int action) =>
            instanceId == instance && actionIndex == action;
    }
}
