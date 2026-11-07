# siteverify response fixtures

Response bodies from Google's `https://www.google.com/recaptcha/api/siteverify`. No key and no
token is stored here. A `.derived.json` file is a recorded body with one field removed, for a case
that's hard to get from Google directly; every other file is used as recorded.

| File | Source | How it was obtained |
| --- | --- | --- |
| `v2-test-key.json` | recorded | A token from `grecaptcha.execute` with Google's published v2 test site key (reCAPTCHA FAQ, "automated tests"), verified with the matching test secret. No score and no action: what a v2 key returns on a v3 integration. |
| `v3-submit-form.json` | recorded | A v3 key on `localhost`, `grecaptcha.execute` with action `submit_form` (the action Hub sends). |
| `v3-other-action.json` | recorded | The same v3 key, action `sign_in`. |
| `v3-token-reused.json` | recorded | The `v3-submit-form` token sent to siteverify a second time: `timeout-or-duplicate`. |
| `v3-wrong-secret.json` | recorded | A fresh v3 token sent with a made-up secret. Google answers `invalid-input-response`, not `invalid-input-secret`. |
| `v3-submit-form-without-score.derived.json` | derived | `v3-submit-form.json` without `score`. |
| `v3-submit-form-without-action.derived.json` | derived | `v3-submit-form.json` without `action`. |

Recorded on 2026-11-07 from a page on `localhost`. The page posted each token to a local script,
which called siteverify and saved only the response body.
