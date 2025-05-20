
## 🚀 Phase 1: Kickoff & Outbound Prototype

### 1.1 Get Piranha CMS running locally

* **What:** Clone the Piranha CMS demo repository (e.g. `PiranhaCMS/piranha.core`), restore NuGet packages, apply any required database migrations.
* **Why:** Ensures everyone starts from a known baseline and can run the web project.
* **How to Verify:**
  * Navigate to `https://localhost:5001` (or your port).
  * You can log into the admin UI, create a simple page, and see it render on the front end.

### 1.2 Spin up RabbitMQ via Docker & confirm UI

* **What:**
  * Create a `docker-compose.yml` (or `docker run`) for RabbitMQ with the Management Plugin enabled (`rabbitmq:3-management`).
  * Expose ports `5672` (AMQP) and `15672` (HTTP UI).
* **Why:** Provides the broker for all event traffic.
* **How to Verify:**
  * Visit `http://localhost:15672`, log in with default credentials (`guest`/`guest`), and see the Overview page.
  * Confirm you can create a test queue and publish a manual message via the UI.

### 1.3 Scaffold `PiranhaEventPublisher` project

* **What:**
  * In your solution, add a new Class Library (or Console App) named `ContentsRUs.Eventing.Publisher`.
  * Reference `RabbitMQ.Client` and `Piranha.Core`.
  * Add a service class (e.g. `PiranhaEventPublisher`) with methods like `PublishAsync<T>(T @event)`.
* **Why:** Isolates all outbound logic in a reusable component.
* **How to Verify:**
  * From `Main()`, call `PublishAsync(new { Test = "Hello" })` and ensure it connects, declares an exchange, and sends a message.

### 1.4 Integrate structured JSON logging (Serilog) with trace IDs

* **What:**
  * Add `Serilog.AspNetCore` and configure in `appsettings.json` for JSON output.
  * In `PiranhaEventPublisher`, generate a `TraceId` (GUID) for each call and include it in the log context.
  * Log at INFO level: exchange name, routing key, payload size, trace ID.
* **Why:** Gives you machine-readable logs and a way to correlate events.
* **How to Verify:**
  * Run the Publisher; check console logs in JSON format showing the required fields.

---

## 🔄 Phase 2: Inbound Listener & Admin UI

### 2.1 Build `ExternalEventListenerService` (IHostedService)

* **What:**
  * In a new class library (or within the CMS web project), create a class implementing `IHostedService`.
  * In `StartAsync`, connect to RabbitMQ, declare the same exchange(s), bind to a queue, and register a `EventingBasicConsumer`.
  * In `StopAsync`, gracefully close the channel and connection.
* **Why:** Keeps the CMS always listening for external events.
* **How to Verify:**
  * On application start, verify via logs that the queue is declared and the consumer is attached.

### 2.2 Define inbound DTO (`PromotionUpdatedEvent`) & log on receipt

* **What:**
  * Create a C# model with relevant properties (`PromotionId`, `Name`, `Payload`, `Timestamp`).
  * In the consumer’s `Received` callback, deserialize the JSON, log all properties at DEBUG level, then `Ack()` the message.
* **Why:** Provides a clear contract for incoming messages and visibility when they arrive.
* **How to Verify:**
  * From RabbitMQ UI, publish a JSON message to the exchange with the correct routing key.
  * See the CMS console or file logs output the deserialized DTO.

### 2.3 Piranha Admin UI: toggle inbound/outbound per content model

* **What:**
  * Under “Settings” in the Piranha admin, add a new section “Eventing.”
  * List all page types or content models with checkboxes for “Publish on save” and “Subscribe to inbound.”
  * Save settings to a custom database table or Piranha’s `AppParam` store.
* **Why:** Lets content admins decide which models generate or consume events without code changes.
* **How to Verify:**
  * Flip a “Publish on save” toggle off, create content—observe that no outbound event fires.
  * Enable “Subscribe to inbound,” publish a test inbound message—see it processed.

### 2.4 Add basic ASP NET Core `/health` endpoint

* **What:**
  * Install `Microsoft.Extensions.Diagnostics.HealthChecks` and add health checks for:
    1. RabbitMQ connectivity (attempt a brief connect/ping).
    2. Presence of at least one configured signing key (Phase 3).
  * Expose `/health` returning JSON with “Healthy”/“Unhealthy” status.
