using System;

namespace Game.Simulation
{
    public enum SkipStopCause
    {
        None        = 0,
        GuardHunger = 1,
        GuardThirst = 2,
        GuardWarmth = 3,
        Interrupt   = 4,
    }

    /// <summary>
    /// Reason a skip stopped early. Null = skip ran to full duration.
    /// Produced by SimResolver.CheckSkipGuards() and SkipManager.EnqueueInterrupt().
    /// Consumed by SkipManager (stage 11 equivalent) and surfaced to clients via NetworkVariables.
    /// </summary>
    public class SkipStopReason
    {
        public SkipStopCause Cause;
        public int           DreamerSlot;  // which dreamer tripped the guard; -1 for non-dreamer causes
        public string        Note;
    }

    /// <summary>
    /// Result when a sleeping dreamer is force-woken by a guard/interrupt stop (0.1.4b).
    /// Published via SkipManager.OnSleepCutShort; see SleepEndedResult for the unified 0.1.6 type.
    /// </summary>
    public class SleepCutShortResult
    {
        public int   DreamerSlot;
        public float ActualSleptMinutes;
        public bool  WasCutShort;
    }

    /// <summary>
    /// Unified sleep-end record used by 0.1.6 wake resolution.
    /// Covers both planned stage-4 completions (WasCutShort=false) and
    /// guard/interrupt force-wakes (WasCutShort=true).
    /// Populated by SimResolver.Step() via its sleepEnded output list,
    /// and by SkipManager.ForceWakeSleepers() for cut-short cases.
    /// </summary>
    public class SleepEndedResult
    {
        public int   DreamerSlot;
        public float SleptMinutes;
        public bool  WasCutShort;
    }
}
