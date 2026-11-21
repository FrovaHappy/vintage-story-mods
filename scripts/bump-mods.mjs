import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const MOD_CATALOG = {
  seraphui: {
    modId: "seraphui",
    name: "Seraph UI",
    dir: "mods/system-ui"
  },
  tradersoverhaul: {
    modId: "tradersoverhaul",
    name: "Traders Overhaul",
    dir: "mods/comerciantes"
  },
  seraphqol: {
    modId: "seraphqol",
    name: "Seraph QoL",
    dir: "mods/seraph-qol"
  }
};

const MOD_ORDER = ["seraphui", "tradersoverhaul", "seraphqol"];
const VALID_BUMPS = new Set(["major", "minor", "fix", "dev"]);

/**
 * Computes the next SemVer string for a mod given its current version and bump kind.
 */
export function bumpSemVer(currentVersion, bumpType) {
  if (!VALID_BUMPS.has(bumpType)) {
    throw new Error(`Unsupported bump type: ${bumpType}`);
  }

  const match = String(currentVersion).trim().match(/^(\d+)\.(\d+)\.(\d+)(?:-dev\.(\d+))?$/);
  if (!match) {
    throw new Error(`Invalid SemVer version: "${currentVersion}"`);
  }

  const major = Number(match[1]);
  const minor = Number(match[2]);
  const patch = Number(match[3]);
  const hasDev = match[4] !== undefined;
  const devNum = hasDev ? Number(match[4]) : -1;

  if (bumpType === "dev") {
    return hasDev
      ? `${major}.${minor}.${patch}-dev.${devNum + 1}`
      : `${major}.${minor}.${patch + 1}-dev.0`;
  }

  if (bumpType === "fix") {
    return hasDev ? `${major}.${minor}.${patch}` : `${major}.${minor}.${patch + 1}`;
  }

  if (bumpType === "minor") {
    return hasDev && patch === 0
      ? `${major}.${minor}.0`
      : `${major}.${minor + 1}.0`;
  }

  // major
  return hasDev && minor === 0 && patch === 0
    ? `${major}.0.0`
    : `${major + 1}.0.0`;
}

/**
 * Extracts non-empty changelog sections containing at least one bullet entry, or returns null.
 */
export function extractActiveChangelog(rawContent) {
  if (!rawContent || !rawContent.trim()) {
    return null;
  }

  const lines = rawContent.replace(/\r\n/g, "\n").split("\n");
  const sections = [];
  let currentHeader = null;
  let currentBullets = [];

  const flushSection = () => {
    if (currentBullets.length === 0) return;
    const block = currentHeader
      ? `${currentHeader}\n${currentBullets.join("\n")}`
      : currentBullets.join("\n");
    sections.push(block);
  };

  for (const rawLine of lines) {
    const line = rawLine.trimEnd();
    if (/^#\s+Unreleased$/i.test(line)) {
      continue;
    }

    if (/^###\s+/.test(line)) {
      flushSection();
      currentHeader = line;
      currentBullets = [];
      continue;
    }

    if (/^-\s+\S/.test(line)) {
      currentBullets.push(line);
    } else if (currentBullets.length > 0 && /^\s{2,}\S/.test(line)) {
      currentBullets.push(line);
    }
  }

  flushSection();
  return sections.length > 0 ? sections.join("\n\n") : null;
}

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

function writeJson(filePath, data) {
  fs.writeFileSync(filePath, JSON.stringify(data, null, 2) + "\n", "utf8");
}

function toPosixRelative(rootDir, absPath) {
  return path.relative(rootDir, absPath).split(path.sep).join("/");
}

/**
 * Bumps versions, updates seraphui dependencies, freezes per-mod changelogs, and builds the release manifest.
 */
