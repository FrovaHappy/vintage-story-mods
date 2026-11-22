import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { bumpSemVer, extractActiveChangelog, runReleaseBump } from "./bump-mods.mjs";

function createTempRepoFixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "vs-mods-release-"));
  const mods = [
    {
      dir: "mods/system-ui",
      modinfo: {
        type: "code",
        modid: "seraphui",
        name: "Seraph UI",
        version: "0.1.0",
        dependencies: { game: "1.22.0" }
      },
      unreleased: "# Unreleased\n\n### Added\n- **Theme helper (`SystemUITheme`)**: Added accent color tokens.\n",
      metadata: "# Publisher Metadata\n- **Versión actual:** `0.1.0`\n"
    },
    {
      dir: "mods/comerciantes",
      modinfo: {
        type: "code",
        modid: "tradersoverhaul",
        name: "Traders Overhaul",
        version: "0.1.0",
        dependencies: { game: "1.22.0", survival: "1.22.0", seraphui: "0.1.0" }
      },
      unreleased: "# Unreleased\n",
      metadata: "# Publisher Metadata\n- **Versión actual:** `0.1.0`\n"
    },
    {
      dir: "mods/seraph-qol",
      modinfo: {
        type: "code",
        modid: "seraphqol",
        name: "Seraph QoL",
        version: "0.1.1-dev.1",
        dependencies: { game: "1.22.0", survival: "1.22.0", seraphui: "0.1.0" }
      },
      unreleased: "# Unreleased\n\n### Added\n- **Inventory selector hatching (`InventoryButtonsComposer`)**: Diagonal hatch on inactive slots.\n",
      metadata: "# Publisher Metadata\n- **Versión actual:** `0.1.0`\n"
    }
  ];

  for (const mod of mods) {
    const modDir = path.join(root, mod.dir);
    fs.mkdirSync(path.join(modDir, "changelogs"), { recursive: true });
    fs.mkdirSync(path.join(modDir, "publisher"), { recursive: true });
    fs.writeFileSync(path.join(modDir, "modinfo.json"), JSON.stringify(mod.modinfo, null, 2) + "\n");
    fs.writeFileSync(path.join(modDir, "changelogs", "unreleased.md"), mod.unreleased);
    fs.writeFileSync(path.join(modDir, "publisher", "metadata.md"), mod.metadata);
  }

  return root;
}

test("bumpSemVer handles stable and dev transitions", () => {
  assert.equal(bumpSemVer("0.1.0", "fix"), "0.1.1");
  assert.equal(bumpSemVer("0.1.0", "minor"), "0.2.0");
  assert.equal(bumpSemVer("0.1.0", "major"), "1.0.0");
  assert.equal(bumpSemVer("0.1.0", "dev"), "0.1.1-dev.0");
  assert.equal(bumpSemVer("0.1.1-dev.0", "dev"), "0.1.1-dev.1");
  assert.equal(bumpSemVer("0.1.1-dev.2", "fix"), "0.1.1");
  assert.equal(bumpSemVer("0.2.0-dev.1", "minor"), "0.2.0");
  assert.equal(bumpSemVer("0.1.1-dev.1", "minor"), "0.2.0");
  assert.equal(bumpSemVer("2.0.0-dev.3", "major"), "2.0.0");
  assert.equal(bumpSemVer("1.2.0-dev.0", "major"), "2.0.0");
});

test("extractActiveChangelog strips empty sections and returns null when no bullets exist", () => {
  assert.equal(extractActiveChangelog("# Unreleased\n\n### Added\n- \n\n### Fixed\n"), null);

  const parsed = extractActiveChangelog("# Unreleased\n\n### Added\n- **Item (`Comp`)**: Did thing.\n\n### Fixed\n");
  assert.equal(parsed, "### Added\n- **Item (`Comp`)**: Did thing.");
});

