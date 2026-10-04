using UnityEngine;
using XRim.Core.Gdd;
using XRim.Rules;

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

        [Tooltip("D14, GDD §5: no diagonal swipes. A flick may be this many degrees off a straight up, down or sideways line and " +
                 "still count; one pointing between two directions does nothing. 45 leaves no gap. Not in the GDD.")]
        [GddTbd("§5", "Diagonal or combined swipes", Proposal = "Not allowed")]
        [Placeholder("Technical value: the width of the gap between swipe directions is not in the GDD (D14)")]
        [Range(0f, 45f)]
        [SerializeField] private float _swipeAxisToleranceDegrees = 30f;

        [Tooltip("A finger that stays this close to where it landed, as a fraction of screen height, has not moved: it tapped. " +
                 "A tap in the weapon zone raises the item where it was tapped (a shield); a quick tap in the body zone clears the body move. " +
                 "Not in the GDD.")]
        [Placeholder("Technical value: finger jitter allowance for a tap is not in the GDD")]
        [Range(0f, 0.1f)]
        [SerializeField] private float _tapMaxTravelScreenFraction = 0.01f;

        [Tooltip("GDD §4–5: the dual-zone scheme's feel is TBD. When on, a quick tap in the body zone sets the body move back to " +
                 "neutral (otherwise only a new swipe replaces it).")]
        [GddTbd("§4–5", "Overall feel of the dual-zone scheme", Proposal = "Validate in prototype")]
        [SerializeField] private bool _tapInBodyZoneClearsMove = true;

        [Tooltip("D24, GDD §4: developer stand-in for the first-launch handedness pick (Session 18). Left mirrors the screen " +
                 "layout (own dummy and body zone on the right). The sandbox toggles it live; the dummy's weapon hand is not " +
                 "affected until the real pick exists.")]
        [GddTbd("§4", "Can handedness be changed later? Separate mirrored-layout toggle", Proposal = "Changeable in settings; no separate toggle")]
        [Placeholder("Developer setting until the first-launch pick exists (Session 18)")]
        [SerializeField] private Handedness _devHandedness = Handedness.Right;

        [Tooltip("GDD §18: the reference resolution is TBD.")]
        [GddTbd("§18", "Arena unit scale and reference resolution")]
        [Placeholder("§18 reference resolution is TBD")]
        [SerializeField] private Vector2Int _referenceResolution = new Vector2Int(1920, 1080);

        public float BodyZoneWidthFraction => _bodyZoneWidthFraction;
        public float SwipeWindowSeconds => _swipeWindowSeconds;
        public float MinSwipeDistanceScreenFraction => _minSwipeDistanceScreenFraction;
        public float SwipeAxisToleranceDegrees => _swipeAxisToleranceDegrees;
        public float TapMaxTravelScreenFraction => _tapMaxTravelScreenFraction;
        public bool TapInBodyZoneClearsMove => _tapInBodyZoneClearsMove;
        public Handedness DevHandedness => _devHandedness;
        public Vector2Int ReferenceResolution => _referenceResolution;
    }
}
