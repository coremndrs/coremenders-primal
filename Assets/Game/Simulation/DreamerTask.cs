using System;

namespace Game.Simulation
{
    /// <summary>
    /// Authoritative per-dreamer task state (TDD §4.4 stage 4).
    /// Duration is baked in at assignment time from NeedsConfig so in-flight tasks
    /// are not affected by config changes. Outputs are applied by SimResolver on completion.
    /// </summary>
    [Serializable]
    public class DreamerTask
    {
        public TaskType type             = TaskType.Idle;
        public float    durationMinutes  = 0f;
        public float    elapsedMinutes   = 0f;

        // Object-bound task handle (0.2.8): populated when type == Gathering.
        // -1 = not bound to an object task.
        public int processableId  = -1;
        public int actionIndex    = -1;

        public bool IsComplete => type != TaskType.Idle && elapsedMinutes >= durationMinutes;
    }
}
