using RabbitMQ.Client;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using System;
using System.Threading.Tasks;

namespace ContentsRUs.Eventing.Publisher
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Setup Serilog
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithThreadId()
                .Enrich.WithProcessId()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Information()
                .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
                .CreateLogger();

            Log.Information("Piranha Event Publisher Starting");

            try
            {
                string hostName = "localhost";
                int port = 5672;
                string username = "user";
                string password = "password";

                Log.Information("Connecting to RabbitMQ at {Host}:{Port}", hostName, port);

                await using var publisher = new PiranhaEventPublisher(
                    hostName: hostName,
                    port: port,
                    user: username,
                    pass: password);

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

                var traceId = Guid.NewGuid().ToString();
                using (LogContext.PushProperty("TraceId", traceId))
                {
                    Log.Information("Connected to RabbitMQ. Publishing event with TraceId {TraceId}", traceId);

                    string routingKey = "content.test";
                    await publisher.PublishAsync(
                        @event: testEvent,
                        routingKey: routingKey);

                    Log.Information("Event published with routing key {RoutingKey}", routingKey);
                }

                Log.Information("Publisher finished. Check RabbitMQ for the event.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "An error occurred during publishing");
            }
            finally
            {
                Log.CloseAndFlush();
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
