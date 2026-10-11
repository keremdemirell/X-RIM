using UnityEngine;
using XRim.Core.Gdd;

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

        [Tooltip("Bolts and nuts sprayed when a limb is severed or a torso splits (GDD §2, §12).")]
        [Placeholder("§2/§12: the debris spray has no numbers yet (Session 13 polishes it)")]
        [SerializeField] private int _debrisPieces = 10;

        [Tooltip("Drops of hydraulic fluid in the same spray.")]
        [Placeholder("§2/§12: the debris spray has no numbers yet (Session 13 polishes it)")]
        [SerializeField] private int _fluidDrops = 8;

        [Tooltip("How fast the spray leaves the joint, in arena units per second.")]
        [Placeholder("§2/§12: the debris spray has no numbers yet (Session 13 polishes it)")]
        [SerializeField] private float _debrisSpeedUnitsPerSecond = 420f;

        [Tooltip("A bolt's size, in arena units (a nut is a little bigger).")]
        [Placeholder("§2/§12: the debris spray has no numbers yet (Session 13 polishes it)")]
        [SerializeField] private float _debrisSizeUnits = 9f;

        [Tooltip("A drop of fluid's size, in arena units.")]
        [Placeholder("§2/§12: the debris spray has no numbers yet (Session 13 polishes it)")]
        [SerializeField] private float _fluidDropSizeUnits = 7f;

        [Tooltip("The share of its speed a piece keeps each time it bounces on the floor.")]
        [Placeholder("§2/§12: the debris spray has no numbers yet (Session 13 polishes it)")]
        [Range(0f, 1f)]
        [SerializeField] private float _debrisBounceFraction = 0.35f;

        public float HitStopSeconds => _hitStopSeconds;
        public float SeverSlowMotionSpeed => _severSlowMotionSpeed;
        public float SeverSlowMotionSeconds => _severSlowMotionSeconds;
        public float HeavyHitShakeWorldUnits => _heavyHitShakeWorldUnits;
        public float DefaultPlaybackSpeed => _defaultPlaybackSpeed;
        public int DebrisPieces => _debrisPieces;
        public int FluidDrops => _fluidDrops;
        public float DebrisSpeedUnitsPerSecond => _debrisSpeedUnitsPerSecond;
        public float DebrisSizeUnits => _debrisSizeUnits;
        public float FluidDropSizeUnits => _fluidDropSizeUnits;
        public float DebrisBounceFraction => _debrisBounceFraction;
    }
}
