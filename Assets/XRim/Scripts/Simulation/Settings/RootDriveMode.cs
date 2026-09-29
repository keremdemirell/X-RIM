namespace XRim.Simulation.Settings
{
    /// <summary>How a dummy's torso follows its root target (standing still, or a body move).</summary>
    public enum RootDriveMode
    {
        /// <summary>
        /// The torso is a physics body pulled toward the root target by a strength-limited joint, so it stands
        /// upright but a hard hit can still knock it (physical comedy, pillar 4).
        /// </summary>
        Powered = 0,

        /// <summary>Fallback: the torso is moved exactly onto the root target and nothing can push it.</summary>
        Kinematic = 1,
    }
}
