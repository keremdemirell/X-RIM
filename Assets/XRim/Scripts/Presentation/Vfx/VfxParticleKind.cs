namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// The shapes of bloodless mechanical destruction (GDD §2, Decided: bolts, nuts, springs, sparks and hydraulic fluid; no blood), and
    /// the flashes, rings and puffs that make an impact read. Debris pieces fall and stay on the floor; the rest fade out.
    /// </summary>
    public enum VfxParticleKind
    {
        Bolt = 0,
        Nut = 1,
        Spring = 2,
        Gear = 3,

        /// <summary>A drop of hydraulic fluid: it falls, then lies flattened on the floor.</summary>
        Drop = 4,

        /// <summary>A short-lived bright streak, stretched along its motion.</summary>
        Spark = 5,

        /// <summary>A soft cloud (dust from the floor, steam from a split torso) that drifts, grows and fades.</summary>
        Puff = 6,

        /// <summary>A ring that grows and fades (a shield block, a crush's shock).</summary>
        Ring = 7,

        /// <summary>A brief bright disc where something landed.</summary>
        Flash = 8,
    }
}
