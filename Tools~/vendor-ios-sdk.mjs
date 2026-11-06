#!/usr/bin/env node
// Copies the Swift sources of fload-attribution-ios at a pinned tag into this
// package, or (with --check) fails when the vendored copy differs from that tag
// byte for byte. Usage:
//   node Tools~/vendor-ios-sdk.mjs <tag> <destination> [--check] [--prefix=<p>]
// --prefix renames each X.swift to <p>X.swift (contents unchanged), for builds
// that put every plugin's Swift into one module, where two files with the same
// name fail the build (Unity's UnityFramework target).
// The CocoaPods trunk turns read-only on 2 December 2026, so the iOS SDK is
// never a pod. Packages that build through CocoaPods (or Unity's generated
// Xcode project) compile a vendored copy, and this script keeps it honest.
import { execFileSync } from 'node:child_process';
import { cpSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative } from 'node:path';

const REPO = 'https://github.com/fload-ai/fload-attribution-ios.git';
const args = process.argv.slice(2);
const prefixArg = args.find((a) => a.startsWith('--prefix='));
const prefix = prefixArg ? prefixArg.slice('--prefix='.length) : '';
const [tag, destination, flag] = args.filter((a) => a !== prefixArg);
const rename = (file) => {
  if (!prefix || !file.endsWith('.swift')) return file;
  const parts = file.split('/');
  parts[parts.length - 1] = prefix + parts[parts.length - 1];
  return parts.join('/');
};
if (!tag || !destination) {
  console.error('usage: vendor-ios-sdk.mjs <tag> <destination> [--check]');
  process.exit(2);
}

const checkout = mkdtempSync(join(tmpdir(), 'fload-ios-sdk-'));
try {
  execFileSync('git', ['-c', 'advice.detachedHead=false', 'clone', '--quiet', '--depth', '1', '--branch', tag, REPO, checkout], { stdio: 'inherit' });
  const source = join(checkout, 'Sources', 'FloadAttribution');
  if (flag === '--check') {
    const problems = compare(source, destination);
    if (problems.length > 0) {
      console.error(`Vendored iOS SDK differs from fload-attribution-ios ${tag}:`);
      for (const problem of problems) console.error(`  ${problem}`);
      process.exit(1);
    }
    console.log(`Vendored iOS SDK matches fload-attribution-ios ${tag}.`);
  } else {
    rmSync(destination, { recursive: true, force: true });
    for (const file of files(source)) {
      const target = join(destination, rename(file));
      mkdirSync(dirname(target), { recursive: true });
      cpSync(join(source, file), target);
    }
    console.log(`Vendored fload-attribution-ios ${tag} into ${destination}.`);
  }
} finally {
  rmSync(checkout, { recursive: true, force: true });
}

function files(root) {
  const out = [];
  const walk = (dir) => {
    for (const name of readdirSync(dir)) {
      if (name === '.DS_Store' || name.endsWith('.meta')) continue; // Unity writes .meta files beside each asset
      const path = join(dir, name);
      if (statSync(path).isDirectory()) walk(path);
      else out.push(relative(root, path));
    }
  };
  walk(root);
  return out.sort();
}

function compare(expectedRoot, actualRoot) {
  const problems = [];
  let actual = [];
  try {
    actual = files(actualRoot);
  } catch {
    return [`${actualRoot} is missing`];
  }
  const expected = files(expectedRoot);
  const renamed = expected.map(rename);
  expected.forEach((file, i) => {
    const target = renamed[i];
    if (!actual.includes(target)) problems.push(`missing ${target}`);
    else if (!readFileSync(join(expectedRoot, file)).equals(readFileSync(join(actualRoot, target)))) problems.push(`changed ${target}`);
  });
  for (const file of actual) if (!renamed.includes(file)) problems.push(`extra ${file}`);
  return problems;
}
