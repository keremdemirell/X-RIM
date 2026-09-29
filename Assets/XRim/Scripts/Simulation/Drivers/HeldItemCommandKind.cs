namespace XRim.Simulation.Drivers
{
    public enum HeldItemCommandKind
    {
        /// <summary>Move the held item exactly onto a pose during the next step (kinematic body).</summary>
        MoveTo = 0,

        /// <summary>Accelerate the held item (dynamic body); it can be slowed or deflected by what it hits.</summary>
        Push = 1,
    }
}
