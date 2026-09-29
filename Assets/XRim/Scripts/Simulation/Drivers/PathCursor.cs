using XRim.Core;
using XRim.Rules.Paths;

namespace XRim.Simulation.Drivers
{
    /// <summary>Finds the point at a distance along a path (arc length), clamped to the path's ends.</summary>
    public sealed class PathCursor
    {
        private readonly Vec2[] _points;
        private readonly float[] _cumulativeUnits;

        public float LengthUnits { get; }
        public bool IsEmpty => _points.Length == 0;

        public PathCursor(WeaponPath path)
        {
            Guard.NotNull(path, nameof(path));
            int count = path.Points.Count;
            _points = new Vec2[count];
            _cumulativeUnits = new float[count];
            float length = 0f;
            for (int i = 0; i < count; i++)
            {
                _points[i] = path.Points[i];
                if (i > 0) length += Vec2.Distance(_points[i - 1], _points[i]);
                _cumulativeUnits[i] = length;
            }

            LengthUnits = length;
        }

        public Vec2 PointAt(float distanceUnits)
        {
            if (_points.Length == 0) return Vec2.Zero;
            if (distanceUnits <= 0f) return _points[0];
            int last = _points.Length - 1;
            if (distanceUnits >= LengthUnits) return _points[last];

            int low = 0, high = last;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (_cumulativeUnits[mid] <= distanceUnits) low = mid;
                else high = mid;
            }

            float segment = _cumulativeUnits[high] - _cumulativeUnits[low];
            float t = segment > 0f ? (distanceUnits - _cumulativeUnits[low]) / segment : 0f;
            return Vec2.Lerp(_points[low], _points[high], t);
        }
    }
}
