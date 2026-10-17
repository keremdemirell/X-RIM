namespace XRim.Rules.Planning
{
    public enum CommandRejection
    {
        None = 0,
        NotInPlanningPhase = 1,
        /// <summary>Not in the loadout or its extra options, or not available this turn (lost, or no arm left to hold it, §12).</summary>
        WeaponNotInLoadout = 2,
        WeaponSwitchLockedOut = 3,
        BodyMoveNotAllowed = 4,
        InkBudgetExceeded = 5,
        InvalidPath = 6,
        SignatureUnavailable = 7,
        ReadyCancelNotAllowed = 8,
        DrawingLockedOut = 9,

        /// <summary>Ready is pressed: the plan is frozen until Ready is cancelled (GDD §3).</summary>
        AlreadyReady = 10,

        /// <summary>The signature move exists but is not charged: the once-per-match use is spent, or the bar is not full (GDD §8).</summary>
        SignatureNotCharged = 11,

        /// <summary>The signature move is tied to other weapons than the one in hand (GDD §8, TBD: by default a move works with any weapon).</summary>
        SignatureWrongWeapon = 12,
    }
}
