namespace XRim.Simulation.Scenarios
{
    /// <summary>Which rules event a <see cref="ScenarioEventRecord"/> holds. The numbers are stored in files: never renumber them.</summary>
    public enum ScenarioEventKind
    {
        BodyMoveStarted = 0,
        AttackStarted = 1,
        SignatureUsed = 2,
        HitLanded = 3,
        WeaponClash = 4,
        ShieldBlock = 5,
        AttackInterrupted = 6,
        StatusApplied = 7,
        LimbSevered = 8,
        WeaponDropped = 9,
        FighterDied = 10,
        ElectricWall = 11,
        WeaponTraitHeal = 12,

        /// <summary>An event type this code does not know yet: only its kind, time and type name are kept.</summary>
        Other = 99,
    }
}
