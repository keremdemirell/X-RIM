using XRim.Core;

namespace XRim.Economy
{
    /// <summary>
    /// Anything that affects combat. Its price type is <see cref="EarnedAmount"/> only, so it cannot be priced in
    /// premium currency (GDD §16, Decided). Unlockable weapons and shields are sidegrades, never strictly stronger.
    /// </summary>
    public sealed class GameplayUnlock
    {
        public string Id { get; }
        public GameplayUnlockKind Kind { get; }
        public EarnedAmount Price { get; }

        public GameplayUnlock(string id, GameplayUnlockKind kind, EarnedAmount price)
        {
            Id = Guard.NotNull(id, nameof(id));
            Kind = kind;
            Price = price;
        }
    }
}
