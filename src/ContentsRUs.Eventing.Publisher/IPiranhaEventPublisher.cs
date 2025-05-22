namespace ContentsRUs.Eventing.Publisher
{
    public interface IPiranhaEventPublisher
    {
        Task PublishAsync<T>(T @event, string routingKey = "content.published");
    }
}
