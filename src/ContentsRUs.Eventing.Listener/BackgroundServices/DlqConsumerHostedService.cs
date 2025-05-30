using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContentRUs.Eventing.Listener.BackgroundServices
{
    public class DlqConsumerHostedService : BackgroundService
    {
        private readonly ILogger<DlqConsumerHostedService> _logger;
        private readonly ILogger _dlqLogger;

        private readonly IConfiguration _config;
        private readonly ConnectionFactory _factory;
        private IConnection _connection;
        private IChannel _channel;
        private AsyncEventingBasicConsumer _consumer;
        private readonly string _outputDir;


        public DlqConsumerHostedService(ILogger<DlqConsumerHostedService> logger, ILoggerFactory loggerFactory, IConfiguration config)
        {

            _config = config;
            _outputDir = _config["DlqOutputDirectory"] ?? "dlq-messages";
            Directory.CreateDirectory(_outputDir);
            _dlqLogger = loggerFactory.CreateLogger("Eventing.DLQ");
            _logger = logger;
            _factory = new ConnectionFactory
            {
                UserName = _config["RabbitMQ:UserName"] ?? "guest",
                Password = _config["RabbitMQ:Password"] ?? "guest",
                VirtualHost = "/",
                HostName = _config["RabbitMQ:HostName"] ?? "localhost",
                Port = _config.GetValue<int>("RabbitMQ:Port", 5672),
                AutomaticRecoveryEnabled = bool.TryParse(_config["RabbitMQ:AutomaticRecoveryEnabled"], out var are) && are,
                TopologyRecoveryEnabled = bool.TryParse(_config["RabbitMQ:TopologyRecoveryEnabled"], out var tre) && tre,
                RequestedHeartbeat = TimeSpan.FromSeconds(double.TryParse(_config["RabbitMQ:RequestedHeartbeat"], out var hb) ? hb : 30),
                RequestedConnectionTimeout = TimeSpan.FromMilliseconds(double.TryParse(_config["RabbitMQ:RequestedConnectionTimeout"], out var ct) ? ct : 60000),
                ClientProvidedName = _config["RabbitMQ:ClientProvidedName"]
            };
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _connection = await _factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync();

            var rabbitConfig = _config.GetSection("RabbitMQ");
            var dlqName = _config["RabbitMQ:DeadLetterQueue"] ?? "cms.dlq";
            var prefetch = ushort.TryParse(_config["RabbitMQ:PrefetchCount"], out var pf) ? pf : (ushort)10;

            await _channel.QueueDeclareAsync(queue: dlqName, durable: true, exclusive: false, autoDelete: false);
            await _channel.BasicQosAsync(0, prefetch, false);

            _consumer = new AsyncEventingBasicConsumer(_channel);
            _consumer.ReceivedAsync += OnDlqMessageReceivedAsync;

            await _channel.BasicConsumeAsync(queue: dlqName, autoAck: false, consumer: _consumer);

            _logger.LogInformation("DLQ consumer started and listening on queue '{Queue}'", dlqName);
            await base.StartAsync(cancellationToken);
        }

        private async Task OnDlqMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            string content = Encoding.UTF8.GetString(body);

            object payload;
            try
            {
                payload = JsonSerializer.Deserialize<object>(content);
            }
            catch
            {
                payload = content;
            }

            var logEntry = new
            {
                Metadata = new
                {
                    RoutingKey = ea.RoutingKey,
                    Exchange = ea.Exchange,
                    ContentType = ea.BasicProperties.ContentType,
                    MessageId = ea.BasicProperties.MessageId,
                    Timestamp = ea.BasicProperties.Timestamp.UnixTime,
                    Headers = ea.BasicProperties.Headers
                },
                Payload = payload
            };

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string filename = Path.Combine(_outputDir, $"dlq-msg-{timestamp}-{ea.DeliveryTag}.json");

            // Log to dedicated DLQ log file via Serilog
            _dlqLogger.LogWarning("DLQ Message: {Info}", JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = true }));

            // (Optional) Still save per-message file if you want
            await File.WriteAllTextAsync(filename, JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = true }));

            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        }


        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Not needed since all work is event-driven in StartAsync
            return Task.CompletedTask;
        }
    }
}
