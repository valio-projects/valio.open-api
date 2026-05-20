import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  site: 'https://valio.dev',
  integrations: [
    starlight({
      title: 'ValIO',
      description: 'OpenAPI code generator for TypeScript, C#, Java, Kotlin, Python, PHP, Swift, and Unreal-flavored C++.',
      defaultLocale: 'en',
      locales: {
        en:      { label: 'English',  lang: 'en' },
        ru:      { label: 'Русский',  lang: 'ru' },
        de:      { label: 'Deutsch',  lang: 'de' },
        'zh-cn': { label: '简体中文', lang: 'zh-CN' }
      },
      lastUpdated: true,
      sidebar: [
        { label: 'Getting started', link: '/getting-started/' },
        { label: 'CLI',           autogenerate: { directory: 'cli' } },
        { label: 'Web',           autogenerate: { directory: 'web' } },
        { label: 'Desktop',       autogenerate: { directory: 'desktop' } },
        { label: 'Languages',     autogenerate: { directory: 'languages' } },
        { label: 'Configuration', autogenerate: { directory: 'configuration' } },
        { label: 'Architecture',  autogenerate: { directory: 'architecture' } },
        { label: 'Contributing',  autogenerate: { directory: 'contributing' } },
        { label: 'FAQ',           link: '/faq/' }
      ],
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/your-org/valio' }
      ]
    })
  ]
});
