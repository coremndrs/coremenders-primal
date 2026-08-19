using System;
using System.Collections.Generic;

namespace Game.Simulation
{
    /// <summary>
    /// Tuning knobs for the buff/debuff system (TDD §0.1.5a/b/c).
    /// Exposed as SerializeField on WorldClockDriver; Inspector-editable.
    ///
    /// Buff defs are authored in the defs list. Two built-in fallback defs are always
    /// available via GetDef() even when the list is empty, so tests work without Inspector setup.
    /// </summary>
    [Serializable]
    public class BuffConfig
    {
        // ── Buff definitions (author in Inspector or leave empty to use fallbacks) ──

        public List<BuffDef> defs = new List<BuffDef>();

        // ── Protection-buff durations per difficulty (IG minutes, GDD §18) ─────────

        public float firstDreamDurationMinutes = 1920f; // 32h — Normal initial shared buff
        public float hardcoreDurationMinutes   = 960f;  // 16h
        public float hardDurationMinutes       = 1440f; // 24h
        public float normalDurationMinutes     = 1920f; // 32h
        public float easyDurationMinutes       = 2880f; // 48h
        public float consumableDurationMinutes = 960f;  // Save-consumable personal buff (placeholder)

        // ── Sleep → checkpoint (0.1.6) ────────────────────────────────────────────
        // Minimum sleep minutes required to bank a checkpoint on wake.
        // Short cut-short sleeps below this threshold bank nothing (TDD §0.1.6a/a2).
        public float sleepMinBankThresholdMinutes = 60f; // 1h minimum

        // ── Dream flow (0.1.7) ────────────────────────────────────────────────────
        // Real-time seconds the partner has to rescue a downed dreamer.
        public float rescueWindowSeconds = 120f; // 2 real-time minutes
        // World-units distance within which the rescue action is valid.
        public float rescueRangeUnits = 3f;
        // Vitality restored to a downed dreamer on successful rescue.
        public float rescueVitalityRestore = 30f;
        // Nightmare energy-recovery multipliers (applied to sleep/rest energy restore).
        public float nightmareTier1RecoveryMultiplier = 0.5f;
        public float nightmareTier2RecoveryMultiplier = 0.25f;
        // Nightmare buff durations (IG minutes).
        public float nightmareTier1DurationMinutes = 240f; // 4h
        public float nightmareTier2DurationMinutes = 240f; // 4h

        // ── Wire-id mapping ───────────────────────────────────────────────────────
        // Kept in one place so DreamerBuffSync (packer) and the client HUD (resolver)
        // both use the same authoritative mapping. Stable: never change existing values.

        public static BuffNetId ToNetId(string defId) => defId switch
        {
            "protection"        => BuffNetId.Protection,
            "test_drain_half"   => BuffNetId.TestDrainHalf,
            "nightmare_tier1"   => BuffNetId.NightmareTier1,
            "nightmare_tier2"   => BuffNetId.NightmareTier2,
            "nourishment_hunger"=> BuffNetId.NourishmentHunger,
            "nourishment_thirst"=> BuffNetId.NourishmentThirst,
            "exhaustion"        => BuffNetId.Exhaustion,
            "sickness"          => BuffNetId.Sickness,
            _                   => BuffNetId.None,
        };

        public static string ToDefId(BuffNetId netId) => netId switch
        {
            BuffNetId.Protection        => "protection",
            BuffNetId.TestDrainHalf     => "test_drain_half",
            BuffNetId.NightmareTier1    => "nightmare_tier1",
            BuffNetId.NightmareTier2    => "nightmare_tier2",
            BuffNetId.NourishmentHunger => "nourishment_hunger",
            BuffNetId.NourishmentThirst => "nourishment_thirst",
            BuffNetId.Exhaustion        => "exhaustion",
            BuffNetId.Sickness          => "sickness",
            _                           => "",
        };

        // ── Lookup ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the def with the given id from the authored list, falling back to the
        /// built-in defaults so tests work without Inspector authoring.
        /// </summary>
        public BuffDef GetDef(string id)
        {
            if (!string.IsNullOrEmpty(id) && defs != null)
                foreach (var d in defs)
                    if (d.id == id) return d;

            if (id == DefaultTestBuff.id)            return DefaultTestBuff;
            if (id == DefaultProtectionDef.id)       return DefaultProtectionDef;
            if (id == DefaultNightmareTier1.id)      return DefaultNightmareTier1;
            if (id == DefaultNightmareTier2.id)      return DefaultNightmareTier2;
            if (id == DefaultNourishmentHunger.id)   return DefaultNourishmentHunger;
            if (id == DefaultNourishmentThirst.id)   return DefaultNourishmentThirst;
            if (id == DefaultExhaustion.id)          return DefaultExhaustion;
            if (id == DefaultSickness.id)            return DefaultSickness;
            return null;
        }

