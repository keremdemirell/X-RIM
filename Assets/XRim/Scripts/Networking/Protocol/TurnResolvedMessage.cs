using XRim.Core;
using XRim.Simulation.Recording;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// The turn's result as the server simulated it (GDD §18, server-authoritative), the same for both players: every recorded frame (rounded
    /// finely, <see cref="PoseRounding"/>), every event with its exact numbers (raw contacts included: the hit effects find their impact point
    /// in them), the execution report and the exact final board the next planning phase starts from.
    /// </summary>
    public sealed class TurnResolvedMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.TurnResolved;

        public TurnResult Result { get; }

        public TurnResolvedMessage(TurnResult result)
        {
            Result = Guard.NotNull(result, nameof(result));
        }
    }
}
