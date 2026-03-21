# Access Boundary: Auth vs Access endpoints

## `Endpoints/Auth/*`

Authentication lifecycle and identity endpoints core for the RBAC (Role based access control) flow:
- `login`, `register`, `logout`
- `me` / current-user lookups

These endpoints deal with authenticating the caller and/or returning identity information.

## `Endpoints/Access/*`

Authorization projection endpoints (ReBAC / resource-based access control):
- return *permissions* for a resource/action context
- public/respondent flows are allowed only when explicitly marked (e.g. `AllowAnonymous()`)

### Public vs Management

- `Endpoints/Access/Public/*`: anonymous/public respondent flows (explicit `AllowAnonymous()`).
- `Endpoints/Access/*`: authenticated backend/management flows for users with Hub access.

Naming conventions:
- Hub backend/management access: `Get<Resource>Access`
- Public/respondent access: `Get<Resource>PublicAccess`
- Prefer resource-specific endpoints over generic “resource access” endpoints until you see repeated duplication across multiple resources.

