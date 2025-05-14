using ContentsRUs.Eventing.Publisher;
using System;
using System.Threading.Tasks;

namespace ContentsRUs.Eventing.Publisher
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Piranha Event Publisher Test Starting...");

            // Ensure RabbitMQ is running (from your previous task)
            var publisher = new PiranhaEventPublisher("localhost");

            var testEvent = new
            {
                TestProperty = "Hello from Piranha Event Publisher!",
                Timestamp = DateTime.UtcNow,
                RandomValue = new Random().Next(1, 100)
            };

            try
            {
                Console.WriteLine("Publishing test event...");
                await publisher.PublishAsync(testEvent, "test.route");
                Console.WriteLine("Test event published successfully.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error publishing event: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}