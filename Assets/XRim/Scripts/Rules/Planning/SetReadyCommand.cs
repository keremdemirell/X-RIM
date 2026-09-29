namespace XRim.Rules.Planning
{
    /// <summary>Press Ready, or cancel it when <c>MatchSettings.AllowReadyCancel</c> permits (GDD §3, TBD).</summary>
    public sealed class SetReadyCommand : PlanningCommand
    {
        public bool IsReady { get; }

        public SetReadyCommand(bool isReady)
        {
            IsReady = isReady;
        }
    }
}
