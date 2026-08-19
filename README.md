# WFE - Workflow Engine (work in progress)

## Status as of this export

Built and included in this solution:
- **WFE.Models** - EF Core entity classes
- **WFE.Core** - schema parsing/model, engine contracts, the lightweight rule engine, the
  Expression condition evaluator, PublishMessage action
- **WFE.Runtime** - TransitionEngine, WorkflowRuntime (the execution loop), CommandService,
  ActionExecutorRegistry/ConditionExecutorRegistry
- **WFE.Persistence** - WfeDbContext, EF entity configurations, EF implementations of the
  Core persistence contracts
- **WFE.Web** - ASP.NET Core Web API, DI composition root (Program.cs), and four controllers:
  - `POST /api/schemes`, `GET /api/schemes/{id}`, `POST /api/schemes/{id}/publish` -
    save a designer-exported scheme, publish it into a runnable WfeProcessScheme snapshot
  - `POST /api/instances/start`, `GET /api/instances/{id}` - start/inspect instances
  - `GET /api/instances/{id}/commands`, `POST /api/instances/{id}/commands/{name}` -
    list/invoke commands
  - `POST /api/ingestion/packets` - the endpoint your OPC-UA/MQTT microservice posts
    packets to (requires `ActorId` identifying the calling service)
  - Swagger UI at `/swagger` (Development environment only, per `launchSettings.json`'s
    `ASPNETCORE_ENVIRONMENT=Development`) - browse and try every endpoint above from there.
- **Built-in actions** (`WFE.Runtime/BuiltInActions`) - `SetParameter`, `RemoveParameter`,
  `AddNumberToParameter`, `FileWrite`, `FileRead`, `FileDelete`, `HTTPRequest` - all matched
  against your actual sample XMLs' `ActionParameter` JSON shapes.
- **Built-in conditions** (`WFE.Runtime/BuiltInConditions`) - `CheckParameter`,
  `CheckHTTPRequest`, `LoopIsNotCompletedAndBroken` - matched against your samples.
