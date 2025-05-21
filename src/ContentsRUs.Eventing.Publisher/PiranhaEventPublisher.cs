using RabbitMQ.Client;
using System.Text;
using Newtonsoft.Json;
using Serilog;
using Serilog.Context;

namespace ContentsRUs.Eventing.Publisher
{
    public class PiranhaEventPublisher : IAsyncDisposable
    {
        private readonly ConnectionFactory _factory;
        private readonly string _exchange = "piranha.events";
        private readonly string _queueName = "piranha.events.queue";

        public PiranhaEventPublisher(string hostName, int port, string user, string pass)
        {
            _factory = new ConnectionFactory
            {
                UserName = user,
                Password = pass,
                VirtualHost = "/",
                HostName = hostName
            };
        }

        public async Task PublishAsync<T>(T @event, string routingKey = "content.published")
        {
            var traceId = Guid.NewGuid().ToString();

            using (LogContext.PushProperty("TraceId", traceId))
            {
                try
                {
                    Log.Information("Establishing RabbitMQ connection");

                    IConnection conn = await _factory.CreateConnectionAsync();
                    IChannel channel = await conn.CreateChannelAsync();

                    await channel.ExchangeDeclareAsync(_exchange, ExchangeType.Direct);
                    await channel.QueueDeclareAsync(_queueName, false, false, false, null);
                    await channel.QueueBindAsync(_queueName, _exchange, routingKey, null);

                    string jsonData = JsonConvert.SerializeObject(@event, Formatting.Indented,
                        new JsonSerializerSettings
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });

                    byte[] messageBodyBytes = Encoding.UTF8.GetBytes(jsonData);
                    int payloadSize = messageBodyBytes.Length;
                    Log.Information(
                        "Publishing event | Exchange: {Exchange} | RoutingKey: {RoutingKey} | PayloadSize: {PayloadSize} bytes | TraceId: {TraceId}",
                        _exchange,
                        routingKey,
                        payloadSize,
                        traceId
                    );


                    var props = new BasicProperties
                    {
                        ContentType = "application/json",
                        DeliveryMode = DeliveryModes.Persistent,
                        MessageId = Guid.NewGuid().ToString(),
                        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    };

                    await channel.BasicPublishAsync(_exchange, routingKey, mandatory: true,
                        basicProperties: props, body: messageBodyBytes);

                    Log.Information("Event published | RoutingKey: {RoutingKey} | PayloadPreview: {Preview}",
                        routingKey,
                        jsonData.Substring(0, Math.Min(100, jsonData.Length)));

                    await channel.CloseAsync();
                    await conn.CloseAsync();
                    await channel.DisposeAsync();
                    await conn.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to publish event | RoutingKey: {RoutingKey}", routingKey);
                    throw;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Task.CompletedTask;
        }
    }
}
