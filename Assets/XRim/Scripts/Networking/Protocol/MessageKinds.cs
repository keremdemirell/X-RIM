namespace XRim.Networking.Protocol
{
    /// <summary>Which way each <see cref="MessageKind"/> travels.</summary>
    public static class MessageKinds
    {
        /// <summary>The first kind a server sends; every kind below it is sent by a client.</summary>
        private const byte FirstServerKind = (byte)MessageKind.MatchFound;

        /// <summary>A kind a client sends. The server refuses every other kind from a client: a turn's result, a planning window, a verdict.</summary>
        public static bool IsSentByClient(MessageKind kind) => (byte)kind < FirstServerKind;

        /// <summary>A kind this version of the protocol knows.</summary>
        public static bool IsKnown(MessageKind kind)
        {
            switch (kind)
            {
                case MessageKind.ClientHello:
                case MessageKind.LoadoutPick:
                case MessageKind.PlanningCommand:
                case MessageKind.PlaybackFinished:
                case MessageKind.Ping:
                case MessageKind.MatchFound:
                case MessageKind.MatchStarted:
                case MessageKind.PlanningStarted:
                case MessageKind.PublicState:
                case MessageKind.CommandAnswer:
                case MessageKind.PlanState:
                case MessageKind.PlanningLocked:
                case MessageKind.TurnResolved:
                case MessageKind.MatchEnded:
                case MessageKind.ConnectionNotice:
                case MessageKind.Pong:
                case MessageKind.Goodbye:
                    return true;
                default:
                    return false;
            }
        }
    }
}
