using XRim.Core;
using XRim.Networking;
using XRim.Rules.Planning;

namespace XRim.Input
{
    /// <summary>Passes every command on to the real sink and tells the <see cref="PlanDraft"/> what the rules made of it.</summary>
    public sealed class DraftTrackingSink : IPlanningCommandSink
    {
        private readonly IPlanningCommandSink _inner;
        private readonly PlanDraft _draft;

        public DraftTrackingSink(IPlanningCommandSink inner, PlanDraft draft)
        {
            _inner = Guard.NotNull(inner, nameof(inner));
            _draft = Guard.NotNull(draft, nameof(draft));
        }

        public CommandResult Send(PlanningCommand command)
        {
            CommandResult result = _inner.Send(command);
            _draft.Record(command, result);
            return result;
        }
    }
}
