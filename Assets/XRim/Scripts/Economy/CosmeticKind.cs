namespace XRim.Economy
{
    /// <summary>
    /// Cosmetics for sale (GDD §16, Decided). None may change hitboxes, silhouette, reach or readability;
    /// they are referenced only by Presentation and Economy, never by the rules or simulation.
    /// </summary>
    public enum CosmeticKind
    {
        Skin = 0,
        DummyModel = 1,
        ArenaTheme = 2,
        HitEffect = 3,
        GruntPack = 4,
    }
}
