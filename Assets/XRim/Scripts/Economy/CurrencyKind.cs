namespace XRim.Economy
{
    /// <summary>
    /// Which balance pays for a cosmetic (GDD §16: "some cosmetics" take earned currency, all of them take premium). Gameplay unlocks have
    /// no such choice: <see cref="GameplayUnlock"/> is priced in earned currency only, so this type never appears next to one.
    /// </summary>
    public enum CurrencyKind
    {
        Earned = 0,
        Premium = 1,
    }
}
