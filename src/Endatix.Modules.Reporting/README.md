# Endatix.Modules.Reporting

Reporting bounded context for BI-ready survey exports.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Modules.Reporting
```

## Recommended Usage:

For running and hosting the Endatix Platform, **Endatix.Api.Host** is the recommended main package as it simplifies the installation and setup process.

```bash
dotnet add package Endatix.Api.Host
```

The module is registered automatically by `EndatixBuilder.UseDefaults()` — no host code change is needed to get it, and none is needed to keep it off. Availability is decided at runtime by the feature flag (see [Registration](#registration)), not by whether the package is present.

## Module layout (Modulith)

Packaging (what is public vs what stays in the module): [`ARCHITECTURE.md`](../../ARCHITECTURE.md#module-packaging-contracts-vs-domain).

Integration pipeline state lives on `FlattenedSubmission` in the `reporting` schema. A future reporting read API can expose `SubmissionIntegrationSnapshotDto` per submission without touching core `Submission` list endpoints.

## Schema

Database schema: `reporting`

| Table | Purpose |
|-------|---------|
| `FormSchemas` | Compiled form schema (`FlatteningMap` + `Codebook` + `SearchKeys`) per tenant + form |
| `FlattenedSubmissions` | Flat submission answers aligned to form schema (`DataJson`), and their search copy (`SearchJson`) |
| `ExportFormats` | Tenant export rows (CSV, JSON, Excel/XLSX, codebook) |
| `SurveyTypeExportMappings` | Allowed export formats per survey type (with optional default and tenant fallback) |
| `SubmissionViews` | Saved submissions grid view per tenant + form + owner (`OwnerUserId` `0` = shared form view) |

`FlattenedSubmissions.SubmissionId` is the row key. Backfill and the completion outbox both update that row; they do not insert a second one.

`POST …/reporting/submissions/backfill` pages one scope per call. Omitted `completionScope` is `completed`. `incomplete` flattens drafts. The outbox still skips drafts. A processed row is skipped when the submission's `ModifiedAt ?? CreatedAt` is not newer than the row's `SourceModifiedAt` (the stamp of the version that was flattened), unless `force` is true. The row's own `ModifiedAt` is not used: the worker sets it after reading the submission, so a save during flattening would look older and never be refreshed. Rows without `SourceModifiedAt` (processed before it existed) are reprocessed once. A flatten whose submission is gone removes an existing row only when the submission was soft-deleted in the same tenant and form; otherwise it throws, so the outbox retries and backfill reports it as failed. With background jobs on (`Endatix:Outbox:DeliverToJobQueue`), the flatten job retries instead and then dead-letters. A submission or its form deleted after the flatten read the submission loses the row the flatten wrote, hard-deleted as its deletion sync removes it: whether the row ended processed or skipped, lost to a newer flatten's write, or the flatten failed or was cancelled after marking it processing (its schema gone with the form included). Such a failure then ends the flatten as a success, since there is nothing left to retry; a cancelled flatten still ends cancelled. Export drops soft-deleted submissions.

Flattens of one submission can run out of order, as retries or as jobs, so each write is guarded by `SourceRevision`: the submission's `Revision` the row was written from. A write lands only when the row has no `SourceRevision` or one not newer than the write's, so an older flatten finishing last changes nothing, and a forced rebuild at the same revision still lands. `SourceRevision` orders writes; `SourceModifiedAt` only tells backfill whether a row is stale. A draft's saves raise no events and so share one revision: among them, the last flatten to write wins.

### Saved submissions grid view

`GET forms/{formId}/reporting/submission-view` (`submissions.view`) returns the form's shared view and its `version`, or `version` `0` and `{"v":1,"columns":[]}` when nobody saved one. `PUT` (`forms.edit`) takes `{ version, settings }`: version `0` creates the view, the current version replaces it, and any other version is 409. Both are 404 for a form that is not in the caller's tenant.

- **Settings v1:** `{"v":1,"columns":[{"key","visible","width"}],"pageSize","sort":{"key","direction"}}`. `columns` lists only the columns someone changed, in grid order. Unknown properties are stored as written. Malformed settings, or more than `SubmissionViewSettings.MaxJsonLength` characters, are 400.
- **Reads drop stale columns.** A column whose key is neither in the form's compiled `FlatteningMap` nor one of the grid's own columns (`SubmissionViewColumns.MetadataKeys`) is left out of the response. The stored row keeps it. A save's response is filtered the same way. Before the form's schema is compiled there is nothing to judge the columns by, so they come back as stored.
- **Concurrency.** `Version` is an EF concurrency token (not `xmin`), and `(TenantId, FormId, OwnerUserId)` is unique. Two saves from the same version cannot both win: the losing update changes no row and the losing create hits the index; both are 409.
- **Owner `0`, not `NULL`:** SQL Server allows one `NULL` in a unique index, PostgreSQL many. Personal views will use the user id in the same table.
- `form.deleted` removes the form's views together with its schema and flattened rows.

### FormSchema compile modes

On every compile path (outbox `form.definition.updated`, manual `POST .../reporting/compile-schema`):

| Mode | When | Behavior |
|------|------|----------|
| **Replace** | Form has **0 real** submissions (`IsTestSubmission == false`) and the definition is not older than the schema's revision; or `replace=true` | Rebuild FlatteningMap + Codebook from the definition only, at its revision. After save, hard-delete that form’s `FlattenedSubmissions` rows (test flatten debris), unless a flatten asked for the rebuild (see below). |
| **Merge** | Form has **≥1 real** submission, or the definition is older than the schema's revision and a submission on it is flattened | Append-only merge: retain historical columns, questions, and choice-catalog values. |

On a form with no real submissions, a compile for a definition older than the schema's revision, without `replace=true`, leaves the schema as it is: compiles run as jobs that can be reordered or retried, so the older definition's compile may run after the newer one's committed, and merging it would bring back questions the newer definition removed and put its labels and locales back.

Test submissions alone do **not** force merge. This is a defensive bridge until Form Publish makes publish the controlled compile moment.

Rebuilds of one form run one at a time. Each reads the schema, chooses its mode, compiles and saves in one transaction holding the form's transaction lock (`ITransactionLock`, scope `TransactionLockScopes.ReportingFormSchema`, key `{tenantId}:{formId}`; on PostgreSQL `pg_advisory_xact_lock(scope, hashtext(key))`, on SQL Server `sp_getapplock` on `{scope}:{key}`), so a rebuild that waited decides on what the one before it saved. Without it, two rebuilds that read the schema at once each saved only their own columns, and the revision still looked current.

- **Decided under the lock.** The mode is chosen from the schema as read under the lock. A definition older than the schema never replaces it, so it never drops a newer definition's columns. It merges in the columns the schema lacks for a flatten of a submission on it, or when the form has real submissions, and otherwise leaves the schema as it is. Whether the form has real submissions is read under the lock (an `EXISTS`), and only when it decides between a replace and a merge.
- **A waiter that finds the work done stops.** A flatten that needs a definition's columns stops when the schema is at that definition's revision or newer and has them. A compile after a definition changed still compiles, as the definition may have been edited in place under the same id, but saves nothing, and clears no rows, when the result is what is stored.
- **A flatten's replace keeps the flattened rows.** The submission being flattened already has its row, as may others flattened at the same time; clearing them would leave those submissions with no row. Test rows flattened on an older definition stay until the next compile that replaces and clears.
- **What runs under the lock:** the schema read, the mode choice, the real-submission check when it decides the mode, the compile, the save, and for a replace the clearing of the flattened rows. The definition read, the definition's columns, and the form-deleted check run outside it. After the commit the definition is read again, and a rebuild that compiled JSON an in-place edit has since replaced runs once more.
- **The wait is bounded at 10 seconds** (`lock_timeout` on PostgreSQL, `@LockTimeout` on SQL Server, for the lock alone, under the 30-second default command timeout). A wait that runs out throws `TransactionLockTimeoutException`; a job retries it, and a backfill reports the submission as failed and processes it on its next run.
- **SQL Server:** `SqlServerTransactionLock`. A lock that times out leaves the transaction open on SQL Server, unlike PostgreSQL; the rebuild rolls it back either way.

The schema is read untracked, and tracked only while it is saved, so a long-lived scope such as a backfill sees what other scopes committed, and never what a rolled-back rebuild of its own saved. Whether the schema has an older definition's columns is worked out once per scope for each saved version of the schema (`FormSchemaCoverage`).

A flatten rebuilds the schema only for a submission on a definition newer than the schema's revision, or on an older one whose columns the schema lacks (a replace dropped them). A merge keeps every column, so an older definition is normally already in the schema; rebuilding from it would save the schema again for every such submission and put the older definition's labels and locales back.

A form deleted after a compile read it loses the schema that compile wrote, hard-deleted as its deletion sync removes it.

### Submission Grid columns

`GET forms/{formId}/reporting/columns?locale=` (`submissions.view`) lists the columns of the form's compiled schema in flattening-map order, for the Submission Grid's headers, column picker and answer filters. Each column has its key and kind, a title in `locale` (falling back to the default), the SurveyJS question type, an answer `filterType` (`text`, `date`, `datetime` or none) and its page and panel. The filter type is worked out at read time from the column kind and the codebook question's type and `inputType`, so a schema needs no recompile for it and export is unchanged. A form without a compiled schema answers `schemaReady: false` with no columns.

`readiness` counts the form's live submissions (`submissions`, `completedSubmissions`, `inProgress`) from core and its flattened rows by status (`prepared`, `pending` including `not_processed`, `failed`, `skippedInProgress`). An out-of-date count is not there yet.

Codebook question entries carry the question's place in the form: `pageName`, `pageTitle` (a locale map like `title`, left out when the page has no title) and `parentPanelTitle` (default locale). They are written at compile time, so a schema compiled before these keys existed has no page until its next compile. Codebook `version` stays 1.

### Submission Grid rows

`GET forms/{formId}/reporting/submissions` (`submissions.view`) lists one page of a form's submissions for the Submission Grid. It takes the same filters, sort and paging as `GET forms/{formId}/submissions` (both build their query with `ListByFormIdRequest.ToQuery()`), so the rows, their order, the 404 for an unknown form and the tenant scope match the submissions list. `columns` is a comma-separated list of column keys from `GET forms/{formId}/reporting/columns`: at most 500 keys, each up to 512 characters. A key that contains a comma cannot be requested.

Each row carries the list fields plus:

- `rowState`, from the submission's flattened row: `prepared` (processed from the submission's current `ModifiedAt ?? CreatedAt`, and has data), `stale` (processed from an older version, or has no data), `pending` (`not_processed`, queued or being prepared), `failed`, `skipped` (in progress when the flatten ran) or `none` (no flattened row).
- `answers`, the requested columns that have a value.
- `answersSource`: `prepared` when the row is `prepared`, otherwise `raw`. Raw answers come from the submission's own `jsonData`, which is keyed by question name, so only simple question columns can be found there. Choice indicators, matrix cells and other flattened keys are left out until the row is prepared.

Without `answer=` filters, the page comes from the core list; its flattened rows are then read by id, with `DataJson` only when `columns` is set. With them, see [Answer filters on the submissions grid](#answer-filters-on-the-submissions-grid).

`submitterProfile.{field}` filters, on this list and on the submissions list, read the profile snapshot with `JSON_VALUE` on SQL Server. They compare under the `Latin1_General_100_BIN2` collation, so matching is case-sensitive as on PostgreSQL, whatever the database collation. Unlike PostgreSQL, SQL Server ignores trailing spaces when it compares strings, and `JSON_VALUE` returns NULL for a value over 4,000 characters, so such a value never matches `=` and always matches `!=`.

### Submitter profile fields

`Customizations:Reporting:SubmitterProfileFields` lists the keys of `Submission.SubmitterProfileSnapshot` (the claims `Endatix:Submitter:ProfileSnapshotFields` captures) that prepared submissions carry. It is empty by default, and empty adds no column: schemas, flattened rows and exports stay as they were.

```json
"Customizations": {
  "Reporting": {
    "SubmitterProfileFields": ["email", "department"]
  }
}
```

- **Columns.** Each listed field is a `submitter_<field>` column of kind `SubmitterProfile`, data type `string`, label `Submitter <field>`. A compile adds only the definition's columns. Like audience columns, the submitter columns join the schema when a flatten finds them missing: under the form's lock, appended at the schema's own definition revision, so listing a field never rebuilds a schema from an older definition and its labels stay the current ones. A schema at its column limit is flattened without them, with a warning, instead of failing. The limit is checked once for a flatten's audience and submitter columns together, so near the limit a listed field can also hold back the audience columns a response carries. A replace that clears the flattened rows drops them until the next flatten adds them back. A question already named `submitter_<field>` keeps the key, and the profile value is not exported: the schema keeps the first column a key had, the same rule that lets a question win over an `audience_` key.
- **Values.** A flatten copies only the fields listed now, as text. A field the snapshot lacks, or one no longer listed whose column a merge kept, is null.
- **Export.** The submitter columns close the row after the audience columns, under their keys as stored (no alias profile or key separator), so listing a field moves no existing column. Shoji writes them as text variables.
- **Freshness.** A `submission.updated` event whose `changeKind` has `submitter` flattens the row again, as `answers` and `definition` do.

The snapshot holds personal data: list only the fields reports need.

To roll out a field:

1. **Deploy this version to every node first.** A node on an older version drops a column kind it does not know, so while a rolling deploy runs it writes rows, and can save a schema, without the submitter columns.
2. **Set the option with exactly the names in `Endatix:Submitter:ProfileSnapshotFields`**, in the same case: a field the snapshot does not capture is always null. For saved grid views migrated from the Hub's legacy grid to keep their submitter columns, the Hub's `ENDATIX_SUBMITTER_GRID_PROFILE_FIELDS` must name the same fields, in the same case.
3. **Restart.** The option is read once at startup.
4. **Run a forced backfill per form** (`POST …/reporting/submissions/backfill` with `force`). The first flatten adds the columns; the backfill fills the rows flattened before the field was listed, or during the deploy.

### Search copy (`SearchJson`)

Answer filters read a small copy of each processed row, not its whole `DataJson`: scanning `DataJson` on a 1,000-question
form costs seconds at 50k rows (PostgreSQL detoasts every document; SQL Server's `JSON_VALUE` cost grows with the key's
position). `FormSchemas.SearchKeys` decides which keys the copy holds, as a JSON array of
`{ key, source: "auto" | "user", filterType, addedAt, revision }`.

- **At most 64 keys** (`FormSearchKeys.MaxKeys`). Above that the copy passes PostgreSQL's 2 KB TOAST threshold on fully
  answered forms and costs about 3× more.
- **Automatic keys, up to 50.** Each compile appends the schema's searchable columns, in schema order, while fewer than
  50 keys are `auto`: a question's own value column with a submissions-grid filter type (`SubmissionGridFilterTypes`),
  whose question is not `readOnly` in the definition (a read-only page or panel makes its questions read-only). The other
  14 slots (`FormSearchKeys.MaxUserKeys`) are for keys people add (`source: "user"`, see below).
- **Append-only for the compile.** A key stays while an answer filter can read its column, so rows written from it stay
  valid; a replace compile that drops the column, or a question no filter supports any more, drops the key. A key whose
  question changed filter type takes the new one, whoever added it (a person's key on a read-only question too). Only a
  person removes a key otherwise, and only one they added.
- **Written with the answers.** A flatten writes `SearchJson` in the same conditional update as `DataJson`: the search
  keys answered with a scalar, each as text — lowercased for `text`, `yyyy-MM-dd` for `date`, `yyyy-MM-ddTHH:mm:ss` for
  `datetime` (read by ISO shape; a time zone is ignored; another shape is kept as entered and never matches a range).
  Numbers and booleans become their JSON text; arrays and objects are left out. A skipped row has no copy.
- **Rebuilt in SQL.** `SearchCopyRebuilder` rewrites a form's copies from `DataJson` in batches of 1,000 rows by
  submission id, one statement per batch, without flattening again. Every compile runs it after its commit, whether it
  saved the schema or left it as it was: it rewrites only rows behind the schema's revision (below), so it catches up
  rows the compile's new keys left behind, and rows a rebuild before it did not finish, and costs one empty batch when
  none are. Adding a person's key asks for it in the background (below). The SQL normalises dates as the flatten
  does, and `SearchCopyRebuildIntegrationTests` checks both write the same copies. Lowercasing differs in one place:
  the flatten uses .NET's invariant rules, the SQL the database's `lower()` / `LOWER`, which follow its collation.
  Under a `C` or POSIX PostgreSQL collation the SQL lowercases ASCII only, so non-ASCII text a rebuild wrote can miss
  a contains filter until the row is flattened again.
- **Search keys revision: rows that have not caught up count as not checked.** `FormSchemas.SearchKeysRevision` moves
  forward whenever existing rows need a value they were not written with (a key added for a column they hold, a
  person's key, a changed filter type), and that key's `revision` takes the new value. A flatten stamps its row with the
  revision of the keys it read, and the rebuild stamps each row it rewrites
  (`FlattenedSubmissions.SearchKeysRevision`). The rebuild rewrites only rows stamped older than the schema, and rows with
  no copy whatever their stamp, so running it again finishes a run that was cut off, and a rebuild that read older keys never overwrites a newer row. A flatten
  that read the keys before a change and writes after that change's rebuild passed its row reads the schema's revision
  after its write, and runs the rebuild when it wrote an older one. A key is pending while some live processed row is
  stamped below its revision or has no copy. A filter
  on keys `K` checks a row only when its stamp is at least `FormSearchKeys.RevisionNeededFor(K)`
  (`SearchCopyRows.HoldingSearchKeys`); any other row counts as not checked, not as not matching. Removing a key needs
  no new revision: filters stop reading it at once, and its stale values are never read.
- **Filters.** `IRelationalJsonObjectKeyFilter.WhereKeyMatches` (contains, lowercased pattern as a parameter) and
  `WhereKeyAtLeast` / `WhereKeyBefore` (ranges compared by code point, on values that start with `yyyy-MM-dd`, so a
  value kept as entered never falls in a range). SQL Server reads keys with
  `JSON_VALUE(… RETURNING nvarchar(max))`, because lax `JSON_VALUE` returns NULL above 4,000 characters, and compares
  lowercased values, so a case-sensitive database collation still matches without case.

### Answer filters on the submissions grid

`GET forms/{formId}/reporting/submissions` takes repeated `answer={operator}:{key}:{value}` parameters, AND-combined
with each other and with the list's metadata filters (`SubmissionGridAnswerSearch`).

- **Operators.** `contains` on `text` search keys (case-insensitive, 1–200 characters); `from` and `to` on `date` and
  `datetime` keys, each a calendar day `yyyy-MM-dd`, both inclusive. The key is percent-encoded (`encodeURIComponent`),
  because a column key can contain `:`. At most 10 filters.
- **Errors.** A malformed filter is 400 on `answer`. A key that is not one of the form's search keys
  (`answer_key_not_filterable`), or an operator its filter type lacks (`answer_operator_not_supported`), is 400 with
  `fields.answer`. A missing, deleted or another tenant's form is 404 first, as without answer filters. A form with
  no compiled schema is 409 `reporting_not_prepared`; without answer filters the same form lists normally.
- **Which rows are checked.** Only rows prepared since the submission's last change (`SourceModifiedAt` equals the
  submission's `ModifiedAt ?? CreatedAt`) that have a search copy. Every other submission inside the metadata filters
  is left out of the page and counted in answer-filter coverage (`answerFilterCoverage`: `uncheckedCompleted`,
  `uncheckedInProgress`): the rows not prepared since the submission's last change, or not rebuilt for the form's
  current search keys, so the grid can offer to prepare them. The field is absent without answer filters.
- **One query.** The Reporting context maps the core `Submissions` table read-only (`CoreSubmissionRow`, excluded
  from migrations, at the name the core context resolves). The list's metadata filters run on it with the core rules
  (`SubmissionGridMetadataSpec`; submitter profile filters through `IRelationalSubmitterProfileFilter`, the same
  provider SQL as the core list), joined to the matching prepared rows. The ordered ids of every match are read once;
  the count and the page come from that list, then the page's rows load by id. Answer-filter coverage is one more statement.

**Upgrading:** existing forms get their keys on their next compile (`POST …/reporting/compile-schema`), which then
rebuilds their rows' copies. Until then their copies are empty.

**Rolling deploy.** `AddSearchCopy` is an expand-only migration ([Schema changes during a rolling deploy](../../ARCHITECTURE.md#schema-changes-during-a-rolling-deploy)):
`FormSchemas.SearchKeys` defaults to `[]` and `FlattenedSubmissions.SearchJson` is nullable, so the previous release's
writes still succeed, and no contract migration follows. The expand release is the first stable release after `v0.9.0`
that ships it. During its rollout an old pod still flattens without knowing the copy: a row it writes after a new pod
compiled the form keeps `SearchJson` as it was (`NULL` for a new row, the previous copy for one it rewrites) until the row
is flattened again, and the compile that rebuilt the form's copies does not see it. #1265 (endatix#1273) closes that
window for rows an old pod creates: each row stamps the search keys revision it was written with, and an old pod
leaves a new row at 0, so it counts as not yet checked and the next rebuild catches it up. A row an old pod rewrites
keeps the stamp and copy it had, so it stays as it was until it is flattened again.

**Performance.** `tests/Endatix.IntegrationTests/Performance` measures the filters at scale (opt-in, see
`tests/README.md`). p95 of the whole request (ids and sort key of every match, count, page of 50), 50,000 rows, flattened
rows only for the first tables; the grid answer search rows below add the core join and the answer-filter coverage count:

| Shape (2026-10-16, Testcontainers, 8 vCPU) | No answer filter | 1–3 filters, 1 caller | 1–3 filters, 4 callers | Rebuild of 50k copies |
| --- | --- | --- | --- | --- |
| PostgreSQL realistic (1,000 questions, 20% answered) | 36 ms | 62–154 ms | 93–118 ms | 43 s |
| PostgreSQL worst (1,000 questions, all answered) | 49 ms | 56–124 ms | 90–140 ms | 135 s |
| SQL Server realistic | 200 ms | 220–423 ms | 584–790 ms | 53 s |
| SQL Server worst | 295 ms | 163–269 ms | 609–754 ms | 69 s |

Grid answer search (`SubmissionGridAnswerSearch`: core join, metadata filters, answer-filter coverage count), same data:

| Shape (Testcontainers, 8 vCPU) | 1–3 filters, 1 caller | 1–3 filters, 4 callers | No match, 1 caller |
| --- | --- | --- | --- |
| PostgreSQL realistic, 2026-10-16, `bc271d63` | 64–106 ms | 101–127 ms | 81 ms |
| PostgreSQL realistic, 2026-10-19, `2beee763` (ranges check the date shape) | 106–188 ms | 125–222 ms | 94 ms |
| SQL Server realistic, 2026-10-16, `bc271d63` | 307–400 ms | 786–805 ms | 188 ms |
| SQL Server realistic, 2026-10-19, `2beee763` (ranges check the date shape) | 325–413 ms | 777–861 ms | 209 ms |

64 search keys (50 automatic, 14 people's keys with the deep filtered questions). SQL Server with 4 concurrent callers
is the edge: 805–861 ms for 3 deep contains filters against an 800 ms target (567 ms at `bc271d63` and 305 ms at
`2beee763` once the copies were rebuilt). The 2026-10-19 run was slower in every scenario, the ones without a date range
included (3 deep contains: 805 → 861 ms), so the date shape check is within run-to-run noise. Each p95 is taken over 7
runs per scenario after 2 warm-ups, so it is the slowest of the 7, and the two runs spread 777–861 ms: the 805 ms miss
is inside that spread. The harness calls the search in process against one Testcontainer, without HTTP. The form-exists
read (one indexed `Forms` row per answer-filter request) was added after these runs, so it is not in the numbers;
endatix#1451 re-measures. Measure again before raising the key cap or adding work to the grid query.

#### Keys people choose

`POST /api/forms/{formId}/reporting/search-keys` with `{ "key": "<column key>" }` adds a key in the slots kept for
people; `DELETE /api/forms/{formId}/reporting/search-keys?key=<column key>` removes one (the key is a query parameter,
because column keys may hold characters a path segment cannot). Both need `Forms.Edit`; a form of another tenant is 404.
Both answer `200` with `{ formId, keys: [{ key, source, filterType, pending }], slots: { used, max, userUsed, userMax } }`.

- **Add.** Any column of the compiled schema with a submissions-grid filter type, read-only questions included (people
  may want what the form fills in; the compile leaves them out only because respondents do not). A column with no
  filter, or not in the schema, is 400. Over 14 people's keys, or 64 in all, is 400 and names both caps. A key the form
  already has changes nothing to the keys.
- **Rebuild in the background.** After the keys commit, the request writes an outbox message
  (`reporting.search_copy.rebuild_requested`, `SearchCopyRebuildRequests`) and answers at once, with the key
  `pending` while the form has prepared rows. The relay runs `RebuildSearchCopyOutboxHandler` inline, or as the
  `ReportingRebuildSearchCopy` job when `Endatix:Outbox:DeliverToJobQueue` is on; a rebuild that throws retries as any
  outbox message does. The outbox, not the job queue directly, because the Jobs module is off by default and
  PostgreSQL only, while the outbox runs on both databases. The message is written in the App database after the
  Reporting commit, so the two are not atomic, and the write is best-effort: it ignores the caller going away, and a
  failed write is logged, not returned as a 5xx. A message lost that way leaves the rows pending, never wrong. Every
  add, including a repeat of a key the form has, writes a message, and every compile runs the rebuild, so either
  finishes a rebuild that was cut off or lost; the rebuild skips rows already stamped. Inline in the relay, a rebuild of
  a large form (tens of seconds, "Rebuild of 50k copies" above) holds up the messages behind it, as a compile's
  rebuild does.
- **Remove.** Only a key with `source: "user"` (an automatic key is 400; a column that is not a key is 404). Filters
  stop reading it at once.
- **Concurrency.** Both hold the form's schema lock (`IFormSchemaRepository.LockAndGetByFormIdAsync`), the one a
  compile holds, so neither saves over the other's keys; a lock held too long is 503.
- **Columns catalog.** `GET …/reporting/columns` reports per column `searchable`, `searchPending` (some prepared row is
  stamped older than the key's revision) and `searchSource` (`auto`, `user` or null), and `searchSlots` for the form.

## Registration

Registered via `EndatixBuilder.UseDefaults()` → `UseModule(ReportingModule.Instance)`. Capabilities: `IEndatixModule`, `IHasFeatureFlag`, `IHasDbMigrations`, `IHasFastEndpoints` (serializers/OpenAPI tags via `ConfigureFastEndpoints`). Do **not** also `Api.ScanAssemblies` this assembly. The module runs on **PostgreSQL** and on **SQL Server 2025 or later (or Azure SQL Database)**; it needs SQL Server's native `json` type. `ConfigureServices` registers `ReportingPostgreSqlDbContext` or `ReportingSqlServerDbContext` by `ConnectionStrings:DefaultConnection_DbProvider`, and consumers resolve it as `IReportingDbContext` or `ReportingDbContextBase`. Disabled by default until:

```json
"Endatix": {
  "FeatureFlags": {
    "ReportingModule": true
  }
}
```

## Migrations

Run the commands from the `oss` folder.

> [!NOTE]
> Always use `Endatix.WebHost` as startup project, and this module as the migrations project.
> Each provider has its own context: `ReportingPostgreSqlDbContext` and `ReportingSqlServerDbContext`. Each one's
> design-time factory pins its provider, so pick the provider with `--context` and set
> `ConnectionStrings:DefaultConnection` to a database of that provider. See "Module persistence" in
> `src/Endatix.Framework/README.md`.

Migrations live in provider-specific subfolders under `Persistence/Migrations/`:

- `Persistence/Migrations/PostgreSql/` (`ReportingPostgreSqlDbContext`) — **available** (`InitialReporting`, `SeedDefaultExportFormats`, `AddFlattenedSubmissionSourceModifiedAt`, `AddFlattenedSubmissionSourceRevision`, `AddSubmissionViews`, `AddSearchCopy`, `AddSearchKeysRevision`)

  Tenant export formats are rows, not code. Runtime catalog: `DefaultExportFormats.All`
  (`SeedDefaultsAsync` / `tenant.created`). **Existing tenants:** frozen SQL in
  `SeedDefaultExportFormats` — do not generate that migration from the catalog; a new default
  needs a new data migration. **New tenants:** outbox `tenant.created` → `IDefaultExportFormatsSeeder`.
- `Persistence/Migrations/SqlServer/` (`ReportingSqlServerDbContext`) — **available** (`InitialReporting`, `AddSubmissionViews`, `AddSearchCopy`, `AddSearchKeysRevision`)

  One migration creates the schema and, at the end of its `Up()`, seeds the same default formats and CSV default
  mapping for every existing tenant as the PostgreSQL `SeedDefaultExportFormats` migration: frozen T-SQL in `SeedDefaultExportFormatsSqlServerSql`,
  with ids derived from the tenant id (`HASHBYTES`). Do not generate it from the catalog either.

On SQL Server, text comparisons follow the database collation, case-insensitive by default, where PostgreSQL compares
case-sensitively: two export formats of a tenant whose names differ only in case are one name there (the API already
treats them as one).

Startup also applies Reporting migrations when `Customizations:Reporting:ApplyMigrationsAtStartup` is true (default)
and `Endatix:Data:EnableAutoMigrations` is enabled.

### Add migration

```bash
# PostgreSQL
dotnet ef migrations add <MigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingPostgreSqlDbContext \
  --output-dir Persistence/Migrations/PostgreSql

