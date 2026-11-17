# Form schema fixtures

Golden files for the Reporting compile and export pipeline. A test compares what the code produces today with the
committed file, so a change to the output shows up as a diff in review.

| Folder | What it holds | Test |
| --- | --- | --- |
| `*.json` (this folder) | One definition per question type, with its expected column keys and flattened submission | `FormDefinitionFlattenerTests`, `FlatteningBehaviorFactsTests`, `FlattenedSubmissionFlattenerTests` |
| `AllQuestions/` | One definition with every question type: keys, flat output, codebook, Shoji codebook, Crunch export keys | `AllQuestionsCompileTests` |
| `CustomerExcerpts/` | Obfuscated excerpts of customer forms, with their expected keys | `FormDefinitionFlattenerTests` |
| `Personalization/`, `AllQuestionsPersonalized/`, `SubmitterProfile/` | Audience and submitter profile columns | `PersonalizationGoldenTests`, `AllQuestionsPersonalizedTests`, `SubmitterProfileGoldenTests` |
| `Scenarios/` | One export edge case per folder, reviewed as whole documents | `ScenarioGoldenTests` |

## Scenarios

Each folder under `Scenarios/` is one edge case:

- `definition.json`: the form definition, as small as the case allows.
- `submission.json` (optional): one stored response, as Hub stores it. With it, the scenario exports every format.
- `expected-codebook.json`: the format-neutral codebook it compiles to.
- `expected-shoji-codebook.json`: the Shoji codebook it exports to.

A scenario with a `submission.json` also has:

- `expected-flattening-map.json`: the compiled form schema, i.e. its columns and keys;
- `expected-flat.json`: the flattened submission row;
- `expected-csv.csv`, `expected-csv-shoji.csv` and `expected-json.json`: the submission exports;
- `expected-xlsx.json`: the xlsx export as the text of each cell, sheet by sheet and row by row, never the binary.

The submission exports run through the tabular data source and the Infrastructure exporters with the settings of the
default export formats (`DefaultExportFormats`), and csv-shoji with the Shoji format's. The row's system values are
pinned (`PersonalizationPipeline.ExportRows`). Global value transformers do not run: they rewrite file URLs and large
values only.

`ScenarioGoldenTests` runs every folder that has a `definition.json`, so a new scenario needs no new test. It
compares each `expected-*` file the folder has, fails on one it does not export, and checks that the csv-shoji file
imports against the Shoji codebook (`CrunchImportCheck`).

