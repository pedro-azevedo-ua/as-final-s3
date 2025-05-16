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

  * Right-click on the Solution in Solution Explorer.
  * Select "Add" -> "New Project...".
  * Search for "Class Library".
  * Click "Next".
  * **Project Name:** `ContentsRUs.Eventing.Publisher`
  * **Location:** src/.
  * Click "Next".
  * Select the framework (e.g., .NET 8.0).
  * Click "Create".
  * ```
    dotnet sln add ContentsRUs.Eventing.Publisher/ContentsRUs.Eventing.Publisher.csproj
    ```
- Add RabbitMQ.Client package:

  ```
  > cd src/ContentsRUs.Eventing.Publisher
  > dotnet add package RabbitMQ.Client
  ```
- Reference the core piranha project:

  ```
  > dotnet add reference ../../core/Piranha/Piranha.csproj
  ```
- Create `PiranhaEventPublisher.cs`
- Create `Program.cs`
- run ContentsRUs.Eventing.Publisher
- check RabbitMQ in the browser if has the messages

### 1.4 | JS Consumer PoC: use amqplib in Node.js to consume & print JSON from the queue

- created a js app in consumer-js
- run with npm start and is waiting for messages
