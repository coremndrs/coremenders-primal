using System;
using System.Collections.Generic;

namespace Game.Simulation
{
    /// <summary>
    /// One material reserved (consumed) by a craft at commit time (TDD §5.7.0, 0.2.10a2).
    /// Stored on the <see cref="CraftRecord"/> so a cancel/partial can refund exactly what was
    /// taken — iterated, never per-numbered-field (CLAUDE.md "lists not numbered fields").
    /// </summary>
    [Serializable]
    public class CraftReservedInput
    {
        public int   itemDefId;
        public float amount;
    }

    /// <summary>
    /// Authoritative saved state for one in-flight craft (TDD §5.7, 0.2.10).
    ///
    /// Two homes, one type (§5.7.8):
    ///   • Hand craft (self-contained, §5.7.1)  → DreamerRecord.craft. Timestamp-driven:
    ///     done when clock ≥ startTime + duration; the crafter is busy for the window.
    ///   • Station craft (object-bound, §5.7.1) → Instance.craft on the station. Labor-driven
    ///     via the station Instance's location.accrual[actionIndex] (the §5.5.5 engine); the
    ///     record only carries the recipe identity, seed inputs, and requiredLabor override.
    ///
    /// RNG-as-state (§5.7.3): the tier roll is a pure function of (crafterSlot, recipeId,
    /// startTime, craftCounter) — all saved here — so a skip across completion and a replay
    /// re-derive the identical output, and a revert-then-recraft (new startTime) is a new roll.
    /// </summary>
    [Serializable]
    public class CraftRecord
    {
        public int   recipeId;
        public int   crafterSlot;    // committing crafter (seed input; station-craft primary payout target)
        public float startTime;      // clock.totalInGameMinutes at commit (seed input; hand-craft completion)
        public float duration;       // hand: recipe timeCost (IG minutes). Station uses accrual labor instead.
        public int   craftCounter;   // monotonic save value for seed uniqueness (§5.7.3, b2)
        public int   outputAmount = 1;
        public int   rollGranularity;// 0 = PerUnit, 1 = PerBatch (mirrors RollGranularity enum)
        public bool  isStation;      // false = hand (self-contained); true = station (object-bound)

        // Station-craft binding: which of the station's actions this craft accrues on, and the
        // labor threshold (from the recipe, overriding the station Def action's timeRequired).
        public int   stationActionIndex = -1;
        public float requiredLabor;

        // Reserved costs for refund (hand path; station path refunds via the §5.5 engine's join clock).
        public float reservedTime;
        public float reservedEnergy;
        public List<CraftReservedInput> reservedInputs = new List<CraftReservedInput>();

        /// <summary>Hand-craft completion: timestamp model, exactly like §5.5.0 / spoilage.</summary>
        public bool HandComplete(float now) => !isStation && now >= startTime + duration;
    }
}
