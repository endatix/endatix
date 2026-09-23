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

The `BackgroundJobs` table **is** the queue. Enqueueing is a single insert. Retry state —
attempt count, next attempt time, and the terminal statuses — lives on the job row, so no
second system can disagree with it about what a job is doing.

> [!IMPORTANT]
> This package currently provides **persistence and enqueueing**. It also contains an internal
> job state repository, which nothing uses yet, and the public `IJobDispatchStrategy` and
> `IJobMetrics` extension points, which enqueueing calls only when a host registers them. No
> runner or sweeper is hosted, so enqueued jobs stay `Pending`. The
> state machine, the handler contract, and the schema below describe the full design and are
> what execution will be built against. Cancelling a job and reporting a running
> job's progress through conditional writes is tracked in
> [endatix/endatix#1061](https://github.com/endatix/endatix/issues/1061).

Work that outlives a request belongs here: exporting a large submission set, delivering a
webhook to one endpoint, backfilling or purging tenant data.

## Module layout

| Namespace | Contents |
|-----------|----------|
| `Endatix.Core.Abstractions.BackgroundJobs` | `IBackgroundJobQueue`, `IBackgroundJobHandler`, `BackgroundJobRequest`, `JobStatus` — referenced by anything that enqueues or handles |
| `Endatix.Modules.Jobs.Domain` | `BackgroundJob` entity and its state machine |
| `Endatix.Modules.Jobs.Persistence` | `jobs`-schema contexts, EF configuration, migrations |
| `Endatix.Modules.Jobs.Features` | `BackgroundJobQueue` |
| `Endatix.Modules.Jobs.Runtime` | `BackgroundJobsOptions`, the internal job state repository, and the `IJobDispatchStrategy` and `IJobMetrics` seams with their defaults — see [Runtime](#runtime) |

The abstractions live in `Endatix.Core` rather than here so that assemblies which cannot
reference this module — `Endatix.Infrastructure`, most notably — can still enqueue.

## Schema

Database schema: `jobs`

| Table | Purpose |
|-------|---------|
| `BackgroundJobs` | One row per unit of work: type, payload, tenant, status, progress, retry state |

The schema carries its own `__EFMigrationsHistory`, so job migrations advance independently
of app-schema migrations.

### Status

`Pending` → `Processing` → `Completed` | `Failed` | `DeadLettered` | `Retrying` | `Canceled`,
and `Retrying` → `Processing`. A job cancelled before it is claimed never runs.

| Status | Meaning | Terminal |
|--------|---------|----------|
| `Pending` | Enqueued, never started | no |
| `Processing` | Claimed by a runner; a heartbeat is expected | no |
| `Retrying` | An attempt failed retryably; waiting for its next attempt time | no |
| `Completed` | Success | yes |
| `Failed` | Deterministic failure — retrying cannot help | yes |
| `DeadLettered` | Retryable failure that exhausted its attempt budget | yes |
| `Canceled` | Cancelled by a user or by host shutdown | yes |

`Failed` and `DeadLettered` are separate because one status cannot express both "do not
retry this" and "retried and gave up", and operators need to tell them apart.

`Pending` and `Retrying` both mean *eligible to run at `NextAttemptAt`*, so a single query
dispatches either.

## Enqueueing

```csharp
var jobId = await backgroundJobQueue.EnqueueAsync(
    new BackgroundJobRequest("SubmissionExport", payloadJson, tenantId, userId),
    cancellationToken);
```

Use `EnqueueManyAsync` for fan-out — one job per webhook endpoint, say. The batch commits in
a single transaction, so a partial fan-out cannot deliver to some destinations and silently
drop the rest.

> [!IMPORTANT]
> Enqueueing is **not** transactionally joined to app-schema writes: jobs live on their own
> `DbContext`, which cannot enlist in an `AppDbContext` transaction. To commit a domain change
> and a job together, raise a domain event and enqueue from the outbox handler — the outbox
> already guarantees the event survives the business transaction.

## Writing a handler

> [!NOTE]
> Nothing executes handlers yet (see above), so an implementation registered today will not be
> invoked. The contract is documented here because it is what handlers will be held to, and
> because the obligations below are far cheaper to honour while a handler is being written than
> to retrofit.

Implement `IBackgroundJobHandler` and register it in DI. Routing is by `JobType`, and handlers
may live in any assembly.

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

## Runtime

`IJobDispatchStrategy` and `IJobMetrics` are public so that a host can replace them. Enqueueing
uses them in this order:

1. Commit the rows.
2. If a strategy is registered, `TryOffer` each job id in request order. This is a latency
   optimisation only: a refused or failed offer leaves the job for the sweeper.
3. If metrics are registered, record `Enqueued` for each job, then `OfferRejected` for each
   refusal.

`false` from `TryOffer` means the strategy refused the job for back-pressure, and is counted as
`OfferRejected`. A throw is a bug in the strategy: it is logged at Warning and
swallowed, and is not counted as a refusal. Neither reaches the caller, whose rows are already committed.

The module registers neither seam yet, and no runner is hosted, so rows stay `Pending` even if
they are offered into a channel.

The default strategy queues at most `MaxConcurrency × 25` job ids, a multiplier fixed on purpose:
anything beyond stays in the database for the next sweep.

The default metrics record on the `Endatix.Jobs` meter (`JobsModule.MeterName`), and
`EndatixTelemetryBuilder` subscribes the host's metrics pipeline to it.

| Instrument | Type | Unit | Tags |
|------------|------|------|------|
| `endatix.jobs.events` | counter | `{event}` | `endatix.job.type`, `endatix.job.event` |
| `endatix.jobs.queue.depth` | gauge | `{job}` | — |
| `endatix.jobs.backlog.count` | gauge | `{job}` | `endatix.job.type` |
| `endatix.jobs.backlog.oldest_wait` | gauge | `s` | `endatix.job.type` |
| `endatix.jobs.duration` | histogram | `s` | `endatix.job.type`, `endatix.job.outcome` |

Every instance reports the same cluster-wide backlog, so aggregate the two backlog gauges across
instances with max, not sum.

## Registration

Registered via `EndatixBuilder.UseDefaults()` → `UseModule(JobsModule.Instance)`. The module
implements `IEndatixModule` and `IHasDbMigrations`.

Gated by `Endatix:FeatureFlags:JobsModule`, **off by default**. The module owns a DbContext
and its own migrations, so registering it where nothing enqueues would create a schema no code
writes to.

> [!IMPORTANT]
> The flag has to be turned on in the same release that moves webhook delivery onto the queue.
> Left off, upgrading hosts lose fan-out with no error to explain it.

Whether a given process *executes* jobs is a separate, configuration-level question — which is
what allows API and worker roles to be deployed separately from the same image.

## Migrations

**PostgreSQL is currently the only supported provider.** With the flag off — the default —
nothing is registered and other providers are unaffected. With the flag on and a different
provider configured, the host fails at startup naming the constraint, rather than deferring the
failure to whichever call site enqueues first.

A second provider therefore has to exist by the time a feature depends on the queue.

Persistence is nonetheless **provider-split**: `JobsPostgreSqlDbContext` derives from
`JobsDbContextBase` and owns its migrations and model snapshot under
`Persistence/Migrations/PostgreSql`.

This is not stylistic. EF Core keeps **one model snapshot per context type**, so generating
two providers' migrations against a single shared context makes the second generation
overwrite the first's snapshot — after which the next migration for the first provider diffs
against the wrong model and emits nonsense. Adding a provider therefore means adding a derived
context, its own design-time factory, its own `Config/<Provider>/` entity configuration and its
own migrations folder — never reusing an existing one.

Run the commands from the repository root.

> [!NOTE]
> Always use `Endatix.WebHost` as the startup project. The design-time factories pin their own
> provider, so `ConnectionStrings:DefaultConnection_DbProvider` does not affect which
> migrations are generated — but a valid `ConnectionStrings:DefaultConnection` must be present.

### PostgreSQL

```bash
dotnet ef migrations add <Name> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Jobs \
  --context JobsPostgreSqlDbContext \
  --output-dir Persistence/Migrations/PostgreSql
```

Migrations apply automatically at startup when `Endatix:Data:EnableAutoMigrations` is enabled.
