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

## Cards over tables

The theme ships a small `edx-` component set. Reach for it before you reach for a Markdown table — tables render heavily here, wrap badly on narrow content columns, and developers skim past them.

| Use a… | When |
| --- | --- |
| **Card grid** (`edx-grid` + `edx-card`) | Two or three parallel things a reader picks between: components, providers, deployment targets |
| **Spec list** (`edx-specs`) | Labeled facts about one thing: versions, ports, required setup |
| **Link card** (`edx-linkcard`) | "Next steps" and cross-page navigation, 2–4 destinations |
| **Bullets or prose** | Anything with one dimension — a list of supported browsers is a list, not a table |
| **Table** | Genuine two-dimensional lookup or comparison, and nothing above fits |

Table rules when you do use one: three columns maximum, short cells, no code blocks or multi-sentence paragraphs inside a cell, and never for sequential steps — those are an ordered list or a single code block.

### Card markup

Copy this shape. `card` supplies the theme's border, radius, and hover; `edx-card` supplies the layout.

```mdx
<div className="edx-grid">
  <div className="card edx-card">
    <div className="edx-card__head">
      <Icon name="server" />
      <div>
        <p className="edx-card__eyebrow">Backend</p>
        <h3 className="edx-card__title">Endatix API</h3>
      </div>
    </div>
    <dl className="edx-specs">
      <dt>SDK</dt>
      <dd><a href="https://dotnet.microsoft.com/download/dotnet/10.0">.NET 10</a> — <code>10.0.100</code> or newer</dd>
      <dt>Dev URL</dt>
      <dd><code>{"https://localhost:5001"}</code></dd>
    </dl>
    <div className="edx-card__footer">One line of context that does not fit a spec row.</div>
  </div>
</div>
```

The icon is optional. Drop the `<Icon>` and the `edx-card__head` wrapper for an icon-less card — the eyebrow and title stack on their own.

Available classes, all defined under `DOC CARDS & SPEC LISTS` in `src/css/endatix-theme.css`:

- `edx-grid` — responsive auto-fit grid. Add `edx-grid--compact` for narrower tracks (three link cards in one row).
- `edx-card` — `edx-card__head` (icon + text row), `edx-card__eyebrow` (uppercase category label), `edx-card__title`, `edx-card__lede`, `edx-card__footer` (muted, bottom-aligned).
- `edx-specs` — `<dl>` rendered as a two-column table with rules between rows. Keep `<dt>` to one or two words so the label column stays narrow.
- `edx-pill` — small status chip. `edx-pill--required` for the brand-tinted "Required" variant.
- `edx-linkcard` — anchor card with an animated arrow: an optional `<Icon>`, then `edx-linkcard__title` + `edx-linkcard__desc`.

Add new classes to that same block with the `edx-` prefix rather than inlining `style={{...}}` in MDX. Page-local styles are the thing this set exists to replace.

### Icons

`<Icon name="server" />` renders the tinted tile in a card header. It is registered globally in `src/theme/MDXComponents.tsx`, so **no import in the page** — and it renders the tile, not a bare glyph, so never wrap it yourself.

- **Names come from the registry** in `src/components/Icon/index.tsx`, which maps kebab names onto [Lucide](https://lucide.dev) icons. To add one, add a named import and a registry entry — never a wildcard import or a dynamic `lucide-react[name]` lookup, both of which pull the whole ~1,500-icon set into the bundle. An unknown name throws at build time.
- **Default accent is brand blue.** `accent="violet" | "green" | "amber"` exists to separate peers that sit side by side (API vs Hub), not to decorate. Two cards that really are the same kind of thing — two database providers — keep the same icon and the same accent.
- **Icons are decorative**, marked `aria-hidden`; the title carries the meaning. Never use one as the only signal.
- Pick distinguishable glyphs. Two box-shaped icons next to each other read as one.

Anything added under `src/theme/` needs a `pnpm start` restart — swizzled components are not hot-reloaded, and the running dev server renders `Expected component \`Icon\` to be defined` until it is restarted.

### MDX gotchas these rules avoid

- **Keep each `<dt>`/`<dd>` on one line.** Multi-line JSX children are parsed as Markdown blocks and get wrapped in a `<p>`, which breaks `<p>` containers and adds stray margins.
- **Use `<div>`, not `<p>`, for any JSX text container** that might hold more than a word — same reason.
- **Wrap bare URLs in an expression**: `<code>{"https://localhost:5001"}</code>`. Plain text inside `<code>` still gets GFM autolinked, giving you a blue link inside a code span.
- **Explicit heading IDs (`## Title {#my-id}`) do not compile here.** Let the slugger derive the anchor from the heading text — ``### PostgreSQL `pg_trgm` extension`` yields `#postgresql-pg_trgm-extension`.
- **Headings are a public API.** Before renaming one, `grep -rn "#existing-anchor" docs` — other pages link to it and the build only warns on broken anchors.

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
