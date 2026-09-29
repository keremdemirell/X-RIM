using System.Collections.Generic;
using XRim.Core;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// A drawn weapon path in the fighter's torso frame, in arena units, with +X toward the opponent.
    /// GDD §6 (Decided): the path moves with the body. It holds positions only and never timestamps,
    /// so finger speed cannot affect the result (§4 input fairness, Decided).
    /// </summary>
    public sealed class WeaponPath
    {
        private readonly Vec2[] _points;

        public IReadOnlyList<Vec2> Points => _points;
        public float LengthUnits { get; }
        public bool IsEmpty => _points.Length < 2;

        public static WeaponPath Empty { get; } = new WeaponPath(new Vec2[0]);

        public WeaponPath(IReadOnlyList<Vec2> points)
        {
            _points = new Vec2[points.Count];
            float length = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                _points[i] = points[i];
                if (i > 0) length += Vec2.Distance(points[i - 1], points[i]);
            }

            LengthUnits = length;
        }
    }
}
