using ContentsRUs.Eventing.Publisher;
using Microsoft.Extensions.Hosting;

public class PiranhaPublisherInitializer : IHostedService
{
    private readonly IPiranhaEventPublisher _publisher;
    public PiranhaPublisherInitializer(IPiranhaEventPublisher publisher) => _publisher = publisher;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _publisher.InitializeAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
