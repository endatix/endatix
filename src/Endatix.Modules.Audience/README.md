# Endatix.Modules.Audience

Audience personalization for Endatix: per-form properties and people, tenant match key, and
(later) import and personalised links.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET.

## Registration

Gated by `Endatix:FeatureFlags:PersonalizationModule` (off by default). Requires PostgreSQL.
`EndatixBuilder.UseDefaults()` calls `UseModule(AudienceModule.Instance)`.

## Schema

Database schema: `audience`

| Table | Purpose |
|-------|---------|
| `Settings` | One row per tenant: match key (`email` / `external_id`). No row means `email` |
| `Properties` | Property definitions per form (`VariableName` immutable). Choice types need `ChoicesJson`, a JSON array of distinct keys; other types take no choices |
| `Members` | One person per tenant + match key. `Identifier` keeps the value as entered; `NormalizedIdentifier` is the unique match key. Emails must be one valid address and use the same normalizer as app users; external ids keep their case |
| `Memberships` | Person on one form |
| `PropertyValues` | Property cells on a membership. Each value is at most 4,000 characters and must fit the data type (see below). Empty clears the cell |

## API (directory)

| Method | Route | Notes |
|--------|-------|-------|
| GET/PUT | `audience/settings` | Tenant match key and `isLocked` |
| GET/POST | `forms/{formId}/audience/properties` | List / create |
| PATCH/DELETE | `forms/{formId}/audience/properties/{propertyId}` | Rename/reorder / delete |
| GET/POST | `forms/{formId}/audience/people` | List (page ≤ 5,000) / add |
| PUT/DELETE | `forms/{formId}/audience/people/{membershipId}` | Edit values / remove from this form only |

Permission: `Forms.Edit`, except `PUT audience/settings`, which needs `Tenant.ManageSettings`.

## Rules

- **Match key.** Locked while any person is on any form's audience (`isLocked`). Saving the current
  key is a no-op. A change soft-deletes members that are on no form, since they were matched under
  the old key.
- **Values by data type.** `number`: invariant decimal (`-12.5`, `1e3`). `boolean`: `true` or
  `false`. `date`: `YYYY-MM-DD`. `date_time`: ISO 8601 (`2026-10-05T09:30`, optional seconds and
  offset). `single_choice`: one key from `ChoicesJson`. `multiple_choice`: a JSON array of keys.
  `AllowsOther` accepts any key.
- **Concurrency.** Person creates take a shared tenant lock; a match-key change takes it
  exclusively. The unique indexes settle create races (a second pass reuses the member; a duplicate
  on one form is a 409). Two updates adding the same cell: the second pass overwrites. The people
  list counts and reads in one snapshot.
- **Deletes.** Removing a person deletes the membership and its cells; the member stays. Deleting a
  property soft-deletes its cells in one statement. Reads skip cells of deleted properties.
