namespace XRim.Networking.Protocol
{
    /// <summary>Planning is over for this turn (both Ready, or the timers ended); the plans are locked and the server simulates the turn.</summary>
    public sealed class PlanningLockedMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.PlanningLocked;

        public int TurnIndex { get; }

        public PlanningLockedMessage(int turnIndex)
        {
            TurnIndex = turnIndex;
        }
    }
}
