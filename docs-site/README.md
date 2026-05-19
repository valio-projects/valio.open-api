# ValIO documentation site

Starlight (Astro) site that serves the public ValIO documentation in
English, Russian, German, and Simplified Chinese.

## Local development

```bash
cd docs-site
npm install
npm run dev          # http://localhost:4321
npm run build        # static site -> dist/
npm run preview
```

## Layout

```
src/content/docs/
├── en/             # English (default locale)
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
    ├── contributing/
    └── faq.md
```

Untranslated pages automatically fall back to English via Starlight's
locale fallback mechanism.

## Deployment

A GitHub Actions workflow (`.github/workflows/docs.yml` in the repo root)
publishes the built `dist/` folder to GitHub Pages on every push to
`master` that touches `docs-site/**`.
