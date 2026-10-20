# Endatix.Modules.Audience

Audience personalization for Endatix: per-form properties and people, tenant match key, CSV import,
and personalised links.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET.

## Registration

Gated by `Endatix:FeatureFlags:PersonalizationModule` (off by default). Runs on PostgreSQL and on SQL Server 2025
or later (or Azure SQL Database). `EndatixBuilder.UseDefaults()` calls `UseModule(AudienceModule.Instance)`.

The module registers `AudiencePostgreSqlDbContext` or `AudienceSqlServerDbContext` for the configured provider;
handlers see it only as `IAudienceDbContext`. On SQL Server, keep `READ_COMMITTED_SNAPSHOT` on for the database (on in a
database Endatix creates and in Azure SQL Database; a database created beforehand may have it off): without it, other
tenants' audience reads wait for a running CSV import to commit.

## Migrations

Each provider has its own chain in this assembly: `Persistence/Migrations/PostgreSql/` and
`Persistence/Migrations/SqlServer/`. Run from the repository root:

```bash
# PostgreSQL
ConnectionStrings__DefaultConnection="Host=localhost;Database=endatix;Username=postgres;Password=..." \
dotnet ef migrations add <MigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Audience \
  --context AudiencePostgreSqlDbContext \
  --output-dir Persistence/Migrations/PostgreSql

# SQL Server
ConnectionStrings__DefaultConnection="Server=localhost;Database=endatix;User Id=sa;Password=...;TrustServerCertificate=True" \
dotnet ef migrations add <MigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Audience \
  --context AudienceSqlServerDbContext \
  --output-dir Persistence/Migrations/SqlServer
```

A model change needs a migration in both chains.

## Schema

Database schema: `audience`

| Table | Purpose |
|-------|---------|
| `Settings` | One row per tenant: match key (`email` / `external_id`). No row means `email` |
| `Properties` | Property definitions per form (`VariableName` immutable). Choice types need `ChoicesJson`, a JSON array of distinct keys; other types take no choices |
| `Members` | One person per tenant + match key. `Identifier` keeps the value as entered; `NormalizedIdentifier` is the unique match key. Emails must be one valid address and use the same normalizer as app users; external ids keep their case. On SQL Server the match key uses the binary collation `Latin1_General_100_BIN2`, so it compares case-sensitively as on PostgreSQL |
| `Memberships` | Person on one form. `IsTest` marks a test person; it is set when the person is added and never changed |
| `PropertyValues` | Property cells on a membership. Each value is at most 4,000 characters and must fit the data type (see below). A stored value is never empty: saving `""` clears (soft-deletes) the cell |
| `Imports` | One row per confirmed CSV import: counts, file name, who ran it |
| `Links` | One hashed token per membership (`IX_Links_Membership`). `SubmissionId` is set once, on first open |

## API (directory)

