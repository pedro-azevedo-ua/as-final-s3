using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using Swashbuckle.AspNetCore.SwaggerGen;

public class MetricsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Paths.Add("/metrics", new OpenApiPathItem
        {
            Description = "Prometheus metrics endpoint (text/plain).",
            Operations = new Dictionary<OperationType, OpenApiOperation>
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Metrics" } },
                    Summary = "Exposes Prometheus metrics in plain text format.",
                    Description = "Provides various application metrics related to message processing, event handling, and RabbitMQ connections. Metrics are formatted for Prometheus scraping.",
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse
                        {
                            Description = "Plain text Prometheus metrics.",
                            Content = new Dictionary<string, OpenApiMediaType>
                            {
                                ["text/plain"] = new OpenApiMediaType
                                {
                                    Example = new OpenApiString(@"
# Metrics exposed by the ExternalEventListener

# Total number of messages received from RabbitMQ
listener_messages_received_total

# Total number of messages rejected due to invalid JSON
listener_messages_invalid_json_total

# Total number of messages rejected due to invalid signature
listener_messages_invalid_signature_total

# Total number of messages rejected due to schema validation failure
listener_messages_schema_invalid_total

# Total number of messages successfully processed and acknowledged
listener_messages_acked_total

# Total number of messages negatively acknowledged (nacked)
listener_messages_nacked_total

# Summary of time taken to process messages
listener_message_processing_duration_seconds

# Total number of successful RabbitMQ connections
listener_rabbitmq_connections_total

# Total number of failures during RabbitMQ setup, labeled by 'step'
listener_rabbitmq_setup_failures_total{step=""setup""}

# Indicates if the listener's consumer is active (1) or not (0)
listener_consumer_active

# Total number of events processed, labeled by 'event_type'
listener_events_processed_total{event_type=""example_event""}

# Total number of events failed to process, labeled by 'event_type'
listener_events_failed_total{event_type=""example_event""}

# Duration of event processing, labeled by 'event_type'
listener_event_processing_duration_seconds{event_type=""example_event""}

# Total number of attempts to create content
listener_content_create_attempts_total

# Total number of content creation failures
listener_content_create_failures_total

# Time taken to create content
listener_content_create_duration_seconds

# Total number of attempts to delete content
listener_content_delete_attempts_total

# Total number of failed content deletions
listener_content_delete_failures_total

# Time taken to delete content
listener_content_delete_duration_seconds

# Total number of messages sent to Dead Letter Queue
listener_messages_deadlettered_total

#Total number of messages successfully published.
publisher_messages_published_total

#Total number of failed message publications
publisher_publish_failures_total
")
                                }
                            }
                        }
                    }
                }
            }
        });
    }
}

