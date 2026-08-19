using System;

namespace Game.Simulation
{
    [Flags]
    public enum ItemTypeTag : byte
    {
        None    = 0,
        General = 1,
        Ammo    = 2,
        Tool    = 4,
        Food    = 8,
    }
}