* **Why:** Gives automated systems (and you) a quick “is it up?” check.
* **How to Verify:**
  * Hit `https://localhost:5001/health` and see all checks reported as healthy once RabbitMQ is running.

---

## 🔐 Phase 3: Security & Observability

### 3.1 HMAC-SHA256 signing in Publisher

* **What:**
  * Take a UTF-8 payload, compute `HMACSHA256(payload, secretKey)`, Base64-encode the result.
  * Add it as a message header (e.g. `"X-Signature"`).
* **Why:** Guarantees outbound message integrity and authenticity.
* **How to Verify:**
  * Inspect the raw AMQP message in RabbitMQ UI or use a small consumer to read headers.

### 3.2 Admin UI for managing outbound secret keys

* **What:**
  * In the “Eventing” settings page, add a secure field to enter or rotate the HMAC secret.
  * Encrypt at rest (e.g. using ASP NET Data Protection) before saving to DB.
* **Why:** Allows operations teams to change keys without redeploying code.
* **How to Verify:**
  * Rotate the key, publish a message, and see the new signature in the header.

### 3.3 Verify HMAC in listener; reject or DLQ invalid messages

* **What:**
  * On message receipt, recompute HMAC over the body using the stored secret.
  * If it doesn’t match the header, `BasicNack` with `requeue: false` (dead-letter).
* **Why:** Prevents processing of tampered or unauthorized messages.
* **How to Verify:**
  * Publish a message with an incorrect signature—confirm it lands in the DLQ and is not processed.

### 3.4 Metrics (Prometheus counters)

* **What:**
  * Add `prometheus-net` library.
  * Create counters: `events_published_total`, `events_consumed_total`, `events_failed_total`.
  * Expose `/metrics` endpoint.
* **Why:** Enables you to track throughput and error rates over time.
* **How to Verify:**
  * Scrape the metrics endpoint (e.g. via `curl`) and see counters increasing as you publish/consume.

### 3.5 Tracing (OpenTelemetry spans)

* **What:**
  * Add `OpenTelemetry.Exporter.Console` (or another exporter).
  * For each publish/consume/HMAC operation, start a span with attributes (e.g., `exchange`, `routing_key`, `trace_id`).
* **Why:** Allows end-to-end visibility in a distributed tracing tool.
* **How to Verify:**
  * Run the app; observe spans in the console or local collector showing hierarchical timing.

### 3.6 Demo “runbook” script & tidy README

* **What:**
  * Write markdown instructions:
    1. Prerequisites (Docker, .NET SDK).
    2. How to configure secrets & toggles in UI.
    3. Steps to publish inbound/outbound.
  * Update the repo’s `README.md` with build and run commands, port mappings, and health/metrics URLs.
* **Why:** Ensures your instructor or a teammate can reproduce the demo.
* **How to Verify:**
  * Follow the runbook on a fresh clone and complete a sample publish/consume cycle without additional help.

---

## ✅ Phase 4: Final Prep & Demo

### 4.1 Freeze code & push to repo

* Tag the final commit (e.g. `v1.0-demo`) and ensure all branches are merged.

### 4.2 Dry-run end-to-end demo; capture backup

* Perform the exact steps from your runbook, time each segment.
* Take screenshots or record video snippets as a fallback.

### 4.3 Slide deck polish

* **Slides Structure:**
  1. **Problem & Vision** (why event-driven, multi-tenant, secure)
  2. **Architecture Diagram** (modules, exchanges, queues, UI)
  3. **Key Features** (publishing, listening, security, observability)
  4. **Live Demo Flow** (outline steps, show metrics/traces)
* Keep it under 10 slides.

### 4.4 Demo Day!

* Assign speakers for each section (e.g., one person covers architecture, another runs the live demo).
* Stick to a 15–20 min window: 5 min overview, 10 min demo, 5 min Q&A.

---

## 🌟 Optional Tasks (time‐permitting)

* **Retry Logic:** exponential back-off for transient RabbitMQ failures
* **Load Test:** use a simple script (e.g. `hey`, `k6`) to send 1 K messages/minute, observe `/metrics`
* **DLQ Dashboard:** a small Razor page showing dead-letter queue contents and failure reasons
* **RBAC in UI:** limit who can toggle settings based on Piranha roles
* **Unit Tests:** xUnit tests for publisher, listener, HMAC validation
* **Swagger/OpenAPI:** auto-generate docs for your `/health` and `/metrics` endpoints

---

Use these detailed checklists as your “acceptance criteria” for each card. Good luck—you’ve got this!
