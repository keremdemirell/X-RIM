using XRim.Core;
using XRim.Rules.Planning;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// A side's public planning state changed: a weapon switch is shown at once (GDD §6), Ready is shown (designer 2026-09-29), and a charged
    /// signature move only when that is shown (§8). Body moves, paths and the chosen signature move never travel during planning.
    /// </summary>
    public sealed class PublicStateMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.PublicState;

        public Side Side { get; }

        public PublicPlanningState State { get; }

        public PublicStateMessage(Side side, PublicPlanningState state)
        {
            Side = side;
            State = state;
        }
    }
}