| Scenario | Case |
| --- | --- |
| `copied-choices-selected-mode` | A radiogroup copies its choices from a checkbox with `choicesFromQuestion` in selected mode (#1163). |
| `loop-over-copied-choices` | Question loops over a checkbox and a ranking that copy their choices (#1721). Each repeats once per choice its source offers, read on the question at the root of the copy chain. Until #1724 and #1723 publish the copied choices, `aware` stays one text column, `occasionRank` has no column, and iterations are labelled with their values. |
| `copied-choices-loops` | A synthetic survey with copied choices throughout (#1722), below. |

### `copied-choices-loops`

About 50 questions on five pages, three choices each, and one response that answers them all.

- **Hidden inputs:** `region`, a hidden radiogroup with a default; `hiddenBrands` and `hiddenTraits`, hidden checkboxes
  filled by `defaultValueExpression` (a calculated value, numbers where the choices are strings).
- **Copies:** `AidedBrands`, a checkbox copying `hiddenBrands` (selected, None); `favorite`, a radiogroup copying
  `AidedBrands` (a chain); `brandRank`, a ranking copying `AidedBrands` (selected, select-to-rank).
- **Loops,** each with an html element and one question:
  - `considerLoop` over `AidedBrands`, with a radiogroup with its own choices and a `visibleIf`;
  - `traitsLoop` over `AidedBrands`, with a checkbox copying `hiddenTraits` (None, Other);
  - `attributeLoop` over the hidden `hiddenBrands`, with a checkbox copying `AidedBrands` that keeps the Creator's
    `item1..3`;
  - `rankLoop` over `brandRank`, with a radiogroup copying `AidedBrands`;
  - `reasonLoop` over `AidedBrands` with `randomizeLoop`, with a text question.
- **Copy matrix,** all copying `stores`:

  | Syntax | Mode | Checkbox | Radiogroup | Ranking | Loop (source) |
  | --- | --- | --- | --- | --- | --- |
  | `choicesFromQuestion` | all | `cfqAllPick` | `cfqAllOne` | `cfqAllRank` | `cfqAllLoop` (checkbox) |
  | `choicesFromQuestion` | selected | `cfqSelectedPick` | `cfqSelectedOne` | `cfqSelectedRank` | `cfqSelectedLoop` (ranking) |
  | `choicesFromQuestion` | unselected | `cfqUnselectedPick` | `cfqUnselectedOne` | `cfqUnselectedRank` | `cfqUnselectedLoop` (radiogroup) |
  | `edxCarryForwardSources` | all | `edxAllPick` | `edxAllOne` | `edxAllRank` | `edxAllLoop` (checkbox) |
  | `edxCarryForwardSources` | selected | `edxSelectedPick` | `edxSelectedOne` | `edxSelectedRank` | `edxSelectedLoop` (ranking) |
  | `edxCarryForwardSources` | unselected | `edxUnselectedPick` | `edxUnselectedOne` | `edxUnselectedRank` | `edxUnselectedLoop` (radiogroup) |

  The mode does not change the export: a copy publishes every choice its sources offer, whichever the respondent was
  shown, so the three rows of each syntax compile alike.
- **Other questions:** `household`, a dynamic panel without `loopSource`; `satisfaction` (matrix); `usage`
  (matrixdropdown); `contact` (multipletext); `comments` (text); `brandPool`, a calculated value in the result, and
  `regionLabel`, one left out.
- **The response:** loop panels in a different order from their iterations, keyed by `itemValue`; a None pick; an
  Other with its comment; an iteration left unanswered (`traitsLoop`, brand 1).

The submission is written by hand in the shape Hub stores (`itemValue` and `loopIndex` on each loop panel). It has not
been captured from a Hub submission yet.

### Known gaps the goldens pin

The goldens hold today's output, wrong parts included, so each fix shows up as a golden diff:

- **#1723, a ranking that copies its choices:** `brandRank`, `occasionRank` and every `*Rank` have no column, and
  their codebook entries have no choices.
- **#1724, a checkbox that copies its choices:** `AidedBrands`, `aware`, every `*Pick` and `traits` export as one text
  column of the picked values (Shoji `text`). `attribute` gets the leftover `item1..3` and `none` columns, all 0 except
  None, because its answers are the copied values.
- **Iteration labels:** a loop over a checkbox or a ranking that copies its choices labels its iterations with their
  values (`1`, not `Brand A`) until #1724 and #1723 publish the copied choices.
- **#1441, a choice the respondent was not shown:** a copy's cells are 1 or 0 for every choice once #1724 lands; the
  empty cell for a choice never shown stays in #1441.
- **#1725, JSON keys:** the JSON export camel-cases question names (`aidedBrands`).
- **#1726, plain-text labels:** HTML in a label reaches the Shoji codebook as written (`hiddenBrands--1`).
- **#1544, a copied Other:** see below.
- **#1160, data lists:** a copy of a data list has no choices to publish; no scenario has one yet.

Known gap: in selected mode a copy also offers the source's Other when the respondent picked it, and stores `other`.
The codebook publishes only the sources' own choices, so that answer has no category and its csv-shoji cell is empty.
`CopiedChoicesCrunchExportTests.CsvShoji_CopiedOtherInSelectedMode_HasNoCategoryUntil1544` pins this until #1544
publishes the source's Other as a special item. A question loop over such a copy has no panel for that Other either, so the answers in
the panel the respondent saw for it are not exported until #1544.

### Adding a scenario

1. Write the definition: create `Scenarios/<name>/definition.json`. Use a kebab-case name for the case, not for a
   customer, and keep customer content out: obfuscate names, titles and choices. Add a `submission.json` to export
   every format; never commit a customer's submission.
2. Regenerate: remove `Skip` from
   `AllQuestionsCompileTests.AllQuestionsGoldenFixtures_Regenerate_WithCurrentPipeline_WritesExpectedFixtureFiles`, then
   run it:

   ```bash
   dotnet test tests/Endatix.Modules.Reporting.Tests --filter-method "*AllQuestionsGoldenFixtures_Regenerate*"
   ```

   It rewrites every scenario's expected files in the source tree, and the `AllQuestions/` goldens. Put `Skip` back.
3. Review: read the expected files as a reviewer would, check every label, type, category and cell against the
   definition and the submission, and add the scenario to the table above. Commit the definition, the submission and
   the goldens together.
