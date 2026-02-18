namespace VAuto.Zone.Core.Components
{
    /// <summary>
    /// Data struct tracking active arena damage on a player entity.
    /// Used by the damage tracking system to manage player damage state.
    /// </summary>
    public struct ArenaDamageState
    {
        /// <summary>
        /// Current damage value, decays each tick via multiplicative decay (damage *= 0.85f).
        /// </summary>
        public float CurrentDamage;

        /// <summary>
        /// Last time (in seconds since process start) damage tick was applied.
        /// Used to control 1.0-second tick interval.
        /// </summary>
        public double LastTickTime;

        /// <summary>
        /// Hash of the zone ID this damage state was created for.
        /// Used to detect and cleanup when player moves to different zone.
        /// </summary>
        public int ZoneIdHash;

        /// <summary>
        /// Original damage value before any decay was applied.
        /// Stored for reference and debugging purposes.
        /// </summary>
        public float InitialDamage;
    }
}
