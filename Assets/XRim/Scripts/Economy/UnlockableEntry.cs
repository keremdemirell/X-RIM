using System;
using XRim.Core.Gdd;

namespace XRim.Economy
{
    /// <summary>
    /// One weapon, shield or signature move in the shop's data (GDD §16). Priced in earned currency only: there is deliberately no
    /// premium price field. Unlockables are sidegrades, never strictly stronger (Decided).
    /// </summary>
    [Serializable]
    public sealed class UnlockableEntry
    {
        /// <summary>The weapon id or signature move id the rules know it by.</summary>
        public string Id = string.Empty;

        public GameplayUnlockKind Kind = GameplayUnlockKind.Weapon;

        [Placeholder("§16 prices are TBD (designer 2026-10-25: placeholders)")]
        public long EarnedPrice = 0;

        /// <summary>A new player already owns it (starting weapons, designer 2026-10-15; the starter move, 2026-10-25).</summary>
        public bool StartsOwned = false;

        public UnlockableEntry()
        {
        }

        public UnlockableEntry(string id, GameplayUnlockKind kind, long earnedPrice, bool startsOwned)
        {
            Id = id;
            Kind = kind;
            EarnedPrice = earnedPrice;
            StartsOwned = startsOwned;
        }
    }
}
