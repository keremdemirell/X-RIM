using XRim.Economy;
using XRim.Presentation.Vfx;

namespace XRim.App
{
    /// <summary>
    /// Turns the cosmetics a player has equipped into what the match draws (GDD §16, Decided: no cosmetic may change hitboxes, silhouette,
    /// reach or readability). Only Presentation types come out of here, so a cosmetic has no way into the rules or the physics. So far only
    /// one cosmetic does anything in a match: the Neon Sparks hit-effect set (colours only).
    /// </summary>
    public static class CosmeticLooks
    {
        /// <summary>The hit-effect set for an equipped hit-effect cosmetic; the plain set for none, or for one this build does not know.</summary>
        public static VfxStyle HitEffectStyle(string equippedHitEffectId)
        {
            if (equippedHitEffectId == CosmeticIds.NeonSparks) return VfxStyles.NeonSparks();
            return VfxStyles.Default();
        }
    }
}
