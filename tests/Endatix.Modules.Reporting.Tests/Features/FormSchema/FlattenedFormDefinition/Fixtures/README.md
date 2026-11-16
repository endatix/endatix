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
- `expected-codebook.json`: the format-neutral codebook it compiles to.
- `expected-shoji-codebook.json`: the Shoji codebook it exports to.

`ScenarioGoldenTests` runs every folder that has a `definition.json`, so a new scenario needs no new test.

| Scenario | Case |
| --- | --- |
| `copied-choices-selected-mode` | A radiogroup copies its choices from a checkbox with `choicesFromQuestion` in selected mode (#1163). |

Known gap: in selected mode a copy also offers the source's Other when the respondent picked it, and stores `other`.
The codebook publishes only the sources' own choices, so that answer has no category and its csv-shoji cell is empty.
`CopiedChoicesCrunchExportTests.CsvShoji_CopiedOtherInSelectedMode_HasNoCategoryUntil1544` pins this until #1544
publishes the source's Other as a special item.

### Adding a scenario

1. Write the definition: create `Scenarios/<name>/definition.json`. Use a kebab-case name for the case, not for a
   customer, and keep customer content out: obfuscate names, titles and choices.
2. Regenerate: remove `Skip` from
   `AllQuestionsCompileTests.AllQuestionsGoldenFixtures_Regenerate_WithCurrentPipeline_WritesExpectedFixtureFiles`, then
   run it:

   ```bash
   dotnet test tests/Endatix.Modules.Reporting.Tests --filter "FullyQualifiedName~AllQuestionsGoldenFixtures_Regenerate"
   ```

   It writes both expected files in the source tree, and rewrites the `AllQuestions/` goldens. Put `Skip` back.
3. Review: read both expected files as a reviewer would, check every label, type and category against the definition,
   and add the scenario to the table above. Commit the definition and both goldens together.
