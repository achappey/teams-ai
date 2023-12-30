namespace Microsoft.Teams.AI.AI.Planners
{
    internal sealed class ParsedCommandResult
    {
        public int Length { get; set; }
        public IPredictedCommand Command { get; set; }
        public ParsedCommandResult(int length, IPredictedCommand command)
        {
            this.Length = length;
            this.Command = command;
        }
    }
}
