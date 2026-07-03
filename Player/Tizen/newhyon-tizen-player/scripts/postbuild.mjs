import { cp, mkdir, readdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const sampleRoot = path.resolve(scriptDir, '..');
const distDir = path.join(sampleRoot, 'dist');
const outputName = 'NewHyOnTizenPlayer';
const extensionProjectDir = path.join(sampleRoot, outputName);
const legacyExtensionProjectDir = path.join(sampleRoot, 'Player');

const tizenProjectYaml = `# Project type [web_app, test_runner]
project_type: web_app

# Default profile, Tizen API version
profile: tv-samsung
api_version: "7.0"

# Build type [Debug/ Release/ Test]
build_type: Debug

# Output name for application
output_name: ${outputName}

# Output path for build
output_path: ""

# Enable size optimization of wgt for web workspace
opt: false

# Signing profile to be used for Tizen package signing
signing_profile: ""

# list of certs in web project (.trust-anchor)
trust_anchor: []

# list of files in web project (.html,.css etc) and resources
files:
__FILES__

# list of files to exclude based on the matched patterns
excludes:
  - Build/*
  - Debug/*
  - Directory.Build.targets
  - \\.Build/.*
  - \\.buildResult/.*
  - \\.csproj
  - \\.externalToolBuilders/.*
  - \\.gn
  - \\.manifest\\.tmp
  - \\.obj/.*
  - \\.package/.*
  - \\.project
  - \\.rds_delta
  - \\.sdk_delta.info
  - \\.settings/.*
  - \\.sign/.*
  - \\.tizen-ui-builder-tool.xml
  - \\.tproject
  - \\.wgt
  - author-signature\\.xml
  - signature.*\\.xml
  - webUnitTest/*

# project dependencies
deps: []
`;

async function listFiles(directory, relative = '') {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries.sort((left, right) => left.name.localeCompare(right.name))) {
    if (entry.isDirectory() && (entry.name === 'Build' || entry.name === 'Debug')) {
      continue;
    }

    const entryRelative = relative ? path.posix.join(relative, entry.name) : entry.name;
    const entryPath = path.join(directory, entry.name);

    if (entry.isDirectory()) {
      files.push(...(await listFiles(entryPath, entryRelative)));
      continue;
    }

    if (
      path.posix.basename(entryRelative) === '.DS_Store' ||
      entryRelative === 'tizen_web_project.yaml' ||
      entryRelative === '.manifest.tmp' ||
      entryRelative === 'author-signature.xml' ||
      /^signature\d*\.xml$/.test(path.posix.basename(entryRelative)) ||
      entryRelative.endsWith('.wgt')
    ) {
      continue;
    }

    files.push(entryRelative);
  }

  return files;
}

async function main() {
  await mkdir(distDir, { recursive: true });
  const files = await listFiles(distDir);
  const renderedFiles = files.map((file) => `  - ${file}`).join('\n');
  await writeFile(path.join(distDir, 'tizen_web_project.yaml'), tizenProjectYaml.replace('__FILES__', renderedFiles), 'utf8');

  await rm(legacyExtensionProjectDir, { recursive: true, force: true });
  await rm(extensionProjectDir, { recursive: true, force: true });
  await cp(distDir, extensionProjectDir, { recursive: true });
}

await main();
