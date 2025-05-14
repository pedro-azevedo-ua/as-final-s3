using System;
using System.Text;
using System.Threading.Channels;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace ContentsRUs.Eventing.Publisher
{
    public class PiranhaEventPublisher : IDisposable
    {
        private readonly string _connectionString;
        private readonly string _exchange = "piranha.events";

        public PiranhaEventPublisher(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task PublishAsync(object @event, string routingKey = "")
        {
            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
            };

            // Await the connection task to get the IConnection instance
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync(); // Fixed: Use CreateChannelAsync instead of CreateModel

            await channel.ExchangeDeclareAsync(exchange: _exchange, type: ExchangeType.Direct, durable: true);

            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);
            await channel.QueueDeclareAsync(queue: "hello", durable: false, exclusive: false, autoDelete: false,
    arguments: null);

            await channel.BasicPublishAsync(exchange: _exchange, routingKey: routingKey, body: body);
            Console.WriteLine($" [x] Sent {message}");

            Console.WriteLine($"[x] Published event to {_exchange} with body '{body}'");
        }

        public void Dispose()
        {
            // No resources to dispose because we're using short-lived connections
        }
    }
}
