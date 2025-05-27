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
        private readonly ILogger<ExternalEventListenerService> _logger;
        private readonly ConnectionFactory _factory;
        private readonly string _exchange = "piranha.external.events";
        private readonly string _queueName = "piranha.external.queue";
        private readonly string _routingKey;

        private IConnection _connection;
        private IChannel _channel;
        private AsyncEventingBasicConsumer _consumer;
        private readonly IConfiguration _config;
        //private readonly IApi _api;

        public ExternalEventListenerService(
         IServiceProvider serviceProvider,
         IConfiguration config,
         ILogger<ExternalEventListenerService> logger
        )
            {
            _serviceProvider = serviceProvider;
            _logger = logger;

            _config = config;
            _routingKey = config["RabbitMQ:RoutingKey"] ?? "content.#"; // Default routing key if not configured
            //_api = api;

            _factory = new ConnectionFactory
            {
                UserName = config["RabbitMQ:UserName"] ?? "guest",
                Password = config["RabbitMQ:Password"] ?? "guest",
                VirtualHost = "/",
                HostName = config["RabbitMQ:HostName"] ?? "localhost",
                Port = config.GetValue<int>("RabbitMQ:Port", 5672),
            };

            Console.WriteLine($"[ExternalEventListenerService] Using RabbitMQ host: {_factory.HostName}, port: {_factory.Port}, user: {_factory.UserName}, routing key: {config["RabbitMQ:RoutingKey"]}");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting External Event Listener Service");

            try
            {
                // Create connection and channel
                _connection = await _factory.CreateConnectionAsync(cancellationToken);
                _channel = await _connection.CreateChannelAsync();

                // Setup exchange, queue and binding
                await _channel.ExchangeDeclareAsync(
                    exchange: _exchange,
                    type: ExchangeType.Direct,
                    durable: false,
                    autoDelete: false);

                await _channel.QueueDeclareAsync(
                    queue: _queueName,
                    durable: false,
                    exclusive: false,
                    autoDelete: false);

                await _channel.QueueBindAsync(
                    queue: _queueName,
                    exchange: _exchange,
                    routingKey: _routingKey);

                // Create and configure consumer
                _consumer = new AsyncEventingBasicConsumer(_channel);
                _consumer.ReceivedAsync += OnMessageReceivedAsync;

                // Start consuming
                await _channel.BasicConsumeAsync(
                    queue: _queueName,
                    autoAck: false, // Use manual acknowledgment for reliability
                    consumer: _consumer);

                _logger.LogInformation("External Event Listener started. Listening for messages with routing key: {RoutingKey}", _routingKey);
                Console.WriteLine($"[ExternalEventListenerService] RExternal Event Listener started. Listening for messages with routing key {_routingKey}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting External Event Listener Service");
                // Clean up on error
                _channel?.Dispose();
                _connection?.Dispose();
                throw;
            }
        }

        private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;

            _logger.LogInformation("Received message with routing key: {RoutingKey}", routingKey);
            Console.WriteLine($"[ExternalEventListenerService] Received message with routing key: {routingKey}");

            SecureContentEvent secureEvent;
            try
            {
                secureEvent = JsonConvert.DeserializeObject<SecureContentEvent>(message);
               
            }
            catch (JsonException ex)
            {
                _logger.LogInformation(ex, "Received invalid JSON message: {Message}", message);
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                return;
            }
            Console.WriteLine($"[ExternalEventListenerService] secureEvent.Signature: {secureEvent?.Signature}");
            // Get signing key from config
            var signingKey = _config["Security:MessageSigningKey"];
            Console.WriteLine($"[ExternalEventListenerService] signingKey: {signingKey}");
            if (string.IsNullOrEmpty(signingKey))
            {
                Console.WriteLine($"[ExternalEventListenerService] signing key missing in configuration. Cannot validate message signature.");
                _logger.LogInformation("Signing key missing in configuration. Cannot validate message signature.");
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                return;
            }

            try
            {
                bool isValid = MessageSecurityHelper.VerifyHmacSignature(secureEvent, secureEvent.Signature, signingKey);
                Console.WriteLine($"[ExternalEventListenerService] signature valid? {isValid}");
                if (!isValid)
                {
                    Console.WriteLine($"[ExternalEventListenerService] signature invalid");
                    _logger.LogInformation(
                  "Rejected message with invalid signature. EventId: {EventId}, HashedUserId: {HashedUserId}, RoutingKey: {RoutingKey}",
                  secureEvent.Id, secureEvent.HashedUserId, routingKey);
                    Console.WriteLine($"[ExternalEventListenerService] signature invalid");

                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExternalEventListenerService] Exception during signature verification: {ex}");
                _logger.LogError(ex, "Exception during signature verification");
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                return;
            }



            Console.WriteLine($"[ExternalEventListenerService] signature valid");


            if (!MessageSecurityHelper.ValidateSecureContentEvent(secureEvent, out var validationError))
            {
                _logger.LogInformation(
                    "Rejected message due to schema validation error: {ValidationError}. EventId: {EventId}, RoutingKey: {RoutingKey}",
                    validationError, secureEvent?.Id, routingKey);
                Console.WriteLine($"[ExternalEventListenerService] validation error: {validationError}");

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                return;
            }

            Console.WriteLine($"[ExternalEventListenerService] validation ok: {secureEvent?.Id} - {secureEvent?.Name}");
            // Log accepted and process
            _logger.LogInformation("Accepted message. EventId: {EventId}, HashedUserId: {HashedUserId}, RoutingKey: {RoutingKey}",
                secureEvent.Id, secureEvent.HashedUserId, routingKey);
            // All checks passed, process the event
            await ProcessSecureContentEventAsync(secureEvent, routingKey);
            await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        }

        private async Task ProcessSecureContentEventAsync(SecureContentEvent secureEvent, string routingKey)
        {
            // Use secureEvent.Content, secureEvent.Author, etc.
            // Route based on secureEvent.Name or routingKey
            switch (routingKey)
            {
                case "content.create":
                    await CreateContentAsync(secureEvent.Content, secureEvent.Author);
                    break;
                case "content.update":
                    await UpdateContentAsync(secureEvent.Content, secureEvent.Author);
                    break;
                case "content.delete":
                    if (secureEvent.Content != null && !string.IsNullOrWhiteSpace(secureEvent.Content.Title))
                        await DeleteContentByTitleAsync(secureEvent.Content.Title);
                    else
                        _logger.LogWarning("Delete event received without a title.");
                    break;
                default:
                    _logger.LogWarning("Unknown routing key: {RoutingKey}", routingKey);
                    break;
            }
        }

        private async Task CreateContentAsync(ContentData content, AuthorData author)
        {
            // 1. Ensure your page type exists in Piranha (e.g., "StandardPage")
            //    You can pass content.Type if it's set correctly in your message.
            if (string.IsNullOrWhiteSpace(content.Type))
            {
                _logger.LogInformation("Content type is missing. Cannot create page.");
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
                    _logger.LogError("No site found in the database.");
                    return;
                }
                page.SiteId = defaultSite.Id;

                // Optionally set ParentId if you want a child page
                // page.ParentId = ...;

                page.Published = DateTime.UtcNow;
                await api.Pages.SaveAsync(page);

            }

            _logger.LogInformation("Created new page: {Title} ({Slug}) of type {Type}", content.Title, content.Slug, content.Type);
        }


        private async Task UpdateContentAsync(ContentData content, AuthorData author)
        {
            //string title = data.Payload.Title;
            //_logger.LogInformation("Updating content: {Title}", title);
            //Console.WriteLine($"[ExternalEventListenerService] Updating content: {title}");

            // Exemplo de código com Piranha API (se for injetada no construtor)
            
            //var page = await _api.Pages.GetByIdAsync(data.id);
            //if (page != null)
            //{
            //    page.Title = data.title;
            //    page.Body = data.body;
            //    await _api.Pages.SaveAsync(page);
            //}
            //*/
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
                    _logger.LogWarning("No page found with title '{Title}'", title);
                    return;
                }

                await api.Pages.DeleteAsync(page.Id);
                _logger.LogInformation("Deleted page with title '{Title}' and ID {Id}", title, page.Id);

            }
        }



        // Implement other handlers similarly

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping External Event Listener Service");

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
                _logger.LogError(ex, "Error stopping External Event Listener Service");
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
                _logger?.LogError(ex, "Error disposing External Event Listener Service");
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}