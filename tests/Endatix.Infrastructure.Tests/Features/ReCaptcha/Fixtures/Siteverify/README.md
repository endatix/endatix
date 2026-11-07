# siteverify response fixtures

Response bodies from Google's `https://www.google.com/recaptcha/api/siteverify`. Each body is
used as recorded: no key and no token is stored here. A file marked **derived** is a recorded body
with one field removed, so the tests can cover a case that's hard to get from Google directly.

| File | Source | How it was obtained |
| --- | --- | --- |
| `v2-test-key.json` | recorded | A token from `grecaptcha.execute` with Google's published v2 test site key (reCAPTCHA FAQ, "automated tests"), verified with the matching published test secret. No score and no action: the shape a v2 key returns on a v3 integration. |

Recorded on 2026-11-07 from a page on `localhost`. The token went straight from the page to a
local script that called siteverify and saved only the response body.
