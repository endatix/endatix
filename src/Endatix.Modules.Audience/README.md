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

A model change needs a migration in both chains, and follows
[Schema changes during a rolling deploy](../../ARCHITECTURE.md#schema-changes-during-a-rolling-deploy): the previous
release keeps writing while the new one migrates, so a constraint on a new column ships one stable release after the
column. `AudienceMigrationsRollingDeployTests` catches a single migration that adds both.

`AddPropertyValueIsTest` (endatix#1376) copies each membership's test flag onto its cells, then adds
`FK_PropertyValues_Membership` (`TenantId`, `MembershipId`, `IsTest` → `AK_Memberships_TestFlag` on Memberships), which
validates every existing cell. From then on the database refuses a cell whose tenant or test flag differs from its
membership's. An older API version writes `IsTest = false` on a Test participant's new cell and fails with the foreign
key (a 500). The migration adds the column and the key together only because the module was a preview until then
(`personalization-module`, off by default), so no production host wrote `PropertyValues` before it; a host that turned
the preview on sees that 500 while the upgrade rolls out (endatix#1389). The column add is metadata-only on
PostgreSQL 11+ and SQL Server Enterprise/Azure SQL, and a table rewrite on SQL Server Standard; the index rebuilds
block writes to `PropertyValues` while they run. This is the module's only foreign key; nothing hard-deletes a
membership. A future purge must delete its cells first and add an unfiltered index on (`TenantId`, `MembershipId`,
`IsTest`) before it: the key's checks also see soft-deleted cells, so the filtered `IX_PropertyValues_Cell` cannot serve
them, and each membership delete scans `PropertyValues` (22 ms per membership at 500,000 cells on PostgreSQL 17, every
page on SQL Server 2025).

## Schema

Database schema: `audience`

| Table | Purpose |
|-------|---------|
| `Settings` | One row per tenant: match key (`email` / `external_id`). No row means `email` |
| `Properties` | Property definitions per form (`VariableName` immutable). Choice types need `ChoicesJson`, a JSON array of distinct keys; other types take no choices |
| `Members` | One person per tenant + match key. `Identifier` keeps the value as entered; `NormalizedIdentifier` is the unique match key. Emails must be one valid address and use the same normalizer as app users; external ids keep their case. On SQL Server the match key uses the binary collation `Latin1_General_100_BIN2`, so it compares case-sensitively as on PostgreSQL |
| `Memberships` | Person on one form. `IsTest` marks a test person; it is set when the person is added and never changed |
| `PropertyValues` | Property cells on a membership. Each value is at most 4,000 characters and must fit the data type (see below). A stored value is never empty: saving `""` clears (soft-deletes) the cell. `IsTest` is the membership's test flag, copied when the cell is written (it never changes on either), so coverage counts test and real cells from `IX_PropertyValues_Property` (tenant, Property, test flag) with no join. `FK_PropertyValues_Membership` ties the cell's tenant and test flag to its membership; cells are built only through `PropertyValueCreateArgs.For(membership, …)` |
| `Imports` | One row per confirmed CSV import: counts, file name, who ran it |
| `Links` | One hashed token per membership (`IX_Links_Membership`). `SubmissionId` is set once, on first open |

## API (directory)

| Method | Route | Notes |
|--------|-------|-------|
| GET/PUT | `audience/settings` | Tenant match key and `isLocked` |
| GET/POST | `forms/{formId}/audience/properties` | List / create |
| PATCH/DELETE | `forms/{formId}/audience/properties/{propertyId}` | Rename/reorder / delete |
| GET/POST | `forms/{formId}/audience/people` | List (page ≤ 5,000) / add. The list takes `test=all\|real\|test` (default `all`, anything else is a 400) and `search` (people whose identifier contains it, ignoring case: `ILIKE` on PostgreSQL, the column's collation on SQL Server, case-insensitive by default; `%`, `_` and `[` match literally; blank or whitespace is no search; longer than an identifier after trimming is a 400); its total counts only the people kept, and each item has `isTest`. Add takes `isTest` (default `false`). Each list item has `notFilledIn`: how many of the form's Properties the person has no value for (the list only; the single participant, create and update responses leave it out) |
| PUT/DELETE | `forms/{formId}/audience/people/{membershipId}` | Edit values / remove from this form only |
| GET | `forms/{formId}/audience/people/{memberId}` | One participant, by member id (not membership id): the list item's fields plus `submissions` and `submissionTotal`. The submissions are theirs on this form: the one their live link opened, their Submitter's, and Snapshots that name them and are either a test submission or an on-behalf create (`PersonalizationSnapshotJson.NamesParticipant` plus `IsOnBehalf`, exact lookups of the Snapshot's top-level `memberId` and `source` keys). On-behalf is included through the Snapshot so a create whose best-effort Submitter bind did not run is still theirs. Newest first (created, then id), at most 50; `submissionTotal` counts them all. Deleted ones are left out. Each has `id`, `createdAt`, `completedAt`, `isComplete`, `status`, `isTest`, and `snapshotSource` (`link`, `on_behalf`, `test`), left out without a Snapshot. Not on this form, removed, or another tenant's is 404 |
| GET | `forms/{formId}/audience/variables` | The form's Variables for the designer, in SurveyJS's variable presets shape (3.1.1+): `{ variablePresets: { definition, presets, totalPresets } \| null, coverage, editing, readOnlyPresets }`. `definition` is survey JSON with one top-level question per Property in tab order (see Rules → Variables); `presets` has up to 20 participants, Test participants first and then real ones (see Rules → Variables), each with `name` their identifier, `description` `Test participant` or `Participant`, and `variables` their live values typed by `AudienceValueJson.Typed`, the function the Snapshot uses. `totalPresets` counts every participant on the form, test and real. `readOnlyPresets` names the real participants among the presets, in list order, so the designer locks them (`onVariablePresetEditing`); it is empty when the form has no Properties. `variablePresets` is null when the form has no Properties. `coverage` counts the form's test and real participants (`participants`, also when it has no Properties) and, per Property in tab order, those with a value (`properties[]`: `variableName`, `filledTest`, `filledReal`, zero when nobody filled it); `editing` (`allowAdd`, `allowEdit`, `allowDelete`) is `true, true, false` (Test participants are added and edited from the designer, none deleted), all false when the form has no Properties. Another tenant's or a missing form is 404 |
| GET | `forms/{formId}/audience/variables/participants/{memberId}` | One participant's preset for Preview as, by member id (not membership id): `{ name, description, variables }`, built as a preset in the variables read (name = identifier, description `Test participant` or `Participant`, values typed by `AudienceValueJson.Typed`, one entry per live cell of a current Property). Any participant on the form, real or test. Not on the form, removed, or another tenant's form is 404 |
| PUT | `forms/{formId}/audience/variables/test-participants` | Save the designer's edited presets: `{ presets: [{ name, variables }] }`, the whole list (≤ 100), each `name` an identifier. Returns the variables read again (200). See Rules → Variables write-back. A change to a real participant (one sent back unchanged is left alone), an identifier the match key refuses, a value its Property refuses, or one identifier given twice is 400 and writes nothing; a parallel add of the same new participant is 409 |
| POST | `forms/{formId}/audience/import` | CSV up to 5,000 rows. Rows and summary save together; rejected rows (blank, too long, duplicate identifier) are reported by spreadsheet row, not written. A mapping that names an unknown property or a missing column is a 400. With `isTest`, people new to this form are added as test persons; people already on it get no writes (flag and values) and are counted as skipped |
| POST | `forms/{formId}/audience/links` | Issue one link per real person who does not have one, up to 5,000 real people; test persons get none. The raw token is in this response only. A parallel issue loses on `IX_Links_Membership` and retries once |
| POST | `forms/{formId}/audience/test-submissions` | Test as this person: `{membershipId}` → 201 `{submissionId, token}`. Every call opens a new test submission (see Rules → Test submissions). A disabled form, a form without a definition, or a person not on the form is 404. An open while the form is being deleted is 503; an open after the deletion, or after the form was disabled meanwhile, is 404 |
| POST | `forms/{formId}/audience/test-submissions/{submissionId}/restart` | Start over: `{token}` → deletes that test submission and opens a new one for the same person → 201 `{submissionId, token}`. `token` is the one the test submission was opened with; a missing token is 400, a wrong one 404. A submission that is not a test submission opened as a person is 400; a person removed from the form is 404 and keeps the old one. A Start over while the form is being deleted is 503, and after the deletion 404; either way it opens nothing |
| POST | `forms/{formId}/audience/links/{token}/redeem` | Anonymous, `Throttle(60, 60)` (in-process, per client: its `X-Forwarded-For`, else its connection's address; over the limit is 429, and a request with neither is 403). First open pins the active form definition and the live audience cells on one in-progress submission. A later publish, a new property, or an edited cell does not change that response. The submission it starts shows the person's label (`submitterDisplayId`): the label of the Submitter bound to them, else their identifier. A parallel first open keeps one submission. A later open returns it. Unknown token, a disabled form, or a person removed before the first open is 404 and does not mark the link opened. A deleted submission is 404, unless it was a test. A link whose submission was marked as test, deleted or not, opens a new one: redeem clears that claim itself (only while the link still names that submission) instead of waiting for the outbox. A person removed after open is 404 too, and nothing is written: their access token is refused. On a one-response form, a person who already has a submission (from on-behalf, or an earlier link) opens that one; it is marked started and gets the live cells if it has none. Another open of the same link holding it past 5 s is 503, as is an open while the form is being deleted; an open after the deletion is 404. Response is `submissionId`, `created`, `accessToken` (deprecated: the submission's opaque SubmissionToken, which follows the form's privacy), `submissionAccessToken`, `submissionAccessTokenExpiresAt` — not the snapshot. `submissionAccessToken` is a signed submission access token with `submit` (fill in and complete), sent as `tokenType=AccessToken`, on a public or private form, with no login. It lasts the form's response-token lifetime (form, else tenant). A signed token always expires: a lifetime that never expires, or one past 60 days, gets the 60-day cap (`ISubmissionAccessTokenService.MAX_EXPIRY_MINUTES`); a tenant with no settings gets `TenantSettings.DefaultSubmissionTokenExpiryHours` (24 h). It follows the submission token's after-completion rule: once the submission is complete or screened out it is refused (400, `token_revoked`), unless the tenant keeps tokens valid after completion. It is bound to the link: after a reissue, or once the person is removed from the form, it is refused (400, `token_revoked`) within 1 minute (a cached check); a disabled or deleted form refuses it at once (404). `accessToken` is removed in a later release |

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
  on one form is a 409). Two imports of one form that add the same new people in different orders can deadlock on
  those indexes; the victim takes the same second pass. The identifier is checked against the key read under the
  lock. An edit of a participant's
  values and their removal both take that participant's stripe of the form's people lock exclusively, then the participant's
  lock (`MembershipLock`: scope `AudienceMembership`, key the membership id), and read the membership again under
  them: an edit that waited for a removal is a 404, and a removal that waited for an edit soft-deletes the cells it
  wrote, so a removed participant never keeps a live value (endatix#1361). The CSV import writes values of people already
  on the form in one transaction, so it takes every stripe of the form's lock shared (scope `AudienceFormPeople`),
  after the tenant lock and before it reads the form's people. The form's lock is 16 stripes, keyed
  `{formId}:{stripe}`: an edit and a removal hold their participant's stripe (from a hash of the membership id)
  exclusively, an import holds all 16 shared, in ascending order within one 5 s wait. So imports of one form run
  together (endatix#1383), every import excludes every edit and every removal on the form, and edits and removals
  run together unless they share a stripe, when one waits for the other for the length of that section. Lock queues
  are first come, first served, so an import that asks for the stripe of an edit or a removal queued behind another
  import waits until that writer gets it or gives up. A removal that waited for an import soft-deletes the cells it
  wrote, and an import that waited for a removal adds the participant again. An edit waits for an import on its stripe
  (endatix#1384): without that stripe, the edit's insert waited on the import's uncommitted cell for the 30 s command
  timeout while holding the participant's lock, and then failed with a 500. An edit that loses a cell race to a writer
  that skips the locks takes a second locked pass, which overwrites. Every wait is 5 s, then 503 whose `detail` is the
  message of the lock that waited: "This participant is being changed. Try again." for the participant's, "People on this form are
  being imported or changed. Try again." for the form's (an edit or a removal behind an import or another edit or removal on its
  stripe, or an import behind an edit or a removal). On PostgreSQL two keys of one scope can
  share a `hashtext` lock, so unrelated people or forms then wait for each other, within that bound. Cells are written in property-id order. The people
  list counts and then reads the page without a transaction, so it takes no locks that wait behind writers; when a
  person added in between makes the page longer than the count, the total is raised to cover the rows shown.
- **Test persons.** A test person is on one form only for testing: never issued a personalised link, and shown with
  `isTest` so the list can filter them. The flag belongs to the membership, so the same member can be a test person on
  one form and a real person on another. It is set at add (`isTest` on add or import) and cannot change: to turn a test
  person into a real one, remove them and add them again. An import marked as test never touches a person already on
  the form (neither its flag nor its values), so real people's data stays as it was. A test person locks the match key
  like any other person. Removing a test person removes only that membership.
- **Deletes.** Removing a person deletes the membership and its cells; the member stays. Deleting a
  property soft-deletes its cells in one statement. Reads skip cells of deleted properties. These take no participant lock: an
  edit in flight can still write a cell after them, and it stays out of every read with its Property (a form's
  deletion deletes its Properties too).
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
- **Participant on a form.** `FormParticipants` is the one rule: `ByMember` finds a member's live membership on the tenant's form, and `Of` joins memberships to their members by key, with the member's tenant filter dropped by name (the membership is the tenant's; with the filter, PostgreSQL can scan the tenant's members per membership). The participant panel (`ParticipantReader`), Preview as (`AudienceVariablesReader.ReadOneAsync`), opening a test submission by member id (`TestSubjects`) and a link open (`LinkOpenGates.PersonAsync`) all go through it. `ParticipantValues` is the one values read (live cells by property id), used by the people list, the participant panel and Preview as.
- **Match-key Property.** The form's `text` Property whose variable name is the tenant's match key (`email` or `external_id`, `MatchKeyProperty.Of`, the key read in the same statement through `IdentifierKindReader.Stored`, email when the tenant has no settings) holds each participant's identifier wherever values are read: the people list (`values`, so `notFilledIn` counts it as filled), the participant panel and the create and update replies, Preview as and the variables read (`ParticipantValues`, which takes the Property from its caller), Coverage (filled for every test and real participant) and the Snapshot of new submissions (`SnapshotCells`, given the form and identifier by the open). It is derived, never stored, and the identifier wins over a cell stored for that Property; Snapshots already written are not changed (endatix#1365). Only a `text` Property: the identifier is the text the participant was added with, and `Typed` would turn an external id `007` in a `number` Property into `7`, or put an address in a date or a choice its rules refuse, so a Property of another data type named `email` or `external_id` is an ordinary one. Writes follow the read-only real presets (endatix#1359): a value for the match-key Property that is the identifier or empty is left out and stores nothing, and any other value is refused, naming the Property ("Email is the participant identifier and cannot be changed."), by one rule (`MatchKeyProperty.WriteError`) in the participant panel (`CreatePersonCommand`, `UpdatePersonCommand`: a 400), the designer's presets (`PresetReconciler`, before the first write: a 400) and the CSV import (`ImportRowReader`: the row is rejected).
- **Variables.** `AudienceValueJson` is the one place a cell becomes the JSON a survey reads: the Snapshot (`variables` and `values[]`) and the designer's presets both call `Typed`, so a value previewed for a Test participant is the value frozen on their next test submission. A number that is not finite stays a string, because JSON cannot carry it. `VariableDefinition` is the only data type → question mapping: `text` and `date_time` → `text` (no `datetime-local`, which drops the stored offset), `number` → `text` with `inputType: number`, `date` → `text` with `inputType: date`, `boolean` → `boolean`, `single_choice` → `dropdown`, `multiple_choice` → `checkbox`; choices are the keys in order, and `AllowsOther` adds `allowCustomChoices` (a `multiple_choice` becomes a `tagbox`), which stores free text as the Audience does. `AudienceVariablesReader` reads the Properties, the first 20 participants (Test participants first, each group by when they were added: one ORDER BY IsTest DESC, Id), and their live cells (by the membership ids just read and the live Property ids, one table and a seek per participant on `IX_PropertyValues_Cell`; an `EXISTS` on Properties there scanned every cell of the tenant), and the coverage counts: participants by test flag, then filled cells in one GROUP BY on Property and the cell's test flag over the form's live Properties (one index range on `IX_PropertyValues_Property` each), with no join between cells and memberships, which timed out at 30 s in CI at 5,000 × 50 (PostgreSQL looped the memberships over the cells). The statements share no snapshot, so each count is clamped to `0..participants`. The membership filter has no EXISTS on Members, as the people list does: the only Member delete (`RetireMembersAsync`, on an identifier-kind change) runs only while the tenant has no live memberships. A cell exists only while it holds a value, so a count of cells is a count of people filled in. Up to six statements; it stops after the participant count when the form has no Properties or no participants. At 5,000 Test and 5,000 real participants × 50 Properties (495,000 cells) a warm read takes a p50 of about 55 ms on PostgreSQL and 150 ms on SQL Server in the integration test (fresh statistics; 190 and 140 ms stale), in the same statements as a small form (endatix#1376). `ReadOneAsync` reads one participant, real or test, with the Properties and their live cells, in four statements (the participant, the match-key Property, the Properties, their cells); it stops after the first when they are not on the form. The presets list those Test participants by identifier, then the real ones by identifier, so a form with no Test participants still previews as its first 20 participants. `totalPresets` counts every participant, test and real (CSV import can add up to 5,000). A choice key cannot contain `|`, because SurveyJS splits a choice written as `value|text`.
- **Variables write-back.** `SaveTestParticipantValues` matches each preset to a participant on the form by identifier, normalized by the tenant's match key (an email ignores case). `PresetReconciler` plans every write before the first one: a new identifier is added as a Test participant with its filled values; a Test participant gets only the values that differ from their cells, compared through `Typed` then `Untyped` so `12.50` equals `12.5`, a variable left out clearing its cell; values that are equal are not rewritten (a number keeps the digits it was stored with), which makes a replay write nothing; a change to a real participant refuses the request ("Real participants are read-only."), while a real participant's preset sent back unchanged is left alone, because the designer sends its whole list, read-only presets included; a variable that is no Property is ignored; participants left out of the list are untouched (nothing is deleted). Values are checked by `PropertyValueRules` only where they will be written, so an unchanged value stored before a rule changed is kept, also when another value of that participant changes. A variables object that repeats a name is a 400. Writes go through `CreatePersonCommand` (`IsTest`) and `UpdatePersonCommand`, so their locks, races and rules apply; they stop at the first failure, and a replay finishes the rest. The reply's presets are the first 20 participants (Test participants first) plus every one the request named, so a preset saved past the first 20 is in it. `AudienceValueJson.Untyped` turns a survey value into cell text: null is empty, a string its text, a number one form (`R`, so `1.50` and `"1.5"` become `1.5`), anything else compact JSON with non-ASCII text kept (an emoji is still escaped). `Untyped(t, Typed(t, v)) == v` for a value in that form, and `Typed(t, Untyped(t, x)) == Typed(t, x)` always.
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
  both in one save. A person removed after open is 404 on redeem too, and their
  access token is refused (see the redeem route above).
- **One response per person.** On a one-response form the open resolves the person's Submitter before the lock:
  `Member.SubmitterId` when bound, else `Integration` + the identifier (what on-behalf resolves), created if missing
  and bound once. The link's submission carries it with the single-response gate, so `UX_Submissions_RestrictionKey`
  refuses a second one from either path. A refused insert rolls back, and the retry takes the other writer's
  submission over. A create stores its token in its insert, so a take-over's token is never replaced by a later
  save. For email tenants the identifier is matched as first entered, so an on-behalf call with another
  casing is a different Submitter until `Member.SubmitterId` is bound. See [ARCHITECTURE.md → Transaction locks](../../ARCHITECTURE.md#serializing-work-across-requests-transaction-locks).
