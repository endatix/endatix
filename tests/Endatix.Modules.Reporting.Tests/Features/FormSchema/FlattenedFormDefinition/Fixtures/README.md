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
- `versions.json` (optional): earlier form versions. `earlier` lists definition files compiled in order before
  `definition.json` (the first replaces, each later one merges, as a form with real submissions does), and
  `submittedOn` names the one the submission was saved on. Without it, the submission was saved on `definition.json`.
- `expected-codebook.json`: the format-neutral codebook it compiles to.
- `expected-shoji-codebook.json`: the Shoji codebook it exports to.

A scenario with a `submission.json` also has:

- `expected-flattening-map.json`: the compiled form schema, i.e. its columns and keys;
- `expected-flat.json`: the flattened submission row;
- `expected-csv.csv`, `expected-csv-shoji.csv` and `expected-json.json`: the submission exports;
- `expected-xlsx.json`: the xlsx export cell by cell, sheet by sheet and row by row, never the binary. A cell reads
  `<type>[@<style>]:<text>`: `s` text, `n` number, `b` boolean; a date is a number with the date style. `styles` gives
  each style a cell uses its `numFmtId` and format code.

The submission exports run through the tabular data source and the Infrastructure exporters with the settings of the
default export formats (`DefaultExportFormats`), and csv-shoji with the Shoji format's. The row's system values are
pinned (`PersonalizationPipeline.ExportRows`), and the response is flattened with the row's completion state, so a
copy's "shown" rule that holds only for a complete submission applies as it does in the running API.
`ScenarioCsvExportFlowTests` (integration tests) runs every scenario with a submission and no `versions.json` over HTTP, compile, backfill
and the CSV export format included, and compares its answer columns with `expected-csv.csv`. Global value transformers do not run: they rewrite file URLs and large
values only.

`ScenarioGoldenTests` runs every folder that has a `definition.json`, so a new scenario needs no new test. The folder
must hold exactly the goldens the scenario exports. JSON is compared as it is committed (indented, in written order),
and csv byte for byte, CRLF included (`Scenarios/.gitattributes` keeps git from converting them). A scenario with a
submission also checks that the csv-shoji file imports against the Shoji codebook (`CrunchImportCheck`).

