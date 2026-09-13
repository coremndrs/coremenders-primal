namespace Game.Simulation
{
    /// <summary>
    /// What the dreamer's single active slot reads as — the label the drain multiplier (§4.4
    /// stage 3) and the HUD key off. Since the 0.2.11a3 channel collapse this lives on
    /// <see cref="ActionRecord.taskType"/>; the DreamerTask record that used to carry it is gone.
    /// </summary>
    public enum TaskType
    {
        Idle      = 0,
        Eating    = 1,
        Sleeping  = 2,
        Resting   = 3,
        Gathering = 4,  // object-bound task; dreamer is contributing to a ProcessableInstance action (0.2.8)
        Crafting  = 5,  // self-contained (hand) craft in progress on the dreamer (0.2.10a)
    }
}
