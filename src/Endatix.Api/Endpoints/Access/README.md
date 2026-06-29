# Access Boundary: Auth vs Access endpoints

## `Endpoints/Auth/*`

Authentication lifecycle and identity endpoints core for the RBAC (Role based access control) flow:
- `login`, `register`, `logout`
- `me` / current-user lookups

These endpoints deal with authenticating the caller and/or returning identity information.

## `Endpoints/Access/*`

Authenticated Hub management ReBAC projection endpoints:
- return *permissions* for a resource/action context in the admin UI
- require Hub access (e.g. `Permissions(Actions.Access.Hub)`)

Examples:
- `GET access/forms/{formId}` — [`GetFormAccess`](GetFormAccess.cs)
- `GET access/forms/{formId}/submissions/{submissionId}` — [`GetSubmissionAccess`](GetSubmissionAccess.cs)

## `Endpoints/Public/*`

Respondent-facing BFF endpoints grouped with [`PublicApiGroup`](../Common/PublicApiGroup.cs) under the `public/` URL prefix. Public form access lives here (not under `Endpoints/Access/`):

- `GET public/forms/{formId}/access` — [`GetFormPublicAccess`](../Public/Forms/GetFormPublicAccess.cs)
- `POST public/forms/{formId}/access-tokens` — [`CreateFormAccessToken`](../Public/Forms/CreateFormAccessToken.cs)

Naming conventions:
- Hub backend/management access: `Get<Resource>Access` in `Endpoints/Access/`
- Public/respondent access: `Get<Resource>PublicAccess` in `Endpoints/Public/`
- Prefer resource-specific endpoints over generic “resource access” endpoints until you see repeated duplication across multiple resources.

See also root [`ARCHITECTURE.md`](../../../../../ARCHITECTURE.md) (OSS API endpoint layout).
