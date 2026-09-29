using UnityEngine;
using XRim.Core.Gdd;

namespace XRim.Config
{
    /// <summary>
    /// Screen-side input tuning. Nothing here may change an outcome: paths are converted to arena units and
    /// resampled by the rules, so screen size and touch rate never matter (GDD §4 input fairness).
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Client + "Input", fileName = "Input")]
    public sealed class InputConfig : ScriptableObject
    {
        [Tooltip("GDD §4: the body zone covers about 15% of the screen width over the player's own dummy. Tunable.")]
        [Range(0.05f, 0.4f)]
        [SerializeField] private float _bodyZoneWidthFraction = 0.15f;

        [Tooltip("GDD §5: a stance swipe takes about 0.3 s. Tunable.")]
        [SerializeField] private float _swipeWindowSeconds = 0.3f;

        [Tooltip("Minimum finger travel, as a fraction of screen height, for a flick to count as a swipe. Technical value, not in the GDD.")]
        [Placeholder("Technical value: minimum swipe distance is not in the GDD")]
        [SerializeField] private float _minSwipeDistanceScreenFraction = 0.03f;

        [Tooltip("GDD §18: the reference resolution is TBD.")]
        [GddTbd("§18", "Arena unit scale and reference resolution")]
        [Placeholder("§18 reference resolution is TBD")]
        [SerializeField] private Vector2Int _referenceResolution = new Vector2Int(1920, 1080);

        public float BodyZoneWidthFraction => _bodyZoneWidthFraction;
        public float SwipeWindowSeconds => _swipeWindowSeconds;
        public float MinSwipeDistanceScreenFraction => _minSwipeDistanceScreenFraction;
        public Vector2Int ReferenceResolution => _referenceResolution;
    }
}