test("runReleaseBump updates selected mod, freezes changelog, and keeps fixed zipName", () => {
  const root = createTempRepoFixture();
  try {
    const manifest = runReleaseBump({
      rootDir: root,
      selectedMods: ["seraphqol"],
      bumpType: "fix",
      releaseDate: "2026-11-21"
    });

    assert.equal(manifest.releases.length, 1);
    assert.deepEqual(manifest.releases[0], {
      modId: "seraphqol",
      name: "Seraph QoL",
      dir: "mods/seraph-qol",
      bumpType: "fix",
      isDev: false,
      version: "0.1.1",
      tag: "seraphqol-v0.1.1",
      zipName: "seraphqol.zip",
      notesFile: "mods/seraph-qol/changelogs/v0.1.1.md"
    });

    const modinfo = JSON.parse(fs.readFileSync(path.join(root, "mods/seraph-qol/modinfo.json"), "utf8"));
    assert.equal(modinfo.version, "0.1.1");

    const frozen = fs.readFileSync(path.join(root, "mods/seraph-qol/changelogs/v0.1.1.md"), "utf8");
    assert.match(frozen, /^# Release v0\.1\.1 \(2026-11-21\)/);
    assert.match(frozen, /Inventory selector hatching/);

    const resetUnreleased = fs.readFileSync(path.join(root, "mods/seraph-qol/changelogs/unreleased.md"), "utf8");
    assert.equal(resetUnreleased, "# Unreleased\n");

    const metadata = fs.readFileSync(path.join(root, "mods/seraph-qol/publisher/metadata.md"), "utf8");
    assert.match(metadata, /\*\*Versión actual:\*\* `0\.1\.1`/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("runReleaseBump propagates seraphui version to dependent mods automatically", () => {
  const root = createTempRepoFixture();
  try {
    const manifest = runReleaseBump({
      rootDir: root,
      selectedMods: ["seraphui"],
      bumpType: "minor",
      releaseDate: "2026-11-21"
    });

    assert.equal(manifest.releases.length, 1);
    assert.equal(manifest.releases[0].tag, "seraphui-v0.2.0");

    const tradersModinfo = JSON.parse(fs.readFileSync(path.join(root, "mods/comerciantes/modinfo.json"), "utf8"));
    const qolModinfo = JSON.parse(fs.readFileSync(path.join(root, "mods/seraph-qol/modinfo.json"), "utf8"));
    assert.equal(tradersModinfo.dependencies.seraphui, "0.2.0");
    assert.equal(qolModinfo.dependencies.seraphui, "0.2.0");
    assert.ok(manifest.changedFiles.includes("mods/comerciantes/modinfo.json"));
    assert.ok(manifest.changedFiles.includes("mods/seraph-qol/modinfo.json"));
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("runReleaseBump dev release does not freeze unreleased.md or leak dev dep to unselected mods", () => {
  const root = createTempRepoFixture();
  try {
    const manifest = runReleaseBump({
      rootDir: root,
      selectedMods: ["seraphui", "seraphqol"],
      bumpType: "dev",
      releaseDate: "2026-11-21"
    });

    assert.equal(manifest.releases[0].version, "0.1.1-dev.0");
    assert.equal(manifest.releases[0].tag, "seraphui-v0.1.1-dev.0");
    assert.equal(manifest.releases[0].isDev, true);
    assert.equal(manifest.releases[1].version, "0.1.1-dev.2");
    assert.equal(manifest.releases[1].tag, "seraphqol-v0.1.1-dev.2");
    assert.equal(manifest.releases[1].isDev, true);
    assert.equal(manifest.releases[1].notesFile, "mods/seraph-qol/changelogs/unreleased.md");

    // Selected dependent mod gets dev seraphui dep; unselected tradersoverhaul stays at 0.1.0
    const qolModinfo = JSON.parse(fs.readFileSync(path.join(root, "mods/seraph-qol/modinfo.json"), "utf8"));
    const tradersModinfo = JSON.parse(fs.readFileSync(path.join(root, "mods/comerciantes/modinfo.json"), "utf8"));
    assert.equal(qolModinfo.dependencies.seraphui, "0.1.1-dev.0");
    assert.equal(tradersModinfo.dependencies.seraphui, "0.1.0");
    assert.equal(fs.existsSync(path.join(root, "mods/seraph-qol/changelogs/v0.1.1-dev.2.md")), false);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("runReleaseBump supports per-mod bump types with none skipped", () => {
  const root = createTempRepoFixture();
  try {
    const manifest = runReleaseBump({
      rootDir: root,
      modBumps: {
        seraphui: "fix",
        tradersoverhaul: "fix",
        seraphqol: "minor"
      },
      releaseDate: "2026-11-22"
    });

    assert.equal(manifest.releases.length, 3);
    assert.equal(manifest.releases[0].modId, "seraphui");
    assert.equal(manifest.releases[0].version, "0.1.1");
    assert.equal(manifest.releases[0].isDev, false);
    assert.equal(manifest.releases[1].modId, "tradersoverhaul");
    assert.equal(manifest.releases[1].version, "0.1.1");
    assert.equal(manifest.releases[1].isDev, false);
    assert.equal(manifest.releases[2].modId, "seraphqol");
    assert.equal(manifest.releases[2].version, "0.2.0");
    assert.equal(manifest.releases[2].isDev, false);

    const tradersModinfo = JSON.parse(fs.readFileSync(path.join(root, "mods/comerciantes/modinfo.json"), "utf8"));
    const qolModinfo = JSON.parse(fs.readFileSync(path.join(root, "mods/seraph-qol/modinfo.json"), "utf8"));
    assert.equal(tradersModinfo.dependencies.seraphui, "0.1.1");
    assert.equal(qolModinfo.dependencies.seraphui, "0.1.1");
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("runReleaseBump isolates mixed dev and stable releases per mod and rejects all none", () => {
  const root = createTempRepoFixture();
  try {
    assert.throws(() => {
      runReleaseBump({
        rootDir: root,
        modBumps: {
          seraphui: "none",
          tradersoverhaul: "none",
          seraphqol: "none"
        },
        releaseDate: "2026-11-22"
      });
    }, /At least one mod/i);

    const manifest = runReleaseBump({
      rootDir: root,
      modBumps: {
        seraphui: "dev",
        tradersoverhaul: "fix",
        seraphqol: "none"
      },
      releaseDate: "2026-11-22"
    });

    assert.equal(manifest.releases.length, 2);
    assert.equal(manifest.releases[0].modId, "seraphui");
    assert.equal(manifest.releases[0].isDev, true);
    assert.equal(manifest.releases[0].version, "0.1.1-dev.0");
    assert.equal(manifest.releases[1].modId, "tradersoverhaul");
    assert.equal(manifest.releases[1].isDev, false);
    assert.equal(manifest.releases[1].version, "0.1.1");

    // Stable release must not depend on dev seraphui; skipped none mod stays untouched
    const tradersModinfo = JSON.parse(fs.readFileSync(path.join(root, "mods/comerciantes/modinfo.json"), "utf8"));
    const qolModinfo = JSON.parse(fs.readFileSync(path.join(root, "mods/seraph-qol/modinfo.json"), "utf8"));
    assert.equal(tradersModinfo.dependencies.seraphui, "0.1.0");
    assert.equal(qolModinfo.version, "0.1.1-dev.1");
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
