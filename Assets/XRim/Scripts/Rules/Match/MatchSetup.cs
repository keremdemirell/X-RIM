using XRim.Core;

namespace XRim.Rules.Match
{
    public sealed class MatchSetup
    {
        public PerSide<FighterSetup> Fighters { get; }

        /// <summary>Seed for every random choice in the match, so a match can be replayed exactly.</summary>
        public uint RandomSeed { get; }

        public MatchSetup(PerSide<FighterSetup> fighters, uint randomSeed)
        {
            Fighters = Guard.NotNull(fighters, nameof(fighters));
            RandomSeed = randomSeed;
        }
    }
}
