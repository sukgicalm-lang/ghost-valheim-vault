// Verify the published 1.0.6 install path with the proposed public feed metadata.
import 'fake-indexeddb/auto';
import { beforeEach, afterEach, test, expect, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import * as path from 'node:path';
import axios from 'axios';
import * as Db from '../src/r2mm/manager/PackageDexieStore';
import { GHOST_PACKAGE, GHOST_SMELTER_PACKAGE, ghostPackageFromReleases } from '../src/ghost/GhostRepositoryCore';
import { getFullDependencyList, InstallMode } from '../src/utils/DependencyUtils';
import ThunderstoreCombo from '../src/model/ThunderstoreCombo';
import ManifestV2 from '../src/model/ManifestV2';
import VersionNumber from '../src/model/VersionNumber';
import ProfileModList from '../src/r2mm/mods/ProfileModList';
import ProfileInstallerProvider from '../src/providers/ror2/installing/ProfileInstallerProvider';
import { providePathImplementation } from '../src/providers/node/path/path';
import PathResolver from '../src/r2mm/manager/PathResolver';
import { installModsToProfile } from '../src/utils/ProfileUtils';
vi.mock('../src/i18n/instance', () => ({ i18n: { global: { t: (s: string) => s } } }));
vi.mock('../src/utils/DownloadUtils', () => ({ wasDownloadedFromOnline: vi.fn().mockResolvedValue(false) }));
const read = (file: string) => JSON.parse(readFileSync(new URL('../../../docs/' + file, import.meta.url), 'utf8'));
const suite = read('release.json'), catalog = read('releases.json'), smelter = read('smelter-release.json');
const game = { internalFolderName: 'Valheim' } as any;
const profile = {} as any;
function ordinary(fullName: string, version: string) {
    const p = ghostPackageFromReleases([{ ...suite, version, file: `downloads/fixture-${version}.zip`, dependencies: [] }]);
    const [owner, name] = fullName.split('-');
    return { ...p, owner, name, full_name: fullName, versions: p.versions.map(v => ({ ...v, name, full_name: fullName + '-' + version, dependencies: [] })) };
}
function manifest(name: string, version: string) { const m = new ManifestV2(); m.setName(name); m.setVersionNumber(new VersionNumber(version)); return m; }
async function seedPrerequisites() {
    await Db.upsertPackageListChunk('Valheim', suite.dependencies.filter((d: string) => !d.startsWith(GHOST_SMELTER_PACKAGE + '-')).map((d: string) => {
        const version = d.slice(d.lastIndexOf('-') + 1), name = d.slice(0, d.lastIndexOf('-')); return ordinary(name, version);
    }));
}
async function resolveMain(installed: ManifestV2[] = [], mode = InstallMode.INSTALL_SPECIFIC) {
    await seedPrerequisites();
    vi.spyOn(axios, 'get').mockImplementation(async (url: any) => ({ data: String(url).endsWith('/smelter-release.json') ? smelter : String(url).endsWith('/smelter-releases.json') ? [smelter] : String(url).endsWith('/release.json') ? suite : catalog }));
    const combo = await ThunderstoreCombo.fromProtocol('ghostvault://v1/install/Ghost-GhostValheimSuite/latest', game);
    expect(combo).toBeInstanceOf(ThunderstoreCombo);
    return getFullDependencyList([combo as ThunderstoreCombo], game, installed, mode);
}
beforeEach(async () => { vi.restoreAllMocks(); await Db.resetCommunity('Valheim'); providePathImplementation(() => path); PathResolver.MOD_ROOT = '/tmp/ghost-required-test'; });
afterEach(() => vi.restoreAllMocks());
test('latest feed and version history both require the smelter without changing suite archive identity', () => {
    expect(suite.dependencies).toContain(GHOST_SMELTER_PACKAGE + '-1.0.0');
    expect(catalog.find((r: any) => r.version === suite.version).dependencies).toEqual(suite.dependencies);
    expect(suite.version).toBe('1.0.40');
    expect(suite.bytes).toBe(398775554);
    expect(suite.sha256).toBe('0c4d2677828c48e9ce38acd528f8cfa09188e83858e1d88f7f4568cb7020dd54');
});
test('the single website suite protocol resolves BOTH mandatory packages through the real dependency resolver', async () => {
    const combos = await resolveMain();
    const names = combos.map(c => c.getMod().getFullName());
    expect(names.filter(n => n === GHOST_PACKAGE)).toHaveLength(1);
    expect(names.filter(n => n === GHOST_SMELTER_PACKAGE)).toHaveLength(1);
    expect(names.indexOf(GHOST_SMELTER_PACKAGE)).toBeLessThan(names.indexOf(GHOST_PACKAGE));
    expect(combos.find(c => c.getMod().getFullName() === GHOST_SMELTER_PACKAGE)!.getVersion().getGhostSha256()).toBe(smelter.sha256);
});
test('an existing current suite still resolves the missing mandatory smelter and installs it without replacing the suite', async () => {
    const old = [manifest(GHOST_PACKAGE, suite.version), ...suite.dependencies.filter((d: string) => !d.startsWith(GHOST_SMELTER_PACKAGE + '-')).map((d: string) => manifest(d.slice(0, d.lastIndexOf('-')), d.slice(d.lastIndexOf('-') + 1)))];
    const combos = await resolveMain(old);
    expect(combos.map(c => c.getMod().getFullName())).toEqual(expect.arrayContaining([GHOST_PACKAGE, GHOST_SMELTER_PACKAGE]));
    vi.spyOn(ProfileModList, 'getModList').mockResolvedValue(old);
    vi.spyOn(ProfileModList, 'saveModList').mockResolvedValue(undefined as any);
    const install = vi.fn().mockResolvedValue(null), uninstall = vi.fn().mockResolvedValue(null);
    vi.spyOn(ProfileInstallerProvider, 'instance', 'get').mockReturnValue({ installMod: install, uninstallMod: uninstall, disableMod: vi.fn(), enableMod: vi.fn() } as any);
    const result = await installModsToProfile(combos, profile);
    expect(install.mock.calls.map(([m]) => m.getName())).toEqual([GHOST_SMELTER_PACKAGE]);
    expect(uninstall.mock.calls.map(([m]) => m.getName())).toEqual([GHOST_SMELTER_PACKAGE]);
    expect(result.find(m => m.getName() === GHOST_SMELTER_PACKAGE)!.isEnabled()).toBe(true);
    expect(result.find(m => m.getName() === GHOST_PACKAGE)).toBe(old[0]);
});
test('already installed required packages are not duplicated by the normal install path', async () => {
    const installed = [manifest(GHOST_PACKAGE, suite.version), manifest(GHOST_SMELTER_PACKAGE, smelter.version)];
    const combos = await resolveMain(installed);
    expect(combos.filter(c => c.getMod().getFullName() === GHOST_SMELTER_PACKAGE)).toHaveLength(0);
});
test('Update All retains the required smelter dependency when updating the suite', async () => {
    const combos = await resolveMain([manifest(GHOST_PACKAGE, '1.0.39')], InstallMode.UPDATE_ALL);
    expect(combos.some(c => c.getMod().getFullName() === GHOST_SMELTER_PACKAGE)).toBe(true);
});
test('ordinary mod installation remains independent of Ghost setup', async () => {
    await Db.upsertPackageListChunk('Valheim', [ordinary('Other-Example', '1.0.0')]);
    const combos = await Db.getCombosByDependencyStrings(game, ['Other-Example-1.0.0']);
    expect((await getFullDependencyList(combos, game, [], InstallMode.INSTALL_SPECIFIC)).map(c => c.getMod().getFullName())).toEqual(['Other-Example']);
});
