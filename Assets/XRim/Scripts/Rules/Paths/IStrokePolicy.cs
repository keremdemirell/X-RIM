namespace XRim.Rules.Paths
{
    /// <summary>
    /// How a new stroke combines with the path already drawn this turn (GDD §6). Decided by the designer on
    /// 2026-09-29 (D5): one continuous stroke per turn; redrawing replaces it (<see cref="ReplaceStrokePolicy"/>).
    /// Works on drawn polylines, before resampling.
    /// </summary>
    public interface IStrokePolicy
    {
        WeaponPath Combine(WeaponPath current, WeaponPath newStroke);
    }
}
