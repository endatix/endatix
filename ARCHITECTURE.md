# Endatix OSS architecture

This document describes how the Endatix API packages are organized today and the **direction** we prefer as the codebase evolves. The exact shape will mature over time; new work should align with these principles rather than copy legacy ceremony.

**Related**

- [Ardalis Minimal Clean Architecture](https://ardalis.github.io/CleanArchitecture/minimal-clean-architecture/) — vertical slices, optional Mediator/CQRS, pragmatic DDD
- SaaS module example: [`src/Endatix.Modules.Agents`](../../src/Endatix.Modules.Agents) + [`Endatix.Modules.Agents.Contracts`](../../src/Endatix.Modules.Agents.Contracts)
- Workspace product notes: repo-root [`ARCHITECTURE.md`](../ARCHITECTURE.md)

---

## Package layout (today)

| Package | Role |
|---------|------|
| `Endatix.Core` | Domain entities, authorization model, shared abstractions (`IRoleManagementService`, …), domain events, **mutation** use cases that must stay persistence-agnostic |
| `Endatix.Infrastructure` | EF Core, identity, email, auth providers, **feature read models** and infrastructure services |
| `Endatix.Api` | FastEndpoints (HTTP), request/response DTOs, validation |
| `Endatix.Framework` | Shared hosting/DI helpers |
| `Endatix.Hosting` / `Endatix.WebHost` | Composition root |

Dependency flow:

```
Api → Infrastructure → Core
Api → Core (policies, Result, shared types)
Core → (no Infrastructure)
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

| Kind | Where | Example |
|------|-------|---------|
| Domain rules / invariants | `Core` | Folder slug uniqueness, `SystemRole` governance |
| Commands (mutations, events) | `Core` use case + handler **or** Infrastructure/module command (per feature) | `GrantPlatformAdminCommand` → `IRoleManagementService` |
| Queries (lists, admin views) | `Infrastructure/Features/{Feature}/` | `ListPlatformAdmins`, `ListPlatformTenants` |
| HTTP | `Api/Endpoints/{Feature}/` | FastEndpoints, validators, API response mapping |

**Naming:** feature query types are named after the operation (`ListPlatformAdmins`), with a single `ExecuteAsync` entry point. This is a **read model**, not MediatR `IQuery<T>`.

---

## Two expressions of the same idea

Monolith features and modules follow the same vertical-slice mindset at different packaging levels.

| Concern | OSS monolith (`Infrastructure/Features/…`) | SaaS module (`Endatix.Modules.*`) |
|---------|---------------------------------------------|-----------------------------------|
| **Feature folder** | `Features/PlatformAdmin/ListPlatformAdmins/` | `Features/Conversations/ListConversations.cs` + handler |
| **Public contracts** | API response DTOs in `Endatix.Api` endpoints | `Endatix.Modules.Agents.Contracts` (`AgentDto`, …) |
| **Domain** | Shared `Endatix.Core` entities | Module `Domain/` (e.g. `Agent`, `Conversation`) |
| **Persistence** | Shared `AppDbContext` / `AppIdentityDbContext` | Module `Persistence/AgentsDbContext` |
| **DI registration** | `AddPlatformAdminFeatures()` | `AddAgentsModule()` |
| **Reads** | Concrete `List*` type → `ExecuteAsync` (no MediatR) | Often MediatR handler + DbContext **inside the module** (still no Core interface) |
| **Writes** | MediatR + Core handler + port (`IRoleManagementService`) | MediatR command/handler in module |
| **Endpoints** | `Endatix.Api/Endpoints/Admin/…` | FastEndpoints colocated in module `Features/*/…cs` |

**Agents** (`ListConversationsHandler`) uses MediatR but injects `AgentsDbContext` directly — no `IConversationListService` in Core. That is the same pragmatic read-side pattern; only the dispatch mechanism differs (Mediator vs direct injection).

**Direction:** large or optional capabilities extract to `Endatix.Modules.*` + `*.Contracts`. Core OSS features stay in `Infrastructure/Features/` with the same slice naming and registration style until extraction pays off.

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
  ListPlatformAdminCandidates/
    ListPlatformAdminCandidates.cs
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

**Registration today** (`AddPlatformAdminFeatures`) mixes styles: shared EF behind `IPlatformAdminUserListing`, thin orchestrators as concrete `ListPlatformAdmins` / `ListPlatformAdminCandidates`, and `IListPlatformTenants` (a transitional endpoint-test seam). Align new work with the [testing evolution](#testing-evolution) below rather than adding more one-off `IList*` types.

**Writes** (`Grant` / `Revoke`) still use MediatR + Core handlers + `IRoleManagementService` because they enforce governance and publish domain events.

This split is the template for new platform-admin and similar admin read endpoints.

---

## When to use MediatR

| Use MediatR | Prefer direct feature type |
|-------------|----------------------------|
| Mutations with invariants and domain events | Paged admin list / report projection |
| Multi-step workflow | Single DbContext (or two) + map to DTO |
| Shared pipeline behaviors (validation, logging) | Endpoint → `ListX.ExecuteAsync` |
| Cross-feature orchestration in Core | Auth admin settings read (`IAuthSettingsReader`) |

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

| Layer | What to test | How |
|-------|----------------|-----|
| **API endpoints** | HTTP mapping, paging defaults, response shaping | `Factory.Create` + mock **`IMediator`** (preferred for reads that use it) or mock **internal** slice contract only when the endpoint already depends on one. Response mapper tests (e.g. `PlatformAdminUserResponseTests`) where mapping is non-trivial. |
| **Thin orchestrators** | Parameter forwarding, early exits, scope selection | Unit-test the `List*` class with **mocked shared read contract** (e.g. `ListPlatformAdmins` + `IPlatformAdminUserListing`). |
| **Shared EF read logic** | Query composition, filters, projections | Extract **pure or mockable** pieces and test those (`PlatformAdminUserRoleScope`, `PlatformAdminExternalRoleReader`, `IRelationalSubstringLikeFilter`). The DbContext-heavy class itself is an orchestrator of EF — cover behavior through collaborators + orchestrator tests above, not a live database. |
| **Core mutations** | Invariants, events, port calls | Unit tests with mocked ports (`IRoleManagementService`, …) on `ICommandHandler` / handler types. |

### `IListPlatformTenants` and similar

Yes, the **`ListPlatformTenants` implementation** should be covered — but not via integration tests. Today the endpoint is tested through `IListPlatformTenants`; the EF implementation is the gap. Close it by:

1. Extracting reusable, testable collaborators (search filter, role scope, external-role parsing — same playbook as Platform Admin users).
2. Unit-testing the thin `List*` orchestrator if it gains branching logic.
3. **Not** adding permanent endpoint-only interfaces; treat `IListPlatformTenants` as transitional.

`PlatformAdminUserListing` follows the target shape: heavy EF stays in one class, testable logic lives in `Common/` helpers, and `ListPlatformAdmins` tests mock `IPlatformAdminUserListing`.

### Testing evolution

Core already defines MediatR markers — `ICommand<T>`, `IQuery<T>`, `ICommandHandler<,>`, `IQueryHandler<,>` — for **Core-owned** work (mutations, cross-cutting reads like `GetAuthSettingsQuery`). Infrastructure feature reads today mostly use **direct `List*` injection**, which forces ad hoc test seams (`IListPlatformTenants`) or skipped implementation tests.

**Direction (no big-bang):**

| Path | When | Endpoint tests | Implementation tests |
|------|------|----------------|----------------------|
| **A — Decomposed direct injection** (current Platform Admin users) | Shared EF across multiple lists in a slice | Inject concrete `List*`; mock only if an **internal** shared contract exists | Mock shared contract; unit-test extracted `Common/` helpers |
| **B — Infrastructure MediatR reads** (Agents module style) | New or refactored OSS read endpoints | Always **`IMediator`** — one established mock | Handler unit tests with mocked DbContext ports / sub-services (same as Core handlers) |
| **C — Core `IQuery`** | Read belongs in Core (persistence-agnostic, shared rules) | `IMediator` | Core handler + mocked ports |

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

## Paged list requests (API)

Searchable paged endpoints share validation and defaults:

| Piece | Location |
|-------|----------|
| Limits (`MaxPageSize`, `MaxSearchLength`, defaults) | `Endatix.Core/Infrastructure/Paging/PagedRequestLimits.cs` |
| Request contract | `ISearchablePagedRequest` extends `IPagedRequest` |
| Validation | `SearchablePagedRequestValidator` (includes `PagedRequestValidator`) |
| Resolved values in endpoints | `request.ResolvePage()`, `request.ResolvePageSize()` |

Use for new list endpoints with optional `Search`. Feature-specific filters stay in the endpoint validator (see `ListUsersValidator` for role/status).

---

## Decision log

| Date | Decision |
|------|----------|
| 2026-06 | Prefer Infrastructure feature queries over Core read handlers when logic is EF-heavy and persistence-specific. |
| 2026-06 | Platform Admin lists: `*QueryService` → `List*` slice types; shared logic in `Common/`; register via `AddPlatformAdminFeatures()`. |
| 2026-06 | Document Agents module as modular end-state; OSS monolith uses the same slice naming inside `Infrastructure/Features/`. |
| 2026-06 | Shared paging: `PagedRequestLimits`, `ISearchablePagedRequest`, `SearchablePagedRequestValidator` for searchable list endpoints. |
| 2026-06 | Testing: unit-only for feature reads — decomposed collaborators + orchestrator mocks; evolve standalone reads toward Infrastructure `IQuery`/MediatR or internal `Common/` contracts; avoid permanent endpoint-facing `IList*` seams. |
