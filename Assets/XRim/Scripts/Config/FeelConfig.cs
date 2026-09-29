using UnityEngine;

namespace XRim.Config
{
    /// <summary>
    /// Presentation-only feel tuning (GDD pillar 1). These values change how a recorded turn is played back,
    /// never what happened in it, so they can be pushed hard without affecting fairness or sync.
    /// None of them are GDD values.
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Client + "Feel", fileName = "Feel")]
    public sealed class FeelConfig : ScriptableObject
    {
        [Tooltip("Playback freeze on a landed hit, in real seconds.")]
        [SerializeField] private float _hitStopSeconds = 0.06f;

        [Tooltip("Playback speed while a sever plays out (1 = real time).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _severSlowMotionSpeed = 0.25f;

        [Tooltip("How long the sever slow motion lasts, in real seconds.")]
        [SerializeField] private float _severSlowMotionSeconds = 0.6f;

        [Tooltip("Camera shake amplitude on heavy hits, in world units.")]
        [SerializeField] private float _heavyHitShakeWorldUnits = 0.15f;

        [Tooltip("Default execution playback speed (1 = real time).")]
        [SerializeField] private float _defaultPlaybackSpeed = 1f;

        public float HitStopSeconds => _hitStopSeconds;
        public float SeverSlowMotionSpeed => _severSlowMotionSpeed;
        public float SeverSlowMotionSeconds => _severSlowMotionSeconds;
        public float HeavyHitShakeWorldUnits => _heavyHitShakeWorldUnits;
        public float DefaultPlaybackSpeed => _defaultPlaybackSpeed;
    }
}
