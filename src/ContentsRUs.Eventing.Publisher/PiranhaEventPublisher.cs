using RabbitMQ.Client;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prometheus;
using System;
using System.Threading.Tasks;

namespace ContentsRUs.Eventing.Publisher
{
    public class PiranhaEventPublisher : IPiranhaEventPublisher, IAsyncDisposable
    {
        private readonly ConnectionFactory _factory;
        private IConnection _connection;
        private IChannel _channel;
        private readonly string _exchange;
        private readonly string _exchangeType;
        private readonly ILogger _publisherLogger;

        private static readonly Counter MessagesPublished = Metrics.CreateCounter(
            "publisher_messages_published_total",
            "Total number of messages successfully published.");

        private static readonly Counter PublishFailures = Metrics.CreateCounter(
            "publisher_publish_failures_total",
            "Total number of failed message publications.");

        public PiranhaEventPublisher(IConfiguration config, ILoggerFactory loggerFactory)
        {
            _publisherLogger = loggerFactory.CreateLogger("Eventing.Publisher");
            _factory = new ConnectionFactory
            {
                HostName = config["RabbitMQ:HostName"],
                Port = config.GetValue<int>("RabbitMQ:Port", 5672),
                UserName = config["RabbitMQ:UserName"],
                Password = config["RabbitMQ:Password"],
                VirtualHost = config["RabbitMQ:VirtualHost"] ?? "/",
                AutomaticRecoveryEnabled = config.GetValue<bool>("RabbitMQ:AutomaticRecoveryEnabled", true),
                TopologyRecoveryEnabled = config.GetValue<bool>("RabbitMQ:TopologyRecoveryEnabled", true)
            };

            _exchange = config["RabbitMQ:EventsExchange"];
            _exchangeType = config["RabbitMQ:ExchangeType"] ?? ExchangeType.Topic;
        }

        public async Task InitializeAsync()
        {
            _connection = await _factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            await _channel.ExchangeDeclareAsync(_exchange, _exchangeType, durable: true, autoDelete: false);
        }

        public async Task PublishAsync<T>(T @event, string routingKey)
        {
            if (_channel == null)
                throw new InvalidOperationException("Publisher not initialized. Call InitializeAsync() first.");

            string jsonData = JsonConvert.SerializeObject(@event, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            byte[] body = Encoding.UTF8.GetBytes(jsonData);

            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            _publisherLogger.LogInformation("Publishing event to exchange '{Exchange}' with routing key '{RoutingKey}'", _exchange, routingKey);
            try
            {
                await _channel.BasicPublishAsync(_exchange, routingKey, true, props, body);
                 MessagesPublished.Inc();
                _publisherLogger.LogInformation("Message published and confirmed by broker.");
            }
            catch (Exception ex)
            {
                 PublishFailures.Inc();
                _publisherLogger.LogError(ex, "Failed to publish or confirm message");
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel != null)
            {
                await _channel.CloseAsync();
                await _channel.DisposeAsync();
            }
            if (_connection != null)
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }
        }
    }
}
