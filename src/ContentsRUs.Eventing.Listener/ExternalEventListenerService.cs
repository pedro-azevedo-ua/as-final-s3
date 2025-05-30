using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Newtonsoft.Json;
using ContentsRUs.Eventing.Shared.Helpers;
using ContentsRUs.Eventing.Shared.Models;
using Microsoft.Extensions.Configuration;
using Piranha;
using Microsoft.Extensions.DependencyInjection;


namespace ContentsRUs.Eventing.Listener
{
    public class ExternalEventListenerService : BackgroundService, IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _listenerLogger;
        private readonly ConnectionFactory _factory;

        private IConnection _connection;
        private IChannel _channel;
        private AsyncEventingBasicConsumer _consumer;
        private readonly IConfiguration _config;
        //private readonly IApi _api;

        public ExternalEventListenerService(
        IServiceProvider serviceProvider,
            IConfiguration config,
            ILoggerFactory loggerFactory // <-- Change: inject ILoggerFactory instead of ILogger<ExternalEventListenerService>
        )
        {
            _serviceProvider = serviceProvider;
            _config = config;
            _listenerLogger = loggerFactory.CreateLogger("Eventing.Listener");

            _factory = new ConnectionFactory
            {
                UserName = config["RabbitMQ:UserName"] ?? "guest",
                Password = config["RabbitMQ:Password"] ?? "guest",
                VirtualHost = "/",
                HostName = config["RabbitMQ:HostName"] ?? "localhost",
                Port = config.GetValue<int>("RabbitMQ:Port", 5672),
            };

        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // 1) Connect
            _connection = await _factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync();

            // 2) Declare the “normal” exchange
            var reqEx = _config["RabbitMQ:RequestsExchange"] ?? "cms.requests";
            await _channel.ExchangeDeclareAsync(
                exchange: reqEx,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            // 3) Declare the DLX (dead-letter exchange) and DLQ
            var dlxName = _config["RabbitMQ:DeadLetterExchange"] ?? "cms.dlx";
            var dlqName = _config["RabbitMQ:DeadLetterQueue"] ?? "cms.dlq";
            var dlqRouting = _config["RabbitMQ:DeadLetterRoutingKey"] ?? "dlq";

            // DLX can be direct, topic, fanout—use whatever fits. Here we use direct.
            await _channel.ExchangeDeclareAsync(
                exchange: dlxName,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            // Declare the DLQ queue & bind it to the DLX
            await _channel.QueueDeclareAsync(
                queue: dlqName,
                durable: true,
                exclusive: false,
                autoDelete: false);
            await _channel.QueueBindAsync(
                queue: dlqName,
                exchange: dlxName,
                routingKey: dlqRouting);

            // 4) Declare your “requests” queue WITH dead-letter pointers
            var queueArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange",    dlxName    },
                { "x-dead-letter-routing-key", dlqRouting }
            };

            await _channel.QueueDeclareAsync(
                queue: _config["RabbitMQ:RequestsQueue"] ?? "cms.requests.processor",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs);

            await _channel.QueueBindAsync(
                queue: _config["RabbitMQ:RequestsQueue"],
                exchange: reqEx,
                routingKey: _config["RabbitMQ:RequestRoutingKey"] ?? "page.*.request");

            // 5) Start consuming
            _consumer = new AsyncEventingBasicConsumer(_channel);
            _consumer.ReceivedAsync += OnMessageReceivedAsync;
            await _channel.BasicConsumeAsync(
                queue: _config["RabbitMQ:RequestsQueue"],
                autoAck: false,
                consumer: _consumer);

            _listenerLogger.LogInformation("Listening on {Queue}", _config["RabbitMQ:RequestsQueue"]);
        }




        private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            _listenerLogger.LogInformation("Received message with routing key: {RoutingKey}", routingKey);

            SecureContentEvent secureEvent;
            try
            {
                secureEvent = JsonConvert.DeserializeObject<SecureContentEvent>(message);
            }
            catch (JsonException ex)
            {
                _listenerLogger.LogWarning(ex, "Invalid JSON message received: {RawMessage}", message);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false); // <-- Nack, not Ack
                return;
            }

