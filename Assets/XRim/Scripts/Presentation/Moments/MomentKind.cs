namespace XRim.Presentation.Moments
{
    /// <summary>The kinds of "did you see that?" turns (GDD pillar 1) the instant replay shows, most important first.</summary>
    public enum MomentKind
    {
        /// <summary>The decisive moment of a sudden-death turn (§14: the first clean hit wins).</summary>
        SuddenDeathFinish = 0,

        /// <summary>Both dummies die in one turn (§3: a double KO goes to sudden death).</summary>
        DoubleKnockOut = 1,

        /// <summary>A dummy dies: the match-winning KO (designer 2026-10-11: always replayed).</summary>
        KnockOut = 2,

        /// <summary>A limb comes off (§12).</summary>
        Sever = 3,

        /// <summary>
        /// The §1 reference moment: a dummy ducks, jumps or slips back out of the other's swing and lands its own blow in the same turn,
        /// while the other lands nothing.
        /// </summary>
        DodgeCounter = 4,

        /// <summary>A hit cancels the other dummy's attack before it lands (§9), and that dummy lands nothing.</summary>
        CounterHit = 5,

        /// <summary>Both dummies land a hit at exactly the same instant.</summary>
        Trade = 6,

        /// <summary>A weapon crushes through the other (§10).</summary>
        Crush = 7,

        /// <summary>
        /// Not found by the detector: a replay asked for (the debug tools' "replay the last turn") of a turn without a legendary moment,
        /// centred on its hardest hit or its middle (<see cref="RequestedMoment"/>).
        /// </summary>
        Requested = 8,
    }
}
