namespace XRim.Bots.Balance
{
    /// <summary>What one weapon did for one side in one match.</summary>
    public sealed class WeaponTally
    {
        /// <summary>Turns it swung (a drawn path or a signature move with it).</summary>
        public int Swings { get; set; }

        public int Hits { get; set; }

        /// <summary>HP its hits removed.</summary>
        public float Damage { get; set; }

        /// <summary>Limbs its hits severed.</summary>
        public int Severs { get; set; }

        /// <summary>1 when its hit was the match-winning blow.</summary>
        public int Kos { get; set; }

        /// <summary>Turns it played a signature move (GDD §8).</summary>
        public int SignatureSwings { get; set; }

        /// <summary>Of those, the turns in which its side landed a hit (Session 15's note: a slow weapon plays only the start of a pattern).</summary>
        public int SignatureHitTurns { get; set; }
    }
}
