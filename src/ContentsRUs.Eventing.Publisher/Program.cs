using RabbitMQ.Client;
using System;
using System.Threading.Tasks;

namespace ContentsRUs.Eventing.Publisher
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Piranha Event Publisher Demo");
            Console.WriteLine("============================");

            try
            {
                // Connection parameters matching your Docker Compose configuration
                string hostName = "localhost";
                int port = 5672;
                string username = "user";           // From RABBITMQ_DEFAULT_USER
                string password = "password";       // From RABBITMQ_DEFAULT_PASS

                Console.WriteLine($"Connecting to RabbitMQ at {hostName}:{port}...");

                // Create publisher instance
                await using var publisher = new PiranhaEventPublisher(
                    hostName: hostName,
                    port: port,
                    user: username,
                    pass: password);

                // Create a test event object with more detailed content
                var testEvent = new
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Content Update",
                    CreatedAt = DateTime.UtcNow,
                    Content = new
                    {
                        Title = "Sample Article",
                        Slug = "sample-article",
                        Excerpt = "This is a test excerpt from Piranha CMS",
                        Body = "This is the full body of the test article from Piranha CMS",
                        LastModified = DateTime.UtcNow,
                        Categories = new[] { "News", "Technology" }
                    },
                    Author = new
                    {
                        Id = 1,
                        Name = "John Doe",
                        Email = "john@example.com"
                    }
                };

                Console.WriteLine("Connected! Publishing test event as JSON...");

                // Publish with a specific routing key
                string routingKey = "content.test";
                await publisher.PublishAsync(
                    @event: testEvent,
                    routingKey: routingKey);

                Console.WriteLine($"Event published successfully with routing key: {routingKey}");
                Console.WriteLine("Check your RabbitMQ management console to see the JSON message in the queue!");
                Console.WriteLine($"Management console: http://localhost:15672/ (user: {username}, password: {password})");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}