namespace Game.Simulation
{
    /// <summary>
    /// How an action delivers its effect (TDD §0.2.3, §5.5).
    /// </summary>
    public enum ActionPayout
    {
        Gradual   = 0, // installs a buff that delivers the effect over the action window
        EndEffect = 1, // fires once at action completion (dream-save, crafting output, etc.)
    }
}
