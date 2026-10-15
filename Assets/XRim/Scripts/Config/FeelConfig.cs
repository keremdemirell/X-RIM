using UnityEngine;
using XRim.Core.Gdd;

namespace XRim.Config
{
    /// <summary>
    /// Presentation-only feel tuning (GDD pillar 1). These values change how a recorded turn is played back, framed and shown,
    /// never what happened in it, so they can be pushed hard without affecting fairness or sync. None of them are GDD values.
    /// Session 13: hitstops scaled by what happened, slow motion that starts just before a big moment, camera framing, shake and
    /// punch-in (GDD §2 camera behaviour is TBD: the fixed side view stays the default, the rest is switchable here).
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Client + "Feel", fileName = "Feel")]
    public sealed class FeelConfig : ScriptableObject
    {
        private const string FeelPassPlaceholder = "Session 13 feel pass: no GDD value, tuned at PT5";

        /// <summary>The debris sizes a new Feel asset starts with (the art spec written without the Editor reads them too).</summary>
        public const float DefaultDebrisSizeUnits = 9f;
        public const float DefaultFluidDropSizeUnits = 7f;
        public const float DefaultSparkSizeUnits = 7f;

        /// <summary>The duel framing and replay zoom a new Feel asset starts with (the art spec's pixel advice is worked out from them).</summary>
        public const float DefaultDuelViewHeightUnits = 700f;
        public const float DefaultReplayZoomViewFraction = 0.65f;
        public const float DefaultFramingFloorMarginUnits = 90f;
        public const float DefaultFramingEdgeMarginUnits = 60f;

        // ----- Hitstop: a short freeze of playback when something lands (real seconds) -----

        [Header("Hitstop (real seconds)")]
        [Tooltip("The freeze of the lightest landed hit. Every point of damage adds the per-damage time below.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _hitStopBaseSeconds = 0.05f;

        [Tooltip("Extra freeze per point of damage a hit deals (a 20-damage mace hit freezes longer than an 8-damage rapier poke).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _hitStopSecondsPerDamage = 0.005f;

        [Tooltip("A head hit's freeze is multiplied by this.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _headHitStopMultiplier = 1.5f;

        [Tooltip("No ordinary hit freezes longer than this.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _maxHitStopSeconds = 0.16f;

        [Tooltip("The freeze when a hit kills (or any death: the torso splits).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _killHitStopSeconds = 0.22f;

        [Tooltip("The freeze when a limb is severed.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _severHitStopSeconds = 0.12f;

        [Tooltip("The freeze when a weapon crushes through another (GDD §10).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _crushHitStopSeconds = 0.1f;

        [Tooltip("The freeze when two weapons rebound in sparks, or one deflects the other (GDD §10).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _reboundHitStopSeconds = 0.07f;

        [Tooltip("The freeze of a full shield block (GDD §7).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _fullBlockHitStopSeconds = 0.06f;

        [Tooltip("The freeze of a partial shield block (GDD §7).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _partialBlockHitStopSeconds = 0.04f;

        [Tooltip("The freeze of an electric wall zap (GDD §13).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _wallZapHitStopSeconds = 0.1f;

        // ----- Slow motion: every turn is simulated before it is shown, so playback can slow down just BEFORE a big moment -----

        [Header("Slow motion (speed: 1 = real time)")]
        [Tooltip("Playback speed while a sever plays out.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _severSlowMotionSpeed = 0.25f;

        [Tooltip("How long the slowed stretch around a sever lasts on screen, in real seconds.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _severSlowMotionRealSeconds = 1f;

        [Tooltip("The share of the slowed stretch that comes before the sever (the anticipation).")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0f, 1f)]
        [SerializeField] private float _severSlowMotionLeadFraction = 0.3f;

        [Tooltip("Playback speed around a death (a KO: the torso splits).")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0.05f, 1f)]
        [SerializeField] private float _knockOutSlowMotionSpeed = 0.2f;

        [Tooltip("How long the slowed stretch around a death lasts on screen, in real seconds.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _knockOutSlowMotionRealSeconds = 1.4f;

        [Tooltip("The share of the slowed stretch that comes before the death.")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0f, 1f)]
        [SerializeField] private float _knockOutSlowMotionLeadFraction = 0.25f;

        [Tooltip("Playback speed around an interrupt counter-hit (a hit that cancels the other dummy's attack, GDD §9).")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0.05f, 1f)]
        [SerializeField] private float _counterHitSlowMotionSpeed = 0.35f;

        [Tooltip("How long the slowed stretch around a counter-hit lasts on screen, in real seconds.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _counterHitSlowMotionRealSeconds = 0.6f;

        [Tooltip("The share of the slowed stretch that comes before the counter-hit.")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0f, 1f)]
        [SerializeField] private float _counterHitSlowMotionLeadFraction = 0.4f;

        [Tooltip("Playback speed around the decisive moment of a sudden-death turn (GDD §14: the first clean hit wins).")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0.05f, 1f)]
        [SerializeField] private float _suddenDeathFinishSlowMotionSpeed = 0.12f;

        [Tooltip("How long the slowed stretch around a sudden-death finish lasts on screen, in real seconds.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _suddenDeathFinishSlowMotionRealSeconds = 1.6f;

        [Tooltip("The share of the slowed stretch that comes before the sudden-death finish.")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0f, 1f)]
        [SerializeField] private float _suddenDeathFinishSlowMotionLeadFraction = 0.4f;

        [Tooltip("How long playback takes to slow down into a slowed stretch and to speed up out of it, in seconds of the turn.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _slowMotionRampTurnSeconds = 0.04f;

        // ----- Camera (GDD §2: camera behaviour is TBD; the fixed side view stays the default) -----

        [Header("Camera framing")]
        [Tooltip("GDD §2 (TBD): Whole arena = the fixed view of the whole arena; Duel = a closer fixed side view centred between the " +
                 "fighters, set at the start of each turn and settling after playback, never moving during planning (designer 2026-10-11).")]
        [GddTbd("§2", "Camera behaviour", Proposal = "Fixed side view")]
        [SerializeField] private CameraFramingMode _cameraFraming = CameraFramingMode.Duel;

        [Tooltip("Duel framing: how many arena units the screen height shows (the whole-arena view shows 1150). Lower = bigger dummies.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _duelViewHeightUnits = DefaultDuelViewHeightUnits;

        [Tooltip("Duel framing: arena units kept in view behind each fighter's pelvis (its back, a knockback, its electric wall). " +
                 "The view widens when the fighters stand too far apart for this.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _framingMarginBehindUnits = 160f;

        [Tooltip("Duel framing: arena units shown below the floor line (room for the HP bars).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _framingFloorMarginUnits = DefaultFramingFloorMarginUnits;

        [Tooltip("Duel framing: how far past the arena's edge the view may show, in arena units.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _framingEdgeMarginUnits = DefaultFramingEdgeMarginUnits;

        [Tooltip("Duel framing: how long the camera takes to settle on the next turn's framing after playback, in real seconds.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _framingGlideSeconds = 0.35f;

        [Header("Camera shake (a share of the screen height)")]
        [Tooltip("GDD §2 (TBD): shake the camera on impacts. It always returns to the framing before planning.")]
        [GddTbd("§2", "Camera behaviour", Proposal = "Fixed side view")]
        [SerializeField] private bool _shakeOnImpacts = true;

        [Tooltip("The shake of the lightest landed hit, as a share of the screen height.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _shakeBaseViewFraction = 0.003f;

        [Tooltip("Extra shake per point of damage a hit deals.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _shakePerDamageViewFraction = 0.0008f;

        [Tooltip("No shake is stronger than this; a death and a sever shake this hard.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _maxShakeViewFraction = 0.025f;

        [Tooltip("The shake of a crush-through (GDD §10).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _crushShakeViewFraction = 0.016f;

        [Tooltip("The shake of a rebound, a deflection or a full shield block.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _clashShakeViewFraction = 0.008f;

        [Tooltip("The shake of an electric wall zap.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _wallZapShakeViewFraction = 0.018f;

        [Tooltip("How long a shake lasts, fading out, in real seconds.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _shakeSeconds = 0.25f;

        [Tooltip("How fast the camera trembles.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _shakeFrequencyHz = 22f;

        [Header("Camera punch-in")]
        [Tooltip("GDD §2 (TBD): a quick zoom toward big moments (crush, counter-hit, sever, death), then back.")]
        [GddTbd("§2", "Camera behaviour", Proposal = "Fixed side view")]
        [SerializeField] private bool _punchInOnBigMoments = true;

        [Tooltip("A punch-in shrinks the view to this share of its size, around the moment (crush, counter-hit).")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0.3f, 1f)]
        [SerializeField] private float _punchInViewFraction = 0.86f;

        [Tooltip("The same for the biggest moments (a sever, a death).")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0.3f, 1f)]
        [SerializeField] private float _bigPunchInViewFraction = 0.78f;

        [Tooltip("How long the zoom in takes, in real seconds.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _punchInSeconds = 0.06f;

        [Tooltip("How long the camera stays zoomed in, in real seconds.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _punchHoldSeconds = 0.18f;

        [Tooltip("How long the zoom back out takes, in real seconds.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _punchOutSeconds = 0.35f;

        [Header("Instant replay")]
        [Tooltip("GDD §17 (TBD; designer 2026-10-11: a local instant replay only): after a legendary turn plays, it plays again at once in slow " +
                 "motion with the camera on the moment; a tap skips it. Nothing is saved or shared.")]
        [GddTbd("§17", "Replays and clip sharing", Proposal = "Local instant replay only")]
        [SerializeField] private bool _autoReplayLegendaryTurns = true;

        [Tooltip("Replay every KO, the match-winning blow (designer 2026-10-11), and a double KO.")]
        [SerializeField] private bool _replayKnockOuts = true;

        [SerializeField] private bool _replaySuddenDeathFinishes = true;
        [SerializeField] private bool _replaySevers = true;

        [Tooltip("The §1 reference moment: a duck, jump or step back out of the other's swing, then a hit, taking none.")]
        [SerializeField] private bool _replayDodgeCounters = true;

        [SerializeField] private bool _replayCounterHits = true;
        [SerializeField] private bool _replayTrades = true;
        [SerializeField] private bool _replayCrushes = true;

        [Tooltip("The replay starts this long before the moment, in seconds of the turn.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _replayLeadSeconds = 0.35f;

        [Tooltip("And ends this long after it, in seconds of the turn.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _replayTailSeconds = 0.45f;

        [Tooltip("The replay plays at this speed at most (the moment's own slow motion still goes slower).")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0.05f, 1f)]
        [SerializeField] private float _replaySpeed = 0.4f;

        [Tooltip("The replay's camera closes in on the moment: the view shrinks to this share of its size.")]
        [Placeholder(FeelPassPlaceholder)]
        [Range(0.3f, 1f)]
        [SerializeField] private float _replayZoomViewFraction = DefaultReplayZoomViewFraction;

        [Tooltip("How long the replay's camera takes to close in, in real seconds.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _replayZoomSeconds = 0.4f;

        [Header("Playback")]
        [Tooltip("Default execution playback speed (1 = real time).")]
        [SerializeField] private float _defaultPlaybackSpeed = 1f;

        // ----- Debris (Session 11 placeholder spray) -----

        [Header("Debris")]
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
        [SerializeField] private float _debrisSizeUnits = DefaultDebrisSizeUnits;

        [Tooltip("A drop of fluid's size, in arena units.")]
        [Placeholder("§2/§12: the debris spray has no numbers yet (Session 13 polishes it)")]
        [SerializeField] private float _fluidDropSizeUnits = DefaultFluidDropSizeUnits;

        [Tooltip("The share of its speed a piece keeps each time it bounces on the floor.")]
        [Placeholder("§2/§12: the debris spray has no numbers yet (Session 13 polishes it)")]
        [Range(0f, 1f)]
        [SerializeField] private float _debrisBounceFraction = 0.35f;

        [Tooltip("Springs and gears popping out of a severed joint, besides the bolts and nuts.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private int _severSprings = 3;

        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private int _severGears = 1;

        [Tooltip("A splitting torso (a death) sprays this many times what a sever does, plus puffs of steam.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _deathBurstMultiplier = 2f;

        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private int _deathSteamPuffs = 5;

        [Header("Sparks and impacts")]
        [Tooltip("Sparks off a landed hit: this many, plus the per-damage share, up to the most.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private int _hitSparksBase = 6;

        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _hitSparksPerDamage = 0.5f;

        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private int _maxHitSparks = 24;

        [Tooltip("A hit at least this strong also knocks bolts and nuts loose.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _heavyHitMinDamage = 15f;

        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private int _heavyHitDebrisPieces = 3;

        [Tooltip("The shower of sparks when two weapons rebound (GDD §10: 'both weapons rebound in a shower of sparks').")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private int _clashSparks = 24;

        [Tooltip("Bolts and nuts knocked loose by a crush-through.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private int _crushDebrisPieces = 4;

        [Tooltip("How fast sparks fly, in arena units per second.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _sparkSpeedUnitsPerSecond = 650f;

        [Tooltip("How long a spark lives, in seconds of the turn (slow motion and hitstops stretch it).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _sparkSeconds = 0.28f;

        [Tooltip("A spark's width, in arena units (it stretches along its flight).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _sparkSizeUnits = DefaultSparkSizeUnits;

        [Tooltip("At most this many effect pieces at once; past it, the oldest are reused.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private int _maxVfxPieces = 260;

        [Header("Struck dummy (drawing only)")]
        [Tooltip("How long a struck part flashes white, in seconds of the turn.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _partFlashSeconds = 0.12f;

        [Tooltip("The struck dummy's drawing rocks away from the blow by this many degrees per point of damage, up to the most; the " +
                 "physics underneath does not move.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _flinchDegreesPerDamage = 0.4f;

        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _maxFlinchDegrees = 9f;

        [Tooltip("And is pushed back this far per point of damage (arena units), up to the most.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _flinchPushUnitsPerDamage = 0.6f;

        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _maxFlinchPushUnits = 12f;

        [Tooltip("How long the flinch takes to spring back, in seconds of the turn.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _flinchSeconds = 0.3f;

        [Header("Weapon trails")]
        [Tooltip("A fading streak behind each blade tip while it swings: how much of the swing it shows, in seconds of the turn (0 = no trails).")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _trailSeconds = 0.09f;

        [Tooltip("The trail's width at the tip, as a multiple of the weapon's own width.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _trailWidthMultiplier = 1.6f;

        [Tooltip("A tip moving slower than this (arena units per second) leaves no trail.")]
        [Placeholder(FeelPassPlaceholder)]
        [SerializeField] private float _trailMinSpeedUnitsPerSecond = 200f;

        public float HitStopBaseSeconds => _hitStopBaseSeconds;
        public float HitStopSecondsPerDamage => _hitStopSecondsPerDamage;
        public float HeadHitStopMultiplier => _headHitStopMultiplier;
        public float MaxHitStopSeconds => _maxHitStopSeconds;
        public float KillHitStopSeconds => _killHitStopSeconds;
        public float SeverHitStopSeconds => _severHitStopSeconds;
        public float CrushHitStopSeconds => _crushHitStopSeconds;
        public float ReboundHitStopSeconds => _reboundHitStopSeconds;
        public float FullBlockHitStopSeconds => _fullBlockHitStopSeconds;
        public float PartialBlockHitStopSeconds => _partialBlockHitStopSeconds;
        public float WallZapHitStopSeconds => _wallZapHitStopSeconds;

        public float SeverSlowMotionSpeed => _severSlowMotionSpeed;
        public float SeverSlowMotionRealSeconds => _severSlowMotionRealSeconds;
        public float SeverSlowMotionLeadFraction => _severSlowMotionLeadFraction;
        public float KnockOutSlowMotionSpeed => _knockOutSlowMotionSpeed;
        public float KnockOutSlowMotionRealSeconds => _knockOutSlowMotionRealSeconds;
        public float KnockOutSlowMotionLeadFraction => _knockOutSlowMotionLeadFraction;
        public float CounterHitSlowMotionSpeed => _counterHitSlowMotionSpeed;
        public float CounterHitSlowMotionRealSeconds => _counterHitSlowMotionRealSeconds;
        public float CounterHitSlowMotionLeadFraction => _counterHitSlowMotionLeadFraction;
        public float SuddenDeathFinishSlowMotionSpeed => _suddenDeathFinishSlowMotionSpeed;
        public float SuddenDeathFinishSlowMotionRealSeconds => _suddenDeathFinishSlowMotionRealSeconds;
        public float SuddenDeathFinishSlowMotionLeadFraction => _suddenDeathFinishSlowMotionLeadFraction;
        public float SlowMotionRampTurnSeconds => _slowMotionRampTurnSeconds;

        public CameraFramingMode CameraFraming => _cameraFraming;
        public float DuelViewHeightUnits => _duelViewHeightUnits;
        public float FramingMarginBehindUnits => _framingMarginBehindUnits;
        public float FramingFloorMarginUnits => _framingFloorMarginUnits;
        public float FramingEdgeMarginUnits => _framingEdgeMarginUnits;
        public float FramingGlideSeconds => _framingGlideSeconds;
        public bool ShakeOnImpacts => _shakeOnImpacts;
        public float ShakeBaseViewFraction => _shakeBaseViewFraction;
        public float ShakePerDamageViewFraction => _shakePerDamageViewFraction;
        public float MaxShakeViewFraction => _maxShakeViewFraction;
        public float CrushShakeViewFraction => _crushShakeViewFraction;
        public float ClashShakeViewFraction => _clashShakeViewFraction;
        public float WallZapShakeViewFraction => _wallZapShakeViewFraction;
        public float ShakeSeconds => _shakeSeconds;
        public float ShakeFrequencyHz => _shakeFrequencyHz;
        public bool PunchInOnBigMoments => _punchInOnBigMoments;
        public float PunchInViewFraction => _punchInViewFraction;
        public float BigPunchInViewFraction => _bigPunchInViewFraction;
        public float PunchInSeconds => _punchInSeconds;
        public float PunchHoldSeconds => _punchHoldSeconds;
        public float PunchOutSeconds => _punchOutSeconds;

        public bool AutoReplayLegendaryTurns => _autoReplayLegendaryTurns;
        public bool ReplayKnockOuts => _replayKnockOuts;
        public bool ReplaySuddenDeathFinishes => _replaySuddenDeathFinishes;
        public bool ReplaySevers => _replaySevers;
        public bool ReplayDodgeCounters => _replayDodgeCounters;
        public bool ReplayCounterHits => _replayCounterHits;
        public bool ReplayTrades => _replayTrades;
        public bool ReplayCrushes => _replayCrushes;
        public float ReplayLeadSeconds => _replayLeadSeconds;
        public float ReplayTailSeconds => _replayTailSeconds;
        public float ReplaySpeed => _replaySpeed;
        public float ReplayZoomViewFraction => _replayZoomViewFraction;
        public float ReplayZoomSeconds => _replayZoomSeconds;

        public float DefaultPlaybackSpeed => _defaultPlaybackSpeed;
        public int DebrisPieces => _debrisPieces;
        public int FluidDrops => _fluidDrops;
        public float DebrisSpeedUnitsPerSecond => _debrisSpeedUnitsPerSecond;
        public float DebrisSizeUnits => _debrisSizeUnits;
        public float FluidDropSizeUnits => _fluidDropSizeUnits;
        public float DebrisBounceFraction => _debrisBounceFraction;
        public int SeverSprings => _severSprings;
        public int SeverGears => _severGears;
        public float DeathBurstMultiplier => _deathBurstMultiplier;
        public int DeathSteamPuffs => _deathSteamPuffs;
        public int HitSparksBase => _hitSparksBase;
        public float HitSparksPerDamage => _hitSparksPerDamage;
        public int MaxHitSparks => _maxHitSparks;
        public float HeavyHitMinDamage => _heavyHitMinDamage;
        public int HeavyHitDebrisPieces => _heavyHitDebrisPieces;
        public int ClashSparks => _clashSparks;
        public int CrushDebrisPieces => _crushDebrisPieces;
        public float SparkSpeedUnitsPerSecond => _sparkSpeedUnitsPerSecond;
        public float SparkSeconds => _sparkSeconds;
        public float SparkSizeUnits => _sparkSizeUnits;
        public int MaxVfxPieces => _maxVfxPieces;
        public float PartFlashSeconds => _partFlashSeconds;
        public float FlinchDegreesPerDamage => _flinchDegreesPerDamage;
        public float MaxFlinchDegrees => _maxFlinchDegrees;
        public float FlinchPushUnitsPerDamage => _flinchPushUnitsPerDamage;
        public float MaxFlinchPushUnits => _maxFlinchPushUnits;
        public float FlinchSeconds => _flinchSeconds;
        public float TrailSeconds => _trailSeconds;
        public float TrailWidthMultiplier => _trailWidthMultiplier;
        public float TrailMinSpeedUnitsPerSecond => _trailMinSpeedUnitsPerSecond;
    }
}
