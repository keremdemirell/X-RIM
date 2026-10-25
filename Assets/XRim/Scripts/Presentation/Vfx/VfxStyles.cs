using UnityEngine;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// The hit-effect sets the game can put on a dummy (GDD §16: cosmetics, Decided: none may change hitboxes, silhouette, reach or
    /// readability). A set only changes colours, never what happens, and it leaves alone the colours that tell the players something about
    /// the fight: a full block, a partial block and the electric wall's zap read the same in every set.
    /// </summary>
    public static class VfxStyles
    {
        /// <summary>The set every dummy has until it wears another.</summary>
        public static VfxStyle Default() => new VfxStyle();

        /// <summary>
        /// "Neon Sparks" (Session 18, the first real cosmetic, placeholder look): hot pink and electric cyan sparks, a bright flash, neon
        /// green bolts, and weapon trails that burn magenta and fade to cyan.
        /// </summary>
        public static VfxStyle NeonSparks()
        {
            var plain = new VfxStyle();
            return new VfxStyle
            {
                Spark = new Color(1f, 0.15f, 0.85f),
                Bolt = new Color(0.35f, 1f, 0.55f),
                Nut = new Color(0.1f, 0.75f, 0.55f),
                Spring = new Color(1f, 0.4f, 0.95f),
                Gear = new Color(0.25f, 0.9f, 1f),
                Fluid = plain.Fluid,
                Dust = plain.Dust,
                Steam = plain.Steam,
                Flash = new Color(0.85f, 1f, 1f, 0.95f),
                FullBlock = plain.FullBlock,
                PartialBlock = plain.PartialBlock,
                Zap = plain.Zap,
                PartFlash = new Color(0.9f, 1f, 1f),
                TrailHead = new Color(1f, 0.25f, 0.95f, 0.9f),
                TrailTail = new Color(0f, 0.9f, 1f, 0f),
            };
        }
    }
}
