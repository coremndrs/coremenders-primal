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

        // Trivial bracket (§5.5.5, 0.2.11d1): remaining minutes that may be spent on Trivial actions,
        // which execute instantly and do NOT advance the world clock. Deliberately separate from
        // timePool so instant execution can never become a way to buy free hours — the bracket is
        // bounded, resets to trivialBracketDailyBase at wake, and grows only by converting general
        // pool hours a dreamer idled through during a skip (d3). Leftovers are lost at sleep (d4).
        public float trivialBracket = 60f;

        // The dreamer's ONE active action slot + its FIFO queue (0.2.3b, collapsed 0.2.11a3).
        // Index 0 is the active slot (started = true); the rest are queued ahead. The retired
        // second channel (DreamerTask) is gone — sleep, rest, gathering, hand craft and station
        // craft all occupy this same slot now, distinguished by ActionRecord.kind.
        // Reserve-on-assign — pool/energy/item debited at Add time, not at start time.
        public List<ActionRecord> actionQueue = new List<ActionRecord>();

        // Active self-contained (hand) craft (TDD §5.7.1, 0.2.10a). null = not crafting.
        // Station crafts live on the station Instance (Instance.craft), not here (§5.7.8).
        // This is the craft PAYLOAD; slot occupancy is the ActionRecord above (kind = HandCraft).
        public CraftRecord craft = null;

        // ── Single-slot accessors (0.2.11a3) ─────────────────────────────────────
        // Methods, not properties: DreamerRecord is serialised field-and-property by
        // NewtonsoftSerializer, so a property here would write a duplicate copy of the
        // active record into dreamer_<slot>.json on every save.

        /// <summary>The entry occupying the active slot (started or about to start), or null.</summary>
        public ActionRecord ActiveAction() =>
            actionQueue != null && actionQueue.Count > 0 ? actionQueue[0] : null;

        /// <summary>True when the slot is taken — the dreamer cannot commit to another Active action.</summary>
        public bool IsBusy() => ActiveAction() != null;

        /// <summary>
        /// What the dreamer is doing, for the Stage-3 activity drain multiplier and the HUD.
        /// Replaces the retired <c>task.type</c>. Only a STARTED entry counts: a queued-ahead
        /// action must not change the drain rate before it begins.
        /// </summary>
        public TaskType CurrentTaskType()
        {
            var a = ActiveAction();
            return a != null && a.started ? a.taskType : TaskType.Idle;
        }

        /// <summary>
        /// Queue index of the entry bound to the given world-object action, or -1. Used by
        /// MapEntitySync to clear the slot when object-bound labor stops or completes.
        /// </summary>
        public int IndexOfWorldAction(int instanceId, int actionIndex)
        {
            if (actionQueue == null) return -1;
            for (int i = 0; i < actionQueue.Count; i++)
                if (actionQueue[i] != null && actionQueue[i].BindsWorldAction(instanceId, actionIndex))
                    return i;
            return -1;
        }

        /// <summary>
        /// Removes the entry bound to the given world-object action, if any. Returns true if one
        /// was removed. Whatever was queued behind it is promoted by SimResolver stage 4a on the
        /// next tick (that is the single auto-advance path — see <see cref="RemoveAt"/>).
        /// </summary>
        public bool ClearWorldAction(int instanceId, int actionIndex)
        {
            int i = IndexOfWorldAction(instanceId, actionIndex);
            if (i < 0) return false;
            RemoveAt(i);
            return true;
        }

        /// <summary>
        /// Removes the queue entry at <paramref name="index"/>.
        ///
        /// Deliberately does NOT start the entry behind it. Promotion happens in SimResolver
        /// stage 4a, which stamps the head entry started and installs its nourishment buffs in one
        /// place; starting it here too would either duplicate that buff logic in the Simulation
        /// layer or silently skip it.
        /// </summary>
        public void RemoveAt(int index)
        {
            if (actionQueue == null || index < 0 || index >= actionQueue.Count) return;
            actionQueue.RemoveAt(index);
        }

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
