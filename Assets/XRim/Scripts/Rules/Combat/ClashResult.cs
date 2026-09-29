using XRim.Core;

namespace XRim.Rules.Combat
{
    public readonly struct ClashResult
    {
        public ClashKind Kind { get; }

        /// <summary>The weapon that continues its path, if exactly one does.</summary>
        public Side? Winner { get; }

        /// <summary>Damage multiplier for the winner's remaining path (crush-through penalty, GDD §10).</summary>
        public float WinnerDamageMultiplier { get; }

        public ClashResult(ClashKind kind, Side? winner, float winnerDamageMultiplier)
        {
            Kind = kind;
            Winner = winner;
            WinnerDamageMultiplier = winnerDamageMultiplier;
        }
    }
}
