// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import starlightImageZoom from 'starlight-image-zoom';
// starlightLinksValidator is installed but disabled for v0.1 — many cross-page links
// are still being added/renamed. Re-enable once content stabilises.

// https://astro.build/config
export default defineConfig({
  site: 'https://valio.dev',
  server: { host: '0.0.0.0' },

  // Alias /en/<path> to /<path> for users who type or share /en/ URLs.
  // English content lives at the site root (defaultLocale: 'root' below);
  // Astro generates per-route HTML stubs that 301-redirect /en/* to /*.
  redirects: {
    '/en': '/',
    '/en/': '/',
    '/en/[...slug]': '/[...slug]',
  },

  integrations: [
    starlight({
      title: 'ValIO Docs',
      description:
        'Type-safe HTTP clients, models and schemas for 12 languages from OpenAPI 3.x.',

      // Social links (Starlight ≥0.33 expects array form).
      social: [
        {
          icon: 'github',
          label: 'GitHub',
          href: 'https://github.com/valio/valio',
        },
      ],

      // "Edit this page" links point at the master branch in the docs-site folder.
      editLink: {
        baseUrl: 'https://github.com/valio/valio/edit/master/docs-site/',
      },

      lastUpdated: true,

      // Four locales. English uses Starlight's special `root` key — its files
      // live at /docs root (no /en/ prefix); other locales serve under their key.
      // /en/* URLs are redirected to root via the top-level `redirects:` map
      // so old links and search results still work.
      defaultLocale: 'root',
      locales: {
        root: { label: 'English', lang: 'en' },
        ru: { label: 'Русский', lang: 'ru' },
        de: { label: 'Deutsch', lang: 'de' },
        'zh-cn': { label: '简体中文', lang: 'zh-CN' },
      },

      // Code blocks: GitHub themes + wrap long lines by default.
      expressiveCode: {
        themes: ['github-dark', 'github-light'],
        defaultProps: {
          wrap: true,
        },
      },

      // Explicit sidebar groups. Reference/CLI/Languages/Configuration default
      // to collapsed so the landing TOC stays scannable.
      sidebar: [
        {
          label: 'Getting Started',
          items: [
            { label: 'Introduction', link: '/' },
            { label: 'Getting started', link: '/getting-started/' },
            { label: 'FAQ', link: '/faq/' },
          ],
        },
        {
          label: 'Frontends',
          autogenerate: { directory: 'frontends' },
        },
        {
          label: 'CLI Reference',
          autogenerate: { directory: 'cli' },
          collapsed: true,
        },
        {
          label: 'Web App',
          autogenerate: { directory: 'web' },
        },
        {
          label: 'Desktop App',
          autogenerate: { directory: 'desktop' },
        },
        {
          label: 'Languages',
          autogenerate: { directory: 'languages' },
          collapsed: true,
        },
        {
          label: 'Stacks',
          autogenerate: { directory: 'stacks' },
          collapsed: true,
        },
        {
          label: 'Configuration',
          autogenerate: { directory: 'configuration' },
          collapsed: true,
        },
        {
          label: 'Architecture',
          autogenerate: { directory: 'architecture' },
        },
        {
          label: 'Recipes',
          autogenerate: { directory: 'recipes' },
        },
        {
          label: 'Reference',
          autogenerate: { directory: 'reference' },
          collapsed: true,
        },
        {
          label: 'Contributing',
          autogenerate: { directory: 'contributing' },
        },
      ],

      // Plugins:
      //  - starlightLinksValidator: fails the build on broken internal links.
      //    errorOnFallbackPages=false: don't fail when a translated page is
      //    missing and falls back to English.
      //  - starlightImageZoom: click any image to enlarge.
      plugins: [
        starlightImageZoom(),
      ],
    }),
  ],
});
