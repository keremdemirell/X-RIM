namespace XRim.Rules.Planning
{
    public enum CommandRejection
    {
        None = 0,
        NotInPlanningPhase = 1,
        WeaponNotInLoadout = 2,
        WeaponSwitchLockedOut = 3,
        BodyMoveNotAllowed = 4,
        InkBudgetExceeded = 5,
        InvalidPath = 6,
        SignatureUnavailable = 7,
        ReadyCancelNotAllowed = 8,
        DrawingLockedOut = 9,
    }
}
