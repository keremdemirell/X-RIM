using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using XRim.Core;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace XRim.Input
{
    /// <summary>
    /// Reads touches through the Input System's EnhancedTouch API (multi-finger touch with per-finger history) while enabled; the
    /// legacy UnityEngine.Input class is never used. With <see cref="MouseActsAsTouch"/> on (the Editor and desktop builds),
    /// touch simulation turns the mouse into a finger, so the whole scheme can be played with a mouse. Simulation switches the
    /// <c>Mouse</c> device off for everything else, which is why it is a switch and not always on.
    /// Each contact's history is read as well as its latest state, so the readings between two frames are not lost: a slow frame
    /// or a 60 Hz screen gives the same stroke as a 120 Hz one (GDD §4).
    /// </summary>
    public sealed class TouchInputReader : MonoBehaviour, ITouchSource
    {
        private readonly Dictionary<int, double> _lastSeenSecondsByTouch = new Dictionary<int, double>();
        private readonly HashSet<int> _endedTouches = new HashSet<int>();
        private readonly List<TouchSample> _fresh = new List<TouchSample>();
        [Tooltip("Turns the mouse into a finger (touch simulation) for the Editor and desktop builds. While on, Mouse.current " +
                 "reads nothing, so code that reads the mouse directly stops working.")]
        [SerializeField] private bool _mouseActsAsTouch;

        private bool _simulatingTouch;

        public bool MouseActsAsTouch
        {
            get => _mouseActsAsTouch;
            set
            {
                _mouseActsAsTouch = value;
                if (isActiveAndEnabled) ApplyTouchSimulation();
            }
        }

        public Vec2 ScreenSizePixels => new Vec2(Screen.width, Screen.height);

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            _lastSeenSecondsByTouch.Clear();
            _endedTouches.Clear();
            ApplyTouchSimulation();
        }

        private void OnDisable()
        {
            if (_simulatingTouch) TouchSimulation.Disable();
            _simulatingTouch = false;
            EnhancedTouchSupport.Disable();
        }

        private void ApplyTouchSimulation()
        {
            if (_mouseActsAsTouch && !_simulatingTouch)
            {
                TouchSimulation.Enable();
                _simulatingTouch = true;
            }
            else if (!_mouseActsAsTouch && _simulatingTouch)
            {
                TouchSimulation.Disable();
                _simulatingTouch = false;
            }
        }

        public void ReadInto(List<TouchSample> samples)
        {
            if (!EnhancedTouchSupport.enabled) return;

            var start = samples.Count;
            var active = Touch.activeTouches;
            for (int i = 0; i < active.Count; i++)
            {
                AddNewReadings(active[i], samples);
            }

            if (active.Count == 0)
            {
                _lastSeenSecondsByTouch.Clear();
                _endedTouches.Clear();
            }

            samples.Sort(start, samples.Count - start, ByTime.Instance);
        }

        /// <summary>The touch's readings newer than the last one seen, oldest first (the history runs newest first).</summary>
        private void AddNewReadings(Touch touch, List<TouchSample> samples)
        {
            int id = touch.touchId;

            // A contact that already ended is never read again, even if the Input System lists it once more.
            if (_endedTouches.Contains(id)) return;
            double lastSeen = _lastSeenSecondsByTouch.TryGetValue(id, out double seen) ? seen : double.NegativeInfinity;
            _fresh.Clear();

            var history = touch.history;
            for (int i = 0; i < history.Count; i++)
            {
                AddIfNew(history[i], id, lastSeen);
            }

            AddIfNew(touch, id, lastSeen);
            _fresh.Sort(ByTime.Instance);
            foreach (TouchSample sample in _fresh)
            {
                samples.Add(sample);
                lastSeen = System.Math.Max(lastSeen, sample.TimeSeconds);
                if (sample.Phase == TouchSamplePhase.Ended || sample.Phase == TouchSamplePhase.Canceled)
                {
                    _lastSeenSecondsByTouch.Remove(id);
                    _endedTouches.Add(id);
                    return;
                }
            }

            _lastSeenSecondsByTouch[id] = lastSeen;
        }

        private void AddIfNew(Touch reading, int id, double lastSeen)
        {
            if (reading.time <= lastSeen) return;
            TouchSamplePhase? phase = Phase(reading.phase);
            if (!phase.HasValue) return;

            Vector2 position = reading.screenPosition;
            _fresh.Add(new TouchSample(id, phase.Value, new Vec2(position.x, position.y), reading.time));
        }

        private static TouchSamplePhase? Phase(TouchPhase phase)
        {
            switch (phase)
            {
                case TouchPhase.Began: return TouchSamplePhase.Began;
                case TouchPhase.Moved:
                case TouchPhase.Stationary: return TouchSamplePhase.Moved;
                case TouchPhase.Ended: return TouchSamplePhase.Ended;
                case TouchPhase.Canceled: return TouchSamplePhase.Canceled;
                default: return null;
            }
        }

        private sealed class ByTime : IComparer<TouchSample>
        {
            public static readonly ByTime Instance = new ByTime();

            public int Compare(TouchSample a, TouchSample b)
            {
                int byTime = a.TimeSeconds.CompareTo(b.TimeSeconds);
                return byTime != 0 ? byTime : a.FingerId.CompareTo(b.FingerId);
            }
        }
    }
}
