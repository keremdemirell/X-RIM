namespace XRim.Rules.Planning
{
    public sealed class SetBodyMoveCommand : PlanningCommand
    {
        public BodyMove Move { get; }

        public SetBodyMoveCommand(BodyMove move)
        {
            Move = move;
        }
    }
}
