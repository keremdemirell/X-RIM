using XRim.Core;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// GDD §6, D3 (Decided 2026-09-29): a stroke can start anywhere. A straight lead-in from the weapon's current
    /// tip to the first drawn point is added in front of it. The lead-in is part of the path, so it costs ink and
    /// time like any other part: reach is never gained for free.
    /// </summary>
    public sealed class LeadInFromTipPathStartPolicy : IPathStartPolicy
    {
        public WeaponPath ResolveStart(WeaponPath drawn, Vec2 weaponTipLocal)
        {
            Guard.NotNull(drawn, nameof(drawn));
            if (drawn.Points.Count == 0)
            {
                return WeaponPath.Empty;
            }

            var points = new Vec2[drawn.Points.Count + 1];
            points[0] = weaponTipLocal;
            for (int i = 0; i < drawn.Points.Count; i++)
            {
                points[i + 1] = drawn.Points[i];
            }

            return new WeaponPath(points);
        }
    }
}
