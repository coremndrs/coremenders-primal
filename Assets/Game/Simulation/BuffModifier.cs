using System;

namespace Game.Simulation
{
    [Serializable]
    public class BuffModifier
    {
        public BuffStat stat;
        public float    value; // multiplicative (1.0 = no change, 0.5 = half drain)
    }
}
