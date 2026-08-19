namespace Game.Simulation
{
    /// <summary>
    /// Produced by SimResolver.Step when an end-payout action completes (TDD §0.2.3d1).
    /// WorldClockDriver processes this list to trigger host-side effects (dream-save, etc.).
    /// </summary>
    public class ActionEndedResult
    {
        public int            DreamerSlot;
        public ActionEndEffect EndEffect;
    }
}
