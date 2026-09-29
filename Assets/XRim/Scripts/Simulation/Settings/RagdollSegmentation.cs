namespace XRim.Simulation.Settings
{
    /// <summary>How many physics bodies a dummy has. The designer decides it at PT1 (D2). Hit zones are the same either way.</summary>
    public enum RagdollSegmentation
    {
        /// <summary>One body per BodyPart: head, torso, two arms, two legs.</summary>
        SixBodies = 6,

        /// <summary>Arms and legs split into upper and lower segments; both segments map to the same BodyPart.</summary>
        TenBodies = 10,
    }
}
