# Endatix OSS architecture

This document describes how the Endatix API packages are organized today and the **direction** we prefer as the codebase evolves. The exact shape will mature over time; new work should align with these principles rather than copy legacy ceremony.

**Related**

- [Ardalis Minimal Clean Architecture](https://ardalis.github.io/CleanArchitecture/minimal-clean-architecture/) — vertical slices, optional Mediator/CQRS, pragmatic DDD
- OSS reference module: [`Endatix.Modules.Reporting`](src/Endatix.Modules.Reporting/) + [`Endatix.Modules.Reporting.Contracts`](src/Endatix.Modules.Reporting.Contracts/)
- Commercial waitlist and Agents modules are **not in this git root**. GitHub: [`Endatix.SaaS.Management`](https://github.com/endatix/endatix-saas/tree/main/src/Endatix.SaaS.Management) / [`.Contracts`](https://github.com/endatix/endatix-saas/tree/main/src/Endatix.SaaS.Management.Contracts), [`Endatix.Modules.Agents`](https://github.com/endatix/endatix-saas/tree/main/src/Endatix.Modules.Agents) / [`.Contracts`](https://github.com/endatix/endatix-saas/tree/main/src/Endatix.Modules.Agents.Contracts). In a SaaS workspace checkout, use repo-root [`AGENTS.md`](https://github.com/endatix/endatix-saas/blob/main/AGENTS.md) (path map) and [`ARCHITECTURE.md`](https://github.com/endatix/endatix-saas/blob/main/ARCHITECTURE.md) (onboarding flows).

---

## Package layout (today)

| Package                               | Role                                                                                                                                                               |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Endatix.Core`                        | Domain entities, authorization model, shared abstractions (`IRoleManagementService`, …), domain events, **mutation** use cases that must stay persistence-agnostic |
| `Endatix.Infrastructure`              | EF Core, identity, email, auth providers, **feature read models** and infrastructure services                                                                      |
| `Endatix.Api`                         | FastEndpoints (HTTP), request/response DTOs, validation                                                                                                            |
| `Endatix.Framework`                   | Shared hosting/DI helpers                                                                                                                                          |
| `Endatix.Hosting` / `Endatix.WebHost` | Composition root. DevTools: `GET /dev/embed-host` (`view=builder\|bare`).                                                                                          |
| `Endatix.Modules.*`                   | Optional bounded contexts (domain, persistence, features)                                                                                                          |
| `Endatix.Modules.*.Contracts`         | Module **public** surface only — see [Module packaging](#module-packaging-contracts-vs-domain)                                                                     |

Dependency flow:

```
Api → Infrastructure → Core
Api → Core (policies, Result, shared types)
Api → Modules.*.Contracts (when exposing module vocabulary on HTTP DTOs)
Core → (no Infrastructure)
Core → (no Modules.* — domain and module vocabulary stay out of Core)
Modules.* → Core, Infrastructure, Modules.*.Contracts
```

`Endatix.Api` references `Endatix.Infrastructure` so endpoints may inject concrete read-model types. That is intentional for admin/list queries.

---

## Preferred direction: minimal clean architecture

We are moving away from **Core handlers + Core interfaces + Infrastructure implementations** for every read path, especially when the “use case” is mostly EF projection and paging.

Core cannot reference EF and other infrastructure concerns. Forcing every list through Core creates ceremony (`IQuery` → handler → `IXxxService` → EF) without adding domain value. [Minimal Clean Architecture](https://ardalis.github.io/CleanArchitecture/minimal-clean-architecture/) keeps **rules in the domain** and **data access colocated with the feature** that needs it.

### Principles

1. **Vertical slices by feature** — colocate related code (queries, shared projection helpers, endpoints) under a feature name, not scattered by technical layer only.
2. **Core owns rules, not every query** — entities, invariants, authorization, and ports for swappable integrations stay in Core. Paged admin lists and report-style reads live in Infrastructure (or a module).
3. **Mediator/CQRS is optional** — use MediatR for mutations, workflows, and domain events; skip it for thin EF read models.
4. **Interfaces at real boundaries only** — email, storage, role mutation ports, host plugins. **Internal** shared read contracts within a feature slice are fine when multiple queries reuse EF logic (e.g. `IPlatformAdminUserListing`). Avoid **endpoint-facing** `IListX` interfaces whose only purpose is mocking a single concrete query in API tests; prefer the patterns in [Testing](#testing) instead.
5. **Specifications when reused** — Ardalis specs for repeatable aggregate filters in Core/repositories; LINQ in feature query classes for one-off admin projections.

### Work classification

| Kind                         | Where                                                                        | Example                                                |
| ---------------------------- | ---------------------------------------------------------------------------- | ------------------------------------------------------ |
| Domain rules / invariants    | `Core`                                                                       | Folder slug uniqueness, `SystemRole` governance        |
| Commands (mutations, events) | `Core` use case + handler **or** Infrastructure/module command (per feature) | `GrantPlatformAdminCommand` → `IRoleManagementService` |
| Queries (lists, admin views) | `Infrastructure/Features/{Feature}/`                                         | `ListPlatformAdmins`, `ListPlatformTenants`            |
| HTTP                         | `Api/Endpoints/{Feature}/`                                                   | FastEndpoints, validators, API response mapping        |

**Naming:** feature query types are named after the operation (`ListPlatformAdmins`), with a single `ExecuteAsync` entry point. This is a **read model**, not MediatR `IQuery<T>`.

---

## Two expressions of the same idea

Monolith features and modules follow the same vertical-slice mindset at different packaging levels.

| Concern              | OSS monolith (`Infrastructure/Features/…`)               | SaaS module (`Endatix.Modules.*`)                                                                 |
| -------------------- | -------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| **Feature folder**   | `Features/PlatformAdmin/ListPlatformAdmins/`             | `Features/Conversations/ListConversations.cs` + handler                                           |
| **Public contracts** | API response DTOs in `Endatix.Api` endpoints             | `Endatix.Modules.*.Contracts` (DTOs, commands, queries, events, wire codes — not domain)          |
| **Domain**           | Shared `Endatix.Core` entities                           | Module `Domain/` (e.g. `Agent`, `Conversation`)                                                   |
| **Persistence**      | Shared `AppDbContext` / `AppIdentityDbContext`           | Module `Persistence/AgentsDbContext`                                                              |
| **DI registration**  | `AddPlatformAdminFeatures()`                             | `{Name}Module` + `EndatixBuilder.UseModule()` (OSS Reporting/Jobs/Audience, SaaS Agents + SaaS.Management) |
| **Reads**            | Concrete `List*` type → `ExecuteAsync` (no MediatR)      | Often MediatR handler + DbContext **inside the module** (still no Core interface)                 |
| **Writes**           | MediatR + Core handler + port (`IRoleManagementService`) | MediatR command/handler in module                                                                 |
| **Endpoints**        | `Endatix.Api/Endpoints/Admin/…`                          | FastEndpoints colocated in module `Features/*/…cs`                                                |

**Agents** (`ListConversationsHandler`) uses MediatR but injects `AgentsDbContext` directly — no `IConversationListService` in Core. That is the same pragmatic read-side pattern; only the dispatch mechanism differs (Mediator vs direct injection).

**Direction:** large or optional capabilities extract to `Endatix.Modules.*` + `*.Contracts`. Core OSS features stay in `Infrastructure/Features/` with the same slice naming and registration style until extraction pays off.

**Reference module:** [`Endatix.Modules.Reporting`](src/Endatix.Modules.Reporting/) — BI export read model; see [Module packaging](#module-packaging-contracts-vs-domain).

---

## Module packaging (Contracts vs domain)

Follows [Modulith](https://github.com/foxminchan/Modulith)-style modules. Each module is two projects. **`Endatix.Modules.{Name}` is private.** **`Endatix.Modules.{Name}.Contracts` is the only assembly another module, the host, or a background job may reference.**

| Belongs in Contracts | Stays in the module |
| --- | --- |
| Named payload records and event-type strings another process deserializes | The domain event class (`IIntegrationEvent`) |
| Read DTOs and wire codes (status strings used by filters or HTTP) | Aggregate, value objects, entity error rules |
| A command or query **only when another module calls it** | Slice commands, queries, validators, and handlers |
| | `Persistence/`, `{Name}Module.cs`, and the module's own endpoints |

Contracts must not reference domain entities, EF types, or handlers. The module references Contracts. Nothing else references the module project.

**Integration events.** The wire shape and the in-process event are different types.

- The payload is a named record in Contracts: ids, plus the minimum non-personal data a subscriber needs. If the subscriber loads the row by id, the payload is that id. Email, company, and free-text comments stay on the aggregate.
- The event-type string (`saas.signup.submitted`) lives next to that record.
- The domain event stays in the module (`Domain/Events`). When the id is assigned at save, the event holds the aggregate and `GetPayload()` copies fields into the Contracts record at capture. Contracts cannot take that entity reference. When the id already exists at raise time, the domain event can carry the id directly; it still stays in the module.

**Reference.** Reporting puts status codes and read DTOs in Contracts and `SubmissionIntegrationState` in the module (below). SaaS.Management uses the same split for waitlist events: `SignupSubmittedPayload` in Contracts, `SignupSubmittedEvent` in the module.

Every packable project needs a `README.md` next to the csproj. `Directory.Build.props` packs that file; the Canary pack fails with `NU5019` when it is missing. `Endatix.Modules.Audience.Contracts` is the example.

**Reporting example (`SubmissionIntegrationState`):**

- **Contracts** — `SubmissionIntegrationStatusCodes`, `SubmissionIntegrationSnapshotDto` (future dedicated read API).
- **Domain** — `SubmissionIntegrationState` value object (`[ComplexType]` on `FlattenedSubmission.Integration`), `FlattenedSubmission` (pipeline source of truth).
- **Core `Submission`** — unchanged; integration state is not denormalized onto core rows. Hub can call a future reporting endpoint for integration snapshots.
- **Do not** overload core `Submission.Status` — that is tenant **business workflow** (new/read/approved); integration status is **reporting pipeline** (pending/processed/failed).

**Persistence notes (Reporting PR):**

- Separate `reporting` schema + `ReportingDbContextBase` (one derived context per provider) — CQRS read model, not bloating core `Submissions`.
- Module entities use `BaseEntity` + `ITenantOwned`, not `TenantEntity`, when the context must stay isolated (`Tenant` navigation pulls the core EF graph).
- EF Core 10: `[ComplexType]` + `ComplexProperty` on `FlattenedSubmission` integration state.
- Provider-specific JSON columns and migrations live under `Persistence/Migrations/{PostgreSql|SqlServer}/`.

### Reporting module layout (feature-first)

`Endatix.Modules.Reporting` follows the same **feature-first vertical slices** as monolith `Infrastructure/Features/`, with a dedicated `Shared/` folder for code reused across slices today.

```
Endatix.Modules.Reporting/
  Domain/                          # persistence aggregates
    FlattenedSubmission.cs
    FormSchema.cs
    SubmissionIntegrationState.cs
    ExportFormat.cs
    …

  Features/
    FormSchema/
      FlattenedFormDefinition/
        FormDefinitionFlattener.cs
      FormSchema/
        FormSchemaCompiler.cs
        MergedFormSchema.cs
        FormSchemaColumn.cs
        SchemaCompilationLimits.cs

    FlattenedSubmission/
      FlattenedSubmissionFlattener.cs

  Shared/                          # reused today — not speculative
    SurveyJs/
      SurveyJsElementTypes.cs
      SurveyJsChoiceHelper.cs
      ExportPathBuilder.cs

  Persistence/
  ReportingModule.cs
```

**Dependency flow inside the module:**

```
Features/FormSchema/FlattenedFormDefinition ──┐
Features/FormSchema/FormSchema              ├──► Shared/SurveyJs
Features/FlattenedSubmission              ──┘       (also uses FormSchema types)

Domain/ (entities)         ──► no Features, no Shared, no Persistence
Persistence                ──► Domain
```

**Classification rules (apply the shared-code checklist):**

| Kind                                | Where                                          | Example                                       |
| ----------------------------------- | ---------------------------------------------- | --------------------------------------------- |
| Reporting entities / pipeline state | `Domain/`                                      | `FlattenedSubmission`, `FormSchema`           |
| Compiled form schema model + limits | `Features/FormSchema/FormSchema/`              | `MergedFormSchema`, `SchemaCompilationLimits` |
| Definition → column flattening      | `Features/FormSchema/FlattenedFormDefinition/` | `FormDefinitionFlattener`                     |
| Submission → flat row pipeline      | `Features/FlattenedSubmission/`                | `FlattenedSubmissionFlattener`                |
| SurveyJS JSON traversal helpers     | `Shared/SurveyJs/`                             | `SurveyJsChoiceHelper`, `ExportPathBuilder`   |

**Why not all JSON in Domain?** SurveyJS parsing is reporting-pipeline infrastructure colocated in feature slices; persisted aggregates stay in `Domain/`. Handlers in PR-E5+ call `FormSchemaCompiler` / `FlattenedSubmissionFlattener` — they do not reimplement tree walks.

**Terminology:** "Codebook" is an **export format** in Infrastructure (`CodebookJsonExporter`). Reporting module vocabulary uses **FormSchema**.

**Submission export formats:** exporters live in `Infrastructure/Exporting/Exporters/` and register with `AddExporter`; `ExportValidator` accepts every registered wire key, so a new format needs no API change. `csv` stays raw interchange. `xlsx` (`SubmissionXlsxExporter`, DocumentFormat.OpenXml) is the Excel path — 16+ digit IDs as inline strings, dates as OADate + custom number format, JSON booleans as Excel boolean cells (codebook still `1`/`0`). **Picker lists `reporting.ExportFormats` rows, not registered capabilities.** Runtime catalog: `DefaultExportFormats.All` (`tenant.created` seeder). Existing tenants: frozen `SeedDefaultExportFormats` SQL (same `hashtextextended` ids as `InitialReporting`) — a new default is a new migration, not a catalog change to an applied one. SQL Server's single `InitialReporting` migration ends with the same seed as frozen T-SQL (`SeedDefaultExportFormatsSqlServerSql`, `HASHBYTES` ids).

**Tests mirror features:** golden JSON fixtures under `tests/.../Features/FormSchema/FlattenedFormDefinition/Fixtures/`; compiler tests in `Features/FormSchema/FormSchema/`; submission mapping in `Features/FlattenedSubmission/`.

### Module registration (OSS)

Optional OSS modules implement `IEndatixModule` in a single `{Name}Module` class — no separate `Setup.cs`, no nested registration type.

```csharp
public sealed class ReportingModule : IEndatixModule, IHasFeatureFlag, IHasDbMigrations, IHasFastEndpoints
{
    public static readonly ReportingModule Instance = new();
    private ReportingModule() { }

    public Assembly Assembly => typeof(ReportingModule).Assembly;
    public string FeatureFlag => FeatureFlags.ReportingModule;

    public void ConfigureServices(EndatixModuleBuilder builder)
    {
        if (DatabaseProviderResolver.IsPostgreSql(builder.Configuration))
            AddDbContext<ReportingPostgreSqlDbContext>(builder);
        else
            AddDbContext<ReportingSqlServerDbContext>(builder);
        builder.Services.AddScoped<IReportingDbContext>(sp => sp.GetRequiredService<ReportingDbContextBase>());
        // ... the module's own services, including builder.AddOptions<ReportingOptions>(ReportingOptions.SECTION_NAME)
    }

    private static void AddDbContext<TContext>(EndatixModuleBuilder builder) where TContext : ReportingDbContextBase
    {
        builder.AddDbContextWithMigrations<TContext>(
            ReportingPersistence.ConfigureDbContextOptions,
            // Keeps the module's own opt-out: Customizations:Reporting:ApplyMigrationsAtStartup=false.
            shouldMigrate: sp => sp.GetRequiredService<IOptions<ReportingOptions>>().Value.ApplyMigrationsAtStartup);
        builder.Services.AddScoped<ReportingDbContextBase>(sp => sp.GetRequiredService<TContext>());
    }
}
```

Host wiring: OSS `UseDefaults()` calls `UseModule(ReportingModule.Instance)`, `UseModule(JobsModule.Instance)`, and `UseModule(AudienceModule.Instance)`. `UseModule` scans MediatR on `Assembly`, invokes `ConfigureServices` at finalization, and **only if** the module implements `IHasFastEndpoints` registers FastEndpoints discovery (flag-off modules contribute nothing). Do **not** also call `Api.ScanAssemblies` from `Program` for that assembly — duplicate scan bypasses the flag.

**Provider:** Reporting, Audience and Jobs run on **PostgreSQL and SQL Server 2025 or later (or Azure SQL Database)**. Their persistence follows the module pattern — a `…DbContextBase` plus a PostgreSQL and a SQL Server context (`ReportingPostgreSqlDbContext`/`ReportingSqlServerDbContext`, `AudiencePostgreSqlDbContext`/`AudienceSqlServerDbContext`, `JobsPostgreSqlDbContext`/`JobsSqlServerDbContext`), each with its own migrations under `Persistence/Migrations/<Provider>/`, both migrations namespaces set to the migrations root — and `ConfigureServices` registers the active provider's context only. Audience keeps the SQL Server match key in a binary collation so it stays case-sensitive; Jobs does the same for its dedup columns and its trigger acquisition group. On SQL Server, Jobs enqueues under Quartz's cluster-wide trigger lock by default (`Endatix:BackgroundJobs:LockOnInsert`), and a node set to enqueue without it refuses to start unless the database has `READ_COMMITTED_SNAPSHOT` on. Details: [Reporting README](src/Endatix.Modules.Reporting/README.md), [Audience README](src/Endatix.Modules.Audience/README.md), [Jobs README](src/Endatix.Modules.Jobs/README.md). SaaS Agents and `Endatix.SaaS.Management` are **PostgreSQL-only**: they throw in `ConfigureServices` when `DefaultConnection_DbProvider` is not postgres. Audience uses schema `audience` and is gated by `FeatureFlags.PersonalizationModule` (catalogue key `personalization-module`); the module name is the sample frame, the flag is the product name. Never set `SqlServerMigrationsNamespace` to a PostgreSQL migrations folder. Work in a module that must not interleave across processes takes `ITransactionLock` (`Endatix.Infrastructure.Data.Locking`), a named lock held until the transaction ends; each provider's persistence builder registers it: `pg_advisory_xact_lock` on PostgreSQL, `sp_getapplock` with a transaction owner on SQL Server. How to use it: [Transaction locks](#serializing-work-across-requests-transaction-locks).

**Optimistic concurrency:** For aggregates that can be updated concurrently, use `long Revision` + `IsConcurrencyToken()` and bump `Revision` on `Modified` at save. Same CLR type and column on both providers. Do not use PostgreSQL `xmin` (`uint` / `IsRowVersion`) on dual-provider (or future dual-provider) modules — SQL Server `rowversion` is `byte[]`. Identity keeps `ConcurrencyStamp`. `Form`/`Submission.Revision` today is outbox/event pairing, not an EF concurrency token. Map `DbUpdateConcurrencyException` to HTTP 409. The one exception is `Submission.IsDeleted`, a concurrency token whose conflict means the submission was deleted meanwhile, on its own or with its form: `AppDbContext` maps it to `SubmissionDeletedException`, and the writers answer 404 ([Transaction locks](#serializing-work-across-requests-transaction-locks)). A second concurrency token on `Submission` must make `AppDbContext.IsSubmissionDeletedMeanwhile` check the database values of `IsDeleted` first; otherwise a real concurrent edit surfaces as 404 instead of 409. Unique indexes still cover insert uniqueness.

**Flags:** OSS catalogue (`FeatureFlags.cs`) is for modules `UseDefaults()` might load. SaaS.Management is **module-local** (`SaaSManagementFeatureFlags.SaaSManagement`), default off. SaaS Agents is **always-on** (no `IHasFeatureFlag`) but still implements `IHasFastEndpoints` so endpoints are discovered.

Commercial waitlist (`Endatix.SaaS.Management`) is **not** registered by OSS `UseDefaults`. Product wiring and Hub `/signup` live in the SaaS workspace — see [Related](#endatix-oss-architecture). Distinct from OSS tenant self-registration (`POST /api/auth/register` + Hub `/t/{slug}/register`).

**Startup migrations (two phases):** When `Endatix:Data:EnableAutoMigrations` is true, `DatabaseMigrationService` first migrates core `AppDbContext` and `AppIdentityDbContext`, then iterates registered [`IDbContextMigrationContributor`](src/Endatix.Framework/Modules/IDbContextMigrationContributor.cs) instances for opt-in module/custom contexts. Modules implement `IHasDbMigrations` as a marker; the host warns if `AddDbContextWithMigrations` was not called.

**Module options.** `Endatix.Modules.Jobs` is the pattern for a module's configuration surface:

- One section per module, named by the options type itself — `BackgroundJobsOptions.SectionName` (`Endatix:BackgroundJobs`). The type carries the defaults; no doc restates them.
- The module registers `AddOptions<T>().BindConfiguration(SectionName).ValidateOnStart()` together with an `IValidateOptions<T>` — Jobs adds both when the runner and sweeper are registered — so a value the module cannot run with fails the host at startup rather than at the first tick.
- Validation messages name the **full configuration key** (`Endatix:BackgroundJobs:JobTypes:SubmissionExport:BackoffCapSeconds`): that is what an operator searches for in `appsettings.json` or an environment variable.
- Per-item overrides are a `Dictionary<string, TOverrides>` of nullable properties. Keys match regardless of case, and each unset property falls back to the global value of the same name, one value at a time.
- Overridable values are read only through the resolver (`BackgroundJobsOptions.ResolvePolicy(jobType)`). Reading the global property directly ignores every override, so the runtime never reads those properties itself.

---

## Reference slice: Platform Admin

Platform-scoped admin **reads** use Infrastructure vertical slices:

```
Endatix.Infrastructure/Features/PlatformAdmin/
  PlatformAdminFeatureExtensions.cs   # AddPlatformAdminFeatures()
  Common/
    PlatformAdminUserListItem.cs
    PlatformAdminUserListing.cs       # shared EF logic (internal)
  ListPlatformAdmins/
    ListPlatformAdmins.cs
  ListPlatformTenants/
    ListPlatformTenants.cs
    PlatformTenantListItem.cs
```

Endpoints inject the feature query type directly (or `IMediator` for mutations):

```csharp
// Read — direct injection (today)
public sealed class List(ListPlatformAdmins listPlatformAdmins) : Endpoint<...>

// Write — MediatR + Core handler
public sealed class Grant(IMediator mediator) : Endpoint<...>
```

**Registration today** (`AddPlatformAdminFeatures`) mixes styles: shared EF behind `IPlatformAdminUserListing`, thin orchestrators as concrete `ListPlatformAdmins`, and `IListPlatformTenants` (a transitional endpoint-test seam). Align new work with the [testing evolution](#testing-evolution) below rather than adding more one-off `IList*` types.

**Writes** (`Grant` / `Revoke`) still use MediatR + Core handlers + `IRoleManagementService` because they enforce governance and publish domain events.

This split is the template for new platform-admin and similar admin read endpoints.

---

## When to use MediatR

| Use MediatR                                     | Prefer direct feature type                       |
| ----------------------------------------------- | ------------------------------------------------ |
| Mutations with invariants and domain events     | Paged admin list / report projection             |
| Multi-step workflow                             | Single DbContext (or two) + map to DTO           |
| Shared pipeline behaviors (validation, logging) | Endpoint → `ListX.ExecuteAsync`                  |
| Cross-feature orchestration in Core             | Auth admin settings read (`IAuthSettingsReader`) |

Modules may keep MediatR for reads when endpoints and handlers already live in one package. OSS monolith admin lists use **direct `List*` injection** when a slice has shared `Common/` EF (path A); standalone reads should lean toward **Infrastructure `IQuery` + `IMediator`** (path B) for consistent endpoint tests — see [Testing evolution](#testing-evolution).

---

## When to keep legacy patterns (for now)

Do not big-bang refactor. Existing areas may still use:

- `Core/UseCases/.../IQueryHandler` + `IUserService` for tenant user lists
- Ardalis specifications in repositories for aggregate CRUD
- MediatR on all endpoints

When touching a feature, prefer migrating **reads** to Infrastructure feature queries; leave stable mutation paths until there is a clear benefit.

---

## Testing

We do **not** rely on integration or test-database coverage for feature read models. Testability comes from **layered unit tests** and **extracted collaborators**, not from registering mock-friendly interfaces on every query.

### Layers

| Layer                    | What to test                                       | How                                                                                                                                                                                                                                                                                                       |
| ------------------------ | -------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **API endpoints**        | HTTP mapping, paging defaults, response shaping    | `Factory.Create` + mock **`IMediator`** (preferred for reads that use it) or mock **internal** slice contract only when the endpoint already depends on one. Response mapper tests (e.g. `PlatformAdminUserResponseTests`) where mapping is non-trivial.                                                  |
| **Thin orchestrators**   | Parameter forwarding, early exits, scope selection | Unit-test the `List*` class with **mocked shared read contract** (e.g. `ListPlatformAdmins` + `IPlatformAdminUserListing`).                                                                                                                                                                               |
| **Shared EF read logic** | Query composition, filters, projections            | Extract **pure or mockable** pieces and test those (`PlatformAdminUserRoleScope`, `PlatformAdminExternalRoleReader`, `IRelationalSubstringLikeFilter`). The DbContext-heavy class itself is an orchestrator of EF — cover behavior through collaborators + orchestrator tests above, not a live database. |
| **Core mutations**       | Invariants, events, port calls                     | Unit tests with mocked ports (`IRoleManagementService`, …) on `ICommandHandler` / handler types.                                                                                                                                                                                                          |

### `IListPlatformTenants` and similar

Yes, the **`ListPlatformTenants` implementation** should be covered — but not via integration tests. Today the endpoint is tested through `IListPlatformTenants`; the EF implementation is the gap. Close it by:

1. Extracting reusable, testable collaborators (search filter, role scope, external-role parsing — same playbook as Platform Admin users).
2. Unit-testing the thin `List*` orchestrator if it gains branching logic.
3. **Not** adding permanent endpoint-only interfaces; treat `IListPlatformTenants` as transitional.

`PlatformAdminUserListing` follows the target shape: heavy EF stays in one class, testable logic lives in `Common/` helpers, and `ListPlatformAdmins` tests mock `IPlatformAdminUserListing`.

### Testing evolution

Core already defines MediatR markers — `ICommand<T>`, `IQuery<T>`, `ICommandHandler<,>`, `IQueryHandler<,>` — for **Core-owned** work (mutations, cross-cutting reads like `GetAuthSettingsQuery`). Infrastructure feature reads today mostly use **direct `List*` injection**, which forces ad hoc test seams (`IListPlatformTenants`) or skipped implementation tests.

**Direction (no big-bang):**

| Path                                                               | When                                                      | Endpoint tests                                                               | Implementation tests                                                                  |
| ------------------------------------------------------------------ | --------------------------------------------------------- | ---------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| **A — Decomposed direct injection** (current Platform Admin users) | Shared EF across multiple lists in a slice                | Inject concrete `List*`; mock only if an **internal** shared contract exists | Mock shared contract; unit-test extracted `Common/` helpers                           |
| **B — Infrastructure MediatR reads** (Agents module style)         | New or refactored OSS read endpoints                      | Always **`IMediator`** — one established mock                                | Handler unit tests with mocked DbContext ports / sub-services (same as Core handlers) |
| **C — Core `IQuery`**                                              | Read belongs in Core (persistence-agnostic, shared rules) | `IMediator`                                                                  | Core handler + mocked ports                                                           |

Prefer **A** when a slice has shared EF in `Common/`. Prefer **B** when a read is standalone and endpoint test consistency matters more than avoiding MediatR. Do **not** introduce parallel `IListX` interfaces solely for API tests — either use `IMediator` (B/C) or extract internal collaborators (A).

Mutations stay on **Core `ICommand`** + handlers (or module commands) regardless of read path.

```
                    ┌─────────────────────────────────────┐
  Endpoint          │  IMediator.Send(ListXQuery)         │  ← target for standalone reads (B)
                    │  or  listX.ExecuteAsync(...)        │  ← OK when Common/ is decomposed (A)
                    └─────────────────┬───────────────────┘
                                      │
          ┌───────────────────────────┼───────────────────────────┐
          ▼                           ▼                           ▼
   Core ICommandHandler      Infra IQueryHandler (B)     List* + IPlatformAdmin* (A)
   + ports                   + DbContext / sub-services   + Common/* unit tests
```

When touching Platform Admin registration or new admin lists, migrate toward **one dispatch style per slice** and remove duplicate registrations (e.g. both `IListPlatformTenants` and concrete `ListPlatformTenants`).

---

## Paged list requests

**Api:** compose capability interfaces on the request DTO (`IPagedRequest`, `ISearchableRequest`, `ISortableRequest<T>`, `IFilterable`, plus typed calendar ranges such as `ICreatedRange` / `IModifiedRange`; or `ISearchablePagedRequest` = page + search). Validate with the matching `*RequestValidator`. Map once via `ListRequestExtensions` (`ToSearchablePageRequest`, `ToSortRequest`, `ToCreatedRange`, …). HTTP stays flat stem strings (`createdFrom`/`createdTo`).

**Core:** pass normalized records into read models — `PageRequest`, `SearchablePageRequest`, `SortRequest<T>`, and **`UtcDateTimeRange`** for each timestamp column. Limits in `PagedRequestLimits.cs`. **Count first, then page:** `paging.ForTotal(totalRecords)` (on `PageRequest` or `PagingParameters`) returns a `ResolvedPage` clamped to the last page. Fetch with `resolved.Skip` / `resolved.PageSize` (specs: `new PagingParameters(resolved)`), then return `resolved.ToPaged(items)`. Never skip the raw requested page. EF lists use `QueryablePaging.ToPagedAsync` (`Endatix.Infrastructure.Data`), which does all three. Reference: `ListThemesHandler`, `ListPlatformTenants`. Calendar day strings (`YYYY-MM-DD`) are parsed at the API boundary into one `UtcDateTimeRange` (inclusive From / exclusive To UTC). Specs call `WhereUtcRange`; do not explode back to paired `DateTime?` on ports, queries, or export DTOs.

**Hub client** (`hub/lib/endatix-api/shared`, future `packages/@endatix/api-client`): compose the same wire shape with TypeScript helpers — `IPagedRequest`, `SortRequest<TFields>`, `DateRangeFilter<"created">` / `AuditDateFilters`, plus `parseCalendarDateYmd` / `pickDateRangeFilters` / `appendSortParams` / `appendDateRangeFilters` in `list-query.ts`. Sort field unions stay property names (`createdAt`); date stems stay bare verbs (`"created"` → `createdFrom`/`createdTo`). Feature URL parsers call these helpers; do not hand-copy From/To fields on new list DTOs. Do not nest query objects to mirror Core’s `UtcDateTimeRange`.

| Capability           | Api                                                                   | Core / notes                                                                                 | Hub (`lib/endatix-api`)                             |
| -------------------- | --------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- | --------------------------------------------------- |
| Paging               | `IPagedRequest`                                                       | `PageRequest`                                                                                | `IPagedRequest`                                     |
| Search               | `ISearchableRequest`                                                  | `SearchablePageRequest`                                                                      | explicit `search?` on list DTOs                     |
| Sort                 | `ISortableRequest<TEnum>` (`sortBy` + `sortDir`)                      | `SortRequest<TEnum>` — enum per list                                                         | `SortRequest<TFields>`                              |
| Calendar dates       | `ICreatedRange` / `IModifiedRange` / … (`createdFrom`/`createdTo`, …) | `UtcDateTimeRange` on queries/specs/ports; `WhereUtcRange`                                   | `DateRangeFilter<"created">`, `AuditDateFilters`, … |
| Filter (REST facets) | `IFilterable`                                                         | `FilterParameters` via `FilteredRequestValidator(validFields)` — **not** for DateTime ranges | explicit facet props / `filter=` segments           |
| Filter (domain)      | explicit props / criteria record                                      | e.g. `PlatformAdminUserListCriteria`                                                         | feature-specific request fields                     |

```csharp
var paging = request.ToSearchablePageRequest();
var sort = request.ToSortRequest(PlatformTenantListSortBy.Name);
var created = request.ToCreatedRange();
var filters = request.Filter; // facet expressions, validated by FilteredRequestValidator
```

**Core / Infrastructure list contracts:** `(SearchablePageRequest paging, TCriteria criteria, CancellationToken)`. Put feature filters, `SortRequest<T>?`, and `UtcDateTimeRange` on the criteria record — not as extra method parameters. `SortRequest<T>?` null means keep that list’s documented default order. Return **`Paged<T>` as output only** (not input). Examples: `IUserService.ListUsersAsync` + `UserListCriteria`, `IRoleManagementService.ListRolesAsync` + `RoleListCriteria`, `IListPlatformTenants` + `PlatformTenantListCriteria`, `IPlatformAdminUserListing` + `PlatformAdminUserListCriteria`. Specs still taking a loose `(sortBy, sortDescending)` pair should move to `SortRequest<T>` (see endatix/endatix#980).

---

## Domain and integration events

Endatix uses **domain events** on aggregates (`BaseEntity` → `HasDomainEventsBase`) for in-process reactions and for **durable integration events** captured to the outbox on `SaveChanges`.

### Classification

| Kind                  | Interface                                                            | Dispatch                                         | Example                                                                                               |
| --------------------- | -------------------------------------------------------------------- | ------------------------------------------------ | ----------------------------------------------------------------------------------------------------- |
| In-process only       | `DomainEventBase`                                                    | MediatR after save (when wired)                  | Internal notifications                                                                                |
| Durable / integration | `IIntegrationEvent`                                                  | Outbox capture in `AppDbContext.ProcessEntities` | `submission.completed`, `form.definition.updated`                                                     |
| Customer webhook      | `IIntegrationEvent` + `WebHookOutboxIntegrationEventHandler` mapping | Outbox relay → HTTP                              | `form.updated`, `submission.completed`                                                                |
| Module subscriber     | `IIntegrationEvent` + `IOutboxIntegrationEventHandler`               | Outbox relay → in-process handler                | Reporting: `form.definition.updated`, `submission.updated`, `tenant.created` (default export formats) |

A module registers each subscriber with `services.AddOutboxWork<TWork>()` (`Endatix.Infrastructure.Features.Outbox`): the handler implements `IOutboxWork<TWork>` (`JobType`, `HandledEventTypes`, `Read`), and the one call registers it inline, as a background job (`OutboxJobHandler`) and as the job's subscription to every event in `HandledEventTypes`, so `Endatix:Outbox:DeliverToJobQueue` can move it to the job queue. The job's stored input is `{"outboxMessageId":N}` under the work's `JobType` literal. The job fails for good, without retry, when the outbox row is gone or unreadable, or when an app-level row's payload names another tenant; anything the work throws is retried. With the switch on, startup fails when an outbox work has no job of its own or lists fewer `EventTypes` than its job subscribes, when any other inline handler's events have no subscription from its assembly that no work owns, or when an inline handler is registered twice; `OutboxWorkCoverageTests` boots every module that way.

Plain `DomainEventBase` events that are **not** `IIntegrationEvent` are ignored by the outbox dispatcher and stay in-process.

### Aggregate orchestration (recommended)

Apply these rules in **Core entities** (`Submission`, `Form`, …). Application handlers orchestrate persistence and authorization; they must **not** diff fields or call separate `Notify*` methods after mutation.

1. **Evaluate before mutate** — compare incoming values to current state _before_ assignment. Avoid capturing `previousX` variables, mutating, then passing them to a detector method (parameter creep).
2. **No-op when unchanged** — if material state is identical, return without bumping revision or raising an event (`UpdateStatus`, `SetEnabled`, active-definition activation).
3. **Pair revision + event** — use a private `RegisterRevisedDomainEvent(...)` helper that calls `IncrementRevision()` then `RegisterDomainEvent(...)`. Do **not** override `RegisterDomainEvent` globally — some events intentionally skip the bump (e.g. `form.created` at revision 1, `submission.deleted`).
4. **Encapsulate reporting triggers on the aggregate** — e.g. `Form.UpdateActiveDefinitionSchema` and `Form.SetActiveFormDefinition` raise `FormDefinitionUpdatedEvent`; handlers call those methods instead of separate notify methods. Keep the split explicit: `SetActiveFormDefinition` changes which definition row is active (pointer swap); `UpdateActiveDefinitionSchema` mutates the current active row's JSON/draft status. Do not pass constructed clones to `SetActiveFormDefinition` to effect schema edits.
5. **Use `[Flags]` enums for multi-field changes** — accumulate `SubmissionChangeKinds` inline when several fields can change in one operation; subscribers filter with domain masks (`SubmissionChangeKindsMasks.SubmissionData`, `AffectsSubmissionData()`).
6. **Capture payload values deliberately** — integration event constructors should capture **revision at raise time** (`private readonly long _revision = aggregate.Revision`) so multiple events in one transaction keep distinct revisions. Prefer reading **live aggregate state in `GetPayload()`** for IDs: by the time capture runs (inside `SaveChanges`) the EF `OnAdd` generator has already stamped a real `Id`. Do not freeze `Id` in an event constructor at `new Entity()` / `Create(args)` time — it is still `0` there. Where the event class and the payload record live: [Module packaging](#module-packaging-contracts-vs-domain).

### Entity Ids (snowflake)

The `Id` is a client-assigned snowflake `long`, never a database `IDENTITY`/serial. `BaseEntity.Id`
carries **no** data annotation — `ApplySnowflakeIdValueGenerators` is the single source of truth.

- **Core:** `Form.Create(args)` leaves `Id == 0`; EF stamps it on `Add`. `Form.Create(long id, args)` (same on `Submission`; `FormDefinition.Create(long id, …)`) is for tests, imports, seeding. Handlers and infrastructure services take **no** `IIdGenerator` (only `DataSeeder` / design-time still hold one).
- **Infrastructure:** `ApplySnowflakeIdValueGenerators(Database)` (last in `OnModelCreating`) sets every `long Id` PK to `ValueGeneratedOnAdd` + EF `HasValueGeneratorFactory<SnowflakeValueGeneratorFactory>` + store strategy `None` for the context's provider only, so a snapshot or designer carries one provider's annotation (the parameterless overload, which writes both, is obsolete; `CommittedModelProviderAnnotationsTests` checks every committed snapshot and designer for the other provider's annotation, `CommittedSnapshotDriftTests` checks every snapshot against its context's current model). Jobs is the reference order. The factory only constructs `SnowflakeValueGenerator` (EF caches the model per context type and provider, because each provider gets its own internal service provider). At `Add`, the generator resolves `IIdGenerator<long>` from the host (`AddDbContext` application services), else `SnowflakeIdGenerator`. Do not pass/`new` an `IIdGenerator` in `OnModelCreating`. OSS: App, Identity, Reporting, Jobs, Audience. SaaS `AgentsDbContext` must call the helper last (`IIdGenerator` is already in DI). `ApplySnowflakeIdValueGeneratorsTests` fails CI if a `long Id` PK is missing the generator or still IDENTITY/serial.
- **Not Framework.** `ProcessEntities` / `ApplyEndatixEntityDefaults` stamp `CreatedAt` / `ModifiedAt` only.
- **Tests:** NSubstitute `AddAsync` must stamp `Id` (`ci.Arg<T>().Id = SomeId`). Hand-built contexts need no Id factory.
- **Seeding:** tenant catalogs `Add` per missing default and concede unique races one row at a time (a batch save would drop the rest of the catalog). No `HasData` for those. `DataSeeder` still uses `IIdGenerator` because the in-memory seed graph reads Ids before SaveChanges.

**Known limitations.** `Id` still has a public setter (assign-once is a follow-up). Snowflake is the public id and is roughly enumerable. Legacy positional constructors stay `[Obsolete]` until a test sweep.

### Outbox capture flow

```mermaid
Handler → Form.Create(args) / mutation (RegisterDomainEvent)
       → repository.AddAsync            (EF OnAdd generator stamps Id here)
       → repository.SaveChangesAsync
       → AppDbContext.ProcessEntities
            1. Stamp CreatedAt / ModifiedAt
            2. OutboxIntegrationEventDispatcher.Capture → GetPayload() reads live aggregate Id + serialize
            3. Add OutboxMessage rows (Ids from the same OnAdd generator)
```

```csharp
SubmissionChangeKinds changeKind = SubmissionChangeKinds.None;
if (IsComplete)
{
    if (!string.Equals(JsonData, jsonData, StringComparison.Ordinal))
        changeKind |= SubmissionChangeKinds.Answers;
    // …
}

JsonData = jsonData;
// …

if (changeKind != SubmissionChangeKinds.None)
{
    RegisterRevisedDomainEvent(new SubmissionUpdatedEvent(this, changeKind));
}
```

`OutboxIntegrationEventDispatcher` is intentionally generic — it never switches on concrete event types. Adding a new integration event requires **no dispatcher changes**; wire a subscriber or webhook mapping if needed.

### Testing split

| Layer                  | Project                        | What to test                                                               |
| ---------------------- | ------------------------------ | -------------------------------------------------------------------------- |
| Event rules & revision | `Endatix.Core.Tests`           | Aggregate methods raise the right events / flags; payload shape unit tests |
| Outbox capture         | `Endatix.Infrastructure.Tests` | `OutboxIntegrationEventDispatcher` serializes integration events           |
| End-to-end persistence | `Endatix.IntegrationTests`     | Real DB: outbox rows commit with aggregates; module handlers (Reporting)   |

Do **not** use EF InMemory in module unit tests for persistence — use `Endatix.IntegrationTests` with Testcontainers when the behavior depends on a real provider (JSON columns, query filters, repositories).

---

## Submission lifecycle timestamps

`Submission` carries three distinct clocks:

| Property      | When set                                                                                              | Meaning                                           |
| ------------- | ----------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| `CreatedAt`   | EF insert stamp                                                                                       | Row created (includes prefill / create-on-behalf) |
| `StartedAt`   | Respondent `Create` (`StartSubmission`), first content `Update(...)`, or complete-without-prior-start | First respondent engagement recorded by the API   |
| `CompletedAt` | First incomplete→complete transition                                                                  | Submission finished                               |

Rules:

- Respondent `Create` (public submit / `submissions.create`) stamps `StartedAt` via `StartSubmission`.
- CreateOnBehalf / prefill leave `StartedAt` null until first engagement `Update` (unless completing on create).
- `Update` calls `EnsureStarted()` once (idempotent).
- Completing with `StartedAt` still null sets `StartedAt = CompletedAt` (duration ≈ 0).
- Hub completion time and export `DurationSeconds` use `CompletedAt − StartedAt` (Hub falls back to `CreatedAt` only when `startedAt` is absent on older payloads).

---

## Observability

Logs, metrics and traces come from **one mechanism**: the OpenTelemetry SDK, exporting OTLP. There is
no second logging stack and no vendor SDK in the dependency graph.

| Concern      | Decision                                                                                               |
| ------------ | ------------------------------------------------------------------------------------------------------ |
| Signals      | All three from the OTel SDK, sharing one resource so a record and its span agree on `service.name`     |
| Activation   | Off until an OTLP endpoint resolves. Nothing configured means no exporter allocated                    |
| Precedence   | `OTEL_*` environment variables are authoritative; `Endatix:Telemetry` is a fallback, never an override |
| Registration | `EndatixTelemetryBuilder` owns all three exporters; `EndatixLoggingBuilder` owns the provider list     |
| Failure mode | A malformed endpoint or protocol throws at startup rather than exporting nothing quietly               |
| Vendors      | None shipped. Consumers add their own through `Logging.Configure(...)` or `AddOpenTelemetry()`         |

**Why the log exporter lives in the telemetry builder, not the logging builder.** All three signals
have to resolve the same endpoint, protocol and resource. Splitting that across two builders means
duplicating the resolution and risking a log record whose `service.name` disagrees with its span —
at which point nothing joins them.

**Ordering between the two builders is load-bearing.** `EndatixLoggingBuilder` calls
`ClearProviders()`, and `AddLogging` applies its callback immediately rather than at provider-build
time. The OTel log provider therefore survives only because `Logging.UseDefaults()` runs before
`Telemetry.Build()`, which `FinalizeConfiguration()` guarantees. A test pins this.

**Serilog's remaining role.** It is not the logging pipeline. It is the rotation implementation
behind an optional, off-by-default file provider, registered as a single aliased `ILoggerProvider`
and absent from the configuration surface. .NET ships no file provider, and rotation — concurrent
writers, retention pruning, disk-full, Windows file locking — is not worth owning.

**No vendor exporters, deliberately.** Endatix ships NuGet packages; which APM a host reports to is
the host's choice. An Azure SDK in `Endatix.Hosting` would be in the transitive graph of every
consumer, including those who never deploy to Azure — a permanent dependency to save three
documented lines.

---

## Serializing work across requests (transaction locks)

Use this when a decision reads state and then writes several rows, and two requests or processes must not both make it. Examples: the first open of a link, a match-key change against member inserts, a form-schema rebuild.

**Try a single statement first.** When one row decides the outcome, a unique index or a conditional `UPDATE … WHERE <still unclaimed>` that checks its row count is enough. It needs no lock and no extra connection. Use a lock only when the decision spans reads plus several writes.

**Avoid the wrong shapes.** These two came up on the way to the link-open lock (#1204, #1210, #1211):
- **Write, then undo the loser's write.** Both requests insert, one claims, the other deletes its insert. A delete can hit a row the winner has just adopted.
- **Reserve an id, claim it, then insert.** It needs an `IIdGenerator` in a handler, matching on a primary-key name, and ids out of creation order. A design that needs exceptions to platform rules is a sign to look for an existing primitive first.

### The pattern

Reference: `LinkOpenLock` + `RedeemLinkHandler` (Audience). The exclusive/shared variants are `MatchKeyLock` and `FormSubmissionsLock`: every write that adds a submission shares its form's lock with a near-zero wait, so it is refused (503) during a deletion instead of queueing with a connection held. The deletion checks the form unlocked (a missing or other tenant's form never locks), then takes the lock exclusively, reads the form again, and soft-deletes the submissions by form in batches of 1,000 by id (`SoftDeleteInBatchesAsync`), before it touches the form's own rows, so no statement escalates to a table lock on SQL Server. The batches are an id keyset, but the database picks the plan: SQL Server may read a form's batch through the filtered `IX_Submissions_FormId_CollectionStatus`, where the id narrows nothing, so each batch reads all the form's live rows again (follow-up #1322; the SQL Server data migration hints `IX_Submissions_FormId` for this reason). `Submission.IsDeleted` is a concurrency token: a tracked write that read a submission before its batch matches no row, rolls back with its outbox rows, and is not found (`SubmissionDeletedException`).

1. **Fast path without the lock.** Read the deciding state untracked. When the work is already done (the link is claimed), carry on unlocked.
2. **Take the lock as the transaction's first statement.** Use `transactionLock.BeginLockedTransactionAsync(database, request, ct)` (`TransactionLockExtensions`). It begins the transaction, acquires the lock, and disposes the transaction if acquiring fails. Take one lock per transaction; the stripes of a striped lock (item 3) count as one, taken in ascending order within one wait. A section that needs two takes both before it reads or writes anything, in one order no other section reverses: a link's first open takes the link's lock, then its form's shared `FormSubmissionsLock` (`LinkOpenLock.BeginOpenAsync`).
3. **Key narrowly.** Add a scope constant to `TransactionLockScopes`, and key by the smallest thing that must not interleave (a link id, `{tenantId}:{formId}`). Opens of different keys never wait for each other. Take a shared lock for writers that may run together and an exclusive one for the change that excludes them all (`MatchKeyLock`). Two groups that each run together but exclude the other (imports and removals of a form's people) can't share one shared/exclusive lock: one group would exclude itself. Split the lock into stripes by a key the members of one group have each: that group holds its own stripe exclusively, the other holds every stripe shared, in ascending order within one wait (`MembershipLock`, 16 stripes by a hash of the membership id; endatix#1383). Hash the key before taking the stripe: a snowflake id's low bits are its per-millisecond sequence. Two members of the striped group on one stripe wait for each other, so stripe the group whose sections are short. Lock queues are first come, first served, so a member of the shared group can still wait behind a striped member queued behind another shared one.
4. **Hold one connection.** Every read and write in the section runs on the connection and transaction that hold the lock. When the section spans two contexts, the second joins the first: `other.Database.SetDbConnection(locked.GetDbConnection())`, then `UseTransactionAsync(transaction.GetDbTransaction())`, and `UseTransactionAsync(null)` on dispose (`LinkOpenLock`). Never rent a second pooled connection while holding the lock. Both contexts use `DefaultConnection`, so they share one pool. With N sections open at once and a pool of N, each holds one connection and waits for another that never comes, so every request on the host fails. Joining also commits all the writes together, so a failure or a lost race rolls them all back and leaves no orphan.
5. **Re-check under the lock, from the database.** Another request may have finished while this one waited. Re-read the deciding state untracked. Never act on an entity the context tracked before the wait. `GetByIdAsync` returns the tracked copy, which is stale, and the section would overwrite the other request's write.
6. **Keep the section short.** Inside it, only decide and write. Reads that need no ordering (resuming, a token extend, a check of state the section never writes such as `LinkOpenGates`), slow I/O and HTTP calls run outside the lock, so a refused request never waits for it. A query in the section must use an index: every waiter holds a connection for as long as the section runs.
7. **Bound the wait.** Set `TransactionLockRequest.Timeout`, shorter than the command timeout. Without it, a waiter holds a connection for 30 s and then fails with a raw provider exception, which becomes a 500. Map `TransactionLockTimeoutException` to `Result.Unavailable` (503, retryable) on HTTP. Jobs retry it. The timeout bounds only the lock: a write that waits on another transaction's uncommitted row (an insert behind the same key in a unique index) waits for the command timeout. When a longer section holds a lock and writes rows this section writes too, take that lock first, in a mode that conflicts with the longer section, so the wait happens on the lock before this section holds anything: an edit of a participant takes their stripe of the form's people lock exclusively, as a removal does, which a CSV import holds shared (item 3; #1384).
8. **Keep the database guard and check it.** The lock orders only the code that takes it. A pod still on the previous version during a rolling deploy ([Schema changes during a rolling deploy](#schema-changes-during-a-rolling-deploy)), or another app on the database, can write without it. Keep the conditional `UPDATE` (check its row count) or the unique index (catch its exception, e.g. `DuplicateSubmissionException`). When it says another writer won, roll back and continue from what that writer stored. The refused insert stays tracked as `Added` after the rollback, so clear the change tracker before the next save (`LockedSection`).
9. **Handle rows written before the lock existed.** Rows the old code left can break assumptions the lock now guarantees. Example: a link claimed before its token was saved. Detect that state and take the locked path for it, or repair it once.

### Testing a lock

- **Force the interleaving; don't hope for it.** A `DbCommandInterceptor` gate pauses the first request at a chosen command while it holds the lock. Match both providers' quoting (`"Submissions"`, `[Submissions]`). Then start the second request (`AudienceDirectoryFlowTests.LinkRaces.cs`).
- **Assert the waiting.** The second request has not completed after a short probe, then finishes with the first one's result.
- **Assert one connection.** The joined context's `GetDbConnection()` is the locking context's connection.
- **Cover what the lock doesn't order:** a writer that skips the lock wins (rollback and resume), and a lock held by the test past the timeout (503).
- **Never leave a paused request running.** Release and drain every started request on dispose (`ConfigureAwaitOptions.SuppressThrowing`), so a failed assert does not leak a request into the next test.
- **Check that each test catches its bug.** Remove each guard (the lock, the join, the row-count check, the timeout) and confirm that its test fails.

---

## Reads that grow with imported data (query performance)

Use this when a read's cost grows with rows a tenant can create in bulk (one CSV import adds up to 5,000 people, a form can have 50 Properties), or when CI times out on a statement that passes locally. The worked example is the Audience variables read (`AudienceVariablesReader`): #1363, where a join on cells and memberships timed out in CI, and #1376, which cut its warm p50 by a third to a half at 5,000 Test and 5,000 real participants × 50 Properties.

**Measure before changing.** A plan depends on table statistics. The integration tests share one database, and a bulk import outruns autovacuum, so the planner may still see tables as near empty. Reasoning from LINQ picked the wrong shape here twice, and the first fix for #1376 (reading an id list once) moved nothing measurable. Every step below produces a number or a plan for the PR's Evidence.

### The pattern

1. **Profile the whole request, per statement.** Time the HTTP read, the handler, the reader, the assembly and the serialization separately, and every statement's `Duration` from a `CommandExecuted` `DiagnosticListener` (`StatementCounter` in the Audience flow tests keeps the SQL). Tag the probe (`[DEBUG-<issue>]`) and never commit it. In #1376 SQL was 90% of the request; assembly and serialization were 0.1 ms each, so compiled queries or a faster serializer had nothing to win.
2. **Read the plans on both providers.** On PostgreSQL, `auto_explain` with `log_analyze` set on the test database (`ALTER DATABASE … SET session_preload_libraries = auto_explain`, then clear the pool); on SQL Server, `sys.dm_exec_query_stats` with `sys.dm_exec_query_plan`. Compare the rows each plan reads with the rows it returns: a statement returning 990 cells read 281k buffers.
3. **Test at realistic size, on the risky side.** An integration test at 5,000 × 50 on PostgreSQL and with `ENDATIX_TEST_DB_PROVIDER=SqlServer`. Put the bulk where the code is at risk: 5,000 real participants with one Test participant never ran the many-Test-ids path.
4. **Time it with stale and fresh statistics.** Stale: analyze the tables before the import and stop them refreshing (`autovacuum_enabled = false` on PostgreSQL, `UPDATE STATISTICS … WITH NORECOMPUTE` on SQL Server). Fresh: `VACUUM ANALYZE` or `UPDATE STATISTICS … WITH FULLSCAN` after it. Report one cold read and p50 and p95 over at least 20 warm reads. A shared laptop varies by ±50% between runs, so alternate before and after runs under the same load, and set the target before the first change.
5. **Read a small set by its key, on one table.** The presets need 20 participants' cells. An `EXISTS` on the Properties let both planners drive from the 50 Properties: PostgreSQL read every cell of the tenant (196 ms), SQL Server scanned the whole table (48 ms). Filtering by the live Property ids the caller already read leaves one table and a seek per participant on `IX_PropertyValues_Cell` (tenant and membership, with the Property ids checked in the index), 1–3 ms (`PropertyValuesWriter.ActiveCells`).
6. **Count each large set once, with no join.** Count with `GROUP BY … COUNT` in the database. When the count splits rows by a flag of a related row, a join or `IN (subquery)` lets the planner loop one side over the other: PostgreSQL timed out at 30 s (#1363). An id list sent back as one parameter avoids that plan but still reads every row twice. When the flag never changes (`Membership.IsTest`), copy it onto the counted row and put it in the index (`PropertyValue.IsTest`, `IX_PropertyValues_Property` on tenant, Property and test flag), so one `GROUP BY` over one index range per Property counts both groups. The cost is a copy that must never drift, so let the database enforce it, as it already holds the row's `TenantId` copy: a composite foreign key from the row's (tenant, parent id, flag) onto an alternate key with the same three columns on the parent (`FK_PropertyValues_Membership` → `AK_Memberships_TestFlag`), validated after the migration copies the flag onto the rows already written. Build the row only from its parent (`PropertyValueCreateArgs.For(membership, …)`, with no public constructor) and give the copy no default in the EF model (EF would then omit it from inserts and read it back). An older version that writes the wrong flag then fails loudly instead of miscounting, so add the key one release after the column ([Schema changes during a rolling deploy](#schema-changes-during-a-rolling-deploy)).
7. **One change at a time; keep what moves p50.** Measure each change before the next. Candidates that the profile shows below the noise stay out (here: the Properties read twice, about 2 ms).
8. **Assert the shape, not only the count.** A statement-count assertion against a small form passes when both forms change together. Assert the statements themselves: one counts the cells, none filters memberships by the flag, none joins the two tables (`CountsCells`, `FiltersTestMemberships`, `JoinsCellsToMemberships` in `AudienceDirectoryFlowTests.Coverage.cs`).

---

## Schema changes during a rolling deploy

Use this when a migration changes a table that the previous release writes. Two releases share the database during every deploy. The API migrates at startup (`Endatix:Data:EnableAutoMigrations`), and the Helm chart (`helm/templates/deployment.yaml`) sets no `strategy`, so Kubernetes' default rolling update starts the new pod before it stops the old one, even at one replica. The new pod migrates while the old pod still serves requests with the previous release's EF model and its own locks. Any host that starts the new version before it stops the old one has the same window.

The example is `AddPropertyValueIsTest` (endatix#1376, #1389). It adds `PropertyValues.IsTest` and, in the same migration, `FK_PropertyValues_Membership` on it. The previous release's insert omits the column, so the database default `false` applies, and for a Test participant the foreign key refuses the row: a 500 until the last old pod stops. It ships both at once only because the Audience module was a preview until then (`personalization-module`, off by default since v0.8.0), so no production host wrote `PropertyValues` before it. A host that turned the preview on sees that 500 during that one upgrade.

**The rule: expand, then contract, one release apart.** A release here is a stable `vX.Y.Z`, not a canary: OSS hosts upgrade from stable to stable, and the expand must already be running everywhere before the contract starts. A migration makes only changes that the previous release's writes still satisfy. A change those writes would break ships in a later stable release than the expand. An upgrade must pass through the expand release; the contract release's notes say so in their **Compatibility** table. The rule lives in the migrations, not in the deploy, so it holds for any host and deploy tool.

| Change | Release N (expand) | Release N+1 (contract) |
| --- | --- | --- |
| Add a column to a table the previous release writes | Nullable, and new code reads `NULL` as the old meaning; or `NOT NULL` with a database default that is right for every row the previous release inserts (EF's `defaultValue:` in the migration is that default). Backfill the rows whose right value differs from the default. | Backfill again: rows the previous release wrote during N's window took the default or `NULL`. Drop the default if new code must always write the column; when the EF model has no default (checklist item 2), EF scaffolds no change for it, so write that `AlterColumn` by hand. |
| A foreign key, unique index, unique constraint or check that covers that column | — | Add it after that backfill. Adding it validates every row. |
| A new transaction lock on writes the previous release also makes | Take the lock, and keep the database guard ([Transaction locks](#serializing-work-across-requests-transaction-locks), step 8), so the previous release's unlocked writes stay correct. | Rely on the lock: every running writer takes it. |
| Rename or drop a column or table | Stop reading it, and make it nullable or defaulted so inserts that omit it succeed. For a rename, add the new column and write both. | Drop it (after a backfill of the new column, for a rename). |

A new table, and constraints on it, need no second release: the previous release never writes it. A foreign key from the new table onto a table the previous release deletes from or rekeys is the exception: it constrains those writes, so it waits for the contract.

**A failed migration does not stop the pod.** `DatabaseMigrationService` logs the failure and lets the host start, so a contract whose validation fails on a row leaves the pod serving on the expanded schema, with the later migrations unapplied; only the startup log shows it. Check that the contract migration applied after the deploy (`__EFMigrationsHistory` in the module's schema), and fix the rows, not the schema.

**Why not migrate in a pre-deploy job.** A Helm pre-upgrade hook still leaves the old pods serving against the new schema until the rollout ends. Only stopping every old pod before the new one starts (`strategy: Recreate`, or scaling to zero) closes the window, which takes the API down on every release that migrates. An OSS host that deploys some other way never sees the chart's job.

**During N's window, a lock is not yet a guarantee.** A writer of the previous release does not take N's lock, so a locked request can wait on its uncommitted row (an insert behind the same key in a unique index) until the command timeout, which surfaces as a raw provider exception (step 7: a 500). The database guard keeps the data correct. When the previous release writes those rows in a long section (a CSV import), turn the command timeout in N's locked section into `Result.Unavailable` (503, retryable) as well, or the window returns 500s.

### Checklist for a migration author

1. **List what the previous release writes** to every table the migration changes: its EF model at the last stable tag, not `main` or the last canary.
2. **Give a new column a safe value for old writers.** Nullable, or a database default that is right for the previous release's inserts. EF scaffolds the CLR default (`false`, `0`, `""`) for every required column, so decide whether that value is right; no test can (`AudienceMigrationsRollingDeployTests` only catches a required column with no default at all). When new code must always write the column (a copied flag), give it no default in the EF model, so EF never omits it from new inserts (`PropertyValue.IsTest`).
3. **Never constrain a column in the release that adds it** (foreign key, unique index or constraint, check). Ship the constraint in the next stable release, behind a second backfill. `AudienceMigrationsRollingDeployTests` catches the column and the constraint in one Audience migration; it cannot see two migrations of one release, raw `Sql(...)`, an `AlterColumn` or other modules, so the PR review checks those. It also flags a unique index that only gains the new column (a widening); exempt that by name with the reason.
4. **Keep the database guard for a new lock** for one release, and map a command timeout in its section to 503 when an old-pod write can hold its rows for long.
5. **Say in the PR which release is the expand** and which issue tracks the contract, so the second half ships. After the deploy, confirm the migration is in `__EFMigrationsHistory`.

---

## Multi-tenancy (platform tenants)

Gated by the deployment flag `multi-tenancy` (`FeatureFlags.MultiTenancy`). Off → mutating `/admin/tenants` and assume-tenant endpoints return 404 rather than advertising the feature.

**Isolation vs routing**

- **Authorization** is the signed JWT `tid` (session tenant). Optional `act` marks an assume-tenant session. Path segments, headers, and query strings are never trusted for data access.
- **`Tenant.ShortUrl`** is an **opaque 8-character lowercase alphanumeric id** (alphabet `a-z0-9`, CSPRNG via `IShortUrlGenerator`, letter-heavy). It is unique, immutable, and used only on unauthenticated routes such as `/t/{shortUrl}/signin` and `/t/{shortUrl}/register`. It is **not** derived from the tenant name and is not client-supplied. Inbound lookup is folded with `ShortUrl.Normalize`, then compared exactly.
- Numeric `Tenant.Id` stays internal (JWT, FKs, admin APIs). `GET /public/tenants/{shortUrl}` must not return it.
- Forms can reuse `IShortUrlGenerator` later with a per-entity `Form.ShortUrl` column. A polymorphic URLs table is deferred until vanity aliases or redirects are required.

**Data isolation (EF query filters)**

`ApplyEndatixQueryFilters` registers two **named** EF 10 filters on every entity of `AppDbContext`, `ReportingDbContextBase`, `JobsDbContextBase`, and `AudienceDbContextBase`:

| Name                                 | Applies to                | Predicate                               |
| ------------------------------------ | ------------------------- | --------------------------------------- |
| `EndatixQueryFilterNames.SoftDelete` | anything with `IsDeleted` | `!IsDeleted`                            |
| `EndatixQueryFilterNames.Tenant`     | `ITenantOwned`            | `ambient == 0 \|\| TenantId == ambient` |

Ambient tenant is `ITenantContext.TenantId`, set only by `TenantMiddleware`. **Tenant `0` is bypass, not isolation** — background work (outbox relay, hosted services, provisioning) runs unscoped and therefore sees every tenant.

- **Declare every global filter as a named filter, keyed in [`EndatixQueryFilterNames`](src/Endatix.Infrastructure/Data/EndatixQueryFilterNames.cs).** A new cross-cutting filter adds a constant there plus its own `HasQueryFilter(name, ...)` call. Never register an anonymous filter and never `&&` a new condition onto an existing one — a combined filter can only be dropped whole, which is what forces the blanket opt-out below.
- Opt out **by name** so the other filter keeps applying:

  ```csharp
  dbContext.ExportFormats
      .IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
      .Where(format => format.TenantId == tenantId);
  ```

  Bare `IgnoreQueryFilters()` also drops soft delete and forces a hand-copied `!IsDeleted` that drifts. Keep it only where a purge must include soft-deleted rows (`FlattenedSubmissionRepository.DeleteByFormIdAsync`).

- Work that targets a tenant other than the ambient one takes `tenantId` as a parameter and filters on it explicitly — `ExportFormatRepository.SeedDefaultsAsync` (outbox `tenant.created`). A unique-index collision on seed is a race when the live row is the same default (name + target + delivery + profile); a different profile reusing the name is skipped so the rest of the catalog still seeds. Tenant-scope mapping is looked up regardless of `IsDefault` so a cleared default is not duplicated. Filtered unique indexes on `SurveyTypeExportMapping` include `IsDeleted = false` so a soft-deleted default does not block a live replacement (`ReportingDbContextTests.Model_SurveyTypeExportMapping_UsesFilteredUniqueIndexes`).
- **HTTP reads of tenant-owned rows pass the tenant and refuse `<= 0`.** An authenticated principal can reach a handler with an ambient tenant of `0`: `TenantMiddleware` returns without setting it when the `tid` claim is missing or unparseable, and the default policy does not require one. On a filtered read, that is every tenant's data. The endpoint passes `tenantContext.TenantId` into the query, the handler returns `Result.Unauthorized` when it is `<= 0`, and the predicate filters on `TenantId` itself instead of trusting the filter (`GetExportFormatHandler`, `GetJobHandler`). Cover it over HTTP (`JobStatusApiFlowTests`) — an in-memory context is neither `TenantMiddleware` nor the database filter.
- **Never use tenant `0` as the "other" tenant in a test.** It bypasses the filter, so the test passes with or without the code under test. Use a second non-zero tenant (`ReportingQueryFilterTests`, `ExportFormatSeedIntegrationTests`). In `Endatix.IntegrationTests`, `new IntegrationTenantContext(id)` enforces this; `IntegrationTenantContext.Bypass` is the one filter-off stub.

**Create/edit**

- PlatformAdmin `POST /admin/tenants` provisions `Tenant` + `TenantSettings` in one transaction and assigns the public id server-side (unique-index retry).
- `PATCH /admin/tenants/{id}` may change name, description, and self-registration policy. The short URL cannot change.
- Creating a tenant does **not** add the PlatformAdmin as a member of that tenant. A user belongs to **one** tenant (`AppUser.TenantId`). Multi-tenant membership is a later Identity table, not cloned roles.

**JWT session** (`AccessTokenIssueOptions` / `AccessTokenSession`). Refresh remints from the **token** (`tid` + optional `act`), not from `AppUser.TenantId`. One mint path; `act` is not impersonation (`sub` stays the admin). Outbox `tenant.context.changed` with kinds `assumed` / `exited`. Kind `switched` is reserved for a future membership product.

| Session                       | `sub`        | `tid`           | `act`                                                   | Status                      |
| ----------------------------- | ------------ | --------------- | ------------------------------------------------------- | --------------------------- |
| Login                         | member       | home tenant     | absent                                                  | refresh keeps session `tid` |
| Assume tenant                 | **admin**    | target          | admin                                                   | not impersonation           |
| On-behalf / login-as-customer | **customer** | customer tenant | support plus a **new claim** (do not reuse `act` alone) | **not implemented**         |

Overloading `act` so `sub` is the customer would break assume-authz (`act == sub`). Support until then is assume-tenant (see the tenant as admin).

`POST /auth/assume-tenant` mints the assumed session, `POST /auth/exit-assume` returns to the home tenant. Both require `AuthorizationPolicies.PlatformAdminAccess`, sit behind `MultiTenancyGate` (off → 404), and answer with `TenantSessionResponse`. Assumed access tokens expire after `AssumeTenantSession.AccessExpiryMinutes`; refresh keeps that shorter lifetime while `act` is present. Sessions do **not** nest — assuming from an assumed session into a **different** tenant is a **400**; exit first. Assuming the tenant the session `tid` already has is a **409** (no new tokens). `POST /auth/exit-assume` when the session is **not** assumed stays **400** — that is a missing `act`, not “already in the requested tenant”. Neither handler writes membership; the audit trail is `tenant.context.changed` raised on the assumed tenant. Refresh persistence failure is returned as an infrastructure error (same as login), not a silent success.

`Actions.Platform.AssumeTenants` is reserved and **not catalog-seeded** — add an AppIdentity migration before any policy requires it. `Actions.Platform.ImpersonateUsers` is catalog-seeded but unused.

**Assume-tenant vs impersonation**

- **Assume** (`POST /auth/assume-tenant`): PlatformAdmin enters a tenant without becoming a member. JWT `tid` is the target; `act` is the actor. Short-lived access token. Must not appear in the tenant user directory. Exit with `POST /auth/exit-assume`.
- **Impersonation** (`platform.users.impersonate`) stays reserved. Assume is not “login as user X”.

**Self-registration**

- Default **off**. `GET /api/public/tenants/{shortUrl}` returns `{ slug, name, selfRegistrationEnabled, allowedAuthProviders }` (no numeric id). Unknown, deleted, or invalid short URL format → 404. Rate-limited. Successful lookups use **HybridCache** (`GetOrCreateResultAsync`, 3 min); 404s are not stored. `PATCH /admin/tenants/{id}` removes the entry. `allowedAuthProviders` is a UI hint — register does **not** enforce the list this ship.
- `POST /api/auth/register` without `tenantSlug` still creates an unattached user (`TenantId = 0`) for global `/create-account`. With `tenantSlug`: unknown → 404; self-reg off → 403; email already registered → validation error; otherwise register into that tenant and assign `DefaultRegistrationRoleName` as the **shared** system role (not a cloned `AppRole`). The role is resolved against the tenant with `GetMissingAssignableRoleNamesAsync` **before** the user is created (same pre-flight as `InviteUserHandler`), and `RegisterTenantUserAsync` then writes the user row and the role grant in **one `AppIdentityDbContext` transaction** — both live in that context, so this needs no saga or compensation. The verification email is sent only after the commit, because it cannot be unsent. Self-registration therefore never leaves an account without a role, which no retry could repair once the email is taken. PlatformAdmin and Public are not allowed as defaults. Raises `UserRegisteredEvent`.

**Caching**

- Use `HybridCache` (`AddHybridCache` in Infrastructure), not `IMemoryCache`.
- Success-only `Result` factories: [`HybridCacheExtensions.GetOrCreateResultAsync`](src/Endatix.Infrastructure/Caching/HybridCacheExtension.cs) — public tenant GET.
- Authz session data: [`AuthorizationCache`](src/Endatix.Infrastructure/Identity/Authorization/AuthorizationCache.cs) (tags + invalidate). Do not cache failed lookups or anonymous-oracle failures.

---

## Decision log

| Date    | Decision                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-07 | Submission `StartedAt` for reliable completion duration (distinct from `CreatedAt`); export includes `StartedAt` + computed `DurationSeconds`. See [Submission lifecycle timestamps](#submission-lifecycle-timestamps).                                                                                                                                                                                                                                             |
| 2026-07 | Domain events: evaluate-before-mutate on aggregates; reporting triggers (`FormDefinitionUpdatedEvent`, `SubmissionUpdatedEvent`) live on entities, not handlers. Documented in [Domain and integration events](#domain-and-integration-events).                                                                                                                                                                                                                     |
| 2026-08 | Observability: three signals, one mechanism. The OpenTelemetry SDK owns logs, metrics and traces; `OTEL_*` environment variables are authoritative over `appsettings.json`; telemetry is off until an endpoint is configured. Serilog is removed as the logging pipeline and retained only as the rotation implementation behind an optional, off-by-default file provider. **Endatix ships no vendor exporters** — OTLP only. See [Observability](#observability). |
| 2026-08 | Tenant public id: keep `Tenant.ShortUrl` as a unique immutable `varchar(8)` column; generate with `IShortUrlGenerator` (CSPRNG, `a-z0-9` alphabet, letter-heavy). Do not derive from name. JWT `tid` remains the isolation boundary. See [Multi-tenancy (platform tenants)](#multi-tenancy-platform-tenants).                                                                                                                                                       |
| 2026-08 | Tenant access: one home `AppUser.TenantId`. Assume-tenant uses JWT `act` and does not write membership. `platform.users.impersonate` stays reserved. Self-reg is opt-in per tenant via opaque public id. Multi-tenant membership is a later Identity table.                                                                                                                                                                                                         |
| 2026-09 | JWT session is `tid` + optional `act`. Refresh copies that session. `tenant.context.changed` covers assume and exit. Impersonation (`sub` = customer) is a later claim, not a second token service. See [JWT session](#multi-tenancy-platform-tenants).                                                                                                                                                                                                             |
| 2026-09 | Submissions Excel: a native XLSX exporter, not CSV `="id"` wrapping — CSV stays raw interchange. XLSX types a cell from the JSON value only; numeric-looking text (`007`, `NaN`) stays text. See [Module packaging](#module-packaging-contracts-vs-domain) (submission export formats).                                                                                                                                                                             |
| 2026-10 | Submission export `collectionStatus`: pipe-separated wire codes. When set, it replaces `completionStatus` (`IsComplete`). Reporting filters with `Contains` on the `CollectionStatus` value, not `.Code` (the value converter does not translate member access). Legacy SQL functions return the column and do not apply the filter. |
| 2026-10 | Background Jobs run on SQL Server as well as PostgreSQL, through the module pattern (`JobsSqlServerDbContext`). On SQL Server, enqueueing takes Quartz's trigger lock (`LockOnInsert` defaults to `true` there, `false` on PostgreSQL); turning it off is an operations change that needs `READ_COMMITTED_SNAPSHOT`, which the scheduler checks before it starts. The acquisition group column is computed in a pinned binary collation, read in the same one, so acquisition never depends on the database's default collation. See the [Jobs README](src/Endatix.Modules.Jobs/README.md). |
| 2026-10 | Screen-out closes the interview without `IsComplete`. Public saves send `collectionOutcome=screen_out`. Later updates are rejected. The webhook is `submission.collection_status_changed`, not `submission.completed`. |
| 2026-10 | Schema changes during a rolling deploy: expand, then contract, one stable release apart, and an upgrade passes through the expand. A column ships nullable or with a default that is right for the previous release's writes; a foreign key, unique index or check on it ships in a later stable release behind a second backfill; a new lock keeps its database guard for one release. Chosen over a pre-deploy migration job, which leaves old pods serving during the rollout unless they are stopped first (downtime on every migrating release), and which only Helm deploys would get. See [Schema changes during a rolling deploy](#schema-changes-during-a-rolling-deploy). |
