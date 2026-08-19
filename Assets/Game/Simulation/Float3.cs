using System;

namespace Game.Simulation
{
    [Serializable]
    public struct Float3
    {
        public float x, y, z;

        public Float3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static readonly Float3 Zero = new Float3(0f, 0f, 0f);

        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }
}
