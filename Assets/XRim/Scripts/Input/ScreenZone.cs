namespace XRim.Input
{
    public enum ScreenZone
    {
        /// <summary>The ~15% strip over the player's own dummy: swipes set the body move (GDD §4, §5).</summary>
        Body = 0,

        /// <summary>The remaining ~85%: drawing sets the weapon path (GDD §4, §6).</summary>
        Weapon = 1,
    }
}
