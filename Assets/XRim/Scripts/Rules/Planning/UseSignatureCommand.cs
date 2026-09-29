namespace XRim.Rules.Planning
{
    /// <summary>Tapping a signature move button: its preset pattern replaces the drawn path (GDD §8).</summary>
    public sealed class UseSignatureCommand : PlanningCommand
    {
        public SignatureMoveId Signature { get; }

        public UseSignatureCommand(SignatureMoveId signature)
        {
            Signature = signature;
        }
    }
}
