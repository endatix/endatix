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

`POST forms/{formId}/reporting/compile-schema` and `POST forms/{formId}/submissions/backfill` accept `submissions.export` or `forms.edit`, so a user who may export can prepare a form for it. `replace=true` on compile and `force=true` on backfill clear or overwrite flattened rows, so they also need `forms.edit`; without it the endpoint returns 403 before it sends the command.

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

### Audience columns in exports

Participant values close an export row as one block, under their keys as stored (`ParticipantIdentifier`, `participant_<variable>`). A question whose export key matches a participant key keeps the key, and the participant column is left out. An export read with the Shoji codebook (`csv-shoji`, or any export with the Crunch alias profile or the `--` separator) applies this to every question of the form, whether the column scope includes it or not: the codebook is never scoped and gives the key to the question, so the export would otherwise write participant values under a variable Crunch reads as the question's. A plain `csv`, `json` or `xlsx` export applies it only to the questions it exports. Submitter profile columns follow the same rule. A tabular export that leaves out a participant column logs one warning naming the form and the keys, and one more for submitter profile columns.

The native codebook lists the block under `participants` (its title "Participants", `identifier` and `variables`), and each participant column entry has `surveyJsType` and `exportShape` `participant`. Canary builds from 0.8.1-canary.11 until #1282 keyed it `AudienceIdentifier` and `audience_<variable>` under a `personalization` block, with `personalization` as each column's type and shape. A schema flattened then keeps those columns until a replace compile and a backfill (Hub: **Rebuild reporting data…** with **Full recompile**); meanwhile they get no value from a new flatten and stay out of the `participants` block.

### Submission Grid columns

`GET forms/{formId}/reporting/columns?locale=` (`submissions.view`) lists the columns of the form's compiled schema in flattening-map order, for the Submission Grid's headers, column picker and answer filters. Each column has its key and kind, a title in `locale` (falling back to the default), the SurveyJS question type, an answer `filterType` (`text`, `date`, `datetime` or none) and its page and panel. The filter type is worked out at read time from the column kind and the codebook question's type and `inputType`, so a schema needs no recompile for it and export is unchanged. A form without a compiled schema answers `schemaReady: false` with no columns.

`readiness` counts the form's live submissions (`submissions`, `completedSubmissions`, `inProgress`) from core and its flattened rows by status (`prepared`, `pending` including `not_processed`, `failed`, `skippedInProgress`). An out-of-date count is not there yet.

Codebook question entries carry the question's place in the form: `pageName`, `pageTitle` (a locale map like `title`, left out when the page has no title), `parentPanelTitle` (default locale) and `parentPanels` (see below). They are written at compile time, so a schema compiled before these keys existed has no page until its next compile. Codebook `version` stays 1.

### Submission Grid rows

`GET forms/{formId}/reporting/submissions` (`submissions.view`) lists one page of a form's submissions for the Submission Grid. It takes the same filters, sort and paging as `GET forms/{formId}/submissions` (both build their query with `ListByFormIdRequest.ToQuery()`), so the rows, their order, the 404 for an unknown form and the tenant scope match the submissions list. `columns` is a comma-separated list of column keys from `GET forms/{formId}/reporting/columns`: at most 500 keys, each up to 512 characters. A key that contains a comma cannot be requested.

Each row carries the list fields plus:

- `rowState`, from the submission's flattened row: `prepared` (processed from the submission's current `ModifiedAt ?? CreatedAt`, and has data), `stale` (processed from an older version, or has no data), `pending` (`not_processed`, queued or being prepared), `failed`, `skipped` (in progress when the flatten ran) or `none` (no flattened row).
- `answers`, the requested columns that have a value.
- `answersSource`: `prepared` when the row is `prepared`, otherwise `raw`. Raw answers come from the submission's own `jsonData`, which is keyed by question name, so only simple question columns can be found there. Choice indicators, matrix cells and other flattened keys are left out until the row is prepared.

