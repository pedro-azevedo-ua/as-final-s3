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
using Prometheus;


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

        private static readonly Counter MessagesReceived = Metrics.CreateCounter(
            "listener_messages_received_total",
            "Total number of messages received from RabbitMQ.");

        private static readonly Counter MessagesInvalidJson = Metrics.CreateCounter(
            "listener_messages_invalid_json_total",
            "Total number of messages rejected due to invalid JSON.");

        private static readonly Counter MessagesInvalidSignature = Metrics.CreateCounter(
            "listener_messages_invalid_signature_total",
            "Total number of messages rejected due to invalid signature.");

        private static readonly Counter MessagesSchemaInvalid = Metrics.CreateCounter(
            "listener_messages_schema_invalid_total",
            "Total number of messages rejected due to schema validation failure.");

        private static readonly Counter MessagesAcked = Metrics.CreateCounter(
            "listener_messages_acked_total",
            "Total number of messages successfully processed and acknowledged.");

        private static readonly Counter MessagesNacked = Metrics.CreateCounter(
            "listener_messages_nacked_total",
            "Total number of messages negatively acknowledged (nacked).");

        private static readonly Summary MessageProcessingDuration = Metrics.CreateSummary(
            "listener_message_processing_duration_seconds",
            "Summary of time taken to process messages.");

        private static readonly Counter RabbitMqConnections = Metrics.CreateCounter(
            "listener_rabbitmq_connections_total",
            "Total number of successful RabbitMQ connections.");

        private static readonly Counter RabbitMqSetupFailures = Metrics.CreateCounter(
            "listener_rabbitmq_setup_failures_total",
            "Total number of failures during RabbitMQ exchange/queue setup.",
            new CounterConfiguration { LabelNames = new[] { "step" } });

        private static readonly Gauge ConsumerActive = Metrics.CreateGauge(
            "listener_consumer_active",
            "Indicates if the listener's consumer is actively listening (1 = active, 0 = inactive).");

        private static readonly Counter EventsProcessed = Metrics.CreateCounter(
            "listener_events_processed_total",
            "Total number of events processed, labeled by event type.",
            new CounterConfiguration
            {
                LabelNames = new[] { "event_type" }
            });

        private static readonly Counter EventsFailed = Metrics.CreateCounter(
            "listener_events_failed_total",
            "Total number of events that failed to process, labeled by event type.",
            new CounterConfiguration
            {
                LabelNames = new[] { "event_type" }
            });

        private static readonly Summary EventProcessingDuration = Metrics.CreateSummary(
            "listener_event_processing_duration_seconds",
            "Duration of event processing, labeled by event type.",
            new SummaryConfiguration
            {
                LabelNames = new[] { "event_type" }
            });

        private static readonly Counter ContentCreateAttempts = Metrics.CreateCounter(
            "listener_content_create_attempts_total",
            "Total number of attempts to create content.");

        private static readonly Counter ContentCreateFailures = Metrics.CreateCounter(
            "listener_content_create_failures_total",
            "Total number of content creation failures.");

        private static readonly Summary ContentCreateDuration = Metrics.CreateSummary(
            "listener_content_create_duration_seconds",
            "Time taken to create content.");

        private static readonly Counter ContentDeleteAttempts = Metrics.CreateCounter(
            "listener_content_delete_attempts_total",
            "Total number of attempts to delete content by title.");

        private static readonly Counter ContentDeleteFailures = Metrics.CreateCounter(
            "listener_content_delete_failures_total",
            "Total number of failed content deletions.");

        private static readonly Summary ContentDeleteDuration = Metrics.CreateSummary(
            "listener_content_delete_duration_seconds",
            "Time taken to delete content by title.");

        private static readonly Counter MessagesDeadLettered = Metrics.CreateCounter(
            "listener_messages_deadlettered_total",
            "Total number of messages sent to the Dead Letter Queue (DLQ).");

        public ExternalEventListenerService(
        IServiceProvider serviceProvider,
            IConfiguration config,
            ILoggerFactory loggerFactory 
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
            try
            {

                // connect
                _connection = await _factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync();
                RabbitMqConnections.Inc();

                // declare the normal exchange
                var reqEx = _config["RabbitMQ:RequestsExchange"] ?? "cms.requests";
                await _channel.ExchangeDeclareAsync(
                    exchange: reqEx,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                // declare the  dead-letter exchange and DLQ
                var dlxName = _config["RabbitMQ:DeadLetterExchange"] ?? "cms.dlx";
                var dlqName = _config["RabbitMQ:DeadLetterQueue"] ?? "cms.dlq";
                var dlqRouting = _config["RabbitMQ:DeadLetterRoutingKey"] ?? "dlq";

                await _channel.ExchangeDeclareAsync(
                    exchange: dlxName,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                // declare the DLQ queue & bind it to the DLX
                await _channel.QueueDeclareAsync(
                    queue: dlqName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);
                await _channel.QueueBindAsync(
                    queue: dlqName,
                    exchange: dlxName,
                    routingKey: dlqRouting);

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

                _consumer = new AsyncEventingBasicConsumer(_channel);
                _consumer.ReceivedAsync += OnMessageReceivedAsync;
                await _channel.BasicConsumeAsync(
                    queue: _config["RabbitMQ:RequestsQueue"],
                    autoAck: false,
                    consumer: _consumer);

                ConsumerActive.Set(1);
                _listenerLogger.LogInformation("Listening on {Queue}", _config["RabbitMQ:RequestsQueue"]);
            }
            catch (Exception e)
            {
                _listenerLogger.LogError(e, "Failed to start listener and connect to RabbitMQ");
                RabbitMqSetupFailures.WithLabels("start_async").Inc();
                ConsumerActive.Set(0);
            }
        }

        private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            var timer = MessageProcessingDuration.NewTimer();
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            MessagesReceived.Inc();
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
                MessagesDeadLettered.Inc();
                return;
            }

            var signingKey = _config["Security:MessageSigningKey"];
            if (string.IsNullOrEmpty(signingKey))
            {
                _listenerLogger.LogWarning("Signing key missing in configuration. Cannot validate message signature.");
                MessagesInvalidJson.Inc();
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false); // <-- Nack, not Ack
                MessagesNacked.Inc();
                MessagesDeadLettered.Inc();
                timer.ObserveDuration();
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

                    MessagesInvalidSignature.Inc();
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false); 
                    MessagesDeadLettered.Inc();
                    MessagesNacked.Inc();
                    timer.ObserveDuration();
                    return;
                }
            }
            catch (Exception ex)
            {
                _listenerLogger.LogError(ex, "Exception during signature verification");
                MessagesInvalidSignature.Inc();
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false); 
                MessagesDeadLettered.Inc();
                MessagesNacked.Inc();
                timer.ObserveDuration();
                return;
            }

            if (!MessageSecurityHelper.ValidateSecureContentEvent(secureEvent, out var validationError))
            {
                _listenerLogger.LogWarning(
                    "Rejected message due to schema validation error: {ValidationError}. EventId: {EventId}, RoutingKey: {RoutingKey}",
                    validationError, secureEvent?.Id, routingKey);

                MessagesSchemaInvalid.Inc();
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false); 
                MessagesDeadLettered.Inc();
                MessagesNacked.Inc();
                timer.ObserveDuration();
                return;
            }

            _listenerLogger.LogInformation(
                "Accepted message. EventId: {EventId}, HashedUserId: {HashedUserId}, RoutingKey: {RoutingKey}",
                secureEvent.Id, secureEvent.HashedUserId, routingKey);

            // All checks passed, process the event
            await ProcessSecureContentEventAsync(secureEvent, routingKey);
            await _channel.BasicAckAsync(ea.DeliveryTag, false); 
            MessagesAcked.Inc();

            timer.ObserveDuration();
        }

        private async Task ProcessSecureContentEventAsync(SecureContentEvent secureEvent, string routingKey)
        {
            var eventType = routingKey ?? "unknown";
            var timer = EventProcessingDuration.WithLabels(eventType).NewTimer();

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

                EventsProcessed.WithLabels(eventType).Inc();
            }
            catch (Exception ex)
            {
                EventsFailed.WithLabels(eventType).Inc();
                _listenerLogger.LogError(ex, "Error processing event {EventName} with routing key: {RoutingKey}", secureEvent?.Name, routingKey);
            }
            finally
            {
                timer.ObserveDuration();
            }
        }


        private async Task CreateContentAsync(ContentData content, AuthorData author)
        {

            ContentCreateAttempts.Inc();
            var timer = ContentCreateDuration.NewTimer();


            if (string.IsNullOrWhiteSpace(content.Type))
            {
                _listenerLogger.LogInformation("Content type is missing. Cannot create page.");
                timer.ObserveDuration();
                return;
            }
            Console.WriteLine($"[ExternalEventListenerService] Creating content of type: {content.Type}");

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var api = scope.ServiceProvider.GetRequiredService<IApi>();

                    var pageType = content.Type ?? "StandardPage";
                    var page = await api.Pages.CreateAsync<Piranha.Models.PageBase>(pageType);

                    page.Title = content.Title;
                    page.Slug = content.Slug;

            
                    var sites = await api.Sites.GetAllAsync();
                    var defaultSite = sites.FirstOrDefault();
                    if (defaultSite == null)
                    {
                        _listenerLogger.LogError("No site found in the database.");
                        ContentCreateFailures.Inc();
                        return;
                    }

                    page.SiteId = defaultSite.Id;



                    page.Published = DateTime.UtcNow;
                    await api.Pages.SaveAsync(page);

                }

                _listenerLogger.LogInformation("Created new page: {Title} ({Slug}) of type {Type}", content.Title, content.Slug, content.Type);
            }
            catch (Exception e)
            {
                ContentCreateFailures.Inc();
                _listenerLogger.LogError(e, "Failed to create content for title: {Title}", content?.Title);
            }
            finally
            {
                timer.ObserveDuration();
            }
        }


        private async Task UpdateContentAsync(ContentData content, AuthorData author)
        {

        }

        private async Task DeleteContentByTitleAsync(string title)
        {
            ContentDeleteAttempts.Inc();
            var timer = ContentDeleteDuration.NewTimer();

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var api = scope.ServiceProvider.GetRequiredService<IApi>();
                    var pages = await api.Pages.GetAllAsync();

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
            catch (Exception e)
            {
                ContentDeleteFailures.Inc();
                _listenerLogger.LogError(e, "Failed to delete content with title: {Title}", title);
            }
            finally
            {
                timer.ObserveDuration();
            }
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _listenerLogger.LogInformation("Stopping External Event Listener Service");

            try
            {
                if (_consumer != null)
                {
                    _consumer.ReceivedAsync -= OnMessageReceivedAsync;
                }

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
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _listenerLogger?.LogError(ex, "Error disposing External Event Listener Service");
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}