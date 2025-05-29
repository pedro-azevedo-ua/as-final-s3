namespace ContentsRUs.Eventing.Publisher
{
    public interface IPiranhaEventPublisher
    {
        Task InitializeAsync();
        Task PublishAsync<T>(T @event, string routingKey);
    }
}
