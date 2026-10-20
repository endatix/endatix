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
  (tables `jobs.qrtz_*`), is the **queue**: it decides when and on which node a job runs, and
  re-runs the work of a node that stopped. A retry is a trigger the job wrapper stores itself.

Every job has exactly one row and one Quartz trigger, keyed by the job's id; the trigger carries
that id and, on a re-fire, what the re-fire is for. The payload and anything secret stay on the row.
One thing is in both: a re-fire that carries a failure the handler returned holds its message, the
handler's own `Result` text (never exception text), cut to 2048 characters as the row cuts it, in
the trigger's job data, where the Quartz [dashboard](#runtime) shows it.

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
of app-schema migrations. Quartz's tables are created by the `jobs` migrations only; see
[Quartz schema and upgrades](#quartz-schema-and-upgrades).

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

**Idempotent enqueue.** A request may carry a `DedupKey` naming its unit of work — for a fan-out,
`{outboxMessageId}:{subscriber}`. It is unique per tenant and job type (a filtered unique index,
`IX_BackgroundJobs_DedupKey`), and enqueueing a key that already exists returns the existing job's
id without a second row or trigger, even when two enqueues race. Requests without a key never
collide. It names the work, never the attempt: no timestamps or counters in it. On SQL Server a key
that ends with a space (U+0020) is refused, because SQL Server ignores trailing spaces when it
compares; PostgreSQL compares keys as written and takes it. A blank key means no key.

Use `EnqueueManyAsync` for fan-out — one job per webhook endpoint, say. The batch commits in
a single transaction, so a partial fan-out cannot deliver to some destinations and silently
drop the rest. A batch inserts and schedules its keyed jobs ordered by tenant, job type and key, so
for keyed jobs request order is kept neither for their ids nor for the order jobs due at once start
in; the ids `EnqueueManyAsync` returns still match the requests. On PostgreSQL the order makes two batches that share keys lock them in the same order, so one waits for
the other instead of deadlocking. On SQL Server the query plan picks the order rows reach an index, so
two such batches can still deadlock. An enqueue that loses a deadlock or a serialization failure
anywhere in its attempt (inserting the rows, storing their triggers, committing) rolls back, reads the
keys again and retries once, and the caller gets the existing jobs; a second one in a row is thrown.

> [!IMPORTANT]
> Enqueueing is **not** transactionally joined to app-schema writes: jobs live on their own
> `DbContext`, which cannot enlist in an `AppDbContext` transaction. To commit a domain change
> and a job together, raise a domain event and enqueue from the outbox — the outbox already
> guarantees the event survives the business transaction.

## Writing a handler

Derive from `BackgroundJobHandler<TPayload>` and register it with
`services.AddBackgroundJobHandler<THandler, TPayload>()`, which keys it by job type so a job run
builds only its own handler. The job type comes from the payload; handlers may live in any
assembly. A job type that ends with a space fails startup on either database: job types are code
constants, and SQL Server would hold one equal to the type without the space. Two
handlers declaring the same job type, or job types that differ only in case, fail startup:
on SQL Server the scheduler stores each job type's durable job under a key the database compares in its
default collation, which ignores case by default.

A job type that needs other settings than the global defaults declares them at the same call, so every
host runs it as tuned: `AddBackgroundJobHandler<THandler, TPayload>(new BackgroundJobTypeDefaults
{ MaxAttempts = 8, MaxConcurrency = 4 })`. A handler registered by a factory declares them with
`services.AddBackgroundJobTypeDefaults(jobType, defaults)`. A value the host sets in configuration
wins over it ([Configuration](#configuration)).

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

**Starting without a database.** A node whose database is away (unreachable, failing over, timing
out while connecting) starts without its scheduler, logs that it could not reach its database, with
the driver's failure (which never includes the password), on the first try and every 12th after, and
starts the scheduler once it can; a database that refuses, such as a login it does not accept, fails
the start. So does a PostgreSQL role that may not take Quartz's lock (it needs SELECT, UPDATE and
INSERT on `jobs.qrtz_locks`): PostgreSQL's refusal reaches the start only as the 25P02 Quartz reports
after it, so the scheduler asks for the role's privileges when it meets one, and a failed
transaction inside the lock that the role and the database do not explain is retried as a lost race. A database that cannot be opened (SQL Server 4060, PostgreSQL 3D000) is retried too, with an
error naming it ("cannot open database …: it is missing, offline or being restored, or not created
yet"): SQL Server reports a missing database exactly as it reports one that is offline or being
restored, and PostgreSQL's may still be being created. A read-only database (SQL Server 3906,
PostgreSQL 25006, or a standby) is waited out the same way, with an error saying it is read-only,
as during a failover, or set read-only on purpose. A deadlock, or a statement that times out once
the node has connected, is a lost race rather than an outage, and is retried as one, with its own
warning. An outage that fails building the scheduler, whether the start or an enqueue asked for it,
is waited out the same way. The dashboard and its HTTP API never build it: until it is built, the
dashboard lists it as a registration with no status yet, and calls for it answer not found, so a poll
during an outage neither fails nor starts a build. Quartz never hands out a scheduler whose build failed, so
the next try rebuilds it from the module's Quartz registration, with the same thread pool, batch
size, store and listeners as a first build; it is started by the waiting start, as a first build is,
and only on a node that runs jobs. A node stopping while it waits stops waiting at once. Meanwhile
the host's readiness probe reports the database as down. It only checks that it can connect,
though, so a read-only database (SQL Server `READ_ONLY`, a PostgreSQL standby) reports Ready while
the node runs no jobs; the error that the database is read-only is what says so. A rebuild replays only what was configured
inside the module's Quartz registration: anything a host registers for the scheduler next to
`AddJobsScheduler` instead, such as a keyed `IDriverDelegate`, a plugin or a history store, is
lost on a rebuild, and the rebuilt scheduler runs without it.

**Each job type is its own Quartz execution group**, capped per node at
`JobTypes:{JobType}:MaxConcurrency` (default `1`). Every group this host has no handler for is
capped at `0`, so a node never takes a job it cannot run; the job waits for a node that can. The
thread pool holds every registered job type's full cap at once, so it is sized as the sum of the
caps, and there is no global concurrency setting. Trigger acquisition reads, for each group with a
free slot on the node, at most as many of its oldest triggers as it has free slots, so a backlog
of one job type never holds up another. One acquisition takes up to a trigger per free thread
(the scheduler's batch size is the pool size) and never more of a job type than its free slots.
The triggers Quartz creates to recover a dead node's jobs carry no group; they are given their job
type's when read for acquisition, so recovered jobs count against the cap like any other. If a
node ever fires a job type it has no handler for, the wrapper declines it without touching the row
and offers the trigger again 30 seconds later.

**Backlog warning.** A job that waited past `MisfireThresholdSeconds` for a slot increments
`endatix.jobs.misfired` (tag `endatix.job.type`), and again at every threshold while it waits. The warning
`Background job {JobId} of type {JobType} waited past the misfire threshold; {Misfires} triggers of
this type misfired since the last warning.` is logged at most once per job type per threshold. A job type no running node can handle shows up here:
deploy the module that handles it.

**Dashboard.** With `Dashboard:Enabled`, the Quartz dashboard is served at `/quartz` and its HTTP
API at `/quartz-api`, both behind the `PlatformAdmin` policy and read-only unless
`Dashboard:AllowWrites` is set. It is an operator tool: a caller that may write can schedule jobs
on the host, and it shows every tenant's triggers. Even with writes on, only Endatix's own job
class may be named.

Each job type the host has a handler for gets one durable Quartz job, which requests recovery,
so a job cut off by a stopped or crashed node runs again on another. A host enqueueing a job type
checks once per process that its durable job is in the store, and stores it again if it has since
been deleted. Every node sharing the store must run the same Quartz version, and nodes' clocks
must agree within about a second.

**Trigger lock.** Quartz serializes trigger state across the cluster through one row lock
(`TRIGGER_ACCESS`). Each acquisition takes it once for its whole batch, and so does firing that
batch; each firing takes it once more as it completes, and an idle node takes it once per idle wait.
A failed attempt with attempts left takes it once more, to store its retry's trigger: that replaces
the trigger that fired, and Quartz takes the lock for every replacement, `LockOnInsert` or not.
Whether enqueueing takes it (`LockOnInsert`) depends on the provider:

- **PostgreSQL** — off by default. A trigger is invisible to every node's acquisition until its
  transaction commits, so enqueueing has nothing to take turns over.
- **SQL Server** — on by default. Unless the database reads committed snapshots, an acquisition
  reading the triggers would wait on every open enqueue's uncommitted rows. So every enqueue takes
  the cluster-wide lock and holds it until its transaction commits: enqueues and acquisitions on
  every node take turns, and a webhook fan-out, which stores one trigger per endpoint in one
  transaction, holds it longest. [Trigger lock on SQL Server](#trigger-lock-on-sql-server) says what
  that costs and when to turn it off.

### Trigger lock on SQL Server

Measured on SQL Server 2025 (Developer edition, in a Linux container under emulation on one laptop,
so the absolute numbers are indicative; compare the rows with each other):

| `READ_COMMITTED_SNAPSHOT` | `LockOnInsert` | Another enqueue / its job fires, while a 2 s enqueue transaction is open | Deadlocks (3 × 200 concurrent enqueues, 2 acquiring nodes) | Schedule / fire, triggers per second |
|---|---|---|---|---|
| off | `true` (default) | waits 2.0 s / fires after 2.1 s | 0 | 197–216 / 141–151 |
| off | `false` (refused at startup) | 0.02 s / fires after 2.1 s | 0 | 224–227 / 124–145 |
| on | `true` | waits 2.0 s / fires after 2.1 s | 0 | 208–209 / 148–149 |
| on | `false` | 0.02 s / fires after 0.03–0.13 s (once 2.1 s) | 0 | 221–231 / 144–149 |

Enqueue throughput, same machine, 8 concurrent enqueuers on one node while another node executes
and acquires (two runs each; PostgreSQL with its default for comparison, running natively rather
than under emulation, so its row is not a like-for-like comparison):

| Database | `READ_COMMITTED_SNAPSHOT` | `LockOnInsert` | Single enqueues | Fan-out batches of 20 triggers |
|---|---|---|---|---|
| SQL Server | off | `true` (default) | 160–221/s, 36–50 ms each | 15–16/s (≈ 300 jobs/s), 510–540 ms each |
| SQL Server | on | `true` | 178–216/s, 37–45 ms each | 15/s (≈ 300 jobs/s), 520–530 ms each |
| SQL Server | on | `false` | 441–662/s, 12–18 ms each | 52–57/s (≈ 1,050–1,140 jobs/s), 140–150 ms each |
| PostgreSQL | — | `false` (default) | 1,450–1,530/s, 5 ms each | 114–125/s (≈ 2,300–2,500 jobs/s), 64–70 ms each |

With one enqueuer at a time the lock costs next to nothing (the throughput column of the first
table). With many at once they take turns for the lock, cluster-wide, so SQL Server enqueues about a
third as fast as without it, and a fan-out batch holds everyone else up for as long as it takes to
store all its triggers. It causes no deadlocks either way: `LockOnInsert = false` is a **latency and
throughput option, not a deadlock fix**. Keep the default unless enqueues queue behind each other,
or jobs start late, at your volume: many concurrent enqueuers, large webhook fan-outs, or callers that
enqueue inside a long unit of work. Without
`READ_COMMITTED_SNAPSHOT`, turning the lock off only moves the wait from enqueueing to acquisition,
which is why a node refuses that combination at startup, naming both settings. The node checks the
database once, when its scheduler starts or at its first enqueue, and keeps the answer: a refused node
fails every enqueue without asking again, and needs a restart once a DBA has turned
`READ_COMMITTED_SNAPSHOT` on, just as a database that turns snapshots off later is noticed only when
the nodes restart. The switch below restarts the nodes after the DBA's step, so it never meets either.

Switching is an operations change, not a release:

1. A DBA turns on `READ_COMMITTED_SNAPSHOT` for the database. Azure SQL Database has it on by
   default, so there this step is already done. It needs a moment with no other
   connection to it: `ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE`
   ends the others. Check it with
   `SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()`.
2. The operator sets `Endatix:BackgroundJobs:LockOnInsert` to `false` on every node.
3. The nodes restart, after step 1: a node checks the database once, at its start. Until every node
   has restarted, the ones still taking the lock take turns with each other, which is harmless.

To go back, remove the setting (or set it to `true`) and restart the nodes; the database setting can
stay on.

### Retention

`JobRetentionJob` runs on the `Retention:Cron` schedule, on one node at a time, in an execution
group of its own with one thread beyond the job types' caps. Each run deletes finished rows whose
`ExpiresAt` has passed, `BatchSize` at a time, for at most `MaxBatchesPerRun` batches. It never
deletes a row that is not finished. Every terminal write sets `ExpiresAt` to its time plus the job
type's `RetentionDays` when the row has none. Quartz deletes its own finished triggers.

### Execution

`BackgroundJobExecution` is the only Quartz job class. It only orchestrates each firing, through
one class per step:

1. **Admits the firing** (`JobFiringAdmission`). A firing that carries no job id, as one an
   operator fires by hand from the dashboard does, is ignored. A job type this host has no handler
   for is declined without touching the row, and its trigger is offered again 30 seconds later.
2. **Claims** the row (`JobAttemptClaimer`) with a compare-and-swap from `Pending`/`Retrying` to
   `Processing` that increments `AttemptCount`. When Quartz reports a recovered firing, or the
   firing was scheduled to take over an attempt that left its row unsettled, it re-claims the row
   from `Processing`, fenced on the attempt it read, and dead-letters a job that has no attempt left
   instead. A claim that changes nothing ends the firing. The firing's trigger is the job's only
   one, so a claim that throws re-fires the job, to be claimed again, rather than ending it. A re-fire
   that carries the outcome of an attempt that finished is not claimed at all: it writes that outcome
   (step 4).
3. **Runs the handler** (`JobHandlerRunner`) in its own DI scope, under an `Endatix.Jobs` activity
   whose parent is the trace captured at enqueue, with one token linked from the runtime ceiling
   (`MaxRuntimeMinutes`), the cancellation watcher and the host's shutdown.
4. **Records the outcome** (`JobOutcomeRecorder`) with one write fenced on the claimed attempt,
   tried again a few times if it throws, and records the lifecycle metrics only when it lands.
   A firing whose outcome still cannot be written is re-fired shortly by `UnrecordedJobRefire`. When
   the handler decided the outcome — it returned success or a failure `Result` — the re-fire carries
   it in the trigger's `outcome.v1` data and writes it, fenced on that attempt as the first firing's
   write was, rather than re-claiming the row: re-claiming would run finished work again, or
   dead-letter a last attempt that succeeded. A throw is not carried; the re-claim judges it against
   the attempt budget as it is then. Such a re-fire is still marked to re-claim, so a node of an
   earlier version, which does not read the outcome, takes the job over as before, and a re-fire an
   earlier version stored carries no outcome and is re-claimed, as is one whose outcome cannot be
   read, with a warning saying why. While the database is away, the outcome is carried for as long
   as it takes. Away means the write timed out, lost a deadlock, met a failover, a lost connection,
   a server shutting down, a read-only database (SQL Server 3906, PostgreSQL 25006), or an Azure SQL
   database that is busy or paused (`DatabaseFailures` lists
   the PostgreSQL SQLSTATEs and SQL Server error numbers), the driver marked the failure transient,
   this host is stopping, or the job's row cannot be read either. A read-only database is an outage
   because the error cannot tell a failover's secondary from a database set read-only on purpose, and
   dropping an outcome during a failover would run finished work again; one read-only on purpose
   keeps the outcome retrying, and a warning, at most once a minute, says the database is read-only.
   A write the database refuses while
   up, because of a constraint or a trigger, say, does not clear by trying again: such refusals are
   counted on the outcome, and after 3 the outcome is dropped, with an error logged, and the row is
   re-claimed. A node without the job type's handler declines such a re-fire, and the outcome stays
   on its trigger; a firing that writes it because the scheduler keeps refusing its trigger counts
   refusals the same way, carries the count on the trigger it stores once the scheduler takes it,
   and dead-letters the job, fenced on the outcome's attempt, once the 3 are spent.
   The wrapper never throws to Quartz. Every retry gets a trigger of the job's own, stored for the
   next attempt's time before the row says `Retrying`:

| Handler | Row | Quartz |
|---------|-----|--------|
| returns success | `Completed` | done |
| returns a failure `Result` | `Failed`, with its message | done, no retry |
| throws, attempts left | `Retrying` | a trigger of the job's own runs the next attempt after the backoff |
| as above, but that trigger cannot be stored | stays `Processing`, nothing written | fires again 5 s later and re-claims the row |
| as above, and the scheduler rejects the re-fire 3 times | `DeadLettered`, with a safe message | done |
| throws, attempts spent | `DeadLettered`, with a safe message | done |
| row set to `Canceled` meanwhile | stays `Canceled` | done |
| host stopped waiting for it | stays `Processing`, nothing written | re-run on the next node to check in |
| its node stopped during the last attempt | `DeadLettered` when recovered, the handler not run again | done |
| row taken over by another attempt meanwhile | left to that attempt | done; the handler's token is cancelled |
| returns success or a failure `Result`, but that outcome cannot be written (tried 4 times) | stays `Processing` | fires again 5 s later carrying the outcome, writes it if the row is still at that attempt, and is not run again; while the database is away, fires again |
| as above, but the scheduler rejects that re-fire 3 times | the carried outcome, written by the firing itself if the row is still at that attempt; `DeadLettered` if the database refuses that write while up | done |
| as above, and the row was cancelled, taken over or recorded meanwhile | left as it is | done |
| returns success or a failure `Result`, and the database, while up, refuses the carried write (a constraint or a trigger, say) on 3 re-fires | stays `Processing`; the outcome is dropped, with an error logged | fires again 5 s later and re-claims the row, as a throw's re-fire does |
| returns success or a failure `Result`, but that outcome cannot be written while the host is stopping | stays `Processing`; the outcome is not carried, since nothing is stored once the scheduler has let go | re-run on the next node to check in, or `DeadLettered` there with the stopped-on-last-attempt message if that was its last attempt |
| throws, and its outcome cannot be written (tried 4 times) | stays `Processing` | fires again 5 s later and re-claims the row |
| not run: its claim threw | as it was | fires again 5 s later and claims the row |

A re-fire is a trigger too, and storing it can fail. While the scheduler is stopping it refuses
every new trigger but still completes the firings it waits for, which would delete the job's last
trigger; the firing is held instead until the scheduler lets go of it, and the next node to check
in recovers it. Once the scheduler has let go, its job store is shut down and refuses every
completion, so a firing that ends then leaves its trigger in place and stores nothing. A re-fire
that fails on a node that keeps running is logged at `Error`, then at most once a minute, and tried
again, after a wait that doubles from 0.5 s up to 5 s; the firing does not complete before then, so
the row is never left `Processing` with no trigger. Each try sets the trigger 5 s after that try,
so one stored late is not a misfire. A held firing occupies a worker thread of its job type's cap
until its trigger is stored.

A failure the database caused is tried until the trigger is stored. Any other refusal is tried 3
times, with the job type's durable job stored again first if it has gone missing, and then the
firing settles the row itself, if it is still as the firing left it, and completes, so a rejection
that will not clear does not hold a worker for good. A re-fire that carries an outcome writes that
outcome, fenced on its attempt, and counts the attempt's end once; if the database refuses that write
while up, or the re-fire carries no outcome, the firing dead-letters the row.
Should that write fail too, the firing goes on trying as before.

The row's `AttemptCount`, against the job type's `MaxAttempts` on the node that runs the attempt,
decides dead-lettering, and the retry's backoff is that node's too. Nothing about retries is fixed
at enqueue, so a change to either applies to jobs already waiting. Exception text never reaches
`ErrorMessage`. `NextAttemptAt` is the time the retry's trigger was stored for.

**Triggers from earlier versions** carry a Quartz retry policy, because the wrapper used to throw
for Quartz to retry. It no longer throws, so such a trigger ends its firing like any other, and the
retry runs on a trigger of the job's own, which carries none. Should a firing on such a trigger throw
anyway until its policy is spent while the row is unfinished, `JobTriggerListener` re-fires the job
5 s later to take it over, logs `The scheduler ran out of retries for background job …` and counts
`endatix.jobs.retries_exhausted_mismatch`. Every trigger of a job carries the job's id as its key,
so the take-over replaces any trigger the job has and never adds a second.

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
| `endatix.jobs.retries_exhausted_mismatch` | counter | `{job}` | `endatix.job.type` |
| `endatix.jobs.misfired` | counter | `{trigger}` | `endatix.job.type` |

The outbox relay's fan-out into jobs reports on a meter of its own, `Endatix.Outbox`; its instrument is listed
in the Background Processing docs (`docs/endatix-docs/docs/configuration/background-processing.mdx`, Metrics).

## Configuration

Under `Endatix:BackgroundJobs`, with per-job-type settings under `JobTypes:{JobType}`.

**Anything set in configuration wins over code.** Each per-type setting of a job type is looked up on
its own, and the first of these that has a value is used:

1. the job type's own key, `JobTypes:{JobType}:{Setting}`, in appsettings or an environment variable;
2. the global key, `{Setting}`, if the host set it;
3. the job type's default, declared in code when its handler was registered (the table below);
4. the global default (the second table below).

`MaxConcurrency` has no global key, so it takes the job type's own key, then the job type's default,
then `1`.

For example, a host whose `appsettings.json` has

```json
"Endatix": {
  "BackgroundJobs": {
    "RetentionDays": 30,
    "JobTypes": { "WebHookDelivery": { "MaxAttempts": 10 } }
  }
}
```

keeps every finished job's row for 30 days, webhook deliveries included, and tries a webhook delivery
10 times; the other webhook settings stay as declared in code. The same with environment variables:

```bash
Endatix__BackgroundJobs__RetentionDays=30
Endatix__BackgroundJobs__JobTypes__WebHookDelivery__MaxAttempts=10
```

The options validator checks the values each job type runs with, and names the key each one came
from; a value from code is named by the `JobTypes` key that replaces it.

| Job type | MaxAttempts | MaxRuntimeMinutes | BackoffBaseSeconds / BackoffCapSeconds | RetentionDays | MaxConcurrency | Declared in |
|----------|-------------|-------------------|----------------------------------------|---------------|----------------|-------------|
| `WebHookDelivery` | 8 | 5 | 10 / 3600 | 3 | 4 | `Endatix.Infrastructure` |
| `ReportingCompileFormSchema`, `ReportingFlattenSubmission`, `ReportingSeedDefaultExportFormats`, `ReportingSyncFormDeletion`, `ReportingSyncSubmissionDeletion` | 5 | 10 | 10 / 600 | 3 | 2 | `Endatix.Modules.Reporting` |
| any other | global | global | global | global | 1 | — |

| Key | Default | Purpose |
|-----|---------|---------|
| `RunInProcess` | `true` | Whether this host executes jobs |
| `IdleWaitTimeSeconds` | `2` | How long an idle node waits before looking for jobs another node scheduled |
| `CancellationPollSeconds` | `10` | How often a running job notices it was cancelled |
| `MisfireThresholdSeconds` | `60` | How long a due job may wait for a slot before the backlog warning |
| `ShutdownWaitSeconds` | `30` | How long a stopping host waits for running jobs before leaving them for recovery; never longer than the host's `HostOptions.ShutdownTimeout` (30 s by default) |
| `LockOnInsert` | `false` on PostgreSQL, `true` on SQL Server | Whether enqueueing holds the scheduler's cluster-wide trigger lock until it commits. Read from configuration once, at registration: a `Configure` or `PostConfigure` in code does not change it, and a node whose options say otherwise logs a warning at startup |
| `MaxRuntimeMinutes` | `60` | Ceiling on one attempt (per type) |
| `MaxAttempts` | `3` | Attempts before `DeadLettered` (per type); read when an attempt ends, so a change also applies to jobs already waiting |
| `BackoffBaseSeconds` / `BackoffCapSeconds` | `30` / `900` | Retry backoff (per type) |
| `RetentionDays` | `7` | How long a finished job's row is kept (per type); stamped as `ExpiresAt` when it finishes |
| `Retention:Cron` / `BatchSize` / `MaxBatchesPerRun` | `0 0/15 * * * ?` / `1000` / `50` | When the retention job runs and how much one run deletes |
| `JobTypes:{JobType}:MaxConcurrency` | `1` | Jobs of this type one node runs at once; `0` declines the type |
| `Clustering:CheckinIntervalSeconds` / `CheckinMisfireThresholdSeconds` | `7.5` / `7.5` | A node silent for their sum is presumed dead |
| `Dashboard:Enabled` / `Dashboard:AllowWrites` | `false` / `false` | The operator dashboard, for platform admins |
| `Clustering:InstanceId` | generated | This node's identity; set only to a value no other running node uses |

## Registration

Registered via `EndatixBuilder.UseDefaults()` → `UseModule(JobsModule.Instance)`. Capabilities:
`IEndatixModule`, `IHasFeatureFlag`, `IHasDbMigrations`, `IHasFastEndpoints`. Do **not** also
`Api.ScanAssemblies` this assembly (bypasses the flag).

Gated by `Endatix:FeatureFlags:JobsModule`, **off by default**. The module owns a DbContext
and its own migrations, so registering it where nothing enqueues would create a schema no code
writes to.

## Migrations

The module runs on **PostgreSQL** and on **SQL Server 2025 or later, or Azure SQL Database**, and
registers the context of the configured provider only. With the flag off — the default — nothing is
registered.

Persistence is **provider-split**: `JobsPostgreSqlDbContext` and `JobsSqlServerDbContext` derive
from `JobsDbContextBase`, and each owns its migrations and model snapshot under
`Persistence/Migrations/<Provider>`. EF Core keeps one model snapshot per context type, so adding a
provider means adding a derived context, its own design-time factory, its own `Config/<Provider>/`
configuration and its own migrations folder — never reusing an existing one.

The migrations were reset once, in the change that moved scheduling onto Quartz, to a single
`InitialBackgroundJobs` migration. v0.7.6 and the canaries before this change shipped the earlier
ones (`AddBackgroundJobs`, `RequireRealTenantOnBackgroundJobs`), and EF cannot upgrade a database
from them: it would try to create `jobs."BackgroundJobs"` again and fail. Any database where
`Endatix:FeatureFlags:JobsModule` was turned on under those releases needs `DROP SCHEMA jobs CASCADE`
once before upgrading. That deletes every job row and the schema's migration history. Those rows
never ran, because no release executed jobs, and they cannot be carried over, because each job now
needs a Quartz trigger as well as its row. Back up `jobs."BackgroundJobs"` first if you need a
record of them. From here on `jobs` migrations are append-only.

SQL Server's chain starts with its own `InitialBackgroundJobs`, which creates everything the three
PostgreSQL migrations create, including the trigger acquisition index; `PinTriggerAcquisitionGroupCollation`
then re-creates the acquisition column in a pinned binary collation, with its index. On an upgraded
database that rebuilds the persisted column, which rewrites `qrtz_TRIGGERS` under a schema lock, so
acquisition pauses for the length of the migration, briefly unless the trigger backlog is large. It needs **SQL Server 2025 or
later, or Azure SQL Database**, for the native `json` column type. Trigger acquisition reads its free
groups with `OPENJSON`, which needs database compatibility level 130 or later; that is already a
requirement of the product, whose export procedures (`export_form_submissions_v1`–`v5`) use `OPENJSON` too,
and a startup check for it is tracked in [#1217](https://github.com/endatix/endatix/issues/1217).

Run the commands from the repository root, with `Endatix.WebHost` as the startup project. Each
design-time factory pins its provider and reads `ConnectionStrings:DefaultConnection`, so set a
connection string of that provider.

```bash
# PostgreSQL
dotnet ef migrations add <Name> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Jobs \
  --context JobsPostgreSqlDbContext \
  --output-dir Persistence/Migrations/PostgreSql

# SQL Server
dotnet ef migrations add <Name> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Jobs \
  --context JobsSqlServerDbContext \
  --output-dir Persistence/Migrations/SqlServer
```

Migrations apply automatically at startup when `Endatix:Data:EnableAutoMigrations` is enabled.

### Quartz schema and upgrades

Quartz never migrates its own schema. The module keeps Quartz's `SchemaProvisioning` at `Validate`:
a node checks the tables at startup and refuses to start when one is missing or outdated, but never
creates or changes one. Only the `jobs` EF migrations do.

Both providers' `InitialBackgroundJobs` run Quartz's create script for their dialect,
`create_postgres.sql` or `create_sqlServer.sql`, read at migration time from the Quartz.NET assembly
the module references (`QuartzSchemaScripts`), with the table prefix `jobs.qrtz_`. The module keeps
no copy of either script, so a fresh database always gets the schema that Quartz version validates.

Quartz 4.2.2 embeds only its create scripts, not its upgrade scripts. Quartz's upgrade scripts
(`database/migrations/<version>/` in the Quartz.NET repository, such as
`4.2/add_continuations_<dialect>.sql`) are additive and guarded (`IF NOT EXISTS`, `IF COL_LENGTH(…)
IS NULL` and the like), so they are no-ops on a database whose tables the create script just made.

When a Quartz bump changes the schema, add one EF migration **per provider**. The upgrade scripts do
not use the create script's `{0}`/`{1}` placeholders: they name Quartz's default tables, so each
statement has to be pointed at the module's tables (`QuartzRegistration.TablePrefix`, `jobs.qrtz_`)
by hand.

1. Copy Quartz's guarded upgrade SQL for that provider inline with `migrationBuilder.Sql("""…""")`,
   under a comment linking the file at the Quartz release tag, for example
   `https://github.com/quartznet/quartznet/blob/v<version>/database/migrations/<version>/<file>.sql`.
   `AddTriggerAcquisitionIndex` shows the style. Keep every guard, adapted as below.
   - **SQL Server** (`…_sqlServer.sql`): point every reference to a Quartz table at
     `jobs.qrtz_<TABLE>`, in whatever form it appears: `[dbo].[QRTZ_<TABLE>]` becomes
     `[jobs].[qrtz_<TABLE>]`, `OBJECT_ID('dbo.QRTZ_<TABLE>')` becomes `OBJECT_ID('jobs.qrtz_<TABLE>')`,
     `COL_LENGTH('QRTZ_<TABLE>', …)` becomes `COL_LENGTH('jobs.qrtz_<TABLE>', …)`, and the same for
     any `REFERENCES`. An unqualified or `dbo` reference resolves in the default schema and never sees
     the module's table, so its guard runs a statement that is not needed. Also rename constraint and
     index names from `PK_/FK_/IDX_QRTZ_…` to `PK_/FK_/IDX_qrtz_…`, in the `sys.indexes` guard and the
     `CREATE` alike: the create script names them with the `qrtz_` prefix, so an unrenamed guard misses
     the existing index and the `CREATE` fails (or, under a case-sensitive collation, adds a duplicate).
     The script separates batches with `GO`, which is a client command, not T-SQL: put each batch in
     its own `migrationBuilder.Sql` call.
   - **PostgreSQL** (`…_postgres.sql`): names are lower-case and unquoted. Replace each table reference
     `qrtz_<table>` with `jobs.qrtz_<table>` (not the `qrtz_` inside index names, which already match
     the create script's once lower-cased), and add `table_schema = 'jobs'` to each `information_schema` check
     while keeping its `table_name = 'qrtz_<table>'` literal unqualified, because `table_name` never
     includes the schema; only the `ALTER TABLE` / `CREATE … ON` targets become `jobs.qrtz_<table>`.
     Without the schema check, the guard matches a same-named table in any schema. Each `DO $$ … $$;`
     block can stay in one call.
2. If a later Quartz version embeds its upgrade scripts, load them from the package the way
   `QuartzSchemaScripts` loads the create script, once they carry its placeholders; until then, adapt
   them as above.
3. A fresh database runs `InitialBackgroundJobs` with the new create script first, so the guarded
   upgrade then changes nothing.

**Keep the acquisition index.** Trigger acquisition reads `qrtz_TRIGGERS` through an index of the
module's own, `idx_endatix_qrtz_t_acquire`, keyed on the group each trigger belongs to
(`COALESCE(EXECUTION_GROUP, JOB_NAME)`). On SQL Server that group is the persisted computed column
`ENDATIX_ACQUIRE_GROUP`, and the SQL Server acquisition filters on the column by name; on PostgreSQL
the index is on the expression itself. An upgrade migration whose Quartz script rebuilds
`qrtz_TRIGGERS` (drops and re-creates it, or copies it into a new table) must re-create, in the same
EF migration, `ENDATIX_ACQUIRE_GROUP` and `idx_endatix_qrtz_t_acquire` on SQL Server and
`idx_endatix_qrtz_t_acquire` on PostgreSQL. Quartz validates its own columns only, so it accepts the
extra column. On SQL Server they also block `ALTER TABLE … ALTER COLUMN`: the column on `EXECUTION_GROUP`
and `JOB_NAME`, the index on its other key and included columns (`JOB_GROUP`, `MISFIRE_INSTR`,
`PREFERRED_NODE` and the rest), except where an `nvarchar` key or included column only widens (a
primary-key column never may). An upgrade
migration that alters one of them drops `idx_endatix_qrtz_t_acquire`, then `ENDATIX_ACQUIRE_GROUP`,
first, and re-creates both with the same definitions in the same migration.

On SQL Server, `ENDATIX_ACQUIRE_GROUP` is computed in a pinned binary collation
(`Latin1_General_100_BIN2`), and acquisition reads the free groups in the same one, so groups match
ordinally, as on PostgreSQL, whatever the database's default collation is or later becomes. SQL Server
refuses `ALTER DATABASE … COLLATE` while any schema-bound object depends on the collation (error 5075):
check constraints, filtered indexes and computed columns, `ENDATIX_ACQUIRE_GROUP` among them. A DBA who
changes it anyway drops those objects first and re-creates them afterwards; for this one, drop
`idx_endatix_qrtz_t_acquire` and then `ENDATIX_ACQUIRE_GROUP`, and re-create both with the statements in
the SQL Server `PinTriggerAcquisitionGroupCollation` migration.

See the Quartz.NET [database documentation](https://www.quartz-scheduler.net/documentation/quartz-4.x/db/)
and its [operations page](https://www.quartz-scheduler.net/documentation/quartz-4.x/operations.html).

## Upgrading Quartz

The packages pin Quartz exactly (`[4.2.2]`), because the module depends on its internals and every
node sharing the store must run the same version; an upgrade bumps the pin. An app that references
`Endatix.Modules.Jobs` can take a Quartz patch before Endatix ships it only by overriding the pin
with its own Quartz reference, which NuGet flags with NU1608 and which runs the module on a Quartz
it was not tested with; when another package needs a different Quartz instead, restore fails with
NU1107. That is deliberate: Quartz patches ship as Endatix releases.

An upgrade comes down to two checks:

- [ ] The acquisition-SQL snapshot tests pass for both providers
  (`GetSelectNextTriggerToAcquireSql_Quartz422_MatchesPinnedSql` and
  `LimitEachGroupToItsFreeSlots_Quartz422Statement_MatchesPinnedRewrite`; step 4).
- [ ] When the schema changed, one inline upgrade migration per provider adds it, as
  [Quartz schema and upgrades](#quartz-schema-and-upgrades) describes (step 3).

Unit tests in `Endatix.Modules.Jobs.Tests` pin what the module takes from Quartz, so an upgrade
that changes it fails in CI rather than at deploy time:

1. Bump `Quartz` and `Quartz.Dashboard` together in `Directory.Packages.props`, keeping the exact
   `[x.y.z]` range, and every mention of the old version in this README and in
   `THIRD-PARTY-NOTICES`.
2. Run the module's tests. `PinnedQuartzVersion`, beside the pinned hashes in
   `QuartzSchemaScriptsTests`, names the Quartz the hashes were taken from, and a test fails on every
   upgrade until it names the referenced Quartz: update it once the steps below pass.
3. If `QuartzSchemaScriptsTests` fails on a pinned hash, the SQL that provider's migration runs
   changed. The hash covers the statements `QuartzSchemaScripts` splits Quartz's create script into
   (`create_postgres.sql` for PostgreSQL, `create_sqlServer.sql` for SQL Server), without comment
   lines or indentation and with the table prefix filled in, so changing
   `QuartzRegistration.TablePrefix` moves it too: a prefix change needs a `jobs` migration that
   renames the existing tables and indexes, then a re-pinned hash. For a Quartz upgrade, diff that
   script between the two versions. If the diff changes no table, column, index or constraint
   (keyword case, statement order, a reworded existence guard), update the pinned hash only.
   Otherwise add an idempotent `jobs` migration for what changed, as
   [Quartz schema and upgrades](#quartz-schema-and-upgrades) describes, then update the hash.

   The guard tests (`PostgreSqlTables_…_ReturnsOneStatementPerEntry`,
   `SqlServerTables_…_GuardEachCreateByItsOwnExistenceCheck`) fail when a create would no longer be
   skipped for an object that exists, which the migrations and the store tests rely on when they
   re-run these statements. If Quartz reworded a guard, widen the test's pattern. If it dropped
   one, add a step to `QuartzSchemaScripts` that wraps that create in an existence check (the
   migrations and the store tests all read the statements through it), then re-pin the hash.
4. If `GetSelectNextTriggerToAcquireSql_Quartz422_MatchesPinnedSql` fails for a provider, Quartz's
   acquisition statement changed. Review that provider's rewrite
   (`ExecutionGroupFilteringPostgreSqlDelegate` or `ExecutionGroupFilteringSqlServerDelegate`,
   `LimitEachGroupToItsFreeSlots`) against the new statement, and the `idx_endatix_qrtz_t_acquire`
   index that serves it. Then pin the new statements in `Quartz422AcquisitionSql` and the rewrite of
   them in `Quartz422RewrittenAcquisitionSql` (checked by
   `LimitEachGroupToItsFreeSlots_Quartz422Statement_MatchesPinnedRewrite`), both renamed for the
   new version, together with the tests named after the version and their mentions here.
5. Whatever the tests say, re-read the other overrides of both delegates against the new Quartz
   `StdAdoDelegate` and `AdoJobStoreBase` acquisition code: `SelectTriggers` relies on acquisition
   loading the triggers it fires through it, and `SelectTriggersToAcquire` relies on the command
   being prepared inside the base call. No test sees a change there, and if `SelectTriggers` stopped
   being called, recovered triggers would no longer count against their job type's cap.
6. Roll the upgrade out to every node sharing the store: they must all run the same Quartz version
   (see [Runtime](#runtime)).

## Third-party licence

Quartz.NET is Apache-2.0 licensed. Its licence text ships in `THIRD-PARTY-NOTICES`, both in
this package and in the API image (`/app/THIRD-PARTY-NOTICES`).
