namespace XRim.Core
{
    /// <summary>Seeded randomness. Rules, simulation and bots never use System.Random or UnityEngine.Random directly.</summary>
    public interface IRandom
    {
        /// <summary>Returns an int in [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Returns a float in [0, 1).</summary>
        float NextFloat();
    }
}
