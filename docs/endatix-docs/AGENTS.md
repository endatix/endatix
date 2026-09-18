# Endatix Docs — Agent runbook

Docusaurus 3 site for [docs.endatix.com](https://docs.endatix.com). Content lives in `docs/`,
theme tokens and shared classes in `src/css/endatix-theme.css`.

This file is the **local setup and run** guide for this package. It is valid in a standalone
[endatix/endatix](https://github.com/endatix/endatix) checkout. Do not link outside this git
root (no `endatix-saas` `.agents/` or sibling `docs/` inventories).

## Setup

From this directory (`docs/endatix-docs` in the OSS repo):

```bash
pnpm install
```

Node version: `package.json` `engines`. Do not invent a second docs toolchain.

## Run

| Command | What |
| --- | --- |
| `pnpm start` | Dev server at [http://localhost:3005](http://localhost:3005/) (see `README.md`) |
| `pnpm build` | Production build. **Always run before you call a docs change done.** |

`onBrokenLinks: "throw"` — a bad link fails the build. The HTML minifier surfaces invalid markup
as SSG warnings. Zero warnings is the bar.

Most content edits hot-reload. **Restart `pnpm start` after changing `src/theme/`** (MDX
component scope).

Hub (Endatix Hub) is a **separate** repository — not this checkout. Do not assume a sibling
`hub/` folder.

## Theme preview

Navbar **light / dark** toggle on `http://localhost:3005` is for checking docs chrome and MDX
components.

Do not add a second `:root` / `[data-theme="dark"]` block. Drawing tokens (`--edx-sheep-*`) live
in the existing light/dark blocks of `endatix-theme.css`. Card/grid classes (`edx-grid`,
`edx-card`, …) are under `DOC CARDS & SPEC LISTS` — articles use MDX tags, not pasted class
markup.

## Site chrome (not article MDX)

- Homepage-only: `HomepageFeatures`, `CallToAction`.
- 404 sheep: `Sheep` (`src/components/Sheep`). Swizzle `@docusaurus/theme-classic`
  **`NotFound/Content` only** into `src/theme/NotFound/Content` (keep the default Layout wrapper).
  Rules under `.edx-notfound` / `.edx-sheep`.
- Hosting: `staticwebapp.config.json` rewrites HTTP 404 → `/404.html`. Deploy copies that file
  into `build/` — never use `navigationFallback` to `index.html` for this site.

MDX tags are registered in [`src/theme/MDXComponents.tsx`](src/theme/MDXComponents.tsx). Adding a
reusable article component: register it there and add the component under `src/components/`.
PascalCase tags only (MDX v3 treats lowercase as HTML). No import in the page.
