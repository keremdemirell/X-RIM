namespace XRim.Networking.Protocol
{
    /// <summary>
    /// The client finished playing this turn back, its instant replay included. The server opens the next planning phase once both players
    /// have (or the wait runs out), so a replay never eats planning time (ARCHITECTURE §5).
    /// </summary>
    public sealed class PlaybackFinishedMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.PlaybackFinished;

        public int TurnIndex { get; }

        public PlaybackFinishedMessage(int turnIndex)
        {
            TurnIndex = turnIndex;
        }
    }
}
