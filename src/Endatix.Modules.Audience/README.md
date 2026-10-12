# Endatix.Modules.Audience

Audience personalization for Endatix: per-form properties and people, tenant match key, CSV import,
and personalised links.

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
| `PropertyValues` | Property cells on a membership. Each value is at most 4,000 characters and must fit the data type (see below). A stored value is never empty: saving `""` clears (soft-deletes) the cell |
| `Imports` | One row per confirmed CSV import: counts, file name, who ran it |
| `Links` | One hashed token per membership (`IX_Links_Membership`). `SubmissionId` is set once, on first open |

## API (directory)

| Method | Route | Notes |
|--------|-------|-------|
| GET/PUT | `audience/settings` | Tenant match key and `isLocked` |
| GET/POST | `forms/{formId}/audience/properties` | List / create |
| PATCH/DELETE | `forms/{formId}/audience/properties/{propertyId}` | Rename/reorder / delete |
| GET/POST | `forms/{formId}/audience/people` | List (page ≤ 5,000) / add |
| PUT/DELETE | `forms/{formId}/audience/people/{membershipId}` | Edit values / remove from this form only |
| POST | `forms/{formId}/audience/import` | CSV up to 5,000 rows. Rows and summary save together; rejected rows (blank, too long, duplicate identifier) are reported by spreadsheet row, not written. A mapping that names an unknown property or a missing column is a 400 |
| POST | `forms/{formId}/audience/links` | Issue one link per person who does not have one, up to 5,000. The raw token is in this response only. A parallel issue loses on `IX_Links_Membership` and retries once |
| POST | `forms/{formId}/audience/links/{token}/redeem` | Anonymous, `Throttle(60, 60)` (in-process; over the limit is 403). First open creates one in-progress submission and freezes `PersonalizationSnapshot`. A parallel first open keeps one submission. A later open returns that submission. Unknown token, or a link whose submission was deleted, is 404. Response is `submissionId`, `created`, `accessToken` — not the snapshot |

Permission: `Forms.Edit`, except `PUT audience/settings`, which needs `Tenant.ManageSettings`, and redeem, which is anonymous.

## Rules

- **Match key.** Locked while any person is on any form's audience (`isLocked`). Saving the current
  key is a no-op and takes no lock. A change soft-deletes members that are on no form, since they were matched under
  the old key.
- **Values by data type.** `number`: invariant, no surrounding spaces, any finite double (`-12.5`, `1e3`, `1e29`). `boolean`: `true` or
  `false`. `date`: `YYYY-MM-DD`. `date_time`: ISO 8601 (`2026-10-05T09:30`, optional seconds and
  offset). `single_choice`: one key from `ChoicesJson`. `multiple_choice`: a JSON array of keys.
  `AllowsOther` accepts any key.
- **Concurrency.** Person creates take a shared tenant lock; a match-key change takes it
  exclusively. The unique indexes settle create races (a second pass reuses the member; a duplicate
  on one form is a 409). The identifier is checked against the key read under the lock. Two
  updates adding the same cell: the second pass overwrites. Cells are written in property-id order. The people
  list counts and reads in one snapshot.
- **Deletes.** Removing a person deletes the membership and its cells; the member stays. Deleting a
  property soft-deletes its cells in one statement. Reads skip cells of deleted properties.
- **Form deleted.** The `form.deleted` outbox handler soft-deletes the form's cells, memberships
  and properties; tenant members stay. Until it runs, the form's people still count toward the
  match-key lock.
- **Links.** The token is stored as a SHA-256 hash. Opening claims the link with one update
  (`SubmissionId` still null), so two first opens leave one submission. A crash between creating
  the submission and that update can leave an unopened link; the next open creates another.
  Disabled forms, a removed person, and sharing one submission with on-behalf are not gated here.
