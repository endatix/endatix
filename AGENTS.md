# Endatix OSS - Agent Instructions

## 1. Architecture stance

Use practical Vertical Slices inside Clean Architecture.

* Keep the project boundaries: `Endatix.Api`, `Endatix.Core`, `Endatix.Infrastructure`.
* Inside each project, organize by **feature first**, not by technical bucket first.
* Prefer cohesive slice paths, for example:
  * `Endatix.Api/Endpoints/Access/...`
  * `Endatix.Core/Authorization/Access/...`
  * `Endatix.Infrastructure/Features/AccessControl/...`


* **Commands/Writes:** enforce invariants through Core use cases/domain.
* **Queries/Reads:** for single-slice read models, API endpoints may depend directly on Infrastructure query services.
* Use MediatR where it adds real value (cross-slice orchestration, reusable flows), not as mandatory ceremony.

## 3. Anti-fake-abstraction rules

Avoid creating abstractions that exist only to satisfy layering.

* Do not introduce Core interfaces for a read-only path with a single implementation and single consumer.
* Keep query contracts in Infrastructure feature folders when the concern is implementation-centric read composition.
* Move contracts to Core only when they represent stable domain policy or need multiple implementations/reuse.

## 4. Access feature conventions

* Treat Access as its own feature umbrella for now.
* Keep shared access types under `Core/Authorization/Access`:
  * contexts
  * access data DTOs
  * permission/resource constants
* Keep read execution under `Infrastructure/Features/AccessControl`:
  * queries
  * policies
  * mapping/caching orchestration

## 5. Naming and testing

* Endpoint, validator, and test names must describe the exact access mode (`Public`, `Management`, etc.).
* Follow AAA in tests with explicit `Arrange`, `Act`, `Assert` sections.
* Prefer integration tests for endpoint behavior and focused unit tests for access policy/query logic.
