## 🚀 Phase 1: Kickoff & Outbound Prototype

**Dates:** May 14–20

| 🔲 | Task                                                                                | Owner |    Due    | ⏱️ Complexity |
| :-: | :---------------------------------------------------------------------------------- | :---- | :-------: | :-------------: |
| 1.1 | **Get Piranha CMS running locally**                                           |       | May 16 AM |       Low       |
| 1.2 | **Spin up RabbitMQ via Docker & confirm UI**                                  |       | May 16 AM |       Low       |
| 1.3 | **Scaffold** `PiranhaEventPublisher` **project**                      |       | May 16 PM |     Medium     |
| 1.4 | Integrate**structured JSON logging**(Serilog) with trace IDs in the Publisher |       |  May 20  |     Medium     |

---

## 🔄 Phase 2: Inbound Listener & Admin UI

**Dates:** May 17–25

| 🔲 | Task                                                                                            | Owner |  Due  | ⏱️ Complexity |
| :-: | :---------------------------------------------------------------------------------------------- | :---- | :----: | :-------------: |
| 2.1 | **Build** `ExternalEventListenerService` **(IHostedService)**                     |       | May 21 |     Medium     |
| 2.2 | Define inbound DTO (`PromotionUpdatedEvent`) and**log on receipt**                      |       | May 22 |       Low       |
| 2.3 | **Piranha Admin UI** : toggle inbound/outbound per content model                          |       | May 25 |      High      |
| 2.4 | Add basic**ASP NET Core** `/health`endpoint (RabbitMQ connectivity + key config checks) |       | May 25 |       Low       |

---

## 🔐 Phase 3: Security & Observability

**Dates:** May 26–June 1

| 🔲 | Task                                                                   | Owner |  Due  | ⏱️ Complexity |
| :-: | :--------------------------------------------------------------------- | :---- | :----: | :-------------: |
| 3.1 | **HMAC-SHA256 signing**in Publisher                              |       | May 29 |     Medium     |
| 3.2 | **Admin UI**for managing outbound secret keys                    |       | May 30 |     Medium     |
| 3.3 | **Verify HMAC**in listener; reject or DLQ invalid messages       |       | June 1 |      High      |
| 3.4 | **Metrics**(Prometheus counters: published, consumed, failures)  |       | June 1 |     Medium     |
| 3.5 | **Tracing**(OpenTelemetry spans around publish/consume/HMAC ops) |       | June 1 |      High      |
| 3.6 | **Demo “runbook” script**& tidy README                         | Group | June 1 |       Low       |

---

## ✅ Phase 4: Final Prep & Demo

**Dates:** June 2–4

| 🔲 | Task                                                                        | Owner |  Due  | ⏱️ Complexity |
| :-: | :-------------------------------------------------------------------------- | :---- | :----: | :-------------: |
| 4.1 | **Freeze code** ; push to repo with build/run instructions            | Group | June 3 |       Low       |
| 4.2 | **Dry-run**end-to-end demo; capture screenshots/video backup          | Group | June 3 |     Medium     |
| 4.3 | **Slide deck polish** : architecture diagram, key features, demo flow | Group | June 3 |     Medium     |
| 4.4 | **Demo Day!**Present secure, event-driven CMS flow                          | Group | June 4 |       —       |

---

## 🌟 Optional Tasks (if time permits)

* [ ] **Retry Logic** in Publisher/Consumer for transient RabbitMQ errors
* [ ] **Load Test** end-to-end flow (e.g., 1,000 publishes/min) and capture metrics
* [ ] **DLQ Dashboard** : simple UI showing messages dead-lettered with reasons
* [ ] **Role-Based Access** in Admin UI (who can toggle inbound/outbound)
* [ ] **Unit-tests** for Publisher, Listener, and HMAC modules
* [ ] **Swagger/OpenAPI** docs for any new HTTP endpoints
