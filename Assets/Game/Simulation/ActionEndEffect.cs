namespace Game.Simulation
{
    /// <summary>
    /// The one-shot effect fired when an EndEffect action completes (TDD §0.2.3d).
    /// </summary>
    public enum ActionEndEffect
    {
        None      = 0,
        DreamSave = 1, // banks a personal dream-save checkpoint on completion
    }
}