# SQL Server
dotnet ef migrations add <MigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingSqlServerDbContext \
  --output-dir Persistence/Migrations/SqlServer
```

Always pass `--output-dir`. Without it, the first SQL Server migration (no snapshot yet) lands in the project's
`Migrations/` folder with the namespace `Endatix.Modules.Reporting.Migrations`, and every later migration added without
`--output-dir` follows it there. EF still finds and applies them: Reporting sets both migrations namespaces to the same
root, so namespace filtering is off and each context finds its migrations by their `[DbContext]` attribute. The cost is
the files themselves: they sit outside `Persistence/Migrations/SqlServer` and the module's migrations namespace, and
nothing fails to point that out. See "Adding a migration" in `src/Endatix.Framework/README.md`.

The commands below use the PostgreSQL context; pass `--context ReportingSqlServerDbContext` for SQL Server.

### Remove last migration

1. List migrations and check whether the last one is already applied locally:

```bash
dotnet ef migrations list \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingPostgreSqlDbContext
```

2. If it is applied, roll the database back to the previous migration:

```bash
dotnet ef database update <PreviousMigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingPostgreSqlDbContext
```

3. Remove the last migration:

```bash
dotnet ef migrations remove \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingPostgreSqlDbContext
```

### Apply migrations

```bash
dotnet ef database update \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingPostgreSqlDbContext
```