export function runReleaseBump({ rootDir, selectedMods, bumpType, releaseDate }) {
  if (!VALID_BUMPS.has(bumpType)) {
    throw new Error(`Unsupported bump type: ${bumpType}`);
  }

  const normalizedSet = new Set(
    selectedMods.map(id => id.trim().toLowerCase()).filter(Boolean)
  );
  const orderedSelected = MOD_ORDER.filter(id => normalizedSet.has(id));
  if (orderedSelected.length === 0) {
    throw new Error("At least one mod must be selected for release.");
  }

  const isDev = bumpType === "dev";
  const dateStr = releaseDate || new Date().toISOString().slice(0, 10);
  const changedFiles = new Set();
  const releases = [];
  let newSeraphUiVersion = null;

  for (const modId of orderedSelected) {
    const spec = MOD_CATALOG[modId];
    const modinfoPath = path.join(rootDir, spec.dir, "modinfo.json");
    const modinfo = readJson(modinfoPath);
    const nextVersion = bumpSemVer(modinfo.version, bumpType);
    const tag = `${modId}-v${nextVersion}`;

    modinfo.version = nextVersion;
    if (modId === "seraphui") {
      newSeraphUiVersion = nextVersion;
    } else if (newSeraphUiVersion && modinfo.dependencies && typeof modinfo.dependencies.seraphui === "string") {
      modinfo.dependencies.seraphui = newSeraphUiVersion;
    }

    writeJson(modinfoPath, modinfo);
    changedFiles.add(toPosixRelative(rootDir, modinfoPath));

    let notesFile = null;
    const changelogsDir = path.join(rootDir, spec.dir, "changelogs");
    const unreleasedPath = path.join(changelogsDir, "unreleased.md");
    if (fs.existsSync(unreleasedPath)) {
      const activeBody = extractActiveChangelog(fs.readFileSync(unreleasedPath, "utf8"));
      if (activeBody) {
        if (!isDev) {
          const frozenPath = path.join(changelogsDir, `v${nextVersion}.md`);
          fs.writeFileSync(
            frozenPath,
            `# Release v${nextVersion} (${dateStr})\n\n${activeBody}\n`,
            "utf8"
          );
          fs.writeFileSync(unreleasedPath, "# Unreleased\n", "utf8");
          changedFiles.add(toPosixRelative(rootDir, frozenPath));
          changedFiles.add(toPosixRelative(rootDir, unreleasedPath));
          notesFile = toPosixRelative(rootDir, frozenPath);
        } else {
          notesFile = toPosixRelative(rootDir, unreleasedPath);
        }
      }
    }

    if (!isDev) {
      const metadataPath = path.join(rootDir, spec.dir, "publisher", "metadata.md");
      if (fs.existsSync(metadataPath)) {
        const currentMeta = fs.readFileSync(metadataPath, "utf8");
        const updatedMeta = currentMeta.replace(
          /(\*\*Versión actual:\*\*\s*`)[^`]+(`)/,
          `$1${nextVersion}$2`
        );
        if (updatedMeta !== currentMeta) {
          fs.writeFileSync(metadataPath, updatedMeta, "utf8");
          changedFiles.add(toPosixRelative(rootDir, metadataPath));
        }
      }
    }

    releases.push({
      modId: spec.modId,
      name: spec.name,
      dir: spec.dir,
      version: nextVersion,
      tag,
      zipName: `${spec.modId}.zip`,
      notesFile
    });
  }

  // Propagate stable seraphui bump to unselected dependent mods too
  if (newSeraphUiVersion && !isDev) {
    for (const dependentId of ["tradersoverhaul", "seraphqol"]) {
      if (normalizedSet.has(dependentId)) continue;
      const depSpec = MOD_CATALOG[dependentId];
      const depModinfoPath = path.join(rootDir, depSpec.dir, "modinfo.json");
      const depModinfo = readJson(depModinfoPath);
      if (depModinfo.dependencies && depModinfo.dependencies.seraphui !== newSeraphUiVersion) {
        depModinfo.dependencies.seraphui = newSeraphUiVersion;
        writeJson(depModinfoPath, depModinfo);
        changedFiles.add(toPosixRelative(rootDir, depModinfoPath));
      }
    }
  }

  const summaryParts = releases.map(r => `${r.modId} v${r.version}`);
  return {
    isDev,
    commitMessage: `chore(release): ${summaryParts.join(", ")}`,
    changedFiles: Array.from(changedFiles),
    releases
  };
}

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    const token = argv[i];
    if (token === "--mods") args.mods = argv[++i];
    else if (token === "--bump") args.bump = argv[++i];
    else if (token === "--date") args.date = argv[++i];
    else if (token === "--root") args.root = argv[++i];
    else if (token === "--manifest") args.manifest = argv[++i];
  }
  return args;
}

const isEntrypoint =
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);

if (isEntrypoint) {
  const args = parseArgs(process.argv.slice(2));
  const rootDir = path.resolve(args.root || path.join(path.dirname(fileURLToPath(import.meta.url)), ".."));
  const selectedMods = (args.mods || "").split(",").map(s => s.trim()).filter(Boolean);
  const manifest = runReleaseBump({
    rootDir,
    selectedMods,
    bumpType: args.bump || "dev",
    releaseDate: args.date
  });

  const jsonOut = JSON.stringify(manifest, null, 2) + "\n";
  if (args.manifest) {
    fs.writeFileSync(args.manifest, jsonOut, "utf8");
  } else {
    process.stdout.write(jsonOut);
  }
}
