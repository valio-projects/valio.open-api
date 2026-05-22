# ValIO documentation site

Astro 5 + Starlight site that serves the public ValIO documentation in
English, Russian, German, and Simplified Chinese.

## Prerequisites

- Node.js **22+** (Active LTS) — `node --version`
- npm **10+** — bundled with Node 22

## Local development

```bash
cd docs-site
npm install
npm run dev          # http://localhost:4321
```

## Useful scripts

| Script                | What it does                                                  |
| --------------------- | ------------------------------------------------------------- |
| `npm run dev`         | Start the dev server on http://localhost:4321                 |
| `npm run build`       | Build the static site into `dist/`                            |
| `npm run preview`     | Preview the production build locally                          |
| `npm run clean`       | Remove `dist`, `.astro`, and `node_modules/.cache`            |
| `npm run lint:md`     | Lint Markdown / MDX with `markdownlint-cli2`                  |
| `npm run lint:links`  | Build + validate internal links (`starlight-links-validator`) |
| `npm run format`      | Format `src/**/*` with Prettier                               |
| `npm run format:check`| Check formatting without writing                              |
| `npm run i18n:report` | Print a Markdown coverage table for all four locales          |

## Windows helpers

The repo ships seven `.bat` wrappers so you can double-click or run them from
`cmd.exe` without remembering the npm script name:

- `install.bat`
- `dev.bat`
- `build.bat`
- `preview.bat`
- `clean.bat`
- `lint.bat` (runs `lint:md` then `lint:links`)
- `format.bat`

Each `.bat` pauses on error so the console window stays open.

## Layout

```
src/content/docs/
├── (en pages live at the root)
├── ru/             # Русский
├── de/             # Deutsch
└── zh-cn/          # 简体中文
    ├── index.mdx
    ├── getting-started.md
    ├── cli/
    ├── web/
    ├── desktop/
    ├── languages/
    ├── configuration/
    ├── architecture/
    ├── recipes/
    ├── reference/
    ├── frontends/
    ├── contributing/
    └── faq.md
```

Untranslated pages automatically fall back to English via Starlight's locale
fallback mechanism. Run `npm run i18n:report` to see what's missing.

## Deployment

A GitHub Actions workflow (`.github/workflows/docs.yml` in the repo root)
publishes the built `dist/` folder to GitHub Pages on every push to `master`
that touches `docs-site/**`.
