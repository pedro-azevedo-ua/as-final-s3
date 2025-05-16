
using RabbitMQ.Client;
using System.Text;
using System.Threading.Channels;
using Newtonsoft.Json;

namespace ContentsRUs.Eventing.Publisher
{
    public class PiranhaEventPublisher : IAsyncDisposable
    {
        private readonly ConnectionFactory _factory;
        private readonly string _exchange = "piranha.events";
        private readonly string _queueName = "piranha.events.queue";

        public PiranhaEventPublisher(string hostName, int port, string user, string pass)
        {
            _factory = new ConnectionFactory();
            // "guest"/"guest" by default, limited to localhost connections
            _factory.UserName = user;
            _factory.Password = pass;
            _factory.VirtualHost = "/";
            _factory.HostName = hostName;
        }

        public async Task PublishAsync<T>(T @event, string routingKey = "content.published")
        {
            // 1) Acquire connection & channel asynchronously
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
            var props = new BasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = DeliveryModes.Persistent;
            props.MessageId = Guid.NewGuid().ToString();
            props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            await channel.BasicPublishAsync(_exchange, routingKey,
                mandatory: true, basicProperties: props, body: messageBodyBytes);

            Console.WriteLine($"Published JSON message: {jsonData.Substring(0, Math.Min(100, jsonData.Length))}...");


            await channel.CloseAsync();
            await conn.CloseAsync();
            await channel.DisposeAsync();
            await conn.DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Task.CompletedTask;
        }
    }
}
