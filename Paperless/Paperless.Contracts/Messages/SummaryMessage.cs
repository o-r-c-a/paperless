namespace Paperless.Contracts.Messages
{
    public class SummaryMessage
    {
        public Guid Id { get; init; }           
        public string Name { get; init; } = "";  
        public string Summary { get; init; } = "";
    }
}