| Scenario | Case |
| --- | --- |
| `copied-choices-selected-mode` | A radiogroup (`Q2`) and a checkbox (`Q3`) copy their choices from a checkbox with Other (`Q1`) with `choicesFromQuestion` in selected mode (#1163). `Q1` picks `1` and Other with text, so both copies also offer `Q1`'s Other, last and labelled with `Q1`'s `otherText` (#1763). `Q2` picks it: its Other category, and `Q2__other_text` holds `Q1`'s text. `Q3` picks `1`: its Other is 0 and `2` and `3`, which `Q1` did not pick, are empty. |
| `loop-over-copied-choices` | Question loops over a checkbox and a ranking that copy their choices (#1721). Each repeats once per choice its source offers, read on the question at the root of the copy chain. `occasionRank` has one rank column per copied choice, and `occasionLoop` labels its iterations with the source's labels (#1723). `aware` has one indicator column per copied choice and `considerLoop` labels its iterations with the source's labels (#1724). `brands` offers 11 choices and `considerLoop` holds a `reasons` checkbox with 11, declared 2, 10, 1, 11, 3, …, 9. The Shoji codebook lists a loop's categorical-array members (1, 2, …, 10, 11) and multiple-response subvariables (2, 10, 1, 11, 3, …) in choice order (#1764). The loop variables' metadata keys stay in key order (`considerLoop--10--reasons` before `considerLoop--1--reasons`) and the `order` section is unchanged, so no variable moves in an existing Crunch dataset. |
| `copied-choices-loops` | A synthetic survey with copied choices throughout (#1722), below. |
| `calculated-value-name-clashes` | Calculated values named like a checkbox, a matrix, a ranking and a multiple-text question, and one named like a question's title (#1171). Each gets its own codebook column. In Shoji the checkbox and matrix keep their variables and the two calculated values are left out; the ranking and multiple-text clashes get `text` variables under their own names; the title clash is named `totalAmount -- totalAmount`. |
| `hidden-pages-copies-and-specials` | One question per page, five pages with a `visibleIf`. `topFruit`, a radiogroup copying `fruits` (selected; the source has None and Other), sits on a page hidden by the answer and is empty. `veggiesAgain` copies `veggies` on a page shown by the answer and picks its own None: the source's pick is 0, the rest empty, None 1 (a rule that holds for a complete submission). `snacksAgain` adds its own None to a source without one. `rating` is a matrix in Hub's carousel display. `extras`, a checkbox with its own choices on a hidden page, has no answer, so every one of its cells is empty (#1753); `extrasNote` is a comment on a hidden page. |
| `numeric-values-hidden-questions` | Choice values written as numbers and stored as numbers. Questions with `visible: false` and a `visibleIf`, answered and not: `reasons`, a checkbox with Other picked and its text; `brandStore`, a radiogroup with Other picked (`other` natively, the Other category in Shoji); `brandSwitch` unanswered. `photos`, a panel with the same pattern, holds two unanswered file questions. |
| `capped-ranking-loops` | `topOccasions` ranks two of the choices `occasionPool` was given (`selected`, `minSelectedChoices` and `maxSelectedChoices` 2, select-to-rank); unranked choices are empty. `occasionPool` is filled by a `defaultValueExpression` with numbers and strings, two of them values its choices do not offer (known gap below). `occasionLoop` repeats over the ranking, with a radiogroup copying `aware` that picks its own None in one iteration. `traitLoop`, a loop with its own `visibleIf`, repeats over `aware` with a checkbox copying the top-level `traits`: 1, 0 and empty per iteration, under a rule that holds for a complete submission. |
| `unanswered-own-choices` | Checkboxes and a tagbox with their own choices that a completed response leaves unanswered (#1753). `channels` and `topics` (with None), which nothing can hide, are 0. Empty in every column: `flavours` (its own `visibleIf`, None included), `internalTags` (`visible: false`), `stores` (in a panel with a `visibleIf`, Other included), `elsewhere` (on a page with a `visibleIf`; that page was shown, but the compile cannot tell, so an unanswered question there is empty too), `staffNotes` (on a `visible: false` page), and `reasonDetail` in `reasonLoop` (its own `visibleIf` inside a loop item the respondent had). `reasons`, answered on a page with a `visibleIf`, is 1 and 0. An incomplete response is covered by `InlineChoiceVisibilityTests` and the in-progress row of `AllQuestionsPersonalized/`. |
| `copy-of-a-copy` | Checkboxes that copy a copy (#1754). `aware` copies the brands picked in `brands` (selected); `pickTop`, and `pick` in `brandLoop` over `brands`, copy what `aware` picked (selected): `aware`'s picks are 0 or 1, the brand it left is empty, and the copy's None is 0 when not picked. `fromShortlist` copies all of `shortlist`, which caps its list (`edxCarryForwardMaxChoices`), so its unpicked choices and None stay empty. |
| `question-across-versions` | A checkbox removed and one added between form versions (#1758). Version 1 has `oldPerks`, version 2 drops it, and `definition.json` adds `newPerks`; the merge keeps every column. The completed submission was saved on version 2 and leaves `channels` unanswered: `oldPerks` and `newPerks`, which its version does not have, are empty in every column, while `channels`, which nothing can hide, is 0 and the answered `topics` 1 and 0. |

### `copied-choices-loops`

About 50 questions on five pages, three choices each, and one response that answers them all.

- **Hidden inputs:** `region`, a hidden radiogroup with a default; `hiddenBrands` and `hiddenTraits`, hidden checkboxes
  filled by `defaultValueExpression` from the calculated values `brandPool` and `traitPool` (numbers where the choices
  are strings).
- **Copies:** `AidedBrands`, a checkbox copying `hiddenBrands` (selected, None); `favorite`, a radiogroup copying
  `AidedBrands` (a chain); `brandRank`, a ranking copying `AidedBrands` (selected, select-to-rank).
- **Loops,** each with an html element and one question:
  - `considerLoop` over `AidedBrands`, with a radiogroup with its own choices and a `visibleIf`;
  - `traitsLoop` over `AidedBrands`, with a checkbox copying `hiddenTraits` (None, Other);
  - `attributeLoop` over the hidden `hiddenBrands`, with a checkbox copying `AidedBrands` that keeps the Creator's
    `item1..3`; a copy of a copy, so what `AidedBrands` picked is 1 or 0 and the rest empty (#1754);
  - `rankLoop` over `brandRank`, with a radiogroup copying `AidedBrands`;
  - `reasonLoop` over `AidedBrands` with `randomizeLoop`, with a text question;
  - `cfqAllLoop` and `edxAllLoop` (copy matrix, below) each also hold a ranking copying `stores`: `cfqLoopRank`
    (`choicesFromQuestion`, all) and `edxLoopRank` (`edxCarryForwardSources`, selected). `edxAllLoop` also holds
    `edxLoopPick`, a checkbox copying `stores` (`edxCarryForwardSources`, unselected, Other) that keeps the Creator's
    `item1..3`.
- **Copy matrix,** all copying `stores`:

  | Syntax | Mode | Checkbox | Radiogroup | Ranking | Loop (source) |
  | --- | --- | --- | --- | --- | --- |
  | `choicesFromQuestion` | all | `cfqAllPick` | `cfqAllOne` | `cfqAllRank` | `cfqAllLoop` (checkbox) |
  | `choicesFromQuestion` | selected | `cfqSelectedPick` | `cfqSelectedOne` | `cfqSelectedRank` | `cfqSelectedLoop` (ranking) |
  | `choicesFromQuestion` | unselected | `cfqUnselectedPick` | `cfqUnselectedOne` | `cfqUnselectedRank` | `cfqUnselectedLoop` (radiogroup) |
  | `edxCarryForwardSources` | all | `edxAllPick` | `edxAllOne` | `edxAllRank` | `edxAllLoop` (checkbox) |
  | `edxCarryForwardSources` | selected | `edxSelectedPick` | `edxSelectedOne` | `edxSelectedRank` | `edxSelectedLoop` (ranking) |
  | `edxCarryForwardSources` | unselected | `edxUnselectedPick` | `edxUnselectedOne` | `edxUnselectedRank` | `edxUnselectedLoop` (radiogroup) |

  `cfqTagPick` is a tagbox copying `stores` (`choicesFromQuestion`, all, None).

  The mode does not change the export: a copy publishes every choice its sources offer, whichever the respondent was
  shown, so the three rows of each syntax compile alike.
- **Other questions:** `household`, a dynamic panel without `loopSource`; `satisfaction` (matrix); `usage`
  (matrixdropdown); `contact` (multipletext); `comments` (text); `brandPool` and `traitPool`, calculated values in the
  result, and `regionLabel`, one left out.
- **The response:** loop panels in a different order from their iterations, keyed by `itemValue`; a None pick; an
  Other with its comment; an iteration left unanswered (`traitsLoop`, brand 1).

The submission is written by hand in the shape Hub stores (`itemValue` and `loopIndex` on each loop panel). It has not
been captured from a Hub submission yet.

### Known gaps the goldens pin

The goldens hold today's output, wrong parts included, so each fix shows up as a golden diff:

- **#1280, rank names in Crunch:** a rank variable is named by its choice value at top level (`Rank the brands you know
  -- 1`), and inside a loop not by its choice at all: `cfqLoopRank`'s three variables per iteration share one name, made
  unique with the alias (`... -- cfqAllLoop--1--cfqLoopRank--2`). An inline ranking gets the same; #1280 regroups each
  ranking into one array.
- **#1441, a choice the respondent was not shown:** a copy's cells are 1 or 0 for every choice (#1724); the empty cell
  for a choice never shown stays in #1441.
- **#1725, JSON keys:** the JSON export camel-cases question names (`aidedBrands`).
- **#1726, plain-text labels:** HTML in a label reaches the Shoji codebook as written (`hiddenBrands--1`), and loop
  titles keep `{panel.itemText}`.
- **#1160, data lists:** a copy of a data list has no choices to publish; no scenario has one yet.
- **#1441, stored values never offered:** a value a checkbox stores but its choices do not offer has no column and is
  not exported (`capped-ranking-loops`, `occasionPool` 1 and 6).
- **A 0 the respondent did not see, with its own choices (no scenario yet):** `choicesVisibleIf` or a choice's own
  `visibleIf` can hide a choice that still writes 0 (#1759); a question after a `complete` trigger ended the survey writes 0;
  a checkbox moved between the form's root and a loop across versions writes 0 in the place its version did not have it,
  because the check against the submission's version (#1758) matches the question's name, not where it sits (#1770).
  #1753 covers only question, panel and page visibility.

Known gap: a question loop over a selected-mode copy that offers its source's Other (#1763) has no panel for that
Other, so the answers in the panel the respondent saw for it are not exported.

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