            var signingKey = _config["Security:MessageSigningKey"];
            if (string.IsNullOrEmpty(signingKey))
            {
                _listenerLogger.LogWarning("Signing key missing in configuration. Cannot validate message signature.");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false); // <-- Nack, not Ack
                return;
            }

            try
            {
                bool isValid = MessageSecurityHelper.VerifyHmacSignature(secureEvent, secureEvent.Signature, signingKey);
                if (!isValid)
                {
                    _listenerLogger.LogWarning(
                        "Rejected message with invalid signature. EventId: {EventId}, HashedUserId: {HashedUserId}, RoutingKey: {RoutingKey}",
                        secureEvent.Id, secureEvent.HashedUserId, routingKey);

                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false); // <-- Nack, not Ack
                    return;
                }
            }
            catch (Exception ex)
            {
                _listenerLogger.LogError(ex, "Exception during signature verification");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false); // <-- Nack, not Ack
                return;
            }

            if (!MessageSecurityHelper.ValidateSecureContentEvent(secureEvent, out var validationError))
            {
                _listenerLogger.LogWarning(
                    "Rejected message due to schema validation error: {ValidationError}. EventId: {EventId}, RoutingKey: {RoutingKey}",
                    validationError, secureEvent?.Id, routingKey);

                await _channel.BasicNackAsync(ea.DeliveryTag, false, false); // <-- Nack, not Ack
                return;
            }

            _listenerLogger.LogInformation(
                "Accepted message. EventId: {EventId}, HashedUserId: {HashedUserId}, RoutingKey: {RoutingKey}",
                secureEvent.Id, secureEvent.HashedUserId, routingKey);

            // All checks passed, process the event
            await ProcessSecureContentEventAsync(secureEvent, routingKey);
            await _channel.BasicAckAsync(ea.DeliveryTag, false); // Only ack here!
        }



        private async Task ProcessSecureContentEventAsync(SecureContentEvent secureEvent, string routingKey)
        {
            _listenerLogger.LogInformation("Processing event {EventName} with routing key: {RoutingKey}", secureEvent?.Name, routingKey);

            try
            {
                switch (routingKey)
                {
                    case "page.create.request":
                        _listenerLogger.LogDebug("Invoking content creation handler for title: {Title}", secureEvent?.Content?.Title);
                        await CreateContentAsync(secureEvent.Content, secureEvent.Author);
                        break;

                    case "page.update.request":
                        _listenerLogger.LogDebug("Invoking content update handler for title: {Title}", secureEvent?.Content?.Title);
                        await UpdateContentAsync(secureEvent.Content, secureEvent.Author);
                        break;

                    case "page.delete.request":
                        if (secureEvent.Content != null && !string.IsNullOrWhiteSpace(secureEvent.Content.Title))
                        {
                            _listenerLogger.LogDebug("Invoking content delete handler for title: {Title}", secureEvent.Content.Title);
                            await DeleteContentByTitleAsync(secureEvent.Content.Title);
                        }
                        else
                        {
                            _listenerLogger.LogWarning("Delete event received without a valid title. EventId: {EventId}", secureEvent?.Id);
                        }
                        break;

                    default:
                        _listenerLogger.LogWarning("Received event with unknown routing key: {RoutingKey}. EventId: {EventId}", routingKey, secureEvent?.Id);
                        break;
                }
            }
            catch (Exception ex)
            {
                _listenerLogger.LogError(ex, "Error processing event {EventName} with routing key: {RoutingKey}", secureEvent?.Name, routingKey);
            }
        }


        private async Task CreateContentAsync(ContentData content, AuthorData author)
        {
            // 1. Ensure your page type exists in Piranha (e.g., "StandardPage")
            //    You can pass content.Type if it's set correctly in your message.
            if (string.IsNullOrWhiteSpace(content.Type))
            {
                _listenerLogger.LogInformation("Content type is missing. Cannot create page.");
                return;
            }
            Console.WriteLine($"[ExternalEventListenerService] Creating content of type: {content.Type}");

            using (var scope = _serviceProvider.CreateScope())
            {
                var api = scope.ServiceProvider.GetRequiredService<IApi>();

                var pageType = content.Type ?? "StandardPage";
                var page = await api.Pages.CreateAsync<Piranha.Models.PageBase>(pageType);

                page.Title = content.Title;
                page.Slug = content.Slug;

                // Set SiteId (required)
                var sites = await api.Sites.GetAllAsync();
                var defaultSite = sites.FirstOrDefault();
                if (defaultSite == null)
                {
                    _listenerLogger.LogError("No site found in the database.");
                    return;
                }
                page.SiteId = defaultSite.Id;

                // Optionally set ParentId if you want a child page
                // page.ParentId = ...;

                page.Published = DateTime.UtcNow;
                await api.Pages.SaveAsync(page);

            }

            _listenerLogger.LogInformation("Created new page: {Title} ({Slug}) of type {Type}", content.Title, content.Slug, content.Type);
        }


        private async Task UpdateContentAsync(ContentData content, AuthorData author)
        {
 
        }

        private async Task DeleteContentByTitleAsync(string title)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var api = scope.ServiceProvider.GetRequiredService<IApi>();
                // Get all pages (optionally, you can filter by site or parent if needed)
                var pages = await api.Pages.GetAllAsync();

                // Find the page with the exact matching title
                var page = pages.FirstOrDefault(p => string.Equals(p.Title, title, StringComparison.OrdinalIgnoreCase));

                if (page == null)
                {
                    _listenerLogger.LogWarning("No page found with title '{Title}'", title);
                    return;
                }

                await api.Pages.DeleteAsync(page.Id);
                _listenerLogger.LogInformation("Deleted page with title '{Title}' and ID {Id}", title, page.Id);

            }
        }



        // Implement other handlers similarly

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _listenerLogger.LogInformation("Stopping External Event Listener Service");

            try
            {
                // Remove the event handler
                if (_consumer != null)
                {
                    _consumer.ReceivedAsync -= OnMessageReceivedAsync;
                }

                // Close channel and connection gracefully
                await _channel?.CloseAsync();
                await _connection?.CloseAsync();
            }
            catch (Exception ex)
            {
                _listenerLogger.LogError(ex, "Error stopping External Event Listener Service");
            }
        }

        public void Dispose()
        {
            // Clean up resources
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                // Just log, don't throw from Dispose
                _listenerLogger?.LogError(ex, "Error disposing External Event Listener Service");
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}