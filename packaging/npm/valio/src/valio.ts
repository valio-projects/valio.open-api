#!/usr/bin/env node
// ValIO CLI npm shim — resolves the platform-specific binary from the
// matching @valio/cli-<platform>-<arch> optional dependency and spawns it,
// forwarding argv, stdio, and the exit code.

import { spawn } from "node:child_process";
import { createRequire } from "node:module";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);

interface PlatformInfo {
  /** npm scoped package name, e.g. "@valio/cli-linux-x64". */
  pkg: string;
  /** File name of the executable inside the platform package. */
  exe: string;
}

/**
 * Maps Node's process.platform/arch to the matching @valio/cli-* package
 * and the executable file name shipped inside it.
 */
function resolvePlatform(): PlatformInfo | null {
  const platform = process.platform;
  const arch = process.arch;
  const exe = platform === "win32" ? "valio.exe" : "valio";

  // Keep this table in sync with packaging/npm/platforms/* and the
  // optionalDependencies in package.json.
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

/**
 * Resolves the absolute path to the platform binary by locating the
 * package.json of the corresponding @valio/cli-* package and joining
 * the executable name to its directory.
 *
 * Returns null if the package is not installed (e.g. user ran
 * `npm install --no-optional`) or if the binary file is missing.
 */
function resolveBinary(info: PlatformInfo): string | null {
  try {
    const manifestPath = require.resolve(`${info.pkg}/package.json`);
    const binaryPath = join(dirname(manifestPath), info.exe);
    return existsSync(binaryPath) ? binaryPath : null;
  } catch {
    return null;
  }
}

function printMissingBinaryError(info: PlatformInfo | null): void {
  const platform = `${process.platform}-${process.arch}`;
  process.stderr.write(
    [
      `valio: failed to locate the native CLI binary for ${platform}.`,
      "",
      info
        ? `Expected to find it via the optional dependency '${info.pkg}'.`
        : `No prebuilt npm package exists for this platform (${platform}).`,
      "",
      "Possible causes:",
      "  - npm was run with --no-optional or --omit=optional.",
      "  - The install failed silently and the platform package is missing.",
      "  - You are on a platform we do not ship a prebuilt binary for.",
      "",
      "Fixes:",
      "  - Reinstall:  npm install -g valio",
      "  - Or install via .NET:  dotnet tool install --global ValIO.Cli",
      "",
    ].join("\n"),
  );
}

function main(): void {
  const info = resolvePlatform();
  if (!info) {
    printMissingBinaryError(null);
    process.exit(1);
  }

  const binary = resolveBinary(info);
  if (!binary) {
    printMissingBinaryError(info);
    process.exit(1);
  }

  const child = spawn(binary, process.argv.slice(2), {
    stdio: "inherit",
    // Don't use a shell — pass args verbatim to the native binary.
    shell: false,
    windowsHide: true,
  });

  child.on("error", (err) => {
    process.stderr.write(`valio: failed to spawn '${binary}': ${err.message}\n`);
    process.exit(1);
  });

  child.on("exit", (code, signal) => {
    if (signal) {
      // Re-raise the signal so the caller sees the same termination cause.
      process.kill(process.pid, signal);
      return;
    }
    process.exit(code ?? 0);
  });

  // Forward common termination signals to the child so Ctrl-C etc. work.
  const forward: NodeJS.Signals[] = ["SIGINT", "SIGTERM", "SIGHUP"];
  for (const sig of forward) {
    process.on(sig, () => {
      if (!child.killed) child.kill(sig);
    });
  }
}

// Touch fileURLToPath to keep the import in case future logic needs __dirname.
void fileURLToPath;

main();
