namespace Game.Simulation
{
    /// <summary>
    /// How an action relates to presence, the wall clock, and the world clock (TDD §5.5.1, 0.2.11a1).
    ///
    /// Authored per-def, never derived from duration — a duration threshold creates a cliff that
    /// every def drifts toward and that players feel as a seam. Duration <i>informs</i> the choice
    /// (nothing multi-hour is Trivial) but does not determine it.
    ///
    ///   Active  — presence required and locked to the work; full duration at 1× or a skip;
    ///             world clock advances by the duration; pool draw = duration.
    ///   Trivial — presence required but momentary; ~instant in wall clock; world clock does not
    ///             advance; the duration is debited from the trivial bracket (§5.5.5, 0.2.11d).
    ///   Passive — no presence; the process runs on an object and completes when the world clock
    ///             reaches its end, whether that clock advanced in real time or through a skip.
    ///
    /// Active is value 0 so a def that has not yet been authored reads as the conservative class:
    /// a mis-defaulted Trivial would hand the player free hours, a mis-defaulted Active only costs
    /// them time. The reclassification pass (0.2.11a2) authors every shipped def explicitly.
    /// </summary>
    public enum ActionClass
    {
        Active  = 0,
        Trivial = 1,
        Passive = 2,
    }
}
