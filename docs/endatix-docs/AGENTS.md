# Endatix Docs — Agent Instructions

Docusaurus 3 site for [docs.endatix.com](https://docs.endatix.com). Content lives in `docs/`, theme tokens and shared classes in `src/css/endatix-theme.css`.

**Always run `pnpm build` before you call a docs change done.** `onBrokenLinks: "throw"` means a bad link fails the build, and the HTML minifier surfaces invalid markup as SSG warnings. Zero warnings is the bar.

## Voice

Write for a developer who is mid-task and wants to leave the page as fast as possible.

- **Short and concrete over complete.** One accurate sentence beats a paragraph that covers every case. If a section can be cut without losing a fact, cut it.
- **No marketing.** Never "powerful", "seamless", "effortlessly", "robust", "simply", "just", "blazing fast". Never sell a feature inside a reference page — say what it does and what it needs.
- **No filler verbosity either.** Skip "In this section we will…", "As you can see", restating the heading in the first line, and summary paragraphs that repeat what was just said.
- **Stay inside the page's job.** A requirements or reference page states what is needed and links to the page that explains how to get it. It does not teach provisioning steps, CLI flags, or verification queries — the moment a paragraph starts walking through another product's console, it belongs on the deployment or configuration page instead.
- **Second person, present tense, active voice.** "Create the extension before running migrations", not "The extension should be created".
- **Lead with the constraint, then the reason.** "SQL Server 2025 or later — migrations declare `json` columns, a type added in 2025."
- **Name the failure.** When something breaks, quote the actual error and say what to run to check. Exact error text is more useful than a warning sentence.
- **Every version, port, env var, and file path is verified against the repo** — `global.json`, `Directory.Build.props`, `package.json` engines, `launchSettings.json`, migrations, Compose files. Never carry a number over from an older doc without re-checking it.
- Sentence case for headings. American English. Bold for the thing a reader scans for, not for emphasis.

## Components (`src/components`)

Registered globally in [`src/theme/MDXComponents.tsx`](src/theme/MDXComponents.tsx) ([Docusaurus MDX scope](https://docusaurus.io/docs/markdown-features/react#mdx-component-scope)). PascalCase tags only — MDX v3 treats lowercase as HTML. **No import in the page.** Restart `pnpm start` after changing `src/theme/`.

| Tag | Path | Use |
| --- | --- | --- |
| `CardGrid` | `src/components/CardGrid` | Responsive grid. `compact` for next-step rows. |
| `Card` | `src/components/Card` | Generic card: `eyebrow`, `title`, optional `icon` / `accent` / `lede` / `footer`. Children are free-form. |
| `Specs` / `Spec` | `src/components/Specs`, `Spec` | Label/value rows inside a `Card`. `<Spec label="SDK">…</Spec>`. |
| `LinkCard` | `src/components/LinkCard` | Next-step link: `to`, `title`, `description`, optional `icon`. |
| `Pill` | `src/components/Pill` | Status chip. `required` for the brand tint. |
| `Settings` / `Setting` | `src/components/Settings`, `Setting` | Configuration-key reference — env vars, `appsettings.json` paths. One `Setting` per key (`name` is the anchor). Optional: `required`, `default`, `note`, `status` (`deprecated` \| `removed`), `since`, `replacedBy`. Children are the explanation. Never comma-join names. |
| `Icon` | `src/components/Icon` | Tinted Lucide tile. Used by Card/LinkCard; also valid in MDX. |

Homepage-only (not MDX): `HomepageFeatures` (feature row), `CallToAction` (landing CTA).

Theme-only (not MDX): `Sheep` (`src/components/Sheep`) — decorative CSS sheep for the 404. Swizzle `@docusaurus/theme-classic` **`NotFound/Content` only** into `src/theme/NotFound/Content` (keep the default Layout wrapper). Drawing tokens (`--edx-sheep-*`) live in the light/dark blocks of `endatix-theme.css`; rules under `.edx-notfound` / `.edx-sheep`. Do not add a second `:root` / `[data-theme="dark"]` block.

Hosting: `staticwebapp.config.json` rewrites HTTP 404 → `/404.html`. Deploy copies that file into `build/` — never use `navigationFallback` to `index.html` for this site.

Classes (`edx-grid`, `edx-card`, …) live in `src/css/endatix-theme.css` under `DOC CARDS & SPEC LISTS`. Prefer the tags above; do not paste the class markup into pages.

```mdx
<CardGrid>
  <Card eyebrow="Backend" title="Endatix API" icon="server" footer="One line of context.">
    <Specs>
      <Spec label="SDK"><a href="https://dotnet.microsoft.com/download/dotnet/10.0">.NET 10</a></Spec>
    </Specs>
  </Card>
</CardGrid>
```

Reference page: `docs/getting-started/system-requirements.mdx`.

Prefer these tags over Markdown tables (tables wrap badly here). Browsers and one-dimension lists stay bullets. Real 2D lookup: a table, three columns max.

A list of configuration keys is never a table — each row needs a sentence or two of explanation, which a Markdown column wraps into a wall of text. Use `Settings` / `Setting`; every key then gets its own anchor for deep links. Reference page: `docs/developers/hub/environment.mdx`.

### Icons

`<Icon name="server" />` (or `icon="server"` on Card/LinkCard) maps kebab names onto [Lucide](https://lucide.dev) in `src/components/Icon/index.tsx`. Add a **named** import + registry entry — never a wildcard or `lucide-react[name]` (pulls the whole set). Unknown names throw at build time.

- **Default accent is brand blue.** `accent="violet" | "green" | "amber"` separates peers (API vs Hub), not decoration. Two database providers share icon and accent.
- Decorative (`aria-hidden`); the title carries meaning.

### MDX gotchas these rules avoid

- **Spec rows:** use `<Spec>` — do not hand-write `<dt>`/`<dd>` in MDX (Markdown wraps them in `<p>`).
- **Use `<div>`, not `<p>`, for any JSX text container** that might hold more than a word.
- **Wrap bare URLs in an expression**: `<code>{"https://localhost:5001"}</code>`. Plain text inside `<code>` still gets GFM autolinked.
- **Explicit heading IDs (`## Title {#my-id}`) do not compile here.** Slug from heading text — ``### PostgreSQL `pg_trgm` extension`` → `#postgresql-pg_trgm-extension`.
- **Headings are a public API.** Before renaming one, `grep -rn "#existing-anchor" docs`.

## Page shape

```yaml
---
sidebar_position: 2
title: System Requirements
description: One sentence a search result can show. Name the concrete things — runtimes, versions, extensions.
---
```

H1 matches `title`. Open with one or two sentences that say what the page covers and what it assumes — no preamble. Close with a "Next steps" link-card grid when the reader has an obvious next move.

Cross-link with absolute paths (`/docs/configuration/settings/persistence-settings`), not relative ones. Use admonitions (`:::tip`, `:::warning`) for a genuine trap, not to decorate a normal paragraph.

`docs/getting-started/system-requirements.mdx` is the current reference for all of the above.
