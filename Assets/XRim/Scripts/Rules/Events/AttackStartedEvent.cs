using XRim.Core;

namespace XRim.Rules.Events
{
    /// <summary>
    /// A dummy's weapon (or shield, club, headbutt) starts along its drawn path, when execution starts (GDD §3, §6; Session 13). It carries
    /// the player's input, like <see cref="BodyMoveStartedEvent"/>, so the turn's result tells everyone, a remote client included, which
    /// side swung and with what. Playback hooks the swing's whoosh and the attacker's effort grunt on it (§2), and the moment detector
    /// reads that the opponent swung. Never read by the rules: it changes no outcome.
    /// </summary>
    public sealed class AttackStartedEvent : MatchEvent
    {
        public Side Side { get; }
        public WeaponId Weapon { get; }

        /// <summary>What kind of item swings: a weapon, a shield, a club, a body blow.</summary>
        public WeaponKind Kind { get; }

        public AttackStartedEvent(SimTime time, Side side, WeaponId weapon, WeaponKind kind) : base(time)
        {
            Side = side;
            Weapon = weapon;
            Kind = kind;
        }
    }
}
