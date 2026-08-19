using Game.Simulation;
using Unity.Netcode;

namespace Game.Networking
{
    /// <summary>
    /// INetworkSerializable bridge between the pure Game.Simulation.AppearanceData POCO
    /// and NGO's serialization layer. Lives in Networking so the Simulation wall stays intact.
    /// </summary>
    public struct AppearanceDataDto : INetworkSerializable, System.IEquatable<AppearanceDataDto>
    {
        public int SkinTone;
        public int HairStyle;
        public int HairColor;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SkinTone);
            serializer.SerializeValue(ref HairStyle);
            serializer.SerializeValue(ref HairColor);
        }

        public static AppearanceDataDto From(AppearanceData data) => new AppearanceDataDto
        {
            SkinTone  = data.skinTone,
            HairStyle = data.hairStyle,
            HairColor = data.hairColor
        };

        public AppearanceData ToAppearanceData() => new AppearanceData
        {
            skinTone  = SkinTone,
            hairStyle = HairStyle,
            hairColor = HairColor
        };

        public bool Equals(AppearanceDataDto other) =>
            SkinTone == other.SkinTone && HairStyle == other.HairStyle && HairColor == other.HairColor;

        public override bool Equals(object obj) => obj is AppearanceDataDto o && Equals(o);

        public override int GetHashCode() => System.HashCode.Combine(SkinTone, HairStyle, HairColor);
    }
}
