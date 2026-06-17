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
4. **Interfaces at real boundaries only** — email, storage, role mutation ports, host plugins. Do not add `IListXQuery` with a single implementation used by one endpoint.
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

Endpoints inject the concrete query type:

```csharp
public sealed class List(ListPlatformAdmins listPlatformAdmins) : Endpoint<...>
{
    public override async Task<...> ExecuteAsync(...)
        => TypedResultsBuilder.MapResult(
            await listPlatformAdmins.ExecuteAsync(...),
            PlatformAdminUserResponse.MapPage);
}
```

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

Modules may keep MediatR for reads when endpoints and handlers already live in one package; OSS monolith admin lists prefer **direct injection** to avoid Core indirection.

---

## When to keep legacy patterns (for now)

Do not big-bang refactor. Existing areas may still use:

- `Core/UseCases/.../IQueryHandler` + `IUserService` for tenant user lists
- Ardalis specifications in repositories for aggregate CRUD
- MediatR on all endpoints

When touching a feature, prefer migrating **reads** to Infrastructure feature queries; leave stable mutation paths until there is a clear benefit.

---

## Testing

| Layer | Approach |
|-------|----------|
| Core domain / mutation handlers | Unit tests with mocked ports |
| Infrastructure feature queries | Integration tests with test DB when added |
| API | Endpoint/functional tests; response mapping tests (e.g. `PlatformAdminUserResponseTests`) |

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
