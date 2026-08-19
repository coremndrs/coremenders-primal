using System;
using System.Collections.Generic;

namespace Game.Simulation
{
    public enum YieldModel        { Proportional, Atomic }
    public enum DepletionBehavior { Remove, Transform }
    public enum PayoutDestination { Inventory, Location }

    /// <summary>
    /// One "segment" of piecewise-linear labor accrual (TDD §5.5.5).
    /// Opened on every contributor-change event; rate = sum of active contributors.
    /// </summary>
    [Serializable]
    public class LaborSegment
    {
        public float segmentStart;
        public float ratePerMinute;
        public float accumulatedAtStart;
    }

    /// <summary>
    /// Per-action mutable accrual state for an object-bound task (TDD §5.5.5, §5.6.6, 0.2.8b1).
    /// Attaches to Instance.location.accrual[] for InWorld instances with actions.
    /// </summary>
    [Serializable]
    public class ActionAccrualState
    {
        public float             accumulatedLabor;
        public bool              isComplete;
        public List<LaborSegment> segments    = new List<LaborSegment>();
        public List<int>          contributors = new List<int>();

        public float LaborAt(float clockMinutes)
        {
            if (isComplete || segments.Count == 0) return accumulatedLabor;
            var last = segments[segments.Count - 1];
            return last.accumulatedAtStart + (clockMinutes - last.segmentStart) * last.ratePerMinute;
        }

        public bool HasActiveContributors => contributors.Count > 0;

        public void AddContributor(int dreamerSlot, float clockMinutes)
        {
            if (contributors.Contains(dreamerSlot)) return;
            accumulatedLabor = LaborAt(clockMinutes);
            contributors.Add(dreamerSlot);
            segments.Add(new LaborSegment
            {
                segmentStart       = clockMinutes,
                ratePerMinute      = contributors.Count,
                accumulatedAtStart = accumulatedLabor,
            });
        }

        public void RemoveContributor(int dreamerSlot, float clockMinutes)
        {
            if (!contributors.Contains(dreamerSlot)) return;
            accumulatedLabor = LaborAt(clockMinutes);
            contributors.Remove(dreamerSlot);
            segments.Clear();
            if (contributors.Count > 0)
            {
                segments.Add(new LaborSegment
                {
                    segmentStart       = clockMinutes,
                    ratePerMinute      = contributors.Count,
                    accumulatedAtStart = accumulatedLabor,
                });
            }
        }

        public void Complete(float clockMinutes)
        {
            accumulatedLabor = LaborAt(clockMinutes);
            isComplete       = true;
            segments.Clear();
            contributors.Clear();
        }
    }
}
