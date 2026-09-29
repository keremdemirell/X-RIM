namespace XRim.Simulation.Settings
{
    /// <summary>
    /// How the held weapon follows its path. A pure feel choice (pillar 1); the designer decides it at PT1 (D1).
    /// Both keep the GDD §9 speed model: the target is always the path point at d = v·t.
    /// </summary>
    public enum WeaponDriverKind
    {
        /// <summary>The weapon is exactly on its path every step and pushes through whatever it meets.</summary>
        Kinematic = 0,

        /// <summary>A spring-damper force chases the path target, so impacts can slow or deflect the weapon.</summary>
        Motor = 1,
    }
}
