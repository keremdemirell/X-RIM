using UnityEngine;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// A hit-effect set: the colours of every effect and the look of the weapon trails (GDD §16: "hit and impact effects, such as sparks and
    /// neon trails" are cosmetics). Each dummy can have its own (<see cref="MatchVfx.SetStyle"/>); the effects of a blow use its attacker's.
    /// Cosmetic only: it changes colours, never what happens.
    /// </summary>
    public sealed class VfxStyle
    {
        public Color Spark = new Color(1f, 0.85f, 0.45f);
        public Color Bolt = new Color(0.62f, 0.63f, 0.66f);
        public Color Nut = new Color(0.4f, 0.4f, 0.43f);
        public Color Spring = new Color(0.78f, 0.74f, 0.55f);
        public Color Gear = new Color(0.55f, 0.5f, 0.42f);
        public Color Fluid = new Color(0.25f, 0.6f, 0.95f, 0.9f);
        public Color Dust = new Color(0.62f, 0.58f, 0.5f, 0.55f);
        public Color Steam = new Color(0.9f, 0.92f, 0.95f, 0.5f);
        public Color Flash = new Color(1f, 0.97f, 0.85f, 0.95f);
        public Color FullBlock = new Color(0.55f, 0.9f, 1f, 0.95f);
        public Color PartialBlock = new Color(1f, 0.6f, 0.2f, 0.9f);
        public Color Zap = new Color(0.45f, 0.95f, 1f, 1f);

        /// <summary>A struck part flashes toward this colour.</summary>
        public Color PartFlash = new Color(1f, 1f, 0.92f);

        /// <summary>The trail at the tip and at its fading tail.</summary>
        public Color TrailHead = new Color(1f, 1f, 1f, 0.75f);
        public Color TrailTail = new Color(0.6f, 0.85f, 1f, 0f);

        /// <summary>The colour an effect's role has in this set.</summary>
        public Color ColorOf(VfxColorRole role)
        {
            switch (role)
            {
                case VfxColorRole.Spark: return Spark;
                case VfxColorRole.Bolt: return Bolt;
                case VfxColorRole.Nut: return Nut;
                case VfxColorRole.Spring: return Spring;
                case VfxColorRole.Gear: return Gear;
                case VfxColorRole.Fluid: return Fluid;
                case VfxColorRole.Dust: return Dust;
                case VfxColorRole.Steam: return Steam;
                case VfxColorRole.Flash: return Flash;
                case VfxColorRole.FullBlock: return FullBlock;
                case VfxColorRole.PartialBlock: return PartialBlock;
                default: return Zap;
            }
        }
    }
}
