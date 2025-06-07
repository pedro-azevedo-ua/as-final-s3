# Phase 1

### 1.1 | **Get Piranha CMS running locally**

- Started by forking the Piranha CMS repository.
- Followed the [Piranha CMS documentation](https://piranhacms.org/docs/) to set up the CMS locally.

```
> dotnet restore
> dotnet build
> cd examples/MvcWeb
> dotnet tool install --global dotnet-ef
> dotnet ef database update
> dotnet run --framework net8.0
```

- Accessed the CMS at `http://localhost:5000/manager` with default credentials: `admin / password`.

### 1.2 | **Spin up RabbitMQ via Docker & confirm UI**

- Created a `rabbitmq/docker-compose.yml` file for RabbitMQ with the Management Plugin enabled.

```yaml
version: '3.8'

services:
	rabbitmq:
	image: rabbitmq:3-management
	container_name: rabbitmq_broker
	ports:
		- "5672:5672"  # AMQP port
		- "15672:15672" # Management UI port
	volumes:
		- rabbitmq_data:/var/lib/rabbitmq
	environment:
		RABBITMQ_DEFAULT_USER: user
		RABBITMQ_DEFAULT_PASS: password
		RABBITMQ_NODENAME: rabbit@localhost # Recommended for stability

volumes:
	rabbitmq_data:
```

- Run the Docker container:

```
> docker-compose up -d
```

- Accessed RabbitMQ UI at `http://localhost:15672` with credentials: `user / password`.

### 1.3 | *Scaffold* `PiranhaEventPublisher` **project**

- Create new project to the solution:

  - Right-click on the Solution in Solution Explorer.
  - Select "Add" -> "New Project...".
  - Search for "Class Library".
  - Click "Next".
  - Project Name: `ContentsRUs.Eventing.Publisher`
  - Location: src/.
  - Click "Next".
  - Select the framework (e.g., .NET 8.0).
  - Click "Create".
  - `dotnet sln add ContentsRUs.Eventing.Publisher/ContentsRUs.Eventing.Publisher.csproj`
- Add RabbitMQ.Client package:

  `

  > cd src/ContentsRUs.Eventing.Publisher
  > dotnet add package RabbitMQ.Client
  > `
  >
- Reference the core piranha project:

  `

  > dotnet add reference ../../core/Piranha/Piranha.csproj
  > `
  >
- Create `PiranhaEventPublisher.cs`
- Create `Program.cs`
- run ContentsRUs.Eventing.Publisher
- check RabbitMQ in the browser if has the messages

### 1.4 | JS Consumer PoC: use amqplib in Node.js to consume & print JSON from the queue

- created a js app in consumer-js
- run with npm start and is waiting for messages - ``npm run startProducer``

### Integratestructured JSON logging(Serilog) with trace IDs in the Publisher**

`

> cd src/ContentsRUs.Eventing.Publisher
> dotnet add package Serilog.AspNetCore
> dotnet add package Serilog.Sinks.Console
> dotnet add package Serilog.Enrichers.Thread
> dotnet add package Serilog.Enrichers.Environment
> dotnet add package Serilog.Enrichers.Process
> dotnet add package Serilog.Settings.Configuration
> `

- run ContentsRUs.Eventing.Publisher
- check console output

### 2.1 Build `ExternalEventListenerService` (IHostedService)

- Create new project to the solution:

  - Right-click on the Solution in Solution Explorer.
  - Select "Add" -> "New Project...".
  - Search for "Class Library".
  - Click "Next".
  - **Project Name:** `ContentsRUs.Eventing.Listener`
  - **Location:** src/.
  - Click "Next".
  - Select the framework (e.g., .NET 8.0).
  - Click "Create".
    `dotnet sln add ContentsRUs.Eventing.Listener/ContentsRUs.Eventing.Listener.csproj`
  - Create `ExternalEventListenerService.cs`

    - this will listen for messages and performe actions depending on the message from external sources, handles messages with actions depending on the routing key.
  - In the example/MvcWeb:

    - appsettings.json:  ``"RabbitMQ": { "HostName": "localhost", "Port": 5672, "UserName": "user", "Password": "password", "Exchange": "piranha.external.events", "Queue": "piranha.external.queue", "RoutingKey": "content.#" },``
    - In the Program.cs:

    `builder.Services.AddSingleton<IHostedService>(sp => new ExternalEventListenerService( sp.GetRequiredService<ILogger<ExternalEventListenerService>>(), builder.Configuration["RabbitMQ:HostName"] ?? "localhost", int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672"), builder.Configuration["RabbitMQ:UserName"] ?? "user", builder.Configuration["RabbitMQ:Password"] ?? "password", routingKey: "content.#" ));`
  - created the sender.js to send messages to the broker
- To see it working:

  ### 2.2 Define inbound DTO (`PromotionUpdatedEvent`) & log on receipt


  - Created new event DTO:

    - File: ContentsRUs.Eventing.Models/DomainEvent.cs
    - Proporties included:
      - Guid EventId
      - string ModelName
      - string Action
      - EventDetails Payload:
        - From another class EventDetails with:
          - string Title
          - string Description
          - object Body
      - DateTime Timestamp
  - Updated ExternalEventListenerService.cs in ContentsRus.Eventing.Listener:

    - Added switch case for routing key "content.#"
    - Deserializes the message body into a DomainEvent object
    - Logs the received event details using ILogger
  - How to test:

    - RabbitMQ running in docker
    - Run MvcWeb examples:
      - ``dotnet run --framework net8.0``
    - In RabbitMQ UI, go to piranha.external.events and publish a JSON message, like:
      - Routing key: content.#
      - Payload:
        ```
          {
            "EventId": "b7e6d63b-8f61-426c-b2c4-77181a0e7db9",
            "ModelName": "page",
            "Action": "create",
            "Payload: {
              ...
             },
            "Timestamp": "2025-05-21T14:10:00Z" 
          }
        ```
    - And you should see the logs in the terminal or CMS log file

  ### 2.5 Hook into Piranha Publish Events

  ### Logs

  - RabbitMQ running in docker
  - Run MvcWeb examples:

    - ``dotnet run --framework net8.0``
  - ``dotnet run --framework net8.0``
  - Run producer

    - ``npm run startProducer``
    - this will send a message that will trigger some logs in the piranha
  - ``npm run startProducer``
  - this will send a message that will trigger some logs in the piranha
  - PiranhaManager/PageApiController in `public async Task <PageEditModel>`` Save(PageEditModel model){}`

    - added the publish method
  - added the publish method
  - examples/MvcWeb/appsettigns.json:
    `"Serilog": { "Using": ["Serilog.Sinks.File"], "MinimumLevel": { "Default": "Information", "Override": { "Microsoft": "Warning", ... "Name": "ByIncludingOnly", "Args": { "expression": "SourceContext like '%Security%'" } } ] } } ] } `
- Adicionar no PageApiController os logs

### 2.x Trigger events from 3rd parties, secure.

- Created ContentRUs.Eventing.Shared:

  - helpers/MessageSecurityHelper.cs
    - With funcion to hash the user id
    - Function to ComputeHmacSignature and another to validate, to validate the source of the message
    - ValidateSecureContentEvent, to validate the structure of the message.
- Updated the ContentRUs.Eventing.Listener/externalEventListenerService:

  - to handle the messages receive, validate them and publish a page (event triggerd)
- MvcWeb

  - add Security - MessageSigningKey

### 2.x Serilogs - centralized

- define in the MVC appsettings.json the files and what to include
- In MVC program.cs added:
  - `builder.Host.UseSerilog((context, services, options) => { options .Enrich.FromLogContext() .Enrich.WithProperty("Application", "Piranha.CMS") .Enrich.WithMachineName() .Enrich.WithProcessId() .Enrich.WithThreadId() .WriteTo.Logger(lc => lc .Filter.ByIncludingOnly("SourceContext like '%RabbitMQ%'") .WriteTo.File( path: "Logs/Events/events-.log", rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {CorrelationId} {Message}{NewLine}{Exception}") ) .WriteTo.Logger(lc => lc .Filter.ByIncludingOnly("SourceContext like '%Security%'") .WriteTo.File( path: "Logs/Security/security-.log", rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {CorrelationId} {Message}{NewLine}{Exception}") ) .ReadFrom.Configuration(context.Configuration); // Read from appsettings.json if needed });`
- See logs in MvcWeb/logs
- Applyied some in tge EsternalEventListenerService

### 2.x Piranha Page CheckBox to enable Outbound subscription

What Was Planned

Under **Settings** in the Piranha admin, a new **"Eventing"** section would be created.

This section would list **all page types or content models**, each with two toggles:

- **“Publish on save”** – controls outbound event publishing
- **“Subscribe to inbound”** – enables inbound message processing

The configuration would be stored in a custom database table or Piranha’s `AppParam` store to allow runtime toggling without code changes.

---

What Was Implemented

Instead of a centralized "Settings" section, the implementation was simplified as follows:

- A custom boolean field named `PublishEvents` was added as a **region** to individual page types (e.g., a checkbox).
- On **save** or **delete** actions:
  - If `PublishEvents == true`, an **outbound event** is published via RabbitMQ.
  - If `PublishEvents == false` or not present, the action completes but no event is emitted.
- **Inbound subscription** logic was **not implemented** at this stage.

This approach allows **per-page-instance control** over event publishing without the need for a global admin UI.

---

Why

This design allows content editors to control whether events are triggered **per page**, with no code changes required (as long as the page type includes the `PublishEvents` region).

---

How to Verify

- Create or edit a page with `PublishEvents = false`, then save — **no outbound event** is published.
- Set `PublishEvents = true`, then save — an outbound event **is published** to RabbitMQ.
- Deleting a page follows the same logic — event only published if `PublishEvents == true`.

### 3.X Improved RabbitMQ and implemented Dead-Letter-Queue


feat(rabbitmq): robust event publishing & consuming for page lifecycle

* Implemented .NET event publisher library with DI, async init, and durable publishing for page events (publish, draft, unpublish, delete)
* Refactored MVC controllers to use injected publisher and config-based routing keys
* Standardized event payload structure and signing across .NET and Node.js
* Improved Node.js producer for durable exchange, config-driven routing, and HMAC signatures
* Developed flexible Node.js consumer: supports CLI selection of event types (page.published, page.deleted, page.draft), durable queue/exchange, and detailed logging
* Centralized RabbitMQ connection and routing config in appsettings.json and JS config
* Modified the  `ExternalEventListenerService` in order to redirect bad messages to the dead letter queue.
* Developed a service to listen to dead letters and log in a file, `ContentRus.Eventing.Listener/BackgrounfServices/DlqConsumerHostedService`
* Added badMessage to js producer to easy testing

### 3.X More logs

Improved serilog to have files for publishing, listening and dead letter queues

### 3.X Create Prometheus and Grafana containers

- Install the packet Prometeus for ASP.NET Core:
  - On the MvcWeb folder execute:

    ```
    dotnet add package prometheus-net.AspNetCore
    ```
  - Expose endpoint ``/metrics`` with some changes in ``Program.cs``, like ``endpoints.MapMetrics()``
  - Creation of the Observability folder and a file named ``prometheus.yml``:

    ```yml
    global:
    scrape_interval: 5s

    scrape_configs:
      - job_name: 'piranha_app'
        static_configs:
          - targets: ['host.docker.internal:5000']
    ```
  - Create docker-compose.observability.yml:

    ```yml
    version: '3.8'

    services:
      prometheus:
        image: prom/prometheus:latest
        container_name: prometheus
        volumes:
          - ./prometheus.yml:/etc/prometheus/prometheus.yml
        command:
          - --config.file=/etc/prometheus/prometheus.yml
        ports:
          - "9090:9090"

      grafana:
        image: grafana/grafana:latest
        container_name: grafana
        ports:
          - "3000:3000"
        volumes:
          - grafana-storage:/var/lib/grafana

    volumes:
      grafana-storage:
    ```
  - Up the containers:

    ```
    docker compose -f docker-compose.observability.yml up -d
    ```
  - Access to Prometheus and Grafana:

    - Prometheus: http://localhost:9090
    - Grafana: http://localhost:3000
      - Login: ``admin/admin``
  - Configure Grafana with Prometheus:

    - Open Grafana
    - Go to Settings > Data Sources > Add data source
    - Choice Prometheus

      - In URL, put http://localhost:9090
    - Click Save & Test
    - It is possible now to:

      - Create dashboards
      - Import public dashboards
      - Visualize metrics, like: ``http_requests_total``, ``dotnet_gc_collection_count``, etc

### 3.X Prometheus Alerts and Grafana Dashboards (Observability)

To ensure robust observability in our system, we integrated **Prometheus** for monitoring and **Grafana** for visualization of metrics. This setup allows us to track performance, detect anomalies, and react promptly to failures.

We used the `prometheus-net.AspNetCore` library to expose application metrics at the `/metrics` endpoint in Prometheus format.

#### Metrics Implemented 

 Message Processing

- `listener_messages_received_total` – Total number of messages received from RabbitMQ.
- `listener_messages_invalid_json_total` – Messages rejected due to invalid JSON.
- `listener_messages_invalid_signature_total` – Messages rejected due to invalid signature.
- `listener_messages_schema_invalid_total` – Messages rejected due to schema validation failure.
- `listener_messages_acked_total` – Messages successfully acknowledged.
- `listener_messages_nacked_total` – Messages negatively acknowledged.
- `listener_message_processing_duration_seconds` – Duration of message processing.
- `listener_messages_deadlettered_total` – Messages sent to the Dead Letter Queue (DLQ).

Event Handling

- `listener_events_processed_total` – Total events processed (labeled by event type).
- `listener_events_failed_total` – Total failed events (labeled by event type).
- `listener_event_processing_duration_seconds` – Duration of event processing (labeled by event type).

Content Operations

- `listener_content_create_attempts_total` – Number of content creation attempts.
- `listener_content_create_failures_total` – Number of content creation failures.
- `listener_content_create_duration_seconds` – Time taken for content creation.
- `listener_content_delete_attempts_total` – Number of content deletion attempts.
- `listener_content_delete_failures_total` – Number of content deletion failures.
- `listener_content_delete_duration_seconds` – Time taken for content deletion.

RabbitMQ Connectivity

- `listener_rabbitmq_connections_total` – Successful connections to RabbitMQ.
- `listener_rabbitmq_setup_failures_total` – Setup failures (labeled by step).
- `listener_consumer_active` – Indicates if the consumer is active (gauge metric).

Publisher Metrics

- `publisher_messages_published_total` – Total number of messages successfully published.
- `publisher_publish_failures_total` – Total number of failed message publications.

These metrics provide insight into both internal performance and external system interactions.

#### Grafana Dashboards

Grafana was used to visualize Prometheus metrics through custom dashboards, enabling us to:

- Publisher statistics (success/failure counts and ratios).
- Message flow: received, acknowledged, rejected, and dead-lettered.
- Event types processed by the listener and the publisher (e.g., content creation).
- Listener Statistics Dashboard (e.g., percentage of invalid messages, processing success rate or average processing time).
- RabbitMQ health indicators and consumer activity.
- API-triggered events (e.g., page save or delete actions).


#### Prometheus Alerts

As part of the monitoring infrastructure, several **Prometheus alerts** were defined to ensure the reliability and performance of the RabbitMQ messaging system. These alerts are configured in [`Observability/alerts.rules.yml`](Observability/alerts.rules.yml) and focus on key indicators of system health:

### 🔔 Defined Alerts

- **HighDLQMessageCount**
  - **Condition:** More than 10 messages in the Dead Letter Queue (DLQ) for over 5 seconds.
  - **Purpose:** Detects potential issues in message processing that result in frequent DLQ usage.

- **QueueBacklog**
  - **Condition:** More than 50 unprocessed messages remain in a queue for over 2 minutes.
  - **Purpose:** Highlights possible processing bottlenecks or consumer lag.

- **RabbitMQNodeDown**
  - **Condition:** RabbitMQ node is unreachable for more than 30 seconds.
  - **Purpose:** Indicates potential RabbitMQ outages or network failures.

These alerts help ensure **early detection** of failures and support rapid **issue resolution** to maintain system stability and performance.


### 4.X Optional: Swagger/OpenAPI docs for new endpoints

  - Updated ``Program.cs`` to add Swashbuckle (Swagger) services and middleware for API documentation:
    - Registered Swagger generator with ``builder.Services.AddSwaggerGen(...)``
    - Configured Swagger UI middleware with ``app.UseSwagger()`` and ``app.UseSwaggerUI()``
    - Added ``MetricsDocumentFilter`` to enrich the Swagger document with ``/metrics`` and endpoint.
  - Implemented a custom Swagger Document Filter (``MetricsDocumentFilter``) to manually add the ``/metrics`` endpoint to the OpenAPI spec
  - This was necessary because the ``/metrics`` endpoint is served by Prometheus middleware outside of the MVC pipeline and is not automatically detected by Swagger
  - Added XML comments and descriptions for both endpoints to improve API documentation quality

  - How to access:
    - Run the application default on port 5000
    - Navigate to ```http://localhost:5000/swagger`` in the browser
    - The Swagger UI shows all documented endpoints, including the custom ``/metrics``