# Comparing a draft with the published form

## Check before publication

`POST /api/forms/{formId}/draft/checks` with `{}` requires `forms.view`.
It reads the same admitted draft as the comparison and does not save anything.
`draftVersion` is a decimal string; `contentHash` identifies its survey. Recheck after editing.
Missing or inaccessible forms return 404. Pending upgrades and missing mandatory check services return 503 Problem Details.

`outcome` is `block` for duplicate generated column keys or unknown element types,
`warn` when a warning requires acknowledgement, otherwise `pass`. An inclusive tenant
reporting limit is a warning only when exceeded. Use `findings[].key` unchanged for
acknowledgement, `blocking` to disable publication, and `requiresAck` for the warning
checkbox. `export/not-checked` is informational: opening the dialog does not check export names.

For publication, send `CheckFormDefinition` with `Publication` and the frozen tenant,
form, version, content hash and document. An unavailable result is an infrastructure
failure, never a content refusal or permission to commit. The export-name detector is
required before publication checking can return a complete report.

## Compare with the published form

`GET /api/forms/{formId}/draft/diff?against=published` requires `forms.view`.
The response describes changes **from published to draft**. It is display-only.

| Situation | `publishedNumber` | `changes` | Review |
| --- | --- | --- | --- |
| No active definition, including after editing the draft | `null` | `[]` | First version |
| An active definition with identical canonical JSON | Live version number | `[]` | No changes |
| The draft differs | Live version number | Ordered changes | Summary or JSON |

For changed documents the same response includes `publishedJson` and `draftJson`,
canonical JSON **strings**. Use these for a unified JSON view and to resolve page
and question titles for the summary. They are omitted for empty change lists.
The strong `ETag` is the current **draft** version, including `"0"` before the
first edit. Refetch when opening Review: the published definition may have
changed independently. The response is `Cache-Control: no-store`. The API reads
the live pointer, resolved number, draft, and pending-upgrade state in the same
database view and releases it before computing the diff.

```json
{
  "publishedNumber": 4,
  "changes": [
    {
      "kind": "changed",
      "path": "/pages/name:contact/title",
      "before": "Contact",
      "after": "Contact details"
    }
  ],
  "publishedJson": "{\"pages\":[{\"name\":\"contact\",\"title\":\"Contact\"}]}",
  "draftJson": "{\"pages\":[{\"name\":\"contact\",\"title\":\"Contact details\"}]}"
}
```

**Paths.** The root is `""`. Split other paths on `/` after the initial slash,
then unescape `~1` to `/` and `~0` to `~` in each segment. An object segment is
a literal property name. In an array:

- `name:<name>` identifies a page, element, template element or named matrix column, case-sensitive.
- `value:<JSON scalar>` identifies a choice, row, rating or value-based matrix column. Parse the
  selector payload with `JSON.parse` and compare scalar values by type and value.
  `value:1` and `value:"1"` identify different items; `1` and `1.0` denote the same number.
  If two values coincide as JavaScript numbers, or a number is outside the safe integer range,
  the collection uses positional selectors. Canonical documents retain the original numeric tokens.
- `index:<zero-based index>` addresses other ordered arrays, and the entire collection when either side has missing or duplicate identities.

For findings or page grouping, `pages/name:p/elements/name:q/choices/value:"yes"`
identifies page `p`, element `q`, choice `"yes"`; nested element paths retain every
panel. A property can follow the item selector, including a locale under a title.

**Change kinds.** `added` carries `after`, `removed` carries `before`, and `changed`
carries both, including literal JSON nulls. `moved` changes the position of a
keyed array member. For keyed insertions, removals and moves, `predecessorBefore`
and `predecessorAfter` identify the preceding sibling on the relevant side at
that step (unescaped selector, null or absent for the first position).
Renaming a uniquely named element yields a removal and an addition. A reorder
keeps a longest subsequence of common siblings in place, so moving a single
question from the beginning to the end produces one `moved` change for that question.

An invalid target or form ID returns 400; a missing or inaccessible tenant form
returns 404 after authorization. A pending legacy draft upgrade returns 503,
as on the draft read endpoint.
