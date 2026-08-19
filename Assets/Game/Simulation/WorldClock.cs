using System;

namespace Game.Simulation
{
    /// <summary>
    /// Authoritative in-game world clock (TDD §E1).
    ///
    /// Lives in the RDM's WorldState. Advances via Advance(realSeconds) called
    /// by the host's WorldClockDriver every frame — the embryo of the elapsed-time
    /// resolver (TDD §1.7). The "small steps == one big jump" invariant is trivially
    /// true here (linear advance); the resolver generalises it in Cluster 1.
    ///
    /// No Unity dependency — engine delta-time is supplied by the caller.
    /// </summary>
    [Serializable]
    public class WorldClock
    {
        public float totalInGameMinutes;
        public float realSecondsPerInGameMinute = 6f;
        public bool  isPaused;

        /// <summary>Advances the clock by realSeconds of wall-clock time (no-op when paused).</summary>
        public void Advance(float realSeconds)
        {
            if (isPaused) return;
            totalInGameMinutes += realSeconds / realSecondsPerInGameMinute;
        }

        public void Pause()  => isPaused = true;
        public void Resume() => isPaused = false;

        /// <summary>Returns (day, hour, minute). Day starts at 1; hour and minute are 0-based.</summary>
        public (int day, int hour, int minute) GetTime()
        {
            int totalMins = (int)totalInGameMinutes;
            int day       = totalMins / 1440 + 1;       // 1440 = 24 * 60
            int remaining = totalMins % 1440;
            int hour      = remaining / 60;
            int minute    = remaining % 60;
            return (day, hour, minute);
        }

        public string GetTimeString()
        {
            var (day, hour, minute) = GetTime();
            return $"Day {day} — {hour:D2}:{minute:D2}";
        }
    }
}