Without `answer=` filters, the page comes from the core list; its flattened rows are then read by id, with `DataJson` only when `columns` is set. With them, see [Answer filters on the submissions grid](#answer-filters-on-the-submissions-grid).

`submitterProfile.{field}` filters, on this list and on the submissions list, read the profile snapshot with `JSON_VALUE` on SQL Server. They compare under the `Latin1_General_100_BIN2` collation, so matching is case-sensitive as on PostgreSQL, whatever the database collation. Unlike PostgreSQL, SQL Server ignores trailing spaces when it compares strings, and `JSON_VALUE` returns NULL for a value over 4,000 characters, so such a value never matches `=` and always matches `!=`.

### Codebook panels and Crunch folders

Each question entry in the codebook names the panels it sits in, outermost first: `parentPanels: [{ "key", "title" }]`. The key tells panels apart: the panel's name, or its position (`page1#2`, the third element on `page1`) when it has none. The title is the panel's default-locale title, or its name when it has none. `parentPanelTitle` (the nearest panel's title) stays for readers of older codebooks. A panel with neither a name nor a title adds no entry. `ParentPanel` (`QuestionPlacement.cs`) is the one place a key and title are worked out, for a compile and for a codebook read back.

The Shoji codebook (`codebook-shoji`) groups its `order` into folders Crunch shows on import: Meta-data (system columns), Participant variables (`ParticipantIdentifier`, `participant_*`), Submitter (`submitter_*`), Calculated values and Questions; an empty folder is left out. The names are in `ShojiFolderNames`. Panels and loops become sub-folders of Questions, nested along `parentPanels` and keyed by panel name. A repeated title among siblings is numbered ("Details (2)"). `FormId` goes in `table.hidden` instead: Crunch refuses an alias that is both hidden and ordered.

An entry with only `parentPanelTitle` (a codebook saved before `parentPanels` existed, or a stale entry a merge kept) takes the path of the panel with that title when the codebook's other entries name exactly one; otherwise it gets one folder per title, without nesting, until the form's schema is compiled again. A loop column with no question entry takes the loop's path from its other columns. Folder names are the title on one line, unique among siblings without regard to case. A compile's new question entries win over the stored ones, in both replace and merge.

### Numeric radio matrix codes

For a radio matrix column defined as `{ "value": 10, "text": "High" }`, the native codebook writes `value: "10"` and labels it `High`. Native data keeps the numeric answer `10`. In a Shoji CSV the same cell holds the category ID from the matching Shoji codebook, which need not be `10`. Columns stay in definition order; a previously omitted numeric column gets an ID after the already published IDs.

Merge compilation keeps published category IDs, including categories removed from a matrix that still exists. New categories receive IDs after all reserved IDs. Numeric codes compare by exact numeric value (`10.0` and `1E1` match `10`); a string `"10"` is a different choice. If a flattened number could also be a string choice's ordinal, Shoji uses the original answer to distinguish them. Without a matching original answer the cell is missing.

Shoji keeps the published names of a typed matrix's categories as well as their IDs. If `10` was published as `High` and `20` replaces it with the same label, the new name is `High (20)`. The native codebook still shows the definition's label. When a flattened cell contains the exact string code, Shoji can decode it even without the original submission; `"80"` never means numeric `80`.

An upgrade does not rewrite saved schemas. A codebook compiled before numeric codes were identified may contain labels instead of codes. Exports keep the historical behavior for such a matrix; backfill alone cannot recover its codebook. A merge that cannot establish category continuity returns a conflict before saving. Historical-only questions retain their stored metadata. Explicit replacement starts a new schema and can discard historical categories and rows; it is not an identity-preserving upgrade.

### Copied choices

A single select (dropdown, radiogroup and the other single base selects) or a multi-select (checkbox, tagbox) can copy its choices from other questions: SurveyJS `choicesFromQuestion`, or Hub carry forward (`edxCarryForwardEnabled: true` with `edxCarryForwardSources`). The compiler reads where a question's choices come from once (`ChoiceSource`, with Hub's precedence: data list, then `choicesFromQuestion`, then `choicesByUrl`, then carry forward). A copy publishes every choice it can offer: the union of its sources' own choices in source order, the first occurrence of a value winning, label included. A source that copies its own choices is followed. Modes (`all`, `selected`, `unselected`), `edxCarryForwardMaxChoices` and priority items only shape a respondent's list, so they change no column; they decide which indicator cells may be 0 (below). The sources' Other, None and other special items are not copied; the question's own still are. One exception follows SurveyJS (#1763): a `choicesFromQuestion` copy in `selected` mode with no Other of its own also offers its source's Other, which SurveyJS adds once the respondent picked it and typed its text, and the copy then stores `other` with no text of its own. Its catalog lists that Other last, after the copy's own special items, labelled with the source's `otherText`, and records `otherFrom: <source>`. Merge keeps the latest recorded source. When a copy picked `other`, the export uses its own non-empty text first, then the text of the source offered by the submission's form version. If that version is unknown, it uses the stored source.

A checkbox or tagbox copy writes 1 for a picked Other. An unanswered copy is empty. For an answered copy with an exact visibility rule that holds for the submission (below), an unpicked Other is 0 if the submission's form version showed the copy's own Other or its source picked Other with text. Otherwise it is empty. An unknown version cannot prove the copy showed its own Other; only the stored source can supply that evidence by having a picked Other with text. Hub carry forward never copies a source's Other. Known gaps: a question loop over such a copy has no panel for that Other; a ranking copy lists no copied Other, so a ranked `other` has no column; and a copy of such a copy, which SurveyJS gives that Other as a regular choice valued `other`, has no category for it.

A base select's (single or multiple) or a ranking's entry records the source as `choiceSource`, left out for inline choices:

| `kind` | Field | When |
| --- | --- | --- |
| `carryForward` | `sources`: question names, in order | `choicesFromQuestion` (one source) or carry forward |
| `dataList` | `listId` | `edxDataListId` |
| `unresolvable` | `reason`: `choiceValuesFromQuestion` or `choicesByUrl` | values copied from answers, or choices loaded from a URL |

A reader treats an unknown `kind` as inline. Only a copy changes what is published: a data-list, URL or values question keeps its own choices, as before, until #1160. A missing source, or one that is not a select, is skipped, as Hub skips it. When a copy has a source the codebook cannot read (a data list, a URL list, values from answers, or a cycle), or has neither ordinary choices nor a copied Other, its entry stays `categorical` with only its own special items and is marked `degraded: "text"`, the one value `degraded` has. The Shoji codebook then writes a text variable and `csv-shoji` the stored value, and each export logs one warning naming those questions. A multi-select copy that is degraded keeps one text column of the picked values and its `multiple_response` entry is marked the same way.

A checkbox or tagbox that copies its choices (#1724) gets one indicator column per choice the copy offers, `<question>__<value>` (inside a question loop, `<loop>__<iteration>__<question>__<value>`), then its own special items, the keys a multi-select with its own choices gets. Leftover inline choices (Creator's `item1..3`) are ignored. Its `multiple_response` entry records `choiceSource` and lists the copied choices labelled on their source question; the columns' `choiceLabel` and the Shoji subvariables read the same labels, and a question loop over it labels its iterations with them. A cell is 1 when the choice was picked, 0 when the respondent was shown it and did not pick it, and empty when they were not shown it or it cannot be known (#1733). Columns are append-only under Merge (the compile after a form edit once it has real submissions): a source choice removed or renamed after answers keeps its column, so those answers still export as 1, and a rename splits old and new answers across two columns. A value with no column (one the copy's sources never offered, such as typed text stored as a value, or a removed or renamed choice after a replace compile, `replace=true`, Hub **Full recompile**, which rebuilds the columns from the current form) is picked in no column: its other cells are 0 only where an exact rule shows every choice, and empty otherwise.

**Not shown (#1733).** `CopyVisibility` owns the rule; `ChoiceCatalogs.Indicator` is the one question the flatten asks, and `IndicatorCell` the only writer of an indicator cell. A picked choice is 1. For a select with its own choices, unpicked values covered by `choicesVisibleIf` or a choice’s `visibleIf` are empty. Use the nonblank conditions from the submitted version at the column’s loop path (panel `valueName` or name), including when the stored codebook predates choice conditions. If the matching own question or value is absent there, fall back to the catalog’s `conditionalChoices`; merge compiles keep their union. The other values are 0 when answered. With no answer they are 0 only in a completed submission, when no definition could hide the question, and when the definition the submission was saved on has it (#1758: a question added or removed in another version was never shown; the flatten reads the names in `Submission.FormDefinition` with `CopyDefinition.QuestionsIn`, and an unknown definition keeps the 0); otherwise every cell is empty, its Other and None included. A question can be hidden when it has a `visibleIf` or a static `visible: false`, on itself or on an enclosing panel, dynamic panel or page, or when the definition uses its name twice (`CopyDefinition.CanHide`, the facts copies use). The compile records that on its multi-select choice catalog as `canBeHidden: true`, and every merge keeps it once any definition set it; a replace compile rebuilds it from the current definition, and a question that stops being a select loses it. A copy with no answer in its scope is empty in every column, its own special items included. An answered copy with an exact rule shows its own None, Refuse to answer and Don't know (0), and a copied choice only when the rule rebuilds it from the sources' answers in the same submission: `all` shows every regular choice of a source, `selected` the ones the source picked, `unselected` the ones it offers and did not pick, any source showing it being enough. A source is read in the copy's own loop item first, then each enclosing one, then the root (`AnswerScope`); a source with no answer there (missing, null, `""` or `[]`) makes the cell unknown. The copy's own special items do not depend on its sources, so an answered copy with an exact rule writes 0 for an unpicked None even when a source has no answer; for its own Other the submission’s form version must also show it. The rule is exact only for a SurveyJS mode spelled `all`, `selected` or `unselected` (absent is `all`), or a Hub mode Hub's map knows (absent or blank is `all`), with no `edxCarryForwardMaxChoices` of 1 or more, no `choicesVisibleIf` on the copy, select sources with no `choicesVisibleIf` or choice `visibleIf` that have their own inline choices or copy them (below), and no copy or source name the definition uses twice (a root question and a loop's, or two loops', share one catalog; `CopyDefinition`). A survey whose `clearInvisibleValues` is `none`, `onHidden` or `onHiddenContainer` keeps answers a respondent never saw, so a page, panel or question `visibleIf` anywhere in its definition makes every copy unknown (`CopyDefinition.HasVisibleIf`; #1736). A survey whose `clearInvisibleValues` is absent or `onComplete` clears them on complete: a copy or source under a `visibleIf`, its own or one on an enclosing panel, dynamic panel or page (`CopyDefinition.ContainerVisibleIfNames`), is then exact only for a completed submission (`shown.whenComplete`), its own special items included: in an incomplete submission an unpicked None or Other is empty too (`CopyVisibility.HoldsFor`). Only select sources set `whenComplete`: a text source adds no choices. A copy or select source whose own `clearIfInvisible` keeps its answer (`none`, `onHidden`, `onHiddenContainer`; `default` or none defers to the survey) is unknown when a `visibleIf`, its own or an enclosing panel's, dynamic panel's or page's, can hide it (`CopyDefinition.KeepsHiddenAnswer`). The walk reads pages, `elements`, dynamic panel `templateElements` and matrix `detailElements`. A copy with a static `visible: false`, on itself or on an enclosing panel or page (`CopyDefinition.AlwaysHiddenNames`), is unknown, since a default or an expression can still give it an answer. A static `visible: false` source, or one in a hidden panel or page, is still exact: its stored answer is what the copy built its list from. A source that is a ranking is unknown in every mode. A source that copies its choices (#1754) offers the union its chain resolves to (`ChoiceSources.Offered`); a chain with a data list, a URL list or a cycle is unknown (`SourceCopyIsNotExact`). Under `selected` the copy shows what that source picked, which was on its list whatever capped or shaped it, so nothing further up is read. Under `all` or `unselected` the copy shows what the source showed: the source must be a checkbox or tagbox whose own rule is exact (the compile recurses through `CopyVisibility.Compile`), the run reads the source's stored `shown` (`ChoiceCatalogs.VisibilityOf`), a source marked `copied` with no stored rule is unknown, and the copy's rule takes `whenComplete` from the source's. The run reads each copied source's list once per cell, so sources that share upstream copies cost one read each, not one per path; rules that name each other, which no compile writes, read as unknown instead of looping. A chain longer than 64 links is unknown, at compile (`SourceCopyIsNotExact`) and at run time, so a long chain never overflows the stack. Exact copies record it on their multi-select choice catalog as `shown: { mode, sources: [{ name, values }], whenComplete? }`, each source with the regular values it offered, which the rule reads instead of the source's catalog (the union of every definition); anything else, and every codebook compiled before this, has no `shown`, and its unpicked cells are empty. A copy is recognised by its question entry's `choiceSource.kind` (`carryForward`), which every codebook since #1724 has, or by `copied: true` on its select catalog, which every merge keeps (a stored question entry that was a copy marks the catalog too), so a question made inline later still never writes 0 for rows saved while it copied. The marker covers new rows too: after a merge compile, a former copy writes 1 or empty, never 0, until a replace compile rebuilds the catalog from the current definition. The reverse holds as well: an inline select made a copy turns the 0s of rows saved while it was inline into empty on a backfill, since the merge drops the new rule over a catalog that had none. The codebook's merge keeps `shown` only when the stored and the current rule are equal, each source offers the same values (a choice moved between sources drops it, and so does a `shown` without per-source values), and the copy offers the same choices and special items; otherwise it drops it, and it stays dropped until a replace compile. A question that stops being a select loses `shown` with its other select fields. A replace uses the current definition’s `shown` rule for every stored row, so a form whose copy changed its mode, its sources' choices or whether it copies at all should be compiled in merge mode before its backfill. A loop item the submission has no item for is empty in every column, a checkbox or tagbox included. No Shoji category changes: `csv-shoji` writes the empty cell and Crunch imports it as No Data.

A ranking that copies its choices (#1723) gets one rank column per choice the copy offers, `<question>__<value>` (inside a question loop, `<loop>__<iteration>__<question>__<value>`), the same keys a ranking with its own choices gets. Its codebook entry and choice catalog list those choices, each labelled on its source question's choice, then Other when the ranking shows it (no column, as for its own choices), and its rank columns' `choiceLabel` reads the same labels. A question loop over it labels its iterations with them. A ranking cell (`RankCell`, also a matrix column's position) holds the rank, 0 for a choice an answered ranking left unranked, and is empty when the ranking has no answer (#1734). A ranking that copies its choices (`ChoiceCatalogs.Copies`: its question entry or a sticky `copied` on its catalog) leaves an unranked choice empty instead, since its list depends on its sources; an inline ranking keeps 0. A ranking copy whose source the codebook cannot read gets no rank columns and lists no choices, never its leftover inline ones; it is not marked `degraded`, since it has no text column to fall back to.

**Existing forms (#1753):** with no recompile, a backfill with `force=true` leaves an unanswered checkbox or tagbox with its own choices empty in an incomplete submission, and keeps its 0s in a completed one. A completed submission's unanswered question that a `visibleIf` or a `visible: false` can hide turns empty only once a compile records `canBeHidden`: compile (merge mode keeps it), then backfill with `force=true`. In the Hub only **Rebuild reporting data**, **Full recompile** does both; a rebuild without it backfills without `force` and skips unchanged rows. Shipped in 0.9.3, a breaking change for the Reporting preview: an exported 0 becomes empty.

**Existing forms (#1758):** no compile is needed: the check reads the submission's own definition when its row is written, so a backfill with `force=true` (`completionScope=completed`) empties the 0s of questions its version did not have. The Hub's **Full recompile** also rewrites them, but its `replace=true` compile drops the columns of questions the current definition removed. Shipped in 0.9.3: an exported 0 becomes empty.

**Existing forms (#1763):** a schema compiled before has no Other on a selected-mode copy of a source with Other. A merge compile adds it with the next id, so a radiogroup or dropdown copy exports it after a backfill with `force=true`; a checkbox or tagbox copy's rule then differs from the stored one, so the merge drops it and only a compile with `replace=true` (Hub **Full recompile**) restores its 0s. Compile with `replace=true`, then backfill with `force=true` for both `completionScope` values.

**Existing forms (#1754):** a 0.9.2 schema has no `shown` for a copy of a copy, and a merge never adds one over a catalog without it, so compile with `replace=true`, then backfill with `force=true` for both `completionScope` values (Hub: **Rebuild reporting data**, **Full recompile**). A form whose copies changed across compiled definitions compiles in merge mode instead, as below, which leaves empty the cells of a copy whose rule differs; a definition that was never compiled is judged by the current rule. Shipped in 0.9.3: an empty cell becomes 0 where the source's answer proves the choice shown.

**Existing forms (#1733, #1736):** a stored `shown` rule is replaced only by a compile. Upgrade in this order: compile with `replace=true` first, then backfill with `force=true`; a backfill before the compile judges rows by the old rule. Exports read stored rows, so rows flattened before the upgrade keep their 0s until a backfill with `force=true` rewrites them. A schema compiled before #1733 needs no recompile for that backfill to be safe: its copies have no `shown`, so their unpicked cells become empty, never a wrong 0. For exact 0s, compile with `replace=true`, then backfill with `force=true`; a form whose copies changed across definitions compiles in merge mode instead, as above.

**Existing forms (#1724):** a schema compiled before this keeps empty categories for a copied question until its next compile. `POST forms/{formId}/reporting/compile-schema` (merge mode once the form has real submissions) adds them; a merge keeps the ids of choices already in the catalog. A copied ranking's new rank columns are only added: run a backfill with `force=true` after the compile so existing rows fill them. A copied checkbox or tagbox changes shape (one text column, or a loop's leftover `item1..3` columns, becomes indicator columns), and a merge keeps the old columns beside the new ones, which `csv-shoji` then writes with no Shoji variable: compile with `replace=true` (Hub: **Rebuild reporting data**, **Full recompile**), then backfill. A copy is held to `MaxChoicesPerQuestion` (2,500) like any select: a checkbox copying more choices than that, its sources together, now fails the form's compile where it used to be one text column.

### Submitter profile fields

`Customizations:Reporting:SubmitterProfileFields` lists the keys of `Submission.SubmitterProfileSnapshot` (the claims `Endatix:Submitter:ProfileSnapshotFields` captures) that prepared submissions carry. It is empty by default, and empty adds no column: schemas, flattened rows and exports stay as they were.

```json
"Customizations": {
  "Reporting": {
    "SubmitterProfileFields": ["email", "department"]
  }
}
```

- **Columns.** Each listed field is a `submitter_<field>` column of kind `SubmitterProfile`, data type `string`, label `Submitter <field>`. A compile adds only the definition's columns. Like audience columns, the submitter columns join the schema when a flatten finds them missing: under the form's lock, appended at the schema's own definition revision, so listing a field never rebuilds a schema from an older definition and its labels stay the current ones. A schema at its column limit is flattened without them, with a warning, instead of failing. The limit is checked once for a flatten's audience and submitter columns together, so near the limit a listed field can also hold back the audience columns a response carries. A replace that clears the flattened rows drops them until the next flatten adds them back. A question already named `submitter_<field>` keeps the key, and the profile value is not exported: the schema keeps the first column a key had, the same rule that lets a question win over a `participant_` key.
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
