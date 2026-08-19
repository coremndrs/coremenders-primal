using System;
using System.Collections.Generic;

namespace Game.Simulation
{
    [Serializable]
    public class DreamerRecord
    {
        public int slot;
        public Float3 position;
        public AppearanceData appearance = new AppearanceData();
        public DreamerNeeds needs = new DreamerNeeds();
        public float energy = 80f;
        public DreamerTask task = new DreamerTask();
        public float vitality = 100f;
        public AfflictionSet afflictions = new AfflictionSet();
        public bool isIncapacitated = false;
        public List<BuffInstance> buffs = new List<BuffInstance>();
        // Current Nightmare tier (0 = none, 1 = tier-1, 2 = tier-2). Persisted so the
        // escalation check in 0.1.7c reads the post-revert tier correctly.
        public int nightmareTier = 0;

        // Multi-container equip slots (0.2.5a): backpack + pouches + quiver.
        // Items belong to a container's contents, not directly to the dreamer.
        public EquipSlots equipSlots = new EquipSlots();

        // 24h day pool: remaining labor-minutes; debited on action commit, reset at midnight (0.2.3a).
        public float timePool = 1440f;

        // Action queue: index 0 is the active slot (started = true); the rest are queued (0.2.3b).
        // Reserve-on-assign — pool/energy/item debited at Add time, not at start time.
        public List<ActionRecord> actionQueue = new List<ActionRecord>();

        // Active self-contained (hand) craft (TDD §5.7.1, 0.2.10a). null = not crafting.
        // Station crafts live on the station Instance (Instance.craft), not here (§5.7.8).
        public CraftRecord craft = null;

        /// <summary>
        /// Returns the checkpoint ids referenced by this dreamer's live protection buffs
        /// (TDD §0.1.5b — ValidCheckpointsFor). Coverage is emergent: a checkpoint is valid
        /// for this dreamer if and only if they hold a live buff pointing to it.
        /// </summary>
        public IEnumerable<string> ValidCheckpoints()
        {
            if (buffs == null) yield break;
            foreach (var b in buffs)
                if (b.defId == "protection" && !string.IsNullOrEmpty(b.checkpointId))
                    yield return b.checkpointId;
        }
    }
}
