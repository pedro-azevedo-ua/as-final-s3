namespace ContentsRUs.Eventing.Models;

public class DomainEvent<TPayload>
{
    public Guid EventId { get; set; }
    public string ModelName { get; set; }      // e.g. "Page", "BlogPost", "Product"
    public string Action { get; set; }         // e.g. "Created", "Updated", "Deleted"
    public EventDetails Payload { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}