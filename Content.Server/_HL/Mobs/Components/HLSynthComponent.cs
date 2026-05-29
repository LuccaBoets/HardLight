using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Mobs.Components
{
    [RegisterComponent]
    public sealed partial class HLSynthComponent : Component
    {
        /// <summary>
        /// How often SynthBlood is evaluated for nanite generation.
        /// </summary>
        [DataField]
        public TimeSpan UpdateInterval = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Next scheduled nanite-generation update time.
        /// </summary>
        [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
        public TimeSpan NextUpdate = TimeSpan.Zero;

        /// <summary>
        /// How often nanites attempt bleed sealing, passive repair, and blood restoration.
        /// </summary>
        [DataField]
        public TimeSpan HealUpdateInterval = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Next scheduled healing update time.
        /// </summary>
        [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
        public TimeSpan NextHealUpdate = TimeSpan.Zero;

        /// <summary>
        /// Minimum SynthBlood reserve to keep before converting fullerene into nanites.
        /// </summary>
        [DataField]
        public FixedPoint2 BloodNaniteThreshold = 150;

        /// <summary>
        /// Amount of SynthBlood consumed per full nanite-generation step.
        /// </summary>
        [DataField]
        public FixedPoint2 NaniteGenerationCost = 5;

        /// <summary>
        /// Amount of active nanites created per generation step.
        /// </summary>
        [DataField]
        public FixedPoint2 NaniteGenerationAmount = 1;

        /// <summary>
        /// Maximum active nanite volume the bloodstream should hold.
        /// </summary>
        [DataField]
        public FixedPoint2 MaxNanites = 20;

        /// <summary>
        /// Preferred SynthBlood level to refill back toward once nanites are full.
        /// </summary>
        [DataField]
        public FixedPoint2 TargetBloodVolume = 250;

        /// <summary>
        /// Nanite cost of one full passive repair tick.
        /// </summary>
        [DataField]
        public FixedPoint2 NaniteHealCost = 0.5;

        /// <summary>
        /// SynthBlood restored per healing tick while nanites are full.
        /// </summary>
        [DataField]
        public FixedPoint2 PassiveBloodRestore = 1;

        /// <summary>
        /// Maximum brute healing applied per full passive repair tick.
        /// </summary>
        [DataField]
        public FixedPoint2 PassiveBruteHeal = 2;

        /// <summary>
        /// Maximum burn healing applied per full passive repair tick.
        /// </summary>
        [DataField]
        public FixedPoint2 PassiveBurnHeal = 2;

        /// <summary>
        /// Nanite cost of one full bleed-sealing tick.
        /// </summary>
        [DataField]
        public FixedPoint2 BleedSealNaniteCost = 0.55;

        /// <summary>
        /// Bleed amount reduced by one full bleed-sealing tick.
        /// </summary>
        [DataField]
        public float BleedSealAmount = 0.3f;
    }
}
