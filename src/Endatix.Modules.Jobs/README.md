# Endatix.Modules.Jobs

Durable background job queue for long-running tenant operations.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Modules.Jobs
```

## Recommended Usage:

For running and hosting the Endatix Platform, **Endatix.Api.Host** is the recommended main package as it simplifies the installation and setup process.

```bash
dotnet add package Endatix.Api.Host
```

The module is registered automatically by `EndatixBuilder.UseDefaults()` — no host code change is needed.

## What it is

Two stores in one `jobs` schema, with one job each:

- The `BackgroundJobs` table is the **record** of every job: tenant, status, progress, attempts,
  error, trace and retention. It is what `GET jobs/{jobId}` reads, and the only place a job's
  state lives.
- [Quartz.NET](https://www.quartz-scheduler.net/) 4.2.2, clustered through the same database
  (tables `jobs.qrtz_*`), is the **queue**: it decides when and on which node a job runs, retries
  it on its trigger's policy, and re-runs the work of a node that stopped.

Every job has exactly one row and one Quartz trigger, keyed by the job's id; the trigger carries
nothing but that id.

> [!NOTE]
> **In this release per-job-type concurrency caps are configured but not yet enforced**: a node
> runs any job type it has a handler for on any free thread.

## Module layout

| Namespace | Contents |
|-----------|----------|
| `Endatix.Core.Abstractions.BackgroundJobs` | `IBackgroundJobQueue`, `IBackgroundJobHandler`, `BackgroundJobHandler<TPayload>`, `IBackgroundJobPayload`, `BackgroundJobRequest`, `BackgroundJobPayloadSerializer`, `JobStatus` — referenced by anything that enqueues or handles, and free of any Quartz type |
| `Endatix.Modules.Jobs.Domain` | `BackgroundJob` entity and its state machine |
| `Endatix.Modules.Jobs.Persistence` | `jobs`-schema contexts, EF configuration, migrations (including Quartz's tables) |
| `Endatix.Modules.Jobs.Features` | `BackgroundJobQueue` |
| `Endatix.Modules.Jobs.Runtime` | `BackgroundJobsOptions`, the Quartz registration, the handler registry, the job wrapper, the job state repository and `IJobMetrics` |

The abstractions live in `Endatix.Core` rather than here so that assemblies which cannot
reference this module — `Endatix.Infrastructure`, most notably — can still enqueue and handle.

## Schema

Database schema: `jobs`

| Table | Purpose |
|-------|---------|
| `BackgroundJobs` | One row per unit of work: type, payload, tenant, status, progress, retry state |
| `qrtz_*` | Quartz.NET's clustered job store: durable jobs, triggers, fired triggers, node check-ins, locks |

The schema carries its own `__EFMigrationsHistory`, so job migrations advance independently
of app-schema migrations. Quartz's tables are created by the `jobs` migrations only, from the
PostgreSQL script embedded in the referenced Quartz.NET version, so a fresh database always gets
the schema that version expects. Quartz validates them at startup (`SchemaProvisioning.Validate`)
and never creates them, so a node whose tables are missing or outdated fails to start. A Quartz
upgrade that changes its schema also ships a `jobs` migration, for databases created before it.
That migration must be idempotent (`ADD COLUMN IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`, and
so on): a database created after the upgrade already has the new schema from
`InitialBackgroundJobs` when it runs.

### Status

`Pending` → `Processing` → `Completed` | `Failed` | `DeadLettered` | `Retrying` | `Canceled`,
and `Retrying` → `Processing`. A job cancelled before it is claimed never runs.

| Status | Meaning | Terminal |
|--------|---------|----------|
| `Pending` | Enqueued, never started | no |
| `Processing` | Claimed by the job wrapper on the node Quartz fired it on | no |
| `Retrying` | An attempt failed retryably; waiting for its next attempt | no |
| `Completed` | Success | yes |
| `Failed` | Deterministic failure — retrying cannot help | yes |
| `DeadLettered` | Retryable failure that exhausted its attempt budget | yes |
| `Canceled` | Cancelled by a user | yes |

`Failed` and `DeadLettered` are separate because one status cannot express both "do not
retry this" and "retried and gave up", and operators need to tell them apart.

## Enqueueing

Feature code builds a request from a typed payload:

```csharp
public sealed record SubmissionExportPayload(long FormId, long ExportFormatId) : IBackgroundJobPayload
{
    // Persisted on the row and in Quartz's job keys: never change it.
    public static string JobType => "SubmissionExport";
}

var jobId = await backgroundJobQueue.EnqueueAsync(
    BackgroundJobRequest.Create(new SubmissionExportPayload(formId, formatId), tenantId, userId),
    cancellationToken);