| Method | Route | Notes |
|--------|-------|-------|
| GET/PUT | `audience/settings` | Tenant match key and `isLocked` |
| GET/POST | `forms/{formId}/audience/properties` | List / create |
| PATCH/DELETE | `forms/{formId}/audience/properties/{propertyId}` | Rename/reorder / delete |
| GET/POST | `forms/{formId}/audience/people` | List (page ≤ 5,000) / add. The list takes `test=all\|real\|test` (default `all`, anything else is a 400); its total counts only the people kept, and each item has `isTest`. Add takes `isTest` (default `false`) |
| PUT/DELETE | `forms/{formId}/audience/people/{membershipId}` | Edit values / remove from this form only |
| POST | `forms/{formId}/audience/import` | CSV up to 5,000 rows. Rows and summary save together; rejected rows (blank, too long, duplicate identifier) are reported by spreadsheet row, not written. A mapping that names an unknown property or a missing column is a 400. With `isTest`, people new to this form are added as test persons; people already on it get no writes (flag and values) and are counted as skipped |
| POST | `forms/{formId}/audience/links` | Issue one link per real person who does not have one, up to 5,000 real people; test persons get none. The raw token is in this response only. A parallel issue loses on `IX_Links_Membership` and retries once |
| POST | `forms/{formId}/audience/test-submissions` | Test as this person: `{membershipId}` → 201 `{submissionId, token}`. Every call opens a new test submission (see Rules → Test submissions). A disabled form, a form without a definition, or a person not on the form is 404. An open while the form is being deleted is 503; an open after the deletion, or after the form was disabled meanwhile, is 404 |
| POST | `forms/{formId}/audience/test-submissions/{submissionId}/restart` | Start over: `{token}` → deletes that test submission and opens a new one for the same person → 201 `{submissionId, token}`. `token` is the one the test submission was opened with; a missing token is 400, a wrong one 404. A submission that is not a test submission opened as a person is 400; a person removed from the form is 404 and keeps the old one. A Start over while the form is being deleted is 503, and after the deletion 404; either way it opens nothing |
| POST | `forms/{formId}/audience/links/{token}/redeem` | Anonymous, `Throttle(60, 60)` (in-process, per client: its `X-Forwarded-For`, else its connection's address; over the limit is 429, and a request with neither is 403). First open pins the active form definition and the live audience cells on one in-progress submission. A later publish, a new property, or an edited cell does not change that response. The submission it starts shows the person's label (`submitterDisplayId`): the label of the Submitter bound to them, else their identifier. A parallel first open keeps one submission. A later open returns it. Unknown token, a disabled form, or a person removed before the first open is 404 and does not mark the link opened. A deleted submission is 404, unless it was a test. A link whose submission was marked as test, deleted or not, opens a new one: redeem clears that claim itself (only while the link still names that submission) instead of waiting for the outbox. A person removed after open still resumes while the form is enabled. On a one-response form, a person who already has a submission (from on-behalf, or an earlier link) opens that one; it is marked started and gets the live cells if it has none. Another open of the same link holding it past 5 s is 503, as is an open while the form is being deleted; an open after the deletion is 404. Response is `submissionId`, `created`, `accessToken` — not the snapshot |

Permission: `Forms.Edit`, except `PUT audience/settings`, which needs `Tenant.ManageSettings`, the test submission
routes, which need both `Forms.Test` and `Forms.Edit` (a test submission carries the person's identifier and values),
and redeem, which is anonymous.

## Rules

- **Match key.** Locked while any person is on any form's audience (`isLocked`). Saving the current
  key is a no-op and takes no lock. A change soft-deletes members that are on no form, since they were matched under
  the old key.
- **Values by data type.** `number`: invariant, no surrounding spaces, any finite double (`-12.5`, `1e3`, `1e29`). `boolean`: `true` or
  `false`. `date`: `YYYY-MM-DD`. `date_time`: ISO 8601 (`2026-10-05T09:30`, optional seconds and
  offset). `single_choice`: one key from `ChoicesJson`. `multiple_choice`: a JSON array of keys.
  `AllowsOther` accepts any key.
- **Concurrency.** Person creates and imports take a shared tenant lock; a match-key change takes it
  exclusively. The lock is `ITransactionLock` (scope `AudienceMatchKey`, key the tenant id), held until the
  transaction ends. The unique indexes settle create races (a second pass reuses the member; a duplicate
  on one form is a 409). The identifier is checked against the key read under the lock. Two
  updates adding the same cell: the second pass overwrites. Cells are written in property-id order. The people
  list counts and then reads the page without a transaction, so it takes no locks that wait behind writers; when a
  person added in between makes the page longer than the count, the total is raised to cover the rows shown.
- **Test persons.** A test person is on one form only for testing: never issued a personalised link, and shown with
  `isTest` so the list can filter them. The flag belongs to the membership, so the same member can be a test person on
  one form and a real person on another. It is set at add (`isTest` on add or import) and cannot change: to turn a test
  person into a real one, remove them and add them again. An import marked as test never touches a person already on
  the form (neither its flag nor its values), so real people's data stays as it was. A test person locks the match key
  like any other person. Removing a test person removes only that membership.
- **Deletes.** Removing a person deletes the membership and its cells; the member stays. Deleting a
  property soft-deletes its cells in one statement. Reads skip cells of deleted properties.
- **Form deleted.** The `form.deleted` outbox handler soft-deletes the form's cells, memberships
  and properties; tenant members stay. Until it runs, the form's people still count toward the
  match-key lock.
- **Marked as test.** The `submission.updated` outbox handler (`changeKind` `test`) frees the link of a submission
  still marked as test, deleted or not, keeping the token. It locks the submission's row while it clears the link,
  so a mark as real cannot commit between the check and the clear. Redeem frees such a link itself too, so the
  handler only tidies the link early. Marking a submission real is a 409 naming the other one when its link has since opened a
  real submission, and a 400 for a test opened as a person (Test as this person): its answers are a builder's, and
  as a real response it would pass every one-per-person check.
- **Outbox delivery.** Both outbox handlers also run as background jobs (`AudienceDeleteFormAudience`,
  `AudienceReleaseLinkOnTestMark`), each registered with its job and subscription by one `AddOutboxWork` call in
  `AudienceOutboxJobs`. A handler registered inline only stops a host with `Endatix:Outbox:DeliverToJobQueue` on
  from starting. The release job is enqueued only for a test mark, not for
  every `submission.updated`.
- **On behalf.** `POST forms/{formId}/submissions/onbehalf` (`Submissions.CreateOnBehalf`, not this module's routes) freezes a snapshot when `Submitter.ExternalSubjectId` matches a member on a form that has properties. Unknown id, no properties, or no submitter leaves the create unchanged. The snapshot is `source: on_behalf` plus `memberId` and `values[]` beside `variables`. Binding `Member.SubmitterId` is one update that matches only while it is null.
- **Test submissions.** `TestSubmissionOpener` is the one way to build one: a started submission on the form's active
  definition, `IsTestSubmission`, no Submitter and no single-response gate, labelled with the person's identifier,
  and a snapshot of the person's current cells with `source: test` and `memberId`. It never reads or writes `Links`,
  `Member.SubmitterId` or `Submitters`, so testing as a person leaves their personalised link unopened (or still
  pointing at their real response) and their one-response gate as it was. The person is found through
  `LinkOpenGates.PersonAsync`, the query a link open uses, with the tenant and form stated in the read. The values
  freeze with `Submission.FreezeForTest`, which refuses a real submission or a second freeze. Start over needs the
  submission's token, so one builder cannot discard another's test. It finds the person by the snapshot's
  `memberId`, checks they are still on the form, and reads their values and mints the token before it deletes
  anything. An open and a Start over then write through `TestSubmissionWriter`, which takes the form's shared
  `FormSubmissionsLock` (no wait: 503 while the form is being deleted), reads the form again (enabled, with an
  active definition, else 404), and inserts on the definition active now; the locked section reads nothing else, so
  it holds one connection. A Start over deletes the old submission in the same save as the insert: `IsDeleted` is a
  concurrency token, so the delete matches only while the old one is live, and of two Start overs at once one
  replaces it and the other gets 404 and saves nothing. A save that loses a deadlock is 503. A failure to mint the
  token saves nothing and the old test submission stays. Test submissions are left out of exports unless the export asks for them.
- **Two personalised create paths.** On behalf (`source: on_behalf`) is for real responses entered by a trusted
  caller: it resolves and binds a Submitter and counts against one-per-person. Test as this person
  (`source: test`) is for survey builders and does neither. The snapshot shape is the same, since both read cells
  through `SnapshotCells`; `source` on the submission's `personalization` read tells them apart.
- **Links.** The token is stored as a SHA-256 hash. A claimed link resumes without a lock. A first open checks
  `LinkOpenGates` before the lock: a disabled form or a removed person is 404 at once, and a link open never
  writes either. It then takes `ITransactionLock` (scope `AudienceLinkOpen`, key the link id, 5 s wait, then 503) through `LinkOpenLock`,
  and then its form's shared `FormSubmissionsLock` (no wait: 503 while the form is being deleted), on one
  connection: `AppDbContext` joins the audience transaction. Under the lock it re-reads the claim, adopts a submission
  an earlier open left (`FormId` + `AudienceLinkId`) or inserts one, issues the token, and claims with one update
  (`SubmissionId` still null). All of that commits together. A claim lost to a writer outside the lock rolls back
  and resumes that writer's submission. A link claimed before tokens were issued this way gets its first token
  under the lock. A person removed before that claim is not found, and the link stays unopened: every path under the
  lock (insert or take-over) reads the snapshot, which reads the cells before the membership, and a removal deletes
  both in one save. A person removed after open still resumes
  that submission while the form is enabled.
- **One response per person.** On a one-response form the open resolves the person's Submitter before the lock:
  `Member.SubmitterId` when bound, else `Integration` + the identifier (what on-behalf resolves), created if missing
  and bound once. The link's submission carries it with the single-response gate, so `UX_Submissions_RestrictionKey`
  refuses a second one from either path. A refused insert rolls back, and the retry takes the other writer's
  submission over. A create stores its token in its insert, so a take-over's token is never replaced by a later
  save. For email tenants the identifier is matched as first entered, so an on-behalf call with another
  casing is a different Submitter until `Member.SubmitterId` is bound. See [ARCHITECTURE.md → Transaction locks](../../ARCHITECTURE.md#serializing-work-across-requests-transaction-locks).
