namespace Game.Simulation
{
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
