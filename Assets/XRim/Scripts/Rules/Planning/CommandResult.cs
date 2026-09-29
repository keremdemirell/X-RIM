namespace XRim.Rules.Planning
{
    public readonly struct CommandResult
    {
        public CommandRejection Rejection { get; }
        public bool Accepted => Rejection == CommandRejection.None;

        private CommandResult(CommandRejection rejection)
        {
            Rejection = rejection;
        }

        public static CommandResult Ok => new CommandResult(CommandRejection.None);

        public static CommandResult Reject(CommandRejection rejection) => new CommandResult(rejection);
    }
}
