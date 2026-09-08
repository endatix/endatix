# Embed host assets

Markup, styles and script for `GET /dev/embed-host` (see `../EmbedHostPage.cs`).

Kept as real `.html` / `.css` / `.js` files so editors give syntax highlighting, formatting and
linting; they are embedded resources, inlined into a single response at render time. There is no
static-file route and no second request.

`__UPPER_SNAKE__` tokens are the substitution points. `EmbedHostPage` fills every one of them and
HTML-encodes any value that came from the query string.
