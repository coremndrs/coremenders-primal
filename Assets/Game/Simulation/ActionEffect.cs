using System;

namespace Game.Simulation
{
    /// <summary>
    /// A dreamer resource that an action can drain or restore. Extend freely — the mapping to a
    /// DreamerRecord field lives in one place (SimResolver.ApplyActionEffect).
    /// </summary>
    public enum ResourceStat
    {
        Energy,
        TimePool,
        Hunger,
        Thirst,
        Warmth,
        Vitality,
    }

    /// <summary>
    /// One generalized, data-driven cost/reward on an action's <see cref="ItemAction"/> (the item's
    /// per-action config). <c>amount</c> is a ONE-TIME TOTAL applied up front when the action starts:
    /// negative = cost, positive = reward. If the action is stopped or finishes before the worker did
    /// their whole share, the unused fraction is refunded. The action's TIME cost is not expressed here
    /// — it is the single <c>ItemAction.timeRequired</c> value (which also drives the duration). Authoring
    /// is pure config; one engine applies it to every item's every action.
    /// </summary>
    [Serializable]
    public class ActionEffect
    {
        public ResourceStat stat;
        public float        amount; // one-time total: <0 cost, >0 reward (refunded pro-rata if stopped)
    }
}
