# Comparing a draft with the published form

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
changed independently. The response is `Cache-Control: no-store`.

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
- `value:<JSON scalar>` identifies a choice, row, rating or value-based matrix column. For example
  `value:1` and `value:"1"` identify different items.
- `index:<zero-based index>` addresses other ordered arrays, and the entire collection when either side has missing or duplicate identities.

For findings or page grouping, `pages/name:p/elements/name:q/choices/value:"yes"`
identifies page `p`, element `q`, choice `"yes"`; nested element paths retain every
panel. A property can follow the item selector, including a locale under a title.

**Change kinds.** `added` carries `after`, `removed` carries `before`, and `changed`
carries both, including literal JSON nulls. `moved` changes the position of a
keyed array member. For keyed insertions, removals and moves, `predecessorBefore`
and `predecessorAfter` identify the preceding sibling on the relevant side at
that step (unescaped selector, null or absent for the first position).
Renaming a uniquely named element yields a removal and an addition.

An invalid target or form ID returns 400; a missing or inaccessible tenant form
returns 404 after authorization. A pending legacy draft upgrade returns 503,
as on the draft read endpoint.
