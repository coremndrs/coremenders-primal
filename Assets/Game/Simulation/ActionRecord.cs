using System;

namespace Game.Simulation
{
    /// <summary>
    /// A single entry in a dreamer's action queue (TDD §0.2.3b1).
    /// Index 0 in DreamerRecord.actionQueue is the active slot; rest are queued.
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

        // Per-minute rates derived from totals; used to set nourishment buff magnitudeOverride
        public float HungerPerMin => duration > 0f ? hungerTotal / duration : 0f;
        public float ThirstPerMin => duration > 0f ? thirstTotal / duration : 0f;
    }
}
