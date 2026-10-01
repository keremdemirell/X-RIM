using XRim.Simulation.Physics;

namespace XRim.Networking
{
    /// <summary>Optional extras for a <see cref="LocalTurnAuthority"/>. Every default suits a headless match.</summary>
    public sealed class LocalTurnAuthorityOptions
    {
        /// <summary>How long to wait after a turn resolves before the next planning timer starts. Default: not at all.</summary>
        public PlaybackHoldSettings PlaybackHold { get; set; } = new PlaybackHoldSettings();

        /// <summary>Where each weapon tip rests. Default: read from the frozen pose (<see cref="PoseWeaponTipLocator"/>).</summary>
        public IWeaponTipLocator WeaponTips { get; set; }

        /// <summary>
        /// The pose both dummies start the match in. Default: <see cref="StartingBoard"/> (the guard stance at the starting
        /// gap, each holding its first loadout weapon).
        /// </summary>
        public PoseSnapshot InitialPose { get; set; }
    }
}
