using XRim.Core;

namespace XRim.Presentation.Cameras
{
    /// <summary>
    /// A quick camera punch-in toward a big moment and back (GDD §2 camera behaviour TBD, flagged in the Feel asset; presentation only):
    /// the view shrinks to a share of its size around the moment's point, holds, then eases back out. A stronger punch replaces a
    /// weaker one still on its way in or holding; otherwise the newest one wins. Real (screen) time.
    /// </summary>
    public sealed class CameraPunch
    {
        private float _viewFraction = 1f;
        private float _inSeconds;
        private float _holdSeconds;
        private float _outSeconds;
        private float _ageSeconds;
        private bool _active;

        public bool IsActive => _active;

        /// <summary>The point the view zooms toward (it stays where it is on the screen), canonical arena units.</summary>
        public Vec2 FocusUnits { get; private set; }

        /// <summary>The share of its normal size the view has now (1 = no punch).</summary>
        public float ZoomFraction
        {
            get
            {
                if (!_active) return 1f;
                if (_ageSeconds < _inSeconds) return Lerp(1f, _viewFraction, Smooth(_ageSeconds / _inSeconds));
                float afterHold = _ageSeconds - _inSeconds - _holdSeconds;
                if (afterHold <= 0f) return _viewFraction;
                return _outSeconds > 0f ? Lerp(_viewFraction, 1f, Smooth(afterHold / _outSeconds)) : 1f;
            }
        }

        /// <param name="viewFraction">The share of its size the view shrinks to (0.8 = the view is 80% as big, things look 25% bigger).</param>
        public void Trigger(Vec2 focusUnits, float viewFraction, float inSeconds, float holdSeconds, float outSeconds)
        {
            if (viewFraction >= 1f || viewFraction <= 0f) return;
            bool stillIn = _active && _ageSeconds < _inSeconds + _holdSeconds;
            if (stillIn && _viewFraction <= viewFraction) return; // a punch at least as strong is still on its way in or holding

            FocusUnits = focusUnits;
            _viewFraction = viewFraction;
            _inSeconds = inSeconds > 0f ? inSeconds : 0f;
            _holdSeconds = holdSeconds > 0f ? holdSeconds : 0f;
            _outSeconds = outSeconds > 0f ? outSeconds : 0f;
            _ageSeconds = 0f;
            _active = true;
        }

        public void Advance(float realDeltaSeconds)
        {
            if (!_active || realDeltaSeconds <= 0f) return;
            _ageSeconds += realDeltaSeconds;
            if (_ageSeconds >= _inSeconds + _holdSeconds + _outSeconds) _active = false;
        }

        public void Clear()
        {
            _active = false;
            _ageSeconds = 0f;
        }

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;

        /// <summary>Ease in and out (smoothstep).</summary>
        private static float Smooth(float t)
        {
            float clamped = XMath.Clamp01(t);
            return clamped * clamped * (3f - 2f * clamped);
        }
    }
}