```

Use `EnqueueManyAsync` for fan-out — one job per webhook endpoint, say. The batch commits in
a single transaction, so a partial fan-out cannot deliver to some destinations and silently
drop the rest.

> [!IMPORTANT]
> Enqueueing is **not** transactionally joined to app-schema writes: jobs live on their own
> `DbContext`, which cannot enlist in an `AppDbContext` transaction. To commit a domain change
> and a job together, raise a domain event and enqueue from the outbox — the outbox already
> guarantees the event survives the business transaction.

## Writing a handler

Derive from `BackgroundJobHandler<TPayload>` and register it with
`services.AddBackgroundJobHandler<THandler, TPayload>()`, which keys it by job type so a job run
builds only its own handler. The job type comes from the payload; handlers may live in any
assembly. Two handlers declaring the same job type fail startup.

```csharp
internal sealed class SubmissionExportJobHandler(..., ILogger<SubmissionExportJobHandler> logger)
    : BackgroundJobHandler<SubmissionExportPayload>(logger)
{
    protected override Task<Result> ExecuteAsync(
        BackgroundJobContext job, SubmissionExportPayload payload, CancellationToken cancellationToken) => ...;
}
```

Payload rules: the job type is a string literal declared once, on the payload; a payload
written by one release must deserialize in the next (adding an optional property is fine;
renaming, removing or retyping one is a new job type); keep payloads thin — ids plus the
minimum non-personal data. Input that cannot be read ends the job `Failed` without a retry, and the
base class logs why through the logger it is given.

Four obligations, each invisible until it hurts in production:

1. **Return a failure `Result` for deterministic errors; throw only for transient ones.**
   This is the only retry signal there is. Throwing on a permanent error re-runs expensive
   work until the attempt budget is gone.
2. **Scope every query to the job's `TenantId` explicitly.** Outside a request the ambient
   tenant filter is permissive, not restrictive — a handler that queries as if it were in a
   request reads every tenant's data.
3. **Honour the `CancellationToken`**, or the job cannot be cancelled or time-limited.
4. **Do not hold one `DbContext` for the length of the job.** Open a scope per chunk via
   `IServiceScopeFactory`; a change tracker held for minutes accumulates every row streamed
   through it.

Handlers never reference a Quartz type.

## Runtime

Every host that registers the module builds a Quartz scheduler named `endatix-jobs` against
the shared store. `Endatix:BackgroundJobs:RunInProcess` decides what it does with it:

| `RunInProcess` | The host |
|----------------|----------|
| `true` (default) | Enqueues and **executes** jobs |
| `false` | Enqueues only: its scheduler has no threads and is never started |

That is what allows API and worker roles to be deployed separately from the same image.

The Quartz thread pool holds every registered job type's full concurrency cap at once
(`JobTypes:{JobType}:MaxConcurrency`, default `1`), so it is sized as the sum of the caps.
There is no global concurrency setting.

Each job type the host has a handler for gets one durable Quartz job, which requests recovery,
so a job cut off by a stopped or crashed node runs again on another. Every node sharing the
store must run the same Quartz version, and nodes' clocks must agree within about a second.

### Execution

`BackgroundJobExecution` is the only Quartz job class. It only orchestrates each firing, through
one class per step:

1. **Claims** the row (`JobAttemptClaimer`) with a compare-and-swap from `Pending`/`Retrying` to
   `Processing` that increments `AttemptCount`. When Quartz reports a recovered firing, or the
   firing was scheduled to take over an unrecorded attempt, it re-claims the row from `Processing`,
   fenced on the attempt it read, and dead-letters a job that has no attempt left instead. A claim
   that changes nothing ends the firing.
2. **Runs the handler** (`JobHandlerRunner`) in its own DI scope, under an `Endatix.Jobs` activity
   whose parent is the trace captured at enqueue, with one token linked from the runtime ceiling
   (`MaxRuntimeMinutes`), the cancellation watcher and the host's shutdown.
3. **Records the outcome** (`JobOutcomeRecorder`) with one write fenced on the claimed attempt,
   tried again a few times if it throws, and records the lifecycle metrics only when it lands.
   A firing whose outcome still cannot be written is re-fired shortly by `UnrecordedJobRefire`:

| Handler | Row | Quartz |
|---------|-----|--------|
| returns success | `Completed` | done |
| returns a failure `Result` | `Failed`, with its message | done, no retry |
| throws, attempts left | `Retrying` | the trigger's retry policy schedules the next attempt |
| throws, attempts spent | `DeadLettered`, with a safe message | done |
| row set to `Canceled` meanwhile | stays `Canceled` | done |
| host stopped waiting for it | stays `Processing`, nothing written | re-run on the next node to check in |
| its node stopped during the last attempt | `DeadLettered` when recovered, the handler not run again | done |
| row taken over by another attempt meanwhile | left to that attempt | done; the handler's token is cancelled |
| its outcome cannot be written (tried 4 times) | stays `Processing` | fires again 5 s later and re-claims the row |

The row's `AttemptCount`, not Quartz's retry counter, decides dead-lettering: a run recovered after
a crash consumes an attempt Quartz never counts. Exception text never reaches `ErrorMessage`.
After Quartz schedules a retry, `NextAttemptAt` mirrors the trigger's next fire time.

**Shutdown.** A stopping host gives running jobs `ShutdownWaitSeconds` to finish and record their
outcome. Jobs still running then are left as they are: their handlers are told to stop, nothing is
recorded, and Quartz re-runs them on the next node to check in.

**Cancellation.** Setting a row to `Canceled` reaches a running handler through the wrapper's
watcher, which re-reads the status every `CancellationPollSeconds`, on whichever node runs the job.

`IJobMetrics` is public so a host can replace it. The default records on the `Endatix.Jobs`
meter (`JobsModule.MeterName`), and `EndatixTelemetryBuilder` subscribes the host's metrics
pipeline to it.

| Instrument | Type | Unit | Tags |
|------------|------|------|------|
| `endatix.jobs.events` | counter | `{event}` | `endatix.job.type`, `endatix.job.event` |
| `endatix.jobs.duration` | histogram | `s` | `endatix.job.type`, `endatix.job.outcome` |

## Configuration

Under `Endatix:BackgroundJobs`, with per-job-type overrides under `JobTypes:{JobType}`:

| Key | Default | Purpose |
|-----|---------|---------|
| `RunInProcess` | `true` | Whether this host executes jobs |
| `IdleWaitTimeSeconds` | `2` | How long an idle node waits before looking for jobs another node scheduled |
| `CancellationPollSeconds` | `10` | How often a running job notices it was cancelled |
| `ShutdownWaitSeconds` | `30` | How long a stopping host waits for running jobs before leaving them for recovery; never longer than the host's `HostOptions.ShutdownTimeout` (30 s by default) |
| `MaxRuntimeMinutes` | `60` | Ceiling on one attempt (per type) |
| `MaxAttempts` | `3` | Attempts before `DeadLettered` (per type) |
| `BackoffBaseSeconds` / `BackoffCapSeconds` | `30` / `900` | Retry backoff (per type) |
| `RetentionDays` | `7` | How long a finished job's row is kept (per type) |
| `JobTypes:{JobType}:MaxConcurrency` | `1` | Jobs of this type one node runs at once; `0` declines the type |
| `Clustering:CheckinIntervalSeconds` / `CheckinMisfireThresholdSeconds` | `7.5` / `7.5` | A node silent for their sum is presumed dead |
| `Clustering:InstanceId` | generated | This node's identity; set only to a value no other running node uses |

## Registration

Registered via `EndatixBuilder.UseDefaults()` → `UseModule(JobsModule.Instance)`. Capabilities:
`IEndatixModule`, `IHasFeatureFlag`, `IHasDbMigrations`, `IHasFastEndpoints`. Do **not** also
`Api.ScanAssemblies` this assembly (bypasses the flag).

Gated by `Endatix:FeatureFlags:JobsModule`, **off by default**. The module owns a DbContext
and its own migrations, so registering it where nothing enqueues would create a schema no code
writes to.

## Migrations

**PostgreSQL is currently the only supported provider.** With the flag off — the default —
nothing is registered and other providers are unaffected. With the flag on and a different
provider configured, the host fails at startup naming the constraint.

Persistence is **provider-split**: `JobsPostgreSqlDbContext` derives from `JobsDbContextBase`
and owns its migrations and model snapshot under `Persistence/Migrations/PostgreSql`. EF Core
keeps one model snapshot per context type, so adding a provider means adding a derived context,
its own design-time factory, its own `Config/<Provider>/` configuration and its own migrations
folder — never reusing an existing one.

The migrations were reset once, in the change that moved scheduling onto Quartz, to a single
`InitialBackgroundJobs` migration. v0.7.6 and the canaries before this change shipped the earlier
ones (`AddBackgroundJobs`, `RequireRealTenantOnBackgroundJobs`), and EF cannot upgrade a database
from them: it would try to create `jobs."BackgroundJobs"` again and fail. Any database where
`Endatix:FeatureFlags:JobsModule` was turned on under those releases needs `DROP SCHEMA jobs CASCADE`
once before upgrading. That deletes every job row and the schema's migration history. Those rows
never ran, because no release executed jobs, and they cannot be carried over, because each job now
needs a Quartz trigger as well as its row. Back up `jobs."BackgroundJobs"` first if you need a
record of them. From here on `jobs` migrations are append-only.

Run the commands from the repository root, with `Endatix.WebHost` as the startup project.

```bash
dotnet ef migrations add <Name> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Jobs \
  --context JobsPostgreSqlDbContext \
  --output-dir Persistence/Migrations/PostgreSql
```

Migrations apply automatically at startup when `Endatix:Data:EnableAutoMigrations` is enabled.

## Third-party licence

Quartz.NET is Apache-2.0 licensed. Its licence text ships in `THIRD-PARTY-NOTICES`, both in
this package and in the API image (`/app/THIRD-PARTY-NOTICES`).
