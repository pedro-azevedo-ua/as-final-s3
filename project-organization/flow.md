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

- Ran the Docker container:

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