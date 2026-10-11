using System;
using XRim.Core.Gdd;
using XRim.Rules.Limbs;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// What losing a limb changes (GDD §12): leg loss, whose limbs can be wielded, dropped weapons and the club's retrieval. The
    /// off-hand damage share lives in <see cref="DamageSettings.OffHandDamageMultiplier"/> and the club's slot in
    /// <see cref="LoadoutSettings.SeveredLimbIsExtraOption"/>.
    /// </summary>
    [Serializable]
    public sealed class LimbLossSettings
    {
        [GddTbd("§12", "Effect of losing one leg, and both legs",
            Proposal = "Designer 2026-10-10 (recommended default): no jump or lunge on one leg, no body moves on none")]
        public LegLossPenalty LegLossPenalty = LegLossPenalty.NoJumpOrLungeOnOneLeg;

        /// <summary>True: any severed limb on the floor can be wielded as a club, the opponent's included. False: only the dummy's own.</summary>
        [GddTbd("§12", "Can a dummy wield the opponent's severed limb? (not covered by the GDD)",
            Proposal = "Designer 2026-10-10 (G1): yes, any limb on the floor")]
        public bool AnySeveredLimbCanBeWielded = true;

        /// <summary>
        /// False: the weapon that drops with its arm comes back in the other hand from the next turn and the loadout stays whole.
        /// True: it is lost for the rest of the match.
        /// </summary>
        [GddTbd("§12", "Is the weapon dropped with the dominant arm lost for the match? (not covered by the GDD)",
            Proposal = "Designer 2026-10-10 (G2): no, the other hand holds it from the next turn")]
        public bool DroppedWeaponIsLost;

        /// <summary>When picking the club retrieves a limb from the floor; the other option keeps to the club's own reach.</summary>
        [GddTbd("§12", "Retrieving a severed limb",
            Proposal = "Designer 2026-10-10 (recommended default): automatic on selection, even when the limb flew out of reach")]
        public LimbRetrievalRule Retrieval = LimbRetrievalRule.Automatic;
    }
}
