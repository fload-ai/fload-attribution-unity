#!/usr/bin/env node
// Writes the .meta file Unity needs beside every asset in this package. A
// package installed from git is immutable, so Unity will not create them and
// ignores an asset without one. GUIDs are derived from the package path, so
// rerunning gives the same files; existing .meta files are left alone unless
// --force is given. Folders and files whose names end in ~ or start with a dot
// are ignored by Unity and get none.
//   node Tools~/generate-meta.mjs [--force] [--check]
import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { extname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const force = process.argv.includes('--force');
const check = process.argv.includes('--check');
const missing = [];

const guid = (path) => createHash('md5').update(`com.fload.attribution/${path}`).digest('hex');
const tail = '  userData: \n  assetBundleName: \n  assetBundleVariant: \n';

const iosPlugin = (id) => `fileFormatVersion: 2
guid: ${id}
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 1
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any: 
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  - first:
      iPhone: iOS
    second:
      enabled: 1
      settings:
        AddToEmbeddedBinaries: false
        CPU: AnyCPU
        CompileFlags: 
        FrameworkDependencies: 
${tail}`;

function metaFor(path, isDirectory) {
  const id = guid(path);
  if (isDirectory) return `fileFormatVersion: 2\nguid: ${id}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n${tail}`;
  const ext = extname(path);
  if (path === 'package.json') return `fileFormatVersion: 2\nguid: ${id}\nPackageManifestImporter:\n  externalObjects: {}\n${tail}`;
  if (ext === '.cs') return `fileFormatVersion: 2\nguid: ${id}\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n${tail}`;
  if (ext === '.asmdef') return `fileFormatVersion: 2\nguid: ${id}\nAssemblyDefinitionImporter:\n  externalObjects: {}\n${tail}`;
  if (['.swift', '.mm', '.m', '.h', '.xcprivacy'].includes(ext)) return iosPlugin(id);
  if (ext === '.unity') return `fileFormatVersion: 2\nguid: ${id}\nDefaultImporter:\n  externalObjects: {}\n${tail}`;
  if (['.md', '.json', '.xml', '.txt'].includes(ext)) return `fileFormatVersion: 2\nguid: ${id}\nTextScriptImporter:\n  externalObjects: {}\n${tail}`;
  return `fileFormatVersion: 2\nguid: ${id}\nDefaultImporter:\n  externalObjects: {}\n${tail}`;
}

function walk(dir, { insideSample }) {
  for (const name of readdirSync(dir).sort()) {
    if (name.startsWith('.') || name.endsWith('.meta')) continue;
    const path = join(dir, name);
    const rel = relative(root, path);
    const isDirectory = statSync(path).isDirectory();
    // Samples~ is copied into the project on import; its files carry metas so the
    // scene keeps pointing at the sample script. Other ~ folders are tooling.
    if (name.endsWith('~')) {
      if (rel === 'Samples~') walk(path, { insideSample: true });
      continue;
    }
    write(rel, isDirectory);
    if (isDirectory) walk(path, { insideSample });
  }
}

function write(rel, isDirectory) {
  const metaPath = join(root, `${rel}.meta`);
  const expected = metaFor(rel, isDirectory);
  if (check) {
    if (!existsSync(metaPath)) missing.push(`${rel}.meta`);
    return;
  }
  if (existsSync(metaPath) && !force) return;
  writeFileSync(metaPath, expected);
}

walk(root, { insideSample: false });
// Unity reads the guid; make sure no two files share one.
if (check) {
  if (missing.length) {
    console.error(`Missing .meta files:\n  ${missing.join('\n  ')}`);
    process.exit(1);
  }
  console.log('Every asset has a .meta file.');
}
const seen = new Map();
const scan = (dir) => {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) { if (!name.startsWith('.')) scan(path); continue; }
    if (!name.endsWith('.meta')) continue;
    const id = /guid: ([0-9a-f]{32})/.exec(readFileSync(path, 'utf8'))?.[1];
    if (seen.has(id)) { console.error(`Duplicate guid ${id}: ${seen.get(id)} and ${path}`); process.exit(1); }
    seen.set(id, path);
  }
};
scan(root);
