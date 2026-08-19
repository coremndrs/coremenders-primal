namespace Game.Simulation
{
    /// <summary>
    /// RNG-as-state for crafting (TDD §5.7.3, 0.2.10b) — the first stochastic system.
    ///
    /// There is NO global stream. Every draw is a pure function of saved authoritative state:
    /// a per-craft seed derived from (crafterId, recipeId, startTime, craftCounter), plus a
    /// varying unit index. So replaying a saved craft (skip across completion) re-derives the
    /// identical tier(s); a revert-then-recraft (different startTime → different seed) is a new
    /// roll. Pure C# — no UnityEngine, no System.Random instance state — so it lives in the
    /// Simulation assembly and reproduces bit-for-bit on host and client.
    ///
    /// The heavier global-stream RNG-as-state stays deferred to the first *continuous*
    /// stochastic process (weather / AI); see TODO.md "RNG-as-state".
    /// </summary>
    public static class CraftRng
    {
        /// <summary>Per-craft seed = f(crafterId, recipeId, startTime, craftCounter). §5.7.3 b2.</summary>
        public static int Seed(int crafterId, int recipeId, float startTime, int craftCounter)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + crafterId;
                h = h * 31 + recipeId;
                h = h * 31 + (int)startTime;   // IG-minute granularity; craftCounter disambiguates same-minute crafts
                h = h * 31 + craftCounter;
                return h;
            }
        }

        /// <summary>
        /// Deterministic uniform value in [0,1) for (seed, unitIndex). PerUnit varies unitIndex
        /// so all N unit rolls come from the one saved seed; PerBatch passes unitIndex = 0.
        /// xorshift32 over the mixed seed — stateless, so it re-derives identically on replay.
        /// </summary>
        public static float Unit01(int seed, int unitIndex)
        {
            unchecked
            {
                uint x = (uint)seed ^ (uint)(unitIndex * 0x9E3779B1);
                if (x == 0u) x = 0x1234567u;   // avoid the xorshift fixed point
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                return (x & 0xFFFFFF) / (float)0x1000000;   // 24-bit mantissa → [0,1)
            }
        }

        /// <summary>
        /// Weighted pick of a tier index from <paramref name="weights"/> for (seed, unitIndex).
        /// <paramref name="skillModifier"/> is the Cluster-7 skill seam (§5.7.4): 0 = the fixed
        /// unskilled distribution the weights already encode (low tiers common, high rare). It is
        /// inert for now (any value leaves the authored weights unchanged); the reshape lands with
        /// the skill system.
        /// </summary>
        public static int TierIndex(int seed, int unitIndex, float[] weights, float skillModifier = 0f)
        {
            if (weights == null || weights.Length == 0) return 0;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i] > 0f ? weights[i] : 0f;
            if (total <= 0f) return 0;

            float r   = Unit01(seed, unitIndex) * total;
            float acc = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i] > 0f ? weights[i] : 0f;
                if (r < acc) return i;
            }
            return weights.Length - 1;
        }
    }
}
