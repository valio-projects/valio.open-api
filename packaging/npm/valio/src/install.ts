// postinstall: makes the platform binary executable on Unix-like systems.
//
// npm installs only the @valio/cli-* optional dependency that matches the
// host's os/cpu. After install, the file ships without the executable bit on
// non-Windows hosts because npm strips perms during pack/unpack. We chmod 755
// here so `valio` works without the user having to fix it themselves.
//
// On Windows, .exe needs no chmod and PATHEXT handles it.
//
// This script must NEVER exit non-zero. A failed postinstall would break
// `npm install` for users who legitimately don't need the binary right away
// (e.g. CI caches, no-op installs) and there's no recoverable action they
// can take from here.

import { chmodSync, existsSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";

const require = createRequire(import.meta.url);

interface PlatformInfo {
  pkg: string;
  exe: string;
}

function resolvePlatform(): PlatformInfo | null {
  const platform = process.platform;
  const arch = process.arch;
  const exe = platform === "win32" ? "valio.exe" : "valio";

  const table: Record<string, Record<string, string>> = {
    win32:  { x64:   "@valio/cli-win32-x64" },
    linux:  { x64:   "@valio/cli-linux-x64",
              arm64: "@valio/cli-linux-arm64" },
    darwin: { x64:   "@valio/cli-darwin-x64",
              arm64: "@valio/cli-darwin-arm64" },
  };

  const pkg = table[platform]?.[arch];
  return pkg ? { pkg, exe } : null;
}

function warn(message: string): void {
  // Prefix so it's obvious where the message comes from in `npm install`
  // output, but stay on stderr and don't exit non-zero.
  process.stderr.write(`valio (postinstall): ${message}\n`);
}

function main(): void {
  const info = resolvePlatform();
  if (!info) {
    warn(
      `no prebuilt binary exists for ${process.platform}-${process.arch}. ` +
        "You can still install via 'dotnet tool install --global ValIO.Cli'.",
    );
    return;
  }

  let manifestPath: string;
  try {
    manifestPath = require.resolve(`${info.pkg}/package.json`);
  } catch {
    warn(
      `optional dependency '${info.pkg}' was not installed. ` +
        "Re-run 'npm install valio' or pass --include=optional if you previously omitted optionals.",
    );
    return;
  }

  const binary = join(dirname(manifestPath), info.exe);
  if (!existsSync(binary)) {
    warn(
      `expected binary '${binary}' is missing from '${info.pkg}'. ` +
        "The platform package may be corrupt — try reinstalling.",
    );
    return;
  }

  if (process.platform === "win32") {
    // .exe files don't need an executable bit on Windows.
    return;
  }

  try {
    chmodSync(binary, 0o755);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    warn(`failed to chmod '${binary}': ${msg}`);
  }
}

main();
