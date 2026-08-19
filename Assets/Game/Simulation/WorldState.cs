using System;

namespace Game.Simulation
{
    [Serializable]
    public class WorldState
    {
        public int           schemaVersion;
        public string        mapId;
        public WorldClock    clock      = new WorldClock();
        public DreamerRecord[] dreamers = new DreamerRecord[2];
        // Set to true on Game Over so a loaded slot surfaces the end-of-run state (TDD §0.1.7c).
        public bool isGameOver = false;
        // Monotonic craft counter — RNG-as-state seed uniqueness (TDD §5.7.3, 0.2.10b2).
        // Global, non-positional → lives on WorldState (world.json). MUST be listed in
        // SaveSystem.Save's worldOnly constructor or it silently saves as 0 (CLAUDE.md trap).
        public int craftCounter = 0;
        // Ground items and harvest nodes moved to MapEntityLayer (map_{id}.json) at 0.2.7a.
    }
}
