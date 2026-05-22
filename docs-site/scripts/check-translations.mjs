#!/usr/bin/env node
// check-translations.mjs
//
// Walks src/content/docs/ to enumerate every English page (anything not under
// _snippets/, ru/, de/, zh-cn/), then checks whether each translated locale
// has a matching file at the same relative path.
//
// Prints a Markdown coverage table to stdout. Always exits 0 — this is a
// reporting tool, not a gate.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const DOCS_ROOT = path.resolve(__dirname, '..', 'src', 'content', 'docs');
const LOCALES = ['ru', 'de', 'zh-cn'];
const EXCLUDED_DIRS = new Set(['_snippets', ...LOCALES]);

/**
 * Recursively walk a directory and yield relative file paths (POSIX
 * separators) for every .md / .mdx file found, skipping any directory whose
 * name is in `excludeAtRoot` (only checked at the top level).
 */
function* walk(dir, baseDir, excludeAtRoot) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      const isAtRoot = dir === baseDir;
      if (isAtRoot && excludeAtRoot.has(entry.name)) continue;
      yield* walk(path.join(dir, entry.name), baseDir, excludeAtRoot);
    } else if (entry.isFile() && /\.(md|mdx)$/.test(entry.name)) {
      const full = path.join(dir, entry.name);
      yield path.relative(baseDir, full).split(path.sep).join('/');
    }
  }
}

function hasTranslation(locale, relPath) {
  return fs.existsSync(path.join(DOCS_ROOT, locale, relPath));
}

function pct(n, total) {
  if (total === 0) return '0%';
  return Math.round((n / total) * 100) + '%';
}

if (!fs.existsSync(DOCS_ROOT)) {
  console.error(`docs root not found: ${DOCS_ROOT}`);
  process.exit(0);
}

const pages = [...walk(DOCS_ROOT, DOCS_ROOT, EXCLUDED_DIRS)].sort();

const counts = { en: pages.length, ru: 0, de: 0, 'zh-cn': 0 };
const rows = [];

for (const page of pages) {
  const cells = { en: true };
  for (const locale of LOCALES) {
    const present = hasTranslation(locale, page);
    cells[locale] = present;
    if (present) counts[locale]++;
  }
  rows.push({ page, cells });
}

const total = pages.length;

console.log('# ValIO docs translation coverage');
console.log('');
console.log(`Total English pages: **${total}**`);
console.log('');
console.log('| Page | en | ru | de | zh-cn |');
console.log('|------|----|-----|-----|-------|');
for (const { page, cells } of rows) {
  const mark = (v) => (v ? 'OK' : 'MISSING');
  console.log(
    `| ${page} | ${mark(cells.en)} | ${mark(cells.ru)} | ${mark(cells.de)} | ${mark(cells['zh-cn'])} |`
  );
}
console.log(
  `| **Coverage** | ${pct(counts.en, total)} | ${pct(counts.ru, total)} | ${pct(counts.de, total)} | ${pct(counts['zh-cn'], total)} |`
);

process.exit(0);
