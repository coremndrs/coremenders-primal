using System;

namespace Game.Simulation
{
    /// <summary>
    /// Byte-sized enum for checkpoint coverage. Used as wire-safe numeric in VoteOptionData
    /// so no strings cross the NGO boundary. coverageLabel in CheckpointMeta stays as a
    /// string for disk persistence; ParseCoverage converts it for network transport.
    /// </summary>
    public enum CheckpointCoverage : byte
    {
        Unknown  = 0,
        Personal = 1,
        Shared   = 2,
        Dev      = 3,
    }

    /// <summary>
    /// Metadata sidecar written alongside each named checkpoint file (TDD §0.1.5b).
    /// Stored as meta.json in the checkpoint directory.
    /// coverageLabel ("Shared" | "Personal" | "Dev") is disk-only; the Wake Up vote
    /// converts it to CheckpointCoverage before sending anything over the wire.
    /// </summary>
    [Serializable]
    public class CheckpointMeta
    {
        public string id;
        public long   timestampUtc;            // DateTime.UtcNow.Ticks at creation; used for vote ordering (0.1.7)
        public string coverageLabel;           // disk format — parse to CheckpointCoverage for wire transport
        public float  capturedAtInGameMinutes; // world clock value when captured; cost proxy for Wake Up vote (0.1.7b)

        public static CheckpointCoverage ParseCoverage(string label) => label switch
        {
            "Shared"   => CheckpointCoverage.Shared,
            "Personal" => CheckpointCoverage.Personal,
            "Dev"      => CheckpointCoverage.Dev,
            _          => CheckpointCoverage.Unknown,
        };
    }
}