- **Loops (Phase 2)** - `StartLoopFor` (numeric/DateTime counter, matches `LoopForDateTime.xml`)
  and `StartLoopForeach` (delimited list or list-from-parameter, matches `LoopForeach.xml`).
  Both are re-entrant: the same activity runs once per iteration (init on first entry, advance
  on every re-entry via the schema's own loop-back transition), with internal bookkeeping
  namespaced by `LoopName` so `LoopIsNotCompletedAndBroken` can find it independent of whatever
  parameter names the schema exposes.
- **Parallel/Subprocess (Phase 3)** - matches `ParallelProcessesWithoutWaiting.xml` and
  `ParallelProcessesWithWaiting.xml`. A `Fork+Start` transition enqueues a durable work item
  (`WfeProcessWorkItem`) instead of spawning inline; `WFE.Runtime.Scheduling.SubprocessWorker`
  (a `BackgroundService`) polls that queue and runs each subprocess independently - the
  original caller (e.g. a packet-ingestion request) never waits on it. A `Fork+Finalize`
  transition ends a subprocess instance's own execution and merges its parameters into the
  parent (`OverwriteAllNulls` strategy). `CheckAllSubprocessesCompleted` implements the
  AND-join (`WaitingForMerge` in the sample) by counting enqueued-vs-completed forks.
  `WfeProcessInstance` gained `ParentInstanceId`/`RootInstanceId`/`ForkTransitionName`.

**Not yet built:**
- **`LoggingMessageBroker` is a placeholder that only logs - it does not publish anywhere.**
  Replace `WFE.Web/Infrastructure/LoggingMessageBroker.cs` with a real RabbitMQ/Kafka/MQTT/etc
  client before relying on PublishMessage actions.
- Loops, parallel/subprocess execution, dynamic CodeActions, and the Schedule trigger type
  (Phases 2-4) - the schema model parses their XML shape, nothing executes them yet.
  (**Loops, Parallel/Subprocess, dynamic CodeActions, and the Schedule trigger are all now
  built** - CodeActions are disabled by default, see the dedicated section below.)
- No EF Core migrations exist yet (see below).
- No authentication/authorization on any endpoint - add before exposing this beyond a
  trusted internal network.

## Schedule trigger type

Not shown in any of your sample XMLs, so this shape is my own design - built **general-purpose**
per your instruction, not tied to the sensor-packet use case:

```xml
<Trigger Type="Schedule">
  <ScheduleParameter><![CDATA[{"Mode":"Interval","IntervalSeconds":30}]]></ScheduleParameter>
</Trigger>
<!-- or -->
<Trigger Type="Schedule">
  <ScheduleParameter><![CDATA[{"Mode":"TargetDateTime","ParameterName":"NextRunTime"}]]></ScheduleParameter>
</Trigger>
```

- `Interval` - re-check every N seconds (industrial polling: "check the sensor threshold again
  in 30s while nothing's changed").
- `TargetDateTime` - fire once the instance's own `ParameterName` parameter (a DateTime string)
  has passed (generic business process: "escalate if `@Deadline` has passed", "follow up at
  `@NextContactDate`").
- Cron-style expressions were considered and **deliberately not built** (real dependency/
  complexity cost for uncertain payoff) - the `Mode` field leaves room to add one later without
  a breaking change.

Mechanically: `WfeProcessInstance.NextScheduledCheckTime` is set whenever an instance parks in
`Waiting` at an activity with an outbound Schedule transition (computed as the earliest of all
such transitions' next-fire-times, if more than one). `WFE.Runtime.Scheduling.ScheduleWorker`
(another `BackgroundService`, same pattern as `SubprocessWorker`) polls for instances past that
time and calls `IWorkflowRuntime.ResumeScheduledAsync`, which evaluates the Schedule
transition's conditions exactly like an Auto transition would (Always/Expression/Action/
Otherwise, same two-pass resolution) and continues the normal Auto-transition loop from there.
Polling interval is `ScheduleWorker:PollingIntervalMs` (default 1000ms) - that's the real
latency floor regardless of how short an `IntervalSeconds` you configure on a transition.

## Dynamic CodeActions - read before enabling

**Disabled by default.** Set `CodeActions:Enabled=true` in `appsettings.json` to turn it on.
Before you do:

- **This is arbitrary code execution as a feature.** A `<CodeAction>` in a published schema
  compiles and runs real C# with the full permissions of the `WFE.Web` process. Modern .NET has
  no in-process sandboxing (no AppDomain/CAS like old .NET Framework) - there is no code-level
  boundary here, only whatever access control sits in front of `SchemeDesignerController`'s
  save/publish endpoints. **Never enable this unless those endpoints are authenticated and
  restricted to people you'd trust to run code on this server directly.**
- I deliberately did **not** build a namespace/API blocklist (you were offered one and declined) -
  a CodeAction body can reference anything the compiled assembly can reach, with no restriction.
- **This is a NEW contract, not compatibility with your sample XML's CodeAction body.** Your
  `ParametersAndExpressions.xml` sample's `AddNumberToParameter` CodeAction was written against
  a different, third-party product's runtime (`OptimaJet.Workflow.Core.Runtime`,
  `WF.Sample.Business.Workflow`, etc.) - those assemblies don't exist in this solution and
  can't. Pasting that exact body in will fail to compile with a clear "namespace not found"
  error - that's expected, not a bug. A CodeAction body written for **this** engine gets two
  variables in scope: `context` (a `WorkflowExecutionContext` - `context.Instance`,
  `context.Parameters.GetAsync/SetAsync`, etc.) and `parameters` (this CodeAction's own declared
  `<Parameter>` values, already bound against the incoming JSON with defaults/required-checks
  applied). See the doc comment in `CodeActionCompiler.cs` for the exact generated wrapper shape.
- Only `Type="Action"` (wired through `ActionRef`) and `Type="Condition"` (wired through
  `Condition NameRef`, **my assumption** for the natural counterpart - no sample shows one) are
  supported. `<Parameters>` binding, compile caching (per schema, since schemes are immutable
  once published), and surfaced compiler diagnostics on failure are all implemented - this
  directly fixes the two real bugs found reviewing your `WfeRuntime.cs`: diagnostics used to be
  unreachable dead code after an early `throw`, and the reference list was rebuilt on every
  single compilation instead of cached once.

## WFE.Client - evaluation harness

A separate, standalone ASP.NET Core MVC project (no project reference to the engine - it talks
to `WFE.Web` purely over HTTP, the way your real ingestion microservice would):

- **`Services/RabbitMqSubscriberService.cs`** - a `BackgroundService` using **RabbitMQ.Client**
  that consumes from an existing queue and forwards every message to the engine's
  `POST /api/ingestion/packets`. Does **not** declare exchanges/bindings by default
  (`RabbitMq:DeclareQueue=false`) - it assumes your queue/exchange/bindings already exist and
  only consumes. Messages are manually ack'd only after the engine call succeeds; a failed
  ingest Nacks with requeue - fine for evaluation, but add a dead-letter policy on your queue if
  you need better isolation from a persistently-failing engine. **Disabled by default**
  (`RabbitMq:AutoConnect=false`) so the app runs with zero broker dependency until you point the
  connection settings at your real instance.
- **Dashboard at `/`** - connection status, a manual "send a test packet" form (simulates a
  broker message with no broker needed - the fastest way to evaluate the engine), and a
  round-trip-timed log of recent activity (instance id, resulting activity/status, latency).
- **Message shape assumption**: if a message payload parses as JSON with `Tag`/`Value` keys,
  those are used (rest of the object becomes packet Metadata); otherwise the message's routing
  key becomes `Tag` and the raw payload becomes `Value`. Adjust `HandleMessageAsync` if your
  actual packets look different.

**Before running:** set `RabbitMq:HostName`/`Port`/`VirtualHost`/`UserName`/`Password`/`QueueName`
in `WFE.Client/appsettings.json` to match your already-running RabbitMQ setup, set
`RabbitMq:AutoConnect=true`, and set `WfeApi:ProcessSchemeId` to a scheme you've already
published on the engine. Also, since `WfeApi:BaseUrl` defaults to `WFE.Web`'s HTTPS dev URL,
you'll need to trust the local dev cert once (`dotnet dev-certs https --trust`) or the client's
HTTP calls will fail with an SSL error - the client itself has no certificate configuration to
fix, this is purely a local-dev-environment step.

## Test-from-WfeScheme fast path (engine) + batch records (client)

Two related additions, both from your latest request:

- **`IWorkflowRuntime.StartInstanceFromSchemeAsync` / `ProcessPacketFromSchemeAsync`** - a new
  engine capability alongside the existing publish-then-start flow. Point it at a `WfeScheme.Id`
  directly; internally it calls the new `IProcessSchemeProvider.CreateSnapshotAsync`, which
  copies that scheme's **current** XML into a brand-new `WfeProcessScheme` row at that exact
  moment and starts the instance against it. Editing the `WfeScheme` afterward can never
  retroactively affect an instance already running - it's working from its own frozen copy, per
  your description. **Every call creates a new `WfeProcessScheme` row** (no reuse, no
  supersede-previous bookkeeping) - fine for rapid test iteration, but repeated test runs will
  accumulate rows over time; nothing here cleans those up automatically. The instance's
  `Status`/`Activity`/`State` are tracked and persisted continuously as it executes, exactly as
  before - this was already the existing behavior, not something new. `POST /api/instances/start`
  and `POST /api/ingestion/packets` both now accept **either** `ProcessSchemeId` (the original,
  already-published-snapshot path) **or** `WfeSchemeId` (this new fast path) - provide exactly
  one. `SchemeDesignerController.Publish` was refactored to share the same snapshot-creation
  code rather than duplicating it.

- **`WFE.Client` now expects batch messages, not single packets.** `RabbitMqOptions` config
  changed from `ProcessSchemeId` to `WfeSchemeId` (matches the fast path above - the client
  always tests against a `WfeScheme`, never a pre-published snapshot). Each RabbitMQ message is
  now parsed as a JSON array of up to ~500 `SensorRecordDto` rows
  (`DeviceId`/`Tag`/`Value`/`Timestamp` + anything else via `[JsonExtensionData]`, since you
  didn't specify the exact extra field names - adjust `SensorRecordDto` if yours differ). Each
  record starts its own instance, processed with bounded concurrency
  (`RabbitMq:MaxConcurrentIngests`, default 10) instead of firing 500 simultaneous HTTP calls.
  **Ack semantics changed accordingly**: the whole message is Ack'd once every record has been
  attempted, regardless of individual failures - Nacking a 500-row batch over one bad record
  would redeliver (and duplicate-instance) the 499 that already succeeded. See the doc comment
  in `RabbitMqSubscriberService.cs` for the full reasoning; add per-record dead-lettering on
  your side if you need stronger guarantees than "logged and visible on the dashboard" for
  individual failures.

## Test auto-advancer (WFE.Client) - not a production pattern

`Services/TestAutoAdvancerService.cs` polls `GET /api/instances?status=Waiting` and
auto-invokes a Command on each so an evaluation run doesn't stall waiting for a manual click.
**Disabled by default** (`TestAutoAdvancer:Enabled=false`). Deliberately leaves alone any
instance Waiting on a Schedule trigger (those have zero available Commands) - that automation
genuinely belongs in `WFE.Web`'s `ScheduleWorker`, not here. If a workflow needs multiple
sequential Command-gated steps, it advances one step per `TestAutoAdvancer:PollingIntervalMs` -
that interval is effectively how fast such a workflow flows through during a test run. Set
`TestAutoAdvancer:PreferredCommandName` to constrain it to one specific command instead of
"whichever is first available." This required a new generic endpoint,
`GET /api/instances?status=&take=`, on the engine (`ProcessInstanceController`) - useful for
monitoring in general, not just this feature.

**Why this isn't in `WFE.Web` instead:** a background process that silently invokes
human/external-gated Commands defeats the reason those transitions are Command-triggered rather
than Auto in the first place. It only belongs in the client, clearly framed as a test
convenience with its own distinct actor id (`client:test-auto-advancer`) in the audit trail -
never mistake this for how a production deployment should advance Command-gated transitions.

## Assumptions worth double-checking against your actual designer output

- **Parallel/Subprocess assumptions (all in `WorkflowRuntime.cs`/`CheckAllSubprocessesCompletedCondition.cs`):**
  - Only `SubprocessStartupType="AnotherThread"` and `SubprocessStartupParameterCopyStrategy="CopyAll"`
    are implemented for Fork+Start transitions - anything else throws (faults the instance)
    rather than silently doing something unintended.
  - Only `SubprocessFinalizeParameterMergeStrategy="OverwriteAllNulls"` is implemented for
    Fork+Finalize transitions - same fail-loud approach for anything else.
  - `CheckAllSubprocessesCompleted` only implements `Mode="AllSubprocessesAndParent"` (the only
    value your sample shows), and counts **every** fork ever spawned by the instance regardless
    of which transition spawned it. If a schema forks from more than one distinct fork point on
    the same instance, this join waits for all of them together, not per-fork-point - neither
    sample needed that distinction, so I didn't build it. Flag me if you need it.
  - A spawned subprocess runs against the **same** WfeProcessScheme as its parent - there's no
    support for a fork pointing at a genuinely different scheme (not shown in either sample).
  - **Real latency cost**: `SubprocessWorker` polls every `SubprocessWorker:PollingIntervalMs`
    (default 500ms) - a fork enqueued right after a poll can sit for up to that long before the
    subprocess actually starts. For your packet pipeline, if a forked subprocess needs to
    finish within a tight per-packet budget, lower this value (at the cost of more idle-poll
    load) or reconsider whether that particular fork should be async at all.
- **`StartLoopFor`'s `CounterType`**: only `"DateTime"` appears in your sample. I implemented
  `"Number"` as the numeric counterpart based on the field shape, but that literal is a guess -
  confirm what your designer actually emits for a numeric loop and I'll adjust.
- **`MaxAutoHops` was raised from a hardcoded 100 to a configurable 10,000**
  (`WorkflowRuntime:MaxAutoHops` in `appsettings.json`). Your own `LoopForDateTime.xml` sample
  (32 daily iterations x ~3 hops) already used ~96 of the old 100-hop budget - it would have
  intermittently faulted a perfectly correct schema. Loops with more iterations than that
  should raise this further.
- **Loop iterations amplify the parameter-store write cost** flagged back in the Persistence
  step (`EfWorkflowParameterStore` does one `SaveChangesAsync` per parameter write). A
  32-iteration date loop is now ~3 parameter writes/iteration = ~100 DB round-trips just for
  loop bookkeeping. Still functionally correct, but worth batching (per-activity, not per-call)
  before running loops with thousands of iterations.
- **`CheckParameter`** accepts either `"Parameter"` or `"ParameterName"` as the key for which
  parameter to check, since your own sample exports use both inconsistently
  (`FileWriteRead.xml` uses `Parameter`, others use `ParameterName`).
- **`CheckHTTPRequest`** only *reads back* a previously-stored HTTPRequest response - it does
  NOT re-issue an HTTP call itself, even though the sample's `ActionParameter` also carries
  `Url`/`Post`/`ContentType`/`Parameters` fields. See the doc comment in
  `CheckHttpRequestCondition.cs` for the reasoning; flag me if that's wrong.
- **`FileWrite`/`FileRead`/`FileDelete`** confine every `Path` to a sandboxed root
  (`FileActions:RootPath` in `appsettings.json`, defaults to `wfe-files` under the app's base
  directory) - a schema can never read/write/delete outside that root, even with a
  path-traversal attempt like `"Path":"../../../etc/passwd"`.
- **`HTTPRequest`** ignores the sample's `"Parameters":"true"` field - `AddProcessInstanceParameters`
  is treated as the real toggle for whether instance parameters ride along with the request.

## Before this builds and runs

1. Confirm/adjust the SQL Server connection string in `WFE.Web/appsettings.json`
   (or swap the provider package in `WFE.Web.csproj` + `Program.cs` for Postgres/etc).
2. Restore, build, create the initial migration, and run:
   ```
   dotnet restore
   dotnet build
   dotnet ef migrations add InitialCreate --project WFE.Persistence --startup-project WFE.Web
   dotnet ef database update --project WFE.Persistence --startup-project WFE.Web
   dotnet run --project WFE.Web
   ```

## Important caveat

This was written and reviewed without a .NET SDK available in the authoring environment -
treat it as a solid first-compile attempt, not a verified-green build. Send back whatever
`dotnet build` reports and I'll fix it immediately.

## Next steps (planned)

Built-in actions/conditions from the sample XMLs, then Phase 2 (loops).