        // ── Built-in fallback defs ─────────────────────────────────────────────────

        /// <summary>
        /// Test buff: halves all Tier-1 drain rates for 60 IG minutes.
        /// Applied via the "Test Buff" dev button (0.1.5a acceptance).
        /// </summary>
        public static readonly BuffDef DefaultTestBuff = new BuffDef
        {
            id              = "test_drain_half",
            displayName     = "Test: ½ Drain",
            durationMinutes = 60f,
            modifiers       = new List<BuffModifier>
            {
                new BuffModifier { stat = BuffStat.HungerDrainMultiplier, value = 0.5f },
                new BuffModifier { stat = BuffStat.ThirstDrainMultiplier, value = 0.5f },
                new BuffModifier { stat = BuffStat.WarmthDrainMultiplier, value = 0.5f },
            },
        };

        /// <summary>
        /// Protection buff def: no stat modifiers — it is purely a checkpoint reference carrier.
        /// Duration is set per-instance at creation (First Dream / consumable / sleep-scaled).
        /// </summary>
        public static readonly BuffDef DefaultProtectionDef = new BuffDef
        {
            id              = "protection",
            displayName     = "Protection",
            durationMinutes = 1920f,
            modifiers       = new List<BuffModifier>(),
        };

        /// <summary>Tier-1 Nightmare: reduces energy recovery from sleep/rest by 50%.</summary>
        public static readonly BuffDef DefaultNightmareTier1 = new BuffDef
        {
            id              = "nightmare_tier1",
            displayName     = "Nightmare (I)",
            durationMinutes = 240f,
            modifiers       = new List<BuffModifier>
            {
                new BuffModifier { stat = BuffStat.EnergyRecoveryMultiplier, value = 0.5f },
            },
        };

        /// <summary>Tier-2 Nightmare: reduces energy recovery from sleep/rest by 75%.</summary>
        public static readonly BuffDef DefaultNightmareTier2 = new BuffDef
        {
            id              = "nightmare_tier2",
            displayName     = "Nightmare (II)",
            durationMinutes = 240f,
            modifiers       = new List<BuffModifier>
            {
                new BuffModifier { stat = BuffStat.EnergyRecoveryMultiplier, value = 0.25f },
            },
        };

        /// <summary>
        /// Nourishment (hunger): additive hunger restore per IG minute while eating.
        /// The actual rate is always set via magnitudeOverride from ActionRecord.HungerPerMin;
        /// the value here is a placeholder that activates only if override is 0.
        /// </summary>
        public static readonly BuffDef DefaultNourishmentHunger = new BuffDef
        {
            id              = "nourishment_hunger",
            displayName     = "Nourishing",
            durationMinutes = 0f, // set dynamically per action (duration + 1)
            modifiers       = new List<BuffModifier>
            {
                new BuffModifier { stat = BuffStat.HungerRestoreRate, value = 1f },
            },
        };

        /// <summary>
        /// Nourishment (thirst): additive thirst restore per IG minute while drinking.
        /// Rate set via magnitudeOverride from ActionRecord.ThirstPerMin.
        /// </summary>
        public static readonly BuffDef DefaultNourishmentThirst = new BuffDef
        {
            id              = "nourishment_thirst",
            displayName     = "Refreshing",
            durationMinutes = 0f,
            modifiers       = new List<BuffModifier>
            {
                new BuffModifier { stat = BuffStat.ThirstRestoreRate, value = 1f },
            },
        };

        /// <summary>
        /// Exhaustion debuff: applied when the day pool goes negative (overdraft or movement).
        /// Placeholder penalty — tune in Inspector by overriding this def in the defs list.
        /// </summary>
        public static readonly BuffDef DefaultExhaustion = new BuffDef
        {
            id              = "exhaustion",
            displayName     = "Exhausted",
            durationMinutes = 1440f, // default 24h; overridden by NeedsConfig.exhaustionDurationMinutes at instance creation
            modifiers       = new List<BuffModifier>
            {
                new BuffModifier { stat = BuffStat.HungerDrainMultiplier, value = 1.5f },
                new BuffModifier { stat = BuffStat.ThirstDrainMultiplier, value = 1.5f },
            },
        };

        /// <summary>
        /// Sickness debuff: applied when a dreamer eats spoiled food (0.2.4d).
        /// Placeholder penalty — tune duration and modifiers in Inspector.
        /// </summary>
        public static readonly BuffDef DefaultSickness = new BuffDef
        {
            id              = "sickness",
            displayName     = "Sick",
            durationMinutes = 120f, // 2h placeholder
            modifiers       = new List<BuffModifier>
            {
                new BuffModifier { stat = BuffStat.ThirstDrainMultiplier, value = 1.5f },
            },
        };
    }
}
