using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ContentsRUs.Eventing.Listener
{
    public class ExternalEventListenerService : IHostedService, IDisposable
    {
        private readonly ILogger<ExternalEventListenerService> _logger;
        private readonly ConnectionFactory _factory;
        private readonly string _exchange = "piranha.external.events";
        private readonly string _queueName = "piranha.external.queue";
        private readonly string _routingKey;

        private IConnection _connection;
        private IChannel _channel;
        private AsyncEventingBasicConsumer _consumer;

        public ExternalEventListenerService(
            ILogger<ExternalEventListenerService> logger,
            string hostName = "localhost",
            int port = 5672,
            string user = "user",
            string pass = "password",
            string routingKey = "content.#")
        {
            _logger = logger;
            _routingKey = routingKey;

            _factory = new ConnectionFactory
            {
                UserName = user,
                Password = pass,
                VirtualHost = "/",
                HostName = hostName,
                Port = port,
            };
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
            try
            {
                // Extract message data
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var routingKey = ea.RoutingKey;

                _logger.LogInformation("Received message with routing key: {RoutingKey}", routingKey);
                Console.WriteLine($"[ExternalEventListenerService] Received message with routing key: {routingKey}");
                // Try to parse as JSON
                try
                {
                    // Parse and handle the message as JSON
                    var messageData = JsonConvert.DeserializeObject<dynamic>(message);

                    // Process based on routing key
                    await ProcessMessageAsync(messageData, routingKey);

                    // Acknowledge successful processing
                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

                    _logger.LogInformation("Successfully processed message with routing key: {RoutingKey}", routingKey);
                    Console.WriteLine($"[ExternalEventListenerService] Successfully processed message with routing key: {routingKey}");
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Received invalid JSON message: {Message}", message);
                    // Still acknowledge - we don't want to reprocess invalid messages
                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");

                try
                {
                    // Negative acknowledge with requeue to try again later
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                }
                catch (Exception nackEx)
                {
                    _logger.LogError(nackEx, "Error sending negative acknowledgment");
                }
            }
        }

        // In ExternalEventListenerService.cs
        private async Task ProcessMessageAsync(dynamic messageData, string routingKey)
        {
            switch (routingKey)
            {
                case "content.create":
                    await CreateContentAsync(messageData);
                    break;

                //case "content.update":
                //    await UpdateContentAsync(messageData);
                //    break;

                //case "content.delete":
                //    await DeleteContentAsync(messageData);
                //    break;

                default:
                    _logger.LogWarning("Unknown routing key: {RoutingKey}", routingKey);
                    break;
            }
        }

        private async Task CreateContentAsync(dynamic data)
        {
            // Example implementation:
            // 1. Access Piranha API (inject it in constructor)
            // 2. Create content from data
            string title = data.title;
            _logger.LogInformation("Creating content: {Title}", title);

            //For debuging console
            Console.WriteLine($"[ExternalEventListenerService] Creating content: {title}");

            // Example with Piranha API (you'd need to inject IApi)
            /*
            var page = await _api.Pages.CreateAsync<StandardPage>();
            page.Title = data.title;
            page.MetaDescription = data.description;
            page.Body = data.body;

            await _api.Pages.SaveAsync(page);
            */
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
    }
}